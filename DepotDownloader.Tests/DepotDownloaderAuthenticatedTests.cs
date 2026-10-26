using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using SteamKit2.Authentication;
using Xunit;

namespace DepotDownloader.Tests
{
    // To set Steam login:
    // dotnet user-secrets set "username" "..."
    // dotnet user-secrets set "pw" "..."
    public class DepotDownloaderAuthenticatedTests(ITestOutputHelper testOutputHelper)
    {
        private readonly IConfiguration _secrets = new ConfigurationBuilder()
            .AddUserSecrets<DepotDownloaderAuthenticatedTests>()
            .Build();

        private readonly DataReceivedEventHandler _messageHandler = (sender, e) =>
        {
            if (e.Data != null) testOutputHelper.WriteLine("DepotDownloader: " + e.Data);
        };

        [Fact]
        public async Task AppDownloadViaTaskWithAuthenticator()
        {
            Assert.SkipWhen(string.IsNullOrEmpty(_secrets["username"]) || string.IsNullOrEmpty(_secrets["pw"]),
                "Login info not set");

            var appDownloadConfig = new AppDownloadConfig
            {
                AppId = 3811760,
                Username = _secrets["username"],
                Password = _secrets["pw"]
            };

            await DepotDownloaderApi.AppDownload(appDownloadConfig, _messageHandler, _messageHandler,
                new UserConsoleAuthenticator(), TestContext.Current.CancellationToken);
        }
    }
}
