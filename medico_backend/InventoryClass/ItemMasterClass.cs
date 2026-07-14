using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.InventoryModel;
using Npgsql;
using OfficeOpenXml;
using System.Data;

namespace medico_backend.InventoryClass
{
    public class ItemMasterClass
    {
        private readonly string con;

        public ItemMasterClass(IConfiguration config)
        {
            con = config.GetConnectionString("inventory_conn");
        }

        // ─── ITEM MASTER ──────────────────────────────────────────────────────────────

 public async Task<string> InsertItem(item_master item)
{
    try
    {
        using (IDbConnection db = new NpgsqlConnection(con))
        {
            await db.InsertAsync(item);

            return "Item Inserted Successfully";
        }
    }
    catch (Exception ex)
    {
        return ex.Message;
    }
}
        public async Task<string> UpdateItem(item_master item)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    bool result = await db.UpdateAsync(item);

                    return result
                        ? "Item Updated Successfully"
                        : "Item Not Found";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<string> DeleteItem(long itemcode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.item_master
                        SET deleted = true, isactive = false
                        WHERE itemcode = @itemcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, new { itemcode, tenantcode });
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Item delete failed: " + ex.Message);
            }
        }

        public async Task<IEnumerable<item_master>> GetAllItems(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<item_master>(@"
                SELECT * FROM public.item_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY itemcode DESC;",
                new { tenantcode });
        }

        public async Task<item_master?> GetItemByCode(long itemcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryFirstOrDefaultAsync<item_master>(@"
                SELECT * FROM public.item_master
                WHERE itemcode = @itemcode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { itemcode, tenantcode });
        }

        // ─── VENDOR MASTER ────────────────────────────────────────────────────────────

        public async Task<long> UpsertVendor(vendor_master vendor)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    if (vendor.vendorcode == 0)
                    {
                        // Insert
                        long id = await db.InsertAsync(vendor);
                        return id;
                    }
                    else
                    {
                        // Update
                        bool updated = await db.UpdateAsync(vendor);

                        if (updated)
                            return vendor.vendorcode;
                        else
                            throw new Exception("Vendor not found");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor upsert failed: " + ex.Message);
            }
        }

        public async Task<string> UpdateVendor(vendor_master vendor)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.vendor_master
                        SET
                            vendorname            = @vendorname,
                            shortname             = @shortname,
                            vendortype            = @vendortype,
                            contactperson         = @contactperson,
                            phonenumber           = @phonenumber,
                            alternatephonenumber  = @alternatephonenumber,
                            emailid               = @emailid,
                            website               = @website,
                            gstnumber             = @gstnumber,
                            pannumber             = @pannumber,
                            taxid                 = @taxid,
                            registrationnumber    = @registrationnumber,
                            addressline1          = @addressline1,
                            addressline2          = @addressline2,
                            landmark              = @landmark,
                            city                  = @city,
                            district              = @district,
                            state                 = @state,
                            postalcode            = @postalcode,
                            countrycode           = @countrycode,
                            countryname           = @countryname,
                            currencycode          = @currencycode,
                            paymentterms          = @paymentterms,
                            creditperiod          = @creditperiod,
                            bankname              = @bankname,
                            accountnumber         = @accountnumber,
                            ifsccode              = @ifsccode,
                            swiftcode             = @swiftcode,
                            ibannumber            = @ibannumber,
                            isactive              = @isactive,
                            deleted               = @deleted,
                            modifieddate          = CURRENT_TIMESTAMP,
                            usercode              = @usercode,
                            tenantcode            = @tenantcode,
                            branchcode            = @branchcode
                        WHERE vendorcode = @vendorcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, vendor);
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor update failed: " + ex.Message);
            }
        }

        public async Task<string> DeleteVendor(long vendorcode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.vendor_master
                        SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
                        WHERE vendorcode = @vendorcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, new { vendorcode, tenantcode });
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor delete failed: " + ex.Message);
            }
        }

        public async Task<IEnumerable<vendor_master>> GetAllVendors(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<vendor_master>(@"
                SELECT * FROM public.vendor_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY vendorcode DESC;",
                new { tenantcode });
        }

        public async Task<vendor_master?> GetVendorByCode(long vendorcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryFirstOrDefaultAsync<vendor_master>(@"
                SELECT * FROM public.vendor_master
                WHERE vendorcode = @vendorcode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { vendorcode, tenantcode });
        }

        // ─── PURCHASE MASTER ──────────────────────────────────────────────────────────

              public async Task<long> InsertPurchase(purchase_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // ==========================
                        // Purchase Master Insert
                        // ==========================

                        string masterQuery = @"
                INSERT INTO purchase_master
                (
                    billno,
                    billdate,
                    invoiceno,
                    invoicedate,
                    vendorcode,
                    grossamount,
                    discountamount,
                    taxamount,
                    netamount,
                    paymentmode,
                    paymentstatus,
                    currencycode,
                    remarks,
                    isactive,
                    deleted,
                    createddate,
                    usercode,
                    tenantcode,
                    branchcode,
                    companycode
                )
                VALUES
                (
                    @billno,
                    @billdate,
                    @invoiceno,
                    @invoicedate,
                    @vendorcode,
                    @grossamount,
                    @discountamount,
                    @taxamount,
                    @netamount,
                    @paymentmode,
                    @paymentstatus,
                    @currencycode,
                    @remarks,
                    @isactive,
                    @deleted,
                    CURRENT_TIMESTAMP,
                    @usercode,
                    @tenantcode,
                    @branchcode,
                    @companycode
                )
                RETURNING purchasecode;";

                        long purchasecode = await db.ExecuteScalarAsync<long>(
                            masterQuery,
                            request.master,
                            transaction);

                        // ==========================
                        // Purchase Detail Insert
                        // ==========================

                        string detailQuery = @"
                INSERT INTO purchase_detail
                (
                    purchasecode,
                    itemcode,
                    quantity,
                    freequantity,
                    uomcode,
                    rate,
                    discountpercentage,
                    discountamount,
                    taxpercentage,
                    taxamount,
                    amount,
                    totalamount,
                    batchno,
                    manufacturingdate,
                    expirydate,
                    orderedqty,
                    receivedqty,
                    rejectedqty,
                    warehousecode,
                    tenantcode
                )
                VALUES
                (
                    @purchasecode,
                    @itemcode,
                    @quantity,
                    @freequantity,
                    @uomcode,
                    @rate,
                    @discountpercentage,
                    @discountamount,
                    @taxpercentage,
                    @taxamount,
                    @amount,
                    @totalamount,
                    @batchno,
                    @manufacturingdate,
                    @expirydate,
                    @orderedqty,
                    @receivedqty,
                    @rejectedqty,
                    @warehousecode,
                    @tenantcode
                );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = purchasecode;

                            await db.ExecuteAsync(
                                detailQuery,
                                item,
                                transaction);

                            // Stock Update/Insert comes here...

                            // Check stock exists
                            //========================================
                            // Check Stock Exists
                            //========================================

                            string checkStockQuery = @"
SELECT stockcode
FROM stock_master
WHERE itemcode = @itemcode
AND warehousecode = @warehousecode
AND batchno = @batchno;";

                            var stockcode = await db.ExecuteScalarAsync<long?>(
                                checkStockQuery,
                                new
                                {
                                    item.itemcode,
                                    item.warehousecode,
                                    item.batchno
                                },
                                transaction);

                            //========================================
                            // Update Existing Stock
                            //========================================

                            if (stockcode.HasValue)
                            {
                                string updateStockQuery = @"
UPDATE stock_master
SET
    purchasedqty = COALESCE(purchasedqty,0) + @quantity,
    closingstock = COALESCE(closingstock,0) + @quantity,
    unitcost = @rate,
    stockvalue = (COALESCE(closingstock,0) + @quantity) * @rate,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode;";

                                await db.ExecuteAsync(
                                    updateStockQuery,
                                    new
                                    {
                                        stockcode = stockcode.Value,
                                        quantity = item.quantity,
                                        rate = item.rate
                                    },
                                    transaction);
                            }
                            else
                            {
                                //========================================
                                // Insert New Stock
                                //========================================

                                string insertStockQuery = @"
    INSERT INTO stock_master
    (
        itemcode,
        warehousecode,
        branchcode,
        openingstock,
        purchasedqty,
        soldqty,
        damagedqty,
        returnqty,
        closingstock,
        unitcost,
        stockvalue,
        batchno,
        manufacturingdate,
        expirydate,
        isactive,
        deleted,
        createddate,
        usercode,
        tenantcode,
        companycode
    )
    VALUES
    (
        @itemcode,
        @warehousecode,
        @branchcode,
        0,
        @receivedqty,
        0,
        0,
        0,
        @receivedqty,
        @rate,
        (@receivedqty * @rate),
        @batchno,
        @manufacturingdate,
        @expirydate,
        TRUE,
        FALSE,
        CURRENT_TIMESTAMP,
        @usercode,
        @tenantcode,
        @companycode
    );";

                                await db.ExecuteAsync(
                                    insertStockQuery,
                                    new
                                    {
                                        item.itemcode,
                                        item.warehousecode,
                                        branchcode = request.master.branchcode,
                                        receivedqty = item.receivedqty,
                                        item.rate,
                                        item.batchno,
                                        item.manufacturingdate,
                                        item.expirydate,
                                        usercode = request.master.usercode,
                                        tenantcode = request.master.tenantcode,
                                        companycode = request.master.companycode
                                    },
                                    transaction);
                            }
                        } // End foreach

                        //==========================
                        // Commit Transaction
                        //==========================

                        transaction.Commit();

                        return purchasecode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();

                        throw new Exception("Purchase Insert Failed : " + ex.Message);
                    }
                }
            }
        }

        public async Task<long> UpdatePurchase(purchase_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        string masterQuery = @"
                UPDATE public.purchase_master
                SET
                    billno                 = @billno,
                    billdate               = @billdate,
                    invoiceno              = @invoiceno,
                    invoicedate            = @invoicedate,
                    vendorcode             = @vendorcode,
                    grossamount            = @grossamount,
                    discountamount         = @discountamount,
                    taxamount              = @taxamount,
                    netamount              = @netamount,
                    transportationcharges  = @transportationcharges,
                    roundoff               = @roundoff,
                    paymentmode            = @paymentmode,
                    paymentstatus          = @paymentstatus,
                    currencycode           = @currencycode,
                    isactive               = @isactive,
                    deleted                = @deleted,
                    remarks                = @remarks,
                    modifieddate           = CURRENT_TIMESTAMP,
                    usercode               = @usercode,
                    tenantcode             = @tenantcode,
                    branchcode             = @branchcode,
                    companycode            = @companycode,
                    grncode                = @grncode
                WHERE purchasecode = @purchasecode
                  AND tenantcode = @tenantcode;";

                        int masterRows = await db.ExecuteAsync(
                            masterQuery,
                            request.master,
                            transaction);

                        if (masterRows == 0)
                            throw new Exception("Purchase record not found.");

                        await db.ExecuteAsync(
                            @"DELETE FROM public.purchase_detail
                      WHERE purchasecode = @purchasecode;",
                            new
                            {
                                purchasecode = request.master.purchasecode
                            },
                            transaction);

                        string detailQuery = @"
                INSERT INTO public.purchase_detail
                (
                    purchasecode,
                    itemcode,
                    quantity,
                    freequantity,
                    uomcode,
                    rate,
                    discountpercentage,
                    discountamount,
                    taxpercentage,
                    taxamount,
                    amount,
                    totalamount,
                    batchno,
                    manufacturingdate,
                    expirydate,
                    orderedqty,
                    receivedqty,
                    rejectedqty,
                    warehousecode,
                    packaging,
                    manufacturercode,
                    tenantcode
                )
                VALUES
                (
                    @purchasecode,
                    @itemcode,
                    @quantity,
                    @freequantity,
                    @uomcode,
                    @rate,
                    @discountpercentage,
                    @discountamount,
                    @taxpercentage,
                    @taxamount,
                    @amount,
                    @totalamount,
                    @batchno,
                    @manufacturingdate,
                    @expirydate,
                    @orderedqty,
                    @receivedqty,
                    @rejectedqty,
                    @warehousecode,
                    @packaging,
                    @manufacturercode,
                    @tenantcode
                );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = request.master.purchasecode;

                            await db.ExecuteAsync(
                                detailQuery,
                                item,
                                transaction);
                        }

                        transaction.Commit();

                        return request.master.purchasecode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Purchase update failed: {ex.Message}");
                    }
                }
            }
        }

        public async Task<string> DeletePurchase(long purchasecode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        int masterRows = await db.ExecuteAsync(@"
                UPDATE public.purchase_master
                SET
                    deleted = true,
                    isactive = false,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE purchasecode = @purchasecode
                  AND tenantcode = @tenantcode;",
                            new
                            {
                                purchasecode,
                                tenantcode
                            },
                            transaction);

                        await db.ExecuteAsync(
                            @"DELETE FROM public.purchase_detail
                      WHERE purchasecode = @purchasecode;",
                            new { purchasecode },
                            transaction);

                        transaction.Commit();

                        return masterRows > 0 ? "Success" : "Failed";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase delete failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<IEnumerable<purchase_request>> GetAllPurchases(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var query = @"
    SELECT
    m.purchasecode,
    m.billno,
    m.billdate::timestamp,
    m.invoiceno,
    m.invoicedate::timestamp,   -- ← cast nullable date
    m.vendorcode,
    m.grossamount,
    m.discountamount,
    m.taxamount,
    m.netamount,
    m.paymentmode,
    m.paymentstatus,
    m.currencycode,
    m.isactive,
    m.deleted,
    m.remarks,
    m.createddate::timestamp,
    m.modifieddate::timestamp,
    m.usercode,
    m.tenantcode,
    m.branchcode,
    m.companycode,
    m.grncode,
    m.transportationcharges,
    m.roundoff,
    
    d.purchasedetailcode,
    d.purchasecode,
    d.itemcode,
    d.quantity,
    d.freequantity,
    d.uomcode,
    d.rate,
    d.discountpercentage,
    d.discountamount,
    d.taxpercentage,
    d.taxamount,
    d.amount,
    d.totalamount,
    d.batchno,
    d.manufacturingdate::timestamp,   -- ← cast
    d.expirydate::timestamp,          -- ← cast
    d.orderedqty,
    d.receivedqty,
    d.rejectedqty,
    d.warehousecode,
    d.tenantcode,
    d.packaging,
    d.manufacturercode

    FROM public.purchase_master m

    LEFT JOIN public.purchase_detail d
        ON m.purchasecode = d.purchasecode

    WHERE m.deleted = false
      AND m.tenantcode = @tenantcode

    ORDER BY m.purchasecode DESC;";

            var purchaseDictionary = new Dictionary<long, purchase_request>();

            await db.QueryAsync<purchase_master, purchase_detail, purchase_request>(
                query,
                (master, detail) =>
                {
                    if (!purchaseDictionary.TryGetValue(master.purchasecode, out var purchase))
                    {
                        purchase = new purchase_request
                        {
                            master = master,
                            details = new List<purchase_detail>()
                        };

                        purchaseDictionary.Add(master.purchasecode, purchase);
                    }

                    if (detail != null && detail.purchasedetailcode != 0)
                    {
                        purchase.details.Add(detail);
                    }

                    return purchase;
                },
                new { tenantcode },
                splitOn: "purchasedetailcode"
            );

            return purchaseDictionary.Values.ToList();
        }

        public async Task<purchase_request?> GetPurchaseByCode(
            long purchasecode,
            string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var query = @"
    SELECT
        m.purchasecode,
        m.billno,
        m.billdate,
        m.invoiceno,
        m.invoicedate,
        m.vendorcode,
        m.grossamount,
        m.discountamount,
        m.taxamount,
        m.netamount,
        m.paymentmode,
        m.paymentstatus,
        m.currencycode,
        m.isactive,
        m.deleted,
        m.remarks,
        m.createddate,
        m.modifieddate,
        m.usercode,
        m.tenantcode,
        m.branchcode,
        m.companycode,
        m.grncode,

        d.purchasedetailcode,
        d.purchasecode,
        d.itemcode,
        d.quantity,
        d.freequantity,
        d.uomcode,
        d.rate,
        d.discountpercentage,
        d.discountamount,
        d.taxpercentage,
        d.taxamount,
        d.amount,
        d.totalamount,
        d.batchno,
        d.manufacturingdate,
        d.expirydate,
        d.orderedqty,
        d.receivedqty,
        d.rejectedqty,
        d.warehousecode,
        d.tenantcode

    FROM public.purchase_master m

    LEFT JOIN public.purchase_detail d
        ON m.purchasecode = d.purchasecode

    WHERE m.purchasecode = @purchasecode
      AND m.tenantcode = @tenantcode
      AND m.deleted = false;";

            purchase_request? response = null;

            await db.QueryAsync<purchase_master, purchase_detail, purchase_request>(
                query,
                (master, detail) =>
                {
                    if (response == null)
                    {
                        response = new purchase_request
                        {
                            master = master,
                            details = new List<purchase_detail>()
                        };
                    }

                    if (detail != null && detail.purchasedetailcode != 0)
                    {
                        response.details.Add(detail);
                    }

                    return response;
                },
                new
                {
                    purchasecode,
                    tenantcode
                },
                splitOn: "purchasedetailcode"
            );

            return response;
        }
        // ─── STOCK MASTER ─────────────────────────────────────────────────────────────

        public async Task<long> InsertStock(stock_master stock)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        INSERT INTO public.stock_master
                        (
                            itemcode, warehousecode, branchcode, locationcode,
                            openingstock, purchasedqty, soldqty, damagedqty, returnqty, closingstock,
                            unitcost, stockvalue,
                            batchno, manufacturingdate, expirydate,
                            isactive, deleted, createddate,
                            usercode, tenantcode, companycode
                        )
                        VALUES
                        (
                            @itemcode, @warehousecode, @branchcode, @locationcode,
                            @openingstock, @purchasedqty, @soldqty, @damagedqty, @returnqty, @closingstock,
                            @unitcost, @stockvalue,
                            @batchno, @manufacturingdate, @expirydate,
                            @isactive, @deleted, CURRENT_TIMESTAMP,
                            @usercode, @tenantcode, @companycode
                        )
                        RETURNING stockcode;";

                    return await db.ExecuteScalarAsync<long>(query, stock);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock insert failed: " + ex.Message);
            }
        }

        public async Task<string> UpdateStock(stock_master stock)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.stock_master
                        SET
                            itemcode          = @itemcode,
                            warehousecode     = @warehousecode,
                            branchcode        = @branchcode,
                            locationcode      = @locationcode,
                            openingstock      = @openingstock,
                            purchasedqty      = @purchasedqty,
                            soldqty           = @soldqty,
                            damagedqty        = @damagedqty,
                            returnqty         = @returnqty,
                            closingstock      = @closingstock,
                            unitcost          = @unitcost,
                            stockvalue        = @stockvalue,
                            batchno           = @batchno,
                            manufacturingdate = @manufacturingdate,
                            expirydate        = @expirydate,
                            isactive          = @isactive,
                            deleted           = @deleted,
                            modifieddate      = CURRENT_TIMESTAMP,
                            usercode          = @usercode,
                            tenantcode        = @tenantcode,
                            companycode       = @companycode
                        WHERE stockcode = @stockcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, stock);
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock update failed: " + ex.Message);
            }
        }

        public async Task<string> DeleteStock(long stockcode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.stock_master
                        SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
                        WHERE stockcode = @stockcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, new { stockcode, tenantcode });
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock delete failed: " + ex.Message);
            }
        }

        public async Task<IEnumerable<stock_master>> GetAllStocks(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            return await db.QueryAsync<stock_master>(@"
        SELECT 
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            openingstock,
            purchasedqty,
            soldqty,
            damagedqty,
            returnqty,
            closingstock,
            unitcost,
            stockvalue,
            batchno,

            manufacturingdate::timestamp AS manufacturingdate,
            expirydate::timestamp AS expirydate,

            isactive,
            deleted,
            createddate,
            modifieddate,
            usercode,
            tenantcode,
            companycode
        FROM public.stock_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY stockcode DESC;",
                new { tenantcode });
        }

        public async Task<stock_master?> GetStockByCode(long stockcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            return await db.QueryFirstOrDefaultAsync<stock_master>(@"
        SELECT 
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            openingstock,
            purchasedqty,
            soldqty,
            damagedqty,
            returnqty,
            closingstock,
            unitcost,
            stockvalue,
            batchno,

            manufacturingdate::timestamp AS manufacturingdate,
            expirydate::timestamp AS expirydate,

            isactive,
            deleted,
            createddate,
            modifieddate,
            usercode,
            tenantcode,
            companycode
        FROM public.stock_master
        WHERE stockcode = @stockcode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { stockcode, tenantcode });
        }

        public async Task<IEnumerable<stock_master>> GetStockByItem(long itemcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            return await db.QueryAsync<stock_master>(@"
        SELECT 
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            openingstock,
            purchasedqty,
            soldqty,
            damagedqty,
            returnqty,
            closingstock,
            unitcost,
            stockvalue,
            batchno,

            manufacturingdate::timestamp AS manufacturingdate,
            expirydate::timestamp AS expirydate,

            isactive,
            deleted,
            createddate,
            modifieddate,
            usercode,
            tenantcode,
            companycode
        FROM public.stock_master
        WHERE itemcode = @itemcode
          AND tenantcode = @tenantcode
          AND deleted = false
        ORDER BY stockcode DESC;",
                new { itemcode, tenantcode });
        }

        // ─── INDENT MASTER ────────────────────────────────────────────────────────────

        public async Task<long> InsertIndent(indent_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        string masterQuery = @"
                            INSERT INTO public.indent_master
                            (
                                indentno, indentdate, requestedby,
                                departmentcode, branchcode, remarks,
                                approvalstatus, isactive, deleted,
                                createddate, tenantcode
                            )
                            VALUES
                            (
                                @indentno, @indentdate, @requestedby,
                                @departmentcode, @branchcode, @remarks,
                                @approvalstatus, @isactive, @deleted,
                                CURRENT_TIMESTAMP, @tenantcode
                            )
                            RETURNING indentcode;";

                        long indentcode = await db.ExecuteScalarAsync<long>(masterQuery, request.master, transaction);

                        string detailQuery = @"
                            INSERT INTO public.indent_detail
                            (indentcode, itemcode, requestedqty, approvedqty, issuedqty, remarks)
                            VALUES
                            (@indentcode, @itemcode, @requestedqty, @approvedqty, @issuedqty, @remarks);";

                        foreach (var item in request.details)
                        {
                            item.indentcode = indentcode;
                            await db.ExecuteAsync(detailQuery, item, transaction);
                        }

                        transaction.Commit();
                        return indentcode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Indent insert failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<long> UpdateIndent(indent_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        await db.ExecuteAsync(@"
                            UPDATE public.indent_master
                            SET
                                indentno       = @indentno,
                                indentdate     = @indentdate,
                                requestedby    = @requestedby,
                                departmentcode = @departmentcode,
                                branchcode     = @branchcode,
                                remarks        = @remarks,
                                approvalstatus = @approvalstatus,
                                tenantcode     = @tenantcode
                            WHERE indentcode = @indentcode
                              AND tenantcode = @tenantcode;",
                            request.master, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM public.indent_detail WHERE indentcode = @indentcode;",
                            new { indentcode = request.master.indentcode }, transaction);

                        string insertDetail = @"
                            INSERT INTO public.indent_detail
                            (indentcode, itemcode, requestedqty, approvedqty, issuedqty, remarks)
                            VALUES
                            (@indentcode, @itemcode, @requestedqty, @approvedqty, @issuedqty, @remarks);";

                        foreach (var item in request.details)
                        {
                            item.indentcode = request.master.indentcode;
                            await db.ExecuteAsync(insertDetail, item, transaction);
                        }

                        transaction.Commit();
                        return request.master.indentcode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Indent update failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<string> DeleteIndent(long indentcode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        await db.ExecuteAsync(@"
                            UPDATE public.indent_master
                            SET deleted = true, isactive = false
                            WHERE indentcode = @indentcode
                              AND tenantcode = @tenantcode;",
                            new { indentcode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM public.indent_detail WHERE indentcode = @indentcode;",
                            new { indentcode }, transaction);

                        transaction.Commit();
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Indent delete failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<IEnumerable<object>> GetAllIndents(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var masters = await db.QueryAsync<indent_master>(@"
        SELECT *
        FROM indent_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY indentcode DESC;",
                new { tenantcode });

            var result = new List<object>();

            foreach (var master in masters)
            {
                var details = await db.QueryAsync<indent_detail>(@"
            SELECT *
            FROM indent_detail
            WHERE indentcode = @indentcode;",
                    new
                    {
                        indentcode = master.indentcode,
                        tenantcode
                    });

                result.Add(new
                {
                    master,
                    details
                });
            }

            return result;
        }

        public async Task<object?> GetIndentByCode(long indentcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<indent_master>(@"
        SELECT *
        FROM indent_master
        WHERE indentcode = @indentcode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { indentcode, tenantcode });

            if (master == null)
                return null;

            var details = await db.QueryAsync<indent_detail>(@"
        SELECT *
        FROM indent_detail
        WHERE indentcode = @indentcode;",
                new { indentcode, tenantcode });

            return new
            {
                master,
                details
            };
        }

        // ─── PURCHASE ENTRY (GRN) ─────────────────────────────────────────────────────

        public async Task<long> InsertPurchaseEntry(purchase_entry_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var purchaseentrycode = await db.ExecuteScalarAsync<long>(@"
                            INSERT INTO purchase_entry_master
                            (
                                grnno, grndate, receivedby,
                                billno, billdate, invoiceno, invoicedate,
                                vendorcode,
                                totalqty, receivedqty,
                                grossamount, discountamount, taxamount, othercharges, netamount,
                                paymentmode, paymentstatus,
                                approvalstatus, posted,
                                remarks, isactive, deleted,
                                createddate, usercode,
                                tenantcode, branchcode, companycode
                            )
                            VALUES
                            (
                                @grnno, @grndate, @receivedby,
                                @billno, @billdate, @invoiceno, @invoicedate,
                                @vendorcode,
                                @totalqty, @receivedqty,
                                @grossamount, @discountamount, @taxamount, @othercharges, @netamount,
                                @paymentmode, @paymentstatus,
                                'PENDING', false,
                                @remarks, @isactive, @deleted,
                                CURRENT_TIMESTAMP, @usercode,
                                @tenantcode, @branchcode, @companycode
                            )
                            RETURNING purchaseentrycode;",
                            request.master, transaction);

                        foreach (var item in request.details)
                        {
                            item.purchaseentrycode = purchaseentrycode;

                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_entry_detail
                                (
                                    purchaseentrycode, itemcode,
                                    orderedqty, receivedqty, rejectedqty, quantity,
                                    rate, discountpercentage, discountamount,
                                    taxpercentage, taxamount,
                                    amount, totalamount,
                                    batchno, manufacturingdate, expirydate,
                                    warehousecode, tenantcode
                                )
                                VALUES
                                (
                                    @purchaseentrycode, @itemcode,
                                    @orderedqty, @receivedqty, @rejectedqty, @quantity,
                                    @rate, @discountpercentage, @discountamount,
                                    @taxpercentage, @taxamount,
                                    @amount, @totalamount,
                                    @batchno, @manufacturingdate, @expirydate,
                                    @warehousecode, @tenantcode
                                );",
                                item, transaction);
                        }

                        var purchasecode = await db.ExecuteScalarAsync<long>(@"
                            INSERT INTO purchase_master
                            (
                                billno, billdate, invoiceno, invoicedate,
                                vendorcode,
                                grossamount, discountamount, taxamount, netamount,
                                paymentmode, paymentstatus, currencycode,
                                isactive, deleted, remarks,
                                createddate, usercode,
                                tenantcode, branchcode, companycode,
                                grncode
                            )
                            VALUES
                            (
                                @billno, @billdate, @invoiceno, @invoicedate,
                                @vendorcode,
                                @grossamount, @discountamount, @taxamount, @netamount,
                                @paymentmode, @paymentstatus, @currencycode,
                                @isactive, @deleted, @remarks,
                                CURRENT_TIMESTAMP, @usercode,
                                @tenantcode, @branchcode, @companycode,
                                @grncode
                            )
                            RETURNING purchasecode;",
                            new
                            {
                                request.master.billno,
                                request.master.billdate,
                                request.master.invoiceno,
                                request.master.invoicedate,
                                request.master.vendorcode,
                                request.master.grossamount,
                                request.master.discountamount,
                                request.master.taxamount,
                                request.master.netamount,
                                request.master.paymentmode,
                                request.master.paymentstatus,
                                currencycode = "INR",
                                request.master.isactive,
                                request.master.deleted,
                                request.master.remarks,
                                request.master.usercode,
                                request.master.tenantcode,
                                request.master.branchcode,
                                request.master.companycode,
                                grncode = purchaseentrycode
                            },
                            transaction);

                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_detail
                                (
                                    purchasecode, itemcode, quantity,
                                    rate, discountpercentage, discountamount,
                                    taxpercentage, taxamount,
                                    amount, totalamount,
                                    batchno, manufacturingdate, expirydate,
                                    tenantcode
                                )
                                VALUES
                                (
                                    @purchasecode, @itemcode, @qty,
                                    @rate, @discountpercentage, @discountamount,
                                    @taxpercentage, @taxamount,
                                    @amount, @totalamount,
                                    @batchno, @manufacturingdate, @expirydate,
                                    @tenantcode
                                );",
                                new
                                {
                                    purchasecode,
                                    item.itemcode,
                                    qty = item.receivedqty,
                                    item.rate,
                                    item.discountpercentage,
                                    item.discountamount,
                                    item.taxpercentage,
                                    item.taxamount,
                                    item.amount,
                                    item.totalamount,
                                    item.batchno,
                                    item.manufacturingdate,
                                    item.expirydate,
                                    item.tenantcode
                                },
                                transaction);
                        }

                        transaction.Commit();
                        return purchaseentrycode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Insert failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<string> UpdatePurchaseEntry(purchase_entry_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var entryId = request.master.purchaseentrycode;
                        var tenantcode = request.master.tenantcode;

                        var oldItems = await db.QueryAsync<purchase_entry_detail>(
                            "SELECT * FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = entryId }, transaction);

                        foreach (var item in oldItems)
                        {
                            await db.ExecuteAsync(@"
                                UPDATE stock_master
                                SET purchasedqty = purchasedqty - @qty,
                                    closingstock = closingstock - @qty
                                WHERE itemcode = @itemcode
                                  AND tenantcode = @tenantcode;",
                                new { qty = item.receivedqty, item.itemcode, tenantcode }, transaction);
                        }

                        await db.ExecuteAsync(@"
                            UPDATE purchase_entry_master
                            SET
                                billno         = @billno,
                                billdate       = @billdate,
                                invoiceno      = @invoiceno,
                                invoicedate    = @invoicedate,
                                vendorcode     = @vendorcode,
                                totalqty       = @totalqty,
                                receivedqty    = @receivedqty,
                                grossamount    = @grossamount,
                                discountamount = @discountamount,
                                taxamount      = @taxamount,
                                netamount      = @netamount,
                                paymentmode    = @paymentmode,
                                paymentstatus  = @paymentstatus,
                                remarks        = @remarks,
                                modifieddate   = CURRENT_TIMESTAMP
                            WHERE purchaseentrycode = @purchaseentrycode
                              AND tenantcode = @tenantcode;",
                            request.master, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = entryId }, transaction);

                        foreach (var item in request.details)
                        {
                            item.purchaseentrycode = entryId;

                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_entry_detail
                                (purchaseentrycode, itemcode, receivedqty, rate, amount, totalamount, tenantcode)
                                VALUES
                                (@purchaseentrycode, @itemcode, @receivedqty, @rate, @amount, @totalamount, @tenantcode);",
                                item, transaction);
                        }

                        await db.ExecuteAsync(@"
                            UPDATE purchase_master
                            SET
                                billno         = @billno,
                                billdate       = @billdate,
                                invoiceno      = @invoiceno,
                                invoicedate    = @invoicedate,
                                vendorcode     = @vendorcode,
                                grossamount    = @grossamount,
                                discountamount = @discountamount,
                                taxamount      = @taxamount,
                                netamount      = @netamount,
                                paymentmode    = @paymentmode,
                                paymentstatus  = @paymentstatus,
                                remarks        = @remarks,
                                modifieddate   = CURRENT_TIMESTAMP
                            WHERE grncode = @purchaseentrycode
                              AND tenantcode = @tenantcode;",
                            request.master, transaction);

                        var purchasecode = await db.ExecuteScalarAsync<long>(
                            "SELECT purchasecode FROM purchase_master WHERE grncode = @id AND tenantcode = @tenantcode",
                            new { id = entryId, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_detail WHERE purchasecode = @pc",
                            new { pc = purchasecode }, transaction);

                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_detail
                                (purchasecode, itemcode, quantity, rate, totalamount, tenantcode)
                                VALUES
                                (@purchasecode, @itemcode, @qty, @rate, @totalamount, @tenantcode);",
                                new
                                {
                                    purchasecode,
                                    item.itemcode,
                                    qty = item.receivedqty,
                                    item.rate,
                                    item.totalamount,
                                    item.tenantcode
                                },
                                transaction);
                        }

                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                                UPDATE stock_master
                                SET purchasedqty = purchasedqty + @qty,
                                    closingstock = closingstock + @qty
                                WHERE itemcode = @itemcode
                                  AND tenantcode = @tenantcode;",
                                new { qty = item.receivedqty, item.itemcode, item.tenantcode }, transaction);
                        }

                        transaction.Commit();
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Update failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<string> DeletePurchaseEntry(long purchaseentrycode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var items = await db.QueryAsync<purchase_entry_detail>(
                            "SELECT * FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = purchaseentrycode }, transaction);

                        foreach (var item in items)
                        {
                            await db.ExecuteAsync(@"
                                UPDATE stock_master
                                SET purchasedqty = purchasedqty - @qty,
                                    closingstock = closingstock - @qty
                                WHERE itemcode = @itemcode
                                  AND tenantcode = @tenantcode;",
                                new { qty = item.receivedqty, item.itemcode, tenantcode }, transaction);
                        }

                        var purchasecode = await db.ExecuteScalarAsync<long>(
                            "SELECT purchasecode FROM purchase_master WHERE grncode = @id AND tenantcode = @tenantcode",
                            new { id = purchaseentrycode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_detail WHERE purchasecode = @pc",
                            new { pc = purchasecode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_master WHERE purchasecode = @pc AND tenantcode = @tenantcode",
                            new { pc = purchasecode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = purchaseentrycode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_master WHERE purchaseentrycode = @id AND tenantcode = @tenantcode",
                            new { id = purchaseentrycode, tenantcode }, transaction);

                        transaction.Commit();
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Delete failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<List<purchase_entry_request>> GetAllPurchaseEntries(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var masters = (await db.QueryAsync<purchase_entry_master>(@"
        SELECT 
            purchaseentrycode,
            grnno,
            grndate,
            receivedby,
            billno,
            TO_CHAR(billdate, 'YYYY-MM-DD') AS billdate,
            TO_CHAR(invoicedate, 'YYYY-MM-DD') AS invoicedate,
            vendorcode,
            totalqty,
            receivedqty,
            grossamount,
            discountamount,
            taxamount,
            othercharges,
            netamount,
            paymentmode,
            paymentstatus,
            approvalstatus,
            posted,
            remarks,
            isactive,
            deleted,
            usercode,
            tenantcode,
            branchcode,
            companycode
        FROM public.purchase_entry_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY purchaseentrycode DESC;",
                new { tenantcode })).ToList();

            var details = (await db.QueryAsync<purchase_entry_detail>(@"
        SELECT 
            purchaseentrydetailcode,
            purchaseentrycode,
            itemcode,
            orderedqty,
            receivedqty,
            rejectedqty,
            quantity,
            rate,
            discountpercentage,
            discountamount,
            taxpercentage,
            taxamount,
            amount,
            totalamount,
            batchno,
            TO_CHAR(manufacturingdate, 'YYYY-MM-DD') AS manufacturingdate,
            TO_CHAR(expirydate, 'YYYY-MM-DD') AS expirydate,
            warehousecode,
            tenantcode
        FROM public.purchase_entry_detail
        WHERE tenantcode = @tenantcode;",
                new { tenantcode })).ToList();

            var result = masters.Select(m => new purchase_entry_request
            {
                master = m,
                details = details
                    .Where(d => d.purchaseentrycode == m.purchaseentrycode)
                    .ToList()
            }).ToList();

            return result;
        }
        public async Task<purchase_entry_request?> GetPurchaseEntryByCode(long purchaseentrycode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<purchase_entry_master>(@"
        SELECT 
            purchaseentrycode,
            grnno,
            grndate,
            receivedby,
            billno,
            TO_CHAR(billdate, 'YYYY-MM-DD') AS billdate,
            TO_CHAR(invoicedate, 'YYYY-MM-DD') AS invoicedate,
            vendorcode,
            totalqty,
            receivedqty,
            grossamount,
            discountamount,
            taxamount,
            othercharges,
            netamount,
            paymentmode,
            paymentstatus,
            approvalstatus,
            posted,
            remarks,
            isactive,
            deleted,
            TO_CHAR(createddate, 'YYYY-MM-DD') AS createddate,
            TO_CHAR(modifieddate, 'YYYY-MM-DD') AS modifieddate,
            usercode,
            tenantcode,
            branchcode,
            companycode
        FROM public.purchase_entry_master
        WHERE purchaseentrycode = @purchaseentrycode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { purchaseentrycode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<purchase_entry_detail>(@"
        SELECT 
            purchaseentrydetailcode,
            purchaseentrycode,
            itemcode,
            orderedqty,
            receivedqty,
            rejectedqty,
            quantity,
            rate,
            discountpercentage,
            discountamount,
            taxpercentage,
            taxamount,
            amount,
            totalamount,
            batchno,
            TO_CHAR(manufacturingdate, 'YYYY-MM-DD') AS manufacturingdate,
            TO_CHAR(expirydate, 'YYYY-MM-DD') AS expirydate,
            warehousecode,
            tenantcode
        FROM public.purchase_entry_detail
        WHERE purchaseentrycode = @purchaseentrycode;",
                new { purchaseentrycode });

            return new purchase_entry_request
            {
                master = master,
                details = details.ToList()
            };
        }


        // ─── EXCEL BULK UPLOAD ────────────────────────────────────────────────────────

        // tenantcode parameter added — passed from controller header
        public async Task ProcessExcel(string filePath, string tenantcode)
        {
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                int rowCount = worksheet.Dimension.Rows;

                var items = new List<item_master>();

                for (int row = 2; row <= rowCount; row++)
                {
                    var item = new item_master
                    {
                        itemname = worksheet.Cells[row, 1].Text,
                        shortname = worksheet.Cells[row, 2].Text,
                        description = worksheet.Cells[row, 3].Text,
                        categorycode = int.Parse(worksheet.Cells[row, 4].Text),
                        subcategorycode = int.Parse(worksheet.Cells[row, 5].Text),
                        purchaserate = decimal.Parse(worksheet.Cells[row, 6].Text),
                        tenantcode = tenantcode   // set from header
                    };

                    items.Add(item);
                }

                await InsertBulk(items, tenantcode);
            }
        }

        public async Task InsertBulk(List<item_master> items, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
                    INSERT INTO item_master
                    (itemname, shortname, description, categorycode, subcategorycode, purchaserate, tenantcode)
                    VALUES
                    (@itemname, @shortname, @description, @categorycode, @subcategorycode, @purchaserate, @tenantcode)";

                foreach (var item in items)
                {
                    item.tenantcode = tenantcode; // ensure every row carries the correct tenant
                    await db.ExecuteAsync(query, item);
                }
            }
        }
        public async Task<long> InsertCategory(category_master category)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    long categorycode = await db.ExecuteScalarAsync<long>(
                        "SELECT COALESCE(MAX(categorycode),0)+1 FROM category_master");

                    category.categorycode = categorycode;

                    string query = @"
                    INSERT INTO category_master
                    (
                        categorycode,
                        categoryname,
                        shortname,
                        description,
                        parentcategorycode,
                        isactive,
                        deleted,
                        createddate,
                        usercode,
                        tenantcode
                    )
                    VALUES
                    (
                        @categorycode,
                        @categoryname,
                        @shortname,
                        @description,
                        @parentcategorycode,
                        @isactive,
                        @deleted,
                        CURRENT_TIMESTAMP,
                        @usercode,
                        @tenantcode
                    )";

                    await db.ExecuteAsync(query, category);

                    return categorycode;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Insert failed : " + ex.Message);
            }
        }

        // GET ALL CATEGORIES
        public async Task<IEnumerable<category_master>> GetCategories(string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM category_master
                    WHERE tenantcode = @tenantcode
                    AND deleted = false
                    ORDER BY categorycode";

                    return await db.QueryAsync<category_master>(
                        query,
                        new { tenantcode });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get failed : " + ex.Message);
            }
        }

        // GET CATEGORY BY ID
        public async Task<category_master?> GetCategoryById(
            long categorycode,
            string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM category_master
                    WHERE categorycode = @categorycode
                    AND tenantcode = @tenantcode
                    AND deleted = false";

                    return await db.QueryFirstOrDefaultAsync<category_master>(
                        query,
                        new { categorycode, tenantcode });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get by id failed : " + ex.Message);
            }
        }

        // UPDATE CATEGORY
        public async Task<bool> UpdateCategory(category_master category)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE category_master
                    SET
                        categoryname = @categoryname,
                        shortname = @shortname,
                        description = @description,
                        parentcategorycode = @parentcategorycode,
                        isactive = @isactive,
                        deleted = @deleted,
                        usercode = @usercode,
                        tenantcode = @tenantcode
                    WHERE categorycode = @categorycode
                    AND tenantcode = @tenantcode";

                    int rows = await db.ExecuteAsync(query, category);

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Update failed : " + ex.Message);
            }
        }

        // DELETE CATEGORY
        public async Task<bool> DeleteCategory(
            long categorycode,
            string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE category_master
                    SET
                        deleted = true,
                        isactive = false
                    WHERE categorycode = @categorycode
                    AND tenantcode = @tenantcode";

                    int rows = await db.ExecuteAsync(
                        query,
                        new { categorycode, tenantcode });

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Delete failed : " + ex.Message);
            }
        }
        // INSERT
        public async Task<long> InsertUom(uom_master model)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                    INSERT INTO uom_master
                    (
                        orderno,
                        name,
                        shortname,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate,
                        packsize,
                        tenant_code
                    )
                    VALUES
                    (
                        @orderno,
                        @name,
                        @shortname,
                        @description,
                        @deleted,
                        @usercode,
                        @computercode,
                        CURRENT_TIMESTAMP,
                        CURRENT_TIMESTAMP,
                        @packsize,
                        @tenant_code
                    )
                    RETURNING ucode;";

                    return await db.ExecuteScalarAsync<long>(query, model);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Insert failed : " + ex.Message);
            }
        }

        // GET ALL
        public async Task<IEnumerable<uom_master>> GetAllUom(string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM uom_master
                    WHERE deleted = false
                    AND tenant_code = @tenant_code
                    ORDER BY ucode DESC";

                    return await db.QueryAsync<uom_master>(query, new { tenant_code });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get failed : " + ex.Message);
            }
        }

        // GET BY ID
        public async Task<uom_master?> GetUomByCode(long ucode, string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM uom_master
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                    return await db.QueryFirstOrDefaultAsync<uom_master>(
                        query,
                        new { ucode, tenant_code });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get by code failed : " + ex.Message);
            }
        }

        // UPDATE
        public async Task<bool> UpdateUom(uom_master model)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE uom_master
                    SET
                        orderno = @orderno,
                        name = @name,
                        shortname = @shortname,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        packsize = @packsize,
                        tenant_code = @tenant_code
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code";

                    int rows = await db.ExecuteAsync(query, model);

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Update failed : " + ex.Message);
            }
        }

        // DELETE
        public async Task<bool> DeleteUom(long ucode, string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE uom_master
                    SET deleted = true
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code";

                    int rows = await db.ExecuteAsync(
                        query,
                        new { ucode, tenant_code });

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Delete failed : " + ex.Message);
            }
        }
        public async Task<string> InsertParentCategory(parent_category_master model)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        INSERT INTO parent_category_master
        (
            parentcategoryname,
            shortname,
            description,
            isactive,
            deleted,
            createddate,
            tenantcode
        )
        VALUES
        (
            @parentcategoryname,
            @shortname,
            @description,
            @isactive,
            @deleted,
            CURRENT_TIMESTAMP,
            @tenantcode
        )";

                await db.ExecuteAsync(query, model);

                return "Inserted Successfully";
            }
        }

        public async Task<IEnumerable<parent_category_master>> GetParentCategories(string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        SELECT *
        FROM parent_category_master
        WHERE deleted = false
        AND tenantcode = @tenantcode
        ORDER BY parentcategorycode";

                return await db.QueryAsync<parent_category_master>(
                    query,
                    new { tenantcode });
            }
        }

        public async Task<string> UpdateParentCategory(parent_category_master model)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        UPDATE parent_category_master
        SET
            parentcategoryname = @parentcategoryname,
            shortname = @shortname,
            description = @description,
            isactive = @isactive,
            deleted = @deleted
        WHERE parentcategorycode = @parentcategorycode
        AND tenantcode = @tenantcode";

                await db.ExecuteAsync(query, model);

                return "Updated Successfully";
            }
        }

        public async Task<string> DeleteParentCategory(int parentcategorycode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        UPDATE parent_category_master
        SET deleted = true
        WHERE parentcategorycode = @parentcategorycode
        AND tenantcode = @tenantcode";

                await db.ExecuteAsync(query,
                    new { parentcategorycode, tenantcode });

                return "Deleted Successfully";
            }
        }
        public async Task<string> InsertLedger(ledger_master model)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
            INSERT INTO ledger_master
            (
                ledgername, lcode, ldgcode,
                taxtype, taxsubtype, taxpercentage,
                gstpercentage, hsncode,
                isactive, deleted, createddate,
                tenantcode
            )
            VALUES
            (
                @ledgername, @lcode, @ldgcode,
                @taxtype, @taxsubtype, @taxpercentage,
                @gstpercentage, @hsncode,
                @isactive, @deleted, CURRENT_TIMESTAMP,
                @tenantcode
            )
            RETURNING ledgercode;";

                await db.ExecuteScalarAsync<long>(query, model);
                return "Inserted Successfully";
            }
        }

        public async Task<IEnumerable<ledger_master>> GetLedger(string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
            SELECT * FROM ledger_master
            WHERE deleted = false
            AND tenantcode = @tenantcode
            ORDER BY ledgercode";

                return await db.QueryAsync<ledger_master>(query, new { tenantcode });
            }
        }

        public async Task<string> UpdateLedger(ledger_master model)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                UPDATE ledger_master
                SET
                    ledgername    = @ledgername,
                    lcode         = @lcode,
                    ldgcode       = @ldgcode,
                    taxtype       = @taxtype,
                    taxsubtype    = @taxsubtype,
                    taxpercentage = @taxpercentage,
                    gstpercentage = @gstpercentage,
                    hsncode       = @hsncode,
                    isactive      = @isactive,
                    deleted       = @deleted
                WHERE ledgercode = @ledgercode
                AND tenantcode = @tenantcode";

                    int rows = await db.ExecuteAsync(query, model);
                    return rows > 0 ? "Updated Successfully" : "Failed";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<bool> DeleteLedger(int ledgercode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
            UPDATE ledger_master
            SET deleted = true, isactive = false
            WHERE ledgercode = @ledgercode
            AND tenantcode = @tenantcode";

                var rows = await db.ExecuteAsync(query, new { ledgercode, tenantcode });
                return rows > 0;
            }
        }
        // INSERT LEDGER TYPE
        public async Task<string> InsertLedgerType(ledger_type_master ledger)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(con);
                ledger.ledgertypecode = null;
                ledger.createddate = DateTime.UtcNow;
                ledger.deleted = false;
                ledger.isactive = true;

                await db.InsertAsync(ledger);
                return "Inserted Successfully";
            }
            catch (Exception ex)
            {
                Console.WriteLine("InsertLedgerType ERROR: " + ex.Message);
                return ex.Message;
            }
        }


        // UPDATE LEDGER TYPE
        public async Task<string> UpdateLedgerType(ledger_type_master ledger)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_type_master
        SET
            ledgertypename = @ledgertypename,
            shortname = @shortname,
            description = @description,
            naturetype = @naturetype,
            isactive = @isactive,
            tenantcode = @tenantcode,
            isgstapplicable = @isgstapplicable,
            isvatapplicable = @isvatapplicable,
            sgstpercentage = @sgstpercentage,
            cgstpercentage = @cgstpercentage,
            igstpercentage = @igstpercentage
        WHERE ledgertypecode = @ledgertypecode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgertypecode", ledger.ledgertypecode);
                    cmd.Parameters.AddWithValue("@ledgertypename", ledger.ledgertypename);
                    cmd.Parameters.AddWithValue("@shortname", ledger.shortname);
                    cmd.Parameters.AddWithValue("@description", ledger.description ?? "");
                    cmd.Parameters.AddWithValue("@naturetype", ledger.naturetype);
                    cmd.Parameters.AddWithValue("@isactive", ledger.isactive);
                    cmd.Parameters.AddWithValue("@tenantcode", ledger.tenantcode);
                    cmd.Parameters.AddWithValue("@isgstapplicable", ledger.isgstapplicable);
                    cmd.Parameters.AddWithValue("@isvatapplicable", ledger.isvatapplicable);
                    cmd.Parameters.AddWithValue("@sgstpercentage", ledger.sgstpercentage);
                    cmd.Parameters.AddWithValue("@cgstpercentage", ledger.cgstpercentage);
                    cmd.Parameters.AddWithValue("@igstpercentage", ledger.igstpercentage);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows == 0)
                        return "Ledger Type not found";
                }
            }

            return "Updated Successfully";
        }


        // DELETE LEDGER TYPE
        public async Task<bool> DeleteLedgerType(int ledgertypecode, string tenantcode)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_type_master
        SET deleted = true
        WHERE ledgertypecode = @ledgertypecode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgertypecode", ledgertypecode);
                    cmd.Parameters.AddWithValue("@tenantcode", tenantcode);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    return rows > 0;
                }
            }
        }



        // INSERT LEDGER GROUP
        public async Task<string> InsertLedgerGroup(ledger_group_master ledger)
        {
            try
            {
                using (var conn = new NpgsqlConnection(con))
                {
                    await conn.OpenAsync();
                    ledger.ledgergroupcode = null;
                    await conn.InsertAsync(ledger);

                    return "Inserted Successfully";
                }
            }catch(Exception ex)
            {
                return ex.Message.ToString();
            }
        }



        // UPDATE LEDGER GROUP
        public async Task<string> UpdateLedgerGroup(ledger_group_master ledger)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_group_master
        SET
            ledgergroupname = @ledgergroupname,
            shortname = @shortname,
            ledgertypecode = @ledgertypecode,
            description = @description,
            isactive = @isactive,
            tenantcode = @tenantcode
        WHERE ledgergroupcode = @ledgergroupcode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgergroupcode", ledger.ledgergroupcode);
                    cmd.Parameters.AddWithValue("@ledgergroupname", ledger.ledgergroupname);
                    cmd.Parameters.AddWithValue("@shortname", ledger.shortname);
                    cmd.Parameters.AddWithValue("@ledgertypecode", ledger.ledgertypecode);
                    cmd.Parameters.AddWithValue("@description", ledger.description ?? "");
                    cmd.Parameters.AddWithValue("@isactive", ledger.isactive);
                    cmd.Parameters.AddWithValue("@tenantcode", ledger.tenantcode);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows == 0)
                        return "Ledger Group not found";
                }
            }

            return "Updated Successfully";
        }



        // DELETE LEDGER GROUP
        public async Task<bool> DeleteLedgerGroup(int ledgergroupcode, string tenantcode)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_group_master
        SET deleted = true
        WHERE ledgergroupcode = @ledgergroupcode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgergroupcode", ledgergroupcode);
                    cmd.Parameters.AddWithValue("@tenantcode", tenantcode);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    return rows > 0;
                }
            }
        }
        public async Task<List<ledger_type_master>> GetLedgerTypes()
{
    List<ledger_type_master> list = new List<ledger_type_master>();

    using (var conn = new NpgsqlConnection(con))
    {
        await conn.OpenAsync();

        string query = @"SELECT * FROM ledger_type_master 
                         WHERE deleted = false
                         ORDER BY ledgertypecode";

        using (var cmd = new NpgsqlCommand(query, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new ledger_type_master
                {
                    ledgertypecode = Convert.ToInt32(reader["ledgertypecode"]),
                    ledgertypename = reader["ledgertypename"].ToString(),
                    shortname = reader["shortname"].ToString(),
                    description = reader["description"].ToString(),
                    naturetype = Convert.ToInt32(reader["naturetype"]),
                    isactive = Convert.ToBoolean(reader["isactive"]),
                    createddate = Convert.ToDateTime(reader["createddate"]),
                    tenantcode = reader["tenantcode"].ToString(),
                    isgstapplicable = Convert.ToBoolean(reader["isgstapplicable"]),
                    isvatapplicable = Convert.ToBoolean(reader["isvatapplicable"]),
                    sgstpercentage = Convert.ToDecimal(reader["sgstpercentage"]),
                    cgstpercentage = Convert.ToDecimal(reader["cgstpercentage"]),
                    igstpercentage = Convert.ToDecimal(reader["igstpercentage"]),
                    deleted = Convert.ToBoolean(reader["deleted"])
                });
            }
        }
    }

    return list;
}

// GET ALL
public async Task<List<ledger_group_master>> GetLedgerGroups()
{
    List<ledger_group_master> list = new List<ledger_group_master>();

    using (var conn = new NpgsqlConnection(con))
    {
        await conn.OpenAsync();

        string query = @"SELECT * FROM ledger_group_master 
                         WHERE deleted = false
                         ORDER BY ledgergroupcode";

        using (var cmd = new NpgsqlCommand(query, conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new ledger_group_master
                {
                    ledgergroupcode = Convert.ToInt32(reader["ledgergroupcode"]),
                    ledgergroupname = reader["ledgergroupname"].ToString(),
                    shortname = reader["shortname"].ToString(),
                    ledgertypecode = Convert.ToInt32(reader["ledgertypecode"]),
                    description = reader["description"].ToString(),
                    isactive = Convert.ToBoolean(reader["isactive"]),
                    createddate = Convert.ToDateTime(reader["createddate"]),
                    tenantcode = reader["tenantcode"].ToString(),
                    deleted = Convert.ToBoolean(reader["deleted"])
                });
            }
        }
    }

    return list;
}
            public async Task<string> InsertSales(sales_request request)
    {
        using (var conn = new NpgsqlConnection(con))
        {
            await conn.OpenAsync();

            using (var trans = await conn.BeginTransactionAsync())
            {
                try
                {
                    string masterQuery = @"
            INSERT INTO sales_master
            (
                salescode,
                billno,
                billdate,
                invoiceno,
                invoicedate,
                customercode,
                grossamount,
                discountamount,
                taxamount,
                netamount,
                paymentmode,
                paymentstatus,
                currencycode,
                isactive,
                deleted,
                remarks,
                createddate,
                modifieddate,
                usercode,
                tenantcode,
                branchcode,
                companycode,
                ordercode
            )
            VALUES
            (
                @salescode,
                @billno,
                @billdate,
                @invoiceno,
                @invoicedate,
                @customercode,
                @grossamount,
                @discountamount,
                @taxamount,
                @netamount,
                @paymentmode,
                @paymentstatus,
                @currencycode,
                @isactive,
                @deleted,
                @remarks,
                @createddate,
                @modifieddate,
                @usercode,
                @tenantcode,
                @branchcode,
                @companycode,
                @ordercode
            );";

                    await conn.ExecuteAsync(
                        masterQuery,
                        request.master,
                        trans
                    );

                    string detailQuery = @"
            INSERT INTO sales_detail
            (
                salesdetailcode,
                salescode,
                itemcode,
                quantity,
                freequantity,
                uomcode,
                rate,
                discountpercentage,
                discountamount,
                taxpercentage,
                taxamount,
                amount,
                totalamount,
                batchno,
                manufacturingdate,
                expirydate,
                orderedqty,
                deliveredqty,
                returnedqty,
                warehousecode,
                tenantcode
            )
            VALUES
            (
                @salesdetailcode,
                @salescode,
                @itemcode,
                @quantity,
                @freequantity,
                @uomcode,
                @rate,
                @discountpercentage,
                @discountamount,
                @taxpercentage,
                @taxamount,
                @amount,
                @totalamount,
                @batchno,
                @manufacturingdate,
                @expirydate,
                @orderedqty,
                @deliveredqty,
                @returnedqty,
                @warehousecode,
                @tenantcode
            );";

                    foreach (var item in request.details)
                    {
                        item.salescode = request.master.salescode;

                        await conn.ExecuteAsync(
                            detailQuery,
                            item,
                            trans
                        );
                    }

                    await trans.CommitAsync();

                    return "Sales Inserted Successfully";
                }
                catch (Exception ex)
                {
                    await trans.RollbackAsync();

                    return $"Error : {ex.Message}";
                }
            }
        }
    
}
    
    public async Task<string> UpdateSales(sales_request request)
    {
        using (var conn = new NpgsqlConnection(con))
        {
            await conn.OpenAsync();

            using (var trans = await conn.BeginTransactionAsync())
            {
                try
                {
                    string updateMaster = @"
            UPDATE sales_master SET
                billno=@billno,
                billdate=@billdate,
                invoiceno=@invoiceno,
                invoicedate=@invoicedate,
                customercode=@customercode,
                grossamount=@grossamount,
                discountamount=@discountamount,
                taxamount=@taxamount,
                netamount=@netamount,
                paymentmode=@paymentmode,
                paymentstatus=@paymentstatus,
                currencycode=@currencycode,
                remarks=@remarks,
                modifieddate=NOW(),
                usercode=@usercode,
                tenantcode=@tenantcode,
                branchcode=@branchcode,
                companycode=@companycode,
                ordercode=@ordercode
            WHERE salescode=@salescode";

                    await conn.ExecuteAsync(updateMaster, request.master, trans);

                    await conn.ExecuteAsync(
                        "DELETE FROM sales_detail WHERE salescode=@salescode",
                        new { request.master.salescode },
                        trans);

                    string detailQuery = @"
            INSERT INTO sales_detail
            (
                salesdetailcode,salescode,itemcode,quantity,
                freequantity,uomcode,rate,
                discountpercentage,discountamount,
                taxpercentage,taxamount,
                amount,totalamount,batchno,
                manufacturingdate,expirydate,
                orderedqty,deliveredqty,
                returnedqty,warehousecode,
                tenantcode
            )
            VALUES
            (
                @salesdetailcode,@salescode,@itemcode,@quantity,
                @freequantity,@uomcode,@rate,
                @discountpercentage,@discountamount,
                @taxpercentage,@taxamount,
                @amount,@totalamount,@batchno,
                @manufacturingdate,@expirydate,
                @orderedqty,@deliveredqty,
                @returnedqty,@warehousecode,
                @tenantcode
            )";

                    foreach (var item in request.details)
                    {
                        item.salescode = request.master.salescode;
                        await conn.ExecuteAsync(detailQuery, item, trans);
                    }

                    await trans.CommitAsync();

                    return "Sales Updated Successfully";
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
        }
    }
    public async Task<IEnumerable<sales_master>> GetSales()
    {
        using (var conn = new NpgsqlConnection(con))
        {
            string query = @"
    SELECT *
    FROM sales_master
    WHERE deleted = false
    ORDER BY salescode DESC";

            return await conn.QueryAsync<sales_master>(query);
        }
    }
    public async Task<string> DeleteSales(long salescode)
    {
        using (var conn = new NpgsqlConnection(con))
        {
            await conn.OpenAsync();

            string query = @"
    UPDATE sales_master
    SET deleted = true,
        isactive = false,
        modifieddate = NOW()
    WHERE salescode = @salescode";

            await conn.ExecuteAsync(query, new { salescode });

            return "Sales Deleted Successfully";
            }
        }
        public async Task<string> UpsertWarehouse(warehouse_master warehouse)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
      INSERT INTO warehouse_master
      (
          orderno,
          warehousename,
          shortname,
          description,
          location,
          tenantcode,
          isactive,
          isdeleted,
          createddate
      )
      VALUES
      (
          @orderno,
          @warehousename,
          @shortname,
          @description,
          @location,
          @tenantcode,
          @isactive,
          @isdeleted,
          @createddate
      )
      ON CONFLICT (warehousecode)
      DO UPDATE SET
          warehousename = EXCLUDED.warehousename,
         
          shortname = EXCLUDED.shortname,
          description = EXCLUDED.description,
          location = EXCLUDED.location,
          tenantcode = EXCLUDED.tenantcode,
          isactive = EXCLUDED.isactive,
          isdeleted = EXCLUDED.isdeleted;";

                    await db.ExecuteAsync(query, warehouse);

                    return "Warehouse Upserted Successfully";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> InsertOrUpdateWarehouse(warehouse_master warehouse)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(con);

                var existing = await db.GetAsync<warehouse_master>(warehouse.warehousecode);

                if (existing == null)
                {
                    await db.InsertAsync(warehouse);
                    return "Warehouse Inserted Successfully";
                }
                else
                {
                    await db.UpdateAsync(warehouse);
                    return "Warehouse Updated Successfully";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteWarehouse(int warehousecode)
  {
      try
      {
          using (IDbConnection db = new NpgsqlConnection(con))
          {
              string query = @"UPDATE warehouse_master
                       SET isdeleted = true
                       WHERE warehousecode = @warehousecode";

              await db.ExecuteAsync(query, new { warehousecode });

              return "Warehouse Deleted Successfully";
          }
      }
      catch (Exception ex)
      {
          return ex.Message;
      }
  }
  public async Task<IEnumerable<warehouse_master>> GetWarehouseList()
  {
      try
      {
          using (IDbConnection db = new NpgsqlConnection(con))
          {
              string query = @"SELECT *
                       FROM warehouse_master
                       WHERE isdeleted = false
                       ORDER BY warehousecode";

              return await db.QueryAsync<warehouse_master>(query);
          }
      }
      catch (Exception)
      {
          throw;
      }
  }
        public async Task<string> UpsertManufacturer(manufacturer_master manufacturer)
   {
       try
       {
           using (IDbConnection db = new NpgsqlConnection(con))
           {
               string query;

               if (manufacturer.manufacturercode == 0)
               {
                   query = @"
           INSERT INTO manufacturer_master
           (
               manufacturername,
               shortname,
               description,
               contactperson,
               phoneno,
               email,
               address,
               gstno,
               isactive,
               deleted,
               createddate,
               usercode,
               tenantcode
           )
           VALUES
           (
               @manufacturername,
               @shortname,
               @description,
               @contactperson,
               @phoneno,
               @email,
               @address,
               @gstno,
               @isactive,
               @deleted,
               @createddate,
               @usercode,
               @tenantcode
           );";

                   await db.ExecuteAsync(query, manufacturer);

                   return "Manufacturer Created Successfully";
               }
               else
               {
                   query = @"
           UPDATE manufacturer_master
           SET
               manufacturername = @manufacturername,
               shortname = @shortname,
               description = @description,
               contactperson = @contactperson,
               phoneno = @phoneno,
               email = @email,
               address = @address,
               gstno = @gstno,
               isactive = @isactive,
               usercode = @usercode,
               tenantcode = @tenantcode
           WHERE manufacturercode = @manufacturercode;";

                   await db.ExecuteAsync(query, manufacturer);

                   return "Manufacturer Updated Successfully";
               }
           }
       }
       catch (Exception ex)
       {
           return ex.Message;
       }
   }
 public async Task<IEnumerable<manufacturer_master>> GetManufacturerList()
 {
     try
     {
         using (IDbConnection db = new NpgsqlConnection(con))
         {
             string query = @"
     SELECT *
     FROM manufacturer_master
     WHERE deleted = false
     ORDER BY manufacturercode";

             return await db.QueryAsync<manufacturer_master>(query);
         }
     }
     catch (Exception)
     {
         throw;
     }
 }
 public async Task<string> DeleteManufacturer(long manufacturercode)
 {
     try
     {
         using (IDbConnection db = new NpgsqlConnection(con))
         {
             string query = @"
     UPDATE manufacturer_master
     SET deleted = true
     WHERE manufacturercode = @manufacturercode";

             await db.ExecuteAsync(query, new { manufacturercode });

             return "Manufacturer Deleted Successfully";
         }
     }
     catch (Exception ex)
     {
         return ex.Message;
     }
 }
    }
}
    

