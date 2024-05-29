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
            var set = new SortedSet<ContentInfo>(new DedupeComparer(_bufferSize, _scans));
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

        private class DedupeComparer(int bufferSize = 64, int scans = 5) : IComparer<ContentInfo>
        {
            private readonly long _scans = scans;

            public int Compare(ContentInfo x, ContentInfo y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (ReferenceEquals(null, y))
                    return 1;
                if (ReferenceEquals(null, x))
                    return -1;
                if (x.Size != y.Size)
                    return x.Size.CompareTo(y.Size);

                return CompareFileContents(x, y);
            }

            private int CompareFileContents(ContentInfo x, ContentInfo y)
            {
                using var fx = File.OpenRead(x.FilePath);
                using var fy = File.OpenRead(y.FilePath);

                Span<byte> xBuffer = new byte[bufferSize];
                Span<byte> yBuffer = new byte[bufferSize];
                for (var i = 1; i < _scans; i++)
                {
                    var xbytes = fx.Read(xBuffer);
                    var ybytes = fy.Read(yBuffer);
                    var comp = xbytes.CompareTo(ybytes);
                    if (comp != 0)
                        return comp;

                    if (!xBuffer.SequenceEqual(yBuffer))
                        return xBuffer.SequenceCompareTo(yBuffer);

                    fx.Position = fx.Length / _scans * i;
                    fy.Position = fy.Length / _scans * i;
                }

                return 0;
            }
        }
    }
}
