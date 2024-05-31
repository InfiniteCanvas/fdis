using System.Threading.Channels;

namespace fdis.Utilities
{
    public class Broadcaster
    {
        public static async Task CreateBroadcastChannels<T>(Channel<T>        source,
                                                            Channel<T>[]      receivers,
                                                            CancellationToken cancellationToken = default)
        {
            var unboundedChannelOptions = new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true
            };
            for (var i = 0; i < receivers.Length; i++)
                receivers[i] = Channel.CreateUnbounded<T>(unboundedChannelOptions);

            await foreach (var item in source.Reader.ReadAllAsync(cancellationToken))
            {
                for (var index = 0; index < receivers.Length; index++)
                {
                    var channel = receivers[index];
                    await channel.Writer.WriteAsync(item, cancellationToken);
                }
            }

            foreach (var receiver in receivers)
                receiver.Writer.Complete();
        }
    }
}
