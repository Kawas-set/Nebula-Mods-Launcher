namespace ModLauncher.Services;

public static class LauncherPaths
{
    public static string DataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModLauncher");

    public static string DownloadsRoot => Path.Combine(DataRoot, "Downloads");
    public static string StateFilePath => Path.Combine(DataRoot, "launcher-state.json");
}
