DepotDownloaderSubProcess
===============
[![NuGet Version](https://img.shields.io/nuget/v/DepotDownloaderSubProcess)](https://www.nuget.org/packages/DepotDownloaderSubProcess)
[![.NET Core CI](https://github.com/Alienmario/DepotDownloaderSubProcess/actions/workflows/build.yml/badge.svg)](https://github.com/Alienmario/DepotDownloaderSubProcess/actions/workflows/build.yml)


A C# API wrapping `Steam depot downloader` in a sub-process utilizing `Tmds.ExecFunction` library.

This avoids many pitfalls of calling the original utility that has been specifically written for CLI usage.
Original code remains largely unchanged to facilitate easy sync with upstream.

## Usage

As per [ExecFunction](https://github.com/tmds/Tmds.ExecFunction) documentation, add the following to your Main method.
```c#
public static int Main(string[] args)
{
    // Starting a subprocess function? [Tmds.ExecFunction]
    if (ExecFunction.IsExecFunctionCommand(args))
    {
        return ExecFunction.Program.Main(args);
    }

    // Start normally..
}
```
### App download
```c#
CancellationTokenSource cts = new CancellationTokenSource();

var cfg = new DepotDownloader.AppDownloadConfig
{
    // AccountSettingsFileName = "custom.config",
    // Username = "user",
    // Password = "pass",
    InstallDirectory = "C:/apps/244310",
    VerifyAll = true,
    AppId = 244310
};
```
A) Using an async enumerable:
```c#
int? exitCode = null;
await foreach ((string message, bool error) in DepotDownloader.SubProcess.AppDownload(
        cfg, ec => exitCode = ec, new UserConsoleAuthenticator(), cts.Token))
{
    Console.WriteLine("DepotDownloader: " + message);
}

if (exitCode != DepotDownloader.SubProcess.Success)
{
    Console.WriteLine("Download failed");
}
```
B) Using a task and callbacks:
```c#
void MessageHandler(object sender, DataReceivedEventArgs e)
{
    if (e.Data != null) Console.WriteLine("DepotDownloader: " + e.Data);
}

int exitCode = await DepotDownloader.SubProcess.AppDownload(
        cfg, MessageHandler, MessageHandler, new UserConsoleAuthenticator(), cts.Token);

if (exitCode != DepotDownloader.SubProcess.Success)
{
    Console.WriteLine("Download failed");
}
```
