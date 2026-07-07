using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;
using medico_backend.Services;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorProfileController : ControllerBase
    {
        private readonly DoctorProfileClass _cls;
        private readonly S3ImageService _s3;

        private const string EntityType = "doctor-profile";

        public DoctorProfileController(DoctorProfileClass cls, S3ImageService s3)
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

        // ─── Get by Pcode ─────────────────────────────────────────────
        [HttpGet("get-by-pcode")]
        public async Task<IActionResult> GetByPcode(int pcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByPcode(pcode, tenant));
        }

        // ─── Get by Dcode ─────────────────────────────────────────────
        [HttpGet("get-by-dcode")]
        public async Task<IActionResult> GetByDcode(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByDcode(dcode, tenant));
        }

        // ─── Get Published Profiles ──────────────────────────────────
        [HttpGet("get-published")]
        public async Task<IActionResult> GetPublished()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetPublished(tenant));
        }

        // ─── Get Full Profile (doctor_master + doctor_profile) ───────
        [HttpGet("get-full-profile")]
        public async Task<IActionResult> GetFullProfile(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await _cls.GetFullProfile(dcode, tenant);
            if (res == null) return NotFound(new { message = "Doctor not found" });
            return Ok(res);
        }

        // ─── Insert (with banner image) ───────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromForm] DoctorProfileModel data,
            [FromForm] IFormFile? bannerImageFile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            if (bannerImageFile != null && bannerImageFile.Length > 0)
            {
                data.banner_image = await _s3.UploadAsync(
                    bannerImageFile, tenant, EntityType, data.dcode, "banner");
            }

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", pcode = data.pcode });
        }

        // ─── Update (with banner image replace) ──────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromForm] DoctorProfileModel data,
            [FromForm] IFormFile? bannerImageFile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var existing = await _cls.GetByPcode(data.pcode, tenant);

            data.banner_image = await _s3.ReplaceAsync(
                bannerImageFile,
                existing?.banner_image,
                tenant, EntityType, data.dcode, "banner");

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int pcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var existing = await _cls.GetByPcode(pcode, tenant);

            var res = await _cls.Delete(pcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            await _s3.DeleteAsync(existing?.banner_image);

            return Ok(new { message = "Doctor profile deleted successfully" });
        }
    }
}