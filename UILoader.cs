using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.IO;

namespace OfflineMinecraftLauncher;

public static class UILoader
{
    public static Control? Load(string axamlPath)
    {
        try
        {
            if (!File.Exists(axamlPath))
                return null;

            var xaml = File.ReadAllText(axamlPath);
            if (string.IsNullOrWhiteSpace(xaml))
            {
                // LauncherLog.Warn($"Skipping runtime XAML load because '{axamlPath}' is empty.");
                return null;
            }

            var baseUri = new Uri(Path.GetFullPath(axamlPath));
            var root = (Control)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, typeof(UILoader).Assembly, null, baseUri);
            return Unwrap(root);
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"Failed to load XAML from '{axamlPath}'.", ex);
            return null;
        }
    }

    private static Control? Unwrap(Control? root)
    {
        if (root == null) return null;
        if (root is Window window)
        {
            var content = window.Content as Control;
            window.Content = null;
            return content;
        }
        return root;
    }

}
