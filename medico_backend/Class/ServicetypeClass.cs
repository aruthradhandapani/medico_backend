using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class ServiceTypeClass
    {
        private readonly string _db_conn;

        public ServiceTypeClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        private IDbConnection Connection() => new NpgsqlConnection(_db_conn);

        public async Task<List<ServiceTypeModel>> GetAll(string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                string sql = @"SELECT * FROM service_type_master
                               WHERE tenant_code = @tenant_code AND deleted = false
                               ORDER BY service_id";
                var res = await db.QueryAsync<ServiceTypeModel>(sql, new { tenant_code });
                return res.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServiceTypeClass.GetAll ERROR: " + ex.Message);
                return new List<ServiceTypeModel>();
            }
        }

        public async Task<ServiceTypeModel?> GetById(int service_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                string sql = @"SELECT * FROM service_type_master
                               WHERE service_id = @service_id
                               AND   tenant_code = @tenant_code
                               AND   deleted = false";
                return await db.QueryFirstOrDefaultAsync<ServiceTypeModel>(sql, new { service_id, tenant_code });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServiceTypeClass.GetById ERROR: " + ex.Message);
                return null;
            }
        }

        // ─── Insert (Dapper.Contrib InsertAsync — returns generated service_id) ──
        public async Task<(bool success, int? service_id, string? error)> Insert(ServiceTypeModel model, string tenant_code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.service_name))
                    return (false, null, "service_name is required");

                if (model.scope != "DOCTOR" && model.scope != "TENANT")
                    return (false, null, "scope must be DOCTOR or TENANT");

                using IDbConnection db = Connection();

                model.tenant_code = tenant_code;
                model.deleted = false;
                model.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                model.updated_at = model.created_at;

                int newId = await db.InsertAsync(model);
                return (true, newId, null);
            }
            catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "23505")
            {
                return (false, null, $"Service '{model.service_name}' already exists for this tenant");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServiceTypeClass.Insert ERROR: " + ex.Message);
                return (false, null, ex.Message);
            }
        }

        // ─── Update (Dapper.Contrib UpdateAsync) ───────────────────
        public async Task<(bool success, string? error)> Update(ServiceTypeModel model, string tenant_code)
        {
            try
            {
                if (model.service_id <= 0)
                    return (false, "service_id is required");

                if (model.scope != "DOCTOR" && model.scope != "TENANT")
                    return (false, "scope must be DOCTOR or TENANT");

                using IDbConnection db = Connection();

                var existing = await GetById(model.service_id, tenant_code);
                if (existing == null)
                    return (false, $"Service id {model.service_id} not found for this tenant");

                model.tenant_code = tenant_code;
                model.deleted = false;
                model.created_at = existing.created_at;
                model.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                bool success = await db.UpdateAsync(model);
                return (success, success ? null : "Update failed — no rows affected");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServiceTypeClass.Update ERROR: " + ex.Message);
                return (false, ex.Message);
            }
        }

        // ─── Soft delete — OP protected by service_name match ──────
        public async Task<(bool success, string? error)> SoftDelete(int service_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var existing = await GetById(service_id, tenant_code);
                if (existing == null)
                    return (false, $"Service id {service_id} not found for this tenant");

                if (string.Equals(existing.service_name?.Trim(), "OP Registration", StringComparison.OrdinalIgnoreCase))
                    return (false, "OP Registration cannot be deleted — it is required for core registration");

                existing.deleted = true;
                existing.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                bool success = await db.UpdateAsync(existing);
                return (success, success ? null : "Soft delete failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServiceTypeClass.SoftDelete ERROR: " + ex.Message);
                return (false, ex.Message);
            }
        }

        // ─── Hard delete ────────────────────────────────────────────
        public async Task<(bool success, string? error)> Delete(int service_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var existing = await db.QueryFirstOrDefaultAsync<ServiceTypeModel>(
                    @"SELECT * FROM service_type_master WHERE service_id = @service_id AND tenant_code = @tenant_code",
                    new { service_id, tenant_code });

                if (existing == null)
                    return (false, $"Service id {service_id} not found for this tenant");

                if (string.Equals(existing.service_name?.Trim(), "OP Registration", StringComparison.OrdinalIgnoreCase))
                    return (false, "OP Registration cannot be deleted — it is required for core registration");

                int usedCount = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) FROM op_registration
                      WHERE service_id = @service_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { service_id, tenant_code });

                if (usedCount > 0)
                    return (false, $"Cannot hard delete — {usedCount} registration(s) already use this service. Use soft delete instead.");

                bool success = await db.DeleteAsync(existing);
                return (success, success ? null : "Delete failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServiceTypeClass.Delete ERROR: " + ex.Message);
                return (false, ex.Message);
            }
        }
    }
}