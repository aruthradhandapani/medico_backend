using medico_backend.Services;
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly TestClass _cls;
        private readonly S3ImageService _s3;
        private readonly ILogger<TestController> _logger;

        private const string EntityType = "test-results";
        private const string ImagePrefix = "result";

        public TestController(
            TestClass cls,
            S3ImageService s3,
            ILogger<TestController> logger)
        {
            _cls = cls;
            _s3 = s3;
            _logger = logger;
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private string? TenantCode =>
            Request.Headers.TryGetValue("tenant_code", out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.ToString() : null;

        // ── GET all ───────────────────────────────────────────────────────────

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            try
            {
                var res = await _cls.Get_Data(TenantCode);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get tests failed");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── INSERT ────────────────────────────────────────────────────────────
        // testImageFiles is indexed 1-to-1 with dto.ResultRows
        // testImageFiles[0] → image for ResultRows[0], etc.

        [HttpPost("insert")]
        [RequestSizeLimit(100_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
        public async Task<IActionResult> Add_Test(
            [FromForm] TestInsertDto dto,
            [FromForm] List<IFormFile>? testImageFiles)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            try
            {
                // Insert returns (result, tcode, resultIds) — resultIds indexed to ResultRows
                var (result, generatedTcode, resultIds) =
                    await _cls.Insert_Test(dto, TenantCode);

                if (result != "Success")
                    return BadRequest(new { message = result });

                // Upload image per result row and save key to test_result_master
                if (testImageFiles?.Count > 0 && resultIds?.Count > 0)
                {
                    int uploadCount = Math.Min(testImageFiles.Count, resultIds.Count);

                    for (int i = 0; i < uploadCount; i++)
                    {
                        var file = testImageFiles[i];
                        var resultId = resultIds[i];

                        if (file is not { Length: > 0 }) continue;

                        var imageKey = await _s3.UploadAsync(
                            file, TenantCode, EntityType,
                            (int)generatedTcode!.Value,
                            $"{ImagePrefix}-{resultId}");

                        if (imageKey is not null)
                        {
                            // Saves imageKey → test_result_master.testimage
                            await _cls.UpdateResultImage(resultId, TenantCode, imageKey);
                            _logger.LogInformation(
                                "Insert: result row [{Id}] image saved → {Key}",
                                resultId, imageKey);
                        }
                    }
                }

                return Ok(new { message = "Inserted Successfully", tcode = generatedTcode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Insert test failed");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────────────
        // For existing result rows  → ReplaceAsync (old S3 key removed, new one saved)
        // For brand-new result rows → UploadAsync  (class assigns testresultid in-place)

        [HttpPost("update")]
        [RequestSizeLimit(100_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
        public async Task<IActionResult> Update_Test(
            [FromForm] TestInsertDto dto,
            [FromForm] List<IFormFile>? testImageFiles)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.TestMaster.tcode <= 0)
                return BadRequest(new { message = "Invalid tcode." });
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            try
            {
                // ── Pass 1 (pre-update): replace images on existing result rows ──
                // Must happen before Update_Test so the new key is stamped on the DTO
                // and persisted by Dapper's UpdateAsync inside the class.
                if (testImageFiles?.Count > 0 && dto.ResultRows?.Count > 0)
                {
                    int count = Math.Min(testImageFiles.Count, dto.ResultRows.Count);

                    for (int i = 0; i < count; i++)
                    {
                        var trm = dto.ResultRows[i].ResultMaster;
                        var file = testImageFiles[i];

                        // trcode == 0  →  new row, skip here (handled in Pass 2 post-update)
                        if (trm is null || trm.trcode <= 0 || file is not { Length: > 0 })
                            continue;

                        var existingKey = await _cls.GetResultImage(trm.testresultid, TenantCode);

                        var newKey = await _s3.ReplaceAsync(
                            file, existingKey, TenantCode, EntityType,
                            (int)dto.TestMaster.tcode,
                            $"{ImagePrefix}-{trm.testresultid}");

                        // Stamp on DTO — Update_Test's UpdateAsync will persist it
                        trm.testimage = newKey;
                    }
                }

                var updateResult = await _cls.Update_Test(dto, TenantCode);

                if (updateResult != "Success")
                    return BadRequest(new { message = updateResult });

                // ── Pass 2 (post-update): upload images for brand-new result rows ──
                // After Update_Test the class has called Guid.NewGuid() on new rows
                // and written it back to trm.testresultid (reference type — visible here).
                // trcode is still 0 in memory (InsertAsync doesn't write it back), 
                // so that remains our reliable "new row" signal.
                if (testImageFiles?.Count > 0 && dto.ResultRows?.Count > 0)
                {
                    int count = Math.Min(testImageFiles.Count, dto.ResultRows.Count);

                    for (int i = 0; i < count; i++)
                    {
                        var trm = dto.ResultRows[i].ResultMaster;
                        var file = testImageFiles[i];

                        // trcode > 0  →  existing row, already handled in Pass 1
                        if (trm is null || trm.trcode > 0 || file is not { Length: > 0 })
                            continue;

                        var imageKey = await _s3.UploadAsync(
                            file, TenantCode, EntityType,
                            (int)dto.TestMaster.tcode,
                            $"{ImagePrefix}-{trm.testresultid}");

                        if (imageKey is not null)
                        {
                            // Saves imageKey → test_result_master.testimage
                            await _cls.UpdateResultImage(trm.testresultid, TenantCode, imageKey);
                            _logger.LogInformation(
                                "Update: new result row [{Id}] image saved → {Key}",
                                trm.testresultid, imageKey);
                        }
                    }
                }

                return Ok(new { message = "Updated Successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update test [{Tcode}] failed", dto.TestMaster.tcode);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── DELETE (hard) ─────────────────────────────────────────────────────
        // Fetches ALL result image keys in ONE query before deleting DB rows,
        // then cleans up S3 after the transaction succeeds.

        [HttpGet("delete")]
        public async Task<IActionResult> Delete_Test([FromQuery] int tcode)
        {
            if (tcode <= 0)
                return BadRequest(new { message = "Invalid tcode." });
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            try
            {
                // Single query — avoids N+1 calls from looping GetResultImage per row
                var imageKeys = await _cls.GetResultImagesByTcode(tcode, TenantCode);

                var result = await _cls.Delete_Test(tcode, TenantCode);

                if (result != "Success")
                    return BadRequest(new { message = result });

                // Remove S3 objects only after DB rows are gone
                foreach (var key in imageKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                    await _s3.DeleteAsync(key);

                return Ok(new { message = "Permanently Deleted Successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hard delete test [{Tcode}] failed", tcode);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── SOFT DELETE ───────────────────────────────────────────────────────

        [HttpGet("softdelete")]
        public async Task<IActionResult> SoftDelete_Test([FromQuery] int tcode)
        {
            if (tcode <= 0) return BadRequest("Invalid tcode");
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("tenant_code is required in header");

            var res = await _cls.SoftDelete_Test(tcode, TenantCode);
            return res == "Success" ? Ok("Soft Deleted Successfully") : BadRequest(res);
        }

        // ── GET FULL RESULT ───────────────────────────────────────────────────

        [HttpGet("result/get")]
        public async Task<IActionResult> Get_TestResult([FromQuery] long tcode)
        {
            if (tcode <= 0) return BadRequest("Invalid tcode");
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("tenant_code is required in header");

            var res = await _cls.Get_TestResult(tcode, TenantCode);
            return res is null ? NotFound("No result found") : Ok(res);
        }
    }
}