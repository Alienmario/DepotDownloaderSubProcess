using Tmds.Utils;
using Xunit.MicrosoftTestingPlatform;
using Xunit.Runner.InProc.SystemConsole;

namespace DepotDownloader.Tests;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (ExecFunction.IsExecFunctionCommand(args))
            return ExecFunction.Program.Main(args);

        if (args.Any(arg => arg is "--server" or "--internal-msbuild-node"))
            return TestPlatformTestFramework.RunAsync(args, SelfRegisteredExtensions.AddSelfRegisteredExtensions).GetAwaiter().GetResult();

        return ConsoleRunner.Run(args).GetAwaiter().GetResult();
    }
}
