namespace llyrtkframework.Domain;

/// <summary>
/// 値オブジェクトの基底クラス
/// DDD（ドメイン駆動設計）における値オブジェクトを実装するための基底クラス
/// </summary>
/// <remarks>
/// 値オブジェクトは以下の特性を持ちます：
/// - 不変性（Immutability）
/// - 構造的等価性（Structural Equality）
/// - 副作用のない振る舞い
/// </remarks>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// 等価性の比較に使用するコンポーネントを返します
    /// </summary>
    /// <returns>等価性チェックに使用する値のコレクション</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// オブジェクトが等しいかどうかを判定します
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// ValueObject の等価性を判定します
    /// </summary>
    public bool Equals(ValueObject? other)
    {
        if (other == null || other.GetType() != GetType())
            return false;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// ハッシュコードを取得します
    /// </summary>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    /// <summary>
    /// 等価演算子
    /// </summary>
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// 非等価演算子
    /// </summary>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 値オブジェクトの文字列表現を返します
    /// デフォルトでは型名を返しますが、オーバーライド可能です
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name} {{ {string.Join(", ", GetEqualityComponents().Select(c => c?.ToString() ?? "null"))} }}";
    }
}

/// <summary>
/// 単一の値を持つ値オブジェクトの基底クラス
/// </summary>
/// <typeparam name="T">値の型</typeparam>
public abstract class SingleValueObject<T> : ValueObject
{
    /// <summary>
    /// 内部の値
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="value">値</param>
    protected SingleValueObject(T value)
    {
        Value = value;
    }

    /// <summary>
    /// 等価性の比較に使用するコンポーネントを返します
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// 文字列表現を返します
    /// </summary>
    public override string ToString()
    {
        return Value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 暗黙的に T 型に変換します
    /// </summary>
    public static implicit operator T(SingleValueObject<T> valueObject)
    {
        return valueObject.Value;
    }
}
