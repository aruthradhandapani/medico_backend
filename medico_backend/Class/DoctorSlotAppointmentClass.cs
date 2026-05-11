using Dapper;
using Npgsql;
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

        // ═══════════════════════════════════════════
        // MASTER
        // ═══════════════════════════════════════════

        // ─────────────────────────────────────────
        // MASTER - GET ALL
        // ─────────────────────────────────────────
        public async Task<List<DoctorAppointmentSlotMasterModel>> GetAllMaster(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT slot_master_id,
                                  dcode,
                                  tenant_code,
                                  day_of_week,
                                  slot_start_time,
                                  slot_end_time,
                                  max_patients,
                                  max_walkin,
                                  max_online,
                                  slot_date,
                                  is_active,
                                  isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   doctor_appointment_slot_master
                           WHERE  isdeleted   = false
                           AND    tenant_code = @tenant_code
                           ORDER  BY dcode, day_of_week, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotMasterModel>(
                sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // MASTER - GET BY DOCTOR
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
                                  max_walkin,
                                  max_online,
                                  slot_date,
                                  is_active,
                                  isdeleted,
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
        // MASTER - BULK INSERT
        // ─────────────────────────────────────────
        public async Task<object> BulkInsertMaster(
            List<DoctorAppointmentSlotMasterModel> slots,
            string tenant_code)
        {
            var success = new List<string>();
            var skipped = new List<string>();
            var failed = new List<string>();

            using IDbConnection db = new NpgsqlConnection(_db_conn);

            foreach (var data in slots)
            {
                try
                {
                    string checkSql = @"SELECT COUNT(1)
                                FROM doctor_appointment_slot_master
                                WHERE isdeleted       = false
                                AND tenant_code       = @tenant_code
                                AND dcode             = @dcode
                                AND day_of_week       = @day_of_week
                                AND slot_start_time   = @slot_start_time
                                AND slot_end_time     = @slot_end_time";

                    int exists = await db.ExecuteScalarAsync<int>(checkSql, new
                    {
                        tenant_code,
                        data.dcode,
                        data.day_of_week,
                        slot_start_time = data.slot_start_time.ToTimeSpan(),
                        slot_end_time = data.slot_end_time.ToTimeSpan()
                    });

                    if (exists > 0)
                    {
                        skipped.Add(
                            $"{data.day_of_week} {data.slot_start_time}-{data.slot_end_time}");
                        continue;
                    }

                    data.slot_master_id = Guid.NewGuid();
                    data.tenant_code = tenant_code;

                    data.created_at = DateTime.UtcNow;
                    data.updated_at = DateTime.UtcNow;

                    string sql = @"INSERT INTO doctor_appointment_slot_master
                    (
                        slot_master_id,
                        dcode,
                        tenant_code,
                        day_of_week,
                        slot_start_time,
                        slot_end_time,
                        max_patients,
                        max_walkin,
                        max_online,
                        slot_date,
                        is_active,
                        isdeleted,
                        created_at,
                        updated_at
                    )
                    VALUES
                    (
                        @slot_master_id,
                        @dcode,
                        @tenant_code,
                        @day_of_week,
                        @slot_start_time,
                        @slot_end_time,
                        @max_patients,
                        @max_walkin,
                        @max_online,
                        @slot_date,
                        @is_active,
                        @isdeleted,
                        @created_at,
                        @updated_at
                    )";

                    await db.ExecuteAsync(sql, new
                    {
                        data.slot_master_id,
                        data.dcode,
                        data.tenant_code,
                        data.day_of_week,
                        slot_start_time = data.slot_start_time.ToTimeSpan(),
                        slot_end_time = data.slot_end_time.ToTimeSpan(),
                        data.max_patients,
                        data.max_walkin,
                        data.max_online,
                        slot_date = data.slot_date.HasValue
                            ? data.slot_date.Value.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,
                        data.is_active,
                        data.isdeleted,
                        data.created_at,
                        data.updated_at
                    });

                    success.Add(
                        $"{data.day_of_week} {data.slot_start_time}-{data.slot_end_time}");
                }
                catch (Exception ex)
                {
                    failed.Add(ex.Message);
                }
            }

            return new
            {
                inserted = success,
                skipped = skipped,
                failed = failed
            };
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
                                   day_of_week     = @day_of_week,
                                   slot_start_time = @slot_start_time,
                                   slot_end_time   = @slot_end_time,
                                   max_patients    = @max_patients,
                                   max_walkin      = @max_walkin,
                                   max_online      = @max_online,
                                   slot_date       = @slot_date,
                                   is_active       = @is_active,
                                   updated_at      = @updated_at
                               WHERE slot_master_id = @slot_master_id
                               AND   tenant_code    = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new
                {
                    data.dcode,
                    data.day_of_week,
                    slot_start_time = data.slot_start_time.ToTimeSpan(),
                    slot_end_time = data.slot_end_time.ToTimeSpan(),
                    data.max_patients,
                    data.max_walkin,
                    data.max_online,
                    slot_date = data.slot_date.HasValue
                        ? data.slot_date.Value.ToDateTime(TimeOnly.MinValue)
                        : (DateTime?)null,
                    data.is_active,
                    data.updated_at,
                    data.slot_master_id,
                    data.tenant_code
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

        // ═══════════════════════════════════════════
        // DETAILS
        // ═══════════════════════════════════════════

        // ─────────────────────────────────────────
        // DETAILS - GET ALL
        // ─────────────────────────────────────────
        public async Task<List<DoctorAppointmentSlotDetailsModel>> GetAllDetails(string tenant_code)
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
                                  max_walkin,
                                  max_online,
                                  booked_count,
                                  walkin_count,
                                  online_count,
                                  (max_patients - booked_count) AS remaining_seats,
                                  slot_status,
                                  is_active,
                                  isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   doctor_appointment_slot_details
                           WHERE  isdeleted   = false
                           AND    tenant_code = @tenant_code
                           ORDER  BY appointment_date, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(
                sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // DETAILS - GET BY DATE
        // ─────────────────────────────────────────
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
                                  max_walkin,
                                  max_online,
                                  booked_count,
                                  walkin_count,
                                  online_count,
                                  (max_patients - booked_count) AS remaining_seats,
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

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(sql, new
            {
                dcode,
                appointment_date = appointment_date.ToDateTime(TimeOnly.MinValue),
                tenant_code
            });
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
                data.booked_count = 0;
                data.walkin_count = 0;
                data.online_count = 0;
                data.slot_status = "OPEN";
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                string sql = @"INSERT INTO doctor_appointment_slot_details
                        (slot_detail_id, slot_master_id, dcode, tenant_code,
                         appointment_date, slot_start_time, slot_end_time,
                         max_patients, max_walkin, max_online,
                         booked_count, walkin_count, online_count,
                         slot_status, is_active, isdeleted, created_at, updated_at)
                       VALUES
                        (@slot_detail_id, @slot_master_id, @dcode, @tenant_code,
                         @appointment_date, @slot_start_time, @slot_end_time,
                         @max_patients, @max_walkin, @max_online,
                         @booked_count, @walkin_count, @online_count,
                         @slot_status, @is_active, @isdeleted, @created_at, @updated_at)";

                await db.ExecuteAsync(sql, new
                {
                    data.slot_detail_id,
                    data.slot_master_id,
                    data.dcode,
                    data.tenant_code,
                    appointment_date = data.appointment_date.ToDateTime(TimeOnly.MinValue),
                    slot_start_time = data.slot_start_time.ToTimeSpan(),
                    slot_end_time = data.slot_end_time.ToTimeSpan(),
                    data.max_patients,
                    data.max_walkin,
                    data.max_online,
                    data.booked_count,
                    data.walkin_count,
                    data.online_count,
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
                                   appointment_date = @appointment_date,
                                   slot_start_time  = @slot_start_time,
                                   slot_end_time    = @slot_end_time,
                                   max_patients     = @max_patients,
                                   max_walkin       = @max_walkin,
                                   max_online       = @max_online,
                                   slot_status      = @slot_status,
                                   is_active        = @is_active,
                                   updated_at       = @updated_at
                               WHERE slot_detail_id = @slot_detail_id
                               AND   tenant_code    = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new
                {
                    data.slot_master_id,
                    data.dcode,
                    appointment_date = data.appointment_date.ToDateTime(TimeOnly.MinValue),
                    slot_start_time = data.slot_start_time.ToTimeSpan(),
                    slot_end_time = data.slot_end_time.ToTimeSpan(),
                    data.max_patients,
                    data.max_walkin,
                    data.max_online,
                    data.slot_status,
                    data.is_active,
                    data.updated_at,
                    data.slot_detail_id,
                    data.tenant_code
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
        // DETAILS - BULK INSERT BY DATE LIST
        // ─────────────────────────────────────────
        public async Task<object> BulkInsertDetails(
            BulkInsertSlotDetailsRequest request, string tenant_code)
        {
            var success_dates = new List<string>();
            var skipped_dates = new List<string>();
            var failed_dates = new List<string>();

            using IDbConnection db = new NpgsqlConnection(_db_conn);

            foreach (var date in request.appointment_dates)
            {
                try
                {
                    // ✅ Duplicate check per date
                    string checkSql = @"SELECT COUNT(1)
                                        FROM   doctor_appointment_slot_details
                                        WHERE  isdeleted        = false
                                        AND    dcode            = @dcode
                                        AND    tenant_code      = @tenant_code
                                        AND    slot_master_id   = @slot_master_id
                                        AND    appointment_date = @appointment_date
                                        AND    slot_start_time  = @slot_start_time
                                        AND    slot_end_time    = @slot_end_time";

                    int exists = await db.ExecuteScalarAsync<int>(checkSql, new
                    {
                        request.dcode,
                        tenant_code,
                        request.slot_master_id,
                        appointment_date = date.ToDateTime(TimeOnly.MinValue),
                        slot_start_time = request.slot_start_time.ToTimeSpan(),
                        slot_end_time = request.slot_end_time.ToTimeSpan()
                    });

                    if (exists > 0)
                    {
                        skipped_dates.Add(date.ToString("yyyy-MM-dd") + " (already exists)");
                        continue;
                    }

                    string insertSql = @"INSERT INTO doctor_appointment_slot_details
                            (slot_detail_id, slot_master_id, dcode, tenant_code,
                             appointment_date, slot_start_time, slot_end_time,
                             max_patients, max_walkin, max_online,
                             booked_count, walkin_count, online_count,
                             slot_status, is_active, isdeleted, created_at, updated_at)
                           VALUES
                            (@slot_detail_id, @slot_master_id, @dcode, @tenant_code,
                             @appointment_date, @slot_start_time, @slot_end_time,
                             @max_patients, @max_walkin, @max_online,
                             0, 0, 0,
                             'OPEN', @is_active, false,
                             @created_at, @updated_at)";

                    await db.ExecuteAsync(insertSql, new
                    {
                        slot_detail_id = Guid.NewGuid(),
                        request.slot_master_id,
                        request.dcode,
                        tenant_code,
                        appointment_date = date.ToDateTime(TimeOnly.MinValue),
                        slot_start_time = request.slot_start_time.ToTimeSpan(),
                        slot_end_time = request.slot_end_time.ToTimeSpan(),
                        request.max_patients,
                        request.max_walkin,
                        request.max_online,
                        request.is_active,
                        created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                    });

                    success_dates.Add(date.ToString("yyyy-MM-dd"));
                }
                catch (Exception ex)
                {
                    failed_dates.Add(date.ToString("yyyy-MM-dd") + $" (error: {ex.Message})");
                }
            }

            return new
            {
                total_requested = request.appointment_dates.Count,
                total_inserted = success_dates.Count,
                total_skipped = skipped_dates.Count,
                total_failed = failed_dates.Count,
                inserted_dates = success_dates,
                skipped_dates = skipped_dates,
                failed_dates = failed_dates
            };
        }
    }
}