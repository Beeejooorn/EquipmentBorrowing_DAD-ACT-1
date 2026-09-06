namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Domain;

public interface IBorrowingRepository
{
    Task AddAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default);

    Task<List<Borrowing>> GetActiveBorrowingsByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);

}