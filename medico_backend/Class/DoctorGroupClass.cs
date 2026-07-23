using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorGroupMasterClass
    {
        private readonly string db_conn;

        public DoctorGroupMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        public async Task<string> Insert(DoctorGroupMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.is_deleted = false;

                await db.InsertAsync(data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> Update(DoctorGroupMasterModel data)
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

        public async Task<string> Delete(long group_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE doctor_group_master
                               SET is_deleted = true,
                                   ibsdate    = now()
                               WHERE group_id    = @group_id
                               AND tenant_code    = @tenant_code";

                await db.ExecuteAsync(sql, new { group_id, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<List<DoctorGroupMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM doctor_group_master
                           WHERE is_deleted = false
                           AND tenant_code  = @tenant_code
                           ORDER BY display_order";

            var res = await db.QueryAsync<DoctorGroupMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        public async Task<DoctorGroupMasterModel?> GetById(long group_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM doctor_group_master
                           WHERE is_deleted = false
                           AND group_id     = @group_id
                           AND tenant_code  = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<DoctorGroupMasterModel>(sql, new { group_id, tenant_code });
        }
    }
}