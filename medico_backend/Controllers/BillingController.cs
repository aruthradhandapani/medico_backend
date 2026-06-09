using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class HmsBillingController : ControllerBase
    {
        private readonly HmsBillingClass _billingService;

        public HmsBillingController(HmsBillingClass billingService)
        {
            _billingService = billingService;
        }

        // Helper framework parsing tenant claims identifiers safely
        private string GetTenantCode()
        {
            // 1. Try JWT claim first
            var fromClaim = User.FindFirst("tenant_code")?.Value
                         ?? User.FindFirst("TenantCode")?.Value;
            if (!string.IsNullOrEmpty(fromClaim)) return fromClaim;

            // 2. Read from request header
            if (Request.Headers.TryGetValue("tenant_code", out var headerVal)
                && !string.IsNullOrEmpty(headerVal))
                return headerVal.ToString();

            // 3. Last fallback
            return "SYSTEM";
        }

        [HttpPost("save-bill")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HmsBillResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SaveBill([FromBody] CreateHmsBillRequest request)
        {
            string tenantToken = GetTenantCode();
            var (status, data) = await _billingService.SaveBill(request, tenantToken);

            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(data);
        }

        [HttpGet("get-bill/{requestGuid}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HmsBillResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBillByGuid(string requestGuid)
        {
            string tenantToken = GetTenantCode();
            var result = await _billingService.FetchBillRecordByGuid(requestGuid, tenantToken);

            if (result == null)
                return NotFound(new { message = "Requested billing document resource not tracked down inside workspace." });

            return Ok(result);
        }

        [HttpPost("list-bills")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
        public async Task<IActionResult> GetPaginatedBillsList([FromBody] HmsBillFilterRequest criteria)
        {
            string tenantToken = GetTenantCode();
            var (recordsList, totalCount) = await _billingService.QueryPaginatedBillsList(criteria, tenantToken);

            return Ok(new
            {
                total = totalCount,
                page = criteria.page,
                pageSize = criteria.pagesize,
                data = recordsList
            });
        }

        [HttpPost("add-payment")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HmsBillResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecordSettlePayment([FromBody] AddHmsPaymentRequest paymentPayload)
        {
            string tenantToken = GetTenantCode();
            var (status, data) = await _billingService.AddPayment(paymentPayload, tenantToken);

            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(data);
        }

        [HttpPost("cancel-bill")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TerminateInvoiceEntry([FromBody] CancelHmsBillRequest cancelPayload)
        {
            string tenantToken = GetTenantCode();
            string status = await _billingService.CancelBill(cancelPayload, tenantToken);

            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(new { message = "Billing matrix and relative receipting lines successfully voided." });
        }

        [HttpPost("counter/open-shift")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HmsCounterTimingDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InitializeSessionShift([FromBody] HmsCounterTiming shiftParams)
        {
            string tenantToken = GetTenantCode();
            var (status, session) = await _billingService.OpenCounterShift(shiftParams, tenantToken);

            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(session);
        }

        [HttpPost("counter/close-shift")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CloseActiveShiftSession([FromBody] CloseCounterRequest closePayload)
        {
            string tenantToken = GetTenantCode();
            string status = await _billingService.CloseCounterShift(closePayload, tenantToken);

            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(new { message = "Operational work window safely shut down recorded." });
        }

        [HttpGet("reports/daily-collection")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<HmsDailyCollectionDto>))]
        public async Task<IActionResult> RetrieveDailyCollectionSummary([FromQuery] int branchCode, [FromQuery] DateTime date)
        {
            string tenantToken = GetTenantCode();
            var metricsData = await _billingService.ExtractDailyCollectionSummaryReport(branchCode, date, tenantToken);
            return Ok(metricsData);
        }
    }
}