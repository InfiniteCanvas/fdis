using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IProducer : IDisposable
    {
        public ValueTask<ReadOnlyMemory<ContentInfo>> GetFiles(string sourceUri, CancellationToken cancellationToken = default);

        public ValueTask<List<Result>> ProvideData(string               sourceUri,
                                                   Channel<ContentInfo> contentChannel,
                                                   CancellationToken    cancellationToken = default);
    }
}
