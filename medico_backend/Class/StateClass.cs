using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class StateMasterClass
    {
        private readonly string db_conn;

        public StateMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT STATECODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextStateCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(statecode), 0) + 1
                           FROM state_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(StateMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.statecode = await GetNextStateCode(tenant_code);
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
        public async Task<string> Update(StateMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                var result = await db.UpdateAsync(data);

                return result ? "Success" : "No Data Found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(int statecode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE state_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE statecode = @statecode
                               AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { statecode, tenant_code });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (tenant + global)
        // ─────────────────────────────────────────
        public async Task<List<StateMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM state_master
                           WHERE deleted = false
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                           ORDER BY statecode";

            var result = await db.QueryAsync<StateMasterModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY STATECODE (tenant + global)
        // ─────────────────────────────────────────
        public async Task<StateMasterModel?> GetByStateCode(int statecode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM state_master
                           WHERE deleted = false
                           AND statecode = @statecode
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)";

            return await db.QueryFirstOrDefaultAsync<StateMasterModel>(
                sql, new { statecode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY STATE NAME (tenant + global)
        // ─────────────────────────────────────────
        public async Task<List<StateMasterModel>> SearchByStateName(string statename, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM state_master
                           WHERE deleted = false
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                           AND LOWER(statename) LIKE LOWER(@statename)
                           ORDER BY statename";

            var result = await db.QueryAsync<StateMasterModel>(
                sql, new { statename = $"%{statename}%", tenant_code });

            return result.ToList();
        }
    }
}