package dev.deathclient.mod.mixin;

import net.minecraft.client.gui.screen.Screen;
import net.minecraft.client.gui.screen.TitleScreen;
import net.minecraft.text.Text;
import org.spongepowered.asm.mixin.Mixin;

@Mixin(TitleScreen.class)
public abstract class TitleScreenMixin extends Screen {
    protected TitleScreenMixin(Text title) {
        super(title);
    }
    // Rendering is now handled entirely by FancyMenu.
    // This mixin is kept empty so we don't have to edit the mixins.json and risk mapping issues.
}
