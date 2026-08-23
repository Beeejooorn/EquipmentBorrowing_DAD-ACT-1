namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Domain;

public interface IStudentRepository
{
    Task<Student?> GetStudentAsync(
        int id,
        CancellationToken cancellationToken = default);
}