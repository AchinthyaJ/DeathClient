using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;

namespace OfflineMinecraftLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => AppRuntime.SkinServer.Dispose();
            _ = StartSkinServerInBackgroundAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartSkinServerInBackgroundAsync()
    {
        try
        {
            await AppRuntime.SkinServer.StartAsync();
        }
        catch (Exception ex)
        {
            LauncherLog.Error("Failed to initialize node skin server.", ex);
        }
    }
}
