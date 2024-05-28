using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IProducer : IDisposable, INamed
    {
        public ValueTask<List<Result>> ProvideData(string               sourceUri,
                                                   Channel<ContentInfo> contentChannel,
                                                   CancellationToken    cancellationToken = default);
    }
}
