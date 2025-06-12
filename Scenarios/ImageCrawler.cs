using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios
{
    public static class ImageCrawler
    {
        public static async Task DownloadImages()
        {
            ILogger logger = new ConsoleLogger("WebCrawler", LogLevel.Std | LogLevel.Wrn | LogLevel.Err);

            RateLimiter limiter = new RateLimiter(2, 20);
            HashSet<Regex> allowedUrlRegexes = new HashSet<Regex>();
            allowedUrlRegexes.Add(new Regex("https?://androidarts.com/.*?"));

            await WebCrawler.Crawl(
                new Uri("http://androidarts.com/"),
                async (crawledPage) =>
                {
                    logger.Log("Crawling " + crawledPage.Url);
                    limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    foreach (Uri link in ExtractImageLinks(crawledPage.Url, crawledPage.Html, logger))
                    {
                        string fileName = WebUtility.UrlDecode(link.AbsolutePath.Substring(link.AbsolutePath.LastIndexOf('/')));
                        FileInfo targetFile = new FileInfo(@"C:\Code\WebCrawler\staging\" + fileName);
                        if (!targetFile.Exists)
                        {
                            logger.Log("Downloading " + link);
                            await WebCrawler.DownloadFile(link, targetFile, logger, crawledPage.Url.ToString());
                            limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                        }
                    }

                    return true;
                },
                logger,
                allowedUrlRegexes);

            logger.Log("Done with all processing");
        }

        private static readonly Regex IMAGE_LINK_EXTRACTOR = new Regex("src=([\"\'])([^#].+?(?:jpg|png|gif|webp))\\1", RegexOptions.IgnoreCase);

        private static List<Uri> ExtractImageLinks(Uri baseUrl, string page, ILogger logger)
        {
            List<Uri> returnVal = new List<Uri>();

            foreach (Match m in IMAGE_LINK_EXTRACTOR.Matches(page))
            {
                string x = WebUtility.HtmlDecode(m.Groups[2].Value);
                Uri linkUrl;

                try
                {
                    // Is it a relative uri? resolve it
                    if (x.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        x.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        linkUrl = new Uri(x);
                        returnVal.Add(linkUrl);
                    }
                    else
                    {
                        linkUrl = new Uri(baseUrl, x);
                        returnVal.Add(linkUrl);
                    }
                }
                catch (UriFormatException e)
                {
                    logger.Log(e, LogLevel.Err);
                }
            }

            return returnVal;
        }
    }
}
