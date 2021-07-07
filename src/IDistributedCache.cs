using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace RedisProtobufCollections
{
    public interface IDistributedCache<T>
    {
        T Get(string key);

        Task<T> GetAsync(string key, CancellationToken cancellationToken = default);

        void Refresh(string key);

        Task RefreshAsync(string key, CancellationToken cancellationToken = default);

        void Remove(string key);

        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        void Set(string key, T value, DistributedCacheEntryOptions options);

        Task SetAsync(string key, T value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);
    }
}
