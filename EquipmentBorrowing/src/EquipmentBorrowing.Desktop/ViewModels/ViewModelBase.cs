using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

// Shared feedback message for every screen.
// The View colours the message box differently for success, rejection and error,
// so the user can always tell the three situations apart.
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool hasStatus;

    [ObservableProperty]
    private bool isStatusSuccess;

    [ObservableProperty]
    private bool isStatusRejected;

    [ObservableProperty]
    private bool isStatusError;

    // Shows the result of a save operation (borrow, return, register, update...).
    protected void ShowResult(OperationResult result)
    {
        if (result.Kind == ResultKind.Success)
        {
            ShowStatus(result.Message, success: true, rejected: false, error: false);
        }
        else if (result.Kind == ResultKind.NoChange)
        {
            // Neutral information (blue box).
            ShowStatus(result.Message, success: false, rejected: false, error: false);
        }
        else if (result.Kind == ResultKind.Rejected || result.Kind == ResultKind.NotFound)
        {
            ShowStatus(result.Message, success: false, rejected: true, error: false);
        }
        else
        {
            // Conflict, Failed and UnknownOutcome are shown as errors.
            ShowStatus(result.Message, success: false, rejected: false, error: true);
        }
    }

    protected void ShowRejected(string message)
    {
        ShowStatus(message, success: false, rejected: true, error: false);
    }

    protected void ShowError(string message)
    {
        ShowStatus(message, success: false, rejected: false, error: true);
    }

    protected void ShowInfo(string message)
    {
        ShowStatus(message, success: false, rejected: false, error: false);
    }

    protected void ClearStatus()
    {
        StatusMessage = null;
        HasStatus = false;
        IsStatusSuccess = false;
        IsStatusRejected = false;
        IsStatusError = false;
    }

    private void ShowStatus(string message, bool success, bool rejected, bool error)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
        IsStatusRejected = rejected;
        IsStatusError = error;
        HasStatus = true;
    }
}
