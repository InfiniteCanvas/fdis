using System.Diagnostics.CodeAnalysis;
using System.Text;
using fdis.Consumers;
using fdis.Data;
using fdis.Interfaces;
using fdis.Middlewares;
using fdis.Providers;
using fdis.Utilities;
using fdis.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utf8StringInterpolation;
using ZLogger;
using ZLogger.Providers;

namespace fdis
{
    internal static class Program
    {
        private static bool _error;

        [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)"),
         RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
        private static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            var builder = Host.CreateApplicationBuilder(args);

            ConfigureLogging(builder, args);
            builder.Services.Configure<HostOptions>(options =>
                                                    {
                                                        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
                                                    });

            builder.Services.Configure<AppSettings>(appSettings =>
                                                    {
                                                        new ConfigurationBuilder()
                                                           .SetBasePath(Directory.GetCurrentDirectory())
                                                           .AddJsonFile("config.json", true, true)
                                                           .AddCommandLine(args)
                                                           .Build()
                                                           .Bind(appSettings);
                                                    });
            builder.Services.AddSingleton(cts);
            builder.Services.AddKeyedTransient<IProvider, FileReader>("FileReader");
            builder.Services.AddKeyedTransient<IMiddleWare, FileArchiver>("FileArchiver");
            builder.Services.AddKeyedTransient<IMiddleWare, FileFilter>("FileFilter");
            builder.Services.AddKeyedTransient<IMiddleWare, FileSorter>("FileSorter");
            builder.Services.AddKeyedTransient<IMiddleWare, FilePathCollisionSolver>("FilePathCollisionSolver");
            builder.Services.AddKeyedTransient<IMiddleWare, DeduplicateFiles>("DeduplicateFiles");
            builder.Services.AddKeyedTransient<IMiddleWare, ConvertImagesToWebp>("ConvertImagesToWebp");
            builder.Services.AddKeyedTransient<IConsumer, FileWriter>("FileWriter");
            builder.Services.AddKeyedTransient<IConsumer, GofileUploader>("GofileUploader");
            builder.Services.AddKeyedSingleton<TimedRateLimiter>("Gofile");
            builder.Services.AddSingleton<SemaphoreSlim>(provider =>
                                                         {
                                                             var config = provider.GetService<IOptions<AppSettings>>()?.Value;
                                                             return config != null
                                                                 ? new SemaphoreSlim(config.Threads)
                                                                 : new SemaphoreSlim(1);
                                                         });
            builder.Services.ConfigureHttpClients();

            builder.Services.AddHostedService<Main>();
            var host = builder.Build();

            await host.RunAsync(cts.Token);
        }

        private static void ConfigureHttpClients(this IServiceCollection services)
        {
            services.AddHttpClient("Gofile",
                                   client =>
                                   {
                                       client.BaseAddress = new Uri(@"https://api.gofile.io/servers");
                                   });
        }

        private static void ConfigureLogging(HostApplicationBuilder builder, string[] args)
        {
            var settings = new AppSettings();
            new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config.json", true, true)
               .AddCommandLine(args)
               .Build()
               .Bind(settings);
            var logLevel = settings.Logging switch
            {
                "Critical"    => LogLevel.Critical,
                "Error"       => LogLevel.Error,
                "Warning"     => LogLevel.Warning,
                "Information" => LogLevel.Information,
                "Debug"       => LogLevel.Debug,
                "Trace"       => LogLevel.Trace,
                _             => LogLevel.Debug
            };

            builder.Logging.ClearProviders()
                   .SetMinimumLevel(logLevel)
                   .AddZLoggerConsole(ConsoleOptions)
                   .AddZLoggerRollingFile(FileOptions);
            return;

            void FileOptions(ZLoggerRollingFileOptions options)
            {
                options.FilePathSelector = (offset, i) => $"{offset:yyMMdd}_{i}.log";
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 1024 * 10;
                options.UsePlainTextFormatter(formatter =>
                                              {
                                                  formatter.SetPrefixFormatter($"[{0}|{1}]",
                                                                               (in MessageTemplate template,
                                                                                in LogInfo         info) => template.Format(info.Timestamp,
                                                                                   info.LogLevel));
                                                  formatter.SetSuffixFormatter($" |{0}.{1}|",
                                                                               (in MessageTemplate template,
                                                                                in LogInfo info) => template.Format(info.Category, info.MemberName));
                                                  formatter.SetExceptionFormatter((writer, ex) => Utf8String.Format(writer,
                                                                                      $"{ex.Message}"));
                                              });
            }

            void ConsoleOptions(ZLoggerConsoleOptions options)
                => options.UsePlainTextFormatter(formatter =>
                                                 {
                                                     formatter.SetPrefixFormatter($"[{0}|{1}][{2}] ",
                                                                                  (in MessageTemplate template,
                                                                                   in LogInfo         info)
                                                                                      => template.Format(info.Timestamp.Local.ToString("hh:mm:ss"),
                                                                                          info.LogLevel,
                                                                                          Encoding.UTF8.GetString(info.Category.Utf8Span
                                                                                              [(info.Category.Utf8Span
                                                                                                   .LastIndexOf((byte)'.')
                                                                                              + 1)..])));
                                                     formatter.SetSuffixFormatter($" [{0}]",
                                                                                  (in MessageTemplate template,
                                                                                   in LogInfo         info) => template.Format(info.MemberName));
                                                     formatter.SetExceptionFormatter((writer, ex) => Utf8String.Format(writer,
                                                                                         $"{ex.Message}"));
                                                 });
        }
    }
}
