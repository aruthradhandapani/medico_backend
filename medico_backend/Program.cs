using Dapper;
using Medico_Backend.Class;
using Medico_Backend.Controllers;

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


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
