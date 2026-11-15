using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using EquipmentBorrowing.Application.Models;

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

app.MapGet("/api/v1/students", async (
    string? search,
    StudentQueryService studentService,
    CancellationToken cancellationToken) =>
{
    var result = string.IsNullOrWhiteSpace(search)
        ? await studentService.GetAllStudentsAsync(cancellationToken)
        : await studentService.SearchStudentsAsync(search.Trim(), cancellationToken);

    if (!result.IsSuccess)
    {
        return Results.Json(
            new { code = "server_error", message = "Could not load students." },
            statusCode: 500);
    }

    var students = result.Items.Select(student => new
    {
        id = student.Id,
        studentNumber = student.StudentNumber,
        fullName = student.Name,
        contact = student.ContactInfo,
        isEligible = student.IsAllowedToBorrow
    });

    return Results.Ok(students);
});

app.MapGet("/api/v1/students/{id:int}", async (
    int id,
    StudentQueryService studentService,
    CancellationToken cancellationToken) =>
{
    var result = await studentService.GetStudentByIdAsync(
        id,
        cancellationToken);

    if (result.IsNotFound)
    {
        return Results.NotFound(new
        {
            code = "not_found",
            message = "Student not found."
        });
    }

    if (!result.IsSuccess)
    {
        return Results.Json(
            new { code = "server_error", message = "Could not load student." },
            statusCode: 500);
    }

    var student = result.Items[0];

    return Results.Ok(new
    {
        id = student.Id,
        studentNumber = student.StudentNumber,
        fullName = student.Name,
        contact = student.ContactInfo,
        isEligible = student.IsAllowedToBorrow
    });
});


app.MapPost("/api/v1/students", async (
    CreateStudentRequest request,
    StudentManagementService studentService,
    CancellationToken cancellationToken) =>
{
    OperationResult result = await studentService.RegisterStudentAsync(
        request.StudentNumber,
        request.FullName,
        request.Contact,
        cancellationToken);

    if (result.Kind == ResultKind.Rejected)
    {
        bool duplicate = result.Message.Contains("already registered");
        return Results.Json(
            new
            {
                code = duplicate ? "duplicate_student_number" : "invalid_student",
                message = result.Message
            },
            statusCode: duplicate ? 409 : 400);
    }

    if (result.Kind == ResultKind.UnknownOutcome)
    {
        return Results.Json(
            new
            {
                code = "unknown_outcome",
                message = "The save outcome is unknown. Check the student list before retrying."
            },
            statusCode: 500);
    }

    if (!result.IsSuccess || result.RecordId is null)
    {
        return Results.Json(
            new { code = "server_error", message = "Could not register student." },
            statusCode: 500);
    }

    int id = result.RecordId.Value;

    return Results.Created($"/api/v1/students/{id}", new
    {
        id,
        studentNumber = request.StudentNumber!.Trim(),
        fullName = request.FullName!.Trim(),
        contact = string.IsNullOrWhiteSpace(request.Contact)
            ? null
            : request.Contact.Trim(),
        isEligible = true
    });
});

app.MapPut("/api/v1/students/{id:int}", async (
    int id,
    CreateStudentRequest request,
    StudentManagementService studentService,
    StudentQueryService studentQueryService,
    CancellationToken cancellationToken) =>
{
    OperationResult result = await studentService.UpdateStudentAsync(
        id,
        request.StudentNumber,
        request.FullName,
        request.Contact,
        cancellationToken);

    if (result.Kind == ResultKind.NotFound)
    {
        return Results.NotFound(new
        {
            code = "not_found",
            message = "Student not found."
        });
    }

    if (result.Kind == ResultKind.Rejected)
    {
        bool duplicate = result.Message.Contains("already registered");
        return Results.Json(
            new
            {
                code = duplicate ? "duplicate_student_number" : "invalid_student",
                message = result.Message
            },
            statusCode: duplicate ? 409 : 400);
    }

    if (result.Kind == ResultKind.UnknownOutcome)
    {
        return Results.Json(
            new
            {
                code = "unknown_outcome",
                message = "The save outcome is unknown. Check the student before retrying."
            },
            statusCode: 500);
    }

    if (!result.IsSuccess)
    {
        return Results.Json(
            new { code = "server_error", message = "Could not update student." },
            statusCode: 500);
    }

    var readback = await studentQueryService.GetStudentByIdAsync(
        id,
        cancellationToken);

    if (!readback.IsSuccess)
    {
        return Results.Json(
            new
            {
                code = "readback_failed",
                message = "The update may have saved, but the student could not be reloaded. Check the student before retrying."
            },
            statusCode: 500);
    }

    var student = readback.Items[0];

    return Results.Ok(new
    {
        id = student.Id,
        studentNumber = student.StudentNumber,
        fullName = student.Name,
        contact = student.ContactInfo,
        isEligible = student.IsAllowedToBorrow
    });
});

app.Run();


record CreateStudentRequest(
    string? StudentNumber,
    string? FullName,
    string? Contact);