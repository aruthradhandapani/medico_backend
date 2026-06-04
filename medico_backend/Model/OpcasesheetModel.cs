using Dapper.Contrib.Extensions;


namespace medico_backend.Model
{

    [Table("op_case_sheet")]
    public class OpCaseSheetModel
    {
        [ExplicitKey]
        public Guid sheet_id { get; set; } = Guid.NewGuid();
        public Guid op_id { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public DateTime? visit_date { get; set; } = DateTime.UtcNow.Date;


        // Clinical notes
        public string? chief_complaint { get; set; }
        public string? symptoms { get; set; }
        public string? examination { get; set; }
        public string? advise { get; set; }
        public string? notes { get; set; }


        // Follow up
        public DateTime? followup_date { get; set; }
        public string? followup_notes { get; set; }


        // Status
        public bool is_consulted { get; set; } = false;
        public string sheet_status { get; set; } = "DRAFT";  // DRAFT / FINAL


        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    [Table("op_case_sheet_diagnosis")]
    public class OpCaseSheetDiagnosisModel
    {
        [ExplicitKey]
        public Guid diag_id { get; set; } = Guid.NewGuid();
        public Guid sheet_id { get; set; }
        public Guid op_id { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public DateTime visit_date { get; set; }


        public int sno { get; set; }


        // ICD-10
        public string? icd_code { get; set; }
        public string? icd_description { get; set; }
        public string? diagnosis_text { get; set; }


        // Classification
        public string? diagnosis_type { get; set; }   // PRIMARY / SECONDARY / COMORBIDITY
        public string? condition_type { get; set; }   // ACUTE / CHRONIC / FOLLOWUP
        public string? severity { get; set; }         // MILD / MODERATE / SEVERE
        public string status { get; set; } = "ACTIVE"; // ACTIVE / RESOLVED / ONGOING


        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    [Table("op_case_sheet_symptoms")]
    public class OpCaseSheetSymptomModel
    {
        [ExplicitKey]
        public Guid symptom_id { get; set; } = Guid.NewGuid();
        public Guid sheet_id { get; set; }
        public Guid op_id { get; set; }
        public decimal custid { get; set; }
        public int sno { get; set; }


        public string symptom_text { get; set; } = string.Empty;
        public string? duration { get; set; }
        public string? severity { get; set; }  // MILD / MODERATE / SEVERE
        public string? notes { get; set; }


        public string? tenant_code { get; set; }
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    [Table("op_prescription_master")]
    public class OpPrescriptionMasterModel
    {
        [ExplicitKey]
        public Guid pr_id { get; set; } = Guid.NewGuid();
        public string pr_code { get; set; } = string.Empty;   // PR/2026/06/0001
        public Guid? sheet_id { get; set; }
        public Guid op_id { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public DateTime visit_date { get; set; }


        public DateTime pr_date { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public string? topremarks { get; set; }
        public string? bottonremarks { get; set; }
        public bool is_dispensed { get; set; } = false;


        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    [Table("op_prescription_detail")]
    public class OpPrescriptionDetailModel
    {
        [ExplicitKey]
        public Guid pr_det_id { get; set; } = Guid.NewGuid();
        public Guid pr_id { get; set; }
        public string pr_code { get; set; } = string.Empty;
        public Guid? diag_id { get; set; }
        public int sno { get; set; }


        // Drug info
        public string drug_name { get; set; } = string.Empty;
        public decimal? drug_code { get; set; }
        public string? generic_name { get; set; }
        public string? drug_category { get; set; }


        // Dosage
        public string morning { get; set; } = "0";
        public string afternoon { get; set; } = "0";
        public string evening { get; set; } = "0";
        public string night { get; set; } = "0";
        public bool before_food { get; set; } = false;
        public bool after_food { get; set; } = false;
        public int? days { get; set; }
        public decimal? qty { get; set; }
        public string? route { get; set; }   // ORAL / IV / IM / TOPICAL


        // Billing
        public decimal? rate { get; set; }
        public decimal? mrp { get; set; }
        public bool is_billed { get; set; } = false;


        public string? notes { get; set; }
        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    [Table("op_investigation_master")]
    public class OpInvestigationMasterModel
    {
        [ExplicitKey]
        public Guid inv_id { get; set; } = Guid.NewGuid();
        public string inv_code { get; set; } = string.Empty;   // INV/2026/06/0001
        public Guid? sheet_id { get; set; }
        public Guid op_id { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public DateTime visit_date { get; set; }


        public DateTime inv_date { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public string? notes { get; set; }
        public bool is_urgent { get; set; } = false;
        public string status { get; set; } = "ORDERED";  // ORDERED / PARTIAL / COMPLETED


        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    [Table("op_investigation_detail")]
    public class OpInvestigationDetailModel
    {
        [ExplicitKey]
        public Guid inv_det_id { get; set; } = Guid.NewGuid();
        public Guid inv_id { get; set; }
        public Guid? diag_id { get; set; }
        public int sno { get; set; }


        public string test_name { get; set; } = string.Empty;
        public int? test_code { get; set; }
        public string? test_category { get; set; }


        public decimal quantity { get; set; } = 1;
        public decimal? rate { get; set; }
        public decimal? amount { get; set; }


        // Result tracking
        public string result_status { get; set; } = "PENDING";  // PENDING / COMPLETED
        public string? result_value { get; set; }
        public DateTime? result_date { get; set; }
        public string? result_notes { get; set; }


        public bool is_billed { get; set; } = false;
        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }


    // ─────────────────────────────────────────────────────────────
    // REQUEST SHAPES
    // ─────────────────────────────────────────────────────────────


    /// <summary>
    /// Save the full OP Case Sheet in one call.
    /// Vitals are NOT included here — use the existing
    /// POST api/OpRegistration/save-vitals endpoint.
    /// </summary>
    public class SaveCaseSheetRequest
    {
        // Visit identifiers
        public string op_id { get; set; } = string.Empty;
        public decimal custid { get; set; }
        public int dcode { get; set; }


        // null = new sheet; provide sheet_id to update existing
        public string? sheet_id { get; set; }


        // Clinical notes (free text)
        public string? chief_complaint { get; set; }
        public string? symptoms { get; set; }        // free-text paragraph (legacy support)
        public string? examination { get; set; }
        public string? advise { get; set; }
        public string? notes { get; set; }


        // Follow up
        public DateTime? followup_date { get; set; }
        public string? followup_notes { get; set; }


        // Sheet status
        public string sheet_status { get; set; } = "DRAFT";  // DRAFT / FINAL


        // Structured symptoms list
        public List<CaseSheetSymptomItem> symptom_list { get; set; } = new();


        // Structured diagnosis list
        public List<CaseSheetDiagnosisItem> diagnosis_list { get; set; } = new();


        // Prescription
        public CaseSheetPrescriptionRequest? prescription { get; set; }


        // Investigation
        public CaseSheetInvestigationRequest? investigation { get; set; }
    }


    public class CaseSheetSymptomItem
    {
        public int sno { get; set; }
        public string symptom_text { get; set; } = string.Empty;
        public string? duration { get; set; }        // "3 days", "1 week"
        public string? severity { get; set; }        // MILD / MODERATE / SEVERE
        public string? notes { get; set; }
    }


    public class CaseSheetDiagnosisItem
    {
        public int sno { get; set; }
        public string? icd_code { get; set; }
        public string? icd_description { get; set; }
        public string? diagnosis_text { get; set; }
        public string? diagnosis_type { get; set; }  // PRIMARY / SECONDARY / COMORBIDITY
        public string? condition_type { get; set; }  // ACUTE / CHRONIC / FOLLOWUP
        public string? severity { get; set; }        // MILD / MODERATE / SEVERE
        public string status { get; set; } = "ACTIVE";
    }


    public class CaseSheetPrescriptionRequest
    {
        /// <summary>null = new. Provide pr_code to update existing.</summary>
        public string? pr_code { get; set; }
        public string? topremarks { get; set; }
        public string? bottonremarks { get; set; }
        public List<CaseSheetPrescriptionItem> items { get; set; } = new();
    }


    public class CaseSheetPrescriptionItem
    {
        public int sno { get; set; }
        public string? diag_id { get; set; }         // link to specific diagnosis
        public string drug_name { get; set; } = string.Empty;
        public decimal? drug_code { get; set; }
        public string? generic_name { get; set; }
        public string? drug_category { get; set; }
        public string morning { get; set; } = "0";
        public string afternoon { get; set; } = "0";
        public string evening { get; set; } = "0";
        public string night { get; set; } = "0";
        public bool before_food { get; set; } = false;
        public bool after_food { get; set; } = false;
        public int? days { get; set; }
        public decimal? qty { get; set; }
        public string? route { get; set; }
        public decimal? rate { get; set; }
        public decimal? mrp { get; set; }
        public string? notes { get; set; }
    }


    public class CaseSheetInvestigationRequest
    {
        /// <summary>null = new. Provide inv_id to update existing.</summary>
        public string? inv_id { get; set; }
        public string? notes { get; set; }
        public bool is_urgent { get; set; } = false;
        public List<CaseSheetInvestigationItem> tests { get; set; } = new();
    }


    public class CaseSheetInvestigationItem
    {
        public int sno { get; set; }
        public string? diag_id { get; set; }         // link to specific diagnosis
        public string test_name { get; set; } = string.Empty;
        public int? test_code { get; set; }
        public string? test_category { get; set; }
        public decimal quantity { get; set; } = 1;
        public decimal? rate { get; set; }
        public decimal? amount { get; set; }
    }


    // ─────────────────────────────────────────────────────────────
    // FINALIZE REQUEST
    // ─────────────────────────────────────────────────────────────
    public class FinalizeCaseSheetRequest
    {
        public string sheet_id { get; set; } = string.Empty;
        public bool is_consulted { get; set; } = true;
    }


    // ─────────────────────────────────────────────────────────────
    // UPDATE RESULT REQUEST
    // ─────────────────────────────────────────────────────────────
    public class UpdateInvestigationResultRequest
    {
        public string inv_det_id { get; set; } = string.Empty;
        public string? result_value { get; set; }
        public string? result_notes { get; set; }
        public string result_status { get; set; } = "COMPLETED";
    }


    // ─────────────────────────────────────────────────────────────
    // RESPONSE / VIEW SHAPES
    // ─────────────────────────────────────────────────────────────


    public class CaseSheetViewModel
    {
        public string? sheet_id { get; set; }
        public string? op_id { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public DateTime? visit_date { get; set; }


        // Clinical notes
        public string? chief_complaint { get; set; }
        public string? symptoms { get; set; }
        public string? examination { get; set; }
        public string? advise { get; set; }
        public string? notes { get; set; }


        // Follow up
        public DateTime? followup_date { get; set; }
        public string? followup_notes { get; set; }


        // Status
        public bool is_consulted { get; set; }
        public string? sheet_status { get; set; }


        // Structured lists
        public List<OpCaseSheetSymptomModel> symptom_list { get; set; } = new();
        public List<OpCaseSheetDiagnosisModel> diagnosis_list { get; set; } = new();


        // Prescription
        public CaseSheetPrescriptionViewModel? prescription { get; set; }


        // Investigation
        public CaseSheetInvestigationViewModel? investigation { get; set; }
    }


    public class CaseSheetPrescriptionViewModel
    {
        public string? pr_id { get; set; }
        public string? pr_code { get; set; }
        public DateTime? pr_date { get; set; }
        public string? topremarks { get; set; }
        public string? bottonremarks { get; set; }
        public bool is_dispensed { get; set; }
        public List<OpPrescriptionDetailModel> items { get; set; } = new();
    }


    public class CaseSheetInvestigationViewModel
    {
        public string? inv_id { get; set; }
        public string? inv_code { get; set; }
        public DateTime? inv_date { get; set; }
        public string? notes { get; set; }
        public bool is_urgent { get; set; }
        public string? status { get; set; }
        public List<OpInvestigationDetailModel> tests { get; set; } = new();
    }
}
