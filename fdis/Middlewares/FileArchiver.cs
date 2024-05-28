using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;
using ZLogger;

namespace fdis.Middlewares
{
    public class FileArchiver(ILogger<FileArchiver> logger) : IMiddleWare
    {
        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var results = new List<Result>();
            using var archive = ZipArchive.Create();
            var archivePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!Path.Exists(contentInfo.FilePath))
                    continue;
                archive.AddEntry(contentInfo.FileName, contentInfo.FilePath);
                logger.ZLogDebug($"[{Name}] {contentInfo} added to archive");
                results.Add(Result.Success($"{contentInfo} added to archive"));
            }

            logger.ZLogInformation($"[{Name}] Writing {archive.Entries.Count} entries to archive, compressing {archive.TotalUncompressSize >> 20}MiB..");
            archive.SaveTo(archivePath, new ZipWriterOptions(CompressionType.LZMA));
            var archiveInfo = new FileInfo(archivePath);
            await targetChannel.Writer.WriteAsync(new ContentInfo
                                                  {
                                                      FilePath = archivePath,
                                                      FolderRelativeToSource = @".\",
                                                      FileName = "archive.zip",
                                                      Size = archiveInfo.Length
                                                  },
                                                  cancellationToken);
            targetChannel.Writer.Complete();
            logger.ZLogInformation($"[{Name}] Compressed {archive.TotalUncompressSize >> 20}MiB to {archiveInfo.Length >> 20}MiB");

            return results;
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }

        public string Name => nameof(FileArchiver);
    }
}
