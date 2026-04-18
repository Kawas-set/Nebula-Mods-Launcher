using System.Text;

namespace ModLauncher.Services;

public static class ExceptionLogService
{
    public static void Log(Exception exception, string context)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NebulaModsLauncher",
                "logs");

            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(logDirectory, "launcher-errors.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {context}");
            builder.AppendLine(exception.ToString());
            builder.AppendLine(new string('-', 80));

            File.AppendAllText(logPath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Avoid secondary crashes while logging.
        }
    }
}
