namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;

public partial class BorrowingsViewModel : ViewModelBase
{
    private readonly BorrowingQueryService _borrowingQueryService;
    private readonly EquipmentQueryService _equipmentQueryService;
    private readonly StudentQueryService _studentQueryService;
    private readonly ReturnEquipmentService _returnEquipmentService;

    public ObservableCollection<BorrowingDisplayItem> ActiveBorrowings { get; } = new();

    [ObservableProperty]
    private BorrowingDisplayItem? selectedBorrowing;

    [ObservableProperty]
    private string? statusMessage;

    public BorrowingsViewModel(
        BorrowingQueryService borrowingQueryService,
        EquipmentQueryService equipmentQueryService,
        StudentQueryService studentQueryService,
        ReturnEquipmentService returnEquipmentService)
    {
        _borrowingQueryService = borrowingQueryService;
        _equipmentQueryService = equipmentQueryService;
        _studentQueryService = studentQueryService;
        _returnEquipmentService = returnEquipmentService;

        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var borrowings = await _borrowingQueryService.GetAllActiveBorrowingsAsync();
        var equipment = await _equipmentQueryService.GetAllEquipmentAsync();
        var students = await _studentQueryService.GetAllStudentsAsync();

        ActiveBorrowings.Clear();

        foreach (var borrowing in borrowings)
        {
            var equipmentName = equipment.FirstOrDefault(e => e.Id == borrowing.EquipmentId)?.Name ?? "Unknown";
            var studentName = students.FirstOrDefault(s => s.Id == borrowing.StudentId)?.Name ?? "Unknown";

            ActiveBorrowings.Add(new BorrowingDisplayItem(borrowing, studentName, equipmentName));
        }
    }

    [RelayCommand]
    private async Task ReturnAsync()
    {
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Please select a borrowing to return.";
            return;
        }

        var result = await _returnEquipmentService.ReturnAsync(SelectedBorrowing.Borrowing);

        StatusMessage = result.IsSuccess
            ? "Equipment returned successfully."
            : result.ErrorMessage;

        await LoadAsync();
    }
}