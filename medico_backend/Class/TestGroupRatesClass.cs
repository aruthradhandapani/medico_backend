using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Medico_Backend.Model;
using Npgsql;
using System.Data;

namespace Medico_Backend.Class
{
    public class TestGroupRateClass
    {
        private readonly string db_conn;

        public TestGroupRateClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET ALL RATE ROWS FOR A ROOM TYPE
        // (the "split-up charge list" the UI shows/edits per rmtcode)
        // ─────────────────────────────────────────
        public async Task<List<TestGroupRateModel>> GetByRmtcode(int rmtcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM public.test_group_rates
                           WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code
                           ORDER BY roomchargehead";

            var res = await db.QueryAsync<TestGroupRateModel>(sql, new { rmtcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // INSERT SINGLE ROW
        // ─────────────────────────────────────────
        public async Task<string> Insert(TestGroupRateModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entereddate = DateTime.UtcNow;

                var id = await db.InsertAsync(data);
                data.id = id;
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE SINGLE ROW
        // ─────────────────────────────────────────
        public async Task<string> Update(TestGroupRateModel data)
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
        // DELETE SINGLE ROW
        // ─────────────────────────────────────────
        public async Task<string> Delete(int id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"DELETE FROM public.test_group_rates
                               WHERE id = @id AND tenant_code = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new { id, tenant_code });
                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SAVE WHOLE LIST FOR A ROOM TYPE (replace-all)
        // Convenience for the "edit this room type's split-up charges"
        // screen: user submits the full list, old rows for that rmtcode
        // are wiped and the new list is inserted. Simpler than diffing
        // adds/edits/removes client-side.
        // ─────────────────────────────────────────
        public async Task<string> SaveForRoomType(SaveRoomTypeRatesRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                await db.ExecuteAsync(
                    @"DELETE FROM public.test_group_rates
                      WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code",
                    new { req.rmtcode, tenant_code }, tx);

                foreach (var r in req.rates)
                {
                    var row = new TestGroupRateModel
                    {
                        rmtcode = req.rmtcode,
                        roomchargehead = r.roomchargehead,
                        subtestcode = r.subtestcode,
                        testrate = r.testrate,
                        usercode = 1,
                        entereddate = DateTime.UtcNow,
                        tenant_code = tenant_code
                    };
                    await db.InsertAsync(row, tx);
                }

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (across all room types — admin/reference view)
        // ─────────────────────────────────────────
        public async Task<List<TestGroupRateModel>> GetAll(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM public.test_group_rates
                           WHERE tenant_code = @tenant_code
                           ORDER BY rmtcode, roomchargehead";

            var res = await db.QueryAsync<TestGroupRateModel>(sql, new { tenant_code });
            return res.ToList();
        }
    }
}