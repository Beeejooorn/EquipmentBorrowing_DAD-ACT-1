namespace EquipmentBorrowing.Desktop.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

// Equipment Catalog screen:
// UC-01 View Equipment Catalog, UC-02 Find Available Equipment, UC-03 Borrow Equipment.
public partial class EquipmentViewModel : ViewModelBase
{
    private readonly EquipmentQueryService _equipmentQueryService;
    private readonly StudentQueryService _studentQueryService;
    private readonly BorrowEquipmentService _borrowEquipmentService;

    public ObservableCollection<Equipment> EquipmentList { get; } = new();
    public ObservableCollection<Student> Students { get; } = new();

    public LoadState CatalogState { get; } = new();

    [ObservableProperty]
    private bool showAvailableOnly;

    [ObservableProperty]
    private Equipment? selectedEquipment;

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private DateTimeOffset? expectedReturnDate = DateTimeOffset.Now.AddDays(7);

    public EquipmentViewModel(
        EquipmentQueryService equipmentQueryService,
        StudentQueryService studentQueryService,
        BorrowEquipmentService borrowEquipmentService)
    {
        _equipmentQueryService = equipmentQueryService;
        _studentQueryService = studentQueryService;
        _borrowEquipmentService = borrowEquipmentService;
    }

    // Runs automatically when the "Show available equipment only" box is ticked or unticked.
    partial void OnShowAvailableOnlyChanged(bool value)
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        // Remember what was selected so we can select it again after reloading.
        int? keepEquipmentId = SelectedEquipment?.Id;
        int? keepStudentId = SelectedStudent?.Id;

        CatalogState.StartLoading();

        // UC-02 uses the "available only" query, UC-01 loads everything.
        QueryResult<Equipment> equipmentResult;

        if (ShowAvailableOnly)
        {
            equipmentResult = await _equipmentQueryService.GetAvailableEquipmentAsync();
        }
        else
        {
            equipmentResult = await _equipmentQueryService.GetAllEquipmentAsync();
        }

        EquipmentList.Clear();

        if (!equipmentResult.IsSuccess)
        {
            CatalogState.ShowError(equipmentResult.ErrorMessage ?? "Could not load equipment.");
            return;
        }

        foreach (Equipment item in equipmentResult.Items)
        {
            EquipmentList.Add(item);
        }

        if (ShowAvailableOnly)
        {
            CatalogState.ShowLoaded(EquipmentList.Count, "No equipment is available right now.");
        }
        else
        {
            CatalogState.ShowLoaded(EquipmentList.Count, "The equipment catalog is empty.");
        }

        // Students for the borrow form.
        QueryResult<Student> studentResult = await _studentQueryService.GetAllStudentsAsync();
        Students.Clear();

        foreach (Student student in studentResult.Items)
        {
            Students.Add(student);
        }

        // Select the same student and item again (they are new objects after reloading).
        foreach (Equipment item in EquipmentList)
        {
            if (item.Id == keepEquipmentId)
            {
                SelectedEquipment = item;
            }
        }

        foreach (Student student in Students)
        {
            if (student.Id == keepStudentId)
            {
                SelectedStudent = student;
            }
        }
    }

    [RelayCommand]
    private async Task BorrowAsync()
    {
        // Presentation checks only: is something selected?
        // The real business rules are checked again by the service.
        if (SelectedStudent is null)
        {
            ShowRejected("Please select a student.");
            return;
        }

        if (SelectedEquipment is null)
        {
            ShowRejected("Please select equipment.");
            return;
        }

        DateOnly? returnDate = null;

        if (ExpectedReturnDate != null)
        {
            returnDate = DateOnly.FromDateTime(ExpectedReturnDate.Value.Date);
        }

        OperationResult result = await _borrowEquipmentService.BorrowAsync(
            SelectedStudent.Id,
            SelectedEquipment.Id,
            returnDate);

        ShowResult(result);

        // Refresh so the screen shows the current availability (also after a rejection or conflict).
        await LoadAsync();
    }
}
