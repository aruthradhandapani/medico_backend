using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class AppointmentPreBookingClass
    {
        private readonly string db_conn;

        public AppointmentPreBookingClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }


        // ─────────────────────────────────────────
        // ADD (create a new appointment pre-booking)
        // ─────────────────────────────────────────
        public async Task<string> Add(string tenant_code, AddAppointmentPreBookingRequest req)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var nextId = await db.ExecuteScalarAsync<long>(@"
            SELECT COALESCE(MAX(preferenceid), 0) + 1
            FROM appointment_pre_booking",
                    new { });

                var entry = new AppointmentPreBookingModel
                {
                    preferenceid = nextId,
                    tenant_code = tenant_code,
                    custcode = req.custcode,
                    dcode = req.dcode,
                    husband_name = req.husband_name,
                    service_type = req.service_type,
                    appointment_date = req.appointment_date,
                    remarks = req.remarks,
                    deleted = false,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow
                };

                await db.InsertAsync(entry);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL — optional patient name / date filter
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> Get(string tenant_code, string? name, DateTime? date)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    a.preferenceid,
                    a.tenant_code,
                    a.custcode,
                    c.name AS patient_name,
                    c.mobile,
                    a.dcode,
                    d.name AS doctor_name,
                    a.husband_name,
                    a.service_type,
                    a.appointment_date,
                    a.remarks,
                    a.usercode,
                    a.computercode,
                    a.entereddate,
                    a.ibsdate
                FROM appointment_pre_booking a
                LEFT JOIN customer_master c ON c.custcode = a.custcode
                LEFT JOIN doctor_master d ON d.dcode = a.dcode AND d.tenant_code = a.tenant_code
                WHERE a.tenant_code = @tenant_code
                AND a.deleted = false
                AND (@name IS NULL OR c.name ILIKE '%' || @name || '%')
                AND (@date IS NULL OR a.appointment_date::date = @date)
                ORDER BY a.appointment_date ASC";

            return await db.QueryAsync(sql, new { tenant_code, name, date = date.HasValue ? date.Value.Date : (DateTime?)null });
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<AppointmentPreBookingModel?> GetById(long preferenceid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM appointment_pre_booking
                WHERE preferenceid = @preferenceid
                AND tenant_code = @tenant_code
                AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<AppointmentPreBookingModel>(sql, new { preferenceid, tenant_code });
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(string tenant_code, UpdateAppointmentPreBookingRequest req)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE appointment_pre_booking
                    SET custcode = @custcode,
                        dcode = @dcode,
                        husband_name = @husband_name,
                        service_type = @service_type,
                        appointment_date = @appointment_date,
                        remarks = @remarks,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE preferenceid = @preferenceid
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    req.preferenceid,
                    tenant_code,
                    req.custcode,
                    req.dcode,
                    req.husband_name,
                    req.service_type,
                    req.appointment_date,
                    req.remarks,
                    req.usercode,
                    req.computercode,
                    ibsdate = DateTime.UtcNow
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
        public async Task<string> Delete(long preferenceid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE appointment_pre_booking
                    SET deleted = true,
                        ibsdate = @ibsdate
                    WHERE preferenceid = @preferenceid
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { preferenceid, tenant_code, ibsdate = DateTime.UtcNow });
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}