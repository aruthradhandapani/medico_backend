using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class RoomTypeMasterClass
    {
        private readonly string db_conn;

        public RoomTypeMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT RMTCODE (per tenant)
        // ─────────────────────────────────────────
        public async Task<int> GetNextRmtcode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(rmtcode), 0) + 1
                           FROM public.roomtype_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(RoomTypeMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.rmtcode = await GetNextRmtcode(data.tenant_code!);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                await db.InsertAsync(data);
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
        public async Task<string> Update(RoomTypeMasterModel data)
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
        public async Task<string> Delete(int rmtcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.roomtype_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE rmtcode      = @rmtcode
                               AND tenant_code     = @tenant_code";

                await db.ExecuteAsync(sql, new { rmtcode, tenant_code });
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
        public async Task<List<RoomTypeMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.roomtype_master
                           WHERE deleted      = false
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<RoomTypeMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY RMTCODE
        // ─────────────────────────────────────────
        public async Task<RoomTypeMasterModel?> GetByRmtcode(int rmtcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.roomtype_master
                           WHERE deleted    = false
                           AND rmtcode      = @rmtcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<RoomTypeMasterModel>(
                sql, new { rmtcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY BRANCH (branchcode)
        // ─────────────────────────────────────────
        public async Task<List<RoomTypeMasterModel>> GetByBranch(int branchcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.roomtype_master
                           WHERE deleted      = false
                           AND branchcode     = @branchcode
                           AND tenant_code     = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<RoomTypeMasterModel>(sql, new { branchcode, tenant_code });
            return res.ToList();
        }
    }
}