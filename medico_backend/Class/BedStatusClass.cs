using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using medico_backend.Model;

namespace medico_backend.Class
{
    public class BedStatusClass
    {
        private readonly string _db_conn;

        public BedStatusClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // LOG OCCUPIED (called on admit / transfer-in)
        // Takes an existing connection + transaction so it participates
        // in the caller's transaction (IpRegistrationClass / BedTransferClass).
        // ─────────────────────────────────────────
        public async Task InsertOccupied(
            IDbConnection db, IDbTransaction tx,
            int bedcode, Guid ip_id, string ip_no, decimal custid,
            DateTime admitted_at, string tenant_code)
        {
            var row = new BedStatusModel
            {
                bed_status_id = Guid.NewGuid(),
                bedcode = bedcode,
                ip_id = ip_id,
                ip_no = ip_no,
                custid = custid,
                status = "OCCUPIED",
                admitted_at = admitted_at,
                is_cleaned = false,
                tenant_code = tenant_code,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            await db.InsertAsync(row, tx);
        }

        // ─────────────────────────────────────────
        // LOG VACANT (called on discharge / cancel / transfer-out)
        // Marks the latest OCCUPIED row for that bed+ip_id as vacated, needing cleaning.
        // Takes an existing connection + transaction so it participates
        // in the caller's transaction.
        // ─────────────────────────────────────────
        public async Task MarkVacant(
            IDbConnection db, IDbTransaction tx,
            int bedcode, Guid ip_id, string tenant_code)
        {
            await db.ExecuteAsync(@"
                UPDATE public.bed_status
                SET status = 'VACANT',
                    discharged_at = now(),
                    is_cleaned = false,
                    updated_at = now()
                WHERE bedcode = @bedcode AND ip_id = @ip_id
                AND tenant_code = @tenant_code AND status = 'OCCUPIED'",
                new { bedcode, ip_id, tenant_code }, tx);
        }

        // ─────────────────────────────────────────
        // MARK BED CLEANED (housekeeping confirms — bed becomes available)
        // Standalone call — opens its own connection since it's not part
        // of an admit/discharge/transfer transaction.
        // ─────────────────────────────────────────
        public async Task<string> MarkCleaned(MarkBedCleanedRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
                UPDATE public.bed_status
                SET is_cleaned = true,
                    status = 'AVAILABLE',
                    cleaned_at = now(),
                    cleaned_by = @cleaned_by,
                    notes = COALESCE(@notes, notes),
                    updated_at = now()
                WHERE bed_status_id = (
                    SELECT bed_status_id FROM public.bed_status
                    WHERE bedcode = @bedcode AND tenant_code = @tenant_code
                    ORDER BY created_at DESC LIMIT 1
                )";

            int rows = await db.ExecuteAsync(sql, new
            {
                req.bedcode,
                req.cleaned_by,
                req.notes,
                tenant_code
            });

            return rows > 0 ? "Success" : "No pending status row found for this bed";
        }

        // ─────────────────────────────────────────
        // GET FULL HISTORY FOR A BED
        // ─────────────────────────────────────────
        public async Task<List<BedStatusModel>> GetByBed(int bedcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM public.bed_status
                           WHERE bedcode = @bedcode AND tenant_code = @tenant_code
                           ORDER BY created_at DESC";

            var res = await db.QueryAsync<BedStatusModel>(sql, new { bedcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET HISTORY FOR A PATIENT ADMISSION
        // ─────────────────────────────────────────
        public async Task<List<BedStatusModel>> GetByIpId(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM public.bed_status
                           WHERE ip_id = @ip_id AND tenant_code = @tenant_code
                           ORDER BY created_at DESC";

            var res = await db.QueryAsync<BedStatusModel>(sql, new { ip_id, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BEDS PENDING CLEANING (vacated but not yet cleaned)
        // ─────────────────────────────────────────
        public async Task<List<dynamic>> GetPendingCleaning(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
                SELECT bs.bed_status_id, bs.bedcode, bm.bedname, bm.wrdcode, bm.flrcode,
                       bs.ip_no, bs.discharged_at
                FROM public.bed_status bs
                INNER JOIN public.bed_master bm ON bm.bedcode = bs.bedcode AND bm.tenant_code = bs.tenant_code
                WHERE bs.tenant_code = @tenant_code
                AND bs.status = 'VACANT'
                AND bs.is_cleaned = false
                AND bs.bed_status_id = (
                    SELECT bed_status_id FROM public.bed_status b2
                    WHERE b2.bedcode = bs.bedcode AND b2.tenant_code = bs.tenant_code
                    ORDER BY b2.created_at DESC LIMIT 1
                )
                ORDER BY bs.discharged_at";

            var res = await db.QueryAsync<dynamic>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET ALL (log view)
        // ─────────────────────────────────────────
        public async Task<List<BedStatusModel>> GetAll(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM public.bed_status
                           WHERE tenant_code = @tenant_code
                           ORDER BY created_at DESC";

            var res = await db.QueryAsync<BedStatusModel>(sql, new { tenant_code });
            return res.ToList();
        }
    }
}