using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using EquipmentBorrowing.Infrastructure.Persistence;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

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
            var services = new ServiceCollection();
            ConfigureServices(services);
            var provider = services.BuildServiceProvider();

            // Create the database / apply pending migrations (seeds on first run only).
            using (var context = provider.GetRequiredService<EquipmentBorrowingDbContext>())
            {
                context.Database.Migrate();
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EquipmentBorrowing");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "equipmentborrowing.db");

        services.AddDbContext<EquipmentBorrowingDbContext>(
            options => options
                .UseSqlite($"Data Source={dbPath}")
                .LogTo(message => System.Diagnostics.Debug.WriteLine(message),
                       Microsoft.Extensions.Logging.LogLevel.Information),
            ServiceLifetime.Transient);

        services.AddTransient<IEquipmentRepository, EfEquipmentRepository>();
        services.AddTransient<IStudentRepository, EfStudentRepository>();
        services.AddTransient<IBorrowingRepository, EfBorrowingRepository>();

        services.AddTransient<BorrowEquipmentService>();
        services.AddTransient<ReturnEquipmentService>();
        services.AddTransient<EquipmentQueryService>();
        services.AddTransient<StudentQueryService>();
        services.AddTransient<BorrowingQueryService>();

        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<MainViewModel>();
    }
}