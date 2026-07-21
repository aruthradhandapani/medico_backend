using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class OgQueueClass
    {
        private readonly string db_conn;

        public OgQueueClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // Adds a patient to the OG queue.
        // token_no is copied directly from vitals_entry — not generated separately.
        // entry_type: "direct" (straight doctor visit) or "test_completed" (lab/scan done, now for doctor)
        // Dedup: same custcode + dcode + same day is not added twice
        // ─────────────────────────────────────────
        public async Task<string> AddToQueue(string tenant_code, string custcode, int dcode, string token_no, TimeOnly? arrival_time, string? notes, string entry_type = "direct")
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var todayUtc = DateTime.UtcNow.Date;

                var already = await db.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(1)
                    FROM og_queue
                    WHERE tenant_code = @tenant_code
                    AND custcode = @custcode
                    AND dcode = @dcode
                    AND created_at::date = @today
                    AND deleted = false",
                    new { tenant_code, custcode, dcode, today = todayUtc });

                if (already > 0)
                    return "Already in queue";

                var entry = new OgQueueModel
                {
                    tenant_code = tenant_code,
                    og_token_no = token_no,
                    custcode = custcode,
                    dcode = dcode,
                    arrival_time = arrival_time,
                    notes = notes,
                    entry_type = entry_type,
                    status = "waiting",
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow,
                    deleted = false
                };

                var newId = await db.InsertAsync(entry);
                return newId > 0 ? "Success" : "Failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (og screen list, both types combined) — optional name/date filter
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> Get(string tenant_code, string? name, DateTime? date)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    o.ogentryid,
                    o.tenant_code,
                    o.og_token_no,
                    o.custcode,
                    c.name AS patient_name,
                    o.dcode,
                    d.name AS doctor_name,
                    o.arrival_time,
                    o.out_time,
                    o.entry_type,
                    o.notes,
                    o.status,
                    o.usercode,
                    o.computercode,
                    o.created_at,
                    o.updated_at
                FROM og_queue o
                LEFT JOIN customer_master c ON c.custcode = o.custcode
                LEFT JOIN doctor_master d ON d.dcode = o.dcode AND d.tenant_code = o.tenant_code
                WHERE o.tenant_code = @tenant_code
                AND o.deleted = false
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR o.created_at::date = @date)
                ORDER BY o.dcode, o.og_token_no::int ASC";

            return await db.QueryAsync(sql, new { tenant_code, name, date = date.HasValue ? date.Value.Date : (DateTime?)null });
        }

        // ─────────────────────────────────────────
        // LAB & SCAN LIST — all patients under investigation = 'lab' or 'scan', with current status
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetLabScanList(string tenant_code, string? name, DateTime? date, string? status)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT
            v.vitalentryid,
            v.token_no,
            v.custcode,
            c.name AS patient_name,
            c.mobile,
            v.dcode,
            d.name AS doctor_name,
            v.investigation,
            v.test_name,
            v.status,
            v.entered_date,
            v.arrival_time
        FROM vitals_entry v
        LEFT JOIN customer_master c ON c.custcode = v.custcode
        LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
        WHERE v.tenant_code = @tenant_code
        AND v.investigation IN ('lab', 'scan')
        AND v.deleted = false
        AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
        AND (@date IS NULL OR v.entered_date::date = @date)
        AND (@status IS NULL OR v.status = @status)
        ORDER BY v.entered_date ASC";

            return await db.QueryAsync(sql, new
            {
                tenant_code,
                name,
                date = date.HasValue ? date.Value.Date : (DateTime?)null,
                status
            });
        }

        // ─────────────────────────────────────────
        // CONSULTATION LIST — all patients under investigation = 'doctor', with current status
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetConsultationList(string tenant_code, string? name, DateTime? date, string? status)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT
            v.vitalentryid,
            v.token_no,
            v.custcode,
            c.name AS patient_name,
            c.mobile,
            v.dcode,
            d.name AS doctor_name,
            v.investigation,
            v.test_name,
            v.status,
            v.entered_date,
            v.arrival_time
        FROM vitals_entry v
        LEFT JOIN customer_master c ON c.custcode = v.custcode
        LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
        WHERE v.tenant_code = @tenant_code
        AND v.investigation = 'doctor'
        AND v.deleted = false
        AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
        AND (@date IS NULL OR v.entered_date::date = @date)
        AND (@status IS NULL OR v.status = @status)
        ORDER BY v.entered_date ASC";

            return await db.QueryAsync(sql, new
            {
                tenant_code,
                name,
                date = date.HasValue ? date.Value.Date : (DateTime?)null,
                status
            });
        }

        // ─────────────────────────────────────────
        // LAB & SCAN PATIENT LIST — patients whose test is done, now waiting on doctor
        // (entry_type = 'test_completed')
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetTestCompletedList(string tenant_code, string? name, DateTime? date, int? dcode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    o.ogentryid,
                    o.og_token_no,
                    o.custcode,
                    c.name AS patient_name,
                    c.mobile,
                    o.dcode,
                    d.name AS doctor_name,
                    o.arrival_time,
                    o.out_time,
                    o.notes,
                    o.status,
                    o.created_at
                FROM og_queue o
                LEFT JOIN customer_master c ON c.custcode = o.custcode
                LEFT JOIN doctor_master d ON d.dcode = o.dcode AND d.tenant_code = o.tenant_code
                WHERE o.tenant_code = @tenant_code
                AND o.entry_type = 'test_completed'
                AND o.deleted = false
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR o.created_at::date = @date)
                AND (@dcode IS NULL OR o.dcode = @dcode)
                ORDER BY o.dcode, o.og_token_no::int ASC";

            return await db.QueryAsync(sql, new
            {
                tenant_code,
                name,
                date = date.HasValue ? date.Value.Date : (DateTime?)null,
                dcode
            });
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<OgQueueModel?> GetById(int ogentryid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM og_queue
                WHERE ogentryid = @ogentryid
                AND tenant_code = @tenant_code
                AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<OgQueueModel>(sql, new { ogentryid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY DOCTOR (doctor's own queue, all entry types, today)
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetByDoctor(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    o.ogentryid,
                    o.og_token_no,
                    o.custcode,
                    c.name AS patient_name,
                    o.arrival_time,
                    o.out_time,
                    o.entry_type,
                    o.notes,
                    o.status,
                    o.created_at
                FROM og_queue o
                LEFT JOIN customer_master c ON c.custcode = o.custcode
                WHERE o.dcode = @dcode
                AND o.tenant_code = @tenant_code
                AND o.deleted = false
                ORDER BY o.og_token_no::int ASC";

            return await db.QueryAsync(sql, new { dcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY STATUS (waiting / in_consultation / completed)
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetByStatus(string status, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    o.ogentryid,
                    o.og_token_no,
                    c.name AS patient_name,
                    d.name AS doctor_name,
                    o.arrival_time,
                    o.out_time,
                    o.entry_type,
                    o.notes,
                    o.status,
                    o.created_at
                FROM og_queue o
                LEFT JOIN customer_master c ON c.custcode = o.custcode
                LEFT JOIN doctor_master d ON d.dcode = o.dcode AND d.tenant_code = o.tenant_code
                WHERE o.status = @status
                AND o.tenant_code = @tenant_code
                AND o.deleted = false
                ORDER BY o.dcode, o.og_token_no::int ASC";

            return await db.QueryAsync(sql, new { status, tenant_code });
        }

        // ─────────────────────────────────────────
        // UPDATE STATUS (waiting → in_consultation → completed)
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(int ogentryid, string tenant_code, string status, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE og_queue
                    SET status = @status,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE ogentryid = @ogentryid
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    ogentryid,
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

        // ─────────────────────────────────────────
        // UPDATE OUT TIME (patient leaves / consultation ends)
        // ─────────────────────────────────────────
        public async Task<string> UpdateOutTime(int ogentryid, string tenant_code, TimeOnly out_time, string? status, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE og_queue
                    SET out_time = @out_time,
                        status = COALESCE(@status, status),
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE ogentryid = @ogentryid
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    ogentryid,
                    tenant_code,
                    out_time,
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

        // ─────────────────────────────────────────
        // DELETE (soft delete)
        // ─────────────────────────────────────────
        public async Task<string> Delete(int ogentryid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE og_queue
                    SET deleted = true,
                        updated_at = @updated_at
                    WHERE ogentryid = @ogentryid
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { ogentryid, tenant_code, updated_at = DateTime.UtcNow });
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}