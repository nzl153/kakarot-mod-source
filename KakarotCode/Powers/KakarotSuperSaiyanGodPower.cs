using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// Super Saiyan God: +8 Strength / +5 Dexterity; while active, cards in hand (and while resolving from hand) have Energy cost reduced by 1 (min 0). X-cost cards are not modified.
/// </summary>
public sealed class KakarotSuperSaiyanGodPower : KakarotPower
{
    private const int StatBonus = 8;
    private const int DexterityBonus = 5;

    private int _strengthBonus = StatBonus;
    private int _dexterityBonus = DexterityBonus;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StatBonus),
        new DynamicVar("Dexterity", DexterityBonus),
    ];

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(target, _strengthBonus, target, cardSource);
        await KakarotPowerCmd.Apply<DexterityPower>(target, _dexterityBonus, target, cardSource);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(oldOwner, -_strengthBonus, oldOwner, null);
        await KakarotPowerCmd.Apply<DexterityPower>(oldOwner, -_dexterityBonus, oldOwner, null);
        KakarotFormVisuals.Refresh(oldOwner);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!IsOwnersCardInHandOrPlay(card))
        {
            return false;
        }

        if (card.EnergyCost.CostsX)
        {
            return false;
        }

        if (originalCost <= 0)
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - 1m);
        return modifiedCost != originalCost;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        return false;
    }

    private bool IsOwnersCardInHandOrPlay(CardModel card)
    {
        if (card.Owner?.Creature != Owner)
        {
            return false;
        }

        return card.Pile?.Type is PileType.Hand or PileType.Play;
    }
}
