using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TenantDirectUrlClass
    {
        private readonly string db_conn;

        public TenantDirectUrlClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<TenantDirectUrlModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.tenant_direct_url
                WHERE COALESCE(isdeleted,false) = false
                ORDER BY id";

            var result = await db.QueryAsync<TenantDirectUrlModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<TenantDirectUrlModel?> GetById(long id)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.tenant_direct_url
                WHERE id = @id
                AND COALESCE(isdeleted,false) = false";

            return await db.QueryFirstOrDefaultAsync<TenantDirectUrlModel>(
                sql,
                new { id });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(TenantDirectUrlModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entered_date = DateTime.UtcNow;
                data.ibsd_date = DateTime.UtcNow;
                data.isdeleted = false;

                string sql = @"
                    INSERT INTO mastertenant.tenant_direct_url
                    (
                        tenant_code,
                        tenant_name,
                        title,
                        url,
                        entered_date,
                        ibsd_date,
                        isdeleted
                    )
                    VALUES
                    (
                        @tenant_code,
                        @tenant_name,
                        @title,
                        @url,
                        @entered_date,
                        @ibsd_date,
                        @isdeleted
                    )";

                await db.ExecuteAsync(sql, data);

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(TenantDirectUrlModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsd_date = DateTime.UtcNow;

                string sql = @"
                    UPDATE mastertenant.tenant_direct_url
                    SET
                        tenant_code = @tenant_code,
                        tenant_name = @tenant_name,
                        title = @title,
                        url = @url,
                        ibsd_date = @ibsd_date
                    WHERE id = @id";

                await db.ExecuteAsync(sql, data);

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> SoftDelete(long id)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE mastertenant.tenant_direct_url
                    SET
                        isdeleted = true,
                        ibsd_date = now()
                    WHERE id = @id";

                await db.ExecuteAsync(sql, new { id });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // HARD DELETE
        // ─────────────────────────────────────────
        public async Task<string> HardDelete(long id)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    DELETE FROM mastertenant.tenant_direct_url
                    WHERE id = @id";

                await db.ExecuteAsync(sql, new { id });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SEARCH BY TENANT NAME AND URL
        // ─────────────────────────────────────────
        public async Task<List<TenantDirectUrlModel>> Search(string? tenant_name, string? url)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT *
        FROM mastertenant.tenant_direct_url
        WHERE COALESCE(isdeleted,false) = false
        AND
        (
            (@tenant_name IS NULL OR LOWER(tenant_name) LIKE LOWER(@tenant_name_pattern))
        )
        AND
        (
            (@url IS NULL OR LOWER(url) LIKE LOWER(@url_pattern))
        )
        ORDER BY tenant_name";

            var result = await db.QueryAsync<TenantDirectUrlModel>(
                sql,
                new
                {
                    tenant_name,
                    url,
                    tenant_name_pattern = $"%{tenant_name}%",
                    url_pattern = $"%{url}%"
                });

            return result.ToList();
        }
    }
}