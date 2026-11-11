namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

public interface IBorrowingRepository
{
    Task AddAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default);

    // Tracked: the return service changes it and saves it.
    Task<Borrowing?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveBorrowingAsync(
        int equipmentId,
        CancellationToken cancellationToken = default);

    // UC-04
    Task<List<BorrowingRecord>> GetActiveRecordsAsync(
        CancellationToken cancellationToken = default);

    // UC-07
    Task<List<BorrowingRecord>> GetStudentHistoryAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    // UC-08
    Task<List<BorrowingRecord>> GetEquipmentHistoryAsync(
        int equipmentId,
        CancellationToken cancellationToken = default);

    // UC-09
    Task<List<BorrowingRecord>> SearchAsync(
        BorrowingSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
