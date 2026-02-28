using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using SteamKit2.Authentication;
using Xunit;

namespace DepotDownloader.Tests
{
    public class DepotDownloaderApiTests(ITestOutputHelper testOutputHelper)
    {
        private readonly IConfiguration secrets = new ConfigurationBuilder()
            .AddUserSecrets<DepotDownloaderApiTests>()
            .Build();

        private readonly DataReceivedEventHandler messageHandler = (sender, e) =>
        {
            if (e.Data != null) testOutputHelper.WriteLine("DepotDownloader: " + e.Data);
        };

        [Fact]
        public async Task AppDownloadViaTask()
        {
            var appDownloadConfig = new AppDownloadConfig
            {
                AppId = 1007
            };
            await DepotDownloaderApi.AppDownload(appDownloadConfig, messageHandler, messageHandler,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task AppDownloadViaAsyncEnumerable()
        {
            var appDownloadConfig = new AppDownloadConfig
            {
                AppId = 1007
            };
            try
            {
                await foreach (var (msg, isError) in DepotDownloaderApi.AppDownload(appDownloadConfig,
                                   cancellationToken: TestContext.Current.CancellationToken))
                {
                    testOutputHelper.WriteLine("DepotDownloader: " + msg);
                }
            }
            catch (DepotDownloaderApiException e)
            {
                testOutputHelper.WriteLine("DepotDownloader: " + e.Message);
            }
        }

        [Fact]
        public async Task AppDownloadViaTaskWithAuthenticator()
        {
            // To set login info:
            // dotnet user-secrets set "username" "..."
            // dotnet user-secrets set "pw" "..."
            Assert.SkipWhen(string.IsNullOrEmpty(secrets["username"]) || string.IsNullOrEmpty(secrets["pw"]),
                "Login info not set");

            var appDownloadConfig = new AppDownloadConfig
            {
                AppId = 3811760,
                Username = secrets["username"],
                Password = secrets["pw"]
            };

            await DepotDownloaderApi.AppDownload(appDownloadConfig, messageHandler, messageHandler,
                new UserConsoleAuthenticator(), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task GetAppBuildId()
        {
            var cfg = new GetAppBuildIdConfig { AppId = 307290 };
            Assert.Equal(587726u, await DepotDownloaderApi.GetAppBuildId(cfg, TestContext.Current.CancellationToken));
        }
    }
}
