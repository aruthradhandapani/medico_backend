using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AreaMasterController : ControllerBase
    {
        private readonly AreaMasterClass cls;

        public AreaMasterController(AreaMasterClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();
            return Ok(data);
        }

        [HttpGet("get-by-areacode")]
        public async Task<IActionResult> GetByAreaCode(int areacode)
        {
            var data = await cls.GetByAreaCode(areacode);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("search-by-areaname")]
        public async Task<IActionResult> SearchByAreaName(string areaname)
        {
            var data = await cls.SearchByAreaName(areaname);
            return Ok(data);
        }

        [HttpGet("get-next-areacode")]
        public async Task<IActionResult> GetNextAreaCode()
        {
            var data = await cls.GetNextAreaCode();
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] AreaMasterModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] AreaMasterModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int areacode)
        {
            var result = await cls.Delete(areacode);
            return Ok(result);
        }
    }
}