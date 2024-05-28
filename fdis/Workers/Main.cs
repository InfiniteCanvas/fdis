using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Workers
{
    public class Main(
        ILogger<Main>            logger,
        AppSettings              settings,
        IServiceProvider         serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
    {
        private IProducer?    _provider;
        private IMiddleWare[] _middlewares;
        private IConsumer[]   _consumers;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.ZLogInformation($"Starting File Distributor (fdis)..");

            SetupComponents();
            var tasks = new List<Task>();

            // provider
            var unboundedChannelOptions = new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true };
            var sourceChannel = Channel.CreateUnbounded<ContentInfo>(unboundedChannelOptions);
            logger.ZLogInformation($"Running provider {settings.Provider}");
            Task.Run(async () =>
                     {
                         foreach (var result in await _provider!.ProvideData(settings.Source, sourceChannel, stoppingToken).ConfigureAwait(false))
                         {
                             logger.ZLogDebug($"{result.Status.ToString()}: {result.Info}");
                         }
                     },
                     stoppingToken)
                .AddTo(tasks);

            // middlewares
            var middleChannels = new Channel<ContentInfo>[settings.Middlewares.Length + 1];
            var middlewareCounter = 0;
            middleChannels[0] = sourceChannel;
            if (_middlewares.Length > 0)
            {
                foreach (var middleware in _middlewares)
                {
                    middlewareCounter++;
                    logger.ZLogInformation($"Linking middleware {middleware.Name}");
                    middleChannels[middlewareCounter] = Channel.CreateUnbounded<ContentInfo>(unboundedChannelOptions);
                    var counter = middlewareCounter;
                    Task.Run(async () =>
                             {
                                 foreach (var result in await middleware.ProcessData(middleChannels[counter - 1],
                                                                                     middleChannels[counter],
                                                                                     stoppingToken)
                                                                        .ConfigureAwait(false))
                                 {
                                     logger.ZLogDebug($"{result.Status.ToString()}: {result.Info}");
                                 }
                             },
                             stoppingToken)
                        .AddTo(tasks);
                }
            }

            // consumers
            foreach (var consumer in _consumers)
            {
                Task.Run(async () =>
                         {
                             foreach (var result in await consumer.ConsumeData(middleChannels[middlewareCounter], stoppingToken)
                                                                  .ConfigureAwait(false))
                             {
                                 logger.ZLogDebug($"{result.Status.ToString()}: {result.Info}");
                             }
                         },
                         stoppingToken)
                    .AddTo(tasks);
            }

            await Task.WhenAll(tasks);
            logger.ZLogInformation($"Done with all tasks. Shutting down..");

            hostApplicationLifetime.StopApplication();
        }

        private void SetupComponents()
        {
            _provider = serviceProvider.GetKeyedService<IProducer>(settings.Provider);
            if (settings.Provider.IsNullOrWhiteSpace() || _provider == null)
            {
                logger.ZLogCritical($"No provider found");
                hostApplicationLifetime.StopApplication();
            }

            _consumers = settings.Consumers.Select(serviceProvider.GetKeyedService<IConsumer>).OfType<IConsumer>().ToArray();

            if (settings.Consumers.Length == 0 || _consumers.Length == 0)
            {
                logger.ZLogCritical($"No consumers found");
                hostApplicationLifetime.StopApplication();
            }

            _middlewares = settings.Middlewares.Select(serviceProvider.GetKeyedService<IMiddleWare>).OfType<IMiddleWare>().ToArray();
        }
    }
}
