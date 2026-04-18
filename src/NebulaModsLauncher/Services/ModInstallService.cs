using System.IO.Compression;

namespace ModLauncher.Services;

public sealed class ModInstallService
{
    public async Task<string> InstallPluginDllAsync(
        string dllPath,
        string gameFolderPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
            throw new FileNotFoundException("Скачанный DLL-файл не найден.", dllPath);

        if (string.IsNullOrWhiteSpace(gameFolderPath) || !Directory.Exists(gameFolderPath))
            throw new DirectoryNotFoundException("Папка игры не найдена.");

        if (!string.Equals(Path.GetExtension(dllPath), ".dll", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Прямая BepInEx-установка поддерживает только .dll файл.");

        var bepInExFolder = Path.Combine(gameFolderPath, "BepInEx");
        if (!Directory.Exists(bepInExFolder))
            throw new DirectoryNotFoundException("В папке игры не найден BepInEx.");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pluginsFolder = Path.Combine(bepInExFolder, "plugins");
            Directory.CreateDirectory(pluginsFolder);

            var targetPath = Path.Combine(pluginsFolder, Path.GetFileName(dllPath));
            File.Copy(dllPath, targetPath, overwrite: true);
            return targetPath;
        }, cancellationToken);
    }

    public async Task<bool> RemoveInstalledPluginDllAsync(
        string installedPluginPath,
        string gameFolderPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installedPluginPath))
            throw new ArgumentException("Не указан путь к установленной DLL.", nameof(installedPluginPath));

        if (string.IsNullOrWhiteSpace(gameFolderPath) || !Directory.Exists(gameFolderPath))
            throw new DirectoryNotFoundException("Папка игры не найдена.");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pluginsFolder = Path.Combine(gameFolderPath, "BepInEx", "plugins");
            if (!Directory.Exists(pluginsFolder))
                throw new DirectoryNotFoundException("В папке игры не найден BepInEx/plugins.");

            var normalizedPluginsFolder = NormalizePath(pluginsFolder);
            var normalizedInstalledPath = NormalizePath(installedPluginPath);

            if (!IsPathInsideDirectory(normalizedInstalledPath, normalizedPluginsFolder))
                throw new InvalidOperationException("Можно удалять только DLL внутри BepInEx/plugins.");

            if (!File.Exists(normalizedInstalledPath))
                return false;

            File.Delete(normalizedInstalledPath);
            RemoveEmptyParentDirectories(Path.GetDirectoryName(normalizedInstalledPath), normalizedPluginsFolder);
            return true;
        }, cancellationToken);
    }

    public async Task<string> InstallPluginDllFromArchiveAsync(
        string archivePath,
        string gameFolderPath,
        IEnumerable<string> preferredDllNames,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("Скачанный архив не найден.", archivePath);

        if (string.IsNullOrWhiteSpace(gameFolderPath) || !Directory.Exists(gameFolderPath))
            throw new DirectoryNotFoundException("Папка игры не найдена.");

        if (!string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Извлечение DLL поддерживается только для .zip архивов.");

        var bepInExFolder = Path.Combine(gameFolderPath, "BepInEx");
        if (!Directory.Exists(bepInExFolder))
            throw new DirectoryNotFoundException("В папке игры не найден BepInEx.");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var archive = ZipFile.OpenRead(archivePath);

            var dllEntries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Where(entry => entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Where(entry => !IsCommonDependencyDll(entry.Name))
                .ToList();

            if (dllEntries.Count == 0)
                throw new InvalidOperationException("В архиве не найден подходящий DLL-файл мода.");

            var entry = ChoosePreferredDllEntry(dllEntries, preferredDllNames);
            if (entry is null)
                throw new InvalidOperationException("Не удалось определить DLL-файл мода внутри архива.");

            var pluginsFolder = Path.Combine(bepInExFolder, "plugins");
            Directory.CreateDirectory(pluginsFolder);

            var targetPath = Path.Combine(pluginsFolder, entry.Name);

            using var source = entry.Open();
            using var target = File.Create(targetPath);
            source.CopyTo(target);

            return targetPath;
        }, cancellationToken);
    }

    public async Task<ModInstallResult> InstallArchiveAsync(
        string archivePath,
        string gameFolderPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("Скачанный архив не найден.", archivePath);

        if (string.IsNullOrWhiteSpace(gameFolderPath) || !Directory.Exists(gameFolderPath))
            throw new DirectoryNotFoundException("Папка игры не найдена.");

        if (!string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Автоустановка пока поддерживает только .zip архивы.");

        return await Task.Run(() =>
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "ModLauncher", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                ZipFile.ExtractToDirectory(archivePath, tempRoot, overwriteFiles: true);

                var sourceRoot = ResolveInstallRoot(tempRoot);
                var installedFileCount = CopyDirectory(sourceRoot, gameFolderPath, cancellationToken);

                return new ModInstallResult(
                    installedFileCount,
                    !string.Equals(sourceRoot, tempRoot, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }, cancellationToken);
    }

    private static ZipArchiveEntry? ChoosePreferredDllEntry(
        IReadOnlyList<ZipArchiveEntry> dllEntries,
        IEnumerable<string> preferredDllNames)
    {
        var preferredNames = preferredDllNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var candidateName in preferredNames)
        {
            var exact = dllEntries.FirstOrDefault(entry =>
                Normalize(Path.GetFileNameWithoutExtension(entry.Name))
                    .Equals(candidateName, StringComparison.Ordinal));

            if (exact is not null)
                return exact;

            var contains = dllEntries.FirstOrDefault(entry =>
                Normalize(Path.GetFileNameWithoutExtension(entry.Name))
                    .Contains(candidateName, StringComparison.Ordinal));

            if (contains is not null)
                return contains;
        }

        var pluginFolderEntry = dllEntries.FirstOrDefault(entry =>
            Normalize(entry.FullName).Contains("bepinexplugins", StringComparison.Ordinal));

        if (pluginFolderEntry is not null)
            return pluginFolderEntry;

        var rootEntry = dllEntries.FirstOrDefault(entry =>
            entry.FullName.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));

        if (rootEntry is not null)
            return rootEntry;

        return dllEntries.Count == 1
            ? dllEntries[0]
            : dllEntries.OrderBy(entry => entry.FullName.Count(ch => ch == '/' || ch == '\\'))
                .ThenBy(entry => entry.Name.Length)
                .FirstOrDefault();
    }

    private static string ResolveInstallRoot(string extractedRoot)
    {
        var directories = Directory.GetDirectories(extractedRoot);
        var files = Directory.GetFiles(extractedRoot);

        return files.Length == 0 && directories.Length == 1
            ? directories[0]
            : extractedRoot;
    }

    private static int CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(targetDirectory);

        var copiedFiles = 0;

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, targetFilePath, overwrite: true);
            copiedFiles++;
        }

        foreach (var directoryPath in Directory.GetDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            copiedFiles += CopyDirectory(
                directoryPath,
                Path.Combine(targetDirectory, Path.GetFileName(directoryPath)),
                cancellationToken);
        }

        return copiedFiles;
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

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathInsideDirectory(string candidatePath, string rootDirectoryPath)
    {
        return candidatePath.Equals(rootDirectoryPath, StringComparison.OrdinalIgnoreCase) ||
               candidatePath.StartsWith(rootDirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               candidatePath.StartsWith(rootDirectoryPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveEmptyParentDirectories(string? startDirectory, string rootDirectory)
    {
        var currentDirectory = startDirectory;

        while (!string.IsNullOrWhiteSpace(currentDirectory) &&
               !currentDirectory.Equals(rootDirectory, StringComparison.OrdinalIgnoreCase) &&
               Directory.Exists(currentDirectory) &&
               !Directory.EnumerateFileSystemEntries(currentDirectory).Any())
        {
            Directory.Delete(currentDirectory);
            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

public sealed record ModInstallResult(int InstalledFileCount, bool TrimmedArchiveRootDirectory);
