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

        public async Task<IEnumerable<ScanResultEntryModel>> Search(string tenant_code, string? name, DateTime? date)
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
                AND v.investigation = 'scan'
                AND v.deleted = false
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR v.entered_date::date = @date)
                ORDER BY v.entered_date ASC";

            return await db.QueryAsync<ScanResultEntryModel>(
                sql, new { tenant_code, name, date = date?.Date });
        }

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
                    AND investigation = 'scan'
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