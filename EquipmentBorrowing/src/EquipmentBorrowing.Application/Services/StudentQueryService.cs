namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class StudentQueryService
{
    private readonly IStudentRepository _studentRepository;

    public StudentQueryService(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
    }

    public Task<IEnumerable<Student>> GetAllStudentsAsync(CancellationToken cancellationToken = default)
    {
        return _studentRepository.GetAllAsync(cancellationToken);
    }
}