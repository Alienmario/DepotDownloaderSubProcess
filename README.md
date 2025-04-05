DepotDownloaderSubProcess
===============

A C# API wrapping `Steam depot downloader` in a sub-process utilizing `Tmds.ExecFunction` library.

This avoids many pitfalls of calling the original utility that has been specifically written for CLI usage.
Original code remains largely unchanged to facilitate easy sync with upstream.

## Known issues
So far only anonymous login has been tested.

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
    InstallDirectory = "C:/my_apps",
    VerifyAll = true,
    AppId = 244310
};

int? exitCode = null;
await foreach ((string message, bool error) in DepotDownloader.SubProcess.AppDownload(cfg, i => exitCode = i, cts.Token))
{
    Console.WriteLine("DepotDownloader: " + message);
}

if (exitCode != DepotDownloader.SubProcess.Success)
{
    Console.WriteLine("Download failed");
}
```
