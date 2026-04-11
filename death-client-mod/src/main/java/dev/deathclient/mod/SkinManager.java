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
    private static final Identifier SKIN_TEXTURE_ID = Identifier.of(AetherLauncherMod.MOD_ID, "textures/skin/custom_skin");

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
            AetherLauncherMod.LOGGER.info("[Aether Launcher/SkinManager] Marked skin for deferred loading: {}", skinPath);
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
        if (skinPath == null || !skinPath.toFile().exists()) return;
        
        // Safety check: Don't register if device isn't ready (reflective to compile on all versions)
        try {
            java.lang.reflect.Method getDeviceMethod = com.mojang.blaze3d.systems.RenderSystem.class.getMethod("getDevice");
            if (getDeviceMethod.invoke(null) == null) return;
        } catch (NoSuchMethodException e) {
            // If getDevice doesn't exist, we're likely on an older version or it's not required yet
        } catch (Exception e) {
            return; // Other failure means not ready
        }

        try {
            // Dispose old texture if present
            disposeTexture();

            // Read the PNG
            InputStream is = new FileInputStream(skinPath.toFile());
            NativeImage image = NativeImage.read(is);
            is.close();

            // Validate dimensions (standard Minecraft skin is 64x64 or legacy 64x32)
            int w = image.getWidth();
            int h = image.getHeight();
            if (w != 64 || (h != 64 && h != 32)) {
                AetherLauncherMod.LOGGER.warn("[Aether Launcher/SkinManager] Skin has unusual dimensions: {}x{} (expected 64x64 or 64x32)", w, h);
            }

            // Create and register the texture using reflection for maximum cross-version compatibility
            try {
                java.lang.reflect.Constructor<?>[] constructors = NativeImageBackedTexture.class.getConstructors();
                for (java.lang.reflect.Constructor<?> c : constructors) {
                    Class<?>[] params = c.getParameterTypes();
                    if (params.length == 1 && params[0] == NativeImage.class) {
                        skinTexture = (NativeImageBackedTexture) c.newInstance(image);
                        break;
                    } else if (params.length == 2 && params[1] == NativeImage.class) {
                        if (params[0] == String.class) {
                            skinTexture = (NativeImageBackedTexture) c.newInstance("death_client_skin", image);
                        } else {
                            skinTexture = (NativeImageBackedTexture) c.newInstance((java.util.function.Supplier<String>)() -> "death_client_skin", image);
                        }
                        break;
                    }
                }
                
                if (skinTexture == null) {
                    throw new RuntimeException("Could not find a valid NativeImageBackedTexture constructor!");
                }
            } catch (Exception ex) {
                throw new RuntimeException("Reflective texture creation failed", ex);
            }

            MinecraftClient.getInstance().getTextureManager().registerTexture(SKIN_TEXTURE_ID, skinTexture);
            textureRegistered = true;
            hasSkin = true;

            AetherLauncherMod.LOGGER.info("[Aether Launcher/SkinManager] Skin texture registered successfully.");
        } catch (Exception e) {
            AetherLauncherMod.LOGGER.error("[Aether Launcher/SkinManager] Failed to register skin texture", e);
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
        AetherLauncherMod.LOGGER.info("[Aether Launcher/SkinManager] Skin cleared.");
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
