using System;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace RedisProtobufCollections.Extensions
{
    public class RedisListOptions : IOptions<RedisListOptions>, IOptions<RedisCacheOptions>
    {
        public RedisKey RedisListKey { get; set; }

        public RedisCacheOptions CacheOptions { get; set;} = null!;

        RedisListOptions IOptions<RedisListOptions>.Value => this;

        RedisCacheOptions IOptions<RedisCacheOptions>.Value => CacheOptions;
    }
}
