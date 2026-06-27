using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CounterMasterClass
    {
        private readonly string db_conn;

        public CounterMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT CNTCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextCounterCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(cntcode), 0) + 1
                           FROM counter_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(CounterMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.cntcode = await GetNextCounterCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO counter_master
                    (
                        cntcode,
                        tenant_code,
                        orderno,
                        shortname,
                        name,
                        description,
                        bhcode,
                        cashlcode,
                        patientbilllcode,
                        referabillllcode,
                        patientsaleslcode,
                        referalsaleslcode,
                        commissionbilllcode,
                        commissionsaleslcode,
                        expenselcode,
                        timingcurrent,
                        timingvariable,
                        timingfixed,
                        timingfrom,
                        timingto,
                        isinsurance,
                        hdcode,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @cntcode,
                        @tenant_code,
                        @orderno,
                        @shortname,
                        @name,
                        @description,
                        @bhcode,
                        @cashlcode,
                        @patientbilllcode,
                        @referabillllcode,
                        @patientsaleslcode,
                        @referalsaleslcode,
                        @commissionbilllcode,
                        @commissionsaleslcode,
                        @expenselcode,
                        @timingcurrent,
                        @timingvariable,
                        @timingfixed,
                        @timingfrom,
                        @timingto,
                        @isinsurance,
                        @hdcode,
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
        public async Task<string> Update(CounterMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE counter_master
                    SET
                        orderno              = @orderno,
                        shortname            = @shortname,
                        name                 = @name,
                        description          = @description,
                        bhcode               = @bhcode,
                        cashlcode            = @cashlcode,
                        patientbilllcode     = @patientbilllcode,
                        referabillllcode     = @referabillllcode,
                        patientsaleslcode    = @patientsaleslcode,
                        referalsaleslcode    = @referalsaleslcode,
                        commissionbilllcode  = @commissionbilllcode,
                        commissionsaleslcode = @commissionsaleslcode,
                        expenselcode         = @expenselcode,
                        timingcurrent        = @timingcurrent,
                        timingvariable       = @timingvariable,
                        timingfixed          = @timingfixed,
                        timingfrom           = @timingfrom,
                        timingto             = @timingto,
                        isinsurance          = @isinsurance,
                        hdcode               = @hdcode,
                        deleted              = @deleted,
                        usercode             = @usercode,
                        computercode         = @computercode,
                        ibsdate              = @ibsdate,
                        tenant_code          = @tenant_code
                    WHERE cntcode = @cntcode
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
        public async Task<string> Delete(decimal cntcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                int openShifts = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM counter_timing WHERE cntcode = @cntcode AND todate IS NULL",
                    new { cntcode });

                if (openShifts > 0)
                    return "Cannot delete a counter with an open shift. Close the shift first.";

                string sql = @"
                    UPDATE counter_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE cntcode = @cntcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { cntcode, tenant_code });
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
        public async Task<List<CounterMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM counter_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno, cntcode";

            var result = await db.QueryAsync<CounterMasterModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CNTCODE
        // ─────────────────────────────────────────
        public async Task<CounterMasterModel?> GetByCounterCode(decimal cntcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM counter_master
                WHERE deleted = false
                AND cntcode = @cntcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<CounterMasterModel>(
                sql, new { cntcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<CounterMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM counter_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY name";

            var result = await db.QueryAsync<CounterMasterModel>(
                sql, new { name = $"%{name}%", tenant_code });
            return result.ToList();
        }
    }
}