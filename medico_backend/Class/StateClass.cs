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
        public async Task<int> GetNextStateCode()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(statecode), 0) + 1
                           FROM state_master";

            return await db.ExecuteScalarAsync<int>(sql);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(StateMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.statecode = await GetNextStateCode();
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
        public async Task<string> Update(StateMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

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
        // DELETE (SOFT DELETE)
        // ─────────────────────────────────────────
        public async Task<string> Delete(int statecode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE state_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE statecode = @statecode";

                await db.ExecuteAsync(sql, new { statecode });

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
        public async Task<List<StateMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM state_master
                           WHERE deleted = false
                           ORDER BY statecode";

            var result = await db.QueryAsync<StateMasterModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY STATECODE
        // ─────────────────────────────────────────
        public async Task<StateMasterModel?> GetByStateCode(int statecode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM state_master
                           WHERE deleted = false
                           AND statecode = @statecode";

            return await db.QueryFirstOrDefaultAsync<StateMasterModel>(
                sql,
                new { statecode });
        }

        // ─────────────────────────────────────────
        // SEARCH BY STATE NAME
        // ─────────────────────────────────────────
        public async Task<List<StateMasterModel>> SearchByStateName(string statename)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM state_master
                           WHERE deleted = false
                           AND LOWER(statename) LIKE LOWER(@statename)
                           ORDER BY statename";

            var result = await db.QueryAsync<StateMasterModel>(
                sql,
                new { statename = $"%{statename}%" });

            return result.ToList();
        }
    }
}