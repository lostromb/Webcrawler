using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios
{
    public static class ThingExplainer
    {
        public static async Task ProcessImages()
        {
            ILogger logger = new ConsoleLogger("WebCrawler", Durandal.Common.Logger.LogLevel.Std | Durandal.Common.Logger.LogLevel.Wrn | Durandal.Common.Logger.LogLevel.Err);

            DirectoryInfo sourceDirectory = new DirectoryInfo(@"C:\Users\lostromb\Desktop\thing explainer\OEBPS");
            string downloadTargetDirectory = @"C:\Users\lostromb\Desktop\thing explainer\pages";
            if (!Directory.Exists(downloadTargetDirectory))
            {
                logger.Log("Creating staging directory " + downloadTargetDirectory);
                Directory.CreateDirectory(downloadTargetDirectory);
            }

            IFileSystem stagingFileSystem = new RealFileSystem(logger.Clone("FileSystem"), downloadTargetDirectory);
            RateLimiter limiter = new RateLimiter(1, 20);

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

            ChromeDriver webDriver = new ChromeDriver(chromeDriverService, chromeOptions);
            try
            {
                webDriver.Manage().Window.Maximize();
                bool first = true;
                foreach (FileInfo htmlFile in sourceDirectory.EnumerateFiles("*.xhtml"))
                {
                    if (first)
                    {
                        first = false;
                        //continue;
                    }

                    string url = "file:///" + htmlFile.FullName;
                    webDriver.Navigate().GoToUrl(url);
                    await Task.Delay(1000);
                    object commandResult;
                    Dictionary<string, object> commandResultDict;
                    string screenCaptureBase64;
                    byte[] screenCaptureData;

                    webDriver.ExecuteCustomDriverCommand("Page.setDeviceMetricsOverride", new Dictionary<string, object>()
                    {
                        { "width", 0 },
                        { "height", 0 },
                        { "deviceScaleFactor", 0 },
                        { "mobile", false },
                        { "scale", 1 },
                    });

                    Dictionary<string, object> viewport = new Dictionary<string, object>()
                    {
                        { "x", 0 },
                        { "y", 0 },
                        { "width", 648 },
                        { "height", 944 },
                        { "scale", 3 },
                    };

                    commandResult = webDriver.ExecuteCustomDriverCommand("Page.captureScreenshot", new Dictionary<string, object>()
                    {
                        { "format", "jpeg" },
                        { "quality", 90 },
                        { "fromSurface", true },
                        { "clip", viewport },
                    });

                    commandResultDict = commandResult as Dictionary<string, object>;
                    screenCaptureBase64 = commandResultDict["data"] as string;
                    screenCaptureData = Convert.FromBase64String(screenCaptureBase64);
                    File.WriteAllBytes(downloadTargetDirectory + "\\" + htmlFile.Name + ".jpg", screenCaptureData);

                    //// Capture bottom half of page

                    //webDriver.ExecuteChromeCommand("Input.dispatchKeyEvent", new Dictionary<string, object>()
                    //{
                    //    { "type", "keyDown" },
                    //    { "windowsVirtualKeyCode", 34},
                    //});
                    //await Task.Delay(500);

                    //commandResult = webDriver.ExecuteChromeCommandWithResult("Page.captureScreenshot", new Dictionary<string, object>()
                    //{
                    //    { "format", "png" },
                    //});

                    //commandResultDict = commandResult as Dictionary<string, object>;
                    //screenCaptureBase64 = commandResultDict["data"] as string;
                    //screenCaptureData = Convert.FromBase64String(screenCaptureBase64);

                    //// Stitch bitmap together and save as jpg
                    //File.WriteAllBytes(downloadTargetDirectory + "\\" + htmlFile.Name + "_2.png", screenCaptureData);
                }
            }
            finally
            {
                webDriver.Quit();
            }
        }

        private struct ChapterAndPage
        {
            public readonly int ChapterNum;
            public readonly int PageNum;

            public ChapterAndPage(int chapter, int page)
            {
                ChapterNum = chapter;
                PageNum = page;
            }

            public override int GetHashCode()
            {
                return ChapterNum.GetHashCode() ^ (PageNum.GetHashCode() << 1);
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is ChapterAndPage))
                {
                    return false;
                }

                ChapterAndPage other = (ChapterAndPage)obj;
                return ChapterNum == other.ChapterNum &&
                    PageNum == other.PageNum;
            }
        }

        private static async Task ExtractPageImageLink(IWebDriver webDriver, ILogger logger, ChapterAndPage currentPage, IDictionary<ChapterAndPage, Uri> outputPages)
        {
            // Retry loop
            for (int c = 0; c < 100; c++)
            {
                foreach (IWebElement webElement in webDriver.FindElements(By.XPath("/html/body/div[@id=\'content\']/div[2]/*/*/img")))
                {
                    logger.Log("Found page " + currentPage.PageNum + " image " + webElement.GetAttribute("src"), Durandal.Common.Logger.LogLevel.Vrb);
                    outputPages[currentPage] = new Uri(webElement.GetAttribute("src"));
                    return;
                }

                await Task.Delay(100);
            }
        }
    }
}
