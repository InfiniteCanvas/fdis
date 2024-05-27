using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace fdis.Middlewares
{
    public class FileCompressor : IMiddleWare
    {
        public async ValueTask<ReadOnlyMemory<ContentInfo>> ProcessFiles(ReadOnlyMemory<ContentInfo> files,
                                                                         CancellationToken           cancellationToken = default)
        {
            using var archive = ZipArchive.Create();
            var archivePath = Path.Combine(Directory.CreateTempSubdirectory().FullName, "archive.zip");
            await using var archiveFile = File.Create(archivePath);
            for (var index = 0; index < files.Span.Length; index++)
            {
                var file = files.Span[index];
                if (!Path.Exists(file.Path))
                    continue;
                await using var f = File.OpenRead(file.Path);
                archive.AddEntry($"{index}{file.FileName}", f, f.Length);
            }

            archive.SaveTo(archiveFile);

            return new ReadOnlyMemory<ContentInfo>([new ContentInfo { Path = archivePath, FileExtension = "zip", FileName = "archive.zip" }]);
        }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var results = new List<Result>();
            using var archive = ZipArchive.Create();
            // var archivePath = Path.Combine(Directory.CreateTempSubdirectory().FullName, "archive.zip");
            var archivePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            Console.WriteLine(archivePath);

            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!Path.Exists(contentInfo.Path))
                    continue;
                archive.AddEntry(contentInfo.FileName, contentInfo.Path);
                results.Add(Result.Success($"{contentInfo} added to archive"));
            }

            archive.SaveTo(archivePath, new ZipWriterOptions(CompressionType.LZMA));
            await targetChannel.Writer.WriteAsync(new ContentInfo
                                                  {
                                                      Path = archivePath,
                                                      FileExtension = "zip",
                                                      FileName = "archive.zip",
                                                      Size = new FileInfo(archivePath).Length
                                                  },
                                                  cancellationToken);
            targetChannel.Writer.Complete();

            return results;
        }
    }
}
