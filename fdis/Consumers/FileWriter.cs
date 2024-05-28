using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Consumers
{
    public class FileWriter(ILogger<FileWriter> logger, AppSettings settings) : IConsumer
    {
        public async ValueTask<Result> Consume(ContentInfo contentInfos, CancellationToken cancellationToken = default)
        {
            var saveFolder = Path.Combine(settings.SaveFolder, contentInfos.FolderRelativeToSource);
            logger.ZLogDebug($"Consuming {contentInfos.FileName}[{contentInfos.FilePath}]");
            if (!File.Exists(contentInfos.FilePath))
                return Result.Error($"{contentInfos.FilePath} doesn't exist");

            var data = await File.ReadAllBytesAsync(contentInfos.FilePath, cancellationToken);
            var savePath = Path.Combine(saveFolder, contentInfos.FileName).GetFullPath();
            Directory.CreateDirectory(saveFolder);
            await File.WriteAllBytesAsync(savePath, data, cancellationToken);

            return Result.Success($"{contentInfos.FilePath} copied to {savePath}");
        }

        public async ValueTask<List<Result>> ConsumeData(Channel<ContentInfo> contentChannel, CancellationToken cancellationToken = default)
        {
            var results = new List<Result>();
            await foreach (var contentInfo in contentChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var result = await Consume(contentInfo, cancellationToken);
                results.Add(result);
            }

            logger.ZLogInformation($"Wrote {results.Count} files to {settings.SaveFolder}");
            return results;
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }

        public string Name => nameof(FileWriter);
    }
}
