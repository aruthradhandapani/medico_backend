using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabResultController : ControllerBase
    {
        private readonly LabResultClass _cls;

        public LabResultController(LabResultClass cls)
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
            string? tenantCode = Request.Headers["tenant_code"].ToString();

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

        [HttpPost("saveresultentry")]
        public async Task<IActionResult> SaveResult([FromBody] List<LabResultEntry> results)
        {
            try
            {
                string? tenantCode = Request.Headers["tenant_code"].ToString();
                if (string.IsNullOrWhiteSpace(tenantCode))
                {
                    tenantCode = User.FindFirst("tenant_code")?.Value ?? "";
                }

                if (results is not { Count: > 0 })
                    return BadRequest(new { status = "Failed", message = "No entries supplied." });

                await _cls.SaveResult(results, tenantCode);
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

        [HttpPost("saveresult")]
        public async Task<IActionResult> SaveResultAlias([FromBody] List<LabResultEntry> results)
        {
            return await SaveResult(results);
        }

        [HttpPost("saveresultimage")]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue, ValueCountLimit = int.MaxValue)]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> SaveResultImage([FromForm] IList<ResultImageUpload> upload)
        {
            try
            {
                if (upload is not { Count: > 0 })
                    return BadRequest(new { status = "Failed", message = "No images supplied." });

                string? tenantCode = Request.Headers["tenant_code"].ToString();

                var results = new List<object>();

                foreach (var item in upload)
                {
                    if (item.testresultid == Guid.Empty || item.image_file is not { Length: > 0 })
                    {
                        results.Add(new { testresultid = item.testresultid, status = "Skipped", message = "Missing testresultid or file." });
                        continue;
                    }

                    try
                    {
                        var imagePath = await _cls.SaveResultImage(item.testresultid, item.image_file, tenantCode);

                        results.Add(imagePath == null
                            ? new { testresultid = item.testresultid, status = "Failed", message = "No matching result row found." }
                            : new { testresultid = item.testresultid, status = "Success", image_path = imagePath });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Controller:SaveResultImage] testresultid={item.testresultid} {ex.GetType().Name}: {ex.Message}");
                        results.Add(new { testresultid = item.testresultid, status = "Failed", message = ex.Message });
                    }
                }

                return Ok(new { status = "Completed", results });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller:SaveResultImage] {ex.GetType().Name}: {ex.Message}\n{ex}");
                return StatusCode(500, new { status = "Failed", message = ex.Message, detail = ex.InnerException?.Message });
            }
        }
    }
}
