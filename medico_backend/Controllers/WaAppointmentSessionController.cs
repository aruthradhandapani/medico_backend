using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WaAppointmentSessionController : ControllerBase
    {
        private readonly WaAppointmentSessionClass cls;

        public WaAppointmentSessionController(WaAppointmentSessionClass _cls)
        {
            cls = _cls;
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET BY PHONE NUMBER
        // ─────────────────────────────────────────
        [HttpGet("get-by-phone")]
        public async Task<IActionResult> GetByPhoneNumber(string phonenumber)
        {
            if (string.IsNullOrWhiteSpace(phonenumber))
            {
                return BadRequest("phonenumber is required");
            }

            var data = await cls.GetByPhoneNumber(phonenumber);

            if (data == null)
            {
                return NotFound("No active session found for this phone number");
            }

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] WaAppointmentSessionModel data)
        {
            if (string.IsNullOrWhiteSpace(data.phonenumber))
            {
                return BadRequest("phonenumber is required");
            }

            var result = await cls.Insert(data);
            return Ok(result);
        }
    }
}