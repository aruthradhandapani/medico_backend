using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Npgsql;

namespace medico_backend.Class
{
    public class PrescriptionClass
    {
        private readonly string _connectionString;
        private readonly ILogger<PrescriptionClass> _logger;

        public PrescriptionClass(IConfiguration config, ILogger<PrescriptionClass> logger)
        {
            _connectionString = config.GetConnectionString("jprohms_conn")
                                ?? throw new InvalidOperationException("PrescriptionDb connection string is missing.");
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection() => new(_connectionString);

        public async Task<int> InsertAsync(PrescriptionModel model)
        {
            using var conn = CreateConnection();
            var newId = await conn.InsertAsync(model);
            return (int)newId;
        }

        public async Task<IEnumerable<PrescriptionModel>> GetAllAsync()
        {
            using var conn = CreateConnection();
            return await conn.GetAllAsync<PrescriptionModel>();
        }

        // In your repository / data class
        public async Task<PrescriptionModel?> GetByFilePathAsync(string filePath)
        {
            using var conn = CreateConnection();
            const string sql = "SELECT * FROM prescriptions WHERE filepath = @FilePath LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<PrescriptionModel>(sql, new { FilePath = filePath });
        }

        public async Task<PrescriptionModel?> GetByIdAsync(int id)
        {
            using var conn = CreateConnection();
            return await conn.GetAsync<PrescriptionModel>(id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = CreateConnection();
            var record = await conn.GetAsync<PrescriptionModel>(id);
            if (record == null) return false;
            return await conn.DeleteAsync(record);
        }
    }
}