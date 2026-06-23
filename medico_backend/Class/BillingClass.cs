using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace medico_backend.Class
{
    public class HmsBillingClass
    {
        private readonly string _conn;
        private readonly ILogger<HmsBillingClass> _logger;

        public HmsBillingClass(IConfiguration cfg, ILogger<HmsBillingClass> logger)
        {
            _conn = cfg.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Database connection string 'conn' not found.");
            _logger = logger;
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ════════════════════════════════════════════════════════════════════════
        //  1. SAVE / CREATE / UPDATE BILLS
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsBillResponse? data)> SaveBill(CreateHmsBillRequest req, string tenantCode)
        {
            var validationErr = ValidateBillRequest(req);
            if (validationErr != null) return (validationErr, null);

            return string.IsNullOrEmpty(req.requestguid)
                ? await CreateBill(req, tenantCode)
                : await UpdateBill(req.requestguid, req, tenantCode);
        }

        private async Task<(string status, HmsBillResponse? data)> CreateBill(CreateHmsBillRequest req, string tenantCode)
        {
            _logger.LogInformation(">>>>>> RESOLVED TENANT CODE: [{tenant}]", tenantCode);
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Verify/Retrieve Active Counter Timing
                var currentShift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing 
                      WHERE bhcode = @bhcode AND cntcode = @cntcode AND todate IS NULL AND tenant_code = @tenantCode 
                      LIMIT 1", new { bhcode = req.enteredbhcode, cntcode = req.cntcode, tenantCode }, tx);

                if (currentShift == null)
                    return ("Selected billing counter shift session is not open.", null);

                // Fetch Sequential Master Record Configurations 
                var masterBillConfig = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master 
                      WHERE isreceiptno = false AND issampleno = false AND deleted = false AND tenant_code = @tenantCode 
                      LIMIT 1", new { tenantCode }, tx);

                if (masterBillConfig == null)
                    return ("Bill Number sequential configuration master rule not found.", null);

                // Generate Bill sequence and barcode numbers
                var billNumInfo = await GetNextSequenceNumber(db, tx, masterBillConfig.bncode, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                string requestGuid = Guid.NewGuid().ToString();
                double lineGrossTotal = req.items.Sum(x => (x.amount ?? 0));
                double aggregateDiscount = (req.discountamount ?? 0) + (req.specialdiscount ?? 0);
                double calculativeNetAmount = lineGrossTotal - aggregateDiscount;
                if (calculativeNetAmount < 0) calculativeNetAmount = 0;

                var masterRecord = new HmsLabRequestMaster
                {
                    requestguid = requestGuid,
                    requestsno = billNumInfo.sno,
                    requestsnoprint = billNumInfo.snoprint,
                    requestbarcode = billNumInfo.barcode,
                    requestconvertedbarcode = billNumInfo.barcode,
                    requestdatetime = DateTime.UtcNow,
                    requesteddatetime = DateTime.UtcNow,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    bncode = billNumInfo.used_bncode,
                    cntcode = req.cntcode,
                    cnttid = currentShift.cnttid,
                    custid = req.custid,
                    name = req.patient_name,
                    gender = req.gender,
                    dateofbirth = req.dateofbirth,
                    ageyears = req.ageyears,
                    agemonths = req.agemonths,
                    agedays = req.agedays,
                    mobileno = req.mobileno,
                    address = req.address,
                    areacode = req.areacode,
                    dcode = req.dcode,
                    consultantdcode = req.consultantdcode,
                    ftcode = req.ftcode,
                    pmcode = req.pmcode,
                    ctcode = req.ctcode,
                    ricode = req.ricode,
                    requestamount = lineGrossTotal,
                    discountper = req.discountper,
                    discountamount = req.discountamount,
                    specialdiscount = req.specialdiscount,
                    totalamount = calculativeNetAmount,
                    paidamount = req.paidamount ?? 0,
                    paidviareceipt = req.paidamount ?? 0,
                    pmc1 = req.pmc1 ?? 0,
                    pmc2 = req.pmc2 ?? 0,
                    pmc3 = req.pmc3 ?? 0,
                    iscashbill = req.iscashbill,
                    iscreditbill = req.iscreditbill,
                    isinvestigation = true,
                    requeststatus = true,
                    resultstatus = false,
                    deleted = false,
                    isdeleted = false,
                    isverified = false,
                    isinsurancepatient = req.isinsurancepatient,
                    policyno = req.policyno,
                    authorisationno = req.authorisationno,
                    concessionreason = req.concessionreason,
                    card_refno = req.card_refno,
                    bank_app = req.bank_app,
                    bill_category = "HMS",
                    sheet_id = req.sheet_id,
                    opvisitid = req.op_id,
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    tenant_code = tenantCode
                };

                await db.InsertAsync(masterRecord, tx);

                // Insert Items
                int itemIndex = 1;
                foreach (var line in req.items)
                {
                    var detailRow = new HmsLabRequestDetail
                    {
                        requestdetailsid = Guid.NewGuid().ToString(),
                        requestguid = requestGuid,
                        testsno = itemIndex++,
                        tcode = line.tcode,
                        chargetype = line.charge_type,
                        item_name = line.item_name,
                        item_ref_id = line.item_ref_id,
                        testrate = line.unit_rate,
                        standardprice = line.unit_rate,
                        testamount = line.amount,
                        discount = line.discount,
                        newamount = (line.amount ?? 0) - (line.discount ?? 0),
                        gstper = line.gst_per,
                        gstamount = ((line.amount ?? 0) - (line.discount ?? 0)) * ((line.gst_per ?? 0) / 100.0),
                        qty = line.qty,
                        ttid = line.ttid,
                        resultstatus = false,
                        requeststatus = true,
                        isdeleted = false,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(detailRow, tx);
                }

                // If immediate payment is recorded, log receipts
                HmsReceiptInserted? activeReceiptInfo = null;
                if ((req.paidamount ?? 0) > 0)
                {
                    activeReceiptInfo = await GenerateReceiptLog(db, tx, masterRecord, req.paidamount ?? 0, req.collection_type, tenantCode);
                }

                tx.Commit();
                var result = await FetchBillRecordByGuid(requestGuid, tenantCode);
                return ("SUCCESS", result);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed to create HMS billing record transaction entry context.");
                return ($"Internal transaction error: {ex.Message}", null);
            }
        }

        private async Task<(string status, HmsBillResponse? data)> UpdateBill(string requestGuid, CreateHmsBillRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existingMaster = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    "SELECT * FROM lab_request_master WHERE requestguid = @requestGuid AND tenant_code = @tenantCode",
                    new { requestGuid, tenantCode }, tx);

                if (existingMaster == null) return ("Target billing record profile update vector not located.", null);
                if (existingMaster.isdeleted == true || existingMaster.deleted == true) return ("Modification criteria locked against deleted profiles.", null);

                double lineGrossTotal = req.items.Sum(x => (x.amount ?? 0));
                double aggregateDiscount = (req.discountamount ?? 0) + (req.specialdiscount ?? 0);
                double calculativeNetAmount = lineGrossTotal - aggregateDiscount;
                if (calculativeNetAmount < 0) calculativeNetAmount = 0;

                existingMaster.custid = req.custid;
                existingMaster.name = req.patient_name;
                existingMaster.gender = req.gender;
                existingMaster.dateofbirth = req.dateofbirth;
                existingMaster.ageyears = req.ageyears;
                existingMaster.agemonths = req.agemonths;
                existingMaster.agedays = req.agedays;
                existingMaster.mobileno = req.mobileno;
                existingMaster.address = req.address;
                existingMaster.areacode = req.areacode;
                existingMaster.dcode = req.dcode;
                existingMaster.consultantdcode = req.consultantdcode;
                existingMaster.ftcode = req.ftcode;
                existingMaster.pmcode = req.pmcode;
                existingMaster.ctcode = req.ctcode;
                existingMaster.ricode = req.ricode;
                existingMaster.requestamount = lineGrossTotal;
                existingMaster.discountper = req.discountper;
                existingMaster.discountamount = req.discountamount;
                existingMaster.specialdiscount = req.specialdiscount;
                existingMaster.totalamount = calculativeNetAmount;
                existingMaster.pmc1 = req.pmc1 ?? 0;
                existingMaster.pmc2 = req.pmc2 ?? 0;
                existingMaster.pmc3 = req.pmc3 ?? 0;
                existingMaster.iscashbill = req.iscashbill;
                existingMaster.iscreditbill = req.iscreditbill;
                existingMaster.isinsurancepatient = req.isinsurancepatient;
                existingMaster.policyno = req.policyno;
                existingMaster.authorisationno = req.authorisationno;
                existingMaster.concessionreason = req.concessionreason;
                existingMaster.card_refno = req.card_refno;
                existingMaster.bank_app = req.bank_app;
                existingMaster.alteredbhcode = req.enteredbhcode;

                await db.UpdateAsync(existingMaster, tx);

                // Re-align detail lines
                await db.ExecuteAsync("DELETE FROM lab_request_details WHERE requestguid = @requestGuid AND tenant_code = @tenantCode", new { requestGuid, tenantCode }, tx);

                int itemIndex = 1;
                foreach (var line in req.items)
                {
                    var detailRow = new HmsLabRequestDetail
                    {
                        requestdetailsid = Guid.NewGuid().ToString(),
                        requestguid = requestGuid,
                        testsno = itemIndex++,
                        tcode = line.tcode,
                        chargetype = line.charge_type,
                        item_name = line.item_name,
                        item_ref_id = line.item_ref_id,
                        testrate = line.unit_rate,
                        standardprice = line.unit_rate,
                        testamount = line.amount,
                        discount = line.discount,
                        newamount = (line.amount ?? 0) - (line.discount ?? 0),
                        gstper = line.gst_per,
                        gstamount = ((line.amount ?? 0) - (line.discount ?? 0)) * ((line.gst_per ?? 0) / 100.0),
                        qty = line.qty,
                        ttid = line.ttid,
                        resultstatus = false,
                        requeststatus = true,
                        isdeleted = false,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(detailRow, tx);
                }

                tx.Commit();
                var result = await FetchBillRecordByGuid(requestGuid, tenantCode);
                return ("SUCCESS", result);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed executing structural content update over hms billing profile.");
                return ($"Update action transaction failure: {ex.Message}", null);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. RECEIPT LOGGING ENGINE
        // ════════════════════════════════════════════════════════════════════════

        private async Task<HmsReceiptInserted> GenerateReceiptLog(IDbConnection db, IDbTransaction tx, HmsLabRequestMaster bill, double collectionValue, string channelType, string tenantCode)
        {
            var masterReceiptConfig = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                @"SELECT * FROM billno_master 
                  WHERE isreceiptno = true AND deleted = false AND tenant_code = @tenantCode LIMIT 1", new { tenantCode }, tx);

            if (masterReceiptConfig == null)
                throw new InvalidOperationException("Receipt generation sequence layout configs missing.");

            var seqReceipt = await GetNextSequenceNumber(db, tx, masterReceiptConfig.bncode, bill.enteredbhcode ?? 0, (int?)bill.cntcode ?? 0, tenantCode);

            string receiptGuid = Guid.NewGuid().ToString();

            var masterReceipt = new HmsReceiptMaster
            {
                receiptguid = receiptGuid,
                receiptdate = DateTime.UtcNow,
                receiptsno = seqReceipt.sno,
                receiptsnoprint = seqReceipt.snoprint,
                receiptbarcode = seqReceipt.barcode,
                receiptcovertedbarcode = seqReceipt.barcode,
                cntcode = bill.cntcode,
                cnttid = bill.cnttid,
                tmcode = bill.tmcode,
                pmcode = bill.pmcode,
                ctcode = bill.ctcode,
                bankname = bill.bank_app,
                paymentreference = bill.card_refno,
                amountpaid = collectionValue,
                amountadjusted = collectionValue,
                amounttotal = collectionValue,
                deleted = false,
                isdeleted = false,
                isbill = true,
                ispatient = true,
                receipttype = "HMS",
                custid = (int?)bill.custid,
                opvisitid = bill.opvisitid,
                enteredbhcode = bill.enteredbhcode,
                usercode = bill.usercode,
                computercode = bill.computercode,
                entereddate = DateTime.UtcNow,
                ibsdate = DateTime.UtcNow,
                tenant_code = tenantCode
            };

            await db.InsertAsync(masterReceipt, tx);

            var receiptLine = new HmsReceiptDetail
            {
                receiptdetailsid = Guid.NewGuid().ToString(),
                receiptguid = receiptGuid,
                requestguid = bill.requestguid,
                receiptamount = collectionValue,
                discount_amount = 0,
                refund_amount = 0,
                deleted = false,
                usercode = bill.usercode,
                computercode = bill.computercode,
                entereddate = DateTime.UtcNow,
                ibsdate = DateTime.UtcNow,
                tenant_code = tenantCode
            };

            await db.InsertAsync(receiptLine, tx);

            string balanceCollectionGuid = Guid.NewGuid().ToString();
            var balancingContext = new HmsBalanceCollectionBy
            {
                balancecollectionbyid = balanceCollectionGuid,
                bhcode = bill.enteredbhcode,
                collected_date = DateTime.UtcNow,
                collection_type = channelType.ToUpper(),
                receipt_guid = receiptGuid,
                request_guid = bill.requestguid,
                collectedamount = collectionValue,
                tmcode = bill.tmcode,
                cntcode = bill.cntcode,
                cnttid = bill.cnttid,
                ctcode = bill.ctcode,
                pmcode = bill.pmcode,
                deleted = false,
                usercode = bill.usercode,
                computercode = bill.computercode,
                entereddate = DateTime.UtcNow,
                ibsdate = DateTime.UtcNow,
                tenant_code = tenantCode
            };

            await db.InsertAsync(balancingContext, tx);

            return new HmsReceiptInserted
            {
                guid = receiptGuid,
                barcode = seqReceipt.barcode,
                snoprint = seqReceipt.snoprint
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. POST BALANCE PAYMENT SETTLEMENT
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsBillResponse? data)> AddPayment(AddHmsPaymentRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var masterBill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    "SELECT * FROM lab_request_master WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                if (masterBill == null) return ("Target invoice reference for allocation entry context is missing.", null);

                double netLimit = masterBill.totalamount ?? 0;
                double existingSettled = masterBill.paidamount ?? 0;
                double pendingResidual = netLimit - existingSettled;

                if (req.amount <= 0 || req.amount > (pendingResidual + 0.01))
                    return ($"Payment amount conflict. Pending amount balance remaining: {pendingResidual}", null);

                masterBill.paidamount = existingSettled + req.amount;
                masterBill.paidviareceipt = (masterBill.paidviareceipt ?? 0) + req.amount;
                masterBill.pmc1 = (masterBill.pmc1 ?? 0) + (req.pmc1 ?? 0);
                masterBill.pmc2 = (masterBill.pmc2 ?? 0) + (req.pmc2 ?? 0);
                masterBill.pmc3 = (masterBill.pmc3 ?? 0) + (req.pmc3 ?? 0);

                await db.UpdateAsync(masterBill, tx);

                await GenerateReceiptLog(db, tx, masterBill, req.amount, req.collection_type, tenantCode);

                tx.Commit();
                var response = await FetchBillRecordByGuid(req.requestguid, tenantCode);
                return ("SUCCESS", response);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Error processing incoming ledger collection context over allocation parameters.");
                return ($"Ledger posting error context: {ex.Message}", null);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  4. BILL CANCELLATION
        // ════════════════════════════════════════════════════════════════════════

        public async Task<string> CancelBill(CancelHmsBillRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var masterRecord = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    "SELECT * FROM lab_request_master WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                if (masterRecord == null) return "Billing file master reference layout profile not found.";
                if (masterRecord.isdeleted == true) return "Target asset index context profile state already registers cancelled indicators.";

                await db.ExecuteAsync(
                    @"UPDATE lab_request_master 
                      SET isdeleted = true, deleted = true, concessionreason = CONCAT(@reason, ' | Cancelled Context') 
                      WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, reason = req.reason, tenantCode }, tx);

                await db.ExecuteAsync(
                    "UPDATE lab_request_details SET isdeleted = true WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                await db.ExecuteAsync(
                    @"UPDATE receipt_master SET isdeleted = true, deleted = true WHERE receiptguid IN (
                        SELECT receiptguid FROM receipt_details WHERE requestguid = @requestguid AND tenant_code = @tenantCode
                    )", new { requestguid = req.requestguid, tenantCode }, tx);

                await db.ExecuteAsync(
                    "UPDATE receipt_details SET deleted = true WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                await db.ExecuteAsync(
                    "UPDATE balancecollectionby SET deleted = true WHERE request_guid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                tx.Commit();
                return "SUCCESS";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Structural operational state execution context rollback occurred handling cancellations.");
                return $"Structural cancellation action framework context failure mapping: {ex.Message}";
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  5. COUNTER SEED SESSIONS MANAGEMENT
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsCounterTimingDto? session)> OpenCounterShift(HmsCounterTiming req, string tenantCode)
        {
            using var db = GetConnection();
            var runningSessionCheck = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                @"SELECT * FROM counter_timing 
                  WHERE bhcode = @bhcode AND cntcode = @cntcode AND todate IS NULL AND tenant_code = @tenantCode LIMIT 1",
                new { bhcode = req.bhcode, cntcode = req.cntcode, tenantCode });

            if (runningSessionCheck != null)
                return ("An active shift counter is currently operational for this target interface window.", null);

            int progressiveShiftIndex = await db.ExecuteScalarAsync<int>(
                @"SELECT COALESCE(MAX(shiftsno), 0) + 1 FROM counter_timing 
                  WHERE bhcode = @bhcode AND cntcode = @cntcode AND counterdate = @counterdate::DATE AND tenant_code = @tenantCode",
                new { bhcode = req.bhcode, cntcode = req.cntcode, counterdate = req.counterdate?.ToString("yyyy-MM-dd"), tenantCode });

            req.cnttid = Guid.NewGuid().ToString();
            req.shiftsno = progressiveShiftIndex;
            req.fromdate = DateTime.UtcNow;
            req.todate = null;
            req.tenant_code = tenantCode;

            await db.InsertAsync(req);

            var mappedData = await EvaluateShiftProfiling(req.cnttid, tenantCode);
            return ("SUCCESS", mappedData);
        }

        public async Task<string> CloseCounterShift(CloseCounterRequest req, string tenantCode)
        {
            using var db = GetConnection();
            var targetedContext = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                "SELECT * FROM counter_timing WHERE cnttid = @cnttid AND tenant_code = @tenantCode",
                new { cnttid = req.cnttid, tenantCode });

            if (targetedContext == null) return "Session configuration timeline reference path target missing context.";
            if (targetedContext.todate != null) return "Operational state context parameter flag evaluates closed already.";

            await db.ExecuteAsync(
                "UPDATE counter_timing SET todate = @now WHERE cnttid = @cnttid AND tenant_code = @tenantCode",
                new { now = DateTime.UtcNow, cnttid = req.cnttid, tenantCode });

            return "SUCCESS";
        }

        // ════════════════════════════════════════════════════════════════════════
        //  6. READ DATA QUERIES & ANALYTICAL COMPILATIONS
        // ════════════════════════════════════════════════════════════════════════

        public async Task<HmsBillResponse?> FetchBillRecordByGuid(string requestGuid, string tenantCode)
        {
            using var db = GetConnection();
            var master = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT rm.*, bm.name as countername 
                  FROM lab_request_master rm 
                  LEFT JOIN billno_master bm ON rm.bncode = bm.bncode AND rm.tenant_code = bm.tenant_code
                  WHERE rm.requestguid = @requestGuid AND rm.tenant_code = @tenantCode", new { requestGuid, tenantCode });

            if (master == null) return null;

            var items = await db.QueryAsync<HmsBillLineResponse>(
                @"SELECT requestdetailsid, chargetype as charge_type, item_name, tcode, item_ref_id, 
                         testrate as unit_rate, testamount as amount, discount, newamount as final_amount, qty, gstper as gst_per, gstamount as gst_amount
                  FROM lab_request_details 
                  WHERE requestguid = @requestGuid AND tenant_code = @tenantCode ORDER BY testsno ASC", new { requestGuid, tenantCode });

            var receiptProfile = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT r.receiptguid, r.receiptsnoprint, r.receiptbarcode 
                  FROM receipt_details d
                  INNER JOIN receipt_master r ON d.receiptguid = r.receiptguid
                  WHERE d.requestguid = @requestGuid AND r.isdeleted = false AND r.tenant_code = @tenantCode LIMIT 1", new { requestGuid, tenantCode });

            double totalInvoiceLimit = (double)(master.totalamount ?? 0.0);
            double recognizedCollections = (double)(master.paidamount ?? 0.0);
            double computedDifference = totalInvoiceLimit - recognizedCollections;

            return new HmsBillResponse
            {
                requestguid = master.requestguid,
                bill_no = master.requestsnoprint,
                barcode = master.requestbarcode,
                bill_date = master.requestdatetime,
                custid = master.custid,
                patient_name = master.name,
                gender = master.gender,
                mobileno = master.mobileno,
                ageyears = master.ageyears,
                enteredbhcode = master.enteredbhcode,
                cntcode = (int?)master.cntcode,
                cnttid = master.cnttid,
                tmcode = master.tmcode,
                gross_amount = master.requestamount,
                discount_amount = (double)(master.discountamount ?? 0.0) + (double)(master.specialdiscount ?? 0.0),
                tax_amount = master.taxamount,
                net_amount = totalInvoiceLimit,
                paid_amount = recognizedCollections,
                balance_amount = computedDifference < 0.01 ? 0.0 : computedDifference,
                is_settled = (computedDifference <= 0.05),
                pmc1 = master.pmc1,
                pmc2 = master.pmc2,
                pmc3 = master.pmc3,
                counter_name = master.countername,
                receiptguid = receiptProfile?.receiptguid,
                receipt_no = receiptProfile?.receiptsnoprint,
                receipt_barcode = receiptProfile?.receiptbarcode,
                items = items.ToList()
            };
        }

        public async Task<(List<HmsBillSummary> data, int totalCount)> QueryPaginatedBillsList(HmsBillFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var parameters = new DynamicParameters();
            parameters.Add("tenantCode", tenantCode);

            string queryConditions = "WHERE m.isdeleted = false AND m.bill_category = 'HMS' AND m.tenant_code = @tenantCode ";

            if (filter.bhcode.HasValue)
            {
                queryConditions += " AND m.enteredbhcode = @bhcode ";
                parameters.Add("bhcode", filter.bhcode);
            }
            if (filter.cntcode.HasValue)
            {
                queryConditions += " AND m.cntcode = @cntcode ";
                parameters.Add("cntcode", filter.cntcode);
            }
            if (filter.fromdate.HasValue)
            {
                queryConditions += " AND m.requestdatetime >= @fromdate ";
                parameters.Add("fromdate", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                queryConditions += " AND m.requestdatetime <= @todate ";
                parameters.Add("todate", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (filter.custid.HasValue)
            {
                queryConditions += " AND m.custid = @custid ";
                parameters.Add("custid", filter.custid);
            }
            if (filter.pendingonly == true)
            {
                queryConditions += " AND (m.totalamount - m.paidamount) > 0.05 ";
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                queryConditions += " AND (m.name ILIKE @searchTerm OR m.requestsnoprint ILIKE @searchTerm OR m.mobileno ILIKE @searchTerm) ";
                parameters.Add("searchTerm", $"%{filter.search}%");
            }

            string totalSumQuery = $"SELECT COUNT(*) FROM lab_request_master m {queryConditions}";
            int aggregatedCount = await db.ExecuteScalarAsync<int>(totalSumQuery, parameters);

            int rowsOffset = (filter.page - 1) * filter.pagesize;
            parameters.Add("limit", filter.pagesize);
            parameters.Add("offset", rowsOffset);

            string structuralFetchQuery = $@"
                SELECT m.requestguid, m.requestsnoprint as bill_no, m.name as patient_name, m.mobileno, 
                       m.requestdatetime as bill_date, m.requestamount as gross_amount, 
                       (COALESCE(m.discountamount,0) + COALESCE(m.specialdiscount,0)) as discount_amount,
                       m.totalamount as net_amount, m.paidamount as paid_amount, 
                       m.enteredbhcode, m.cntcode
                FROM lab_request_master m
                {queryConditions}
                ORDER BY m.requestdatetime DESC 
                LIMIT @limit OFFSET @offset";

            var dataRows = await db.QueryAsync<HmsBillSummary>(structuralFetchQuery, parameters);
            var listings = dataRows.Select(x => {
                double computedDiff = (x.net_amount ?? 0) - (x.paid_amount ?? 0);
                x.balance_amount = computedDiff < 0.01 ? 0 : computedDiff;
                x.is_settled = (computedDiff <= 0.05);
                return x;
            }).ToList();

            return (listings, aggregatedCount);
        }

        public async Task<HmsCounterTimingDto?> EvaluateShiftProfiling(string sessionId, string tenantCode)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<HmsCounterTimingDto>(
                @"SELECT c.*, b.name as counter_name,
                         CASE WHEN c.todate IS NULL THEN true ELSE false END as is_open,
                         CASE WHEN c.todate IS NOT NULL THEN true ELSE false END as is_closed
                  FROM counter_timing c
                  LEFT JOIN billno_master b ON c.cntcode = b.cntcode AND c.tenant_code = b.tenant_code
                  WHERE c.cnttid = @sessionId AND c.tenant_code = @tenantCode LIMIT 1", new { sessionId, tenantCode });
        }

        public async Task<List<HmsDailyCollectionDto>> ExtractDailyCollectionSummaryReport(int branchCode, DateTime reportingDay, string tenantCode)
        {
            using var db = GetConnection();
            string compilationRawQuery = @"
                SELECT 
                    @reportingDay::DATE as date,
                    m.enteredbhcode as bhcode,
                    m.cntcode,
                    bm.name as counter_name,
                    COUNT(m.requestguid)::INT as total_bills,
                    SUM(COALESCE(m.requestamount, 0))::DOUBLE PRECISION as gross_amount,
                    SUM(COALESCE(m.discountamount, 0) + COALESCE(m.specialdiscount, 0))::DOUBLE PRECISION as discount_amount,
                    SUM(COALESCE(m.totalamount, 0))::DOUBLE PRECISION as net_amount,
                    SUM(COALESCE(m.pmc1, 0))::DOUBLE PRECISION as collected_cash,
                    SUM(COALESCE(m.pmc2, 0))::DOUBLE PRECISION as collected_card,
                    SUM(COALESCE(m.pmc3, 0))::DOUBLE PRECISION as collected_upi,
                    SUM(COALESCE(m.paidamount, 0))::DOUBLE PRECISION as total_collected
                FROM lab_request_master m
                LEFT JOIN billno_master bm ON m.cntcode = bm.cntcode AND m.tenant_code = bm.tenant_code
                WHERE m.enteredbhcode = @branchCode 
                  AND m.requestdatetime::DATE = @reportingDay::DATE 
                  AND m.isdeleted = false 
                  AND m.bill_category = 'HMS'
                  AND m.tenant_code = @tenantCode
                GROUP BY m.enteredbhcode, m.cntcode, bm.name";

            var summaryList = await db.QueryAsync<HmsDailyCollectionDto>(compilationRawQuery, new { branchCode, reportingDay = reportingDay.Date, tenantCode });
            return summaryList.Select(x => {
                double diff = x.net_amount - x.total_collected;
                x.pending_amount = diff < 0.01 ? 0 : diff;
                return x;
            }).ToList();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  7. SEQUENTIAL NUMBERING GENERATOR (SELECT FOR UPDATE)
        // ════════════════════════════════════════════════════════════════════════

        private async Task<HmsNumberResult> GetNextSequenceNumber(IDbConnection db, IDbTransaction tx, decimal engineCode, int branchReference, int counterReference, string tenantCode)
        {
            // Lock row sequentially via SELECT FOR UPDATE to eliminate racing criteria conditions 
            var sequentialRecord = await db.QueryFirstOrDefaultAsync<HmsBillNoSequence>(
    @"SELECT seq_id, bncode, bhcode, cntcode, orderno,
             last_used_date::timestamp AS last_used_date,
             tenant_code, snoprint
      FROM billno_sequence
      WHERE bncode = @engineCode AND bhcode = @branchReference AND cntcode = @counterReference AND tenant_code = @tenantCode
      FOR UPDATE", new { engineCode, branchReference, counterReference, tenantCode }, tx);

            int targetedProgressiveOrder = 1;

            if (sequentialRecord == null)
            {
                var definitionMaster = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    "SELECT orderno FROM billno_master WHERE bncode = @engineCode AND tenant_code = @tenantCode",
                    new { engineCode, tenantCode }, tx);

                if (definitionMaster != null) targetedProgressiveOrder = definitionMaster.orderno;

                var initialRow = new HmsBillNoSequence
                {
                    bncode = engineCode,
                    bhcode = branchReference,
                    cntcode = counterReference,
                    orderno = targetedProgressiveOrder,
                    last_used_date = DateTime.UtcNow.Date,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(initialRow, tx);
            }
            else
            {
                targetedProgressiveOrder = sequentialRecord.orderno + 1;
                sequentialRecord.orderno = targetedProgressiveOrder;
                sequentialRecord.last_used_date = DateTime.UtcNow.Date;
                await db.UpdateAsync(sequentialRecord, tx);
            }

            var identityPrefixMeta = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT shortname, name FROM billno_master WHERE bncode = @engineCode AND tenant_code = @tenantCode",
                new { engineCode, tenantCode }, tx);

            string compositeShortToken = identityPrefixMeta?.shortname ?? "INV";
            string printRepresentation = $"{compositeShortToken}-{DateTime.UtcNow:yyMM}-{targetedProgressiveOrder:D5}";
            string trackingBarcode = $"{engineCode}{branchReference}{counterReference}{targetedProgressiveOrder}";

            return new HmsNumberResult
            {
                sno = targetedProgressiveOrder,
                snoprint = printRepresentation,
                barcode = trackingBarcode,
                used_bncode = engineCode
            };
        }

        private string? ValidateBillRequest(CreateHmsBillRequest requestPayload)
        {
            if (requestPayload == null) return "Missing structural parameter body array context contents.";
            if (string.IsNullOrEmpty(requestPayload.patient_name)) return "Patient nomenclature descriptor context must contain value parameters.";
            if (requestPayload.items == null || !requestPayload.items.Any()) return "Invoice payload does not define child operational data line line-items.";

            foreach (var element in requestPayload.items)
            {
                if (string.IsNullOrEmpty(element.item_name) && !element.tcode.HasValue)
                    return "Item descriptions require alternate literal content names when code master markers are unavailable.";
                if ((element.amount ?? 0) < 0) return "Line calculation properties cannot process negative evaluation values.";
            }
            return null;
        }
        // ════════════════════════════════════════════════════════════════════════
        //  8. BILLNO MASTER CONFIGURATION (Bill / Receipt / Sample number setup)
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, BillNoMasterResponse? data)> CreateBillNoConfig(
            CreateBillNoMasterRequest req, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(req.name))
                return ("Configuration name is required.", null);
            if (string.IsNullOrWhiteSpace(req.shortname))
                return ("Short code (prefix, e.g. 'BILL', 'RCP') is required.", null);
            if (req.shortname.Length > 10)
                return ("Short code must be 10 characters or fewer.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Guard against duplicate "type" configs for the same scope when isdefault=true.
                // Only one default bill-number config and one default receipt-number config
                // should exist per tenant (mirrors how CreateBill/GenerateReceiptLog pick "LIMIT 1").
                if (req.isdefault)
                {
                    var clashing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                        @"SELECT * FROM billno_master
                   WHERE tenant_code = @t AND deleted = false
                     AND isreceiptno = @isrcpt AND issampleno = @issample
                     AND isdefault = true
                   LIMIT 1",
                        new { t = tenantCode, isrcpt = req.isreceiptno, issample = req.issampleno }, tx);

                    if (clashing != null)
                        return ($"A default configuration of this type already exists: '{clashing.name}' " +
                                $"(bncode={clashing.bncode}). Update or delete it first, or set isdefault=false.", null);
                }

                // Generate next bncode for this tenant (locked to avoid race conditions)
               // decimal nextCode = await db.ExecuteScalarAsync<decimal>(
               //     @"SELECT COALESCE(MAX(bncode), 0) + 1 FROM billno_master
               //WHERE tenant_code = @t FOR UPDATE",
               //     new { t = tenantCode }, tx);

                 await db.ExecuteAsync(
    "SELECT pg_advisory_xact_lock(hashtext(@t))",
    new { t = tenantCode }, tx);

                // Safe MAX after lock acquired
                decimal nextCode = await db.ExecuteScalarAsync<decimal>(
                    @"SELECT COALESCE(MAX(bncode), 0) + 1 FROM billno_master WHERE tenant_code = @t",
                    new { t = tenantCode }, tx);

                var row = new HmsBillNoMaster
                {
                    bncode = nextCode,
                    orderno = req.orderno,
                    name = req.name,
                    shortname = req.shortname.ToUpper(),
                    bhcode = req.bhcode,
                    cntcode = req.cntcode,
                    isdefault = req.isdefault,
                    allbranch = req.allbranch,
                    allcounter = req.allcounter,
                    restartfinancialyear = req.restartfinancialyear,
                    restartcalendaryear = req.restartcalendaryear,
                    restartmonthly = req.restartmonthly,
                    restartdaily = req.restartdaily,
                    issampleno = req.issampleno,
                    isreceiptno = req.isreceiptno,
                    deleted = false,
                    tenant_code = tenantCode,
                    usercode = req.usercode ?? 0,
                    computercode = req.computercode ?? 0,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow
                };

                await db.InsertAsync(row, tx);
                tx.Commit();

                return ("SUCCESS", MapToResponse(row, 0));
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CreateBillNoConfig failed.");
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        public async Task<(string status, BillNoMasterResponse? data)> UpdateBillNoConfig(
            UpdateBillNoMasterRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                    new { bn = req.bncode, t = tenantCode }, tx);

                if (existing == null) return ("Configuration not found.", null);
                if (existing.deleted) return ("Cannot update a deleted configuration. Restore it first.", null);

                if (req.name != null) existing.name = req.name;
                if (req.shortname != null) existing.shortname = req.shortname.ToUpper();
                if (req.orderno.HasValue) existing.orderno = req.orderno.Value;
                if (req.bhcode.HasValue) existing.bhcode = req.bhcode;
                if (req.cntcode.HasValue) existing.cntcode = req.cntcode;
                if (req.isdefault.HasValue) existing.isdefault = req.isdefault;
                if (req.allbranch.HasValue) existing.allbranch = req.allbranch;
                if (req.allcounter.HasValue) existing.allcounter = req.allcounter;
                if (req.restartfinancialyear.HasValue) existing.restartfinancialyear = req.restartfinancialyear;
                if (req.restartcalendaryear.HasValue) existing.restartcalendaryear = req.restartcalendaryear;
                if (req.restartmonthly.HasValue) existing.restartmonthly = req.restartmonthly;
                if (req.restartdaily.HasValue) existing.restartdaily = req.restartdaily;
                if (req.issampleno.HasValue) existing.issampleno = req.issampleno;
                if (req.isreceiptno.HasValue) existing.isreceiptno = req.isreceiptno;

                // Re-check default clash if isdefault is being turned on
                if (existing.isdefault == true)
                {
                    var clashing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                        @"SELECT * FROM billno_master
                   WHERE tenant_code = @t AND deleted = false AND bncode <> @bn
                     AND isreceiptno = @isrcpt AND issampleno = @issample
                     AND isdefault = true
                   LIMIT 1",
                        new { t = tenantCode, bn = existing.bncode, isrcpt = existing.isreceiptno, issample = existing.issampleno }, tx);

                    if (clashing != null)
                        return ($"Another default configuration of this type already exists: '{clashing.name}'.", null);
                }

                await db.UpdateAsync(existing, tx);
                tx.Commit();

                int inUse = await CountSequenceRows(existing.bncode, tenantCode);
                return ("SUCCESS", MapToResponse(existing, inUse));
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "UpdateBillNoConfig failed for bncode={bn}", req.bncode);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        public async Task<string> DeleteBillNoConfig(DeleteBillNoMasterRequest req, string tenantCode)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                new { bn = req.bncode, t = tenantCode });

            if (existing == null) return "Configuration not found.";
            if (existing.deleted) return "Configuration is already deleted.";

            // Warn rather than block — sequence rows just stop advancing for this config once it's soft-deleted.
            // Bills already created referencing this bncode are unaffected (they keep their own bncode value).
            await db.ExecuteAsync(
                "UPDATE billno_master SET deleted = true WHERE bncode = @bn AND tenant_code = @t",
                new { bn = req.bncode, t = tenantCode });

            return "SUCCESS";
        }

        public async Task<(string status, BillNoMasterResponse? data)> RestoreBillNoConfig(
            decimal bncode, string tenantCode)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });

            if (existing == null) return ("Configuration not found.", null);
            if (!existing.deleted) return ("Configuration is not deleted.", null);

            await db.ExecuteAsync(
                "UPDATE billno_master SET deleted = false WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });

            existing.deleted = false;
            int inUse = await CountSequenceRows(bncode, tenantCode);
            return ("SUCCESS", MapToResponse(existing, inUse));
        }

        public async Task<BillNoMasterResponse?> GetBillNoConfigByCode(decimal bncode, string tenantCode)
        {
            using var db = GetConnection();
            var row = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });

            if (row == null) return null;
            int inUse = await CountSequenceRows(bncode, tenantCode);
            return MapToResponse(row, inUse);
        }

        public async Task<(List<BillNoMasterResponse> data, int totalCount)> GetBillNoConfigList(
            BillNoMasterFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = "WHERE tenant_code = @t ";

            if (filter.includeDeleted != true)
                where += " AND deleted = false ";
            if (filter.isreceiptno.HasValue)
            {
                where += " AND isreceiptno = @isrcpt ";
                p.Add("isrcpt", filter.isreceiptno);
            }
            if (filter.issampleno.HasValue)
            {
                where += " AND issampleno = @issample ";
                p.Add("issample", filter.issampleno);
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += " AND (name ILIKE @s OR shortname ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            int total = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM billno_master {where}", p);

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = (await db.QueryAsync<HmsBillNoMaster>(
                $@"SELECT * FROM billno_master {where}
           ORDER BY isreceiptno ASC, issampleno ASC, bncode ASC
           LIMIT @limit OFFSET @offset", p)).ToList();

            var results = new List<BillNoMasterResponse>();
            foreach (var row in rows)
            {
                int inUse = await CountSequenceRows(row.bncode, tenantCode);
                results.Add(MapToResponse(row, inUse));
            }

            return (results, total);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<int> CountSequenceRows(decimal bncode, string tenantCode)
        {
            using var db = GetConnection();
            return await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM billno_sequence WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });
        }

        private BillNoMasterResponse MapToResponse(HmsBillNoMaster row, int inUse) => new()
        {
            bncode = row.bncode,
            name = row.name,
            shortname = row.shortname,
            orderno = row.orderno,
            bhcode = row.bhcode,
            cntcode = row.cntcode,
            isdefault = row.isdefault,
            allbranch = row.allbranch,
            allcounter = row.allcounter,
            restartfinancialyear = row.restartfinancialyear,
            restartcalendaryear = row.restartcalendaryear,
            restartmonthly = row.restartmonthly,
            restartdaily = row.restartdaily,
            issampleno = row.issampleno,
            isreceiptno = row.isreceiptno,
            deleted = row.deleted,
            tenant_code = row.tenant_code,
            entereddate = row.entereddate,
            sequence_rows_in_use = inUse
        };
        // ════════════════════════════════════════════════════════════════════════
        //  9. DEDICATED BILL UPDATE
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, UpdateHmsBillResponse? data)> UpdateBillDedicated(
            UpdateHmsBillRequest req, string tenantCode)
        {
            // ── Validations ───────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(req.requestguid))
                return ("requestguid is required for update.", null);
            if (string.IsNullOrWhiteSpace(req.patient_name))
                return ("Patient name is required.", null);
            if (req.items == null || !req.items.Any())
                return ("At least one line item is required.", null);

            foreach (var item in req.items)
            {
                if (string.IsNullOrWhiteSpace(item.item_name) && !item.tcode.HasValue)
                    return ("Each item must have either item_name or tcode.", null);
                if ((item.amount ?? 0) < 0)
                    return ("Item amount cannot be negative.", null);
            }

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // ── Fetch existing bill (lock row) ────────────────────────────
                var existing = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    @"SELECT * FROM lab_request_master
               WHERE requestguid = @rg AND tenant_code = @t
               FOR UPDATE",
                    new { rg = req.requestguid, t = tenantCode }, tx);

                if (existing == null)
                    return ("Bill not found.", null);
                if (existing.isdeleted == true || existing.deleted == true)
                    return ("Cannot update a cancelled bill.", null);

                // ── Recalculate amounts ───────────────────────────────────────
                double lineGrossTotal = req.items.Sum(x => x.amount ?? 0);
                double aggregateDiscount = (req.discountamount ?? 0) + (req.specialdiscount ?? 0);
                double netAmount = Math.Max(lineGrossTotal - aggregateDiscount, 0);

                // ── Apply changes to master ───────────────────────────────────
                existing.custid = req.custid ?? existing.custid;
                existing.name = req.patient_name ?? existing.name;
                existing.gender = req.gender ?? existing.gender;
                existing.dateofbirth = req.dateofbirth ?? existing.dateofbirth;
                existing.ageyears = req.ageyears ?? existing.ageyears;
                existing.agemonths = req.agemonths ?? existing.agemonths;
                existing.agedays = req.agedays ?? existing.agedays;
                existing.mobileno = req.mobileno ?? existing.mobileno;
                existing.address = req.address ?? existing.address;
                existing.areacode = req.areacode ?? existing.areacode;
                existing.dcode = req.dcode ?? existing.dcode;
                existing.consultantdcode = req.consultantdcode ?? existing.consultantdcode;
                existing.ftcode = req.ftcode ?? existing.ftcode;
                existing.pmcode = req.pmcode ?? existing.pmcode;
                existing.ctcode = req.ctcode ?? existing.ctcode;
                existing.ricode = req.ricode ?? existing.ricode;
                existing.discountper = req.discountper ?? existing.discountper;
                existing.discountamount = req.discountamount ?? existing.discountamount;
                existing.specialdiscount = req.specialdiscount ?? existing.specialdiscount;
                existing.pmc1 = req.pmc1 ?? existing.pmc1;
                existing.pmc2 = req.pmc2 ?? existing.pmc2;
                existing.pmc3 = req.pmc3 ?? existing.pmc3;
                existing.iscashbill = req.iscashbill ?? existing.iscashbill;
                existing.iscreditbill = req.iscreditbill ?? existing.iscreditbill;
                existing.isinsurancepatient = req.isinsurancepatient ?? existing.isinsurancepatient;
                existing.policyno = req.policyno ?? existing.policyno;
                existing.authorisationno = req.authorisationno ?? existing.authorisationno;
                existing.concessionreason = req.concessionreason ?? existing.concessionreason;
                existing.card_refno = req.card_refno ?? existing.card_refno;
                existing.bank_app = req.bank_app ?? existing.bank_app;
                existing.sheet_id = req.sheet_id ?? existing.sheet_id;
                existing.opvisitid = req.op_id ?? existing.opvisitid;
                existing.alteredbhcode = req.enteredbhcode ?? existing.alteredbhcode;
                existing.requestamount = lineGrossTotal;
                existing.totalamount = netAmount;

                await db.UpdateAsync(existing, tx);

                // ── Delete old detail lines and re-insert ─────────────────────
                await db.ExecuteAsync(
                    @"DELETE FROM lab_request_details
               WHERE requestguid = @rg AND tenant_code = @t",
                    new { rg = req.requestguid, t = tenantCode }, tx);

                int sno = 1;
                foreach (var line in req.items)
                {
                    await db.InsertAsync(new HmsLabRequestDetail
                    {
                        requestdetailsid = Guid.NewGuid().ToString(),
                        requestguid = req.requestguid,
                        testsno = sno++,
                        tcode = line.tcode,
                        chargetype = line.charge_type,
                        item_name = line.item_name,
                        item_ref_id = line.item_ref_id,
                        testrate = line.unit_rate,
                        standardprice = line.unit_rate,
                        testamount = line.amount,
                        discount = line.discount,
                        newamount = (line.amount ?? 0) - (line.discount ?? 0),
                        gstper = line.gst_per,
                        gstamount = ((line.amount ?? 0) - (line.discount ?? 0))
                                           * ((line.gst_per ?? 0) / 100.0),
                        qty = line.qty,
                        ttid = line.ttid,
                        resultstatus = false,
                        requeststatus = true,
                        isdeleted = false,
                        tenant_code = tenantCode
                    }, tx);
                }

                tx.Commit();

                // ── Fetch and return updated bill ─────────────────────────────
                var updated = await FetchBillRecordByGuid(req.requestguid, tenantCode);

                return ("SUCCESS", new UpdateHmsBillResponse
                {
                    requestguid = updated?.requestguid ?? req.requestguid,
                    bill_no = updated?.bill_no,
                    barcode = updated?.barcode,
                    bill_date = updated?.bill_date,
                    custid = updated?.custid,
                    patient_name = updated?.patient_name,
                    gender = updated?.gender,
                    mobileno = updated?.mobileno,
                    ageyears = updated?.ageyears,
                    enteredbhcode = updated?.enteredbhcode,
                    cntcode = updated?.cntcode,
                    gross_amount = updated?.gross_amount,
                    discount_amount = updated?.discount_amount,
                    net_amount = updated?.net_amount,
                    paid_amount = updated?.paid_amount,
                    balance_amount = updated?.balance_amount,
                    is_settled = updated?.is_settled ?? false,
                    message = "Bill updated successfully.",
                    items = updated?.items ?? new()
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "UpdateBillDedicated failed for requestguid={rg}", req.requestguid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }
        public async Task<HmsCounterTimingDto?> GetActiveShiftByBranchCounter(int bhcode, int cntcode, string tenantCode)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<HmsCounterTimingDto>(
                @"SELECT c.*, b.name as counter_name,
                 CASE WHEN c.todate IS NULL THEN true ELSE false END as is_open,
                 CASE WHEN c.todate IS NOT NULL THEN true ELSE false END as is_closed
          FROM counter_timing c
          LEFT JOIN billno_master b ON c.cntcode = b.cntcode AND c.tenant_code = b.tenant_code
          WHERE c.bhcode = @bhcode AND c.cntcode = @cntcode AND c.todate IS NULL AND c.tenant_code = @tenantCode
          LIMIT 1", new { bhcode, cntcode, tenantCode });
        }
    }
}