using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TenantReportMethodClass
    {
        private readonly string _db_conn;

        public TenantReportMethodClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn");
        }

        private IDbConnection Connection() => new NpgsqlConnection(_db_conn);

        // ─── Get by Tenant ────────────────────────────────────────────
        public async Task<TenantReportMethodModel?> GetByTenant(string tenant_code)
        {
            using IDbConnection db = Connection();

            const string sql = @"
                SELECT * FROM mastertenant.tenant_report_method
                WHERE tenant_code = @tenant_code
                AND deleted = false
                LIMIT 1";

            return await db.QueryFirstOrDefaultAsync<TenantReportMethodModel>(
                sql, new { tenant_code });
        }

        // ─── Insert ───────────────────────────────────────────────────
        public async Task<string> Insert(TenantReportMethodModel data)
        {
            try
            {
                using IDbConnection db = Connection();

                data.created_at = DateTime.UtcNow;
                data.updated_at = DateTime.UtcNow;
                data.deleted = false;

                const string sql = @"
            INSERT INTO mastertenant.tenant_report_method
                (tenant_code,
                 logo_url,
                 isletterhead, letterhead_url,
                 isletterbottom, bottom_url,
                 all_branch,
                 iswatermark, watermark_url,
                 report_title, report_font, report_font_size, report_color,
                 paper_size, orientation,
                 margin_top, margin_bottom, margin_left, margin_right,
                 issignature, signature_url, signature_label,
                 isheadertext, header_text,
                 isfootertext, footer_text,
                 isbarcode, isqrcode,
                 show_printed_datetime, datetime_format,
                 created_at, updated_at, deleted)
            VALUES
                (@tenant_code,
                 @logo_url,
                 @isletterhead, @letterhead_url,
                 @isletterbottom, @bottom_url,
                 @all_branch,
                 @iswatermark, @watermark_url,
                 @report_title, @report_font, @report_font_size, @report_color,
                 @paper_size, @orientation,
                 @margin_top, @margin_bottom, @margin_left, @margin_right,
                 @issignature, @signature_url, @signature_label,
                 @isheadertext, @header_text,
                 @isfootertext, @footer_text,
                 @isbarcode, @isqrcode,
                 @show_printed_datetime, @datetime_format,
                 @created_at, @updated_at, @deleted)
            RETURNING id";

                // ← Pass anonymous object WITHOUT id
                data.id = await db.ExecuteScalarAsync<int>(sql, new
                {
                    data.tenant_code,
                    data.logo_url,
                    data.isletterhead,
                    data.letterhead_url,
                    data.isletterbottom,
                    data.bottom_url,
                    data.all_branch,
                    data.iswatermark,
                    data.watermark_url,
                    data.report_title,
                    data.report_font,
                    data.report_font_size,
                    data.report_color,
                    data.paper_size,
                    data.orientation,
                    data.margin_top,
                    data.margin_bottom,
                    data.margin_left,
                    data.margin_right,
                    data.issignature,
                    data.signature_url,
                    data.signature_label,
                    data.isheadertext,
                    data.header_text,
                    data.isfootertext,
                    data.footer_text,
                    data.isbarcode,
                    data.isqrcode,
                    data.show_printed_datetime,
                    data.datetime_format,
                    data.created_at,
                    data.updated_at,
                    data.deleted
                });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─── Update ───────────────────────────────────────────────────
        public async Task<string> Update(TenantReportMethodModel data)
        {
            try
            {
                using IDbConnection db = Connection();

                data.updated_at = DateTime.UtcNow;

                const string sql = @"
                    UPDATE mastertenant.tenant_report_method SET
                        logo_url                = @logo_url,
                        isletterhead            = @isletterhead,
                        letterhead_url          = @letterhead_url,
                        isletterbottom          = @isletterbottom,
                        bottom_url              = @bottom_url,
                        all_branch              = @all_branch,
                        iswatermark             = @iswatermark,
                        watermark_url           = @watermark_url,
                        report_title            = @report_title,
                        report_font             = @report_font,
                        report_font_size        = @report_font_size,
                        report_color            = @report_color,
                        paper_size              = @paper_size,
                        orientation             = @orientation,
                        margin_top              = @margin_top,
                        margin_bottom           = @margin_bottom,
                        margin_left             = @margin_left,
                        margin_right            = @margin_right,
                        issignature             = @issignature,
                        signature_url           = @signature_url,
                        signature_label         = @signature_label,
                        isheadertext            = @isheadertext,
                        header_text             = @header_text,
                        isfootertext            = @isfootertext,
                        footer_text             = @footer_text,
                        isbarcode               = @isbarcode,
                        isqrcode                = @isqrcode,
                        show_printed_datetime   = @show_printed_datetime,
                        datetime_format         = @datetime_format,
                        updated_at              = @updated_at
                    WHERE tenant_code = @tenant_code
                    AND deleted = false";

                int rows = await db.ExecuteAsync(sql, data);
                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─── Soft Delete ──────────────────────────────────────────────
        public async Task<string> Delete(int id, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                const string sql = @"
            UPDATE mastertenant.tenant_report_method
            SET deleted    = true,
                updated_at = now()
            WHERE id          = @id
            AND tenant_code   = @tenant_code";

                await db.ExecuteAsync(sql, new { id, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}