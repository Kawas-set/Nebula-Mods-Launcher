using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ModLauncher.Models;
using ModLauncher.Services;
using ModLauncher.ViewModels;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ModLauncher.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly GameFolderDetectionService _gameFolderDetectionService = new();
    private bool _initialized;
    private Vector _savedRootScrollOffset;
    private Vector _savedDetailsScrollOffset;

    public MainWindow()
    {
        InitializeComponent();
        DataContext ??= new MainWindowViewModel();
        Opened += OnOpened;
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
        PropertyChanged += OnWindowPropertyChanged;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        UpdateWindowChrome();

        if (_initialized)
            return;

        _initialized = true;

        if (DataContext is MainWindowViewModel vm)
        {
            try
            {
                await vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                ExceptionLogService.Log(ex, "MainWindow.OnOpened");
                vm.StatusText = vm.Loc.Format("message.error_generic", ex.Message);
            }
        }
    }

    private async void LoadClicked(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync("MainWindow.LoadClicked", vm => vm.LoadCatalogAsync());
    }

    private async void RefreshClicked(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync("MainWindow.RefreshClicked", vm => vm.RefreshAllAsync());
    }

    private async void DownloadTargetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel || sender is not ComboBox comboBox)
            return;

        if (comboBox.SelectedItem is DownloadTargetOption option)
            await RunSafeAsync("MainWindow.DownloadTargetSelectionChanged", vm => vm.SetDownloadTargetAsync(option));
    }

    private async void AssetSelectionModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel || sender is not ComboBox comboBox)
            return;

        if (comboBox.SelectedItem is AssetSelectionModeOption option)
            await RunSafeAsync("MainWindow.AssetSelectionModeChanged", vm => vm.SetAssetSelectionModeAsync(option));
    }

    private async void LanguageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel || sender is not ComboBox comboBox)
            return;

        if (comboBox.SelectedItem is LanguageOption option)
            await RunSafeAsync("MainWindow.LanguageSelectionChanged", vm => vm.SetLanguageAsync(option));
    }

    private async void DownloadClicked(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync("MainWindow.DownloadClicked", vm => vm.DownloadSelectedAsync());
    }

    private void OpenSelectedSourceClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var sourceUrl = vm.SelectedMod?.SourceUrl;
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            vm.StatusText = vm.Loc.Get("message.source_open_missing");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(sourceUrl)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindow.OpenSelectedSourceClicked");
            vm.StatusText = vm.Loc.Format("message.source_open_failed", ex.Message);
        }
    }

    private async void AboutClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindow.AboutClicked");

            if (DataContext is MainWindowViewModel vm)
                vm.StatusText = vm.Loc.Format("message.error_generic", ex.Message);
        }
    }

    private async void InstallClicked(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync("MainWindow.InstallClicked", vm => vm.InstallSelectedAsync());
    }

    private async void RemoveClicked(object? sender, RoutedEventArgs e)
    {
        await RunSafeAsync("MainWindow.RemoveClicked", vm => vm.RemoveSelectedAsync());
    }

    private async void ChooseGameFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        try
        {
            if (!StorageProvider.CanPickFolder)
            {
                vm.StatusText = vm.Loc.Get("message.folder_picker_unsupported");
                return;
            }

            IStorageFolder? suggestedStartLocation = null;

            if (!string.IsNullOrWhiteSpace(vm.GameFolderPath) && Directory.Exists(vm.GameFolderPath))
                suggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(vm.GameFolderPath);

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = vm.Loc.Get("message.folder_picker_title"),
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStartLocation
            });

            var selectedPath = folders.Count > 0
                ? folders[0].TryGetLocalPath()
                : null;

            if (!string.IsNullOrWhiteSpace(selectedPath))
                await vm.SetGameFolderPathAsync(selectedPath);
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindow.ChooseGameFolderClicked");
            vm.StatusText = vm.Loc.Format("message.error_generic", ex.Message);
        }
    }

    private async void AutoDetectGameFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.IsBusy = true;
        vm.StatusText = Localize(
            vm,
            "Ищу установки Among Us на этом ПК...",
            "Looking for Among Us installations on this PC...");

        try
        {
            var detectedFolders = await _gameFolderDetectionService.DetectAsync();

            if (detectedFolders.Count == 0)
            {
                vm.StatusText = Localize(
                    vm,
                    "Не нашел подходящих папок Among Us. Можешь выбрать путь вручную.",
                    "No suitable Among Us folders were found. You can still choose the folder manually.");
                return;
            }

            DetectedGameFolderOption? selectedFolder;

            if (detectedFolders.Count == 1)
            {
                selectedFolder = detectedFolders[0];
            }
            else
            {
                vm.StatusText = Localize(
                    vm,
                    "Найдено несколько версий Among Us. Выбери нужную.",
                    "Multiple Among Us installations were found. Pick the one you want to use.");

                var pickerWindow = new GameFolderChoiceWindow(
                    Localize(vm, "Найдено несколько папок игры", "Multiple game folders found"),
                    Localize(
                        vm,
                        "Лаунчер нашел несколько установок Among Us. Выбери ту, с которой будем работать.",
                        "Nebula Mods Launcher found multiple Among Us installations. Choose the one we should use."),
                    Localize(vm, "Использовать", "Use this folder"),
                    Localize(vm, "Отмена", "Cancel"),
                    detectedFolders);

                selectedFolder = await pickerWindow.ShowDialog<DetectedGameFolderOption?>(this);
                if (selectedFolder is null)
                {
                    vm.StatusText = Localize(vm, "Автопоиск отменен.", "Auto-detect was cancelled.");
                    return;
                }
            }

            await vm.SetDownloadTargetAsync(DownloadTargetOption.FromKey(selectedFolder.PlatformKey));
            await vm.SetGameFolderPathAsync(selectedFolder.Path);
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, "MainWindow.AutoDetectGameFolderClicked");
            vm.StatusText = $"{Localize(vm, "Ошибка", "Error")}: {ex.Message}";
        }
        finally
        {
            vm.IsBusy = false;
        }
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (IsTitleBarButtonInteraction(e.Source))
            return;

        BeginMoveDrag(e);
    }

    private void TitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsTitleBarButtonInteraction(e.Source))
            return;

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void MinimizeWindowClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindowClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
            UpdateWindowChrome();
    }

    private void UpdateWindowChrome()
    {
        var isFullscreenLike = WindowState is WindowState.Maximized or WindowState.FullScreen;

        WindowShell.Margin = isFullscreenLike
            ? new Thickness(0)
            : new Thickness(10);

        WindowShell.CornerRadius = isFullscreenLike
            ? new CornerRadius(0)
            : new CornerRadius(32);

        WindowShell.BorderThickness = isFullscreenLike
            ? new Thickness(0)
            : new Thickness(1);

        WindowContentGrid.Margin = isFullscreenLike
            ? new Thickness(8)
            : new Thickness(18);

        TitleBarPanel.CornerRadius = isFullscreenLike
            ? new CornerRadius(18)
            : new CornerRadius(22);
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RestoreScrollOffsets, DispatcherPriority.Background);
        Dispatcher.UIThread.Post(RestoreScrollOffsets, DispatcherPriority.Input);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        CaptureScrollOffsets();
    }

    private void CaptureScrollOffsets()
    {
        var rootScrollViewer = this.FindControl<ScrollViewer>("RootScrollViewer");
        if (rootScrollViewer is not null)
            _savedRootScrollOffset = rootScrollViewer.Offset;

        var detailsScrollViewer = this.FindControl<ScrollViewer>("DetailsScrollViewer");
        if (detailsScrollViewer is not null)
            _savedDetailsScrollOffset = detailsScrollViewer.Offset;
    }

    private void RestoreScrollOffsets()
    {
        var rootScrollViewer = this.FindControl<ScrollViewer>("RootScrollViewer");
        if (rootScrollViewer is not null)
            rootScrollViewer.Offset = _savedRootScrollOffset;

        var detailsScrollViewer = this.FindControl<ScrollViewer>("DetailsScrollViewer");
        if (detailsScrollViewer is not null)
            detailsScrollViewer.Offset = _savedDetailsScrollOffset;
    }

    private static bool IsTitleBarButtonInteraction(object? source)
    {
        if (source is Button or ComboBox)
            return true;

        return source is Visual visual
            && (visual.FindAncestorOfType<Button>() is not null
                || visual.FindAncestorOfType<ComboBox>() is not null);
    }

    private static string Localize(MainWindowViewModel vm, string russian, string english)
    {
        return vm.SelectedLanguage.Key == "en" ? english : russian;
    }

    private async Task RunSafeAsync(string context, Func<MainWindowViewModel, Task> action)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        try
        {
            await action(vm);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ExceptionLogService.Log(ex, context);
            vm.StatusText = vm.Loc.Format("message.error_generic", ex.Message);
        }
    }
}
