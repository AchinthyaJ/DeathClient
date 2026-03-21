package dev.deathclient.mod.config;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonObject;
import dev.deathclient.mod.CapeManager;
import dev.deathclient.mod.DeathClientMod;
import dev.deathclient.mod.SkinManager;
import net.fabricmc.loader.api.FabricLoader;

import java.io.IOException;
import java.nio.file.*;

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
            DeathClientMod.LOGGER.error("[Death Client] Failed to create config directories", e);
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
        if (!Files.exists(configFile)) {
            DeathClientMod.LOGGER.info("[Death Client] No config file found, using defaults.");
            return;
        }

        try {
            String json = Files.readString(configFile);
            JsonObject obj = GSON.fromJson(json, JsonObject.class);

            if (obj.has("skinFile"))    skinFileName = obj.get("skinFile").getAsString();
            if (obj.has("capeFile"))    capeFileName = obj.get("capeFile").getAsString();
            if (obj.has("skinEnabled")) skinEnabled  = obj.get("skinEnabled").getAsBoolean();
            if (obj.has("capeEnabled")) capeEnabled  = obj.get("capeEnabled").getAsBoolean();

            lastConfigModified = Files.getLastModifiedTime(configFile).toMillis();
            DeathClientMod.LOGGER.info("[Death Client] Config loaded: skin={} ({}), cape={} ({})",
                skinFileName, skinEnabled ? "enabled" : "disabled",
                capeFileName, capeEnabled ? "enabled" : "disabled");
        } catch (Exception e) {
            DeathClientMod.LOGGER.error("[Death Client] Failed to read config", e);
        }
    }

    private void loadSkin() {
        SkinManager skinMgr = SkinManager.getInstance();

        if (!skinEnabled) {
            skinMgr.clearSkin();
            DeathClientMod.LOGGER.info("[Death Client] Skin override is disabled in config.");
            return;
        }

        Path skinPath = skinsDir.resolve(skinFileName);
        if (Files.exists(skinPath)) {
            try {
                lastSkinModified = Files.getLastModifiedTime(skinPath).toMillis();
                skinMgr.loadSkin(skinPath);
                DeathClientMod.LOGGER.info("[Death Client] Loaded skin from: {}", skinPath);
            } catch (Exception e) {
                DeathClientMod.LOGGER.error("[Death Client] Failed to load skin", e);
                skinMgr.clearSkin();
            }
        } else {
            // Try to find any PNG in skins directory
            try (DirectoryStream<Path> stream = Files.newDirectoryStream(skinsDir, "*.png")) {
                for (Path entry : stream) {
                    lastSkinModified = Files.getLastModifiedTime(entry).toMillis();
                    skinMgr.loadSkin(entry);
                    DeathClientMod.LOGGER.info("[Death Client] Auto-detected skin: {}", entry.getFileName());
                    break; // Use the first one found
                }
            } catch (IOException e) {
                DeathClientMod.LOGGER.warn("[Death Client] Could not scan skins directory", e);
            }

            if (!skinMgr.hasSkin()) {
                DeathClientMod.LOGGER.info("[Death Client] No skin file found in {}", skinsDir);
            }
        }
    }

    private void loadCape() {
        CapeManager capeMgr = CapeManager.getInstance();

        if (!capeEnabled) {
            capeMgr.clearCape();
            DeathClientMod.LOGGER.info("[Death Client] Cape override is disabled in config.");
            return;
        }

        Path capePath = capesDir.resolve(capeFileName);
        if (Files.exists(capePath)) {
            try {
                lastCapeModified = Files.getLastModifiedTime(capePath).toMillis();
                capeMgr.loadCape(capePath);
                DeathClientMod.LOGGER.info("[Death Client] Loaded cape from: {}", capePath);
            } catch (Exception e) {
                DeathClientMod.LOGGER.error("[Death Client] Failed to load cape", e);
                capeMgr.clearCape();
            }
        } else {
            // Try to find any PNG in capes directory
            try (DirectoryStream<Path> stream = Files.newDirectoryStream(capesDir, "*.png")) {
                for (Path entry : stream) {
                    lastCapeModified = Files.getLastModifiedTime(entry).toMillis();
                    capeMgr.loadCape(entry);
                    DeathClientMod.LOGGER.info("[Death Client] Auto-detected cape: {}", entry.getFileName());
                    break;
                }
            } catch (IOException e) {
                DeathClientMod.LOGGER.warn("[Death Client] Could not scan capes directory", e);
            }

            if (!capeMgr.hasCape()) {
                DeathClientMod.LOGGER.info("[Death Client] No cape file found in {}", capesDir);
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
            Files.writeString(configFile, GSON.toJson(obj));
            DeathClientMod.LOGGER.info("[Death Client] Wrote default config to {}", configFile);
        } catch (IOException e) {
            DeathClientMod.LOGGER.error("[Death Client] Failed to write default config", e);
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
                    DeathClientMod.LOGGER.info("[Death Client] Config file changed, reloading...");
                    readConfig();
                    loadSkin();
                    loadCape();
                    return;
                }
            }

            // Check skin file changes
            Path skinPath = skinsDir.resolve(skinFileName);
            if (Files.exists(skinPath)) {
                long skinMod = Files.getLastModifiedTime(skinPath).toMillis();
                if (skinMod != lastSkinModified) {
                    DeathClientMod.LOGGER.info("[Death Client] Skin file changed, hot-reloading...");
                    loadSkin();
                }
            }

            // Check cape file changes
            Path capePath = capesDir.resolve(capeFileName);
            if (Files.exists(capePath)) {
                long capeMod = Files.getLastModifiedTime(capePath).toMillis();
                if (capeMod != lastCapeModified) {
                    DeathClientMod.LOGGER.info("[Death Client] Cape file changed, hot-reloading...");
                    loadCape();
                }
            }
        } catch (IOException e) {
            // Silently ignore — file might be mid-write
        }
    }
}
