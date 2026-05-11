using Amazon.S3;
using Dapper;
using medico_backend.Class;
using medico_backend.Controllers;
using medico_backend.InventoryClass;
using medico_backend.InventoryController;
using medico_backend.Services;
using Medico_Backend.Class;
using Medico_Backend.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Minio;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<TestMasterClass>();
builder.Services.AddScoped<TestMasterController>();
builder.Services.AddScoped<PrefixMasterClass>();
builder.Services.AddScoped<PrefixMasterController>();
builder.Services.AddScoped<UserMasterClass>();
builder.Services.AddScoped<UserMasterController>();




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

builder.Services.AddControllers();
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