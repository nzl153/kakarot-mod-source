using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Potions;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace KakarotMod.KakarotCode.Events;

public sealed class KakarotKorinTowerTrial : CustomEventModel
{
    private const decimal MaxHpLoss = 8m;
    private const string PortraitPath =
        "res://images/events/kakarot_korin_tower_trial.png";
    private const string FallbackPortraitPath =
        "res://images/events/kakarot_tournament_cafeteria.png";

    public override string CustomInitialPortraitPath =>
        ResourceLoader.Exists(PortraitPath) ? PortraitPath : FallbackPortraitPath;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(
                    this,
                    ChaseKorin,
                    $"{Id.Entry}.pages.INITIAL.options.CHASE_KORIN",
                    HoverTipFactory.FromRelic<KakarotSacredWaterJug>())
                .ThatDecreasesMaxHp(MaxHpLoss),
            new EventOption(
                this,
                TakeSenzuJar,
                $"{Id.Entry}.pages.INITIAL.options.TAKE_SENZU_JAR",
                HoverTipFactory.FromPotion(ModelDb.Potion<KakarotSenzuJar>())),
            new EventOption(
                this,
                Leave,
                $"{Id.Entry}.pages.INITIAL.options.LEAVE"),
        ];
    }

    private async Task ChaseKorin()
    {
        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            MaxHpLoss,
            isFromCard: false);
        await RelicCmd.Obtain<KakarotSacredWaterJug>(Owner);
        SetEventFinished(PageDescription("CHASE_KORIN"));
    }

    private async Task TakeSenzuJar()
    {
        await RewardsCmd.OfferCustom(
            Owner,
            [new PotionReward(ModelDb.Potion<KakarotSenzuJar>().ToMutable(), Owner)]);
        SetEventFinished(PageDescription("TAKE_SENZU_JAR"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }
}
