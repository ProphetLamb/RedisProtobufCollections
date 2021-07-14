using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using RedisProtobufCollections.Exceptions;

namespace RedisProtobufCollections
{
    /// <summary>
    ///     A ordered <see cref="ICollection{T}"/>-like representation of an entry of a <see cref="RedisCache"/>.
    /// </summary>
    /// <typeparam name="T">The type of items of the list.</typeparam>
    public class ProtobufRedisKeyOrderedCollection<T> :
        ICollection<T>,
        ICollection,
        IReadOnlyCollection<T>
        where T : new()
    {
        private readonly ProtobufRedisKeyKeyList<T> _keyList;
        private IComparer<T>? _comparer; 
        
        public ProtobufRedisKeyOrderedCollection(IOptions<RedisListOptions> optionsAccessor)
        {
            _keyList = new ProtobufRedisKeyKeyList<T>(optionsAccessor);
        }

        /// <inheritdoc cref="List{T}.Count" />
        public int Count => _keyList.Count;

        /// <summary>
        ///     The comparer used to determine the order in which elements are placed within the list.
        /// </summary>
        public IComparer<T> Comparer
        {
            get => _comparer ?? Comparer<T>.Default;
            set => _comparer = value;
        }

        /// <inheritdoc/>
        public bool IsSynchronized => _keyList.IsSynchronized;

        /// <inheritdoc/>
        public object SyncRoot => _keyList.SyncRoot;

        /// <inheritdoc/>
        bool ICollection<T>.IsReadOnly => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            int index = BinarySearch(item);
            if (index < 0)
            {
                _keyList.Insert(~index, item);
            }
        }

        /// <inheritdoc/>
        public void Clear() => _keyList.Clear();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => BinarySearch(item) >= 0;

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            int index = BinarySearch(item);
            if (index < 0)
            {
                _keyList.RemoveAt(~index);
                return true;
            }
            return false;
        }
        
        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex)
        {
            _keyList.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public void CopyTo(Array array, int index) => CopyTo((T[])array, index);

        /// <inheritdoc cref="List{T}.RemoveAt" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_OverEqualsMax(ExceptionArgument.index, Count);
            }
            _keyList.RemoveAt(index);
        }
        
        internal int BinarySearch(in T item)
        {
            IComparer<T> comparer = Comparer;
            int lo = 0;
            int hi = _keyList.Count - 1;
            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                int order = comparer.Compare(_keyList[i], item);
 
                if (order == 0)
                {
                    return i;
                }
                if (order < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
                
            }
 
            return ~lo;
        }

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
        public RedisKeyList<T>.Enumerator GetEnumerator() => _keyList.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}