using Dapper.Contrib.Extensions;
using medico_backend.InventoryModel;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace medico_backend.Model
{
    // ─────────────────────────────────────────────────────────────────────────
    //  TABLE-MAPPED ENTITIES  (properties match PostgreSQL column names)
    // ─────────────────────────────────────────────────────────────────────────

    [Table("lab_result_master")]
    public class lab_result_master
    {
        [Key]
        public long recode { get; set; }
        public Guid? resultguid { get; set; }
        public int? resultsno { get; set; }
        public string? resultbarcode { get; set; }
        public string? resultconvertedbarcode { get; set; }
        public DateTime? resultdatetime { get; set; }
        public Guid? requestguid { get; set; }
        public string? resultsms { get; set; }
        public string? description { get; set; }
        public bool? approvalstatus { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("lab_result_properties")]
    public class LabResultPropertiesModel
    {
        [ExplicitKey]
        public Guid trpid { get; set; }
        public Guid? testresultid { get; set; }
        public string? resultvaluetype { get; set; }
        public int? defaultunitscode { get; set; }
        public Guid? fxtcode { get; set; }
        public Guid? defaultvalueforfxtype { get; set; }
        public string? defaultvalue { get; set; }
        public bool? simplenormalvalues { get; set; }
        public bool? detailednormalvalues { get; set; }
        public string? rangetype { get; set; }
        public double? fromnormalvalue { get; set; }
        public double? tonormalvalue { get; set; }
        public string? conclusionforhigher { get; set; }
        public string? conclusionforlower { get; set; }
        public bool? printfixedtextconclusioninreport { get; set; }
        public string? conclusionforfixedtext { get; set; }
        public bool? showagedbased { get; set; }
        public bool? printconclusioninreport { get; set; }
        public bool? printconclusioninbottom { get; set; }
        public bool? showalertonhigherlower { get; set; }
        public bool? isaddresult { get; set; }
        public bool? printunitsinnormalvalues { get; set; }
        public bool? printnormalvaluesatbottom { get; set; }
        public bool? printspecialfieldsatrightside { get; set; }
        public bool? groupvaluesbysex { get; set; }
        public bool? groupvaluesbyspecialfield { get; set; }
        public string? footermessage { get; set; }
        public int? rtmcode { get; set; }
        public bool? printresultonly { get; set; }
        public bool? resultnormal { get; set; }
        public bool? resulthigh { get; set; }
        public bool? resultlow { get; set; }
        public bool? isgraph { get; set; }
        public double? graphvalue { get; set; }
        public int? decimalvalue { get; set; }
        public int? scode { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public bool? usedefault { get; set; }
        public Guid? normalvalueforfxtype { get; set; }
        public string? normalvalue { get; set; }
        public Guid? mastertestresultid { get; set; }
        public string? criticallowtype { get; set; }
        public string? criticallowrange { get; set; }
        public string? criticalhightype { get; set; }
        public string? criticalhighrange { get; set; }
        public string? tenant_code { get; set; }

        // Proof image attached during result entry
        public string? image_path { get; set; }
    }

    [Table("lab_result_details")]
    public class LabResultDetailsModel
    {
        [ExplicitKey]
        public Guid lrdid { get; set; }
        public Guid? resultguid { get; set; }
        public Guid? testresultid { get; set; }
        public bool? sendsms { get; set; }
        public string? smsshortname { get; set; }
        public int? tcode { get; set; }
        public int? testsno { get; set; }
        public string? description { get; set; }
        public string? quotescolumn { get; set; }
        public string? enteredresult { get; set; }
        public string? units { get; set; }
        public string? normalvalues { get; set; }
        public string? defaultnormalvalues { get; set; }
        public int? dstylecode { get; set; }
        public int? qstylecode { get; set; }
        public int? estylecode { get; set; }
        public int? ustylecode { get; set; }
        public int? nstylecode { get; set; }
        public string? valuetype { get; set; }
        public string? resulttype { get; set; }
        public string? calculatedformula { get; set; }
        public int? fromtcode { get; set; }
        public Guid? fromtestresultid { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("lab_result_detailednormalvalues")]
    public class LabResultDetailedNormalValuesModel
    {
        [ExplicitKey]
        public Guid trdnid { get; set; }
        public Guid? testresultid { get; set; }
        public int? sno { get; set; }
        public int? agefrom { get; set; }
        public string? agefromtype { get; set; }
        public int? ageto { get; set; }
        public string? agetotype { get; set; }
        public string? sex { get; set; }
        public string? rangetype { get; set; }
        public double? rangefrom { get; set; }
        public double? rangeto { get; set; }
        public Guid? specialconditioncode { get; set; }
        public string? agerangetype { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("lab_result_textnormalvalues")]
    public class LabResultTextNormalValuesModel
    {
        [ExplicitKey]
        public Guid trtid { get; set; }
        public Guid? testresultid { get; set; }
        public string? sex { get; set; }
        public string? normalvalue { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("lab_result_calculatedformula")]
    public class LabResultCalculatedFormulaModel
    {
        [ExplicitKey]
        public Guid trcfid { get; set; }
        public Guid? testresultid { get; set; }
        public string? sex { get; set; }
        public string? calculatedformula { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    public class LabResultEntry
    {
        public string requestguid { get; set; } = string.Empty;
        public int slno { get; set; }
        public int tcode { get; set; }
        public string? testname { get; set; }
        public int orderno { get; set; }
        public int grouporder { get; set; }
        public int gcode { get; set; }
        public string? groupname { get; set; }
        public string? col2 { get; set; }
        public string? enteredresult { get; set; }
        public string? resultvaluetype { get; set; }
        public string? normalvalue { get; set; }
        public string? resulttype { get; set; }
        public Guid testresultid { get; set; }
        public bool status { get; set; }
        public Guid defaultvalueforfxtype { get; set; }
        public Guid fxtcode { get; set; }
        public int fromtcode { get; set; }
        public Guid fromtestresultid { get; set; }
        public bool resultnormal { get; set; }
        public bool resulthigh { get; set; }
        public bool resultlow { get; set; }
        public bool simplenv { get; set; }
        public bool detailednv { get; set; }
        public string? calculatedformula { get; set; }
        public int defaultunitscode { get; set; }
        public string? defaultunitvalue { get; set; }
        public string? unitname { get; set; }
        public int mccode { get; set; }
        public string? machinename { get; set; }
        public int scode { get; set; }
        public string? samplename { get; set; }
        public int rtmcode { get; set; }
        public string? fixedvalues { get; set; }
        public int resultenteredby { get; set; }
        public bool isauthorized1 { get; set; }
        public bool isauthorized2 { get; set; }
        public int resultauthorizedby { get; set; }
        public int resultauthorizedby2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<test_result_properties>? testproperties { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<LabResultPropertiesModel>? labproperties { get; set; }
        public List<LabResultTextNormalValuesModel>? textnormalvalues { get; set; }

        public List<LabResultDetailedNormalValuesModel>? detailednormalvalues { get; set; }
        public List<LabResultCalculatedFormulaModel>? calculatedformulas { get; set; }

        // Proof image — attached during result entry; uploaded to MinIO inside SaveResult.
        // Ignored during JSON serialization (not sent back to client).
        [JsonIgnore]
        public IFormFile? image_file { get; set; }
    }

    public class ViewResultSearch
    {
        public int requestsno { get; set; }
        public DateTimeOffset requestdatetime { get; set; }
        public string? custcode { get; set; }
        public string? name { get; set; }
        public string? doctor { get; set; }

        public decimal requestamount { get; set; }
        public decimal discountper { get; set; }
        public decimal discountamount { get; set; }
        public decimal totalamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balance { get; set; }

        public string requestguid { get; set; }

        // Request-level type flags (same value on every test row for same request)
        public bool? isinvestigation { get; set; }
        public bool? isculture { get; set; }
        public bool? istextreport { get; set; }

        // Per-test columns (vary row to row)
        public string? tcode { get; set; }
        public int? ttid { get; set; }        // 1=Investigation, 2=Culture, 4=TextReport
        public bool? resultstatus { get; set; }
        public bool? isprinted { get; set; }
        public bool? isauthorized1 { get; set; }
        public bool? isauthorized2 { get; set; }
        public bool? isworklistprinted { get; set; }

        public int? gcode { get; set; }
        public string? groupname { get; set; }
        public string? testname { get; set; }

        public bool? isscan { get; set; }
        public bool? islab { get; set; }

        public int? enteredbhcode { get; set; }
        public int? alteredbhcode { get; set; }
        public long? bncode { get; set; }

        public int? billno { get; set; }
        public string? billnoprint { get; set; }
        public int? billbncode { get; set; }

        public int? dcode { get; set; }
        public string? mobile { get; set; }
        public string? doctorfullname { get; set; }

        public bool? requestresultstatus { get; set; }
        public bool? isinvestigationauthorized1 { get; set; }
        public bool? isinvestigationauthorized2 { get; set; }
        public bool? isinvestigationprinted1 { get; set; }

        public string? pathologyno { get; set; }
        public string? onlinecode { get; set; }
        public string? onlinepassword { get; set; }
        public string? isothers { get; set; }
        public string? hospitalid { get; set; }
        public string? hospitalpatientid { get; set; }
        public bool? issendsms { get; set; }

        public string? address { get; set; }
        public string? area { get; set; }
        public string? city { get; set; }
        public string? zipcode { get; set; }
        public string? state { get; set; }
        public string? tenant_code { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RESPONSE MODELS
    // ─────────────────────────────────────────────────────────────────────────

    public class ResultEntryModel
    {
        public IList<LabResultEntry> results { get; set; } = new List<LabResultEntry>();
        public IList<UomMasterModel> units { get; set; } = new List<UomMasterModel>();
        public IList<SampleMasterModel> samples { get; set; } = new List<SampleMasterModel>();
        public IList<MachineMasterModel> machines { get; set; } = new List<MachineMasterModel>();
        public IList<ReportMethodModel> methods { get; set; } = new List<ReportMethodModel>();
    }

    public class TestResultDetailsModel
    {
        public bool isresulted { get; set; }

        // Populated when isresulted = false  →  master tables (test_result_*)
        public IList<test_result_properties> properties { get; set; } = new List<test_result_properties>();
        public IList<test_result_textnormalvalues> textnormalvalues { get; set; } = new List<test_result_textnormalvalues>();
        public IList<test_result_detailednormalvalues> detailedNormalvalues { get; set; } = new List<test_result_detailednormalvalues>();
        public IList<TestResultCalculatedformula> calculatedformulas { get; set; } = new List<TestResultCalculatedformula>();

        // Populated when isresulted = true   →  result copy tables (lab_result_*)
        public IList<LabResultPropertiesModel> labproperties { get; set; } = new List<LabResultPropertiesModel>();
        public IList<LabResultTextNormalValuesModel> labtextnormalvalues { get; set; } = new List<LabResultTextNormalValuesModel>();
        public IList<LabResultDetailedNormalValuesModel> labdetailedNormalvalues { get; set; } = new List<LabResultDetailedNormalValuesModel>();
        public IList<LabResultCalculatedFormulaModel> labcalculatedformulas { get; set; } = new List<LabResultCalculatedFormulaModel>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  REQUEST / SAVE DTOs
    // ─────────────────────────────────────────────────────────────────────────

    public class LabResultInsertDto
    {
        public lab_result_master lab_result_master { get; set; } = new();
        public LabResultPropertiesModel lab_result_properties { get; set; } = new();
        public LabResultDetailsModel lab_result_details { get; set; } = new();
        public LabResultCalculatedFormulaModel lab_result_calculatedformula { get; set; } = new();
    }

    public class LabResultSaveRequest
    {
        public HmsLabRequestMaster lab_result_master { get; set; } = new();
        public List<LabResultPropertiesModel> lab_result_properties { get; set; } = new();
        public List<LabResultDetailsModel> lab_result_details { get; set; } = new();
        public List<LabResultTextNormalValuesModel> lab_result_textnormalvalues { get; set; } = new();
        public List<LabResultDetailedNormalValuesModel> lab_result_detailedNormalvalues { get; set; } = new();
        public List<LabResultCalculatedFormulaModel> lab_result_calculatedformula { get; set; } = new();
    }

    public class ApproveResultRequest
    {
        public Guid requestguid { get; set; }
        public List<int> tcodes { get; set; } = new();
        public int resultauthorizedby { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  COMPOSITE / LEGACY MODELS  (kept for backward-compat)
    // ─────────────────────────────────────────────────────────────────────────

    public class LabResultData
    {
        public lab_result_master lab_result_master { get; set; } = new();
        public IList<MasterLabResultData> masterlab_result_datas { get; set; } = new List<MasterLabResultData>();
    }

    public class MasterLabResultData
    {
        public LabResultEntry lab_result_entry { get; set; } = new();
        public List<LabResultDetailsModel> lab_result_details { get; set; } = new();
        public List<Master_Result_Properties> master_result_properties { get; set; } = new();
        public List<test_result_textnormalvalues> master_result_textnormalvalues { get; set; } = new();
        public List<test_result_detailednormalvalues> master_result_detailedNormalvalues { get; set; } = new();
        public List<TestResultCalculatedformula> master_result_calculatedformula { get; set; } = new();
    }

    public class Master_Result_Properties
    {
        public Guid trpid { get; set; }
        public Guid testresultid { get; set; }
        public string resultvaluetype { get; set; } = string.Empty;
        public int defaultunitscode { get; set; }
        public Guid fxtcode { get; set; }
        public Guid defaultvalueforfxtype { get; set; }
        public string defaultvalue { get; set; } = string.Empty;
        public bool simplenormalvalues { get; set; }
        public bool detailedNormalvalues { get; set; }
        public string rangetype { get; set; } = string.Empty;
        public float fromnormalvalue { get; set; }
        public float tonormalvalue { get; set; }
        public string conclusionforhigher { get; set; } = string.Empty;
        public string conclusionforlower { get; set; } = string.Empty;
        public bool printfixedtextconclusioninreport { get; set; }
        public string conclusionforfixedtext { get; set; } = string.Empty;
        public bool showagedbased { get; set; }
        public bool printconclusioninreport { get; set; }
        public bool printconclusioninbottom { get; set; }
        public bool showalertonhigherlower { get; set; }
        public bool isaddresult { get; set; }
        public bool printunitsinnormalvalues { get; set; }
        public bool printnormalvaluesatbottom { get; set; }
        public bool printspecialfieldsatrightside { get; set; }
        public bool groupvaluesbysex { get; set; }
        public bool groupvaluesbyspecialfield { get; set; }
        public string footermessage { get; set; } = string.Empty;
        public int rtmcode { get; set; }
        public bool printresultonly { get; set; }
        public bool isgraph { get; set; }
        public float graphvalue { get; set; }
        public int decimalvalue { get; set; }
        public int scode { get; set; }
        public DateTime entereddate { get; set; }
        public int mccode { get; set; }
        public int performedcount { get; set; }
        public bool usedefault { get; set; }
        public Guid normalvalueforfxtype { get; set; }
        public string normalvalue { get; set; } = string.Empty;
        public bool isabnormal { get; set; }
        public string criticallowtype { get; set; } = string.Empty;
        public string criticallowrange { get; set; } = string.Empty;
        public string criticalhightype { get; set; } = string.Empty;
        public string criticalhighrange { get; set; } = string.Empty;
        public bool resultnormal { get; set; }
        public bool resulthigh { get; set; }
        public bool resultlow { get; set; }
        public Guid mastertestresultid { get; set; }
    }

    public class CustomerResultDto
    {
        public string requestguid { get; set; }
        public DateTime date { get; set; }
        public string TestName { get; set; }
        public string Result { get; set; }
    }

    public class ResultImageUpload
    {
        public Guid testresultid { get; set; }
        public IFormFile? image_file { get; set; }
    }
}