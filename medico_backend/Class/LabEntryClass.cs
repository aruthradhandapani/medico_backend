// Class/LabResultEntryClass.cs
using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class LabResultEntryClass
    {
        private readonly string db_conn;
        private readonly OgQueueClass ogQueue;

        // Matches any of the 5 investigation slots (in1..in5), case-insensitive —
        // same rule VitalsClass.HasInvestigation uses for "lab" / "scan" / "doctor"
        private const string LabInvestigationFilter = @"
            (
                v.in1 ILIKE 'lab' OR
                v.in2 ILIKE 'lab' OR
                v.in3 ILIKE 'lab' OR
                v.in4 ILIKE 'lab' OR
                v.in5 ILIKE 'lab'
            )";

        public LabResultEntryClass(IConfiguration configuration, OgQueueClass _ogQueue)
        {
            db_conn = configuration.GetConnectionString("conn");
            ogQueue = _ogQueue;
        }

        // All lab entries, no filters — always scoped to any in1..in5 = 'lab'
        // Dummy rows are excluded since they hold no real investigation data.
        public async Task<IEnumerable<LabResultEntryModel>> Get(string tenant_code)
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
                AND {LabInvestigationFilter}
                AND v.deleted = false
                AND v.status != 'dummy'
                ORDER BY v.updated_at DESC";

            return await db.QueryAsync<LabResultEntryModel>(sql, new { tenant_code });
        }

        // Search by name + optional date, always scoped to any in1..in5 = 'lab'
        public async Task<IEnumerable<LabResultEntryModel>> Search(string tenant_code, string? name, DateTime? date)
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
                AND {LabInvestigationFilter}
                AND v.deleted = false
                AND v.status != 'dummy'
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR v.entered_date::date = @date)
                ORDER BY v.updated_at DESC";

            return await db.QueryAsync<LabResultEntryModel>(
                sql, new { tenant_code, name, date = date?.Date });
        }

        // Updates vitals_entry.status directly — reflects instantly in
        // main Vitals get/get-by-status and the token screen, since it's the same table.
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
                    AND {LabInvestigationFilter}
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