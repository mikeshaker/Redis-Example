﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Redis.Example.Extensions;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace Redis.Example
{
    internal class Program
    {
        private static readonly int Total = 30000;

        private static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();


            IServiceCollection services = new ServiceCollection();
            services.AddRedis(config);

            var serviceProvider = services.BuildServiceProvider();

            var _redisClient = serviceProvider.GetService<IRedisCacheClient>();
            //Normal
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,defaultDatabase=6");

            var db = redis.GetDatabase();

            var x = _redisClient.Db0.Database.IsConnected("");
            Console.ResetColor();
            for (var i = 1; i <= 5; i++)
            {
                await _redisClient.Db1.FlushDbAsync();
                await _redisClient.Db6.FlushDbAsync();
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Batch # {i}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                await NewMethod(_redisClient);
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Magenta;
                await OldMethod(db);
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        private static async Task NewMethod(IRedisCacheClient _redisClient)
        {
            await GenerateClocks(_redisClient.Db1);

            await LookupSingleItem(_redisClient.Db1);
            await LookupAllItems(_redisClient.Db1);
        }

        private static async Task OldMethod(IDatabase db)
        {
            await GenerateClocks(db);
            await LookupSingleItem2(db);
        }

        private static async Task LookupSingleItem(IRedisDatabase db)

        {
            var sw = Stopwatch.StartNew();
            var random = new Random();
            for (var i = 0; i < 100; i++)
            {
                var id = $"GT-{random.Next(0, Total)}";
                var oneClock = await db.HashGetAsync<ClockInfo>("clocksPartition1", id);

                if (oneClock == null) Console.WriteLine("not found");
            }

            Console.WriteLine($"Looking up single item a 100 times (v2): {Math.Round(sw.Elapsed.TotalMilliseconds)}ms");
        }

        private static async Task LookupSingleItem2(IDatabaseAsync db)
        {
            var sw = Stopwatch.StartNew();

            var random = new Random();

            for (var i = 0; i < 100; i++)
            {
                var id = $"GT-{random.Next(0, Total)}";

                var entries = await db.HashGetAllAsync($"clock:{id}");
                var clock = ClockInfo.FromHashEntries(entries);

                if (clock == null) Console.WriteLine("not found");
            }

            Console.WriteLine($"Looking up single item a 100 times (v1): {Math.Round(sw.Elapsed.TotalMilliseconds)}ms");
        }

        private static async Task LookupAllItems(IRedisDatabase db)
        {
            var sw = Stopwatch.StartNew();
            var allClocks = await db.HashGetAllAsync<ClockInfo>("clocksPartition1");

            Console.WriteLine(
                $"Get {allClocks.Count} Items back as List (v2): {Math.Round(sw.Elapsed.TotalMilliseconds)}ms");
        }

        /// <summary>
        ///     Using Extensions
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task GenerateClocks(IRedisDatabase db)
        {
            var sw = Stopwatch.StartNew();

            var allClocks = new Dictionary<string, ClockInfo>();
            var random = new Random();

            for (var i = 0; i < Total; i++)
            {
                var partition = $"GT-{i}";
                var clock = new ClockInfo
                {
                    DeviceId = partition,
                    SerialNumber = partition.ToLower(),
                    ClientId = random.Next(10000, 99999),
                    ClockGroupId = Guid.NewGuid()
                };
                allClocks.Add(partition, clock);
            }

            await db.HashSetAsync("clocksPartition1", allClocks);

            await db.UpdateExpiryAsync("clocksPartition1", DateTimeOffset.Now.AddMinutes(5));
            Console.WriteLine($"Storing {Total} (v2): {Math.Round(sw.Elapsed.TotalMilliseconds)}ms");
        }

        /// <summary>
        ///     Using HashSetAsync
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task GenerateClocks(IDatabaseAsync db)
        {
            var sw = Stopwatch.StartNew();
            var random = new Random();

            for (var i = 0; i < Total; i++)
            {
                var id = $"GT-{i}";
                var clock = new ClockInfo
                {
                    DeviceId = id,
                    SerialNumber = id.ToLower(),
                    ClientId = random.Next(10000, 99999),
                    ClockGroupId = Guid.NewGuid()
                };
                await db.HashSetAsync($"clock:{id}", clock.ToHashEntries());
                await db.KeyExpireAsync($"clock:{id}", DateTime.Now.AddMinutes(5));
            }

            Console.WriteLine(
                $"Storing {Total} Using original HashSetAsync (v1): {Math.Round(sw.Elapsed.TotalMilliseconds, 3)}ms");
        }


        public class ClockInfo
        {
            public string DeviceId { get; set; }
            public string SerialNumber { get; set; }
            public int ClientId { get; set; }
            public Guid ClockGroupId { get; set; }

            public static ClockInfo FromHashEntries(HashEntry[] hashEntries)
            {
                var clock = new ClockInfo();
                foreach (var entry in hashEntries)
                    switch ((string) entry.Name)
                    {
                        case "DeviceId":
                            clock.DeviceId = entry.Value;
                            break;
                        case "SerialNumber":
                            clock.SerialNumber = entry.Value;
                            break;
                        case "ClientId":
                            clock.ClientId = (int) entry.Value;
                            break;
                        case "ClockGroupId":
                            clock.ClockGroupId = Guid.Parse(entry.Value);
                            break;
                    }

                return clock;
            }

            public HashEntry[] ToHashEntries()
            {
                return new[]
                {
                    new HashEntry("DeviceId", DeviceId),
                    new HashEntry("SerialNumber", SerialNumber),
                    new HashEntry("ClientId", ClientId),
                    new HashEntry("ClockGroupId", ClockGroupId.ToString())
                };
            }
        }
    }
}