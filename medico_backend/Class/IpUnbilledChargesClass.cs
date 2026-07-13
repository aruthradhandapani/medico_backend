using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using medico_backend.Model;

namespace medico_backend.Class
{
    public class IpUnbilledChargeClass
    {
        private readonly string _db_conn;

        public IpUnbilledChargeClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // GENERIC INSERT — used by every charge type below.
        // Participates in the caller's transaction when one is passed.
        // ─────────────────────────────────────────
        private async Task<string> Insert(
            IDbConnection db, IDbTransaction? tx,
            string entrytype, string? entryid, Guid ip_id, decimal custid,
            int? tcode, double rate, double quantity,
            double discount, double charityamount,
            string tenant_code, DateTime? chargedate = null)
        {
            var row = new UnbilledChargeRow
            {
                unbilledid = Guid.NewGuid().ToString(),
                entrytype = entrytype,
                entryid = entryid,
                chargedate = chargedate ?? DateTime.UtcNow,
                custid = custid,
                ipvisitid = ip_id.ToString(),
                tcode = tcode,
                quantity = quantity,
                rate = rate,
                amount = Math.Round(rate * quantity, 2),
                discount = discount,
                charityamount = charityamount,
                billedstatus = false,
                tenant_code = tenant_code
            };

            if (tx != null)
                await db.InsertAsync(row, tx);
            else
                await db.InsertAsync(row);

            return row.unbilledid!;
        }

        // ─────────────────────────────────────────
        // ROOM CHARGE — closes a bed-stay segment and charges it.
        // Called from BedTransferClass (old bed) and IpRegistrationClass
        // (Discharge/CancelAdmission) inside their existing transactions.
        // Splits by rate: each transfer closes one segment at its own rate.
        // ─────────────────────────────────────────
        public async Task CloseRoomSegmentAndCharge(
            IDbConnection db, IDbTransaction tx,
            Guid ip_id, decimal custid, int bedcode, int rmtcode,
            DateTime admitdate, string tenant_code, DateTime segmentEnd, int roomTcode)
        {
            var lastTo = await db.ExecuteScalarAsync<DateTime?>(
                @"SELECT MAX(chargedate) FROM public.unbilledcharges
                  WHERE ipvisitid = @ip_id AND tenant_code = @tenant_code
                  AND entrytype = @entrytype",
                new { ip_id = ip_id.ToString(), tenant_code, entrytype = IpEntryType.ROOM }, tx);

            DateTime segmentStart = lastTo ?? admitdate;
            if (segmentEnd <= segmentStart) return;

            var room = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT roomrate FROM public.roomtype_master
                  WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code",
                new { rmtcode, tenant_code }, tx);

            double roomrate = (double?)room?.roomrate ?? 0;
            double noOfDays = Math.Max(1, Math.Ceiling((segmentEnd - segmentStart).TotalDays));

            await Insert(db, tx, IpEntryType.ROOM, bedcode.ToString(), ip_id, custid,
                roomTcode, roomrate, noOfDays, 0, 0, tenant_code, segmentEnd);
        }

        // ─────────────────────────────────────────
        // NURSING CHARGE — standalone call (e.g. nurse logs a shift charge)
        // ─────────────────────────────────────────
        public async Task<string> AddNursingCharge(AddIpNursingChargeRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var id = await Insert(db, null, IpEntryType.NURSING, null, req.ip_id, req.custid,
                req.tcode, req.rate, req.quantity, req.discount, req.charityamount, tenant_code);
            return id;
        }

        // ─────────────────────────────────────────
        // TEST/INVESTIGATION CHARGE — standalone call (lab test ordered for IP patient)
        // ─────────────────────────────────────────
        public async Task<string> AddTestCharge(AddIpTestChargeRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var id = await Insert(db, null, IpEntryType.INVESTIGATION, req.entryid, req.ip_id, req.custid,
                req.tcode, req.rate, req.quantity, req.discount, req.charityamount, tenant_code);
            return id;
        }

        // ─────────────────────────────────────────
        // GET ALL CHARGES FOR A STAY (any type)
        // ─────────────────────────────────────────
        public async Task<List<UnbilledChargeRow>> GetByIp(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            string sql = @"SELECT * FROM public.unbilledcharges
                           WHERE ipvisitid = @ip_id AND tenant_code = @tenant_code
                           ORDER BY chargedate";
            var res = await db.QueryAsync<UnbilledChargeRow>(sql, new { ip_id = ip_id.ToString(), tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET UNBILLED (optionally filtered by ip_id and/or entrytype)
        // ─────────────────────────────────────────
        public async Task<List<UnbilledChargeRow>> GetUnbilled(
            string tenant_code, Guid? ip_id = null, string? entrytype = null)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            string sql = @"SELECT * FROM public.unbilledcharges
                           WHERE tenant_code = @tenant_code
                           AND (billedstatus IS NULL OR billedstatus = false)
                           AND ipvisitid IS NOT NULL
                           AND (@ip_id IS NULL OR ipvisitid = @ip_id)
                           AND (@entrytype IS NULL OR entrytype = @entrytype)
                           ORDER BY chargedate";
            var res = await db.QueryAsync<UnbilledChargeRow>(sql, new
            {
                tenant_code,
                ip_id = ip_id.HasValue ? ip_id.Value.ToString() : null,
                entrytype
            });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // MARK BILLED
        // ─────────────────────────────────────────
        public async Task<string> MarkBilled(MarkIpChargesBilledRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            string sql = @"UPDATE public.unbilledcharges
                           SET billedstatus = true,
                               billno = @billno,
                               billid = @billid,
                               billeddate = now(),
                               billedquantity = @billedquantity,
                               billedamount = @billedamount
                           WHERE unbilledid = ANY(@unbilledids) AND tenant_code = @tenant_code";
            int rows = await db.ExecuteAsync(sql, new
            {
                req.billno,
                req.billid,
                req.billedquantity,
                req.billedamount,
                unbilledids = req.unbilledids.ToArray(),
                tenant_code
            });
            return rows > 0 ? "Success" : "No matching charges found";
        }
    }
}