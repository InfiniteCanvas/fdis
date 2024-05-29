using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Consumers
{
    public class FileWriter(ILogger<FileWriter> logger) : IConsumer
    {
        private const int    _DEFAULT_BUFFER_SIZE = 81920;
        private       int    _bufferSize          = _DEFAULT_BUFFER_SIZE;
        private       string _saveFolder          = Directory.GetCurrentDirectory().Combine("output");

        public IConsumer Configure(Dictionary<string, string> options)
        {
            _saveFolder = options["SaveFolder"];
            if (!options.ContainsKey("BufferSize") || !int.TryParse(options["BufferSize"], out _bufferSize))
            {
                logger.ZLogWarning($"BufferSize not set for {Name}, defaulting to 81920");
                _bufferSize = _DEFAULT_BUFFER_SIZE;
            }

            return this;
        }

        public async ValueTask<List<Result>> ConsumeData(Channel<ContentInfo> contentChannel, CancellationToken cancellationToken = default)
        {
            var results = new List<Result>();
            await foreach (var contentInfo in contentChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var result = await Consume(contentInfo, cancellationToken);
                results.Add(result);
            }

            logger.ZLogInformation($"Wrote {results.Count} files to {_saveFolder}");
            return results;
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }

        public string Name => nameof(FileWriter);

        private async ValueTask<Result> Consume(ContentInfo contentInfo, CancellationToken cancellationToken = default)
        {
            var saveFolder = Path.Combine(_saveFolder, contentInfo.FolderRelativeToSource);
            logger.ZLogDebug($"Consuming {contentInfo.FileName}[{contentInfo.FilePath}]");
            if (!File.Exists(contentInfo.FilePath))
                return Result.Failure($"{contentInfo.FilePath} doesn't exist");

            var savePath = Path.Combine(saveFolder, contentInfo.FileName).GetFullPath();
            Directory.CreateDirectory(saveFolder);

            await using (var sourceStream = File.OpenRead(contentInfo.FilePath))
            {
                await using (var destinationStream = File.Create(savePath))
                    await sourceStream.CopyToAsync(destinationStream, _bufferSize, cancellationToken);
            }

            return Result.Success($"{contentInfo.FilePath} copied to {savePath}");
        }
    }
}
