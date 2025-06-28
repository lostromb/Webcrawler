using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
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
    public class ACastShowDetails
    {
        public string ShowUrlName;
        public string InternalId;
        public string FileNameBase;
        public string MetadataArtist;
        public string MetadataAlbum;
        public string MetadataAlbumArtist;
        public string MetadataGenre;
        public int EpisodeNumberPadding = 3;
        public int EncodeBitrate = 16;
    }

    internal class ACastJsonEpisodesResponse
    {
        [JsonProperty("results")]
        internal List<ACastJsonEpisode> Results { get; set; }
    }

    internal class ACastJsonEpisode
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("creationDate")]
        public string CreationDate { get; set; }

        [JsonProperty("episodeNumber")]
        public int EpisodeNumber { get; set; }
    }

    public static class ACastPodcasts
    {
        public static async Task DownloadNewAcastPodcastEpisodes(
            ACastShowDetails showDetails,
            DirectoryInfo outputDirectory,
            int minimumEpisode = 0)
        {
            string stagingDirectory = @".\\staging";
            ILogger logger = new ConsoleLogger("WebCrawler", LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Vrb);

            RateLimiter limiter = new RateLimiter(2, 20);
            Regex downloadLinkMatcher = new Regex("<meta property=\\\"og:audio\\\" content=\\\"(.+?\\.mp3)\\\"");
            
            using (IThreadPool workerThreadPool = new FixedCapacityThreadPool(
                new TaskThreadPool(),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "WorkerThreadPool",
                Environment.ProcessorCount))
            {
                // Download the episode index
                string indexJsonUrl = "https://shows.acast.com/api/shows/" + showDetails.InternalId + "/episodes?paginate=true&page=1&results=1000";
                ACastJsonEpisodesResponse episodeIndex;

                logger.Log("Starting to download podcast " + showDetails.FileNameBase);
                using (HttpResponse resp = await WebCrawler.MakeRequest(new Uri(indexJsonUrl), logger))
                {
                    limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    episodeIndex = await resp.ReadContentAsJsonObjectAsync<ACastJsonEpisodesResponse>(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                foreach (ACastJsonEpisode episode in episodeIndex.Results)
                {
                    //if (episodeFilter != null && !episodeFilter.Process(ref escapedEpisodeName, logger))
                    //{
                    //    logger.Log("Episode did not match title filter; skipping...");
                    //    return true;
                    //}

                    if (episode.EpisodeNumber < minimumEpisode)
                    {
                        logger.Log("Episode number " + episode.EpisodeNumber + " is below " + minimumEpisode + "; skipping...");
                        continue;
                    }

                    if (string.IsNullOrEmpty(episode.Alias))
                    {
                        continue;
                    }

                    string fileNameBase = string.Format("{0} {1:D" + showDetails.EpisodeNumberPadding + "} - {2}", showDetails.FileNameBase, episode.EpisodeNumber, episode.Title);
                    foreach (var reservedChar in Path.GetInvalidFileNameChars())
                    {
                        fileNameBase = fileNameBase.Replace(reservedChar, '_');
                    }

                    FileInfo targetMp3File = new FileInfo(Path.Combine(stagingDirectory, fileNameBase + ".mp3"));
                    FileInfo targetOpusFile = new FileInfo(Path.Combine(outputDirectory.FullName, fileNameBase + ".opus"));

                    if (targetOpusFile.Exists)
                    {
                        logger.Log("Already have file " + targetOpusFile.Name, LogLevel.Std);
                        continue;
                    }

                    // Get the mp3 link
                    string episodePageUrl = $"https://shows.acast.com/{showDetails.ShowUrlName}/episodes/{episode.Alias}";
                    string episodePageContent;
                    using (HttpResponse resp = await WebCrawler.MakeRequest(new Uri(episodePageUrl), logger))
                    {
                        limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                        episodePageContent = await resp.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    string downloadUri = StringUtils.RegexRip(downloadLinkMatcher, episodePageContent, 1, logger);
                    if (string.IsNullOrEmpty(downloadUri))
                    {
                        logger.Log("Empty download URI " + episode.Alias, LogLevel.Wrn);
                        continue;
                    }

                    await WebCrawler.DownloadFile(new Uri(downloadUri), targetMp3File, logger);

                    CommandLineParams encodeMetadata = new CommandLineParams();
                    encodeMetadata.AddParameter("-metadata", $"title=\"{CommandLine.EscapeConsoleString(episode.Title)}\"");
                    encodeMetadata.AddParameter("-metadata", $"artist=\"{CommandLine.EscapeConsoleString(showDetails.MetadataArtist)}\"");
                    encodeMetadata.AddParameter("-metadata", $"album=\"{CommandLine.EscapeConsoleString(showDetails.MetadataAlbum)}\"");
                    encodeMetadata.AddParameter("-metadata", string.Format("track=\"{0:D3}\"", episode.EpisodeNumber));
                    encodeMetadata.AddParameter("-metadata", $"album_artist=\"{CommandLine.EscapeConsoleString(showDetails.MetadataAlbumArtist)}\"");
                    encodeMetadata.AddParameter("-metadata", $"genre=\"{CommandLine.EscapeConsoleString(showDetails.MetadataGenre)}\"");

                    // Now queue up the mp3 -> opus conversion
                    workerThreadPool.EnqueueUserAsyncWorkItem(new BackgroundEncoderTaskClosure(
                        targetMp3File,
                        targetOpusFile,
                        showDetails.EncodeBitrate,
                        encodeMetadata,
                        logger).Run);
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
