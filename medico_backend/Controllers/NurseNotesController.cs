using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NurseNotesController : ControllerBase
    {
        private readonly NurseNotesClass _nurseNotesCls;

        public NurseNotesController(NurseNotesClass nurseNotesCls)
        {
            _nurseNotesCls = nurseNotesCls;
        }

        // Resolve tenant_code the same way the rest of the app does.
        // Replace this with your actual tenant resolution (header / claim / middleware).
        private string GetTenantCode() =>
            Request.Headers["tenant_code"].FirstOrDefault() ?? string.Empty;

        // ─────────────────────────────────────────
        // POST api/NurseNotes/add
        // ─────────────────────────────────────────
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] AddNurseNoteRequest req)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.Add(req, tenant_code);

            if (result.StartsWith("Success"))
                return Ok(new { message = result });

            return BadRequest(new { message = result });
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/by-ip/{ip_id}?note_type=DRESSING
        // ─────────────────────────────────────────
        [HttpGet("by-ip/{ip_id}")]
        public async Task<IActionResult> GetByIpId(Guid ip_id, [FromQuery] string? note_type = null)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.GetByIpId(ip_id, tenant_code, note_type);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/{note_id}
        // ─────────────────────────────────────────
        [HttpGet("{note_id}")]
        public async Task<IActionResult> GetById(Guid note_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.GetById(note_id, tenant_code);
            if (result == null) return NotFound(new { message = "Note not found" });

            return Ok(result);
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/medication-history/{ip_id}
        // Joined view: administration log + the actual ordered drug details
        // ─────────────────────────────────────────
        [HttpGet("medication-history/{ip_id}")]
        public async Task<IActionResult> GetMedicationHistory(Guid ip_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.GetMedicationHistory(ip_id, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/dressing-history/{ip_id}
        // ─────────────────────────────────────────
        [HttpGet("dressing-history/{ip_id}")]
        public async Task<IActionResult> GetDressingHistory(Guid ip_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.GetDressingHistory(ip_id, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/handover-history/{ip_id}
        // ─────────────────────────────────────────
        [HttpGet("handover-history/{ip_id}")]
        public async Task<IActionResult> GetHandoverHistory(Guid ip_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.GetHandoverHistory(ip_id, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/full-chart/{ip_id}
        // Combined view: nurse_notes + patient_vitals + symptoms + prescriptions + investigations
        // ─────────────────────────────────────────
        [HttpGet("full-chart/{ip_id}")]
        public async Task<IActionResult> GetFullIpChart(Guid ip_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.GetFullIpChart(ip_id, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // POST api/NurseNotes/update
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdateNurseNoteRequest req)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.Update(req, tenant_code);

            if (result == "Success")
                return Ok(new { message = result });

            return BadRequest(new { message = result });
        }

        // ─────────────────────────────────────────
        // PUT api/NurseNotes/verify
        // ─────────────────────────────────────────
        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyNurseNoteRequest req)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.Verify(req, tenant_code);

            if (result == "Success")
                return Ok(new { message = result });

            return BadRequest(new { message = result });
        }

        // ─────────────────────────────────────────
        // PUT api/NurseNotes/cancel
        // ─────────────────────────────────────────
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelNurseNoteRequest req)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.Cancel(req, tenant_code);

            if (result == "Success")
                return Ok(new { message = result });

            return BadRequest(new { message = result });
        }

        // ─────────────────────────────────────────
        // GET api/NurseNotes/delete/{note_id}
        // (soft delete — kept as GET per project convention; sets isdeleted = true)
        // ─────────────────────────────────────────
        [HttpGet("delete/{note_id}")]
        public async Task<IActionResult> Delete(Guid note_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await _nurseNotesCls.Delete(note_id, tenant_code);

            if (result == "Success")
                return Ok(new { message = result });

            return NotFound(new { message = result });
        }
    }
}