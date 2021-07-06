using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using RedisProtobufCollections.Exceptions;

using StackExchange.Redis;

namespace RedisProtobufCollections
{
    public class ProtoBufRedisDictionary<TKey, TValue> :
        IDictionary<TKey, TValue>,
        ICollection,
        IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, int> _dictionary = new();
        private readonly ProtoBufRedisList<TValue> _redisList;

        public ProtoBufRedisDictionary(string databaseKey, ConnectionMultiplexer connection)
        {
            _redisList = new ProtoBufRedisList<TValue>(databaseKey, connection);
        }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.this" />
        public TValue this[TKey key]
        {
            get => _redisList[_dictionary[key]];
            set => _redisList[_dictionary[key]] = value;
        }

        /// <inheritdoc />
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

        /// <inheritdoc />
        public ICollection<TKey> Keys => _dictionary.Keys;

        /// <inheritdoc />
        ICollection<TValue> IDictionary<TKey, TValue>.Values => _redisList;

        /// <inheritdoc />
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _redisList;

        /// <inheritdoc cref="ICollection{T}.Count" />
        public int Count => _redisList.Count;

        /// <inheritdoc />
        public bool IsSynchronized => true;

        /// <summary>The <see cref="RedisDatabase"/>.</summary>
        public object SyncRoot => _redisList.SyncRoot;

        /// <inheritdoc />
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        /// <inheritdoc />
        public void Add(TKey key, TValue value)
        {
            int count = _redisList.Count;
            if (_dictionary.TryAdd(key, count))
            {
                _redisList.Add(value);
            }
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <inheritdoc />
        public void Clear()
        {
            _dictionary.Clear();
            _redisList.Clear();
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (_dictionary.TryGetValue(item.Key, out int index))
                return EqualityComparer<TValue>.Default.Equals(_redisList[index], item.Value);

            return false;
        }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.ContainsKey" />
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        /// <inheritdoc cref="IDictionary{Tkey, TValue}.TryGetValue" />
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (_dictionary.TryGetValue(key, out int index))
            {
                value = _redisList[index];
                return true;
            }
            value = default(TValue);
            return false;
        }

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (arrayIndex < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException_LessZero(ExceptionArgument.arrayIndex);
            if (array.Length - arrayIndex < Count)
                ThrowHelper.ThrowArgumentException_ArrayCapacityOverMax(ExceptionArgument.array, Count);
            var keyEn = _dictionary.Keys.GetEnumerator();
            var valueEn = _redisList.GetEnumerator();
            for(int i = 0; i < Count; i++)
            {
                if (keyEn.MoveNext() && valueEn.MoveNext())
                {
                    array[i + arrayIndex] = KeyValuePair.Create(keyEn.Current, valueEn.Current);
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException_EnumeratorUnsyncVersion();
                }
            }
            keyEn.Dispose();
            valueEn.Dispose();
        }

        /// <inheritdoc />
        void ICollection.CopyTo(Array array, int index) => CopyTo((KeyValuePair<TKey, TValue>[]) array, index);

        /// <inheritdoc />
        public bool Remove(TKey key)
        {
            if (_dictionary.Remove(key, out int index))
            {
                _redisList.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (_dictionary.TryGetValue(item.Key, out int index)
                && EqualityComparer<TValue>.Default.Equals(_redisList[index], item.Value))
            {
                return Remove(item.Key);
            }
            return false;
        }

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <inheritdoc />
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private ProtoBufRedisDictionary<TKey, TValue>? _dictionary;
            private IEnumerator<TKey> _keyEnumerator;
            private RedisList<TValue>.Enumerator _valueEnumerator;

            public Enumerator(ProtoBufRedisDictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _keyEnumerator = dictionary._dictionary.Keys.GetEnumerator();
                _valueEnumerator = dictionary._redisList.GetEnumerator();
            }

            /// <inheritdoc />
            public KeyValuePair<TKey, TValue> Current => KeyValuePair.Create(_keyEnumerator.Current, _valueEnumerator.Current);

            /// <inheritdoc />
            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose()
            {
                if (_dictionary == null)
                    return;
                _dictionary = null;
                _keyEnumerator.Dispose();
                _valueEnumerator.Dispose();
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                bool keyNext = _keyEnumerator.MoveNext();
                bool valueNext = _valueEnumerator.MoveNext();
                if (keyNext == valueNext)
                    return keyNext;
                ThrowHelper.ThrowInvalidOperationException_EnumeratorUnsyncVersion();
                return default; // Unreachable
            }

            /// <inheritdoc />
            public void Reset()
            {
                ThrowHelper.ThrowIfObjectDisposed(_dictionary == null);

                _keyEnumerator.Dispose();
                _keyEnumerator = _dictionary._dictionary.Keys.GetEnumerator();
                _valueEnumerator.Dispose();
                _valueEnumerator = _dictionary._redisList.GetEnumerator();
            }
        }
    }
}
