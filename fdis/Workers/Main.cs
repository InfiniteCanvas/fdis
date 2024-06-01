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
        private          IProvider[]   _providers   = [];

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.ZLogInformation($"Starting File Distributor (fdis)..");
            logger.ZLogInformation($"Threads: [{_settings.Threads}]");

            SetupComponents();
            var unboundedChannelOptions = new UnboundedChannelOptions { SingleReader = false, SingleWriter = true };
            var tasks = new List<Task>();

            // provider
            var emptyChannel = Channel.CreateUnbounded<ContentInfo>();
            emptyChannel.Writer.Complete();
            var sourceChannels = new Channel<ContentInfo>[_providers.Length];
            for (var index = 0; index < _providers.Length; index++)
            {
                var i = index;
                var provider = _providers[index];
                sourceChannels[i] = Channel.CreateUnbounded<ContentInfo>(unboundedChannelOptions);
                if (index == 0)
                {
                    Task.Run(async () =>
                             {
                                 logger.ZLogInformation($"Setting up initial {provider.Name}");
                                 foreach (var result in await provider.ProvideData(emptyChannel, sourceChannels[i], stoppingToken))
                                     logger.ZLogDebug($"{provider.Name}[{result.Status.ToString()}]: {result.Info}");
                             },
                             stoppingToken)
                        .AddTo(tasks);
                }
                else
                {
                    Task.Run(async () =>
                             {
                                 logger.ZLogInformation($"Feeding {_providers[i - 1].Name} into {_providers[i].Name}");
                                 foreach (var result in await provider.ProvideData(sourceChannels[i - 1], sourceChannels[i], stoppingToken))
                                     logger.ZLogDebug($"{provider.Name}[{result.Status.ToString()}]: {result.Info}");
                             },
                             stoppingToken)
                        .AddTo(tasks);
                }
            }

            var sourceChannel = sourceChannels[^1];

            // middlewares
            var middleChannels = new Channel<ContentInfo>[_middlewares.Length + 1];
            var middlewareCounter = 0;
            middleChannels[0] = sourceChannel;
            foreach (var middleware in _middlewares)
            {
                middlewareCounter++;
                if (middlewareCounter == 1)
                    logger.ZLogInformation($"Feeding source {_providers[^1].Name} into {middleware.Name}");
                else
                    logger.ZLogInformation($"Feeding middleware {_middlewares[middlewareCounter - 2]} into {middleware}");

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

            var processedChannel = middleChannels[^1];

            // consumers
            var consumerChannels = new Channel<ContentInfo>[_consumers.Length];
            ChannelUtils.Broadcast(processedChannel, consumerChannels, stoppingToken).AddTo(tasks);
            for (var index = 0; index < _consumers.Length; index++)
            {
                var consumer = _consumers[index];
                var consumerChannel = consumerChannels[index];
                Task.Run(async () =>
                         {
                             foreach (var result in await consumer.ConsumeData(consumerChannel, stoppingToken)
                                                                  .ConfigureAwait(false))
                                 logger.ZLogDebug($"[{consumer.Name}]{result.Status.ToString()}: {result.Info}");
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
            // providers
            var providers = new List<IProvider>();
            foreach (var providerOptions in _settings.Providers)
            {
                var provider = serviceProvider.GetKeyedService<IProvider>(providerOptions.Type);
                provider?.Configure(providerOptions.Options).AddTo(providers);
            }

            _providers = providers.ToArray();

            if (_providers.Length == 0)
            {
                logger.ZLogCritical($"No providers found");
                hostApplicationLifetime.StopApplication();
                return;
            }

            // consumers
            var consumers = new List<IConsumer>();
            foreach (var consumerOptions in _settings.Consumers)
            {
                var consumer = serviceProvider.GetKeyedService<IConsumer>(consumerOptions.Type);
                consumer?.Configure(consumerOptions.Options).AddTo(consumers);
            }

            _consumers = consumers.ToArray();

            if (_consumers.Length == 0)
            {
                logger.ZLogCritical($"No consumers found");
                hostApplicationLifetime.StopApplication();
                return;
            }

            // middlewares
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
