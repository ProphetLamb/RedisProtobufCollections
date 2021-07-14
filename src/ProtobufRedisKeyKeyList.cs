using System;
using HeaplessUtility.Pool;
using Microsoft.Extensions.Options;

using ProtoBuf;

using StackExchange.Redis;

namespace RedisProtobufCollections
{
    /// <summary>
    ///     A <see cref="RedisKeyList{T}"/> using ProtoBuffer for serialization & deserialization.
    /// </summary>
    /// <typeparam name="T">The ProtoContract type.</typeparam>
    /// <remarks>
    ///     Handles serialization via read-only-memory & arrays. Inefficient for small data structures. Use only if you cant serialize as a primitive struct such as int, long, float & double.
    /// </remarks>
    public class ProtobufRedisKeyKeyList<T> : RedisKeyList<T>
        where T : new()
    {
        public ProtobufRedisKeyKeyList(IOptions<RedisListOptions> optionsAccessor) : base(optionsAccessor)
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
            BufferWriter<byte> writer = BufferWriterCache<byte>.Acquire(256);
            // Serialize the object
            Serializer.Serialize(writer, obj);
            value = writer.ToMemory(out leased);
            
            BufferWriterCache<byte>.Release(writer);
        }
    }
}
