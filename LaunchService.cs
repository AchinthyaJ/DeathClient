using System;
using System.IO;
using System.Text.Json;

namespace OfflineMinecraftLauncher;

public class LaunchConfig
{
    public string username { get; set; } = string.Empty;
    public string skin { get; set; } = string.Empty;
    public string cape { get; set; } = string.Empty;
}

public class LaunchService
{
    public string PrepareInstanceProfile(string instanceDirectory, UserProfile profile, Uri? skinServerBaseUri = null)
    {
        // Write to config/death-client/ which is where the Fabric mod's ConfigLoader reads
        var targetDir = Path.Combine(instanceDirectory, "config", "death-client");
        Directory.CreateDirectory(targetDir);

        var configPath = Path.Combine(targetDir, "death-client.json");
        
        // Determine if skin/cape files exist
        bool hasSkin = !string.IsNullOrWhiteSpace(profile.SkinPath) && File.Exists(profile.SkinPath);
        bool hasCape = !string.IsNullOrWhiteSpace(profile.CapePath) && File.Exists(profile.CapePath);
        var skinUrl = hasSkin && skinServerBaseUri is not null
            ? new Uri(skinServerBaseUri, "v1/skins/current").ToString()
            : string.Empty;
        var capeUrl = hasCape && skinServerBaseUri is not null
            ? new Uri(skinServerBaseUri, "v1/capes/current").ToString()
            : string.Empty;

        var config = new
        {
            skinFile = "skin.png",
            capeFile = "cape.png",
            skinEnabled = hasSkin,
            capeEnabled = hasCape,
            skinUrl,
            capeUrl
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);

        return configPath;
    }
}
