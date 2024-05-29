using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace fdis.Workers
{
    public class Main(
        ILogger<Main>            logger,
        IOptions<AppSettings>    options,
        IServiceProvider         serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
    {
        private readonly AppSettings   _settings    = options.Value;
        private          IConsumer[]   _consumers   = [];
        private          IMiddleWare[] _middlewares = [];
        private          IProvider?    _provider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.ZLogInformation($"Starting File Distributor (fdis)..");

            SetupComponents();
            var tasks = new List<Task>();

            // provider
            var unboundedChannelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = true };
            var sourceChannel = Channel.CreateUnbounded<ContentInfo>(unboundedChannelOptions);
            logger.ZLogInformation($"Running provider {_settings.Provider}");
            Task.Run(async () =>
                     {
                         foreach (var result in await _provider!.ProvideData(_settings.Source, sourceChannel, stoppingToken).ConfigureAwait(false))
                             logger.ZLogDebug($"{result.Status.ToString()}: {result.Info}");
                     },
                     stoppingToken)
                .AddTo(tasks);

            // middlewares
            var middleChannels = new Channel<ContentInfo>[_settings.Middlewares.Length + 1];
            var middlewareCounter = 0;
            middleChannels[0] = sourceChannel;
            if (_middlewares.Length > 0)
                foreach (var middleware in _middlewares)
                {
                    middlewareCounter++;
                    logger.ZLogInformation($"Linking middleware {middleware.Name}, source[{middlewareCounter - 1}] to target[{middlewareCounter}]");
                    middleChannels[middlewareCounter] = Channel.CreateUnbounded<ContentInfo>(unboundedChannelOptions);
                    var counter = middlewareCounter;
                    Task.Run(async () =>
                             {
                                 foreach (var result in await middleware.ProcessData(middleChannels[counter - 1],
                                                                                     middleChannels[counter],
                                                                                     stoppingToken)
                                                                        .ConfigureAwait(false))
                                     logger.ZLogDebug($"{result.Status.ToString()}: {result.Info}");
                             },
                             stoppingToken)
                        .AddTo(tasks);
                }

            // consumers
            foreach (var consumer in _consumers)
                Task.Run(async () =>
                         {
                             foreach (var result in await consumer.ConsumeData(middleChannels[middlewareCounter], stoppingToken)
                                                                  .ConfigureAwait(false))
                                 logger.ZLogDebug($"{result.Status.ToString()}: {result.Info}");
                         },
                         stoppingToken)
                    .AddTo(tasks);

            await Task.WhenAll(tasks);
            logger.ZLogInformation($"Done with all tasks. Shutting down..");

            hostApplicationLifetime.StopApplication();
        }

        private void SetupComponents()
        {
            _provider = serviceProvider.GetKeyedService<IProvider>(_settings.Provider.Type);
            if (_provider == null)
            {
                logger.ZLogCritical($"No provider found");
                hostApplicationLifetime.StopApplication();
                return;
            }

            _provider.Configure(_settings.Provider.Options);

            var consumers = new List<IConsumer>();
            foreach (var consumerOptions in _settings.Consumers)
            {
                var consumer = serviceProvider.GetKeyedService<IConsumer>(consumerOptions.Type);
                consumer?.Configure(consumerOptions.Options).AddTo(consumers);
            }

            _consumers = consumers.ToArray();

            if (_settings.Consumers.Length == 0 || _consumers.Length == 0)
            {
                logger.ZLogCritical($"No consumers found");
                hostApplicationLifetime.StopApplication();
                return;
            }

            var middlewares = new List<IMiddleWare>();
            foreach (var middlewareOptions in _settings.Middlewares)
            {
                var middleware = serviceProvider.GetKeyedService<IMiddleWare>(middlewareOptions.Type);
                middleware?.Configure(middlewareOptions.Options).AddTo(middlewares);
            }

            _middlewares = middlewares.ToArray();
        }
    }
}