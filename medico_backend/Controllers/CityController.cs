using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CityMasterController : ControllerBase
    {
        private readonly CityMasterClass cls;

        public CityMasterController(CityMasterClass _cls)
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
        // GET BY CITYCODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-citycode")]
        public async Task<IActionResult> GetByCityCode(int citycode)
        {
            var data = await cls.GetByCityCode(citycode);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // SEARCH BY CITY NAME
        // ─────────────────────────────────────────
        [HttpGet("search-by-cityname")]
        public async Task<IActionResult> SearchByCityName(string cityname)
        {
            var data = await cls.SearchByCityName(cityname);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET NEXT CITYCODE
        // ─────────────────────────────────────────
        [HttpGet("get-next-citycode")]
        public async Task<IActionResult> GetNextCityCode()
        {
            var data = await cls.GetNextCityCode();
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CityMasterModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CityMasterModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int citycode)
        {
            var result = await cls.Delete(citycode);
            return Ok(result);
        }
    }
}