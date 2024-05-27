using fdis.Interfaces;

namespace fdis.Producers
{
    public class FileReader : IProducer
    {
        public void Dispose() { }

        public ValueTask<ReadOnlyMemory<ContentInfo>> GetFiles(string sourceUri, CancellationToken cancellationToken = default)
        {
            if (!Path.Exists(sourceUri))
            {
                Console.WriteLine($"Couldn't find {sourceUri}");
                return ValueTask.FromResult(new ReadOnlyMemory<ContentInfo>([]));
            }

            var offset = Path.GetFullPath(sourceUri).Length + 1;
            var filePaths = Directory.GetFiles(sourceUri, "*", SearchOption.AllDirectories);
            Console.WriteLine(string.Join(", ", filePaths));
            var result = new ContentInfo[filePaths.Length];

            for (var index = 0; index < filePaths.Length; index++)
            {
                var filePath = filePaths[index];
                Console.WriteLine($"Content found {filePath}");
                result[index] = new ContentInfo
                {
                    Path = filePath,
                    FileName = filePath[offset..],
                    Size = new FileInfo(filePath).Length,
                    FileExtension = Path.GetExtension(filePath)
                };
            }

            return ValueTask.FromResult(new ReadOnlyMemory<ContentInfo>(result));
        }
    }
}
