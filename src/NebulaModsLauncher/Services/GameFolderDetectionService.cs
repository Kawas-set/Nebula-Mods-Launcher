using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ModLauncher.Models;
using System.Runtime.Versioning;

namespace ModLauncher.Services;

[SupportedOSPlatform("windows")]
public sealed class GameFolderDetectionService
{
    private const string SteamAppId = "945360";
    private static readonly Regex SteamLibraryPathRegex = new("\"path\"\\s*\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<IReadOnlyList<DetectedGameFolderOption>> DetectAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<DetectedGameFolderOption>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var results = new List<DetectedGameFolderOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddSteamInstallations(results, seen);
            AddEpicInstallations(results, seen);
            AddXboxInstallations(results, seen);
            AddItchInstallations(results, seen);

            return results
                .OrderBy(GetPlatformPriority)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    private static void AddSteamInstallations(
        ICollection<DetectedGameFolderOption> results,
        ISet<string> seen)
    {
        foreach (var steamRoot in GetSteamRoots())
        {
            foreach (var libraryPath in GetSteamLibraryPaths(steamRoot))
            {
                var appManifest = Path.Combine(libraryPath, "steamapps", $"appmanifest_{SteamAppId}.acf");
                var gamePath = Path.Combine(libraryPath, "steamapps", "common", "Among Us");

                if (File.Exists(appManifest) || Directory.Exists(gamePath))
                    TryAdd(results, seen, gamePath, DownloadTargetOption.SteamItch.Key, "Steam");
            }
        }
    }

    private static void AddEpicInstallations(
        ICollection<DetectedGameFolderOption> results,
        ISet<string> seen)
    {
        foreach (var manifestPath in GetEpicManifestFiles())
        {
            try
            {
                using var stream = File.OpenRead(manifestPath);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                var displayName = root.TryGetProperty("DisplayName", out var displayNameProperty)
                    ? displayNameProperty.GetString()
                    : null;

                var installLocation = root.TryGetProperty("InstallLocation", out var installLocationProperty)
                    ? installLocationProperty.GetString()
                    : null;

                var appName = root.TryGetProperty("MainGameAppName", out var appNameProperty)
                    ? appNameProperty.GetString()
                    : null;

                var looksLikeAmongUs =
                    ContainsAmongUs(displayName) ||
                    ContainsAmongUs(installLocation) ||
                    ContainsAmongUs(appName);

                if (looksLikeAmongUs)
                    TryAdd(results, seen, installLocation, DownloadTargetOption.EpicGames.Key, "Epic Games");
            }
            catch
            {
                // Ignore malformed manifest files.
            }
        }

        foreach (var drive in DriveInfo.GetDrives().Where(static drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
        {
            TryAdd(
                results,
                seen,
                Path.Combine(drive.RootDirectory.FullName, "Program Files", "Epic Games", "AmongUs"),
                DownloadTargetOption.EpicGames.Key,
                "Epic Games");

            TryAdd(
                results,
                seen,
                Path.Combine(drive.RootDirectory.FullName, "Epic Games", "AmongUs"),
                DownloadTargetOption.EpicGames.Key,
                "Epic Games");
        }
    }

    private static void AddXboxInstallations(
        ICollection<DetectedGameFolderOption> results,
        ISet<string> seen)
    {
        foreach (var drive in DriveInfo.GetDrives().Where(static drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
        {
            var root = drive.RootDirectory.FullName;

            TryAdd(
                results,
                seen,
                Path.Combine(root, "XboxGames", "Among Us", "Content"),
                DownloadTargetOption.MicrosoftStore.Key,
                "Xbox App");

            TryAdd(
                results,
                seen,
                Path.Combine(root, "XboxGames", "Among Us"),
                DownloadTargetOption.MicrosoftStore.Key,
                "Xbox App");

            var modifiableAppsRoot = Path.Combine(root, "Program Files", "ModifiableWindowsApps");
            if (!Directory.Exists(modifiableAppsRoot))
                continue;

            try
            {
                foreach (var directory in Directory.GetDirectories(modifiableAppsRoot, "*Among Us*"))
                {
                    TryAdd(
                        results,
                        seen,
                        directory,
                        DownloadTargetOption.MicrosoftStore.Key,
                        "Microsoft Store");
                }
            }
            catch
            {
                // Ignore access issues.
            }
        }
    }

    private static void AddItchInstallations(
        ICollection<DetectedGameFolderOption> results,
        ISet<string> seen)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            TryAdd(
                results,
                seen,
                Path.Combine(userProfile, "AppData", "Roaming", "itch", "apps", "Among Us"),
                DownloadTargetOption.SteamItch.Key,
                "Itch.io");
        }

        foreach (var drive in DriveInfo.GetDrives().Where(static drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
        {
            TryAdd(
                results,
                seen,
                Path.Combine(drive.RootDirectory.FullName, "itch apps", "Among Us"),
                DownloadTargetOption.SteamItch.Key,
                "Itch.io");
        }
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var registryValue in new[]
                 {
                     ReadRegistryValue(RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                     ReadRegistryValue(RegistryHive.CurrentUser, @"Software\Valve\Steam", "InstallPath"),
                     ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                     ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath")
                 })
        {
            if (!string.IsNullOrWhiteSpace(registryValue))
                roots.Add(registryValue);
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
            roots.Add(Path.Combine(programFilesX86, "Steam"));

        return roots.Where(Directory.Exists);
    }

    private static IEnumerable<string> GetSteamLibraryPaths(string steamRoot)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        results.Add(steamRoot);

        var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
            return results;

        try
        {
            var content = File.ReadAllText(libraryFoldersPath);
            foreach (Match match in SteamLibraryPathRegex.Matches(content))
            {
                var rawPath = match.Groups["path"].Value.Replace(@"\\", @"\");
                if (!string.IsNullOrWhiteSpace(rawPath))
                    results.Add(rawPath);
            }
        }
        catch
        {
            // Ignore broken VDF files.
        }

        return results.Where(Directory.Exists);
    }

    private static IEnumerable<string> GetEpicManifestFiles()
    {
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonAppData))
            return [];

        var manifestsRoot = Path.Combine(commonAppData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifestsRoot))
            return [];

        try
        {
            return Directory.GetFiles(manifestsRoot, "*.item", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return [];
        }
    }

    private static void TryAdd(
        ICollection<DetectedGameFolderOption> results,
        ISet<string> seen,
        string? folderPath,
        string platformKey,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(folderPath.Trim());
        }
        catch
        {
            return;
        }

        if (!Directory.Exists(normalizedPath))
            return;

        if (!ContainsGameExecutable(normalizedPath))
            return;

        if (!seen.Add(normalizedPath))
            return;

        results.Add(new DetectedGameFolderOption
        {
            PlatformKey = platformKey,
            DisplayName = displayName,
            Path = normalizedPath
        });
    }

    private static bool ContainsGameExecutable(string folderPath)
    {
        return File.Exists(Path.Combine(folderPath, "Among Us.exe")) ||
               File.Exists(Path.Combine(folderPath, "AmongUs.exe"));
    }

    private static int GetPlatformPriority(DetectedGameFolderOption option)
    {
        return option.PlatformKey switch
        {
            "steam_itch" => 0,
            "epic_games" => 1,
            "microsoft_store" => 2,
            _ => 3
        };
    }

    private static bool ContainsAmongUs(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains("Among Us", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadRegistryValue(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
                using var key = baseKey.OpenSubKey(subKey);
                return key?.GetValue(valueName) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
