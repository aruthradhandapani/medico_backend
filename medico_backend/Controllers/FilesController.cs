using medico_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly S3ImageService _s3Service;
        private readonly ILogger<FilesController> _logger;

        public FilesController(S3ImageService s3Service, ILogger<FilesController> logger)
        {
            _s3Service = s3Service;
            _logger = logger;
        }

        // POST: api/files/upload
        // Query:  ?entityType=customers&entityId=101&prefix=customer
        // Header: tenant_code
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
            [FromForm] IFormFile file,
            [FromQuery] string entityType,   // "customers" | "users"
            [FromQuery] long entityId,
            [FromQuery] string prefix)       // "customer" | "signature" | "avatar"
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var tenant_code = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required.");

            if (string.IsNullOrEmpty(entityType) || entityId <= 0)
                return BadRequest("entityType and entityId are required.");

            var key = await _s3Service.UploadAsync(file, tenant_code, entityType, entityId, prefix);
            if (key == null)
                return BadRequest("Upload failed.");

            return Ok(new { key });
        }

        // GET: api/files/download?key=LabCare/tenant1/customers/101/customer_photo.jpg
        [HttpGet("download")]
        public async Task<IActionResult> Download([FromQuery] string key)
        {
            if (string.IsNullOrEmpty(key))
                return BadRequest("key is required.");

            var result = await _s3Service.DownloadAsync(key);
            if (result == null)
                return NotFound("File not found.");

            return Ok(new
            {
                FileName = result.Value.FileName,
                Base64 = result.Value.Data,
                ContentType = result.Value.ContentType
            });
        }

        // DELETE: api/files/delete?key=LabCare/tenant1/customers/101/customer_photo.jpg
        [HttpDelete("delete")]
        public async Task<IActionResult> Delete([FromQuery] string key)
        {
            if (string.IsNullOrEmpty(key))
                return BadRequest("key is required.");

            await _s3Service.DeleteAsync(key);
            return Ok(new { Message = "File deleted successfully." });
        }

        // GET: api/files/list?entityType=customers&entityId=101
        // Header: tenant_code
        [HttpGet("list")]
        public async Task<IActionResult> List(
            [FromQuery] string entityType,
            [FromQuery] long entityId)
        {
            var tenant_code = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required.");

            var files = await _s3Service.ListAsync(tenant_code, entityType, entityId);
            return Ok(files);
        }
    }
}