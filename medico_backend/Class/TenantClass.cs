using Dapper;
using Npgsql;
using System.Data;
using medico_backend.Model;

namespace medico_backend.Class
{
    public class TenantClass
    {
        private readonly string _db_conn;

        public TenantClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // GENERATE NEXT TENANT_CODE (e.g. TEN001, TEN002...)
        // ─────────────────────────────────────────
        private async Task<string> GenerateTenantCode(IDbConnection db)
        {
            string sql = @"SELECT COALESCE(MAX(
                               CAST(SUBSTRING(tenant_code FROM 4) AS INT)
                           ), 0) + 1
                           FROM mastertenant.tenants
                           WHERE tenant_code ~ '^TEN[0-9]+$'";

            int next = await db.ExecuteScalarAsync<int>(sql);
            return $"TEN{next:D3}";
        }

  
        

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(UpdateTenantRequest req)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                string sql = @"UPDATE mastertenant.tenants SET
                    tenant_name        = @tenant_name,
                    connection_string  = @connection_string,
                    is_active          = @is_active,
                    logo_url           = @logo_url,
                    host_url           = @host_url,
                    api_url            = @api_url,
                    contact_email      = @contact_email,
                    contact_number     = @contact_number,
                    legal_name         = @legal_name,
                    contact_person     = @contact_person,
                    alternate_mobile   = @alternate_mobile,
                    gst_number         = @gst_number,
                    pan_number         = @pan_number,
                    address_line1      = @address_line1,
                    address_line2      = @address_line2,
                    city               = @city,
                    state              = @state,
                    country            = @country,
                    pincode            = @pincode,
                    time_zone          = @time_zone,
                    currency           = @currency,
                    business_type      = @business_type,
                    register_num       = @register_num
                    WHERE tenant_id = @tenant_id AND isdeleted = false";

                int rows = await db.ExecuteAsync(sql, req);
                return rows > 0 ? "Success" : "Tenant not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(Guid tenant_id)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                string sql = @"UPDATE mastertenant.tenants
                               SET isdeleted = true, is_active = false
                               WHERE tenant_id = @tenant_id";

                int rows = await db.ExecuteAsync(sql, new { tenant_id });
                return rows > 0 ? "Success" : "Tenant not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<TenantModel>> GetAll()
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT id, tenant_id, tenant_code, tenant_name, connection_string,
                                  is_active, created_at, logo_url, host_url, api_url,
                                  contact_email, contact_number, legal_name, contact_person,
                                  alternate_mobile, gst_number, pan_number, address_line1,
                                  address_line2, city, state, country, pincode, time_zone,
                                  currency, business_type, register_num, isdeleted
                           FROM mastertenant.tenants
                           WHERE isdeleted = false
                           ORDER BY id";

            var res = await db.QueryAsync<TenantModel>(sql);
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY TENANT_ID
        // ─────────────────────────────────────────
        public async Task<TenantModel?> GetByTenantId(Guid tenant_id)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT id, tenant_id, tenant_code, tenant_name, connection_string,
                                  is_active, created_at, logo_url, host_url, api_url,
                                  contact_email, contact_number, legal_name, contact_person,
                                  alternate_mobile, gst_number, pan_number, address_line1,
                                  address_line2, city, state, country, pincode, time_zone,
                                  currency, business_type, register_num, isdeleted
                           FROM mastertenant.tenants
                           WHERE tenant_id = @tenant_id AND isdeleted = false";

            return await db.QueryFirstOrDefaultAsync<TenantModel>(sql, new { tenant_id });
        }

        // ─────────────────────────────────────────
        // GET BY TENANT_CODE
        // ─────────────────────────────────────────
        public async Task<TenantModel?> GetByTenantCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT id, tenant_id, tenant_code, tenant_name, connection_string,
                                  is_active, created_at, logo_url, host_url, api_url,
                                  contact_email, contact_number, legal_name, contact_person,
                                  alternate_mobile, gst_number, pan_number, address_line1,
                                  address_line2, city, state, country, pincode, time_zone,
                                  currency, business_type, register_num, isdeleted
                           FROM mastertenant.tenants
                           WHERE tenant_code = @tenant_code AND isdeleted = false";

            return await db.QueryFirstOrDefaultAsync<TenantModel>(sql, new { tenant_code });
        }
    }
}