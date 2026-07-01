using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class MachineMasterClass
    {
        private readonly string _db_conn;

        public MachineMasterClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn");
        }

        private IDbConnection Connection() => new NpgsqlConnection(_db_conn);

        // ─── Get Next Mccode ──────────────────────────────────────────
        public async Task<int> GetNextMccode(string tenant_code)
        {
            using IDbConnection db = Connection();

            const string sql = @"
                SELECT COALESCE(MAX(mccode), 0) + 1
                FROM machine_master
                WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─── Get All ──────────────────────────────────────────────────
        public async Task<List<MachineMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = Connection();

            const string sql = @"
                SELECT * FROM machine_master
                WHERE deleted     = false
                AND tenant_code   = @tenant_code
                ORDER BY orderno";

            var res = await db.QueryAsync<MachineMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─── Get by Mccode ────────────────────────────────────────────
        public async Task<MachineMasterModel?> GetByMccode(int mccode, string tenant_code)
        {
            using IDbConnection db = Connection();

            const string sql = @"
                SELECT * FROM machine_master
                WHERE deleted     = false
                AND mccode        = @mccode
                AND tenant_code   = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<MachineMasterModel>(
                sql, new { mccode, tenant_code });
        }

        // ─── Insert ───────────────────────────────────────────────────
        public async Task<string> Insert(MachineMasterModel data)
        {
            try
            {
                using IDbConnection db = Connection();

                data.mccode = await GetNextMccode(data.tenant_code!);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                const string sql = @"
                    INSERT INTO machine_master
                        (mccode, orderno, shortname, name, description,
                         manufacturer, model, portnumber, baudrate,
                         parity, stopbits, databits,
                         deleted, usercode, computercode,
                         entereddate, ibsdate, tenant_code)
                    VALUES
                        (@mccode, @orderno, @shortname, @name, @description,
                         @manufacturer, @model, @portnumber, @baudrate,
                         @parity, @stopbits, @databits,
                         @deleted, @usercode, @computercode,
                         @entereddate, @ibsdate, @tenant_code)";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─── Update ───────────────────────────────────────────────────
        public async Task<string> Update(MachineMasterModel data)
        {
            try
            {
                using IDbConnection db = Connection();

                data.ibsdate = DateTime.UtcNow;

                const string sql = @"
                    UPDATE machine_master SET
                        orderno       = @orderno,
                        shortname     = @shortname,
                        name          = @name,
                        description   = @description,
                        manufacturer  = @manufacturer,
                        model         = @model,
                        portnumber    = @portnumber,
                        baudrate      = @baudrate,
                        parity        = @parity,
                        stopbits      = @stopbits,
                        databits      = @databits,
                        usercode      = @usercode,
                        ibsdate       = @ibsdate
                    WHERE mccode      = @mccode
                    AND tenant_code   = @tenant_code";

                int rows = await db.ExecuteAsync(sql, data);
                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─── Soft Delete ──────────────────────────────────────────────
        public async Task<string> Delete(int mccode, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                const string sql = @"
                    UPDATE machine_master
                    SET deleted     = true,
                        ibsdate     = now()
                    WHERE mccode      = @mccode
                    AND tenant_code   = @tenant_code";

                await db.ExecuteAsync(sql, new { mccode, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}