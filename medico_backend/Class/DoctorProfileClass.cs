using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorProfileClass
    {
        private readonly string db_conn;

        public DoctorProfileClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(DoctorProfileModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

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
        public async Task<string> Update(DoctorProfileModel data)
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
        public async Task<string> Delete(int pcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE doctor_profile
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE pcode       = @pcode
                               AND tenant_code   = @tenant_code";

                await db.ExecuteAsync(sql, new { pcode, tenant_code });
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
        public async Task<List<DoctorProfileModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_profile
                           WHERE deleted      = false
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<DoctorProfileModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY PCODE
        // ─────────────────────────────────────────
        public async Task<DoctorProfileModel?> GetByPcode(int pcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_profile
                           WHERE deleted    = false
                           AND pcode        = @pcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<DoctorProfileModel>(
                sql, new { pcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY DCODE (profile for a specific doctor)
        // ─────────────────────────────────────────
        public async Task<DoctorProfileModel?> GetByDcode(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_profile
                           WHERE deleted    = false
                           AND dcode        = @dcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<DoctorProfileModel>(
                sql, new { dcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET PUBLISHED PROFILES ONLY (for website/app listing)
        // ─────────────────────────────────────────
        public async Task<List<DoctorProfileModel>> GetPublished(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM doctor_profile
                           WHERE deleted        = false
                           AND is_published     = true
                           AND tenant_code      = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<DoctorProfileModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET FULL PROFILE (JOIN with doctor_master)
        // ─────────────────────────────────────────
        public async Task<dynamic?> GetFullProfile(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT dm.dcode, dm.name, dm.qualification, dm.doctorcode,
                                  dm.doctorimage, dm.mobile, dm.email,
                                  dp.pcode, dp.about, dp.experience_years, dp.education_details,
                                  dp.operations_performed, dp.patients_treated, dp.achievements,
                                  dp.memberships, dp.publications, dp.languages_known,
                                  dp.profile_video_url, dp.banner_image, dp.is_published
                           FROM doctor_master dm
                           LEFT JOIN doctor_profile dp
                             ON dm.dcode = dp.dcode AND dm.tenant_code = dp.tenant_code
                             AND dp.deleted = false
                           WHERE dm.deleted = false
                           AND dm.dcode = @dcode
                           AND dm.tenant_code = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync(sql, new { dcode, tenant_code });
            return res;
        }
    }
}