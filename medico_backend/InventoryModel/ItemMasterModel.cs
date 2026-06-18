using Dapper.Contrib.Extensions;

namespace medico_backend.InventoryModel
{
    [Table("item_master")]
    public class item_master

    {
        [Key]
        public long itemcode { get; set; }
        public string itemname { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }

        public int categorycode { get; set; }
        public int subcategorycode { get; set; }

        public int hsncode { get; set; }

        public string itemtype { get; set; }

        public decimal gstpercentage { get; set; }

        public int uomcode { get; set; }

        public decimal purchaserate { get; set; }

        public decimal salesrate { get; set; }

        public decimal mrp { get; set; }

        public decimal currentstock { get; set; }

        public decimal minstock { get; set; }

        public decimal reorderlevel { get; set; }
        public decimal packsize { get; set; }
        public bool isexpiry { get; set; }          // Missing
        public int expiryalertdays { get; set; }    // Missing
        public bool batchrequired { get; set; }

        public bool expiryrequired { get; set; }

        public bool serialrequired { get; set; }

        public int brandcode { get; set; }

        public int manufacturercode { get; set; }

        public int taxcode { get; set; }

        // New Fields

        public int naturetype { get; set; }

        // Asset = 1
        // Liability = 2
        // Expense = 3
        // Income = 4

        public string? manufacturername { get; set; }
        public int ledgergroupcode { get; set; }

        public string? drugname { get; set; }

        public string? packaging { get; set; }

        public bool isactive { get; set; }

        public bool deleted { get; set; }

        public DateTime createddate { get; set; }

        public int usercode { get; set; }

        public string tenantcode { get; set; }
    }
    [Table("vendor_master")]

    public class vendor_master
 {
        [Key]
     public long vendorcode { get; set; }

     public string vendorname { get; set; }

     public string shortname { get; set; }

     public string vendortype { get; set; }

     public string contactperson { get; set; }

     public string phonenumber { get; set; }

     public string alternatephonenumber { get; set; }

     public string emailid { get; set; }

     public string website { get; set; }

     public string gstnumber { get; set; }

     public string pannumber { get; set; }

     public string taxid { get; set; }

     public string registrationnumber { get; set; }

     // New Fields

     public string? druglicenseno { get; set; }

     public string? fssaino { get; set; }

     public decimal? vendorrating { get; set; }

     public string addressline1 { get; set; }

     public string addressline2 { get; set; }

     public string landmark { get; set; }

     public string city { get; set; }

     public string district { get; set; }

     public string state { get; set; }

     public string postalcode { get; set; }

     public string countrycode { get; set; }

     public string countryname { get; set; }

     public string currencycode { get; set; }

     public string paymentterms { get; set; }

     public string creditperiod { get; set; }

     public string bankname { get; set; }

     public string accountnumber { get; set; }

     public string ifsccode { get; set; }

     public string swiftcode { get; set; }

     public string ibannumber { get; set; }

     public bool isactive { get; set; }

     public bool deleted { get; set; }

     public DateTime createddate { get; set; }

     public DateTime? modifieddate { get; set; }

     public long usercode { get; set; }

     public string tenantcode { get; set; }

     public string branchcode { get; set; }
 }
   public class purchase_master
{
    public long purchasecode { get; set; }

    public string? billno { get; set; }

    public DateTime billdate { get; set; }

    public string? invoiceno { get; set; }

    public DateTime? invoicedate { get; set; }

    public long? vendorcode { get; set; }

    public decimal grossamount { get; set; }

    public decimal discountamount { get; set; }

    public decimal taxamount { get; set; }

    public decimal netamount { get; set; }

    // New Fields
    public decimal transportationcharges { get; set; }

    public decimal roundoff { get; set; }

    public string? paymentmode { get; set; }

    public string? paymentstatus { get; set; }

    public string? currencycode { get; set; }

    public bool isactive { get; set; }

    public bool deleted { get; set; }

    public string? remarks { get; set; }

    public DateTime createddate { get; set; }

    public DateTime? modifieddate { get; set; }

    public long? usercode { get; set; }

    public string? tenantcode { get; set; }

    public string? branchcode { get; set; }

    public string? companycode { get; set; }

    public long grncode { get; set; }
}
public class purchase_detail
{
    public long purchasedetailcode { get; set; }

    public long purchasecode { get; set; }

    public long itemcode { get; set; }

    public decimal quantity { get; set; }

    public decimal freequantity { get; set; }

    public long? uomcode { get; set; }

    public decimal rate { get; set; }

    public decimal discountpercentage { get; set; }

    public decimal discountamount { get; set; }

    public decimal taxpercentage { get; set; }

    public decimal taxamount { get; set; }

    public decimal amount { get; set; }

    public decimal totalamount { get; set; }

    public string? batchno { get; set; }

    public DateTime? manufacturingdate { get; set; }

    public DateTime? expirydate { get; set; }

    // New Fields

    public decimal orderedqty { get; set; }

    public decimal receivedqty { get; set; }

    public decimal rejectedqty { get; set; }

    // Warehouse / Store
    public string warehousecode { get; set; }

    // Packaging
    public string? packaging { get; set; }

    // Manufacturer
    public string manufacturercode { get; set; }

    public string? tenantcode { get; set; }
}

    public class purchase_request
    {
        public purchase_master master { get; set; }
        public List<purchase_detail> details { get; set; }
    }
    public class stock_master
    {
        public long stockcode { get; set; }

        public long itemcode { get; set; }

        public long? warehousecode { get; set; }
        public string? branchcode { get; set; }
        public string? locationcode { get; set; }

        public decimal openingstock { get; set; }
        public decimal purchasedqty { get; set; }
        public decimal soldqty { get; set; }
        public decimal damagedqty { get; set; }
        public decimal returnqty { get; set; }
        public decimal closingstock { get; set; }

        public decimal unitcost { get; set; }
        public decimal stockvalue { get; set; }

        public string? batchno { get; set; }
        public DateTime? manufacturingdate { get; set; }
        public DateTime? expirydate { get; set; }

        public bool isactive { get; set; }
        public bool deleted { get; set; }

        public DateTime createddate { get; set; }
        public DateTime? modifieddate { get; set; }
        public long? usercode { get; set; }

        public string? tenantcode { get; set; }
        public string? companycode { get; set; }
    }
    public class indent_master
    {
        public long indentcode { get; set; }

        // Indent Info
        public string? indentno { get; set; }
        public DateTime indentdate { get; set; }

        // Request Info
        public long? requestedby { get; set; }
        public long? departmentcode { get; set; }
        public string? branchcode { get; set; }

        // Remarks
        public string? remarks { get; set; }

        // Approval
        public string? approvalstatus { get; set; }   // PENDING / APPROVED / REJECTED
        public long? approvedby { get; set; }
        public DateTime? approveddate { get; set; }
        public string? approvalremarks { get; set; }

        // Status
        public bool isactive { get; set; }
        public bool deleted { get; set; }

        // Audit
        public DateTime createddate { get; set; }

        // Multi Tenant
        public string? tenantcode { get; set; }
    }
    public class indent_detail
    {
        public long indentdetailcode { get; set; }

        // Parent Indent
        public long indentcode { get; set; }

        // Item Reference
        public long itemcode { get; set; }

        // Quantity
        public decimal requestedqty { get; set; }
        public decimal approvedqty { get; set; }
        public decimal issuedqty { get; set; }

        // Remarks
        public string? remarks { get; set; }
    }
    public class indent_request
    {
        public indent_master master { get; set; }
        public List<indent_detail> details { get; set; }
    }
    public class purchase_entry_master
    {
        public long purchaseentrycode { get; set; }

        // GRN
        public string? grnno { get; set; }
        public DateTime grndate { get; set; }
        public long? receivedby { get; set; }

        // Purchase
        public string? billno { get; set; }
        public DateTime? billdate { get; set; }
        public string? invoiceno { get; set; }
        public DateTime? invoicedate { get; set; }

        public long vendorcode { get; set; }

        public decimal totalqty { get; set; }
        public decimal receivedqty { get; set; }

        public decimal grossamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal taxamount { get; set; }
        public decimal othercharges { get; set; }
        public decimal netamount { get; set; }

        public string? paymentmode { get; set; }
        public string? paymentstatus { get; set; }

        public string? approvalstatus { get; set; }
        public bool posted { get; set; }

        public string? remarks { get; set; }

        public bool isactive { get; set; }
        public bool deleted { get; set; }

        public DateTime createddate { get; set; }
        public DateTime? modifieddate { get; set; }
        public long? usercode { get; set; }

        public string? tenantcode { get; set; }
        public string? branchcode { get; set; }
        public string? companycode { get; set; }
    }
    public class purchase_entry_detail
    {
        public long purchaseentrydetailcode { get; set; }

        public long purchaseentrycode { get; set; }
        public long itemcode { get; set; }

        public decimal orderedqty { get; set; }
        public decimal receivedqty { get; set; }
        public decimal rejectedqty { get; set; }
        public decimal quantity { get; set; }

        public decimal rate { get; set; }
        public decimal discountpercentage { get; set; }
        public decimal discountamount { get; set; }
        public decimal taxpercentage { get; set; }
        public decimal taxamount { get; set; }

        public decimal amount { get; set; }
        public decimal totalamount { get; set; }

        public string? batchno { get; set; }
        public DateTime? manufacturingdate { get; set; }
        public DateTime? expirydate { get; set; }

        public long? warehousecode { get; set; }

        public string? tenantcode { get; set; }
    }
    public class purchase_entry_request
    {
        public purchase_entry_master master { get; set; }
        public List<purchase_entry_detail> details { get; set; }
    }
    public class category_master
    {
        public long categorycode { get; set; }
        public string categoryname { get; set; }
        public string shortname { get; set; }
        public string? description { get; set; }
        public int parentcategorycode { get; set; }
        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public int usercode { get; set; }
        public string? tenantcode { get; set; }
    }
    public class uom_master
    {
        public long ucode { get; set; }

        public int orderno { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? description { get; set; }

        public bool? deleted { get; set; } = false;

        public int? usercode { get; set; }

        public int? computercode { get; set; }

        public DateTime? entereddate { get; set; }

        public DateTime? ibsdate { get; set; }

        public int? packsize { get; set; }

        public string? tenant_code { get; set; }
    }
    public class parent_category_master
    {
        public int parentcategorycode { get; set; }
        public string parentcategoryname { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public string? tenantcode { get; set; }
    }

    public class ledger_master
    {
        public int ledgercode { get; set; }
        public string ledgername { get; set; }
        public string lcode { get; set; }
        public string ldgcode { get; set; }

        public string taxtype { get; set; }
        public string taxsubtype { get; set; }
        public decimal taxpercentage { get; set; }

        public decimal gstpercentage { get; set; }
        public string hsncode { get; set; }

        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public string? tenantcode { get; set; }
    }
    [Table("ledger_type_master")]
    public class ledger_type_master
    {
        [Key]
        public int? ledgertypecode { get; set; }
        public string ledgertypename { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public int naturetype { get; set; }
        public bool isactive { get; set; } = true;
        public DateTime createddate { get; set; } = DateTime.UtcNow;
        public string tenantcode { get; set; }
        public bool isgstapplicable { get; set; } = false;
        public bool isvatapplicable { get; set; } = false;
        public decimal sgstpercentage { get; set; } = 0;
        public decimal cgstpercentage { get; set; } = 0;
        public decimal igstpercentage { get; set; } = 0;
        public bool deleted { get; set; } = false;
    }
    [Table("ledger_group_master")]
    public class ledger_group_master
    {
        [Key]
        public int? ledgergroupcode { get; set; }

        public string ledgergroupname { get; set; }

        public string shortname { get; set; }

        public int ledgertypecode { get; set; }

        public string description { get; set; }

        public bool isactive { get; set; } = true;

        public DateTime createddate { get; set; } = DateTime.Now;

        public string tenantcode { get; set; }

        public bool deleted { get; set; } = false;
    }
    public class sales_master
    {
        public long salescode { get; set; }

        public string? billno { get; set; }

        public DateTime billdate { get; set; }

        public string? invoiceno { get; set; }

        public DateTime? invoicedate { get; set; }

        public long? customercode { get; set; }

        public decimal grossamount { get; set; }

        public decimal discountamount { get; set; }

        public decimal taxamount { get; set; }

        public decimal netamount { get; set; }

        public string? paymentmode { get; set; }

        public string? paymentstatus { get; set; }

        public string? currencycode { get; set; }

        public bool isactive { get; set; }

        public bool deleted { get; set; }

        public string? remarks { get; set; }

        public DateTime createddate { get; set; }

        public DateTime? modifieddate { get; set; }

        public long? usercode { get; set; }

        public string? tenantcode { get; set; }

        public string? branchcode { get; set; }

        public string? companycode { get; set; }

        public long? ordercode { get; set; }
    }
    public class sales_detail
    {
        public long salesdetailcode { get; set; }

        public long salescode { get; set; }

        public long itemcode { get; set; }

        public decimal quantity { get; set; }

        public decimal freequantity { get; set; }

        public long? uomcode { get; set; }

        public decimal rate { get; set; }

        public decimal discountpercentage { get; set; }

        public decimal discountamount { get; set; }

        public decimal taxpercentage { get; set; }

        public decimal taxamount { get; set; }

        public decimal amount { get; set; }

        public decimal totalamount { get; set; }

        public string? batchno { get; set; }

        public DateTime? manufacturingdate { get; set; }

        public DateTime? expirydate { get; set; }

        // Sales Specific

        public decimal orderedqty { get; set; }

        public decimal deliveredqty { get; set; }

        public decimal returnedqty { get; set; }

        public long? warehousecode { get; set; }

        public string? tenantcode { get; set; }
    }
    public class sales_request
    {
        public sales_master master { get; set; }
        public List<sales_detail> details { get; set; }
    }
    [Table("warehouse_master")]
    public class warehouse_master
 {
        [Key]
     public int? warehousecode { get; set; }
     public int orderno { get; set; }

     public string warehousename { get; set; }
     public string shortname { get; set; }
     public string description { get; set; }
     public string location { get; set; }

     public int tenantcode { get; set; }

     public bool isactive { get; set; }
     public bool isdeleted { get; set; }

     public DateTime createddate { get; set; }
 }
  public class manufacturer_master
 {
     public long manufacturercode { get; set; }
     public string manufacturername { get; set; }
     public string shortname { get; set; }
     public string description { get; set; }
     public string contactperson { get; set; }
     public string phoneno { get; set; }
     public string email { get; set; }
     public string address { get; set; }
     public string gstno { get; set; }
     public bool isactive { get; set; }
     public bool deleted { get; set; }
     public DateTime createddate { get; set; }
     public int usercode { get; set; }
     public string tenantcode { get; set; }
 }
}


