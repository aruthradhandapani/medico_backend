using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class ProductListClass
    {
        private readonly string db_conn;

        public ProductListClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<ProductListModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.product_list
                ORDER BY id";

            var result = await db.QueryAsync<ProductListModel>(sql);

            return result.ToList();
        }
        // ─────────────────────────────────────────
        // GET BY TENANT CODE
        // ─────────────────────────────────────────
        public async Task<List<ProductListModel>> GetByTenant(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.product_list
                WHERE tenant_code = @tenant_code
                ORDER BY id";

            var result = await db.QueryAsync<ProductListModel>(sql, new { tenant_code });

            return result.ToList();
        }
    }
}