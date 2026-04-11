using System.Text.Json;

namespace OfflineMinecraftLauncher;

internal sealed class UserSettingsStore
{
    public static UserSettingsStore? Instance { get; private set; }
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public UserSettingsStore(string launcherBasePath)
    {
        Instance = this;
        var settingsDirectory = Path.Combine(launcherBasePath, "death-client");
        Directory.CreateDirectory(settingsDirectory);
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public UserSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new UserSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            LauncherLog.Error($"Failed to load settings from '{_settingsPath}'.", ex);
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        LauncherLog.AtomicWriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
    }
}

internal sealed class UserSettings
{
    public string Username { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string CustomSkinPath { get; set; } = string.Empty;
    public string CustomCapePath { get; set; } = string.Empty;
    public bool EnableFancyMenu { get; set; } = false;
    public bool OfflineMode { get; set; } = true;
    public string ClientLayout { get; set; } = string.Empty; // Store layout JSON or preferences
    public string AccentColor { get; set; } = "#6E5BFF";
    public List<string> SectionOrder { get; set; } = ["Hero", "Stats", "Actions"];
    public string LastSelectedProfilePath { get; set; } = string.Empty;
    public string BaseMinecraftPath { get; set; } = string.Empty;
    public string SelectedAccountId { get; set; } = string.Empty;
    public string MicrosoftClientId { get; set; } = string.Empty;
    public List<LauncherAccount> Accounts { get; set; } = [];

    // New Launch Settings
    public int MaxRamMb { get; set; } = 2048;
    public string JvmArgs { get; set; } = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions -Dsun.stdout.encoding=UTF-8";
    public int WindowWidth { get; set; } = 854;
    public int WindowHeight { get; set; } = 480;
    public bool SidebarOnRight { get; set; } = false;
    public bool PerformanceMode { get; set; } = false;
}
