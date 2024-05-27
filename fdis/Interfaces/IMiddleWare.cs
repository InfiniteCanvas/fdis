using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IMiddleWare
    {
        public ValueTask<ReadOnlyMemory<ContentInfo>> ProcessFiles(ReadOnlyMemory<ContentInfo> files, CancellationToken cancellationToken = default);

        public ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                   Channel<ContentInfo> targetChannel,
                                                   CancellationToken    cancellationToken = default);
    }
}
