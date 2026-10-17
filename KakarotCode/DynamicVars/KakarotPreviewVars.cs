#nullable enable
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace KakarotMod.KakarotCode.DynamicVars;

// Preview code may update PreviewValue only; BaseValue participates in synchronized rollback state.
// Canonical cards have no Owner, so calculations must short-circuit outside a run or combat.

// Uses the same self-damage formula as card resolution.
public sealed class KakarotReducedHpLossVar : DynamicVar
{
    public KakarotReducedHpLossVar(string name, decimal baseValue)
        : base(name, baseValue)
    {
    }

    private decimal Compute()
    {
        if (_owner is not CardModel card || card.CombatState == null)
        {
            return BaseValue;
        }

        try
        {
            return KakarotTrainingSelfHpCost.Resolve(BaseValue, card.Owner?.Creature);
        }
        catch (CanonicalModelException)
        {
            return BaseValue;
        }
        catch (MutableModelException)
        {
            return BaseValue;
        }
    }

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        PreviewValue = Compute();
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        return Compute();
    }

    public override string ToString()
    {
        return ((int)Compute()).ToString();
    }
}
