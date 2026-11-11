using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1. Read the connection string (from user secrets / environment variable).
            string? startupError = null;
            string connectionString = "";

            try
            {
                connectionString = DatabaseSettings.GetConnectionString();
            }
            catch (InvalidOperationException ex)
            {
                startupError = ex.Message;
            }

            // 2. Register everything in one place (composition root).
            var services = new ServiceCollection();
            ConfigureServices(services, connectionString);
            var provider = services.BuildServiceProvider();

            // 3. Apply migrations and seed sample data (only if the database is empty).
            //    This happens here, in the composition root, never in a View or ViewModel.
            if (startupError == null)
            {
                try
                {
                    provider.GetRequiredService<DatabaseInitializer>().Initialize();
                }
                catch (Exception ex)
                {
                    startupError = "Could not prepare the PostgreSQL database. " +
                                   "Check that PostgreSQL is running and that the connection settings are correct. " +
                                   "Details: " + GetInnermostMessage(ex);
                }
            }

            // 4. Show the window. If the database is not ready, the window still opens
            //    and shows the problem instead of crashing.
            MainViewModel mainViewModel = provider.GetRequiredService<MainViewModel>();
            mainViewModel.StartupErrorMessage = startupError;

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services, string connectionString)
    {
        // Infrastructure: DbContext factory, unit of work factory, error classifier, initializer.
        InfrastructureServices.AddInfrastructure(services, connectionString);

        // Application services are stateless, so a new one each time is fine.
        services.AddTransient<BorrowEquipmentService>();
        services.AddTransient<ReturnEquipmentService>();
        services.AddTransient<EquipmentQueryService>();
        services.AddTransient<StudentQueryService>();
        services.AddTransient<BorrowingQueryService>();
        services.AddTransient<StudentManagementService>();

        // ViewModels: each one is created once by MainViewModel.
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<MainViewModel>();
    }

    // Shows the most specific error text (it never contains the password).
    private static string GetInnermostMessage(Exception ex)
    {
        Exception current = ex;

        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
