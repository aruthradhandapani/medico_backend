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
    /// <summary>
    /// HMS Due Collection Service
    ///
    /// Tables used (all confirmed against real DDL):
    ///   lab_request_master        — bill master; paidamount is updated on every settlement
    ///   receipt_master            — one row per payment event
    ///   receipt_details           — links receipt → bill
    ///   receipt_advances          — advance ledger (deposit / usage / refund rows)
    ///   balancecollectionby       — cash-flow ledger (both legacy + new columns written)
    ///   balancecollectionbytest   — test-level settlement flag per tcode
    ///   billno_master             — receipt number sequence configuration
    ///   billno_sequence           — sequence counter (SELECT FOR UPDATE, race-safe)
    ///   counter_timing            — validates open shift before any collection
    ///   doctor_master             — doctor name for preview enrichment
    ///
    /// Advance logic (FIFO):
    ///   Oldest advance deposit is consumed first.
    ///   A deposit can be partially consumed across multiple collections.
    ///   Available balance = SUM(deposit rows) − SUM(usage + refund rows).
    ///
    /// All collection operations run inside a single NpgsqlTransaction.
    /// On any error the transaction is rolled back — the DB is always left consistent.
    /// </summary>
    public class HmsDueCollectionClass
    {
        private readonly string _conn;
        private readonly ILogger<HmsDueCollectionClass> _logger;

        public HmsDueCollectionClass(
            IConfiguration cfg,
            ILogger<HmsDueCollectionClass> logger)
        {
            _conn = cfg.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Connection string 'conn' not found.");
            _logger = logger;
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ═══════════════════════════════════════════════════════════════════════
        //  1. DRY-RUN PREVIEW  (no DB writes)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsDuePreviewResponse? data)> GetDuePreview(
            string requestguid, double requestedAdvance, string tenantCode)
        {
            try
            {
                using var db = GetConnection();

                var bill = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT lrm.*, dm.name AS doctor_name
                        FROM lab_request_master lrm
                        LEFT JOIN doctor_master dm
                               ON dm.dcode = lrm.dcode AND dm.tenant_code = lrm.tenant_code
                       WHERE lrm.requestguid  = @rg
                         AND lrm.tenant_code  = @t
                         AND COALESCE(lrm.isdeleted, false) = false
                         AND COALESCE(lrm.deleted,   false) = false
                       LIMIT 1",
                    new { rg = requestguid, t = tenantCode });

                if (bill == null)
                    return ("Bill not found or has been cancelled.", null);

                double totalBill = bill.totalamount ?? 0;
                double previousPaid = bill.paidamount ?? 0;
                double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                if (currentDue <= 0.01)
                    return ("This bill has no outstanding due amount.", null);

                double advanceAvail = await GetPatientAdvanceBalance(
                    db, (decimal?)bill.custid, tenantCode);

                double advanceToUse = Math.Round(
                    Math.Min(requestedAdvance, Math.Min(advanceAvail, currentDue)), 2);
                double cashNeeded = Math.Round(Math.Max(currentDue - advanceToUse, 0), 2);
                double dueAfter = Math.Round(Math.Max(currentDue - advanceToUse - cashNeeded, 0), 2);

                return ("SUCCESS", new HmsDuePreviewResponse
                {
                    requestguid = requestguid,
                    bill_no = bill.requestsnoprint,
                    bill_date = bill.requestdatetime,
                    bill_type = bill.bill_category,
                    custid = bill.custid,
                    patient_name = bill.name,
                    mobileno = bill.mobileno,
                    opvisitid = bill.opvisitid,
                    doctor_name = bill.doctor_name,
                    dcode = bill.dcode == null ? (int?)null : Convert.ToInt32((decimal)bill.dcode),
                    total_bill_amount = totalBill,
                    previous_paid = previousPaid,
                    current_due = currentDue,
                    advance_available = advanceAvail,
                    advance_to_use = advanceToUse,
                    amount_to_collect = cashNeeded,
                    due_after = dueAfter
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDuePreview failed for requestguid={rg}", requestguid);
                return (ex.Message, null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  2. COLLECT DUE — atomic transaction
        //
        //  Scenario 1 — Cash only     : advance_to_use=0,    cash_collected=3000
        //  Scenario 2 — Advance only  : advance_to_use=2000, cash_collected=0
        //  Scenario 3 — Both          : advance_to_use=2000, cash_collected=1000
        //
        //  Advance logic:
        //    - Deducts from receipt_advances deposit rows (FIFO)
        //    - Writes usage rows so remaining advance balance is trackable
        //    - Cancel revert soft-deletes usage rows → balance is restored
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsDueCollectionResponse? data)> CollectDue(
            HmsDueCollectionRequest req, string tenantCode)
        {
            var validErr = ValidateDueRequest(req);
            if (validErr != null) return (validErr, null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Lock bill row to prevent concurrent collection
                var bill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    @"SELECT * FROM lab_request_master
                       WHERE requestguid = @rg AND tenant_code = @t
                       FOR UPDATE",
                    new { rg = req.requestguid, t = tenantCode }, tx);

                if (bill == null)
                    return ("Bill not found.", null);
                if (bill.isdeleted == true || bill.deleted == true)
                    return ("Cannot collect due on a cancelled bill.", null);

                double totalBill = bill.totalamount ?? 0;
                double previousPaid = bill.paidamount ?? 0;
                double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                if (currentDue <= 0.01)
                    return ("This bill has no outstanding due amount.", null);

                // Validate advance inside transaction (balance locked via advisory or row lock)
                double advanceBalance = await GetPatientAdvanceBalance(db, bill.custid, tenantCode, tx);
                double advanceToUse = Math.Round(req.advance_to_use, 2);
                double cashCollected = Math.Round(req.cash_collected, 2);

                if (advanceToUse > advanceBalance + 0.01)
                    return ($"Advance requested ({advanceToUse:F2}) exceeds " +
                            $"available advance ({advanceBalance:F2}).", null);
                if (advanceToUse > currentDue + 0.01)
                    return ($"Advance requested ({advanceToUse:F2}) exceeds " +
                            $"current due ({currentDue:F2}).", null);

                double dueAfterAdvance = Math.Round(Math.Max(currentDue - advanceToUse, 0), 2);

                if (cashCollected > dueAfterAdvance + 0.01)
                    return ($"Payment ({cashCollected:F2}) exceeds remaining due " +
                            $"after advance adjustment ({dueAfterAdvance:F2}).", null);

                double totalSettled = Math.Round(advanceToUse + cashCollected, 2);
                double dueAfter = Math.Round(Math.Max(currentDue - totalSettled, 0), 2);

                // Validate open counter shift
                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t
                       LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found for this branch/counter. " +
                            "Please open a shift before collecting.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
                       WHERE isreceiptno = true AND deleted = false AND tenant_code = @t
                       LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found in billno_master.", null);

                string receiptGuid = Guid.NewGuid().ToString();
                string? advReceiptGuid = null;
                HmsNumberResult? seqInfo = null;

                // ── A. CASH / CARD / UPI / CHEQUE / BANK receipt ──────────────
                if (cashCollected > 0.01)
                {
                    seqInfo = await GetNextReceiptSequence(
                        db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                    var cashReceipt = new HmsDueReceiptMaster
                    {
                        receiptguid = receiptGuid,
                        receiptdate = DateTime.UtcNow,
                        receiptsno = seqInfo.sno,
                        receiptsnoprint = seqInfo.snoprint,
                        receiptbarcode = seqInfo.barcode,
                        receiptcovertedbarcode = seqInfo.barcode,
                        cntcode = req.cntcode,
                        cnttid = shift.cnttid,
                        tmcode = req.tmcode ?? bill.tmcode,
                        pmcode = req.pmcode ?? bill.pmcode,
                        ctcode = req.ctcode ?? bill.ctcode,
                        bankname = req.bank_name,
                        paymentreference = req.reference_no,
                        cardno = req.card_no,
                        chequedate = req.cheque_date,
                        amountpaid = cashCollected,
                        amountadjusted = cashCollected,
                        amounttotal = cashCollected,
                        deleted = false,
                        isdeleted = false,
                        isbill = true,
                        ispatient = true,
                        isrefund = false,
                        receipttype = "DUE",
                        custid = (int?)bill.custid,
                        opvisitid = bill.opvisitid,
                        enteredbhcode = req.enteredbhcode,
                        usercode = req.usercode,
                        computercode = req.computercode,
                        entereddate = DateTime.UtcNow,
                        ibsdate = DateTime.UtcNow,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(cashReceipt, tx);

                    var detail = new HmsDueReceiptDetail
                    {
                        receiptdetailsid = Guid.NewGuid().ToString(),
                        receiptguid = receiptGuid,
                        requestguid = req.requestguid,
                        receiptamount = cashCollected,
                        discount_amount = 0,
                        refund_amount = 0,
                        deleted = false,
                        usercode = req.usercode,
                        computercode = req.computercode,
                        entereddate = DateTime.UtcNow,
                        ibsdate = DateTime.UtcNow,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(detail, tx);

                    await InsertBalanceCollectionRow(
                        db, tx,
                        bhcode: req.enteredbhcode,
                        collectionType: req.collection_type.ToUpper(),
                        receiptGuid: receiptGuid,
                        requestGuid: req.requestguid,
                        amount: cashCollected,
                        shift: shift,
                        req: req,
                        bill: bill,
                        tenantCode: tenantCode);
                }

                // ── B. ADVANCE ADJUSTMENT (FIFO) ─────────────────────────────
                //
                //  For each deposit consumed:
                //    1. Write a receipt_advances USAGE row
                //       → receiptguid  = original deposit receipt guid (links back to deposit)
                //       → requestguid  = current bill guid (marks it as "used against this bill")
                //       → receiptamount = amount deducted from this deposit
                //    2. Write a balancecollectionby row with collection_type='ADVANCE'
                //       so daily reports include the advance adjustment
                //
                //  This means:
                //    Available balance = SUM(rows where requestguid IS NULL)
                //                     − SUM(rows where requestguid IS NOT NULL)
                //  On cancel: soft-delete the usage rows → balance is restored automatically
                // ─────────────────────────────────────────────────────────────
                if (advanceToUse > 0.01)
                {
                    var deposits = (await db.QueryAsync<dynamic>(
                        @"SELECT ra.receiptadvanceid,
                                 ra.receiptguid,
                                 ra.receiptamount,
                                 COALESCE(
                                     (SELECT SUM(u.receiptamount)
                                        FROM receipt_advances u
                                       WHERE u.receiptguid  = ra.receiptguid
                                         AND u.requestguid IS NOT NULL
                                         AND COALESCE(u.deleted, false) = false),
                                 0) AS used_amount
                            FROM receipt_advances ra
                           WHERE ra.requestguid IS NULL
                             AND COALESCE(ra.deleted, false) = false
                             AND ra.receiptguid IN (
                                 SELECT rm.receiptguid
                                   FROM receipt_master rm
                                  WHERE rm.custid      = @custid
                                    AND rm.tenant_code = @t
                                    AND rm.receipttype = 'ADVANCE'
                                    AND COALESCE(rm.isdeleted, false) = false
                             )
                           ORDER BY ra.receiptadvanceid ASC",
                        new { custid = (int?)bill.custid, t = tenantCode }, tx)).ToList();

                    double remaining = advanceToUse;

                    foreach (var dep in deposits)
                    {
                        if (remaining <= 0.01) break;

                        double available = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                        if (available <= 0.01) continue;

                        double debit = Math.Round(Math.Min(available, remaining), 2);

                        // Write usage row — links this deposit to this bill
                        // Cancelling will soft-delete this row → restores the balance
                        await db.ExecuteAsync(
                            @"INSERT INTO receipt_advances
                                (receiptadvanceid, receiptguid, requestguid, receiptamount,
                                 deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                              VALUES
                                (@id, @rg, @billGuid, @amt,
                                 false, @t, @uc, @cc, @dt, @dt)",
                            new
                            {
                                id = Guid.NewGuid().ToString(),
                                rg = (string)dep.receiptguid,       // original deposit receipt
                                billGuid = req.requestguid,         // bill this advance is used for
                                amt = debit,
                                t = tenantCode,
                                uc = req.usercode,
                                cc = req.computercode,
                                dt = DateTime.UtcNow
                            }, tx);

                        // Also write balancecollectionby so daily reports include advance usage
                        await InsertBalanceCollectionRow(
                            db, tx,
                            bhcode: req.enteredbhcode,
                            collectionType: "ADVANCE",
                            receiptGuid: (string)dep.receiptguid,
                            requestGuid: req.requestguid,
                            amount: debit,
                            shift: shift,
                            req: req,
                            bill: bill,
                            tenantCode: tenantCode);

                        if (advReceiptGuid == null)
                            advReceiptGuid = (string)dep.receiptguid;

                        remaining = Math.Round(remaining - debit, 2);
                    }

                    if (remaining > 0.01)
                    {
                        tx.Rollback();
                        return ("Advance balance insufficient to complete adjustment. " +
                                "Please refresh and try again.", null);
                    }
                }

                // ── C. Update lab_request_master ──────────────────────────────
                await db.ExecuteAsync(
                    @"UPDATE lab_request_master
                         SET paidamount     = COALESCE(paidamount,     0) + @total,
                             paidviareceipt = COALESCE(paidviareceipt, 0) + @cash
                       WHERE requestguid = @rg AND tenant_code = @t",
                    new
                    {
                        total = totalSettled,
                        cash = cashCollected,
                        rg = req.requestguid,
                        t = tenantCode
                    }, tx);

                // ── D. Mark tests settled when bill is fully paid ──────────────
                if (dueAfter <= 0.05)
                {
                    await db.ExecuteAsync(
                        @"UPDATE balancecollectionbytest
                             SET requeststatus = true
                           WHERE balancecollectionbyid IN (
                               SELECT balancecollectionbyid
                                 FROM balancecollectionby
                                WHERE request_guid = @rg
                                  AND tenant_code  = @t
                                  AND COALESCE(deleted, false) = false
                           )",
                        new { rg = req.requestguid, t = tenantCode }, tx);
                }

                tx.Commit();

                return ("SUCCESS", new HmsDueCollectionResponse
                {
                    receipt_guid = cashCollected > 0.01 ? receiptGuid : null,
                    receipt_no = cashCollected > 0.01 ? seqInfo?.snoprint : null,
                    receipt_barcode = cashCollected > 0.01 ? seqInfo?.barcode : null,
                    advance_receipt_guid = advReceiptGuid,
                    requestguid = req.requestguid,
                    bill_no = bill.requestsnoprint,
                    patient_name = bill.name,
                    mobileno = bill.mobileno,
                    total_bill_amount = totalBill,
                    due_before = currentDue,
                    advance_used = advanceToUse,
                    cash_collected = cashCollected,
                    total_settled = totalSettled,
                    due_after = dueAfter,
                    is_fully_settled = dueAfter <= 0.05,
                    collection_type = req.collection_type.ToUpper(),
                    reference_no = req.reference_no,
                    collected_date = DateTime.UtcNow,
                    remarks = req.remarks
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CollectDue transaction failed for requestguid={rg}", req.requestguid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  2b. BULK COLLECT DUE
        //
        //  Collects payment for multiple bills in a single atomic transaction.
        //  One shared receipt_master row is created for the batch.
        //  Each bill gets its own receipt_details and balancecollectionby row.
        //
        //  Advance (FIFO) is applied per-bill in order — if advance runs out
        //  mid-batch, remaining bills use cash only.
        //
        //  POST /api/HmsDueCollection/collect/bulk
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsBulkDueCollectionResponse? data)> BulkCollectDue(
    HmsBulkDueCollectionRequest req, string tenantCode)
        {
            if (req.items == null || !req.items.Any())
                return ("No items provided for bulk collection.", null);

            if (req.enteredbhcode == null || req.cntcode == null)
                return ("Branch code (enteredbhcode) and counter code (cntcode) are required.", null);
            if (req.usercode == null)
                return ("usercode is required.", null);
            if (string.IsNullOrWhiteSpace(req.collection_type))
                return ("collection_type is required.", null);

            var refRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "CARD", "UPI", "CHEQUE", "BANK" };

            // Validate every item's EFFECTIVE payment mode (item override, else batch default)
            foreach (var item in req.items)
            {
                if (string.IsNullOrWhiteSpace(item.requestguid))
                    return ("Each item must include requestguid.", null);
                if (item.advance_to_use < 0)
                    return ($"Advance amount cannot be negative for bill {item.requestguid}.", null);
                if (item.cash_collected < 0)
                    return ($"Cash collected cannot be negative for bill {item.requestguid}.", null);

                string effType = (item.collection_type ?? req.collection_type).ToUpper();
                string? effRef = item.reference_no ?? req.reference_no;

                if (item.cash_collected > 0
                    && refRequired.Contains(effType)
                    && string.IsNullOrWhiteSpace(effRef))
                {
                    return ($"Transaction reference number is required for bill {item.requestguid} " +
                            $"with payment mode '{effType}'.", null);
                }
            }

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
               WHERE bhcode = @bh AND cntcode = @cnt
                 AND todate IS NULL AND tenant_code = @t
               LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found. Please open a shift before collecting.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
               WHERE isreceiptno = true AND deleted = false AND tenant_code = @t
               LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found in billno_master.", null);

                var seqInfo = await GetNextReceiptSequence(
                    db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                string batchReceiptGuid = Guid.NewGuid().ToString();
                double batchCashTotal = req.items.Sum(i => Math.Round(i.cash_collected, 2));

                // Shared receipt_master header — uses batch-level payment fields.
                // (Per-item overrides are recorded on each bill's balancecollectionby row below;
                //  the receipt header itself can only carry one bank/card/cheque reference.)
                var batchReceipt = new HmsDueReceiptMaster
                {
                    receiptguid = batchReceiptGuid,
                    receiptdate = DateTime.UtcNow,
                    receiptsno = seqInfo.sno,
                    receiptsnoprint = seqInfo.snoprint,
                    receiptbarcode = seqInfo.barcode,
                    receiptcovertedbarcode = seqInfo.barcode,
                    cntcode = req.cntcode,
                    cnttid = shift.cnttid,
                    tmcode = req.tmcode,
                    pmcode = req.pmcode,
                    ctcode = req.ctcode,
                    bankname = req.bank_name,
                    paymentreference = req.reference_no,
                    cardno = req.card_no,
                    chequedate = req.cheque_date,
                    amountpaid = batchCashTotal,
                    amountadjusted = batchCashTotal,
                    amounttotal = batchCashTotal,
                    deleted = false,
                    isdeleted = false,
                    isbill = true,
                    ispatient = true,
                    isrefund = false,
                    receipttype = "DUE",
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(batchReceipt, tx);

                var itemResults = new List<HmsBulkDueItemResult>();
                double batchAdvanceUsed = 0;
                double batchCashCollected = 0;

                foreach (var item in req.items)
                {
                    // Resolve effective per-item values (item override → batch default)
                    string itemCollectionType = (item.collection_type ?? req.collection_type).ToUpper();
                    string? itemReferenceNo = item.reference_no ?? req.reference_no;
                    string? itemBankName = item.bank_name ?? req.bank_name;
                    string? itemCardNo = item.card_no ?? req.card_no;
                    DateTime? itemChequeDate = item.cheque_date ?? req.cheque_date;
                    string? itemRemarks = item.remarks ?? req.remarks;

                    // Lock this bill
                    var bill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                        @"SELECT * FROM lab_request_master
                   WHERE requestguid = @rg AND tenant_code = @t
                   FOR UPDATE",
                        new { rg = item.requestguid, t = tenantCode }, tx);

                    if (bill == null)
                    {
                        itemResults.Add(new HmsBulkDueItemResult
                        {
                            requestguid = item.requestguid,
                            status = "skipped",
                            message = "Bill not found.",
                            collection_type = itemCollectionType,
                            reference_no = itemReferenceNo,
                            bank_name = itemBankName,
                            card_no = itemCardNo,
                            cheque_date = itemChequeDate,
                            remarks = itemRemarks
                        });
                        continue;
                    }

                    if (bill.isdeleted == true || bill.deleted == true)
                    {
                        itemResults.Add(new HmsBulkDueItemResult
                        {
                            requestguid = item.requestguid,
                            bill_no = bill.requestsnoprint,
                            patient_name = bill.name,
                            status = "skipped",
                            message = "Bill is cancelled.",
                            collection_type = itemCollectionType,
                            reference_no = itemReferenceNo,
                            bank_name = itemBankName,
                            card_no = itemCardNo,
                            cheque_date = itemChequeDate,
                            remarks = itemRemarks
                        });
                        continue;
                    }

                    double totalBill = bill.totalamount ?? 0;
                    double previousPaid = bill.paidamount ?? 0;
                    double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                    if (currentDue <= 0.01)
                    {
                        itemResults.Add(new HmsBulkDueItemResult
                        {
                            requestguid = item.requestguid,
                            bill_no = bill.requestsnoprint,
                            patient_name = bill.name,
                            status = "skipped",
                            message = "No outstanding due.",
                            due_before = 0,
                            due_after = 0,
                            is_fully_settled = true,
                            collection_type = itemCollectionType,
                            reference_no = itemReferenceNo,
                            bank_name = itemBankName,
                            card_no = itemCardNo,
                            cheque_date = itemChequeDate,
                            remarks = itemRemarks
                        });
                        continue;
                    }

                    double itemAdvanceToUse = Math.Round(item.advance_to_use, 2);
                    double itemCashCollected = Math.Round(item.cash_collected, 2);

                    // Validate / clamp advance for this item
                    if (itemAdvanceToUse > 0.01)
                    {
                        double advBal = await GetPatientAdvanceBalance(db, bill.custid, tenantCode, tx);
                        if (itemAdvanceToUse > advBal + 0.01)
                            itemAdvanceToUse = Math.Round(advBal, 2);
                        if (itemAdvanceToUse > currentDue + 0.01)
                            itemAdvanceToUse = Math.Round(currentDue, 2);
                    }

                    double dueAfterAdvance = Math.Round(Math.Max(currentDue - itemAdvanceToUse, 0), 2);

                    if (itemCashCollected > dueAfterAdvance + 0.01)
                        itemCashCollected = Math.Round(dueAfterAdvance, 2); // clamp

                    double itemTotalSettled = Math.Round(itemAdvanceToUse + itemCashCollected, 2);
                    double itemDueAfter = Math.Round(Math.Max(currentDue - itemTotalSettled, 0), 2);

                    // receipt_details — link this bill to the batch receipt
                    await db.InsertAsync(new HmsDueReceiptDetail
                    {
                        receiptdetailsid = Guid.NewGuid().ToString(),
                        receiptguid = batchReceiptGuid,
                        requestguid = item.requestguid,
                        receiptamount = itemCashCollected,
                        discount_amount = 0,
                        refund_amount = 0,
                        deleted = false,
                        usercode = req.usercode,
                        computercode = req.computercode,
                        entereddate = DateTime.UtcNow,
                        ibsdate = DateTime.UtcNow,
                        tenant_code = tenantCode
                    }, tx);

                    // balancecollectionby — cash portion (per-item payment mode/reference)
                    if (itemCashCollected > 0.01)
                    {
                        await db.ExecuteAsync(
                            @"INSERT INTO balancecollectionby (
                          balancecollectionbyid,
                          bhcode,
                          collecteddate, collectiontype, receiptguid, requestguid,
                          collected_date, collection_type, receipt_guid, request_guid,
                          collectedamount, deleted,
                          usercode, computercode,
                          entereddate, ibsdate,
                          tmcode, cntcode, cnttid, pmcode, ctcode,
                          tenant_code)
                      VALUES (
                          @id, @bh,
                          @dt, @ct, @rg, @req,
                          @dt, @ct, @rg, @req,
                          @amt, false,
                          @uc, @cc,
                          @dt, @dt,
                          @tm, @cnt, @cnttid, @pm, @cct,
                          @t)",
                            new
                            {
                                id = Guid.NewGuid().ToString(),
                                bh = req.enteredbhcode,
                                dt = DateTime.UtcNow,
                                ct = itemCollectionType,
                                rg = batchReceiptGuid,
                                req = item.requestguid,
                                amt = itemCashCollected,
                                uc = req.usercode,
                                cc = req.computercode,
                                tm = item.tmcode ?? req.tmcode ?? bill.tmcode,
                                cnt = req.cntcode,
                                cnttid = shift.cnttid,
                                pm = item.pmcode ?? req.pmcode ?? bill.pmcode,
                                cct = item.ctcode ?? req.ctcode ?? bill.ctcode,
                                t = tenantCode
                            }, tx);
                    }

                    // Advance FIFO for this item
                    string? itemAdvReceiptGuid = null;
                    if (itemAdvanceToUse > 0.01)
                    {
                        var deposits = (await db.QueryAsync<dynamic>(
                            @"SELECT ra.receiptadvanceid,
                             ra.receiptguid,
                             ra.receiptamount,
                             COALESCE(
                                 (SELECT SUM(u.receiptamount)
                                    FROM receipt_advances u
                                   WHERE u.receiptguid  = ra.receiptguid
                                     AND u.requestguid IS NOT NULL
                                     AND COALESCE(u.deleted, false) = false),
                             0) AS used_amount
                        FROM receipt_advances ra
                       WHERE ra.requestguid IS NULL
                         AND COALESCE(ra.deleted, false) = false
                         AND ra.receiptguid IN (
                             SELECT rm.receiptguid
                               FROM receipt_master rm
                              WHERE rm.custid      = @custid
                                AND rm.tenant_code = @t
                                AND rm.receipttype = 'ADVANCE'
                                AND COALESCE(rm.isdeleted, false) = false
                         )
                       ORDER BY ra.receiptadvanceid ASC",
                            new { custid = (int?)bill.custid, t = tenantCode }, tx)).ToList();

                        double remaining = itemAdvanceToUse;

                        foreach (var dep in deposits)
                        {
                            if (remaining <= 0.01) break;

                            double avail = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                            if (avail <= 0.01) continue;

                            double debit = Math.Round(Math.Min(avail, remaining), 2);

                            await db.ExecuteAsync(
                                @"INSERT INTO receipt_advances
                            (receiptadvanceid, receiptguid, requestguid, receiptamount,
                             deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                          VALUES
                            (@id, @rg, @billGuid, @amt,
                             false, @t, @uc, @cc, @dt, @dt)",
                                new
                                {
                                    id = Guid.NewGuid().ToString(),
                                    rg = (string)dep.receiptguid,
                                    billGuid = item.requestguid,
                                    amt = debit,
                                    t = tenantCode,
                                    uc = req.usercode,
                                    cc = req.computercode,
                                    dt = DateTime.UtcNow
                                }, tx);

                            await db.ExecuteAsync(
                                @"INSERT INTO balancecollectionby (
                              balancecollectionbyid,
                              bhcode,
                              collecteddate, collectiontype, receiptguid, requestguid,
                              collected_date, collection_type, receipt_guid, request_guid,
                              collectedamount, deleted,
                              usercode, computercode,
                              entereddate, ibsdate,
                              tmcode, cntcode, cnttid, pmcode, ctcode,
                              tenant_code)
                          VALUES (
                              @id, @bh,
                              @dt, 'ADVANCE', @rg, @req,
                              @dt, 'ADVANCE', @rg, @req,
                              @amt, false,
                              @uc, @cc,
                              @dt, @dt,
                              @tm, @cnt, @cnttid, @pm, @cct,
                              @t)",
                                new
                                {
                                    id = Guid.NewGuid().ToString(),
                                    bh = req.enteredbhcode,
                                    dt = DateTime.UtcNow,
                                    rg = (string)dep.receiptguid,
                                    req = item.requestguid,
                                    amt = debit,
                                    uc = req.usercode,
                                    cc = req.computercode,
                                    tm = item.tmcode ?? req.tmcode ?? bill.tmcode,
                                    cnt = req.cntcode,
                                    cnttid = shift.cnttid,
                                    pm = item.pmcode ?? req.pmcode ?? bill.pmcode,
                                    cct = item.ctcode ?? req.ctcode ?? bill.ctcode,
                                    t = tenantCode
                                }, tx);

                            if (itemAdvReceiptGuid == null)
                                itemAdvReceiptGuid = (string)dep.receiptguid;

                            remaining = Math.Round(remaining - debit, 2);
                        }

                        itemAdvanceToUse = Math.Round(itemAdvanceToUse - remaining, 2);
                        itemTotalSettled = Math.Round(itemAdvanceToUse + itemCashCollected, 2);
                        itemDueAfter = Math.Round(Math.Max(currentDue - itemTotalSettled, 0), 2);
                    }

                    // Update lab_request_master
                    await db.ExecuteAsync(
                        @"UPDATE lab_request_master
                     SET paidamount     = COALESCE(paidamount,     0) + @total,
                         paidviareceipt = COALESCE(paidviareceipt, 0) + @cash
                   WHERE requestguid = @rg AND tenant_code = @t",
                        new
                        {
                            total = itemTotalSettled,
                            cash = itemCashCollected,
                            rg = item.requestguid,
                            t = tenantCode
                        }, tx);

                    // Mark tests settled if fully paid
                    if (itemDueAfter <= 0.05)
                    {
                        await db.ExecuteAsync(
                            @"UPDATE balancecollectionbytest
                         SET requeststatus = true
                       WHERE balancecollectionbyid IN (
                           SELECT balancecollectionbyid
                             FROM balancecollectionby
                            WHERE request_guid = @rg
                              AND tenant_code  = @t
                              AND COALESCE(deleted, false) = false
                       )",
                            new { rg = item.requestguid, t = tenantCode }, tx);
                    }

                    batchAdvanceUsed += itemAdvanceToUse;
                    batchCashCollected += itemCashCollected;

                    itemResults.Add(new HmsBulkDueItemResult
                    {
                        requestguid = item.requestguid,
                        bill_no = bill.requestsnoprint,
                        patient_name = bill.name,
                        advance_used = itemAdvanceToUse,
                        cash_collected = itemCashCollected,
                        total_settled = itemTotalSettled,
                        due_before = currentDue,
                        due_after = itemDueAfter,
                        is_fully_settled = itemDueAfter <= 0.05,
                        advance_receipt_guid = itemAdvReceiptGuid,
                        collection_type = itemCollectionType,
                        reference_no = itemReferenceNo,
                        bank_name = itemBankName,
                        card_no = itemCardNo,
                        cheque_date = itemChequeDate,
                        remarks = itemRemarks,
                        status = "collected",
                        message = "OK"
                    });
                }

                tx.Commit();

                return ("SUCCESS", new HmsBulkDueCollectionResponse
                {
                    batch_receipt_guid = batchReceiptGuid,
                    receipt_no = seqInfo.snoprint,
                    receipt_barcode = seqInfo.barcode,
                    collection_type = req.collection_type.ToUpper(),
                    total_advance_used = Math.Round(batchAdvanceUsed, 2),
                    total_cash_collected = Math.Round(batchCashCollected, 2),
                    total_settled = Math.Round(batchAdvanceUsed + batchCashCollected, 2),
                    items_processed = itemResults.Count(r => r.status == "collected"),
                    items_skipped = itemResults.Count(r => r.status == "skipped"),
                    collected_date = DateTime.UtcNow,
                    items = itemResults
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "BulkCollectDue failed");
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  3. DEPOSIT ADVANCE
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsAdvanceReceiptResponse? data)> DepositAdvance(
            HmsAdvanceDepositRequest req, string tenantCode)
        {
            if (req.amount <= 0)
                return ("Advance deposit amount must be greater than zero.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found. Please open a shift first.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
                       WHERE isreceiptno = true AND deleted = false AND tenant_code = @t LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found.", null);

                var seqInfo = await GetNextReceiptSequence(
                    db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                string receiptGuid = Guid.NewGuid().ToString();

                var receipt = new HmsDueReceiptMaster
                {
                    receiptguid = receiptGuid,
                    receiptdate = DateTime.UtcNow,
                    receiptsno = seqInfo.sno,
                    receiptsnoprint = seqInfo.snoprint,
                    receiptbarcode = seqInfo.barcode,
                    receiptcovertedbarcode = seqInfo.barcode,
                    cntcode = req.cntcode,
                    cnttid = shift.cnttid,
                    tmcode = req.tmcode,
                    pmcode = req.pmcode,
                    ctcode = req.ctcode,
                    bankname = req.bank_name,
                    paymentreference = req.reference_no,
                    cardno = req.card_no,
                    chequedate = req.cheque_date,
                    amountpaid = req.amount,
                    amountadjusted = req.amount,
                    amounttotal = req.amount,
                    deleted = false,
                    isdeleted = false,
                    isbill = false,
                    ispatient = true,
                    isrefund = false,
                    receipttype = "ADVANCE",
                    custid = (int)req.custid,
                    opvisitid = req.opvisitid,
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(receipt, tx);

                // Deposit row — requestguid NULL = unallocated credit
                await db.ExecuteAsync(
                    @"INSERT INTO receipt_advances
                        (receiptadvanceid, receiptguid, requestguid, receiptamount,
                         deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                      VALUES
                        (@id, @rg, NULL, @amt, false, @t, @uc, @cc, @dt, @dt)",
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        rg = receiptGuid,
                        amt = req.amount,
                        t = tenantCode,
                        uc = req.usercode,
                        cc = req.computercode,
                        dt = DateTime.UtcNow
                    }, tx);

                // balancecollectionby — advance deposit shows in daily cash collection
                await db.ExecuteAsync(
                    @"INSERT INTO balancecollectionby (
                          balancecollectionbyid,
                          bhcode, collecteddate, collectiontype, receiptguid, requestguid,
                          collected_date, collection_type, receipt_guid, request_guid,
                          collectedamount, deleted, usercode, computercode,
                          entereddate, ibsdate, tmcode, cntcode, cnttid, pmcode, ctcode,
                          tenant_code)
                      VALUES (
                          @id,
                          @bh, @dt, @ct, @rg, NULL,
                          @dt, @ct, @rg, NULL,
                          @amt, false, @uc, @cc,
                          @dt, @dt, @tm, @cnt, @cnttid, @pm, @cct,
                          @t)",
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        bh = req.enteredbhcode,
                        dt = DateTime.UtcNow,
                        ct = req.collection_type.ToUpper(),
                        rg = receiptGuid,
                        amt = req.amount,
                        uc = req.usercode,
                        cc = req.computercode,
                        tm = req.tmcode,
                        cnt = req.cntcode,
                        cnttid = shift.cnttid,
                        pm = req.pmcode,
                        cct = req.ctcode,
                        t = tenantCode
                    }, tx);

                tx.Commit();

                return ("SUCCESS", new HmsAdvanceReceiptResponse
                {
                    receipt_guid = receiptGuid,
                    receipt_no = seqInfo.snoprint,
                    receipt_barcode = seqInfo.barcode,
                    receipt_type = "ADVANCE",
                    custid = req.custid,
                    patient_name = req.patient_name,
                    amount = req.amount,
                    collection_type = req.collection_type.ToUpper(),
                    receipt_date = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "DepositAdvance failed for custid={c}", req.custid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  4. REFUND ADVANCE
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsAdvanceReceiptResponse? data)> RefundAdvance(
            HmsAdvanceRefundRequest req, string tenantCode)
        {
            if (req.refund_amount <= 0)
                return ("Refund amount must be greater than zero.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                double available = await GetPatientAdvanceBalance(db, req.custid, tenantCode, tx);

                if (req.refund_amount > available + 0.01)
                    return ($"Refund amount ({req.refund_amount:F2}) exceeds " +
                            $"available advance balance ({available:F2}).", null);

                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
                       WHERE isreceiptno = true AND deleted = false AND tenant_code = @t LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt sequence configuration not found.", null);

                var seqInfo = await GetNextReceiptSequence(
                    db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                string receiptGuid = Guid.NewGuid().ToString();

                var refundReceipt = new HmsDueReceiptMaster
                {
                    receiptguid = receiptGuid,
                    receiptdate = DateTime.UtcNow,
                    receiptsno = seqInfo.sno,
                    receiptsnoprint = seqInfo.snoprint,
                    receiptbarcode = seqInfo.barcode,
                    receiptcovertedbarcode = seqInfo.barcode,
                    cntcode = req.cntcode,
                    cnttid = shift.cnttid,
                    amountpaid = req.refund_amount,
                    amountadjusted = req.refund_amount,
                    amounttotal = req.refund_amount,
                    deleted = false,
                    isdeleted = false,
                    isbill = false,
                    ispatient = true,
                    isrefund = true,
                    receipttype = "ADVANCE_REFUND",
                    custid = (int)req.custid,
                    opvisitid = req.opvisitid,
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(refundReceipt, tx);

                var deposits = (await db.QueryAsync<dynamic>(
                    @"SELECT ra.receiptadvanceid,
                             ra.receiptguid,
                             ra.receiptamount,
                             COALESCE(
                                 (SELECT SUM(u.receiptamount)
                                    FROM receipt_advances u
                                   WHERE u.receiptguid  = ra.receiptguid
                                     AND u.requestguid IS NOT NULL
                                     AND COALESCE(u.deleted, false) = false),
                             0) AS used_amount
                        FROM receipt_advances ra
                       WHERE ra.requestguid IS NULL
                         AND COALESCE(ra.deleted, false) = false
                         AND ra.receiptguid IN (
                             SELECT rm.receiptguid FROM receipt_master rm
                              WHERE rm.custid      = @custid
                                AND rm.tenant_code = @t
                                AND rm.receipttype = 'ADVANCE'
                                AND COALESCE(rm.isdeleted, false) = false
                         )
                       ORDER BY ra.receiptadvanceid ASC",
                    new { custid = (int)req.custid, t = tenantCode }, tx)).ToList();

                double remaining = req.refund_amount;

                foreach (var dep in deposits)
                {
                    if (remaining <= 0.01) break;

                    double avail = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                    if (avail <= 0.01) continue;

                    double deduct = Math.Round(Math.Min(avail, remaining), 2);

                    await db.ExecuteAsync(
                        @"INSERT INTO receipt_advances
                            (receiptadvanceid, receiptguid, requestguid, receiptamount,
                             deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                          VALUES
                            (@id, @rg, @refundGuid, @amt,
                             false, @t, @uc, @cc, @dt, @dt)",
                        new
                        {
                            id = Guid.NewGuid().ToString(),
                            rg = (string)dep.receiptguid,
                            refundGuid = receiptGuid,
                            amt = deduct,
                            t = tenantCode,
                            uc = req.usercode,
                            cc = req.computercode,
                            dt = DateTime.UtcNow
                        }, tx);

                    remaining = Math.Round(remaining - deduct, 2);
                }

                tx.Commit();

                return ("SUCCESS", new HmsAdvanceReceiptResponse
                {
                    receipt_guid = receiptGuid,
                    receipt_no = seqInfo.snoprint,
                    receipt_barcode = seqInfo.barcode,
                    receipt_type = "ADVANCE_REFUND",
                    custid = req.custid,
                    amount = req.refund_amount,
                    receipt_date = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "RefundAdvance failed for custid={c}", req.custid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  5. CANCEL DUE COLLECTION RECEIPT
        //
        //  Example: Bill = 1200, paid 300, due = 900
        //    Cancel receipt → soft-delete receipt rows
        //                   → revert paidamount by 300
        //                   → bill due goes back to 1200
        //    If advance was used in that collection:
        //                   → soft-delete usage rows in receipt_advances
        //                   → advance balance is restored automatically
        //
        //  The amount to revert is read from balancecollectionby rows tied to
        //  this receipt (cash + advance rows combined) so the revert is always
        //  exact regardless of how the payment was split.
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<string> CancelDueCollection(
            string receiptGuid, int? usercode, string? reason, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var receipt = await db.QueryFirstOrDefaultAsync<HmsDueReceiptMaster>(
                    @"SELECT * FROM receipt_master
                       WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                if (receipt == null) return "Receipt not found.";
                if (receipt.isdeleted == true) return "Receipt is already cancelled.";
                if (receipt.receipttype != "DUE")
                    return "Only DUE receipts can be cancelled through this endpoint. " +
                           "Use the advance refund endpoint for ADVANCE receipts.";

                // Get bill linkage
                var detail = await db.QueryFirstOrDefaultAsync<HmsDueReceiptDetail>(
                    @"SELECT * FROM receipt_details
                       WHERE receiptguid = @rg AND tenant_code = @t
                         AND COALESCE(deleted, false) = false
                       LIMIT 1",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                string? requestGuid = detail?.requestguid;

                // ── 1. Restore advance usage rows ─────────────────────────────
                //
                //  Find all ADVANCE balancecollectionby rows for this bill that
                //  were written during this collection, then soft-delete the
                //  corresponding receipt_advances usage rows.
                //
                //  After soft-delete:
                //    GetPatientAdvanceBalance → deposits unchanged, used reduced
                //    → available balance is automatically restored
                // ─────────────────────────────────────────────────────────────
                if (requestGuid != null)
                {
                    // Find advance balancecollectionby rows written for this bill
                    // We identify them by: collection_type=ADVANCE AND request_guid=bill AND
                    // they were written at the same time as the DUE receipt
                    // (the receipt_guid in those rows points to the original deposit receipt)
                    var advanceRows = await db.QueryAsync<dynamic>(
                        @"SELECT receipt_guid, collectedamount
                            FROM balancecollectionby
                           WHERE request_guid    = @rg
                             AND tenant_code     = @t
                             AND collection_type = 'ADVANCE'
                             AND COALESCE(deleted, false) = false",
                        new { rg = requestGuid, t = tenantCode }, tx);

                    foreach (var adv in advanceRows)
                    {
                        // Soft-delete the usage row — this restores the advance balance
                        await db.ExecuteAsync(
                            @"UPDATE receipt_advances
                                 SET deleted = true
                               WHERE receiptguid = @advRg
                                 AND requestguid = @billRg
                                 AND COALESCE(deleted, false) = false",
                            new { advRg = (string)adv.receipt_guid, billRg = requestGuid }, tx);
                    }
                }

                // ── 2. Soft-delete receipt_master, receipt_details, balancecollectionby ──
                await db.ExecuteAsync(
                    @"UPDATE receipt_master
                         SET isdeleted = true, deleted = true
                       WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                await db.ExecuteAsync(
                    @"UPDATE receipt_details
                         SET deleted = true
                       WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                await db.ExecuteAsync(
                    @"UPDATE balancecollectionby
                         SET deleted = true
                       WHERE receipt_guid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                // ── 3. Revert lab_request_master paid amounts ─────────────────
                //
                //  We reconstruct exact amounts from balancecollectionby
                //  (already marked deleted above, so read with deleted=true check)
                //  to be precise about what cash vs advance was involved.
                // ─────────────────────────────────────────────────────────────
                if (requestGuid != null)
                {
                    // Cash amount: non-ADVANCE rows tied to this receipt
                    var cashAmount = await db.ExecuteScalarAsync<double>(
                        @"SELECT COALESCE(SUM(collectedamount), 0)
                            FROM balancecollectionby
                           WHERE receipt_guid    = @rg
                             AND tenant_code     = @t
                             AND collection_type <> 'ADVANCE'",
                        new { rg = receiptGuid, t = tenantCode }, tx);

                    // Advance amount: ADVANCE rows tied to this bill
                    var advanceAmount = await db.ExecuteScalarAsync<double>(
                        @"SELECT COALESCE(SUM(collectedamount), 0)
                            FROM balancecollectionby
                           WHERE request_guid    = @rg
                             AND tenant_code     = @t
                             AND collection_type = 'ADVANCE'",
                        new { rg = requestGuid, t = tenantCode }, tx);

                    double totalRevert = Math.Round(cashAmount + advanceAmount, 2);

                    // Revert paidamount (total) and paidviareceipt (cash only)
                    // GREATEST prevents going negative from double-cancel or data issues
                    await db.ExecuteAsync(
                        @"UPDATE lab_request_master
                             SET paidamount     = GREATEST(COALESCE(paidamount,     0) - @total, 0),
                                 paidviareceipt = GREATEST(COALESCE(paidviareceipt, 0) - @cash,  0)
                           WHERE requestguid = @rg AND tenant_code = @t",
                        new { total = totalRevert, cash = cashAmount, rg = requestGuid, t = tenantCode }, tx);

                    // ── 4. Un-settle balancecollectionbytest ──────────────────
                    await db.ExecuteAsync(
                        @"UPDATE balancecollectionbytest
                             SET requeststatus = false
                           WHERE balancecollectionbyid IN (
                               SELECT balancecollectionbyid
                                 FROM balancecollectionby
                                WHERE request_guid = @rg AND tenant_code = @t
                           )",
                        new { rg = requestGuid, t = tenantCode }, tx);
                }

                tx.Commit();
                return "SUCCESS";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CancelDueCollection failed for receiptGuid={rg}", receiptGuid);
                return $"Cancellation error: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  6. PAGINATED DUE COLLECTION LIST
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsDueCollectionSummary> data, int totalCount)> GetDueCollectionList(
            HmsDueCollectionFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE r.tenant_code = @t
                               AND COALESCE(r.isdeleted, false) = false
                               AND r.receipttype = 'DUE' ";

            if (filter.custid.HasValue)
            {
                where += " AND r.custid = @custid ";
                p.Add("custid", (int)filter.custid.Value);
            }
            if (!string.IsNullOrEmpty(filter.requestguid))
            {
                where += " AND rd.requestguid = @rg ";
                p.Add("rg", filter.requestguid);
            }
            if (filter.bhcode.HasValue)
            {
                where += " AND r.enteredbhcode = @bh ";
                p.Add("bh", filter.bhcode);
            }
            if (filter.cntcode.HasValue)
            {
                where += " AND r.cntcode = @cnt ";
                p.Add("cnt", filter.cntcode);
            }
            if (filter.fromdate.HasValue)
            {
                where += " AND r.receiptdate >= @from ";
                p.Add("from", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                where += " AND r.receiptdate <= @to ";
                p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += @" AND (lrm.name ILIKE @s
                             OR lrm.mobileno ILIKE @s
                             OR r.receiptsnoprint ILIKE @s
                             OR lrm.requestsnoprint ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }
            if (filter.pending_only == true)
            {
                where += @" AND (COALESCE(lrm.totalamount,0)
                                - COALESCE(lrm.paidamount,0)) > 0.05 ";
            }

            string joins = @"
                FROM receipt_master r
               INNER JOIN receipt_details rd
                       ON rd.receiptguid  = r.receiptguid
                      AND rd.tenant_code  = r.tenant_code
                      AND COALESCE(rd.deleted, false) = false
               INNER JOIN lab_request_master lrm
                       ON lrm.requestguid = rd.requestguid
                      AND lrm.tenant_code = rd.tenant_code
                LEFT  JOIN doctor_master dm
                       ON dm.dcode        = lrm.dcode
                      AND dm.tenant_code  = lrm.tenant_code
                LEFT  JOIN mastertenant.branch_master bm
                       ON bm.bh_code      = r.enteredbhcode
                      AND bm.tenant_code  = r.tenant_code";

            int total = await db.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) {joins} {where}", p);

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = await db.QueryAsync<HmsDueCollectionSummary>($@"
                SELECT
                    r.receiptguid                                                AS receipt_guid,
                    r.receiptsnoprint                                            AS receipt_no,
                    lrm.name                                                     AS patient_name,
                    lrm.mobileno,
                    lrm.requestsnoprint                                          AS bill_no,
                    dm.name                                                      AS doctor_name,
                    r.amountpaid                                                 AS amount_paid,
                    COALESCE((
                        SELECT SUM(bcb.collectedamount)
                          FROM balancecollectionby bcb
                         WHERE bcb.request_guid    = rd.requestguid
                           AND bcb.receipt_guid    != r.receiptguid
                           AND bcb.collection_type = 'ADVANCE'
                           AND COALESCE(bcb.deleted, false) = false
                           AND bcb.tenant_code     = @t
                    ), 0)                                                        AS advance_used,
                    (SELECT bcb2.collection_type
                       FROM balancecollectionby bcb2
                      WHERE bcb2.receipt_guid = r.receiptguid
                        AND bcb2.tenant_code  = @t
                        AND COALESCE(bcb2.deleted, false) = false
                      LIMIT 1)                                                   AS collection_type,
                    r.receipttype                                                AS receipt_type,
                    r.receiptdate                                                AS receipt_date,
                    COALESCE(lrm.totalamount, 0)                                AS total_bill_amount,
                    GREATEST(COALESCE(lrm.totalamount,0)
                           - COALESCE(lrm.paidamount, 0), 0)                   AS current_due,
                    CASE WHEN (COALESCE(lrm.totalamount,0)
                             - COALESCE(lrm.paidamount, 0)) <= 0.05
                         THEN true ELSE false END                               AS is_fully_settled,
                    bm.name                                                      AS branch_name
                {joins} {where}
                ORDER BY r.receiptdate DESC
                LIMIT @limit OFFSET @offset", p);

            return (rows.ToList(), total);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  7. PATIENT ADVANCE SUMMARY
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<HmsPatientAdvanceSummary> GetPatientAdvanceSummary(
            decimal custid, string tenantCode)
        {
            using var db = GetConnection();

            var patientName = await db.ExecuteScalarAsync<string>(
                @"SELECT name FROM lab_request_master
                   WHERE custid = @c AND tenant_code = @t
                     AND COALESCE(isdeleted, false) = false
                   ORDER BY requestdatetime DESC LIMIT 1",
                new { c = (int)custid, t = tenantCode });

            var rows = (await db.QueryAsync<HmsAdvanceLedgerRow>(
                @"SELECT ra.receiptadvanceid,
                         ra.receiptguid,
                         ra.requestguid,
                         ra.receiptamount,
                         rm.receiptdate,
                         rm.receiptsnoprint
                    FROM receipt_advances ra
                   INNER JOIN receipt_master rm
                           ON ra.receiptguid  = rm.receiptguid
                          AND rm.tenant_code  = @t
                   WHERE rm.custid      = @c
                     AND rm.tenant_code = @t
                     AND rm.receipttype = 'ADVANCE'
                     AND COALESCE(rm.isdeleted, false) = false
                     AND COALESCE(ra.deleted,   false) = false
                   ORDER BY ra.receiptadvanceid ASC",
                new { c = (int)custid, t = tenantCode })).ToList();

            foreach (var row in rows)
                row.transaction_type = row.requestguid == null ? "DEPOSIT" : "USED";

            var refundTotal = await db.ExecuteScalarAsync<double>(
                @"SELECT COALESCE(SUM(amounttotal), 0)
                    FROM receipt_master
                   WHERE custid      = @c
                     AND tenant_code = @t
                     AND receipttype = 'ADVANCE_REFUND'
                     AND COALESCE(isdeleted, false) = false",
                new { c = (int)custid, t = tenantCode });

            double totalDeposited = rows.Where(r => r.requestguid == null).Sum(r => r.receiptamount);
            double totalUsed = rows.Where(r => r.requestguid != null).Sum(r => r.receiptamount);
            double available = Math.Round(Math.Max(totalDeposited - totalUsed - refundTotal, 0), 2);

            return new HmsPatientAdvanceSummary
            {
                custid = custid,
                patient_name = patientName,
                total_advance_deposited = totalDeposited,
                total_advance_used = totalUsed,
                total_advance_refunded = refundTotal,
                available_balance = available,
                ledger = rows
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  8. GET ALL DUE BILLS
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsAllDueBillRow> data, int totalCount, HmsAllDueSummary summary)>
            GetAllDueBills(HmsAllDueFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE lrm.tenant_code = @t
                       AND COALESCE(lrm.isdeleted, false) = false
                       AND COALESCE(lrm.deleted,   false) = false
                       AND lrm.bill_category = 'HMS'
                       AND (COALESCE(lrm.totalamount, 0)
                          - COALESCE(lrm.paidamount,  0)) > 0.05 ";

            if (filter.bhcode.HasValue) { where += " AND lrm.enteredbhcode = @bh "; p.Add("bh", filter.bhcode); }
            if (filter.cntcode.HasValue) { where += " AND lrm.cntcode = @cnt "; p.Add("cnt", filter.cntcode); }
            if (filter.custid.HasValue) { where += " AND lrm.custid = @custid "; p.Add("custid", (int)filter.custid.Value); }
            if (filter.dcode.HasValue) { where += " AND lrm.dcode = @dcode "; p.Add("dcode", filter.dcode); }
            if (filter.fromdate.HasValue) { where += " AND lrm.requestdatetime >= @from "; p.Add("from", filter.fromdate.Value.Date); }
            if (filter.todate.HasValue) { where += " AND lrm.requestdatetime <= @to "; p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1)); }
            if (filter.min_due.HasValue) { where += " AND (COALESCE(lrm.totalamount,0) - COALESCE(lrm.paidamount, 0)) >= @mindue "; p.Add("mindue", filter.min_due.Value); }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += @" AND (lrm.name ILIKE @s OR lrm.mobileno ILIKE @s
                      OR lrm.requestsnoprint ILIKE @s OR CAST(lrm.custid AS TEXT) ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            string joins = @"
        FROM lab_request_master lrm
        LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode AND dm.tenant_code = lrm.tenant_code
        LEFT JOIN mastertenant.branch_master bm ON bm.bh_code = lrm.enteredbhcode AND bm.tenant_code = lrm.tenant_code";

            var agg = await db.QueryFirstAsync<dynamic>(
                $@"SELECT COUNT(*) AS total_count,
               COALESCE(SUM(lrm.totalamount), 0) AS total_billed,
               COALESCE(SUM(lrm.paidamount), 0) AS total_paid,
               COALESCE(SUM(lrm.totalamount - COALESCE(lrm.paidamount, 0)), 0) AS total_due
           {joins} {where}", p);

            int totalCount = (int)agg.total_count;

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = (await db.QueryAsync<HmsAllDueBillRow>(
                $@"SELECT
               lrm.requestguid,
               lrm.requestsnoprint AS bill_no,
               lrm.requestdatetime AS bill_date,
               lrm.bill_category   AS bill_type,
               lrm.custid,
               lrm.name            AS patient_name,
               lrm.mobileno,
               lrm.opvisitid,
               lrm.dcode,
               dm.name             AS doctor_name,
               lrm.enteredbhcode,
               bm.name             AS branch_name,
               COALESCE(lrm.totalamount, 0)                        AS total_bill_amount,
               COALESCE(lrm.paidamount,  0)                        AS paid_amount,
               COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0) AS due_amount,
               COALESCE((
                   SELECT
                       SUM(CASE WHEN ra.requestguid IS NULL THEN ra.receiptamount ELSE 0 END)
                     - SUM(CASE WHEN ra.requestguid IS NOT NULL THEN ra.receiptamount ELSE 0 END)
                   FROM receipt_advances ra
                   INNER JOIN receipt_master rm ON ra.receiptguid = rm.receiptguid
                   WHERE rm.custid = lrm.custid AND rm.tenant_code = lrm.tenant_code
                     AND rm.receipttype = 'ADVANCE'
                     AND COALESCE(rm.isdeleted, false) = false
                     AND COALESCE(ra.deleted, false) = false
               ), 0) AS advance_available,
               (SELECT MAX(rm2.receiptdate)
                  FROM receipt_master rm2
                 INNER JOIN receipt_details rd2 ON rd2.receiptguid = rm2.receiptguid
                  WHERE rd2.requestguid = lrm.requestguid AND rm2.tenant_code = lrm.tenant_code
                    AND COALESCE(rm2.isdeleted, false) = false
               ) AS last_paid_date
           {joins} {where}
           ORDER BY lrm.requestdatetime DESC
           LIMIT @limit OFFSET @offset", p)).ToList();

            return (rows, totalCount, new HmsAllDueSummary
            {
                total_bills = totalCount,
                total_billed = Math.Round((double)agg.total_billed, 2),
                total_paid = Math.Round((double)agg.total_paid, 2),
                total_due = Math.Round((double)agg.total_due, 2)
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  9. PAID HISTORY
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsPaidHistoryRow> data, int totalCount, HmsPaidHistorySummary summary)>
            GetPaidHistory(HmsPaidHistoryFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE rm.tenant_code = @t AND COALESCE(rm.isdeleted, false) = false ";

            if (filter.bhcode.HasValue) { where += " AND rm.enteredbhcode = @bh "; p.Add("bh", filter.bhcode); }
            if (filter.cntcode.HasValue) { where += " AND rm.cntcode = @cnt "; p.Add("cnt", filter.cntcode); }
            if (filter.custid.HasValue) { where += " AND rm.custid = @custid "; p.Add("custid", (int)filter.custid.Value); }
            if (filter.fromdate.HasValue) { where += " AND rm.receiptdate >= @from "; p.Add("from", filter.fromdate.Value.Date); }
            if (filter.todate.HasValue) { where += " AND rm.receiptdate <= @to "; p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1)); }
            if (!string.IsNullOrEmpty(filter.receipt_type) && filter.receipt_type.ToUpper() != "ALL")
            { where += " AND rm.receipttype = @rtype "; p.Add("rtype", filter.receipt_type.ToUpper()); }
            if (!string.IsNullOrEmpty(filter.collection_type))
            {
                where += @" AND EXISTS (SELECT 1 FROM balancecollectionby bcb
                             WHERE bcb.receipt_guid = rm.receiptguid AND bcb.collection_type = @coltype
                               AND bcb.tenant_code = @t AND COALESCE(bcb.deleted, false) = false) ";
                p.Add("coltype", filter.collection_type.ToUpper());
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += @" AND (lrm.name ILIKE @s OR lrm.mobileno ILIKE @s
                      OR rm.receiptsnoprint ILIKE @s OR lrm.requestsnoprint ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            string joins = @"
        FROM receipt_master rm
        LEFT JOIN receipt_details rd ON rd.receiptguid = rm.receiptguid AND rd.tenant_code = rm.tenant_code AND COALESCE(rd.deleted, false) = false
        LEFT JOIN lab_request_master lrm ON lrm.requestguid = rd.requestguid AND lrm.tenant_code = rd.tenant_code
        LEFT JOIN doctor_master dm ON dm.dcode = lrm.dcode AND dm.tenant_code = lrm.tenant_code
        LEFT JOIN mastertenant.branch_master bm ON bm.bh_code = rm.enteredbhcode AND bm.tenant_code = rm.tenant_code";

            var agg = await db.QueryFirstAsync<dynamic>(
                $@"SELECT COUNT(DISTINCT rm.receiptguid) AS total_receipts,
               COALESCE(SUM(CASE WHEN rm.receipttype = 'DUE' THEN rm.amountpaid ELSE 0 END), 0) AS total_due_collected,
               COALESCE(SUM(CASE WHEN rm.receipttype = 'ADVANCE' THEN rm.amountpaid ELSE 0 END), 0) AS total_advance_deposited,
               COALESCE(SUM(CASE WHEN rm.receipttype = 'ADVANCE_REFUND' THEN rm.amountpaid ELSE 0 END), 0) AS total_advance_refunded
           {joins} {where}", p);

            int totalCount = (int)agg.total_receipts;
            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = (await db.QueryAsync<HmsPaidHistoryRow>(
                $@"SELECT
               rm.receiptguid, rm.receiptsnoprint AS receipt_no,
               rm.receiptbarcode AS receipt_barcode, rm.receipttype AS receipt_type,
               rm.receiptdate, rm.amountpaid AS amount,
               rm.custid, lrm.name AS patient_name, lrm.mobileno,
               lrm.requestsnoprint AS bill_no, lrm.requestguid AS bill_guid,
               lrm.requestdatetime AS bill_date, dm.name AS doctor_name,
               bm.name AS branch_name, rm.bankname AS bank_name,
               rm.paymentreference AS reference_no, rm.chequedate,
               (SELECT bcb.collection_type FROM balancecollectionby bcb
                 WHERE bcb.receipt_guid = rm.receiptguid AND bcb.tenant_code = @t
                   AND COALESCE(bcb.deleted, false) = false LIMIT 1) AS collection_type,
               CASE WHEN lrm.requestguid IS NOT NULL
                    THEN GREATEST(COALESCE(lrm.totalamount,0) - COALESCE(lrm.paidamount,0), 0)
                    ELSE NULL END AS remaining_due,
               CASE WHEN lrm.requestguid IS NOT NULL
                    THEN (COALESCE(lrm.totalamount,0) - COALESCE(lrm.paidamount,0)) <= 0.05
                    ELSE NULL END AS is_bill_settled
           {joins} {where}
           ORDER BY rm.receiptdate DESC
           LIMIT @limit OFFSET @offset", p)).ToList();

            return (rows, totalCount, new HmsPaidHistorySummary
            {
                total_receipts = totalCount,
                total_due_collected = Math.Round((double)agg.total_due_collected, 2),
                total_advance_deposited = Math.Round((double)agg.total_advance_deposited, 2),
                total_advance_refunded = Math.Round((double)agg.total_advance_refunded, 2),
                net_collected = Math.Round(
                    (double)agg.total_due_collected + (double)agg.total_advance_deposited
                    - (double)agg.total_advance_refunded, 2)
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes available advance balance for a patient.
        ///   Balance = SUM(deposit rows where requestguid IS NULL)
        ///           − SUM(usage+refund rows where requestguid IS NOT NULL)
        /// Soft-deleted rows are excluded, so cancelling a collection and
        /// soft-deleting its usage rows automatically restores the balance.
        /// </summary>
        private async Task<double> GetPatientAdvanceBalance(
            IDbConnection db, decimal? custid, string tenantCode,
            IDbTransaction? tx = null)
        {
            if (custid == null) return 0;

            var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                      COALESCE(SUM(CASE WHEN ra.requestguid IS NULL
                                        THEN ra.receiptamount ELSE 0 END), 0) AS deposited,
                      COALESCE(SUM(CASE WHEN ra.requestguid IS NOT NULL
                                        THEN ra.receiptamount ELSE 0 END), 0) AS used
                    FROM receipt_advances ra
                   INNER JOIN receipt_master rm ON ra.receiptguid = rm.receiptguid
                   WHERE rm.custid      = @c
                     AND rm.tenant_code = @t
                     AND rm.receipttype = 'ADVANCE'
                     AND COALESCE(rm.isdeleted,  false) = false
                     AND COALESCE(ra.deleted,    false) = false",
                new { c = (int?)custid, t = tenantCode }, tx);

            if (result == null) return 0;
            double balance = (double)result.deposited - (double)result.used;
            return Math.Round(Math.Max(balance, 0), 2);
        }

        /// <summary>
        /// Race-safe receipt sequence using SELECT FOR UPDATE on billno_sequence.
        /// </summary>
        private async Task<HmsNumberResult> GetNextReceiptSequence(
            IDbConnection db, IDbTransaction tx,
            HmsBillNoMaster cfg, int bhcode, int cntcode, string tenantCode)
        {
            var seq = await db.QueryFirstOrDefaultAsync<HmsBillNoSequence>(
    @"SELECT seq_id, bncode, bhcode, cntcode, orderno,
             last_used_date::timestamp AS last_used_date,
             tenant_code, snoprint
      FROM billno_sequence
      WHERE bncode = @bn AND bhcode = @bh AND cntcode = @cnt AND tenant_code = @t
      FOR UPDATE",
    new { bn = cfg.bncode, bh = bhcode, cnt = cntcode, t = tenantCode }, tx);

            int nextOrder;

            if (seq == null)
            {
                nextOrder = cfg.orderno;
                await db.InsertAsync(new HmsBillNoSequence
                {
                    bncode = cfg.bncode,
                    bhcode = bhcode,
                    cntcode = cntcode,
                    orderno = nextOrder,
                    last_used_date = DateTime.UtcNow.Date,
                    tenant_code = tenantCode
                }, tx);
            }
            else
            {
                nextOrder = seq.orderno + 1;
                seq.orderno = nextOrder;
                seq.last_used_date = DateTime.UtcNow.Date;
                await db.UpdateAsync(seq, tx);
            }

            string prefix = cfg.shortname ?? "RCP";
            string snoprint = $"{prefix}-{DateTime.UtcNow:yyMM}-{nextOrder:D5}";
            string barcode = $"{cfg.bncode}{bhcode}{cntcode}{nextOrder}";

            return new HmsNumberResult
            {
                sno = nextOrder,
                snoprint = snoprint,
                barcode = barcode,
                used_bncode = cfg.bncode
            };
        }

        /// <summary>
        /// Inserts a balancecollectionby row writing BOTH legacy and new-style columns
        /// for backward compatibility with existing LIMS/HMS reports.
        /// </summary>
        private async Task InsertBalanceCollectionRow(
            IDbConnection db, IDbTransaction tx,
            int? bhcode, string collectionType,
            string receiptGuid, string? requestGuid,
            double amount, HmsCounterTiming shift,
            HmsDueCollectionRequest req, HmsLabRequestMaster bill,
            string tenantCode)
        {
            await db.ExecuteAsync(
                @"INSERT INTO balancecollectionby (
                      balancecollectionbyid,
                      bhcode,
                      collecteddate, collectiontype, receiptguid, requestguid,
                      collected_date, collection_type, receipt_guid, request_guid,
                      collectedamount, deleted,
                      usercode, computercode,
                      entereddate, ibsdate,
                      tmcode, cntcode, cnttid, pmcode, ctcode,
                      tenant_code)
                  VALUES (
                      @id, @bh,
                      @dt, @ct, @rg, @req,
                      @dt, @ct, @rg, @req,
                      @amt, false,
                      @uc, @cc,
                      @dt, @dt,
                      @tm, @cnt, @cnttid, @pm, @cct,
                      @t)",
                new
                {
                    id = Guid.NewGuid().ToString(),
                    bh = bhcode,
                    dt = DateTime.UtcNow,
                    ct = collectionType,
                    rg = receiptGuid,
                    req = requestGuid,
                    amt = amount,
                    uc = req.usercode,
                    cc = req.computercode,
                    tm = req.tmcode ?? bill.tmcode,
                    cnt = req.cntcode,
                    cnttid = shift.cnttid,
                    pm = req.pmcode ?? bill.pmcode,
                    cct = req.ctcode ?? bill.ctcode,
                    t = tenantCode
                }, tx);
        }

        private static string? ValidateDueRequest(HmsDueCollectionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.requestguid))
                return "Bill reference (requestguid) is required.";
            if (req.advance_to_use < 0)
                return "Advance amount cannot be negative.";
            if (req.cash_collected < 0)
                return "Cash collected cannot be negative.";
            if (req.advance_to_use == 0 && req.cash_collected == 0)
                return "At least one of advance_to_use or cash_collected must be greater than zero.";

            var refRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "CARD", "UPI", "CHEQUE", "BANK" };
            if (req.cash_collected > 0
                && refRequired.Contains(req.collection_type)
                && string.IsNullOrWhiteSpace(req.reference_no))
                return $"Transaction reference number is required for payment mode '{req.collection_type}'.";

            if (req.enteredbhcode == null || req.cntcode == null)
                return "Branch code (enteredbhcode) and counter code (cntcode) are required.";
            if (req.usercode == null)
                return "usercode is required (collected_by identity).";

            return null;
        }
    }
}