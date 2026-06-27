using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorMasterClass
    {
        private readonly string db_conn;

        public DoctorMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        // In Insert method, get both codes in ONE connection:
        public async Task<string> Insert(DoctorMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                // Both use same connection — no race condition
                //data.dcode = await GetNextDcode(data.tenant_code!);
                data.doctorcode = $"DOC{data.dcode.ToString("D3")}"; // derive directly, no second query
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

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
        public async Task<string> Update(DoctorMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                var res = await db.UpdateAsync(data);
                return res ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(int dcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE doctor_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE dcode       = @dcode
                               AND tenant_code   = @tenant_code";

                await db.ExecuteAsync(sql, new { dcode, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<DoctorMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_master
                           WHERE deleted      = false
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<DoctorMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY DCODE
        // ─────────────────────────────────────────
        public async Task<DoctorMasterModel?> GetByDcode(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_master
                           WHERE deleted    = false
                           AND dcode        = @dcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<DoctorMasterModel>(
                sql, new { dcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET CONSULTANTS ONLY
        // ─────────────────────────────────────────
        public async Task<List<DoctorMasterModel>> GetConsultants(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_master
                           WHERE deleted        = false
                           AND isconsultant     = true
                           AND tenant_code      = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<DoctorMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET REFERRALS ONLY
        // ─────────────────────────────────────────
        public async Task<List<DoctorMasterModel>> GetReferrals(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_master
                           WHERE deleted        = false
                           AND isreferral       = true
                           AND tenant_code      = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<DoctorMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET NEXT DCODE (Auto Increment)
        // ─────────────────────────────────────────
        public async Task<int> GetNextDcode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(dcode), 0) + 1
                           FROM doctor_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        public async Task<string> GetNextDoctorCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT COALESCE(MAX(dcode), 0) + 1
        FROM doctor_master
        WHERE tenant_code = @tenant_code";

            int nextNo = await db.ExecuteScalarAsync<int>(
                sql,
                new { tenant_code });

            return $"DOC{nextNo.ToString("D3")}";
        }
    }
}