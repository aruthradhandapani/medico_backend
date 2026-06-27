using medico_backend.Class;
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommissionPaymentDetailsController : ControllerBase
    {
        private readonly CommissionPaymentDetailsClass cls;

        public CommissionPaymentDetailsController(CommissionPaymentDetailsClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();
            return Ok(data);
        }

        [HttpGet("get-by-cpdid")]
        public async Task<IActionResult> GetByCpdId(string cpdid)
        {
            var data = await cls.GetByCpdId(cpdid);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("get-by-commissionpaymentguid")]
        public async Task<IActionResult> GetByCommissionPaymentGuid(string commissionpaymentguid)
        {
            var data = await cls.GetByCommissionPaymentGuid(commissionpaymentguid);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CommissionPaymentDetailsModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CommissionPaymentDetailsModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(string cpdid)
        {
            var result = await cls.Delete(cpdid);
            return Ok(result);
        }
    }
}