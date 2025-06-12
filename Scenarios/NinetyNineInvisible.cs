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
    public static class NinetyNineInvisible
    {
        public static async Task DownloadPodcast()
        {
            string downloadTargetDirectory = @".\\staging";
            ILogger logger = new ConsoleLogger("WebCrawler", LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Vrb);
            IFileSystem outputFileSystem = new RealFileSystem(logger.Clone("FileSystem"), downloadTargetDirectory);

            RateLimiter limiter = new RateLimiter(2, 20);


            ISet<Regex> patterns = new HashSet<Regex>();
            Regex indexPageRegex = new Regex("^https:\\/\\/99percentinvisible.org\\/episodes\\/page\\/\\d+\\/?\\?view_option=list$");
            patterns.Add(indexPageRegex);

            Regex articleRegex = new Regex("<article [\\w\\W]+?<\\/article>");
            Regex downloadPageRegex = new Regex("^https:\\/\\/99percentinvisible.org\\/episode\\/.+?\\/download");
            //MultiRegex downloadLinkMatcher = new MultiRegex();
            //downloadLinkMatcher.AddRegex(new Regex("<a\\s+href=\"([^\"]+?\\.mp3(?:\\?nocache)?)\""));
            //downloadLinkMatcher.AddRegex(new Regex("<a[^>]+href=\\\"([^\\\"]+?\\.mp3(?:\\?[^\\\"]*))\\\""));
            //downloadLinkMatcher.AddRegex(new Regex("<source\\s+src=\"([\\w\\W]+?\\.mp3(?:\\?nocache)?)\""));
            Regex episodeTitleMatcher = new Regex("<h3.+?><.+?>(.+?)<");
            Regex episodeNumberMatcher = new Regex("data-episode-number=\"(\\d+)\">");
            Regex episodeDateMatcher = new Regex("<span>(\\d{2})\\.(\\d{2})\\.(\\d{2})<\\/span>");
            Regex downloadLinkMatcher = new Regex("href=\"(\\/episode\\/.+?\\/download)\"");
            string baseUrl = "https://99percentinvisible.org/episodes/page/11/?view_option=list";

            logger.Log("Starting to download 99% Invisible");
            await WebCrawler.Crawl(new Uri(baseUrl),
                async (webPage) =>
                {
                    limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    logger.Log("Page url is " + webPage.Url, LogLevel.Vrb);

                    // Make sure we're on an index page
                    Match indexPageMatch = indexPageRegex.Match(webPage.Url.AbsoluteUri);
                    if (!indexPageMatch.Success)
                    {
                        return true;
                    }

                    // Find all articles listed on this index page
                    logger.Log("Got an index page, parsing articles...");

                    MatchCollection articleMatches = articleRegex.Matches(webPage.Html);
                    logger.Log("Found " + articleMatches.Count + " articles");
                    foreach (Match articleMatch in articleMatches)
                    {
                        string articleText = articleMatch.Value;
                        Match downloadLinkMatch = downloadLinkMatcher.Match(articleText);
                        Match episodeTitleMatch = episodeTitleMatcher.Match(articleText);
                        Match episodeNumberMatch = episodeNumberMatcher.Match(articleText);
                        Match episodeDateMatch = episodeDateMatcher.Match(articleText);
                        if (!downloadLinkMatch.Success || !episodeTitleMatch.Success || !episodeNumberMatch.Success)
                        {
                            if (episodeTitleMatch.Success)
                            {
                                logger.Log($"Invalid match data for episode {episodeTitleMatch.Groups[1].Value} (missing title, episode num, or mp3 link?)", LogLevel.Wrn);
                            }
                            else
                            {
                                logger.Log("Invalid match data (missing title, episode num, or mp3 link?)", LogLevel.Wrn);
                            }

                            continue;
                        }

                        logger.Log($"Found {episodeTitleMatch.Groups[1].Value}, {episodeNumberMatch.Groups[1].Value}, {downloadLinkMatch.Groups[1].Value}");

                        //if (!episodeTitleMatch.Groups[1].Value.ToLower().Contains("secretly incredibly fascinating:"))
                        //{
                        //    logger.Log("Not an actual episode " + episodeTitleMatch.Groups[1].Value, LogLevel.Std);
                        //    return;
                        //}

                        int episodeNum = int.Parse(episodeNumberMatch.Groups[1].Value);

                        string episodeDate = "2000-01-01";
                        if (episodeDateMatch.Success)
                        {
                            episodeDate = $"20{episodeDateMatch.Groups[3].Value}-{episodeDateMatch.Groups[1].Value}-{episodeDateMatch.Groups[2].Value}";
                        }

                        //if (episodeNum <= 656)
                        //{
                        //    logger.Log("Already have episode " + episodeNum, LogLevel.Std);
                        //    return;
                        //}

                        string fileName = string.Format("99% Invisible {0:D3} - {1} - {2}.mp3", episodeNum, episodeDate, episodeTitleMatch.Groups[1].Value);
                        foreach (var reservedChar in Path.GetInvalidFileNameChars())
                        {
                            fileName = fileName.Replace(reservedChar, '_');
                        }

                        FileInfo targetFile = new FileInfo(Path.Combine(downloadTargetDirectory, fileName));

                        if (targetFile.Exists)
                        {
                            logger.Log("Already have file " + targetFile.Name, LogLevel.Std);
                            continue;
                        }

                        // Download the episode
                        limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                        logger.Log("Downloading " + targetFile.Name);
                        string downloadPageAbsoluteLink = "https://99percentinvisible.org" + downloadLinkMatch.Groups[1].Value;
                        await WebCrawler.DownloadFile(new Uri(downloadPageAbsoluteLink), targetFile, logger, webPage.Url.AbsoluteUri);
                    }
                    return true;
                },
                logger,
                patterns);

            // logger.Log("Waiting for worker threads to finish...");
            // await workerThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            logger.Log("Done with all processing");
        }
    }
}
