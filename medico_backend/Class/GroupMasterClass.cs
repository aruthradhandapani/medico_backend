using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class GroupMasterClass
    {
        private readonly string db_conn;

        public GroupMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT GCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextGCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(gcode), 0) + 1
                           FROM group_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(GroupMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.gcode = await GetNextGCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO group_master
                    (
                        gcode,
                        tenant_code,
                        dcode,
                        orderno,
                        name,
                        shortname,
                        description,
                        footer,
                        departmentcode,
                        isscan,
                        islab,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate,
                        ischarges,
                        isinventory,
                        ispackage,
                        istreatment
                    )
                    VALUES
                    (
                        @gcode,
                        @tenant_code,
                        @dcode,
                        @orderno,
                        @name,
                        @shortname,
                        @description,
                        @footer,
                        @departmentcode,
                        @isscan,
                        @islab,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate,
                        @ischarges,
                        @isinventory,
                        @ispackage,
                        @istreatment
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
        public async Task<string> Update(GroupMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE group_master
                    SET
                        dcode = @dcode,
                        orderno = @orderno,
                        name = @name,
                        shortname = @shortname,
                        description = @description,
                        footer = @footer,
                        departmentcode = @departmentcode,
                        isscan = @isscan,
                        islab = @islab,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate,
                        ischarges = @ischarges,
                        isinventory = @isinventory,
                        ispackage = @ispackage,
                        istreatment = @istreatment
                    WHERE gcode = @gcode
                    AND tenant_code = @tenant_code";

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
        public async Task<string> Delete(decimal gcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE group_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE gcode = @gcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { gcode, tenant_code });

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
        public async Task<List<GroupMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM group_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<GroupMasterModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY GCODE
        // ─────────────────────────────────────────
        public async Task<GroupMasterModel?> GetByGCode(decimal gcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM group_master
                WHERE deleted = false
                AND gcode = @gcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<GroupMasterModel>(
                sql,
                new { gcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<GroupMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM group_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<GroupMasterModel>(
                sql,
                new { name = $"%{name}%", tenant_code });

            return result.ToList();
        }
    }
}