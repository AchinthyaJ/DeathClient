using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using CmlLib.Core;

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
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            // First-run onboarding: create account before showing launcher UI.
            // We also show onboarding if no accounts exist yet.
            try
            {
                var initialPath = new MinecraftPath();
                initialPath.CreateDirs();
                var store = new UserSettingsStore(initialPath.BasePath);
                var settings = store.Load();
                var needsOnboarding = settings.IsFirstRun || settings.Accounts.Count == 0;
                desktop.MainWindow = needsOnboarding ? new FirstRunAccountWindow() : new MainWindow();
            }
            catch
            {
                desktop.MainWindow = new MainWindow();
            }
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
