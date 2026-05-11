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
        public async Task<int> GetNextCountryCode()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(countrycode), 0) + 1
                           FROM country_master";

            return await db.ExecuteScalarAsync<int>(sql);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CountryMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.countrycode = await GetNextCountryCode();
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
        public async Task<string> Update(CountryMasterModel data)
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
        public async Task<string> Delete(int countrycode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE country_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE countrycode = @countrycode";

                await db.ExecuteAsync(sql, new { countrycode });

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
        public async Task<List<CountryMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM country_master
                           WHERE deleted = false
                           ORDER BY countrycode";

            var result = await db.QueryAsync<CountryMasterModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY COUNTRYCODE
        // ─────────────────────────────────────────
        public async Task<CountryMasterModel?> GetByCountryCode(int countrycode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM country_master
                           WHERE deleted = false
                           AND countrycode = @countrycode";

            return await db.QueryFirstOrDefaultAsync<CountryMasterModel>(
                sql,
                new { countrycode });
        }

        // ─────────────────────────────────────────
        // SEARCH BY COUNTRY NAME
        // ─────────────────────────────────────────
        public async Task<List<CountryMasterModel>> SearchByCountryName(string countryname)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM country_master
                           WHERE deleted = false
                           AND LOWER(countryname) LIKE LOWER(@countryname)
                           ORDER BY countryname";

            var result = await db.QueryAsync<CountryMasterModel>(
                sql,
                new { countryname = $"%{countryname}%" });

            return result.ToList();
        }
    }
}