namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

// Students screen:
// UC-12 Register Student, UC-13 Update Student Details, UC-14 Change Student Eligibility.
public partial class StudentsViewModel : ViewModelBase
{
    private readonly StudentQueryService _studentQueryService;
    private readonly StudentManagementService _studentManagementService;

    public ObservableCollection<Student> StudentList { get; } = new();

    public LoadState ListState { get; } = new();

    // ---- Finding a student ----

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private Student? selectedStudent;

    // ---- The form on the right side ----

    [ObservableProperty]
    private string? formStudentNumber;

    [ObservableProperty]
    private string? formFullName;

    [ObservableProperty]
    private string? formContactInfo;

    // ---- Information about the selected student ----

    [ObservableProperty]
    private bool hasSelectedStudent;

    [ObservableProperty]
    private bool isSelectedStudentEligible;

    [ObservableProperty]
    private string selectedStudentTitle = "No student selected";

    public StudentsViewModel(
        StudentQueryService studentQueryService,
        StudentManagementService studentManagementService)
    {
        _studentQueryService = studentQueryService;
        _studentManagementService = studentManagementService;
    }

    // Runs automatically when the user selects another student in the list.
    partial void OnSelectedStudentChanged(Student? value)
    {
        if (value is null)
        {
            // Keep what is typed in the form (the list may just be reloading).
            HasSelectedStudent = false;
            IsSelectedStudentEligible = false;
            SelectedStudentTitle = "No student selected";
            return;
        }

        FormStudentNumber = value.StudentNumber;
        FormFullName = value.Name;
        FormContactInfo = value.ContactInfo;

        HasSelectedStudent = true;
        IsSelectedStudentEligible = value.IsAllowedToBorrow;
        SelectedStudentTitle = $"{value.StudentNumber} - {value.Name}";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await LoadStudentsAsync(SelectedStudent?.Id);
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        SearchText = "";
        await LoadStudentsAsync(SelectedStudent?.Id);
    }

    // Loads the list (using the search box) and selects the student with this Id again.
    private async Task LoadStudentsAsync(int? studentIdToSelect)
    {
        ListState.StartLoading();

        QueryResult<Student> result = await _studentQueryService.SearchStudentsAsync(SearchText);

        StudentList.Clear();

        if (!result.IsSuccess)
        {
            ListState.ShowError(result.ErrorMessage ?? "Could not load students.");
            return;
        }

        foreach (Student student in result.Items)
        {
            StudentList.Add(student);
        }

        // "No matches" is a normal empty state, not an error (BR-17, BR-19).
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ListState.ShowLoaded(StudentList.Count, "No students are registered yet.");
        }
        else
        {
            ListState.ShowLoaded(StudentList.Count, $"No students match \"{SearchText.Trim()}\".");
        }

        if (studentIdToSelect != null)
        {
            foreach (Student student in StudentList)
            {
                if (student.Id == studentIdToSelect)
                {
                    SelectedStudent = student;
                }
            }
        }
    }

    // UC-12
    [RelayCommand]
    private async Task RegisterAsync()
    {
        OperationResult result = await _studentManagementService.RegisterStudentAsync(
            FormStudentNumber, FormFullName, FormContactInfo);

        ShowResult(result);

        if (result.IsSuccess)
        {
            // Show the saved record: reload everything and select the new student.
            SearchText = "";
            await LoadStudentsAsync(result.RecordId);
        }
        else if (result.Kind != ResultKind.Rejected)
        {
            // Database problem: refresh so the screen shows the current data.
            await LoadStudentsAsync(null);
        }
        // When the input was rejected we keep the form, so the user can correct it.
    }

    // UC-13
    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (SelectedStudent is null)
        {
            ShowRejected("Select the student you want to edit first.");
            return;
        }

        int studentId = SelectedStudent.Id;

        OperationResult result = await _studentManagementService.UpdateStudentAsync(
            studentId, FormStudentNumber, FormFullName, FormContactInfo);

        ShowResult(result);

        if (result.IsSuccess)
        {
            await LoadStudentsAsync(studentId);
        }
        else if (result.Kind == ResultKind.NotFound)
        {
            // The student was removed or changed elsewhere: refresh and clear the selection.
            await LoadStudentsAsync(null);
        }
        else if (result.Kind != ResultKind.Rejected)
        {
            await LoadStudentsAsync(studentId);
        }
    }

    // UC-14 (permit)
    [RelayCommand]
    private async Task PermitAsync()
    {
        await ChangeEligibilityAsync(true);
    }

    // UC-14 (restrict)
    [RelayCommand]
    private async Task RestrictAsync()
    {
        await ChangeEligibilityAsync(false);
    }

    private async Task ChangeEligibilityAsync(bool allowToBorrow)
    {
        if (SelectedStudent is null)
        {
            ShowRejected("Select a student first.");
            return;
        }

        int studentId = SelectedStudent.Id;

        OperationResult result = await _studentManagementService.ChangeEligibilityAsync(studentId, allowToBorrow);

        ShowResult(result);

        if (result.Kind == ResultKind.NotFound)
        {
            await LoadStudentsAsync(null);
        }
        else
        {
            // Reload so the list shows the saved status (or the current one after an error).
            await LoadStudentsAsync(studentId);
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedStudent = null;
        FormStudentNumber = "";
        FormFullName = "";
        FormContactInfo = "";
        ClearStatus();
    }
}
