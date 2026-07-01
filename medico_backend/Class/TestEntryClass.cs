using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TestClass
    {
        private readonly string db_conn;

        public TestClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─── INSERT ───────────────────────────────────────────────────────────
        // Returns inserted tcode + list of testresultids (indexed by ResultRows)
        // so the controller can upload per-row images after the transaction.

        public async Task<(string result, long? tcode, List<Guid>? resultIds)> Insert_Test(
            TestInsertDto dto, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                db.Open();
                using var tx = db.BeginTransaction();
                try
                {
                    // ── test_master ──────────────────────────────────────────
                    var master = dto.TestMaster;
                    master.tenant_code = tenant_code;
                    master.computercode = 1;
                    master.entereddate = DateTimeOffset.UtcNow;
                    master.ibsdate = DateTimeOffset.UtcNow;
                    master.deleted = false;

                    var insertedTcode = await db.InsertAsync(master, tx);

                    var insertedResultIds = new List<Guid>();

                    // ── result rows ──────────────────────────────────────────
                    if (dto.ResultRows?.Count > 0)
                    {
                        foreach (var row in dto.ResultRows)
                        {
                            var trm = row.ResultMaster!;
                            trm.testresultid = Guid.NewGuid();
                            trm.trguid = Guid.NewGuid();
                            trm.tcode = insertedTcode;
                            trm.tenant_code = tenant_code;
                            trm.entereddate = DateTimeOffset.UtcNow;
                            trm.ibsdate = DateTimeOffset.UtcNow;
                            trm.deleted = false;
                            trm.usercode = master.usercode;
                            trm.computercode = master.computercode;
                            trm.testimage = null; // set after S3 upload by controller
                            await db.InsertAsync(trm, tx);

                            // track id for controller image upload
                            insertedResultIds.Add(trm.testresultid);

                            // ── test_result_properties ───────────────────────
                            if (row.ResultProperties != null)
                            {
                                var trp = row.ResultProperties;
                                trp.trpid = Guid.NewGuid();
                                trp.testresultid = trm.testresultid;
                                trp.entereddate = DateTimeOffset.UtcNow;
                                trp.tenant_code = tenant_code;
                                await db.InsertAsync(trp, tx);
                            }

                            // ── test_result_calculatedformula ────────────────
                            if (row.CalculatedFormulas?.Count > 0)
                            {
                                foreach (var cf in row.CalculatedFormulas)
                                {
                                    cf.trcfid = Guid.NewGuid();
                                    cf.testresultid = trm.testresultid;
                                    cf.entereddate = DateTimeOffset.UtcNow;
                                    cf.tenant_code = tenant_code;
                                }
                                await db.InsertAsync(row.CalculatedFormulas, tx);
                            }

                            // ── test_result_detailednormalvalues ─────────────
                            if (row.DetailedNormalValues?.Count > 0)
                            {
                                foreach (var dn in row.DetailedNormalValues)
                                {
                                    dn.trdnid = Guid.NewGuid();
                                    dn.testresultid = trm.testresultid;
                                    dn.entereddate = DateTimeOffset.UtcNow;
                                    dn.tenant_code = tenant_code;
                                }
                                await db.InsertAsync(row.DetailedNormalValues, tx);
                            }

                            // ── test_result_textnormalvalues ─────────────────
                            if (row.TextNormalValues?.Count > 0)
                            {
                                foreach (var tn in row.TextNormalValues)
                                {
                                    tn.trtid = Guid.NewGuid();
                                    tn.testresultid = trm.testresultid;
                                    tn.entereddate = DateTimeOffset.UtcNow;
                                    tn.tenant_code = tenant_code;
                                }
                                await db.InsertAsync(row.TextNormalValues, tx);
                            }
                        }
                    }

                    tx.Commit();
                    return ("Success", insertedTcode, insertedResultIds);
                }
                catch (Exception ex) { tx.Rollback(); return (ex.Message, null, null); }
            }
            catch (Exception ex) { return (ex.Message, null, null); }
        }

        // ─── RESULT IMAGE HELPERS ─────────────────────────────────────────────

        /// <summary>Updates S3 key on test_result_master after upload.</summary>
        public async Task UpdateResultImage(
            Guid testresultid, string tenant_code, string imageKey)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                await db.ExecuteAsync(
                    @"UPDATE test_result_master
                         SET testimage    = @imageKey
                       WHERE testresultid = @testresultid
                         AND tenant_code  = @tenant_code",
                    new { testresultid, tenant_code, imageKey });
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"UpdateResultImage failed for testresultid={testresultid}: {ex.Message}", ex);
            }
        }

        /// <summary>Reads current S3 key from test_result_master before replace/delete.</summary>
        public async Task<string?> GetResultImage(Guid testresultid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                return await db.ExecuteScalarAsync<string?>(
                    @"SELECT testimage
                        FROM test_result_master
                       WHERE testresultid = @testresultid
                         AND tenant_code  = @tenant_code
                       LIMIT 1",
                    new { testresultid, tenant_code });
            }
            catch { return null; }
        }

        // ─── UPDATE ───────────────────────────────────────────────────────────

        public async Task<string> Update_Test(TestInsertDto dto, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                db.Open();
                using var tx = db.BeginTransaction();
                try
                {
                    // ── test_master ──────────────────────────────────────────
                    var master = dto.TestMaster;
                    master.tenant_code = tenant_code;
                    master.ibsdate = DateTimeOffset.UtcNow;
                    master.deleted = false;

                    var updated = await db.UpdateAsync(master, tx);
                    if (!updated)
                        throw new Exception(
                            $"test_master with tcode={master.tcode} not found.");

                    // ── result rows ──────────────────────────────────────────
                    if (dto.ResultRows?.Count > 0)
                    {
                        foreach (var row in dto.ResultRows)
                        {
                            var trm = row.ResultMaster!;
                            bool isNewRow = trm.trcode <= 0;

                            trm.tcode = master.tcode;
                            trm.tenant_code = tenant_code;
                            trm.ibsdate = DateTimeOffset.UtcNow;
                            trm.deleted = false;
                            trm.usercode = master.usercode;
                            trm.computercode = master.computercode;

                            if (isNewRow)
                            {
                                trm.testresultid = Guid.NewGuid();
                                trm.trguid = Guid.NewGuid();
                                trm.entereddate = DateTimeOffset.UtcNow;
                                trm.testimage = null; // controller sets after upload
                                await db.InsertAsync(trm, tx);
                            }
                            else
                            {
                                // preserve existing image — don't overwrite with null
                                var existingImage = await db.ExecuteScalarAsync<string?>(
                                    @"SELECT testimage FROM test_result_master
                                       WHERE testresultid = @id AND tenant_code = @t",
                                    new { id = trm.testresultid, t = tenant_code }, tx);

                                trm.testimage = existingImage;
                                await db.UpdateAsync(trm, tx);

                                // delete child rows for clean re-insert
                                var p = new { id = trm.testresultid };
                                await db.ExecuteAsync(
                                    "DELETE FROM test_result_properties           WHERE testresultid=@id", p, tx);
                                await db.ExecuteAsync(
                                    "DELETE FROM test_result_calculatedformula    WHERE testresultid=@id", p, tx);
                                await db.ExecuteAsync(
                                    "DELETE FROM test_result_detailednormalvalues WHERE testresultid=@id", p, tx);
                                await db.ExecuteAsync(
                                    "DELETE FROM test_result_textnormalvalues     WHERE testresultid=@id", p, tx);
                            }

                            // ── test_result_properties ───────────────────────
                            if (row.ResultProperties != null)
                            {
                                var trp = row.ResultProperties;
                                trp.trpid = Guid.NewGuid();
                                trp.testresultid = trm.testresultid;
                                trp.entereddate = DateTimeOffset.UtcNow;
                                trp.tenant_code = tenant_code;
                                await db.InsertAsync(trp, tx);
                            }

                            // ── test_result_calculatedformula ────────────────
                            if (row.CalculatedFormulas?.Count > 0)
                            {
                                foreach (var cf in row.CalculatedFormulas)
                                {
                                    cf.trcfid = Guid.NewGuid();
                                    cf.testresultid = trm.testresultid;
                                    cf.entereddate = DateTimeOffset.UtcNow;
                                    cf.tenant_code = tenant_code;
                                }
                                await db.InsertAsync(row.CalculatedFormulas, tx);
                            }

                            // ── test_result_detailednormalvalues ─────────────
                            if (row.DetailedNormalValues?.Count > 0)
                            {
                                foreach (var dn in row.DetailedNormalValues)
                                {
                                    dn.trdnid = Guid.NewGuid();
                                    dn.testresultid = trm.testresultid;
                                    dn.entereddate = DateTimeOffset.UtcNow;
                                    dn.tenant_code = tenant_code;
                                }
                                await db.InsertAsync(row.DetailedNormalValues, tx);
                            }

                            // ── test_result_textnormalvalues ─────────────────
                            if (row.TextNormalValues?.Count > 0)
                            {
                                foreach (var tn in row.TextNormalValues)
                                {
                                    tn.trtid = Guid.NewGuid();
                                    tn.testresultid = trm.testresultid;
                                    tn.entereddate = DateTimeOffset.UtcNow;
                                    tn.tenant_code = tenant_code;
                                }
                                await db.InsertAsync(row.TextNormalValues, tx);
                            }
                        }
                    }

                    tx.Commit();
                    return "Success";
                }
                catch (Exception ex) { tx.Rollback(); return ex.Message; }
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─── DELETE (hard) ────────────────────────────────────────────────────

        public async Task<string> Delete_Test(int tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                db.Open();
                using var tx = db.BeginTransaction();
                try
                {
                    var resultIds = (await db.QueryAsync<Guid>(
                        @"SELECT testresultid FROM test_result_master
                          WHERE tcode=@tcode AND tenant_code=@tenant_code",
                        new { tcode, tenant_code }, tx)).ToArray();

                    if (resultIds.Length > 0)
                    {
                        var p = new { ids = resultIds };
                        await db.ExecuteAsync(
                            "DELETE FROM test_result_calculatedformula    WHERE testresultid=ANY(@ids)", p, tx);
                        await db.ExecuteAsync(
                            "DELETE FROM test_result_detailednormalvalues WHERE testresultid=ANY(@ids)", p, tx);
                        await db.ExecuteAsync(
                            "DELETE FROM test_result_textnormalvalues     WHERE testresultid=ANY(@ids)", p, tx);
                        await db.ExecuteAsync(
                            "DELETE FROM test_result_properties           WHERE testresultid=ANY(@ids)", p, tx);
                        await db.ExecuteAsync(
                            "DELETE FROM test_result_master               WHERE testresultid=ANY(@ids)", p, tx);
                    }

                    await db.ExecuteAsync(
                        "DELETE FROM test_master WHERE tcode=@tcode AND tenant_code=@tenant_code",
                        new { tcode, tenant_code }, tx);

                    tx.Commit();
                    return "Success";
                }
                catch (Exception ex) { tx.Rollback(); return ex.Message; }
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─── SOFT DELETE ──────────────────────────────────────────────────────

        public async Task<string> SoftDelete_Test(int tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                await db.ExecuteAsync(
                    "UPDATE test_master SET deleted=true WHERE tcode=@tcode AND tenant_code=@tenant_code",
                    new { tcode, tenant_code });
                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─── GET ALL ──────────────────────────────────────────────────────────

        public async Task<IList<TestMasterModel>> Get_Data(string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                var res = await db.QueryAsync<TestMasterModel>(
                    @"SELECT * FROM test_master
                      WHERE deleted=false AND tenant_code=@tenant_code
                      ORDER BY orderno",
                    new { tenant_code });
                return res.ToList();
            }
            catch { return []; }
        }

        // ─── GET FULL RESULT BY TCODE ─────────────────────────────────────────

        public async Task<object?> Get_TestResult(long tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var resultMasterList = (await db.QueryAsync<test_result_master>(
                    @"SELECT * FROM test_result_master
                      WHERE tcode=@tcode AND tenant_code=@tenant_code AND deleted=false
                      ORDER BY slno",
                    new { tcode, tenant_code })).ToList();

                if (resultMasterList.Count == 0) return null;

                var resultIds = resultMasterList
                    .Select(x => x.testresultid)
                    .ToArray();

                var props = (await db.QueryAsync<test_result_properties>(
                    @"SELECT * FROM test_result_properties
                      WHERE testresultid=ANY(@resultIds) AND tenant_code=@tenant_code",
                    new { resultIds, tenant_code })).ToList();

                var calcFormulas = (await db.QueryAsync<TestResultCalculatedformula>(
                    @"SELECT * FROM test_result_calculatedformula
                      WHERE testresultid=ANY(@resultIds) AND tenant_code=@tenant_code",
                    new { resultIds, tenant_code })).ToList();

                var detailedNormal = (await db.QueryAsync<test_result_detailednormalvalues>(
                    @"SELECT * FROM test_result_detailednormalvalues
                      WHERE testresultid=ANY(@resultIds) AND tenant_code=@tenant_code
                      ORDER BY testresultid, sno",
                    new { resultIds, tenant_code })).ToList();

                var textNormal = (await db.QueryAsync<test_result_textnormalvalues>(
                    @"SELECT * FROM test_result_textnormalvalues
                      WHERE testresultid=ANY(@resultIds) AND tenant_code=@tenant_code",
                    new { resultIds, tenant_code })).ToList();

                var grouped = resultMasterList.Select(master => new
                {
                    ResultMaster = master,         // includes testimage
                    ResultProperties = props
                                         .FirstOrDefault(p => p.testresultid == master.testresultid),
                    CalculatedFormulas = calcFormulas
                                         .Where(cf => cf.testresultid == master.testresultid)
                                         .OrderBy(cf => cf.sex).ToList(),
                    DetailedNormalValues = detailedNormal
                                         .Where(dn => dn.testresultid == master.testresultid)
                                         .OrderBy(dn => dn.sno).ToList(),
                    TextNormalValues = textNormal
                                         .Where(tn => tn.testresultid == master.testresultid)
                                         .OrderBy(tn => tn.sex).ToList()
                }).ToList();

                return new { tcode, TotalResults = grouped.Count, Results = grouped };
            }
            catch { return null; }
        }

        public async Task<List<Guid>> GetAllResultIds(int tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                var ids = await db.QueryAsync<Guid>(
                    @"SELECT testresultid FROM test_result_master
               WHERE tcode=@tcode AND tenant_code=@tenant_code",
                    new { tcode, tenant_code });
                return ids.ToList();
            }
            catch { return []; }
        }

        // ─── GET ALL RESULT IMAGE KEYS BY TCODE ──────────────────────────────────

        public async Task<List<string?>> GetResultImagesByTcode(int tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                var keys = await db.QueryAsync<string?>(
                    @"SELECT testimage 
                FROM test_result_master
               WHERE tcode       = @tcode
                 AND tenant_code = @tenant_code
                 AND testimage IS NOT NULL",
                    new { tcode, tenant_code });
                return keys.ToList();
            }
            catch { return []; }
        }
    }
}