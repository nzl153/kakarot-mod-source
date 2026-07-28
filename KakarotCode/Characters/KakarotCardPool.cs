using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace KakarotMod.KakarotCode.Characters;

public class KakarotCardPool : CustomCardPoolModel
{
    public override string Title => Kakarot.CharacterId;

    public override string EnergyColorName => "kakarot";
    public override string BigEnergyIconPath => ImageHelper.GetImagePath("packed/sprite_fonts/kakarot_energy_icon.png");
    public override string TextEnergyIconPath => ImageHelper.GetImagePath("packed/sprite_fonts/kakarot_energy_icon.png");

    // Gold-ish frame tint closer to Regent palette.
    public override float H => 0.135f;
    public override float S => 0.85f;
    public override float V => 1.0f;

    public override Color DeckEntryCardColor => new("E8B64A");
    public override Color EnergyOutlineColor => new("7A4F12");
    public override bool IsColorless => false;
}
