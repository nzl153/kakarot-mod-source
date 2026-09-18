#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

public abstract class FriezaMinionBase : CustomMonsterModel
{
    public override bool HasDeathSfx => false;
    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (ReferenceEquals(combatState, CombatState))
        {
            FriezaBossVisuals.EnsureBreathing(Creature, 2.5f);
        }

        return Task.CompletedTask;
    }

    protected IEnumerable<Creature> LivingPlayerCreatures()
    {
        return CombatState.Players
            .Where(static player => player.Creature.IsAlive)
            .Select(static player => player.Creature);
    }

    protected Task Attack(
        int damage,
        int hits = 1,
        string hitFx = "vfx/vfx_attack_blunt",
        string? fallbackSfx = null,
        bool hitVfxAtBase = false)
    {
        AttackCommand command = DamageCmd.Attack(damage)
            .WithHitCount(hits)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx(hitFx, null, fallbackSfx);
        if (hitVfxAtBase)
        {
            command.WithHitVfxSpawnedAtBase();
        }

        return command.Execute(null);
    }

    protected static MonsterMoveStateMachine Alternating(MoveState first, MoveState second)
    {
        first.FollowUpState = second;
        second.FollowUpState = first;
        return new MonsterMoveStateMachine([first, second], first);
    }

    protected const string ArrivalMoveId = "ARRIVAL_POSE";

    // 第二波是在玩家回合中途登场的（弗利萨掉到半血就当场召唤），
    // 那时玩家的能量与格挡已经按旧的敌人数量分配完了，紧接着挨两份新输出没有任何招架余地。
    // 让新到场的成员先摆一回合架势：意图照常显示，玩家看得见、下回合才需要应对。
    protected MonsterMoveStateMachine ArrivingAlternating(MoveState first, MoveState second)
    {
        first.FollowUpState = second;
        second.FollowUpState = first;
        var arrival = new MoveState(ArrivalMoveId, ArrivalPose, new StunIntent())
        {
            FollowUpState = first,
        };
        return new MonsterMoveStateMachine([arrival, first, second], arrival);
    }

    private Task ArrivalPose(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(0.85f, 0.35f, 1.3f, 1f));
        return Task.CompletedTask;
    }
}

public sealed class FriezaGuldo : FriezaMinionBase
{
    private const string VisualPath = "res://Kakarot/Scenes/Frieza/GuldoVisual.tscn";

    public override string CustomVisualPath => VisualPath;
    protected override string VisualsPath => VisualPath;
    public override int MinInitialHp => 38;
    public override int MaxInitialHp => 38;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bind = new MoveState("TIME_BIND", TimeBind, new DebuffIntent());
        var strike = new MoveState("PSYCHIC_STRIKE", PsychicStrike, new SingleAttackIntent(5));
        return Alternating(bind, strike);
    }

    private async Task TimeBind(IReadOnlyList<Creature> targets)
    {
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(0.18f, 1.2f, 0.8f, 1f));
        FriezaBossVisuals.PlayTargetPulse(
            targets,
            new Color(0.1f, 1.25f, 0.78f, 0.9f),
            0.9f);
        await KakarotPowerCmd.Apply<WeakPower>(targets, 1m, Creature, null);
    }

    private Task PsychicStrike(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(0.18f, 1.2f, 0.8f, 1f));
        FriezaBossVisuals.PlayTargetPulse(
            LivingPlayerCreatures(),
            new Color(0.1f, 1.25f, 0.78f, 0.9f),
            1.15f);
        return Attack(5, hitFx: "vfx/vfx_attack_lightning");
    }
}

public sealed class FriezaRecoome : FriezaMinionBase
{
    private const string VisualPath = "res://Kakarot/Scenes/Frieza/RecoomeVisual.tscn";

    public override string CustomVisualPath => VisualPath;
    protected override string VisualsPath => VisualPath;
    public override int MinInitialHp => 65;
    public override int MaxInitialHp => 65;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bomber = new MoveState("RECOOME_BOMBER", RecoomeBomber, new SingleAttackIntent(10));
        var charge = new MoveState("RECOOME_CHARGE", RecoomeCharge, new BuffIntent());
        var heavy = new MoveState("RECOOME_HEAVY", RecoomeHeavy, new SingleAttackIntent(15));
        bomber.FollowUpState = charge;
        charge.FollowUpState = heavy;
        heavy.FollowUpState = bomber;
        return new MonsterMoveStateMachine([bomber, charge, heavy], bomber);
    }

    private Task RecoomeCharge(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(1.4f, 0.45f, 0.4f, 1f));
        return Task.CompletedTask;
    }

    private Task RecoomeBomber(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayAttackMotion(Creature, 24f);
        FriezaBossVisuals.PlayEnergyBolts(
            Creature,
            LivingPlayerCreatures(),
            new Color(1.5f, 0.42f, 0.08f, 1f),
            new Color(1.5f, 1.05f, 0.25f, 1f),
            sizeMultiplier: 1.1f,
            arcHeight: 70f);
        return Attack(10);
    }

    private async Task RecoomeHeavy(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayAttackMotion(Creature, 52f);
        FriezaBossVisuals.PlayTargetPulse(
            LivingPlayerCreatures(),
            new Color(1.5f, 0.4f, 0.08f, 0.9f),
            1.35f);
        await Attack(
            15,
            hitFx: "vfx/vfx_heavy_blunt",
            fallbackSfx: "heavy_attack.mp3",
            hitVfxAtBase: true);
    }
}

public sealed class FriezaCaptainGinyu : FriezaMinionBase
{
    private const string VisualPath = "res://Kakarot/Scenes/Frieza/CaptainGinyuVisual.tscn";

    public override string CustomVisualPath => VisualPath;
    protected override string VisualsPath => VisualPath;
    public override int MinInitialHp => 90;
    public override int MaxInitialHp => 90;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var combo = new MoveState("GINYU_COMBO", GinyuCombo, new MultiAttackIntent(6, 2));
        var command = new MoveState("FIGHTING_COMMAND", FightingCommand, new DefendIntent(), new BuffIntent());
        return ArrivingAlternating(combo, command);
    }

    private async Task FightingCommand(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(0.85f, 0.35f, 1.3f, 1f));
        foreach (Creature enemy in CombatState.Enemies.Where(static enemy => enemy.IsAlive))
        {
            await CreatureCmd.GainBlock(enemy, 8m, ValueProp.Move, null);
            await KakarotPowerCmd.Apply<StrengthPower>(enemy, 1m, Creature, null);
        }
    }

    private Task GinyuCombo(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayAttackMotion(Creature, 42f);
        return Attack(6, 2, "vfx/vfx_attack_slash");
    }
}

public sealed class FriezaBurterJeice : FriezaMinionBase
{
    private const string VisualPath = "res://Kakarot/Scenes/Frieza/BurterJeiceVisual.tscn";

    public override string CustomVisualPath => VisualPath;
    protected override string VisualsPath => VisualPath;
    public override int MinInitialHp => 80;
    public override int MaxInitialHp => 80;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var purpleComet = new MoveState("PURPLE_COMET", PurpleComet, new MultiAttackIntent(4, 3));
        var crusherBall = new MoveState("CRUSHER_BALL", CrusherBall, new SingleAttackIntent(12), new DebuffIntent());
        return ArrivingAlternating(purpleComet, crusherBall);
    }

    private async Task CrusherBall(IReadOnlyList<Creature> targets)
    {
        FriezaBossVisuals.PlayAttackMotion(Creature, 24f);
        FriezaBossVisuals.PlayEnergyBolts(
            Creature,
            LivingPlayerCreatures(),
            new Color(1.5f, 0.1f, 0.12f, 1f),
            new Color(1.5f, 0.55f, 0.15f, 1f),
            sizeMultiplier: 1.45f,
            arcHeight: 118f);
        await Attack(12, hitFx: "vfx/vfx_starry_impact");
        await KakarotPowerCmd.Apply<VulnerablePower>(targets, 1m, Creature, null);
    }

    private Task PurpleComet(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayAttackMotion(Creature, 34f);
        FriezaBossVisuals.PlayEnergyBolts(
            Creature,
            LivingPlayerCreatures(),
            new Color(0.18f, 0.72f, 1.5f, 1f),
            new Color(1.5f, 0.18f, 0.16f, 1f),
            hits: 3,
            sizeMultiplier: 0.95f,
            arcHeight: 92f);
        return Attack(4, 3, "vfx/vfx_attack_blunt");
    }
}
