using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IProvider : IDisposable, INamed
    {
        public IProvider Configure(Dictionary<string, string> options);

        public ValueTask<List<Result>> ProvideData(Channel<ContentInfo> sourceChannel,
                                                   Channel<ContentInfo> targetChannel,
                                                   CancellationToken    cancellationToken = default);
    }
}
