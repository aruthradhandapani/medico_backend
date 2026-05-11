using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class PrefixMasterClass
    {
        private readonly string db_conn;

        public PrefixMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET ALL PREFIX
        // ─────────────────────────────────────────
        public async Task<List<PrefixMasterModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM prefix_master
                WHERE deleted = false
                ORDER BY prefixcode";

            var result = await db.QueryAsync<PrefixMasterModel>(sql);

            return result.ToList();
        }
    }
}