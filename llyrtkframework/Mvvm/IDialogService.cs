using llyrtkframework.Results;

namespace llyrtkframework.Mvvm;

/// <summary>
/// ダイアログサービスのインターフェース
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 情報メッセージを表示します
    /// </summary>
    Task ShowInformationAsync(string title, string message);

    /// <summary>
    /// 警告メッセージを表示します
    /// </summary>
    Task ShowWarningAsync(string title, string message);

    /// <summary>
    /// エラーメッセージを表示します
    /// </summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// 確認ダイアログを表示します
    /// </summary>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>
    /// 入力ダイアログを表示します
    /// </summary>
    Task<Result<string>> ShowInputAsync(string title, string message, string defaultValue = "");

    /// <summary>
    /// ファイルを開くダイアログを表示します
    /// </summary>
    Task<Result<string>> ShowOpenFileDialogAsync(string title, string filter = "All files (*.*)|*.*");

    /// <summary>
    /// ファイルを保存するダイアログを表示します
    /// </summary>
    Task<Result<string>> ShowSaveFileDialogAsync(string title, string defaultFileName = "", string filter = "All files (*.*)|*.*");

    /// <summary>
    /// フォルダ選択ダイアログを表示します
    /// </summary>
    Task<Result<string>> ShowFolderBrowserDialogAsync(string title);
}
