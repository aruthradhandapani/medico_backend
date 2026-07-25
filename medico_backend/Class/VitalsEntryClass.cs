using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class VitalsClass
    {
        private readonly string db_conn;
        private readonly OgQueueClass ogQueue;

        public VitalsClass(IConfiguration configuration, OgQueueClass _ogQueue)
        {
            db_conn = configuration.GetConnectionString("conn");
            ogQueue = _ogQueue;
        }

        // ─────────────────────────────────────────
        // Checks whether any of the 5 investigation slots holds this value
        // ─────────────────────────────────────────
        private static bool HasInvestigation(VitalsModel data, string value)
        {
            return string.Equals(data.in1, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.in2, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.in3, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.in4, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.in5, value, StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────
        // A token number is a reserved "dummy" slot if it falls at
        // 1, 26, 51, 76, 101, 126 ... (every 25th token)
        // ─────────────────────────────────────────
        private static bool IsDummySlot(int tokenNumber)
        {
            return (tokenNumber - 1) % 25 == 0;
        }

        public async Task<InsertVitalsResult> Insert(VitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                db.Open();
                using var transaction = db.BeginTransaction();

                var todayUtc = DateTime.UtcNow.Date;
                int? pendingReservedDcode = null;
                string? pendingReservedTokenNo = null;
                TimeOnly? pendingReservedArrivalTime = null;

                // ── NEW: resolve whether this doctor's queue is GROUP-shared or per-doctor ──
                // dcode is mandatory at the controller level, so this always resolves.
                var scope = await db.QueryFirstOrDefaultAsync<DoctorScope>(@"
                    SELECT
                        dm.group_id AS GroupId,
                        COALESCE(dgm.token_type, 'DOCTOR') AS TokenType
                    FROM doctor_master dm
                    LEFT JOIN doctor_group_master dgm
                           ON dgm.group_id = dm.group_id
                          AND dgm.tenant_code = dm.tenant_code
                          AND dgm.is_deleted = false
                          AND dgm.is_active = true
                    WHERE dm.dcode = @dcode
                      AND dm.tenant_code = @tenant_code
                      AND dm.deleted = false",
                    new { dcode = data.dcode, tenant_code = data.tenant_code }, transaction);

                bool isGroupScope = scope != null
                                    && scope.GroupId.HasValue
                                    && string.Equals(scope.TokenType, "GROUP", StringComparison.OrdinalIgnoreCase);

                long? scopeGroupId = isGroupScope ? scope!.GroupId : null;

                // lock key now includes the scope so unrelated queues don't block each other
                string lockScopeToken = isGroupScope ? $"G{scopeGroupId}" : $"D{data.dcode}";
                long lockKey = (tenant_code: data.tenant_code, date: todayUtc, scope: lockScopeToken).GetHashCode();
                await db.ExecuteAsync("SELECT pg_advisory_xact_lock(@lockKey)", new { lockKey }, transaction);

                // Guard against the same insert landing twice (double-click / retry)
                string dupSql = @"
                    SELECT *
                    FROM vitals_entry
                    WHERE tenant_code = @tenant_code
                    AND custcode = @custcode
                    AND in1 IS NOT DISTINCT FROM @in1
                    AND in2 IS NOT DISTINCT FROM @in2
                    AND in3 IS NOT DISTINCT FROM @in3
                    AND in4 IS NOT DISTINCT FROM @in4
                    AND in5 IS NOT DISTINCT FROM @in5
                    AND test_name IS NOT DISTINCT FROM @test_name
                    AND dcode IS NOT DISTINCT FROM @dcode
                    AND deleted = false
                    AND created_at > (now() - interval '5 seconds')
                    ORDER BY created_at DESC
                    LIMIT 1";

                var dup = await db.QueryFirstOrDefaultAsync<VitalsModel>(
                    dupSql,
                    new
                    {
                        tenant_code = data.tenant_code,
                        custcode = data.custcode,
                        data.in1,
                        data.in2,
                        data.in3,
                        data.in4,
                        data.in5,
                        test_name = data.test_name,
                        dcode = data.dcode
                    },
                    transaction);

                if (dup != null)
                {
                    transaction.Commit();
                    return new InsertVitalsResult { message = "Success", token_no = dup.token_no };
                }

                // ── Last token issued today, SCOPED to this group (or this doctor individually) ──
                string lastTokenSql = isGroupScope
                    ? @"SELECT token_no
                        FROM vitals_entry
                        WHERE tenant_code = @tenant_code
                        AND entered_date::date = @today
                        AND group_id = @scopeGroupId
                        ORDER BY CAST(token_no AS INT) DESC
                        LIMIT 1"
                    : @"SELECT token_no
                        FROM vitals_entry
                        WHERE tenant_code = @tenant_code
                        AND entered_date::date = @today
                        AND group_id IS NULL
                        AND dcode = @dcode
                        ORDER BY CAST(token_no AS INT) DESC
                        LIMIT 1";

                var lastTokenStr = await db.ExecuteScalarAsync<string?>(lastTokenSql,
                    new { tenant_code = data.tenant_code, today = todayUtc, scopeGroupId, dcode = data.dcode },
                    transaction);

                int lastToken = lastTokenStr != null ? int.Parse(lastTokenStr) : 0;
                int nextToken = lastToken + 1;

                if (IsDummySlot(nextToken))
                {
                    int? dummyDcode = data.dcode;

                    if (isGroupScope)
                    {
                        var anyGroupDoctorDcode = await db.ExecuteScalarAsync<int?>(@"
            SELECT dm.dcode
            FROM doctor_master dm
            WHERE dm.group_id = @scopeGroupId
            AND dm.tenant_code = @tenant_code
            AND dm.deleted = false
            ORDER BY dm.dcode
            LIMIT 1",
                            new { scopeGroupId, tenant_code = data.tenant_code }, transaction);

                        dummyDcode = anyGroupDoctorDcode ?? data.dcode;
                    }

                    var dummy = new VitalsModel
                    {
                        tenant_code = data.tenant_code,
                        token_no = nextToken.ToString("D3"),
                        custcode = "RESERVED",
                        dcode = dummyDcode,
                        group_id = isGroupScope ? scopeGroupId : null,
                        in1 = "doctor",
                        is_vip = true,
                        status = "reserved",
                        arrival_time = data.arrival_time ?? TimeOnly.FromDateTime(DateTime.Now),
                        entered_date = DateTime.UtcNow,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow,
                        deleted = false
                    };

                    await db.InsertAsync(dummy, transaction);

                    // ✅ capture so we can push into og_queue AFTER commit succeeds
                    pendingReservedDcode = dummyDcode;
                    pendingReservedTokenNo = dummy.token_no;
                    pendingReservedArrivalTime = dummy.arrival_time;

                    nextToken++;
                }

                data.token_no = nextToken.ToString("D3");
                data.group_id = scopeGroupId; // NULL for individual-queue doctors, set for group-queue doctors
                data.is_vip = false; // real patient rows are never the reserved dummy slot
                data.entered_date = DateTime.UtcNow;
                data.created_at = DateTime.UtcNow;
                data.updated_at = DateTime.UtcNow;
                data.deleted = false;

                data.in1_status = !string.IsNullOrEmpty(data.in1) ? (data.in1_status ?? "waiting_for_test") : null;
                data.in2_status = !string.IsNullOrEmpty(data.in2) ? (data.in2_status ?? "waiting_for_test") : null;
                data.in3_status = !string.IsNullOrEmpty(data.in3) ? (data.in3_status ?? "waiting_for_test") : null;
                data.in4_status = !string.IsNullOrEmpty(data.in4) ? (data.in4_status ?? "waiting_for_test") : null;
                data.in5_status = !string.IsNullOrEmpty(data.in5) ? (data.in5_status ?? "waiting_for_test") : null;


                var newId = await db.InsertAsync(data, transaction);

                transaction.Commit();

                if (pendingReservedTokenNo != null)
                {
                    await ogQueue.AddReservedToQueue(data.tenant_code!, pendingReservedDcode, pendingReservedTokenNo, pendingReservedArrivalTime);
                }

                if (newId > 0)
                {
                    if (HasInvestigation(data, "doctor")
                        && data.dcode.HasValue
                        && !string.IsNullOrEmpty(data.custcode))
                    {
                        await ogQueue.AddToQueue(data.tenant_code!, data.custcode!, data.dcode.Value, data.token_no!, data.arrival_time, data.test_name, "direct");
                    }
                    return new InsertVitalsResult { message = "Success", token_no = data.token_no };
                }

                return new InsertVitalsResult { message = "Failed", token_no = null };
            }
            catch (Exception ex)
            {
                return new InsertVitalsResult { message = ex.Message, token_no = null };
            }
        }

        // small POCO for the doctor-scope lookup
        private class DoctorScope
        {
            public long? GroupId { get; set; }
            public string TokenType { get; set; } = "DOCTOR";
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(VitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<VitalsModel>(
                    "SELECT * FROM vitals_entry WHERE vitalentryid = @vitalentryid AND tenant_code = @tenant_code AND deleted = false",
                    new { vitalentryid = data.vitalentryid, tenant_code = data.tenant_code });

                if (existing == null)
                    return "Record not found for this tenant";

                // Immutable fields — always keep DB values
                data.token_no = existing.token_no;
                data.is_vip = existing.is_vip;
                data.entered_date = existing.entered_date;
                data.created_at = existing.created_at;
                data.updated_at = DateTime.UtcNow;
                data.deleted = existing.deleted;

                // Mutable fields — only overwrite if the caller actually sent a value,
                // otherwise keep what's already in the DB (partial-update semantics)
                data.custcode = !string.IsNullOrEmpty(data.custcode) ? data.custcode : existing.custcode;
                data.dcode = data.dcode ?? existing.dcode;
                data.in1 = data.in1 ?? existing.in1;
                data.in2 = data.in2 ?? existing.in2;
                data.in3 = data.in3 ?? existing.in3;
                data.in4 = data.in4 ?? existing.in4;
                data.in5 = data.in5 ?? existing.in5;
                data.in1_status = data.in1_status ?? existing.in1_status;
                data.in2_status = data.in2_status ?? existing.in2_status;
                data.in3_status = data.in3_status ?? existing.in3_status;
                data.in4_status = data.in4_status ?? existing.in4_status;
                data.in5_status = data.in5_status ?? existing.in5_status;
                data.test_name = data.test_name ?? existing.test_name;
                data.status = data.status ?? existing.status;
                data.arrival_time = data.arrival_time ?? existing.arrival_time;
                data.usercode = data.usercode != 0 ? data.usercode : existing.usercode;
                data.computercode = data.computercode != 0 ? data.computercode : existing.computercode;
                data.group_id = data.group_id ?? existing.group_id;

                var result = await db.UpdateAsync(data);

                if (result)
                {
                    bool anyLabScanNewlyReceived =
                        (string.Equals(data.in1_status, "report_received", StringComparison.OrdinalIgnoreCase) && IsLabScanValue(data.in1)) ||
                        (string.Equals(data.in2_status, "report_received", StringComparison.OrdinalIgnoreCase) && IsLabScanValue(data.in2)) ||
                        (string.Equals(data.in3_status, "report_received", StringComparison.OrdinalIgnoreCase) && IsLabScanValue(data.in3)) ||
                        (string.Equals(data.in4_status, "report_received", StringComparison.OrdinalIgnoreCase) && IsLabScanValue(data.in4)) ||
                        (string.Equals(data.in5_status, "report_received", StringComparison.OrdinalIgnoreCase) && IsLabScanValue(data.in5));

                    if (anyLabScanNewlyReceived)
                    {
                        var promoteResult = await PromoteToConsultationIfReady(data.vitalentryid, data.tenant_code!);
                        return $"Success ({promoteResult})";
                    }
                }

                return result ? "Success" : "Failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static bool IsLabScanValue(string? investigation) =>
            string.Equals(investigation, "lab", StringComparison.OrdinalIgnoreCase)
            || string.Equals(investigation, "scan", StringComparison.OrdinalIgnoreCase)
            || string.Equals(investigation, "ecg-echo", StringComparison.OrdinalIgnoreCase);

        // ─────────────────────────────────────────
        // UPDATE STATUS ONLY (quick status change, e.g. from token/lab/scan screens)
        // When lab/scan status becomes 'report_received', pushes patient into OG queue
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(int vitalentryid, string tenant_code, string status, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET status = @status,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    vitalentryid,
                    tenant_code,
                    status,
                    usercode,
                    computercode,
                    updated_at = DateTime.UtcNow
                });

                if (rows > 0 && string.Equals(status, "report_received", StringComparison.OrdinalIgnoreCase))
                {
                    var v = await GetById(vitalentryid, tenant_code);
                    if (v != null && (HasInvestigation(v, "lab") || HasInvestigation(v, "scan") || HasInvestigation(v, "ecg-echo")))
                    {
                        var promoteResult = await PromoteToConsultationIfReady(vitalentryid, tenant_code);
                        return $"Success ({promoteResult})";
                    }
                }
                return rows > 0 ? "Success" : "Record not found";

                
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE (soft delete → deleted = true)
        // Token number is NOT reused — Insert() computes the next token from
        // MAX(token_no) regardless of deleted status, so this is safe.
        // ─────────────────────────────────────────
        public async Task<string> Delete(int vitalentryid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET deleted = true,
                        updated_at = @updated_at
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { vitalentryid, tenant_code, updated_at = DateTime.UtcNow });
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (active, non-deleted, non-dummy) — joined with customer & doctor for display
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    v.vitalentryid,
                    v.tenant_code,
                    v.token_no,
                    v.custcode,
                    c.name AS patient_name,
                    v.dcode,
                    d.name AS doctor_name,
                    v.in1,
                    v.in2,
                    v.in3,
                    v.in4,
                    v.in5,
                    v.in1_status,
                    v.in2_status,
                    v.in3_status,
                    v.in4_status,
                    v.in5_status,
                    v.test_name,
                    v.status,
                    v.is_vip,
                    v.entered_date,
                    v.arrival_time,
                    v.usercode,
                    v.computercode,
                    v.created_at,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
                WHERE v.tenant_code = @tenant_code
                AND v.deleted = false
                AND v.status != 'dummy'
                ORDER BY v.created_at DESC";

            var result = await db.QueryAsync(sql, new { tenant_code });
            return result;
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<VitalsModel?> GetById(int vitalentryid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM vitals_entry
                WHERE vitalentryid = @vitalentryid
                AND tenant_code = @tenant_code
                AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<VitalsModel>(sql, new { vitalentryid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY STATUS (e.g. token display screen filtering)
        // Dummy rows are excluded unless the caller explicitly asks for status = "dummy"
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetByStatus(string status, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    v.vitalentryid,
                    v.tenant_code,
                    v.token_no,
                    v.custcode,
                    c.name AS patient_name,
                    v.dcode,
                    d.name AS doctor_name,
                    v.in1,
                    v.in2,
                    v.in3,
                    v.in4,
                    v.in5,
                    v.test_name,
                    v.status,
                    v.is_vip,
                    v.entered_date,
                    v.arrival_time,
                    v.usercode,
                    v.computercode,
                    v.created_at,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
                WHERE v.status = @status
                AND v.tenant_code = @tenant_code
                AND v.deleted = false
                ORDER BY v.entered_date ASC";

            var result = await db.QueryAsync(sql, new { status, tenant_code });
            return result;
        }

        // ─────────────────────────────────────────
        // GET ALL DUMMY TOKENS (no filters) — every reserved VIP slot across all doctors
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetAllDummyList(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT
            v.vitalentryid,
            v.token_no,
            v.custcode,
            'Reserved' AS patient_name,
            v.dcode,
            d.name AS doctor_name,
            v.group_id,
            v.in1,
            v.is_vip,
            v.status,
            v.arrival_time,
            v.entered_date,
            v.created_at
        FROM vitals_entry v
        LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
        WHERE v.tenant_code = @tenant_code
        AND v.status = 'reserved'
        AND v.is_vip = true
        AND v.in1 ILIKE 'doctor'
        AND v.deleted = false
        ORDER BY v.entered_date DESC";

            return await db.QueryAsync(sql, new { tenant_code });
        }
        // ─────────────────────────────────────────
        // UPDATE A SINGLE INVESTIGATION SLOT'S STATUS (e.g. lab report received,
        // while doctor consultation for the same visit is still pending).
        // slotNumber: 1-5, corresponding to in1_status .. in5_status
        // Pushes to OG queue when THAT SLOT's investigation type becomes report_received.
        // ─────────────────────────────────────────
        private static readonly HashSet<string> ValidSlots = new(StringComparer.OrdinalIgnoreCase)
{
    "in1", "in2", "in3", "in4", "in5"
};

        public async Task<string> UpdateSlotStatus(int vitalentryid, string tenant_code, string slot, string status, int usercode, int computercode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slot) || !ValidSlots.Contains(slot))
                    return "slot must be one of: in1, in2, in3, in4, in5";

                using IDbConnection db = new NpgsqlConnection(db_conn);

                string slotLower = slot.ToLowerInvariant();
                string statusColumn = $"{slotLower}_status";

                string sql = $@"
            UPDATE vitals_entry
            SET {statusColumn} = @status,
                usercode = @usercode,
                computercode = @computercode,
                updated_at = @updated_at
            WHERE vitalentryid = @vitalentryid
            AND tenant_code = @tenant_code
            AND deleted = false
            AND {slotLower} IS NOT NULL";

                var rows = await db.ExecuteAsync(sql, new
                {
                    vitalentryid,
                    tenant_code,
                    status,
                    usercode,
                    computercode,
                    updated_at = DateTime.UtcNow
                });

                if (rows > 0 && string.Equals(status, "report_received", StringComparison.OrdinalIgnoreCase))
                {
                    var v = await GetById(vitalentryid, tenant_code);
                    if (v != null)
                    {
                        string? investigationValue = slotLower switch
                        {
                            "in1" => v.in1,
                            "in2" => v.in2,
                            "in3" => v.in3,
                            "in4" => v.in4,
                            "in5" => v.in5,
                            _ => null
                        };

                        if (string.Equals(investigationValue, "lab", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(investigationValue, "scan", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(investigationValue, "ecg-echo", StringComparison.OrdinalIgnoreCase))
                        {
                            await PromoteToConsultationIfReady(vitalentryid, tenant_code);
                        }
                    }
                }

                return rows > 0 ? "Success" : "Record not found or slot is empty";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        // ─────────────────────────────────────────
        // True if this slot is not a lab/scan/ecg-echo slot at all (irrelevant),
        // or if it is one and its report has been received.
        // ─────────────────────────────────────────
        private static bool SlotDoneOrNotApplicable(string? investigation, string? investigationStatus)
        {
            if (string.IsNullOrEmpty(investigation))
                return true;

            bool isLabScan = string.Equals(investigation, "lab", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(investigation, "scan", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(investigation, "ecg-echo", StringComparison.OrdinalIgnoreCase);

            if (!isLabScan)
                return true; // e.g. a 'doctor' slot — not our concern here

            return string.Equals(investigationStatus, "report_received", StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────
        // True only when EVERY lab/scan/ecg-echo slot on this visit has its report in.
        // Mirrors OgQueueClass.AllLabScanCompletedFilter, but in C# against a loaded row.
        // ─────────────────────────────────────────
        private static bool AllLabScanSlotsCompleted(VitalsModel v)
        {
            return SlotDoneOrNotApplicable(v.in1, v.in1_status)
                && SlotDoneOrNotApplicable(v.in2, v.in2_status)
                && SlotDoneOrNotApplicable(v.in3, v.in3_status)
                && SlotDoneOrNotApplicable(v.in4, v.in4_status)
                && SlotDoneOrNotApplicable(v.in5, v.in5_status);
        }

        // ─────────────────────────────────────────
        // Once ALL lab/scan/ecg-echo slots on this visit are report_received, this
        // patient is done with investigations and belongs in the consultation queue.
        // - If no 'doctor' slot exists yet, writes 'doctor' into the first free slot
        //   (status waiting_for_test — same convention Insert() uses).
        // - If a 'doctor' slot already exists (e.g. patient was also entry_type=direct),
        //   leave it as-is.
        // - Either way, pushes/refreshes the og_queue row as entry_type=test_completed,
        //   which is what makes them appear under GetConsultationList / DoctorFilter.
        // ─────────────────────────────────────────
        
        private async Task<string> PromoteToConsultationIfReady(int vitalentryid, string tenant_code)
        {
            var v = await GetById(vitalentryid, tenant_code);
            if (v == null)
                return "Vitals record not found";
            if (!v.dcode.HasValue || string.IsNullOrEmpty(v.custcode))
                return "Missing dcode/custcode — cannot queue";

            if (!AllLabScanSlotsCompleted(v))
                return "Not all lab/scan/ecg-echo slots are report_received yet";

            if (!HasInvestigation(v, "doctor"))
            {
                string? freeSlot = v.in1 == null ? "in1"
                                  : v.in2 == null ? "in2"
                                  : v.in3 == null ? "in3"
                                  : v.in4 == null ? "in4"
                                  : v.in5 == null ? "in5"
                                  : null;

                if (freeSlot == null)
                    return "All 5 investigation slots full — no room to add doctor slot";

                using IDbConnection db = new NpgsqlConnection(db_conn);
                string statusCol = $"{freeSlot}_status";

                string sql = $@"
            UPDATE vitals_entry
            SET {freeSlot} = 'doctor',
                {statusCol} = 'waiting',
                status = 'waiting_for_doctor',
                updated_at = @updated_at
            WHERE vitalentryid = @vitalentryid
            AND tenant_code = @tenant_code
            AND deleted = false";

                await db.ExecuteAsync(sql, new { updated_at = DateTime.UtcNow, vitalentryid, tenant_code });
            }

            var queueResult = await ogQueue.AddToQueue(tenant_code, v.custcode!, v.dcode.Value, v.token_no!, v.arrival_time, v.test_name, "test_completed");
            return $"Promoted to consultation ({queueResult})";
        }
    }

    public class InsertVitalsResult
    {
        public string message { get; set; } = "";
        public string? token_no { get; set; }
    }
}