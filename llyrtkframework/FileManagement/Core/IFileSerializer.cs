namespace llyrtkframework.FileManagement.Core;

/// <summary>
/// ファイルのシリアライズ・デシリアライズを行うインターフェース
/// </summary>
/// <typeparam name="T">シリアライズする型</typeparam>
public interface IFileSerializer<T> where T : class
{
    /// <summary>オブジェクトを文字列にシリアライズします</summary>
    string Serialize(T data);

    /// <summary>文字列からオブジェクトをデシリアライズします</summary>
    T Deserialize(string content);
}
