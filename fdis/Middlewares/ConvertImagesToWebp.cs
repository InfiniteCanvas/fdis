using System.Text.RegularExpressions;
using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using ZLogger;

namespace fdis.Middlewares
{
    public class ConvertImagesToWebp(ILogger<ConvertImagesToWebp> logger) : IMiddleWare
    {
        private readonly List<FileInfo> _tempFiles = new();
        private          WebpEncoder    _encoder;
        private          int            _quality;
        private          Regex          _regex;

        public void Dispose()
        {
            foreach (var tempFile in _tempFiles)
            {
                logger.ZLogDebug($"Cleaning up {tempFile}");
                tempFile.Delete();
            }
        }

        public string Name => nameof(ConvertImagesToWebp);

        public IMiddleWare Configure(Dictionary<string, string> options)
        {
            if (!options.TryGetValue("Regex", out var pattern))
            {
                pattern = @".*\.(png|bmp)";
                logger.ZLogWarning($"No regex set, defaulting to {pattern}");
            }

            if (!options.TryGetValue("Mode", out var mode))
            {
                mode = "Lossy";
                logger.ZLogWarning($"No mode set, defaulting to {mode}");
            }

            if (!options.TryGetValue("Quality", out var quality) || !int.TryParse(quality, out _quality))
            {
                _quality = 0;
                logger.ZLogWarning($"No quality set, defaulting to {_quality}");
            }

            _encoder = mode.Equals("Lossy") switch
            {
                true  => new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = _quality },
                false => new WebpEncoder { FileFormat = WebpFileFormatType.Lossless, Quality = _quality }
            };
            _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return this;
        }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var results = new List<Result>();
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!_regex.IsMatch(contentInfo.FileName))
                {
                    await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
                    continue;
                }

                try
                {
                    using var image = await Image.LoadAsync(contentInfo.FilePath, cancellationToken);
                    var path = Path.GetTempPath().Combine(Path.GetTempFileName());
                    var renamed = $"{Path.GetFileNameWithoutExtension(contentInfo.FileName)}.webp";

                    await image.SaveAsWebpAsync(path, _encoder, cancellationToken);
                    logger.ZLogDebug($"Converted {contentInfo.FileName} to webp");
                    Result.Success($"Converted {contentInfo.FileName} to webp").AddTo(results);
                    new FileInfo(path).AddTo(_tempFiles);
                    await targetChannel.Writer.WriteAsync(contentInfo with { FileName = renamed, FilePath = path }, cancellationToken);
                }
                catch (Exception e)
                {
                    logger.ZLogError(e, $"Something went wrong trying to convert image at {contentInfo.FilePath}");
                    Result.Failure($"Something went wrong trying to convert image at {contentInfo.FilePath}", e);
                }
            }

            targetChannel.Writer.Complete();
            logger.ZLogInformation($"Converted {results.Count(result => result.Status == Result.ResultStatus.Success)} images to webp {_encoder.FileFormat}[{_quality}]");

            return results;
        }

        public override string ToString() { return $"{Name}[{_regex}][{_encoder.FileFormat.ToString()}:{_quality}]"; }
    }
}
