using System.Collections.Generic;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Cards.Basic;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace KakarotMod.KakarotCode.Characters;

public class Kakarot : PlaceholderCharacterModel
{
    private static readonly string[] MultiplayerHandAssetPaths =
    [
        "res://images/ui/hands/multiplayer_hand_kakarot_point.png",
        "res://images/ui/hands/multiplayer_hand_kakarot_rock.png",
        "res://images/ui/hands/multiplayer_hand_kakarot_paper.png",
        "res://images/ui/hands/multiplayer_hand_kakarot_scissors.png",
    ];

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

    public override string CustomVisualPath => "res://Kakarot/Scenes/KakarotVisual.tscn";
    public override string CustomCharacterSelectBg => "res://Kakarot/Scenes/CharSelect/KakarotCharSelectBg.tscn";

    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override Control CustomIcon
    {
        get
        {
            var icon = new TextureRect
            {
                Texture = PreloadManager.Cache.GetTexture2D(CustomIconTexturePath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }

    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
    public override string CustomArmPointingTexturePath => MultiplayerHandAssetPaths[0];
    public override string CustomArmRockTexturePath => MultiplayerHandAssetPaths[1];
    public override string CustomArmPaperTexturePath => MultiplayerHandAssetPaths[2];
    public override string CustomArmScissorsTexturePath => MultiplayerHandAssetPaths[3];
    // 选人→战斗的入场过场：气爆式阈值展开材质（由 BaseLib Harmony 补丁接进原生 CharacterSelectTransitionPath；
    // NTransition 会 tween 材质的 threshold，遮罩灰度决定覆盖先后：中心白最先、四角黑最后）。
    public override string CustomCharacterSelectTransitionPath => "res://Kakarot/Materials/Transitions/kakarot_transition_mat.tres";

    // 这个 FMOD 事件不存在，故意的：KakarotAudioPatches 认这个字符串来补播我们自己的 transition.wav。
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_kakarot";

    protected override IEnumerable<string> ExtraAssetPaths => MultiplayerHandAssetPaths;

    // Placeholder characters need at least one Architect victory effect.
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
