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

    /// <summary>
    /// Runtime only: after playing the Super Saiyan God transform card this combat, duplicate copies become unplayable.
    /// Cleared at combat end so each new fight can ritual-grant and play transform again (per-combat, not per-run).
    /// </summary>
    private bool _superSaiyanGodUsedThisCombat;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int _dragonFistBurstBonusThisRun;

    private int _wishGoldPendingThisCombat;

    /// <summary>每场新战斗的首次我方回合开始时给予 1 点怒气。</summary>
    private bool _pendingOpeningCombatRage;

    /// <summary>变身卡打出后的集气检索、解除卡后续。</summary>
    private bool _pendingSuperSaiyanTransformFollowup;

    /// <summary>自在极意「神之气」本回合触发率加成（百分比）。遗物字段参与战斗快照，并在回合或战斗边界归零。</summary>
    private int _godKiBonusPercentThisTurn;

    private int _ultraInstinctHpLossRollCounterThisCombat;

    private bool _wildReturnToOriginGrantedThisCombat;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private int _whoElseButMePlayCountThisRun;

    /// <summary>True after Super Saiyan God transform was played this combat; reset when combat ends.</summary>
    public bool SuperSaiyanGodUsedThisCombat => _superSaiyanGodUsedThisCombat;
    public int DragonFistBurstBonusThisRun => _dragonFistBurstBonusThisRun;
    public bool WildReturnToOriginGrantedThisCombat => _wildReturnToOriginGrantedThisCombat;

    /// <summary>「舍我其谁」本局已成功打出并结算的次数（用于下次攻击段数）。</summary>
    public int WhoElseButMePlayCountThisRun => _whoElseButMePlayCountThisRun;

    /// <summary>
    /// Resolve the active Saiyan-bloodline relic regardless of whether it is
    /// the starter relic or the upgraded Legendary Lineage replacement.
    /// </summary>
    public static SaiyanBlood ResolveBloodlineRelic(Player owner)
    {
        if (owner == null)
        {
            return null;
        }

        return owner.GetRelic<SaiyanBlood>() ?? owner.GetRelic<KakarotLegendaryLineage>();
    }

    /// <summary>Call when transform card resolves; prevents duplicate plays until combat ends.</summary>
    public void MarkSuperSaiyanGodUsedThisCombat()
    {
        AssertMutable();
        _superSaiyanGodUsedThisCombat = true;
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

    public void MarkWildReturnToOriginGrantedThisCombat()
    {
        AssertMutable();
        _wildReturnToOriginGrantedThisCombat = true;
    }

    /// <summary>本回合神之气加成（百分比）。仅读取，供触发率计算与 UI 使用。</summary>
    public int GodKiBonusPercentThisTurn => _godKiBonusPercentThisTurn;

    /// <summary>神之气打出时叠加本回合触发率加成。</summary>
    public void AddGodKiBonusPercentThisTurn(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        AssertMutable();
        _godKiBonusPercentThisTurn += amount;
    }

    /// <summary>回合开始 / 进出战斗时清零本回合神之气加成。</summary>
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

    /// <summary>Transfer run-scoped counters from another SaiyanBlood before it is removed (e.g. boss relic upgrade).</summary>
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

    /// <summary>本场战斗你第一个回合开始时额外获得的基础怒气。传说血统覆写为 2。</summary>
    protected virtual decimal OpeningCombatRageBonus => 1m;

    /// <summary>战斗结束时：当前生命低于「最大生命 × 该百分比」则回复 <see cref="EndCombatHealAmount"/>。传说血统覆写为更高百分比与回复量。</summary>
    protected virtual int EndCombatHealHpThresholdPercent => 70;

    protected virtual decimal EndCombatHealAmount => 6m;

    /// <summary>「变身」牌 OnPlay 末尾调用：标记须在 <see cref="AfterCardPlayed"/> 中完成集气检索与解除卡发放。</summary>
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

        // 整数式比较，避免浮点非确定性（联机）：CurrentHp/MaxHp < Percent/100。
        if (creature.CurrentHp * 100 < creature.MaxHp * EndCombatHealHpThresholdPercent)
        {
            Flash();
            await CreatureCmd.Heal(creature, EndCombatHealAmount);
        }
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

            Flash();
            await PlayerCmd.GainStars(1m, Owner);
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
            _wildReturnToOriginGrantedThisCombat = false;
            if (Owner?.Character is KakarotCharacter)
            {
                _pendingOpeningCombatRage = true;
            }

            _pendingSuperSaiyanTransformFollowup = false;

            await UpdateNearDeathBoost();
        }
    }

    /// <summary>Clears per-turn God Ki / UI proc bonus without a visible Power (see KakarotUltraInstinctCombatState).</summary>
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

        _superSaiyanGodUsedThisCombat = false;
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
        _wildReturnToOriginGrantedThisCombat = false;
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

        // 残血爆发阈值：生命 ≤ 最大值 20%（CurrentHp*5 <= MaxHp）。整数比较，联机确定。
        bool isNearDeath = creature.CurrentHp * 5 <= creature.MaxHp;
        Status = isNearDeath ? RelicStatus.Active : RelicStatus.Normal;

        if (isNearDeath && !_nearDeathBoostApplied)
        {
            Flash();
            await KakarotPowerCmd.Apply<StrengthPower>(creature, 2m, creature, null);
            await KakarotPowerCmd.Apply<DexterityPower>(creature, 2m, creature, null);
            _nearDeathBoostApplied = true;
        }
        else if (!isNearDeath && _nearDeathBoostApplied)
        {
            Flash();
            await KakarotPowerCmd.Apply<StrengthPower>(creature, -2m, creature, null);
            await KakarotPowerCmd.Apply<DexterityPower>(creature, -2m, creature, null);
            _nearDeathBoostApplied = false;
        }
    }
}
