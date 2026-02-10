using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using System.Windows.Input;

namespace llyrtkframework.Mvvm;

/// <summary>
/// 非同期デリゲートコマンド
/// </summary>
public class AsyncDelegateCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly ILogger? _logger;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncDelegateCommand(Func<Task> execute, Func<bool>? canExecute = null, ILogger? logger = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _logger = logger;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync();
    }

    /// <summary>
    /// コマンドを非同期実行します
    /// </summary>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing async command");
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// CanExecuteChangedイベントを発火します
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// パラメータ付き非同期デリゲートコマンド
/// </summary>
/// <typeparam name="T">パラメータの型</typeparam>
public class AsyncDelegateCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private readonly ILogger? _logger;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncDelegateCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null, ILogger? logger = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _logger = logger;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync((T?)parameter);
    }

    /// <summary>
    /// コマンドを非同期実行します
    /// </summary>
    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(parameter);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing async command with parameter");
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// CanExecuteChangedイベントを発火します
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
