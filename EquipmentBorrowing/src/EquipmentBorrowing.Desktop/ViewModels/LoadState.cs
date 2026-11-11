using CommunityToolkit.Mvvm.ComponentModel;

namespace EquipmentBorrowing.Desktop.ViewModels;

// The state of one list on the screen while it is loaded from the database.
// The View shows exactly one of: a loading bar, an error with Retry,
// a "record not found" message, an "empty" message, or the list itself (BR-19).
public partial class LoadState : ObservableObject
{
    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isNotFound;

    [ObservableProperty]
    private string? notFoundMessage;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string? emptyMessage;

    public void StartLoading()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        IsNotFound = false;
        NotFoundMessage = null;
        IsEmpty = false;
    }

    public void ShowLoaded(int itemCount, string emptyText)
    {
        IsLoading = false;
        HasError = false;
        ErrorMessage = null;
        IsNotFound = false;
        NotFoundMessage = null;
        EmptyMessage = emptyText;
        IsEmpty = itemCount == 0;
    }

    // The data could not be loaded (for example the database is not reachable).
    public void ShowError(string message)
    {
        IsLoading = false;
        IsEmpty = false;
        IsNotFound = false;
        NotFoundMessage = null;
        ErrorMessage = message;
        HasError = true;
    }

    // The selected student/item does not exist anymore (missing or stale selection).
    public void ShowNotFound(string message)
    {
        IsLoading = false;
        IsEmpty = false;
        HasError = false;
        ErrorMessage = null;
        NotFoundMessage = message;
        IsNotFound = true;
    }

    public void Clear()
    {
        IsLoading = false;
        HasError = false;
        ErrorMessage = null;
        IsNotFound = false;
        NotFoundMessage = null;
        IsEmpty = false;
    }
}
