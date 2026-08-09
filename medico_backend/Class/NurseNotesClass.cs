using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class NurseNotesClass
    {
        private readonly string _db_conn;

        public NurseNotesClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_db_conn);

        // ─────────────────────────────────────────
        // ADD A NURSE NOTE (any note_type)
        // ─────────────────────────────────────────
        public async Task<string> Add(AddNurseNoteRequest req, string tenant_code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.note_type))
                    return "note_type is required";

                using var db = GetConnection();

                var ipExists = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM ip_registration
                      WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { req.ip_id, tenant_code });

                if (ipExists == 0)
                    return "IP Registration not found";

                string noteType = req.note_type.ToUpper();

                // ── Type-specific required-field checks ──
                // AFTER
                if (noteType == "MEDICATION")
                {
                    string source = string.IsNullOrWhiteSpace(req.medication_source) ? "PRESCRIBED" : req.medication_source.ToUpper();

                    if (source == "PRESCRIBED" && req.prescription_detail_id == null)
                        return "prescription_detail_id is required when medication_source is PRESCRIBED";

                    if (source == "NURSE_INITIATED" && string.IsNullOrWhiteSpace(req.standalone_drug_name))
                        return "standalone_drug_name is required when medication_source is NURSE_INITIATED";
                }

                if (noteType == "DRESSING" && string.IsNullOrWhiteSpace(req.dressing_site))
                    return "dressing_site is required for DRESSING notes";

                if (noteType == "MEDICATION" && req.prescription_detail_id != null)
                {
                    var prExists = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(1) FROM op_prescription_detail
                          WHERE pr_det_id = @prescription_detail_id AND tenant_code = @tenant_code
                          AND isdeleted = false",
                        new { req.prescription_detail_id, tenant_code });

                    if (prExists == 0)
                        return "Prescription detail not found";
                }

                var row = new NurseNotesModel
                {
                    note_id = Guid.NewGuid(),
                    ip_id = req.ip_id,
                    note_type = noteType,
                    note_datetime = req.note_datetime ?? DateTime.UtcNow,
                    shift = req.shift,

                    patient_status = req.patient_status,
                    consciousness_status = req.consciousness_status,
                    clinical_condition = req.clinical_condition,
                    mobility_status = req.mobility_status,
                    diet_type = req.diet_type,
                    activity_status = req.activity_status,

                    prescription_detail_id = req.prescription_detail_id,
                    scheduled_time = req.scheduled_time,
                    given_time = req.given_time,
                    medication_status = req.medication_status,
                    medication_source = string.IsNullOrWhiteSpace(req.medication_source) ? "PRESCRIBED" : req.medication_source.ToUpper(),
                    standalone_drug_name = req.standalone_drug_name,
                    standalone_dosage = req.standalone_dosage,
                    standalone_route = req.standalone_route,

                    infusion_name = req.infusion_name,
                    fluid_type = req.fluid_type,
                    infusion_volume = req.infusion_volume,
                    infusion_volume_unit = req.infusion_volume_unit,
                    infusion_rate = req.infusion_rate,
                    infusion_rate_unit = req.infusion_rate_unit,
                    infusion_site = req.infusion_site,
                    infusion_start_time = req.infusion_start_time,
                    infusion_end_time = req.infusion_end_time,
                    infusion_status = req.infusion_status,
                    total_volume_given = req.total_volume_given,

                    device_id = req.device_id,
                    device_name = req.device_name,
                    device_type = req.device_type,
                    pump_rate = req.pump_rate,
                    pump_rate_unit = req.pump_rate_unit,
                    pump_volume = req.pump_volume,
                    pump_start_time = req.pump_start_time,
                    pump_end_time = req.pump_end_time,
                    device_status = req.device_status,
                    alarm_status = req.alarm_status,

                    dressing_site = req.dressing_site,
                    wound_type = req.wound_type,
                    dressing_type = req.dressing_type,
                    wound_length = req.wound_length,
                    wound_width = req.wound_width,
                    wound_depth = req.wound_depth,
                    wound_condition = req.wound_condition,
                    wound_discharge = req.wound_discharge,
                    wound_discharge_amount = req.wound_discharge_amount,
                    bleeding = req.bleeding,
                    infection_sign = req.infection_sign,
                    dressing_status = req.dressing_status,

                    procedure_id = req.procedure_id,
                    procedure_name = req.procedure_name,
                    procedure_site = req.procedure_site,
                    procedure_start_time = req.procedure_start_time,
                    procedure_end_time = req.procedure_end_time,
                    procedure_status = req.procedure_status,
                    patient_response = req.patient_response,
                    complication = req.complication,
                    complication_details = req.complication_details,
                    procedure_result_value = req.procedure_result_value,
                    procedure_result_unit = req.procedure_result_unit,

                    oral_intake_ml = req.oral_intake_ml,
                    iv_fluid_ml = req.iv_fluid_ml,
                    tube_feed_ml = req.tube_feed_ml,
                    other_intake_ml = req.other_intake_ml,
                    total_intake_ml = req.total_intake_ml,
                    urine_ml = req.urine_ml,
                    vomit_ml = req.vomit_ml,
                    drain_ml = req.drain_ml,
                    stool_count = req.stool_count,
                    other_output_ml = req.other_output_ml,
                    total_output_ml = req.total_output_ml,

                    oxygen_required = req.oxygen_required,
                    oxygen_device = req.oxygen_device,
                    oxygen_flow_rate = req.oxygen_flow_rate,
                    oxygen_flow_unit = req.oxygen_flow_unit,
                    fio2 = req.fio2,
                    oxygen_start_time = req.oxygen_start_time,
                    oxygen_end_time = req.oxygen_end_time,
                    oxygen_status = req.oxygen_status,

                    pain_present = req.pain_present,
                    pain_location = req.pain_location,
                    pain_type = req.pain_type,
                    pain_duration = req.pain_duration,
                    pain_intervention = req.pain_intervention,
                    pain_after_intervention_score = req.pain_after_intervention_score,

                    movement_type = req.movement_type,
                    from_location = req.from_location,
                    to_location = req.to_location,
                    movement_datetime = req.movement_datetime,
                    movement_reason = req.movement_reason,

                    fall_risk_score = req.fall_risk_score,
                    fall_risk_level = req.fall_risk_level,
                    fall_risk_scale = req.fall_risk_scale,
                    fall_precautions = req.fall_precautions,
                    fall_occurred = req.fall_occurred,
                    fall_incident_details = req.fall_incident_details,

                    skin_integrity = req.skin_integrity,
                    pressure_sore_present = req.pressure_sore_present,
                    pressure_sore_site = req.pressure_sore_site,
                    pressure_sore_stage = req.pressure_sore_stage,
                    braden_score = req.braden_score,
                    repositioning_done = req.repositioning_done,
                    repositioning_time = req.repositioning_time,

                    catheter_type = req.catheter_type,
                    catheter_size = req.catheter_size,
                    catheter_insertion_date = req.catheter_insertion_date,
                    catheter_care_done = req.catheter_care_done,
                    bowel_movement = req.bowel_movement,
                    bladder_status = req.bladder_status,

                    restraint_used = req.restraint_used,
                    restraint_type = req.restraint_type,
                    restraint_reason = req.restraint_reason,
                    restraint_start_time = req.restraint_start_time,
                    restraint_end_time = req.restraint_end_time,
                    restraint_site_checked = req.restraint_site_checked,

                    isolation_required = req.isolation_required,
                    isolation_type = req.isolation_type,
                    ppe_used = req.ppe_used,
                    hand_hygiene_compliance = req.hand_hygiene_compliance,

                    education_topic = req.education_topic,
                    education_given_to = req.education_given_to,
                    education_method = req.education_method,
                    patient_understanding = req.patient_understanding,

                    handover_to = req.handover_to,
                    pending_medications = req.pending_medications,
                    pending_investigations = req.pending_investigations,
                    pending_procedures = req.pending_procedures,
                    special_instructions = req.special_instructions,
                    handover_notes = req.handover_notes,

                    notes = req.notes,
                    given_by = req.given_by,
                    dcode = req.dcode,

                    note_status = "COMPLETED",
                    tenant_code = tenant_code,
                    isdeleted = false,
                    created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                await db.InsertAsync(row);
                return $"Success|NoteId:{row.note_id}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // FULL NURSING TIMELINE FOR AN IP (optionally filtered by note_type)
        // ─────────────────────────────────────────
        public async Task<List<NurseNotesModel>> GetByIpId(
            Guid ip_id, string tenant_code, string? note_type = null)
        {
            using var db = GetConnection();

            string sql = @"SELECT * FROM nurse_notes
                            WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                            AND (@note_type IS NULL OR note_type = @note_type)
                            ORDER BY note_datetime DESC";

            var res = await db.QueryAsync<NurseNotesModel>(
                sql, new { ip_id, tenant_code, note_type = note_type?.ToUpper() });
            return res.ToList();
        }

        public async Task<NurseNotesModel?> GetById(Guid note_id, string tenant_code)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<NurseNotesModel>(
                @"SELECT * FROM nurse_notes
                  WHERE note_id = @note_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { note_id, tenant_code });
        }

        // ── Medication administration history — joins the ordered drug details ──
        public async Task<List<dynamic>> GetMedicationHistory(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();

            string sql = @"
                SELECT
                    nn.note_id, nn.note_datetime, nn.shift,
                    nn.scheduled_time, nn.given_time, nn.medication_status,
                    nn.given_by, nn.notes,
                    pd.pr_det_id, pd.drug_name, pd.generic_name, pd.drug_category,
                    pd.morning, pd.afternoon, pd.evening, pd.night,
                    pd.before_food, pd.after_food, pd.route, pd.days, pd.qty
                FROM nurse_notes nn
                LEFT JOIN op_prescription_detail pd ON pd.pr_det_id = nn.prescription_detail_id
                WHERE nn.ip_id = @ip_id AND nn.tenant_code = @tenant_code
                AND nn.note_type = 'MEDICATION' AND nn.isdeleted = false
                ORDER BY nn.note_datetime DESC";

            var res = await db.QueryAsync<dynamic>(sql, new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<List<NurseNotesModel>> GetDressingHistory(Guid ip_id, string tenant_code)
            => await GetByIpId(ip_id, tenant_code, "DRESSING");

        public async Task<List<NurseNotesModel>> GetHandoverHistory(Guid ip_id, string tenant_code)
            => await GetByIpId(ip_id, tenant_code, "HANDOVER");

        // ─────────────────────────────────────────
        // COMBINED IP NURSING CHART — pulls nurse_notes + vitals + symptoms +
        // prescriptions + investigations for one ip_id in a single response.
        // ─────────────────────────────────────────
        public async Task<IpNursingChartViewModel> GetFullIpChart(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();

            var chart = new IpNursingChartViewModel { ip_id = ip_id };

            // Npgsql does not support multiple in-flight commands on one connection
            // (no MARS) — each query must be awaited before the next one starts.
            var notes = await db.QueryAsync<NurseNotesModel>(
                @"SELECT * FROM nurse_notes
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
          ORDER BY note_datetime DESC",
                new { ip_id, tenant_code });
            chart.nurse_notes = notes.ToList();

            var vitals = await db.QueryAsync<dynamic>(
                @"SELECT * FROM patient_vitals
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
          ORDER BY created_at DESC",
                new { ip_id, tenant_code });
            chart.vitals = vitals.ToList();

            var symptoms = await db.QueryAsync<dynamic>(
                @"SELECT * FROM op_case_sheet_symptoms
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code
          ORDER BY created_at DESC",
                new { ip_id, tenant_code });
            chart.symptoms = symptoms.ToList();

            var prescriptions = await db.QueryAsync<dynamic>(
                @"SELECT pm.pr_code, pm.pr_date, pd.*
          FROM op_prescription_master pm
          JOIN op_prescription_detail pd ON pd.pr_id = pm.pr_id
          WHERE pm.ip_id = @ip_id AND pm.tenant_code = @tenant_code
          AND pm.isdeleted = false AND pd.isdeleted = false
          ORDER BY pm.pr_date DESC",
                new { ip_id, tenant_code });
            chart.prescriptions = prescriptions.ToList();

            var investigations = await db.QueryAsync<dynamic>(
                @"SELECT im.inv_code, im.inv_date, id.*
          FROM op_investigation_master im
          JOIN op_investigation_detail id ON id.inv_id = im.inv_id
          WHERE im.ip_id = @ip_id AND im.tenant_code = @tenant_code
          AND im.isdeleted = false AND id.isdeleted = false
          ORDER BY im.inv_date DESC",
                new { ip_id, tenant_code });
            chart.investigations = investigations.ToList();

            return chart;
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(UpdateNurseNoteRequest req, string tenant_code)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<NurseNotesModel>(
                @"SELECT * FROM nurse_notes
                  WHERE note_id = @note_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.note_id, tenant_code });

            if (existing == null) return "Note not found";
            if (existing.note_status == "CANCELLED") return "Cannot update a cancelled note";

            string sql = @"UPDATE nurse_notes SET
                note_type = @note_type, note_datetime = @note_datetime, shift = @shift,

                patient_status = @patient_status, consciousness_status = @consciousness_status,
                clinical_condition = @clinical_condition, mobility_status = @mobility_status,
                diet_type = @diet_type, activity_status = @activity_status,

                prescription_detail_id = @prescription_detail_id, scheduled_time = @scheduled_time,
                given_time = @given_time, medication_status = @medication_status,
                medication_source=@medication_source,standalone_drug_name=@standalone_drug_name,
                standalone_dosage=@standalone_dosage,standalone_route=@standalone_route,

                infusion_name = @infusion_name, fluid_type = @fluid_type,
                infusion_volume = @infusion_volume, infusion_volume_unit = @infusion_volume_unit,
                infusion_rate = @infusion_rate, infusion_rate_unit = @infusion_rate_unit,
                infusion_site = @infusion_site, infusion_start_time = @infusion_start_time,
                infusion_end_time = @infusion_end_time, infusion_status = @infusion_status,
                total_volume_given = @total_volume_given,

                device_id = @device_id, device_name = @device_name, device_type = @device_type,
                pump_rate = @pump_rate, pump_rate_unit = @pump_rate_unit, pump_volume = @pump_volume,
                pump_start_time = @pump_start_time, pump_end_time = @pump_end_time,
                device_status = @device_status, alarm_status = @alarm_status,

                dressing_site = @dressing_site, wound_type = @wound_type, dressing_type = @dressing_type,
                wound_length = @wound_length, wound_width = @wound_width, wound_depth = @wound_depth,
                wound_condition = @wound_condition, wound_discharge = @wound_discharge,
                wound_discharge_amount = @wound_discharge_amount, bleeding = @bleeding,
                infection_sign = @infection_sign, dressing_status = @dressing_status,

                procedure_id = @procedure_id, procedure_name = @procedure_name, procedure_site = @procedure_site,
                procedure_start_time = @procedure_start_time, procedure_end_time = @procedure_end_time,
                procedure_status = @procedure_status, patient_response = @patient_response,
                complication = @complication, complication_details = @complication_details,
                procedure_result_value = @procedure_result_value, procedure_result_unit = @procedure_result_unit,

                oral_intake_ml = @oral_intake_ml, iv_fluid_ml = @iv_fluid_ml, tube_feed_ml = @tube_feed_ml,
                other_intake_ml = @other_intake_ml, total_intake_ml = @total_intake_ml,
                urine_ml = @urine_ml, vomit_ml = @vomit_ml, drain_ml = @drain_ml,
                stool_count = @stool_count, other_output_ml = @other_output_ml, total_output_ml = @total_output_ml,

                oxygen_required = @oxygen_required, oxygen_device = @oxygen_device,
                oxygen_flow_rate = @oxygen_flow_rate, oxygen_flow_unit = @oxygen_flow_unit,
                fio2 = @fio2, oxygen_start_time = @oxygen_start_time, oxygen_end_time = @oxygen_end_time,
                oxygen_status = @oxygen_status,

                pain_present = @pain_present, pain_location = @pain_location, pain_type = @pain_type,
                pain_duration = @pain_duration, pain_intervention = @pain_intervention,
                pain_after_intervention_score = @pain_after_intervention_score,

                movement_type = @movement_type, from_location = @from_location, to_location = @to_location,
                movement_datetime = @movement_datetime, movement_reason = @movement_reason,

                fall_risk_score = @fall_risk_score, fall_risk_level = @fall_risk_level,
                fall_risk_scale = @fall_risk_scale, fall_precautions = @fall_precautions,
                fall_occurred = @fall_occurred, fall_incident_details = @fall_incident_details,

                skin_integrity = @skin_integrity, pressure_sore_present = @pressure_sore_present,
                pressure_sore_site = @pressure_sore_site, pressure_sore_stage = @pressure_sore_stage,
                braden_score = @braden_score, repositioning_done = @repositioning_done,
                repositioning_time = @repositioning_time,

                catheter_type = @catheter_type, catheter_size = @catheter_size,
                catheter_insertion_date = @catheter_insertion_date, catheter_care_done = @catheter_care_done,
                bowel_movement = @bowel_movement, bladder_status = @bladder_status,

                restraint_used = @restraint_used, restraint_type = @restraint_type,
                restraint_reason = @restraint_reason, restraint_start_time = @restraint_start_time,
                restraint_end_time = @restraint_end_time, restraint_site_checked = @restraint_site_checked,

                isolation_required = @isolation_required, isolation_type = @isolation_type,
                ppe_used = @ppe_used, hand_hygiene_compliance = @hand_hygiene_compliance,

                education_topic = @education_topic, education_given_to = @education_given_to,
                education_method = @education_method, patient_understanding = @patient_understanding,

                handover_to = @handover_to, pending_medications = @pending_medications,
                pending_investigations = @pending_investigations, pending_procedures = @pending_procedures,
                special_instructions = @special_instructions, handover_notes = @handover_notes,

                notes = @notes, given_by = @given_by, dcode = @dcode,
                updated_at = now()
                WHERE note_id = @note_id AND tenant_code = @tenant_code";

            int rows = await db.ExecuteAsync(sql, new
            {
                note_type = req.note_type.ToUpper(),
                note_datetime = req.note_datetime ?? existing.note_datetime,
                req.shift,

                req.patient_status,
                req.consciousness_status,
                req.clinical_condition,
                req.mobility_status,
                req.diet_type,
                req.activity_status,

                req.prescription_detail_id,
                req.scheduled_time,
                req.given_time,
                req.medication_status,
                req.medication_source,
                req.standalone_drug_name,
                req.standalone_dosage,
                req.standalone_route,

                req.infusion_name,
                req.fluid_type,
                req.infusion_volume,
                req.infusion_volume_unit,
                req.infusion_rate,
                req.infusion_rate_unit,
                req.infusion_site,
                req.infusion_start_time,
                req.infusion_end_time,
                req.infusion_status,
                req.total_volume_given,

                req.device_id,
                req.device_name,
                req.device_type,
                req.pump_rate,
                req.pump_rate_unit,
                req.pump_volume,
                req.pump_start_time,
                req.pump_end_time,
                req.device_status,
                req.alarm_status,

                req.dressing_site,
                req.wound_type,
                req.dressing_type,
                req.wound_length,
                req.wound_width,
                req.wound_depth,
                req.wound_condition,
                req.wound_discharge,
                req.wound_discharge_amount,
                req.bleeding,
                req.infection_sign,
                req.dressing_status,

                req.procedure_id,
                req.procedure_name,
                req.procedure_site,
                req.procedure_start_time,
                req.procedure_end_time,
                req.procedure_status,
                req.patient_response,
                req.complication,
                req.complication_details,
                req.procedure_result_value,
                req.procedure_result_unit,

                req.oral_intake_ml,
                req.iv_fluid_ml,
                req.tube_feed_ml,
                req.other_intake_ml,
                req.total_intake_ml,
                req.urine_ml,
                req.vomit_ml,
                req.drain_ml,
                req.stool_count,
                req.other_output_ml,
                req.total_output_ml,

                req.oxygen_required,
                req.oxygen_device,
                req.oxygen_flow_rate,
                req.oxygen_flow_unit,
                req.fio2,
                req.oxygen_start_time,
                req.oxygen_end_time,
                req.oxygen_status,

                req.pain_present,
                req.pain_location,
                req.pain_type,
                req.pain_duration,
                req.pain_intervention,
                req.pain_after_intervention_score,

                req.movement_type,
                req.from_location,
                req.to_location,
                req.movement_datetime,
                req.movement_reason,

                req.fall_risk_score,
                req.fall_risk_level,
                req.fall_risk_scale,
                req.fall_precautions,
                req.fall_occurred,
                req.fall_incident_details,

                req.skin_integrity,
                req.pressure_sore_present,
                req.pressure_sore_site,
                req.pressure_sore_stage,
                req.braden_score,
                req.repositioning_done,
                req.repositioning_time,

                req.catheter_type,
                req.catheter_size,
                req.catheter_insertion_date,
                req.catheter_care_done,
                req.bowel_movement,
                req.bladder_status,

                req.restraint_used,
                req.restraint_type,
                req.restraint_reason,
                req.restraint_start_time,
                req.restraint_end_time,
                req.restraint_site_checked,

                req.isolation_required,
                req.isolation_type,
                req.ppe_used,
                req.hand_hygiene_compliance,

                req.education_topic,
                req.education_given_to,
                req.education_method,
                req.patient_understanding,

                req.handover_to,
                req.pending_medications,
                req.pending_investigations,
                req.pending_procedures,
                req.special_instructions,
                req.handover_notes,

                req.notes,
                req.given_by,
                req.dcode,
                req.note_id,
                tenant_code
            });

            return rows > 0 ? "Success" : "Update failed";
        }

        // ─────────────────────────────────────────
        // VERIFY (e.g. senior nurse/doctor sign-off on a note)
        // ─────────────────────────────────────────
        public async Task<string> Verify(VerifyNurseNoteRequest req, string tenant_code)
        {
            using var db = GetConnection();

            int rows = await db.ExecuteAsync(
                @"UPDATE nurse_notes
                  SET verified_by = @verified_by, verified_at = now(), updated_at = now()
                  WHERE note_id = @note_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.verified_by, req.note_id, tenant_code });

            return rows > 0 ? "Success" : "Note not found";
        }

        // ─────────────────────────────────────────
        // CANCEL (audit-preserving — keeps the row, marks it cancelled)
        // ─────────────────────────────────────────
        public async Task<string> Cancel(CancelNurseNoteRequest req, string tenant_code)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<NurseNotesModel>(
                @"SELECT * FROM nurse_notes
                  WHERE note_id = @note_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.note_id, tenant_code });

            if (existing == null) return "Note not found";
            if (existing.note_status == "CANCELLED") return "Note already cancelled";

            int rows = await db.ExecuteAsync(
                @"UPDATE nurse_notes
                  SET note_status = 'CANCELLED', cancelled_by = @cancelled_by,
                      cancelled_at = now(), cancel_reason = @cancel_reason, updated_at = now()
                  WHERE note_id = @note_id AND tenant_code = @tenant_code",
                new { req.cancelled_by, cancel_reason = req.cancel_reason ?? "No reason given", req.note_id, tenant_code });

            return rows > 0 ? "Success" : "Cancel failed";
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(Guid note_id, string tenant_code)
        {
            using var db = GetConnection();
            int rows = await db.ExecuteAsync(
                @"UPDATE nurse_notes SET isdeleted = true, updated_at = now()
                  WHERE note_id = @note_id AND tenant_code = @tenant_code",
                new { note_id, tenant_code });

            return rows > 0 ? "Success" : "Note not found";
        }
    }
}