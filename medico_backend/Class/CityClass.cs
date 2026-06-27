using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CityMasterClass
    {
        private readonly string db_conn;

        public CityMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT CITYCODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextCityCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(citycode), 0) + 1
                           FROM city_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CityMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.citycode = await GetNextCityCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO city_master
                    (
                        citycode,
                        tenant_code,
                        orderno,
                        shortname,
                        cityname,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @citycode,
                        @tenant_code,
                        @orderno,
                        @shortname,
                        @cityname,
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
        public async Task<string> Update(CityMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE city_master
                    SET
                        orderno      = @orderno,
                        shortname    = @shortname,
                        cityname     = @cityname,
                        description  = @description,
                        deleted      = @deleted,
                        usercode     = @usercode,
                        computercode = @computercode,
                        ibsdate      = @ibsdate,
                        tenant_code  = @tenant_code
                    WHERE citycode = @citycode
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
        public async Task<string> Delete(int citycode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE city_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE citycode = @citycode
                               AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { citycode, tenant_code });
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
        public async Task<List<CityMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM city_master
                           WHERE deleted = false
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                           ORDER BY citycode";

            var result = await db.QueryAsync<CityMasterModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CITYCODE (tenant + global)
        // ─────────────────────────────────────────
        public async Task<CityMasterModel?> GetByCityCode(int citycode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM city_master
                           WHERE deleted = false
                           AND citycode = @citycode
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)";

            return await db.QueryFirstOrDefaultAsync<CityMasterModel>(
                sql, new { citycode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY CITY NAME (tenant + global)
        // ─────────────────────────────────────────
        public async Task<List<CityMasterModel>> SearchByCityName(string cityname, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM city_master
                           WHERE deleted = false
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                           AND LOWER(cityname) LIKE LOWER(@cityname)
                           ORDER BY cityname";

            var result = await db.QueryAsync<CityMasterModel>(
                sql, new { cityname = $"%{cityname}%", tenant_code });
            return result.ToList();
        }
    }
}