using System.Collections.Generic;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Cards.Basic;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace KakarotMod.KakarotCode.Characters;

public class Kakarot : PlaceholderCharacterModel
{
    public const string CharacterId = "Kakarot";

    public override string PlaceholderID => "ironclad";

    public static readonly Color Color = new("f39c12");

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override int StartingHp => 80;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<KakarotStrike>(),
        ModelDb.Card<KakarotStrike>(),
        ModelDb.Card<KakarotStrike>(),
        ModelDb.Card<KakarotStrike>(),
        ModelDb.Card<KakarotEyeBeam>(),
        ModelDb.Card<KakarotDefend>(),
        ModelDb.Card<KakarotDefend>(),
        ModelDb.Card<KakarotDefend>(),
        ModelDb.Card<KakarotDefend>(),
        ModelDb.Card<KakarotChargeUp>(),
        ModelDb.Card<KakarotKaioken>(),
    ];

    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<SaiyanBlood>()];

    public override CardPoolModel CardPool => ModelDb.CardPool<KakarotCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<KakarotRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<KakarotPotionPool>();

    // 1.1 entry points for custom presentation assets.
    public override string CustomVisualPath => "res://Kakarot/Scenes/KakarotVisual.tscn";
    public override string CustomCharacterSelectBg => "res://Kakarot/Scenes/CharSelect/KakarotCharSelectBg.tscn";

    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    /// <summary>
    /// Architect victory sequence shuffles these VFX for the “player attacks Architect” beat.
    /// Placeholder characters must still return a non-empty list or the flow can misbehave.
    /// </summary>
    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter"
        ];
    }
}
