using System.Text.Json;
using ModLauncher.Models;

namespace ModLauncher.Services;

public sealed class LauncherStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(LauncherPaths.StateFilePath))
            return new LauncherState();

        await using var stream = File.OpenRead(LauncherPaths.StateFilePath);

        var state = await JsonSerializer.DeserializeAsync<LauncherState>(
            stream,
            JsonOptions,
            cancellationToken);

        return Normalize(state);
    }

    public async Task SaveAsync(LauncherState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(LauncherPaths.DataRoot);

        var normalized = Normalize(state);
        var tempPath = $"{LauncherPaths.StateFilePath}.tmp";

        if (File.Exists(tempPath))
            File.Delete(tempPath);

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
        }

        if (File.Exists(LauncherPaths.StateFilePath))
            File.Delete(LauncherPaths.StateFilePath);

        File.Move(tempPath, LauncherPaths.StateFilePath);
    }

    private static LauncherState Normalize(LauncherState? state)
    {
        state ??= new LauncherState();
        state.GameFolderPath ??= "";
        state.DownloadTargetKey = string.IsNullOrWhiteSpace(state.DownloadTargetKey)
            ? DownloadTargetOption.Auto.Key
            : state.DownloadTargetKey;
        state.AssetSelectionModeKey = string.IsNullOrWhiteSpace(state.AssetSelectionModeKey)
            ? AssetSelectionModeOption.Auto.Key
            : state.AssetSelectionModeKey;
        state.LanguageKey = string.IsNullOrWhiteSpace(state.LanguageKey)
            ? LanguageOption.English.Key
            : state.LanguageKey;
        state.InstalledMods ??= new List<InstalledModState>();

        foreach (var installedMod in state.InstalledMods)
        {
            installedMod.ModId ??= "";
            installedMod.Version ??= "";
            installedMod.InstalledFromFile ??= "";
            installedMod.InstalledPluginPath ??= "";
        }

        return state;
    }
}
