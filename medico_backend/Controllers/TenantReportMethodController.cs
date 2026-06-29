using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;
using medico_backend.Services;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantReportMethodController : ControllerBase
    {
        private readonly TenantReportMethodClass _cls;
        private readonly S3ImageService _s3;

        private const string EntityType = "tenant-settings";

        public TenantReportMethodController(TenantReportMethodClass cls, S3ImageService s3)
        {
            _cls = cls;
            _s3 = s3;
        }

        // ─── Get ──────────────────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            var result = await _cls.GetByTenant(tenant);
            return Ok(result);
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
    [FromForm] TenantReportMethodModel data,
    [FromForm] IFormFile? logoFile,
    [FromForm] IFormFile? letterheadFile,
    [FromForm] IFormFile? bottomFile,
    [FromForm] IFormFile? watermarkFile,
    [FromForm] IFormFile? signatureFile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            data.tenant_code = tenant;

            // Step 1: Insert — data.id is set after this
            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            // Step 2: Upload images using data.id directly
            bool needsUpdate = false;

            if (logoFile != null && logoFile.Length > 0)
            {
                data.logo_url = await _s3.UploadAsync(
                    logoFile, tenant, EntityType, data.id, "logo");
                needsUpdate = true;
            }
            if (letterheadFile != null && letterheadFile.Length > 0)
            {
                data.letterhead_url = await _s3.UploadAsync(
                    letterheadFile, tenant, EntityType, data.id, "letterhead");
                needsUpdate = true;
            }
            if (bottomFile != null && bottomFile.Length > 0)
            {
                data.bottom_url = await _s3.UploadAsync(
                    bottomFile, tenant, EntityType, data.id, "bottom");
                needsUpdate = true;
            }
            if (watermarkFile != null && watermarkFile.Length > 0)
            {
                data.watermark_url = await _s3.UploadAsync(
                    watermarkFile, tenant, EntityType, data.id, "watermark");
                needsUpdate = true;
            }
            if (signatureFile != null && signatureFile.Length > 0)
            {
                data.signature_url = await _s3.UploadAsync(
                    signatureFile, tenant, EntityType, data.id, "signature");
                needsUpdate = true;
            }

            // Step 3: Update image keys if any uploaded
            if (needsUpdate)
                await _cls.Update(data);

            return Ok(new { message = "Success", id = data.id });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromForm] TenantReportMethodModel data,
            [FromForm] IFormFile? logoFile,
            [FromForm] IFormFile? letterheadFile,
            [FromForm] IFormFile? bottomFile,
            [FromForm] IFormFile? watermarkFile,
            [FromForm] IFormFile? signatureFile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            data.tenant_code = tenant;

            // Step 1: Get existing to preserve old image keys
            var existing = await _cls.GetByTenant(tenant);
            if (existing == null)
                return NotFound(new { message = "Settings not found for this tenant" });

            long entityId = existing.id;

            // Step 2: Replace or preserve each image
            data.logo_url = await _s3.ReplaceAsync(
                logoFile, existing.logo_url,
                tenant, EntityType, entityId, "logo");

            data.letterhead_url = await _s3.ReplaceAsync(
                letterheadFile, existing.letterhead_url,
                tenant, EntityType, entityId, "letterhead");

            data.bottom_url = await _s3.ReplaceAsync(
                bottomFile, existing.bottom_url,
                tenant, EntityType, entityId, "bottom");

            data.watermark_url = await _s3.ReplaceAsync(
                watermarkFile, existing.watermark_url,
                tenant, EntityType, entityId, "watermark");

            data.signature_url = await _s3.ReplaceAsync(
                signatureFile, existing.signature_url,
                tenant, EntityType, entityId, "signature");

            // Step 3: Update DB
            var res = await _cls.Update(data);
            return res == "Success"
                ? Ok(new { message = "Updated successfully" })
                : BadRequest(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete([FromQuery] int id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            var existing = await _cls.GetByTenant(tenant);

            var res = await _cls.Delete(id, tenant);  // pass both
            if (res != "Success")
                return BadRequest(new { message = res });

            if (existing != null)
            {
                await _s3.DeleteAsync(existing.logo_url);
                await _s3.DeleteAsync(existing.letterhead_url);
                await _s3.DeleteAsync(existing.bottom_url);
                await _s3.DeleteAsync(existing.watermark_url);
                await _s3.DeleteAsync(existing.signature_url);
            }

            return Ok(new { message = "Deleted successfully" });
        }
    }
}