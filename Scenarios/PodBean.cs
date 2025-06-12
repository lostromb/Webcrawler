using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Extensions.NativeAudio.Components;
using Newtonsoft.Json.Linq;
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
    public class PodBeanDetails
    {
        public string InternalId;
        public string FileNameBase;
        public string MetadataArtist;
        public string MetadataAlbum;
        public string MetadataAlbumArtist;
        public string MetadataGenre;
        public int EncodeBitrate = 16;
    }

    public static class PodBean
    {
        public static async Task DownloadPodcast(
            PodBeanDetails showDetails,
            DirectoryInfo outputDirectory)
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
                Regex indexPageRegex = new Regex("^https:\\/\\/" + showDetails.InternalId + "\\.podbean\\.com\\/page\\/\\d+$");
                Regex episodePageRegex = new Regex("^https:\\/\\/www\\.podbean\\.com\\/site\\/EpisodeDownload\\/[^\\?]+$");
                patterns.Add(indexPageRegex);
                patterns.Add(episodePageRegex);
                Regex metadataParser = new Regex("<script type=\\\"application\\/ld\\+json\\\">([\\w\\W]+?)<\\/script>");

                int filesAlreadyDownloaded = 0;

                logger.Log("Starting to download podcast " + showDetails.InternalId);
                for (int page = 1; page < 20; page++)
                {
                    if (filesAlreadyDownloaded > 10)
                    {
                        break;
                    }

                    string baseUrl = $"https://{showDetails.InternalId}.podbean.com/page/{page}";
                    await WebCrawler.Crawl(new Uri(baseUrl),
                        async (webPage) =>
                        {
                            if (filesAlreadyDownloaded > 10)
                            {
                                logger.Log("Reached existing file limit; assuming we have all other episodes already");
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

                            logger.Log("Got an episode page, parsing metadata...");
                            Match metadataMatch = metadataParser.Match(webPage.Html);
                            if (!metadataMatch.Success)
                            {
                                logger.Log("Failed to parse metadata", LogLevel.Err);
                                return true;
                            }

                            string metadataJson = WebUtility.HtmlDecode(metadataMatch.Groups[1].Value);
                            JObject metadata = JObject.Parse(metadataJson);
                            string episodePublishDate = metadata["datePublished"].Value<string>();
                            string episodeName = metadata["name"].Value<string>();
                            string episodeDescription = metadata["description"].Value<string>();
                            string episodeMp3Link = metadata["associatedMedia"]["contentUrl"].Value<string>();

                            string fileNameBase = string.Format("{0} {1} - {2}", showDetails.FileNameBase, episodePublishDate, episodeName);
                            foreach (var reservedChar in Path.GetInvalidFileNameChars())
                            {
                                fileNameBase = fileNameBase.Replace(reservedChar, '_');
                            }

                            FileInfo targetMp3File = new FileInfo(Path.Combine(stagingDirectory, fileNameBase + ".mp3"));
                            FileInfo targetOpusFile = new FileInfo(Path.Combine(outputDirectory.FullName, fileNameBase + ".opus"));

                            if (targetOpusFile.Exists)
                            {
                                logger.Log("Already have file " + targetOpusFile.Name, LogLevel.Std);
                                filesAlreadyDownloaded++;
                                return true;
                            }

                            await WebCrawler.DownloadFile(new Uri(episodeMp3Link), targetMp3File, logger, webPage.Url.AbsoluteUri);

                            CommandLineParams encodeMetadata = new CommandLineParams();
                            encodeMetadata.AddParameter("-metadata", $"{FfmpegMetadataKey.TITLE}=\"{CommandLine.EscapeConsoleString(episodeName)}\"");
                            encodeMetadata.AddParameter("-metadata", $"{FfmpegMetadataKey.ARTIST}=\"{CommandLine.EscapeConsoleString(showDetails.MetadataArtist)}\"");
                            encodeMetadata.AddParameter("-metadata", $"{FfmpegMetadataKey.ALBUM}=\"{CommandLine.EscapeConsoleString(showDetails.MetadataAlbum)}\"");
                            //encodeMetadata.AddParameter("-metadata", $"COMMENT=\"{CommandLine.EscapeConsoleString(episodeDescription)}\"");
                            encodeMetadata.AddParameter("-metadata", $"{FfmpegMetadataKey.DATE}=\"{CommandLine.EscapeConsoleString(episodePublishDate)}\"");
                            encodeMetadata.AddParameter("-metadata", $"{FfmpegMetadataKey.ALBUM_ARTIST}=\"{CommandLine.EscapeConsoleString(showDetails.MetadataAlbumArtist)}\"");
                            encodeMetadata.AddParameter("-metadata", $"{FfmpegMetadataKey.GENRE}=\"{CommandLine.EscapeConsoleString(showDetails.MetadataGenre)}\"");

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
                }

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
