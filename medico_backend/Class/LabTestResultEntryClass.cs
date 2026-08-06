using Dapper;
using medico_backend.InventoryModel;
using medico_backend.Model;
using medico_backend.Services;
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Org.BouncyCastle.Ocsp;
using SkiaSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace medico_backend.Class
{
    public class LabTestResultClass
    {
        private readonly string _connectionString;
        private readonly CustomerMasterClass _customerClass;
        private readonly S3ImageService _s3Service;

        public LabTestResultClass(IConfiguration configuration, CustomerMasterClass customerClass, S3ImageService s3Service)
        {
            _connectionString = configuration.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Connection string 'conn' not found.");
            _customerClass = customerClass;
            _s3Service = s3Service;
        }

        private static string Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? string.Empty :
            value.Length <= maxLength ? value :
            value[..maxLength];

        private NpgsqlConnection CreateConnection() => new(_connectionString);

        public async Task<ResultEntryModel> GetResult(string guid, string tenantCode)
        {
            try
            {
                await using var db = CreateConnection();
                await db.OpenAsync();

                const string sql = @"
-- ── RS 1 : Pending tests (resultstatus = false) ──────────────────────────────
SELECT  CAST(lrm.requestguid AS VARCHAR(50))                                AS requestguid,
        trm.slno,
        tm.tcode,
        tm.name                                                             AS testname,
        tm.orderno,
        COALESCE(gm.orderno, 0)                                             AS grouporder,
        COALESCE(gm.gcode,   0)                                             AS gcode,
        COALESCE(gm.name,   '')                                             AS groupname,
        trm.col2,
        ''                                                                  AS enteredresult,
        trm.resulttype,
        COALESCE(trp.resultvaluetype,    '')                                AS resultvaluetype,
        COALESCE(trp.normalvalue,        '')                                AS normalvalue,
        trm.testresultid,
        lrd.resultstatus                                                    AS status,
        COALESCE(trp.defaultvalueforfxtype, '00000000-0000-0000-0000-000000000000'::uuid)
                                                                            AS defaultvalueforfxtype,
        COALESCE(trp.fxtcode,               '00000000-0000-0000-0000-000000000000'::uuid)
                                                                            AS fxtcode,
        true                                                                AS resultnormal,
        false                                                               AS resulthigh,
        false                                                               AS resultlow,
        trm.fromtcode,
        trm.fromtestresultid,
        COALESCE(trp.simplenormalvalues,    false)                          AS simplenv,
        COALESCE(trp.detailednormalvalues,  false)                          AS detailednv,
        trm.calculatedformula,
        COALESCE(trp.defaultunitscode, 0)                                   AS defaultunitscode,
        COALESCE(trp.mccode, 0)                                             AS mccode,
        COALESCE(trp.scode,  0)                                             AS scode,
        0                                                                   AS resultenteredby,
        false                                                               AS isauthorized1,
        false                                                               AS isauthorized2,
        0                                                                   AS resultauthorizedby,
        0                                                                   AS resultauthorizedby2,
        ''                                                                  AS fixedvalues,
        COALESCE(sm.name,  '')                                              AS samplename,
        COALESCE(uom.name, '')                                              AS unitname
FROM   lab_request_master  lrm
JOIN   lab_request_details lrd ON lrm.requestguid = lrd.requestguid
JOIN   test_master         tm  ON lrd.tcode = tm.tcode
                               AND tm.tenant_code = @TenantCode
JOIN   test_result_master  trm ON tm.tcode = trm.tcode
                               AND trm.tenant_code = @TenantCode
LEFT JOIN test_result_properties trp
       ON trp.testresultid = COALESCE(
              CASE WHEN trm.fromtestresultid IS NOT NULL
                        AND trm.fromtestresultid != '00000000-0000-0000-0000-000000000000'::uuid
                   THEN trm.fromtestresultid
              END,
              trm.testresultid
          )
      AND (trp.usedefault = true OR trp.usedefault IS NULL)
      AND (trp.tenant_code = @TenantCode OR trp.tenant_code = '0' OR trp.tenant_code IS NULL)
LEFT JOIN group_master   gm  ON tm.gcode              = gm.gcode
                             AND gm.tenant_code         = @TenantCode
LEFT JOIN sample_master  sm  ON tm.scode              = sm.scode
                             AND sm.tenant_code         = @TenantCode
LEFT JOIN machine_master mm  ON trp.mccode            = mm.mccode
                             AND mm.tenant_code         = @TenantCode
LEFT JOIN uom_master     uom ON trp.defaultunitscode  = uom.ucode
                             AND uom.tenant_code        = @TenantCode
LEFT JOIN report_method  rm  ON trp.rtmcode           = rm.rtmcode
                             AND rm.tenant_code         = @TenantCode
WHERE lrm.requestguid  = @RequestGUIDText
  AND lrd.resultstatus = false
  AND lrd.ttid         IN (1)
  AND lrd.tenant_code  = @TenantCode

UNION ALL

-- ── RS 1 cont. : Already-saved results (resultstatus = true) ─────────────────
SELECT  CAST(lrm2.requestguid AS VARCHAR(50))                               AS requestguid,
        lrd2.testsno                                                        AS slno,
        tm2.tcode,
        tm2.name                                                            AS testname,
        tm2.orderno,
        COALESCE(gm2.orderno, 0)                                            AS grouporder,
        COALESCE(gm2.gcode,   0)                                            AS gcode,
        COALESCE(gm2.name,   '')                                            AS groupname,
        lrd2.description                                                    AS col2,
        lrd2.enteredresult,
        lrd2.resulttype,
        COALESCE(lrp.resultvaluetype,    '')                                AS resultvaluetype,
        COALESCE(lrp.normalvalue,        '')                                AS normalvalue,
        lrd2.testresultid,
        true                                                                AS status,
        COALESCE(lrp.defaultvalueforfxtype, '00000000-0000-0000-0000-000000000000'::uuid)
                                                                            AS defaultvalueforfxtype,
        COALESCE(lrp.fxtcode,               '00000000-0000-0000-0000-000000000000'::uuid)
                                                                            AS fxtcode,
        COALESCE(lrp.resultnormal, true)                                    AS resultnormal,
        COALESCE(lrp.resulthigh,   false)                                   AS resulthigh,
        COALESCE(lrp.resultlow,    false)                                   AS resultlow,
        lrd2.fromtcode,
        lrd2.fromtestresultid,
        COALESCE(lrp.simplenormalvalues,   false)                           AS simplenv,
        COALESCE(lrp.detailednormalvalues, false)                           AS detailednv,
        lrd2.calculatedformula,
        COALESCE(lrp.defaultunitscode, 0)                                   AS defaultunitscode,
        COALESCE(lrp.mccode, 0)                                             AS mccode,
        COALESCE(lrp.scode,  0)                                             AS scode,
        COALESCE(reqd.resultenteredby,     0)                               AS resultenteredby,
        COALESCE(reqd.isauthorized1,    false)                              AS isauthorized1,
        COALESCE(reqd.isauthorized2,    false)                              AS isauthorized2,
        COALESCE(reqd.resultauthorizedby,  0)                               AS resultauthorizedby,
        COALESCE(reqd.resultauthorizedby2, 0)                               AS resultauthorizedby2,
        ''                                                                  AS fixedvalues,
        COALESCE(sm2.name,  '')                                             AS samplename,
        COALESCE(uom2.name, '')                                             AS unitname
FROM   lab_result_master lrm2
JOIN   lab_result_details lrd2 ON lrm2.resultguid = lrd2.resultguid
JOIN   test_master         tm2 ON lrd2.tcode = tm2.tcode
                               AND tm2.tenant_code = @TenantCode
LEFT JOIN lab_result_properties lrp
       ON lrd2.testresultid = lrp.testresultid
      AND (lrp.tenant_code = @TenantCode OR lrp.tenant_code = '0' OR lrp.tenant_code IS NULL)
LEFT JOIN group_master    gm2  ON tm2.gcode              = gm2.gcode
                               AND gm2.tenant_code         = @TenantCode
LEFT JOIN sample_master   sm2  ON tm2.scode              = sm2.scode
                               AND sm2.tenant_code         = @TenantCode
LEFT JOIN machine_master  mm2  ON lrp.mccode             = mm2.mccode
                               AND mm2.tenant_code         = @TenantCode
LEFT JOIN uom_master      uom2 ON lrp.defaultunitscode   = uom2.ucode
                               AND uom2.tenant_code        = @TenantCode
LEFT JOIN report_method   rm2  ON lrp.rtmcode            = rm2.rtmcode
                               AND rm2.tenant_code         = @TenantCode
LEFT JOIN lab_request_details reqd
      ON reqd.requestguid = lrm2.requestguid::text 
      AND reqd.tcode        = lrd2.tcode
      AND reqd.tenant_code  = @TenantCode
WHERE lrm2.requestguid = @RequestGUID
  AND lrm2.tenant_code = @TenantCode;

-- ── RS 2 : Units ──────────────────────────────────────────────────────────────
SELECT * FROM uom_master
WHERE tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL;

-- ── RS 3 : Samples ────────────────────────────────────────────────────────────
SELECT * FROM sample_master
WHERE tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL;

-- ── RS 4 : Machines ───────────────────────────────────────────────────────────
SELECT * FROM machine_master
WHERE tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL;

-- ── RS 5 : Report methods ─────────────────────────────────────────────────────
SELECT * FROM report_method
WHERE tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL;

-- ── RS 6 : test_result_properties (pending rows) ─────────────────────────────
WITH pending_ids AS (
    SELECT DISTINCT
        CASE WHEN trm.fromtestresultid IS NOT NULL
                  AND trm.fromtestresultid != '00000000-0000-0000-0000-000000000000'::uuid
             THEN trm.fromtestresultid
             ELSE trm.testresultid
        END AS resolvedid
    FROM   lab_request_details lrd
    JOIN   test_result_master  trm ON lrd.tcode = trm.tcode
                                   AND trm.tenant_code = @TenantCode
    WHERE  lrd.requestguid  = @RequestGUIDText
      AND  lrd.resultstatus = false
      AND  lrd.ttid         IN (1)
      AND  lrd.tenant_code  = @TenantCode
)
SELECT trp.* FROM test_result_properties trp
JOIN   pending_ids p ON trp.testresultid = p.resolvedid
WHERE  (trp.usedefault = true OR trp.usedefault IS NULL)
  AND  (trp.tenant_code = @TenantCode OR trp.tenant_code = '0' OR trp.tenant_code IS NULL);

-- ── RS 7 : test_result_textnormalvalues (pending rows) ───────────────────────
WITH pending_ids AS (
    SELECT DISTINCT
        CASE WHEN trm.fromtestresultid IS NOT NULL
                  AND trm.fromtestresultid != '00000000-0000-0000-0000-000000000000'::uuid
             THEN trm.fromtestresultid ELSE trm.testresultid END AS resolvedid
    FROM   lab_request_details lrd
    JOIN   test_result_master  trm ON lrd.tcode = trm.tcode AND trm.tenant_code = @TenantCode
    WHERE  lrd.requestguid = @RequestGUIDText AND lrd.resultstatus = false
      AND  lrd.ttid        IN (1)
      AND  lrd.tenant_code = @TenantCode
)
SELECT trnv.* FROM test_result_textnormalvalues trnv
JOIN   pending_ids p ON trnv.testresultid = p.resolvedid
WHERE  (trnv.tenant_code = @TenantCode OR trnv.tenant_code = '0' OR trnv.tenant_code IS NULL);

-- ── RS 8 : test_result_detailednormalvalues (pending rows) ───────────────────
WITH pending_ids AS (
    SELECT DISTINCT
        CASE WHEN trm.fromtestresultid IS NOT NULL
                  AND trm.fromtestresultid != '00000000-0000-0000-0000-000000000000'::uuid
             THEN trm.fromtestresultid ELSE trm.testresultid END AS resolvedid
    FROM   lab_request_details lrd
    JOIN   test_result_master  trm ON lrd.tcode = trm.tcode AND trm.tenant_code = @TenantCode
    WHERE  lrd.requestguid = @RequestGUIDText AND lrd.resultstatus = false
      AND  lrd.ttid        IN (1)
      AND  lrd.tenant_code = @TenantCode
)
SELECT trdnv.* FROM test_result_detailednormalvalues trdnv
JOIN   pending_ids p ON trdnv.testresultid = p.resolvedid
WHERE  (trdnv.tenant_code = @TenantCode OR trdnv.tenant_code = '0' OR trdnv.tenant_code IS NULL);

-- ── RS 9 : test_result_calculatedformula (pending rows) ──────────────────────
WITH pending_ids AS (
    SELECT DISTINCT
        CASE WHEN trm.fromtestresultid IS NOT NULL
                  AND trm.fromtestresultid != '00000000-0000-0000-0000-000000000000'::uuid
             THEN trm.fromtestresultid ELSE trm.testresultid END AS resolvedid
    FROM   lab_request_details lrd
    JOIN   test_result_master  trm ON lrd.tcode = trm.tcode AND trm.tenant_code = @TenantCode
    WHERE  lrd.requestguid = @RequestGUIDText AND lrd.resultstatus = false
      AND  lrd.ttid        IN (1)
      AND  lrd.tenant_code = @TenantCode
)
SELECT tcf.* FROM test_result_calculatedformula tcf
JOIN   pending_ids p ON tcf.testresultid = p.resolvedid
WHERE  (tcf.tenant_code = @TenantCode OR tcf.tenant_code = '0' OR tcf.tenant_code IS NULL);

-- ── RS 10–13 : Lab_Result_* details (saved rows) ─────────────────────────────
SELECT lrp.*
FROM   lab_result_properties lrp
JOIN   lab_result_details    lrd ON lrp.testresultid = lrd.testresultid
JOIN   lab_result_master     lrm ON lrd.resultguid   = lrm.resultguid
WHERE  lrm.requestguid  = @RequestGUID
  AND  lrm.tenant_code  = @TenantCode
  AND  (lrp.tenant_code = @TenantCode OR lrp.tenant_code = '0' OR lrp.tenant_code IS NULL);

SELECT lrnv.*
FROM   lab_result_textnormalvalues lrnv
JOIN   lab_result_details          lrd ON lrnv.testresultid = lrd.testresultid
JOIN   lab_result_master           lrm ON lrd.resultguid    = lrm.resultguid
WHERE  lrm.requestguid  = @RequestGUID
  AND  lrm.tenant_code  = @TenantCode
  AND  lrnv.tenant_code = @TenantCode;

SELECT lrdnv.*
FROM   lab_result_detailednormalvalues lrdnv
JOIN   lab_result_details              lrd ON lrdnv.testresultid = lrd.testresultid
JOIN   lab_result_master               lrm ON lrd.resultguid     = lrm.resultguid
WHERE  lrm.requestguid  = @RequestGUID
  AND  lrm.tenant_code  = @TenantCode
  AND  lrdnv.tenant_code = @TenantCode;

SELECT lcf.*
FROM   lab_result_calculatedformula lcf
JOIN   lab_result_details           lrd ON lcf.testresultid = lrd.testresultid
JOIN   lab_result_master            lrm ON lrd.resultguid   = lrm.resultguid
WHERE  lrm.requestguid  = @RequestGUID
  AND  lrm.tenant_code  = @TenantCode
  AND  lcf.tenant_code  = @TenantCode;";

                var param = new
                {
                    RequestGUID = Guid.Parse(guid),   // for uuid columns (lab_result_*)
                    RequestGUIDText = guid,               // for text columns (lab_request_*)
                    TenantCode = tenantCode
                };

                using var multi = await db.QueryMultipleAsync(sql, param);

                var results = (await multi.ReadAsync<LabResultEntry>()).ToList();
                var units = (await multi.ReadAsync<UomMasterModel>()).ToList();
                var samples = (await multi.ReadAsync<SampleMasterModel>()).ToList();
                var machines = (await multi.ReadAsync<MachineMasterModel>()).ToList();
                var methods = (await multi.ReadAsync<ReportMethodModel>()).ToList();

                var testProps = (await multi.ReadAsync<test_result_properties>())
                                    .ToLookup(x => x.testresultid!.Value);
                var testTNV = (await multi.ReadAsync<test_result_textnormalvalues>())
                                    .ToLookup(x => x.testresultid!.Value);
                var testDNV = (await multi.ReadAsync<test_result_detailednormalvalues>())
                                    .ToLookup(x => x.testresultid!.Value);
                var testCF = (await multi.ReadAsync<TestResultCalculatedformula>())
                                    .ToLookup(x => x.testresultid!.Value);

                var labProps = (await multi.ReadAsync<LabResultPropertiesModel>())
                                   .ToLookup(x => x.testresultid);
                var labTNV = (await multi.ReadAsync<LabResultTextNormalValuesModel>())
                                   .ToLookup(x => x.testresultid);
                var labDNV = (await multi.ReadAsync<LabResultDetailedNormalValuesModel>())
                                   .ToLookup(x => x.testresultid);
                var labCF = (await multi.ReadAsync<LabResultCalculatedFormulaModel>())
                                   .ToLookup(x => x.testresultid);

                foreach (var entry in results)
                {
                    if (!entry.status)
                    {
                        Guid lookupId = entry.fromtestresultid != Guid.Empty
                            ? entry.fromtestresultid
                            : entry.testresultid;

                        entry.testproperties = testProps[lookupId].ToList();

                        entry.textnormalvalues = testTNV[lookupId]
                            .Select(r => new LabResultTextNormalValuesModel
                            {
                                sex = r.sex,
                                normalvalue = r.normalvalue,
                                mccode = r.mccode,
                                scode = r.scode,
                                performedcount = r.performedcount ?? 1
                            }).ToList();

                        entry.detailednormalvalues = testDNV[lookupId]
                            .Select(r => new LabResultDetailedNormalValuesModel
                            {
                                sno = r.sno ?? 0,
                                agefrom = r.agefrom ?? 0,
                                agefromtype = r.agefromtype,
                                ageto = r.ageto ?? 0,
                                agetotype = r.agetotype,
                                sex = r.sex,
                                rangetype = r.rangetype,
                                rangefrom = r.rangefrom ?? 0.0,
                                rangeto = r.rangeto ?? 0.0,
                                specialconditioncode = r.specialconditioncode,
                                agerangetype = r.agerangetype,
                                mccode = r.mccode,
                                scode = r.scode,
                                performedcount = r.performedcount ?? 1
                            }).ToList();

                        entry.calculatedformulas = testCF[lookupId]
                            .Select(r => new LabResultCalculatedFormulaModel
                            {
                                sex = r.sex,
                                calculatedformula = r.calculatedformula,
                                mccode = r.mccode,
                                scode = r.scode,
                                performedcount = r.performedcount ?? 1
                            }).ToList();
                    }
                    else
                    {
                        entry.labproperties = labProps[entry.testresultid].ToList();
                        entry.textnormalvalues = labTNV[entry.testresultid].ToList();
                        entry.detailednormalvalues = labDNV[entry.testresultid].ToList();
                        entry.calculatedformulas = labCF[entry.testresultid].ToList();
                    }
                }

                return new ResultEntryModel
                {
                    results = results,
                    units = units,
                    samples = samples,
                    machines = machines,
                    methods = methods
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetResult] ERROR: {ex.Message}\n{ex}");
                throw;
            }
        }

        public async Task<TestResultDetailsModel> GetTestResultDetails(Guid testResultId, string tenantCode)
        {
            try
            {
                await using var db = CreateConnection();
                await db.OpenAsync();

                var param = new { Id = testResultId, TenantCode = tenantCode };

                bool existsInLab = await db.ExecuteScalarAsync<bool>(
                    @"SELECT EXISTS (
                        SELECT 1 FROM lab_result_properties
                        WHERE  testresultid = @Id AND tenant_code = @TenantCode
                      )", param);

                if (existsInLab)
                {
                    const string sql = @"
SELECT * FROM lab_result_properties
WHERE  testresultid = @Id AND (usedefault = true OR usedefault IS NULL) AND tenant_code = @TenantCode;

SELECT * FROM lab_result_textnormalvalues
WHERE  testresultid = @Id AND tenant_code = @TenantCode;

SELECT * FROM lab_result_detailednormalvalues
WHERE  testresultid = @Id AND tenant_code = @TenantCode;

SELECT * FROM lab_result_calculatedformula
WHERE  testresultid = @Id AND tenant_code = @TenantCode;";

                    using var multi = await db.QueryMultipleAsync(sql, param);

                    return new TestResultDetailsModel
                    {
                        isresulted = true,
                        labproperties = (await multi.ReadAsync<LabResultPropertiesModel>()).ToList(),
                        labtextnormalvalues = (await multi.ReadAsync<LabResultTextNormalValuesModel>()).ToList(),
                        labdetailedNormalvalues = (await multi.ReadAsync<LabResultDetailedNormalValuesModel>()).ToList(),
                        labcalculatedformulas = (await multi.ReadAsync<LabResultCalculatedFormulaModel>()).ToList(),
                        properties = new List<test_result_properties>(),
                        textnormalvalues = new List<test_result_textnormalvalues>(),
                        detailedNormalvalues = new List<test_result_detailednormalvalues>(),
                        calculatedformulas = new List<TestResultCalculatedformula>()
                    };
                }
                else
                {
                    const string sql = @"
SELECT * FROM test_result_properties
WHERE  testresultid = @Id
  AND  (usedefault = true OR usedefault IS NULL)
  AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL);

SELECT * FROM test_result_textnormalvalues
WHERE  testresultid = @Id
  AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL);

SELECT * FROM test_result_detailednormalvalues
WHERE  testresultid = @Id
  AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL);

SELECT * FROM test_result_calculatedformula
WHERE  testresultid = @Id
  AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL);";

                    using var multi = await db.QueryMultipleAsync(sql, param);

                    return new TestResultDetailsModel
                    {
                        isresulted = false,
                        properties = (await multi.ReadAsync<test_result_properties>()).ToList(),
                        textnormalvalues = (await multi.ReadAsync<test_result_textnormalvalues>()).ToList(),
                        detailedNormalvalues = (await multi.ReadAsync<test_result_detailednormalvalues>()).ToList(),
                        calculatedformulas = (await multi.ReadAsync<TestResultCalculatedformula>()).ToList(),
                        labproperties = new List<LabResultPropertiesModel>(),
                        labtextnormalvalues = new List<LabResultTextNormalValuesModel>(),
                        labdetailedNormalvalues = new List<LabResultDetailedNormalValuesModel>(),
                        labcalculatedformulas = new List<LabResultCalculatedFormulaModel>()
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetTestResultDetails] ERROR: {ex.Message}\n{ex}");
                return new TestResultDetailsModel();
            }
        }

        public async Task<bool> SaveResult(List<LabResultEntry> resultEntries, string tenantCode)
        {
            if (resultEntries is not { Count: > 0 })
                return false;

            if (string.IsNullOrWhiteSpace(tenantCode))
                throw new ArgumentException("tenantCode is required", nameof(tenantCode));

            try
            {
                await using var db = CreateConnection();
                await db.OpenAsync();
                await using var transaction = await db.BeginTransactionAsync();

                try
                {
                    foreach (var group in resultEntries.GroupBy(x => x.requestguid))
                        await ProcessResultGroupAsync(db, transaction, group.Key, group.ToList(), tenantCode);

                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"[SaveResult] ROLLBACK — {ex.GetType().Name}: {ex.Message}\n{ex}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveResult] ERROR — {ex.GetType().Name}: {ex.Message}\n{ex}");
                throw;
            }
        }

        private async Task ProcessResultGroupAsync(
            NpgsqlConnection db,
            NpgsqlTransaction transaction,
            string requestGuid,
            List<LabResultEntry> entries,
            string tenantCode)
        {
            int userCode = entries.First().resultenteredby;

            string? existingResultGuid = await db.QueryFirstOrDefaultAsync<string?>(@"
                SELECT resultguid::text
                FROM   lab_result_master
                WHERE  requestguid = @RequestGUID
                  AND  tenant_code = @TenantCode
                LIMIT  1",
            new { RequestGUID = Guid.Parse(requestGuid), TenantCode = tenantCode }, transaction);

            bool isUpdate = !string.IsNullOrEmpty(existingResultGuid);
            string resultGuid = isUpdate ? existingResultGuid! : Guid.NewGuid().ToString();

            var barcodeInfo = await db.QueryFirstOrDefaultAsync<(string barcode, string convertedBarcode)?>(@"
        SELECT requestbarcode, requestconvertedbarcode
        FROM   lab_request_master
        WHERE  requestguid = @RequestGUID
          AND  tenant_code = @TenantCode
        LIMIT  1",
                new { RequestGUID = requestGuid, TenantCode = tenantCode }, transaction);

            string resultBarcode = barcodeInfo?.barcode ?? string.Empty;
            string resultConvertedBarcode = barcodeInfo?.convertedBarcode ?? string.Empty;

            var masterToLab = new Dictionary<Guid, Guid>();
            var tCodeToLabId = new Dictionary<int, Guid>();
            var existingMasterIds = new HashSet<Guid>();

            if (isUpdate)
            {
                var existingLabRows = (await db.QueryAsync<(Guid labTestResultId, int tcode)>(@"
        SELECT testresultid, tcode
        FROM   lab_result_details
        WHERE  resultguid  = @ResultGUID
          AND  tenant_code = @TenantCode",
                    new { ResultGUID = Guid.Parse(resultGuid), TenantCode = tenantCode }, transaction)).ToList();

                var existingLabIds = existingLabRows.Select(r => r.labTestResultId).ToHashSet();

                var propMapping = new Dictionary<Guid, Guid>();
                if (existingLabIds.Count > 0)
                {
                    var propRows = await db.QueryAsync<(Guid masterTestResultId, Guid labTestResultId)>(@"
            SELECT mastertestresultid, testresultid
            FROM   lab_result_properties
            WHERE  testresultid  = ANY(@Ids)
              AND  mastertestresultid IS NOT NULL
              AND  mastertestresultid != '00000000-0000-0000-0000-000000000000'
              AND  tenant_code = @TenantCode",
                        new { Ids = existingLabIds.ToArray(), TenantCode = tenantCode }, transaction);

                    foreach (var r in propRows)
                        propMapping[r.masterTestResultId] = r.labTestResultId;
                }

                foreach (var e in entries)
                {
                    if (e.testresultid == Guid.Empty) continue;

                    if (propMapping.TryGetValue(e.testresultid, out var mappedLabId))
                    {
                        masterToLab[e.testresultid] = mappedLabId;
                        existingMasterIds.Add(e.testresultid);
                    }
                    else if (existingLabIds.Contains(e.testresultid))
                    {
                        masterToLab[e.testresultid] = e.testresultid;
                        existingMasterIds.Add(e.testresultid);
                    }
                    else
                    {
                        masterToLab[e.testresultid] = Guid.NewGuid();
                    }
                }

                foreach (var r in existingLabRows)
                    tCodeToLabId[r.tcode] = r.labTestResultId;
            }
            else
            {
                foreach (var e in entries)
                    if (e.testresultid != Guid.Empty && !masterToLab.ContainsKey(e.testresultid))
                        masterToLab[e.testresultid] = Guid.NewGuid();
            }

            string ResolveFormula(string? formula)
            {
                if (string.IsNullOrEmpty(formula)) return string.Empty;
                foreach (var (masterId, labId) in masterToLab)
                    formula = formula.Replace(masterId.ToString(), labId.ToString(),
                                              StringComparison.OrdinalIgnoreCase);
                return formula;
            }

            Guid ResolveFromTestResultId(Guid fromId) =>
                fromId != Guid.Empty && masterToLab.TryGetValue(fromId, out var mapped) ? mapped : fromId;

            if (isUpdate)
            {
                await db.ExecuteAsync(@"
            UPDATE lab_result_master
            SET    resultdatetime = @Now,
                   entereddate    = @Now
            WHERE  resultguid  = @ResultGUID
              AND  tenant_code = @TenantCode",
                    new
                    {
                        Now = DateTime.UtcNow,
                        ResultGUID = Guid.Parse(resultGuid),
                        TenantCode = tenantCode
                    },
                    transaction);
            }
            else
            {
                await db.ExecuteAsync(@"
            INSERT INTO lab_result_master
                (resultguid, resultsno, resultbarcode, resultconvertedbarcode,
                 resultdatetime, requestguid, description,
                 deleted, usercode, computercode, entereddate, ibsdate, tenant_code)
            VALUES
                (@ResultGUID, 1, @ResultBarcode, @ResultConvertedBarcode,
                 @Now, @RequestGUID,
                 '', false, @UserCode, 0, @Now, @Now, @TenantCode)",
                    new
                    {
                        ResultGUID = Guid.Parse(resultGuid),
                        ResultBarcode = resultBarcode,
                        ResultConvertedBarcode = resultConvertedBarcode,
                        Now = DateTime.UtcNow,
                        RequestGUID = Guid.Parse(requestGuid),
                        UserCode = userCode,
                        TenantCode = tenantCode
                    },
                    transaction);
            }

            var masterIds = entries
                .Where(e => e.testresultid != Guid.Empty)
                .Select(e => e.testresultid)
                .Distinct()
                .ToArray();

            var allTestProps = (await db.QueryAsync<test_result_properties>(@"
        SELECT * FROM test_result_properties
        WHERE  testresultid = ANY(@Ids)
          AND  (usedefault = true OR usedefault IS NULL)
          AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL)",
                new { Ids = masterIds, TenantCode = tenantCode }, transaction))
                .ToDictionary(p => p.testresultid!.Value);

            var rtmFallbackMap = (await db.QueryAsync<(Guid testresultid, int rtmcode)>(@"
        SELECT DISTINCT ON (testresultid) testresultid, rtmcode
        FROM   test_result_properties
        WHERE  testresultid = ANY(@Ids)
          AND  rtmcode      > 0
          AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL)
        ORDER  BY testresultid, rtmcode",
                new { Ids = masterIds, TenantCode = tenantCode }, transaction))
                .ToDictionary(r => r.testresultid, r => r.rtmcode);

            var allTextNV = (await db.QueryAsync<test_result_textnormalvalues>(@"
        SELECT * FROM test_result_textnormalvalues
        WHERE  testresultid = ANY(@Ids)
          AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL)",
                new { Ids = masterIds, TenantCode = tenantCode }, transaction))
                .ToLookup(r => r.testresultid!.Value);

            var allDetailedNV = (await db.QueryAsync<test_result_detailednormalvalues>(@"
        SELECT * FROM test_result_detailednormalvalues
        WHERE  testresultid = ANY(@Ids)
          AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL)",
                new { Ids = masterIds, TenantCode = tenantCode }, transaction))
                .ToLookup(r => r.testresultid!.Value);

            var allFormulas = (await db.QueryAsync<TestResultCalculatedformula>(@"
        SELECT * FROM test_result_calculatedformula
        WHERE  testresultid = ANY(@Ids)
          AND  (tenant_code = @TenantCode OR tenant_code = '0' OR tenant_code IS NULL)",
                new { Ids = masterIds, TenantCode = tenantCode }, transaction))
                .ToLookup(r => r.testresultid!.Value);

            var tnvList = new List<object>();
            var dnvList = new List<object>();
            var cfList = new List<object>();

            foreach (var entry in entries)
            {
                Guid labId = entry.testresultid != Guid.Empty
                             && masterToLab.TryGetValue(entry.testresultid, out var mapped)
                    ? mapped
                    : Guid.NewGuid();

                int resolvedFromTCode = entry.fromtcode;
                Guid resolvedFromTestResultId = entry.fromtestresultid;

                if (entry.resultvaluetype == "Calculated Value"
                    && (resolvedFromTCode == 0 || resolvedFromTestResultId == Guid.Empty))
                {
                    if (!string.IsNullOrEmpty(entry.calculatedformula))
                    {
                        var firstGuidStr = entry.calculatedformula
                            .Split('|')
                            .FirstOrDefault(p => Guid.TryParse(p.Trim(), out _));

                        if (Guid.TryParse(firstGuidStr?.Trim(), out var formulaSourceId)
                            && formulaSourceId != Guid.Empty)
                        {
                            var refEntry = entries.FirstOrDefault(e => e.testresultid == formulaSourceId);
                            if (refEntry != null)
                            {
                                resolvedFromTCode = refEntry.tcode;
                                resolvedFromTestResultId = formulaSourceId;
                            }
                        }
                    }

                    if ((resolvedFromTCode == 0 || resolvedFromTestResultId == Guid.Empty)
                        && entry.testresultid != Guid.Empty)
                    {
                        var masterRef = await db.QueryFirstOrDefaultAsync<(int fromtcode, Guid fromtestresultid)?>(@"
                    SELECT fromtcode, fromtestresultid
                    FROM   test_result_master
                    WHERE  testresultid = @Id
                    LIMIT  1",
                            new { Id = entry.testresultid }, transaction);

                        if (masterRef.HasValue)
                        {
                            resolvedFromTCode = masterRef.Value.fromtcode;
                            resolvedFromTestResultId = masterRef.Value.fromtestresultid;
                        }
                    }
                }

                Guid remappedFromTestResultId = ResolveFromTestResultId(resolvedFromTestResultId);

                Guid sourceId = resolvedFromTestResultId != Guid.Empty
                    ? resolvedFromTestResultId
                    : entry.testresultid;

                var labProp = entry.labproperties?.FirstOrDefault(lp => lp.testresultid == entry.testresultid);

                allTestProps.TryGetValue(entry.testresultid, out var testProp);

                int rtmCode = testProp?.rtmcode ?? 0;
                if (rtmCode == 0 && entry.rtmcode > 0)
                    rtmCode = entry.rtmcode;
                if (rtmCode == 0 && entry.testresultid != Guid.Empty)
                    rtmFallbackMap.TryGetValue(entry.testresultid, out rtmCode);

                string resolvedDefaultValue = !string.IsNullOrWhiteSpace(entry.defaultunitvalue)
                    ? Truncate(entry.defaultunitvalue, 500)
                    : Truncate(testProp?.defaultvalue, 500);

                string resolvedNormalValue = !string.IsNullOrWhiteSpace(entry.normalvalue)
                    ? Truncate(entry.normalvalue, 8000)
                    : Truncate(testProp?.normalvalue, 8000);

                var propsParam = new
                {
                    TestResultID = labId,
                    MasterTestResultId = entry.testresultid,
                    ResultValueType = Truncate(entry.resultvaluetype, 50),
                    DefaultUnitsCode = entry.defaultunitscode,
                    FXTCode = entry.fxtcode == Guid.Empty ? (Guid?)null : entry.fxtcode,
                    DefaultValueforFXType = entry.defaultvalueforfxtype == Guid.Empty ? (Guid?)null : entry.defaultvalueforfxtype,
                    SCode = entry.scode,
                    MCCode = entry.mccode,
                    SimpleNormalValues = entry.simplenv,
                    DetailedNormalValues = entry.detailednv,
                    ResultNormal = entry.resultnormal,
                    ResultHigh = entry.resulthigh,
                    ResultLow = entry.resultlow,
                    DefaultValue = resolvedDefaultValue,
                    NormalValue = resolvedNormalValue,
                    RTMCode = rtmCode,
                    RangeType = Truncate(labProp?.rangetype ?? testProp?.rangetype ?? "Normal", 50),
                    FromNormalValue = labProp?.fromnormalvalue ?? testProp?.fromnormalvalue ?? 0.0,
                    ToNormalValue = labProp?.tonormalvalue ?? testProp?.tonormalvalue ?? 0.0,
                    ConclusionForHigher = !string.IsNullOrWhiteSpace(labProp?.conclusionforhigher)
                        ? labProp!.conclusionforhigher
                        : testProp?.conclusionforhigher ?? string.Empty,
                    ConclusionForLower = !string.IsNullOrWhiteSpace(labProp?.conclusionforlower)
                        ? labProp!.conclusionforlower
                        : testProp?.conclusionforlower ?? string.Empty,
                    PrintFixedTextConclusionInReport = labProp?.printfixedtextconclusioninreport
                        ?? testProp?.printfixedtextconclusioninreport ?? false,
                    ConclusionforFixedText = !string.IsNullOrWhiteSpace(entry.fixedvalues)
                        ? Truncate(entry.fixedvalues, 4000)
                        : (!string.IsNullOrWhiteSpace(labProp?.conclusionforfixedtext)
                            ? labProp!.conclusionforfixedtext
                            : testProp?.conclusionforfixedtext ?? string.Empty),
                    ShowAgedBased = labProp?.showagedbased ?? testProp?.showagedbased ?? false,
                    PrintConclusionInReport = labProp?.printconclusioninreport ?? testProp?.printconclusioninreport ?? false,
                    PrintConclusionInBottom = labProp?.printconclusioninbottom ?? testProp?.printconclusioninbottom ?? false,
                    ShowAlertOnHigherLower = labProp?.showalertonhigherlower ?? testProp?.showalertonhigherlower ?? false,
                    IsAddResult = labProp?.isaddresult ?? testProp?.isaddresult ?? false,
                    PrintUnitsInNormalValues = labProp?.printunitsinnormalvalues ?? testProp?.printunitsinnormalvalues ?? false,
                    PrintNormalValuesatBottom = labProp?.printnormalvaluesatbottom ?? testProp?.printnormalvaluesatbottom ?? false,
                    PrintSpecialFieldsatRightSide = labProp?.printspecialfieldsatrightside ?? testProp?.printspecialfieldsatrightside ?? false,
                    GroupValuesbySex = labProp?.groupvaluesbysex ?? testProp?.groupvaluesbysex ?? false,
                    GroupValuesbySpecialField = labProp?.groupvaluesbyspecialfield ?? testProp?.groupvaluesbyspecialfield ?? false,
                    FooterMessage = !string.IsNullOrWhiteSpace(labProp?.footermessage)
                        ? labProp!.footermessage
                        : testProp?.footermessage ?? string.Empty,
                    PrintResultOnly = labProp?.printresultonly ?? testProp?.printresultonly ?? false,
                    IsGraph = labProp?.isgraph ?? testProp?.isgraph ?? false,
                    GraphValue = labProp?.graphvalue ?? testProp?.graphvalue ?? 0.0,
                    DecimalValue = labProp?.decimalvalue ?? testProp?.decimalvalue ?? 2,
                    CriticalLowType = !string.IsNullOrWhiteSpace(labProp?.criticallowtype)
                        ? labProp!.criticallowtype
                        : testProp?.criticallowtype ?? string.Empty,
                    CriticalLowRange = !string.IsNullOrWhiteSpace(labProp?.criticallowrange)
                        ? labProp!.criticallowrange
                        : testProp?.criticallowrange ?? string.Empty,
                    CriticalHighType = !string.IsNullOrWhiteSpace(labProp?.criticalhightype)
                        ? labProp!.criticalhightype
                        : testProp?.criticalhightype ?? string.Empty,
                    CriticalHighRange = !string.IsNullOrWhiteSpace(labProp?.criticalhighrange)
                        ? labProp!.criticalhighrange
                        : testProp?.criticalhighrange ?? string.Empty,
                    TenantCode = tenantCode,
                    // Proof image: uploaded to MinIO; NULL when no image supplied.
                    imagepath = await ResolveProofImagePathAsync(entry, labId, tenantCode, db, transaction)
                };

                bool detailExists = isUpdate && existingMasterIds.Contains(entry.testresultid);

                if (detailExists)
                {
                    await db.ExecuteAsync(@"
                UPDATE lab_result_details
                SET    testsno           = @TestSno,
                       description       = @Description,
                       enteredresult     = @EnteredResult,
                       units             = @Units,
                       valuetype         = @ValueType,
                       resulttype        = @ResultType,
                       calculatedformula = @CalculatedFormula,
                       fromtcode         = @FromTCode,
                       fromtestresultid  = @FromTestResultID
                WHERE  testresultid = @TestResultID
                  AND  tenant_code  = @TenantCode",
                        new
                        {
                            TestResultID = labId,
                            TestSno = entry.slno,
                            Description = Truncate(entry.col2, 4000),
                            EnteredResult = Truncate(entry.enteredresult, 500),
                            Units = Truncate(entry.unitname, 50),
                            ValueType = Truncate(entry.resultvaluetype, 50),
                            ResultType = Truncate(entry.resulttype, 50),
                            CalculatedFormula = Truncate(ResolveFormula(entry.calculatedformula), 400),
                            FromTCode = resolvedFromTCode,
                            FromTestResultID = remappedFromTestResultId == Guid.Empty
                                                    ? (Guid?)null
                                                    : remappedFromTestResultId,
                            TenantCode = tenantCode
                        }, transaction);

                    await db.ExecuteAsync(@"
                UPDATE lab_result_properties
                SET    mastertestresultid               = @MasterTestResultId,
                       resultvaluetype                  = @ResultValueType,
                       defaultunitscode                 = @DefaultUnitsCode,
                       fxtcode                          = @FXTCode,
                       defaultvalueforfxtype            = @DefaultValueforFXType,
                       scode                            = @SCode,
                       mccode                           = @MCCode,
                       simplenormalvalues               = @SimpleNormalValues,
                       detailednormalvalues             = @DetailedNormalValues,
                       resultnormal                     = @ResultNormal,
                       resulthigh                       = @ResultHigh,
                       resultlow                        = @ResultLow,
                       defaultvalue                     = @DefaultValue,
                       normalvalue                      = @NormalValue,
                       rtmcode                          = @RTMCode,
                       rangetype                        = @RangeType,
                       fromnormalvalue                  = @FromNormalValue,
                       tonormalvalue                    = @ToNormalValue,
                       conclusionforhigher              = @ConclusionForHigher,
                       conclusionforlower               = @ConclusionForLower,
                       printfixedtextconclusioninreport = @PrintFixedTextConclusionInReport,
                       conclusionforfixedtext           = @ConclusionforFixedText,
                       showagedbased                    = @ShowAgedBased,
                       printconclusioninreport          = @PrintConclusionInReport,
                       printconclusioninbottom          = @PrintConclusionInBottom,
                       showalertonhigherlower           = @ShowAlertOnHigherLower,
                       isaddresult                      = @IsAddResult,
                       printunitsinnormalvalues         = @PrintUnitsInNormalValues,
                       printnormalvaluesatbottom        = @PrintNormalValuesatBottom,
                       printspecialfieldsatrightside    = @PrintSpecialFieldsatRightSide,
                       groupvaluesbysex                 = @GroupValuesbySex,
                       groupvaluesbyspecialfield        = @GroupValuesbySpecialField,
                       footermessage                    = @FooterMessage,
                       printresultonly                  = @PrintResultOnly,
                       isgraph                          = @IsGraph,
                       graphvalue                       = @GraphValue,
                       decimalvalue                     = @DecimalValue,
                       criticallowtype                  = @CriticalLowType,
                       criticallowrange                 = @CriticalLowRange,
                       criticalhightype                 = @CriticalHighType,
                       criticalhighrange                = @CriticalHighRange,
                       image_path                       = COALESCE(@ImagePath, image_path)
                WHERE  testresultid = @TestResultID
                  AND  tenant_code  = @TenantCode",
                        propsParam, transaction);

                    await db.ExecuteAsync(@"
                DELETE FROM lab_result_textnormalvalues
                WHERE  testresultid = @Id AND tenant_code = @TenantCode;
                DELETE FROM lab_result_detailednormalvalues
                WHERE  testresultid = @Id AND tenant_code = @TenantCode;
                DELETE FROM lab_result_calculatedformula
                WHERE  testresultid = @Id AND tenant_code = @TenantCode;",
                        new { Id = labId, TenantCode = tenantCode }, transaction);
                }
                else
                {
                    await db.ExecuteAsync(@"
                INSERT INTO lab_result_details
                    (resultguid, testresultid, sendsms, smsshortname, tcode, testsno,
                     description, quotescolumn, enteredresult, units, normalvalues,
                     defaultnormalvalues, dstylecode, qstylecode, estylecode, ustylecode,
                     nstylecode, valuetype, resulttype, calculatedformula,
                     fromtcode, fromtestresultid, lrdid, tenant_code)
                VALUES
                    (@ResultGUID, @TestResultID, false, '', @TCode, @TestSno,
                     @Description, @QuotesColumn, @EnteredResult, @Units, '',
                     '', 0, 0, 0, 0,
                     0, @ValueType, @ResultType, @CalculatedFormula,
                     @FromTCode, @FromTestResultID, @LRDID, @TenantCode)",
                        new
                        {
                            ResultGUID = Guid.Parse(resultGuid),
                            TestResultID = labId,
                            TCode = entry.tcode,
                            TestSno = entry.slno,
                            Description = Truncate(entry.col2, 4000),
                            QuotesColumn = Truncate(entry.col2, 4000),
                            EnteredResult = Truncate(entry.enteredresult, 500),
                            Units = Truncate(entry.unitname, 50),
                            ValueType = Truncate(entry.resultvaluetype, 50),
                            ResultType = Truncate(entry.resulttype, 50),
                            CalculatedFormula = Truncate(ResolveFormula(entry.calculatedformula), 400),
                            FromTCode = resolvedFromTCode,
                            FromTestResultID = remappedFromTestResultId == Guid.Empty
                                                    ? (Guid?)null
                                                    : remappedFromTestResultId,
                            LRDID = Guid.NewGuid(),
                            TenantCode = tenantCode
                        }, transaction);

                    await db.ExecuteAsync(@"
                INSERT INTO lab_result_properties
                    (trpid, testresultid, mastertestresultid,
                     resultvaluetype, defaultunitscode, fxtcode,
                     defaultvalueforfxtype, defaultvalue,
                     simplenormalvalues, detailednormalvalues,
                     rangetype, fromnormalvalue, tonormalvalue,
                     conclusionforhigher, conclusionforlower,
                     printfixedtextconclusioninreport, conclusionforfixedtext,
                     showagedbased, printconclusioninreport, printconclusioninbottom,
                     showalertonhigherlower, isaddresult,
                     printunitsinnormalvalues, printnormalvaluesatbottom,
                     printspecialfieldsatrightside, groupvaluesbysex,
                     groupvaluesbyspecialfield, footermessage, rtmcode,
                     printresultonly, resultnormal, resulthigh, resultlow,
                     isgraph, graphvalue, decimalvalue,
                     scode, mccode, performedcount, usedefault,
                     normalvalueforfxtype, normalvalue,
                     criticallowtype, criticallowrange,
                     criticalhightype, criticalhighrange, image_path, tenant_code)
                VALUES
                    (@TRPId, @TestResultID, @MasterTestResultId,
                     @ResultValueType, @DefaultUnitsCode, @FXTCode,
                     @DefaultValueforFXType, @DefaultValue,
                     @SimpleNormalValues, @DetailedNormalValues,
                     @RangeType, @FromNormalValue, @ToNormalValue,
                     @ConclusionForHigher, @ConclusionForLower,
                     @PrintFixedTextConclusionInReport, @ConclusionforFixedText,
                     @ShowAgedBased, @PrintConclusionInReport, @PrintConclusionInBottom,
                     @ShowAlertOnHigherLower, @IsAddResult,
                     @PrintUnitsInNormalValues, @PrintNormalValuesatBottom,
                     @PrintSpecialFieldsatRightSide, @GroupValuesbySex,
                     @GroupValuesbySpecialField, @FooterMessage, @RTMCode,
                     @PrintResultOnly, @ResultNormal, @ResultHigh, @ResultLow,
                     @IsGraph, @GraphValue, @DecimalValue,
                     @SCode, @MCCode, 1, true,
                     @NormalValueForFxType, @NormalValue,
                     @CriticalLowType, @CriticalLowRange,
                     @CriticalHighType, @CriticalHighRange, @ImagePath, @TenantCode)",
                        new
                        {
                            TRPId = Guid.NewGuid(),
                            propsParam.TestResultID,
                            propsParam.MasterTestResultId,
                            propsParam.ResultValueType,
                            propsParam.DefaultUnitsCode,
                            propsParam.FXTCode,
                            propsParam.DefaultValueforFXType,
                            propsParam.DefaultValue,
                            propsParam.SimpleNormalValues,
                            propsParam.DetailedNormalValues,
                            propsParam.RangeType,
                            propsParam.FromNormalValue,
                            propsParam.ToNormalValue,
                            propsParam.ConclusionForHigher,
                            propsParam.ConclusionForLower,
                            propsParam.PrintFixedTextConclusionInReport,
                            propsParam.ConclusionforFixedText,
                            propsParam.ShowAgedBased,
                            propsParam.PrintConclusionInReport,
                            propsParam.PrintConclusionInBottom,
                            propsParam.ShowAlertOnHigherLower,
                            propsParam.IsAddResult,
                            propsParam.PrintUnitsInNormalValues,
                            propsParam.PrintNormalValuesatBottom,
                            propsParam.PrintSpecialFieldsatRightSide,
                            propsParam.GroupValuesbySex,
                            propsParam.GroupValuesbySpecialField,
                            propsParam.FooterMessage,
                            propsParam.RTMCode,
                            propsParam.PrintResultOnly,
                            propsParam.ResultNormal,
                            propsParam.ResultHigh,
                            propsParam.ResultLow,
                            propsParam.IsGraph,
                            propsParam.GraphValue,
                            propsParam.DecimalValue,
                            propsParam.SCode,
                            propsParam.MCCode,
                            NormalValueForFxType = Guid.Empty,
                            propsParam.NormalValue,
                            propsParam.CriticalLowType,
                            propsParam.CriticalLowRange,
                            propsParam.CriticalHighType,
                            propsParam.CriticalHighRange,
                            propsParam.imagepath,
                            propsParam.TenantCode
                        }, transaction);
                }

                bool needsTNV = entry.simplenv
                                || entry.resultvaluetype == "TN"
                                || entry.resultvaluetype == "Text"
                                || entry.textnormalvalues?.Count > 0;

                if (needsTNV)
                {
                    if (entry.textnormalvalues?.Count > 0)
                    {
                        tnvList.AddRange(entry.textnormalvalues.Select(row => new
                        {
                            TRTID = Guid.NewGuid(),
                            TestResultID = labId,
                            Sex = Truncate(row.sex, 10),
                            NormalValue = Truncate(row.normalvalue, 500),
                            MCCode = row.mccode,
                            PerformedCount = row.performedcount,
                            SCode = row.scode,
                            TenantCode = tenantCode
                        }));
                    }
                    else
                    {
                        tnvList.AddRange(allTextNV[sourceId].Select(row => new
                        {
                            TRTID = Guid.NewGuid(),
                            TestResultID = labId,
                            Sex = Truncate(row.sex, 10),
                            NormalValue = Truncate(row.normalvalue, 500),
                            MCCode = entry.mccode,
                            PerformedCount = row.performedcount ?? 1,
                            SCode = entry.scode,
                            TenantCode = tenantCode
                        }));
                    }
                }

                if (entry.detailednv || entry.detailednormalvalues?.Count > 0)
                {
                    if (entry.detailednormalvalues?.Count > 0)
                    {
                        dnvList.AddRange(entry.detailednormalvalues.Select(row => new
                        {
                            TRDNID = Guid.NewGuid(),
                            TestResultID = labId,
                            Sno = row.sno,
                            AgeFrom = row.agefrom,
                            AgeFromType = Truncate(row.agefromtype, 20),
                            AgeTo = row.ageto,
                            AgeToType = Truncate(row.agetotype, 20),
                            Sex = Truncate(row.sex, 10),
                            RangeType = Truncate(row.rangetype, 50),
                            RangeFrom = row.rangefrom,
                            RangeTo = row.rangeto,
                            SpecialConditionCode = row.specialconditioncode,
                            AgeRangeType = Truncate(row.agerangetype, 20),
                            MCCode = row.mccode,
                            PerformedCount = row.performedcount,
                            SCode = row.scode,
                            TenantCode = tenantCode
                        }));
                    }
                    else
                    {
                        dnvList.AddRange(allDetailedNV[sourceId].Select(row => new
                        {
                            TRDNID = Guid.NewGuid(),
                            TestResultID = labId,
                            Sno = row.sno ?? 0,
                            AgeFrom = row.agefrom ?? 0,
                            AgeFromType = Truncate(row.agefromtype, 20),
                            AgeTo = row.ageto ?? 0,
                            AgeToType = Truncate(row.agetotype, 20),
                            Sex = Truncate(row.sex, 10),
                            RangeType = Truncate(row.rangetype, 50),
                            RangeFrom = row.rangefrom ?? 0.0,
                            RangeTo = row.rangeto ?? 0.0,
                            SpecialConditionCode = row.specialconditioncode,
                            AgeRangeType = Truncate(row.agerangetype, 20),
                            MCCode = entry.mccode,
                            PerformedCount = row.performedcount ?? 1,
                            SCode = entry.scode,
                            TenantCode = tenantCode
                        }));
                    }
                }

                if (entry.calculatedformulas?.Count > 0)
                {
                    cfList.AddRange(entry.calculatedformulas.Select(row => new
                    {
                        TRCFID = Guid.NewGuid(),
                        TestResultID = labId,
                        Sex = Truncate(row.sex, 10),
                        CalculatedFormula = Truncate(ResolveFormula(row.calculatedformula), 400),
                        MCCode = row.mccode,
                        PerformedCount = row.performedcount,
                        SCode = row.scode,
                        TenantCode = tenantCode
                    }));
                }
                else if (entry.resultvaluetype == "Calculated Value"
                     || !string.IsNullOrEmpty(entry.calculatedformula))
                {
                    var formulaRows = allFormulas[sourceId].ToList();
                    if (formulaRows.Count == 0 && sourceId != entry.testresultid)
                        formulaRows = allFormulas[entry.testresultid].ToList();

                    cfList.AddRange(formulaRows.Select(row => new
                    {
                        TRCFID = Guid.NewGuid(),
                        TestResultID = labId,
                        Sex = Truncate(row.sex, 10),
                        CalculatedFormula = Truncate(ResolveFormula(row.calculatedformula), 400),
                        MCCode = entry.mccode,
                        PerformedCount = row.performedcount ?? 1,
                        SCode = entry.scode,
                        TenantCode = tenantCode
                    }));
                }

                int rowsUpdated = await db.ExecuteAsync(@"
            UPDATE lab_request_details
            SET    resultstatus        = true,
                   resultenteredby     = @ResultEnteredBy,
                   resultentereddate   = @ResultEnteredDate,
                   isauthorized1       = @IsAuthorized1,
                   isauthorized2       = @IsAuthorized2,
                   resultauthorizedby  = @ResultAuthorizedBy,
                   resultauthorizedby2 = @ResultAuthorizedBy2,
                   firstauthorizedate  = @FirstAuthorizeDate,
                   secondauthorizedate = @SecondAuthorizeDate
            WHERE  requestguid = @RequestGUID
              AND  tcode = @TCode
              AND  tenant_code = @TenantCode",
                    new
                    {
                        RequestGUID = entry.requestguid,
                        TCode = Convert.ToDecimal(entry.tcode),
                        ResultEnteredBy = entry.resultenteredby,
                        ResultEnteredDate = DateTime.UtcNow,
                        IsAuthorized1 = entry.isauthorized1,
                        IsAuthorized2 = entry.isauthorized2,
                        ResultAuthorizedBy = entry.isauthorized1 ? entry.resultauthorizedby : 0,
                        ResultAuthorizedBy2 = entry.isauthorized2 ? entry.resultauthorizedby2 : 0,
                        FirstAuthorizeDate = entry.isauthorized1 ? (DateTime?)DateTime.UtcNow : null,
                        SecondAuthorizeDate = entry.isauthorized2 ? (DateTime?)DateTime.UtcNow : null,
                        TenantCode = tenantCode
                    }, transaction);
                if (rowsUpdated == 0)
                {
                    Console.WriteLine($"[SaveResult] WARNING — no lab_request_details row matched " +
                    $"requestguid={requestGuid}, tcode={entry.tcode}, tenant={tenantCode}. " +
                    $"resultstatus was NOT updated for this test.");
                }
            }

            foreach (var row in tnvList)
            {
                await db.ExecuteAsync(@"
            INSERT INTO lab_result_textnormalvalues
                (trtid, testresultid, sex, normalvalue, mccode, performedcount, scode, tenant_code)
            VALUES
                (@TRTID, @TestResultID, @Sex, @NormalValue, @MCCode, @PerformedCount, @SCode, @TenantCode)",
                    row, transaction);
            }

            foreach (var row in dnvList)
            {
                await db.ExecuteAsync(@"
            INSERT INTO lab_result_detailednormalvalues
                (trdnid, testresultid, sno, agefrom, agefromtype, ageto, agetotype,
                 sex, rangetype, rangefrom, rangeto, specialconditioncode,
                 agerangetype, mccode, performedcount, scode, tenant_code)
            VALUES
                (@TRDNID, @TestResultID, @Sno, @AgeFrom, @AgeFromType, @AgeTo, @AgeToType,
                 @Sex, @RangeType, @RangeFrom, @RangeTo, @SpecialConditionCode,
                 @AgeRangeType, @MCCode, @PerformedCount, @SCode, @TenantCode)",
                    row, transaction);
            }

            foreach (var row in cfList)
            {
                await db.ExecuteAsync(@"
            INSERT INTO lab_result_calculatedformula
                (trcfid, testresultid, sex, calculatedformula, mccode, performedcount, scode, tenant_code)
            VALUES
                (@TRCFID, @TestResultID, @Sex, @CalculatedFormula, @MCCode, @PerformedCount, @SCode, @TenantCode)",
                    row, transaction);
            }

            int pendingCount = await db.QueryFirstOrDefaultAsync<int>(@"
        SELECT COUNT(*)
        FROM   lab_request_details
        WHERE  requestguid  = @RequestGUID
          AND  resultstatus = false
          AND  tenant_code  = @TenantCode",
                new { RequestGUID = requestGuid, TenantCode = tenantCode }, transaction);

            if (pendingCount == 0)
            {
                var authCounts = await db.QueryFirstOrDefaultAsync<(int notAuth1, int notAuth2)>(@"
            SELECT
                COUNT(*) FILTER (WHERE isauthorized1 = false) AS notAuth1,
                COUNT(*) FILTER (WHERE isauthorized2 = false) AS notAuth2
            FROM   lab_request_details
            WHERE  requestguid = @RequestGUID
              AND  tenant_code = @TenantCode",
                    new { RequestGUID = requestGuid, TenantCode = tenantCode }, transaction);

                await db.ExecuteAsync(@"
            UPDATE lab_request_master
            SET    resultstatus                = true,
                   isinvestigationauthorized1 = @IsAuth1,
                   isinvestigationauthorized2 = @IsAuth2
            WHERE  requestguid = @RequestGUID
              AND  tenant_code = @TenantCode",
                    new
                    {
                        RequestGUID = requestGuid,
                        IsAuth1 = authCounts.notAuth1 == 0,
                        IsAuth2 = authCounts.notAuth2 == 0,
                        TenantCode = tenantCode
                    }, transaction);
            }
        }

        public async Task<IList<ViewResultSearch>> GetResultList(
            int dcode, string sd, string ed, string tenantCode)
        {
            if (!DateTime.TryParse(sd, out DateTime startDate) ||
                !DateTime.TryParse(ed, out DateTime endDate))
                return new List<ViewResultSearch>();

            DateTimeOffset startOffset = new DateTimeOffset(startDate.Date, TimeSpan.Zero);
            DateTimeOffset endOffset = new DateTimeOffset(endDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

            try
            {
                await using var db = CreateConnection();
                await db.OpenAsync();

                const string sql = @"
                    SELECT *
                    FROM   viewresultsearch
                    WHERE  tenant_code      = @TenantCode
                      AND  (@DCode = 0 OR dcode = @DCode)
                      AND  requestdatetime >= @StartDate
                      AND  requestdatetime <= @EndDate";

                Console.WriteLine($"[GetResultList] TenantCode='{tenantCode}' DCode={dcode} Start={startOffset} End={endOffset}");

                var result = await db.QueryAsync<ViewResultSearch>(sql, new
                {
                    TenantCode = tenantCode,
                    DCode = dcode,
                    StartDate = startOffset,
                    EndDate = endOffset
                });

                return result.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetResultList] ERROR: {ex.Message}\n{ex}");
                return new List<ViewResultSearch>();
            }
        }

        public async Task<List<List<CustomerResultDto>>> GetCustomerResultsLoading(
    string custcode, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(custcode))
                throw new ArgumentException("Customer code cannot be null or empty.", nameof(custcode));

            if (string.IsNullOrWhiteSpace(tenantCode))
                throw new ArgumentException("Tenant code cannot be null or empty.", nameof(tenantCode));

            try
            {
                // ✅ UPDATED: use CustomerClass to resolve custid by tenant custcode
                var customer = await _customerClass.GetCustomerByCustCode(custcode, tenantCode);
                if (customer == null)
                    return new List<List<CustomerResultDto>>();

                var custid = customer.custid;

                await using var db = CreateConnection();
                await db.OpenAsync();

                const string sql = @"
                    WITH Req AS(
                        SELECT r.requestguid,
                                r.requestdatetime
                        FROM    lab_request_master r
                        INNER JOIN lab_result_master rm ON rm.requestguid = r.requestguid
                                                        AND rm.tenant_code = @TenantCode
                        WHERE r.custid = @CustId
                          AND r.tenant_code = @TenantCode
                    )
                    SELECT
                        CAST(req.requestguid AS VARCHAR)  AS requestguid,
                        req.requestdatetime AS date,
                        d.description AS TestName,
                        d.enteredresult AS Result
                    FROM    Req req
                    INNER JOIN lab_result_master rm  ON rm.requestguid = req.requestguid
                                                        AND rm.tenant_code = @TenantCode
                    INNER JOIN lab_result_details d   ON d.resultguid = rm.resultguid
                                                        AND d.tenant_code = @TenantCode
                    INNER JOIN lab_request_details lrd ON lrd.requestguid = req.requestguid
                                                        AND lrd.tcode = d.tcode
                                                        AND lrd.ttid = 1
                                                        AND lrd.tenant_code = @TenantCode
                    ORDER BY req.requestdatetime DESC, d.description ASC";

                var flat = (await db.QueryAsync<CustomerResultDto>(sql, new
                {
                    CustId = custid,
                    TenantCode = tenantCode
                })).ToList();

                var grouped = flat
                    .GroupBy(r => r.requestguid)
                    .Select(g => g.ToList())
                    .ToList();

                return grouped;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCustomerResultsLoading] ERROR: {ex.Message}\n{ex}");
                throw;
            }
        }
        /// <summary>
        /// Resolves the MinIO image path for a result entry's proof image.
        /// Called inside ProcessResultGroupAsync before building propsParam.
        /// Handles IFormFile, base64 string, or no image (returns null).
        /// When an image already exists in lab_result_properties, the old MinIO
        /// object is deleted via S3ImageService.ReplaceAsync before uploading the new one.
        /// </summary>
        /// <summary>
        /// Resolves the MinIO image path for a result entry's proof image.
        /// Called inside ProcessResultGroupAsync before building propsParam.
        /// Handles IFormFile only. When an image already exists in lab_result_properties,
        /// the old MinIO object is replaced via S3ImageService.ReplaceAsync.
        /// </summary>
        private async Task<string?> ResolveProofImagePathAsync(
            LabResultEntry entry,
            Guid labId,
            string tenantCode,
            NpgsqlConnection db,
            NpgsqlTransaction transaction)
        {
            bool hasFile = entry.image_file != null && entry.image_file.Length > 0;

            if (!hasFile)
                return null;

            string? oldKey = await db.QueryFirstOrDefaultAsync<string?>(@"
        SELECT image_path
        FROM   lab_result_properties
        WHERE  testresultid = @LabId
          AND  tenant_code  = @TenantCode
        LIMIT 1",
                new { LabId = labId, TenantCode = tenantCode }, transaction);

            // ── Build date-partitioned entityType + custom prefix ──
            var now = DateTime.UtcNow;
            string datedEntityType = $"result-proof/{now:yyyy}/{now:MM}/{now:dd}";
            string customPrefix = $"{entry.requestguid}proof{entry.slno}";

            try
            {
                return await _s3Service.ReplaceAsync(
                    entry.image_file!, oldKey, tenantCode, datedEntityType, 0, customPrefix);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResolveProofImagePathAsync] ERROR: {ex.Message}");
                return oldKey;
            }
        }
    }
}
