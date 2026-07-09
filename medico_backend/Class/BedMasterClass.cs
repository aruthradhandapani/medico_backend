using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class BedMasterClass
    {
        private readonly string db_conn;

        public BedMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(BedMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                var id = await db.InsertAsync(data);
                data.bedcode = id;

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
        public async Task<string> Update(BedMasterModel data)
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
        public async Task<string> Delete(int bedcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.bed_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE bedcode      = @bedcode
                               AND tenant_code    = @tenant_code";

                await db.ExecuteAsync(sql, new { bedcode, tenant_code });
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
        public async Task<List<BedMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY BEDCODE
        // ─────────────────────────────────────────
        public async Task<BedMasterModel?> GetByBedcode(int bedcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND bedcode      = @bedcode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<BedMasterModel>(
                sql, new { bedcode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY BRANCH
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> GetByBranch(int branchcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND branchcode     = @branchcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { branchcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY WARD
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> GetByWard(int wrdcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND wrdcode        = @wrdcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { wrdcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY ROOM TYPE
        // ─────────────────────────────────────────
        public async Task<List<BedMasterModel>> GetByRoomType(int rmtcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND rmtcode        = @rmtcode
                           AND tenant_code    = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<BedMasterModel>(sql, new { rmtcode, tenant_code });
            return res.ToList();
        }
        // ─────────────────────────────────────────
        // GET AVAILABLE BEDS (not currently occupied)
        // A bed is "occupied" only if an ip_registration row references it
        // with ip_status = 'ADMITTED' and isdeleted = false.
        // The moment that patient is discharged (ip_status -> 'DISCHARGED')
        // or transferred to another bed, this bed automatically becomes
        // available again — no separate "free the bed" step needed.
        //
        // Optional filters: branchcode, blockcode, flrcode, wrdcode, rmtcode
        // ─────────────────────────────────────────
        public async Task<List<dynamic>> GetAvailableBeds(
            string tenant_code,
            int? branchcode = null,
            int? blockcode = null,
            int? flrcode = null,
            int? wrdcode = null,
            int? rmtcode = null)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT
            bm.bedcode, bm.bedname, bm.shortname,
            bm.branchcode, bm.rmtcode, bm.wrdcode, bm.flrcode, bm.hdcode,
            bm.islaundry,
            rm.name  AS roomtype_name,
            rm.roomrate,
            wm.name  AS ward_name,
            fm.name  AS floor_name,
            fm.blockcode,
            bk.name  AS block_name
        FROM public.bed_master bm
        LEFT JOIN public.roomtype_master rm ON rm.rmtcode = bm.rmtcode AND rm.tenant_code = bm.tenant_code
        LEFT JOIN public.ward_master wm ON wm.wrdcode = bm.wrdcode AND wm.tenant_code = bm.tenant_code
        LEFT JOIN public.floor_master fm ON fm.flrcode = bm.flrcode AND fm.tenant_code = bm.tenant_code
        LEFT JOIN public.block_master bk ON bk.blockcode = fm.blockcode AND bk.tenant_code = bm.tenant_code
        WHERE (bm.deleted IS NULL OR bm.deleted = false)
          AND bm.tenant_code = @tenant_code
          AND (@branchcode IS NULL OR bm.branchcode = @branchcode)
          AND (@blockcode  IS NULL OR fm.blockcode  = @blockcode)
          AND (@flrcode    IS NULL OR bm.flrcode    = @flrcode)
          AND (@wrdcode    IS NULL OR bm.wrdcode    = @wrdcode)
          AND (@rmtcode    IS NULL OR bm.rmtcode    = @rmtcode)
          AND NOT EXISTS (
              SELECT 1 FROM ip_registration ip
              WHERE ip.bedcode = bm.bedcode
                AND ip.tenant_code = bm.tenant_code
                AND ip.ip_status = 'ADMITTED'
                AND ip.isdeleted = false
          )
        ORDER BY fm.orderno, wm.orderno, bm.orderno";

            var res = await db.QueryAsync<dynamic>(sql, new
            {
                tenant_code,
                branchcode,
                blockcode,
                flrcode,
                wrdcode,
                rmtcode
            });

            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET OCCUPIED BEDS (optional companion endpoint —
        // useful for a "current occupancy" dashboard view)
        // ─────────────────────────────────────────
        public async Task<List<dynamic>> GetOccupiedBeds(
            string tenant_code,
            int? branchcode = null,
            int? blockcode = null,
            int? flrcode = null,
            int? wrdcode = null,
            int? rmtcode = null)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT
            bm.bedcode, bm.bedname, bm.shortname,
            bm.branchcode, bm.rmtcode, bm.wrdcode, bm.flrcode,
            rm.name  AS roomtype_name,
            wm.name  AS ward_name,
            fm.name  AS floor_name,
            fm.blockcode,
            bk.name  AS block_name,
            ip.ip_id, ip.ip_no, ip.custid, ip.dcode, ip.admitdate
        FROM public.bed_master bm
        INNER JOIN ip_registration ip
            ON ip.bedcode = bm.bedcode
           AND ip.tenant_code = bm.tenant_code
           AND ip.ip_status = 'ADMITTED'
           AND ip.isdeleted = false
        LEFT JOIN public.roomtype_master rm ON rm.rmtcode = bm.rmtcode AND rm.tenant_code = bm.tenant_code
        LEFT JOIN public.ward_master wm ON wm.wrdcode = bm.wrdcode AND wm.tenant_code = bm.tenant_code
        LEFT JOIN public.floor_master fm ON fm.flrcode = bm.flrcode AND fm.tenant_code = bm.tenant_code
        LEFT JOIN public.block_master bk ON bk.blockcode = fm.blockcode AND bk.tenant_code = bm.tenant_code
        WHERE (bm.deleted IS NULL OR bm.deleted = false)
          AND bm.tenant_code = @tenant_code
          AND (@branchcode IS NULL OR bm.branchcode = @branchcode)
          AND (@blockcode  IS NULL OR fm.blockcode  = @blockcode)
          AND (@flrcode    IS NULL OR bm.flrcode    = @flrcode)
          AND (@wrdcode    IS NULL OR bm.wrdcode    = @wrdcode)
          AND (@rmtcode    IS NULL OR bm.rmtcode    = @rmtcode)
        ORDER BY fm.orderno, wm.orderno, bm.orderno";

            var res = await db.QueryAsync<dynamic>(sql, new
            {
                tenant_code,
                branchcode,
                blockcode,
                flrcode,
                wrdcode,
                rmtcode
            });

            return res.ToList();
        }
    }
}