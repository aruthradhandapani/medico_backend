using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("mastertenant.tenant_report_method")]
    public class TenantReportMethodModel
    {
        [Key]
        [Write(false)]  // ← add this — tells Dapper never to write id as a parameter
        public int id { get; set; }

        public string tenant_code { get; set; } = string.Empty;

        // Logo
        public string? logo_url { get; set; }

        // Letterhead
        public bool isletterhead { get; set; } = false;
        public string? letterhead_url { get; set; }

        // Letter Bottom
        public bool isletterbottom { get; set; } = false;
        public string? bottom_url { get; set; }

        // Branch
        public bool all_branch { get; set; } = true;

        // Watermark
        public bool iswatermark { get; set; } = false;
        public string? watermark_url { get; set; }

        // Report Settings
        public string? report_title { get; set; }
        public string? report_font { get; set; } 
        public int? report_font_size { get; set; }
        public string? report_color { get; set; } 

        // Paper Settings
        public string? paper_size { get; set; } 
        public string? orientation { get; set; } 
        public int margin_top { get; set; } = 10;
        public int margin_bottom { get; set; } = 10;
        public int margin_left { get; set; } = 10;
        public int margin_right { get; set; } = 10;

        // Signature
        public bool issignature { get; set; } = false;
        public string? signature_url { get; set; }
        public string? signature_label { get; set; }

        // Header / Footer
        public bool isheadertext { get; set; } = false;
        public string? header_text { get; set; }
        public bool isfootertext { get; set; } = false;
        public string? footer_text { get; set; }

        // Barcode / QR
        public bool isbarcode { get; set; } = false;
        public bool isqrcode { get; set; } = false;

        // Date & Time
        public bool? show_printed_datetime { get; set; } 
        public string? datetime_format { get; set; } = "dd/MM/yyyy HH:mm";

        // Audit
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
        public bool deleted { get; set; } = false;
    }
}