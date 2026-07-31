using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorCurrentStatusClass
    {
        private readonly string db_conn;

        public DoctorCurrentStatusClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // SET / UPSERT STATUS (insert if not exists, else update)
        // ─────────────────────────────────────────
        public async Task<string> SetStatus(DoctorCurrentStatusModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;

                string sql = @"
                    INSERT INTO doctor_current_status
                    (
                        dcode,
                        tenant_code,
                        status,
                        remarks,
                        expected_return_time,
                        updated_by
                    )
                    VALUES
                    (
                        @dcode,
                        @tenant_code,
                        @status,
                        @remarks,
                        @expected_return_time,
                        @updated_by
                    )
                    ON CONFLICT (dcode, tenant_code)
                    DO UPDATE SET
                        status                = EXCLUDED.status,
                        remarks               = EXCLUDED.remarks,
                        expected_return_time  = EXCLUDED.expected_return_time,
                        updated_by            = EXCLUDED.updated_by,
                        updated_at            = CURRENT_TIMESTAMP";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE (by status_id)
        // ─────────────────────────────────────────
        public async Task<string> Update(DoctorCurrentStatusModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;

                string sql = @"
                    UPDATE doctor_current_status
                    SET
                        status                = @status,
                        remarks               = @remarks,
                        expected_return_time  = @expected_return_time,
                        updated_by            = @updated_by
                    WHERE status_id = @status_id
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, data);
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE (by status_id)
        // ─────────────────────────────────────────
        public async Task<string> Delete(long status_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    DELETE FROM doctor_current_status
                    WHERE status_id = @status_id
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { status_id, tenant_code });
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (by tenant)
        // ─────────────────────────────────────────
        public async Task<List<DoctorCurrentStatusModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM doctor_current_status
                WHERE tenant_code = @tenant_code
                ORDER BY dcode";

            var result = await db.QueryAsync<DoctorCurrentStatusModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY DCODE
        // ─────────────────────────────────────────
        public async Task<DoctorCurrentStatusModel?> GetByDcode(long dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM doctor_current_status
                WHERE dcode = @dcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<DoctorCurrentStatusModel>(
                sql, new { dcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH / FILTER BY STATUS
        // ─────────────────────────────────────────
        public async Task<List<DoctorCurrentStatusModel>> SearchByStatus(string status, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM doctor_current_status
                WHERE tenant_code = @tenant_code
                AND status = @status
                ORDER BY dcode";

            var result = await db.QueryAsync<DoctorCurrentStatusModel>(
                sql, new { status, tenant_code });
            return result.ToList();
        }
    }
}