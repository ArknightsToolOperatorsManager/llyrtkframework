using FluentValidation;
using FluentValidation.Results;
using ReactiveUI;
using System.Collections;
using System.ComponentModel;
using System.Reactive.Linq;

namespace llyrtkframework.Mvvm;

/// <summary>
/// FluentValidation統合ViewModelベースクラス
/// INotifyDataErrorInfoをサポートし、自動バリデーションを提供します
/// </summary>
/// <typeparam name="TViewModel">ViewModelの型（自己参照型）</typeparam>
/// <typeparam name="TValidator">FluentValidatorの型</typeparam>
public abstract class ValidatableViewModelBase<TViewModel, TValidator> : ViewModelBase, INotifyDataErrorInfo
    where TViewModel : ValidatableViewModelBase<TViewModel, TValidator>
    where TValidator : IValidator<TViewModel>, new()
{
    private readonly TValidator _validator = new();
    private readonly Dictionary<string, List<string>> _errors = new();
    private ValidationResult? _lastValidationResult;
    private IDisposable? _validationSubscription;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Any();

    /// <summary>
    /// バリデーションが有効かどうか（自動バリデーションON/OFF）
    /// </summary>
    private bool _isValidationEnabled = true;
    public bool IsValidationEnabled
    {
        get => _isValidationEnabled;
        set => this.RaiseAndSetIfChanged(ref _isValidationEnabled, value);
    }

    /// <summary>
    /// バリデーションのデバウンス時間（デフォルト: 300ms）
    /// </summary>
    private TimeSpan _validationDebounce = TimeSpan.FromMilliseconds(300);
    public TimeSpan ValidationDebounce
    {
        get => _validationDebounce;
        set
        {
            _validationDebounce = value;
            SetupAutoValidation(); // デバウンス時間変更時に再設定
        }
    }

    protected ValidatableViewModelBase()
    {
        SetupAutoValidation();
    }

    private void SetupAutoValidation()
    {
        // 既存のサブスクリプションを破棄
        _validationSubscription?.Dispose();

        // プロパティ変更時に自動バリデーション
        _validationSubscription = this.Changed
            .Throttle(_validationDebounce) // デバウンス
            .Where(_ => IsValidationEnabled)
            .ObserveOn(RxApp.MainThreadScheduler) // UIスレッドで実行
            .Subscribe(async _ =>
            {
                try
                {
                    await ValidateAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Validation error: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// 非同期バリデーション実行
    /// </summary>
    public async Task<bool> ValidateAsync()
    {
        try
        {
            _lastValidationResult = await _validator.ValidateAsync(
                new ValidationContext<TViewModel>((TViewModel)this));

            UpdateErrors();
            return _lastValidationResult.IsValid;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Validation exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 同期バリデーション実行
    /// </summary>
    public bool Validate()
    {
        try
        {
            _lastValidationResult = _validator.Validate(
                new ValidationContext<TViewModel>((TViewModel)this));

            UpdateErrors();
            return _lastValidationResult.IsValid;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Validation exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 特定プロパティのバリデーション
    /// </summary>
    public async Task<bool> ValidatePropertyAsync(string propertyName)
    {
        try
        {
            var selector = new FluentValidation.Internal.MemberNameValidatorSelector(new[] { propertyName });
            var context = new ValidationContext<TViewModel>(
                (TViewModel)this,
                new FluentValidation.Internal.PropertyChain(),
                selector);

            var result = await _validator.ValidateAsync(context);

            // 該当プロパティのエラーを更新
            UpdatePropertyErrors(propertyName, result);

            return !_errors.ContainsKey(propertyName) || !_errors[propertyName].Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Property validation exception: {ex.Message}");
            return false;
        }
    }

    private void UpdateErrors()
    {
        if (_lastValidationResult == null)
            return;

        // 古いエラーをクリア
        var oldProperties = _errors.Keys.ToList();
        _errors.Clear();

        // 新しいエラーを設定
        foreach (var error in _lastValidationResult.Errors)
        {
            if (!_errors.ContainsKey(error.PropertyName))
                _errors[error.PropertyName] = new List<string>();

            _errors[error.PropertyName].Add(error.ErrorMessage);
        }

        // エラー変更を通知（古いプロパティ + 新しいプロパティ）
        var allProperties = oldProperties
            .Union(_lastValidationResult.Errors.Select(e => e.PropertyName))
            .Distinct();

        foreach (var prop in allProperties)
        {
            OnErrorsChanged(prop);
        }

        this.RaisePropertyChanged(nameof(HasErrors));
    }

    private void UpdatePropertyErrors(string propertyName, ValidationResult result)
    {
        // 該当プロパティのエラーをクリア
        if (_errors.ContainsKey(propertyName))
            _errors.Remove(propertyName);

        // 新しいエラーを設定
        foreach (var error in result.Errors.Where(e => e.PropertyName == propertyName))
        {
            if (!_errors.ContainsKey(propertyName))
                _errors[propertyName] = new List<string>();

            _errors[propertyName].Add(error.ErrorMessage);
        }

        OnErrorsChanged(propertyName);
        this.RaisePropertyChanged(nameof(HasErrors));
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _errors.Values.SelectMany(e => e);

        return _errors.ContainsKey(propertyName)
            ? _errors[propertyName]
            : Enumerable.Empty<string>();
    }

    protected void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 最後のバリデーション結果を取得
    /// </summary>
    public ValidationResult? GetLastValidationResult() => _lastValidationResult;

    /// <summary>
    /// すべてのエラーメッセージを取得
    /// </summary>
    public IEnumerable<string> GetAllErrorMessages()
    {
        return _errors.Values.SelectMany(e => e);
    }

    /// <summary>
    /// 特定プロパティのエラーメッセージを取得
    /// </summary>
    public IEnumerable<string> GetPropertyErrors(string propertyName)
    {
        return _errors.ContainsKey(propertyName)
            ? _errors[propertyName]
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// すべてのエラーをクリア
    /// </summary>
    public void ClearErrors()
    {
        var properties = _errors.Keys.ToList();
        _errors.Clear();

        foreach (var prop in properties)
        {
            OnErrorsChanged(prop);
        }

        this.RaisePropertyChanged(nameof(HasErrors));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _validationSubscription?.Dispose();
            _validationSubscription = null;
        }
        base.Dispose(disposing);
    }
}
