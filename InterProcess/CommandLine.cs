using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.InterProcess
{
    internal class CommandLineResult
    {
        public int ProcessExitCode;
        public string ProcessStdOut;
    }

    internal class CommandLineParams
    {
        private readonly List<KeyValuePair<string, string>> _parameters = new List<KeyValuePair<string, string>>();

        public IReadOnlyCollection<KeyValuePair<string, string>> Parameters => _parameters;

        public void AddParameter(string value)
        {
            _parameters.Add(new KeyValuePair<string, string>(value, string.Empty));
        }

        public void AddParameter(string key, string value)
        {
            _parameters.Add(new KeyValuePair<string, string>(key, value));
        }

        public void AddParameters(CommandLineParams other)
        {
            foreach (var kvp in other.Parameters)
            {
                _parameters.Add(kvp);
            }
        }

        public void Clear()
        {
            _parameters.Clear();
        }

        public override string ToString()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                bool first = true;
                StringBuilder builder = pooledSb.Builder;
                foreach (var kvp in _parameters)
                {
                    if (!first)
                    {
                        builder.Append(" ");
                    }

                    builder.Append(kvp.Key);
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        builder.Append(" ");
                        builder.Append(kvp.Value);
                    }

                    first = false;
                }

                return builder.ToString();
            }
        }
    }

    internal class CommandLine
    {
        public static async Task<CommandLineResult> RunProcessAndCaptureOutput(string exePath, CommandLineParams args, ILogger logger)
        {
            string argsString = args.ToString();
            logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Running {0} {1}", exePath, argsString);
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = argsString,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            Process process = Process.Start(processInfo);

            Stream processOutput = process.StandardError.BaseStream;
            using (MemoryStream returnVal = new MemoryStream())
            {
                byte[] chunk = new byte[1024];
                bool reading = true;
                while (reading)
                {
                    int outputChunkSize = await processOutput.ReadAsync(chunk, 0, chunk.Length);
                    if (outputChunkSize > 0)
                    {
                        returnVal.Write(chunk, 0, outputChunkSize);
                    }
                    else
                    {
                        reading = false;
                    }
                }

                string ascii = Encoding.ASCII.GetString(returnVal.ToArray());
                if (process.ExitCode == 0)
                {
                    logger.Log(exePath + " " + args + " has exited with code " + process.ExitCode, LogLevel.Vrb);
                }
                else
                {
                    logger.Log(ascii);
                    logger.Log(exePath + " " + args + " has exited with code " + process.ExitCode, LogLevel.Err);
                }

                return new CommandLineResult()
                {
                    ProcessExitCode = process.ExitCode,
                    ProcessStdOut = ascii
                };
            }
        }

        public static string EscapeConsoleString(string input)
        {
            return input.Replace("\"", "\\\"");
        }
    }
}
