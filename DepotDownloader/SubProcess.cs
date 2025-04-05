// This file is subject to the terms and conditions defined
// in file 'LICENSE', which is part of this source code package.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Tmds.Utils;

namespace DepotDownloader
{
    public class AppDownloadConfig : DownloadConfig
    {
        public uint AppId { get; set; }
        public List<(uint depotId, ulong manifestId)> DepotManifestIds { get; set; } = [];
        public string Branch { get; set; } = ContentDownloader.DEFAULT_BRANCH;
        public string? OS { get; set; }
        public string? Arch { get; set; }
        public string? Language { get; set; }
        public bool LowViolence { get; set; }
    }

    public static class SubProcess
    {
        public const int Success = 0;
        public const int Error_Unknown = 1;
        public const int Error_General = 2;
        public const int Error_Login = 3;

        public static IAsyncEnumerable<(string msg, bool isError)> AppDownload(
            AppDownloadConfig config,
            Action<int>? exitHandler = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess(AppDownloadInner, config, exitHandler, cancellationToken);
        }

        public static Task<int> AppDownload(AppDownloadConfig config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess(AppDownloadInner, config, messageHandler, errorMessageHandler, cancellationToken);
        }

        private static async Task<int> AppDownloadInner(string[] input)
        {
            var cfg = Deserialize<AppDownloadConfig>(input[0]);
            var parentProcess = Process.GetProcessById(Convert.ToInt32(input[1]));
            parentProcess.EnableRaisingEvents = true;
            parentProcess.Exited += (sender, args) =>
            {
                Process.GetCurrentProcess().Kill(true);
            };

            if (cfg.MaxDownloads <= 0)
                cfg.MaxDownloads = 8;

            ContentDownloader.Config = cfg;

            // Todo login handling
            if (!ContentDownloader.InitializeSteam3(null, null))
            {
                return Error_Login;
            }

            try
            {
                await ContentDownloader.DownloadAppAsync(cfg.AppId, cfg.DepotManifestIds, cfg.Branch, cfg.OS, cfg.Arch,
                    cfg.Language, cfg.LowViolence, false);
            }
            catch (Exception ex) when (ex is ContentDownloaderException or OperationCanceledException)
            {
                Console.WriteLine(ex.Message);
                return Error_General;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Download failed to due to an unhandled exception: {0}", e.Message);
                return Error_Unknown;
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }
            return Success;
        }

        /// Executes given action in a new process, returning an IAsyncEnumerable that yields for every console message.
        private static async IAsyncEnumerable<(string msg, bool isError)> ExecInProcess(
            Func<string[], Task<int>> action,
            object config,
            Action<int>? exitHandler = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var process = ExecFunction.Start(action, [Serialize(config), Environment.ProcessId.ToString()], options =>
            {
                options.StartInfo.RedirectStandardOutput = true;
                options.StartInfo.RedirectStandardError = true;
            });
            try
            {
                Task<string?>? readOutputTask = null;
                Task<string?>? readErrorTask = null;
                while (!process.HasExited)
                {
                    if (readOutputTask == null || readOutputTask.IsCompleted)
                        readOutputTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
                    if (readErrorTask == null || readErrorTask.IsCompleted)
                        readErrorTask = process.StandardError.ReadLineAsync(cancellationToken).AsTask();
                    var completedTask = await Task.WhenAny(readOutputTask, readErrorTask);

                    var message = completedTask.Result;
                    if (message != null)
                    {
                        yield return (message, completedTask == readErrorTask);
                    }
                }
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(true);
            }

            var exitCode = Error_Unknown;
            try
            {
                exitCode = process.ExitCode;
            }
            finally
            {
                exitHandler?.Invoke(exitCode);
            }
        }

        /// Executes given action in a new process, returning an awaitable task.
        public static async Task<int> ExecInProcess(
            Func<string[], Task<int>> action,
            object config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            CancellationToken cancellationToken = default)
        {
            var process = ExecFunction.Start(action, [Serialize(config)], options =>
            {
                options.StartInfo.RedirectStandardOutput = true;
                options.StartInfo.RedirectStandardError = true;
            });
            try
            {
                if (messageHandler != null)
                {
                    process.OutputDataReceived += messageHandler;
                    process.BeginOutputReadLine();
                }
                if (errorMessageHandler != null)
                {
                    process.ErrorDataReceived += errorMessageHandler;
                    process.BeginErrorReadLine();
                }
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(true);
            }

            try
            {
                return process.ExitCode;
            }
            catch
            {
                return Error_Unknown;
            }
        }

        private static string Serialize(object obj)
        {
            var xmlSerializer = new XmlSerializer(obj.GetType());
            using var stringWriter = new StringWriter();
            xmlSerializer.Serialize(stringWriter, obj);
            return stringWriter.ToString();
        }

        private static T Deserialize<T>(string str)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            using var stringReader = new StringReader(str);
            return (T)xmlSerializer.Deserialize(stringReader)!;
        }
    }
}
