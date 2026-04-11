import os

replacements = [
    ("DEATH CLIENT", "AETHER LAUNCHER"),
    ("Death Client", "Aether Launcher"),
    ("DeathClient", "AetherLauncher")
]

for root, dirs, files in os.walk("."):
    if "tmp" in root or ".git" in root or "build" in root or "bin" in root or "obj" in root or "publish" in root: continue
    for file in files:
        if file.endswith(".cs") or file.endswith(".java") or file.endswith(".md") or file.endswith(".json") or file.endswith(".axaml"):
            path = os.path.join(root, file)
            with open(path, "r", encoding="utf-8") as f:
                content = f.read()
            original = content
            for old, new in replacements:
                content = content.replace(old, new)
            if content != original:
                with open(path, "w", encoding="utf-8") as f:
                    f.write(content)
                print(f"Updated string texts in {path}")
