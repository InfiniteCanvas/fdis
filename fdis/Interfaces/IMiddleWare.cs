namespace fdis.Interfaces
{
    public interface IMiddleWare
    {
        public ValueTask<ReadOnlyMemory<ContentInfo>> ProcessFiles(ReadOnlyMemory<ContentInfo> files, CancellationToken cancellationToken = default);
    }
}
