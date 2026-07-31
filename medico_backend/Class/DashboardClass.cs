using Dapper;
using Npgsql;
using System.Data;

namespace Medico_Backend.Class
{
    // ═════════════════════════════════════════════════════════════════════
    // DASHBOARD / ANALYTICS
    // Read-only reporting layer. Doesn't touch og_queue or vitals_entry —
    // only queries them. Every method is scoped by tenant_code like the
    // rest of the app.
    // ═════════════════════════════════════════════════════════════════════
    public class DashboardClass
    {
        private readonly string db_conn;

        // Same 3 investigation values treated as "lab/scan work" everywhere else
        private const string LabScanEcgArray = "ARRAY['lab','scan','ecg-echo']";

        // "Done" markers per investigation family — lab/scan/ecg-echo close out
        // with 'report_received'; doctor consultations close out with 'completed'
        private const string DoneStatusArray = "ARRAY['report_received','completed']";

        public DashboardClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }
        private const string DoctorSlotFilter = @"
    (
        v.in1 ILIKE 'doctor' OR
        v.in2 ILIKE 'doctor' OR
        v.in3 ILIKE 'doctor' OR
        v.in4 ILIKE 'doctor' OR
        v.in5 ILIKE 'doctor'
    )";

        // ─────────────────────────────────────────────────────────────
        // 1. TODAY'S SNAPSHOT
        // One call for the dashboard header cards: total tokens today,
        // and a pending/completed split for every investigation type
        // (lab, scan, ecg-echo, doctor) seen today.
        // ─────────────────────────────────────────────────────────────
        public async Task<dynamic> GetTodaySnapshot(string tenant_code, DateTime? date = null)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var d = (date ?? DateTime.UtcNow).Date;

            string breakdownSql = $@"
                WITH slots AS (
                    SELECT
                        unnest(ARRAY[v.in1, v.in2, v.in3, v.in4, v.in5]) AS inv,
                        unnest(ARRAY[v.in1_status, v.in2_status, v.in3_status, v.in4_status, v.in5_status]) AS inv_status
                    FROM vitals_entry v
                    WHERE v.tenant_code = @tenant_code
                    AND v.entered_date::date = @d
                    AND v.deleted = false
                    AND v.status != 'dummy'
                )
                SELECT
                    inv AS investigation_type,
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE inv_status ILIKE ANY({DoneStatusArray})) AS completed,
                    COUNT(*) FILTER (WHERE inv_status IS NULL OR inv_status NOT ILIKE ANY({DoneStatusArray})) AS pending
                FROM slots
                WHERE inv IS NOT NULL
                GROUP BY inv
                ORDER BY inv";

            var breakdown = (await db.QueryAsync(breakdownSql, new { tenant_code, d })).ToList();

            string totalsSql = @"
                SELECT
                    COUNT(DISTINCT v.vitalentryid) AS total_visits,
                    COUNT(DISTINCT v.dcode) AS doctors_active,
                    COUNT(DISTINCT v.group_id) FILTER (WHERE v.group_id IS NOT NULL) AS groups_active
                FROM vitals_entry v
                WHERE v.tenant_code = @tenant_code
                AND v.entered_date::date = @d
                AND v.deleted = false
                AND v.status != 'dummy'";

            var totals = await db.QueryFirstOrDefaultAsync(totalsSql, new { tenant_code, d });

            return new
            {
                date = d,
                total_visits = totals?.total_visits ?? 0,
                doctors_active = totals?.doctors_active ?? 0,
                groups_active = totals?.groups_active ?? 0,
                by_investigation_type = breakdown
            };
        }

        // ─────────────────────────────────────────────────────────────
        // 2. DOCTOR-WISE TOKEN COUNT (any date range)
        // Answers "which doctor is generating the most tokens" for
        // today, this week, this month, or any custom range.
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetDoctorWiseCount(string tenant_code, DateTime? fromDate, DateTime? toDate, int? topN = null)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var from = (fromDate ?? DateTime.UtcNow.AddDays(-6)).Date;
            var to = (toDate ?? DateTime.UtcNow).Date;

            string sql = $@"
        SELECT
            v.dcode,
            d.name AS doctor_name,
            d.group_id,
            g.group_name,
            COUNT(DISTINCT v.vitalentryid) AS token_count,
            COUNT(DISTINCT v.vitalentryid) FILTER (WHERE v.entered_date::date = CURRENT_DATE) AS today_count,
            COUNT(DISTINCT v.vitalentryid) FILTER (WHERE COALESCE(o.status, 'waiting') <> 'completed') AS pending_count,
            COUNT(DISTINCT v.vitalentryid) FILTER (WHERE o.status = 'completed') AS completed_count,
            ROUND(COUNT(DISTINCT v.vitalentryid)::numeric / GREATEST((@to::date - @from::date) + 1, 1), 2) AS avg_per_day
        FROM vitals_entry v
        LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
        LEFT JOIN doctor_group_master g ON g.group_id = d.group_id AND g.tenant_code = d.tenant_code AND g.is_deleted = false
        LEFT JOIN og_queue o ON o.tenant_code = v.tenant_code
                            AND o.custcode = v.custcode
                            AND o.dcode = v.dcode
                            AND o.og_token_no = v.token_no
                            AND o.deleted = false
        WHERE v.tenant_code = @tenant_code
        AND v.entered_date::date BETWEEN @from AND @to
        AND v.deleted = false
        AND v.status != 'dummy'
        AND v.custcode != 'RESERVED'
        AND {DoctorSlotFilter}
        GROUP BY v.dcode, d.name, d.group_id, g.group_name
        ORDER BY token_count DESC
        LIMIT @topN";

            return await db.QueryAsync(sql, new { tenant_code, from, to, topN = topN ?? 1000 });
        }

        // ─────────────────────────────────────────────────────────────
        // 3. GROUP-WISE TOKEN COUNT (any date range)
        // Same idea, rolled up to doctor-group level (for GROUP token_type
        // queues that share one token series across several doctors).
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetGroupWiseCount(string tenant_code, DateTime? fromDate, DateTime? toDate)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var from = (fromDate ?? DateTime.UtcNow.AddDays(-6)).Date;
            var to = (toDate ?? DateTime.UtcNow).Date;

            string sql = @"
                SELECT
                    v.group_id,
                    g.group_name,
                    COUNT(*) AS token_count,
                    COUNT(DISTINCT v.dcode) AS doctor_count,
                    COUNT(*) FILTER (WHERE v.entered_date::date = CURRENT_DATE) AS today_count
                FROM vitals_entry v
                LEFT JOIN doctor_group_master g ON g.group_id = v.group_id AND g.tenant_code = v.tenant_code AND g.is_deleted = false
                WHERE v.tenant_code = @tenant_code
                AND v.group_id IS NOT NULL
                AND v.entered_date::date BETWEEN @from AND @to
                AND v.deleted = false
                AND v.status != 'dummy'
                AND v.custcode != 'RESERVED'
                GROUP BY v.group_id, g.group_name
                ORDER BY token_count DESC";

            return await db.QueryAsync(sql, new { tenant_code, from, to });
        }

        // ─────────────────────────────────────────────────────────────
        // 4. HOURLY TOKEN GENERATION DISTRIBUTION (peak-hour heatmap)
        // "At which time were tokens generated the most" — grouped by
        // hour of created_at. Pass date = null for an all-time heatmap,
        // or a specific date for that day only. Optionally scope to one doctor.
        // NOTE: created_at is stored as DateTime.UtcNow, so hours here are
        // UTC hours — convert to local time in the UI if your tenants aren't UTC.
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetHourlyDistribution(string tenant_code, DateTime? date, int? dcode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var d = date?.Date;

            string sql = @"
                SELECT
                    EXTRACT(HOUR FROM v.created_at)::int AS hour_of_day_utc,
                    COUNT(*) AS token_count
                FROM vitals_entry v
                WHERE v.tenant_code = @tenant_code
                AND (@d::date IS NULL OR v.entered_date::date = @d)
                AND (@dcode::int IS NULL OR v.dcode = @dcode)
                AND v.deleted = false
                AND v.status != 'dummy'
                AND v.custcode != 'RESERVED'
                GROUP BY EXTRACT(HOUR FROM v.created_at)
                ORDER BY hour_of_day_utc";

            return await db.QueryAsync(sql, new { tenant_code, d, dcode });
        }

        // ─────────────────────────────────────────────────────────────
        // 5. PAST-DAYS TOKEN TREND
        // Daily totals for the last N days — feeds a line/bar chart.
        // Optionally scoped to one doctor to compare their own trend.
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetPastDaysTrend(string tenant_code, int days, int? dcode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var from = DateTime.UtcNow.Date.AddDays(-(Math.Max(days, 1) - 1));

            string sql = @"
                SELECT
                    v.entered_date::date AS day,
                    COUNT(*) AS token_count,
                    COUNT(*) FILTER (WHERE v.status = 'waiting_for_doctor') AS waiting_for_doctor,
                    COUNT(*) FILTER (WHERE v.status = 'completed') AS completed
                FROM vitals_entry v
                WHERE v.tenant_code = @tenant_code
                AND v.entered_date::date >= @from
                AND (@dcode::int IS NULL OR v.dcode = @dcode)
                AND v.deleted = false
                AND v.status != 'dummy'
                AND v.custcode != 'RESERVED'
                GROUP BY v.entered_date::date
                ORDER BY day";

            return await db.QueryAsync(sql, new { tenant_code, from, dcode });
        }

        // ─────────────────────────────────────────────────────────────
        // 6. PAST-DAYS TREND, PER DOCTOR (deep-dive)
        // Same as #5 but broken out by doctor per day — good for a
        // stacked-area chart or a doctor-vs-doctor comparison over time.
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetPastDaysTrendByDoctor(string tenant_code, int days)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var from = DateTime.UtcNow.Date.AddDays(-(Math.Max(days, 1) - 1));

            string sql = @"
                SELECT
                    v.entered_date::date AS day,
                    v.dcode,
                    d.name AS doctor_name,
                    COUNT(*) AS token_count
                FROM vitals_entry v
                LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
                WHERE v.tenant_code = @tenant_code
                AND v.entered_date::date >= @from
                AND v.deleted = false
                AND v.status != 'dummy'
                AND v.custcode != 'RESERVED'
                GROUP BY v.entered_date::date, v.dcode, d.name
                ORDER BY day, token_count DESC";

            return await db.QueryAsync(sql, new { tenant_code, from });
        }

        // ─────────────────────────────────────────────────────────────
        // 7. INVESTIGATION-TYPE BREAKDOWN OVER A DATE RANGE
        // Lab vs scan vs ecg-echo vs doctor, day by day — for a stacked
        // bar chart of "what kind of work is the queue mostly made of".
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetInvestigationBreakdownTrend(string tenant_code, DateTime? fromDate, DateTime? toDate)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var from = (fromDate ?? DateTime.UtcNow.AddDays(-6)).Date;
            var to = (toDate ?? DateTime.UtcNow).Date;

            string sql = @"
                WITH slots AS (
                    SELECT
                        v.entered_date::date AS day,
                        unnest(ARRAY[v.in1, v.in2, v.in3, v.in4, v.in5]) AS inv
                    FROM vitals_entry v
                    WHERE v.tenant_code = @tenant_code
                    AND v.entered_date::date BETWEEN @from AND @to
                    AND v.deleted = false
                    AND v.status != 'dummy'
                )
                SELECT day, inv AS investigation_type, COUNT(*) AS count
                FROM slots
                WHERE inv IS NOT NULL
                GROUP BY day, inv
                ORDER BY day, inv";

            return await db.QueryAsync(sql, new { tenant_code, from, to });
        }

        // ─────────────────────────────────────────────────────────────
        // 8. APPROXIMATE TURNAROUND TIME per investigation type
        // Average minutes between row creation and its updated_at, for
        // rows currently sitting at 'report_received'.
        //
        // ⚠️ APPROXIMATION, NOT AN EXACT SLA METRIC: the schema has one
        // updated_at per row, not one per slot. If a row was edited more
        // than once (e.g. lab status set, then something else changed
        // later), this will overstate the true turnaround. For an exact
        // number, add per-slot completed_at columns and stamp them in
        // VitalsClass.UpdateSlotStatus. Until then, treat this as a
        // rough trend indicator, not a guarantee.
        // ─────────────────────────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetApproxTurnaroundTime(string tenant_code, DateTime? fromDate, DateTime? toDate)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var from = (fromDate ?? DateTime.UtcNow.AddDays(-6)).Date;
            var to = (toDate ?? DateTime.UtcNow).Date;

            string sql = @"
                SELECT investigation_type, COUNT(*) AS sample_size,
                       ROUND(AVG(minutes)::numeric, 1) AS avg_minutes,
                       ROUND(MIN(minutes)::numeric, 1) AS min_minutes,
                       ROUND(MAX(minutes)::numeric, 1) AS max_minutes
                FROM (
                    SELECT 'lab' AS investigation_type, EXTRACT(EPOCH FROM (v.updated_at - v.created_at)) / 60 AS minutes
                    FROM vitals_entry v
                    WHERE v.tenant_code = @tenant_code AND v.entered_date::date BETWEEN @from AND @to AND v.deleted = false
                    AND ((v.in1 ILIKE 'lab' AND v.in1_status ILIKE 'report_received')
                      OR (v.in2 ILIKE 'lab' AND v.in2_status ILIKE 'report_received')
                      OR (v.in3 ILIKE 'lab' AND v.in3_status ILIKE 'report_received')
                      OR (v.in4 ILIKE 'lab' AND v.in4_status ILIKE 'report_received')
                      OR (v.in5 ILIKE 'lab' AND v.in5_status ILIKE 'report_received'))

                    UNION ALL

                    SELECT 'scan', EXTRACT(EPOCH FROM (v.updated_at - v.created_at)) / 60
                    FROM vitals_entry v
                    WHERE v.tenant_code = @tenant_code AND v.entered_date::date BETWEEN @from AND @to AND v.deleted = false
                    AND ((v.in1 ILIKE 'scan' AND v.in1_status ILIKE 'report_received')
                      OR (v.in2 ILIKE 'scan' AND v.in2_status ILIKE 'report_received')
                      OR (v.in3 ILIKE 'scan' AND v.in3_status ILIKE 'report_received')
                      OR (v.in4 ILIKE 'scan' AND v.in4_status ILIKE 'report_received')
                      OR (v.in5 ILIKE 'scan' AND v.in5_status ILIKE 'report_received'))

                    UNION ALL

                    SELECT 'ecg-echo', EXTRACT(EPOCH FROM (v.updated_at - v.created_at)) / 60
                    FROM vitals_entry v
                    WHERE v.tenant_code = @tenant_code AND v.entered_date::date BETWEEN @from AND @to AND v.deleted = false
                    AND ((v.in1 ILIKE 'ecg-echo' AND v.in1_status ILIKE 'report_received')
                      OR (v.in2 ILIKE 'ecg-echo' AND v.in2_status ILIKE 'report_received')
                      OR (v.in3 ILIKE 'ecg-echo' AND v.in3_status ILIKE 'report_received')
                      OR (v.in4 ILIKE 'ecg-echo' AND v.in4_status ILIKE 'report_received')
                      OR (v.in5 ILIKE 'ecg-echo' AND v.in5_status ILIKE 'report_received'))
                ) sub
                GROUP BY investigation_type";

            return await db.QueryAsync(sql, new { tenant_code, from, to });
        }

        // ─────────────────────────────────────────────────────────────
        // 9. STATUS FUNNEL for a given day
        // Where is everyone stuck right now: waiting for test, test
        // pending, waiting for doctor, in consultation, completed.
        // ─────────────────────────────────────────────────────────────
        public async Task<dynamic> GetStatusFunnel(string tenant_code, DateTime? date = null)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            var d = (date ?? DateTime.UtcNow).Date;

            string sql = @"
                SELECT
                    COALESCE(v.status, 'unknown') AS status,
                    COUNT(*) AS count
                FROM vitals_entry v
                WHERE v.tenant_code = @tenant_code
                AND v.entered_date::date = @d
                AND v.deleted = false
                AND v.status != 'dummy'
                AND v.custcode != 'RESERVED'
                GROUP BY v.status
                ORDER BY count DESC";

            var vitalsFunnel = await db.QueryAsync(sql, new { tenant_code, d });

            string queueSql = @"
                SELECT
                    COALESCE(o.status, 'unknown') AS status,
                    COUNT(*) AS count
                FROM og_queue o
                WHERE o.tenant_code = @tenant_code
                AND o.created_at::date = @d
                AND o.deleted = false
                GROUP BY o.status
                ORDER BY count DESC";

            var queueFunnel = await db.QueryAsync(queueSql, new { tenant_code, d });

            return new { date = d, vitals_status = vitalsFunnel, queue_status = queueFunnel };
        }

        // ─────────────────────────────────────────────────────────────
        // 10. FULL DEEP-DIVE — one call that bundles everything above.
        // Meant for a single dashboard page load; each piece can still
        // be called individually for lazy-loaded widgets.
        // ─────────────────────────────────────────────────────────────
        public async Task<dynamic> GetFullDashboard(string tenant_code, int trendDays = 30)
        {
            var today = await GetTodaySnapshot(tenant_code);
            var doctorWiseLast7 = await GetDoctorWiseCount(tenant_code, DateTime.UtcNow.AddDays(-6), DateTime.UtcNow);
            var groupWiseLast7 = await GetGroupWiseCount(tenant_code, DateTime.UtcNow.AddDays(-6), DateTime.UtcNow);
            var hourlyToday = await GetHourlyDistribution(tenant_code, DateTime.UtcNow, null);
            var hourlyAllTime = await GetHourlyDistribution(tenant_code, null, null);
            var pastDaysTrend = await GetPastDaysTrend(tenant_code, trendDays, null);
            var investigationTrend = await GetInvestigationBreakdownTrend(tenant_code, DateTime.UtcNow.AddDays(-(trendDays - 1)), DateTime.UtcNow);
            var turnaround = await GetApproxTurnaroundTime(tenant_code, DateTime.UtcNow.AddDays(-(trendDays - 1)), DateTime.UtcNow);
            var funnel = await GetStatusFunnel(tenant_code);

            return new
            {
                today_snapshot = today,
                doctor_wise_last_7_days = doctorWiseLast7,
                group_wise_last_7_days = groupWiseLast7,
                hourly_distribution_today = hourlyToday,
                hourly_distribution_all_time = hourlyAllTime,
                past_days_trend = pastDaysTrend,
                investigation_breakdown_trend = investigationTrend,
                approx_turnaround_time = turnaround,
                status_funnel_today = funnel
            };
        }
    }
}