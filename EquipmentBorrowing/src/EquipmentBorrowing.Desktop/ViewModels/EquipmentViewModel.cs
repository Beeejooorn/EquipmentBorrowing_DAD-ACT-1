namespace EquipmentBorrowing.Desktop.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

public partial class EquipmentViewModel : ViewModelBase
{
    private readonly EquipmentQueryService _equipmentQueryService;
    private readonly StudentQueryService _studentQueryService;
    private readonly BorrowEquipmentService _borrowEquipmentService;

    public ObservableCollection<Equipment> EquipmentList { get; } = new();
    public ObservableCollection<Student> Students { get; } = new();

    [ObservableProperty]
    private Equipment? selectedEquipment;

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private DateTimeOffset? expectedReturnDate = DateTimeOffset.Now.AddDays(7);

    [ObservableProperty]
    private string? statusMessage;

    public EquipmentViewModel(
        EquipmentQueryService equipmentQueryService,
        StudentQueryService studentQueryService,
        BorrowEquipmentService borrowEquipmentService)
    {
        _equipmentQueryService = equipmentQueryService;
        _studentQueryService = studentQueryService;
        _borrowEquipmentService = borrowEquipmentService;

        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var equipment = await _equipmentQueryService.GetAllEquipmentAsync();
        EquipmentList.Clear();
        foreach (var item in equipment)
        {
            EquipmentList.Add(item);
        }

        var students = await _studentQueryService.GetAllStudentsAsync();
        Students.Clear();
        foreach (var student in students)
        {
            Students.Add(student);
        }
    }



    [RelayCommand]
    private async Task BorrowAsync()
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Please select a student.";
            return;
        }

        if (SelectedEquipment is null)
        {
            StatusMessage = "Please select equipment.";
            return;
        }

        if (ExpectedReturnDate is null)
        {
            StatusMessage = "Please select an expected return date.";
            return;
        }

        var result = await _borrowEquipmentService.BorrowAsync(
            SelectedStudent.Id,
            SelectedEquipment.Id,
            ExpectedReturnDate.Value.DateTime);

        StatusMessage = result.IsSuccess
            ? $"Borrowed successfully. Return by {result.Borrowing!.ExpectedReturnDate:d}."
            : result.ErrorMessage;

        await LoadAsync();
    }
}