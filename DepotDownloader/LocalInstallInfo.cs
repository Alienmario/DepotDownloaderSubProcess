// This file is subject to the terms and conditions defined
// in file 'LICENSE', which is part of this source code package.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DepotDownloader
{
    public enum LocalInstallState { NoData, Incomplete, Complete }

    public class LocalDepotInstallInfo
    {
        [JsonIgnore] public uint DepotId { get; set; }
        public ulong? ManifestId { get; set; }
        [JsonIgnore] public bool IsComplete => ManifestId != null && ManifestId != ContentDownloader.INVALID_MANIFEST_ID;
    }

    public class LocalInstallInfo
    {
        private uint? _buildId;

        [JsonIgnore] public string InstallPath { get; set; } = string.Empty;

        [JsonIgnore]
        public LocalInstallState State => Depots.Count == 0
            ? LocalInstallState.NoData
            : Depots.Values.All(d => d.IsComplete)
                ? LocalInstallState.Complete
                : LocalInstallState.Incomplete;

        public uint? AppId { get; set; }
        public string? Branch { get; set; }

        public uint? BuildId
        {
            get => State == LocalInstallState.Complete ? _buildId : null;
            set => _buildId = value is > 0 ? value : null;
        }

        public Dictionary<uint, LocalDepotInstallInfo> Depots { get; set; } = [];

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(InstallPath)) return;
            var configDir = Path.Combine(InstallPath, ContentDownloader.CONFIG_DIR);
            Directory.CreateDirectory(configDir);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(this, LocalInstallInfoJsonContext.Default.LocalInstallInfo);
            File.WriteAllBytes(Path.Combine(configDir, ContentDownloader.DEPOT_CONFIG_FILE), bytes);
        }

        internal static LocalInstallInfo Load(string installPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(installPath);

            var filename = Path.Combine(installPath, ContentDownloader.CONFIG_DIR, ContentDownloader.DEPOT_CONFIG_FILE);
            if (!File.Exists(filename))
            {
                return new LocalInstallInfo { InstallPath = installPath };
            }

            try
            {
                var bytes = File.ReadAllBytes(filename);
                var info = JsonSerializer.Deserialize(bytes, LocalInstallInfoJsonContext.Default.LocalInstallInfo)
                    ?? new LocalInstallInfo();
                info.InstallPath = installPath;
                info.Depots ??= [];
                foreach (var (depotId, depot) in info.Depots)
                {
                    if (depot == null)
                        throw new JsonException($"Depot entry '{depotId}' is null.");
                    depot.DepotId = depotId;
                }
                return info;
            }
            catch (JsonException ex)
            {
                throw new DepotDownloaderApiException(DepotDownloaderApiException.GeneralError,
                    $"Install metadata '{filename}' contains invalid or corrupt data: {ex.Message}");
            }
        }
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(LocalInstallInfo))]
    internal partial class LocalInstallInfoJsonContext : JsonSerializerContext { }
}
