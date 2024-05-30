using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Middlewares
{
    public class FilePathCollisionSolver(ILogger<FilePathCollisionSolver> logger) : IMiddleWare
    {
        private bool _rename;

        public void Dispose() { }

        public string Name => nameof(FilePathCollisionSolver);

        public IMiddleWare Configure(Dictionary<string, string> options)
        {
            if (!options.TryGetValue("Mode", out var mode))
            {
                logger.ZLogWarning($"Mode not set in {Name}, defaulting to 'Remove'");
                mode = "Remove";
            }

            _rename = mode.Equals("Rename", StringComparison.InvariantCultureIgnoreCase);

            return this;
        }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var set = new SortedSet<ContentInfo>(new ContentInfo.PathSorter());
            var collisions = 0;

            await foreach (var content in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!set.Add(content))
                {
                    collisions++;
                    if (_rename)
                    {
                        var renamed =
                            $"{Path.GetFileNameWithoutExtension(content.FileName)}{Path.GetRandomFileName()}.{Path.GetExtension(content.FileName)}";
                        logger.ZLogInformation($"{content.FileName} collided, renaming it to {renamed}");
                        await targetChannel.Writer.WriteAsync(content with { FileName = renamed }, cancellationToken);
                    }
                    else
                        logger.ZLogWarning($"{content.FileName} collided, removing it");
                }
                else
                {
                    logger.ZLogDebug($"{content.FileName} added");
                    await targetChannel.Writer.WriteAsync(content, cancellationToken);
                }
            }

            targetChannel.Writer.Complete();
            return [Result.Success($"{set.Count + collisions} files processed, {collisions} solved")];
        }
    }
}
