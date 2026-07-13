using System.Threading.Tasks;
using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class WaAppointmentSessionController : ControllerBase
    {
        private readonly WaAppointmentSessionClass _sessionService;

        public WaAppointmentSessionController(WaAppointmentSessionClass sessionService)
        {
            _sessionService = sessionService;
        }

        [HttpGet("active")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WaAppointmentSession))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActiveSession([FromQuery] string phonenumber)
        {
            if (string.IsNullOrWhiteSpace(phonenumber))
                return BadRequest(new { message = "phonenumber is required." });

            var session = await _sessionService.GetActiveSessionByPhone(phonenumber);
            if (session == null)
                return NotFound(new { message = "No active session found for this phone number." });

            return Ok(session);
        }

        [HttpGet("{sessionid}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WaAppointmentSession))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int sessionid)
        {
            var session = await _sessionService.GetById(sessionid);
            if (session == null)
                return NotFound(new { message = "Session not found." });

            return Ok(session);
        }

        [HttpPost("create")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WaAppointmentSession))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSession([FromBody] CreateWaSessionRequest req)
        {
            var (status, data) = await _sessionService.CreateSession(req);
            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(data);
        }

        [HttpPost("update")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WaAppointmentSession))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSession([FromBody] UpdateWaSessionRequest req)
        {
            var (status, data) = await _sessionService.UpdateSession(req);

            if (status == "Session not found.")
                return NotFound(new { message = status });
            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(data);
        }

        [HttpPost("close")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CloseSession([FromBody] CloseWaSessionRequest req)
        {
            var status = await _sessionService.CloseSession(req.sessionid);

            if (status == "Session not found.")
                return NotFound(new { message = status });
            if (status != "SUCCESS")
                return BadRequest(new { message = status });

            return Ok(new { message = "Session closed successfully." });
        }

    }
}