using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebCrawler.InterProcess;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class TalkProcessor
    {
        private const int MAX_NATURAL_TITLE_LENGTH = 80;

        public static string GetFilePathM4AOrdinalNaming(DirectoryInfo baseDirectory, ConferenceTalk talkInfo)
        {
            string m4aFolderPath = Path.Combine(
                baseDirectory.FullName,
                "aac-ord",
                talkInfo.Language.ToBcp47Alpha3String(),
                talkInfo.Conference.Year.ToString() + "-" + (talkInfo.Conference.Phase == ConferencePhase.April ? "04" : "10"));
            string m4aFilePath = Path.Combine(m4aFolderPath, talkInfo.InternalName + ".m4a");
            return m4aFilePath;
        }

        public static string GetFilePathOpusOrdinalNaming(DirectoryInfo baseDirectory, ConferenceTalk talkInfo)
        {
            string m4aFolderPath = Path.Combine(
                baseDirectory.FullName,
                "opus-ord",
                talkInfo.Language.ToBcp47Alpha3String(),
                talkInfo.Conference.Year.ToString() + "-" + (talkInfo.Conference.Phase == ConferencePhase.April ? "04" : "10"));
            string m4aFilePath = Path.Combine(m4aFolderPath, talkInfo.InternalName + ".opus");
            return m4aFilePath;
        }

        public static string GetFilePathM4ANaturalNaming(DirectoryInfo baseDirectory, ConferenceTalk talkInfo, LocalizedStrings localeStrings)
        {
            string trimmedTalkTitle = talkInfo.Title;
            while (trimmedTalkTitle.Length > MAX_NATURAL_TITLE_LENGTH && trimmedTalkTitle.Contains(' '))
            {
                trimmedTalkTitle = trimmedTalkTitle.Substring(0, trimmedTalkTitle.LastIndexOf(' '));
            }

            string outputFileNameBase = Common.SanitizeFileName(string.Format("{0} - {1}{2} - {3}",
                talkInfo.Speaker, talkInfo.SessionIndex, talkInfo.TalkIndex, trimmedTalkTitle));
            string m4aFolderPath = Path.Combine(
                baseDirectory.FullName,
                "aac-nat",
                talkInfo.Language.ToBcp47Alpha3String(),
                Common.SanitizeFolderName(localeStrings.ConferenceName(talkInfo.Conference)));
            string m4aFilePath = Path.Combine(m4aFolderPath, outputFileNameBase + ".m4a");
            return m4aFilePath;
        }

        public static string GetFilePathOpusNaturalNaming(DirectoryInfo baseDirectory, ConferenceTalk talkInfo, LocalizedStrings localeStrings)
        {
            string trimmedTalkTitle = talkInfo.Title;
            while (trimmedTalkTitle.Length > MAX_NATURAL_TITLE_LENGTH && trimmedTalkTitle.Contains(' '))
            {
                trimmedTalkTitle = trimmedTalkTitle.Substring(0, trimmedTalkTitle.LastIndexOf(' '));
            }

            string outputFileNameBase = Common.SanitizeFileName(string.Format("{0} - {1}{2} - {3}",
                talkInfo.Speaker, talkInfo.SessionIndex, talkInfo.TalkIndex, trimmedTalkTitle));
            string opusFolderPath = Path.Combine(
                baseDirectory.FullName,
                "opus-nat",
                talkInfo.Language.ToBcp47Alpha3String(),
                Common.SanitizeFolderName(localeStrings.ConferenceName(talkInfo.Conference)));
            string opusFilePath = Path.Combine(opusFolderPath, outputFileNameBase + ".opus");
            return opusFilePath;
        }

        public static async Task DownloadTalkAndConvert(
            ConferenceTalk talkInfo,
            WebClient commonWebClient,
            string downloadLink,
            DirectoryInfo stagingDir,
            ILogger logger,
            LocalizedStrings localeStrings)
        {
            // Download the file
            string baseFileName = Common.SanitizeFileName(string.Format("{0} - {1}{2} - {3} - {4}",
                talkInfo.Speaker, talkInfo.SessionIndex, talkInfo.TalkIndex, talkInfo.Title, talkInfo.Language.ToBcp47Alpha3String()));
            string downloadedAudioFullPath;
            if (downloadLink.Contains("m3u8"))
            {
                // If it's an M3U8 then we have to download each AAC transport segment and stitch them. Blah
                string masterM3UFilePath = Path.Combine(stagingDir.FullName, baseFileName + "-master.m3u8");
                await commonWebClient.DownloadFileTaskAsync(downloadLink, masterM3UFilePath);

                // Find the link to the audio m3u8 file
                string[] masterM3ULines = File.ReadAllLines(masterM3UFilePath);
                Regex audioM3ULinkMatcher = new Regex("GROUP-ID=\"(audio-\\d)\".+?URI=\\\"(.+?)\\\"");
                Regex audioSegmentLinkMatcher = new Regex("^http.+?\\.ts.*$");
                string audioM3ULink = null;
                foreach (string s in masterM3ULines)
                {
                    Match m = audioM3ULinkMatcher.Match(s);
                    if (m.Success)
                    {
                        audioM3ULink = m.Groups[2].Value;
                    }
                }

                if (string.IsNullOrEmpty(audioM3ULink))
                {
                    logger.Log("Couldn't find audio m3u link in master playlist file", LogLevel.Err);
                    return;
                }

                string audioM3UFilePath = Path.Combine(stagingDir.FullName, baseFileName + "-audio.m3u8");
                await commonWebClient.DownloadFileTaskAsync(audioM3ULink, audioM3UFilePath);
                string[] audioM3ULines = File.ReadAllLines(audioM3UFilePath);
                List<FileInfo> segmentFiles = new List<FileInfo>();

                int segmentIdx = 0;
                string concatFilePath = Path.Combine(stagingDir.FullName, baseFileName + "-concat.txt");
                using (Stream concatWriteStream = new FileStream(concatFilePath, FileMode.Create, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(concatWriteStream, StringUtils.UTF8_WITHOUT_BOM))
                {
                    foreach (string s in audioM3ULines)
                    {
                        if (audioSegmentLinkMatcher.IsMatch(s))
                        {
                            string segmentPath = Path.Combine(stagingDir.FullName, baseFileName + "-segment" + segmentIdx + ".ts");
                            await commonWebClient.DownloadFileTaskAsync(s, segmentPath);
                            segmentFiles.Add(new FileInfo(segmentPath));
                            segmentIdx++;
                            writer.WriteLine($"file \'{segmentPath}\'");
                        }
                    }

                    writer.Close();
                }

                // Stitch .ts files together into one big file and containerize it into mp4
                downloadedAudioFullPath = Path.Combine(stagingDir.FullName, baseFileName + ".m4a");

                CommandLineParams ffmpegArgs = new CommandLineParams();
                ffmpegArgs.AddParameter("-f", "concat");
                ffmpegArgs.AddParameter("-safe", "0");
                ffmpegArgs.AddParameter("-i", $"\"{concatFilePath}\"");
                ffmpegArgs.AddParameter("-c", "copy");
                ffmpegArgs.AddParameter("-map", "0:a:0");
                ffmpegArgs.AddParameter("-y");
                ffmpegArgs.AddParameter($"\"{downloadedAudioFullPath}\"");
                CommandLineResult procResult = await AudioProcessor.Ffmpeg_EncodeRaw(ffmpegArgs, logger);

                // Clean up all our intermediate files
                File.Delete(masterM3UFilePath);
                File.Delete(audioM3UFilePath);
                File.Delete(concatFilePath);
                foreach (FileInfo segmentFile in segmentFiles)
                {
                    segmentFile.Delete();
                }
                if (procResult.ProcessExitCode != 0)
                {
                    logger.Log("ffmpeg returned error code " + procResult.ProcessExitCode + " while encoding " + downloadedAudioFullPath, LogLevel.Err);
                    return;
                }
            }
            else
            {
                // If it's an MP4 video then easy, just download the mp4
                string mp4FileName = baseFileName + ".mp4";
                downloadedAudioFullPath = Path.Combine(stagingDir.FullName, mp4FileName);

                logger.Log("Downloading " + mp4FileName);
                await commonWebClient.DownloadFileTaskAsync(downloadLink, downloadedAudioFullPath);
                logger.Log("Downloaded " + mp4FileName, LogLevel.Vrb);
            }

            await Convert(talkInfo, downloadedAudioFullPath, stagingDir, logger, localeStrings);
        }

        private static async Task<bool> Convert(
            ConferenceTalk talkInfo,
            string inputMediaFile,
            DirectoryInfo outputBaseDirectory,
            ILogger logger,
            LocalizedStrings localeStrings)
        {
            // Convert the input file to .wav
            string intermediateFileNameBase = Common.SanitizeFileName(string.Format("{0} - {1}{2} - {3} - {4}",
                talkInfo.Speaker, talkInfo.SessionIndex, talkInfo.TalkIndex, talkInfo.Title, talkInfo.Language.ToBcp47Alpha3String()));
            string rawWavFilePath = Path.Combine(outputBaseDirectory.FullName, intermediateFileNameBase + ".wav");

            CommandLineResult procResult = await AudioProcessor.Ffmpeg_ConvertAudioToWav(inputMediaFile, rawWavFilePath, logger, channels: 1);
            if (procResult.ProcessExitCode != 0)
            {
                logger.Log("FFMpeg returned error code " + procResult.ProcessExitCode + " while encoding " + rawWavFilePath);
                return false;
            }

            // Inspect the wav to determine boundaries and peak volume
            string normalizedWavFilePath = Path.Combine(outputBaseDirectory.FullName, intermediateFileNameBase + "_normalized.wav");
            AudioStatistics stats = await AudioProcessor.GetAudioStatistics(rawWavFilePath, logger);
            await AudioProcessor.NormalizeAudio(rawWavFilePath, normalizedWavFilePath, stats, true, logger);

            string m4aNoMetaFilePath = Path.Combine(outputBaseDirectory.FullName, intermediateFileNameBase + "_nometa.m4a");
            string m4aOrdinalFilePath = GetFilePathM4AOrdinalNaming(outputBaseDirectory, talkInfo);
            string m4aNaturalFilePath = GetFilePathM4ANaturalNaming(outputBaseDirectory, talkInfo, localeStrings);
            string opusOrdinalFilePath = GetFilePathOpusOrdinalNaming(outputBaseDirectory, talkInfo);
            string opusNaturalFilePath = GetFilePathOpusNaturalNaming(outputBaseDirectory, talkInfo, localeStrings);

            Directory.CreateDirectory(new FileInfo(m4aOrdinalFilePath).Directory.FullName);
            Directory.CreateDirectory(new FileInfo(m4aNaturalFilePath).Directory.FullName);
            Directory.CreateDirectory(new FileInfo(opusOrdinalFilePath).Directory.FullName);
            Directory.CreateDirectory(new FileInfo(opusNaturalFilePath).Directory.FullName);

            // Convert to opus using ffmpeg
            CommandLineParams additionalCommandLine = new CommandLineParams();
            additionalCommandLine.AddParameter("-map_metadata", "-1");
            AppendTalkMetadataToFfmpegCommandLine(talkInfo, additionalCommandLine, localeStrings);
            procResult = await AudioProcessor.Ffmpeg_ConvertAudioToOpus(normalizedWavFilePath, opusOrdinalFilePath, logger, 1, 16, 0, additionalCommandLine);
            if (procResult.ProcessExitCode != 0)
            {
                logger.Log("ffmpeg returned error code " + procResult.ProcessExitCode + " while encoding " + opusOrdinalFilePath, LogLevel.Err);
                return false;
            }

            // Now convert the .wav to AAC with winamp codec
            procResult = await AudioProcessor.FhgAACEnc_ConvertWavToAacCBR(normalizedWavFilePath, m4aNoMetaFilePath, logger);
            if (procResult.ProcessExitCode != 0)
            {
                logger.Log("fhgaacenc returned error code " + procResult.ProcessExitCode + " while encoding " + m4aNoMetaFilePath, LogLevel.Err);
                return false;
            }

            // Use ffmpeg to amend the metadata
            additionalCommandLine.Clear();
            additionalCommandLine.AddParameter("-map_metadata", "-1");
            additionalCommandLine.AddParameter("-map", "0:a:0");
            AppendTalkMetadataToFfmpegCommandLine(talkInfo, additionalCommandLine, localeStrings);
            procResult = await AudioProcessor.Ffmpeg_CopyStreams(m4aNoMetaFilePath, m4aOrdinalFilePath, additionalCommandLine, logger);
            if (procResult.ProcessExitCode != 0)
            {
                logger.Log("ffmpeg returned error code " + procResult.ProcessExitCode + " while encoding " + m4aOrdinalFilePath, LogLevel.Err);
                return false;
            }

            // Copy ordinal files to natural named files
            File.Copy(opusOrdinalFilePath, opusNaturalFilePath);
            File.Copy(m4aOrdinalFilePath, m4aNaturalFilePath);

            // Delete intermediate files
            File.Delete(rawWavFilePath);
            File.Delete(m4aNoMetaFilePath);
            File.Delete(normalizedWavFilePath);
            File.Delete(inputMediaFile);

            return true;
        }

        private static void AppendTalkMetadataToFfmpegCommandLine(ConferenceTalk talkInfo, CommandLineParams commandLineBuilder, LocalizedStrings localeStrings)
        {
            commandLineBuilder.AddParameter("-metadata", "title=\"" + CommandLine.EscapeConsoleString(talkInfo.Title) + "\"");
            commandLineBuilder.AddParameter("-metadata", "artist=\"" + CommandLine.EscapeConsoleString(talkInfo.Speaker) + "\"");
            commandLineBuilder.AddParameter("-metadata", "album=\"" + CommandLine.EscapeConsoleString(localeStrings.ConferenceName(talkInfo.Conference)) + "\"");
            commandLineBuilder.AddParameter("-metadata", "track=\"" + talkInfo.SessionIndex.ToString() + talkInfo.TalkIndex.ToString() + "\"");
            commandLineBuilder.AddParameter("-metadata", "album_artist=\"" + localeStrings.ConferenceAlbumName + "\"");
            commandLineBuilder.AddParameter("-metadata", "genre=\"Speech\"");
            commandLineBuilder.AddParameter("-metadata", "date=\"" + talkInfo.Conference.Year + "\"");
            commandLineBuilder.AddParameter("-metadata", "composer=\"" + CommandLine.EscapeConsoleString(localeStrings.NameOfTheChurch) + "\"");
            commandLineBuilder.AddParameter("-metadata", "publisher=\"" + CommandLine.EscapeConsoleString(localeStrings.NameOfTheChurch) + "\"");
            commandLineBuilder.AddParameter("-metadata", "organization=\"" + CommandLine.EscapeConsoleString(localeStrings.NameOfTheChurch) + "\"");
            commandLineBuilder.AddParameter("-metadata", "copyright=\"" + CommandLine.EscapeConsoleString(localeStrings.CopyrightText(talkInfo.Conference.Year)) + "\"");
            commandLineBuilder.AddParameter("-metadata:s:a:0", "language=\"" + CommandLine.EscapeConsoleString(talkInfo.Language.Iso639_2) + "\"");
        }
    }
}
