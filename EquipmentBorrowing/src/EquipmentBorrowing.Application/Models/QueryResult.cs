namespace EquipmentBorrowing.Application.Models;

// Returned by every "load a list" operation.
// It lets the screen tell apart three different situations (BR-19):
//   - IsSuccess with an empty list   -> "no records / no matches"
//   - IsNotFound                     -> the selected student/item does not exist anymore
//   - not IsSuccess                  -> the data could not be loaded (e.g. database is down)
public class QueryResult<T>
{
    public bool IsSuccess { get; }
    public bool IsNotFound { get; }
    public List<T> Items { get; }
    public string? ErrorMessage { get; }

    private QueryResult(bool isSuccess, bool isNotFound, List<T> items, string? errorMessage)
    {
        IsSuccess = isSuccess;
        IsNotFound = isNotFound;
        Items = items;
        ErrorMessage = errorMessage;
    }

    public static QueryResult<T> Ok(List<T> items)
    {
        return new QueryResult<T>(true, false, items, null);
    }

    public static QueryResult<T> NotFound(string message)
    {
        return new QueryResult<T>(false, true, new List<T>(), message);
    }

    public static QueryResult<T> Error(string message)
    {
        return new QueryResult<T>(false, false, new List<T>(), message);
    }
}
