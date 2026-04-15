using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CmlLib.Core;

namespace OfflineMinecraftLauncher;

public sealed class FirstRunAccountWindow : Window
{
    private readonly UserSettingsStore _settingsStore;
    private readonly UserSettings _settings;
    private readonly MinecraftAuthenticationService _authService = new();

    private enum AccountMode
    {
        Offline,
        Microsoft
    }

    private AccountMode _mode = AccountMode.Offline;

    private readonly TextBox _usernameInput;
    private readonly Button _modeButton;
    private readonly Button _submitButton;
    private readonly TextBlock _hintText;

    public FirstRunAccountWindow()
    {
        Title = "Aether Launcher - Welcome";
        Width = 820;
        Height = 540;
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#05070D"), 0),
                new GradientStop(Color.Parse("#0B1120"), 1)
            }
        };
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var initialPath = new MinecraftPath();
        initialPath.CreateDirs();
        _settingsStore = new UserSettingsStore(initialPath.BasePath);
        _settings = _settingsStore.Load();

        _modeButton = new Button
        {
            Content = "Offline Mode",
            Background = new SolidColorBrush(Color.FromArgb(125, 14, 20, 34)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(16, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.FromArgb(85, 110, 91, 255)),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold
        };

        _usernameInput = new TextBox
        {
            Watermark = "Username (offline)",
            Background = new SolidColorBrush(Color.FromArgb(170, 9, 13, 24)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 92, 115, 166)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16, 13)
        };

        _hintText = new TextBlock
        {
            Text = "Offline account: choose any username.",
            Foreground = new SolidColorBrush(Color.Parse("#9CA7BF")),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        _submitButton = new Button
        {
            Content = "Create",
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#5972FF"), 0),
                    new GradientStop(Color.Parse("#59D6FF"), 1)
                }
            },
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 160, 220, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(18, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 240,
            Height = 48,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };

        _modeButton.Click += (_, _) =>
        {
            _mode = _mode == AccountMode.Offline ? AccountMode.Microsoft : AccountMode.Offline;
            SyncModeUi();
        };

        _submitButton.Click += async (_, _) => await SubmitAsync();

        Content = new Grid
        {
            Children =
            {
                new Canvas
                {
                    IsHitTestVisible = false,
                    Children =
                    {
                        new Border
                        {
                            Width = 500,
                            Height = 500,
                            CornerRadius = new CornerRadius(999),
                            Background = new RadialGradientBrush
                            {
                                Center = new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
                                GradientOrigin = new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
                                RadiusX = new RelativeScalar(0.62, RelativeUnit.Relative),
                                RadiusY = new RelativeScalar(0.62, RelativeUnit.Relative),
                                GradientStops =
                                {
                                    new GradientStop(Color.FromArgb(34, 110, 91, 255), 0),
                                    new GradientStop(Color.FromArgb(0, 110, 91, 255), 1)
                                }
                            },
                            [Canvas.LeftProperty] = -120d,
                            [Canvas.TopProperty] = -130d
                        },
                        new Border
                        {
                            Width = 360,
                            Height = 360,
                            CornerRadius = new CornerRadius(999),
                            Background = new RadialGradientBrush
                            {
                                GradientStops =
                                {
                                    new GradientStop(Color.FromArgb(18, 56, 214, 196), 0),
                                    new GradientStop(Color.FromArgb(0, 56, 214, 196), 1)
                                }
                            },
                            [Canvas.RightProperty] = -80d,
                            [Canvas.BottomProperty] = -60d
                        }
                    },
                },
                new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 680,
                    Padding = new Thickness(34),
                    CornerRadius = new CornerRadius(32),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 112, 138, 190)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromArgb(214, 8, 12, 22)),
                    Child = new StackPanel
                    {
                        Spacing = 18,
                        Children =
                        {
                            new Border
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Background = new SolidColorBrush(Color.FromArgb(120, 18, 26, 46)),
                                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 110, 91, 255)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(999),
                                Padding = new Thickness(12, 7),
                                Child = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 10,
                                    Children =
                                    {
                                        new Border
                                        {
                                            Width = 18,
                                            Height = 18,
                                            CornerRadius = new CornerRadius(9),
                                            Background = new RadialGradientBrush
                                            {
                                                GradientStops =
                                                {
                                                    new GradientStop(Color.Parse("#72C8FF"), 0),
                                                    new GradientStop(Color.Parse("#6E5BFF"), 1)
                                                }
                                            }
                                        },
                                        new TextBlock
                                        {
                                            Text = "Aether Launcher",
                                            Foreground = Brushes.White,
                                            FontSize = 12,
                                            FontWeight = FontWeight.SemiBold,
                                            FontFamily = new FontFamily("Inter, Segoe UI")
                                        }
                                    }
                                }
                            },
                            new TextBlock
                            {
                                Text = "Welcome aboard.",
                                FontSize = 34,
                                FontWeight = FontWeight.Bold,
                                Foreground = new SolidColorBrush(Color.Parse("#F1EDE7")),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextAlignment = TextAlignment.Center,
                                FontFamily = new FontFamily("Inter, Segoe UI")
                            },
                            new TextBlock
                            {
                                Text = "Choose your name to begin your adventure.",
                                FontSize = 16,
                                Foreground = new SolidColorBrush(Color.Parse("#C3CCDE")),
                                TextWrapping = TextWrapping.Wrap,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                TextAlignment = TextAlignment.Center,
                                FontFamily = new FontFamily("Inter, Segoe UI")
                            },
                            new Border
                            {
                                Padding = new Thickness(20),
                                CornerRadius = new CornerRadius(24),
                                Background = new SolidColorBrush(Color.FromArgb(110, 14, 18, 31)),
                                BorderBrush = new SolidColorBrush(Color.FromArgb(85, 77, 101, 145)),
                                BorderThickness = new Thickness(1),
                                Child = new StackPanel
                                {
                                    Spacing = 14,
                                    Children =
                                    {
                                        new Border
                                        {
                                            HorizontalAlignment = HorizontalAlignment.Stretch,
                                            Background = new SolidColorBrush(Color.FromArgb(90, 10, 15, 26)),
                                            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 74, 108, 168)),
                                            BorderThickness = new Thickness(1),
                                            CornerRadius = new CornerRadius(999),
                                            Padding = new Thickness(16, 0),
                                            Height = 58,
                                            Child = _usernameInput
                                        },
                                        _modeButton,
                                        _hintText,
                                        _submitButton
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        if (_settings.IsFirstRun && string.IsNullOrWhiteSpace(_settings.SelectedAccountId) && _settings.Accounts.Count == 0)
        {
            _usernameInput.Text = string.IsNullOrWhiteSpace(_settings.Username) ? Environment.UserName : _settings.Username;
        }

        SyncModeUi();
    }

    private void SyncModeUi()
    {
        if (_mode == AccountMode.Offline)
        {
            _modeButton.Content = "Offline Mode  ·  Toggle";
            _usernameInput.IsVisible = true;
            _hintText.Text = "Offline account: choose any username.";
            _submitButton.Content = "Create";
        }
        else
        {
            _modeButton.Content = "Microsoft Mode  ·  Toggle";
            _usernameInput.IsVisible = false;
            _hintText.Text = "Microsoft account: you’ll sign in in your browser (online-mode servers supported).";
            _submitButton.Content = "Continue to browser";
        }
    }

    private async Task SubmitAsync()
    {
        _submitButton.IsEnabled = false;
        _modeButton.IsEnabled = false;
        _usernameInput.IsEnabled = false;

        try
        {
            if (_mode == AccountMode.Offline)
            {
                var username = (_usernameInput.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(username))
                {
                    await DialogService.ShowInfoAsync(this, "Username required", "Enter a username.");
                    return;
                }

                var acc = new LauncherAccount
                {
                    Provider = "offline",
                    Username = username,
                    DisplayName = username
                };

                _settings.Accounts.Add(acc);
                _settings.SelectedAccountId = acc.Id;
                _settings.Username = username;
                _settings.OfflineMode = true;
                _settings.IsFirstRun = false;
                _settingsStore.Save(_settings);

                OpenMainWindowAndClose();
                return;
            }

            var clientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId) ? "00000000402b5328" : _settings.MicrosoftClientId;
            using var cts = new CancellationTokenSource();

            var session = await _authService.BeginDeviceLoginAsync(clientId, cts.Token);
            Process.Start(new ProcessStartInfo { FileName = session.VerificationUri, UseShellExecute = true });

            var dialogTask = DialogService.ShowMicrosoftAuthDialogAsync(this, session.UserCode, session.VerificationUri, cts);
            var pollTask = _authService.CompleteDeviceLoginAsync(clientId, session, cts.Token);

            var completed = await Task.WhenAny(dialogTask, pollTask);
            if (completed != pollTask)
            {
                // User cancelled
                return;
            }

            var account = await pollTask;
            var existing = _settings.Accounts.Find(a => a.Provider == "microsoft" && a.Uuid == account.Uuid);
            if (existing != null) _settings.Accounts.Remove(existing);

            _settings.Accounts.Add(account);
            _settings.SelectedAccountId = account.Id;
            _settings.Username = account.Username;
            _settings.OfflineMode = false;
            _settings.IsFirstRun = false;
            _settingsStore.Save(_settings);

            OpenMainWindowAndClose();
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Account setup failed", ex.Message);
        }
        finally
        {
            _submitButton.IsEnabled = true;
            _modeButton.IsEnabled = true;
            _usernameInput.IsEnabled = true;
        }
    }

    private void OpenMainWindowAndClose()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = new MainWindow();
            desktop.MainWindow = main;
            main.Show();
            Close();
            return;
        }

        // Fallback
        new MainWindow().Show();
        Close();
    }
}

