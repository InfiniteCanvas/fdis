using fdis.Interfaces;
using SharpCompress.Archives.Zip;

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
    }
}
