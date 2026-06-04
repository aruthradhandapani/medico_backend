using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    // ─────────────────────────────────────────────────────────────
    // DB TABLE MODELS — exact column names from your PostgreSQL tables
    // All IDs are text (stored as UUID strings)
    // ─────────────────────────────────────────────────────────────

    [Table("patientproblem")]
    public class PatientProblemModel
    {
        [ExplicitKey]
        public string? problemid { get; set; }
        public DateTime? problemdate { get; set; }
        public int? invstno { get; set; }
        public string? invstnoprint { get; set; }
        public string? invstnobarcode { get; set; }
        public string? invstnoconvertedbarcode { get; set; }
        public decimal? hdcode { get; set; }
        public decimal? bncode { get; set; }
        public decimal? custid { get; set; }
        public int? dcode { get; set; }
        public string? opvisitid { get; set; }      // → op_registration.op_id (text)
        public string? notes { get; set; }
        public bool? deleted { get; set; } = false;
        public int? enteredbhcode { get; set; }
        public string? problemtype { get; set; }    // OPCASE | VITAL | FOLLOWUP

        // ── Added for tenant support (ALTER TABLE needed — see note) ──
        public string? tenant_code { get; set; }

        // ── Not in DB — populated in response only ────────────────────
        [Computed] public List<ProblemCodeItem>? problems { get; set; }
        [Computed] public List<DiagnosisCodeItem>? diagnoses { get; set; }
        [Computed] public List<SymptomAnswerItem>? symptoms { get; set; }
    }

    [Table("patientproblem_problem")]
    public class PatientProblemProblemModel
    {
        [ExplicitKey]
        public string? ppid { get; set; }
        public string? problemid { get; set; }
        public int? sno { get; set; }
        public int? pbcode { get; set; }
    }

    [Table("patientproblem_diagnosis")]
    public class PatientProblemDiagnosisModel
    {
        [ExplicitKey]
        public string? ppdid { get; set; }
        public string? problemid { get; set; }
        public int? sno { get; set; }
        public int? dccode { get; set; }
    }

    [Table("patientproblem_symptoms")]
    public class PatientProblemSymptomModel
    {
        [ExplicitKey]
        public string? pbsid { get; set; }
        public string? problemid { get; set; }
        public string? prmid { get; set; }
        public decimal slno { get; set; }
        public int pbcode { get; set; }
        public string? resultvaluetype { get; set; }
        public string? question { get; set; }
        public string? answer { get; set; }
        public string? answerprbpid { get; set; }
        public int? frompbcode { get; set; }
        public string? fromquestionid { get; set; }
        public bool? iscombineanswer { get; set; }
        public string? resultvaluetype1 { get; set; }
        public string? blockrtf { get; set; }
    }

    [Table("patientproblem_symptompossibilities")]
    public class PatientProblemSymptomPossibilityModel
    {
        [ExplicitKey]
        public string? pbspid { get; set; }
        public string? problemid { get; set; }
        public int? pbcode { get; set; }
        public string? prbpid { get; set; }
        public string? prmid { get; set; }
        public int? sno { get; set; }
        public string? possibility { get; set; }
        // Note: "Type" column has capital T in DB — mapped via Dapper column alias in queries
        public string? type { get; set; }
        public string? typetext { get; set; }
        public string? sympsno { get; set; }
        public bool? isselected { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    // MASTER TABLES — still needed for template & problem list
    // Share these table names when ready, for now using placeholders
    // that match the SQL Server naming pattern converted to lowercase
    // ─────────────────────────────────────────────────────────────

    [Table("problem_master")]
    public class ProblemMasterModel
    {
        [ExplicitKey]
        public int pbcode { get; set; }
        public int orderNo { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public int? pcgcode { get; set; }
        public string? description { get; set; }
        public bool? ismultiple { get; set; }
        public bool deleted { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("problem_report_master")]
    public class ProblemReportMasterModel
    {
        [ExplicitKey]
        public string? prmid { get; set; }
        public decimal slno { get; set; }
        public int pbcode { get; set; }
        public string? resultvaluetype { get; set; }
        public string? question { get; set; }
        public int? frompbcode { get; set; }
        public string? fromquestionid { get; set; }
        public bool? iscombineanswer { get; set; }
        public string? resultvaluetype1 { get; set; }
        public string? blockrtf { get; set; }
        public string? tenant_code { get; set; }

        // Loaded alongside — not a DB column
        [Computed]
        public List<ProblemReportMasterPossibilityModel>? possibilities { get; set; }
    }

    [Table("problem_report_master_possibilities")]
    public class ProblemReportMasterPossibilityModel
    {
        [ExplicitKey]
        public string? prbpid { get; set; }
        public string? prmid { get; set; }
        public int? sno { get; set; }
        public string? possibility { get; set; }
        public int? pbcode { get; set; }
        public string? type { get; set; }
        public string? typetext { get; set; }
        public string? sympsno { get; set; }
        public string? tenant_code { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    // REQUEST / RESPONSE SHAPES
    // ─────────────────────────────────────────────────────────────

    public class ProblemCodeItem
    {
        public int pbcode { get; set; }
    }

    public class DiagnosisCodeItem
    {
        public int dccode { get; set; }
    }

    /// <summary>
    /// One Q&A row — same shape as ProblemReportMasterModel so the
    /// frontend loads template, fills answers, and POSTs the same list back.
    /// </summary>
    public class SymptomAnswerItem
    {
        public string? prmid { get; set; }
        public decimal slno { get; set; }
        public int pbcode { get; set; }
        public string? resultvaluetype { get; set; }
        public string? question { get; set; }
        public string? answer { get; set; }
        public string? answerprbpid { get; set; }
        public int? frompbcode { get; set; }
        public string? fromquestionid { get; set; }
        public bool? iscombineanswer { get; set; }
        public string? resultvaluetype1 { get; set; }
        public string? blockrtf { get; set; }

        /// <summary>For Selection / Multiple Selection questions only</summary>
        public List<SymptomPossibilityItem>? selected_possibilities { get; set; }
    }

    public class SymptomPossibilityItem
    {
        public string? prbpid { get; set; }
        public int? sno { get; set; }
        public string? possibility { get; set; }
        public string? type { get; set; }
        public string? typetext { get; set; }
        public string? sympsno { get; set; }
        public bool isselected { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    // SAVE REQUEST — single payload sent from frontend
    // ─────────────────────────────────────────────────────────────
    public class SaveEMRRequest
    {
        /// <summary>Null = new record. Provide to update existing.</summary>
        public string? problemid { get; set; }

        public string opvisitid { get; set; } = string.Empty;  // op_registration.op_id
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public decimal? hdcode { get; set; }
        public string? notes { get; set; }
        public string? problemtype { get; set; }   // OPCASE | VITAL | FOLLOWUP

        public List<ProblemCodeItem> problems { get; set; } = new();
        public List<DiagnosisCodeItem> diagnoses { get; set; } = new();
        public List<SymptomAnswerItem> symptoms { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────
    // CASE HISTORY RESPONSE — flat list for history panel
    // ─────────────────────────────────────────────────────────────
    public class CaseHistoryItem
    {
        public string? problemid { get; set; }
        public DateTime? problemdate { get; set; }
        public string? invstnoprint { get; set; }
        public string? problemname { get; set; }    // from problem_master.name
        public string? question { get; set; }
        public string? answer { get; set; }
        public string? resultvaluetype { get; set; }
        public int? pbcode { get; set; }
        public decimal? slno { get; set; }
    }
}