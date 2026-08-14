using System;
using System.Collections.Generic;

namespace medico_backend.Model
{
    public class StockReportModel
    {
        public long stockcode { get; set; }
        public long itemcode { get; set; }
        public string? itemname { get; set; }
        public string? batchno { get; set; }
        public DateOnly? expirydate { get; set; }
        public string? warehousename { get; set; }
        public decimal openingstock { get; set; }
        public decimal purchasedqty { get; set; }
        public decimal soldqty { get; set; }
        public decimal closingstock { get; set; }
        public decimal unitcost { get; set; }
        public decimal stockvalue { get; set; }
    }

    public class StockReportRequest
    {
        public IList<StockReportModel>? items { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public string? reporttype { get; set; } = "Stock Master Report";
        public DateTime? asofdate { get; set; }
    }

    public class SalesReportModel
    {
        public long salescode { get; set; }
        public string? billno { get; set; }
        public DateTime billdate { get; set; }
        public string? invoiceno { get; set; }
        public DateTime? invoicedate { get; set; }
        public string? patientid { get; set; }
        public string? patientname { get; set; }
        public string? salestype { get; set; }
        public string? consultant { get; set; }
        public decimal grossamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal taxamount { get; set; }
        public decimal netamount { get; set; }
        public decimal paidamount { get; set; }
        public decimal balanceamount { get; set; }
        public string? paymentmode { get; set; }
        public string? paymentstatus { get; set; }
        public string? warehousename { get; set; }
    }

    public class SalesReportRequest
    {
        public IList<SalesReportModel>? items { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? reporttype { get; set; } = "Sales Master Report";
    }
}
