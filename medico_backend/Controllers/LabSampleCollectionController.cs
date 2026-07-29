using medico_backend.Model;
using medico_backend.Class;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace medico_backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LabSampleCollectionController : ControllerBase
    {
        private readonly LabSampleCollectionClass _cls;

        public LabSampleCollectionController(LabSampleCollectionClass cls) => _cls = cls;

        private string? TenantCode => Request.Headers["tenant_code"].FirstOrDefault();

        [HttpGet("loadsamplecollection")]
        public async Task<IActionResult> LoadSampleCollection(
            [FromQuery] DateTime? fromdate,
            [FromQuery] DateTime? todate,
            [FromQuery] string? status)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            var (data, error) = await _cls.Load_SampleCollection(
                TenantCode,
                fromdate ?? DateTime.Today,
                todate ?? DateTime.Today,
                status);

            if (error != null) return StatusCode(500, error);
            return Ok(data);
        }

        [HttpGet("loadsamplereceived")]
        public async Task<IActionResult> LoadSampleReceived(
            [FromQuery] DateTime? fromdate,
            [FromQuery] DateTime? todate)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            var (data, error) = await _cls.Load_SampleReceived(
                TenantCode,
                fromdate ?? DateTime.Today,
                todate ?? DateTime.Today);

            if (error != null) return StatusCode(500, error);
            return Ok(data);
        }

        [HttpPost("savesamplecollection")]
        public async Task<IActionResult> SaveSampleCollection(
            [FromBody] SaveSampleCollectionRequest request)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            var data = request.collection;

            if (data.requestguid == Guid.Empty || data.requestguid is null)
                return BadRequest("requestguid is required.");

            if (data.scode is null or 0)
                return BadRequest("scode is required.");

            if (data.isreject == true && string.IsNullOrWhiteSpace(request.rejectreason))
                return BadRequest("rejectreason is required when isreject is true.");

            if (data.is_resampling == true && string.IsNullOrWhiteSpace(request.resamplingreason))
                return BadRequest("resamplingreason is required when is_resampling is true.");

            if (data.isreject == true && data.isaccept == true)
                return BadRequest("Specimen cannot be both accepted and rejected.");

            if (data.is_resampling == true && data.isaccept == true)
                return BadRequest("Specimen cannot be both accepted and pending resampling.");

            data.tenant_code = TenantCode;

            var (result, lrspid) = await _cls.Save_SampleCollection(
                data, request.rejectreason, request.resamplingreason);

            if (result == "No record found")
                return NotFound("Specimen record not found or already deleted.");

            if (result != "Success")
                return StatusCode(500, result);

            return Ok(new { message = "Sample collection saved successfully.", lrspid });
        }

        [HttpPost("savesamplereceived")]
        public async Task<IActionResult> SaveSampleReceived(
            [FromBody] SaveSampleReceiveRequest request)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            if (request.lrsprid == Guid.Empty)
                return BadRequest("lrsprid is required.");

            request.tenant_code = TenantCode;

            var result = await _cls.Save_SampleReceive(request);

            return result switch
            {
                "Success" => Ok("Sample received successfully."),
                "No record found" => NotFound("Receive record not found."),
                "Already received" => BadRequest("Specimen already received."),
                _ => StatusCode(500, result)
            };
        }

        [HttpGet("loadsampletransfer")]
        public async Task<IActionResult> LoadSampleTransfer(
    [FromQuery] DateTime? fromdate,
    [FromQuery] DateTime? todate,
    [FromQuery] Guid? requestguid,
    [FromQuery] int? gcode,
    [FromQuery] string? direction,
    [FromQuery] string? status)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            if (!string.IsNullOrWhiteSpace(direction)
                && direction.ToLower().Trim() is not ("incoming" or "outgoing"))
                return BadRequest("direction must be 'incoming' or 'outgoing'.");

            var (data, error) = await _cls.Load_SampleTransfer(
                TenantCode,
                fromdate ?? DateTime.Today,
                todate ?? DateTime.Today,
                requestguid,
                gcode,
                direction,
                status);

            if (error != null) return StatusCode(500, error);
            return Ok(data);
        }

        // ══════════════════════════════════════════════════════════════
        // NEW: POST sampletransfer
        //
        //   Body.action = TRANSFER (default) → creates a new transfer:
        //     { "lrspid": "...", "from_gcode": 1, "to_gcode": 2, "usercode": 7 }
        //
        //   Body.action = RECEIVE / COMPLETE / RETURN / CANCEL → advances
        //   an existing transfer:
        //     { "lrsptid": "...", "action": "RECEIVE", "usercode": 7 }
        // ══════════════════════════════════════════════════════════════
        [HttpPost("sampletransfer")]
        public async Task<IActionResult> SampleTransfer([FromBody] SaveSampleTransferRequest request)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            if (string.IsNullOrWhiteSpace(request.action))
                request.action = SampleTransferAction.Transfer;

            var action = request.action.ToUpper().Trim();
            bool isNewTransfer = action == SampleTransferAction.Transfer
                                  && (request.lrsptid is null || request.lrsptid == Guid.Empty);

            if (isNewTransfer)
            {
                if (request.lrspid is null || request.lrspid == Guid.Empty)
                    return BadRequest("lrspid is required.");

                if (request.from_gcode is null || request.from_gcode == 0)
                    return BadRequest("from_gcode is required.");

                if (request.to_gcode is null || request.to_gcode == 0)
                    return BadRequest("to_gcode is required.");

                if (request.from_gcode == request.to_gcode)
                    return BadRequest("from_gcode and to_gcode cannot be the same.");
            }
            else if (request.lrsptid is null || request.lrsptid == Guid.Empty)
            {
                return BadRequest("lrsptid is required for this action.");
            }

            request.tenant_code = TenantCode;

            var (result, lrsptid) = await _cls.Save_SampleTransfer(request);

            if (result == "Success")
                return Ok(new { message = "Sample transfer saved successfully.", lrsptid });

            if (result == "No record found")
                return NotFound("Transfer record not found.");

            if (result.StartsWith("Specimen record not found"))
                return NotFound(result);

            // Validation / state-machine violations (missing fields, wrong-order
            // transitions, duplicate actions, unknown action) → 400
            return BadRequest(result);
        }

        // ══════════════════════════════════════════════════════════════
        // NEW: GET loadpatientstatus?requestguid=...
        //
        //   Consolidated per-request review: for every test — collection
        //   status, every receive row (by gcode), every transfer row
        //   (from/to gcode with full state), and result entry /
        //   authorize1 / authorize2 status with the acting usercode.
        // ══════════════════════════════════════════════════════════════
        [HttpGet("loadpatientstatus")]
        public async Task<IActionResult> LoadPatientStatus([FromQuery] Guid requestguid)
        {
            if (string.IsNullOrEmpty(TenantCode))
                return BadRequest("Tenant code is required.");

            if (requestguid == Guid.Empty)
                return BadRequest("requestguid is required.");

            var (data, error) = await _cls.Load_PatientStatus(TenantCode, requestguid);

            if (error == "Request not found")
                return NotFound(error);

            if (error != null)
                return StatusCode(500, error);

            return Ok(data);
        }
    }
}