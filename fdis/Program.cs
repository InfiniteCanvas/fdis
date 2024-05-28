using System.Diagnostics.CodeAnalysis;
using System.Text;
using fdis.Consumers;
using fdis.Data;
using fdis.Interfaces;
using fdis.Middlewares;
using fdis.Producers;
using fdis.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Utf8StringInterpolation;
using ZLogger;
using ZLogger.Formatters;
using ZLogger.Providers;

namespace fdis
{
    internal class Program
    {
        private static bool _error;

        [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
        [RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
        private static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            var settings = new AppSettings();
            new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config.json", optional: true, reloadOnChange: true)
               .AddCommandLine(args)
               .Build()
               .Bind(settings);

            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders()
                   .SetMinimumLevel(LogLevel.Information)
                   .AddZLoggerConsole(options =>
                                      {
                                          options.UsePlainTextFormatter(ConsoleFormatter);
                                      })
                   .AddZLoggerRollingFile(options =>
                                          {
                                              options.FilePathSelector = (offset, i) => $"{offset:yyMMdd}_{i}.log";
                                              options.RollingInterval = RollingInterval.Day;
                                              options.RollingSizeKB = 1024 * 10;
                                              options.UsePlainTextFormatter(TextFileFormatter);
                                          });

            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton(cts);
            builder.Services.AddKeyedSingleton<IProducer, FileReader>("FileReader");
            builder.Services.AddKeyedSingleton<IMiddleWare, FileArchiver>("FileArchiver");
            builder.Services.AddKeyedSingleton<IConsumer, FileWriter>("FileWriter");
            builder.Services.AddSingleton<SemaphoreSlim>(provider =>
                                                         {
                                                             var config = provider.GetService<AppSettings>();
                                                             return config != null ? new SemaphoreSlim(config.Threads) : new SemaphoreSlim(1);
                                                         });

            builder.Services.AddHostedService<Main>();
            var host = builder.Build();

            await host.RunAsync(token: cts.Token);
        }

        private static void TextFileFormatter(PlainTextZLoggerFormatter formatter)
        {
            formatter.SetPrefixFormatter($"[{0}|{1}]",
                                         (in MessageTemplate template,
                                          in LogInfo         info) => template.Format(info.Timestamp,
                                                                                      info.LogLevel));
            formatter.SetSuffixFormatter($" |{0}.{1}|",
                                         (in MessageTemplate template,
                                          in LogInfo         info) => template.Format(info.Category, info.MemberName));
            formatter.SetExceptionFormatter((writer, ex) => Utf8String.Format(writer,
                                                                              $"{ex.Message}"));
        }

        private static void ConsoleFormatter(PlainTextZLoggerFormatter formatter)
        {
            formatter.SetPrefixFormatter($"[{0}|{1}][{2}] ",
                                         (in MessageTemplate template,
                                          in LogInfo         info) => template.Format(info.Timestamp.Local.ToString("hh:mm:ss"),
                                                                                      info.LogLevel,
                                                                                      Encoding.UTF8.GetString(info.Category.Utf8Span[(info.Category
                                                                                             .Utf8Span
                                                                                             .LastIndexOf((byte)'.')
                                                                                        + 1)..])));
            formatter.SetSuffixFormatter($" [{0}]",
                                         (in MessageTemplate template,
                                          in LogInfo         info) => template.Format(info.MemberName));
            formatter.SetExceptionFormatter((writer, ex) => Utf8String.Format(writer,
                                                                              $"{ex.Message}"));
        }
    }
}
