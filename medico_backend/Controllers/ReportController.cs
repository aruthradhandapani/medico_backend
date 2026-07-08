using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly ReportClass _reportClass;
        
        public ReportController(ReportClass reportClass)
        {
            _reportClass = reportClass;
        }

        private string T => Request.Headers["tenant_code"].ToString();
        private int Me => int.Parse(User.FindFirst("user_code")?.Value ?? "1");

        [HttpGet("getstatement")]
        public async Task<IActionResult> Statement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.StatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getduestatement")]
        public async Task<IActionResult> DueStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.DueStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getdiscountstatement")]
        public async Task<IActionResult> DiscountStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.DiscountStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getreferralstatement")]
        public async Task<IActionResult> ReferralStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.ReferralStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getdiscountsummary")]
        public async Task<IActionResult> DiscountSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.DiscountSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getduesummary")]
        public async Task<IActionResult> DueSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.DueSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getgroupstatement")]
        public async Task<IActionResult> GroupStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.GroupStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getgroupsummary")]
        public async Task<IActionResult> GroupSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string periodtype)
        {
            var res = await _reportClass.GroupSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getteststatement")]
        public async Task<IActionResult> TestStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.TestStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("gettestsummary")]
        public async Task<IActionResult> TestSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string periodtype)
        {
            var res = await _reportClass.TestSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getreferralsummary")]
        public async Task<IActionResult> ReferralSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string periodtype)
        {
            var res = await _reportClass.ReferralSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getsummary")]
        public async Task<IActionResult> Summary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.SummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getreferralreceipt")]
        public async Task<IActionResult> ReferralReceipt([FromQuery] Guid receiptguid)
        {
            var res = await _reportClass.ReferralReceiptPDF(receiptguid, T);
            return Ok(res);
        }

        [HttpGet("getpatientreceipt")]
        public async Task<IActionResult> PatientReceipt([FromQuery] Guid receiptguid)
        {
            var res = await _reportClass.PatientReceiptPDF(receiptguid, T);
            return Ok(res);
        }

        [HttpGet("getbill")]
        public async Task<IActionResult> Bill([FromQuery] Guid requestguid)
        {
            var res = await _reportClass.BillPDF(requestguid, T);
            return Ok(res);
        }

        [HttpGet("getworklist")]
        public async Task<IActionResult> Worklist(
            [FromQuery] Guid? requestguid,
            [FromQuery] DateTime? fromdate,
            [FromQuery] DateTime? todate,
            [FromQuery] string? gcode)
        {
            var res = await _reportClass.WorklistPDF(requestguid, fromdate, todate, gcode, T);
            return Ok(res);
        }

        [HttpGet("culture-report")]
        public async Task<IActionResult> GetCultureReport([FromQuery] Guid requestguid, [FromQuery] bool? isletterhead = false)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var result = await _reportClass.GetCultureReportAsync(requestguid, T, isletterhead);

            return Ok(result);
        }

        [HttpGet("labreport")]
        public async Task<IActionResult> GetLabReport([FromQuery] Guid requestguid, [FromQuery] bool? isletterhead)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var result = await _reportClass.GetLabReportAsync(requestguid, T, isletterhead);

            return Ok(result);
        }

        [HttpGet("getpaymodestatement")]
        public async Task<IActionResult> PayModeStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.PayModeStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getpaymodesummary")]
        public async Task<IActionResult> PayModeSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.PayModeSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }
    }
}
