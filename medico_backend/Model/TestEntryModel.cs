using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("test_result_master")]
    public class test_result_master
    {
        [Dapper.Contrib.Extensions.Key]
        public long trcode { get; set; }
        public Guid testresultid { get; set; }
        public Guid? trguid { get; set; }
        public long slno { get; set; }
        public long tcode { get; set; }
        public int? fromtcode { get; set; }
        public string resulttype { get; set; } = string.Empty;
        public string? col2 { get; set; }
        public string? col3 { get; set; }
        public string? col4 { get; set; }
        public string? col5 { get; set; }
        public string? col6 { get; set; }
        public string? cellcontent { get; set; }
        public string? normalfemale { get; set; }
        public string? normalchild { get; set; }
        public int? stylecode { get; set; }
        public bool? sendsms { get; set; }
        public string? smsshortname { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTimeOffset? entereddate { get; set; }
        public DateTimeOffset? ibsdate { get; set; }
        public bool? printinseparatepage { get; set; }
        public bool? iscalculated { get; set; }
        public bool? isentered { get; set; }
        public string? calculatedformula { get; set; }
        public int? dstylecode { get; set; }
        public int? qstylecode { get; set; }
        public int? estylecode { get; set; }
        public int? ustylecode { get; set; }
        public int? nstylecode { get; set; }
        public Guid? fromtestresultid { get; set; }
        public string? skycode { get; set; }
        public bool? is_escalation { get; set; }
        public string? tenant_code { get; set; }
        public string? testimage { get; set; }  // ← S3 key
    }

    [Table("test_result_properties")]
    public class test_result_properties
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
        public bool? isgraph { get; set; }
        public double? graphvalue { get; set; }
        public int? decimalvalue { get; set; }
        public int? scode { get; set; }
        public DateTimeOffset? entereddate { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public bool? usedefault { get; set; }
        public Guid? normalvalueforfxtype { get; set; }
        public string? normalvalue { get; set; }
        public bool? isabnormal { get; set; }
        public string? criticallowtype { get; set; }
        public string? criticallowrange { get; set; }
        public string? criticalhightype { get; set; }
        public string? criticalhighrange { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("test_result_calculatedformula")]
    public class TestResultCalculatedformula
    {
        [ExplicitKey]
        public Guid trcfid { get; set; }
        public Guid? testresultid { get; set; }
        public string? sex { get; set; }
        public string? calculatedformula { get; set; }
        public DateTimeOffset? entereddate { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("test_result_detailednormalvalues")]
    public class test_result_detailednormalvalues
    {
        [ExplicitKey]
        public Guid trdnid { get; set; }
        public Guid? testresultid { get; set; }

        public int? sno { get; set; }       // row order

        // Age range
        public int? agefrom { get; set; }
        public string? agefromtype { get; set; }  // Days, Mths, Yrs
        public int? ageto { get; set; }
        public string? agetotype { get; set; }    // Days, Mths, Yrs
        public string? sex { get; set; }

        public string? rangetype { get; set; }    // Between, >, <, etc.
        public double? rangefrom { get; set; }
        public double? rangeto { get; set; }

        public Guid? specialconditioncode { get; set; }
        public string? agerangetype { get; set; } // label like "Adult Male"

        public DateTimeOffset? entereddate { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("test_result_textnormalvalues")]
    public class test_result_textnormalvalues
    {
        [ExplicitKey]
        public Guid trtid { get; set; }
        public Guid? testresultid { get; set; }
        public string? sex { get; set; }

        public string? normalvalue { get; set; }  // display text e.g. "> 40 mg/dL"

        public DateTimeOffset? entereddate { get; set; }
        public int? mccode { get; set; }
        public int? performedcount { get; set; }
        public int? scode { get; set; }
        public string? tenant_code { get; set; }
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    public class TestResultRowDto
    {
        public test_result_master? ResultMaster { get; set; }
        public test_result_properties? ResultProperties { get; set; }
        public List<TestResultCalculatedformula>? CalculatedFormulas { get; set; }
        public List<test_result_detailednormalvalues>? DetailedNormalValues { get; set; }
        public List<test_result_textnormalvalues>? TextNormalValues { get; set; }
    }

    public class TestInsertDto
    {
        public TestMasterModel TestMaster { get; set; } = new();
        public List<TestResultRowDto>? ResultRows { get; set; }
    }
}