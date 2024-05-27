namespace fdis.Interfaces
{
    public interface IProducer : IDisposable
    {
        public ValueTask<ReadOnlyMemory<ContentInfo>> GetFiles(string sourceUri, CancellationToken cancellationToken = default);
    }
}
