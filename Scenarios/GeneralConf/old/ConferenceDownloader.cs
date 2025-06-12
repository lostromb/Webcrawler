//using Durandal.Common.Logger;
//using Durandal.Common.Time;
//using System;
//using System.Diagnostics;
//using System.IO;
//using System.Text;
//using System.Text.RegularExpressions;

//namespace Webcrawler.Scenarios.GeneralConf
//{
//    public static class ConferenceDownloader
//    {
//        public const double VOLUME_GAIN = 3;
//        public static readonly Regex VolumeDetectMatcher = new Regex("\\[Parsed_volumedetect.+\\] max_volume: ([\\d\\.\\-]+) dB");

//        public static void Download()
//        {
//            ILogger logger = new ConsoleLogger();
//            string ffmpegPath = @"C:\Program Files\ffmpeg\bin\ffmpeg.exe";
//            string[] inputLines = File.ReadAllLines(@"C:\Users\lostromb\Desktop\conference\timestamps.tsv");
//            foreach (string line in inputLines)
//            {
//                string[] parts = line.Split('\t');
//                string inputFileName = "C:\\Users\\lostromb\\Desktop\\conference\\input\\" + parts[0];
//                TimeSpan startTime = TimeSpanExtensions.ParseTimeSpan(parts[1]);
//                TimeSpan endTime = TimeSpanExtensions.ParseTimeSpan(parts[2]);
//                string trackNum = parts[3];
//                string speaker = parts[4];
//                string talkName = parts[5];
//                string startTimeString = parts[1];
//                string durationString = TimeSpanExtensions.PrintTimeSpan(endTime - startTime);

//                string outputFileName = "C:\\Users\\lostromb\\Desktop\\conference\\output\\" + SanitizeFileName(speaker) + " - " + SanitizeFileName(talkName) + ".opus";
//                logger.Log("Processing " + outputFileName);
//                // Parse the volume gain for the talk
//                Tuple<string, int> procResult = RunProcessAndCaptureOutput(ffmpegPath, "-nostdin -ss " + startTimeString  + " -i \"" + EscapeConsoleString(inputFileName) + "\" -t " + durationString + " -af \"volumedetect\" -f null -", logger);
//                if (procResult.Item2 != 0)
//                {
//                    logger.Log("Failed to parse volume", LogLevel.Err);
//                    continue;
//                }

//                double maxVolumeDb;
//                Match volumeDetectMatch = VolumeDetectMatcher.Match(procResult.Item1);
//                if (!volumeDetectMatch.Success || !double.TryParse(volumeDetectMatch.Groups[1].Value, out maxVolumeDb))
//                {
//                    maxVolumeDb = -3;
//                }

//                logger.Log("Parsed maximum volume as " + maxVolumeDb.ToString("F1") + "dB", LogLevel.Std);
//                double volumeIncrease = Math.Abs(maxVolumeDb) + VOLUME_GAIN;

//                logger.Log("Starting encoding...", LogLevel.Vrb);
//                StringBuilder commandLineBuilder = new StringBuilder();
//                commandLineBuilder.Append(" -nostdin");
//                commandLineBuilder.Append(" -ss " + startTimeString);
//                commandLineBuilder.Append(" -i \"" + EscapeConsoleString(inputFileName) + "\"");
//                commandLineBuilder.Append(" -map_metadata -1");
//                commandLineBuilder.Append(" -map 0:a:0");
//                commandLineBuilder.Append(" -c:a libopus");
//                commandLineBuilder.Append(" -b:a 24K");
//                commandLineBuilder.Append(" -t " + durationString);
//                commandLineBuilder.Append(" -af \"volume=" + volumeIncrease.ToString("F1") + "dB\"");
//                commandLineBuilder.Append(" -metadata title=\"" + EscapeConsoleString(talkName) + "\"");
//                commandLineBuilder.Append(" -metadata artist=\"" + EscapeConsoleString(speaker) + "\"");
//                commandLineBuilder.Append(" -metadata album=\"October 2021 General Conference\"");
//                commandLineBuilder.Append(" -metadata track=\"" + trackNum + "\"");
//                commandLineBuilder.Append(" -metadata album_artist=\"LDS General Conference\"");
//                commandLineBuilder.Append(" -metadata genre=\"Speech\"");
//                commandLineBuilder.Append(" -metadata date=\"2021\"");
//                commandLineBuilder.Append(" -metadata composer=\"The Church of Jesus Christ of Latter-day Saints\"");
//                commandLineBuilder.Append(" -metadata copyright=\"2021 by Intellectual Reserve, Inc. All rights reserved.\"");
//                commandLineBuilder.Append(" \"" + EscapeConsoleString(outputFileName) + "\"");
//                procResult = RunProcessAndCaptureOutput(ffmpegPath, commandLineBuilder.ToString(), logger);
//                if (procResult.Item2 != 0)
//                {
//                    logger.Log("Failed to encode file", LogLevel.Err);
//                    continue;
//                }
//            }

//            //CompressManifest();
//            //using (IThreadPool baseThreadPool = new SystemThreadPool(NullLogger.Singleton, NullMetricCollector.Singleton, DimensionSet.Empty))
//            //using (IThreadPool fixedThreadPool = new FixedCapacityThreadPool(
//            //    baseThreadPool,
//            //    NullLogger.Singleton,
//            //    NullMetricCollector.Singleton,
//            //    DimensionSet.Empty,
//            //    "FixedThreadPool",
//            //    8,
//            //    ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable,
//            //    TimeSpan.Zero))
//            //{
//            //    while (true)
//            //    {
//            //        fixedThreadPool.EnqueueUserWorkItem(RunFunctionalTests);
//            //        Thread.Sleep(5000);
//            //    }
//            //}

//            //AsyncMain(args).ConfigureAwait(false).GetAwaiter().GetResult();
//            //SomeObject s = new SomeObject();
//            //s.Strings = new List<string>();
//            //s.Strings.Add("Nott");
//            //s.Strings.Add("Jester");
//            //JsonSerializerSettings settings = new JsonSerializerSettings()
//            //{
//            //    TypeNameHandling = TypeNameHandling.All
//            //};
//            //string json = JsonConvert.SerializeObject(s, settings);
//            //Console.WriteLine(json);
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
