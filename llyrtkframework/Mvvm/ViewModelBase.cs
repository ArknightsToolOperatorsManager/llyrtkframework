using ReactiveUI;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace llyrtkframework.Mvvm;

/// <summary>
/// ViewModelの基底クラス
/// </summary>
public abstract class ViewModelBase : ReactiveObject, INotifyPropertyChanged
{
    private bool _isBusy;
    private string _title = string.Empty;

    /// <summary>
    /// ビュー表示中かどうか
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    /// <summary>
    /// ビューのタイトル
    /// </summary>
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    /// <summary>
    /// ViewModelが初期化される際に呼ばれます
    /// </summary>
    public virtual void OnInitialize()
    {
    }

    /// <summary>
    /// Viewがアクティブになった際に呼ばれます
    /// </summary>
    public virtual void OnActivated()
    {
    }

    /// <summary>
    /// Viewが非アクティブになった際に呼ばれます
    /// </summary>
    public virtual void OnDeactivated()
    {
    }

    /// <summary>
    /// プロパティ変更通知のヘルパーメソッド
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// プロパティ変更イベントを発火します
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.RaisePropertyChanged(propertyName);
    }

    /// <summary>
    /// リソースを解放します
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        // 派生クラスでオーバーライドしてリソースを解放
    }
}
