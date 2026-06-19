using System;
using System.Collections.Generic;
using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  HMS DUE COLLECTION — MODELS & DTOs
    //
    //  All table models are mapped to the confirmed PostgreSQL DDL.
    //  balancecollectionby has duplicate legacy/new columns — both sets are
    //  written so existing reports and LIMS queries continue to work.
    //
    //  receipt_advances conventions:
    //    Deposit row : requestguid = NULL          → unallocated advance credit
    //    Usage row   : requestguid = bill guid      → advance used against a bill
    //    Refund row  : requestguid = refund receipt guid → advance refunded to patient
    //
    //  receipt_master.receipttype values used here:
    //    'DUE'            → due payment (cash / card / UPI / cheque / bank)
    //    'ADVANCE'        → advance deposit from patient
    //    'ADVANCE_REFUND' → excess advance refunded back at discharge
    // ═══════════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────────
    //  TABLE MODELS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps to: public.receipt_master (confirmed DDL)
    /// One row per payment event — due collection, advance deposit, or refund.
    /// </summary>
    [Table("receipt_master")]
    public class HmsDueReceiptMaster
    {
        [ExplicitKey] public string? receiptguid { get; set; }
        public DateTime? receiptdate { get; set; }
        public int? receiptsno { get; set; }
        public string? receiptsnoprint { get; set; }
        public string? receiptbarcode { get; set; }
        public string? receiptcovertedbarcode { get; set; }
        public decimal? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? tmcode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public long? payledgercode { get; set; }
        public string? bankname { get; set; }
        public string? paymentreference { get; set; }  // cheque no / UPI ref / card ref
        public DateTime? chequedate { get; set; }
        public string? cardno { get; set; }
        public DateTime? carddate { get; set; }
        public double? amountpaid { get; set; }
        public double? amountadjusted { get; set; }
        public double? amounttotal { get; set; }
        public bool? deleted { get; set; }
        public bool? isdeleted { get; set; }
        public int? enteredbhcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? receipttype { get; set; }  // DUE / ADVANCE / ADVANCE_REFUND
        public int? custid { get; set; }
        public string? opvisitid { get; set; }
        public string? tenant_code { get; set; }
        public bool? isbill { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }
    }

    /// <summary>
    /// Maps to: public.receipt_details (confirmed DDL)
    /// Links a receipt to the bill it covers.
    /// </summary>
    [Table("receipt_details")]
    public class HmsDueReceiptDetail
    {
        [ExplicitKey] public string? receiptdetailsid { get; set; }
        public string? receiptguid { get; set; }
        public string? requestguid { get; set; }
        public double? receiptamount { get; set; }
        public double? discount_amount { get; set; }
        public double? refund_amount { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? tenant_code { get; set; }
    }

    /// <summary>
    /// Maps to: public.receipt_advances (confirmed DDL + migration columns)
    ///
    /// DEPOSIT : requestguid IS NULL        → credit (advance in)
    /// USAGE   : requestguid = bill guid    → debit  (advance applied to bill)
    /// REFUND  : requestguid = refund guid  → debit  (advance returned to patient)
    ///
    /// Available balance per patient = SUM(deposits) − SUM(usage + refund rows)
    /// FIFO order is maintained by receiptadvanceid sort (UUID monotonic).
    ///
    /// Migration required before deploy — run receipt_advances_migration.sql:
    ///   ALTER TABLE receipt_advances ADD COLUMN deleted boolean DEFAULT false,
    ///     ADD COLUMN tenant_code varchar(50), ADD COLUMN usercode int,
    ///     ADD COLUMN computercode int, ADD COLUMN entereddate timestamp,
    ///     ADD COLUMN ibsdate timestamp;
    /// </summary>
    [Table("receipt_advances")]
    public class HmsDueReceiptAdvance
    {
        [ExplicitKey] public string? receiptadvanceid { get; set; }
        public string? receiptguid { get; set; }  // advance deposit receipt
        public string? requestguid { get; set; }  // NULL / bill guid / refund guid
        public double? receiptamount { get; set; }
        // Migration columns (added via receipt_advances_migration.sql)
        public bool? deleted { get; set; }
        public string? tenant_code { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
    }

    /// <summary>
    /// Maps to: public.balancecollectionby (confirmed DDL)
    ///
    /// Cash-flow ledger — one row per collection event.
    /// Both legacy and new-style columns are written for backward compatibility.
    ///
    ///   Legacy columns  : collecteddate, collectiontype, receiptguid, requestguid
    ///   New columns     : collected_date, collection_type, receipt_guid, request_guid
    ///
    /// collection_type / collectiontype values:
    ///   CASH / CARD / UPI / CHEQUE / BANK → real money collected
    ///   ADVANCE                            → advance adjustment (no new cash, ledger only)
    /// </summary>
    [Table("balancecollectionby")]
    public class HmsDueBalanceCollectionBy
    {
        [ExplicitKey] public string? balancecollectionbyid { get; set; }
        public int? bhcode { get; set; }
        // Legacy columns (write both for backward compat)
        public DateTime? collecteddate { get; set; }
        public string? collectiontype { get; set; }
        public string? receiptguid { get; set; }
        public string? requestguid { get; set; }
        // New-style columns (preferred)
        public DateTime? collected_date { get; set; }
        public string? collection_type { get; set; }
        public string? receipt_guid { get; set; }
        public string? request_guid { get; set; }
        // Common columns
        public double? collectedamount { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public decimal? tmcode { get; set; }
        public decimal? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public string? tenant_code { get; set; }
    }

    /// <summary>
    /// Maps to: public.balancecollectionbytest (confirmed DDL)
    /// Test-level settlement flag per tcode.
    /// requeststatus = true  → this tcode is fully paid.
    /// requeststatus = false → this tcode still has pending due.
    /// Set to true when bill becomes fully settled; reverted to false on cancel.
    /// </summary>
    [Table("balancecollectionbytest")]
    public class HmsDueBalanceCollectionByTest
    {
        [ExplicitKey] public string? balancecollectionbytestid { get; set; }
        public decimal? tcode { get; set; }
        public string? balancecollectionbyid { get; set; }
        public double? collectedamount { get; set; }
        public bool? requeststatus { get; set; }  // true = settled
        public string? tenant_code { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  REQUEST DTOs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Due collection request — three scenarios all use this single DTO.
    ///
    ///   Scenario 1 — Cash only         : advance_to_use = 0, cash_collected > 0
    ///   Scenario 2 — Advance only       : advance_to_use > 0, cash_collected = 0
    ///   Scenario 3 — Advance + Cash     : advance_to_use > 0, cash_collected > 0
    ///
    /// Partial payment is allowed — remaining balance stays as due on the bill.
    /// advance_to_use is clamped server-side to min(available, current_due).
    /// </summary>
    public class HmsDueCollectionRequest
    {
        /// <summary>Bill to collect against (lab_request_master.requestguid).</summary>
        public string requestguid { get; set; } = string.Empty;

        /// <summary>
        /// Advance to adjust from patient's advance wallet (FIFO, oldest first).
        /// 0 = do not use advance. Cannot exceed available balance or current due.
        /// </summary>
        public double advance_to_use { get; set; } = 0;

        /// <summary>New cash / card / UPI collected right now (new money).</summary>
        public double cash_collected { get; set; } = 0;

        // ── Payment mode ─────────────────────────────────────────────────────
        /// <summary>CASH / CARD / UPI / CHEQUE / BANK</summary>
        public string collection_type { get; set; } = "CASH";
        /// <summary>Mandatory for CARD / UPI / CHEQUE / BANK.</summary>
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }

        // ── Session / location ───────────────────────────────────────────────
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public string? remarks { get; set; }
    }

    /// <summary>
    /// Advance deposit request — patient pays in bulk upfront.
    /// Creates receipt_master (receipttype='ADVANCE') + receipt_advances deposit row.
    /// </summary>
    public class HmsAdvanceDepositRequest
    {
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public string? opvisitid { get; set; }
        public double amount { get; set; }
        public string collection_type { get; set; } = "CASH";
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? remarks { get; set; }
    }

    /// <summary>
    /// Advance refund request — leftover advance returned to patient at IP discharge.
    /// FIFO: oldest deposits consumed first.
    /// Creates receipt_master (receipttype='ADVANCE_REFUND', isrefund=true).
    /// </summary>
    public class HmsAdvanceRefundRequest
    {
        public decimal custid { get; set; }
        public string? opvisitid { get; set; }
        public double refund_amount { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? remarks { get; set; }
    }

    /// <summary>Filter for paginated due collection history list.</summary>
    public class HmsDueCollectionFilterRequest
    {
        public decimal? custid { get; set; }
        public string? requestguid { get; set; }  // filter by specific bill
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        /// <summary>Searches: patient name / mobile / receipt no / bill no.</summary>
        public string? search { get; set; }
        /// <summary>If true, only returns bills that still have due > 0.</summary>
        public bool? pending_only { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RESPONSE DTOs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dry-run preview — shown to staff BEFORE confirming collection.
    /// No DB writes. Call GET /due-collection/preview/{requestguid}?advanceToUse=2000.
    ///
    /// UI should show:
    ///   current_due       → what the patient owes now
    ///   advance_available → how much advance wallet has
    ///   advance_to_use    → server-clamped value (can't exceed min(available, due))
    ///   amount_to_collect → cash the staff needs to collect from patient
    ///   due_after         → what will remain if confirmed
    /// </summary>
    public class HmsDuePreviewResponse
    {
        // Bill info
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public DateTime? bill_date { get; set; }
        public string? bill_type { get; set; }  // OP / IP / Lab / Pharmacy

        // Patient info
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? opvisitid { get; set; }

        // Doctor / Department
        public string? doctor_name { get; set; }
        public int? dcode { get; set; }

        // Amount breakdown
        public double total_bill_amount { get; set; }
        public double previous_paid { get; set; }
        public double current_due { get; set; }

        // Advance wallet
        public double advance_available { get; set; }
        /// <summary>Server-clamped: min(requested, min(available, current_due)).</summary>
        public double advance_to_use { get; set; }

        // Collection summary
        public double amount_to_collect { get; set; }  // cash still needed
        public double due_after { get; set; }  // remaining after confirmation
    }

    /// <summary>Returned after a successful due collection (CollectDue).</summary>
    public class HmsDueCollectionResponse
    {
        // Receipt info (only populated when cash_collected > 0)
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }

        // Advance receipt used (first advance receipt guid, if advance was applied)
        public string? advance_receipt_guid { get; set; }

        // Bill reference
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }

        // Amount breakdown
        public double total_bill_amount { get; set; }
        public double due_before { get; set; }
        public double advance_used { get; set; }
        public double cash_collected { get; set; }
        public double total_settled { get; set; }
        public double due_after { get; set; }
        public bool is_fully_settled { get; set; }

        // Payment info
        public string collection_type { get; set; } = string.Empty;
        public string? reference_no { get; set; }
        public DateTime collected_date { get; set; }
        public string? collected_by { get; set; }  // username resolved at service level
        public string? remarks { get; set; }
    }

    /// <summary>Returned after advance deposit or refund.</summary>
    public class HmsAdvanceReceiptResponse
    {
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string? receipt_type { get; set; }   // ADVANCE / ADVANCE_REFUND
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public double amount { get; set; }
        public string? collection_type { get; set; }
        public DateTime receipt_date { get; set; }
    }

    /// <summary>
    /// Patient advance balance summary.
    /// Call GET /due-collection/advance-summary/{custid} before opening due collection.
    /// </summary>
    public class HmsPatientAdvanceSummary
    {
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public double total_advance_deposited { get; set; }
        public double total_advance_used { get; set; }
        public double total_advance_refunded { get; set; }
        public double available_balance { get; set; }
        public List<HmsAdvanceLedgerRow> ledger { get; set; } = new();
    }

    /// <summary>
    /// One row from the patient's advance ledger (receipt_advances joined with receipt_master).
    /// </summary>
    public class HmsAdvanceLedgerRow
    {
        public string? receiptadvanceid { get; set; }
        public string? receiptguid { get; set; }
        /// <summary>NULL = deposit, bill guid = used, refund guid = refunded.</summary>
        public string? requestguid { get; set; }
        public double receiptamount { get; set; }
        public DateTime? receiptdate { get; set; }
        public string? receiptsnoprint { get; set; }
        /// <summary>DEPOSIT / USED / REFUNDED — derived field, not stored.</summary>
        public string? transaction_type { get; set; }
    }

    /// <summary>Single row in the paginated due collection history list.</summary>
    public class HmsDueCollectionSummary
    {
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? bill_no { get; set; }
        public string? doctor_name { get; set; }
        public double amount_paid { get; set; }
        public double advance_used { get; set; }
        public string? collection_type { get; set; }
        public string? receipt_type { get; set; }
        public DateTime receipt_date { get; set; }
        public double total_bill_amount { get; set; }
        public double current_due { get; set; }
        public bool is_fully_settled { get; set; }
        public string? branch_name { get; set; }
        public string? collected_by { get; set; }
    }
    // ── All Due Bills ──────────────────────────────────────────────────────

    public class HmsAllDueFilterRequest
    {
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public decimal? custid { get; set; }
        public int? dcode { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public double? min_due { get; set; }   // only bills with due >= this value
        public string? search { get; set; }   // name / mobile / bill_no / custid
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsAllDueBillRow
    {
        public string? requestguid { get; set; }
        public string? bill_no { get; set; }
        public DateTime? bill_date { get; set; }
        public string? bill_type { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? opvisitid { get; set; }
        public int? dcode { get; set; }
        public string? doctor_name { get; set; }
        public int? enteredbhcode { get; set; }
        public string? branch_name { get; set; }
        public double total_bill_amount { get; set; }
        public double paid_amount { get; set; }
        public double due_amount { get; set; }
        public double advance_available { get; set; }
        public DateTime? last_paid_date { get; set; }
    }

    public class HmsAllDueSummary
    {
        public int total_bills { get; set; }
        public double total_billed { get; set; }
        public double total_paid { get; set; }
        public double total_due { get; set; }
    }

    // ── Paid History ───────────────────────────────────────────────────────

    public class HmsPaidHistoryFilterRequest
    {
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public decimal? custid { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        /// <summary>DUE / ADVANCE / ADVANCE_REFUND / ALL (default ALL)</summary>
        public string? receipt_type { get; set; }
        /// <summary>CASH / CARD / UPI / CHEQUE / BANK / ADVANCE</summary>
        public string? collection_type { get; set; }
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsPaidHistoryRow
    {
        public string? receiptguid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string? receipt_type { get; set; }
        public DateTime? receiptdate { get; set; }
        public double? amount { get; set; }
        public int? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? bill_no { get; set; }
        public string? bill_guid { get; set; }
        public DateTime? bill_date { get; set; }
        public string? doctor_name { get; set; }
        public string? branch_name { get; set; }
        public string? collection_type { get; set; }
        public string? bank_name { get; set; }
        public string? reference_no { get; set; }
        public DateTime? chequedate { get; set; }
        public double? remaining_due { get; set; }
        public bool? is_bill_settled { get; set; }
    }

    public class HmsPaidHistorySummary
    {
        public int total_receipts { get; set; }
        public double total_due_collected { get; set; }
        public double total_advance_deposited { get; set; }
        public double total_advance_refunded { get; set; }
        public double net_collected { get; set; }
    }
}