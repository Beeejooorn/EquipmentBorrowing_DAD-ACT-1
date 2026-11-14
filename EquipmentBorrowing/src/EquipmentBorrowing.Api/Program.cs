using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

string connectionString = DatabaseSettings.GetConnectionString();
InfrastructureServices.AddInfrastructure(builder.Services, connectionString);

builder.Services.AddSingleton<CheckoutFailureSimulator>();
builder.Services.AddTransient<BorrowEquipmentService>();
builder.Services.AddTransient<ReturnEquipmentService>();
builder.Services.AddTransient<EquipmentQueryService>();
builder.Services.AddTransient<StudentQueryService>();
builder.Services.AddTransient<BorrowingQueryService>();
builder.Services.AddTransient<StudentManagementService>();
builder.Services.AddTransient<EquipmentManagementService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { message = "Equipment Borrowing API is running." }));

app.Run();