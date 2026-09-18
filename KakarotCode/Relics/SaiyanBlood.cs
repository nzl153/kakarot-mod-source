using System;
using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KakarotMod.KakarotCode.Relics;

public class SaiyanBlood : KakarotRelic
{
    private const string RageHoverIconPath = "res://images/packed/sprite_fonts/kakarot_star_icon.png";

    private bool _nearDeathBoostApplied;

    private static HoverTip BuildRageKeywordHoverTip()
    {
        var title = new LocString("static_hover_tips", "KAKAROT_RELIC_RAGE_KEYWORD.title");
        var description = new LocString("static_hover_tips", "KAKAROT_RELIC_RAGE_KEYWORD.description");
        Texture2D icon = null;
        if (ResourceLoader.Exists(RageHoverIconPath))
        {
            icon = PreloadManager.Cache.GetTexture2D(RageHoverIconPath);
        }

        return new HoverTip(title, description, icon);
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            // Do not cache HoverTip instances; stale loaded references can stick on screen.
            yield return BuildRageKeywordHoverTip();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int _dragonFistBurstBonusThisRun { get; set; }

    private int _wishGoldPendingThisCombat;

    private bool _pendingOpeningCombatRage;

    private bool _pendingSuperSaiyanTransformFollowup;

    // Relic-backed storage participates in rollback snapshots; static storage desynchronizes replay.
    private int _godKiBonusPercentThisTurn;

    private int _ultraInstinctHpLossRollCounterThisCombat;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int _whoElseButMePlayCountThisRun { get; set; }

    public int DragonFistBurstBonusThisRun => _dragonFistBurstBonusThisRun;

    public int WhoElseButMePlayCountThisRun => _whoElseButMePlayCountThisRun;

    // Support both the starter relic and its boss-relic replacement.
    public static SaiyanBlood ResolveBloodlineRelic(Player owner)
    {
        if (owner == null)
        {
            return null;
        }

        return owner.GetRelic<SaiyanBlood>() ?? owner.GetRelic<KakarotLegendaryLineage>();
    }

    public void AddDragonFistBurstBonusThisRun(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AssertMutable();
        _dragonFistBurstBonusThisRun += amount;
    }

    public void AddWishGoldPendingThisCombat(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AssertMutable();
        _wishGoldPendingThisCombat += amount;
    }

    public void RegisterWhoElseButMePlayed()
    {
        AssertMutable();
        _whoElseButMePlayCountThisRun++;
    }

    public int GodKiBonusPercentThisTurn => _godKiBonusPercentThisTurn;

    public void AddGodKiBonusPercentThisTurn(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        AssertMutable();
        _godKiBonusPercentThisTurn += amount;
    }

    public void ResetGodKiBonusPercentThisTurn()
    {
        AssertMutable();
        _godKiBonusPercentThisTurn = 0;
    }

    public int NextUltraInstinctHpLossRollIndex()
    {
        AssertMutable();
        _ultraInstinctHpLossRollCounterThisCombat++;
        return _ultraInstinctHpLossRollCounterThisCombat;
    }

    // Preserve run-scoped counters when the starter relic is replaced.
    public void TransferRunPersistentStateFrom(SaiyanBlood old)
    {
        if (old == null || ReferenceEquals(old, this))
        {
            return;
        }

        AssertMutable();
        if (old._dragonFistBurstBonusThisRun > 0)
        {
            _dragonFistBurstBonusThisRun += old._dragonFistBurstBonusThisRun;
        }

        if (old._whoElseButMePlayCountThisRun > 0)
        {
            _whoElseButMePlayCountThisRun += old._whoElseButMePlayCountThisRun;
        }
    }

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected virtual decimal OpeningCombatRageBonus => 1m;

    protected virtual int EndCombatHealHpThresholdPercent => 70;

    protected virtual decimal EndCombatHealAmount => 6m;

    public static void MarkPendingSuperSaiyanTransformFollowup(Player player)
    {
        var relic = ResolveBloodlineRelic(player);
        if (relic != null)
        {
            relic.AssertMutable();
            relic._pendingSuperSaiyanTransformFollowup = true;
        }
    }

    private async Task FlushSuperSaiyanTransformFollowupAsync(Player player, CardModel playedTransformCard)
    {
        if (!CombatManager.Instance.IsInProgress || player.PlayerCombatState == null || player.Creature?.CombatState == null)
        {
            return;
        }

        await KakarotSuperSaiyanRefundCards.TryRetrieveChargeUpToHand(player, playedTransformCard);

        var cancelId = ModelDb.Card<KakarotCancelSuperSaiyanForm>().Id;
        if (player.PlayerCombatState.AllCards.Any(c => c.Id == cancelId))
        {
            return;
        }

        var cancel = player.Creature.CombatState.CreateCard<KakarotCancelSuperSaiyanForm>(player);
        await CardPileCmd.AddGeneratedCardToCombat(cancel, PileType.Hand, player);
    }

    private async Task TryBloodlinePostCombatHealAsync()
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        var creature = Owner.Creature;
        if (creature.CurrentHp <= 0)
        {
            return;
        }

        // Integer comparison avoids floating-point divergence in multiplayer.
        if (creature.CurrentHp * 100 < creature.MaxHp * EndCombatHealHpThresholdPercent)
        {
            Flash();
            await CreatureCmd.Heal(creature, EndCombatHealAmount);
        }
    }

    // 受击怒气量可由觉醒版覆写；敌方伤害来源判定统一由基类处理。
    protected virtual decimal RageOnEnemyDamage => 1m;

    // 受击怒气只在敌方生物造成实际生命损失时获得；必须使用带 dealer 的伤害钩子，以排除自伤和无主伤害。
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (Owner?.Creature == null || target != Owner.Creature)
        {
            return;
        }

        if (!CombatManager.Instance.IsInProgress || target.CombatState == null)
        {
            return;
        }

        // 全格挡不触发，因为没有实际生命损失。
        if (result == null || result.UnblockedDamage <= 0)
        {
            return;
        }

        // dealer 必须来自敌对阵营；null 表示无主伤害，自伤 dealer 与 target 同阵营。
        if (dealer == null || dealer.Side == target.Side)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainStars(RageOnEnemyDamage, Owner);
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        // Event/self damage outside combat has no combat state; skip rage gain there.
        if (creature == Owner?.Creature && delta < 0 && CombatManager.Instance.IsInProgress && creature.CombatState != null)
        {
            if (Owner.Character is KakarotCharacter)
            {
                KakarotCombatPresentation.TryPlayHitReaction(creature);
            }

            // 此钩子没有伤害来源，因此不处理怒气；怒气在 AfterDamageReceived 中结算。
            if (Owner.Character is KakarotCharacter)
            {
                await KakarotUltraInstinctTriggerHelper.OnPlayerTookHpLoss(Owner, creature, creature.CombatState, delta);
            }
        }

        if (CombatManager.Instance.IsInProgress)
        {
            await UpdateNearDeathBoost();
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var cardOwner = cardPlay.Card.Owner;
        if (cardOwner is { Character: KakarotCharacter } && ReferenceEquals(ResolveBloodlineRelic(cardOwner), this))
        {
            KakarotCombatPresentation.TryPlayAttackWindup(cardOwner, cardPlay);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        var cardOwner = cardPlay.Card.Owner;
        var isThisBloodlineKakarot =
            cardOwner is { Character: KakarotCharacter } &&
            ReferenceEquals(ResolveBloodlineRelic(cardOwner), this);

        if (isThisBloodlineKakarot)
        {
            if (_pendingSuperSaiyanTransformFollowup && cardPlay.Card.Id == ModelDb.Card<KakarotSuperSaiyanTransform>().Id)
            {
                _pendingSuperSaiyanTransformFollowup = false;
                await FlushSuperSaiyanTransformFollowupAsync(cardOwner, cardPlay.Card);
            }
        }

        if (isThisBloodlineKakarot && cardPlay.Card.Tags.Contains(CardTag.Strike))
        {
            Flash();
            await PlayerCmd.GainStars(1m, cardOwner);
        }

        if (isThisBloodlineKakarot && KakarotWildHelper.HasWild(cardPlay.Card))
        {
            await KakarotWildRitualHandler.OnWildCardPlayed(context, cardOwner, cardPlay.Card);
        }
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            KakarotUltraInstinctCombatState.ResetForNewCombat(Owner);
            _wishGoldPendingThisCombat = 0;
            _ultraInstinctHpLossRollCounterThisCombat = 0;
            if (Owner?.Character is KakarotCharacter)
            {
                _pendingOpeningCombatRage = true;
            }

            _pendingSuperSaiyanTransformFollowup = false;

            await UpdateNearDeathBoost();
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player is { Character: KakarotCharacter } && ReferenceEquals(ResolveBloodlineRelic(player), this))
        {
            if (_pendingOpeningCombatRage)
            {
                await KakarotSuperSaiyanRefundCards.TryRetrieveTransformToHandIfBuriedAsync(player, this);
                _pendingOpeningCombatRage = false;
                if (OpeningCombatRageBonus > 0m)
                {
                    await PlayerCmd.GainStars(OpeningCombatRageBonus, player);
                }
            }

            KakarotUltraInstinctCombatState.OnPlayerTurnStarted(player);
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await TryBloodlinePostCombatHealAsync();

        if (_wishGoldPendingThisCombat > 0)
        {
            await GrantGoldAfterCombat(_wishGoldPendingThisCombat);
            _wishGoldPendingThisCombat = 0;
        }

        _pendingOpeningCombatRage = false;
        _nearDeathBoostApplied = false;
        Status = RelicStatus.Normal;
        if (Owner?.Creature != null)
        {
            await PowerCmd.Remove<KakarotJusticeHeartRitualPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotGodKiRitualPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotUltraInstinctTurnCleanupPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotInsightUltraInstinctPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotUltraInstinctOmenPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotPerfectUltraInstinctPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotWildRitualPower>(Owner.Creature);
            await PowerCmd.Remove<KakarotSuperSaiyan4Power>(Owner.Creature);
            await PowerCmd.Remove<KakarotSuperSaiyan4EnergyCapPower>(Owner.Creature);
        }

        KakarotUltraInstinctCombatState.ResetForNewCombat(Owner);
        _ultraInstinctHpLossRollCounterThisCombat = 0;
        _pendingSuperSaiyanTransformFollowup = false;
    }

    private async Task GrantGoldAfterCombat(int amount)
    {
        if (amount <= 0 || Owner == null)
        {
            return;
        }

        await PlayerCmd.GainGold(amount, Owner);
    }

    private async Task UpdateNearDeathBoost()
    {
        Creature creature = Owner?.Creature;
        if (creature == null)
        {
            return;
        }

        // Integer comparison keeps the 20% health threshold deterministic.
        bool isNearDeath = creature.CurrentHp * 5 <= creature.MaxHp;
        Status = isNearDeath ? RelicStatus.Active : RelicStatus.Normal;

        if (isNearDeath && !_nearDeathBoostApplied)
        {
            Flash();
            await KakarotPowerCmd.Apply<StrengthPower>(creature, 2m, creature, null);
            await KakarotPowerCmd.Apply<DexterityPower>(creature, 2m, creature, null);
            _nearDeathBoostApplied = true;
            // 气场只跟随该遗物状态，避免未持有遗物时错误显示加成提示。
            KakarotCombatPresentation.SetNearDeathAura(creature, true);
        }
        else if (!isNearDeath && _nearDeathBoostApplied)
        {
            Flash();
            await KakarotPowerCmd.Apply<StrengthPower>(creature, -2m, creature, null);
            await KakarotPowerCmd.Apply<DexterityPower>(creature, -2m, creature, null);
            _nearDeathBoostApplied = false;
            KakarotCombatPresentation.SetNearDeathAura(creature, false);
        }
    }
}
