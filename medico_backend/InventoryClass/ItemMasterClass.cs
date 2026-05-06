using medico_backend.InventoryModel;
using Npgsql;
using System.Data;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Data.SqlClient;
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



        public async Task<long> InsertItem(item_master item)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query;

                    // INSERT if new item
                    if (item.itemcode == 0)
                    {
                        query = @"
                    INSERT INTO item_master
                    (
                        itemname,
                        shortname,
                        description,
                        categorycode,
                        subcategorycode,
                        itemtype,
                        uomcode,
                        purchaserate,
                        salesrate,
                        isactive,
                        deleted,
                        createddate,
                        usercode
                    )
                    VALUES
                    (
                        @itemname,
                        @ShortName,
                        @Description,
                        @CategoryCode,
                        @SubCategoryCode,
                        @ItemType,
                        @UomCode,
                        @PurchaseRate,
                        @SalesRate,
                        @IsActive,
                        @Deleted,
                        CURRENT_TIMESTAMP,
                        @UserCode
                    )
                    RETURNING itemcode;";
                    }
                    else
                    {
                        // UPDATE existing item
                        query = @"
                    UPDATE item_master
                    SET
                        itemname = @itemname,
                        shortname = @ShortName,
                        description = @Description,
                        categorycode = @CategoryCode,
                        subcategorycode = @SubCategoryCode,
                        itemtype = @ItemType,
                        uomcode = @UomCode,
                        purchaserate = @PurchaseRate,
                        salesrate = @SalesRate,
                        isactive = @IsActive,
                        deleted = @Deleted,
                        usercode = @UserCode
                    WHERE itemcode = @ItemCode
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
        public async Task<long> UpsertVendor(vendor_master vendor)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query;

                    // INSERT
                    if (vendor.vendorcode == 0)
                    {
                        query = @"
                    INSERT INTO vendor_master
                    (
                        vendorname,
                        shortname,
                        vendortype,
                        contactperson,
                        phonenumber,
                        alternatephonenumber,
                        emailid,
                        website,
                        gstnumber,
                        pannumber,
                        taxid,
                        registrationnumber,
                        addressline1,
                        addressline2,
                        landmark,
                        city,
                        district,
                        state,
                        postalcode,
                        countrycode,
                        countryname,
                        currencycode,
                        paymentterms,
                        creditperiod,
                        bankname,
                        accountnumber,
                        ifsccode,
                        swiftcode,
                        ibannumber,
                        isactive,
                        deleted,
                        createddate,
                        usercode,
                        tenantcode,
                        branchcode
                    )
                    VALUES
                    (
                        @VendorName,
                        @ShortName,
                        @VendorType,
                        @ContactPerson,
                        @PhoneNumber,
                        @AlternatePhoneNumber,
                        @EmailId,
                        @Website,
                        @GstNumber,
                        @PanNumber,
                        @TaxId,
                        @RegistrationNumber,
                        @AddressLine1,
                        @AddressLine2,
                        @Landmark,
                        @City,
                        @District,
                        @State,
                        @PostalCode,
                        @CountryCode,
                        @CountryName,
                        @CurrencyCode,
                        @PaymentTerms,
                        @CreditPeriod,
                        @BankName,
                        @AccountNumber,
                        @IfscCode,
                        @SwiftCode,
                        @IbanNumber,
                        @IsActive,
                        @Deleted,
                        CURRENT_TIMESTAMP,
                        @UserCode,
                        @TenantCode,
                        @BranchCode
                    )
                    RETURNING vendorcode;";
                    }
                    else
                    {
                        // UPDATE
                        query = @"
                    UPDATE vendor_master
                    SET
                        vendorname = @VendorName,
                        shortname = @ShortName,
                        vendortype = @VendorType,
                        contactperson = @ContactPerson,
                        phonenumber = @PhoneNumber,
                        alternatephonenumber = @AlternatePhoneNumber,
                        emailid = @EmailId,
                        website = @Website,
                        gstnumber = @GstNumber,
                        pannumber = @PanNumber,
                        taxid = @TaxId,
                        registrationnumber = @RegistrationNumber,
                        addressline1 = @AddressLine1,
                        addressline2 = @AddressLine2,
                        landmark = @Landmark,
                        city = @City,
                        district = @District,
                        state = @State,
                        postalcode = @PostalCode,
                        countrycode = @CountryCode,
                        countryname = @CountryName,
                        currencycode = @CurrencyCode,
                        paymentterms = @PaymentTerms,
                        creditperiod = @CreditPeriod,
                        bankname = @BankName,
                        accountnumber = @AccountNumber,
                        ifsccode = @IfscCode,
                        swiftcode = @SwiftCode,
                        ibannumber = @IbanNumber,
                        isactive = @IsActive,
                        deleted = @Deleted,
                        modifieddate = CURRENT_TIMESTAMP,
                        usercode = @UserCode,
                        tenantcode = @TenantCode,
                        branchcode = @BranchCode
                    WHERE vendorcode = @VendorCode
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
                    vendorname = @vendorname,
                    shortname = @shortname,
                    vendortype = @vendortype,
                    contactperson = @contactperson,
                    phonenumber = @phonenumber,
                    alternatephonenumber = @alternatephonenumber,
                    emailid = @emailid,
                    website = @website,
                    gstnumber = @gstnumber,
                    pannumber = @pannumber,
                    taxid = @taxid,
                    registrationnumber = @registrationnumber,
                    addressline1 = @addressline1,
                    addressline2 = @addressline2,
                    landmark = @landmark,
                    city = @city,
                    district = @district,
                    state = @state,
                    postalcode = @postalcode,
                    countrycode = @countrycode,
                    countryname = @countryname,
                    currencycode = @currencycode,
                    paymentterms = @paymentterms,
                    creditperiod = @creditperiod,
                    bankname = @bankname,
                    accountnumber = @accountnumber,
                    ifsccode = @ifsccode,
                    swiftcode = @swiftcode,
                    ibannumber = @ibannumber,
                    isactive = @isactive,
                    deleted = @deleted,
                    modifieddate = CURRENT_TIMESTAMP,
                    usercode = @usercode,
                    tenantcode = @tenantcode,
                    branchcode = @branchcode
                WHERE vendorcode = @vendorcode;";

                    int rows = await db.ExecuteAsync(query, vendor);

                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor update failed: " + ex.Message);
            }
        }
        public async Task<string> DeleteVendor(long vendorcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                UPDATE public.vendor_master
                SET
                    deleted = true,
                    isactive = false,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE vendorcode = @vendorcode;";

                    int rows = await db.ExecuteAsync(query, new { vendorcode });

                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor delete failed: " + ex.Message);
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
                    itemname = @itemname,
                    shortname = @shortname,
                    description = @description,
                    categorycode = @categorycode,
                    subcategorycode = @subcategorycode,
                    itemtype = @itemtype,
                    uomcode = @uomcode,
                    purchaserate = @purchaserate,
                    salesrate = @salesrate,
                    isactive = @isactive,
                    deleted = @deleted,
                    usercode = @usercode,
                    tenantcode = @tenantcode
                WHERE itemcode = @itemcode;";

                    int rows = await db.ExecuteAsync(query, item);

                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Item update failed: " + ex.Message);
            }
        }
        public async Task<string> DeleteItem(long itemcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                UPDATE public.item_master
                SET
                    deleted = true,
                    isactive = false
                WHERE itemcode = @itemcode;";

                    int rows = await db.ExecuteAsync(query, new { itemcode });

                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Item delete failed: " + ex.Message);
            }
        }
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
                            transaction
                        );

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
                        @tenantcode
                    );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = purchasecode;

                            await db.ExecuteAsync(
                                detailQuery,
                                item,
                                transaction
                            );
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
                        // Update Master
                        string masterQuery = @"
                    UPDATE public.purchase_master
                    SET
                        billno = @billno,
                        billdate = @billdate,
                        invoiceno = @invoiceno,
                        invoicedate = @invoicedate,
                        vendorcode = @vendorcode,
                        grossamount = @grossamount,
                        discountamount = @discountamount,
                        taxamount = @taxamount,
                        netamount = @netamount,
                        paymentmode = @paymentmode,
                        paymentstatus = @paymentstatus,
                        currencycode = @currencycode,
                        remarks = @remarks,
                        isactive = @isactive,
                        deleted = @deleted,
                        modifieddate = CURRENT_TIMESTAMP,
                        usercode = @usercode,
                        tenantcode = @tenantcode,
                        branchcode = @branchcode,
                        companycode = @companycode
                    WHERE purchasecode = @purchasecode;";

                        await db.ExecuteAsync(
                            masterQuery,
                            request.master,
                            transaction
                        );

                        // Delete old details
                        string deleteDetailQuery = @"
                    DELETE FROM public.purchase_detail
                    WHERE purchasecode = @purchasecode;";

                        await db.ExecuteAsync(
                            deleteDetailQuery,
                            new { purchasecode = request.master.purchasecode },
                            transaction
                        );

                        // Insert new details
                        string insertDetailQuery = @"
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
                        @tenantcode
                    );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = request.master.purchasecode;

                            await db.ExecuteAsync(
                                insertDetailQuery,
                                item,
                                transaction
                            );
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
        public async Task<string> DeletePurchase(long purchasecode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // Soft delete master
                        string masterQuery = @"
                    UPDATE public.purchase_master
                    SET
                        deleted = true,
                        isactive = false,
                        modifieddate = CURRENT_TIMESTAMP
                    WHERE purchasecode = @purchasecode;";

                        int masterRows = await db.ExecuteAsync(
                            masterQuery,
                            new { purchasecode },
                            transaction
                        );

                        // Delete details
                        string detailQuery = @"
                    DELETE FROM public.purchase_detail
                    WHERE purchasecode = @purchasecode;";

                        await db.ExecuteAsync(
                            detailQuery,
                            new { purchasecode },
                            transaction
                        );

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
                    @locationcode,
                    @openingstock,
                    @purchasedqty,
                    @soldqty,
                    @damagedqty,
                    @returnqty,
                    @closingstock,
                    @unitcost,
                    @stockvalue,
                    @batchno,
                    @manufacturingdate,
                    @expirydate,
                    @isactive,
                    @deleted,
                    CURRENT_TIMESTAMP,
                    @usercode,
                    @tenantcode,
                    @companycode
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
                    itemcode = @itemcode,
                    warehousecode = @warehousecode,
                    branchcode = @branchcode,
                    locationcode = @locationcode,
                    openingstock = @openingstock,
                    purchasedqty = @purchasedqty,
                    soldqty = @soldqty,
                    damagedqty = @damagedqty,
                    returnqty = @returnqty,
                    closingstock = @closingstock,
                    unitcost = @unitcost,
                    stockvalue = @stockvalue,
                    batchno = @batchno,
                    manufacturingdate = @manufacturingdate,
                    expirydate = @expirydate,
                    isactive = @isactive,
                    deleted = @deleted,
                    modifieddate = CURRENT_TIMESTAMP,
                    usercode = @usercode,
                    tenantcode = @tenantcode,
                    companycode = @companycode
                WHERE stockcode = @stockcode;";

                    int rows = await db.ExecuteAsync(query, stock);

                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock update failed: " + ex.Message);
            }
        }
        public async Task<string> DeleteStock(long stockcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                UPDATE public.stock_master
                SET
                    deleted = true,
                    isactive = false,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE stockcode = @stockcode;";

                    int rows = await db.ExecuteAsync(query, new { stockcode });

                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock delete failed: " + ex.Message);
            }
        }
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
                        indentno,
                        indentdate,
                        requestedby,
                        departmentcode,
                        branchcode,
                        remarks,
                        approvalstatus,
                        isactive,
                        deleted,
                        createddate,
                        tenantcode
                    )
                    VALUES
                    (
                        @indentno,
                        @indentdate,
                        @requestedby,
                        @departmentcode,
                        @branchcode,
                        @remarks,
                        @approvalstatus,
                        @isactive,
                        @deleted,
                        CURRENT_TIMESTAMP,
                        @tenantcode
                    )
                    RETURNING indentcode;";

                        long indentcode = await db.ExecuteScalarAsync<long>(
                            masterQuery,
                            request.master,
                            transaction
                        );

                        string detailQuery = @"
                    INSERT INTO public.indent_detail
                    (
                        indentcode,
                        itemcode,
                        requestedqty,
                        approvedqty,
                        issuedqty,
                        remarks
                    )
                    VALUES
                    (
                        @indentcode,
                        @itemcode,
                        @requestedqty,
                        @approvedqty,
                        @issuedqty,
                        @remarks
                    );";

                        foreach (var item in request.details)
                        {
                            item.indentcode = indentcode;

                            await db.ExecuteAsync(
                                detailQuery,
                                item,
                                transaction
                            );
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
                        string updateMaster = @"
                    UPDATE public.indent_master
                    SET
                        indentno = @indentno,
                        indentdate = @indentdate,
                        requestedby = @requestedby,
                        departmentcode = @departmentcode,
                        branchcode = @branchcode,
                        remarks = @remarks,
                        approvalstatus = @approvalstatus,
                        tenantcode = @tenantcode
                    WHERE indentcode = @indentcode;";

                        await db.ExecuteAsync(
                            updateMaster,
                            request.master,
                            transaction
                        );

                        string deleteDetail = @"
                    DELETE FROM public.indent_detail
                    WHERE indentcode = @indentcode;";

                        await db.ExecuteAsync(
                            deleteDetail,
                            new { indentcode = request.master.indentcode },
                            transaction
                        );

                        string insertDetail = @"
                    INSERT INTO public.indent_detail
                    (
                        indentcode,
                        itemcode,
                        requestedqty,
                        approvedqty,
                        issuedqty,
                        remarks
                    )
                    VALUES
                    (
                        @indentcode,
                        @itemcode,
                        @requestedqty,
                        @approvedqty,
                        @issuedqty,
                        @remarks
                    );";

                        foreach (var item in request.details)
                        {
                            item.indentcode = request.master.indentcode;

                            await db.ExecuteAsync(
                                insertDetail,
                                item,
                                transaction
                            );
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
        public async Task<string> DeleteIndent(long indentcode)
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
                        deleted = true,
                        isactive = false
                    WHERE indentcode = @indentcode;",
                            new { indentcode },
                            transaction);

                        await db.ExecuteAsync(@"
                    DELETE FROM public.indent_detail
                    WHERE indentcode = @indentcode;",
                            new { indentcode },
                            transaction);

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
        public async Task<long> InsertPurchaseEntry(purchase_entry_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // 1️⃣ INSERT purchase_entry_master (GRN)
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
                        remarks,
                        isactive, deleted,
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
                        @remarks,
                        @isactive, @deleted,
                        CURRENT_TIMESTAMP, @usercode,
                        @tenantcode, @branchcode, @companycode
                    )
                    RETURNING purchaseentrycode;",
                            request.master, transaction);

                        // 2️⃣ INSERT purchase_entry_detail
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
                            warehousecode,
                            tenantcode
                        )
                        VALUES
                        (
                            @purchaseentrycode, @itemcode,
                            @orderedqty, @receivedqty, @rejectedqty, @quantity,
                            @rate, @discountpercentage, @discountamount,
                            @taxpercentage, @taxamount,
                            @amount, @totalamount,
                            @batchno, @manufacturingdate, @expirydate,
                            @warehousecode,
                            @tenantcode
                        );",
                                item, transaction);
                        }

                        // 3️⃣ INSERT purchase_master (IMPORTANT PART 🔥)
                        var purchasecode = await db.ExecuteScalarAsync<long>(@"
                    INSERT INTO purchase_master
                    (
                        billno, billdate, invoiceno, invoicedate,
                        vendorcode,
                        grossamount, discountamount, taxamount, netamount,
                        paymentmode, paymentstatus,
                        currencycode,
                        isactive, deleted,
                        remarks,
                        createddate, usercode,
                        tenantcode, branchcode, companycode,
                        grncode
                    )
                    VALUES
                    (
                        @billno, @billdate, @invoiceno, @invoicedate,
                        @vendorcode,
                        @grossamount, @discountamount, @taxamount, @netamount,
                        @paymentmode, @paymentstatus,
                        @currencycode,
                        @isactive, @deleted,
                        @remarks,
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
                                grncode = purchaseentrycode   // ⭐ KEY LINE
                            },
                            transaction);

                        // 4️⃣ INSERT purchase_detail
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
                                    qty = item.receivedqty, // ✅ IMPORTANT
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

                        // 1️⃣ Reverse Stock
                        var oldItems = await db.QueryAsync<purchase_entry_detail>(
                            "SELECT * FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = entryId }, transaction);

                        foreach (var item in oldItems)
                        {
                            await db.ExecuteAsync(@"
                        UPDATE stock_master
                        SET purchasedqty = purchasedqty - @qty,
                            closingstock = closingstock - @qty
                        WHERE itemcode = @itemcode;",
                                new { qty = item.receivedqty, item.itemcode }, transaction);
                        }

                        // 2️⃣ Update purchase_entry_master
                        await db.ExecuteAsync(@"
                    UPDATE purchase_entry_master
                    SET billno=@billno,
                        billdate=@billdate,
                        invoiceno=@invoiceno,
                        invoicedate=@invoicedate,
                        vendorcode=@vendorcode,
                        totalqty=@totalqty,
                        receivedqty=@receivedqty,
                        grossamount=@grossamount,
                        discountamount=@discountamount,
                        taxamount=@taxamount,
                        netamount=@netamount,
                        paymentmode=@paymentmode,
                        paymentstatus=@paymentstatus,
                        remarks=@remarks,
                        modifieddate=CURRENT_TIMESTAMP
                    WHERE purchaseentrycode=@purchaseentrycode;",
                            request.master, transaction);

                        // 3️⃣ Delete old entry details
                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_detail WHERE purchaseentrycode=@id",
                            new { id = entryId }, transaction);

                        // 4️⃣ Insert new entry details
                        foreach (var item in request.details)
                        {
                            item.purchaseentrycode = entryId;

                            await db.ExecuteAsync(@"
                        INSERT INTO purchase_entry_detail
                        (purchaseentrycode,itemcode,receivedqty,rate,amount,totalamount,tenantcode)
                        VALUES
                        (@purchaseentrycode,@itemcode,@receivedqty,@rate,@amount,@totalamount,@tenantcode);",
                                item, transaction);
                        }

                        // 5️⃣ Update purchase_master
                        await db.ExecuteAsync(@"
                    UPDATE purchase_master
                    SET billno=@billno,
                        billdate=@billdate,
                        invoiceno=@invoiceno,
                        invoicedate=@invoicedate,
                        vendorcode=@vendorcode,
                        grossamount=@grossamount,
                        discountamount=@discountamount,
                        taxamount=@taxamount,
                        netamount=@netamount,
                        paymentmode=@paymentmode,
                        paymentstatus=@paymentstatus,
                        remarks=@remarks,
                        modifieddate=CURRENT_TIMESTAMP
                    WHERE grncode=@purchaseentrycode;",
                            request.master, transaction);

                        // 6️⃣ Get purchasecode
                        var purchasecode = await db.ExecuteScalarAsync<long>(
                            "SELECT purchasecode FROM purchase_master WHERE grncode=@id",
                            new { id = entryId }, transaction);

                        // 7️⃣ Delete old purchase_detail
                        await db.ExecuteAsync(
                            "DELETE FROM purchase_detail WHERE purchasecode=@pc",
                            new { pc = purchasecode }, transaction);

                        // 8️⃣ Insert new purchase_detail
                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                        INSERT INTO purchase_detail
                        (purchasecode,itemcode,quantity,rate,totalamount,tenantcode)
                        VALUES
                        (@purchasecode,@itemcode,@qty,@rate,@totalamount,@tenantcode);",
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

                        // 9️⃣ Add New Stock
                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                        UPDATE stock_master
                        SET purchasedqty = purchasedqty + @qty,
                            closingstock = closingstock + @qty
                        WHERE itemcode = @itemcode;",
                                new { qty = item.receivedqty, item.itemcode }, transaction);
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
        public async Task<string> DeletePurchaseEntry(long purchaseentrycode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // 1️⃣ Get items
                        var items = await db.QueryAsync<purchase_entry_detail>(
                            "SELECT * FROM purchase_entry_detail WHERE purchaseentrycode=@id",
                            new { id = purchaseentrycode }, transaction);

                        // 2️⃣ Reverse stock
                        foreach (var item in items)
                        {
                            await db.ExecuteAsync(@"
                        UPDATE stock_master
                        SET purchasedqty = purchasedqty - @qty,
                            closingstock = closingstock - @qty
                        WHERE itemcode = @itemcode;",
                                new { qty = item.receivedqty, item.itemcode }, transaction);
                        }

                        // 3️⃣ Get purchasecode
                        var purchasecode = await db.ExecuteScalarAsync<long>(
                            "SELECT purchasecode FROM purchase_master WHERE grncode=@id",
                            new { id = purchaseentrycode }, transaction);

                        // 4️⃣ Delete purchase_detail
                        await db.ExecuteAsync(
                            "DELETE FROM purchase_detail WHERE purchasecode=@pc",
                            new { pc = purchasecode }, transaction);

                        // 5️⃣ Delete purchase_master
                        await db.ExecuteAsync(
                            "DELETE FROM purchase_master WHERE purchasecode=@pc",
                            new { pc = purchasecode }, transaction);

                        // 6️⃣ Delete entry detail
                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_detail WHERE purchaseentrycode=@id",
                            new { id = purchaseentrycode }, transaction);

                        // 7️⃣ Delete entry master
                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_master WHERE purchaseentrycode=@id",
                            new { id = purchaseentrycode }, transaction);

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
        public async Task ProcessExcel(string filePath)
        {
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                int rowCount = worksheet.Dimension.Rows;

                var items = new List<item_master>();

                for (int row = 2; row <= rowCount; row++) // skip header
                {
                    var item = new item_master
                    {
                        itemname = worksheet.Cells[row, 1].Text,
                        shortname = worksheet.Cells[row, 2].Text,
                        description = worksheet.Cells[row, 3].Text,
                        categorycode = int.Parse(worksheet.Cells[row, 4].Text),
                        subcategorycode = int.Parse(worksheet.Cells[row, 5].Text),
                        purchaserate = decimal.Parse(worksheet.Cells[row, 6].Text)
                    };

                    items.Add(item);
                }

                await InsertBulk(items);
            }
        }
        public async Task InsertBulk(List<item_master> items)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"INSERT INTO item_master
                        (itemname, shortname, description, categorycode, subcategorycode, purchaserate)
                        VALUES
                        (@itemname, @shortname, @description, @categorycode, @subcategorycode, @purchaserate)";

                foreach (var item in items)
                {
                    await db.ExecuteAsync(query, item);
                }
            }
        }
    }
}
