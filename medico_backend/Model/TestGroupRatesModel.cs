using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("public.test_group_rates")]
    public class TestGroupRateModel
    {
        [Key]
        public int id { get; set; }

        public int? rmtcode { get; set; }
        public int? roomchargehead { get; set; }
        public int? subtestcode { get; set; }
        public int? testrate { get; set; }
        public int? usercode { get; set; } = 1;
        public DateTime? entereddate { get; set; }
        public string? tenant_code { get; set; }
    }

    // Convenience DTO for saving a whole rate-list in one call
    public class SaveRoomTypeRatesRequest
    {
        public int rmtcode { get; set; }
        public List<TestGroupRateEntry> rates { get; set; } = new();
    }

    public class TestGroupRateEntry
    {
        public int roomchargehead { get; set; }
        public int? subtestcode { get; set; }
        public int testrate { get; set; }
    }
}