using Dapper.Contrib.Extensions;

[Table("service_type_master")]
public class ServiceTypeModel
{
    [Key]   // auto-generated int PK — Contrib fills this in on InsertAsync
    public int service_id { get; set; }
    public string tenant_code { get; set; } = string.Empty;
    public string service_name { get; set; } = string.Empty;
    public string? token_prefix { get; set; }
    public string scope { get; set; } = "TENANT";
    public bool deleted { get; set; }
    public DateTime created_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    public DateTime updated_at { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
