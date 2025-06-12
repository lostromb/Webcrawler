using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler
{
    public static class WebCrawler
    {
        private static readonly string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:80.0) Gecko/20100101 Firefox/80.0";
        private static readonly Regex LINK_EXTRACTOR = new Regex("href=([\"\'])([^#].+?)\\1", RegexOptions.IgnoreCase);
        private static readonly IHttpClientFactory HTTP_CLIENT_FACTORY;
        
        static WebCrawler()
        {
            HTTP_CLIENT_FACTORY = new PortableHttpClientFactory();
        }

        public class CrawledPage
        {
            public Uri Url;
            public string Html;
            public List<Uri> Links;
        }

        public static async Task DownloadFile(Uri uri, FileInfo target, ILogger logger, string referer = "")
        {
            try
            {
                IHttpClient httpClient = HTTP_CLIENT_FACTORY.CreateHttpClient(uri, logger);
                HttpRequest request = HttpRequest.CreateOutgoing(uri.PathAndQuery);
                request.RequestHeaders["User-Agent"] = USER_AGENT;
                if (!string.IsNullOrEmpty(referer))
                {
                    request.RequestHeaders["Referer"] = referer;
                }

                using (HttpResponse response = await httpClient.SendRequestAsync(request))
                {
                    if (response.ResponseCode == 200)
                    {
                        using (FileStream writeStream = new FileStream(target.FullName, FileMode.Create, FileAccess.Write))
                        {
                            using (Stream httpStream = response.ReadContentAsStream())
                            {
                                await httpStream.CopyToAsync(writeStream);
                            }
                        }
                    }
                    else
                    {
                        string responseString = await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                        logger.Log("Failed to download " + uri.AbsoluteUri + ": " + response.ResponseCode + " " + responseString, LogLevel.Err);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
            }
        }

        public static async Task<HttpResponse> MakeRequest(Uri uri, ILogger logger, string referer = "")
        {
            try
            {
                IHttpClient httpClient = HTTP_CLIENT_FACTORY.CreateHttpClient(uri, logger);
                using (HttpRequest request = HttpRequest.CreateOutgoing(uri.PathAndQuery))
                {
                    request.RequestHeaders["User-Agent"] = USER_AGENT;
                    if (!string.IsNullOrEmpty(referer))
                    {
                        request.RequestHeaders["Referer"] = referer;
                    }

                    return await httpClient.SendRequestAsync(request);
                }
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
                return null;
            }
        }

        public static async Task Crawl(Uri startUrl, Func<CrawledPage, Task<bool>> pageAction, ILogger logger, ISet<Regex> allowedUrlPatterns = null)
        {
            if (allowedUrlPatterns == null)
                allowedUrlPatterns = new HashSet<Regex>();

            ISet<string> crawledUrls = new HashSet<string>();
            Queue<Uri> urlQueue = new Queue<Uri>();
            urlQueue.Enqueue(startUrl);
            crawledUrls.Add(startUrl.AbsoluteUri);
            
            while (urlQueue.Count > 0)
            {
                CrawledPage thisPage = new CrawledPage();
                thisPage.Url = urlQueue.Dequeue();
                logger.Log("Crawling " + thisPage.Url, LogLevel.Vrb);
                try
                {
                    IHttpClient httpClient = HTTP_CLIENT_FACTORY.CreateHttpClient(thisPage.Url, logger);
                    HttpRequest request = HttpRequest.CreateOutgoing(thisPage.Url.PathAndQuery);
                    request.RequestHeaders["User-Agent"] = USER_AGENT;
                    //request.RequestHeaders["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                    //request.RequestHeaders["Accept-Language"] = "en-US,en;q=0.5";
                    //request.RequestHeaders["Cache-Control"] = "max-age=0";
                    //request.RequestHeaders["Host"] = "www.webtoons.com";
                    //request.RequestHeaders["Upgrade-Insecure-Requests"] = "1";
                    //request.RequestHeaders["Referer"] = "https://www.webtoons.com/en/dailySchedule";
                    //request.RequestHeaders["Cookie"] = "locale=en; needGDPR=false; needCCPA=true; allowedCCPACookieMarketing=gad%2Cfb%2Ctw%2Csn%2Cbi%2Ctt; timezoneOffset=-7; rw=w_2142_7%2Cw_658_615%2Cw_1285_133%2Cw_437_103%2Cw_471_139%2Cw_1455_16%2Cw_1363_34%2Cw_747_21%2Cw_387_62%2Cw_473_173";
                    using (HttpResponse response = await httpClient.SendRequestAsync(request))
                    {
                        if (response.ResponseCode < 300)
                        {
                            thisPage.Html = await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            thisPage.Links = ExtractUrls(thisPage.Url, thisPage.Html, logger);
                            bool result = await pageAction(thisPage);
                            if (!result)
                            {
                                logger.Log("Got signal to stop crawling");
                                urlQueue.Clear();
                            }
                            else
                            {
                                foreach (Uri url in FilterList(thisPage.Links, allowedUrlPatterns))
                                {
                                    if (!crawledUrls.Contains(url.AbsoluteUri))
                                    {
                                        urlQueue.Enqueue(url);
                                        crawledUrls.Add(url.AbsoluteUri);
                                    }
                                }
                            }
                        }
                        else
                        {
                            string responseString = await response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            logger.Log("Failed to download " + thisPage.Url.AbsoluteUri + ": " + response.ResponseCode + " " + responseString, LogLevel.Err);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                }
            }
        }

        private static List<Uri> ExtractUrls(Uri baseUrl, string page, ILogger logger)
        {
            List<Uri> returnVal = new List<Uri>();

            foreach (Match m in LINK_EXTRACTOR.Matches(page))
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

        private static List<Uri> FilterList(List<Uri> list, ISet<Regex> allowedUrlPatterns)
        {
            List<Uri> filteredList = new List<Uri>();
            foreach (Uri linkUrl in list)
            {
                //Console.WriteLine ("Found link " + linkUrl);
                bool allowed = allowedUrlPatterns.Count == 0;
                foreach (Regex r in allowedUrlPatterns)
                {
                    if (r.Match(linkUrl.OriginalString).Success)
                    {
                        allowed = true;
                        break;
                    }
                }

                if (linkUrl.AbsolutePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
                    linkUrl.AbsolutePath.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    allowed = false;
                }

                if (allowed)
                {
                    //Console.WriteLine ("It's allowed");
                    filteredList.Add(linkUrl);
                }
            }

            return filteredList;
        }
    }
}
