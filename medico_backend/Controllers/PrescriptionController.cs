using medico_backend.Class;
using medico_backend.Model;
using medico_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PrescriptionController : ControllerBase
    {
        private readonly PrescriptionClass _prescriptionClass;
        private readonly S3PrescriptionService _s3;
        private readonly ILogger<PrescriptionController> _logger;

        public PrescriptionController(
            PrescriptionClass prescriptionClass,
            S3PrescriptionService s3,
            ILogger<PrescriptionController> logger)
        {
            _prescriptionClass = prescriptionClass;
            _s3 = s3;
            _logger = logger;
        }

        // POST: api/prescription/upload
        // Form: file, bucketName, clientFolder, fileName
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
    [FromForm] IFormFile file,
    [FromForm] string bucketName,
    [FromForm] string clientFolder,
    [FromForm] string fileName)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (string.IsNullOrWhiteSpace(clientFolder))
                return BadRequest("clientFolder is required.");

            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest("fileName is required.");

            if (string.IsNullOrWhiteSpace(bucketName))
                return BadRequest("bucketName is required.");

            // Log exactly what Flutter is sending
            _logger.LogInformation("Upload Request → file: {Name}, size: {Size}, contentType: {CT}, bucket: {Bucket}, folder: {Folder}",
                file.FileName, file.Length, file.ContentType, bucketName, clientFolder);

            try
            {
                var key = await _s3.UploadAsync(file, bucketName, clientFolder, fileName);
                if (key == null)
                {
                    _logger.LogError("S3 upload returned null → check S3PrescriptionService logs");
                    return BadRequest("Upload to storage failed.");
                }

                var model = new PrescriptionModel
                {
                    filename = Path.GetFileName(key),
                    bucketname = bucketName,
                    filepath = $"{bucketName}/{key}",
                    uploaded_date = DateTime.UtcNow
                };

                _logger.LogInformation("Inserting to DB → {FilePath}", model.filepath);

                var newId = await _prescriptionClass.InsertAsync(model);

                _logger.LogInformation("DB insert success → ID: {Id}", newId);

                return Ok(new
                {
                    Message = "Uploaded successfully.",
                    Id = newId,
                    model.filename,
                    model.bucketname,
                    model.filepath,
                    model.uploaded_date
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prescription upload exception");
                return StatusCode(500, ex.Message);
            }
        }

        // GET: api/prescription/get
        [HttpGet("get")]
        public async Task<IActionResult> GetAll()
        {
            var records = await _prescriptionClass.GetAllAsync();
            return Ok(records);
        }

        // GET: api/prescription/get/{id}
        [HttpGet("get/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var record = await _prescriptionClass.GetByIdAsync(id);
            if (record == null)
                return NotFound("Prescription not found.");

            return Ok(record);
        }

        // GET: api/prescription/download/{id}
        [HttpGet("download")]
        public async Task<IActionResult> Download([FromQuery]int id)
        {
            var record = await _prescriptionClass.GetByIdAsync(id);
            if (record == null)
                return NotFound("Prescription not found.");

            var key = S3PrescriptionService.ExtractKey(record.filepath, record.bucketname);
            var result = await _s3.DownloadAsync(record.bucketname, key);
            if (result == null)
                return NotFound("File not found.");

            return Ok(result.Value.Data);
        }

        // DELETE: api/prescription/delete/{id}
        [HttpGet("delete")]
        public async Task<IActionResult> Delete([FromQuery]int id)
        {
            var record = await _prescriptionClass.GetByIdAsync(id);
            if (record == null)
                return NotFound("Prescription not found.");

            var key = S3PrescriptionService.ExtractKey(record.filepath, record.bucketname);
            await _s3.DeleteAsync(record.bucketname, key);

            var deleted = await _prescriptionClass.DeleteAsync(id);
            if (!deleted)
                return BadRequest("DB deletion failed.");

            return Ok(new { Message = "File deleted successfully." });
        }

        // GET: api/prescription/list?bucketName=cms&clientFolder=prescriptions/john
        [HttpGet("list")]
        public async Task<IActionResult> List(
            [FromQuery] string bucketName,
            [FromQuery] string clientFolder)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                return BadRequest("bucketName is required.");

            if (string.IsNullOrWhiteSpace(clientFolder))
                return BadRequest("clientFolder is required.");

            var files = await _s3.ListAsync(bucketName, clientFolder);
            return Ok(files);
        }
    }
}
