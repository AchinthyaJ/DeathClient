package dev.deathclient.mod.mixin;

import dev.deathclient.mod.AetherLauncherMod;
import dev.deathclient.mod.SkinManager;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.network.PlayerListEntry;
import net.minecraft.client.util.SkinTextures;
import net.minecraft.util.Identifier;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/**
 * Mixin into PlayerListEntry to override skin texture resolution.
 * 
 * HOOK POINT: PlayerListEntry#getSkinTextures()
 * 
 * This is where Minecraft resolves the skin texture for a given player.
 * By injecting at the RETURN of this method, we can replace the skin
 * Identifier in the returned SkinTextures with our custom texture —
 * but ONLY for the local player.
 * 
 * Why PlayerListEntry?
 *   In modern Minecraft (1.20+), skin textures flow through:
 *     PlayerListEntry → SkinTextures → PlayerEntityRenderer
 *   This is the cleanest interception point because:
 *   - It's called every frame during rendering
 *   - It returns a SkinTextures record that bundles skin + cape + model
 *   - We can construct a new SkinTextures with our overrides
 * 
 * Why not SkinProvider/SkinTexture?
 *   - SkinProvider deals with fetching from Mojang's API (irrelevant offline)
 *   - Direct texture replacement would affect ALL players on a LAN server
 */
@Mixin(PlayerListEntry.class)
public abstract class PlayerSkinProviderMixin {

    @Inject(method = "getSkinTextures", at = @At("RETURN"), cancellable = true)
    private void deathClient_overrideSkinTextures(CallbackInfoReturnable<SkinTextures> cir) {
        SkinManager skinMgr = SkinManager.getInstance();

        if (!skinMgr.hasSkin()) return;

        // Only override for the local player
        MinecraftClient client = MinecraftClient.getInstance();
        if (client == null || client.player == null) return;

        PlayerListEntry self = (PlayerListEntry) (Object) this;
        
        // Get the player's profile from the entry
        if (client.player.getGameProfile() == null) return;
        
        // Check if this entry belongs to the local player
        PlayerListEntry localEntry = client.getNetworkHandler() != null 
            ? client.getNetworkHandler().getPlayerListEntry(client.player.getUuid()) 
            : null;
        
        if (localEntry == null || self != localEntry) return;

        // Ensure texture is registered (handles deferred loading)
        skinMgr.ensureTextureRegistered();
        if (!skinMgr.isTextureReady()) return;

        SkinTextures original = cir.getReturnValue();
        if (original == null) return;

        // Build new SkinTextures with our custom skin
        Identifier customSkinId = skinMgr.getSkinTextureId();
        
        SkinTextures overridden = new SkinTextures(
            customSkinId,                       // skin texture → OUR custom texture
            original.textureUrl(),              // textureUrl (unused in rendering)
            original.capeTexture(),             // cape texture (CapeManager handles this)
            original.elytraTexture(),           // elytra texture
            original.model(),                   // model type (wide/slim) 
            original.secure()                   // secure flag
        );

        cir.setReturnValue(overridden);
    }
}
