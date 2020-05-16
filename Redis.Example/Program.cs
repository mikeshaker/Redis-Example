using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Redis.PruebasDeConcepto.Extensions;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Redis.Example
{
    class Program
    {
        private static int Total = 30000;
        static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();


            IServiceCollection services = new ServiceCollection();
            services.AddRedis(config);

            var serviceProvider = services.BuildServiceProvider();

            IRedisCacheClient _redisClient = serviceProvider.GetService<IRedisCacheClient>();
            await _redisClient.Db1.FlushDbAsync();
            await _redisClient.Db6.FlushDbAsync();

            //Normal
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,defaultDatabase=6");

            var db = redis.GetDatabase();

            Console.ResetColor();
            for (int i = 1; i <= 5; i++)
            {
                 Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Batch # {i}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                await NewMethod(_redisClient);
                Console.ResetColor();
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var random = new Random();
            for (var i = 0; i < 100; i++)
            {
                var id = $"GT-{random.Next(0, Total)}";
                var oneClock = await db.HashGetAsync<ClockInfo>("clocksPartition1", id);

                if (oneClock == null)
                {
                    Console.WriteLine("not found");
                }
            }

            Console.WriteLine($"Looking up single item a 100 times (v2): {sw.Elapsed.TotalMilliseconds}ms");
        }

        private static async Task LookupAllItems(IRedisDatabase db)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var allClocks = await db.HashGetAllAsync<ClockInfo>("clocksPartition1");

            Console.WriteLine($"Get {allClocks.Count} Items back as List (v2): {sw.Elapsed.TotalMilliseconds}ms");
        }
        private static async Task GenerateClocks(IRedisDatabase db)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var allClocks = new Dictionary<string, ClockInfo>();
                var random = new Random();

                for (var i = 0; i < Total; i++)
                {
                    var partition = $"GT-{i}";
                    var clock = new ClockInfo()
                    {
                    DeviceId = partition,
                    SerialNumber = partition.ToLower(),
                    ClientId = random.Next(10000, 99999),
                    ClockGroupId = Guid.NewGuid(),
                    };
                    allClocks.Add(partition, clock);
                }
                await db.HashSetAsync<ClockInfo>("clocksPartition1", allClocks);
            Console.WriteLine($"Storing {Total} (v2): {sw.Elapsed.TotalMilliseconds}ms");
        }

        private static async Task GenerateClocks(IDatabaseAsync db)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var allClocks = new Dictionary<string, ClockInfo>();

            var random = new Random();

            for (var i = 0; i < Total; i++)
            {
                var id = $"GT-{i}";
                var clock = new ClockInfo()
                {
                    DeviceId = id,
                    SerialNumber = id.ToLower(),
                    ClientId = random.Next(10000, 99999),
                    ClockGroupId = Guid.NewGuid(),

                };
                await db.HashSetAsync($"clock:{id}", clock.ToHashEntries());
                allClocks.Add(id, clock);
            }

            Console.WriteLine($"Storing {Total} Using original HashSetAsync (v1): {sw.Elapsed.TotalMilliseconds}ms");

        }

        private static async Task LookupSingleItem2(IDatabaseAsync db)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var random = new Random();

            for (var i = 0; i < 100; i++)
            {
                var id = $"GT-{random.Next(0, Total)}";

                var entries = await db.HashGetAllAsync($"clock:{id}");
                var clock = ClockInfo.FromHashEntries(entries);

                if (clock == null)
                {
                    Console.WriteLine("not found");
                }
            }
            Console.WriteLine($"Looking up single item a 100 times (v1): {sw.Elapsed.TotalMilliseconds}ms");
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
                {
                    switch ((string)entry.Name)
                    {
                        case "DeviceId": clock.DeviceId = entry.Value; break;
                        case "SerialNumber": clock.SerialNumber = entry.Value; break;
                        case "ClientId": clock.ClientId = (int)entry.Value; break;
                        case "ClockGroupId": clock.ClockGroupId = Guid.Parse(entry.Value); break;
                    }
                }
                return clock;

            }
            public HashEntry[] ToHashEntries()
            {
                return new HashEntry[]
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