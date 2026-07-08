using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class BedMasterClass
    {
        private readonly string db_conn;

        public BedMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(BedMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                var id = await db.InsertAsync(data);
                data.bedcode = id;

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(BedMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                var res = await db.UpdateAsync(data);
                return res ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(int bedcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.bed_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE bedcode      = @bedcode
                               AND tenant_code    = @tenant_code";

                await db.ExecuteAsync(sql, new { bedcode, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY BEDCODE
        // ─────────────────────────────────────────
        public async Task<BedMasterModel?> GetByBedcode(int bedcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND bedcode      = @bedcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<BedMasterModel>(
                sql, new { bedcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY BRANCH
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> GetByBranch(int branchcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND branchcode     = @branchcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { branchcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY WARD
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> GetByWard(int wrdcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND wrdcode        = @wrdcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { wrdcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY ROOM TYPE
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> GetByRoomType(int rmtcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND rmtcode        = @rmtcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { rmtcode, tenant_code });
            return res.ToList();
        }
    }
}