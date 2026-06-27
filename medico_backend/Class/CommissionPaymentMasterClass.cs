using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CommissionPaymentMasterClass
    {
        private readonly string db_conn;

        public CommissionPaymentMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CommissionPaymentMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.commissionpaymentguid = Guid.NewGuid().ToString();
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO commissionpayment_master
                    (
                        commissionpaymentguid,
                        commissionpaymentdate,
                        commissionpaymentsno,
                        commissionpaymentbarcode,
                        commissionpaymentcovertedbarcode,
                        pmcode,
                        payledgercode,
                        bankname,
                        paymentreference,
                        chequedate,
                        cardno,
                        carddate,
                        amountpaid,
                        amountadjusted,
                        amounttotal,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @commissionpaymentguid,
                        @commissionpaymentdate,
                        @commissionpaymentsno,
                        @commissionpaymentbarcode,
                        @commissionpaymentcovertedbarcode,
                        @pmcode,
                        @payledgercode,
                        @bankname,
                        @paymentreference,
                        @chequedate,
                        @cardno,
                        @carddate,
                        @amountpaid,
                        @amountadjusted,
                        @amounttotal,
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
        public async Task<string> Update(CommissionPaymentMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE commissionpayment_master
                    SET
                        commissionpaymentdate = @commissionpaymentdate,
                        commissionpaymentsno = @commissionpaymentsno,
                        commissionpaymentbarcode = @commissionpaymentbarcode,
                        commissionpaymentcovertedbarcode = @commissionpaymentcovertedbarcode,
                        pmcode = @pmcode,
                        payledgercode = @payledgercode,
                        bankname = @bankname,
                        paymentreference = @paymentreference,
                        chequedate = @chequedate,
                        cardno = @cardno,
                        carddate = @carddate,
                        amountpaid = @amountpaid,
                        amountadjusted = @amountadjusted,
                        amounttotal = @amounttotal,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE commissionpaymentguid = @commissionpaymentguid";

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
        public async Task<string> Delete(string commissionpaymentguid)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE commissionpayment_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE commissionpaymentguid = @commissionpaymentguid";

                await db.ExecuteAsync(sql, new { commissionpaymentguid });

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
        public async Task<List<CommissionPaymentMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_master
                WHERE deleted = false
                ORDER BY commissionpaymentdate DESC";

            var result = await db.QueryAsync<CommissionPaymentMasterModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY GUID
        // ─────────────────────────────────────────
        public async Task<CommissionPaymentMasterModel?> GetByGuid(string commissionpaymentguid)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_master
                WHERE deleted = false
                AND commissionpaymentguid = @commissionpaymentguid";

            return await db.QueryFirstOrDefaultAsync<CommissionPaymentMasterModel>(
                sql,
                new { commissionpaymentguid });
        }

        // ─────────────────────────────────────────
        // GET BY BARCODE
        // ─────────────────────────────────────────
        public async Task<CommissionPaymentMasterModel?> GetByBarcode(string commissionpaymentbarcode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_master
                WHERE deleted = false
                AND commissionpaymentbarcode = @commissionpaymentbarcode";

            return await db.QueryFirstOrDefaultAsync<CommissionPaymentMasterModel>(
                sql,
                new { commissionpaymentbarcode });
        }

        // ─────────────────────────────────────────
        // GET BY DATE RANGE
        // ─────────────────────────────────────────
        public async Task<List<CommissionPaymentMasterModel>> GetByDateRange(DateTime fromdate, DateTime todate)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM commissionpayment_master
                WHERE deleted = false
                AND commissionpaymentdate BETWEEN @fromdate AND @todate
                ORDER BY commissionpaymentdate DESC";

            var result = await db.QueryAsync<CommissionPaymentMasterModel>(
                sql,
                new { fromdate, todate });

            return result.ToList();
        }
    }
}