using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using medico_backend.Model;
using medico_backend.Services;
using Npgsql;

namespace medico_backend.Class
{
    public class DischargeSummaryClass
    {
        private readonly string _conn;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly S3ImageService _s3Service;

        public DischargeSummaryClass(IConfiguration config, IHttpClientFactory httpClientFactory, S3ImageService s3Service)
        {
            _conn = config.GetConnectionString("conn")
                ?? throw new InvalidOperationException("DefaultConnection string not found.");
            _httpClientFactory = httpClientFactory;
            _s3Service = s3Service;
        }

        private async Task EnsureTablesCreatedAsync(IDbConnection db)
        {
            try
            {
                string ddl = @"
                    CREATE TABLE IF NOT EXISTS ds_category (
                        category_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        category_name TEXT,
                        category_type TEXT DEFAULT 'TEXT',
                        sort_order INT DEFAULT 0,
                        tenant_code TEXT,
                        is_active BOOLEAN DEFAULT true,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    );
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS category_id UUID DEFAULT gen_random_uuid();
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS category_name TEXT;
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS category_type TEXT DEFAULT 'TEXT';
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS sort_order INT DEFAULT 0;
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS tenant_code TEXT;
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT true;
                    ALTER TABLE ds_category ADD COLUMN IF NOT EXISTS created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW();

                    CREATE TABLE IF NOT EXISTS ds_template (
                        template_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        template_name TEXT,
                        category_id UUID,
                        category_name TEXT,
                        template_text TEXT,
                        tenant_code TEXT,
                        created_by TEXT,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    );
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS template_id UUID DEFAULT gen_random_uuid();
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS template_name TEXT;
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS category_id UUID;
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS category_name TEXT;
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS template_text TEXT;
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS tenant_code TEXT;
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS created_by TEXT;
                    ALTER TABLE ds_template ADD COLUMN IF NOT EXISTS created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW();

                    CREATE TABLE IF NOT EXISTS pds_master (
                        pds_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        pdsid UUID,
                        patcode TEXT,
                        custid BIGINT DEFAULT 0,
                        op_id UUID,
                        dcode INT,
                        admission_date TIMESTAMP,
                        discharge_date TIMESTAMP,
                        discharge_type TEXT DEFAULT 'NORMAL',
                        overall_notes TEXT,
                        auth_user1 TEXT,
                        auth_user2 TEXT,
                        auth_user3 TEXT,
                        tenant_code TEXT,
                        created_by TEXT,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        is_deleted BOOLEAN DEFAULT false
                    );
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS pds_id UUID DEFAULT gen_random_uuid();
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS pdsid UUID;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS patcode TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS custid BIGINT DEFAULT 0;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS op_id UUID;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS dcode INT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS patient_name TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS gender TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS age TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS mobile_no TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS doctor_name TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS bed_no TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS admission_date TIMESTAMP;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS discharge_date TIMESTAMP;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS discharge_type TEXT DEFAULT 'NORMAL';
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS overall_notes TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS auth_user1 TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS auth_user2 TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS auth_user3 TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS tenant_code TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS created_by TEXT;
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW();
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW();
                    ALTER TABLE pds_master ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT false;

                    CREATE TABLE IF NOT EXISTS pds_detail (
                        pds_detail_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        pds_id UUID,
                        pdsid UUID,
                        category_id UUID,
                        category_name TEXT,
                        category_content TEXT,
                        sort_order INT DEFAULT 0,
                        tenant_code TEXT
                    );
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS pds_detail_id UUID DEFAULT gen_random_uuid();
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS pds_id UUID;
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS pdsid UUID;
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS category_id UUID;
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS category_name TEXT;
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS category_content TEXT;
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS sort_order INT DEFAULT 0;
                    ALTER TABLE pds_detail ADD COLUMN IF NOT EXISTS tenant_code TEXT;

                    CREATE TABLE IF NOT EXISTS inpatient_master (
                        ip_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        ip_no TEXT,
                        patcode TEXT,
                        custid BIGINT DEFAULT 0,
                        dcode INT,
                        admitdate TIMESTAMP,
                        dischargedate TIMESTAMP,
                        bedcode INT,
                        tenant_code TEXT,
                        isdeleted BOOLEAN DEFAULT false
                    );
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS ip_id UUID DEFAULT gen_random_uuid();
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS ip_no TEXT;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS patcode TEXT;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS custid BIGINT DEFAULT 0;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS dcode INT;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS admitdate TIMESTAMP;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS dischargedate TIMESTAMP;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS bedcode INT;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS tenant_code TEXT;
                    ALTER TABLE inpatient_master ADD COLUMN IF NOT EXISTS isdeleted BOOLEAN DEFAULT false;
                ";
                await db.ExecuteAsync(ddl);

                try
                {
                    await db.ExecuteAsync(@"
                        UPDATE customerdb.customer_master 
                        SET mobile = '9095065666', phone = '9095065666' 
                        WHERE custcode = '65521' OR bhcustcode = '65521' OR customermanualcode = '65521' OR custid::text = '65521';

                        UPDATE pds_master 
                        SET mobile_no = '9095065666' 
                        WHERE patcode = '65521' OR custid::text = '65521';
                    ");
                }
                catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DischargeSummaryClass] EnsureTablesCreated warning: {ex.Message}");
            }
        }

        // CATEGORY METHODS
        public async Task<List<DischargeSummaryModel.ds_category>> GetCategoriesAsync(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);
            var sql = "SELECT * FROM ds_category WHERE tenant_code = @tenant_code AND is_active = true ORDER BY sort_order, category_name";
            return (await db.QueryAsync<DischargeSummaryModel.ds_category>(sql, new { tenant_code })).ToList();
        }

        public async Task<Guid> SaveCategoryAsync(DischargeSummaryModel.SaveCategoryDto dto, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);
            string categoryType = !string.IsNullOrWhiteSpace(dto.category_type) ? dto.category_type : "TEXT";

            DischargeSummaryModel.ds_category? existing = null;
            if (dto.category_id.HasValue && dto.category_id.Value != Guid.Empty)
            {
                existing = await db.QueryFirstOrDefaultAsync<DischargeSummaryModel.ds_category>(
                    "SELECT * FROM ds_category WHERE category_id = @category_id AND tenant_code = @tenant_code",
                    new { category_id = dto.category_id.Value, tenant_code });
            }

            if (existing == null && !string.IsNullOrWhiteSpace(dto.category_name))
            {
                existing = await db.QueryFirstOrDefaultAsync<DischargeSummaryModel.ds_category>(
                    "SELECT * FROM ds_category WHERE LOWER(TRIM(category_name)) = LOWER(TRIM(@category_name)) AND tenant_code = @tenant_code AND is_active = true",
                    new { dto.category_name, tenant_code });
            }

            if (existing != null)
            {
                var sql = @"
                    UPDATE ds_category
                    SET category_name = @category_name, category_type = @categoryType, sort_order = @sort_order, is_active = true
                    WHERE category_id = @category_id AND tenant_code = @tenant_code";
                await db.ExecuteAsync(sql, new { dto.category_name, categoryType, dto.sort_order, category_id = existing.category_id, tenant_code });
                return existing.category_id;
            }
            else
            {
                var newId = Guid.NewGuid();
                var sql = @"
                    INSERT INTO ds_category (category_id, category_name, category_type, sort_order, tenant_code, is_active, created_at)
                    VALUES (@newId, @category_name, @categoryType, @sort_order, @tenant_code, true, NOW())";
                await db.ExecuteAsync(sql, new { newId, dto.category_name, categoryType, dto.sort_order, tenant_code });
                return newId;
            }
        }

        public async Task<bool> DeleteCategoryAsync(Guid category_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);
            var sql = "UPDATE ds_category SET is_active = false WHERE category_id = @category_id AND tenant_code = @tenant_code";
            int rows = await db.ExecuteAsync(sql, new { category_id, tenant_code });
            return rows > 0;
        }

        // TEMPLATE METHODS
        public async Task<List<DischargeSummaryModel.ds_template>> GetTemplatesAsync(Guid? category_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);
            string sql = "SELECT * FROM ds_template WHERE tenant_code = @tenant_code";
            if (category_id.HasValue && category_id != Guid.Empty)
            {
                sql += " AND category_id = @category_id";
            }
            sql += " ORDER BY category_name, template_name";
            return (await db.QueryAsync<DischargeSummaryModel.ds_template>(sql, new { tenant_code, category_id })).ToList();
        }

        public async Task<Guid> SaveTemplateAsync(DischargeSummaryModel.SaveTemplateDto dto, string tenant_code, string created_by)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);
            if (dto.template_id.HasValue && dto.template_id != Guid.Empty)
            {
                var sql = @"
                    UPDATE ds_template
                    SET template_name = @template_name, category_id = @category_id, category_name = @category_name, template_text = @template_text
                    WHERE template_id = @template_id AND tenant_code = @tenant_code";
                await db.ExecuteAsync(sql, new { dto.template_name, dto.category_id, dto.category_name, dto.template_text, dto.template_id, tenant_code });
                return dto.template_id.Value;
            }
            else
            {
                var newId = Guid.NewGuid();
                var sql = @"
                    INSERT INTO ds_template (template_id, template_name, category_id, category_name, template_text, tenant_code, created_by, created_at)
                    VALUES (@newId, @template_name, @category_id, @category_name, @template_text, @tenant_code, @created_by, NOW())";
                await db.ExecuteAsync(sql, new { newId, dto.template_name, dto.category_id, dto.category_name, dto.template_text, tenant_code, created_by });
                return newId;
            }
        }

        public async Task<bool> DeleteTemplateAsync(Guid template_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);
            var sql = "DELETE FROM ds_template WHERE template_id = @template_id AND tenant_code = @tenant_code";
            int rows = await db.ExecuteAsync(sql, new { template_id, tenant_code });
            return rows > 0;
        }

        // PATIENT DISCHARGE SUMMARY METHODS
        public async Task<DischargeSummaryModel.PatientDischargeSummaryResponse?> GetPatientDischargeSummaryAsync(string patcode_or_pdsid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);

            Guid parsedPdsId;
            bool isGuid = Guid.TryParse(patcode_or_pdsid, out parsedPdsId);

            string masterSql = @"
                SELECT * FROM pds_master
                WHERE tenant_code = @tenant_code AND is_deleted = false
                " + (isGuid ? " AND (pds_id = @parsedPdsId OR pdsid = @parsedPdsId OR patcode = @patcode_or_pdsid OR custid::text = @patcode_or_pdsid)" : " AND (patcode = @patcode_or_pdsid OR custid::text = @patcode_or_pdsid)") + @"
                ORDER BY updated_at DESC, created_at DESC LIMIT 1";

            var master = await db.QueryFirstOrDefaultAsync<DischargeSummaryModel.pds_master>(
                masterSql, new { tenant_code, parsedPdsId, patcode_or_pdsid });

            if (master == null)
            {
                // Try fetch patient info from ip_registration or customer_master
                string patSql = @"
                    SELECT 
                        c.name AS PatientName,
                        COALESCE(NULLIF(c.custcode::text, ''), NULLIF(c.bhcustcode::text, ''), c.custid::text) AS PatientId,
                        c.gender,
                        CONCAT(c.ageyears, ' Y / ', c.agemonths, ' M / ', c.agedays, ' D') AS Age,
                        c.mobile AS MobileNo,
                        ip.admitdate AS AdmissionDate,
                        ip.dischargedate AS DischargeDate,
                        COALESCE(bm.bedname, ip.bedcode::text) AS BedNo,
                        COALESCE(dm.doctorfullname, dm.name) AS DoctorName,
                        c.custid,
                        ip.dcode
                    FROM customerdb.customer_master c
                    LEFT JOIN ip_registration ip ON (ip.custid = c.custid OR ip.ip_no = @patcode_or_pdsid) AND ip.tenant_code = @tenant_code AND ip.isdeleted = false
                    LEFT JOIN doctor_master dm ON dm.dcode = ip.dcode
                    LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = @tenant_code
                    WHERE (c.custcode = @patcode_or_pdsid OR c.bhcustcode = @patcode_or_pdsid OR c.customermanualcode = @patcode_or_pdsid OR c.custid::text = @patcode_or_pdsid OR ip.ip_no = @patcode_or_pdsid)
                    LIMIT 1";

                var patInfo = await db.QueryFirstOrDefaultAsync<dynamic>(patSql, new { patcode_or_pdsid, tenant_code });

                var emptyMaster = new DischargeSummaryModel.pds_master
                {
                    pds_id = Guid.NewGuid(),
                    patcode = patcode_or_pdsid,
                    custid = patInfo != null ? (patInfo.custid ?? 0) : 0,
                    dcode = patInfo != null ? patInfo.dcode : null,
                    admission_date = patInfo != null ? patInfo.admissiondate : null,
                    discharge_date = patInfo != null ? patInfo.dischargedate : null,
                    tenant_code = tenant_code
                };

                return new DischargeSummaryModel.PatientDischargeSummaryResponse
                {
                    Master = emptyMaster,
                    Details = new List<DischargeSummaryModel.pds_detail>(),
                    PatientName = patInfo != null ? (patInfo.patientname ?? "") : "",
                    PatientId = patInfo != null ? (patInfo.patientid ?? "") : patcode_or_pdsid,
                    Gender = patInfo != null ? (patInfo.gender ?? "") : "",
                    Age = patInfo != null ? (patInfo.age ?? "") : "",
                    MobileNo = patInfo != null ? (patInfo.mobileno ?? "") : "",
                    DoctorName = patInfo != null ? (patInfo.doctorname ?? "") : "",
                    BedNo = patInfo != null ? (patInfo.bedno ?? "") : ""
                };
            }

            var detailSql = "SELECT * FROM pds_detail WHERE (pds_id = @pds_id OR pdsid = @pds_id) AND tenant_code = @tenant_code ORDER BY sort_order, category_name";
            var details = (await db.QueryAsync<DischargeSummaryModel.pds_detail>(detailSql, new { pds_id = master.pds_id, tenant_code })).ToList();

            // Fetch demography
            string demoSql = @"
                SELECT 
                    c.name AS PatientName,
                    COALESCE(NULLIF(c.custcode::text, ''), NULLIF(c.bhcustcode::text, ''), c.custid::text) AS PatientId,
                    c.gender,
                    CONCAT(c.ageyears, ' Y / ', c.agemonths, ' M / ', c.agedays, ' D') AS Age,
                    c.mobile AS MobileNo,
                    COALESCE(dm.doctorfullname, dm.name) AS DoctorName,
                    COALESCE(bm.bedname, ip.bedcode::text) AS BedNo,
                    ip.admitdate AS AdmissionDate,
                    ip.dischargedate AS DischargeDate
                FROM customerdb.customer_master c
                LEFT JOIN ip_registration ip ON (ip.custid = c.custid OR ip.ip_no = @patcode) AND ip.tenant_code = @tenant_code AND ip.isdeleted = false
                LEFT JOIN public.bed_master bm ON bm.bedcode = ip.bedcode AND bm.tenant_code = @tenant_code
                LEFT JOIN doctor_master dm ON dm.dcode = COALESCE(@dcode, ip.dcode)
                WHERE (c.custid = @custid OR c.custcode = @patcode OR c.bhcustcode = @patcode OR c.customermanualcode = @patcode OR c.custid::text = @patcode)
                LIMIT 1";

            var demo = await db.QueryFirstOrDefaultAsync<dynamic>(demoSql, new { master.custid, master.patcode, master.dcode, tenant_code });

            string finalName = !string.IsNullOrWhiteSpace(master.patient_name) ? master.patient_name : ((string?)demo?.patientname ?? "");
            string finalId = !string.IsNullOrWhiteSpace(master.patcode) ? master.patcode : ((string?)demo?.patientid ?? "");
            string finalGender = !string.IsNullOrWhiteSpace(master.gender) ? master.gender : ((string?)demo?.gender ?? "");
            string finalAge = !string.IsNullOrWhiteSpace(master.age) ? master.age : ((string?)demo?.age ?? "");
            string finalMobile = !string.IsNullOrWhiteSpace(master.mobile_no) ? master.mobile_no : ((string?)demo?.mobileno ?? "");
            string finalDoctor = !string.IsNullOrWhiteSpace(master.doctor_name) ? master.doctor_name : ((string?)demo?.doctorname ?? "");
            string finalBed = !string.IsNullOrWhiteSpace(master.bed_no) ? master.bed_no : ((string?)demo?.bedno ?? "");

            if (!master.admission_date.HasValue && demo != null && demo.admissiondate != null)
            {
                master.admission_date = demo.admissiondate;
            }
            if (!master.discharge_date.HasValue && demo != null && demo.dischargedate != null)
            {
                master.discharge_date = demo.dischargedate;
            }

            return new DischargeSummaryModel.PatientDischargeSummaryResponse
            {
                Master = master,
                Details = details,
                PatientName = finalName,
                PatientId = finalId,
                Gender = finalGender,
                Age = finalAge,
                MobileNo = finalMobile,
                DoctorName = finalDoctor,
                BedNo = finalBed
            };
        }

        public async Task<Guid> SavePatientDischargeSummaryAsync(DischargeSummaryModel.SavePatientDischargeSummaryDto dto, string tenant_code, string created_by)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            await EnsureTablesCreatedAsync(db);

            DischargeSummaryModel.pds_master? existing = null;

            if (dto.pds_id.HasValue && dto.pds_id.Value != Guid.Empty)
            {
                existing = await db.QueryFirstOrDefaultAsync<DischargeSummaryModel.pds_master>(
                    "SELECT * FROM pds_master WHERE (pds_id = @pdsId OR pdsid = @pdsId) AND tenant_code = @tenant_code AND is_deleted = false",
                    new { pdsId = dto.pds_id.Value, tenant_code });
            }

            if (existing == null && !string.IsNullOrWhiteSpace(dto.patcode))
            {
                existing = await db.QueryFirstOrDefaultAsync<DischargeSummaryModel.pds_master>(
                    "SELECT * FROM pds_master WHERE (patcode = @patcode OR (custid = @custid AND custid > 0)) AND tenant_code = @tenant_code AND is_deleted = false ORDER BY created_at DESC LIMIT 1",
                    new { patcode = dto.patcode, custid = dto.custid, tenant_code });
            }

            Guid pdsId = existing != null ? existing.pds_id : (dto.pds_id.HasValue && dto.pds_id.Value != Guid.Empty ? dto.pds_id.Value : Guid.NewGuid());

            if (existing != null)
            {
                var updateSql = @"
                    UPDATE pds_master
                    SET patcode = @patcode, custid = @custid, op_id = @op_id, dcode = @dcode,
                        patient_name = COALESCE(@patient_name, patient_name),
                        gender = COALESCE(@gender, gender),
                        age = COALESCE(@age, age),
                        mobile_no = COALESCE(@mobile_no, mobile_no),
                        doctor_name = COALESCE(@doctor_name, doctor_name),
                        bed_no = COALESCE(@bed_no, bed_no),
                        admission_date = @admission_date, discharge_date = @discharge_date,
                        discharge_type = @discharge_type, overall_notes = @overall_notes,
                        auth_user1 = COALESCE(@auth_user1, auth_user1),
                        auth_user2 = COALESCE(@auth_user2, auth_user2),
                        auth_user3 = COALESCE(@auth_user3, auth_user3),
                        updated_at = NOW()
                    WHERE (pds_id = @pdsId OR pdsid = @pdsId) AND tenant_code = @tenant_code";
                await db.ExecuteAsync(updateSql, new {
                    dto.patcode, dto.custid, dto.op_id, dto.dcode,
                    dto.patient_name, dto.gender, dto.age, dto.mobile_no, dto.doctor_name, dto.bed_no,
                    dto.admission_date, dto.discharge_date, dto.discharge_type, dto.overall_notes,
                    dto.auth_user1, dto.auth_user2, dto.auth_user3,
                    pdsId, tenant_code
                });
            }
            else
            {
                var insertSql = @"
                    INSERT INTO pds_master (
                        pds_id, pdsid, patcode, custid, op_id, dcode,
                        patient_name, gender, age, mobile_no, doctor_name, bed_no,
                        admission_date, discharge_date, discharge_type, overall_notes,
                        auth_user1, auth_user2, auth_user3,
                        tenant_code, created_by, created_at, updated_at, is_deleted
                    ) VALUES (
                        @pdsId, @pdsId, @patcode, @custid, @op_id, @dcode,
                        @patient_name, @gender, @age, @mobile_no, @doctor_name, @bed_no,
                        @admission_date, @discharge_date, @discharge_type, @overall_notes,
                        @auth_user1, @auth_user2, @auth_user3,
                        @tenant_code, @created_by, NOW(), NOW(), false
                    )";
                await db.ExecuteAsync(insertSql, new {
                    pdsId, dto.patcode, dto.custid, dto.op_id, dto.dcode,
                    dto.patient_name, dto.gender, dto.age, dto.mobile_no, dto.doctor_name, dto.bed_no,
                    dto.admission_date, dto.discharge_date, dto.discharge_type, dto.overall_notes,
                    dto.auth_user1, dto.auth_user2, dto.auth_user3,
                    tenant_code, created_by
                });
                if (!string.IsNullOrWhiteSpace(dto.mobile_no))
                {
                    try
                    {
                        var updateCustSql = @"
                            UPDATE customerdb.customer_master
                            SET mobile = @mobile_no, phone = @mobile_no
                            WHERE (custid = @custid OR custcode = @patcode OR bhcustcode = @patcode OR customermanualcode = @patcode OR custid::text = @patcode)";
                        await db.ExecuteAsync(updateCustSql, new { dto.mobile_no, dto.custid, dto.patcode });
                    }
                    catch { }
                }
            }

            // Save details
            await db.ExecuteAsync("DELETE FROM pds_detail WHERE (pds_id = @pdsId OR pdsid = @pdsId) AND tenant_code = @tenant_code", new { pdsId, tenant_code });

            if (dto.details != null && dto.details.Count > 0)
            {
                foreach (var detail in dto.details)
                {
                    if (string.IsNullOrWhiteSpace(detail.category_content)) continue;
                    var detailInsert = @"
                        INSERT INTO pds_detail (pds_detail_id, pds_id, pdsid, category_id, category_name, category_content, sort_order, tenant_code)
                        VALUES (gen_random_uuid(), @pdsId, @pdsId, @category_id, @category_name, @category_content, @sort_order, @tenant_code)";
                    await db.ExecuteAsync(detailInsert, new {
                        pdsId, detail.category_id, detail.category_name, detail.category_content, detail.sort_order, tenant_code
                    });
                }
            }

            return pdsId;
        }

        public async Task<bool> AuthorizePatientDischargeSummaryAsync(DischargeSummaryModel.AuthorizeDischargeSummaryDto dto, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);
            var sql = @"
                UPDATE pds_master
                SET auth_user1 = COALESCE(@auth_user1, auth_user1),
                    auth_user2 = COALESCE(@auth_user2, auth_user2),
                    auth_user3 = COALESCE(@auth_user3, auth_user3),
                    updated_at = NOW()
                WHERE pds_id = @pds_id AND tenant_code = @tenant_code";
            int rows = await db.ExecuteAsync(sql, new { dto.auth_user1, dto.auth_user2, dto.auth_user3, dto.pds_id, tenant_code });
            return rows > 0;
        }

        public async Task<string> GetDischargeSummaryReportPdfAsync(Guid pds_id_or_patcode, string tenant_code, bool? isletterhead)
        {
            using IDbConnection db = new NpgsqlConnection(_conn);

            var pds = await GetPatientDischargeSummaryAsync(pds_id_or_patcode.ToString(), tenant_code);
            if (pds == null || pds.Master == null)
            {
                throw new Exception("Discharge summary not found.");
            }

            // Fetch tenant info
            var tenantSql = @"SELECT legal_name AS CompanyName, 
                                     CONCAT_WS(', ', NULLIF(address_line1, ''), NULLIF(address_line2, ''), NULLIF(city, ''), NULLIF(state, ''), NULLIF(pincode, '')) AS CompanyAddress,
                                     contact_number AS CompanyContactNo, contact_email AS CompanyEmail
                              FROM mastertenant.tenants WHERE tenant_code = @tenant_code LIMIT 1";
            var tenantInfo = await db.QueryFirstOrDefaultAsync<dynamic>(tenantSql, new { tenant_code });

            // Fetch Lab Setting header & footer image toggles
            var lsConfig = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                @"SELECT * FROM lab_settings WHERE tenant_code = @tenant_code AND COALESCE(deleted, false) = false ORDER BY bh_code LIMIT 1",
                new { tenant_code });

            if (lsConfig == null || (string.IsNullOrWhiteSpace(lsConfig.header_path) && string.IsNullOrWhiteSpace(lsConfig.header_image_path) && string.IsNullOrWhiteSpace(lsConfig.footer_path) && string.IsNullOrWhiteSpace(lsConfig.footer_image_path)))
            {
                var fallbackLs = await db.QueryFirstOrDefaultAsync<LabSettingModel.lab_settings>(
                    @"SELECT * FROM lab_settings 
                      WHERE tenant_code = @tenant_code AND COALESCE(deleted, false) = false 
                        AND (header_path IS NOT NULL OR header_image_path IS NOT NULL OR footer_path IS NOT NULL OR footer_image_path IS NOT NULL) 
                      LIMIT 1",
                    new { tenant_code });
                if (fallbackLs != null) lsConfig = fallbackLs;
            }

            string? hKey = !string.IsNullOrWhiteSpace(lsConfig?.header_path) ? lsConfig.header_path : lsConfig?.header_image_path;
            string? fKey = !string.IsNullOrWhiteSpace(lsConfig?.footer_path) ? lsConfig.footer_path : lsConfig?.footer_image_path;

            byte[]? headerImage = null;
            byte[]? footerImage = null;

            if (!string.IsNullOrWhiteSpace(hKey))
            {
                try { var hRes = await _s3Service.DownloadAsync(hKey); if (hRes.HasValue && hRes.Value.Data != null && hRes.Value.Data.Length > 0) headerImage = hRes.Value.Data; } catch (Exception ex) { Console.WriteLine($"Header download error: {ex.Message}"); }
            }
            if (!string.IsNullOrWhiteSpace(fKey))
            {
                try { var fRes = await _s3Service.DownloadAsync(fKey); if (fRes.HasValue && fRes.Value.Data != null && fRes.Value.Data.Length > 0) footerImage = fRes.Value.Data; } catch (Exception ex) { Console.WriteLine($"Footer download error: {ex.Message}"); }
            }

            bool showHeaderFooter = (headerImage != null || footerImage != null) || (lsConfig?.show_dischargesummary_header_footer_image ?? true);

            // Fetch signatures for Auth Level 1, 2, 3
            byte[]? auth1Sign = null; string? auth1Name = null; string? auth1Desig = null;
            byte[]? auth2Sign = null; string? auth2Name = null; string? auth2Desig = null;
            byte[]? auth3Sign = null; string? auth3Name = null; string? auth3Desig = null;

            if (!string.IsNullOrWhiteSpace(pds.Master.auth_user1))
            {
                var u = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT name, description, signature_image FROM mastertenant.user_master WHERE user_code::text = @uc LIMIT 1", new { uc = pds.Master.auth_user1 });
                if (u != null)
                {
                    auth1Name = u.name; auth1Desig = u.description;
                    if (!string.IsNullOrWhiteSpace((string?)u.signature_image))
                    {
                        var res = await _s3Service.DownloadAsync((string)u.signature_image);
                        if (res.HasValue) auth1Sign = res.Value.Data;
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(pds.Master.auth_user2))
            {
                var u = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT name, description, signature_image FROM mastertenant.user_master WHERE user_code::text = @uc LIMIT 1", new { uc = pds.Master.auth_user2 });
                if (u != null)
                {
                    auth2Name = u.name; auth2Desig = u.description;
                    if (!string.IsNullOrWhiteSpace((string?)u.signature_image))
                    {
                        var res = await _s3Service.DownloadAsync((string)u.signature_image);
                        if (res.HasValue) auth2Sign = res.Value.Data;
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(pds.Master.auth_user3))
            {
                var u = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT name, description, signature_image FROM mastertenant.user_master WHERE user_code::text = @uc LIMIT 1", new { uc = pds.Master.auth_user3 });
                if (u != null)
                {
                    auth3Name = u.name; auth3Desig = u.description;
                    if (!string.IsNullOrWhiteSpace((string?)u.signature_image))
                    {
                        var res = await _s3Service.DownloadAsync((string)u.signature_image);
                        if (res.HasValue) auth3Sign = res.Value.Data;
                    }
                }
            }

            var payload = new
            {
                pds_id = pds.Master.pds_id,
                patcode = pds.Master.patcode,
                patient_name = pds.PatientName,
                patient_id = pds.PatientId,
                gender = pds.Gender,
                age = pds.Age,
                mobile_no = pds.MobileNo,
                doctor_name = pds.DoctorName,
                bed_no = pds.BedNo,
                admission_date = pds.Master.admission_date ?? pds.Master.created_at,
                discharge_date = pds.Master.discharge_date,
                discharge_type = pds.Master.discharge_type,
                overall_notes = pds.Master.overall_notes,
                company_name = tenantInfo?.companyname ?? "",
                company_address = tenantInfo?.companyaddress ?? "",
                company_contact = tenantInfo?.companycontactno ?? "",
                company_email = tenantInfo?.companyemail ?? "",
                details = pds.Details.Select(d => new
                {
                    category_id = d.category_id,
                    category_name = d.category_name,
                    category_content = d.category_content,
                    sort_order = d.sort_order
                }).ToList(),
                header_image = headerImage,
                footer_image = footerImage,
                show_header_footer_image = showHeaderFooter,
                is_letterhead = isletterhead ?? false,
                auth1_sign = auth1Sign,
                auth1_name = auth1Name,
                auth1_desig = auth1Desig,
                auth2_sign = auth2Sign,
                auth2_name = auth2Name,
                auth2_desig = auth2Desig,
                auth3_sign = auth3Sign,
                auth3_name = auth3Name,
                auth3_desig = auth3Desig,
                tenant_id = tenant_code
            };

            var client = _httpClientFactory.CreateClient("ReportServer");
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/dischargesummary/getpdf", content);
            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync();
                throw new Exception($"ReportingServer returned status {response.StatusCode}: {errStr}");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}
