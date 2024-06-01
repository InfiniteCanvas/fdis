using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using fdis.Data;
using fdis.Data.gofile;
using fdis.Interfaces;
using fdis.Utilities;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace fdis.Consumers
{
    public class GofileUploader(ILogger<GofileUploader> logger, IHttpClientFactory httpClientFactory) : IConsumer
    {
        private static readonly TimedRateLimiter           _timedRateLimiter      = new();
        private static readonly JsonSerializerOptions      _jsonSerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly        HttpClient                 _httpClient            = httpClientFactory.CreateClient("Gofile");
        private readonly        Dictionary<string, string> _subfolders            = new();
        private                 string?                    _accountId;
        private                 string                     _dataDownloadPage;
        private                 string?                    _folderId;
        private                 Uri                        _serverUri;

        public void Dispose() { _httpClient.Dispose(); }

        public string Name => nameof(GofileUploader);

        public IConsumer Configure(Dictionary<string, string> options)
        {
            _folderId = options.GetValueOrDefault("FolderId",   null);
            _accountId = options.GetValueOrDefault("AccountId", null);

            if (_accountId != null)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accountId);

            return this;
        }

        public async ValueTask<List<Result>> ConsumeData(Channel<ContentInfo> contentChannel, CancellationToken cancellationToken = default)
        {
            var results = new List<Result>();

            await FirstUpload(cancellationToken);

            await foreach (var contentInfo in contentChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var result = await UploadFile(contentInfo, cancellationToken);
                result.AddTo(results);
            }

            Result.Success($"Access downloads from {_dataDownloadPage}").AddTo(results);
            return results;
        }

        private async Task GetAndSetServer(CancellationToken cancellationToken)
        {
            await _timedRateLimiter.WaitUntilAllowed();
            var response = await _httpClient.GetStringAsync(@"https://api.gofile.io/servers", cancellationToken);
            var result = JsonSerializer.Deserialize<ServersResponse>(response);
            logger.ZLogDebug($"Getting server result: {response}");
            var server = result.data.servers.FirstOrDefault(s => s.zone.Equals("eu"));
            server ??= result.data.servers.FirstOrDefault();
            _serverUri = new Uri($"https://{server?.name ?? "store1"}.gofile.io/contents/uploadfile");
            logger.ZLogDebug($"Server set to {_serverUri.ToString()}");
        }

        private async Task<string> GetOrCreateSubfolderId(string subfolderStructure, CancellationToken cancellationToken = default)
        {
            logger.ZLogDebug($"Getting id for subfolder: {subfolderStructure}");
            var trimmed = subfolderStructure.TrimStart('.', Path.DirectorySeparatorChar)
                                            .TrimEnd(Path.DirectorySeparatorChar);
            logger.ZLogDebug($"trimmed subfolder: {trimmed}");
            if (trimmed.IsNullOrWhiteSpace())
                return _folderId;

            var subFolders = trimmed.Split(Path.DirectorySeparatorChar);
            for (var i = 0; i < subFolders.Length; i++)
            {
                var folder = subFolders[i];
                if (!_subfolders.ContainsKey(folder))
                {
                    var body = new
                    {
                        parentFolderId = i switch
                        {
                            0   => _folderId,
                            > 0 => _subfolders[subFolders[i - 1]]
                        },
                        folderName = folder
                    };

                    var responseMessage = await _httpClient.PostAsJsonAsync(@"https://api.gofile.io/contents/createFolder",
                                                                            body,
                                                                            _jsonSerializerOptions,
                                                                            cancellationToken);
                    var responseBody = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
                    logger.ZLogDebug($"Folder creation response: {responseBody}");
                    var data = JsonSerializer.Deserialize<CreateFolderResponse>(responseBody, _jsonSerializerOptions);

                    if (data != null)
                        _subfolders.Add(folder, data.Data.FolderId);
                    else
                        return _folderId;
                }
            }

            return _subfolders[subFolders[^1]];
        }

        private async Task FirstUpload(CancellationToken cancellationToken)
        {
            await GetAndSetServer(cancellationToken);

            using var formContent = new MultipartFormDataContent();
            if (_folderId != null)
                formContent.Add(new StringContent(_folderId), "folderId");
            // formContent.Add(new StreamContent(contentInfo.FileInfo.OpenRead()), "file", contentInfo.FileName);
            formContent.Add(new StringContent("Gofile is good"), "file", "ayo.txt");
            var response = await _httpClient.PostAsync(_serverUri, formContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = responseBody;
                var uploadResponse = JsonSerializer.Deserialize<UploadFileResponse>(json);
                Debug.Assert(uploadResponse != null, nameof(uploadResponse) + " != null");
                _accountId = uploadResponse?.data.guestToken;
                _folderId = uploadResponse?.data.parentFolder;
                logger.ZLogDebug($"first upload result: {json}");
                logger.ZLogDebug($"Set token to {_accountId}, folder id to {_folderId}");

                if (_accountId != null)
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accountId);

                Debug.Assert(uploadResponse != null, nameof(uploadResponse) + " != null");
                _dataDownloadPage = uploadResponse.data.downloadPage;
                logger.ZLogInformation($"Uploading files to {_dataDownloadPage}");
            }

            logger.ZLogError($"[{response.StatusCode.ToString()}] {responseBody}");
        }

        private async Task<Result> UploadFile(ContentInfo contentInfo, CancellationToken cancellationToken)
        {
            using var formContent = new MultipartFormDataContent();
            logger.ZLogDebug($"Uploading {contentInfo}");

            Debug.Assert(_folderId != null, nameof(_folderId) + " should be set in GofileUploader after the first upload");

            var folder = await GetOrCreateSubfolderId(contentInfo.FolderRelativeToSource, cancellationToken);
            formContent.Add(new StringContent(folder),                          "folderId");
            formContent.Add(new StreamContent(contentInfo.FileInfo.OpenRead()), "file", contentInfo.FileName);
            var response = await _httpClient.PostAsync(_serverUri, formContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.ZLogDebug($"{responseString}");
            if (response.IsSuccessStatusCode)
            {
                var json = responseString;
                var uploadResponse = JsonSerializer.Deserialize<UploadFileResponse>(json);

                return Result.Success($"Uploaded to: {uploadResponse?.data.downloadPage}");
            }

            logger.ZLogError($"{contentInfo} | {responseString}");
            return Result.Failure($"[{response.StatusCode.ToString()}] {responseString}");
        }
    }
}
