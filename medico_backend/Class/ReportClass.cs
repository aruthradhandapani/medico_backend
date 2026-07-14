using Amazon.Runtime;
using Amazon.S3.Model;
using Dapper;
using medico_backend.Model;
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

        public async Task<string> BillPDF(Guid requestguid, string tenant_code, bool? isletterhead = false)
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
                lrm.requestdatetime::timestamp                               AS BillDate,

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
                COALESCE(lrm.entereddate, lrm.requestdatetime)::timestamp    AS CreatedTime,

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

        private static RoutineReportModel MapRow(
            RawReportRow r, Dictionary<string, byte[]> cache) => new()
            {
                TenantId = r.TenantId,
                RequestSno = r.RequestSno,
                RequestBarCode = string.IsNullOrWhiteSpace(r.RequestBarCode)
                    ? GenerateBlankPng()
                    : GenerateBarcodePng(r.RequestBarCode),
                RequestDateTime = r.RequestDateTime,
                RequestedDateTime = r.RequestedDateTime,
                Name = r.Name,
                Gender = r.Gender,
                DateofBirth = r.DateofBirth,
                AgeYears = r.AgeYears,
                AgeMonths = r.AgeMonths,
                AgeDays = r.AgeDays,
                Address = r.Address,
                MobileNo = r.MobileNo,
                RequestAmount = r.RequestAmount,
                Description = r.Description,
                CustCode = r.CustCode,
                Sample = r.Sample,
                TestName = r.TestName,
                GroupName = r.GroupName,
                Doctor = r.Doctor,
                Initial = r.Initial,
                NameTitle = r.NameTitle,
                Reference = r.Reference,
                DoctorCode = r.DoctorCode,
                EnteredResult = r.EnteredResult,
                Reporting = r.Reporting,
                CityName = r.CityName,
                AreaPinCode = r.AreaPinCode,
                AreaName = r.AreaName,
                RequestGUID = r.RequestGUID,
                TestSno = r.TestSno,
                Col2 = r.Col2,
                Units = (!string.IsNullOrWhiteSpace(r.Units) ? r.Units : r.TRPUName) ?? "",
                ResultSno = r.ResultSno,
                CustomerImage = GetImage(r.CustomerImage, cache),
                SignatureImage = GetImage(r.SignatureImage, cache),
                ResultGUID = r.ResultGUID,
                ValueType = r.ValueType,
                TCode = r.TCode,
                ResultDateTime = r.ResultDateTime,
                ResultType = r.ResultType,
                PrintInSeparatePage = r.PrintInSeparatePage,
                TestOrderNo = r.TestOrderNo,
                GroupOrderNo = r.GroupOrderNo,
                RoomNo = r.RoomNo,
                HospitalID = r.HospitalID,
                Email = r.Email,
                AlteredBHCode = r.AlteredBHCode,
                CollectedDateTime = r.CollectedDateTime,
                OnlineCode = r.OnlineCode,
                ResultValueType = r.ResultValueType,
                DefaultValue = r.DefaultValue,
                SimpleNormalValues = r.SimpleNormalValues,
                DetailedNormalValues = r.DetailedNormalValues,
                RangeType = r.RangeType,
                FromNormalValue = r.FromNormalValue,
                ToNormalValue = r.ToNormalValue,
                ConclusionForHigher = r.ConclusionForHigher,
                ConclusionForLower = r.ConclusionForLower,
                ShowAgedBased = r.ShowAgedBased,
                ShowAlertOnHigherLower = r.ShowAlertOnHigherLower,
                FooterMessage = r.FooterMessage,
                TRPUName = r.TRPUName,
                FixedValues = r.FixedValues,
                DecimalPlaces = r.DecimalPlaces,
                ReportingMethod = r.ReportingMethod,
                TestResultID = r.TestResultID,
                RequestSnoPrint = r.RequestSnoPrint,
                PrintResultOnly = r.PrintResultOnly,
                ResultNormal = r.ResultNormal,
                ResultHigh = r.ResultHigh,
                ResultLow = r.ResultLow,
                IsInvestigationPartial = r.IsInvestigationPartial,
                ResultSample = r.ResultSample,
                DoctorFullName = r.DoctorFullName,
                DCode = r.DCode,
                FrontHospitalID = r.FrontHospitalID,
                FrontHospitalPatientID = r.FrontHospitalPatientID,
                IsAuthorized1 = r.IsAuthorized1,
                DoctorTitle = r.DoctorTitle,
                SecondDoctorName = r.SecondDoctorName,
                SecondDCode = r.SecondDCode,
                DefaultAuthorizeImage = GetImage(r.DefaultAuthorizeImage, cache),
                DefaultAuthorizeName = r.DefaultAuthorizeName,
                DefaultAuthorizeDesignation = r.DefaultAuthorizeDesignation,
                NormalValues = r.NormalValues,
                RowNum = r.RowNum,
                MCCode = r.MCCode,
            };

        private static AuthorizedUser MapAuth(
            RawAuthUser a, Dictionary<string, byte[]> cache) => new()
            {
                EnteredSign = GetImage(a.EnteredSign, cache) ?? GenerateBlankPng(),
                EnteredBy = a.EnteredBy,
                EnteredByDesignation = a.EnteredByDesignation,
                AuthorizedSign = GetImage(a.AuthorizedSign, cache) ?? GenerateBlankPng(),
                AuthorizedBy = a.AuthorizedBy,
                AuthorizedByDesignation = a.AuthorizedByDesignation,
                AuthorizedSign2 = GetImage(a.AuthorizedSign2, cache) ?? GenerateBlankPng(),
                AuthorizedBy2 = a.AuthorizedBy2,
                AuthorizedByDesignation2 = a.AuthorizedByDesignation2,
            };

        public async Task<string?> GetLabReportAsync(Guid requestguid, string tenant_code, bool? isletterhead)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                // ── Step 1: Result rows (all image columns as text paths) ─────────────
                const string resultSql = @"
SELECT
    -- ── Request / Patient ─────────────────────────────────────────────────
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
    CASE WHEN COALESCE(lrm.ageyears::int, 0) = 0 AND COALESCE(lrm.agemonths::int, 0) = 0 AND COALESCE(lrm.agedays::int, 0) = 0 THEN COALESCE(cm.ageyears, 0) ELSE COALESCE(lrm.ageyears::int, 0) END AS AgeYears,
    CASE WHEN COALESCE(lrm.ageyears::int, 0) = 0 AND COALESCE(lrm.agemonths::int, 0) = 0 AND COALESCE(lrm.agedays::int, 0) = 0 THEN COALESCE(cm.agemonths, 0) ELSE COALESCE(lrm.agemonths::int, 0) END AS AgeMonths,
    CASE WHEN COALESCE(lrm.ageyears::int, 0) = 0 AND COALESCE(lrm.agemonths::int, 0) = 0 AND COALESCE(lrm.agedays::int, 0) = 0 THEN COALESCE(cm.agedays, 0) ELSE COALESCE(lrm.agedays::int, 0) END AS AgeDays,
    COALESCE(lrm.address,  '')                                          AS Address,
    COALESCE(lrm.mobileno, '')                                          AS MobileNo,
    COALESCE(lrm.requestamount, 0)::float8                              AS RequestAmount,

    -- ── Test / Result ──────────────────────────────────────────────────────
    COALESCE(lrdd.description,    '')                                   AS Description,
    COALESCE(cm.custcode,         '')                                   AS CustCode,
    COALESCE(sm.name,             '')                                   AS Sample,
    COALESCE(tm.name,             '')                                   AS TestName,
    COALESCE(gm.name,             '')                                   AS GroupName,

    -- ── Doctor ────────────────────────────────────────────────────────────
    COALESCE(dm.name,      '')                                          AS Doctor,
    COALESCE(dm.initial,   '')                                          AS Initial,
    COALESCE(dm.nametitle, '')                                          AS NameTitle,
    ''                                                                  AS Reference,
    COALESCE(lrm.dcode::text, '')                                       AS DoctorCode,

    COALESCE(lrdd.enteredresult, '')                                    AS EnteredResult,
    COALESCE(rtm.name,           '')                                    AS Reporting,

    -- ── Area ──────────────────────────────────────────────────────────────
    ''                                                                  AS CityName,
    COALESCE(ar.areapincode, '')                                        AS AreaPinCode,
    COALESCE(ar.areaname,    '')                                        AS AreaName,

    -- ── Identifiers ───────────────────────────────────────────────────────
    lrm.requestguid::text                                               AS RequestGUID,
    COALESCE(lrd.testsno,       0)                                      AS TestSno,
    COALESCE(lrdd.quotescolumn, '')                                     AS Col2,
    COALESCE(
        NULLIF(lrdd.units, ''),
        NULLIF(uom.name,   ''),
        ''
    )                                                                   AS Units,
    COALESCE(lrdd.testsno,      0)                                      AS ResultSno,

    -- ── Image columns → MinIO paths (string), NOT bytea ───────────────────
    NULL::text                                                          AS CustomerImage,
    NULL::text                                                          AS SignatureImage,

    COALESCE(lrm_res.resultguid, '')                                    AS ResultGUID,
    COALESCE(lrdd.valuetype,     '')                                    AS ValueType,
    COALESCE(lrd.tcode::int,      0)                                    AS TCode,
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
    COALESCE(lrm.alteredbhcode::text, '')                               AS AlteredBHCode,
    COALESCE(sc.collectedtime, lrm.collecteddatetime)                   AS CollectedDateTime,
    COALESCE(lrm.onlinecode, '')                                        AS OnlineCode,

    -- ── Result properties ─────────────────────────────────────────────────
    COALESCE(lrp.resultvaluetype,        '')                            AS ResultValueType,
    COALESCE(lrp.defaultvalue,           '')                            AS DefaultValue,
    COALESCE(lrp.simplenormalvalues,   false)                           AS SimpleNormalValues,
    COALESCE(lrp.detailednormalvalues, false)                           AS DetailedNormalValues,
    COALESCE(lrp.rangetype,              '')                            AS RangeType,

    COALESCE(
        CASE WHEN lrp.detailednormalvalues = true
             THEN COALESCE(dnv.rangefrom, dnv_master.rangefrom)
        END,
        lrp.fromnormalvalue,
        0
    )::float8                                                          AS FromNormalValue,
    COALESCE(
        CASE WHEN lrp.detailednormalvalues = true
             THEN COALESCE(dnv.rangeto, dnv_master.rangeto)
        END,
        lrp.tonormalvalue,
        0
    )::float8                                                          AS ToNormalValue,

    COALESCE(lrp.conclusionforhigher,    '')                            AS ConclusionForHigher,
    COALESCE(lrp.conclusionforlower,     '')                            AS ConclusionForLower,
    COALESCE(lrp.showagedbased,        false)                           AS ShowAgedBased,
    COALESCE(lrp.showalertonhigherlower,false)                          AS ShowAlertOnHigherLower,
    COALESCE(lrp.footermessage,          '')                            AS FooterMessage,
    COALESCE(uom.name,                   '')                            AS TRPUName,

    COALESCE(lrp.decimalvalue,            0)                            AS DecimalPlaces,
    COALESCE(rtm_master.name, rtm.name, '')                             AS ReportingMethod,
    COALESCE(lrdd.testresultid,
        '00000000-0000-0000-0000-000000000000'::uuid)                   AS TestResultID,

    COALESCE(lrm.requestsnoprint, '')                                   AS RequestSnoPrint,
    COALESCE(lrp.printresultonly, false)                                AS PrintResultOnly,
    COALESCE(lrp.resultnormal,    false)                                AS ResultNormal,
    COALESCE(lrp.resulthigh,      false)                                AS ResultHigh,
    COALESCE(lrp.resultlow,       false)                                AS ResultLow,
    COALESCE(lrm.isinvestigationpartial, false)                         AS IsInvestigationPartial,

    COALESCE(sm.name,  '')                                              AS ResultSample,
    COALESCE(dm.name,  '')                                              AS DoctorFullName,
    COALESCE(lrm.dcode::int,  0)                                        AS DCode,
    COALESCE(lrm.fronthospitalid,        '')                            AS FrontHospitalID,
    COALESCE(lrm.fronthospitalpatientid, '')                            AS FrontHospitalPatientID,
    COALESCE(lrd.isauthorized1, false)                                  AS IsAuthorized1,
    COALESCE(dm.nametitle,  '')                                         AS DoctorTitle,
    COALESCE(lrm.seconddoctorname, '')                                  AS SecondDoctorName,
    COALESCE(lrm.seconddcode::int,  0)                                  AS SecondDCode,

    -- ── Authorizer signature → MinIO path string ──────────────────────────
    au.signature_image                                                  AS DefaultAuthorizeImage,
    COALESCE(au.name,        '')                                        AS DefaultAuthorizeName,
    COALESCE(au.description, '')                                        AS DefaultAuthorizeDesignation,

    COALESCE(
        NULLIF(lrdd.normalvalues, ''),
        NULLIF(
            CONCAT_WS(', ',
                -- a) ALL matching detailed age bands — result-level, else master-level
                CASE
                    WHEN lrp.detailednormalvalues = true
                    THEN COALESCE(dnv_all.fulltext, dnv_all_master.fulltext)
                    ELSE NULL
                END,
                -- b) sex-specific text normal value — result-level, else master-level
                CASE
                    WHEN lrp.simplenormalvalues = true
                    THEN NULLIF(COALESCE(tnv.normalvalue, tnv_master.normalvalue), '')
                    ELSE NULL
                END
                
            ),
            ''
        ),
        NULLIF(lrp.normalvalue, ''),
        ''
    )                                                                   AS NormalValues,

    ROW_NUMBER() OVER (
        ORDER BY COALESCE(gm.orderno, 9999),
                 COALESCE(tm.orderno, 9999),
                 COALESCE(lrd.testsno, 0),
                 COALESCE(lrdd.testsno, 0)
    )::int                                                              AS RowNum,
    COALESCE(lrp.mccode, 0)                                             AS MCCode

FROM lab_request_master lrm

-- customer_master (cross-schema)
LEFT JOIN customerdb.customer_master cm
       ON cm.custid      = lrm.custid

-- doctor_master
LEFT JOIN doctor_master dm
       ON dm.dcode       = lrm.dcode
      AND dm.tenant_code = lrm.tenant_code

-- lab_request_details — investigation rows only (ttid = 1)
LEFT JOIN lab_request_details lrd
       ON lrd.requestguid = lrm.requestguid
      AND lrd.tenant_code = lrm.tenant_code
      AND (lrd.ttid = 1 OR lrd.ttid IS NULL)

-- test_master
LEFT JOIN test_master tm
       ON tm.tcode       = lrd.tcode
      AND tm.tenant_code = lrm.tenant_code

-- group_master
LEFT JOIN group_master gm
       ON gm.gcode       = tm.gcode
      AND gm.tenant_code = lrm.tenant_code

-- lab_result_master: one per request
LEFT JOIN lab_result_master lrm_res
       ON lrm_res.requestguid::text = lrm.requestguid::text
      AND COALESCE(lrm_res.deleted, false) = false

-- lab_result_details: match by tcode within the result set for this request
LEFT JOIN lab_result_details lrdd
       ON lrdd.resultguid  = lrm_res.resultguid
      AND lrdd.tcode       = lrd.tcode

-- lab_result_properties
LEFT JOIN lab_result_properties lrp
       ON lrp.testresultid = lrdd.testresultid

-- Patient age in days — computed once, reused by every age-matching lateral below
LEFT JOIN LATERAL (
    SELECT
        (CASE WHEN COALESCE(lrm.ageyears::int,0)=0 AND COALESCE(lrm.agemonths::int,0)=0 AND COALESCE(lrm.agedays::int,0)=0
              THEN COALESCE(cm.ageyears,0) ELSE COALESCE(lrm.ageyears::int,0) END) * 365
      + (CASE WHEN COALESCE(lrm.ageyears::int,0)=0 AND COALESCE(lrm.agemonths::int,0)=0 AND COALESCE(lrm.agedays::int,0)=0
              THEN COALESCE(cm.agemonths,0) ELSE COALESCE(lrm.agemonths::int,0) END) * 30
      + (CASE WHEN COALESCE(lrm.ageyears::int,0)=0 AND COALESCE(lrm.agemonths::int,0)=0 AND COALESCE(lrm.agedays::int,0)=0
              THEN COALESCE(cm.agedays,0) ELSE COALESCE(lrm.agedays::int,0) END)
        AS age_in_days
) page ON true

-- ════════════════════════════════════════════════════════════════════════
-- DETAILED NORMAL VALUES 
-- ════════════════════════════════════════════════════════════════════════

-- (1) Result-level
LEFT JOIN LATERAL (
    SELECT dnv.rangefrom, dnv.rangeto, dnv.rangetype, dnv.sex
    FROM lab_result_detailednormalvalues dnv
    WHERE dnv.testresultid = lrdd.testresultid
      AND (
            dnv.sex IS NULL OR dnv.sex = '' OR
            UPPER(dnv.sex) = UPPER(lrm.gender) OR
            UPPER(dnv.sex) IN ('BOTH','ALL')
          )
      AND page.age_in_days BETWEEN
            COALESCE(dnv.agefrom,0) * CASE LOWER(COALESCE(dnv.agefromtype,'years'))
                                            WHEN 'year'   THEN 365 WHEN 'years'  THEN 365
                                            WHEN 'month'  THEN 30  WHEN 'months' THEN 30
                                            WHEN 'week'   THEN 7   WHEN 'weeks'  THEN 7
                                            WHEN 'day'    THEN 1   WHEN 'days'   THEN 1
                                            ELSE 365 END
          AND
            COALESCE(dnv.ageto,999) * CASE LOWER(COALESCE(dnv.agetotype,'years'))
                                            WHEN 'year'   THEN 365 WHEN 'years'  THEN 365
                                            WHEN 'month'  THEN 30  WHEN 'months' THEN 30
                                            WHEN 'week'   THEN 7   WHEN 'weeks'  THEN 7
                                            WHEN 'day'    THEN 1   WHEN 'days'   THEN 1
                                            ELSE 365 END
    ORDER BY (UPPER(dnv.sex) = UPPER(lrm.gender)) DESC, dnv.sno ASC NULLS LAST
    LIMIT 1
) dnv ON lrp.detailednormalvalues = true

-- (2) Master-level fallback
LEFT JOIN LATERAL (
    SELECT tdnv.rangefrom, tdnv.rangeto, tdnv.rangetype, tdnv.sex
    FROM test_result_detailednormalvalues tdnv
    WHERE tdnv.testresultid = lrp.mastertestresultid
      AND (tdnv.tenant_code = lrm.tenant_code OR tdnv.tenant_code = '0' OR tdnv.tenant_code IS NULL)
      AND (
            tdnv.sex IS NULL OR tdnv.sex = '' OR
            UPPER(tdnv.sex) = UPPER(lrm.gender) OR
            UPPER(tdnv.sex) IN ('BOTH','ALL')
          )
      AND page.age_in_days BETWEEN
            COALESCE(tdnv.agefrom,0) * CASE LOWER(COALESCE(tdnv.agefromtype,'years'))
                                            WHEN 'year'   THEN 365 WHEN 'years'  THEN 365
                                            WHEN 'month'  THEN 30  WHEN 'months' THEN 30
                                            WHEN 'week'   THEN 7   WHEN 'weeks'  THEN 7
                                            WHEN 'day'    THEN 1   WHEN 'days'   THEN 1
                                            ELSE 365 END
          AND
            COALESCE(tdnv.ageto,999) * CASE LOWER(COALESCE(tdnv.agetotype,'years'))
                                            WHEN 'year'   THEN 365 WHEN 'years'  THEN 365
                                            WHEN 'month'  THEN 30  WHEN 'months' THEN 30
                                            WHEN 'week'   THEN 7   WHEN 'weeks'  THEN 7
                                            WHEN 'day'    THEN 1   WHEN 'days'   THEN 1
                                            ELSE 365 END
    ORDER BY (UPPER(tdnv.sex) = UPPER(lrm.gender)) DESC, tdnv.sno ASC NULLS LAST
    LIMIT 1
) dnv_master ON lrp.detailednormalvalues = true
            AND dnv.rangefrom IS NULL
            AND dnv.rangeto   IS NULL

-- (3) Result-level display text
LEFT JOIN LATERAL (
    SELECT STRING_AGG(band.linetext, '<br/>' ORDER BY band.agefrom_days, band.sno) AS fulltext
    FROM (
        SELECT
            adnv.sno,
            COALESCE(adnv.agefrom,0) * CASE LOWER(COALESCE(adnv.agefromtype,'years'))
                WHEN 'year'  THEN 365 WHEN 'years'  THEN 365
                WHEN 'month' THEN 30  WHEN 'months' THEN 30
                WHEN 'week'  THEN 7   WHEN 'weeks'  THEN 7
                WHEN 'day'   THEN 1   WHEN 'days'   THEN 1
                ELSE 365 END AS agefrom_days,
            CONCAT(
                adnv.agefrom, ' ', adnv.agefromtype,
                ' - ',
                adnv.ageto,   ' ', adnv.agetotype,
                ' : ',
                CASE
                    WHEN UPPER(COALESCE(adnv.rangetype,'')) LIKE 'GREATER%'
                        THEN CONCAT('> ', TRIM(TO_CHAR(adnv.rangefrom, 'FM999999990.######')))
                    WHEN UPPER(COALESCE(adnv.rangetype,'')) LIKE 'LESS%'
                        THEN CONCAT('< ', TRIM(TO_CHAR(adnv.rangeto, 'FM999999990.######')))
                    ELSE CONCAT(
                            TRIM(TO_CHAR(adnv.rangefrom, 'FM999999990.######')),
                            ' - ',
                            TRIM(TO_CHAR(adnv.rangeto,   'FM999999990.######'))
                         )
                END
            ) AS linetext
        FROM lab_result_detailednormalvalues adnv
        WHERE adnv.testresultid = lrdd.testresultid
          AND (
                adnv.sex IS NULL OR adnv.sex = '' OR
                UPPER(adnv.sex) = UPPER(lrm.gender) OR
                UPPER(adnv.sex) IN ('BOTH','ALL')
              )
    ) band
) dnv_all ON lrp.detailednormalvalues = true

-- (4) Master-level fallback display text
LEFT JOIN LATERAL (
    SELECT STRING_AGG(band.linetext, '<br/>' ORDER BY band.agefrom_days, band.sno) AS fulltext
    FROM (
        SELECT
            tdnv.sno,
            COALESCE(tdnv.agefrom,0) * CASE LOWER(COALESCE(tdnv.agefromtype,'years'))
                WHEN 'year'  THEN 365 WHEN 'years'  THEN 365
                WHEN 'month' THEN 30  WHEN 'months' THEN 30
                WHEN 'week'  THEN 7   WHEN 'weeks'  THEN 7
                WHEN 'day'   THEN 1   WHEN 'days'   THEN 1
                ELSE 365 END AS agefrom_days,
            CONCAT(
                tdnv.agefrom, ' ', tdnv.agefromtype,
                ' - ',
                tdnv.ageto,   ' ', tdnv.agetotype,
                ' : ',
                CASE
                    WHEN UPPER(COALESCE(tdnv.rangetype,'')) LIKE 'GREATER%'
                        THEN CONCAT('> ', TRIM(TO_CHAR(tdnv.rangefrom, 'FM999999990.######')))
                    WHEN UPPER(COALESCE(tdnv.rangetype,'')) LIKE 'LESS%'
                        THEN CONCAT('< ', TRIM(TO_CHAR(tdnv.rangeto, 'FM999999990.######')))
                    ELSE CONCAT(
                            TRIM(TO_CHAR(tdnv.rangefrom, 'FM999999990.######')),
                            ' - ',
                            TRIM(TO_CHAR(tdnv.rangeto,   'FM999999990.######'))
                         )
                END
            ) AS linetext
        FROM test_result_detailednormalvalues tdnv
        WHERE tdnv.testresultid = lrp.mastertestresultid
          AND (tdnv.tenant_code = lrm.tenant_code OR tdnv.tenant_code = '0' OR tdnv.tenant_code IS NULL)
          AND (
                tdnv.sex IS NULL OR tdnv.sex = '' OR
                UPPER(tdnv.sex) = UPPER(lrm.gender) OR
                UPPER(tdnv.sex) IN ('BOTH','ALL')
              )
    ) band
) dnv_all_master ON lrp.detailednormalvalues = true
                 AND dnv_all.fulltext IS NULL

-- ════════════════════════════════════════════════════════════════════════
-- TEXT NORMAL VALUES 
-- ════════════════════════════════════════════════════════════════════════

LEFT JOIN LATERAL (
    SELECT tnv.normalvalue, tnv.sex
    FROM lab_result_textnormalvalues tnv
    WHERE tnv.testresultid = lrdd.testresultid
      AND (
            tnv.sex IS NULL OR tnv.sex = '' OR
            UPPER(tnv.sex) = UPPER(lrm.gender) OR
            UPPER(tnv.sex) IN ('BOTH','ALL')
          )
    ORDER BY (UPPER(tnv.sex) = UPPER(lrm.gender)) DESC
    LIMIT 1
) tnv ON lrp.simplenormalvalues = true

LEFT JOIN LATERAL (
    SELECT ttnv.normalvalue, ttnv.sex
    FROM test_result_textnormalvalues ttnv
    WHERE ttnv.testresultid = lrp.mastertestresultid
      AND (ttnv.tenant_code = lrm.tenant_code OR ttnv.tenant_code = '0' OR ttnv.tenant_code IS NULL)
      AND (
            ttnv.sex IS NULL OR ttnv.sex = '' OR
            UPPER(ttnv.sex) = UPPER(lrm.gender) OR
            UPPER(ttnv.sex) IN ('BOTH','ALL')
          )
    ORDER BY (UPPER(ttnv.sex) = UPPER(lrm.gender)) DESC
    LIMIT 1
) tnv_master ON lrp.simplenormalvalues = true
            AND NULLIF(tnv.normalvalue, '') IS NULL

-- sample_master via test_master.scode
LEFT JOIN sample_master sm
       ON sm.scode       = tm.scode
      AND sm.tenant_code = lrm.tenant_code

-- uom_master
LEFT JOIN uom_master uom
       ON uom.ucode       = lrp.defaultunitscode::bigint
      AND uom.tenant_code = lrm.tenant_code

-- report_method via lab_result_properties
LEFT JOIN report_method rtm
       ON rtm.rtmcode     = lrp.rtmcode::bigint
      AND rtm.tenant_code = lrm.tenant_code

-- report_method via test_result_properties master
LEFT JOIN test_result_properties trp_master
       ON trp_master.testresultid = lrp.mastertestresultid
      AND (trp_master.tenant_code = lrm.tenant_code
           OR trp_master.tenant_code = '0'
           OR trp_master.tenant_code IS NULL)
LEFT JOIN report_method rtm_master
       ON rtm_master.rtmcode     = trp_master.rtmcode::bigint
      AND rtm_master.tenant_code = lrm.tenant_code

-- Specimen collection
LEFT JOIN LATERAL (
    SELECT collectedtime
    FROM   lab_request_specimencollection
    WHERE  requestguid = lrm.requestguid
      AND  scode       = tm.scode
      AND  tenant_code = lrm.tenant_code
      AND  COALESCE(isdeleted, false) = false
    ORDER  BY collectedtime DESC
    LIMIT  1
) sc ON true

-- area_master
LEFT JOIN area_master ar
       ON ar.areacode     = lrm.areacode

-- Authorizer user
LEFT JOIN LATERAL (
    SELECT u.name, u.description, u.signature_image
    FROM mastertenant.user_master u
    WHERE u.user_code = lrd.resultauthorizedby
    LIMIT 1
) au ON true

WHERE lrm.requestguid = @requestguid
  AND lrm.tenant_code = @tenant_code
  AND COALESCE(lrm.deleted, false) = false

ORDER BY COALESCE(gm.orderno, 9999),
         COALESCE(tm.orderno, 9999),
         COALESCE(lrd.testsno, 0),
         COALESCE(lrdd.testsno, 0)";

                var rawRows = (await db.QueryAsync<RawReportRow>(
                    resultSql, new { requestguid = requestguid.ToString(), tenant_code }
                )).ToList();

                if (rawRows.Count == 0) return null;

                // ── Step 2: Auth users ────────────────────────────────────────────────
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
WHERE lrd.requestguid = @requestguid
  AND lrd.tenant_code = @tenant_code
  AND (lrd.ttid = 1 OR lrd.ttid IS NULL)";

                var rawAuth = (await db.QueryAsync<RawAuthUser>(
                    authSql, new { requestguid = requestguid.ToString(), tenant_code }
                )).ToList();

                // ── Step 3: Collect all unique non-null MinIO paths ───────────────────
                var allPaths = rawRows
                    .SelectMany(r => new[]
                    {
                        r.DefaultAuthorizeImage,
                        r.CustomerImage,
                        r.SignatureImage
                    })
                    .Concat(rawAuth.SelectMany(a => new[]
                    {
                        a.EnteredSign,
                        a.AuthorizedSign,
                        a.AuthorizedSign2
                    }));

                // ── Step 4: Fetch all images from MinIO in parallel ───────────────────
                var imageCache = await BuildImageCacheAsync(allPaths);

                // ── Step 5: Map raw DTOs → final models ───────────────────────────────
                var results = rawRows.Select(r => MapRow(r, imageCache)).ToList();
                var authUsers = rawAuth.Select(a => MapAuth(a, imageCache)).ToList();

                // ── Step 6: POST to report server ─────────────────────────────────────
                var payload = new RoutineLabReport { rrm = results, auth = authUsers };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"/api/routinereport/iscanlabreport?tenantId={tenant_code}&isLetterhead={isletterhead}",
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

        // ─────────────────────────────────────────────────────────────────────────
        // Code-128B barcode renderer (SkiaSharp)
        // ─────────────────────────────────────────────────────────────────────────
        private static readonly int[] Code128BEncoding = new[]
        {
            212222, 222122, 222221, 121223,
            121322, 131222, 122213, 122312,
            132212, 221213, 221312, 231212,
            112232, 122132, 122231, 113222,
            123122, 123221, 223211, 221132,
            221231, 213212, 223112, 312131,
            311222, 321122, 321221, 312212,
            322112, 322211, 212123, 212321,
            232121, 111323, 131123, 131321,
            112313, 132113, 132311, 211313,
            231113, 231311, 112133, 112331,
            132131, 113123, 113321, 133121,
            313121, 211331, 231131, 213113,
            213311, 213131, 311123, 311321,
            331121, 312113, 312311, 332111,
            314111, 221411, 431111, 111224,
            111422, 121124, 121421, 141122,
            141221, 112214, 112412, 122114,
            122411, 142112, 142211, 241211,
            221114, 213114, 213411, 221141,
            413111, 141113, 141311, 311141,
            411113, 411311, 113141, 114131,
            311411, 341111, 111143, 111341,
            131141, 114113, 114311
        };

        private static readonly int[] Code128Special =
        {
            211412,
            2331112
        };

        private static byte[] GenerateBarcodePng(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return GenerateBlankPng();

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

            AppendBarPattern(bars, Code128BEncoding[checksum % 103]);

            AppendBarPattern(bars, Code128Special[1]);

            const int barUnit = 3;
            const int quietZone = 14;
            const int barcodeH = 80;

            int totalBarWidth = bars.Sum() * barUnit;
            int imgW = totalBarWidth + quietZone * 2;

            using var bitmap = new SKBitmap(imgW, barcodeH);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };

            int x = quietZone;
            for (int i = 0; i < bars.Count; i++)
            {
                int w = bars[i] * barUnit;
                if (i % 2 == 0)
                    canvas.DrawRect(x, 0, w, barcodeH, blackPaint);
                x += w;
            }

            using var img = SKImage.FromBitmap(bitmap);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
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
                        c.custcode AS PatientId,
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

                var payload = new CasesheetReportRequest
                {
                    CasesheetData = casesheet,
                    LogoImage = logoImage,
                    IsLetterhead = isletterhead ?? false,
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
                        c.custcode AS PatientId,
                        COALESCE(ipm.patcode, c.custcode) AS VisitNo,
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

                var payload = new CasesheetReportRequest
                {
                    CasesheetData = casesheet,
                    LogoImage = logoImage,
                    IsLetterhead = isletterhead ?? false,
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
    }
}
