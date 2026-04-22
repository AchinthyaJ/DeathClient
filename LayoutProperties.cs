using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OfflineMinecraftLauncher;

/// <summary>
/// All attached properties that can be set in an AXAML layout file.
/// Only the properties you specify in the file will be applied — everything else stays default.
///
/// Example AXAML usage:
///   <UserControl xmlns="https://github.com/avaloniaui"
///                xmlns:app="clr-namespace:OfflineMinecraftLauncher"
///                app:LayoutProperties.WindowShape="square"
///                app:LayoutProperties.WindowRadius="0"
///                app:LayoutProperties.SidebarBackground="#0D1117"
///                app:LayoutProperties.AccentColor="#FF5B5B"
///                app:LayoutProperties.TitleText="My Launcher"
///                app:LayoutProperties.CardCornerRadius="12"
///                app:LayoutProperties.NavButtonSpacing="8" />
/// </summary>
public static class LayoutProperties
{
    // ─── Window / Shell ─────────────────────────────────────────────────
    public static readonly AttachedProperty<double> WindowWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("WindowWidth", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> WindowHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("WindowHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> WindowMinWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("WindowMinWidth", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> WindowMinHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("WindowMinHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<CornerRadius> WindowRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, CornerRadius>("WindowRadius", typeof(LayoutProperties), new CornerRadius(-1));

    public static readonly AttachedProperty<string?> WindowShapeProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("WindowShape", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> WindowBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("WindowBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> WindowBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("WindowBorderColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> WindowBorderThicknessProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("WindowBorderThickness", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> WindowMarginProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("WindowMargin", typeof(LayoutProperties), double.NaN);

    // ─── Sidebar ────────────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> SidebarBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SidebarBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> SidebarBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SidebarBorderColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> SidebarWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("SidebarWidth", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> SidebarSideProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SidebarSide", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> SidebarCollapsedProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SidebarCollapsed", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> SidebarPaddingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("SidebarPadding", typeof(LayoutProperties), double.NaN);

    // ─── Navigation ─────────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> NavPositionProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavPosition", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> NavButtonBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavButtonBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> NavButtonActiveBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavButtonActiveBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> NavButtonForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavButtonForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> NavButtonActiveForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavButtonActiveForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> NavButtonCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("NavButtonCornerRadius", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> NavButtonSpacingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("NavButtonSpacing", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> NavButtonHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("NavButtonHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> NavButtonFontSizeProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("NavButtonFontSize", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> NavButtonBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavButtonBorderColor", typeof(LayoutProperties));
    public static readonly AttachedProperty<double?> NavButtonBorderThicknessProperty =
        AvaloniaProperty.RegisterAttached<Control, double?>("NavButtonBorderThickness", typeof(LayoutProperties));

    // ─── Typography / Branding ──────────────────────────────────────────
    public static readonly AttachedProperty<string?> TitleTextProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("TitleText", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("TitleFontSize", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> TitleForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("TitleForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> PrimaryFontFamilyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("PrimaryFontFamily", typeof(LayoutProperties));

    // ─── Typography / Branding (Extended) ───────────────────────────────────
    public static readonly AttachedProperty<string?> TitleFontFamilyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("TitleFontFamily", typeof(LayoutProperties));
    public static readonly AttachedProperty<string?> ButtonFontFamilyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonFontFamily", typeof(LayoutProperties));
    public static readonly AttachedProperty<string?> NavButtonFontFamilyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavButtonFontFamily", typeof(LayoutProperties));
    public static readonly AttachedProperty<string?> FieldFontFamilyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("FieldFontFamily", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> PrimaryForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("PrimaryForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> SecondaryForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SecondaryForeground", typeof(LayoutProperties));

    // ─── Colors / Accent ────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> AccentColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("AccentColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> BackgroundOpacityProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("BackgroundOpacity", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> BackgroundOverlayColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("BackgroundOverlayColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> BackgroundImagePathProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("BackgroundImagePath", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> BackgroundImageUrlProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("BackgroundImageUrl", typeof(LayoutProperties));

    // ─── Cards / Panels ─────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> CardBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("CardBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> CardCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("CardCornerRadius", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> CardBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("CardBorderColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> CardPaddingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("CardPadding", typeof(LayoutProperties), double.NaN);

    // ─── Buttons ────────────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> ButtonBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ButtonForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> ButtonCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ButtonCornerRadius", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> ButtonHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ButtonHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> ButtonFontSizeProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ButtonFontSize", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> ButtonPaddingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ButtonPadding", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> ButtonBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonBorderColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double?> ButtonBorderThicknessProperty =
        AvaloniaProperty.RegisterAttached<Control, double?>("ButtonBorderThickness", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ButtonHoverBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonHoverBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ButtonHoverForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonHoverForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ButtonHoverBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ButtonHoverBorderColor", typeof(LayoutProperties));

    // ─── Content Area ───────────────────────────────────────────────────
    public static readonly AttachedProperty<double> ContentPaddingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ContentPadding", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> ContentSpacingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ContentSpacing", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> ContentBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ContentBackground", typeof(LayoutProperties));

    // ─── Fields (TextBox/ComboBox) ──────────────────────────────────────
    public static readonly AttachedProperty<string?> FieldBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("FieldBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> FieldForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("FieldForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> FieldBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("FieldBorderColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> FieldRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("FieldRadius", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> FieldPaddingProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("FieldPadding", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> FieldFontSizeProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("FieldFontSize", typeof(LayoutProperties), double.NaN);

    // ─── Progress Bars ──────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> ProgressBarForegroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ProgressBarForeground", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ProgressBarBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ProgressBarBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> ProgressBarHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ProgressBarHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> ProgressBarRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ProgressBarRadius", typeof(LayoutProperties), double.NaN);

    // ─── Item Cards (Instances/Mods) ────────────────────────────────────
    public static readonly AttachedProperty<string?> ItemCardBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ItemCardBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> ItemCardRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ItemCardRadius", typeof(LayoutProperties), double.NaN);

    // ─── Overlays ───────────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> OverlayColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("OverlayColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> SectionOrderProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SectionOrder", typeof(LayoutProperties));

    // ─── Compact / Density ──────────────────────────────────────────────
    public static readonly AttachedProperty<string?> CompactModeProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("CompactMode", typeof(LayoutProperties));

    // ─── Accounts Button ────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> AccountsButtonContentProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("AccountsButtonContent", typeof(LayoutProperties), "Accounts");

    // ─── Accounts Overlay ───────────────────────────────────────────────
    public static readonly AttachedProperty<string?> AccountsOverlayBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("AccountsOverlayBackground", typeof(LayoutProperties));
    public static readonly AttachedProperty<double?> AccountsOverlayCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double?>("AccountsOverlayCornerRadius", typeof(LayoutProperties));
    public static readonly AttachedProperty<string?> AccountsOverlayBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("AccountsOverlayBorderColor", typeof(LayoutProperties));
    public static readonly AttachedProperty<double?> AccountsOverlayBorderThicknessProperty =
        AvaloniaProperty.RegisterAttached<Control, double?>("AccountsOverlayBorderThickness", typeof(LayoutProperties));

    // ─── Hover Effects (existing) ───────────────────────────────────────
    public static readonly AttachedProperty<string?> HoverAnimationProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("HoverAnimation", typeof(LayoutProperties));

    public static readonly AttachedProperty<IBrush?> BackgroundOnHoverProperty =
        AvaloniaProperty.RegisterAttached<Control, IBrush?>("BackgroundOnHover", typeof(LayoutProperties));

    // ─── Header (top bar / title area) ──────────────────────────────────
    // These were previously removed but user AXAML files may reference them.
    // They are read in ApplyLayoutFileProperties and forwarded to SidebarBackground / similar.
    public static readonly AttachedProperty<string?> HeaderBackgroundProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("HeaderBackground", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> HeaderHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("HeaderHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> HeaderBorderColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("HeaderBorderColor", typeof(LayoutProperties));

    // ─── Section Divider ────────────────────────────────────────────────
    public static readonly AttachedProperty<string?> SectionDividerColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("SectionDividerColor", typeof(LayoutProperties));

    // ─── Compatibility Tokens (kept to prevent AXAML stripping) ────────
    public static readonly AttachedProperty<double> AccentStripHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("AccentStripHeight", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> AccentStripColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("AccentStripColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> NavIndicatorStyleProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavIndicatorStyle", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> NavIndicatorColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavIndicatorColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> AccentGlowProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("AccentGlow", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> AccentGlowIntensityProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("AccentGlowIntensity", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<string?> StatusOnlineColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("StatusOnlineColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> StatusWarningColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("StatusWarningColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> StatusOfflineColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("StatusOfflineColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ScrollbarThumbColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ScrollbarThumbColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<string?> ScrollbarTrackColorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("ScrollbarTrackColor", typeof(LayoutProperties));

    public static readonly AttachedProperty<double> ScrollbarWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ScrollbarWidth", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> BackgroundOverlayOpacityProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("BackgroundOverlayOpacity", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<double> BackgroundImageBlurProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("BackgroundImageBlur", typeof(LayoutProperties), double.NaN);

    public static readonly AttachedProperty<bool> PlayButtonGlobalProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("PlayButtonGlobal", typeof(LayoutProperties), false);

    // ═══ Getters / Setters ══════════════════════════════════════════════

    // Window
    public static double GetWindowWidth(Control c) => c.GetValue(WindowWidthProperty);
    public static void SetWindowWidth(Control c, double v) => c.SetValue(WindowWidthProperty, v);
    public static double GetWindowHeight(Control c) => c.GetValue(WindowHeightProperty);
    public static void SetWindowHeight(Control c, double v) => c.SetValue(WindowHeightProperty, v);
    public static double GetWindowMinWidth(Control c) => c.GetValue(WindowMinWidthProperty);
    public static void SetWindowMinWidth(Control c, double v) => c.SetValue(WindowMinWidthProperty, v);
    public static double GetWindowMinHeight(Control c) => c.GetValue(WindowMinHeightProperty);
    public static void SetWindowMinHeight(Control c, double v) => c.SetValue(WindowMinHeightProperty, v);
    public static CornerRadius GetWindowRadius(Control c) => c.GetValue(WindowRadiusProperty);
    public static void SetWindowRadius(Control c, CornerRadius v) => c.SetValue(WindowRadiusProperty, v);
    public static string? GetWindowShape(Control c) => c.GetValue(WindowShapeProperty);
    public static void SetWindowShape(Control c, string? v) => c.SetValue(WindowShapeProperty, v);
    public static string? GetWindowBackground(Control c) => c.GetValue(WindowBackgroundProperty);
    public static void SetWindowBackground(Control c, string? v) => c.SetValue(WindowBackgroundProperty, v);
    public static string? GetWindowBorderColor(Control c) => c.GetValue(WindowBorderColorProperty);
    public static void SetWindowBorderColor(Control c, string? v) => c.SetValue(WindowBorderColorProperty, v);
    public static double GetWindowBorderThickness(Control c) => c.GetValue(WindowBorderThicknessProperty);
    public static void SetWindowBorderThickness(Control c, double v) => c.SetValue(WindowBorderThicknessProperty, v);
    public static double GetWindowMargin(Control c) => c.GetValue(WindowMarginProperty);
    public static void SetWindowMargin(Control c, double v) => c.SetValue(WindowMarginProperty, v);

    // Sidebar
    public static string? GetSidebarBackground(Control c) => c.GetValue(SidebarBackgroundProperty);
    public static void SetSidebarBackground(Control c, string? v) => c.SetValue(SidebarBackgroundProperty, v);
    public static string? GetSidebarBorderColor(Control c) => c.GetValue(SidebarBorderColorProperty);
    public static void SetSidebarBorderColor(Control c, string? v) => c.SetValue(SidebarBorderColorProperty, v);
    public static double GetSidebarWidth(Control c) => c.GetValue(SidebarWidthProperty);
    public static void SetSidebarWidth(Control c, double v) => c.SetValue(SidebarWidthProperty, v);
    public static string? GetSidebarSide(Control c) => c.GetValue(SidebarSideProperty);
    public static void SetSidebarSide(Control c, string? v) => c.SetValue(SidebarSideProperty, v);
    public static string? GetSidebarCollapsed(Control c) => c.GetValue(SidebarCollapsedProperty);
    public static void SetSidebarCollapsed(Control c, string? v) => c.SetValue(SidebarCollapsedProperty, v);
    public static double GetSidebarPadding(Control c) => c.GetValue(SidebarPaddingProperty);
    public static void SetSidebarPadding(Control c, double v) => c.SetValue(SidebarPaddingProperty, v);

    // Navigation
    public static string? GetNavPosition(Control c) => c.GetValue(NavPositionProperty);
    public static void SetNavPosition(Control c, string? v) => c.SetValue(NavPositionProperty, v);
    public static string? GetNavButtonBackground(Control c) => c.GetValue(NavButtonBackgroundProperty);
    public static void SetNavButtonBackground(Control c, string? v) => c.SetValue(NavButtonBackgroundProperty, v);
    public static string? GetNavButtonActiveBackground(Control c) => c.GetValue(NavButtonActiveBackgroundProperty);
    public static void SetNavButtonActiveBackground(Control c, string? v) => c.SetValue(NavButtonActiveBackgroundProperty, v);
    public static string? GetNavButtonForeground(Control c) => c.GetValue(NavButtonForegroundProperty);
    public static void SetNavButtonForeground(Control c, string? v) => c.SetValue(NavButtonForegroundProperty, v);
    public static string? GetNavButtonActiveForeground(Control c) => c.GetValue(NavButtonActiveForegroundProperty);
    public static void SetNavButtonActiveForeground(Control c, string? v) => c.SetValue(NavButtonActiveForegroundProperty, v);
    public static double GetNavButtonCornerRadius(Control c) => c.GetValue(NavButtonCornerRadiusProperty);
    public static void SetNavButtonCornerRadius(Control c, double v) => c.SetValue(NavButtonCornerRadiusProperty, v);
    public static double GetNavButtonSpacing(Control c) => c.GetValue(NavButtonSpacingProperty);
    public static void SetNavButtonSpacing(Control c, double v) => c.SetValue(NavButtonSpacingProperty, v);
    public static double GetNavButtonHeight(Control c) => c.GetValue(NavButtonHeightProperty);
    public static void SetNavButtonHeight(Control c, double v) => c.SetValue(NavButtonHeightProperty, v);
    public static double GetNavButtonFontSize(Control c) => c.GetValue(NavButtonFontSizeProperty);
    public static void SetNavButtonFontSize(Control c, double v) => c.SetValue(NavButtonFontSizeProperty, v);

    // Typography
    public static string? GetTitleText(Control c) => c.GetValue(TitleTextProperty);
    public static void SetTitleText(Control c, string? v) => c.SetValue(TitleTextProperty, v);
    public static double GetTitleFontSize(Control c) => c.GetValue(TitleFontSizeProperty);
    public static void SetTitleFontSize(Control c, double v) => c.SetValue(TitleFontSizeProperty, v);
    public static string? GetTitleForeground(Control c) => c.GetValue(TitleForegroundProperty);
    public static void SetTitleForeground(Control c, string? v) => c.SetValue(TitleForegroundProperty, v);
    public static string? GetPrimaryFontFamily(Control c) => c.GetValue(PrimaryFontFamilyProperty);
    public static void SetPrimaryFontFamily(Control c, string? v) => c.SetValue(PrimaryFontFamilyProperty, v);

    // Typography / Branding (Extended)
    public static string? GetTitleFontFamily(Control c) => c.GetValue(TitleFontFamilyProperty);
    public static void SetTitleFontFamily(Control c, string? v) => c.SetValue(TitleFontFamilyProperty, v);
    public static string? GetButtonFontFamily(Control c) => c.GetValue(ButtonFontFamilyProperty);
    public static void SetButtonFontFamily(Control c, string? v) => c.SetValue(ButtonFontFamilyProperty, v);
    public static string? GetNavButtonFontFamily(Control c) => c.GetValue(NavButtonFontFamilyProperty);
    public static void SetNavButtonFontFamily(Control c, string? v) => c.SetValue(NavButtonFontFamilyProperty, v);
    public static string? GetFieldFontFamily(Control c) => c.GetValue(FieldFontFamilyProperty);
    public static void SetFieldFontFamily(Control c, string? v) => c.SetValue(FieldFontFamilyProperty, v);
    public static string? GetPrimaryForeground(Control c) => c.GetValue(PrimaryForegroundProperty);
    public static void SetPrimaryForeground(Control c, string? v) => c.SetValue(PrimaryForegroundProperty, v);
    public static string? GetSecondaryForeground(Control c) => c.GetValue(SecondaryForegroundProperty);
    public static void SetSecondaryForeground(Control c, string? v) => c.SetValue(SecondaryForegroundProperty, v);

    // Colors
    public static string? GetAccentColor(Control c) => c.GetValue(AccentColorProperty);
    public static void SetAccentColor(Control c, string? v) => c.SetValue(AccentColorProperty, v);
    public static double GetBackgroundOpacity(Control c) => c.GetValue(BackgroundOpacityProperty);
    public static void SetBackgroundOpacity(Control c, double v) => c.SetValue(BackgroundOpacityProperty, v);
    public static string? GetBackgroundOverlayColor(Control c) => c.GetValue(BackgroundOverlayColorProperty);
    public static void SetBackgroundOverlayColor(Control c, string? v) => c.SetValue(BackgroundOverlayColorProperty, v);
    public static string? GetBackgroundImagePath(Control c) => c.GetValue(BackgroundImagePathProperty);
    public static void SetBackgroundImagePath(Control c, string? v) => c.SetValue(BackgroundImagePathProperty, v);
    public static string? GetBackgroundImageUrl(Control c) => c.GetValue(BackgroundImageUrlProperty);
    public static void SetBackgroundImageUrl(Control c, string? v) => c.SetValue(BackgroundImageUrlProperty, v);

    // Cards
    public static string? GetCardBackground(Control c) => c.GetValue(CardBackgroundProperty);
    public static void SetCardBackground(Control c, string? v) => c.SetValue(CardBackgroundProperty, v);
    public static double GetCardCornerRadius(Control c) => c.GetValue(CardCornerRadiusProperty);
    public static void SetCardCornerRadius(Control c, double v) => c.SetValue(CardCornerRadiusProperty, v);
    public static string? GetCardBorderColor(Control c) => c.GetValue(CardBorderColorProperty);
    public static void SetCardBorderColor(Control c, string? v) => c.SetValue(CardBorderColorProperty, v);
    public static double GetCardPadding(Control c) => c.GetValue(CardPaddingProperty);
    public static void SetCardPadding(Control c, double v) => c.SetValue(CardPaddingProperty, v);

    // Buttons
    public static string? GetButtonBackground(Control c) => c.GetValue(ButtonBackgroundProperty);
    public static void SetButtonBackground(Control c, string? v) => c.SetValue(ButtonBackgroundProperty, v);
    public static string? GetButtonForeground(Control c) => c.GetValue(ButtonForegroundProperty);
    public static void SetButtonForeground(Control c, string? v) => c.SetValue(ButtonForegroundProperty, v);
    public static double GetButtonCornerRadius(Control c) => c.GetValue(ButtonCornerRadiusProperty);
    public static void SetButtonCornerRadius(Control c, double v) => c.SetValue(ButtonCornerRadiusProperty, v);
    public static double GetButtonHeight(Control c) => c.GetValue(ButtonHeightProperty);
    public static void SetButtonHeight(Control c, double v) => c.SetValue(ButtonHeightProperty, v);
    public static double GetButtonFontSize(Control c) => c.GetValue(ButtonFontSizeProperty);
    public static void SetButtonFontSize(Control c, double v) => c.SetValue(ButtonFontSizeProperty, v);
    public static double GetButtonPadding(Control c) => c.GetValue(ButtonPaddingProperty);
    public static void SetButtonPadding(Control c, double v) => c.SetValue(ButtonPaddingProperty, v);

    // Buttons (Extended)
    public static string? GetButtonBorderColor(Control c) => c.GetValue(ButtonBorderColorProperty);
    public static void SetButtonBorderColor(Control c, string? v) => c.SetValue(ButtonBorderColorProperty, v);
    public static double? GetButtonBorderThickness(Control c) => c.GetValue(ButtonBorderThicknessProperty);
    public static void SetButtonBorderThickness(Control c, double? v) => c.SetValue(ButtonBorderThicknessProperty, v);

    public static string? GetButtonHoverBackground(Control c) => c.GetValue(ButtonHoverBackgroundProperty);
    public static void SetButtonHoverBackground(Control c, string? v) => c.SetValue(ButtonHoverBackgroundProperty, v);
    public static string? GetButtonHoverForeground(Control c) => c.GetValue(ButtonHoverForegroundProperty);
    public static void SetButtonHoverForeground(Control c, string? v) => c.SetValue(ButtonHoverForegroundProperty, v);
    public static string? GetButtonHoverBorderColor(Control c) => c.GetValue(ButtonHoverBorderColorProperty);
    public static void SetButtonHoverBorderColor(Control c, string? v) => c.SetValue(ButtonHoverBorderColorProperty, v);

    public static string? GetNavButtonBorderColor(Control c) => c.GetValue(NavButtonBorderColorProperty);
    public static void SetNavButtonBorderColor(Control c, string? v) => c.SetValue(NavButtonBorderColorProperty, v);
    public static double? GetNavButtonBorderThickness(Control c) => c.GetValue(NavButtonBorderThicknessProperty);
    public static void SetNavButtonBorderThickness(Control c, double? v) => c.SetValue(NavButtonBorderThicknessProperty, v);

    // Content
    public static double GetContentPadding(Control c) => c.GetValue(ContentPaddingProperty);
    public static void SetContentPadding(Control c, double v) => c.SetValue(ContentPaddingProperty, v);
    public static double GetContentSpacing(Control c) => c.GetValue(ContentSpacingProperty);
    public static void SetContentSpacing(Control c, double v) => c.SetValue(ContentSpacingProperty, v);
    public static string? GetContentBackground(Control c) => c.GetValue(ContentBackgroundProperty);
    public static void SetContentBackground(Control c, string? v) => c.SetValue(ContentBackgroundProperty, v);

    // Density
    public static string? GetCompactMode(Control c) => c.GetValue(CompactModeProperty);
    public static void SetCompactMode(Control c, string? v) => c.SetValue(CompactModeProperty, v);

    // Accounts Button
    public static string? GetAccountsButtonContent(Control c) => c.GetValue(AccountsButtonContentProperty);
    public static void SetAccountsButtonContent(Control c, string? v) => c.SetValue(AccountsButtonContentProperty, v);

    // Accounts Overlay
    public static string? GetAccountsOverlayBackground(Control c) => c.GetValue(AccountsOverlayBackgroundProperty);
    public static void SetAccountsOverlayBackground(Control c, string? v) => c.SetValue(AccountsOverlayBackgroundProperty, v);
    public static double? GetAccountsOverlayCornerRadius(Control c) => c.GetValue(AccountsOverlayCornerRadiusProperty);
    public static void SetAccountsOverlayCornerRadius(Control c, double? v) => c.SetValue(AccountsOverlayCornerRadiusProperty, v);
    public static string? GetAccountsOverlayBorderColor(Control c) => c.GetValue(AccountsOverlayBorderColorProperty);
    public static void SetAccountsOverlayBorderColor(Control c, string? v) => c.SetValue(AccountsOverlayBorderColorProperty, v);
    public static double? GetAccountsOverlayBorderThickness(Control c) => c.GetValue(AccountsOverlayBorderThicknessProperty);
    public static void SetAccountsOverlayBorderThickness(Control c, double? v) => c.SetValue(AccountsOverlayBorderThicknessProperty, v);

    // Fields
    public static string? GetFieldBackground(Control c) => c.GetValue(FieldBackgroundProperty);
    public static void SetFieldBackground(Control c, string? v) => c.SetValue(FieldBackgroundProperty, v);
    public static string? GetFieldForeground(Control c) => c.GetValue(FieldForegroundProperty);
    public static void SetFieldForeground(Control c, string? v) => c.SetValue(FieldForegroundProperty, v);
    public static string? GetFieldBorderColor(Control c) => c.GetValue(FieldBorderColorProperty);
    public static void SetFieldBorderColor(Control c, string? v) => c.SetValue(FieldBorderColorProperty, v);
    public static double GetFieldRadius(Control c) => c.GetValue(FieldRadiusProperty);
    public static void SetFieldRadius(Control c, double v) => c.SetValue(FieldRadiusProperty, v);
    public static double GetFieldPadding(Control c) => c.GetValue(FieldPaddingProperty);
    public static void SetFieldPadding(Control c, double v) => c.SetValue(FieldPaddingProperty, v);
    public static double GetFieldFontSize(Control c) => c.GetValue(FieldFontSizeProperty);
    public static void SetFieldFontSize(Control c, double v) => c.SetValue(FieldFontSizeProperty, v);

    // Progress
    public static string? GetProgressBarForeground(Control c) => c.GetValue(ProgressBarForegroundProperty);
    public static void SetProgressBarForeground(Control c, string? v) => c.SetValue(ProgressBarForegroundProperty, v);
    public static string? GetProgressBarBackground(Control c) => c.GetValue(ProgressBarBackgroundProperty);
    public static void SetProgressBarBackground(Control c, string? v) => c.SetValue(ProgressBarBackgroundProperty, v);
    public static double GetProgressBarHeight(Control c) => c.GetValue(ProgressBarHeightProperty);
    public static void SetProgressBarHeight(Control c, double v) => c.SetValue(ProgressBarHeightProperty, v);
    public static double GetProgressBarRadius(Control c) => c.GetValue(ProgressBarRadiusProperty);
    public static void SetProgressBarRadius(Control c, double v) => c.SetValue(ProgressBarRadiusProperty, v);

    // Item Cards
    public static string? GetItemCardBackground(Control c) => c.GetValue(ItemCardBackgroundProperty);
    public static void SetItemCardBackground(Control c, string? v) => c.SetValue(ItemCardBackgroundProperty, v);
    public static double GetItemCardRadius(Control c) => c.GetValue(ItemCardRadiusProperty);
    public static void SetItemCardRadius(Control c, double v) => c.SetValue(ItemCardRadiusProperty, v);

    // Overlays
    public static string? GetOverlayColor(Control c) => c.GetValue(OverlayColorProperty);
    public static void SetOverlayColor(Control c, string? v) => c.SetValue(OverlayColorProperty, v);

    // Sections
    public static string? GetSectionOrder(Control c) => c.GetValue(SectionOrderProperty);
    public static void SetSectionOrder(Control c, string? v) => c.SetValue(SectionOrderProperty, v);

    // Hover
    public static string? GetHoverAnimation(Control c) => c.GetValue(HoverAnimationProperty);
    public static void SetHoverAnimation(Control c, string? v) => c.SetValue(HoverAnimationProperty, v);
    public static IBrush? GetBackgroundOnHover(Control c) => c.GetValue(BackgroundOnHoverProperty);
    public static void SetBackgroundOnHover(Control c, IBrush? v) => c.SetValue(BackgroundOnHoverProperty, v);

    // Header
    public static string? GetHeaderBackground(Control c) => c.GetValue(HeaderBackgroundProperty);
    public static void SetHeaderBackground(Control c, string? v) => c.SetValue(HeaderBackgroundProperty, v);
    public static double GetHeaderHeight(Control c) => c.GetValue(HeaderHeightProperty);
    public static void SetHeaderHeight(Control c, double v) => c.SetValue(HeaderHeightProperty, v);
    public static string? GetHeaderBorderColor(Control c) => c.GetValue(HeaderBorderColorProperty);
    public static void SetHeaderBorderColor(Control c, string? v) => c.SetValue(HeaderBorderColorProperty, v);

    // Section Divider
    public static string? GetSectionDividerColor(Control c) => c.GetValue(SectionDividerColorProperty);
    public static void SetSectionDividerColor(Control c, string? v) => c.SetValue(SectionDividerColorProperty, v);

    // Compatibility Tokens
    public static double GetAccentStripHeight(Control c) => c.GetValue(AccentStripHeightProperty);
    public static void SetAccentStripHeight(Control c, double v) => c.SetValue(AccentStripHeightProperty, v);
    public static string? GetAccentStripColor(Control c) => c.GetValue(AccentStripColorProperty);
    public static void SetAccentStripColor(Control c, string? v) => c.SetValue(AccentStripColorProperty, v);
    public static string? GetNavIndicatorStyle(Control c) => c.GetValue(NavIndicatorStyleProperty);
    public static void SetNavIndicatorStyle(Control c, string? v) => c.SetValue(NavIndicatorStyleProperty, v);
    public static string? GetNavIndicatorColor(Control c) => c.GetValue(NavIndicatorColorProperty);
    public static void SetNavIndicatorColor(Control c, string? v) => c.SetValue(NavIndicatorColorProperty, v);
    public static string? GetAccentGlow(Control c) => c.GetValue(AccentGlowProperty);
    public static void SetAccentGlow(Control c, string? v) => c.SetValue(AccentGlowProperty, v);
    public static double GetAccentGlowIntensity(Control c) => c.GetValue(AccentGlowIntensityProperty);
    public static void SetAccentGlowIntensity(Control c, double v) => c.SetValue(AccentGlowIntensityProperty, v);
    public static string? GetStatusOnlineColor(Control c) => c.GetValue(StatusOnlineColorProperty);
    public static void SetStatusOnlineColor(Control c, string? v) => c.SetValue(StatusOnlineColorProperty, v);
    public static string? GetStatusWarningColor(Control c) => c.GetValue(StatusWarningColorProperty);
    public static void SetStatusWarningColor(Control c, string? v) => c.SetValue(StatusWarningColorProperty, v);
    public static string? GetStatusOfflineColor(Control c) => c.GetValue(StatusOfflineColorProperty);
    public static void SetStatusOfflineColor(Control c, string? v) => c.SetValue(StatusOfflineColorProperty, v);
    public static string? GetScrollbarThumbColor(Control c) => c.GetValue(ScrollbarThumbColorProperty);
    public static void SetScrollbarThumbColor(Control c, string? v) => c.SetValue(ScrollbarThumbColorProperty, v);
    public static string? GetScrollbarTrackColor(Control c) => c.GetValue(ScrollbarTrackColorProperty);
    public static void SetScrollbarTrackColor(Control c, string? v) => c.SetValue(ScrollbarTrackColorProperty, v);
    public static double GetScrollbarWidth(Control c) => c.GetValue(ScrollbarWidthProperty);
    public static void SetScrollbarWidth(Control c, double v) => c.SetValue(ScrollbarWidthProperty, v);
    public static double GetBackgroundOverlayOpacity(Control c) => c.GetValue(BackgroundOverlayOpacityProperty);
    public static void SetBackgroundOverlayOpacity(Control c, double v) => c.SetValue(BackgroundOverlayOpacityProperty, v);
    public static double GetBackgroundImageBlur(Control c) => c.GetValue(BackgroundImageBlurProperty);
    public static void SetBackgroundImageBlur(Control c, double v) => c.SetValue(BackgroundImageBlurProperty, v);
    public static bool GetPlayButtonGlobal(Control c) => c.GetValue(PlayButtonGlobalProperty);
    public static void SetPlayButtonGlobal(Control c, bool v) => c.SetValue(PlayButtonGlobalProperty, v);
}
