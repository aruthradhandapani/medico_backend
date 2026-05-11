using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountryMasterController : ControllerBase
    {
        private readonly CountryMasterClass cls;

        public CountryMasterController(CountryMasterClass _cls)
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
        // GET BY COUNTRYCODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-countrycode")]
        public async Task<IActionResult> GetByCountryCode(int countrycode)
        {
            var data = await cls.GetByCountryCode(countrycode);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // SEARCH BY COUNTRY NAME
        // ─────────────────────────────────────────
        [HttpGet("search-by-countryname")]
        public async Task<IActionResult> SearchByCountryName(string countryname)
        {
            var data = await cls.SearchByCountryName(countryname);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET NEXT COUNTRYCODE
        // ─────────────────────────────────────────
        [HttpGet("get-next-countrycode")]
        public async Task<IActionResult> GetNextCountryCode()
        {
            var data = await cls.GetNextCountryCode();
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CountryMasterModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CountryMasterModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int countrycode)
        {
            var result = await cls.Delete(countrycode);
            return Ok(result);
        }
    }
}