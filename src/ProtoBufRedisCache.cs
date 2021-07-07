using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using ProtoBuf;
using RedisProtobufCollections.Utility;

namespace RedisProtobufCollections
{
    public class ProtoBufRedisCache : IStrongDistributedCache, IDistributedCache, IDisposable
    {
        private readonly RedisCache _cache;
        private readonly ThreadLocal<PoolBufferWriter<byte>?> _writer = new();
        private bool _disposed;

        public ProtoBufRedisCache(IOptions<RedisCacheOptions> optionsAccessor)
        {
            _cache = new(optionsAccessor);
        }

        public T Get<T>(string key)
        {
            byte[]? bytes = _cache.Get(key);
            if (bytes == null || bytes.Length == 0)
                return default(T)!;
            MemoryStream stream = new(bytes);
            T result = Serializer.Deserialize<T>(stream);
            stream.Dispose();
            return result;
        }

        byte[] IDistributedCache.Get(string key) => _cache.Get(key);

        public async Task<T> GetAsync<T>(string key, CancellationToken token = default)
        {
            byte[]? bytes = await _cache.GetAsync(key, token).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
                return default(T)!;
            MemoryStream stream = new(bytes);
            T result = Serializer.Deserialize<T>(stream);
            stream.Dispose();
            return result;
        }

        Task<byte[]> IDistributedCache.GetAsync(string key, CancellationToken token) => _cache.GetAsync(key, token);

        public void Refresh(string key) => _cache.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _cache.RefreshAsync(key, token);

        public void Remove(string key) => _cache.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _cache.RemoveAsync(key);

        public void Set<T>(string key, T value, DistributedCacheEntryOptions options)
        {
            PoolBufferWriter<byte> writer = GetWriter();
            Serializer.Serialize(writer, value);
            _cache.Set(key, writer.ToArray(), options);
        }

        void IDistributedCache.Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _cache.Set(key, value, options);

        public async Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            PoolBufferWriter<byte> writer = GetWriter();
            Serializer.Serialize(writer, value);
            await _cache.SetAsync(key, writer.ToArray(), options).ConfigureAwait(false);
        }

        Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token) =>
            _cache.SetAsync(key, value, options, token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PoolBufferWriter<byte> GetWriter()
        {
            PoolBufferWriter<byte>? writer = _writer.Value;
            if (writer != null)
            {
                return writer;
            }
            _writer.Value = writer = new(256);
            return writer;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _cache.Dispose();

            foreach(PoolBufferWriter<byte>? writer in _writer.Values)
            {
                writer?.Dispose();
            }
            _writer.Dispose();

            _disposed = true;
        }
    }
}
