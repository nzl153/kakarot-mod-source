using BaseLib.Abstracts;
using Godot;

namespace KakarotMod.KakarotCode.Characters;

public class KakarotRelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "kakarot";
    public override Color LabOutlineColor => Kakarot.Color;
}
