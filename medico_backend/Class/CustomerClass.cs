using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CustomerMasterClass
    {
        private readonly string db_conn;

        public CustomerMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("cust_conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT CUSTID (Auto Increment)
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextCustId(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(custid), 0) + 1
                           FROM customer_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CustomerMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.custid = await GetNextCustId(data.tenant_code!);
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
        public async Task<string> Update(CustomerMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                var res = await db.UpdateAsync(data);
                return res ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(decimal custid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE customer_master
                               SET deleted    = true,
                                   ibsdate    = now()
                               WHERE custid       = @custid
                               AND tenant_code    = @tenant_code";

                await db.ExecuteAsync(sql, new { custid, tenant_code });
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
        public async Task<List<CustomerMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted      = false
                           AND tenant_code    = @tenant_code
                           ORDER BY custid";

            var res = await db.QueryAsync<CustomerMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CUSTID
        // ─────────────────────────────────────────
        public async Task<CustomerMasterModel?> GetByCustId(decimal custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted      = false
                           AND custid         = @custid
                           AND tenant_code    = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<CustomerMasterModel>(
                sql, new { custid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY MOBILE
        // ─────────────────────────────────────────
        public async Task<List<CustomerMasterModel>> GetByMobile(string mobile, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted      = false
                           AND mobile         = @mobile
                           AND tenant_code    = @tenant_code
                           ORDER BY custid";

            var res = await db.QueryAsync<CustomerMasterModel>(sql, new { mobile, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY NAME (Search)
        // ─────────────────────────────────────────
        public async Task<List<CustomerMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted      = false
                           AND LOWER(name)    LIKE LOWER(@name)
                           AND tenant_code    = @tenant_code
                           ORDER BY name";

            var res = await db.QueryAsync<CustomerMasterModel>(
                sql, new { name = $"%{name}%", tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET INSURANCE PATIENTS
        // ─────────────────────────────────────────
        public async Task<List<CustomerMasterModel>> GetInsurancePatients(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted              = false
                           AND isinsurancepatient     = true
                           AND tenant_code            = @tenant_code
                           ORDER BY custid";

            var res = await db.QueryAsync<CustomerMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET IP PATIENTS
        // ─────────────────────────────────────────
        public async Task<List<CustomerMasterModel>> GetIPPatients(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted      = false
                           AND isip           = true
                           AND tenant_code    = @tenant_code
                           ORDER BY custid";

            var res = await db.QueryAsync<CustomerMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET OP PATIENTS
        // ─────────────────────────────────────────
        public async Task<List<CustomerMasterModel>> GetOPPatients(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM customer_master
                           WHERE deleted      = false
                           AND isop           = true
                           AND tenant_code    = @tenant_code
                           ORDER BY custid";

            var res = await db.QueryAsync<CustomerMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }
    }
}