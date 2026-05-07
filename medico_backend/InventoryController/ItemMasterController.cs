using medico_backend.InventoryClass;
using medico_backend.InventoryModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.InventoryController
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class ItemMasterController : ControllerBase
    {
        private readonly ItemMasterClass itemclass;

        public ItemMasterController(ItemMasterClass _imc)
        {
            itemclass = _imc;
        }

       
        private string GetTenantCode() =>
            Request.Headers["tenantcode"].FirstOrDefault() ?? string.Empty;

       
        private IActionResult MissingTenantCode() =>
            BadRequest(new { Status = "Failed", Message = "Header 'tenantcode' is required." });

        // ─── ITEM MASTER ─────────────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/insertitem</summary>
        [HttpPost("insertitem")]
        public async Task<IActionResult> InsertItem([FromBody] item_master item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.tenantcode))
                    item.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(item.tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.InsertItem(item);

                return Ok(new
                {
                    Status = "Success",
                    Message = item.itemcode == 0 ? "Item inserted successfully" : "Item updated successfully",
                    ItemCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>POST /api/ItemMaster/updateitem</summary>
        [HttpPost("updateitem")]
        public async Task<IActionResult> UpdateItem([FromBody] item_master item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.tenantcode))
                    item.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(item.tenantcode))
                    return MissingTenantCode();

                var res = await itemclass.UpdateItem(item);

                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Item updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update item" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/deleteitem?itemcode=1</summary>
        [HttpGet("deleteitem")]
        public async Task<IActionResult> DeleteItem(long itemcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var res = await itemclass.DeleteItem(itemcode, tenantcode);

                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Item deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to delete item" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getallitems  — tenantcode from header</summary>
        [HttpGet("getallitems")]
        public async Task<IActionResult> GetAllItems()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllItems(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getitembycode?itemcode=1</summary>
        [HttpGet("getitembycode")]
        public async Task<IActionResult> GetItemByCode(long itemcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetItemByCode(itemcode, tenantcode);

                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Item not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── VENDOR MASTER ───────────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/upsertvendor</summary>
        [HttpPost("upsertvendor")]
        public async Task<IActionResult> UpsertVendor([FromBody] vendor_master vendor)
        {
            try
            {
                if (string.IsNullOrEmpty(vendor.tenantcode))
                    vendor.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(vendor.tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.UpsertVendor(vendor);

                return Ok(new
                {
                    Status = "Success",
                    Message = vendor.vendorcode == 0 ? "Vendor inserted successfully" : "Vendor updated successfully",
                    VendorCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>POST /api/ItemMaster/updatevendor</summary>
        [HttpPost("updatevendor")]
        public async Task<IActionResult> UpdateVendor([FromBody] vendor_master vendor)
        {
            try
            {
                if (string.IsNullOrEmpty(vendor.tenantcode))
                    vendor.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(vendor.tenantcode))
                    return MissingTenantCode();

                var res = await itemclass.UpdateVendor(vendor);

                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Vendor updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update vendor" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/deletevendor?vendorcode=1</summary>
        [HttpGet("deletevendor")]
        public async Task<IActionResult> DeleteVendor(long vendorcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var res = await itemclass.DeleteVendor(vendorcode, tenantcode);

                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Vendor deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to delete vendor" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getallvendors  — tenantcode from header</summary>
        [HttpGet("getallvendors")]
        public async Task<IActionResult> GetAllVendors()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllVendors(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getvendorbycode?vendorcode=1</summary>
        [HttpGet("getvendorbycode")]
        public async Task<IActionResult> GetVendorByCode(long vendorcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetVendorByCode(vendorcode, tenantcode);

                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Vendor not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── PURCHASE MASTER ─────────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/insertpurchase</summary>
        [HttpPost("insertpurchase")]
        public async Task<IActionResult> InsertPurchase([FromBody] purchase_request request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.master.tenantcode))
                    request.master.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    return MissingTenantCode();

                foreach (var d in request.details)
                    if (string.IsNullOrEmpty(d.tenantcode))
                        d.tenantcode = request.master.tenantcode;

                var result = await itemclass.InsertPurchase(request);

                return Ok(new { Status = "Success", Message = "Purchase inserted successfully", PurchaseCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>POST /api/ItemMaster/updatepurchase</summary>
        [HttpPost("updatepurchase")]
        public async Task<IActionResult> UpdatePurchase([FromBody] purchase_request request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.master.tenantcode))
                    request.master.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    return MissingTenantCode();

                foreach (var d in request.details)
                    if (string.IsNullOrEmpty(d.tenantcode))
                        d.tenantcode = request.master.tenantcode;

                var result = await itemclass.UpdatePurchase(request);

                return Ok(new { Status = "Success", Message = "Purchase updated successfully", PurchaseCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/deletepurchase?purchasecode=1</summary>
        [HttpGet("deletepurchase")]
        public async Task<IActionResult> DeletePurchase(long purchasecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeletePurchase(purchasecode, tenantcode);

                return result == "Success"
                    ? Ok(new { Status = "Success", Message = "Purchase deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Purchase not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getallpurchases  — tenantcode from header</summary>
        [HttpGet("getallpurchases")]
        public async Task<IActionResult> GetAllPurchases()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllPurchases(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getpurchasebycode?purchasecode=1</summary>
        [HttpGet("getpurchasebycode")]
        public async Task<IActionResult> GetPurchaseByCode(long purchasecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetPurchaseByCode(purchasecode, tenantcode);

                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Purchase not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── STOCK MASTER ────────────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/insertstock</summary>
        [HttpPost("insertstock")]
        public async Task<IActionResult> InsertStock([FromBody] stock_master stock)
        {
            try
            {
                if (string.IsNullOrEmpty(stock.tenantcode))
                    stock.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(stock.tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.InsertStock(stock);
                return Ok(new { Status = "Success", StockCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>POST /api/ItemMaster/updatestock</summary>
        [HttpPost("updatestock")]
        public async Task<IActionResult> UpdateStock([FromBody] stock_master stock)
        {
            try
            {
                if (string.IsNullOrEmpty(stock.tenantcode))
                    stock.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(stock.tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.UpdateStock(stock);
                return Ok(new { Status = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/deletestock?stockcode=1</summary>
        [HttpGet("deletestock")]
        public async Task<IActionResult> DeleteStock(long stockcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteStock(stockcode, tenantcode);
                return Ok(new { Status = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getallstocks  — tenantcode from header</summary>
        [HttpGet("getallstocks")]
        public async Task<IActionResult> GetAllStocks()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllStocks(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getstockbycode?stockcode=1</summary>
        [HttpGet("getstockbycode")]
        public async Task<IActionResult> GetStockByCode(long stockcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetStockByCode(stockcode, tenantcode);

                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Stock not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getstockbyitem?itemcode=1  — tenantcode from header</summary>
        [HttpGet("getstockbyitem")]
        public async Task<IActionResult> GetStockByItem(long itemcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetStockByItem(itemcode, tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── INDENT MASTER ───────────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/insertindent</summary>
        [HttpPost("insertindent")]
        public async Task<IActionResult> InsertIndent([FromBody] indent_request request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.master.tenantcode))
                    request.master.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.InsertIndent(request);
                return Ok(new { Status = "Success", Message = "Indent inserted successfully", IndentCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>POST /api/ItemMaster/updateindent</summary>
        [HttpPost("updateindent")]
        public async Task<IActionResult> UpdateIndent([FromBody] indent_request request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.master.tenantcode))
                    request.master.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.UpdateIndent(request);
                return Ok(new { Status = "Success", Message = "Indent updated successfully", IndentCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/deleteindent?indentcode=1</summary>
        [HttpGet("deleteindent")]
        public async Task<IActionResult> DeleteIndent(long indentcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteIndent(indentcode, tenantcode);

                return result == "Success"
                    ? Ok(new { Status = "Success", Message = "Indent deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Indent not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getallindents  — tenantcode from header</summary>
        [HttpGet("getallindents")]
        public async Task<IActionResult> GetAllIndents()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllIndents(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getindentbycode?indentcode=1</summary>
        [HttpGet("getindentbycode")]
        public async Task<IActionResult> GetIndentByCode(long indentcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetIndentByCode(indentcode, tenantcode);

                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Indent not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── PURCHASE ENTRY (GRN) ────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/insertpurchaseentry</summary>
        [HttpPost("insertpurchaseentry")]
        public async Task<IActionResult> InsertPurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                if (request == null || request.master == null || request.details == null || !request.details.Any())
                    return BadRequest(new { Status = "Failed", Message = "Invalid request data" });

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    request.master.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    return MissingTenantCode();

                foreach (var d in request.details)
                    if (string.IsNullOrEmpty(d.tenantcode))
                        d.tenantcode = request.master.tenantcode;

                var result = await itemclass.InsertPurchaseEntry(request);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase Entry + Purchase Master inserted successfully",
                    PurchaseEntryCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>POST /api/ItemMaster/updatepurchaseentry</summary>
        [HttpPost("updatepurchaseentry")]
        public async Task<IActionResult> UpdatePurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.master.tenantcode))
                    request.master.tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(request.master.tenantcode))
                    return MissingTenantCode();

                foreach (var d in request.details)
                    if (string.IsNullOrEmpty(d.tenantcode))
                        d.tenantcode = request.master.tenantcode;

                var res = await itemclass.UpdatePurchaseEntry(request);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>DELETE /api/ItemMaster/deletepurchaseentry?id=1</summary>
        [HttpDelete("deletepurchaseentry")]
        public async Task<IActionResult> DeletePurchaseEntry(long id)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var res = await itemclass.DeletePurchaseEntry(id, tenantcode);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getallpurchaseentries  — tenantcode from header</summary>
        [HttpGet("getallpurchaseentries")]
        public async Task<IActionResult> GetAllPurchaseEntries()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllPurchaseEntries(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        /// <summary>GET /api/ItemMaster/getpurchaseentrybycode?purchaseentrycode=1</summary>
        [HttpGet("getpurchaseentrybycode")]
        public async Task<IActionResult> GetPurchaseEntryByCode(long purchaseentrycode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetPurchaseEntryByCode(purchaseentrycode, tenantcode);

                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Purchase Entry not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── EXCEL UPLOAD ────────────────────────────────────────────────────────────

        /// <summary>POST /api/ItemMaster/upload-excel</summary>
        [HttpPost("upload-excel")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, file.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                await itemclass.ProcessExcel(filePath);

                return Ok("Excel uploaded & data inserted successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}