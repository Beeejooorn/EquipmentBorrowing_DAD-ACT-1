using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Desktop.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

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
            MainViewModel mainViewModel = provider.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        string apiUrl = Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_API_URL") ?? "http://localhost:5085";
        services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(10) });
        services.AddSingleton<IDesktopApi, DesktopApi>();

        // ViewModels: each one is created once by MainViewModel.
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<CheckoutViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<BorrowingSearchViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<EquipmentManagementViewModel>();
        services.AddTransient<MainViewModel>();
    }

}
