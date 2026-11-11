using Microsoft.Extensions.Configuration;

namespace EquipmentBorrowing.Infrastructure.Persistence;

// Reads the PostgreSQL connection string WITHOUT storing it in the project files.
//
// It is looked up in this order (the last one found wins):
//   1. .NET user secrets of this project   (dotnet user-secrets set ...)
//   2. the environment variable  ConnectionStrings__EquipmentBorrowing
//
// See the README section "Configure the connection (no credentials in Git)".
public static class DatabaseSettings
{
    public const string ConnectionStringName = "EquipmentBorrowing";

    public static string GetConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DatabaseSettings).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        string? connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The PostgreSQL connection string is not configured. " +
                "Set 'ConnectionStrings:EquipmentBorrowing' with dotnet user-secrets " +
                "(or the environment variable ConnectionStrings__EquipmentBorrowing). See README.");
        }

        return connectionString;
    }
}
