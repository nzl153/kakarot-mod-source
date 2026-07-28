using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace KakarotMod.KakarotCode.Characters;

// Kept for compatibility with saves and mods that reference the former pool.
public class KakarotCoopColorlessCardPool : CustomCardPoolModel
{
    public override string Title => "Kakarot Coop";

    public override string BigEnergyIconPath => ImageHelper.GetImagePath("packed/sprite_fonts/colorless_energy_icon.png");
    public override string TextEnergyIconPath => ImageHelper.GetImagePath("packed/sprite_fonts/colorless_energy_icon.png");

    public override float H => 0f;
    public override float S => 0f;
    public override float V => 0.75f;

    public override Color DeckEntryCardColor => new("b0b0b0");
    public override Color EnergyOutlineColor => new("6a6a6a");
    public override bool IsColorless => true;
}
