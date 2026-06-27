using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class ReportMethodClass
    {
        private readonly string db_conn;

        public ReportMethodClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT RTMCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextRtmCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(rtmcode), 0) + 1
                           FROM report_method
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(ReportMethodModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.rtmcode = await GetNextRtmCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO report_method
                    (
                        rtmcode,
                        tenant_code,
                        orderno,
                        shortname,
                        name,
                        durationtime,
                        duration,
                        description,
                        footer,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @rtmcode,
                        @tenant_code,
                        @orderno,
                        @shortname,
                        @name,
                        @durationtime,
                        @duration,
                        @description,
                        @footer,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate
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
        public async Task<string> Update(ReportMethodModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE report_method
                    SET
                        orderno = @orderno,
                        shortname = @shortname,
                        name = @name,
                        durationtime = @durationtime,
                        duration = @duration,
                        description = @description,
                        footer = @footer,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE rtmcode = @rtmcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, data);

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(decimal rtmcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE report_method
                    SET deleted = true,
                        ibsdate = now()
                    WHERE rtmcode = @rtmcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { rtmcode, tenant_code });

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
        public async Task<List<ReportMethodModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM report_method
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<ReportMethodModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY RTMCODE
        // ─────────────────────────────────────────
        public async Task<ReportMethodModel?> GetByRtmCode(decimal rtmcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM report_method
                WHERE deleted = false
                AND rtmcode = @rtmcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<ReportMethodModel>(
                sql,
                new { rtmcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<ReportMethodModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM report_method
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<ReportMethodModel>(
                sql,
                new { name = $"%{name}%", tenant_code });

            return result.ToList();
        }
    }
}