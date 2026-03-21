using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionMetadata;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace OfflineMinecraftLauncher;

public partial class LauncherForm : Form
{
    private readonly MinecraftLauncher _defaultLauncher;
    private readonly MinecraftPath _defaultMinecraftPath;
    private readonly LauncherProfileStore _profileStore;
    private readonly ModrinthClient _modrinthClient = new();
    private readonly ToolTip _characterPreviewTooltip = new();
    private readonly ToolTip _helpTooltip = new();
    private readonly string _launcherProfilesPath;

    private string _playerUuid = string.Empty;
    private LauncherProfile? _selectedProfile;
    private CancellationTokenSource? _searchCancellation;

    public LauncherForm()
    {
        _defaultMinecraftPath = new MinecraftPath();
        _defaultMinecraftPath.CreateDirs();
        _launcherProfilesPath = Path.Combine(_defaultMinecraftPath.BasePath, "launcher_profiles.json");
        _profileStore = new LauncherProfileStore(_defaultMinecraftPath.BasePath);
        _defaultLauncher = CreateLauncher(_defaultMinecraftPath);

        if (!File.Exists(_launcherProfilesPath))
            File.WriteAllText(_launcherProfilesPath, "{\"profiles\":{}}");

        InitializeComponent();
    }

    private MinecraftLauncher CreateLauncher(MinecraftPath path)
    {
        path.CreateDirs();
        var launcher = new MinecraftLauncher(path);
        launcher.FileProgressChanged += _launcher_FileProgressChanged;
        launcher.ByteProgressChanged += _launcher_ByteProgressChanged;
        return launcher;
    }

    private async void LauncherForm_Load(object sender, EventArgs e)
    {
        usernameInput.Text = Properties.Settings.Default.Username;
        cbVersion.Text = Properties.Settings.Default.Version;

        if (string.IsNullOrWhiteSpace(usernameInput.Text))
            usernameInput.Text = Environment.UserName;

        _helpTooltip.SetToolTip(characterHelpPictureBox, characterHelpPictureBox.Tag?.ToString());
        _characterPreviewTooltip.SetToolTip(characterPictureBox, characterPictureBox.Tag?.ToString());

        profileLoaderCombo.SelectedIndex = 0;
        modrinthProjectTypeCombo.SelectedIndex = 0;
        modrinthLoaderCombo.SelectedIndex = 0;
        minecraftVersion.SelectedIndex = 0;

        RefreshProfiles();
        await ListVersionsAsync();
        SyncModrinthFilters();
        UpdateCharacterPreview();
        UpdateLauncherContext();
        SetProgressState("Ready to install or launch.", 0, 0);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _modrinthClient.Dispose();
        base.OnFormClosed(e);
    }

    private async Task ListVersionsAsync(bool includeAll = false)
    {
        cbVersion.Items.Clear();

        var versions = await _defaultLauncher.GetAllVersionsAsync();
        foreach (var version in versions)
        {
            if (version.GetVersionType() == MVersionType.Release ||
                version.GetVersionType() == MVersionType.Custom ||
                includeAll)
            {
                cbVersion.Items.Add(version.Name);
            }
        }

        if (_selectedProfile is not null && !cbVersion.Items.Contains(_selectedProfile.GameVersion))
            cbVersion.Items.Insert(0, _selectedProfile.GameVersion);

        if (string.IsNullOrWhiteSpace(cbVersion.Text))
            cbVersion.Text = versions.LatestReleaseName;
    }

    private async void btnStart_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
        {
            MessageBox.Show("Enter a username before launching.", "Username required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var versionToLaunch = _selectedProfile?.VersionId ?? cbVersion.Text.Trim();
        if (string.IsNullOrWhiteSpace(versionToLaunch))
        {
            MessageBox.Show("Select a Minecraft version or profile before launching.", "Version required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_selectedProfile is not null &&
            (_selectedProfile.Loader == "forge" || _selectedProfile.Loader == "neoforge"))
        {
            MessageBox.Show("Forge and NeoForge packs can be downloaded, but launching those loaders is not implemented yet.", "Unsupported loader", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ToggleBusyState(true, "Priming the launcher...");

        try
        {
            var launcherPath = _selectedProfile is null
                ? _defaultMinecraftPath
                : new MinecraftPath(_selectedProfile.InstanceDirectory);
            var launcher = CreateLauncher(launcherPath);

            if (_selectedProfile is not null)
            {
                await EnsureProfileReadyAsync(_selectedProfile, launcher, CancellationToken.None);
                versionToLaunch = _selectedProfile.VersionId;
            }
            else
            {
                await launcher.InstallAsync(versionToLaunch);
            }

            var session = MSession.CreateOfflineSession(usernameInput.Text.Trim());
            session.UUID = _playerUuid;

            var process = await launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
            {
                Session = session
            });
            process.Start();

            Properties.Settings.Default.Username = usernameInput.Text.Trim();
            Properties.Settings.Default.Version = cbVersion.Text.Trim();
            Properties.Settings.Default.Save();
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch Minecraft.\n{ex.Message}", "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task EnsureProfileReadyAsync(LauncherProfile profile, MinecraftLauncher launcher, CancellationToken cancellationToken)
    {
        await launcher.InstallAsync(profile.GameVersion);

        if (profile.Loader == "fabric")
            await EnsureFabricProfileAsync(profile, cancellationToken);
        else if (profile.Loader == "quilt")
            throw new InvalidOperationException("Quilt profile launching is not implemented yet.");
        else if (profile.Loader == "forge" || profile.Loader == "neoforge")
            throw new InvalidOperationException($"{profile.Loader} profile launching is not implemented yet.");
    }

    private async Task EnsureFabricProfileAsync(LauncherProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.LoaderVersion))
            throw new InvalidOperationException("Fabric loader version is missing from the profile.");

        var versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
        var versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
        if (File.Exists(versionJsonPath))
            return;

        Directory.CreateDirectory(versionDirectory);
        var manifestJson = await _modrinthClient.GetStringAsync(
            $"https://meta.fabricmc.net/v2/versions/loader/{profile.GameVersion}/{profile.LoaderVersion}/profile/json",
            cancellationToken);

        var manifestDocument = JsonDocument.Parse(manifestJson);
        if (manifestDocument.RootElement.TryGetProperty("id", out var idElement))
        {
            var profileVersionId = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(profileVersionId) &&
                !string.Equals(profile.VersionId, profileVersionId, StringComparison.Ordinal))
            {
                profile.VersionId = profileVersionId;
                _profileStore.Save(profile);
                versionDirectory = Path.Combine(profile.InstanceDirectory, "versions", profile.VersionId);
                versionJsonPath = Path.Combine(versionDirectory, $"{profile.VersionId}.json");
                Directory.CreateDirectory(versionDirectory);
            }
        }

        File.WriteAllText(versionJsonPath, manifestJson);
    }

    private async void minecraftVersion_SelectedIndexChanged(object sender, EventArgs e)
    {
        await ListVersionsAsync(minecraftVersion.SelectedIndex == 1);
    }

    private void usernameInput_TextChanged(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
        {
            _playerUuid = string.Empty;
            characterPictureBox.Image = null;
            characterPictureBox.Tag = null;
            _characterPreviewTooltip.SetToolTip(characterPictureBox, null);
            btnStart.Enabled = false;
            return;
        }

        btnStart.Enabled = true;
        _playerUuid = Character.GenerateUuidFromUsername(usernameInput.Text.Trim());
        UpdateCharacterPreview();
    }

    private void cbVersion_TextChanged(object sender, EventArgs e)
    {
        UpdateCharacterPreview();
        if (_selectedProfile is null)
            SyncModrinthFilters();
    }

    private void UpdateCharacterPreview()
    {
        var selectedVersion = _selectedProfile?.GameVersion ?? cbVersion.Text;
        var resourceName = Character.GetCharacterResourceNameFromUuidAndGameVersion(_playerUuid, selectedVersion);
        if (string.IsNullOrWhiteSpace(resourceName))
            return;

        if (Properties.Resources.ResourceManager.GetObject(resourceName) is Bitmap bitmap)
        {
            characterPictureBox.Image = bitmap;
            characterPictureBox.Tag = resourceName.Replace("_", " ");
            _characterPreviewTooltip.SetToolTip(characterPictureBox, characterPictureBox.Tag?.ToString());
        }
    }

    private void _launcher_FileProgressChanged(object? sender, InstallerProgressChangedEventArgs args)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => _launcher_FileProgressChanged(sender, args)));
            return;
        }

        pbFiles.Maximum = Math.Max(1, args.TotalTasks);
        pbFiles.Value = Math.Min(args.ProgressedTasks, pbFiles.Maximum);
        statusLabel.Text = $"Installing {args.Name}";
        installDetailsLabel.Text = $"{args.ProgressedTasks} / {args.TotalTasks} files";
    }

    private void _launcher_ByteProgressChanged(object? sender, ByteProgress args)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => _launcher_ByteProgressChanged(sender, args)));
            return;
        }

        pbProgress.Maximum = 100;
        pbProgress.Value = args.TotalBytes <= 0
            ? 0
            : (int)Math.Min(100, args.ProgressedBytes * 100 / args.TotalBytes);
    }

    private void RefreshProfiles(LauncherProfile? selectProfile = null)
    {
        var profiles = _profileStore.LoadProfiles();

        profileListBox.BeginUpdate();
        profileListBox.Items.Clear();
        foreach (var profile in profiles)
            profileListBox.Items.Add(profile);
        profileListBox.EndUpdate();

        if (profiles.Count == 0)
        {
            profileListBox.SelectedIndex = -1;
            _selectedProfile = null;
            UpdateLauncherContext();
            return;
        }

        LauncherProfile? profileToSelect = null;
        if (selectProfile is not null)
        {
            profileToSelect = profiles.FirstOrDefault(profile => string.Equals(profile.InstanceDirectory, selectProfile.InstanceDirectory, StringComparison.Ordinal));
        }
        else if (_selectedProfile is not null)
        {
            profileToSelect = profiles.FirstOrDefault(profile => string.Equals(profile.InstanceDirectory, _selectedProfile.InstanceDirectory, StringComparison.Ordinal));
        }

        if (profileToSelect is null)
        {
            profileListBox.SelectedIndex = -1;
            _selectedProfile = null;
            UpdateLauncherContext();
            return;
        }

        profileListBox.SelectedItem = profileToSelect;
    }

    private void profileListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        _selectedProfile = profileListBox.SelectedItem as LauncherProfile;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
    }

    private void profileListBox_DoubleClick(object sender, EventArgs e)
    {
        profileListBox.ClearSelected();
        _selectedProfile = null;
        UpdateLauncherContext();
        SyncModrinthFilters();
        UpdateCharacterPreview();
    }

    private void UpdateLauncherContext()
    {
        if (_selectedProfile is null)
        {
            activeProfileBadge.Text = "Quick Launch";
            activeProfileBadge.BackColor = Color.FromArgb(255, 120, 48);
            activeContextLabel.Text = $"Launching default .minecraft using {cbVersion.Text.Trim()}";
            installModeLabel.Text = "Select a profile below to install mods into an isolated instance.";
            btnStart.Text = "Launch";
            return;
        }

        activeProfileBadge.Text = _selectedProfile.Name;
        activeProfileBadge.BackColor = Color.FromArgb(62, 214, 180);
        activeContextLabel.Text = $"Active profile: {_selectedProfile.LoaderDisplay}";
        installModeLabel.Text = $"Mods and packs install into {_selectedProfile.Name}.";
        btnStart.Text = $"Launch {_selectedProfile.Name}";
    }

    private void SyncModrinthFilters()
    {
        modrinthVersionInput.Text = _selectedProfile?.GameVersion ?? cbVersion.Text.Trim();

        var loader = _selectedProfile?.Loader ?? "vanilla";
        for (int i = 0; i < modrinthLoaderCombo.Items.Count; i++)
        {
            if (string.Equals(modrinthLoaderCombo.Items[i]?.ToString(), loader, StringComparison.OrdinalIgnoreCase))
            {
                modrinthLoaderCombo.SelectedIndex = i;
                return;
            }
        }

        modrinthLoaderCombo.SelectedIndex = 0;
    }

    private async void createProfileButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(profileNameInput.Text))
        {
            MessageBox.Show("Give the profile a name before creating it.", "Profile name required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(cbVersion.Text))
        {
            MessageBox.Show("Select a Minecraft version before creating a profile.", "Version required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var loader = profileLoaderCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "vanilla";
        string? loaderVersion = null;

        try
        {
            ToggleBusyState(true, "Creating profile...");

            if (loader == "fabric")
                loaderVersion = await ResolveLatestFabricVersionAsync(cbVersion.Text.Trim(), CancellationToken.None);

            var profile = _profileStore.CreateProfile(profileNameInput.Text.Trim(), cbVersion.Text.Trim(), loader, loaderVersion);
            if (loader == "fabric")
                await EnsureFabricProfileAsync(profile, CancellationToken.None);

            RefreshProfiles(profile);
            UpdateLauncherContext();
            SetProgressState($"Profile {profile.Name} is ready.", 0, 0);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create profile.\n{ex.Message}", "Profile error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task<string> ResolveLatestFabricVersionAsync(string gameVersion, CancellationToken cancellationToken)
    {
        var payload = await _modrinthClient.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}", cancellationToken);
        using var json = JsonDocument.Parse(payload);
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("loader", out var loaderElement) &&
                loaderElement.TryGetProperty("version", out var versionElement))
            {
                var version = versionElement.GetString();
                if (!string.IsNullOrWhiteSpace(version))
                    return version;
            }
        }

        throw new InvalidOperationException($"No Fabric loader build was found for Minecraft {gameVersion}.");
    }

    private async void modrinthSearchButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(modrinthSearchInput.Text))
        {
            MessageBox.Show("Enter a search term to browse Modrinth.", "Search required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            ToggleBusyState(true, "Searching Modrinth...");

            var projectType = modrinthProjectTypeCombo.SelectedItem?.ToString()?.ToLowerInvariant() ?? "mod";
            var gameVersion = string.IsNullOrWhiteSpace(modrinthVersionInput.Text) ? null : modrinthVersionInput.Text.Trim();
            var loader = NormalizeLoaderFilter();
            var results = await _modrinthClient.SearchProjectsAsync(
                modrinthSearchInput.Text,
                projectType,
                gameVersion,
                loader,
                _searchCancellation.Token);

            BindSearchResults(results);
            SetProgressState($"Found {results.Count} {projectType} results.", 0, 0);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Modrinth search failed.\n{ex.Message}", "Search failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private string? NormalizeLoaderFilter()
    {
        var selected = modrinthLoaderCombo.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected) || string.Equals(selected, "Any", StringComparison.OrdinalIgnoreCase))
            return null;

        return selected.ToLowerInvariant();
    }

    private void BindSearchResults(IReadOnlyList<ModrinthProject> results)
    {
        modrinthResultsListView.BeginUpdate();
        modrinthResultsListView.Items.Clear();
        foreach (var project in results)
        {
            var item = new ListViewItem(project.Title);
            item.SubItems.Add(project.ProjectType);
            item.SubItems.Add(project.Downloads.ToString("N0"));
            item.SubItems.Add(project.Author);
            item.Tag = project;
            modrinthResultsListView.Items.Add(item);
        }
        modrinthResultsListView.EndUpdate();

        if (modrinthResultsListView.Items.Count > 0)
            modrinthResultsListView.Items[0].Selected = true;
        else
        {
            modrinthDetailsBox.Text = "No matching Modrinth projects found for the current filters.";
            installSelectedButton.Enabled = false;
        }
    }

    private void modrinthResultsListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (modrinthResultsListView.SelectedItems.Count == 0)
        {
            modrinthDetailsBox.Text = "Search Modrinth to browse mods and modpacks.";
            installSelectedButton.Enabled = false;
            return;
        }

        installSelectedButton.Enabled = true;
        var project = (ModrinthProject)modrinthResultsListView.SelectedItems[0].Tag;
        modrinthDetailsBox.Text =
            $"{project.Title}\n" +
            $"Type: {project.ProjectType}\n" +
            $"Author: {project.Author}\n" +
            $"Downloads: {project.Downloads:N0}\n" +
            $"Followers: {project.Follows:N0}\n" +
            $"Categories: {string.Join(", ", project.Categories)}\n\n" +
            $"{project.Description}";
        installSelectedButton.Text = project.ProjectType == "modpack" ? "Install Selected Pack" : "Install Selected Mod";
    }

    private async void installSelectedButton_Click(object sender, EventArgs e)
    {
        if (modrinthResultsListView.SelectedItems.Count == 0)
            return;

        var project = (ModrinthProject)modrinthResultsListView.SelectedItems[0].Tag;
        try
        {
            ToggleBusyState(true, $"Installing {project.Title}...");

            if (project.ProjectType == "modpack")
                await InstallModpackFromProjectAsync(project, CancellationToken.None);
            else
                await InstallSelectedModAsync(project, CancellationToken.None);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Install failed.\n{ex.Message}", "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task InstallSelectedModAsync(ModrinthProject project, CancellationToken cancellationToken)
    {
        if (_selectedProfile is null)
        {
            MessageBox.Show("Create or select a profile before installing mods.", "Profile required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_selectedProfile.Loader == "vanilla")
        {
            var result = MessageBox.Show(
                "This profile is vanilla. Most Modrinth mods need Fabric, Quilt, Forge, or NeoForge. Continue anyway?",
                "Vanilla profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
        }

        var versions = await _modrinthClient.GetProjectVersionsAsync(project.ProjectId, _selectedProfile.GameVersion, _selectedProfile.Loader, cancellationToken);
        var version = versions.FirstOrDefault(HasPrimaryFile) ?? versions.FirstOrDefault();
        if (version is null)
            throw new InvalidOperationException($"No compatible version was found for {_selectedProfile.LoaderDisplay}.");

        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        installed.Add(project.ProjectId);
        await InstallModVersionAsync(_selectedProfile, version, installed, cancellationToken);
        SetProgressState($"Installed {project.Title} into {_selectedProfile.Name}.", 0, 0);
    }

    private static bool HasPrimaryFile(ModrinthProjectVersion version) =>
        version.Files.Any(file => file.Primary && file.Filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private async Task InstallModVersionAsync(LauncherProfile profile, ModrinthProjectVersion version, HashSet<string> installedProjectIds, CancellationToken cancellationToken)
    {
        foreach (var dependency in version.Dependencies.Where(d => d.DependencyType == "required" && !string.IsNullOrWhiteSpace(d.ProjectId)))
        {
            if (!installedProjectIds.Add(dependency.ProjectId!))
                continue;

            var dependencyVersions = await _modrinthClient.GetProjectVersionsAsync(dependency.ProjectId!, profile.GameVersion, profile.Loader, cancellationToken);
            var dependencyVersion = dependencyVersions.FirstOrDefault(HasPrimaryFile) ?? dependencyVersions.FirstOrDefault();
            if (dependencyVersion is not null)
                await InstallModVersionAsync(profile, dependencyVersion, installedProjectIds, cancellationToken);
        }

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException($"Version {version.VersionNumber} did not include a downloadable file.");

        Directory.CreateDirectory(profile.ModsDirectory);
        var destinationPath = Path.Combine(profile.ModsDirectory, file.Filename);
        await _modrinthClient.DownloadFileAsync(file.Url, destinationPath, CreateDownloadProgress(file.Filename), cancellationToken);
        await VerifyFileHashAsync(destinationPath, file.Hashes);
        _profileStore.Save(profile);
    }

    private async Task VerifyFileHashAsync(string filePath, IReadOnlyDictionary<string, string> hashes)
    {
        if (!hashes.TryGetValue("sha1", out var expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
            return;

        await using var file = File.OpenRead(filePath);
        var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(file)).ToLowerInvariant();
        if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Hash mismatch detected for {Path.GetFileName(filePath)}.");
    }

    private async Task InstallModpackFromProjectAsync(ModrinthProject project, CancellationToken cancellationToken)
    {
        var gameVersion = string.IsNullOrWhiteSpace(modrinthVersionInput.Text) ? null : modrinthVersionInput.Text.Trim();
        var loader = NormalizeLoaderFilter();
        var versions = await _modrinthClient.GetProjectVersionsAsync(project.ProjectId, gameVersion, loader, cancellationToken);
        var version = versions.FirstOrDefault(v => v.Files.Any(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)))
            ?? versions.FirstOrDefault();
        if (version is null)
            throw new InvalidOperationException("No compatible modpack build was found.");

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException("The selected modpack version has no downloadable file.");

        var tempMrpack = Path.Combine(Path.GetTempPath(), $"{project.Slug}-{version.VersionNumber}.mrpack");
        await _modrinthClient.DownloadFileAsync(file.Url, tempMrpack, CreateDownloadProgress(file.Filename), cancellationToken);
        await InstallMrpackAsync(tempMrpack, project, cancellationToken);
    }

    private async void importMrpackButton_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Modrinth Modpack (*.mrpack)|*.mrpack",
            Multiselect = false,
            Title = "Import Modrinth modpack"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            ToggleBusyState(true, $"Importing {Path.GetFileName(dialog.FileName)}...");
            await InstallMrpackAsync(dialog.FileName, null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Modpack import failed.\n{ex.Message}", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task InstallMrpackAsync(string mrpackPath, ModrinthProject? sourceProject, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(mrpackPath);
        var indexEntry = archive.GetEntry("modrinth.index.json")
            ?? throw new InvalidOperationException("The pack is missing modrinth.index.json.");

        await using var indexStream = indexEntry.Open();
        var index = await JsonSerializer.DeserializeAsync<MrPackIndex>(indexStream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to read the modpack manifest.");

        if (!string.Equals(index.Game, "minecraft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported pack game: {index.Game}.");

        var gameVersion = index.Dependencies.TryGetValue("minecraft", out var minecraftVersion)
            ? minecraftVersion
            : throw new InvalidOperationException("The modpack does not specify a Minecraft version.");

        var loader = "vanilla";
        string? loaderVersion = null;

        foreach (var candidate in new[] { "fabric", "quilt", "forge", "neoforge" })
        {
            if (index.Dependencies.TryGetValue(candidate, out var candidateVersion))
            {
                loader = candidate;
                loaderVersion = candidateVersion;
                break;
            }
        }

        var profileName = string.IsNullOrWhiteSpace(index.Name)
            ? sourceProject?.Title ?? Path.GetFileNameWithoutExtension(mrpackPath)
            : index.Name;
        var profile = _profileStore.CreateProfile(profileName, gameVersion, loader, loaderVersion, sourceProject?.Slug);

        pbFiles.Maximum = Math.Max(1, index.Files.Count);
        pbFiles.Value = 0;

        int completedFiles = 0;
        foreach (var file in index.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(file.Env?.Client, "unsupported", StringComparison.OrdinalIgnoreCase))
                continue;

            var downloadUrl = file.Downloads.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(downloadUrl))
                continue;

            var destinationPath = GetSafeDestinationPath(profile.InstanceDirectory, file.Path);
            await _modrinthClient.DownloadFileAsync(downloadUrl, destinationPath, CreateDownloadProgress(file.Path), cancellationToken);
            await VerifyFileHashAsync(destinationPath, file.Hashes);

            completedFiles++;
            pbFiles.Value = Math.Min(pbFiles.Maximum, completedFiles);
            installDetailsLabel.Text = $"{completedFiles} / {index.Files.Count} pack files";
        }

        ExtractOverrideEntries(archive, "overrides/", profile.InstanceDirectory);
        ExtractOverrideEntries(archive, "client-overrides/", profile.InstanceDirectory);

        if (loader == "fabric")
            await EnsureFabricProfileAsync(profile, cancellationToken);

        _profileStore.Save(profile);
        RefreshProfiles(profile);
        SetProgressState($"Installed modpack {profile.Name}.", 0, 0);

        if (loader == "forge" || loader == "neoforge" || loader == "quilt")
        {
            MessageBox.Show(
                $"{profile.Name} was imported, but launching {loader} packs is not implemented yet.",
                "Pack imported",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private static void ExtractOverrideEntries(ZipArchive archive, string prefix, string destinationRoot)
    {
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var relativePath = entry.FullName[prefix.Length..];
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            var destinationPath = GetSafeDestinationPath(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                continue;

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static string GetSafeDestinationPath(string root, string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe path detected: {relativePath}");

        return fullPath;
    }

    private Progress<(long BytesRead, long? TotalBytes)> CreateDownloadProgress(string fileName)
    {
        return new Progress<(long BytesRead, long? TotalBytes)>(progress =>
        {
            statusLabel.Text = $"Downloading {Path.GetFileName(fileName)}";
            if (progress.TotalBytes is long totalBytes && totalBytes > 0)
            {
                pbProgress.Value = (int)Math.Min(100, progress.BytesRead * 100 / totalBytes);
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} / {FormatBytes(totalBytes)}";
            }
            else
            {
                pbProgress.Value = 0;
                installDetailsLabel.Text = $"{FormatBytes(progress.BytesRead)} downloaded";
            }
        });
    }

    private void ToggleBusyState(bool isBusy, string statusText)
    {
        btnStart.Enabled = !isBusy && !string.IsNullOrWhiteSpace(usernameInput.Text);
        createProfileButton.Enabled = !isBusy;
        modrinthSearchButton.Enabled = !isBusy;
        installSelectedButton.Enabled = !isBusy && modrinthResultsListView.SelectedItems.Count > 0;
        importMrpackButton.Enabled = !isBusy;
        statusLabel.Text = statusText;
        if (!isBusy)
            pbProgress.Value = 0;
    }

    private void SetProgressState(string statusText, int fileProgress, int byteProgress)
    {
        statusLabel.Text = statusText;
        installDetailsLabel.Text = _selectedProfile?.LoaderDisplay ?? cbVersion.Text.Trim();
        pbFiles.Value = Math.Clamp(fileProgress, 0, pbFiles.Maximum);
        pbProgress.Value = Math.Clamp(byteProgress, 0, pbProgress.Maximum);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.#} {sizes[order]}";
    }
}
