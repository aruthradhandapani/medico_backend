using Dapper;
using Dapper.Contrib.Extensions;
using Medico_Backend.Model;
using Npgsql;
using System.Data;

namespace Medico_Backend.Class
{
    public class AppointmentPreBookingClass
    {
        private readonly string db_conn;
        private readonly VitalsClass vitals;
        private readonly OgQueueClass ogQueue;

        public AppointmentPreBookingClass(IConfiguration configuration, VitalsClass _vitals, OgQueueClass _ogQueue)
        {
            db_conn = configuration.GetConnectionString("conn");
            vitals = _vitals;
            ogQueue = _ogQueue;
        }

        // ─────────────────────────────────────────
        // ADD (create a new appointment pre-booking)
        // ─────────────────────────────────────────
        public async Task<string> Add(string tenant_code, AddAppointmentPreBookingRequest req)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var entry = new AppointmentPreBookingModel
                {
                    tenant_code = tenant_code,
                    custcode = req.custcode,
                    dcode = req.dcode,
                    husband_name = req.husband_name,
                    service_type = req.service_type,
                    test_name = req.test_name,
                    appointment_date = req.appointment_date,
                    remarks = req.remarks,
                    deleted = false,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow
                };

                string sql = @"
                    INSERT INTO appointment_pre_booking
                        (tenant_code, custcode, dcode, husband_name, service_type, test_name,
                         appointment_date, remarks, deleted, usercode, computercode,
                         entereddate, ibsdate)
                    VALUES
                        (@tenant_code, @custcode, @dcode, @husband_name, @service_type, @test_name,
                         @appointment_date, @remarks, @deleted, @usercode, @computercode,
                         @entereddate, @ibsdate)
                    RETURNING preferenceid";

                var newId = await db.ExecuteScalarAsync<long>(sql, entry);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL — optional patient name / date / status filter
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> Get(string tenant_code, string? name, DateTime? date, string? status = null)
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
                    a.test_name,
                    a.appointment_date,
                    a.remarks,
                    a.status,
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
                AND (@status IS NULL OR a.status = @status)
                ORDER BY a.appointment_date ASC";

            return await db.QueryAsync(sql, new
            {
                tenant_code,
                name,
                date = date.HasValue ? date.Value.Date : (DateTime?)null,
                status
            });
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
                        test_name = @test_name,
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
                    req.test_name,
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

        // ─────────────────────────────────────────
        // MARK VISITED — patient shows up for their pre-booked appointment.
        // Flips pre-booking status to 'visited' and inserts the actual
        // vitals_entry (generates the token). Does NOT push into og_queue
        // directly — that still only happens via VitalsClass.Insert's own
        // internal logic (immediately for "doctor", later for lab/scan
        // once status becomes 'report_received').
        // ─────────────────────────────────────────
        public async Task<string> MarkVisited(long preferenceid, string tenant_code, string? in1, string? in2, string? in3, string? in4, string? in5, string? test_name, TimeOnly? arrival_time, bool is_vip, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var booking = await db.QueryFirstOrDefaultAsync<AppointmentPreBookingModel>(@"
            SELECT * FROM appointment_pre_booking
            WHERE preferenceid = @preferenceid
            AND tenant_code = @tenant_code
            AND deleted = false",
                    new { preferenceid, tenant_code });

                if (booking == null)
                    return "Booking not found";

                if (booking.status == "visited")
                    return "Already marked as visited";

                if (!booking.dcode.HasValue || string.IsNullOrEmpty(booking.custcode))
                    return "Booking is missing custcode or dcode";

                var vitalsData = new VitalsModel
                {
                    tenant_code = tenant_code,
                    custcode = booking.custcode,
                    dcode = booking.dcode,
                    in1 = in1?.Trim().ToLowerInvariant(),
                    in2 = in2?.Trim().ToLowerInvariant(),
                    in3 = in3?.Trim().ToLowerInvariant(),
                    in4 = in4?.Trim().ToLowerInvariant(),
                    in5 = in5?.Trim().ToLowerInvariant(),
                    test_name = test_name ?? booking.test_name ?? booking.service_type,
                    arrival_time = arrival_time,
                    status = "waiting",
                    is_vip = is_vip,
                    usercode = usercode,
                    computercode = computercode
                };

                var result = await vitals.Insert(vitalsData);

                if (result.message != "Success")
                    return $"Failed to create vitals entry: {result.message}";

                var rows = await db.ExecuteAsync(@"
            UPDATE appointment_pre_booking
            SET status = 'visited',
                ibsdate = @ibsdate
            WHERE preferenceid = @preferenceid
            AND tenant_code = @tenant_code",
                    new { preferenceid, tenant_code, ibsdate = DateTime.UtcNow });

                return rows > 0 ? $"Success - token_no: {result.token_no}" : "Failed to update booking status";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}