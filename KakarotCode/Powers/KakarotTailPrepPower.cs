using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>Marks your next Attack as Wild and gives temporary Strength for that Attack only.</summary>
public sealed class KakarotTailPrepPower : KakarotPower
{
    private decimal _damageBonus = 4m;
    private CardModel _nextAttackCard;
    private bool _temporaryStrengthApplied;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamageBonus", 4m)];

    protected override string SmartDescriptionLocKey => "KAKAROTMOD-KAKAROT_TAIL_PREP_POWER.smartDescription";

    public void Configure(decimal damageBonus)
    {
        AssertMutable();
        _damageBonus = damageBonus;
        DynamicVars["DamageBonus"].BaseValue = damageBonus;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        // Previous marked attack was cancelled (disarmed / shackled / returned to hand);
        // clean up its leaked state before applying to the new card.
        if (_nextAttackCard != null && cardPlay.Card != _nextAttackCard)
        {
            if (_temporaryStrengthApplied && _damageBonus > 0)
            {
                await KakarotPowerCmd.Apply<StrengthPower>(Owner, -_damageBonus, Owner, null, silent: true);
                _temporaryStrengthApplied = false;
            }

            _nextAttackCard = null;
        }

        if (_nextAttackCard != null)
        {
            return;
        }

        _nextAttackCard = cardPlay.Card;
        _nextAttackCard.AddKeyword(KakarotWildKeyword.Wild);
        KakarotWildHelper.EnsureWildActsUpgradedIfSs4(Owner.Player, _nextAttackCard);

        if (_damageBonus > 0)
        {
            await KakarotPowerCmd.Apply<StrengthPower>(Owner, _damageBonus, Owner, null, silent: true);
            _temporaryStrengthApplied = true;
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_nextAttackCard == null || cardPlay.Card != _nextAttackCard)
        {
            return;
        }

        Flash();
        if (_temporaryStrengthApplied && _damageBonus > 0)
        {
            await KakarotPowerCmd.Apply<StrengthPower>(choiceContext, Owner, -_damageBonus, Owner, null, silent: true);
            _temporaryStrengthApplied = false;
        }

        await PowerCmd.Remove(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_temporaryStrengthApplied && _damageBonus > 0)
        {
            await KakarotPowerCmd.Apply<StrengthPower>(oldOwner, -_damageBonus, oldOwner, null, silent: true);
            _temporaryStrengthApplied = false;
        }
    }
}
