using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class BedTransferClass
    {
        private readonly string db_conn;

        public BedTransferClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT (log a transfer / admission)
        // ─────────────────────────────────────────
        public async Task<string> Insert(BedTransferModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entereddate = DateTime.UtcNow;
                if (data.transferdate == null)
                    data.transferdate = DateTime.UtcNow;

                var id = await db.InsertAsync(data);
                data.transferid = id;

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL TRANSFERS FOR A PATIENT (by custid)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> GetByCustId(decimal custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE custid       = @custid
                           AND tenant_code    = @tenant_code
                           ORDER BY transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { custid, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY VISIT (lastvisitid)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> GetByVisit(string lastvisitid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE lastvisitid  = @lastvisitid
                           AND tenant_code    = @tenant_code
                           ORDER BY transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { lastvisitid, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET LATEST TRANSFER (current bed for a visit)
        // ─────────────────────────────────────────
        public async Task<BedTransferModel?> GetLatestByVisit(string lastvisitid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE lastvisitid  = @lastvisitid
                           AND tenant_code    = @tenant_code
                           ORDER BY transferdate DESC
                           LIMIT 1";

            return await db.QueryFirstOrDefaultAsync<BedTransferModel>(
                sql, new { lastvisitid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET ALL (paginated could be added later)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE tenant_code = @tenant_code
                           ORDER BY transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET CURRENTLY OCCUPIED BEDS (not checked out)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> GetActiveAdmissions(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT DISTINCT ON (lastvisitid) *
                           FROM public.bed_transfer
                           WHERE tenant_code = @tenant_code
                           ORDER BY lastvisitid, transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { tenant_code });
            return res.Where(x => x.ischeckout != true).ToList();
        }
    }
}