namespace EquipmentBorrowing.Desktop.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;
using EquipmentBorrowing.Domain;

// Multi-Item Checkout screen: UC-06.
public partial class CheckoutViewModel : ViewModelBase
{
    private readonly StudentQueryService _studentQueryService;
    private readonly EquipmentQueryService _equipmentQueryService;
    private readonly BorrowingQueryService _borrowingQueryService;
    private readonly BorrowEquipmentService _borrowEquipmentService;
    private readonly CheckoutFailureSimulator _failureSimulator;

    // Used to ignore an old "active count" answer when the user already picked another student.
    private int _studentInfoRequestNumber = 0;

    public ObservableCollection<Student> Students { get; } = new();

    // All items (also borrowed ones), so the rule "item is not available" can be demonstrated.
    public ObservableCollection<Equipment> AllEquipment { get; } = new();

    // The items the user put in the checkout list. Duplicates are possible here on purpose.
    public ObservableCollection<CheckoutLine> CheckoutLines { get; } = new();

    public LoadState FormState { get; } = new();

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private string? studentInfoText;

    [ObservableProperty]
    private Equipment? itemToAdd;

    [ObservableProperty]
    private CheckoutLine? selectedLine;

    [ObservableProperty]
    private DateTimeOffset? expectedReturnDate = DateTimeOffset.Now.AddDays(7);

    [ObservableProperty]
    private string checkoutSummary = "No items in the checkout list yet.";

    // ---- Demo / test options (test seam, see CheckoutFailureSimulator) ----

    [ObservableProperty]
    private bool simulateFailureAfterFirstWrite;

    [ObservableProperty]
    private bool simulateLostCommit;

    public CheckoutViewModel(
        StudentQueryService studentQueryService,
        EquipmentQueryService equipmentQueryService,
        BorrowingQueryService borrowingQueryService,
        BorrowEquipmentService borrowEquipmentService,
        CheckoutFailureSimulator failureSimulator)
    {
        _studentQueryService = studentQueryService;
        _equipmentQueryService = equipmentQueryService;
        _borrowingQueryService = borrowingQueryService;
        _borrowEquipmentService = borrowEquipmentService;
        _failureSimulator = failureSimulator;
    }

    // ---- Loading ----

    [RelayCommand]
    public async Task LoadAsync()
    {
        int? keepStudentId = SelectedStudent?.Id;

        FormState.StartLoading();

        QueryResult<Student> studentResult = await _studentQueryService.GetAllStudentsAsync();
        QueryResult<Equipment> equipmentResult = await _equipmentQueryService.GetAllEquipmentAsync();

        Students.Clear();
        AllEquipment.Clear();

        if (!studentResult.IsSuccess)
        {
            FormState.ShowError(studentResult.ErrorMessage ?? "Could not load students.");
            return;
        }

        if (!equipmentResult.IsSuccess)
        {
            FormState.ShowError(equipmentResult.ErrorMessage ?? "Could not load equipment.");
            return;
        }

        foreach (Student student in studentResult.Items)
        {
            Students.Add(student);
        }

        foreach (Equipment item in equipmentResult.Items)
        {
            AllEquipment.Add(item);
        }

        FormState.ShowLoaded(AllEquipment.Count, "There is no equipment registered yet.");

        // Select the same student again.
        foreach (Student student in Students)
        {
            if (student.Id == keepStudentId)
            {
                SelectedStudent = student;
            }
        }

        // The lines still point to the old item objects: swap them for the fresh ones
        // so the list shows the current availability.
        RefreshCheckoutLines();
    }

    private void RefreshCheckoutLines()
    {
        List<CheckoutLine> oldLines = new List<CheckoutLine>(CheckoutLines);
        CheckoutLines.Clear();

        foreach (CheckoutLine line in oldLines)
        {
            Equipment item = line.Item;

            foreach (Equipment freshItem in AllEquipment)
            {
                if (freshItem.Id == line.Item.Id)
                {
                    item = freshItem;
                }
            }

            CheckoutLines.Add(new CheckoutLine(line.LineNumber, item));
        }

        UpdateSummary();
    }

    // Runs automatically when another student is selected: show how many items they already have.
    partial void OnSelectedStudentChanged(Student? value)
    {
        _ = UpdateStudentInfoAsync(value);
    }

    private async Task UpdateStudentInfoAsync(Student? student)
    {
        _studentInfoRequestNumber++;
        int myRequestNumber = _studentInfoRequestNumber;

        if (student is null)
        {
            StudentInfoText = null;
            return;
        }

        QueryResult<BorrowingRecord> result = await _borrowingQueryService.GetActiveBorrowingsAsync();

        // The user picked another student while we were waiting: ignore this old answer.
        if (myRequestNumber != _studentInfoRequestNumber)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            StudentInfoText = "Could not load the student's active borrowings.";
            return;
        }

        int activeCount = 0;

        foreach (BorrowingRecord record in result.Items)
        {
            if (record.StudentId == student.Id)
            {
                activeCount++;
            }
        }

        string eligibility = student.IsAllowedToBorrow ? "eligible" : "RESTRICTED";
        StudentInfoText = $"{student.Name} is {eligibility} and has {activeCount} of 3 active borrowings.";
    }

    // ---- Building the checkout list ----

    [RelayCommand]
    private void AddItem()
    {
        if (ItemToAdd is null)
        {
            ShowRejected("Choose an item to add first.");
            return;
        }

        int nextLineNumber = CheckoutLines.Count + 1;
        CheckoutLines.Add(new CheckoutLine(nextLineNumber, ItemToAdd));
        UpdateSummary();
        ClearStatus();
    }

    [RelayCommand]
    private void RemoveSelectedLine()
    {
        if (SelectedLine is null)
        {
            ShowRejected("Select a line in the checkout list to remove.");
            return;
        }

        CheckoutLines.Remove(SelectedLine);
        RenumberLines();
        UpdateSummary();
    }

    [RelayCommand]
    private void ClearLines()
    {
        CheckoutLines.Clear();
        UpdateSummary();
        ClearStatus();
    }

    private void RenumberLines()
    {
        List<CheckoutLine> oldLines = new List<CheckoutLine>(CheckoutLines);
        CheckoutLines.Clear();

        int number = 1;

        foreach (CheckoutLine line in oldLines)
        {
            CheckoutLines.Add(new CheckoutLine(number, line.Item));
            number++;
        }
    }

    private void UpdateSummary()
    {
        if (CheckoutLines.Count == 0)
        {
            CheckoutSummary = "No items in the checkout list yet.";
        }
        else
        {
            CheckoutSummary = $"{CheckoutLines.Count} item(s) in the checkout list.";
        }
    }

    // ---- Checking out ----

    [RelayCommand]
    private async Task CheckOutAsync()
    {
        if (SelectedStudent is null)
        {
            ShowRejected("Please select a student.");
            return;
        }

        List<int> equipmentIds = new List<int>();

        foreach (CheckoutLine line in CheckoutLines)
        {
            equipmentIds.Add(line.Item.Id);
        }

        DateOnly? returnDate = null;

        if (ExpectedReturnDate != null)
        {
            returnDate = DateOnly.FromDateTime(ExpectedReturnDate.Value.Date);
        }

        // All business rules (2+ items, no duplicates, eligibility, limit, availability, date)
        // are checked by the service, inside one transaction.
        OperationResult result = await _borrowEquipmentService.CheckOutMultipleAsync(
            SelectedStudent.Id, equipmentIds, returnDate);

        ShowResult(result);

        if (result.IsSuccess)
        {
            CheckoutLines.Clear();
        }

        // Always refresh: after success, rejection, failure or an unknown outcome
        // the screen must show what is really in the database now.
        // (Selecting the student again also refreshes the "active borrowings" text.)
        await LoadAsync();
    }

    // ---- Demo / test options ----

    partial void OnSimulateFailureAfterFirstWriteChanged(bool value)
    {
        if (value)
        {
            SimulateLostCommit = false;
            _failureSimulator.Mode = SimulatedCheckoutFailure.AfterFirstWrite;
        }
        else if (!SimulateLostCommit)
        {
            _failureSimulator.Mode = SimulatedCheckoutFailure.None;
        }
    }

    partial void OnSimulateLostCommitChanged(bool value)
    {
        if (value)
        {
            SimulateFailureAfterFirstWrite = false;
            _failureSimulator.Mode = SimulatedCheckoutFailure.LostCommitAcknowledgement;
        }
        else if (!SimulateFailureAfterFirstWrite)
        {
            _failureSimulator.Mode = SimulatedCheckoutFailure.None;
        }
    }
}
