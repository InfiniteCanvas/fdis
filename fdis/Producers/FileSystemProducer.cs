using fdis.Interfaces;

namespace fdis.Producers
{
    public class FileSystemProducer : IProducer
    {
        public void Dispose() { }

        public ValueTask<Memory<ContentInfo>> GetFileData(string sourceUri, CancellationToken cancellationToken = default)
        {
            if (!Path.Exists(sourceUri))
            {
                Console.WriteLine($"Couldn't find {sourceUri}");
                return ValueTask.FromResult(new Memory<ContentInfo>([]));
            }

            var filePaths = Directory.GetFiles(sourceUri, "*", SearchOption.AllDirectories);
            Console.WriteLine(string.Join(", ", filePaths));
            Memory<ContentInfo> result = new ContentInfo[filePaths.Length];

            for (var index = 0; index < filePaths.Length; index++)
            {
                var filePath = filePaths[index];
                Console.WriteLine($"Content found {filePath}");
                result.Span[index] = new ContentInfo
                {
                    Path = filePath,
                    FileName = Path.GetFileName(filePath),
                    Size = new FileInfo(filePath).Length,
                    FileExtension = Path.GetExtension(filePath)
                };
            }

            return ValueTask.FromResult(result);
        }

        public async ValueTask<Memory<byte>> GetData(ContentInfo contentInfo, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(contentInfo.Path))
                return new Memory<byte>([]);

            return new Memory<byte>(await File.ReadAllBytesAsync(contentInfo.Path, cancellationToken));
        }
    }
}
