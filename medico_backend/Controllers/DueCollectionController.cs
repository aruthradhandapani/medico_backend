using System;
using System.Threading.Tasks;
using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace medico_backend.Controllers
{
    /// <summary>
    /// HMS Due Collection API
    ///
    /// Tenant is extracted (in priority order):
    ///   1. JWT claim "tenant_code" or "TenantCode"
    ///   2. Request header "X-Tenant-Code"
    ///   3. Fallback: "DEFAULT" (logs a warning)
    ///
    /// Endpoints:
    ///   GET  /api/HmsDueCollection/preview/{requestguid}?advanceToUse=2000
    ///   POST /api/HmsDueCollection/collect
    ///   DEL  /api/HmsDueCollection/cancel/{receiptGuid}
    ///   POST /api/HmsDueCollection/list
    ///   GET  /api/HmsDueCollection/advance-summary/{custid}
    ///   POST /api/HmsDueCollection/advance/deposit
    ///   POST /api/HmsDueCollection/advance/refund
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HmsDueCollectionController : ControllerBase
    {
        private readonly HmsDueCollectionClass _service;
        private readonly ILogger<HmsDueCollectionController> _logger;

        public HmsDueCollectionController(
            HmsDueCollectionClass service,
            ILogger<HmsDueCollectionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Resolves the tenant code for each request in priority order:
        ///   1. JWT claim
        ///   2. X-Tenant-Code header
        ///   3. Fallback "DEFAULT"
        /// </summary>
        private string ResolveTenantCode()
        {
            var fromClaim = User.FindFirst("tenant_code")?.Value
                         ?? User.FindFirst("TenantCode")?.Value;
            if (!string.IsNullOrWhiteSpace(fromClaim))
                return fromClaim.Trim();

            if (Request.Headers.TryGetValue("tenant_code", out var header)
                && !string.IsNullOrWhiteSpace(header))
                return header.ToString().Trim();

            _logger.LogWarning("Tenant code not found in JWT or headers — using DEFAULT.");
            return "DEFAULT";
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  1. DRY-RUN PREVIEW  (no DB writes)
        //
        //  Show the operator a full breakdown before they confirm collection.
        //  Call this when the patient arrives at the counter — the response
        //  tells you: current_due, advance_available, how much advance would
        //  be used, how much cash is still needed, and what remains after.
        //
        //  Query params:
        //    advanceToUse (optional, default 0) — how much advance the
        //    operator wants to apply. Server clamps to
        //    min(requested, min(advance_available, current_due)).
        //
        //  GET /api/HmsDueCollection/preview/{requestguid}?advanceToUse=2000
        //
        //  Response 200:
        //  {
        //    "success": true,
        //    "data": {
        //      "requestguid"       : "abc-123",
        //      "bill_no"           : "HMS-2606-00012",
        //      "bill_date"         : "2026-06-10T09:30:00",
        //      "bill_type"         : "HMS",
        //      "custid"            : 1001,
        //      "patient_name"      : "Ravi Kumar",
        //      "mobileno"          : "9876543210",
        //      "doctor_name"       : "Dr. Priya",
        //      "total_bill_amount" : 5000.00,
        //      "previous_paid"     : 0.00,
        //      "current_due"       : 5000.00,
        //      "advance_available" : 2000.00,
        //      "advance_to_use"    : 2000.00,
        //      "amount_to_collect" : 3000.00,
        //      "due_after"         : 0.00
        //    }
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet("preview/{requestguid}")]
        public async Task<IActionResult> GetDuePreview(
            string requestguid,
            [FromQuery] double advanceToUse = 0)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.GetDuePreview(requestguid, advanceToUse, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in due preview for requestguid={rg}", requestguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  2. COLLECT DUE  (atomic transaction — main endpoint)
        //
        //  Three scenarios all use this one endpoint:
        //    Scenario 1 — Cash only   : "advance_to_use": 0,    "cash_collected": 3000
        //    Scenario 2 — Advance only: "advance_to_use": 2000, "cash_collected": 0
        //    Scenario 3 — Both        : "advance_to_use": 2000, "cash_collected": 1000
        //
        //  Partial payment is allowed — set cash_collected to the partial amount.
        //  Remaining due stays on the bill and the patient can pay it later.
        //
        //  POST /api/HmsDueCollection/collect
        //
        //  Request body:
        //  {
        //    "requestguid"    : "abc-123",
        //    "advance_to_use" : 2000,
        //    "cash_collected" : 1000,
        //    "collection_type": "CASH",       // CASH / CARD / UPI / CHEQUE / BANK
        //    "reference_no"   : null,          // mandatory for CARD/UPI/CHEQUE/BANK
        //    "bank_name"      : null,
        //    "card_no"        : null,
        //    "cheque_date"    : null,
        //    "enteredbhcode"  : 1,
        //    "cntcode"        : 1,
        //    "usercode"       : 10,
        //    "computercode"   : 1,
        //    "pmcode"         : 1,
        //    "remarks"        : "Patient partial payment"
        //  }
        //
        //  Response 200:
        //  {
        //    "success": true,
        //    "data": {
        //      "receipt_guid"        : "rcp-guid-here",
        //      "receipt_no"          : "RCP-2606-00001",
        //      "receipt_barcode"     : "...",
        //      "advance_receipt_guid": "adv-guid-here",
        //      "requestguid"         : "abc-123",
        //      "bill_no"             : "HMS-2606-00012",
        //      "patient_name"        : "Ravi Kumar",
        //      "total_bill_amount"   : 5000.00,
        //      "due_before"          : 5000.00,
        //      "advance_used"        : 2000.00,
        //      "cash_collected"      : 1000.00,
        //      "total_settled"       : 3000.00,
        //      "due_after"           : 2000.00,
        //      "is_fully_settled"    : false,
        //      "collection_type"     : "CASH",
        //      "collected_date"      : "2026-06-19T10:30:00Z"
        //    }
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("collect")]
        public async Task<IActionResult> CollectDue([FromBody] HmsDueCollectionRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.CollectDue(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CollectDue for requestguid={rg}", req?.requestguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  3. CANCEL DUE COLLECTION RECEIPT
        //
        //  Fully reverses a DUE receipt — restores the bill to pre-collection state.
        //  Only DUE receipts can be cancelled here.
        //  Use /advance/refund for ADVANCE receipts.
        //
        //  What gets reversed:
        //    → receipt_master.isdeleted = true
        //    → receipt_details.deleted = true
        //    → balancecollectionby.deleted = true (cash + advance rows)
        //    → receipt_advances usage rows: deleted = true (advance restored)
        //    → lab_request_master.paidamount and paidviareceipt reduced back
        //    → balancecollectionbytest.requeststatus = false (tests un-settled)
        //
        //  DELETE /api/HmsDueCollection/cancel/{receiptGuid}?usercode=10
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet("cancel/{receiptGuid}")]
        public async Task<IActionResult> CancelDueCollection(
            string receiptGuid,
            [FromQuery] int? usercode = null,
            [FromQuery] string? reason = null)
        {
            try
            {
                string tenant = ResolveTenantCode();
                string status = await _service.CancelDueCollection(receiptGuid, usercode, reason, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new
                {
                    success = true,
                    message = "Due collection receipt cancelled and all entries reversed successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling receipt={rg}", receiptGuid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  4. PAGINATED DUE COLLECTION LIST
        //
        //  Returns all DUE receipts with enriched patient, bill, doctor and
        //  branch information. All filter fields are optional.
        //
        //  POST /api/HmsDueCollection/list
        //
        //  Request body:
        //  {
        //    "custid"      : 1001,            // optional — filter by patient
        //    "requestguid" : null,            // optional — filter by specific bill
        //    "bhcode"      : 1,
        //    "cntcode"     : 1,
        //    "fromdate"    : "2026-06-01",
        //    "todate"      : "2026-06-30",
        //    "search"      : "Ravi",          // searches name, mobile, receipt_no, bill_no
        //    "pending_only": true,            // only bills still with due > 0
        //    "page"        : 1,
        //    "pagesize"    : 20
        //  }
        //
        //  Response 200:
        //  {
        //    "success"    : true,
        //    "totalCount" : 42,
        //    "page"       : 1,
        //    "pagesize"   : 20,
        //    "data"       : [ { ... } ]
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("list")]
        public async Task<IActionResult> GetDueCollectionList(
            [FromBody] HmsDueCollectionFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount) = await _service.GetDueCollectionList(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching due collection list.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  5. PATIENT ADVANCE SUMMARY
        //
        //  Returns the patient's advance wallet — total deposited, total used,
        //  total refunded, available balance, and a full chronological ledger.
        //
        //  Call this first when opening the due collection screen for a patient
        //  so the operator can see the available advance before deciding how
        //  much to apply (then call /preview with the chosen amount).
        //
        //  GET /api/HmsDueCollection/advance-summary/{custid}
        //
        //  Response 200:
        //  {
        //    "success": true,
        //    "data": {
        //      "custid"                  : 1001,
        //      "patient_name"            : "Ravi Kumar",
        //      "total_advance_deposited" : 5000.00,
        //      "total_advance_used"      : 2000.00,
        //      "total_advance_refunded"  : 0.00,
        //      "available_balance"       : 3000.00,
        //      "ledger": [
        //        { "receiptadvanceid": "...", "receiptamount": 5000, "requestguid": null, "transaction_type": "DEPOSIT" },
        //        { "receiptadvanceid": "...", "receiptamount": 2000, "requestguid": "bill-guid", "transaction_type": "USED" }
        //      ]
        //    }
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet("advance-summary/{custid}")]
        public async Task<IActionResult> GetPatientAdvanceSummary(decimal custid)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var data = await _service.GetPatientAdvanceSummary(custid, tenant);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching advance summary for custid={c}", custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  6. DEPOSIT ADVANCE
        //
        //  Patient pays a bulk amount upfront — IP admission, pre-payment, etc.
        //  The amount is credited to the patient's advance wallet and can later
        //  be applied against any bill using the /collect endpoint.
        //
        //  Tables written:
        //    receipt_master    → receipttype='ADVANCE', isbill=false
        //    receipt_advances  → deposit row (requestguid = null = unallocated)
        //    balancecollectionby → so advance deposit appears in daily collection
        //
        //  POST /api/HmsDueCollection/advance/deposit
        //
        //  Request body:
        //  {
        //    "custid"         : 1001,
        //    "patient_name"   : "Ravi Kumar",
        //    "opvisitid"      : "VISIT-001",   // optional
        //    "amount"         : 5000,
        //    "collection_type": "CASH",
        //    "reference_no"   : null,
        //    "bank_name"      : null,
        //    "enteredbhcode"  : 1,
        //    "cntcode"        : 1,
        //    "usercode"       : 10,
        //    "computercode"   : 1,
        //    "pmcode"         : 1
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/deposit")]
        public async Task<IActionResult> DepositAdvance([FromBody] HmsAdvanceDepositRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.DepositAdvance(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error depositing advance for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  7. REFUND ADVANCE
        //
        //  Use at IP discharge when advance balance > total bills.
        //  The surplus advance is returned to the patient in cash.
        //  FIFO: oldest advance deposits are consumed first.
        //
        //  Tables written:
        //    receipt_master   → receipttype='ADVANCE_REFUND', isrefund=true
        //    receipt_advances → debit rows against oldest deposits (FIFO)
        //
        //  POST /api/HmsDueCollection/advance/refund
        //
        //  Request body:
        //  {
        //    "custid"        : 1001,
        //    "opvisitid"     : "VISIT-001",
        //    "refund_amount" : 2000,
        //    "enteredbhcode" : 1,
        //    "cntcode"       : 1,
        //    "usercode"      : 10,
        //    "computercode"  : 1,
        //    "remarks"       : "IP discharge — refund surplus advance"
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/refund")]
        public async Task<IActionResult> RefundAdvance([FromBody] HmsAdvanceRefundRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.RefundAdvance(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding advance for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpPost("due-bills")]
        public async Task<IActionResult> GetAllDueBills(
    [FromBody] HmsAllDueFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) =
                    await _service.GetAllDueBills(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all due bills.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("paid-history")]
        public async Task<IActionResult> GetPaidHistory(
            [FromBody] HmsPaidHistoryFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) =
                    await _service.GetPaidHistory(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paid history.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}