using System;
using System.Threading;
using Microsoft.Extensions.Options;

using ProtoBuf;
using ProtoBuf.Meta;

using RedisProtobufCollections.Exceptions;
using RedisProtobufCollections.Extensions;
using RedisProtobufCollections.Utility;

using StackExchange.Redis;

namespace RedisProtobufCollections
{
    /// <summary>
    ///     A <see cref="RedisList{T}"/> using ProtoBuffer for serialization & deserialization.
    /// </summary>
    /// <typeparam name="T">The ProtoContract type.</typeparam>
    /// <remarks>
    ///     Handles serialization via read-only-memory & arrays. Inefficient for small data structures. Use only if you cant serialize as a primitive struct such as int, long, float & double.
    /// </remarks>
    public class ProtoBufRedisList<T> : RedisList<T>
        where T : new()
    {
        private ThreadLocal<PoolBufferWriter<byte>?>? _writer = new();

        public ProtoBufRedisList(IOptions<RedisListOptions> optionsAccessor) : base(optionsAccessor)
        { }

        /*
         * Serialize byte[] or ReadOnlyMemory<byte>, and deserialize as ReadOnlyMemory<byte>.
         * This way we store as StorageType.Raw and may return the heap allocated memory, if any; otherwise we get a heap allocation.
         */

        /// <inheritdoc />
        protected override T Deserialize(in RedisValue serialized)
        {
            return Serializer.Deserialize<T>(serialized);
        }

        /// <inheritdoc />
        protected override void Deserialize(in RedisValue serialized, ref T value)
        {
            value = Serializer.Deserialize<T>(serialized);
        }

        /// <inheritdoc />
        protected override void Serialize(in T obj, ref RedisValue value, out byte[] leased)
        {
            PoolBufferWriter<byte>? writer = _writer!.Value;
            if (writer == null)
                _writer.Value = writer = new(256);

            // Serialize the object
            Serializer.Serialize(writer, obj);

            value = writer.ToMemory(out leased);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_writer == null)
                return;

            base.Dispose();

            foreach(PoolBufferWriter<byte>? writer in _writer.Values)
            {
                writer?.Dispose();
            }

            _writer.Dispose();

            _writer = null;
        }
    }
}
