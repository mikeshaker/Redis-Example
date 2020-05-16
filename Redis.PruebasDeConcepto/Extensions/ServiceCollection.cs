﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis.Extensions.Core;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.Newtonsoft;
using StackExchange.Redis.Extensions.Utf8Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redis.PruebasDeConcepto.Extensions
{
    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
        {

			var redisConfiguration = new RedisConfiguration()
			{
				AbortOnConnectFail = true,
				KeyPrefix = "",
				Hosts = new RedisHost[]
				{
					new RedisHost(){Host = "localhost", Port = 6379},
				},
				AllowAdmin = true,
				ConnectTimeout = 3000,
				Database = 5,
				Ssl = false,
				ServerEnumerationStrategy = new ServerEnumerationStrategy()
				{
					Mode = ServerEnumerationStrategy.ModeOptions.All,
					TargetRole = ServerEnumerationStrategy.TargetRoleOptions.Any,
					UnreachableServerAction = ServerEnumerationStrategy.UnreachableServerActionOptions.Throw
				},
				MaxValueLength = 1024,
				PoolSize = 5
			};
			//	var redisConfiguration = configuration.GetSection("Redis").Get<RedisConfiguration>();

			services.AddSingleton<IRedisCacheClient, RedisCacheClient>();
			services.AddSingleton<IRedisCacheConnectionPoolManager, RedisCacheConnectionPoolManager>();
			services.AddSingleton<ISerializer, NewtonsoftSerializer>();


			services.AddSingleton((provider) =>
			{
				return provider.GetRequiredService<IRedisCacheClient>().GetDbFromConfiguration();
			});

			services.AddSingleton(redisConfiguration);
			return services;

        }

    }
}
