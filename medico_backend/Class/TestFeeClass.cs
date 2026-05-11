using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TestFeeMasterClass
    {
        private readonly string db_conn;

        public TestFeeMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT TFCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextTfCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT COALESCE(MAX(tfcode), 0) + 1
                FROM test_fee_master
                WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(
                sql,
                new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(TestFeeMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tfcode = await GetNextTfCode(data.tenant_code!);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO test_fee_master
                    (
                        tfcode,
                        tcode,
                        ftcode,
                        margintype,
                        marginpercentage,
                        marginamount,
                        amount,
                        feeper,
                        feeamount,
                        charityper,
                        charityamount,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate,
                        runningcost,
                        commissiontype,
                        tenant_code
                    )
                    VALUES
                    (
                        @tfcode,
                        @tcode,
                        @ftcode,
                        @margintype,
                        @marginpercentage,
                        @marginamount,
                        @amount,
                        @feeper,
                        @feeamount,
                        @charityper,
                        @charityamount,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate,
                        @runningcost,
                        @commissiontype,
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
        public async Task<string> Update(TestFeeMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE test_fee_master
                    SET
                        tcode = @tcode,
                        ftcode = @ftcode,
                        margintype = @margintype,
                        marginpercentage = @marginpercentage,
                        marginamount = @marginamount,
                        amount = @amount,
                        feeper = @feeper,
                        feeamount = @feeamount,
                        charityper = @charityper,
                        charityamount = @charityamount,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate,
                        runningcost = @runningcost,
                        commissiontype = @commissiontype
                    WHERE tfcode = @tfcode
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
        public async Task<string> Delete(decimal tfcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE test_fee_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE tfcode = @tfcode
                    AND tenant_code = @tenant_code";

                int result = await db.ExecuteAsync(
                    sql,
                    new { tfcode, tenant_code });

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
        public async Task<List<TestFeeMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_fee_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY tfcode";

            var result = await db.QueryAsync<TestFeeMasterModel>(
                sql,
                new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY TFCODE
        // ─────────────────────────────────────────
        public async Task<TestFeeMasterModel?> GetByTfCode(
            decimal tfcode,
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_fee_master
                WHERE deleted = false
                AND tfcode = @tfcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<TestFeeMasterModel>(
                sql,
                new { tfcode, tenant_code });
        }
    }
}