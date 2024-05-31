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
    public class FileFilter(ILogger<FileFilter> logger, IHostApplicationLifetime hostApplicationLifetime) : IMiddleWare
    {
        private bool _allow;

        private Regex _regex;

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        public string Name => nameof(FileFilter);

        public IMiddleWare Configure(Dictionary<string, string> options)
        {
            if (!options.TryGetValue("Regex", out var regex))
            {
                logger.ZLogCritical($"No regex defined for {Name}, stopping app.");
                hostApplicationLifetime.StopApplication();
                return this;
            }

            if (!options.TryGetValue("Mode", out var mode))
            {
                logger.ZLogWarning($"No mode defined for {Name}, defaulting to 'Allow'");
                mode = "Allow";
            }

            _regex = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _allow = mode.Equals("Allow", StringComparison.InvariantCultureIgnoreCase);

            return this;
        }

        /// <inheritdoc />
        public async ValueTask<List<Result>> ProcessData(Channel<ContentInfo> sourceChannel,
                                                         Channel<ContentInfo> targetChannel,
                                                         CancellationToken    cancellationToken = default)
        {
            var results = new List<Result>();
            await foreach (var contentInfo in sourceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (_regex.IsMatch(contentInfo.FolderRelativeToSource.Combine(contentInfo.FileName)))
                {
                    if (_allow)
                    {
                        logger.ZLogDebug($"{contentInfo} allowed by {this}");
                        await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
                        results.Add(Result.Success($"Allowed file: {contentInfo.FileName}"));
                    }
                    else
                    {
                        logger.ZLogDebug($"{contentInfo} blocked by {this}");
                        results.Add(Result.Failure($"Blocked file: {contentInfo.FileName}"));
                    }
                }
                else
                {
                    if (_allow)
                    {
                        logger.ZLogDebug($"{contentInfo} blocked by {this}");
                        results.Add(Result.Failure($"Blocked file: {contentInfo.FileName}"));
                    }
                    else
                    {
                        logger.ZLogDebug($"{contentInfo} allowed by {this}");
                        await targetChannel.Writer.WriteAsync(contentInfo, cancellationToken);
                        results.Add(Result.Success($"Allowed file: {contentInfo.FileName}"));
                    }
                }
            }

            targetChannel.Writer.Complete();
            logger.ZLogInformation($"Allowed {results.Count(result => result.Status == Result.ResultStatus.Success)}, blocked {results.Count(result => result.Status == Result.ResultStatus.Failure)}");

            return results;
        }

        public override string ToString() { return $"{Name}[{_regex}][{_allow switch { true => "Allow", _ => "Reject" }}]"; }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Free managed resources when Dispose(bool) is called.

                // if there are any object that implements IDisposable,
                // make sure to call their Dispose method here.
                // For example:
                // - close any streams
            }

            // Free unmanaged resources whether Dispose is called or not.
        }
    }
}