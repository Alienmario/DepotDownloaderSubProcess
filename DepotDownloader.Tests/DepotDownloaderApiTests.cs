using System.Diagnostics;
using Xunit;

namespace DepotDownloader.Tests
{
    public class DepotDownloaderApiTests(ITestOutputHelper testOutputHelper)
    {
        private readonly DataReceivedEventHandler _messageHandler = (sender, e) =>
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
            await DepotDownloaderApi.AppDownload(appDownloadConfig, _messageHandler, _messageHandler,
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
        public async Task GetAppBuildId()
        {
            Assert.Equal(587726u, await DepotDownloaderApi.GetAppBuildId(307290, cancellationToken: TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetAppBranches()
        {
            var branches = await DepotDownloaderApi.GetAppBranches(413150, TestContext.Current.CancellationToken);
            Assert.NotEmpty(branches);
            testOutputHelper.WriteLine("- Listing branches -");
            foreach (var b in branches)
            {
                testOutputHelper.WriteLine($"{b.Name}: BuildId={b.BuildId}, Desc='{b.Description}', Pwd={b.PasswordRequired}, Time={b.TimeUpdated}");
            }
            Assert.Contains(branches, b => b.Name == "public" && b.BuildId > 0);
        }

        [Fact]
        public async Task GetLocalInstallInfoAfterAppDownload()
        {
            var installDir = CreateTempDir();
            try
            {
                var appDownloadConfig = new AppDownloadConfig
                {
                    AppId = 1007,
                    InstallDirectory = installDir
                };
                await DepotDownloaderApi.AppDownload(appDownloadConfig, _messageHandler, _messageHandler,
                    cancellationToken: TestContext.Current.CancellationToken);

                var expectedBuildId = await DepotDownloaderApi.GetAppBuildId(1007,
                    cancellationToken: TestContext.Current.CancellationToken);
                var info = DepotDownloaderApi.GetLocalInstallInfo(installDir);

                Assert.Equal(LocalInstallState.Complete, info.State);
                Assert.Equal(1007u, info.AppId);
                Assert.Equal(ContentDownloader.DEFAULT_BRANCH, info.Branch);
                Assert.Equal(expectedBuildId, info.BuildId);
                Assert.NotEmpty(info.Depots);
                Assert.All(info.Depots.Values, d => Assert.True(d.IsComplete));
            }
            finally
            {
                Directory.Delete(installDir, true);
            }
        }

        [Fact]
        public void GetLocalInstallInfoNoData()
        {
            var installDir = CreateTempDir();
            try
            {
                var info = DepotDownloaderApi.GetLocalInstallInfo(installDir);
                Assert.Equal(LocalInstallState.NoData, info.State);
                Assert.Null(info.BuildId);
                Assert.Null(info.AppId);
                Assert.Null(info.Branch);
                Assert.Empty(info.Depots);

                info = DepotDownloaderApi.GetLocalInstallInfo(Path.Combine(installDir, "missing"));
                Assert.Equal(LocalInstallState.NoData, info.State);
            }
            finally
            {
                Directory.Delete(installDir, true);
            }
        }

        [Fact]
        public void GetLocalInstallInfoIncomplete()
        {
            var installDir = CreateTempDir();
            try
            {
                WriteDepotJson(installDir, new LocalInstallInfo
                {
                    AppId = 1007,
                    Branch = "public",
                    BuildId = 500,
                    Depots = new()
                    {
                        [10] = new() { ManifestId = 111 },
                        [11] = new() { ManifestId = null }
                    }
                });

                var info = DepotDownloaderApi.GetLocalInstallInfo(installDir);
                Assert.Equal(LocalInstallState.Incomplete, info.State);
                Assert.Equal(1007u, info.AppId);
                Assert.Equal("public", info.Branch);
                Assert.Null(info.BuildId);
                Assert.Equal(2, info.Depots.Count);

                Assert.True(info.Depots.TryGetValue(10, out var d10));
                Assert.Equal(10u, d10.DepotId);
                Assert.Equal(111ul, d10.ManifestId);
                Assert.True(d10.IsComplete);

                Assert.True(info.Depots.TryGetValue(11, out var d11));
                Assert.Equal(11u, d11.DepotId);
                Assert.Null(d11.ManifestId);
                Assert.False(d11.IsComplete);
            }
            finally
            {
                Directory.Delete(installDir, true);
            }
        }

        [Fact]
        public void GetLocalInstallInfoComplete()
        {
            var installDir = CreateTempDir();
            try
            {
                WriteDepotJson(installDir, new LocalInstallInfo
                {
                    AppId = 1007,
                    Branch = "public",
                    BuildId = 500,
                    Depots = new()
                    {
                        [10] = new() { ManifestId = 111 },
                        [11] = new() { ManifestId = 222 }
                    }
                });

                var info = DepotDownloaderApi.GetLocalInstallInfo(installDir);
                Assert.Equal(LocalInstallState.Complete, info.State);
                Assert.Equal(1007u, info.AppId);
                Assert.Equal("public", info.Branch);
                Assert.Equal(500u, info.BuildId);
                Assert.Equal(2, info.Depots.Count);
            }
            finally
            {
                Directory.Delete(installDir, true);
            }
        }

        [Fact]
        public void GetLocalInstallInfoCorruptJsonThrows()
        {
            var installDir = CreateTempDir();
            try
            {
                var configDir = Path.Combine(installDir, ".DepotDownloader");
                Directory.CreateDirectory(configDir);
                File.WriteAllText(Path.Combine(configDir, "depot.json"), "invalid json {");

                Assert.Throws<DepotDownloaderApiException>(() => DepotDownloaderApi.GetLocalInstallInfo(installDir));

                File.WriteAllText(Path.Combine(configDir, "depot.json"), """{"depots":{"10":null}}""");

                Assert.Throws<DepotDownloaderApiException>(() => DepotDownloaderApi.GetLocalInstallInfo(installDir));
            }
            finally
            {
                Directory.Delete(installDir, true);
            }
        }

        [Fact]
        public void GetLocalInstallInfoCompleteWithoutBuildId()
        {
            var installDir = CreateTempDir();
            try
            {
                WriteDepotJson(installDir, new LocalInstallInfo
                {
                    Depots = new()
                    {
                        [10] = new() { ManifestId = 111 }
                    }
                });

                var info = DepotDownloaderApi.GetLocalInstallInfo(installDir);
                Assert.Equal(LocalInstallState.Complete, info.State);
                Assert.Null(info.BuildId);
                Assert.Equal(111ul, Assert.Single(info.Depots.Values).ManifestId);
            }
            finally
            {
                Directory.Delete(installDir, true);
            }
        }

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "DepotDownloaderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void WriteDepotJson(string installDir, LocalInstallInfo info)
        {
            info.InstallPath = installDir;
            info.Save();
        }
    }
}
