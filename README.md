DepotDownloaderSubProcess
===============
[![NuGet Version](https://img.shields.io/nuget/v/DepotDownloaderSubProcess)](https://www.nuget.org/packages/DepotDownloaderSubProcess)
[![.NET Core CI](https://github.com/Alienmario/DepotDownloaderSubProcess/actions/workflows/build.yml/badge.svg)](https://github.com/Alienmario/DepotDownloaderSubProcess/actions/workflows/build.yml)


A C# API wrapping `Steam depot downloader` in a sub-process utilizing `Tmds.ExecFunction` library.

This avoids many pitfalls of calling the original utility that has been specifically written for CLI usage.
Original code remains largely unchanged to facilitate easy sync with upstream.

> [!NOTE]
> **Install Metadata Format:** Unlike upstream DepotDownloader, which stores local depot configuration as deflate-compressed protobuf in `.DepotDownloader/depot.config`, this project stores richer local install metadata (`AppId`, `Branch`, `BuildId`, and per-depot `ManifestId` and completion state) as JSON in `.DepotDownloader/depot.json` via `LocalInstallInfo` (queryable with `DepotDownloaderApi.GetLocalInstallInfo`).

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
Internally delegates to GetAppBranches call.
```c#
try
{
    uint buildId = await DepotDownloaderApi.GetAppBuildId(appId);
}
catch (DepotDownloaderApiException e)
{
    Console.WriteLine("Build ID check failed: " + e.Message);
}
```

### Get app branches
Returns a list of available branches and their details (`Name`, `BuildId`, `Description`, `PasswordRequired`, `TimeUpdated`) for an app:
```c#
try
{
    List<AppBranchInfo> branches = await DepotDownloaderApi.GetAppBranches(appId);
}
catch (DepotDownloaderApiException e)
{
    Console.WriteLine("Branch check failed: " + e.Message);
}
```

### Get local install info
Reads local install status (`State`, `AppId`, `Branch`, `BuildId`, `Depots`) from the metadata in `.DepotDownloader/depot.json`:
```c#
try
{
    LocalInstallInfo installInfo = DepotDownloaderApi.GetLocalInstallInfo("C:/apps/244310");
}
catch (DepotDownloaderApiException e)
{
    Console.WriteLine("Failed to read install info: " + e.Message);
}
```
