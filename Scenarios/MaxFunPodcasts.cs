using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
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
using WebCrawler.InterProcess;
using WebCrawler.Scenarios.GeneralConf;

namespace WebCrawler.Scenarios
{
    public interface MaxFunEpisodeFilter
    {
        bool Process(ref string episodeTitle, ILogger logger);
    }

    public class MaxFunShowDetails
    {
        public string InternalId;
        public string FileNameBase;
        public string MetadataArtist;
        public string MetadataAlbum;
        public string MetadataAlbumArtist;
        public string MetadataGenre;
        public int EpisodeNumberPadding = 3;
        public int EncodeBitrate = 16;
    }

    public static class MaxFunPodcasts
    {
        public static async Task DownloadNewMaxFunPodcastEpisodes(
            MaxFunShowDetails showDetails,
            DirectoryInfo outputDirectory,
            int minimumEpisode = 0,
            MaxFunEpisodeFilter episodeFilter = null)
        {
            string stagingDirectory = @".\\staging";
            ILogger logger = new ConsoleLogger("WebCrawler", LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Vrb);

            RateLimiter limiter = new RateLimiter(2, 20);

            using (IThreadPool workerThreadPool = new FixedCapacityThreadPool(
                new TaskThreadPool(),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "WorkerThreadPool",
                Environment.ProcessorCount))
            {
                ISet<Regex> patterns = new HashSet<Regex>();
                Regex indexPageRegex = new Regex("^https:\\/\\/maximumfun\\.org\\/podcasts\\/" + showDetails.InternalId + "\\/\\?_post-type=episode&paged=\\d+$");
                Regex episodePageRegex = new Regex("^https:\\/\\/maximumfun\\.org\\/episodes\\/" + showDetails.InternalId + "\\/.+$");
                patterns.Add(indexPageRegex);
                patterns.Add(episodePageRegex);
                MultiRegex downloadLinkMatcher = new MultiRegex();
                //downloadLinkMatcher.AddRegex(new Regex("<a\\s+href=\"([^\"]+?\\.mp3(?:\\?nocache)?)\""));
                downloadLinkMatcher.AddRegex(new Regex("<a[^>]+href=\\\"([^\\\"]+?\\.mp3(?:\\?[^\\\"]+)?)\\\""));
                downloadLinkMatcher.AddRegex(new Regex("<source\\s+src=\"([\\w\\W]+?\\.mp3(?:\\?nocache)?)\""));
                Regex episodeTitleMatcher = new Regex("id=\"episode-title\"[\\w\\W]{1,30}<h1>\\s*(.+?)\\s*<\\/h1>");
                Regex episodeNumberMatcher = new Regex("id=\"single-episode-number\"[\\w\\W]{1,30}<h3>\\s*Episode (\\d+?)\\s*<\\/h3>");
                string baseUrl = "https://maximumfun.org/podcasts/" + showDetails.InternalId + "/?_post-type=episode&_paged=1";

                int missedEpisodeNumCount = 0;

                logger.Log("Starting to download podcast " + showDetails.InternalId);
                await WebCrawler.Crawl(new Uri(baseUrl),
                    async (webPage) =>
                    {
                        if (missedEpisodeNumCount > 10)
                        {
                            return false;
                        }

                        limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                        logger.Log("Page url is " + webPage.Url, LogLevel.Vrb);

                        // Are we on an episode page?"
                        Match episodePageMatch = episodePageRegex.Match(webPage.Url.AbsoluteUri);
                        if (!episodePageMatch.Success)
                        {
                            return true;
                        }

                        logger.Log("Got an episode page, finding mp3 link...");

                        Match downloadLinkMatch = downloadLinkMatcher.Match(webPage.Html);
                        Match episodeTitleMatch = episodeTitleMatcher.Match(webPage.Html);
                        Match episodeNumberMatch = episodeNumberMatcher.Match(webPage.Html);
                        if (!downloadLinkMatch.Success)
                        {
                            logger.Log("Invalid match data: Missing download link", LogLevel.Wrn);
                            return true;
                        }
                        if (!episodeTitleMatch.Success)
                        {
                            logger.Log("Invalid match data: Missing episode title", LogLevel.Wrn);
                            return true;
                        }
                        if (!episodeNumberMatch.Success)
                        {
                            logger.Log("Invalid match data: Missing episode number", LogLevel.Wrn);
                            return true;
                        }

                        string escapedEpisodeName = episodeTitleMatch.Groups[1].Value;
                        escapedEpisodeName = WebUtility.HtmlDecode(escapedEpisodeName);
                        if (episodeFilter != null && !episodeFilter.Process(ref escapedEpisodeName, logger))
                        {
                            logger.Log("Episode did not match title filter; skipping...");
                            return true;
                        }

                        int episodeNum = int.Parse(episodeNumberMatch.Groups[1].Value);
                        if (episodeNum < minimumEpisode)
                        {
                            logger.Log("Episode number " + episodeNum + " is below " + minimumEpisode + "; skipping...");
                            Interlocked.Increment(ref missedEpisodeNumCount);
                            return true;
                        }
                        if (episodeNum > 1000)
                        {
                            logger.Log("Episode number " + episodeNum + " is probably invalid? Skipping...");
                            return true;
                        }

                        string fileNameBase = string.Format("{0} {1:D" + showDetails.EpisodeNumberPadding + "} - {2}", showDetails.FileNameBase, episodeNum, escapedEpisodeName);
                        foreach (var reservedChar in Path.GetInvalidFileNameChars())
                        {
                            fileNameBase = fileNameBase.Replace(reservedChar, '_');
                        }

                        FileInfo targetMp3File = new FileInfo(Path.Combine(stagingDirectory, fileNameBase + ".mp3"));
                        FileInfo targetOpusFile = new FileInfo(Path.Combine(outputDirectory.FullName, fileNameBase + ".opus"));

                        if (targetOpusFile.Exists)
                        {
                            logger.Log("Already have file " + targetOpusFile.Name, LogLevel.Std);
                            return true;
                        }

                        string downloadUri = downloadLinkMatch.Groups[1].Value;
                        downloadUri = WebUtility.HtmlDecode(downloadUri); // ?
                        await WebCrawler.DownloadFile(new Uri(downloadUri), targetMp3File, logger, webPage.Url.AbsoluteUri);

                        CommandLineParams encodeMetadata = new CommandLineParams();
                        encodeMetadata.AddParameter("-metadata", $"title=\"{CommandLine.EscapeConsoleString(escapedEpisodeName)}\"");
                        encodeMetadata.AddParameter("-metadata", $"artist=\"{CommandLine.EscapeConsoleString(showDetails.MetadataArtist)}\"");
                        encodeMetadata.AddParameter("-metadata", $"album=\"{CommandLine.EscapeConsoleString(showDetails.MetadataAlbum)}\"");
                        encodeMetadata.AddParameter("-metadata", string.Format("track=\"{0:D3}\"", episodeNum));
                        encodeMetadata.AddParameter("-metadata", $"album_artist=\"{CommandLine.EscapeConsoleString(showDetails.MetadataAlbumArtist)}\"");
                        encodeMetadata.AddParameter("-metadata", $"genre=\"{CommandLine.EscapeConsoleString(showDetails.MetadataGenre)}\"");

                        // Now queue up the mp3 -> opus conversion
                        workerThreadPool.EnqueueUserAsyncWorkItem(new BackgroundEncoderTaskClosure(
                            targetMp3File,
                            targetOpusFile,
                            showDetails.EncodeBitrate,
                            encodeMetadata,
                            logger).Run);

                        return true;
                    },
                    logger,
                    patterns);

                logger.Log("Waiting for worker threads to finish...");
                while (workerThreadPool.TotalWorkItems > 0)
                {
                    await workerThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                logger.Log("Done with all processing");
            }
        }

        private class BackgroundEncoderTaskClosure
        {
            public FileInfo inputMp3File;
            public FileInfo outputOpusFile;
            public CommandLineParams ffmpegCommandLine;
            public int encodeBitrateKbps;
            public ILogger logger;

            public BackgroundEncoderTaskClosure(FileInfo inputMp3File, FileInfo outputOpusFile, int encodeBitrateKbps, CommandLineParams ffmpegCommandLine, ILogger logger)
            {
                this.inputMp3File = inputMp3File;
                this.outputOpusFile = outputOpusFile;
                this.encodeBitrateKbps = encodeBitrateKbps;
                this.ffmpegCommandLine = ffmpegCommandLine;
                this.logger = logger;
            }

            public async Task Run()
            {
                await AudioProcessor.Ffmpeg_ConvertAudioToOpus(inputMp3File.FullName, outputOpusFile.FullName, logger, 1, encodeBitrateKbps, 0, ffmpegCommandLine);
                inputMp3File.Delete();
            }
        }
    }
}
