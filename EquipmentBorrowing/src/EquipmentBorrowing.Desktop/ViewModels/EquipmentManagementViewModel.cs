namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

// Manage Equipment screen: UC-15 Register Equipment, UC-16 Update Equipment Details.
public partial class EquipmentManagementViewModel : ViewModelBase
{
    private readonly EquipmentQueryService _equipmentQueryService;
    private readonly EquipmentManagementService _equipmentManagementService;

    public ObservableCollection<Equipment> EquipmentList { get; } = new();

    public LoadState ListState { get; } = new();

    // ---- Finding an item ----

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private Equipment? selectedEquipment;

    // ---- The form on the right side (descriptive fields only) ----

    [ObservableProperty]
    private string? formEquipmentCode;

    [ObservableProperty]
    private string? formName;

    [ObservableProperty]
    private string? formDescription;

    // ---- Information about the selected item (read-only) ----

    [ObservableProperty]
    private bool hasSelectedEquipment;

    [ObservableProperty]
    private bool isSelectedEquipmentAvailable;

    [ObservableProperty]
    private string selectedEquipmentTitle = "No equipment selected";

    public EquipmentManagementViewModel(
        EquipmentQueryService equipmentQueryService,
        EquipmentManagementService equipmentManagementService)
    {
        _equipmentQueryService = equipmentQueryService;
        _equipmentManagementService = equipmentManagementService;
    }

    // Runs automatically when the user selects another item in the list.
    partial void OnSelectedEquipmentChanged(Equipment? value)
    {
        if (value is null)
        {
            // Keep what is typed in the form (the list may just be reloading).
            HasSelectedEquipment = false;
            IsSelectedEquipmentAvailable = false;
            SelectedEquipmentTitle = "No equipment selected";
            return;
        }

        FormEquipmentCode = value.EquipmentCode;
        FormName = value.Name;
        FormDescription = value.Description;

        HasSelectedEquipment = true;
        IsSelectedEquipmentAvailable = value.IsAvailable;
        SelectedEquipmentTitle = $"{value.EquipmentCode} - {value.Name}";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await LoadEquipmentAsync(SelectedEquipment?.Id);
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        SearchText = "";
        await LoadEquipmentAsync(SelectedEquipment?.Id);
    }

    // Loads the list (using the search box) and selects the item with this Id again.
    private async Task LoadEquipmentAsync(int? equipmentIdToSelect)
    {
        ListState.StartLoading();

        QueryResult<Equipment> result = await _equipmentQueryService.SearchEquipmentAsync(SearchText);

        EquipmentList.Clear();

        if (!result.IsSuccess)
        {
            ListState.ShowError(result.ErrorMessage ?? "Could not load equipment.");
            return;
        }

        foreach (Equipment item in result.Items)
        {
            EquipmentList.Add(item);
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ListState.ShowLoaded(EquipmentList.Count, "No equipment is registered yet.");
        }
        else
        {
            ListState.ShowLoaded(EquipmentList.Count, $"No equipment matches \"{SearchText.Trim()}\".");
        }

        if (equipmentIdToSelect != null)
        {
            foreach (Equipment item in EquipmentList)
            {
                if (item.Id == equipmentIdToSelect)
                {
                    SelectedEquipment = item;
                }
            }
        }
    }

    // UC-15
    [RelayCommand]
    private async Task RegisterAsync()
    {
        OperationResult result = await _equipmentManagementService.RegisterEquipmentAsync(
            FormEquipmentCode, FormName, FormDescription);

        ShowResult(result);

        if (result.IsSuccess)
        {
            // Show the saved record: reload everything and select the new item.
            SearchText = "";
            await LoadEquipmentAsync(result.RecordId);
        }
        else if (result.Kind != ResultKind.Rejected)
        {
            await LoadEquipmentAsync(null);
        }
        // When the input was rejected we keep the form, so the user can correct it.
    }

    // UC-16
    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (SelectedEquipment is null)
        {
            ShowRejected("Select the equipment you want to edit first.");
            return;
        }

        int equipmentId = SelectedEquipment.Id;

        OperationResult result = await _equipmentManagementService.UpdateEquipmentAsync(
            equipmentId, FormEquipmentCode, FormName, FormDescription);

        ShowResult(result);

        if (result.IsSuccess)
        {
            await LoadEquipmentAsync(equipmentId);
        }
        else if (result.Kind == ResultKind.NotFound)
        {
            await LoadEquipmentAsync(null);
        }
        else if (result.Kind != ResultKind.Rejected)
        {
            await LoadEquipmentAsync(equipmentId);
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedEquipment = null;
        FormEquipmentCode = "";
        FormName = "";
        FormDescription = "";
        ClearStatus();
    }
}
