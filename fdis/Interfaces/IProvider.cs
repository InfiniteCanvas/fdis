using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IProvider : IDisposable, INamed
    {
        public IProvider Configure(Dictionary<string, string> options);

        public ValueTask<List<Result>> ProvideData(string               sourceUri,
                                                   Channel<ContentInfo> contentChannel,
                                                   CancellationToken    cancellationToken = default);
    }
}