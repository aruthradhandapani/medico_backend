using Medico_Backend.Class;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    // ═════════════════════════════════════════════════════════════════════
    // /api/dashboard — read-only analytics endpoints.
    // Register DashboardClass in Program.cs like the other classes:
    //   builder.Services.AddScoped<DashboardClass>();
    // ═════════════════════════════════════════════════════════════════════
    // Every request must send:  tenant_code: T1   (an HTTP header, not a query param)
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private const string TenantHeaderName = "tenant_code";
        private readonly DashboardClass dashboard;

        public DashboardController(DashboardClass _dashboard)
        {
            dashboard = _dashboard;
        }

        // Pulls tenant_code out of the request headers for every action below.
        // Returns null (and the caller returns 400) if it's missing/blank.
        private bool TryGetTenantCode(out string tenant_code, out IActionResult? error)
        {
            tenant_code = Request.Headers[TenantHeaderName].ToString();

            if (string.IsNullOrWhiteSpace(tenant_code))
            {
                error = BadRequest($"Missing required header: {TenantHeaderName}");
                return false;
            }

            error = null;
            return true;
        }

        // GET /api/dashboard/today-snapshot?date=2026-07-28
        // header: tenant_code: T1
        [HttpGet("today-snapshot")]
        public async Task<IActionResult> TodaySnapshot([FromQuery] DateTime? date)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetTodaySnapshot(tenant_code, date);
            return Ok(result);
        }

        // GET /api/dashboard/doctor-wise?from=2026-07-01&to=2026-07-28&top=10
        // header: tenant_code: T1
        [HttpGet("doctor-wise")]
        public async Task<IActionResult> DoctorWise([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? top)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetDoctorWiseCount(tenant_code, from, to, top);
            return Ok(result);
        }

        // GET /api/dashboard/group-wise?from=2026-07-01&to=2026-07-28
        // header: tenant_code: T1
        [HttpGet("group-wise")]
        public async Task<IActionResult> GroupWise([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetGroupWiseCount(tenant_code, from, to);
            return Ok(result);
        }

        // GET /api/dashboard/hourly-distribution?date=2026-07-28&dcode=12
        // header: tenant_code: T1
        // Omit date for an all-time peak-hour heatmap instead of a single day.
        [HttpGet("hourly-distribution")]
        public async Task<IActionResult> HourlyDistribution([FromQuery] DateTime? date, [FromQuery] int? dcode)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetHourlyDistribution(tenant_code, date, dcode);
            return Ok(result);
        }

        // GET /api/dashboard/past-days-trend?days=30&dcode=12
        // header: tenant_code: T1
        [HttpGet("past-days-trend")]
        public async Task<IActionResult> PastDaysTrend([FromQuery] int days = 30, [FromQuery] int? dcode = null)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetPastDaysTrend(tenant_code, days, dcode);
            return Ok(result);
        }

        // GET /api/dashboard/past-days-trend-by-doctor?days=30
        // header: tenant_code: T1
        [HttpGet("past-days-trend-by-doctor")]
        public async Task<IActionResult> PastDaysTrendByDoctor([FromQuery] int days = 30)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetPastDaysTrendByDoctor(tenant_code, days);
            return Ok(result);
        }

        // GET /api/dashboard/investigation-breakdown-trend?from=2026-07-01&to=2026-07-28
        // header: tenant_code: T1
        [HttpGet("investigation-breakdown-trend")]
        public async Task<IActionResult> InvestigationBreakdownTrend([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetInvestigationBreakdownTrend(tenant_code, from, to);
            return Ok(result);
        }

        // GET /api/dashboard/turnaround-time?from=2026-07-01&to=2026-07-28
        // header: tenant_code: T1
        // Approximate — see the caveat in DashboardClass.GetApproxTurnaroundTime.
        [HttpGet("turnaround-time")]
        public async Task<IActionResult> TurnaroundTime([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetApproxTurnaroundTime(tenant_code, from, to);
            return Ok(result);
        }

        // GET /api/dashboard/status-funnel?date=2026-07-28
        // header: tenant_code: T1
        [HttpGet("status-funnel")]
        public async Task<IActionResult> StatusFunnel([FromQuery] DateTime? date)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetStatusFunnel(tenant_code, date);
            return Ok(result);
        }

        // GET /api/dashboard/full?trendDays=30
        // header: tenant_code: T1
        // One combined payload for a full dashboard page load.
        [HttpGet("full")]
        public async Task<IActionResult> Full([FromQuery] int trendDays = 30)
        {
            if (!TryGetTenantCode(out var tenant_code, out var error)) return error!;
            var result = await dashboard.GetFullDashboard(tenant_code, trendDays);
            return Ok(result);
        }
    }
}