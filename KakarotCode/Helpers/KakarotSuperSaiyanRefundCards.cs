using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Helpers;

/// <summary>
/// 变身时：「集气」从现有牌堆中检索置入手牌（不复制新卡）。「变身」由打出牌的 <see cref="PileType.Hand"/> 结算回到手牌，此处不再生成。
/// </summary>
internal static class KakarotSuperSaiyanRefundCards
{
    /// <summary>
    /// 检索顺序：抽牌堆 → 弃牌堆 → 消耗堆 → 手牌（已在手则不动）。
    /// 手牌满时目标为抽牌堆顶：若集气<strong>已在抽牌堆</strong>，禁止再 Add(Draw→Draw)，否则引擎可能把卡移出 Run 导致整场构筑里消失。
    /// 若在战斗垛里找不到但仍存在于 <see cref="Player.Deck"/> 列表，再尝试拉进战斗垛。
    /// </summary>
    internal static async Task TryRetrieveChargeUpToHand(Player player, AbstractModel source)
    {
        var cs = player.PlayerCombatState;
        var chargeUpId = ModelDb.Card<KakarotChargeUp>().Id;
        var chargeUpEntry = chargeUpId.Entry;
        bool IsChargeUp(CardModel c) =>
            c != null && (c.Id == chargeUpId || (c.Id.Entry != null && c.Id.Entry == chargeUpEntry));

        foreach (var pile in new[] { cs.DrawPile, cs.DiscardPile, cs.ExhaustPile, cs.Hand })
        {
            var card = pile.Cards.FirstOrDefault(IsChargeUp);
            if (card == null) continue;

            if (pile == cs.Hand) return;

            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }

        if (cs.AllCards.Any(IsChargeUp)) return;

        var deckCard = player.Deck?.Cards?.FirstOrDefault(IsChargeUp);
        if (deckCard == null) return;

        await CardPileCmd.Add(deckCard, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
    }

    /// <summary>
    /// <strong>仅补救 SL / 合并异常</strong>：正常满 12 发变身仍走 <see cref="Relics.SaiyanBlood"/> 的 <c>TryDeliver…</c>，一般会直接进手牌。
    /// 每场战斗<strong>首回合开始</strong>再扫一遍：若变身已在本场垛/构筑里却不在手牌（例如只登记在构筑、<c>AllCards</c> 游离），则抽到可打位置；避免与正规发放叠两次 <c>Add</c>。
    /// </summary>
    internal static async Task TryRetrieveTransformToHandIfBuriedAsync(Player player, AbstractModel source)
    {
        if (!CombatManager.Instance.IsInProgress || player?.PlayerCombatState is not { } cs)
        {
            return;
        }

        var transformId = ModelDb.Card<KakarotSuperSaiyanTransform>().Id;
        var transformEntry = transformId.Entry;
        bool IsTransform(CardModel c) =>
            c != null && (c.Id == transformId || (c.Id.Entry != null && c.Id.Entry == transformEntry));

        if (cs.Hand.Cards.Any(IsTransform))
        {
            return;
        }

        bool InStandardCombatPile(CardModel card) =>
            card != null && (
                cs.Hand.Cards.Contains(card) ||
                cs.DrawPile.Cards.Contains(card) ||
                cs.DiscardPile.Cards.Contains(card) ||
                cs.ExhaustPile.Cards.Contains(card));

        foreach (var pile in new[] { cs.DrawPile, cs.DiscardPile, cs.ExhaustPile })
        {
            var card = pile.Cards.FirstOrDefault(IsTransform);
            if (card == null) continue;

            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }

        var limbo = cs.AllCards?.FirstOrDefault(c => IsTransform(c) && !InStandardCombatPile(c));
        if (limbo != null)
        {
            await CardPileCmd.Add(limbo, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }

        var fromDeck = player.Deck?.Cards?.FirstOrDefault(IsTransform);
        if (fromDeck == null)
        {
            return;
        }

        await CardPileCmd.Add(fromDeck, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
    }
}