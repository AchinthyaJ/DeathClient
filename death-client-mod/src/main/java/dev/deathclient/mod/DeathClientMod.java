package dev.deathclient.mod;

import dev.deathclient.mod.config.ConfigLoader;
import net.fabricmc.api.ClientModInitializer;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Main entrypoint for the Aether Launcher skin/cape override mod.
 * 
 * This mod is CLIENT-ONLY. It:
 *   1. Reads config from  config/death-client/death-client.json
 *   2. Loads skin/cape PNGs from  config/death-client/skins/  and  config/death-client/capes/
 *   3. Overrides the local player's skin and cape textures at runtime via Mixins
 * 
 * No server-side component is needed — other players won't see your custom skin
 * (since Mojang auth is bypassed in offline mode).
 */
public class AetherLauncherMod implements ClientModInitializer {

    public static final String MOD_ID = "death-client-mod";
    public static final Logger LOGGER = LoggerFactory.getLogger(MOD_ID);

    @Override
    public void onInitializeClient() {
        LOGGER.info("[Aether Launcher] Initializing skin & cape override system...");

        // Load config (finds skin/cape paths)
        ConfigLoader.getInstance().load();

        // Register a tick callback to support hot-reload
        net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents.END_CLIENT_TICK.register(client -> {
            ConfigLoader.getInstance().tickHotReload();
        });

        LOGGER.info("[Aether Launcher] Skin override: {}", 
            SkinManager.getInstance().hasSkin() ? "ACTIVE (" + SkinManager.getInstance().getSkinPath() + ")" : "INACTIVE");
        LOGGER.info("[Aether Launcher] Cape override: {}", 
            CapeManager.getInstance().hasCape() ? "ACTIVE (" + CapeManager.getInstance().getCapePath() + ")" : "INACTIVE");
        LOGGER.info("[Aether Launcher] Mod initialized successfully. Hot-reload is enabled (checks every 5s).");
    }
}
