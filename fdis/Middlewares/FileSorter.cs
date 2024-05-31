using System.Text.RegularExpressions;
using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Middlewares
{
    public class FileSorter(ILogger<FileSorter> logger, IHostApplicationLifetime hostApplicationLifetime) : IMiddleWare
    {
        private Regex  _regex;
        private string _subfolder;

        public void Dispose() { }

        public string Name => $"{nameof(FileSorter)}";

        public IMiddleWare Configure(Dictionary<string, string> options)
        {
            if (!options.TryGetValue("Regex", out var regex))
            {
                logger.ZLogCritical($"No regex specified in {Name}. Exiting.");
                hostApplicationLifetime.StopApplication();
                return this;
            }

            if (!options.TryGetValue("Subfolder", out var subfolder))
            {
                logger.ZLogCritical($"No subfolder specified in {Name}. Exiting.");
                hostApplicationLifetime.StopApplication();
                return this;
            }

            _regex = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _subfolder = subfolder;

            return this;
        }

        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var results = new List<Result>();
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var fileRelativePath = contentInfo.FolderRelativeToSource.Combine(contentInfo.FileName);
                if (!_regex.IsMatch(fileRelativePath))
                {
                    logger.ZLogDebug($"{fileRelativePath} did not match {_regex}, subfolder unchanged");
                    await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
                    continue;
                }

                var c = contentInfo with { FolderRelativeToSource = _subfolder };
                logger.ZLogDebug($"{fileRelativePath} matched {_regex}, subfolder changed");
                results.Add(Result.Success($"Changed subfolder of {c.FileName} from {contentInfo.FolderRelativeToSource} to {c.FolderRelativeToSource}"));
                await targetChannel.Writer.WriteAsync(c, cancellationToken);
            }

            targetChannel.Writer.Complete();
            logger.ZLogInformation($"Sorted {results.Count} into {_subfolder}");

            return results;
        }

        public override string ToString() { return $"{Name}[{_regex}]/[{_subfolder}]"; }
    }
}