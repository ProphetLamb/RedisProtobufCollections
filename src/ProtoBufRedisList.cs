using System;

using ProtoBuf;
using ProtoBuf.Meta;

using RedisProtobufCollections.Exceptions;
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
    {
        private PoolBufferWriter<byte>? _writer = new(256);

        public ProtoBufRedisList(string key, ConnectionMultiplexer connection) : base(key, connection)
        {
            // Throws if it isn't a ProtoContract type.
            _ = RuntimeTypeModel.Default.Add(typeof(T), false);
        }

        /// <inheritdoc />
        protected override IDatabase GetRedisDatabase()
        {
            ThrowHelper.ThrowIfObjectDisposed(m_connection == null);
            return m_connection!.GetDatabase();
        }

        /*
         * Serialize byte[], and deserialize as ReadOnlyMemory<byte>.
         * This way we store as StorageType.Raw and may return the heap allocated memory, if any; otherwise we get a heap allocation.
         */
        /// <inheritdoc />
        protected override T Deserialize(in RedisValue serialized)
        {
            return Serializer.Deserialize<T>(serialized);
        }

        /// <inheritdoc />
        protected override RedisValue Serialize(in T obj)
        {
            ThrowHelper.ThrowIfObjectDisposed(_writer == null);

            // Serialize the object
            Serializer.Serialize(_writer, obj);

            // Sadly we have to copy the memory of the writer to the wild-wild heap, because we cant just keep jonking memory from the pool.
            return _writer.ToArray();
        }
        /// <inheritdoc />
        protected override RedisValue Serialize(in T obj, out byte[] leased)
        {
            ThrowHelper.ThrowIfObjectDisposed(_writer == null);

            // Serialize the object
            Serializer.Serialize(_writer, obj);
            
            return _writer.ToMemory(out leased);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_writer == null)
                return;

            base.Dispose();

            _writer.Dispose();
            _writer = null;
        }
    }
}
