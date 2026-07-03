using Dapper;
using medico_backend.Model;
using Medico_Backend.Model;
using Npgsql;
using System.Data;
using static medico_backend.Model.OPRegistrationModel;

namespace medico_backend.Class
{
    public class OpRegistrationClass
    {
        private readonly string _db_conn;
        private readonly UnbilledChargesClass _unbilledCls;   // ADD

        public OpRegistrationClass(IConfiguration configuration, UnbilledChargesClass unbilledCls)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
            _unbilledCls = unbilledCls;
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

                // ── 5. Auto-add consultation fee to unbilledcharges ─────
                // NOTE: you'll need a source for tcode/rate/amount — either
                // hardcode a default, pull from a doctor-fee master table,
                // or accept it as an optional field on OpRegistrationModel.
                // Placeholder below uses a fixed rate; replace with your actual lookup.
                // ── 5. Auto-add consultation fee to unbilledcharges ─────
                var (feeTcode, feeRate, feeAmount) = await GetDoctorConsultationFee(db, data.dcode, data.tenant_code!);

                await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                {
                    op_id = data.op_id.ToString(),
                    custid = data.custid,
                    tcode = feeTcode,
                    rate = feeRate,
                    amount = feeAmount,
                    quantity = 1
                }, data.tenant_code!);

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
             sugar_level, pain_scale,
             waist_cm, hip_cm,
             pedal_oedema, jvp, cvs, rs, cns, abdomen,
             cardiac_monitor, cd_echo, blood_chemistry,
             allergy_notes, hba1c, ecg_notes, head_circumference_cm,
             entered_by, tenant_code, isdeleted, created_at, updated_at)
           VALUES
            (@vital_id, @op_id, @op_no, @custid, @dcode,
             @height_cm, @weight_kg, @bmi, @temperature_f,
             @pulse_rate, @respiratory_rate,
             @bp_systolic, @bp_diastolic, @spo2,
             @sugar_level, @pain_scale,
             @waist_cm, @hip_cm,
             @pedal_oedema, @jvp, @cvs, @rs, @cns, @abdomen,
             @cardiac_monitor, @cd_echo, @blood_chemistry,
             @allergy_notes, @hba1c, @ecg_notes, @head_circumference_cm,
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
                waist_cm              = @waist_cm,
                hip_cm                = @hip_cm,
                pedal_oedema          = @pedal_oedema,
                jvp                   = @jvp,
                cvs                   = @cvs,
                rs                    = @rs,
                cns                   = @cns,
                abdomen               = @abdomen,
                cardiac_monitor       = @cardiac_monitor,
                cd_echo               = @cd_echo,
                blood_chemistry       = @blood_chemistry,
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
        // ─────────────────────────────────────────
        // DIRECT WALK-IN REGISTRATION
        // Flow 1: Patient knows doctor → pass dcode
        // Flow 2: Patient doesn't know → pass duty_dcode (assigned at reception)
        // ─────────────────────────────────────────
        public async Task<string> DirectWalkinRegistration(
     DirectWalkinRequest req,
     string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Determine doctor
                int assignedDcode = req.dcode.HasValue && req.dcode > 0
                    ? req.dcode.Value
                    : (req.duty_dcode.HasValue && req.duty_dcode > 0
                        ? req.duty_dcode.Value
                        : 0);

                if (assignedDcode == 0)
                    return "Either dcode or duty_dcode is required";

                // Validate slot
                var slot = await db.QueryFirstOrDefaultAsync<DoctorAppointmentSlotDetailsModel>(
                @"SELECT *
          FROM doctor_appointment_slot_details
          WHERE slot_detail_id = @slot_detail_id
            AND tenant_code = @tenant_code
            AND isdeleted = false
            AND is_active = true",
                new
                {
                    req.slot_detail_id,
                    tenant_code
                });

                if (slot == null)
                    return "Slot not found";

                // Validate doctor belongs to slot
                if (slot.dcode != assignedDcode)
                    return "Selected slot does not belong to selected doctor";

                // Slot status check
                if (slot.slot_status == "FULL")
                    return "Slot is full";

                if (slot.slot_status == "CANCELLED")
                    return "Slot is cancelled";

                if (slot.slot_status == "CLOSED")
                    return "Slot is closed";

                // Walk-in quota validation
                if (slot.walkin_count >= slot.max_walkin)
                    return "Walk-in quota full for this slot";

                // Total capacity validation
                if (slot.booked_count >= slot.max_patients)
                    return "Slot capacity reached";

                // Generate token
                int token = slot.booked_count + 1;

                var data = new OpRegistrationModel
                {
                    op_id = Guid.NewGuid(),
                    op_no = await GenerateOpNo(db, tenant_code),

                    custid = req.custid,
                    dcode = assignedDcode,
                    department_code = req.department_code,

                    slot_detail_id = slot.slot_detail_id,

                    visit_type = string.IsNullOrWhiteSpace(req.visit_type)
                        ? "NEWVISIT"
                        : req.visit_type.ToUpper(),

                    reg_type = "WALKIN",

                    visit_date = slot.appointment_date,

                    token_no = token,
                    queue_no = token,

                    visit_status = "WAITING",

                    notes = req.notes,

                    is_direct_walkin = true,
                    duty_dcode = req.duty_dcode,

                    tenant_code = tenant_code,
                    isdeleted = false,

                    created_at = DateTime.SpecifyKind(
                        DateTime.UtcNow,
                        DateTimeKind.Utc),

                    updated_at = DateTime.SpecifyKind(
                        DateTime.UtcNow,
                        DateTimeKind.Utc)
                };

                // Insert OP Registration
                await db.ExecuteAsync(@"
        INSERT INTO op_registration
        (
            op_id,
            op_no,
            custid,
            dcode,
            department_code,
            slot_detail_id,
            visit_type,
            reg_type,
            visit_date,
            token_no,
            queue_no,
            visit_status,
            notes,
            is_direct_walkin,
            duty_dcode,
            tenant_code,
            isdeleted,
            created_at,
            updated_at
        )
        VALUES
        (
            @op_id,
            @op_no,
            @custid,
            @dcode,
            @department_code,
            @slot_detail_id,
            @visit_type,
            @reg_type,
            @visit_date,
            @token_no,
            @queue_no,
            @visit_status,
            @notes,
            @is_direct_walkin,
            @duty_dcode,
            @tenant_code,
            @isdeleted,
            @created_at,
            @updated_at
        )",
                new
                {
                    data.op_id,
                    data.op_no,
                    data.custid,
                    data.dcode,
                    data.department_code,
                    data.slot_detail_id,
                    data.visit_type,
                    data.reg_type,

                    visit_date = data.visit_date.ToDateTime(TimeOnly.MinValue),

                    data.token_no,
                    data.queue_no,
                    data.visit_status,
                    data.notes,
                    data.is_direct_walkin,
                    data.duty_dcode,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });
                // ── Auto-add consultation fee to unbilledcharges ─────
                var (feeTcode, feeRate, feeAmount) = await GetDoctorConsultationFee(db, assignedDcode, tenant_code);

                await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                {
                    op_id = data.op_id.ToString(),
                    custid = data.custid,
                    tcode = feeTcode,
                    rate = feeRate,
                    amount = feeAmount,
                    quantity = 1
                }, tenant_code);


                // Update slot counters
                await db.ExecuteAsync(@"
        UPDATE doctor_appointment_slot_details
        SET
            booked_count = booked_count + 1,
            walkin_count = walkin_count + 1,
            updated_at = now()
        WHERE slot_detail_id = @slot_detail_id
          AND tenant_code = @tenant_code",
                new
                {
                    slot_detail_id = slot.slot_detail_id,
                    tenant_code
                });

                // Mark slot FULL if reached capacity
                await db.ExecuteAsync(@"
        UPDATE doctor_appointment_slot_details
        SET slot_status = 'FULL'
        WHERE slot_detail_id = @slot_detail_id
          AND booked_count >= max_patients
          AND tenant_code = @tenant_code",
                new
                {
                    slot_detail_id = slot.slot_detail_id,
                    tenant_code
                });

                return $"Success|OpNo:{data.op_no}|OpId:{data.op_id}|Token:{token}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // TRANSFER TO ANOTHER DOCTOR
        // Called after duty doctor sees patient and decides to refer to specialist.
        // Old OP → TRANSFERRED, new OP created for specialist with WAITING status.
        // ─────────────────────────────────────────
        public async Task<string> TransferDoctor(
    TransferDoctorRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var op = await db.QueryFirstOrDefaultAsync<OpRegistrationModel>(
                    @"SELECT * FROM op_registration
              WHERE op_id = @op_id
              AND tenant_code = @tenant_code
              AND isdeleted = false",
                    new { req.op_id, tenant_code });

                if (op == null)
                    return "OP Registration not found";

                if (op.visit_status == "COMPLETED")
                    return "Cannot transfer a completed visit";

                if (op.visit_status == "CANCELLED")
                    return "Cannot transfer a cancelled visit";

                if (op.visit_status == "TRANSFERRED")
                    return "Already transferred";

                DoctorAppointmentSlotDetailsModel? slot = null;

                if (req.slot_detail_id.HasValue)
                {
                    slot = await db.QueryFirstOrDefaultAsync<DoctorAppointmentSlotDetailsModel>(
                        @"SELECT *
                  FROM doctor_appointment_slot_details
                  WHERE slot_detail_id = @slot_detail_id
                  AND tenant_code = @tenant_code
                  AND isdeleted = false
                  AND is_active = true",
                        new
                        {
                            slot_detail_id = req.slot_detail_id,
                            tenant_code
                        });

                    if (slot == null)
                        return "Slot not found";

                    if (slot.dcode != req.transfer_to_dcode)
                        return "Selected slot does not belong to selected doctor";

                    if (slot.slot_status == "FULL")
                        return "Slot is full";

                    if (slot.slot_status == "CANCELLED")
                        return "Slot is cancelled";

                    if (slot.slot_status == "CLOSED")
                        return "Slot is closed";

                    if (slot.booked_count >= slot.max_patients)
                        return "Slot capacity reached";
                }

                // Generate token
                int newToken;

                if (slot != null)
                {
                    newToken = slot.booked_count + 1;
                }
                else
                {
                    newToken = await db.ExecuteScalarAsync<int>(
                        @"SELECT COALESCE(MAX(token_no),0) + 1
                  FROM op_registration
                  WHERE dcode = @dcode
                  AND tenant_code = @tenant_code
                  AND isdeleted = false
                  AND visit_date = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date",
                        new
                        {
                            dcode = req.transfer_to_dcode,
                            tenant_code
                        });
                }

                // Mark old OP as transferred
                await db.ExecuteAsync(
                    @"UPDATE op_registration
                    SET visit_status = 'TRANSFERRED',
                  transferred_to_dcode = @transfer_to_dcode,
                  transfer_reason = @transfer_reason,
                  updated_at = now()
                  WHERE op_id = @op_id
                  AND tenant_code = @tenant_code",
                    new
                    {
                        req.op_id,
                        req.transfer_to_dcode,
                        req.transfer_reason,
                        tenant_code
                    });

                var newOp = new OpRegistrationModel
                {
                    op_id = Guid.NewGuid(),
                    op_no = await GenerateOpNo(db, tenant_code),

                    custid = op.custid,
                    dcode = req.transfer_to_dcode,
                    department_code = op.department_code,

                    slot_detail_id = req.slot_detail_id,

                    visit_type = "FOLLOWUP",
                    reg_type = op.reg_type,

                    visit_date = slot != null
                        ? slot.appointment_date
                        : DateOnly.FromDateTime(
                            TimeZoneInfo.ConvertTimeFromUtc(
                                DateTime.UtcNow,
                                TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"))),

                    token_no = newToken,
                    queue_no = newToken,

                    visit_status = "WAITING",

                    notes = $"Transferred from OP# {op.op_no}. Reason: {req.transfer_reason}",

                    is_direct_walkin = op.is_direct_walkin,
                    duty_dcode = op.duty_dcode,

                    tenant_code = tenant_code,
                    isdeleted = false,

                    created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                await db.ExecuteAsync(@"
            INSERT INTO op_registration
            (
                op_id,
                op_no,
                custid,
                dcode,
                department_code,
                slot_detail_id,
                visit_type,
                reg_type,
                visit_date,
                token_no,
                queue_no,
                visit_status,
                notes,
                is_direct_walkin,
                duty_dcode,
                tenant_code,
                isdeleted,
                created_at,
                updated_at
            )
            VALUES
            (
                @op_id,
                @op_no,
                @custid,
                @dcode,
                @department_code,
                @slot_detail_id,
                @visit_type,
                @reg_type,
                @visit_date,
                @token_no,
                @queue_no,
                @visit_status,
                @notes,
                @is_direct_walkin,
                @duty_dcode,
                @tenant_code,
                @isdeleted,
                @created_at,
                @updated_at
            )",
                    new
                    {
                        newOp.op_id,
                        newOp.op_no,
                        newOp.custid,
                        newOp.dcode,
                        newOp.department_code,
                        newOp.slot_detail_id,
                        newOp.visit_type,
                        newOp.reg_type,
                        visit_date = newOp.visit_date.ToDateTime(TimeOnly.MinValue),
                        newOp.token_no,
                        newOp.queue_no,
                        newOp.visit_status,
                        newOp.notes,
                        newOp.is_direct_walkin,
                        newOp.duty_dcode,
                        newOp.tenant_code,
                        newOp.isdeleted,
                        newOp.created_at,
                        newOp.updated_at
                    });

                // Update slot counters
                if (slot != null)
                {
                    await db.ExecuteAsync(
                        @"UPDATE doctor_appointment_slot_details
                  SET booked_count = booked_count + 1,
                      updated_at = now()
                  WHERE slot_detail_id = @slot_detail_id
                  AND tenant_code = @tenant_code",
                        new
                        {
                            slot_detail_id = slot.slot_detail_id,
                            tenant_code
                        });

                    await db.ExecuteAsync(
                        @"UPDATE doctor_appointment_slot_details
                  SET slot_status = 'FULL'
                  WHERE slot_detail_id = @slot_detail_id
                  AND booked_count >= max_patients
                  AND tenant_code = @tenant_code",
                        new
                        {
                            slot_detail_id = slot.slot_detail_id,
                            tenant_code
                        });
                }

                return $"Success|NewOpNo:{newOp.op_no}|NewOpId:{newOp.op_id}|Token:{newToken}|TransferredTo:{req.transfer_to_dcode}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<List<DoctorBookingListModel>> GetDoctorBookings(
    int dcode,
    DateOnly appointment_date,
    string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
        SELECT
    b.booking_id,
    b.booking_no,
    b.custid,
    b.dcode,
    b.appointment_date,
    b.token_no,
    b.booking_status
FROM appointment_booking b
WHERE b.dcode = @dcode
AND b.appointment_date = @appointment_date
AND b.tenant_code = @tenant_code
          AND b.isdeleted = false
        ORDER BY b.token_no";

            var result = await db.QueryAsync<DoctorBookingListModel>(
                sql,
                new
                {
                    dcode,
                    appointment_date = appointment_date.ToDateTime(TimeOnly.MinValue),
                    tenant_code
                });

            return result.ToList();
        }
        // ─────────────────────────────────────────
        // GET ALL OP REGISTRATIONS WITH CUSTOMER DETAILS
        // Anchored on op_registration so every OP always shows,
        // even if the matching customer record can't be found.
        // ─────────────────────────────────────────
        public async Task<List<dynamic>> GetAllCustomersWithOp(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
        SELECT 
            op.op_id,
            op.op_no,
            op.booking_id,
            op.booking_no,
            op.slot_detail_id,
            op.custid,
            op.dcode              AS op_dcode,
            op.department_code,
            op.visit_type,
            op.reg_type,
            op.visit_date,
            op.token_no,
            op.queue_no,
            op.visit_status,
            op.notes,
            op.is_direct_walkin,
            op.duty_dcode,
            op.transferred_to_dcode,
            op.transfer_reason,
            op.created_at         AS op_created_at,
            op.updated_at         AS op_updated_at,
            c.custcode,
            c.name,
            c.mobile,
            c.email,
            c.gender,
            c.dateofbirth,
            c.ageyears,
            c.agemonths,
            c.agedays,
            c.city,
            c.area,
            c.street,
            c.customerimage,
            c.tenant_code         AS cust_tenant_code
        FROM op_registration op
        LEFT JOIN customerdb.customer_master c
               ON c.custid::numeric      = op.custid::numeric
              AND TRIM(c.tenant_code)    = TRIM(op.tenant_code)
              AND c.deleted              = false
        WHERE op.isdeleted          = false
        AND   TRIM(op.tenant_code)  = TRIM(@tenant_code)
        ORDER BY op.visit_date DESC, op.created_at DESC";

            var res = await db.QueryAsync<dynamic>(sql, new { tenant_code });
            return res.ToList();
        }
        private async Task<(int? tcode, double rate, double amount)> GetDoctorConsultationFee(
     IDbConnection db, int dcode, string tenant_code)
        {
            var doctor = await db.QueryFirstOrDefaultAsync<DoctorMasterModel>(
                @"SELECT tcode, opcharge FROM doctor_master
          WHERE dcode = @dcode AND tenant_code = @tenant_code AND deleted = false",
                new { dcode, tenant_code });

            double fee = doctor?.opcharge ?? 0;
            int? tcode = doctor?.tcode;   // already int? — no cast needed

            return (tcode, fee, fee);
        }
    }
}
