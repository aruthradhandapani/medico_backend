using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StateMasterController : ControllerBase
    {
        private readonly StateMasterClass cls;

        public StateMasterController(StateMasterClass _cls)
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
        // GET BY STATECODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-statecode")]
        public async Task<IActionResult> GetByStateCode(int statecode)
        {
            var data = await cls.GetByStateCode(statecode);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // SEARCH BY STATE NAME
        // ─────────────────────────────────────────
        [HttpGet("search-by-statename")]
        public async Task<IActionResult> SearchByStateName(string statename)
        {
            var data = await cls.SearchByStateName(statename);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET NEXT STATECODE
        // ─────────────────────────────────────────
        [HttpGet("get-next-statecode")]
        public async Task<IActionResult> GetNextStateCode()
        {
            var data = await cls.GetNextStateCode();
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] StateMasterModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] StateMasterModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int statecode)
        {
            var result = await cls.Delete(statecode);
            return Ok(result);
        }
    }
}