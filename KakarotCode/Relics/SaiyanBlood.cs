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

    // 挨打获得的怒气量。觉醒版（传说血脉）覆写成 2。
    // 抽成属性是为了让「只认敌人伤害」这条判定只写一份 ——
    // 之前觉醒版自己在旧钩子上又加了一次，结果基类改了它没改，自伤照样刷怒气。
    protected virtual decimal RageOnEnemyDamage => 1m;

    // 怒气只认「挨打」——敌人真正打掉血才给，卡面文案就是这么写的。
    //
    // 以前挂在 AfterCurrentHpChanged 上，那个钩子只有 delta、没有来源，
    // 于是自伤（界王拳等）也刷怒气。而且给的是固定 1 点、与掉血量无关，
    // 等于奖励「掉血次数」而不是「掉血量」，最优解变成频繁小额自伤 —— 与设计相反。
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

        // 全格挡不给：没真掉血就不算挨打。
        if (result == null || result.UnblockedDamage <= 0)
        {
            return;
        }

        // 🔴 关键判定：必须是对面阵营的生物打的。
        // dealer 为 null 的是无主伤害（中毒跳伤就是 CreatureCmd.Damage(dealer: null)），
        // 自伤的 dealer 是自己，两者都不算挨打。
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

            // ⚠ 怒气不在这里给了。这个钩子只有 delta、拿不到伤害来源，
            // 结果是「自己扣血也算挨打」，和卡面文案对不上。
            // 已挪到 AfterDamageReceived，那里有 dealer。
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
            // 纯表现层。气场只跟着这个遗物的状态走，不去判血量百分比——
            // 没拿到这个遗物的人不该看见它，否则等于承诺了一个他没有的加成。
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
