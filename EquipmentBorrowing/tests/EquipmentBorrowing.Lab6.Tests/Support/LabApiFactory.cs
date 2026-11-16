using EquipmentBorrowing.Application.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Lab6.Tests.Support;

// Starts the REAL API (the same Program.cs, routes and services) inside the test process,
// connected to the disposable PostgreSQL test database.
// Requests go through the real ASP.NET Core pipeline: routing, model binding, status codes.
public class LabApiFactory : WebApplicationFactory<Program>
{
    public LabApiFactory()
    {
        // Program.cs reads the connection string from this environment variable
        // (it has priority over user-secrets), so the API uses the TEST database.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__EquipmentBorrowing",
            LabDatabase.GetConnectionString());
    }

    // The API registers ONE CheckoutFailureSimulator. Tests can turn the controlled fault on
    // through it (used by TC6-E04 and TC6-E07). It is OFF by default.
    public CheckoutFailureSimulator FailureSimulator
    {
        get { return Services.GetRequiredService<CheckoutFailureSimulator>(); }
    }
}
