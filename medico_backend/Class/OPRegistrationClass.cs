using Dapper;
using Npgsql;
using System.Data;
using static medico_backend.Model.OPRegistrationModel;

namespace medico_backend.Class
{
    public class OpRegistrationClass
    {
        private readonly string _db_conn;

        public OpRegistrationClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // GENERATE OP NUMBER
        // Format: OPD/2026/05/0001 — resets each month
        // ─────────────────────────────────────────
        private async Task<string> GenerateOpNo(IDbConnection db, string tenant_code)
        {
            string sql = @"SELECT COALESCE(MAX(
                           CAST(SPLIT_PART(op_no, '/', 4) AS INT)
                       ), 0) + 1
                       FROM   op_registration
                       WHERE  tenant_code = @tenant_code
                       AND    isdeleted   = false
                       AND    SPLIT_PART(op_no, '/', 2) = @year
                       AND    SPLIT_PART(op_no, '/', 3) = @month";

            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month });
            return $"OPD/{year}/{month}/{next:D4}";
        }
        public async Task<string> CreateOpRegistration(OpRegistrationModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // ── 1. Validate visit_type ──────────────────────────────
                var allowedVisitTypes = new[] { "NEWVISIT", "FOLLOWUP" };
                if (!allowedVisitTypes.Contains(data.visit_type.ToUpper()))
                    return "Invalid visit_type. Allowed: NEWVISIT, FOLLOWUP";
                data.visit_type = data.visit_type.ToUpper();

                // ── 2. Validate reg_type ────────────────────────────────
                var allowedRegTypes = new[] { "WALKIN", "ONLINE" };
                if (!allowedRegTypes.Contains(data.reg_type.ToUpper()))
                    return "Invalid reg_type. Allowed: WALKIN, ONLINE";
                data.reg_type = data.reg_type.ToUpper();

                // ── 3. Both WALKIN and ONLINE use booking flow ──────────
                //       because WALKIN is now booked via /book endpoint
                //       so both always have a booking_id
                if (!data.booking_id.HasValue || data.booking_id == Guid.Empty)
                    return "booking_id is required. Both WALKIN and ONLINE must be pre-booked.";

                string checkBookingSql = @"SELECT booking_status, token_no,
                                          slot_detail_id, booking_type,
                                          booking_no
                                   FROM   appointment_booking
                                   WHERE  booking_id  = @booking_id
                                   AND    tenant_code = @tenant_code
                                   AND    isdeleted   = false";

                var booking = await db.QueryFirstOrDefaultAsync(
                    checkBookingSql, new { data.booking_id, data.tenant_code });

                if (booking == null)
                    return "Booking not found";

                if (booking.booking_status == "CANCELLED")
                    return "Cannot register a cancelled booking";

                if (booking.booking_status == "VISITED")
                    return "Patient already registered for this booking";

                // ✅ Carry token from booking — assigned at booking time
                data.token_no = (int)booking.token_no;

                // ✅ Carry reg_type from booking_type (WALKIN/ONLINE)
                data.reg_type = ((string)booking.booking_type).ToUpper();

                // ✅ Carry booking_no from booking
                data.booking_no = (string)booking.booking_no;

                // ✅ Carry slot from booking if not provided
                if (data.slot_detail_id == null || data.slot_detail_id == Guid.Empty)
                    data.slot_detail_id = (Guid?)booking.slot_detail_id;

                // ✅ Mark booking as VISITED
                await db.ExecuteAsync(
                    @"UPDATE appointment_booking
              SET    booking_status = 'VISITED',
                     updated_at     = now()
              WHERE  booking_id     = @booking_id
              AND    tenant_code    = @tenant_code",
                    new { data.booking_id, data.tenant_code });

                // ── 4. Set defaults & insert ────────────────────────────
                data.op_id = Guid.NewGuid();
                data.op_no = await GenerateOpNo(db, data.tenant_code!);
                data.visit_date = DateOnly.FromDateTime(DateTime.UtcNow);
                data.visit_status = "WAITING";
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                await db.ExecuteAsync(@"INSERT INTO op_registration
    (op_id, op_no, booking_id, booking_no, slot_detail_id, custid, dcode,
     department_code, visit_type, reg_type, visit_date,
     token_no, queue_no, visit_status, notes,
     tenant_code, isdeleted, created_at, updated_at)
   VALUES
    (@op_id, @op_no, @booking_id, @booking_no, @slot_detail_id, @custid, @dcode,
     @department_code, @visit_type, @reg_type, @visit_date,
     @token_no, @queue_no, @visit_status, @notes,
     @tenant_code, @isdeleted, @created_at, @updated_at)", new
                {
                    data.op_id,
                    data.op_no,
                    data.booking_id,
                    data.booking_no,
                    data.slot_detail_id,
                    data.custid,
                    data.dcode,
                    data.department_code,
                    data.visit_type,
                    data.reg_type,
                    visit_date = data.visit_date.ToDateTime(TimeOnly.MinValue),
                    data.token_no,
                    data.queue_no,
                    data.visit_status,
                    data.notes,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

                return $"Success|OpNo:{data.op_no}|OpId:{data.op_id}|Token:{data.token_no}|RegType:{data.reg_type}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // UPDATE VISIT STATUS
        // WAITING → IN_CONSULTATION → COMPLETED
        // ─────────────────────────────────────────
        public async Task<string> UpdateVisitStatus(
            Guid op_id, string visit_status, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var allowed = new[] { "WAITING", "IN_CONSULTATION", "COMPLETED", "CANCELLED" };
                if (!allowed.Contains(visit_status.ToUpper()))
                    return $"Invalid status. Allowed: {string.Join(", ", allowed)}";

                string sql = @"UPDATE op_registration
                           SET visit_status = @visit_status,
                               updated_at   = now()
                           WHERE op_id      = @op_id
                           AND   tenant_code = @tenant_code
                           AND   isdeleted   = false";

                int rows = await db.ExecuteAsync(sql,
                    new { op_id, visit_status = visit_status.ToUpper(), tenant_code });

                return rows > 0 ? "Success" : "OP Registration not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // SAVE VITALS
        // ─────────────────────────────────────────
        public async Task<string> SaveVitals(PatientVitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Validate op exists
                string checkSql = @"SELECT op_id FROM op_registration
                                WHERE  op_id       = @op_id
                                AND    tenant_code = @tenant_code
                                AND    isdeleted   = false";

                var op = await db.QueryFirstOrDefaultAsync(
                    checkSql, new { data.op_id, data.tenant_code });

                if (op == null) return "OP Registration not found";

                // Auto calculate BMI if height and weight provided
                if (data.height_cm > 0 && data.weight_kg > 0)
                {
                    decimal heightM = data.height_cm.Value / 100;
                    data.bmi = Math.Round(data.weight_kg.Value / (heightM * heightM), 2);
                }

                data.vital_id = Guid.NewGuid();
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                string insertSql = @"INSERT INTO patient_vitals
                (vital_id, op_id, op_no, custid, dcode,
                 height_cm, weight_kg, bmi, temperature_f,
                 pulse_rate, respiratory_rate,
                 bp_systolic, bp_diastolic, spo2,
                 sugar_level, pain_scale, allergy_notes,
                 hba1c, ecg_notes, head_circumference_cm,
                 entered_by, tenant_code, isdeleted, created_at, updated_at)
               VALUES
                (@vital_id, @op_id, @op_no, @custid, @dcode,
                 @height_cm, @weight_kg, @bmi, @temperature_f,
                 @pulse_rate, @respiratory_rate,
                 @bp_systolic, @bp_diastolic, @spo2,
                 @sugar_level, @pain_scale, @allergy_notes,
                 @hba1c, @ecg_notes, @head_circumference_cm,
                 @entered_by, @tenant_code, @isdeleted, @created_at, @updated_at)";

                await db.ExecuteAsync(insertSql, data);

                return $"Success|VitalId:{data.vital_id}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // GET TODAY'S OP LIST BY DOCTOR
        // ─────────────────────────────────────────
        public async Task<List<OpRegistrationModel>> GetTodayOpList(
            int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM op_registration
                       WHERE  isdeleted   = false
                       AND    dcode       = @dcode
                       AND    visit_date  = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date
                       AND    tenant_code = @tenant_code
                       ORDER  BY queue_no, token_no";

            var res = await db.QueryAsync<OpRegistrationModel>(sql, new { dcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET ALL OP LIST (with optional filters)
        // ─────────────────────────────────────────
        public async Task<List<OpRegistrationModel>> GetAllOpList(
            string tenant_code, int? dcode = null, DateOnly? from_date = null,
            DateOnly? to_date = null, string? visit_status = null)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM op_registration
                   WHERE isdeleted = false
                   AND tenant_code = @tenant_code
                   AND (@dcode IS NULL OR dcode = @dcode)
                   AND (@from_date IS NULL OR visit_date >= @from_date)
                   AND (@to_date IS NULL OR visit_date <= @to_date)
                   AND (@visit_status IS NULL OR visit_status = @visit_status)
                   ORDER BY visit_date DESC, queue_no, token_no";

            var res = await db.QueryAsync<OpRegistrationModel>(sql, new
            {
                tenant_code,
                dcode,
                from_date = from_date.HasValue ? from_date.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                to_date = to_date.HasValue ? to_date.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                visit_status = visit_status?.ToUpper()
            });

            return res.ToList();
        }

        // ─────────────────────────────────────────
        // UPDATE VITALS
        // ─────────────────────────────────────────
        public async Task<string> UpdateVitals(PatientVitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Check vital exists
                string checkSql = @"SELECT vital_id FROM patient_vitals
                            WHERE vital_id    = @vital_id
                            AND   tenant_code = @tenant_code
                            AND   isdeleted   = false";

                var existing = await db.QueryFirstOrDefaultAsync(
                    checkSql, new { data.vital_id, data.tenant_code });

                if (existing == null) return "Vital record not found";

                // Recalculate BMI if height/weight updated
                if (data.height_cm > 0 && data.weight_kg > 0)
                {
                    decimal heightM = data.height_cm.Value / 100;
                    data.bmi = Math.Round(data.weight_kg.Value / (heightM * heightM), 2);
                }

                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                string sql = @"UPDATE patient_vitals SET
                        height_cm             = @height_cm,
                        weight_kg             = @weight_kg,
                        bmi                   = @bmi,
                        temperature_f         = @temperature_f,
                        pulse_rate            = @pulse_rate,
                        respiratory_rate      = @respiratory_rate,
                        bp_systolic           = @bp_systolic,
                        bp_diastolic          = @bp_diastolic,
                        spo2                  = @spo2,
                        sugar_level           = @sugar_level,
                        pain_scale            = @pain_scale,
                        allergy_notes         = @allergy_notes,
                        hba1c                 = @hba1c,
                        ecg_notes             = @ecg_notes,
                        head_circumference_cm = @head_circumference_cm,
                        entered_by            = @entered_by,
                        updated_at            = @updated_at
                       WHERE vital_id    = @vital_id
                       AND   tenant_code = @tenant_code
                       AND   isdeleted   = false";

                int rows = await db.ExecuteAsync(sql, data);
                return rows > 0 ? "Success" : "Update failed";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // GET ALL VITALS — by op_id or custid
        // ─────────────────────────────────────────
        public async Task<List<PatientVitalsModel>> GetAllVitals(
            string tenant_code, Guid? op_id = null, decimal? custid = null)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM patient_vitals
                   WHERE isdeleted   = false
                   AND   tenant_code = @tenant_code
                   AND   (@op_id   IS NULL OR op_id  = @op_id)
                   AND   (@custid  IS NULL OR custid = @custid)
                   ORDER BY created_at DESC";

            var res = await db.QueryAsync<PatientVitalsModel>(sql, new { tenant_code, op_id, custid });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET PARTICULAR VITAL BY vital_id
        // ─────────────────────────────────────────
        public async Task<PatientVitalsModel?> GetVitalById(
            Guid vital_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM patient_vitals
                   WHERE vital_id    = @vital_id
                   AND   tenant_code = @tenant_code
                   AND   isdeleted   = false";

            return await db.QueryFirstOrDefaultAsync<PatientVitalsModel>(
                sql, new { vital_id, tenant_code });
        }
    }
}
