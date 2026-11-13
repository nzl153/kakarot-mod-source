using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace KakarotMod.KakarotCode.DragonBalls;

public sealed class KakarotDragonBallReward(Player player, bool found) : CustomReward(player)
{
    [CustomEnum]
    public static RewardType DragonBall;

    private bool _claimed;

    protected override RewardType RewardType => DragonBall;
    public override LocString Description => new("gameplay_ui", "KAKAROTMOD-DRAGON_BALL_REWARD");
    public override bool IsPopulated => true;
    protected override string IconPath => Player.GetRelic<KakarotSevenDragonBalls>()?.SelectedSet switch
    {
        KakarotDragonBallSet.Namek => "res://Kakarot/Images/UI/kakarot_dragon_ball_namek.png",
        KakarotDragonBallSet.Super => "res://Kakarot/Images/UI/kakarot_dragon_ball_super.png",
        KakarotDragonBallSet.BlackStar => "res://Kakarot/Images/UI/kakarot_dragon_ball_blackstar.png",
        _ => "res://Kakarot/Images/UI/kakarot_dragon_ball_earth.png",
    };
    public override CreateRewardFromSave<CustomReward> DeserializeMethod => CreateFromSerializable;

    public override void Populate() { }
    public override void MarkContentAsSeen() { }

    protected override Task<bool> OnSelect()
    {
        var relic = Player.GetRelic<KakarotSevenDragonBalls>();
        if (!_claimed && relic is { CanCollect: true })
        {
            if (found)
            {
                relic.CollectBall();
            }

            if (LocalContext.IsMe(Player))
            {
                var text = new LocString("gameplay_ui", found
                    ? "KAKAROTMOD-DRAGON_BALL_FOUND" : "KAKAROTMOD-DRAGON_BALL_NOT_FOUND").GetFormattedText();
                NGame.Instance.AddChildSafely(NFullscreenTextVfx.Create(text));
            }
        }

        _claimed = true;
        return Task.FromResult(true);
    }

    public override SerializableReward ToSerializable() => new()
    {
        RewardType = DragonBall,
        WasGoldStolenBack = _claimed,
        GoldAmount = found ? 1 : 0,
    };

    public static KakarotDragonBallReward CreateFromSerializable(SerializableReward save, Player player) =>
        new(player, save.GoldAmount == 1)
    {
        _claimed = save.WasGoldStolenBack,
    };
}
