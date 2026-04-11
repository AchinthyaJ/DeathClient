import re

with open('MainWindow.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Remove usernameInput field
code = re.sub(r' +private readonly TextBox usernameInput;\n', '', code)

# 2. Remove initialization
code = re.sub(r' +usernameInput = CreateTextBox\(\);\n +usernameInput\.TextChanged \+= \(_, _\) => UsernameInput_TextChanged\(\);\n', '', code)

# 3. Remove watermark
code = re.sub(r' +usernameInput\.Watermark = "Enter Username";\n', '', code)

# 4. Remove fallback controls logic
to_remove_fallback = """        usernameInput.Text = _settings.Username;
        usernameInput.Background = Brushes.Transparent;
        usernameInput.BorderBrush = Brushes.Transparent;
        usernameInput.Foreground = new SolidColorBrush(Color.Parse("#B0BACF"));
        usernameInput.FontSize = 18;
        usernameInput.Padding = new Thickness(0);
        usernameInput.TextChanged += (_, _) => {
            _settings.Username = usernameInput.Text;
            _settingsStore.Save(_settings);
        };"""
code = code.replace(to_remove_fallback, "")

# 5. Remove from BuildLaunchDeck
to_remove_launch_deck = """                new Border { Height = 12 },
                usernameInput,
                new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(40, 255,255,255)), Margin = new Thickness(0, 8, 0, 0) }"""
code = code.replace(to_remove_launch_deck, "                new Border { Height = 12 }")

# 6. InitializeAsync
to_remove_init = """        usernameInput.Text = _settings.Username;
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
            usernameInput.Text = Environment.UserName;"""
replace_init = """        if (string.IsNullOrWhiteSpace(_settings.Username))
            _settings.Username = Environment.UserName;
        UpdateActiveAccountDisplay();"""
code = code.replace(to_remove_init, replace_init)

# 7. LaunchAsync start
to_remove_launch_async = """    private async Task LaunchAsync()
    {
        if (string.IsNullOrWhiteSpace(usernameInput.Text))"""
replace_launch_async = """    private async Task LaunchAsync()
    {
        var activeUsername = GetActiveUsername();
        if (string.IsNullOrWhiteSpace(activeUsername))"""
code = code.replace(to_remove_launch_async, replace_launch_async)

# 8. LaunchAsync middle 1
code = code.replace('            $"Launch {targetLabel} as {usernameInput.Text.Trim()}?");', '            $"Launch {targetLabel} as {activeUsername}?");')

# 9. LaunchAsync session creation
to_remove_launch_async_session = """            var session = MSession.CreateOfflineSession(usernameInput.Text.Trim());
            session.UUID = _playerUuid;"""
replace_launch_async_session = """            var session = GetLaunchSession();"""
code = code.replace(to_remove_launch_async_session, replace_launch_async_session)

# 10. LaunchAsync settings update
code = code.replace('            _settings.Username = usernameInput.Text.Trim();', '            _settings.Username = activeUsername;')

# 11. UsernameInput_TextChanged -> UpdateCharacterFromUsername
to_replace_username_changed = """    private void UsernameInput_TextChanged()
    {
        if (string.IsNullOrWhiteSpace(usernameInput.Text))
        {
            _playerUuid = string.Empty;
            _characterInfo.Text = "Play offline";
            return;
        }

        _characterInfo.Text = $"Play as {usernameInput.Text.Trim()}";
        _playerUuid = Character.GenerateUuidFromUsername(usernameInput.Text.Trim());"""
replacement_username_changed = """    private void UpdateCharacterFromUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _playerUuid = string.Empty;
            _characterInfo.Text = "Play offline";
            return;
        }

        _characterInfo.Text = $"Play as {username}";
        _playerUuid = Character.GenerateUuidFromUsername(username);"""
code = code.replace(to_replace_username_changed, replacement_username_changed)

# 12. btnStart Update
code = code.replace('btnStart.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(usernameInput.Text);', 'btnStart.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(GetActiveUsername());')

# 13. Another stray reference (if any)
# I think I found a line 'Child = usernameInput' in grep output? Wait grep output line 2968: 'Child = usernameInput'
# Let's verify and remove it if there's any.
code = re.sub(r' +Child = usernameInput\n', '', code)


with open('MainWindow.cs', 'w', encoding='utf-8') as f:
    f.write(code)

print("Done")
