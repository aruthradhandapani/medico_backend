using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CommissionPaymentDetailsClass
    {
        private readonly string db_conn;

        public CommissionPaymentDetailsClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CommissionPaymentDetailsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.cpdid = Guid.NewGuid().ToString();

                string sql = @"
                    INSERT INTO commissionpayment_details
                    (
                        commissionpaymentguid,
                        requestguid,
                        commissionpaymentamount,
                        cpdid
                    )
                    VALUES
                    (
                        @commissionpaymentguid,
                        @requestguid,
                        @commissionpaymentamount,
                        @cpdid
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
        public async Task<string> Update(CommissionPaymentDetailsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE commissionpayment_details
                    SET
                        commissionpaymentguid = @commissionpaymentguid,
                        requestguid = @requestguid,
                        commissionpaymentamount = @commissionpaymentamount
                    WHERE cpdid = @cpdid";

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
        public async Task<string> Delete(string cpdid)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    DELETE FROM commissionpayment_details
                    WHERE cpdid = @cpdid";

                await db.ExecuteAsync(sql, new { cpdid });

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
        public async Task<List<CommissionPaymentDetailsModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_details
                ORDER BY cpdid";

            var result = await db.QueryAsync<CommissionPaymentDetailsModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CPDID
        // ─────────────────────────────────────────
        public async Task<CommissionPaymentDetailsModel?> GetByCpdId(string cpdid)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_details
                WHERE cpdid = @cpdid";

            return await db.QueryFirstOrDefaultAsync<CommissionPaymentDetailsModel>(
                sql,
                new { cpdid });
        }

        // ─────────────────────────────────────────
        // GET BY COMMISSION PAYMENT GUID
        // ─────────────────────────────────────────
        public async Task<List<CommissionPaymentDetailsModel>> GetByCommissionPaymentGuid(string commissionpaymentguid)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_details
                WHERE commissionpaymentguid = @commissionpaymentguid";

            var result = await db.QueryAsync<CommissionPaymentDetailsModel>(
                sql,
                new { commissionpaymentguid });

            return result.ToList();
        }
    }
}