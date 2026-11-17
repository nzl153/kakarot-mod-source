using KakarotMod.KakarotCode.Encounters;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Gravity;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;

namespace KakarotMod.KakarotCode.Relics;

public sealed class KakarotGravityWeights : KakarotRelic
{
    private int _level = 1;
    private int _trainingStrength;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override string PackedIconPath => "kakarot_gravity_weights.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "kakarot_gravity_weights_outline.png".RelicImagePath();
    protected override string BigIconPath => "kakarot_gravity_weights.png".BigRelicImagePath();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedLevel
    {
        get => _level;
        set
        {
            AssertMutable();
            _level = value;
            DynamicVars["Gravity"].BaseValue = value switch { 1 => 10, 2 => 50, 3 => 100, _ => 0 };
            DynamicVars["HpBoost"].BaseValue = HpBoost;
            DynamicVars["DamageBoost"].BaseValue = DamageBoost;
            DynamicVars["VictoryUnlocked"].BaseValue = value >= 2 ? 1 : 0;
            DynamicVars["RestUnlocked"].BaseValue = value >= 3 ? 1 : 0;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int SavedTrainingStrength
    {
        get => _trainingStrength;
        set
        {
            AssertMutable();
            _trainingStrength = value;
            DynamicVars["TrainingStrength"].BaseValue = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private bool WeightsUsed { get; set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private bool FirstAttackPlayed { get; set; }

    public bool RequestPending { get; set; }

    public int Level => SavedLevel;
    public int TrainingStrength => SavedTrainingStrength;
    public bool UsedWeightsThisCombat => WeightsUsed;

    public void SetLevel(int level) => SavedLevel = level;
    public void AddTrainingStrength() => SavedTrainingStrength++;

    public int HpBoost => Level switch { 1 => 25, 2 => 50, 3 => 80, _ => 0 };
    public int DamageBoost => Level switch { 1 => 10, 2 => 20, 3 => 30, _ => 0 };

    // 弗利萨战：十倍、五十倍 +10% 生命和伤害，百倍 +20% 生命、+15% 伤害。
    public int HpBoostIn(EncounterModel encounter) => encounter is FriezaBossEncounter ? (Level == 3 ? 20 : 10) : HpBoost;
    private int DamageBoostIn(EncounterModel encounter) => encounter is FriezaBossEncounter ? (Level == 3 ? 15 : 10) : DamageBoost;

    public bool IsActive => Level is >= 1 and <= 3
        && Owner.RunState.Players.Count == 1
        && Owner.Character is KakarotCharacter
        && !RunManager.Instance.NetService.Type.IsMultiplayer();

    private bool IsTrainingCombat => IsActive && Owner.Creature.CombatState != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Gravity", 10),
        new DynamicVar("HpBoost", 25),
        new DynamicVar("DamageBoost", 10),
        new DynamicVar("VictoryUnlocked", 0),
        new DynamicVar("RestUnlocked", 0),
        new DynamicVar("TrainingStrength", 0),
        new DynamicVar("Rage", 1),
        new DynamicVar("Dexterity", 5),
    ];

    public override Task BeforeCombatStart()
    {
        FirstAttackPlayed = false;
        WeightsUsed = false;
        RequestPending = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStartLate()
    {
        if (IsTrainingCombat && Level >= 3 && TrainingStrength > 0)
        {
            Flash();
            await KakarotPowerCmd.Apply<StrengthPower>(Owner.Creature, TrainingStrength, Owner.Creature, null);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (!IsTrainingCombat || FirstAttackPlayed
            || cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Attack
            || cardPlay.IsAutoPlay || !cardPlay.IsFirstInSeries)
        {
            return;
        }

        FirstAttackPlayed = true;
        Flash();
        await PlayerCmd.GainStars(1, Owner);
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!IsActive || Level < 2 || UsedWeightsThisCombat)
        {
            return;
        }

        Flash();
        await CreatureCmd.GainMaxHp(Owner.Creature, room.RoomType is RoomType.Elite or RoomType.Boss ? 2 : 1);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        // Victory hooks run after AfterCombatEnd, so retain the weight-use flag here.
        RequestPending = false;
        return Task.CompletedTask;
    }

#if STS2_V109
    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource, CardPlay cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource)
#endif
    {
        return IsTrainingCombat && dealer is { Side: CombatSide.Enemy, Monster: not null }
            && props.IsPoweredAttack()
            ? 1m + DamageBoostIn(Owner.Creature.CombatState.Encounter) / 100m
            : 1m;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner || !IsActive || Level < 3)
        {
            return false;
        }

        options.Add(new KakarotGravityTrainRestSiteOption(player));
        return true;
    }

    public void MarkWeightsUsed()
    {
        AssertMutable();
        WeightsUsed = true;
        RequestPending = false;
        Flash();
    }
}
