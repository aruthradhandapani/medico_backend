// Class/ScanResultEntryClass.cs
using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class ScanResultEntryClass
    {
        private readonly string db_conn;
        private readonly OgQueueClass ogQueue;

        // Matches any of the 5 investigation slots (in1..in5), case-insensitive —
        // same rule VitalsClass.HasInvestigation uses for "lab" / "scan" / "doctor"
        private const string ScanInvestigationFilter = @"
            (
                v.in1 ILIKE 'scan' OR
                v.in2 ILIKE 'scan' OR
                v.in3 ILIKE 'scan' OR
                v.in4 ILIKE 'scan' OR
                v.in5 ILIKE 'scan'
            )";

        public ScanResultEntryClass(IConfiguration configuration, OgQueueClass _ogQueue)
        {
            db_conn = configuration.GetConnectionString("conn");
            ogQueue = _ogQueue;
        }

        // ─────────────────────────────────────────
        // GET ALL — all scan investigation entries for this tenant
        // ─────────────────────────────────────────
        public async Task<IEnumerable<ScanResultEntryModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = $@"
                SELECT
                    v.vitalentryid,
                    v.token_no,
                    v.custcode,
                    c.name AS patient_name,
                    c.mobile,
                    v.test_name,
                    v.status,
                    v.entered_date,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                WHERE v.tenant_code = @tenant_code
                AND {ScanInvestigationFilter}
                AND v.deleted = false
                AND v.status != 'dummy'
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<ScanResultEntryModel>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH — optional name/date filter
        // ─────────────────────────────────────────
        public async Task<IEnumerable<ScanResultEntryModel>> Search(string tenant_code, string? name, DateTime? date)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = $@"
                SELECT
                    v.vitalentryid,
                    v.token_no,
                    v.custcode,
                    c.name AS patient_name,
                    c.mobile,
                    v.test_name,
                    v.status,
                    v.entered_date,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                WHERE v.tenant_code = @tenant_code
                AND {ScanInvestigationFilter}
                AND v.deleted = false
                AND v.status != 'dummy'
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR v.entered_date::date = @date)
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<ScanResultEntryModel>(
                sql, new { tenant_code, name, date = date?.Date });
        }

        // ─────────────────────────────────────────
        // UPDATE STATUS (e.g. on_going → result_pending → report_received)
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(int vitalentryid, string tenant_code, string status, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = $@"
                    UPDATE vitals_entry v
                    SET status = @status,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE v.vitalentryid = @vitalentryid
                    AND v.tenant_code = @tenant_code
                    AND {ScanInvestigationFilter}
                    AND v.deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    vitalentryid,
                    tenant_code,
                    status,
                    usercode,
                    computercode,
                    updated_at = DateTime.UtcNow
                });

                if (rows > 0 && string.Equals(status, "report_received", StringComparison.OrdinalIgnoreCase))
                {
                    var v = await db.QueryFirstOrDefaultAsync<VitalsModel>(@"
                        SELECT * FROM vitals_entry
                        WHERE vitalentryid = @vitalentryid AND tenant_code = @tenant_code AND deleted = false",
                        new { vitalentryid, tenant_code });

                    if (v != null && v.dcode.HasValue && !string.IsNullOrEmpty(v.custcode))
                    {
                        await ogQueue.AddToQueue(tenant_code, v.custcode!, v.dcode.Value, v.token_no!, v.arrival_time, v.test_name, "test_completed");
                    }
                }

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}