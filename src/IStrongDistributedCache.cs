using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace RedisProtobufCollections
{
    /// <summary>
    /// Represents a distributed cache of values.
    /// </summary>
    public interface IStrongDistributedCache
    {
        /// <inheritdoc cref="IDistributedCache.Get"/>
        T Get<T>(string key);

        /// <inheritdoc cref="IDistributedCache.GetAsync"/>
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="IDistributedCache.Refresh"/>
        void Refresh(string key);

        /// <inheritdoc cref="IDistributedCache.RefreshAsync"/>
        Task RefreshAsync(string key, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="IDistributedCache.Remove"/>
        void Remove(string key);

        /// <inheritdoc cref="IDistributedCache.RemoveAsync"/>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="IDistributedCache.Set"/>
        void Set<T>(string key, T value, DistributedCacheEntryOptions options);

        /// <inheritdoc cref="IDistributedCache.SetAsync"/>
        Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);
    }
}
