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
            Request.Headers["tenantcode"].ToString();

        private IActionResult MissingTenantCode() =>
            BadRequest(new { Status = "Failed", Message = "Header 'tenantcode' is required." });

        // ─── ITEM MASTER ──────────────────────────────────────────────────────────────

        [HttpPost("insertitem")]
        public async Task<IActionResult> InsertItem([FromBody] item_master item)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                item.tenantcode = tenantcode; // always override from header

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

        [HttpPost("updateitem")]
        public async Task<IActionResult> UpdateItem([FromBody] item_master item)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                item.tenantcode = tenantcode;

                var res = await itemclass.UpdateItem(item);
                return res == "Item Updated Successfully"
                    ? Ok(new { Status = "Success", Message = "Item updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update item" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

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

        // ─── VENDOR MASTER ────────────────────────────────────────────────────────────

        [HttpPost("upsertvendor")]
        public async Task<IActionResult> UpsertVendor([FromBody] vendor_master vendor)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                vendor.tenantcode = tenantcode;

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

        [HttpPost("updatevendor")]
        public async Task<IActionResult> UpdateVendor([FromBody] vendor_master vendor)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                vendor.tenantcode = tenantcode;

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

        // ─── PURCHASE MASTER ──────────────────────────────────────────────────────────

        [HttpPost("insertpurchase")]
        public async Task<IActionResult> InsertPurchase([FromBody] purchase_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var result = await itemclass.InsertPurchase(request);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase inserted successfully",
                    PurchaseCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updatepurchase")]
        public async Task<IActionResult> UpdatePurchase([FromBody] purchase_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var result = await itemclass.UpdatePurchase(request);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase updated successfully",
                    PurchaseCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deletepurchase")]
        public async Task<IActionResult> DeletePurchase(long purchasecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

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

        [HttpGet("getallpurchases")]
        public async Task<IActionResult> GetAllPurchases()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetAllPurchases(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getpurchasebycode")]
        public async Task<IActionResult> GetPurchaseByCode(long purchasecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

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

        // ─── STOCK MASTER ─────────────────────────────────────────────────────────────

        [HttpPost("insertstock")]
        public async Task<IActionResult> InsertStock([FromBody] stock_master stock)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                stock.tenantcode = tenantcode;

                var result = await itemclass.InsertStock(stock);
                return Ok(new { Status = "Success", StockCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updatestock")]
        public async Task<IActionResult> UpdateStock([FromBody] stock_master stock)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                stock.tenantcode = tenantcode;

                var result = await itemclass.UpdateStock(stock);
                return Ok(new { Status = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

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

        // ─── INDENT MASTER ────────────────────────────────────────────────────────────

        [HttpPost("insertindent")]
        public async Task<IActionResult> InsertIndent([FromBody] indent_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;

                var result = await itemclass.InsertIndent(request);
                return Ok(new { Status = "Success", Message = "Indent inserted successfully", IndentCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updateindent")]
        public async Task<IActionResult> UpdateIndent([FromBody] indent_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;

                var result = await itemclass.UpdateIndent(request);
                return Ok(new { Status = "Success", Message = "Indent updated successfully", IndentCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

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

        // ─── PURCHASE ENTRY (GRN) ─────────────────────────────────────────────────────

        [HttpPost("insertpurchaseentry")]
        public async Task<IActionResult> InsertPurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (request == null || request.master == null || request.details == null || !request.details.Any())
                    return BadRequest(new { Status = "Failed", Message = "Invalid request data" });

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

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

        [HttpPost("updatepurchaseentry")]
        public async Task<IActionResult> UpdatePurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var res = await itemclass.UpdatePurchaseEntry(request);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deletepurchaseentry")]
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

        // ─── EXCEL UPLOAD ─────────────────────────────────────────────────────────────

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

                await itemclass.ProcessExcel(filePath, tenantcode);

                return Ok("Excel uploaded & data inserted successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost("insertcategory")]
        public async Task<IActionResult> Insert([FromBody] category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var id = await itemclass.InsertCategory(model);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Category inserted successfully",
                    CategoryCode = id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }


        // GET ALL CATEGORY
        [HttpGet("getcategory")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var data = await itemclass.GetCategories(tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }


        // UPDATE CATEGORY
        [HttpPost("updatecategory")]
        public async Task<IActionResult> Update([FromBody] category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var result = await itemclass.UpdateCategory(model);

                return Ok(new
                {
                    Status = result ? "Success" : "Failed",
                    Message = result
                        ? "Category updated successfully"
                        : "Update failed"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }


        // DELETE CATEGORY
        [HttpGet("deletecategory")]
        public async Task<IActionResult> DeleteCategory(long id)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteCategory(id, tenantcode);

                return Ok(new
                {
                    Status = result ? "Success" : "Failed",
                    Message = result
                        ? "Category deleted successfully"
                        : "Delete failed"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        // INSERT
        [HttpPost("insertuom")]
        public async Task<IActionResult> Insert(uom_master model)
        {
            try
            {
                var tenant = GetTenantCode();

                if (string.IsNullOrEmpty(tenant))
                    return BadRequest("tenant_code header required");

                model.tenant_code = tenant;

                var id = await itemclass.InsertUom(model);

                return Ok(new
                {
                    status = "Success",
                    ucode = id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET ALL
        [HttpGet("getalluom")]
        public async Task<IActionResult> Getuom()
        {
            try
            {
                var tenant = GetTenantCode();

                var data = await itemclass.GetAllUom(tenant);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET BY CODE
        [HttpGet("getuombycode")]
        public async Task<IActionResult> GetByCode(long ucode)
        {
            try
            {
                var tenant = GetTenantCode();

                var data = await itemclass.GetUomByCode(ucode, tenant);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // UPDATE
        [HttpPost("updateuom")]
        public async Task<IActionResult> Update(uom_master model)
        {
            try
            {
                var tenant = GetTenantCode();

                model.tenant_code = tenant;

                var result = await itemclass.UpdateUom(model);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE
        [HttpGet("deleteuom")]
        public async Task<IActionResult> Delete(long ucode)
        {
            try
            {
                var tenant = GetTenantCode();

                var result = await itemclass.DeleteUom(ucode, tenant);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("insertparentcategory")]
        public async Task<IActionResult> InsertParentCategory(parent_category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var result = await itemclass.InsertParentCategory(model);

                return Ok(new
                {
                    Status = "Success",
                    Message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("getparentcategory")]
        public async Task<IActionResult> GetParentCategories()
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetParentCategories(tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        [HttpPost("updateparentcategory")]
        public async Task<IActionResult> UpdateParentCategory(parent_category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var result = await itemclass.UpdateParentCategory(model);

                return Ok(new
                {
                    Status = "Success",
                    Message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("deleteparentcategory")]
        public async Task<IActionResult> DeleteParentCategory(int parentcategorycode)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteParentCategory(
                    parentcategorycode,
                    tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        [HttpPost("insertledger")]
        public async Task<IActionResult> InsertLedger([FromBody] ledger_master ledger)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                ledger.tenantcode = tenantcode;

                var result = await itemclass.InsertLedger(ledger);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Ledger inserted successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updateledger")]
        public async Task<IActionResult> UpdateLedger([FromBody] ledger_master ledger)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                ledger.tenantcode = tenantcode;

                var res = await itemclass.UpdateLedger(ledger);

                return res == "Updated Successfully"
                    ? Ok(new { Status = "Success", Message = "Ledger updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update ledger" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getledger")]
        public async Task<IActionResult> GetLedger()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetLedger(tenantcode);

                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deleteledger")]
        public async Task<IActionResult> DeleteLedger(int ledgercode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteLedger(ledgercode, tenantcode);

                return result
                    ? Ok(new { Status = "Success", Message = "Ledger deleted successfully" })
                    : NotFound(new { Status = "Failed", Message = "Ledger not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
        [HttpPost("insertledgertype")]
        public async Task<IActionResult> InsertLedgerType([FromBody] ledger_type_master ledger)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { Status = "Failed", Message = "Tenant code missing" });

                ledger.tenantcode = tenantcode;

                var result = await itemclass.InsertLedgerType(ledger);

                return result == "Inserted Successfully"
                    ? Ok(new { Status = "Success", Message = result })
                    : BadRequest(new { Status = "Failed", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }


        // UPDATE
        [HttpPost("updateledgertype")]
        public async Task<IActionResult> UpdateLedgerType([FromBody] ledger_type_master ledger)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            ledger.tenantcode = tenantcode;

            var result = await itemclass.UpdateLedgerType(ledger);

            return Ok(new
            {
                Status = "Success",
                Message = result
            });
        }


        // DELETE
        [HttpGet("deleteledgertype")]
        public async Task<IActionResult> DeleteLedgerType(int ledgertypecode)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            var result = await itemclass.DeleteLedgerType(ledgertypecode, tenantcode);

            if (result)
            {
                return Ok(new
                {
                    Status = "Success",
                    Message = "Ledger Type deleted successfully"
                });
            }

            return NotFound(new
            {
                Status = "Failed",
                Message = "Ledger Type not found"
            });
        }



        [HttpPost("insertledgergroup")]
        public async Task<IActionResult> InsertLedgerGroup([FromBody] ledger_group_master ledger)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            ledger.tenantcode = tenantcode;

            var result = await itemclass.InsertLedgerGroup(ledger);

            return Ok(new
            {
                Status = "Success",
                Message = result
            });
        }


        // UPDATE
        [HttpPost("updateledgergroup")]
        public async Task<IActionResult> UpdateLedgerGroup([FromBody] ledger_group_master ledger)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            ledger.tenantcode = tenantcode;

            var result = await itemclass.UpdateLedgerGroup(ledger);

            return Ok(new
            {
                Status = "Success",
                Message = result
            });
        }


        // DELETE
        [HttpDelete("deleteledgergroup")]
        public async Task<IActionResult> DeleteLedgerGroup(int ledgergroupcode)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            var result = await itemclass.DeleteLedgerGroup(ledgergroupcode, tenantcode);

            if (result)
            {
                return Ok(new
                {
                    Status = "Success",
                    Message = "Ledger Group deleted successfully"
                });
            }

            return NotFound(new
            {
                Status = "Failed",
                Message = "Ledger Group not found"
            });
        }
         [HttpGet("getledgertype")]
 public async Task<IActionResult> GetLedgerTypes()
 {
     var result = await itemclass.GetLedgerTypes();

     return Ok(new
     {
         Status = "Success",
         Data = result
     });
 }
 [HttpGet("getledgergroup")]
 public async Task<IActionResult> GetLedgerGroups()
 {
     var result = await itemclass.GetLedgerGroups();

     return Ok(new
     {
         Status = "Success",
         Data = result
     });
 }
  [HttpPost("insertsales")]
 public async Task<IActionResult> InsertSales([FromBody] sales_request request)
 {
     try
     {
         var result = await itemclass.InsertSales(request);

         return Ok(new
         {
             Status = "Success",
             Message = result
         });
     }
     catch (Exception ex)
     {
         return BadRequest(new
         {
             Status = "Error",
             Message = ex.Message
         });
     }
 }
 [HttpPost("updatesales")]
 public async Task<IActionResult> UpdateSales([FromBody] sales_request request)
 {
     try
     {
         var result = await itemclass.UpdateSales(request);

         return Ok(new
         {
             Status = "Success",
             Message = result
         });
     }
     catch (Exception ex)
     {
         return BadRequest(new
         {
             Status = "Error",
             Message = ex.Message
         });
     }
 }
 [HttpGet("getsales")]
 public async Task<IActionResult> GetSales()
 {
     var data = await itemclass.GetSales();

     return Ok(new
     {
         Status = "Success",
         Data = data
     });
 }
 [HttpDelete("deletesales")]
 public async Task<IActionResult> DeleteSales(long salescode)
 {
     try
     {
         var result = await itemclass.DeleteSales(salescode);

         return Ok(new
         {
             Status = "Success",
             Message = result
         });
     }
     catch (Exception ex)
     {
         return BadRequest(new
         {
             Status = "Error",
             Message = ex.Message
         });
     }
 }
  [HttpPost("upsertwarehouse")]
 public async Task<IActionResult> Upsert([FromBody] warehouse_master warehouse)
 {
     try
     {
         var result = await itemclass.InsertOrUpdateWarehouse(warehouse);
         return Ok(new
         {
             status = true,
             message = result
         });
     }
     catch (Exception ex)
     {
         return BadRequest(new
         {
             status = false,
             message = ex.Message
         });
     }
 }
 [HttpGet("getallwarehouse")]
 public async Task<IActionResult> GetAllS()
 {
     try
     {
         var result = await itemclass.GetWarehouseList();
         return Ok(result);
     }
     catch (Exception ex)
     {
         return BadRequest(ex.Message);
     }
 }
 [HttpDelete("deletewarehouse")]
 public async Task<IActionResult> Delete(int warehousecode)
 {
     try
     {
         var result = await itemclass.DeleteWarehouse(warehousecode);
         return Ok(result);
     }
     catch (Exception ex)
     {
         return BadRequest(ex.Message);
     }
 }
 [HttpPost("upsertmanufacturer")]
public async Task<IActionResult> UpsertManufacturer([FromBody] manufacturer_master manufacturer)
{
    try
    {
        var result = await itemclass.UpsertManufacturer(manufacturer);
        return Ok(result);
    }
    catch (Exception ex)
    {
        return BadRequest(ex.Message);
    }
}
[HttpGet("getlistmanufacturer")]
public async Task<IActionResult> GetList()
{
    try
    {
        var result = await itemclass.GetManufacturerList();
        return Ok(result);
    }
    catch (Exception ex)
    {
        return BadRequest(ex.Message);
            }
        }
        [HttpDelete("deletemanufacturer")]
        public async Task<IActionResult> Deletemanufacturer(long manufacturercode)
        {
            try
            {
                var result = await itemclass.DeleteManufacturer(manufacturercode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        // ─── PURCHASE RETURN ──────────────────────────────────────────────────────────

        // Step 1: search by item name + batch no → returns vendor + rate + available qty
        [HttpGet("getpurchasereturnlookup")]
        public async Task<IActionResult> GetPurchaseReturnLookup(long itemcode, string? batchno = null)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetPurchaseReturnLookup(itemcode, batchno, tenantcode);

                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // Step 2: submit returnqty + packsize → computes amount, updates stock + purchase_detail
        [HttpPost("insertpurchasereturn")]
        public async Task<IActionResult> InsertPurchaseReturn([FromBody] purchase_return_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.tenantcode = tenantcode;

                var result = await itemclass.InsertPurchaseReturn(request);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase return processed successfully",
                    PurchaseReturnCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
    }
}
    
        
    
    

