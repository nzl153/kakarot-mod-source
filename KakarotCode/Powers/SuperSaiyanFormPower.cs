using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Characters;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ValueProp = MegaCrit.Sts2.Core.ValueProps.ValueProp;

namespace KakarotMod.KakarotCode.Powers;

// Shared state and upkeep for Super Saiyan stages 1-3.
public sealed class SuperSaiyanFormPower : KakarotPower
{
    private int _tier;
    private int _strengthBonus;
    private int _dexterityBonus;
    private int _appliedStrengthBonus;
    private int _appliedDexterityBonus;

    // SS3 leak count grows with turns spent in the form.
    private int _ss3LeakStreak;

    // The first SS3 Attack is doubled as one card, including all hits.
    private bool _ss3FirstAttackDoublingConsumed;

    private CardModel _ss3DoubledAttackCard;

    // Tracks the first-Attack rage trigger for SS1 and SS2.
    private bool _firstAttackBonusRageGrantedThisTurn;

    public bool FirstAttackBonusRageGrantedThisTurn => _firstAttackBonusRageGrantedThisTurn;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override string SmartDescriptionLocKey => _tier switch
    {
        1 => "KAKAROTMOD-SUPER_SAIYAN_FORM_POWER.smart_t1",
        2 => "KAKAROTMOD-SUPER_SAIYAN_FORM_POWER.smart_t2",
        3 => "KAKAROTMOD-SUPER_SAIYAN_FORM_POWER.smart_t3",
        _ => base.SmartDescriptionLocKey,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", 3m),
        new DynamicVar("Dexterity", 3m),
        new DynamicVar("Tier", 1m),
    ];

    public void ConfigureTier(int tier, bool firstAttackBonusRageGrantedThisTurn = false)
    {
        AssertMutable();
        _tier = tier;
        _firstAttackBonusRageGrantedThisTurn = firstAttackBonusRageGrantedThisTurn;
        _ss3FirstAttackDoublingConsumed = tier == 3 && firstAttackBonusRageGrantedThisTurn;
        if (tier != 3)
        {
            _ss3LeakStreak = 0;
        }

        (_strengthBonus, _dexterityBonus) = tier switch
        {
            1 => (3, 3),
            2 => (5, 4),
            3 => (8, 5),
            _ => (0, 0),
        };

        DynamicVars["Tier"].BaseValue = tier;
        DynamicVars["Strength"].BaseValue = _strengthBonus;
        DynamicVars["Dexterity"].BaseValue = _dexterityBonus;
    }

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        var strengthBefore = target.GetPower<StrengthPower>()?.Amount ?? 0;
        await KakarotPowerCmd.Apply<StrengthPower>(target, _strengthBonus, target, cardSource);
        _appliedStrengthBonus = (target.GetPower<StrengthPower>()?.Amount ?? 0) - strengthBefore;

        var dexterityBefore = target.GetPower<DexterityPower>()?.Amount ?? 0;
        await KakarotPowerCmd.Apply<DexterityPower>(target, _dexterityBonus, target, cardSource);
        _appliedDexterityBonus = (target.GetPower<DexterityPower>()?.Amount ?? 0) - dexterityBefore;
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, System.Collections.Generic.IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side != Owner.Side || Owner.Player == null)
        {
            return;
        }

        var cost = _tier switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            _ => 0,
        };

        if (cost <= 0)
        {
            return;
        }

        var stars = Owner.Player.PlayerCombatState.Stars;
        if (stars < cost)
        {
            Flash();
            var owner = Owner;
            var nextTier = _tier - 1;
            await PowerCmd.Remove<SuperSaiyanFormPower>(owner);

            if (nextTier > 0)
            {
                var downgradedForm = (SuperSaiyanFormPower)ModelDb.Power<SuperSaiyanFormPower>().ToMutable();
                downgradedForm.ConfigureTier(nextTier, _firstAttackBonusRageGrantedThisTurn);
                await KakarotPowerCmd.Apply(downgradedForm, owner, nextTier, owner, null);
                KakarotFormVisuals.Refresh(owner);
            }

            return;
        }

        Flash();
        await PlayerCmd.LoseStars(cost, Owner.Player);
    }

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            _firstAttackBonusRageGrantedThisTurn = false;
            _ss3FirstAttackDoublingConsumed = false;
            _ss3DoubledAttackCard = null;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        if (_tier == 3)
        {
            _ss3LeakStreak++;
            var leakCount = System.Math.Min(_ss3LeakStreak, 2);
            var cs = player.Creature?.CombatState;
            for (var i = 0; i < leakCount; i++)
            {
                var leak = cs != null
                    ? cs.CreateCard<KakarotKiLeak>(player)
                    : CombatState.CreateCard<KakarotKiLeak>(player);
                await CardPileCmd.AddGeneratedCardToCombat(leak, PileType.Draw, player);
            }

            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (_tier == 3
            && !_ss3FirstAttackDoublingConsumed
            && cardPlay.Card.Owner == Owner.Player
            && cardPlay.Card.Type == CardType.Attack)
        {
            _ss3DoubledAttackCard = cardPlay.Card;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_tier == 3
            && _ss3DoubledAttackCard != null
            && cardPlay.Card == _ss3DoubledAttackCard)
        {
            _ss3FirstAttackDoublingConsumed = true;
            _ss3DoubledAttackCard = null;
        }

        if (_tier >= 1
            && !_firstAttackBonusRageGrantedThisTurn
            && cardPlay.Card.Owner == Owner.Player
            && Owner.Player.Character is KakarotCharacter
            && cardPlay.Card.Type == CardType.Attack)
        {
            Flash();
            await PlayerCmd.GainStars(_tier, Owner.Player);
            _firstAttackBonusRageGrantedThisTurn = true;
        }
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player && _tier == 2)
        {
            await PlayerCmd.GainEnergy(1m, player);
        }
    }

#if STS2_BETA
    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
#endif
    {
        var isPoweredAttack = IsPoweredAttackSafe(props);
        if (_tier != 3
            || _ss3FirstAttackDoublingConsumed
            || _ss3DoubledAttackCard == null
            || cardSource != _ss3DoubledAttackCard
            || dealer != Owner
            || !isPoweredAttack)
        {
            return 1m;
        }

        return 2m;
    }

    // Some runtime builds block ValuePropExtensions.IsPoweredAttack; use a narrow fallback.
    private static bool IsPoweredAttackSafe(ValueProp props)
    {
        try
        {
            return ValuePropExtensions.IsPoweredAttack(props);
        }
        catch (MethodAccessException)
        {
            // Fallback heuristic: only apply on direct move/attack value props.
            return props == ValueProp.Move;
        }
        catch (MissingMethodException)
        {
            return props == ValueProp.Move;
        }
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await RemoveOwnedStatBonus<StrengthPower>(oldOwner, _appliedStrengthBonus);
        await RemoveOwnedStatBonus<DexterityPower>(oldOwner, _appliedDexterityBonus);
        await PowerCmd.Remove<SuperSaiyanRagePower>(oldOwner);
        KakarotFormVisuals.Refresh(oldOwner);
    }

    private static async Task RemoveOwnedStatBonus<T>(Creature owner, int amount)
        where T : PowerModel
    {
        if (amount == 0 || owner.GetPower<T>() is not { } statPower)
        {
            return;
        }

        // This is rollback of a form-owned bonus, not a debuff. Applying a negative
        // Power would let Artifact block the rollback and permanently duplicate stats.
        statPower.SetAmount(statPower.Amount - amount);
        if (statPower.ShouldRemoveDueToAmount())
        {
            await PowerCmd.Remove(statPower);
        }
    }
}
