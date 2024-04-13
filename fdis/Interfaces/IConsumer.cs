using fdis.Data;

namespace fdis.Interfaces
{
    public interface IConsumer
    {
        public ValueTask<Result> Consume(ContentInfo contentInfos, Memory<byte> content, CancellationToken cancellationToken);
    }
}
