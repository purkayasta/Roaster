using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Roaster.Editor.Shared;

public abstract class EditorComponentBase : ComponentBase
{
    [Inject] public ISnackbar Snackbar { get; set; } = null!;
    [Inject] public IDialogService DialogService { get; set; } = null!;

    protected bool Running { get; private set; }

    protected async Task RunLogException(Func<Task> action)
    {
        Running = true;
        StateHasChanged();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            Running = false;
            StateHasChanged();
        }
    }

    protected void RunLogException(Action action)
    {
        Running = true;
        StateHasChanged();
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            Running = false;
            StateHasChanged();
        }
    }
}
