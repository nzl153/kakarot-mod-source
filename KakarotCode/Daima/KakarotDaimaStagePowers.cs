using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Daima;

// One stage power at a time. Stats are hook modifiers rather than Strength/Dexterity stacks,
// so stage swaps never need a rollback that Artifact could block.
public abstract class KakarotDaimaStagePower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // Applied as real Strength/Dexterity deltas by KakarotDaima.SetStage so the buffs are visible.
    public abstract int StrengthBonus { get; }
    public abstract int DexterityBonus { get; }

    // Suffix of kakarot_combat_model_<ModelName>.png
    public abstract string ModelName { get; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthBonus),
        new DynamicVar("Dexterity", DexterityBonus),
    ];

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        KakarotFormVisuals.Refresh(oldOwner);
        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player == Owner.Player ? count + 1 : count;

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner != Owner.Player || !KakarotDaima.IsBanned(card);

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner.Player)
        {
            KakarotDaima.UpgradeFeatureCardIfSs4(card);
        }

        return Task.CompletedTask;
    }

    // Same fallback as SuperSaiyanFormPower: some runtime builds block IsPoweredAttack.
    internal static bool IsPoweredAttackSafe(ValueProp props)
    {
        try
        {
            return props.IsPoweredAttack();
        }
        catch (System.MethodAccessException)
        {
            return props == ValueProp.Move;
        }
        catch (System.MissingMethodException)
        {
            return props == ValueProp.Move;
        }
    }
}

public sealed class KakarotDaimaKidPower : KakarotDaimaStagePower
{
    public override int StrengthBonus => -2;
    public override int DexterityBonus => -1;
    public override string ModelName => "daima_kid";
}

public sealed class KakarotDaimaSs1Power : KakarotDaimaStagePower
{
    public override int StrengthBonus => 0;
    public override int DexterityBonus => 1;
    public override string ModelName => "daima_ss1";
    public override string CustomPackedIconPath => "super_saiyan_form_power.png".PowerImagePath();
    public override string CustomBigIconPath => "super_saiyan_form_power.png".BigPowerImagePath();
}

public sealed class KakarotDaimaSs3Power : KakarotDaimaStagePower
{
    public override int StrengthBonus => 3;
    public override int DexterityBonus => 2;
    public override string ModelName => "daima_ss3";
    public override string CustomPackedIconPath => "super_saiyan_form_power.png".PowerImagePath();
    public override string CustomBigIconPath => "super_saiyan_form_power.png".BigPowerImagePath();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && Owner.Player != null)
        {
            Flash();
            await PlayerCmd.GainStars(1m, Owner.Player);
        }
    }
}

public sealed class KakarotDaimaSs4KidPower : KakarotDaimaStagePower
{
    public override int StrengthBonus => 5;
    public override int DexterityBonus => 3;
    public override string ModelName => "daima_ss4_kid";

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && Owner.Player != null)
        {
            Flash();
            await PlayerCmd.GainStars(1m, Owner.Player);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player && cardPlay.Card.Type == CardType.Attack)
        {
            Flash();
            await KakarotDaima.GrantWish(Owner.Player);
        }
    }
}

public sealed class KakarotDaimaSs4Power : KakarotDaimaStagePower
{
    public const decimal VigorPerTurn = 2m;

    public override int StrengthBonus => 10;
    public override int DexterityBonus => 5;
    public override string ModelName => "daima_ss4";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthBonus),
        new DynamicVar("Dexterity", DexterityBonus),
        new DynamicVar("Vigor", VigorPerTurn),
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            Flash();
            await KakarotPowerCmd.Apply<VigorPower>(Owner, VigorPerTurn, Owner, null);
        }
    }
}

// Keep the completed counter until the player chooses to awaken.
public sealed class KakarotDaimaWishPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Goal", KakarotDaima.WishGoal)];
}
