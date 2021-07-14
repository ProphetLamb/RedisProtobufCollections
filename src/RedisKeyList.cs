using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using RedisProtobufCollections.Exceptions;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace RedisProtobufCollections
{
    /// <summary>
    ///     A <see cref="IList"/>-like representation of an entry of a <see cref="RedisCache"/>.
    /// </summary>
    /// <typeparam name="T">The type of items of the list.</typeparam>
    public abstract class RedisKeyList<T> :
        ICollection<T>,
        ICollection,
        IReadOnlyList<T>,
        IDisposable
        where T : new()
    {
        private volatile IConnectionMultiplexer? _connection;
        
        private readonly SemaphoreSlim _connectionLock = new(1,1);

        private RedisListOptions? _options;

        protected RedisKey m_key;

        protected IDatabase? m_cache;

        private readonly string _instance;

        public RedisKeyList(RedisKey key, IOptions<RedisCacheOptions> optionsAccessor)
            : this(new RedisListOptions { RedisListKey = key, CacheOptions = optionsAccessor.Value })
        { }

        protected RedisKeyList(IOptions<RedisListOptions> optionsAccessor)
        {
            if (optionsAccessor.Value?.CacheOptions == null)
                ThrowHelper.ThrowArgumentException(ExceptionArgument.optionAccessor, "RedisListOptions are invalid, CacheOptions are null.");

            _options = optionsAccessor.Value;
            _instance = optionsAccessor.Value.CacheOptions.InstanceName;
        }

        /// <inheritdoc />
        public bool IsSynchronized => true;

        /// <summary>The <see cref="RedisDatabase"/>.</summary>
        public object SyncRoot
        {
            get
            {
                Connect();
                return m_cache!;
            }
        }

        /// <inheritdoc cref="IList{T}.Count" />
        public int Count
        {
            get
            {
                Connect();
                return (int)m_cache!.ListLength(m_key);
            }
        }

        /// <inheritdoc />
        bool ICollection<T>.IsReadOnly => false;

        /// <inheritdoc cref="IList{T}.this" />
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Connect();

                return Deserialize(m_cache!.ListGetByIndex(m_key, index));
            }
        }

        /// <summary>
        /// Assigns a value to <see cref="_connection"/>, <see cref="m_key"/>, <see cref="m_cache"/> when called the first time.
        /// </summary>
        /// <exception cref="InvalidOperationException">The object is disposed.</exception>
        protected virtual void Connect()
        {
            ThrowHelper.ThrowIfObjectDisposed(_options == null);

            if (m_cache != null)
                return; // Already initialized.

            _connectionLock.Wait(); // Prevent connection reentry
            try
            {
                RedisCacheOptions options = _options.CacheOptions;
                if (options.ConnectionMultiplexerFactory == null)
                {
                    if (options.ConfigurationOptions != null)
                    {
                        _connection = ConnectionMultiplexer.Connect(options.ConfigurationOptions);
                    }
                    else
                    {
                        _connection = ConnectionMultiplexer.Connect(options.Configuration);
                    }
                }
                else
                {
                    _connection = options.ConnectionMultiplexerFactory().GetAwaiter().GetResult();
                }
                TryRegisterProfiler();
                m_cache = _connection.GetDatabase();
                m_key = _options.RedisListKey;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void TryRegisterProfiler()
        {
            if (_connection != null && _options!.CacheOptions.ProfilingSession != null)
            {
                _connection.RegisterProfiler(_options.CacheOptions.ProfilingSession);
            }
        }

        /// <summary>
        ///     Serializes a object to a <see cref="RedisValue"/>.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <param name="serialized">>A value that can be added to the <see cref="RedisDatabase"/>. Could be dirty, may only be written to.</param>
        /// <param name="leased">The array of the memory backing the <see cref="RedisValue"/>, to be returned to the array-pool when no longer needed.</param>
        protected abstract void Serialize(in T value, ref RedisValue serialized, out byte[] leased);

        /// <summary>
        ///     Deserializes a <see cref="RedisValue"/> to the object of the instance.
        /// </summary>
        /// <param name="serialized">The value obtained from the redis database.</param>
        /// <returns>A item of the list.</returns>
        protected abstract T Deserialize(in RedisValue serialized);

        /// <summary>
        ///     Deserializes a <see cref="RedisValue"/> to the object of the instance.
        /// </summary>
        /// <param name="serialized">The value obtained from the redis database.</param>
        /// <param name="value">A item of the list.</param>
        protected abstract void Deserialize(in RedisValue serialized, ref T value);

        /// <inheritdoc />
        public int IndexOf(T item)
        {
            Connect();

            IDatabase cache = m_cache!;
            RedisValue probe = new();
            Serialize(item, ref probe, out byte[] leased);

            for (int i = 0; i < Count; i++)
            {
                // Both are StorageType.Raw, RedisValue will call Span.SequenceEquals, which is Memory.Compare
                if (!probe.Equals(cache.ListGetByIndex(m_key, i)))
                    continue;

                ArrayPool<byte>.Shared.Return(leased);
                return i;
            }

            return -1;
        }

        /// <inheritdoc />
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <inheritdoc />
        public void Add(T item)
        {
            Connect();

            RedisValue value = new();
            Serialize(item, ref value, out byte[] leased);

            m_cache!.ListRightPush(m_key, value);

            ArrayPool<byte>.Shared.Return(leased);
        }

        /// <inheritdoc />
        public void Insert(int index, T item)
        {
            Connect();

            RedisValue value = new();
            Serialize(item, ref value, out byte[] leased);

            m_cache!.ListInsertBefore(m_key, m_cache.ListGetByIndex(m_key, index), value);

            ArrayPool<byte>.Shared.Return(leased);
        }

        /// <inheritdoc />
        public bool Remove(T item)
        {
            Connect();

            RedisValue value = new();
            Serialize(item, ref value, out byte[] leased);

            long removedAt = m_cache!.ListRemove(m_key, value);

            ArrayPool<byte>.Shared.Return(leased);
            return removedAt > 0;
        }

        /// <inheritdoc />
        public void RemoveAt(int index)
        {
            Connect();

            RedisValue value = m_cache!.ListGetByIndex(m_key, index);
            if (!value.IsNull)
            {
                m_cache.ListRemove(m_key, value);
            }
        }

        /// <inheritdoc />
        public void Clear() => m_cache?.KeyDelete(m_key);

        public T[] GetRange(int startIndex, int count)
        {
            if (count < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException_LessZero(ExceptionArgument.count);

            Connect();

            RedisValue[] values = m_cache!.ListRange(m_key, startIndex, startIndex + count);
            T[] segment = new T[values.Length];

            for(int i = 0; i < values.Length; i++)
            {
                Deserialize(values[i], ref segment[i]);
            }

            return segment;
        }

        public Span<T> GetRange(int startIndex, int count, out T[] leased)
        {
            if (count < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException_LessZero(ExceptionArgument.count);

            Connect();

            RedisValue[] values = m_cache!.ListRange(m_key, startIndex, startIndex + count - 1);

            if (values.Length != count)
                ThrowHelper.ThrowInvalidOperationException($"Redis returned {values.Length} items, but expected {count} items.");

            leased = ArrayPool<T>.Shared.Rent(count);

            for(int i = 0; i < count; i++)
            {
                Deserialize(values[i], ref leased[i]);
            }

            return leased.AsSpan(0, count);
        }

        public int AddRange(IEnumerable<T> items)
        {
            Connect();

            int count;
            if (items is ICollection<T> collection)
            {
                count = collection.Count;
                T[] buffer = ArrayPool<T>.Shared.Rent(count);

                collection.CopyTo(buffer, 0);
                AddSpan(buffer.AsSpan(0, count));

                ArrayPool<T>.Shared.Return(buffer, true);
                return count;
            }

            count = 0;
            RedisValue value = new();
            foreach(T item in items)
            {
                Serialize(item, ref value, out byte[] leased);

                m_cache!.ListRightPush(m_key, value);

                ArrayPool<byte>.Shared.Return(leased);
                // At this point memory at value is dirty, but can still be used by serialize as it only writes value not reads.
                count++;
            }
            return count;
        }

        public void AddSpan(Span<T> items)
        {
            Connect();

            RedisValue value = new();

            for(int i = 0; i < items.Length; i++)
            {
                Serialize(items[i], ref value, out byte[] leased);

                m_cache!.ListRightPush(m_key, value);

                ArrayPool<byte>.Shared.Return(leased);
                // At this point memory at value is dirty, but can still be used by serialize as it only writes value not reads.
            }
        }

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            Connect();

            m_cache!.ListRange(m_key).CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        void ICollection.CopyTo(Array array, int index)
        {
            CopyTo((T[])array, index);
        }

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
        public Enumerator GetEnumerator() => new(this);

        /// <inheritdoc />
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public virtual void Dispose()
        {
            if (_options == null)
                return;

            _connectionLock.Dispose();
            _connection!.Dispose();

            _options = null;
            _connection = null;
            m_cache = null;
        }

        public struct Enumerator : IEnumerator<T>
        {
            private RedisKeyList<T>? _keyList;
            private readonly int _count;
            internal int Index;

            public Enumerator(RedisKeyList<T> keyList)
            {
                _keyList = keyList;
                _count = keyList.Count;
                Index = 0;
            }

            public T Current => _keyList![Index-1];

            object? IEnumerator.Current => Current;

            public void Dispose()
            {
                _keyList = null;
            }

            public bool MoveNext()
            {
                return Index++ < _count;
            }

            public void Reset()
            {
                Index = 0;
            }
        }
    }
}
