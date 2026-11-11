namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// UC-12 Register Student, UC-13 Update Student Details, UC-14 Change Student Eligibility.
public class StudentManagementService
{
    private const int StudentNumberMaxLength = 30;
    private const int NameMaxLength = 100;
    private const int ContactMaxLength = 100;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public StudentManagementService(IUnitOfWorkFactory unitOfWorkFactory, IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
    }

    // UC-12
    public async Task<OperationResult> RegisterStudentAsync(
        string? studentNumber,
        string? fullName,
        string? contactInfo,
        CancellationToken cancellationToken = default)
    {
        // Clean the input: remove spaces at the start/end,
        // and store a blank contact as "no contact" (null), see BR-02.
        string number = (studentNumber ?? "").Trim();
        string name = (fullName ?? "").Trim();
        string? contact = CleanOptionalText(contactInfo);

        string? validationError = ValidateStudent(number, name, contact);

        if (validationError != null)
        {
            return OperationResult.Rejected(validationError);
        }

        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            // BR-01: friendly check first. The unique index in PostgreSQL is the final guard.
            bool numberTaken = await unitOfWork.Students.StudentNumberExistsAsync(number, null, cancellationToken);

            if (numberTaken)
            {
                return OperationResult.Rejected(
                    $"Student number \"{number}\" is already registered " +
                    "(numbers are compared ignoring upper/lower case and spaces). Nothing was saved.");
            }

            // BR-04: a new student is eligible to borrow by default.
            Student student = new Student(0, number, name, contact, IsAllowedToBorrow: true);

            await unitOfWork.Students.AddAsync(student, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success(
                $"Student {student.StudentNumber} - {student.Name} was registered and is eligible to borrow.",
                student.Id);
        }
        catch (Exception ex)
        {
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }

    // UC-13: only number, name and contact can change.
    // The student keeps the same Id, so the borrowing history stays linked (BR-03).
    public async Task<OperationResult> UpdateStudentAsync(
        int studentId,
        string? studentNumber,
        string? fullName,
        string? contactInfo,
        CancellationToken cancellationToken = default)
    {
        string number = (studentNumber ?? "").Trim();
        string name = (fullName ?? "").Trim();
        string? contact = CleanOptionalText(contactInfo);

        string? validationError = ValidateStudent(number, name, contact);

        if (validationError != null)
        {
            return OperationResult.Rejected(validationError);
        }

        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            Student? student = await unitOfWork.Students.GetByIdAsync(studentId, cancellationToken);

            if (student is null)
            {
                return OperationResult.NotFound("This student no longer exists. The list has been refreshed.");
            }

            // Another student may not already use this number (this student itself is ignored).
            bool numberTaken = await unitOfWork.Students.StudentNumberExistsAsync(number, studentId, cancellationToken);

            if (numberTaken)
            {
                return OperationResult.Rejected(
                    $"Student number \"{number}\" already belongs to another student. Nothing was saved.");
            }

            student.UpdateDetails(number, name, contact);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success($"Details of {student.StudentNumber} - {student.Name} were updated.", student.Id);
        }
        catch (Exception ex)
        {
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }

    // UC-14: allowToBorrow = true means "permit", false means "restrict".
    public async Task<OperationResult> ChangeEligibilityAsync(
        int studentId,
        bool allowToBorrow,
        CancellationToken cancellationToken = default)
    {
        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            Student? student = await unitOfWork.Students.GetByIdAsync(studentId, cancellationToken);

            if (student is null)
            {
                return OperationResult.NotFound("This student no longer exists. The list has been refreshed.");
            }

            // 2a. The student already has the chosen status: tell the user, change nothing.
            if (student.IsAllowedToBorrow == allowToBorrow)
            {
                string currentStatus = allowToBorrow ? "eligible to borrow" : "restricted";
                return OperationResult.NoChange($"{student.Name} is already {currentStatus}. No changes were made.");
            }

            student.SetEligibility(allowToBorrow);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            if (allowToBorrow)
            {
                return OperationResult.Success($"{student.Name} is now eligible to borrow equipment.", student.Id);
            }

            // BR-04: restricting does not cancel or return existing borrowings.
            return OperationResult.Success(
                $"{student.Name} is now restricted from borrowing. " +
                "Existing active borrowings are not affected and can still be returned.",
                student.Id);
        }
        catch (Exception ex)
        {
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }

    // Returns an error message, or null when the input is valid.
    private static string? ValidateStudent(string number, string name, string? contact)
    {
        if (number == "")
        {
            return "Student number is required.";
        }

        if (number.Length > StudentNumberMaxLength)
        {
            return $"Student number can have at most {StudentNumberMaxLength} characters.";
        }

        if (name == "")
        {
            return "Full name is required.";
        }

        if (name.Length > NameMaxLength)
        {
            return $"Full name can have at most {NameMaxLength} characters.";
        }

        if (contact != null && contact.Length > ContactMaxLength)
        {
            return $"Contact information can have at most {ContactMaxLength} characters.";
        }

        return null;
    }

    // "   " or "" becomes null (no value). Other text is trimmed.
    private static string? CleanOptionalText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }
}
