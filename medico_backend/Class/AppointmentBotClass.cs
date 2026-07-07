using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class AppointmentBotClass
    {
        private readonly string db_conn;

        public AppointmentBotClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(AppointmentBotModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                await db.InsertAsync(data);
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
        public async Task<string> Update(AppointmentBotModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var res = await db.UpdateAsync(data);
                return res ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE (hard delete — no 'deleted' flag on this table)
        // ─────────────────────────────────────────
        public async Task<string> Delete(int bot_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"DELETE FROM whatsappdb.appointment_bot
                               WHERE bot_id      = @bot_id
                               AND tenant_code   = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { bot_id, tenant_code });
                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBotModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM whatsappdb.appointment_bot
                           WHERE tenant_code = @tenant_code
                           ORDER BY bot_id";

            var res = await db.QueryAsync<AppointmentBotModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY BOT_ID
        // ─────────────────────────────────────────
        public async Task<AppointmentBotModel?> GetByBotId(int bot_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM whatsappdb.appointment_bot
                           WHERE bot_id      = @bot_id
                           AND tenant_code   = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<AppointmentBotModel>(
                sql, new { bot_id, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY BH_CODE (bot config for a specific branch)
        // ─────────────────────────────────────────
        public async Task<AppointmentBotModel?> GetByBhCode(int bh_code, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM whatsappdb.appointment_bot
                           WHERE bh_code     = @bh_code
                           AND tenant_code   = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<AppointmentBotModel>(
                sql, new { bh_code, tenant_code });
            return res;
        }
    }
}