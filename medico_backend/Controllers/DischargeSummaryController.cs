using System;
using System.Threading.Tasks;
using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DischargeSummaryController : ControllerBase
    {
        private readonly DischargeSummaryClass _dsClass;

        public DischargeSummaryController(DischargeSummaryClass dsClass)
        {
            _dsClass = dsClass;
        }

        private string T => Request.Headers["tenant_code"].ToString();
        private string UserCode => User.FindFirst("user_code")?.Value ?? "system";

        // MASTER CATEGORIES
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var list = await _dsClass.GetCategoriesAsync(T);
            return Ok(new { success = true, data = list });
        }

        [HttpPost("category")]
        public async Task<IActionResult> SaveCategory([FromBody] DischargeSummaryModel.SaveCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var id = await _dsClass.SaveCategoryAsync(dto, T);
            return Ok(new { success = true, category_id = id });
        }

        [HttpDelete("category/{id}")]
        public async Task<IActionResult> DeleteCategory([FromRoute] Guid id)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _dsClass.DeleteCategoryAsync(id, T);
            return Ok(new { success = res });
        }

        // MASTER TEMPLATES
        [HttpGet("templates")]
        public async Task<IActionResult> GetTemplates([FromQuery] Guid? category_id)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var list = await _dsClass.GetTemplatesAsync(category_id, T);
            return Ok(new { success = true, data = list });
        }

        [HttpPost("template")]
        public async Task<IActionResult> SaveTemplate([FromBody] DischargeSummaryModel.SaveTemplateDto dto)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var id = await _dsClass.SaveTemplateAsync(dto, T, UserCode);
            return Ok(new { success = true, template_id = id });
        }

        [HttpDelete("template/{id}")]
        public async Task<IActionResult> DeleteTemplate([FromRoute] Guid id)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _dsClass.DeleteTemplateAsync(id, T);
            return Ok(new { success = res });
        }

        // PATIENT DISCHARGE SUMMARY ENTRIES
        [HttpGet("patient")]
        [HttpGet("patient/{*patcode_or_pdsid}")]
        public async Task<IActionResult> GetPatientDischargeSummary([FromRoute] string? patcode_or_pdsid, [FromQuery] string? code)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var targetCode = !string.IsNullOrWhiteSpace(patcode_or_pdsid) ? patcode_or_pdsid : code;
            if (string.IsNullOrWhiteSpace(targetCode))
                return BadRequest(new { success = false, message = "Patient code or ID required" });

            targetCode = System.Net.WebUtility.UrlDecode(targetCode);

            var res = await _dsClass.GetPatientDischargeSummaryAsync(targetCode, T);
            if (res == null)
            {
                res = new DischargeSummaryModel.PatientDischargeSummaryResponse
                {
                    Master = new DischargeSummaryModel.pds_master
                    {
                        pds_id = Guid.NewGuid(),
                        patcode = targetCode,
                        tenant_code = T
                    },
                    Details = new System.Collections.Generic.List<DischargeSummaryModel.pds_detail>()
                };
            }

            return Ok(new { success = true, data = res });
        }

        [HttpPost("patient/save")]
        public async Task<IActionResult> SavePatientDischargeSummary([FromBody] DischargeSummaryModel.SavePatientDischargeSummaryDto dto)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var pdsId = await _dsClass.SavePatientDischargeSummaryAsync(dto, T, UserCode);
            return Ok(new { success = true, pds_id = pdsId });
        }

        [HttpPost("patient/authorize")]
        public async Task<IActionResult> AuthorizePatientDischargeSummary([FromBody] DischargeSummaryModel.AuthorizeDischargeSummaryDto dto)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _dsClass.AuthorizePatientDischargeSummaryAsync(dto, T);
            return Ok(new { success = res });
        }
    }
}
