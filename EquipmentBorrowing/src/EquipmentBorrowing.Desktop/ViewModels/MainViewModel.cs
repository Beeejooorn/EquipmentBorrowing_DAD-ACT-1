namespace EquipmentBorrowing.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ViewModelBase
{
    private readonly EquipmentViewModel _equipmentViewModel;
    private readonly BorrowingsViewModel _borrowingsViewModel;

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    // Set by App.axaml.cs when the database could not be prepared at startup.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartupError))]
    private string? startupErrorMessage;

    public bool HasStartupError
    {
        get { return !string.IsNullOrEmpty(StartupErrorMessage); }
    }

    public MainViewModel(EquipmentViewModel equipmentViewModel, BorrowingsViewModel borrowingsViewModel)
    {
        _equipmentViewModel = equipmentViewModel;
        _borrowingsViewModel = borrowingsViewModel;

        currentViewModel = _equipmentViewModel;
        _ = _equipmentViewModel.LoadAsync();
    }

    // Each screen reloads its data when it is opened, so it always shows the current database state.
    // The loading runs in the background (not awaited) so the window stays responsive.

    [RelayCommand]
    private void ShowEquipment()
    {
        CurrentViewModel = _equipmentViewModel;
        _ = _equipmentViewModel.LoadAsync();
    }

    [RelayCommand]
    private void ShowBorrowings()
    {
        CurrentViewModel = _borrowingsViewModel;
        _ = _borrowingsViewModel.LoadAsync();
    }
}
