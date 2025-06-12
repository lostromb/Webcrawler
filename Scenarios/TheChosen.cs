using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios
{
    public static class TheChosen
    {
        public static async Task Download()
        {
            ILogger logger = new ConsoleLogger("WebCrawler",LogLevel.Std | LogLevel.Wrn | LogLevel.Err);
            RateLimiter limiter = new RateLimiter(2, 20);

            string downloadTargetDirectory = Environment.CurrentDirectory + "\\staging";
            if (!Directory.Exists(downloadTargetDirectory))
            {
                logger.Log("Creating staging directory " + downloadTargetDirectory);
                Directory.CreateDirectory(downloadTargetDirectory);
            }

            IFileSystem stagingFileSystem = new RealFileSystem(logger.Clone("FileSystem"), downloadTargetDirectory);
            IHttpClientFactory httpClientFactory = new PortableHttpClientFactory(
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty);

            IHttpClient httpClient = httpClientFactory.CreateHttpClient(new Uri("https://media.angelstudios.com"), logger.Clone("HttpClient"));

            List<Tuple<string, string>> masterPlaylistEntries = new List<Tuple<string, string>>();

            //masterPlaylistEntries.Add(new Tuple<string, string>("1x00 - The Shepherd (Pilot Episode)", "https://media.angelstudios.com/copied-content/masters/3ad11d2f-1a88-4e2e-935d-5df81f2c9668.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x01 - I Have Called You By Name", "https://media.angelstudios.com/copied-content/masters/e1d257c5-1737-43f3-a352-0b07b2fe89e2.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x02 - Shabbat", "https://media.angelstudios.com/copied-content/masters/e47c505b-fcf0-40f4-aadb-831325a62851.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x03 - Jesus Loves the Little Children", "https://media.angelstudios.com/copied-content/masters/43d3ae2e-c3c2-48bf-afe0-af799d0657fe.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x04 - The Rock on Which It Is Built", "https://media.angelstudios.com/copied-content/masters/dbe3721c-6e55-41fc-a80b-8d80c388b47e.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x05 - The Wedding Gift", "https://media.angelstudios.com/copied-content/masters/5f3895e3-9c0b-4149-8fb3-71a08cf87bb7.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x06 - Indescribable Compassion", "https://media.angelstudios.com/copied-content/masters/dd09a560-c423-4b25-929c-58ecd0f49d05.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x07 - Invitations", "https://media.angelstudios.com/copied-content/masters/12c339c7-a434-4c0e-8b1f-4009e48cc137.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("1x08 - I Am He", "https://media.angelstudios.com/copied-content/masters/304e6876-3e51-4603-9c05-b6ba6456a2b9.m3u8"));

            //masterPlaylistEntries.Add(new Tuple<string, string>("2x00 - The Messengers (Christmas Special)", "https://media.angelstudios.com/copied-content/masters/a37ac587-7063-4421-8a94-05689e8b605e.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x01 - Thunder", "https://media.angelstudios.com/copied-content/masters/97d6e90f-286b-4fb0-ad12-317d6aae6e9a.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x02 - I Saw You", "https://media.angelstudios.com/copied-content/masters/f63dc48b-1b24-4e60-bf1a-93ac87b05d06.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x03 - Matthew 4:24", "https://media.angelstudios.com/copied-content/masters/b5cd5415-3d96-4bda-80a7-286ceab9854a.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x04 - The Perfect Opportunity", "https://media.angelstudios.com/copied-content/masters/94229242-cf5e-41f7-8b0b-bedf53261c22.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x05 - Spirit", "https://media.angelstudios.com/copied-content/masters/b5b88945-47c9-4866-a71e-bf2d4bc7cfad.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x06 - Unlawful", "https://media.angelstudios.com/copied-content/masters/e603086f-a03b-42e4-a9f7-10b36ee30c8f.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x07 - Reckoning", "https://media.angelstudios.com/copied-content/masters/5638b84b-3085-4363-8dd1-0c15aeccc938.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("2x08 - Beyond Mountains", "https://media.angelstudios.com/copied-content/masters/b6302cbd-f856-4af1-b0b0-b49f072350e7.m3u8"));

            //masterPlaylistEntries.Add(new Tuple<string, string>("3x01 - Homecoming", "https://media.angelstudios.com/copied-content/masters/63a17449-6bde-4b8a-a4b6-8f068b84c9ec.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x02 - Two by Two", "https://media.angelstudios.com/copied-content/masters/266db866-464f-4f6b-b0af-3b37caf80f78.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x03 - Physician, Heal Yourself", "https://media.angelstudios.com/copied-content/masters/80f86d64-feab-4df4-95ac-f01775f80da2.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x04 - Clean, Part 1", "https://media.angelstudios.com/copied-content/masters/a602cd41-842b-4c89-8a01-618b3e7b5fd5.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x05 - Clean, Part 2", "https://media.angelstudios.com/copied-content/masters/2e243b09-8e3a-42c9-9421-6988748752c1.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x06 - Intensity in Tent City", "https://media.angelstudios.com/copied-content/masters/0384fcc5-9f3b-4aaf-a0fe-f7159965119f.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x07 - Ears to Hear", "https://media.angelstudios.com/copied-content/masters/3b291341-d0c7-4874-ae9a-e38e27a92afc.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("3x08 - Sustenance", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));

            // season 4
            // Master playlist: https://api.frontrow.cc/channels/12884901895/VIDEO/184683596182/v2/hls.m3u8?viewerToken=(token)
            // Audio playlist: https://fastly.frontrowcdn.com/channels/12884901895/VIDEO/184683596182/279172878841.m3u8
            // Audio segments: https://fastly.frontrowcdn.com/channels/12884901895/VIDEO/184683596182/audio/279172878841/seg_0.aac
            // Video segments: https://fastly.frontrowcdn.com/channels/12884901895/VIDEO/184683596182/video/480p/seg_0.ts?viewerToken=(token)
            // English subs: https://fastly.frontrowcdn.com/channels/12884901895/VIDEO/184683596182/subtitle/279172890650/seg_0.vtt

            masterPlaylistEntries.Add(new Tuple<string, string>("4x01 - Promises", "https://api.frontrow.cc/channels/12884901895/VIDEO/184683596182/v2/hls.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x02 - Confessions", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x03 - Moon to Blood", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x04 - Calm Before", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x05 - Sitting, Serving, Scheming", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x06 - Dedication", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x07 - The Last Sign", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));
            //masterPlaylistEntries.Add(new Tuple<string, string>("4x08 - Humble", "https://media.angelstudios.com/copied-content/masters/bfc8e9c1-69fa-4a69-902b-a1070243b9a4.m3u8"));

            string viewerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwczovL2Zyb250cm93LmNjL3VzZXJfaWQiOjE4MDM5NzAwODg0MCwiaHR0cHM6Ly9mcm9udHJvdy5jYy9jaGFubmVsX2lkIjoxMjg4NDkwMTg5NSwicm9sZSI6Ik1FTUJFUiIsImdlbyI6IlVTIiwibGFuZ3VhZ2UiOiJFTiIsImRldmljZV9pZCI6IiIsImRldmljZV9vcyI6IndlYiIsImRldmljZV9vc192ZXJzaW9uIjoiIiwiZGV2aWNlX3R5cGUiOiJXRUIiLCJkZXZpY2VfdmVyc2lvbiI6IjIuNC4xMSIsImlzcyI6Imh0dHBzOi8vYXBpLmZyb250cm93LmNjIiwiYXVkIjpbImh0dHBzOi8vYXBpLmZyb250cm93LmNjIl0sImV4cCI6MTcyNTI2NTU5OCwiaWF0IjoxNzI1MjQzOTk4LCJqdGkiOiJmMTQxMzg1NC1iYzFlLTQzZjQtOTdjOC04MWY4YTEyZjRjODIifQ.DSYJvQAgGf2KhVWdgHcobQuYsFXj95FD-qyKuHrbXy8";

            foreach (Tuple<string, string> masterPlaylistEntry in masterPlaylistEntries)
            {
                List<SubtitleInformation> subtitles = new List<SubtitleInformation>();
                List<VirtualPath> tempStagingFiles = new List<VirtualPath>();

                try
                {
                    // Download the master playlist
                    VirtualPath masterPlaylistFileName = new VirtualPath(masterPlaylistEntry.Item1 + ".m3u8");
                    logger.Log($"Downloading master playlist for {masterPlaylistEntry.Item1}");
                    VirtualPath masterPlaylistFile = await DownloadFileToStagingIfNotPresent(
                        httpClient,
                        string.Format("{0}?viewerToken={1}", masterPlaylistEntry.Item2, viewerToken),
                        stagingFileSystem,
                        logger,
                        limiter,
                        masterPlaylistFileName);

                    string finalOutputVideoName = SanitizeFileName(masterPlaylistEntry.Item1) + ".mkv";
                    tempStagingFiles.Add(masterPlaylistFile);
                    if (stagingFileSystem.Exists(new VirtualPath(finalOutputVideoName)))
                    {
                        logger.Log("We already have the final video for " + finalOutputVideoName);
                        continue;
                    }

                    //string masterPlaylistData = string.Join("\n", await stagingFileSystem.ReadLinesAsync(masterPlaylistFile));
                    string masterPlaylistData = string.Join(string.Empty, (await stagingFileSystem.ReadLinesAsync(masterPlaylistFile)).Select((s) => Convert.FromBase64String(s)));

                    // Fetch all subtitles
                    logger.Log("Parsing subtitle playlist urls");
                    //Regex subtitlePlaylistUrlMatcher = new Regex("#EXT-X-MEDIA:TYPE=SUBTITLES,URI=\"(.+?)\",.{1,30},LANGUAGE=\"(.+?)\",NAME=\"(.+?)\"");
                    Regex subtitlePlaylistUrlMatcher = new Regex("#EXT-X-MEDIA:TYPE=SUBTITLES.{1,30},NAME=\"(.+?)\",.{1,50}LANGUAGE=\"(.+?)\",URI=\"(.+?)\"");
                    Regex subtitleUrlMatcher = new Regex("http.+?\\.vtt");
                    foreach (Match subtitlePlaylistUrlMatch in subtitlePlaylistUrlMatcher.Matches(masterPlaylistData))
                    {
                        string url = subtitlePlaylistUrlMatch.Groups[1].Value;
                        LanguageCode lang = LanguageCode.TryParse(subtitlePlaylistUrlMatch.Groups[2].Value);
                        if (lang != null)
                        {
                            VirtualPath localPlaylistFile = new VirtualPath("subtitle." + lang.Iso639_2 + ".m3u8");
                            logger.Log("Downloading " + localPlaylistFile.FullName);
                            await DownloadFileToStagingIfNotPresent(
                                httpClient,
                                url,
                                stagingFileSystem,
                                logger,
                                limiter,
                                localPlaylistFile);

                            tempStagingFiles.Add(localPlaylistFile);

                            string subPlaylistData = string.Join("\n", await stagingFileSystem.ReadLinesAsync(localPlaylistFile));
                            Match subtitleUrlMatch = subtitleUrlMatcher.Match(subPlaylistData);

                            if (!subtitleUrlMatch.Success)
                            {
                                logger.Log("Could not find subtitle URL in playlist file " + localPlaylistFile.Name, LogLevel.Wrn);
                                continue;
                            }

                            // Get the actual .vtt file for the sub
                            VirtualPath localVttFile = new VirtualPath(lang.Iso639_2 + ".vtt");
                            url = subtitleUrlMatch.Value;
                            logger.Log("Downloading " + localVttFile.FullName);
                            await DownloadFileToStagingIfNotPresent(
                                httpClient,
                                url,
                                stagingFileSystem,
                                logger,
                                limiter,
                                localVttFile);

                            tempStagingFiles.Add(localVttFile);

                            subtitles.Add(new SubtitleInformation()
                            {
                                Language = lang,
                                LanguageName = subtitlePlaylistUrlMatch.Groups[3].Value,
                                PlaylistUrl = url,
                                LocalPlaylistPath = localPlaylistFile,
                                VttPath = localVttFile
                            });
                        }
                    }

                    // Get the audio stream
                    logger.Log("Extracting audio playlist URLs");
                    Regex audioPlaylistUrlMatcher = new Regex("#EXT-X-MEDIA:TYPE=AUDIO,[\\w\\W]{1,50}LANGUAGE=\\\"(.+?)\\\"[\\w\\W]{1,50}URI=\\\"(https:\\/\\/.+?m3u8)\\\"");


                    // Figure out the video stream from master playlist
                    logger.Log("Extracting video playlist URLs");
                    Regex videoPlaylistUrlMatcher = new Regex("#EXT-X-STREAM-INF:BANDWIDTH=(\\d+).{1,100}RESOLUTION=(.+?),[\\w\\W]{1,50}(https:\\/\\/.+?m3u8)");
                    int highestBandwidth = 0;
                    string videoPlaylistUrl = string.Empty;
                    foreach (Match videoPlaylistUrlMatch in videoPlaylistUrlMatcher.Matches(masterPlaylistData))
                    {
                        logger.Log("Bandwidth " + videoPlaylistUrlMatch.Groups[1].Value + " resolution " + videoPlaylistUrlMatch.Groups[2].Value);
                        int bandwidth = int.Parse(videoPlaylistUrlMatch.Groups[1].Value);
                        if (bandwidth > highestBandwidth)
                        {
                            videoPlaylistUrl = videoPlaylistUrlMatch.Groups[3].Value;
                            highestBandwidth = bandwidth;
                        }
                    }

                    if (string.IsNullOrEmpty(videoPlaylistUrl))
                    {
                        logger.Log("Failed to find video playlist", LogLevel.Err);
                        return;
                    }

                    // Download video playlist
                    logger.Log("Downloading video playlist " + videoPlaylistUrl);
                    VirtualPath videoPlaylistFile = await DownloadFileToStagingIfNotPresent(
                        httpClient,
                        videoPlaylistUrl,
                        stagingFileSystem,
                        logger,
                        limiter);

                    tempStagingFiles.Add(videoPlaylistFile);
                    string videoPlaylistData = string.Join("\n", await stagingFileSystem.ReadLinesAsync(videoPlaylistFile));

                    // Download video segments
                    Regex videoSegmentUrlMatcher = new Regex("http.+?\\/\\d+\\.ts");
                    MatchCollection videoSegmentMatches = videoSegmentUrlMatcher.Matches(videoPlaylistData);
                    List<VirtualPath> segmentFiles = new List<VirtualPath>();
                    foreach (Match videoSegmentMatch in videoSegmentMatches)
                    {
                        VirtualPath videoSegmentFile = await DownloadFileToStagingIfNotPresent(
                            httpClient,
                            videoSegmentMatch.Value,
                            stagingFileSystem,
                            logger,
                            limiter);

                        tempStagingFiles.Add(videoSegmentFile);
                        segmentFiles.Add(videoSegmentFile);
                    }

                    // Create an ffmpeg playlist with all our downloaded .ts files
                    StringBuilder concatArgBuilder = new StringBuilder();
                    concatArgBuilder.Append("\"concat:");
                    bool firstFile = true;
                    VirtualPath concatFilePath = new VirtualPath("Concat.txt");
                    using (Stream concatWriteStream = stagingFileSystem.OpenStream(concatFilePath, FileOpenMode.Create, FileAccessMode.Write))
                    using (StreamWriter writer = new StreamWriter(concatWriteStream, StringUtils.UTF8_WITHOUT_BOM))
                    {
                        foreach (var segmentFile in segmentFiles)
                        {
                            writer.WriteLine($"file \'{segmentFile.Name}\'");
                            if (!firstFile)
                            {
                                concatArgBuilder.Append("|");
                            }

                            concatArgBuilder.Append(segmentFile.Name);
                            firstFile = false;
                        }

                        writer.Close();
                    }

                    tempStagingFiles.Add(concatFilePath);
                    concatArgBuilder.Append("\"");

                    // Stitch .ts files together into one big file
                    //string ffmpegArgs = $"-f concat -i {concatFilePath.Name} -c copy -y video.ts";
                    string ffmpegArgs = $" -i {concatArgBuilder} -c copy -y video.ts";
                    await RunFfmpeg(new DirectoryInfo(downloadTargetDirectory), ffmpegArgs);
                    tempStagingFiles.Add(new VirtualPath("video.ts"));

                    // Now merge that video with all of the subtitles
                    StringBuilder inputFileBuilder = new StringBuilder();
                    StringBuilder mapParamBuilder = new StringBuilder();
                    inputFileBuilder.Append($" -i video.ts");
                    mapParamBuilder.Append($" -map 0:v -map 0:a");

                    int mapIdx = 1;
                    foreach (SubtitleInformation sub in subtitles)
                    {
                        inputFileBuilder.Append($" -i {sub.VttPath.Name}");
                        mapParamBuilder.Append($" -map {mapIdx}:s:0 -metadata:s:s:{mapIdx - 1} language=\"{sub.Language.CountryAgnostic().Iso639_2}\" -metadata:s:s:{mapIdx - 1} title=\"{sub.LanguageName}\"");
                        mapIdx++;
                    }

                    string mergedFileName = masterPlaylistFile.NameWithoutExtension + ".mkv";
                    ffmpegArgs = $"{inputFileBuilder} {mapParamBuilder} -c:v copy -c:a copy -c:s srt \"{finalOutputVideoName}\"";
                    await RunFfmpeg(new DirectoryInfo(downloadTargetDirectory), ffmpegArgs);
                }
                catch (Exception e)
                {
                    logger.Log(e);
                }
                finally
                {
                    // And clear all our temp staging files
                    foreach (VirtualPath tempFile in tempStagingFiles)
                    {
                        try
                        {
                            //stagingFileSystem.Delete(tempFile);
                        }
                        catch (Exception e)
                        {
                            logger.Log(e);
                        }
                    }
                }
            }
        }

        private static async Task<VirtualPath> DownloadFileToStagingIfNotPresent(
            IHttpClient httpClient,
            string url,
            IFileSystem stagingFileSystem,
            ILogger queryLogger,
            RateLimiter limiter,
            VirtualPath overrideFileName = null)
        {
            VirtualPath localFileName = overrideFileName ?? new VirtualPath(url.Substring(url.LastIndexOf('/') + 1));
            if (await stagingFileSystem.ExistsAsync(localFileName))
            {
                queryLogger.Log("Already have " + localFileName.Name);
                return localFileName;
            }

            limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
            queryLogger.Log("Downloading " + localFileName.Name);
            using (HttpRequest request = HttpRequest.CreateOutgoing(url))
            using (HttpResponse resp = await httpClient.SendRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, queryLogger))
            {
                if (resp.ResponseCode != 200)
                {
                    throw new Exception("Could not download file " + url);
                }

                try
                {
                    using (NonRealTimeStream contentStream = resp.ReadContentAsStream())
                    using (Stream fileStream = await stagingFileSystem.OpenStreamAsync(localFileName, FileOpenMode.CreateNew, FileAccessMode.Write))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e);
                    await stagingFileSystem.DeleteAsync(localFileName);
                }
            }

            return localFileName;
        }

        private static async Task RunFfmpeg(DirectoryInfo workingDirectory, string commandLine)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                WorkingDirectory = workingDirectory.FullName,
                Arguments = commandLine,
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            Process ffmpegProcess = Process.Start(processInfo);

            while (!ffmpegProcess.HasExited)
            {
                await Task.Delay(100);
            }
        }

        private class SubtitleInformation
        {
            public LanguageCode Language { get; set; }
            public string LanguageName { get; set; }
            public string PlaylistUrl { get; set; }
            public VirtualPath LocalPlaylistPath { get; set; }
            public VirtualPath VttPath { get; set; }
        }

        private static string SanitizeFileName(string input)
        {
            foreach (char notAllowed in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(notAllowed, '_');
            }

            return input;
        }
    }
}
