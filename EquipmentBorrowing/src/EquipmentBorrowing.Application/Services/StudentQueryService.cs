namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// Read-only student queries used to locate and select students.
public class StudentQueryService
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public StudentQueryService(IUnitOfWorkFactory unitOfWorkFactory, IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
    }

    public async Task<QueryResult<Student>> GetAllStudentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            List<Student> students = await unitOfWork.Students.GetAllAsync(cancellationToken);
            return QueryResult<Student>.Ok(students);
        }
        catch (Exception ex)
        {
            return QueryResult<Student>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }

    public async Task<QueryResult<Student>> SearchStudentsAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            List<Student> students = await unitOfWork.Students.SearchAsync(searchText, cancellationToken);
            return QueryResult<Student>.Ok(students);
        }
        catch (Exception ex)
        {
            return QueryResult<Student>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }
}
