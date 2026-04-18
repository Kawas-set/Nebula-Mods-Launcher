namespace ModLauncher.Models;

public sealed class ModCatalogEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
    public string? IconPath { get; set; }
    public string? SourceUrl { get; set; }
    public string? License { get; set; }
    public string? PreferredAssetName { get; set; }
    public string? SteamItchAssetName { get; set; }
    public string? MicrosoftStoreAssetName { get; set; }
    public string? EpicGamesAssetName { get; set; }
}
