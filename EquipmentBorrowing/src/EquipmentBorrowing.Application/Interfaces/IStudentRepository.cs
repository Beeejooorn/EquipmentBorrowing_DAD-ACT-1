namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Domain;

public interface IStudentRepository
{
    // Tracked: the service may change the student and save it.
    Task<Student?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<List<Student>> GetAllAsync(
        CancellationToken cancellationToken = default);

    // Partial, case-insensitive match on student number or name. Empty text returns everyone.
    Task<List<Student>> SearchAsync(
        string? searchText,
        CancellationToken cancellationToken = default);

    // Compares numbers after trimming and ignoring upper/lower case (BR-01).
    // ignoreStudentId lets an update skip the student being edited.
    Task<bool> StudentNumberExistsAsync(
        string studentNumber,
        int? ignoreStudentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Student student,
        CancellationToken cancellationToken = default);
}
