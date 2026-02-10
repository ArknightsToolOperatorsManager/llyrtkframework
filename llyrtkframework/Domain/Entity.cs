namespace llyrtkframework.Domain;

/// <summary>
/// エンティティの基底クラス
/// DDD（ドメイン駆動設計）におけるエンティティを実装するための基底クラス
/// </summary>
/// <remarks>
/// エンティティは以下の特性を持ちます：
/// - 一意の識別子（Identity）
/// - 同一性による等価性（Identity-based Equality）
/// - ライフサイクルを持つ
/// </remarks>
/// <typeparam name="TId">識別子の型</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>
    /// エンティティの識別子
    /// </summary>
    public TId Id { get; protected set; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="id">識別子</param>
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// デフォルトコンストラクタ（ORMなどで使用）
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    protected Entity()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// オブジェクトが等しいかどうかを判定します（識別子ベース）
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id.Equals(other.Id);
    }

    /// <summary>
    /// エンティティの等価性を判定します
    /// </summary>
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id.Equals(other.Id);
    }

    /// <summary>
    /// ハッシュコードを取得します
    /// </summary>
    public override int GetHashCode()
    {
        return (GetType().ToString() + Id).GetHashCode();
    }

    /// <summary>
    /// 等価演算子
    /// </summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
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
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// エンティティの文字列表現を返します
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name} [Id={Id}]";
    }
}

/// <summary>
/// Guid を識別子とするエンティティの基底クラス
/// </summary>
public abstract class GuidEntity : Entity<Guid>
{
    /// <summary>
    /// コンストラクタ
    /// </summary>
    protected GuidEntity() : base(Guid.NewGuid())
    {
    }

    /// <summary>
    /// 識別子を指定するコンストラクタ
    /// </summary>
    /// <param name="id">識別子</param>
    protected GuidEntity(Guid id) : base(id)
    {
    }
}

/// <summary>
/// int を識別子とするエンティティの基底クラス
/// </summary>
public abstract class IntEntity : Entity<int>
{
    /// <summary>
    /// デフォルトコンストラクタ
    /// </summary>
    protected IntEntity() : base(0)
    {
    }

    /// <summary>
    /// 識別子を指定するコンストラクタ
    /// </summary>
    /// <param name="id">識別子</param>
    protected IntEntity(int id) : base(id)
    {
    }
}

/// <summary>
/// long を識別子とするエンティティの基底クラス
/// </summary>
public abstract class LongEntity : Entity<long>
{
    /// <summary>
    /// デフォルトコンストラクタ
    /// </summary>
    protected LongEntity() : base(0)
    {
    }

    /// <summary>
    /// 識別子を指定するコンストラクタ
    /// </summary>
    /// <param name="id">識別子</param>
    protected LongEntity(long id) : base(id)
    {
    }
}

/// <summary>
/// string を識別子とするエンティティの基底クラス
/// </summary>
public abstract class StringEntity : Entity<string>
{
    /// <summary>
    /// デフォルトコンストラクタ
    /// </summary>
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
    protected StringEntity() : base(null)
#pragma warning restore CS8625
    {
    }

    /// <summary>
    /// 識別子を指定するコンストラクタ
    /// </summary>
    /// <param name="id">識別子</param>
    protected StringEntity(string id) : base(id)
    {
    }
}
