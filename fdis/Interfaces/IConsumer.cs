using System.Threading.Channels;
using fdis.Data;

namespace fdis.Interfaces
{
    public interface IConsumer : IDisposable, INamed
    {
        public IConsumer Configure(Dictionary<string, string> options);

        public ValueTask<List<Result>> ConsumeData(Channel<ContentInfo> contentChannel, CancellationToken cancellationToken = default);
    }
}