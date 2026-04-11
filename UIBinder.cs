using Avalonia.Controls;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace OfflineMinecraftLauncher;

public static class UIBinder
{
    public static void Bind(Control? root, MainWindow form)
    {
        if (root == null) return;
        var descendants = root.GetVisualDescendants().OfType<Control>().ToList();

        var formType = typeof(MainWindow);
        
        // Loop through and bind all controls in the AXAML to their corresponding fields in the form
        var fields = formType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                             .Where(f => typeof(Control).IsAssignableFrom(f.FieldType));
                             
        var fieldDict = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields) fieldDict[f.Name] = f;

        foreach (var control in descendants)
        {
            if (!string.IsNullOrEmpty(control.Name) && fieldDict.TryGetValue(control.Name, out var field))
            {
                if (field.FieldType.IsAssignableFrom(control.GetType()))
                {
                    field.SetValue(form, control);
                }
            }
        }

        // --- Automatic Navigation/Tab Wiring ---
        // Buttons with Name or Tag matching these will automatically trigger tab switching
        var sectionNames = new[] { "home", "launch", "instances", "profiles", "modrinth", "mods", "performance", "settings", "layout" };
        foreach (var button in descendants.OfType<Button>())
        {
            var target = (button.Tag?.ToString() ?? button.Name ?? "").ToLower();
            if (sectionNames.Contains(target))
            {
                button.Click += (s, e) => form.SetActiveSection(target);
                WireHoverMotion(button);
            }
        }
        
        // --- Fallback Content Injection ---
        // If the AXAML provides a "MainContentArea" Panel, we inject all the core sections 
        // that were NOT provided by the custom AXAML layout herself.
        var contentArea = descendants.FirstOrDefault(c => string.Equals(c.Name, "MainContentArea", StringComparison.OrdinalIgnoreCase)) as Panel;
        if (contentArea != null)
        {
            var fallbackSections = new[] { "launchSection", "modrinthSection", "profilesSection", "performanceSection", "settingsSection", "layoutSection" };
            foreach (var sectionName in fallbackSections)
            {
                if (fieldDict.TryGetValue(sectionName, out var field))
                {
                    var sectionObj = field.GetValue(form);
                    if (sectionObj is Control section && section.Parent == null)
                    {
                        contentArea.Children.Add(section);
                    }
                }
            }
        }

        // --- Navigation Actions ---
        WireEvent<Button>(descendants, "BtnMods", (b) => b.Click += (s, e) => form.SetActiveSection("modrinth"));
        WireEvent<Button>(descendants, "BtnInstances", (b) => b.Click += (s, e) => form.SetActiveSection("profiles"));

        // --- Instance Editor ---
        WireEvent<Button>(descendants, "ClearProfileButton", (b) => b.Click += (s, e) => {
             if (fieldDict.TryGetValue("_instanceEditorOverlay", out var f) && f.GetValue(form) is Control overlay) overlay.IsVisible = false;
        });

    }

    private static void WireEvent<T>(List<Control> descendants, string name, Action<T> action) where T : Control
    {
        var control = descendants.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) as T;
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
