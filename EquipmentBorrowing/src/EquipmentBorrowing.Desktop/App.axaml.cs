using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

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

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        var equipmentRepository = new InMemoryEquipmentRepository();
        equipmentRepository.Seed(new[]
        {
            new Equipment(1, "Digital Camera", true),
            new Equipment(2, "Tripod", true),
            new Equipment(3, "Projector", true),
        });

        var studentRepository = new InMemoryStudentRepository();
        studentRepository.Seed(new[]
        {
            new Student(1, "Juan Dela Cruz", true),
            new Student(2, "Maria Santos", false),
        });

        var borrowingRepository = new InMemoryBorrowingRepository();

        services.AddSingleton<IEquipmentRepository>(equipmentRepository);
        services.AddSingleton<IStudentRepository>(studentRepository);
        services.AddSingleton<IBorrowingRepository>(borrowingRepository);

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