using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Medico_Backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class UnbilledChargesClass
    {
        private readonly string _conn;
        public UnbilledChargesClass(IConfiguration cfg) =>
            _conn = cfg.GetConnectionString("conn")!;

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ── Add consultation fee after OP confirmation ──────────────
        public async Task<string> AddConsultationCharge(AddUnbilledConsultationRequest req, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                // Guard against double-adding consultation charge for the same OP visit
                int already = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM unbilledcharges
                      WHERE opvisitid = @op_id AND entrytype = 'CONSULTATION'
                      AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { req.op_id, tenant_code });

                if (already > 0)
                    return "Consultation charge already pending for this visit";

                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = "CONSULTATION",
                    entryid = req.op_id,
                    chargedate = DateTime.UtcNow,
                    custid = req.custid,
                    opvisitid = req.op_id,
                    tcode = req.tcode,
                    quantity = req.quantity,
                    rate = req.rate,
                    amount = req.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code
                };

                await db.InsertAsync(row);
                return $"Success|UnbilledId:{row.unbilledid}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Add investigation test charges (called from case sheet save, same tx) ──
        public async Task AddInvestigationCharges(
            IDbConnection db, IDbTransaction tx,
            string op_id, decimal custid, string tenant_code,
            IEnumerable<(string inv_det_id, int? test_code, decimal quantity, decimal? rate, decimal? amount)> tests)
        {
            foreach (var t in tests)
            {
                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = "INVESTIGATION",
                    entryid = t.inv_det_id,
                    chargedate = DateTime.UtcNow,
                    custid = custid,
                    opvisitid = op_id,
                    tcode = t.test_code,
                    quantity = (double?)t.quantity,
                    rate = (double?)t.rate,
                    amount = (double?)t.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code
                };
                await db.InsertAsync(row, tx);
            }
        }

        // ── Fetch pending unbilled charges for the billing screen ──
        public async Task<List<UnbilledChargeRow>> GetUnbilledByVisit(string opvisitid, string tenant_code)
        {
            using var db = GetConnection();
            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT * FROM unbilledcharges
                  WHERE opvisitid = @opvisitid AND tenant_code = @tenant_code
                  AND (billedstatus = false OR billedstatus IS NULL)
                  ORDER BY chargedate",
                new { opvisitid, tenant_code });
            return rows.ToList();
        }

        public async Task<List<UnbilledChargeRow>> GetUnbilledByCustomer(decimal custid, string tenant_code)
        {
            using var db = GetConnection();
            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT * FROM unbilledcharges
                  WHERE custid = @custid AND tenant_code = @tenant_code
                  AND (billedstatus = false OR billedstatus IS NULL)
                  ORDER BY chargedate",
                new { custid, tenant_code });
            return rows.ToList();
        }

        // ── Mark charges billed (called inside HmsBillingClass.CreateBill's own tx) ──
        public async Task MarkAsBilled(
            IDbConnection db, IDbTransaction tx,
            List<string> unbilledIds, string billno, string billid, string tenant_code)
        {
            if (unbilledIds == null || !unbilledIds.Any()) return;

            await db.ExecuteAsync(
                @"UPDATE unbilledcharges
                  SET billedstatus   = true,
                      billno         = @billno,
                      billid         = @billid,
                      billeddate     = @now,
                      billedquantity = quantity,
                      billedamount   = amount
                  WHERE unbilledid = ANY(@ids) AND tenant_code = @tenant_code",
                new { billno, billid, now = DateTime.UtcNow, ids = unbilledIds.ToArray(), tenant_code },
                tx);
        }
        public async Task AddInvestigationChargeRow(
    IDbConnection db, string op_id, decimal custid, string tenant_code,
    string entryId, int? testCode, decimal quantity, decimal? rate, decimal? amount)
        {
            var row = new UnbilledChargeRow
            {
                unbilledid = Guid.NewGuid().ToString(),
                entrytype = "INVESTIGATION",
                entryid = entryId,
                chargedate = DateTime.UtcNow,
                custid = custid,
                opvisitid = op_id,
                tcode = testCode,
                quantity = (double?)quantity,
                rate = (double?)rate,
                amount = (double?)amount,
                discount = 0,
                charityamount = 0,
                billedstatus = false,
                tenant_code = tenant_code
            };
            await db.InsertAsync(row);
        }
        // ── Compute chargeable "days" for a room stay ──────────────
        // Full 24h blocks = full days. Leftover <=12h = +0.5 day, >12h = +1 day.
        public static double ComputeChargeableDays(DateTime admitdate, DateTime endPoint)
        {
            var elapsed = endPoint - admitdate;
            if (elapsed <= TimeSpan.Zero) return 0;

            double totalHours = elapsed.TotalHours;
            double fullDays = Math.Floor(totalHours / 24.0);
            double remainderHours = totalHours - (fullDays * 24.0);

            double days = fullDays;
            if (remainderHours > 0)
                days += remainderHours <= 12.0 ? 0.5 : 1.0;

            return days;
        }

        // ── Push/refresh the room-type charge for one admission ────
        // Splits the day-charge into test_group_rates components for the
        // room's rmtcode. Safe to call repeatedly (daily) and again at
        // discharge — it only ever adds the incremental days since the
        // last accrual, per component.
        public async Task<string> AccrueIpRoomCharge(Guid ip_id, string tenant_code, DateTime? asOf = null)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var ip = await db.QueryFirstOrDefaultAsync(
                    @"SELECT ip_id, ip_no, custid, rmtcode, admitdate, dischargedate, ip_status
              FROM ip_registration
              WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
              FOR UPDATE",
                    new { ip_id, tenant_code }, tx);

                if (ip == null) { tx.Rollback(); return "IP Registration not found"; }
                if (ip.rmtcode == null) { tx.Rollback(); return "No room type set for this admission"; }

                DateTime admitdate = ip.admitdate;
                DateTime endPoint = ip.ip_status == "DISCHARGED"
                    ? (DateTime)(ip.dischargedate ?? asOf ?? DateTime.UtcNow)
                    : (asOf ?? DateTime.UtcNow);

                double totalDays = ComputeChargeableDays(admitdate, endPoint);
                if (totalDays <= 0) { tx.Rollback(); return "No chargeable days yet"; }

                var rateRows = (await db.QueryAsync<TestGroupRateModel>(
                    @"SELECT * FROM public.test_group_rates
              WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code",
                    new { rmtcode = ip.rmtcode, tenant_code }, tx)).ToList();

                if (!rateRows.Any()) { tx.Rollback(); return "No room charge split-up configured for this room type"; }

                // days already pushed to unbilledcharges for this stay (all components
                // move in lockstep, so the max across them is the authoritative figure)
                double alreadyCharged = await db.ExecuteScalarAsync<double?>(
                    @"SELECT COALESCE(MAX(COALESCE(billedquantity, quantity)), 0)
              FROM unbilledcharges
              WHERE ipvisitid = @ipvisitid AND entrytype = @entrytype AND tenant_code = @tenant_code",
                    new { ipvisitid = ip_id.ToString(), entrytype = IpEntryType.ROOM, tenant_code }, tx) ?? 0;

                double delta = Math.Round(totalDays - alreadyCharged, 2);
                if (delta <= 0) { tx.Rollback(); return "Already up to date, nothing new to charge"; }

                foreach (var r in rateRows)
                {
                    string entryId = $"{ip_id}|ROOM|{r.subtestcode}";

                    var openRow = await db.QueryFirstOrDefaultAsync<UnbilledChargeRow>(
                        @"SELECT * FROM unbilledcharges
                  WHERE ipvisitid = @ipvisitid AND entrytype = @entrytype AND entryid = @entryid
                  AND tenant_code = @tenant_code
                  AND (billedstatus = false OR billedstatus IS NULL)
                  FOR UPDATE",
                        new { ipvisitid = ip_id.ToString(), entrytype = IpEntryType.ROOM, entryid = entryId, tenant_code }, tx);

                    if (openRow != null)
                    {
                        double newQty = (openRow.quantity ?? 0) + delta;
                        await db.ExecuteAsync(
                            @"UPDATE unbilledcharges
                      SET quantity = @quantity, rate = @rate, amount = @amount, chargedate = @chargedate
                      WHERE unbilledid = @unbilledid AND tenant_code = @tenant_code",
                            new
                            {
                                quantity = newQty,
                                rate = (double?)r.testrate,
                                amount = newQty * (double?)r.testrate,
                                chargedate = DateTime.UtcNow,
                                unbilledid = openRow.unbilledid,
                                tenant_code
                            }, tx);
                    }
                    else
                    {
                        var row = new UnbilledChargeRow
                        {
                            unbilledid = Guid.NewGuid().ToString(),
                            entrytype = IpEntryType.ROOM,
                            entryid = entryId,
                            chargedate = DateTime.UtcNow,
                            custid = ip.custid,
                            ipvisitid = ip_id.ToString(),
                            tcode = r.subtestcode,
                            quantity = delta,
                            rate = (double?)r.testrate,
                            amount = delta * (double?)r.testrate,
                            discount = 0,
                            charityamount = 0,
                            billedstatus = false,
                            tenant_code = tenant_code
                        };
                        await db.InsertAsync(row, tx);
                    }
                }

                tx.Commit();
                return $"Success|DaysCharged:{totalDays}|Delta:{delta}";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }

        // ── Run for every currently-admitted patient (nightly job) ─
        public async Task<string> AccrueAllActiveIpRoomCharges(string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var activeIds = await db.QueryAsync<Guid>(
                @"SELECT ip_id FROM ip_registration
          WHERE ip_status = 'ADMITTED' AND isdeleted = false AND tenant_code = @tenant_code",
                new { tenant_code });

            int ok = 0, skipped = 0;
            foreach (var ip_id in activeIds)
            {
                var res = await AccrueIpRoomCharge(ip_id, tenant_code);
                if (res.StartsWith("Success")) ok++;
                else skipped++;
            }

            return $"Processed {ok + skipped} admissions — {ok} charged, {skipped} skipped";
        }
    }
}