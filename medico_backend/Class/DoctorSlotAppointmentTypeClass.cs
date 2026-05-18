using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorAppointmentSlotTypeClass
    {
        private readonly string _db_conn;

        public DoctorAppointmentSlotTypeClass(
            IConfiguration configuration)
        {
            _db_conn =
                configuration.GetConnectionString("conn")!;
        }

        // ═══════════════════════════════════════
        // GET ALL
        // ═══════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotTypeModel>>
            GetAll(string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_type_id,
                name,
                shortname,
                colorcode,
                description,
                is_visiting,
                emoji,
                entereddate AT TIME ZONE 'UTC'
                    AS entereddate,
                ibsdate AT TIME ZONE 'UTC'
                    AS ibsdate,
                deleted,
                tenant_code
            FROM doctor_appointment_slot_type
            WHERE deleted = false
            AND tenant_code = @tenant_code
            ORDER BY slot_type_id DESC";

            var res =
                await db.QueryAsync<
                    DoctorAppointmentSlotTypeModel>(
                    sql,
                    new { tenant_code });

            return res.ToList();
        }

        // ═══════════════════════════════════════
        // GET BY ID
        // ═══════════════════════════════════════
        public async Task<DoctorAppointmentSlotTypeModel?>
            GetById(
                long slot_type_id,
                string tenant_code)
        {
            using IDbConnection db =
                new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_type_id,
                name,
                shortname,
                colorcode,
                description,
                is_visiting,
                emoji,
                entereddate AT TIME ZONE 'UTC'
                    AS entereddate,
                ibsdate AT TIME ZONE 'UTC'
                    AS ibsdate,
                deleted,
                tenant_code
            FROM doctor_appointment_slot_type
            WHERE slot_type_id = @slot_type_id
            AND tenant_code = @tenant_code
            AND deleted = false";

            return await db.QueryFirstOrDefaultAsync
            <DoctorAppointmentSlotTypeModel>(
                sql,
                new
                {
                    slot_type_id,
                    tenant_code
                });
        }

        // ═══════════════════════════════════════
        // INSERT
        // ═══════════════════════════════════════
        public async Task<string> Insert(
            DoctorAppointmentSlotTypeModel data)
        {
            try
            {
                using IDbConnection db =
                    new NpgsqlConnection(_db_conn);

                string checkSql = @"
                SELECT COUNT(1)
                FROM doctor_appointment_slot_type
                WHERE LOWER(name) = LOWER(@name)
                AND tenant_code = @tenant_code
                AND deleted = false";

                int exists =
                    await db.ExecuteScalarAsync<int>(
                        checkSql,
                        new
                        {
                            data.name,
                            data.tenant_code
                        });

                if (exists > 0)
                    return "Slot Type Already Exists";

                data.entereddate =
                    DateTime.SpecifyKind(
                        DateTime.UtcNow,
                        DateTimeKind.Utc);

                data.ibsdate =
                    DateTime.SpecifyKind(
                        DateTime.UtcNow,
                        DateTimeKind.Utc);

                string sql = @"
                INSERT INTO doctor_appointment_slot_type
                (
                    name,
                    shortname,
                    colorcode,
                    description,
                    is_visiting,
                    emoji,
                    entereddate,
                    ibsdate,
                    deleted,
                    tenant_code
                )
                VALUES
                (
                    @name,
                    @shortname,
                    @colorcode,
                    @description,
                    @is_visiting,
                    @emoji,
                    @entereddate,
                    @ibsdate,
                    false,
                    @tenant_code
                )";

                await db.ExecuteAsync(sql, data);

                return "Inserted Successfully";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════
        public async Task<string> Update(
            DoctorAppointmentSlotTypeModel data)
        {
            try
            {
                using IDbConnection db =
                    new NpgsqlConnection(_db_conn);

                data.ibsdate =
                    DateTime.SpecifyKind(
                        DateTime.UtcNow,
                        DateTimeKind.Utc);

                string sql = @"
                UPDATE doctor_appointment_slot_type
                SET
                    name = @name,
                    shortname = @shortname,
                    colorcode = @colorcode,
                    description = @description,
                    is_visiting = @is_visiting,
                    emoji = @emoji,
                    ibsdate = @ibsdate
                WHERE slot_type_id = @slot_type_id
                AND tenant_code = @tenant_code";

                int rows =
                    await db.ExecuteAsync(sql, data);

                return rows > 0
                    ? "Updated Successfully"
                    : "No Data Found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════
        // DELETE
        // ═══════════════════════════════════════
        public async Task<string> Delete(
            long slot_type_id,
            string tenant_code)
        {
            try
            {
                using IDbConnection db =
                    new NpgsqlConnection(_db_conn);

                string sql = @"
                UPDATE doctor_appointment_slot_type
                SET
                    deleted = true,
                    ibsdate = now()
                WHERE slot_type_id = @slot_type_id
                AND tenant_code = @tenant_code";

                int rows =
                    await db.ExecuteAsync(sql,
                    new
                    {
                        slot_type_id,
                        tenant_code
                    });

                return rows > 0
                    ? "Deleted Successfully"
                    : "No Data Found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}