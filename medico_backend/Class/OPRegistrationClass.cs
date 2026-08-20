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
                db.Open();

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

                // ✅ Carry reg_type from booking_type (WALKIN/ONLINE)
                data.reg_type = ((string)booking.booking_type).ToUpper();

                // ✅ Carry booking_no from booking
                data.booking_no = (string)booking.booking_no;

                // ✅ Carry slot from booking if not provided
                if (data.slot_detail_id == null || data.slot_detail_id == Guid.Empty)
                    data.slot_detail_id = (Guid?)booking.slot_detail_id;

                // ── 4. Token + insert, all inside ONE locked transaction ──
                using var tx = db.BeginTransaction();
                try
                {
                    var (tokenNo, seq) = await GenerateNextTokenNo(
        db, tx, data.dcode, data.slot_detail_id, data.reg_type, data.tenant_code!);
                    data.token_no = tokenNo;
                    data.queue_no = seq;

                    data.op_id = Guid.NewGuid();
                    data.op_no = await GenerateOpNo(db, data.tenant_code!);   // see note below
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
                    }, tx);

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }

                // ── 5. Auto-add consultation fee — stays OUTSIDE the transaction,
                // a delay here doesn't cause duplicate tokens, no need to hold the lock ──
                var (feeTcode, feeRate, feeAmount) = await GetDoctorConsultationFee(db, data.dcode, data.tenant_code!, data.custid);

                Console.WriteLine($"[UNBILL-DEBUG][CreateOpRegistration] op_id={data.op_id} custid={data.custid} tcode={feeTcode} rate={feeRate} amount={feeAmount}");

                await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                {
                    op_id = data.op_id.ToString(),
                    custid = data.custid,
                    tcode = feeTcode,
                    rate = feeRate,
                    amount = feeAmount,
                    quantity = 1
                }, data.tenant_code!);

                Console.WriteLine($"[UNBILL-DEBUG][CreateOpRegistration] AddConsultationCharge call completed for op_id={data.op_id}");

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

                bool hasOp = data.op_id.HasValue && data.op_id != Guid.Empty;
                bool hasIp = data.ip_id.HasValue && data.ip_id != Guid.Empty;

                if (!hasOp && !hasIp)
                    return "Either op_id or ip_id is required";

                if (hasOp)
                {
                    var op = await db.QueryFirstOrDefaultAsync(
                        @"SELECT op_id FROM op_registration
                  WHERE  op_id       = @op_id
                  AND    tenant_code = @tenant_code
                  AND    isdeleted   = false",
                        new { data.op_id, data.tenant_code });

                    if (op == null) return "OP Registration not found";
                }

                if (hasIp)
                {
                    var ip = await db.QueryFirstOrDefaultAsync(
                        @"SELECT ip_id FROM ip_registration
                  WHERE  ip_id       = @ip_id
                  AND    tenant_code = @tenant_code
                  AND    isdeleted   = false",
                        new { data.ip_id, data.tenant_code });

                    if (ip == null) return "IP Registration not found";
                }

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
        (vital_id, op_id, op_no, ip_id, ip_no, custid, dcode,
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
        (@vital_id, @op_id, @op_no, @ip_id, @ip_no, @custid, @dcode,
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
        // Optimized + Paid Status + TIMING INSTRUMENTATION
        // ─────────────────────────────────────────
        public async Task<List<OpRegistrationListModel>> GetAllOpList(
            string tenant_code,
            int? dcode = null,
            DateOnly? from_date = null,
            DateOnly? to_date = null,
            string? visit_status = null)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();
            //Console.WriteLine($"[TIMING][GetAllOpList] db.Open(): {sw.ElapsedMilliseconds}ms");
            sw.Restart();

            string sql = @"
SELECT
    o.op_id, o.op_no, o.booking_id, o.booking_no, o.slot_detail_id,
    o.custid, o.dcode, o.department_code, o.visit_type, o.reg_type,
    o.visit_date, o.token_no, o.queue_no, o.visit_status, o.notes,
    o.is_direct_walkin, o.duty_dcode, o.transferred_to_dcode, o.transfer_reason,
    o.is_dressing, o.tenant_code, o.isdeleted, o.created_at, o.updated_at,
    cl.name AS patient_name,
    cl.mobile,
    cl.isvip,
    cs.refer_to_ip,
    uc.billedstatus AS unbilled_status,
    CASE
        WHEN uc.billedstatus = true
         AND lrm.requestguid IS NOT NULL
         AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
        THEN true ELSE false
    END AS paid_status,
    dm.name AS doctor_name,
    sd.slot_start_time,
    sd.slot_end_time
FROM op_registration o
LEFT JOIN customerdb.customer_master cl
       ON cl.custid = o.custid AND cl.deleted = false
LEFT JOIN doctor_master dm
       ON dm.dcode = o.dcode AND dm.tenant_code = o.tenant_code AND dm.deleted = false
LEFT JOIN doctor_appointment_slot_details sd
       ON sd.slot_detail_id = o.slot_detail_id AND sd.tenant_code = o.tenant_code
LEFT JOIN LATERAL (
    SELECT refer_to_ip
    FROM op_case_sheet
    WHERE op_case_sheet.op_id = o.op_id
    AND op_case_sheet.tenant_code = o.tenant_code
    AND op_case_sheet.isdeleted = false
    ORDER BY created_at DESC LIMIT 1
) cs ON true
LEFT JOIN LATERAL (
    SELECT billedstatus, billid
    FROM unbilledcharges
    WHERE unbilledcharges.opvisitid = o.op_id::text
    AND unbilledcharges.entrytype = 'CONSULTATION'
    AND unbilledcharges.tenant_code = o.tenant_code
    ORDER BY chargedate DESC LIMIT 1
) uc ON true
LEFT JOIN lab_request_master lrm
       ON lrm.requestguid = uc.billid
      AND lrm.tenant_code = o.tenant_code
      AND COALESCE(lrm.isdeleted, false) = false
      AND COALESCE(lrm.deleted, false) = false
WHERE o.isdeleted = false
AND o.tenant_code = @tenant_code
AND (@dcode IS NULL OR o.dcode = @dcode)
AND (@from_date IS NULL OR o.visit_date >= @from_date)
AND (@to_date IS NULL OR o.visit_date < @to_date + INTERVAL '1 day')
AND (@visit_status IS NULL OR o.visit_status = @visit_status)
ORDER BY o.visit_date DESC, o.queue_no, o.token_no";

            var res = await db.QueryAsync<OpRegistrationListModel>(
                sql,
                new
                {
                    tenant_code,
                    dcode,
                    from_date = from_date.HasValue ? from_date.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    to_date = to_date.HasValue ? to_date.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                    visit_status = string.IsNullOrWhiteSpace(visit_status) ? null : visit_status.ToUpper()
                },
                commandTimeout: 60
            );

            //Console.WriteLine($"[TIMING][GetAllOpList] db.QueryAsync (execute + read all rows): {sw.ElapsedMilliseconds}ms");
            sw.Restart();

            var list = res.ToList();
            //Console.WriteLine($"[TIMING][GetAllOpList] .ToList() materialize: {sw.ElapsedMilliseconds}ms");

            //Console.WriteLine($"[TIMING][GetAllOpList] TOTAL: {totalSw.ElapsedMilliseconds}ms, rows={list.Count}");

            return list;
        }


        // Used only by GetAllOpList — same shape as OpRegistrationModel
        // plus the joined patient display fields.
        public class OpRegistrationListModel : OpRegistrationModel
        {
            public string? patient_name { get; set; }
            public string? mobile { get; set; }

            public bool? isvip { get; set; }

            // ✅ NEW — from op_case_sheet (most recent sheet for this op_id)
            public bool? refer_to_ip { get; set; }

            // ✅ NEW — from unbilledcharges, CONSULTATION entry for this op_id
            // true = billed, false = unbilled, null = no consultation charge row found
            public bool? unbilled_status { get; set; }
            public bool? paid_status { get; set; }

            // ✅ NEW — from doctor_master
            public string? doctor_name { get; set; }

            // ✅ NEW — from doctor_appointment_slot_details (via o.slot_detail_id)
            public TimeOnly? slot_start_time { get; set; }
            public TimeOnly? slot_end_time { get; set; }
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
     string tenant_code, Guid? op_id = null, Guid? ip_id = null, decimal? custid = null)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM patient_vitals
           WHERE isdeleted   = false
           AND   tenant_code = @tenant_code
           AND   (@op_id  IS NULL OR op_id  = @op_id)
           AND   (@ip_id  IS NULL OR ip_id  = @ip_id)
           AND   (@custid IS NULL OR custid = @custid)
           ORDER BY created_at DESC";

            var res = await db.QueryAsync<PatientVitalsModel>(sql, new { tenant_code, op_id, ip_id, custid });
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
        public async Task<string> DirectWalkinRegistration(
    DirectWalkinRequest req,
    string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                db.Open();

                int assignedDcode = req.dcode.HasValue && req.dcode > 0
                    ? req.dcode.Value
                    : (req.duty_dcode.HasValue && req.duty_dcode > 0
                        ? req.duty_dcode.Value
                        : 0);

                if (assignedDcode == 0)
                    return "Either dcode or duty_dcode is required";

                // ── NEW: resolve service, same lookup ServiceRegistration uses ──
                ServiceTypeModel? svc = null;
                bool isDressing = false;
                if (req.service_id.HasValue && req.service_id > 0)
                {
                    svc = await db.QueryFirstOrDefaultAsync<ServiceTypeModel>(
                        @"SELECT * FROM service_type_master
                  WHERE service_id = @service_id AND tenant_code = @tenant_code AND deleted = false",
                        new { req.service_id, tenant_code });

                    if (svc == null)
                        return $"Service id {req.service_id} not configured for this tenant";

                    isDressing = string.Equals(svc.service_name?.Trim(), "Dressing", StringComparison.OrdinalIgnoreCase);
                }

                string visitType = svc != null
                    ? svc.service_name
                    : (string.IsNullOrWhiteSpace(req.visit_type) ? "NEWVISIT" : req.visit_type.ToUpper());

                bool slotRequired = await db.ExecuteScalarAsync<bool?>(
                    @"SELECT is_slot_required FROM lab_settings
              WHERE tenant_code = @tenant_code AND deleted = false
              ORDER BY (bh_code IS NULL) LIMIT 1",
                    new { tenant_code }) ?? true;

                if (!slotRequired)
                {
                    var noSlotData = new OpRegistrationModel
                    {
                        op_id = Guid.NewGuid(),
                        op_no = await GenerateOpNo(db, tenant_code),
                        custid = req.custid,
                        dcode = assignedDcode,
                        department_code = req.department_code,
                        slot_detail_id = null,
                        visit_type = visitType,
                        reg_type = "WALKIN",
                        visit_date = DateOnly.FromDateTime(
                            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"))),
                        visit_status = "WAITING",
                        notes = req.notes,
                        is_direct_walkin = true,
                        duty_dcode = req.duty_dcode,
                        is_dressing = isDressing,          // NEW
                        service_id = req.service_id,       // NEW
                        tenant_code = tenant_code,
                        isdeleted = false,
                        created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                    };

                    using (var tx = db.BeginTransaction())
                    {
                        try
                        {
                            var (noSlotToken, noSlotSeq) = await GenerateNextTokenNo(
                                db, tx, assignedDcode, null, "WALKIN", tenant_code, service_id: req.service_id);
                            noSlotData.token_no = noSlotToken;
                            noSlotData.queue_no = noSlotSeq;

                            await db.ExecuteAsync(@"
                        INSERT INTO op_registration
                        (op_id, op_no, custid, dcode, department_code, slot_detail_id, visit_type,
                         reg_type, visit_date, token_no, queue_no, visit_status, notes,
                         is_direct_walkin, duty_dcode, is_dressing, service_id,
                         tenant_code, isdeleted, created_at, updated_at)
                        VALUES
                        (@op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id, @visit_type,
                         @reg_type, @visit_date, @token_no, @queue_no, @visit_status, @notes,
                         @is_direct_walkin, @duty_dcode, @is_dressing, @service_id,
                         @tenant_code, @isdeleted, @created_at, @updated_at)",
                                new
                                {
                                    noSlotData.op_id,
                                    noSlotData.op_no,
                                    noSlotData.custid,
                                    noSlotData.dcode,
                                    noSlotData.department_code,
                                    noSlotData.slot_detail_id,
                                    noSlotData.visit_type,
                                    noSlotData.reg_type,
                                    visit_date = noSlotData.visit_date.ToDateTime(TimeOnly.MinValue),
                                    noSlotData.token_no,
                                    noSlotData.queue_no,
                                    noSlotData.visit_status,
                                    noSlotData.notes,
                                    noSlotData.is_direct_walkin,
                                    noSlotData.duty_dcode,
                                    noSlotData.is_dressing,
                                    noSlotData.service_id,
                                    noSlotData.tenant_code,
                                    noSlotData.isdeleted,
                                    noSlotData.created_at,
                                    noSlotData.updated_at
                                }, tx);

                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }

                    int? noSlotTcode; double noSlotRate; double noSlotAmount;
                    if (svc != null)
                        (noSlotTcode, noSlotRate, noSlotAmount) = await GetServiceCharge(db, svc.service_name, tenant_code);
                    else
                        (noSlotTcode, noSlotRate, noSlotAmount) = await GetDoctorConsultationFee(db, assignedDcode, tenant_code, req.custid);

                    await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                    {
                        op_id = noSlotData.op_id.ToString(),
                        custid = noSlotData.custid,
                        tcode = noSlotTcode,
                        rate = noSlotRate,
                        amount = noSlotAmount,
                        quantity = 1
                    }, tenant_code);

                    return $"Success|OpNo:{noSlotData.op_no}|OpId:{noSlotData.op_id}|Token:{noSlotData.token_no}" +
                           (svc != null ? $"|ServiceId:{req.service_id}" : "");
                }

                // slotRequired == true → validate slot
                var slot = await db.QueryFirstOrDefaultAsync<DoctorAppointmentSlotDetailsModel>(
                    @"SELECT *
              FROM doctor_appointment_slot_details
              WHERE slot_detail_id = @slot_detail_id
                AND tenant_code = @tenant_code
                AND isdeleted = false
                AND is_active = true",
                    new { req.slot_detail_id, tenant_code });

                if (slot == null)
                    return "Slot not found";

                if (slot.dcode != assignedDcode)
                    return "Selected slot does not belong to selected doctor";

                if (slot.slot_status == "FULL")
                    return "Slot is full";

                if (slot.slot_status == "CANCELLED")
                    return "Slot is cancelled";

                if (slot.slot_status == "CLOSED")
                    return "Slot is closed";

                if (slot.walkin_count >= slot.max_walkin)
                    return "Walk-in quota full for this slot";

                if (slot.booked_count >= slot.max_patients)
                    return "Slot capacity reached";

                var data = new OpRegistrationModel
                {
                    op_id = Guid.NewGuid(),
                    op_no = await GenerateOpNo(db, tenant_code),
                    custid = req.custid,
                    dcode = assignedDcode,
                    department_code = req.department_code,
                    slot_detail_id = slot.slot_detail_id,
                    visit_type = visitType,
                    reg_type = "WALKIN",
                    visit_date = slot.appointment_date,
                    visit_status = "WAITING",
                    notes = req.notes,
                    is_direct_walkin = true,
                    duty_dcode = req.duty_dcode,
                    is_dressing = isDressing,          // NEW
                    service_id = req.service_id,       // NEW
                    tenant_code = tenant_code,
                    isdeleted = false,
                    created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                string token;

                using (var tx = db.BeginTransaction())
                {
                    try
                    {
                        var (tokenStr, seq) = await GenerateNextTokenNo(
                            db, tx, assignedDcode, slot.slot_detail_id, "WALKIN", tenant_code, service_id: req.service_id);
                        token = tokenStr;
                        data.token_no = tokenStr;
                        data.queue_no = seq;

                        await db.ExecuteAsync(@"
                    INSERT INTO op_registration
                    (
                        op_id, op_no, custid, dcode, department_code, slot_detail_id,
                        visit_type, reg_type, visit_date, token_no, queue_no, visit_status,
                        notes, is_direct_walkin, duty_dcode, is_dressing, service_id,
                        tenant_code, isdeleted, created_at, updated_at
                    )
                    VALUES
                    (
                        @op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id,
                        @visit_type, @reg_type, @visit_date, @token_no, @queue_no, @visit_status,
                        @notes, @is_direct_walkin, @duty_dcode, @is_dressing, @service_id,
                        @tenant_code, @isdeleted, @created_at, @updated_at
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
                                data.is_dressing,
                                data.service_id,
                                data.tenant_code,
                                data.isdeleted,
                                data.created_at,
                                data.updated_at
                            }, tx);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                int? feeTcode2; double feeRate2; double feeAmount2;
                if (svc != null)
                    (feeTcode2, feeRate2, feeAmount2) = await GetServiceCharge(db, svc.service_name, tenant_code);
                else
                    (feeTcode2, feeRate2, feeAmount2) = await GetDoctorConsultationFee(db, assignedDcode, tenant_code, req.custid);

                await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                {
                    op_id = data.op_id.ToString(),
                    custid = data.custid,
                    tcode = feeTcode2,
                    rate = feeRate2,
                    amount = feeAmount2,
                    quantity = 1
                }, tenant_code);

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET
                booked_count = booked_count + 1,
                walkin_count = walkin_count + 1,
                updated_at = now()
            WHERE slot_detail_id = @slot_detail_id
              AND tenant_code = @tenant_code",
                    new { slot_detail_id = slot.slot_detail_id, tenant_code });

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET slot_status = 'FULL'
            WHERE slot_detail_id = @slot_detail_id
              AND booked_count >= max_patients
              AND tenant_code = @tenant_code",
                    new { slot_detail_id = slot.slot_detail_id, tenant_code });

                return $"Success|OpNo:{data.op_no}|OpId:{data.op_id}|Token:{token}" +
                       (svc != null ? $"|ServiceId:{req.service_id}" : "");
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> TransferDoctor(
    TransferDoctorRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                db.Open();

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
                        new { slot_detail_id = req.slot_detail_id, tenant_code });

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

                string newToken;
                OpRegistrationModel newOp;

                using (var tx = db.BeginTransaction())
                {
                    try
                    {
                        var (tokenStr, seq) = await GenerateNextTokenNo(
            db, tx, req.transfer_to_dcode, slot?.slot_detail_id, op.reg_type, tenant_code);
                        newToken = tokenStr;

                        // Mark old OP as transferred (same tx)
                        await db.ExecuteAsync(
                            @"UPDATE op_registration
                      SET visit_status = 'TRANSFERRED',
                          transferred_to_dcode = @transfer_to_dcode,
                          transfer_reason = @transfer_reason,
                          updated_at = now()
                      WHERE op_id = @op_id
                      AND tenant_code = @tenant_code",
                            new { req.op_id, req.transfer_to_dcode, req.transfer_reason, tenant_code }, tx);

                        newOp = new OpRegistrationModel
                        {
                            op_id = Guid.NewGuid(),
                            op_no = await GenerateOpNo(db, tenant_code),
                            custid = op.custid,
                            dcode = req.transfer_to_dcode,
                            department_code = op.department_code,
                            slot_detail_id = req.slot_detail_id,
                            visit_type = "FOLLOWUP",
                            reg_type = op.reg_type,
                            visit_date = req.visit_date,
                            token_no = newToken,
                            queue_no = seq,  // ← newToken is now string, queue_no is int  // use the int from the tuple, not newToken
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
                        op_id, op_no, custid, dcode, department_code, slot_detail_id,
                        visit_type, reg_type, visit_date, token_no, queue_no, visit_status,
                        notes, is_direct_walkin, duty_dcode, tenant_code, isdeleted, created_at, updated_at
                    )
                    VALUES
                    (
                        @op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id,
                        @visit_type, @reg_type, @visit_date, @token_no, @queue_no, @visit_status,
                        @notes, @is_direct_walkin, @duty_dcode, @tenant_code, @isdeleted, @created_at, @updated_at
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
                                newOp.visit_date,
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
                            }, tx);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                // Update slot counters (outside tx — fine, doesn't affect token correctness)
                if (slot != null)
                {
                    await db.ExecuteAsync(
                        @"UPDATE doctor_appointment_slot_details
                  SET booked_count = booked_count + 1,
                      updated_at = now()
                  WHERE slot_detail_id = @slot_detail_id
                  AND tenant_code = @tenant_code",
                        new { slot_detail_id = slot.slot_detail_id, tenant_code });

                    await db.ExecuteAsync(
                        @"UPDATE doctor_appointment_slot_details
                  SET slot_status = 'FULL'
                  WHERE slot_detail_id = @slot_detail_id
                  AND booked_count >= max_patients
                  AND tenant_code = @tenant_code",
                        new { slot_detail_id = slot.slot_detail_id, tenant_code });
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
    IDbConnection db, int dcode, string tenant_code, decimal? custid = null)
        {
            var doctor = await db.QueryFirstOrDefaultAsync<DoctorMasterModel>(
                @"SELECT tcode, opcharge, override_flat_opcharge FROM doctor_master
          WHERE dcode = @dcode
          AND   TRIM(tenant_code) = TRIM(@tenant_code)
          AND   deleted = false",
                new { dcode, tenant_code });

            int? tcode = await db.ExecuteScalarAsync<int?>(
                @"SELECT tcode FROM test_master
          WHERE TRIM(tenant_code) = TRIM(@tenant_code)
          AND   deleted     = false
          AND   name ILIKE 'Consultation%'
          ORDER BY tcode
          LIMIT 1",
                new { tenant_code });

            Console.WriteLine($"[FEE-DEBUG] dcode={dcode} tenant={tenant_code} custid={custid} doctorFound={doctor != null}");

            if (doctor == null)
            {
                Console.WriteLine($"[FEE-DEBUG] Doctor not found — returning 0");
                return (tcode, 0, 0);
            }

            double flatFee = doctor.opcharge ?? 0;
            Console.WriteLine($"[FEE-DEBUG] flatFee={flatFee} override={doctor.override_flat_opcharge}");

            if (doctor.override_flat_opcharge == true)
            {
                Console.WriteLine($"[FEE-DEBUG] override_flat_opcharge=true — returning flatFee={flatFee}");
                return (tcode, flatFee, flatFee);
            }

            bool ageWiseSplit = await db.ExecuteScalarAsync<bool?>(
                @"SELECT bool_or(COALESCE(op_age_wise_split, false)) FROM lab_settings
          WHERE TRIM(tenant_code) = TRIM(@tenant_code) AND deleted = false",
                new { tenant_code }) ?? false;

            Console.WriteLine($"[FEE-DEBUG] ageWiseSplit={ageWiseSplit}");

            if (!ageWiseSplit)
            {
                Console.WriteLine($"[FEE-DEBUG] Split OFF — returning flatFee={flatFee}");
                return (tcode, flatFee, flatFee);
            }

            if (custid.HasValue)
            {
                // ✅ FIX: customerdb.customer_master is central/shared — a patient can be
                // registered under a parent tenant_code or a different branch than the
                // one on today's op_registration. A hard "AND tenant_code = @tenant_code"
                // silently excludes the row → cust becomes null → age null → fee falls
                // through to 0. Look up by custid first (globally unique), and only use
                // tenant_code to prefer an exact-tenant match when duplicates exist.
                var custMatches = (await db.QueryAsync(
                    @"SELECT ageyears, dateofbirth, tenant_code
              FROM   customerdb.customer_master
              WHERE  custid::numeric = @custid::numeric
              AND    deleted = false",
                    new { custid })).ToList();

                var cust = custMatches.FirstOrDefault(c =>
                               string.Equals(((string)c.tenant_code)?.Trim(), tenant_code?.Trim(),
                                              StringComparison.OrdinalIgnoreCase))
                           ?? custMatches.FirstOrDefault();   // fallback: any tenant, if no exact match

                Console.WriteLine($"[FEE-DEBUG] custFound={cust != null} matchCount={custMatches.Count} ageyears={cust?.ageyears} dob={cust?.dateofbirth}");

                int? age = null;
                if (cust != null)
                {
                    if (cust.ageyears != null)
                    {
                        age = (int)cust.ageyears;
                    }
                    else if (cust.dateofbirth != null)
                    {
                        DateTime dob = (DateTime)cust.dateofbirth;
                        int computed = DateTime.UtcNow.Year - dob.Year;
                        if (dob.Date > DateTime.UtcNow.AddYears(-computed)) computed--;
                        age = computed;
                    }
                }

                Console.WriteLine($"[FEE-DEBUG] resolvedAge={age}");

                if (age.HasValue)
                {
                    double? slabFee = await db.ExecuteScalarAsync<double?>(
                        @"SELECT opcharge FROM doctor_op_charge_slab
                  WHERE TRIM(tenant_code) = TRIM(@tenant_code)
                  AND   dcode   = @dcode
                  AND   deleted = false
                  AND   @age BETWEEN min_age AND max_age
                  ORDER BY min_age LIMIT 1",
                        new { tenant_code, dcode, age = age.Value });

                    Console.WriteLine($"[FEE-DEBUG] slabFee={slabFee} (dcode={dcode}, age={age.Value})");

                    if (slabFee.HasValue)
                        return (tcode, slabFee.Value, slabFee.Value);
                }
            }
            else
            {
                Console.WriteLine($"[FEE-DEBUG] custid is null — cannot resolve age");
            }

            Console.WriteLine($"[FEE-DEBUG] NO SLAB MATCHED — returning 0");
            return (tcode, 0, 0);
        }
        public async Task<string> DressingRegistration(DressingRegistrationRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                if (req.dcode == 0)
                    return "dcode is required";

                bool slotRequired = await db.ExecuteScalarAsync<bool?>(
                    @"SELECT is_slot_required FROM lab_settings
              WHERE tenant_code = @tenant_code AND deleted = false
              ORDER BY (bh_code IS NULL) LIMIT 1",
                    new { tenant_code }) ?? true;

                if (!slotRequired)
                {
                    // NO-SLOT MODE — token restarts at 1 daily, counted only among dressing rows
                    int dressingSeq = await db.ExecuteScalarAsync<int>(
                        @"SELECT COALESCE(MAX(
                      NULLIF(regexp_replace(token_no, '\D', '', 'g'), '')::int
                  ), 0) + 1
                  FROM op_registration
                  WHERE tenant_code = @tenant_code AND isdeleted = false
                  AND is_dressing = true
                  AND visit_date = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date",
                        new { dcode = req.dcode, tenant_code });

                    string dressingToken = $"D{dressingSeq}";

                    var noSlotData = new OpRegistrationModel
                    {
                        op_id = Guid.NewGuid(),
                        op_no = await GenerateOpNo(db, tenant_code),
                        custid = req.custid,
                        dcode = req.dcode,
                        department_code = req.department_code,
                        slot_detail_id = null,
                        visit_type = "DRESSING",
                        reg_type = "WALKIN",
                        visit_date = DateOnly.FromDateTime(DateTime.UtcNow),
                        token_no = dressingToken,   // "D1", "D2", ...
                        queue_no = dressingSeq,     // plain int, unchanged
                        is_dressing = true,
                        visit_status = "WAITING",
                        notes = req.notes,
                        is_direct_walkin = true,
                        tenant_code = tenant_code,
                        isdeleted = false,
                        created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                    };

                    await db.ExecuteAsync(@"
                INSERT INTO op_registration
                (op_id, op_no, custid, dcode, department_code, slot_detail_id, visit_type,
                 reg_type, visit_date, token_no, queue_no, is_dressing,
                 visit_status, notes, is_direct_walkin, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id, @visit_type,
                 @reg_type, @visit_date, @token_no, @queue_no, @is_dressing,
                 @visit_status, @notes, @is_direct_walkin, @tenant_code, @isdeleted, @created_at, @updated_at)",
                        new
                        {
                            noSlotData.op_id,
                            noSlotData.op_no,
                            noSlotData.custid,
                            noSlotData.dcode,
                            noSlotData.department_code,
                            noSlotData.slot_detail_id,
                            noSlotData.visit_type,
                            noSlotData.reg_type,
                            visit_date = noSlotData.visit_date.ToDateTime(TimeOnly.MinValue),
                            noSlotData.token_no,
                            noSlotData.queue_no,
                            noSlotData.is_dressing,
                            noSlotData.visit_status,
                            noSlotData.notes,
                            noSlotData.is_direct_walkin,
                            noSlotData.tenant_code,
                            noSlotData.isdeleted,
                            noSlotData.created_at,
                            noSlotData.updated_at
                        });

                    return $"Success|OpNo:{noSlotData.op_no}|OpId:{noSlotData.op_id}|Token:{noSlotData.token_no}";
                }

                // SLOT-REQUIRED MODE — same slot validation as DirectWalkinRegistration
                var slot = await db.QueryFirstOrDefaultAsync<DoctorAppointmentSlotDetailsModel>(
                    @"SELECT *
              FROM doctor_appointment_slot_details
              WHERE slot_detail_id = @slot_detail_id
                AND tenant_code = @tenant_code
                AND isdeleted = false
                AND is_active = true",
                    new { req.slot_detail_id, tenant_code });

                if (slot == null)
                    return "Slot not found";

                if (slot.dcode != req.dcode)
                    return "Selected slot does not belong to selected doctor";

                if (slot.slot_status == "FULL")
                    return "Slot is full";

                if (slot.slot_status == "CANCELLED")
                    return "Slot is cancelled";

                if (slot.slot_status == "CLOSED")
                    return "Slot is closed";

                if (slot.walkin_count >= slot.max_walkin)
                    return "Walk-in quota full for this slot";

                if (slot.booked_count >= slot.max_patients)
                    return "Slot capacity reached";

                int slotSeq = slot.booked_count + 1;
                string slotToken = $"D{slotSeq}";

                var data = new OpRegistrationModel
                {
                    op_id = Guid.NewGuid(),
                    op_no = await GenerateOpNo(db, tenant_code),
                    custid = req.custid,
                    dcode = req.dcode,
                    department_code = req.department_code,
                    slot_detail_id = slot.slot_detail_id,
                    visit_type = "DRESSING",
                    reg_type = "WALKIN",
                    visit_date = slot.appointment_date,
                    token_no = slotToken,   // "D1", "D2", ...
                    queue_no = slotSeq,     // plain int, unchanged
                    is_dressing = true,
                    visit_status = "WAITING",
                    notes = req.notes,
                    is_direct_walkin = true,
                    tenant_code = tenant_code,
                    isdeleted = false,
                    created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                await db.ExecuteAsync(@"
            INSERT INTO op_registration
            (op_id, op_no, custid, dcode, department_code, slot_detail_id, visit_type,
             reg_type, visit_date, token_no, queue_no, is_dressing,
             visit_status, notes, is_direct_walkin, tenant_code, isdeleted, created_at, updated_at)
            VALUES
            (@op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id, @visit_type,
             @reg_type, @visit_date, @token_no, @queue_no, @is_dressing,
             @visit_status, @notes, @is_direct_walkin, @tenant_code, @isdeleted, @created_at, @updated_at)",
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
                        data.is_dressing,
                        data.visit_status,
                        data.notes,
                        data.is_direct_walkin,
                        data.tenant_code,
                        data.isdeleted,
                        data.created_at,
                        data.updated_at
                    });

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET booked_count = booked_count + 1,
                walkin_count = walkin_count + 1,
                updated_at = now()
            WHERE slot_detail_id = @slot_detail_id
              AND tenant_code = @tenant_code",
                    new { slot_detail_id = slot.slot_detail_id, tenant_code });

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET slot_status = 'FULL'
            WHERE slot_detail_id = @slot_detail_id
              AND booked_count >= max_patients
              AND tenant_code = @tenant_code",
                    new { slot_detail_id = slot.slot_detail_id, tenant_code });

                return $"Success|OpNo:{data.op_no}|OpId:{data.op_id}|Token:{data.token_no}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        private async Task<(string token_no, int seq)> GenerateNextTokenNo(
    IDbConnection db, IDbTransaction tx,
    int dcode, Guid? slot_detail_id, string reg_type, string tenant_code,
    int? service_id = null)   // ✅ was string? service_code
        {
            string bookingType = string.IsNullOrWhiteSpace(reg_type) ? "WALKIN" : reg_type.ToUpper();

            string? prefix;
            bool sharedSequence;
            long? groupId = null;

            if (service_id == null)
            {
                // ── EXISTING OP LOGIC — unchanged ──────────────────
                var groupRow = await db.QueryFirstOrDefaultAsync(
                    @"SELECT dm.group_id, dm.token_prefix AS doctor_prefix,
                     dgm.token_type, dgm.token_prefix AS group_prefix
              FROM   doctor_master dm
              LEFT JOIN doctor_group_master dgm
                     ON dgm.group_id     = dm.group_id
                    AND dgm.tenant_code  = dm.tenant_code
                    AND dgm.is_deleted   = false
                    AND dgm.is_active    = true
              WHERE  dm.dcode       = @dcode
              AND    dm.tenant_code = @tenant_code
              AND    dm.deleted     = false",
                    new { dcode, tenant_code }, tx);

                groupId = groupRow?.group_id;
                string tokenType = (string)(groupRow?.token_type ?? "DOCTOR");
                prefix = (groupId.HasValue && tokenType == "GROUP")
                    ? (string?)groupRow?.group_prefix
                    : (string?)groupRow?.doctor_prefix;

                sharedSequence = groupId.HasValue && tokenType == "GROUP";
            }
            else
            {
                // ── service_type_master by id ──────────────────────
                var svc = await db.QueryFirstOrDefaultAsync<ServiceTypeModel>(
                    @"SELECT * FROM service_type_master
              WHERE service_id = @service_id AND tenant_code = @tenant_code AND deleted = false",
                    new { service_id, tenant_code }, tx);

                if (svc == null)
                    throw new Exception($"Service id {service_id} not configured for this tenant");

                prefix = svc.token_prefix;
                sharedSequence = svc.scope != "DOCTOR";
            }

            int seq;

            if (slot_detail_id.HasValue && slot_detail_id != Guid.Empty)
            {
                // ── SLOT MODE — unchanged ────────────────────────────
                string lockKey = $"SLOT:{tenant_code}:{slot_detail_id}";
                await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey))",
                    new { lockKey }, tx);

                string sql = @"SELECT online_token_start, online_token_end, online_count,
                               walkin_token_start, walkin_token_end, walkin_count
                        FROM   doctor_appointment_slot_details
                        WHERE  slot_detail_id = @slot_detail_id
                        AND    tenant_code    = @tenant_code
                        FOR UPDATE";

                var row = await db.QueryFirstOrDefaultAsync(
                    sql, new { slot_detail_id, tenant_code }, tx);

                if (row == null)
                    throw new Exception("Slot not found while generating token");

                int start, end, used;

                if (bookingType == "WALKIN")
                {
                    if (row.walkin_token_start == null || row.walkin_token_end == null)
                        throw new Exception("Walk-in token range not configured on this slot");
                    start = (int)row.walkin_token_start;
                    end = (int)row.walkin_token_end;
                    used = (int)row.walkin_count;
                }
                else
                {
                    if (row.online_token_start == null || row.online_token_end == null)
                        throw new Exception("Online token range not configured on this slot");
                    start = (int)row.online_token_start;
                    end = (int)row.online_token_end;
                    used = (int)row.online_count;
                }

                seq = bookingType == "WALKIN" ? start + used : start + used - 1;
                if (seq > end)
                    throw new Exception($"{bookingType} token quota exhausted for this slot");
            }
            else
            {
                string lockKey;
                string sql;
                object param;

                if (service_id == null && sharedSequence)
                {
                    // GROUP MODE — OP only, unchanged
                    lockKey = $"GROUP:{tenant_code}:{groupId}";

                    sql = @"SELECT COALESCE(MAX(
                        NULLIF(regexp_replace(o.token_no, '\D', '', 'g'), '')::int
                    ), 0) + 1
                    FROM   op_registration o
                    JOIN   doctor_master d
                           ON d.dcode = o.dcode AND d.tenant_code = o.tenant_code
                    WHERE  d.group_id    = @groupId
                    AND    o.tenant_code = @tenant_code
                    AND    o.isdeleted   = false
                    AND    COALESCE(o.is_dressing, false) = false
                    AND    o.visit_date  = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date";

                    param = new { groupId, tenant_code };
                }
                else if (service_id == null)
                {
                    // INDIVIDUAL MODE — OP only, unchanged
                    lockKey = $"DCODE:{tenant_code}:{dcode}";

                    sql = @"SELECT COALESCE(MAX(
                        NULLIF(regexp_replace(token_no, '\D', '', 'g'), '')::int
                    ), 0) + 1
                    FROM   op_registration
                    WHERE  dcode        = @dcode
                    AND    tenant_code  = @tenant_code
                    AND    isdeleted    = false
                    AND    COALESCE(is_dressing, false) = false
                    AND    visit_date   = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date";

                    param = new { dcode, tenant_code };
                }
                else if (sharedSequence)
                {
                    // TENANT-scope service (e.g. DRESSING)
                    lockKey = $"SVC:{tenant_code}:{service_id}";

                    sql = @"SELECT COALESCE(MAX(
                        NULLIF(regexp_replace(token_no, '\D', '', 'g'), '')::int
                    ), 0) + 1
                    FROM   op_registration
                    WHERE  service_id  = @service_id
                    AND    tenant_code = @tenant_code
                    AND    isdeleted   = false
                    AND    visit_date  = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date";

                    param = new { service_id, tenant_code };
                }
                else
                {
                    // DOCTOR-scope service
                    lockKey = $"SVC:{tenant_code}:{service_id}:{dcode}";

                    sql = @"SELECT COALESCE(MAX(
                        NULLIF(regexp_replace(token_no, '\D', '', 'g'), '')::int
                    ), 0) + 1
                    FROM   op_registration
                    WHERE  service_id  = @service_id
                    AND    dcode       = @dcode
                    AND    tenant_code = @tenant_code
                    AND    isdeleted   = false
                    AND    visit_date  = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date";

                    param = new { service_id, dcode, tenant_code };
                }

                await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey))",
                    new { lockKey }, tx);

                seq = await db.ExecuteScalarAsync<int>(sql, param, tx);
            }

            string tokenNo = string.IsNullOrWhiteSpace(prefix) ? seq.ToString() : $"{prefix}{seq}";
            return (tokenNo, seq);
        }
        // ─────────────────────────────────────────
        // CANCEL OP FOR A CANCELLED BOOKING
        // Called by AppointmentBookingClass right after a booking is cancelled.
        // Sets visit_status = CANCELLED on the linked OP — but only if the
        // patient hasn't already been seen (WAITING or IN_CONSULTATION).
        // A COMPLETED visit is left alone since the consultation already
        // happened regardless of what the booking says now.
        // ─────────────────────────────────────────
        public async Task CancelOpForBooking(Guid booking_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            await db.ExecuteAsync(@"
        UPDATE op_registration
        SET visit_status = 'CANCELLED',
            updated_at   = now()
        WHERE booking_id   = @booking_id
        AND   tenant_code  = @tenant_code
        AND   isdeleted    = false
        AND   visit_status IN ('WAITING', 'IN_CONSULTATION')",
                new { booking_id, tenant_code });
        }
        // ─────────────────────────────────────────
        // CLOSE OP FOR A RESCHEDULED BOOKING
        // Called by AppointmentBookingClass right when the OLD booking is
        // soft-deleted for a reschedule. Sets visit_status = RESCHEDULED on
        // the linked OP — same guard as cancel: only if the patient hasn't
        // been seen yet (WAITING or IN_CONSULTATION). A new OP gets created
        // separately, at check-in, against the NEW booking — this method
        // does not create anything.
        // ─────────────────────────────────────────
        public async Task CloseOpForReschedule(Guid booking_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            await db.ExecuteAsync(@"
        UPDATE op_registration
        SET visit_status = 'RESCHEDULED',
            updated_at   = now()
        WHERE booking_id   = @booking_id
        AND   tenant_code  = @tenant_code
        AND   isdeleted    = false
        AND   visit_status IN ('WAITING', 'IN_CONSULTATION')",
                new { booking_id, tenant_code });
        }
        // ─────────────────────────────────────────
        // CANCEL OP REGISTRATION
        // Separate from UpdateVisitStatus so cancel-specific
        // rules (blocked states, reason capture) live in one place.
        // ─────────────────────────────────────────
        public async Task<string> CancelOp(CancelOpRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var op = await db.QueryFirstOrDefaultAsync<OpRegistrationModel>(
                    @"SELECT * FROM op_registration
              WHERE op_id      = @op_id
              AND   tenant_code = @tenant_code
              AND   isdeleted   = false",
                    new { req.op_id, tenant_code });

                if (op == null)
                    return "OP Registration not found";

                if (op.visit_status == "COMPLETED")
                    return "Cannot cancel a completed visit";

                if (op.visit_status == "CANCELLED")
                    return "Visit is already cancelled";

                if (op.visit_status == "TRANSFERRED")
                    return "Cannot cancel a transferred visit";

                string notes = string.IsNullOrWhiteSpace(req.cancel_reason)
                    ? op.notes ?? string.Empty
                    : string.IsNullOrWhiteSpace(op.notes)
                        ? $"Cancelled. Reason: {req.cancel_reason}"
                        : $"{op.notes} | Cancelled. Reason: {req.cancel_reason}";

                string sql = @"UPDATE op_registration
                   SET visit_status = 'CANCELLED',
                       notes        = @notes,
                       updated_at   = now()
                   WHERE op_id      = @op_id
                   AND   tenant_code = @tenant_code
                   AND   isdeleted   = false";

                int rows = await db.ExecuteAsync(sql, new { req.op_id, notes, tenant_code });

                return rows > 0 ? "Success" : "Cancel failed";
            }
            catch (Exception ex) { return ex.Message; }
        }
        private async Task<(long? group_id, string token_type, string? prefix)> GetDoctorGroupInfo(
    IDbConnection db, IDbTransaction tx, int dcode, string tenant_code)
        {
            var row = await db.QueryFirstOrDefaultAsync(
                @"SELECT dm.group_id, dgm.token_type, dgm.short_name, dgm.group_name
          FROM   doctor_master dm
          LEFT JOIN doctor_group_master dgm
                 ON dgm.group_id     = dm.group_id
                AND dgm.tenant_code  = dm.tenant_code
                AND dgm.is_deleted   = false
                AND dgm.is_active    = true
          WHERE  dm.dcode       = @dcode
          AND    dm.tenant_code = @tenant_code
          AND    dm.deleted     = false",
                new { dcode, tenant_code }, tx);

            if (row == null || row.group_id == null)
                return (null, "DOCTOR", null);   // no group at all → individual

            string tokenType = (string)(row.token_type ?? "DOCTOR");
            string? prefix = (string?)row.short_name ?? (string?)row.group_name;

            return ((long?)row.group_id, tokenType, prefix);
        }
        private async Task<(int? tcode, double rate, double amount)> GetServiceCharge(
    IDbConnection db, string service_name, string tenant_code)
        {
            var test = await db.QueryFirstOrDefaultAsync(
                @"SELECT tcode, amount FROM test_master
          WHERE TRIM(tenant_code) = TRIM(@tenant_code)
          AND   deleted     = false
          AND   name ILIKE @name_pattern
          ORDER BY tcode
          LIMIT 1",
                new { tenant_code, name_pattern = $"{service_name}%" });

            if (test == null)
            {
                Console.WriteLine($"[SVC-FEE-DEBUG] No test_master match for '{service_name}' — returning 0");
                return (null, 0, 0);
            }

            int tcode = (int)test.tcode;
            double amount = test.amount != null ? (double)test.amount : 0;

            Console.WriteLine($"[SVC-FEE-DEBUG] service='{service_name}' tcode={tcode} amount={amount}");

            return (tcode, amount, amount);
        }
        public async Task<string> ServiceRegistration(ServiceRegistrationRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                db.Open();

                if (req.dcode == 0)
                    return "dcode is required";

                if (req.service_id <= 0)
                    return "service_id is required";

                var svc = await db.QueryFirstOrDefaultAsync<ServiceTypeModel>(
                    @"SELECT * FROM service_type_master
              WHERE service_id = @service_id AND tenant_code = @tenant_code AND deleted = false",
                    new { req.service_id, tenant_code });

                if (svc == null)
                    return $"Service id {req.service_id} not configured for this tenant";

                bool isDressing = string.Equals(svc.service_name?.Trim(), "Dressing", StringComparison.OrdinalIgnoreCase);

                bool slotRequired = await db.ExecuteScalarAsync<bool?>(
                    @"SELECT is_slot_required FROM lab_settings
              WHERE tenant_code = @tenant_code AND deleted = false
              ORDER BY (bh_code IS NULL) LIMIT 1",
                    new { tenant_code }) ?? true;

                if (!slotRequired)
                {
                    var noSlotData = new OpRegistrationModel
                    {
                        op_id = Guid.NewGuid(),
                        op_no = await GenerateOpNo(db, tenant_code),
                        custid = req.custid,
                        dcode = req.dcode,
                        department_code = req.department_code,
                        slot_detail_id = null,
                        visit_type = svc.service_name,
                        reg_type = "WALKIN",
                        visit_date = DateOnly.FromDateTime(
                            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"))),
                        visit_status = "WAITING",
                        notes = req.notes,
                        is_direct_walkin = true,
                        is_dressing = isDressing,
                        service_id = req.service_id,
                        tenant_code = tenant_code,
                        isdeleted = false,
                        created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                    };

                    using (var tx = db.BeginTransaction())
                    {
                        try
                        {
                            var (tokenNo, seq) = await GenerateNextTokenNo(
                                db, tx, req.dcode, null, "WALKIN", tenant_code, service_id: req.service_id);
                            noSlotData.token_no = tokenNo;
                            noSlotData.queue_no = seq;

                            await db.ExecuteAsync(@"
                        INSERT INTO op_registration
                        (op_id, op_no, custid, dcode, department_code, slot_detail_id, visit_type,
                         reg_type, visit_date, token_no, queue_no, is_dressing, service_id,
                         visit_status, notes, is_direct_walkin, tenant_code, isdeleted, created_at, updated_at)
                        VALUES
                        (@op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id, @visit_type,
                         @reg_type, @visit_date, @token_no, @queue_no, @is_dressing, @service_id,
                         @visit_status, @notes, @is_direct_walkin, @tenant_code, @isdeleted, @created_at, @updated_at)",
                                new
                                {
                                    noSlotData.op_id,
                                    noSlotData.op_no,
                                    noSlotData.custid,
                                    noSlotData.dcode,
                                    noSlotData.department_code,
                                    noSlotData.slot_detail_id,
                                    noSlotData.visit_type,
                                    noSlotData.reg_type,
                                    visit_date = noSlotData.visit_date.ToDateTime(TimeOnly.MinValue),
                                    noSlotData.token_no,
                                    noSlotData.queue_no,
                                    noSlotData.is_dressing,
                                    service_id = req.service_id,
                                    noSlotData.visit_status,
                                    noSlotData.notes,
                                    noSlotData.is_direct_walkin,
                                    noSlotData.tenant_code,
                                    noSlotData.isdeleted,
                                    noSlotData.created_at,
                                    noSlotData.updated_at
                                }, tx);

                            tx.Commit();
                        }
                        catch { tx.Rollback(); throw; }
                    }

                    var (noSlotTcode, noSlotRate, noSlotAmount) = await GetServiceCharge(db, svc.service_name, tenant_code);

                    await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                    {
                        op_id = noSlotData.op_id.ToString(),
                        custid = noSlotData.custid,
                        tcode = noSlotTcode,
                        rate = noSlotRate,
                        amount = noSlotAmount,
                        quantity = 1
                    }, tenant_code);

                    return $"Success|OpNo:{noSlotData.op_no}|OpId:{noSlotData.op_id}|Token:{noSlotData.token_no}|ServiceId:{req.service_id}";
                }

                // ── SLOT-REQUIRED MODE ──
                var slot = await db.QueryFirstOrDefaultAsync<DoctorAppointmentSlotDetailsModel>(
                    @"SELECT * FROM doctor_appointment_slot_details
              WHERE slot_detail_id = @slot_detail_id AND tenant_code = @tenant_code
              AND isdeleted = false AND is_active = true",
                    new { req.slot_detail_id, tenant_code });

                if (slot == null) return "Slot not found";
                if (slot.dcode != req.dcode) return "Selected slot does not belong to selected doctor";
                if (slot.slot_status == "FULL") return "Slot is full";
                if (slot.slot_status == "CANCELLED") return "Slot is cancelled";
                if (slot.slot_status == "CLOSED") return "Slot is closed";
                if (slot.walkin_count >= slot.max_walkin) return "Walk-in quota full for this slot";
                if (slot.booked_count >= slot.max_patients) return "Slot capacity reached";

                var data = new OpRegistrationModel
                {
                    op_id = Guid.NewGuid(),
                    op_no = await GenerateOpNo(db, tenant_code),
                    custid = req.custid,
                    dcode = req.dcode,
                    department_code = req.department_code,
                    slot_detail_id = slot.slot_detail_id,
                    visit_type = svc.service_name,
                    reg_type = "WALKIN",
                    visit_date = slot.appointment_date,
                    visit_status = "WAITING",
                    notes = req.notes,
                    is_direct_walkin = true,
                    is_dressing = isDressing,
                    service_id = req.service_id,
                    tenant_code = tenant_code,
                    isdeleted = false,
                    created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                string token;

                using (var tx = db.BeginTransaction())
                {
                    try
                    {
                        var (tokenStr, seq) = await GenerateNextTokenNo(
                            db, tx, req.dcode, slot.slot_detail_id, "WALKIN", tenant_code, service_id: req.service_id);
                        token = tokenStr;
                        data.token_no = tokenStr;
                        data.queue_no = seq;

                        await db.ExecuteAsync(@"
                    INSERT INTO op_registration
                    (op_id, op_no, custid, dcode, department_code, slot_detail_id, visit_type,
                     reg_type, visit_date, token_no, queue_no, is_dressing, service_id,
                     visit_status, notes, is_direct_walkin, tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@op_id, @op_no, @custid, @dcode, @department_code, @slot_detail_id, @visit_type,
                     @reg_type, @visit_date, @token_no, @queue_no, @is_dressing, @service_id,
                     @visit_status, @notes, @is_direct_walkin, @tenant_code, @isdeleted, @created_at, @updated_at)",
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
                                data.is_dressing,
                                service_id = req.service_id,
                                data.visit_status,
                                data.notes,
                                data.is_direct_walkin,
                                data.tenant_code,
                                data.isdeleted,
                                data.created_at,
                                data.updated_at
                            }, tx);

                        tx.Commit();
                    }
                    catch { tx.Rollback(); throw; }
                }

                var (feeTcode, feeRate, feeAmount) = await GetServiceCharge(db, svc.service_name, tenant_code);

                await _unbilledCls.AddConsultationCharge(new AddUnbilledConsultationRequest
                {
                    op_id = data.op_id.ToString(),
                    custid = data.custid,
                    tcode = feeTcode,
                    rate = feeRate,
                    amount = feeAmount,
                    quantity = 1
                }, tenant_code);

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET booked_count = booked_count + 1, walkin_count = walkin_count + 1, updated_at = now()
            WHERE slot_detail_id = @slot_detail_id AND tenant_code = @tenant_code",
                    new { slot_detail_id = slot.slot_detail_id, tenant_code });

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET slot_status = 'FULL'
            WHERE slot_detail_id = @slot_detail_id AND booked_count >= max_patients AND tenant_code = @tenant_code",
                    new { slot_detail_id = slot.slot_detail_id, tenant_code });

                return $"Success|OpNo:{data.op_no}|OpId:{data.op_id}|Token:{token}|ServiceId:{req.service_id}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
