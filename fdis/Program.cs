using System.Threading.Channels;
using fdis.Consumers;
using fdis.Data;
using fdis.Interfaces;
using fdis.Middlewares;
using fdis.Producers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace fdis
{
    internal class Program
    {
        public static  IConfiguration Configuration;
        private static string         _testPath = @"D:\Repos\C#\fdis\samples";
        private static bool           _error;

        private static async Task<int> Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders()
                   .SetMinimumLevel(LogLevel.Debug)
                   .AddZLoggerConsole()
                   .AddZLoggerRollingFile((offset, i) => Path.Combine(Directory.GetCurrentDirectory(), $"{offset.Date:yyMMdd}_{i}.log"),
                                          RollingInterval.Month);

            AddConfig(args);

            var producerChannel = Channel.CreateUnbounded<ContentInfo>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });

            var producer = new FileReader();
            await SetupProducer(producer, producerChannel, _testPath);

            var middle = new FileCompressor();
            var middleChannel = await SetupMiddlewares(producerChannel, default);

            var consumer = new FileWriter();
            await SetupConsumer(consumer, middleChannel);

            return _error ? 1 : 0;
        }

        private static async ValueTask FillProducerChannel(IProducer            producer,
                                                           string               sourceUri,
                                                           Channel<ContentInfo> channel,
                                                           CancellationToken    cancellationToken = default)
        {
            var files = await producer.GetFiles(sourceUri, cancellationToken);
            for (var index = 0; index < files.Span.Length; index++)
            {
                var file = files.Span[index];
                await channel.Writer.WriteAsync(file, cancellationToken);
            }

            channel.Writer.Complete();
        }

        private static void AddConfig(string[] args)
        {
            var defaults = new Dictionary<string, string>();
            defaults.Add("SaveFolder", Path.Combine(Directory.GetCurrentDirectory(), "files"));
            defaults.Add("Threads",    "1");

            var builder = new ConfigurationBuilder()
                         .AddInMemoryCollection(defaults!)
                         .SetBasePath(Directory.GetCurrentDirectory())
                         .AddJsonFile("config.json", optional: true, reloadOnChange: true)
                         .AddCommandLine(args);

            Configuration = builder.Build();
        }

        private static async ValueTask SetupProducer(IProducer            producer,
                                                     Channel<ContentInfo> channel,
                                                     string               sourceUri,
                                                     CancellationToken    cancellationToken = default)
        {
            var results = await producer.ProvideData(sourceUri, channel, cancellationToken);
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }

        private static async ValueTask SetupConsumer(IConsumer consumer, Channel<ContentInfo> channel, CancellationToken cancellationToken = default)
        {
            var results = await consumer.ConsumeData(channel, cancellationToken);
            foreach (var result in results)
            {
                Console.WriteLine(result);
                if (result.Status == Result.ResultStatus.Error)
                    Program._error = true;
            }
        }

        private static async ValueTask<Channel<ContentInfo>> SetupMiddlewares(Channel<ContentInfo> sourceChannel,
                                                                              CancellationToken    cancellationToken = default,
                                                                              params IMiddleWare[] middleWares)
        {
            var lastChannel = sourceChannel;
            for (int i = 0; i < middleWares.Length; i++)
            {
                var middleware = middleWares[i];
                var targetChannel =
                    Channel.CreateUnbounded<ContentInfo>(new UnboundedChannelOptions
                    {
                        SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true
                    });
                await middleware.ProcessData(sourceChannel, targetChannel, cancellationToken);
                lastChannel = targetChannel;
            }

            return lastChannel;
        }
    }
}
