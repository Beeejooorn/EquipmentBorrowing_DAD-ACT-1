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

app.MapGet("/", () => Results.Ok(new
{
    message = "Equipment Borrowing API is running."
}));

app.MapGet("/api/v1/equipment", async (
    string? available,
    EquipmentQueryService equipmentService,
    CancellationToken cancellationToken) =>
{
    if (available is not null && !bool.TryParse(available, out _))
    {
        return Results.BadRequest(new
        {
            code = "invalid_query",
            message = "available must be true or false."
        });
    }

    var result = available is not null && bool.Parse(available)
        ? await equipmentService.GetAvailableEquipmentAsync(cancellationToken)
        : await equipmentService.GetAllEquipmentAsync(cancellationToken);

    if (!result.IsSuccess)
    {
        return Results.Json(
            new { code = "server_error", message = "Could not load equipment." },
            statusCode: 500);
    }

    var equipment = result.Items.Select(item => new
    {
        id = item.Id,
        equipmentCode = item.EquipmentCode,
        name = item.Name,
        description = item.Description,
        isAvailable = item.IsAvailable
    });

    return Results.Ok(equipment);
});

app.Run();