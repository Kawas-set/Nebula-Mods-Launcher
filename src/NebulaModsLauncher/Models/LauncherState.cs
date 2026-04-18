namespace ModLauncher.Models;

public sealed class LauncherState
{
    public string GameFolderPath { get; set; } = "";
    public string DownloadTargetKey { get; set; } = DownloadTargetOption.Auto.Key;
    public string AssetSelectionModeKey { get; set; } = AssetSelectionModeOption.Auto.Key;
    public string LanguageKey { get; set; } = LanguageOption.English.Key;
    public List<InstalledModState> InstalledMods { get; set; } = new();
}

public sealed class InstalledModState
{
    public string ModId { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
    public string InstalledFromFile { get; set; } = "";
    public string InstalledPluginPath { get; set; } = "";
}
