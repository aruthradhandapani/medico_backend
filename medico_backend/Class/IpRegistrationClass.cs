using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;
using static medico_backend.Model.IPRegistrationModel;

namespace medico_backend.Class
{
    public class IpRegistrationClass
    {
        private readonly string _db_conn;

        public IpRegistrationClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // GENERATE IP NUMBER
        // Format: IPD/2026/07/0001 — resets each month
        // ─────────────────────────────────────────
        private async Task<string> GenerateIpNo(IDbConnection db, string tenant_code)
        {
            string sql = @"SELECT COALESCE(MAX(
                           CAST(SPLIT_PART(ip_no, '/', 4) AS INT)
                       ), 0) + 1
                       FROM   ip_registration
                       WHERE  tenant_code = @tenant_code
                       AND    isdeleted   = false
                       AND    SPLIT_PART(ip_no, '/', 2) = @year
                       AND    SPLIT_PART(ip_no, '/', 3) = @month";

            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month });
            return $"IPD/{year}/{month}/{next:D4}";
        }

        // ─────────────────────────────────────────
        // CREATE IP REGISTRATION (ADMIT PATIENT)
        // ─────────────────────────────────────────
        public async Task<string> CreateIpRegistration(CreateIpRegistrationRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // ── Validate insurance fields if patient is insured ─────
                if (req.isinsurancepatient && string.IsNullOrWhiteSpace(req.policyno))
                {
                    tx.Rollback();
                    return "policyno is required for insurance patients";
                }

                // ── Validate bed is free ────────────────────────────────
                var bedRow = await db.QueryFirstOrDefaultAsync(
                    @"SELECT bedcode FROM public.bed_master
                      WHERE bedcode = @bedcode AND tenant_code = @tenant_code
                      AND (deleted IS NULL OR deleted = false)
                      FOR UPDATE",
                    new { req.bedcode, tenant_code }, tx);

                if (bedRow == null)
                {
                    tx.Rollback();
                    return "Bed not found";
                }

                var activeOccupant = await db.QueryFirstOrDefaultAsync(
                    @"SELECT ip_id FROM ip_registration
                      WHERE bedcode = @bedcode AND tenant_code = @tenant_code
                      AND ip_status = 'ADMITTED' AND isdeleted = false",
                    new { req.bedcode, tenant_code }, tx);

                if (activeOccupant != null)
                {
                    tx.Rollback();
                    return "Selected bed is currently occupied";
                }

                var data = new IpRegistrationModel
                {
                    ip_id = Guid.NewGuid(),
                    ip_no = await GenerateIpNo(db, tenant_code),
                    custid = req.custid,
                    booking_id = req.booking_id,
                    op_id = req.op_id,
                    dcode = req.dcode,
                    referring_dcode = req.referring_dcode,
                    department_code = req.department_code,
                    admission_type = string.IsNullOrWhiteSpace(req.admission_type) ? "PLANNED" : req.admission_type.ToUpper(),
                    admission_reason = req.admission_reason,
                    admitdate = req.admitdate ?? DateTime.UtcNow,
                    expected_dischargedate = req.expected_dischargedate,
                    branchcode = req.branchcode,
                    blockcode = req.blockcode,
                    flrcode = req.flrcode,
                    wrdcode = req.wrdcode,
                    rmtcode = req.rmtcode,
                    bedcode = req.bedcode,
                    ip_status = "ADMITTED",
                    isinsurancepatient = req.isinsurancepatient,
                    insurance_company = req.insurance_company,
                    policyno = req.policyno,
                    authorizationno = req.authorizationno,
                    tpa_name = req.tpa_name,
                    insurance_approved_amount = req.insurance_approved_amount,
                    insurance_status = req.isinsurancepatient ? "PENDING" : null,
                    guardian_name = req.guardian_name,
                    guardian_relation = req.guardian_relation,
                    guardian_contact = req.guardian_contact,
                    notes = req.notes,
                    tenant_code = tenant_code,
                    isdeleted = false,
                    created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ip_registration
                    (ip_id, ip_no, custid, booking_id, op_id, dcode, referring_dcode, department_code,
                     admission_type, admission_reason, admitdate, expected_dischargedate,
                     branchcode, blockcode, flrcode, wrdcode, rmtcode, bedcode, ip_status,
                     isinsurancepatient, insurance_company, policyno, authorizationno, tpa_name,
                     insurance_approved_amount, insurance_status,
                     guardian_name, guardian_relation, guardian_contact,
                     notes, tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@ip_id, @ip_no, @custid, @booking_id, @op_id, @dcode, @referring_dcode, @department_code,
                     @admission_type, @admission_reason, @admitdate, @expected_dischargedate,
                     @branchcode, @blockcode, @flrcode, @wrdcode, @rmtcode, @bedcode, @ip_status,
                     @isinsurancepatient, @insurance_company, @policyno, @authorizationno, @tpa_name,
                     @insurance_approved_amount, @insurance_status,
                     @guardian_name, @guardian_relation, @guardian_contact,
                     @notes, @tenant_code, @isdeleted, @created_at, @updated_at)",
                    data, tx);


                tx.Commit();
                return $"Success|IpNo:{data.ip_no}|IpId:{data.ip_id}";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DISCHARGE PATIENT
        // ─────────────────────────────────────────
        public async Task<string> Discharge(DischargeRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var existing = await db.QueryFirstOrDefaultAsync<IpRegistrationModel>(
                "SELECT * FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.ip_id, tenant_code });

            if (existing == null) return "IP Registration not found";
            if (existing.ip_status == "DISCHARGED") return "Patient already discharged";

            string sql = @"UPDATE ip_registration
                           SET ip_status         = 'DISCHARGED',
                               dischargedate     = now(),
                               discharge_type    = @discharge_type,
                               discharge_summary = @discharge_summary,
                               updated_at        = now()
                           WHERE ip_id = @ip_id AND tenant_code = @tenant_code";

            int rows = await db.ExecuteAsync(sql, new
            {
                req.ip_id,
                discharge_type = req.discharge_type.ToUpper(),
                req.discharge_summary,
                tenant_code
            });

            if (rows == 0) return "Update failed";

            // Mark bed_transfer as checked out
            await db.ExecuteAsync(@"
                UPDATE public.bed_transfer
                SET ischeckout = true, transferdate = now()
                WHERE lastvisitid = @lastvisitid AND tenant_code = @tenant_code",
                new { lastvisitid = req.ip_id.ToString(), tenant_code });

            return "Success";
        }

        // ─────────────────────────────────────────
        // TRANSFER BED (during admission)
        // ─────────────────────────────────────────
        public async Task<string> TransferBed(IpBedTransferRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existing = await db.QueryFirstOrDefaultAsync<IpRegistrationModel>(
                    "SELECT * FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false FOR UPDATE",
                    new { req.ip_id, tenant_code }, tx);

                if (existing == null) { tx.Rollback(); return "IP Registration not found"; }
                if (existing.ip_status != "ADMITTED") { tx.Rollback(); return "Patient is not currently admitted"; }

                var occupied = await db.QueryFirstOrDefaultAsync(
                    @"SELECT ip_id FROM ip_registration
                      WHERE bedcode = @transbed AND tenant_code = @tenant_code
                      AND ip_status = 'ADMITTED' AND isdeleted = false AND ip_id <> @ip_id",
                    new { req.transbed, tenant_code, req.ip_id }, tx);

                if (occupied != null) { tx.Rollback(); return "Target bed is already occupied"; }

                await db.ExecuteAsync(@"
                    UPDATE ip_registration
                    SET flrcode = @transfloor, wrdcode = @transward, bedcode = @transbed,
                        updated_at = now()
                    WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                    new { req.transfloor, req.transward, req.transbed, req.ip_id, tenant_code }, tx);

                await db.ExecuteAsync(@"
                    INSERT INTO public.bed_transfer
                    (lastvisitid, custid, admitteddate, currentfloor, currentroom, currentbed,
                     transferdate, transfloor, transroom, transbed, transferedby, reason,
                     ischeckout, tenant_code, entereddate)
                    VALUES
                    (@lastvisitid, @custid, @admitteddate, @currentfloor, @currentroom, @currentbed,
                     now(), @transfloor, @transroom, @transbed, @transferedby, @reason,
                     false, @tenant_code, now())",
                    new
                    {
                        lastvisitid = req.ip_id.ToString(),
                        existing.custid,
                        admitteddate = existing.admitdate,
                        currentfloor = existing.flrcode,
                        currentroom = existing.rmtcode,
                        currentbed = existing.bedcode,
                        req.transfloor,
                        req.transroom,
                        req.transbed,
                        req.transferedby,
                        req.reason,
                        tenant_code
                    }, tx);

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (with filters)
        // ─────────────────────────────────────────
        public async Task<List<IpRegistrationModel>> GetAll(
            string tenant_code, string? ip_status = null, int? dcode = null)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM ip_registration
                           WHERE isdeleted = false AND tenant_code = @tenant_code
                           AND (@ip_status IS NULL OR ip_status = @ip_status)
                           AND (@dcode IS NULL OR dcode = @dcode)
                           ORDER BY admitdate DESC";

            var res = await db.QueryAsync<IpRegistrationModel>(sql, new { tenant_code, ip_status = ip_status?.ToUpper(), dcode });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY IP_ID
        // ─────────────────────────────────────────
        public async Task<IpRegistrationModel?> GetById(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM ip_registration
                           WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false";

            return await db.QueryFirstOrDefaultAsync<IpRegistrationModel>(sql, new { ip_id, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET CURRENTLY ADMITTED PATIENTS (with customer + bed details)
        // ─────────────────────────────────────────
        public async Task<List<dynamic>> GetActiveAdmissions(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
                SELECT
                    ip.ip_id, ip.ip_no, ip.custid, ip.dcode, ip.admitdate,
                    ip.ip_status, ip.isinsurancepatient, ip.insurance_status,
                    ip.bedcode, bm.bedname,
                    rm.name AS roomtype_name,
                    wm.name AS ward_name,
                    fm.name AS floor_name,
                    c.name AS patient_name, c.mobile, c.gender
                FROM ip_registration ip
                LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = ip.tenant_code
                LEFT JOIN public.roomtype_master rm ON rm.rmtcode = ip.rmtcode AND rm.tenant_code = ip.tenant_code
                LEFT JOIN public.ward_master wm ON wm.wrdcode = ip.wrdcode AND wm.tenant_code = ip.tenant_code
                LEFT JOIN public.floor_master fm ON fm.flrcode = ip.flrcode AND fm.tenant_code = ip.tenant_code
                LEFT JOIN customerdb.customer_master c ON c.custid::numeric = ip.custid AND TRIM(c.tenant_code) = TRIM(ip.tenant_code)
                WHERE ip.isdeleted = false AND ip.ip_status = 'ADMITTED' AND ip.tenant_code = @tenant_code
                ORDER BY ip.admitdate DESC";

            var res = await db.QueryAsync<dynamic>(sql, new { tenant_code });
            return res.ToList();
        }
        // ─────────────────────────────────────────
        // UPDATE (full replace of entered fields — same shape as Create)
        // ─────────────────────────────────────────
        public async Task<string> Update(UpdateIpRegistrationRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var existing = await db.QueryFirstOrDefaultAsync<IpRegistrationModel>(
                "SELECT * FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.ip_id, tenant_code });

            if (existing == null) return "IP Registration not found";
            if (existing.ip_status == "DISCHARGED") return "Cannot update a discharged record";
            if (existing.ip_status == "CANCELLED") return "Cannot update a cancelled record";

            if (req.isinsurancepatient && string.IsNullOrWhiteSpace(req.policyno))
                return "policyno is required for insurance patients";

            string sql = @"UPDATE ip_registration SET
        custid                    = @custid,
        booking_id                = @booking_id,
        op_id                      = @op_id,
        dcode                     = @dcode,
        referring_dcode            = @referring_dcode,
        department_code            = @department_code,
        admission_type             = @admission_type,
        admission_reason           = @admission_reason,
        admitdate                  = @admitdate,
        expected_dischargedate     = @expected_dischargedate,
        branchcode                 = @branchcode,
        blockcode                  = @blockcode,
        flrcode                    = @flrcode,
        wrdcode                    = @wrdcode,
        rmtcode                    = @rmtcode,
        isinsurancepatient         = @isinsurancepatient,
        insurance_company          = @insurance_company,
        policyno                   = @policyno,
        authorizationno            = @authorizationno,
        tpa_name                   = @tpa_name,
        insurance_approved_amount  = @insurance_approved_amount,
        insurance_status           = @insurance_status,
        guardian_name              = @guardian_name,
        guardian_relation          = @guardian_relation,
        guardian_contact           = @guardian_contact,
        notes                      = @notes,
        updated_at                 = now()
        WHERE ip_id = @ip_id AND tenant_code = @tenant_code";

            int rows = await db.ExecuteAsync(sql, new
            {
                req.custid,
                req.booking_id,
                req.op_id,
                req.dcode,
                req.referring_dcode,
                req.department_code,
                admission_type = string.IsNullOrWhiteSpace(req.admission_type) ? existing.admission_type : req.admission_type.ToUpper(),
                req.admission_reason,
                admitdate = req.admitdate ?? existing.admitdate,
                req.expected_dischargedate,
                req.branchcode,
                req.blockcode,
                req.flrcode,
                req.wrdcode,
                req.rmtcode,
                req.isinsurancepatient,
                req.insurance_company,
                req.policyno,
                req.authorizationno,
                req.tpa_name,
                req.insurance_approved_amount,
                insurance_status = req.insurance_status ?? (req.isinsurancepatient ? existing.insurance_status : null),
                req.guardian_name,
                req.guardian_relation,
                req.guardian_contact,
                req.notes,
                req.ip_id,
                tenant_code
            });

            return rows > 0 ? "Success" : "Update failed";
        }

        // ─────────────────────────────────────────
        // CANCEL ADMISSION (soft delete — only if never actually admitted/occupied)
        // Frees the bed if one was reserved. Cannot cancel once discharged.
        // ─────────────────────────────────────────
        public async Task<string> CancelAdmission(CancelAdmissionRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existing = await db.QueryFirstOrDefaultAsync<IpRegistrationModel>(
                    "SELECT * FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false FOR UPDATE",
                    new { req.ip_id, tenant_code }, tx);

                if (existing == null) { tx.Rollback(); return "IP Registration not found"; }
                if (existing.ip_status == "DISCHARGED") { tx.Rollback(); return "Cannot cancel a discharged admission"; }

                await db.ExecuteAsync(@"
            UPDATE ip_registration
            SET ip_status = 'CANCELLED', isdeleted = true, notes = COALESCE(notes || ' | ', '') || @reason, updated_at = now()
            WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                    new { ip_id = req.ip_id, reason = "Cancelled: " + (req.reason ?? "No reason given"), tenant_code }, tx);

                // Free up the bed_transfer record too
                await db.ExecuteAsync(@"
            UPDATE public.bed_transfer
            SET ischeckout = true, transferdate = now()
            WHERE lastvisitid = @lastvisitid AND tenant_code = @tenant_code",
                    new { lastvisitid = req.ip_id.ToString(), tenant_code }, tx);

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }
    }
}