using Amazon.S3;
using Dapper;
using medico_backend.Class;
using medico_backend.Controller;
using medico_backend.Controllers;
using medico_backend.InventoryClass;
using medico_backend.InventoryController;
using medico_backend.Services;
using Medico_Backend.Class;
using Medico_Backend.Controllers;
using Medico_Backend.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Minio;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

SqlMapper.AddTypeHandler(typeof(TimeOnly), new TimeOnlyTypeHandler());
SqlMapper.AddTypeHandler(typeof(TimeOnly?), new NullableTimeOnlyTypeHandler());

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

// 2. Multipart form parsing limit
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Add services to the container.

builder.Services.AddControllers();

// Fix Npgsql UTC timestamp handling
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<CustomerMasterClass>();
builder.Services.AddScoped<CustomerMasterController>();
builder.Services.AddScoped<DoctorMasterClass>();
builder.Services.AddScoped<DoctorMasterController>();
builder.Services.AddScoped<DoctorAppointmentSlotClass>();
builder.Services.AddScoped<DoctorAppointmentSlotController>();
builder.Services.AddScoped<FilesController>();
builder.Services.AddScoped<S3ImageService>();
builder.Services.AddSingleton<S3PrescriptionService>();
builder.Services.AddScoped<PrescriptionClass>();
builder.Services.AddScoped<PrescriptionController>();
builder.Services.AddScoped<ItemMasterClass>();
builder.Services.AddScoped<ItemMasterController>();
builder.Services.AddScoped<PrefixMasterClass>();
builder.Services.AddScoped<PrefixMasterController>();
builder.Services.AddScoped<AppointmentBookingClass>();
builder.Services.AddScoped<AppointmentBookingController>();
builder.Services.AddScoped<DoctorAppointmentSlotTypeController>();
builder.Services.AddScoped<DoctorAppointmentSlotTypeClass>();
builder.Services.AddScoped<TenantDirectUrlClass>();
builder.Services.AddScoped<TenantDirectUrlController>();
builder.Services.AddScoped<OpRegistrationClass>();
builder.Services.AddScoped<OpRegistrationController>();
builder.Services.AddScoped<PatientEMRClass>();
builder.Services.AddScoped<PatientEMRController>();
builder.Services.AddScoped<NewOPCaseSheetClass>();
builder.Services.AddScoped<CaseSheetController>();
builder.Services.AddScoped<HmsBillingClass>();
builder.Services.AddScoped<HmsBillingController>();
builder.Services.AddScoped<HmsDueCollectionClass>();
builder.Services.AddScoped<HmsDueCollectionController>();
builder.Services.AddScoped<CounterMasterClass>();
builder.Services.AddScoped<CounterMasterController>();
builder.Services.AddScoped<CounterTimingClass>();
builder.Services.AddScoped<CounterTimingController>();
builder.Services.AddScoped<DoctorTypeMasterClass>();
builder.Services.AddScoped<DoctorTypeMasterController>();
builder.Services.AddScoped<DoctorSpecialtyMasterClass>();
builder.Services.AddScoped<DoctorSpecialtyMasterController>();
builder.Services.AddScoped<ReimbursementCompanyMasterClass>();
builder.Services.AddScoped<ReimbursementCompanyMasterController>();
builder.Services.AddScoped<UomMasterClass>();
builder.Services.AddScoped<UomMasterController>();
builder.Services.AddScoped<ReportMethodClass>();
builder.Services.AddScoped<ReportMethodController>();
builder.Services.AddScoped<GroupMasterClass>();
builder.Services.AddScoped<GroupMasterController>();
builder.Services.AddScoped<SampleMasterClass>();
builder.Services.AddScoped<SampleMasterController>();
builder.Services.AddScoped<TestTypeMasterClass>();
builder.Services.AddScoped<TestTypeMasterController>();
builder.Services.AddScoped<CountryMasterClass>();
builder.Services.AddScoped<CountryMasterController>();
builder.Services.AddScoped<StateMasterClass>();
builder.Services.AddScoped<StateMasterController>();
builder.Services.AddScoped<CityMasterClass>();
builder.Services.AddScoped<CityMasterController>();
builder.Services.AddScoped<AreaMasterClass>();
builder.Services.AddScoped<AreaMasterController>();
builder.Services.AddScoped<FeeTypeMasterClass>();
builder.Services.AddScoped<FeeTypeMasterController>();
builder.Services.AddScoped<TestFeeMasterClass>();
builder.Services.AddScoped<TestFeeMasterController>();
builder.Services.AddScoped<TestMasterClass>();
builder.Services.AddScoped<TestMasterController>();
builder.Services.AddScoped<TenantReportMethodClass>();
builder.Services.AddScoped<TenantReportMethodController>();
builder.Services.AddScoped<MachineMasterClass>();
builder.Services.AddScoped<MachineMasterController>();
builder.Services.AddScoped<TestClass>();
builder.Services.AddScoped<TestController>();
builder.Services.AddScoped<UnbilledChargesClass>();
builder.Services.AddScoped<UnbilledChargesController>();
builder.Services.AddScoped<ProductListClass>();
builder.Services.AddScoped<ProductListController>();
builder.Services.AddScoped<WaAppointmentSessionClass>();
builder.Services.AddScoped<WaAppointmentSessionController>();
builder.Services.AddScoped<DoctorProfileClass>();
builder.Services.AddScoped<DoctorProfileController>();
builder.Services.AddScoped<WaAppointmentSessionClass>();
builder.Services.AddScoped<WaAppointmentSessionController>();
builder.Services.AddScoped<AppointmentBotClass>();
builder.Services.AddScoped<AppointmentBotController>();
builder.Services.AddScoped<RoomTypeMasterClass>();
builder.Services.AddScoped<RoomTypeMasterController>();
builder.Services.AddScoped<FloorMasterClass>();
builder.Services.AddScoped<FloorMasterController>();
builder.Services.AddScoped<WardMasterClass>();
builder.Services.AddScoped<WardMasterController>();
builder.Services.AddScoped<BedMasterClass>();
builder.Services.AddScoped<BedMasterController>();
builder.Services.AddScoped<BedTransferClass>();
builder.Services.AddScoped<BedTransferController>();
builder.Services.AddScoped<NurseMasterClass>();
builder.Services.AddScoped<NurseMasterController>();
builder.Services.AddScoped<BlockMasterClass>();
builder.Services.AddScoped<BlockMasterController>();
builder.Services.AddScoped<IpRegistrationClass>();
builder.Services.AddScoped<IpRegistrationController>();
builder.Services.AddScoped<TenantClass>();
builder.Services.AddScoped<TenantController>();
builder.Services.AddScoped<ReportClass>();
builder.Services.AddScoped<ReportController>();
builder.Services.AddScoped<BedStatusClass>();
builder.Services.AddScoped<BedStatusController>();
builder.Services.AddScoped<PaymodeMasterClass>();
builder.Services.AddScoped<PaymodeMasterController>();
builder.Services.AddScoped<UserClass>();
builder.Services.AddScoped<UserController>();
builder.Services.AddScoped<AppointmentBotClass>();
builder.Services.AddScoped<AppointmentBotController>();
builder.Services.AddScoped<TestGroupRateClass>();
builder.Services.AddScoped<TestGroupRateController>();
builder.Services.AddScoped<TenantProductSubscriptionClass>();
builder.Services.AddScoped<TenantProductSubscriptionController>();
builder.Services.AddScoped<VitalsClass>();
builder.Services.AddScoped<VitalsController>();
builder.Services.AddScoped<LabResultEntryClass>();
builder.Services.AddScoped<LabResultEntryController>();
builder.Services.AddScoped<ScanResultEntryClass>();
builder.Services.AddScoped<ScanResultEntryController>();
builder.Services.AddScoped<OgQueueClass>();
builder.Services.AddScoped<OgQueueController>();
builder.Services.AddScoped<AppointmentPreBookingClass>();
builder.Services.AddScoped<AppointmentPreBookingController>();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var s3Config = new AmazonS3Config
    {
        ServiceURL = config["S3:ServiceUrl"],   // "https://s3.iscansoft.com"
        ForcePathStyle = true,                  // ← required for MinIO
        UseHttp = false
    };

    return new AmazonS3Client(
        config["S3:AccessKey"],
        config["S3:SecretKey"],
        s3Config
    );
});

// Service For Forget Password (via E-Mail)
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();

// Service Token Generation
builder.Services.AddScoped<TokenService>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // ✅ Map "role" claim from TokenService → ClaimTypes.Role
    // This is needed because TokenService uses new Claim("role", "Tenant")
    // instead of ClaimTypes.Role — without this mapping, [Authorize(Policy)] won't work
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"])),

        // ✅ Tell the SDK which claim is the role claim
        RoleClaimType = "role"
    };
});

var minioSection = builder.Configuration.GetSection("Minio");
bool useSSL = bool.TryParse(minioSection["UseSSL"], out var ssl) && ssl;

builder.Services.AddMinio(cfg =>
    cfg.WithEndpoint(minioSection["Endpoint"])
       .WithCredentials(minioSection["AccessKey"], minioSection["SecretKey"])
       .WithSSL(useSSL)
);
// ✅ TenantOnly Policy — matches "role": "Tenant" from GenerateTenantToken
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantOnly", policy =>
        policy.RequireRole("Tenant"));
});


AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddHttpClient("ReportServer", client =>
{
    var url = builder.Configuration.GetConnectionString("reportserver");
    client.BaseAddress = new Uri(url ?? "https://localhost:7053");
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();