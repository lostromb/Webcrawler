using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebCrawler.Mangadex.Converters;
using WebCrawler.Mangadex.Schemas;

namespace WebCrawler.Mangadex.Api
{
    public class MangadexApi
    {
        private static readonly string USER_AGENT = "Polite Mangadex Downloader v3 - loganstromberg@gmail.com";

        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpClient _mainHttpClient;
        private readonly RateLimiter _apiRateLimiter;
        private readonly RateLimiter _imagesRateLimiter;
        private readonly JsonSerializer _jsonSerializer;

        public MangadexApi(IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _httpClientFactory = httpClientFactory.AssertNonNull(nameof(httpClientFactory));
            _logger = logger.AssertNonNull(nameof(logger));
            _mainHttpClient = _httpClientFactory.CreateHttpClient("api.mangadex.org", 443, true, logger.Clone("MangaApiHttpClient"));

            _jsonSerializer = new JsonSerializer();
            _jsonSerializer.Converters.Add(new LanguageCodeConverter());
            _jsonSerializer.Converters.Add(new MangadexEntityConverter());
            _jsonSerializer.Converters.Add(new EntityTypeConverter());
            _jsonSerializer.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            _apiRateLimiter = new RateLimiter(0.5, 20);
            _imagesRateLimiter = new RateLimiter(1, 20);
        }

        public async Task<FeedQueryResponse> GetFeed(FeedQueryParameters queryParams, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _apiRateLimiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
            string baseUrl = string.Format("/manga/{0}/feed", queryParams.MangaId.ToString().ToLowerInvariant());
            HttpRequest request = HttpRequest.CreateOutgoing(baseUrl);
            request.RequestHeaders.Set("User-Agent", USER_AGENT);
            request.GetParameters.Set("offset", queryParams.Offset.ToString(CultureInfo.InvariantCulture));
            request.GetParameters.Set("limit", queryParams.Limit.ToString(CultureInfo.InvariantCulture));
            request.GetParameters.Add("includes[]", "scanlation_group");
            if (queryParams.TranslatedLanguage != null)
            {
                foreach (var lang in queryParams.TranslatedLanguage)
                {
                    request.GetParameters.Add("translatedLanguage[]", lang.ToBcp47Alpha2String());
                }
            }

            request.GetParameters.Add("contentRating[]", "safe");
            request.GetParameters.Add("contentRating[]", "suggestive");
            request.GetParameters.Add("contentRating[]", "erotica");
            request.GetParameters.Add("contentRating[]", "pornographic");
            HttpResponse resp = await _mainHttpClient.SendRequestAsync(request, cancelToken, realTime, _logger).ConfigureAwait(false);
            return await resp.ReadContentAsJsonObjectAsync<FeedQueryResponse>(_jsonSerializer, cancelToken, realTime).ConfigureAwait(false);
        }

        public async Task<AggregateQueryResponse> GetAggregate(AggregateQueryParameters queryParams, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _apiRateLimiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
            string baseUrl = string.Format("/manga/{0}/aggregate", queryParams.MangaId.ToString().ToLowerInvariant());
            HttpRequest request = HttpRequest.CreateOutgoing(baseUrl);
            request.RequestHeaders.Set("User-Agent", USER_AGENT);

            if (queryParams.TranslatedLanguage != null)
            {
                foreach (var lang in queryParams.TranslatedLanguage)
                {
                    request.GetParameters.Add("translatedLanguage[]", lang.ToBcp47Alpha2String());
                }
            }

            HttpResponse resp = await _mainHttpClient.SendRequestAsync(request, cancelToken, realTime, _logger).ConfigureAwait(false);
            return await resp.ReadContentAsJsonObjectAsync<AggregateQueryResponse>(_jsonSerializer, cancelToken, realTime).ConfigureAwait(false);
        }

        public async Task<ChapterQueryResponse> GetChapterInfo(Guid chapterId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _apiRateLimiter.Limit(realTime, cancelToken);
            string baseUrl = string.Format("/chapter/{0}", chapterId.ToString().ToLowerInvariant());
            HttpRequest request = HttpRequest.CreateOutgoing(baseUrl);
            request.RequestHeaders.Set("User-Agent", USER_AGENT);
            HttpResponse resp = await _mainHttpClient.SendRequestAsync(request, cancelToken, realTime, _logger).ConfigureAwait(false);
            return await resp.ReadContentAsJsonObjectAsync<ChapterQueryResponse>(_jsonSerializer, cancelToken, realTime).ConfigureAwait(false);
        }

        public async Task<ChapterImageQueryResponse> GetChapterImageInfo(Guid chapterId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _apiRateLimiter.Limit(realTime, cancelToken);
            string baseUrl = string.Format("/at-home/server/{0}", chapterId.ToString().ToLowerInvariant());
            HttpRequest request = HttpRequest.CreateOutgoing(baseUrl);
            request.RequestHeaders.Set("User-Agent", USER_AGENT);
            HttpResponse resp = await _mainHttpClient.SendRequestAsync(request, cancelToken, realTime, _logger).ConfigureAwait(false);
            return await resp.ReadContentAsJsonObjectAsync<ChapterImageQueryResponse>(_jsonSerializer, cancelToken, realTime).ConfigureAwait(false);
        }

        public async Task<IList<VirtualPath>> DownloadChapterImages(string baseUrl, ChapterImageInfo imageInfo, IFileSystem targetFileSystem, ILogger logger)
        {
            List<VirtualPath> downloadedFiles = new List<VirtualPath>();
            using (IHttpClient httpClient = _httpClientFactory.CreateHttpClient(new Uri(baseUrl), logger.Clone("ChapterImageDownloader")))
            {
                int imageNum = 1;
                foreach (string imageFileName in imageInfo.Data)
                {
                    string targetUrl = string.Format("{0}/data/{1}/{2}", baseUrl, imageInfo.Hash, imageFileName);
                    string fileExtension = imageFileName.Substring(imageFileName.LastIndexOf('.'));
                    VirtualPath downloadedFilePath = new VirtualPath(string.Format("{0:D3}{1}", imageNum++, fileExtension));

                    _imagesRateLimiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    using (HttpRequest request = HttpRequest.CreateOutgoing(targetUrl))
                    {
                        request.RequestHeaders.Set("User-Agent", USER_AGENT);
                        using (HttpResponse response = await httpClient.SendRequestAsync(request))
                        {
                            if (response == null || response.ResponseCode != 200)
                            {
                                logger.Log("Failed to download " + targetUrl, LogLevel.Err);
                                return null;
                            }

                            using (Stream downloadStream = response.ReadContentAsStream())
                            using (Stream fileStream = await targetFileSystem.OpenStreamAsync(downloadedFilePath, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
                            {
                                await downloadStream.CopyToAsync(fileStream);
                                downloadedFiles.Add(downloadedFilePath);
                            }
                        }
                    }
                }
            }

            return downloadedFiles;
        }
    }
}
