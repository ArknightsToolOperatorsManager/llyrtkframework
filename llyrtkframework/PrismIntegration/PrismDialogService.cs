using llyrtkframework.Mvvm;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using Prism.Dialogs;

namespace llyrtkframework.PrismIntegration;

/// <summary>
/// Prism の IDialogService を使用した DialogService 実装
/// </summary>
public class PrismDialogService : llyrtkframework.Mvvm.IDialogService
{
    private readonly Prism.Dialogs.IDialogService _prismDialogService;
    private readonly ILogger<PrismDialogService> _logger;

    public PrismDialogService(
        Prism.Dialogs.IDialogService prismDialogService,
        ILogger<PrismDialogService> logger)
    {
        _prismDialogService = prismDialogService;
        _logger = logger;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var parameters = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };

            _prismDialogService.ShowDialog("ConfirmationDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                {
                    tcs.SetResult(true);
                }
                else
                {
                    tcs.SetResult(false);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show confirmation dialog");
            tcs.SetResult(false);
        }

        return tcs.Task;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var parameters = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };

            _prismDialogService.ShowDialog("ErrorDialog", parameters, result =>
            {
                tcs.SetResult(true);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show error dialog");
            tcs.SetResult(false);
        }

        return tcs.Task;
    }

    public Task ShowInformationAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var parameters = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };

            _prismDialogService.ShowDialog("InformationDialog", parameters, result =>
            {
                tcs.SetResult(true);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show information dialog");
            tcs.SetResult(false);
        }

        return tcs.Task;
    }

    public Task ShowWarningAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var parameters = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };

            _prismDialogService.ShowDialog("WarningDialog", parameters, result =>
            {
                tcs.SetResult(true);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show warning dialog");
            tcs.SetResult(false);
        }

        return tcs.Task;
    }

    public Task<Result<string>> ShowInputAsync(string title, string message, string defaultValue = "")
    {
        var tcs = new TaskCompletionSource<Result<string>>();

        try
        {
            var parameters = new DialogParameters
            {
                { "title", title },
                { "message", message },
                { "defaultValue", defaultValue }
            };

            _prismDialogService.ShowDialog("InputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var inputValue = result.Parameters.GetValue<string>("inputValue");
                    tcs.SetResult(Result<string>.Success(inputValue ?? string.Empty));
                }
                else
                {
                    tcs.SetResult(Result<string>.Failure("Input cancelled"));
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show input dialog");
            tcs.SetResult(Result<string>.FromException(ex, "Failed to show input dialog"));
        }

        return tcs.Task;
    }

    public Task<Result<string>> ShowOpenFileDialogAsync(string title, string filter = "All files (*.*)|*.*")
    {
        _logger.LogWarning("ShowOpenFileDialogAsync is not implemented for Prism integration");
        return Task.FromResult(Result<string>.Failure("Not implemented"));
    }

    public Task<Result<string>> ShowSaveFileDialogAsync(string title, string defaultFileName = "", string filter = "All files (*.*)|*.*")
    {
        _logger.LogWarning("ShowSaveFileDialogAsync is not implemented for Prism integration");
        return Task.FromResult(Result<string>.Failure("Not implemented"));
    }

    public Task<Result<string>> ShowFolderBrowserDialogAsync(string title)
    {
        _logger.LogWarning("ShowFolderBrowserDialogAsync is not implemented for Prism integration");
        return Task.FromResult(Result<string>.Failure("Not implemented"));
    }
}
