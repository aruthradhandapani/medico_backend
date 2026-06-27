using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CounterTimingClass
    {
        private readonly string db_conn;

        public CounterTimingClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT SHIFTSNO (per bhcode/cntcode/date/tenant)
        // ─────────────────────────────────────────
        public async Task<int> GetNextShiftNo(int bhcode, int cntcode, DateTime counterdate, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(shiftsno), 0) + 1
                           FROM counter_timing
                           WHERE bhcode = @bhcode
                           AND cntcode = @cntcode
                           AND counterdate = @counterdate::date
                           AND tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { bhcode, cntcode, counterdate, tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT (OPEN SHIFT)
        // ─────────────────────────────────────────
        public async Task<string> Insert(CounterTimingModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                if (data.bhcode == null || data.cntcode == null || data.counterdate == null)
                    return "bhcode, cntcode and counterdate are required.";

                data.tenant_code = tenant_code;

                int openShifts = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) FROM counter_timing
                      WHERE bhcode = @bhcode AND cntcode = @cntcode AND todate IS NULL
                      AND tenant_code = @tenant_code",
                    new { data.bhcode, data.cntcode, tenant_code });

                if (openShifts > 0)
                    return "An open shift already exists for this branch/counter. Close it first.";

                data.cnttid = Guid.NewGuid().ToString();
                data.shiftsno = await GetNextShiftNo(data.bhcode.Value, data.cntcode.Value, data.counterdate.Value, tenant_code);
                data.fromdate = DateTime.UtcNow;
                data.todate = null;
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    INSERT INTO counter_timing
                    (
                        cnttid,
                        bhcode,
                        cntcode,
                        shiftsno,
                        counterdate,
                        fromdate,
                        todate,
                        tenant_code,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @cnttid,
                        @bhcode,
                        @cntcode,
                        @shiftsno,
                        @counterdate,
                        @fromdate,
                        @todate,
                        @tenant_code,
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
        // UPDATE (e.g. CLOSE SHIFT)
        // ─────────────────────────────────────────
        public async Task<string> Update(CounterTimingModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE counter_timing
                    SET
                        bhcode = @bhcode,
                        cntcode = @cntcode,
                        counterdate = @counterdate,
                        fromdate = @fromdate,
                        todate = @todate,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE cnttid = @cnttid
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
        public async Task<string> Delete(string cnttid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                    "SELECT * FROM counter_timing WHERE cnttid = @cnttid AND tenant_code = @tenant_code",
                    new { cnttid, tenant_code });

                if (existing == null)
                    return "Data Not Found";

                if (existing.todate == null)
                {
                    int linkedBills = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(*) FROM lab_request_master
                          WHERE cnttid = @cnttid AND isdeleted = false
                          AND tenant_code = @tenant_code",
                        new { cnttid, tenant_code });

                    if (linkedBills > 0)
                        return $"Cannot delete: {linkedBills} active bill(s) are linked to this shift. Close the shift first.";
                }

                string sql = @"DELETE FROM counter_timing WHERE cnttid = @cnttid AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { cnttid, tenant_code });

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
        public async Task<List<CounterTimingModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE tenant_code = @tenant_code
                ORDER BY counterdate DESC, fromdate DESC";

            var result = await db.QueryAsync<CounterTimingModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CNTTID
        // ─────────────────────────────────────────
        public async Task<CounterTimingModel?> GetByCnttid(string cnttid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE cnttid = @cnttid
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                sql,
                new { cnttid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY CNTCODE (history for a counter)
        // ─────────────────────────────────────────
        public async Task<List<CounterTimingModel>> GetByCntcode(int cntcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE cntcode = @cntcode
                AND tenant_code = @tenant_code
                ORDER BY counterdate DESC, fromdate DESC";

            var result = await db.QueryAsync<CounterTimingModel>(
                sql,
                new { cntcode, tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET OPEN SHIFT (bhcode + cntcode)
        // ─────────────────────────────────────────
        public async Task<CounterTimingModel?> GetOpenShift(int bhcode, int cntcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE bhcode = @bhcode
                AND cntcode = @cntcode
                AND todate IS NULL
                AND tenant_code = @tenant_code
                LIMIT 1";

            return await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                sql,
                new { bhcode, cntcode, tenant_code });
        }
    }
}