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

        public LabResultEntryClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // Search by name + optional date, always scoped to investigation = 'lab'
        public async Task<IEnumerable<LabResultEntryModel>> Search(string tenant_code, string? name, DateTime? date)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    v.id,
                    v.token_no,
                    v.custid,
                    c.name AS patient_name,
                    c.mobile,
                    v.test_name,
                    v.status,
                    v.entered_date
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custid = v.custid
                WHERE v.tenant_code = @tenant_code
                AND v.investigation = 'lab'
                AND v.deleted = false
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR v.entered_date::date = @date)
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<LabResultEntryModel>(
                sql, new { tenant_code, name, date = date?.Date });
        }

        // Updates vitals_entry.status directly — reflects instantly in
        // main Vitals get/get-by-status and the token screen, since it's the same table.
        public async Task<string> UpdateStatus(int id, string tenant_code, string status, int usercode, int computercode)
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
                    WHERE id = @id
                    AND tenant_code = @tenant_code
                    AND investigation = 'lab'
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    id,
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