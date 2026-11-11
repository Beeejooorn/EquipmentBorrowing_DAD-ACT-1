namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;

// Search Borrowings screen: UC-09 Search, Filter and Sort Borrowings.
// It also lets the user try to return the selected record (UC-05),
// which shows that a returned record cannot be returned again.
public partial class BorrowingSearchViewModel : ViewModelBase
{
    private readonly BorrowingQueryService _borrowingQueryService;
    private readonly ReturnEquipmentService _returnEquipmentService;

    public ObservableCollection<BorrowingRecord> Results { get; } = new();

    public LoadState ResultState { get; } = new();

    // Choices for the three drop-downs (plain text, so the View needs no converters).
    public List<string> StatusOptions { get; } = new List<string> { "All", "Active", "Returned" };
    public List<string> SortFieldOptions { get; } = new List<string> { "Checkout date", "Expected return date" };
    public List<string> SortDirectionOptions { get; } = new List<string> { "Ascending (oldest first)", "Descending (newest first)" };

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private string selectedStatus = "All";

    [ObservableProperty]
    private string selectedSortField = "Checkout date";

    [ObservableProperty]
    private string selectedSortDirection = "Ascending (oldest first)";

    [ObservableProperty]
    private string resultCountText = "";

    [ObservableProperty]
    private BorrowingRecord? selectedRecord;

    public BorrowingSearchViewModel(
        BorrowingQueryService borrowingQueryService,
        ReturnEquipmentService returnEquipmentService)
    {
        _borrowingQueryService = borrowingQueryService;
        _returnEquipmentService = returnEquipmentService;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        BorrowingSearchCriteria criteria = BuildCriteria();

        ResultState.StartLoading();
        ResultCountText = "";

        QueryResult<BorrowingRecord> result = await _borrowingQueryService.SearchBorrowingsAsync(criteria);

        Results.Clear();

        if (!result.IsSuccess)
        {
            ResultState.ShowError(result.ErrorMessage ?? "Could not search borrowings.");
            return;
        }

        foreach (BorrowingRecord record in result.Items)
        {
            Results.Add(record);
        }

        ResultState.ShowLoaded(Results.Count, "No borrowings match these criteria. Try other words or click Clear.");
        ResultCountText = $"{Results.Count} borrowing(s) found.";
    }

    // Turns the choices on the screen into search criteria for the service.
    private BorrowingSearchCriteria BuildCriteria()
    {
        BorrowingSearchCriteria criteria = new BorrowingSearchCriteria();
        criteria.SearchText = SearchText;

        if (SelectedStatus == "Active")
        {
            criteria.Status = BorrowingStatusFilter.Active;
        }
        else if (SelectedStatus == "Returned")
        {
            criteria.Status = BorrowingStatusFilter.Returned;
        }
        else
        {
            criteria.Status = BorrowingStatusFilter.All;
        }

        if (SelectedSortField == "Expected return date")
        {
            criteria.SortBy = BorrowingSortField.ExpectedReturnDate;
        }
        else
        {
            criteria.SortBy = BorrowingSortField.CheckoutDate;
        }

        criteria.SortDescending = SelectedSortDirection.StartsWith("Descending");

        return criteria;
    }

    // Puts every choice back to its default and shows all borrowings again.
    [RelayCommand]
    private async Task ClearAsync()
    {
        SearchText = "";
        SelectedStatus = "All";
        SelectedSortField = "Checkout date";
        SelectedSortDirection = "Ascending (oldest first)";
        ClearStatus();
        await SearchAsync();
    }

    // UC-05 from this screen. A record that is already returned is rejected by the service.
    [RelayCommand]
    private async Task ReturnSelectedAsync()
    {
        if (SelectedRecord is null)
        {
            ShowRejected("Select a borrowing in the results first.");
            return;
        }

        OperationResult result = await _returnEquipmentService.ReturnAsync(SelectedRecord.BorrowingId);

        ShowResult(result);

        // Refresh the results so the status shown is the one saved in the database.
        await SearchAsync();
    }
}
