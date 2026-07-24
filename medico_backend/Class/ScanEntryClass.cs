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
        // GET ALL — status resolved from whichever slot holds 'scan'
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
                    CASE
                        WHEN v.in1 ILIKE 'scan' THEN v.in1_status
                        WHEN v.in2 ILIKE 'scan' THEN v.in2_status
                        WHEN v.in3 ILIKE 'scan' THEN v.in3_status
                        WHEN v.in4 ILIKE 'scan' THEN v.in4_status
                        WHEN v.in5 ILIKE 'scan' THEN v.in5_status
                    END AS status,
                    v.entered_date,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                WHERE v.tenant_code = @tenant_code
                AND {ScanInvestigationFilter}
                AND v.deleted = false
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<ScanResultEntryModel>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH — same slot-resolved status
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
                    CASE
                        WHEN v.in1 ILIKE 'scan' THEN v.in1_status
                        WHEN v.in2 ILIKE 'scan' THEN v.in2_status
                        WHEN v.in3 ILIKE 'scan' THEN v.in3_status
                        WHEN v.in4 ILIKE 'scan' THEN v.in4_status
                        WHEN v.in5 ILIKE 'scan' THEN v.in5_status
                    END AS status,
                    v.entered_date,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                WHERE v.tenant_code = @tenant_code
                AND {ScanInvestigationFilter}
                AND v.deleted = false
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR v.entered_date::date = @date)
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<ScanResultEntryModel>(
                sql, new { tenant_code, name, date = date?.Date });
        }

        // ─────────────────────────────────────────
        // UPDATE STATUS — resolves which slot holds 'scan' for this row,
        // updates only that slot's status column, not the shared status field.
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(int vitalentryid, string tenant_code, string status, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var v = await db.QueryFirstOrDefaultAsync<VitalsModel>(@"
                    SELECT * FROM vitals_entry
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code
                    AND deleted = false",
                    new { vitalentryid, tenant_code });

                if (v == null)
                    return "Record not found";

                string? scanSlot = null;
                if (string.Equals(v.in1, "scan", StringComparison.OrdinalIgnoreCase)) scanSlot = "in1";
                else if (string.Equals(v.in2, "scan", StringComparison.OrdinalIgnoreCase)) scanSlot = "in2";
                else if (string.Equals(v.in3, "scan", StringComparison.OrdinalIgnoreCase)) scanSlot = "in3";
                else if (string.Equals(v.in4, "scan", StringComparison.OrdinalIgnoreCase)) scanSlot = "in4";
                else if (string.Equals(v.in5, "scan", StringComparison.OrdinalIgnoreCase)) scanSlot = "in5";

                if (scanSlot == null)
                    return "This record has no 'scan' investigation slot";

                string statusColumn = $"{scanSlot}_status";

                string sql = $@"
                    UPDATE vitals_entry
                    SET {statusColumn} = @status,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code
                    AND deleted = false";

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
                    if (v.dcode.HasValue && !string.IsNullOrEmpty(v.custcode))
                    {
                        await ogQueue.AddToQueue(tenant_code, v.custcode!, v.dcode.Value, v.token_no!, v.arrival_time, v.test_name, "test_completed");
                    }
                }

                return rows > 0 ? "Success" : "Failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}