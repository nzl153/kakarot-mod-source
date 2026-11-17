#nullable enable
using KakarotMod.KakarotCode.DragonBalls;
using KakarotMod.KakarotCode.Encounters;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Relics;

public sealed class KakarotSevenDragonBalls : KakarotRelic
{
    private KakarotDragonBallSet _selectedSet;
    private int _ballCount;
    private Task? _selectionTask;
    private KakarotWish _wishes;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => BallCount;
    public override string PackedIconPath => "kakarot_seven_dragon_balls.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "kakarot_seven_dragon_balls_outline.png".RelicImagePath();
    protected override string BigIconPath => "kakarot_seven_dragon_balls.png".BigRelicImagePath();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private KakarotDragonBallSet SavedSelectedSet
    {
        get => _selectedSet;
        set
        {
            AssertMutable();
            _selectedSet = value;
            DynamicVars["Set"].BaseValue = (int)value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedBallCount
    {
        get => _ballCount;
        set
        {
            AssertMutable();
            _ballCount = Math.Clamp(value, 0, KakarotDragonBallRules.RequiredBalls);
            DynamicVars["Balls"].BaseValue = _ballCount;
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedNormalCombatMisses { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private bool SavedHasWished { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private KakarotWish SavedWishes
    {
        get => _wishes;
        set
        {
            AssertMutable();
            _wishes = value;
            DynamicVars["Wished"].BaseValue = value == KakarotWish.None ? 0 : 1;
            foreach (var wish in Enum.GetValues<KakarotWish>().Where(wish => wish != KakarotWish.None))
            {
                DynamicVars[wish.ToString()].BaseValue = value.HasFlag(wish) ? 1 : 0;
            }
        }
    }

    public KakarotDragonBallSet SelectedSet => SavedSelectedSet;
    public int BallCount => SavedBallCount;
    public bool HasWished => SavedHasWished;
    public KakarotWish Wishes => SavedWishes;

    public bool RequestPending { get; set; }
    public bool CanCollect => SelectedSet != KakarotDragonBallSet.None
        && BallCount < KakarotDragonBallRules.RequiredBalls && !HasWished;

    public bool CanSummon => BallCount == KakarotDragonBallRules.RequiredBalls && !HasWished
        && CombatManager.Instance.IsInProgress && !CombatManager.Instance.PlayerActionsDisabled
        && !CombatManager.Instance.IsPlayerReadyToEndTurn(Owner)
        && Owner.PlayerCombatState?.Phase == PlayerTurnPhase.Play && Owner.Creature.IsAlive;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Set", 0),
        new DynamicVar("Balls", 0),
        new DynamicVar("RequiredBalls", KakarotDragonBallRules.RequiredBalls),
        new DynamicVar("Wished", 0),
    }.Concat(Enum.GetValues<KakarotWish>().Where(wish => wish != KakarotWish.None)
        .Select(wish => new DynamicVar(wish.ToString(), 0)));

    public void MarkWished(KakarotWish wishes)
    {
        AssertMutable();
        SavedWishes = wishes;
        SavedHasWished = true;
        Flash();
    }

    public override async Task BeforeCombatStartLate()
    {
        if (Wishes == KakarotWish.SuperRitual)
        {
            await KakarotDragonBallWishes.GrantRituals(new ThrowingPlayerChoiceContext(), Owner);
        }
        else if (Wishes == KakarotWish.BlackStarPower)
        {
            await KakarotDragonBallWishes.GrantBlackStarPower(new ThrowingPlayerChoiceContext(), Owner);
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && Wishes == KakarotWish.SuperEnergy)
        {
            var vars = ModelDb.Card<KakarotWishSuperEnergy>().DynamicVars;
            await PlayerCmd.GainEnergy(vars["Energy"].BaseValue, Owner);
            await PlayerCmd.GainStars(vars["Rage"].BaseValue, Owner);
        }
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player == Owner && Wishes == KakarotWish.BlackStarDraw
            ? count + ModelDb.Card<KakarotWishBlackStarDraw>().DynamicVars["Cards"].BaseValue : count;

#if STS2_V109
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
#endif
        => Wishes == KakarotWish.SuperBasics && dealer == Owner.Creature
            && cardSource?.Owner == Owner && cardSource.Tags.Contains(CardTag.Strike)
            ? ModelDb.Card<KakarotWishSuperBasics>().DynamicVars["Multiplier"].BaseValue : 1m;

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
        => Wishes == KakarotWish.SuperBasics && target == Owner.Creature
            && cardSource?.Owner == Owner && cardSource.Tags.Contains(CardTag.Defend)
            ? ModelDb.Card<KakarotWishSuperBasics>().DynamicVars["Multiplier"].BaseValue : 1m;

    public override Task AfterActEntered() => Owner.RunState.CurrentActIndex == 0
        ? EnsureAllSetsSelected() : Task.CompletedTask;

    public override Task BeforeCombatStart()
    {
        RequestPending = false;
        return EnsureAllSetsSelected();
    }

    private Task EnsureAllSetsSelected()
    {
        // 先按玩家顺序预留所有选择，避免远端等待时本地先古选牌抢占选择编号。
        var tasks = Owner.RunState.Players.Select(player => player.GetRelic<KakarotSevenDragonBalls>())
            .OfType<KakarotSevenDragonBalls>().Select(relic => relic.EnsureSetSelected()).ToArray();
        return Task.WhenAll(tasks);
    }

    private Task EnsureSetSelected()
    {
        if (SelectedSet != KakarotDragonBallSet.None)
        {
            return Task.CompletedTask;
        }

        return _selectionTask ??= KakarotDragonBallSelection.Choose(this);
    }

    public void SelectSet(KakarotDragonBallSet selectedSet)
    {
        AssertMutable();
        if (SelectedSet == KakarotDragonBallSet.None)
        {
            SavedSelectedSet = selectedSet;
            Flash();
        }
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!CanCollect || !room.Encounter.ShouldGiveRewards)
        {
            return Task.CompletedTask;
        }

        bool drops;
        if (room.RoomType is RoomType.Elite or RoomType.Boss || room.Encounter is FriezaBossEncounter)
        {
            drops = true;
        }
        else if (room.RoomType == RoomType.Monster && SelectedSet != KakarotDragonBallSet.Super)
        {
            bool blackStar = SelectedSet == KakarotDragonBallSet.BlackStar;
            int pity = blackStar ? KakarotDragonBallRules.BlackStarPityMisses
                : KakarotDragonBallRules.EarthAndNamekPityMisses;
            int chance = blackStar ? KakarotDragonBallRules.BlackStarDropPercent
                : KakarotDragonBallRules.EarthAndNamekDropPercent;
            drops = SavedNormalCombatMisses >= pity || Owner.PlayerRng.Rewards.NextInt(100) < chance;
            if (!drops)
            {
                SavedNormalCombatMisses++;
            }
        }
        else
        {
            return Task.CompletedTask;
        }

        if (drops)
        {
            SavedNormalCombatMisses = 0;
        }

        // 每场都给一次搜寻，结果在这里定好，点开奖励时才揭晓。
        // 战后存档在此钩子之后、生成奖励列表之前，额外奖励随房间保存。
        room.AddExtraReward(Owner, new KakarotDragonBallReward(Owner, drops));

        return Task.CompletedTask;
    }

    public void DebugSet(int count, KakarotDragonBallSet? set)
    {
        AssertMutable();
        if (set != null)
        {
            SavedSelectedSet = set.Value;
            SavedWishes = KakarotWish.None;
            SavedHasWished = false;
        }

        SavedBallCount = count;
    }

    public void CollectBall()
    {
        AssertMutable();
        SavedBallCount++;
        Flash();
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        RequestPending = false;
        if (Wishes == KakarotWish.SuperVitality && Owner.Creature.IsAlive)
        {
            await CreatureCmd.Heal(Owner.Creature,
                ModelDb.Card<KakarotWishSuperVitality>().DynamicVars["Heal"].BaseValue);
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _selectionTask = null;
        RequestPending = false;
    }
}
