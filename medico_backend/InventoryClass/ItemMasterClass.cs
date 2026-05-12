using medico_backend.InventoryModel;
using Npgsql;
using System.Data;
using OfficeOpenXml;
using Dapper;

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

        public async Task<long> InsertItem(item_master item)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query;

                    if (item.itemcode == 0)
                    {
                        query = @"
                            INSERT INTO item_master
                            (
                                itemname, shortname, description,
                                categorycode, subcategorycode, itemtype,
                                uomcode, purchaserate, salesrate,
                                isactive, deleted, createddate,
                                usercode, tenantcode
                            )
                            VALUES
                            (
                                @itemname, @shortname, @description,
                                @categorycode, @subcategorycode, @itemtype,
                                @uomcode, @purchaserate, @salesrate,
                                @isactive, @deleted, CURRENT_TIMESTAMP,
                                @usercode, @tenantcode
                            )
                            RETURNING itemcode;";
                    }
                    else
                    {
                        query = @"
                            UPDATE item_master
                            SET
                                itemname        = @itemname,
                                shortname       = @shortname,
                                description     = @description,
                                categorycode    = @categorycode,
                                subcategorycode = @subcategorycode,
                                itemtype        = @itemtype,
                                uomcode         = @uomcode,
                                purchaserate    = @purchaserate,
                                salesrate       = @salesrate,
                                isactive        = @isactive,
                                deleted         = @deleted,
                                usercode        = @usercode,
                                tenantcode      = @tenantcode
                            WHERE itemcode = @itemcode
                            RETURNING itemcode;";
                    }

                    return await db.ExecuteScalarAsync<long>(query, item);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Upsert failed: " + ex.Message);
            }
        }

        public async Task<string> UpdateItem(item_master item)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.item_master
                        SET
                            itemname        = @itemname,
                            shortname       = @shortname,
                            description     = @description,
                            categorycode    = @categorycode,
                            subcategorycode = @subcategorycode,
                            itemtype        = @itemtype,
                            uomcode         = @uomcode,
                            purchaserate    = @purchaserate,
                            salesrate       = @salesrate,
                            isactive        = @isactive,
                            deleted         = @deleted,
                            usercode        = @usercode,
                            tenantcode      = @tenantcode
                        WHERE itemcode = @itemcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, item);
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Item update failed: " + ex.Message);
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

                    string query;

                    if (vendor.vendorcode == 0)
                    {
                        query = @"
                            INSERT INTO vendor_master
                            (
                                vendorname, shortname, vendortype,
                                contactperson, phonenumber, alternatephonenumber,
                                emailid, website, gstnumber, pannumber,
                                taxid, registrationnumber,
                                addressline1, addressline2, landmark,
                                city, district, state, postalcode,
                                countrycode, countryname, currencycode,
                                paymentterms, creditperiod,
                                bankname, accountnumber, ifsccode, swiftcode, ibannumber,
                                isactive, deleted, createddate,
                                usercode, tenantcode, branchcode
                            )
                            VALUES
                            (
                                @vendorname, @shortname, @vendortype,
                                @contactperson, @phonenumber, @alternatephonenumber,
                                @emailid, @website, @gstnumber, @pannumber,
                                @taxid, @registrationnumber,
                                @addressline1, @addressline2, @landmark,
                                @city, @district, @state, @postalcode,
                                @countrycode, @countryname, @currencycode,
                                @paymentterms, @creditperiod,
                                @bankname, @accountnumber, @ifsccode, @swiftcode, @ibannumber,
                                @isactive, @deleted, CURRENT_TIMESTAMP,
                                @usercode, @tenantcode, @branchcode
                            )
                            RETURNING vendorcode;";
                    }
                    else
                    {
                        query = @"
                            UPDATE vendor_master
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
                              AND tenantcode = @tenantcode
                            RETURNING vendorcode;";
                    }

                    return await db.ExecuteScalarAsync<long>(query, vendor);
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
                        string masterQuery = @"
                            INSERT INTO public.purchase_master
                            (
                                billno, billdate, invoiceno, invoicedate,
                                vendorcode,
                                grossamount, discountamount, taxamount, netamount,
                                paymentmode, paymentstatus, currencycode,
                                remarks, isactive, deleted,
                                createddate, usercode,
                                tenantcode, branchcode, companycode
                            )
                            VALUES
                            (
                                @billno, @billdate, @invoiceno, @invoicedate,
                                @vendorcode,
                                @grossamount, @discountamount, @taxamount, @netamount,
                                @paymentmode, @paymentstatus, @currencycode,
                                @remarks, @isactive, @deleted,
                                CURRENT_TIMESTAMP, @usercode,
                                @tenantcode, @branchcode, @companycode
                            )
                            RETURNING purchasecode;";

                        long purchasecode = await db.ExecuteScalarAsync<long>(masterQuery, request.master, transaction);

                        string detailQuery = @"
                            INSERT INTO public.purchase_detail
                            (
                                purchasecode, itemcode, quantity, freequantity,
                                uomcode, rate, discountpercentage, discountamount,
                                taxpercentage, taxamount, amount, totalamount,
                                batchno, manufacturingdate, expirydate, tenantcode
                            )
                            VALUES
                            (
                                @purchasecode, @itemcode, @quantity, @freequantity,
                                @uomcode, @rate, @discountpercentage, @discountamount,
                                @taxpercentage, @taxamount, @amount, @totalamount,
                                @batchno, @manufacturingdate, @expirydate, @tenantcode
                            );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = purchasecode;
                            await db.ExecuteAsync(detailQuery, item, transaction);
                        }

                        transaction.Commit();
                        return purchasecode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase insert failed: " + ex.Message);
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
                                billno          = @billno,
                                billdate        = @billdate,
                                invoiceno       = @invoiceno,
                                invoicedate     = @invoicedate,
                                vendorcode      = @vendorcode,
                                grossamount     = @grossamount,
                                discountamount  = @discountamount,
                                taxamount       = @taxamount,
                                netamount       = @netamount,
                                paymentmode     = @paymentmode,
                                paymentstatus   = @paymentstatus,
                                currencycode    = @currencycode,
                                remarks         = @remarks,
                                isactive        = @isactive,
                                deleted         = @deleted,
                                modifieddate    = CURRENT_TIMESTAMP,
                                usercode        = @usercode,
                                tenantcode      = @tenantcode,
                                branchcode      = @branchcode,
                                companycode     = @companycode
                            WHERE purchasecode = @purchasecode
                              AND tenantcode = @tenantcode;";

                        await db.ExecuteAsync(masterQuery, request.master, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM public.purchase_detail WHERE purchasecode = @purchasecode;",
                            new { purchasecode = request.master.purchasecode }, transaction);

                        string insertDetailQuery = @"
                            INSERT INTO public.purchase_detail
                            (
                                purchasecode, itemcode, quantity, freequantity,
                                uomcode, rate, discountpercentage, discountamount,
                                taxpercentage, taxamount, amount, totalamount,
                                batchno, manufacturingdate, expirydate, tenantcode
                            )
                            VALUES
                            (
                                @purchasecode, @itemcode, @quantity, @freequantity,
                                @uomcode, @rate, @discountpercentage, @discountamount,
                                @taxpercentage, @taxamount, @amount, @totalamount,
                                @batchno, @manufacturingdate, @expirydate, @tenantcode
                            );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = request.master.purchasecode;
                            await db.ExecuteAsync(insertDetailQuery, item, transaction);
                        }

                        transaction.Commit();
                        return request.master.purchasecode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase update failed: " + ex.Message);
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
                            SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
                            WHERE purchasecode = @purchasecode
                              AND tenantcode = @tenantcode;",
                            new { purchasecode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM public.purchase_detail WHERE purchasecode = @purchasecode;",
                            new { purchasecode }, transaction);

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

        public async Task<IEnumerable<purchase_master>> GetAllPurchases(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<purchase_master>(@"
                SELECT * FROM public.purchase_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY purchasecode DESC;",
                new { tenantcode });
        }

        public async Task<purchase_request?> GetPurchaseByCode(long purchasecode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<purchase_master>(@"
                SELECT * FROM public.purchase_master
                WHERE purchasecode = @purchasecode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { purchasecode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<purchase_detail>(@"
                SELECT * FROM public.purchase_detail
                WHERE purchasecode = @purchasecode;",
                new { purchasecode });

            return new purchase_request { master = master, details = details.ToList() };
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
                SELECT * FROM public.stock_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY stockcode DESC;",
                new { tenantcode });
        }

        public async Task<stock_master?> GetStockByCode(long stockcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryFirstOrDefaultAsync<stock_master>(@"
                SELECT * FROM public.stock_master
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { stockcode, tenantcode });
        }

        public async Task<IEnumerable<stock_master>> GetStockByItem(long itemcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<stock_master>(@"
                SELECT * FROM public.stock_master
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

        public async Task<IEnumerable<indent_master>> GetAllIndents(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<indent_master>(@"
                SELECT * FROM public.indent_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY indentcode DESC;",
                new { tenantcode });
        }

        public async Task<indent_request?> GetIndentByCode(long indentcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<indent_master>(@"
                SELECT * FROM public.indent_master
                WHERE indentcode = @indentcode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { indentcode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<indent_detail>(@"
                SELECT * FROM public.indent_detail WHERE indentcode = @indentcode;",
                new { indentcode });

            return new indent_request { master = master, details = details.ToList() };
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

        public async Task<IEnumerable<purchase_entry_master>> GetAllPurchaseEntries(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<purchase_entry_master>(@"
                SELECT * FROM public.purchase_entry_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY purchaseentrycode DESC;",
                new { tenantcode });
        }

        public async Task<purchase_entry_request?> GetPurchaseEntryByCode(long purchaseentrycode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<purchase_entry_master>(@"
                SELECT * FROM public.purchase_entry_master
                WHERE purchaseentrycode = @purchaseentrycode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { purchaseentrycode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<purchase_entry_detail>(@"
                SELECT * FROM public.purchase_entry_detail
                WHERE purchaseentrycode = @purchaseentrycode;",
                new { purchaseentrycode });

            return new purchase_entry_request { master = master, details = details.ToList() };
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
    }
}