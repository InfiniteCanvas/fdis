using System.Text.RegularExpressions;
using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using ZLogger;

namespace fdis.Middlewares
{
    public class FileArchiver(ILogger<FileArchiver> logger) : IMiddleWare
    {
        private string _archiveName;
        private string _archivePath;
        private Regex  _regex;

        public IMiddleWare Configure(Dictionary<string, string> options)
        {
            _regex = new Regex(options["Regex"], RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _archiveName = options["ArchiveName"];
            return this;
        }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var results = new List<Result>();
            using var archive = ZipArchive.Create();
            _archivePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            logger.ZLogInformation($"Archiving items that match {_regex.ToString()}");
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!Path.Exists(contentInfo.FilePath))
                    continue;
                // match file name, since filepath can be other temp archives
                if (!_regex.IsMatch(contentInfo.FolderRelativeToSource.Combine(contentInfo.FileName)))
                {
                    logger.ZLogDebug($"{contentInfo} did not match {_regex.ToString()}, not added to archive");
                    await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
                    continue;
                }

                archive.AddEntry(contentInfo.FolderRelativeToSource.Combine(contentInfo.FileName), contentInfo.FilePath);
                logger.ZLogDebug($"{contentInfo.FolderRelativeToSource.Combine(contentInfo.FileName)} added to archive");
                results.Add(Result.Success($"{contentInfo} added to archive"));
            }

            logger.ZLogInformation($"Writing {archive.Entries.Count} entries to archive [{_archiveName}], compressing {archive.TotalUncompressSize >> 20}MiB..");
            archive.SaveTo(_archivePath, CompressionType.Deflate);
            var archiveInfo = new FileInfo(_archivePath);
            await targetChannel.Writer.WriteAsync(new ContentInfo(FileInfo: new FileInfo(_archivePath),
                                                                  FolderRelativeToSource: @".\",
                                                                  FileName: _archiveName),
                                                  cancellationToken);
            targetChannel.Writer.Complete();
            logger.ZLogInformation($"Compressed {_archiveName} from {archive.TotalUncompressSize >> 20}MiB to {archiveInfo.Length >> 20}MiB");

            return results;
        }

        public void Dispose()
        {
            logger.ZLogInformation($"Cleaning up {Name}, deleting archive at {_archivePath}");
            File.Delete(_archivePath);
        }

        public string Name => nameof(FileArchiver);

        public override string ToString() { return $"{Name}[{_regex}]/[{_archiveName}]"; }
    }
}
