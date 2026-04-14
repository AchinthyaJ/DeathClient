using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OfflineMinecraftLauncher;

internal sealed class NodeSkinServerManager : IDisposable
{
    private static readonly Uri BaseUri = new("http://127.0.0.1:47135/");
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private readonly string _storageDirectory;
    private Process? _process;
    private bool _ownsProcess;

    public NodeSkinServerManager()
    {
        _storageDirectory = Path.Combine(AppRuntime.DataDirectory, "skin-server");
        Directory.CreateDirectory(_storageDirectory);
    }

    public Uri ServerBaseUri => BaseUri;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(cancellationToken))
            return;

        if (_process is { HasExited: false })
            return;

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "node-skin-server", "server.js");
        if (!File.Exists(scriptPath))
        {
            LauncherLog.Warn($"Node skin server script not found at '{scriptPath}'.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add("47135");
        startInfo.ArgumentList.Add("--storage");
        startInfo.ArgumentList.Add(_storageDirectory);

        try
        {
            _process = Process.Start(startInfo);
            _ownsProcess = _process is not null;
        }
        catch (Exception ex)
        {
            LauncherLog.Error("Failed to start node skin server process.", ex);
            return;
        }

        if (_process is null)
            return;

        _ = Task.Run(() => DrainAsync(_process.StandardOutput, cancellationToken), cancellationToken);
        _ = Task.Run(() => DrainAsync(_process.StandardError, cancellationToken), cancellationToken);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (await IsHealthyAsync(cancellationToken))
                return;

            await Task.Delay(250, cancellationToken);
        }

        LauncherLog.Warn("Node skin server did not become healthy in time.");
    }

    public void Dispose()
    {
        if (!_ownsProcess || _process is null)
            return;

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignore shutdown failures during app exit.
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _ownsProcess = false;
        }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(line))
                    LauncherLog.Info($"[SkinServer] {line}");
            }
        }
        catch
        {
            // Ignore stream closure during shutdown.
        }
    }

    private static async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(new Uri(BaseUri, "health"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
