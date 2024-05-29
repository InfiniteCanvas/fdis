using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IMiddleWare : IDisposable, INamed
    {
        public IMiddleWare Configure(Dictionary<string, string> options);

        public ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                   Channel<ContentInfo> targetChannel,
                                                   CancellationToken    cancellationToken = default);
    }
}