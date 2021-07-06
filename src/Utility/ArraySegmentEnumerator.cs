using System;
using System.Collections;
using System.Collections.Generic;

namespace RedisProtobufCollections.Utility
{
    /*
     * Source: https://gist.github.com/ProphetLamb/92fe57a889cb325ae2dbc74aaa8da608
     */
    public struct ArraySegmentEnumerator<TItem> : IEnumerator<TItem>
    {
        private TItem[]? _elements;
        private readonly int _startIndex;
        private readonly int _count;
        private int _index;

        internal ArraySegmentEnumerator(TItem[]? elements, int startIndex, int count)
        {
            _elements = elements;
            _index = _startIndex = startIndex;
            _count = count;
        }

        public int Index => _index - 1;

        /// <inheritdoc />
        public bool MoveNext() => _elements != null && _index++ < _count;

        /// <inheritdoc />
        public void Reset() => _index = _startIndex;

        /// <inheritdoc />
        public TItem Current => _elements![_index-1];

        /// <inheritdoc />
        object? IEnumerator.Current => Current;

        /// <inheritdoc />
        public void Dispose()
        {
            _elements = null;
        }
    }
}
