using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Animation;
using System.Diagnostics;
using System.Threading;

namespace OfflineMinecraftLauncher;

internal static class DialogService
{
    public static async Task ShowInfoAsync(Window owner, string title, string message)
    {
        var dialog = CreateDialog(title, message, includeCancel: false, out _, out var okButton);
        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(Window owner, string title, string message)
    {
        var dialog = CreateDialog(title, message, includeCancel: true, out var cancelButton, out var okButton);
        bool result = false;

        okButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        cancelButton!.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
        return result;
    }

    private static Window CreateDialog(string title, string message, bool includeCancel, out Button? cancelButton, out Button okButton)
    {
        var accentColor = Color.Parse("#3ED6B4");
        var secondaryColor = Color.Parse("#3E56D6");

        cancelButton = null;
        okButton = new Button
        {
            Content = includeCancel ? "Continue" : "OK",
            MinWidth = 110,
            Background = new SolidColorBrush(accentColor),
            Foreground = Brushes.Black,
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(8),
            FontWeight = FontWeight.Bold
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12
        };

        if (includeCancel)
        {
            cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 110,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Colors.White, 0.2),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                Padding = new Thickness(16, 8),
                CornerRadius = new CornerRadius(8)
            };
            buttons.Children.Add(cancelButton);
        }

        buttons.Children.Add(okButton);

        return new Window
        {
            Title = title,
            Width = 480,
            Height = 260,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.Full,
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = -1,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#0F111A"), 0),
                    new GradientStop(Color.Parse("#090C12"), 1)
                }
            },
            Content = new Border
            {
                Padding = new Thickness(32),
                Child = new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title.ToUpper(),
                            FontSize = 18,
                            FontWeight = FontWeight.Black,
                            LetterSpacing = 1,
                            Foreground = new SolidColorBrush(accentColor)
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 14,
                            LineHeight = 22,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#CDD5E4"))
                        },
                        new Separator { Background = new SolidColorBrush(Colors.White, 0.05), Margin = new Thickness(0, 8) },
                        buttons
                    }
                }
            }
        };
    }

    public static async Task<string?> ShowTextInputAsync(Window owner, string title, string message, bool isPassword = false)
    {
        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 96,
            Background = new SolidColorBrush(Color.Parse("#3ED6B4")),
            Foreground = Brushes.Black
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 96
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12,
            Children = { cancelButton, okButton }
        };

        var input = new TextBox 
        { 
            Width = 400, 
            HorizontalAlignment = HorizontalAlignment.Left, 
            CornerRadius = new CornerRadius(12),
            PasswordChar = isPassword ? '*' : '\0'
        };

        var dialog = new Window
        {
            Title = title,
            Width = 500,
            Height = 300,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.Full,
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = -1,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#0F111A"), 0),
                    new GradientStop(Color.Parse("#090C12"), 1)
                }
            },
            Content = new Border
            {
                Padding = new Thickness(32),
                Child = new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title.ToUpper(),
                            FontSize = 18,
                            FontWeight = FontWeight.Black,
                            LetterSpacing = 1,
                            Foreground = new SolidColorBrush(Color.Parse("#3ED6B4"))
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 14,
                            LineHeight = 22,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#CDD5E4"))
                        },
                        input,
                        new Separator { Background = new SolidColorBrush(Colors.White, 0.05), Margin = new Thickness(0, 8) },
                        buttons
                    }
                }
            }
        };

        string? result = null;

        okButton.Click += (_, _) =>
        {
            result = input.Text;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
        return result;
    }

    public static Window ShowModelessInfo(Window owner, string title, string message)
    {
        var okButton = new Button
        {
            Content = "Close",
            MinWidth = 96,
            Background = new SolidColorBrush(Color.Parse("#3ED6B4")),
            Foreground = Brushes.Black
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12,
            Children = { okButton }
        };

        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#121623")),
            Content = new Border
            {
                Padding = new Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 18,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brushes.White
                        },
                        new TextBox
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            IsReadOnly = true,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Foreground = new SolidColorBrush(Color.Parse("#CDD5E4"))
                        },
                        buttons
                    }
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();

        dialog.Show(owner);
        return dialog;
    }

    public static async Task<bool> ShowMicrosoftAuthDialogAsync(Window owner, string userCode, string verificationUri, CancellationTokenSource cts)
    {
        var accentColor = Color.Parse("#3ED6B4");
        var secondaryColor = Color.Parse("#3E56D6");
        var cancelColor = Color.Parse("#FF5757");

        var okButton = new Button
        {
            Content = "Cancel Login",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(cancelColor, 0.2),
            BorderBrush = new SolidColorBrush(cancelColor),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(8),
            Transitions = new Transitions
            {
                new BrushTransition { Property = Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(200) }
            }
        };
        okButton.PointerEntered += (_, _) => okButton.Background = new SolidColorBrush(cancelColor);
        okButton.PointerExited += (_, _) => okButton.Background = new SolidColorBrush(cancelColor, 0.2);

        var copyButton = new Button
        {
            Content = "📋 Copy Code",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(secondaryColor),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(8),
            Transitions = new Transitions
            {
                new TransformOperationsTransition { Property = Button.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
            }
        };

        var copyWrapper = new Border
        {
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, Color = Color.FromArgb(40, 0, 0, 0) }),
            CornerRadius = new CornerRadius(8),
            Child = copyButton
        };

        var browserButton = new Button
        {
            Content = "🌐 Open Browser",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(accentColor),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(accentColor),
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(8),
            Transitions = new Transitions
            {
                new BrushTransition { Property = Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(200) }
            }
        };
        browserButton.PointerEntered += (_, _) => browserButton.Background = new SolidColorBrush(accentColor, 0.1);
        browserButton.PointerExited += (_, _) => browserButton.Background = Brushes.Transparent;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16,
            Children = { copyWrapper, browserButton, okButton }
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4,
            Foreground = new SolidColorBrush(accentColor),
            Background = new SolidColorBrush(Colors.White, 0.05),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 300,
            Margin = new Thickness(0, 8)
        };

        var dialog = new Window
        {
            Title = "Microsoft Sign-In",
            Width = 520,
            Height = 360,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.Full,
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = -1,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#0F111A"), 0),
                    new GradientStop(Color.Parse("#090C12"), 1)
                }
            },
            Content = new Border
            {
                Padding = new Thickness(40),
                Child = new StackPanel
                {
                    Spacing = 24,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "Microsoft Authentication",
                                    FontSize = 26,
                                    FontWeight = FontWeight.Bold,
                                    Foreground = Brushes.White,
                                    HorizontalAlignment = HorizontalAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = "SECURE LOGIN GATEWAY",
                                    FontSize = 10,
                                    FontWeight = FontWeight.Light,
                                    LetterSpacing = 2,
                                    Foreground = new SolidColorBrush(accentColor, 0.8),
                                    HorizontalAlignment = HorizontalAlignment.Center
                                }
                            }
                        },
                        new TextBlock
                        {
                            Text = "Please enter the code below on the Microsoft website to continue:",
                            FontSize = 14,
                            LineHeight = 20,
                            Foreground = new SolidColorBrush(Color.Parse("#8E96A3")),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new Border
                        {
                            Background = new SolidColorBrush(Colors.White, 0.03),
                            BorderBrush = new SolidColorBrush(Colors.White, 0.1),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(24, 12),
                            CornerRadius = new CornerRadius(16),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            BoxShadow = new BoxShadows(new BoxShadow { Blur = 30, Color = Color.FromArgb(20, 0, 0, 0), OffsetX = 0, OffsetY = 10 }),
                            Child = new TextBlock
                            {
                                Text = userCode,
                                FontSize = 40,
                                FontWeight = FontWeight.Black,
                                Foreground = new SolidColorBrush(accentColor),
                                LetterSpacing = 6
                            }
                        },
                        new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                progressBar,
                                new TextBlock
                                {
                                    Text = "Waiting for authorization...",
                                    FontSize = 12,
                                    Foreground = new SolidColorBrush(Colors.White, 0.4),
                                    HorizontalAlignment = HorizontalAlignment.Center
                                }
                            }
                        },
                        buttons
                    }
                }
            }
        };

        bool cancelled = false;
        okButton.Click += (_, _) => { cancelled = true; try { cts.Cancel(); } catch { } dialog.Close(); };
        
        copyButton.Click += async (_, _) => 
        {
            var topLevel = TopLevel.GetTopLevel(dialog);
            if (topLevel?.Clipboard != null) 
            {
                await topLevel.Clipboard.SetTextAsync(userCode);
                copyButton.Content = "✅ Copied!";
                await Task.Delay(1000);
                copyButton.Content = "📋 Copy Code";
            }
        };
        
        browserButton.Click += (_, _) => 
        {
            try { Process.Start(new ProcessStartInfo { FileName = verificationUri, UseShellExecute = true }); } catch { }
        };

        dialog.Closed += (_, _) => 
        { 
            if (!cancelled) 
            {
                try { cts.Cancel(); } 
                catch (ObjectDisposedException) { }
            } 
        };

        await dialog.ShowDialog(owner);
        return !cancelled;
    }
}
