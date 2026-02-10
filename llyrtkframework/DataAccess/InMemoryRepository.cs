using llyrtkframework.Results;
using llyrtkframework.Specification;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace llyrtkframework.DataAccess;

/// <summary>
/// インメモリリポジトリの実装
/// </summary>
/// <typeparam name="T">エンティティの型</typeparam>
public class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly ConcurrentDictionary<object, T> _storage = new();
    private readonly Func<T, object> _idSelector;
    private readonly ILogger? _logger;
    private int _nextId = 1;

    public InMemoryRepository(Func<T, object> idSelector, ILogger? logger = null)
    {
        _idSelector = idSelector ?? throw new ArgumentNullException(nameof(idSelector));
        _logger = logger;
    }

    public Task<Result<T>> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = _idSelector(entity);

            // IDが未設定（0やnullなど）の場合は自動採番
            if (id == null || (id is int intId && intId == 0))
            {
                id = _nextId++;
                SetId(entity, id);
            }

            if (_storage.TryAdd(id, entity))
            {
                _logger?.LogDebug("Entity added with ID: {Id}", id);
                return Task.FromResult(Result<T>.Success(entity));
            }

            return Task.FromResult(Result<T>.Failure($"Entity with ID {id} already exists"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding entity");
            return Task.FromResult(Result<T>.FromException(ex, "Error adding entity"));
        }
    }

    public Task<Result<T>> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = _idSelector(entity);

            if (_storage.TryGetValue(id, out _))
            {
                _storage[id] = entity;
                _logger?.LogDebug("Entity updated with ID: {Id}", id);
                return Task.FromResult(Result<T>.Success(entity));
            }

            return Task.FromResult(Result<T>.Failure($"Entity with ID {id} not found"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating entity");
            return Task.FromResult(Result<T>.FromException(ex, "Error updating entity"));
        }
    }

    public Task<Result> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = _idSelector(entity);

            if (_storage.TryRemove(id, out _))
            {
                _logger?.LogDebug("Entity deleted with ID: {Id}", id);
                return Task.FromResult(Result.Success());
            }

            return Task.FromResult(Result.Failure($"Entity with ID {id} not found"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting entity");
            return Task.FromResult(Result.FromException(ex, "Error deleting entity"));
        }
    }

    public Task<Result<T>> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_storage.TryGetValue(id, out var entity))
            {
                return Task.FromResult(Result<T>.Success(entity));
            }

            return Task.FromResult(Result<T>.Failure($"Entity with ID {id} not found"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting entity by ID");
            return Task.FromResult(Result<T>.FromException(ex, "Error getting entity by ID"));
        }
    }

    public Task<Result<IEnumerable<T>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = _storage.Values.ToList();
            return Task.FromResult(Result<IEnumerable<T>>.Success(entities));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all entities");
            return Task.FromResult(Result<IEnumerable<T>>.FromException(ex, "Error getting all entities"));
        }
    }

    public Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            var compiled = predicate.Compile();
            var entities = _storage.Values.Where(compiled).ToList();
            return Task.FromResult(Result<IEnumerable<T>>.Success(entities));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error finding entities");
            return Task.FromResult(Result<IEnumerable<T>>.FromException(ex, "Error finding entities"));
        }
    }

    public Task<Result<IEnumerable<T>>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = _storage.Values.Where(specification.IsSatisfiedBy).ToList();
            return Task.FromResult(Result<IEnumerable<T>>.Success(entities));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error finding entities with specification");
            return Task.FromResult(Result<IEnumerable<T>>.FromException(ex, "Error finding entities with specification"));
        }
    }

    public Task<Result<bool>> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            var compiled = predicate.Compile();
            var exists = _storage.Values.Any(compiled);
            return Task.FromResult(Result<bool>.Success(exists));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking entity existence");
            return Task.FromResult(Result<bool>.FromException(ex, "Error checking entity existence"));
        }
    }

    public Task<Result<int>> CountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(Result<int>.Success(_storage.Count));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error counting entities");
            return Task.FromResult(Result<int>.FromException(ex, "Error counting entities"));
        }
    }

    public Task<Result<int>> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            var compiled = predicate.Compile();
            var count = _storage.Values.Count(compiled);
            return Task.FromResult(Result<int>.Success(count));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error counting entities with predicate");
            return Task.FromResult(Result<int>.FromException(ex, "Error counting entities with predicate"));
        }
    }

    private void SetId(T entity, object id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.CanWrite)
        {
            idProperty.SetValue(entity, Convert.ChangeType(id, idProperty.PropertyType));
        }
    }
}
