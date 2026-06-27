using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class ReimbursementCompanyMasterClass
    {
        private readonly string db_conn;

        public ReimbursementCompanyMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT RICODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextRiCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(ricode), 0) + 1
                           FROM reimbursement_company_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(ReimbursementCompanyMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ricode = await GetNextRiCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO reimbursement_company_master
                    (
                        ricode, orderno, ftcode, shortname, name,
                        address, city, pincode, state, country,
                        phone, contactname, mobile, fax, email,
                        website, description, areacode, deleted,
                        usercode, computercode, entereddate, ibsdate,
                        lcode, monthlytype, tenant_code
                    )
                    VALUES
                    (
                        @ricode, @orderno, @ftcode, @shortname, @name,
                        @address, @city, @pincode, @state, @country,
                        @phone, @contactname, @mobile, @fax, @email,
                        @website, @description, @areacode, @deleted,
                        @usercode, @computercode, @entereddate, @ibsdate,
                        @lcode, @monthlytype, @tenant_code
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
        public async Task<string> Update(ReimbursementCompanyMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE reimbursement_company_master
                    SET
                        orderno      = @orderno,
                        ftcode       = @ftcode,
                        shortname    = @shortname,
                        name         = @name,
                        address      = @address,
                        city         = @city,
                        pincode      = @pincode,
                        state        = @state,
                        country      = @country,
                        phone        = @phone,
                        contactname  = @contactname,
                        mobile       = @mobile,
                        fax          = @fax,
                        email        = @email,
                        website      = @website,
                        description  = @description,
                        areacode     = @areacode,
                        deleted      = @deleted,
                        usercode     = @usercode,
                        computercode = @computercode,
                        ibsdate      = @ibsdate,
                        lcode        = @lcode,
                        monthlytype  = @monthlytype,
                        tenant_code  = @tenant_code
                    WHERE ricode = @ricode
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
        public async Task<string> Delete(decimal ricode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE reimbursement_company_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE ricode = @ricode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { ricode, tenant_code });
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
        public async Task<List<ReimbursementCompanyMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM reimbursement_company_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<ReimbursementCompanyMasterModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY RICODE
        // ─────────────────────────────────────────
        public async Task<ReimbursementCompanyMasterModel?> GetByRiCode(decimal ricode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM reimbursement_company_master
                WHERE deleted = false
                AND ricode = @ricode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<ReimbursementCompanyMasterModel>(
                sql, new { ricode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<ReimbursementCompanyMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM reimbursement_company_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<ReimbursementCompanyMasterModel>(
                sql, new { name = $"%{name}%", tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY FTCODE
        // ─────────────────────────────────────────
        public async Task<List<ReimbursementCompanyMasterModel>> GetByFtCode(int ftcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM reimbursement_company_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND ftcode = @ftcode
                ORDER BY orderno";

            var result = await db.QueryAsync<ReimbursementCompanyMasterModel>(
                sql, new { ftcode, tenant_code });
            return result.ToList();
        }
    }
}