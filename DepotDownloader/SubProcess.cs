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
using Microsoft.VisualBasic;
using SteamKit2.Authentication;
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

        /// Messages starting with this string are control messages. Used for communicating with the parent process.
        internal const string MagicMessage = "$DDSPMM*";
        /// Delimiter by which individual args of control messages are separated.
        internal const char MagicMessageDelimiter = ControlChars.NullChar;

        public static IAsyncEnumerable<(string msg, bool isError)> AppDownload(
            AppDownloadConfig config,
            Action<int>? exitHandler = null,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess(AppDownloadInner, config, exitHandler, authenticator, cancellationToken);
        }

        public static Task<int> AppDownload(AppDownloadConfig config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess(AppDownloadInner, config, messageHandler, errorMessageHandler, authenticator, cancellationToken);
        }

        private static async Task<int> AppDownloadInner(string[] input)
        {
            InitSubProcess(input);
            var cfg = Deserialize<AppDownloadConfig>(input[0]);

            if (cfg.MaxDownloads <= 0)
                cfg.MaxDownloads = 8;

            if (cfg.AccountSettingsFileName != null)
            {
                AccountSettingsStore.LoadFromFile(cfg.AccountSettingsFileName);
            }

            ContentDownloader.Config = cfg;
            ContentDownloader.Authenticator = new SubProcessAuthenticator();

            if (!ContentDownloader.InitializeSteam3(cfg.Username, cfg.Password))
            {
                return Error_Login;
            }

            try
            {
                await ContentDownloader.DownloadAppAsync(cfg.AppId, cfg.DepotManifestIds, cfg.Branch, cfg.OS, cfg.Arch,
                    cfg.Language, cfg.LowViolence, false).ConfigureAwait(false);
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
            IAuthenticator? authenticator = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var process = ExecFunction.Start(action, [Serialize(config), Environment.ProcessId.ToString()], options =>
            {
                options.StartInfo.RedirectStandardOutput = true;
                options.StartInfo.RedirectStandardError = true;
                options.StartInfo.RedirectStandardInput = true;
            });
            try
            {
                Task<string?>?[] readStreamTasks = [null, null];
                while (!process.HasExited)
                {
                    readStreamTasks[0] ??= process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
                    readStreamTasks[1] ??= process.StandardError.ReadLineAsync(cancellationToken).AsTask();
                    var completedTask = await Task.WhenAny(readStreamTasks!).ConfigureAwait(false);

                    var message = completedTask.Result;
                    if (message != null)
                    {
                        var isError = completedTask == readStreamTasks[1];
                        if (!isError && message.StartsWith(MagicMessage))
                        {
                            await ProcessMagicMessage(message, process, authenticator).ConfigureAwait(false);
                        }
                        else
                        {
                            yield return (message, isError);
                        }
                    }
                    readStreamTasks[Array.IndexOf(readStreamTasks, completedTask)] = null;
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
        private static async Task<int> ExecInProcess(
            Func<string[], Task<int>> action,
            object config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            var process = ExecFunction.Start(action, [Serialize(config), Environment.ProcessId.ToString()], options =>
            {
                options.StartInfo.RedirectStandardOutput = true;
                options.StartInfo.RedirectStandardError = true;
                options.StartInfo.RedirectStandardInput = true;
            });
            try
            {
                process.OutputDataReceived += async (sender, args) =>
                {
                    if (args.Data != null && args.Data.StartsWith(MagicMessage))
                    {
                        await ProcessMagicMessage(args.Data, process, authenticator).ConfigureAwait(false);
                    }
                    else
                    {
                        messageHandler?.Invoke(sender, args);
                    }
                };
                process.BeginOutputReadLine();

                if (errorMessageHandler != null)
                {
                    process.ErrorDataReceived += errorMessageHandler;
                    process.BeginErrorReadLine();
                }

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
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

        private static void InitSubProcess(string[] input)
        {
            var parentProcess = Process.GetProcessById(Convert.ToInt32(input[1]));
            parentProcess.EnableRaisingEvents = true;
            parentProcess.Exited += (sender, args) =>
            {
                Process.GetCurrentProcess().Kill(true);
            };
        }

        private static async Task ProcessMagicMessage(string message, Process subprocess, IAuthenticator? authenticator)
        {
            var args = message[(MagicMessage.Length + 1)..].Split(MagicMessageDelimiter);
            if (args.Length == 0)
                return;

            switch (args[0])
            {
                case "GetDeviceCode" or "GetEmailCode" or "AcceptDeviceConfirmation" when authenticator == null:
                    throw new InvalidOperationException("Authenticator instance is required");

                case "GetDeviceCode":
                {
                    var previousCodeWasIncorrect = Convert.ToBoolean(args[1]);

                    var code = await authenticator
                        .GetDeviceCodeAsync(previousCodeWasIncorrect)
                        .ConfigureAwait(false);
                    await subprocess.StandardInput
                        .WriteLineAsync(code)
                        .ConfigureAwait(false);
                    break;
                }
                case "GetEmailCode":
                {
                    var email = args[1];
                    var previousCodeWasIncorrect = Convert.ToBoolean(args[2]);

                    var code = await authenticator
                        .GetEmailCodeAsync(email, previousCodeWasIncorrect)
                        .ConfigureAwait(false);
                    await subprocess.StandardInput
                        .WriteLineAsync(code)
                        .ConfigureAwait(false);
                    break;
                }
                case "AcceptDeviceConfirmation":
                {
                    var acceptDeviceConfirmation = await authenticator
                        .AcceptDeviceConfirmationAsync()
                        .ConfigureAwait(false);
                    await subprocess.StandardInput
                        .WriteLineAsync(Convert.ToString(acceptDeviceConfirmation))
                        .ConfigureAwait(false);
                    break;
                }
            }
        }

        internal static void SendMagicMessage(params object[] args)
        {
            Console.WriteLine(string.Join(MagicMessageDelimiter, [MagicMessage, .. args]));
        }

        internal static string WaitForResponse()
        {
            string? line;
            do
            {
                line = Console.ReadLine();
            }
            while (line == null);
            return line;
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

    internal class SubProcessAuthenticator : IAuthenticator
    {
        public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            SubProcess.SendMagicMessage("GetDeviceCode", previousCodeWasIncorrect);
            return Task.FromResult(SubProcess.WaitForResponse());
        }

        public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            SubProcess.SendMagicMessage("GetEmailCode", email, previousCodeWasIncorrect);
            return Task.FromResult(SubProcess.WaitForResponse());
        }

        public Task<bool> AcceptDeviceConfirmationAsync()
        {
            SubProcess.SendMagicMessage("AcceptDeviceConfirmation");
            return Task.FromResult(Convert.ToBoolean(SubProcess.WaitForResponse()));
        }
    }
}
