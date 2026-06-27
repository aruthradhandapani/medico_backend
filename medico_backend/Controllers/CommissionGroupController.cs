using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommissionGroupMasterController : ControllerBase
    {
        private readonly CommissionGroupMasterClass cls;

        public CommissionGroupMasterController(CommissionGroupMasterClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();
            return Ok(data);
        }

        [HttpGet("get-by-cgcode")]
        public async Task<IActionResult> GetByCgCode(decimal cgcode)
        {
            var data = await cls.GetByCgCode(cgcode);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("search-by-name")]
        public async Task<IActionResult> SearchByName(string name)
        {
            var data = await cls.SearchByName(name);
            return Ok(data);
        }

        [HttpGet("get-next-cgcode")]
        public async Task<IActionResult> GetNextCgCode()
        {
            var data = await cls.GetNextCgCode();
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CommissionGroupMasterModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CommissionGroupMasterModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal cgcode)
        {
            var result = await cls.Delete(cgcode);
            return Ok(result);
        }
    }
}