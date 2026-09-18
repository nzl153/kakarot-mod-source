#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

public sealed class FriezaBoss : CustomMonsterModel
{
    // Encounter balance values are centralized here.
    private const int WhiteHp = 290;
    private const int GoldenHp = 450;
    private const int BlackHp = 520;
    private const int GoldenPhaseStrength = 4;
    private const int SaucerDamagePerPlayer = 65;
    private const decimal SupernovaThresholdRatio = 0.25m;

    // 引爆是直接扣血，不是攻击，所以不参与力量/易伤结算。
    private const decimal SupernovaDetonateHpLoss = 70m;

    private int _phase = 1;
    private int _phaseMoveIndex;
    private bool _phaseTransitionPending;
    private bool _goldenTransformQueued;
    private bool _secondWavePending;
    private bool _whiteOpeningDone;
    private bool _secondWaveSummoned;
    private bool _goldenOpeningDone;
    private bool _blackOpeningDone;
    private int _goldenStrengthRemaining;
    private int _blackNormalActionsUntilSupernova = 2;

    private MoveState? _whiteSummon;
    private MoveState? _goldenTransform;
    private MoveState? _psychicPressure;
    private MoveState? _contemptuousFinger;
    private MoveState? _deathBeam;
    private MoveState? _emperorOrder;
    private MoveState? _goldenBarrage;
    private MoveState? _deathSaucer;
    private MoveState? _resolveSaucer;
    private MoveState? _goldenHeavy;
    private MoveState? _blackFlash;
    private MoveState? _blackBurst;
    private MoveState? _emperorShockwave;
    private MoveState? _finalBeam;
    private MoveState? _supernovaStart;
    private MoveState? _supernovaCharge;
    private MoveState? _supernovaDetonate;
    private MoveState? _supernovaStun;

    private const string FriezaVisualPath = "res://Kakarot/Scenes/Frieza/FriezaBossVisual.tscn";

    public override string CustomVisualPath => FriezaVisualPath;
    protected override string VisualsPath => FriezaVisualPath;
    public override int MinInitialHp => WhiteHp;
    public override int MaxInitialHp => WhiteHp;
    public override bool HasDeathSfx => false;
    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    public int Phase
    {
        get => _phase;
        private set
        {
            AssertMutable();
            _phase = value;
        }
    }

    private int PhaseMoveIndex
    {
        get => _phaseMoveIndex;
        set
        {
            AssertMutable();
            _phaseMoveIndex = value;
        }
    }

    private bool PhaseTransitionPending
    {
        get => _phaseTransitionPending;
        set
        {
            AssertMutable();
            _phaseTransitionPending = value;
        }
    }

    private bool GoldenTransformQueued
    {
        get => _goldenTransformQueued;
        set
        {
            AssertMutable();
            _goldenTransformQueued = value;
        }
    }

    private bool SecondWavePending
    {
        get => _secondWavePending;
        set
        {
            AssertMutable();
            _secondWavePending = value;
        }
    }

    private bool WhiteOpeningDone
    {
        get => _whiteOpeningDone;
        set
        {
            AssertMutable();
            _whiteOpeningDone = value;
        }
    }

    private bool SecondWaveSummoned
    {
        get => _secondWaveSummoned;
        set
        {
            AssertMutable();
            _secondWaveSummoned = value;
        }
    }

    private bool GoldenOpeningDone
    {
        get => _goldenOpeningDone;
        set
        {
            AssertMutable();
            _goldenOpeningDone = value;
        }
    }

    private bool BlackOpeningDone
    {
        get => _blackOpeningDone;
        set
        {
            AssertMutable();
            _blackOpeningDone = value;
        }
    }

    private int GoldenStrengthRemaining
    {
        get => _goldenStrengthRemaining;
        set
        {
            AssertMutable();
            _goldenStrengthRemaining = value;
        }
    }

    private int BlackNormalActionsUntilSupernova
    {
        get => _blackNormalActionsUntilSupernova;
        set
        {
            AssertMutable();
            _blackNormalActionsUntilSupernova = value;
        }
    }

    public bool IsSaucerPending => GetSaucerPower() != null;
    public bool IsAwaitingGoldenTransformation => Phase == 1 && PhaseTransitionPending;
    public ulong SaucerTargetNetId
    {
        get
        {
            FriezaDeathSaucerPower? power = GetSaucerPower();
            return power != null && power.TargetIndex < CombatState.Players.Count
                ? CombatState.Players[power.TargetIndex].NetId
                : 0UL;
        }
    }

    private int PlayerCount => Math.Max(1, CombatState.Players.Count);
    private int SaucerThreshold => SaucerDamagePerPlayer * PlayerCount;
    // 血量上限本身已经过 ScaleHpForMultiplayer 按人数缩放，所以这里不再乘 PlayerCount。
    private int SupernovaThreshold => (int)Math.Ceiling(Creature.MaxHp * SupernovaThresholdRatio);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await KakarotPowerCmd.Apply<FriezaEmperorGuardPower>(Creature, 1m, Creature, null);
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!ReferenceEquals(combatState, CombatState))
        {
            return;
        }

        FriezaBossVisuals.SetPhase(Creature, Phase, animate: false);
        FriezaBossVisuals.EnsureBreathing(Creature, 3.5f);
        FriezaBossVisuals.SyncSaucerTarget(this);

        if (side != Creature.Side || Phase != 3)
        {
            return;
        }

        // 仙豆或神器只提供一回合喘息；黑金阶段下回合补回威压，但不覆盖现有计数。
        foreach (Player player in CombatState.Players.Where(static player => player.Creature.IsAlive))
        {
            if (!player.Creature.HasPower<FriezaBlackPressurePower>())
            {
                await KakarotPowerCmd.Apply<FriezaBlackPressurePower>(player.Creature, 1m, Creature, null);
            }
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var decision = new ConditionalBranchState("FRIEZA_DECISION");

        _whiteSummon = new MoveState("WHITE_SUMMON", WhiteSummon, new SummonIntent());
        _goldenTransform = new MoveState(
            "GOLDEN_TRANSFORM",
            TransformToGolden,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        _psychicPressure = new MoveState(
            "PSYCHIC_PRESSURE",
            PsychicPressure,
            new SingleAttackIntent(17),
            new DebuffIntent());
        _contemptuousFinger = new MoveState(
            "CONTEMPTUOUS_FINGER",
            ContemptuousFinger,
            new MultiAttackIntent(14, 2),
            new DebuffIntent());
        _deathBeam = new MoveState("DEATH_BEAM", DeathBeam, new SingleAttackIntent(() => Phase == 1 ? 24m : 30m));
        _emperorOrder = new MoveState("EMPEROR_ORDER", EmperorOrder, new BuffIntent(), new DefendIntent());

        _goldenBarrage = new MoveState("GOLDEN_BARRAGE", GoldenBarrage, new MultiAttackIntent(8, 5));
        _deathSaucer = new MoveState("DEATH_SAUCER", DeathSaucer, new DebuffIntent());
        _resolveSaucer = new MoveState("DEATH_SAUCER_RETURN", ResolveDeathSaucer, new UnknownIntent());
        _goldenHeavy = new MoveState(
            "GOLDEN_HEAVY",
            GoldenHeavy,
            new SingleAttackIntent(34),
            new DebuffIntent());

        _blackFlash = new MoveState("BLACK_FLASH", BlackFlash, new SingleAttackIntent(40));
        _blackBurst = new MoveState("BLACK_BURST", BlackBurst, new MultiAttackIntent(10, 4));
        _emperorShockwave = new MoveState(
            "EMPEROR_SHOCKWAVE",
            EmperorShockwave,
            new SingleAttackIntent(34),
            new DebuffIntent());
        _finalBeam = new MoveState("FINAL_BEAM", FinalBeam, new SingleAttackIntent(40), new CardDebuffIntent());
        _supernovaStart = new MoveState(
            "SUPERNOVA_START",
            SupernovaStart,
            new SingleAttackIntent(15),
            new DefendIntent(),
            new BuffIntent());
        _supernovaCharge = new MoveState(
            "SUPERNOVA_CHARGE",
            SupernovaCharge,
            new SingleAttackIntent(15),
            new BuffIntent());
        _supernovaDetonate = new MoveState("SUPERNOVA_DETONATE", SupernovaDetonate, new DeathBlowIntent(() => SupernovaDetonateHpLoss));
        _supernovaStun = new MoveState("SUPERNOVA_BROKEN", SupernovaStun, new StunIntent());

        MoveState[] moves =
        [
            _whiteSummon,
            _goldenTransform,
            _psychicPressure,
            _contemptuousFinger,
            _deathBeam,
            _emperorOrder,
            _goldenBarrage,
            _deathSaucer,
            _resolveSaucer,
            _goldenHeavy,
            _blackFlash,
            _blackBurst,
            _emperorShockwave,
            _finalBeam,
            _supernovaStart,
            _supernovaCharge,
            _supernovaDetonate,
            _supernovaStun,
        ];
        foreach (MoveState move in moves)
        {
            move.FollowUpState = decision;
        }

        decision.AddState(_goldenTransform, () => IsAwaitingGoldenTransformation);
        decision.AddState(_whiteSummon, () => Phase == 1 && !WhiteOpeningDone);
        decision.AddState(_psychicPressure, () =>
            Phase == 1 && HasLivingFriezaMinion() && PhaseMoveIndex % 3 == 0);
        decision.AddState(_contemptuousFinger, () =>
            Phase == 1 && HasLivingFriezaMinion() && PhaseMoveIndex % 3 == 1);
        decision.AddState(_emperorOrder, () => Phase == 1 && HasLivingFriezaMinion());
        decision.AddState(_deathBeam, () => Phase == 1 && PhaseMoveIndex % 2 == 0);
        decision.AddState(_contemptuousFinger, () => Phase == 1);

        decision.AddState(_goldenBarrage, () => Phase == 2 && !GoldenOpeningDone);
        decision.AddState(_resolveSaucer, () => Phase == 2 && HasSaucerPower());
        decision.AddState(_deathBeam, () => Phase == 2 && PhaseMoveIndex % 3 == 0);
        decision.AddState(_deathSaucer, () => Phase == 2 && PhaseMoveIndex % 3 == 1);
        decision.AddState(_goldenHeavy, () => Phase == 2);

        decision.AddState(_blackFlash, () => Phase == 3 && !BlackOpeningDone);
        // 超新星 = 黑金阶段的爆发检测，必须排在黑金普通招式之前，否则永远轮不到。
        decision.AddState(_supernovaStun, () => Phase == 3 && IsSupernovaBroken());
        decision.AddState(_supernovaCharge, () => Phase == 3 && GetSupernovaPower()?.Turns > 0);
        decision.AddState(_supernovaDetonate, () =>
            Phase == 3 && GetSupernovaPower() is { Turns: 0 });
        decision.AddState(_supernovaStart, () => Phase == 3 && BlackNormalActionsUntilSupernova <= 0);
        decision.AddState(_blackBurst, () => Phase == 3 && PhaseMoveIndex % 2 == 0);
        decision.AddState(_finalBeam, () => Phase == 3);

        return new MonsterMoveStateMachine([decision, .. moves], decision);
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Creature || Phase >= 3)
        {
            return amount;
        }

        if (PhaseTransitionPending)
        {
            return 0m;
        }

        int floor = Phase == 1 ? 1 : (int)Math.Ceiling(Creature.MaxHp * 0.20m);
        return Math.Min(amount, Math.Max(0, Creature.CurrentHp - floor));
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Creature && delta < 0m && Phase < 3)
        {
            int floor = Phase == 1 ? 1 : (int)Math.Ceiling(Creature.MaxHp * 0.20m);
            if (Creature.CurrentHp <= floor)
            {
                PhaseTransitionPending = true;
            }
        }

        if (Phase == 1 && !SecondWaveSummoned && ShouldSummonSecondWave())
        {
            SecondWavePending = true;
        }

        return Task.CompletedTask;
    }

    public override bool ShouldDie(Creature creature)
    {
        return creature != Creature || Phase >= 3;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Creature || Phase >= 3)
        {
            return;
        }

        PhaseTransitionPending = true;
        if (Phase == 1)
        {
            if (Creature.CurrentHp <= 0)
            {
                await CreatureCmd.SetCurrentHp(Creature, 1m);
            }

            await ResolvePendingAtActionBoundary();
            return;
        }

        await PerformPendingPhaseTransition();
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ResolvePendingAtActionBoundary();
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (command.ModelSource is CardModel || command.Attacker == Creature)
        {
            return;
        }

        await ResolvePendingAtActionBoundary();
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        await ResolvePendingAtActionBoundary();
    }

    public override Task AfterDamageReceivedLate(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Creature)
        {
            return Task.CompletedTask;
        }

        if (cardSource != null && IsDirectCardDamage(props))
        {
            int dealt = result.TotalDamage + result.OverkillDamage;
            FriezaDeathSaucerPower? saucer = GetSaucerPower();
            if (saucer != null)
            {
                saucer.SetProgress(saucer.TargetIndex, saucer.Damage + dealt, SaucerThreshold);
            }
            FriezaSupernovaChargePower? supernova = GetSupernovaPower();
            if (supernova != null)
            {
                // 打断只认真正掉的血：打在弗利萨格挡上的部分不计入进度。
                int unblocked = result.UnblockedDamage + result.OverkillDamage;
                int accumulatedDamage = supernova.Damage + unblocked;
                supernova.SetProgress(
                    supernova.Turns,
                    accumulatedDamage,
                    SupernovaThreshold);
                if (accumulatedDamage >= SupernovaThreshold && _supernovaStun != null)
                {
                    SetMoveImmediate(_supernovaStun, forceTransition: true);
                }
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented &&
            Phase == 1 &&
            !PhaseTransitionPending &&
            !SecondWaveSummoned &&
            creature.Monster is FriezaGuldo or FriezaRecoome &&
            ShouldSummonSecondWave())
        {
            SecondWavePending = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await ResolvePendingAtActionBoundary();
    }

    private async Task ResolvePendingAtActionBoundary()
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        if (PhaseTransitionPending && Phase == 1)
        {
            if (!GoldenTransformQueued)
            {
                SecondWavePending = false;
                await WithdrawSummons();
                GoldenTransformQueued = true;
                SetMoveImmediate(_goldenTransform!, forceTransition: true);
            }

            return;
        }

        if (SecondWavePending)
        {
            await SummonSecondWave();
        }

        if (!PhaseTransitionPending)
        {
            return;
        }

        await PerformPendingPhaseTransition();
    }

    private static bool IsDirectCardDamage(ValueProp props)
    {
        return props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
    }

    private bool ShouldSummonSecondWave()
    {
        return Creature.CurrentHp <= Creature.MaxHp / 2;
    }

    private async Task WhiteSummon(IReadOnlyList<Creature> _)
    {
        if (SecondWaveSummonedOrPending())
        {
            return;
        }

        WhiteOpeningDone = true;
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(0.8f, 0.35f, 1.4f, 1f));
        Creature guldo = await CreatureCmd.Add<FriezaGuldo>(CombatState, "support1");
        Creature recoome = await CreatureCmd.Add<FriezaRecoome>(CombatState, "support2");
        FriezaBossVisuals.PlaySummonEffect(guldo);
        FriezaBossVisuals.PlaySummonEffect(recoome);
    }

    private async Task TransformToGolden(IReadOnlyList<Creature> _)
    {
        await PerformPendingPhaseTransition();
    }

    private async Task SummonSecondWave()
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        SecondWavePending = false;
        if (SecondWaveSummoned || Phase != 1)
        {
            return;
        }

        if (!WhiteOpeningDone)
        {
            WhiteOpeningDone = true;
            if (!GoldenTransformQueued)
            {
                SetMoveImmediate(_psychicPressure!, forceTransition: true);
            }
        }

        SecondWaveSummoned = true;
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(0.8f, 0.35f, 1.4f, 1f));
        Creature ginyu = await CreatureCmd.Add<FriezaCaptainGinyu>(CombatState, "support3");
        Creature burterJeice = await CreatureCmd.Add<FriezaBurterJeice>(CombatState, "support4");
        FriezaBossVisuals.PlaySummonEffect(ginyu);
        FriezaBossVisuals.PlaySummonEffect(burterJeice);
    }

    private async Task WithdrawSummons()
    {
        foreach (Creature enemy in CombatState.Enemies.Where(static enemy =>
                     enemy.IsAlive && enemy.Monster is FriezaMinionBase).ToList())
        {
            await CreatureCmd.Escape(enemy);
        }
    }

    private async Task PerformPendingPhaseTransition()
    {
        if (!CombatManager.Instance.IsInProgress || !PhaseTransitionPending || Phase >= 3)
        {
            return;
        }

        PhaseTransitionPending = false;
        GoldenTransformQueued = false;
        SecondWavePending = false;
        await WithdrawSummons();
        if (Creature.Block > 0)
        {
            await KakarotBetaCompat.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                Creature,
                Creature.Block);
        }

        await ClearPhasePowers();
        Phase++;
        PhaseMoveIndex = 0;
        if (Phase == 2)
        {
            await SetPhaseHp(GoldenHp);
            GoldenStrengthRemaining = GoldenPhaseStrength;
            await KakarotPowerCmd.Apply<ArtifactPower>(Creature, 3m, Creature, null);
            await KakarotPowerCmd.Apply<StrengthPower>(Creature, GoldenPhaseStrength, Creature, null);
            await KakarotPowerCmd.Apply<FriezaGoldenStaminaPower>(
                Creature,
                GoldenPhaseStrength,
                Creature,
                null);
        }
        else
        {
            FriezaBossVisuals.ClearSaucerTarget();
            await SetPhaseHp(BlackHp);
            await KakarotPowerCmd.Apply<ArtifactPower>(Creature, 3m, Creature, null);
            await CreatureCmd.GainBlock(Creature, 80m, ValueProp.Move, null);
            foreach (Player player in CombatState.Players.Where(static player => player.Creature.IsAlive))
            {
                await KakarotPowerCmd.Apply<FriezaBlackPressurePower>(player.Creature, 1m, Creature, null);
            }
            SetMoveImmediate(_blackFlash!, forceTransition: true);
        }

        FriezaBossVisuals.SetPhase(Creature, Phase);
        FriezaBossVisuals.PlayTransformationFlash(Creature, Phase);
    }

    private async Task ClearPhasePowers()
    {
        foreach (PowerModel power in Creature.Powers.ToList())
        {
            await PowerCmd.Remove(power);
        }
    }

    private async Task SetPhaseHp(int baseHp)
    {
        int scaled = (int)Creature.ScaleHpForMultiplayer(
            baseHp,
            CombatState.Encounter,
            PlayerCount,
            CombatState.RunState.CurrentActIndex);
        await CreatureCmd.SetMaxAndCurrentHp(Creature, scaled);
    }

    private async Task PsychicPressure(IReadOnlyList<Creature> targets)
    {
        FriezaBossVisuals.PlayFriezaBeam(
            Creature,
            LivingPlayerCreatures(),
            PhaseBeamColor,
            thickness: 0.15f,
            dark: PhaseBeamIsDark);
        await Attack(17, hitFx: "vfx/vfx_attack_lightning");
        await KakarotPowerCmd.Apply<WeakPower>(targets, 1m, Creature, null);
        await KakarotPowerCmd.Apply<FrailPower>(targets, 1m, Creature, null);
        AdvanceMove();
    }

    private async Task ContemptuousFinger(IReadOnlyList<Creature> targets)
    {
        FriezaBossVisuals.PlayFriezaBeam(
            Creature,
            LivingPlayerCreatures(),
            PhaseBeamColor,
            bursts: 2,
            thickness: 0.11f,
            dark: PhaseBeamIsDark);
        await Attack(14, 2, "vfx/vfx_attack_lightning");
        await KakarotPowerCmd.Apply<VulnerablePower>(targets, 2m, Creature, null);
        AdvanceMove();
    }

    private async Task DeathBeam(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayFriezaBeam(
            Creature,
            LivingPlayerCreatures(),
            PhaseBeamColor,
            thickness: Phase == 1 ? 0.20f : 0.24f,
            dark: PhaseBeamIsDark);
        await Attack(Phase == 1 ? 24 : 30, hitFx: "vfx/vfx_attack_lightning");
        AdvanceMove();
        await FinishGoldenMove();
    }

    private async Task EmperorOrder(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayCastMotion(Creature, new Color(1.2f, 0.55f, 1.35f, 1f));
        foreach (Creature summon in CombatState.Enemies.Where(static enemy =>
                     enemy.IsAlive && enemy.Monster is FriezaMinionBase))
        {
            await CreatureCmd.GainBlock(summon, 15m, ValueProp.Move, null);
            await KakarotPowerCmd.Apply<StrengthPower>(summon, 2m, Creature, null);
        }
        AdvanceMove();
    }

    private async Task GoldenBarrage(IReadOnlyList<Creature> _)
    {
        GoldenOpeningDone = true;
        FriezaBossVisuals.PlayAttackMotion(Creature, 30f);
        FriezaVfxKit.PlayBolts(
            Creature,
            LivingPlayerCreatures(),
            PhaseKiColor,
            hits: 5,
            size: 1.05f,
            arcHeight: 72f);
        await Attack(8, 5, "vfx/vfx_attack_blunt");
        await FinishGoldenMove();
    }

    private async Task DeathSaucer(IReadOnlyList<Creature> _)
    {
        Player? target = CombatState.Players.FirstOrDefault(static player => player.Creature.IsAlive);
        if (target == null)
        {
            return;
        }

        int targetIndex = CombatState.Players.ToList().IndexOf(target);
        var power = (FriezaDeathSaucerPower)ModelDb.Power<FriezaDeathSaucerPower>().ToMutable();
        await KakarotPowerCmd.Apply(power, Creature, 1m, Creature, null);
        GetSaucerPower()?.SetProgress(targetIndex, 0, SaucerThreshold);
        FriezaBossVisuals.SyncSaucerTarget(this);
        FriezaBossVisuals.PlayCastMotion(
            Creature,
            new Color(1.3f, 0.45f, 1.25f, 1f));
        FriezaBossVisuals.PlayDeathSaucerFlight(
            Creature,
            target.Creature,
            reflected: false);
        AdvanceMove();
        await FinishGoldenMove();
    }

    private async Task ResolveDeathSaucer(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.ClearSaucerTarget();
        FriezaDeathSaucerPower? saucer = GetSaucerPower();
        bool reflected = saucer?.Damage >= SaucerThreshold;
        Player? target = CombatState.Players.FirstOrDefault(player =>
            player.NetId == SaucerTargetNetId && player.Creature.IsAlive);
        target ??= CombatState.Players.FirstOrDefault(static player => player.Creature.IsAlive);
        if (reflected)
        {
            if (target != null)
            {
                FriezaBossVisuals.PlayDeathSaucerFlight(
                    target.Creature,
                    Creature,
                    reflected: true);
            }
            await CreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                Creature,
                50m,
                ValueProp.Unpowered,
                Creature);
        }
        else
        {
            if (target != null)
            {
                FriezaBossVisuals.PlayDeathSaucerFlight(
                    Creature,
                    target.Creature,
                    reflected: false);
                for (int i = 0; i < 2; i++)
                {
                    await CreatureCmd.Damage(
                        new ThrowingPlayerChoiceContext(),
                        target.Creature,
                        26m,
                        ValueProp.Move,
                        Creature);
                }
            }
        }

        await PowerCmd.Remove<FriezaDeathSaucerPower>(Creature);
        await FinishGoldenMove();
    }

    private async Task GoldenHeavy(IReadOnlyList<Creature> targets)
    {
        FriezaBossVisuals.PlayHeavyWindup(Creature);
        await Attack(
            34,
            hitFx: "vfx/vfx_heavy_blunt",
            fallbackSfx: "heavy_attack.mp3",
            hitVfxAtBase: true);
        FriezaBossVisuals.PlayHitStop();
        await KakarotPowerCmd.Apply<VulnerablePower>(targets, 2m, Creature, null);
        AdvanceMove();
        await FinishGoldenMove();
    }

    private async Task FinishGoldenMove()
    {
        if (Phase != 2 || GoldenStrengthRemaining <= 0)
        {
            return;
        }

        StrengthPower? strength = Creature.GetPower<StrengthPower>();
        int currentStrength = Math.Max(0, strength?.Amount ?? 0);
        GoldenStrengthRemaining = Math.Min(GoldenStrengthRemaining, currentStrength);
        if (strength != null && GoldenStrengthRemaining > 0)
        {
            strength.SetAmount(currentStrength - 1);
            GoldenStrengthRemaining--;
            if (strength.ShouldRemoveDueToAmount())
            {
                await PowerCmd.Remove(strength);
            }
        }

        FriezaGoldenStaminaPower? stamina = Creature.GetPower<FriezaGoldenStaminaPower>();
        if (stamina != null)
        {
            stamina.SetAmount(GoldenStrengthRemaining);
            if (stamina.ShouldRemoveDueToAmount())
            {
                await PowerCmd.Remove(stamina);
            }
        }
    }

    private async Task BlackFlash(IReadOnlyList<Creature> _)
    {
        BlackOpeningDone = true;
        FriezaBossVisuals.PlayHeavyWindup(Creature, strongest: true);
        foreach (Creature flashTarget in LivingPlayerCreatures())
        {
            FriezaVfxKit.PlayBlackFlash(flashTarget, PhaseKiColor);
        }

        await Attack(
            40,
            hitFx: "vfx/vfx_giant_horizontal_slash",
            fallbackSfx: "heavy_attack.mp3");
        FriezaBossVisuals.PlayHitStop(strongest: true);
    }

    private async Task BlackBurst(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayAttackMotion(Creature, 34f);
        FriezaVfxKit.PlayBolts(
            Creature,
            LivingPlayerCreatures(),
            PhaseKiColor,
            hits: 4,
            size: 1.15f,
            arcHeight: 115f);
        await Attack(10, 4, "vfx/vfx_attack_blunt");
        FinishBlackNormalAction();
    }

    private async Task EmperorShockwave(IReadOnlyList<Creature> targets)
    {
        FriezaBossVisuals.PlayShockwave(
            Creature,
            new Color(0.65f, 0.08f, 0.12f, 0.9f),
            1.25f);
        await Attack(
            34,
            hitFx: "vfx/vfx_heavy_blunt",
            fallbackSfx: "heavy_attack.mp3",
            hitVfxAtBase: true);
        FriezaBossVisuals.PlayHitStop();
        await KakarotPowerCmd.Apply<WeakPower>(targets, 2m, Creature, null);
        FinishBlackNormalAction();
    }

    private async Task FinalBeam(IReadOnlyList<Creature> _)
    {
        FriezaBossVisuals.PlayFriezaBeam(
            Creature,
            LivingPlayerCreatures(),
            PhaseBeamColor,
            thickness: 0.31f,
            dark: PhaseBeamIsDark);
        await Attack(40, hitFx: "vfx/vfx_starry_impact");
        foreach (Player player in CombatState.Players.Where(static player => player.Creature.IsAlive))
        {
            for (int i = 0; i < 2; i++)
            {
                CardModel leak = CombatState.CreateCard<FriezaQiLeak>(player);
                await CardPileCmd.AddGeneratedCardToCombat(leak, PileType.Draw, player);
            }
        }
        FinishBlackNormalAction();
    }

    private async Task SupernovaStart(IReadOnlyList<Creature> _)
    {
        const int initialChargeTurns = 2;
        await CreatureCmd.GainBlock(Creature, 35m, ValueProp.Move, null);
        FriezaVfxKit.PlayBolts(
            Creature,
            LivingPlayerCreatures(),
            PhaseKiColor,
            hits: 1,
            size: 1.2f,
            arcHeight: 135f);
        await Attack(15, hitFx: "vfx/vfx_attack_blunt");
        int remainingChargeTurns = initialChargeTurns - 1;
        var power = (FriezaSupernovaChargePower)ModelDb.Power<FriezaSupernovaChargePower>().ToMutable();
        await KakarotPowerCmd.Apply(power, Creature, 1m, Creature, null);
        GetSupernovaPower()?.SetProgress(remainingChargeTurns, 0, SupernovaThreshold);
        FriezaSupernovaVfx.PlayCharge(Creature, level: 1);
    }

    private async Task SupernovaCharge(IReadOnlyList<Creature> _)
    {
        FriezaVfxKit.PlayBolts(
            Creature,
            LivingPlayerCreatures(),
            PhaseKiColor,
            hits: 1,
            size: 1.2f,
            arcHeight: 135f);
        // 第二回合的球更大更亮，读作「快满了」。
        FriezaSupernovaVfx.PlayCharge(Creature, level: 2);
        await Attack(15, hitFx: "vfx/vfx_attack_blunt");
        FriezaSupernovaChargePower? power = GetSupernovaPower();
        if (power != null)
        {
            power.SetProgress(Math.Max(0, power.Turns - 1), power.Damage, SupernovaThreshold);
        }
    }

    private async Task SupernovaDetonate(IReadOnlyList<Creature> _)
    {
        foreach (Creature blastTarget in LivingPlayerCreatures())
        {
            FriezaSupernovaVfx.PlayDetonate(blastTarget);
        }

        FriezaBossVisuals.PlayShockwave(
            Creature,
            new Color(1.5f, 0.35f, 0.05f, 1f),
            1.65f);
        FriezaBossVisuals.PlayHeavyWindup(Creature, strongest: true);
        // 处决：绕过格挡直接扣血，否则 2 回合蓄力会被一个格挡完全消化。
        foreach (Creature supernovaTarget in LivingPlayerCreatures())
        {
            await CreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                supernovaTarget,
                SupernovaDetonateHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Creature);
        }
        FriezaBossVisuals.PlayHitStop(strongest: true);
        await AddQiLeakToDiscards();
        await EndSupernova();
    }

    private async Task SupernovaStun(IReadOnlyList<Creature> _)
    {
        await EndSupernova();
    }

    private async Task EndSupernova()
    {
        BlackNormalActionsUntilSupernova = 2;
        await PowerCmd.Remove<FriezaSupernovaChargePower>(Creature);
    }

    private async Task AddQiLeakToDiscards()
    {
        foreach (Player player in CombatState.Players.Where(static player => player.Creature.IsAlive))
        {
            for (int i = 0; i < 2; i++)
            {
                CardModel leak = CombatState.CreateCard<FriezaQiLeak>(player);
                await CardPileCmd.AddGeneratedCardToCombat(leak, PileType.Discard, player);
            }
        }
    }

    private async Task Attack(
        int damage,
        int hits = 1,
        string hitFx = "vfx/vfx_attack_blunt",
        string? fallbackSfx = null,
        bool hitVfxAtBase = false)
    {
        // 原版 hitFx 是「金属打击」的语言，弗利萨全套是气。
        // 所有招式都从这一个方法出去，所以在这里换掉就等于全阶段一起换。
        // 音效保留：WithHitFx 的 sfx/tmpSfx 和 vfx 是独立字段。
        AttackCommand command = DamageCmd.Attack(damage)
            .WithHitCount(hits)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx(null, null, fallbackSfx)
            .WithHitVfxNode(ResolveHitVfx(hitFx));
        if (hitVfxAtBase)
        {
            command.WithHitVfxSpawnedAtBase();
        }

        await command.Execute(null);
    }

    // 弗利萨各形态的气色：淡紫 → 亮紫 → 暗紫。
    //
    // 🔴 黄金形态原本用金色，飞过来的粒子在战斗背景上几乎看不出来——
    // 背景本身偏暖偏亮，暖色气弹会被吃掉。整条线改走紫，靠明度分形态。
    // 形态之间的区分不能只靠色相，要靠明度：淡 → 亮 → 暗。
    // 死亡光线的配色：一阶段紫、二阶段黄、三阶段黑。
    //
    // 和 PhaseKiColor 是两套：光束又大又亮，暖色不会被背景吃掉，
    // 所以二阶段可以用黄；小颗粒子不行，那边整条线走紫。
    private Color PhaseBeamColor => Phase switch
    {
        2 => new Color(1.00f, 0.82f, 0.22f),
        3 => new Color(0.72f, 0.30f, 1.00f),
        _ => new Color(0.66f, 0.32f, 1.00f),
    };

    // 三阶段的光束是黑柱，需要非加法的暗层打底。
    private bool PhaseBeamIsDark => Phase >= 3;

    private Color PhaseKiColor => Phase switch
    {
        2 => new Color(0.78f, 0.30f, 1.00f),
        3 => new Color(0.52f, 0.12f, 0.88f),
        _ => new Color(0.86f, 0.56f, 1.00f),
    };

    // 按原本挂的原版特效反推这一招是什么手感，形状和大小跟着走。
    // 这样每招的意图（气弹 / 拳 / 重击 / 横扫 / 大招）不用逐个重标。
    private Func<Creature, Node2D> ResolveHitVfx(string? vanillaHitFx)
    {
        (KiHitStyle Style, float Size) shape = vanillaHitFx switch
        {
            "vfx/vfx_attack_lightning" => (KiHitStyle.Palm, 1.05f),
            "vfx/vfx_heavy_blunt" => (KiHitStyle.Fist, 1.45f),
            "vfx/vfx_giant_horizontal_slash" => (KiHitStyle.Slash, 1.55f),
            "vfx/vfx_starry_impact" => (KiHitStyle.Palm, 1.70f),
            _ => (KiHitStyle.Fist, 1.05f),
        };

        // 弗利萨在右侧朝左打，火星要跟着翻。
        return KakarotCombatPresentation.KiHit(shape.Style, shape.Size, PhaseKiColor, facing: -1f);
    }

    private IEnumerable<Creature> LivingPlayerCreatures()
    {
        return CombatState.Players
            .Where(static player => player.Creature.IsAlive)
            .Select(static player => player.Creature);
    }

    private void AdvanceMove()
    {
        PhaseMoveIndex++;
    }

    private void FinishBlackNormalAction()
    {
        PhaseMoveIndex++;
        BlackNormalActionsUntilSupernova--;
    }

    private bool HasSaucerPower()
    {
        return GetSaucerPower() != null;
    }

    private FriezaDeathSaucerPower? GetSaucerPower()
    {
        return Creature.GetPower<FriezaDeathSaucerPower>();
    }

    private FriezaSupernovaChargePower? GetSupernovaPower()
    {
        return Creature.GetPower<FriezaSupernovaChargePower>();
    }

    private bool IsSupernovaBroken()
    {
        return GetSupernovaPower()?.Damage >= SupernovaThreshold;
    }

    private bool HasLivingFriezaMinion()
    {
        return CombatState.Enemies.Any(static enemy =>
            enemy.IsAlive && enemy.Monster is FriezaMinionBase);
    }

    private bool SecondWaveSummonedOrPending()
    {
        return SecondWaveSummoned || SecondWavePending;
    }
}
