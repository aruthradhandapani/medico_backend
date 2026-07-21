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

        public ScanResultEntryClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET ALL — all scan investigation entries for this tenant
        // ─────────────────────────────────────────
        public async Task<IEnumerable<ScanResultEntryModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
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
                AND v.investigation = 'scan'
                AND v.deleted = false
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<ScanResultEntryModel>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH — optional name/date filter
        // ─────────────────────────────────────────
        public async Task<IEnumerable<ScanResultEntryModel>> Search(string tenant_code, string? name, DateTime? date)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
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
                AND v.investigation = 'scan'
                AND v.deleted = false
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

                string sql = @"
                    UPDATE vitals_entry
                    SET status = @status,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code
                    AND investigation = 'scan'
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

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}