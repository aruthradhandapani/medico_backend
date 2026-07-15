using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;
using medico_backend.Class;

namespace Medico_Backend.Class
{
    public class BedTransferClass
    {
        private readonly string db_conn;
        private readonly BedStatusClass _bedStatusCls;
        private readonly UnbilledChargesClass _unbilledCls;

        public BedTransferClass(IConfiguration configuration, BedStatusClass bedStatusCls, UnbilledChargesClass unbilledCls)
        {
            db_conn = configuration.GetConnectionString("conn");
            _bedStatusCls = bedStatusCls;
            _unbilledCls = unbilledCls;
        }

        // ─────────────────────────────────────────
        // INSERT (log a transfer)
        // If lastvisitid is a valid ip_registration.ip_id GUID and that patient
        // is currently ADMITTED, this also:
        //   1. Updates ip_registration's bedcode/flrcode/wrdcode/rmtcode to the new bed
        //      (looked up authoritatively from bed_master, not from the raw request)
        //   2. Marks the OLD bed VACANT in bed_status
        //   3. Marks the NEW bed OCCUPIED in bed_status
        // All in a single transaction.
        // ─────────────────────────────────────────
        public async Task<string> Insert(BedTransferModel data)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                data.entereddate = DateTime.UtcNow;
                if (data.transferdate == null)
                    data.transferdate = DateTime.UtcNow;

                if (string.IsNullOrWhiteSpace(data.transbed?.ToString()) || data.transbed == null)
                {
                    tx.Rollback();
                    return "transbed is required";
                }

                // Try to resolve lastvisitid as an ip_registration.ip_id
                Guid? ipId = Guid.TryParse(data.lastvisitid, out var parsedGuid) ? parsedGuid : null;

                if (ipId.HasValue)
                {
                    var ip = await db.QueryFirstOrDefaultAsync<dynamic>(
                       @"SELECT ip_id, ip_no, custid, bedcode, flrcode, wrdcode, rmtcode, ip_status
                        FROM ip_registration
                        WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                        FOR UPDATE",
                    new { ip_id = ipId.Value, tenant_code = data.tenant_code }, tx);

                    if (ip == null) { tx.Rollback(); return "IP Registration not found for lastvisitid"; }
                    if ((string)ip.ip_status != "ADMITTED") { tx.Rollback(); return "Patient is not currently admitted"; }

                    int newBedcode = data.transbed.Value;
                    int oldBedcode = (int)ip.bedcode;

                    if (newBedcode == oldBedcode)
                    {
                        tx.Rollback();
                        return "Target bed is the same as current bed";
                    }

                    // Confirm target bed isn't already occupied by someone else
                    var occupiedBy = await db.QueryFirstOrDefaultAsync(
                        @"SELECT ip_id FROM ip_registration
                          WHERE bedcode = @newBedcode AND tenant_code = @tenant_code
                          AND ip_status = 'ADMITTED' AND isdeleted = false AND ip_id <> @ip_id",
                        new { newBedcode, tenant_code = data.tenant_code, ip_id = ipId.Value }, tx);

                    if (occupiedBy != null) { tx.Rollback(); return "Target bed is already occupied"; }

                    // Authoritative room/floor/ward info comes from bed_master, not the raw request
                    var newBed = await db.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT bedcode, flrcode, wrdcode, rmtcode, branchcode
                          FROM public.bed_master
                          WHERE bedcode = @newBedcode AND tenant_code = @tenant_code
                          AND (deleted IS NULL OR deleted = false)",
                        new { newBedcode, tenant_code = data.tenant_code }, tx);

                    if (newBed == null) { tx.Rollback(); return "Target bed not found in bed_master"; }

                    // Stamp authoritative before/after room codes — don't trust the caller's `data`
                    data.currentfloor = (int?)ip.flrcode;
                    data.currentroom = (int?)ip.rmtcode;   // room TYPE code — what room-rent keys off
                    data.currentbed = oldBedcode;
                    data.transfloor = (int?)newBed.flrcode;
                    data.transroom = (int?)newBed.rmtcode;
                    // data.transbed already correct — came from the validated request

                    // 1. Insert the bed_transfer log row
                    var id = await db.InsertAsync(data, tx);
                    data.transferid = id;

                    // 2. Update ip_registration's current bed/room allocation
                    await db.ExecuteAsync(@"
                        UPDATE ip_registration
                        SET bedcode = @bedcode, flrcode = @flrcode, wrdcode = @wrdcode, rmtcode = @rmtcode,
                            updated_at = now()
                        WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                        new
                        {
                            bedcode = (int)newBed.bedcode,
                            flrcode = (int?)newBed.flrcode,
                            wrdcode = (int?)newBed.wrdcode,
                            rmtcode = (int?)newBed.rmtcode,
                            ip_id = ipId.Value,
                            tenant_code = data.tenant_code
                        }, tx);

                    // 3. Update bed_status: old bed vacant, new bed occupied
                    // 3. Update bed_status: old bed -> immediately AVAILABLE (transfer, not discharge),
                    //    new bed -> OCCUPIED
                    await _bedStatusCls.MarkVacant(db, tx, oldBedcode, ipId.Value, data.tenant_code!);
                    await _bedStatusCls.InsertOccupied(
                        db, tx, newBedcode, ipId.Value, (string)ip.ip_no, (decimal)ip.custid,
                        DateTime.UtcNow, data.tenant_code!);


                }
                else
                {
                    // No linked ip_registration — just log the transfer row as-is
                    var id = await db.InsertAsync(data, tx);
                    data.transferid = id;
                }

                tx.Commit();

                if (ipId.HasValue)
                    await _unbilledCls.RecalculateRoomRent(ipId.Value, data.tenant_code!);

                return "Success";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL TRANSFERS FOR A PATIENT (by custid)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> GetByCustId(decimal custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE custid       = @custid
                           AND tenant_code    = @tenant_code
                           ORDER BY transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { custid, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY VISIT (lastvisitid)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> GetByVisit(string lastvisitid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE lastvisitid  = @lastvisitid
                           AND tenant_code    = @tenant_code
                           ORDER BY transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { lastvisitid, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET LATEST TRANSFER (current bed for a visit)
        // ─────────────────────────────────────────
        public async Task<BedTransferModel?> GetLatestByVisit(string lastvisitid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE lastvisitid  = @lastvisitid
                           AND tenant_code    = @tenant_code
                           ORDER BY transferdate DESC
                           LIMIT 1";

            return await db.QueryFirstOrDefaultAsync<BedTransferModel>(
                sql, new { lastvisitid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.bed_transfer
                           WHERE tenant_code = @tenant_code
                           ORDER BY transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET CURRENTLY OCCUPIED BEDS (not checked out)
        // ─────────────────────────────────────────
        public async Task<List<BedTransferModel>> GetActiveAdmissions(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT DISTINCT ON (lastvisitid) *
                           FROM public.bed_transfer
                           WHERE tenant_code = @tenant_code
                           ORDER BY lastvisitid, transferdate DESC";

            var res = await db.QueryAsync<BedTransferModel>(sql, new { tenant_code });
            return res.Where(x => x.ischeckout != true).ToList();
        }
    }
}