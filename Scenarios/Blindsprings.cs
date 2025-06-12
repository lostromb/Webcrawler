using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios
{
    public static class Blindsprings
    {
        public static async Task Download()
        {
            string downloadTargetDirectory = @".\\staging";
            ILogger logger = new ConsoleLogger("WebCrawler", LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Vrb);
            IFileSystem outputFileSystem = new RealFileSystem(logger.Clone("FileSystem"), downloadTargetDirectory);

            RateLimiter limiter = new RateLimiter(2, 20);
            IHttpClientFactory httpClientFactory = new PortableHttpClientFactory();

            Regex nextPageLinkMatcher = new Regex("<a class=\"cc-next\" rel=\"next\" href=\"(.+?)\"></a>");
            Regex comicImageMatcher = new Regex("<img title=\"(.+?)\" src=\"(.+?)\" id=\"cc-comic\" */>");
            Uri currentUrl = new Uri("https://www.blindsprings.com/comic/blindsprings-cover-book-one");

            IHttpClient httpClient = httpClientFactory.CreateHttpClient(currentUrl, logger);
            int comicPage = 0;

            while (true)
            {
                limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                logger.Log("Crawling page " + comicPage + " " + currentUrl, LogLevel.Vrb);
                try
                {
                    HttpRequest request = HttpRequest.CreateOutgoing(currentUrl.PathAndQuery);
                    request.RequestHeaders["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:80.0) Gecko/20100101 Firefox/80.0";
                    //request.RequestHeaders["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                    //request.RequestHeaders["Accept-Language"] = "en-US,en;q=0.5";
                    //request.RequestHeaders["Cache-Control"] = "max-age=0";
                    //request.RequestHeaders["Host"] = "www.webtoons.com";
                    //request.RequestHeaders["Upgrade-Insecure-Requests"] = "1";
                    //request.RequestHeaders["Referer"] = "https://www.webtoons.com/en/dailySchedule";
                    //request.RequestHeaders["Cookie"] = "locale=en; needGDPR=false; needCCPA=true; allowedCCPACookieMarketing=gad%2Cfb%2Ctw%2Csn%2Cbi%2Ctt; timezoneOffset=-7; rw=w_2142_7%2Cw_658_615%2Cw_1285_133%2Cw_437_103%2Cw_471_139%2Cw_1455_16%2Cw_1363_34%2Cw_747_21%2Cw_387_62%2Cw_473_173";
                    HttpResponse response = await httpClient.SendRequestAsync(request);
                    if (response.ResponseCode >= 300)
                    {
                        logger.Log("Error response.", LogLevel.Err);
                        string error = await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                        logger.Log(error);
                        break;
                    }

                    string html = await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Match comicImageMatch = comicImageMatcher.Match(html);

                    string comicTitle = WebUtility.HtmlDecode(comicImageMatch.Groups[1].Value);
                    if (comicTitle.Length > 60)
                    {
                        comicTitle = comicTitle.Substring(0, 60);
                    }

                    string fileName = string.Format("{0:D3} - {1}.jpg", comicPage, comicTitle);
                    foreach (var reservedChar in Path.GetInvalidFileNameChars())
                    {
                        fileName = fileName.Replace(reservedChar, '_');
                    }

                    FileInfo targetFile = new FileInfo(Path.Combine(downloadTargetDirectory, fileName));

                    await WebCrawler.DownloadFile(new Uri(comicImageMatch.Groups[2].Value), targetFile, logger, currentUrl.AbsoluteUri);
                    comicPage++;

                    Match nextPageLinkMatch = nextPageLinkMatcher.Match(html);
                    if (!nextPageLinkMatch.Success)
                    {
                        logger.Log("Could not find next page button");
                        break;
                    }
                    else
                    {
                        currentUrl = new Uri(nextPageLinkMatch.Groups[1].Value);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e);
                    break;
                }
            }

            logger.Log("Done with all processing");
        }
    }
}
