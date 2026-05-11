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
        public async Task<int> GetNextCityCode()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(citycode), 0) + 1
                           FROM city_master";

            return await db.ExecuteScalarAsync<int>(sql);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CityMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.citycode = await GetNextCityCode();
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO city_master
                    (
                        citycode,
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
        public async Task<string> Update(CityMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE city_master
                    SET
                        orderno = @orderno,
                        shortname = @shortname,
                        cityname = @cityname,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE citycode = @citycode";

                await db.ExecuteAsync(sql, data);

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE (SOFT DELETE)
        // ─────────────────────────────────────────
        public async Task<string> Delete(int citycode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE city_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE citycode = @citycode";

                await db.ExecuteAsync(sql, new { citycode });

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
        public async Task<List<CityMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM city_master
                           WHERE deleted = false
                           ORDER BY citycode";

            var result = await db.QueryAsync<CityMasterModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CITYCODE
        // ─────────────────────────────────────────
        public async Task<CityMasterModel?> GetByCityCode(int citycode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM city_master
                           WHERE deleted = false
                           AND citycode = @citycode";

            return await db.QueryFirstOrDefaultAsync<CityMasterModel>(
                sql,
                new { citycode });
        }

        // ─────────────────────────────────────────
        // SEARCH BY CITY NAME
        // ─────────────────────────────────────────
        public async Task<List<CityMasterModel>> SearchByCityName(string cityname)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM city_master
                           WHERE deleted = false
                           AND LOWER(cityname) LIKE LOWER(@cityname)
                           ORDER BY cityname";

            var result = await db.QueryAsync<CityMasterModel>(
                sql,
                new { cityname = $"%{cityname}%" });

            return result.ToList();
        }
    }
}