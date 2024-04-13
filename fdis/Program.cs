using System.Threading.Channels;
using fdis.Consumers;
using fdis.Producers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis
{
    internal class Program
    {
        private static string _testPath = @"D:\Repos\C#\fdis\samples";

        private static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders()
                   .SetMinimumLevel(LogLevel.Debug)
                   .AddZLoggerConsole();

            var producerChannel = Channel.CreateUnbounded<(ContentInfo, Memory<byte>)>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });

            var producer = new FileSystemProducer();

            Console.WriteLine($"Reading from {_testPath}");
            var files = await producer.GetFileData(_testPath);

            for (int i = 0; i < files.Length; i++)
            {
                var file = files.Span[i];
                Console.WriteLine($"Pushing file {file.Path} into producer");
                var data = await producer.GetData(file);
                await producerChannel.Writer.WriteAsync((file, data));
            }

            producerChannel.Writer.Complete();

            var consumer = new FileSystemConsumer();
            await foreach (var (contentInfo, memory) in producerChannel.Reader.ReadAllAsync())
            {
                var result = await consumer.Consume(contentInfo, memory);
                Console.WriteLine($"Result {result.Status}, {result.Info}");
            }
        }
    }
}
