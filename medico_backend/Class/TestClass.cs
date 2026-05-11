using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TestMasterClass
    {
        private readonly string db_conn;

        public TestMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT TCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextTcode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT COALESCE(MAX(tcode),0) + 1
                FROM test_master
                WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(
                sql,
                new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(TestMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tcode = await GetNextTcode(data.tenant_code!);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO test_master
                    (
                        tcode,
                        gcode,
                        scode,
                        rtcode,
                        ucode,
                        rtmcode,
                        orderno,
                        name,
                        shortname,
                        qty,
                        amount,
                        lockresult,
                        locksms,
                        textcontent,
                        culturereport,
                        ""Routine"",
                        outlab,
                        description,
                        footer,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate,
                        printinseparatepage,
                        printgraphinreport,
                        graphtype,
                        istest,
                        ispackage,
                        packcode,
                        gstlcode,
                        gstper,
                        gstamount,
                        hsn,
                        hsndescription,
                        isnodiscount,
                        ccf,
                        ccv,
                        csf,
                        ""CSV"",
                        hsf,
                        hsv,
                        isccv,
                        iscsv,
                        isncv,
                        ncf,
                        ncv,
                        tcf,
                        tenant_code
                    )
                    VALUES
                    (
                        @tcode,
                        @gcode,
                        @scode,
                        @rtcode,
                        @ucode,
                        @rtmcode,
                        @orderno,
                        @name,
                        @shortname,
                        @qty,
                        @amount,
                        @lockresult,
                        @locksms,
                        @textcontent,
                        @culturereport,
                        @Routine,
                        @outlab,
                        @description,
                        @footer,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate,
                        @printinseparatepage,
                        @printgraphinreport,
                        @graphtype,
                        @istest,
                        @ispackage,
                        @packcode,
                        @gstlcode,
                        @gstper,
                        @gstamount,
                        @hsn,
                        @hsndescription,
                        @isnodiscount,
                        @ccf,
                        @ccv,
                        @csf,
                        @CSV,
                        @hsf,
                        @hsv,
                        @isccv,
                        @iscsv,
                        @isncv,
                        @ncf,
                        @ncv,
                        @tcf,
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
        public async Task<string> Update(TestMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE test_master
                    SET
                        gcode = @gcode,
                        scode = @scode,
                        rtcode = @rtcode,
                        ucode = @ucode,
                        rtmcode = @rtmcode,
                        orderno = @orderno,
                        name = @name,
                        shortname = @shortname,
                        qty = @qty,
                        amount = @amount,
                        lockresult = @lockresult,
                        locksms = @locksms,
                        textcontent = @textcontent,
                        culturereport = @culturereport,
                        ""Routine"" = @Routine,
                        outlab = @outlab,
                        description = @description,
                        footer = @footer,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate,
                        printinseparatepage = @printinseparatepage,
                        printgraphinreport = @printgraphinreport,
                        graphtype = @graphtype,
                        istest = @istest,
                        ispackage = @ispackage,
                        packcode = @packcode,
                        gstlcode = @gstlcode,
                        gstper = @gstper,
                        gstamount = @gstamount,
                        hsn = @hsn,
                        hsndescription = @hsndescription,
                        isnodiscount = @isnodiscount,
                        ccf = @ccf,
                        ccv = @ccv,
                        csf = @csf,
                        ""CSV"" = @CSV,
                        hsf = @hsf,
                        hsv = @hsv,
                        isccv = @isccv,
                        iscsv = @iscsv,
                        isncv = @isncv,
                        ncf = @ncf,
                        ncv = @ncv,
                        tcf = @tcf
                    WHERE tcode = @tcode
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
        public async Task<string> Delete(decimal tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE test_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE tcode = @tcode
                    AND tenant_code = @tenant_code";

                int result = await db.ExecuteAsync(
                    sql,
                    new { tcode, tenant_code });

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
        public async Task<List<TestMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY tcode";

            var result = await db.QueryAsync<TestMasterModel>(
                sql,
                new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY TCODE
        // ─────────────────────────────────────────
        public async Task<TestMasterModel?> GetByTcode(
            decimal tcode,
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_master
                WHERE deleted = false
                AND tcode = @tcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<TestMasterModel>(
                sql,
                new { tcode, tenant_code });
        }
    }
}