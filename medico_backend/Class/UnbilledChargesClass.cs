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
        // ── Core billing rule — see algorithm notes ──
        public decimal CalculateRoomRentDays(DateTime roomEntryTime, DateTime currentTime)
        {
            var totalHours = (currentTime - roomEntryTime).TotalHours;
            if (totalHours < 12)
                return 0m;

            int fullDays = (int)(totalHours / 24);
            double remainingHours = totalHours % 24;

            decimal charge = fullDays;
            if (remainingHours >= 12)
                charge += 0.5m;

            return charge;
        }

        // ── Recalculate ROOMRENT for a stay, only the unbilled remainder ──
        public async Task<string> RecalculateRoomRent(Guid ip_id, string tenant_code, DateTime? asOf = null)
        {
            try
            {
                using var db = GetConnection();
                string ipIdStr = ip_id.ToString();

                var ip = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT admitdate, dischargedate, ip_status, rmtcode, custid
              FROM ip_registration
              WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                    new { ip_id, tenant_code });

                if (ip == null)
                    return $"IP Registration not found for ip_id='{ip_id}' tenant_code='{tenant_code}'";

                DateTime admitdate = (DateTime)ip.admitdate;
                DateTime cutoffEnd = ip.ip_status == "DISCHARGED" && ip.dischargedate != null
                    ? (DateTime)ip.dischargedate
                    : (asOf ?? DateTime.UtcNow);

                var transfers = (await db.QueryAsync<dynamic>(
                    @"SELECT transferdate, currentroom, transroom
              FROM public.bed_transfer
              WHERE lastvisitid = @ipIdStr AND tenant_code = @tenant_code
              ORDER BY transferdate ASC",
                    new { ipIdStr, tenant_code })).ToList();

                // Build segments — each transfer is a hard boundary; old room's segment
                // is frozen there, new room's segment starts its own clock from zero
                var segments = new List<(int? rmtcode, DateTime from, DateTime to)>();
                DateTime segStart = admitdate;
                int? segRmt = transfers.Count > 0 ? (int?)transfers[0].currentroom : (int?)ip.rmtcode;

                foreach (var t in transfers)
                {
                    DateTime segEnd = (DateTime)t.transferdate;
                    if (segEnd > segStart) segments.Add((segRmt, segStart, segEnd));
                    segStart = segEnd;
                    segRmt = (int?)t.transroom;
                }
                if (cutoffEnd > segStart) segments.Add((segRmt, segStart, cutoffEnd));

                // Don't re-charge periods already billed
                DateTime billedThrough = await db.ExecuteScalarAsync<DateTime?>(
                    @"SELECT MAX(chargedate) FROM unbilledcharges
              WHERE entrytype = 'ROOMRENT' AND opvisitid = @ipIdStr
              AND tenant_code = @tenant_code AND billedstatus = true",
                    new { ipIdStr, tenant_code }) ?? admitdate;

                var toCharge = segments
                    .Select(s => (s.rmtcode, from: s.from < billedThrough ? billedThrough : s.from, s.to))
                    .Where(s => s.to > s.from)
                    .ToList();

                // Wipe previously-projected (still unbilled) ROOMRENT rows — billed ones untouched
                await db.ExecuteAsync(
                    @"DELETE FROM unbilledcharges
              WHERE entrytype = 'ROOMRENT' AND opvisitid = @ipIdStr
              AND tenant_code = @tenant_code AND (billedstatus = false OR billedstatus IS NULL)",
                    new { ipIdStr, tenant_code });

                int totalRatesFound = 0, totalRowsInserted = 0;

                foreach (var seg in toCharge)
                {
                    if (seg.rmtcode == null) continue;

                    decimal chargedDays = CalculateRoomRentDays(seg.from, seg.to);
                    if (chargedDays <= 0) continue;   // < 12 hrs in this segment → free, nothing to insert

                    var rates = (await db.QueryAsync<TestGroupRateModel>(
                        @"SELECT * FROM public.test_group_rates
                  WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code",
                        new { seg.rmtcode, tenant_code })).ToList();

                    totalRatesFound += rates.Count;

                    foreach (var rate in rates)
                    {
                        var row = new UnbilledChargeRow
                        {
                            unbilledid = Guid.NewGuid().ToString(),
                            entrytype = "ROOMRENT",
                            entryid = $"{ipIdStr}|{rate.roomchargehead}|{rate.subtestcode}",
                            chargedate = seg.to,
                            custid = (decimal)ip.custid,
                            opvisitid = ipIdStr,
                            tcode = rate.subtestcode ?? rate.roomchargehead,
                            quantity = (double)chargedDays,
                            rate = rate.testrate,
                            amount = (double)chargedDays * (rate.testrate ?? 0),
                            discount = 0,
                            charityamount = 0,
                            billedstatus = false,
                            tenant_code = tenant_code
                        };
                        await db.InsertAsync(row);
                        totalRowsInserted++;
                    }
                }

                return $"Success|Segments:{toCharge.Count}|RatesFound:{totalRatesFound}|RowsInserted:{totalRowsInserted}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Room-rent breakdown for a stay, with the charge-head/testfeegroup split ──
        public async Task<List<dynamic>> GetIpRoomRentSummary(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            string ipIdStr = ip_id.ToString();
            string sql = @"
        SELECT uc.*, tgr.roomchargehead, tgr.subtestcode
        FROM unbilledcharges uc
        LEFT JOIN public.test_group_rates tgr
               ON tgr.subtestcode = uc.tcode AND tgr.tenant_code = uc.tenant_code
        WHERE uc.entrytype = 'ROOMRENT' AND uc.opvisitid = @ipIdStr
          AND uc.tenant_code = @tenant_code
        ORDER BY uc.chargedate";
            var res = await db.QueryAsync<dynamic>(sql, new { ipIdStr, tenant_code });
            return res.ToList();
        }

        // ── Void pending unbilled ROOMRENT charges for a cancelled admission ──
        // Billed rows (payment already collected) are left untouched — that's a refund concern.
        public async Task CloseUnbilledForIp(IDbConnection db, IDbTransaction tx, Guid ip_id, string tenant_code)
        {
            string ipIdStr = ip_id.ToString();
            await db.ExecuteAsync(
                @"DELETE FROM unbilledcharges
          WHERE entrytype = 'ROOMRENT' AND opvisitid = @ipIdStr
          AND tenant_code = @tenant_code AND (billedstatus = false OR billedstatus IS NULL)",
                new { ipIdStr, tenant_code }, tx);
        }

        // ── All charges for this IP stay have been billed? ──
        public async Task<bool> IsFullyBilled(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            string ipIdStr = ip_id.ToString();
            int pending = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM unbilledcharges
          WHERE opvisitid = @ipIdStr AND tenant_code = @tenant_code
          AND (billedstatus = false OR billedstatus IS NULL)",
                new { ipIdStr, tenant_code });
            return pending == 0;
        }


        public async Task<bool> IsPaymentSettled(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
  
            throw new NotImplementedException("Wire IsPaymentSettled to your bill/payment table");
        }
    }
}