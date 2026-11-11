using CommunityToolkit.Mvvm.ComponentModel;

namespace EquipmentBorrowing.Desktop.ViewModels;

// The state of one list on the screen while it is loaded from the database.
// The View shows exactly one of: a loading bar, an error with Retry,
// an "empty" message, or the list itself (BR-19).
public partial class LoadState : ObservableObject
{
    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string? emptyMessage;

    public void StartLoading()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        IsEmpty = false;
    }

    public void ShowLoaded(int itemCount, string emptyText)
    {
        IsLoading = false;
        HasError = false;
        ErrorMessage = null;
        EmptyMessage = emptyText;
        IsEmpty = itemCount == 0;
    }

    public void ShowError(string message)
    {
        IsLoading = false;
        IsEmpty = false;
        ErrorMessage = message;
        HasError = true;
    }

    public void Clear()
    {
        IsLoading = false;
        HasError = false;
        ErrorMessage = null;
        IsEmpty = false;
    }
}
