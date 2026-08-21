using Amazon.Runtime;
using Amazon.S3.Model;
using Dapper;
using medico_backend.Model;
using Medico_Backend.Model;
using medico_backend.Services;
using Npgsql;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Data;
using System.Text;
using System.Text.Json;

namespace medico_backend.Class
{
    public class ReportClass
    {
        private readonly IConfiguration _config;
        private readonly string _conn;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient client;
        private readonly S3ImageService _s3Service;

        public ReportClass(IConfiguration config, IHttpClientFactory httpClientFactory, S3ImageService s3Service)
        {
            _config = config;
            _conn = _config.GetConnectionString("conn");
            _httpClientFactory = httpClientFactory;
            client = _httpClientFactory.CreateClient("ReportServer");
            _s3Service = s3Service;
        }

        public async Task<string?> StatementPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(_conn))
                {
                    string sql = @"
                        SELECT
                            lrm.requestsno                              AS sampleid,
                            DATE(lrm.requestdatetime)::timestamp        AS date,

                            COALESCE(cm.custcode, '')                   AS custcode,
                            lrm.name                                    AS patientname,
                            lrm.mobileno                                AS mobile,
                            COALESCE(dm.name, '')                       AS referral,

                            -- TOTALBILLEDAMOUNT: gross before discount = requestamount
                            COALESCE(lrm.requestamount, 0)              AS billedamount,

                            -- NETAMOUNT: already stored as net in DB = totalamount
                            COALESCE(lrm.totalamount, 0)::text          AS netamount,

                            -- PAIDAMOUNT: actual cash received
                            COALESCE(lrm.paidamount, 0)::text           AS paidamount,

                            -- BALANCEAMOUNT: net - paid - refund - dueconcession
                            COALESCE(
                                lrm.totalamount
                                - COALESCE(lrm.paidamount, 0)
                                - 0
                                - 0,
                            0)::text                                    AS balanceamount,

                            -- DISCOUNTAMOUNT: all pre-billing reductions
                            COALESCE(
                                COALESCE(lrm.discountamount, 0)
                                + COALESCE(lrm.ourdiscount, 0)
                                + COALESCE(lrm.specialdiscount, 0),
                            0)::text                                    AS discountamount

                        FROM lab_request_master lrm

                        LEFT JOIN customerdb.customer_master cm
                            ON cm.custid = lrm.custid

                        LEFT JOIN doctor_master dm
                            ON dm.dcode = lrm.dcode

                        WHERE lrm.tenant_code = @tenant_code
                            AND COALESCE(lrm.deleted, false) = false
                            AND lrm.requestdatetime >= @fromdate
                            AND lrm.requestdatetime <  @todate + INTERVAL '1 day'

                        ORDER BY lrm.requestdatetime;
                    ";

                    var statementRows = (await db.QueryAsync<StatementsModel>(
                                sql,
                                new { fromdate, todate, tenant_code }
                            )).ToList();

                    // ─── Step 2: Company info ─────────────────────────────────────────────
                    string sql1 = @"
                    SELECT legal_name, address_line1, contact_number, contact_email
                    FROM mastertenant.tenants
                    WHERE tenant_code = @tenant_code
                ";

                    var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                        sql1,
                        new { tenant_code }
                    );

                    // ─── Step 3: Build payload ────────────────────────────────────────────
                    var payload = new StatementRequest
                    {
                        statements = statementRows,
                        fromdate = fromdate,
                        todate = todate,
                        CompanyName = companyInfo?.legal_name,
                        CompanyAddress = companyInfo?.address_line1,
                        CompanyContactNo = companyInfo?.contact_number,
                        CompanyEmail = companyInfo?.contact_email
                    };

                    var client = _httpClientFactory.CreateClient("ReportServer");

                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("/api/statement/getstatement", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Report server error {response.StatusCode}: {error}");
                    }

                    // ✅ Read as string — report server returns base64
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SummaryClass.Summary: {ex.Message}");
                throw ex;
            }
        }

        public async Task<string?> DueStatementPDF(
            DateTime fromdate,
            DateTime todate,
            string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        lrm.requestsno AS sampleid,
        DATE(lrm.requestdatetime)::timestamp AS date,

        COALESCE(cm.custcode,'') AS custcode,
        lrm.name AS patientname,
        lrm.mobileno AS mobile,
        COALESCE(dm.name,'') AS referral,

        COALESCE(lrm.requestamount,0) AS billedamount,
        COALESCE(lrm.totalamount,0)::text AS netamount,
        COALESCE(lrm.paidamount,0)::text AS paidamount,

        COALESCE(
            lrm.totalamount
            - COALESCE(lrm.paidamount,0)
            - 0
            - 0,
        0)::text AS balanceamount,

        COALESCE(
            COALESCE(lrm.discountamount,0)
            + COALESCE(lrm.ourdiscount,0)
            + COALESCE(lrm.specialdiscount,0),
        0)::text AS discountamount

    FROM lab_request_master lrm

    LEFT JOIN customerdb.customer_master cm
        ON cm.custid = lrm.custid

    LEFT JOIN doctor_master dm
        ON dm.dcode = lrm.dcode

    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted,false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'

      AND (
            lrm.totalamount
            - COALESCE(lrm.paidamount,0)
            - 0
            - 0
          ) > 0

    ORDER BY lrm.requestdatetime";

                var rows = (await db.QueryAsync<StatementsModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new StatementRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email,
                    statementtype = "Due" // ✅ Set the statement type to "Due"
                };

                var client = _httpClientFactory.CreateClient("ReportServer");

                var response = await client.PostAsync(
                    "/api/statement/GetStatement", // ✅ Using the unified endpoint
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> DiscountStatementPDF(
            DateTime fromdate,
            DateTime todate,
            string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        lrm.requestsno AS sampleid,
        DATE(lrm.requestdatetime)::timestamp AS date,

        COALESCE(cm.custcode,'') AS custcode,
        lrm.name AS patientname,
        lrm.mobileno AS mobile,
        COALESCE(dm.name,'') AS referral,

        COALESCE(lrm.requestamount,0) AS billedamount,
        COALESCE(lrm.totalamount,0)::text AS netamount,
        COALESCE(lrm.paidamount,0)::text AS paidamount,

        COALESCE(
            lrm.totalamount
            - COALESCE(lrm.paidamount,0)
            - 0
            - 0,
        0)::text AS balanceamount,

        COALESCE(
            COALESCE(lrm.discountamount,0)
            + COALESCE(lrm.ourdiscount,0)
            + COALESCE(lrm.specialdiscount,0),
        0)::text AS discountamount

    FROM lab_request_master lrm

    LEFT JOIN customerdb.customer_master cm
        ON cm.custid = lrm.custid

    LEFT JOIN doctor_master dm
        ON dm.dcode = lrm.dcode

    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted,false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'

      AND (
            COALESCE(lrm.discountamount,0)
            + COALESCE(lrm.ourdiscount,0)
            + COALESCE(lrm.specialdiscount,0)
          ) > 0

    ORDER BY lrm.requestdatetime";

                var rows = (await db.QueryAsync<StatementsModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new StatementRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email,
                    statementtype = "Discount" // ✅ Set the statement type to "Discount"
                };

                var client = _httpClientFactory.CreateClient("ReportServer");

                var response = await client.PostAsync(
                    "/api/statement/GetStatement", // ✅ Using the unified endpoint
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> ReferralStatementPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        lrm.requestdatetime                                     AS date,
        COALESCE(dm.name, 'SELF')                               AS referral,
        lrm.requestsno                                          AS patientid,
        lrm.name                                                AS patientname,
        lrm.mobileno                                            AS mobile,
        tm.name                                                 AS testname,
        COALESCE(lrd.testamount, 0)                             AS price,
        lrm.requestguid                                         AS requestguid,
        COALESCE(cm.custcode, '')                               AS custcode,
        COALESCE(lrm.discountamount, 0)
        + COALESCE(lrm.ourdiscount, 0)
        + COALESCE(lrm.specialdiscount, 0)                      AS discountamount,
        COALESCE(lrm.totalamount, 0)                            AS netamount,
        COALESCE(lrm.paidamount, 0)                             AS paidamount,
        (COALESCE(lrm.totalamount, 0) 
         - COALESCE(lrm.paidamount, 0) 
         - 0 
         - 0)                      AS balanceamount
    FROM lab_request_master lrm
    INNER JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
    INNER JOIN test_master tm ON tm.tcode = lrd.tcode
    LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode
    LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted, false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
    ORDER BY referral, date, patientname";

                var rows = (await db.QueryAsync<ReferralStatementModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new ReferralStatementRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");

                var response = await client.PostAsync(
                    "/api/statement/GetReferralStatement",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> GroupStatementPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        lrm.requestdatetime                                     AS date,
        COALESCE(gm.name, 'Unknown')                            AS groupname,
        lrm.requestsno                                          AS patientid,
        lrm.name                                                AS patientname,
        lrm.mobileno                                            AS mobile,
        COALESCE(dm.name, '')                                   AS referral,
        tm.name                                                 AS testname,
        COALESCE(lrd.testamount, 0)                             AS price,
        lrm.requestguid                                         AS requestguid,
        COALESCE(cm.custcode, '')                               AS custcode,
        COALESCE(lrm.discountamount, 0)
        + COALESCE(lrm.ourdiscount, 0)
        + COALESCE(lrm.specialdiscount, 0)                      AS discountamount,
        COALESCE(lrm.totalamount, 0)                            AS netamount,
        COALESCE(lrm.paidamount, 0)                             AS paidamount,
        (COALESCE(lrm.totalamount, 0) 
         - COALESCE(lrm.paidamount, 0) 
         - 0 
         - 0)                      AS balanceamount
    FROM lab_request_master lrm
    INNER JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
    INNER JOIN test_master tm ON tm.tcode = lrd.tcode
    LEFT JOIN group_master gm ON gm.gcode = tm.gcode
    LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode
    LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted, false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
    ORDER BY groupname, date, patientname";

                var rows = (await db.QueryAsync<GroupStatementModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new GroupStatementRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/statement/GetGroupStatement",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> SummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(_conn))
                {
                    // ─── Step 1: Select the grouping expression based on period type ───
                    string groupExpression = "DATE(lrm.requestdatetime)";
                    if (string.Equals(periodtype, "month-wise", StringComparison.OrdinalIgnoreCase))
                    {
                        groupExpression = "DATE_TRUNC('month', lrm.requestdatetime)";
                    }
                    else if (string.Equals(periodtype, "year-wise", StringComparison.OrdinalIgnoreCase))
                    {
                        groupExpression = "DATE_TRUNC('year', lrm.requestdatetime)";
                    }

                    // ─── Step 2: Summary rows grouped dynamically ─────────────────────
                    string sql = $@"
                SELECT
                    {groupExpression}::timestamp            AS date,

                    --BILLED: count of requests per period
                    COUNT(*)                                        AS billed,

                    --BILLEDAMOUNT: gross before discount = requestamount
                    COALESCE(SUM(lrm.requestamount), 0)             AS billedamount,

                    --DISCOUNT: all pre-billing reductions
                    COALESCE(SUM(
                        COALESCE(lrm.discountamount, 0)
                        + COALESCE(lrm.ourdiscount, 0)
                        + COALESCE(lrm.specialdiscount, 0)
                    ), 0)                                           AS discount,

                    --NETAMOUNT: totalamount already stores net after discount
                    COALESCE(SUM(lrm.totalamount), 0)               AS netamount,

                    --PAID: total cash received
                    COALESCE(SUM(lrm.paidamount), 0)                AS paid,

                    --BALANCE: net - paid - refund - dueconcession
                    COALESCE(SUM(
                        lrm.totalamount
                        - COALESCE(lrm.paidamount, 0)
                        - 0
                        - 0
                    ), 0)                                           AS balance

                FROM lab_request_master lrm
                WHERE lrm.tenant_code = @tenant_code
                  AND COALESCE(lrm.deleted, false) = false
                  AND lrm.requestdatetime >= @fromdate
                  AND lrm.requestdatetime < @todate + INTERVAL '1 day'
                GROUP BY {groupExpression}
                ORDER BY {groupExpression};
                                ";

                    var summaryRows = (await db.QueryAsync<SummaryModel>(
                        sql,
                        new { fromdate, todate, tenant_code }
                    )).ToList();

                    // ─── Step 3: Company info ─────────────────────────────────────────
                    string sql1 = @"
        SELECT legal_name, address_line1, contact_number, contact_email
        FROM mastertenant.tenants
        WHERE tenant_code = @tenant_code
    ";

                    var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                        sql1,
                        new { tenant_code }
                    );

                    // ─── Step 4: Build payload ────────────────────────────────────────
                    var payload = new SummaryRequest
                    {
                        summary = summaryRows,
                        fromdate = fromdate,
                        todate = todate,
                        CompanyName = companyInfo?.legal_name,
                        CompanyAddress = companyInfo?.address_line1,
                        CompanyContactNo = companyInfo?.contact_number,
                        CompanyEmail = companyInfo?.contact_email,
                        periodtype = periodtype
                    };

                    // ─── Step 5: POST to report server ────────────────────────────────
                    var client = _httpClientFactory.CreateClient("ReportServer");
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("/api/summary/getsummary", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Report server error {response.StatusCode}: {error}");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public async Task<string?> DiscountSummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(_conn))
                {
                    // ─── Step 1: Select the grouping expression based on period type ───
                    string groupExpression = "DATE(lrm.requestdatetime)";
                    if (string.Equals(periodtype, "month-wise", StringComparison.OrdinalIgnoreCase))
                    {
                        groupExpression = "DATE_TRUNC('month', lrm.requestdatetime)";
                    }
                    else if (string.Equals(periodtype, "year-wise", StringComparison.OrdinalIgnoreCase))
                    {
                        groupExpression = "DATE_TRUNC('year', lrm.requestdatetime)";
                    }

                    // ─── Step 2: Summary rows grouped dynamically ─────────────────────
                    string sql = $@"
                SELECT
                    {groupExpression}::timestamp            AS date,

                    --BILLED: count of requests per period
                    COUNT(DISTINCT lrm.requestguid)                 AS billed,

                    --BILLEDAMOUNT: gross before discount = requestamount
                    COALESCE(SUM(lrm.requestamount), 0)             AS billedamount,

                    --DISCOUNT: all pre-billing reductions
                    COALESCE(SUM(
                        COALESCE(lrm.discountamount, 0)
                        + COALESCE(lrm.ourdiscount, 0)
                        + COALESCE(lrm.specialdiscount, 0)
                    ), 0)                                           AS discount,

                    --NETAMOUNT: totalamount already stores net after discount
                    COALESCE(SUM(lrm.totalamount), 0)               AS netamount,

                    --PAID: total cash received
                    COALESCE(SUM(lrm.paidamount), 0)                AS paid,

                    --BALANCE: net - paid - refund - dueconcession
                    COALESCE(SUM(
                        lrm.totalamount
                        - COALESCE(lrm.paidamount, 0)
                        - 0
                        - 0
                    ), 0)                                           AS balance

                FROM lab_request_master lrm
                WHERE lrm.tenant_code = @tenant_code
                  AND COALESCE(lrm.deleted, false) = false
                  AND lrm.requestdatetime >= @fromdate
                  AND lrm.requestdatetime < @todate + INTERVAL '1 day'
                  AND (
                      COALESCE(lrm.discountamount, 0)
                      + COALESCE(lrm.ourdiscount, 0)
                      + COALESCE(lrm.specialdiscount, 0)
                  ) > 0
                GROUP BY {groupExpression}
                ORDER BY {groupExpression};
                                ";

                    var summaryRows = (await db.QueryAsync<SummaryModel>(
                        sql,
                        new { fromdate, todate, tenant_code }
                    )).ToList();

                    // ─── Step 3: Company info ─────────────────────────────────────────
                    string sql1 = @"
        SELECT legal_name, address_line1, contact_number, contact_email
        FROM mastertenant.tenants
        WHERE tenant_code = @tenant_code
    ";

                    var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                        sql1,
                        new { tenant_code }
                    );

                    // ─── Step 4: Build payload ────────────────────────────────────────
                    var payload = new SummaryRequest
                    {
                        summary = summaryRows,
                        fromdate = fromdate,
                        todate = todate,
                        CompanyName = companyInfo?.legal_name,
                        CompanyAddress = companyInfo?.address_line1,
                        CompanyContactNo = companyInfo?.contact_number,
                        CompanyEmail = companyInfo?.contact_email,
                        periodtype = periodtype,
                        summarytype = "Discount Summary"
                    };

                    // ─── Step 5: POST to report server ────────────────────────────────
                    var client = _httpClientFactory.CreateClient("ReportServer");
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("/api/summary/getsummary", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Report server error {response.StatusCode}: {error}");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public async Task<string?> DueSummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(_conn))
                {
                    // ─── Step 1: Select the grouping expression based on period type ───
                    string groupExpression = "DATE(lrm.requestdatetime)";
                    if (string.Equals(periodtype, "month-wise", StringComparison.OrdinalIgnoreCase))
                    {
                        groupExpression = "DATE_TRUNC('month', lrm.requestdatetime)";
                    }
                    else if (string.Equals(periodtype, "year-wise", StringComparison.OrdinalIgnoreCase))
                    {
                        groupExpression = "DATE_TRUNC('year', lrm.requestdatetime)";
                    }

                    // ─── Step 2: Summary rows grouped dynamically ─────────────────────
                    string sql = $@"
                SELECT
                    {groupExpression}::timestamp            AS date,

                    --BILLED: count of requests per period
                    COUNT(DISTINCT lrm.requestguid)                 AS billed,

                    --BILLEDAMOUNT: gross before discount = requestamount
                    COALESCE(SUM(lrm.requestamount), 0)             AS billedamount,

                    --DISCOUNT: all pre-billing reductions
                    COALESCE(SUM(
                        COALESCE(lrm.discountamount, 0)
                        + COALESCE(lrm.ourdiscount, 0)
                        + COALESCE(lrm.specialdiscount, 0)
                    ), 0)                                           AS discount,

                    --NETAMOUNT: totalamount already stores net after discount
                    COALESCE(SUM(lrm.totalamount), 0)               AS netamount,

                    --PAID: total cash received
                    COALESCE(SUM(lrm.paidamount), 0)                AS paid,

                    --BALANCE: net - paid - refund - dueconcession
                    COALESCE(SUM(
                        lrm.totalamount
                        - COALESCE(lrm.paidamount, 0)
                        - 0
                        - 0
                    ), 0)                                           AS balance

                FROM lab_request_master lrm
                WHERE lrm.tenant_code = @tenant_code
                  AND COALESCE(lrm.deleted, false) = false
                  AND lrm.requestdatetime >= @fromdate
                  AND lrm.requestdatetime < @todate + INTERVAL '1 day'
                  AND (
                      lrm.totalamount
                      - COALESCE(lrm.paidamount, 0)
                      - 0
                      - 0
                  ) > 0
                GROUP BY {groupExpression}
                ORDER BY {groupExpression};
                                ";

                    var summaryRows = (await db.QueryAsync<SummaryModel>(
                        sql,
                        new { fromdate, todate, tenant_code }
                    )).ToList();

                    // ─── Step 3: Company info ─────────────────────────────────────────
                    string sql1 = @"
        SELECT legal_name, address_line1, contact_number, contact_email
        FROM mastertenant.tenants
        WHERE tenant_code = @tenant_code
    ";

                    var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                        sql1,
                        new { tenant_code }
                    );

                    // ─── Step 4: Build payload ────────────────────────────────────────
                    var payload = new SummaryRequest
                    {
                        summary = summaryRows,
                        fromdate = fromdate,
                        todate = todate,
                        CompanyName = companyInfo?.legal_name,
                        CompanyAddress = companyInfo?.address_line1,
                        CompanyContactNo = companyInfo?.contact_number,
                        CompanyEmail = companyInfo?.contact_email,
                        periodtype = periodtype,
                        summarytype = "Due Summary"
                    };

                    // ─── Step 5: POST to report server ────────────────────────────────
                    var client = _httpClientFactory.CreateClient("ReportServer");
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("/api/summary/getsummary", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Report server error {response.StatusCode}: {error}");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public async Task<string?> GroupSummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        COALESCE(gm.name, 'Unknown')                            AS groupname,
        COUNT(*)                                                AS billed,
        COALESCE(SUM(lrd.testamount), 0)                        AS billedamount,
        COALESCE(SUM(COALESCE(lrd.discount, 0)), 0)             AS discountamount,
        COALESCE(SUM(COALESCE(lrd.testamount - lrd.discount, 0)), 0)          AS netamount,
        COALESCE(SUM(
            CASE WHEN COALESCE(lrm.totalamount, 0) > 0 
                 THEN COALESCE(lrm.paidamount, 0) * (COALESCE(lrd.testamount - lrd.discount, 0) / lrm.totalamount)
                 ELSE 0 END
        ), 0)                                                   AS paidamount,
        COALESCE(SUM(
            CASE WHEN COALESCE(lrm.totalamount, 0) > 0 
                 THEN COALESCE(
                        lrm.totalamount 
                        - COALESCE(lrm.paidamount, 0) 
                        - 0 
                        - 0
                      ) * (COALESCE(lrd.testamount-lrd.discount, 0) / lrm.totalamount)
                 ELSE 0 END
        ), 0)                                                   AS balanceamount
    FROM lab_request_master lrm
    INNER JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
    INNER JOIN test_master tm ON tm.tcode = lrd.tcode
    INNER JOIN group_master gm ON gm.gcode = tm.gcode
    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted, false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
    GROUP BY COALESCE(gm.name, 'Unknown')
    ORDER BY groupname";

                var rows = (await db.QueryAsync<GroupSummaryModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new GroupSummaryRequest
                {
                    summary = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email,
                    periodtype = periodtype
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/summary/GetGroupSummary",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> TestStatementPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        lrm.requestdatetime                                     AS date,
        COALESCE(tm.name, 'Unknown')                            AS testname,
        lrm.requestsno                                          AS patientid,
        lrm.name                                                AS patientname,
        lrm.mobileno                                            AS mobile,
        COALESCE(dm.name, '')                                   AS referral,
        COALESCE(lrd.testamount, 0)                             AS price,
        lrm.requestguid                                         AS requestguid,
        COALESCE(cm.custcode, '')                               AS custcode,
        COALESCE(lrm.discountamount, 0)
        + COALESCE(lrm.ourdiscount, 0)
        + COALESCE(lrm.specialdiscount, 0)                      AS discountamount,
        COALESCE(lrm.totalamount, 0)                            AS netamount,
        COALESCE(lrm.paidamount, 0)                             AS paidamount,
        (COALESCE(lrm.totalamount, 0) 
         - COALESCE(lrm.paidamount, 0) 
         - 0 
         - 0)                      AS balanceamount
    FROM lab_request_master lrm
    INNER JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
    INNER JOIN test_master tm ON tm.tcode = lrd.tcode
    LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode
    LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted, false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
    ORDER BY testname, date, patientname";

                var rows = (await db.QueryAsync<TestStatementModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new TestStatementRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/statement/GetTestStatement",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> TestSummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        COALESCE(tm.name, 'Unknown')                            AS testname,
        COUNT(*)                                                AS billed,
        COALESCE(SUM(lrd.testamount), 0)                        AS billedamount,
        COALESCE(SUM(COALESCE(lrd.discount, 0)), 0)             AS discountamount,
        COALESCE(SUM(COALESCE(lrd.testamount-lrd.discount, 0)), 0)          AS netamount,
        COALESCE(SUM(
            CASE WHEN COALESCE(lrm.totalamount, 0) > 0 
                 THEN COALESCE(lrm.paidamount, 0) * (COALESCE(lrd.testamount-lrd.discount, 0) / lrm.totalamount)
                 ELSE 0 END
        ), 0)                                                   AS paidamount,
        COALESCE(SUM(
            CASE WHEN COALESCE(lrm.totalamount, 0) > 0 
                 THEN COALESCE(
                        lrm.totalamount 
                        - COALESCE(lrm.paidamount, 0) 
                        - 0 
                        - 0
                      ) * (COALESCE(lrd.testamount-lrd.discount, 0) / lrm.totalamount)
                 ELSE 0 END
        ), 0)                                                   AS balanceamount
    FROM lab_request_master lrm
    INNER JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
    INNER JOIN test_master tm ON tm.tcode = lrd.tcode
    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted, false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
    GROUP BY COALESCE(tm.name, 'Unknown')
    ORDER BY testname";

                var rows = (await db.QueryAsync<TestSummaryModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new TestSummaryRequest
                {
                    summary = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email,
                    periodtype = periodtype
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/summary/GetTestSummary",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> ReferralSummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
    SELECT
        COALESCE(dm.name, '')                       AS referral,
        COUNT(*)                                    AS billed,
        COALESCE(SUM(lrm.requestamount), 0)         AS billedamount,
        COALESCE(SUM(
            COALESCE(lrm.discountamount, 0)
            + COALESCE(lrm.ourdiscount, 0)
            + COALESCE(lrm.specialdiscount, 0)
        ), 0)                                       AS discountamount,
        COALESCE(SUM(lrm.totalamount), 0)           AS netamount,
        COALESCE(SUM(lrm.paidamount), 0)            AS paidamount,
        COALESCE(SUM(
            lrm.totalamount
            - COALESCE(lrm.paidamount, 0)
            - 0
            - 0
        ), 0)                                       AS balanceamount
    FROM lab_request_master lrm
    LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode
    WHERE lrm.tenant_code = @tenant_code
      AND COALESCE(lrm.deleted, false) = false
      AND lrm.requestdatetime >= @fromdate
      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
    GROUP BY COALESCE(dm.name, '')
    ORDER BY referral";

                var rows = (await db.QueryAsync<ReferralSummaryModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name,address_line1,contact_number,contact_email
      FROM mastertenant.tenants
      WHERE tenant_code=@tenant_code",
                    new { tenant_code });

                var payload = new ReferralSummaryRequest
                {
                    summary = rows,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email,
                    periodtype = periodtype
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/summary/GetReferralSummary",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<string?> ReferralReceiptPDF(Guid receiptguid, string tenant_code, bool? isletterhead = false)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT
                        rm.receiptsnoprint                                                          AS ReceiptNo,
                        rm.receiptdate                                                              AS ReceiptDate,

                        COALESCE(req.dcode, 0)                                                      AS dcode,
                        COALESCE(NULLIF(req.doctorfullname,''), NULLIF(req.docname,''), 'SELF')     AS ReferralName,
                        req.mobileno                                                                AS MobileNo,
                        req.address                                                                 AS Address,

                        bm.name                                                                     AS BranchName,

                        t.legal_name                                                                AS CompanyName,
                        CONCAT_WS(', ',
                            NULLIF(t.address_line1, ''),
                            NULLIF(t.address_line2, ''),
                            NULLIF(t.city,          ''),
                            NULLIF(t.state,         ''),
                            NULLIF(t.pincode,       '')
                        )                                                                           AS CompanyAddress,
                        t.contact_number                                                            AS CompanyContactNo,
                        t.contact_email                                                             AS CompanyEmail,

                        COUNT(rd.receiptdetailsid)                                                AS Totalbills,

                        CAST(CASE WHEN amt.HasRows THEN COALESCE(amt.TotalBillAmount, 0) ELSE COALESCE(rm.amounttotal, 0) END AS DECIMAL(18,2)) AS TotalAmount,
                        CAST(COALESCE(rm.amountpaid, 0) AS DECIMAL(18,2)) AS PaidAmount,
                        CAST(CASE WHEN amt.HasRows THEN COALESCE(amt.BalanceAmount, 0) ELSE COALESCE(rm.amounttotal - rm.amountpaid, 0) END AS DECIMAL(18,2)) AS BalanceAmount,

                        pm.name                                                                     AS PaymentMode,

                        COALESCE(amt.PendingBillsBeforeCount, 0)                                    AS TotalBillsCount,
                        COALESCE(amt.PaidBillsInReceiptCount, 0)                                    AS PaidBillsCount,
                        COALESCE(amt.PendingBillsBeforeCount - amt.PaidBillsInReceiptCount, 0)      AS PendingBillsCount

                    FROM receipt_master rm

                    LEFT JOIN receipt_details rd
                           ON rd.receiptguid = rm.receiptguid
                          AND rd.tenant_code = rm.tenant_code
                          AND (rd.deleted = false OR rd.deleted IS NULL)

                    LEFT JOIN paymode_master pm
                           ON pm.pmcode = CAST(rm.pmcode AS INT)

                    LEFT JOIN mastertenant.branch_master bm
                           ON bm.bh_code = rm.enteredbhcode

                    LEFT JOIN mastertenant.tenants t
                           ON t.tenant_code = rm.tenant_code

                    LEFT JOIN LATERAL (

                        -- PATH 1: receipt_details.requestguid → lab_request_master → doctor_master
                        SELECT
                            dm.dcode,
                            dm.mobile       AS mobileno,
                            dm.address,
                            dm.doctorfullname,
                            dm.name         AS docname,
                            1               AS _priority,
                            (dm.dcode IS NOT NULL AND dm.dcode <> 0)::int AS _hasdoc
                        FROM   receipt_details    rd2
                        JOIN   lab_request_master lrm
                               ON  lrm.requestguid = rd2.requestguid
                               AND lrm.tenant_code  = rd2.tenant_code
                        -- ✅ Removed dm.tenant_code filter — avoids silent mismatch
                        LEFT JOIN doctor_master   dm ON dm.dcode = CAST(lrm.dcode AS INT)
                        WHERE  rd2.receiptguid = rm.receiptguid
                          AND  rd2.tenant_code = rm.tenant_code
                          AND  (rd2.deleted = false OR rd2.deleted IS NULL)
                          AND  rd2.requestguid IS NOT NULL

                        UNION ALL

                        -- PATH 2 (fallback): receipt_master.cnttid → lab_request_master → doctor_master
                        SELECT
                            dm.dcode,
                            dm.mobile       AS mobileno,
                            dm.address,
                            dm.doctorfullname,
                            dm.name         AS docname,
                            2               AS _priority,
                            (dm.dcode IS NOT NULL AND dm.dcode <> 0)::int AS _hasdoc
                        FROM   lab_request_master lrm
                        LEFT JOIN doctor_master   dm ON dm.dcode = CAST(lrm.dcode AS INT)
                        WHERE  lrm.cnttid      = rm.cnttid
                          AND  lrm.tenant_code = rm.tenant_code
                          AND  rm.cnttid IS NOT NULL
                          AND  (lrm.deleted = false OR lrm.deleted IS NULL)

                        UNION ALL

                        -- PATH 3 (last resort): receipt_master.cntcode → most recent lab_request_master
                        SELECT
                            dm.dcode,
                            dm.mobile       AS mobileno,
                            dm.address,
                            dm.doctorfullname,
                            dm.name         AS docname,
                            3               AS _priority,
                            (dm.dcode IS NOT NULL AND dm.dcode <> 0)::int AS _hasdoc
                        FROM   lab_request_master lrm
                        LEFT JOIN doctor_master   dm ON dm.dcode = CAST(lrm.dcode AS INT)
                        WHERE  CAST(lrm.cntcode AS INT) = rm.cntcode
                          AND  lrm.tenant_code          = rm.tenant_code
                          AND  rm.cntcode IS NOT NULL
                          AND  (lrm.deleted = false OR lrm.deleted IS NULL)

                        -- ✅ Prefer rows that actually have a doctor, then by path priority
                        ORDER BY _hasdoc DESC, _priority ASC
                        LIMIT 1

                    ) req ON true

                    LEFT JOIN LATERAL (
                        SELECT
                            COUNT(lrm.requestguid) > 0 AS HasRows,
                            
                            -- Total Amount: sum of outstanding before this receipt
                            COALESCE(SUM(
                                COALESCE(lrm.totalamount, 0) 
                                - COALESCE(trans.paid_before, 0)
                                - COALESCE(trans.concession_before, 0)
                                + COALESCE(trans.refund_before, 0)
                            ), 0) AS TotalBillAmount,
                            
                            -- Balance Amount: sum of outstanding after this receipt
                            COALESCE(SUM(
                                COALESCE(lrm.totalamount, 0)
                                - (COALESCE(trans.paid_before, 0) + COALESCE(trans.paid_current, 0))
                                - (COALESCE(trans.concession_before, 0) + COALESCE(trans.concession_current, 0))
                                + (COALESCE(trans.refund_before, 0) + COALESCE(trans.refund_current, 0))
                            ), 0) AS BalanceAmount,

                            -- Since all selected rows were pending before:
                            COUNT(lrm.requestguid)::int AS PendingBillsBeforeCount,

                            COALESCE(SUM(CASE WHEN (
                                -- Settled after:
                                (COALESCE(lrm.totalamount, 0)
                                 - (COALESCE(trans.paid_before, 0) + COALESCE(trans.paid_current, 0))
                                 - (COALESCE(trans.concession_before, 0) + COALESCE(trans.concession_current, 0))
                                 + (COALESCE(trans.refund_before, 0) + COALESCE(trans.refund_current, 0))) <= 0
                            ) THEN 1 ELSE 0 END), 0)::int AS PaidBillsInReceiptCount

                        FROM lab_request_master lrm
                        LEFT JOIN LATERAL (
                            SELECT
                                COALESCE(SUM(CASE WHEN category = 'before' THEN receipt_amount ELSE 0 END), 0) AS paid_before,
                                COALESCE(SUM(CASE WHEN category = 'before' THEN discount_amount ELSE 0 END), 0) AS concession_before,
                                COALESCE(SUM(CASE WHEN category = 'before' THEN refund_amount ELSE 0 END), 0) AS refund_before,
                                
                                COALESCE(SUM(CASE WHEN category = 'current' THEN receipt_amount ELSE 0 END), 0) AS paid_current,
                                COALESCE(SUM(CASE WHEN category = 'current' THEN discount_amount ELSE 0 END), 0) AS concession_current,
                                COALESCE(SUM(CASE WHEN category = 'current' THEN refund_amount ELSE 0 END), 0) AS refund_current
                            FROM (
                                SELECT 
                                    rd_sub.receiptamount AS receipt_amount,
                                    rd_sub.discount_amount,
                                    rd_sub.refund_amount,
                                    CASE 
                                        WHEN rm_sub.receiptguid = rm.receiptguid THEN 'current'
                                        WHEN rm_sub.receiptdate < rm.receiptdate OR (rm_sub.receiptdate = rm.receiptdate AND rm_sub.receiptsno < rm.receiptsno) THEN 'before'
                                        ELSE 'future'
                                    END AS category
                                FROM receipt_details rd_sub
                                JOIN receipt_master rm_sub ON rm_sub.receiptguid = rd_sub.receiptguid
                                WHERE rd_sub.requestguid = lrm.requestguid
                                  AND (rd_sub.deleted = false OR rd_sub.deleted IS NULL)
                                  AND (rm_sub.deleted = false OR rm_sub.deleted IS NULL)
                            ) t
                        ) trans ON true
                        WHERE lrm.tenant_code = rm.tenant_code
                          AND (lrm.deleted = false OR lrm.deleted IS NULL)
                          AND lrm.requestdatetime <= rm.receiptdate
                          AND (
                              (COALESCE(req.dcode, 0) <> 0 AND CAST(lrm.dcode AS INT) = req.dcode)
                              OR
                              (COALESCE(req.dcode, 0) = 0 AND lrm.requestguid IN (
                                  SELECT requestguid FROM receipt_details WHERE receiptguid = rm.receiptguid AND (deleted = false OR deleted IS NULL)
                                  UNION
                                  SELECT request_guid AS requestguid FROM balancecollectionby WHERE receipt_guid = rm.receiptguid AND (deleted = false OR deleted IS NULL)
                              ))
                          )
                          -- Filter: Was pending before this receipt:
                          AND (
                              (COALESCE(lrm.totalamount, 0) 
                               - COALESCE(trans.paid_before, 0)
                               - COALESCE(trans.concession_before, 0)
                               + COALESCE(trans.refund_before, 0)) > 0
                          )
                    ) amt ON true

                    WHERE rm.receiptguid = @receiptguid
                      AND rm.tenant_code = @tenant_code
                      AND (rm.deleted = false OR rm.deleted IS NULL)

                    GROUP BY
                        rm.receiptsnoprint,
                        rm.receiptdate,
                        req.dcode,
                        req.doctorfullname,
                        req.docname,
                        req.mobileno,
                        req.address,
                        bm.name,
                        t.legal_name, t.address_line1, t.address_line2,
                        t.city, t.state, t.pincode,
                        t.contact_number, t.contact_email,
                        rm.amounttotal,
                        rm.amountpaid,
                        pm.name,
                        amt.HasRows,
                        amt.TotalBillAmount,
                        amt.BalanceAmount,
                        amt.PendingBillsBeforeCount,
                        amt.PaidBillsInReceiptCount";

                var receiptData = await db.QueryFirstOrDefaultAsync<ReceiptPdfModel>(
                    sql, new { receiptguid = receiptguid.ToString(), tenant_code });

                if (receiptData == null)
                    return null;

                var payload = new ReceiptRequest
                {
                    ReceiptData = receiptData,
                    LogoImage = null,
                    IsLetterhead = isletterhead ?? false,
                    TenantId = tenant_code
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/receipt/getreferralreceipt", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<string?> PatientReceiptPDF(Guid receiptguid, string tenant_code, bool? isletterhead = false)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT
                        rm.receiptsnoprint                                                          AS ReceiptNo,
                        rm.receiptdate                                                              AS ReceiptDate,

                        COALESCE(pat.custcode, '')                                                  AS PatientId,
                        COALESCE(pat.name, '')                                                      AS PatientName,
                        pat.age                                                                     AS Age,
                        COALESCE(pat.gender, '')                                                    AS Gender,
                        pat.mobileno                                                                AS MobileNo,
                        pat.address                                                                 AS Address,

                        COALESCE(NULLIF(pat.doctorfullname,''), NULLIF(pat.docname,''), 'SELF')     AS ReferralName,

                        bm.name                                                                     AS BranchName,

                        t.legal_name                                                                AS CompanyName,
                        CONCAT_WS(', ',
                            NULLIF(t.address_line1, ''),
                            NULLIF(t.address_line2, ''),
                            NULLIF(t.city,          ''),
                            NULLIF(t.state,         ''),
                            NULLIF(t.pincode,       '')
                        )                                                                           AS CompanyAddress,
                        t.contact_number                                                            AS CompanyContactNo,
                        t.contact_email                                                             AS CompanyEmail,

                        CAST(CASE WHEN amt.HasRows THEN COALESCE(amt.TotalBillAmount, 0) ELSE COALESCE(rm.amounttotal, 0) END AS DECIMAL(18,2)) AS TotalAmount,
                        CAST(COALESCE(rm.amountpaid,  0) AS DECIMAL(18,2))                         AS PaidAmount,
                        CAST(CASE WHEN amt.HasRows THEN COALESCE(amt.BalanceAmount, 0) ELSE COALESCE(rm.amounttotal - rm.amountpaid, 0) END AS DECIMAL(18,2)) AS BalanceAmount,

                        pm.name                                                                     AS PaymentMode,

                        CAST(0 AS DECIMAL(18,2))                                                    AS PreviousPaid

                    FROM receipt_master rm

                    LEFT JOIN receipt_details rd
                           ON rd.receiptguid = rm.receiptguid
                          AND rd.tenant_code = rm.tenant_code
                          AND (rd.deleted = false OR rd.deleted IS NULL)

                    LEFT JOIN paymode_master pm
                           ON pm.pmcode = CAST(rm.pmcode AS INT)

                    LEFT JOIN mastertenant.branch_master bm
                           ON bm.bh_code = rm.enteredbhcode

                    LEFT JOIN mastertenant.tenants t
                           ON t.tenant_code = rm.tenant_code

                    LEFT JOIN LATERAL (
                        SELECT
                            COUNT(rguid.requestguid) > 0 AS HasRows,
                            -- Total outstanding before this receipt's payments/concessions/refunds
                            COALESCE(SUM(
                                COALESCE(lrm.totalamount, 0)
                                - COALESCE(trans.paid_before, 0)
                                - COALESCE(trans.concession_before, 0)
                                + COALESCE(trans.refund_before, 0)
                            ), 0) AS TotalBillAmount,
                            
                            -- Total outstanding after this receipt's payments/concessions/refunds
                            COALESCE(SUM(
                                COALESCE(lrm.totalamount, 0)
                                - (COALESCE(trans.paid_before, 0) + COALESCE(trans.paid_current, 0))
                                - (COALESCE(trans.concession_before, 0) + COALESCE(trans.concession_current, 0))
                                + (COALESCE(trans.refund_before, 0) + COALESCE(trans.refund_current, 0))
                            ), 0) AS BalanceAmount
                        FROM (
                            SELECT requestguid FROM receipt_details WHERE receiptguid = rm.receiptguid AND (deleted = false OR deleted IS NULL)
                            UNION
                            SELECT request_guid AS requestguid FROM balancecollectionby WHERE receipt_guid = rm.receiptguid AND (deleted = false OR deleted IS NULL)
                        ) rguid
                        JOIN lab_request_master lrm ON lrm.requestguid = rguid.requestguid AND lrm.tenant_code = rm.tenant_code
                        LEFT JOIN LATERAL (
                            SELECT
                                COALESCE(SUM(CASE WHEN category = 'before' THEN receipt_amount ELSE 0 END), 0) AS paid_before,
                                COALESCE(SUM(CASE WHEN category = 'before' THEN discount_amount ELSE 0 END), 0) AS concession_before,
                                COALESCE(SUM(CASE WHEN category = 'before' THEN refund_amount ELSE 0 END), 0) AS refund_before,
                                
                                COALESCE(SUM(CASE WHEN category = 'current' THEN receipt_amount ELSE 0 END), 0) AS paid_current,
                                COALESCE(SUM(CASE WHEN category = 'current' THEN discount_amount ELSE 0 END), 0) AS concession_current,
                                COALESCE(SUM(CASE WHEN category = 'current' THEN refund_amount ELSE 0 END), 0) AS refund_current
                            FROM (
                                SELECT 
                                    rd_sub.receiptamount AS receipt_amount,
                                    rd_sub.discount_amount,
                                    rd_sub.refund_amount,
                                    CASE 
                                        WHEN rm_sub.receiptguid = rm.receiptguid THEN 'current'
                                        WHEN rm_sub.receiptdate < rm.receiptdate OR (rm_sub.receiptdate = rm.receiptdate AND rm_sub.receiptsno < rm.receiptsno) THEN 'before'
                                        ELSE 'future'
                                    END AS category
                                FROM receipt_details rd_sub
                                JOIN receipt_master rm_sub ON rm_sub.receiptguid = rd_sub.receiptguid
                                WHERE rd_sub.requestguid = lrm.requestguid
                                  AND (rd_sub.deleted = false OR rd_sub.deleted IS NULL)
                                  AND (rm_sub.deleted = false OR rm_sub.deleted IS NULL)
                            ) t
                        ) trans ON true
                    ) amt ON true

                    LEFT JOIN LATERAL (
                        SELECT
                            cm.custcode,
                            lrm.name,
                            CONCAT(
                                CASE WHEN COALESCE(lrm.ageyears::int, 0) = 0 AND COALESCE(lrm.agemonths::int, 0) = 0 AND COALESCE(lrm.agedays::int, 0) = 0 THEN COALESCE(cm.ageyears::text, '0') ELSE COALESCE(lrm.ageyears, '0') END, ' Y ',
                                CASE WHEN COALESCE(lrm.ageyears::int, 0) = 0 AND COALESCE(lrm.agemonths::int, 0) = 0 AND COALESCE(lrm.agedays::int, 0) = 0 THEN COALESCE(cm.agemonths::text, '0') ELSE COALESCE(lrm.agemonths, '0') END, ' M'
                            ) AS age,
                            lrm.gender,
                            lrm.mobileno,
                            lrm.address,
                            dm.doctorfullname,
                            dm.name AS docname
                        FROM receipt_details rd2
                        JOIN lab_request_master lrm
                          ON lrm.requestguid = rd2.requestguid
                          AND lrm.tenant_code = rd2.tenant_code
                        LEFT JOIN customerdb.customer_master cm
                          ON cm.custid = lrm.custid
                        LEFT JOIN doctor_master dm
                          ON dm.dcode = CAST(lrm.dcode AS INT)
                        WHERE rd2.receiptguid = rm.receiptguid
                          AND rd2.tenant_code = rm.tenant_code
                          AND (rd2.deleted = false OR rd2.deleted IS NULL)
                          AND rd2.requestguid IS NOT NULL
                        LIMIT 1
                    ) pat ON true

                    WHERE rm.receiptguid = @receiptguid
                      AND rm.tenant_code = @tenant_code
                      AND (rm.deleted = false OR rm.deleted IS NULL)

                    GROUP BY
                        rm.receiptsnoprint,
                        rm.receiptdate,
                        pat.custcode,
                        pat.name,
                        pat.age,
                        pat.gender,
                        pat.mobileno,
                        pat.address,
                        pat.doctorfullname,
                        pat.docname,
                        bm.name,
                        t.legal_name, t.address_line1, t.address_line2,
                        t.city, t.state, t.pincode,
                        t.contact_number, t.contact_email,
                        rm.amounttotal,
                        rm.amountpaid,
                        pm.name,
                        amt.HasRows,
                        amt.TotalBillAmount,
                        amt.BalanceAmount";

                var receiptData = await db.QueryFirstOrDefaultAsync<PatientReceiptPdfModel>(
                    sql, new { receiptguid = receiptguid.ToString(), tenant_code });

                if (receiptData == null)
                    return null;

                var client = _httpClientFactory.CreateClient("ReportServer");
                var payload = new PatientReceiptRequest
                {
                    ReceiptData = receiptData,
                    LogoImage = null,
                    IsLetterhead = isletterhead ?? false,
                    TenantId = tenant_code
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/receipt/getpatientreceipt", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<string>   BillPDF(Guid requestguid, string tenant_code, bool? isletterhead = false)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                // ─── Step 1: Bill data ────────────────────────────────────────────
                string billSql = @"
            SELECT 
                -- ✅ PatientId: custcode from customer_master (mirrors statement JOIN)
                COALESCE(cm.custcode, '')                                    AS PatientId,

                -- ✅ BillNo: prefer formatted print version, fall back to int::text
                COALESCE(lrm.requestsno::text)     AS BillNo,
                lrm.requestsnoprint::text AS RequestSnoPrint,
                (lrm.requestdatetime AT TIME ZONE 'Asia/Kolkata') AS BillDate,
                COALESCE(lrm.name,    '')                                    AS PatientName,
                CONCAT(
                    COALESCE(lrm.ageyears,  '0'), ' Y ',
                    COALESCE(lrm.agemonths, '0'), ' M'
                )                                                            AS Age,
                COALESCE(lrm.mobileno, '')                                   AS CusMobileNo,
                COALESCE(cm.careof, '')                                     AS CareOf,
                COALESCE(lrm.gender,  '')                                    AS Gender,
                COALESCE(lrm.address, '')                                    AS PatientAddress,
                COALESCE(dm.name,     '')                                    AS DoctorName,

                COALESCE(lrm.onlinecode,     '')                             AS OnlineCode,
                COALESCE(lrm.onlinepassword, '')                             AS Password,

                COALESCE(um.name, '')                                        AS CreatedBy,
                (COALESCE(lrm.entereddate, lrm.requestdatetime) AT TIME ZONE 'Asia/Kolkata') AS CreatedTime,
                -- TotalAmount: gross before discount (requestamount)
                COALESCE(lrm.requestamount, 0)::numeric                      AS TotalAmount,

                -- DiscountAmount: all pre-billing reductions
                COALESCE(
                    COALESCE(lrm.discountamount,   0)
                    + COALESCE(lrm.ourdiscount,    0)
                    + COALESCE(lrm.specialdiscount, 0),
                0)::numeric                                                  AS DiscountAmount,
                COALESCE(
                    COALESCE(lrm.requestamount, 0)
                    - COALESCE(lrm.discountamount,   0)
                    - COALESCE(lrm.ourdiscount,    0)
                    - COALESCE(lrm.specialdiscount, 0),
                0)::numeric                                                  AS NetAmount,

                -- ReceivedAmount: cash paid + receipt paid
                COALESCE(
                    COALESCE(lrm.paidamount,       0),
                0)::numeric                                                  AS ReceivedAmount,

                -- BalanceAmount: net - paid - refund - dueconcession
                COALESCE(
                    COALESCE(lrm.requestamount,      0)
                    - COALESCE(lrm.discountamount,   0)
                    - COALESCE(lrm.ourdiscount,      0)
                    - COALESCE(lrm.specialdiscount,  0)
                    - COALESCE(lrm.paidamount,       0)
                    - 0
                    - 0,
                0)::numeric AS BalanceAmount,

                lrm.tenant_code                                              AS TenantId

            FROM lab_request_master lrm

            -- ✅ custcode for PatientId — mirrors the statement query JOIN
            LEFT JOIN customerdb.customer_master cm ON cm.custid     = lrm.custid
            LEFT JOIN doctor_master              dm ON dm.dcode       = lrm.dcode
            LEFT JOIN mastertenant.user_master                um ON um.user_code   = lrm.usercode

            WHERE lrm.requestguid = @requestguid
              AND lrm.tenant_code = @tenant_code;
        ";

                var bill = await db.QueryFirstOrDefaultAsync<CashBillModel>(
                    billSql,
                    new { requestguid = requestguid.ToString(), tenant_code }
                );

                if (bill == null)
                    throw new Exception($"Bill not found for requestguid={requestguid}");

                // ─── Step 2: Company info ─────────────────────────────────────────
                string sql1 = @"
            SELECT
                legal_name,
                COALESCE(address_line1,  '') AS address_line1,
                COALESCE(contact_number, '') AS contact_number,
                COALESCE(contact_email,  '') AS contact_email,
                COALESCE(host_url,        '') AS host_url
            FROM mastertenant.tenants
            WHERE tenant_code = @tenant_code;
        ";

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    sql1,
                    new { tenant_code }
                );

                // ─── Step 3: Build payload ────────────────────────────────────────
                bill.LabName = companyInfo?.legal_name ?? string.Empty;
                bill.Address = companyInfo?.address_line1 ?? string.Empty;
                bill.MobileNo = companyInfo?.contact_number ?? string.Empty;
                bill.ContactNo = companyInfo?.contact_number ?? string.Empty;
                bill.Email = companyInfo?.contact_email ?? string.Empty;
                bill.Website = companyInfo?.host_url ?? string.Empty;
                bill.HelplineNo = companyInfo?.contact_number ?? string.Empty;

                // Line items (part of payload build — same block as step 3)
                string itemSql = @"
                        SELECT
                ROW_NUMBER() OVER (ORDER BY lrd.testsno) AS SNo,
                COALESCE(tm.name, '') AS TestName,
                COALESCE(lrd.testamount, 0)::numeric AS Amount
            FROM lab_request_details lrd
            LEFT JOIN test_master tm
                ON tm.tcode = lrd.tcode
            WHERE lrd.requestguid = @requestguid
              AND lrd.tenant_code = @tenant_code
            ORDER BY lrd.testsno;
                    ";

                bill.Items = (
                    await db.QueryAsync<CashBillItemModel>(
                        itemSql,
                        new { requestguid = requestguid.ToString(), tenant_code }
                    )
                ).ToList();

                // ─── Step 4: POST to report server ────────────────────────────────
                bill.isletterhead = isletterhead ?? false;
                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(bill);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/billreceipt/getbill", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                // ✅ returns base64 string — same as statement and summary
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.BillPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string> WorklistPDF(
            Guid? requestguid,
            DateTime? fromdate,
            DateTime? todate,
            string? gcode,
            string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                // ─── Step 1: Company info ─────────────────────────────────────────
                string companySql = @"
            SELECT
                legal_name,
                COALESCE(address_line1,  '') AS address_line1,
                COALESCE(contact_number, '') AS contact_number,
                COALESCE(contact_email,  '') AS contact_email,
                COALESCE(host_url,        '') AS host_url
            FROM mastertenant.tenants
            WHERE tenant_code = @tenant_code;
        ";

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    companySql,
                    new { tenant_code }
                );

                var client = _httpClientFactory.CreateClient("ReportServer");

                if (requestguid.HasValue)
                {
                    // ─── Patient-wise (single patient worklist) ─────────────────────
                    string billSql = @"
                SELECT
                    COALESCE(cm.custcode, '')                                    AS PatientId,
                    COALESCE(lrm.requestsno::text)                               AS BillNo,
                    lrm.requestsnoprint::text                                    AS RequestSnoPrint,
                    lrm.requestdatetime::timestamp                               AS BillDate,

                    COALESCE(lrm.name,    '')                                    AS PatientName,
                    CONCAT(
                        COALESCE(lrm.ageyears,  '0'), ' Y ',
                        COALESCE(lrm.agemonths, '0'), ' M'
                    )                                                            AS Age,
                    COALESCE(lrm.mobileno, '')                                   AS CusMobileNo,
                    COALESCE(cm.careof, '')                                      AS CareOf,
                    COALESCE(lrm.gender,  '')                                    AS Gender,
                    COALESCE(lrm.address, '')                                    AS PatientAddress,
                    COALESCE(dm.name,     '')                                    AS DoctorName,

                    COALESCE(lrm.onlinecode,     '')                             AS OnlineCode,
                    COALESCE(lrm.onlinepassword, '')                             AS Password,

                    COALESCE(um.name, '')                                        AS CreatedBy,
                    COALESCE(lrm.entereddate, lrm.requestdatetime)::timestamp    AS CreatedTime,

                    COALESCE(lrm.requestamount, 0)::numeric                      AS TotalAmount,

                    COALESCE(
                        COALESCE(lrm.discountamount,   0)
                        + COALESCE(lrm.ourdiscount,    0)
                        + COALESCE(lrm.specialdiscount, 0),
                    0)::numeric                                                  AS DiscountAmount,
                    COALESCE(
                        COALESCE(lrm.requestamount, 0)
                        - COALESCE(lrm.discountamount,   0)
                        - COALESCE(lrm.ourdiscount,    0)
                        - COALESCE(lrm.specialdiscount, 0),
                    0)::numeric                                                  AS NetAmount,

                    COALESCE(
                        COALESCE(lrm.paidamount,       0),
                    0)::numeric                                                  AS ReceivedAmount,

                    COALESCE(
                        COALESCE(lrm.requestamount,      0)
                        - COALESCE(lrm.discountamount,   0)
                        - COALESCE(lrm.ourdiscount,    0)
                        - COALESCE(lrm.specialdiscount,  0)
                        - COALESCE(lrm.paidamount,       0)
                        - 0
                        - 0,
                    0)::numeric AS BalanceAmount,

                    lrm.tenant_code                                              AS TenantId

                FROM lab_request_master lrm

                LEFT JOIN customerdb.customer_master cm ON cm.custid     = lrm.custid
                LEFT JOIN doctor_master              dm ON dm.dcode       = lrm.dcode
                LEFT JOIN mastertenant.user_master                um ON um.user_code   = lrm.usercode

                WHERE lrm.requestguid = @requestguid
                  AND lrm.tenant_code = @tenant_code;
            ";

                    var bill = await db.QueryFirstOrDefaultAsync<CashBillModel>(
                        billSql,
                        new { requestguid = requestguid.Value.ToString(), tenant_code }
                    );

                    if (bill == null)
                        throw new Exception($"Worklist details not found for requestguid={requestguid}");

                    bill.LabName = companyInfo?.legal_name ?? string.Empty;
                    bill.Address = companyInfo?.address_line1 ?? string.Empty;
                    bill.MobileNo = companyInfo?.contact_number ?? string.Empty;
                    bill.ContactNo = companyInfo?.contact_number ?? string.Empty;
                    bill.Email = companyInfo?.contact_email ?? string.Empty;
                    bill.Website = companyInfo?.host_url ?? string.Empty;
                    bill.HelplineNo = companyInfo?.contact_number ?? string.Empty;

                    string itemSql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY lrd.testsno) AS SNo,
                    lrd.tcode AS TCode,
                    COALESCE(tm.name, '') AS TestName,
                    COALESCE(lrd.testamount, 0)::numeric AS Amount,
                    COALESCE(gm.name, '') AS GroupName
                FROM lab_request_details lrd
                LEFT JOIN test_master tm ON tm.tcode = lrd.tcode
                LEFT JOIN group_master gm ON gm.gcode = tm.gcode
                WHERE lrd.requestguid = @requestguid
                  AND lrd.tenant_code = @tenant_code
                ORDER BY lrd.testsno;
            ";

                    bill.Items = (
                        await db.QueryAsync<CashBillItemModel>(
                            itemSql,
                            new { requestguid = requestguid.Value.ToString(), tenant_code }
                        )
                    ).GroupBy(i => i.TCode)
                     .Select((g, index) => {
                         var firstItem = g.First();
                         firstItem.SNo = index + 1;
                         return firstItem;
                     })
                     .ToList();

                    // Query sub-parameters
                    string subParamsSql = @"
                SELECT DISTINCT
                    COALESCE(parent_trm.tcode, trm.fromtcode) AS ParentTCode,
                    COALESCE(trm.col2, tm.name) AS SubTestName,
                    trm.slno
                FROM test_result_master trm
                INNER JOIN test_master tm ON tm.tcode = trm.tcode
                LEFT JOIN test_result_master parent_trm ON parent_trm.testresultid = trm.fromtestresultid AND parent_trm.tenant_code = @tenant_code
                WHERE (
                    trm.fromtcode IN (
                        SELECT DISTINCT lrd2.tcode
                        FROM lab_request_details lrd2
                        WHERE lrd2.tenant_code = @tenant_code
                          AND lrd2.requestguid = @requestguid
                    )
                    OR
                    trm.fromtestresultid IN (
                        SELECT DISTINCT parent_trm2.testresultid
                        FROM lab_request_details lrd2
                        INNER JOIN test_result_master parent_trm2 ON parent_trm2.tcode = lrd2.tcode AND parent_trm2.tenant_code = @tenant_code
                        WHERE lrd2.tenant_code = @tenant_code
                          AND lrd2.requestguid = @requestguid
                    )
                )
                AND trm.tenant_code = @tenant_code
                ORDER BY ParentTCode, trm.slno;
            ";

                    var subParams = (await db.QueryAsync<dynamic>(
                        subParamsSql,
                        new { requestguid = requestguid.Value.ToString(), tenant_code }
                    )).ToList();

                    foreach (var item in bill.Items)
                    {
                        item.SubParameters = subParams
                            .Where(sp => (int)sp.parenttcode == item.TCode)
                            .Select(sp => (string)sp.subtestname)
                            .Distinct()
                            .ToList();
                    }

                    var json = JsonSerializer.Serialize(bill);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("/api/billreceipt/getworklist", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Report server error {response.StatusCode}: {error}");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
                else if (fromdate.HasValue && todate.HasValue)
                {
                    // ─── Department-wise / Multi-patient Worklist ──────────────────
                    string billsSql = @"
                SELECT DISTINCT
                    lrm.requestguid                                              AS requestguid,
                    COALESCE(cm.custcode, '')                                    AS PatientId,
                    COALESCE(lrm.requestsno::text)                               AS BillNo,
                    lrm.requestsnoprint::text                                    AS RequestSnoPrint,
                    lrm.requestdatetime::timestamp                               AS BillDate,

                    COALESCE(lrm.name,    '')                                    AS PatientName,
                    CONCAT(
                        COALESCE(lrm.ageyears,  '0'), ' Y ',
                        COALESCE(lrm.agemonths, '0'), ' M'
                    )                                                            AS Age,
                    COALESCE(lrm.mobileno, '')                                   AS CusMobileNo,
                    COALESCE(cm.careof, '')                                      AS CareOf,
                    COALESCE(lrm.gender,  '')                                    AS Gender,
                    COALESCE(lrm.address, '')                                    AS PatientAddress,
                    COALESCE(dm.name,     '')                                    AS DoctorName,

                    COALESCE(lrm.onlinecode,     '')                             AS OnlineCode,
                    COALESCE(lrm.onlinepassword, '')                             AS Password,

                    COALESCE(um.name, '')                                        AS CreatedBy,
                    COALESCE(lrm.entereddate, lrm.requestdatetime)::timestamp    AS CreatedTime,

                    COALESCE(lrm.requestamount, 0)::numeric                      AS TotalAmount,
                    COALESCE(
                        COALESCE(lrm.discountamount,   0)
                        + COALESCE(lrm.ourdiscount,    0)
                        + COALESCE(lrm.specialdiscount, 0),
                    0)::numeric                                                  AS DiscountAmount,
                    COALESCE(
                        COALESCE(lrm.requestamount, 0)
                        - COALESCE(lrm.discountamount,   0)
                        - COALESCE(lrm.ourdiscount,    0)
                        - COALESCE(lrm.specialdiscount, 0),
                    0)::numeric                                                  AS NetAmount,
                    COALESCE(
                        COALESCE(lrm.paidamount,       0),
                    0)::numeric                                                  AS ReceivedAmount,
                    COALESCE(
                        COALESCE(lrm.requestamount,      0)
                        - COALESCE(lrm.discountamount,   0)
                        - COALESCE(lrm.ourdiscount,    0)
                        - COALESCE(lrm.specialdiscount,  0)
                        - COALESCE(lrm.paidamount,       0)
                        - 0
                        - 0,
                    0)::numeric                                                  AS BalanceAmount,

                    lrm.tenant_code                                              AS TenantId

                FROM lab_request_master lrm
                INNER JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
                INNER JOIN test_master tm ON tm.tcode = lrd.tcode
                LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
                LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode 
                LEFT JOIN mastertenant.user_master um ON um.user_code = lrm.usercode

                WHERE lrm.tenant_code = @tenant_code
                  AND COALESCE(lrm.deleted, false) = false
                  AND lrm.requestdatetime >= @fromdate
                  AND lrm.requestdatetime < @todate + INTERVAL '1 day'
                  AND (@gcode IS NULL OR @gcode = '' OR @gcode = '0' OR tm.gcode::text = @gcode);
            ";

                    var billsWithGuids = (await db.QueryAsync<dynamic>(
                        billsSql,
                        new { fromdate = fromdate.Value, todate = todate.Value, gcode, tenant_code }
                    )).Select(b => new {
                        Bill = new CashBillModel
                        {
                            TenantId = b.tenantid,
                            BillNo = b.billno,
                            RequestSnoPrint = b.requestsnoprint,
                            BillDate = b.billdate,
                            PatientName = b.patientname,
                            PatientId = b.patientid,
                            Age = b.age,
                            Gender = b.gender,
                            PatientAddress = b.patientaddress,
                            CusMobileNo = b.cusmobileno,
                            CareOf = b.careof,
                            DoctorName = b.doctorname,
                            OnlineCode = b.onlinecode,
                            Password = b.password,
                            CreatedBy = b.createdby,
                            CreatedTime = b.createdtime,
                            TotalAmount = b.totalamount ?? 0,
                            DiscountAmount = b.discountamount ?? 0,
                            NetAmount = b.netamount ?? 0,
                            ReceivedAmount = b.receivedamount ?? 0,
                            BalanceAmount = b.balanceamount ?? 0,

                            LabName = companyInfo?.legal_name ?? string.Empty,
                            Address = companyInfo?.address_line1 ?? string.Empty,
                            MobileNo = companyInfo?.contact_number ?? string.Empty,
                            ContactNo = companyInfo?.contact_number ?? string.Empty,
                            Email = companyInfo?.contact_email ?? string.Empty,
                            Website = companyInfo?.host_url ?? string.Empty,
                            HelplineNo = companyInfo?.contact_number ?? string.Empty
                        },
                        RequestGUID = (string)b.requestguid
                    }).ToList();

                    // Details query
                    string itemsSql = @"
                SELECT
                    lrd.requestguid                                 AS RequestGUID,
                    lrd.tcode                                       AS TCode,
                    COALESCE(tm.name, '')                           AS TestName,
                    COALESCE(lrd.testamount, 0)::numeric            AS Amount,
                    COALESCE(gm.name, '')                           AS GroupName
                FROM lab_request_details lrd
                INNER JOIN test_master tm ON tm.tcode = lrd.tcode
                LEFT JOIN group_master gm ON gm.gcode = tm.gcode
                WHERE lrd.tenant_code = @tenant_code
                  AND lrd.requestguid IN (
                      SELECT lrm2.requestguid
                      FROM lab_request_master lrm2
                      WHERE lrm2.tenant_code = @tenant_code
                        AND COALESCE(lrm2.deleted, false) = false
                        AND lrm2.requestdatetime >= @fromdate
                        AND lrm2.requestdatetime < @todate + INTERVAL '1 day'
                  )
                  AND (@gcode IS NULL OR @gcode = '' OR @gcode = '0' OR tm.gcode::text = @gcode)
                ORDER BY lrd.requestguid, lrd.testsno;
            ";

                    var items = (await db.QueryAsync<dynamic>(
                        itemsSql,
                        new { fromdate = fromdate.Value, todate = todate.Value, gcode, tenant_code }
                    )).ToList();

                    // Query sub-parameters for multi-patient
                    string multiSubParamsSql = @"
                SELECT DISTINCT
                    COALESCE(parent_trm.tcode, trm.fromtcode) AS ParentTCode,
                    COALESCE(trm.col2, tm.name) AS SubTestName,
                    trm.slno
                FROM test_result_master trm
                INNER JOIN test_master tm ON tm.tcode = trm.tcode
                LEFT JOIN test_result_master parent_trm ON parent_trm.testresultid = trm.fromtestresultid AND parent_trm.tenant_code = @tenant_code
                WHERE (
                    trm.fromtcode IN (
                        SELECT DISTINCT lrd2.tcode
                        FROM lab_request_details lrd2
                        INNER JOIN lab_request_master lrm2 ON lrm2.requestguid = lrd2.requestguid
                        INNER JOIN test_master tm2 ON tm2.tcode = lrd2.tcode
                        WHERE lrd2.tenant_code = @tenant_code
                          AND COALESCE(lrm2.deleted, false) = false
                          AND lrm2.requestdatetime >= @fromdate
                          AND lrm2.requestdatetime < @todate + INTERVAL '1 day'
                          AND (@gcode IS NULL OR @gcode = '' OR @gcode = '0' OR tm2.gcode::text = @gcode)
                    )
                    OR
                    trm.fromtestresultid IN (
                        SELECT DISTINCT parent_trm2.testresultid
                        FROM lab_request_details lrd2
                        INNER JOIN lab_request_master lrm2 ON lrm2.requestguid = lrd2.requestguid
                        INNER JOIN test_result_master parent_trm2 ON parent_trm2.tcode = lrd2.tcode AND parent_trm2.tenant_code = @tenant_code
                        INNER JOIN test_master tm2 ON tm2.tcode = lrd2.tcode
                        WHERE lrd2.tenant_code = @tenant_code
                          AND COALESCE(lrm2.deleted, false) = false
                          AND lrm2.requestdatetime >= @fromdate
                          AND lrm2.requestdatetime < @todate + INTERVAL '1 day'
                          AND (@gcode IS NULL OR @gcode = '' OR @gcode = '0' OR tm2.gcode::text = @gcode)
                    )
                )
                AND trm.tenant_code = @tenant_code
                ORDER BY ParentTCode, trm.slno;
            ";

                    var multiSubParams = (await db.QueryAsync<dynamic>(
                        multiSubParamsSql,
                        new { fromdate = fromdate.Value, todate = todate.Value, gcode, tenant_code }
                    )).ToList();

                    foreach (var item in billsWithGuids)
                    {
                        var uniqueBillItems = items
                            .Where(i => (string)i.requestguid == item.RequestGUID)
                            .GroupBy(i => (int)i.tcode)
                            .Select(g => g.First())
                            .ToList();

                        item.Bill.Items = uniqueBillItems
                            .Select((i, index) => {
                                var tcode = (int)i.tcode;
                                return new CashBillItemModel
                                {
                                    SNo = index + 1,
                                    TCode = tcode,
                                    TestName = i.testname,
                                    Amount = i.amount ?? 0,
                                    GroupName = i.groupname,
                                    SubParameters = multiSubParams
                                        .Where(sp => (int)sp.parenttcode == tcode)
                                        .Select(sp => (string)sp.subtestname)
                                        .Distinct()
                                        .ToList()
                                };
                            }).ToList();
                    }

                    var bills = billsWithGuids.Select(x => x.Bill).ToList();

                    // Build WorklistRequestModel
                    var request = new WorklistRequestModel
                    {
                        Bills = bills,
                        GroupBy = "Department",
                        DateRangeText = fromdate.Value.Date == todate.Value.Date
                            ? $"{fromdate.Value:dd/MM/yyyy}"
                            : $"{fromdate.Value:dd/MM/yyyy} - {todate.Value:dd/MM/yyyy}",
                        LabName = companyInfo?.legal_name ?? string.Empty,
                        Address = companyInfo?.address_line1 ?? string.Empty,
                        MobileNo = companyInfo?.contact_number ?? string.Empty,
                        Email = companyInfo?.contact_email ?? string.Empty
                    };

                    // POST to ReportingServer GetMultiWorklist
                    var json = JsonSerializer.Serialize(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("/api/billreceipt/getmultiworklist", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Report server error {response.StatusCode}: {error}");
                    }

                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    throw new ArgumentException("Either requestguid or both fromdate and todate must be specified.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.WorklistPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string?> GetCultureReportAsync(Guid requestguid, string tenant_code, bool? isletterhead)
        {
            using var db = new NpgsqlConnection(_conn);

            static string? CombineGramStaining(string? gramStain, string? pusCells)
            {
                if (string.IsNullOrWhiteSpace(gramStain) && string.IsNullOrWhiteSpace(pusCells))
                    return null;
                if (string.IsNullOrWhiteSpace(gramStain))
                    return pusCells;
                if (string.IsNullOrWhiteSpace(pusCells))
                    return gramStain;
                return $"{pusCells}, {gramStain}";
            }

            // ── Step 1: Patient + Request header ─────────────────────────
            var header = await db.QueryFirstOrDefaultAsync<dynamic>(@"
        SELECT
            lrm.requestsno                                          AS sid,
            cm.custcode                                             AS patid,
            lrm.name                                               AS patientname,
            COALESCE(lrm.ageyears::text, '0') || ' Yrs'            AS age,
            lrm.gender                                             AS gender,
            dm.name                                                AS doctorname,
            CASE WHEN lrm.dcode IS NULL THEN 'SELF'
                 ELSE dm.name END                                  AS refby,
            lrm.requestdatetime                                    AS receivedon,
            lrm.requestsnoprint                                    AS barcode
        FROM lab_request_master lrm
        LEFT JOIN customerdb.customer_master cm
               ON cm.custid      = lrm.custid
        LEFT JOIN doctor_master dm
               ON dm.dcode       = lrm.dcode
              AND dm.tenant_code = lrm.tenant_code
        WHERE lrm.requestguid = @requestguid
          AND lrm.tenant_code = @tenant_code
          AND lrm.deleted     = false
        LIMIT 1",
                new { requestguid = requestguid.ToString(), tenant_code });

            if (header == null) return null;

            // ── Step 2: All culture masters for this request ──────────────────────
            var cultureMasters = (await db.QueryAsync<dynamic>(@"
        SELECT
            lcm.resultcultureguid::text AS resultcultureguid,
            lcm.isgrowth,
            lcm.reportingmethod,
            lcm.sample                  AS specimen,
            lcm.growthgrade,
            lcm.samplereceiveddate,
            lcm.culturereporteddate,
            lcm.smearafb,
            lcm.puscells,
            lcm.gramsstaining,
            lcm.diagnosis,
            lcm.organismsgrown,
            lcm.organismsgrownb,
            lcm.organismsgrownc,
            lcm.isisolationa,
            lcm.isisolationb,
            lcm.isisolationc,
            lcm.colonycount,
            lcm.colonycountb,
            lcm.colonycountc,
            lcm.comments,
            lcm.commentsa,
            lcm.commentsb,
            lcm.commentsc,
            lcm.reportstatus,
            lcm.isauthorized,
            COALESCE(tm.name,  tm2.name)  AS testname,
            COALESCE(gm.name,  gm2.name)  AS department,
            au.name                       AS signaturedoctorname,
            NULL::text                    AS signaturedoctordesignation,
            -- ✅ Return as TEXT (MinIO path), NOT bytea
            au.signature_image::text      AS signatureimagepath
        FROM lab_culture_master lcm
        LEFT JOIN LATERAL (
            SELECT tcode FROM lab_culture_details
            WHERE  resultcultureguid = lcm.resultcultureguid
              AND  tenant_code       = lcm.tenant_code
            LIMIT  1
        ) lcd ON true
        LEFT JOIN test_master  tm  ON tm.tcode  = lcd.tcode
        LEFT JOIN group_master gm  ON gm.gcode  = tm.gcode
        LEFT JOIN LATERAL (
            SELECT lrd.tcode
            FROM lab_request_details lrd
            WHERE lrd.requestguid = lcm.requestguid
              AND lrd.tenant_code = lcm.tenant_code
              AND lrd.tcode IN (
                  SELECT tcode FROM lab_culture_details
                  WHERE resultcultureguid = lcm.resultcultureguid
                    AND tenant_code       = lcm.tenant_code
              )
            LIMIT 1
        ) lrd2 ON true
        LEFT JOIN test_master  tm2 ON tm2.tcode = lrd2.tcode
        LEFT JOIN group_master gm2 ON gm2.gcode = tm2.gcode
        LEFT JOIN mastertenant.user_master au
               ON au.user_code = lcm.resultauthorizedby
        WHERE lcm.requestguid = @requestguid
          AND lcm.tenant_code = @tenant_code
          AND (lcm.deleted IS NULL OR lcm.deleted = false)
        ORDER BY COALESCE(tm.orderno, tm2.orderno)",
                new { requestguid = requestguid.ToString(), tenant_code })).ToList();

            if (cultureMasters.Count == 0) return null;

            // ── Step 3: Collect all unique signature MinIO paths & batch-fetch ────
            var allSignaturePaths = cultureMasters
                .Select(cm => (string?)GetDynVal(cm, "signatureimagepath"))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var imageCache = await BuildImageCacheAsync(allSignaturePaths);

            // ── Step 4: Build Tests list ──────────────────────────────────────────
            var tests = new List<CultureTestItemModel>();

            foreach (var cm in cultureMasters)
            {
                var rcGuidStr = (string?)GetDynVal(cm, "resultcultureguid");
                if (string.IsNullOrWhiteSpace(rcGuidStr)) continue;
                var rcGuid = Guid.Parse(rcGuidStr);

                var isGrowth = (bool?)GetDynVal(cm, "isgrowth") ?? false;
                var isIsoA = (bool?)GetDynVal(cm, "isisolationa") ?? false;
                var isIsoB = (bool?)GetDynVal(cm, "isisolationb") ?? false;
                var isIsoC = (bool?)GetDynVal(cm, "isisolationc") ?? false;
                var orgA = (string?)GetDynVal(cm, "organismsgrown");
                var orgB = (string?)GetDynVal(cm, "organismsgrownb");
                var orgC = (string?)GetDynVal(cm, "organismsgrownc");

                var organisms = new List<OrganismModel>();

                if (isGrowth)
                {
                    List<dynamic> isoRows;
                    try
                    {
                        isoRows = (await db.QueryAsync<dynamic>(@"
                            SELECT
                                lci.columnname,
                                lci.isolation_a,
                                lci.isolation_b,
                                lci.isolation_c,
                                lci.diskcontenta,
                                lci.diskcontentb,
                                lci.diskcontentc,
                                lci.mma1,
                                lci.mma2,
                                lci.mma3,
                                lci.actualsno,
                                lci.sno
                            FROM lab_culture_isolation lci
                            WHERE lci.resultcultureguid = @rcGuid
                              AND lci.tenant_code       = @tenant_code
                              AND lci.columnname        IS NOT NULL
                            ORDER BY lci.actualsno, lci.sno",
                            new { rcGuid, tenant_code })).ToList();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: lab_culture_isolation query failed: {ex.Message}. Falling back to empty isolation list.");
                        isoRows = new List<dynamic>();
                    }

                    if (isIsoA && !string.IsNullOrWhiteSpace(orgA))
                    {
                        var rowsA = isoRows
                            .Where(r => !string.IsNullOrWhiteSpace((string?)GetDynVal(r, "isolation_a")))
                            .ToList();
                        organisms.Add(BuildOrganism(
                            orgA,
                            (string?)GetDynVal(cm, "colonycount"),
                            (string?)GetDynVal(cm, "commentsa"),
                            rowsA, "isolation_a", "diskcontenta", "mma1"));
                    }

                    if (isIsoB && !string.IsNullOrWhiteSpace(orgB))
                    {
                        var rowsB = isoRows
                            .Where(r => !string.IsNullOrWhiteSpace((string?)GetDynVal(r, "isolation_b")))
                            .ToList();
                        organisms.Add(BuildOrganism(
                            orgB,
                            (string?)GetDynVal(cm, "colonycountb"),
                            (string?)GetDynVal(cm, "commentsb"),
                            rowsB, "isolation_b", "diskcontentb", "mma2"));
                    }

                    if (isIsoC && !string.IsNullOrWhiteSpace(orgC))
                    {
                        var rowsC = isoRows
                            .Where(r => !string.IsNullOrWhiteSpace((string?)GetDynVal(r, "isolation_c")))
                            .ToList();
                        organisms.Add(BuildOrganism(
                            orgC,
                            (string?)GetDynVal(cm, "colonycountc"),
                            (string?)GetDynVal(cm, "commentsc"),
                            rowsC, "isolation_c", "diskcontentc", "mma3"));
                    }
                }

                tests.Add(new CultureTestItemModel
                {
                    Department = (string?)GetDynVal(cm, "department") ?? "Microbiology",
                    ReportType = (string?)GetDynVal(cm, "reportstatus") ?? "Final Report",
                    TestName = (string?)GetDynVal(cm, "testname") ?? "",
                    Specimen = (string?)GetDynVal(cm, "specimen"),
                    ReportingMethod = (string?)GetDynVal(cm, "reportingmethod") ?? "",
                    GramStaining = CombineGramStaining((string?)GetDynVal(cm, "gramsstaining"), (string?)GetDynVal(cm, "puscells")),
                    PusCells = (string?)GetDynVal(cm, "puscells"),
                    Diagnosis = (string?)GetDynVal(cm, "diagnosis") ?? "",
                    Interpretation = BuildInterpretation(cm),
                    Organisms = organisms
                });
            }

            // ── Step 5: Resolve signature for last authorized record ─────────────
            var lastAuth = cultureMasters
                               .LastOrDefault(x => (bool?)GetDynVal(x, "isauthorized") == true)
                           ?? cultureMasters.Last();

            var signaturePath = (string?)GetDynVal(lastAuth, "signatureimagepath");
            var signatureBytes = GetImage(signaturePath, imageCache);

            // ── Step 6: Company info ──────────────────────────────────────────────
            var res = await db.QueryFirstOrDefaultAsync<Tenant>(
                @"SELECT legal_name, address_line1, contact_number, contact_email
          FROM mastertenant.tenants
          WHERE tenant_code = @tenant_code",
                new { tenant_code }
            );

            // ── Step 7: Assemble final DTO ────────────────────────────────────────
            var headerDict = (IDictionary<string, object>)header;
            var firstCulture = (IDictionary<string, object>)cultureMasters.First();

            static DateTime? SafeDate(IDictionary<string, object> d, string key)
            {
                if (!d.TryGetValue(key, out var v)) return null;
                if (v is DateTime dt) return dt;
                if (v is DateTimeOffset dto) return dto.DateTime;
                if (v != null && DateTime.TryParse(v.ToString(), out var parsed)) return parsed;
                return null;
            }

            var results = new CultureReportDto
            {
                TenantId = tenant_code,
                logo = null,
                headerimage = null,
                footerimage = null,
                isletterhead = isletterhead ?? false,

                LabName = res?.legal_name ?? string.Empty,
                Address = res?.address_line1 ?? string.Empty,
                MobileNo = res?.contact_number ?? string.Empty,
                Email = res?.contact_email ?? string.Empty,

                SID = headerDict.TryGetValue("sid", out var sid) ? sid?.ToString() ?? "" : "",
                PatId = headerDict.TryGetValue("patid", out var pid) ? pid?.ToString() ?? "" : "",
                PatientName = headerDict.TryGetValue("patientname", out var pn) ? pn?.ToString() ?? "" : "",
                Age = headerDict.TryGetValue("age", out var age) ? age?.ToString() ?? "" : "",
                Gender = headerDict.TryGetValue("gender", out var gen) ? gen?.ToString() ?? "" : "",
                DoctorName = headerDict.TryGetValue("doctorname", out var dn) ? dn?.ToString() : null,
                RefBy = headerDict.TryGetValue("refby", out var rb) ? rb?.ToString() ?? "SELF" : "SELF",

                CollectedOn = SafeDate(firstCulture, "samplereceiveddate") ?? DateTime.Now,
                ReceivedOn = SafeDate(headerDict, "receivedon") ?? DateTime.Now,
                ReportedOn = GetDynVal(lastAuth, "culturereporteddate") is DateTime repDt
                                  ? repDt : DateTime.Now,

                Barcode = headerDict.TryGetValue("barcode", out var bc) ? bc?.ToString() ?? "" : "",

                GramStaining = CombineGramStaining(
                    firstCulture.TryGetValue("gramsstaining", out var gs) ? gs?.ToString() : null,
                    firstCulture.TryGetValue("puscells", out var pc) ? pc?.ToString() : null),
                PusCells = firstCulture.TryGetValue("puscells", out var pc2) ? pc2?.ToString() : null,

                Tests = tests,

                SignatureImage = signatureBytes,
                SignatureDoctorName = (string?)GetDynVal(lastAuth, "signaturedoctorname"),
                SignatureDoctorDesignation = (string?)GetDynVal(lastAuth, "signaturedoctordesignation")
            };

            // ── Step 8: POST to report server ─────────────────────────────────────
            var json = JsonSerializer.Serialize(results);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/culture/getculturereport", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Report server error {response.StatusCode}: {error}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        private static OrganismModel BuildOrganism(
            string? name,
            string? colonyCount,
            string? comments,
            IList<dynamic> isoRows,
            string isolationCol,
            string diskCol,
            string micCol)
        {
            var antibiotics = isoRows
                .Where(r => !string.IsNullOrWhiteSpace((string?)GetDynVal(r, "columnname")))
                .Select(r => new AntibioticSensitivityModel
                {
                    AntibioticName = (string?)GetDynVal(r, "columnname"),
                    Result = (string?)GetDynVal(r, isolationCol),
                    Disk = GetDynVal(r, diskCol)?.ToString(),
                    MIC = GetDynVal(r, micCol)?.ToString()
                })
                .ToList();

            return new OrganismModel
            {
                OrganismName = name,
                ColonyCount = colonyCount,
                Comments = comments,
                Antibiotics = antibiotics
            };
        }

        private static string BuildInterpretation(dynamic cm)
        {
            var parts = new List<string>();

            var puscells = GetDynVal(cm, "puscells")?.ToString();
            var gramsstaining = GetDynVal(cm, "gramsstaining")?.ToString();
            var smearafb = GetDynVal(cm, "smearafb")?.ToString();
            var comments = GetDynVal(cm, "comments")?.ToString();

            if (!string.IsNullOrWhiteSpace(puscells)) parts.Add($"Pus Cells: {puscells}");
            if (!string.IsNullOrWhiteSpace(gramsstaining)) parts.Add($"Gram Staining: {gramsstaining}");
            if (!string.IsNullOrWhiteSpace(smearafb)) parts.Add($"Smear AFB: {smearafb}");
            if (!string.IsNullOrWhiteSpace(comments)) parts.Add(comments);

            return string.Join(". ", parts);
        }

        private static object? GetDynVal(dynamic obj, string prop)
        {
            if (obj == null) return null;
            try
            {
                var dict = (IDictionary<string, object>)obj;
                return dict.TryGetValue(prop, out var val) ? val : null;
            }
            catch { return null; }
        }

        private async Task<byte[]?> FetchImageBytesAsync(string? minioKey)
        {
            if (string.IsNullOrWhiteSpace(minioKey)) return null;

            try
            {
                var result = await _s3Service.DownloadAsync(minioKey);

                if (result == null) return null;

                return result.Value.Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportClass] Image fetch failed for '{minioKey}': {ex.Message}");
                return null;
            }
        }

        private async Task<Dictionary<string, byte[]>> BuildImageCacheAsync(
            IEnumerable<string?> paths)
        {
            var unique = paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cache = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            await Task.WhenAll(unique.Select(async path =>
            {
                var bytes = await FetchImageBytesAsync(path);
                if (bytes != null)
                    cache[path] = bytes;
            }));

            return new Dictionary<string, byte[]>(cache, StringComparer.OrdinalIgnoreCase);
        }

        private static byte[]? GetImage(string? path, Dictionary<string, byte[]> cache)
            => string.IsNullOrWhiteSpace(path) ? null
               : cache.TryGetValue(path, out var b) ? b : null;

        private static DateTime? ToLocalReportTime(DateTime? dt, DateTime baseRef)
        {
            if (!dt.HasValue || dt.Value == DateTime.MinValue) return dt;
            DateTime val = dt.Value;
            if (val.Kind == DateTimeKind.Utc)
            {
                return val.ToLocalTime();
            }
            if (baseRef != DateTime.MinValue && val < baseRef.AddMinutes(-30))
            {
                try
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(val, DateTimeKind.Utc), TimeZoneInfo.Local);
                }
                catch { }
            }
            return val;
        }

                                private static RoutineReportModel MapRow(
            RawReportRow r, Dictionary<string, byte[]> cache) => new()
            {
                TenantId = r.TenantId ?? "",
                RequestSno = r.RequestSno,
                RequestBarCode = string.IsNullOrWhiteSpace(r.RequestBarCode)
                    ? GenerateBlankPng()
                    : GenerateBarcodePng(r.RequestBarCode),
                RequestDateTime = ToLocalReportTime(r.RequestDateTime, DateTime.MinValue) ?? r.RequestDateTime,
                RequestedDateTime = ToLocalReportTime(r.RequestedDateTime, DateTime.MinValue) ?? r.RequestedDateTime,
                Name = r.Name ?? "",
                Gender = r.Gender ?? "",
                DateofBirth = r.DateofBirth ?? "",
                AgeYears = r.AgeYears,
                AgeMonths = r.AgeMonths,
                AgeDays = r.AgeDays,
                Address = r.Address ?? "",
                MobileNo = r.MobileNo ?? "",
                RequestAmount = r.RequestAmount,
                Description = r.Description ?? "",
                CustCode = r.CustCode ?? "",
                Sample = r.Sample ?? "",
                TestName = r.TestName ?? "",
                GroupName = r.GroupName ?? "",
                Doctor = r.Doctor ?? "",
                Initial = r.Initial ?? "",
                NameTitle = r.NameTitle ?? "",
                Reference = r.Reference ?? "",
                DoctorCode = r.DoctorCode ?? "",
                EnteredResult = r.EnteredResult ?? "",
                Reporting = r.Reporting ?? "",
                CityName = r.CityName ?? "",
                AreaPinCode = r.AreaPinCode ?? "",
                AreaName = r.AreaName ?? "",
                RequestGUID = r.RequestGUID ?? "",
                TestSno = r.TestSno,
                Col2 = r.Col2 ?? "",
                Units = (!string.IsNullOrWhiteSpace(r.Units) ? r.Units : r.TRPUName) ?? "",
                ResultSno = r.ResultSno,
                CustomerImage = GetImage(r.CustomerImage, cache),
                SignatureImage = GetImage(r.SignatureImage, cache),
                ResultGUID = r.ResultGUID ?? "",
                ValueType = r.ValueType ?? "",
                TCode = r.TCode,
                ResultDateTime = ToLocalReportTime(r.ResultDateTime, r.RequestDateTime),
                ResultType = r.ResultType ?? "",
                PrintInSeparatePage = r.PrintInSeparatePage,
                TestOrderNo = r.TestOrderNo,
                GroupOrderNo = r.GroupOrderNo,
                RoomNo = r.RoomNo ?? "",
                HospitalID = r.HospitalID ?? "",
                Email = r.Email ?? "",
                AlteredBHCode = r.AlteredBHCode ?? "",
                CollectedDateTime = ToLocalReportTime(r.CollectedDateTime, r.RequestDateTime),
                OnlineCode = r.OnlineCode ?? "",
                ResultValueType = r.ResultValueType ?? "",
                DefaultValue = r.DefaultValue ?? "",
                SimpleNormalValues = r.SimpleNormalValues,
                DetailedNormalValues = r.DetailedNormalValues,
                RangeType = r.RangeType ?? "",
                FromNormalValue = r.FromNormalValue,
                ToNormalValue = r.ToNormalValue,
                ConclusionForHigher = r.ConclusionForHigher ?? "",
                ConclusionForLower = r.ConclusionForLower ?? "",
                ConclusionForFixedText = r.ConclusionForFixedText ?? "",
                PrintFixedTextConclusionInReport = r.PrintFixedTextConclusionInReport,
                ShowAgedBased = r.ShowAgedBased,
                ShowAlertOnHigherLower = r.ShowAlertOnHigherLower,
                FooterMessage = r.FooterMessage ?? "",
                TRPUName = r.TRPUName ?? "",
                FixedValues = r.FixedValues ?? "",
                DecimalPlaces = r.DecimalPlaces,
                ReportingMethod = r.ReportingMethod ?? "",
                TestResultID = r.TestResultID,
                RequestSnoPrint = r.RequestSnoPrint ?? "",
                PrintResultOnly = r.PrintResultOnly,
                ResultNormal = r.ResultNormal,
                ResultHigh = r.ResultHigh,
                ResultLow = r.ResultLow,
                IsInvestigationPartial = r.IsInvestigationPartial,
                ResultSample = r.ResultSample ?? "",
                DoctorFullName = r.DoctorFullName ?? "",
                DCode = r.DCode,
                FrontHospitalID = r.FrontHospitalID ?? "",
                FrontHospitalPatientID = r.FrontHospitalPatientID ?? "",
                IsAuthorized1 = r.IsAuthorized1,
                DoctorTitle = r.DoctorTitle ?? "",
                SecondDoctorName = r.SecondDoctorName ?? "",
                SecondDCode = r.SecondDCode,
                DefaultAuthorizeImage = GetImage(r.DefaultAuthorizeImage, cache),
                DefaultAuthorizeName = r.DefaultAuthorizeName ?? "",
                DefaultAuthorizeDesignation = r.DefaultAuthorizeDesignation ?? "",
                NormalValues = r.NormalValues ?? "",
                RowNum = r.RowNum,
                MCCode = r.MCCode,
            };

        private static AuthorizedUser MapAuth(
            RawAuthUser a, Dictionary<string, byte[]> cache) => new()
            {
                EnteredSign = GetImage(a.EnteredSign, cache) ?? GenerateBlankPng(),
                EnteredBy = a.EnteredBy ?? "",
                EnteredByDesignation = a.EnteredByDesignation ?? "",
                AuthorizedSign = GetImage(a.AuthorizedSign, cache) ?? GenerateBlankPng(),
                AuthorizedBy = a.AuthorizedBy ?? "",
                AuthorizedByDesignation = a.AuthorizedByDesignation ?? "",
                AuthorizedSign2 = GetImage(a.AuthorizedSign2, cache) ?? GenerateBlankPng(),
                AuthorizedBy2 = a.AuthorizedBy2 ?? "",
                AuthorizedByDesignation2 = a.AuthorizedByDesignation2 ?? "",
            };

        public async Task<(byte[]? HeaderImage, byte[]? FooterImage)> GetHeaderFooterImagesAsync(string tenant_code, int? bh_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = "SELECT header_path, header_image_path, footer_path, footer_image_path FROM lab_settings WHERE tenant_code = @tenant_code AND deleted = false";
                if (bh_code.HasValue && bh_code.Value != 0)
                {
                    sql += " AND bh_code = @bh_code";
                }
                else
                {
                    sql += " AND (bh_code = 0 OR bh_code IS NULL)";
                }

                var row = await db.QueryFirstOrDefaultAsync<dynamic>(sql, new { tenant_code, bh_code });

                if (row == null && bh_code.HasValue && bh_code.Value != 0)
                {
                    string fallbackSql = "SELECT header_path, header_image_path, footer_path, footer_image_path FROM lab_settings WHERE tenant_code = @tenant_code AND deleted = false AND (bh_code = 0 OR bh_code IS NULL)";
                    row = await db.QueryFirstOrDefaultAsync<dynamic>(fallbackSql, new { tenant_code });
                }

                if (row == null)
                    return (null, null);

                var dict = (IDictionary<string, object>)row;
                string? headerKey = (dict.TryGetValue("header_path", out var hp) && hp != null) ? hp.ToString() : (dict.TryGetValue("header_image_path", out var hip) && hip != null ? hip.ToString() : null);
                string? footerKey = (dict.TryGetValue("footer_path", out var fp) && fp != null) ? fp.ToString() : (dict.TryGetValue("footer_image_path", out var fip) && fip != null ? fip.ToString() : null);

                byte[]? headerBytes = await FetchImageBytesAsync(headerKey);
                byte[]? footerBytes = await FetchImageBytesAsync(footerKey);

                return (headerBytes, footerBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetHeaderFooterImagesAsync ERROR: {ex.Message}");
                return (null, null);
            }
        }

        private static string CleanNormalValues(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var lines = raw.Split(new[] { "@", "\r\n", "\n", "<br/>", "<br>" }, StringSplitOptions.RemoveEmptyEntries);
            var cleanedLines = new List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(":"))
                {
                    trimmed = trimmed.Substring(1).Trim();
                }
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    if (!cleanedLines.Contains(trimmed))
                    {
                        cleanedLines.Add(trimmed);
                    }
                }
            }

            return string.Join("<br/>", cleanedLines);
        }

        public async Task<string?> GetLabReportAsync(Guid requestguid, string tenant_code, bool? isletterhead)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                const string resultSql = @"
SELECT
    lrm.tenant_code                                                     AS TenantId,
    COALESCE(lrm.requestsno, 0)                                         AS RequestSno,
    COALESCE(
        NULLIF(lrm.requestconvertedbarcode, ''),
        NULLIF(lrm.requestbarcode, ''),
        lrm.requestsnoprint,
        ''
    )                                                                    AS RequestBarCode,
    lrm.requestdatetime::timestamp                                      AS RequestDateTime,
    COALESCE(lrm.requesteddatetime,
             lrm.requestdatetime)::timestamp                            AS RequestedDateTime,
    COALESCE(lrm.name,        '')                                       AS Name,
    COALESCE(lrm.gender,      '')                                       AS Gender,
    COALESCE(lrm.dateofbirth, '')                                       AS DateofBirth,
    CASE WHEN COALESCE(NULLIF(TRIM(lrm.ageyears::text), '')::int, 0) = 0 AND COALESCE(NULLIF(TRIM(lrm.agemonths::text), '')::int, 0) = 0 AND COALESCE(NULLIF(TRIM(lrm.agedays::text), '')::int, 0) = 0 THEN COALESCE(cm.ageyears, 0) ELSE COALESCE(NULLIF(TRIM(lrm.ageyears::text), '')::int, 0) END AS AgeYears,
    CASE WHEN COALESCE(NULLIF(TRIM(lrm.ageyears::text), '')::int, 0) = 0 AND COALESCE(NULLIF(TRIM(lrm.agemonths::text), '')::int, 0) = 0 AND COALESCE(NULLIF(TRIM(lrm.agedays::text), '')::int, 0) = 0 THEN COALESCE(cm.agemonths, 0) ELSE COALESCE(NULLIF(TRIM(lrm.agemonths::text), '')::int, 0) END AS AgeMonths,
    CASE WHEN COALESCE(NULLIF(TRIM(lrm.ageyears::text), '')::int, 0) = 0 AND COALESCE(NULLIF(TRIM(lrm.agemonths::text), '')::int, 0) = 0 AND COALESCE(NULLIF(TRIM(lrm.agedays::text), '')::int, 0) = 0 THEN COALESCE(cm.agedays, 0) ELSE COALESCE(NULLIF(TRIM(lrm.agedays::text), '')::int, 0) END AS AgeDays,
    COALESCE(lrm.address,  '')                                          AS Address,
    COALESCE(lrm.mobileno, '')                                          AS MobileNo,
    COALESCE(lrm.requestamount, 0)::float8                              AS RequestAmount,
    COALESCE(NULLIF(lrdd.description, ''), NULLIF(lrdd.quotescolumn, ''), tm.name, '') AS Description,
    COALESCE(crm.custcode, cm.custcode, '')                             AS CustCode,
    COALESCE(sm.name,             '')                                   AS Sample,
    COALESCE(tm.name,             '')                                   AS TestName,
    COALESCE(gm.name,             '')                                   AS GroupName,
    COALESCE(NULLIF(TRIM(dm.name), ''), CASE WHEN lrm.dcode IS NULL OR lrm.dcode = 0 THEN 'SELF' ELSE '' END) AS Doctor,
    COALESCE(dm.initial,   '')                                          AS Initial,
    COALESCE(dm.nametitle, '')                                          AS NameTitle,
    COALESCE(dm.reference, '')                                          AS Reference,
    COALESCE(lrm.dcode::text, '')                                       AS DoctorCode,
    COALESCE(lrdd.enteredresult, '')                                    AS EnteredResult,
    COALESCE(rtm.name,           '')                                    AS Reporting,
    ''                                                                  AS CityName,
    COALESCE(ar.areapincode, '')                                        AS AreaPinCode,
    COALESCE(ar.areaname,    '')                                        AS AreaName,
    lrm.requestguid::text                                               AS RequestGUID,
    COALESCE(lrd.testsno,       0)                                      AS TestSno,
    COALESCE(lrdd.quotescolumn, '')                                     AS Col2,
    COALESCE(
        NULLIF(lrdd.units, ''),
        NULLIF(uom.name,   ''),
        ''
    )                                                                   AS Units,
    COALESCE(lrdd.testsno,      0)                                      AS ResultSno,
    NULL::text                                                          AS CustomerImage,
    NULL::text                                                          AS SignatureImage,
    COALESCE(lrm_res.resultguid::text, '')                              AS ResultGUID,
    COALESCE(lrdd.valuetype,     '')                                    AS ValueType,
    COALESCE(NULLIF(TRIM(lrd.tcode::text), '')::int, 0)                 AS TCode,
    lrm_res.resultdatetime                                              AS ResultDateTime,
    CASE
        WHEN lrdd.resulttype IN ('F', 'Footer', 'footer') THEN 'Footer'
        WHEN lrdd.resulttype IS NULL OR lrdd.resulttype = '' THEN ''
        ELSE 'Result'
    END                                                                 AS ResultType,
    COALESCE(tm.printinseparatepage, false)                             AS PrintInSeparatePage,
    COALESCE(tm.orderno, 0)                                             AS TestOrderNo,
    COALESCE(gm.orderno, 0)                                             AS GroupOrderNo,
    COALESCE(lrm.roomno,     '')                                        AS RoomNo,
    COALESCE(lrm.hospitalid, '')                                        AS HospitalID,
    COALESCE(cm.email,       '')                                        AS Email,
    COALESCE(lrm.collecteddatetime, sc.collectedtime)::timestamp        AS CollectedDateTime,
    COALESCE(lrm.onlinecode, '')                                        AS OnlineCode,
    COALESCE(NULLIF(lrp.resultvaluetype, ''), NULLIF(lrdd.valuetype, ''), trp_master.resultvaluetype, '') AS ResultValueType,
    COALESCE(NULLIF(lrp.defaultvalue, ''), trp_master.defaultvalue, '') AS DefaultValue,
    COALESCE(lrp.simplenormalvalues, trp_master.simplenormalvalues, false) AS SimpleNormalValues,
    COALESCE(lrp.detailednormalvalues, trp_master.detailednormalvalues, false) AS DetailedNormalValues,
    COALESCE(NULLIF(lrp.rangetype, ''), trp_master.rangetype, '')       AS RangeType,
    COALESCE(lrp.fromnormalvalue, trp_master.fromnormalvalue, 0)::float8 AS FromNormalValue,
    COALESCE(lrp.tonormalvalue,   trp_master.tonormalvalue, 0)::float8 AS ToNormalValue,
    COALESCE(NULLIF(lrp.conclusionforhigher, ''), trp_master.conclusionforhigher, '') AS ConclusionForHigher,
    COALESCE(NULLIF(lrp.conclusionforlower, ''), trp_master.conclusionforlower, '') AS ConclusionForLower,
    COALESCE(NULLIF(NULLIF(lrp.conclusionforfixedtext, '0'), ''), NULLIF(NULLIF(trp_master.conclusionforfixedtext, '0'), ''), '') AS ConclusionForFixedText,
    COALESCE(lrp.printfixedtextconclusioninreport, trp_master.printfixedtextconclusioninreport, false) AS PrintFixedTextConclusionInReport,
    COALESCE(lrp.showagedbased,         trp_master.showagedbased, false) AS ShowAgedBased,
    COALESCE(lrp.showalertonhigherlower, trp_master.showalertonhigherlower, false) AS ShowAlertOnHigherLower,
    COALESCE(NULLIF(lrp.footermessage, ''), trp_master.footermessage, '') AS FooterMessage,
    COALESCE(uom.name,                   '')                            AS TRPUName,
    COALESCE(lrp.decimalvalue,           trp_master.decimalvalue, 0)   AS DecimalPlaces,
    COALESCE(rtm_master.name, rtm.name,  '')                            AS ReportingMethod,
    COALESCE(
        NULLIF(lrdd.testresultid, '00000000-0000-0000-0000-000000000000'::uuid),
        NULLIF(lrp.mastertestresultid, '00000000-0000-0000-0000-000000000000'::uuid),
        trm_master.testresultid,
        '00000000-0000-0000-0000-000000000000'::uuid
    )                                                                   AS TestResultID,
    COALESCE(lrm.requestsnoprint, '')                                   AS RequestSnoPrint,
    COALESCE(lrp.printresultonly, trp_master.printresultonly, false)    AS PrintResultOnly,
    COALESCE(lrp.resultnormal,    false)                                AS ResultNormal,
    COALESCE(lrp.resulthigh,      false)                                AS ResultHigh,
    COALESCE(lrp.resultlow,       false)                                AS ResultLow,
    COALESCE(lrm.isinvestigationpartial, false)                         AS IsInvestigationPartial,
    COALESCE(sm.name,  '')                                              AS ResultSample,
    COALESCE(NULLIF(TRIM(dm.name), ''), CASE WHEN lrm.dcode IS NULL OR lrm.dcode = 0 THEN 'SELF' ELSE '' END) AS DoctorFullName,
    COALESCE(lrm.dcode::int,  0)                                        AS DCode,
    COALESCE(lrm.fronthospitalid,        '')                            AS FrontHospitalID,
    COALESCE(lrm.fronthospitalpatientid, '')                            AS FrontHospitalPatientID,
    COALESCE(lrd.isauthorized1, false)                                  AS IsAuthorized1,
    COALESCE(dm.nametitle,  '')                                         AS DoctorTitle,
    COALESCE(lrm.seconddoctorname, '')                                  AS SecondDoctorName,
    COALESCE(lrm.seconddcode::int,  0)                                  AS SecondDCode,
    au.signature_image                                                  AS DefaultAuthorizeImage,
    COALESCE(au.name,        '')                                        AS DefaultAuthorizeName,
    COALESCE(au.description, '')                                        AS DefaultAuthorizeDesignation,
    -- Raw fallback only. Age/sex-based detailed ranges, text-normal-value matching,
    -- and fixed-text conclusion appending are all handled in C# by SetNormalRanges().
    COALESCE(
        NULLIF(lrdd.normalvalues, ''),
        NULLIF(lrp.normalvalue, ''),
        NULLIF(trp_master.normalvalue, ''),
        ''
    )                                                                   AS NormalValues,
    ROW_NUMBER() OVER (
        ORDER BY COALESCE(gm.orderno, 9999),
                 COALESCE(tm.orderno, 9999),
                 COALESCE(lrd.testsno, 0),
                 COALESCE(lrdd.testsno, 0)
    )::int                                                              AS RowNum,
    COALESCE(lrp.mccode, trp_master.mccode, 0)                          AS MCCode,
    COALESCE(NULLIF(TRIM(lrm.alteredbhcode::text), ''), lrm.enteredbhcode::text, '0') AS AlteredBHCode,
    lrp.image_path                                                      AS ResultImagePath
FROM lab_request_master lrm
LEFT JOIN customerdb.customer_master cm
       ON cm.custid      = lrm.custid
LEFT JOIN customerdb.customer_registration_master crm
       ON crm.custid = cm.custid AND (LOWER(crm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(crm.tenant_code) = LOWER(@tenant_code) OR crm.tenant_code = '0' OR crm.tenant_code IS NULL)
LEFT JOIN doctor_master dm
       ON dm.dcode       = lrm.dcode
      AND (LOWER(dm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(dm.tenant_code) = LOWER(@tenant_code) OR dm.tenant_code = '0' OR dm.tenant_code IS NULL)
LEFT JOIN lab_request_details lrd
       ON lrd.requestguid::text = lrm.requestguid::text
      AND (LOWER(lrd.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(lrd.tenant_code) = LOWER(@tenant_code) OR lrd.tenant_code = '0' OR lrd.tenant_code IS NULL)
      AND (lrd.ttid = 1 OR lrd.ttid = 0 OR lrd.ttid IS NULL)
LEFT JOIN test_master tm
       ON tm.tcode       = lrd.tcode
      AND (LOWER(tm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(tm.tenant_code) = LOWER(@tenant_code) OR tm.tenant_code = '0' OR tm.tenant_code IS NULL)
LEFT JOIN group_master gm
       ON gm.gcode       = tm.gcode
      AND (LOWER(gm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(gm.tenant_code) = LOWER(@tenant_code) OR gm.tenant_code = '0' OR gm.tenant_code IS NULL)
LEFT JOIN LATERAL (
    SELECT *
    FROM lab_result_master lrm_sub
    WHERE lrm_sub.requestguid::text = lrm.requestguid::text
      AND (LOWER(lrm_sub.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(lrm_sub.tenant_code) = LOWER(@tenant_code) OR lrm_sub.tenant_code = '0' OR lrm_sub.tenant_code IS NULL)
      AND COALESCE(lrm_sub.deleted, false) = false
    ORDER BY lrm_sub.entereddate DESC NULLS LAST, lrm_sub.resultdatetime DESC NULLS LAST
    LIMIT 1
) lrm_res ON true
LEFT JOIN lab_result_details lrdd
       ON lrdd.resultguid  = lrm_res.resultguid
      AND lrdd.tcode       = lrd.tcode
      AND (LOWER(lrdd.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(lrdd.tenant_code) = LOWER(@tenant_code) OR lrdd.tenant_code = '0' OR lrdd.tenant_code IS NULL)
LEFT JOIN lab_result_properties lrp
       ON lrp.testresultid = lrdd.testresultid
      AND (LOWER(lrp.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(lrp.tenant_code) = LOWER(@tenant_code) OR lrp.tenant_code = '0' OR lrp.tenant_code IS NULL)
LEFT JOIN LATERAL (
    SELECT trm.testresultid
    FROM test_result_master trm
    WHERE trm.tcode = lrd.tcode
      AND (LOWER(trm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(trm.tenant_code) = LOWER(@tenant_code) OR trm.tenant_code = '0' OR trm.tenant_code IS NULL)
    ORDER BY (LOWER(trm.tenant_code) = LOWER(lrm.tenant_code)) DESC
    LIMIT 1
) trm_master ON true
LEFT JOIN LATERAL (
    SELECT trp.*
    FROM test_result_properties trp
    WHERE trp.testresultid = COALESCE(
        NULLIF(lrp.mastertestresultid, '00000000-0000-0000-0000-000000000000'::uuid),
        trm_master.testresultid
    )
      AND (LOWER(trp.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(trp.tenant_code) = LOWER(@tenant_code) OR trp.tenant_code = '0' OR trp.tenant_code IS NULL)
    ORDER BY (LOWER(trp.tenant_code) = LOWER(lrm.tenant_code)) DESC, (trp.usedefault = true) DESC
    LIMIT 1
) trp_master ON true
LEFT JOIN sample_master sm
       ON sm.scode       = COALESCE(lrp.scode, trp_master.scode)
      AND (LOWER(sm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(sm.tenant_code) = LOWER(@tenant_code) OR sm.tenant_code = '0' OR sm.tenant_code IS NULL)
LEFT JOIN uom_master uom
       ON uom.ucode       = COALESCE(lrp.defaultunitscode, trp_master.defaultunitscode)::bigint
      AND (LOWER(uom.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(uom.tenant_code) = LOWER(@tenant_code) OR uom.tenant_code = '0' OR uom.tenant_code IS NULL)
LEFT JOIN report_method rtm
       ON rtm.rtmcode     = lrp.rtmcode::bigint
      AND (LOWER(rtm.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(rtm.tenant_code) = LOWER(@tenant_code) OR rtm.tenant_code = '0' OR rtm.tenant_code IS NULL)
LEFT JOIN report_method rtm_master
       ON rtm_master.rtmcode     = trp_master.rtmcode::bigint
      AND (LOWER(rtm_master.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(rtm_master.tenant_code) = LOWER(@tenant_code) OR rtm_master.tenant_code = '0' OR rtm_master.tenant_code IS NULL)
LEFT JOIN LATERAL (
    SELECT sc_sub.collectedtime
    FROM   lab_request_specimencollection sc_sub
    WHERE  sc_sub.requestguid::text = lrm.requestguid::text
      AND  sc_sub.scode       = COALESCE(lrp.scode, trp_master.scode)
      AND  (LOWER(sc_sub.tenant_code) = LOWER(lrm.tenant_code) OR LOWER(sc_sub.tenant_code) = LOWER(@tenant_code) OR sc_sub.tenant_code = '0' OR sc_sub.tenant_code IS NULL)
      AND  COALESCE(sc_sub.isdeleted, false) = false
    ORDER  BY sc_sub.collectedtime DESC
    LIMIT  1
) sc ON true
LEFT JOIN area_master ar
       ON ar.areacode     = lrm.areacode
LEFT JOIN LATERAL (
    SELECT u.name, u.description, u.signature_image
    FROM mastertenant.user_master u
    WHERE u.user_code = lrd.resultauthorizedby
    LIMIT 1
) au ON true
WHERE (lrm.requestguid::text = @requestguid OR LOWER(lrm.requestguid::text) = LOWER(@requestguid))
  AND (LOWER(lrm.tenant_code) = LOWER(@tenant_code) OR lrm.tenant_code IS NULL OR lrm.tenant_code = '0' OR @tenant_code IS NULL OR @tenant_code = '' OR @tenant_code = '0')
  AND COALESCE(lrm.deleted, false) = false
ORDER BY COALESCE(gm.orderno, 9999),
         COALESCE(tm.orderno, 9999),
         COALESCE(lrd.testsno, 0),
         COALESCE(lrdd.testsno, 0);"; 
                
                var rawRows = (await db.QueryAsync<RawReportRow>(
                    resultSql, new { requestguid = requestguid.ToString(), tenant_code }
                )).ToList();

                if (rawRows.Count == 0) return null;

                rawRows = (await SetNormalRanges(rawRows.ToList(), db)).ToList();

                const string authSql = @"
SELECT DISTINCT
    COALESCE(eu.name,        '')    AS EnteredBy,
    COALESCE(eu.description, '')    AS EnteredByDesignation,
    eu.signature_image               AS EnteredSign,
    COALESCE(au1.name,        '')   AS AuthorizedBy,
    COALESCE(au1.description, '')   AS AuthorizedByDesignation,
    au1.signature_image              AS AuthorizedSign,
    COALESCE(au2.name,        '')   AS AuthorizedBy2,
    COALESCE(au2.description, '')   AS AuthorizedByDesignation2,
    au2.signature_image              AS AuthorizedSign2
FROM lab_request_details lrd
LEFT JOIN mastertenant.user_master eu
       ON eu.user_code  = lrd.resultenteredby
LEFT JOIN mastertenant.user_master au1
       ON au1.user_code = lrd.resultauthorizedby
LEFT JOIN mastertenant.user_master au2
       ON au2.user_code = lrd.resultauthorizedby2
WHERE lrd.requestguid::text = @requestguid::text
  AND (lrd.tenant_code = @tenant_code OR lrd.tenant_code = '0' OR lrd.tenant_code IS NULL)
  AND (lrd.ttid = 1 OR lrd.ttid = 0 OR lrd.ttid IS NULL)";

                var rawAuth = (await db.QueryAsync<RawAuthUser>(
                    authSql, new { requestguid = requestguid.ToString(), tenant_code }
                )).ToList();

                var allPaths = rawRows
                    .SelectMany(r => new[]
                    {
                r.DefaultAuthorizeImage,
                r.CustomerImage,
                r.SignatureImage,
                r.ResultImagePath
                    })
                    .Concat(rawAuth.SelectMany(a => new[]
                    {
                a.EnteredSign,
                a.AuthorizedSign,
                a.AuthorizedSign2
                    }));

                var imageCache = await BuildImageCacheAsync(allPaths);

                var results = rawRows.Select(r => MapRow(r, imageCache)).ToList();

                // ✅ UTC fix: force Kind=Utc so JSON carries the trailing 'Z'
                foreach (var r in results)
                {
                    if (r.CollectedDateTime.HasValue)
                    {
                        var dt = r.CollectedDateTime.Value;
                        if (dt.Kind == DateTimeKind.Local)
                        {
                            // Legacy Npgsql reads timestamptz as Local (IST) → convert back to UTC
                            r.CollectedDateTime = dt.ToUniversalTime();
                        }
                        else if (dt.Kind == DateTimeKind.Unspecified)
                        {
                            // Plain timestamp column (no tz) — assume value is UTC and tag it
                            r.CollectedDateTime = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        }
                        // If Kind == Utc, leave unchanged
                    }
                }

                var authUsers = rawAuth.Select(a => MapAuth(a, imageCache)).ToList();

                int? bhCode = null;
                var firstRow = rawRows.FirstOrDefault();
                if (firstRow != null && !string.IsNullOrWhiteSpace(firstRow.AlteredBHCode) && int.TryParse(firstRow.AlteredBHCode, out int pBh))
                {
                    bhCode = pBh;
                }

                // Check lab_settings for fixed (true) vs dynamic (false) signatures mode and QR/collection toggles
                string lsSql = @"
                    SELECT use_labsetting_signatures, auth1_show, auth1_name, auth1_designation, auth1_signature_path,
                           auth2_show, auth2_name, auth2_designation, auth2_signature_path,
                           auth3_show, auth3_name, auth3_designation, auth3_signature_path,
                           report_qr, sample_collection, ref_by, critical_value_indication,
                           iscan_margin_top, iscan_margin_bottom, iscan_margin_left, iscan_margin_right,
                           ls_signature_on_end, ls_signature_on_each_page, show_name_age_single_row, result_row_align_top
                    FROM lab_settings
                    WHERE (LOWER(tenant_code) = LOWER(@tenant_code) OR tenant_code IS NULL)
                      AND (bh_code = @bhCode OR bh_code = 0 OR bh_code IS NULL)
                      AND (deleted = false OR deleted IS NULL)
                    ORDER BY CASE WHEN bh_code = @bhCode AND bh_code <> 0 THEN 1 WHEN bh_code = 0 THEN 2 ELSE 3 END, lsid DESC
                    LIMIT 1;";
                bool reportQr = true;         // default: show QR
                bool sampleCollection = true;  // default: show collected datetime
                bool showReportImages = true;
                bool? refBy = null;
                bool? criticalValueIndication = true; // default: show arrows
                double? iscanMarginTop = null;
                double? iscanMarginBottom = null;
                double? iscanMarginLeft = null;
                double? iscanMarginRight = null;
                bool? lsSignatureOnEnd = null;
                bool? lsSignatureOnEachPage = null;
                bool? showNameAgeSingleRow = false;
                bool? resultRowAlignTop = false;
                try
                {
                    var lsConfig = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(lsSql, new { tenant_code, bhCode = bhCode ?? 0 });
                    if (lsConfig != null && lsConfig.use_labsetting_signatures == true)
                    {
                        var sig1 = !string.IsNullOrEmpty(lsConfig.auth1_signature_path) ? await FetchImageBytesAsync(lsConfig.auth1_signature_path) ?? GenerateBlankPng() : GenerateBlankPng();
                        var sig2 = !string.IsNullOrEmpty(lsConfig.auth2_signature_path) ? await FetchImageBytesAsync(lsConfig.auth2_signature_path) ?? GenerateBlankPng() : GenerateBlankPng();
                        var sig3 = !string.IsNullOrEmpty(lsConfig.auth3_signature_path) ? await FetchImageBytesAsync(lsConfig.auth3_signature_path) ?? GenerateBlankPng() : GenerateBlankPng();

                        var fixedAuth = new AuthorizedUser
                        {
                            Auth1Show = lsConfig.auth1_show ?? true,
                            EnteredBy = lsConfig.auth1_name ?? "",
                            EnteredByDesignation = lsConfig.auth1_designation ?? "",
                            EnteredSign = sig1,

                            Auth2Show = lsConfig.auth2_show ?? true,
                            AuthorizedBy = lsConfig.auth2_name ?? "",
                            AuthorizedByDesignation = lsConfig.auth2_designation ?? "",
                            AuthorizedSign = sig2,

                            Auth3Show = lsConfig.auth3_show ?? true,
                            AuthorizedBy2 = lsConfig.auth3_name ?? "",
                            AuthorizedByDesignation2 = lsConfig.auth3_designation ?? "",
                            AuthorizedSign2 = sig3,
                        };
                        authUsers = new List<AuthorizedUser> { fixedAuth };
                    }
                    reportQr = lsConfig?.report_qr ?? true;
                    sampleCollection = lsConfig?.sample_collection ?? false;
                    refBy = lsConfig?.ref_by;
                    criticalValueIndication = lsConfig?.critical_value_indication ?? true;
                    showReportImages = lsConfig?.show_report_header_footer_image ?? true;
                    iscanMarginTop = lsConfig?.iscan_margin_top;
                    iscanMarginBottom = lsConfig?.iscan_margin_bottom;
                    iscanMarginLeft = lsConfig?.iscan_margin_left;
                    iscanMarginRight = lsConfig?.iscan_margin_right;
                    lsSignatureOnEnd = lsConfig?.ls_signature_on_end;
                    lsSignatureOnEachPage = lsConfig?.ls_signature_on_each_page;
                    showNameAgeSingleRow = lsConfig?.show_name_age_single_row ?? false;
                    resultRowAlignTop = lsConfig?.result_row_align_top ?? false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error evaluating signature mode: {ex.Message}");
                }

                var (headerImg, footerImg) = await GetHeaderFooterImagesAsync(tenant_code, bhCode);
                if (!showReportImages)
                {
                    headerImg = null;
                    footerImg = null;
                }

                if (authUsers == null || authUsers.Count == 0)
                {
                    authUsers = new List<AuthorizedUser>
                    {
                        new AuthorizedUser
                        {
                            Auth1Show = true,
                            Auth2Show = true,
                            Auth3Show = true,
                            EnteredBy = " ",
                            EnteredByDesignation = " ",
                            EnteredSign = GenerateBlankPng(),
                            AuthorizedBy = " ",
                            AuthorizedByDesignation = " ",
                            AuthorizedSign = GenerateBlankPng(),
                            AuthorizedBy2 = " ",
                            AuthorizedByDesignation2 = " ",
                            AuthorizedSign2 = GenerateBlankPng()
                        }
                    };
                }

                var payload = new RoutineLabReport
                {
                    rrm = results,
                    auth = authUsers,
                    HeaderImage = headerImg,
                    FooterImage = footerImg,
                    report_qr = reportQr,
                    sample_collection = sampleCollection,
                    ref_by = refBy,
                    critical_value_indication = criticalValueIndication,
                    iscan_margin_top = iscanMarginTop,
                    iscan_margin_bottom = iscanMarginBottom,
                    iscan_margin_left = iscanMarginLeft,
                    iscan_margin_right = iscanMarginRight,
                    ls_signature_on_end = lsSignatureOnEnd,
                    ls_signature_on_each_page = lsSignatureOnEachPage,
                    show_name_age_single_row = showNameAgeSingleRow ?? false,
                    result_row_align_top = resultRowAlignTop ?? false
                };

                // ✅ Remove any row whose TestName contains "ECG" (case-insensitive)
                if (payload.rrm != null && tenant_code == "TEN567")
                {
                    payload.rrm = payload.rrm
                        .Where(r => string.IsNullOrEmpty(r.TestName) ||
                                    !r.TestName.Contains("ECG", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"/api/routinereport/iscanlabreport?tenantId={tenant_code}&isLetterhead={(isletterhead ?? false).ToString().ToLower()}",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetLabReportAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<IList<RawReportRow>> SetNormalRanges(IList<RawReportRow> _dyn, IDbConnection db)
        {
            try
            {
                if (_dyn == null || !_dyn.Any())
                {
                    return _dyn ?? new List<RawReportRow>();
                }

                var testResultIds = _dyn
                                  .Select(x => x.TestResultID)
                                  .Where(x => x != Guid.Empty)
                                  .Distinct()
                                  .ToArray();
                IList<RawReportRow> dyn = _dyn;

                // ✅ FIX: split into 3 separate single-statement queries.
                // Npgsql cannot bind one @Ids parameter across a multi-statement
                // (semicolon-separated) batch via QueryMultipleAsync — that's what
                // caused "syntax error at or near $1".
                var lrp = testResultIds.Length > 0
                    ? (await db.QueryAsync<LabResultPropertiesModel>(
                        "SELECT * FROM lab_result_properties WHERE testresultid = ANY(@Ids)",
                        new { Ids = testResultIds }
                    )).ToList()
                    : new List<LabResultPropertiesModel>();

                var lrd = testResultIds.Length > 0
                    ? (await db.QueryAsync<LabResultDetailedNormalValuesModel>(
                        "SELECT * FROM lab_result_detailednormalvalues WHERE testresultid = ANY(@Ids)",
                        new { Ids = testResultIds }
                    )).ToList()
                    : new List<LabResultDetailedNormalValuesModel>();

                var lrt = testResultIds.Length > 0
                    ? (await db.QueryAsync<LabResultTextNormalValuesModel>(
                        "SELECT * FROM lab_result_textnormalvalues WHERE testresultid = ANY(@Ids)",
                        new { Ids = testResultIds }
                    )).ToList()
                    : new LabResultTextNormalValuesModel[0].ToList();

                // ✅ Fallback to master tables (test_result_*) if transactional lab_result_* tables have no entries
                var missingLrpIds = testResultIds.Where(id => !lrp.Any(x => x.testresultid == id)).ToArray();
                if (missingLrpIds.Length > 0)
                {
                    try
                    {
                        var masterLrp = (await db.QueryAsync<LabResultPropertiesModel>(
                            "SELECT * FROM test_result_properties WHERE testresultid = ANY(@Ids)",
                            new { Ids = missingLrpIds }
                        )).ToList();
                        lrp.AddRange(masterLrp);
                    }
                    catch { }
                }

                var missingLrdIds = testResultIds.Where(id => !lrd.Any(x => x.testresultid == id)).ToArray();
                if (missingLrdIds.Length > 0)
                {
                    try
                    {
                        var masterLrd = (await db.QueryAsync<LabResultDetailedNormalValuesModel>(
                            "SELECT * FROM test_result_detailednormalvalues WHERE testresultid = ANY(@Ids)",
                            new { Ids = missingLrdIds }
                        )).ToList();
                        lrd.AddRange(masterLrd);
                    }
                    catch { }
                }

                var missingLrtIds = testResultIds.Where(id => !lrt.Any(x => x.testresultid == id)).ToArray();
                if (missingLrtIds.Length > 0)
                {
                    try
                    {
                        var masterLrt = (await db.QueryAsync<LabResultTextNormalValuesModel>(
                            "SELECT * FROM test_result_textnormalvalues WHERE testresultid = ANY(@Ids)",
                            new { Ids = missingLrtIds }
                        )).ToList();
                        lrt.AddRange(masterLrt);
                    }
                    catch { }
                }

                lrd = lrd.OrderBy(x => x.sno).ToList();

                List<Guid> spcl = lrd
                    .Select(x => x.specialconditioncode)
                    .Where(x => x.HasValue && x.Value != Guid.Empty)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                var spclist = new List<dynamic>();
                if (spcl.Any())
                {
                    var sqlspl = "select * from fixed_values where fxtcode = ANY(@spcl)";
                    spclist = (await db.QueryAsync<dynamic>(sqlspl, new { spcl = spcl.ToArray() })).AsList();
                }
                if (!spclist.Any())
                {
                    try
                    {
                        var sqlspl = "select * from fixed_values";
                        spclist = (await db.QueryAsync<dynamic>(sqlspl)).AsList();
                    }
                    catch { }
                }

                foreach (var it in dyn)
                {
                    var lrp1 = lrp.FirstOrDefault(x => x.testresultid == it.TestResultID && x.usedefault == true)
                            ?? lrp.FirstOrDefault(x => x.testresultid == it.TestResultID);

                    if (lrp1 == null && (it.SimpleNormalValues || it.DetailedNormalValues || lrd.Any(x => x.testresultid == it.TestResultID)))
                    {
                        lrp1 = new LabResultPropertiesModel
                        {
                            testresultid = it.TestResultID,
                            resultvaluetype = !string.IsNullOrWhiteSpace(it.ResultValueType) ? it.ResultValueType : "Number",
                            simplenormalvalues = it.SimpleNormalValues,
                            detailednormalvalues = it.DetailedNormalValues || lrd.Any(x => x.testresultid == it.TestResultID),
                            fromnormalvalue = it.FromNormalValue,
                            tonormalvalue = it.ToNormalValue,
                            rangetype = it.RangeType,
                            showagedbased = it.ShowAgedBased,
                            conclusionforfixedtext = it.ConclusionForFixedText,
                            usedefault = true
                        };
                    }

                        if (lrp1 != null)
                        {
                            if (lrp1.resultvaluetype == "Number" || lrp1.resultvaluetype == "Calculated Value" || lrp1.resultvaluetype == "Numeric")
                            {
                                if (lrp1.simplenormalvalues == true)
                                {
                                    double fr = lrp1.fromnormalvalue ?? 0;
                                    double tr = lrp1.tonormalvalue ?? 0;

                                    if (fr == 0 && tr == 0)
                                    {
                                        it.NormalValues = string.Empty;
                                    }
                                    else
                                    {
                                        it.NormalValues = await this.RefRange(lrp1.rangetype, fr, tr, string.Empty);
                                    }
                                }
                                else if (lrp1.detailednormalvalues == true || lrd.Any(x => x.testresultid == it.TestResultID))
                                {
                                    var testDnvRows = lrd.Where(x => x.testresultid == it.TestResultID).ToList();
                                    if (testDnvRows.Any())
                                    {
                                        if (lrp1.showagedbased == false)
                                        {
                                            it.NormalValues = this.BuildOutput(testDnvRows, spclist, isAgeBased: false);
                                        }
                                        else
                                        {
                                            int ageYear  = int.TryParse(it.AgeYears.ToString(),  out int ay) ? ay : 0;
                                            int ageMonth = int.TryParse(it.AgeMonths.ToString(), out int am) ? am : 0;
                                            int ageDay   = int.TryParse(it.AgeDays.ToString(),   out int ad) ? ad : 0;

                                            if (ageYear == 0 && ageMonth == 0 && ageDay == 0 && !string.IsNullOrWhiteSpace(it.DateofBirth))
                                            {
                                                if (DateTime.TryParse(it.DateofBirth, out DateTime dob))
                                                {
                                                    var now = DateTime.Now;
                                                    ageYear = now.Year - dob.Year;
                                                    if (now < dob.AddYears(ageYear)) ageYear--;
                                                }
                                            }

                                            int    age     = ageYear  > 0 ? ageYear  : (ageMonth > 0 ? ageMonth : ageDay);
                                            string agetype = ageYear  > 0 ? "Yrs"   : (ageMonth > 0 ? "Mths"   : "Dys");

                                            static string NormAgeType(string? t) =>
                                                (t ?? "-").Trim().ToLower() switch
                                                {
                                                    "yrs" or "yr" or "year" or "years"       => "Yrs",
                                                    "mths" or "mth" or "month" or "months"   => "Mths",
                                                    "dys"  or "dy"  or "day"  or "days"      => "Dys",
                                                    _                                         => (t ?? "-").Trim()
                                                };

                                            static string NormSex(string? s)
                                            {
                                                string t = (s ?? "").Trim().ToLower();
                                                if (t == "m" || t == "male" || t == "boy" || t == "man" || t == "1") return "male";
                                                if (t == "f" || t == "female" || t == "girl" || t == "woman" || t == "2") return "female";
                                                if (t == "both" || t == "all" || t == "b" || t == "a" || t == "-" || t == "0" || string.IsNullOrEmpty(t)) return "both";
                                                return t;
                                            }

                                            bool SexMatches(string? rowSex, string? patGender)
                                            {
                                                string rs = NormSex(rowSex);
                                                string pg = NormSex(patGender);
                                                if (rs == "both" || pg == "both") return true;
                                                return rs == pg;
                                            }

                                            var sexRows = testDnvRows
                                                .Where(x => (x.mccode == it.MCCode || x.mccode == 0 || it.MCCode == 0))
                                                .Where(x => SexMatches(x.sex, it.Gender))
                                                .ToList();

                                            var exactGenderRows = sexRows.Where(x => !string.IsNullOrWhiteSpace(x.sex) && x.sex != "-" && x.sex != "0" && !x.sex.Equals("Both", StringComparison.OrdinalIgnoreCase) && !x.sex.Equals("All", StringComparison.OrdinalIgnoreCase)).ToList();
                                            if (exactGenderRows.Any())
                                            {
                                                sexRows = exactGenderRows;
                                            }

                                            bool AgeMatches(LabResultDetailedNormalValuesModel x)
                                            {
                                                double af       = x.agefrom ?? 0;
                                                double at       = x.ageto   ?? 0;
                                                string atType   = NormAgeType(x.agetotype);
                                                string rtype    = (x.agerangetype ?? "-").Trim();

                                                if (af == 0 && at == 0) return false;

                                                if (atType != "-" && !string.Equals(atType, agetype, StringComparison.OrdinalIgnoreCase)) return false;

                                                return rtype.ToLower() switch
                                                {
                                                    "-" or "between" or "range" or "" => age >= af && age <= at,
                                                    "<" or "less than"                 => age <  at,
                                                    "upto" or "<=" or "≤"        => age <= at,
                                                    ">" or "more than"                 => age >  af,
                                                    ">=" or "≥"                  => age >= af,
                                                    _                                  => (af == 0 || age >= af) && (at == 0 || age <= at)
                                                };
                                            }

                                            var matchingAgeRows = sexRows.Where(AgeMatches).ToList();
                                            if (!matchingAgeRows.Any())
                                            {
                                                matchingAgeRows = sexRows.Where(x => (x.agefrom ?? 0) == 0 && (x.ageto ?? 0) == 0).ToList();
                                            }
                                            if (!matchingAgeRows.Any())
                                            {
                                                matchingAgeRows = sexRows;
                                            }
                                            if (!matchingAgeRows.Any())
                                            {
                                                matchingAgeRows = testDnvRows;
                                            }

                                            if (matchingAgeRows.Any())
                                            {
                                                it.NormalValues = this.BuildOutput(matchingAgeRows, spclist, isAgeBased: true);
                                            }
                                            else
                                            {
                                                it.NormalValues = this.BuildOutput(testDnvRows, spclist, isAgeBased: false);
                                            }
                                        }
                                    }
                                }
                            }
                            else if (lrp1.resultvaluetype == "Text" || lrp1.resultvaluetype == "TN" || lrp1.resultvaluetype == "Text & Number")
                            {
                                var tnvMatch = lrt
                                    .Where(x => x.testresultid == it.TestResultID
                                             && (x.mccode == it.MCCode || x.mccode == 0 || it.MCCode == 0)
                                             && (string.IsNullOrWhiteSpace(x.sex) || x.sex == "-"
                                                 || string.Equals(x.sex, it.Gender, StringComparison.OrdinalIgnoreCase)
                                                 || string.Equals(x.sex, "Both", StringComparison.OrdinalIgnoreCase)
                                                 || string.Equals(x.sex, "All", StringComparison.OrdinalIgnoreCase)))
                                    .OrderByDescending(x => string.Equals(x.sex, it.Gender, StringComparison.OrdinalIgnoreCase))
                                    .ThenByDescending(x => x.mccode == it.MCCode)
                                    .Select(x => x.normalvalue)
                                    .FirstOrDefault() ?? "";

                                it.NormalValues = tnvMatch.Replace("\r\n", "<br/>").Replace("\n", "<br/>");
                            }

                            // ✅ ConclusionForFixedText fallback: if set on lab properties, include/append it
                            var fxtRaw = lrp1?.conclusionforfixedtext?.Trim();
                            if (!string.IsNullOrWhiteSpace(fxtRaw) && fxtRaw != "0")
                            {
                                var fxtVal = fxtRaw.Replace("\r\n", "<br/>").Replace("\n", "<br/>");
                                if (string.IsNullOrWhiteSpace(it.NormalValues))
                                {
                                    it.NormalValues = fxtVal;
                                }
                                else if (!it.NormalValues.Contains(fxtRaw))
                                {
                                    it.NormalValues = it.NormalValues + "<br/>" + fxtVal;
                                }
                            }
                        }
                    }
                    return dyn;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SetNormalRanges] ERROR: {ex.Message}\n{ex.StackTrace}");
                return _dyn ?? new List<RawReportRow>();
            }
        }

        public async Task<string> RefRange(string type, Double fr, Double tr, string fixedvalue)
        {
            if (fr == 0 && tr == 0 && string.IsNullOrWhiteSpace(fixedvalue)) return string.Empty;
            string refrange = "";
            string t = (type ?? "").Trim();
            if (string.IsNullOrWhiteSpace(t) || t == "-" || t.Equals("Between", StringComparison.OrdinalIgnoreCase))
            {
                refrange = (fr == tr || tr == 0) ? $"{fr}" : $"{fr} - {tr}";
            }
            else
            {
                double val = fr > 0 ? fr : tr;
                refrange = $"{t} {val}";
            }
            if (!string.IsNullOrEmpty(fixedvalue)) refrange += (fixedvalue.Length > 0 ? (": " + fixedvalue) : "");
            return refrange;
        }

        private static string FormatDetailedRangeValue(string? rangetype, double? rangefromVal, double? rangetoVal)
        {
            double rangefrom = rangefromVal ?? 0;
            double rangeto = rangetoVal ?? 0;
            string op = (rangetype ?? "").Trim();
            if (string.IsNullOrWhiteSpace(op) || op == "-" || op.Equals("Between", StringComparison.OrdinalIgnoreCase) || op.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            {
                if (rangefrom > 0 && rangeto > 0) return rangefrom == rangeto ? $"{rangefrom}" : $"{rangefrom} - {rangeto}";
                if (rangefrom > 0 && rangeto == 0) return $"{rangefrom}";
                if (rangefrom == 0 && rangeto > 0) return $"0 - {rangeto}";
                return "";
            }
            double val = rangefrom > 0 ? rangefrom : rangeto;
            return $"{op} {val}";
        }

        public string BuildOutput(List<LabResultDetailedNormalValuesModel> data, IList<dynamic> fxd, bool isAgeBased = false)
        {
            try
            {
                if (data == null || !data.Any()) return string.Empty;
                var sb = new StringBuilder();

                static string FormatGenderName(string? sex)
                {
                    if (string.IsNullOrWhiteSpace(sex) || sex == "-" || sex == "0") return "";
                    string s = sex.Trim();
                    if (string.Equals(s, "m", StringComparison.OrdinalIgnoreCase)) return "Male";
                    if (string.Equals(s, "f", StringComparison.OrdinalIgnoreCase)) return "Female";
                    if (string.Equals(s, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "both", StringComparison.OrdinalIgnoreCase)) return "Both";
                    if (string.Equals(s, "all", StringComparison.OrdinalIgnoreCase)) return "All";
                    return s;
                }

                static string FormatAgeString(LabResultDetailedNormalValuesModel item)
                {
                    double af = item.agefrom ?? 0;
                    double at = item.ageto ?? 0;
                    string unit = !string.IsNullOrWhiteSpace(item.agetotype) && item.agetotype != "-"
                        ? item.agetotype.Trim()
                        : (!string.IsNullOrWhiteSpace(item.agefromtype) && item.agefromtype != "-" ? item.agefromtype.Trim() : "Yrs");

                    if (af == 0 && at == 0) return "";

                    string op = (item.agerangetype ?? "-").Trim();

                    if (string.IsNullOrWhiteSpace(op) || op == "-" || op.Equals("Between", StringComparison.OrdinalIgnoreCase) || op.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                    {
                        if (af == 0 && at > 0) return $"0 - {at} {unit}".Trim();
                        if (af > 0 && at == 0) return $"{af} {unit}".Trim();
                        if (af == at) return $"{af} {unit}".Trim();
                        return $"{af} - {at} {unit}".Trim();
                    }
                    else
                    {
                        if (af > 0 && at > 0 && af != at) return $"{af} - {at} {unit}".Trim();
                        double ageVal = af > 0 ? af : at;
                        return $"{op} {ageVal} {unit}".Trim();
                    }
                }

                string GetSpecialName(LabResultDetailedNormalValuesModel item)
                {
                    string specialName = "";
                    if (item.specialconditioncode.HasValue && item.specialconditioncode.Value != Guid.Empty && fxd != null)
                    {
                        foreach (var fxdRow in fxd)
                        {
                            var row = (IDictionary<string, object>)fxdRow;
                            if (row.TryGetValue("fxtcode", out var fc) && fc is Guid fGuid && fGuid == item.specialconditioncode.Value)
                            {
                                row.TryGetValue("fixedvalues", out var fv);
                                specialName = fv?.ToString() ?? "";
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(specialName) && !string.IsNullOrWhiteSpace(item.agerangetype))
                    {
                        string ag = item.agerangetype.Trim();
                        string agClean = ag.Replace(" ", "");
                        if (ag != "-" && ag != "<" && ag != ">" && ag != "<=" && ag != ">=" && agClean != "0--" && agClean != "0-0" && !ag.Equals("Between", StringComparison.OrdinalIgnoreCase))
                        {
                            specialName = ag;
                        }
                    }

                    return specialName.Trim();
                }

                if (isAgeBased)
                {
                    foreach (var item in data)
                    {
                        string specialName = GetSpecialName(item);
                        string rangeValue = FormatDetailedRangeValue(item.rangetype, item.rangefrom, item.rangeto);

                        if (!string.IsNullOrWhiteSpace(specialName))
                        {
                            sb.AppendLine($"{rangeValue} : {specialName}");
                        }
                        else
                        {
                            sb.AppendLine(rangeValue);
                        }
                    }

                    return sb.ToString().Trim();
                }

                foreach (var item in data.OrderBy(x => x.sno))
                {
                    string rangeValue = FormatDetailedRangeValue(item.rangetype, item.rangefrom, item.rangeto);
                    string genderStr = FormatGenderName(item.sex);
                    string ageStr = FormatAgeString(item);
                    string specialName = GetSpecialName(item);

                    var prefixParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(genderStr)) prefixParts.Add(genderStr);
                    if (!string.IsNullOrWhiteSpace(ageStr)) prefixParts.Add(ageStr);
                    if (!string.IsNullOrWhiteSpace(specialName) && !prefixParts.Contains(specialName)) prefixParts.Add(specialName);

                    string prefix = string.Join(" ", prefixParts).Trim();

                    if (!string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(rangeValue))
                    {
                        sb.AppendLine($"{prefix} : {rangeValue}");
                    }
                    else if (!string.IsNullOrWhiteSpace(rangeValue))
                    {
                        sb.AppendLine(rangeValue);
                    }
                    else if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        sb.AppendLine(prefix);
                    }
                }

                return sb.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<string?> PayModeStatementPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string paymodeSql = @"
                    SELECT pmcode, name 
                    FROM paymode_master 
                    WHERE tenant_code = @tenant_code 
                      AND COALESCE(deleted, false) = false 
                    ORDER BY orderno, pmcode";

                var paymodes = (await db.QueryAsync<PayModeHeader>(
                    paymodeSql,
                    new { tenant_code })).ToList();

                string sql = @"
                    WITH period_requests AS (
                        SELECT 
                            requestguid,
                            true AS is_created_in_period
                        FROM lab_request_master
                        WHERE tenant_code = @tenant_code
                          AND COALESCE(deleted, false) = false
                          AND requestdatetime >= @fromdate
                          AND requestdatetime < @todate + INTERVAL '1 day'
                        
                        UNION
                        
                        SELECT DISTINCT 
                            bcb.request_guid AS requestguid,
                            false AS is_created_in_period
                        FROM balancecollectionby bcb
                        JOIN lab_request_master lrm ON lrm.requestguid = bcb.request_guid
                        WHERE bcb.tenant_code = @tenant_code
                          AND COALESCE(bcb.deleted, false) = false
                          AND COALESCE(lrm.deleted, false) = false
                          AND COALESCE(bcb.collected_date, bcb.entereddate) >= @fromdate
                          AND COALESCE(bcb.collected_date, bcb.entereddate) < @todate + INTERVAL '1 day'
                          AND NOT (lrm.requestdatetime >= @fromdate AND lrm.requestdatetime < @todate + INTERVAL '1 day')
                    )
                    SELECT
                        lrm.requestsno                              AS sampleid,
                        DATE(CASE WHEN pr.is_created_in_period THEN lrm.requestdatetime ELSE pay.latest_collected_date END)::timestamp AS date,
                        COALESCE(cm.custcode, '')                    AS custcode,
                        lrm.name                                     AS patientname,
                        lrm.mobileno                                 AS mobile,
                        COALESCE(dm.name, '')                        AS referral,
                        
                        CASE WHEN pr.is_created_in_period THEN COALESCE(lrm.requestamount, 0) ELSE 0 END AS billedamount,
                        CASE WHEN pr.is_created_in_period THEN COALESCE(
                            COALESCE(lrm.discountamount, 0)
                            + COALESCE(lrm.ourdiscount, 0)
                            + COALESCE(lrm.specialdiscount, 0),
                        0) ELSE 0 END                               AS discountamount,
                        CASE WHEN pr.is_created_in_period THEN COALESCE(lrm.totalamount, 0) ELSE 0 END AS netamount,
                        
                        COALESCE(pay.period_paid, 0)                 AS paidamount,
                        COALESCE(
                            lrm.totalamount
                            - COALESCE(lrm.paidamount, 0)
                            - 0
                            - 0,
                        0)                                           AS balanceamount,
                        
                        COALESCE(pay.pmc1, 0)::int                   AS pmc1,
                        COALESCE(pay.pmc1_amount, 0)                 AS pmc1_amount,
                        COALESCE(pay.pmc2, 0)::int                   AS pmc2,
                        COALESCE(pay.pmc2_amount, 0)                 AS pmc2_amount,
                        COALESCE(pay.pmc3, 0)::int                   AS pmc3,
                        COALESCE(pay.pmc3_amount, 0)                 AS pmc3_amount
                    FROM period_requests pr
                    JOIN lab_request_master lrm ON lrm.requestguid = pr.requestguid
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode
                    LEFT JOIN LATERAL (
                        SELECT
                            MAX(t.collected_date) AS latest_collected_date,
                            MAX(CASE WHEN t.rn = 1 THEN t.pmcode END) AS pmc1,
                            COALESCE(MAX(CASE WHEN t.rn = 1 THEN t.collected_amount END), 0) AS pmc1_amount,
                            MAX(CASE WHEN t.rn = 2 THEN t.pmcode END) AS pmc2,
                            COALESCE(MAX(CASE WHEN t.rn = 2 THEN t.collected_amount END), 0) AS pmc2_amount,
                            MAX(CASE WHEN t.rn = 3 THEN t.pmcode END) AS pmc3,
                            COALESCE(MAX(CASE WHEN t.rn = 3 THEN t.collected_amount END), 0) AS pmc3_amount,
                            COALESCE(SUM(t.collected_amount), 0) AS period_paid
                        FROM (
                            SELECT 
                                bcb_in.pmcode, 
                                SUM(bcb_in.collectedamount) AS collected_amount,
                                MAX(COALESCE(bcb_in.collected_date, bcb_in.entereddate)) AS collected_date,
                                ROW_NUMBER() OVER (ORDER BY SUM(bcb_in.collectedamount) DESC, bcb_in.pmcode) AS rn
                            FROM balancecollectionby bcb_in
                            WHERE bcb_in.request_guid = pr.requestguid
                              AND bcb_in.tenant_code = @tenant_code
                              AND COALESCE(bcb_in.deleted, false) = false
                              AND COALESCE(bcb_in.collected_date, bcb_in.entereddate) >= @fromdate
                              AND COALESCE(bcb_in.collected_date, bcb_in.entereddate) < @todate + INTERVAL '1 day'
                            GROUP BY bcb_in.pmcode
                        ) t
                    ) pay ON true
                    ORDER BY date ASC";

                var statements = (await db.QueryAsync<PayModeStatementModel>(
                    sql,
                    new { fromdate, todate, tenant_code })).ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code });

                var payload = new PayModeStatementRequest
                {
                    statements = statements,
                    paymodes = paymodes,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/paymodereport/GetPayModeStatement", content);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.PayModeStatementPDF: {ex.Message}");
                throw;
            }
        }

        private class RawSummaryRow
        {
            public DateTime date { get; set; }
            public int billed { get; set; }
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

        public async Task<string?> PayModeSummaryPDF(DateTime fromdate, DateTime todate, string tenant_code, string periodtype)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string paymodeSql = @"
                    SELECT pmcode, name 
                    FROM paymode_master 
                    WHERE tenant_code = @tenant_code 
                      AND COALESCE(deleted, false) = false 
                    ORDER BY orderno, pmcode";

                var paymodes = (await db.QueryAsync<PayModeHeader>(
                    paymodeSql,
                    new { tenant_code })).ToList();

                string sql = @"
                    WITH period_requests AS (
                        SELECT 
                            requestguid,
                            true AS is_created_in_period
                        FROM lab_request_master
                        WHERE tenant_code = @tenant_code
                          AND COALESCE(deleted, false) = false
                          AND requestdatetime >= @fromdate
                          AND requestdatetime < @todate + INTERVAL '1 day'
                        
                        UNION
                        
                        SELECT DISTINCT 
                            bcb.request_guid AS requestguid,
                            false AS is_created_in_period
                        FROM balancecollectionby bcb
                        JOIN lab_request_master lrm ON lrm.requestguid = bcb.request_guid
                        WHERE bcb.tenant_code = @tenant_code
                          AND COALESCE(bcb.deleted, false) = false
                          AND COALESCE(lrm.deleted, false) = false
                          AND COALESCE(bcb.collected_date, bcb.entereddate) >= @fromdate
                          AND COALESCE(bcb.collected_date, bcb.entereddate) < @todate + INTERVAL '1 day'
                          AND NOT (lrm.requestdatetime >= @fromdate AND lrm.requestdatetime < @todate + INTERVAL '1 day')
                    )
                    SELECT
                        DATE(CASE WHEN pr.is_created_in_period THEN lrm.requestdatetime ELSE pay.latest_collected_date END)::timestamp AS date,
                        CASE WHEN pr.is_created_in_period THEN 1 ELSE 0 END AS billed,
                        CASE WHEN pr.is_created_in_period THEN COALESCE(lrm.requestamount, 0) ELSE 0 END AS billedamount,
                        CASE WHEN pr.is_created_in_period THEN COALESCE(
                            COALESCE(lrm.discountamount, 0)
                            + COALESCE(lrm.ourdiscount, 0)
                            + COALESCE(lrm.specialdiscount, 0),
                        0) ELSE 0 END                               AS discountamount,
                        CASE WHEN pr.is_created_in_period THEN COALESCE(lrm.totalamount, 0) ELSE 0 END AS netamount,
                        
                        COALESCE(pay.period_paid, 0)                 AS paidamount,
                        COALESCE(
                            lrm.totalamount
                            - COALESCE(lrm.paidamount, 0)
                            - 0
                            - 0,
                        0)                                           AS balanceamount,
                        
                        COALESCE(pay.pmc1, 0)::int                   AS pmc1,
                        COALESCE(pay.pmc1_amount, 0)                 AS pmc1_amount,
                        COALESCE(pay.pmc2, 0)::int                   AS pmc2,
                        COALESCE(pay.pmc2_amount, 0)                 AS pmc2_amount,
                        COALESCE(pay.pmc3, 0)::int                   AS pmc3,
                        COALESCE(pay.pmc3_amount, 0)                 AS pmc3_amount
                    FROM period_requests pr
                    JOIN lab_request_master lrm ON lrm.requestguid = pr.requestguid
                    LEFT JOIN LATERAL (
                        SELECT
                            MAX(t.collected_date) AS latest_collected_date,
                            MAX(CASE WHEN t.rn = 1 THEN t.pmcode END) AS pmc1,
                            COALESCE(MAX(CASE WHEN t.rn = 1 THEN t.collected_amount END), 0) AS pmc1_amount,
                            MAX(CASE WHEN t.rn = 2 THEN t.pmcode END) AS pmc2,
                            COALESCE(MAX(CASE WHEN t.rn = 2 THEN t.collected_amount END), 0) AS pmc2_amount,
                            MAX(CASE WHEN t.rn = 3 THEN t.pmcode END) AS pmc3,
                            COALESCE(MAX(CASE WHEN t.rn = 3 THEN t.collected_amount END), 0) AS pmc3_amount,
                            COALESCE(SUM(t.collected_amount), 0) AS period_paid
                        FROM (
                            SELECT 
                                bcb_in.pmcode, 
                                SUM(bcb_in.collectedamount) AS collected_amount,
                                MAX(COALESCE(bcb_in.collected_date, bcb_in.entereddate)) AS collected_date,
                                ROW_NUMBER() OVER (ORDER BY SUM(bcb_in.collectedamount) DESC, bcb_in.pmcode) AS rn
                            FROM balancecollectionby bcb_in
                            WHERE bcb_in.request_guid = pr.requestguid
                              AND bcb_in.tenant_code = @tenant_code
                              AND COALESCE(bcb_in.deleted, false) = false
                              AND COALESCE(bcb_in.collected_date, bcb_in.entereddate) >= @fromdate
                              AND COALESCE(bcb_in.collected_date, bcb_in.entereddate) < @todate + INTERVAL '1 day'
                            GROUP BY bcb_in.pmcode
                        ) t
                    ) pay ON true";

                var rawRows = (await db.QueryAsync<RawSummaryRow>(
                    sql,
                    new { fromdate, todate, tenant_code })).ToList();

                Func<DateTime, DateTime> groupKeySelector = d => d.Date;
                if (string.Equals(periodtype, "month-wise", StringComparison.OrdinalIgnoreCase))
                {
                    groupKeySelector = d => new DateTime(d.Year, d.Month, 1);
                }
                else if (string.Equals(periodtype, "year-wise", StringComparison.OrdinalIgnoreCase))
                {
                    groupKeySelector = d => new DateTime(d.Year, 1, 1);
                }

                var grouped = rawRows
                    .GroupBy(r => groupKeySelector(r.date))
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        var summaryModel = new PayModeSummaryModel
                        {
                            date = g.Key,
                            billed = g.Sum(x => x.billed),
                            billedamount = g.Sum(x => x.billedamount),
                            discountamount = g.Sum(x => x.discountamount),
                            netamount = g.Sum(x => x.netamount),
                            paidamount = g.Sum(x => x.paidamount),
                            balanceamount = g.Sum(x => x.balanceamount),
                            paymode_amounts = new List<PayModeAmountModel>()
                        };

                        foreach (var pm in paymodes)
                        {
                            decimal totalPm = g.Sum(x =>
                                (x.pmc1 == pm.pmcode ? x.pmc1_amount : 0) +
                                (x.pmc2 == pm.pmcode ? x.pmc2_amount : 0) +
                                (x.pmc3 == pm.pmcode ? x.pmc3_amount : 0)
                            );
                            summaryModel.paymode_amounts.Add(new PayModeAmountModel
                            {
                                pmcode = pm.pmcode,
                                amount = totalPm
                            });
                        }

                        return summaryModel;
                    })
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code });

                var payload = new PayModeSummaryRequest
                {
                    summary = grouped,
                    paymodes = paymodes,
                    fromdate = fromdate,
                    todate = todate,
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email,
                    periodtype = periodtype
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/paymodereport/GetPayModeSummary", content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.PayModeSummaryPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string> OPCasesheetPDF(Guid sheet_id, string tenant_code, bool? isletterhead = false)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string mainSql = @"
                    SELECT 
                        cs.sheet_id, cs.op_id, cs.custid, cs.dcode, cs.visit_date::timestamp AS CaseSheetVisitDate,
                        cs.chief_complaint AS ChiefComplaint, cs.symptoms AS Symptoms, cs.examination AS Examination, 
                        cs.advise AS Advise, cs.notes AS Notes, cs.followup_date::timestamp AS FollowupDate, cs.followup_notes AS FollowupNotes,
                        c.name AS PatientName, c.gender, c.mobile AS MobileNo,
                        CONCAT_WS(', ', NULLIF(c.street, ''), NULLIF(c.city, ''), NULLIF(c.state, ''), NULLIF(c.zipcode, '')) AS Address,
                        c.ageyears, c.agemonths, c.agedays,
                        COALESCE(NULLIF(c.custcode::text, ''), NULLIF(c.bhcustcode::text, ''), NULLIF(c.customermanualcode::text, ''), c.custid::text, '') AS PatientId,
                        opr.op_no AS VisitNo,
                        opr.visit_date::timestamp AS VisitDate,
                        dm.doctorfullname AS DoctorName,
                        bm.name AS BranchName,
                        t.legal_name AS CompanyName,
                        CONCAT_WS(', ', NULLIF(t.address_line1, ''), NULLIF(t.address_line2, ''), NULLIF(t.city, ''), NULLIF(t.state, ''), NULLIF(t.pincode, '')) AS CompanyAddress,
                        t.contact_number AS CompanyContactNo,
                        t.contact_email AS CompanyEmail
                    FROM op_case_sheet cs
                    LEFT JOIN customerdb.customer_master c ON c.custid = cs.custid
                    LEFT JOIN op_registration opr ON opr.op_id = cs.op_id
                    LEFT JOIN doctor_master dm ON dm.dcode = cs.dcode
                    LEFT JOIN mastertenant.tenants t ON t.tenant_code = cs.tenant_code
                    LEFT JOIN mastertenant.branch_master bm ON bm.bh_code = c.bhcode
                    WHERE cs.sheet_id = CAST(@sheet_id AS uuid)
                      AND cs.tenant_code = @tenant_code
                      AND cs.isdeleted = false
                    LIMIT 1";

                var casesheet = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                    mainSql, new { sheet_id = sheet_id.ToString(), tenant_code });

                if (casesheet == null)
                    throw new Exception("Casesheet not found");

                if (string.IsNullOrWhiteSpace(casesheet.PatientId) && casesheet.custid > 0)
                {
                    casesheet.PatientId = casesheet.custid.ToString();
                }

                casesheet.ReportHeader = "OUT PATIENT CASE SHEET";
                casesheet.Age = $"{casesheet.ageyears} Y / {casesheet.agemonths} M / {casesheet.agedays} D";

                // Child Lists
                string symptomsSql = @"
                    SELECT sno, symptom_text AS SymptomText, duration, severity, notes
                    FROM op_case_sheet_symptoms
                    WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code
                    ORDER BY sno";
                casesheet.SymptomsList = (await db.QueryAsync<CasesheetSymptomItemDto>(
                    symptomsSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                string diagSql = @"
                    SELECT sno, icd_code AS IcdCode, icd_description AS IcdDescription, diagnosis_text AS DiagnosisText,
                           diagnosis_type AS DiagnosisType, condition_type AS ConditionType, severity, status
                    FROM op_case_sheet_diagnosis
                    WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code
                    ORDER BY sno";
                casesheet.DiagnosisList = (await db.QueryAsync<CasesheetDiagnosisItemDto>(
                    diagSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                string presSql = @"
                    SELECT sno, drug_name AS DrugName, morning, afternoon, evening, night,
                           before_food AS BeforeFood, after_food AS AfterFood, days, qty, route, notes
                    FROM op_prescription_detail
                    WHERE pr_id IN (
                        SELECT pr_id FROM op_prescription_master 
                        WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false
                    )
                    AND isdeleted = false
                    ORDER BY sno";
                casesheet.PrescriptionList = (await db.QueryAsync<CasesheetPrescriptionItemDto>(
                    presSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                string invSql = @"
                    SELECT sno, test_name AS TestName, test_category AS TestCategory, quantity
                    FROM op_investigation_detail
                    WHERE inv_id IN (
                        SELECT inv_id FROM op_investigation_master 
                        WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false
                    )
                    AND isdeleted = false
                    ORDER BY sno";
                casesheet.InvestigationList = (await db.QueryAsync<CasesheetInvestigationItemDto>(
                    invSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                // Fetch patient vitals
                CasesheetReportPdfModel? vitals = null;
                if (casesheet.op_id.HasValue && casesheet.op_id != Guid.Empty)
                {
                    string vitalsSql = @"
                        SELECT 
                            height_cm, weight_kg, bmi, temperature_f, pulse_rate, respiratory_rate, bp_systolic, bp_diastolic, spo2,
                            sugar_level, pain_scale, waist_cm, hip_cm, pedal_oedema, jvp, cvs, rs, cns, abdomen,
                            cardiac_monitor, cd_echo, blood_chemistry, allergy_notes, hba1c, ecg_notes, head_circumference_cm
                        FROM patient_vitals
                        WHERE op_id = CAST(@op_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false
                        LIMIT 1";
                    vitals = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                        vitalsSql, new { op_id = casesheet.op_id, tenant_code });
                }

                // Fallback to fetch by custid if not found by op_id
                if (vitals == null && casesheet.custid > 0)
                {
                    string fallbackVitalsSql = @"
                        SELECT 
                            height_cm, weight_kg, bmi, temperature_f, pulse_rate, respiratory_rate, bp_systolic, bp_diastolic, spo2,
                            sugar_level, pain_scale, waist_cm, hip_cm, pedal_oedema, jvp, cvs, rs, cns, abdomen,
                            cardiac_monitor, cd_echo, blood_chemistry, allergy_notes, hba1c, ecg_notes, head_circumference_cm
                        FROM patient_vitals
                        WHERE custid = @custid AND tenant_code = @tenant_code AND isdeleted = false
                        ORDER BY created_at DESC
                        LIMIT 1";
                    vitals = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                        fallbackVitalsSql, new { custid = casesheet.custid, tenant_code });
                }

                if (vitals != null)
                {
                    casesheet.height_cm = vitals.height_cm;
                    casesheet.weight_kg = vitals.weight_kg;
                    casesheet.bmi = vitals.bmi;
                    casesheet.temperature_f = vitals.temperature_f;
                    casesheet.pulse_rate = vitals.pulse_rate;
                    casesheet.respiratory_rate = vitals.respiratory_rate;
                    casesheet.bp_systolic = vitals.bp_systolic;
                    casesheet.bp_diastolic = vitals.bp_diastolic;
                    casesheet.spo2 = vitals.spo2;
                    casesheet.sugar_level = vitals.sugar_level;
                    casesheet.pain_scale = vitals.pain_scale;
                    casesheet.waist_cm = vitals.waist_cm;
                    casesheet.hip_cm = vitals.hip_cm;
                    casesheet.pedal_oedema = vitals.pedal_oedema;
                    casesheet.jvp = vitals.jvp;
                    casesheet.cvs = vitals.cvs;
                    casesheet.rs = vitals.rs;
                    casesheet.cns = vitals.cns;
                    casesheet.abdomen = vitals.abdomen;
                    casesheet.cardiac_monitor = vitals.cardiac_monitor;
                    casesheet.cd_echo = vitals.cd_echo;
                    casesheet.blood_chemistry = vitals.blood_chemistry;
                    casesheet.allergy_notes = vitals.allergy_notes;
                    casesheet.hba1c = vitals.hba1c;
                    casesheet.ecg_notes = vitals.ecg_notes;
                    casesheet.head_circumference_cm = vitals.head_circumference_cm;
                }

                // Fetch Prescription Remarks
                var presMst = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                    @"SELECT topremarks, bottonremarks FROM op_prescription_master 
                      WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false 
                      LIMIT 1", new { sheet_id = sheet_id.ToString(), tenant_code });
                if (presMst != null)
                {
                    casesheet.topremarks = presMst.topremarks;
                    casesheet.bottonremarks = presMst.bottonremarks;
                }

                // Fetch Investigation Remarks/Urgency
                var invMst = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                    @"SELECT notes AS InvestigationNotes, is_urgent AS IsInvestigationUrgent FROM op_investigation_master 
                      WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false 
                      LIMIT 1", new { sheet_id = sheet_id.ToString(), tenant_code });
                if (invMst != null)
                {
                    casesheet.InvestigationNotes = invMst.InvestigationNotes;
                    casesheet.IsInvestigationUrgent = invMst.IsInvestigationUrgent;
                }

                byte[] logoImage = null;

                var lsConfig = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                    @"SELECT * FROM lab_settings WHERE tenant_code = @tenant_code AND COALESCE(deleted, false) = false ORDER BY bh_code LIMIT 1",
                    new { tenant_code });

                if (lsConfig == null || (string.IsNullOrWhiteSpace(lsConfig.header_path) && string.IsNullOrWhiteSpace(lsConfig.header_image_path) && string.IsNullOrWhiteSpace(lsConfig.footer_path) && string.IsNullOrWhiteSpace(lsConfig.footer_image_path)))
                {
                    var fallbackLs = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                        @"SELECT * FROM lab_settings 
                          WHERE tenant_code = @tenant_code AND COALESCE(deleted, false) = false 
                            AND (header_path IS NOT NULL OR header_image_path IS NOT NULL OR footer_path IS NOT NULL OR footer_image_path IS NOT NULL) 
                          LIMIT 1",
                        new { tenant_code });
                    if (fallbackLs != null) lsConfig = fallbackLs;
                }

                string? hKey = !string.IsNullOrWhiteSpace(lsConfig?.header_path) ? lsConfig.header_path : lsConfig?.header_image_path;
                string? fKey = !string.IsNullOrWhiteSpace(lsConfig?.footer_path) ? lsConfig.footer_path : lsConfig?.footer_image_path;

                bool showHeaderFooter = lsConfig?.show_op_casesheet_header_footer_image ?? lsConfig?.show_casesheet_header_footer_image ?? lsConfig?.show_report_header_footer_image ?? true;
                if (!string.IsNullOrWhiteSpace(hKey) || !string.IsNullOrWhiteSpace(fKey))
                {
                    if (!lsConfig.show_op_casesheet_header_footer_image.HasValue) showHeaderFooter = true;
                }

                byte[]? headerImage = null;
                byte[]? footerImage = null;

                if (showHeaderFooter && lsConfig != null)
                {
                    if (!string.IsNullOrWhiteSpace(hKey))
                    {
                        try { var hRes = await _s3Service.DownloadAsync(hKey); if (hRes.HasValue) headerImage = hRes.Value.Data; } catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(fKey))
                    {
                        try { var fRes = await _s3Service.DownloadAsync(fKey); if (fRes.HasValue) footerImage = fRes.Value.Data; } catch { }
                    }
                }

                var payload = new CasesheetReportRequest
                {
                    CasesheetData = casesheet,
                    LogoImage = logoImage,
                    IsLetterhead = isletterhead ?? false,
                    HeaderImage = headerImage,
                    FooterImage = footerImage,
                    show_header_footer_image = showHeaderFooter,
                    TenantId = tenant_code
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/casesheet/getopcasesheet", content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.OPCasesheetPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string> IPCasesheetPDF(Guid sheet_id, string tenant_code, bool? isletterhead = false)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string mainSql = @"
                    SELECT 
                        cs.sheet_id, cs.op_id, cs.custid, cs.dcode, cs.visit_date::timestamp AS CaseSheetVisitDate,
                        cs.chief_complaint AS ChiefComplaint, cs.symptoms AS Symptoms, cs.examination AS Examination, 
                        cs.advise AS Advise, cs.notes AS Notes, cs.followup_date::timestamp AS FollowupDate, cs.followup_notes AS FollowupNotes,
                        c.name AS PatientName, c.gender, c.mobile AS MobileNo,
                        CONCAT_WS(', ', NULLIF(c.street, ''), NULLIF(c.city, ''), NULLIF(c.state, ''), NULLIF(c.zipcode, '')) AS Address,
                        c.ageyears, c.agemonths, c.agedays,
                        COALESCE(NULLIF(c.custcode::text, ''), NULLIF(c.bhcustcode::text, ''), NULLIF(c.customermanualcode::text, ''), c.custid::text, '') AS PatientId,
                        COALESCE(ipm.patcode, c.custcode::text, c.custid::text) AS VisitNo,
                        COALESCE(ipm.regdate, cs.visit_date)::timestamp AS VisitDate,
                        ipm.bedcode::text AS BedNo,
                        dm.doctorfullname AS DoctorName,
                        bm.name AS BranchName,
                        t.legal_name AS CompanyName,
                        CONCAT_WS(', ', NULLIF(t.address_line1, ''), NULLIF(t.address_line2, ''), NULLIF(t.city, ''), NULLIF(t.state, ''), NULLIF(t.pincode, '')) AS CompanyAddress,
                        t.contact_number AS CompanyContactNo,
                        t.contact_email AS CompanyEmail
                    FROM op_case_sheet cs
                    LEFT JOIN customerdb.customer_master c ON c.custid = cs.custid
                    LEFT JOIN inpatient_master ipm ON ipm.patcode = c.custcode
                    LEFT JOIN doctor_master dm ON dm.dcode = cs.dcode
                    LEFT JOIN mastertenant.tenants t ON t.tenant_code = cs.tenant_code
                    LEFT JOIN mastertenant.branch_master bm ON bm.bh_code = c.bhcode
                    WHERE cs.sheet_id = CAST(@sheet_id AS uuid)
                      AND cs.tenant_code = @tenant_code
                      AND cs.isdeleted = false
                    LIMIT 1";

                var casesheet = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                    mainSql, new { sheet_id = sheet_id.ToString(), tenant_code });

                if (casesheet == null)
                    throw new Exception("Casesheet not found");

                if (string.IsNullOrWhiteSpace(casesheet.PatientId) && casesheet.custid > 0)
                {
                    casesheet.PatientId = casesheet.custid.ToString();
                }

                casesheet.ReportHeader = "IN PATIENT CASE SHEET";
                casesheet.Age = $"{casesheet.ageyears} Y / {casesheet.agemonths} M / {casesheet.agedays} D";

                // Child Lists
                string symptomsSql = @"
                    SELECT sno, symptom_text AS SymptomText, duration, severity, notes
                    FROM op_case_sheet_symptoms
                    WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code
                    ORDER BY sno";
                casesheet.SymptomsList = (await db.QueryAsync<CasesheetSymptomItemDto>(
                    symptomsSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                string diagSql = @"
                    SELECT sno, icd_code AS IcdCode, icd_description AS IcdDescription, diagnosis_text AS DiagnosisText,
                           diagnosis_type AS DiagnosisType, condition_type AS ConditionType, severity, status
                    FROM op_case_sheet_diagnosis
                    WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code
                    ORDER BY sno";
                casesheet.DiagnosisList = (await db.QueryAsync<CasesheetDiagnosisItemDto>(
                    diagSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                string presSql = @"
                    SELECT sno, drug_name AS DrugName, morning, afternoon, evening, night,
                           before_food AS BeforeFood, after_food AS AfterFood, days, qty, route, notes
                    FROM op_prescription_detail
                    WHERE pr_id IN (
                        SELECT pr_id FROM op_prescription_master 
                        WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false
                    )
                    AND isdeleted = false
                    ORDER BY sno";
                casesheet.PrescriptionList = (await db.QueryAsync<CasesheetPrescriptionItemDto>(
                    presSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                string invSql = @"
                    SELECT sno, test_name AS TestName, test_category AS TestCategory, quantity
                    FROM op_investigation_detail
                    WHERE inv_id IN (
                        SELECT inv_id FROM op_investigation_master 
                        WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false
                    )
                    AND isdeleted = false
                    ORDER BY sno";
                casesheet.InvestigationList = (await db.QueryAsync<CasesheetInvestigationItemDto>(
                    invSql, new { sheet_id = sheet_id.ToString(), tenant_code })).ToList();

                // Fetch patient vitals
                CasesheetReportPdfModel? vitals = null;
                if (casesheet.op_id.HasValue && casesheet.op_id != Guid.Empty)
                {
                    string vitalsSql = @"
                        SELECT 
                            height_cm, weight_kg, bmi, temperature_f, pulse_rate, respiratory_rate, bp_systolic, bp_diastolic, spo2,
                            sugar_level, pain_scale, waist_cm, hip_cm, pedal_oedema, jvp, cvs, rs, cns, abdomen,
                            cardiac_monitor, cd_echo, blood_chemistry, allergy_notes, hba1c, ecg_notes, head_circumference_cm
                        FROM patient_vitals
                        WHERE op_id = CAST(@op_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false
                        LIMIT 1";
                    vitals = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                        vitalsSql, new { op_id = casesheet.op_id, tenant_code });
                }

                // Fallback to fetch by custid if not found by op_id
                if (vitals == null && casesheet.custid > 0)
                {
                    string fallbackVitalsSql = @"
                        SELECT 
                            height_cm, weight_kg, bmi, temperature_f, pulse_rate, respiratory_rate, bp_systolic, bp_diastolic, spo2,
                            sugar_level, pain_scale, waist_cm, hip_cm, pedal_oedema, jvp, cvs, rs, cns, abdomen,
                            cardiac_monitor, cd_echo, blood_chemistry, allergy_notes, hba1c, ecg_notes, head_circumference_cm
                        FROM patient_vitals
                        WHERE custid = @custid AND tenant_code = @tenant_code AND isdeleted = false
                        ORDER BY created_at DESC
                        LIMIT 1";
                    vitals = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                        fallbackVitalsSql, new { custid = casesheet.custid, tenant_code });
                }

                if (vitals != null)
                {
                    casesheet.height_cm = vitals.height_cm;
                    casesheet.weight_kg = vitals.weight_kg;
                    casesheet.bmi = vitals.bmi;
                    casesheet.temperature_f = vitals.temperature_f;
                    casesheet.pulse_rate = vitals.pulse_rate;
                    casesheet.respiratory_rate = vitals.respiratory_rate;
                    casesheet.bp_systolic = vitals.bp_systolic;
                    casesheet.bp_diastolic = vitals.bp_diastolic;
                    casesheet.spo2 = vitals.spo2;
                    casesheet.sugar_level = vitals.sugar_level;
                    casesheet.pain_scale = vitals.pain_scale;
                    casesheet.waist_cm = vitals.waist_cm;
                    casesheet.hip_cm = vitals.hip_cm;
                    casesheet.pedal_oedema = vitals.pedal_oedema;
                    casesheet.jvp = vitals.jvp;
                    casesheet.cvs = vitals.cvs;
                    casesheet.rs = vitals.rs;
                    casesheet.cns = vitals.cns;
                    casesheet.abdomen = vitals.abdomen;
                    casesheet.cardiac_monitor = vitals.cardiac_monitor;
                    casesheet.cd_echo = vitals.cd_echo;
                    casesheet.blood_chemistry = vitals.blood_chemistry;
                    casesheet.allergy_notes = vitals.allergy_notes;
                    casesheet.hba1c = vitals.hba1c;
                    casesheet.ecg_notes = vitals.ecg_notes;
                    casesheet.head_circumference_cm = vitals.head_circumference_cm;
                }

                // Fetch Prescription Remarks
                var presMst = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                    @"SELECT topremarks, bottonremarks FROM op_prescription_master 
                      WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false 
                      LIMIT 1", new { sheet_id = sheet_id.ToString(), tenant_code });
                if (presMst != null)
                {
                    casesheet.topremarks = presMst.topremarks;
                    casesheet.bottonremarks = presMst.bottonremarks;
                }

                // Fetch Investigation Remarks/Urgency
                var invMst = await db.QueryFirstOrDefaultAsync<CasesheetReportPdfModel>(
                    @"SELECT notes AS InvestigationNotes, is_urgent AS IsInvestigationUrgent FROM op_investigation_master 
                      WHERE sheet_id = CAST(@sheet_id AS uuid) AND tenant_code = @tenant_code AND isdeleted = false 
                      LIMIT 1", new { sheet_id = sheet_id.ToString(), tenant_code });
                if (invMst != null)
                {
                    casesheet.InvestigationNotes = invMst.InvestigationNotes;
                    casesheet.IsInvestigationUrgent = invMst.IsInvestigationUrgent;
                }

                byte[] logoImage = null;

                var lsConfig = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                    @"SELECT * FROM lab_settings WHERE tenant_code = @tenant_code AND COALESCE(deleted, false) = false ORDER BY bh_code LIMIT 1",
                    new { tenant_code });

                if (lsConfig == null || (string.IsNullOrWhiteSpace(lsConfig.header_path) && string.IsNullOrWhiteSpace(lsConfig.header_image_path) && string.IsNullOrWhiteSpace(lsConfig.footer_path) && string.IsNullOrWhiteSpace(lsConfig.footer_image_path)))
                {
                    var fallbackLs = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                        @"SELECT * FROM lab_settings 
                          WHERE tenant_code = @tenant_code AND COALESCE(deleted, false) = false 
                            AND (header_path IS NOT NULL OR header_image_path IS NOT NULL OR footer_path IS NOT NULL OR footer_image_path IS NOT NULL) 
                          LIMIT 1",
                        new { tenant_code });
                    if (fallbackLs != null) lsConfig = fallbackLs;
                }

                string? hKey = !string.IsNullOrWhiteSpace(lsConfig?.header_path) ? lsConfig.header_path : lsConfig?.header_image_path;
                string? fKey = !string.IsNullOrWhiteSpace(lsConfig?.footer_path) ? lsConfig.footer_path : lsConfig?.footer_image_path;

                bool showHeaderFooter = lsConfig?.show_ip_casesheet_header_footer_image ?? lsConfig?.show_casesheet_header_footer_image ?? lsConfig?.show_report_header_footer_image ?? true;
                if (!string.IsNullOrWhiteSpace(hKey) || !string.IsNullOrWhiteSpace(fKey))
                {
                    if (!lsConfig.show_ip_casesheet_header_footer_image.HasValue) showHeaderFooter = true;
                }

                byte[]? headerImage = null;
                byte[]? footerImage = null;

                if (showHeaderFooter && lsConfig != null)
                {
                    if (!string.IsNullOrWhiteSpace(hKey))
                    {
                        try { var hRes = await _s3Service.DownloadAsync(hKey); if (hRes.HasValue) headerImage = hRes.Value.Data; } catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(fKey))
                    {
                        try { var fRes = await _s3Service.DownloadAsync(fKey); if (fRes.HasValue) footerImage = fRes.Value.Data; } catch { }
                    }
                }

                var payload = new CasesheetReportRequest
                {
                    CasesheetData = casesheet,
                    LogoImage = logoImage,
                    IsLetterhead = isletterhead ?? false,
                    HeaderImage = headerImage,
                    FooterImage = footerImage,
                    show_header_footer_image = showHeaderFooter,
                    TenantId = tenant_code
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/casesheet/getipcasesheet", content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.IPCasesheetPDF: {ex.Message}");
                throw;
            }
        }

        // ========================================================================
        // CONSOLIDATED BILL IMPLEMENTATION
        // ========================================================================

        /// <summary>
        /// Fetches consolidated bill data from database for a given RequestGuid
        /// </summary>
        public async Task<ConsolidatedBillData?> GetConsolidatedBillDataAsync(
            string requestGuid, 
            bool includeMedicines, 
            string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                // Get request/patient header information
                Guid parsedGuid = Guid.Empty;
                Guid.TryParse(requestGuid, out parsedGuid);

                string headerSql = @"
                        SELECT 
                            COALESCE(NULLIF(lrm.requestsno::text, ''), NULLIF(lrm.requestguid::text, ''), 'BILL-001') AS BillNo,
                            lrm.requestguid::text AS RequestGuid,
                            COALESCE(lrm.requestdatetime, NOW()) AS BillDate,
                            COALESCE(NULLIF(lrm.name, ''), NULLIF(cm.name, ''), 'Patient') AS PatientName,
                            COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(lrm.custid::text, ''), '') AS PatientId,
                            COALESCE(
                                NULLIF(CONCAT(NULLIF(lrm.ageyears, '0'), ' Y ', NULLIF(lrm.agemonths, '0'), ' M'), ' Y  M'),
                                NULLIF(CONCAT(NULLIF(cm.ageyears, 0), ' Y ', NULLIF(cm.agemonths, 0), ' M'), ' Y  M'),
                                ''
                            ) AS Age,
                            COALESCE(NULLIF(lrm.gender, ''), NULLIF(cm.gender, ''), '') AS Gender,
                            COALESCE(NULLIF(lrm.address, ''), NULLIF(CONCAT_WS(', ', NULLIF(cm.street, ''), NULLIF(cm.area, ''), NULLIF(cm.city, ''), NULLIF(cm.state, '')), ''), '') AS PatientAddress,
                            COALESCE(NULLIF(lrm.mobileno, ''), NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS CusMobileNo,
                            COALESCE(NULLIF(cm.careof, ''), '') AS CareOf,
                            COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS DoctorName,
                            COALESCE(NULLIF(ip.ip_no, ''), NULLIF(op.op_no, ''), NULLIF(lrm.ip_id::text, ''), NULLIF(lrm.opvisitid, ''), '') AS AdmissionNo,
                            ip.admitdate AS AdmissionDate,
                            ip.dischargedate AS DischargeDate,
                            COALESCE(bm.bedname, ip.bedcode::text, '-') AS BedNo,
                            COALESCE(lrm.requestamount, 0) AS TotalAmount,
                            COALESCE(
                                COALESCE(lrm.discountamount, 0) +
                                COALESCE(lrm.ourdiscount, 0) +
                                COALESCE(lrm.specialdiscount, 0), 0
                            ) AS DiscountAmount,
                            COALESCE(lrm.totalamount, 0) AS NetAmount,
                            COALESCE(lrm.paidamount, 0) AS ReceivedAmount,
                            COALESCE(
                                lrm.totalamount - COALESCE(lrm.paidamount, 0), 0
                            ) AS BalanceAmount,
                            COALESCE(um.name, '') AS CreatedBy,
                            COALESCE(lrm.entereddate, lrm.requestdatetime, NOW()) AS CreatedTime,
                            lrm.tenant_code AS TenantCode,
                            COALESCE(lrm.alteredbhcode::int, lrm.enteredbhcode, 0) AS BhCode,
                            lrm.custid AS CustId,
                            lrm.ip_id::text AS IpId,
                            COALESCE(NULLIF(lrm.opvisitid, ''), NULLIF(lrm.sheet_id, '')) AS OutpatientId
                        FROM lab_request_master lrm
                        LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
                        LEFT JOIN ip_registration ip ON ip.ip_id = lrm.ip_id
                        LEFT JOIN op_registration op ON (op.op_id::text = lrm.opvisitid OR op.op_id::text = lrm.sheet_id)
                        LEFT JOIN doctor_master dm ON (dm.dcode = lrm.dcode OR dm.dcode = ip.dcode OR dm.dcode = op.dcode)
                        LEFT JOIN mastertenant.user_master um ON um.user_code = lrm.usercode
                        LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND (bm.tenant_code = ip.tenant_code OR bm.tenant_code = lrm.tenant_code)
                        WHERE (lrm.requestguid::text = @requestGuid OR LOWER(lrm.requestguid::text) = LOWER(@requestGuid))
                          AND (COALESCE(@tenant_code, '') = '' OR lrm.tenant_code IS NULL OR lrm.tenant_code = '' OR lrm.tenant_code = @tenant_code)
                          AND COALESCE(lrm.deleted, false) = false
                        LIMIT 1";

                var header = await db.QueryFirstOrDefaultAsync<RawReportHeader>(
                    headerSql, 
                    new { requestGuid, tenant_code = tenant_code ?? "" },
                    commandTimeout: 120);

                // Fallback 0: Check lab_request_master by ip_id, opvisitid/sheet_id, requestsno, or custid
                if (header == null)
                {
                    string lrmAltHeaderSql = @"
                        SELECT 
                            COALESCE(NULLIF(lrm.requestsno::text, ''), NULLIF(lrm.requestguid::text, ''), 'BILL-001') AS BillNo,
                            lrm.requestguid::text AS RequestGuid,
                            COALESCE(lrm.requestdatetime, NOW()) AS BillDate,
                            COALESCE(NULLIF(lrm.name, ''), NULLIF(cm.name, ''), 'Patient') AS PatientName,
                            COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(lrm.custid::text, ''), '') AS PatientId,
                            COALESCE(
                                NULLIF(CONCAT(NULLIF(lrm.ageyears, '0'), ' Y ', NULLIF(lrm.agemonths, '0'), ' M'), ' Y  M'),
                                NULLIF(CONCAT(NULLIF(cm.ageyears, 0), ' Y ', NULLIF(cm.agemonths, 0), ' M'), ' Y  M'),
                                ''
                            ) AS Age,
                            COALESCE(NULLIF(lrm.gender, ''), NULLIF(cm.gender, ''), '') AS Gender,
                            COALESCE(NULLIF(lrm.address, ''), NULLIF(CONCAT_WS(', ', NULLIF(cm.street, ''), NULLIF(cm.area, ''), NULLIF(cm.city, ''), NULLIF(cm.state, '')), ''), '') AS PatientAddress,
                            COALESCE(NULLIF(lrm.mobileno, ''), NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS CusMobileNo,
                            COALESCE(NULLIF(cm.careof, ''), '') AS CareOf,
                            COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS DoctorName,
                            COALESCE(NULLIF(ip.ip_no, ''), NULLIF(op.op_no, ''), NULLIF(lrm.ip_id::text, ''), NULLIF(lrm.opvisitid, ''), '') AS AdmissionNo,
                            ip.admitdate AS AdmissionDate,
                            ip.dischargedate AS DischargeDate,
                            COALESCE(bm.bedname, ip.bedcode::text, '-') AS BedNo,
                            COALESCE(lrm.requestamount, 0) AS TotalAmount,
                            COALESCE(
                                COALESCE(lrm.discountamount, 0) +
                                COALESCE(lrm.ourdiscount, 0) +
                                COALESCE(lrm.specialdiscount, 0), 0
                            ) AS DiscountAmount,
                            COALESCE(lrm.totalamount, 0) AS NetAmount,
                            COALESCE(lrm.paidamount, 0) AS ReceivedAmount,
                            COALESCE(
                                lrm.totalamount - COALESCE(lrm.paidamount, 0), 0
                            ) AS BalanceAmount,
                            COALESCE(um.name, '') AS CreatedBy,
                            COALESCE(lrm.entereddate, lrm.requestdatetime, NOW()) AS CreatedTime,
                            lrm.tenant_code AS TenantCode,
                            COALESCE(lrm.alteredbhcode::int, lrm.enteredbhcode, 0) AS BhCode,
                            lrm.custid AS CustId,
                            lrm.ip_id::text AS IpId,
                            COALESCE(NULLIF(lrm.opvisitid, ''), NULLIF(lrm.sheet_id, '')) AS OutpatientId
                        FROM lab_request_master lrm
                        LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
                        LEFT JOIN ip_registration ip ON ip.ip_id = lrm.ip_id
                        LEFT JOIN op_registration op ON (op.op_id::text = lrm.opvisitid OR op.op_id::text = lrm.sheet_id)
                        LEFT JOIN doctor_master dm ON (dm.dcode = lrm.dcode OR dm.dcode = ip.dcode OR dm.dcode = op.dcode)
                        LEFT JOIN mastertenant.user_master um ON um.user_code = lrm.usercode
                        LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND (bm.tenant_code = ip.tenant_code OR bm.tenant_code = lrm.tenant_code)
                        WHERE (lrm.ip_id::text = @requestGuid
                               OR lrm.opvisitid = @requestGuid
                               OR lrm.sheet_id = @requestGuid
                               OR lrm.requestsno::text = @requestGuid
                               OR lrm.custid::text = @requestGuid)
                          AND (COALESCE(@tenant_code, '') = '' OR lrm.tenant_code IS NULL OR lrm.tenant_code = '' OR lrm.tenant_code = @tenant_code)
                          AND COALESCE(lrm.deleted, false) = false
                        ORDER BY lrm.requestdatetime DESC
                        LIMIT 1";

                    header = await db.QueryFirstOrDefaultAsync<RawReportHeader>(
                        lrmAltHeaderSql, 
                        new { requestGuid, tenant_code = tenant_code ?? "" },
                        commandTimeout: 120);
                }

                // Fallback 1: Check IP Registration
                if (header == null)
                {
                    string ipHeaderSql = @"
                        SELECT 
                            COALESCE(NULLIF(ip.ip_no, ''), 'BILL-IP') AS BillNo,
                            ip.ip_id::text AS RequestGuid,
                            COALESCE(ip.admitdate, NOW()) AS BillDate,
                            COALESCE(NULLIF(cm.name, ''), 'Patient') AS PatientName,
                            COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(ip.custid::text, ''), '') AS PatientId,
                            CONCAT(COALESCE(cm.ageyears, 0), ' Y ', COALESCE(cm.agemonths, 0), ' M') AS Age,
                            COALESCE(NULLIF(cm.gender, ''), '') AS Gender,
                            CONCAT_WS(', ', NULLIF(cm.street, ''), NULLIF(cm.area, ''), NULLIF(cm.city, ''), NULLIF(cm.state, '')) AS PatientAddress,
                            COALESCE(NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS CusMobileNo,
                            COALESCE(NULLIF(cm.careof, ''), '') AS CareOf,
                            COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS DoctorName,
                            COALESCE(NULLIF(ip.ip_no, ''), ip.ip_id::text) AS AdmissionNo,
                            ip.admitdate AS AdmissionDate,
                            ip.dischargedate AS DischargeDate,
                            COALESCE(bm.bedname, ip.bedcode::text, '-') AS BedNo,
                            0 AS TotalAmount,
                            0 AS DiscountAmount,
                            0 AS NetAmount,
                            0 AS ReceivedAmount,
                            0 AS BalanceAmount,
                            '' AS CreatedBy,
                            COALESCE(ip.admitdate, NOW()) AS CreatedTime,
                            ip.tenant_code AS TenantCode,
                            0 AS BhCode,
                            ip.custid AS CustId,
                            ip.ip_id::text AS IpId,
                            '' AS OutpatientId
                        FROM ip_registration ip
                        LEFT JOIN customerdb.customer_master cm ON cm.custid = ip.custid
                        LEFT JOIN doctor_master dm ON dm.dcode = ip.dcode
                        LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = ip.tenant_code
                        WHERE (LOWER(ip.ip_id::text) = LOWER(@requestGuid) OR ip.ip_no = @requestGuid)
                          AND (COALESCE(@tenant_code, '') = '' OR ip.tenant_code IS NULL OR ip.tenant_code = '' OR ip.tenant_code = @tenant_code)
                        LIMIT 1";

                    header = await db.QueryFirstOrDefaultAsync<RawReportHeader>(
                        ipHeaderSql, 
                        new { requestGuid, tenant_code = tenant_code ?? "" },
                        commandTimeout: 120);
                }

                // Fallback 2: Check OP Registration
                if (header == null)
                {
                    string opHeaderSql = @"
                        SELECT 
                            COALESCE(NULLIF(op.op_no, ''), 'BILL-OP') AS BillNo,
                            op.op_id::text AS RequestGuid,
                            COALESCE(op.visit_date, NOW()) AS BillDate,
                            COALESCE(NULLIF(cm.name, ''), 'Patient') AS PatientName,
                            COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(op.custid::text, ''), '') AS PatientId,
                            CONCAT(COALESCE(cm.ageyears, 0), ' Y ', COALESCE(cm.agemonths, 0), ' M') AS Age,
                            COALESCE(NULLIF(cm.gender, ''), '') AS Gender,
                            CONCAT_WS(', ', NULLIF(cm.street, ''), NULLIF(cm.area, ''), NULLIF(cm.city, ''), NULLIF(cm.state, '')) AS PatientAddress,
                            COALESCE(NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS CusMobileNo,
                            COALESCE(NULLIF(cm.careof, ''), '') AS CareOf,
                            COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS DoctorName,
                            COALESCE(NULLIF(op.op_no, ''), op.op_id::text) AS AdmissionNo,
                            NULL::timestamp AS AdmissionDate,
                            NULL::timestamp AS DischargeDate,
                            '-' AS BedNo,
                            0 AS TotalAmount,
                            0 AS DiscountAmount,
                            0 AS NetAmount,
                            0 AS ReceivedAmount,
                            0 AS BalanceAmount,
                            '' AS CreatedBy,
                            COALESCE(op.visit_date, NOW()) AS CreatedTime,
                            op.tenant_code AS TenantCode,
                            0 AS BhCode,
                            op.custid AS CustId,
                            '' AS IpId,
                            op.op_id::text AS OutpatientId
                        FROM op_registration op
                        LEFT JOIN customerdb.customer_master cm ON cm.custid = op.custid
                        LEFT JOIN doctor_master dm ON dm.dcode = op.dcode
                        WHERE (LOWER(op.op_id::text) = LOWER(@requestGuid) OR op.op_no = @requestGuid)
                          AND (COALESCE(@tenant_code, '') = '' OR op.tenant_code IS NULL OR op.tenant_code = '' OR op.tenant_code = @tenant_code)
                        LIMIT 1";

                    header = await db.QueryFirstOrDefaultAsync<RawReportHeader>(
                        opHeaderSql, 
                        new { requestGuid, tenant_code = tenant_code ?? "" },
                        commandTimeout: 120);
                }

                // Fallback 3: Check Customer Master directly
                if (header == null)
                {
                    string cmHeaderSql = @"
                        SELECT 
                            'BILL-CONSOLIDATED' AS BillNo,
                            @requestGuid AS RequestGuid,
                            NOW() AS BillDate,
                            COALESCE(NULLIF(cm.name, ''), 'Patient') AS PatientName,
                            COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(cm.custid::text, ''), '') AS PatientId,
                            CONCAT(COALESCE(cm.ageyears, 0), ' Y ', COALESCE(cm.agemonths, 0), ' M') AS Age,
                            COALESCE(NULLIF(cm.gender, ''), '') AS Gender,
                            CONCAT_WS(', ', NULLIF(cm.street, ''), NULLIF(cm.area, ''), NULLIF(cm.city, ''), NULLIF(cm.state, '')) AS PatientAddress,
                            COALESCE(NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS CusMobileNo,
                            COALESCE(NULLIF(cm.careof, ''), '') AS CareOf,
                            'SELF' AS DoctorName,
                            0 AS TotalAmount,
                            0 AS DiscountAmount,
                            0 AS NetAmount,
                            0 AS ReceivedAmount,
                            0 AS BalanceAmount,
                            '' AS CreatedBy,
                            NOW() AS CreatedTime,
                            @tenant_code AS TenantCode,
                            0 AS BhCode,
                            cm.custid AS CustId,
                            '' AS IpId,
                            '' AS OutpatientId
                        FROM customerdb.customer_master cm
                        WHERE (cm.custcode::text = @requestGuid OR cm.custid::text = @requestGuid)
                        LIMIT 1";

                    header = await db.QueryFirstOrDefaultAsync<RawReportHeader>(
                        cmHeaderSql, 
                        new { requestGuid, tenant_code = tenant_code ?? "" },
                        commandTimeout: 120);
                }

                // Fallback 4: Create default header object so request never fails with null
                if (header == null)
                {
                    header = new RawReportHeader
                    {
                        BillNo = "BILL-CONSOLIDATED",
                        RequestGuid = requestGuid,
                        BillDate = DateTime.Now,
                        PatientName = "Patient",
                        PatientId = "",
                        Age = "",
                        Gender = "",
                        PatientAddress = "",
                        CusMobileNo = "",
                        CareOf = "",
                        DoctorName = "SELF",
                        TotalAmount = 0m,
                        DiscountAmount = 0m,
                        NetAmount = 0m,
                        ReceivedAmount = 0m,
                        BalanceAmount = 0m,
                        CreatedBy = "",
                        CreatedTime = DateTime.Now,
                        TenantCode = tenant_code ?? "",
                        BhCode = 0,
                        CustId = null,
                        IpId = "",
                        OutpatientId = ""
                    };
                }

                if (string.IsNullOrWhiteSpace(tenant_code))
                {
                    tenant_code = (string)header.TenantCode ?? "";
                }

                // Get company/tenant information
                string companySql = @"
                    SELECT legal_name, address_line1, contact_number, contact_email, host_url
                    FROM mastertenant.tenants
                    WHERE tenant_code = @tenant_code
                    LIMIT 1";

                var company = await db.QueryFirstOrDefaultAsync<dynamic>(
                    companySql, 
                    new { tenant_code });

                string rGuid = header.RequestGuid?.ToString() ?? requestGuid;
                string ipIdStr = header.IpId?.ToString() ?? "";
                string opIdStr = header.OutpatientId?.ToString() ?? "";
                decimal custId = header.CustId ?? 0m;

                // Lab Bills (Lab Tests & Investigation Charges)
                string labBillsSql = @"
                    SELECT 
                        ROW_NUMBER() OVER (ORDER BY T.ServiceDate) AS SNo,
                        T.ItemName,
                        T.Description,
                        T.Quantity,
                        T.UnitPrice,
                        T.Amount,
                        T.ServiceDate,
                        T.DateTime
                    FROM (
                        SELECT 
                            COALESCE(tm.name, lrd.item_name, 'Lab Test') AS ItemName,
                            '' AS Description,
                            1 AS Quantity,
                            COALESCE(lrd.testamount, 0) AS UnitPrice,
                            COALESCE(lrd.testamount, 0) AS Amount,
                            COALESCE(lrm.requestdatetime, lrm.entereddate, NOW()) AS ServiceDate,
                            COALESCE(lrm.requestdatetime, lrm.entereddate, NOW()) AS DateTime
                        FROM lab_request_details lrd
                        INNER JOIN lab_request_master lrm ON lrm.requestguid::text = lrd.requestguid::text
                        LEFT JOIN test_master tm ON tm.tcode = lrd.tcode
                        WHERE (
                            lrd.requestguid::text = @rGuid 
                            OR LOWER(lrd.requestguid::text) = LOWER(@rGuid)
                            OR lrd.requestguid::text = @requestGuid
                            OR LOWER(lrd.requestguid::text) = LOWER(@requestGuid)
                            OR (@custId > 0 AND lrm.custid = @custId)
                            OR (COALESCE(@ipIdStr, '') <> '' AND lrm.ip_id::text = @ipIdStr)
                            OR (COALESCE(@opIdStr, '') <> '' AND (lrm.opvisitid = @opIdStr OR lrm.sheet_id = @opIdStr))
                        )
                        AND (COALESCE(@tenant_code, '') = '' OR lrd.tenant_code IS NULL OR lrd.tenant_code = '' OR lrd.tenant_code = @tenant_code)
                        AND (lrd.ttid = 1 OR lrd.ttid = 0 OR lrd.ttid IS NULL)

                        UNION ALL

                        SELECT 
                            COALESCE(oid.test_name, 'Lab Test / Investigation') AS ItemName,
                            COALESCE(oid.test_category, '') AS Description,
                            CAST(COALESCE(oid.quantity, 1) AS INT) AS Quantity,
                            COALESCE(oid.rate, 0) AS UnitPrice,
                            COALESCE(oid.amount, COALESCE(oid.quantity, 1) * COALESCE(oid.rate, 0)) AS Amount,
                            COALESCE(oim.created_at, NOW()) AS ServiceDate,
                            COALESCE(oim.created_at, NOW()) AS DateTime
                        FROM op_investigation_detail oid
                        INNER JOIN op_investigation_master oim ON oim.inv_id = oid.inv_id
                        LEFT JOIN op_case_sheet cs ON cs.sheet_id = oim.sheet_id
                        WHERE (
                            (COALESCE(@opIdStr, '') <> '' AND (oim.sheet_id::text = @opIdStr OR oim.op_id::text = @opIdStr OR cs.op_id::text = @opIdStr OR cs.sheet_id::text = @opIdStr))
                            OR (COALESCE(@ipIdStr, '') <> '' AND (oim.ip_id::text = @ipIdStr OR cs.ip_id::text = @ipIdStr))
                            OR (oim.sheet_id::text = @requestGuid OR oim.op_id::text = @requestGuid OR cs.op_id::text = @requestGuid OR cs.sheet_id::text = @requestGuid)
                            OR (@custId > 0 AND (oim.custid = @custId OR cs.custid = @custId))
                        )
                        AND (COALESCE(@tenant_code, '') = '' OR oid.tenant_code IS NULL OR oid.tenant_code = '' OR oid.tenant_code = @tenant_code)
                        AND COALESCE(oid.isdeleted, false) = false
                        AND COALESCE(oid.is_billed, false) = false
                        AND NOT EXISTS (
                            SELECT 1 
                            FROM lab_request_details lrd2
                            INNER JOIN lab_request_master lrm2 ON lrm2.requestguid::text = lrd2.requestguid::text
                            WHERE lrm2.tenant_code = oim.tenant_code
                              AND (
                                  LOWER(lrm2.requestguid::text) = LOWER(oim.op_id::text)
                                  OR LOWER(lrm2.opvisitid) = LOWER(oim.op_id::text)
                                  OR LOWER(lrm2.requestguid::text) = LOWER(oim.sheet_id::text)
                                  OR (oim.ip_id IS NOT NULL AND lrm2.ip_id::text = oim.ip_id::text)
                                  OR (lrm2.custid = oim.custid AND lrm2.custid > 0)
                              )
                              AND (
                                  lrd2.tcode::text = oid.test_code::text 
                                  OR LOWER(TRIM(COALESCE(lrd2.item_name, ''))) = LOWER(TRIM(oid.test_name))
                              )
                        )

                        UNION ALL

                        SELECT 
                            COALESCE(tm.name, 'Investigation') AS ItemName,
                            'Investigation Charge' AS Description,
                            CAST(COALESCE(uc.quantity, 1) AS INT) AS Quantity,
                            COALESCE(uc.rate, 0) AS UnitPrice,
                            CASE WHEN COALESCE(uc.amount, 0) = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END AS Amount,
                            COALESCE(uc.chargedate, NOW()) AS ServiceDate,
                            COALESCE(uc.chargedate, NOW()) AS DateTime
                        FROM unbilledcharges uc
                        LEFT JOIN test_master tm ON tm.tcode::text = uc.tcode::text
                        WHERE (
                            (COALESCE(@opIdStr, '') <> '' AND uc.opvisitid = @opIdStr)
                            OR (COALESCE(@ipIdStr, '') <> '' AND uc.ip_id::text = @ipIdStr)
                            OR (uc.opvisitid = @requestGuid OR uc.ip_id::text = @requestGuid)
                            OR (@custId > 0 AND uc.custid = @custId)
                        )
                        AND (COALESCE(@tenant_code, '') = '' OR uc.tenant_code IS NULL OR uc.tenant_code = '' OR uc.tenant_code = @tenant_code)
                        AND UPPER(uc.entrytype) IN ('INVESTIGATION', 'LAB', 'LABTEST', 'TEST')
                        AND COALESCE(uc.billedstatus, false) = false
                    ) T
                    ORDER BY T.ServiceDate";

                var labBillItems = (await db.QueryAsync<ConsolidatedBillItem>(
                    labBillsSql, 
                    new { rGuid, requestGuid, custId, ipIdStr, opIdStr, tenant_code },
                    commandTimeout: 120)).ToList();

                var labBills = labBillItems.Any() ? new ConsolidatedBillCategory
                {
                    CategoryName = "Lab Bills",
                    Items = labBillItems,
                    SubTotal = labBillItems.Sum(i => i.Amount ?? 0m)
                } : null;

                // Consultation Charges - from unbilledcharges table
                string consultationSql = @"
                    SELECT 
                        ROW_NUMBER() OVER (ORDER BY uc.chargedate) AS SNo,
                        'Consultation Fee' AS ItemName,
                        COALESCE(dm.doctorfullname, dm.name, 'General Consultation') AS Description,
                        CAST(COALESCE(uc.quantity, 1) AS INT) AS Quantity,
                        COALESCE(uc.rate, 0) AS UnitPrice,
                        CASE WHEN COALESCE(uc.amount, 0) = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END AS Amount,
                        COALESCE(uc.chargedate, NOW()) AS ServiceDate,
                        COALESCE(uc.chargedate, NOW()) AS DateTime
                    FROM unbilledcharges uc
                    LEFT JOIN doctor_master dm ON dm.dcode::text = uc.tcode::text
                    WHERE (
                        (COALESCE(@opIdStr, '') <> '' AND uc.opvisitid = @opIdStr)
                        OR (COALESCE(@ipIdStr, '') <> '' AND uc.ip_id::text = @ipIdStr)
                        OR (uc.opvisitid = @requestGuid OR uc.ip_id::text = @requestGuid)
                        OR (@custId > 0 AND uc.custid = @custId)
                    )
                    AND (COALESCE(@tenant_code, '') = '' OR uc.tenant_code IS NULL OR uc.tenant_code = '' OR uc.tenant_code = @tenant_code)
                    AND UPPER(uc.entrytype) = 'CONSULTATION'
                    AND COALESCE(uc.billedstatus, false) = false
                    ORDER BY uc.chargedate";

                var consultationItems = (await db.QueryAsync<ConsolidatedBillItem>(
                    consultationSql, 
                    new { opIdStr, ipIdStr, requestGuid, custId, tenant_code },
                    commandTimeout: 120)).ToList();

                var consultation = consultationItems.Any() ? new ConsolidatedBillCategory
                {
                    CategoryName = "Consultation",
                    Items = consultationItems,
                    SubTotal = consultationItems.Sum(i => i.Amount ?? 0m)
                } : null;

                // Nurse Charges (Injections, Dressings, Procedures, Services) from virges
                string nurseChargesSql = @"
                    SELECT 
                        ROW_NUMBER() OVER (ORDER BY uc.chargedate) AS SNo,
                        COALESCE(NULLIF(tm.name, ''), 'Nurse Charge / Service') AS ItemName,
                        '' AS Description,
                        CAST(COALESCE(uc.quantity, 1) AS INT) AS Quantity,
                        COALESCE(uc.rate, 0) AS UnitPrice,
                        CASE WHEN COALESCE(uc.amount, 0) = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END AS Amount,
                        COALESCE(uc.chargedate, NOW()) AS ServiceDate,
                        COALESCE(uc.chargedate, NOW()) AS DateTime
                    FROM unbilledcharges uc
                    LEFT JOIN test_master tm ON tm.tcode::text = uc.tcode::text
                    WHERE (
                        (COALESCE(@opIdStr, '') <> '' AND uc.opvisitid = @opIdStr)
                        OR (COALESCE(@ipIdStr, '') <> '' AND uc.ip_id::text = @ipIdStr)
                        OR (uc.opvisitid = @requestGuid OR uc.ip_id::text = @requestGuid)
                        OR (@custId > 0 AND uc.custid = @custId)
                    )
                    AND (COALESCE(@tenant_code, '') = '' OR uc.tenant_code IS NULL OR uc.tenant_code = '' OR uc.tenant_code = @tenant_code)
                    AND (uc.entrytype IS NULL OR UPPER(uc.entrytype) NOT IN ('CONSULTATION', 'ROOMRENT', 'INVESTIGATION', 'LAB', 'LABTEST', 'TEST'))
                    AND COALESCE(uc.billedstatus, false) = false
                    ORDER BY uc.chargedate";

                var investigationItems = (await db.QueryAsync<ConsolidatedBillItem>(
                    nurseChargesSql, 
                    new { opIdStr, ipIdStr, requestGuid, custId, tenant_code },
                    commandTimeout: 120)).ToList();

                var nurseCharges = investigationItems.Any() ? new ConsolidatedBillCategory
                {
                    CategoryName = "Nurse Charges (Injections & Services)",
                    Items = investigationItems,
                    SubTotal = investigationItems.Sum(i => i.Amount ?? 0m)
                } : null;

                // Bed / Room Charges - from unbilledcharges (ROOMRENT)
                string bedChargesSql = @"
                    SELECT 
                        ROW_NUMBER() OVER (ORDER BY uc.chargedate) AS SNo,
                        uc.tcode::text AS Code,
                        COALESCE(bm.bedname, rm.name, 'Bed / Room Rent') AS ItemName,
                        COALESCE(bm.shortname, rm.name, 'Room Rent') AS Description,
                        CAST(COALESCE(uc.quantity, 1) AS INT) AS Quantity,
                        COALESCE(uc.rate, 0) AS UnitPrice,
                        CASE WHEN COALESCE(uc.amount, 0) = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END AS Amount,
                        COALESCE(uc.chargedate, NOW()) AS ServiceDate,
                        COALESCE(uc.chargedate, NOW()) AS DateTime
                    FROM unbilledcharges uc
                    LEFT JOIN public.roomtype_master rm ON rm.rmtcode = uc.tcode AND rm.tenant_code = uc.tenant_code
                    LEFT JOIN public.bed_master bm ON bm.bedcode = uc.bedcode AND bm.tenant_code = uc.tenant_code
                    WHERE (
                        (COALESCE(@ipIdStr, '') <> '' AND uc.ip_id::text = @ipIdStr)
                        OR (uc.ip_id::text = @requestGuid)
                        OR (@custId > 0 AND uc.custid = @custId)
                    )
                    AND (COALESCE(@tenant_code, '') = '' OR uc.tenant_code IS NULL OR uc.tenant_code = '' OR uc.tenant_code = @tenant_code)
                    AND UPPER(uc.entrytype) = 'ROOMRENT'
                    AND COALESCE(uc.billedstatus, false) = false
                    ORDER BY uc.chargedate";

                // Query splits for all room types
                string testGroupRatesSql = @"
                    SELECT 
                        tgr.rmtcode,
                        tm.name AS SubtestName,
                        tgr.testrate
                    FROM public.test_group_rates tgr
                    INNER JOIN public.test_master tm ON tm.tcode = tgr.subtestcode
                    WHERE (COALESCE(@tenant_code, '') = '' OR tgr.tenant_code = @tenant_code)";

                var testGroupRates = (await db.QueryAsync<dynamic>(
                    testGroupRatesSql, 
                    new { tenant_code },
                    commandTimeout: 120)).ToList();

                var bedChargeItemsRaw = (await db.QueryAsync<ConsolidatedBillItem>(
                    bedChargesSql, 
                    new { ipIdStr, requestGuid, custId, tenant_code },
                    commandTimeout: 120)).ToList();

                var bedChargeItems = new List<ConsolidatedBillItem>();
                var extraNurseItems = new List<ConsolidatedBillItem>();
                var extraConsultationItems = new List<ConsolidatedBillItem>();

                foreach (var rc in bedChargeItemsRaw)
                {
                    int rmtcode = 0;
                    int.TryParse(rc.Code, out rmtcode);

                    var splits = testGroupRates.Where(x => (int)x.rmtcode == rmtcode).ToList();
                    if (splits.Any())
                    {
                        foreach (var split in splits)
                        {
                            string subtestName = split.subtestname ?? "Room Charge / Service";
                            decimal testrate = (decimal)(split.testrate ?? 0m);

                            var splitItem = new ConsolidatedBillItem
                            {
                                Code = rc.Code,
                                ItemName = subtestName,
                                Description = rc.Description,
                                Quantity = rc.Quantity,
                                UnitPrice = testrate,
                                Amount = rc.Quantity * testrate,
                                ServiceDate = rc.ServiceDate,
                                DateTime = rc.DateTime
                            };

                            string nameLower = subtestName.ToLowerInvariant();
                            if (nameLower.Contains("consultation"))
                            {
                                extraConsultationItems.Add(splitItem);
                            }
                            else if (nameLower.Contains("room") || nameLower.Contains("bed") || nameLower.Contains("rent"))
                            {
                                bedChargeItems.Add(splitItem);
                            }
                            else
                            {
                                extraNurseItems.Add(splitItem);
                            }
                        }
                    }
                    else
                    {
                        bedChargeItems.Add(rc);
                    }
                }

                if (extraConsultationItems.Any())
                {
                    consultationItems.AddRange(extraConsultationItems);
                }

                if (extraNurseItems.Any())
                {
                    investigationItems.AddRange(extraNurseItems);
                }

                // Re-sequence SNo
                int bedSno = 1;
                foreach (var item in bedChargeItems) item.SNo = bedSno++;

                int nurseSno = 1;
                foreach (var item in investigationItems) item.SNo = nurseSno++;

                int consultSno = 1;
                foreach (var item in consultationItems) item.SNo = consultSno++;

                var bedCharges = bedChargeItems.Any() ? new ConsolidatedBillCategory
                {
                    CategoryName = "Bed Charges",
                    Items = bedChargeItems,
                    SubTotal = bedChargeItems.Sum(i => i.Amount ?? 0m)
                } : null;

                // Medicines/Prescriptions - directly from prescription details (only if includeMedicines is true)
                ConsolidatedBillCategory? medicines = null;
                if (includeMedicines)
                {
                    string medicinesSql = @"
                        SELECT 
                            ROW_NUMBER() OVER (ORDER BY opd.sno) AS SNo,
                            COALESCE(opd.drug_name, 'Medicine') AS ItemName,
                            CONCAT(
                                COALESCE(opd.morning, '0'), '-',
                                COALESCE(opd.afternoon, '0'), '-',
                                COALESCE(opd.evening, '0'), '-',
                                COALESCE(opd.night, '0'),
                                ' for ', COALESCE(opd.days::text, '0'), ' days'
                            ) AS Description,
                            COALESCE(opd.qty, 0) AS Quantity,
                            COALESCE(opd.rate, 0) AS UnitPrice,
                            COALESCE(opd.qty, 0) * COALESCE(opd.rate, 0) AS Amount,
                            COALESCE(opm.created_at, opd.created_at, NOW()) AS ServiceDate,
                            COALESCE(opm.created_at, opd.created_at, NOW()) AS DateTime
                        FROM op_prescription_detail opd
                        INNER JOIN op_prescription_master opm ON opm.pr_id = opd.pr_id
                        LEFT JOIN op_case_sheet cs ON cs.sheet_id = opm.sheet_id
                        WHERE (
                            (COALESCE(@opIdStr, '') <> '' AND (opm.sheet_id::text = @opIdStr OR opm.op_id::text = @opIdStr OR cs.op_id::text = @opIdStr OR cs.sheet_id::text = @opIdStr))
                            OR (COALESCE(@ipIdStr, '') <> '' AND (opm.ip_id::text = @ipIdStr OR cs.ip_id::text = @ipIdStr))
                            OR (opm.sheet_id::text = @requestGuid OR opm.op_id::text = @requestGuid OR cs.op_id::text = @requestGuid OR cs.sheet_id::text = @requestGuid)
                            OR (@custId > 0 AND (opm.custid = @custId OR cs.custid = @custId))
                        )
                        AND (COALESCE(@tenant_code, '') = '' OR opd.tenant_code IS NULL OR opd.tenant_code = '' OR opd.tenant_code = @tenant_code)
                        AND COALESCE(opd.isdeleted, false) = false
                        AND COALESCE(opd.is_billed, false) = false
                        ORDER BY opd.sno";

                    var medicineItems = (await db.QueryAsync<ConsolidatedBillItem>(
                        medicinesSql, 
                        new { opIdStr, ipIdStr, requestGuid, custId, tenant_code })).ToList();

                    medicines = medicineItems.Any() ? new ConsolidatedBillCategory
                    {
                        CategoryName = "Medicines",
                        Items = medicineItems,
                        SubTotal = medicineItems.Sum(i => i.Amount ?? 0m)
                    } : null;
                }

                // Build BillSummary and DetailedBreakup lists for Sample Report UI
                var billSummaryList = new List<ConsolidatedBillSummaryItem>();
                var detailedBreakupList = new List<ConsolidatedBillCategory>();

                decimal roomNurseTotal = (bedCharges?.SubTotal ?? 0m) + (nurseCharges?.SubTotal ?? 0m);
                if (roomNurseTotal > 0 || bedCharges != null || nurseCharges != null)
                {
                    billSummaryList.Add(new ConsolidatedBillSummaryItem
                    {
                        PrimaryCode = "100000",
                        Particulars = "Room & Nursing Charges",
                        Amount = roomNurseTotal
                    });
                }

                if (bedCharges != null && bedCharges.Items.Count > 0)
                {
                    bedCharges.CategoryCode = "100000";
                    bedCharges.CategoryName = "Room/Bed Charges";
                    detailedBreakupList.Add(bedCharges);
                }

                if (nurseCharges != null && nurseCharges.Items.Count > 0)
                {
                    nurseCharges.CategoryCode = "102001";
                    nurseCharges.CategoryName = "Nursing Charges";
                    detailedBreakupList.Add(nurseCharges);
                }

                if (includeMedicines && medicines != null && medicines.Items.Count > 0)
                {
                    billSummaryList.Add(new ConsolidatedBillSummaryItem
                    {
                        PrimaryCode = "400000",
                        Particulars = "Medicine & Consumables",
                        Amount = medicines.SubTotal ?? 0m
                    });
                    medicines.CategoryCode = "400000";
                    medicines.CategoryName = "Medicine & Consumables";
                    detailedBreakupList.Add(medicines);
                }

                if (consultation != null && consultation.Items.Count > 0)
                {
                    billSummaryList.Add(new ConsolidatedBillSummaryItem
                    {
                        PrimaryCode = "500000",
                        Particulars = "Consultations",
                        Amount = consultation.SubTotal ?? 0m
                    });
                    consultation.CategoryCode = "500000";
                    consultation.CategoryName = "Consultations";
                    detailedBreakupList.Add(consultation);
                }

                if (labBills != null && labBills.Items.Count > 0)
                {
                    billSummaryList.Add(new ConsolidatedBillSummaryItem
                    {
                        PrimaryCode = "600000",
                        Particulars = "Investigation Charges",
                        Amount = labBills.SubTotal ?? 0m
                    });
                    labBills.CategoryCode = "600000";
                    labBills.CategoryName = "Investigation Charges";
                    detailedBreakupList.Add(labBills);
                }

                // Compute overall financial totals dynamically from summary line items
                decimal calcTotal = billSummaryList.Sum(item => item.Amount ?? 0m);

                decimal rawTotal = header.TotalAmount != null ? Convert.ToDecimal(header.TotalAmount) : 0m;
                decimal finalTotal = calcTotal > 0 ? calcTotal : rawTotal;

                if (billSummaryList.Count == 0 && finalTotal > 0)
                {
                    billSummaryList.Add(new ConsolidatedBillSummaryItem
                    {
                        PrimaryCode = "100000",
                        Particulars = "Hospital Service Charges",
                        Amount = finalTotal
                    });
                }

                decimal rawDiscount = header.DiscountAmount != null ? Convert.ToDecimal(header.DiscountAmount) : 0m;
                decimal finalNet = finalTotal - rawDiscount;

                // Fetch actual paid amount from balancecollectionby (both legacy & new-style guid columns)
                // Also covers IP/OP visits via receipt_details linkage
                string paidAmountSql = @"
                    SELECT COALESCE(SUM(bcb.collectedamount), 0)
                    FROM balancecollectionby bcb
                    WHERE COALESCE(bcb.deleted, false) = false
                      AND (
                          -- new-style guid columns
                          (COALESCE(@rGuid, '') <> '' AND (
                              LOWER(bcb.request_guid) = LOWER(@rGuid)
                              OR LOWER(bcb.request_guid) = LOWER(@requestGuid)
                          ))
                          -- legacy guid columns
                          OR (COALESCE(@rGuid, '') <> '' AND (
                              LOWER(bcb.requestguid) = LOWER(@rGuid)
                              OR LOWER(bcb.requestguid) = LOWER(@requestGuid)
                          ))
                          -- match via ip_id
                          OR (COALESCE(@ipIdStr, '') <> '' AND bcb.request_guid IN (
                              SELECT lrm2.requestguid::text
                              FROM lab_request_master lrm2
                              WHERE lrm2.ip_id::text = @ipIdStr
                                AND COALESCE(lrm2.deleted, false) = false
                          ))
                          OR (COALESCE(@ipIdStr, '') <> '' AND bcb.requestguid IN (
                              SELECT lrm2.requestguid::text
                              FROM lab_request_master lrm2
                              WHERE lrm2.ip_id::text = @ipIdStr
                                AND COALESCE(lrm2.deleted, false) = false
                          ))
                          -- match via opvisitid / sheet_id
                          OR (COALESCE(@opIdStr, '') <> '' AND bcb.request_guid IN (
                              SELECT lrm2.requestguid::text
                              FROM lab_request_master lrm2
                              WHERE (lrm2.opvisitid = @opIdStr OR lrm2.sheet_id = @opIdStr)
                                AND COALESCE(lrm2.deleted, false) = false
                          ))
                          OR (COALESCE(@opIdStr, '') <> '' AND bcb.requestguid IN (
                              SELECT lrm2.requestguid::text
                              FROM lab_request_master lrm2
                              WHERE (lrm2.opvisitid = @opIdStr OR lrm2.sheet_id = @opIdStr)
                                AND COALESCE(lrm2.deleted, false) = false
                          ))
                      )
                      AND (COALESCE(@tenant_code, '') = '' OR bcb.tenant_code IS NULL OR bcb.tenant_code = '' OR bcb.tenant_code = @tenant_code)";

                var bcbPaidObj = await db.ExecuteScalarAsync<object?>(
                    paidAmountSql,
                    new { rGuid, requestGuid, ipIdStr, opIdStr, tenant_code });
                decimal bcbPaid = bcbPaidObj != null ? Convert.ToDecimal(bcbPaidObj) : 0m;

                // Fallback: sum from balancecollectionbytest (test-level settlement)
                decimal bcbtPaid = 0m;
                if (bcbPaid == 0m)
                {
                    string paidByTestSql = @"
                        SELECT COALESCE(SUM(bcbt.collectedamount), 0)
                        FROM balancecollectionbytest bcbt
                        INNER JOIN balancecollectionby bcb ON bcb.balancecollectionbyid = bcbt.balancecollectionbyid
                        WHERE COALESCE(bcb.deleted, false) = false
                          AND (
                              LOWER(bcb.request_guid) = LOWER(@rGuid)
                              OR LOWER(bcb.request_guid) = LOWER(@requestGuid)
                              OR LOWER(bcb.requestguid) = LOWER(@rGuid)
                              OR LOWER(bcb.requestguid) = LOWER(@requestGuid)
                          )
                          AND (COALESCE(@tenant_code, '') = '' OR bcbt.tenant_code IS NULL OR bcbt.tenant_code = '' OR bcbt.tenant_code = @tenant_code)";

                    var bcbtPaidObj = await db.ExecuteScalarAsync<object?>(
                        paidByTestSql,
                        new { rGuid, requestGuid, tenant_code });
                    bcbtPaid = bcbtPaidObj != null ? Convert.ToDecimal(bcbtPaidObj) : 0m;
                }

                // Final paid = bcb sum → bcbt sum → header paidamount from lab_request_master
                decimal rawPaidHeader = header.ReceivedAmount != null ? Convert.ToDecimal(header.ReceivedAmount) : 0m;
                decimal rawPaid = bcbPaid > 0m ? bcbPaid
                                : bcbtPaid > 0m ? bcbtPaid
                                : rawPaidHeader;
                decimal finalBalance = finalNet - rawPaid;

                // Query Advances from receipt_advances
                var advancesList = new List<ConsolidatedBillAdvanceRow>();
                if (custId > 0)
                {
                    try
                    {
                        string advancesSql = @"
                            -- Advances / Refunds / Usage
                            SELECT 
                                rm.receiptsnoprint AS ReceiptNo,
                                rm.receiptdate AS Date,
                                CASE
                                    WHEN ra.requestguid IS NULL THEN 'DEPOSIT'
                                    WHEN rref.receipttype = 'ADVANCE_REFUND' THEN 'REFUND'
                                    ELSE 'USED'
                                END AS Type,
                                CAST(COALESCE(ra.receiptamount, 0) AS DECIMAL) AS Amount,
                                COALESCE(rm.paymentmode, '') AS Description
                            FROM receipt_advances ra
                            INNER JOIN receipt_master rm
                                    ON ra.receiptguid  = rm.receiptguid
                                   AND rm.tenant_code  = @tenant_code
                            LEFT JOIN receipt_master rref
                                    ON rref.receiptguid  = ra.requestguid
                                   AND rref.tenant_code  = @tenant_code
                                   AND rref.receipttype  = 'ADVANCE_REFUND'
                            WHERE rm.custid      = @custId
                              AND rm.tenant_code = @tenant_code
                              AND rm.receipttype = 'ADVANCE'
                              AND COALESCE(rm.isdeleted, false) = false
                              AND COALESCE(ra.deleted,   false) = false

                            UNION ALL

                            -- Direct Collections / Payments
                            SELECT 
                                rm.receiptsnoprint AS ReceiptNo,
                                rm.receiptdate AS Date,
                                'PAYMENT' AS Type,
                                CAST(COALESCE(bcb.collectedamount, 0) AS DECIMAL) AS Amount,
                                COALESCE(bcb.collection_type, rm.paymentmode, '') AS Description
                            FROM balancecollectionby bcb
                            INNER JOIN receipt_master rm
                                    ON rm.receiptguid  = bcb.receipt_guid
                                   AND rm.tenant_code  = bcb.tenant_code
                            WHERE bcb.tenant_code = @tenant_code
                              AND COALESCE(bcb.deleted, false) = false
                              AND COALESCE(rm.isdeleted, false) = false
                              AND (
                                  -- match via requestguid/rGuid
                                  (COALESCE(@rGuid, '') <> '' AND (
                                      LOWER(bcb.request_guid) = LOWER(@rGuid)
                                      OR LOWER(bcb.request_guid) = LOWER(@requestGuid)
                                      OR LOWER(bcb.requestguid) = LOWER(@rGuid)
                                      OR LOWER(bcb.requestguid) = LOWER(@requestGuid)
                                  ))
                                  -- match via ip_id
                                  OR (COALESCE(@ipIdStr, '') <> '' AND bcb.request_guid IN (
                                      SELECT lrm2.requestguid::text
                                      FROM lab_request_master lrm2
                                      WHERE lrm2.ip_id::text = @ipIdStr
                                        AND COALESCE(lrm2.deleted, false) = false
                                  ))
                                  OR (COALESCE(@ipIdStr, '') <> '' AND bcb.requestguid IN (
                                      SELECT lrm2.requestguid::text
                                      FROM lab_request_master lrm2
                                      WHERE lrm2.ip_id::text = @ipIdStr
                                        AND COALESCE(lrm2.deleted, false) = false
                                  ))
                                  -- match via opvisitid/sheet_id
                                  OR (COALESCE(@opIdStr, '') <> '' AND bcb.request_guid IN (
                                      SELECT lrm2.requestguid::text
                                      FROM lab_request_master lrm2
                                      WHERE (lrm2.opvisitid = @opIdStr OR lrm2.sheet_id = @opIdStr)
                                        AND COALESCE(lrm2.deleted, false) = false
                                  ))
                                  OR (COALESCE(@opIdStr, '') <> '' AND bcb.requestguid IN (
                                      SELECT lrm2.requestguid::text
                                      FROM lab_request_master lrm2
                                      WHERE (lrm2.opvisitid = @opIdStr OR lrm2.sheet_id = @opIdStr)
                                        AND COALESCE(lrm2.deleted, false) = false
                                  ))
                              )
                            ORDER BY Date ASC";

                        var advRows = await db.QueryAsync<ConsolidatedBillAdvanceRow>(
                            advancesSql,
                            new { custId = (int)custId, tenant_code, rGuid, requestGuid, ipIdStr, opIdStr });
                        
                        if (advRows != null)
                        {
                            advancesList = advRows.ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error querying advances for consolidated bill: {ex.Message}");
                    }
                }

                // Build the consolidated bill data object
                var billData = new ConsolidatedBillData
                {
                    Advances = advancesList,
                    // Company Info
                    LabName = (string)(company?.legal_name ?? ""),
                    BranchName = "",
                    Address = (string)(company?.address_line1 ?? ""),
                    MobileNo = (string)(company?.contact_number ?? ""),
                    ContactNo = (string)(company?.contact_number ?? ""),
                    Email = (string)(company?.contact_email ?? ""),
                    Website = (string)(company?.host_url ?? ""),
                    HelplineNo = (string)(company?.contact_number ?? ""),
                    RegNo = "",
                    Timings = "",
                    ClosedDay = "",

                    // Bill Info
                    BillNo = header.BillNo?.ToString() ?? "",
                    RequestGuid = header.RequestGuid ?? "",
                    BillDate = header.BillDate != null ? (DateTime?)Convert.ToDateTime(header.BillDate) : DateTime.Now,

                    // Patient & Admission Info
                    PatientUid = header.PatientId ?? "",
                    PatientName = header.PatientName ?? "",
                    PatientId = header.PatientId ?? "",
                    Age = header.Age ?? "",
                    Gender = header.Gender ?? "",
                    PatientAddress = header.PatientAddress ?? "",
                    CusMobileNo = header.CusMobileNo ?? "",
                    CareOf = header.CareOf ?? "",
                    AdmissionNo = !string.IsNullOrWhiteSpace(header.AdmissionNo) ? header.AdmissionNo : (!string.IsNullOrEmpty(ipIdStr) ? ipIdStr : (!string.IsNullOrEmpty(opIdStr) ? opIdStr : header.BillNo?.ToString() ?? "")),
                    AdmissionDate = header.AdmissionDate,
                    DischargeDate = header.DischargeDate,
                    BedNo = !string.IsNullOrWhiteSpace(header.BedNo) ? header.BedNo : "-",

                    // Summary & Breakup
                    BillSummary = billSummaryList,
                    DetailedBreakup = detailedBreakupList,

                    // Categories
                    LabBills = labBills,
                    Consultation = consultation,
                    NurseCharges = nurseCharges,
                    BedCharges = bedCharges,
                    Medicines = medicines,

                    // Totals
                    TotalAmount = finalTotal,
                    TotalBillAmount = finalTotal,
                    AmountPayable = finalNet,
                    AmountPaid = rawPaid,
                    ReceivedAmount = rawPaid,
                    DiscountAmount = rawDiscount,
                    NetAmount = finalNet,
                    BalanceAmount = finalBalance,
                    AmountInWords = null,

                    // Footer
                    CreatedBy = header.CreatedBy ?? "",
                    CreatedTime = header.CreatedTime != null ? (DateTime?)Convert.ToDateTime(header.CreatedTime) : DateTime.Now,
                    billauthorizedby = null,
                    billauthorizesignature = null
                };

                return billData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetConsolidatedBillDataAsync: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates a consolidated bill PDF by calling the ReportingServer
        /// </summary>
        public async Task<string> ConsolidatedBillPDF(
            Guid requestguid, 
            bool includeMedicines, 
            string tenant_code, 
            bool? isletterhead = false,
            Guid? op_id = null)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                // Resolve the ID to use: op_id (explicit OP), ip_id (IP patient), or requestguid fallback
                string resolvedIpId = requestguid.ToString();

                // If op_id is explicitly provided, skip resolution and use it directly
                if (op_id.HasValue && op_id.Value != Guid.Empty)
                {
                    resolvedIpId = op_id.Value.ToString();
                }
                else
                {
                    // 1. Try to resolve from lab_request_master (IP path)
                    string resolveIpSql = @"
                        SELECT ip_id::text 
                        FROM lab_request_master 
                        WHERE (requestguid::text = @requestguidStr) 
                          AND (COALESCE(@tenant_code, '') = '' OR tenant_code = @tenant_code)
                          AND ip_id IS NOT NULL 
                        LIMIT 1";
                    string ipIdStr = await db.QueryFirstOrDefaultAsync<string>(resolveIpSql, new { requestguidStr = requestguid.ToString(), tenant_code });
                    if (!string.IsNullOrEmpty(ipIdStr))
                    {
                        resolvedIpId = ipIdStr;
                    }
                    else
                    {
                        // 2. Try to resolve from op_case_sheet (IP path via sheet)
                        string resolveIpFromSheetSql = @"
                            SELECT ip_id::text 
                            FROM op_case_sheet 
                            WHERE (sheet_id::text = @requestguidStr)
                              AND (COALESCE(@tenant_code, '') = '' OR tenant_code = @tenant_code)
                              AND ip_id IS NOT NULL 
                            LIMIT 1";
                        string ipIdFromSheet = await db.QueryFirstOrDefaultAsync<string>(resolveIpFromSheetSql, new { requestguidStr = requestguid.ToString(), tenant_code });
                        if (!string.IsNullOrEmpty(ipIdFromSheet))
                        {
                            resolvedIpId = ipIdFromSheet;
                        }
                        else
                        {
                            // 3. Try to resolve as OP registration directly (OP patient path)
                            string resolveOpSql = @"
                                SELECT op_id::text 
                                FROM op_registration 
                                WHERE (LOWER(op_id::text) = LOWER(@requestguidStr) OR op_no = @requestguidStr)
                                  AND (COALESCE(@tenant_code, '') = '' OR tenant_code = @tenant_code)
                                LIMIT 1";
                            string opIdFromReg = await db.QueryFirstOrDefaultAsync<string>(resolveOpSql, new { requestguidStr = requestguid.ToString(), tenant_code });
                            if (!string.IsNullOrEmpty(opIdFromReg))
                            {
                                resolvedIpId = opIdFromReg;
                            }
                            else
                            {
                                // 4. Try to resolve op_id from lab_request_master (OP path via opvisitid/sheet_id)
                                string resolveOpFromLrmSql = @"
                                    SELECT COALESCE(NULLIF(opvisitid, ''), NULLIF(sheet_id, '')) AS op_id_str
                                    FROM lab_request_master 
                                    WHERE (requestguid::text = @requestguidStr) 
                                      AND (COALESCE(@tenant_code, '') = '' OR tenant_code = @tenant_code)
                                      AND ip_id IS NULL
                                      AND (opvisitid IS NOT NULL OR sheet_id IS NOT NULL)
                                    LIMIT 1";
                                string opIdFromLrm = await db.QueryFirstOrDefaultAsync<string>(resolveOpFromLrmSql, new { requestguidStr = requestguid.ToString(), tenant_code });
                                if (!string.IsNullOrEmpty(opIdFromLrm))
                                {
                                    resolvedIpId = opIdFromLrm;
                                }
                            }
                        }
                    }
                }


                // Verify bill data exists
                var billData = await GetConsolidatedBillDataAsync(
                    resolvedIpId.ToString(), 
                    includeMedicines, 
                    tenant_code);

                if (billData == null)
                    throw new Exception("Consolidated bill data not found");

                // Fetch lab settings for header/footer images
                var lsConfig = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                    @"SELECT * FROM lab_settings 
                      WHERE tenant_code = @tenant_code 
                        AND COALESCE(deleted, false) = false 
                      ORDER BY bh_code 
                      LIMIT 1",
                    new { tenant_code });

                byte[]? headerImage = null;
                byte[]? footerImage = null;
                byte[]? logoImage = null;

                if (lsConfig != null)
                {
                    string? hKey = !string.IsNullOrWhiteSpace(lsConfig.header_path) 
                        ? lsConfig.header_path 
                        : lsConfig.header_image_path;
                    
                    string? fKey = !string.IsNullOrWhiteSpace(lsConfig.footer_path) 
                        ? lsConfig.footer_path 
                        : lsConfig.footer_image_path;

                    if (!string.IsNullOrWhiteSpace(hKey))
                    {
                        try 
                        { 
                            var hRes = await _s3Service.DownloadAsync(hKey); 
                            if (hRes.HasValue) headerImage = hRes.Value.Data; 
                        } 
                        catch { }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(fKey))
                    {
                        try 
                        { 
                            var fRes = await _s3Service.DownloadAsync(fKey); 
                            if (fRes.HasValue) footerImage = fRes.Value.Data; 
                        } 
                        catch { }
                    }
                }

                // Build the request payload for ReportingServer
                var payload = new ConsolidatedBillRequest
                {
                    RequestGuid = resolvedIpId.ToString(),
                    TenantId = tenant_code,
                    IncludeMedicines = includeMedicines,
                    isletterhead = isletterhead ?? false,
                    headerimage = headerImage,
                    footerimage = footerImage,
                    logo = logoImage,
                    show_header_footer_image = true,
                    BillData = billData
                };

                // Call the ReportingServer
                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/consolidatedbill/getconsolidatedbill", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.ConsolidatedBillPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string?> StockReportPDF(string tenant_code, string? warehousecode = null, DateTime? date = null)
        {
            try
            {
                string inventoryConn = _config.GetConnectionString("inventory_conn");
                List<StockReportModel> stockList;
                using (IDbConnection dbInv = new NpgsqlConnection(inventoryConn))
                {
                    string sqlStock = @"
                        SELECT 
                            sm.stockcode,
                            sm.itemcode,
                            im.itemname,
                            sm.batchno,
                            sm.expirydate,
                            wm.warehousename,
                            COALESCE(sm.openingstock, 0) AS openingstock,
                            COALESCE(sm.purchasedqty, 0) AS purchasedqty,
                            COALESCE(sm.soldqty, 0) AS soldqty,
                            COALESCE(sm.closingstock, 0) AS closingstock,
                            COALESCE(sm.unitcost, 0) AS unitcost,
                            COALESCE(sm.stockvalue, 0) AS stockvalue
                        FROM public.stock_master sm
                        LEFT JOIN public.item_master im ON im.itemcode::text = sm.itemcode::text
                        LEFT JOIN public.warehouse_master wm ON wm.warehousecode::text = sm.warehousecode::text
                        WHERE COALESCE(sm.deleted, false) = false 
                          AND sm.tenantcode = @tenant_code
                          AND (COALESCE(@warehousecode, '') = '' OR sm.warehousecode::text = @warehousecode)
                        ORDER BY im.itemname, sm.batchno";

                    stockList = (await dbInv.QueryAsync<StockReportModel>(sqlStock, new { tenant_code, warehousecode })).ToList();
                }

                dynamic? companyInfo = null;
                byte[]? logoImage = null;

                using (IDbConnection db = new NpgsqlConnection(_conn))
                {
                    string sqlCompany = @"
                        SELECT legal_name, address_line1, contact_number, contact_email, logo_url
                        FROM mastertenant.tenants
                        WHERE tenant_code = @tenant_code";

                    companyInfo = await db.QueryFirstOrDefaultAsync<dynamic>(sqlCompany, new { tenant_code });
                    if (companyInfo != null && !string.IsNullOrEmpty((string?)companyInfo.logo_url))
                    {
                        try
                        {
                            var logoRes = await _s3Service.DownloadAsync((string)companyInfo.logo_url);
                            if (logoRes.HasValue) logoImage = logoRes.Value.Data;
                        }
                        catch { }
                    }
                }

                var payload = new StockReportRequest
                {
                    items = stockList,
                    LogoImage = logoImage,
                    CompanyName = (string?)(companyInfo?.legal_name),
                    CompanyAddress = (string?)(companyInfo?.address_line1),
                    CompanyContactNo = (string?)(companyInfo?.contact_number),
                    CompanyEmail = (string?)(companyInfo?.contact_email),
                    reporttype = "Stock Master Report",
                    asofdate = date
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/summary/stock-report", content);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.StockReportPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string?> SalesReportPDF(DateTime fromdate, DateTime todate, string tenant_code, string? warehousecode = null)
        {
            try
            {
                string inventoryConn = _config.GetConnectionString("inventory_conn");
                List<SalesReportModel> salesList;
                using (IDbConnection dbInv = new NpgsqlConnection(inventoryConn))
                {
                    string sqlSales = @"
                        SELECT DISTINCT ON (sm.salescode)
                            sm.salescode,
                            sm.billno,
                            sm.billdate,
                            sm.invoiceno,
                            sm.invoicedate,
                            sm.patientid,
                            sm.patientname,
                            sm.salestype,
                            COALESCE(
                                NULLIF(sm.consultant, ''),
                                NULLIF(sm.address, ''),
                                ''
                            ) AS consultant,
                            COALESCE(sm.grossamount, 0) AS grossamount,
                            COALESCE(sm.discountamount, 0) AS discountamount,
                            COALESCE(sm.taxamount, 0) AS taxamount,
                            COALESCE(sm.netamount, 0) AS netamount,
                            sm.paymentmode,
                            sm.paymentstatus,
                            CASE 
                                WHEN LOWER(COALESCE(sm.paymentstatus, '')) = 'paid' THEN COALESCE(sm.netamount, 0) 
                                ELSE 0 
                            END AS paidamount,
                            CASE 
                                WHEN LOWER(COALESCE(sm.paymentstatus, '')) = 'paid' THEN 0 
                                ELSE COALESCE(sm.netamount, 0) 
                            END AS balanceamount,
                            COALESCE(
                                NULLIF(wm.warehousename, ''),
                                NULLIF(wm2.warehousename, ''),
                                NULLIF(sm.warehousefield, ''),
                                'Main Store'
                            ) AS warehousename
                        FROM public.sales_master sm
                        LEFT JOIN public.warehouse_master wm 
                               ON (wm.warehousecode::text = sm.warehousecode::text OR wm.warehousecode::text = sm.warehousefield::text OR wm.warehousename = sm.warehousefield)
                              AND wm.tenantcode = sm.tenantcode
                        LEFT JOIN public.sales_detail sd 
                               ON sd.salescode = sm.salescode AND sd.tenantcode = sm.tenantcode
                        LEFT JOIN public.warehouse_master wm2 
                               ON wm2.warehousecode::text = sd.warehousecode::text AND wm2.tenantcode = sd.tenantcode
                        WHERE COALESCE(sm.deleted, false) = false 
                          AND sm.tenantcode = @tenant_code
                          AND sm.billdate >= @fromdate 
                          AND sm.billdate < @todate + INTERVAL '1 day'
                          AND (
                                COALESCE(@warehousecode, '') = ''
                             OR COALESCE(@warehousecode, '') = 'null'
                             OR COALESCE(@warehousecode, '') = 'undefined'
                             OR COALESCE(@warehousecode, '') = 'all'
                             OR sm.warehousecode::text = @warehousecode
                             OR sm.warehousefield::text = @warehousecode
                             OR sd.warehousecode::text = @warehousecode
                             OR LOWER(wm.warehousename) = LOWER(@warehousecode)
                             OR LOWER(wm2.warehousename) = LOWER(@warehousecode)
                             OR LOWER(wm.shortname) = LOWER(@warehousecode)
                          )
                        ORDER BY sm.salescode, sm.billdate DESC";

                    salesList = (await dbInv.QueryAsync<SalesReportModel>(sqlSales, new { fromdate, todate, tenant_code, warehousecode })).ToList();
                }

                dynamic? companyInfo = null;
                byte[]? logoImage = null;

                using (IDbConnection db = new NpgsqlConnection(_conn))
                {
                    string sqlCompany = @"
                        SELECT legal_name, address_line1, contact_number, contact_email, logo_url
                        FROM mastertenant.tenants
                        WHERE tenant_code = @tenant_code";

                    companyInfo = await db.QueryFirstOrDefaultAsync<dynamic>(sqlCompany, new { tenant_code });
                    if (companyInfo != null && !string.IsNullOrEmpty((string?)companyInfo.logo_url))
                    {
                        try
                        {
                            var logoRes = await _s3Service.DownloadAsync((string)companyInfo.logo_url);
                            if (logoRes.HasValue) logoImage = logoRes.Value.Data;
                        }
                        catch { }
                    }
                }

                var payload = new SalesReportRequest
                {
                    items = salesList,
                    LogoImage = logoImage,
                    CompanyName = (string?)(companyInfo?.legal_name),
                    CompanyAddress = (string?)(companyInfo?.address_line1),
                    CompanyContactNo = (string?)(companyInfo?.contact_number),
                    CompanyEmail = (string?)(companyInfo?.contact_email),
                    fromdate = fromdate,
                    todate = todate,
                    reporttype = "Sales Report"
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/summary/sales-report", content);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Report server error {response.StatusCode}: {error}");
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.SalesReportPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string?> DailyBillReportPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT
                        lrm.requestdatetime                         AS date,
                        COALESCE(cm.custcode, '')                   AS custcode,
                        CONCAT(
                            lrm.name, 
                            CASE 
                                WHEN COALESCE(lrm.ageyears, '') <> '' 
                                THEN CONCAT('-', lrm.ageyears, ' Yrs/', COALESCE(LEFT(lrm.gender, 1), '')) 
                                ELSE '' 
                            END
                        )                                           AS patientname,
                        COALESCE(dm.name, 'Self')                   AS referral,
                        COALESCE(
                            (
                                SELECT STRING_AGG(COALESCE(lrd.item_name, tm.name, ''), ', ')
                                FROM lab_request_details lrd
                                LEFT JOIN test_master tm ON tm.tcode = lrd.tcode
                                WHERE lrd.requestguid = lrm.requestguid
                                  AND COALESCE(lrd.isdeleted, false) = false
                            ),
                            ''
                        )                                           AS testnames,
                        COALESCE(lrm.totalamount, 0)                AS totalamount,
                        COALESCE(lrm.paidamount, 0)                 AS paidamount,
                        COALESCE(lrm.totalamount - COALESCE(lrm.paidamount, 0), 0) AS balanceamount
                    FROM lab_request_master lrm
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = lrm.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode
                    WHERE lrm.tenant_code = @tenant_code
                      AND COALESCE(lrm.deleted, false) = false
                      AND lrm.requestdatetime >= @fromdate
                      AND lrm.requestdatetime < @todate + INTERVAL '1 day'
                    ORDER BY lrm.requestdatetime;
                ";

                var rows = (await db.QueryAsync<DailyBillReportModel>(
                    sql,
                    new { fromdate, todate, tenant_code }))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code });

                var payload = new DailyBillReportRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    reportTitle = "BILLS",
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/Statement/GetDailyBillReport",
                    new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.DailyBillReportPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<List<OpAndIpPatientDto>> GetOpPatientsAsync(
            string tenant_code, 
            DateTime? fromDate = null, 
            DateTime? toDate = null, 
            string? search = null)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT 
                        'OP' AS Type,
                        op.op_id::text AS RequestGuid,
                        COALESCE(NULLIF(op.op_no, ''), op.op_id::text) AS Id,
                        COALESCE(op.custid, 0) AS CustId,
                        COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(op.custid::text, ''), '') AS PatientCode,
                        COALESCE(NULLIF(cm.name, ''), 'Patient') AS PatientName,
                        CONCAT(COALESCE(cm.ageyears, 0), ' Y ', COALESCE(cm.agemonths, 0), ' M') AS Age,
                        COALESCE(NULLIF(cm.gender, ''), '') AS Gender,
                        COALESCE(NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS MobileNo,
                        COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS DoctorName,
                        COALESCE(op.visit_date, NOW()) AS VisitDate,
                        NULL::timestamp AS DischargeDate,
                        '-' AS BedNo
                    FROM op_registration op
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = op.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = op.dcode
                    WHERE (COALESCE(@tenant_code, '') = '' OR op.tenant_code IS NULL OR op.tenant_code = '' OR op.tenant_code = @tenant_code)
                      AND (@fromDate IS NULL OR op.visit_date >= @fromDate)
                      AND (@toDate IS NULL OR op.visit_date <= @toDate)
                      AND (@search IS NULL OR @search = '' 
                           OR LOWER(cm.name) LIKE LOWER(CONCAT('%', @search, '%'))
                           OR LOWER(cm.custcode::text) LIKE LOWER(CONCAT('%', @search, '%'))
                           OR LOWER(op.op_no) LIKE LOWER(CONCAT('%', @search, '%'))
                           OR LOWER(cm.mobile) LIKE LOWER(CONCAT('%', @search, '%')))
                    ORDER BY op.visit_date DESC";

                var list = (await db.QueryAsync<OpAndIpPatientDto>(
                    sql, 
                    new { tenant_code, fromDate, toDate, search }, 
                    commandTimeout: 120)).ToList();

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetOpPatientsAsync: {ex.Message}");
                return new List<OpAndIpPatientDto>();
            }
        }

        public async Task<List<OpAndIpPatientDto>> GetIpPatientsAsync(
            string tenant_code, 
            DateTime? fromDate = null, 
            DateTime? toDate = null, 
            string? search = null)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT 
                        'IP' AS Type,
                        ip.ip_id::text AS RequestGuid,
                        COALESCE(NULLIF(ip.ip_no, ''), ip.ip_id::text) AS Id,
                        COALESCE(ip.custid, 0) AS CustId,
                        COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(ip.custid::text, ''), '') AS PatientCode,
                        COALESCE(NULLIF(cm.name, ''), 'Patient') AS PatientName,
                        CONCAT(COALESCE(cm.ageyears, 0), ' Y ', COALESCE(cm.agemonths, 0), ' M') AS Age,
                        COALESCE(NULLIF(cm.gender, ''), '') AS Gender,
                        COALESCE(NULLIF(cm.mobile, ''), NULLIF(cm.phone, ''), '') AS MobileNo,
                        COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS DoctorName,
                        COALESCE(ip.admitdate, NOW()) AS VisitDate,
                        ip.dischargedate AS DischargeDate,
                        COALESCE(bm.bedname, ip.bedcode::text, '-') AS BedNo
                    FROM ip_registration ip
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = ip.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = ip.dcode
                    LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = ip.tenant_code
                    WHERE (COALESCE(@tenant_code, '') = '' OR ip.tenant_code IS NULL OR ip.tenant_code = '' OR ip.tenant_code = @tenant_code)
                      AND (@fromDate IS NULL OR ip.admitdate >= @fromDate)
                      AND (@toDate IS NULL OR ip.admitdate <= @toDate)
                      AND (@search IS NULL OR @search = '' 
                           OR LOWER(cm.name) LIKE LOWER(CONCAT('%', @search, '%'))
                           OR LOWER(cm.custcode::text) LIKE LOWER(CONCAT('%', @search, '%'))
                           OR LOWER(ip.ip_no) LIKE LOWER(CONCAT('%', @search, '%'))
                           OR LOWER(cm.mobile) LIKE LOWER(CONCAT('%', @search, '%')))
                    ORDER BY ip.admitdate DESC";

                var list = (await db.QueryAsync<OpAndIpPatientDto>(
                    sql, 
                    new { tenant_code, fromDate, toDate, search }, 
                    commandTimeout: 120)).ToList();

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetIpPatientsAsync: {ex.Message}");
                return new List<OpAndIpPatientDto>();
            }
        }

        public async Task<string> GetOpReportPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT 
                        COALESCE(op.visit_date, NOW()) AS date,
                        COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(op.custid::text, ''), '') AS custcode,
                        COALESCE(NULLIF(cm.name, ''), 'Patient') AS patientname,
                        COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS referral,
                        COALESCE(
                            NULLIF(
                                CONCAT_WS(', ',
                                    (
                                        SELECT STRING_AGG(DISTINCT COALESCE(NULLIF(tm.name, ''), NULLIF(dm_uc.doctorfullname, ''), NULLIF(dm_uc.name, ''), uc.tcode::text), ', ')
                                        FROM unbilledcharges uc
                                        LEFT JOIN test_master tm ON tm.tcode::text = uc.tcode::text
                                        LEFT JOIN doctor_master dm_uc ON dm_uc.dcode::text = uc.tcode::text
                                        WHERE uc.opvisitid = op.op_id::text
                                          AND COALESCE(uc.billedstatus, false) = false
                                    ),
                                    (
                                        SELECT STRING_AGG(DISTINCT tm.name, ', ')
                                        FROM lab_request_master lrm
                                        JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
                                        JOIN test_master tm ON tm.tcode = lrd.tcode
                                        WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text)
                                          AND COALESCE(lrm.deleted, false) = false
                                          AND COALESCE(lrd.isdeleted, false) = false
                                    )
                                ), ''
                            ),
                            'OP Consultation & Services'
                        ) AS testnames,
                        COALESCE(
                            NULLIF((
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                    FROM unbilledcharges uc WHERE uc.opvisitid = op.op_id::text
                                    UNION ALL
                                    SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                ) t
                            ), 0),
                            COALESCE(NULLIF(dm.opcharge, 0), 250)
                        ) AS totalamount,
                        COALESCE(
                            (
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                    UNION ALL
                                    SELECT SUM(COALESCE(rm.amountpaid, 0)) AS amt
                                    FROM receipt_master rm WHERE rm.opvisitid = op.op_id::text AND COALESCE(rm.deleted, false) = false
                                ) t
                            ), 0
                        ) AS paidamount,
                        GREATEST(0,
                            COALESCE(
                                NULLIF((
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                        FROM unbilledcharges uc WHERE uc.opvisitid = op.op_id::text
                                        UNION ALL
                                        SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                    ) t
                                ), 0),
                                COALESCE(NULLIF(dm.opcharge, 0), 250)
                            ) - 
                            COALESCE(
                                (
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                        UNION ALL
                                        SELECT SUM(COALESCE(rm.amountpaid, 0)) AS amt
                                        FROM receipt_master rm WHERE rm.opvisitid = op.op_id::text AND COALESCE(rm.deleted, false) = false
                                    ) t
                                ), 0
                            )
                        ) AS balanceamount
                    FROM op_registration op
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = op.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = op.dcode
                    WHERE (COALESCE(@tenant_code, '') = '' OR op.tenant_code IS NULL OR op.tenant_code = '' OR op.tenant_code = @tenant_code)
                      AND op.visit_date >= @fromdate
                      AND op.visit_date < @todate + INTERVAL '1 day'
                      AND COALESCE(op.isdeleted, false) = false
                    ORDER BY op.visit_date;
                ";

                var rows = (await db.QueryAsync<DailyBillReportModel>(
                    sql,
                    new { fromdate, todate, tenant_code },
                    commandTimeout: 120))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code },
                    commandTimeout: 120);

                var payload = new DailyBillReportRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    reportTitle = "OP LIST",
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/Statement/GetDailyBillReport",
                    new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetOpReportPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetIpReportPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT 
                        COALESCE(ip.admitdate, NOW()) AS date,
                        COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(ip.custid::text, ''), '') AS custcode,
                        COALESCE(NULLIF(cm.name, ''), 'Patient') AS patientname,
                        COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS referral,
                        COALESCE(
                            NULLIF(
                                CONCAT_WS(', ',
                                    (
                                        SELECT STRING_AGG(DISTINCT COALESCE(NULLIF(tm.name, ''), NULLIF(rm.name, ''), NULLIF(bm_uc.bedname, ''), uc.tcode::text), ', ')
                                        FROM unbilledcharges uc
                                        LEFT JOIN test_master tm ON tm.tcode::text = uc.tcode::text
                                        LEFT JOIN public.roomtype_master rm ON rm.rmtcode = uc.tcode AND rm.tenant_code = uc.tenant_code
                                        LEFT JOIN public.bed_master bm_uc ON bm_uc.bedcode = uc.bedcode AND bm_uc.tenant_code = uc.tenant_code
                                        WHERE uc.ip_id::text = ip.ip_id::text
                                          AND COALESCE(uc.billedstatus, false) = false
                                    ),
                                    (
                                        SELECT STRING_AGG(DISTINCT tm.name, ', ')
                                        FROM lab_request_master lrm
                                        JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
                                        JOIN test_master tm ON tm.tcode = lrd.tcode
                                        WHERE lrm.ip_id::text = ip.ip_id::text
                                          AND COALESCE(lrm.deleted, false) = false
                                          AND COALESCE(lrd.isdeleted, false) = false
                                    )
                                ), ''
                            ),
                            CONCAT_WS(' - ', 'IP Services & Room', NULLIF(bm.bedname, ''))
                        ) AS testnames,
                        COALESCE(
                            NULLIF((
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                    FROM unbilledcharges uc WHERE uc.ip_id::text = ip.ip_id::text
                                    UNION ALL
                                    SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                ) t
                            ), 0),
                            COALESCE(ip.insurance_approved_amount, 1000)
                        ) AS totalamount,
                        COALESCE(
                            (
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                    UNION ALL
                                    SELECT SUM(COALESCE(ra.advanceamount, 0)) AS amt
                                    FROM receipt_advances ra WHERE ra.custid = ip.custid AND COALESCE(ra.deleted, false) = false
                                ) t
                            ), 0
                        ) AS paidamount,
                        GREATEST(0,
                            COALESCE(
                                NULLIF((
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                        FROM unbilledcharges uc WHERE uc.ip_id::text = ip.ip_id::text
                                        UNION ALL
                                        SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                    ) t
                                ), 0),
                                COALESCE(ip.insurance_approved_amount, 1000)
                            ) - 
                            COALESCE(
                                (
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                        UNION ALL
                                        SELECT SUM(COALESCE(ra.advanceamount, 0)) AS amt
                                        FROM receipt_advances ra WHERE ra.custid = ip.custid AND COALESCE(ra.deleted, false) = false
                                    ) t
                                ), 0
                            )
                        ) AS balanceamount
                    FROM ip_registration ip
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = ip.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = ip.dcode
                    LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = ip.tenant_code
                    WHERE (COALESCE(@tenant_code, '') = '' OR ip.tenant_code IS NULL OR ip.tenant_code = '' OR ip.tenant_code = @tenant_code)
                      AND ip.admitdate >= @fromdate
                      AND ip.admitdate < @todate + INTERVAL '1 day'
                      AND COALESCE(ip.deleted, false) = false
                    ORDER BY ip.admitdate;
                ";

                var rows = (await db.QueryAsync<DailyBillReportModel>(
                    sql,
                    new { fromdate, todate, tenant_code },
                    commandTimeout: 120))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code },
                    commandTimeout: 120);

                var payload = new DailyBillReportRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    reportTitle = "IP LIST",
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/Statement/GetDailyBillReport",
                    new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetIpReportPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetDoctorwiseOpReportPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT 
                        COALESCE(op.visit_date, NOW()) AS date,
                        COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(op.custid::text, ''), '') AS custcode,
                        COALESCE(NULLIF(cm.name, ''), 'Patient') AS patientname,
                        COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS referral,
                        COALESCE(
                            NULLIF(
                                CONCAT_WS(', ',
                                    (
                                        SELECT STRING_AGG(DISTINCT COALESCE(NULLIF(tm.name, ''), NULLIF(dm_uc.doctorfullname, ''), NULLIF(dm_uc.name, ''), uc.tcode::text), ', ')
                                        FROM unbilledcharges uc
                                        LEFT JOIN test_master tm ON tm.tcode::text = uc.tcode::text
                                        LEFT JOIN doctor_master dm_uc ON dm_uc.dcode::text = uc.tcode::text
                                        WHERE uc.opvisitid = op.op_id::text
                                          AND COALESCE(uc.billedstatus, false) = false
                                    ),
                                    (
                                        SELECT STRING_AGG(DISTINCT tm.name, ', ')
                                        FROM lab_request_master lrm
                                        JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
                                        JOIN test_master tm ON tm.tcode = lrd.tcode
                                        WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text)
                                          AND COALESCE(lrm.deleted, false) = false
                                          AND COALESCE(lrd.isdeleted, false) = false
                                    )
                                ), ''
                            ),
                            'OP Consultation & Services'
                        ) AS testnames,
                        COALESCE(
                            NULLIF((
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                    FROM unbilledcharges uc WHERE uc.opvisitid = op.op_id::text
                                    UNION ALL
                                    SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                ) t
                            ), 0),
                            COALESCE(NULLIF(dm.opcharge, 0), 250)
                        ) AS totalamount,
                        COALESCE(
                            (
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                    UNION ALL
                                    SELECT SUM(COALESCE(rm.amountpaid, 0)) AS amt
                                    FROM receipt_master rm WHERE rm.opvisitid = op.op_id::text AND COALESCE(rm.deleted, false) = false
                                ) t
                            ), 0
                        ) AS paidamount,
                        GREATEST(0,
                            COALESCE(
                                NULLIF((
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                        FROM unbilledcharges uc WHERE uc.opvisitid = op.op_id::text
                                        UNION ALL
                                        SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                    ) t
                                ), 0),
                                COALESCE(NULLIF(dm.opcharge, 0), 250)
                            ) - 
                            COALESCE(
                                (
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE (lrm.opvisitid = op.op_id::text OR lrm.sheet_id = op.op_id::text) AND COALESCE(lrm.deleted, false) = false
                                        UNION ALL
                                        SELECT SUM(COALESCE(rm.amountpaid, 0)) AS amt
                                        FROM receipt_master rm WHERE rm.opvisitid = op.op_id::text AND COALESCE(rm.deleted, false) = false
                                    ) t
                                ), 0
                            )
                        ) AS balanceamount
                    FROM op_registration op
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = op.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = op.dcode
                    WHERE (COALESCE(@tenant_code, '') = '' OR op.tenant_code IS NULL OR op.tenant_code = '' OR op.tenant_code = @tenant_code)
                      AND op.visit_date >= @fromdate
                      AND op.visit_date < @todate + INTERVAL '1 day'
                      AND COALESCE(op.isdeleted, false) = false
                    ORDER BY dm.doctorfullname, op.visit_date;
                ";

                var rows = (await db.QueryAsync<DailyBillReportModel>(
                    sql,
                    new { fromdate, todate, tenant_code },
                    commandTimeout: 120))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code },
                    commandTimeout: 120);

                var payload = new DailyBillReportRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    reportTitle = "Doctor OP List",
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/Statement/GetDailyBillReport",
                    new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetDoctorwiseOpReportPDF: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetDoctorwiseIpReportPDF(DateTime fromdate, DateTime todate, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                string sql = @"
                    SELECT 
                        COALESCE(ip.admitdate, NOW()) AS date,
                        COALESCE(NULLIF(cm.custcode::text, ''), NULLIF(ip.custid::text, ''), '') AS custcode,
                        COALESCE(NULLIF(cm.name, ''), 'Patient') AS patientname,
                        COALESCE(NULLIF(dm.doctorfullname, ''), NULLIF(dm.name, ''), 'SELF') AS referral,
                        COALESCE(
                            NULLIF(
                                CONCAT_WS(', ',
                                    (
                                        SELECT STRING_AGG(DISTINCT COALESCE(NULLIF(tm.name, ''), NULLIF(rm.name, ''), NULLIF(bm_uc.bedname, ''), uc.tcode::text), ', ')
                                        FROM unbilledcharges uc
                                        LEFT JOIN test_master tm ON tm.tcode::text = uc.tcode::text
                                        LEFT JOIN public.roomtype_master rm ON rm.rmtcode = uc.tcode AND rm.tenant_code = uc.tenant_code
                                        LEFT JOIN public.bed_master bm_uc ON bm_uc.bedcode = uc.bedcode AND bm_uc.tenant_code = uc.tenant_code
                                        WHERE uc.ip_id::text = ip.ip_id::text
                                          AND COALESCE(uc.billedstatus, false) = false
                                    ),
                                    (
                                        SELECT STRING_AGG(DISTINCT tm.name, ', ')
                                        FROM lab_request_master lrm
                                        JOIN lab_request_details lrd ON lrd.requestguid = lrm.requestguid
                                        JOIN test_master tm ON tm.tcode = lrd.tcode
                                        WHERE lrm.ip_id::text = ip.ip_id::text
                                          AND COALESCE(lrm.deleted, false) = false
                                          AND COALESCE(lrd.isdeleted, false) = false
                                    )
                                ), ''
                            ),
                            CONCAT_WS(' - ', 'IP Services & Room', NULLIF(bm.bedname, ''))
                        ) AS testnames,
                        COALESCE(
                            NULLIF((
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                    FROM unbilledcharges uc WHERE uc.ip_id::text = ip.ip_id::text
                                    UNION ALL
                                    SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                ) t
                            ), 0),
                            COALESCE(ip.insurance_approved_amount, 1000)
                        ) AS totalamount,
                        COALESCE(
                            (
                                SELECT SUM(amt) FROM (
                                    SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                    FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                    UNION ALL
                                    SELECT SUM(COALESCE(ra.advanceamount, 0)) AS amt
                                    FROM receipt_advances ra WHERE ra.custid = ip.custid AND COALESCE(ra.deleted, false) = false
                                ) t
                            ), 0
                        ) AS paidamount,
                        GREATEST(0,
                            COALESCE(
                                NULLIF((
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(CASE WHEN uc.amount = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) AS amt
                                        FROM unbilledcharges uc WHERE uc.ip_id::text = ip.ip_id::text
                                        UNION ALL
                                        SELECT SUM(COALESCE(lrm.totalamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                    ) t
                                ), 0),
                                COALESCE(ip.insurance_approved_amount, 1000)
                            ) - 
                            COALESCE(
                                (
                                    SELECT SUM(amt) FROM (
                                        SELECT SUM(COALESCE(lrm.paidamount, 0)) AS amt
                                        FROM lab_request_master lrm WHERE lrm.ip_id::text = ip.ip_id::text AND COALESCE(lrm.deleted, false) = false
                                        UNION ALL
                                        SELECT SUM(COALESCE(ra.advanceamount, 0)) AS amt
                                        FROM receipt_advances ra WHERE ra.custid = ip.custid AND COALESCE(ra.deleted, false) = false
                                    ) t
                                ), 0
                            )
                        ) AS balanceamount
                    FROM ip_registration ip
                    LEFT JOIN customerdb.customer_master cm ON cm.custid = ip.custid
                    LEFT JOIN doctor_master dm ON dm.dcode = ip.dcode
                    LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = ip.tenant_code
                    WHERE (COALESCE(@tenant_code, '') = '' OR ip.tenant_code IS NULL OR ip.tenant_code = '' OR ip.tenant_code = @tenant_code)
                      AND ip.admitdate >= @fromdate
                      AND ip.admitdate < @todate + INTERVAL '1 day'
                      AND COALESCE(ip.deleted, false) = false
                    ORDER BY dm.doctorfullname, ip.admitdate;
                ";

                var rows = (await db.QueryAsync<DailyBillReportModel>(
                    sql,
                    new { fromdate, todate, tenant_code },
                    commandTimeout: 120))
                    .ToList();

                var companyInfo = await db.QueryFirstOrDefaultAsync<Tenant>(
                    @"SELECT legal_name, address_line1, contact_number, contact_email
                      FROM mastertenant.tenants
                      WHERE tenant_code = @tenant_code",
                    new { tenant_code },
                    commandTimeout: 120);

                var payload = new DailyBillReportRequest
                {
                    statements = rows,
                    fromdate = fromdate,
                    todate = todate,
                    reportTitle = "Doctor IP List",
                    CompanyName = companyInfo?.legal_name,
                    CompanyAddress = companyInfo?.address_line1,
                    CompanyContactNo = companyInfo?.contact_number,
                    CompanyEmail = companyInfo?.contact_email
                };

                var client = _httpClientFactory.CreateClient("ReportServer");
                var response = await client.PostAsync(
                    "/api/Statement/GetDailyBillReport",
                    new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"));

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetDoctorwiseIpReportPDF: {ex.Message}");
                throw;
            }
        }
        public async Task<string?> GetPharmacyBillAsync(string billNo, string tenantCode, string orientation = "portrait")
        {
            try
            {
                var inventoryConn = _config.GetConnectionString("inventory_conn");
                if (string.IsNullOrEmpty(inventoryConn))
                    throw new Exception("Inventory connection string is not configured.");

                using IDbConnection db = new NpgsqlConnection(inventoryConn);
                
                var response = new PharmacyBillResponse();

                // Get Header & Footer preferences from main DB
                using (IDbConnection mainDb = new NpgsqlConnection(_conn))
                {
                    var labSettings = await mainDb.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT show_pharmacy_header_footer_image FROM lab_settings WHERE tenant_code = @tenantCode",
                        new { tenantCode });

                    response.ShowHeaderFooter = labSettings?.show_pharmacy_header_footer_image ?? true;

                    if (response.ShowHeaderFooter)
                    {
                        var tenant = await mainDb.QueryFirstOrDefaultAsync<dynamic>(
                            "SELECT logo_url FROM mastertenant.tenants WHERE tenant_code = @tenantCode",
                            new { tenantCode });

                        response.HeaderImagePath = tenant?.logo_url;
                        response.FooterImagePath = tenant?.footer_url;
                    }
                    
                    var companyInfo = await mainDb.QueryFirstOrDefaultAsync<dynamic>(@"
                        SELECT
                            legal_name,
                            COALESCE(address_line1,  '') AS address_line1,
                            COALESCE(contact_number, '') AS contact_number,
                            COALESCE(contact_email,  '') AS contact_email,
                            COALESCE(gst_number,     '') AS gst_number
                        FROM mastertenant.tenants
                        WHERE tenant_code = @tenantCode", 
                        new { tenantCode });
                        
                    response.LabName = companyInfo?.legal_name ?? string.Empty;
                    response.BranchName = ""; // Or fetch branch name if necessary
                    response.Address = companyInfo?.address_line1 ?? string.Empty;
                    response.MobileNo = companyInfo?.contact_number ?? string.Empty;
                    response.Email = companyInfo?.contact_email ?? string.Empty;
                    response.GSTNo = companyInfo?.gst_number ?? string.Empty;
                    response.logo = null;
                    response.isletterhead = labSettings?.show_pharmacy_header_footer_image ?? true;
                    
                    response.billauthorizedby = response.LabName;
                    response.billauthorizesignature = null;
                    response.Orientation = orientation;
                }

                // Query Sales Master
                string sqlMaster = @"
                    SELECT 
                        salescode, billno, billdate, invoiceno, invoicedate, customercode, 
                        grossamount, discountamount, taxamount, netamount, paymentmode, 
                        paymentstatus, currencycode, isactive, deleted, remarks, createddate, 
                        modifieddate, usercode, tenantcode, branchcode, companycode, ordercode, 
                        salestype, warehousecode, warehousefield, patientid, patientname, address, consultant
                    FROM sales_master
                    WHERE billno = @billNo AND tenantcode = @tenantCode AND COALESCE(deleted, false) = false";

                var master = await db.QueryFirstOrDefaultAsync<SalesMasterModel>(sqlMaster, new { billNo, tenantCode });
                
                if (master == null)
                    return null;

                response.BillDetails = master;

                // Query Sales Details
                string sqlDetails = @"
                    SELECT 
                        sd.salesdetailcode, sd.salescode, sd.itemcode, sd.quantity, sd.freequantity, 
                        sd.uomcode, sd.rate, sd.discountpercentage, sd.discountamount, sd.taxpercentage, 
                        sd.taxamount, sd.amount, sd.totalamount, sd.batchno, sd.manufacturingdate, 
                        sd.expirydate, sd.warehousecode, sd.tenantcode, sd.soldqty, sd.returnedqty,
                        im.itemname
                    FROM sales_detail sd
                    LEFT JOIN item_master im ON sd.itemcode = im.itemcode
                    WHERE sd.salescode = @SalesCode AND sd.tenantcode = @tenantCode";

                var details = await db.QueryAsync<SalesDetailModel>(sqlDetails, new { SalesCode = master.salescode, tenantCode });
                response.Items = details.ToList();

                var json = JsonSerializer.Serialize(response);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var apiResponse = await client.PostAsync("/api/Statement/GetPharmacyBill", content);
                
                apiResponse.EnsureSuccessStatusCode();
                return await apiResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReportClass.GetPharmacyBillAsync: {ex.Message}");
                throw;
            }
        }

        private static readonly int[] Code128BEncoding = new[]
        {
            212222, 222122, 222221, 121223, 121322, 131222, 122213, 122312, 132212, 221213,
            221312, 231212, 112232, 122132, 122231, 113222, 123122, 123221, 223211, 221132,
            221231, 213212, 223112, 312131, 311222, 321122, 321221, 312212, 322112, 322211,
            212123, 212321, 232121, 111323, 131123, 131321, 112313, 132113, 132311, 211313,
            231113, 231311, 112133, 112331, 132131, 113123, 113321, 133121, 313121, 211331,
            231131, 213113, 213311, 213131, 311123, 311321, 331121, 312113, 312311, 332111,
            314111, 221411, 431111, 111224, 111422, 121124, 121421, 141122, 141221, 112214,
            112412, 122114, 122411, 142112, 142211, 241211, 221114, 213114, 213411, 221141,
            413111, 141113, 141311, 311141, 411113, 411311, 113141, 114131, 311411, 341111,
            111143, 111341, 131141, 114113, 114311, 411131, 211412, 211214, 211232, 233111,
            200000, 211112, 211211
        };

        private static readonly int[] Code128Special =
        {
            211412,
            2331112
        };

        private static byte[] GenerateBarcodePng(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return GenerateBlankPng();

            try
            {
                var bars = new System.Collections.Generic.List<int>();

                AppendBarPattern(bars, Code128Special[0]);

                int checksum = 104;
                for (int i = 0; i < text.Length; i++)
                {
                    int code = text[i] - 32;
                    if (code < 0 || code >= Code128BEncoding.Length) code = 0;
                    checksum += code * (i + 1);
                    AppendBarPattern(bars, Code128BEncoding[code]);
                }

                int checkIdx = Math.Abs(checksum % 103);
                if (checkIdx >= Code128BEncoding.Length) checkIdx = 0;
                AppendBarPattern(bars, Code128BEncoding[checkIdx]);

                AppendBarPattern(bars, Code128Special[1]);

                const int barUnit = 3;
                const int quietZone = 14;
                const int barcodeH = 80;

                int totalBarWidth = bars.Sum() * barUnit;
                int imgW = Math.Max(totalBarWidth + quietZone * 2, 100);

                using var bitmap = new SkiaSharp.SKBitmap(imgW, barcodeH);
                using var canvas = new SkiaSharp.SKCanvas(bitmap);
                canvas.Clear(SkiaSharp.SKColors.White);

                using var blackPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, IsAntialias = false };

                int x = quietZone;
                for (int i = 0; i < bars.Count; i++)
                {
                    int w = bars[i] * barUnit;
                    if (i % 2 == 0)
                        canvas.DrawRect(x, 0, w, barcodeH, blackPaint);
                    x += w;
                }

                using var img = SkiaSharp.SKImage.FromBitmap(bitmap);
                using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                return data?.ToArray() ?? GenerateBlankPng();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenerateBarcodePng] Error generating barcode for '{text}': {ex.Message}");
                return GenerateBlankPng();
            }
        }

        private static void AppendBarPattern(System.Collections.Generic.List<int> bars, int pattern)
        {
            string s = pattern.ToString();
            foreach (char c in s)
                bars.Add(c - '0');
        }

        private static byte[] GenerateBlankPng()
        {
            using var bmp = new SkiaSharp.SKBitmap(1, 1);
            using var img = SkiaSharp.SKImage.FromBitmap(bmp);
            using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

    }
}
