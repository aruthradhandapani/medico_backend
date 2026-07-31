using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using static LabSettingModel;

namespace medico_backend.Class
{
    public class LabSettingClass
    {
        private readonly IConfiguration config;
        private readonly string db_conn;

        public LabSettingClass(IConfiguration configuration)
        {
            config = configuration;
            db_conn = config.GetConnectionString("conn");
        }

        private IDbConnection Connection() => new NpgsqlConnection(db_conn);

        private async Task EnsureColumnsCreatedAsync(IDbConnection db)
        {
            try
            {
                string sql = @"
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_bill_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_report_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_culture_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_receipt_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_op_casesheet_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_ip_casesheet_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_casesheet_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_dischargesummary_header_footer_image BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS use_labsetting_signatures BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS use_labsetting_culture_signatures BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS report_qr BOOLEAN DEFAULT true;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS iscan_margin_top DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS iscan_margin_bottom DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS iscan_margin_left DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS iscan_margin_right DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS culture_margin_top DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS culture_margin_bottom DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS culture_margin_left DOUBLE PRECISION DEFAULT 0;
                    ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS culture_margin_right DOUBLE PRECISION DEFAULT 0;
                ";
                await db.ExecuteAsync(sql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LabSettingClass] EnsureColumnsCreatedAsync warning: {ex.Message}");
            }
        }

        // ─── Get (single/filtered by bh_code) ──────────────────────────
        public async Task<IList<lab_settings>> GetLab_Settings(int? bh_code, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                await EnsureColumnsCreatedAsync(db);
                string query = "SELECT * FROM lab_settings WHERE tenant_code = @tenant_code AND deleted = false";
                if (bh_code.HasValue)
                    query += " AND bh_code = @bh_code";
                var result = await db.QueryAsync<lab_settings>(query, new { tenant_code, bh_code });

                if (result == null || result.Count() == 0)
                {
                    string query1 = "SELECT * FROM lab_settings WHERE tenant_code = @tenant_code AND deleted = false and (bh_code = 0 or bh_code is null)";
                    result = await db.QueryAsync<lab_settings>(query1, new { tenant_code });
                }
                return result.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetLab_Settings ERROR: " + ex.Message);
                return new List<lab_settings>();
            }
        }


        // ─── Get All (all settings rows for a tenant, across branches) ─
        public async Task<IList<lab_settings>> GetAll(string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                await EnsureColumnsCreatedAsync(db);
                const string query = @"
                    SELECT * FROM lab_settings 
                    WHERE tenant_code = @tenant_code AND deleted = false
                    ORDER BY bh_code";
                var result = await db.QueryAsync<lab_settings>(query, new { tenant_code });
                return result.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetAll ERROR: " + ex.Message);
                return new List<lab_settings>();
            }
        }

        // ─── Insert ─────────────────────────────────────────────────────
        public async Task<(Guid lsid, string? error)> Insert(lab_settings model, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                await EnsureColumnsCreatedAsync(db);

                model.lsid = model.lsid == Guid.Empty ? Guid.NewGuid() : model.lsid;
                model.tenant_code = tenant_code;
                model.deleted = false;

                await db.InsertAsync(model);
                return (model.lsid, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Insert ERROR: " + ex.Message);
                return (Guid.Empty, ex.Message);
            }
        }

        // ─── Update ─────────────────────────────────────────────────────
        // ✅ Only allowed if the row belongs to the caller's tenant
        public async Task<(bool success, string? error)> Update(lab_settings model, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                await EnsureColumnsCreatedAsync(db);

                lab_settings? existing = null;
                if (model.lsid != Guid.Empty)
                {
                    existing = await db.QueryFirstOrDefaultAsync<lab_settings>(
                        "SELECT * FROM lab_settings WHERE lsid = @lsid AND deleted = false",
                        new { model.lsid });
                }

                if (existing == null)
                {
                    string bhQuery = "SELECT * FROM lab_settings WHERE tenant_code = @tenant_code AND deleted = false";
                    if (model.bh_code.HasValue)
                        bhQuery += " AND bh_code = @bh_code";
                    else
                        bhQuery += " AND (bh_code = 0 OR bh_code IS NULL)";

                    existing = await db.QueryFirstOrDefaultAsync<lab_settings>(bhQuery, new { tenant_code, bh_code = model.bh_code });
                }

                if (existing == null)
                {
                    var (newLsid, insertErr) = await Insert(model, tenant_code);
                    return (newLsid != Guid.Empty, insertErr);
                }

                if (existing.tenant_code != tenant_code)
                    return (false, "Access denied. Record belongs to a different tenant.");

                model.lsid = existing.lsid;
                model.tenant_code = tenant_code;
                model.deleted = false;

                bool success = await db.UpdateAsync(model);
                return (success, success ? null : "Update failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Update ERROR: " + ex.Message);
                return (false, ex.Message);
            }
        }

        // ─── Get By LSID ────────────────────────────────────────────────
        public async Task<lab_settings?> GetByLsid(Guid lsid, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                await EnsureColumnsCreatedAsync(db);
                const string sql = "SELECT * FROM lab_settings WHERE lsid = @lsid AND tenant_code = @tenant_code AND deleted = false";
                return await db.QueryFirstOrDefaultAsync<lab_settings>(sql, new { lsid, tenant_code });
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetByLsid ERROR: " + ex.Message);
                return null;
            }
        }

        // ─── Update Header, Footer & Signature Paths ──────────────────────────────
        public async Task<bool> UpdateImageAndSignaturePaths(
            Guid lsid,
            string tenant_code,
            string? headerPath,
            string? footerPath,
            string? auth1SigPath,
            string? auth2SigPath,
            string? auth3SigPath,
            string? cultureAuth1SigPath = null,
            string? cultureAuth2SigPath = null,
            string? cultureAuth3SigPath = null)
        {
            try
            {
                using IDbConnection db = Connection();
                string sql = @"
                    UPDATE lab_settings
                    SET header_path = COALESCE(@headerPath, header_path),
                        footer_path = COALESCE(@footerPath, footer_path),
                        auth1_signature_path = COALESCE(@auth1SigPath, auth1_signature_path),
                        auth2_signature_path = COALESCE(@auth2SigPath, auth2_signature_path),
                        auth3_signature_path = COALESCE(@auth3SigPath, auth3_signature_path),
                        culture_auth1_signature_path = COALESCE(@cultureAuth1SigPath, culture_auth1_signature_path),
                        culture_auth2_signature_path = COALESCE(@cultureAuth2SigPath, culture_auth2_signature_path),
                        culture_auth3_signature_path = COALESCE(@cultureAuth3SigPath, culture_auth3_signature_path)
                    WHERE lsid = @lsid AND tenant_code = @tenant_code";
                int rows = await db.ExecuteAsync(sql, new { lsid, tenant_code, headerPath, footerPath, auth1SigPath, auth2SigPath, auth3SigPath, cultureAuth1SigPath, cultureAuth2SigPath, cultureAuth3SigPath });
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateImageAndSignaturePaths ERROR: " + ex.Message);
                return false;
            }
        }

        // ─── Soft Delete ────────────────────────────────────────────────
        // ✅ Only allowed if the row belongs to the caller's tenant
        public async Task<(bool success, string? error)> SoftDelete(Guid lsid, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var ownerTenant = await db.ExecuteScalarAsync<string?>(
                    "SELECT tenant_code FROM lab_settings WHERE lsid = @lsid AND deleted = false",
                    new { lsid });

                if (ownerTenant == null)
                    return (false, "Lab setting not found.");
                if (ownerTenant != tenant_code)
                    return (false, "Access denied. Record belongs to a different tenant.");

                const string sql = @"
                    UPDATE lab_settings 
                    SET deleted = true 
                    WHERE lsid = @lsid AND tenant_code = @tenant_code";

                int res = await db.ExecuteAsync(sql, new { lsid, tenant_code });
                return (res > 0, res > 0 ? null : "Soft delete failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("SoftDelete ERROR: " + ex.Message);
                return (false, ex.Message);
            }
        }

        // ─── Hard Delete ────────────────────────────────────────────────
        // ✅ Only allowed if the row belongs to the caller's tenant
        public async Task<(bool success, string? error)> Delete(Guid lsid, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var ownerTenant = await db.ExecuteScalarAsync<string?>(
                    "SELECT tenant_code FROM lab_settings WHERE lsid = @lsid",
                    new { lsid });

                if (ownerTenant == null)
                    return (false, "Lab setting not found.");
                if (ownerTenant != tenant_code)
                    return (false, "Access denied. Record belongs to a different tenant.");

                const string sql = "DELETE FROM lab_settings WHERE lsid = @lsid AND tenant_code = @tenant_code";
                int res = await db.ExecuteAsync(sql, new { lsid, tenant_code });
                return (res > 0, res > 0 ? null : "Delete failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Delete ERROR: " + ex.Message);
                return (false, ex.Message);
            }
        }
    }
}