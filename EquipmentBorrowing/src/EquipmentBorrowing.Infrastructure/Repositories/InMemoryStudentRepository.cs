namespace EquipmentBorrowing.Infrastructure.Repositories;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class InMemoryStudentRepository : IStudentRepository
{
    private readonly List<Student> _students = new();

    public Task<Student?> GetStudentAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = _students.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(student);
    }
    public void Seed(IEnumerable<Student> students)
    {
        _students.AddRange(students);
    }

}