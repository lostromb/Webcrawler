using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Extensions.NativeAudio.Components;

namespace WebCrawler.InterProcess
{
    internal static class AudioProcessor
    {
        public static async Task<CommandLineResult> Ffmpeg_EncodeRaw(
            CommandLineParams args,
            ILogger logger)
        {
            return await CommandLine.RunProcessAndCaptureOutput("ffmpeg.exe", args, logger);
        }

        public static async Task<CommandLineResult> Ffmpeg_CopyStreams(
            string inputMediaFile,
            string outputMediaFile,
            CommandLineParams mappingParams,
            ILogger logger)
        {
            CommandLineParams args = new CommandLineParams();
            args.AddParameter("-nostdin");
            args.AddParameter("-i", $"\"{CommandLine.EscapeConsoleString(inputMediaFile)}\"");
            args.AddParameter("-c", "copy");
            if (mappingParams != null)
            {
                args.AddParameters(mappingParams);
            }

            args.AddParameter("-y");
            args.AddParameter($"\"{CommandLine.EscapeConsoleString(outputMediaFile)}\"");
            return await CommandLine.RunProcessAndCaptureOutput("ffmpeg.exe", args, logger);
        }

        public static async Task<CommandLineResult> Ffmpeg_ConvertAudioToWav(
            string inputMediaFile,
            string outputWavFile,
            ILogger logger,
            int sampleRate = 48000,
            int channels = 1,
            int inputAudioStream = 0)
        {
            CommandLineParams args = new CommandLineParams();
            args.AddParameter("-nostdin");
            args.AddParameter("-i", $"\"{CommandLine.EscapeConsoleString(inputMediaFile)}\"");
            args.AddParameter("-map_metadata", "-1");
            args.AddParameter("-map", $"0:a:{inputAudioStream}");
            args.AddParameter("-c:a", "pcm_s16le");
            args.AddParameter("-ar", $"{sampleRate}");
            args.AddParameter("-ac", $"{channels}");
            args.AddParameter("-y");
            args.AddParameter($"\"{CommandLine.EscapeConsoleString(outputWavFile)}\"");
            return await CommandLine.RunProcessAndCaptureOutput("ffmpeg.exe", args, logger);
        }

        public static async Task<CommandLineResult> Ffmpeg_ConvertAudioToOpus(
            string inputMediaFile,
            string outputOpusFile,
            ILogger logger,
            int channels = 1,
            int bitrateKbps = 24,
            int inputAudioStream = 0,
            CommandLineParams additionalArgs = null)
        {
            CommandLineParams args = new CommandLineParams();
            args.AddParameter("-nostdin");
            args.AddParameter("-i", $"\"{CommandLine.EscapeConsoleString(inputMediaFile)}\"");
            args.AddParameter("-map", $"0:a:{inputAudioStream}");
            args.AddParameter("-c:a", "libopus");
            args.AddParameter("-b:a", $"{bitrateKbps}K");
            args.AddParameter("-ac", $"{channels}");
            args.AddParameter("-y");

            if (additionalArgs != null)
            {
                args.AddParameters(additionalArgs);
            }

            args.AddParameter($"\"{CommandLine.EscapeConsoleString(outputOpusFile)}\"");
            return await CommandLine.RunProcessAndCaptureOutput("ffmpeg.exe", args, logger);
        }

        public static async Task<CommandLineResult> FhgAACEnc_ConvertWavToAacCBR(string inputWavFile, string outputM4aFile, ILogger logger, int bitrateKbps = 96, string profile = "hev2")
        {
            CommandLineParams args = new CommandLineParams();
            args.AddParameter("--cbr", $"{bitrateKbps}");
            args.AddParameter("--profile", $"{profile}");
            args.AddParameter("--quiet");
            args.AddParameter($"\"{CommandLine.EscapeConsoleString(inputWavFile)}\"");
            args.AddParameter($"\"{CommandLine.EscapeConsoleString(outputM4aFile)}\"");
            return await CommandLine.RunProcessAndCaptureOutput("fhgaacenc.exe", args, logger);
        }

        public static async Task<AudioStatistics> GetAudioStatistics(string inputFilePath, ILogger logger)
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            {
                bool gotInitialAudio = false;
                TimeSpan earliestSoundTime = TimeSpan.Zero;
                TimeSpan latestSoundTime = TimeSpan.Zero;
                TimeSpan currentTime = TimeSpan.Zero;
                float highestRmsVolume = 0.0f;
                MovingPercentile peakPercentiles = new MovingPercentile(10000, 0.25, 0.5, 0.75, 0.95, 0.99);
                const float silenceThresholdRms = 0.01f;
                float scaleFactor = 1.0f;

                using (IAudioSampleSource decoder = await FfmpegAudioSampleSource.Create(graph, "AudioDecoder", logger, new FileInfo(inputFilePath)))
                {
                    AudioSampleFormat inputFormat = decoder.OutputFormat;
                    using (NullAudioSampleTarget target = new NullAudioSampleTarget(graph, inputFormat, "AudioTarget"))
                    using (PassiveVolumeMeter meter = new PassiveVolumeMeter(graph, inputFormat, "SlowMeter", TimeSpan.FromMilliseconds(3000)))
                    {
                        decoder.ConnectOutput(meter);
                        meter.ConnectOutput(target);
                        while (!decoder.PlaybackFinished)
                        {
                            int samplesRead = await target.ReadSamplesFromInput(
                                (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(inputFormat.SampleRateHz, TimeSpan.FromMilliseconds(10)),
                                CancellationToken.None,
                                DefaultRealTimeProvider.Singleton);
                            if (samplesRead <= 0)
                            {
                                continue;
                            }

                            currentTime += AudioMath.ConvertSamplesPerChannelToTimeSpan(inputFormat.SampleRateHz, samplesRead);
                            float currentRms = meter.GetLoudestRmsVolume();
                            float currentPeak = meter.GetPeakVolume();
                            if (currentRms > silenceThresholdRms)
                            {
                                latestSoundTime = currentTime;

                                if (!gotInitialAudio)
                                {
                                    earliestSoundTime = currentTime;
                                    gotInitialAudio = true;
                                }

                                if (currentRms > highestRmsVolume)
                                {
                                    highestRmsVolume = currentRms;
                                }

                                peakPercentiles.Add(currentPeak);
                            }
                        }

                        // 0.13 magic volume scaling number here
                        //scaleFactor = Math.Min(5.0f, 0.13f / Math.Max(0.0001f, (float)highestRmsVolume));

                        // Just normalize using 99th percentile peak instead of with RMS
                        scaleFactor = 1.00f / (float)peakPercentiles.GetPercentile(0.99);
                        logger.Log(inputFilePath + " Sound start: " + earliestSoundTime.PrintTimeSpan() + " Sound end: " + latestSoundTime.PrintTimeSpan() + " Peak RMS: " + highestRmsVolume + " Scale factor: " + scaleFactor, LogLevel.Vrb);
                    }
                }

                return new AudioStatistics()
                {
                    VolumeHistogram = peakPercentiles,
                    AudioEndTime = earliestSoundTime,
                    AudioStartTime = latestSoundTime
                };
            }
        }

        public static async Task NormalizeAudio(
            string inputFilePath,
            string outputWavFilePath,
            AudioStatistics statistics,
            bool trimLength,
            ILogger logger)
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            {
                TimeSpan currentTime = TimeSpan.Zero;
                MovingPercentile peakPercentiles = new MovingPercentile(10000, 0.25, 0.5, 0.75, 0.95, 0.99);
                float scaleFactor = 1.0f;

                // Renormalize the file using the stats we got
                TimeSpan trimStartTime = TimeSpan.Zero;
                TimeSpan trimEndTime = TimeSpan.FromDays(30);
                if (trimLength)
                {
                    trimStartTime = statistics.AudioStartTime - TimeSpan.FromMilliseconds(2500); // preroll 2.5 seconds
                    trimEndTime = statistics.AudioEndTime + TimeSpan.FromMilliseconds(5000); // postroll 5 seconds (plus the decay from the volume meter which adds ~1.5 sec
                }
                
                currentTime = TimeSpan.Zero;

                using (FileStream waveOutStream = new FileStream(outputWavFilePath, FileMode.Create, FileAccess.ReadWrite))
                using (IAudioSampleSource decoder = await FfmpegAudioSampleSource.Create(graph, "AudioDecoder", logger, new FileInfo(inputFilePath)))
                using (AudioEncoder encoder = new RiffWaveEncoder(graph, decoder.OutputFormat, "WaveEncoder", logger))
                using (NullAudioSampleTarget nullTarget = new NullAudioSampleTarget(graph, decoder.OutputFormat, "NullTarget"))
                using (VolumeFilter volume = new VolumeFilter(graph, decoder.OutputFormat, "VolumeFilter"))
                {
                    await encoder.Initialize(waveOutStream, false, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                    decoder.ConnectOutput(nullTarget);

                    // Preskip
                    while (!decoder.PlaybackFinished && currentTime < trimStartTime)
                    {
                        int samplesRead = await nullTarget.ReadSamplesFromInput(
                            (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(decoder.OutputFormat.SampleRateHz, TimeSpan.FromMilliseconds(10)),
                            CancellationToken.None,
                            DefaultRealTimeProvider.Singleton);
                        if (samplesRead <= 0)
                        {
                            continue;
                        }

                        currentTime += AudioMath.ConvertSamplesPerChannelToTimeSpan(decoder.OutputFormat.SampleRateHz, samplesRead);
                    }

                    // Main processing
                    decoder.ConnectOutput(volume);
                    volume.ConnectOutput(encoder);
                    volume.VolumeLinear = scaleFactor;
                    while (!decoder.PlaybackFinished && currentTime < trimEndTime)
                    {
                        int samplesRead = await encoder.ReadFromSource(
                            (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(decoder.OutputFormat.SampleRateHz, TimeSpan.FromMilliseconds(10)),
                            CancellationToken.None,
                            DefaultRealTimeProvider.Singleton);
                        if (samplesRead <= 0)
                        {
                            continue;
                        }

                        currentTime += AudioMath.ConvertSamplesPerChannelToTimeSpan(decoder.OutputFormat.SampleRateHz, samplesRead);
                    }

                    await encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    
                }
            }
        }
    }
}
