DepotDownloaderSubProcess
===============
[![NuGet Version](https://img.shields.io/nuget/v/DepotDownloaderSubProcess)](https://www.nuget.org/packages/DepotDownloaderSubProcess)
[![.NET Core CI](https://github.com/Alienmario/DepotDownloaderSubProcess/actions/workflows/build.yml/badge.svg)](https://github.com/Alienmario/DepotDownloaderSubProcess/actions/workflows/build.yml)


A C# API wrapping `Steam depot downloader` in a sub-process utilizing `Tmds.ExecFunction` library.

This avoids many pitfalls of calling the original utility that has been specifically written for CLI usage.
Original code remains largely unchanged to facilitate easy sync with upstream.

> [!WARNING]
> Does not support trimming or AOT - this stems from DepotDownloader itself.
> Make sure `<PublishTrimmed>` and `<PublishAot>` are not true.

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
Prepare inputs:
```c#
var cts = new CancellationTokenSource();
var authenticator = new UserConsoleAuthenticator();
var cfg = new AppDownloadConfig
{
    // AccountSettingsFileName = "custom.config",
    // Username = "user",
    // Password = "pass",
    InstallDirectory = "C:/apps/244310",
    VerifyAll = true,
    AppId = 244310
};
```
Method **A**) Using an async enumerable:
```c#
try
{
    await foreach (var (message, isError) in DepotDownloaderApi.AppDownload(cfg, authenticator, cts.Token))
    {
        Console.WriteLine("DepotDownloader: " + message);
    }
}
catch (DepotDownloaderApiException e)
{
    Console.WriteLine("Download failed: " + e.Message);
}
```
Method **B**) Using a task and message callbacks:
```c#
void MessageHandler(object sender, DataReceivedEventArgs e)
{
    if (e.Data != null) Console.WriteLine("DepotDownloader: " + e.Data);
}

try
{
    await DepotDownloaderApi.AppDownload(cfg, MessageHandler, MessageHandler, authenticator, cts.Token);
}
catch (DepotDownloaderApiException e)
{
    Console.WriteLine("Download failed: " + e.Message);
}
```
### Pubfile (Workshop) download
Pubfile download is similiar to App download, but uses `PubFileDownloadConfig` :
```c#
var cfg = new PubFileDownloadConfig
{
    AppId = ...,
    PublishedFileId = ...
};
... DepotDownloaderApi.PubFileDownload(cfg, ...)
```

### UGC (Workshop) download
UGC download is similiar to App download, but uses `UGCDownloadConfig` :
```c#
var cfg = new UGCDownloadConfig
{
    AppId = ...,
    UGCId = ...
};
... DepotDownloaderApi.UGCDownload(cfg, ...)
```

### Get app Build ID
Returns most recent build ID for an app and a branch (default: public)
```c#
var cfg = new GetAppBuildIdConfig { AppId = ... };
try
{
    uint buildId = await DepotDownloaderApi.GetAppBuildId(cfg);
}
catch (DepotDownloaderApiException e)
{
    Console.WriteLine("Build ID check failed: " + e.Message);
}
```
