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
 * Manages custom skin texture loading and registration.
 * 
 * How it works:
 *   1. Reads a skin PNG from disk
 *   2. Registers it as a dynamic texture in Minecraft's TextureManager
 *   3. The mixin (PlayerSkinProviderMixin) redirects the player's skin 
 *      Identifier to point to our registered texture
 * 
 * The texture is registered under the Identifier:
 *   death-client-mod:textures/skin/custom_skin
 * 
 * This approach is safe because:
 *   - We use Minecraft's own TextureManager API
 *   - We only override the LOCAL player's skin
 *   - The texture is properly disposed on reload
 */
public class SkinManager {

    private static final SkinManager INSTANCE = new SkinManager();
    private static final Identifier SKIN_TEXTURE_ID = Identifier.of(DeathClientMod.MOD_ID, "textures/skin/custom_skin");

    private boolean hasSkin = false;
    private boolean textureRegistered = false;
    private Path currentSkinPath = null;
    private NativeImageBackedTexture skinTexture = null;

    private SkinManager() {}

    public static SkinManager getInstance() {
        return INSTANCE;
    }

    /**
     * Load a skin PNG from the given path and register it as a Minecraft texture.
     */
    public void loadSkin(Path skinPath) {
        this.currentSkinPath = skinPath;

        // Texture registration must happen on the render thread
        MinecraftClient client = MinecraftClient.getInstance();
        if (client == null) {
            // Too early — mark for deferred loading
            this.hasSkin = true;
            DeathClientMod.LOGGER.info("[Death Client/SkinManager] Marked skin for deferred loading: {}", skinPath);
            return;
        }

        // Schedule on render thread if we're not already on it
        if (!RenderSystem.isOnRenderThread()) {
            RenderSystem.recordRenderCall(() -> registerTexture(skinPath));
            this.hasSkin = true;
        } else {
            registerTexture(skinPath);
        }
    }

    private void registerTexture(Path skinPath) {
        try {
            // Dispose old texture if present
            disposeTexture();

            // Read the PNG
            InputStream is = new FileInputStream(skinPath.toFile());
            NativeImage image = NativeImage.read(is);
            is.close();

            // Validate dimensions (standard Minecraft skin is 64×64 or legacy 64×32)
            int w = image.getWidth();
            int h = image.getHeight();
            if (w != 64 || (h != 64 && h != 32)) {
                DeathClientMod.LOGGER.warn("[Death Client/SkinManager] Skin has unusual dimensions: {}×{} (expected 64×64 or 64×32)", w, h);
                // We'll still load it — Minecraft can handle slightly off dimensions
            }

            // Create and register the texture safely to support multiple MC versions
            try {
                skinTexture = new NativeImageBackedTexture(image);
            } catch (Throwable e) {
                // Fallback using reflection if mapping changed in this Minecraft build
                for (java.lang.reflect.Constructor<?> c : NativeImageBackedTexture.class.getConstructors()) {
                    if (c.getParameterCount() == 1) {
                        skinTexture = (NativeImageBackedTexture) c.newInstance(image);
                        break;
                    }
                }
                if (skinTexture == null) throw new RuntimeException("Could not reflective construct NativeImageBackedTexture", e);
            }
            MinecraftClient.getInstance().getTextureManager().registerTexture(SKIN_TEXTURE_ID, skinTexture);
            textureRegistered = true;
            hasSkin = true;

            DeathClientMod.LOGGER.info("[Death Client/SkinManager] Skin texture registered: {} ({}×{})", SKIN_TEXTURE_ID, w, h);
        } catch (Exception e) {
            DeathClientMod.LOGGER.error("[Death Client/SkinManager] Failed to register skin texture", e);
            hasSkin = false;
            textureRegistered = false;
        }
    }

    /**
     * Try deferred registration — called from mixin if texture wasn't ready at init.
     */
    public void ensureTextureRegistered() {
        if (hasSkin && !textureRegistered && currentSkinPath != null) {
            registerTexture(currentSkinPath);
        }
    }

    private void disposeTexture() {
        if (skinTexture != null) {
            try {
                MinecraftClient client = MinecraftClient.getInstance();
                if (client != null && client.getTextureManager() != null) {
                    client.getTextureManager().destroyTexture(SKIN_TEXTURE_ID);
                }
            } catch (Exception ignored) {}
            skinTexture = null;
            textureRegistered = false;
        }
    }

    public void clearSkin() {
        disposeTexture();
        hasSkin = false;
        currentSkinPath = null;
        DeathClientMod.LOGGER.info("[Death Client/SkinManager] Skin cleared.");
    }

    // --- Accessors ---

    public boolean hasSkin() {
        return hasSkin;
    }

    public boolean isTextureReady() {
        return hasSkin && textureRegistered;
    }

    public Identifier getSkinTextureId() {
        return SKIN_TEXTURE_ID;
    }

    public Path getSkinPath() {
        return currentSkinPath;
    }
}
