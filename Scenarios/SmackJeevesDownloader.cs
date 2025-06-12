using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
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
    public static class SmackJeevesDownloader
    {
        public static async Task DownloadSmackJeevesComics()
        {
            string downloadTargetDirectory = @"C:\Code\WebCrawler\staging";
            ILogger logger = new ConsoleLogger("WebCrawler", Durandal.Common.Logger.LogLevel.Std | Durandal.Common.Logger.LogLevel.Wrn | Durandal.Common.Logger.LogLevel.Err);
            RateLimiter limiter = new RateLimiter(1, 20);
            PooledTcpClientSocketFactory socketFactory = new PooledTcpClientSocketFactory(
                logger.Clone("SocketFactory"),
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                System.Security.Authentication.SslProtocols.None,
                ignoreCertErrors: true);
            SocketHttpClientFactory httpClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(socketFactory),
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty,
                Http2SessionManager.Default);

            int outputImageNum = 1;
            string baseUrl = "https://www.smackjeeves.com/discover/detail?titleNo=177959&articleNo=";

            // See if chromium exists
            string chromeBinaryLoc = Environment.CurrentDirectory + "\\chromium";
            if (!File.Exists(chromeBinaryLoc + "\\chrome.exe"))
            {
                if (!File.Exists(Environment.CurrentDirectory + "\\chromium.zip"))
                {
                    logger.Log("Chromium.zip not found!", Durandal.Common.Logger.LogLevel.Err);
                    return;
                }

                logger.Log("Unpacking chromium binary");
                IFileSystem localFileSystem = new RealFileSystem(logger.Clone("FileSystem"));
                using (ZipFile chromiumZip = new ZipFile(new VirtualPath("chromium.zip"), logger.Clone("ChromiumUnpacker"), localFileSystem))
                {
                    chromiumZip.ExtractAll(VirtualPath.Root);
                }
            }

            logger.Log("Creating chrome driver");
            ChromeOptions chromeOptions = new ChromeOptions()
            {
                PageLoadStrategy = PageLoadStrategy.Normal,
                BinaryLocation = chromeBinaryLoc + "\\chrome.exe",
                LeaveBrowserRunning = false,
            };

            ChromeDriverService chromeDriverService = ChromeDriverService.CreateDefaultService(chromeBinaryLoc, "chromedriver.exe");

            IWebDriver webDriver = new ChromeDriver(chromeDriverService, chromeOptions);
            try
            {
                for (int chapter = 1; chapter <= 471; chapter++)
                {
                    string comicPageUrl = baseUrl + chapter.ToString();
                    limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    logger.Log("Crawling " + comicPageUrl);
                    webDriver.Navigate().GoToUrl(comicPageUrl);
                    
                    foreach (IWebElement webElement in webDriver.FindElements(By.ClassName("comic-image__image")))
                    {
                        string imageUrl = webElement.GetAttribute("src");
                        string extension = imageUrl.Contains(".jpg") ? "jpg" : "png";
                        string outputImageFileName = string.Format("{0}\\{1:D4}.{2}", downloadTargetDirectory, outputImageNum++, extension);
                        logger.Log("Downloading image " + imageUrl + " to " + outputImageFileName);
                        using (IHttpClient httpClient = httpClientFactory.CreateHttpClient(new Uri(imageUrl), logger.Clone("ImageDownloadClient")))
                        {
                            HttpResponse httpResponse = await httpClient.SendRequestAsync(HttpRequest.CreateOutgoing(imageUrl));
                            if (httpResponse.ResponseCode == 200)
                            {
                                ArraySegment<byte> fileSegment = await httpResponse.ReadContentAsByteArrayAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                                byte[] singleSegment = new byte[fileSegment.Count];
                                Buffer.BlockCopy(fileSegment.Array, fileSegment.Offset, singleSegment, 0, fileSegment.Count);
                                File.WriteAllBytes(outputImageFileName, singleSegment);
                            }
                            else
                            {
                                logger.Log("Failed to download image, response code " + httpResponse.ResponseCode, Durandal.Common.Logger.LogLevel.Err);
                            }
                        }
                    }
                }
            }
            finally
            {
                webDriver.Quit();
            }
        }
    }
}
