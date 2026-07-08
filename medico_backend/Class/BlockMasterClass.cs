using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class BlockMasterClass
    {
        private readonly string db_conn;

        public BlockMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(BlockMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                var id = await db.InsertAsync(data);
                data.blockcode = id;

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
        public async Task<string> Update(BlockMasterModel data)
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
        public async Task<string> Delete(int blockcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.block_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE blockcode    = @blockcode
                               AND tenant_code    = @tenant_code";

                await db.ExecuteAsync(sql, new { blockcode, tenant_code });
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
        public async Task<List<BlockMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.block_master
                           WHERE deleted      = false
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BlockMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY BLOCKCODE
        // ─────────────────────────────────────────
        public async Task<BlockMasterModel?> GetByBlockcode(int blockcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.block_master
                           WHERE deleted    = false
                           AND blockcode    = @blockcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<BlockMasterModel>(
                sql, new { blockcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY BRANCH
        // ─────────────────────────────────────────
        public async Task<List<BlockMasterModel>> GetByBranch(int branchcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.block_master
                           WHERE deleted      = false
                           AND branchcode     = @branchcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BlockMasterModel>(sql, new { branchcode, tenant_code });
            return res.ToList();
        }
    }
}