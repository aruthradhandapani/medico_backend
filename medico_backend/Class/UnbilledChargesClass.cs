using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class UnbilledChargesClass
    {
        private readonly string _conn;
        public UnbilledChargesClass(IConfiguration cfg) =>
            _conn = cfg.GetConnectionString("conn")!;

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ── Add consultation fee after OP confirmation ──────────────
        public async Task<string> AddConsultationCharge(AddUnbilledConsultationRequest req, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                // Guard against double-adding consultation charge for the same OP visit
                int already = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM unbilledcharges
                      WHERE opvisitid = @op_id AND entrytype = 'CONSULTATION'
                      AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { req.op_id, tenant_code });

                if (already > 0)
                    return "Consultation charge already pending for this visit";

                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = "CONSULTATION",
                    entryid = req.op_id,
                    chargedate = DateTime.UtcNow,
                    custid = req.custid,
                    opvisitid = req.op_id,
                    tcode = req.tcode,
                    quantity = req.quantity,
                    rate = req.rate,
                    amount = req.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code
                };

                await db.InsertAsync(row);
                return $"Success|UnbilledId:{row.unbilledid}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Add investigation test charges (called from case sheet save, same tx) ──
        public async Task AddInvestigationCharges(
            IDbConnection db, IDbTransaction tx,
            string op_id, decimal custid, string tenant_code,
            IEnumerable<(string inv_det_id, int? test_code, decimal quantity, decimal? rate, decimal? amount)> tests)
        {
            foreach (var t in tests)
            {
                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = "INVESTIGATION",
                    entryid = t.inv_det_id,
                    chargedate = DateTime.UtcNow,
                    custid = custid,
                    opvisitid = op_id,
                    tcode = t.test_code,
                    quantity = (double?)t.quantity,
                    rate = (double?)t.rate,
                    amount = (double?)t.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code
                };
                await db.InsertAsync(row, tx);
            }
        }

        // ── Fetch pending unbilled charges for the billing screen ──
        public async Task<List<UnbilledChargeRow>> GetUnbilledByVisit(string opvisitid, string tenant_code)
        {
            using var db = GetConnection();
            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT * FROM unbilledcharges
                  WHERE opvisitid = @opvisitid AND tenant_code = @tenant_code
                  AND (billedstatus = false OR billedstatus IS NULL)
                  ORDER BY chargedate",
                new { opvisitid, tenant_code });
            return rows.ToList();
        }

        public async Task<List<UnbilledChargeRow>> GetUnbilledByCustomer(decimal custid, string tenant_code)
        {
            using var db = GetConnection();
            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT * FROM unbilledcharges
                  WHERE custid = @custid AND tenant_code = @tenant_code
                  AND (billedstatus = false OR billedstatus IS NULL)
                  ORDER BY chargedate",
                new { custid, tenant_code });
            return rows.ToList();
        }

        // ── Mark charges billed (called inside HmsBillingClass.CreateBill's own tx) ──
        public async Task MarkAsBilled(
            IDbConnection db, IDbTransaction tx,
            List<string> unbilledIds, string billno, string billid, string tenant_code)
        {
            if (unbilledIds == null || !unbilledIds.Any()) return;

            await db.ExecuteAsync(
                @"UPDATE unbilledcharges
                  SET billedstatus   = true,
                      billno         = @billno,
                      billid         = @billid,
                      billeddate     = @now,
                      billedquantity = quantity,
                      billedamount   = amount
                  WHERE unbilledid = ANY(@ids) AND tenant_code = @tenant_code",
                new { billno, billid, now = DateTime.UtcNow, ids = unbilledIds.ToArray(), tenant_code },
                tx);
        }
        public async Task AddInvestigationChargeRow(
    IDbConnection db, string op_id, decimal custid, string tenant_code,
    string entryId, int? testCode, decimal quantity, decimal? rate, decimal? amount)
        {
            var row = new UnbilledChargeRow
            {
                unbilledid = Guid.NewGuid().ToString(),
                entrytype = "INVESTIGATION",
                entryid = entryId,
                chargedate = DateTime.UtcNow,
                custid = custid,
                opvisitid = op_id,
                tcode = testCode,
                quantity = (double?)quantity,
                rate = (double?)rate,
                amount = (double?)amount,
                discount = 0,
                charityamount = 0,
                billedstatus = false,
                tenant_code = tenant_code
            };
            await db.InsertAsync(row);
        }
    }
}