using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;

namespace OfflineMinecraftLauncher
{
    public class UIWatcher
    {
        private readonly string _axamlPath;
        private readonly Action<Control?> _onReload;
        private FileSystemWatcher? _watcher;
        private System.Threading.Timer? _debounceTimer;

        public UIWatcher(string axamlPath, Action<Control?> onReload)
        {
            _axamlPath = Path.GetFullPath(axamlPath);
            _onReload = onReload;

            var directory = Path.GetDirectoryName(_axamlPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            _watcher = new FileSystemWatcher(directory, Path.GetFileName(_axamlPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce the file change event to prevent rapid reloads
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var newRoot = UILoader.Load(_axamlPath);
                        _onReload(newRoot);
                    }
                    catch (Exception ex)
                    {
                        LauncherLog.Error($"UI hot reload failed for '{_axamlPath}'.", ex);
                    }
                });
            }, null, 500, System.Threading.Timeout.Infinite);
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
