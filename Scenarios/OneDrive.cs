using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
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
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios
{
    public static class OneDrive
    {
        public static async Task Download()
        {
            ILogger logger = new ConsoleLogger("WebCrawler", Durandal.Common.Logger.LogLevel.Std | Durandal.Common.Logger.LogLevel.Wrn | Durandal.Common.Logger.LogLevel.Err);

            string downloadTargetDirectory = @"C:\Code\WebCrawler\bin\staging";
            if (!Directory.Exists(downloadTargetDirectory))
            {
                logger.Log("Creating staging directory " + downloadTargetDirectory);
                Directory.CreateDirectory(downloadTargetDirectory);
            }

            DirectoryInfo stagingDir = new DirectoryInfo(downloadTargetDirectory);

            // See if chromium exists
            string chromeBinaryLoc = Environment.CurrentDirectory + "\\chrome";
            if (!File.Exists(chromeBinaryLoc + "\\chrome.exe"))
            {
                if (!File.Exists(Environment.CurrentDirectory + "\\chrome.zip"))
                {
                    logger.Log("Chrome.zip not found!", Durandal.Common.Logger.LogLevel.Err);
                    return;
                }

                logger.Log("Unpacking chrome binary");
                IFileSystem localFileSystem = new RealFileSystem(logger.Clone("FileSystem"));
                using (ZipFile chromiumZip = new ZipFile(new VirtualPath("chrome.zip"), logger.Clone("ChromeUnpacker"), localFileSystem))
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

            string[] inputFileTable = File.ReadAllLines(@"C:\Users\lostromb\Desktop\onedrive.tsv");
            Regex tsvParser = new Regex("(.+?)\t(.+?)\t(.+)");

            ChromeDriverService chromeDriverService = ChromeDriverService.CreateDefaultService(chromeBinaryLoc, "chromedriver.exe");
            bool firstLaunch = true;

            using (DownloadMonitor downloadMonitor = new DownloadMonitor(logger.Clone("DownloadManager")))
            {
                ChromeDriver webDriver = new ChromeDriver(chromeDriverService, chromeOptions);
                try
                {
                    foreach (string inputLine in inputFileTable)
                    {
                        Match tsvMatch = tsvParser.Match(inputLine);
                        if (!tsvMatch.Success)
                        {
                            continue;
                        }

                        string url = tsvMatch.Groups[3].Value;
                        string targetFileNameBase = tsvMatch.Groups[1].Value + " - " + tsvMatch.Groups[2].Value;
                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            targetFileNameBase = targetFileNameBase.Replace(c, '_');
                        }

                        // See if a file like this already exists
                        bool fileAlreadyExists = false;
                        foreach (var file in stagingDir.EnumerateFiles())
                        {
                            if (file.Name.StartsWith(targetFileNameBase))
                            {
                                fileAlreadyExists = true;
                                break;
                            }
                        }

                        if (fileAlreadyExists)
                        {
                            logger.Log(targetFileNameBase + " appears to already exist; skipping...");
                            continue;
                        }

                        webDriver.Navigate().GoToUrl(url);

                        await Task.Delay(TimeSpan.FromSeconds(5));

                        if (firstLaunch)
                        {
                            // Allow interactive login
                            logger.Log("Waiting 30 seconds for interactive login");
                            await Task.Delay(TimeSpan.FromSeconds(30));
                            logger.Log("Waiting finished");
                            firstLaunch = false;
                        }

                        // Wait for the download button to appear
                        IWebElement downloadButton = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.ClassName("od-ButtonBarCommand-label"))), TimeSpan.FromSeconds(10));

                        if (downloadButton == null)
                        {
                            logger.Log("No download button found, assuming blocked or dead file", Durandal.Common.Logger.LogLevel.Wrn);
                            continue;
                        }

                        // Click the download button
                        downloadButton.Click();

                        // Wait for the download to complete
                        RetrieveResult<FileInfo> fileResult = await downloadMonitor.WaitForNewDownload(CancellationToken.None, TimeSpan.FromMinutes(30));

                        if (!fileResult.Success)
                        {
                            logger.Log("Couldn't download " + url, Durandal.Common.Logger.LogLevel.Err);
                            continue;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(1));

                        // Move file from download dir to staging dir
                        string targetFileName = targetFileNameBase + fileResult.Result.Extension;
                        string targetFileFullName = Path.Combine(downloadTargetDirectory, targetFileName);
                        if (File.Exists(targetFileFullName))
                        {
                            logger.Log(targetFileName + " already exists, deleting new...");
                            File.Delete(fileResult.Result.FullName);
                        }
                        else
                        {
                            File.Move(fileResult.Result.FullName, targetFileFullName);
                            logger.Log("Downloaded " + targetFileName);
                        }
                    }
                }
                finally
                {
                    webDriver.Quit();
                }
            }
        }

        internal class DownloadMonitor : IDisposable
        {
            private readonly FileSystemWatcher _watcher;
            private readonly ILogger _logger;
            private readonly BufferedChannel<FileInfo> _downloadCompleteSignal;

            public DownloadMonitor(ILogger logger)
            {
                DirectoryInfo downloadsDirectory = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                downloadsDirectory = new DirectoryInfo(Path.Combine(downloadsDirectory.Parent.FullName, "Downloads"));
                _watcher = new FileSystemWatcher(downloadsDirectory.FullName);
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                _watcher.EnableRaisingEvents = true;
                _watcher.IncludeSubdirectories = false;
                _watcher.Created += DownloadFileCreated;
                _watcher.Changed += DownloadFileChanged;
                _watcher.Renamed += DownloadFileRenamed;
                _logger = logger.AssertNonNull(nameof(logger));
                _downloadCompleteSignal = new BufferedChannel<FileInfo>();
            }

            public async Task<RetrieveResult<FileInfo>> WaitForNewDownload(CancellationToken cancelToken, TimeSpan timeout)
            {
                return await _downloadCompleteSignal.TryReceiveAsync(cancelToken, DefaultRealTimeProvider.Singleton, timeout);
            }

            private void DownloadFileCreated(object sender, FileSystemEventArgs e)
            {
                //_logger.Log("FILE CREATED " + e.FullPath + " " + e.ChangeType);
            }

            private void DownloadFileChanged(object sender, FileSystemEventArgs e)
            {
               // _logger.Log("FILE CHANGED " + e.FullPath + " " + e.ChangeType);
            }

            private void DownloadFileRenamed(object sender, RenamedEventArgs e)
            {
                //_logger.Log("FILE RENAMED " + e.OldName + " -> " + e.Name);

                // When a download finishes, the .crdownload file renames to the actual final file.
                // Use this as the primary signal
                if (e.OldName.EndsWith(".crdownload"))
                {
                    _downloadCompleteSignal.Send(new FileInfo(e.FullPath));
                }
            }

            public void Dispose()
            {
                _watcher.Dispose();
                _downloadCompleteSignal.Dispose();
            }
        }


        private static async Task<IWebElement> WaitForElementToLoad(Func<Task<IWebElement>> loadFunction, TimeSpan maxWaitTime)
        {
            TimeSpan timeWaited = TimeSpan.Zero;
            TimeSpan backoffTime = TimeSpan.FromMilliseconds(100);
            // Retry loop
            for (int c = 0; c < 100 && timeWaited < maxWaitTime; c++)
            {
                try
                {
                    IWebElement returnVal = await loadFunction();
                    if (returnVal != null)
                    {
                        return returnVal;
                    }
                }
                catch (Exception) { }

                await Task.Delay(backoffTime).ConfigureAwait(false);
                timeWaited += backoffTime;
                backoffTime += TimeSpan.FromMilliseconds(100);
            }

            return null;
        }
    }
}
