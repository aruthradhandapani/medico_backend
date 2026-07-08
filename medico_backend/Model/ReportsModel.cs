namespace medico_backend.Model
{
    public class StatementsModel
    {
        public int sampleid { get; set; }
        public DateTime date { get; set; }
        public string custcode { get; set; }
        public string patientname { get; set; }
        public string? mobile { get; set; }
        public string? referral { get; set; }
        public decimal? billedamount { get; set; }
        public string? netamount { get; set; }
        public string? paidamount { get; set; }
        public string? balanceamount { get; set; }
        public string? discountamount { get; set; }
    }

    public class StatementRequest
    {
        public IList<StatementsModel>? statements { get; set; }
        public byte[]? LogoImage { get; set; }
        //Company Details
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? statementtype { get; set; }
    }

    public class SummaryModel
    {
        public DateTime date { get; set; }
        public long? billed { get; set; }
        public decimal? billedamount { get; set; }
        public decimal? paid { get; set; }
        public decimal? balance { get; set; }
        public decimal? discount { get; set; }
        public decimal? netamount { get; set; }
    }

    public class SummaryRequest
    {
        public IList<SummaryModel>? summary { get; set; }
        public byte[]? LogoImage { get; set; }
        //Company Details
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
        public string? summarytype { get; set; }
    }

    public class DueSummaryModel
    {
        public DateTime date { get; set; }
        public long? billed { get; set; }
        public decimal? billedamount { get; set; }
        public decimal? paid { get; set; }
        public decimal? balance { get; set; }
        public decimal? discount { get; set; }
        public decimal? netamount { get; set; }
    }

    public class DueSummaryRequest
    {
        public IList<DueSummaryModel>? summary { get; set; }
        public byte[]? LogoImage { get; set; }
        //Company Details
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
    }

    public class DiscountSummaryModel
    {
        public DateTime date { get; set; }
        public long? billed { get; set; }
        public decimal? billedamount { get; set; }
        public decimal? paid { get; set; }
        public decimal? balance { get; set; }
        public decimal? discount { get; set; }
        public decimal? netamount { get; set; }
    }

    public class DiscountSummaryRequest
    {
        public IList<DiscountSummaryModel>? summary { get; set; }
        public byte[]? LogoImage { get; set; }
        //Company Details
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
    }

    public class ReferralStatementModel
    {
        public DateTime date { get; set; }
        public string? referral { get; set; }
        public string? patientid { get; set; }
        public string? patientname { get; set; }
        public string? testname { get; set; }
        public decimal price { get; set; }
        public string? requestguid { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
        public string? mobile { get; set; }
        public string? custcode { get; set; }
    }

    public class ReferralStatementRequest
    {
        public IList<ReferralStatementModel>? statements { get; set; }
        public byte[]? LogoImage { get; set; }
        //Company Details
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
    }

    public class GroupStatementModel
    {
        public DateTime date { get; set; }
        public string? groupname { get; set; }
        public string? patientid { get; set; }
        public string? patientname { get; set; }
        public string? testname { get; set; }
        public decimal price { get; set; }
        public string? requestguid { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
        public string? mobile { get; set; }
        public string? referral { get; set; }
        public string? custcode { get; set; }
    }

    public class GroupStatementRequest
    {
        public IList<GroupStatementModel>? statements { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
    }

    public class GroupSummaryModel
    {
        public string? groupname { get; set; }
        public long billed { get; set; }
        public decimal billedamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
    }

    public class GroupSummaryRequest
    {
        public IList<GroupSummaryModel>? summary { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
    }

    public class TestStatementModel
    {
        public DateTime date { get; set; }
        public string? testname { get; set; }
        public string? patientid { get; set; }
        public string? patientname { get; set; }
        public decimal price { get; set; }
        public string? requestguid { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
        public string? mobile { get; set; }
        public string? referral { get; set; }
        public string? custcode { get; set; }
    }

    public class TestStatementRequest
    {
        public IList<TestStatementModel>? statements { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
    }

    public class TestSummaryModel
    {
        public string? testname { get; set; }
        public long billed { get; set; }
        public decimal billedamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
    }

    public class TestSummaryRequest
    {
        public IList<TestSummaryModel>? summary { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
    }

    public class ReferralSummaryModel
    {
        public string? referral { get; set; }
        public long billed { get; set; }
        public decimal billedamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
    }

    public class ReferralSummaryRequest
    {
        public IList<ReferralSummaryModel>? summary { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
    }

    public class BillModel
    {
        public string TestName { get; set; }

        public decimal Amount { get; set; }
    }
    public class BillReceiptData
    {
        public string CustomerName { get; set; }

        public string InvoiceNo { get; set; }

        public decimal BillAmount { get; set; }

        public DateTime BillDate { get; set; }
    }

    public class CashBillModel
    {

        public string TenantId { get; set; }
        public byte[]? logo { get; set; }
        public byte[]? headerimage { get; set; }
        public byte[]? footerimage { get; set; }

        public bool isletterhead { get; set; } = false;
        // Header
        public string? LabName { get; set; }
        public string? BranchName { get; set; }
        public string? Address { get; set; }
        public string? MobileNo { get; set; }
        public string? ContactNo { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? HelplineNo { get; set; }

        // Bill Info
        public string BillNo { get; set; }
        public string? RequestSnoPrint { get; set; }
        public DateTime BillDate { get; set; }

        // Patient Info

        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string Age { get; set; }
        public string Gender { get; set; }
        public string PatientAddress { get; set; }
        public string CusMobileNo { get; set; }
        public string CareOf { get; set; }
        public string DoctorName { get; set; }

        // Online Info
        public string OnlineCode { get; set; }
        public string Password { get; set; }

        // Footer
        public string CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal BalanceAmount { get; set; }

        public string? billauthorizedby { get; set; }
        public byte[]? billauthorizesignature { get; set; }


        // Test Details
        public IList<CashBillItemModel> Items { get; set; } = new List<CashBillItemModel>();
    }

    public class CashBillItemModel
    {
        public int SNo { get; set; }

        public string TestName { get; set; }

        public decimal Amount { get; set; }

        public string? GroupName { get; set; }

        public IList<string>? SubParameters { get; set; }

        public int TCode { get; set; }
    }

    public class WorklistRequestModel
    {
        public IList<CashBillModel> Bills { get; set; } = new List<CashBillModel>();
        public string GroupBy { get; set; } = "Department"; // "Department" or "Patient"
        public string? DateRangeText { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? LabName { get; set; }
        public string? BranchName { get; set; }
        public string? Address { get; set; }
        public string? MobileNo { get; set; }
        public string? Email { get; set; }
    }
    public class CultureTestItemModel
    {
        public string Department { get; set; }
        public string ReportType { get; set; }
        public string TestName { get; set; }
        public string? Specimen { get; set; }
        public string ReportingMethod { get; set; }
        public string? GramStaining { get; set; }
        public string? PusCells { get; set; }
        public string Interpretation { get; set; }

        public string Diagnosis { get; set; } = "";
        public IList<OrganismModel> Organisms { get; set; } = new List<OrganismModel>();
    }

    public class AntibioticSensitivityModel
    {
        public string? AntibioticName { get; set; }
        public string? Disk { get; set; }
        public string? Result { get; set; }
        public string? MIC { get; set; }
    }

    public class OrganismModel
    {
        public string? OrganismName { get; set; }
        public string? ColonyCount { get; set; }
        public string? Comments { get; set; }
        public IList<AntibioticSensitivityModel>? Antibiotics { get; set; } = new List<AntibioticSensitivityModel>();
    }

    public class CultureReportDto
    {
        public string TenantId { get; set; }

        // Branding Byte Data
        public byte[]? logo { get; set; }
        public byte[]? headerimage { get; set; }
        public byte[]? footerimage { get; set; }
        public bool isletterhead { get; set; } = false;

        // Patient & Report Header Info
        public string SID { get; set; }
        public string PatId { get; set; }
        public string PatientName { get; set; }
        public string Age { get; set; }
        public string Gender { get; set; }
        public string? DoctorName { get; set; }
        public string RefBy { get; set; }
        public string? LabName { get; set; }
        public string? BranchName { get; set; }
        public string? Address { get; set; }
        public string? MobileNo { get; set; }
        public string? Email { get; set; }

        // Dates
        // Change these three from DateTime to DateTime?
        public DateTime CollectedOn { get; set; }
        public DateTime ReceivedOn { get; set; }
        public DateTime ReportedOn { get; set; }

        // Barcode Value
        public string Barcode { get; set; }

        // Dynamic Flat Properties for Single-Test Backward Compatibility
        public string? Department { get; set; }
        public string? ReportType { get; set; }
        public string? TestName { get; set; }
        public string? Specimen { get; set; }
        public string? ReportingMethod { get; set; }
        public string? GramStaining { get; set; }
        public string? PusCells { get; set; }
        public string? Interpretation { get; set; }
        // Structured multi-organism / dynamic antibiotic properties
        public string? Diagnosis { get; set; }
        public IList<OrganismModel>? Organisms { get; set; } = new List<OrganismModel>();

        // List of multiple tests
        public IList<CultureTestItemModel>? Tests { get; set; } = new List<CultureTestItemModel>();
        // Doctor / Signature Info
        public byte[]? SignatureImage { get; set; }
        public string? SignatureDoctorName { get; set; }
        public string? SignatureDoctorDesignation { get; set; }
    }

    public class RoutineReportModel
    {
        public string TenantId { get; set; }
        public int RequestSno { get; set; }
        public byte[] RequestBarCode { get; set; }
        public DateTime RequestDateTime { get; set; }
        public DateTime RequestedDateTime { get; set; }

        public string Name { get; set; }
        public string Gender { get; set; }
        public string DateofBirth { get; set; }
        public int AgeYears { get; set; }
        public int AgeMonths { get; set; }
        public int AgeDays { get; set; }
        public string Address { get; set; }
        public string MobileNo { get; set; }

        public Double RequestAmount { get; set; }

        public string Description { get; set; }
        public string CustCode { get; set; }
        public string Sample { get; set; }
        public string TestName { get; set; }
        public string GroupName { get; set; }

        public string Doctor { get; set; }
        public string Initial { get; set; }
        public string NameTitle { get; set; }
        public string Reference { get; set; }
        public string DoctorCode { get; set; }

        public string EnteredResult { get; set; }
        public string Reporting { get; set; }

        public string CityName { get; set; }
        public string AreaPinCode { get; set; }

        public string RequestGUID { get; set; }

        public int TestSno { get; set; }

        public string Col2 { get; set; }
        public string Units { get; set; }

        public int ResultSno { get; set; }

        public byte[]? CustomerImage { get; set; }
        public byte[]? SignatureImage { get; set; }

        public string ResultGUID { get; set; }

        public string ValueType { get; set; }
        public int TCode { get; set; }

        public DateTime? ResultDateTime { get; set; }

        public string ResultType { get; set; }

        public bool PrintInSeparatePage { get; set; }

        public int TestOrderNo { get; set; }
        public int GroupOrderNo { get; set; }

        public string RoomNo { get; set; }
        public string HospitalID { get; set; }

        public string Email { get; set; }
        public string AreaName { get; set; }
        public string AlteredBHCode { get; set; }

        public DateTime? CollectedDateTime { get; set; }

        public string OnlineCode { get; set; }
        public string ResultValueType { get; set; }

        public string DefaultValue { get; set; }
        public bool SimpleNormalValues { get; set; }
        public bool DetailedNormalValues { get; set; }

        public string RangeType { get; set; }

        public Double FromNormalValue { get; set; }
        public Double ToNormalValue { get; set; }

        public string ConclusionForHigher { get; set; }
        public string ConclusionForLower { get; set; }

        public bool ShowAgedBased { get; set; }
        public bool ShowAlertOnHigherLower { get; set; }

        public string FooterMessage { get; set; }

        public string TRPUName { get; set; }

        public string FixedValues { get; set; }

        public int DecimalPlaces { get; set; }

        public string ReportingMethod { get; set; }

        public Guid TestResultID { get; set; }

        public string RequestSnoPrint { get; set; }

        public bool PrintResultOnly { get; set; }

        public bool ResultNormal { get; set; }
        public bool ResultHigh { get; set; }
        public bool ResultLow { get; set; }

        public bool IsInvestigationPartial { get; set; }

        public string ResultSample { get; set; }

        public string DoctorFullName { get; set; }
        public int DCode { get; set; }

        public string FrontHospitalID { get; set; }
        public string FrontHospitalPatientID { get; set; }

        public bool IsAuthorized1 { get; set; }

        public string DoctorTitle { get; set; }
        public string SecondDoctorName { get; set; }
        public int SecondDCode { get; set; }

        public byte[]? DefaultAuthorizeImage { get; set; }
        public string DefaultAuthorizeName { get; set; }
        public string DefaultAuthorizeDesignation { get; set; }

        public string NormalValues { get; set; }

        public int RowNum { get; set; }
        public int MCCode { get; set; }


    }
    public class AuthorizedUser
    {
        public byte[] EnteredSign { get; set; }
        public string EnteredBy { get; set; }
        public string EnteredByDesignation { get; set; }

        public byte[] AuthorizedSign { get; set; }
        public string AuthorizedBy { get; set; }
        public string AuthorizedByDesignation { get; set; }

        public byte[] AuthorizedSign2 { get; set; }
        public string AuthorizedBy2 { get; set; }
        public string AuthorizedByDesignation2 { get; set; }
    }
    public class RoutineLabReport
    {
        public IList<RoutineReportModel> rrm { get; set; }
        public IList<AuthorizedUser> auth { get; set; }
    }
    public class RoutineReportRequest
    {
        public IList<RoutineReportModel> results { get; set; }
        // Header & Footer branding
        public byte[]? HeaderImage { get; set; }
        public byte[]? FooterImage { get; set; }
        public bool IsLetterhead { get; set; } = false;
    }

    public class ReceiptPdfModel
    {
        public string? ReceiptNo { get; set; }
        public DateTime ReceiptDate { get; set; }

        public int dcode { get; set; }

        public string? ReferralName { get; set; }
        public string? MobileNo { get; set; }
        public string? Address { get; set; }

        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }

        //public string? BillNo { get; set; }

        public int? Totalbills { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }

        public string? PaymentMode { get; set; }

        public int TotalBillsCount { get; set; }
        public int PaidBillsCount { get; set; }
        public int PendingBillsCount { get; set; }
    }

    public class ReceiptRequest
    {
        public ReceiptPdfModel? ReceiptData { get; set; }

        public byte[]? LogoImage { get; set; }
    }

    public class PatientReceiptPdfModel
    {
        public string? ReceiptNo { get; set; }
        public DateTime ReceiptDate { get; set; }

        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? Age { get; set; }
        public string? Gender { get; set; }
        public string? MobileNo { get; set; }
        public string? Address { get; set; }

        public string? ReferralName { get; set; }

        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }

        public string? PaymentMode { get; set; }
    }

    public class PatientReceiptRequest
    {
        public PatientReceiptPdfModel? ReceiptData { get; set; }
        public byte[]? LogoImage { get; set; }
    }



    // Lab Result TNV,DNV AND CF

    public class LabReportDNV
    {
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

    public class LabReportTNV
    {
        public Guid trtid { get; set; }
        public Guid? testresultid { get; set; }
        public string? sex { get; set; }
        public string? normalvalue { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    public class LabReportCF
    {
        public Guid trcfid { get; set; }
        public Guid? testresultid { get; set; }
        public string? sex { get; set; }
        public string? calculatedformula { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    public class PayModeHeader
    {
        public int pmcode { get; set; }
        public string name { get; set; } = string.Empty;
    }

    public class PayModeAmountModel
    {
        public int pmcode { get; set; }
        public decimal amount { get; set; }
    }

    public class PayModeStatementModel
    {
        public string sampleid { get; set; } = string.Empty;
        public DateTime date { get; set; }
        public string custcode { get; set; } = string.Empty;
        public string patientname { get; set; } = string.Empty;
        public string? mobile { get; set; }
        public string? referral { get; set; }
        public decimal billedamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
        public int pmc1 { get; set; }
        public decimal pmc1_amount { get; set; }
        public int pmc2 { get; set; }
        public decimal pmc2_amount { get; set; }
        public int pmc3 { get; set; }
        public decimal pmc3_amount { get; set; }
    }

    public class PayModeStatementRequest
    {
        public IList<PayModeStatementModel>? statements { get; set; }
        public IList<PayModeHeader>? paymodes { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
    }

    public class PayModeSummaryModel
    {
        public DateTime date { get; set; }
        public long? billed { get; set; }
        public decimal? billedamount { get; set; }
        public decimal? paidamount { get; set; }
        public decimal? balanceamount { get; set; }
        public decimal? discountamount { get; set; }
        public decimal? netamount { get; set; }
        public List<PayModeAmountModel> paymode_amounts { get; set; } = new();
    }

    public class PayModeSummaryRequest
    {
        public IList<PayModeSummaryModel>? summary { get; set; }
        public IList<PayModeHeader>? paymodes { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? periodtype { get; set; }
    }

    public class Tenant
    {
        public string legal_name { get; set; }
        public string address_line1 { get; set; }
        public string contact_number { get; set; }
        public string contact_email { get; set; }
        public string? host_url { get; set; }
    }

    public class RawReportRow
    {
        public string TenantId { get; set; } = "";
        public int RequestSno { get; set; }
        public DateTime RequestDateTime { get; set; }
        public DateTime RequestedDateTime { get; set; }
        public string Name { get; set; } = "";
        public string Gender { get; set; } = "";
        public string DateofBirth { get; set; } = "";
        public int AgeYears { get; set; }
        public int AgeMonths { get; set; }
        public int AgeDays { get; set; }
        public string Address { get; set; } = "";
        public string MobileNo { get; set; } = "";
        public double RequestAmount { get; set; }
        public string Description { get; set; } = "";
        public string CustCode { get; set; } = "";
        public string Sample { get; set; } = "";
        public string TestName { get; set; } = "";
        public string GroupName { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string Initial { get; set; } = "";
        public string NameTitle { get; set; } = "";
        public string Reference { get; set; } = "";
        public string DoctorCode { get; set; } = "";
        public string EnteredResult { get; set; } = "";
        public string Reporting { get; set; } = "";
        public string CityName { get; set; } = "";
        public string AreaPinCode { get; set; } = "";
        public string AreaName { get; set; } = "";
        public string RequestGUID { get; set; } = "";
        public int TestSno { get; set; }
        public string Col2 { get; set; } = "";
        public string Units { get; set; } = "";
        public int ResultSno { get; set; }
        public string ResultGUID { get; set; } = "";
        public string ValueType { get; set; } = "";
        public int TCode { get; set; }
        public DateTime? ResultDateTime { get; set; }
        public string ResultType { get; set; } = "";
        public bool PrintInSeparatePage { get; set; }
        public int TestOrderNo { get; set; }
        public int GroupOrderNo { get; set; }
        public string RoomNo { get; set; } = "";
        public string HospitalID { get; set; } = "";
        public string Email { get; set; } = "";
        public string AlteredBHCode { get; set; } = "";
        public DateTime? CollectedDateTime { get; set; }
        public string OnlineCode { get; set; } = "";
        public string ResultValueType { get; set; } = "";
        public string DefaultValue { get; set; } = "";
        public bool SimpleNormalValues { get; set; }
        public bool DetailedNormalValues { get; set; }
        public string RangeType { get; set; } = "";
        public double FromNormalValue { get; set; }
        public double ToNormalValue { get; set; }
        public string ConclusionForHigher { get; set; } = "";
        public string ConclusionForLower { get; set; } = "";
        public bool ShowAgedBased { get; set; }
        public bool ShowAlertOnHigherLower { get; set; }
        public string FooterMessage { get; set; } = "";
        public string TRPUName { get; set; } = "";
        public string FixedValues { get; set; } = "";
        public int DecimalPlaces { get; set; }
        public string ReportingMethod { get; set; } = "";
        public Guid TestResultID { get; set; }
        public string RequestSnoPrint { get; set; } = "";
        public bool PrintResultOnly { get; set; }
        public bool ResultNormal { get; set; }
        public bool ResultHigh { get; set; }
        public bool ResultLow { get; set; }
        public bool IsInvestigationPartial { get; set; }
        public string ResultSample { get; set; } = "";
        public string DoctorFullName { get; set; } = "";
        public int DCode { get; set; }
        public string FrontHospitalID { get; set; } = "";
        public string FrontHospitalPatientID { get; set; } = "";
        public bool IsAuthorized1 { get; set; }
        public string DoctorTitle { get; set; } = "";
        public string SecondDoctorName { get; set; } = "";
        public int SecondDCode { get; set; }
        public string DefaultAuthorizeName { get; set; } = "";
        public string DefaultAuthorizeDesignation { get; set; } = "";
        public string NormalValues { get; set; } = "";
        public int RowNum { get; set; }
        public int MCCode { get; set; }

        public string RequestBarCode { get; set; }
        public string? CustomerImage { get; set; }
        public string? SignatureImage { get; set; }
        public string? DefaultAuthorizeImage { get; set; }
    }

    public class RawAuthUser
    {
        public string EnteredBy { get; set; } = "";
        public string EnteredByDesignation { get; set; } = "";
        public string AuthorizedBy { get; set; } = "";
        public string AuthorizedByDesignation { get; set; } = "";
        public string AuthorizedBy2 { get; set; } = "";
        public string AuthorizedByDesignation2 { get; set; } = "";

        public string? EnteredSign { get; set; }
        public string? AuthorizedSign { get; set; }
        public string? AuthorizedSign2 { get; set; }
    }
}
