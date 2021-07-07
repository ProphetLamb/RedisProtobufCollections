using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace RedisProtobufCollections
{
    public interface IStrongDistributedCache
    {
        T Get<T>(string key);

        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        void Refresh(string key);

        Task RefreshAsync(string key, CancellationToken cancellationToken = default);

        void Remove(string key);

        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        void Set<T>(string key, T value, DistributedCacheEntryOptions options);

        Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default);
    }
}
