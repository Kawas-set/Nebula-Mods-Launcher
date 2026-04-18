using System.ComponentModel;
using ModLauncher.Services;

namespace ModLauncher.Models;

public sealed class DownloadTargetOption : INotifyPropertyChanged
{
    private DownloadTargetOption(string key)
    {
        Key = key;
        AppLocalizer.Instance.LanguageChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }

    public static readonly DownloadTargetOption Auto = new("auto");
    public static readonly DownloadTargetOption SteamItch = new("steam_itch");
    public static readonly DownloadTargetOption MicrosoftStore = new("microsoft_store");
    public static readonly DownloadTargetOption EpicGames = new("epic_games");

    public static IReadOnlyList<DownloadTargetOption> All { get; } =
    [
        Auto,
        SteamItch,
        MicrosoftStore,
        EpicGames
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string DisplayName => AppLocalizer.Instance.TranslateDownloadTarget(Key);

    public static DownloadTargetOption FromKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Auto;

        return All.FirstOrDefault(option =>
                   option.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
               ?? Auto;
    }

    public override string ToString() => DisplayName;
}
