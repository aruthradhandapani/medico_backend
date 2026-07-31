using medico_backend.Class;
using medico_backend.Services;
using Microsoft.AspNetCore.Mvc;
using static LabSettingModel;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabSettingController : ControllerBase
    {
        private readonly LabSettingClass _labSettingClass;
        private readonly S3ImageService _s3Service;

        private const string EntityType = "labsettings";

        public LabSettingController(LabSettingClass labSettingClass, S3ImageService s3Service)
        {
            _labSettingClass = labSettingClass;
            _s3Service = s3Service;
        }

        private string GetTenantCode()
        {
            var tenantCode = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                var user = HttpContext.User;
                tenantCode = user?.FindFirst("tenant_code")?.Value ?? user?.FindFirst("TenantCode")?.Value ?? "";
            }
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                tenantCode = "0010";
            }
            return tenantCode;
        }

        // ─── Get (filtered by bh_code) ─────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get([FromQuery] int? bh_code)
        {
            try
            {
                var tenant_code = GetTenantCode();
                var result = await _labSettingClass.GetLab_Settings(bh_code, tenant_code);
                return Ok(result ?? new List<lab_settings>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.ToString() });
            }
        }

        // ─── Get All ────────────────────────────────────────────────────
        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var tenant_code = GetTenantCode();
                var result = await _labSettingClass.GetAll(tenant_code);
                return Ok(result ?? new List<lab_settings>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.ToString() });
            }
        }

        // ─── Insert ─────────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromForm] lab_settings model,
            [FromForm] IFormFile? headerFile,
            [FromForm] IFormFile? footerFile,
            [FromForm] IFormFile? auth1SignatureFile,
            [FromForm] IFormFile? auth2SignatureFile,
            [FromForm] IFormFile? auth3SignatureFile,
            [FromForm] IFormFile? cultureAuth1SignatureFile,
            [FromForm] IFormFile? cultureAuth2SignatureFile,
            [FromForm] IFormFile? cultureAuth3SignatureFile)
        {
            try
            {
                var tenant_code = GetTenantCode();

                var (lsid, error) = await _labSettingClass.Insert(model, tenant_code);
                if (lsid == Guid.Empty)
                    return BadRequest(new { message = "Insertion failed", detail = error });

                model.lsid = lsid;
                await UploadAndSaveAllImages(
                    null,
                    model,
                    tenant_code,
                    headerFile,
                    footerFile,
                    auth1SignatureFile,
                    auth2SignatureFile,
                    auth3SignatureFile,
                    cultureAuth1SignatureFile,
                    cultureAuth2SignatureFile,
                    cultureAuth3SignatureFile);

                return Ok(new
                {
                    message = "Inserted Successfully",
                    lsid,
                    header_path = model.header_path,
                    footer_path = model.footer_path,
                    auth1_signature_path = model.auth1_signature_path,
                    auth2_signature_path = model.auth2_signature_path,
                    auth3_signature_path = model.auth3_signature_path,
                    culture_auth1_signature_path = model.culture_auth1_signature_path,
                    culture_auth2_signature_path = model.culture_auth2_signature_path,
                    culture_auth3_signature_path = model.culture_auth3_signature_path
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Update ─────────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromForm] lab_settings model,
            [FromForm] IFormFile? headerFile,
            [FromForm] IFormFile? footerFile,
            [FromForm] IFormFile? auth1SignatureFile,
            [FromForm] IFormFile? auth2SignatureFile,
            [FromForm] IFormFile? auth3SignatureFile,
            [FromForm] IFormFile? cultureAuth1SignatureFile,
            [FromForm] IFormFile? cultureAuth2SignatureFile,
            [FromForm] IFormFile? cultureAuth3SignatureFile)
        {
            try
            {
                var tenant_code = GetTenantCode();

                // 1. Load existing record BEFORE modifying DB so we have the real old MinIO keys
                lab_settings? existing = null;
                if (model.lsid != Guid.Empty)
                    existing = await _labSettingClass.GetByLsid(model.lsid, tenant_code);

                // 2. Perform image replacements (ReplaceAsync deletes old MinIO files & uploads new ones)
                await UploadAndSaveAllImages(
                    existing,
                    model,
                    tenant_code,
                    headerFile,
                    footerFile,
                    auth1SignatureFile,
                    auth2SignatureFile,
                    auth3SignatureFile,
                    cultureAuth1SignatureFile,
                    cultureAuth2SignatureFile,
                    cultureAuth3SignatureFile);

                // 3. Save updated model into PostgreSQL
                var (success, error) = await _labSettingClass.Update(model, tenant_code);
                if (!success)
                    return BadRequest(new { message = "Update failed", detail = error });

                return Ok(new
                {
                    message = "Updated Successfully",
                    header_path = model.header_path,
                    footer_path = model.footer_path,
                    auth1_signature_path = model.auth1_signature_path,
                    auth2_signature_path = model.auth2_signature_path,
                    auth3_signature_path = model.auth3_signature_path,
                    culture_auth1_signature_path = model.culture_auth1_signature_path,
                    culture_auth2_signature_path = model.culture_auth2_signature_path,
                    culture_auth3_signature_path = model.culture_auth3_signature_path
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Upload Header / Footer / Signature Images ──────────────────────────────
        [HttpPost("upload-images")]
        public async Task<IActionResult> UploadImages(
            [FromForm] Guid lsid,
            [FromForm] int? bh_code,
            [FromForm] IFormFile? headerFile,
            [FromForm] IFormFile? footerFile,
            [FromForm] IFormFile? auth1SignatureFile,
            [FromForm] IFormFile? auth2SignatureFile,
            [FromForm] IFormFile? auth3SignatureFile,
            [FromForm] IFormFile? cultureAuth1SignatureFile,
            [FromForm] IFormFile? cultureAuth2SignatureFile,
            [FromForm] IFormFile? cultureAuth3SignatureFile)
        {
            try
            {
                var tenant_code = GetTenantCode();

                if (lsid == Guid.Empty)
                    return BadRequest(new { message = "Valid lsid is required" });

                var existing = await _labSettingClass.GetByLsid(lsid, tenant_code);
                if (existing == null)
                    return NotFound(new { message = "Lab setting not found" });

                if (bh_code.HasValue)
                    existing.bh_code = bh_code.Value;

                await UploadAndSaveAllImages(
                    existing,
                    existing,
                    tenant_code,
                    headerFile,
                    footerFile,
                    auth1SignatureFile,
                    auth2SignatureFile,
                    auth3SignatureFile,
                    cultureAuth1SignatureFile,
                    cultureAuth2SignatureFile,
                    cultureAuth3SignatureFile);

                return Ok(new
                {
                    message = "Images and signatures updated successfully",
                    header_path = existing.header_path,
                    footer_path = existing.footer_path,
                    auth1_signature_path = existing.auth1_signature_path,
                    auth2_signature_path = existing.auth2_signature_path,
                    auth3_signature_path = existing.auth3_signature_path,
                    culture_auth1_signature_path = existing.culture_auth1_signature_path,
                    culture_auth2_signature_path = existing.culture_auth2_signature_path,
                    culture_auth3_signature_path = existing.culture_auth3_signature_path
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Soft Delete ────────────────────────────────────────────────
        [HttpGet("softdelete")]
        public async Task<IActionResult> SoftDelete([FromQuery] Guid lsid)
        {
            try
            {
                var tenant_code = Request.Headers["tenant_code"].ToString();
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "Tenant code is required" });

                var (success, error) = await _labSettingClass.SoftDelete(lsid, tenant_code);
                return success
                    ? Ok(new { message = "Lab setting soft deleted successfully" })
                    : BadRequest(new { message = error });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Hard Delete ────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete([FromQuery] Guid lsid)
        {
            try
            {
                var tenant_code = Request.Headers["tenant_code"].ToString();
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "Tenant code is required" });

                var (success, error) = await _labSettingClass.Delete(lsid, tenant_code);
                return success
                    ? Ok(new { message = "Lab setting deleted permanently" })
                    : BadRequest(new { message = error });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Private Helpers ───────────────────────────────────────────
        private static string? SanitizeKey(string? key, string bucketName = "labcare")
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            key = key.Trim();
            if (key.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(key, UriKind.Absolute, out var uri))
                {
                    key = uri.AbsolutePath.TrimStart('/');
                    if (key.StartsWith(bucketName + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        key = key[(bucketName.Length + 1)..];
                    }
                }
            }
            key = key.TrimStart('/');
            if (key.StartsWith(bucketName + "/", StringComparison.OrdinalIgnoreCase))
            {
                key = key[(bucketName.Length + 1)..];
            }
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }

        private async Task<lab_settings?> GetExistingLabSettingAsync(lab_settings model, string tenantCode)
        {
            if (model.lsid != Guid.Empty)
            {
                var existing = await _labSettingClass.GetByLsid(model.lsid, tenantCode);
                if (existing != null) return existing;
            }

            var settings = await _labSettingClass.GetLab_Settings(model.bh_code, tenantCode);
            return settings.FirstOrDefault();
        }

        private async Task UploadAndSaveAllImages(
            lab_settings? existing,
            lab_settings model,
            string tenant_code,
            IFormFile? headerFile,
            IFormFile? footerFile,
            IFormFile? auth1SigFile,
            IFormFile? auth2SigFile,
            IFormFile? auth3SigFile,
            IFormFile? cultureAuth1SigFile = null,
            IFormFile? cultureAuth2SigFile = null,
            IFormFile? cultureAuth3SigFile = null)
        {
            if (existing == null)
                existing = await GetExistingLabSettingAsync(model, tenant_code);

            // Extract & sanitize true old MinIO object keys from database prior to any update
            string? headerOld = SanitizeKey(existing?.header_path);
            string? footerOld = SanitizeKey(existing?.footer_path);
            string? sig1Old = SanitizeKey(existing?.auth1_signature_path);
            string? sig2Old = SanitizeKey(existing?.auth2_signature_path);
            string? sig3Old = SanitizeKey(existing?.auth3_signature_path);
            string? cultureSig1Old = SanitizeKey(existing?.culture_auth1_signature_path);
            string? cultureSig2Old = SanitizeKey(existing?.culture_auth2_signature_path);
            string? cultureSig3Old = SanitizeKey(existing?.culture_auth3_signature_path);

            string? headerKey = null;
            string? footerKey = null;
            string? sig1Key = null;
            string? sig2Key = null;
            string? sig3Key = null;
            string? cultureSig1Key = null;
            string? cultureSig2Key = null;
            string? cultureSig3Key = null;
            long entityId = model.bh_code ?? existing?.bh_code ?? 0;

            // ReplaceAsync = DeleteAsync(oldKey) + UploadAsync(newFile)
            if (headerFile != null && headerFile.Length > 0)
                headerKey = await _s3Service.ReplaceAsync(headerFile, headerOld, tenant_code, EntityType, entityId, "header");
            else if (string.IsNullOrWhiteSpace(model.header_path))
                model.header_path = headerOld;

            if (footerFile != null && footerFile.Length > 0)
                footerKey = await _s3Service.ReplaceAsync(footerFile, footerOld, tenant_code, EntityType, entityId, "footer");
            else if (string.IsNullOrWhiteSpace(model.footer_path))
                model.footer_path = footerOld;

            if (auth1SigFile != null && auth1SigFile.Length > 0)
                sig1Key = await _s3Service.ReplaceAsync(auth1SigFile, sig1Old, tenant_code, EntityType, entityId, "signature1");
            else if (string.IsNullOrWhiteSpace(model.auth1_signature_path))
                model.auth1_signature_path = sig1Old;

            if (auth2SigFile != null && auth2SigFile.Length > 0)
                sig2Key = await _s3Service.ReplaceAsync(auth2SigFile, sig2Old, tenant_code, EntityType, entityId, "signature2");
            else if (string.IsNullOrWhiteSpace(model.auth2_signature_path))
                model.auth2_signature_path = sig2Old;

            if (auth3SigFile != null && auth3SigFile.Length > 0)
                sig3Key = await _s3Service.ReplaceAsync(auth3SigFile, sig3Old, tenant_code, EntityType, entityId, "signature3");
            else if (string.IsNullOrWhiteSpace(model.auth3_signature_path))
                model.auth3_signature_path = sig3Old;

            if (cultureAuth1SigFile != null && cultureAuth1SigFile.Length > 0)
                cultureSig1Key = await _s3Service.ReplaceAsync(cultureAuth1SigFile, cultureSig1Old, tenant_code, EntityType, entityId, "culturesignature1");
            else if (string.IsNullOrWhiteSpace(model.culture_auth1_signature_path))
                model.culture_auth1_signature_path = cultureSig1Old;

            if (cultureAuth2SigFile != null && cultureAuth2SigFile.Length > 0)
                cultureSig2Key = await _s3Service.ReplaceAsync(cultureAuth2SigFile, cultureSig2Old, tenant_code, EntityType, entityId, "culturesignature2");
            else if (string.IsNullOrWhiteSpace(model.culture_auth2_signature_path))
                model.culture_auth2_signature_path = cultureSig2Old;

            if (cultureAuth3SigFile != null && cultureAuth3SigFile.Length > 0)
                cultureSig3Key = await _s3Service.ReplaceAsync(cultureAuth3SigFile, cultureSig3Old, tenant_code, EntityType, entityId, "culturesignature3");
            else if (string.IsNullOrWhiteSpace(model.culture_auth3_signature_path))
                model.culture_auth3_signature_path = cultureSig3Old;

            if (headerKey != null || footerKey != null || sig1Key != null || sig2Key != null || sig3Key != null ||
                cultureSig1Key != null || cultureSig2Key != null || cultureSig3Key != null)
            {
                if (headerKey != null) { model.header_path = headerKey; model.header_image_path = headerKey; }
                if (footerKey != null) { model.footer_path = footerKey; model.footer_image_path = footerKey; }
                if (sig1Key != null) model.auth1_signature_path = sig1Key;
                if (sig2Key != null) model.auth2_signature_path = sig2Key;
                if (sig3Key != null) model.auth3_signature_path = sig3Key;
                if (cultureSig1Key != null) model.culture_auth1_signature_path = cultureSig1Key;
                if (cultureSig2Key != null) model.culture_auth2_signature_path = cultureSig2Key;
                if (cultureSig3Key != null) model.culture_auth3_signature_path = cultureSig3Key;

                await _labSettingClass.UpdateImageAndSignaturePaths(
                    model.lsid,
                    tenant_code,
                    model.header_path,
                    model.footer_path,
                    model.auth1_signature_path,
                    model.auth2_signature_path,
                    model.auth3_signature_path,
                    model.culture_auth1_signature_path,
                    model.culture_auth2_signature_path,
                    model.culture_auth3_signature_path);
            }
        }
    }
}