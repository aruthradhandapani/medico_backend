using medico_backend.Class;
using medico_backend.Model;
using medico_backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LabTestResultController : ControllerBase
    {
        private readonly LabTestResultClass _cls;

        public LabTestResultController(LabTestResultClass cls)
        {
            _cls = cls;
        }

        [HttpGet("LoadResultEntry")]
        public async Task<IActionResult> LoadResultEntry([FromQuery] string requestGuid)
        {
            try
            {
                var tenant_code = Request.Headers["tenant_code"].ToString();
                var result = await _cls.GetResult(requestGuid, tenant_code);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        [HttpGet("LoadTestResultDetails")]
        public async Task<IActionResult> LoadTestResultDetails([FromQuery] Guid testResultId)
        {
            try
            {
                var tenant_code = Request.Headers["tenant_code"].ToString();
                var result = await _cls.GetTestResultDetails(testResultId, tenant_code);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("ViewResultSearch")]
        public async Task<IActionResult> GetResultList(
    [FromQuery] int dcode,
    [FromQuery] string fromdate,
    [FromQuery] string todate)
        {
            // tenantCode is resolved from the JWT / request context the same way
            // every other endpoint in LIMS_Backend does it.
            //string? tenantCode = User.FindFirst("tenant_code")?.Value;
            string tenantCode = null;
            if (tenantCode == null)
            {
                tenantCode = Request.Headers["tenant_code"].ToString();
            }

            if (string.IsNullOrWhiteSpace(tenantCode))
                return Unauthorized("Tenant context missing.");

            if (string.IsNullOrWhiteSpace(fromdate) || string.IsNullOrWhiteSpace(todate))
                return BadRequest("fromdate and todate are required.");

            try
            {
                var result = await _cls.GetResultList(dcode, fromdate, todate, tenantCode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetResultList] Controller error: {ex.Message}");
                return StatusCode(500, "An error occurred while fetching results.");
            }
        }

        [HttpGet("deltacheck")]
        public async Task<IActionResult> GetCustomerResults([FromQuery] string custcode)
        {
            try
            {
                var tenantCode = Request.Headers["tenant_code"].ToString();
                var result = await _cls.GetCustomerResultsLoading(custcode, tenantCode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching CustomerResults: {ex.Message}");
                return BadRequest(ex.Message.ToString());
            }
        }


        // ── Save result entry (supports both JSON and multipart/form-data) ──
        // When the client sends multipart/form-data, individual image_file fields
        // are bound automatically by ASP.NET Core model binding.
        // When the client sends application/json, proof_image_base64 can be used instead.
        [HttpPost("saveresultentry")]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue, ValueCountLimit = int.MaxValue)]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> SaveResult([FromForm] List<LabResultEntry> entries)
        {
            try
            {
                string? tenantCode = User.FindFirst("tenant_code")?.Value
                                     ?? Request.Headers["tenant_code"].ToString();

                var result = await _cls.SaveResult(entries, tenantCode);
                return Ok(new { status = "Success", message = "Result saved successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller:SaveResult] {ex.GetType().Name}: {ex.Message}\n{ex}");
                return StatusCode(500, new
                {
                    status = "Failed",
                    message = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }
    }
}