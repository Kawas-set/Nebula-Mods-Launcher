using System.ComponentModel;
using ModLauncher.Models;

namespace ModLauncher.Services;

public sealed class AppLocalizer : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["window_title"] = "Nebula Mods Launcher",
        ["hero_kicker"] = "Among Us Mod Hub",
        ["hero_subtitle"] = "Лаунчер модов с официальными GitHub-источниками, выбором файлов релиза и установкой в папку игры или BepInEx/plugins.",
        ["metric_catalog"] = "Каталог",
        ["metric_platform"] = "Платформа",
        ["metric_mode"] = "Режим",
        ["metric_language"] = "Язык",
        ["metric_selected_mod"] = "Выбранный мод",
        ["metric_status"] = "Статус",
        ["metric_game_folder"] = "Папка игры",
        ["metric_latest_version"] = "Последняя версия",
        ["catalog_title"] = "Каталог модов",
        ["catalog_caption"] = "Выбери мод слева, чтобы посмотреть релиз и установить его.",
        ["catalog_total"] = "Всего",
        ["actions_title"] = "Действия",
        ["button_load_catalog"] = "Загрузить каталог",
        ["button_check_updates"] = "Проверить обновления",
        ["button_download_release"] = "Скачать с официального источника",
        ["button_install"] = "Установить в папку игры",
        ["button_remove_dll"] = "Удалить DLL мода",
        ["button_choose_game_folder"] = "Выбрать папку игры",
        ["button_open_source"] = "Открыть официальный источник",
        ["button_about"] = "About",
        ["session_title"] = "Состояние сессии",
        ["field_download_target"] = "Для какой версии скачивать",
        ["field_asset_mode"] = "Режим файла",
        ["field_language"] = "Язык интерфейса",
        ["field_latest_version"] = "Последняя версия",
        ["field_installed_version"] = "Установленная версия",
        ["field_release_date"] = "Дата релиза",
        ["field_last_install"] = "Последняя установка",
        ["field_game_folder"] = "Папка игры",
        ["field_mod_by"] = "Mod by",
        ["field_source"] = "Source",
        ["field_source_caption"] = "Официальный GitHub repo / release",
        ["field_license"] = "License",
        ["field_entry_status"] = "Status",
        ["files_title"] = "Файлы и установка",
        ["field_release_file"] = "Файл релиза",
        ["field_select_release_file"] = "Выбрать файл релиза",
        ["field_downloaded_file"] = "Скачанный файл",
        ["field_search_asset"] = "Искать asset по имени",
        ["field_search_asset_watermark"] = "Например: mod.zip",
        ["changelog_title"] = "Описание релиза / changelog",
        ["placeholder_game_folder"] = "Папка игры не выбрана.",
        ["placeholder_no_selected_asset"] = "Подходящий файл не найден",
        ["placeholder_no_release"] = "Опубликованный релиз не найден.",
        ["placeholder_empty_release_body"] = "Описание релиза пустое.",
        ["placeholder_unknown_license"] = "Не указана",
        ["selected_mod_placeholder"] = "Мод не выбран",
        ["entry_status_unofficial"] = "Неофициальная запись лаунчера",
        ["about_title"] = "About Nebula Mods Launcher",
        ["about_close"] = "Закрыть",
        ["about_body"] = "This launcher is an unofficial fan-made tool intended to help users access and install Among Us mods from official public repositories or release pages.\n\nThis launcher is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.\n\nThis launcher is not affiliated with, endorsed by, or sponsored by the authors of any third-party mods featured in it, unless explicitly stated otherwise.\n\nThird-party mods remain the property of their respective authors. This launcher does not claim ownership of any third-party content and does not rebrand such mods as its own.\n\nWhen available, the launcher downloads mod files directly from the official public repositories or release pages provided by the original mod authors.\n\nAll mod names, logos, trademarks, and visual identities belong to their respective owners.\n\nThis project does not include, distribute, or replace the base game files of Among Us.",
        ["footer_tagline"] = "Unofficial fan-made launcher for Among Us mods.",
        ["install_mode.dll_only_requires_bepinex"] = "Только DLL: для установки нужен BepInEx в папке игры",
        ["install_mode.dll_only"] = "Только DLL: беру прямой .dll или извлекаю мод из .zip в BepInEx/plugins",
        ["install_mode.archive_only"] = "Только архив: установка из .zip/.7z/.rar",
        ["install_mode.auto_bepinex"] = "Авто: BepInEx найден, сначала ищу .dll, потом пробую извлечь мод из .zip",
        ["install_mode.auto_archive"] = "Авто: обычная установка из архива",
        ["status.unchecked"] = "Не проверялся",
        ["status.checking"] = "Проверка...",
        ["status.release_found"] = "Релиз найден",
        ["status.release_found_no_file"] = "Релиз найден, но без подходящего файла",
        ["status.no_releases"] = "Релизов нет",
        ["status.downloaded"] = "Скачан",
        ["status.installed"] = "Установлен",
        ["status.installed_update"] = "Установлен, есть обновление",
        ["status.installed_local"] = "Установлен локально",
        ["status.error"] = "Ошибка",
        ["status_pill.unchecked"] = "Ожидание",
        ["status_pill.checking"] = "Проверка",
        ["status_pill.release_found"] = "Релиз",
        ["status_pill.release_found_no_file"] = "Нужен файл",
        ["status_pill.no_releases"] = "Нет релиза",
        ["status_pill.downloaded"] = "Скачан",
        ["status_pill.installed"] = "Установлен",
        ["status_pill.installed_update"] = "Обновить",
        ["status_pill.installed_local"] = "Локально",
        ["status_pill.error"] = "Ошибка",
        ["download_target.auto"] = "Авто",
        ["download_target.steam_itch"] = "Steam / Itch",
        ["download_target.microsoft_store"] = "Microsoft Store / Xbox App",
        ["download_target.epic_games"] = "Epic Games",
        ["asset_mode.auto"] = "Авто",
        ["asset_mode.dll_only"] = "Только DLL",
        ["asset_mode.archive_only"] = "Только архив",
        ["message.ready"] = "Готово.",
        ["message.game_folder_changed"] = "Папка игры изменена: {0}. Режим: {1}.",
        ["message.game_folder_saved"] = "Папка игры сохранена: {0}. Режим: {1}.",
        ["message.game_folder_save_error"] = "Не удалось сохранить папку игры: {0}",
        ["message.download_target_saved"] = "Целевая версия загрузки: {0}",
        ["message.download_target_save_error"] = "Не удалось сохранить целевую версию загрузки: {0}",
        ["message.asset_mode_saved"] = "Режим файла: {0}",
        ["message.asset_mode_save_error"] = "Не удалось сохранить режим файла: {0}",
        ["message.language_saved"] = "Язык интерфейса: {0}",
        ["message.language_save_error"] = "Не удалось сохранить язык интерфейса: {0}",
        ["message.loading_catalog"] = "Загружаю каталог модов...",
        ["message.catalog_empty"] = "Каталог пуст. Заполни Data/mods.json.",
        ["message.catalog_loaded"] = "Каталог загружен. Модов: {0}.",
        ["message.catalog_load_error"] = "Ошибка загрузки каталога: {0}",
        ["message.operation_in_progress"] = "Подожди, пока завершится текущая операция.",
        ["message.refresh_require_catalog"] = "Сначала загрузи каталог модов.",
        ["message.refresh_started"] = "Проверяю релизы на GitHub...",
        ["message.refresh_rate_limit"] = "GitHub временно ограничил запросы. Успешно: {0}, без релизов: {1}, ошибок: {2}. Остальные моды пока не проверялись.",
        ["message.refresh_completed"] = "Проверка завершена. Успешно: {0}, без релизов: {1}.",
        ["message.refresh_completed_with_errors"] = "Проверка завершена с ошибками. Успешно: {0}, без релизов: {1}, ошибок: {2}.",
        ["message.refresh_failed_unexpected"] = "Проверка прервалась из-за ошибки: {0}",
        ["message.choose_mod"] = "Выбери мод слева.",
        ["message.download_get_release"] = "Получаю релиз для {0}...",
        ["message.no_release"] = "Опубликованный релиз не найден.",
        ["message.no_matching_download_file"] = "В релизе нет подходящего файла для скачивания.",
        ["message.download_started"] = "Скачиваю {0}...",
        ["message.download_completed"] = "Файл скачан: {0}",
        ["message.error_generic"] = "Ошибка: {0}",
        ["message.choose_game_folder"] = "Сначала выбери папку игры.",
        ["message.game_folder_missing"] = "Выбранная папка игры не существует.",
        ["message.dll_requires_bepinex"] = "Для режима \"Только DLL\" нужен установленный BepInEx в папке игры.",
        ["message.install_prepare"] = "Подготавливаю установку {0}...",
        ["message.install_running"] = "Устанавливаю {0}...",
        ["message.dll_installed"] = "DLL установлена в BepInEx/plugins: {0}",
        ["message.dll_from_archive_installed"] = "DLL извлечена из архива и установлена в BepInEx/plugins: {0}",
        ["message.dll_extract_fallback"] = "Не удалось извлечь DLL из архива, ставлю весь архив: {0}",
        ["message.dll_mode_not_supported"] = "Режим \"Только DLL\" сейчас поддерживает прямые .dll и извлечение мода из .zip.",
        ["message.install_completed"] = "Установка завершена: {0} файлов.{1}",
        ["message.install_trimmed_note"] = " Верхняя папка архива была пропущена автоматически.",
        ["message.install_error"] = "Ошибка установки: {0}",
        ["message.no_install_info"] = "Для этого мода нет сохраненной информации об установке.",
        ["message.remove_only_dll"] = "Удаление доступно только для DLL-модов, установленных в BepInEx/plugins.",
        ["message.remove_running"] = "Удаляю DLL {0}...",
        ["message.remove_completed"] = "DLL удалена: {0}",
        ["message.remove_already_missing"] = "DLL уже отсутствовала, запись об установке очищена: {0}",
        ["message.remove_error"] = "Ошибка удаления: {0}",
        ["message.settings_load_error"] = "Не удалось загрузить настройки: {0}",
        ["message.download_before_install"] = "Скачиваю {0} перед установкой...",
        ["message.folder_picker_unsupported"] = "На этой платформе выбор папки недоступен.",
        ["message.folder_picker_title"] = "Выберите папку игры",
        ["message.source_open_missing"] = "Для этого мода не указан официальный источник.",
        ["message.source_open_failed"] = "Не удалось открыть официальный источник: {0}"
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["window_title"] = "Nebula Mods Launcher",
        ["hero_kicker"] = "Among Us Mod Hub",
        ["hero_subtitle"] = "A mod launcher with official GitHub sources, selectable release files, and install targets for the game folder or BepInEx/plugins.",
        ["metric_catalog"] = "Catalog",
        ["metric_platform"] = "Platform",
        ["metric_mode"] = "Mode",
        ["metric_language"] = "Language",
        ["metric_selected_mod"] = "Selected mod",
        ["metric_status"] = "Status",
        ["metric_game_folder"] = "Game folder",
        ["metric_latest_version"] = "Latest version",
        ["catalog_title"] = "Mod catalog",
        ["catalog_caption"] = "Pick a mod on the left to inspect its release and install it.",
        ["catalog_total"] = "Total",
        ["actions_title"] = "Actions",
        ["button_load_catalog"] = "Load catalog",
        ["button_check_updates"] = "Check updates",
        ["button_download_release"] = "Download from official source",
        ["button_install"] = "Install into game folder",
        ["button_remove_dll"] = "Remove mod DLL",
        ["button_choose_game_folder"] = "Choose game folder",
        ["button_open_source"] = "Open official source",
        ["button_about"] = "About",
        ["session_title"] = "Session state",
        ["field_download_target"] = "Download target",
        ["field_asset_mode"] = "File mode",
        ["field_language"] = "Interface language",
        ["field_latest_version"] = "Latest version",
        ["field_installed_version"] = "Installed version",
        ["field_release_date"] = "Release date",
        ["field_last_install"] = "Last install",
        ["field_game_folder"] = "Game folder",
        ["field_mod_by"] = "Mod by",
        ["field_source"] = "Source",
        ["field_source_caption"] = "Official GitHub repo / release",
        ["field_license"] = "License",
        ["field_entry_status"] = "Status",
        ["files_title"] = "Files and install",
        ["field_release_file"] = "Release file",
        ["field_select_release_file"] = "Choose release file",
        ["field_downloaded_file"] = "Downloaded file",
        ["field_search_asset"] = "Search asset by name",
        ["field_search_asset_watermark"] = "For example: mod.zip",
        ["changelog_title"] = "Release notes / changelog",
        ["placeholder_game_folder"] = "Game folder not selected.",
        ["placeholder_no_selected_asset"] = "No matching file found",
        ["placeholder_no_release"] = "No published release was found.",
        ["placeholder_empty_release_body"] = "Release description is empty.",
        ["placeholder_unknown_license"] = "Unknown",
        ["selected_mod_placeholder"] = "No mod selected",
        ["entry_status_unofficial"] = "Unofficial launcher entry",
        ["about_title"] = "About Nebula Mods Launcher",
        ["about_close"] = "Close",
        ["about_body"] = "This launcher is an unofficial fan-made tool intended to help users access and install Among Us mods from official public repositories or release pages.\n\nThis launcher is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.\n\nThis launcher is not affiliated with, endorsed by, or sponsored by the authors of any third-party mods featured in it, unless explicitly stated otherwise.\n\nThird-party mods remain the property of their respective authors. This launcher does not claim ownership of any third-party content and does not rebrand such mods as its own.\n\nWhen available, the launcher downloads mod files directly from the official public repositories or release pages provided by the original mod authors.\n\nAll mod names, logos, trademarks, and visual identities belong to their respective owners.\n\nThis project does not include, distribute, or replace the base game files of Among Us.",
        ["footer_tagline"] = "Unofficial fan-made launcher for Among Us mods.",
        ["install_mode.dll_only_requires_bepinex"] = "DLL Only: BepInEx must already be installed in the game folder",
        ["install_mode.dll_only"] = "DLL Only: use a direct .dll or extract the mod from .zip into BepInEx/plugins",
        ["install_mode.archive_only"] = "Archive Only: install from .zip/.7z/.rar",
        ["install_mode.auto_bepinex"] = "Auto: BepInEx detected, try a .dll first and then extract from .zip",
        ["install_mode.auto_archive"] = "Auto: standard archive install",
        ["status.unchecked"] = "Not checked",
        ["status.checking"] = "Checking...",
        ["status.release_found"] = "Release found",
        ["status.release_found_no_file"] = "Release found, but no matching file",
        ["status.no_releases"] = "No releases",
        ["status.downloaded"] = "Downloaded",
        ["status.installed"] = "Installed",
        ["status.installed_update"] = "Installed, update available",
        ["status.installed_local"] = "Installed locally",
        ["status.error"] = "Error",
        ["status_pill.unchecked"] = "Idle",
        ["status_pill.checking"] = "Checking",
        ["status_pill.release_found"] = "Release",
        ["status_pill.release_found_no_file"] = "Need file",
        ["status_pill.no_releases"] = "No release",
        ["status_pill.downloaded"] = "Downloaded",
        ["status_pill.installed"] = "Installed",
        ["status_pill.installed_update"] = "Update",
        ["status_pill.installed_local"] = "Local",
        ["status_pill.error"] = "Error",
        ["download_target.auto"] = "Auto",
        ["download_target.steam_itch"] = "Steam / Itch",
        ["download_target.microsoft_store"] = "Microsoft Store / Xbox App",
        ["download_target.epic_games"] = "Epic Games",
        ["asset_mode.auto"] = "Auto",
        ["asset_mode.dll_only"] = "DLL Only",
        ["asset_mode.archive_only"] = "Archive Only",
        ["message.ready"] = "Ready.",
        ["message.game_folder_changed"] = "Game folder changed: {0}. Mode: {1}.",
        ["message.game_folder_saved"] = "Game folder saved: {0}. Mode: {1}.",
        ["message.game_folder_save_error"] = "Couldn't save the game folder: {0}",
        ["message.download_target_saved"] = "Download target: {0}",
        ["message.download_target_save_error"] = "Couldn't save the download target: {0}",
        ["message.asset_mode_saved"] = "File mode: {0}",
        ["message.asset_mode_save_error"] = "Couldn't save the file mode: {0}",
        ["message.language_saved"] = "Interface language: {0}",
        ["message.language_save_error"] = "Couldn't save the interface language: {0}",
        ["message.loading_catalog"] = "Loading mod catalog...",
        ["message.catalog_empty"] = "The catalog is empty. Fill in Data/mods.json.",
        ["message.catalog_loaded"] = "Catalog loaded. Mods: {0}.",
        ["message.catalog_load_error"] = "Catalog loading error: {0}",
        ["message.operation_in_progress"] = "Please wait until the current operation finishes.",
        ["message.refresh_require_catalog"] = "Load the mod catalog first.",
        ["message.refresh_started"] = "Checking GitHub releases...",
        ["message.refresh_rate_limit"] = "GitHub temporarily limited requests. Successful: {0}, no releases: {1}, errors: {2}. The rest of the mods were not checked yet.",
        ["message.refresh_completed"] = "Check complete. Successful: {0}, no releases: {1}.",
        ["message.refresh_completed_with_errors"] = "Check completed with errors. Successful: {0}, no releases: {1}, errors: {2}.",
        ["message.refresh_failed_unexpected"] = "The refresh stopped because of an error: {0}",
        ["message.choose_mod"] = "Choose a mod on the left.",
        ["message.download_get_release"] = "Getting the release for {0}...",
        ["message.no_release"] = "No published release was found.",
        ["message.no_matching_download_file"] = "The release has no matching file to download.",
        ["message.download_started"] = "Downloading {0}...",
        ["message.download_completed"] = "File downloaded: {0}",
        ["message.error_generic"] = "Error: {0}",
        ["message.choose_game_folder"] = "Choose the game folder first.",
        ["message.game_folder_missing"] = "The selected game folder does not exist.",
        ["message.dll_requires_bepinex"] = "DLL Only mode requires BepInEx in the game folder.",
        ["message.install_prepare"] = "Preparing {0} for installation...",
        ["message.install_running"] = "Installing {0}...",
        ["message.dll_installed"] = "DLL installed to BepInEx/plugins: {0}",
        ["message.dll_from_archive_installed"] = "DLL extracted from the archive and installed to BepInEx/plugins: {0}",
        ["message.dll_extract_fallback"] = "Couldn't extract a DLL from the archive, installing the whole archive instead: {0}",
        ["message.dll_mode_not_supported"] = "DLL Only mode currently supports direct .dll files and extracting a mod from .zip.",
        ["message.install_completed"] = "Installation complete: {0} files.{1}",
        ["message.install_trimmed_note"] = " The archive's top-level folder was skipped automatically.",
        ["message.install_error"] = "Installation error: {0}",
        ["message.no_install_info"] = "There is no saved install info for this mod.",
        ["message.remove_only_dll"] = "Removal is available only for DLL mods installed into BepInEx/plugins.",
        ["message.remove_running"] = "Removing DLL {0}...",
        ["message.remove_completed"] = "DLL removed: {0}",
        ["message.remove_already_missing"] = "The DLL was already missing; the install record was cleared: {0}",
        ["message.remove_error"] = "Removal error: {0}",
        ["message.settings_load_error"] = "Couldn't load settings: {0}",
        ["message.download_before_install"] = "Downloading {0} before installation...",
        ["message.folder_picker_unsupported"] = "Folder picking is not available on this platform.",
        ["message.folder_picker_title"] = "Choose the game folder",
        ["message.source_open_missing"] = "No official source is set for this mod.",
        ["message.source_open_failed"] = "Couldn't open the official source: {0}"
    };

    private static readonly string[] LocalizedPropertyNames =
    [
        nameof(WindowTitle),
        nameof(HeroKicker),
        nameof(HeroSubtitle),
        nameof(MetricCatalog),
        nameof(MetricPlatform),
        nameof(MetricMode),
        nameof(MetricLanguage),
        nameof(MetricSelectedMod),
        nameof(MetricStatus),
        nameof(MetricGameFolder),
        nameof(MetricLatestVersion),
        nameof(CatalogTitle),
        nameof(CatalogCaption),
        nameof(CatalogTotal),
        nameof(ActionsTitle),
        nameof(ButtonLoadCatalog),
        nameof(ButtonCheckUpdates),
        nameof(ButtonDownloadRelease),
        nameof(ButtonInstall),
        nameof(ButtonRemoveDll),
        nameof(ButtonChooseGameFolder),
        nameof(ButtonOpenSource),
        nameof(ButtonAbout),
        nameof(SessionTitle),
        nameof(FieldDownloadTarget),
        nameof(FieldAssetMode),
        nameof(FieldLanguage),
        nameof(FieldLatestVersion),
        nameof(FieldInstalledVersion),
        nameof(FieldReleaseDate),
        nameof(FieldLastInstall),
        nameof(FieldGameFolder),
        nameof(FieldModBy),
        nameof(FieldSource),
        nameof(FieldSourceCaption),
        nameof(FieldLicense),
        nameof(FieldEntryStatus),
        nameof(FilesTitle),
        nameof(FieldReleaseFile),
        nameof(FieldSelectReleaseFile),
        nameof(FieldDownloadedFile),
        nameof(FieldSearchAsset),
        nameof(FieldSearchAssetWatermark),
        nameof(ChangelogTitle),
        nameof(PlaceholderGameFolder),
        nameof(PlaceholderNoSelectedAsset),
        nameof(PlaceholderNoRelease),
        nameof(PlaceholderEmptyReleaseBody),
        nameof(PlaceholderUnknownLicense),
        nameof(SelectedModPlaceholder),
        nameof(EntryStatusUnofficial),
        nameof(AboutTitle),
        nameof(AboutClose),
        nameof(AboutBody),
        nameof(FooterTagline)
    ];

    private string _languageKey = "en";

    public static AppLocalizer Instance { get; } = new();

    private AppLocalizer()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string LanguageKey => _languageKey;

    public string WindowTitle => Get("window_title");
    public string HeroKicker => Get("hero_kicker");
    public string HeroSubtitle => Get("hero_subtitle");
    public string MetricCatalog => Get("metric_catalog");
    public string MetricPlatform => Get("metric_platform");
    public string MetricMode => Get("metric_mode");
    public string MetricLanguage => Get("metric_language");
    public string MetricSelectedMod => Get("metric_selected_mod");
    public string MetricStatus => Get("metric_status");
    public string MetricGameFolder => Get("metric_game_folder");
    public string MetricLatestVersion => Get("metric_latest_version");
    public string CatalogTitle => Get("catalog_title");
    public string CatalogCaption => Get("catalog_caption");
    public string CatalogTotal => Get("catalog_total");
    public string ActionsTitle => Get("actions_title");
    public string ButtonLoadCatalog => Get("button_load_catalog");
    public string ButtonCheckUpdates => Get("button_check_updates");
    public string ButtonDownloadRelease => Get("button_download_release");
    public string ButtonInstall => Get("button_install");
    public string ButtonRemoveDll => Get("button_remove_dll");
    public string ButtonChooseGameFolder => Get("button_choose_game_folder");
    public string ButtonOpenSource => Get("button_open_source");
    public string ButtonAbout => Get("button_about");
    public string SessionTitle => Get("session_title");
    public string FieldDownloadTarget => Get("field_download_target");
    public string FieldAssetMode => Get("field_asset_mode");
    public string FieldLanguage => Get("field_language");
    public string FieldLatestVersion => Get("field_latest_version");
    public string FieldInstalledVersion => Get("field_installed_version");
    public string FieldReleaseDate => Get("field_release_date");
    public string FieldLastInstall => Get("field_last_install");
    public string FieldGameFolder => Get("field_game_folder");
    public string FieldModBy => Get("field_mod_by");
    public string FieldSource => Get("field_source");
    public string FieldSourceCaption => Get("field_source_caption");
    public string FieldLicense => Get("field_license");
    public string FieldEntryStatus => Get("field_entry_status");
    public string FilesTitle => Get("files_title");
    public string FieldReleaseFile => Get("field_release_file");
    public string FieldSelectReleaseFile => Get("field_select_release_file");
    public string FieldDownloadedFile => Get("field_downloaded_file");
    public string FieldSearchAsset => Get("field_search_asset");
    public string FieldSearchAssetWatermark => Get("field_search_asset_watermark");
    public string ChangelogTitle => Get("changelog_title");
    public string PlaceholderGameFolder => Get("placeholder_game_folder");
    public string PlaceholderNoSelectedAsset => Get("placeholder_no_selected_asset");
    public string PlaceholderNoRelease => Get("placeholder_no_release");
    public string PlaceholderEmptyReleaseBody => Get("placeholder_empty_release_body");
    public string PlaceholderUnknownLicense => Get("placeholder_unknown_license");
    public string SelectedModPlaceholder => Get("selected_mod_placeholder");
    public string EntryStatusUnofficial => Get("entry_status_unofficial");
    public string AboutTitle => Get("about_title");
    public string AboutClose => Get("about_close");
    public string AboutBody => Get("about_body");
    public string FooterTagline => Get("footer_tagline");

    public void SetLanguage(string? languageKey)
    {
        var normalized = LanguageOption.FromKey(languageKey).Key;
        if (string.Equals(_languageKey, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        _languageKey = normalized;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageKey)));

        foreach (var propertyName in LocalizedPropertyNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        var dictionary = GetDictionary();
        return dictionary.TryGetValue(key, out var value)
            ? value
            : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }

    public string TranslateStatus(string statusKey)
    {
        return Get($"status.{statusKey}");
    }

    public string TranslateStatusPill(string statusKey)
    {
        return Get($"status_pill.{statusKey}");
    }

    public string TranslateDownloadTarget(string targetKey)
    {
        return Get($"download_target.{targetKey}");
    }

    public string TranslateAssetMode(string modeKey)
    {
        return Get($"asset_mode.{modeKey}");
    }

    private IReadOnlyDictionary<string, string> GetDictionary()
    {
        return string.Equals(_languageKey, "en", StringComparison.OrdinalIgnoreCase)
            ? English
            : Russian;
    }
}
