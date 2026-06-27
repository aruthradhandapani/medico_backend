using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CountryMasterClass
    {
        private readonly string db_conn;

        public CountryMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT COUNTRYCODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextCountryCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(countrycode), 0) + 1
                           FROM country_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CountryMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.countrycode = await GetNextCountryCode(tenant_code);
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
        public async Task<string> Update(CountryMasterModel data, string tenant_code)
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
        public async Task<string> Delete(int countrycode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE country_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE countrycode = @countrycode
                               AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { countrycode, tenant_code });

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
        public async Task<List<CountryMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM country_master
                           WHERE deleted = false
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                           ORDER BY countrycode";

            var result = await db.QueryAsync<CountryMasterModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY COUNTRYCODE (tenant + global)
        // ─────────────────────────────────────────
        public async Task<CountryMasterModel?> GetByCountryCode(int countrycode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM country_master
                           WHERE deleted = false
                           AND countrycode = @countrycode
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)";

            return await db.QueryFirstOrDefaultAsync<CountryMasterModel>(
                sql, new { countrycode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY COUNTRY NAME (tenant + global)
        // ─────────────────────────────────────────
        public async Task<List<CountryMasterModel>> SearchByCountryName(string countryname, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM country_master
                           WHERE deleted = false
                           AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                           AND LOWER(countryname) LIKE LOWER(@countryname)
                           ORDER BY countryname";

            var result = await db.QueryAsync<CountryMasterModel>(
                sql, new { countryname = $"%{countryname}%", tenant_code });

            return result.ToList();
        }
    }
}