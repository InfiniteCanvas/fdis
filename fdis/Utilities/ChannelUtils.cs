using System.Threading.Channels;

namespace fdis.Utilities
{
    public static class ChannelUtils
    {
        public static async Task Broadcast<T>(Channel<T>        source,
                                              Channel<T>[]      receivers,
                                              CancellationToken cancellationToken = default)
        {
            var unboundedChannelOptions = new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true
            };
            for (var i = 0; i < receivers.Length; i++)
                receivers[i] = Channel.CreateUnbounded<T>(unboundedChannelOptions);

            await foreach (var item in source.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                for (var index = 0; index < receivers.Length; index++)
                {
                    var channel = receivers[index];
                    await channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var receiver in receivers)
                receiver.Writer.Complete();
        }

        public static async Task Funnel<T>(Channel<T>[]      sources,
                                           Channel<T>        receiver,
                                           CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();
            foreach (var source in sources)
            {
                Task.Run(async () =>
                         {
                             await foreach (var item in source.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                                 await receiver.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                         },
                         cancellationToken)
                    .AddTo(tasks);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            receiver.Writer.Complete();
        }
    }
}