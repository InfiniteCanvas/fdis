using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Providers
{
    public class FileReader(ILogger<FileReader> logger, IHostApplicationLifetime hostApplicationLifetime) : IProvider
    {
        private bool    _deduplicate;
        private string? _source;

        public void Dispose() { }

        public IProvider Configure(Dictionary<string, string> options)
        {
            if (!options.TryGetValue("Source", out _source))
            {
                logger.ZLogCritical($"Source is not set for {Name}, aborting.");
                hostApplicationLifetime.StopApplication();
                return this;
            }

            var mode = options.GetValueOrDefault("Mode", "Deduplicate");
            _deduplicate = mode.Equals("Deduplicate", StringComparison.InvariantCultureIgnoreCase);

            return this;
        }

        public async ValueTask<List<Result>> ProvideData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var set = new SortedSet<ContentInfo>(new ContentInfo.DedupeComparer());
            var fileCounter = 0;
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                fileCounter++;
                if (_deduplicate)
                    set.Add(contentInfo);
                await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
            }

            logger.ZLogDebug($"Added {fileCounter} files from source channel to target channel");

            if (!Path.Exists(_source))
                return [Result.Failure($"Couldn't find {_source}")];

            if (Directory.Exists(_source))
                return await ProcessDirectory(_source, targetChannel, set, cancellationToken);


            var content = new ContentInfo
            {
                FilePath = _source, FileName = Path.GetFileName(_source), FolderRelativeToSource = "", Size = new FileInfo(_source).Length
            };
            await targetChannel.Writer.WriteAsync(content, cancellationToken);

            return [Result.Success($"Read file: {content.FilePath}")];
        }

        public string Name => $"{nameof(FileReader)}[{_source}]";

        private async ValueTask<List<Result>> ProcessDirectory(string                 sourceUri,
                                                               Channel<ContentInfo>   contentChannel,
                                                               SortedSet<ContentInfo> dedupeSet,
                                                               CancellationToken      cancellationToken)
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
                if (_deduplicate && !dedupeSet.Add(contentInfo))
                {
                    logger.ZLogDebug($"Skipping duplicate file: {contentInfo.FilePath}");
                    results.Add(Result.Failure($"Skipping duplicate file: {contentInfo.FilePath}"));
                    continue;
                }

                await contentChannel.Writer.WriteAsync(contentInfo,
                                                       cancellationToken);
                logger.ZLogDebug($"Content read {contentInfo.FilePath}");
                results.Add(Result.Success($"Content found {contentInfo.FilePath}"));
            }

            contentChannel.Writer.Complete();
            logger.ZLogInformation($"Read {filePaths.Length} items from {sourceUri}, discarded {results.Count(result => result.Status == Result.ResultStatus.Failure)} items");
            return results;
        }
    }
}
