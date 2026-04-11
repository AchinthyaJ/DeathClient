package dev.deathclient.mod.mixin;

import dev.deathclient.mod.CapeManager;
import dev.deathclient.mod.AetherLauncherMod;
import dev.deathclient.mod.SkinManager;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.network.AbstractClientPlayerEntity;
import net.minecraft.client.util.SkinTextures;
import net.minecraft.util.Identifier;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/**
 * Mixin into AbstractClientPlayerEntity to override cape texture.
 * 
 * HOOK POINT: AbstractClientPlayerEntity#getSkinTextures()
 * 
 * Why a separate mixin for capes?
 *   PlayerListEntry and AbstractClientPlayerEntity both have getSkinTextures(),
 *   but the cape rendering pipeline specifically uses the player entity's method.
 *   By intercepting here, we ensure cape overrides work in ALL contexts:
 *   - Third-person view
 *   - Elytra rendering
 *   - Tab list player head (if supported)
 * 
 * The cape Identifier is inserted into the SkinTextures alongside any
 * skin overrides from PlayerSkinProviderMixin. Both mixins work together:
 *   1. PlayerSkinProviderMixin replaces skin in PlayerListEntry
 *   2. This mixin replaces cape in AbstractClientPlayerEntity
 *   
 * Since AbstractClientPlayerEntity.getSkinTextures() typically delegates
 * to PlayerListEntry.getSkinTextures(), our skin override flows through
 * automatically. This mixin only needs to handle the CAPE injection.
 */
@Mixin(AbstractClientPlayerEntity.class)
public abstract class AbstractClientPlayerMixin {

    @Inject(method = "getSkinTextures", at = @At("RETURN"), cancellable = true)
    private void deathClient_overrideCapeTexture(CallbackInfoReturnable<SkinTextures> cir) {
        CapeManager capeMgr = CapeManager.getInstance();
        SkinManager skinMgr = SkinManager.getInstance();

        boolean hasCape = capeMgr.hasCape();
        boolean hasSkin = skinMgr.hasSkin();

        // Nothing to override
        if (!hasCape && !hasSkin) return;

        // Only override for the local player
        MinecraftClient client = MinecraftClient.getInstance();
        if (client == null || client.player == null) return;

        AbstractClientPlayerEntity self = (AbstractClientPlayerEntity) (Object) this;
        if (self != client.player) return;

        SkinTextures original = cir.getReturnValue();
        if (original == null) return;

        // Ensure textures are registered (deferred loading)
        if (hasCape) capeMgr.ensureTextureRegistered();
        if (hasSkin) skinMgr.ensureTextureRegistered();

        // Determine what to override
        Identifier skinTexture = original.texture();
        Identifier capeTexture = original.capeTexture();
        Identifier elytraTexture = original.elytraTexture();

        // Override skin if available
        if (hasSkin && skinMgr.isTextureReady()) {
            skinTexture = skinMgr.getSkinTextureId();
        }

        // Override cape and elytra if available
        if (hasCape && capeMgr.isTextureReady()) {
            Identifier customCapeId = capeMgr.getCapeTextureId();
            capeTexture = customCapeId;
            elytraTexture = customCapeId; // Elytra uses the same texture as cape
        }

        // Only create a new SkinTextures if something actually changed
        if (skinTexture != original.texture() || capeTexture != original.capeTexture()) {
            SkinTextures overridden = new SkinTextures(
                skinTexture,
                original.textureUrl(),
                capeTexture,
                elytraTexture,
                original.model(),
                original.secure()
            );
            cir.setReturnValue(overridden);
        }
    }
}
