using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CommissionGroupMasterClass
    {
        private readonly string db_conn;

        public CommissionGroupMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT CGCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextCgCode()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(cgcode), 0) + 1
                           FROM commissiongroup_master";

            return await db.ExecuteScalarAsync<decimal>(sql);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CommissionGroupMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.cgcode = await GetNextCgCode();
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO commissiongroup_master
                    (
                        cgcode,
                        orderno,
                        shortname,
                        name,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @cgcode,
                        @orderno,
                        @shortname,
                        @name,
                        @description,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate
                    )";

                await db.ExecuteAsync(sql, data);

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
        public async Task<string> Update(CommissionGroupMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE commissiongroup_master
                    SET
                        orderno = @orderno,
                        shortname = @shortname,
                        name = @name,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE cgcode = @cgcode";

                await db.ExecuteAsync(sql, data);

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(decimal cgcode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE commissiongroup_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE cgcode = @cgcode";

                await db.ExecuteAsync(sql, new { cgcode });

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
        public async Task<List<CommissionGroupMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissiongroup_master
                WHERE deleted = false
                ORDER BY cgcode";

            var result = await db.QueryAsync<CommissionGroupMasterModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CGCODE
        // ─────────────────────────────────────────
        public async Task<CommissionGroupMasterModel?> GetByCgCode(decimal cgcode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissiongroup_master
                WHERE deleted = false
                AND cgcode = @cgcode";

            return await db.QueryFirstOrDefaultAsync<CommissionGroupMasterModel>(
                sql,
                new { cgcode });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<CommissionGroupMasterModel>> SearchByName(string name)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissiongroup_master
                WHERE deleted = false
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY name";

            var result = await db.QueryAsync<CommissionGroupMasterModel>(
                sql,
                new { name = $"%{name}%" });

            return result.ToList();
        }
    }
}