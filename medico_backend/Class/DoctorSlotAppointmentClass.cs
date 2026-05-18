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
        // MASTER - GET ALL
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotMasterModel>>
            GetAllMaster(string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_master_id,
                slotnum,
                dcode,
                tenant_code,
                day_of_week,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                avgtime,
                slot_date,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_master
            WHERE isdeleted = false
            AND is_cancel=false
            AND tenant_code = @tenant_code
            ORDER BY slot_date, slot_start_time";

            var res =
                await db.QueryAsync<DoctorAppointmentSlotMasterModel>(
                    sql,
                    new { tenant_code });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // MASTER - GET BY DOCTOR
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotMasterModel>>
            GetMasterByDoctor(
                int dcode,
                string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_master_id,
                slotnum,
                dcode,
                tenant_code,
                day_of_week,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                avgtime,
                slot_date,
                is_active,
                is_cancel,
                isdeleted,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_master
            WHERE isdeleted = false
            AND is_cancel=false
            AND dcode = @dcode
            AND tenant_code = @tenant_code
            ORDER BY slot_date, slot_start_time";

            var res =
                await db.QueryAsync<DoctorAppointmentSlotMasterModel>(
                    sql,
                    new
                    {
                        dcode,
                        tenant_code
                    });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // GET NEXT SLOT NUMBER
        // ═══════════════════════════════════════════
        public async Task<int> GetNextSlotNum(
            int dcode,
            string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT COALESCE(MAX(slotnum),0)+1
            FROM doctor_appointment_slot_master
            WHERE dcode = @dcode
            AND tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    dcode,
                    tenant_code
                });
        }

        // ═══════════════════════════════════════════
        // MASTER - BULK INSERT
        // ═══════════════════════════════════════════
        public async Task<object> BulkInsertMaster(
     List<DoctorAppointmentSlotMasterModel> slots,
     string tenant_code)
        {
            var inserted = new List<string>();
            var skipped = new List<string>();
            var failed = new List<string>();

            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            foreach (var data in slots)
            {
                try
                {
                    // ✅ VALIDATE DATE
                    if (!data.slot_date.HasValue)
                    {
                        failed.Add("slot_date is required");
                        continue;
                    }

                    // ✅ VALIDATE TIME
                    if (data.slot_start_time >= data.slot_end_time)
                    {
                        failed.Add(
                            $"Invalid time range : " +
                            $"{data.slot_start_time} - {data.slot_end_time}");

                        continue;
                    }


                    string overlapSql = @"
            SELECT COUNT(1)
            FROM doctor_appointment_slot_master
            WHERE isdeleted = false
            AND tenant_code = @tenant_code
            AND dcode = @dcode
            AND slot_date = @slot_date
            AND (
                    @new_start < slot_end_time
                AND @new_end   > slot_start_time
                )";

                    int overlapExists =
                        await db.ExecuteScalarAsync<int>(
                            overlapSql,
                            new
                            {
                                tenant_code,
                                data.dcode,

                                slot_date =
                                    data.slot_date.Value
                                        .ToDateTime(TimeOnly.MinValue),

                                new_start =
                                    data.slot_start_time
                                        .ToTimeSpan(),

                                new_end =
                                    data.slot_end_time
                                        .ToTimeSpan()
                            });

                    // ✅ IF SLOT EXISTS / OVERLAPS
                    if (overlapExists > 0)
                    {
                        skipped.Add(
                            $"Slot already exists or overlaps : " +
                            $"{data.slot_date:dd-MM-yyyy} " +
                            $"{data.slot_start_time} - " +
                            $"{data.slot_end_time}");

                        continue;
                    }

                    // ✅ GENERATE MASTER ID
                    data.slot_master_id = Guid.NewGuid();

                    // ✅ SLOT NUMBER
                    data.slotnum =
                        await GetNextSlotNum(
                            data.dcode,
                            tenant_code);

                    data.tenant_code = tenant_code;

                    data.created_at =
                        DateTime.SpecifyKind(
                            DateTime.UtcNow,
                            DateTimeKind.Utc);

                    data.updated_at =
                        DateTime.SpecifyKind(
                            DateTime.UtcNow,
                            DateTimeKind.Utc);

                    // ✅ INSERT MASTER
                    string masterSql = @"
            INSERT INTO doctor_appointment_slot_master
            (
                slot_master_id,
                slotnum,
                dcode,
                tenant_code,
                day_of_week,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                avgtime,
                slot_date,
                is_active,
                isdeleted,
                created_at,
                updated_at
            )
            VALUES
            (
                @slot_master_id,
                @slotnum,
                @dcode,
                @tenant_code,
                @day_of_week,
                @slot_start_time,
                @slot_end_time,
                @typeofslot,
                @max_patients,
                @max_walkin,
                @max_online,
                @avgtime,
                @slot_date,
                @is_active,
                @isdeleted,
                @created_at,
                @updated_at
            )";

                    await db.ExecuteAsync(masterSql, new
                    {
                        data.slot_master_id,
                        data.slotnum,
                        data.dcode,
                        data.tenant_code,
                        data.day_of_week,

                        slot_start_time =
                            data.slot_start_time
                                .ToTimeSpan(),

                        slot_end_time =
                            data.slot_end_time
                                .ToTimeSpan(),
                        data.typeofslot,
                        data.max_patients,
                        data.max_walkin,
                        data.max_online,
                        data.avgtime,
                        slot_date =
                            data.slot_date.Value
                                .ToDateTime(TimeOnly.MinValue),

                        data.is_active,
                        data.isdeleted,
                        data.created_at,
                        data.updated_at
                    });

                    // ✅ AUTO INSERT DETAILS
                    string detailSql = @"
            INSERT INTO doctor_appointment_slot_details
            (
                slot_detail_id,
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
                slot_status,
                is_active,
                isdeleted,
                created_at,
                updated_at
            )
            VALUES
            (
                @slot_detail_id,
                @slot_master_id,
                @dcode,
                @tenant_code,
                @appointment_date,
                @slot_start_time,
                @slot_end_time,
                @max_patients,
                @max_walkin,
                @max_online,
                0,
                0,
                0,
                'OPEN',
                @is_active,
                false,
                @created_at,
                @updated_at
            )";

                    await db.ExecuteAsync(detailSql, new
                    {
                        slot_detail_id = Guid.NewGuid(),

                        slot_master_id =
                            data.slot_master_id,

                        data.dcode,

                        tenant_code,

                        appointment_date =
                            data.slot_date.Value
                                .ToDateTime(TimeOnly.MinValue),

                        slot_start_time =
                            data.slot_start_time
                                .ToTimeSpan(),

                        slot_end_time =
                            data.slot_end_time
                                .ToTimeSpan(),

                        data.max_patients,
                        data.max_walkin,
                        data.max_online,

                        data.is_active,

                        created_at =
                            data.created_at,

                        updated_at =
                            data.updated_at
                    });

                    inserted.Add(
                        $"Inserted Successfully : " +
                        $"{data.slot_date:dd-MM-yyyy} " +
                        $"{data.slot_start_time} - " +
                        $"{data.slot_end_time}");
                }
                catch (Exception ex)
                {
                    failed.Add(ex.Message);
                }
            }

            return new
            {
                inserted,
                skipped,
                failed
            };
        }

        // ═══════════════════════════════════════════
        // MASTER - UPDATE
        // ═══════════════════════════════════════════
        public async Task<object> BulkUpdateMaster(
            List<DoctorAppointmentSlotMasterModel> slots,
            string tenant_code)
        {
            var updated = new List<string>();
            var failed = new List<string>();

            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            foreach (var data in slots)
            {
                try
                {
                    data.updated_at =
                        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                    string masterSql = @"
            UPDATE doctor_appointment_slot_master
            SET
                dcode = @dcode,
                day_of_week = @day_of_week,
                slot_start_time = @slot_start_time,
                slot_end_time = @slot_end_time,
                typeofslot = @typeofslot,
                max_patients = @max_patients,
                max_walkin = @max_walkin,
                max_online = @max_online,
                avgtime = @avgtime,
                slot_date = @slot_date,
                is_active = @is_active,
                updated_at = @updated_at
            WHERE slot_master_id = @slot_master_id
            AND tenant_code = @tenant_code";

                    int rows = await db.ExecuteAsync(masterSql, new
                    {
                        data.dcode,
                        data.day_of_week,

                        slot_start_time = data.slot_start_time.ToTimeSpan(),
                        slot_end_time = data.slot_end_time.ToTimeSpan(),

                        data.typeofslot,
                        data.max_patients,
                        data.max_walkin,
                        data.max_online,
                        data.avgtime,

                        slot_date = data.slot_date.HasValue
                            ? data.slot_date.Value.ToDateTime(TimeOnly.MinValue)
                            : (DateTime?)null,

                        data.is_active,
                        data.updated_at,
                        data.slot_master_id,
                        tenant_code
                    });

                    if (rows > 0)
                        updated.Add($"Updated: {data.slot_master_id}");
                    else
                        failed.Add($"Not Found: {data.slot_master_id}");
                }
                catch (Exception ex)
                {
                    failed.Add(ex.Message);
                }
            }

            return new
            {
                updated,
                failed
            };
        }

        // ═══════════════════════════════════════════
        // MASTER - DELETE
        // ═══════════════════════════════════════════
        public async Task<string> DeleteMaster(
            Guid slot_master_id,
            string tenant_code)
        {
            try
            {
                using IDbConnection db =
                    new NpgsqlConnection(_db_conn);

                // ✅ DELETE MASTER
                string masterSql = @"
                UPDATE doctor_appointment_slot_master
                SET
                    isdeleted = true,
                    is_active = false,
                    updated_at = now()
                WHERE slot_master_id = @slot_master_id
                AND tenant_code = @tenant_code";

                await db.ExecuteAsync(masterSql, new
                {
                    slot_master_id,
                    tenant_code
                });

                // ✅ DELETE DETAILS
                string detailSql = @"
                UPDATE doctor_appointment_slot_details
                SET
                    isdeleted = true,
                    is_active = false,
                    updated_at = now()
                WHERE slot_master_id = @slot_master_id
                AND tenant_code = @tenant_code";

                await db.ExecuteAsync(detailSql, new
                {
                    slot_master_id,
                    tenant_code
                });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════════
        // DETAILS - GET ALL
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotDetailsModel>>
            GetAllDetails(string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_detail_id,
                slot_master_id,
                dcode,
                tenant_code,
                appointment_date,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                booked_count,
                walkin_count,
                online_count,
                (max_patients - booked_count)
                    AS remaining_seats,
                slot_status,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC'
                    AS created_at,
                updated_at AT TIME ZONE 'UTC'
                    AS updated_at
            FROM doctor_appointment_slot_details
            WHERE isdeleted = false
            AND tenant_code = @tenant_code
            ORDER BY appointment_date,
                     slot_start_time";

            var res =
                await db.QueryAsync<
                    DoctorAppointmentSlotDetailsModel>(
                    sql,
                    new { tenant_code });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // DETAILS - GET BY DATE
        // ═══════════════════════════════════════════
        public async Task<
            List<DoctorAppointmentSlotDetailsModel>>
            GetDetailsByDate(
                int dcode,
                DateOnly appointment_date,
                string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_detail_id,
                slot_master_id,
                dcode,
                tenant_code,
                appointment_date,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                booked_count,
                walkin_count,
                online_count,
                (max_patients - booked_count)
                    AS remaining_seats,
                slot_status,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC'
                    AS created_at,
                updated_at AT TIME ZONE 'UTC'
                    AS updated_at
            FROM doctor_appointment_slot_details
            WHERE isdeleted = false
            AND dcode = @dcode
            AND appointment_date = @appointment_date
            AND tenant_code = @tenant_code
            ORDER BY slot_start_time";

            var res =
                await db.QueryAsync<
                    DoctorAppointmentSlotDetailsModel>(
                    sql,
                    new
                    {
                        dcode,

                        appointment_date =
                            appointment_date
                                .ToDateTime(
                                    TimeOnly.MinValue),

                        tenant_code
                    });

            return res.ToList();
        }

        public async Task<string> CancelSlot(
    Guid slot_master_id,
    string tenant_code,
    string cancel_reason)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
        UPDATE doctor_appointment_slot_master
        SET
            is_cancel = true,
            cancel_reason = @cancel_reason,
            is_active = false,
            updated_at = now()
        WHERE slot_master_id = @slot_master_id
        AND tenant_code = @tenant_code
        AND isdeleted = false";

            int rows = await db.ExecuteAsync(sql, new
            {
                slot_master_id,
                tenant_code,
                cancel_reason
            });

            if (rows == 0)
                return "No record found";

            // OPTIONAL: also cancel details
            string detailSql = @"
        UPDATE doctor_appointment_slot_details
        SET
            slot_status = 'CANCELLED',
            is_active = false,
            updated_at = now()
        WHERE slot_master_id = @slot_master_id
        AND tenant_code = @tenant_code";

            await db.ExecuteAsync(detailSql, new
            {
                slot_master_id,
                tenant_code
            });

            return "Cancelled Successfully";
        }
    }
}