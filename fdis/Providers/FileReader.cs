using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Providers
{
    public class FileReader(ILogger<FileReader> logger) : IProvider
    {
        public void Dispose() { }

        public IProvider Configure(Dictionary<string, string> options) { return this; }

        public async ValueTask<List<Result>> ProvideData(string               sourceUri,
                                                         Channel<ContentInfo> producerChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            if (!Path.Exists(sourceUri))
                return [Result.Failure($"Couldn't find {sourceUri}")];

            if (Directory.Exists(sourceUri))
                return await ProcessDirectory(sourceUri, producerChannel, cancellationToken);


            var content = new ContentInfo
            {
                FilePath = sourceUri, FileName = Path.GetFileName(sourceUri), FolderRelativeToSource = "", Size = new FileInfo(sourceUri).Length
            };
            await producerChannel.Writer.WriteAsync(content, cancellationToken);

            return [Result.Success($"Read file: {content.FilePath}")];
        }

        public string Name => nameof(FileReader);

        private async ValueTask<List<Result>> ProcessDirectory(string               sourceUri,
                                                               Channel<ContentInfo> producerChannel,
                                                               CancellationToken    cancellationToken)
        {
            var filePaths = Directory.GetFiles(sourceUri, "*", SearchOption.AllDirectories);
            var results = new List<Result>(filePaths.Length);
            logger.ZLogTrace($"{string.Join(", ", filePaths)}");

            foreach (var filePath in filePaths)
            {
                var folderRelativeToSource = sourceUri == Path.GetDirectoryName(filePath)
                    ? ""
                    : Path.GetRelativePath(sourceUri, Path.GetDirectoryName(filePath) ?? filePath);
                var contentInfo = new ContentInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Size = new FileInfo(filePath).Length,
                    FolderRelativeToSource = folderRelativeToSource
                };
                await producerChannel.Writer.WriteAsync(contentInfo,
                                                        cancellationToken);
                logger.ZLogDebug($"Content read {contentInfo.FilePath}");
                results.Add(Result.Success($"Content found {contentInfo.FilePath}"));
            }

            producerChannel.Writer.Complete();
            logger.ZLogInformation($"Read {filePaths.Length} items from {sourceUri}");
            return results;
        }
    }
}
