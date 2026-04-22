using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OfflineMinecraftLauncher;

public static class UILoader
{
    private static readonly HashSet<string> SupportedLayoutProperties = BuildSupportedLayoutProperties();

    public static Control? Load(string axamlPath)
    {
        return LoadInternal(axamlPath, unwrap: true);
    }

    public static Control? LoadRaw(string axamlPath)
    {
        return LoadInternal(axamlPath, unwrap: false);
    }

    private static Control? LoadInternal(string axamlPath, bool unwrap)
    {
        try
        {
            if (!File.Exists(axamlPath))
                return null;

            var xaml = NormalizeLayoutProperties(File.ReadAllText(axamlPath));
            if (string.IsNullOrWhiteSpace(xaml))
            {
                // LauncherLog.Warn($"Skipping runtime XAML load because '{axamlPath}' is empty.");
                return null;
            }

            var baseUri = new Uri(Path.GetFullPath(axamlPath));
            xaml = RemoveDuplicateAttributes(xaml);
            var root = (Control)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, typeof(UILoader).Assembly, null, baseUri);
            return unwrap ? Unwrap(root) : root;
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"[Layout] XAML loader failed for '{axamlPath}': {ex.Message}");
            return null;
        }
    }

    private static string RemoveDuplicateAttributes(string xaml)
    {
        try
        {
            var document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var changed = false;

            foreach (var element in document.Root?.DescendantsAndSelf() ?? Enumerable.Empty<XElement>())
            {
                var seen = new HashSet<XName>();
                var attributes = element.Attributes().ToList();
                for (var i = attributes.Count - 1; i >= 0; i--)
                {
                    var attribute = attributes[i];
                    if (attribute.IsNamespaceDeclaration)
                        continue;

                    if (!seen.Add(attribute.Name))
                    {
                        attribute.Remove();
                        changed = true;
                    }
                }
            }

            return changed ? document.ToString(SaveOptions.DisableFormatting) : xaml;
        }
        catch
        {
            return xaml;
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
    private static string NormalizeLayoutProperties(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
            return xaml;

        // Strip C-style comments that users might accidentally put inside tags
        // Replace with a space to preserve attribute separation
        xaml = Regex.Replace(xaml, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        // Strip properties known to cause resolution failures in the current Avalonia version.
        // We use targeted regexes to avoid stripping valid properties (like StackPanel.Spacing).
        
        // Scrub LetterSpacing, BoxShadow, and Spacing from incompatible tags
        // Replacement with a space preserves attribute separation.
        xaml = Regex.Replace(xaml, @"(<(?:Label|Button|Control)\s+[^>]*?)LetterSpacing\s*=\s*(?:'[^']*'|""[^""]*"")", "$1 ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        xaml = Regex.Replace(xaml, @"(<(?:Button|Control)\s+[^>]*?)BoxShadow\s*=\s*(?:'[^']*'|""[^""]*"")", "$1 ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        xaml = Regex.Replace(xaml, @"(<Grid\s+[^>]*?)Spacing\s*=\s*(?:'[^']*'|""[^""]*"")", "$1 ", RegexOptions.IgnoreCase | RegexOptions.Singleline);



        xaml = EnsureLayoutNamespace(xaml);

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["window_width"] = "dc:LayoutProperties.WindowWidth",
            ["window_height"] = "dc:LayoutProperties.WindowHeight",
            ["window_min_width"] = "dc:LayoutProperties.WindowMinWidth",
            ["window_min_height"] = "dc:LayoutProperties.WindowMinHeight",
            ["window_radius"] = "dc:LayoutProperties.WindowRadius",
            ["window_shape"] = "dc:LayoutProperties.WindowShape",
            ["hover_animation"] = "dc:LayoutProperties.HoverAnimation",
            ["background_on_hover"] = "dc:LayoutProperties.BackgroundOnHover"
        };

        foreach (var alias in aliases)
            xaml = Regex.Replace(xaml, $@"(?<![\w:.-]){Regex.Escape(alias.Key)}\s*=", $"{alias.Value}=", RegexOptions.IgnoreCase);

        xaml = StripUnknownLayoutPropertiesXml(xaml);

        return xaml;
    }

    private static string StripUnknownLayoutPropertiesXml(string xaml)
    {
        try
        {
            var document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var changed = false;

            foreach (var element in document.Root?.DescendantsAndSelf() ?? Enumerable.Empty<XElement>())
            {
                var attributes = element.Attributes().ToList();
                foreach (var attribute in attributes)
                {
                    if (attribute.IsNamespaceDeclaration)
                        continue;

                    var localName = attribute.Name.LocalName;
                    if (!localName.StartsWith("LayoutProperties.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var propertyName = localName["LayoutProperties.".Length..];
                    if (SupportedLayoutProperties.Contains(propertyName))
                        continue;

                    LauncherLog.Warn($"[Layout] Stripping unknown or unregistered property from AXAML: {localName}");
                    attribute.Remove();
                    changed = true;
                }
            }

            return changed ? document.ToString(SaveOptions.DisableFormatting) : xaml;
        }
        catch (Exception ex)
        {
            LauncherLog.Warn($"[Layout] XML parsing failed for stripping, falling back to regex: {ex.Message}");
            // Fall back to regex-based stripping if XML parsing fails for any reason.
            return StripUnknownLayoutPropertiesRegex(xaml);
        }
    }

    private static string StripUnknownLayoutPropertiesRegex(string xaml)
    {
        const string layoutPropertyPattern = "\\s+(?:[A-Za-z_][\\w.-]*:)?LayoutProperties\\.(?<name>[A-Za-z_][\\w.-]*)\\s*=\\s*(?<quote>[\"'])(?<value>.*?)\\k<quote>";

        return Regex.Replace(
            xaml,
            layoutPropertyPattern,
            match =>
            {
                var propertyName = match.Groups["name"].Value;
                return SupportedLayoutProperties.Contains(propertyName) ? match.Value : string.Empty;
            },
            RegexOptions.Singleline);
    }

    private static HashSet<string> BuildSupportedLayoutProperties()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = typeof(LayoutProperties).GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var field in fields)
        {
            if (!field.Name.EndsWith("Property", StringComparison.Ordinal))
                continue;

            var name = field.Name[..^"Property".Length];
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        return names;
    }

    private static string EnsureLayoutNamespace(string xaml)
    {
        if (xaml.Contains("xmlns:dc=", StringComparison.OrdinalIgnoreCase))
            return xaml;

        var match = Regex.Match(xaml, @"<(?<tag>[A-Za-z_][\w:.-]*)(?<attrs>[^>]*)>");
        if (!match.Success)
            return xaml;

        var insertAt = match.Index + match.Length - 1;
        var ns = " xmlns:dc=\"clr-namespace:OfflineMinecraftLauncher\"";
        return xaml.Insert(insertAt, ns);
    }

    public static IEnumerable<string> GetSupportedProperties() => SupportedLayoutProperties;

    /// <summary>
    /// Scans the entire AXAML document (all XML elements) for LayoutProperties.* attributes
    /// and returns them as a flat dictionary. First occurrence wins.
    /// Much more reliable than reading attached properties from just the root control.
    /// </summary>
    public static Dictionary<string, string> ScanAllLayoutProperties(string axamlPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(axamlPath)) return result;

        try
        {
            var xml = File.ReadAllText(axamlPath);
            // Strip C-style comments before parsing
            xml = Regex.Replace(xml, @"/\*.*?\*/", " ", RegexOptions.Singleline);

            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

            foreach (var element in document.Root?.DescendantsAndSelf() ?? Enumerable.Empty<XElement>())
            {
                foreach (var attr in element.Attributes())
                {
                    if (attr.IsNamespaceDeclaration) continue;

                    var localName = attr.Name.LocalName;
                    string? propName = null;

                    if (localName.StartsWith("LayoutProperties.", StringComparison.OrdinalIgnoreCase))
                        propName = localName["LayoutProperties.".Length..];

                    if (propName == null || string.IsNullOrWhiteSpace(propName)) continue;
                    if (!result.ContainsKey(propName))
                        result[propName] = attr.Value;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Warn($"[Layout] ScanAllLayoutProperties failed: {ex.Message}");

            // Fall back to regex scanning when XML is malformed.
            // This preserves style token import even if the visual template has tag issues.
            try
            {
                var xml = File.ReadAllText(axamlPath);
                var pattern = "(?:[A-Za-z_][\\w.-]*:)?LayoutProperties\\.(?<name>[A-Za-z_][\\w.-]*)\\s*=\\s*(?<quote>[\"'])(?<value>.*?)\\k<quote>";
                foreach (Match match in Regex.Matches(xml, pattern, RegexOptions.Singleline))
                {
                    var key = match.Groups["name"].Value;
                    var value = match.Groups["value"].Value;
                    if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                        result[key] = value;
                }
            }
            catch (Exception regexEx)
            {
                LauncherLog.Warn($"[Layout] Regex fallback scan failed: {regexEx.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Walks the loaded control tree and returns all named Panel controls,
    /// keyed by name. Used for the "sections anywhere" slot system.
    /// </summary>
    public static Dictionary<string, Panel> FindNamedSlots(Control? root)
    {
        var slots = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        if (root == null) return slots;

        var queue = new Queue<Control>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is Panel panel && !string.IsNullOrWhiteSpace(panel.Name))
                slots[panel.Name] = panel;

            // Walk children
            if (current is Panel p)
                foreach (var child in p.Children.OfType<Control>())
                    queue.Enqueue(child);
            else if (current is ContentControl cc && cc.Content is Control ccChild)
                queue.Enqueue(ccChild);
            else if (current is Decorator dec && dec.Child is Control decChild)
                queue.Enqueue(decChild);
        }

        return slots;
    }
}
