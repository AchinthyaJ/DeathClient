using System;
using System.IO;
using System.Text.Json;

namespace OfflineMinecraftLauncher;

public class UserProfile
{
    public string Username { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string SkinPath { get; set; } = string.Empty;
    public string CapePath { get; set; } = string.Empty;
}

public class ProfileService
{
    private readonly string _profilesPath;
    private static readonly JsonSerializerOptions LoadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ProfileService()
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".death-client");
        Directory.CreateDirectory(basePath);
        _profilesPath = Path.Combine(basePath, "profiles.json");
    }

    public UserProfile LoadProfile()
    {
        if (!File.Exists(_profilesPath))
            return new UserProfile();

        try
        {
            var json = File.ReadAllText(_profilesPath);
            return JsonSerializer.Deserialize<UserProfile>(json, LoadOptions) ?? new UserProfile();
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"Failed to load user profile from '{_profilesPath}'.", ex);
            return new UserProfile();
        }
    }

    public void SaveProfile(UserProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, SaveOptions);
        LauncherLog.AtomicWriteAllText(_profilesPath, json);
    }
}
