namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ViewModelBase
{
    private readonly EquipmentViewModel _equipmentViewModel;
    private readonly BorrowingsViewModel _borrowingsViewModel;

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    public MainViewModel(EquipmentViewModel equipmentViewModel, BorrowingsViewModel borrowingsViewModel)
    {
        _equipmentViewModel = equipmentViewModel;
        _borrowingsViewModel = borrowingsViewModel;

        currentViewModel = _equipmentViewModel;
    }

    [RelayCommand]
    private void ShowEquipment()
    {
        CurrentViewModel = _equipmentViewModel;
    }

    [RelayCommand]
    private async Task ShowBorrowings()
    {
        await _borrowingsViewModel.LoadAsync();
        CurrentViewModel = _borrowingsViewModel;
    }
}