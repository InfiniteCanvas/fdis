using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Middlewares
{
    public class DeduplicateFiles(ILogger<DeduplicateFiles> logger) : IMiddleWare
    {
        private int _bufferSize;
        private int _scans;

        public void Dispose() { }

        public string Name => nameof(DeduplicateFiles);

        public IMiddleWare Configure(Dictionary<string, string> options)
        {
            if (!options.TryGetValue("BufferSize", out var bufferSizeString) || !int.TryParse(bufferSizeString, out _bufferSize))
            {
                _bufferSize = 64;
                logger.ZLogWarning($"Defaulting BufferSize to {_bufferSize}");
            }

            if (!options.TryGetValue("BufferSize", out var scansString) || !int.TryParse(scansString, out _scans))
            {
                _scans = 5;
                logger.ZLogWarning($"Defaulting Scans to {_scans}");
            }

            return this;
        }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            // dedupe by sorting by size and checking same file sizes with small probes (64 bytes or something)
            var set = new SortedSet<ContentInfo>(new ContentInfo.DedupeComparer(_bufferSize, _scans));
            var dupes = 0;
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (set.Add(contentInfo))
                    await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
                else
                {
                    dupes++;
                    logger.ZLogWarning($"Duplicate found, removing {contentInfo}");
                }
            }

            targetChannel.Writer.Complete();

            return [Result.Success($"Processed {set.Count + dupes}, removed {dupes} duplicates")];
        }
    }
}
