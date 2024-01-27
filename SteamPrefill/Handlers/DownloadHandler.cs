﻿namespace SteamPrefill.Handlers
{
    public sealed class DownloadHandler : IDisposable
    {
        private readonly IAnsiConsole _ansiConsole;
        private readonly CdnPool _cdnPool;
        private readonly HttpClient _client;

        /// <summary>
        /// The URL/IP Address where the Lancache has been detected.
        /// </summary>
        private string _lancacheAddress;

        public DownloadHandler(IAnsiConsole ansiConsole, CdnPool cdnPool)
        {
            _ansiConsole = ansiConsole;
            _cdnPool = cdnPool;

            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "Valve/Steam HTTP Client 1.0");
        }

        public async Task InitializeAsync()
        {
            if (_lancacheAddress == null)
            {
                _lancacheAddress = await LancacheIpResolver.ResolveLancacheIpAsync(_ansiConsole, AppConfig.SteamTriggerDomain);
            }
        }

        /// <summary>
        /// Attempts to download all queued requests.  If all downloads are successful, will return true.
        /// In the case of any failed downloads, the failed downloads will be retried up to 3 times.  If the downloads fail 3 times, then
        /// false will be returned
        /// </summary>
        /// <returns>True if all downloads succeeded.  False if any downloads failed 3 times in a row.</returns>
        public async Task<bool> DownloadQueuedChunksAsync(List<QueuedRequest> queuedRequests, DownloadArguments downloadArgs)
        {
            await InitializeAsync();

            int retryCount = 0;
            var failedRequests = new ConcurrentBag<QueuedRequest>();
            await _ansiConsole.CreateSpectreProgress(downloadArgs.TransferSpeedUnit).StartAsync(async ctx =>
            {
                // Run the initial download
                failedRequests = await AttemptDownloadAsync(ctx, "Downloading..", queuedRequests, downloadArgs);

                // Handle any failed requests
                while (failedRequests.Any() && retryCount < 2)
                {
                    retryCount++;
                    await Task.Delay(2000 * retryCount);
                    failedRequests = await AttemptDownloadAsync(ctx, $"Retrying  {retryCount}..", failedRequests.ToList(), downloadArgs, forceRecache: true);
                }
            });

            // Handling final failed requests
            if (failedRequests.IsEmpty)
            {
                return true;
            }

            _ansiConsole.MarkupLine(Red($"{failedRequests.Count} failed downloads"));
            return false;
        }

        //TODO I don't like the number of parameters here, should maybe rethink the way this is written.
        /// <summary>
        /// Attempts to download the specified requests.  Returns a list of any requests that have failed for any reason.
        /// </summary>
        /// <param name="forceRecache">When specified, will cause the cache to delete the existing cached data for a request, and redownload it again.</param>
        /// <returns>A list of failed requests</returns>
        public async Task<ConcurrentBag<QueuedRequest>> AttemptDownloadAsync(ProgressContext ctx, string taskTitle, List<QueuedRequest> requestsToDownload,
                                                                                DownloadArguments downloadArgs, bool forceRecache = false)
        {
            double requestTotalSize = requestsToDownload.Sum(e => e.CompressedLength);
            var progressTask = ctx.AddTask(taskTitle, new ProgressTaskSettings { MaxValue = requestTotalSize });

            var failedRequests = new ConcurrentBag<QueuedRequest>();

            var cdnServer = _cdnPool.TakeConnection();
            await Parallel.ForEachAsync(requestsToDownload, new ParallelOptions { MaxDegreeOfParallelism = downloadArgs.MaxConcurrentRequests }, body: async (request, _) =>
            {
                try
                {
                    var url = $"http://{_lancacheAddress}/depot/{request.DepotId}/chunk/{request.ChunkId}";
                    if (forceRecache)
                    {
                        url += "?nocache=1";
                    }
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                    requestMessage.Headers.Host = cdnServer.Host;

                    using var cts = new CancellationTokenSource();
                    using var response = await _client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    using Stream responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
                    response.EnsureSuccessStatusCode();

                    // Don't save the data anywhere, so we don't have to waste time writing it to disk.
                    var buffer = new byte[4096];
                    while (await responseStream.ReadAsync(buffer, cts.Token) != 0)
                    {
                    }
                }
                catch (Exception e)
                {
                    failedRequests.Add(request);
                    _ansiConsole.LogMarkupLine(Red($"Request /depot/{request.DepotId}/chunk/{request.ChunkId} failed : {e.GetType()}"));
                    FileLogger.LogExceptionNoStackTrace($"Request /depot/{request.DepotId}/chunk/{request.ChunkId} failed", e);
                }
                progressTask.Increment(request.CompressedLength);
            });

            //TODO In the scenario where a user still had all requests fail, potentially display a warning that there is an underlying issue
            // Only return the connections for reuse if there were no errors
            if (failedRequests.IsEmpty)
            {
                _cdnPool.ReturnConnection(cdnServer);
            }

            // Making sure the progress bar is always set to its max value, in-case some unexpected error leaves the progress bar showing as unfinished
            progressTask.Increment(progressTask.MaxValue);
            return failedRequests;
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}