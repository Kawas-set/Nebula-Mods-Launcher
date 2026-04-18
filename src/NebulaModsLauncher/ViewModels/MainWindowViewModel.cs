using System.Collections.ObjectModel;
using ModLauncher.Models;
using ModLauncher.Services;

namespace ModLauncher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private static readonly DateTimeOffset CurrentReleaseCutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ModCatalogService _catalogService = new();
    private readonly GitHubReleaseClient _gitHubReleaseClient = new();
    private readonly ModDownloadService _modDownloadService = new();
    private readonly ModInstallService _modInstallService = new();
    private readonly LauncherStateService _launcherStateService = new();
    private readonly AppLocalizer _loc = AppLocalizer.Instance;
    private readonly List<ModItemViewModel> _allMods = [];

    private ModItemViewModel? _selectedMod;
    private string _statusText = "";
    private bool _isBusy;
    private bool _initialized;
    private string _gameFolderPath = "";
    private DownloadTargetOption _selectedDownloadTarget = DownloadTargetOption.Auto;
    private AssetSelectionModeOption _selectedAssetSelectionMode = AssetSelectionModeOption.Auto;
    private LanguageOption _selectedLanguage = LanguageOption.English;
    private LauncherState _launcherState = new();
    private string? _statusMessageKey;
    private object[] _statusMessageArgs = [];
    private bool _statusMessageIsRaw;
    private string _catalogSearchText = "";
    private CancellationTokenSource? _selectedModMetadataCts;

    public MainWindowViewModel()
    {
        _loc.LanguageChanged += OnLanguageChanged;
        SetLocalizedStatus("message.ready");
    }

    public ObservableCollection<ModItemViewModel> Mods { get; } = new();
    public IReadOnlyList<DownloadTargetOption> DownloadTargets { get; } = DownloadTargetOption.All;
    public IReadOnlyList<AssetSelectionModeOption> AssetSelectionModes { get; } = AssetSelectionModeOption.All;
    public IReadOnlyList<LanguageOption> Languages { get; } = LanguageOption.All;
    public AppLocalizer Loc => _loc;

    public ModItemViewModel? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (SetField(ref _selectedMod, value))
            {
                OnPropertyChanged(nameof(CanRemoveSelectedDll));
                OnPropertyChanged(nameof(SelectedModNameDisplay));
                QueueSelectedModMetadataLoad(value);
            }
        }
    }

    public string SelectedModNameDisplay => SelectedMod?.Name ?? Loc.SelectedModPlaceholder;

    public string CatalogSearchText
    {
        get => _catalogSearchText;
        set
        {
            if (SetField(ref _catalogSearchText, value))
                ApplyFilters();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public string GameFolderPath
    {
        get => _gameFolderPath;
        private set
        {
            if (SetField(ref _gameFolderPath, value))
            {
                OnPropertyChanged(nameof(GameFolderPathDisplay));
                OnPropertyChanged(nameof(InstallModeDisplay));
                OnPropertyChanged(nameof(CanRemoveSelectedDll));
            }
        }
    }

    public string GameFolderPathDisplay => string.IsNullOrWhiteSpace(GameFolderPath)
        ? Loc.PlaceholderGameFolder
        : GameFolderPath;

    public DownloadTargetOption SelectedDownloadTarget
    {
        get => _selectedDownloadTarget;
        private set
        {
            if (SetField(ref _selectedDownloadTarget, value))
                OnPropertyChanged(nameof(SelectedDownloadTargetDisplay));
        }
    }

    public AssetSelectionModeOption SelectedAssetSelectionMode
    {
        get => _selectedAssetSelectionMode;
        private set
        {
            if (SetField(ref _selectedAssetSelectionMode, value))
            {
                OnPropertyChanged(nameof(SelectedAssetSelectionModeDisplay));
                OnPropertyChanged(nameof(InstallModeDisplay));
            }
        }
    }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        private set
        {
            if (SetField(ref _selectedLanguage, value))
                OnPropertyChanged(nameof(SelectedLanguageDisplay));
        }
    }

    public string SelectedDownloadTargetDisplay => SelectedDownloadTarget.DisplayName;
    public string SelectedAssetSelectionModeDisplay => SelectedAssetSelectionMode.DisplayName;
    public string SelectedLanguageDisplay => SelectedLanguage.DisplayName;
    public string CatalogSearchLabel => SelectedLanguage.Key == "en" ? "Search catalog" : "Поиск по каталогу";
    public string CatalogSearchWatermark => SelectedLanguage.Key == "en"
        ? "Name, author, or GitHub repository"
        : "Название, автор или GitHub репозиторий";
    public string AutoDetectGameFolderButtonText => SelectedLanguage.Key == "en"
        ? "Auto-detect game folder"
        : "Автопоиск папки игры";

    public string InstallModeDisplay => SelectedAssetSelectionMode switch
    {
        _ when SelectedAssetSelectionMode == AssetSelectionModeOption.DllOnly && !PreferPluginDllDownloads =>
            Loc.Get("install_mode.dll_only_requires_bepinex"),
        _ when SelectedAssetSelectionMode == AssetSelectionModeOption.DllOnly =>
            Loc.Get("install_mode.dll_only"),
        _ when SelectedAssetSelectionMode == AssetSelectionModeOption.ArchiveOnly =>
            Loc.Get("install_mode.archive_only"),
        _ when PreferPluginDllDownloads =>
            Loc.Get("install_mode.auto_bepinex"),
        _ => Loc.Get("install_mode.auto_archive")
    };

    public bool CanRemoveSelectedDll => SelectedMod is not null && HasStoredPluginDll(SelectedMod.Id);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        _initialized = true;

        await LoadStateAsync(cancellationToken);
        await LoadCatalogAsync(cancellationToken);

        if (Mods.Count > 0)
            await RefreshAllAsync(cancellationToken);
    }

    public async Task SetGameFolderPathAsync(string gameFolderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameFolderPath))
            return;

        var folderChanged = !string.IsNullOrWhiteSpace(GameFolderPath) &&
            !string.Equals(
                NormalizePath(GameFolderPath),
                NormalizePath(gameFolderPath),
                StringComparison.OrdinalIgnoreCase);

        GameFolderPath = gameFolderPath;
        _launcherState.GameFolderPath = gameFolderPath;

        if (folderChanged)
        {
            _launcherState.InstalledMods.Clear();

            foreach (var mod in _allMods)
            {
                mod.ClearInstalledState();
                mod.RestoreReleaseStatus();
            }
        }

        foreach (var mod in _allMods)
        {
            mod.RefreshSelectedAsset(
                SelectedDownloadTarget,
                SelectedAssetSelectionMode,
                PreferPluginDllDownloads);

            if (mod.HasInstalledState)
                mod.UpdateStatusForCurrentState();
        }

        try
        {
            await _launcherStateService.SaveAsync(_launcherState, cancellationToken);
            OnPropertyChanged(nameof(CanRemoveSelectedDll));

            SetLocalizedStatus(
                folderChanged ? "message.game_folder_changed" : "message.game_folder_saved",
                gameFolderPath,
                InstallModeDisplay);
        }
        catch (Exception ex)
        {
            SetLocalizedStatus("message.game_folder_save_error", ex.Message);
        }
    }

    public async Task SetDownloadTargetAsync(
        DownloadTargetOption? downloadTarget,
        CancellationToken cancellationToken = default)
    {
        var normalizedTarget = downloadTarget ?? DownloadTargetOption.Auto;
        normalizedTarget = DownloadTargetOption.FromKey(normalizedTarget.Key);

        if (SelectedDownloadTarget == normalizedTarget)
            return;

        SelectedDownloadTarget = normalizedTarget;
        _launcherState.DownloadTargetKey = SelectedDownloadTarget.Key;

        foreach (var mod in _allMods)
        {
            mod.RefreshSelectedAsset(
                SelectedDownloadTarget,
                SelectedAssetSelectionMode,
                PreferPluginDllDownloads);

            if (mod.HasInstalledState)
                mod.UpdateStatusForCurrentState();
        }

        try
        {
            await _launcherStateService.SaveAsync(_launcherState, cancellationToken);
            SetLocalizedStatus("message.download_target_saved", SelectedDownloadTarget);
        }
        catch (Exception ex)
        {
            SetLocalizedStatus("message.download_target_save_error", ex.Message);
        }
    }

    public async Task SetAssetSelectionModeAsync(
        AssetSelectionModeOption? assetSelectionMode,
        CancellationToken cancellationToken = default)
    {
        var normalizedMode = assetSelectionMode ?? AssetSelectionModeOption.Auto;
        normalizedMode = AssetSelectionModeOption.FromKey(normalizedMode.Key);

        if (SelectedAssetSelectionMode == normalizedMode)
            return;

        SelectedAssetSelectionMode = normalizedMode;
        _launcherState.AssetSelectionModeKey = SelectedAssetSelectionMode.Key;

        foreach (var mod in _allMods)
        {
            mod.RefreshSelectedAsset(
                SelectedDownloadTarget,
                SelectedAssetSelectionMode,
                PreferPluginDllDownloads);

            if (mod.HasInstalledState)
                mod.UpdateStatusForCurrentState();
        }

        try
        {
            await _launcherStateService.SaveAsync(_launcherState, cancellationToken);
            SetLocalizedStatus("message.asset_mode_saved", SelectedAssetSelectionMode);
        }
        catch (Exception ex)
        {
            SetLocalizedStatus("message.asset_mode_save_error", ex.Message);
        }
    }

    public async Task SetLanguageAsync(LanguageOption? language, CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = LanguageOption.FromKey(language?.Key);
        if (SelectedLanguage == normalizedLanguage)
            return;

        SelectedLanguage = normalizedLanguage;
        _launcherState.LanguageKey = normalizedLanguage.Key;
        _loc.SetLanguage(normalizedLanguage.Key);

        try
        {
            await _launcherStateService.SaveAsync(_launcherState, cancellationToken);
            SetLocalizedStatus("message.language_saved", normalizedLanguage.DisplayName);
        }
        catch (Exception ex)
        {
            SetLocalizedStatus("message.language_save_error", ex.Message);
        }
    }

    public async Task LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetLocalizedStatus("message.operation_in_progress");
            return;
        }

        IsBusy = true;
        SetLocalizedStatus("message.loading_catalog");

        try
        {
            _selectedModMetadataCts?.Cancel();
            _selectedModMetadataCts?.Dispose();
            _selectedModMetadataCts = null;
            SelectedMod = null;

            _allMods.Clear();
            Mods.Clear();

            var catalog = await _catalogService.LoadAsync(cancellationToken);

            foreach (var item in catalog)
            {
                var mod = new ModItemViewModel(item);
                ApplyStoredInstallInfo(mod);
                _allMods.Add(mod);
            }

            ApplyFilters();
            OnPropertyChanged(nameof(CanRemoveSelectedDll));

            if (_allMods.Count == 0)
                SetLocalizedStatus("message.catalog_empty");
            else
                SetLocalizedStatus("message.catalog_loaded", _allMods.Count);
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindowViewModel.LoadCatalogAsync");
            SetLocalizedStatus("message.catalog_load_error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetLocalizedStatus("message.operation_in_progress");
            return;
        }

        if (Mods.Count == 0)
        {
            SetLocalizedStatus("message.refresh_require_catalog");
            return;
        }

        IsBusy = true;
        SetLocalizedStatus("message.refresh_started");

        try
        {
            var updatedCount = 0;
            var noReleaseCount = 0;
            var errorCount = 0;
            var stoppedByRateLimit = false;
            var modsSnapshot = _allMods.ToArray();

            foreach (var mod in modsSnapshot)
            {
                mod.Status = ModItemViewModel.StatusChecking;

                try
                {
                    var release = await _gitHubReleaseClient.GetLatestReleaseAsync(
                        mod.Catalog.Owner,
                        mod.Catalog.Repo,
                        cancellationToken);

                    if (release is null)
                    {
                        mod.ClearReleaseInfo(ModItemViewModel.StatusNoReleases);
                        ApplyStoredInstallInfo(mod);
                        noReleaseCount++;
                        continue;
                    }

                    mod.ApplyRelease(
                        release,
                        SelectedDownloadTarget,
                        SelectedAssetSelectionMode,
                        PreferPluginDllDownloads);

                    ApplyStoredInstallInfo(mod);
                    updatedCount++;
                }
                catch (GitHubReleaseException ex) when (ex.IsRateLimited)
                {
                    ApplyReleaseError(mod, ex.Message);
                    ApplyStoredInstallInfo(mod);
                    errorCount++;
                    stoppedByRateLimit = true;
                    break;
                }
                catch (Exception ex)
                {
                    ApplyReleaseError(mod, ex.Message);
                    ApplyStoredInstallInfo(mod);
                    errorCount++;
                }
            }

            ApplyFilters();
            OnPropertyChanged(nameof(CanRemoveSelectedDll));

            if (stoppedByRateLimit)
            {
                SetLocalizedStatus("message.refresh_rate_limit", updatedCount, noReleaseCount, errorCount);
            }
            else if (errorCount == 0)
            {
                SetLocalizedStatus("message.refresh_completed", updatedCount, noReleaseCount);
            }
            else
            {
                SetLocalizedStatus("message.refresh_completed_with_errors", updatedCount, noReleaseCount, errorCount);
            }
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindowViewModel.RefreshAllAsync");
            SetLocalizedStatus("message.refresh_failed_unexpected", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DownloadSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetLocalizedStatus("message.operation_in_progress");
            return;
        }

        if (SelectedMod is null)
        {
            SetLocalizedStatus("message.choose_mod");
            return;
        }

        IsBusy = true;
        SetLocalizedStatus("message.download_get_release", SelectedMod.Name);

        try
        {
            var release = await _gitHubReleaseClient.GetLatestReleaseAsync(
                SelectedMod.Catalog.Owner,
                SelectedMod.Catalog.Repo,
                cancellationToken);

            if (release is null)
            {
                SelectedMod.ClearReleaseInfo(ModItemViewModel.StatusNoReleases);
                ApplyStoredInstallInfo(SelectedMod);
                SetLocalizedStatus("message.no_release");
                return;
            }

            SelectedMod.ApplyRelease(
                release,
                SelectedDownloadTarget,
                SelectedAssetSelectionMode,
                PreferPluginDllDownloads);

            ApplyStoredInstallInfo(SelectedMod);

            var asset = SelectedMod.LastChosenAsset;
            if (asset is null)
            {
                SetLocalizedStatus("message.no_matching_download_file");
                return;
            }

            SetLocalizedStatus("message.download_started", asset.Name);

            var filePath = await _modDownloadService.DownloadAssetAsync(
                SelectedMod.Id,
                asset,
                cancellationToken);

            SelectedMod.DownloadedFile = filePath;

            if (SelectedMod.HasInstalledState)
                SelectedMod.UpdateStatusForCurrentState();
            else
                SelectedMod.Status = ModItemViewModel.StatusDownloaded;

            SetLocalizedStatus("message.download_completed", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindowViewModel.DownloadSelectedAsync");
            if (SelectedMod.HasInstalledState)
                SelectedMod.UpdateStatusForCurrentState();
            else
                SelectedMod.Status = ModItemViewModel.StatusError;

            SetLocalizedStatus("message.error_generic", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InstallSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetLocalizedStatus("message.operation_in_progress");
            return;
        }

        if (SelectedMod is null)
        {
            SetLocalizedStatus("message.choose_mod");
            return;
        }

        if (string.IsNullOrWhiteSpace(GameFolderPath))
        {
            SetLocalizedStatus("message.choose_game_folder");
            return;
        }

        if (!Directory.Exists(GameFolderPath))
        {
            SetLocalizedStatus("message.game_folder_missing");
            return;
        }

        if (SelectedAssetSelectionMode == AssetSelectionModeOption.DllOnly && !PreferPluginDllDownloads)
        {
            SetLocalizedStatus("message.dll_requires_bepinex");
            return;
        }

        IsBusy = true;
        SetLocalizedStatus("message.install_prepare", SelectedMod.Name);

        try
        {
            var assetPath = await EnsureDownloadedAssetAsync(SelectedMod, cancellationToken);
            var extension = Path.GetExtension(assetPath);
            var isDllOnly = SelectedAssetSelectionMode == AssetSelectionModeOption.DllOnly;
            var shouldPreferPluginInstall =
                SelectedAssetSelectionMode != AssetSelectionModeOption.ArchiveOnly &&
                PreferPluginDllDownloads;

            SetLocalizedStatus("message.install_running", Path.GetFileName(assetPath));

            if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                var installedDllPath = await _modInstallService.InstallPluginDllAsync(
                    assetPath,
                    GameFolderPath,
                    cancellationToken);

                await PersistInstalledStateAsync(SelectedMod, assetPath, installedDllPath, cancellationToken);
                SetLocalizedStatus("message.dll_installed", Path.GetFileName(installedDllPath));
                return;
            }

            if (shouldPreferPluginInstall && string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var installedDllPath = await _modInstallService.InstallPluginDllFromArchiveAsync(
                        assetPath,
                        GameFolderPath,
                        SelectedMod.GetPluginDllCandidateNames(),
                        cancellationToken);

                    await PersistInstalledStateAsync(SelectedMod, assetPath, installedDllPath, cancellationToken);
                    SetLocalizedStatus("message.dll_from_archive_installed", Path.GetFileName(installedDllPath));
                    return;
                }
                catch (Exception ex) when (!isDllOnly)
                {
                    SetLocalizedStatus("message.dll_extract_fallback", ex.Message);
                }
            }

            if (isDllOnly)
                throw new NotSupportedException(Loc.Get("message.dll_mode_not_supported"));

            var result = await _modInstallService.InstallArchiveAsync(
                assetPath,
                GameFolderPath,
                cancellationToken);

            await PersistInstalledStateAsync(SelectedMod, assetPath, null, cancellationToken);

            var extraNote = result.TrimmedArchiveRootDirectory
                ? Loc.Get("message.install_trimmed_note")
                : "";

            SetLocalizedStatus("message.install_completed", result.InstalledFileCount, extraNote);
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindowViewModel.InstallSelectedAsync");
            if (SelectedMod.HasInstalledState)
                SelectedMod.UpdateStatusForCurrentState();
            else
                SelectedMod.Status = ModItemViewModel.StatusError;

            SetLocalizedStatus("message.install_error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanRemoveSelectedDll));
        }
    }

    public async Task RemoveSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            SetLocalizedStatus("message.operation_in_progress");
            return;
        }

        if (SelectedMod is null)
        {
            SetLocalizedStatus("message.choose_mod");
            return;
        }

        if (string.IsNullOrWhiteSpace(GameFolderPath))
        {
            SetLocalizedStatus("message.choose_game_folder");
            return;
        }

        var installedState = FindInstalledModState(SelectedMod.Id);
        if (installedState is null)
        {
            SetLocalizedStatus("message.no_install_info");
            return;
        }

        var installedPluginPath = ResolveExistingPluginDllPath(SelectedMod, installedState);
        if (string.IsNullOrWhiteSpace(installedPluginPath))
        {
            SetLocalizedStatus("message.remove_only_dll");
            return;
        }

        IsBusy = true;
        SetLocalizedStatus("message.remove_running", Path.GetFileName(installedPluginPath));

        try
        {
            var removed = await _modInstallService.RemoveInstalledPluginDllAsync(
                installedPluginPath,
                GameFolderPath,
                cancellationToken);

            _launcherState.InstalledMods.Remove(installedState);
            SelectedMod.ClearInstalledState();
            SelectedMod.RestoreReleaseStatus();

            await _launcherStateService.SaveAsync(_launcherState, cancellationToken);

            SetLocalizedStatus(
                removed ? "message.remove_completed" : "message.remove_already_missing",
                Path.GetFileName(installedPluginPath));
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindowViewModel.RemoveSelectedAsync");
            SetLocalizedStatus("message.remove_error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanRemoveSelectedDll));
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(GameFolderPathDisplay));
        OnPropertyChanged(nameof(InstallModeDisplay));
        OnPropertyChanged(nameof(SelectedDownloadTargetDisplay));
        OnPropertyChanged(nameof(SelectedAssetSelectionModeDisplay));
        OnPropertyChanged(nameof(SelectedLanguageDisplay));
        OnPropertyChanged(nameof(SelectedModNameDisplay));
        OnPropertyChanged(nameof(CatalogSearchLabel));
        OnPropertyChanged(nameof(CatalogSearchWatermark));
        OnPropertyChanged(nameof(AutoDetectGameFolderButtonText));
        RefreshLocalizedStatus();
    }

    private async Task LoadStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            _launcherState = await _launcherStateService.LoadAsync(cancellationToken);
            SelectedLanguage = LanguageOption.FromKey(_launcherState.LanguageKey);
            _loc.SetLanguage(SelectedLanguage.Key);
            GameFolderPath = _launcherState.GameFolderPath;
            SelectedDownloadTarget = DownloadTargetOption.FromKey(_launcherState.DownloadTargetKey);
            SelectedAssetSelectionMode = AssetSelectionModeOption.FromKey(_launcherState.AssetSelectionModeKey);
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindowViewModel.LoadStateAsync");
            _launcherState = new LauncherState();
            SelectedLanguage = LanguageOption.English;
            _loc.SetLanguage(SelectedLanguage.Key);
            SetLocalizedStatus("message.settings_load_error", ex.Message);
        }
    }

    private void QueueSelectedModMetadataLoad(ModItemViewModel? mod)
    {
        _selectedModMetadataCts?.Cancel();
        _selectedModMetadataCts?.Dispose();
        _selectedModMetadataCts = null;

        if (mod is null || !mod.NeedsRepositoryMetadata)
            return;

        _selectedModMetadataCts = new CancellationTokenSource();
        _ = LoadSelectedModMetadataAsync(mod, _selectedModMetadataCts.Token);
    }

    private async Task LoadSelectedModMetadataAsync(ModItemViewModel mod, CancellationToken cancellationToken)
    {
        try
        {
            var repositoryInfo = await _gitHubReleaseClient.GetRepositoryInfoAsync(
                mod.Catalog.Owner,
                mod.Catalog.Repo,
                cancellationToken);

            var license = repositoryInfo.License?.SpdxId;
            if (string.IsNullOrWhiteSpace(license) ||
                license.Equals("NOASSERTION", StringComparison.OrdinalIgnoreCase))
            {
                license = repositoryInfo.License?.Name;
            }

            mod.ApplyRepositoryMetadata(repositoryInfo.HtmlUrl, license);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, $"MainWindowViewModel.LoadSelectedModMetadataAsync::{mod.Id}");
            mod.ApplyRepositoryMetadata(mod.SourceUrl, mod.Catalog.License);
        }
    }

    private void ApplyStoredInstallInfo(ModItemViewModel mod)
    {
        var installed = FindInstalledModState(mod.Id);
        if (installed is null)
            return;

        var hadStoredPluginPath = !string.IsNullOrWhiteSpace(installed.InstalledPluginPath);
        var resolvedPluginPath = ResolveExistingPluginDllPath(mod, installed);

        if (hadStoredPluginPath && string.IsNullOrWhiteSpace(resolvedPluginPath))
        {
            _launcherState.InstalledMods.Remove(installed);
            mod.ClearInstalledState();
            mod.RestoreReleaseStatus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(resolvedPluginPath))
            installed.InstalledPluginPath = resolvedPluginPath;

        mod.ApplyInstalledState(
            installed.Version,
            installed.InstalledAt,
            installed.InstalledFromFile);
        mod.UpdateStatusForCurrentState();
    }

    private async Task<string> EnsureDownloadedAssetAsync(
        ModItemViewModel mod,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(mod.DownloadedFile) &&
            File.Exists(mod.DownloadedFile) &&
            mod.LastChosenAsset is not null &&
            Path.GetFileName(mod.DownloadedFile)
                .Equals(mod.LastChosenAsset.Name, StringComparison.OrdinalIgnoreCase))
        {
            return mod.DownloadedFile;
        }

        var release = await _gitHubReleaseClient.GetLatestReleaseAsync(
            mod.Catalog.Owner,
            mod.Catalog.Repo,
            cancellationToken);

        if (release is null)
        {
            mod.ClearReleaseInfo(ModItemViewModel.StatusNoReleases);
            ApplyStoredInstallInfo(mod);
            throw new InvalidOperationException(Loc.Get("message.no_release"));
        }

        mod.ApplyRelease(
            release,
            SelectedDownloadTarget,
            SelectedAssetSelectionMode,
            PreferPluginDllDownloads);
        ApplyStoredInstallInfo(mod);

        var asset = mod.LastChosenAsset;
        if (asset is null)
            throw new InvalidOperationException(Loc.Get("message.no_matching_download_file"));

        SetLocalizedStatus("message.download_before_install", asset.Name);

        var filePath = await _modDownloadService.DownloadAssetAsync(
            mod.Id,
            asset,
            cancellationToken);

        mod.DownloadedFile = filePath;
        return filePath;
    }

    private async Task PersistInstalledStateAsync(
        ModItemViewModel mod,
        string sourcePath,
        string? installedPluginPath,
        CancellationToken cancellationToken)
    {
        var installedAt = DateTimeOffset.Now;
        var installedVersion = ResolveInstalledVersion(mod, sourcePath);

        mod.ApplyInstalledState(installedVersion, installedAt, sourcePath);
        mod.UpdateStatusForCurrentState();

        UpsertInstalledModState(mod.Id, installedVersion, installedAt, sourcePath, installedPluginPath);
        await _launcherStateService.SaveAsync(_launcherState, cancellationToken);
    }

    private void UpsertInstalledModState(
        string modId,
        string version,
        DateTimeOffset installedAt,
        string sourcePath,
        string? installedPluginPath)
    {
        _launcherState.GameFolderPath = GameFolderPath;
        _launcherState.LanguageKey = SelectedLanguage.Key;

        var existing = FindInstalledModState(modId);
        if (existing is null)
        {
            existing = new InstalledModState
            {
                ModId = modId
            };

            _launcherState.InstalledMods.Add(existing);
        }

        existing.Version = version;
        existing.InstalledAt = installedAt;
        existing.InstalledFromFile = sourcePath;
        existing.InstalledPluginPath = installedPluginPath ?? "";
    }

    private InstalledModState? FindInstalledModState(string modId)
    {
        return _launcherState.InstalledMods.FirstOrDefault(item =>
            item.ModId.Equals(modId, StringComparison.OrdinalIgnoreCase));
    }

    private string? ResolveExistingPluginDllPath(ModItemViewModel mod, InstalledModState installedState)
    {
        var storedPath = NormalizeExistingFilePath(installedState.InstalledPluginPath);
        if (!string.IsNullOrWhiteSpace(storedPath))
            return storedPath;

        return FindLegacyPluginDllPath(mod, installedState);
    }

    private string? FindLegacyPluginDllPath(ModItemViewModel mod, InstalledModState installedState)
    {
        var pluginsFolder = GetPluginsFolderPath();
        if (pluginsFolder is null)
            return null;

        var candidatePaths = Directory
            .GetFiles(pluginsFolder, "*.dll", SearchOption.AllDirectories)
            .Select(NormalizeExistingFilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();

        if (candidatePaths.Count == 0)
            return null;

        var candidateNames = mod.GetPluginDllCandidateNames()
            .Append(Path.GetFileNameWithoutExtension(installedState.InstalledFromFile))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeToken)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var candidateName in candidateNames)
        {
            var exact = candidatePaths.FirstOrDefault(path =>
                NormalizeToken(Path.GetFileNameWithoutExtension(path))
                    .Equals(candidateName, StringComparison.Ordinal));

            if (exact is not null)
                return exact;

            var contains = candidatePaths.FirstOrDefault(path =>
                NormalizeToken(Path.GetFileNameWithoutExtension(path))
                    .Contains(candidateName, StringComparison.Ordinal));

            if (contains is not null)
                return contains;
        }

        return null;
    }

    private bool HasStoredPluginDll(string modId)
    {
        var installedState = FindInstalledModState(modId);
        return installedState is not null &&
               !string.IsNullOrWhiteSpace(installedState.InstalledPluginPath) &&
               File.Exists(installedState.InstalledPluginPath);
    }

    private string? GetPluginsFolderPath()
    {
        if (string.IsNullOrWhiteSpace(GameFolderPath) || !Directory.Exists(GameFolderPath))
            return null;

        var pluginsFolder = Path.Combine(GameFolderPath, "BepInEx", "plugins");
        return Directory.Exists(pluginsFolder) ? pluginsFolder : null;
    }

    private static string? NormalizeExistingFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            var normalizedPath = Path.GetFullPath(filePath.Trim());
            return File.Exists(normalizedPath) ? normalizedPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveInstalledVersion(ModItemViewModel mod, string sourcePath)
    {
        return !string.IsNullOrWhiteSpace(mod.LatestVersion) && mod.LatestVersion != "—"
            ? mod.LatestVersion
            : Path.GetFileNameWithoutExtension(sourcePath);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string NormalizeToken(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private void ApplyFilters()
    {
        var selectedModId = SelectedMod?.Id;
        var search = CatalogSearchText.Trim();

        var filtered = _allMods
            .Where(mod => MatchesSearch(mod, search))
            .Where(HasCurrentReleaseOrUnknown)
            .ToList();

        Mods.Clear();

        foreach (var mod in filtered)
            Mods.Add(mod);

        SelectedMod = !string.IsNullOrWhiteSpace(selectedModId)
            ? Mods.FirstOrDefault(mod => mod.Id.Equals(selectedModId, StringComparison.OrdinalIgnoreCase))
            : Mods.FirstOrDefault();

        SelectedMod ??= Mods.FirstOrDefault();
    }

    private static bool HasCurrentReleaseOrUnknown(ModItemViewModel mod)
    {
        return mod.ReleasePublishedAtValue is null || mod.ReleasePublishedAtValue >= CurrentReleaseCutoff;
    }

    private static bool MatchesSearch(ModItemViewModel mod, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               mod.Author.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               mod.RepoDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               mod.SourceUrl.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void SetLocalizedStatus(string messageKey, params object[] args)
    {
        _statusMessageKey = messageKey;
        _statusMessageArgs = args;
        _statusMessageIsRaw = false;
        StatusText = Loc.Format(messageKey, args);
    }

    private void RefreshLocalizedStatus()
    {
        if (_statusMessageIsRaw || string.IsNullOrWhiteSpace(_statusMessageKey))
            return;

        StatusText = Loc.Format(_statusMessageKey, _statusMessageArgs);
    }

    private bool PreferPluginDllDownloads => HasBepInExInGameFolder();

    private static void ApplyReleaseError(ModItemViewModel mod, string message)
    {
        mod.Status = ModItemViewModel.StatusError;
        mod.Changelog = message;
        mod.SelectedAsset = null;
        mod.DownloadedFile = "";
        mod.AvailableAssets.Clear();
        mod.AssetName = AppLocalizer.Instance.PlaceholderNoSelectedAsset;
        mod.ReleasePublishedAt = "—";
        mod.LatestVersion = "—";
    }

    private bool HasBepInExInGameFolder()
    {
        if (string.IsNullOrWhiteSpace(GameFolderPath) || !Directory.Exists(GameFolderPath))
            return false;

        return Directory.Exists(Path.Combine(GameFolderPath, "BepInEx"));
    }
}
