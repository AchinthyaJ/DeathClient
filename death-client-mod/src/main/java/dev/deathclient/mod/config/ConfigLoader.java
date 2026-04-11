package dev.deathclient.mod.config;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonObject;
import dev.deathclient.mod.CapeManager;
import dev.deathclient.mod.AetherLauncherMod;
import dev.deathclient.mod.SkinManager;
import net.fabricmc.loader.api.FabricLoader;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.file.*;
import java.time.Duration;
import java.util.Arrays;

/**
 * Reads death-client config and populates SkinManager / CapeManager.
 * 
 * Expected directory structure under .minecraft (or instance root):
 * 
 *   config/
 *     death-client/
 *       death-client.json    ← optional, specifies filenames
 *       skins/
 *         skin.png           ← player skin (64×64 PNG)
 *       capes/
 *         cape.png           ← player cape (64×32 PNG)
 * 
 * Config JSON format (all fields optional):
 * {
 *   "skinFile": "skin.png",
 *   "capeFile": "cape.png",
 *   "skinEnabled": true,
 *   "capeEnabled": true
 * }
 * 
 * If no JSON exists, the loader looks for any .png in skins/ and capes/ directories.
 */
public class ConfigLoader {

    private static final ConfigLoader INSTANCE = new ConfigLoader();
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    private static final long HOT_RELOAD_INTERVAL_MS = 5000; // 5 seconds
    private static final HttpClient HTTP_CLIENT = HttpClient.newBuilder()
        .connectTimeout(Duration.ofSeconds(2))
        .build();

    private Path configDir;
    private Path skinsDir;
    private Path capesDir;
    private Path configFile;

    private long lastReloadCheck = 0;
    private long lastSkinModified = 0;
    private long lastCapeModified = 0;
    private long lastConfigModified = 0;

    // Config values
    private String skinFileName = "skin.png";
    private String capeFileName = "cape.png";
    private boolean skinEnabled = true;
    private boolean capeEnabled = true;
    private String skinUrl = "";
    private String capeUrl = "";

    private ConfigLoader() {}

    public static ConfigLoader getInstance() {
        return INSTANCE;
    }

    /**
     * Initial load — called once during mod init.
     */
    public void load() {
        configDir = FabricLoader.getInstance().getConfigDir().resolve("death-client");
        skinsDir = configDir.resolve("skins");
        capesDir = configDir.resolve("capes");
        configFile = configDir.resolve("death-client.json");

        // Create directories if they don't exist
        try {
            Files.createDirectories(skinsDir);
            Files.createDirectories(capesDir);
        } catch (IOException e) {
            AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to create config directories", e);
        }

        // Read JSON config if it exists
        readConfig();

        // Load textures
        loadSkin();
        loadCape();

        // Write default config if none exists
        if (!Files.exists(configFile)) {
            writeDefaultConfig();
        }
    }

    private void readConfig() {
        skinFileName = "skin.png";
        capeFileName = "cape.png";
        skinEnabled = true;
        capeEnabled = true;
        skinUrl = "";
        capeUrl = "";

        if (!Files.exists(configFile)) {
            AetherLauncherMod.LOGGER.info("[Aether Launcher] No config file found, using defaults.");
            return;
        }

        try {
            String json = Files.readString(configFile);
            JsonObject obj = GSON.fromJson(json, JsonObject.class);

            if (obj.has("skinFile"))    skinFileName = obj.get("skinFile").getAsString();
            if (obj.has("capeFile"))    capeFileName = obj.get("capeFile").getAsString();
            if (obj.has("skinEnabled")) skinEnabled  = obj.get("skinEnabled").getAsBoolean();
            if (obj.has("capeEnabled")) capeEnabled  = obj.get("capeEnabled").getAsBoolean();
            if (obj.has("skinUrl"))     skinUrl      = obj.get("skinUrl").getAsString();
            if (obj.has("capeUrl"))     capeUrl      = obj.get("capeUrl").getAsString();

            lastConfigModified = Files.getLastModifiedTime(configFile).toMillis();
            AetherLauncherMod.LOGGER.info("[Aether Launcher] Config loaded: skin={} ({}), cape={} ({}), skinUrl={}, capeUrl={}",
                skinFileName, skinEnabled ? "enabled" : "disabled",
                capeFileName, capeEnabled ? "enabled" : "disabled",
                skinUrl,
                capeUrl);
        } catch (Exception e) {
            AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to read config", e);
        }
    }

    private void loadSkin() {
        SkinManager skinMgr = SkinManager.getInstance();

        if (!skinEnabled) {
            skinMgr.clearSkin();
            AetherLauncherMod.LOGGER.info("[Aether Launcher] Skin override is disabled in config.");
            return;
        }

        if (!skinUrl.isBlank()) {
            Path remoteSkinPath = skinsDir.resolve("remote-skin.png");
            if (downloadRemoteAsset(skinUrl, remoteSkinPath, "skin")) {
                try {
                    lastSkinModified = Files.getLastModifiedTime(remoteSkinPath).toMillis();
                    skinMgr.loadSkin(remoteSkinPath);
                    AetherLauncherMod.LOGGER.info("[Aether Launcher] Loaded skin from local node server: {}", skinUrl);
                    return;
                } catch (Exception e) {
                    AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to load downloaded skin", e);
                    skinMgr.clearSkin();
                }
            }
        }

        Path skinPath = skinsDir.resolve(skinFileName);
        if (Files.exists(skinPath)) {
            try {
                lastSkinModified = Files.getLastModifiedTime(skinPath).toMillis();
                skinMgr.loadSkin(skinPath);
                AetherLauncherMod.LOGGER.info("[Aether Launcher] Loaded skin from: {}", skinPath);
            } catch (Exception e) {
                AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to load skin", e);
                skinMgr.clearSkin();
            }
        } else {
            // Try to find any PNG in skins directory
            try (DirectoryStream<Path> stream = Files.newDirectoryStream(skinsDir, "*.png")) {
                for (Path entry : stream) {
                    lastSkinModified = Files.getLastModifiedTime(entry).toMillis();
                    skinMgr.loadSkin(entry);
                    AetherLauncherMod.LOGGER.info("[Aether Launcher] Auto-detected skin: {}", entry.getFileName());
                    break; // Use the first one found
                }
            } catch (IOException e) {
                AetherLauncherMod.LOGGER.warn("[Aether Launcher] Could not scan skins directory", e);
            }

            if (!skinMgr.hasSkin()) {
                AetherLauncherMod.LOGGER.info("[Aether Launcher] No skin file found in {}", skinsDir);
            }
        }
    }

    private void loadCape() {
        CapeManager capeMgr = CapeManager.getInstance();

        if (!capeEnabled) {
            capeMgr.clearCape();
            AetherLauncherMod.LOGGER.info("[Aether Launcher] Cape override is disabled in config.");
            return;
        }

        if (!capeUrl.isBlank()) {
            Path remoteCapePath = capesDir.resolve("remote-cape.png");
            if (downloadRemoteAsset(capeUrl, remoteCapePath, "cape")) {
                try {
                    lastCapeModified = Files.getLastModifiedTime(remoteCapePath).toMillis();
                    capeMgr.loadCape(remoteCapePath);
                    AetherLauncherMod.LOGGER.info("[Aether Launcher] Loaded cape from local node server: {}", capeUrl);
                    return;
                } catch (Exception e) {
                    AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to load downloaded cape", e);
                    capeMgr.clearCape();
                }
            }
        }

        Path capePath = capesDir.resolve(capeFileName);
        if (Files.exists(capePath)) {
            try {
                lastCapeModified = Files.getLastModifiedTime(capePath).toMillis();
                capeMgr.loadCape(capePath);
                AetherLauncherMod.LOGGER.info("[Aether Launcher] Loaded cape from: {}", capePath);
            } catch (Exception e) {
                AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to load cape", e);
                capeMgr.clearCape();
            }
        } else {
            // Try to find any PNG in capes directory
            try (DirectoryStream<Path> stream = Files.newDirectoryStream(capesDir, "*.png")) {
                for (Path entry : stream) {
                    lastCapeModified = Files.getLastModifiedTime(entry).toMillis();
                    capeMgr.loadCape(entry);
                    AetherLauncherMod.LOGGER.info("[Aether Launcher] Auto-detected cape: {}", entry.getFileName());
                    break;
                }
            } catch (IOException e) {
                AetherLauncherMod.LOGGER.warn("[Aether Launcher] Could not scan capes directory", e);
            }

            if (!capeMgr.hasCape()) {
                AetherLauncherMod.LOGGER.info("[Aether Launcher] No cape file found in {}", capesDir);
            }
        }
    }

    private void writeDefaultConfig() {
        try {
            JsonObject obj = new JsonObject();
            obj.addProperty("skinFile", skinFileName);
            obj.addProperty("capeFile", capeFileName);
            obj.addProperty("skinEnabled", skinEnabled);
            obj.addProperty("capeEnabled", capeEnabled);
            obj.addProperty("skinUrl", skinUrl);
            obj.addProperty("capeUrl", capeUrl);
            Files.writeString(configFile, GSON.toJson(obj));
            AetherLauncherMod.LOGGER.info("[Aether Launcher] Wrote default config to {}", configFile);
        } catch (IOException e) {
            AetherLauncherMod.LOGGER.error("[Aether Launcher] Failed to write default config", e);
        }
    }

    /**
     * Called every client tick — checks for file changes every HOT_RELOAD_INTERVAL_MS.
     */
    public void tickHotReload() {
        long now = System.currentTimeMillis();
        if (now - lastReloadCheck < HOT_RELOAD_INTERVAL_MS) return;
        lastReloadCheck = now;

        try {
            // Check config file changes
            if (Files.exists(configFile)) {
                long configMod = Files.getLastModifiedTime(configFile).toMillis();
                if (configMod != lastConfigModified) {
                    AetherLauncherMod.LOGGER.info("[Aether Launcher] Config file changed, reloading...");
                    readConfig();
                    loadSkin();
                    loadCape();
                    return;
                }
            }

            if (!skinUrl.isBlank()) {
                loadSkin();
            } else {
                Path skinPath = skinsDir.resolve(skinFileName);
                if (Files.exists(skinPath)) {
                    long skinMod = Files.getLastModifiedTime(skinPath).toMillis();
                    if (skinMod != lastSkinModified) {
                        AetherLauncherMod.LOGGER.info("[Aether Launcher] Skin file changed, hot-reloading...");
                        loadSkin();
                    }
                }
            }

            if (!capeUrl.isBlank()) {
                loadCape();
            } else {
                Path capePath = capesDir.resolve(capeFileName);
                if (Files.exists(capePath)) {
                    long capeMod = Files.getLastModifiedTime(capePath).toMillis();
                    if (capeMod != lastCapeModified) {
                        AetherLauncherMod.LOGGER.info("[Aether Launcher] Cape file changed, hot-reloading...");
                        loadCape();
                    }
                }
            }
        } catch (IOException e) {
            // Silently ignore — file might be mid-write
        }
    }

    private boolean downloadRemoteAsset(String url, Path targetPath, String assetLabel) {
        try {
            Files.createDirectories(targetPath.getParent());

            HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .timeout(Duration.ofSeconds(3))
                .GET()
                .build();

            HttpResponse<byte[]> response = HTTP_CLIENT.send(request, HttpResponse.BodyHandlers.ofByteArray());
            if (response.statusCode() != 200) {
                AetherLauncherMod.LOGGER.warn("[Aether Launcher] Node server returned {} for {} {}", response.statusCode(), assetLabel, url);
                return false;
            }

            byte[] body = response.body();
            if (body == null || body.length == 0) {
                AetherLauncherMod.LOGGER.warn("[Aether Launcher] Empty {} payload from {}", assetLabel, url);
                return false;
            }

            if (Files.exists(targetPath)) {
                byte[] existing = Files.readAllBytes(targetPath);
                if (Arrays.equals(existing, body)) {
                    return true;
                }
            }

            Files.write(targetPath, body, StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING);
            return true;
        } catch (Exception e) {
            AetherLauncherMod.LOGGER.warn("[Aether Launcher] Failed to download {} from {}", assetLabel, url, e);
            return false;
        }
    }
}
