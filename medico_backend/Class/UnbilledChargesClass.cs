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
        public async Task AddInvestigationChargeRow(
    IDbConnection db, IDbTransaction tx, string? op_id, Guid? ip_id, decimal custid, string tenant_code,
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
                ip_id = ip_id,
                tcode = testCode,
                quantity = (double?)quantity,
                rate = (double?)rate,
                amount = (double?)amount,
                discount = 0,
                charityamount = 0,
                billedstatus = false,
                tenant_code = tenant_code
            };
            await db.InsertAsync(row, tx);
        }

        // ── Fetch pending unbilled charges for the billing screen ──
        public async Task<List<UnbilledChargeRow>> GetUnbilledByVisit(
            string? opvisitid, string? ip_id, string tenant_code)
        {
            using var db = GetConnection();
            db.Open();   // ✅ explicit open, same as GetAllOpList

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE ((@opvisitid IS NOT NULL AND u.opvisitid = @opvisitid)
                 OR (@ip_id IS NOT NULL AND u.ip_id = CAST(@ip_id AS uuid)))
          AND u.tenant_code = @tenant_code
          AND (u.billedstatus = false OR u.billedstatus IS NULL)
          ORDER BY u.chargedate",
                new { opvisitid, ip_id, tenant_code },
                commandTimeout: 60   // ✅ same timeout bump as GetAllOpList
            );
            return rows.ToList();
        }

        public async Task<List<UnbilledChargeRow>> GetUnbilledByCustomer(decimal custid, string tenant_code)
        {
            using var db = GetConnection();
            db.Open();

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE u.custid = @custid AND u.tenant_code = @tenant_code
          AND (u.billedstatus = false OR u.billedstatus IS NULL)
          ORDER BY u.chargedate",
                new { custid, tenant_code },
                commandTimeout: 60
            );
            return rows.ToList();
        }

        // ── Get all unbilled charges for a tenant, optionally narrowed by op_id or ip_id ──
        public async Task<List<UnbilledChargeRow>> GetAllUnbilled(
            string tenant_code, string? op_id = null, Guid? ip_id = null)
        {
            using var db = GetConnection();
            db.Open();

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE u.tenant_code = @tenant_code
          AND (@op_id IS NULL OR u.opvisitid = @op_id)
          AND (@ip_id IS NULL OR u.ip_id = @ip_id)
          ORDER BY u.chargedate",
                new { tenant_code, op_id, ip_id },
                commandTimeout: 60
            );
            return rows.ToList();
        }
        public async Task AddInvestigationChargeRow(
    IDbConnection db, string? op_id, Guid? ip_id, decimal custid, string tenant_code,
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
                ip_id = ip_id,              // NEW
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
        private static DateTime NormalizeUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        // ── Recalculate ROOMRENT for a stay, only the unbilled remainder ──
        public async Task<string> RecalculateRoomRent(Guid ip_id, string tenant_code, DateTime? asOf = null)
        {
            try
            {
                using var db = GetConnection();

                // AFTER
                var ip = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT admitdate, dischargedate, ip_status, rmtcode, bedcode, custid, billing_rmtcode
      FROM ip_registration
      WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                    new { ip_id, tenant_code });

                if (ip == null)
                    return $"IP Registration not found for ip_id='{ip_id}' tenant_code='{tenant_code}'";

                // Rate lookup always uses the billing type chosen at admission.
                // Falls back to the physical room type only for legacy rows where billing_rmtcode was never set.
                int? effectiveRateRmt = (int?)ip.billing_rmtcode ?? (int?)ip.rmtcode;

                DateTime admitdate = NormalizeUtc((DateTime)ip.admitdate);
                bool isDischarged = ip.ip_status == "DISCHARGED" && ip.dischargedate != null;
                DateTime cutoffEnd = isDischarged
                    ? NormalizeUtc((DateTime)ip.dischargedate)
                    : (asOf ?? DateTime.UtcNow);

                var transfers = (await db.QueryAsync<dynamic>(
                    @"SELECT transferdate, currentroom, transroom, currentbed, transbed
              FROM public.bed_transfer
              WHERE lastvisitid = @ipIdStr AND tenant_code = @tenant_code
              ORDER BY transferdate ASC",
                    new { ipIdStr = ip_id.ToString(), tenant_code })).ToList();

                // Build segments, marking each as "closed" (ended by a real transfer/discharge)
                // or "open" (still ongoing — this is the only one that gets refreshed)
                var segments = new List<(int? rmtcode, int? bedcode, DateTime from, DateTime to, bool isClosed)>();
                DateTime segStart = admitdate;
                int? segRmt = transfers.Count > 0 ? (int?)transfers[0].currentroom : (int?)ip.rmtcode;
                int? segBed = transfers.Count > 0 ? (int?)transfers[0].currentbed : (int?)ip.bedcode;

                foreach (var t in transfers)
                {
                    DateTime segEnd = NormalizeUtc((DateTime)t.transferdate);
                    if (segEnd > segStart) segments.Add((segRmt, segBed, segStart, segEnd, true));  // closed by transfer
                    segStart = segEnd;
                    segRmt = (int?)t.transroom;
                    segBed = (int?)t.transbed;
                }
                if (cutoffEnd > segStart)
                    segments.Add((segRmt, segBed, segStart, cutoffEnd, isDischarged)); // closed only if discharged
                // ══════════════ INSERT THE CLIP LOGIC HERE ══════════════
                DateTime? lastBilledCutoff = await db.ExecuteScalarAsync<DateTime?>(
                    @"SELECT MAX(chargedate) FROM unbilledcharges
                      WHERE entrytype = 'ROOMRENT' AND ip_id = @ip_id AND tenant_code = @tenant_code
                      AND billedstatus = true",
                    new { ip_id, tenant_code });

                if (lastBilledCutoff.HasValue)
                {
                    var cutoff = NormalizeUtc(lastBilledCutoff.Value);
                    segments = segments
                        .Where(s => s.to > cutoff)
                        .Select(s => s.from < cutoff ? (s.rmtcode, s.bedcode, cutoff, s.to, s.isClosed) : s)
                        .ToList();
                }
                int inserted = 0, skippedExisting = 0, skippedNoRoomType = 0;

                // AFTER
                foreach (var seg in segments)
                {
                    if (seg.rmtcode == null) continue;   // still used to confirm the physical segment is valid

                    decimal chargedDays = CalculateRoomRentDays(seg.from, seg.to);
                    if (chargedDays <= 0) continue;

                    // Rate resolved from billing type, NOT the physical segment's rmtcode.
                    var roomType = await db.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT roomrate FROM public.roomtype_master
          WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code
          AND (deleted IS NULL OR deleted = false)",
                        new { rmtcode = effectiveRateRmt, tenant_code });

                    if (roomType == null) { skippedNoRoomType++; continue; }
                    decimal roomRate = (decimal)(roomType.roomrate ?? 0);

                    if (seg.isClosed)
                    {
                        // Closed segment — insert ONCE, keyed on the fixed segment end time.
                        // Never touched again on future recalculates.
                        string closedKey = $"{ip_id}|SEG|{seg.to:o}|bed:{seg.bedcode}";
                        var already = await db.ExecuteScalarAsync<int>(
                            @"SELECT COUNT(1) FROM unbilledcharges WHERE entryid = @closedKey AND tenant_code = @tenant_code",
                            new { closedKey, tenant_code });

                        if (already > 0) { skippedExisting++; continue; }

                        await db.InsertAsync(new UnbilledChargeRow
                        {
                            unbilledid = Guid.NewGuid().ToString(),
                            entrytype = "ROOMRENT",
                            entryid = closedKey,
                            chargedate = seg.to,
                            custid = (decimal)ip.custid,
                            ip_id = ip_id,
                            bedcode = seg.bedcode,        // NEW
                                                          // AFTER (both places)
                            tcode = effectiveRateRmt,
                            quantity = (double)chargedDays,
                            rate = (double)roomRate,
                            amount = (double)(chargedDays * roomRate),
                            discount = 0,
                            charityamount = 0,
                            billedstatus = false,
                            tenant_code = tenant_code
                        });
                        inserted++;
                    }
                    else
                    {
                        // Open segment — the bed the patient is in RIGHT NOW.
                        // Delete only THIS marker's unbilled row and reinsert with fresh quantity.
                        string openKey = $"{ip_id}|OPEN|bed:{seg.bedcode}";

                        await db.ExecuteAsync(
                            @"DELETE FROM unbilledcharges
                      WHERE entryid = @openKey AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                            new { openKey, tenant_code });

                        await db.InsertAsync(new UnbilledChargeRow
                        {
                            unbilledid = Guid.NewGuid().ToString(),
                            entrytype = "ROOMRENT",
                            entryid = openKey,
                            chargedate = seg.to,
                            custid = (decimal)ip.custid,
                            ip_id = ip_id,
                            bedcode = seg.bedcode,        // NEW
                                                          // AFTER (both places)
                            tcode = effectiveRateRmt,
                            quantity = (double)chargedDays,
                            rate = (double)roomRate,
                            amount = (double)(chargedDays * roomRate),
                            discount = 0,
                            charityamount = 0,
                            billedstatus = false,
                            tenant_code = tenant_code
                        });
                        inserted++;
                    }
                }

                return $"Success|Segments:{segments.Count}|Inserted:{inserted}|SkippedExisting:{skippedExisting}|SkippedNoRoomType:{skippedNoRoomType}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Room-rent breakdown for a stay, with the charge-head/testfeegroup split ──
        // Main summary — one row per segment, driven by roomtype_master.roomrate
        public async Task<List<dynamic>> GetIpRoomRentSummary(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            string sql = @"
        SELECT uc.unbilledid, uc.chargedate, uc.tcode AS rmtcode, uc.quantity,
               uc.rate, uc.amount, uc.billedstatus, uc.bedcode,
               rm.name AS roomtype_name, rm.roomrate,
               bm.bedname, bm.shortname
        FROM unbilledcharges uc
        LEFT JOIN public.roomtype_master rm
               ON rm.rmtcode = uc.tcode AND rm.tenant_code = uc.tenant_code
        LEFT JOIN public.bed_master bm
               ON bm.bedcode = uc.bedcode AND bm.tenant_code = uc.tenant_code
        WHERE uc.entrytype = 'ROOMRENT' AND uc.ip_id = @ip_id
          AND uc.tenant_code = @tenant_code
        ORDER BY uc.chargedate";
            var res = await db.QueryAsync<dynamic>(sql, new { ip_id, tenant_code });
            return res.ToList();
        }

        // Optional display-only breakdown — how a room type's rate splits across charge-heads.
        // Not used for billing math, just for showing "what's included" if the UI wants it.
        public async Task<List<TestGroupRateModel>> GetTestGroupBreakdown(int rmtcode, string tenant_code)
        {
            using var db = GetConnection();
            var res = await db.QueryAsync<TestGroupRateModel>(
                @"SELECT * FROM public.test_group_rates
          WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code",
                new { rmtcode, tenant_code });
            return res.ToList();
        }

        // ── Void pending unbilled ROOMRENT charges for a cancelled admission ──
        // Billed rows (payment already collected) are left untouched — that's a refund concern.
        public async Task CloseUnbilledForIp(IDbConnection db, IDbTransaction tx, Guid ip_id, string tenant_code)
        {
            await db.ExecuteAsync(
                @"DELETE FROM unbilledcharges
          WHERE entrytype = 'ROOMRENT' AND ip_id = @ip_id
          AND tenant_code = @tenant_code AND (billedstatus = false OR billedstatus IS NULL)",
                new { ip_id, tenant_code }, tx);
        }

        // ── All charges for this IP stay have been billed? ──
        public async Task<bool> IsFullyBilled(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            int pending = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM unbilledcharges
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code
          AND (billedstatus = false OR billedstatus IS NULL)",
                new { ip_id, tenant_code });
            return pending == 0;
        }


        public async Task<bool> IsPaymentSettled(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            double? outstandingBalance = await db.ExecuteScalarAsync<double?>(
                @"SELECT COALESCE(SUM(totalamount - COALESCE(paidamount,0)), 0)
          FROM lab_request_master
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code
          AND (isdeleted = false OR isdeleted IS NULL)",
                new { ip_id, tenant_code });
            return (outstandingBalance ?? 0) <= 0.05;   // matches the 0.05 tolerance used elsewhere in HmsBillingClass
        }
              
    }
}