//using Durandal.Common.Logger;
//using Durandal.Common.Time;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Webcrawler.Scenarios.GeneralConf
//{
//    public class ConferenceProcessor
//    {
//        public static readonly Regex VolumeDetectMatcher = new Regex("\\[Parsed_volumedetect.+\\] max_volume: ([\\d\\.\\-]+) dB");
//        public const double VOLUME_GAIN = 3;

//        public void Run()
//        {
//            ILogger logger = new ConsoleLogger();
//            using (FileStream tsvIn = new FileStream(@"C:\Users\lostromb\Desktop\conference\input.tsv", FileMode.Open, FileAccess.Read))
//            using (StreamReader streamReader = new StreamReader(tsvIn))
//            {
//                while (!streamReader.EndOfStream)
//                {
//                    string tsvLine = streamReader.ReadLine();
//                    if (string.IsNullOrEmpty(tsvLine))
//                    {
//                        continue;
//                    }

//                    string[] parts = tsvLine.Split('\t');
//                    if (parts.Length != 7)
//                    {
//                        logger.Log("Bad input line " + tsvLine, LogLevel.Err);
//                        continue;
//                    }

//                    TimeSpan startTs = TimeSpanExtensions.ParseTimeSpan(parts[5]);
//                    TimeSpan endTs = TimeSpanExtensions.ParseTimeSpan(parts[6]);

//                    ConferenceTalk talkInfo = new ConferenceTalk()
//                    {
//                        Conference = new Conference()
//                        {
//                            Phase = ConferencePhase.April,
//                            Year = 2021,
//                            PageUrl = null
//                        },
//                        Mp3Url = null,
//                        SessionSourcePath = @"C:\Users\lostromb\Desktop\conference\" + parts[0] + ".mp4",
//                        SessionIndex = int.Parse(parts[1]),
//                        TalkIndex = int.Parse(parts[2]),
//                        Title = parts[3],
//                        Speaker = parts[4],
//                        StartTimeInSession = startTs,
//                        Length = endTs - startTs
//                    };

//                    string outputFileName = @"C:\Users\lostromb\Desktop\conference\" + SanitizeFileName(talkInfo.Speaker + " - " + talkInfo.SessionIndex.ToString() + talkInfo.TalkIndex.ToString() + " - " + talkInfo.Title) + ".opus";
//                    ConvertMP4ToOpus(talkInfo, new FileInfo(outputFileName), logger);
//                }
//            }
//        }

//        private static bool ConvertMP4ToOpus(ConferenceTalk talkInfo, FileInfo outputFile, ILogger logger, string ffmpegPath = "ffmpeg.exe")
//        {
//            logger.Log("Processing " + outputFile.Name);
//            logger.Log("Starting volumedetect...", LogLevel.Vrb);
//            Tuple<string, int> procResult = RunProcessAndCaptureOutput(ffmpegPath, "-nostdin -ss " + talkInfo.StartTimeInSession.PrintTimeSpan() + " -i \"" + EscapeConsoleString(talkInfo.SessionSourcePath) + "\" -map 0:a:0 -t " + talkInfo.Length.PrintTimeSpan() + " -af \"volumedetect\" -f null -", logger);
//            if (procResult.Item2 != 0)
//            {
//                return false;
//            }

//            double maxVolumeDb;
//            Match volumeDetectMatch = VolumeDetectMatcher.Match(procResult.Item1);
//            if (!volumeDetectMatch.Success || !double.TryParse(volumeDetectMatch.Groups[1].Value, out maxVolumeDb))
//            {
//                maxVolumeDb = -3;
//            }

//            logger.Log("Parsed maximum volume as " + maxVolumeDb.ToString("F1") + "dB", LogLevel.Vrb);
//            double volumeIncrease = Math.Abs(maxVolumeDb) + VOLUME_GAIN;

//            logger.Log("Starting encoding...", LogLevel.Vrb);
//            StringBuilder commandLineBuilder = new StringBuilder();
//            commandLineBuilder.Append(" -nostdin");
//            commandLineBuilder.Append(" -ss " + talkInfo.StartTimeInSession.PrintTimeSpan());
//            commandLineBuilder.Append(" -i \"" + EscapeConsoleString(talkInfo.SessionSourcePath) + "\"");
//            commandLineBuilder.Append(" -map_metadata -1");
//            commandLineBuilder.Append(" -map 0:a:0");
//            commandLineBuilder.Append(" -c:a libopus");
//            commandLineBuilder.Append(" -b:a 24K");
//            commandLineBuilder.Append(" -af \"volume=" + volumeIncrease.ToString("F1") + "dB\"");
//            commandLineBuilder.Append(" -t " + talkInfo.Length.PrintTimeSpan());
//            commandLineBuilder.Append(" -y");
//            commandLineBuilder.Append(" -metadata title=\"" + EscapeConsoleString(talkInfo.Title) + "\"");
//            commandLineBuilder.Append(" -metadata artist=\"" + EscapeConsoleString(talkInfo.Speaker) + "\"");
//            commandLineBuilder.Append(" -metadata album=\"" + talkInfo.Conference.ToString() + "\"");
//            commandLineBuilder.Append(" -metadata track=\"" + talkInfo.SessionIndex.ToString() + talkInfo.TalkIndex.ToString() + "\"");
//            commandLineBuilder.Append(" -metadata album_artist=\"LDS General Conference\"");
//            commandLineBuilder.Append(" -metadata genre=\"Speech\"");
//            commandLineBuilder.Append(" -metadata date=\"" + talkInfo.Conference.Year + "\"");
//            commandLineBuilder.Append(" -metadata composer=\"The Church of Jesus Christ of Latter-day Saints\"");
//            commandLineBuilder.Append(" -metadata copyright=\"" + talkInfo.Conference.Year + " by Intellectual Reserve, Inc. All rights reserved.\"");
//            commandLineBuilder.Append(" \"" + EscapeConsoleString(outputFile.FullName) + "\"");
//            procResult = RunProcessAndCaptureOutput(ffmpegPath, commandLineBuilder.ToString(), logger);

//            return procResult.Item2 == 0;
//        }

//        private static Tuple<string, int> RunProcessAndCaptureOutput(string exePath, string args, ILogger logger)
//        {
//            logger.Log("Running " + exePath + " " + args, LogLevel.Vrb);
//            ProcessStartInfo processInfo = new ProcessStartInfo
//            {
//                FileName = exePath,
//                Arguments = args,
//                WindowStyle = ProcessWindowStyle.Hidden,
//                UseShellExecute = false,
//                CreateNoWindow = true,
//                RedirectStandardError = true
//            };

//            Process process = Process.Start(processInfo);

//            Stream processOutput = process.StandardError.BaseStream;
//            using (MemoryStream returnVal = new MemoryStream())
//            {
//                byte[] chunk = new byte[1024];
//                bool reading = true;
//                while (reading)
//                {
//                    int outputChunkSize = processOutput.Read(chunk, 0, chunk.Length);
//                    if (outputChunkSize > 0)
//                    {
//                        returnVal.Write(chunk, 0, outputChunkSize);
//                    }
//                    else
//                    {
//                        reading = false;
//                    }
//                }

//                string ascii = Encoding.ASCII.GetString(returnVal.ToArray());
//                if (process.ExitCode == 0)
//                {
//                    logger.Log(exePath + " " + args + " has exited with code " + process.ExitCode, LogLevel.Vrb);
//                }
//                else
//                {
//                    logger.Log(ascii);
//                    logger.Log(exePath + " " + args + " has exited with code " + process.ExitCode, LogLevel.Err);
//                }
//                return new Tuple<string, int>(ascii, process.ExitCode);
//            }
//        }

//        private static string EscapeConsoleString(string input)
//        {
//            return input.Replace("\"", "\\\"");
//        }

//        private static string SanitizeFileName(string input)
//        {
//            foreach (char notAllowed in Path.GetInvalidFileNameChars())
//            {
//                input = input.Replace(notAllowed, '_');
//            }

//            return input;
//        }
//    }
//}
