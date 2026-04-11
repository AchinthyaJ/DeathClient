package dev.deathclient.mod;

import com.mojang.blaze3d.systems.RenderSystem;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.texture.NativeImage;
import net.minecraft.client.texture.NativeImageBackedTexture;
import net.minecraft.util.Identifier;

import java.io.FileInputStream;
import java.io.InputStream;
import java.nio.file.Path;

/**
 * Manages custom cape texture loading and registration.
 * 
 * Similar approach to SkinManager:
 *   1. Reads a cape PNG from disk (expected: 64×32)
 *   2. Registers it as a dynamic texture
 *   3. The mixin (AbstractClientPlayerMixin) redirects cape rendering
 *      to use our registered texture Identifier
 * 
 * Cape texture is registered under:
 *   death-client-mod:textures/cape/custom_cape
 * 
 * Elytra texture is also overridden to match the cape (same file).
 */
public class CapeManager {

    private static final CapeManager INSTANCE = new CapeManager();
    private static final Identifier CAPE_TEXTURE_ID = Identifier.of(AetherLauncherMod.MOD_ID, "textures/cape/custom_cape");

    private boolean hasCape = false;
    private boolean textureRegistered = false;
    private Path currentCapePath = null;
    private NativeImageBackedTexture capeTexture = null;

    private CapeManager() {}

    public static CapeManager getInstance() {
        return INSTANCE;
    }

    /**
     * Load a cape PNG from the given path and register it as a Minecraft texture.
     */
    public void loadCape(Path capePath) {
        this.currentCapePath = capePath;

        MinecraftClient client = MinecraftClient.getInstance();
        if (client == null) {
            this.hasCape = true;
            AetherLauncherMod.LOGGER.info("[Aether Launcher/CapeManager] Marked cape for deferred loading: {}", capePath);
            return;
        }

        if (!RenderSystem.isOnRenderThread()) {
            RenderSystem.recordRenderCall(() -> registerTexture(capePath));
            this.hasCape = true;
        } else {
            registerTexture(capePath);
        }
    }

    private void registerTexture(Path capePath) {
        try {
            disposeTexture();

            InputStream is = new FileInputStream(capePath.toFile());
            NativeImage image = NativeImage.read(is);
            is.close();

            int w = image.getWidth();
            int h = image.getHeight();

            // Standard cape dimensions: 64×32 or multiples (some HD capes use 2048×1024)
            if (h != 32 && h != 64 && w != 64 && w != 128) {
                AetherLauncherMod.LOGGER.warn("[Aether Launcher/CapeManager] Cape has unusual dimensions: {}×{}", w, h);
            }

            // Create and register the texture safely crossing obfuscation discrepancies
            try {
                capeTexture = new NativeImageBackedTexture(image);
            } catch (Throwable e) {
                for (java.lang.reflect.Constructor<?> c : NativeImageBackedTexture.class.getConstructors()) {
                    if (c.getParameterCount() == 1) {
                        capeTexture = (NativeImageBackedTexture) c.newInstance(image);
                        break;
                    }
                }
                if (capeTexture == null) throw new RuntimeException("Could not reflective construct NativeImageBackedTexture", e);
            }
            MinecraftClient.getInstance().getTextureManager().registerTexture(CAPE_TEXTURE_ID, capeTexture);
            textureRegistered = true;
            hasCape = true;

            AetherLauncherMod.LOGGER.info("[Aether Launcher/CapeManager] Cape texture registered: {} ({}×{})", CAPE_TEXTURE_ID, w, h);
        } catch (Exception e) {
            AetherLauncherMod.LOGGER.error("[Aether Launcher/CapeManager] Failed to register cape texture", e);
            hasCape = false;
            textureRegistered = false;
        }
    }

    public void ensureTextureRegistered() {
        if (hasCape && !textureRegistered && currentCapePath != null) {
            registerTexture(currentCapePath);
        }
    }

    private void disposeTexture() {
        if (capeTexture != null) {
            try {
                MinecraftClient client = MinecraftClient.getInstance();
                if (client != null && client.getTextureManager() != null) {
                    client.getTextureManager().destroyTexture(CAPE_TEXTURE_ID);
                }
            } catch (Exception ignored) {}
            capeTexture = null;
            textureRegistered = false;
        }
    }

    public void clearCape() {
        disposeTexture();
        hasCape = false;
        currentCapePath = null;
        AetherLauncherMod.LOGGER.info("[Aether Launcher/CapeManager] Cape cleared.");
    }

    // --- Accessors ---

    public boolean hasCape() {
        return hasCape;
    }

    public boolean isTextureReady() {
        return hasCape && textureRegistered;
    }

    public Identifier getCapeTextureId() {
        return CAPE_TEXTURE_ID;
    }

    public Path getCapePath() {
        return currentCapePath;
    }
}
