using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Version;
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
using System.Diagnostics;
using System.Windows.Input;
using System.Threading;

namespace OfflineMinecraftLauncher;

public class ModItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isEnabled;
    public string FileName { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (string.IsNullOrEmpty(FullPath)) return; // Init

            try
            {
                if (value && FileName.EndsWith(".disabled"))
                {
                    var newPath = FullPath.Substring(0, FullPath.Length - ".disabled".Length);
                    File.Move(FullPath, newPath);
                    FullPath = newPath;
                    FileName = Path.GetFileName(newPath);
                }
                else if (!value && !FileName.EndsWith(".disabled"))
                {
                    var newPath = FullPath + ".disabled";
                    File.Move(FullPath, newPath);
                    FullPath = newPath;
                    FileName = Path.GetFileName(newPath);
                }
            }
            catch { }
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FileName)));
        }
    }

    public void InitState(bool state) { _isEnabled = state; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}


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
    private readonly ObservableCollection<ModItem> _modItems = [];
    private readonly ObservableCollection<ModrinthProject> _searchResults = [];
    private static readonly string[] ProjectTypeOptions = ["Mod", "Modpack"];
    private static readonly string[] LoaderOptions = ["Any", "Vanilla", "Fabric", "Quilt", "Forge", "NeoForge"];
    private static readonly string[] ProfileLoaderOptions = ["Vanilla", "Fabric", "Quilt", "Forge", "NeoForge"];
    private static readonly string[] VersionCategoryOptions = ["Versions", "Snapshots", "Other sources"];
    private static readonly string[] SourceOptions = ["Modrinth", "CurseForge"];

    private TextBox usernameInput = null!;
    private ComboBox cbVersion = null!;
    private ComboBox minecraftVersion = null!;
    private Button downloadVersionButton = null!;
    private TextBox profileNameInput = null!;
    private TextBox profileGameDirInput = null!;
    private ComboBox profileLoaderCombo = null!;
    private Button createProfileButton = null!;
    private Button renameProfileButton = null!;
    private Button btnStart = null!;
    private CancellationTokenSource? _launchCts;
    private Button launchNavButton = null!;
    private Button profilesNavButton = null!;
    private Button modrinthNavButton = null!;
    private Button performanceNavButton = null!;
    private Button settingsNavButton = null!;
    private Button layoutNavButton = null!;
    private Button accountsNavButton = null!;
    private TextBlock activeProfileBadge = null!;
    private TextBlock activeContextLabel = null!;
    private TextBlock installModeLabel = null!;
    private Image characterImage = null!;
    private TextBlock statusLabel = null!;
    private TextBlock installDetailsLabel = null!;
    private ProgressBar pbFiles = null!;
    private ProgressBar pbProgress = null!;
    private TextBox modrinthSearchInput = null!;
    private ComboBox modrinthProjectTypeCombo = null!;
    private ComboBox modrinthLoaderCombo = null!;
    private ComboBox modrinthSourceCombo = null!;
    private Button modrinthSearchButton = null!;
    private TextBox modrinthVersionInput = null!;
    private ListBox modrinthResultsListBox = null!;
    private TextBlock modrinthDetailsBox = null!;
    private TextBlock modrinthResultsSummary = null!;
    private Button installSelectedButton = null!;
    private Button importMrpackButton = null!;
    private ListBox profileListBox = null!;
    private TextBlock profileInspectorTitle = null!;
    private TextBlock profileInspectorMeta = null!;
    private TextBlock profileInspectorPath = null!;
    private Button clearProfileButton = null!;
    private TextBlock heroInstanceLabel = null!;
    private TextBlock heroPerformanceLabel = null!;
    private TextBlock homeFpsStatValue = null!;
    private TextBlock homeRamStatValue = null!;
    private TextBlock performanceFpsStatValue = null!;
    private TextBlock performanceRamStatValue = null!;
    private TextBlock loadingLabel = null!;
    private Control launchSection = null!;
    private Control modrinthSection = null!;
    private Control profilesSection = null!;
    private Control performanceSection = null!;
    private Control settingsSection = null!;
    private Control layoutSection = null!;
    private Border? _homeStatusBar;
    public ProgressBar? PbProgress { get; set; }
    public TextBox? ModrinthSearchInput { get; set; }
    public System.Collections.Generic.Dictionary<string, object> Fields { get; } = new();
    private Border _instanceEditorOverlay = null!;
    private Border _accountsOverlay = null!;
    private StackPanel _accountsListPanel = new();
    private MinecraftAuthenticationService _authService = new();
    private Border _playOverlay = null!;
    private TextBlock _playOverlayIcon = null!;
    private TextBlock _playOverlayLabel = null!;
    // _notificationCard removed (notification replaced with Featured Servers section)
    // Quick Instance panel
    private ComboBox _quickVersionCombo = null!;
    private ComboBox _quickLoaderCombo = null!;
    private Button _quickInstallButton = null!;

    // Quick Mods panel
    private TextBox _quickModSearch = null!;
    private Button _quickModSearchButton = null!;
    private readonly ListBox _quickModResults = new();
    private readonly ObservableCollection<ModrinthProject> _quickSearchResults = [];

    private ComboBox instanceVersionCombo = null!;
    private ComboBox instanceCategoryCombo = null!;

    private string _playerUuid = string.Empty;
    private LauncherProfile? _selectedProfile;
    private CancellationTokenSource? _searchCancellation;
    private UserSettings _settings;
    private string _activeSection = "launch";
    // Responsive UI state
    private bool _isNarrowMode;
    private Border? _avatarGlass;
    private StackPanel? _avatarControls;
    private Grid? _avatarActions;
    private StackPanel? _mainContentStack;
    private readonly SemaphoreSlim _versionListSemaphore = new(1, 1);

    // Style revert system
    private LayoutStyle? _previousStyle;
    private CancellationTokenSource? _revertCts;
    private Border? _revertOverlay;
    private Control? _importedLayoutRoot;
    private Dictionary<string, Panel> _namedSlots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Control> _sectionSlotControls = new(StringComparer.OrdinalIgnoreCase);
    private static string RuntimeLayoutPath => Path.Combine(AppRuntime.DataDirectory, "death-client", "ui-layout-final.axaml.runtime");


    public MainWindow()
    {
        var initialPath = new MinecraftPath();
        initialPath.CreateDirs();
        _settingsStore = new UserSettingsStore(initialPath.BasePath);
        _settings = _settingsStore.Load();

        // Migrate legacy semicolon-delimited layout tokens to structured Style object
        _settings.MigrateLegacyLayout();
        if (string.IsNullOrWhiteSpace(_settings.ClientLayout))
        {
            // Migration happened or was already clean — persist
            _settingsStore.Save(_settings);
        }

        if (!string.IsNullOrEmpty(_settings.BaseMinecraftPath) && Directory.Exists(_settings.BaseMinecraftPath))
            _defaultMinecraftPath = new MinecraftPath(_settings.BaseMinecraftPath);
        else
            _defaultMinecraftPath = initialPath;

        _defaultMinecraftPath.CreateDirs();
        _profileStore = new LauncherProfileStore(_defaultMinecraftPath.BasePath);
        _defaultLauncher = CreateLauncher(_defaultMinecraftPath);
        ConfigureWindowChrome();
        EnsureFallbackControlsInitialized();

        this.SizeChanged += (s, e) => UpdateResponsiveLayout();
        Opened += async (_, _) => 
        {
            UpdateResponsiveLayout();
            try { await InitializeAsync(); } catch { }
        };

        // If there's an imported AXAML layout file, read its properties into Style
        ApplyLayoutFileProperties();

        // Build the C# UI — always uses the default C# UI, styled by settings.Style
        Content = BuildRoot();


        // Removed duplicated Opened handler
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
        EnsureFallbackControlsInitialized();
        var style = _settings.Style;
        var topNavigation = IsTopNavigationEnabled();
        var collapsedSidebar = IsSidebarCollapsed();
        var compact = style.CompactMode;
        var sidebarWidth = collapsedSidebar ? 72 : (compact ? 200 : (double.IsNaN(style.SidebarWidth) ? 240 : style.SidebarWidth));


        if (topNavigation)
        {
            return WrapWindowSurface(new Grid
            {
                Background = GetMainBackground(),
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    new Border {
                        Background = new SolidColorBrush(Color.FromArgb(8, 110, 91, 255)),
                        IsHitTestVisible = false,
                        ZIndex = 999
                    }.With(rowSpan: 2),
                    
                    new Canvas
                    {
                        Children =
                        {
                            new Border
                            {
                                Width = 500,
                                Height = 500,
                                CornerRadius = new CornerRadius(999),
                                Background = new RadialGradientBrush
                                {
                                    Center = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                    GradientOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                    RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                                    RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                                    GradientStops =
                                    {
                                        new GradientStop(GetAccentColor(20), 0),
                                        new GradientStop(GetAccentColor(0), 1)
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
                                        new GradientStop(GetAccentColor(15), 0),
                                        new GradientStop(GetAccentColor(0), 1)
                                    }
                                },
                                [Canvas.RightProperty] = -180d,
                                [Canvas.TopProperty] = 40d
                            }
                        }
                    }.With(row: 0),

                    // Accent Strip
                    new Border
                    {
                        Height = double.IsNaN(style.AccentStripHeight) ? 2 : style.AccentStripHeight,
                        Background = GetAccentStripBrush(),
                        VerticalAlignment = VerticalAlignment.Top,
                        ZIndex = 2000
                    }.With(rowSpan: 2),

                    TryPlaceInSection("SidebarHost", DetachFromParent(BuildTopNavigation())!)!.With(row: 0),
                    TryPlaceInSection("MainContentHost", DetachFromParent(BuildContent())!)!.With(row: 1),
                    BuildExternalPlayButtonHost(topNavigation: true)!,
                    DetachFromParent(_instanceEditorOverlay)!.With(row: 0, rowSpan: 2, columnSpan: 1),
                    DetachFromParent(_accountsOverlay)!.With(row: 0, rowSpan: 2, columnSpan: 2)
                }
            }, topNavigation: true);

        }

        var sidebarOnRight = string.Equals(style.SidebarSide, "right", StringComparison.OrdinalIgnoreCase);
        return WrapWindowSurface(new Grid
        {
            Background = GetMainBackground(),
            ColumnDefinitions = sidebarOnRight
                ? new ColumnDefinitions($"*,{sidebarWidth}")
                : new ColumnDefinitions($"{sidebarWidth},*"),
            Children =
            {
                new Canvas
                {
                    Children =
                    {
                        new Border
                        {
                            Width = 500,
                            Height = 500,
                            CornerRadius = new CornerRadius(999),
                            Background = new RadialGradientBrush
                            {
                                Center = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                GradientOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                                RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                                GradientStops =
                                {
                                    new GradientStop(Color.FromArgb(20, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 0),
                                    new GradientStop(Color.FromArgb(0, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 1)
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
                                    new GradientStop(Color.FromArgb(15, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 0),
                                    new GradientStop(Color.FromArgb(0, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 1)
                                }
                            },
                            [Canvas.RightProperty] = -180d,
                            [Canvas.TopProperty] = 40d
                        }
                    }
                },
                  sidebarOnRight ? TryPlaceInSection("MainContentHost", DetachFromParent(BuildContent())!)!.With(column: 0) : TryPlaceInSection("SidebarHost", DetachFromParent(BuildHeader())!)!,
                  sidebarOnRight ? TryPlaceInSection("SidebarHost", DetachFromParent(BuildHeader())!)!.With(column: 1) : TryPlaceInSection("MainContentHost", DetachFromParent(BuildContent())!)!.With(column: 1),
                                BuildExternalPlayButtonHost(topNavigation: false)!,
                DetachFromParent(_instanceEditorOverlay)!.With(columnSpan: 2),
                DetachFromParent(_accountsOverlay)!.With(columnSpan: 2)
            }
        }, topNavigation: false);
    }

    // --- Style token accessors (read from structured LayoutStyle) ---

    private bool IsTopNavigationEnabled() => string.Equals(_settings.Style.NavPosition, "top", StringComparison.OrdinalIgnoreCase);

    private bool IsSidebarCollapsed() => !IsTopNavigationEnabled() && _settings.Style.SidebarCollapsed;

    private bool IsSidebarOnRight() => string.Equals(_settings.Style.SidebarSide, "right", StringComparison.OrdinalIgnoreCase);

    private bool HasNamedHost(string hostName)
    {
        if (_namedSlots.ContainsKey(hostName)) return true;
        if (_importedLayoutRoot == null) return false;
        try { return _importedLayoutRoot.FindControl<Control>(hostName) != null; }
        catch { return false; }
    }

    private bool ShouldExternalizePlayButton() => _settings.Style.PlayButtonGlobal || HasNamedHost("PlayButtonHost");

    private Control BuildExternalPlayButtonHost(bool topNavigation)
    {
        if (!ShouldExternalizePlayButton())
            return new Border { IsVisible = false, Width = 0, Height = 0 };

        var defaultHost = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24),
            ZIndex = 2500,
            Child = DetachFromParent(_playOverlay)
        };

        if (topNavigation)
            Grid.SetRowSpan(defaultHost, 2);
        else
            Grid.SetColumnSpan(defaultHost, 2);

        return TryPlaceInSection("PlayButtonHost", defaultHost) ?? defaultHost;
    }

    private int GetStyleCornerRadius() =>
        string.Equals(_settings.Style.BorderStyle, "square", StringComparison.OrdinalIgnoreCase) ? 0 : _settings.Style.CornerRadius;

    private void ToggleSidebarCollapsed()
    {
        _settings.Style.SidebarCollapsed = !IsSidebarCollapsed();
        _settingsStore.Save(_settings);
        RebuildUiFromLayoutState(_activeSection);
    }

    private void RebuildUiFromLayoutState(string activeSection = "layout")
    {
        InvalidateUiCache();

        // Re-load named hosts/section mappings from imported layout so behavior is
        // identical before and after Keep/Revert and other style rebuilds.
        if (File.Exists(RuntimeLayoutPath))
            ApplyLayoutFileProperties();

        Content = BuildRoot();
        SetActiveSection(activeSection);
    }

    // --- Style change with 15-second revert window ---

    private void ApplyStyleWithRevert(Action<LayoutStyle> mutate)
    {
        // Snapshot current style before change
        _previousStyle = _settings.Style.Clone();
        _revertCts?.Cancel();
        _revertCts?.Dispose();

        // Apply the mutation
        mutate(_settings.Style);

        // If border style is square, force corner radius to 0
        if (string.Equals(_settings.Style.BorderStyle, "square", StringComparison.OrdinalIgnoreCase))
            _settings.Style.CornerRadius = 0;

        // Rebuild UI with new style
        RebuildUiFromLayoutState("layout");

        // Show revert overlay with 15s countdown
        ShowRevertOverlay();
    }

    private void ShowRevertOverlay()
    {
        _revertCts = new CancellationTokenSource();
        var ct = _revertCts.Token;
        var secondsLeft = 15;

        var countdownLabel = new TextBlock
        {
            Text = $"Keeping in {secondsLeft}s...",
            Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var keepBtn = new Button
        {
            Content = "✓ Keep Changes",
            Background = new SolidColorBrush(Color.Parse("#2A7A3A")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8),
            FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(0)
        };
        var revertBtn = new Button
        {
            Content = "↩ Revert",
            Background = new SolidColorBrush(Color.Parse("#7A2A2A")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8),
            FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(0)
        };

        keepBtn.Click += (_, _) => ConfirmStyleChange();
        revertBtn.Click += (_, _) => RevertStyleChange();

        _revertOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 14, 18, 28)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A3150")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 32),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Layout changed.",
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    countdownLabel,
                    keepBtn,
                    revertBtn
                }
            }
        };

        // Add overlay on top of current content
        if (Content is Control currentContent)
        {
            // Must detach from Window.Content BEFORE adding to overlay Grid
            Content = null;
            var overlay = new Grid
            {
                Children =
                {
                    currentContent,
                    _revertOverlay
                }
            };
            Content = overlay;
        }

        // Countdown timer
        _ = Task.Run(async () =>
        {
            while (secondsLeft > 0 && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
                secondsLeft--;
                Dispatcher.UIThread.Post(() =>
                {
                    if (!ct.IsCancellationRequested)
                        countdownLabel.Text = $"Keeping in {secondsLeft}s...";
                });
            }

            if (!ct.IsCancellationRequested)
                Dispatcher.UIThread.Post(ConfirmStyleChange);
        }, ct).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
    }

    private void ConfirmStyleChange()
    {
        _revertCts?.Cancel();
        _revertCts?.Dispose();
        _revertCts = null;
        _previousStyle = null;

        _settingsStore.Save(_settings);

        // Remove overlay, rebuild clean
        RebuildUiFromLayoutState("layout");
    }

    private void RevertStyleChange()
    {
        _revertCts?.Cancel();
        _revertCts?.Dispose();
        _revertCts = null;

        if (_previousStyle != null)
        {
            _settings.Style = _previousStyle;
            _previousStyle = null;
            _settingsStore.Save(_settings);
        }

        // Rebuild with reverted style
        RebuildUiFromLayoutState("layout");
    }

    private void ConfigureWindowChrome()
    {
        Title = "Aether Launcher";
        Name = "aether-launcher";
        Width = 1344;
        Height = 714;
        MinWidth = 1100;
        MinHeight = 610;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;
        SystemDecorations = SystemDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = 46;
        TransparencyLevelHint = new[] { 
            WindowTransparencyLevel.AcrylicBlur, 
            WindowTransparencyLevel.Mica, 
            WindowTransparencyLevel.Transparent 
        };

        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/deathclient-taskbar.png")));
        }
        catch
        {
            try
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/dc-icon.png")));
            }
            catch
            {
            }
        }
    }

    private Control WrapWindowSurface(Control content, bool topNavigation)
    {
        var style = _settings.Style;
        var shell = new Grid
        {
            ClipToBounds = false,
            Children = { content }
        };

        if (!topNavigation)
        {
            var floatingControls = BuildWindowControls();
            floatingControls.Margin = new Thickness(0, 16, 16, 0);
            floatingControls.HorizontalAlignment = HorizontalAlignment.Right;
            floatingControls.VerticalAlignment = VerticalAlignment.Top;
            shell.Children.Add(floatingControls);
        }

        var cr = GetStyleCornerRadius();
        
        var margin = style.WindowMargin;
        if (style.CompactMode) margin = Math.Max(0, margin - 4);
        
        var bg = !string.IsNullOrWhiteSpace(style.WindowBackground) ? style.WindowBackground : "#090C12";
        var border = !string.IsNullOrWhiteSpace(style.WindowBorderColor) ? style.WindowBorderColor : "#DC222A3F";

        return new Border
        {
            Margin = new Thickness(margin),
            CornerRadius = new CornerRadius(cr),
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.Parse(bg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Thickness(style.WindowBorderThickness),
            Child = shell
        };
    }


    private StackPanel BuildWindowControls()
    {
        var minimizeButton = CreateWindowControlButton("−", Color.Parse("#F4B63C"), () => WindowState = WindowState.Minimized);
        var maximizeButton = CreateWindowControlButton(WindowState == WindowState.Maximized ? "❐" : "□", Color.Parse("#4AD66D"), () =>
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            Content = BuildRoot();
            SetActiveSection(_activeSection);
        });
        var closeButton = CreateWindowControlButton("✕", Color.Parse("#FF5C70"), Close);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                DetachFromParent(accountsNavButton)!,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children = { minimizeButton, maximizeButton, closeButton }
                }
            }
        };
    }

    private Button CreateWindowControlButton(string glyph, Color color, Action onClick)
    {
        var button = new Button
        {
            Width = 14,
            Height = 14,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(999),
            Background = new SolidColorBrush(color),
            BorderThickness = new Thickness(0),
            Content = new TextBlock
            {
                Text = glyph,
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 12, 16, 24)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            }
        };

        button.Click += (_, _) => onClick();
        button.PointerEntered += (_, _) =>
        {
            if (button.Content is TextBlock label)
                label.Opacity = 1;
        };
        button.PointerExited += (_, _) =>
        {
            if (button.Content is TextBlock label)
                label.Opacity = 0;
        };

        return button;
    }

    private void AttachWindowDrag(Control control)
    {
        control.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
                return;

            try
            {
                BeginMoveDrag(e);
            }
            catch
            {
            }
        };
    }

    private Brush GetMainBackground()
    {
        var style = _settings.Style;

        // 1. If a specific WindowBackground hex color is set, prioritize it
        if (!string.IsNullOrWhiteSpace(style.WindowBackground))
        {
            try { return new SolidColorBrush(Color.Parse(style.WindowBackground)); } catch { }
        }

        // 2. Try Custom Background Image Path from style
        if (!string.IsNullOrWhiteSpace(style.BackgroundImagePath) && File.Exists(style.BackgroundImagePath))
        {
            try {
                var ovOp = double.IsNaN(style.BackgroundOverlayOpacity) ? 1.0 : style.BackgroundOverlayOpacity;
                return new ImageBrush(new Bitmap(style.BackgroundImagePath)) 
                { 
                    Stretch = Stretch.UniformToFill, 
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = ovOp == 1.0 ? style.BackgroundOpacity : 1.0 - ovOp
                };
            } catch { }
        }

        // 3. Try legacy custom_bg.png on disk
        var customBgPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBgPath))
        {
            try {
                return new ImageBrush(new Bitmap(customBgPath)) 
                { 
                    Stretch = Stretch.UniformToFill, 
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = style.BackgroundOpacity 
                };
            } catch { }
        }

        // 4. Default Bundled Resource
        try 
        {
            var asset = AssetLoader.Open(new Uri("avares://AetherLauncher/assets/launcher_background.png"));
            if (asset != null)
            {
                return new ImageBrush(new Bitmap(asset)) 
                { 
                    Stretch = Stretch.UniformToFill, 
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = style.BackgroundOpacity 
                };
            }
        } catch { }

        // 5. Final Fallback to Linear Gradient
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
        var style = _settings.Style;
        var collapsed = IsSidebarCollapsed();
        var sidebarOnRight = IsSidebarOnRight();
        var cr = GetStyleCornerRadius();
        var compact = style.CompactMode;
        var brand = collapsed
            ? (Control)new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.Parse("#121722")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "☠",
                    Foreground = Brushes.White,
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            }
            : new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Margin = new Thickness(4, 8, 4, 28),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Image
                    {
                        Source = new Bitmap(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/deathclient-taskbar.png"))),
                        Width = 28, Height = 28,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = style.TitleText ?? "AETHER LAUNCHER",
                        Foreground = Brushes.White,
                        FontSize = 18,
                        FontWeight = FontWeight.Black,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Inter, Segoe UI")
                    }
                }
            };

        launchNavButton = CreateNavButton("⌂", "Home", collapsed);
        launchNavButton.Click += (_, _) => SetActiveSection("home");
        profilesNavButton = CreateNavButton("▣", "Instances", collapsed);
        profilesNavButton.Click += (_, _) => SetActiveSection("instances");
        modrinthNavButton = CreateNavButton("⌕", "Mods", collapsed);
        modrinthNavButton.Click += (_, _) => SetActiveSection("modrinth");
        performanceNavButton = CreateNavButton("◔", "Performance", collapsed);
        performanceNavButton.Click += (_, _) => SetActiveSection("performance");
        settingsNavButton = CreateNavButton("⚙", "Settings", collapsed);
        settingsNavButton.Click += (_, _) => SetActiveSection("settings");
        layoutNavButton = CreateNavButton("▤", "Layout", collapsed);
        layoutNavButton.Click += (_, _) => SetActiveSection("layout");

        var edgeToggleButton = new Button
        {
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.Parse("#121722")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A3150")),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = sidebarOnRight ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = sidebarOnRight ? new Thickness(-11, 0, 0, 0) : new Thickness(0, 0, -11, 0),
            Content = new TextBlock
            {
                Text = sidebarOnRight
                    ? (collapsed ? "›" : "‹")
                    : (collapsed ? "‹" : "›"),
                Foreground = new SolidColorBrush(Color.Parse("#D5DAE5")),
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        edgeToggleButton.Click += (_, _) => ToggleSidebarCollapsed();

        var sbBg = !string.IsNullOrWhiteSpace(style.SidebarBackground) ? style.SidebarBackground : "#090C12";
        var sbBorder = !string.IsNullOrWhiteSpace(style.SidebarBorderColor) ? style.SidebarBorderColor : "#171B24";
        var sbPad = double.IsNaN(style.SidebarPadding) ? (collapsed ? new Thickness(10, 22, 10, 18) : new Thickness(18, 22, 18, 18)) : new Thickness(style.SidebarPadding);

        var sidebarBody = new Border
        {
            Background = new SolidColorBrush(Color.Parse(sbBg)),
            BorderBrush = new SolidColorBrush(Color.Parse(sbBorder)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = sbPad,
            Child = new StackPanel
            {
                Spacing = collapsed ? 10 : 12,
                Children =
                {
                    brand!,
                    DetachFromParent(launchNavButton)!,
                    DetachFromParent(profilesNavButton)!,
                    DetachFromParent(modrinthNavButton)!,
                    DetachFromParent(performanceNavButton)!,
                    DetachFromParent(settingsNavButton)!,
                    DetachFromParent(layoutNavButton)!
                }
            }
        };
        AttachWindowDrag(sidebarBody);

        return new Grid
        {
            ClipToBounds = false,
            Children =
            {
                sidebarBody,
                edgeToggleButton
            }
        };
    }

    private Control BuildTopNavigation()
    {
        launchNavButton = CreateNavButton("⌂", "Home");
        launchNavButton.Click += (_, _) => SetActiveSection("home");
        profilesNavButton = CreateNavButton("▣", "Instances");
        profilesNavButton.Click += (_, _) => SetActiveSection("instances");
        modrinthNavButton = CreateNavButton("⌕", "Mods");
        modrinthNavButton.Click += (_, _) => SetActiveSection("modrinth");
        performanceNavButton = CreateNavButton("◔", "Performance");
        performanceNavButton.Click += (_, _) => SetActiveSection("performance");
        settingsNavButton = CreateNavButton("⚙", "Settings");
        settingsNavButton.Click += (_, _) => SetActiveSection("settings");
        layoutNavButton = CreateNavButton("▤", "Layout");
        layoutNavButton.Click += (_, _) => SetActiveSection("layout");

        ApplyHoverMotion(launchNavButton);
        ApplyHoverMotion(profilesNavButton);
        ApplyHoverMotion(modrinthNavButton);
        ApplyHoverMotion(performanceNavButton);
        ApplyHoverMotion(settingsNavButton);
        ApplyHoverMotion(layoutNavButton);

        foreach (var button in new[] { launchNavButton, profilesNavButton, modrinthNavButton, performanceNavButton, settingsNavButton, layoutNavButton })
        {
            if (button == null) continue;
            button.Height = 40;
            button.MinWidth = 100;
        }

        var brandBlock = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Image
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/deathclient-taskbar.png"))),
                    Width = 28, Height = 28,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = "AETHER LAUNCHER",
                    Foreground = Brushes.White,
                    FontSize = 18,
                    FontWeight = FontWeight.Black,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Inter, Segoe UI")
                }
            }
        };

        var centeredTabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                DetachFromParent(launchNavButton)!,
                DetachFromParent(profilesNavButton)!,
                DetachFromParent(modrinthNavButton)!,
                DetachFromParent(performanceNavButton)!,
                DetachFromParent(settingsNavButton)!,
                DetachFromParent(layoutNavButton)!
            }
        };

        var topNavigationBar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(210, 9, 12, 18)),
            BorderBrush = new SolidColorBrush(Color.Parse("#171B24")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(22, 10, 22, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("200,*,Auto"),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    brandBlock.With(column: 0),
                    centeredTabs.With(column: 1),
                    BuildWindowControls().With(column: 2)
                }
            }
        };
        AttachWindowDrag(topNavigationBar);
        return topNavigationBar;
    }

    private static T? DetachFromParent<T>(T? control) where T : Control
    {
        if (control == null) return null;
        if (control.Parent is Panel panel)
            panel.Children.Remove(control);
        else if (control.Parent is ContentControl cc)
            cc.Content = null;
        else if (control.Parent is Decorator d)
            d.Child = null;
        else if (control.Parent is Viewbox vb)
            vb.Child = null;
        return control;
    }

    private void EnsureFallbackControlsInitialized()
    {
        if (accountsNavButton == null)
        {
            accountsNavButton = new Button
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 26, 31, 46)),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(20, 10),
                MinWidth = 160,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeight.Bold,
                ZIndex = 50
            };
            accountsNavButton.Click += (_, _) => ShowAccountsOverlay();
            ApplyHoverMotion(accountsNavButton);
            UpdateAccountsButtonText();
        }

        usernameInput ??= CreateTextBox();
        usernameInput.Watermark = "Player name";
        usernameInput.TextChanged -= UsernameInput_TextChanged;
        usernameInput.TextChanged += UsernameInput_TextChanged;

        cbVersion ??= CreateComboBox(_versionItems);
        cbVersion.SelectionChanged -= CbVersion_SelectionChanged;
        cbVersion.SelectionChanged += CbVersion_SelectionChanged;

        minecraftVersion ??= CreateComboBox(VersionCategoryOptions);
        minecraftVersion.SelectionChanged -= MinecraftVersion_SelectionChanged;
        minecraftVersion.SelectionChanged += MinecraftVersion_SelectionChanged;

        downloadVersionButton ??= CreateSecondaryButton("Download Version");
        downloadVersionButton.Click -= DownloadVersionButton_Click;
        downloadVersionButton.Click += DownloadVersionButton_Click;

        profileNameInput ??= CreateTextBox();
        profileNameInput.Watermark = "Profile name";

        profileGameDirInput ??= CreateTextBox();
        profileGameDirInput.Watermark = "Custom game directory (optional)";

        instanceVersionCombo ??= CreateComboBox(_versionItems);
        instanceCategoryCombo ??= CreateComboBox(VersionCategoryOptions);
        instanceCategoryCombo.SelectedItem = "Versions";
        instanceCategoryCombo.SelectionChanged += (_, _) => _ = ListVersionsAsync(instanceCategoryCombo.SelectedItem?.ToString() ?? "Versions");
        _ = ListVersionsAsync("Versions");

        profileLoaderCombo ??= CreateComboBox(ProfileLoaderOptions);

        if (createProfileButton is null)
        {
            createProfileButton = CreatePrimaryButton("Create Profile", "#38D6C4", Colors.Black);
            createProfileButton.Click += async (_, _) => await CreateProfileAsync();
        }

        renameProfileButton ??= CreateSecondaryButton("Rename Profile");
        renameProfileButton.Click -= RenameProfileButton_Click;
        renameProfileButton.Click += RenameProfileButton_Click;

        if (btnStart is null)
        {
            btnStart = CreatePrimaryButton("▶ Play", "#6E5BFF", Colors.White);
            btnStart.Click += async (_, _) => 
            {
                if (_launchCts != null)
                {
                    _launchCts.Cancel();
                    btnStart.IsEnabled = false;
                    btnStart.Content = "Cancelling...";
                }
                else
                {
                    await LaunchAsync();
                }
            };
        }

        activeProfileBadge ??= CreateStatusTextBlock();
        activeContextLabel ??= CreateMutedTextBlock();
        installModeLabel ??= CreateStatusTextBlock();

        characterImage ??= new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        statusLabel ??= CreateStatusTextBlock();
        installDetailsLabel ??= CreateMutedTextBlock();
        pbFiles ??= new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };
        pbProgress ??= new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };

        modrinthSearchInput ??= CreateTextBox();
        modrinthProjectTypeCombo ??= CreateComboBox(ProjectTypeOptions);
        modrinthLoaderCombo ??= CreateComboBox(LoaderOptions);
        modrinthSourceCombo ??= CreateComboBox(SourceOptions);

        if (modrinthSearchButton is null)
        {
            modrinthSearchButton = CreatePrimaryButton("Search", "#6E5BFF", Colors.White);
            modrinthSearchButton.Click += async (_, _) => await SearchModrinthAsync();
        }

        modrinthVersionInput ??= CreateTextBox();
        modrinthResultsListBox ??= new ListBox { ItemsSource = _searchResults };
        modrinthResultsListBox.SelectionChanged -= ModrinthResultsListBox_SelectionChanged;
        modrinthResultsListBox.SelectionChanged += ModrinthResultsListBox_SelectionChanged;

        modrinthDetailsBox ??= CreateMutedTextBlock();
        modrinthDetailsBox.TextWrapping = TextWrapping.Wrap;
        modrinthResultsSummary ??= CreateMutedTextBlock();

        if (installSelectedButton is null)
        {
            installSelectedButton = CreatePrimaryButton("Install Selected", "#38D6C4", Colors.Black);
            installSelectedButton.Click += async (_, _) => await InstallSelectedAsync();
        }

        importMrpackButton ??= CreateSecondaryButton("Import .mrpack");
        importMrpackButton.Click -= ImportMrpackButton_Click;
        importMrpackButton.Click += ImportMrpackButton_Click;

        profileListBox ??= new ListBox { ItemsSource = _profileItems };
        profileListBox.SelectionChanged -= ProfileListBox_SelectionChanged;
        profileListBox.SelectionChanged += ProfileListBox_SelectionChanged;

        profileInspectorTitle ??= CreateStatusTextBlock();
        profileInspectorMeta ??= CreateMutedTextBlock();
        profileInspectorMeta.TextWrapping = TextWrapping.Wrap;
        profileInspectorPath ??= CreateMutedTextBlock();
        profileInspectorPath.TextWrapping = TextWrapping.Wrap;

        clearProfileButton ??= CreateSecondaryButton("Delete Profile");
        clearProfileButton.Click -= ClearProfileButton_Click;
        clearProfileButton.Click += ClearProfileButton_Click;

        heroInstanceLabel ??= new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeight.Black,
            TextWrapping = TextWrapping.Wrap
        };
        heroPerformanceLabel ??= CreateMutedTextBlock();
        homeFpsStatValue ??= new TextBlock();
        homeRamStatValue ??= new TextBlock();
        performanceFpsStatValue ??= new TextBlock();
        performanceRamStatValue ??= new TextBlock();
        loadingLabel ??= CreateMutedTextBlock();

        _quickVersionCombo ??= CreateComboBox(_versionItems);
        _quickLoaderCombo ??= CreateComboBox(ProfileLoaderOptions);

        _quickInstallButton ??= CreatePrimaryButton("Quick Install", "#38D6C4", Colors.Black);
        _quickInstallButton.Click -= QuickInstallButton_Click;
        _quickInstallButton.Click += QuickInstallButton_Click;

        _quickModSearch ??= CreateTextBox();
        _quickModSearch.Watermark = "Search mods";

        _quickModSearchButton ??= CreateSecondaryButton("Quick Search");
        _quickModSearchButton.Click -= QuickModSearchButton_Click;
        _quickModSearchButton.Click += QuickModSearchButton_Click;

        _playOverlay ??= new Border();
        _playOverlayIcon ??= new TextBlock();
        _playOverlayLabel ??= new TextBlock();

        _quickModResults.ItemsSource = _quickSearchResults;
        
        // Use a more robust detachment and re-attachment for the play button
        var playStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var icon = DetachFromParent(_playOverlayIcon);
        var label = DetachFromParent(_playOverlayLabel);
        if (icon != null) playStack.Children.Add(icon);
        if (label != null) playStack.Children.Add(label);
        
        var accentColor = Color.Parse(_settings.AccentColor);
        _playOverlay.Background = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B));
        _playOverlay.BorderBrush = new SolidColorBrush(accentColor);
        _playOverlay.BorderThickness = new Thickness(1);
        _playOverlay.CornerRadius = new CornerRadius(20);
        _playOverlay.Padding = new Thickness(24, 12);
        
        _playOverlayIcon.Foreground = new SolidColorBrush(accentColor);
        _playOverlayIcon.FontSize = 24;
        _playOverlayIcon.Text = "▶";
        
        _playOverlayLabel.Foreground = Brushes.White;
        _playOverlayLabel.FontSize = 18;
        _playOverlayLabel.FontWeight = FontWeight.Bold;
        _playOverlayLabel.Margin = new Thickness(12, 0, 0, 0);
        _playOverlayLabel.Text = "PLAY";

        _playOverlay.Child = playStack;
        _playOverlay.PointerPressed -= PlayOverlay_PointerPressed;
        _playOverlay.PointerPressed += PlayOverlay_PointerPressed;
        _playOverlay.Cursor = new Cursor(StandardCursorType.Hand);

        _instanceEditorOverlay ??= BuildInstanceEditorOverlay();
        _accountsListPanel ??= new StackPanel();
        _accountsOverlay ??= BuildAccountsOverlay();
        PbProgress = pbProgress;
        ModrinthSearchInput = modrinthSearchInput;
        UpdateSelectedProjectDetails();
    }

    private Border BuildInstanceEditorOverlay()
    {
        var cancelButton = CreateSecondaryButton("Cancel");
        cancelButton.Click += (_, _) => _instanceEditorOverlay.IsVisible = false;

        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(170, 5, 8, 16)),
            Padding = new Thickness(32),
            Child = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 460,
                Children =
                {
                    CreateGlassPanel(new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Edit Instance",
                                Foreground = Brushes.White,
                                FontSize = 22,
                                FontWeight = FontWeight.Bold
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Name"),
                                    DetachFromParent(profileNameInput)!
                                }
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Loader"),
                                    DetachFromParent(profileLoaderCombo)!
                                }
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Game Version"),
                                    new Grid
                                    {
                                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                                        ColumnSpacing = 8,
                                        Children =
                                        {
                                            DetachFromParent(instanceCategoryCombo)!.With(column: 0),
                                            DetachFromParent(instanceVersionCombo)!.With(column: 1)
                                        }
                                    }
                                }
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Game Directory Override"),
                                    DetachFromParent(profileGameDirInput)!
                                }
                            },
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                                ColumnSpacing = 10,
                                Children =
                                {
                                    DetachFromParent(createProfileButton)!.With(column: 0),
                                    DetachFromParent(renameProfileButton)!.With(column: 1),
                                    cancelButton!.With(column: 2)
                                }
                            }
                        }
                    }, padding: new Thickness(24), margin: new Thickness(0))
                }
            }
        };
    }

    private void ShowAccountsOverlay()
    {
        RefreshAccountsList();
        _accountsOverlay.IsVisible = true;
        if (accountsNavButton != null) accountsNavButton.IsVisible = false;
    }

    private bool _isAuthenticating;
    private void RefreshAccountsList()
    {
        _accountsListPanel.Children.Clear();
        foreach (var account in _settings.Accounts.ToList())
        {
            var isSelected = account.Id == _settings.SelectedAccountId;

            var avatar = new TextBlock
            {
                Text = "🧑",
                FontSize = 24,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            var nameBlock = new TextBlock
            {
                Text = account.Username,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                FontSize = 14
            };

            var typeColor = account.Provider == "microsoft" ? "#5B80FF" : "#A0A8B8";
            var typeLabel = account.Provider == "microsoft" ? "Microsoft" : "Offline";

            var typeBlock = new TextBlock
            {
                Text = typeLabel,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse(typeColor))
            };

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameBlock, typeBlock } };

            var removeBtn = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#FF5B5B")),
                IsVisible = false 
            };
            removeBtn.Click += (_, _) =>
            {
                _settings.Accounts.Remove(account);
                if (_settings.SelectedAccountId == account.Id)
                {
                    _settings.SelectedAccountId = string.Empty;
                    usernameInput.Text = string.Empty;
                    UsernameInput_TextChanged();
                }
                _settingsStore.Save(_settings);
                RefreshAccountsList();
                UpdateAccountsButtonText();
            };

            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children = { avatar.With(column: 0), textStack.With(column: 1), removeBtn.With(column: 2) }
            };

            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1F2E")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                BorderBrush = isSelected ? new SolidColorBrush(Color.Parse("#38D6C4")) : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 2 : 0),
                Child = rowGrid
            };

            card.PointerEntered += (_, _) => { removeBtn.IsVisible = true; card.Background = new SolidColorBrush(Color.Parse("#22283A")); };
            card.PointerExited += (_, _) => { removeBtn.IsVisible = false; card.Background = new SolidColorBrush(Color.Parse("#1A1F2E")); };

             card.PointerPressed += (_, _) =>
            {
                _settings.SelectedAccountId = account.Id;
                usernameInput.Text = account.Username;
                UsernameInput_TextChanged();
                _settingsStore.Save(_settings);
                RefreshAccountsList();
                UpdateAccountsButtonText();
                _accountsOverlay.IsVisible = false;
                if (accountsNavButton != null) accountsNavButton.IsVisible = true;
            };

            _accountsListPanel.Children.Add(card);
        }
    }

    private async Task AddOfflineAccountAsync()
    {
        var username = await DialogService.ShowTextInputAsync(this, "Add Offline Account", "Enter your username:");
        if (string.IsNullOrWhiteSpace(username)) return;

        var acc = new LauncherAccount { Provider = "offline", Username = username.Trim(), DisplayName = username.Trim() };
        _settings.Accounts.Add(acc);
        _settings.SelectedAccountId = acc.Id;
        usernameInput.Text = acc.Username;
        UsernameInput_TextChanged();
        _settingsStore.Save(_settings);
        UpdateAccountsButtonText();
        RefreshAccountsList();
    }

    private LauncherAccount? GetSelectedAccount()
        => _settings.Accounts.FirstOrDefault(a => a.Id == _settings.SelectedAccountId);

    private string GetActiveUsername()
    {
        var selectedAccount = GetSelectedAccount();
        if (selectedAccount != null && !string.IsNullOrWhiteSpace(selectedAccount.Username))
            return selectedAccount.Username;

        return usernameInput.Text?.Trim() ?? string.Empty;
    }

    private bool IsUsingMicrosoftAccount()
        => string.Equals(GetSelectedAccount()?.Provider, "microsoft", StringComparison.OrdinalIgnoreCase);

    private bool HasManualSkinOverride()
    {
        var manualSkinPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skin.png");
        return string.Equals(_settings.CustomSkinPath, manualSkinPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(manualSkinPath);
    }

    private bool HasManualCapeOverride()
    {
        var manualCapePath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "cape.png");
        return string.Equals(_settings.CustomCapePath, manualCapePath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(manualCapePath);
    }

    private async Task<MSession> BuildLaunchSessionAsync(CancellationToken cancellationToken)
    {
        var selectedAccount = GetSelectedAccount();
        if (selectedAccount != null && string.Equals(selectedAccount.Provider, "microsoft", StringComparison.OrdinalIgnoreCase))
        {
            if (selectedAccount.IsExpired)
            {
                var refreshed = await TryRefreshAccountAsync(selectedAccount);
                if (!refreshed)
                    throw new InvalidOperationException("The selected Microsoft account could not be refreshed. Sign in again.");

                selectedAccount = GetSelectedAccount();
            }

            if (selectedAccount == null || string.IsNullOrWhiteSpace(selectedAccount.MinecraftAccessToken))
                throw new InvalidOperationException("The selected Microsoft account is missing a Minecraft access token. Sign in again.");

            if (string.IsNullOrWhiteSpace(selectedAccount.Uuid))
                throw new InvalidOperationException("The selected Microsoft account is missing the Minecraft profile UUID.");

            return new MSession
            {
                Username = selectedAccount.Username,
                UUID = selectedAccount.Uuid,
                AccessToken = selectedAccount.MinecraftAccessToken,
                Xuid = selectedAccount.Xuid,
                UserType = "msa"
            };
        }

        var username = GetActiveUsername();
        var session = MSession.CreateOfflineSession(username);
        session.UUID = string.IsNullOrWhiteSpace(_playerUuid)
            ? Character.GenerateUuidFromUsername(username)
            : _playerUuid;
        return session;
    }

    private async Task<bool> TryRefreshAccountAsync(LauncherAccount account)
    {
        if (account.Provider != "microsoft" || !account.IsExpired) return true;

        try
        {
            var clientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId) ? "00000000402b5328" : _settings.MicrosoftClientId;
            LauncherLog.Info($"[Microsoft Auth] Refreshing token for {account.Username}...");
            
            var refreshed = await _authService.RefreshMinecraftAccountAsync(clientId, account, CancellationToken.None);
            
            // Update existing account in settings
            var idx = _settings.Accounts.FindIndex(a => a.Id == account.Id);
            if (idx != -1)
            {
                _settings.Accounts[idx] = refreshed;
                _settingsStore.Save(_settings);
                return true;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Info($"[Microsoft Auth] Refresh failed for {account.Username}: {ex.Message}");
        }
        return false;
    }

    private async Task AddMicrosoftAccountAsync()
    {
        if (_isAuthenticating) return;
        _isAuthenticating = true;

        var clientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId) ? "00000000402b5328" : _settings.MicrosoftClientId;
        using var cts = new CancellationTokenSource();
        
        try
        {
            LauncherLog.Info("[Microsoft Auth] Starting device code login...");
            var session = await _authService.BeginDeviceLoginAsync(clientId, cts.Token);

            // Open browser and show premium dialog
            Process.Start(new ProcessStartInfo { FileName = session.VerificationUri, UseShellExecute = true });
            
            var dialogTask = DialogService.ShowMicrosoftAuthDialogAsync(this, session.UserCode, session.VerificationUri, cts);
            var pollTask = _authService.CompleteDeviceLoginAsync(clientId, session, cts.Token);

            var completedTask = await Task.WhenAny(dialogTask, pollTask);

            if (completedTask == pollTask)
            {
                var account = await pollTask;
                var existing = _settings.Accounts.FirstOrDefault(a => a.Uuid == account.Uuid && a.Provider == "microsoft");
                if (existing != null) _settings.Accounts.Remove(existing);

                _settings.Accounts.Add(account);
                _settings.SelectedAccountId = account.Id;
                usernameInput.Text = account.Username;
                UsernameInput_TextChanged();
                _settingsStore.Save(_settings);
                
                LauncherLog.Info($"[Microsoft Auth] Successfully logged in as {account.Username}");
                UpdateAccountsButtonText();
                RefreshAccountsList();
            }
            else
            {
                LauncherLog.Info("[Microsoft Auth] Login cancelled by user.");
            }
        }
        catch (OperationCanceledException)
        {
            LauncherLog.Info("[Microsoft Auth] Login timed out or cancelled.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Authentication Failed", ex.Message);
        }
        finally
        {
            _isAuthenticating = false;
        }
    }



    private Border BuildAccountsOverlay()
    {
        var closeButton = new Button
        {
            Content = "×",
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            FontSize = 24,
            Padding = new Thickness(8, 0)
        };
        closeButton.Click += (_, _) => 
        {
            _accountsOverlay.IsVisible = false;
            if (accountsNavButton != null)
            {
                accountsNavButton.IsVisible = true;
                accountsNavButton.Opacity = 1.0;
                accountsNavButton.RenderTransform = TransformOperations.Parse("scale(1.0)");
            }
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = "Accounts", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center },
                closeButton.With(column: 1)
            }
        };

        var addMicrosoftBtn = CreatePrimaryButton("Add Microsoft Account", "#5B80FF", Colors.White);
        addMicrosoftBtn.Click += async (_, _) => await AddMicrosoftAccountAsync();

        var addOfflineBtn = CreateSecondaryButton("Add Offline");
        addOfflineBtn.Click += async (_, _) => await AddOfflineAccountAsync();

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            Children =
            {
                addMicrosoftBtn.With(column: 0),
                addOfflineBtn.With(column: 1)
            }
        };

        var style = _settings.Style;
        var bgStr = !string.IsNullOrWhiteSpace(style.AccountsOverlayBackground) ? style.AccountsOverlayBackground : "#F0090C12";
        var brdStr = !string.IsNullOrWhiteSpace(style.AccountsOverlayBorderColor) ? style.AccountsOverlayBorderColor : "#641E283C";
        var rad = double.IsNaN(style.AccountsOverlayCornerRadius) ? 0 : style.AccountsOverlayCornerRadius;
        var thick = double.IsNaN(style.AccountsOverlayBorderThickness) ? 1 : style.AccountsOverlayBorderThickness;

        var panel = new Border
        {
            Width = 380,
            Background = new SolidColorBrush(Color.Parse(bgStr)),
            BorderBrush = new SolidColorBrush(Color.Parse(brdStr)),
            BorderThickness = new Thickness(thick, 0, 0, 0),
            CornerRadius = new CornerRadius(rad, 0, 0, rad),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                Children =
                {
                    header.With(row: 0),
                    new ScrollViewer
                    {
                        Margin = new Thickness(0, 20),
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                        Content = _accountsListPanel.With(sp => sp.Spacing = 8)
                    }.With(row: 1),
                    footer.With(row: 2)
                }
            }
        };

        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            ZIndex = 100,
            Child = panel
        };
    }

    private void UpdateAccountsButtonText()
    {
        if (accountsNavButton != null)
        {
            var activeName = GetSelectedAccount()?.Username;
            if (string.IsNullOrWhiteSpace(activeName))
                activeName = string.IsNullOrWhiteSpace(usernameInput.Text) ? _settings.Username : usernameInput.Text;
            if (string.IsNullOrWhiteSpace(activeName))
                activeName = "Accounts";

            // Make it look premium
            var fg = !string.IsNullOrWhiteSpace(_settings.Style.NavButtonForeground) ? _settings.Style.NavButtonForeground : "#A4A8B1";
            var accent = !string.IsNullOrWhiteSpace(_settings.Style.AccentColor) ? _settings.Style.AccentColor! : (!string.IsNullOrWhiteSpace(_settings.AccentColor) ? _settings.AccentColor : "#6E5BFF");
            
            accountsNavButton.Content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(40, Color.Parse(accent).R, Color.Parse(accent).G, Color.Parse(accent).B)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(6),
                        Child = new TextBlock
                        {
                            Text = "🧑",
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Color.Parse(accent)),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = activeName,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse(fg)),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                }
            };
            
            // Add transitions if not already added
            if (accountsNavButton.Transitions == null)
            {
                accountsNavButton.Transitions = new Transitions
                {
                    new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
                    new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
                };
            }
        }
    }

    private Control BuildFeaturedServersSection()
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 16, 0, 12),
            Children =
            {
                new Border
                {
                    Width = 3, Height = 16,
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(Color.Parse(_settings.AccentColor)),
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = "FEATURED SERVERS",
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#8E96A8")),
                    LetterSpacing = 1.5,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        var breakpointCard = BuildServerCard(
            bgAsset: "avares://AetherLauncher/assets/launcher_background.png",
            logoAsset: "avares://AetherLauncher/assets/breakpoint-logo.png",
            serverName: "BreakPoint MC",
            tagLine: "⭐ FEATURED",
            description: "Cracked Server. Optimised for Aether.",
            ip: "breakpoint.mcsrv.net",
            accentHex: "#7E6AFF",
            isFeatured: true
        );

        var hypixelCard = BuildServerCard(
            bgAsset: "avares://AetherLauncher/assets/hypixel_card_bg.png",
            serverName: "Hypixel",
            tagLine: "MINI-GAMES",
            description: "The world's largest server.",
            ip: "mc.hypixel.net",
            accentHex: "#F4C430",
            isFeatured: false
        );

        var donutCard = BuildServerCard(
            bgAsset: "avares://AetherLauncher/assets/donut_smp_card_bg.png",
            serverName: "Donut SMP",
            tagLine: "SURVIVAL",
            description: "Community survival SMP.",
            ip: "play.donutsmp.net",
            accentHex: "#FF8C42",
            isFeatured: false
        );

        var cardsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3.5*, *, *"),
            ColumnSpacing = 10,
            Height = 135,
            Children =
            {
                breakpointCard,
                hypixelCard.With(column: 1),
                donutCard.With(column: 2)
            }
        };

        return new StackPanel { Children = { header, cardsGrid } };
    }

    private Border BuildServerCard(string bgAsset, string serverName, string tagLine, string description, string ip, string accentHex, bool isFeatured, string? logoAsset = null)
    {
        ImageBrush? bgBrush = null;
        try
        {
            var bmp = new Bitmap(AssetLoader.Open(new Uri(bgAsset)));
            bgBrush = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
        }
        catch { }

        // Logo overlay (shows when NOT hovered)
        var logoContent = new Panel();
        if (!string.IsNullOrEmpty(logoAsset))
        {
            try
            {
                var logoBmp = new Bitmap(AssetLoader.Open(new Uri(logoAsset)));
                logoContent.Children.Add(new Image
                {
                    Source = logoBmp,
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Transitions = new Transitions { new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) } }
                });
            }
            catch { }
        }

        // Overlay that shows on hover
        var hoverOverlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(230, 9, 12, 20), 0),
                    new GradientStop(Color.FromArgb(140, 9, 12, 20), 0.6),
                    new GradientStop(Color.FromArgb(0, 9, 12, 20), 1)
                }
            },
            Opacity = 0,
            Transitions = new Transitions
            {
                new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }
            },
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 14, 14),
                Spacing = 4,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(40, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(120, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new TextBlock
                        {
                            Text = tagLine,
                            FontSize = 11,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(accentHex)),
                            LetterSpacing = 1
                        }
                    },
                    new TextBlock
                    {
                        Text = serverName,
                        FontSize = isFeatured ? 20 : 16,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = description,
                        FontSize = 12.5,
                        Foreground = new SolidColorBrush(Color.Parse("#A0AABB")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = $"Copy IP: {ip}",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.Parse(accentHex)),
                        Background = Brushes.Transparent,
                        Padding = new Thickness(0, 2, 0, 0),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Command = new RelayCommand(() => CopyServerIpToClipboard(ip))
                    }
                }
            }
        };

        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Background = bgBrush != null ? bgBrush : new SolidColorBrush(Color.Parse("#1A1F2E")),
            BorderBrush = new SolidColorBrush(Color.FromArgb(isFeatured ? (byte)80 : (byte)40, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B)),
            BorderThickness = new Thickness(1),
            BoxShadow = isFeatured ? new BoxShadows(new BoxShadow
            {
                Blur = 20,
                Color = Color.FromArgb(100, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B),
                OffsetX = 0,
                OffsetY = 0
            }) : default,
            Child = new Grid { Children = { logoContent, hoverOverlay } }
        };

        card.PointerEntered += (_, _) => { hoverOverlay.Opacity = 1; logoContent.Opacity = 0; };
        card.PointerExited += (_, _) => { hoverOverlay.Opacity = 0; logoContent.Opacity = 1; };

        return card;
    }

    private async void CopyServerIpToClipboard(string ip)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null) return;
        await topLevel.Clipboard.SetTextAsync(ip);
    }

    private async void CopyToClipboard(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        await topLevel.Clipboard!.SetTextAsync(text);
    }

    private void EnsureSectionsBuilt()
    {
        EnsureFallbackControlsInitialized();
        launchSection ??= BuildLaunchDeck();
        modrinthSection ??= BuildModrinthDeck();
        profilesSection ??= BuildProfilesDeck();
        performanceSection ??= BuildPerformanceDeck();
        settingsSection ??= BuildSettingsDeck();
        layoutSection ??= BuildLayoutDeck();

        launchSection.IsVisible = _activeSection == "launch";
        modrinthSection.IsVisible = _activeSection == "modrinth";
        profilesSection.IsVisible = _activeSection == "profiles";
        performanceSection.IsVisible = _activeSection == "performance";
        settingsSection.IsVisible = _activeSection == "settings";
        layoutSection.IsVisible = _activeSection == "layout";
    }

    private void InvalidateUiCache()
    {
        // Sections
        launchSection = null!;
        modrinthSection = null!;
        profilesSection = null!;
        performanceSection = null!;
        settingsSection = null!;
        layoutSection = null!;
        
        // Overlays
        _instanceEditorOverlay = null!;
        _accountsOverlay = null!;
        _namedSlots = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        _sectionSlotControls.Clear();
        _playOverlay = new Border();
        
        // Navigation
        launchNavButton = null!;
        profilesNavButton = null!;
        modrinthNavButton = null!;
        performanceNavButton = null!;
        settingsNavButton = null!;
        layoutNavButton = null!;
        accountsNavButton = null!;
        
        // Shared Labels & Fields
        heroInstanceLabel = null!;
        heroPerformanceLabel = null!;
        loadingLabel = null!;
        statusLabel = null!;
        installDetailsLabel = null!;
        activeProfileBadge = null!;
        activeContextLabel = null!;
        usernameInput = null!;
        
        // Progress & Stats
        pbFiles = null!;
        pbProgress = null!;
        homeFpsStatValue = null!;
        homeRamStatValue = null!;
        performanceFpsStatValue = null!;
        performanceRamStatValue = null!;
        
        // Input Controls
        cbVersion = null!;
        minecraftVersion = null!;
        downloadVersionButton = null!;
        profileNameInput = null!;
        profileGameDirInput = null!;
        profileLoaderCombo = null!;
        instanceVersionCombo = null!;
        instanceCategoryCombo = null!;
        _quickVersionCombo = null!;
        _quickLoaderCombo = null!;
        _quickInstallButton = null!;
        _quickModSearch = null!;
        _quickModSearchButton = null!;
        _accountsListPanel = null!;
        _playOverlay = null!;
        _playOverlayIcon = null!;
        _playOverlayLabel = null!;
        
        // Missed Premium UI Fields
        characterImage = null!;
        activeProfileBadge = null!;
        activeContextLabel = null!;
        installModeLabel = null!;
        btnStart = null!;
        profileListBox = null!;
        modrinthResultsListBox = null!;
        modrinthDetailsBox = null!;
        modrinthResultsSummary = null!;
        installSelectedButton = null!;
        importMrpackButton = null!;
        profileInspectorTitle = null!;
        profileInspectorMeta = null!;
        profileInspectorPath = null!;
        clearProfileButton = null!;
        modrinthSearchInput = null!;
        modrinthProjectTypeCombo = null!;
        modrinthLoaderCombo = null!;
        modrinthSourceCombo = null!;
        modrinthSearchButton = null!;
        modrinthVersionInput = null!;
    }

    private Control BuildContent()
    {
        EnsureSectionsBuilt();
        var style = _settings.Style;

        var outerMargin = IsTopNavigationEnabled() ? new Thickness(28, 4, 28, 24) : new Thickness(22);
        if (!double.IsNaN(style.ContentSpacing)) outerMargin = new Thickness(style.ContentSpacing);
        
        var innerPadding = double.IsNaN(style.ContentPadding) ? new Thickness(18) : new Thickness(style.ContentPadding);
        IBrush bg = !string.IsNullOrWhiteSpace(style.ContentBackground) ? new SolidColorBrush(Color.Parse(style.ContentBackground)) : Brushes.Transparent;

        var launch = TryPlaceInSection("LaunchSection", DetachFromParent(launchSection)!);
        var modrinth = TryPlaceInSection("ModrinthSection", DetachFromParent(modrinthSection)!);
        var profiles = TryPlaceInSection("ProfilesSection", DetachFromParent(profilesSection)!);
        var performance = TryPlaceInSection("PerformanceSection", DetachFromParent(performanceSection)!);
        var settings = TryPlaceInSection("SettingsSection", DetachFromParent(settingsSection)!);
        var layout = TryPlaceInSection("LayoutSection", DetachFromParent(layoutSection)!);

        return new Grid
        {
            Margin = outerMargin,
            Children =
            {
                new Border
                {
                    Background = bg,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(30, 100, 120, 180)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(24),
                    Padding = innerPadding,
                    Child = new Grid
                    {
                        Children =
                        {
                            launch!,
                            modrinth!,
                            profiles!,
                            performance!,
                            settings!,
                            layout!
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
                    FontSize = 16,
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
                DetachFromParent(heroInstanceLabel)!,
                DetachFromParent(heroPerformanceLabel)!,
                new Border { Height = 12 },
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
            RadiusX = new RelativeScalar(0.8, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
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
        _playOverlayIcon.FontSize = 18;
        _playOverlayLabel.Text = "PLAY";
        _playOverlayLabel.FontSize = 15;
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
                    new TextBlock { Text = "□", FontSize = 15, Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor)) },
                    new TextBlock { Text = "Mods", FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0) }.With(column: 1),
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
                    new TextBlock { Text = "〓", FontSize = 15, Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor)) },
                    new TextBlock { Text = "Instances", FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0) }.With(column: 1),
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

        var skinContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "●", FontSize = 10, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Skin", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var skinBtn = new Button { Content = skinContent, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        skinBtn.Click += async (_, _) => await ChangeSkinAsync();
        ApplyHoverMotion(skinBtn);

        var capeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "■", FontSize = 10, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Cape", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var capeBtn = new Button { Content = capeContent, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        capeBtn.Click += async (_, _) => await ChangeCapeAsync();
        ApplyHoverMotion(capeBtn);

        var resetContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "×", FontSize = 12, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Reset", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var resetBtn = new Button { Content = resetContent, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        resetBtn.Click += (_, _) => {
            _settings.CustomSkinPath = string.Empty;
            _settingsStore.Save(_settings);
            // SyncSkinShuffleAvatarToLauncher removed
        };
        ApplyHoverMotion(resetBtn);

        var avatarPanel = CreateGlassPanel(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Avatar", FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Opacity = 0.8 },
                new Border { Height = 290, Child = DetachFromParent(characterImage) },
                new TextBlock 
                { 
                    Text = "Character features (Skins/Capes) are under development.", 
                    Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), 
                    FontSize = 10, 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                    ColumnSpacing = 8,
                    Children = { skinBtn.With(column: 0), capeBtn.With(column: 1), resetBtn.With(column: 2) }
                }
            }
        }, padding: new Thickness(24), margin: new Thickness(0));

        _avatarGlass = avatarPanel;
        _avatarControls = (StackPanel)avatarPanel.Child!;
        _avatarActions = (Grid)_avatarControls.Children[3];

        _avatarGlass.PointerEntered += (s, e) => { if (_isNarrowMode) SetAvatarExpansion(true); };
        _avatarGlass.PointerExited += (s, e) => { if (_isNarrowMode) SetAvatarExpansion(false); };

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        if (!ShouldExternalizePlayButton())
            actionRow.Children.Add(DetachFromParent(_playOverlay)!);
        actionRow.Children.Add(actionsGroup);

        _mainContentStack = new StackPanel
        {
            Spacing = 40,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 48, 0, 0),
            Children =
            {
                topInfo,
                actionRow,
                BuildFeaturedServersSection()
            }
        };

        var mainRow = new Grid
        {
            Children =
            {
                _mainContentStack,
                avatarPanel.With(a => {
                    a.HorizontalAlignment = HorizontalAlignment.Right;
                    a.VerticalAlignment = VerticalAlignment.Top;
                    a.ZIndex = 10;
                })
            }
        };

        var statsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 20,
            Children =
            {
                Create1to1StatCard("FPS", homeFpsStatValue, "Average performance"),
                Create1to1StatCard("RAM", homeRamStatValue, "Memory usage").With(column: 1)
            }
        };

        _homeStatusBar = new Border
        {
            Height = 110,
            Background = new SolidColorBrush(Color.Parse("#0D111C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A3143")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(32, 20),
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new StackPanel
                    {
                        Children =
                        {
                            statusLabel.With(tb => {
                                tb.FontSize = 15;
                                tb.FontWeight = FontWeight.Black;
                                tb.Foreground = Brushes.White;
                            }),
                            installDetailsLabel.With(tb => {
                                tb.FontSize = 12;
                                tb.Foreground = new SolidColorBrush(Color.Parse("#8E98AC"));
                                tb.Margin = new Thickness(0, 4, 0, 0);
                            })
                        }
                    },
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            pbFiles.With(pb => {
                                pb.Height = 6;
                                pb.CornerRadius = new CornerRadius(3);
                            }),
                            pbProgress.With(pb => {
                                pb.Height = 14;
                                pb.CornerRadius = new CornerRadius(7);
                                pb.Background = new SolidColorBrush(Color.Parse("#1A1F2E"));
                                pb.Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor));
                            })
                        }
                    }
                }
            }
        };

        return new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 40,
                        Margin = new Thickness(24),
                        Children = { mainRow, statsRow }
                    }
                },
                _homeStatusBar.With(row: 1)
            }
        };
    }

    private Border Create1to1StatCard(string title, TextBlock valueBlock, string subLabel)
    {
        var accentColor = Color.Parse(_settings.AccentColor);
        valueBlock.FontSize = 32;
        valueBlock.FontWeight = FontWeight.Black;
        valueBlock.Foreground = new SolidColorBrush(accentColor);
        valueBlock.Text = "00";

        return CreateGlassPanel(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = title, FontSize = 12.5, Foreground = new SolidColorBrush(Color.Parse("#8E96A8")), FontWeight = FontWeight.Bold },
                valueBlock,
                new TextBlock { Text = subLabel, FontSize = 11.5, Foreground = new SolidColorBrush(Color.Parse("#667899")) }
            }
        }, padding: new Thickness(16), margin: new Thickness(0));
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

        modrinthSourceCombo.CornerRadius = new CornerRadius(16);
        modrinthSourceCombo.Height = 42;
        modrinthSourceCombo.Background = Brushes.Transparent;
        modrinthSourceCombo.BorderBrush = new SolidColorBrush(Color.Parse("#2A3143"));

        modrinthSearchButton.CornerRadius = new CornerRadius(16);
        modrinthSearchButton.Height = 42;
        SetButtonText(modrinthSearchButton, "🔍 Search");
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
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12, 0, 12, 24) // Match image padding
        };

        filterRow.Children.Add(modrinthSearchInput.With(column: 0));

        var sourceText = new TextBlock { Text = "Source", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var sourcePanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { sourceText, modrinthSourceCombo } };
        filterRow.Children.Add(sourcePanel.With(column: 1));
        
        var loaderText = new TextBlock { Text = "Loader", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var loaderPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { loaderText, modrinthLoaderCombo } };
        filterRow.Children.Add(loaderPanel.With(column: 2));

        var versionText = new TextBlock { Text = "Version", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var versionPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { versionText, modrinthVersionInput } };
        filterRow.Children.Add(versionPanel.With(column: 3));

        filterRow.Children.Add(modrinthProjectTypeCombo.With(column: 4));
        
        filterRow.Children.Add(modrinthSearchButton.With(column: 5));
        
        // ── Card Item Template ────────────────────────────────────────────

        modrinthResultsListBox.Background = Brushes.Transparent;
        modrinthResultsListBox.ItemsPanel = new FuncTemplate<Panel?>(() => new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 });
        modrinthResultsListBox.ItemsSource = _searchResults;
        modrinthResultsListBox.Margin = new Thickness(4, 0);

        modrinthResultsListBox.ItemTemplate = new FuncDataTemplate<ModrinthProject>((project, _) =>
        {
            bool isInstalled = _selectedProfile?.InstalledModIds.Contains(project?.ProjectId ?? "") ?? false;
            var installBtn = new Button
            {
                Content = isInstalled ? "Installed" : "Install",
                IsEnabled = !isInstalled,
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
                                    FontSize = 14,
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

        instancesHeader.Children.Add(new TextBlock
        {
            Text = "Instances",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        }.With(column: 0));

        var importBackupBtn = CreateCompactSecondaryButton("⤓ Import Zip");
        importBackupBtn.Click += async (_, _) => await ImportProfileZipAsync();

        var importDirBtn = CreateCompactSecondaryButton("📂 Import Dir");
        importDirBtn.Click += async (_, _) => await ImportInstanceFolderAsync();

        instancesHeader.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { importDirBtn, importBackupBtn }
        }.With(column: 1));

        var addBtn = CreatePrimaryButton("+", "#38D6C4", Colors.Black);
        addBtn.Width = 36;
        addBtn.Height = 36;
        addBtn.CornerRadius = new CornerRadius(18);
        addBtn.Padding = new Thickness(0);
        addBtn.Content = new TextBlock
        {
            Text = "+",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        };
        addBtn.VerticalAlignment = VerticalAlignment.Center;
        addBtn.Click += (_, _) =>
        {
            ClearSelectedProfile();
            createProfileButton.IsVisible = true;
            renameProfileButton.IsVisible = false;
            _instanceEditorOverlay!.IsVisible = true;
        };
        instancesHeader.Children.Add(addBtn.With(column: 2));

        var modsHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(8, 0, 8, 12),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Installed Mods", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                CreateCompactSecondaryButton("⚠ Scan Conflicts").With(btn =>
                {
                    btn.Click += async (_, _) =>
                    {
                        if (_selectedProfile != null) await ScanForModConflictsAsync(_selectedProfile);
                    };
                })
            }
        };

        Button CreateInlineProfileAction(string glyph, string hexColor)
        {
            var button = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse(hexColor)),
                Focusable = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = glyph,
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            return button;
        }

        profileListBox.Background = Brushes.Transparent;
        profileListBox.BorderThickness = new Thickness(0);
        profileListBox.Padding = new Thickness(0);
        profileListBox.ItemTemplate = new FuncDataTemplate<LauncherProfile>((profile, _) =>
        {
            if (profile == null) return new Border();

            var modifyButton = CreateInlineProfileAction("▶", "#38D6C4");
            modifyButton.Click += (_, _) => OpenProfileEditor(profile);

            var renameButton = CreateInlineProfileAction("✎", "#B7C4E9");
            renameButton.Click += (_, _) => OpenProfileEditor(profile);

            var deleteButton = CreateInlineProfileAction("✕", "#FF6B86");
            deleteButton.Click += async (_, _) =>
            {
                _selectedProfile = profile;
                profileListBox.SelectedItem = profile;
                await DeleteSelectedProfileAsync(profile);
            };

            return new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A2030")),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{profile.Name} [{profile.LoaderDisplay}]",
                            Foreground = Brushes.White,
                            FontSize = 14,
                            FontWeight = FontWeight.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }.With(column: 0),
                        modifyButton.With(column: 1),
                        renameButton.With(column: 2),
                        deleteButton.With(column: 3)
                    }
                }
            };
        });

        var modsListBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ItemsSource = _modItems
        };
        modsListBox.ItemTemplate = new FuncDataTemplate<ModItem>((modItem, _) =>
        {
            if (modItem == null) return new Border();

            var enableToggle = new ToggleSwitch
            {
                OnContent = "ON",
                OffContent = "OFF",
                Margin = new Thickness(0, 0, 16, 0)
            };
            enableToggle[!ToggleSwitch.IsCheckedProperty] = new Avalonia.Data.Binding(nameof(ModItem.IsEnabled));

            var deleteBtn = new Button
            {
                Content = "🗑",
                Foreground = Brushes.Tomato,
                Background = Brushes.Transparent,
                FontSize = 18,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8)
            };
            deleteBtn.Click += (_, _) =>
            {
                try
                {
                    if (File.Exists(modItem.FullPath)) File.Delete(modItem.FullPath);
                    _modItems.Remove(modItem);
                }
                catch { }
            };

            var nameBlock = new TextBlock { FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4), TextTrimming = TextTrimming.CharacterEllipsis };
            nameBlock[!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(ModItem.FileName));

            return new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1F2E")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A3143")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                nameBlock,
                                new TextBlock { FontSize = 11, Foreground = Brushes.Gray }.With(tb => tb[!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(ModItem.FileSize)))
                            }
                        }.With(column: 0),
                        enableToggle.With(column: 1),
                        deleteBtn.With(column: 2)
                    }
                }
            };
        });

        var instanceDetails = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                DetachFromParent(profileInspectorTitle)!,
                DetachFromParent(profileInspectorMeta)!,
                DetachFromParent(profileInspectorPath)!
            }
        };

        var instancesPane = CreateGlassPanel(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#111725")),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 0,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#0F1523")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#24324A")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(18),
                        Height = 440,
                        Padding = new Thickness(14),
                        Child = new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            Content = profileListBox
                        }
                    },
                    instanceDetails
                }
            }
        });

        var modsPane = CreateGlassPanel(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#111725")),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(14),
            Child = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0F1420")),
                CornerRadius = new CornerRadius(18),
                Height = 520,
                Padding = new Thickness(14),
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = modsListBox
                }
            }
        });

        return CreateSectionScroller(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 24,
            Margin = new Thickness(4, 4, 4, 60),
            Children =
            {
                new StackPanel
                {
                    Children =
                    {
                        instancesHeader,
                        instancesPane
                    }
                }.With(column: 0),
                new StackPanel
                {
                    Children =
                    {
                        modsHeader,
                        modsPane
                    }
                }.With(column: 1)
            }
        });
    }

    private Control BuildPerformanceDeck()
    {
        var perfFilesPb = new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };
        var perfNetworkPb = new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };

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
        var totalRam = GetSystemRamMb();
        var ramSlider = new Slider 
        { 
            Minimum = 512, 
            Maximum = totalRam, 
            Value = _settings.MaxRamMb,
            SmallChange = 512,
            LargeChange = 1024
        };
        var ramLabel = new TextBlock { Text = $"{_settings.MaxRamMb} MB", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold, Foreground = Brushes.White };
        ramSlider.ValueChanged += (_, e) => {
            var val = (int)(e.NewValue / 512) * 512;
            _settings.MaxRamMb = val;
            ramLabel.Text = $"{val} MB";
            _settingsStore.Save(_settings);
        };

        var jvmArgsInput = CreateTextBox();
        jvmArgsInput.Text = _settings.JvmArgs;
        jvmArgsInput.Watermark = "-Xmx2G -XX:+UseG1GC...";
        jvmArgsInput.TextChanged += (_, _) => {
            _settings.JvmArgs = jvmArgsInput.Text ?? "";
            _settingsStore.Save(_settings);
        };

        var windowWidthInput = CreateTextBox();
        windowWidthInput.Text = _settings.WindowWidth.ToString();
        windowWidthInput.TextChanged += (_, _) => {
            if (int.TryParse(windowWidthInput.Text, out var val)) { _settings.WindowWidth = val; _settingsStore.Save(_settings); }
        };

        var windowHeightInput = CreateTextBox();
        windowHeightInput.Text = _settings.WindowHeight.ToString();
        windowHeightInput.TextChanged += (_, _) => {
            if (int.TryParse(windowHeightInput.Text, out var val)) { _settings.WindowHeight = val; _settingsStore.Save(_settings); }
        };

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

        return CreateSectionScroller(new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(4, 4, 4, 80),
            Children =
            {
                CreateSectionTitle("Settings", "Fine-tune your launch posture and system parameters."),
                CreateGlassPanel(new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        new StackPanel { Spacing = 8, Children = { 
                            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { CreatePanelEyebrow("RAM Allocation"), ramLabel.With(column: 1) } },
                            ramSlider 
                        } },
                        new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Extra JVM Arguments"), jvmArgsInput } },
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,*"),
                            ColumnSpacing = 16,
                            Children =
                            {
                                new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Window Width"), windowWidthInput } },
                                new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Window Height"), windowHeightInput } }.With(column: 1)
                            }
                        },
                        new Separator { Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)) },
                        offlineModeToggle,
                        new Separator { Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)) },
                        new StackPanel { Spacing = 8, Children = { 
                            CreatePanelEyebrow("Installation Directory"), 
                            new TextBlock { Text = _defaultMinecraftPath.BasePath, Foreground = Brushes.Gray, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                            CreateSecondaryButton("Change Directory").With(btn => btn.Click += async (_, _) => await ChangeBaseDirectoryAsync())
                        } }
                    }
                })
            }
        });
    }

    private async Task ChangeBaseDirectoryAsync()
    {
        try {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Base Minecraft Directory" });
            if (folders != null && folders.Count > 0)
            {
                var newPath = folders[0].Path.LocalPath;
                _settings.BaseMinecraftPath = newPath;
                _settingsStore.Save(_settings);
                await DialogService.ShowInfoAsync(this, "Directory Changed", "Please restart the launcher to apply the change.");
            }
        } catch (Exception ex) {
            await DialogService.ShowInfoAsync(this, "Error", $"Failed to change directory: {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        var tasks = new List<Task>();
        tasks.Add(CheckForUpdatesAsync());
        
        tasks.Add(PerformFirstRunSetup());
        await Task.WhenAll(tasks);

        // Auto-refresh selected account if needed
        var selectedAcc = _settings.Accounts.FirstOrDefault(a => a.Id == _settings.SelectedAccountId);
        if (selectedAcc != null && selectedAcc.Provider == "microsoft" && selectedAcc.IsExpired)
        {
            LauncherLog.Info($"[Initialize] Selected account {selectedAcc.Username} expired. Attempting refresh...");
            await TryRefreshAccountAsync(selectedAcc);
        }
        
        loadingLabel.Text = string.Empty;
        usernameInput.Text = string.IsNullOrWhiteSpace(_settings.Username) ? Environment.UserName : _settings.Username;
        if (selectedAcc != null && !string.IsNullOrWhiteSpace(selectedAcc.Username))
            usernameInput.Text = selectedAcc.Username;
        UsernameInput_TextChanged();

        profileLoaderCombo.SelectedIndex = 0;
        _quickLoaderCombo.SelectedIndex = 0;
        modrinthProjectTypeCombo.SelectedIndex = 0;
        modrinthLoaderCombo.SelectedIndex = 0;
        minecraftVersion.SelectedIndex = 0;

        RefreshProfiles();
        tasks.Add(ListVersionsAsync(GetSelectedVersionCategory()));

        if (!string.IsNullOrEmpty(_settings.JvmArgs) && (_settings.JvmArgs.Contains("--sun-misc-unsafe-memory-access") || _settings.JvmArgs.Contains("--enable-native-access")))
        {
            _settings.JvmArgs = _settings.JvmArgs
                .Replace("--sun-misc-unsafe-memory-access=allow", "")
                .Replace("--sun-misc-unsafe-memory-access", "")
                .Replace("--enable-native-access=ALL-UNNAMED", "")
                .Replace("--enable-native-access", "")
                .Trim();
            _settingsStore.Save(_settings);
        }

        // Initialize instance version lists
        if (instanceCategoryCombo != null)
        {
            instanceCategoryCombo.SelectedItem = "Versions";
            tasks.Add(ListVersionsAsync("Versions"));
        }

        if (!string.IsNullOrWhiteSpace(_settings.Version))
        {
            cbVersion.SelectedItem = _settings.Version;
            _quickVersionCombo.SelectedItem = _settings.Version;
        }

        SyncModrinthFilters();
        UpdateCharacterPreview();
        UpdateLauncherContext();
        SetProgressState("Ready", 0, 0);

        await Task.WhenAll(tasks);
    }

    public void SetActiveSection(string section)
    {
        _activeSection = section;

        var launchVisible = section == "home" || section == "launch";
        var modrinthVisible = section == "modrinth";
        var profilesVisible = section == "instances" || section == "profiles";
        var performanceVisible = section == "performance";
        var settingsVisible = section == "settings";
        var layoutVisible = section == "layout";

        launchSection.IsVisible = launchVisible;
        modrinthSection.IsVisible = modrinthVisible;
        profilesSection.IsVisible = profilesVisible;
        performanceSection.IsVisible = performanceVisible;
        settingsSection.IsVisible = settingsVisible;
        layoutSection.IsVisible = layoutVisible;

        if (_sectionSlotControls.TryGetValue("LaunchSection", out var launchHost)) launchHost.IsVisible = launchVisible;
        if (_sectionSlotControls.TryGetValue("ModrinthSection", out var modrinthHost)) modrinthHost.IsVisible = modrinthVisible;
        if (_sectionSlotControls.TryGetValue("ProfilesSection", out var profilesHost)) profilesHost.IsVisible = profilesVisible;
        if (_sectionSlotControls.TryGetValue("PerformanceSection", out var performanceHost)) performanceHost.IsVisible = performanceVisible;
        if (_sectionSlotControls.TryGetValue("SettingsSection", out var settingsHost)) settingsHost.IsVisible = settingsVisible;
        if (_sectionSlotControls.TryGetValue("LayoutSection", out var layoutHost)) layoutHost.IsVisible = layoutVisible;

        if (_playOverlay != null)
        {
            _playOverlay.IsVisible = _settings.Style.PlayButtonGlobal || launchVisible;
        }

        ApplyNavState(launchNavButton, section == "home" || section == "launch");
        ApplyNavState(modrinthNavButton, section == "modrinth");
        ApplyNavState(profilesNavButton, section == "instances" || section == "profiles");
        ApplyNavState(performanceNavButton, section == "performance");
        ApplyNavState(settingsNavButton, section == "settings");
        ApplyNavState(layoutNavButton, section == "layout");
        if (accountsNavButton != null) ApplyNavState(accountsNavButton, section == "accounts");

        if (section == "modrinth" && _searchResults.Count == 0)
        {
            _ = SearchModrinthAsync();
        }
    }

    private async Task ListVersionsAsync(string category = "Versions")
    {
        await _versionListSemaphore.WaitAsync();
        try
        {
            var items = new List<string>();
            VersionMetadataCollection? manifest = null;

            if (!_settings.OfflineMode)
            {
                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        manifest = await _defaultLauncher.GetAllVersionsAsync();
                        break;
                    }
                    catch (Exception) when (attempt < maxAttempts)
                    {
                        await Task.Delay(200 * attempt);
                    }
                }
            }

            if (manifest != null)
            {
                foreach (var version in manifest)
                {
                    if (version != null && ShouldIncludeVersion(version.Name, version.Type, category))
                    {
                        var vn = version.Name;
                        if (!string.IsNullOrWhiteSpace(vn)) items.Add(vn);
                    }
                }
            }
            else
            {
                // Fallback: Scan local versions (for offline mode or internet failure)
                try
                {
                    var versionsDir = Path.Combine(_defaultMinecraftPath.BasePath, "versions");
                    if (File.Exists(versionsDir) || Directory.Exists(versionsDir))
                    {
                        foreach (var dir in Directory.GetDirectories(versionsDir))
                        {
                            var versionName = Path.GetFileName(dir);
                            if (!string.IsNullOrWhiteSpace(versionName))
                            {
                                // In offline mode/not-manifested local folders, we try to guess the type from the name
                                if (ShouldIncludeVersion(versionName, null, category))
                                    items.Add(versionName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LauncherLog.Info($"[Aether Launcher] Offline version list failed: {ex}");
                }
            }

            Dispatcher.UIThread.Post(() => {
                _versionItems.Clear();
                foreach (var item in items) 
                {
                    if (!_versionItems.Contains(item)) _versionItems.Add(item);
                }

                if (_selectedProfile is not null && !_versionItems.Contains(_selectedProfile.GameVersion))
                    _versionItems.Insert(0, _selectedProfile.GameVersion);

                if ((cbVersion.SelectedItem == null || (cbVersion.SelectedItem is string s && !_versionItems.Contains(s))) && _versionItems.Count > 0)
                {
                    try { 
                        var latest = manifest?.FirstOrDefault(v => v.Type == "release")?.Name;
                        cbVersion.SelectedItem = (latest != null && _versionItems.Contains(latest)) ? latest : _versionItems[0]; 
                    } catch { cbVersion.SelectedIndex = 0; }
                }
            });
        }
        finally
        {
            _versionListSemaphore.Release();
        }
    }

    private static bool ShouldIncludeVersion(string name, string? type, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var t = type?.ToLower() ?? string.Empty;
        var isRelease = t == "release" || Regex.IsMatch(name, @"^\d+(\.\d+)*$");
        var isSnapshot = t == "snapshot" || Regex.IsMatch(name, @"^\d{2}w\d{2}[a-z]$", RegexOptions.IgnoreCase);

        if (string.Equals(category, "Versions", StringComparison.OrdinalIgnoreCase))
            return isRelease;

        if (string.Equals(category, "Snapshots", StringComparison.OrdinalIgnoreCase))
            return isSnapshot;

        // "Other sources" category: anything that isn't a standard release or snapshot (like Forge, Fabric, older alphas, etc.)
        return !isRelease && !isSnapshot;
    }

    private string GetSelectedVersionCategory() =>
        minecraftVersion.SelectedItem?.ToString() ?? VersionCategoryOptions[0];

    private async Task LaunchAsync()
    {
        var activeUsername = GetActiveUsername();
        if (string.IsNullOrWhiteSpace(activeUsername))
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

        // [USER REQUEST] Remove confirmation popup for instant launch
        /*
        var shouldLaunch = await DialogService.ShowConfirmAsync(
            this,
            "Launch confirmation",
            $"Launch {targetLabel} as {usernameInput.Text.Trim()}?");
        if (!shouldLaunch)
            return;
        */

        ToggleBusyState(true, "Priming the launcher...");
        btnStart.Content = "Cancel";
        btnStart.IsEnabled = true; // Allow clicking "Cancel"

        _launchCts = new CancellationTokenSource();
        var token = _launchCts.Token;

        try
        {
            var launcherPath = _selectedProfile is null
                ? _defaultMinecraftPath
                : new MinecraftPath(_selectedProfile.InstanceDirectory);
            
            var launcher = CreateLauncher(launcherPath);

            if (_selectedProfile is not null)
            {
                await EnsureProfileReadyAsync(_selectedProfile, launcher, token);
                
                // Ensure the required mods are installed automatically
                var modsDir = Path.Combine(_selectedProfile.InstanceDirectory, "mods");
                Directory.CreateDirectory(modsDir);
                LauncherLog.Info($"[Launch] Autoinstalling required mods for instance: {_selectedProfile.Name}");
                
                // Custom Skin Loader is always required
                await InstallModIfMissingAsync("customskinloader", _selectedProfile, modsDir, token);

                // FancyMenu integration if enabled
                if (_settings.EnableFancyMenu && SupportsFancyMenu(_selectedProfile))
                {
                    await InstallModIfMissingAsync("fancymenu", _selectedProfile, modsDir, token);
                    await InstallModIfMissingAsync("konkrete", _selectedProfile, modsDir, token);
                }
                
                versionToLaunch = _selectedProfile.VersionId;
            }
            else
            {
                await launcher.InstallAsync(versionToLaunch, token);
            }

            var session = await BuildLaunchSessionAsync(token);

            var targetGameVer = _selectedProfile?.GameVersion ?? versionToLaunch;
            var javaPath = await GetJavaPathForVersionAsync(targetGameVer, token);
            var effectiveGamePath = _selectedProfile is not null && !string.IsNullOrWhiteSpace(_selectedProfile.GameDirectoryOverride)
                ? _selectedProfile.GameDirectoryOverride
                : launcherPath.BasePath;

            EnsureDeathClientThemeResourcePack(effectiveGamePath, targetGameVer);

            var process = await launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
            {
                Session = session,
                JavaPath = javaPath,
                MaximumRamMb = _settings.MaxRamMb,
                ExtraJvmArguments = string.IsNullOrWhiteSpace(_settings.JvmArgs)
                    ? Array.Empty<MArgument>()
                    : _settings.JvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(arg => !arg.Contains("--sun-misc-unsafe-memory-access") && !arg.Contains("--enable-native-access")) // Strip recognized JVM killers
                        .Select(arg => new MArgument(arg)),
                ScreenWidth = _settings.WindowWidth,
                ScreenHeight = _settings.WindowHeight,
                Path = _selectedProfile is not null && !string.IsNullOrWhiteSpace(_selectedProfile.GameDirectoryOverride)
                    ? new MinecraftPath(_selectedProfile.GameDirectoryOverride)
                    : launcherPath
            });

            // CRITICAL: Some versions have these flags hardcoded in their version JSON.
            // We strip them from the FINAL command line here if they cause crashes.
            var scrubbedArgs = process.StartInfo.Arguments;
            string[] problematicFlags = { 
                "--sun-misc-unsafe-memory-access=allow", 
                "--enable-native-access=ALL-UNNAMED" 
            };
            
            foreach (var flag in problematicFlags)
            {
                if (scrubbedArgs.Contains(flag))
                {
                    scrubbedArgs = scrubbedArgs.Replace(flag, "").Trim();
                }
            }
            process.StartInfo.Arguments = scrubbedArgs;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            btnStart.Content = "Launching...";
            btnStart.IsEnabled = false;
            
            token.ThrowIfCancellationRequested(); // Final check
            process.Start();

            _settings.Username = activeUsername;
            _settings.Version = cbVersion.SelectedItem?.ToString() ?? string.Empty;
            _settingsStore.Save(_settings);
            
            Close();
        }
        catch (OperationCanceledException)
        {
            LauncherLog.Info("[Launch] User cancelled the launch process.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Launch failed", $"Failed to launch Minecraft.\n{ex.Message}");
        }
        finally
        {
            _launchCts?.Dispose();
            _launchCts = null;
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
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    RefreshProfiles(downloadedProfile);
                    SetProgressState($"Downloaded {versionToInstall}.", 0, 0);
                });
            }

            _settings.Version = versionToInstall;
            _settingsStore.Save(_settings);
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
        else if (profile.Loader == "quilt")
        {
            await launcher.InstallAsync(profile.GameVersion);
            await EnsureQuiltProfileAsync(profile, cancellationToken);
            await launcher.InstallAsync(profile.VersionId);
        }
        else if (profile.Loader == "forge" || profile.Loader == "neoforge")
        {
            await launcher.InstallAsync(profile.GameVersion);
            await EnsureForgeProfileAsync(profile, cancellationToken);
            await launcher.InstallAsync(profile.VersionId);
        }
        else if (profile.Loader == "vanilla")
        {
            await launcher.InstallAsync(profile.GameVersion);
        }
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

    private async Task EnsureQuiltProfileAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoaderVersion))
            throw new InvalidOperationException("Quilt loader version is missing from the profile.");

        var versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
        var versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
        if (File.Exists(versionJsonPath))
            return;

        Directory.CreateDirectory(versionDirectory);
        var manifestJson = await _modrinthClient.GetStringAsync(
            $"https://meta.quiltmc.org/v3/versions/loader/{profile.GameVersion}/{profile.LoaderVersion}/profile/json",
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

    private async Task EnsureForgeProfileAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoaderVersion))
            throw new InvalidOperationException($"{profile.Loader} loader version is missing from the profile.");

        var versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
        var versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
        if (File.Exists(versionJsonPath))
            return;

        Directory.CreateDirectory(versionDirectory);

        string installerUrl;
        string installerFileName;

        if (profile.Loader == "neoforge")
        {
            installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{profile.LoaderVersion}/neoforge-{profile.LoaderVersion}-installer.jar";
            installerFileName = $"neoforge-{profile.LoaderVersion}-installer.jar";
        }
        else
        {
            var forgeVer = $"{profile.GameVersion}-{profile.LoaderVersion}";
            installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{forgeVer}/forge-{forgeVer}-installer.jar";
            installerFileName = $"forge-{forgeVer}-installer.jar";
        }

        var installerPath = Path.Combine(Path.GetTempPath(), installerFileName);
        
        ToggleBusyState(true, $"Downloading {profile.Loader} installer...");
        using (var httpClient = new System.Net.Http.HttpClient())
        {
            var response = await httpClient.GetAsync(installerUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download installer from {installerUrl}");
            
            using var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);
        }

        ToggleBusyState(true, $"Installing {profile.Loader}...");
        var javaPath = await GetJavaPathForVersionAsync(profile.GameVersion, cancellationToken);
        var installArgs = $"\"{installerPath}\" --installClient \"{profile.InstanceDirectory}\"";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"-jar {installArgs}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new Exception($"Installer failed: {error}");
            }
        }
        else
            throw new Exception("Failed to start installer.");

        var versionsDir = Path.Combine(profile.InstanceDirectory, "versions");
        if (Directory.Exists(versionsDir))
        {
            var createdVersionDir = Directory.GetDirectories(versionsDir)
                .FirstOrDefault(d => Path.GetFileName(d).Contains(profile.LoaderVersion) && Path.GetFileName(d).ToLower().Contains(profile.Loader));

            if (createdVersionDir != null)
            {
                var createdVersionId = Path.GetFileName(createdVersionDir);
                if (!string.Equals(profile.VersionId, createdVersionId, StringComparison.Ordinal))
                {
                    profile.VersionId = createdVersionId;
                    _profileStore.Save(profile);
                }
            }
        }
    }

    private async Task<string> GetJavaPathForVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        int requiredJavaVersion = 8;
        
        // Handle standard 1.x.y versions
        if (gameVersion.StartsWith("1."))
        {
            var parts = gameVersion.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                if (minor >= 21) requiredJavaVersion = 21;
                else if (minor >= 17) requiredJavaVersion = 17;
                else if (minor >= 16) requiredJavaVersion = 16;
            }
        }
        else 
        {
            // Handle custom modern versions like "26.1"
            var parts = gameVersion.Split('.');
            if (parts.Length >= 1 && int.TryParse(parts[0], out var major))
            {
                if (major >= 25) requiredJavaVersion = 25; // Java 25 for extremely modern builds (Class version 69.0)
                else if (major >= 21) requiredJavaVersion = 21; 
                else if (major >= 17) requiredJavaVersion = 17;
            }
        }

        var javaDir = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "runtimes", $"java-{requiredJavaVersion}");
        var javaExe = OperatingSystem.IsWindows() ? "java.exe" : "java";
        var javaPath = Path.Combine(javaDir, "bin", javaExe);

        if (File.Exists(javaPath))
            return javaPath;

        ToggleBusyState(true, $"Downloading Java {requiredJavaVersion}...");
        Directory.CreateDirectory(javaDir);

        string os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "mac" : "linux";
        string arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "aarch64",
            System.Runtime.InteropServices.Architecture.X86 => "x32",
            _ => "x64"
        };
        
        var apiUrl = $"https://api.adoptium.net/v3/binary/latest/{requiredJavaVersion}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";
        var tempArchive = Path.Combine(Path.GetTempPath(), $"java-{requiredJavaVersion}-jre.{(os == "windows" ? "zip" : "tar.gz")}");

        using (var httpClient = new System.Net.Http.HttpClient())
        {
            var response = await httpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download JRE for Java {requiredJavaVersion}");

            using var fs = new FileStream(tempArchive, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);
        }

        ToggleBusyState(true, $"Extracting Java {requiredJavaVersion}...");
        if (os == "windows")
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(tempArchive, javaDir, true);
            var foundExe = Directory.GetFiles(javaDir, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (foundExe != null) return foundExe;
        }
        else
        {
            using var extractProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{tempArchive}\" -C \"{javaDir}\" --strip-components=1",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (extractProcess != null) await extractProcess.WaitForExitAsync(cancellationToken);
            
            var foundExe = Directory.GetFiles(javaDir, "java", SearchOption.AllDirectories).FirstOrDefault();
            if (foundExe != null)
            {
                System.Diagnostics.Process.Start("chmod", $"+x \"{foundExe}\"")?.WaitForExit();
                return foundExe;
            }
        }

        throw new Exception($"Java {requiredJavaVersion} executable not found.");
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeathClient-Updater/1.0");
            var currentVersion = new Version(1, 0, 0); 
            
            var response = await client.GetStringAsync("https://api.github.com/repos/AchinthyaJ/DeathClient/releases/latest");
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                var tag = tagElement.GetString();
                if (!string.IsNullOrEmpty(tag) && tag.StartsWith("v"))
                {
                    if (Version.TryParse(tag.Substring(1), out var latestVersion))
                    {
                        if (latestVersion > currentVersion)
                        {
                            Dispatcher.UIThread.Post(async () =>
                            {
                                var download = await DialogService.ShowConfirmAsync(this, "Update Available", $"A new version ({tag}) is available. Would you like to download it?");
                                if (download && doc.RootElement.TryGetProperty("html_url", out var urlElement))
                                {
                                    var url = urlElement.GetString();
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = url,
                                            UseShellExecute = true
                                        });
                                    }
                                }
                            });
                        }
                    }
                }
            }
        }
        catch { }
    }

    private System.Threading.CancellationTokenSource? _skinCancellation;

    public async void UsernameInput_TextChanged()
    {
        var selectedAccount = GetSelectedAccount();
        var username = GetActiveUsername();

        if (string.IsNullOrWhiteSpace(username))
        {
            _playerUuid = string.Empty;
            characterImage.Source = null;
            btnStart.IsEnabled = false;
            return;
        }

        btnStart.IsEnabled = true;
        
        _playerUuid = !string.IsNullOrWhiteSpace(selectedAccount?.Uuid)
            ? selectedAccount!.Uuid
            : Character.GenerateUuidFromUsername(username);
        
        _skinCancellation?.Cancel();
        _skinCancellation = new System.Threading.CancellationTokenSource();
        var token = _skinCancellation.Token;

        UpdateCharacterPreview();

        try
        {
            await Task.Delay(1000, token);
            await FetchAndSetSkinAsync(username, token);
        }
        catch (TaskCanceledException) { }
    }

    private async Task FetchAndSetSkinAsync(string username, CancellationToken token)
    {
        var uuid = GetSelectedAccount()?.Uuid;
        if (string.IsNullOrWhiteSpace(uuid))
            uuid = Character.GenerateUuidFromUsername(username);
        var url = $"https://crafatar.com/skins/{uuid}";
        
        var skinsDir = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skins");
        Directory.CreateDirectory(skinsDir);
        var skinPath = Path.Combine(skinsDir, $"{username}.png");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var bytes = await client.GetByteArrayAsync(url, token);
            await File.WriteAllBytesAsync(skinPath, bytes, token);
            _settings.CustomSkinPath = skinPath;
            _settingsStore.Save(_settings);
        }
        catch
        {
            _settings.CustomSkinPath = string.Empty;
            _settingsStore.Save(_settings);
            if (File.Exists(skinPath))
                File.Delete(skinPath);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (GetActiveUsername() == username)
            {
                UpdateCharacterPreview();
            }
        });
    }

    public void CbVersion_SelectionChanged()
    {
        UpdateCharacterPreview();
        if (_selectedProfile is null)
            SyncModrinthFilters();
    }

    private void UpdateCharacterPreview()
    {
        // Removed SkinShuffle Sync
        
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
                    // Head (base layer: 8,8 size 8x8)
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
            var searchFolders = new[] 
            {
                Path.Combine(AppContext.BaseDirectory, "Resources"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources")
            };

            foreach (var folder in searchFolders)
            {
                var p = Path.Combine(folder, $"{resourceName}.png");
                if (File.Exists(p))
                {
                    imagePath = p;
                    break;
                }
            }
        }

        if (imagePath != null && File.Exists(imagePath))
        {
            try {
                characterImage.Source = new Bitmap(imagePath);
            } catch { characterImage.Source = null; }
        }
        else
        {
            characterImage.Source = null;
        }
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

    public void ProfileListBox_SelectionChanged()
    {
        _selectedProfile = profileListBox.SelectedItem as LauncherProfile;
        if (_selectedProfile is not null)
            profileNameInput.Text = _selectedProfile.Name;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
        RefreshModsList();
        UpdateSelectedProjectDetails();
        RefreshSearchList();
    }

    private void RefreshModsList()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _modItems.Clear();
            if (_selectedProfile == null) return;
            var modsDir = _selectedProfile.ModsDirectory;
            if (!Directory.Exists(modsDir)) return;

            try
            {
                var files = Directory.GetFiles(modsDir);
                int count = 0;
                foreach (var file in files)
                {
                    if (!file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && 
                        !file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var item = new ModItem
                    {
                        FileName = Path.GetFileName(file),
                        FileSize = new FileInfo(file).Length / 1024 + " KB",
                        FullPath = file
                    };
                    // CRITICAL: Initialize the state based on extension, otherwise it defaults to Disabled
                    item.InitState(!file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase));
                    
                    _modItems.Add(item);
                    count++;
                }
                LauncherLog.Info($"[ModsList] Loaded {count} mods for {_selectedProfile.Name}.");
            }
            catch (Exception ex)
            {
                LauncherLog.Error($"[ModsList] Refresh failed for {_selectedProfile.Name}", ex);
            }
        });
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

    private void OpenProfileEditor(LauncherProfile profile)
    {
        _selectedProfile = profile;
        profileListBox.SelectedItem = profile;
        profileNameInput.Text = profile.Name;
        profileGameDirInput.Text = profile.GameDirectoryOverride ?? string.Empty;

        var selectedIndex = Array.FindIndex(ProfileLoaderOptions, option =>
            string.Equals(option, profile.Loader, StringComparison.OrdinalIgnoreCase));
        profileLoaderCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

        createProfileButton.IsVisible = false;
        renameProfileButton.IsVisible = true;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
        RefreshModsList();
        _instanceEditorOverlay.IsVisible = true;
    }

    private void UpdateLauncherContext()
    {
        if (_selectedProfile is null)
        {
            activeProfileBadge.Text = "HOME";
            activeContextLabel.Text = string.Empty;
            installModeLabel.Text = "Default";
            SetButtonText(btnStart, "▶ Play");
            profileInspectorTitle.Text = "Standard Profile";
            profileInspectorMeta.Text = "No isolated profile is active. Mods install only after you create or select a profile.";
            profileInspectorPath.Text = $"Instances root: {_profileStore.GetInstancesRoot()}";
            clearProfileButton.IsEnabled = false;
            renameProfileButton.IsEnabled = false;
            heroInstanceLabel.Text = "Standard Play";
            heroPerformanceLabel.Text = $"{cbVersion.SelectedItem?.ToString() ?? "1.21.1"} • Ready";
            var ramGbInit = _settings.MaxRamMb / 1024.0;
            var expectedFpsInit = Math.Round(ramGbInit * 41.25).ToString();
            var expectedRamInit = $"{Math.Round(ramGbInit, 1)} GB";
            homeFpsStatValue.Text = expectedFpsInit;
            homeRamStatValue.Text = expectedRamInit;
            performanceFpsStatValue.Text = expectedFpsInit;
            performanceRamStatValue.Text = expectedRamInit;
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
        var ramGb = _settings.MaxRamMb / 1024.0;
        var fpsText = Math.Round(ramGb * (_selectedProfile.Loader == "vanilla" ? 41.25 : 30)).ToString();
        var ramText = $"{Math.Round(ramGb, 1)} GB";
        homeFpsStatValue.Text = fpsText;
        homeRamStatValue.Text = ramText;
        performanceFpsStatValue.Text = fpsText;
        performanceRamStatValue.Text = ramText;

        _settings.LastSelectedProfilePath = _selectedProfile.InstanceDirectory;
        _settingsStore.Save(_settings);
    }

    private void SyncModrinthFilters()
    {
        var rawVersion = _selectedProfile?.GameVersion ?? cbVersion.SelectedItem?.ToString() ?? string.Empty;
        // Basic cleanup: if they have "1.21.11" it might be a typo for "1.21.1" or they mean something else
        modrinthVersionInput.Text = rawVersion;
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

        if (instanceVersionCombo.SelectedItem is null)
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version before creating a profile.");
            return;
        }

        var selectedVersion = instanceVersionCombo.SelectedItem!.ToString()!;
        var loader = profileLoaderCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "vanilla";
        string? loaderVersion = null;

        try
        {
            ToggleBusyState(true, "Creating profile...");

            if (loader == "fabric")
                loaderVersion = await ResolveLatestFabricVersionAsync(selectedVersion, CancellationToken.None);
            else if (loader == "quilt")
                loaderVersion = await ResolveLatestQuiltVersionAsync(selectedVersion, CancellationToken.None);
            else if (loader == "forge")
                loaderVersion = await ResolveLatestForgeVersionAsync(selectedVersion, CancellationToken.None);
            else if (loader == "neoforge")
                loaderVersion = await ResolveLatestNeoForgeVersionAsync(selectedVersion, CancellationToken.None);

            var profile = _profileStore.CreateProfile(profileNameInput.Text.Trim(), selectedVersion, loader, loaderVersion, null, profileGameDirInput.Text?.Trim());
            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);
            else if (loader == "quilt")
                await EnsureQuiltProfileAsync(profile, CancellationToken.None);
            else if (loader == "forge" || loader == "neoforge")
                await EnsureForgeProfileAsync(profile, CancellationToken.None);

            // Ensure the required mods are installed automatically immediately
            var modsDir = Path.Combine(profile.InstanceDirectory, "mods");
            Directory.CreateDirectory(modsDir);
            await InstallModIfMissingAsync("customskinloader", profile, modsDir, CancellationToken.None);
            if (_settings.EnableFancyMenu && SupportsFancyMenu(profile))
            {
                await InstallModIfMissingAsync("fancymenu", profile, modsDir, CancellationToken.None);
                await InstallModIfMissingAsync("konkrete", profile, modsDir, CancellationToken.None);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles(profile);
                UpdateSelectedProjectDetails();
                profileNameInput.Text = string.Empty;
                _instanceEditorOverlay.IsVisible = false;
                SetProgressState($"Profile {profile.Name} is ready.", 0, 0);
            });
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
            else if (loader == "quilt")
                loaderVersion = await ResolveLatestQuiltVersionAsync(version, CancellationToken.None);
            else if (loader == "forge")
                loaderVersion = await ResolveLatestForgeVersionAsync(version, CancellationToken.None);
            else if (loader == "neoforge")
                loaderVersion = await ResolveLatestNeoForgeVersionAsync(version, CancellationToken.None);

            var profile = _profileStore.CreateProfile(autoName, version, loader, loaderVersion);

            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);
            else if (loader == "quilt")
                await EnsureQuiltProfileAsync(profile, CancellationToken.None);
            else if (loader == "forge" || loader == "neoforge")
                await EnsureForgeProfileAsync(profile, CancellationToken.None);

            // Ensure the required mods are installed automatically immediately
            var modsDir = Path.Combine(profile.InstanceDirectory, "mods");
            Directory.CreateDirectory(modsDir);
            await InstallModIfMissingAsync("customskinloader", profile, modsDir, CancellationToken.None);
            if (_settings.EnableFancyMenu && SupportsFancyMenu(profile))
            {
                await InstallModIfMissingAsync("fancymenu", profile, modsDir, CancellationToken.None);
                await InstallModIfMissingAsync("konkrete", profile, modsDir, CancellationToken.None);
            }

            // Pre-download the game files
            var launcherPath = new MinecraftPath(profile.InstanceDirectory);
            var launcher = CreateLauncher(launcherPath);
            await launcher.InstallAsync(version);

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles(profile);
                UpdateSelectedProjectDetails();
                SetProgressState($"Instance \"{autoName}\" ready to play!", 0, 0);
            });
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
            await InstallSelectedModAsync(project, CancellationToken.None, null); // We don't have a specific button here easily accessible, button is usually in the search results
            RefreshModsList();
            UpdateSelectedProjectDetails();
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

    private async Task<string> ResolveLatestQuiltVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        var payload = await _modrinthClient.GetStringAsync($"https://meta.quiltmc.org/v3/versions/loader/{gameVersion}", cancellationToken);
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
        throw new InvalidOperationException($"No Quilt loader build was found for Minecraft {gameVersion}.");
    }

    private async Task<string> ResolveLatestForgeVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        try 
        {
            var payload = await _modrinthClient.GetStringAsync($"https://bmclapi2.bangbang93.com/forge/minecraft/{gameVersion}", cancellationToken);
            using var json = JsonDocument.Parse(payload);
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("version", out var versionElement))
                {
                    var version = versionElement.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
            }
        } 
        catch { }
        throw new InvalidOperationException($"No Forge version could be auto-resolved for {gameVersion}.");
    }

    private async Task<string> ResolveLatestNeoForgeVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        try 
        {
            var payload = await _modrinthClient.GetStringAsync($"https://bmclapi2.bangbang93.com/neoforge/list/{gameVersion}", cancellationToken);
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() > 0)
            {
                var first = json.RootElement[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    var version = first.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
                else if (first.TryGetProperty("version", out var verElement))
                {
                    var version = verElement.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
            }
        } 
        catch { }
        throw new InvalidOperationException($"No NeoForge version could be auto-resolved for {gameVersion}.");
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
            // Re-bind ItemsSource in case AXAML re-created the controls
            modrinthResultsListBox.ItemsSource = _searchResults;
            _quickModResults.ItemsSource = _quickSearchResults;

            ToggleBusyState(true, "Searching across platforms...");

            var projectType = modrinthProjectTypeCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "mod";
            var gameVersion = string.IsNullOrWhiteSpace(modrinthVersionInput.Text) ? null : modrinthVersionInput.Text.Trim();
            var loader = NormalizeLoaderFilter();
            var source = modrinthSourceCombo.SelectedItem?.ToString() ?? "Modrinth";
            
            Task<IReadOnlyList<ModrinthProject>>? modrinthTask = null;
            Task<IReadOnlyList<ModrinthProject>>? curseForgeTask = null;

            if (source == "Modrinth")
                modrinthTask = _modrinthClient.SearchProjectsAsync(modrinthSearchInput.Text ?? "", projectType, gameVersion, loader, _searchCancellation.Token);
            else if (source == "CurseForge")
            {
                if (projectType == "mod")
                    curseForgeTask = _curseForgeClient.SearchModsAsync(modrinthSearchInput.Text ?? "", gameVersion, loader, _searchCancellation.Token);
                else if (projectType == "modpack")
                    curseForgeTask = _curseForgeClient.SearchPacksAsync(modrinthSearchInput.Text ?? "", gameVersion, _searchCancellation.Token);
            }

            var mrResults = modrinthTask != null ? await modrinthTask : [];
            var cfResults = curseForgeTask != null ? await curseForgeTask : [];

            var results = new List<ModrinthProject>(mrResults.Count + cfResults.Count);
            results.AddRange(mrResults);
            results.AddRange(cfResults);

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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                modrinthDetailsBox.Text = "No matching projects found. Check your filters (e.g. Version/Loader).";
                installSelectedButton.IsEnabled = false;
            }
        });
    }

    private Control BuildLayoutDeck()
    {
        var title = CreateSectionTitle("Client Layout", "Import a layout file to customize your launcher. Only the properties you specify in the file will be changed.");
        var style = _settings.Style;

        // Current style summary
        var styleInfo = new TextBlock
        {
            Text = $"Current: {style.BorderStyle} (radius {style.CornerRadius}px), nav={style.NavPosition}, sidebar={style.SidebarSide}{(style.SidebarCollapsed ? " [collapsed]" : "")}{(style.CompactMode ? ", compact" : "")}",
            Foreground = new SolidColorBrush(Color.Parse("#7A8AAA")),
            FontSize = 12,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Layout file import/reset
        var layoutSection = CreateSubCard("Layout File", new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Import an AXAML layout file to customize the launcher style. " +
                           "Only the properties you specify in the file (like window_shape=\"square\") are applied \u2014 everything else stays default.",
                    Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                },
                styleInfo,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        CreatePrimaryButton("Import Layout File", "#050505", Color.FromArgb(160, 120, 120, 120)).With(btn => {
                            btn.Click += async (_, _) => await ImportLayoutAsync();
                            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 110, 91, 255));
                        }),
                        CreateSecondaryButton("Reset To Default").With(btn => {
                            btn.Click += async (_, _) => await ResetLayoutAsync();
                        })
                    }
                }
            }
        }, "#1A2035");

        // Simple sidebar/nav toggles (quick access, no file needed)
        var sidebarToggle = new ToggleSwitch
        {
            Content = "Sidebar Position",
            OnContent = "Right",
            OffContent = "Left",
            IsChecked = IsSidebarOnRight(),
            Foreground = Brushes.White
        };
        sidebarToggle.IsCheckedChanged += (_, _) => {
            _settings.Style.SidebarSide = sidebarToggle.IsChecked == true ? "right" : "left";
            _settingsStore.Save(_settings);
            RebuildUiFromLayoutState(_activeSection);
        };

        var topNavToggle = new ToggleSwitch
        {
            Content = "Navigation Placement",
            OnContent = "Top",
            OffContent = "Sidebar",
            IsChecked = IsTopNavigationEnabled(),
            Foreground = Brushes.White
        };
        topNavToggle.IsCheckedChanged += (_, _) => {
            _settings.Style.NavPosition = topNavToggle.IsChecked == true ? "top" : "sidebar";
            if (topNavToggle.IsChecked == true) _settings.Style.SidebarCollapsed = false;
            _settingsStore.Save(_settings);
            RebuildUiFromLayoutState(_activeSection);
        };

        var collapseSidebarToggle = new ToggleSwitch
        {
            Content = "Sidebar Density",
            OnContent = "Collapsed",
            OffContent = "Expanded",
            IsChecked = IsSidebarCollapsed(),
            IsEnabled = !IsTopNavigationEnabled(),
            Foreground = Brushes.White
        };
        collapseSidebarToggle.IsCheckedChanged += (_, _) => {
            _settings.Style.SidebarCollapsed = collapseSidebarToggle.IsChecked == true;
            _settingsStore.Save(_settings);
            RebuildUiFromLayoutState(_activeSection);
        };

        var quickToggles = CreateSubCard("Quick Toggles", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                sidebarToggle,
                topNavToggle,
                collapseSidebarToggle
            }
        }, "#1A2035");

        // Accent colors
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
                }
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
            Children = { title, layoutSection, quickToggles, colorSection, backgroundSection, orderSection, fmSection }
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
            
            var upBtn = new Button { Content = "↑", Width = 32, Height = 32, Margin = new Thickness(4,0), Padding = new Thickness(0), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
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
            
            var downBtn = new Button { Content = "↓", Width = 32, Height = 32, Padding = new Thickness(0), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
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
            InvalidateUiCache();
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

        bool isInstalled = _selectedProfile?.InstalledModIds.Contains(project.ProjectId) ?? false;
        installSelectedButton.IsEnabled = !isInstalled;
        if (isInstalled)
        {
            SetButtonText(installSelectedButton, "Installed");
        }
        else
        {
            SetButtonText(installSelectedButton, project.ProjectType == "modpack" ? "↓ Pack" : "↓ Mod");
        }
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

    private void RefreshSearchList()
    {
        var items = modrinthResultsListBox.ItemsSource as IEnumerable<ModrinthProject>;
        if (items != null)
        {
            var list = items.ToList();
            modrinthResultsListBox.ItemsSource = null;
            modrinthResultsListBox.ItemsSource = list;
        }
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
                await InstallSelectedModAsync(project, CancellationToken.None, installSelectedButton);

            RefreshModsList();
            UpdateSelectedProjectDetails();
            SetButtonProgress(installSelectedButton, 0, false);
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

    private async Task InstallSelectedModAsync(ModrinthProject project, CancellationToken cancellationToken, Button? targetButton = null)
    {
        if (_selectedProfile is null)
        {
            await DialogService.ShowInfoAsync(this, "Profile required", "Create or select a profile before installing mods.");
            return;
        }

        if (project.IsCurseForge)
        {
            await InstallCurseForgeModAsync(project, cancellationToken, targetButton);
            return;
        }

        var versions = await _modrinthClient.GetProjectVersionsAsync(project.ProjectId, _selectedProfile.GameVersion, _selectedProfile.Loader, cancellationToken);
        var version = versions.FirstOrDefault(HasPrimaryFile) ?? versions.FirstOrDefault();
        if (version is null)
            throw new InvalidOperationException($"No compatible version was found for {_selectedProfile.LoaderDisplay}.");

        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { project.ProjectId };
        await InstallModVersionAsync(_selectedProfile, version, installed, cancellationToken, targetButton, project.ProjectId);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            SetProgressState($"Installed {project.Title} into {_selectedProfile.Name}.", 0, 0);
            RefreshSearchList();
        });
    }

    private async Task InstallCurseForgeModAsync(ModrinthProject project, CancellationToken cancellationToken, Button? targetButton = null)
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

        await _curseForgeClient.DownloadFileAsync(file.DownloadUrl, dest, CreateDownloadProgress(file.FileName, targetButton), cancellationToken);
        
        _selectedProfile.InstalledModIds.Add(project.ProjectId);
        _profileStore.Save(_selectedProfile);
        
        SetProgressState($"Installed {project.Title} (CurseForge) into {_selectedProfile.Name}.", 0, 0);
    }

    private static bool HasPrimaryFile(ModrinthProjectVersion version) =>
        version.Files.Any(file => file.Primary && file.Filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private async Task InstallModVersionAsync(LauncherProfile profile, ModrinthProjectVersion version, HashSet<string> installedProjectIds, CancellationToken cancellationToken, Button? targetButton = null, string? projectId = null)
    {
        foreach (var dependency in version.Dependencies.Where(d => d.DependencyType == "required" && !string.IsNullOrWhiteSpace(d.ProjectId)))
        {
            if (!installedProjectIds.Add(dependency.ProjectId!))
                continue;

            var dependencyVersions = await _modrinthClient.GetProjectVersionsAsync(dependency.ProjectId!, profile.GameVersion, profile.Loader, cancellationToken);
            var dependencyVersion = dependencyVersions.FirstOrDefault(HasPrimaryFile) ?? dependencyVersions.FirstOrDefault();
            if (dependencyVersion is not null)
                await InstallModVersionAsync(profile, dependencyVersion, installedProjectIds, cancellationToken, targetButton, dependency.ProjectId);
        }

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException($"Version {version.VersionNumber} did not include a downloadable file.");

        Directory.CreateDirectory(profile.ModsDirectory);
        var destinationPath = Path.Combine(profile.ModsDirectory, file.Filename);
        await _modrinthClient.DownloadFileAsync(file.Url, CreateDownloadDestination(destinationPath), CreateDownloadProgress(file.Filename, targetButton), cancellationToken);
        await VerifyFileHashAsync(destinationPath, file.Hashes);
        
        var pid = projectId ?? version.ProjectId;
        if (!string.IsNullOrEmpty(pid))
            profile.InstalledModIds.Add(pid);
            
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
        else if (loader == "quilt")
            await EnsureQuiltProfileAsync(profile, cancellationToken);
        else if (loader == "forge" || loader == "neoforge")
            await EnsureForgeProfileAsync(profile, cancellationToken);

        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            RefreshProfiles(profile);
            SetActiveSection("profiles");
            SetProgressState($"Installed modpack {profile.Name}.", 0, 0);
        });
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

    private Progress<(long BytesRead, long? TotalBytes)> CreateDownloadProgress(string fileName, Button? targetButton = null)
    {
        return new Progress<(long BytesRead, long? TotalBytes)>(progress =>
        {
            statusLabel.Text = $"Downloading {Path.GetFileName(fileName)}";
            double percent = 0;
            if (progress.TotalBytes is long totalBytes && totalBytes > 0)
            {
                percent = progress.BytesRead * 100d / totalBytes;
                pbProgress.Value = Math.Min(100, percent);
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} / {FormatBytes(totalBytes)}";
            }
            else
            {
                pbProgress.Value = 0;
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} downloaded";
            }

            if (targetButton != null)
            {
                SetButtonProgress(targetButton, percent > 0 ? percent : 0, true);
            }
        });
    }

    private void ToggleBusyState(bool isBusy, string statusText)
    {
        btnStart.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(usernameInput.Text);
        if (isBusy)
        {
            btnStart.Content = "Cancel"; // Default busy state for launch
        }
        else
        {
            btnStart.Content = "▶ Play";
        }
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
        if (_homeStatusBar != null) _homeStatusBar.IsVisible = isBusy;
        if (!isBusy)
        {
            pbProgress.Value = 0;
            if (installSelectedButton != null) SetButtonProgress(installSelectedButton, 0, false);
            if (btnStart != null) SetButtonProgress(btnStart, 0, false);
            if (modrinthSearchButton != null) SetButtonProgress(modrinthSearchButton, 0, false);
        }
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

    private Border CreateCompactStat(string title, TextBlock valueBlock)
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
                                DetachFromParent(heroInstanceLabel)!,
                                DetachFromParent(heroPerformanceLabel)!,
                                new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
                                    CornerRadius = new CornerRadius(14),
                                    Padding = new Thickness(14, 10),
                                    Child = DetachFromParent(usernameInput)!
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

    // Removed static keyword to access _settings
    private TextBox CreateTextBox()
    {
        var style = _settings.Style;
        var inBg = !string.IsNullOrWhiteSpace(style.FieldBackground) ? style.FieldBackground : "#78131B2D";
        var inFg = !string.IsNullOrWhiteSpace(style.FieldForeground) ? style.FieldForeground : "#FFFFFF";
        var inBorder = !string.IsNullOrWhiteSpace(style.FieldBorderColor) ? style.FieldBorderColor : "#36476A";
        var inCr = double.IsNaN(style.FieldRadius) ? 16 : style.FieldRadius;

        return new TextBox
        {
            Background = new SolidColorBrush(Color.Parse(inBg)),
            Foreground = new SolidColorBrush(Color.Parse(inFg)),
            BorderBrush = new SolidColorBrush(Color.Parse(inBorder)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 11),
            CornerRadius = new CornerRadius(inCr),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
    }

    private ComboBox CreateComboBox(IEnumerable<object> items)
    {
        var style = _settings.Style;
        var inBg = !string.IsNullOrWhiteSpace(style.FieldBackground) ? style.FieldBackground : "#78131B2D";
        var inFg = !string.IsNullOrWhiteSpace(style.FieldForeground) ? style.FieldForeground : "#FFFFFF";
        var inBorder = !string.IsNullOrWhiteSpace(style.FieldBorderColor) ? style.FieldBorderColor : "#36476A";
        var inCr = double.IsNaN(style.FieldRadius) ? 16 : style.FieldRadius;

        var comboBox = new ComboBox
        {
            ItemsSource = items.ToList(),
            Background = new SolidColorBrush(Color.Parse(inBg)),
            Foreground = new SolidColorBrush(Color.Parse(inFg)),
            BorderBrush = new SolidColorBrush(Color.Parse(inBorder)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(inCr),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(comboBox);
        return comboBox;
    }

    private ComboBox CreateComboBox(IEnumerable<string> items)
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

    private Button CreatePrimaryButton(string text, string hexColor, Color foreground)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var progressBar = new ProgressBar
        {
            Height = 4,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 2),
            IsVisible = false,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            CornerRadius = new CornerRadius(2)
        };

        var contentGrid = new Grid
        {
            Children = { textBlock, progressBar }
        };

        var button = new Button
        {
            Content = contentGrid,
            Tag = progressBar, // Store progress bar for easy access
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

    private static void SetButtonText(Button button, string text)
    {
        if (button.Content is Grid grid)
        {
            var textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock != null)
            {
                textBlock.Text = text;
                return;
            }
        }
        button.Content = text;
    }

    private static void SetButtonProgress(Button button, double value, bool visible)
    {
        if (button.Tag is ProgressBar pb)
        {
            pb.Value = value;
            pb.IsVisible = visible;
        }
    }

    private Button CreateNavButton(string icon, string label, bool compact = false)
    {
        var style = _settings.Style;
        var buttonHeight = double.IsNaN(style.NavButtonHeight) ? (compact ? 48 : 46) : style.NavButtonHeight;
        var buttonFontSize = double.IsNaN(style.NavButtonFontSize) ? 14 : style.NavButtonFontSize;
        var hAlign = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        
        var iconSize = double.IsNaN(style.NavButtonFontSize) ? (compact ? 18 : 15) : style.NavButtonFontSize + 3;

        var button = new Button
        {
            Content = compact
                ? (object)new TextBlock
                {
                    Text = icon,
                    FontSize = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
                : new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = icon,
                            FontSize = iconSize,
                            Width = 22,
                            TextAlignment = TextAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = label,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = buttonFontSize,
                            FontWeight = FontWeight.SemiBold
                        }
                    }
                },
            Width = compact ? 48 : double.NaN,
            Height = buttonHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = !string.IsNullOrWhiteSpace(style.NavButtonBackground) ? new SolidColorBrush(Color.Parse(style.NavButtonBackground)) : Brushes.Transparent,
            Foreground = !string.IsNullOrWhiteSpace(style.NavButtonForeground) ? new SolidColorBrush(Color.Parse(style.NavButtonForeground)) : new SolidColorBrush(Color.Parse("#A4A8B1")),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(style.NavButtonCornerRadius),
            FontWeight = FontWeight.SemiBold,
            FontSize = buttonFontSize,
            HorizontalContentAlignment = hAlign,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = compact ? new Thickness(0) : new Thickness(16, 0),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(button);
        return button;
    }

    private Button CreateSecondaryButton(string text)
    {
        var style = _settings.Style;
        var btnHeight = double.IsNaN(style.ButtonHeight) ? 48 : style.ButtonHeight;
        var btnFs = double.IsNaN(style.ButtonFontSize) ? 14 : style.ButtonFontSize;
        var btnCr = double.IsNaN(style.ButtonCornerRadius) ? 18 : style.ButtonCornerRadius;
        var btnPad = double.IsNaN(style.ButtonPadding) ? 18 : style.ButtonPadding;
        
        var bg = !string.IsNullOrWhiteSpace(style.ButtonBackground) ? style.ButtonBackground : "#55101728";
        var fg = !string.IsNullOrWhiteSpace(style.ButtonForeground) ? style.ButtonForeground : "#FFFFFF";

        var button = new Button
        {
            Content = text,
            Height = btnHeight,
            Background = new SolidColorBrush(Color.Parse(bg)),
            Foreground = new SolidColorBrush(Color.Parse(fg)),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C4F73")),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(btnPad, 12),
            CornerRadius = new CornerRadius(btnCr),
            FontFamily = new FontFamily("Inter, Segoe UI"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ApplyHoverMotion(button);
        return button;
    }

    private Button CreateCompactSecondaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Height = 30,
            MinWidth = 110,
            Background = new SolidColorBrush(Color.FromArgb(85, 16, 23, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3C4F73")),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(12),
            FontFamily = new FontFamily("Inter, Segoe UI"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ApplyHoverMotion(button);
        return button;
    }

    private Border BuildCard(Control child)
    {
        var style = _settings.Style;
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(style.CardBackground ?? "#0D1522")),
            BorderBrush = new SolidColorBrush(Color.Parse(style.CardBorderColor ?? "#203046")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(double.IsNaN(style.CardCornerRadius) ? 24 : style.CardCornerRadius),
            Padding = new Thickness(double.IsNaN(style.CardPadding) ? 22 : style.CardPadding),
            Child = child
        };
    }

    private Border CreateGlassPanel(Control child, Thickness? padding = null, Thickness? margin = null)
    {
        var style = _settings.Style;
        var panel = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(20, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(5, 255, 255, 255), 1)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(double.IsNaN(style.CardCornerRadius) ? 24 : style.CardCornerRadius),
            Padding = padding ?? new Thickness(22),
            Margin = margin ?? new Thickness(0),
            Child = child
        };
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

    private Control CreateSectionTitle(string text, string subtitle)
    {
        var style = _settings.Style;
        
        var titleText = !string.IsNullOrWhiteSpace(style.TitleText) && text == "Home" ? style.TitleText : text;
        var titleFs = double.IsNaN(style.TitleFontSize) ? 32 : style.TitleFontSize;
        var titleFg = !string.IsNullOrWhiteSpace(style.TitleForeground) ? style.TitleForeground : "#FFFFFF";
        var primaryFont = !string.IsNullOrWhiteSpace(style.PrimaryFontFamily) ? new FontFamily(style.PrimaryFontFamily) : new FontFamily("Inter, Segoe UI");
        var secondaryFg = !string.IsNullOrWhiteSpace(style.SecondaryForeground) ? style.SecondaryForeground : "#A4B4DA";

        return new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 0, 20),
            Children =
            {
                new TextBlock
                {
                    Text = titleText,
                    FontSize = titleFs,
                    FontWeight = FontWeight.Black,
                    Foreground = new SolidColorBrush(Color.Parse(titleFg)),
                    LetterSpacing = 1.2,
                    FontFamily = primaryFont
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(Color.Parse(secondaryFg)),
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = primaryFont
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
            Padding = new Thickness(0, 0, 16, 0),
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

    private Border CreateMetricTile(string title, string subtitle)
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

    private Border CreateSubCard(string title, Control body, string backgroundHex)
    {
        var style = _settings.Style;
        var bg = !string.IsNullOrWhiteSpace(style.CardBackground) ? style.CardBackground : backgroundHex;
        var border = !string.IsNullOrWhiteSpace(style.CardBorderColor) ? style.CardBorderColor : "#21364F";
        var cr = double.IsNaN(style.CardCornerRadius) ? 20 : style.CardCornerRadius;
        var pad = double.IsNaN(style.CardPadding) ? 18 : style.CardPadding;

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(bg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cr),
            Padding = new Thickness(pad),
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

    private void ApplyNavState(Button? button, bool isActive)
    {
        if (button == null) return;
        if (button == accountsNavButton) return;

        var style = _settings.Style;
        var accentColor = Color.Parse(_settings.AccentColor);

        var activeBgToken = !string.IsNullOrWhiteSpace(style.NavButtonActiveBackground) ? style.NavButtonActiveBackground : null;
        var inactiveBgToken = !string.IsNullOrWhiteSpace(style.NavButtonBackground) ? style.NavButtonBackground : null;

        var activeFgToken = !string.IsNullOrWhiteSpace(style.NavButtonActiveForeground) ? style.NavButtonActiveForeground : null;
        var inactiveFgToken = !string.IsNullOrWhiteSpace(style.NavButtonForeground) ? style.NavButtonForeground : "#A4A8B1";

        if (isActive)
        {
            button.BorderThickness = new Thickness(0);

            switch (style.NavIndicatorStyle?.ToLower())
            {
                case "left-pill":
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : Brushes.Transparent;
                    button.BorderThickness = new Thickness(4, 0, 0, 0);
                    button.BorderBrush = new SolidColorBrush(accentColor);
                    break;
                case "underline":
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : Brushes.Transparent;
                    button.BorderThickness = new Thickness(0, 0, 0, 2);
                    button.BorderBrush = new SolidColorBrush(accentColor);
                    break;
                case "glow":
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : Brushes.Transparent;
                    button.Foreground = new SolidColorBrush(accentColor);
                    break;
                case "fill":
                default:
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : new SolidColorBrush(Color.FromArgb(32, accentColor.R, accentColor.G, accentColor.B));
                    button.Foreground = activeFgToken != null ? new SolidColorBrush(Color.Parse(activeFgToken)) : new SolidColorBrush(accentColor);
                    break;
            }
            if (activeFgToken != null) button.Foreground = new SolidColorBrush(Color.Parse(activeFgToken));
        }
        else
        {
            button.Background = inactiveBgToken != null ? new SolidColorBrush(Color.Parse(inactiveBgToken)) : Brushes.Transparent;
            button.Foreground = new SolidColorBrush(Color.Parse(inactiveFgToken));
            button.BorderThickness = new Thickness(0);
            button.BorderBrush = Brushes.Transparent;
        }

        button.CornerRadius = new CornerRadius(double.IsNaN(style.NavButtonCornerRadius) ? 14 : style.NavButtonCornerRadius);
        button.Padding = new Thickness(16, 0);
        button.FontSize = double.IsNaN(style.NavButtonFontSize) ? 14 : style.NavButtonFontSize;
        button.FontWeight = isActive ? FontWeight.Bold : FontWeight.Normal;

    }

    private Border CreateStatTile(string title, TextBlock valueBlock, string subtitle)
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

    private async Task InstallModIfMissingAsync(string slug, LauncherProfile profile, string modsDir, CancellationToken cancellationToken, string? projectId = null)
    {
        try
        {
            if (string.Equals(profile.Loader, "vanilla", StringComparison.OrdinalIgnoreCase))
                return;

            string targetId = projectId ?? slug;
            if (profile.InstalledModIds.Contains(targetId))
            {
                LauncherLog.Info($"[ModInstaller] {targetId} is already tracked. Done.");
                return;
            }

            // We search first to get the official Project ID if not provided.
            LauncherLog.Info($"[ModInstaller] Resolving official ID for {slug} ({profile.GameVersion}/{profile.Loader})...");
            var results = await _modrinthClient.SearchProjectsAsync(targetId, "mod", profile.GameVersion, profile.Loader, cancellationToken);
            var project = results.FirstOrDefault(p => 
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(p.ProjectId, slug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProjectId, projectId, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(slug, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                LauncherLog.Info($"[ModInstaller] Could not find {slug} on Modrinth. Skipping auto-install.");
                return;
            }

            if (profile.InstalledModIds.Contains(project.ProjectId))
            {
                LauncherLog.Info($"[ModInstaller] {project.Title} ({project.ProjectId}) is already tracked. Done.");
                return;
            }

            // Check if the file already exists physically but isn't tracked yet
            var existing = Directory.EnumerateFiles(modsDir, "*.jar")
                .Any(f => Path.GetFileName(f).Contains(slug, StringComparison.OrdinalIgnoreCase));

            if (existing)
            {
                LauncherLog.Info($"[ModInstaller] {project.Title} exists physically but wasn't tracked. Adding ID {project.ProjectId}.");
                profile.InstalledModIds.Add(project.ProjectId);
                _profileStore.Save(profile);
                return;
            }

            LauncherLog.Info($"[ModInstaller] Found {project.Title}. Installing...");
            await InstallSelectedModAsync(project, cancellationToken);
            LauncherLog.Info($"[ModInstaller] {project.Title} installed successfully.");
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"[ModInstaller] Auto-installation of {slug} failed, but continuing instance operation.", ex);
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

    private void EnsureDeathClientThemeResourcePack(string instancePath, string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(instancePath))
            return;

        try
        {
            var rpDir = Path.Combine(instancePath, "resourcepacks");
            Directory.CreateDirectory(rpDir);
            var zipPath = Path.Combine(rpDir, "DeathClientTheme.zip");

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteTextEntry(
                    archive,
                    "pack.mcmeta",
                    "{\"pack\":{\"pack_format\":1,\"description\":\"Aether Launcher UI theme for home, multiplayer, and singleplayer menus\"}}");

                AddExistingFileToArchive(archive, ResolveThemeLogoPath(), "pack.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_title_logo.png"), "assets/minecraft/textures/gui/title/minecraft.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_title_logo.png"), "assets/minecraft/textures/gui/title/minceraft.png");
                WriteTextEntry(archive, "assets/minecraft/textures/gui/title/minecraft.png.mcmeta", "{\"animation\":{\"frametime\":5}}");
                WriteTextEntry(archive, "assets/minecraft/textures/gui/title/minceraft.png.mcmeta", "{\"animation\":{\"frametime\":5}}");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_edition.png"), "assets/minecraft/textures/gui/title/edition.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_button.png"), "assets/minecraft/textures/gui/sprites/widget/button.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_button_highlighted.png"), "assets/minecraft/textures/gui/sprites/widget/button_highlighted.png");
                WriteTextEntry(archive, "assets/minecraft/textures/gui/sprites/widget/button_highlighted.png.mcmeta", "{\"animation\":{\"frametime\":4}}");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_button_disabled.png"), "assets/minecraft/textures/gui/sprites/widget/button_disabled.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_widgets.png"), "assets/minecraft/textures/gui/widgets.png");

                var themeBackground = ResolveThemeBackgroundPath();
                var panoramaBackground = ResolveThemePanoramaPath();
                if (!string.IsNullOrWhiteSpace(panoramaBackground) && IsSquareImage(panoramaBackground))
                {
                    for (var i = 0; i < 6; i++)
                        AddExistingFileToArchive(archive, panoramaBackground, $"assets/minecraft/textures/gui/title/background/panorama_{i}.png");
                }

                if (!string.IsNullOrWhiteSpace(themeBackground))
                    AddExistingFileToArchive(archive, themeBackground, "assets/minecraft/textures/gui/options_background.png");

                WriteTextEntry(
                    archive,
                    "assets/minecraft/texts/splashes.txt",
                    "Aether Launcher: Redefining Play\nUnrivaled Performance, Unmatched Style\nQueue up and dominate\nPeak precision, crafted for champions\nCleanest UI, fastest launch\nOffline mode, but never basic\nJoin the Reborn Movement");

                AddSkinAndCapeEntries(archive);
            }

            UpdateResourcePackOptions(instancePath, "file/DeathClientTheme.zip");
        }
        catch { }
    }

    private void AddSkinAndCapeEntries(ZipArchive archive)
    {
        var allowSkinOverride = !IsUsingMicrosoftAccount() || HasManualSkinOverride();
        var allowCapeOverride = !IsUsingMicrosoftAccount() || HasManualCapeOverride();

        if (allowSkinOverride && !string.IsNullOrWhiteSpace(_settings.CustomSkinPath) && File.Exists(_settings.CustomSkinPath))
        {
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/steve.png");
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/alex.png");
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/player/wide/steve.png");
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/player/slim/alex.png");
        }

        if (allowCapeOverride && !string.IsNullOrWhiteSpace(_settings.CustomCapePath) && File.Exists(_settings.CustomCapePath))
        {
            AddExistingFileToArchive(archive, _settings.CustomCapePath, "assets/minecraft/textures/entity/cape.png");
            AddExistingFileToArchive(archive, _settings.CustomCapePath, "assets/minecraft/textures/entity/elytra.png");
        }
    }

    private void UpdateResourcePackOptions(string instancePath, string packName)
    {
        var optionsPath = Path.Combine(instancePath, "options.txt");
        var lines = File.Exists(optionsPath)
            ? File.ReadAllLines(optionsPath).ToList()
            : [];

        UpsertOptionList(lines, "resourcePacks", packName, includeVanilla: true);
        UpsertOptionList(lines, "incompatibleResourcePacks", packName, includeVanilla: false);
        File.WriteAllLines(optionsPath, lines);
    }

    private static void UpsertOptionList(List<string> lines, string key, string value, bool includeVanilla)
    {
        var index = lines.FindIndex(line => line.StartsWith($"{key}:"));
        var values = index >= 0
            ? ParseOptionList(lines[index])
            : [];

        values.RemoveAll(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        values.Insert(0, value);

        if (includeVanilla && !values.Contains("vanilla", StringComparer.OrdinalIgnoreCase))
            values.Add("vanilla");

        var rendered = string.Join(",", values.Select(item => $"\"{item}\""));
        var nextLine = $"{key}:[{rendered}]";

        if (index >= 0)
            lines[index] = nextLine;
        else
            lines.Add(nextLine);
    }

    private static List<string> ParseOptionList(string line)
    {
        var startIndex = line.IndexOf('[');
        var endIndex = line.LastIndexOf(']');
        if (startIndex < 0 || endIndex <= startIndex)
            return [];

        return line[(startIndex + 1)..endIndex]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().Trim('\"'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveThemeBackgroundPath()
    {
        var customBackground = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBackground))
            return customBackground;

        var bundledBackground = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_menu_background.png");
        if (File.Exists(bundledBackground))
            return bundledBackground;

        return string.Empty;
    }

    private string ResolveThemeLogoPath()
    {
        var bundledLogo = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_logo.png");
        if (File.Exists(bundledLogo))
            return bundledLogo;

        return ResolveThemeBackgroundPath();
    }

    private static string ResolveBundledThemeAsset(string fileName)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        if (File.Exists(bundled))
            return bundled;

        return string.Empty;
    }

    private string ResolveThemePanoramaPath()
    {
        var customBackground = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBackground) && IsSquareImage(customBackground))
            return customBackground;

        var bundledPanorama = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_panorama.png");
        if (File.Exists(bundledPanorama))
            return bundledPanorama;

        return string.Empty;
    }

    private static void AddExistingFileToArchive(ZipArchive archive, string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        archive.CreateEntryFromFile(sourcePath, destinationPath);
    }

    private static bool IsSquareImage(string path)
    {
        try
        {
            using var bitmap = new Bitmap(path);
            return bitmap.PixelSize.Width == bitmap.PixelSize.Height;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static bool SupportsFancyMenu(LauncherProfile profile)
    {
        var loader = profile.Loader?.Trim().ToLowerInvariant();
        if (loader != "fabric" && loader != "quilt")
            return false;

        return IsFancyMenuCapableVersion(profile.GameVersion);
    }

    private static bool IsFancyMenuCapableVersion(string version)
    {
        var match = Regex.Match(version, @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?");
        if (!match.Success)
            return false;

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);

        if (major >= 24)
            return true;

        return major > 1 || (major == 1 && minor >= 19);
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

    private void ApplyHoverMotion(Control? control)
    {
        if (control == null) return;
        control.Transitions = new Transitions
        {
            new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
        };
        
        IBrush? originalBg = null;
        IBrush? originalFg = null;
        IBrush? originalBorder = null;
        bool captured = false;
        
        control.PointerEntered += (s, e) =>
        {
            control.Opacity = 0.85;
            control.RenderTransform = TransformOperations.Parse("scale(1.025)");
            
            if (control is Button btn)
            {
                if (!captured)
                {
                    originalBg = btn.Background;
                    originalFg = btn.Foreground;
                    originalBorder = btn.BorderBrush;
                    captured = true;
                }
                
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBackground)) btn.Background = new SolidColorBrush(Color.Parse(_settings.Style.ButtonHoverBackground));
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverForeground)) btn.Foreground = new SolidColorBrush(Color.Parse(_settings.Style.ButtonHoverForeground));
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBorderColor)) btn.BorderBrush = new SolidColorBrush(Color.Parse(_settings.Style.ButtonHoverBorderColor));
            }
        };
        control.PointerExited += (s, e) =>
        {
            control.Opacity = 1.0;
            control.RenderTransform = TransformOperations.Parse("scale(1.0)");
            if (control is Button btn && captured)
            {
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBackground)) btn.Background = originalBg;
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverForeground)) btn.Foreground = originalFg;
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBorderColor)) btn.BorderBrush = originalBorder;
            }
        };
    }

    public async Task ChangeSkinAsync()
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

    public async Task ChangeCapeAsync()
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
    private int GetSystemRamMb()
    {
        try
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                var info = File.ReadAllText("/proc/meminfo");
                var match = Regex.Match(info, @"MemTotal:\s+(\d+)\s+kB");
                if (match.Success) return int.Parse(match.Groups[1].Value) / 1024;
            }
            return (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
        }
        catch { return 8192; } // Fallback to 8GB
    }

    private async Task ExportProfileAsync(LauncherProfile profile)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "Select Export Destination" });
            if (folder == null || folder.Count == 0) return;

            var exportPath = Path.Combine(folder[0].Path.LocalPath, $"{profile.Name}_backup.zip");
            if (File.Exists(exportPath)) File.Delete(exportPath);

            ToggleBusyState(true, $"Exporting {profile.Name}...");

            await Task.Run(() => {
                using var zip = System.IO.Compression.ZipFile.Open(exportPath, System.IO.Compression.ZipArchiveMode.Create);
                
                // Manifest
                var manifestPath = Path.Combine(profile.InstanceDirectory, LauncherProfile.ManifestFileName);
                if (File.Exists(manifestPath))
                    zip.CreateEntryFromFile(manifestPath, LauncherProfile.ManifestFileName);
                
                // Mods
                if (Directory.Exists(profile.ModsDirectory))
                {
                    foreach (var file in Directory.GetFiles(profile.ModsDirectory))
                        zip.CreateEntryFromFile(file, Path.Combine("mods", Path.GetFileName(file)));
                }

                // Config
                var configDir = Path.Combine(profile.InstanceDirectory, "config");
                if (Directory.Exists(configDir))
                {
                    foreach (var file in Directory.GetFiles(configDir, "*", SearchOption.AllDirectories))
                    {
                        var relPath = Path.GetRelativePath(profile.InstanceDirectory, file);
                        zip.CreateEntryFromFile(file, relPath);
                    }
                }
            });

            await DialogService.ShowInfoAsync(this, "Export Success", $"Profile exported to {exportPath}");
        }
        catch (Exception ex) { await DialogService.ShowInfoAsync(this, "Export Failed", ex.Message); }
        finally { ToggleBusyState(false, "Ready."); }
    }

    public async Task ImportProfileZipAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions 
            { 
                Title = "Select Profile Backup (.zip)",
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Backup Zip") { Patterns = ["*.zip"] }]
            });
            if (files == null || files.Count == 0) return;

            ToggleBusyState(true, "Importing profile...");
            
            await Task.Run(() => {
                var zipPath = files[0].Path.LocalPath;
                using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
                
                var manifestEntry = zip.GetEntry(LauncherProfile.ManifestFileName);
                if (manifestEntry == null) throw new Exception("Manifest not found in zip.");

                LauncherProfile? profile;
                using (var stream = manifestEntry.Open())
                {
                    profile = JsonSerializer.Deserialize<LauncherProfile>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                if (profile == null) throw new Exception("Invalid manifest.");

                var targetDir = Path.Combine(_profileStore.GetInstancesRoot(), Slugify(profile.Name));
                int counter = 1;
                while (Directory.Exists(targetDir))
                {
                    targetDir = Path.Combine(_profileStore.GetInstancesRoot(), $"{Slugify(profile.Name)}-{counter++}");
                }

                Directory.CreateDirectory(targetDir);
                foreach (var entry in zip.Entries)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                    if (!fullPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase)) continue;

                    if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(fullPath);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                        entry.ExtractToFile(fullPath, true);
                    }
                }
                
                // Update the manifest with the new directory
                profile.InstanceDirectory = targetDir;
                _profileStore.Save(profile);
            });

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles();
            });
            await DialogService.ShowInfoAsync(this, "Import Success", "The profile has been imported successfully.");
        }
        catch (Exception ex) { await DialogService.ShowInfoAsync(this, "Import Failed", ex.Message); }
        finally { ToggleBusyState(false, "Ready."); }
    }

    public async Task ImportInstanceFolderAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions 
            { 
                Title = "Select Instance Directory" 
            });
            if (folders == null || folders.Count == 0) return;

            var folderPath = folders[0].Path.LocalPath;
            var folderName = Path.GetFileName(folderPath);
            
            // Basic detection for Fabric/Quilt/Forge
            string loader = "vanilla";
            string gameVersion = _settings.Version; // Default from latest selected or 1.21.1
            if (string.IsNullOrEmpty(gameVersion)) gameVersion = "1.21.1";

            if (Directory.Exists(Path.Combine(folderPath, "mods")))
            {
                loader = "fabric"; // Most common for custom folders, or can be detected via jar scan
            }

            var profile = _profileStore.CreateProfile(folderName, gameVersion, loader, null);
            profile.InstanceDirectory = folderPath; // Redirect to external path
            _profileStore.Save(profile);
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles(profile);
                SetActiveSection("profiles");
            });
            await DialogService.ShowInfoAsync(this, "Import Success", $"Successfully imported {folderName} as an instance.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Import Error", ex.Message);
        }
        finally { ToggleBusyState(false, "Ready."); }
    }

    private string Slugify(string value)
    {
        return Regex.Replace(value.ToLower(), @"[^a-z0-9]", "-").Trim('-');
    }

    private async Task ScanForModConflictsAsync(LauncherProfile profile)
    {
        if (!Directory.Exists(profile.ModsDirectory)) return;

        var logs = new List<string>();
        var modVersions = new Dictionary<string, string>(); // id -> version

        try
        {
            var jars = Directory.GetFiles(profile.ModsDirectory, "*.jar");
            foreach (var jar in jars)
            {
                try {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(jar);
                    var fabricJson = zip.GetEntry("fabric.mod.json");
                    if (fabricJson != null)
                    {
                        using var stream = fabricJson.Open();
                        using var doc = JsonDocument.Parse(stream);
                        if (doc.RootElement.TryGetProperty("id", out var idProp))
                        {
                            var id = idProp.GetString() ?? "";
                            var version = doc.RootElement.TryGetProperty("version", out var vProp) ? vProp.GetString() : "0.0.0";
                            if (!string.IsNullOrEmpty(id)) modVersions[id] = version ?? "";
                        }
                    }
                } catch { /* Skip malformed jars */ }
            }

            foreach (var jar in jars)
            {
                try {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(jar);
                    var fabricJson = zip.GetEntry("fabric.mod.json");
                    if (fabricJson != null)
                    {
                        using var stream = fabricJson.Open();
                        using var doc = JsonDocument.Parse(stream);
                        var modId = doc.RootElement.GetProperty("id").GetString();
                        if (doc.RootElement.TryGetProperty("depends", out var depends))
                        {
                            foreach (var dep in depends.EnumerateObject())
                            {
                                if (dep.Name == "minecraft" || dep.Name == "fabricloader" || dep.Name == "java" || dep.Name == "fabric") continue;
                                if (!modVersions.ContainsKey(dep.Name))
                                    logs.Add($"• {modId} needs '{dep.Name}' but it's missing.");
                            }
                        }
                    }
                } catch { }
            }

            if (logs.Count == 0)
                await DialogService.ShowInfoAsync(this, "Scan Complete", "No obvious missing dependencies found in fabric.mod.json files.");
            else
                await DialogService.ShowInfoAsync(this, "Potential Conflicts", "Missing dependencies found:\n\n" + string.Join("\n", logs));
        }
        catch (Exception ex) { await DialogService.ShowInfoAsync(this, "Scan Failed", ex.Message); }
    }
    private void UpdateResponsiveLayout()
    {
        if (_avatarGlass == null || _avatarControls == null || _avatarActions == null || _mainContentStack == null) return;

        double threshold = 1180; // Slightly higher threshold for safe floating
        _isNarrowMode = this.Bounds.Width < threshold;

        if (_isNarrowMode)
        {
            _mainContentStack.Margin = new Thickness(0); // Content fills screen
            SetAvatarExpansion(false);
        }
        else
        {
            _mainContentStack.Margin = new Thickness(0, 0, 320, 0); // Content respects panel
            _avatarGlass.Background = new LinearGradientBrush { 
                GradientStops = { new GradientStop(Color.FromArgb(60, 25, 31, 56), 0), new GradientStop(Color.FromArgb(30, 15, 21, 36), 1) } 
            };
            _avatarGlass.BorderThickness = new Thickness(1);
            _avatarGlass.IsHitTestVisible = true;
            _avatarControls.Children[0].IsVisible = true;
            _avatarControls.Children[2].IsVisible = true;
            _avatarActions.IsVisible = true;
            _avatarActions.Opacity = 1;
        }
    }

    private void SetAvatarExpansion(bool expanded)
    {
        if (!_isNarrowMode || _avatarGlass == null || _avatarControls == null || _avatarActions == null) return;

        if (expanded)
        {
            _avatarGlass.Background = new SolidColorBrush(Color.FromArgb(200, 9, 12, 18));
            _avatarGlass.BorderThickness = new Thickness(1);
            _avatarControls.Children[0].IsVisible = true;
            _avatarControls.Children[2].IsVisible = true;
            _avatarActions.IsVisible = true;
            _avatarActions.Opacity = 1;
        }
        else
        {
            _avatarGlass.Background = Brushes.Transparent;
            _avatarGlass.BorderThickness = new Thickness(0);
            _avatarControls.Children[0].IsVisible = false;
            _avatarControls.Children[2].IsVisible = false;
            _avatarActions.IsVisible = false;
            _avatarActions.Opacity = 0;
        }
    }

    private Color GetAccentColor(byte alpha)
    {
        try
        {
            var color = Color.Parse(_settings.AccentColor);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }
        catch
        {
            return Color.FromArgb(alpha, 110, 91, 255); // Fallback to #6E5BFF
        }
    }

    private static TextBlock CreateStatusTextBlock() => new()
    {
        Foreground = Brushes.White,
        FontWeight = FontWeight.SemiBold
    };

    private static TextBlock CreateMutedTextBlock() => new()
    {
        Foreground = new SolidColorBrush(Color.Parse("#A0A8B8"))
    };

    private void UsernameInput_TextChanged(object? sender, TextChangedEventArgs e) => UsernameInput_TextChanged();

    private void CbVersion_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SyncModrinthFilters();
        UpdateLauncherContext();
        UpdateCharacterPreview();
    }

    private async void MinecraftVersion_SelectionChanged(object? sender, SelectionChangedEventArgs e) => await ListVersionsAsync(GetSelectedVersionCategory());
    private async void DownloadVersionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DownloadSelectedVersionAsync();
    private async void RenameProfileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await RenameSelectedProfileAsync();
    private async void ClearProfileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DeleteSelectedProfileAsync();
    private async void ImportMrpackButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ImportMrpackAsync();
    private async void QuickInstallButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await QuickInstallInstanceAsync();
    private async void QuickModSearchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await QuickModSearchAsync();
    private void ProfileListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ProfileListBox_SelectionChanged();
    private void ModrinthResultsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateSelectedProjectDetails();

    private async Task PerformFirstRunSetup()
    {
        if (!_settings.IsFirstRun) return;

        // Force reset IsFirstRun only once during development if needed
        // _settings.IsFirstRun = true; 

        // Core directory initialization (silent for all platforms)
        // Core directory initialization in the central data directory
        var directories = new[] 
        { 
            Path.Combine(AppRuntime.DataDirectory, "assets"), 
            Path.Combine(AppRuntime.DataDirectory, "death-client"), 
            Path.Combine(AppRuntime.DataDirectory, "node-skin-server"),
            Path.Combine(AppRuntime.DataDirectory, "death-client-mod")
        };
        foreach (var dir in directories) if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Windows-only visual setup process
        if (OperatingSystem.IsWindows())
        {
            LauncherLog.Info("Performing Windows first-run setup...");
            var setupWin = new SetupWindow();

            try 
            {
                await Dispatcher.UIThread.InvokeAsync(() => setupWin.Show());

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var psCommand = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{Path.Combine(desktopPath, "Aether Launcher.lnk")}'); $s.TargetPath='{exePath}'; $s.Save()";
                    Process.Start(new ProcessStartInfo 
                    { 
                        FileName = "powershell", 
                        Arguments = $"-Command \"{psCommand}\"", 
                        CreateNoWindow = true, 
                        UseShellExecute = false 
                    });
                    LauncherLog.Info("Windows desktop shortcut created.");
                }

                await Task.Delay(4000); // Allow time to read disclaimer
            }
            catch (Exception ex) { LauncherLog.Error("Windows setup failed", ex); }
            finally { await Dispatcher.UIThread.InvokeAsync(() => setupWin.Close()); }
        }

        _settings.IsFirstRun = false;
        _settingsStore.Save(_settings);
    }

    private async void PlayOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_playOverlay).Properties.IsLeftButtonPressed || !_playOverlay.IsEnabled)
            return;

        await LaunchAsync();
    }

    public async void CreateProfileButton_Click() => await CreateProfileAsync();
    public async void BtnStart_Click() => await LaunchAsync();
    public async void ModrinthSearchButton_Click() => await SearchModrinthAsync();
    public void ModrinthResultsListView_SelectedIndexChanged() => UpdateSelectedProjectDetails();
    public async Task ImportLayoutAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select AXAML Layout File",
                FileTypeFilter = [new FilePickerFileType("AXAML") { Patterns = ["*.axaml", "*.runtime"] }]
            });
            if (files == null || files.Count == 0) return;

            // Save the file
            var targetPath = RuntimeLayoutPath;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            await File.WriteAllTextAsync(targetPath, content);

            // Snapshot current style for revert
            _previousStyle = _settings.Style.Clone();
            _revertCts?.Cancel();
            _revertCts?.Dispose();

            // Read properties from the imported file and apply to Style
            ApplyLayoutFileProperties();
            _settingsStore.Save(_settings);

            async Task FadeWindowAsync(double targetOpacity, int durationMs, Easing? easing = null)
            {
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(durationMs),
                        Easing = easing
                    }
                };

                Opacity = targetOpacity;
                await Task.Delay(durationMs + 30);
            }

            await FadeWindowAsync(0.4, 120, new SineEaseOut());

            // Rebuild UI with new style
            InvalidateUiCache();
            Content = BuildRoot();
            await FadeWindowAsync(1.0, 250, new CubicEaseOut());
            SetActiveSection("layout");

            // Show 15-second revert window
            ShowRevertOverlay();
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Import Failed", ex.Message);
        }
    }

    /// <summary>
    /// Reads LayoutProperties from the imported AXAML file and maps them to _settings.Style.
    /// Only the properties specified in the file are updated — everything else stays as-is.
    /// </summary>
    private void ApplyLayoutFileProperties()
    {
        var path = RuntimeLayoutPath;
        if (!File.Exists(path)) return;

        // Load the control tree for the slot system (named Panel hosts)
        Control? root = null;
        try { root = UILoader.Load(path); }
        catch (Exception ex) { LauncherLog.Warn($"[Layout] Control load failed (slot system disabled): {ex.Message}"); }
        _importedLayoutRoot = root;
        _namedSlots = root != null
            ? UILoader.FindNamedSlots(root)
            : new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);

        // Use XML-level scan — properties on ANY element in the document are found reliably
        var props = UILoader.ScanAllLayoutProperties(path);
        if (props.Count == 0) { LauncherLog.Info("[Layout] No LayoutProperties found in file."); return; }

        var style = _settings.Style;
        var ic = System.Globalization.CultureInfo.InvariantCulture;

        bool Str(string key, out string val) { val = ""; return props.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) && (val = v) != null; }
        bool Dbl(string key, out double val) { val = double.NaN; return props.TryGetValue(key, out var s) && double.TryParse(s, System.Globalization.NumberStyles.Any, ic, out val); }
        bool Bool(string key, out bool b) { b = false; if (!props.TryGetValue(key, out var s)) return false; b = string.Equals(s, "true", StringComparison.OrdinalIgnoreCase); return true; }

        // Window / Shell
        if (Str("WindowShape", out var windowShape)) { style.BorderStyle = windowShape; if (string.Equals(windowShape, "square", StringComparison.OrdinalIgnoreCase)) style.CornerRadius = 0; }
        if (Dbl("WindowRadius", out var wr)) style.CornerRadius = (int)wr;
        if (Str("WindowBackground", out var wBg)) style.WindowBackground = wBg;
        if (Str("WindowBorderColor", out var wBrd)) style.WindowBorderColor = wBrd;
        if (Dbl("WindowBorderThickness", out var wBrdT)) style.WindowBorderThickness = wBrdT;
        if (Dbl("WindowMargin", out var wMarg)) style.WindowMargin = wMarg;
        if (Dbl("WindowWidth", out var wW) && wW > 0) Width = wW;
        if (Dbl("WindowHeight", out var wH) && wH > 0) Height = wH;
        if (Dbl("WindowMinWidth", out var wMinW) && wMinW > 0) MinWidth = wMinW;
        if (Dbl("WindowMinHeight", out var wMinH) && wMinH > 0) MinHeight = wMinH;

        // Sidebar (HeaderBackground/HeaderHeight are alias properties for nav panel)
        if (Str("SidebarBackground", out var sbBg)) style.SidebarBackground = sbBg;
        else if (Str("HeaderBackground", out var hdrBg)) style.SidebarBackground = hdrBg;
        if (Str("SidebarBorderColor", out var sbBrd)) style.SidebarBorderColor = sbBrd;
        else if (Str("HeaderBorderColor", out var hdrBorder)) style.SidebarBorderColor = hdrBorder;
        if (Dbl("SidebarWidth", out var sbW) && sbW > 0) style.SidebarWidth = sbW;
        else if (Dbl("HeaderHeight", out var hdrH) && hdrH > 0) style.SidebarWidth = hdrH;
        if (Str("SidebarSide", out var sbSide)) style.SidebarSide = sbSide;
        if (Bool("SidebarCollapsed", out var sbCol)) style.SidebarCollapsed = sbCol;
        if (Dbl("SidebarPadding", out var sbPad)) style.SidebarPadding = sbPad;

        // Navigation
        if (Str("NavPosition", out var navPos)) style.NavPosition = navPos;
        if (Str("NavButtonBackground", out var navBg)) style.NavButtonBackground = navBg;
        if (Str("NavButtonActiveBackground", out var navActBg)) style.NavButtonActiveBackground = navActBg;
        if (Str("NavButtonForeground", out var navFg)) style.NavButtonForeground = navFg;
        if (Str("NavButtonActiveForeground", out var navActFg)) style.NavButtonActiveForeground = navActFg;
        if (Dbl("NavButtonCornerRadius", out var navCr)) style.NavButtonCornerRadius = navCr;
        if (Dbl("NavButtonSpacing", out var navSp)) style.NavButtonSpacing = navSp;
        if (Dbl("NavButtonHeight", out var navH)) style.NavButtonHeight = navH;
        if (Dbl("NavButtonFontSize", out var navFs)) style.NavButtonFontSize = navFs;
        if (Str("NavIndicatorStyle", out var navInd)) style.NavIndicatorStyle = navInd;

        // Typography
        if (Str("TitleText", out var ttxt)) style.TitleText = ttxt;
        if (Dbl("TitleFontSize", out var tFs)) style.TitleFontSize = tFs;
        if (Str("TitleForeground", out var tFg)) style.TitleForeground = tFg;
        if (Str("PrimaryFontFamily", out var pFont)) style.PrimaryFontFamily = pFont;
        if (Str("PrimaryForeground", out var pFg)) style.PrimaryForeground = pFg;
        if (Str("SecondaryForeground", out var sFg2)) style.SecondaryForeground = sFg2;

        // Colors / Accent
        if (Str("AccentColor", out var accent)) { style.AccentColorOverride = accent; style.AccentColor = accent; _settings.AccentColor = accent; }
        if (Dbl("BackgroundOpacity", out var bgOp)) style.BackgroundOpacity = bgOp;
        if (Str("BackgroundOverlayColor", out var bgOvCol)) style.BackgroundOverlayColor = bgOvCol;
        if (Str("BackgroundImageUrl", out var bgUrl))
        {
            var resolved = ResolveAndCacheBackgroundImage(bgUrl);
            if (!string.IsNullOrWhiteSpace(resolved))
                style.BackgroundImagePath = resolved;
        }
        if (Str("BackgroundImagePath", out var bgImg))
        {
            var resolved = ResolveAndCacheBackgroundImage(bgImg);
            if (!string.IsNullOrWhiteSpace(resolved))
                style.BackgroundImagePath = resolved;
            else
                style.BackgroundImagePath = bgImg;
        }
        if (Dbl("BackgroundOverlayOpacity", out var bgOvOp)) style.BackgroundOverlayOpacity = bgOvOp;
        if (Dbl("AccentStripHeight", out var asH)) style.AccentStripHeight = asH;

        // Cards
        if (Str("CardBackground", out var cardBg)) style.CardBackground = cardBg;
        if (Dbl("CardCornerRadius", out var cardCr)) style.CardCornerRadius = cardCr;
        if (Str("CardBorderColor", out var cardBrd)) style.CardBorderColor = cardBrd;
        if (Dbl("CardPadding", out var cardPad)) style.CardPadding = cardPad;

        // Buttons
        if (Str("ButtonBackground", out var btnBg)) style.ButtonBackground = btnBg;
        if (Str("ButtonForeground", out var btnFg)) style.ButtonForeground = btnFg;
        if (Dbl("ButtonCornerRadius", out var btnCr)) style.ButtonCornerRadius = btnCr;
        if (Dbl("ButtonHeight", out var btnH)) style.ButtonHeight = btnH;
        if (Dbl("ButtonFontSize", out var btnFs)) style.ButtonFontSize = btnFs;
        if (Dbl("ButtonPadding", out var btnPad)) style.ButtonPadding = btnPad;
        if (Str("ButtonHoverBackground", out var hBg)) style.ButtonHoverBackground = hBg;
        if (Str("ButtonHoverForeground", out var hFg)) style.ButtonHoverForeground = hFg;
        if (Str("ButtonHoverBorderColor", out var hBrd)) style.ButtonHoverBorderColor = hBrd;

        // Content
        if (Dbl("ContentPadding", out var cPad)) style.ContentPadding = cPad;
        if (Dbl("ContentSpacing", out var cSpac)) style.ContentSpacing = cSpac;
        if (Str("ContentBackground", out var cBg)) style.ContentBackground = cBg;
        if (Bool("CompactMode", out var compactMode)) style.CompactMode = compactMode;

        // Fields
        if (Str("FieldBackground", out var fBg)) style.FieldBackground = fBg;
        if (Str("FieldForeground", out var fFg)) style.FieldForeground = fFg;
        if (Str("FieldBorderColor", out var fBrd2)) style.FieldBorderColor = fBrd2;
        if (Dbl("FieldRadius", out var fRad)) style.FieldRadius = fRad;
        if (Dbl("FieldPadding", out var fPad)) style.FieldPadding = fPad;
        if (Dbl("FieldFontSize", out var fFs)) style.FieldFontSize = fFs;

        // Progress Bars
        if (Str("ProgressBarForeground", out var pbFg)) style.ProgressBarForeground = pbFg;
        if (Str("ProgressBarBackground", out var pbBg)) style.ProgressBarBackground = pbBg;
        if (Dbl("ProgressBarHeight", out var pbH)) style.ProgressBarHeight = pbH;
        if (Dbl("ProgressBarRadius", out var pbR)) style.ProgressBarRadius = pbR;

        // Item Cards
        if (Str("ItemCardBackground", out var icBg)) style.ItemCardBackground = icBg;
        if (Dbl("ItemCardRadius", out var icRad)) style.ItemCardRadius = icRad;

        // Overlays
        if (Str("OverlayColor", out var ovl)) style.OverlayColor = ovl;
        if (Str("AccountsOverlayBackground", out var aob)) style.AccountsOverlayBackground = aob;
        if (Dbl("AccountsOverlayCornerRadius", out var aocr)) style.AccountsOverlayCornerRadius = aocr;
        if (Str("AccountsOverlayBorderColor", out var aobc)) style.AccountsOverlayBorderColor = aobc;
        if (Dbl("AccountsOverlayBorderThickness", out var aobt)) style.AccountsOverlayBorderThickness = aobt;

        // Sections
        if (Str("SectionOrder", out var sectionOrder)) style.SectionOrder = sectionOrder;
        if (Bool("PlayButtonGlobal", out var playGlobal)) style.PlayButtonGlobal = playGlobal;
        else if (Bool("PlayButtonAllTabs", out var playAllTabs)) style.PlayButtonGlobal = playAllTabs;

        LauncherLog.Info($"[Layout] Applied {props.Count} properties. shape={style.BorderStyle}, nav={style.NavPosition}, " +
                         $"sidebar={style.SidebarSide}, accent={style.AccentColorOverride ?? "default"}, slots={_namedSlots.Count}");
    }

    private IBrush GetAccentStripBrush()
    {
        return Brushes.Transparent;
    }

    private string? ResolveAndCacheBackgroundImage(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                var cacheDir = Path.Combine(AppRuntime.DataDirectory, "death-client", "assets", "layout-backgrounds");
                Directory.CreateDirectory(cacheDir);

                var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source));
                var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                var ext = Path.GetExtension(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 8) ext = ".img";
                var cachedPath = Path.Combine(cacheDir, $"{hash}{ext}");

                if (!File.Exists(cachedPath))
                {
                    using var client = new System.Net.Http.HttpClient();
                    var bytes = client.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                    File.WriteAllBytes(cachedPath, bytes);
                    LauncherLog.Info($"[Layout] Downloaded background image to '{cachedPath}'.");
                }

                return cachedPath;
            }

            if (File.Exists(source))
                return Path.GetFullPath(source);

            var runtimeDir = Path.GetDirectoryName(RuntimeLayoutPath);
            if (!string.IsNullOrWhiteSpace(runtimeDir))
            {
                var relativePath = Path.GetFullPath(Path.Combine(runtimeDir, source));
                if (File.Exists(relativePath))
                    return relativePath;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Warn($"[Layout] Failed to resolve background image '{source}': {ex.Message}");
        }

        return null;
    }

    private static bool IsSectionSlotName(string sectionName)
    {
        return string.Equals(sectionName, "LaunchSection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionName, "ModrinthSection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionName, "ProfilesSection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionName, "PerformanceSection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionName, "SettingsSection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionName, "LayoutSection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PreserveHostContent(string sectionName)
    {
        // Only section slots are content-editable. Core hosts (SidebarHost/MainContentHost)
        // always receive launcher defaults unless explicitly replaced through section slots.
        return IsSectionSlotName(sectionName);
    }

    private Control? TryPlaceInSection(string sectionName, Control? defaultContent)
    {
        if (_importedLayoutRoot == null) return defaultContent;

        if (_namedSlots.TryGetValue(sectionName, out var panelHost))
        {
            panelHost = DetachFromParent(panelHost) as Panel ?? panelHost;
            var hasCustomChildren = panelHost.Children.Count > 0;
            if (hasCustomChildren && PreserveHostContent(sectionName))
            {
                if (IsSectionSlotName(sectionName))
                    _sectionSlotControls[sectionName] = panelHost;
                return panelHost;
            }

            panelHost.Children.Clear();
            if (defaultContent != null)
                panelHost.Children.Add(defaultContent);
            return panelHost;
        }

        Control? hostControl = null;
        try { hostControl = _importedLayoutRoot.FindControl<Control>(sectionName); }
        catch { hostControl = null; }

        if (hostControl == null) return defaultContent;

        hostControl = DetachFromParent(hostControl) ?? hostControl;

        if (hostControl is Panel hostPanel)
        {
            var hasCustomChildren = hostPanel.Children.Count > 0;
            if (hasCustomChildren && PreserveHostContent(sectionName))
            {
                if (IsSectionSlotName(sectionName))
                    _sectionSlotControls[sectionName] = hostPanel;
                return hostPanel;
            }

            hostPanel.Children.Clear();
            if (defaultContent != null)
                hostPanel.Children.Add(defaultContent);
            return hostPanel;
        }

        if (hostControl is ContentControl contentHost)
        {
            if (contentHost.Content != null && PreserveHostContent(sectionName))
            {
                if (IsSectionSlotName(sectionName))
                    _sectionSlotControls[sectionName] = contentHost;
                return contentHost;
            }

            contentHost.Content = defaultContent;
            return contentHost;
        }

        if (hostControl is Decorator decoratorHost)
        {
            if (decoratorHost.Child != null && PreserveHostContent(sectionName))
            {
                if (IsSectionSlotName(sectionName))
                    _sectionSlotControls[sectionName] = decoratorHost;
                return decoratorHost;
            }

            decoratorHost.Child = defaultContent;
            return decoratorHost;
        }

        LauncherLog.Warn($"[Layout] Named host '{sectionName}' exists but cannot contain children ({hostControl.GetType().Name}). Falling back to default placement.");
        return defaultContent;
    }

    public async Task ResetLayoutAsync()

    {
        try
        {
            // Reset all style tokens to defaults
            _settings.Style = LayoutStyle.Default();
            _settingsStore.Save(_settings);

            // Remove the imported layout file
            if (File.Exists(RuntimeLayoutPath))
                File.Delete(RuntimeLayoutPath);

            InvalidateUiCache();
            Content = BuildRoot();
            SetActiveSection("layout");

            await DialogService.ShowInfoAsync(this, "Layout Reset", "All styles reset to defaults and layout file removed.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Reset Failed", ex.Message);
        }
    }
}

internal static class AvaloniaControlExtensions
{
    public static T With<T>(this T control, int row = -1, int column = -1, int columnSpan = 1, int rowSpan = 1) where T : Control
    {
        if (row >= 0) Grid.SetRow(control, row);
        if (column >= 0) Grid.SetColumn(control, column);
        if (columnSpan > 1) Grid.SetColumnSpan(control, columnSpan);
        if (rowSpan > 1) Grid.SetRowSpan(control, rowSpan);
        return control;
    }

    public static T With<T>(this T control, Action<T> action) where T : Control
    {
        action(control);
        return control;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

#if false
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Version;
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
using System.Diagnostics;
using System.Windows.Input;
using System.Threading;

namespace OfflineMinecraftLauncher;

public class ModItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isEnabled;
    public string FileName { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (string.IsNullOrEmpty(FullPath)) return; // Init

            try
            {
                if (value && FileName.EndsWith(".disabled"))
                {
                    var newPath = FullPath.Substring(0, FullPath.Length - ".disabled".Length);
                    File.Move(FullPath, newPath);
                    FullPath = newPath;
                    FileName = Path.GetFileName(newPath);
                }
                else if (!value && !FileName.EndsWith(".disabled"))
                {
                    var newPath = FullPath + ".disabled";
                    File.Move(FullPath, newPath);
                    FullPath = newPath;
                    FileName = Path.GetFileName(newPath);
                }
            }
            catch { }
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FileName)));
        }
    }

    public void InitState(bool state) { _isEnabled = state; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}


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
    private readonly ObservableCollection<ModItem> _modItems = [];
    private readonly ObservableCollection<ModrinthProject> _searchResults = [];
    private static readonly string[] ProjectTypeOptions = ["Mod", "Modpack"];
    private static readonly string[] LoaderOptions = ["Any", "Vanilla", "Fabric", "Quilt", "Forge", "NeoForge"];
    private static readonly string[] ProfileLoaderOptions = ["Vanilla", "Fabric", "Quilt", "Forge", "NeoForge"];
    private static readonly string[] VersionCategoryOptions = ["Versions", "Snapshots", "Other sources"];
    private static readonly string[] SourceOptions = ["Modrinth", "CurseForge"];

    private TextBox usernameInput = null!;
    private ComboBox cbVersion = null!;
    private ComboBox minecraftVersion = null!;
    private Button downloadVersionButton = null!;
    private TextBox profileNameInput = null!;
    private TextBox profileGameDirInput = null!;
    private ComboBox profileLoaderCombo = null!;
    private Button createProfileButton = null!;
    private Button renameProfileButton = null!;
    private Button btnStart = null!;
    private CancellationTokenSource? _launchCts;
    private Button launchNavButton = null!;
    private Button profilesNavButton = null!;
    private Button modrinthNavButton = null!;
    private Button performanceNavButton = null!;
    private Button settingsNavButton = null!;
    private Button layoutNavButton = null!;
    private Button accountsNavButton = null!;
    private TextBlock activeProfileBadge = null!;
    private TextBlock activeContextLabel = null!;
    private TextBlock installModeLabel = null!;
    private Image characterImage = null!;
    private TextBlock statusLabel = null!;
    private TextBlock installDetailsLabel = null!;
    private ProgressBar pbFiles = null!;
    private ProgressBar pbProgress = null!;
    private TextBox modrinthSearchInput = null!;
    private ComboBox modrinthProjectTypeCombo = null!;
    private ComboBox modrinthLoaderCombo = null!;
    private ComboBox modrinthSourceCombo = null!;
    private Button modrinthSearchButton = null!;
    private TextBox modrinthVersionInput = null!;
    private ListBox modrinthResultsListBox = null!;
    private TextBlock modrinthDetailsBox = null!;
    private TextBlock modrinthResultsSummary = null!;
    private Button installSelectedButton = null!;
    private Button importMrpackButton = null!;
    private ListBox profileListBox = null!;
    private TextBlock profileInspectorTitle = null!;
    private TextBlock profileInspectorMeta = null!;
    private TextBlock profileInspectorPath = null!;
    private Button clearProfileButton = null!;
    private TextBlock heroInstanceLabel = null!;
    private TextBlock heroPerformanceLabel = null!;
    private TextBlock homeFpsStatValue = null!;
    private TextBlock homeRamStatValue = null!;
    private TextBlock performanceFpsStatValue = null!;
    private TextBlock performanceRamStatValue = null!;
    private TextBlock loadingLabel = null!;
    private Control launchSection = null!;
    private Control modrinthSection = null!;
    private Control profilesSection = null!;
    private Control performanceSection = null!;
    private Control settingsSection = null!;
    private Control layoutSection = null!;
    private Border? _homeStatusBar;
    public ProgressBar? PbProgress { get; set; }
    public TextBox? ModrinthSearchInput { get; set; }
    public System.Collections.Generic.Dictionary<string, object> Fields { get; } = new();
    private Border _instanceEditorOverlay = null!;
    private Border _accountsOverlay = null!;
    private StackPanel _accountsListPanel = new();
    private MinecraftAuthenticationService _authService = new();
    private Border _playOverlay = null!;
    private TextBlock _playOverlayIcon = null!;
    private TextBlock _playOverlayLabel = null!;
    // _notificationCard removed (notification replaced with Featured Servers section)
    // Quick Instance panel
    private ComboBox _quickVersionCombo = null!;
    private ComboBox _quickLoaderCombo = null!;
    private Button _quickInstallButton = null!;

    // Quick Mods panel
    private TextBox _quickModSearch = null!;
    private Button _quickModSearchButton = null!;
    private readonly ListBox _quickModResults = new();
    private readonly ObservableCollection<ModrinthProject> _quickSearchResults = [];

    private ComboBox instanceVersionCombo = null!;
    private ComboBox instanceCategoryCombo = null!;

    private string _playerUuid = string.Empty;
    private LauncherProfile? _selectedProfile;
    private CancellationTokenSource? _searchCancellation;
    private UserSettings _settings;
    private string _activeSection = "launch";
    // Responsive UI state
    private bool _isNarrowMode;
    private Border? _avatarGlass;
    private StackPanel? _avatarControls;
    private Grid? _avatarActions;
    private StackPanel? _mainContentStack;
    private readonly SemaphoreSlim _versionListSemaphore = new(1, 1);

    // Style revert system
    private LayoutStyle? _previousStyle;
    private CancellationTokenSource? _revertCts;
    private Border? _revertOverlay;
    private Control? _importedLayoutRoot;
    private static string RuntimeLayoutPath => Path.Combine(AppRuntime.DataDirectory, "death-client", "ui-layout-final.axaml.runtime");


    public MainWindow()
    {
        var initialPath = new MinecraftPath();
        initialPath.CreateDirs();
        _settingsStore = new UserSettingsStore(initialPath.BasePath);
        _settings = _settingsStore.Load();

        // Migrate legacy semicolon-delimited layout tokens to structured Style object
        _settings.MigrateLegacyLayout();
        if (string.IsNullOrWhiteSpace(_settings.ClientLayout))
        {
            // Migration happened or was already clean — persist
            _settingsStore.Save(_settings);
        }

        if (!string.IsNullOrEmpty(_settings.BaseMinecraftPath) && Directory.Exists(_settings.BaseMinecraftPath))
            _defaultMinecraftPath = new MinecraftPath(_settings.BaseMinecraftPath);
        else
            _defaultMinecraftPath = initialPath;

        _defaultMinecraftPath.CreateDirs();
        _profileStore = new LauncherProfileStore(_defaultMinecraftPath.BasePath);
        _defaultLauncher = CreateLauncher(_defaultMinecraftPath);
        ConfigureWindowChrome();
        EnsureFallbackControlsInitialized();

        this.SizeChanged += (s, e) => UpdateResponsiveLayout();
        Opened += async (_, _) => 
        {
            UpdateResponsiveLayout();
            try { await InitializeAsync(); } catch { }
        };

        // If there's an imported AXAML layout file, read its properties into Style
        ApplyLayoutFileProperties();

        // Build the C# UI — always uses the default C# UI, styled by settings.Style
        Content = BuildRoot();


        // Removed duplicated Opened handler
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
        EnsureFallbackControlsInitialized();
        var style = _settings.Style;
        var topNavigation = IsTopNavigationEnabled();
        var collapsedSidebar = IsSidebarCollapsed();
        var compact = style.CompactMode;
        var sidebarWidth = collapsedSidebar ? 72 : (compact ? 200 : (double.IsNaN(style.SidebarWidth) ? 240 : style.SidebarWidth));


        if (topNavigation)
        {
            return WrapWindowSurface(new Grid
            {
                Background = GetMainBackground(),
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    new Border {
                        Background = new SolidColorBrush(Color.FromArgb(8, 110, 91, 255)),
                        IsHitTestVisible = false,
                        ZIndex = 999
                    }.With(rowSpan: 2),
                    
                    new Canvas
                    {
                        Children =
                        {
                            new Border
                            {
                                Width = 500,
                                Height = 500,
                                CornerRadius = new CornerRadius(999),
                                Background = new RadialGradientBrush
                                {
                                    Center = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                    GradientOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                    RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                                    RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                                    GradientStops =
                                    {
                                        new GradientStop(GetAccentColor(20), 0),
                                        new GradientStop(GetAccentColor(0), 1)
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
                                        new GradientStop(GetAccentColor(15), 0),
                                        new GradientStop(GetAccentColor(0), 1)
                                    }
                                },
                                [Canvas.RightProperty] = -180d,
                                [Canvas.TopProperty] = 40d
                            }
                        }
                    }.With(row: 0),

                    // Accent Strip
                    new Border
                    {
                        Height = double.IsNaN(style.AccentStripHeight) ? 2 : style.AccentStripHeight,
                        Background = GetAccentStripBrush(),
                        VerticalAlignment = VerticalAlignment.Top,
                        ZIndex = 2000
                    }.With(rowSpan: 2),

                    TryPlaceInSection("SidebarHost", DetachFromParent(BuildTopNavigation())!)!.With(row: 0),
                    TryPlaceInSection("MainContentHost", DetachFromParent(BuildContent())!)!.With(row: 1),
                    DetachFromParent(_instanceEditorOverlay)!.With(row: 0, rowSpan: 2, columnSpan: 1),
                    DetachFromParent(_accountsOverlay)!.With(row: 0, rowSpan: 2, columnSpan: 2)
                }
            }, topNavigation: true);

        }

        var sidebarOnRight = string.Equals(style.SidebarSide, "right", StringComparison.OrdinalIgnoreCase);
        return WrapWindowSurface(new Grid
        {
            Background = GetMainBackground(),
            ColumnDefinitions = sidebarOnRight
                ? new ColumnDefinitions($"*,{sidebarWidth}")
                : new ColumnDefinitions($"{sidebarWidth},*"),
            Children =
            {
                new Canvas
                {
                    Children =
                    {
                        new Border
                        {
                            Width = 500,
                            Height = 500,
                            CornerRadius = new CornerRadius(999),
                            Background = new RadialGradientBrush
                            {
                                Center = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                GradientOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                                RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                                RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                                GradientStops =
                                {
                                    new GradientStop(Color.FromArgb(20, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 0),
                                    new GradientStop(Color.FromArgb(0, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 1)
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
                                    new GradientStop(Color.FromArgb(15, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 0),
                                    new GradientStop(Color.FromArgb(0, Color.Parse(_settings.AccentColor ?? "#6E5BFF").R, Color.Parse(_settings.AccentColor ?? "#6E5BFF").G, Color.Parse(_settings.AccentColor ?? "#6E5BFF").B), 1)
                                }
                            },
                            [Canvas.RightProperty] = -180d,
                            [Canvas.TopProperty] = 40d
                        }
                    }
                },
                  sidebarOnRight ? TryPlaceInSection("MainContentHost", DetachFromParent(BuildContent())!)!.With(column: 0) : TryPlaceInSection("SidebarHost", DetachFromParent(BuildHeader())!)!,
                  sidebarOnRight ? TryPlaceInSection("SidebarHost", DetachFromParent(BuildHeader())!)!.With(column: 1) : TryPlaceInSection("MainContentHost", DetachFromParent(BuildContent())!)!.With(column: 1),
                DetachFromParent(_instanceEditorOverlay)!.With(columnSpan: 2),
                DetachFromParent(_accountsOverlay)!.With(columnSpan: 2)
            }
        }, topNavigation: false);
    }

    // --- Style token accessors (read from structured LayoutStyle) ---

    private bool IsTopNavigationEnabled() => string.Equals(_settings.Style.NavPosition, "top", StringComparison.OrdinalIgnoreCase);

    private bool IsSidebarCollapsed() => !IsTopNavigationEnabled() && _settings.Style.SidebarCollapsed;

    private bool IsSidebarOnRight() => string.Equals(_settings.Style.SidebarSide, "right", StringComparison.OrdinalIgnoreCase);

    private int GetStyleCornerRadius() =>
        string.Equals(_settings.Style.BorderStyle, "square", StringComparison.OrdinalIgnoreCase) ? 0 : _settings.Style.CornerRadius;

    private void ToggleSidebarCollapsed()
    {
        _settings.Style.SidebarCollapsed = !IsSidebarCollapsed();
        _settingsStore.Save(_settings);
        Content = BuildRoot();
        SetActiveSection(_activeSection);
    }

    // --- Style change with 15-second revert window ---

    private void ApplyStyleWithRevert(Action<LayoutStyle> mutate)
    {
        // Snapshot current style before change
        _previousStyle = _settings.Style.Clone();
        _revertCts?.Cancel();
        _revertCts?.Dispose();

        // Apply the mutation
        mutate(_settings.Style);

        // If border style is square, force corner radius to 0
        if (string.Equals(_settings.Style.BorderStyle, "square", StringComparison.OrdinalIgnoreCase))
            _settings.Style.CornerRadius = 0;

        // Rebuild UI with new style
        InvalidateUiCache();
        Content = BuildRoot();
        SetActiveSection("layout");

        // Show revert overlay with 15s countdown
        ShowRevertOverlay();
    }

    private void ShowRevertOverlay()
    {
        _revertCts = new CancellationTokenSource();
        var ct = _revertCts.Token;
        var secondsLeft = 15;

        var countdownLabel = new TextBlock
        {
            Text = $"Keeping in {secondsLeft}s...",
            Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var keepBtn = new Button
        {
            Content = "✓ Keep Changes",
            Background = new SolidColorBrush(Color.Parse("#2A7A3A")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8),
            FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(0)
        };
        var revertBtn = new Button
        {
            Content = "↩ Revert",
            Background = new SolidColorBrush(Color.Parse("#7A2A2A")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8),
            FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(0)
        };

        keepBtn.Click += (_, _) => ConfirmStyleChange();
        revertBtn.Click += (_, _) => RevertStyleChange();

        _revertOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 14, 18, 28)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A3150")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 32),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Layout changed.",
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    countdownLabel,
                    keepBtn,
                    revertBtn
                }
            }
        };

        // Add overlay on top of current content
        if (Content is Control currentContent)
        {
            // Must detach from Window.Content BEFORE adding to overlay Grid
            Content = null;
            var overlay = new Grid
            {
                Children =
                {
                    currentContent,
                    _revertOverlay
                }
            };
            Content = overlay;
        }

        // Countdown timer
        _ = Task.Run(async () =>
        {
            while (secondsLeft > 0 && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
                secondsLeft--;
                Dispatcher.UIThread.Post(() =>
                {
                    if (!ct.IsCancellationRequested)
                        countdownLabel.Text = $"Keeping in {secondsLeft}s...";
                });
            }

            if (!ct.IsCancellationRequested)
                Dispatcher.UIThread.Post(ConfirmStyleChange);
        }, ct).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
    }

    private void ConfirmStyleChange()
    {
        _revertCts?.Cancel();
        _revertCts?.Dispose();
        _revertCts = null;
        _previousStyle = null;

        _settingsStore.Save(_settings);

        // Remove overlay, rebuild clean
        InvalidateUiCache();
        Content = BuildRoot();
        SetActiveSection("layout");
    }

    private void RevertStyleChange()
    {
        _revertCts?.Cancel();
        _revertCts?.Dispose();
        _revertCts = null;

        if (_previousStyle != null)
        {
            _settings.Style = _previousStyle;
            _previousStyle = null;
            _settingsStore.Save(_settings);
        }

        // Rebuild with reverted style
        InvalidateUiCache();
        Content = BuildRoot();
        SetActiveSection("layout");
    }

    private void ConfigureWindowChrome()
    {
        Title = "Aether Launcher";
        Name = "aether-launcher";
        Width = 1344;
        Height = 714;
        MinWidth = 1100;
        MinHeight = 610;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;
        SystemDecorations = SystemDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = 46;
        TransparencyLevelHint = new[] { 
            WindowTransparencyLevel.AcrylicBlur, 
            WindowTransparencyLevel.Mica, 
            WindowTransparencyLevel.Transparent 
        };

        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/deathclient-taskbar.png")));
        }
        catch
        {
            try
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/dc-icon.png")));
            }
            catch
            {
            }
        }
    }

    private Control WrapWindowSurface(Control content, bool topNavigation)
    {
        var style = _settings.Style;
        var shell = new Grid
        {
            ClipToBounds = false,
            Children = { content }
        };

        if (!topNavigation)
        {
            var floatingControls = BuildWindowControls();
            floatingControls.Margin = new Thickness(0, 16, 16, 0);
            floatingControls.HorizontalAlignment = HorizontalAlignment.Right;
            floatingControls.VerticalAlignment = VerticalAlignment.Top;
            shell.Children.Add(floatingControls);
        }

        var cr = GetStyleCornerRadius();
        
        var margin = style.WindowMargin;
        if (style.CompactMode) margin = Math.Max(0, margin - 4);
        
        var bg = !string.IsNullOrWhiteSpace(style.WindowBackground) ? style.WindowBackground : "#090C12";
        var border = !string.IsNullOrWhiteSpace(style.WindowBorderColor) ? style.WindowBorderColor : "#DC222A3F";

        return new Border
        {
            Margin = new Thickness(margin),
            CornerRadius = new CornerRadius(cr),
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.Parse(bg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Thickness(style.WindowBorderThickness),
            Child = shell
        };
    }


    private StackPanel BuildWindowControls()
    {
        var minimizeButton = CreateWindowControlButton("−", Color.Parse("#F4B63C"), () => WindowState = WindowState.Minimized);
        var maximizeButton = CreateWindowControlButton(WindowState == WindowState.Maximized ? "❐" : "□", Color.Parse("#4AD66D"), () =>
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            Content = BuildRoot();
            SetActiveSection(_activeSection);
        });
        var closeButton = CreateWindowControlButton("✕", Color.Parse("#FF5C70"), Close);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                DetachFromParent(accountsNavButton)!,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children = { minimizeButton, maximizeButton, closeButton }
                }
            }
        };
    }

    private Button CreateWindowControlButton(string glyph, Color color, Action onClick)
    {
        var button = new Button
        {
            Width = 14,
            Height = 14,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(999),
            Background = new SolidColorBrush(color),
            BorderThickness = new Thickness(0),
            Content = new TextBlock
            {
                Text = glyph,
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 12, 16, 24)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            }
        };

        button.Click += (_, _) => onClick();
        button.PointerEntered += (_, _) =>
        {
            if (button.Content is TextBlock label)
                label.Opacity = 1;
        };
        button.PointerExited += (_, _) =>
        {
            if (button.Content is TextBlock label)
                label.Opacity = 0;
        };

        return button;
    }

    private void AttachWindowDrag(Control control)
    {
        control.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
                return;

            try
            {
                BeginMoveDrag(e);
            }
            catch
            {
            }
        };
    }

    private Brush GetMainBackground()
    {
        var style = _settings.Style;

        // 1. If a specific WindowBackground hex color is set, prioritize it
        if (!string.IsNullOrWhiteSpace(style.WindowBackground))
        {
            try { return new SolidColorBrush(Color.Parse(style.WindowBackground)); } catch { }
        }

        // 2. Try Custom Background Image Path from style
        if (!string.IsNullOrWhiteSpace(style.BackgroundImagePath) && File.Exists(style.BackgroundImagePath))
        {
            try {
                var ovOp = double.IsNaN(style.BackgroundOverlayOpacity) ? 1.0 : style.BackgroundOverlayOpacity;
                return new ImageBrush(new Bitmap(style.BackgroundImagePath)) 
                { 
                    Stretch = Stretch.UniformToFill, 
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = ovOp == 1.0 ? style.BackgroundOpacity : 1.0 - ovOp
                };
            } catch { }
        }

        // 3. Try legacy custom_bg.png on disk
        var customBgPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBgPath))
        {
            try {
                return new ImageBrush(new Bitmap(customBgPath)) 
                { 
                    Stretch = Stretch.UniformToFill, 
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = style.BackgroundOpacity 
                };
            } catch { }
        }

        // 4. Default Bundled Resource
        try 
        {
            var asset = AssetLoader.Open(new Uri("avares://AetherLauncher/assets/launcher_background.png"));
            if (asset != null)
            {
                return new ImageBrush(new Bitmap(asset)) 
                { 
                    Stretch = Stretch.UniformToFill, 
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = style.BackgroundOpacity 
                };
            }
        } catch { }

        // 5. Final Fallback to Linear Gradient
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
        var style = _settings.Style;
        var collapsed = IsSidebarCollapsed();
        var sidebarOnRight = IsSidebarOnRight();
        var cr = GetStyleCornerRadius();
        var compact = style.CompactMode;
        var brand = collapsed
            ? (Control)new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.Parse("#121722")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "☠",
                    Foreground = Brushes.White,
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            }
            : new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Margin = new Thickness(4, 8, 4, 28),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Image
                    {
                        Source = new Bitmap(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/deathclient-taskbar.png"))),
                        Width = 28, Height = 28,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = style.TitleText ?? "AETHER LAUNCHER",
                        Foreground = Brushes.White,
                        FontSize = 18,
                        FontWeight = FontWeight.Black,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Inter, Segoe UI")
                    }
                }
            };

        launchNavButton = CreateNavButton("⌂", "Home", collapsed);
        launchNavButton.Click += (_, _) => SetActiveSection("home");
        profilesNavButton = CreateNavButton("▣", "Instances", collapsed);
        profilesNavButton.Click += (_, _) => SetActiveSection("instances");
        modrinthNavButton = CreateNavButton("⌕", "Mods", collapsed);
        modrinthNavButton.Click += (_, _) => SetActiveSection("modrinth");
        performanceNavButton = CreateNavButton("◔", "Performance", collapsed);
        performanceNavButton.Click += (_, _) => SetActiveSection("performance");
        settingsNavButton = CreateNavButton("⚙", "Settings", collapsed);
        settingsNavButton.Click += (_, _) => SetActiveSection("settings");
        layoutNavButton = CreateNavButton("▤", "Layout", collapsed);
        layoutNavButton.Click += (_, _) => SetActiveSection("layout");

        var edgeToggleButton = new Button
        {
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.Parse("#121722")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A3150")),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = sidebarOnRight ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = sidebarOnRight ? new Thickness(-11, 0, 0, 0) : new Thickness(0, 0, -11, 0),
            Content = new TextBlock
            {
                Text = sidebarOnRight
                    ? (collapsed ? "›" : "‹")
                    : (collapsed ? "‹" : "›"),
                Foreground = new SolidColorBrush(Color.Parse("#D5DAE5")),
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        edgeToggleButton.Click += (_, _) => ToggleSidebarCollapsed();

        var sbBg = !string.IsNullOrWhiteSpace(style.SidebarBackground) ? style.SidebarBackground : "#090C12";
        var sbBorder = !string.IsNullOrWhiteSpace(style.SidebarBorderColor) ? style.SidebarBorderColor : "#171B24";
        var sbPad = double.IsNaN(style.SidebarPadding) ? (collapsed ? new Thickness(10, 22, 10, 18) : new Thickness(18, 22, 18, 18)) : new Thickness(style.SidebarPadding);

        var sidebarBody = new Border
        {
            Background = new SolidColorBrush(Color.Parse(sbBg)),
            BorderBrush = new SolidColorBrush(Color.Parse(sbBorder)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = sbPad,
            Child = new StackPanel
            {
                Spacing = collapsed ? 10 : 12,
                Children =
                {
                    brand!,
                    DetachFromParent(launchNavButton)!,
                    DetachFromParent(profilesNavButton)!,
                    DetachFromParent(modrinthNavButton)!,
                    DetachFromParent(performanceNavButton)!,
                    DetachFromParent(settingsNavButton)!,
                    DetachFromParent(layoutNavButton)!
                }
            }
        };
        AttachWindowDrag(sidebarBody);

        return new Grid
        {
            ClipToBounds = false,
            Children =
            {
                sidebarBody,
                edgeToggleButton
            }
        };
    }

    private Control BuildTopNavigation()
    {
        launchNavButton = CreateNavButton("⌂", "Home");
        launchNavButton.Click += (_, _) => SetActiveSection("home");
        profilesNavButton = CreateNavButton("▣", "Instances");
        profilesNavButton.Click += (_, _) => SetActiveSection("instances");
        modrinthNavButton = CreateNavButton("⌕", "Mods");
        modrinthNavButton.Click += (_, _) => SetActiveSection("modrinth");
        performanceNavButton = CreateNavButton("◔", "Performance");
        performanceNavButton.Click += (_, _) => SetActiveSection("performance");
        settingsNavButton = CreateNavButton("⚙", "Settings");
        settingsNavButton.Click += (_, _) => SetActiveSection("settings");
        layoutNavButton = CreateNavButton("▤", "Layout");
        layoutNavButton.Click += (_, _) => SetActiveSection("layout");

        ApplyHoverMotion(launchNavButton);
        ApplyHoverMotion(profilesNavButton);
        ApplyHoverMotion(modrinthNavButton);
        ApplyHoverMotion(performanceNavButton);
        ApplyHoverMotion(settingsNavButton);
        ApplyHoverMotion(layoutNavButton);

        foreach (var button in new[] { launchNavButton, profilesNavButton, modrinthNavButton, performanceNavButton, settingsNavButton, layoutNavButton })
        {
            if (button == null) continue;
            button.Height = 40;
            button.MinWidth = 100;
        }

        var brandBlock = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Image
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://AetherLauncher/assets/deathclient-taskbar.png"))),
                    Width = 28, Height = 28,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = "AETHER LAUNCHER",
                    Foreground = Brushes.White,
                    FontSize = 18,
                    FontWeight = FontWeight.Black,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Inter, Segoe UI")
                }
            }
        };

        var centeredTabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                DetachFromParent(launchNavButton)!,
                DetachFromParent(profilesNavButton)!,
                DetachFromParent(modrinthNavButton)!,
                DetachFromParent(performanceNavButton)!,
                DetachFromParent(settingsNavButton)!,
                DetachFromParent(layoutNavButton)!
            }
        };

        var topNavigationBar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(210, 9, 12, 18)),
            BorderBrush = new SolidColorBrush(Color.Parse("#171B24")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(22, 10, 22, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("200,*,Auto"),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    brandBlock.With(column: 0),
                    centeredTabs.With(column: 1),
                    BuildWindowControls().With(column: 2)
                }
            }
        };
        AttachWindowDrag(topNavigationBar);
        return topNavigationBar;
    }

    private static T? DetachFromParent<T>(T? control) where T : Control
    {
        if (control == null) return null;
        if (control.Parent is Panel panel)
            panel.Children.Remove(control);
        else if (control.Parent is ContentControl cc)
            cc.Content = null;
        else if (control.Parent is Decorator d)
            d.Child = null;
        else if (control.Parent is Viewbox vb)
            vb.Child = null;
        return control;
    }

    private void EnsureFallbackControlsInitialized()
    {
        if (accountsNavButton == null)
        {
            accountsNavButton = new Button
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 26, 31, 46)),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(20, 10),
                MinWidth = 160,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeight.Bold,
                ZIndex = 50
            };
            accountsNavButton.Click += (_, _) => ShowAccountsOverlay();
            ApplyHoverMotion(accountsNavButton);
            UpdateAccountsButtonText();
        }

        usernameInput ??= CreateTextBox();
        usernameInput.Watermark = "Player name";
        usernameInput.TextChanged -= UsernameInput_TextChanged;
        usernameInput.TextChanged += UsernameInput_TextChanged;

        cbVersion ??= CreateComboBox(_versionItems);
        cbVersion.SelectionChanged -= CbVersion_SelectionChanged;
        cbVersion.SelectionChanged += CbVersion_SelectionChanged;

        minecraftVersion ??= CreateComboBox(VersionCategoryOptions);
        minecraftVersion.SelectionChanged -= MinecraftVersion_SelectionChanged;
        minecraftVersion.SelectionChanged += MinecraftVersion_SelectionChanged;

        downloadVersionButton ??= CreateSecondaryButton("Download Version");
        downloadVersionButton.Click -= DownloadVersionButton_Click;
        downloadVersionButton.Click += DownloadVersionButton_Click;

        profileNameInput ??= CreateTextBox();
        profileNameInput.Watermark = "Profile name";

        profileGameDirInput ??= CreateTextBox();
        profileGameDirInput.Watermark = "Custom game directory (optional)";

        instanceVersionCombo ??= CreateComboBox(_versionItems);
        instanceCategoryCombo ??= CreateComboBox(VersionCategoryOptions);
        instanceCategoryCombo.SelectedItem = "Versions";
        instanceCategoryCombo.SelectionChanged += (_, _) => _ = ListVersionsAsync(instanceCategoryCombo.SelectedItem?.ToString() ?? "Versions");
        _ = ListVersionsAsync("Versions");

        profileLoaderCombo ??= CreateComboBox(ProfileLoaderOptions);

        if (createProfileButton is null)
        {
            createProfileButton = CreatePrimaryButton("Create Profile", "#38D6C4", Colors.Black);
            createProfileButton.Click += async (_, _) => await CreateProfileAsync();
        }

        renameProfileButton ??= CreateSecondaryButton("Rename Profile");
        renameProfileButton.Click -= RenameProfileButton_Click;
        renameProfileButton.Click += RenameProfileButton_Click;

        if (btnStart is null)
        {
            btnStart = CreatePrimaryButton("▶ Play", "#6E5BFF", Colors.White);
            btnStart.Click += async (_, _) => 
            {
                if (_launchCts != null)
                {
                    _launchCts.Cancel();
                    btnStart.IsEnabled = false;
                    btnStart.Content = "Cancelling...";
                }
                else
                {
                    await LaunchAsync();
                }
            };
        }

        activeProfileBadge ??= CreateStatusTextBlock();
        activeContextLabel ??= CreateMutedTextBlock();
        installModeLabel ??= CreateStatusTextBlock();

        characterImage ??= new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        statusLabel ??= CreateStatusTextBlock();
        installDetailsLabel ??= CreateMutedTextBlock();
        pbFiles ??= new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };
        pbProgress ??= new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };

        modrinthSearchInput ??= CreateTextBox();
        modrinthProjectTypeCombo ??= CreateComboBox(ProjectTypeOptions);
        modrinthLoaderCombo ??= CreateComboBox(LoaderOptions);
        modrinthSourceCombo ??= CreateComboBox(SourceOptions);

        if (modrinthSearchButton is null)
        {
            modrinthSearchButton = CreatePrimaryButton("Search", "#6E5BFF", Colors.White);
            modrinthSearchButton.Click += async (_, _) => await SearchModrinthAsync();
        }

        modrinthVersionInput ??= CreateTextBox();
        modrinthResultsListBox ??= new ListBox { ItemsSource = _searchResults };
        modrinthResultsListBox.SelectionChanged -= ModrinthResultsListBox_SelectionChanged;
        modrinthResultsListBox.SelectionChanged += ModrinthResultsListBox_SelectionChanged;

        modrinthDetailsBox ??= CreateMutedTextBlock();
        modrinthDetailsBox.TextWrapping = TextWrapping.Wrap;
        modrinthResultsSummary ??= CreateMutedTextBlock();

        if (installSelectedButton is null)
        {
            installSelectedButton = CreatePrimaryButton("Install Selected", "#38D6C4", Colors.Black);
            installSelectedButton.Click += async (_, _) => await InstallSelectedAsync();
        }

        importMrpackButton ??= CreateSecondaryButton("Import .mrpack");
        importMrpackButton.Click -= ImportMrpackButton_Click;
        importMrpackButton.Click += ImportMrpackButton_Click;

        profileListBox ??= new ListBox { ItemsSource = _profileItems };
        profileListBox.SelectionChanged -= ProfileListBox_SelectionChanged;
        profileListBox.SelectionChanged += ProfileListBox_SelectionChanged;

        profileInspectorTitle ??= CreateStatusTextBlock();
        profileInspectorMeta ??= CreateMutedTextBlock();
        profileInspectorMeta.TextWrapping = TextWrapping.Wrap;
        profileInspectorPath ??= CreateMutedTextBlock();
        profileInspectorPath.TextWrapping = TextWrapping.Wrap;

        clearProfileButton ??= CreateSecondaryButton("Delete Profile");
        clearProfileButton.Click -= ClearProfileButton_Click;
        clearProfileButton.Click += ClearProfileButton_Click;

        heroInstanceLabel ??= new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeight.Black,
            TextWrapping = TextWrapping.Wrap
        };
        heroPerformanceLabel ??= CreateMutedTextBlock();
        homeFpsStatValue ??= new TextBlock();
        homeRamStatValue ??= new TextBlock();
        performanceFpsStatValue ??= new TextBlock();
        performanceRamStatValue ??= new TextBlock();
        loadingLabel ??= CreateMutedTextBlock();

        _quickVersionCombo ??= CreateComboBox(_versionItems);
        _quickLoaderCombo ??= CreateComboBox(ProfileLoaderOptions);

        _quickInstallButton ??= CreatePrimaryButton("Quick Install", "#38D6C4", Colors.Black);
        _quickInstallButton.Click -= QuickInstallButton_Click;
        _quickInstallButton.Click += QuickInstallButton_Click;

        _quickModSearch ??= CreateTextBox();
        _quickModSearch.Watermark = "Search mods";

        _quickModSearchButton ??= CreateSecondaryButton("Quick Search");
        _quickModSearchButton.Click -= QuickModSearchButton_Click;
        _quickModSearchButton.Click += QuickModSearchButton_Click;

        _playOverlay ??= new Border();
        _playOverlayIcon ??= new TextBlock();
        _playOverlayLabel ??= new TextBlock();

        _quickModResults.ItemsSource = _quickSearchResults;
        
        // Use a more robust detachment and re-attachment for the play button
        var playStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var icon = DetachFromParent(_playOverlayIcon);
        var label = DetachFromParent(_playOverlayLabel);
        if (icon != null) playStack.Children.Add(icon);
        if (label != null) playStack.Children.Add(label);
        
        var accentColor = Color.Parse(_settings.AccentColor);
        _playOverlay.Background = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B));
        _playOverlay.BorderBrush = new SolidColorBrush(accentColor);
        _playOverlay.BorderThickness = new Thickness(1);
        _playOverlay.CornerRadius = new CornerRadius(20);
        _playOverlay.Padding = new Thickness(24, 12);
        
        _playOverlayIcon.Foreground = new SolidColorBrush(accentColor);
        _playOverlayIcon.FontSize = 24;
        _playOverlayIcon.Text = "▶";
        
        _playOverlayLabel.Foreground = Brushes.White;
        _playOverlayLabel.FontSize = 18;
        _playOverlayLabel.FontWeight = FontWeight.Bold;
        _playOverlayLabel.Margin = new Thickness(12, 0, 0, 0);
        _playOverlayLabel.Text = "PLAY";

        _playOverlay.Child = playStack;
        _playOverlay.PointerPressed -= PlayOverlay_PointerPressed;
        _playOverlay.PointerPressed += PlayOverlay_PointerPressed;
        _playOverlay.Cursor = new Cursor(StandardCursorType.Hand);

        _instanceEditorOverlay ??= BuildInstanceEditorOverlay();
        _accountsListPanel ??= new StackPanel();
        _accountsOverlay ??= BuildAccountsOverlay();
        PbProgress = pbProgress;
        ModrinthSearchInput = modrinthSearchInput;
        UpdateSelectedProjectDetails();
    }

    private Border BuildInstanceEditorOverlay()
    {
        var cancelButton = CreateSecondaryButton("Cancel");
        cancelButton.Click += (_, _) => _instanceEditorOverlay.IsVisible = false;

        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(170, 5, 8, 16)),
            Padding = new Thickness(32),
            Child = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 460,
                Children =
                {
                    CreateGlassPanel(new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Edit Instance",
                                Foreground = Brushes.White,
                                FontSize = 22,
                                FontWeight = FontWeight.Bold
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Name"),
                                    DetachFromParent(profileNameInput)!
                                }
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Loader"),
                                    DetachFromParent(profileLoaderCombo)!
                                }
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Game Version"),
                                    new Grid
                                    {
                                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                                        ColumnSpacing = 8,
                                        Children =
                                        {
                                            DetachFromParent(instanceCategoryCombo)!.With(column: 0),
                                            DetachFromParent(instanceVersionCombo)!.With(column: 1)
                                        }
                                    }
                                }
                            },
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    CreatePanelEyebrow("Game Directory Override"),
                                    DetachFromParent(profileGameDirInput)!
                                }
                            },
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                                ColumnSpacing = 10,
                                Children =
                                {
                                    DetachFromParent(createProfileButton)!.With(column: 0),
                                    DetachFromParent(renameProfileButton)!.With(column: 1),
                                    cancelButton!.With(column: 2)
                                }
                            }
                        }
                    }, padding: new Thickness(24), margin: new Thickness(0))
                }
            }
        };
    }

    private void ShowAccountsOverlay()
    {
        RefreshAccountsList();
        _accountsOverlay.IsVisible = true;
        if (accountsNavButton != null) accountsNavButton.IsVisible = false;
    }

    private bool _isAuthenticating;
    private void RefreshAccountsList()
    {
        _accountsListPanel.Children.Clear();
        foreach (var account in _settings.Accounts.ToList())
        {
            var isSelected = account.Id == _settings.SelectedAccountId;

            var avatar = new TextBlock
            {
                Text = "🧑",
                FontSize = 24,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            var nameBlock = new TextBlock
            {
                Text = account.Username,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                FontSize = 14
            };

            var typeColor = account.Provider == "microsoft" ? "#5B80FF" : "#A0A8B8";
            var typeLabel = account.Provider == "microsoft" ? "Microsoft" : "Offline";

            var typeBlock = new TextBlock
            {
                Text = typeLabel,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse(typeColor))
            };

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameBlock, typeBlock } };

            var removeBtn = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#FF5B5B")),
                IsVisible = false 
            };
            removeBtn.Click += (_, _) =>
            {
                _settings.Accounts.Remove(account);
                if (_settings.SelectedAccountId == account.Id)
                {
                    _settings.SelectedAccountId = string.Empty;
                    usernameInput.Text = string.Empty;
                    UsernameInput_TextChanged();
                }
                _settingsStore.Save(_settings);
                RefreshAccountsList();
                UpdateAccountsButtonText();
            };

            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children = { avatar.With(column: 0), textStack.With(column: 1), removeBtn.With(column: 2) }
            };

            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1F2E")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                BorderBrush = isSelected ? new SolidColorBrush(Color.Parse("#38D6C4")) : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 2 : 0),
                Child = rowGrid
            };

            card.PointerEntered += (_, _) => { removeBtn.IsVisible = true; card.Background = new SolidColorBrush(Color.Parse("#22283A")); };
            card.PointerExited += (_, _) => { removeBtn.IsVisible = false; card.Background = new SolidColorBrush(Color.Parse("#1A1F2E")); };

             card.PointerPressed += (_, _) =>
            {
                _settings.SelectedAccountId = account.Id;
                usernameInput.Text = account.Username;
                UsernameInput_TextChanged();
                _settingsStore.Save(_settings);
                RefreshAccountsList();
                UpdateAccountsButtonText();
                _accountsOverlay.IsVisible = false;
                if (accountsNavButton != null) accountsNavButton.IsVisible = true;
            };

            _accountsListPanel.Children.Add(card);
        }
    }

    private async Task AddOfflineAccountAsync()
    {
        var username = await DialogService.ShowTextInputAsync(this, "Add Offline Account", "Enter your username:");
        if (string.IsNullOrWhiteSpace(username)) return;

        var acc = new LauncherAccount { Provider = "offline", Username = username.Trim(), DisplayName = username.Trim() };
        _settings.Accounts.Add(acc);
        _settings.SelectedAccountId = acc.Id;
        usernameInput.Text = acc.Username;
        UsernameInput_TextChanged();
        _settingsStore.Save(_settings);
        UpdateAccountsButtonText();
        RefreshAccountsList();
    }

    private LauncherAccount? GetSelectedAccount()
        => _settings.Accounts.FirstOrDefault(a => a.Id == _settings.SelectedAccountId);

    private string GetActiveUsername()
    {
        var selectedAccount = GetSelectedAccount();
        if (selectedAccount != null && !string.IsNullOrWhiteSpace(selectedAccount.Username))
            return selectedAccount.Username;

        return usernameInput.Text?.Trim() ?? string.Empty;
    }

    private bool IsUsingMicrosoftAccount()
        => string.Equals(GetSelectedAccount()?.Provider, "microsoft", StringComparison.OrdinalIgnoreCase);

    private bool HasManualSkinOverride()
    {
        var manualSkinPath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skin.png");
        return string.Equals(_settings.CustomSkinPath, manualSkinPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(manualSkinPath);
    }

    private bool HasManualCapeOverride()
    {
        var manualCapePath = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "cape.png");
        return string.Equals(_settings.CustomCapePath, manualCapePath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(manualCapePath);
    }

    private async Task<MSession> BuildLaunchSessionAsync(CancellationToken cancellationToken)
    {
        var selectedAccount = GetSelectedAccount();
        if (selectedAccount != null && string.Equals(selectedAccount.Provider, "microsoft", StringComparison.OrdinalIgnoreCase))
        {
            if (selectedAccount.IsExpired)
            {
                var refreshed = await TryRefreshAccountAsync(selectedAccount);
                if (!refreshed)
                    throw new InvalidOperationException("The selected Microsoft account could not be refreshed. Sign in again.");

                selectedAccount = GetSelectedAccount();
            }

            if (selectedAccount == null || string.IsNullOrWhiteSpace(selectedAccount.MinecraftAccessToken))
                throw new InvalidOperationException("The selected Microsoft account is missing a Minecraft access token. Sign in again.");

            if (string.IsNullOrWhiteSpace(selectedAccount.Uuid))
                throw new InvalidOperationException("The selected Microsoft account is missing the Minecraft profile UUID.");

            return new MSession
            {
                Username = selectedAccount.Username,
                UUID = selectedAccount.Uuid,
                AccessToken = selectedAccount.MinecraftAccessToken,
                Xuid = selectedAccount.Xuid,
                UserType = "msa"
            };
        }

        var username = GetActiveUsername();
        var session = MSession.CreateOfflineSession(username);
        session.UUID = string.IsNullOrWhiteSpace(_playerUuid)
            ? Character.GenerateUuidFromUsername(username)
            : _playerUuid;
        return session;
    }

    private async Task<bool> TryRefreshAccountAsync(LauncherAccount account)
    {
        if (account.Provider != "microsoft" || !account.IsExpired) return true;

        try
        {
            var clientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId) ? "00000000402b5328" : _settings.MicrosoftClientId;
            LauncherLog.Info($"[Microsoft Auth] Refreshing token for {account.Username}...");
            
            var refreshed = await _authService.RefreshMinecraftAccountAsync(clientId, account, CancellationToken.None);
            
            // Update existing account in settings
            var idx = _settings.Accounts.FindIndex(a => a.Id == account.Id);
            if (idx != -1)
            {
                _settings.Accounts[idx] = refreshed;
                _settingsStore.Save(_settings);
                return true;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Info($"[Microsoft Auth] Refresh failed for {account.Username}: {ex.Message}");
        }
        return false;
    }

    private async Task AddMicrosoftAccountAsync()
    {
        if (_isAuthenticating) return;
        _isAuthenticating = true;

        var clientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId) ? "00000000402b5328" : _settings.MicrosoftClientId;
        using var cts = new CancellationTokenSource();
        
        try
        {
            LauncherLog.Info("[Microsoft Auth] Starting device code login...");
            var session = await _authService.BeginDeviceLoginAsync(clientId, cts.Token);

            // Open browser and show premium dialog
            Process.Start(new ProcessStartInfo { FileName = session.VerificationUri, UseShellExecute = true });
            
            var dialogTask = DialogService.ShowMicrosoftAuthDialogAsync(this, session.UserCode, session.VerificationUri, cts);
            var pollTask = _authService.CompleteDeviceLoginAsync(clientId, session, cts.Token);

            var completedTask = await Task.WhenAny(dialogTask, pollTask);

            if (completedTask == pollTask)
            {
                var account = await pollTask;
                var existing = _settings.Accounts.FirstOrDefault(a => a.Uuid == account.Uuid && a.Provider == "microsoft");
                if (existing != null) _settings.Accounts.Remove(existing);

                _settings.Accounts.Add(account);
                _settings.SelectedAccountId = account.Id;
                usernameInput.Text = account.Username;
                UsernameInput_TextChanged();
                _settingsStore.Save(_settings);
                
                LauncherLog.Info($"[Microsoft Auth] Successfully logged in as {account.Username}");
                UpdateAccountsButtonText();
                RefreshAccountsList();
            }
            else
            {
                LauncherLog.Info("[Microsoft Auth] Login cancelled by user.");
            }
        }
        catch (OperationCanceledException)
        {
            LauncherLog.Info("[Microsoft Auth] Login timed out or cancelled.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Authentication Failed", ex.Message);
        }
        finally
        {
            _isAuthenticating = false;
        }
    }



    private Border BuildAccountsOverlay()
    {
        var closeButton = new Button
        {
            Content = "×",
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            FontSize = 24,
            Padding = new Thickness(8, 0)
        };
        closeButton.Click += (_, _) => 
        {
            _accountsOverlay.IsVisible = false;
            if (accountsNavButton != null)
            {
                accountsNavButton.IsVisible = true;
                accountsNavButton.Opacity = 1.0;
                accountsNavButton.RenderTransform = TransformOperations.Parse("scale(1.0)");
            }
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = "Accounts", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center },
                closeButton.With(column: 1)
            }
        };

        var addMicrosoftBtn = CreatePrimaryButton("Add Microsoft Account", "#5B80FF", Colors.White);
        addMicrosoftBtn.Click += async (_, _) => await AddMicrosoftAccountAsync();

        var addOfflineBtn = CreateSecondaryButton("Add Offline");
        addOfflineBtn.Click += async (_, _) => await AddOfflineAccountAsync();

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            Children =
            {
                addMicrosoftBtn.With(column: 0),
                addOfflineBtn.With(column: 1)
            }
        };

        var style = _settings.Style;
        var bgStr = !string.IsNullOrWhiteSpace(style.AccountsOverlayBackground) ? style.AccountsOverlayBackground : "#F0090C12";
        var brdStr = !string.IsNullOrWhiteSpace(style.AccountsOverlayBorderColor) ? style.AccountsOverlayBorderColor : "#641E283C";
        var rad = double.IsNaN(style.AccountsOverlayCornerRadius) ? 0 : style.AccountsOverlayCornerRadius;
        var thick = double.IsNaN(style.AccountsOverlayBorderThickness) ? 1 : style.AccountsOverlayBorderThickness;

        var panel = new Border
        {
            Width = 380,
            Background = new SolidColorBrush(Color.Parse(bgStr)),
            BorderBrush = new SolidColorBrush(Color.Parse(brdStr)),
            BorderThickness = new Thickness(thick, 0, 0, 0),
            CornerRadius = new CornerRadius(rad, 0, 0, rad),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                Children =
                {
                    header.With(row: 0),
                    new ScrollViewer
                    {
                        Margin = new Thickness(0, 20),
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                        Content = _accountsListPanel.With(sp => sp.Spacing = 8)
                    }.With(row: 1),
                    footer.With(row: 2)
                }
            }
        };

        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            ZIndex = 100,
            Child = panel
        };
    }

    private void UpdateAccountsButtonText()
    {
        if (accountsNavButton != null)
        {
            var activeName = GetSelectedAccount()?.Username;
            if (string.IsNullOrWhiteSpace(activeName))
                activeName = string.IsNullOrWhiteSpace(usernameInput.Text) ? _settings.Username : usernameInput.Text;
            if (string.IsNullOrWhiteSpace(activeName))
                activeName = "Accounts";

            // Make it look premium
            var fg = !string.IsNullOrWhiteSpace(_settings.Style.NavButtonForeground) ? _settings.Style.NavButtonForeground : "#A4A8B1";
            var accent = !string.IsNullOrWhiteSpace(_settings.Style.AccentColor) ? _settings.Style.AccentColor! : (!string.IsNullOrWhiteSpace(_settings.AccentColor) ? _settings.AccentColor : "#6E5BFF");
            
            accountsNavButton.Content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(40, Color.Parse(accent).R, Color.Parse(accent).G, Color.Parse(accent).B)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(6),
                        Child = new TextBlock
                        {
                            Text = "🧑",
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Color.Parse(accent)),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = activeName,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse(fg)),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                }
            };
            
            // Add transitions if not already added
            if (accountsNavButton.Transitions == null)
            {
                accountsNavButton.Transitions = new Transitions
                {
                    new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
                    new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
                };
            }
        }
    }

    private Control BuildFeaturedServersSection()
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 16, 0, 12),
            Children =
            {
                new Border
                {
                    Width = 3, Height = 16,
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(Color.Parse(_settings.AccentColor)),
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = "FEATURED SERVERS",
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#8E96A8")),
                    LetterSpacing = 1.5,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        var breakpointCard = BuildServerCard(
            bgAsset: "avares://AetherLauncher/assets/launcher_background.png",
            logoAsset: "avares://AetherLauncher/assets/breakpoint-logo.png",
            serverName: "BreakPoint MC",
            tagLine: "⭐ FEATURED",
            description: "Cracked Server. Optimised for Aether.",
            ip: "breakpoint.mcsrv.net",
            accentHex: "#7E6AFF",
            isFeatured: true
        );

        var hypixelCard = BuildServerCard(
            bgAsset: "avares://AetherLauncher/assets/hypixel_card_bg.png",
            serverName: "Hypixel",
            tagLine: "MINI-GAMES",
            description: "The world's largest server.",
            ip: "mc.hypixel.net",
            accentHex: "#F4C430",
            isFeatured: false
        );

        var donutCard = BuildServerCard(
            bgAsset: "avares://AetherLauncher/assets/donut_smp_card_bg.png",
            serverName: "Donut SMP",
            tagLine: "SURVIVAL",
            description: "Community survival SMP.",
            ip: "play.donutsmp.net",
            accentHex: "#FF8C42",
            isFeatured: false
        );

        var cardsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3.5*, *, *"),
            ColumnSpacing = 10,
            Height = 135,
            Children =
            {
                breakpointCard,
                hypixelCard.With(column: 1),
                donutCard.With(column: 2)
            }
        };

        return new StackPanel { Children = { header, cardsGrid } };
    }

    private Border BuildServerCard(string bgAsset, string serverName, string tagLine, string description, string ip, string accentHex, bool isFeatured, string? logoAsset = null)
    {
        ImageBrush? bgBrush = null;
        try
        {
            var bmp = new Bitmap(AssetLoader.Open(new Uri(bgAsset)));
            bgBrush = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
        }
        catch { }

        // Logo overlay (shows when NOT hovered)
        var logoContent = new Panel();
        if (!string.IsNullOrEmpty(logoAsset))
        {
            try
            {
                var logoBmp = new Bitmap(AssetLoader.Open(new Uri(logoAsset)));
                logoContent.Children.Add(new Image
                {
                    Source = logoBmp,
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Transitions = new Transitions { new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) } }
                });
            }
            catch { }
        }

        // Overlay that shows on hover
        var hoverOverlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(230, 9, 12, 20), 0),
                    new GradientStop(Color.FromArgb(140, 9, 12, 20), 0.6),
                    new GradientStop(Color.FromArgb(0, 9, 12, 20), 1)
                }
            },
            Opacity = 0,
            Transitions = new Transitions
            {
                new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }
            },
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 14, 14),
                Spacing = 4,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(40, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(120, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new TextBlock
                        {
                            Text = tagLine,
                            FontSize = 11,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(accentHex)),
                            LetterSpacing = 1
                        }
                    },
                    new TextBlock
                    {
                        Text = serverName,
                        FontSize = isFeatured ? 20 : 16,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = description,
                        FontSize = 12.5,
                        Foreground = new SolidColorBrush(Color.Parse("#A0AABB")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = $"Copy IP: {ip}",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.Parse(accentHex)),
                        Background = Brushes.Transparent,
                        Padding = new Thickness(0, 2, 0, 0),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Command = new RelayCommand(() => CopyServerIpToClipboard(ip))
                    }
                }
            }
        };

        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Background = bgBrush != null ? bgBrush : new SolidColorBrush(Color.Parse("#1A1F2E")),
            BorderBrush = new SolidColorBrush(Color.FromArgb(isFeatured ? (byte)80 : (byte)40, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B)),
            BorderThickness = new Thickness(1),
            BoxShadow = isFeatured ? new BoxShadows(new BoxShadow
            {
                Blur = 20,
                Color = Color.FromArgb(100, Color.Parse(accentHex).R, Color.Parse(accentHex).G, Color.Parse(accentHex).B),
                OffsetX = 0,
                OffsetY = 0
            }) : default,
            Child = new Grid { Children = { logoContent, hoverOverlay } }
        };

        card.PointerEntered += (_, _) => { hoverOverlay.Opacity = 1; logoContent.Opacity = 0; };
        card.PointerExited += (_, _) => { hoverOverlay.Opacity = 0; logoContent.Opacity = 1; };

        return card;
    }

    private async void CopyServerIpToClipboard(string ip)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null) return;
        await topLevel.Clipboard.SetTextAsync(ip);
    }

    private async void CopyToClipboard(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        await topLevel.Clipboard!.SetTextAsync(text);
    }

    private void EnsureSectionsBuilt()
    {
        EnsureFallbackControlsInitialized();
        launchSection ??= BuildLaunchDeck();
        modrinthSection ??= BuildModrinthDeck();
        profilesSection ??= BuildProfilesDeck();
        performanceSection ??= BuildPerformanceDeck();
        settingsSection ??= BuildSettingsDeck();
        layoutSection ??= BuildLayoutDeck();

          launchSection.IsVisible = _activeSection == "launch";
          modrinthSection.IsVisible = _activeSection == "modrinth";
          profilesSection.IsVisible = _activeSection == "profiles";
          performanceSection.IsVisible = _activeSection == "performance";
          settingsSection.IsVisible = _activeSection == "settings";
          layoutSection.IsVisible = _activeSection == "layout";
    }

    private void InvalidateUiCache()
    {
        // Sections
        launchSection = null!;
        modrinthSection = null!;
        profilesSection = null!;
        performanceSection = null!;
        settingsSection = null!;
        layoutSection = null!;
        
        // Overlays
          _instanceEditorOverlay = null!;
        _accountsOverlay = null!;
          _namedSlots = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        _playOverlay = new Border();
        
        // Navigation
        launchNavButton = null!;
        profilesNavButton = null!;
        modrinthNavButton = null!;
        performanceNavButton = null!;
        settingsNavButton = null!;
        layoutNavButton = null!;
        accountsNavButton = null!;
        
        // Shared Labels & Fields
        heroInstanceLabel = null!;
        heroPerformanceLabel = null!;
        loadingLabel = null!;
        statusLabel = null!;
        installDetailsLabel = null!;
        activeProfileBadge = null!;
        activeContextLabel = null!;
        usernameInput = null!;
        
        // Progress & Stats
        pbFiles = null!;
        pbProgress = null!;
        homeFpsStatValue = null!;
        homeRamStatValue = null!;
        performanceFpsStatValue = null!;
        performanceRamStatValue = null!;
        
        // Input Controls
        cbVersion = null!;
        minecraftVersion = null!;
        downloadVersionButton = null!;
        profileNameInput = null!;
        profileGameDirInput = null!;
        profileLoaderCombo = null!;
        instanceVersionCombo = null!;
        instanceCategoryCombo = null!;
        _quickVersionCombo = null!;
        _quickLoaderCombo = null!;
        _quickInstallButton = null!;
        _quickModSearch = null!;
        _quickModSearchButton = null!;
        _accountsListPanel = null!;
        _playOverlay = null!;
        _playOverlayIcon = null!;
        _playOverlayLabel = null!;
        
        // Missed Premium UI Fields
        characterImage = null!;
        activeProfileBadge = null!;
        activeContextLabel = null!;
        installModeLabel = null!;
        btnStart = null!;
        profileListBox = null!;
        modrinthResultsListBox = null!;
        modrinthDetailsBox = null!;
        modrinthResultsSummary = null!;
        installSelectedButton = null!;
        importMrpackButton = null!;
        profileInspectorTitle = null!;
        profileInspectorMeta = null!;
        profileInspectorPath = null!;
        clearProfileButton = null!;
        modrinthSearchInput = null!;
        modrinthProjectTypeCombo = null!;
        modrinthLoaderCombo = null!;
        modrinthSourceCombo = null!;
        modrinthSearchButton = null!;
        modrinthVersionInput = null!;
    }

    private Control BuildContent()
    {
        EnsureSectionsBuilt();
        var style = _settings.Style;

        var outerMargin = IsTopNavigationEnabled() ? new Thickness(28, 4, 28, 24) : new Thickness(22);
        if (!double.IsNaN(style.ContentSpacing)) outerMargin = new Thickness(style.ContentSpacing);
        
        var innerPadding = double.IsNaN(style.ContentPadding) ? new Thickness(18) : new Thickness(style.ContentPadding);
        IBrush bg = !string.IsNullOrWhiteSpace(style.ContentBackground) ? new SolidColorBrush(Color.Parse(style.ContentBackground)) : Brushes.Transparent;

          var launch = TryPlaceInSection("LaunchSection", DetachFromParent(launchSection)!);
          var modrinth = TryPlaceInSection("ModrinthSection", DetachFromParent(modrinthSection)!);
          var profiles = TryPlaceInSection("ProfilesSection", DetachFromParent(profilesSection)!);
          var performance = TryPlaceInSection("PerformanceSection", DetachFromParent(performanceSection)!);
          var settings = TryPlaceInSection("SettingsSection", DetachFromParent(settingsSection)!);
          var layout = TryPlaceInSection("LayoutSection", DetachFromParent(layoutSection)!);

          return new Grid
        {
            Margin = outerMargin,
            Children =
            {
                new Border
                {
                    Background = bg,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(30, 100, 120, 180)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(24),
                    Padding = innerPadding,
                    Child = new Grid
                    {
                        Children =
                        {
                              launch!,
                              modrinth!,
                              profiles!,
                              performance!,
                              settings!,
                              layout!
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
                    FontSize = 16,
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
                DetachFromParent(heroInstanceLabel)!,
                DetachFromParent(heroPerformanceLabel)!,
                new Border { Height = 12 },
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
            RadiusX = new RelativeScalar(0.8, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
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
        _playOverlayIcon.FontSize = 18;
        _playOverlayLabel.Text = "PLAY";
        _playOverlayLabel.FontSize = 15;
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
                    new TextBlock { Text = "□", FontSize = 15, Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor)) },
                    new TextBlock { Text = "Mods", FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0) }.With(column: 1),
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
                    new TextBlock { Text = "〓", FontSize = 15, Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor)) },
                    new TextBlock { Text = "Instances", FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(12, 0) }.With(column: 1),
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

        var skinContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "●", FontSize = 10, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Skin", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var skinBtn = new Button { Content = skinContent, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        skinBtn.Click += async (_, _) => await ChangeSkinAsync();
        ApplyHoverMotion(skinBtn);

        var capeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "■", FontSize = 10, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Cape", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var capeBtn = new Button { Content = capeContent, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        capeBtn.Click += async (_, _) => await ChangeCapeAsync();
        ApplyHoverMotion(capeBtn);

        var resetContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "×", FontSize = 12, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Reset", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
        var resetBtn = new Button { Content = resetContent, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), CornerRadius = new CornerRadius(12), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        resetBtn.Click += (_, _) => {
            _settings.CustomSkinPath = string.Empty;
            _settingsStore.Save(_settings);
            // SyncSkinShuffleAvatarToLauncher removed
        };
        ApplyHoverMotion(resetBtn);

        var avatarPanel = CreateGlassPanel(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Avatar", FontSize = 12.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Opacity = 0.8 },
                new Border { Height = 290, Child = DetachFromParent(characterImage) },
                new TextBlock 
                { 
                    Text = "Character features (Skins/Capes) are under development.", 
                    Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), 
                    FontSize = 10, 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                    ColumnSpacing = 8,
                    Children = { skinBtn.With(column: 0), capeBtn.With(column: 1), resetBtn.With(column: 2) }
                }
            }
        }, padding: new Thickness(24), margin: new Thickness(0));

        _avatarGlass = avatarPanel;
        _avatarControls = (StackPanel)avatarPanel.Child!;
        _avatarActions = (Grid)_avatarControls.Children[3];

        _avatarGlass.PointerEntered += (s, e) => { if (_isNarrowMode) SetAvatarExpansion(true); };
        _avatarGlass.PointerExited += (s, e) => { if (_isNarrowMode) SetAvatarExpansion(false); };

        _mainContentStack = new StackPanel
        {
            Spacing = 40,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 48, 0, 0),
            Children =
            {
                topInfo,
                new StackPanel 
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 16,
                    Children = { _playOverlay, actionsGroup }
                },
                BuildFeaturedServersSection()
            }
        };

        var mainRow = new Grid
        {
            Children =
            {
                _mainContentStack,
                avatarPanel.With(a => {
                    a.HorizontalAlignment = HorizontalAlignment.Right;
                    a.VerticalAlignment = VerticalAlignment.Top;
                    a.ZIndex = 10;
                })
            }
        };

        var statsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 20,
            Children =
            {
                Create1to1StatCard("FPS", homeFpsStatValue, "Average performance"),
                Create1to1StatCard("RAM", homeRamStatValue, "Memory usage").With(column: 1)
            }
        };

        _homeStatusBar = new Border
        {
            Height = 110,
            Background = new SolidColorBrush(Color.Parse("#0D111C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A3143")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(32, 20),
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new StackPanel
                    {
                        Children =
                        {
                            statusLabel.With(tb => {
                                tb.FontSize = 15;
                                tb.FontWeight = FontWeight.Black;
                                tb.Foreground = Brushes.White;
                            }),
                            installDetailsLabel.With(tb => {
                                tb.FontSize = 12;
                                tb.Foreground = new SolidColorBrush(Color.Parse("#8E98AC"));
                                tb.Margin = new Thickness(0, 4, 0, 0);
                            })
                        }
                    },
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            pbFiles.With(pb => {
                                pb.Height = 6;
                                pb.CornerRadius = new CornerRadius(3);
                            }),
                            pbProgress.With(pb => {
                                pb.Height = 14;
                                pb.CornerRadius = new CornerRadius(7);
                                pb.Background = new SolidColorBrush(Color.Parse("#1A1F2E"));
                                pb.Foreground = new SolidColorBrush(Color.Parse(_settings.AccentColor));
                            })
                        }
                    }
                }
            }
        };

        return new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 40,
                        Margin = new Thickness(24),
                        Children = { mainRow, statsRow }
                    }
                },
                _homeStatusBar.With(row: 1)
            }
        };
    }

    private Border Create1to1StatCard(string title, TextBlock valueBlock, string subLabel)
    {
        var accentColor = Color.Parse(_settings.AccentColor);
        valueBlock.FontSize = 32;
        valueBlock.FontWeight = FontWeight.Black;
        valueBlock.Foreground = new SolidColorBrush(accentColor);
        valueBlock.Text = "00";

        return CreateGlassPanel(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = title, FontSize = 12.5, Foreground = new SolidColorBrush(Color.Parse("#8E96A8")), FontWeight = FontWeight.Bold },
                valueBlock,
                new TextBlock { Text = subLabel, FontSize = 11.5, Foreground = new SolidColorBrush(Color.Parse("#667899")) }
            }
        }, padding: new Thickness(16), margin: new Thickness(0));
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

        modrinthSourceCombo.CornerRadius = new CornerRadius(16);
        modrinthSourceCombo.Height = 42;
        modrinthSourceCombo.Background = Brushes.Transparent;
        modrinthSourceCombo.BorderBrush = new SolidColorBrush(Color.Parse("#2A3143"));

        modrinthSearchButton.CornerRadius = new CornerRadius(16);
        modrinthSearchButton.Height = 42;
        SetButtonText(modrinthSearchButton, "🔍 Search");
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
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12, 0, 12, 24) // Match image padding
        };

        filterRow.Children.Add(modrinthSearchInput.With(column: 0));

        var sourceText = new TextBlock { Text = "Source", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var sourcePanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { sourceText, modrinthSourceCombo } };
        filterRow.Children.Add(sourcePanel.With(column: 1));
        
        var loaderText = new TextBlock { Text = "Loader", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var loaderPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { loaderText, modrinthLoaderCombo } };
        filterRow.Children.Add(loaderPanel.With(column: 2));

        var versionText = new TextBlock { Text = "Version", Foreground = new SolidColorBrush(Color.Parse("#A0A8B8")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) };
        var versionPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { versionText, modrinthVersionInput } };
        filterRow.Children.Add(versionPanel.With(column: 3));

        filterRow.Children.Add(modrinthProjectTypeCombo.With(column: 4));
        
        filterRow.Children.Add(modrinthSearchButton.With(column: 5));
        
        // ── Card Item Template ────────────────────────────────────────────

        modrinthResultsListBox.Background = Brushes.Transparent;
        modrinthResultsListBox.ItemsPanel = new FuncTemplate<Panel?>(() => new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 });
        modrinthResultsListBox.ItemsSource = _searchResults;
        modrinthResultsListBox.Margin = new Thickness(4, 0);

        modrinthResultsListBox.ItemTemplate = new FuncDataTemplate<ModrinthProject>((project, _) =>
        {
            bool isInstalled = _selectedProfile?.InstalledModIds.Contains(project?.ProjectId ?? "") ?? false;
            var installBtn = new Button
            {
                Content = isInstalled ? "Installed" : "Install",
                IsEnabled = !isInstalled,
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
                                    FontSize = 14,
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

        instancesHeader.Children.Add(new TextBlock
        {
            Text = "Instances",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        }.With(column: 0));

        var importBackupBtn = CreateCompactSecondaryButton("⤓ Import Zip");
        importBackupBtn.Click += async (_, _) => await ImportProfileZipAsync();

        var importDirBtn = CreateCompactSecondaryButton("📂 Import Dir");
        importDirBtn.Click += async (_, _) => await ImportInstanceFolderAsync();

        instancesHeader.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { importDirBtn, importBackupBtn }
        }.With(column: 1));

        var addBtn = CreatePrimaryButton("+", "#38D6C4", Colors.Black);
        addBtn.Width = 36;
        addBtn.Height = 36;
        addBtn.CornerRadius = new CornerRadius(18);
        addBtn.Padding = new Thickness(0);
        addBtn.Content = new TextBlock
        {
            Text = "+",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        };
        addBtn.VerticalAlignment = VerticalAlignment.Center;
        addBtn.Click += (_, _) =>
        {
            ClearSelectedProfile();
            createProfileButton.IsVisible = true;
            renameProfileButton.IsVisible = false;
            _instanceEditorOverlay!.IsVisible = true;
        };
        instancesHeader.Children.Add(addBtn.With(column: 2));

        var modsHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(8, 0, 8, 12),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Installed Mods", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                CreateCompactSecondaryButton("⚠ Scan Conflicts").With(btn =>
                {
                    btn.Click += async (_, _) =>
                    {
                        if (_selectedProfile != null) await ScanForModConflictsAsync(_selectedProfile);
                    };
                })
            }
        };

        Button CreateInlineProfileAction(string glyph, string hexColor)
        {
            var button = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse(hexColor)),
                Focusable = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = glyph,
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            return button;
        }

        profileListBox.Background = Brushes.Transparent;
        profileListBox.BorderThickness = new Thickness(0);
        profileListBox.Padding = new Thickness(0);
        profileListBox.ItemTemplate = new FuncDataTemplate<LauncherProfile>((profile, _) =>
        {
            if (profile == null) return new Border();

            var modifyButton = CreateInlineProfileAction("▶", "#38D6C4");
            modifyButton.Click += (_, _) => OpenProfileEditor(profile);

            var renameButton = CreateInlineProfileAction("✎", "#B7C4E9");
            renameButton.Click += (_, _) => OpenProfileEditor(profile);

            var deleteButton = CreateInlineProfileAction("✕", "#FF6B86");
            deleteButton.Click += async (_, _) =>
            {
                _selectedProfile = profile;
                profileListBox.SelectedItem = profile;
                await DeleteSelectedProfileAsync(profile);
            };

            return new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A2030")),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{profile.Name} [{profile.LoaderDisplay}]",
                            Foreground = Brushes.White,
                            FontSize = 14,
                            FontWeight = FontWeight.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }.With(column: 0),
                        modifyButton.With(column: 1),
                        renameButton.With(column: 2),
                        deleteButton.With(column: 3)
                    }
                }
            };
        });

        var modsListBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ItemsSource = _modItems
        };
        modsListBox.ItemTemplate = new FuncDataTemplate<ModItem>((modItem, _) =>
        {
            if (modItem == null) return new Border();

            var enableToggle = new ToggleSwitch
            {
                OnContent = "ON",
                OffContent = "OFF",
                Margin = new Thickness(0, 0, 16, 0)
            };
            enableToggle[!ToggleSwitch.IsCheckedProperty] = new Avalonia.Data.Binding(nameof(ModItem.IsEnabled));

            var deleteBtn = new Button
            {
                Content = "🗑",
                Foreground = Brushes.Tomato,
                Background = Brushes.Transparent,
                FontSize = 18,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8)
            };
            deleteBtn.Click += (_, _) =>
            {
                try
                {
                    if (File.Exists(modItem.FullPath)) File.Delete(modItem.FullPath);
                    _modItems.Remove(modItem);
                }
                catch { }
            };

            var nameBlock = new TextBlock { FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4), TextTrimming = TextTrimming.CharacterEllipsis };
            nameBlock[!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(ModItem.FileName));

            return new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1F2E")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A3143")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                nameBlock,
                                new TextBlock { FontSize = 11, Foreground = Brushes.Gray }.With(tb => tb[!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(ModItem.FileSize)))
                            }
                        }.With(column: 0),
                        enableToggle.With(column: 1),
                        deleteBtn.With(column: 2)
                    }
                }
            };
        });

        var instanceDetails = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                DetachFromParent(profileInspectorTitle)!,
                DetachFromParent(profileInspectorMeta)!,
                DetachFromParent(profileInspectorPath)!
            }
        };

        var instancesPane = CreateGlassPanel(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#111725")),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 0,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#0F1523")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#24324A")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(18),
                        Height = 440,
                        Padding = new Thickness(14),
                        Child = new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            Content = profileListBox
                        }
                    },
                    instanceDetails
                }
            }
        });

        var modsPane = CreateGlassPanel(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#111725")),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(14),
            Child = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0F1420")),
                CornerRadius = new CornerRadius(18),
                Height = 520,
                Padding = new Thickness(14),
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = modsListBox
                }
            }
        });

        return CreateSectionScroller(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 24,
            Margin = new Thickness(4, 4, 4, 60),
            Children =
            {
                new StackPanel
                {
                    Children =
                    {
                        instancesHeader,
                        instancesPane
                    }
                }.With(column: 0),
                new StackPanel
                {
                    Children =
                    {
                        modsHeader,
                        modsPane
                    }
                }.With(column: 1)
            }
        });
    }

    private Control BuildPerformanceDeck()
    {
        var perfFilesPb = new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };
        var perfNetworkPb = new ProgressBar { Height = 4, CornerRadius = new CornerRadius(2), Minimum = 0, Maximum = 100 };

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
        var totalRam = GetSystemRamMb();
        var ramSlider = new Slider 
        { 
            Minimum = 512, 
            Maximum = totalRam, 
            Value = _settings.MaxRamMb,
            SmallChange = 512,
            LargeChange = 1024
        };
        var ramLabel = new TextBlock { Text = $"{_settings.MaxRamMb} MB", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold, Foreground = Brushes.White };
        ramSlider.ValueChanged += (_, e) => {
            var val = (int)(e.NewValue / 512) * 512;
            _settings.MaxRamMb = val;
            ramLabel.Text = $"{val} MB";
            _settingsStore.Save(_settings);
        };

        var jvmArgsInput = CreateTextBox();
        jvmArgsInput.Text = _settings.JvmArgs;
        jvmArgsInput.Watermark = "-Xmx2G -XX:+UseG1GC...";
        jvmArgsInput.TextChanged += (_, _) => {
            _settings.JvmArgs = jvmArgsInput.Text ?? "";
            _settingsStore.Save(_settings);
        };

        var windowWidthInput = CreateTextBox();
        windowWidthInput.Text = _settings.WindowWidth.ToString();
        windowWidthInput.TextChanged += (_, _) => {
            if (int.TryParse(windowWidthInput.Text, out var val)) { _settings.WindowWidth = val; _settingsStore.Save(_settings); }
        };

        var windowHeightInput = CreateTextBox();
        windowHeightInput.Text = _settings.WindowHeight.ToString();
        windowHeightInput.TextChanged += (_, _) => {
            if (int.TryParse(windowHeightInput.Text, out var val)) { _settings.WindowHeight = val; _settingsStore.Save(_settings); }
        };

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

        return CreateSectionScroller(new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(4, 4, 4, 80),
            Children =
            {
                CreateSectionTitle("Settings", "Fine-tune your launch posture and system parameters."),
                CreateGlassPanel(new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        new StackPanel { Spacing = 8, Children = { 
                            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { CreatePanelEyebrow("RAM Allocation"), ramLabel.With(column: 1) } },
                            ramSlider 
                        } },
                        new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Extra JVM Arguments"), jvmArgsInput } },
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,*"),
                            ColumnSpacing = 16,
                            Children =
                            {
                                new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Window Width"), windowWidthInput } },
                                new StackPanel { Spacing = 8, Children = { CreatePanelEyebrow("Window Height"), windowHeightInput } }.With(column: 1)
                            }
                        },
                        new Separator { Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)) },
                        offlineModeToggle,
                        new Separator { Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)) },
                        new StackPanel { Spacing = 8, Children = { 
                            CreatePanelEyebrow("Installation Directory"), 
                            new TextBlock { Text = _defaultMinecraftPath.BasePath, Foreground = Brushes.Gray, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                            CreateSecondaryButton("Change Directory").With(btn => btn.Click += async (_, _) => await ChangeBaseDirectoryAsync())
                        } }
                    }
                })
            }
        });
    }

    private async Task ChangeBaseDirectoryAsync()
    {
        try {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Base Minecraft Directory" });
            if (folders != null && folders.Count > 0)
            {
                var newPath = folders[0].Path.LocalPath;
                _settings.BaseMinecraftPath = newPath;
                _settingsStore.Save(_settings);
                await DialogService.ShowInfoAsync(this, "Directory Changed", "Please restart the launcher to apply the change.");
            }
        } catch (Exception ex) {
            await DialogService.ShowInfoAsync(this, "Error", $"Failed to change directory: {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        var tasks = new List<Task>();
        tasks.Add(CheckForUpdatesAsync());
        
        tasks.Add(PerformFirstRunSetup());
        await Task.WhenAll(tasks);

        // Auto-refresh selected account if needed
        var selectedAcc = _settings.Accounts.FirstOrDefault(a => a.Id == _settings.SelectedAccountId);
        if (selectedAcc != null && selectedAcc.Provider == "microsoft" && selectedAcc.IsExpired)
        {
            LauncherLog.Info($"[Initialize] Selected account {selectedAcc.Username} expired. Attempting refresh...");
            await TryRefreshAccountAsync(selectedAcc);
        }
        
        loadingLabel.Text = string.Empty;
        usernameInput.Text = string.IsNullOrWhiteSpace(_settings.Username) ? Environment.UserName : _settings.Username;
        if (selectedAcc != null && !string.IsNullOrWhiteSpace(selectedAcc.Username))
            usernameInput.Text = selectedAcc.Username;
        UsernameInput_TextChanged();

        profileLoaderCombo.SelectedIndex = 0;
        _quickLoaderCombo.SelectedIndex = 0;
        modrinthProjectTypeCombo.SelectedIndex = 0;
        modrinthLoaderCombo.SelectedIndex = 0;
        minecraftVersion.SelectedIndex = 0;

        RefreshProfiles();
        tasks.Add(ListVersionsAsync(GetSelectedVersionCategory()));

        if (!string.IsNullOrEmpty(_settings.JvmArgs) && (_settings.JvmArgs.Contains("--sun-misc-unsafe-memory-access") || _settings.JvmArgs.Contains("--enable-native-access")))
        {
            _settings.JvmArgs = _settings.JvmArgs
                .Replace("--sun-misc-unsafe-memory-access=allow", "")
                .Replace("--sun-misc-unsafe-memory-access", "")
                .Replace("--enable-native-access=ALL-UNNAMED", "")
                .Replace("--enable-native-access", "")
                .Trim();
            _settingsStore.Save(_settings);
        }

        // Initialize instance version lists
        if (instanceCategoryCombo != null)
        {
            instanceCategoryCombo.SelectedItem = "Versions";
            tasks.Add(ListVersionsAsync("Versions"));
        }

        if (!string.IsNullOrWhiteSpace(_settings.Version))
        {
            cbVersion.SelectedItem = _settings.Version;
            _quickVersionCombo.SelectedItem = _settings.Version;
        }

        SyncModrinthFilters();
        UpdateCharacterPreview();
        UpdateLauncherContext();
        SetProgressState("Ready", 0, 0);

        await Task.WhenAll(tasks);
    }

    public void SetActiveSection(string section)
    {
        _activeSection = section;

        launchSection.IsVisible = section == "home" || section == "launch";
        modrinthSection.IsVisible = section == "modrinth";
        profilesSection.IsVisible = section == "instances" || section == "profiles";
        performanceSection.IsVisible = section == "performance";
        settingsSection.IsVisible = section == "settings";
        layoutSection.IsVisible = section == "layout";

        ApplyNavState(launchNavButton, section == "home" || section == "launch");
        ApplyNavState(modrinthNavButton, section == "modrinth");
        ApplyNavState(profilesNavButton, section == "instances" || section == "profiles");
        ApplyNavState(performanceNavButton, section == "performance");
        ApplyNavState(settingsNavButton, section == "settings");
        ApplyNavState(layoutNavButton, section == "layout");
        if (accountsNavButton != null) ApplyNavState(accountsNavButton, section == "accounts");

        if (section == "modrinth" && _searchResults.Count == 0)
        {
            _ = SearchModrinthAsync();
        }
    }

    private async Task ListVersionsAsync(string category = "Versions")
    {
        await _versionListSemaphore.WaitAsync();
        try
        {
            var items = new List<string>();
            VersionMetadataCollection? manifest = null;

            if (!_settings.OfflineMode)
            {
                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        manifest = await _defaultLauncher.GetAllVersionsAsync();
                        break;
                    }
                    catch (Exception) when (attempt < maxAttempts)
                    {
                        await Task.Delay(200 * attempt);
                    }
                }
            }

            if (manifest != null)
            {
                foreach (var version in manifest)
                {
                    if (version != null && ShouldIncludeVersion(version.Name, version.Type, category))
                    {
                        var vn = version.Name;
                        if (!string.IsNullOrWhiteSpace(vn)) items.Add(vn);
                    }
                }
            }
            else
            {
                // Fallback: Scan local versions (for offline mode or internet failure)
                try
                {
                    var versionsDir = Path.Combine(_defaultMinecraftPath.BasePath, "versions");
                    if (File.Exists(versionsDir) || Directory.Exists(versionsDir))
                    {
                        foreach (var dir in Directory.GetDirectories(versionsDir))
                        {
                            var versionName = Path.GetFileName(dir);
                            if (!string.IsNullOrWhiteSpace(versionName))
                            {
                                // In offline mode/not-manifested local folders, we try to guess the type from the name
                                if (ShouldIncludeVersion(versionName, null, category))
                                    items.Add(versionName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LauncherLog.Info($"[Aether Launcher] Offline version list failed: {ex}");
                }
            }

            Dispatcher.UIThread.Post(() => {
                _versionItems.Clear();
                foreach (var item in items) 
                {
                    if (!_versionItems.Contains(item)) _versionItems.Add(item);
                }

                if (_selectedProfile is not null && !_versionItems.Contains(_selectedProfile.GameVersion))
                    _versionItems.Insert(0, _selectedProfile.GameVersion);

                if ((cbVersion.SelectedItem == null || (cbVersion.SelectedItem is string s && !_versionItems.Contains(s))) && _versionItems.Count > 0)
                {
                    try { 
                        var latest = manifest?.FirstOrDefault(v => v.Type == "release")?.Name;
                        cbVersion.SelectedItem = (latest != null && _versionItems.Contains(latest)) ? latest : _versionItems[0]; 
                    } catch { cbVersion.SelectedIndex = 0; }
                }
            });
        }
        finally
        {
            _versionListSemaphore.Release();
        }
    }

    private static bool ShouldIncludeVersion(string name, string? type, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var t = type?.ToLower() ?? string.Empty;
        var isRelease = t == "release" || Regex.IsMatch(name, @"^\d+(\.\d+)*$");
        var isSnapshot = t == "snapshot" || Regex.IsMatch(name, @"^\d{2}w\d{2}[a-z]$", RegexOptions.IgnoreCase);

        if (string.Equals(category, "Versions", StringComparison.OrdinalIgnoreCase))
            return isRelease;

        if (string.Equals(category, "Snapshots", StringComparison.OrdinalIgnoreCase))
            return isSnapshot;

        // "Other sources" category: anything that isn't a standard release or snapshot (like Forge, Fabric, older alphas, etc.)
        return !isRelease && !isSnapshot;
    }

    private string GetSelectedVersionCategory() =>
        minecraftVersion.SelectedItem?.ToString() ?? VersionCategoryOptions[0];

    private async Task LaunchAsync()
    {
        var activeUsername = GetActiveUsername();
        if (string.IsNullOrWhiteSpace(activeUsername))
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

        // [USER REQUEST] Remove confirmation popup for instant launch
        /*
        var shouldLaunch = await DialogService.ShowConfirmAsync(
            this,
            "Launch confirmation",
            $"Launch {targetLabel} as {usernameInput.Text.Trim()}?");
        if (!shouldLaunch)
            return;
        */

        ToggleBusyState(true, "Priming the launcher...");
        btnStart.Content = "Cancel";
        btnStart.IsEnabled = true; // Allow clicking "Cancel"

        _launchCts = new CancellationTokenSource();
        var token = _launchCts.Token;

        try
        {
            var launcherPath = _selectedProfile is null
                ? _defaultMinecraftPath
                : new MinecraftPath(_selectedProfile.InstanceDirectory);
            
            var launcher = CreateLauncher(launcherPath);

            if (_selectedProfile is not null)
            {
                await EnsureProfileReadyAsync(_selectedProfile, launcher, token);
                
                // Ensure the required mods are installed automatically
                var modsDir = Path.Combine(_selectedProfile.InstanceDirectory, "mods");
                Directory.CreateDirectory(modsDir);
                LauncherLog.Info($"[Launch] Autoinstalling required mods for instance: {_selectedProfile.Name}");
                
                // Custom Skin Loader is always required
                await InstallModIfMissingAsync("customskinloader", _selectedProfile, modsDir, token);

                // FancyMenu integration if enabled
                if (_settings.EnableFancyMenu && SupportsFancyMenu(_selectedProfile))
                {
                    await InstallModIfMissingAsync("fancymenu", _selectedProfile, modsDir, token);
                    await InstallModIfMissingAsync("konkrete", _selectedProfile, modsDir, token);
                }
                
                versionToLaunch = _selectedProfile.VersionId;
            }
            else
            {
                await launcher.InstallAsync(versionToLaunch, token);
            }

            var session = await BuildLaunchSessionAsync(token);

            var targetGameVer = _selectedProfile?.GameVersion ?? versionToLaunch;
            var javaPath = await GetJavaPathForVersionAsync(targetGameVer, token);
            var effectiveGamePath = _selectedProfile is not null && !string.IsNullOrWhiteSpace(_selectedProfile.GameDirectoryOverride)
                ? _selectedProfile.GameDirectoryOverride
                : launcherPath.BasePath;

            EnsureDeathClientThemeResourcePack(effectiveGamePath, targetGameVer);

            var process = await launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
            {
                Session = session,
                JavaPath = javaPath,
                MaximumRamMb = _settings.MaxRamMb,
                ExtraJvmArguments = string.IsNullOrWhiteSpace(_settings.JvmArgs)
                    ? Array.Empty<MArgument>()
                    : _settings.JvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(arg => !arg.Contains("--sun-misc-unsafe-memory-access") && !arg.Contains("--enable-native-access")) // Strip recognized JVM killers
                        .Select(arg => new MArgument(arg)),
                ScreenWidth = _settings.WindowWidth,
                ScreenHeight = _settings.WindowHeight,
                Path = _selectedProfile is not null && !string.IsNullOrWhiteSpace(_selectedProfile.GameDirectoryOverride)
                    ? new MinecraftPath(_selectedProfile.GameDirectoryOverride)
                    : launcherPath
            });

            // CRITICAL: Some versions have these flags hardcoded in their version JSON.
            // We strip them from the FINAL command line here if they cause crashes.
            var scrubbedArgs = process.StartInfo.Arguments;
            string[] problematicFlags = { 
                "--sun-misc-unsafe-memory-access=allow", 
                "--enable-native-access=ALL-UNNAMED" 
            };
            
            foreach (var flag in problematicFlags)
            {
                if (scrubbedArgs.Contains(flag))
                {
                    scrubbedArgs = scrubbedArgs.Replace(flag, "").Trim();
                }
            }
            process.StartInfo.Arguments = scrubbedArgs;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            btnStart.Content = "Launching...";
            btnStart.IsEnabled = false;
            
            token.ThrowIfCancellationRequested(); // Final check
            process.Start();

            _settings.Username = activeUsername;
            _settings.Version = cbVersion.SelectedItem?.ToString() ?? string.Empty;
            _settingsStore.Save(_settings);
            
            Close();
        }
        catch (OperationCanceledException)
        {
            LauncherLog.Info("[Launch] User cancelled the launch process.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Launch failed", $"Failed to launch Minecraft.\n{ex.Message}");
        }
        finally
        {
            _launchCts?.Dispose();
            _launchCts = null;
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
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    RefreshProfiles(downloadedProfile);
                    SetProgressState($"Downloaded {versionToInstall}.", 0, 0);
                });
            }

            _settings.Version = versionToInstall;
            _settingsStore.Save(_settings);
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
        else if (profile.Loader == "quilt")
        {
            await launcher.InstallAsync(profile.GameVersion);
            await EnsureQuiltProfileAsync(profile, cancellationToken);
            await launcher.InstallAsync(profile.VersionId);
        }
        else if (profile.Loader == "forge" || profile.Loader == "neoforge")
        {
            await launcher.InstallAsync(profile.GameVersion);
            await EnsureForgeProfileAsync(profile, cancellationToken);
            await launcher.InstallAsync(profile.VersionId);
        }
        else if (profile.Loader == "vanilla")
        {
            await launcher.InstallAsync(profile.GameVersion);
        }
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

    private async Task EnsureQuiltProfileAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoaderVersion))
            throw new InvalidOperationException("Quilt loader version is missing from the profile.");

        var versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
        var versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
        if (File.Exists(versionJsonPath))
            return;

        Directory.CreateDirectory(versionDirectory);
        var manifestJson = await _modrinthClient.GetStringAsync(
            $"https://meta.quiltmc.org/v3/versions/loader/{profile.GameVersion}/{profile.LoaderVersion}/profile/json",
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

    private async Task EnsureForgeProfileAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoaderVersion))
            throw new InvalidOperationException($"{profile.Loader} loader version is missing from the profile.");

        var versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
        var versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
        if (File.Exists(versionJsonPath))
            return;

        Directory.CreateDirectory(versionDirectory);

        string installerUrl;
        string installerFileName;

        if (profile.Loader == "neoforge")
        {
            installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{profile.LoaderVersion}/neoforge-{profile.LoaderVersion}-installer.jar";
            installerFileName = $"neoforge-{profile.LoaderVersion}-installer.jar";
        }
        else
        {
            var forgeVer = $"{profile.GameVersion}-{profile.LoaderVersion}";
            installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{forgeVer}/forge-{forgeVer}-installer.jar";
            installerFileName = $"forge-{forgeVer}-installer.jar";
        }

        var installerPath = Path.Combine(Path.GetTempPath(), installerFileName);
        
        ToggleBusyState(true, $"Downloading {profile.Loader} installer...");
        using (var httpClient = new System.Net.Http.HttpClient())
        {
            var response = await httpClient.GetAsync(installerUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download installer from {installerUrl}");
            
            using var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);
        }

        ToggleBusyState(true, $"Installing {profile.Loader}...");
        var javaPath = await GetJavaPathForVersionAsync(profile.GameVersion, cancellationToken);
        var installArgs = $"\"{installerPath}\" --installClient \"{profile.InstanceDirectory}\"";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"-jar {installArgs}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new Exception($"Installer failed: {error}");
            }
        }
        else
            throw new Exception("Failed to start installer.");

        var versionsDir = Path.Combine(profile.InstanceDirectory, "versions");
        if (Directory.Exists(versionsDir))
        {
            var createdVersionDir = Directory.GetDirectories(versionsDir)
                .FirstOrDefault(d => Path.GetFileName(d).Contains(profile.LoaderVersion) && Path.GetFileName(d).ToLower().Contains(profile.Loader));

            if (createdVersionDir != null)
            {
                var createdVersionId = Path.GetFileName(createdVersionDir);
                if (!string.Equals(profile.VersionId, createdVersionId, StringComparison.Ordinal))
                {
                    profile.VersionId = createdVersionId;
                    _profileStore.Save(profile);
                }
            }
        }
    }

    private async Task<string> GetJavaPathForVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        int requiredJavaVersion = 8;
        
        // Handle standard 1.x.y versions
        if (gameVersion.StartsWith("1."))
        {
            var parts = gameVersion.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                if (minor >= 21) requiredJavaVersion = 21;
                else if (minor >= 17) requiredJavaVersion = 17;
                else if (minor >= 16) requiredJavaVersion = 16;
            }
        }
        else 
        {
            // Handle custom modern versions like "26.1"
            var parts = gameVersion.Split('.');
            if (parts.Length >= 1 && int.TryParse(parts[0], out var major))
            {
                if (major >= 25) requiredJavaVersion = 25; // Java 25 for extremely modern builds (Class version 69.0)
                else if (major >= 21) requiredJavaVersion = 21; 
                else if (major >= 17) requiredJavaVersion = 17;
            }
        }

        var javaDir = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "runtimes", $"java-{requiredJavaVersion}");
        var javaExe = OperatingSystem.IsWindows() ? "java.exe" : "java";
        var javaPath = Path.Combine(javaDir, "bin", javaExe);

        if (File.Exists(javaPath))
            return javaPath;

        ToggleBusyState(true, $"Downloading Java {requiredJavaVersion}...");
        Directory.CreateDirectory(javaDir);

        string os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "mac" : "linux";
        string arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "aarch64",
            System.Runtime.InteropServices.Architecture.X86 => "x32",
            _ => "x64"
        };
        
        var apiUrl = $"https://api.adoptium.net/v3/binary/latest/{requiredJavaVersion}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";
        var tempArchive = Path.Combine(Path.GetTempPath(), $"java-{requiredJavaVersion}-jre.{(os == "windows" ? "zip" : "tar.gz")}");

        using (var httpClient = new System.Net.Http.HttpClient())
        {
            var response = await httpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download JRE for Java {requiredJavaVersion}");

            using var fs = new FileStream(tempArchive, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);
        }

        ToggleBusyState(true, $"Extracting Java {requiredJavaVersion}...");
        if (os == "windows")
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(tempArchive, javaDir, true);
            var foundExe = Directory.GetFiles(javaDir, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (foundExe != null) return foundExe;
        }
        else
        {
            using var extractProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{tempArchive}\" -C \"{javaDir}\" --strip-components=1",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (extractProcess != null) await extractProcess.WaitForExitAsync(cancellationToken);
            
            var foundExe = Directory.GetFiles(javaDir, "java", SearchOption.AllDirectories).FirstOrDefault();
            if (foundExe != null)
            {
                System.Diagnostics.Process.Start("chmod", $"+x \"{foundExe}\"")?.WaitForExit();
                return foundExe;
            }
        }

        throw new Exception($"Java {requiredJavaVersion} executable not found.");
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeathClient-Updater/1.0");
            var currentVersion = new Version(1, 0, 0); 
            
            var response = await client.GetStringAsync("https://api.github.com/repos/AchinthyaJ/DeathClient/releases/latest");
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                var tag = tagElement.GetString();
                if (!string.IsNullOrEmpty(tag) && tag.StartsWith("v"))
                {
                    if (Version.TryParse(tag.Substring(1), out var latestVersion))
                    {
                        if (latestVersion > currentVersion)
                        {
                            Dispatcher.UIThread.Post(async () =>
                            {
                                var download = await DialogService.ShowConfirmAsync(this, "Update Available", $"A new version ({tag}) is available. Would you like to download it?");
                                if (download && doc.RootElement.TryGetProperty("html_url", out var urlElement))
                                {
                                    var url = urlElement.GetString();
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = url,
                                            UseShellExecute = true
                                        });
                                    }
                                }
                            });
                        }
                    }
                }
            }
        }
        catch { }
    }

    private System.Threading.CancellationTokenSource? _skinCancellation;

    public async void UsernameInput_TextChanged()
    {
        var selectedAccount = GetSelectedAccount();
        var username = GetActiveUsername();

        if (string.IsNullOrWhiteSpace(username))
        {
            _playerUuid = string.Empty;
            characterImage.Source = null;
            btnStart.IsEnabled = false;
            return;
        }

        btnStart.IsEnabled = true;
        
        _playerUuid = !string.IsNullOrWhiteSpace(selectedAccount?.Uuid)
            ? selectedAccount!.Uuid
            : Character.GenerateUuidFromUsername(username);
        
        _skinCancellation?.Cancel();
        _skinCancellation = new System.Threading.CancellationTokenSource();
        var token = _skinCancellation.Token;

        UpdateCharacterPreview();

        try
        {
            await Task.Delay(1000, token);
            await FetchAndSetSkinAsync(username, token);
        }
        catch (TaskCanceledException) { }
    }

    private async Task FetchAndSetSkinAsync(string username, CancellationToken token)
    {
        var uuid = GetSelectedAccount()?.Uuid;
        if (string.IsNullOrWhiteSpace(uuid))
            uuid = Character.GenerateUuidFromUsername(username);
        var url = $"https://crafatar.com/skins/{uuid}";
        
        var skinsDir = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "skins");
        Directory.CreateDirectory(skinsDir);
        var skinPath = Path.Combine(skinsDir, $"{username}.png");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var bytes = await client.GetByteArrayAsync(url, token);
            await File.WriteAllBytesAsync(skinPath, bytes, token);
            _settings.CustomSkinPath = skinPath;
            _settingsStore.Save(_settings);
        }
        catch
        {
            _settings.CustomSkinPath = string.Empty;
            _settingsStore.Save(_settings);
            if (File.Exists(skinPath))
                File.Delete(skinPath);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (GetActiveUsername() == username)
            {
                UpdateCharacterPreview();
            }
        });
    }

    public void CbVersion_SelectionChanged()
    {
        UpdateCharacterPreview();
        if (_selectedProfile is null)
            SyncModrinthFilters();
    }

    private void UpdateCharacterPreview()
    {
        // Removed SkinShuffle Sync
        
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
                    // Head (base layer: 8,8 size 8x8)
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
            var searchFolders = new[] 
            {
                Path.Combine(AppContext.BaseDirectory, "Resources"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources")
            };

            foreach (var folder in searchFolders)
            {
                var p = Path.Combine(folder, $"{resourceName}.png");
                if (File.Exists(p))
                {
                    imagePath = p;
                    break;
                }
            }
        }

        if (imagePath != null && File.Exists(imagePath))
        {
            try {
                characterImage.Source = new Bitmap(imagePath);
            } catch { characterImage.Source = null; }
        }
        else
        {
            characterImage.Source = null;
        }
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

    public void ProfileListBox_SelectionChanged()
    {
        _selectedProfile = profileListBox.SelectedItem as LauncherProfile;
        if (_selectedProfile is not null)
            profileNameInput.Text = _selectedProfile.Name;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
        RefreshModsList();
        UpdateSelectedProjectDetails();
        RefreshSearchList();
    }

    private void RefreshModsList()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _modItems.Clear();
            if (_selectedProfile == null) return;
            var modsDir = _selectedProfile.ModsDirectory;
            if (!Directory.Exists(modsDir)) return;

            try
            {
                var files = Directory.GetFiles(modsDir);
                int count = 0;
                foreach (var file in files)
                {
                    if (!file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && 
                        !file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var item = new ModItem
                    {
                        FileName = Path.GetFileName(file),
                        FileSize = new FileInfo(file).Length / 1024 + " KB",
                        FullPath = file
                    };
                    // CRITICAL: Initialize the state based on extension, otherwise it defaults to Disabled
                    item.InitState(!file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase));
                    
                    _modItems.Add(item);
                    count++;
                }
                LauncherLog.Info($"[ModsList] Loaded {count} mods for {_selectedProfile.Name}.");
            }
            catch (Exception ex)
            {
                LauncherLog.Error($"[ModsList] Refresh failed for {_selectedProfile.Name}", ex);
            }
        });
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

    private void OpenProfileEditor(LauncherProfile profile)
    {
        _selectedProfile = profile;
        profileListBox.SelectedItem = profile;
        profileNameInput.Text = profile.Name;
        profileGameDirInput.Text = profile.GameDirectoryOverride ?? string.Empty;

        var selectedIndex = Array.FindIndex(ProfileLoaderOptions, option =>
            string.Equals(option, profile.Loader, StringComparison.OrdinalIgnoreCase));
        profileLoaderCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

        createProfileButton.IsVisible = false;
        renameProfileButton.IsVisible = true;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
        RefreshModsList();
        _instanceEditorOverlay.IsVisible = true;
    }

    private void UpdateLauncherContext()
    {
        if (_selectedProfile is null)
        {
            activeProfileBadge.Text = "HOME";
            activeContextLabel.Text = string.Empty;
            installModeLabel.Text = "Default";
            SetButtonText(btnStart, "▶ Play");
            profileInspectorTitle.Text = "Standard Profile";
            profileInspectorMeta.Text = "No isolated profile is active. Mods install only after you create or select a profile.";
            profileInspectorPath.Text = $"Instances root: {_profileStore.GetInstancesRoot()}";
            clearProfileButton.IsEnabled = false;
            renameProfileButton.IsEnabled = false;
            heroInstanceLabel.Text = "Standard Play";
            heroPerformanceLabel.Text = $"{cbVersion.SelectedItem?.ToString() ?? "1.21.1"} • Ready";
            var ramGbInit = _settings.MaxRamMb / 1024.0;
            var expectedFpsInit = Math.Round(ramGbInit * 41.25).ToString();
            var expectedRamInit = $"{Math.Round(ramGbInit, 1)} GB";
            homeFpsStatValue.Text = expectedFpsInit;
            homeRamStatValue.Text = expectedRamInit;
            performanceFpsStatValue.Text = expectedFpsInit;
            performanceRamStatValue.Text = expectedRamInit;
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
        var ramGb = _settings.MaxRamMb / 1024.0;
        var fpsText = Math.Round(ramGb * (_selectedProfile.Loader == "vanilla" ? 41.25 : 30)).ToString();
        var ramText = $"{Math.Round(ramGb, 1)} GB";
        homeFpsStatValue.Text = fpsText;
        homeRamStatValue.Text = ramText;
        performanceFpsStatValue.Text = fpsText;
        performanceRamStatValue.Text = ramText;

        _settings.LastSelectedProfilePath = _selectedProfile.InstanceDirectory;
        _settingsStore.Save(_settings);
    }

    private void SyncModrinthFilters()
    {
        var rawVersion = _selectedProfile?.GameVersion ?? cbVersion.SelectedItem?.ToString() ?? string.Empty;
        // Basic cleanup: if they have "1.21.11" it might be a typo for "1.21.1" or they mean something else
        modrinthVersionInput.Text = rawVersion;
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

        if (instanceVersionCombo.SelectedItem is null)
        {
            await DialogService.ShowInfoAsync(this, "Version required", "Select a Minecraft version before creating a profile.");
            return;
        }

        var selectedVersion = instanceVersionCombo.SelectedItem!.ToString()!;
        var loader = profileLoaderCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "vanilla";
        string? loaderVersion = null;

        try
        {
            ToggleBusyState(true, "Creating profile...");

            if (loader == "fabric")
                loaderVersion = await ResolveLatestFabricVersionAsync(selectedVersion, CancellationToken.None);
            else if (loader == "quilt")
                loaderVersion = await ResolveLatestQuiltVersionAsync(selectedVersion, CancellationToken.None);
            else if (loader == "forge")
                loaderVersion = await ResolveLatestForgeVersionAsync(selectedVersion, CancellationToken.None);
            else if (loader == "neoforge")
                loaderVersion = await ResolveLatestNeoForgeVersionAsync(selectedVersion, CancellationToken.None);

            var profile = _profileStore.CreateProfile(profileNameInput.Text.Trim(), selectedVersion, loader, loaderVersion, null, profileGameDirInput.Text?.Trim());
            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);
            else if (loader == "quilt")
                await EnsureQuiltProfileAsync(profile, CancellationToken.None);
            else if (loader == "forge" || loader == "neoforge")
                await EnsureForgeProfileAsync(profile, CancellationToken.None);

            // Ensure the required mods are installed automatically immediately
            var modsDir = Path.Combine(profile.InstanceDirectory, "mods");
            Directory.CreateDirectory(modsDir);
            await InstallModIfMissingAsync("customskinloader", profile, modsDir, CancellationToken.None);
            if (_settings.EnableFancyMenu && SupportsFancyMenu(profile))
            {
                await InstallModIfMissingAsync("fancymenu", profile, modsDir, CancellationToken.None);
                await InstallModIfMissingAsync("konkrete", profile, modsDir, CancellationToken.None);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles(profile);
                UpdateSelectedProjectDetails();
                profileNameInput.Text = string.Empty;
                _instanceEditorOverlay.IsVisible = false;
                SetProgressState($"Profile {profile.Name} is ready.", 0, 0);
            });
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
            else if (loader == "quilt")
                loaderVersion = await ResolveLatestQuiltVersionAsync(version, CancellationToken.None);
            else if (loader == "forge")
                loaderVersion = await ResolveLatestForgeVersionAsync(version, CancellationToken.None);
            else if (loader == "neoforge")
                loaderVersion = await ResolveLatestNeoForgeVersionAsync(version, CancellationToken.None);

            var profile = _profileStore.CreateProfile(autoName, version, loader, loaderVersion);

            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);
            else if (loader == "quilt")
                await EnsureQuiltProfileAsync(profile, CancellationToken.None);
            else if (loader == "forge" || loader == "neoforge")
                await EnsureForgeProfileAsync(profile, CancellationToken.None);

            // Ensure the required mods are installed automatically immediately
            var modsDir = Path.Combine(profile.InstanceDirectory, "mods");
            Directory.CreateDirectory(modsDir);
            await InstallModIfMissingAsync("customskinloader", profile, modsDir, CancellationToken.None);
            if (_settings.EnableFancyMenu && SupportsFancyMenu(profile))
            {
                await InstallModIfMissingAsync("fancymenu", profile, modsDir, CancellationToken.None);
                await InstallModIfMissingAsync("konkrete", profile, modsDir, CancellationToken.None);
            }

            // Pre-download the game files
            var launcherPath = new MinecraftPath(profile.InstanceDirectory);
            var launcher = CreateLauncher(launcherPath);
            await launcher.InstallAsync(version);

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles(profile);
                UpdateSelectedProjectDetails();
                SetProgressState($"Instance \"{autoName}\" ready to play!", 0, 0);
            });
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
            await InstallSelectedModAsync(project, CancellationToken.None, null); // We don't have a specific button here easily accessible, button is usually in the search results
            RefreshModsList();
            UpdateSelectedProjectDetails();
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

    private async Task<string> ResolveLatestQuiltVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        var payload = await _modrinthClient.GetStringAsync($"https://meta.quiltmc.org/v3/versions/loader/{gameVersion}", cancellationToken);
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
        throw new InvalidOperationException($"No Quilt loader build was found for Minecraft {gameVersion}.");
    }

    private async Task<string> ResolveLatestForgeVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        try 
        {
            var payload = await _modrinthClient.GetStringAsync($"https://bmclapi2.bangbang93.com/forge/minecraft/{gameVersion}", cancellationToken);
            using var json = JsonDocument.Parse(payload);
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("version", out var versionElement))
                {
                    var version = versionElement.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
            }
        } 
        catch { }
        throw new InvalidOperationException($"No Forge version could be auto-resolved for {gameVersion}.");
    }

    private async Task<string> ResolveLatestNeoForgeVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        try 
        {
            var payload = await _modrinthClient.GetStringAsync($"https://bmclapi2.bangbang93.com/neoforge/list/{gameVersion}", cancellationToken);
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() > 0)
            {
                var first = json.RootElement[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    var version = first.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
                else if (first.TryGetProperty("version", out var verElement))
                {
                    var version = verElement.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
            }
        } 
        catch { }
        throw new InvalidOperationException($"No NeoForge version could be auto-resolved for {gameVersion}.");
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
            // Re-bind ItemsSource in case AXAML re-created the controls
            modrinthResultsListBox.ItemsSource = _searchResults;
            _quickModResults.ItemsSource = _quickSearchResults;

            ToggleBusyState(true, "Searching across platforms...");

            var projectType = modrinthProjectTypeCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "mod";
            var gameVersion = string.IsNullOrWhiteSpace(modrinthVersionInput.Text) ? null : modrinthVersionInput.Text.Trim();
            var loader = NormalizeLoaderFilter();
            var source = modrinthSourceCombo.SelectedItem?.ToString() ?? "Modrinth";
            
            Task<IReadOnlyList<ModrinthProject>>? modrinthTask = null;
            Task<IReadOnlyList<ModrinthProject>>? curseForgeTask = null;

            if (source == "Modrinth")
                modrinthTask = _modrinthClient.SearchProjectsAsync(modrinthSearchInput.Text ?? "", projectType, gameVersion, loader, _searchCancellation.Token);
            else if (source == "CurseForge")
            {
                if (projectType == "mod")
                    curseForgeTask = _curseForgeClient.SearchModsAsync(modrinthSearchInput.Text ?? "", gameVersion, loader, _searchCancellation.Token);
                else if (projectType == "modpack")
                    curseForgeTask = _curseForgeClient.SearchPacksAsync(modrinthSearchInput.Text ?? "", gameVersion, _searchCancellation.Token);
            }

            var mrResults = modrinthTask != null ? await modrinthTask : [];
            var cfResults = curseForgeTask != null ? await curseForgeTask : [];

            var results = new List<ModrinthProject>(mrResults.Count + cfResults.Count);
            results.AddRange(mrResults);
            results.AddRange(cfResults);

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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                modrinthDetailsBox.Text = "No matching projects found. Check your filters (e.g. Version/Loader).";
                installSelectedButton.IsEnabled = false;
            }
        });
    }

    private Control BuildLayoutDeck()
    {
        var title = CreateSectionTitle("Client Layout", "Import a layout file to customize your launcher. Only the properties you specify in the file will be changed.");
        var style = _settings.Style;

        // Current style summary
        var styleInfo = new TextBlock
        {
            Text = $"Current: {style.BorderStyle} (radius {style.CornerRadius}px), nav={style.NavPosition}, sidebar={style.SidebarSide}{(style.SidebarCollapsed ? " [collapsed]" : "")}{(style.CompactMode ? ", compact" : "")}",
            Foreground = new SolidColorBrush(Color.Parse("#7A8AAA")),
            FontSize = 12,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Layout file import/reset
        var layoutSection = CreateSubCard("Layout File", new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Import an AXAML layout file to customize the launcher style. " +
                           "Only the properties you specify in the file (like window_shape=\"square\") are applied \u2014 everything else stays default.",
                    Foreground = new SolidColorBrush(Color.Parse("#B0BACF")),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                },
                styleInfo,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        CreatePrimaryButton("Import Layout File", "#050505", Color.FromArgb(160, 120, 120, 120)).With(btn => {
                            btn.Click += async (_, _) => await ImportLayoutAsync();
                            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 110, 91, 255));
                        }),
                        CreateSecondaryButton("Reset To Default").With(btn => {
                            btn.Click += async (_, _) => await ResetLayoutAsync();
                        })
                    }
                }
            }
        }, "#1A2035");

        // Simple sidebar/nav toggles (quick access, no file needed)
        var sidebarToggle = new ToggleSwitch
        {
            Content = "Sidebar Position",
            OnContent = "Right",
            OffContent = "Left",
            IsChecked = IsSidebarOnRight(),
            Foreground = Brushes.White
        };
        sidebarToggle.IsCheckedChanged += (_, _) => {
            _settings.Style.SidebarSide = sidebarToggle.IsChecked == true ? "right" : "left";
            _settingsStore.Save(_settings);
            RebuildUiFromLayoutState(_activeSection);
        };

        var topNavToggle = new ToggleSwitch
        {
            Content = "Navigation Placement",
            OnContent = "Top",
            OffContent = "Sidebar",
            IsChecked = IsTopNavigationEnabled(),
            Foreground = Brushes.White
        };
        topNavToggle.IsCheckedChanged += (_, _) => {
            _settings.Style.NavPosition = topNavToggle.IsChecked == true ? "top" : "sidebar";
            if (topNavToggle.IsChecked == true) _settings.Style.SidebarCollapsed = false;
            _settingsStore.Save(_settings);
            RebuildUiFromLayoutState(_activeSection);
        };

        var collapseSidebarToggle = new ToggleSwitch
        {
            Content = "Sidebar Density",
            OnContent = "Collapsed",
            OffContent = "Expanded",
            IsChecked = IsSidebarCollapsed(),
            IsEnabled = !IsTopNavigationEnabled(),
            Foreground = Brushes.White
        };
        collapseSidebarToggle.IsCheckedChanged += (_, _) => {
            _settings.Style.SidebarCollapsed = collapseSidebarToggle.IsChecked == true;
            _settingsStore.Save(_settings);
            RebuildUiFromLayoutState(_activeSection);
        };

        var quickToggles = CreateSubCard("Quick Toggles", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                sidebarToggle,
                topNavToggle,
                collapseSidebarToggle
            }
        }, "#1A2035");

        // Accent colors
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
                }
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
            Children = { title, layoutSection, quickToggles, colorSection, backgroundSection, orderSection, fmSection }
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
            
            var upBtn = new Button { Content = "↑", Width = 32, Height = 32, Margin = new Thickness(4,0), Padding = new Thickness(0), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
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
            
            var downBtn = new Button { Content = "↓", Width = 32, Height = 32, Padding = new Thickness(0), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
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
            InvalidateUiCache();
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

        bool isInstalled = _selectedProfile?.InstalledModIds.Contains(project.ProjectId) ?? false;
        installSelectedButton.IsEnabled = !isInstalled;
        if (isInstalled)
        {
            SetButtonText(installSelectedButton, "Installed");
        }
        else
        {
            SetButtonText(installSelectedButton, project.ProjectType == "modpack" ? "↓ Pack" : "↓ Mod");
        }
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

    private void RefreshSearchList()
    {
        var items = modrinthResultsListBox.ItemsSource as IEnumerable<ModrinthProject>;
        if (items != null)
        {
            var list = items.ToList();
            modrinthResultsListBox.ItemsSource = null;
            modrinthResultsListBox.ItemsSource = list;
        }
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
                await InstallSelectedModAsync(project, CancellationToken.None, installSelectedButton);

            RefreshModsList();
            UpdateSelectedProjectDetails();
            SetButtonProgress(installSelectedButton, 0, false);
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

    private async Task InstallSelectedModAsync(ModrinthProject project, CancellationToken cancellationToken, Button? targetButton = null)
    {
        if (_selectedProfile is null)
        {
            await DialogService.ShowInfoAsync(this, "Profile required", "Create or select a profile before installing mods.");
            return;
        }

        if (project.IsCurseForge)
        {
            await InstallCurseForgeModAsync(project, cancellationToken, targetButton);
            return;
        }

        var versions = await _modrinthClient.GetProjectVersionsAsync(project.ProjectId, _selectedProfile.GameVersion, _selectedProfile.Loader, cancellationToken);
        var version = versions.FirstOrDefault(HasPrimaryFile) ?? versions.FirstOrDefault();
        if (version is null)
            throw new InvalidOperationException($"No compatible version was found for {_selectedProfile.LoaderDisplay}.");

        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { project.ProjectId };
        await InstallModVersionAsync(_selectedProfile, version, installed, cancellationToken, targetButton, project.ProjectId);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            SetProgressState($"Installed {project.Title} into {_selectedProfile.Name}.", 0, 0);
            RefreshSearchList();
        });
    }

    private async Task InstallCurseForgeModAsync(ModrinthProject project, CancellationToken cancellationToken, Button? targetButton = null)
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

        await _curseForgeClient.DownloadFileAsync(file.DownloadUrl, dest, CreateDownloadProgress(file.FileName, targetButton), cancellationToken);
        
        _selectedProfile.InstalledModIds.Add(project.ProjectId);
        _profileStore.Save(_selectedProfile);
        
        SetProgressState($"Installed {project.Title} (CurseForge) into {_selectedProfile.Name}.", 0, 0);
    }

    private static bool HasPrimaryFile(ModrinthProjectVersion version) =>
        version.Files.Any(file => file.Primary && file.Filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private async Task InstallModVersionAsync(LauncherProfile profile, ModrinthProjectVersion version, HashSet<string> installedProjectIds, CancellationToken cancellationToken, Button? targetButton = null, string? projectId = null)
    {
        foreach (var dependency in version.Dependencies.Where(d => d.DependencyType == "required" && !string.IsNullOrWhiteSpace(d.ProjectId)))
        {
            if (!installedProjectIds.Add(dependency.ProjectId!))
                continue;

            var dependencyVersions = await _modrinthClient.GetProjectVersionsAsync(dependency.ProjectId!, profile.GameVersion, profile.Loader, cancellationToken);
            var dependencyVersion = dependencyVersions.FirstOrDefault(HasPrimaryFile) ?? dependencyVersions.FirstOrDefault();
            if (dependencyVersion is not null)
                await InstallModVersionAsync(profile, dependencyVersion, installedProjectIds, cancellationToken, targetButton, dependency.ProjectId);
        }

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException($"Version {version.VersionNumber} did not include a downloadable file.");

        Directory.CreateDirectory(profile.ModsDirectory);
        var destinationPath = Path.Combine(profile.ModsDirectory, file.Filename);
        await _modrinthClient.DownloadFileAsync(file.Url, CreateDownloadDestination(destinationPath), CreateDownloadProgress(file.Filename, targetButton), cancellationToken);
        await VerifyFileHashAsync(destinationPath, file.Hashes);
        
        var pid = projectId ?? version.ProjectId;
        if (!string.IsNullOrEmpty(pid))
            profile.InstalledModIds.Add(pid);
            
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
        else if (loader == "quilt")
            await EnsureQuiltProfileAsync(profile, cancellationToken);
        else if (loader == "forge" || loader == "neoforge")
            await EnsureForgeProfileAsync(profile, cancellationToken);

        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            RefreshProfiles(profile);
            SetActiveSection("profiles");
            SetProgressState($"Installed modpack {profile.Name}.", 0, 0);
        });
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

    private Progress<(long BytesRead, long? TotalBytes)> CreateDownloadProgress(string fileName, Button? targetButton = null)
    {
        return new Progress<(long BytesRead, long? TotalBytes)>(progress =>
        {
            statusLabel.Text = $"Downloading {Path.GetFileName(fileName)}";
            double percent = 0;
            if (progress.TotalBytes is long totalBytes && totalBytes > 0)
            {
                percent = progress.BytesRead * 100d / totalBytes;
                pbProgress.Value = Math.Min(100, percent);
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} / {FormatBytes(totalBytes)}";
            }
            else
            {
                pbProgress.Value = 0;
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} downloaded";
            }

            if (targetButton != null)
            {
                SetButtonProgress(targetButton, percent > 0 ? percent : 0, true);
            }
        });
    }

    private void ToggleBusyState(bool isBusy, string statusText)
    {
        btnStart.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(usernameInput.Text);
        if (isBusy)
        {
            btnStart.Content = "Cancel"; // Default busy state for launch
        }
        else
        {
            btnStart.Content = "▶ Play";
        }
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
        if (_homeStatusBar != null) _homeStatusBar.IsVisible = isBusy;
        if (!isBusy)
        {
            pbProgress.Value = 0;
            if (installSelectedButton != null) SetButtonProgress(installSelectedButton, 0, false);
            if (btnStart != null) SetButtonProgress(btnStart, 0, false);
            if (modrinthSearchButton != null) SetButtonProgress(modrinthSearchButton, 0, false);
        }
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

    private Border CreateCompactStat(string title, TextBlock valueBlock)
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
                                DetachFromParent(heroInstanceLabel)!,
                                DetachFromParent(heroPerformanceLabel)!,
                                new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
                                    CornerRadius = new CornerRadius(14),
                                    Padding = new Thickness(14, 10),
                                    Child = DetachFromParent(usernameInput)!
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

    // Removed static keyword to access _settings
    private TextBox CreateTextBox()
    {
        var style = _settings.Style;
        var inBg = !string.IsNullOrWhiteSpace(style.FieldBackground) ? style.FieldBackground : "#78131B2D";
        var inFg = !string.IsNullOrWhiteSpace(style.FieldForeground) ? style.FieldForeground : "#FFFFFF";
        var inBorder = !string.IsNullOrWhiteSpace(style.FieldBorderColor) ? style.FieldBorderColor : "#36476A";
        var inCr = double.IsNaN(style.FieldRadius) ? 16 : style.FieldRadius;

        return new TextBox
        {
            Background = new SolidColorBrush(Color.Parse(inBg)),
            Foreground = new SolidColorBrush(Color.Parse(inFg)),
            BorderBrush = new SolidColorBrush(Color.Parse(inBorder)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 11),
            CornerRadius = new CornerRadius(inCr),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
    }

    private ComboBox CreateComboBox(IEnumerable<object> items)
    {
        var style = _settings.Style;
        var inBg = !string.IsNullOrWhiteSpace(style.FieldBackground) ? style.FieldBackground : "#78131B2D";
        var inFg = !string.IsNullOrWhiteSpace(style.FieldForeground) ? style.FieldForeground : "#FFFFFF";
        var inBorder = !string.IsNullOrWhiteSpace(style.FieldBorderColor) ? style.FieldBorderColor : "#36476A";
        var inCr = double.IsNaN(style.FieldRadius) ? 16 : style.FieldRadius;

        var comboBox = new ComboBox
        {
            ItemsSource = items.ToList(),
            Background = new SolidColorBrush(Color.Parse(inBg)),
            Foreground = new SolidColorBrush(Color.Parse(inFg)),
            BorderBrush = new SolidColorBrush(Color.Parse(inBorder)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(inCr),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(comboBox);
        return comboBox;
    }

    private ComboBox CreateComboBox(IEnumerable<string> items)
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

    private Button CreatePrimaryButton(string text, string hexColor, Color foreground)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var progressBar = new ProgressBar
        {
            Height = 4,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 2),
            IsVisible = false,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            CornerRadius = new CornerRadius(2)
        };

        var contentGrid = new Grid
        {
            Children = { textBlock, progressBar }
        };

        var button = new Button
        {
            Content = contentGrid,
            Tag = progressBar, // Store progress bar for easy access
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

    private static void SetButtonText(Button button, string text)
    {
        if (button.Content is Grid grid)
        {
            var textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock != null)
            {
                textBlock.Text = text;
                return;
            }
        }
        button.Content = text;
    }

    private static void SetButtonProgress(Button button, double value, bool visible)
    {
        if (button.Tag is ProgressBar pb)
        {
            pb.Value = value;
            pb.IsVisible = visible;
        }
    }

    private Button CreateNavButton(string icon, string label, bool compact = false)
    {
        var style = _settings.Style;
        var buttonHeight = double.IsNaN(style.NavButtonHeight) ? (compact ? 48 : 46) : style.NavButtonHeight;
        var buttonFontSize = double.IsNaN(style.NavButtonFontSize) ? 14 : style.NavButtonFontSize;
        var hAlign = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        
        var iconSize = double.IsNaN(style.NavButtonFontSize) ? (compact ? 18 : 15) : style.NavButtonFontSize + 3;

        var button = new Button
        {
            Content = compact
                ? (object)new TextBlock
                {
                    Text = icon,
                    FontSize = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
                : new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = icon,
                            FontSize = iconSize,
                            Width = 22,
                            TextAlignment = TextAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = label,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = buttonFontSize,
                            FontWeight = FontWeight.SemiBold
                        }
                    }
                },
            Width = compact ? 48 : double.NaN,
            Height = buttonHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = !string.IsNullOrWhiteSpace(style.NavButtonBackground) ? new SolidColorBrush(Color.Parse(style.NavButtonBackground)) : Brushes.Transparent,
            Foreground = !string.IsNullOrWhiteSpace(style.NavButtonForeground) ? new SolidColorBrush(Color.Parse(style.NavButtonForeground)) : new SolidColorBrush(Color.Parse("#A4A8B1")),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(style.NavButtonCornerRadius),
            FontWeight = FontWeight.SemiBold,
            FontSize = buttonFontSize,
            HorizontalContentAlignment = hAlign,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = compact ? new Thickness(0) : new Thickness(16, 0),
            FontFamily = new FontFamily("Inter, Segoe UI")
        };
        ApplyHoverMotion(button);
        return button;
    }

    private Button CreateSecondaryButton(string text)
    {
        var style = _settings.Style;
        var btnHeight = double.IsNaN(style.ButtonHeight) ? 48 : style.ButtonHeight;
        var btnFs = double.IsNaN(style.ButtonFontSize) ? 14 : style.ButtonFontSize;
        var btnCr = double.IsNaN(style.ButtonCornerRadius) ? 18 : style.ButtonCornerRadius;
        var btnPad = double.IsNaN(style.ButtonPadding) ? 18 : style.ButtonPadding;
        
        var bg = !string.IsNullOrWhiteSpace(style.ButtonBackground) ? style.ButtonBackground : "#55101728";
        var fg = !string.IsNullOrWhiteSpace(style.ButtonForeground) ? style.ButtonForeground : "#FFFFFF";

        var button = new Button
        {
            Content = text,
            Height = btnHeight,
            Background = new SolidColorBrush(Color.Parse(bg)),
            Foreground = new SolidColorBrush(Color.Parse(fg)),
            BorderBrush = new SolidColorBrush(Color.Parse("#3C4F73")),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(btnPad, 12),
            CornerRadius = new CornerRadius(btnCr),
            FontFamily = new FontFamily("Inter, Segoe UI"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ApplyHoverMotion(button);
        return button;
    }

    private Button CreateCompactSecondaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Height = 30,
            MinWidth = 110,
            Background = new SolidColorBrush(Color.FromArgb(85, 16, 23, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3C4F73")),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(12),
            FontFamily = new FontFamily("Inter, Segoe UI"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ApplyHoverMotion(button);
        return button;
    }

    private Border BuildCard(Control child)
    {
        var style = _settings.Style;
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(style.CardBackground ?? "#0D1522")),
            BorderBrush = new SolidColorBrush(Color.Parse(style.CardBorderColor ?? "#203046")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(double.IsNaN(style.CardCornerRadius) ? 24 : style.CardCornerRadius),
            Padding = new Thickness(double.IsNaN(style.CardPadding) ? 22 : style.CardPadding),
            Child = child
        };
    }

    private Border CreateGlassPanel(Control child, Thickness? padding = null, Thickness? margin = null)
    {
        var style = _settings.Style;
        var panel = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(20, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(5, 255, 255, 255), 1)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(double.IsNaN(style.CardCornerRadius) ? 24 : style.CardCornerRadius),
            Padding = padding ?? new Thickness(22),
            Margin = margin ?? new Thickness(0),
            Child = child
        };
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

    private Control CreateSectionTitle(string text, string subtitle)
    {
        var style = _settings.Style;
        
        var titleText = !string.IsNullOrWhiteSpace(style.TitleText) && text == "Home" ? style.TitleText : text;
        var titleFs = double.IsNaN(style.TitleFontSize) ? 32 : style.TitleFontSize;
        var titleFg = !string.IsNullOrWhiteSpace(style.TitleForeground) ? style.TitleForeground : "#FFFFFF";
        var primaryFont = !string.IsNullOrWhiteSpace(style.PrimaryFontFamily) ? new FontFamily(style.PrimaryFontFamily) : new FontFamily("Inter, Segoe UI");
        var secondaryFg = !string.IsNullOrWhiteSpace(style.SecondaryForeground) ? style.SecondaryForeground : "#A4B4DA";

        return new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 0, 20),
            Children =
            {
                new TextBlock
                {
                    Text = titleText,
                    FontSize = titleFs,
                    FontWeight = FontWeight.Black,
                    Foreground = new SolidColorBrush(Color.Parse(titleFg)),
                    LetterSpacing = 1.2,
                    FontFamily = primaryFont
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = new SolidColorBrush(Color.Parse(secondaryFg)),
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = primaryFont
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
            Padding = new Thickness(0, 0, 16, 0),
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

    private Border CreateMetricTile(string title, string subtitle)
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

    private Border CreateSubCard(string title, Control body, string backgroundHex)
    {
        var style = _settings.Style;
        var bg = !string.IsNullOrWhiteSpace(style.CardBackground) ? style.CardBackground : backgroundHex;
        var border = !string.IsNullOrWhiteSpace(style.CardBorderColor) ? style.CardBorderColor : "#21364F";
        var cr = double.IsNaN(style.CardCornerRadius) ? 20 : style.CardCornerRadius;
        var pad = double.IsNaN(style.CardPadding) ? 18 : style.CardPadding;

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(bg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cr),
            Padding = new Thickness(pad),
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

    private void ApplyNavState(Button? button, bool isActive)
    {
        if (button == null) return;
        if (button == accountsNavButton) return;

        var style = _settings.Style;
        var accentColor = Color.Parse(_settings.AccentColor);

        var activeBgToken = !string.IsNullOrWhiteSpace(style.NavButtonActiveBackground) ? style.NavButtonActiveBackground : null;
        var inactiveBgToken = !string.IsNullOrWhiteSpace(style.NavButtonBackground) ? style.NavButtonBackground : null;

        var activeFgToken = !string.IsNullOrWhiteSpace(style.NavButtonActiveForeground) ? style.NavButtonActiveForeground : null;
        var inactiveFgToken = !string.IsNullOrWhiteSpace(style.NavButtonForeground) ? style.NavButtonForeground : "#A4A8B1";

        if (isActive)
        {
            button.BorderThickness = new Thickness(0);

            switch (style.NavIndicatorStyle?.ToLower())
            {
                case "left-pill":
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : Brushes.Transparent;
                    button.BorderThickness = new Thickness(4, 0, 0, 0);
                    button.BorderBrush = new SolidColorBrush(accentColor);
                    break;
                case "underline":
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : Brushes.Transparent;
                    button.BorderThickness = new Thickness(0, 0, 0, 2);
                    button.BorderBrush = new SolidColorBrush(accentColor);
                    break;
                case "glow":
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : Brushes.Transparent;
                    button.Foreground = new SolidColorBrush(accentColor);
                    break;
                case "fill":
                default:
                    button.Background = activeBgToken != null ? new SolidColorBrush(Color.Parse(activeBgToken)) : new SolidColorBrush(Color.FromArgb(32, accentColor.R, accentColor.G, accentColor.B));
                    button.Foreground = activeFgToken != null ? new SolidColorBrush(Color.Parse(activeFgToken)) : new SolidColorBrush(accentColor);
                    break;
            }
            if (activeFgToken != null) button.Foreground = new SolidColorBrush(Color.Parse(activeFgToken));
        }
        else
        {
            button.Background = inactiveBgToken != null ? new SolidColorBrush(Color.Parse(inactiveBgToken)) : Brushes.Transparent;
            button.Foreground = new SolidColorBrush(Color.Parse(inactiveFgToken));
            button.BorderThickness = new Thickness(0);
            button.BorderBrush = Brushes.Transparent;
        }

        button.CornerRadius = new CornerRadius(double.IsNaN(style.NavButtonCornerRadius) ? 14 : style.NavButtonCornerRadius);
        button.Padding = new Thickness(16, 0);
        button.FontSize = double.IsNaN(style.NavButtonFontSize) ? 14 : style.NavButtonFontSize;
        button.FontWeight = isActive ? FontWeight.Bold : FontWeight.Normal;

    }

    private Border CreateStatTile(string title, TextBlock valueBlock, string subtitle)
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

    private async Task InstallModIfMissingAsync(string slug, LauncherProfile profile, string modsDir, CancellationToken cancellationToken, string? projectId = null)
    {
        try
        {
            if (string.Equals(profile.Loader, "vanilla", StringComparison.OrdinalIgnoreCase))
                return;

            string targetId = projectId ?? slug;
            if (profile.InstalledModIds.Contains(targetId))
            {
                LauncherLog.Info($"[ModInstaller] {targetId} is already tracked. Done.");
                return;
            }

            // We search first to get the official Project ID if not provided.
            LauncherLog.Info($"[ModInstaller] Resolving official ID for {slug} ({profile.GameVersion}/{profile.Loader})...");
            var results = await _modrinthClient.SearchProjectsAsync(targetId, "mod", profile.GameVersion, profile.Loader, cancellationToken);
            var project = results.FirstOrDefault(p => 
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(p.ProjectId, slug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProjectId, projectId, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(slug, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                LauncherLog.Info($"[ModInstaller] Could not find {slug} on Modrinth. Skipping auto-install.");
                return;
            }

            if (profile.InstalledModIds.Contains(project.ProjectId))
            {
                LauncherLog.Info($"[ModInstaller] {project.Title} ({project.ProjectId}) is already tracked. Done.");
                return;
            }

            // Check if the file already exists physically but isn't tracked yet
            var existing = Directory.EnumerateFiles(modsDir, "*.jar")
                .Any(f => Path.GetFileName(f).Contains(slug, StringComparison.OrdinalIgnoreCase));

            if (existing)
            {
                LauncherLog.Info($"[ModInstaller] {project.Title} exists physically but wasn't tracked. Adding ID {project.ProjectId}.");
                profile.InstalledModIds.Add(project.ProjectId);
                _profileStore.Save(profile);
                return;
            }

            LauncherLog.Info($"[ModInstaller] Found {project.Title}. Installing...");
            await InstallSelectedModAsync(project, cancellationToken);
            LauncherLog.Info($"[ModInstaller] {project.Title} installed successfully.");
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"[ModInstaller] Auto-installation of {slug} failed, but continuing instance operation.", ex);
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

    private void EnsureDeathClientThemeResourcePack(string instancePath, string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(instancePath))
            return;

        try
        {
            var rpDir = Path.Combine(instancePath, "resourcepacks");
            Directory.CreateDirectory(rpDir);
            var zipPath = Path.Combine(rpDir, "DeathClientTheme.zip");

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteTextEntry(
                    archive,
                    "pack.mcmeta",
                    "{\"pack\":{\"pack_format\":1,\"description\":\"Aether Launcher UI theme for home, multiplayer, and singleplayer menus\"}}");

                AddExistingFileToArchive(archive, ResolveThemeLogoPath(), "pack.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_title_logo.png"), "assets/minecraft/textures/gui/title/minecraft.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_title_logo.png"), "assets/minecraft/textures/gui/title/minceraft.png");
                WriteTextEntry(archive, "assets/minecraft/textures/gui/title/minecraft.png.mcmeta", "{\"animation\":{\"frametime\":5}}");
                WriteTextEntry(archive, "assets/minecraft/textures/gui/title/minceraft.png.mcmeta", "{\"animation\":{\"frametime\":5}}");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_edition.png"), "assets/minecraft/textures/gui/title/edition.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_button.png"), "assets/minecraft/textures/gui/sprites/widget/button.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_button_highlighted.png"), "assets/minecraft/textures/gui/sprites/widget/button_highlighted.png");
                WriteTextEntry(archive, "assets/minecraft/textures/gui/sprites/widget/button_highlighted.png.mcmeta", "{\"animation\":{\"frametime\":4}}");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_button_disabled.png"), "assets/minecraft/textures/gui/sprites/widget/button_disabled.png");
                AddExistingFileToArchive(archive, ResolveBundledThemeAsset("death_client_widgets.png"), "assets/minecraft/textures/gui/widgets.png");

                var themeBackground = ResolveThemeBackgroundPath();
                var panoramaBackground = ResolveThemePanoramaPath();
                if (!string.IsNullOrWhiteSpace(panoramaBackground) && IsSquareImage(panoramaBackground))
                {
                    for (var i = 0; i < 6; i++)
                        AddExistingFileToArchive(archive, panoramaBackground, $"assets/minecraft/textures/gui/title/background/panorama_{i}.png");
                }

                if (!string.IsNullOrWhiteSpace(themeBackground))
                    AddExistingFileToArchive(archive, themeBackground, "assets/minecraft/textures/gui/options_background.png");

                WriteTextEntry(
                    archive,
                    "assets/minecraft/texts/splashes.txt",
                    "Aether Launcher: Redefining Play\nUnrivaled Performance, Unmatched Style\nQueue up and dominate\nPeak precision, crafted for champions\nCleanest UI, fastest launch\nOffline mode, but never basic\nJoin the Reborn Movement");

                AddSkinAndCapeEntries(archive);
            }

            UpdateResourcePackOptions(instancePath, "file/DeathClientTheme.zip");
        }
        catch { }
    }

    private void AddSkinAndCapeEntries(ZipArchive archive)
    {
        var allowSkinOverride = !IsUsingMicrosoftAccount() || HasManualSkinOverride();
        var allowCapeOverride = !IsUsingMicrosoftAccount() || HasManualCapeOverride();

        if (allowSkinOverride && !string.IsNullOrWhiteSpace(_settings.CustomSkinPath) && File.Exists(_settings.CustomSkinPath))
        {
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/steve.png");
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/alex.png");
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/player/wide/steve.png");
            AddExistingFileToArchive(archive, _settings.CustomSkinPath, "assets/minecraft/textures/entity/player/slim/alex.png");
        }

        if (allowCapeOverride && !string.IsNullOrWhiteSpace(_settings.CustomCapePath) && File.Exists(_settings.CustomCapePath))
        {
            AddExistingFileToArchive(archive, _settings.CustomCapePath, "assets/minecraft/textures/entity/cape.png");
            AddExistingFileToArchive(archive, _settings.CustomCapePath, "assets/minecraft/textures/entity/elytra.png");
        }
    }

    private void UpdateResourcePackOptions(string instancePath, string packName)
    {
        var optionsPath = Path.Combine(instancePath, "options.txt");
        var lines = File.Exists(optionsPath)
            ? File.ReadAllLines(optionsPath).ToList()
            : [];

        UpsertOptionList(lines, "resourcePacks", packName, includeVanilla: true);
        UpsertOptionList(lines, "incompatibleResourcePacks", packName, includeVanilla: false);
        File.WriteAllLines(optionsPath, lines);
    }

    private static void UpsertOptionList(List<string> lines, string key, string value, bool includeVanilla)
    {
        var index = lines.FindIndex(line => line.StartsWith($"{key}:"));
        var values = index >= 0
            ? ParseOptionList(lines[index])
            : [];

        values.RemoveAll(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        values.Insert(0, value);

        if (includeVanilla && !values.Contains("vanilla", StringComparer.OrdinalIgnoreCase))
            values.Add("vanilla");

        var rendered = string.Join(",", values.Select(item => $"\"{item}\""));
        var nextLine = $"{key}:[{rendered}]";

        if (index >= 0)
            lines[index] = nextLine;
        else
            lines.Add(nextLine);
    }

    private static List<string> ParseOptionList(string line)
    {
        var startIndex = line.IndexOf('[');
        var endIndex = line.LastIndexOf(']');
        if (startIndex < 0 || endIndex <= startIndex)
            return [];

        return line[(startIndex + 1)..endIndex]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().Trim('\"'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveThemeBackgroundPath()
    {
        var customBackground = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBackground))
            return customBackground;

        var bundledBackground = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_menu_background.png");
        if (File.Exists(bundledBackground))
            return bundledBackground;

        return string.Empty;
    }

    private string ResolveThemeLogoPath()
    {
        var bundledLogo = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_logo.png");
        if (File.Exists(bundledLogo))
            return bundledLogo;

        return ResolveThemeBackgroundPath();
    }

    private static string ResolveBundledThemeAsset(string fileName)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        if (File.Exists(bundled))
            return bundled;

        return string.Empty;
    }

    private string ResolveThemePanoramaPath()
    {
        var customBackground = Path.Combine(_defaultMinecraftPath.BasePath, "death-client", "custom_bg.png");
        if (File.Exists(customBackground) && IsSquareImage(customBackground))
            return customBackground;

        var bundledPanorama = Path.Combine(AppContext.BaseDirectory, "Resources", "death_client_panorama.png");
        if (File.Exists(bundledPanorama))
            return bundledPanorama;

        return string.Empty;
    }

    private static void AddExistingFileToArchive(ZipArchive archive, string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        archive.CreateEntryFromFile(sourcePath, destinationPath);
    }

    private static bool IsSquareImage(string path)
    {
        try
        {
            using var bitmap = new Bitmap(path);
            return bitmap.PixelSize.Width == bitmap.PixelSize.Height;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static bool SupportsFancyMenu(LauncherProfile profile)
    {
        var loader = profile.Loader?.Trim().ToLowerInvariant();
        if (loader != "fabric" && loader != "quilt")
            return false;

        return IsFancyMenuCapableVersion(profile.GameVersion);
    }

    private static bool IsFancyMenuCapableVersion(string version)
    {
        var match = Regex.Match(version, @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?");
        if (!match.Success)
            return false;

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);

        if (major >= 24)
            return true;

        return major > 1 || (major == 1 && minor >= 19);
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

    private void ApplyHoverMotion(Control? control)
    {
        if (control == null) return;
        control.Transitions = new Transitions
        {
            new DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
        };
        
        IBrush? originalBg = null;
        IBrush? originalFg = null;
        IBrush? originalBorder = null;
        bool captured = false;
        
        control.PointerEntered += (s, e) =>
        {
            control.Opacity = 0.85;
            control.RenderTransform = TransformOperations.Parse("scale(1.025)");
            
            if (control is Button btn)
            {
                if (!captured)
                {
                    originalBg = btn.Background;
                    originalFg = btn.Foreground;
                    originalBorder = btn.BorderBrush;
                    captured = true;
                }
                
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBackground)) btn.Background = new SolidColorBrush(Color.Parse(_settings.Style.ButtonHoverBackground));
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverForeground)) btn.Foreground = new SolidColorBrush(Color.Parse(_settings.Style.ButtonHoverForeground));
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBorderColor)) btn.BorderBrush = new SolidColorBrush(Color.Parse(_settings.Style.ButtonHoverBorderColor));
            }
        };
        control.PointerExited += (s, e) =>
        {
            control.Opacity = 1.0;
            control.RenderTransform = TransformOperations.Parse("scale(1.0)");
            if (control is Button btn && captured)
            {
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBackground)) btn.Background = originalBg;
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverForeground)) btn.Foreground = originalFg;
                if (!string.IsNullOrWhiteSpace(_settings.Style.ButtonHoverBorderColor)) btn.BorderBrush = originalBorder;
            }
        };
    }

    public async Task ChangeSkinAsync()
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

    public async Task ChangeCapeAsync()
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
    private int GetSystemRamMb()
    {
        try
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                var info = File.ReadAllText("/proc/meminfo");
                var match = Regex.Match(info, @"MemTotal:\s+(\d+)\s+kB");
                if (match.Success) return int.Parse(match.Groups[1].Value) / 1024;
            }
            return (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
        }
        catch { return 8192; } // Fallback to 8GB
    }

    private async Task ExportProfileAsync(LauncherProfile profile)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "Select Export Destination" });
            if (folder == null || folder.Count == 0) return;

            var exportPath = Path.Combine(folder[0].Path.LocalPath, $"{profile.Name}_backup.zip");
            if (File.Exists(exportPath)) File.Delete(exportPath);

            ToggleBusyState(true, $"Exporting {profile.Name}...");

            await Task.Run(() => {
                using var zip = System.IO.Compression.ZipFile.Open(exportPath, System.IO.Compression.ZipArchiveMode.Create);
                
                // Manifest
                var manifestPath = Path.Combine(profile.InstanceDirectory, LauncherProfile.ManifestFileName);
                if (File.Exists(manifestPath))
                    zip.CreateEntryFromFile(manifestPath, LauncherProfile.ManifestFileName);
                
                // Mods
                if (Directory.Exists(profile.ModsDirectory))
                {
                    foreach (var file in Directory.GetFiles(profile.ModsDirectory))
                        zip.CreateEntryFromFile(file, Path.Combine("mods", Path.GetFileName(file)));
                }

                // Config
                var configDir = Path.Combine(profile.InstanceDirectory, "config");
                if (Directory.Exists(configDir))
                {
                    foreach (var file in Directory.GetFiles(configDir, "*", SearchOption.AllDirectories))
                    {
                        var relPath = Path.GetRelativePath(profile.InstanceDirectory, file);
                        zip.CreateEntryFromFile(file, relPath);
                    }
                }
            });

            await DialogService.ShowInfoAsync(this, "Export Success", $"Profile exported to {exportPath}");
        }
        catch (Exception ex) { await DialogService.ShowInfoAsync(this, "Export Failed", ex.Message); }
        finally { ToggleBusyState(false, "Ready."); }
    }

    public async Task ImportProfileZipAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions 
            { 
                Title = "Select Profile Backup (.zip)",
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Backup Zip") { Patterns = ["*.zip"] }]
            });
            if (files == null || files.Count == 0) return;

            ToggleBusyState(true, "Importing profile...");
            
            await Task.Run(() => {
                var zipPath = files[0].Path.LocalPath;
                using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
                
                var manifestEntry = zip.GetEntry(LauncherProfile.ManifestFileName);
                if (manifestEntry == null) throw new Exception("Manifest not found in zip.");

                LauncherProfile? profile;
                using (var stream = manifestEntry.Open())
                {
                    profile = JsonSerializer.Deserialize<LauncherProfile>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                if (profile == null) throw new Exception("Invalid manifest.");

                var targetDir = Path.Combine(_profileStore.GetInstancesRoot(), Slugify(profile.Name));
                int counter = 1;
                while (Directory.Exists(targetDir))
                {
                    targetDir = Path.Combine(_profileStore.GetInstancesRoot(), $"{Slugify(profile.Name)}-{counter++}");
                }

                Directory.CreateDirectory(targetDir);
                foreach (var entry in zip.Entries)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                    if (!fullPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase)) continue;

                    if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(fullPath);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                        entry.ExtractToFile(fullPath, true);
                    }
                }
                
                // Update the manifest with the new directory
                profile.InstanceDirectory = targetDir;
                _profileStore.Save(profile);
            });

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles();
            });
            await DialogService.ShowInfoAsync(this, "Import Success", "The profile has been imported successfully.");
        }
        catch (Exception ex) { await DialogService.ShowInfoAsync(this, "Import Failed", ex.Message); }
        finally { ToggleBusyState(false, "Ready."); }
    }

    public async Task ImportInstanceFolderAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions 
            { 
                Title = "Select Instance Directory" 
            });
            if (folders == null || folders.Count == 0) return;

            var folderPath = folders[0].Path.LocalPath;
            var folderName = Path.GetFileName(folderPath);
            
            // Basic detection for Fabric/Quilt/Forge
            string loader = "vanilla";
            string gameVersion = _settings.Version; // Default from latest selected or 1.21.1
            if (string.IsNullOrEmpty(gameVersion)) gameVersion = "1.21.1";

            if (Directory.Exists(Path.Combine(folderPath, "mods")))
            {
                loader = "fabric"; // Most common for custom folders, or can be detected via jar scan
            }

            var profile = _profileStore.CreateProfile(folderName, gameVersion, loader, null);
            profile.InstanceDirectory = folderPath; // Redirect to external path
            _profileStore.Save(profile);
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                RefreshProfiles(profile);
                SetActiveSection("profiles");
            });
            await DialogService.ShowInfoAsync(this, "Import Success", $"Successfully imported {folderName} as an instance.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Import Error", ex.Message);
        }
        finally { ToggleBusyState(false, "Ready."); }
    }

    private string Slugify(string value)
    {
        return Regex.Replace(value.ToLower(), @"[^a-z0-9]", "-").Trim('-');
    }

    private async Task ScanForModConflictsAsync(LauncherProfile profile)
    {
        if (!Directory.Exists(profile.ModsDirectory)) return;

        var logs = new List<string>();
        var modVersions = new Dictionary<string, string>(); // id -> version

        try
        {
            var jars = Directory.GetFiles(profile.ModsDirectory, "*.jar");
            foreach (var jar in jars)
            {
                try {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(jar);
                    var fabricJson = zip.GetEntry("fabric.mod.json");
                    if (fabricJson != null)
                    {
                        using var stream = fabricJson.Open();
                        using var doc = JsonDocument.Parse(stream);
                        if (doc.RootElement.TryGetProperty("id", out var idProp))
                        {
                            var id = idProp.GetString() ?? "";
                            var version = doc.RootElement.TryGetProperty("version", out var vProp) ? vProp.GetString() : "0.0.0";
                            if (!string.IsNullOrEmpty(id)) modVersions[id] = version ?? "";
                        }
                    }
                } catch { /* Skip malformed jars */ }
            }

            foreach (var jar in jars)
            {
                try {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(jar);
                    var fabricJson = zip.GetEntry("fabric.mod.json");
                    if (fabricJson != null)
                    {
                        using var stream = fabricJson.Open();
                        using var doc = JsonDocument.Parse(stream);
                        var modId = doc.RootElement.GetProperty("id").GetString();
                        if (doc.RootElement.TryGetProperty("depends", out var depends))
                        {
                            foreach (var dep in depends.EnumerateObject())
                            {
                                if (dep.Name == "minecraft" || dep.Name == "fabricloader" || dep.Name == "java" || dep.Name == "fabric") continue;
                                if (!modVersions.ContainsKey(dep.Name))
                                    logs.Add($"• {modId} needs '{dep.Name}' but it's missing.");
                            }
                        }
                    }
                } catch { }
            }

            if (logs.Count == 0)
                await DialogService.ShowInfoAsync(this, "Scan Complete", "No obvious missing dependencies found in fabric.mod.json files.");
            else
                await DialogService.ShowInfoAsync(this, "Potential Conflicts", "Missing dependencies found:\n\n" + string.Join("\n", logs));
        }
        catch (Exception ex) { await DialogService.ShowInfoAsync(this, "Scan Failed", ex.Message); }
    }
    private void UpdateResponsiveLayout()
    {
        if (_avatarGlass == null || _avatarControls == null || _avatarActions == null || _mainContentStack == null) return;

        double threshold = 1180; // Slightly higher threshold for safe floating
        _isNarrowMode = this.Bounds.Width < threshold;

        if (_isNarrowMode)
        {
            _mainContentStack.Margin = new Thickness(0); // Content fills screen
            SetAvatarExpansion(false);
        }
        else
        {
            _mainContentStack.Margin = new Thickness(0, 0, 320, 0); // Content respects panel
            _avatarGlass.Background = new LinearGradientBrush { 
                GradientStops = { new GradientStop(Color.FromArgb(60, 25, 31, 56), 0), new GradientStop(Color.FromArgb(30, 15, 21, 36), 1) } 
            };
            _avatarGlass.BorderThickness = new Thickness(1);
            _avatarGlass.IsHitTestVisible = true;
            _avatarControls.Children[0].IsVisible = true;
            _avatarControls.Children[2].IsVisible = true;
            _avatarActions.IsVisible = true;
            _avatarActions.Opacity = 1;
        }
    }

    private void SetAvatarExpansion(bool expanded)
    {
        if (!_isNarrowMode || _avatarGlass == null || _avatarControls == null || _avatarActions == null) return;

        if (expanded)
        {
            _avatarGlass.Background = new SolidColorBrush(Color.FromArgb(200, 9, 12, 18));
            _avatarGlass.BorderThickness = new Thickness(1);
            _avatarControls.Children[0].IsVisible = true;
            _avatarControls.Children[2].IsVisible = true;
            _avatarActions.IsVisible = true;
            _avatarActions.Opacity = 1;
        }
        else
        {
            _avatarGlass.Background = Brushes.Transparent;
            _avatarGlass.BorderThickness = new Thickness(0);
            _avatarControls.Children[0].IsVisible = false;
            _avatarControls.Children[2].IsVisible = false;
            _avatarActions.IsVisible = false;
            _avatarActions.Opacity = 0;
        }
    }

    private Color GetAccentColor(byte alpha)
    {
        try
        {
            var color = Color.Parse(_settings.AccentColor);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }
        catch
        {
            return Color.FromArgb(alpha, 110, 91, 255); // Fallback to #6E5BFF
        }
    }

    private static TextBlock CreateStatusTextBlock() => new()
    {
        Foreground = Brushes.White,
        FontWeight = FontWeight.SemiBold
    };

    private static TextBlock CreateMutedTextBlock() => new()
    {
        Foreground = new SolidColorBrush(Color.Parse("#A0A8B8"))
    };

    private void UsernameInput_TextChanged(object? sender, TextChangedEventArgs e) => UsernameInput_TextChanged();

    private void CbVersion_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SyncModrinthFilters();
        UpdateLauncherContext();
        UpdateCharacterPreview();
    }

    private async void MinecraftVersion_SelectionChanged(object? sender, SelectionChangedEventArgs e) => await ListVersionsAsync(GetSelectedVersionCategory());
    private async void DownloadVersionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DownloadSelectedVersionAsync();
    private async void RenameProfileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await RenameSelectedProfileAsync();
    private async void ClearProfileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await DeleteSelectedProfileAsync();
    private async void ImportMrpackButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ImportMrpackAsync();
    private async void QuickInstallButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await QuickInstallInstanceAsync();
    private async void QuickModSearchButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await QuickModSearchAsync();
    private void ProfileListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ProfileListBox_SelectionChanged();
    private void ModrinthResultsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateSelectedProjectDetails();

    private async Task PerformFirstRunSetup()
    {
        if (!_settings.IsFirstRun) return;

        // Force reset IsFirstRun only once during development if needed
        // _settings.IsFirstRun = true; 

        // Core directory initialization (silent for all platforms)
        // Core directory initialization in the central data directory
        var directories = new[] 
        { 
            Path.Combine(AppRuntime.DataDirectory, "assets"), 
            Path.Combine(AppRuntime.DataDirectory, "death-client"), 
            Path.Combine(AppRuntime.DataDirectory, "node-skin-server"),
            Path.Combine(AppRuntime.DataDirectory, "death-client-mod")
        };
        foreach (var dir in directories) if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Windows-only visual setup process
        if (OperatingSystem.IsWindows())
        {
            LauncherLog.Info("Performing Windows first-run setup...");
            var setupWin = new SetupWindow();

            try 
            {
                await Dispatcher.UIThread.InvokeAsync(() => setupWin.Show());

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var psCommand = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{Path.Combine(desktopPath, "Aether Launcher.lnk")}'); $s.TargetPath='{exePath}'; $s.Save()";
                    Process.Start(new ProcessStartInfo 
                    { 
                        FileName = "powershell", 
                        Arguments = $"-Command \"{psCommand}\"", 
                        CreateNoWindow = true, 
                        UseShellExecute = false 
                    });
                    LauncherLog.Info("Windows desktop shortcut created.");
                }

                await Task.Delay(4000); // Allow time to read disclaimer
            }
            catch (Exception ex) { LauncherLog.Error("Windows setup failed", ex); }
            finally { await Dispatcher.UIThread.InvokeAsync(() => setupWin.Close()); }
        }

        _settings.IsFirstRun = false;
        _settingsStore.Save(_settings);
    }

    private async void PlayOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_playOverlay).Properties.IsLeftButtonPressed || !_playOverlay.IsEnabled)
            return;

        await LaunchAsync();
    }

    public async void CreateProfileButton_Click() => await CreateProfileAsync();
    public async void BtnStart_Click() => await LaunchAsync();
    public async void ModrinthSearchButton_Click() => await SearchModrinthAsync();
    public void ModrinthResultsListView_SelectedIndexChanged() => UpdateSelectedProjectDetails();
    public async Task ImportLayoutAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select AXAML Layout File",
                FileTypeFilter = [new FilePickerFileType("AXAML") { Patterns = ["*.axaml", "*.runtime"] }]
            });
            if (files == null || files.Count == 0) return;

            // Save the file
            var targetPath = RuntimeLayoutPath;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            await File.WriteAllTextAsync(targetPath, content);

            // Snapshot current style for revert
            _previousStyle = _settings.Style.Clone();
            _revertCts?.Cancel();
            _revertCts?.Dispose();

            // Read properties from the imported file and apply to Style
            ApplyLayoutFileProperties();
            _settingsStore.Save(_settings);

            // Rebuild UI with new style
            InvalidateUiCache();
            Content = BuildRoot();
            SetActiveSection("layout");

            // Show 15-second revert window
            ShowRevertOverlay();
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Import Failed", ex.Message);
        }
    }

    /// <summary>
    /// Reads LayoutProperties from the imported AXAML file and maps them to _settings.Style.
    /// Only the properties specified in the file are updated — everything else stays as-is.
    /// </summary>
    private void ApplyLayoutFileProperties()
    {
        var path = RuntimeLayoutPath;
        if (!File.Exists(path)) return;

        Control? root = null;
        try
        {
            root = UILoader.Load(path);
            _importedLayoutRoot = root;
        }

        catch (Exception ex)
        {
            LauncherLog.Error("Failed to parse layout file for properties.", ex);
            return;
        }
        if (root == null) return;

        var style = _settings.Style;

        // ─── Window / Shell ─────────────────────────────────────────
        var windowShape = LayoutProperties.GetWindowShape(root);
        if (!string.IsNullOrWhiteSpace(windowShape))
        {
            style.BorderStyle = windowShape;
            if (string.Equals(windowShape, "square", StringComparison.OrdinalIgnoreCase))
                style.CornerRadius = 0;
        }

        var windowRadius = LayoutProperties.GetWindowRadius(root);
        if (windowRadius != new Avalonia.CornerRadius(-1))
            style.CornerRadius = (int)windowRadius.TopLeft;

        var windowBg = LayoutProperties.GetWindowBackground(root);
        if (!string.IsNullOrWhiteSpace(windowBg)) style.WindowBackground = windowBg;

        var windowBorder = LayoutProperties.GetWindowBorderColor(root);
        if (!string.IsNullOrWhiteSpace(windowBorder)) style.WindowBorderColor = windowBorder;

        var borderThick = LayoutProperties.GetWindowBorderThickness(root);
        if (!double.IsNaN(borderThick)) style.WindowBorderThickness = borderThick;

        var winMargin = LayoutProperties.GetWindowMargin(root);
        if (!double.IsNaN(winMargin)) style.WindowMargin = winMargin;

        // Window dimensions (applied directly to window, not to style)
        var w = LayoutProperties.GetWindowWidth(root);
        var h = LayoutProperties.GetWindowHeight(root);
        if (!double.IsNaN(w) && w > 0) Width = w;
        if (!double.IsNaN(h) && h > 0) Height = h;
        var minW = LayoutProperties.GetWindowMinWidth(root);
        var minH = LayoutProperties.GetWindowMinHeight(root);
        if (!double.IsNaN(minW) && minW > 0) MinWidth = minW;
        if (!double.IsNaN(minH) && minH > 0) MinHeight = minH;

        // ─── Sidebar ────────────────────────────────────────────────
        var sidebarBg = LayoutProperties.GetSidebarBackground(root);
        if (!string.IsNullOrWhiteSpace(sidebarBg)) style.SidebarBackground = sidebarBg;

        var sidebarBorder = LayoutProperties.GetSidebarBorderColor(root);
        if (!string.IsNullOrWhiteSpace(sidebarBorder)) style.SidebarBorderColor = sidebarBorder;

        var sbWidth = LayoutProperties.GetSidebarWidth(root);
        if (!double.IsNaN(sbWidth) && sbWidth > 0) style.SidebarWidth = sbWidth;

        var sbSide = LayoutProperties.GetSidebarSide(root);
        if (!string.IsNullOrWhiteSpace(sbSide)) style.SidebarSide = sbSide;

        var sbCollapsed = LayoutProperties.GetSidebarCollapsed(root);
        if (string.Equals(sbCollapsed, "true", StringComparison.OrdinalIgnoreCase)) style.SidebarCollapsed = true;
        else if (string.Equals(sbCollapsed, "false", StringComparison.OrdinalIgnoreCase)) style.SidebarCollapsed = false;

        var sbPadding = LayoutProperties.GetSidebarPadding(root);
        if (!double.IsNaN(sbPadding)) style.SidebarPadding = sbPadding;

        // ─── Navigation ─────────────────────────────────────────────
        var navPos = LayoutProperties.GetNavPosition(root);
        if (!string.IsNullOrWhiteSpace(navPos)) style.NavPosition = navPos;

        var navBg = LayoutProperties.GetNavButtonBackground(root);
        if (!string.IsNullOrWhiteSpace(navBg)) style.NavButtonBackground = navBg;

        var navActiveBg = LayoutProperties.GetNavButtonActiveBackground(root);
        if (!string.IsNullOrWhiteSpace(navActiveBg)) style.NavButtonActiveBackground = navActiveBg;

        var navFg = LayoutProperties.GetNavButtonForeground(root);
        if (!string.IsNullOrWhiteSpace(navFg)) style.NavButtonForeground = navFg;

        var navActiveFg = LayoutProperties.GetNavButtonActiveForeground(root);
        if (!string.IsNullOrWhiteSpace(navActiveFg)) style.NavButtonActiveForeground = navActiveFg;

        var navCr = LayoutProperties.GetNavButtonCornerRadius(root);
        if (!double.IsNaN(navCr)) style.NavButtonCornerRadius = navCr;

        var navSpacing = LayoutProperties.GetNavButtonSpacing(root);
        if (!double.IsNaN(navSpacing)) style.NavButtonSpacing = navSpacing;

        var navHeight = LayoutProperties.GetNavButtonHeight(root);
        if (!double.IsNaN(navHeight)) style.NavButtonHeight = navHeight;

        var navFontSize = LayoutProperties.GetNavButtonFontSize(root);
        if (!double.IsNaN(navFontSize)) style.NavButtonFontSize = navFontSize;

        // ─── Typography / Branding ──────────────────────────────────
        var titleText = LayoutProperties.GetTitleText(root);
        if (!string.IsNullOrWhiteSpace(titleText)) style.TitleText = titleText;

        var titleFs = LayoutProperties.GetTitleFontSize(root);
        if (!double.IsNaN(titleFs)) style.TitleFontSize = titleFs;

        var titleFg = LayoutProperties.GetTitleForeground(root);
        if (!string.IsNullOrWhiteSpace(titleFg)) style.TitleForeground = titleFg;

        var fontFamily = LayoutProperties.GetPrimaryFontFamily(root);
        if (!string.IsNullOrWhiteSpace(fontFamily)) style.PrimaryFontFamily = fontFamily;

        var primaryFg = LayoutProperties.GetPrimaryForeground(root);
        if (!string.IsNullOrWhiteSpace(primaryFg)) style.PrimaryForeground = primaryFg;

        var secondaryFg = LayoutProperties.GetSecondaryForeground(root);
        if (!string.IsNullOrWhiteSpace(secondaryFg)) style.SecondaryForeground = secondaryFg;

        // ─── Colors / Accent ────────────────────────────────────────
        var accentColor = LayoutProperties.GetAccentColor(root);
        if (!string.IsNullOrWhiteSpace(accentColor))
        {
            style.AccentColorOverride = accentColor;
            _settings.AccentColor = accentColor; // Also update main accent
        }

        var bgOpacity = LayoutProperties.GetBackgroundOpacity(root);
        if (!double.IsNaN(bgOpacity)) style.BackgroundOpacity = bgOpacity;

        var bgOverlay = LayoutProperties.GetBackgroundOverlayColor(root);
        if (!string.IsNullOrWhiteSpace(bgOverlay)) style.BackgroundOverlayColor = bgOverlay;

        // ─── Cards ──────────────────────────────────────────────────
        var cardBg = LayoutProperties.GetCardBackground(root);
        if (!string.IsNullOrWhiteSpace(cardBg)) style.CardBackground = cardBg;

        var cardCr = LayoutProperties.GetCardCornerRadius(root);
        if (!double.IsNaN(cardCr)) style.CardCornerRadius = cardCr;

        var cardBorder = LayoutProperties.GetCardBorderColor(root);
        if (!string.IsNullOrWhiteSpace(cardBorder)) style.CardBorderColor = cardBorder;

        var cardPad = LayoutProperties.GetCardPadding(root);
        if (!double.IsNaN(cardPad)) style.CardPadding = cardPad;

        // ─── Buttons ────────────────────────────────────────────────
        var btnBg = LayoutProperties.GetButtonBackground(root);
        if (!string.IsNullOrWhiteSpace(btnBg)) style.ButtonBackground = btnBg;

        var btnFg = LayoutProperties.GetButtonForeground(root);
        if (!string.IsNullOrWhiteSpace(btnFg)) style.ButtonForeground = btnFg;

        var btnCr = LayoutProperties.GetButtonCornerRadius(root);
        if (!double.IsNaN(btnCr)) style.ButtonCornerRadius = btnCr;

        var btnH = LayoutProperties.GetButtonHeight(root);
        if (!double.IsNaN(btnH)) style.ButtonHeight = btnH;

        var btnFs = LayoutProperties.GetButtonFontSize(root);
        if (!double.IsNaN(btnFs)) style.ButtonFontSize = btnFs;

        var btnPad = LayoutProperties.GetButtonPadding(root);
        if (!double.IsNaN(btnPad)) style.ButtonPadding = btnPad;

        var contentPad = LayoutProperties.GetContentPadding(root);
        if (!double.IsNaN(contentPad)) style.ContentPadding = contentPad;

        var contentSpacing = LayoutProperties.GetContentSpacing(root);
        if (!double.IsNaN(contentSpacing)) style.ContentSpacing = contentSpacing;

        var contentBg = LayoutProperties.GetContentBackground(root);
        if (!string.IsNullOrWhiteSpace(contentBg)) style.ContentBackground = contentBg;

        // ─── Density ────────────────────────────────────────────────
        var compactMode = LayoutProperties.GetCompactMode(root);
        if (string.Equals(compactMode, "true", StringComparison.OrdinalIgnoreCase)) style.CompactMode = true;
        else if (string.Equals(compactMode, "false", StringComparison.OrdinalIgnoreCase)) style.CompactMode = false;

        // ─── Fields ─────────────────────────────────────────────────
        var fBg = LayoutProperties.GetFieldBackground(root);
        if (!string.IsNullOrWhiteSpace(fBg)) style.FieldBackground = fBg;

        var fFg = LayoutProperties.GetFieldForeground(root);
        if (!string.IsNullOrWhiteSpace(fFg)) style.FieldForeground = fFg;

        var fBrd = LayoutProperties.GetFieldBorderColor(root);
        if (!string.IsNullOrWhiteSpace(fBrd)) style.FieldBorderColor = fBrd;

        var fRad = LayoutProperties.GetFieldRadius(root);
        if (!double.IsNaN(fRad)) style.FieldRadius = fRad;

        var fPad = LayoutProperties.GetFieldPadding(root);
        if (!double.IsNaN(fPad)) style.FieldPadding = fPad;

        var fFs = LayoutProperties.GetFieldFontSize(root);
        if (!double.IsNaN(fFs)) style.FieldFontSize = fFs;

        // ─── Progress Bars ──────────────────────────────────────────
        var pbFg = LayoutProperties.GetProgressBarForeground(root);
        if (!string.IsNullOrWhiteSpace(pbFg)) style.ProgressBarForeground = pbFg;

        var pbBg = LayoutProperties.GetProgressBarBackground(root);
        if (!string.IsNullOrWhiteSpace(pbBg)) style.ProgressBarBackground = pbBg;

        var pbH = LayoutProperties.GetProgressBarHeight(root);
        if (!double.IsNaN(pbH)) style.ProgressBarHeight = pbH;

        var pbR = LayoutProperties.GetProgressBarRadius(root);
        if (!double.IsNaN(pbR)) style.ProgressBarRadius = pbR;

        // ─── Item Cards ─────────────────────────────────────────────
        var iBg = LayoutProperties.GetItemCardBackground(root);
        if (!string.IsNullOrWhiteSpace(iBg)) style.ItemCardBackground = iBg;

        var iRad = LayoutProperties.GetItemCardRadius(root);
        if (!double.IsNaN(iRad)) style.ItemCardRadius = iRad;

        // ─── Overlays ───────────────────────────────────────────────
        var ovl = LayoutProperties.GetOverlayColor(root);
        if (!string.IsNullOrWhiteSpace(ovl)) style.OverlayColor = ovl;

        var aob = LayoutProperties.GetAccountsOverlayBackground(root);
        if (!string.IsNullOrWhiteSpace(aob)) style.AccountsOverlayBackground = aob;

        var aocr = LayoutProperties.GetAccountsOverlayCornerRadius(root);
        if (aocr.HasValue && !double.IsNaN(aocr.Value)) style.AccountsOverlayCornerRadius = aocr.Value;

        var aobc = LayoutProperties.GetAccountsOverlayBorderColor(root);
        if (!string.IsNullOrWhiteSpace(aobc)) style.AccountsOverlayBorderColor = aobc;

        var aobtc = LayoutProperties.GetAccountsOverlayBorderThickness(root);
        if (aobtc.HasValue && !double.IsNaN(aobtc.Value)) style.AccountsOverlayBorderThickness = aobtc.Value;

        // Button Hovers
        var hBg = LayoutProperties.GetButtonHoverBackground(root);
        if (!string.IsNullOrWhiteSpace(hBg)) style.ButtonHoverBackground = hBg;

        var hFg = LayoutProperties.GetButtonHoverForeground(root);
        if (!string.IsNullOrWhiteSpace(hFg)) style.ButtonHoverForeground = hFg;

        var hBrd = LayoutProperties.GetButtonHoverBorderColor(root);
        if (!string.IsNullOrWhiteSpace(hBrd)) style.ButtonHoverBorderColor = hBrd;

        // ─── Sections ───────────────────────────────────────────────
        var sectionOrder = LayoutProperties.GetSectionOrder(root);
        if (!string.IsNullOrWhiteSpace(sectionOrder)) style.SectionOrder = sectionOrder;

        LauncherLog.Info($"[Layout] Applied properties from file: shape={style.BorderStyle}, radius={style.CornerRadius}, " +
                         $"sidebar={style.SidebarSide}, nav={style.NavPosition}, accent={style.AccentColorOverride ?? "default"}");
    }

    private IBrush GetAccentStripBrush()
    {
        return Brushes.Transparent;
    }

    private Control? TryPlaceInSection(string sectionName, Control? defaultContent)
    {
        if (_importedLayoutRoot == null) return defaultContent;

        if (!_namedSlots.TryGetValue(sectionName, out var host))
            host = _importedLayoutRoot.FindControl<Panel>(sectionName);

        if (host == null) return defaultContent;

        host = DetachFromParent(host) as Panel ?? host;
        host.Children.Clear();
        if (defaultContent != null)
            host.Children.Add(defaultContent);

        return host;
    }

    public async Task ResetLayoutAsync()

    {
        try
        {
            // Reset all style tokens to defaults
            _settings.Style = LayoutStyle.Default();
            _settingsStore.Save(_settings);

            // Remove the imported layout file
            if (File.Exists(RuntimeLayoutPath))
                File.Delete(RuntimeLayoutPath);

            InvalidateUiCache();
            Content = BuildRoot();
            SetActiveSection("layout");

            await DialogService.ShowInfoAsync(this, "Layout Reset", "All styles reset to defaults and layout file removed.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Reset Failed", ex.Message);
        }
    }
}

internal static class AvaloniaControlExtensions
{
    public static T With<T>(this T control, int row = -1, int column = -1, int columnSpan = 1, int rowSpan = 1) where T : Control
    {
        if (row >= 0) Grid.SetRow(control, row);
        if (column >= 0) Grid.SetColumn(control, column);
        if (columnSpan > 1) Grid.SetColumnSpan(control, columnSpan);
        if (rowSpan > 1) Grid.SetRowSpan(control, rowSpan);
        return control;
    }

    public static T With<T>(this T control, Action<T> action) where T : Control
    {
        action(control);
        return control;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

#endif
