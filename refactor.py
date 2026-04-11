import re

with open("MainWindow.cs", "r", encoding="utf-8") as f:
    lines = f.readlines()

new_lines = []
skip_until = None

for i, line in enumerate(lines):
    if skip_until is not None:
        if skip_until in line:
            skip_until = None
        continue

    # Remove fields
    if "private TextBox usernameInput = null!;" in line:
        continue
    if "private Border _accountsOverlay = new();" in line:
        continue
    if "private string _accountStatusMessage" in line:
        continue
    
    # Remove from DetachReusableControls
    if "usernameInput," in line.strip():
        continue
    if "_accountsOverlay" in line.strip() and "," not in line and "{" not in line: # DetachFromParent(_accountsOverlay)
        if "DetachFromParent(_accountsOverlay)" in line:
            pass # We will handle some later

    # Remove EnsureFallbackControlsInitialized parts
    if "usernameInput ??= CreateTextBox();" in line:
        skip_until = "usernameInput.TextChanged += UsernameInput_TextChanged;"
        continue

    if "ApplySelectedAccountToUsernameInput();" in line:
        continue

    if "DetachFromParent(usernameInput)," in line:
        continue
        
    if "usernameInput.Text = _settings.Username;" in line:
        continue
    if "if (string.IsNullOrWhiteSpace(usernameInput.Text))" in line:
        continue
    if "usernameInput.Text = Environment.UserName;" in line:
        continue

    if "var launchUsername = selectedAccount?.Username ?? usernameInput.Text?.Trim() ?? string.Empty;" in line:
        new_lines.append(line.replace("usernameInput.Text?.Trim() ?? string.Empty", "Environment.UserName"))
        continue

    if "btnStart.Enabled = !isBusy && !string.IsNullOrWhiteSpace(usernameInput.Text);" in line:
        new_lines.append(line.replace("!string.IsNullOrWhiteSpace(usernameInput.Text)", "true"))
        continue

    if "public async void UsernameInput_TextChanged()" in line:
        skip_until = "var username = usernameInput.Text.Trim();" # wait, we need to skip the whole method
        continue
    
    # Wait, it's easier to skip methods by checking { matching, but simple replace might be enough.

    # Remove accounts overlay Build methods
    if "private Border BuildAccountsOverlay()" in line:
        skip_until = "private void RefreshAccountsOverlayContent(Border overlay, bool visible)"
        continue

    # Replace accountsBtn in BuildHomeScreen
    if "var accountsBtn = CreateSecondaryButton(\"Accounts\");" in line:
        skip_until = "accountsBtn" # skip this and following
        continue

    new_lines.append(line)

with open("MainWindow.cs", "w", encoding="utf-8") as f:
    f.writelines(new_lines)
