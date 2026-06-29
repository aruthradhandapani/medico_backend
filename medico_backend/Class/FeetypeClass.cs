using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class FeeTypeMasterClass
    {
        private readonly string db_conn;

        public FeeTypeMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT FTCODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextFtCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT COALESCE(MAX(ftcode), 0) + 1
                FROM feetype_master
                WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(
                sql,
                new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(FeeTypeMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ftcode = await GetNextFtCode(data.tenant_code!);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
            INSERT INTO feetype_master
            (
                ftcode,
                orderno,
                shortname,
                name,
                description,
                commissionpercentage,
                iscommission,
                isscheme,
                isspecial,
                isic,
                deleted,
                usercode,
                computercode,
                entereddate,
                ibsdate,
                tenant_code
            )
            VALUES
            (
                @ftcode,
                @orderno,
                @shortname,
                @name,
                @description,
                @commissionpercentage,
                @iscommission,
                @isscheme,
                @isspecial,
                @isic,
                @deleted,
                @usercode,
                @computercode,
                @entereddate,
                @ibsdate,
                @tenant_code
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
        public async Task<string> Update(FeeTypeMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
            UPDATE feetype_master
            SET
                orderno = @orderno,
                shortname = @shortname,
                name = @name,
                description = @description,
                commissionpercentage = @commissionpercentage,
                iscommission = @iscommission,
                isscheme = @isscheme,
                isspecial = @isspecial,
                isic = @isic,
                deleted = @deleted,
                usercode = @usercode,
                computercode = @computercode,
                ibsdate = @ibsdate
            WHERE ftcode = @ftcode
            AND tenant_code = @tenant_code";

                int result = await db.ExecuteAsync(sql, data);

                if (result == 0)
                {
                    return "Data Not Found";
                }

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
        public async Task<string> Delete(int ftcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE feetype_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE ftcode = @ftcode
                    AND tenant_code = @tenant_code";

                int result = await db.ExecuteAsync(
                    sql,
                    new { ftcode, tenant_code });

                if (result == 0)
                {
                    return "Data Not Found";
                }

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
        public async Task<List<FeeTypeMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM feetype_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY ftcode";

            var result = await db.QueryAsync<FeeTypeMasterModel>(
                sql,
                new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY FTCODE
        // ─────────────────────────────────────────
        public async Task<FeeTypeMasterModel?> GetByFtCode(
            int ftcode,
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM feetype_master
                WHERE deleted = false
                AND ftcode = @ftcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<FeeTypeMasterModel>(
                sql,
                new { ftcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<FeeTypeMasterModel>> SearchByName(
            string name,
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM feetype_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY name";

            var result = await db.QueryAsync<FeeTypeMasterModel>(
                sql,
                new
                {
                    name = $"%{name}%",
                    tenant_code
                });

            return result.ToList();
        }
    }
}