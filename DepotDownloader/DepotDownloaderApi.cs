// This file is subject to the terms and conditions defined
// in file 'LICENSE', which is part of this source code package.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

    public class PubFileDownloadConfig : DownloadConfig
    {
        public uint AppId { get; set; }
        public ulong PublishedFileId { get; set; }
    }

    public class UGCDownloadConfig : DownloadConfig
    {
        public uint AppId { get; set; }
        public ulong UGCId { get; set; }
    }

    public class GetAppBuildIdConfig
    {
        public uint AppId { get; set; }
        public string Branch { get; set; } = ContentDownloader.DEFAULT_BRANCH;
    }

    public class DepotDownloaderApiException(int code, string? message) : Exception(message)
    {
        public const int UnknownError = 1;
        public const int GeneralError = 2;
        public const int LoginError = 3;

        public int Code { get; } = code;

        public DepotDownloaderApiException(int code) : this(code, code switch
        {
            UnknownError => "Unknown error",
            GeneralError => "General error",
            LoginError => "Login error",
            _ => null
        })
        { }
    }

    /// <summary>
    /// Provides API capability for the DepotDownloader utility.
    /// </summary>
    public static class DepotDownloaderApi
    {
        /// Messages starting with this string are control messages. Used for communicating with the parent process.
        internal const string MagicMessage = "$DDSPMM*";
        /// Delimiter by which individual args of control messages are separated.
        internal const char MagicMessageDelimiter = ControlChars.NullChar;

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static IAsyncEnumerable<(string msg, bool isError)> AppDownload(
            AppDownloadConfig config,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<object?>(AppDownloadInner, config, null, authenticator, cancellationToken);
        }

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static Task AppDownload(
            AppDownloadConfig config,
            DataReceivedEventHandler? messageHandler,
            DataReceivedEventHandler? errorMessageHandler,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<object?>(AppDownloadInner, config, messageHandler, errorMessageHandler, authenticator, cancellationToken);
        }

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static IAsyncEnumerable<(string msg, bool isError)> PubFileDownload(
            PubFileDownloadConfig config,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<object?>(PubFileDownloadInner, config, null, authenticator, cancellationToken);
        }

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static Task PubFileDownload(
            PubFileDownloadConfig config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<object?>(PubFileDownloadInner, config, messageHandler, errorMessageHandler, authenticator, cancellationToken);
        }

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static IAsyncEnumerable<(string msg, bool isError)> UGCDownload(
            UGCDownloadConfig config,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<object?>(UGCDownloadInner, config, null, authenticator, cancellationToken);
        }

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static Task UGCDownload(
            UGCDownloadConfig config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<object?>(UGCDownloadInner, config, messageHandler, errorMessageHandler, authenticator, cancellationToken);
        }

        /// <exception cref="DepotDownloaderApiException"></exception>
        public static Task<uint> GetAppBuildId(
            GetAppBuildIdConfig config,
            CancellationToken cancellationToken = default)
        {
            return ExecInProcess<uint>(GetAppBuildIdInner, config, null, null, null, cancellationToken);
        }

        private static async Task<int> AppDownloadInner(string[] input)
        {
            var cfg = InitSubProcess<AppDownloadConfig>(input);

            if (!ContentDownloader.InitializeSteam3(cfg.Username, cfg.Password))
            {
                return DepotDownloaderApiException.LoginError;
            }

            try
            {
                await ContentDownloader.DownloadAppAsync(cfg.AppId, cfg.DepotManifestIds, cfg.Branch, cfg.OS, cfg.Arch,
                    cfg.Language, cfg.LowViolence, false).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex) when (ex is ContentDownloaderException or OperationCanceledException)
            {
                Console.WriteLine(ex.Message);
                return DepotDownloaderApiException.GeneralError;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Download failed due to an unhandled exception: {0}", e.Message);
                return DepotDownloaderApiException.UnknownError;
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }
        }

        private static async Task<int> PubFileDownloadInner(string[] input)
        {
            var cfg = InitSubProcess<PubFileDownloadConfig>(input);

            if (!ContentDownloader.InitializeSteam3(cfg.Username, cfg.Password))
            {
                return DepotDownloaderApiException.LoginError;
            }

            try
            {
                await ContentDownloader.DownloadPubfileAsync(cfg.AppId, cfg.PublishedFileId).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex) when (ex is ContentDownloaderException or OperationCanceledException)
            {
                Console.WriteLine(ex.Message);
                return DepotDownloaderApiException.GeneralError;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Download failed due to an unhandled exception: {0}", e.Message);
                return DepotDownloaderApiException.UnknownError;
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }
        }

        private static async Task<int> UGCDownloadInner(string[] input)
        {
            var cfg = InitSubProcess<UGCDownloadConfig>(input);

            if (!ContentDownloader.InitializeSteam3(cfg.Username, cfg.Password))
            {
                return DepotDownloaderApiException.LoginError;
            }

            try
            {
                await ContentDownloader.DownloadUGCAsync(cfg.AppId, cfg.UGCId).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex) when (ex is ContentDownloaderException or OperationCanceledException)
            {
                Console.WriteLine(ex.Message);
                return DepotDownloaderApiException.GeneralError;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Download failed due to an unhandled exception: {0}", e.Message);
                return DepotDownloaderApiException.UnknownError;
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }
        }

        private static Task<int> GetAppBuildIdInner(string[] input)
        {
            var cfg = InitSubProcess<GetAppBuildIdConfig>(input);

            if (!ContentDownloader.InitializeSteam3(null, null))
            {
                return Task.FromResult(DepotDownloaderApiException.LoginError);
            }

            try
            {
                var buildId = ContentDownloader.GetSteam3AppBuildNumber(cfg.AppId, cfg.Branch);
                SetReturnValue(buildId);
                return Task.FromResult(0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return Task.FromResult(DepotDownloaderApiException.UnknownError);
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }
        }

        /// Executes given action in a new process, returning an IAsyncEnumerable that yields for every console message.
        /// <exception cref="DepotDownloaderApiException"></exception>
        private static async IAsyncEnumerable<(string msg, bool isError)> ExecInProcess<R>(
            Func<string[], Task<int>> innerFunc,
            object config,
            Action<R>? returnValueProcessor = null,
            IAuthenticator? authenticator = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var process = ExecFunction.Start(innerFunc, [Serialize(config), Environment.ProcessId.ToString()], options =>
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
                            await ProcessMagicMessage(message, process, authenticator, returnValueProcessor)
                                .ConfigureAwait(false);
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

            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                throw new DepotDownloaderApiException(exitCode);
            }
        }

        /// Executes given action in a new process, returning an awaitable task.
        /// <exception cref="DepotDownloaderApiException"></exception>
        private static async Task<R?> ExecInProcess<R>(
            Func<string[], Task<int>> innerFunc,
            object config,
            DataReceivedEventHandler? messageHandler = null,
            DataReceivedEventHandler? errorMessageHandler = null,
            IAuthenticator? authenticator = null,
            CancellationToken cancellationToken = default)
        {
            R? returnValue = default;
            var process = ExecFunction.Start(innerFunc, [Serialize(config), Environment.ProcessId.ToString()], options =>
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
                        await ProcessMagicMessage<R>(args.Data, process, authenticator, r => returnValue = r)
                            .ConfigureAwait(false);
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

            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                throw new DepotDownloaderApiException(exitCode);
            }
            return returnValue;
        }

        private static T InitSubProcess<T>(string[] input)
        {
            var cfg = Deserialize<T>(input[0]);

            if (cfg is DownloadConfig dcfg)
            {
                if (dcfg.MaxDownloads <= 0)
                {
                    dcfg.MaxDownloads = 8;
                }
                if (dcfg.AccountSettingsFileName != null)
                {
                    AccountSettingsStore.LoadFromFile(dcfg.AccountSettingsFileName);
                }
                ContentDownloader.Config = dcfg;
            }
            ContentDownloader.Authenticator = new SubProcessAuthenticator();

            var parentProcess = Process.GetProcessById(Convert.ToInt32(input[1]));
            parentProcess.EnableRaisingEvents = true;
            parentProcess.Exited += (sender, args) =>
            {
                Process.GetCurrentProcess().Kill(true);
            };

            return cfg;
        }

        private static async Task ProcessMagicMessage<R>(string message,
            Process subprocess,
            IAuthenticator? authenticator,
            Action<R>? returnValueProcessor)
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
                case "SetReturnValue" when returnValueProcessor != null:
                {
                    var returnValue = Deserialize<R>(args[1]);
                    returnValueProcessor(returnValue);
                    break;
                }
            }
        }

        internal static void SetReturnValue(object returnValue)
        {
            SendMagicMessage("SetReturnValue", Serialize(returnValue));
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
            return JsonSerializer.Serialize(obj);
        }

        private static T Deserialize<T>(string str)
        {
            return JsonSerializer.Deserialize<T>(str)!;
        }
    }

    internal class SubProcessAuthenticator : IAuthenticator
    {
        public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            DepotDownloaderApi.SendMagicMessage("GetDeviceCode", previousCodeWasIncorrect);
            return Task.FromResult(DepotDownloaderApi.WaitForResponse());
        }

        public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            DepotDownloaderApi.SendMagicMessage("GetEmailCode", email, previousCodeWasIncorrect);
            return Task.FromResult(DepotDownloaderApi.WaitForResponse());
        }

        public Task<bool> AcceptDeviceConfirmationAsync()
        {
            DepotDownloaderApi.SendMagicMessage("AcceptDeviceConfirmation");
            return Task.FromResult(Convert.ToBoolean(DepotDownloaderApi.WaitForResponse()));
        }
    }
}
