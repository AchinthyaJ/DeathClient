using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;

namespace OfflineMinecraftLauncher;

public static class UIBinder
{
    public static void Bind(Control? root, MainWindow form)
    {
        if (root == null) return;
        var controls = GetControls(root);

        var formType = typeof(MainWindow);
        var sectionFields = new[] { "launchSection", "modrinthSection", "profilesSection", "performanceSection", "settingsSection", "layoutSection" };
        
        // Loop through and bind all controls in the AXAML to their corresponding fields in the form
        var fields = formType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                             .Where(f => typeof(Control).IsAssignableFrom(f.FieldType));
                             
        var fieldDict = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields) fieldDict[f.Name] = f;

        foreach (var control in controls)
        {
            if (!string.IsNullOrEmpty(control.Name) && fieldDict.TryGetValue(control.Name, out var field))
            {
                if (sectionFields.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (field.FieldType.IsAssignableFrom(control.GetType()))
                {
                    field.SetValue(form, control);
                }
            }
        }
        
        // --- Fallback Content Injection ---
        // Section fields are owned by MainWindow. AXAML can provide either one MainContentArea
        // host or individual hosts named like launchSection / launchSectionHost.
        var contentArea = FindControl<Panel>(controls, "MainContentArea");
        if (contentArea != null)
        {
            foreach (var sectionName in sectionFields)
            {
                if (fieldDict.TryGetValue(sectionName, out var field))
                {
                    var sectionObj = field.GetValue(form);
                    if (sectionObj is Control section)
                    {
                        var sectionHost = FindSectionHost(controls, sectionName) ?? contentArea;
                        Rehost(sectionHost, section);
                    }
                }
            }
        }

        controls = GetControls(root);

        ApplyLayoutProperties(root, controls, form);
        ApplyControlRules(controls, form, formType);
        EnsureLauncherTitle(root, controls);
        WireNavigationButtons(controls, form);

    }

    private static List<Control> GetControls(Control root)
    {
        var controls = new List<Control> { root };
        controls.AddRange(root.GetVisualDescendants().OfType<Control>());
        return controls;
    }

    private static void ApplyControlRules(List<Control> controls, MainWindow form, Type formType)
    {
        InitializeComboBoxItems(controls, form, formType);
        InitializeListItems(controls, form, formType);
        WireTextBoxes(controls, form);
        WireComboBoxes(controls, form);
        WireListBoxes(controls, form);
        WireButtons(controls, form);
    }

    private static void WireNavigationButtons(List<Control> controls, MainWindow form)
    {
        foreach (var button in controls.OfType<Button>())
        {
            var target = GetNavigationTarget(button);
            if (string.IsNullOrWhiteSpace(target))
                continue;

            if (target == "accounts")
                button.Click += (_, _) => Invoke(form, "ShowAccountsOverlay");
            else
                button.Click += (_, _) => form.SetActiveSection(NormalizeSectionName(target));

            WireHoverMotion(button);
        }
    }

    private static string? GetNavigationTarget(Button button)
    {
        var explicitTarget = button.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(explicitTarget))
            return IsKnownSection(explicitTarget) ? explicitTarget.ToLowerInvariant() : null;

        return button.Name?.ToLowerInvariant() switch
        {
            "launchnavbutton" or "btnlaunch" or "launchbutton" => "launch",
            "modrinthnavbutton" or "modsnavbutton" or "btnmods" or "modsbutton" => "modrinth",
            "profilesnavbutton" or "instancesnavbutton" or "btninstances" or "profilesbutton" => "profiles",
            "performancenavbutton" or "btnperformance" or "performancebutton" => "performance",
            "settingsnavbutton" or "btnsettings" or "settingsbutton" => "settings",
            "layoutnavbutton" or "headerlayoutbutton" or "footerlayoutbutton" or "btnlayout" or "layoutbutton" => "layout",
            "accountsnavbutton" or "btnaccounts" or "accountsbutton" => "accounts",
            _ => null
        };
    }

    private static bool IsKnownSection(string value)
    {
        var section = value.ToLowerInvariant();
        return section is "home" or "launch" or "instances" or "profiles" or "modrinth" or "mods" or "performance" or "settings" or "layout" or "accounts";
    }

    private static void ApplyLayoutProperties(Control root, List<Control> controls, MainWindow form)
    {
        ApplyWindowProperties(root, controls, form);

        foreach (var control in controls)
        {
            ApplyHoverProperties(control);
        }
    }

    private static void ApplyWindowProperties(Control root, List<Control> controls, MainWindow form)
    {
        var windowWidth = LayoutProperties.GetWindowWidth(root);
        var windowHeight = LayoutProperties.GetWindowHeight(root);
        var windowMinWidth = LayoutProperties.GetWindowMinWidth(root);
        var windowMinHeight = LayoutProperties.GetWindowMinHeight(root);

        if (!double.IsNaN(windowWidth) && windowWidth > 0) form.Width = windowWidth;
        if (!double.IsNaN(windowHeight) && windowHeight > 0) form.Height = windowHeight;
        if (!double.IsNaN(windowMinWidth) && windowMinWidth > 0) form.MinWidth = windowMinWidth;
        if (!double.IsNaN(windowMinHeight) && windowMinHeight > 0) form.MinHeight = windowMinHeight;

        var windowShape = LayoutProperties.GetWindowShape(root);
        var windowRadius = LayoutProperties.GetWindowRadius(root);

        if (!string.IsNullOrWhiteSpace(windowShape) && string.Equals(windowShape, "square", StringComparison.OrdinalIgnoreCase))
            windowRadius = new CornerRadius(0);

        ApplyWindowRadius(root, controls, windowRadius);
    }

    private static void ApplyWindowRadius(Control root, List<Control> controls, CornerRadius radius)
    {
        var surface = FindControl<Border>(controls, "WindowSurface")
            ?? FindControl<Border>(controls, "LauncherWindowSurface")
            ?? root as Border;

        if (surface == null && root is ContentControl contentControl && contentControl.Content is Border border)
            surface = border;

        if (surface == null)
            return;

        surface.CornerRadius = radius;
        surface.ClipToBounds = true;
    }

    private static void ApplyHoverProperties(Control control)
    {
        var hoverAnimation = LayoutProperties.GetHoverAnimation(control);
        var hoverBackground = LayoutProperties.GetBackgroundOnHover(control);

        if (string.IsNullOrWhiteSpace(hoverAnimation) && hoverBackground == null)
            return;

        IBrush? normalBackground = control switch
        {
            Button button => button.Background,
            Border border => border.Background,
            Panel panel => panel.Background,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(hoverAnimation))
            WireNamedHoverAnimation(control, hoverAnimation);

        if (hoverBackground != null)
        {
            control.PointerEntered += (_, _) => SetBackground(control, hoverBackground);
            control.PointerExited += (_, _) => SetBackground(control, normalBackground);
        }
    }

    private static void WireNamedHoverAnimation(Control control, string hoverAnimation)
    {
        var mode = hoverAnimation.Trim().ToLowerInvariant();

        control.PointerEntered += (_, _) =>
        {
            if (mode is "none" or "off" or "false")
                return;

            control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            control.RenderTransform = mode switch
            {
                "lift" => new TranslateTransform(0, -2),
                "fade" => new ScaleTransform(1, 1),
                _ => new ScaleTransform(1.04, 1.04)
            };

            if (mode == "fade")
                control.Opacity = 0.82;
        };

        control.PointerExited += (_, _) =>
        {
            control.RenderTransform = new ScaleTransform(1, 1);
            control.Opacity = 1;
        };
    }

    private static void SetBackground(Control control, IBrush? brush)
    {
        switch (control)
        {
            case Button button:
                button.Background = brush;
                break;
            case Border border:
                border.Background = brush;
                break;
            case Panel panel:
                panel.Background = brush;
                break;
        }
    }

    private static bool IsNamed(Control control, string name)
    {
        return string.Equals(control.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureLauncherTitle(Control root, List<Control> controls)
    {
        var titleText = controls.OfType<TextBlock>().FirstOrDefault(c =>
            IsNamed(c, "HeaderTitle") ||
            IsNamed(c, "LauncherTitle") ||
            IsNamed(c, "AetherLauncherTitle"));

        if (titleText != null)
        {
            if (string.IsNullOrWhiteSpace(titleText.Text))
                titleText.Text = "Aether Launcher";
            return;
        }

        var titleContent = controls.OfType<ContentControl>().FirstOrDefault(c =>
            IsNamed(c, "HeaderTitle") ||
            IsNamed(c, "LauncherTitle") ||
            IsNamed(c, "AetherLauncherTitle"));

        if (titleContent != null)
        {
            if (titleContent.Content == null || string.IsNullOrWhiteSpace(titleContent.Content.ToString()))
                titleContent.Content = "Aether Launcher";
            return;
        }

        var host = FindControl<Panel>(controls, "HeaderSection")
            ?? FindControl<Panel>(controls, "HeaderLabels")
            ?? root as Panel;

        if (host == null && root is ContentControl contentControl && contentControl.Content is Panel panel)
            host = panel;

        host?.Children.Insert(0, new TextBlock
        {
            Name = "HeaderTitle",
            Text = "Aether Launcher",
            Foreground = Brushes.White,
            FontSize = 24,
            FontWeight = FontWeight.Bold
        });
    }

    private static void InitializeComboBoxItems(List<Control> controls, MainWindow form, Type formType)
    {
        SetComboItems(controls, form, formType, "cbVersion", "_versionItems");
        SetComboItems(controls, form, formType, "instanceVersionCombo", "_versionItems");
        SetComboItems(controls, form, formType, "minecraftVersion", "VersionCategoryOptions");
        SetComboItems(controls, form, formType, "instanceCategoryCombo", "VersionCategoryOptions");
        SetComboItems(controls, form, formType, "profileLoaderCombo", "ProfileLoaderOptions");
        SetComboItems(controls, form, formType, "modrinthProjectTypeCombo", "ProjectTypeOptions");
        SetComboItems(controls, form, formType, "modrinthLoaderCombo", "LoaderOptions");
        SetComboItems(controls, form, formType, "modrinthSourceCombo", "SourceOptions");

        SetSelectedItem(controls, "minecraftVersion", "Versions");
        SetSelectedItem(controls, "instanceCategoryCombo", "Versions");
        SetSelectedItem(controls, "modrinthProjectTypeCombo", "Mod");
        SetSelectedItem(controls, "modrinthLoaderCombo", "Any");
        SetSelectedItem(controls, "modrinthSourceCombo", "Modrinth");
    }

    private static void InitializeListItems(List<Control> controls, MainWindow form, Type formType)
    {
        SetListItems(controls, form, formType, "profileListBox", "_profileItems");
        SetListItems(controls, form, formType, "modrinthResultsListBox", "_searchResults");
    }

    private static void WireTextBoxes(List<Control> controls, MainWindow form)
    {
        WireTextChanged(controls, "usernameInput", form, "UsernameInput_TextChanged");
        WireEnterKey(controls, "modrinthSearchInput", form, "SearchModrinthAsync");
        WireEnterKey(controls, "_quickModSearch", form, "QuickModSearchAsync");
    }

    private static void WireComboBoxes(List<Control> controls, MainWindow form)
    {
        WireSelectionChanged(controls, "cbVersion", form, "CbVersion_SelectionChanged");
        WireSelectionChanged(controls, "minecraftVersion", form, "ListVersionsAsync", combo => combo.SelectedItem?.ToString() ?? "Versions");
        WireSelectionChanged(controls, "instanceCategoryCombo", form, "ListVersionsAsync", combo => combo.SelectedItem?.ToString() ?? "Versions");
    }

    private static void WireListBoxes(List<Control> controls, MainWindow form)
    {
        WireSelectionChanged<ListBox>(controls, "profileListBox", form, "ProfileListBox_SelectionChanged");
        WireSelectionChanged<ListBox>(controls, "modrinthResultsListBox", form, "UpdateSelectedProjectDetails");
    }

    private static void WireButtons(List<Control> controls, MainWindow form)
    {
        WireClick(controls, "accountsNavButton", form, "ShowAccountsOverlay");
        WireClick(controls, "downloadVersionButton", form, "DownloadSelectedVersionAsync");
        WireClick(controls, "createProfileButton", form, "CreateProfileAsync");
        WireClick(controls, "renameProfileButton", form, "RenameSelectedProfileAsync");
        WireClick(controls, "btnStart", form, "LaunchAsync");
        WireClick(controls, "modrinthSearchButton", form, "SearchModrinthAsync");
        WireClick(controls, "installSelectedButton", form, "InstallSelectedAsync");
        WireClick(controls, "importMrpackButton", form, "ImportMrpackAsync");
        WireClick(controls, "clearProfileButton", form, "DeleteSelectedProfileAsync");
        WireClick(controls, "_quickInstallButton", form, "QuickInstallInstanceAsync");
        WireClick(controls, "_quickModSearchButton", form, "QuickModSearchAsync");
        WireClick(controls, "ImportLayoutButton", form, "ImportLayoutAsync");
        WireClick(controls, "ResetLayoutButton", form, "ResetLayoutAsync");
        WireClick(controls, "ChangeBaseDirectoryButton", form, "ChangeBaseDirectoryAsync");
    }

    private static void SetComboItems(List<Control> controls, MainWindow form, Type formType, string controlName, string sourceFieldName)
    {
        var combo = FindControl<ComboBox>(controls, controlName);
        if (combo == null) return;

        if (GetFieldValue(form, formType, sourceFieldName) is IEnumerable items)
            combo.ItemsSource = items;
    }

    private static void SetListItems(List<Control> controls, MainWindow form, Type formType, string controlName, string sourceFieldName)
    {
        var listBox = FindControl<ListBox>(controls, controlName);
        if (listBox == null) return;

        if (GetFieldValue(form, formType, sourceFieldName) is IEnumerable items)
            listBox.ItemsSource = items;
    }

    private static void SetSelectedItem(List<Control> controls, string controlName, string value)
    {
        var combo = FindControl<ComboBox>(controls, controlName);
        if (combo != null && combo.SelectedItem == null)
            combo.SelectedItem = value;
    }

    private static object? GetFieldValue(MainWindow form, Type formType, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var field = formType.GetField(fieldName, flags);
        if (field == null) return null;

        return field.GetValue(field.IsStatic ? null : form);
    }

    private static void WireTextChanged(List<Control> controls, string name, MainWindow form, string methodName)
    {
        var textBox = FindControl<TextBox>(controls, name);
        if (textBox == null) return;

        textBox.TextChanged += (_, _) => Invoke(form, methodName);
    }

    private static void WireEnterKey(List<Control> controls, string name, MainWindow form, string methodName)
    {
        var textBox = FindControl<TextBox>(controls, name);
        if (textBox == null) return;

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                Invoke(form, methodName);
        };
    }

    private static void WireSelectionChanged(List<Control> controls, string name, MainWindow form, string methodName, Func<ComboBox, object?>? argFactory = null)
    {
        var comboBox = FindControl<ComboBox>(controls, name);
        if (comboBox == null) return;

        comboBox.SelectionChanged += (_, _) =>
        {
            if (argFactory == null)
                Invoke(form, methodName);
            else
                Invoke(form, methodName, argFactory(comboBox));
        };
    }

    private static void WireSelectionChanged<T>(List<Control> controls, string name, MainWindow form, string methodName) where T : ListBox
    {
        var selector = FindControl<T>(controls, name);
        if (selector == null) return;

        selector.SelectionChanged += (_, _) => Invoke(form, methodName);
    }

    private static void WireClick(List<Control> controls, string name, MainWindow form, string methodName)
    {
        var button = FindControl<Button>(controls, name);
        if (button == null) return;

        button.Click += (_, _) => Invoke(form, methodName);
        WireHoverMotion(button);
    }

    private static async void Invoke(MainWindow form, string methodName, params object?[] args)
    {
        try
        {
            var method = FindMethod(form.GetType(), methodName, args.Length);
            if (method == null)
            {
                LauncherLog.Info($"[UIBinder] No method named '{methodName}' found for AXAML control rule.");
                return;
            }

            var result = method.Invoke(form, BuildMethodArgs(method, args));
            if (result is Task task)
                await task;
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"[UIBinder] Failed to invoke '{methodName}'.", ex);
        }
    }

    private static MethodInfo? FindMethod(Type type, string methodName, int suppliedArgCount)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        return type.GetMethods(flags)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => Math.Abs(m.GetParameters().Length - suppliedArgCount))
            .FirstOrDefault();
    }

    private static object?[] BuildMethodArgs(MethodInfo method, object?[] suppliedArgs)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return [];

        var finalArgs = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i < suppliedArgs.Length)
                finalArgs[i] = suppliedArgs[i];
            else if (parameters[i].HasDefaultValue)
                finalArgs[i] = parameters[i].DefaultValue;
            else
                finalArgs[i] = null;
        }

        return finalArgs;
    }

    private static T? FindControl<T>(List<Control> controls, string name) where T : Control
    {
        return controls.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) as T;
    }

    private static Panel? FindSectionHost(List<Control> controls, string sectionName)
    {
        var candidates = new[]
        {
            sectionName,
            $"{sectionName}Host",
            $"{sectionName}Container"
        };

        return controls.FirstOrDefault(c =>
            c is Panel &&
            !string.IsNullOrEmpty(c.Name) &&
            candidates.Contains(c.Name, StringComparer.OrdinalIgnoreCase)) as Panel;
    }

    private static void Rehost(Panel host, Control section)
    {
        if (ReferenceEquals(section.Parent, host))
            return;

        Detach(section);
        if (!host.Children.Contains(section))
            host.Children.Add(section);
    }

    private static void Detach(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
            case Border border when ReferenceEquals(border.Child, control):
                border.Child = null;
                break;
        }
    }

    private static string NormalizeSectionName(string sectionName)
    {
        return sectionName switch
        {
            "mods" => "modrinth",
            "instances" => "profiles",
            _ => sectionName
        };
    }

    private static void WireEvent<T>(List<Control> controls, string name, Action<T> action) where T : Control
    {
        var control = controls.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) as T;
        if (control != null)
            action(control);
    }
    
    private static void WireHoverMotion(Button b)
    {
        b.PointerEntered += (s, e) => {
            b.Opacity = 0.8;
            b.RenderTransform = new Avalonia.Media.ScaleTransform(1.05, 1.05);
        };
        b.PointerExited += (s, e) => {
            b.Opacity = 1.0;
            b.RenderTransform = new Avalonia.Media.ScaleTransform(1.0, 1.0);
        };
        b.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.DoubleTransition { Property = Control.OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
            new Avalonia.Animation.TransformOperationsTransition { Property = Control.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(200) }
        };
    }
}
