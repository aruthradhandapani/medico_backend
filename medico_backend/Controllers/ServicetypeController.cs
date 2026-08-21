using medico_backend.Class;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceTypeController : ControllerBase
    {
        private readonly ServiceTypeClass _cls;

        public ServiceTypeController(ServiceTypeClass cls)
        {
            _cls = cls;
        }

        private string GetTenantCode() => Request.Headers["tenant_code"].ToString();

        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var tenant = GetTenantCode();
                if (string.IsNullOrWhiteSpace(tenant))
                    return BadRequest(new { message = "tenant_code header is required" });

                var data = await _cls.GetAll(tenant);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error", detail = ex.Message });
            }
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetById([FromQuery] int service_id)
        {
            try
            {
                var tenant = GetTenantCode();
                if (string.IsNullOrWhiteSpace(tenant))
                    return BadRequest(new { message = "tenant_code header is required" });

                if (service_id <= 0)
                    return BadRequest(new { message = "service_id is required" });

                var data = await _cls.GetById(service_id, tenant);
                if (data == null)
                    return NotFound(new { message = $"Service id {service_id} not found" });

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error", detail = ex.Message });
            }
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] ServiceTypeModel model)
        {
            try
            {
                var tenant = GetTenantCode();
                if (string.IsNullOrWhiteSpace(tenant))
                    return BadRequest(new { message = "tenant_code header is required" });

                var (success, service_id, error) = await _cls.Insert(model, tenant);

                if (!success)
                    return BadRequest(new { message = "Insert failed", detail = error });

                return Ok(new { message = "Inserted successfully", service_id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error", detail = ex.Message });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] ServiceTypeModel model)
        {
            try
            {
                var tenant = GetTenantCode();
                if (string.IsNullOrWhiteSpace(tenant))
                    return BadRequest(new { message = "tenant_code header is required" });

                var (success, error) = await _cls.Update(model, tenant);

                if (!success)
                {
                    if (error != null && error.Contains("not found"))
                        return NotFound(new { message = "Update failed", detail = error });

                    return BadRequest(new { message = "Update failed", detail = error });
                }

                return Ok(new { message = "Updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error", detail = ex.Message });
            }
        }

        [HttpGet("softdelete")]
        public async Task<IActionResult> SoftDelete([FromQuery] int service_id)
        {
            try
            {
                var tenant = GetTenantCode();
                if (string.IsNullOrWhiteSpace(tenant))
                    return BadRequest(new { message = "tenant_code header is required" });

                var (success, error) = await _cls.SoftDelete(service_id, tenant);

                if (!success)
                {
                    if (error != null && error.Contains("not found"))
                        return NotFound(new { message = "Soft delete failed", detail = error });

                    return BadRequest(new { message = "Soft delete failed", detail = error });
                }

                return Ok(new { message = "Soft deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error", detail = ex.Message });
            }
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete([FromQuery] int service_id)
        {
            try
            {
                var tenant = GetTenantCode();
                if (string.IsNullOrWhiteSpace(tenant))
                    return BadRequest(new { message = "tenant_code header is required" });

                var (success, error) = await _cls.Delete(service_id, tenant);

                if (!success)
                {
                    if (error != null && error.Contains("not found"))
                        return NotFound(new { message = "Delete failed", detail = error });

                    return BadRequest(new { message = "Delete failed", detail = error });
                }

                return Ok(new { message = "Deleted permanently" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error", detail = ex.Message });
            }
        }
    }
}