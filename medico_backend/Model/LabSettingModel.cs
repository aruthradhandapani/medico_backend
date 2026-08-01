using Dapper.Contrib.Extensions;

public class LabSettingModel
{
    [Table("lab_settings")]
    public class lab_settings
    {
        [ExplicitKey]
        public Guid lsid { get; set; }

        public bool? ls_common_normal_values { get; set; }
        public bool? ls_select_normal_values { get; set; }
        public bool? ls_hide_signature { get; set; }
        public bool? ls_signature_on_end { get; set; }
        public bool? ls_signature_on_each_page { get; set; }

        public string? lab_report_name { get; set; }
        public string? report_header { get; set; }
        public string? report_footer { get; set; }
        public string? cheque_name { get; set; }

        public bool? timing_normal { get; set; }
        public bool? timing_manual { get; set; }

        public bool? bill_white_sheet { get; set; }
        public bool? bill_letter_pad { get; set; }
        public bool? bill_portrait { get; set; }
        public bool? bill_landscape { get; set; }
        public string? bill_paper_size { get; set; } = "A4";
        public string? bill_orientation { get; set; } = "portrait";

        // Routine Report Authorization Signees Config
        public bool? auth1_show { get; set; } = true;
        public string? auth1_name { get; set; }
        public string? auth1_designation { get; set; }
        public string? auth1_signature_path { get; set; }

        public bool? auth2_show { get; set; } = true;
        public string? auth2_name { get; set; }
        public string? auth2_designation { get; set; }
        public string? auth2_signature_path { get; set; }

        public bool? auth3_show { get; set; } = true;
        public string? auth3_name { get; set; }
        public string? auth3_designation { get; set; }
        public string? auth3_signature_path { get; set; }

        // Culture Report Authorization Signees Config
        public bool? culture_auth1_show { get; set; } = true;
        public string? culture_auth1_name { get; set; }
        public string? culture_auth1_designation { get; set; }
        public string? culture_auth1_signature_path { get; set; }

        public bool? culture_auth2_show { get; set; } = true;
        public string? culture_auth2_name { get; set; }
        public string? culture_auth2_designation { get; set; }
        public string? culture_auth2_signature_path { get; set; }

        public bool? culture_auth3_show { get; set; } = true;
        public string? culture_auth3_name { get; set; }
        public string? culture_auth3_designation { get; set; }
        public string? culture_auth3_signature_path { get; set; }

        // Header & Footer Image Display Toggles per Report Type
        public bool? show_bill_header_footer_image { get; set; } = true;
        public bool? show_report_header_footer_image { get; set; } = true;
        public bool? show_culture_header_footer_image { get; set; } = true;
        public bool? show_receipt_header_footer_image { get; set; } = true;
        public bool? show_op_casesheet_header_footer_image { get; set; } = true;
        public bool? show_ip_casesheet_header_footer_image { get; set; } = true;
        public bool? show_casesheet_header_footer_image { get; set; } = true;
        public bool? show_dischargesummary_header_footer_image { get; set; } = true;

        // Signature Mode Selection (True = Fixed LabSettings Signatures, False = Dynamic Test Result Saved & Authorized Users)
        public bool? use_labsetting_signatures { get; set; } = true;
        public bool? use_labsetting_culture_signatures { get; set; } = true;

        // QR Code Display Toggle for Routine Report
        public bool? report_qr { get; set; } = true;

        public double? bill_top { get; set; }
        public double? bill_bottom { get; set; }

        // iScan Routine Report Margin Settings (default 0)
        public double? iscan_margin_top { get; set; } = 0;
        public double? iscan_margin_bottom { get; set; } = 0;
        public double? iscan_margin_left { get; set; } = 0;
        public double? iscan_margin_right { get; set; } = 0;

        // Culture Report Margin Settings (default 0)
        public double? culture_margin_top { get; set; } = 0;
        public double? culture_margin_bottom { get; set; } = 0;
        public double? culture_margin_left { get; set; } = 0;
        public double? culture_margin_right { get; set; } = 0;

        public bool? print_work_order { get; set; }
        public bool? print_online_code_in_bill { get; set; }
        public bool? cust_name_upper { get; set; }
        public bool? direct_result { get; set; }
        public bool? ignore_bill_print { get; set; }

        public bool? sig_name_type { get; set; }
        public bool? jp_normal_alert { get; set; }
        public bool? maintain_patient { get; set; }

        public bool? scan2003 { get; set; }
        public bool? scan200710 { get; set; }

        public bool? multi_branch { get; set; }
        public bool? authorize_results { get; set; }

        public string? backup_path { get; set; }
        public bool? post_cash_advice { get; set; }

        public int? home_collection_tcode { get; set; }

        public bool? ls_bill_user_defined { get; set; }
        public bool? ls_slip_user_defined { get; set; }

        public bool? ls_culture_normal { get; set; }
        public bool? ls_culture_isolated { get; set; }

        public bool? print_branch_name_in_bill { get; set; }
        public bool? focus_address { get; set; }
        public bool? show_hospital_id { get; set; }
        public bool? focus_2nd_refby { get; set; }

        public bool? pathology_no { get; set; }
        public bool? display_user_name { get; set; }

        public bool? same_day { get; set; }
        public bool? next_day { get; set; }

        public DateTime? reporting_time { get; set; }

        public double? regular_discount { get; set; }

        public bool? ls_collectedby { get; set; }
        public bool? print_bill_to_printer { get; set; }
        public bool? print_barcode { get; set; }

        public bool? ls_send_lab_sms { get; set; }
        public bool? ls_send_scan_sms { get; set; }
        public bool? ls_cancelled_bills { get; set; }
        public bool? ls_confirm_counter { get; set; }

        public bool? ls_maintain_year { get; set; }
        public bool? ls_show_all_test { get; set; }

        public bool? collect_due_on_same_branch { get; set; }
        public bool? show_preview_after_entry { get; set; }

        public bool? authorize_results2 { get; set; }
        public bool? ls_culture_simple { get; set; }
        public bool? disc_without_home { get; set; }
        public bool? group_as_dep { get; set; }

        public bool? print_date { get; set; }
        public bool? separate_billing { get; set; }

        public bool? test_machine_add { get; set; }
        public bool? test_search_with_first { get; set; }
        public bool? view_scan_form { get; set; }
        public bool? fixed_type_on_f3 { get; set; }

        public bool? ls_variable_authorize { get; set; }
        public bool? ls_mobile_no { get; set; }

        public bool? easy_billing { get; set; }
        public bool? show_pending_finished { get; set; }

        public bool? sample_collection { get; set; }
        public bool? branch_wise_sample_collection { get; set; }
        public bool? dept_wise_sample_collection { get; set; }
        public bool? print_barcode_directly { get; set; }

        public string? logo_path { get; set; }
        public string? report_bill_common_address { get; set; }
        public string? report_bill_branch_address { get; set; }

        // ✅ Header image file path (for report/bill header)
        public string? header_path { get; set; }
        [Write(false)]
        [Computed]
        public string? header_image_path { get => header_path; set => header_path = value; }

        // ✅ Footer image file path (for report/bill footer)
        public string? footer_path { get; set; }
        [Write(false)]
        [Computed]
        public string? footer_image_path { get => footer_path; set => footer_path = value; }

        public string? tenant_code { get; set; }
        public bool deleted { get; set; }
        public int? bh_code { get; set; }
        public bool? counterset_setting { get; set; }
        public bool? ref_by { get; set; }
        // ✅ NEW — general HMS toggles, moved here instead of a separate settings table
        public bool? is_slot_required { get; set; } = true;   // false = walk-in registration without picking a slot
        public bool? op_age_wise_split { get; set; } = false; // true = doctor's OP charge is split by age slab
    }
}