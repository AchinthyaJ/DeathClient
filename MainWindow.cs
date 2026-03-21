using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionMetadata;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media.Transformation;

namespace OfflineMinecraftLauncher;

public sealed class MainWindow : Window
{
    private readonly MinecraftLauncher _defaultLauncher;
    private readonly MinecraftPath _defaultMinecraftPath;
    private readonly LauncherProfileStore _profileStore;
    private readonly UserSettingsStore _settingsStore;
    private readonly ModrinthClient _modrinthClient = new();
    private readonly CurseForgeClient _curseForgeClient = new();
    private readonly ObservableCollection<string> _versionItems = [];
    private readonly ObservableCollection<LauncherProfile> _profileItems = [];
    private readonly ObservableCollection<ModrinthProject> _searchResults = [];
    private static readonly string[] ProjectTypeOptions = ["Mod", "Modpack"];
    private static readonly string[] LoaderOptions = ["Any", "Vanilla", "Fabric", "Quilt", "Forge", "NeoForge"];
    private static readonly string[] ProfileLoaderOptions = ["Vanilla", "Fabric"];
    private static readonly string[] VersionCategoryOptions = ["Versions", "Snapshots", "Other sources"];

    private readonly TextBox usernameInput;
    private readonly ComboBox cbVersion;
    private readonly ComboBox minecraftVersion;
    private readonly Button downloadVersionButton;
    private readonly TextBox profileNameInput;
    private readonly ComboBox profileLoaderCombo;
    private readonly Button createProfileButton;
    private readonly Button renameProfileButton;
    private readonly Button btnStart;
    private readonly Button launchNavButton;
    private readonly Button profilesNavButton;
    private readonly Button modrinthNavButton;
    private readonly Button performanceNavButton;
    private readonly Button settingsNavButton;
    private readonly Button layoutNavButton;
    private readonly TextBlock activeProfileBadge;
    private readonly TextBlock activeContextLabel;
    private readonly TextBlock installModeLabel;
    private readonly Image characterImage;
    private readonly TextBlock statusLabel;
    private readonly TextBlock installDetailsLabel;
    private readonly ProgressBar pbFiles;
    private readonly ProgressBar pbProgress;
    private readonly TextBox modrinthSearchInput;
    private readonly ComboBox modrinthProjectTypeCombo;
    private readonly ComboBox modrinthLoaderCombo;
    private readonly Button modrinthSearchButton;
    private readonly TextBox modrinthVersionInput;
    private readonly ListBox modrinthResultsListBox;
    private readonly TextBlock modrinthDetailsBox;
    private readonly TextBlock modrinthResultsSummary;
    private readonly Button installSelectedButton;
    private readonly Button importMrpackButton;
    private readonly ListBox profileListBox;
    private readonly TextBlock profileInspectorTitle;
    private readonly TextBlock profileInspectorMeta;
    private readonly TextBlock profileInspectorPath;
    private readonly Button clearProfileButton;
    private readonly TextBlock heroInstanceLabel;
    private readonly TextBlock heroPerformanceLabel;
    private readonly TextBlock homeFpsStatValue;
    private readonly TextBlock homeRamStatValue;
    private readonly TextBlock performanceFpsStatValue;
    private readonly TextBlock performanceRamStatValue;
    private readonly TextBlock loadingLabel;
    private readonly Control launchSection;
    private readonly Control modrinthSection;
    private readonly Control profilesSection;
    private readonly Control performanceSection;
    private readonly Control settingsSection;
    private readonly Control layoutSection;

    // Static play button (now a main launch action)
    private readonly Border _playOverlay;
    private readonly TextBlock _playOverlayIcon;
    private readonly TextBlock _playOverlayLabel;

    // Quick Instance panel
    private readonly ComboBox _quickVersionCombo;
    private readonly ComboBox _quickLoaderCombo;
    private readonly Button _quickInstallButton;

    // Quick Mods panel
    private readonly TextBox _quickModSearch;
    private readonly Button _quickModSearchButton;
    private readonly ListBox _quickModResults;
    private readonly ObservableCollection<ModrinthProject> _quickSearchResults = [];

    private string _playerUuid = string.Empty;
    private LauncherProfile? _selectedProfile;
    private CancellationTokenSource? _searchCancellation;
    private UserSettings _settings;
    private string _activeSection = "launch";
    private readonly Border _instanceEditorOverlay;

    public MainWindow()
    {
        _defaultMinecraftPath = new MinecraftPath();
        _defaultMinecraftPath.CreateDirs();
        _profileStore = new LauncherProfileStore(_defaultMinecraftPath.BasePath);
        _settingsStore = new UserSettingsStore(_defaultMinecraftPath.BasePath);
        _settings = _settingsStore.Load();
        _defaultLauncher = CreateLauncher(_defaultMinecraftPath);

        Width = 1260;
        Height = 820;
        MinWidth = 1040;
        MinHeight = 720;
        Title = "Death Client";
        Background = new SolidColorBrush(Color.Parse("#0B0D18"));

        usernameInput = CreateTextBox();
        usernameInput.TextChanged += (_, _) => UsernameInput_TextChanged();

        cbVersion = CreateComboBox(_versionItems);
        cbVersion.SelectionChanged += (_, _) => CbVersion_SelectionChanged();

        minecraftVersion = CreateComboBox(VersionCategoryOptions);
        minecraftVersion.SelectionChanged += async (_, _) => await ListVersionsAsync(GetSelectedVersionCategory());
        downloadVersionButton = CreateSecondaryButton("Download");
        downloadVersionButton.Click += async (_, _) => await DownloadSelectedVersionAsync();

        profileNameInput = CreateTextBox();
        profileNameInput.Watermark = "Profile name";

        profileLoaderCombo = CreateComboBox(ProfileLoaderOptions);
        createProfileButton = CreatePrimaryButton("＋", "#7C5CFF", Colors.White);
        createProfileButton.Click += async (_, _) => await CreateProfileAsync();
        renameProfileButton = CreateSecondaryButton("Rename");
        renameProfileButton.Click += async (_, _) => await RenameSelectedProfileAsync();

        activeProfileBadge = new TextBlock
        {
            Text = "HOME",
            Foreground = new SolidColorBrush(Color.Parse("#DDE4FF")),
            FontWeight = FontWeight.Bold,
            FontSize = 12
        };

        activeContextLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
            FontSize = 14
        };

        installModeLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8590A5")),
            TextWrapping = TextWrapping.Wrap
        };

        characterImage = new Image
        {
            Width = 178,
            Height = 122,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        btnStart = CreatePrimaryButton("▶ Play", "#6E5BFF", Colors.White);
        btnStart.Click += async (_, _) => await LaunchAsync();

        launchNavButton = CreateNavButton("⌂", "Home");
        launchNavButton.Click += (_, _) => SetActiveSection("launch");
        profilesNavButton = CreateNavButton("〓", "Instances");
        profilesNavButton.Click += (_, _) => SetActiveSection("profiles");
        modrinthNavButton = CreateNavButton("□", "Mods");
        modrinthNavButton.Click += (_, _) => SetActiveSection("modrinth");
        performanceNavButton = CreateNavButton("↯", "Performance");
        performanceNavButton.Click += (_, _) => SetActiveSection("performance");
        settingsNavButton = CreateNavButton("⚙", "Settings");
        settingsNavButton.Click += (_, _) => SetActiveSection("settings");
        layoutNavButton = CreateNavButton("🖌", "Layout");
        layoutNavButton.Click += (_, _) => SetActiveSection("layout");

        usernameInput.Watermark = "Enter Username";

        _instanceEditorOverlay = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Child = new Border
            {
                Width = 480,
                Padding = new Thickness(32),
                CornerRadius = new CornerRadius(28),
                Background = new SolidColorBrush(Color.Parse("#121826")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A3852")),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 60, Color = Color.FromArgb(100, 0, 0, 0) }),
                Child = new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                new TextBlock { Text = "Configure Instance", FontSize = 24, FontWeight = FontWeight.Black, Foreground = Brushes.White },
                                new Button 
                                { 
                                    Content = "✕", 
                                    FontSize = 18, 
                                    Foreground = Brushes.Gray, 
                                    Background = Brushes.Transparent, 
                                    BorderThickness = new Thickness(0),
                                    Padding = new Thickness(8),
                                }.With(column: 1).With(btn => btn.Click += (_, _) => _instanceEditorOverlay.IsVisible = false)
                            }
                        },
                        new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Instance Name"), profileNameInput } },
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,*"),
                            ColumnSpacing = 16,
                            Children =
                            {
                                new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Version Type"), minecraftVersion } },
                                new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Game Version"), cbVersion } }.With(column: 1)
                            }
                        },
                        new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Loader Type"), profileLoaderCombo } },
                        new Border { Height = 12 },
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,*"),
                            ColumnSpacing = 12,
                            Children =
                            {
                                createProfileButton,
                                renameProfileButton.With(column: 1)
                            }
                        }
                    }
                }
            }
        };
        _instanceEditorOverlay.PointerPressed += (_, _) => _instanceEditorOverlay.IsVisible = false;
        if (_instanceEditorOverlay.Child != null)
        {
            _instanceEditorOverlay.Child.PointerPressed += (s, e) => e.Handled = true;
        }

        usernameInput.Text = _settings.Username;
        usernameInput.Background = Brushes.Transparent;
        usernameInput.BorderBrush = Brushes.Transparent;
        usernameInput.Foreground = new SolidColorBrush(Color.Parse("#B0BACF"));
        usernameInput.FontSize = 18;
        usernameInput.Padding = new Thickness(0);
        usernameInput.TextChanged += (_, _) => {
            _settings.Username = usernameInput.Text;
            _settingsStore.Save(_settings);
        };

        statusLabel = new TextBlock
        {
            Text = "Ready",
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold
        };

        installDetailsLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8E98AC"))
        };

        pbFiles = CreateProgressBar();
        pbProgress = CreateProgressBar();

        modrinthSearchInput = CreateTextBox();
        modrinthSearchInput.Watermark = "Search for shaders, optimization mods, adventure packs...";

        modrinthProjectTypeCombo = CreateComboBox(ProjectTypeOptions);
        modrinthLoaderCombo = CreateComboBox(LoaderOptions);
        modrinthSearchButton = CreatePrimaryButton("🔍 Search All Platforms", "#6E5BFF", Colors.White);
        modrinthSearchButton.Click += async (_, _) => await SearchModrinthAsync();

        modrinthVersionInput = CreateTextBox();

        modrinthResultsListBox = new ListBox
        {
            Background = new SolidColorBrush(Color.Parse("#0D111C")),
            BorderThickness = new Thickness(0),
            ItemsSource = _searchResults,
        };
        modrinthResultsListBox.ItemTemplate = new FuncDataTemplate<ModrinthProject>((project, _) =>
            new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#12192A"), 0),
                        new GradientStop(Color.Parse("#0D111C"), 1)
                    }
                },
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(8, 6),
                Padding = new Thickness(14, 12),
                BorderBrush = new SolidColorBrush(Color.Parse("#273451")),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("6,*"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        new Border
                        {
                            Background = new LinearGradientBrush
                            {
                                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                                GradientStops =
                                {
                                    new GradientStop(Color.Parse("#FF8B4D"), 0),
                                    new GradientStop(Color.Parse("#6B61FF"), 1)
                                }
                            },
                            CornerRadius = new CornerRadius(999)
                        },
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = project?.Title ?? "Unknown project",
                                    Foreground = Brushes.White,
                                    FontWeight = FontWeight.Bold,
                                    FontSize = 15
                                },
                                new TextBlock
                                {
                                    Text = $"{(project?.ProjectType ?? "mod").ToUpperInvariant()} · {project?.Author ?? "Unknown"}",
                                    Foreground = new SolidColorBrush(Color.Parse("#87A2D8")),
                                    FontSize = 11,
                                    FontWeight = FontWeight.SemiBold
                                },
                                new TextBlock
                                {
                                    Text = $"{(project?.Downloads ?? 0):N0} downloads",
                                    Foreground = new SolidColorBrush(Color.Parse("#8E98AC")),
                                    FontSize = 12
                                }
                            }
                        }.With(column: 1)
                    }
                }
            });
        modrinthResultsListBox.SelectionChanged += (_, _) => UpdateSelectedProjectDetails();

        modrinthDetailsBox = new TextBlock
        {
            Text = "Enter a search query to browse Modrinth and CurseForge simultaneously.",
            Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        modrinthResultsSummary = new TextBlock
        {
            Text = "Search to discover the best mods and modpacks from Modrinth and CurseForge.",
            Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };

        installSelectedButton = CreatePrimaryButton("↓", "#38D6C4", Colors.Black);
        installSelectedButton.IsEnabled = false;
        installSelectedButton.Click += async (_, _) => await InstallSelectedAsync();

        importMrpackButton = CreateSecondaryButton("⤓");
        importMrpackButton.Click += async (_, _) => await ImportMrpackAsync();

        profileListBox = new ListBox
        {
            Background = new SolidColorBrush(Color.Parse("#0D111C")),
            BorderThickness = new Thickness(0),
            ItemsSource = _profileItems
        };
        profileListBox.ItemTemplate = new FuncDataTemplate<LauncherProfile>((profile, _) =>
        {
            var itemPlayBtn = new Button
            {
                Content = "▶",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.Parse("#38D6C4")),
                FontSize = 18,
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };
            itemPlayBtn.Click += (_, _) => {
                profileListBox.SelectedItem = profile;
                SetActiveSection("launch");
            };

            var itemEditBtn = new Button
            {
                Content = "✎",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.Parse("#8E96A8")),
                FontSize = 16,
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center
            };
            itemEditBtn.Click += (_, _) => {
                profileListBox.SelectedItem = profile;
                createProfileButton.IsVisible = false;
                renameProfileButton.IsVisible = true;
                _instanceEditorOverlay.IsVisible = true;
                profileNameInput.Focus();
            };

            var itemDeleteBtn = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.Parse("#FF5B7E")),
                FontSize = 16,
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center
            };
            itemDeleteBtn.Click += async (_, _) => await DeleteSelectedProfileAsync(profile);

            return new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#101827"), 0),
                        new GradientStop(Color.Parse("#0D111C"), 1)
                    }
                },
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(8, 6),
                Padding = new Thickness(14, 12),
                BorderBrush = new SolidColorBrush(Color.Parse("#273451")),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = profile?.Name ?? "Unnamed Profile",
                                    Foreground = Brushes.White,
                                    FontWeight = FontWeight.Bold,
                                    FontSize = 15
                                },
                                new TextBlock
                                {
                                    Text = profile?.LoaderDisplay ?? "Unknown",
                                    Foreground = new SolidColorBrush(Color.Parse("#7FE0C8")),
                                    FontSize = 12,
                                    FontWeight = FontWeight.SemiBold
                                }
                            }
                        }.With(column: 0),
                        itemPlayBtn.With(column: 1),
                        itemEditBtn.With(column: 2),
                        itemDeleteBtn.With(column: 3)
                    }
                }
            };
        });
        profileListBox.SelectionChanged += (_, _) => ProfileListBox_SelectedIndexChanged();
        profileListBox.DoubleTapped += (_, _) => ClearSelectedProfile();

        profileInspectorTitle = new TextBlock
        {
            Text = "Quick Launch",
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            FontSize = 17
        };
        profileInspectorMeta = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#C6D4EC")),
            TextWrapping = TextWrapping.Wrap
        };
        profileInspectorPath = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8EA0BC")),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5
        };
        clearProfileButton = CreateSecondaryButton("Return To Quick Launch");
        clearProfileButton.Click += (_, _) => ClearSelectedProfile();

        heroInstanceLabel = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 32,
            FontWeight = FontWeight.Black
        };
        heroPerformanceLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8E96A8")),
            FontSize = 16
        };
        homeFpsStatValue = CreateStatValue();
        homeRamStatValue = CreateStatValue();
        performanceFpsStatValue = CreateStatValue();
        performanceRamStatValue = CreateStatValue();
        loadingLabel = new TextBlock
        {
            Text = "Syncing launcher data...",
            Foreground = new SolidColorBrush(Color.Parse("#AFC0E8")),
            FontStyle = FontStyle.Italic
        };

        // Quick Instance controls
        _quickVersionCombo = CreateComboBox(_versionItems);
        _quickLoaderCombo = CreateComboBox(ProfileLoaderOptions);
        _quickInstallButton = CreatePrimaryButton("⚡ Install", "#38D6C4", Colors.Black);
        _quickInstallButton.Click += async (_, _) => await QuickInstallInstanceAsync();

        // Quick Mods controls
        _quickModSearch = CreateTextBox();
        _quickModSearch.Watermark = "Quick mod search…";
        _quickModSearchButton = CreatePrimaryButton("⌕ Find", "#6E5BFF", Colors.White);
        _quickModSearchButton.Click += async (_, _) => await QuickModSearchAsync();
        _quickModResults = new ListBox
        {
            Background = new SolidColorBrush(Color.Parse("#0D111C")),
            BorderThickness = new Thickness(0),
            ItemsSource = _quickSearchResults,
            MaxHeight = 200
        };
        _quickModResults.ItemTemplate = new FuncDataTemplate<ModrinthProject>((project, _) =>
        {
            var installBtn = new Button
            {
                Content = "↓",
                Width = 36,
                Height = 36,
                Background = new SolidColorBrush(Color.Parse("#38D6C4")),
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Transparent,
                CornerRadius = new CornerRadius(10),
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Tag = project
            };
            installBtn.Click += async (sender, _) =>
            {
                if (sender is Button btn && btn.Tag is ModrinthProject p)
                    await QuickInstallModAsync(p);
            };

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(70, 15, 22, 39)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(4, 3),
                Padding = new Thickness(10, 8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 2,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = project?.Title ?? "Unknown",
                                    Foreground = Brushes.White,
                                    FontWeight = FontWeight.Bold,
                                    FontSize = 13
                                },
                                new TextBlock
                                {
                                    Text = $"{project?.Author ?? "?"} · {(project?.Downloads ?? 0):N0} ↓",
                                    Foreground = new SolidColorBrush(Color.Parse("#8E98AC")),
                                    FontSize = 11
                                }
                            }
                        },
                        installBtn.With(column: 1)
                    }
                }
            };
        });

        // Animated play overlay (starts as small circle)
        _playOverlayIcon = new TextBlock
        {
            Text = "▶",
            FontSize = 20,
            Foreground = Brushes.White,
            FontWeight = FontWeight.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0) // slight optical adjustment for play icon
        };
        _playOverlayLabel = new TextBlock
        {
            Text = "PLAY",
            FontSize = 1,
            Opacity = 0,
            Foreground = new SolidColorBrush(Color.Parse("#DDE4FF")),
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            LetterSpacing = 4,
            Margin = new Thickness(0, 0, 0, 0)
        };
        _playOverlay = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#6E5BFF"), 0),
                    new GradientStop(Color.Parse("#A855F7"), 0.5),
                    new GradientStop(Color.Parse("#46B8FF"), 1)
                }
            },
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 32,
                OffsetX = 0,
                OffsetY = 12,
                Color = Color.Parse("#6E5BFF")
            }),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 0,
                Orientation = Orientation.Horizontal,
                Children = { _playOverlayIcon, _playOverlayLabel }
            }
        };
        _playOverlay.PointerPressed += async (_, _) => await LaunchAsync();

        launchSection = BuildLaunchDeck();
        modrinthSection = BuildModrinthDeck();
        profilesSection = BuildProfilesDeck();
        performanceSection = BuildPerformanceDeck();
        settingsSection = BuildSettingsDeck();
        layoutSection = BuildLayoutDeck();

        Content = BuildRoot();
        SetActiveSection("launch");
        Opened += async (_, _) => await InitializeAsync();
        Closed += (_, _) =>
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _modrinthClient.Dispose();
        };
    }

    private MinecraftLauncher CreateLauncher(MinecraftPath path)
    {
        path.CreateDirs();
        var launcher = new MinecraftLauncher(path);
        launcher.FileProgressChanged += _launcher_FileProgressChanged;
        launcher.ByteProgressChanged += _launcher_ByteProgressChanged;
        return launcher;
    }

    private Control BuildRoot()
    {
        return new Grid
        {
            Background = GetMainBackground(),
            ColumnDefinitions = _settings.ClientLayout?.Contains("sidebar:right") == true 
                ? new ColumnDefinitions("*,240")
                : new ColumnDefinitions("240,*"),
            Children =
            {
                new Canvas
                {
                    Children =
                    {
                        // Background nebulae effects
                        new Border
                        {
                            Width = 500,
                            Height = 500,
                            CornerRadius = new CornerRadius(999),
                            Background = new RadialGradientBrush
                            {
                                GradientStops =
                                {
                                    new GradientStop(Color.FromArgb(20, Color.Parse(_settings.AccentColor).R, Color.Parse(_settings.AccentColor).G, Color.Parse(_settings.AccentColor).B), 0),
                                    new GradientStop(Color.FromArgb(0, Color.Parse(_settings.AccentColor).R, Color.Parse(_settings.AccentColor).G, Color.Parse(_settings.AccentColor).B), 1)
                                }
                            },
                            [Canvas.LeftProperty] = -120d,
                            [Canvas.TopProperty] = -30d
                        },
                        new Border
                        {
                            Width = 600,
                            Height = 600,
                            CornerRadius = new CornerRadius(999),
                            Background = new RadialGradientBrush
                            {
                                GradientStops =
                                {
                                    new GradientStop(Color.FromArgb(15, Color.Parse(_settings.AccentColor).R, Color.Parse(_settings.AccentColor).G, Color.Parse(_settings.AccentColor).B), 0),
                                    new GradientStop(Color.FromArgb(0, Color.Parse(_settings.AccentColor).R, Color.Parse(_settings.AccentColor).G, Color.Parse(_settings.AccentColor).B), 1)
                                }
                            },
                            [Canvas.RightProperty] = -180d,
                            [Canvas.TopProperty] = 40d
                        }
                    }
                },
                _settings.ClientLayout?.Contains("sidebar:right") == true ? BuildContent().With(column: 0) : BuildHeader(),
                _settings.ClientLayout?.Contains("sidebar:right") == true ? BuildHeader().With(column: 1) : BuildContent().With(column: 1),
                _instanceEditorOverlay.With(columnSpan: 2)
            }
        };
    }

    private Brush GetMainBackground()
    {
        var customBgPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBgPath))
        {
            try {
                return new ImageBrush(new Bitmap(customBgPath)) { Stretch = Stretch.UniformToFill, Opacity = 0.4 };
            } catch { }
        }

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#0E1119"), 0),
                new GradientStop(Color.Parse("#141822"), 1)
            }
        };
    }

    private Control BuildHeader()
    {
        var logoImagePath = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_logo.png");
        if (!File.Exists(logoImagePath))
            logoImagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", "death_client_logo.png");

        Control logo;
        if (File.Exists(logoImagePath))
        {
            var logoImage = new Image
            {
                Source = new Bitmap(logoImagePath),
                Width = 212,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(-12, 0, -12, 24)
            };
            RenderOptions.SetBitmapInterpolationMode(logoImage, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
            logo = logoImage;
        }
        else
        {
            logo = new TextBlock
            {
                Text = "Death Client",
                FontSize = 26,
                FontWeight = FontWeight.Black,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            };
        }

        ApplyHoverMotion(launchNavButton);
        ApplyHoverMotion(profilesNavButton);
        ApplyHoverMotion(modrinthNavButton);
        ApplyHoverMotion(performanceNavButton);
        ApplyHoverMotion(settingsNavButton);
        ApplyHoverMotion(layoutNavButton);

        return CreateGlassPanel(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                DetachFromParent(logo),
                DetachFromParent(launchNavButton),
                DetachFromParent(profilesNavButton),
                DetachFromParent(modrinthNavButton),
                DetachFromParent(performanceNavButton),
                DetachFromParent(settingsNavButton),
                DetachFromParent(layoutNavButton)
            }
        }, padding: new Thickness(12, 32), margin: new Thickness(14));
    }

    private static T DetachFromParent<T>(T control) where T : Control
    {
        if (control.Parent is Panel panel)
            panel.Children.Remove(control);
        else if (control.Parent is ContentControl cc)
            cc.Content = null;
        else if (control.Parent is Decorator d)
            d.Child = null;
        return control;
    }

    private Control BuildContent()
    {
        return new Grid
        {
            Margin = new Thickness(36),
            Children =
            {
                new Border
                {
                    Background = Brushes.Transparent, // Let the root background/nebulae show
                    BorderBrush = new SolidColorBrush(Color.FromArgb(30, 100, 120, 180)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(24),
                    Padding = new Thickness(12),
                    Child = new Grid
                    {
                        Children =
                        {
                            DetachFromParent(launchSection),
                            DetachFromParent(modrinthSection),
                            DetachFromParent(profilesSection),
                            DetachFromParent(performanceSection),
                            DetachFromParent(settingsSection),
                            DetachFromParent(layoutSection)
                        }
                    }
                }
            }
        };
    }

    private Control BuildNavigationRail()
    {
        return BuildCard(new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "Workspace",
                    Foreground = Brushes.White,
                    FontSize = 22,
                    FontWeight = FontWeight.Bold
                },
                new TextBlock
                {
                    Text = "Play, browse, switch.",
                    Foreground = new SolidColorBrush(Color.Parse("#A8B8D4")),
                    TextWrapping = TextWrapping.Wrap
                },
                launchNavButton,
                modrinthNavButton,
                profilesNavButton,
                new Border
                {
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                            new GradientStop(Color.Parse("#101A2A"), 0),
                            new GradientStop(Color.Parse("#0C1320"), 1)
                        }
                    },
                    BorderBrush = new SolidColorBrush(Color.Parse("#23344C")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(20),
                    Padding = new Thickness(16),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Flow",
                                Foreground = new SolidColorBrush(Color.Parse("#7BC9FF")),
                                FontWeight = FontWeight.Bold
                            },
                            new TextBlock
                            {
                                Text = "▶ Play\n⌕ Find mods\n▣ Pick profile",
                                Foreground = new SolidColorBrush(Color.Parse("#C8D5EC")),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            }
        });
    }

    private Control BuildLaunchDeck()
    {
        // 1:1 REPLICA LAYOUT
        var topInfo = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                heroInstanceLabel,
                heroPerformanceLabel,
                new Border { Height = 12 },
                usernameInput,
                new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(40, 255,255,255)), Margin = new Thickness(0, 8, 0, 0) }
            }
        };

        // PLAY Button with correct glow
        _playOverlay.Width = 220;
        _playOverlay.Height = 56;
        _playOverlay.CornerRadius = new CornerRadius(28);
        _playOverlay.Background = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Radius = 0.8,
            GradientStops =
            {
                new GradientStop(Color.Parse("#7E6BFF"), 0),
                new GradientStop(Color.Parse("#4E44C5"), 0.6),
                new GradientStop(Color.Parse("#3A328C"), 1)
            }
        };
        _playOverlay.BoxShadow = new BoxShadows(new BoxShadow
        {
            Blur = 40,
            Color = Color.FromArgb(180, 110, 91, 255)
        });
        _playOverlayIcon.Text = "▶";
        _playOverlayIcon.FontSize = 20;
        _playOverlayLabel.Text = "PLAY";
        _playOverlayLabel.FontSize = 18;
        _playOverlayLabel.Opacity = 1;
        _playOverlayLabel.Margin = new Thickness(10, 0, 0, 0);

        ApplyHoverMotion(_playOverlay);

        var modsBtn = new Button
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 12),
            Width = 200,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children =
                {
                    new TextBlock { Text = "□", FontSize = 18, Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor)) },
                    new TextBlock { Text = "Mods", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0) }.With(column: 1),
                    new TextBlock { Text = "〉", FontSize = 12, Foreground = Brushes.Gray }.With(column: 2)
                }
            }
        };
        modsBtn.Click += (_, _) => SetActiveSection("modrinth");

        var profilesBtn = new Button
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 12),
            Width = 200,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children =
                {
                    new TextBlock { Text = "〓", FontSize = 18, Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor)) },
                    new TextBlock { Text = "Instances", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0) }.With(column: 1),
                    new TextBlock { Text = "〉", FontSize = 12, Foreground = Brushes.Gray }.With(column: 2)
                }
            }
        };
        profilesBtn.Click += (_, _) => SetActiveSection("profiles");

        var actionsGroup = new StackPanel
        {
            Spacing = 8,
            Children = { modsBtn, profilesBtn }
        };

        foreach (var c in actionsGroup.Children) ApplyHoverMotion(c as Control);

        var skinBtn = new Button { Content = "Skin", Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        skinBtn.Click += async (_, _) => await ChangeSkinAsync();
        ApplyHoverMotion(skinBtn);

        var capeBtn = new Button { Content = "Cape", Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        capeBtn.Click += async (_, _) => await ChangeCapeAsync();
        ApplyHoverMotion(capeBtn);

        var avatarPanel = CreateGlassPanel(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Avatar", FontSize = 14, Foreground = Brushes.White, Opacity = 0.8 },
                new Border { Height = 200, Child = characterImage },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 10,
                    Children = { skinBtn, capeBtn.With(column: 1) }
                }
            }
        }, padding: new Thickness(24), margin: new Thickness(0));

        var mainRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 40,
                    Children =
                    {
                        topInfo,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 16,
                            Children = { _playOverlay, actionsGroup }
                        }
                    }
                },
                avatarPanel.With(column: 1)
            }
        };

        var statsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 20,
            Children =
            {
                Create1to1StatCard("FPS ◆", homeFpsStatValue, "10% ◆", "144"),
                Create1to1StatCard("RAM ◆", homeRamStatValue, "Allocated", "4 GB").With(column: 1)
            }
        };

        return new StackPanel
        {
            Spacing = 40,
            Margin = new Thickness(24),
            Children = { mainRow, statsRow }
        };
    }

    private Border Create1to1StatCard(string title, TextBlock valueBlock, string subLabel, string defaultValue)
    {
        valueBlock.Text = defaultValue;
        valueBlock.FontSize = 42;
        valueBlock.FontWeight = FontWeight.Black;
        valueBlock.Foreground = Brushes.White;

        return CreateGlassPanel(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = title, FontSize = 14, Foreground = new SolidColorBrush(Color.Parse("#8E96A8")), FontWeight = FontWeight.Bold },
                valueBlock,
                new TextBlock { Text = subLabel, FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#667899")) }
            }
        }, padding: new Thickness(24), margin: new Thickness(0));
    }

    private Control BuildModrinthDeck()
    {
        // ── Search & Filter Row ───────────────────────────────────────────
        
        modrinthSearchInput.Watermark = "🔍 Search for mods...";
        modrinthSearchInput.CornerRadius = new CornerRadius(16);
        modrinthSearchInput.Background = new SolidColorBrush(Color.Parse("#1A1F2E"));
        modrinthSearchInput.BorderBrush = new SolidColorBrush(Color.Parse("#2A3143"));
        modrinthSearchInput.BorderThickness = new Thickness(1);
        modrinthSearchInput.Height = 42;
        modrinthSearchInput.VerticalContentAlignment = VerticalAlignment.Center;
        
        // Ensure pressing Enter searches
        modrinthSearchInput.KeyDown += async (_, e) => {
            if (e.Key == Avalonia.Input.Key.Enter) await SearchModrinthAsync();
        };

        // Style the dropdowns to fit
        modrinthLoaderCombo.CornerRadius = new CornerRadius(16);
        modrinthLoaderCombo.Height = 42;
        modrinthLoaderCombo.Background = Brushes.Transparent;
        modrinthLoaderCombo.BorderBrush = new SolidColorBrush(Color.Parse("#2A3143"));

        modrinthVersionInput.CornerRadius = new CornerRadius(16);
        modrinthVersionInput.Height = 42;
        modrinthVersionInput.Background = Brushes.Transparent;
        modrinthVersionInput.BorderBrush = new SolidColorBrush(Color.Parse("#2A3143"));
        modrinthVersionInput.MinHeight = 42;
        
        modrinthProjectTypeCombo.CornerRadius = new CornerRadius(16);
        modrinthProjectTypeCombo.Height = 42;
        modrinthProjectTypeCombo.Background = Brushes.Transparent;
        modrinthProjectTypeCombo.BorderBrush = new SolidColorBrush(Color.Parse("#2A3143"));

        modrinthSearchButton.CornerRadius = new CornerRadius(16);
        modrinthSearchButton.Height = 42;
        modrinthSearchButton.Content = new TextBlock { Text = "🔍 Search", FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        modrinthSearchButton.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#6E5BFF"), 0),
                new GradientStop(Color.Parse("#A855F7"), 1)
            }
        };
        modrinthSearchButton.BorderThickness = new Thickness(0);
        modrinthSearchButton.Padding = new Thickness(16, 0);

        var filterRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12, 0, 12, 24) // Match image padding
        };

        filterRow.Children.Add(modrinthSearchInput.With(column: 0));
        
        var loaderText = new TextBlock { Text = "Loader", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var loaderPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { loaderText, modrinthLoaderCombo } };
        filterRow.Children.Add(loaderPanel.With(column: 1));

        var versionText = new TextBlock { Text = "Version", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var versionPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { versionText, modrinthVersionInput } };
        filterRow.Children.Add(versionPanel.With(column: 2));

        filterRow.Children.Add(modrinthProjectTypeCombo.With(column: 3));
        
        filterRow.Children.Add(modrinthSearchButton.With(column: 4));
        
        // ── Card Item Template ────────────────────────────────────────────

        modrinthResultsListBox.Background = Brushes.Transparent;
        modrinthResultsListBox.ItemsPanel = new FuncTemplate<Panel?>(() => new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 });
        modrinthResultsListBox.Margin = new Thickness(4, 0);

        modrinthResultsListBox.ItemTemplate = new FuncDataTemplate<ModrinthProject>((project, _) =>
        {
            var installBtn = new Button
            {
                Content = "Install",
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 8),
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            installBtn.Click += async (s, _) =>
            {
                if (s is Button btn && btn.Tag is ModrinthProject p)
                {
                    modrinthResultsListBox.SelectedItem = p;
                    await InstallSelectedAsync();
                }
            };
            installBtn.Tag = project;

            var dls = project?.Downloads ?? 0;
            var dlText = dls >= 1_000_000 ? $"{dls / 1_000_000.0:0.0}M+" :
                         dls >= 1_000 ? $"{dls / 1_000.0:0.0}K+" :
                         dls.ToString();

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 22, 28, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(8),
                Padding = new Thickness(16),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 16,
                    Children =
                    {
                        // Mock icon if none exists
                        new Border
                        {
                            Width = 52,
                            Height = 52,
                            CornerRadius = new CornerRadius(12),
                            Background = new SolidColorBrush(Color.Parse("#253245")),
                            Child = new TextBlock
                            {
                                Text = (project?.Title ?? "?").Substring(0, 1).ToUpperInvariant(),
                                FontSize = 24,
                                FontWeight = FontWeight.Black,
                                Foreground = Brushes.White,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }.With(column: 0),

                        new StackPanel
                        {
                            Spacing = 4,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = project?.Title ?? "Unknown",
                                    Foreground = Brushes.White,
                                    FontWeight = FontWeight.Bold,
                                    FontSize = 16,
                                    TextTrimming = TextTrimming.CharacterEllipsis // Avoid grid explosion
                                },
                                new TextBlock
                                {
                                    Text = project?.Description ?? "",
                                    Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")),
                                    FontSize = 12,
                                    TextWrapping = TextWrapping.Wrap,
                                    MaxLines = 2,
                                    TextTrimming = TextTrimming.WordEllipsis
                                },
                                new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 6,
                                    Margin = new Thickness(0, 4, 0, 0),
                                    Children =
                                    {
                                        new TextBlock { Text = "◆", Foreground = new SolidColorBrush(Color.Parse("#6E5BFF")), FontSize = 12 },
                                        new TextBlock { Text = dlText, Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), FontSize = 12 },
                                        new TextBlock { Text = "♡", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), FontSize = 12 }
                                    }
                                }
                            }
                        }.With(column: 1),

                        installBtn.With(column: 2)
                    }
                }
            };
        });

        var resultsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = modrinthResultsListBox,
            MaxHeight = 650 // Fit well into window
        };

        var mainContent = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                filterRow,
                resultsScroll
            }
        };
        
        return CreateSectionScroller(mainContent);
    }

    private Control BuildProfilesDeck()
    {
        var instancesHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(8, 0, 8, 20),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Instances (Left)
        instancesHeader.Children.Add(new TextBlock 
        { 
            Text = "Instances", 
            FontSize = 32, 
            FontWeight = FontWeight.Black, 
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        }.With(column: 0));

        // + (Right)
        var addBtn = CreatePrimaryButton("+", "#38D6C4", Colors.Black);
        addBtn.Width = 44;
        addBtn.Height = 44;
        addBtn.CornerRadius = new CornerRadius(22);
        addBtn.Padding = new Thickness(0);
        addBtn.Content = new TextBlock 
        { 
            Text = "+", 
            FontSize = 28, 
            HorizontalAlignment = HorizontalAlignment.Center, 
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0) // Visual centering adjustment
        };
        addBtn.VerticalAlignment = VerticalAlignment.Center;
        addBtn.Click += (_, _) => {
            ClearSelectedProfile();
            createProfileButton.IsVisible = true;
            renameProfileButton.IsVisible = false;
            _instanceEditorOverlay.IsVisible = true;
        };
        instancesHeader.Children.Add(addBtn.With(column: 2));

        return CreateSectionScroller(new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(4, 4, 4, 80),
            Children =
            {
                instancesHeader,
                CreateGlassPanel(new Border
                {
                    Height = 600,
                    Child = WrapScrollable(profileListBox)
                })
            }
        });
    }

    private Control BuildPerformanceDeck()
    {
        var perfFilesPb = CreateProgressBar();
        var perfNetworkPb = CreateProgressBar();

        return CreateSectionScroller(new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(4, 4, 4, 80),
            Children =
            {
                CreateSectionTitle("Performance", "Track runtime posture and diagnostics."),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 18,
                    Children =
                    {
                        CreateStatTile("FPS Target", performanceFpsStatValue, "Dynamic based on instance").With(column: 0),
                        CreateStatTile("RAM Allocated", performanceRamStatValue, "Current launcher estimate").With(column: 1)
                    }
                },
                CreateGlassPanel(new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        CreatePanelEyebrow("Launch Progress"),
                        CreateProgressRow("Files", perfFilesPb),
                        CreateProgressRow("Network", perfNetworkPb)
                    }
                })
            }
        });
    }

    private Control BuildSettingsDeck()
    {
        var offlineModeToggle = new ToggleSwitch
        {
            Content = "Offline Mode (No Internet)",
            IsChecked = _settings.OfflineMode,
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold
        };
        offlineModeToggle.IsCheckedChanged += (_, _) =>
        {
            _settings.OfflineMode = offlineModeToggle.IsChecked ?? false;
            _settingsStore.Save(_settings);
        };

        return CreateSectionScroller(new Grid
        {
            Margin = new Thickness(4),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 18,
            Children =
            {
                CreateSectionTitle("Settings", "Launcher preferences."),
                CreateGlassPanel(new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        offlineModeToggle,
                        new TextBlock
                        {
                            Text = "Version management is in Instances.",
                            Foreground = new SolidColorBrush(Color.Parse("#B2C0E6")),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }).With(row: 1)
            }
        });
    }

    private async Task InitializeAsync()
    {
        loadingLabel.Text = string.Empty;
        usernameInput.Text = _settings.Username;
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
            usernameInput.Text = Environment.UserName;

        profileLoaderCombo.SelectedIndex = 0;
        _quickLoaderCombo.SelectedIndex = 0;
        modrinthProjectTypeCombo.SelectedIndex = 0;
        modrinthLoaderCombo.SelectedIndex = 0;
        minecraftVersion.SelectedIndex = 0;

        RefreshProfiles();
        await ListVersionsAsync(GetSelectedVersionCategory());

        if (!string.IsNullOrWhiteSpace(_settings.Version))
        {
            cbVersion.SelectedItem = _settings.Version;
            _quickVersionCombo.SelectedItem = _settings.Version;
        }

        SyncModrinthFilters();
        UpdateCharacterPreview();
        UpdateLauncherContext();
        SetProgressState("Ready", 0, 0);
    }

    private void SetActiveSection(string section)
    {
        _activeSection = section;

        launchSection.IsVisible = section == "launch";
        modrinthSection.IsVisible = section == "modrinth";
        profilesSection.IsVisible = section == "profiles";
        performanceSection.IsVisible = section == "performance";
        settingsSection.IsVisible = section == "settings";
        layoutSection.IsVisible = section == "layout";

        ApplyNavState(launchNavButton, section == "launch");
        ApplyNavState(modrinthNavButton, section == "modrinth");
        ApplyNavState(profilesNavButton, section == "profiles");
        ApplyNavState(performanceNavButton, section == "performance");
        ApplyNavState(settingsNavButton, section == "settings");
        ApplyNavState(layoutNavButton, section == "layout");

        if (section == "modrinth" && _searchResults.Count == 0)
        {
            _ = SearchModrinthAsync();
        }
    }

    private async Task ListVersionsAsync(string category = "Versions")
    {
        _versionItems.Clear();

        if (_settings.OfflineMode)
        {
            // Try to load only local versions from cache to avoid internet overhead
            try 
            {
                var versionsDir = Path.Combine(_defaultMinecraftPath.BasePath, "versions");
                if (Directory.Exists(versionsDir))
                {
                    foreach (var dir in Directory.GetDirectories(versionsDir))
                    {
                        var versionName = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(versionName))
                        {
                            _versionItems.Add(versionName);
                        }
                    }
                }
                if (cbVersion.SelectedItem is null && _versionItems.Count > 0)
                    cbVersion.SelectedItem = _versionItems[0];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Death Client] Offline version list failed: {ex}");
            }
            return;
        }

        const int maxAttempts = 5;
        dynamic? versions = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                versions = await _defaultLauncher.GetAllVersionsAsync(CancellationToken.None);
                break;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                statusLabel.Text = "Fetching version manifest failed, retrying...";
                await Task.Delay(350 * attempt);
            }
            catch (Exception) when (attempt == maxAttempts)
            {
                 // Ignore on final attempt, will gracefully fall back
            }
        }

        if (versions is null)
        {
            // Gracefully fallback to local-only retrieval if internet or manifest is fully unavailable
            try 
            {
                Console.WriteLine("[Death Client] Falling back to local offline versions...");
                var versionsDir = Path.Combine(_defaultMinecraftPath.BasePath, "versions");
                if (Directory.Exists(versionsDir))
                {
                    foreach (var dir in Directory.GetDirectories(versionsDir))
                    {
                        var versionName = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(versionName))
                        {
                            _versionItems.Add(versionName);
                        }
                    }
                }
                if (cbVersion.SelectedItem is null && _versionItems.Count > 0)
                    cbVersion.SelectedItem = _versionItems[0];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Death Client] Version fallback read failed: {ex}");
            }
            return;
        }

        foreach (DictionaryEntry entry in (IEnumerable)versions)
        {
            var version = entry.Value;
            if (version is null || !ShouldIncludeVersion(version, category))
                continue;

            var nameProperty = version.GetType().GetProperty("Name");
            var versionName = nameProperty?.GetValue(version)?.ToString();
            if (!string.IsNullOrWhiteSpace(versionName))
            {
                _versionItems.Add(versionName);
            }
        }

        if (_selectedProfile is not null && !_versionItems.Contains(_selectedProfile.GameVersion))
            _versionItems.Insert(0, _selectedProfile.GameVersion);

        if (cbVersion.SelectedItem is null && _versionItems.Count > 0)
            cbVersion.SelectedItem = versions.LatestReleaseName;
    }

    private static bool ShouldIncludeVersion(object version, string category)
    {
        var name = version.GetType().GetProperty("Name")?.GetValue(version)?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var isRelease = Regex.IsMatch(name, @"^\d+(\.\d+)+$");
        var isSnapshot = Regex.IsMatch(name, @"^\d{2}w\d{2}[a-z]$", RegexOptions.IgnoreCase);

        if (string.Equals(category, "Versions", StringComparison.OrdinalIgnoreCase))
            return isRelease;

        if (string.Equals(category, "Snapshots", StringComparison.OrdinalIgnoreCase))
            return isSnapshot;

        return !isRelease && !isSnapshot;
    }

    private string GetSelectedVersionCategory() =>
        minecraftVersion.SelectedItem?.ToString() ?? VersionCategoryOptions[0];

    private async Task LaunchAsync()
    {
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
        {
            await DialogService.ShowInfoAsync(this, "Username required", "Enter a username before launching.");
            return;
        }

        var versionToLaunch = _selectedProfile?.VersionId ?? cbVersion.SelectedItem?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(versionToLaunch))
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version or profile before launching.");
            return;
        }

        var targetLabel = _selectedProfile?.Name ?? versionToLaunch;
        var shouldLaunch = await DialogService.ShowConfirmAsync(
            this,
            "Launch confirmation",
            $"Launch {targetLabel} as {usernameInput.Text.Trim()}?");
        if (!shouldLaunch)
            return;

        if (_selectedProfile is not null &&
            (_selectedProfile.Loader == "forge" || _selectedProfile.Loader == "neoforge"))
        {
            await DialogService.ShowInfoAsync(this, "Unsupported loader", "Forge and NeoForge packs can be downloaded, but launching those loaders is not implemented yet.");
            return;
        }

        ToggleBusyState(true, "Priming the launcher...");

        try
        {
            var launcherPath = _selectedProfile is null
                ? _defaultMinecraftPath
                : new MinecraftPath(_selectedProfile.InstanceDirectory);
            
            var launcher = CreateLauncher(launcherPath);

            if (_selectedProfile is not null)
            {
                await EnsureProfileReadyAsync(_selectedProfile, launcher, CancellationToken.None);
                
                // Ensure the Skin Shuffle ecosystem is installed automatically
                var modsDir = Path.Combine(_selectedProfile.InstanceDirectory, "mods");
                Directory.CreateDirectory(modsDir);
                ToggleBusyState(true, "Checking Skin Shuffle dependencies...");
                await InstallModIfMissingAsync("skinshuffle", _selectedProfile, modsDir, CancellationToken.None);
                await InstallModIfMissingAsync("yacl", _selectedProfile, modsDir, CancellationToken.None);
                
                // Also resolve missing user EMF requirement that crashed earlier
                await InstallModIfMissingAsync("entity_model_features", _selectedProfile, modsDir, CancellationToken.None);
                await InstallModIfMissingAsync("entity_texture_features", _selectedProfile, modsDir, CancellationToken.None);
                
                if (_settings.EnableFancyMenu)
                {
                    ToggleBusyState(true, "Installing FancyMenu...");
                    await EnsureFancyMenuInstalledAsync(_selectedProfile, CancellationToken.None);
                }
                
                versionToLaunch = _selectedProfile.VersionId;
            }
            else
            {
                await launcher.InstallAsync(versionToLaunch);
            }

            if (_selectedProfile is null || (_selectedProfile.Loader != "fabric" && _selectedProfile.Loader != "quilt"))
            {
                // Generate the vanilla offline resource pack fallback utilizing the captured SkinShuffle UUID texture
                EnsureOfflineSkinResourcePack(launcherPath.BasePath);
            }

            var session = MSession.CreateOfflineSession(usernameInput.Text.Trim());
            session.UUID = _playerUuid;

            var process = await launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
            {
                Session = session
            });
            process.Start();

            _settings.Username = usernameInput.Text.Trim();
            _settings.Version = cbVersion.SelectedItem?.ToString() ?? string.Empty;
            _settingsStore.Save(_settings);
            Close();
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Launch failed", $"Failed to launch Minecraft.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }



    private async Task DownloadSelectedVersionAsync()
    {
        if (_settings.OfflineMode)
        {
            await DialogService.ShowInfoAsync(this, "Offline Mode", "Downloading new versions is disabled in Offline Mode.");
            return;
        }

        if (cbVersion.SelectedItem is null)
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version to download.");
            return;
        }

        if (_selectedProfile is not null)
        {
            await DialogService.ShowInfoAsync(this, "Quick Launch only", "Version download is available for the default launcher. Clear the active profile first if you want to preinstall a vanilla version.");
            return;
        }

        var versionToInstall = cbVersion.SelectedItem.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(versionToInstall))
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version to download.");
            return;
        }

        ToggleBusyState(true, $"Downloading {versionToInstall}...");

        try
        {
            await _defaultLauncher.InstallAsync(versionToInstall);
            var existingProfile = _profileStore.LoadProfiles().FirstOrDefault(profile =>
                string.Equals(profile.GameVersion, versionToInstall, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.Loader, "vanilla", StringComparison.OrdinalIgnoreCase));

            if (existingProfile is null)
            {
                var downloadedProfile = _profileStore.CreateProfile($"Unnamed {versionToInstall}", versionToInstall, "vanilla");
                RefreshProfiles(downloadedProfile);
            }

            _settings.Version = versionToInstall;
            _settingsStore.Save(_settings);
            SetProgressState($"Downloaded {versionToInstall}.", 0, 0);
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Download failed", $"Failed to download Minecraft {versionToInstall}.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready");
        }
    }

    private async Task EnsureProfileReadyAsync(LauncherProfile profile, MinecraftLauncher launcher, CancellationToken cancellationToken)
    {
        if (profile.Loader == "fabric")
        {
            await launcher.InstallAsync(profile.GameVersion);
            await EnsureFabricProfileAsync(profile, cancellationToken);
            await launcher.InstallAsync(profile.VersionId);
        }
        else if (profile.Loader == "vanilla")
        {
            await launcher.InstallAsync(profile.GameVersion);
        }
        else if (profile.Loader == "quilt")
            throw new InvalidOperationException("Quilt profile launching is not implemented yet.");
        else if (profile.Loader == "forge" || profile.Loader == "neoforge")
            throw new InvalidOperationException($"{profile.Loader} profile launching is not implemented yet.");
    }

    private async Task EnsureFabricProfileAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoaderVersion))
            throw new InvalidOperationException("Fabric loader version is missing from the profile.");

        var versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
        var versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
        if (File.Exists(versionJsonPath))
            return;

        Directory.CreateDirectory(versionDirectory);
        var manifestJson = await _modrinthClient.GetStringAsync(
            $"https://meta.fabricmc.net/v2/versions/loader/{profile.GameVersion}/{profile.LoaderVersion}/profile/json",
            cancellationToken);

        using var manifestDocument = JsonDocument.Parse(manifestJson);
        if (manifestDocument.RootElement.TryGetProperty("id", out var idElement))
        {
            var profileVersionId = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(profileVersionId) &&
                !string.Equals(profile.VersionId, profileVersionId, StringComparison.Ordinal))
            {
                profile.VersionId = profileVersionId;
                _profileStore.Save(profile);
                versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
                versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
                Directory.CreateDirectory(versionDirectory);
            }
        }

        File.WriteAllText(versionJsonPath, manifestJson);
    }

    private void UsernameInput_TextChanged()
    {
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
        {
            _playerUuid = string.Empty;
            characterImage.Source = null;
            btnStart.IsEnabled = false;
            return;
        }

        btnStart.IsEnabled = true;
        _playerUuid = Character.GenerateUuidFromUsername(usernameInput.Text.Trim());
        UpdateCharacterPreview();
    }

    private void CbVersion_SelectionChanged()
    {
        UpdateCharacterPreview();
        if (_selectedProfile is null)
            SyncModrinthFilters();
    }

    private void UpdateCharacterPreview()
    {
        SyncSkinShuffleAvatarToLauncher();
        
        var skinPath = _settings.CustomSkinPath;
        if (string.IsNullOrEmpty(skinPath) || !File.Exists(skinPath))
            skinPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skin.png");

        if (!string.IsNullOrEmpty(skinPath) && File.Exists(skinPath))
        {
            try
            {
                using var fullSkin = new Bitmap(skinPath);

                // Render full player body: 16 wide x 32 tall (in skin-texture pixels)
                // Head=8x8, Body=8x12, Arms=4x12 each, Legs=4x12 each
                // Layout:  [4px arm][8px body][4px arm] = 16px wide
                //          Head at top centre (4,0) -> (12,8)
                //          Body at (4,8) -> (12,20)
                //          Left arm at (0,8) -> (4,20)
                //          Right arm at (12,8) -> (16,20)
                //          Left leg at (4,20) -> (8,32)
                //          Right leg at (8,20) -> (12,32)
                var bodyBmp = new RenderTargetBitmap(new PixelSize(16, 32));
                using (var ctx = bodyBmp.CreateDrawingContext())
                {
                    // === Head (base layer: 8,8 size 8x8) ===
                    ctx.DrawImage(fullSkin, new Rect(8, 8, 8, 8), new Rect(4, 0, 8, 8));
                    // Head overlay (40,8 size 8x8)
                    ctx.DrawImage(fullSkin, new Rect(40, 8, 8, 8), new Rect(4, 0, 8, 8));

                    // === Body (base layer: 20,20 size 8x12) ===
                    ctx.DrawImage(fullSkin, new Rect(20, 20, 8, 12), new Rect(4, 8, 8, 12));
                    // Body overlay (20,36 size 8x12)
                    ctx.DrawImage(fullSkin, new Rect(20, 36, 8, 12), new Rect(4, 8, 8, 12));

                    // === Right Arm (base layer: 44,20 size 4x12) ===
                    ctx.DrawImage(fullSkin, new Rect(44, 20, 4, 12), new Rect(0, 8, 4, 12));
                    // Right arm overlay (44,36 size 4x12)
                    ctx.DrawImage(fullSkin, new Rect(44, 36, 4, 12), new Rect(0, 8, 4, 12));

                    // === Left Arm (base layer: 36,52 size 4x12) ===
                    ctx.DrawImage(fullSkin, new Rect(36, 52, 4, 12), new Rect(12, 8, 4, 12));
                    // Left arm overlay (52,52 size 4x12)
                    ctx.DrawImage(fullSkin, new Rect(52, 52, 4, 12), new Rect(12, 8, 4, 12));

                    // === Right Leg (base layer: 4,20 size 4x12) ===
                    ctx.DrawImage(fullSkin, new Rect(4, 20, 4, 12), new Rect(4, 20, 4, 12));
                    // Right leg overlay (4,36 size 4x12)
                    ctx.DrawImage(fullSkin, new Rect(4, 36, 4, 12), new Rect(4, 20, 4, 12));

                    // === Left Leg (base layer: 20,52 size 4x12) ===
                    ctx.DrawImage(fullSkin, new Rect(20, 52, 4, 12), new Rect(8, 20, 4, 12));
                    // Left leg overlay (4,52 size 4x12)
                    ctx.DrawImage(fullSkin, new Rect(4, 52, 4, 12), new Rect(8, 20, 4, 12));

                    // === Cape (if available) ===
                    var capePath = _settings.CustomCapePath;
                    if (string.IsNullOrEmpty(capePath) || !File.Exists(capePath))
                        capePath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "cape.png");
                    if (!string.IsNullOrEmpty(capePath) && File.Exists(capePath))
                    {
                        try
                        {
                            using var capeBmp = new Bitmap(capePath);
                            // Cape texture front is at (1,1 size 10x16 in a 64x32 cape texture)
                            // Draw it behind/beside the body, offset slightly to the right to show it peeking
                            // We'll draw it overlapping the body area, slightly wider
                            ctx.DrawImage(capeBmp, new Rect(1, 1, 10, 16), new Rect(3, 8, 10, 16));
                        }
                        catch { /* cape load failed, skip */ }
                    }
                }

                characterImage.Source = bodyBmp;
                RenderOptions.SetBitmapInterpolationMode(characterImage, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
                return;
            }
            catch { /* Fallback to default if load fails */ }
        }

        // Fallback or No custom skin
        RenderOptions.SetBitmapInterpolationMode(characterImage, Avalonia.Media.Imaging.BitmapInterpolationMode.LowQuality);
        var selectedVersion = _selectedProfile?.GameVersion ?? cbVersion.SelectedItem?.ToString() ?? string.Empty;
        var resourceName = Character.GetCharacterResourceNameFromUuidAndGameVersion(_playerUuid, selectedVersion);
        
        string? imagePath = null;
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            imagePath = Path.Combine(AppContext.BaseDirectory, "Resources", $"{resourceName}.png");
            if (!File.Exists(imagePath))
                imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", $"{resourceName}.png");
        }

        if (imagePath != null && File.Exists(imagePath))
            characterImage.Source = new Bitmap(imagePath);
        else
            characterImage.Source = null;
    }

    private void _launcher_FileProgressChanged(object? sender, InstallerProgressChangedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            pbFiles.Maximum = Math.Max(1, args.TotalTasks);
            pbFiles.Value = Math.Min(args.ProgressedTasks, pbFiles.Maximum);
            statusLabel.Text = $"Installing {args.Name}";
            installDetailsLabel.Text = $"{args.ProgressedTasks} / {args.TotalTasks} files";
        });
    }

    private void _launcher_ByteProgressChanged(object? sender, ByteProgress args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            pbProgress.Maximum = 100;
            pbProgress.Value = args.TotalBytes <= 0
                ? 0
                : Math.Min(100, args.ProgressedBytes * 100d / args.TotalBytes);
        });
    }

    private void RefreshProfiles(LauncherProfile? selectProfile = null)
    {
        _profileItems.Clear();
        foreach (var profile in _profileStore.LoadProfiles())
            _profileItems.Add(profile);

        LauncherProfile? profileToSelect = null;
        if (selectProfile is not null)
            profileToSelect = _profileItems.FirstOrDefault(profile => string.Equals(profile.InstanceDirectory, selectProfile.InstanceDirectory, StringComparison.Ordinal));
        else if (_selectedProfile is not null)
            profileToSelect = _profileItems.FirstOrDefault(profile => string.Equals(profile.InstanceDirectory, _selectedProfile.InstanceDirectory, StringComparison.Ordinal));
        else if (!string.IsNullOrEmpty(_settings.LastSelectedProfilePath))
            profileToSelect = _profileItems.FirstOrDefault(profile => string.Equals(profile.InstanceDirectory, _settings.LastSelectedProfilePath, StringComparison.Ordinal));
        
        if (profileToSelect is null && _profileItems.Count > 0)
            profileToSelect = _profileItems[0];
        
        profileListBox.SelectedItem = profileToSelect;
        _selectedProfile = profileToSelect;
        UpdateLauncherContext();
    }

    private void ProfileListBox_SelectedIndexChanged()
    {
        _selectedProfile = profileListBox.SelectedItem as LauncherProfile;
        if (_selectedProfile is not null)
            profileNameInput.Text = _selectedProfile.Name;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
    }

    private void ClearSelectedProfile()
    {
        profileListBox.SelectedItem = null;
        _selectedProfile = null;
        profileNameInput.Text = string.Empty;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
    }

    private void UpdateLauncherContext()
    {
        if (_selectedProfile is null)
        {
            activeProfileBadge.Text = "HOME";
            activeContextLabel.Text = string.Empty;
            installModeLabel.Text = "Default";
            btnStart.Content = "▶ Play";
            profileInspectorTitle.Text = "Standard Profile";
            profileInspectorMeta.Text = "No isolated profile is active. Mods install only after you create or select a profile.";
            profileInspectorPath.Text = $"Instances root: {_profileStore.GetInstancesRoot()}";
            clearProfileButton.IsEnabled = false;
            renameProfileButton.IsEnabled = false;
            heroInstanceLabel.Text = "Standard Play";
            heroPerformanceLabel.Text = $"{cbVersion.SelectedItem?.ToString() ?? "1.21.1"} • Ready";
            homeFpsStatValue.Text = "144";
            homeRamStatValue.Text = "2 GB";
            performanceFpsStatValue.Text = "144";
            performanceRamStatValue.Text = "2 GB";
            return;
        }

        activeProfileBadge.Text = "ACTIVE";
        activeContextLabel.Text = string.Empty;
        installModeLabel.Text = _selectedProfile.Name;
        btnStart.Content = "▶ Play";
        profileInspectorTitle.Text = _selectedProfile.Name;
        profileInspectorMeta.Text = $"{_selectedProfile.LoaderDisplay} · Updated {_selectedProfile.UpdatedUtc.ToLocalTime():g}";
        profileInspectorPath.Text = _selectedProfile.InstanceDirectory;
        clearProfileButton.IsEnabled = true;
        renameProfileButton.IsEnabled = true;
        heroInstanceLabel.Text = _selectedProfile.Name;
        heroPerformanceLabel.Text = $"{_selectedProfile.GameVersion} • Ready";
        var fpsText = _selectedProfile.Loader == "vanilla" ? "165" : "120";
        var ramText = _selectedProfile.Loader == "vanilla" ? "2 GB" : "4 GB";
        homeFpsStatValue.Text = fpsText;
        homeRamStatValue.Text = ramText;
        performanceFpsStatValue.Text = fpsText;
        performanceRamStatValue.Text = ramText;

        _settings.LastSelectedProfilePath = _selectedProfile.InstanceDirectory;
        _settingsStore.Save(_settings);
    }

    private void SyncModrinthFilters()
    {
        modrinthVersionInput.Text = _selectedProfile?.GameVersion ?? cbVersion.SelectedItem?.ToString() ?? string.Empty;
        var loader = _selectedProfile?.Loader ?? "vanilla";

        var selectedIndex = Array.FindIndex(LoaderOptions, option => string.Equals(option, loader, StringComparison.OrdinalIgnoreCase));
        modrinthLoaderCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private async Task CreateProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(profileNameInput.Text))
        {
            await DialogService.ShowInfoAsync(this, "Profile name required", "Give the profile a name before creating it.");
            return;
        }

        if (cbVersion.SelectedItem is null)
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version before creating a profile.");
            return;
        }

        var loader = profileLoaderCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "vanilla";
        string? loaderVersion = null;

        try
        {
            ToggleBusyState(true, "Creating profile...");

            if (loader == "fabric")
                loaderVersion = await ResolveLatestFabricVersionAsync(cbVersion.SelectedItem!.ToString()!, CancellationToken.None);

            var profile = _profileStore.CreateProfile(profileNameInput.Text.Trim(), cbVersion.SelectedItem!.ToString()!, loader, loaderVersion);
            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);

            RefreshProfiles(profile);
            profileNameInput.Text = string.Empty;
            _instanceEditorOverlay.IsVisible = false;
            SetProgressState($"Profile {profile.Name} is ready.", 0, 0);
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Profile error", $"Failed to create profile.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task RenameSelectedProfileAsync()
    {
        if (_selectedProfile is null)
        {
            await DialogService.ShowInfoAsync(this, "Profile required", "Select an instance before renaming it.");
            return;
        }

        var nextName = profileNameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nextName))
        {
            await DialogService.ShowInfoAsync(this, "Profile name required", "Enter a new name for the selected instance.");
            return;
        }

        _selectedProfile.Name = nextName;
        _profileStore.Save(_selectedProfile);
        RefreshProfiles(_selectedProfile);
        _instanceEditorOverlay.IsVisible = false;
        SetProgressState($"Renamed to {nextName}.", 0, 0);
    }

    private async Task DeleteSelectedProfileAsync(LauncherProfile? profile = null)
    {
        var target = profile ?? _selectedProfile;
        if (target is null)
        {
            await DialogService.ShowInfoAsync(this, "Profile required", "Select an instance to delete first.");
            return;
        }

        var confirm = await DialogService.ShowConfirmAsync(
            this,
            "Delete confirmation",
            $"Are you sure you want to delete '{target.Name}'? This will delete all its files including worlds and mods!");

        if (confirm)
        {
            _profileStore.Delete(target);
            RefreshProfiles();
            if (target == _selectedProfile)
                ClearSelectedProfile();
            SetProgressState("Instance deleted.", 0, 0);
        }
    }

    private async Task QuickInstallInstanceAsync()
    {
        var version = _quickVersionCombo.SelectedItem?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version first.");
            return;
        }

        var loader = _quickLoaderCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "vanilla";
        var autoName = $"{version} {char.ToUpper(loader[0])}{loader[1..]}";
        string? loaderVersion = null;

        try
        {
            ToggleBusyState(true, $"Creating {autoName}...");

            if (loader == "fabric")
                loaderVersion = await ResolveLatestFabricVersionAsync(version, CancellationToken.None);

            var profile = _profileStore.CreateProfile(autoName, version, loader, loaderVersion);

            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);

            // Pre-download the game files
            var launcherPath = new MinecraftPath(profile.InstanceDirectory);
            var launcher = CreateLauncher(launcherPath);
            await launcher.InstallAsync(version);

            RefreshProfiles(profile);
            SetProgressState($"Instance \"{autoName}\" ready to play!", 0, 0);
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Install failed", $"Failed to create instance.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready");
        }
    }

    private async Task QuickModSearchAsync()
    {
        if (_settings.OfflineMode)
        {
            await DialogService.ShowInfoAsync(this, "Offline Mode", "Mod searching is disabled in Offline Mode.");
            return;
        }

        var query = _quickModSearch.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            await DialogService.ShowInfoAsync(this, "Search required", "Enter a mod name to search.");
            return;
        }

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            ToggleBusyState(true, "Searching...");
            var gameVersion = _selectedProfile?.GameVersion ?? cbVersion.SelectedItem?.ToString();
            var loader = _selectedProfile?.Loader;
            if (string.Equals(loader, "vanilla", StringComparison.OrdinalIgnoreCase))
                loader = null;

            var results = await _modrinthClient.SearchProjectsAsync(query, "mod", gameVersion, loader, _searchCancellation.Token);
            _quickSearchResults.Clear();
            foreach (var r in results.Take(8))
                _quickSearchResults.Add(r);

            SetProgressState($"Found {results.Count} mods.", 0, 0);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Search failed", $"Modrinth search failed.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready");
        }
    }

    private async Task QuickInstallModAsync(ModrinthProject project)
    {
        if (_selectedProfile is null)
        {
            await DialogService.ShowInfoAsync(this, "Profile required", "Create or select an instance first (use Quick Instance above, or the Instances tab).");
            return;
        }

        try
        {
            ToggleBusyState(true, $"Installing {project.Title}...");
            await InstallSelectedModAsync(project, CancellationToken.None);
            SetProgressState($"Installed {project.Title}!", 0, 0);
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Install failed", $"Install failed.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready");
        }
    }

    private async Task<string> ResolveLatestFabricVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        var payload = await _modrinthClient.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}", cancellationToken);
        using var json = JsonDocument.Parse(payload);
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("loader", out var loaderElement) &&
                loaderElement.TryGetProperty("version", out var versionElement))
            {
                var version = versionElement.GetString();
                if (!string.IsNullOrWhiteSpace(version))
                    return version;
            }
        }

        throw new InvalidOperationException($"No Fabric loader build was found for Minecraft {gameVersion}.");
    }

    private async Task SearchModrinthAsync()
    {
        if (_settings.OfflineMode)
        {
            await DialogService.ShowInfoAsync(this, "Offline Mode", "Mod searching is disabled in Offline Mode.");
            return;
        }

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            ToggleBusyState(true, "Searching across platforms...");

            var projectType = modrinthProjectTypeCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "mod";
            var gameVersion = string.IsNullOrWhiteSpace(modrinthVersionInput.Text) ? null : modrinthVersionInput.Text.Trim();
            var loader = NormalizeLoaderFilter();
            
            var modrinthTask = _modrinthClient.SearchProjectsAsync(modrinthSearchInput.Text ?? "", projectType, gameVersion, loader, _searchCancellation.Token);
            
            Task<IReadOnlyList<ModrinthProject>>? curseForgeTask = null;
            if (projectType == "mod")
                curseForgeTask = _curseForgeClient.SearchModsAsync(modrinthSearchInput.Text ?? "", gameVersion, loader, _searchCancellation.Token);
            else if (projectType == "modpack")
                curseForgeTask = _curseForgeClient.SearchPacksAsync(modrinthSearchInput.Text ?? "", gameVersion, _searchCancellation.Token);

            var mrResults = await modrinthTask;
            var cfResults = curseForgeTask != null ? await curseForgeTask : [];

            var results = new List<ModrinthProject>(mrResults.Count + cfResults.Count);
            int i = 0, j = 0;
            while (i < mrResults.Count || j < cfResults.Count)
            {
                if (i < mrResults.Count) results.Add(mrResults[i++]);
                if (j < cfResults.Count) results.Add(cfResults[j++]);
            }

            BindSearchResults(results);
            SetProgressState($"Found {results.Count} results from Modrinth and CurseForge.", 0, 0);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Search failed", $"Search failed.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private string? NormalizeLoaderFilter()
    {
        var selected = modrinthLoaderCombo.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected) || string.Equals(selected, "Any", StringComparison.OrdinalIgnoreCase))
            return null;

        return selected.ToLowerInvariant();
    }

    private void BindSearchResults(IReadOnlyList<ModrinthProject> results)
    {
        _searchResults.Clear();
        foreach (var result in results)
            _searchResults.Add(result);

        modrinthResultsSummary.Text = results.Count == 0
            ? "No matching projects were found for the current filters."
            : $"Found {results.Count} result{(results.Count == 1 ? string.Empty : "s")} for {modrinthProjectTypeCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "projects"}.";
        modrinthResultsListBox.SelectedItem = _searchResults.FirstOrDefault();
        if (_searchResults.Count == 0)
        {
            modrinthDetailsBox.Text = "No matching Modrinth projects found for the current filters.";
            installSelectedButton.IsEnabled = false;
        }
    }

    private Control BuildLayoutDeck()
    {
        var title = CreateSectionTitle("Client Layout", "Customize your launcher and game interface.");

        var sidebarToggle = new ToggleSwitch
        {
            Content = "Sidebar Position",
            OnContent = "Right",
            OffContent = "Left",
            IsChecked = _settings.ClientLayout?.Contains("sidebar:right") ?? false,
            Foreground = Brushes.White
        };
        sidebarToggle.IsCheckedChanged += (_, _) => {
            var side = sidebarToggle.IsChecked == true ? "sidebar:right" : "sidebar:left";
            _settings.ClientLayout = side;
            _settingsStore.Save(_settings);
            Content = BuildRoot();
            SetActiveSection("layout");
        };

        var colorSection = CreateSubCard("Theme & Appearance", new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Pick a primary accent color for the launcher UI.", Foreground = new SolidColorBrush(Color.Parse("#B0BACF")), FontSize = 14 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        CreateColorPreset("#6E5BFF"),
                        CreateColorPreset("#FF5B5B"),
                        CreateColorPreset("#5BFF85"),
                        CreateColorPreset("#FFB85B"),
                        CreateColorPreset("#5BC2FF")
                    }
                },
                sidebarToggle
            }
        }, "#1A2035");

        var bgBtn = CreateSecondaryButton("Choose Background Image");
        bgBtn.Click += async (_, _) => {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select Background Image", FileTypeFilter = [FilePickerFileTypes.ImageAll] });
            if (files.Count > 0) {
                try {
                    var srcPath = files[0].Path.LocalPath;
                    var destDir = Path.Combine(_defaultMinecraftPath.BasePath, "death-client");
                    Directory.CreateDirectory(destDir);
                    var destPath = Path.Combine(destDir, "custom_bg.png");
                    File.Copy(srcPath, destPath, true);
                    // Update visuals
                    Content = BuildRoot();
                } catch (Exception ex) {
                    await DialogService.ShowInfoAsync(this, "Error", "Failed to set background: " + ex.Message);
                }
            }
        };

        var backgroundSection = CreateSubCard("Background", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Set a custom wallpaper for the launcher dashboard.", Foreground = new SolidColorBrush(Color.Parse("#B0BACF")), FontSize = 14 },
                bgBtn
            }
        }, "#1A2035");

        var fancyMenuToggle = new ToggleSwitch
        {
            Content = "Enable FancyMenu Integration",
            IsChecked = _settings.EnableFancyMenu,
            OnContent = "Enabled",
            OffContent = "Disabled",
            Foreground = Brushes.White
        };
        fancyMenuToggle.IsCheckedChanged += (_, _) => {
            _settings.EnableFancyMenu = fancyMenuToggle.IsChecked ?? false;
            _settingsStore.Save(_settings);
        };

        var fmSection = CreateSubCard("Minecraft Home Screen", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Automatically install FancyMenu and a custom layout in your Minecraft instances.", Foreground = new SolidColorBrush(Color.Parse("#B0BACF")), FontSize = 14, TextWrapping = TextWrapping.Wrap },
                fancyMenuToggle,
                new TextBlock { Text = "Note: This will download FancyMenu and Konkrete mods during launch.", Foreground = new SolidColorBrush(Color.Parse("#6E5BFF")), FontSize = 12, FontWeight = FontWeight.Bold }
            }
        }, "#1A2035");

        var orderSection = CreateSubCard("Launch Screen Order", CreateSectionOrderPicker(), "#1A2035");

        return CreateSectionScroller(new StackPanel
        {
            Spacing = 24,
            Children = { title, colorSection, backgroundSection, orderSection, fmSection }
        });
    }

    private Control CreateSectionOrderPicker()
    {
        var panel = new StackPanel { Spacing = 12 };
        for (int i = 0; i < _settings.SectionOrder.Count; i++)
        {
            var idx = i;
            var name = _settings.SectionOrder[i];
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(4) };
            row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold });
            
            var upBtn = new Button { Content = "↑", Width = 32, Height = 32, Margin = new Thickness(4,0) };
            upBtn.Click += (_, _) => {
                if (idx > 0) {
                    var tmp = _settings.SectionOrder[idx];
                    _settings.SectionOrder[idx] = _settings.SectionOrder[idx-1];
                    _settings.SectionOrder[idx-1] = tmp;
                    _settingsStore.Save(_settings);
                    Content = BuildRoot();
                    SetActiveSection("layout");
                }
            };
            
            var downBtn = new Button { Content = "↓", Width = 32, Height = 32 };
            downBtn.Click += (_, _) => {
                if (idx < _settings.SectionOrder.Count - 1) {
                    var tmp = _settings.SectionOrder[idx];
                    _settings.SectionOrder[idx] = _settings.SectionOrder[idx+1];
                    _settings.SectionOrder[idx+1] = tmp;
                    _settingsStore.Save(_settings);
                    Content = BuildRoot();
                    SetActiveSection("layout");
                }
            };
            
            row.Children.Add(upBtn.With(column: 1));
            row.Children.Add(downBtn.With(column: 2));
            panel.Children.Add(row);
        }
        return panel;
    }

    private Button CreateColorPreset(string hex)
    {
        var btn = new Button
        {
            Width = 32,
            Height = 32,
            Background = new SolidColorBrush(Color.Parse(hex)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(_settings.AccentColor == hex ? 2 : 0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        btn.Click += (_, _) => {
            _settings.AccentColor = hex;
            _settingsStore.Save(_settings);
            Content = BuildRoot();
            SetActiveSection("layout");
        };
        return btn;
    }
    private void UpdateSelectedProjectDetails()
    {
        if (modrinthResultsListBox.SelectedItem is not ModrinthProject project)
        {
            modrinthDetailsBox.Text = "Search to browse mods and modpacks.";
            installSelectedButton.IsEnabled = false;
            return;
        }

        installSelectedButton.IsEnabled = true;
        installSelectedButton.Content = project.ProjectType == "modpack" ? "↓ Pack" : "↓ Mod";
        modrinthResultsSummary.Text = $"Selected {project.Title} by {project.Author}.";
        modrinthDetailsBox.Text =
            $"{project.Title}\n" +
            $"Type: {project.ProjectType}\n" +
            $"Author: {project.Author}\n" +
            $"Downloads: {project.Downloads:N0}\n" +
            $"Followers: {project.Follows:N0}\n" +
            $"Categories: {string.Join(", ", project.Categories)}\n\n" +
            $"{project.Description}";
    }

    private async Task InstallSelectedAsync()
    {
        if (modrinthResultsListBox.SelectedItem is not ModrinthProject project)
            return;

        try
        {
            ToggleBusyState(true, $"Installing {project.Title}...");

            if (project.ProjectType == "modpack")
                await InstallModpackFromProjectAsync(project, CancellationToken.None);
            else
                await InstallSelectedModAsync(project, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Install failed", $"Install failed.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task InstallSelectedModAsync(ModrinthProject project, CancellationToken cancellationToken)
    {
        if (_selectedProfile is null)
        {
            await DialogService.ShowInfoAsync(this, "Profile required", "Create or select a profile before installing mods.");
            return;
        }

        if (project.IsCurseForge)
        {
            await InstallCurseForgeModAsync(project, cancellationToken);
            return;
        }

        var versions = await _modrinthClient.GetProjectVersionsAsync(project.ProjectId, _selectedProfile.GameVersion, _selectedProfile.Loader, cancellationToken);
        var version = versions.FirstOrDefault(HasPrimaryFile) ?? versions.FirstOrDefault();
        if (version is null)
            throw new InvalidOperationException($"No compatible version was found for {_selectedProfile.LoaderDisplay}.");

        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { project.ProjectId };
        await InstallModVersionAsync(_selectedProfile, version, installed, cancellationToken);
        SetProgressState($"Installed {project.Title} into {_selectedProfile.Name}.", 0, 0);
    }

    private async Task InstallCurseForgeModAsync(ModrinthProject project, CancellationToken cancellationToken)
    {
        var files = await _curseForgeClient.GetProjectVersionsAsync(project.ProjectId, _selectedProfile!.GameVersion, _selectedProfile.Loader, cancellationToken);
        var file = files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException("No compatible file found on CurseForge.");

        var modsDir = Path.Combine(_selectedProfile.InstanceDirectory, "mods");
        Directory.CreateDirectory(modsDir);
        var dest = Path.Combine(modsDir, file.FileName);

        if (string.IsNullOrEmpty(file.DownloadUrl))
            throw new InvalidOperationException("This mod has downloads disabled for 3rd party launchers on CurseForge.");

        await _curseForgeClient.DownloadFileAsync(file.DownloadUrl, dest, null, cancellationToken);
        SetProgressState($"Installed {project.Title} (CurseForge) into {_selectedProfile.Name}.", 0, 0);
    }

    private static bool HasPrimaryFile(ModrinthProjectVersion version) =>
        version.Files.Any(file => file.Primary && file.Filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private async Task InstallModVersionAsync(LauncherProfile profile, ModrinthProjectVersion version, HashSet<string> installedProjectIds, CancellationToken cancellationToken)
    {
        foreach (var dependency in version.Dependencies.Where(d => d.DependencyType == "required" && !string.IsNullOrWhiteSpace(d.ProjectId)))
        {
            if (!installedProjectIds.Add(dependency.ProjectId!))
                continue;

            var dependencyVersions = await _modrinthClient.GetProjectVersionsAsync(dependency.ProjectId!, profile.GameVersion, profile.Loader, cancellationToken);
            var dependencyVersion = dependencyVersions.FirstOrDefault(HasPrimaryFile) ?? dependencyVersions.FirstOrDefault();
            if (dependencyVersion is not null)
                await InstallModVersionAsync(profile, dependencyVersion, installedProjectIds, cancellationToken);
        }

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException($"Version {version.VersionNumber} did not include a downloadable file.");

        Directory.CreateDirectory(profile.ModsDirectory);
        var destinationPath = Path.Combine(profile.ModsDirectory, file.Filename);
        await _modrinthClient.DownloadFileAsync(file.Url, CreateDownloadDestination(destinationPath), CreateDownloadProgress(file.Filename), cancellationToken);
        await VerifyFileHashAsync(destinationPath, file.Hashes);
        _profileStore.Save(profile);
    }

    private async Task VerifyFileHashAsync(string filePath, IReadOnlyDictionary<string, string> hashes)
    {
        if (!hashes.TryGetValue("sha1", out var expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
            return;

        await using var file = File.OpenRead(filePath);
        var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(file)).ToLowerInvariant();
        if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Hash mismatch detected for {Path.GetFileName(filePath)}.");
    }

    private async Task InstallModpackFromProjectAsync(ModrinthProject project, CancellationToken cancellationToken)
    {
        var gameVersion = string.IsNullOrWhiteSpace(modrinthVersionInput.Text) ? null : modrinthVersionInput.Text.Trim();
        var loader = NormalizeLoaderFilter();
        var versions = await _modrinthClient.GetProjectVersionsAsync(project.ProjectId, gameVersion, loader, cancellationToken);
        var version = versions.FirstOrDefault(v => v.Files.Any(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)))
            ?? versions.FirstOrDefault();
        if (version is null)
            throw new InvalidOperationException("No compatible modpack build was found.");

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException("The selected modpack version has no downloadable file.");

        var tempMrpack = Path.Combine(Path.GetTempPath(), $"{project.Slug}-{version.VersionNumber}.mrpack");
        await _modrinthClient.DownloadFileAsync(file.Url, tempMrpack, CreateDownloadProgress(file.Filename), cancellationToken);
        await InstallMrpackAsync(tempMrpack, project, cancellationToken);
    }

    private async Task ImportMrpackAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Modrinth modpack",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Modrinth Modpack")
                {
                    Patterns = ["*.mrpack"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            await DialogService.ShowInfoAsync(this, "Import failed", "The selected file is not available as a local path.");
            return;
        }

        try
        {
            ToggleBusyState(true, $"Importing {Path.GetFileName(localPath)}...");
            await InstallMrpackAsync(localPath, null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Import failed", $"Modpack import failed.\n{ex.Message}");
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task InstallMrpackAsync(string mrpackPath, ModrinthProject? sourceProject, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(mrpackPath);
        var indexEntry = archive.GetEntry("modrinth.index.json")
            ?? throw new InvalidOperationException("The pack is missing modrinth.index.json.");

        await using var indexStream = indexEntry.Open();
        var index = await JsonSerializer.DeserializeAsync<MrPackIndex>(indexStream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to read the modpack manifest.");

        if (!string.Equals(index.Game, "minecraft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported pack game: {index.Game}.");

        var gameVersion = index.Dependencies.TryGetValue("minecraft", out var minecraftVersion)
            ? minecraftVersion
            : throw new InvalidOperationException("The modpack does not specify a Minecraft version.");

        var loader = "vanilla";
        string? loaderVersion = null;

        foreach (var candidate in new[] { "fabric", "quilt", "forge", "neoforge" })
        {
            if (index.Dependencies.TryGetValue(candidate, out var candidateVersion))
            {
                loader = candidate;
                loaderVersion = candidateVersion;
                break;
            }
        }

        var profileName = string.IsNullOrWhiteSpace(index.Name)
            ? sourceProject?.Title ?? Path.GetFileNameWithoutExtension(mrpackPath)
            : index.Name;
        var profile = _profileStore.CreateProfile(profileName, gameVersion, loader, loaderVersion, sourceProject?.Slug);

        pbFiles.Maximum = Math.Max(1, index.Files.Count);
        pbFiles.Value = 0;

        int completedFiles = 0;
        foreach (var file in index.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(file.Env?.Client, "unsupported", StringComparison.OrdinalIgnoreCase))
                continue;

            var downloadUrl = file.Downloads.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(downloadUrl))
                continue;

            var destinationPath = GetSafeDestinationPath(profile.InstanceDirectory, file.Path);
            await _modrinthClient.DownloadFileAsync(downloadUrl, CreateDownloadDestination(destinationPath), CreateDownloadProgress(file.Path), cancellationToken);
            await VerifyFileHashAsync(destinationPath, file.Hashes);

            completedFiles++;
            pbFiles.Value = Math.Min(pbFiles.Maximum, completedFiles);
            installDetailsLabel.Text = $"{completedFiles} / {index.Files.Count} pack files";
        }

        ExtractOverrideEntries(archive, "overrides/", profile.InstanceDirectory);
        ExtractOverrideEntries(archive, "client-overrides/", profile.InstanceDirectory);

        if (loader == "fabric")
            await EnsureFabricProfileAsync(profile, cancellationToken);

        _profileStore.Save(profile);
        RefreshProfiles(profile);
        SetProgressState($"Installed modpack {profile.Name}.", 0, 0);

        if (loader == "forge" || loader == "neoforge" || loader == "quilt")
        {
            await DialogService.ShowInfoAsync(this, "Pack imported", $"{profile.Name} was imported, but launching {loader} packs is not implemented yet.");
        }
    }

    private static void ExtractOverrideEntries(ZipArchive archive, string prefix, string destinationRoot)
    {
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var relativePath = entry.FullName[prefix.Length..];
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            var destinationPath = GetSafeDestinationPath(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                continue;

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static string GetSafeDestinationPath(string root, string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe path detected: {relativePath}");

        return fullPath;
    }

    private Progress<(long BytesRead, long? TotalBytes)> CreateDownloadProgress(string fileName)
    {
        return new Progress<(long BytesRead, long? TotalBytes)>(progress =>
        {
            statusLabel.Text = $"Downloading {Path.GetFileName(fileName)}";
            if (progress.TotalBytes is long totalBytes && totalBytes > 0)
            {
                pbProgress.Value = Math.Min(100, progress.BytesRead * 100d / totalBytes);
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} / {FormatBytes(totalBytes)}";
            }
            else
            {
                pbProgress.Value = 0;
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} downloaded";
            }
        });
    }

    private void ToggleBusyState(bool isBusy, string statusText)
    {
        btnStart.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(usernameInput.Text);
        downloadVersionButton.IsEnabled = !isBusy && _selectedProfile is null;
        createProfileButton.IsEnabled = !isBusy;
        modrinthSearchButton.IsEnabled = !isBusy;
        installSelectedButton.IsEnabled = !isBusy && modrinthResultsListBox.SelectedItem is ModrinthProject;
        importMrpackButton.IsEnabled = !isBusy;
        _quickInstallButton.IsEnabled = !isBusy;
        _quickModSearchButton.IsEnabled = !isBusy;
        _playOverlay.IsEnabled = !isBusy;
        _playOverlay.Opacity = isBusy ? 0.5 : 1;
        statusLabel.Text = statusText;
        if (!isBusy)
            pbProgress.Value = 0;
    }

    private void SetProgressState(string statusText, int fileProgress, int byteProgress)
    {
        statusLabel.Text = statusText;
        installDetailsLabel.Text = _selectedProfile?.LoaderDisplay ?? cbVersion.SelectedItem?.ToString() ?? string.Empty;
        pbFiles.Value = Math.Clamp(fileProgress, 0, (int)pbFiles.Maximum);
        pbProgress.Value = Math.Clamp(byteProgress, 0, (int)pbProgress.Maximum);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.#} {sizes[order]}";
    }

    private static TextBlock CreateStatValue()
    {
        return new TextBlock
        {
            Text = "--",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeight.Black,
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
    }

    private static Border CreateCompactStat(string title, TextBlock valueBlock)
    {
        return CreateGlassPanel(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = $"{title}:",
                    Foreground = new SolidColorBrush(Color.Parse("#9EB2E0")),
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                },
                valueBlock.With(column: 1)
            }
        }, padding: new Thickness(14, 10));
    }

    private Control CreateHeroPanel()
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 20,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.2*,0.42*"),
                    ColumnSpacing = 20,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 14,
                            Children =
                            {
                                heroInstanceLabel,
                                heroPerformanceLabel,
                                new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
                                    CornerRadius = new CornerRadius(14),
                                    Padding = new Thickness(14, 10),
                                    Child = usernameInput
                                },
                                new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions("1*"),
                                    Children =
                                    {
                                        btnStart
                                    }
                                }
                            }
                        },
                        new StackPanel
                        {
                            Spacing = 12,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                CreateGlassPanel(new StackPanel
                                {
                                    Spacing = 6,
                                    Children =
                                    {
                                        activeProfileBadge,
                                        installDetailsLabel,
                                        statusLabel
                                    }
                                }, padding: new Thickness(16)),
                                CreateAppearanceCard()
                            }
                        }.With(column: 1)
                    }
                }
            }
        });
    }

    private Control CreateSummaryCard()
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreatePanelEyebrow("Overview"),
                new TextBlock
                {
                    Text = _selectedProfile is null ? "Quick play" : _selectedProfile.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    FontSize = 18
                },
                CreateMiniFeatureRow("◈", "Mods", "Install from Modrinth"),
                CreateMiniFeatureRow("▣", "Instances", "Separate profiles"),
                CreateMiniFeatureRow("⚡", "State", "Ready")
            }
        });
    }

    private Control CreateAppearanceCard()
    {
        var skinButton = CreateSecondaryButton("Skin");
        skinButton.IsEnabled = false;

        var capeButton = CreateSecondaryButton("Cape");
        capeButton.IsEnabled = false;

        return CreateGlassPanel(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreatePanelEyebrow("Appearance"),
                characterImage,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        skinButton,
                        capeButton.With(column: 1)
                    }
                },
                new TextBlock
                {
                    Text = "Placeholder",
                    Foreground = new SolidColorBrush(Color.Parse("#8EA3D4")),
                    FontSize = 12
                }
            }
        }, padding: new Thickness(16));
    }

    private Control CreatePerformanceStatusCard()
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreatePanelEyebrow("Performance"),
                new TextBlock
                {
                    Text = "Stable",
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    FontSize = 18
                },
                CreateMiniFeatureRow("◌", "Frame pacing", "Stable target profile"),
                CreateMiniFeatureRow("◔", "Memory route", "Adaptive RAM suggestion")
            }
        });
    }

    private Control CreateActivityCard()
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreatePanelEyebrow("Recent Activity"),
                CreateMiniFeatureRow("▶", "Launch route", "Default play path armed"),
                CreateMiniFeatureRow("▣", "Instances", "Profile context stays isolated"),
                CreateMiniFeatureRow("⌕", "Discovery", "Search and install without leaving launcher")
            }
        });
    }

    private Control CreateSuggestedModsCard()
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreatePanelEyebrow("Suggested Mods"),
                CreateMiniFeatureRow("⚡", "Sodium", "High-FPS rendering"),
                CreateMiniFeatureRow("☄", "Lithium", "Server and tick optimizations"),
                CreateMiniFeatureRow("✦", "FerriteCore", "Lower memory pressure")
            }
        });
    }

    private Control CreateLogsCard()
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreatePanelEyebrow("Logs"),
                new Expander
                {
                    Header = new TextBlock
                    {
                        Text = "Console output",
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold
                    },
                    Content = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#0A0F18")),
                        CornerRadius = new CornerRadius(16),
                        Padding = new Thickness(14),
                        Child = new TextBlock
                        {
                            Text = $"{statusLabel.Text}\n{installDetailsLabel.Text}",
                            Foreground = new SolidColorBrush(Color.Parse("#A8F0E5")),
                            FontFamily = new FontFamily("Consolas, Inter, monospace"),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            }
        });
    }

    private static Control CreateMiniFeatureRow(string icon, string title, string subtitle)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(70, 15, 22, 39)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 85, 102, 145)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                ColumnSpacing = 12,
                Children =
                {
                    new Border
                    {
                        Width = 38,
                        Height = 38,
                        CornerRadius = new CornerRadius(12),
                        Background = new SolidColorBrush(Color.FromArgb(110, 107, 91, 255)),
                        Child = new TextBlock
                        {
                            Text = icon,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.Bold
                        }
                    },
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                Foreground = Brushes.White,
                                FontWeight = FontWeight.Bold
                            },
                            new TextBlock
                            {
                                Text = subtitle,
                                Foreground = new SolidColorBrush(Color.Parse("#9CADD3"))
                            }
                        }
                    }.With(column: 1)
                }
            }
        };
    }

    private static Control CreateProgressRow(string title, ProgressBar progressBar)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    Foreground = new SolidColorBrush(Color.Parse("#9EB2E0")),
                    FontWeight = FontWeight.SemiBold
                },
                progressBar
            }
        };
    }

    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            Background = new SolidColorBrush(Color.FromArgb(120, 19, 27, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#36476A")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 11),
            CornerRadius = new CornerRadius(16),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
    }

    private static ComboBox CreateComboBox(IEnumerable<object> items)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = items.ToList(),
            Background = new SolidColorBrush(Color.FromArgb(120, 19, 27, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#36476A")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(16),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(comboBox);
        return comboBox;
    }

    private static ComboBox CreateComboBox(IEnumerable<string> items)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = items,
            Background = new SolidColorBrush(Color.FromArgb(120, 19, 27, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#36476A")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(16),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(comboBox);
        return comboBox;
    }

    private static Button CreatePrimaryButton(string text, string hexColor, Color foreground)
    {
        var button = new Button
        {
            Content = text,
            Height = 50,
            Background = new SolidColorBrush(Color.Parse(hexColor)),
            Foreground = new SolidColorBrush(foreground),
            BorderBrush = Brushes.Transparent,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(18, 12),
            CornerRadius = new CornerRadius(18),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(button);
        return button;
    }

    private static Button CreateNavButton(string icon, string label)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = icon, FontSize = 20, Width = 28, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Height = 60,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            FontWeight = FontWeight.Bold,
            FontSize = 17,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(24, 0),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(button);
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Height = 48,
            Background = new SolidColorBrush(Color.FromArgb(85, 16, 23, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3C4F73")),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(18, 12),
            CornerRadius = new CornerRadius(18),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(button);
        return button;
    }

    private static ProgressBar CreateProgressBar()
    {
        return new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static Border BuildCard(Control child)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0D1522")),
            BorderBrush = new SolidColorBrush(Color.Parse("#203046")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(22),
            Child = child
        };
    }

    private static Border CreateGlassPanel(Control child, Thickness? padding = null, Thickness? margin = null)
    {
        var panel = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(60, 25, 31, 56), 0),
                    new GradientStop(Color.FromArgb(30, 15, 21, 36), 1)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 120, 140, 200)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Padding = padding ?? new Thickness(24),
            Margin = margin ?? default,
            Child = child,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 40,
                OffsetX = 0,
                OffsetY = 20,
                Color = Color.FromArgb(60, 0, 0, 0)
            })
        };
        ApplyHoverMotion(panel);
        return panel;
    }

    private static Border CreatePanelEyebrow(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(110, 106, 90, 255)),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text.ToUpperInvariant(),
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 11,
                LetterSpacing = 1.1
            }
        };
    }

    private static Control CreateSectionTitle(string text, string subtitle)
    {
        return new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 0, 20),
            Children =
            {
                new TextBlock
                {
                    Text = text,
                    FontSize = 32,
                    FontWeight = FontWeight.Black,
                    Foreground = Brushes.White,
                    LetterSpacing = 1.2
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(Color.Parse("#A4B4DA")),
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }

    private static TextBlock CreateCaption(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse("#B9C1D3")),
            FontWeight = FontWeight.SemiBold
        };
    }

    private static Control WrapScrollable(Control child)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0D111C")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(2),
            Child = child
        };
    }

    private static Control CreateSectionScroller(Control child)
    {
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = child
        };
    }

    private static Border CreateChip(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#101A29")),
            BorderBrush = new SolidColorBrush(Color.Parse("#23405C")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(10, 5),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.Parse("#D6E6F8")),
                FontWeight = FontWeight.SemiBold
            }
        };
    }

    private static Border CreateMutedChip(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(50, 22, 29, 46)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 60, 72, 105)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 5),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.Parse("#93A4C9")),
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private static Border CreateMetricTile(string title, string subtitle)
    {
        var tile = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(100, 18, 26, 44), 0),
                    new GradientStop(Color.FromArgb(90, 14, 19, 33), 1)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(125, 80, 96, 140)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        FontSize = 15
                    },
                    new TextBlock
                    {
                        Text = subtitle,
                        Foreground = new SolidColorBrush(Color.Parse("#92A0BC")),
                        FontSize = 12
                    }
                }
            }
        };
        ApplyHoverMotion(tile);
        return tile;
    }

    private static Border CreateSubCard(string title, Control body, string backgroundHex)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(backgroundHex)),
            BorderBrush = new SolidColorBrush(Color.Parse("#21364F")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        FontSize = 16
                    },
                    body
                }
            }
        };
    }

    private static Border CreateInfoStrip(string title, Control body, string backgroundHex, string borderHex)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(backgroundHex)),
            BorderBrush = new SolidColorBrush(Color.Parse(borderHex)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = new SolidColorBrush(Color.Parse("#8FB7FF")),
                        FontWeight = FontWeight.Bold
                    },
                    body
                }
            }
        };
    }

    private static void ApplyNavState(Button button, bool isActive)
    {
        var accentHex = UserSettingsStore.Instance?.Load().AccentColor ?? "#6E5BFF";
        var accentColor = Color.Parse(accentHex);
        
        button.Background = new SolidColorBrush(Color.FromArgb((byte)(isActive ? 30 : 0), accentColor.R, accentColor.G, accentColor.B));
        button.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(isActive ? 100 : 0), accentColor.R, accentColor.G, accentColor.B));
        button.Foreground = new SolidColorBrush(isActive ? accentColor : Color.Parse("#B0BACF"));
        button.CornerRadius = new CornerRadius(14);
        button.Padding = new Thickness(16, 10);
    }

    private static Border CreateStatTile(string title, TextBlock valueBlock, string subtitle)
    {
        return CreateGlassPanel(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreatePanelEyebrow(title),
                valueBlock,
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(Color.Parse("#A4B4DA"))
                }
            }
        });
    }

    private async Task EnsureFancyMenuInstalledAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        var modsDir = Path.Combine(profile.InstanceDirectory, "mods");
        Directory.CreateDirectory(modsDir);

        var fmDir = Path.Combine(profile.InstanceDirectory, "fancymenu");
        Directory.CreateDirectory(fmDir);
        Directory.CreateDirectory(Path.Combine(fmDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(fmDir, "assets"));

        // Download FancyMenu and Konkrete if not present
        await InstallModIfMissingAsync("fancymenu", profile, modsDir, cancellationToken);
        await InstallModIfMissingAsync("konkrete", profile, modsDir, cancellationToken);

        // Create default layout
        var layoutPath = Path.Combine(fmDir, "layouts", "death_client_main.txt");
        var layoutContent = 
            "type: layout\n" +
            "layout_name: death_client\n" +
            "layout_target: main_menu\n\n" +
            "[background]\n" +
            "background_type: image\n" +
            "background_image: death_client/bg.png\n";
        
        await File.WriteAllTextAsync(layoutPath, layoutContent, cancellationToken);

        // Copy background if exists
        var customBgPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBgPath))
        {
            var fmAssetsDir = Path.Combine(fmDir, "assets", "death_client");
            Directory.CreateDirectory(fmAssetsDir);
            File.Copy(customBgPath, Path.Combine(fmAssetsDir, "bg.png"), true);
        }
    }

    private async Task InstallModIfMissingAsync(string slug, LauncherProfile profile, string modsDir, CancellationToken cancellationToken)
    {
        var existing = Directory.GetFiles(modsDir, $"*{slug}*.jar");
        if (existing.Length > 0) return;

        var results = await _modrinthClient.SearchProjectsAsync(slug, "mod", profile.GameVersion, profile.Loader, cancellationToken);
        var project = results.FirstOrDefault(p => p.Slug == slug || p.Title.ToLowerInvariant().Contains(slug));
        if (project != null)
        {
            await InstallSelectedModAsync(project, cancellationToken);
        }
    }

    private void SyncSkinShuffleAvatarToLauncher()
    {
        if (_selectedProfile is null) return;
        
        try
        {
            var configDir = Path.Combine(_selectedProfile.InstanceDirectory, "config", "skinshuffle");
            var presetsPath = Path.Combine(configDir, "presets.json");
            
            if (File.Exists(presetsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(presetsPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("chosenPreset", out var chosenPresetElem) && 
                    root.TryGetProperty("loadedPresets", out var presetsArray))
                {
                    int chosenIdx = chosenPresetElem.GetInt32();
                    if (chosenIdx >= 0 && chosenIdx < presetsArray.GetArrayLength())
                    {
                        var preset = presetsArray[chosenIdx];
                        if (preset.TryGetProperty("skin", out var skinObj) && 
                            skinObj.TryGetProperty("skin_name", out var skinNameElem))
                        {
                            var skinName = skinNameElem.GetString();
                            if (!string.IsNullOrEmpty(skinName))
                            {
                                var imagePath = Path.Combine(configDir, "skins", $"{skinName}.png");
                                if (File.Exists(imagePath))
                                {
                                    var destPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skin.png");
                                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                                    File.Copy(imagePath, destPath, true);
                                    
                                    _settings.CustomSkinPath = destPath;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void EnsureOfflineSkinResourcePack(string instancePath)
    {
        if (string.IsNullOrEmpty(_settings.CustomSkinPath) || !File.Exists(_settings.CustomSkinPath))
            return;

        try
        {
            var rpDir = Path.Combine(instancePath, "resourcepacks");
            Directory.CreateDirectory(rpDir);
            var zipPath = Path.Combine(rpDir, "DeathClientSkin.zip");

            if (File.Exists(zipPath)) File.Delete(zipPath);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(_settings.CustomSkinPath, "assets/minecraft/textures/entity/steve.png");
                archive.CreateEntryFromFile(_settings.CustomSkinPath, "assets/minecraft/textures/entity/alex.png");
                archive.CreateEntryFromFile(_settings.CustomSkinPath, "assets/minecraft/textures/entity/player/wide/steve.png");
                archive.CreateEntryFromFile(_settings.CustomSkinPath, "assets/minecraft/textures/entity/player/slim/alex.png");

                if (!string.IsNullOrEmpty(_settings.CustomCapePath) && File.Exists(_settings.CustomCapePath))
                {
                    archive.CreateEntryFromFile(_settings.CustomCapePath, "assets/minecraft/textures/entity/cape.png");
                    archive.CreateEntryFromFile(_settings.CustomCapePath, "assets/minecraft/textures/entity/elytra.png");
                }

                var mcmeta = "{\"pack\":{\"pack_format\":34,\"description\":\"Death Client Auto-Skin\"}}";
                var entry = archive.CreateEntry("pack.mcmeta");
                using (var writer = new StreamWriter(entry.Open())) writer.Write(mcmeta);
            }

            var optionsPath = Path.Combine(instancePath, "options.txt");
            if (File.Exists(optionsPath))
            {
                var lines = File.ReadAllLines(optionsPath).ToList();
                var rpLineIdx = lines.FindIndex(l => l.StartsWith("resourcePacks:"));
                var packName = "file/DeathClientSkin.zip";
                
                if (rpLineIdx >= 0)
                {
                    var rpLine = lines[rpLineIdx];
                    if (!rpLine.Contains(packName))
                    {
                        var startIdx = rpLine.IndexOf('[');
                        var endIdx = rpLine.LastIndexOf(']');
                        if (startIdx >= 0 && endIdx > startIdx)
                        {
                            var packs = rpLine.Substring(startIdx + 1, endIdx - startIdx - 1).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim(' ', '"')).ToList();
                            packs.Insert(0, packName);
                            lines[rpLineIdx] = "resourcePacks:[" + string.Join(",", packs.Select(p => "\"" + p + "\"")) + "]";
                        }
                    }
                }
                else lines.Add($"resourcePacks:[\"{packName}\",\"vanilla\"]");
                File.WriteAllLines(optionsPath, lines);
            }
        }
        catch { }
    }

    private async Task LoadSkinAsync()
    {
        try
        {
            await Task.CompletedTask; // keep async signature
            UpdateCharacterPreview();
        }
        catch { }
    }

    private static void ApplyHoverMotion(Control? control)
    {
        if (control == null) return;
        control.Transitions = new Transitions
        {
            new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
        };
        control.PointerEntered += (s, e) =>
        {
            control.Opacity = 0.85;
            control.RenderTransform = TransformOperations.Parse("scale(1.025)");
        };
        control.PointerExited += (s, e) =>
        {
            control.Opacity = 1.0;
            control.RenderTransform = TransformOperations.Parse("scale(1.0)");
        };
    }

    private async Task ChangeSkinAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Minecraft Skin",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });
            if (files.Count > 0)
            {
                var skinPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skin.png");
                Directory.CreateDirectory(Path.GetDirectoryName(skinPath)!);
                await using var stream = await files[0].OpenReadAsync();
                await using var dest = File.Create(skinPath);
                await stream.CopyToAsync(dest);

                _settings.CustomSkinPath = skinPath;
                _settingsStore.Save(_settings);

                UpdateCharacterPreview();
                await DialogService.ShowInfoAsync(this, "Skin Applied", "Your skin has been updated and will be used when launching vanilla modpacks.");
            }
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Error", $"Failed to set skin: {ex.Message}");
        }
    }

    private async Task ChangeCapeAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Minecraft Cape",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });
            if (files.Count > 0)
            {
                var capePath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "cape.png");
                Directory.CreateDirectory(Path.GetDirectoryName(capePath)!);
                await using var stream = await files[0].OpenReadAsync();
                await using var dest = File.Create(capePath);
                await stream.CopyToAsync(dest);

                _settings.CustomCapePath = capePath;
                _settingsStore.Save(_settings);

                UpdateCharacterPreview();
                await DialogService.ShowInfoAsync(this, "Cape Applied", "Your cape has been updated and will be used when launching vanilla modpacks.");
            }
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Error", $"Failed to set cape: {ex.Message}");
        }
    }
    private static string CreateDownloadDestination(string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        return destinationPath;
    }
}

internal static class AvaloniaControlExtensions
{
    public static T With<T>(this T control, int row = -1, int column = -1, int columnSpan = 1) where T : Control
    {
        if (row >= 0) Grid.SetRow(control, row);
        if (column >= 0) Grid.SetColumn(control, column);
        if (columnSpan > 1) Grid.SetColumnSpan(control, columnSpan);
        return control;
    }

    public static T With<T>(this T control, Action<T> action) where T : Control
    {
        action(control);
        return control;
    }
}
