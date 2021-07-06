using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using StackExchange.Redis;

namespace RedisProtobufCollections
{
    /// <summary>
    ///     A <see cref="IList"/>-like representation of a <see cref="RedisDatabase"/>.
    /// </summary>
    /// <typeparam name="T">The type of items of the database.</typeparam>
    public abstract class RedisList<T> : 
        IList<T>,
        ICollection,
        IReadOnlyList<T>,
        IDisposable
    {
        protected ConnectionMultiplexer? m_connection;
        protected readonly string m_key;

        protected RedisList(string key, ConnectionMultiplexer connection)
        {
            m_key = key;
            m_connection = connection;
        }

        /// <inheritdoc />
        public bool IsSynchronized => true;

        /// <summary>The <see cref="RedisDatabase"/>.</summary>
        public object SyncRoot => GetRedisDatabase();

        /// <summary>
        ///     Creates the <see cref="RedisDatabase"/> representing the list.
        /// </summary>
        /// <returns>The <see cref="RedisDatabase"/> representing the list.</returns>
        protected abstract IDatabase GetRedisDatabase();

        /// <summary>
        ///     Serializes a object to a <see cref="RedisValue"/>.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="leased">The array of the memory backing the <see cref="RedisValue"/>, to be returned to the array-pool when no longer needed.</param>
        /// <returns>A value that can be used while the <paramref name="leased"/> array is not returned to the pool.</returns>
        protected abstract RedisValue Serialize(in T obj, out byte[] leased);

        /// <summary>
        ///     Serializes a object to a <see cref="RedisValue"/>.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A value that can be added to the <see cref="RedisDatabase"/>.</returns>
        protected abstract RedisValue Serialize(in T obj);

        /// <summary>
        ///     Deserializes a <see cref="RedisValue"/> to the object of the instance.
        /// </summary>
        /// <param name="serialized">The value obtained from the redis database.</param>
        /// <returns>A item of the list.</returns>
        protected abstract T Deserialize(in RedisValue serialized);

        /// <inheritdoc />
        public void Insert(int index, T item)
        {
            IDatabase db = GetRedisDatabase();
            RedisValue before = db.ListGetByIndex(m_key, index);
            db.ListInsertBefore(m_key, before, Serialize(item));
        }

        /// <inheritdoc />
        public void RemoveAt(int index)
        {
            IDatabase db = GetRedisDatabase();
            RedisValue value = db.ListGetByIndex(m_key, index);
            if (!value.IsNull)
            {
                db.ListRemove(m_key, value);
            }
        }

        /// <inheritdoc cref="IList{T}.this" />
        public T this[int index]
        {
            get
            {
                RedisValue value = GetRedisDatabase().ListGetByIndex(m_key, index);
                return Deserialize(value);
            }
            set => Insert(index, value);
        }

        /// <inheritdoc />
        public void Add(T item)
        {
            GetRedisDatabase().ListRightPush(m_key, Serialize(item));
        }

        /// <inheritdoc />
        public void Clear() => GetRedisDatabase().KeyDelete(m_key);

        /// <inheritdoc />
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex) => GetRedisDatabase().ListRange(m_key).CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public int IndexOf(T item)
        {
            for (int i = 0; i < Count; i++)
            {
                // Both should be StorageType.Raw, we call a Span.SequenceEquals.
                bool areEqual = GetRedisDatabase().ListGetByIndex(m_key, i).Equals(Serialize(item, out byte[] leased));
                ArrayPool<byte>.Shared.Return(leased);
                if (areEqual)
                    return i;
            }
            return -1;
        }

        /// <inheritdoc />
        void ICollection.CopyTo(Array array, int index)
        {
            CopyTo((T[])array, index);
        }

        /// <inheritdoc cref="IList{T}.Count" />
        public int Count => (int)GetRedisDatabase().ListLength(m_key);

        /// <inheritdoc />
        bool ICollection<T>.IsReadOnly => false;

        /// <inheritdoc />
        public bool Remove(T item)
        {
            long removedAt = GetRedisDatabase().ListRemove(m_key, Serialize(item, out byte[] leased));
            ArrayPool<byte>.Shared.Return(leased);
            return removedAt > 0;
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
            if (m_connection == null)
                return;
            m_connection.Dispose();
            m_connection = null;
        }

        public struct Enumerator : IEnumerator<T>
        {
            private RedisList<T>? _list;
            private readonly int _count;
            internal int Index;

            public Enumerator(RedisList<T> list)
            {
                _list = list;
                _count = list.Count;
                Index = 0;
            }

            public T Current => _list![Index-1];

            object IEnumerator.Current => Current!;

            public void Dispose()
            {
                if (ReferenceEquals(null, _list))
                    return;
                _list = null;
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
