using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ModLauncher.Models;

namespace ModLauncher.Views;

public sealed class GameFolderChoiceWindow : Window
{
    private readonly ComboBox _comboBox;
    private readonly TextBlock _pathTextBlock;
    private readonly Button _useButton;

    public GameFolderChoiceWindow(
        string title,
        string caption,
        string useText,
        string cancelText,
        IReadOnlyList<DetectedGameFolderOption> options)
    {
        Title = title;
        Width = 760;
        Height = 360;
        MinWidth = 640;
        MinHeight = 320;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#08111A"));

        _comboBox = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = options.Count > 0 ? 0 : -1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.Parse("#0F1A24")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2C4D63")),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.Parse("#F4FBFF")),
            Padding = new Thickness(12, 10),
            MinHeight = 46,
            ItemTemplate = new FuncDataTemplate<DetectedGameFolderOption>((item, _) =>
            {
                return new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = item?.DisplayName ?? "",
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = item?.Path ?? "",
                            Foreground = new SolidColorBrush(Color.Parse("#AFC4D3")),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                };
            })
        };

        _pathTextBlock = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#AFC4D3")),
            TextWrapping = TextWrapping.Wrap
        };

        _useButton = new Button
        {
            Content = useText,
            MinWidth = 140,
            Padding = new Thickness(18, 12),
            Background = new SolidColorBrush(Color.Parse("#5FD8FF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#8DE4FF")),
            Foreground = new SolidColorBrush(Color.Parse("#06131D"))
        };

        var cancelButton = new Button
        {
            Content = cancelText,
            MinWidth = 140,
            Padding = new Thickness(18, 12),
            Background = new SolidColorBrush(Color.Parse("#122231")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D536B")),
            Foreground = new SolidColorBrush(Color.Parse("#F4FBFF"))
        };

        _comboBox.SelectionChanged += OnSelectionChanged;
        _comboBox.DoubleTapped += (_, _) => UseSelectedOption();
        _useButton.Click += (_, _) => UseSelectedOption();
        cancelButton.Click += (_, _) => Close((DetectedGameFolderOption?)null);

        UpdateSelectedPath();

        Content = new Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(24),
            Background = new SolidColorBrush(Color.Parse("#0B141D")),
            BorderBrush = new SolidColorBrush(Color.Parse("#264459")),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 24,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#F4FBFF"))
                    },
                    new TextBlock
                    {
                        Text = caption,
                        Foreground = new SolidColorBrush(Color.Parse("#BDD1DF")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    _comboBox,
                    new Border
                    {
                        Padding = new Thickness(14),
                        CornerRadius = new CornerRadius(18),
                        Background = new SolidColorBrush(Color.Parse("#0F1A24")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#264459")),
                        BorderThickness = new Thickness(1),
                        Child = _pathTextBlock
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            cancelButton,
                            _useButton
                        }
                    }
                }
            }
        };
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedPath();
    }

    private void UpdateSelectedPath()
    {
        if (_comboBox.SelectedItem is DetectedGameFolderOption option)
        {
            _pathTextBlock.Text = option.Path;
            _useButton.IsEnabled = true;
        }
        else
        {
            _pathTextBlock.Text = "";
            _useButton.IsEnabled = false;
        }
    }

    private void UseSelectedOption()
    {
        Close(_comboBox.SelectedItem as DetectedGameFolderOption);
    }
}
