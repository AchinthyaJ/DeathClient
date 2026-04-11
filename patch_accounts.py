import re

with open('MainWindow.cs', 'r') as f:
    code = f.read()

# Add fields
fields_patch = """    private readonly ComboBox profileLoaderCombo;
    private readonly Button accountsNavButton;
    private readonly ContextMenu accountsMenu = new();"""
code = code.replace("    private readonly ComboBox profileLoaderCombo;", fields_patch)

# Initialization part
init_patch = """        cbVersion = CreateComboBox(_versionItems);
        accountsNavButton = CreateNavButton("☻", "Accounts");
        accountsNavButton.Click += (_, _) => UpdateAndShowAccountsMenu();"""
code = code.replace("        cbVersion = CreateComboBox(_versionItems);", init_patch)

# Add methods GetActiveUsername, GetLaunchSession, UpdateActiveAccountDisplay, UpdateAndShowAccountsMenu
methods_patch = """
    private string GetActiveUsername()
    {
        var acc = _settings.Accounts?.FirstOrDefault(a => a.Id == _settings.SelectedAccountId);
        if (acc != null && !string.IsNullOrWhiteSpace(acc.Username))
            return acc.Username;
        
        return !string.IsNullOrWhiteSpace(_settings.Username) ? _settings.Username : Environment.UserName;
    }

    private MSession GetLaunchSession()
    {
        var acc = _settings.Accounts?.FirstOrDefault(a => a.Id == _settings.SelectedAccountId);
        if (acc != null && acc.Provider == "microsoft")
        {
            return new MSession
            {
                Username = acc.Username,
                UUID = acc.Uuid,
                AccessToken = acc.MinecraftAccessToken,
                Xuid = acc.Xuid,
                UserType = "msa"
            };
        }
        
        var offlineSession = MSession.CreateOfflineSession(GetActiveUsername());
        offlineSession.UUID = _playerUuid;
        return offlineSession;
    }

    private void UpdateActiveAccountDisplay()
    {
        var username = GetActiveUsername();
        var navPanel = accountsNavButton.Content as StackPanel;
        if (navPanel != null && navPanel.Children.Count > 1 && navPanel.Children[1] is TextBlock tb)
        {
            tb.Text = username + " ▼";
        }
        UpdateCharacterFromUsername(username);
    }

    private void UpdateAndShowAccountsMenu()
    {
        accountsMenu.Items.Clear();
        foreach (var account in _settings.Accounts ?? new List<LauncherAccount>())
        {
            var item = new MenuItem { Header = account.Provider == "microsoft" ? $"🟢 {account.Username} (Microsoft)" : $"⚪ {account.Username} (Offline)" };
            item.Click += (_, _) => {
                _settings.SelectedAccountId = account.Id;
                _settings.Username = account.Username;
                UpdateActiveAccountDisplay();
                _settingsStore.Save(_settings);
            };
            accountsMenu.Items.Add(item);
        }

        accountsMenu.Items.Add(new Separator());
        var msLogin = new MenuItem { Header = "➕ Add Microsoft Account" };
        msLogin.Click += async (_, _) => await AddMicrosoftAccount();
        accountsMenu.Items.Add(msLogin);

        var offlineLogin = new MenuItem { Header = "➕ Add Offline Account" };
        offlineLogin.Click += async (_, _) => await AddOfflineAccount();
        accountsMenu.Items.Add(offlineLogin);

        accountsMenu.Open(accountsNavButton);
    }

    private async Task AddMicrosoftAccount()
    {
        try
        {
            ToggleBusyState(true, "Contacting Microsoft...");
            var authStore = new MinecraftAuthenticationService();
            _settings.MicrosoftClientId = string.IsNullOrWhiteSpace(_settings.MicrosoftClientId) 
                ? "d11b22e1-4560-4965-b169-feaa76e6dbfa" // fallback client ID if user didn't specify one
                : _settings.MicrosoftClientId;

            var session = await authStore.BeginDeviceLoginAsync(_settings.MicrosoftClientId, CancellationToken.None);
            
            ToggleBusyState(false, "Waiting for login.");
            await DialogService.ShowInfoAsync(this, "Microsoft Authentication", 
                session.Message + "\\n\\nComplete the sign-in on your browser. This box will close automatically when done.");
            
            ToggleBusyState(true, "Waiting for device login completion...");
            var newAccount = await authStore.CompleteDeviceLoginAsync(_settings.MicrosoftClientId, session, CancellationToken.None);
            
            _settings.Accounts.Add(newAccount);
            _settings.SelectedAccountId = newAccount.Id;
            _settings.Username = newAccount.Username;
            _settingsStore.Save(_settings);
            UpdateActiveAccountDisplay();
            await DialogService.ShowInfoAsync(this, "Success", $"Logged in successfully as {newAccount.Username}.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync(this, "Login failed", ex.Message);
        }
        finally
        {
            ToggleBusyState(false, "Ready to install or launch.");
        }
    }

    private async Task AddOfflineAccount()
    {
        // Simple input system bypass: since we don't have an input dialog, we'll just add Environment.UserName if not exists, 
        // Or if the user wants custom offline, we can prompt using an overlay or rely on them editing settings.json
        // Let's create a quick offline user name
        var offlineName = await DialogService.ShowConfirmAsync(this, "Offline Account", $"Add offline account for {Environment.UserName}?");
        if (offlineName)
        {
            var acc = new LauncherAccount { Id = Guid.NewGuid().ToString("N"), Provider = "offline", Username = Environment.UserName, DisplayName = Environment.UserName };
            _settings.Accounts.Add(acc);
            _settings.SelectedAccountId = acc.Id;
            _settings.Username = acc.Username;
            _settingsStore.Save(_settings);
            UpdateActiveAccountDisplay();
        }
    }
"""

code = code.replace("    private Control BuildHeader()", methods_patch + "    private Control BuildHeader()")

# Inject into BuildHeader
header_replacement = """                DetachFromParent(settingsNavButton),
                DetachFromParent(layoutNavButton),
                DetachFromParent(accountsNavButton)
            }"""
code = code.replace("""                DetachFromParent(settingsNavButton),
                DetachFromParent(layoutNavButton)
            }""", header_replacement)

# Ensure ApplyHoverMotion
code = code.replace("        ApplyHoverMotion(layoutNavButton);", "        ApplyHoverMotion(layoutNavButton);\n        ApplyHoverMotion(accountsNavButton);")

with open('MainWindow.cs', 'w') as f:
    f.write(code)

print("Patch applied.")
