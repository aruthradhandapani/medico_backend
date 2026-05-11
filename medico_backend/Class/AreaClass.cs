using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class AreaMasterClass
    {
        private readonly string db_conn;

        public AreaMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT AREACODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextAreaCode()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(areacode), 0) + 1
                           FROM area_master";

            return await db.ExecuteScalarAsync<int>(sql);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(AreaMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.areacode = await GetNextAreaCode();
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO area_master
                    (
                        areacode,
                        orderno,
                        shortname,
                        areaname,
                        citycode,
                        areapincode,
                        statecode,
                        countrycode,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @areacode,
                        @orderno,
                        @shortname,
                        @areaname,
                        @citycode,
                        @areapincode,
                        @statecode,
                        @countrycode,
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
        public async Task<string> Update(AreaMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE area_master
                    SET
                        orderno = @orderno,
                        shortname = @shortname,
                        areaname = @areaname,
                        citycode = @citycode,
                        areapincode = @areapincode,
                        statecode = @statecode,
                        countrycode = @countrycode,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE areacode = @areacode";

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
        public async Task<string> Delete(int areacode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE area_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE areacode = @areacode";

                await db.ExecuteAsync(sql, new { areacode });

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
        public async Task<List<AreaMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM area_master
                WHERE deleted = false
                ORDER BY areacode";

            var result = await db.QueryAsync<AreaMasterModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY AREACODE
        // ─────────────────────────────────────────
        public async Task<AreaMasterModel?> GetByAreaCode(int areacode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM area_master
                WHERE deleted = false
                AND areacode = @areacode";

            return await db.QueryFirstOrDefaultAsync<AreaMasterModel>(
                sql,
                new { areacode });
        }

        // ─────────────────────────────────────────
        // SEARCH BY AREA NAME
        // ─────────────────────────────────────────
        public async Task<List<AreaMasterModel>> SearchByAreaName(string areaname)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM area_master
                WHERE deleted = false
                AND LOWER(areaname) LIKE LOWER(@areaname)
                ORDER BY areaname";

            var result = await db.QueryAsync<AreaMasterModel>(
                sql,
                new { areaname = $"%{areaname}%" });

            return result.ToList();
        }
    }
}