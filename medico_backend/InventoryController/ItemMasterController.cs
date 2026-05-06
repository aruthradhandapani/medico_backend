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
        [HttpPost("insertitem")]
        public async Task<IActionResult> InsertItem([FromBody] item_master item)
        {
            try
            {
                var result = await itemclass.InsertItem(item);

                return Ok(new
                {
                    Status = "Success",
                    Message = item.itemcode == 0
                        ? "Item inserted successfully"
                        : "Item updated successfully",
                    ItemCode = result
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
        [HttpPost("upsertvendor")]

        public async Task<IActionResult> UpsertVendor([FromBody] vendor_master vendor)
        {
            try
            {
                var result = await itemclass.UpsertVendor(vendor);

                return Ok(new
                {
                    Status = "Success",
                    Message = vendor.vendorcode == 0
                        ? "Vendor inserted successfully"
                        : "Vendor updated successfully",
                    VendorCode = result
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
        [HttpPut("updatevendor")]
        public async Task<IActionResult> UpdateVendor([FromBody] vendor_master vendor)
        {
            try
            {
                var res = await itemclass.UpdateVendor(vendor);

                if (res == "Success")
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Vendor updated successfully"
                    });
                }

                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Unable to update vendor"
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
        [HttpDelete("deletevendor")]
        public async Task<IActionResult> DeleteVendor(long vendorcode)
        {
            try
            {
                var res = await itemclass.DeleteVendor(vendorcode);

                if (res == "Success")
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Vendor deleted successfully"
                    });
                }

                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Unable to delete vendor"
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
        [HttpPut("updateitem")]
        public async Task<IActionResult> UpdateItem([FromBody] item_master item)
        {
            try
            {
                var res = await itemclass.UpdateItem(item);

                if (res == "Success")
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Item updated successfully"
                    });
                }

                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Unable to update item"
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
        [HttpDelete("deleteitem")]
        public async Task<IActionResult> DeleteItem(long itemcode)
        {
            try
            {
                var res = await itemclass.DeleteItem(itemcode);

                if (res == "Success")
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Item deleted successfully"
                    });
                }

                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Unable to delete item"
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
        [HttpPost("insertpurchase")]
        public async Task<IActionResult> InsertPurchase([FromBody] purchase_request request)
        {
            try
            {
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
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        [HttpPut("updatepurchase")]
        public async Task<IActionResult> UpdatePurchase([FromBody] purchase_request request)
        {
            try
            {
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
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        [HttpDelete("deletepurchase")]
        public async Task<IActionResult> DeletePurchase(long purchasecode)
        {
            try
            {
                var result = await itemclass.DeletePurchase(purchasecode);

                if (result == "Success")
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Purchase deleted successfully"
                    });
                }

                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Purchase not found"
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
        [HttpPost("insertstock")]
        public async Task<IActionResult> InsertStock([FromBody] stock_master stock)
        {
            var result = await itemclass.InsertStock(stock);

            return Ok(new
            {
                Status = "Success",
                StockCode = result
            });
        }
        [HttpPut("updatestock")]
        public async Task<IActionResult> UpdateStock([FromBody] stock_master stock)
        {
            var result = await itemclass.UpdateStock(stock);

            return Ok(new
            {
                Status = result
            });
        }
        [HttpDelete("deletestock")]
        public async Task<IActionResult> DeleteStock(long stockcode)
        {
            var result = await itemclass.DeleteStock(stockcode);

            return Ok(new
            {
                Status = result
            });
        }
        [HttpPost("insertindent")]
        public async Task<IActionResult> InsertIndent([FromBody] indent_request request)
        {
            try
            {
                var result = await itemclass.InsertIndent(request);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Indent inserted successfully",
                    IndentCode = result
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

        // UPDATE
        [HttpPut("updateindent")]
        public async Task<IActionResult> UpdateIndent([FromBody] indent_request request)
        {
            try
            {
                var result = await itemclass.UpdateIndent(request);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Indent updated successfully",
                    IndentCode = result
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

        // DELETE
        [HttpDelete("deleteindent")]
        public async Task<IActionResult> DeleteIndent(long indentcode)
        {
            try
            {
                var result = await itemclass.DeleteIndent(indentcode);

                if (result == "Success")
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Indent deleted successfully"
                    });
                }

                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Indent not found"
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
        [HttpPost("insertpurchaseentry")]
        public async Task<IActionResult> InsertPurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                if (request == null || request.master == null || request.details == null || !request.details.Any())
                {
                    return BadRequest(new
                    {
                        Status = "Failed",
                        Message = "Invalid request data"
                    });
                }

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
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        [HttpPut("updatepurchaseentry")]
        public async Task<IActionResult> Update([FromBody] purchase_entry_request request)
        {
            var res = await itemclass.UpdatePurchaseEntry(request);
            return Ok(res);
        }

        [HttpDelete("deletepurchaseentry")]
        public async Task<IActionResult> Delete(long id)
        {
            var res = await itemclass.DeletePurchaseEntry(id);
            return Ok(res);
        }

        [HttpPost("upload-excel")]
        [HttpPost("upload-excel")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                // Create folder if not exists
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, file.FileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Process Excel
                await itemclass.ProcessExcel(filePath);

                return Ok("Excel uploaded & data inserted successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
