// Class/OgScreenClass.cs
using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class OgScreenClass
    {
        private readonly string db_conn;

        public OgScreenClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // Only: consultation rows directly, OR lab/scan rows whose report is completed
        public async Task<IEnumerable<OgScreenModel>> Search(string tenant_code, string? name, DateTime? date)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY v.entered_date ASC) AS s_no,
                    v.id,
                    v.token_no,
                    c.name AS patient_name,
                    v.in_time,
                    v.out_time,
                    v.notes,
                    v.status
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custid = v.custid
                WHERE v.tenant_code = @tenant_code
                AND v.deleted = false
                AND (
                    v.investigation = 'doctor'
                    OR (v.investigation IN ('lab','scan') AND v.status = 'report_received')
                )
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR v.entered_date::date = @date)
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<OgScreenModel>(
                sql, new { tenant_code, name, date = date?.Date });
        }

        // Marks a patient as "ongoing" (doctor started seeing them) — sets in_time
        public async Task<string> MarkOngoing(int id, string tenant_code, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET status = 'ongoing',
                        in_time = @in_time,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE id = @id
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    id,
                    tenant_code,
                    usercode,
                    computercode,
                    in_time = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                });

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // Marks a patient as "completed" (doctor finished seeing them) — sets out_time
        public async Task<string> MarkCompleted(int id, string tenant_code, string? notes, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET status = 'completed',
                        out_time = @out_time,
                        notes = COALESCE(@notes, notes),
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE id = @id
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    id,
                    tenant_code,
                    notes,
                    usercode,
                    computercode,
                    out_time = DateTime.UtcNow,
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