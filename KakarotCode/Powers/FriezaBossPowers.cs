#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Monsters.Frieza;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class FriezaEmperorGuardPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Reduction", 25m)];

#if STS2_BETA
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (target != Owner || cardSource?.Type != CardType.Attack)
        {
            return 1m;
        }

        bool forceMemberAlive = Owner.CombatState?.Enemies.Any(static creature =>
            creature.IsAlive && creature.Monster is FriezaMinionBase) == true;
        return forceMemberAlive ? 0.75m : 1m;
    }
}

public sealed class FriezaGoldenStaminaPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("StrengthLeft", 4m)];
}

public sealed class FriezaDeathSaucerPower : KakarotPower
{
    private const int TargetStride = 10_000;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Damage;

    public int TargetIndex => Math.Max(0, Amount / TargetStride - 1);
    public int Damage => Math.Max(0, Amount % TargetStride - 1);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", 0m),
        new DynamicVar("Threshold", 70m),
    ];

    public void SetProgress(int targetIndex, int damage, int threshold)
    {
        AssertMutable();
        int cappedDamage = Math.Clamp(damage, 0, threshold);
        DynamicVars["Damage"].BaseValue = cappedDamage;
        DynamicVars["Threshold"].BaseValue = threshold;
        SetAmount((Math.Max(0, targetIndex) + 1) * TargetStride + cappedDamage + 1);
    }
}

public sealed class FriezaSupernovaChargePower : KakarotPower
{
    private const int TurnStride = 10_000;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Turns;

    public int Turns => Math.Max(0, Amount / TurnStride);
    public int Damage => Math.Max(0, Amount % TurnStride - 1);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", 3m),
        new DynamicVar("Damage", 0m),
        new DynamicVar("Threshold", 180m),
    ];

    public void SetProgress(int turns, int damage, int threshold)
    {
        AssertMutable();
        int cappedDamage = Math.Clamp(damage, 0, threshold);
        DynamicVars["Turns"].BaseValue = turns;
        DynamicVars["Damage"].BaseValue = cappedDamage;
        DynamicVars["Threshold"].BaseValue = threshold;
        SetAmount(Math.Max(0, turns) * TurnStride + cappedDamage + 1);
    }
}

public sealed class FriezaBlackPressurePower : KakarotPower
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("SafeCards", 3m),
        new DynamicVar("Played", 0m),
    ];

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            SetAmount(1);
            DynamicVars["Played"].BaseValue = 0m;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;
        if (card.Owner != Owner.Player || card.Rarity == CardRarity.Basic)
        {
            return;
        }

        int nonBasicCardsPlayed = Amount;
        SetAmount(nonBasicCardsPlayed + 1);
        DynamicVars["Played"].BaseValue = nonBasicCardsPlayed;
        if (nonBasicCardsPlayed <= 3 || card.Pile?.Type == PileType.Exhaust)
        {
            return;
        }

        Flash();
        await CardCmd.Exhaust(choiceContext, card);
    }
}
