namespace EquipmentBorrowing.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ViewModelBase
{
    private readonly EquipmentViewModel _equipmentViewModel;
    private readonly CheckoutViewModel _checkoutViewModel;
    private readonly BorrowingsViewModel _borrowingsViewModel;
    private readonly StudentsViewModel _studentsViewModel;
    private readonly EquipmentManagementViewModel _equipmentManagementViewModel;

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

    public MainViewModel(
        EquipmentViewModel equipmentViewModel,
        CheckoutViewModel checkoutViewModel,
        BorrowingsViewModel borrowingsViewModel,
        StudentsViewModel studentsViewModel,
        EquipmentManagementViewModel equipmentManagementViewModel)
    {
        _equipmentViewModel = equipmentViewModel;
        _checkoutViewModel = checkoutViewModel;
        _borrowingsViewModel = borrowingsViewModel;
        _studentsViewModel = studentsViewModel;
        _equipmentManagementViewModel = equipmentManagementViewModel;

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
    private void ShowCheckout()
    {
        CurrentViewModel = _checkoutViewModel;
        _ = _checkoutViewModel.LoadAsync();
    }

    [RelayCommand]
    private void ShowBorrowings()
    {
        CurrentViewModel = _borrowingsViewModel;
        _ = _borrowingsViewModel.LoadAsync();
    }

    [RelayCommand]
    private void ShowStudents()
    {
        CurrentViewModel = _studentsViewModel;
        _ = _studentsViewModel.LoadAsync();
    }

    [RelayCommand]
    private void ShowEquipmentManagement()
    {
        CurrentViewModel = _equipmentManagementViewModel;
        _ = _equipmentManagementViewModel.LoadAsync();
    }
}
