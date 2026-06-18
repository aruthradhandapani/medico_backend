using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class NewOPCaseSheetClass
    {
        private readonly string _db_conn;

        public NewOPCaseSheetClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────────────────────
        // GENERATE PRESCRIPTION CODE
        // Format: PR/2026/06/0001 — resets each month per tenant
        // ─────────────────────────────────────────────────────────
        private async Task<string> GeneratePrCode(IDbConnection db, string tenant_code)
        {
            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            string sql = @"SELECT COALESCE(MAX(
                               CAST(SPLIT_PART(pr_code, '/', 4) AS INT)
                           ), 0) + 1
                           FROM op_prescription_master
                           WHERE tenant_code = @tenant_code
                           AND   isdeleted   = false
                           AND   SPLIT_PART(pr_code, '/', 2) = @year
                           AND   SPLIT_PART(pr_code, '/', 3) = @month";

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month });
            return $"PR/{year}/{month}/{next:D4}";
        }

        // ─────────────────────────────────────────────────────────
        // GENERATE INVESTIGATION CODE
        // Format: INV/2026/06/0001
        // ─────────────────────────────────────────────────────────
        private async Task<string> GenerateInvCode(IDbConnection db, string tenant_code)
        {
            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            string sql = @"SELECT COALESCE(MAX(
                               CAST(SPLIT_PART(inv_code, '/', 4) AS INT)
                           ), 0) + 1
                           FROM op_investigation_master
                           WHERE tenant_code = @tenant_code
                           AND   isdeleted   = false
                           AND   SPLIT_PART(inv_code, '/', 2) = @year
                           AND   SPLIT_PART(inv_code, '/', 3) = @month";

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month });
            return $"INV/{year}/{month}/{next:D4}";
        }

        // ─────────────────────────────────────────────────────────
        // SAVE OP CASE SHEET
        // ─────────────────────────────────────────────────────────
        public async Task<string> SaveCaseSheet(SaveCaseSheetRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // ── 1. Validate OP visit ──────────────────────────
                var opExists = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM op_registration
                      WHERE  op_id       = CAST(@op_id AS uuid)
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { req.op_id, tenant_code });

                if (opExists == 0)
                    return "OP Registration not found";

                // ── 2. Upsert op_case_sheet ───────────────────────
                bool isNew = string.IsNullOrWhiteSpace(req.sheet_id);
                Guid sheetId = isNew ? Guid.NewGuid() : Guid.Parse(req.sheet_id!);

                if (isNew)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO op_case_sheet
                          (sheet_id, op_id, custid, dcode, visit_date,
                           chief_complaint, symptoms, examination,
                           advise, notes, followup_date, followup_notes,
                           is_consulted, sheet_status,
                           tenant_code, isdeleted, created_at, updated_at)
                          VALUES
                          (@sheet_id, CAST(@op_id AS uuid), @custid, @dcode, NOW()::date,
                           @chief_complaint, @symptoms, @examination,
                           @advise, @notes, @followup_date, @followup_notes,
                           false, @sheet_status,
                           @tenant_code, false, NOW(), NOW())",
                        new
                        {
                            sheet_id = sheetId,
                            req.op_id,
                            req.custid,
                            req.dcode,
                            req.chief_complaint,
                            req.symptoms,
                            req.examination,
                            req.advise,
                            req.notes,
                            req.followup_date,
                            req.followup_notes,
                            req.sheet_status,
                            tenant_code
                        });
                }
                else
                {
                    var exists = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(1) FROM op_case_sheet
                          WHERE  sheet_id    = @sheet_id
                          AND    tenant_code = @tenant_code
                          AND    isdeleted   = false",
                        new { sheet_id = sheetId, tenant_code });

                    if (exists == 0) return "Case sheet not found";

                    await db.ExecuteAsync(
                        @"UPDATE op_case_sheet
                          SET    chief_complaint = @chief_complaint,
                                 symptoms        = @symptoms,
                                 examination     = @examination,
                                 advise          = @advise,
                                 notes           = @notes,
                                 followup_date   = @followup_date,
                                 followup_notes  = @followup_notes,
                                 sheet_status    = @sheet_status,
                                 updated_at      = NOW()
                          WHERE  sheet_id    = @sheet_id
                          AND    tenant_code = @tenant_code",
                        new
                        {
                            sheet_id = sheetId,
                            req.chief_complaint,
                            req.symptoms,
                            req.examination,
                            req.advise,
                            req.notes,
                            req.followup_date,
                            req.followup_notes,
                            req.sheet_status,
                            tenant_code
                        });
                }

                // ── 3. Symptoms list (delete + re-insert) ─────────
                await db.ExecuteAsync(
                    "DELETE FROM op_case_sheet_symptoms WHERE sheet_id = @sheet_id",
                    new { sheet_id = sheetId });

                foreach (var s in req.symptom_list)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO op_case_sheet_symptoms
                          (symptom_id, sheet_id, op_id, custid, sno,
                           symptom_text, duration, severity, notes, tenant_code, created_at)
                          VALUES
                          (gen_random_uuid(), @sheet_id, CAST(@op_id AS uuid), @custid, @sno,
                           @symptom_text, @duration, @severity, @snotes, @tenant_code, NOW())",
                        new
                        {
                            sheet_id = sheetId,
                            req.op_id,
                            req.custid,
                            s.sno,
                            s.symptom_text,
                            s.duration,
                            s.severity,
                            snotes = s.notes,
                            tenant_code
                        });
                }

                // ── 4. Diagnosis list (delete + re-insert) ────────
                await db.ExecuteAsync(
                    "DELETE FROM op_case_sheet_diagnosis WHERE sheet_id = @sheet_id",
                    new { sheet_id = sheetId });

                foreach (var d in req.diagnosis_list)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO op_case_sheet_diagnosis
                          (diag_id, sheet_id, op_id, custid, dcode, visit_date, sno,
                           icd_code, icd_description, diagnosis_text,
                           diagnosis_type, condition_type, severity, status,
                           tenant_code, isdeleted, created_at)
                          VALUES
                          (gen_random_uuid(), @sheet_id, CAST(@op_id AS uuid),
                           @custid, @dcode, NOW()::date, @sno,
                           @icd_code, @icd_description, @diagnosis_text,
                           @diagnosis_type, @condition_type, @severity, @status,
                           @tenant_code, false, NOW())",
                        new
                        {
                            sheet_id = sheetId,
                            req.op_id,
                            req.custid,
                            req.dcode,
                            d.sno,
                            d.icd_code,
                            d.icd_description,
                            d.diagnosis_text,
                            d.diagnosis_type,
                            d.condition_type,
                            d.severity,
                            d.status,
                            tenant_code
                        });
                }

                // ── 5. Prescription ───────────────────────────────
                string? prCode = null;
                if (req.prescription != null && req.prescription.items.Any())
                {
                    prCode = await SavePrescription(
                        db, req.prescription, sheetId,
                        req.op_id, req.custid, req.dcode, tenant_code);
                }

                // ── 6. Investigation ──────────────────────────────
                string? invCode = null;
                if (req.investigation != null && req.investigation.tests.Any())
                {
                    invCode = await SaveInvestigation(
                        db, req.investigation, sheetId,
                        req.op_id, req.custid, req.dcode, tenant_code);
                }

                return $"Success" +
                       $"|SheetId:{sheetId}" +
                       $"|PrCode:{prCode ?? "none"}" +
                       $"|InvCode:{invCode ?? "none"}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // SAVE PRESCRIPTION
        // ─────────────────────────────────────────────────────────
        private async Task<string> SavePrescription(
            IDbConnection db,
            CaseSheetPrescriptionRequest req,
            Guid sheetId,
            string op_id,
            decimal custid,
            int dcode,
            string tenant_code)
        {
            bool isNew = string.IsNullOrWhiteSpace(req.pr_code);
            string prCode = isNew
                ? await GeneratePrCode(db, tenant_code)
                : req.pr_code!;

            Guid prId;

            if (isNew)
            {
                prId = Guid.NewGuid();
                await db.ExecuteAsync(
                    @"INSERT INTO op_prescription_master
                      (pr_id, pr_code, sheet_id, op_id, custid, dcode, visit_date,
                       pr_date, topremarks, bottonremarks,
                       is_dispensed, tenant_code, isdeleted, created_at, updated_at)
                      VALUES
                      (@pr_id, @pr_code, @sheet_id, CAST(@op_id AS uuid),
                       @custid, @dcode, NOW()::date,
                       NOW(), @topremarks, @bottonremarks,
                       false, @tenant_code, false, NOW(), NOW())",
                    new
                    {
                        pr_id = prId,
                        pr_code = prCode,
                        sheet_id = sheetId,
                        op_id,
                        custid,
                        dcode,
                        req.topremarks,
                        req.bottonremarks,
                        tenant_code
                    });
            }
            else
            {
                var mst = await db.QueryFirstOrDefaultAsync(
                    @"SELECT pr_id FROM op_prescription_master
                      WHERE  pr_code     = @pr_code
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { pr_code = prCode, tenant_code });

                if (mst == null) throw new Exception("Prescription not found for update");
                prId = (Guid)mst.pr_id;

                await db.ExecuteAsync(
                    @"UPDATE op_prescription_master
                      SET    topremarks    = @topremarks,
                             bottonremarks = @bottonremarks,
                             updated_at    = NOW()
                      WHERE  pr_code       = @pr_code
                      AND    tenant_code   = @tenant_code
                      AND    isdeleted     = false",
                    new { pr_code = prCode, req.topremarks, req.bottonremarks, tenant_code });
            }

            // Delete + re-insert detail lines
            await db.ExecuteAsync(
                "DELETE FROM op_prescription_detail WHERE pr_id = @pr_id",
                new { pr_id = prId });

            foreach (var item in req.items)
            {
                Guid? diagId = string.IsNullOrWhiteSpace(item.diag_id)
                    ? null
                    : Guid.Parse(item.diag_id);

                await db.ExecuteAsync(
                    @"INSERT INTO op_prescription_detail
                      (pr_det_id, pr_id, pr_code, diag_id, sno,
                       drug_name, drug_code, generic_name, drug_category,
                       morning, afternoon, evening, night,
                       before_food, after_food, days, qty, route,
                       rate, mrp, is_billed, notes,
                       tenant_code, isdeleted, created_at)
                      VALUES
                      (gen_random_uuid(), @pr_id, @pr_code, @diag_id, @sno,
                       @drug_name, @drug_code, @generic_name, @drug_category,
                       @morning, @afternoon, @evening, @night,
                       @before_food, @after_food, @days, @qty, @route,
                       @rate, @mrp, false, @notes,
                       @tenant_code, false, NOW())",
                    new
                    {
                        pr_id = prId,
                        pr_code = prCode,
                        diag_id = diagId,
                        item.sno,
                        item.drug_name,
                        item.drug_code,
                        item.generic_name,
                        item.drug_category,
                        item.morning,
                        item.afternoon,
                        item.evening,
                        item.night,
                        item.before_food,
                        item.after_food,
                        item.days,
                        item.qty,
                        item.route,
                        item.rate,
                        item.mrp,
                        item.notes,
                        tenant_code
                    });
            }

            return prCode;
        }

        // ─────────────────────────────────────────────────────────
        // SAVE INVESTIGATION
        // ─────────────────────────────────────────────────────────
        private async Task<string> SaveInvestigation(
            IDbConnection db,
            CaseSheetInvestigationRequest req,
            Guid sheetId,
            string op_id,
            decimal custid,
            int dcode,
            string tenant_code)
        {
            bool isNew = string.IsNullOrWhiteSpace(req.inv_id);
            Guid invId;
            string invCode;

            if (isNew)
            {
                invId = Guid.NewGuid();
                invCode = await GenerateInvCode(db, tenant_code);

                await db.ExecuteAsync(
                    @"INSERT INTO op_investigation_master
                      (inv_id, inv_code, sheet_id, op_id, custid, dcode, visit_date,
                       inv_date, notes, is_urgent, status,
                       tenant_code, isdeleted, created_at, updated_at)
                      VALUES
                      (@inv_id, @inv_code, @sheet_id, CAST(@op_id AS uuid),
                       @custid, @dcode, NOW()::date,
                       NOW(), @notes, @is_urgent, 'ORDERED',
                       @tenant_code, false, NOW(), NOW())",
                    new
                    {
                        inv_id = invId,
                        inv_code = invCode,
                        sheet_id = sheetId,
                        op_id,
                        custid,
                        dcode,
                        req.notes,
                        is_urgent = req.is_urgent,
                        tenant_code
                    });
            }
            else
            {
                invId = Guid.Parse(req.inv_id!);
                var mst = await db.QueryFirstOrDefaultAsync(
                    @"SELECT inv_code FROM op_investigation_master
                      WHERE  inv_id      = @inv_id
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { inv_id = invId, tenant_code });

                if (mst == null) throw new Exception("Investigation not found for update");
                invCode = (string)mst.inv_code;

                await db.ExecuteAsync(
                    @"UPDATE op_investigation_master
                      SET    notes      = @notes,
                             is_urgent  = @is_urgent,
                             updated_at = NOW()
                      WHERE  inv_id      = @inv_id
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { inv_id = invId, req.notes, is_urgent = req.is_urgent, tenant_code });
            }

            // Delete + re-insert test rows
            await db.ExecuteAsync(
                "DELETE FROM op_investigation_detail WHERE inv_id = @inv_id",
                new { inv_id = invId });

            foreach (var test in req.tests)
            {
                Guid? diagId = string.IsNullOrWhiteSpace(test.diag_id)
                    ? null
                    : Guid.Parse(test.diag_id);

                await db.ExecuteAsync(
                    @"INSERT INTO op_investigation_detail
                      (inv_det_id, inv_id, diag_id, sno,
                       test_name, test_code, test_category,
                       quantity, rate, amount,
                       result_status, is_billed, tenant_code, isdeleted, created_at, updated_at)
                      VALUES
                      (gen_random_uuid(), @inv_id, @diag_id, @sno,
                       @test_name, @test_code, @test_category,
                       @quantity, @rate, @amount,
                       'PENDING', false, @tenant_code, false, NOW(), NOW())",
                    new
                    {
                        inv_id = invId,
                        diag_id = diagId,
                        test.sno,
                        test.test_name,
                        test.test_code,
                        test.test_category,
                        test.quantity,
                        test.rate,
                        test.amount,
                        tenant_code
                    });
            }

            return invCode;
        }

        // ─────────────────────────────────────────────────────────
        // FINALIZE CASE SHEET (DRAFT → FINAL)
        // ─────────────────────────────────────────────────────────
        public async Task<string> FinalizeCaseSheet(
            FinalizeCaseSheetRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_case_sheet
                      SET    sheet_status  = 'FINAL',
                             is_consulted  = @is_consulted,
                             updated_at    = NOW()
                      WHERE  sheet_id      = CAST(@sheet_id AS uuid)
                      AND    tenant_code   = @tenant_code
                      AND    isdeleted     = false",
                    new { req.sheet_id, req.is_consulted, tenant_code });

                return rows > 0 ? "Success" : "Case sheet not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // GET CASE SHEET BY OP_ID (full view)
        // ─────────────────────────────────────────────────────────
        public async Task<CaseSheetViewModel?> GetCaseSheetByVisit(
            string op_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var sheet = await db.QueryFirstOrDefaultAsync<OpCaseSheetModel>(
    @"SELECT sheet_id, op_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             chief_complaint, symptoms, examination,
             advise, notes,
             followup_date::timestamp AS followup_date,
             followup_notes, is_consulted, sheet_status,
             tenant_code, isdeleted, created_at, updated_at
      FROM   op_case_sheet
      WHERE  op_id       = CAST(@op_id AS uuid)
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY created_at DESC
      LIMIT  1",
    new { op_id, tenant_code });

            if (sheet == null) return null;

            var vm = new CaseSheetViewModel
            {
                sheet_id = sheet.sheet_id.ToString(),
                op_id = op_id,
                custid = sheet.custid,
                dcode = sheet.dcode,
                visit_date = sheet.visit_date,
                chief_complaint = sheet.chief_complaint,
                symptoms = sheet.symptoms,
                examination = sheet.examination,
                advise = sheet.advise,
                notes = sheet.notes,
                followup_date = sheet.followup_date,
                followup_notes = sheet.followup_notes,
                is_consulted = sheet.is_consulted,
                sheet_status = sheet.sheet_status
            };

            // Symptoms
            vm.symptom_list = (await db.QueryAsync<OpCaseSheetSymptomModel>(
                @"SELECT * FROM op_case_sheet_symptoms
                  WHERE  sheet_id = @sheet_id
                  ORDER  BY sno",
                new { sheet_id = sheet.sheet_id })).ToList();

            // Diagnosis
            vm.diagnosis_list = (await db.QueryAsync<OpCaseSheetDiagnosisModel>(
    @"SELECT diag_id, sheet_id, op_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             sno, icd_code, icd_description, diagnosis_text,
             diagnosis_type, condition_type, severity, status,
             tenant_code, isdeleted, created_at
      FROM   op_case_sheet_diagnosis
      WHERE  sheet_id  = @sheet_id
      AND    isdeleted = false
      ORDER  BY sno",
    new { sheet_id = sheet.sheet_id })).ToList();

            // Prescription
            var prMst = await db.QueryFirstOrDefaultAsync<OpPrescriptionMasterModel>(
    @"SELECT pr_id, pr_code, sheet_id, op_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             pr_date, topremarks, bottonremarks,
             is_dispensed, tenant_code, isdeleted, created_at, updated_at
      FROM   op_prescription_master
      WHERE  sheet_id    = @sheet_id
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY created_at DESC
      LIMIT  1",
    new { sheet_id = sheet.sheet_id, tenant_code });

            if (prMst != null)
            {
                var prDet = (await db.QueryAsync<OpPrescriptionDetailModel>(
                    @"SELECT * FROM op_prescription_detail
                      WHERE  pr_id      = @pr_id
                      AND    isdeleted  = false
                      ORDER  BY sno",
                    new { pr_id = prMst.pr_id })).ToList();

                vm.prescription = new CaseSheetPrescriptionViewModel
                {
                    pr_id = prMst.pr_id.ToString(),
                    pr_code = prMst.pr_code,
                    pr_date = prMst.pr_date,
                    topremarks = prMst.topremarks,
                    bottonremarks = prMst.bottonremarks,
                    is_dispensed = prMst.is_dispensed,
                    items = prDet
                };
            }

            // Investigation
            var invMst = await db.QueryFirstOrDefaultAsync<OpInvestigationMasterModel>(
    @"SELECT inv_id, inv_code, sheet_id, op_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             inv_date, notes, is_urgent, status,
             tenant_code, isdeleted, created_at, updated_at
      FROM   op_investigation_master
      WHERE  sheet_id    = @sheet_id
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY created_at DESC
      LIMIT  1",
    new { sheet_id = sheet.sheet_id, tenant_code });

            if (invMst != null)
            {
                var tests = (await db.QueryAsync<OpInvestigationDetailModel>(
                    @"SELECT * FROM op_investigation_detail
                      WHERE  inv_id     = @inv_id
                      AND    isdeleted  = false
                      ORDER  BY sno",
                    new { inv_id = invMst.inv_id })).ToList();

                vm.investigation = new CaseSheetInvestigationViewModel
                {
                    inv_id = invMst.inv_id.ToString(),
                    inv_code = invMst.inv_code,
                    inv_date = invMst.inv_date,
                    notes = invMst.notes,
                    is_urgent = invMst.is_urgent,
                    status = invMst.status,
                    tests = tests
                };
            }

            return vm;
        }

        // ─────────────────────────────────────────────────────────
        // GET PRESCRIPTION BY OP_ID
        // ─────────────────────────────────────────────────────────
        public async Task<CaseSheetPrescriptionViewModel?> GetPrescription(
            string op_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var prMst = await db.QueryFirstOrDefaultAsync<OpPrescriptionMasterModel>(
    @"SELECT p.pr_id, p.pr_code, p.sheet_id, p.op_id, p.custid, p.dcode,
             p.visit_date::timestamp AS visit_date,
             p.pr_date, p.topremarks, p.bottonremarks,
             p.is_dispensed, p.tenant_code, p.isdeleted, p.created_at, p.updated_at
      FROM   op_prescription_master p
      JOIN   op_case_sheet s ON s.sheet_id = p.sheet_id
      WHERE  s.op_id       = CAST(@op_id AS uuid)
      AND    p.tenant_code = @tenant_code
      AND    p.isdeleted   = false
      ORDER  BY p.created_at DESC
      LIMIT  1",
    new { op_id, tenant_code });

            if (prMst == null) return null;

            var prDet = (await db.QueryAsync<OpPrescriptionDetailModel>(
                @"SELECT * FROM op_prescription_detail
                  WHERE  pr_id     = @pr_id
                  AND    isdeleted = false
                  ORDER  BY sno",
                new { pr_id = prMst.pr_id })).ToList();

            return new CaseSheetPrescriptionViewModel
            {
                pr_id = prMst.pr_id.ToString(),
                pr_code = prMst.pr_code,
                pr_date = prMst.pr_date,
                topremarks = prMst.topremarks,
                bottonremarks = prMst.bottonremarks,
                is_dispensed = prMst.is_dispensed,
                items = prDet
            };
        }

        // ─────────────────────────────────────────────────────────
        // GET INVESTIGATION BY OP_ID
        // ─────────────────────────────────────────────────────────
        public async Task<CaseSheetInvestigationViewModel?> GetInvestigation(
            string op_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var invMst = await db.QueryFirstOrDefaultAsync<OpInvestigationMasterModel>(
    @"SELECT i.inv_id, i.inv_code, i.sheet_id, i.op_id, i.custid, i.dcode,
             i.visit_date::timestamp AS visit_date,
             i.inv_date, i.notes, i.is_urgent, i.status,
             i.tenant_code, i.isdeleted, i.created_at, i.updated_at
      FROM   op_investigation_master i
      JOIN   op_case_sheet s ON s.sheet_id = i.sheet_id
      WHERE  s.op_id       = CAST(@op_id AS uuid)
      AND    i.tenant_code = @tenant_code
      AND    i.isdeleted   = false
      ORDER  BY i.created_at DESC
      LIMIT  1",
    new { op_id, tenant_code });

            if (invMst == null) return null;

            var tests = (await db.QueryAsync<OpInvestigationDetailModel>(
                @"SELECT * FROM op_investigation_detail
                  WHERE  inv_id    = @inv_id
                  AND    isdeleted = false
                  ORDER  BY sno",
                new { inv_id = invMst.inv_id })).ToList();

            return new CaseSheetInvestigationViewModel
            {
                inv_id = invMst.inv_id.ToString(),
                inv_code = invMst.inv_code,
                inv_date = invMst.inv_date,
                notes = invMst.notes,
                is_urgent = invMst.is_urgent,
                status = invMst.status,
                tests = tests
            };
        }

        // ─────────────────────────────────────────────────────────
        // UPDATE INVESTIGATION RESULT
        // ─────────────────────────────────────────────────────────
        public async Task<string> UpdateInvestigationResult(
            UpdateInvestigationResultRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_investigation_detail
                      SET    result_value  = @result_value,
                             result_notes  = @result_notes,
                             result_status = @result_status,
                             result_date   = NOW(),
                             updated_at    = NOW()
                      WHERE  inv_det_id   = CAST(@inv_det_id AS uuid)
                      AND    tenant_code  = @tenant_code",
                    new
                    {
                        req.inv_det_id,
                        req.result_value,
                        req.result_notes,
                        req.result_status,
                        tenant_code
                    });

                if (rows > 0)
                {
                    // Auto-update investigation master status
                    await db.ExecuteAsync(
                        @"UPDATE op_investigation_master
                          SET    status     = CASE
                                               WHEN (
                                                   SELECT COUNT(*) FROM op_investigation_detail
                                                   WHERE inv_id = im.inv_id
                                                   AND result_status = 'PENDING'
                                                   AND isdeleted = false
                                               ) = 0 THEN 'COMPLETED'
                                               ELSE 'PARTIAL'
                                             END,
                                 updated_at = NOW()
                          FROM   op_investigation_master im
                          JOIN   op_investigation_detail d ON d.inv_id = im.inv_id
                          WHERE  d.inv_det_id = CAST(@inv_det_id AS uuid)
                          AND    im.tenant_code = @tenant_code",
                        new { req.inv_det_id, tenant_code });
                }

                return rows > 0 ? "Success" : "Investigation detail not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // DELETE PRESCRIPTION  (soft delete)
        // ─────────────────────────────────────────────────────────
        public async Task<string> DeletePrescription(string pr_code, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_prescription_master
                      SET    isdeleted  = true,
                             updated_at = NOW()
                      WHERE  pr_code     = @pr_code
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { pr_code, tenant_code });

                return rows > 0 ? "Success" : "Prescription not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // DELETE INVESTIGATION  (soft delete)
        // ─────────────────────────────────────────────────────────
        public async Task<string> DeleteInvestigation(string inv_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_investigation_master
                      SET    isdeleted  = true,
                             updated_at = NOW()
                      WHERE  inv_id      = CAST(@inv_id AS uuid)
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { inv_id, tenant_code });

                return rows > 0 ? "Success" : "Investigation not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // GET PATIENT HISTORY (all visits case sheets)
        // ─────────────────────────────────────────────────────────
        public async Task<List<CaseSheetViewModel>> GetPatientHistory(
            decimal custid, string tenant_code, int pageSize = 10, int pageNo = 1)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var sheets = (await db.QueryAsync<OpCaseSheetModel>(
    @"SELECT sheet_id, op_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             chief_complaint, symptoms, examination,
             advise, notes,
             followup_date::timestamp AS followup_date,
             followup_notes, is_consulted, sheet_status,
             tenant_code, isdeleted, created_at, updated_at
      FROM   op_case_sheet
      WHERE  custid      = @custid
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY visit_date DESC
      LIMIT  @pageSize OFFSET @offset",
    new { custid, tenant_code, pageSize, offset = (pageNo - 1) * pageSize }))
    .ToList();

            var result = new List<CaseSheetViewModel>();

            foreach (var s in sheets)
            {
                var vm = new CaseSheetViewModel
                {
                    sheet_id = s.sheet_id.ToString(),
                    op_id = s.op_id.ToString(),
                    custid = s.custid,
                    dcode = s.dcode,
                    visit_date = s.visit_date,
                    chief_complaint = s.chief_complaint,
                    symptoms = s.symptoms,
                    examination = s.examination,
                    advise = s.advise,
                    notes = s.notes,
                    followup_date = s.followup_date,
                    followup_notes = s.followup_notes,
                    is_consulted = s.is_consulted,
                    sheet_status = s.sheet_status
                };

                // Symptoms
                vm.symptom_list = (await db.QueryAsync<OpCaseSheetSymptomModel>(
                    @"SELECT * FROM op_case_sheet_symptoms
          WHERE  sheet_id = @sheet_id
          ORDER  BY sno",
                    new { sheet_id = s.sheet_id })).ToList();

                // Diagnosis
                vm.diagnosis_list = (await db.QueryAsync<OpCaseSheetDiagnosisModel>(
                    @"SELECT diag_id, sheet_id, op_id, custid, dcode,
                 visit_date::timestamp AS visit_date,
                 sno, icd_code, icd_description, diagnosis_text,
                 diagnosis_type, condition_type, severity, status,
                 tenant_code, isdeleted, created_at
          FROM   op_case_sheet_diagnosis
          WHERE  sheet_id  = @sheet_id
          AND    isdeleted = false
          ORDER  BY sno",
                    new { sheet_id = s.sheet_id })).ToList();

                // Prescription
                var prMst = await db.QueryFirstOrDefaultAsync<OpPrescriptionMasterModel>(
                    @"SELECT pr_id, pr_code, sheet_id, op_id, custid, dcode,
                 visit_date::timestamp AS visit_date,
                 pr_date, topremarks, bottonremarks,
                 is_dispensed, tenant_code, isdeleted, created_at, updated_at
          FROM   op_prescription_master
          WHERE  sheet_id    = @sheet_id
          AND    tenant_code = @tenant_code
          AND    isdeleted   = false
          ORDER  BY created_at DESC
          LIMIT  1",
                    new { sheet_id = s.sheet_id, tenant_code });

                if (prMst != null)
                {
                    var prDet = (await db.QueryAsync<OpPrescriptionDetailModel>(
                        @"SELECT * FROM op_prescription_detail
              WHERE  pr_id     = @pr_id
              AND    isdeleted = false
              ORDER  BY sno",
                        new { pr_id = prMst.pr_id })).ToList();

                    vm.prescription = new CaseSheetPrescriptionViewModel
                    {
                        pr_id = prMst.pr_id.ToString(),
                        pr_code = prMst.pr_code,
                        pr_date = prMst.pr_date,
                        topremarks = prMst.topremarks,
                        bottonremarks = prMst.bottonremarks,
                        is_dispensed = prMst.is_dispensed,
                        items = prDet
                    };
                }

                // Investigation
                var invMst = await db.QueryFirstOrDefaultAsync<OpInvestigationMasterModel>(
                    @"SELECT inv_id, inv_code, sheet_id, op_id, custid, dcode,
                 visit_date::timestamp AS visit_date,
                 inv_date, notes, is_urgent, status,
                 tenant_code, isdeleted, created_at, updated_at
          FROM   op_investigation_master
          WHERE  sheet_id    = @sheet_id
          AND    tenant_code = @tenant_code
          AND    isdeleted   = false
          ORDER  BY created_at DESC
          LIMIT  1",
                    new { sheet_id = s.sheet_id, tenant_code });

                if (invMst != null)
                {
                    var tests = (await db.QueryAsync<OpInvestigationDetailModel>(
                        @"SELECT * FROM op_investigation_detail
              WHERE  inv_id    = @inv_id
              AND    isdeleted = false
              ORDER  BY sno",
                        new { inv_id = invMst.inv_id })).ToList();

                    vm.investigation = new CaseSheetInvestigationViewModel
                    {
                        inv_id = invMst.inv_id.ToString(),
                        inv_code = invMst.inv_code,
                        inv_date = invMst.inv_date,
                        notes = invMst.notes,
                        is_urgent = invMst.is_urgent,
                        status = invMst.status,
                        tests = tests
                    };
                }

                result.Add(vm);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // ICD-10 SEARCH
        // ─────────────────────────────────────────────────────────
        public async Task<List<dynamic>> SearchIcd(string query, int limit = 20)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var results = await db.QueryAsync(
                @"SELECT icd_code, icd_description, icd_category
                  FROM   icd_master
                  WHERE  is_active = true
                  AND    (icd_code ILIKE @q OR icd_description ILIKE @q)
                  ORDER  BY icd_code
                  LIMIT  @limit",
                new { q = $"%{query}%", limit });

            return results.Cast<dynamic>().ToList();
        }
        public async Task<List<IcdMasterModel>> GetAllIcd()
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var result = await db.QueryAsync<IcdMasterModel>(
                    @"SELECT 
                        icd_id,
                        icd_code,
                        icd_description,
                        icd_category,
                        icd_chapter,
                        is_active
                      FROM icd_master
                      WHERE is_active = true
                      ORDER BY icd_code");

                return result.ToList();
            }
            catch
            {
                return new List<IcdMasterModel>();
            }
        }

    }
}