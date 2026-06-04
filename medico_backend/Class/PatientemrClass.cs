using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class PatientEMRClass
    {
        private readonly string _db_conn;

        public PatientEMRClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────────────────────
        // GENERATE INVST NO
        // Format: EMR/2026/06/0001 — resets each month per tenant
        // ─────────────────────────────────────────────────────────
        private async Task<(int invstno, string invstnoprint)> GenerateInvstNo(
            IDbConnection db, string tenant_code)
        {
            string sql = @"SELECT COALESCE(MAX(invstno), 0) + 1
                           FROM   patientproblem
                           WHERE  tenant_code = @tenant_code
                           AND    deleted      = false
                           AND    EXTRACT(YEAR  FROM problemdate) = @year
                           AND    EXTRACT(MONTH FROM problemdate) = @month";

            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            int next = await db.ExecuteScalarAsync<int>(sql,
                new { tenant_code, year = now.Year, month = now.Month });

            string print = $"EMR/{year}/{month}/{next:D4}";
            return (next, print);
        }

        // ─────────────────────────────────────────────────────────
        // GET PROBLEM MASTER LIST
        // ─────────────────────────────────────────────────────────
        public async Task<List<ProblemMasterModel>> GetProblemMasterList(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            return (await db.QueryAsync<ProblemMasterModel>(
                @"SELECT * FROM problem_master
                  WHERE deleted = false
                  AND   (tenant_code = @tenant_code OR tenant_code IS NULL)
                  ORDER BY orderNo",
                new { tenant_code })).ToList();
        }

        // ─────────────────────────────────────────────────────────
        // GET EMR TEMPLATE
        // Returns all questions for a pb_code with their possibilities.
        // Frontend calls this to build the dynamic form.
        // ─────────────────────────────────────────────────────────
        public async Task<List<ProblemReportMasterModel>> GetEMRTemplate(
            int pbcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var questions = (await db.QueryAsync<ProblemReportMasterModel>(
                @"SELECT * FROM problem_report_master
                  WHERE  pbcode       = @pbcode
                  AND    (tenant_code = @tenant_code OR tenant_code IS NULL)
                  ORDER  BY slno",
                new { pbcode, tenant_code })).ToList();

            if (!questions.Any()) return questions;

            var prmids = questions.Select(q => q.prmid).ToArray();

            var possibilities = (await db.QueryAsync<ProblemReportMasterPossibilityModel>(
                @"SELECT * FROM problem_report_master_possibilities
                  WHERE  prmid = ANY(@prmids)
                  ORDER  BY sno",
                new { prmids })).ToList();

            foreach (var q in questions)
                q.possibilities = possibilities
                    .Where(p => p.prmid == q.prmid)
                    .ToList();

            return questions;
        }

        // ─────────────────────────────────────────────────────────
        // SAVE EMR  (INSERT or UPDATE)
        // Detect by presence of problemid in request.
        //
        // Child tables are always delete + re-insert so the
        // frontend never has to diff — it just sends the full list.
        // ─────────────────────────────────────────────────────────
        public async Task<string> SaveEMR(SaveEMRRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // ── Validate OP visit ──────────────────────────
                // Use CAST(...AS uuid) — Npgsql misparses @param::uuid
                // because it treats :: after a parameter as an operator.
                var opExists = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM op_registration
                      WHERE  op_id       = CAST(@opvisitid AS uuid)
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { req.opvisitid, tenant_code });

                if (opExists == 0)
                    return "OP Visit not found";

                bool isNew = string.IsNullOrWhiteSpace(req.problemid);
                string problemid = isNew
                    ? Guid.NewGuid().ToString()
                    : req.problemid!;

                // ── 1. Upsert patientproblem ───────────────────
                if (isNew)
                {
                    var (invstno, invstnoprint) =
                        await GenerateInvstNo(db, tenant_code);

                    await db.ExecuteAsync(
                        @"INSERT INTO patientproblem
                          (problemid, problemdate, invstno, invstnoprint,
                           custid, dcode, hdcode, opvisitid,
                           notes, problemtype, deleted, tenant_code)
                          VALUES
                          (@problemid, NOW(), @invstno, @invstnoprint,
                           @custid, @dcode, @hdcode, @opvisitid,
                           @notes, @problemtype, false, @tenant_code)",
                        new
                        {
                            problemid,
                            invstno,
                            invstnoprint,
                            req.custid,
                            req.dcode,
                            req.hdcode,
                            req.opvisitid,
                            req.notes,
                            req.problemtype,
                            tenant_code
                        });
                }
                else
                {
                    var exists = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(1) FROM patientproblem
                          WHERE  problemid   = @problemid
                          AND    tenant_code = @tenant_code
                          AND    deleted     = false",
                        new { problemid, tenant_code });

                    if (exists == 0) return "Problem record not found";

                    await db.ExecuteAsync(
                        @"UPDATE patientproblem
                          SET    hdcode      = @hdcode,
                                 dcode       = @dcode,
                                 notes       = @notes,
                                 problemtype = @problemtype,
                                 problemdate = NOW()
                          WHERE  problemid   = @problemid
                          AND    tenant_code = @tenant_code
                          AND    deleted     = false",
                        new
                        {
                            problemid,
                            req.hdcode,
                            req.dcode,
                            req.notes,
                            req.problemtype,
                            tenant_code
                        });
                }

                // ── 2. patientproblem_problem ──────────────────
                await db.ExecuteAsync(
                    "DELETE FROM patientproblem_problem WHERE problemid = @problemid",
                    new { problemid });

                for (int i = 0; i < req.problems.Count; i++)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO patientproblem_problem
                          (ppid, problemid, sno, pbcode)
                          VALUES (gen_random_uuid()::text, @problemid, @sno, @pbcode)",
                        new { problemid, sno = i + 1, req.problems[i].pbcode });
                }

                // ── 3. patientproblem_diagnosis ────────────────
                await db.ExecuteAsync(
                    "DELETE FROM patientproblem_diagnosis WHERE problemid = @problemid",
                    new { problemid });

                for (int i = 0; i < req.diagnoses.Count; i++)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO patientproblem_diagnosis
                          (ppdid, problemid, sno, dccode)
                          VALUES (gen_random_uuid()::text, @problemid, @sno, @dccode)",
                        new { problemid, sno = i + 1, req.diagnoses[i].dccode });
                }

                // ── 4. patientproblem_symptoms +
                //       patientproblem_symptompossibilities ──────
                await db.ExecuteAsync(
                    "DELETE FROM patientproblem_symptompossibilities WHERE problemid = @problemid",
                    new { problemid });

                await db.ExecuteAsync(
                    "DELETE FROM patientproblem_symptoms WHERE problemid = @problemid",
                    new { problemid });

                foreach (var sym in req.symptoms)
                {
                    string pbsid = Guid.NewGuid().ToString();

                    await db.ExecuteAsync(
                        @"INSERT INTO patientproblem_symptoms
                          (pbsid, problemid, prmid, slno, pbcode,
                           resultvaluetype, question, answer, answerprbpid,
                           frompbcode, fromquestionid, iscombineanswer,
                           resultvaluetype1, blockrtf)
                          VALUES
                          (@pbsid, @problemid, @prmid, @slno, @pbcode,
                           @resultvaluetype, @question, @answer, @answerprbpid,
                           @frompbcode, @fromquestionid, @iscombineanswer,
                           @resultvaluetype1, @blockrtf)",
                        new
                        {
                            pbsid,
                            problemid,
                            sym.prmid,
                            sym.slno,
                            sym.pbcode,
                            sym.resultvaluetype,
                            sym.question,
                            sym.answer,
                            sym.answerprbpid,
                            sym.frompbcode,
                            sym.fromquestionid,
                            sym.iscombineanswer,
                            sym.resultvaluetype1,
                            sym.blockrtf
                        });

                    // ── 5. Possibilities for this row ──────────
                    if (sym.selected_possibilities == null
                        || !sym.selected_possibilities.Any())
                        continue;

                    foreach (var poss in sym.selected_possibilities)
                    {
                        await db.ExecuteAsync(
                            @"INSERT INTO patientproblem_symptompossibilities
                              (pbspid, problemid, pbcode, prbpid, prmid,
                               sno, possibility, ""Type"", typetext, sympsno, isselected)
                              VALUES
                              (gen_random_uuid()::text, @problemid, @pbcode, @prbpid, @prmid,
                               @sno, @possibility, @type, @typetext, @sympsno, @isselected)",
                            new
                            {
                                problemid,
                                sym.pbcode,
                                poss.prbpid,
                                prmid = sym.prmid,
                                poss.sno,
                                poss.possibility,
                                type = poss.type,
                                poss.typetext,
                                poss.sympsno,
                                poss.isselected
                            });
                    }
                }

                return $"Success|ProblemId:{problemid}|InvstNo:{(isNew ? "generated" : "updated")}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // GET EMR BY PROBLEM ID — full record with all child rows
        // ─────────────────────────────────────────────────────────
        public async Task<PatientProblemModel?> GetEMRByProblemId(
            string problemid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var problem = await db.QueryFirstOrDefaultAsync<PatientProblemModel>(
                @"SELECT * FROM patientproblem
                  WHERE  problemid   = @problemid
                  AND    tenant_code = @tenant_code
                  AND    deleted     = false",
                new { problemid, tenant_code });

            if (problem == null) return null;

            await HydrateChildren(db, problem);
            return problem;
        }

        // ─────────────────────────────────────────────────────────
        // GET EMR BY VISIT — all records for one OP visit
        // ─────────────────────────────────────────────────────────
        public async Task<List<PatientProblemModel>> GetEMRByVisit(
            string opvisitid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var problems = (await db.QueryAsync<PatientProblemModel>(
                @"SELECT * FROM patientproblem
                  WHERE  opvisitid   = @opvisitid
                  AND    tenant_code = @tenant_code
                  AND    deleted     = false
                  ORDER  BY problemdate",
                new { opvisitid, tenant_code })).ToList();

            foreach (var p in problems)
                await HydrateChildren(db, p);

            return problems;
        }

        // ─────────────────────────────────────────────────────────
        // GET CASE HISTORY BY CUSTOMER
        // Flat list — replaces old spViewCaseHistory stored proc
        // ─────────────────────────────────────────────────────────
        public async Task<List<CaseHistoryItem>> GetCaseHistory(
            decimal custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
                SELECT
                    pp.problemid,
                    pp.problemdate,
                    pp.invstnoprint,
                    pm.name          AS problemname,
                    ps.question,
                    ps.answer,
                    ps.resultvaluetype,
                    ps.pbcode,
                    ps.slno
                FROM patientproblem             pp
                JOIN patientproblem_problem      ppp ON ppp.problemid = pp.problemid
                JOIN problem_master              pm  ON pm.pbcode     = ppp.pbcode
                JOIN patientproblem_symptoms     ps  ON ps.problemid  = pp.problemid
                                                    AND ps.pbcode     = ppp.pbcode
                WHERE pp.custid      = @custid
                AND   pp.tenant_code = @tenant_code
                AND   pp.deleted     = false
                ORDER BY pp.problemdate DESC, ppp.pbcode, ps.slno";

            return (await db.QueryAsync<CaseHistoryItem>(
                sql, new { custid, tenant_code })).ToList();
        }

        // ─────────────────────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────────────────────
        public async Task<string> DeleteEMR(string problemid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE patientproblem SET deleted = true
                      WHERE  problemid   = @problemid
                      AND    tenant_code = @tenant_code
                      AND    deleted     = false",
                    new { problemid, tenant_code });

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // PRIVATE HELPER — load child rows into a problem record
        // ─────────────────────────────────────────────────────────
        private async Task HydrateChildren(IDbConnection db, PatientProblemModel p)
        {
            p.problems = (await db.QueryAsync<ProblemCodeItem>(
                @"SELECT pbcode FROM patientproblem_problem
                  WHERE problemid = @problemid ORDER BY sno",
                new { p.problemid })).ToList();

            p.diagnoses = (await db.QueryAsync<DiagnosisCodeItem>(
                @"SELECT dccode FROM patientproblem_diagnosis
                  WHERE problemid = @problemid ORDER BY sno",
                new { p.problemid })).ToList();

            var rawSymptoms = (await db.QueryAsync<PatientProblemSymptomModel>(
                @"SELECT * FROM patientproblem_symptoms
                  WHERE problemid = @problemid ORDER BY slno",
                new { p.problemid })).ToList();

            // Load all possibilities for this problem in one query
            var allPoss = (await db.QueryAsync<PatientProblemSymptomPossibilityModel>(
                @"SELECT pbspid, problemid, pbcode, prbpid, prmid,
                         sno, possibility, ""Type"" AS type, typetext, sympsno, isselected
                  FROM   patientproblem_symptompossibilities
                  WHERE  problemid = @problemid",
                new { p.problemid })).ToList();

            p.symptoms = rawSymptoms.Select(s => new SymptomAnswerItem
            {
                prmid = s.prmid,
                slno = s.slno,
                pbcode = s.pbcode,
                resultvaluetype = s.resultvaluetype,
                question = s.question,
                answer = s.answer,
                answerprbpid = s.answerprbpid,
                frompbcode = s.frompbcode,
                fromquestionid = s.fromquestionid,
                iscombineanswer = s.iscombineanswer,
                resultvaluetype1 = s.resultvaluetype1,
                blockrtf = s.blockrtf,
                selected_possibilities = allPoss
                    .Where(pp => pp.prmid == s.prmid)
                    .Select(pp => new SymptomPossibilityItem
                    {
                        prbpid = pp.prbpid,
                        sno = pp.sno,
                        possibility = pp.possibility,
                        type = pp.type,
                        typetext = pp.typetext,
                        sympsno = pp.sympsno,
                        isselected = pp.isselected ?? false
                    }).ToList()
            }).ToList();
        }
    }
}