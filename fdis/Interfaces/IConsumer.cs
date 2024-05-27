using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IConsumer
    {
        public ValueTask<Result> Consume(ContentInfo contentInfos, CancellationToken cancellationToken = default);

        public ValueTask<List<Result>> ConsumeData(Channel<ContentInfo> contentChannel, CancellationToken cancellationToken = default);
    }
}
