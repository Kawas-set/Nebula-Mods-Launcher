using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ModLauncher.Services;
using ModLauncher.Views;
using System.Runtime.Versioning;

namespace ModLauncher;

[SupportedOSPlatform("windows")]
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ExceptionLogService.Log(exception, "AppDomain.CurrentDomain.UnhandledException");
            return;
        }

        ExceptionLogService.Log(
            new Exception(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception"),
            "AppDomain.CurrentDomain.UnhandledException");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ExceptionLogService.Log(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }
}
