using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;

namespace fdis.Middlewares
{
    public class DeduplicateFiles : IMiddleWare
    {
        public void Dispose()
        {
            // TODO release managed resources here
        }

        public string Name => nameof(DeduplicateFiles);

        public IMiddleWare Configure(Dictionary<string, string> options) { throw new NotImplementedException(); }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            // TODO: dedupe by sorting by size and checking same file sizes with small probes (256 bytes) at random
            var contents = new SortedSet<ContentInfo>();
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
                contents.Add(contentInfo);

            throw new NotImplementedException();
        }
    }
}
