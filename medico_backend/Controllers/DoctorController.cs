using medico_backend.Services;// or wherever S3ImageService lives
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorMasterController : ControllerBase
    {
        private readonly DoctorMasterClass _cls;
        private readonly S3ImageService _s3;

        private const string EntityType = "doctors";

        public DoctorMasterController(DoctorMasterClass cls, S3ImageService s3)
        {
            _cls = cls;
            _s3 = s3;
        }

        // ─── Get All ──────────────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.Get(tenant));
        }

        // ─── Get by Dcode ─────────────────────────────────────────────
        [HttpGet("get-by-dcode")]
        public async Task<IActionResult> GetByDcode(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByDcode(dcode, tenant));
        }

        // ─── Get Consultants ──────────────────────────────────────────
        [HttpGet("get-consultants")]
        public async Task<IActionResult> GetConsultants()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetConsultants(tenant));
        }

        // ─── Get Referrals ────────────────────────────────────────────
        [HttpGet("get-referrals")]
        public async Task<IActionResult> GetReferrals()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetReferrals(tenant));
        }

        // ─── Get Next Dcode ───────────────────────────────────────────
        [HttpGet("get-next-dcode")]
        public async Task<IActionResult> GetNextDcode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetNextDcode(tenant));
        }

        // ─── Insert (with image) ──────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
    [FromForm] DoctorMasterModel data,
    [FromForm] IFormFile? doctorImageFile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            // Only update if there is actually an image
            if (doctorImageFile != null && doctorImageFile.Length > 0)
            {
                data.doctorimage = await _s3.UploadAsync(
                    doctorImageFile, tenant, EntityType, data.dcode, "doctor");

                await _cls.Update(data);  // only called when image exists
            }

            return Ok(new { message = "Success", dcode = data.dcode });
        }

        // ─── Update (with image replace) ─────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromForm] DoctorMasterModel data,
            [FromForm] IFormFile? doctorImageFile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            // Step 1: Get existing record to retrieve old image key
            var existing = await _cls.GetByDcode(data.dcode, tenant);

            // Step 2: Replace or preserve image
            data.doctorimage = await _s3.ReplaceAsync(
                doctorImageFile,
                existing?.doctorimage,
                tenant, EntityType, data.dcode, "doctor");

            // Step 3: Update DB
            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            // Step 1: Get image key before deleting
            var existing = await _cls.GetByDcode(dcode, tenant);

            // Step 2: Soft delete in DB
            var res = await _cls.Delete(dcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            // Step 3: Delete image from S3
            await _s3.DeleteAsync(existing?.doctorimage);

            return Ok(new { message = "Doctor deleted successfully" });
        }
    }
}