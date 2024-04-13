using fdis.Data;
using fdis.Interfaces;

namespace fdis.Consumers
{
    public class FileSystemConsumer : IConsumer
    {
        /// <summary>
        ///     For a file stream, the buffer size should ideally be a multiple of the disk sector size (commonly 4096 bytes), and large enough to hold at least
        ///     a few sectors.
        ///     However, modern drives and operating systems may perform their own buffering and read-ahead, so again, larger values can be used to reduce the
        ///     number of I/O operations.
        /// </summary>
        public int bufferSize = 4096;

        public async ValueTask<Result> Consume(ContentInfo       contentInfo,
                                               Memory<byte>      content,
                                               CancellationToken cancellationToken = default)
        {
            try
            {
                var path = Path.Combine("D:\\Repos\\C#\\fdis\\test", contentInfo.FileName);
                Console.WriteLine($"Writing to {path}");
                await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
                return new Result { Status = Result.ResultStatus.Success, Info = "File written successfully" };
            }
            catch (Exception e)
            {
                return new Result { Status = Result.ResultStatus.Error, Info = $"Error while writing files\n{e}" };
            }
        }
    }
}
