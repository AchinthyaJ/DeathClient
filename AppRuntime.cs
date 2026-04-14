using System;
using System.IO;

namespace OfflineMinecraftLauncher;

internal static class AppRuntime
{
    public static string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".death-client");
    public static NodeSkinServerManager SkinServer { get; } = new();
}
