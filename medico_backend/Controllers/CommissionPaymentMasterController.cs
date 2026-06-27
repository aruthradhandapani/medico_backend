using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommissionPaymentMasterController : ControllerBase
    {
        private readonly CommissionPaymentMasterClass cls;

        public CommissionPaymentMasterController(CommissionPaymentMasterClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();
            return Ok(data);
        }

        [HttpGet("get-by-guid")]
        public async Task<IActionResult> GetByGuid(string commissionpaymentguid)
        {
            var data = await cls.GetByGuid(commissionpaymentguid);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("get-by-barcode")]
        public async Task<IActionResult> GetByBarcode(string commissionpaymentbarcode)
        {
            var data = await cls.GetByBarcode(commissionpaymentbarcode);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("get-by-daterange")]
        public async Task<IActionResult> GetByDateRange(DateTime fromdate, DateTime todate)
        {
            var data = await cls.GetByDateRange(fromdate, todate);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CommissionPaymentMasterModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CommissionPaymentMasterModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(string commissionpaymentguid)
        {
            var result = await cls.Delete(commissionpaymentguid);
            return Ok(result);
        }
    }
}