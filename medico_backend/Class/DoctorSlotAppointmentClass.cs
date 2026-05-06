using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorAppointmentSlotClass
    {
        private readonly string _db_conn;

        public DoctorAppointmentSlotClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // MASTER - INSERT
        // ─────────────────────────────────────────
        public async Task<string> InsertMaster(DoctorAppointmentSlotMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                data.slot_master_id = Guid.NewGuid();
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                string sql = @"INSERT INTO doctor_appointment_slot_master
                        (slot_master_id, dcode, tenant_code, day_of_week,
                         slot_start_time, slot_end_time, max_patients,
                         is_active, isdeleted, created_at, updated_at)
                       VALUES
                        (@slot_master_id, @dcode, @tenant_code, @day_of_week,
                         @slot_start_time, @slot_end_time, @max_patients,
                         @is_active, @isdeleted, @created_at, @updated_at)";

                await db.ExecuteAsync(sql, new
                {
                    data.slot_master_id,
                    data.dcode,
                    data.tenant_code,
                    data.day_of_week,
                    slot_start_time = data.slot_start_time.ToTimeSpan(), // ✅ only here
                    slot_end_time = data.slot_end_time.ToTimeSpan(),   // ✅ only here
                    data.max_patients,
                    data.is_active,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // MASTER - UPDATE
        // ─────────────────────────────────────────
        public async Task<string> UpdateMaster(DoctorAppointmentSlotMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                string sql = @"UPDATE doctor_appointment_slot_master
                       SET dcode           = @dcode,
                           tenant_code     = @tenant_code,
                           day_of_week     = @day_of_week,
                           slot_start_time = @slot_start_time,
                           slot_end_time   = @slot_end_time,
                           max_patients    = @max_patients,
                           is_active       = @is_active,
                           updated_at      = @updated_at
                       WHERE slot_master_id = @slot_master_id
                       AND   tenant_code    = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new
                {
                    data.dcode,
                    data.tenant_code,
                    data.day_of_week,
                    slot_start_time = data.slot_start_time.ToTimeSpan(), // ✅
                    slot_end_time = data.slot_end_time.ToTimeSpan(),   // ✅
                    data.max_patients,
                    data.is_active,
                    data.updated_at,
                    data.slot_master_id
                });

                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // MASTER - SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> DeleteMaster(Guid slot_master_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                string sql = @"UPDATE doctor_appointment_slot_master
                               SET isdeleted  = true,
                                   is_active  = false,
                                   updated_at = now()
                               WHERE slot_master_id = @slot_master_id
                               AND   tenant_code    = @tenant_code";
                await db.ExecuteAsync(sql, new { slot_master_id, tenant_code });
                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // MASTER - GET BY DOCTOR
        // ✅ Use raw SQL for SELECT to avoid Dapper.Contrib type-mapping issues
        //    with TimeOnly and DateTime on read
        // ─────────────────────────────────────────
        public async Task<List<DoctorAppointmentSlotMasterModel>> GetMasterByDoctor(
            int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT slot_master_id,
                                  dcode,
                                  tenant_code,
                                  day_of_week,
                                  slot_start_time,
                                  slot_end_time,
                                  max_patients,
                                  is_active,
                                  isdeleted,
                                  -- ✅ Cast timestamptz → timestamp AT UTC so C# DateTime maps cleanly
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   doctor_appointment_slot_master
                           WHERE  isdeleted   = false
                           AND    dcode       = @dcode
                           AND    tenant_code = @tenant_code
                           ORDER  BY day_of_week, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotMasterModel>(
                sql, new { dcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // DETAILS - INSERT
        // ─────────────────────────────────────────
        public async Task<string> InsertDetails(DoctorAppointmentSlotDetailsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                data.slot_detail_id = Guid.NewGuid();
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.booked_count = 0;
                data.slot_status = "OPEN";

                string sql = @"INSERT INTO doctor_appointment_slot_details
                        (slot_detail_id, slot_master_id, dcode, tenant_code,
                         appointment_date, slot_start_time, slot_end_time,
                         max_patients, booked_count, slot_status,
                         is_active, isdeleted, created_at, updated_at)
                       VALUES
                        (@slot_detail_id, @slot_master_id, @dcode, @tenant_code,
                         @appointment_date, @slot_start_time, @slot_end_time,
                         @max_patients, @booked_count, @slot_status,
                         @is_active, @isdeleted, @created_at, @updated_at)";

                await db.ExecuteAsync(sql, new
                {
                    data.slot_detail_id,
                    data.slot_master_id,
                    data.dcode,
                    data.tenant_code,
                    appointment_date = data.appointment_date.ToDateTime(TimeOnly.MinValue), // ✅ DateOnly → DateTime
                    slot_start_time = data.slot_start_time.ToTimeSpan(),                  // ✅ TimeOnly → TimeSpan
                    slot_end_time = data.slot_end_time.ToTimeSpan(),                    // ✅ TimeOnly → TimeSpan
                    data.max_patients,
                    data.booked_count,
                    data.slot_status,
                    data.is_active,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // DETAILS - UPDATE
        // ─────────────────────────────────────────
        public async Task<string> UpdateDetails(DoctorAppointmentSlotDetailsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                string sql = @"UPDATE doctor_appointment_slot_details
                       SET slot_master_id   = @slot_master_id,
                           dcode            = @dcode,
                           tenant_code      = @tenant_code,
                           appointment_date = @appointment_date,
                           slot_start_time  = @slot_start_time,
                           slot_end_time    = @slot_end_time,
                           max_patients     = @max_patients,
                           booked_count     = @booked_count,
                           slot_status      = @slot_status,
                           is_active        = @is_active,
                           updated_at       = @updated_at
                       WHERE slot_detail_id = @slot_detail_id
                       AND   tenant_code    = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new
                {
                    data.slot_master_id,
                    data.dcode,
                    data.tenant_code,
                    appointment_date = data.appointment_date.ToDateTime(TimeOnly.MinValue), // ✅ DateOnly → DateTime
                    slot_start_time = data.slot_start_time.ToTimeSpan(),                  // ✅ TimeOnly → TimeSpan
                    slot_end_time = data.slot_end_time.ToTimeSpan(),                    // ✅ TimeOnly → TimeSpan
                    data.max_patients,
                    data.booked_count,
                    data.slot_status,
                    data.is_active,
                    data.updated_at,
                    data.slot_detail_id
                });

                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex) { return ex.Message; }
        }
        // ─────────────────────────────────────────
        // DETAILS - SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> DeleteDetails(Guid slot_detail_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                string sql = @"UPDATE doctor_appointment_slot_details
                               SET isdeleted  = true,
                                   is_active  = false,
                                   updated_at = now()
                               WHERE slot_detail_id = @slot_detail_id
                               AND   tenant_code    = @tenant_code";
                await db.ExecuteAsync(sql, new { slot_detail_id, tenant_code });
                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // DETAILS - GET BY DATE (by dcode)
        // ─────────────────────────────────────────
        // BY dcode
        public async Task<List<DoctorAppointmentSlotDetailsModel>> GetDetailsByDate(
            int dcode, DateOnly appointment_date, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT slot_detail_id,
                          slot_master_id,
                          dcode,
                          tenant_code,
                          appointment_date,
                          slot_start_time,
                          slot_end_time,
                          max_patients,
                          booked_count,
                          slot_status,
                          is_active,
                          isdeleted,
                          created_at AT TIME ZONE 'UTC' AS created_at,
                          updated_at AT TIME ZONE 'UTC' AS updated_at
                   FROM   doctor_appointment_slot_details
                   WHERE  isdeleted        = false
                   AND    dcode            = @dcode
                   AND    appointment_date = @appointment_date
                   AND    tenant_code      = @tenant_code
                   ORDER  BY slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(
                sql, new
                {
                    dcode,
                    appointment_date = appointment_date.ToDateTime(TimeOnly.MinValue), // ✅
                    tenant_code
                });
            return res.ToList();
        }

        

        // ─────────────────────────────────────────
        // BOOK PATIENT
        // ─────────────────────────────────────────
        public async Task<string> BookPatient(Guid slot_detail_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                string sql = @"UPDATE doctor_appointment_slot_details
                               SET booked_count = booked_count + 1,
                                   slot_status  = CASE
                                                    WHEN booked_count + 1 >= max_patients THEN 'FULL'
                                                    ELSE 'OPEN'
                                                  END,
                                   updated_at   = now()
                               WHERE slot_detail_id = @slot_detail_id
                               AND   tenant_code    = @tenant_code
                               AND   slot_status    = 'OPEN'";
                int rows = await db.ExecuteAsync(sql, new { slot_detail_id, tenant_code });
                return rows > 0 ? "Success" : "Slot is FULL or not available";
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}