using llyrtkframework.Results;

namespace llyrtkframework.Mvvm;

/// <summary>
/// ナビゲーションサービスのインターフェース
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// 指定したViewModelに遷移します
    /// </summary>
    Task<Result> NavigateAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;

    /// <summary>
    /// 指定した名前のビューに遷移します
    /// </summary>
    Task<Result> NavigateAsync(string viewName, object? parameter = null);

    /// <summary>
    /// 前のビューに戻ります
    /// </summary>
    Task<Result> GoBackAsync();

    /// <summary>
    /// 戻ることができるかどうか
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// ナビゲーション履歴をクリアします
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// 現在のViewModelを取得します
    /// </summary>
    ViewModelBase? CurrentViewModel { get; }
}
