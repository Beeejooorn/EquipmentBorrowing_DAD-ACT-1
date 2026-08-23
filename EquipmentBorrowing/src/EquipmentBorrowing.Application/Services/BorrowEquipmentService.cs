namespace EquipmentBorrowing.Application.Services;


using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class BorrowEquipmentService
{
    private const int MaxActiveBorrowings = 3;

    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IBorrowingRepository _borrowingRepository;

    public BorrowEquipmentService(
        IStudentRepository studentRepository,
        IEquipmentRepository equipmentRepository,
        IBorrowingRepository borrowingRepository)
    {
        this._studentRepository = studentRepository;
        this._equipmentRepository = equipmentRepository;
        this._borrowingRepository = borrowingRepository;
    }

    public async Task<BorrowResult> BorrowAsync(
        int studentId,
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentAsync(studentId, cancellationToken);

        if (student is null)
        {
            return new BorrowResult(false, "Student not found.", null);
        }

        if (!student.IsAllowedToBorrow)
        {
            return new BorrowResult(false, "Student is not allowed to borrow.", null);

        }

        var equipment = await _equipmentRepository.GetByIdAsync(equipmentId, cancellationToken);

        if (equipment is null)
        {
            return new BorrowResult(false, "Equipment not found.", null);

        }

        if (!equipment.IsAvailable)
        {
            return new BorrowResult(false, "Equipment is not available", null);
        }

        var activeBorrowings = await _borrowingRepository.GetActiveBorrowingsByStudentAsync(studentId, cancellationToken);

        if (activeBorrowings.Count >= MaxActiveBorrowings)
        {
            return new BorrowResult(false, "Student has reached the maximum number of active borrowings.", null);

        }
        var borrowing = new Borrowing(
            Id: 0,
            StudentId: studentId,
            EquipmentId: equipmentId,
            DateBorrowed: DateTime.UtcNow,
            ExpectedReturnDate: DateTime.UtcNow.AddDays(7));

        await _borrowingRepository.AddAsync(borrowing, cancellationToken);

        equipment.MarkAsUnAvailable();
        await _equipmentRepository.UpdateAsync(equipment, cancellationToken);

        return new BorrowResult(true, null, borrowing);
        // more checks coming next — leave the rest empty for now

    }
}