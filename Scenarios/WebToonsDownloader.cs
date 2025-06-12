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
    public static class WebToonsDownloader
    {
        public class WebToonsComic
        {
            public WebToonsComic(string name, int titleNumber, string urlPath)
            {
                Name = name;
                TitleNumber = titleNumber;
                ComicUrlPath = urlPath;
            }

            public string Name { get; set; }
            public int TitleNumber { get; set; }
            public string ComicUrlPath { get; set; }
        }

        public static async Task DownloadWebtoonsComics()
        {
            string downloadTargetDirectory = @"C:\Code\WebCrawler\staging";
            ILogger logger = new ConsoleLogger("WebCrawler", LogLevel.Std | LogLevel.Wrn | LogLevel.Err);
            IFileSystem outputFileSystem = new RealFileSystem(logger.Clone("FileSystem"), downloadTargetDirectory);

            //VirtualPath inputDir = new VirtualPath("Aerial Magic #003");
            //VirtualPath scratchDir = VirtualPath.Root;
            //await ComicStripProcessor.PostprocessImageStrips(logger, outputFileSystem, outputFileSystem.ListFiles(inputDir).OrderBy((a) => a.Name), scratchDir);
            //return;

            RateLimiter limiter = new RateLimiter(2, 20);
            List<WebToonsComic> comicsToDownload = new List<WebToonsComic>();
            //comicsToDownload.Add(new WebToonsComic("Supersonic Girl", 633, "/en/super-hero/supersonic-girl"));
            //comicsToDownload.Add(new WebToonsComic("Aerial Magic", 1358, "/en/fantasy/aerial-magic"));
            //comicsToDownload.Add(new WebToonsComic("The Steam Dragon Express", 1270, "/en/fantasy/steam-dragon-express"));
            //comicsToDownload.Add(new WebToonsComic("A Good Day to be a Dog", 1390, "/en/romance/a-good-day-tobe-a-dog"));
            //comicsToDownload.Add(new WebToonsComic("The Strange Tales of Oscar Zahn", 685, "/en/fantasy/the-strange-tales-of-oscar-zahn"));
            //comicsToDownload.Add(new WebToonsComic("Shadow Pirates", 1455, "/en/action/shadow-pirates"));
            //comicsToDownload.Add(new WebToonsComic("Inarime", 675, "/en/super-hero/inarime"));
            //comicsToDownload.Add(new WebToonsComic("House of Stars", 1620, "/en/fantasy/house-of-stars"));
            //comicsToDownload.Add(new WebToonsComic("The Wolf & Red Riding Hood", 2142, "/en/comedy/wolf-and-red-riding-hood"));
            //comicsToDownload.Add(new WebToonsComic("Days of Hana", 1246, "/en/drama/days-of-hana"));
            //comicsToDownload.Add(new WebToonsComic("Caster", 1461, "/en/action/caster"));
            comicsToDownload.Add(new WebToonsComic("Lore Olympus", 1320, "/en/romance/lore-olympus"));

            //IThreadPool workerThreadPool = new FixedCapacityThreadPool(
            //    new TaskThreadPool(),
            //    NullLogger.Singleton,
            //    NullMetricCollector.Singleton,
            //    DimensionSet.Empty,
            //    "WorkerThreadPool",
            //    Environment.ProcessorCount);

            foreach (WebToonsComic currentProcessingComic in comicsToDownload)
            {
                ISet<Regex> patterns = new HashSet<Regex>();
                Regex indexPageRegex = new Regex("^https:\\/\\/www\\.webtoons\\.com" + Regex.Escape(currentProcessingComic.ComicUrlPath) + "\\/list\\?title_no=" + currentProcessingComic.TitleNumber.ToString() + "(&page=\\d+)?");
                Regex episodePageRegex = new Regex("^https:\\/\\/www\\.webtoons\\.com" + Regex.Escape(currentProcessingComic.ComicUrlPath) + "\\/.+?\\/viewer\\?title_no=" + currentProcessingComic.TitleNumber.ToString() + "&episode_no=(\\d+)");
                patterns.Add(indexPageRegex);
                patterns.Add(episodePageRegex);
                Regex downloadLinkMatcher = new Regex("class=\\\"_images\\\" data-url=\\\"(https:\\/\\/webtoon-phinf\\.pstatic\\.net\\/.+?\\.(jpg|png|gif).*?)\\\"");
                string baseUrl = "https://www.webtoons.com" + currentProcessingComic.ComicUrlPath + "/list?title_no=" + currentProcessingComic.TitleNumber.ToString();

                logger.Log("Starting to process comic " + currentProcessingComic.Name);
                await WebCrawler.Crawl(new Uri(baseUrl),
                    async (webPage) =>
                    {
                        logger.Log("Page url is " + webPage.Url, LogLevel.Vrb);

                        // Are we on a comic page?
                        Match episodePageMatch = episodePageRegex.Match(webPage.Url.AbsoluteUri);
                        if (episodePageMatch.Success)
                        {
                            int chapterNum = int.Parse(episodePageMatch.Groups[1].Value);
                            logger.Log("Processing " + currentProcessingComic.Name + " chapter " + chapterNum, LogLevel.Std);

                            string comicFileName = string.Format(
                                "{0} #{1:D3}",
                                currentProcessingComic.Name.Replace('?', '_').Replace(':', '_').Replace('\\', '_').Replace('/', '_').Replace('\"', '_'),
                                chapterNum);

                            VirtualPath stagingDirectory = new VirtualPath(comicFileName);
                            if (!(await outputFileSystem.ExistsAsync(stagingDirectory)))
                            {
                                outputFileSystem.CreateDirectory(stagingDirectory);
                            }

                            List<VirtualPath> inputFiles = new List<VirtualPath>();

                            try
                            {
                                int pageNum = 1;
                                MatchCollection comicImageLinks = downloadLinkMatcher.Matches(webPage.Html);
                                foreach (Match downloadLinkMatch in comicImageLinks)
                                {
                                    if (downloadLinkMatch.Success)
                                    {
                                        string pageImageUrl = downloadLinkMatch.Groups[1].Value;
                                        string fileType = downloadLinkMatch.Groups[2].Value;
                                        pageImageUrl = WebUtility.UrlDecode(pageImageUrl);
                                        //string sanitizedFileName = pageImageUrl.Replace('?', '_').Replace(':', '_').Replace('\\', '_').Replace('/', '_').Replace('\"', '_');
                                        string outputFileName = string.Format("raw_{0:D3}.{1}", pageNum, fileType);
                                        FileInfo filePath = new FileInfo(downloadTargetDirectory + Path.DirectorySeparatorChar + stagingDirectory.Name + Path.DirectorySeparatorChar + outputFileName);
                                        if (filePath.Exists)
                                        {
                                            //logger.Log("File " + filePath.FullName + " already exists!", LogLevel.Wrn);
                                        }
                                        else
                                        {
                                            limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                                            logger.Log("Downloading " + pageImageUrl + " to " + filePath.FullName, LogLevel.Vrb);
                                            await WebCrawler.DownloadFile(new Uri(pageImageUrl), filePath, logger, webPage.Url.AbsoluteUri);
                                        }

                                        inputFiles.Add(stagingDirectory.Combine(filePath.Name));
                                        pageNum++;
                                    }
                                }

                                if (inputFiles.Count > 0)
                                {
                                    // Process all the images
                                    IList<VirtualPath> outputFiles = await LongComicStripProcessor.PostprocessImageStrips(logger, outputFileSystem, inputFiles.OrderBy((a) => a.Name), stagingDirectory);

                                    if (outputFiles.Count > 0)
                                    {
                                        // Package all images into a zip archive
                                        VirtualPath archivePath = new VirtualPath(comicFileName + ".cbz");
                                        if (await outputFileSystem.ExistsAsync(archivePath))
                                        {
                                            logger.Log("Output comic file " + archivePath.FullName + " already exists; overwriting...", LogLevel.Wrn);
                                            await outputFileSystem.DeleteAsync(archivePath);
                                        }

                                        logger.Log("Compressing " + archivePath.FullName, LogLevel.Vrb);
                                        using (ZipFile comicArchive = new ZipFile(archivePath, logger.Clone("OutputZipFile"), outputFileSystem))
                                        {
                                            comicArchive.CompressionLevel = Durandal.Common.Compression.ZLib.CompressionLevel.BestSpeed;
                                            comicArchive.AddFiles(outputFiles);
                                            comicArchive.Save();
                                        }

                                        // Dispose of all intermediate files
                                        //try
                                        //{
                                        //    foreach (VirtualPath tempImage in outputFiles)
                                        //    {
                                        //        outputFileSystem.Delete(tempImage);
                                        //    }
                                        //}
                                        //catch (Exception e)
                                        //{
                                        //    logger.Log(e, LogLevel.Err);
                                        //}
                                    }
                                    else
                                    {
                                        logger.Log("No output images found!", LogLevel.Err);
                                    }
                                }
                                else
                                {
                                    logger.Log("No input images found on page " + webPage.Url.AbsoluteUri, LogLevel.Wrn);
                                }
                            }
                            catch (Exception e)
                            {
                                logger.Log(e, LogLevel.Err);
                            }

                            //try
                            //{
                            //    // Dispose of all intermediate files
                            //    foreach (VirtualPath tempImage in inputFiles)
                            //    {
                            //        outputFileSystem.Delete(tempImage);
                            //    }

                            //    outputFileSystem.Delete(stagingDirectory);
                            //}
                            //catch (Exception e)
                            //{
                            //    logger.Log(e, LogLevel.Err);
                            //}
                        }

                        return true;
                    },
                    logger,
                    patterns);

                logger.Log("Done processing comic " + currentProcessingComic.Name);
            }

            // logger.Log("Waiting for worker threads to finish...");
            // await workerThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            logger.Log("Done with all processing");
        }
    }
}
