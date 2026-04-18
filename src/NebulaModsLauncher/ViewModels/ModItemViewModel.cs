using System.Collections.ObjectModel;
using Avalonia.Media;
using ModLauncher.Models;
using ModLauncher.Services;

namespace ModLauncher.ViewModels;

public sealed class ModItemViewModel : ViewModelBase
{
    public const string StatusUnchecked = "unchecked";
    public const string StatusChecking = "checking";
    public const string StatusReleaseFound = "release_found";
    public const string StatusReleaseFoundNoFile = "release_found_no_file";
    public const string StatusNoReleases = "no_releases";
    public const string StatusDownloaded = "downloaded";
    public const string StatusInstalled = "installed";
    public const string StatusInstalledUpdate = "installed_update";
    public const string StatusInstalledLocal = "installed_local";
    public const string StatusError = "error";

    private const string PlaceholderValue = "—";

    private static readonly IBrush NeutralAccentBrush = Brush.Parse("#8EA8BA");
    private static readonly IBrush NeutralBackgroundBrush = Brush.Parse("#15222D");
    private static readonly IBrush NeutralBorderBrush = Brush.Parse("#284152");

    private static readonly IBrush InfoAccentBrush = Brush.Parse("#58D6FF");
    private static readonly IBrush InfoBackgroundBrush = Brush.Parse("#143142");
    private static readonly IBrush InfoBorderBrush = Brush.Parse("#2D6B84");

    private static readonly IBrush SuccessAccentBrush = Brush.Parse("#68E0A3");
    private static readonly IBrush SuccessBackgroundBrush = Brush.Parse("#173428");
    private static readonly IBrush SuccessBorderBrush = Brush.Parse("#2D7C58");

    private static readonly IBrush WarningAccentBrush = Brush.Parse("#FFB454");
    private static readonly IBrush WarningBackgroundBrush = Brush.Parse("#392817");
    private static readonly IBrush WarningBorderBrush = Brush.Parse("#7F5630");

    private static readonly IBrush ErrorAccentBrush = Brush.Parse("#FF7D7D");
    private static readonly IBrush ErrorBackgroundBrush = Brush.Parse("#381A20");
    private static readonly IBrush ErrorBorderBrush = Brush.Parse("#7D343B");

    private static readonly IBrush StatusForegroundBrush = Brush.Parse("#F5FBFF");

    private readonly AppLocalizer _loc = AppLocalizer.Instance;

    private string _latestVersion = PlaceholderValue;
    private string _installedVersion = PlaceholderValue;
    private string _installedAt = PlaceholderValue;
    private string _status = StatusUnchecked;
    private string _changelog = "";
    private string _releasePublishedAt = PlaceholderValue;
    private string _assetName = PlaceholderValue;
    private string _downloadedFile = "";
    private DateTimeOffset? _releasePublishedAtValue;
    private GitHubReleaseInfo? _lastRelease;
    private GitHubReleaseAsset? _selectedAsset;
    private bool _hasManualAssetSelection;

    public ModItemViewModel(ModCatalogEntry catalog)
    {
        Catalog = catalog;
        _loc.LanguageChanged += OnLanguageChanged;
    }

    public ModCatalogEntry Catalog { get; }
    public ObservableCollection<GitHubReleaseAsset> AvailableAssets { get; } = new();

    public string Id => Catalog.Id;
    public string Name => Catalog.Name;
    public string Author => Catalog.Author;
    public string RepoDisplay => $"{Catalog.Owner}/{Catalog.Repo}";
    public string SourceUrl => BuildSourceUrl(Catalog);
    public string LicenseDisplay => string.IsNullOrWhiteSpace(Catalog.License)
        ? _loc.PlaceholderUnknownLicense
        : Catalog.License!;
    public string EntryStatusDisplay => _loc.EntryStatusUnofficial;
    public string Monogram => BuildMonogram(Name);
    public bool HasCustomIcon => !string.IsNullOrWhiteSpace(Catalog.IconPath);
    public bool ShowMonogram => !HasCustomIcon;
    public string? IconUri => BuildIconUri(Catalog.IconPath);
    public bool HasReleaseInfo => _lastRelease is not null;
    public bool HasInstalledState => !IsPlaceholderValue(InstalledVersion);
    public bool NeedsRepositoryMetadata =>
        string.IsNullOrWhiteSpace(Catalog.License) || string.IsNullOrWhiteSpace(Catalog.SourceUrl);

    public string PreferredAssetName
    {
        get => Catalog.PreferredAssetName ?? "";
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if ((Catalog.PreferredAssetName ?? "") == (normalized ?? ""))
                return;

            Catalog.PreferredAssetName = normalized;
            OnPropertyChanged();
        }
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set => SetField(ref _latestVersion, value);
    }

    public string InstalledVersion
    {
        get => _installedVersion;
        set => SetField(ref _installedVersion, value);
    }

    public string InstalledAt
    {
        get => _installedAt;
        set => SetField(ref _installedAt, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusPillText));
                OnPropertyChanged(nameof(StatusAccentBrush));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeBorder));
                OnPropertyChanged(nameof(StatusBadgeForeground));
            }
        }
    }

    public string StatusText => _loc.TranslateStatus(Status);
    public string StatusPillText => _loc.TranslateStatusPill(Status);

    public string Changelog
    {
        get => _changelog;
        set => SetField(ref _changelog, value);
    }

    public string ReleasePublishedAt
    {
        get => _releasePublishedAt;
        set => SetField(ref _releasePublishedAt, value);
    }

    public DateTimeOffset? ReleasePublishedAtValue => _releasePublishedAtValue;

    public string AssetName
    {
        get => _assetName;
        set => SetField(ref _assetName, value);
    }

    public string DownloadedFile
    {
        get => _downloadedFile;
        set => SetField(ref _downloadedFile, value);
    }

    public GitHubReleaseAsset? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (ReferenceEquals(_selectedAsset, value))
                return;

            _selectedAsset = value;
            _hasManualAssetSelection = value is not null;
            OnPropertyChanged();
            ApplySelectedAsset(value);
        }
    }

    public GitHubReleaseAsset? LastChosenAsset { get; private set; }

    public IBrush StatusAccentBrush => GetStatusPalette().Accent;
    public IBrush StatusBadgeBackground => GetStatusPalette().Background;
    public IBrush StatusBadgeBorder => GetStatusPalette().Border;
    public IBrush StatusBadgeForeground => StatusForegroundBrush;

    public void ClearReleaseInfo(string statusText)
    {
        LatestVersion = PlaceholderValue;
        Status = statusText;
        Changelog = _loc.PlaceholderNoRelease;
        _releasePublishedAtValue = null;
        ReleasePublishedAt = PlaceholderValue;
        AssetName = _loc.PlaceholderNoSelectedAsset;
        LastChosenAsset = null;
        SelectedAsset = null;
        _hasManualAssetSelection = false;
        AvailableAssets.Clear();
        _lastRelease = null;
    }

    public void ApplyRelease(
        GitHubReleaseInfo release,
        DownloadTargetOption downloadTarget,
        AssetSelectionModeOption assetSelectionMode,
        bool preferPluginDll)
    {
        _lastRelease = release;

        LatestVersion = !string.IsNullOrWhiteSpace(release.TagName)
            ? release.TagName
            : release.Name;

        Changelog = string.IsNullOrWhiteSpace(release.Body)
            ? _loc.PlaceholderEmptyReleaseBody
            : release.Body;

        _releasePublishedAtValue = release.PublishedAt;
        ReleasePublishedAt = release.PublishedAt?.ToLocalTime().ToString("g") ?? PlaceholderValue;

        ReplaceAvailableAssets(release.Assets);

        var suggestedAsset = ChooseAsset(release, downloadTarget, assetSelectionMode, preferPluginDll);
        var preservedAsset = TryFindSelectedAssetByName(_hasManualAssetSelection ? _selectedAsset?.Name : null);
        SetSelectedAssetInternal(preservedAsset ?? suggestedAsset);

        RestoreReleaseStatus();
    }

    public void ApplyInstalledState(string version, DateTimeOffset installedAt, string? archivePath = null)
    {
        InstalledVersion = string.IsNullOrWhiteSpace(version) ? PlaceholderValue : version;
        InstalledAt = installedAt == default
            ? PlaceholderValue
            : installedAt.ToLocalTime().ToString("g");

        if (!string.IsNullOrWhiteSpace(archivePath))
            DownloadedFile = archivePath;
    }

    public void ClearInstalledState()
    {
        InstalledVersion = PlaceholderValue;
        InstalledAt = PlaceholderValue;
    }

    public void UpdateStatusForCurrentState()
    {
        if (HasInstalledState)
        {
            if (!HasReleaseInfo)
            {
                Status = StatusInstalledLocal;
                return;
            }

            var hasUpdate = !IsPlaceholderValue(LatestVersion) &&
                !InstalledVersion.Equals(LatestVersion, StringComparison.OrdinalIgnoreCase);

            Status = hasUpdate
                ? StatusInstalledUpdate
                : StatusInstalled;
            return;
        }

        RestoreReleaseStatus();
    }

    public void ApplyRepositoryMetadata(string? sourceUrl, string? license)
    {
        var normalizedSourceUrl = string.IsNullOrWhiteSpace(sourceUrl)
            ? null
            : sourceUrl.Trim();
        var normalizedLicense = NormalizeLicense(license);

        var hasChanges = false;

        if (!string.Equals(Catalog.SourceUrl, normalizedSourceUrl, StringComparison.Ordinal))
        {
            Catalog.SourceUrl = normalizedSourceUrl;
            hasChanges = true;
            OnPropertyChanged(nameof(SourceUrl));
        }

        if (!string.Equals(Catalog.License, normalizedLicense, StringComparison.Ordinal))
        {
            Catalog.License = normalizedLicense;
            hasChanges = true;
            OnPropertyChanged(nameof(LicenseDisplay));
        }

        if (hasChanges)
            OnPropertyChanged(nameof(NeedsRepositoryMetadata));
    }

    public void RestoreReleaseStatus()
    {
        if (_lastRelease is null)
        {
            Status = StatusUnchecked;
            return;
        }

        Status = LastChosenAsset is null
            ? StatusReleaseFoundNoFile
            : StatusReleaseFound;
    }

    public void RefreshSelectedAsset(
        DownloadTargetOption downloadTarget,
        AssetSelectionModeOption assetSelectionMode,
        bool preferPluginDll)
    {
        if (_lastRelease is null)
            return;

        ReplaceAvailableAssets(_lastRelease.Assets);
        var suggestedAsset = ChooseAsset(_lastRelease, downloadTarget, assetSelectionMode, preferPluginDll);
        var preservedAsset = TryFindSelectedAssetByName(_hasManualAssetSelection ? _selectedAsset?.Name : null);
        SetSelectedAssetInternal(preservedAsset ?? suggestedAsset);

        if (Status == StatusDownloaded && !DownloadedFileMatchesCurrentAsset())
        {
            Status = LastChosenAsset is null
                ? StatusReleaseFoundNoFile
                : StatusReleaseFound;
        }

        if (Status == StatusReleaseFound && LastChosenAsset is null)
            Status = StatusReleaseFoundNoFile;

        if (Status == StatusReleaseFoundNoFile && LastChosenAsset is not null)
            Status = StatusReleaseFound;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedPlaceholders();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusPillText));
        OnPropertyChanged(nameof(LicenseDisplay));
        OnPropertyChanged(nameof(EntryStatusDisplay));
    }

    private void RefreshLocalizedPlaceholders()
    {
        if (_lastRelease is null && Status == StatusNoReleases)
            Changelog = _loc.PlaceholderNoRelease;

        if (_lastRelease is not null && string.IsNullOrWhiteSpace(_lastRelease.Body))
            Changelog = _loc.PlaceholderEmptyReleaseBody;

        if (LastChosenAsset is null)
            AssetName = _loc.PlaceholderNoSelectedAsset;
    }

    private void ReplaceAvailableAssets(IEnumerable<GitHubReleaseAsset> assets)
    {
        AvailableAssets.Clear();

        foreach (var asset in assets)
            AvailableAssets.Add(asset);
    }

    private void SetSelectedAssetInternal(GitHubReleaseAsset? asset)
    {
        _hasManualAssetSelection = false;

        if (!ReferenceEquals(_selectedAsset, asset))
        {
            _selectedAsset = asset;
            OnPropertyChanged(nameof(SelectedAsset));
        }

        ApplySelectedAsset(asset);
    }

    private void ApplySelectedAsset(GitHubReleaseAsset? asset)
    {
        LastChosenAsset = asset;
        AssetName = asset?.Name ?? _loc.PlaceholderNoSelectedAsset;

        if (!DownloadedFileMatchesCurrentAsset())
            DownloadedFile = "";
    }

    private GitHubReleaseAsset? TryFindSelectedAssetByName(string? assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        return AvailableAssets.FirstOrDefault(asset =>
            asset.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
    }

    private GitHubReleaseAsset? ChooseAsset(
        GitHubReleaseInfo release,
        DownloadTargetOption downloadTarget,
        AssetSelectionModeOption assetSelectionMode,
        bool preferPluginDll)
    {
        if (release.Assets.Count == 0)
            return null;

        if (assetSelectionMode == AssetSelectionModeOption.DllOnly)
        {
            var pluginDllAsset = FindPreferredPluginDllAsset(release.Assets);
            if (pluginDllAsset is not null)
                return pluginDllAsset;

            return ChooseArchiveAsset(release.Assets, downloadTarget) ?? release.Assets.FirstOrDefault();
        }

        if (assetSelectionMode == AssetSelectionModeOption.ArchiveOnly)
            return ChooseArchiveAsset(release.Assets, downloadTarget);

        if (preferPluginDll)
        {
            var pluginDllAsset = FindPreferredPluginDllAsset(release.Assets);
            if (pluginDllAsset is not null)
                return pluginDllAsset;
        }

        var archiveAsset = ChooseArchiveAsset(release.Assets, downloadTarget);
        if (archiveAsset is not null)
            return archiveAsset;

        return release.Assets.FirstOrDefault();
    }

    private GitHubReleaseAsset? ChooseArchiveAsset(
        IEnumerable<GitHubReleaseAsset> assets,
        DownloadTargetOption downloadTarget)
    {
        foreach (var preferredName in GetTargetSpecificAssetNames(downloadTarget))
        {
            var preferred = FindPreferredAsset(assets, preferredName);
            if (preferred is not null)
                return preferred;
        }

        var heuristicMatch = FindByTargetHeuristics(assets, downloadTarget);
        if (heuristicMatch is not null)
            return heuristicMatch;

        var genericPreferred = FindPreferredAsset(assets, Catalog.PreferredAssetName);
        if (genericPreferred is not null)
            return genericPreferred;

        return assets.FirstOrDefault(asset => IsArchive(asset.Name));
    }

    private GitHubReleaseAsset? FindPreferredPluginDllAsset(IEnumerable<GitHubReleaseAsset> assets)
    {
        var dllAssets = assets
            .Where(asset => asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Where(asset => !IsCommonDependencyDll(asset.Name))
            .ToList();

        if (dllAssets.Count == 0)
            return null;

        foreach (var candidateName in GetPrimaryDllCandidateNames())
        {
            var normalizedCandidate = Normalize(candidateName);

            var exact = dllAssets.FirstOrDefault(asset =>
                Normalize(Path.GetFileNameWithoutExtension(asset.Name))
                    .Equals(normalizedCandidate, StringComparison.Ordinal));

            if (exact is not null)
                return exact;

            var contains = dllAssets.FirstOrDefault(asset =>
                Normalize(Path.GetFileNameWithoutExtension(asset.Name))
                    .Contains(normalizedCandidate, StringComparison.Ordinal));

            if (contains is not null)
                return contains;
        }

        return dllAssets.Count == 1 ? dllAssets[0] : dllAssets.FirstOrDefault();
    }

    private IEnumerable<string> GetTargetSpecificAssetNames(DownloadTargetOption downloadTarget)
    {
        var targetSpecificName = downloadTarget.Key switch
        {
            "steam_itch" => Catalog.SteamItchAssetName,
            "microsoft_store" => Catalog.MicrosoftStoreAssetName,
            "epic_games" => Catalog.EpicGamesAssetName,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(targetSpecificName))
            yield return targetSpecificName;
    }

    private static GitHubReleaseAsset? FindPreferredAsset(
        IEnumerable<GitHubReleaseAsset> assets,
        string? preferredAssetName)
    {
        if (string.IsNullOrWhiteSpace(preferredAssetName))
            return null;

        var exact = assets.FirstOrDefault(asset =>
            asset.Name.Equals(preferredAssetName, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
            return exact;

        return assets.FirstOrDefault(asset =>
            asset.Name.Contains(preferredAssetName, StringComparison.OrdinalIgnoreCase));
    }

    private static GitHubReleaseAsset? FindByTargetHeuristics(
        IEnumerable<GitHubReleaseAsset> assets,
        DownloadTargetOption downloadTarget)
    {
        if (downloadTarget == DownloadTargetOption.Auto)
            return null;

        var assetList = assets.ToList();

        foreach (var tokenSet in GetTargetTokenSets(downloadTarget))
        {
            var asset = assetList.FirstOrDefault(candidate =>
                IsArchive(candidate.Name) && MatchesAllTokens(candidate.Name, tokenSet));

            if (asset is not null)
                return asset;
        }

        return null;
    }

    private IEnumerable<string> GetPrimaryDllCandidateNames()
    {
        yield return Id;
        yield return Name;
        yield return RepoDisplay;
        yield return Catalog.Repo;
    }

    public IReadOnlyList<string> GetPluginDllCandidateNames()
    {
        return GetPrimaryDllCandidateNames()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool DownloadedFileMatchesCurrentAsset()
    {
        return LastChosenAsset is not null &&
               !string.IsNullOrWhiteSpace(DownloadedFile) &&
               File.Exists(DownloadedFile) &&
               Path.GetFileName(DownloadedFile)
                   .Equals(LastChosenAsset.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string[]> GetTargetTokenSets(DownloadTargetOption downloadTarget)
    {
        return downloadTarget.Key switch
        {
            "steam_itch" => new[]
            {
                new[] { "steam", "itch" },
                new[] { "steamitch" }
            },
            "microsoft_store" => new[]
            {
                new[] { "microsoft", "store" },
                new[] { "microsoftstore" },
                new[] { "xbox", "app" },
                new[] { "xboxapp" }
            },
            "epic_games" => new[]
            {
                new[] { "epic", "games" },
                new[] { "epicgames" }
            },
            _ => Array.Empty<string[]>()
        };
    }

    private static bool IsArchive(string assetName)
    {
        return assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
               assetName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
               assetName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommonDependencyDll(string assetName)
    {
        var fileName = Path.GetFileNameWithoutExtension(assetName);
        var normalized = Normalize(fileName);

        string[] blockedTokens =
        [
            "0harmony",
            "harmony",
            "bepinex",
            "mono",
            "newtonsoft",
            "unhollower",
            "il2cpp",
            "reactive",
            "system",
            "unity",
            "hazel",
            "jna",
            "naudio",
            "skia"
        ];

        return blockedTokens.Any(normalized.Contains);
    }

    private static bool MatchesAllTokens(string assetName, IEnumerable<string> tokens)
    {
        var normalizedAssetName = Normalize(assetName);
        return tokens.All(token => normalizedAssetName.Contains(Normalize(token), StringComparison.Ordinal));
    }

    private static bool IsPlaceholderValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value == PlaceholderValue || value == "-";
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private (IBrush Accent, IBrush Background, IBrush Border) GetStatusPalette()
    {
        return Status switch
        {
            StatusReleaseFound => (InfoAccentBrush, InfoBackgroundBrush, InfoBorderBrush),
            StatusReleaseFoundNoFile => (WarningAccentBrush, WarningBackgroundBrush, WarningBorderBrush),
            StatusNoReleases => (WarningAccentBrush, WarningBackgroundBrush, WarningBorderBrush),
            StatusDownloaded => (InfoAccentBrush, InfoBackgroundBrush, InfoBorderBrush),
            StatusChecking => (InfoAccentBrush, InfoBackgroundBrush, InfoBorderBrush),
            StatusInstalled => (SuccessAccentBrush, SuccessBackgroundBrush, SuccessBorderBrush),
            StatusInstalledUpdate => (WarningAccentBrush, WarningBackgroundBrush, WarningBorderBrush),
            StatusInstalledLocal => (SuccessAccentBrush, SuccessBackgroundBrush, SuccessBorderBrush),
            StatusError => (ErrorAccentBrush, ErrorBackgroundBrush, ErrorBorderBrush),
            _ => (NeutralAccentBrush, NeutralBackgroundBrush, NeutralBorderBrush)
        };
    }

    private static string BuildMonogram(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "NM";

        var parts = value
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length > 0)
            .Take(2)
            .ToArray();

        if (parts.Length == 0)
            return value.Substring(0, Math.Min(2, value.Length)).ToUpperInvariant();

        if (parts.Length == 1)
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();

        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0])));
    }

    private static string? BuildIconUri(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        var normalized = iconPath.Replace('\\', '/');
        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
    }

    private static string BuildSourceUrl(ModCatalogEntry catalog)
    {
        if (!string.IsNullOrWhiteSpace(catalog.SourceUrl))
            return catalog.SourceUrl!;

        return $"https://github.com/{catalog.Owner}/{catalog.Repo}";
    }

    private static string? NormalizeLicense(string? license)
    {
        if (string.IsNullOrWhiteSpace(license))
            return null;

        var normalized = license.Trim();
        if (normalized.Equals("NOASSERTION", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }
}
