using System.ComponentModel;
using ModLauncher.Services;

namespace ModLauncher.Models;

public sealed class AssetSelectionModeOption : INotifyPropertyChanged
{
    private AssetSelectionModeOption(string key)
    {
        Key = key;
        AppLocalizer.Instance.LanguageChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }

    public static readonly AssetSelectionModeOption Auto = new("auto");
    public static readonly AssetSelectionModeOption DllOnly = new("dll_only");
    public static readonly AssetSelectionModeOption ArchiveOnly = new("archive_only");

    public static IReadOnlyList<AssetSelectionModeOption> All { get; } =
    [
        Auto,
        DllOnly,
        ArchiveOnly
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string DisplayName => AppLocalizer.Instance.TranslateAssetMode(Key);

    public static AssetSelectionModeOption FromKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Auto;

        return All.FirstOrDefault(option =>
                   option.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
               ?? Auto;
    }

    public override string ToString() => DisplayName;
}
