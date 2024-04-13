namespace fdis.Interfaces
{
    public interface IProducer : IDisposable
    {
        public ValueTask<Memory<ContentInfo>> GetFileData(string sourceUri, CancellationToken cancellationToken = default);

        public ValueTask<Memory<byte>> GetData(ContentInfo contentInfo, CancellationToken cancellationToken = default);
    }
}