namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;

// Active Borrowings screen: UC-04 View Active Borrowings, UC-05 Return Equipment.
public partial class BorrowingsViewModel : ViewModelBase
{
    private readonly BorrowingQueryService _borrowingQueryService;
    private readonly ReturnEquipmentService _returnEquipmentService;

    public ObservableCollection<BorrowingRecord> ActiveBorrowings { get; } = new();

    public LoadState ListState { get; } = new();

    [ObservableProperty]
    private BorrowingRecord? selectedBorrowing;

    // true while the "Are you sure?" panel is visible.
    [ObservableProperty]
    private bool isConfirmingReturn;

    [ObservableProperty]
    private string? confirmText;

    public BorrowingsViewModel(
        BorrowingQueryService borrowingQueryService,
        ReturnEquipmentService returnEquipmentService)
    {
        _borrowingQueryService = borrowingQueryService;
        _returnEquipmentService = returnEquipmentService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        ListState.StartLoading();
        IsConfirmingReturn = false;

        QueryResult<BorrowingRecord> result = await _borrowingQueryService.GetActiveBorrowingsAsync();

        ActiveBorrowings.Clear();

        if (!result.IsSuccess)
        {
            ListState.ShowError(result.ErrorMessage ?? "Could not load active borrowings.");
            return;
        }

        foreach (BorrowingRecord record in result.Items)
        {
            ActiveBorrowings.Add(record);
        }

        ListState.ShowLoaded(ActiveBorrowings.Count, "There are no active borrowings.");
    }

    // Step 1 of returning: ask for confirmation.
    [RelayCommand]
    private void AskReturn()
    {
        if (SelectedBorrowing is null)
        {
            ShowRejected("Please select a borrowing to return.");
            return;
        }

        ConfirmText = $"Return {SelectedBorrowing.EquipmentName} ({SelectedBorrowing.EquipmentCode}) " +
                      $"borrowed by {SelectedBorrowing.StudentName}?";
        IsConfirmingReturn = true;
    }

    [RelayCommand]
    private void CancelReturn()
    {
        IsConfirmingReturn = false;
    }

    // Step 2 of returning: the user confirmed.
    [RelayCommand]
    private async Task ConfirmReturnAsync()
    {
        IsConfirmingReturn = false;

        if (SelectedBorrowing is null)
        {
            ShowRejected("Please select a borrowing to return.");
            return;
        }

        // Only the Id is sent. The service reads the current record again from the database.
        OperationResult result = await _returnEquipmentService.ReturnAsync(SelectedBorrowing.BorrowingId);

        ShowResult(result);

        // Always refresh: after success the record leaves this list,
        // after a stale/missing/unknown result the user sees the current data.
        await LoadAsync();
    }
}
