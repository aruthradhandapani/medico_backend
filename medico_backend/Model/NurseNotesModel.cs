using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("nurse_notes")]
    public class NurseNotesModel
    {
        [ExplicitKey]
        public Guid note_id { get; set; } = Guid.NewGuid();

        // Patient / Admission
        public Guid ip_id { get; set; }

        // Note identification
        public string note_type { get; set; } = string.Empty;
        public DateTime note_datetime { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public string? shift { get; set; }

        // ── PATIENT STATUS / GENERAL ─────────────────────
        public string? patient_status { get; set; }
        public string? consciousness_status { get; set; }
        public string? clinical_condition { get; set; }
        public string? mobility_status { get; set; }
        public string? diet_type { get; set; }
        public string? activity_status { get; set; }

        // ── MEDICATION ADMINISTRATION ────────────────────
        // drug_name/dosage/route come from op_prescription_detail via prescription_detail_id
        public Guid? prescription_detail_id { get; set; }
        public DateTime? scheduled_time { get; set; }
        public DateTime? given_time { get; set; }
        public string? medication_status { get; set; }   // GIVEN / SKIPPED / REFUSED / HELD
        public string medication_source { get; set; } = "PRESCRIBED";  // PRESCRIBED / NURSE_INITIATED
        public string? standalone_drug_name { get; set; }
        public string? standalone_dosage { get; set; }
        public string? standalone_route { get; set; }

        // ── INFUSION ──────────────────────────────────────
        public string? infusion_name { get; set; }
        public string? fluid_type { get; set; }
        public decimal? infusion_volume { get; set; }
        public string? infusion_volume_unit { get; set; }
        public decimal? infusion_rate { get; set; }
        public string? infusion_rate_unit { get; set; }
        public string? infusion_site { get; set; }
        public DateTime? infusion_start_time { get; set; }
        public DateTime? infusion_end_time { get; set; }
        public string? infusion_status { get; set; }
        public decimal? total_volume_given { get; set; }

        // ── INFUSION PUMP / DEVICE ────────────────────────
        public long? device_id { get; set; }
        public string? device_name { get; set; }
        public string? device_type { get; set; }
        public decimal? pump_rate { get; set; }
        public string? pump_rate_unit { get; set; }
        public decimal? pump_volume { get; set; }
        public DateTime? pump_start_time { get; set; }
        public DateTime? pump_end_time { get; set; }
        public string? device_status { get; set; }
        public string? alarm_status { get; set; }

        // ── DRESSING / WOUND ──────────────────────────────
        public string? dressing_site { get; set; }
        public string? wound_type { get; set; }
        public string? dressing_type { get; set; }
        public decimal? wound_length { get; set; }
        public decimal? wound_width { get; set; }
        public decimal? wound_depth { get; set; }
        public string? wound_condition { get; set; }
        public string? wound_discharge { get; set; }
        public string? wound_discharge_amount { get; set; }
        public bool? bleeding { get; set; }
        public bool? infection_sign { get; set; }
        public string? dressing_status { get; set; }

        // ── PROCEDURE (bedside nursing procedure) ────────
        public long? procedure_id { get; set; }
        public string? procedure_name { get; set; }
        public string? procedure_site { get; set; }
        public DateTime? procedure_start_time { get; set; }
        public DateTime? procedure_end_time { get; set; }
        public string? procedure_status { get; set; }
        public string? patient_response { get; set; }
        public bool? complication { get; set; }
        public string? complication_details { get; set; }
        public string? procedure_result_value { get; set; }   // e.g. bedside glucometer/ECG reading
        public string? procedure_result_unit { get; set; }

        // ── INTAKE / OUTPUT ───────────────────────────────
        public decimal? oral_intake_ml { get; set; }
        public decimal? iv_fluid_ml { get; set; }
        public decimal? tube_feed_ml { get; set; }
        public decimal? other_intake_ml { get; set; }
        public decimal? total_intake_ml { get; set; }
        public decimal? urine_ml { get; set; }
        public decimal? vomit_ml { get; set; }
        public decimal? drain_ml { get; set; }
        public int? stool_count { get; set; }
        public decimal? other_output_ml { get; set; }
        public decimal? total_output_ml { get; set; }

        // ── OXYGEN ────────────────────────────────────────
        public bool? oxygen_required { get; set; }
        public string? oxygen_device { get; set; }
        public decimal? oxygen_flow_rate { get; set; }
        public string? oxygen_flow_unit { get; set; }
        public decimal? fio2 { get; set; }
        public DateTime? oxygen_start_time { get; set; }
        public DateTime? oxygen_end_time { get; set; }
        public string? oxygen_status { get; set; }

        // ── PAIN (nursing reassessment) ──────────────────
        public bool? pain_present { get; set; }
        public string? pain_location { get; set; }
        public string? pain_type { get; set; }
        public string? pain_duration { get; set; }
        public string? pain_intervention { get; set; }
        public int? pain_after_intervention_score { get; set; }

        // ── PATIENT MOVEMENT ──────────────────────────────
        public string? movement_type { get; set; }
        public string? from_location { get; set; }
        public string? to_location { get; set; }
        public DateTime? movement_datetime { get; set; }
        public string? movement_reason { get; set; }

        // ── FALL RISK ASSESSMENT ──────────────────────────
        public int? fall_risk_score { get; set; }
        public string? fall_risk_level { get; set; }     // LOW / MODERATE / HIGH
        public string? fall_risk_scale { get; set; }      // MORSE / HENDRICH II
        public string? fall_precautions { get; set; }
        public bool? fall_occurred { get; set; }
        public string? fall_incident_details { get; set; }

        // ── SKIN / PRESSURE SORE ASSESSMENT ──────────────
        public string? skin_integrity { get; set; }        // INTACT / BROKEN / REDNESS
        public bool? pressure_sore_present { get; set; }
        public string? pressure_sore_site { get; set; }
        public string? pressure_sore_stage { get; set; }    // STAGE_1..4 / UNSTAGEABLE
        public int? braden_score { get; set; }
        public bool? repositioning_done { get; set; }
        public DateTime? repositioning_time { get; set; }

        // ── CATHETER / ELIMINATION CARE ──────────────────
        public string? catheter_type { get; set; }          // FOLEY / SUPRAPUBIC / CONDOM / NONE
        public string? catheter_size { get; set; }
        public DateTime? catheter_insertion_date { get; set; }
        public bool? catheter_care_done { get; set; }
        public string? bowel_movement { get; set; }          // NORMAL / CONSTIPATED / DIARRHEA / NIL
        public string? bladder_status { get; set; }          // VOIDED / RETENTION / CATHETERIZED

        // ── RESTRAINT ─────────────────────────────────────
        public bool? restraint_used { get; set; }
        public string? restraint_type { get; set; }
        public string? restraint_reason { get; set; }
        public DateTime? restraint_start_time { get; set; }
        public DateTime? restraint_end_time { get; set; }
        public bool? restraint_site_checked { get; set; }

        // ── ISOLATION / INFECTION CONTROL ────────────────
        public bool? isolation_required { get; set; }
        public string? isolation_type { get; set; }          // CONTACT / DROPLET / AIRBORNE / PROTECTIVE
        public string? ppe_used { get; set; }
        public bool? hand_hygiene_compliance { get; set; }

        // ── PATIENT / FAMILY EDUCATION ────────────────────
        public string? education_topic { get; set; }
        public string? education_given_to { get; set; }      // PATIENT / ATTENDER / FAMILY
        public string? education_method { get; set; }        // VERBAL / DEMONSTRATION / PRINTED
        public string? patient_understanding { get; set; }   // UNDERSTOOD / PARTIAL / NOT_UNDERSTOOD

        // ── SHIFT HANDOVER ────────────────────────────────
        public int? handover_to { get; set; }
        public string? pending_medications { get; set; }
        public string? pending_investigations { get; set; }
        public string? pending_procedures { get; set; }
        public string? special_instructions { get; set; }
        public string? handover_notes { get; set; }

        // ── GENERAL NOTES ─────────────────────────────────
        public string? notes { get; set; }

        // ── WHO ENTERED / PERFORMED ───────────────────────
        public int? given_by { get; set; }
        public int? dcode { get; set; }

        // ── AUDIT ─────────────────────────────────────────
        public string note_status { get; set; } = "COMPLETED";
        public int? verified_by { get; set; }
        public DateTime? verified_at { get; set; }
        public int? cancelled_by { get; set; }
        public DateTime? cancelled_at { get; set; }
        public string? cancel_reason { get; set; }

        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public int usercode { get; set; } = 1;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

    }

    // ─────────────────────────────────────────────────────────────
    // REQUEST SHAPES
    // ─────────────────────────────────────────────────────────────

    public class AddNurseNoteRequest
    {
        public Guid ip_id { get; set; }
        public string note_type { get; set; } = string.Empty;
        public DateTime? note_datetime { get; set; }
        public string? shift { get; set; }

        public string? patient_status { get; set; }
        public string? consciousness_status { get; set; }
        public string? clinical_condition { get; set; }
        public string? mobility_status { get; set; }
        public string? diet_type { get; set; }
        public string? activity_status { get; set; }

        public Guid? prescription_detail_id { get; set; }
        public DateTime? scheduled_time { get; set; }
        public DateTime? given_time { get; set; }
        public string? medication_status { get; set; }
        public string medication_source { get; set; } = "PRESCRIBED";  // PRESCRIBED / NURSE_INITIATED
        public string? standalone_drug_name { get; set; }
        public string? standalone_dosage { get; set; }
        public string? standalone_route { get; set; }

        public string? infusion_name { get; set; }
        public string? fluid_type { get; set; }
        public decimal? infusion_volume { get; set; }
        public string? infusion_volume_unit { get; set; }
        public decimal? infusion_rate { get; set; }
        public string? infusion_rate_unit { get; set; }
        public string? infusion_site { get; set; }
        public DateTime? infusion_start_time { get; set; }
        public DateTime? infusion_end_time { get; set; }
        public string? infusion_status { get; set; }
        public decimal? total_volume_given { get; set; }

        public long? device_id { get; set; }
        public string? device_name { get; set; }
        public string? device_type { get; set; }
        public decimal? pump_rate { get; set; }
        public string? pump_rate_unit { get; set; }
        public decimal? pump_volume { get; set; }
        public DateTime? pump_start_time { get; set; }
        public DateTime? pump_end_time { get; set; }
        public string? device_status { get; set; }
        public string? alarm_status { get; set; }

        public string? dressing_site { get; set; }
        public string? wound_type { get; set; }
        public string? dressing_type { get; set; }
        public decimal? wound_length { get; set; }
        public decimal? wound_width { get; set; }
        public decimal? wound_depth { get; set; }
        public string? wound_condition { get; set; }
        public string? wound_discharge { get; set; }
        public string? wound_discharge_amount { get; set; }
        public bool? bleeding { get; set; }
        public bool? infection_sign { get; set; }
        public string? dressing_status { get; set; }

        public long? procedure_id { get; set; }
        public string? procedure_name { get; set; }
        public string? procedure_site { get; set; }
        public DateTime? procedure_start_time { get; set; }
        public DateTime? procedure_end_time { get; set; }
        public string? procedure_status { get; set; }
        public string? patient_response { get; set; }
        public bool? complication { get; set; }
        public string? complication_details { get; set; }
        public string? procedure_result_value { get; set; }   // e.g. bedside glucometer/ECG reading
        public string? procedure_result_unit { get; set; }

        public decimal? oral_intake_ml { get; set; }
        public decimal? iv_fluid_ml { get; set; }
        public decimal? tube_feed_ml { get; set; }
        public decimal? other_intake_ml { get; set; }
        public decimal? total_intake_ml { get; set; }
        public decimal? urine_ml { get; set; }
        public decimal? vomit_ml { get; set; }
        public decimal? drain_ml { get; set; }
        public int? stool_count { get; set; }
        public decimal? other_output_ml { get; set; }
        public decimal? total_output_ml { get; set; }

        public bool? oxygen_required { get; set; }
        public string? oxygen_device { get; set; }
        public decimal? oxygen_flow_rate { get; set; }
        public string? oxygen_flow_unit { get; set; }
        public decimal? fio2 { get; set; }
        public DateTime? oxygen_start_time { get; set; }
        public DateTime? oxygen_end_time { get; set; }
        public string? oxygen_status { get; set; }

        public bool? pain_present { get; set; }
        public string? pain_location { get; set; }
        public string? pain_type { get; set; }
        public string? pain_duration { get; set; }
        public string? pain_intervention { get; set; }
        public int? pain_after_intervention_score { get; set; }

        public string? movement_type { get; set; }
        public string? from_location { get; set; }
        public string? to_location { get; set; }
        public DateTime? movement_datetime { get; set; }
        public string? movement_reason { get; set; }

        public int? fall_risk_score { get; set; }
        public string? fall_risk_level { get; set; }
        public string? fall_risk_scale { get; set; }
        public string? fall_precautions { get; set; }
        public bool? fall_occurred { get; set; }
        public string? fall_incident_details { get; set; }

        public string? skin_integrity { get; set; }
        public bool? pressure_sore_present { get; set; }
        public string? pressure_sore_site { get; set; }
        public string? pressure_sore_stage { get; set; }
        public int? braden_score { get; set; }
        public bool? repositioning_done { get; set; }
        public DateTime? repositioning_time { get; set; }

        public string? catheter_type { get; set; }
        public string? catheter_size { get; set; }
        public DateTime? catheter_insertion_date { get; set; }
        public bool? catheter_care_done { get; set; }
        public string? bowel_movement { get; set; }
        public string? bladder_status { get; set; }

        public bool? restraint_used { get; set; }
        public string? restraint_type { get; set; }
        public string? restraint_reason { get; set; }
        public DateTime? restraint_start_time { get; set; }
        public DateTime? restraint_end_time { get; set; }
        public bool? restraint_site_checked { get; set; }

        public bool? isolation_required { get; set; }
        public string? isolation_type { get; set; }
        public string? ppe_used { get; set; }
        public bool? hand_hygiene_compliance { get; set; }

        public string? education_topic { get; set; }
        public string? education_given_to { get; set; }
        public string? education_method { get; set; }
        public string? patient_understanding { get; set; }

        public int? handover_to { get; set; }
        public string? pending_medications { get; set; }
        public string? pending_investigations { get; set; }
        public string? pending_procedures { get; set; }
        public string? special_instructions { get; set; }
        public string? handover_notes { get; set; }

        public string? notes { get; set; }
        public int? given_by { get; set; }
        public int? dcode { get; set; }

        
    }

    public class UpdateNurseNoteRequest : AddNurseNoteRequest
    {
        public Guid note_id { get; set; }
    }

    public class VerifyNurseNoteRequest
    {
        public Guid note_id { get; set; }
        public int verified_by { get; set; }
    }

    public class CancelNurseNoteRequest
    {
        public Guid note_id { get; set; }
        public int cancelled_by { get; set; }
        public string? cancel_reason { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    // COMBINED IP NURSING CHART VIEW
    // ─────────────────────────────────────────────────────────────
    public class IpNursingChartViewModel
    {
        public Guid ip_id { get; set; }
        public List<NurseNotesModel> nurse_notes { get; set; } = new();
        public List<dynamic> vitals { get; set; } = new();          // from patient_vitals
        public List<dynamic> symptoms { get; set; } = new();        // from op_case_sheet_symptoms
        public List<dynamic> prescriptions { get; set; } = new();   // from op_prescription_detail (joined)
        public List<dynamic> investigations { get; set; } = new();  // from op_investigation_detail
    }
}