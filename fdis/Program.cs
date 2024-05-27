using System.Threading.Channels;
using fdis.Consumers;
using fdis.Interfaces;
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

        private static async Task Main(string[] args)
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

            Console.WriteLine($"Reading from {_testPath}");
            var files = await producer.GetFiles(_testPath);

            for (int i = 0; i < files.Length; i++)
            {
                var file = files.Span[i];
                Console.WriteLine($"Pushing file {file.Path} into producer");
                await producerChannel.Writer.WriteAsync(file);
            }

            producerChannel.Writer.Complete();

            var consumer = new FileWriter();

            await foreach (var contentInfo in producerChannel.Reader.ReadAllAsync())
            {
                var result = await consumer.Consume(contentInfo);
                Console.WriteLine($"Result {result.Status}, {result.Info}");
            }
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
    }
}
