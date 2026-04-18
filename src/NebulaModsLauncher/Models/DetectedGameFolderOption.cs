namespace ModLauncher.Models;

public sealed class DetectedGameFolderOption
{
    public string PlatformKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Path { get; init; } = "";

    public string DisplayTitle => $"{DisplayName} — {Path}";
}
