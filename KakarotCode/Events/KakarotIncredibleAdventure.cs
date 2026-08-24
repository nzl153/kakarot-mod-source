using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Events;

[CustomID("KAKAROT_INCREDIBLE_ADVENTURE")]
public sealed class KakarotIncredibleAdventure : CustomEventModel
{
    // 代价从「掉 6 点当前生命」改成「掉 6 点生命上限」——删 2 张牌的收益太高，
    // 用一次性掉血付账等于白送，改成永久上限才配得上移除两张牌。
    private const decimal GoodbyeMaxHpLoss = 6m;

    public override string CustomInitialPortraitPath =>
        "res://images/events/kakarot_incredible_adventure.png";

    public override bool IsAllowed(IRunState runState) =>
        KakarotEventEligibility.HasKakarot(runState);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        string beginKey = $"{Id.Entry}.pages.INITIAL.options.BEGIN_ADVENTURE";
        string goodbyeKey = $"{Id.Entry}.pages.INITIAL.options.GOODBYE";
        string goodbyeLockedKey = $"{Id.Entry}.pages.INITIAL.options.GOODBYE_LOCKED";

        int removableInDeck = Owner == null
            ? 0
            : PileType.Deck.GetPile(Owner).Cards.Count(static c => c.IsRemovable);

        return removableInDeck >= 2
            ? new List<EventOption>
            {
                new EventOption(this, BeginAdventure, beginKey),
                new EventOption(this, Goodbye, goodbyeKey).ThatDecreasesMaxHp(GoodbyeMaxHpLoss),
            }
            : new List<EventOption>
            {
                new EventOption(this, BeginAdventure, beginKey),
                new EventOption(this, null, goodbyeLockedKey),
            };
    }

    private Task BeginAdventure()
    {
        var upgradable = PileType.Deck.GetPile(Owner!).Cards.Where(static c => c.IsUpgradable).ToList();
        int count = System.Math.Min(2, upgradable.Count);
        for (var i = 0; i < count; i++)
        {
            CardModel pick = Rng.NextItem(upgradable);
            upgradable.Remove(pick);
            CardCmd.Upgrade(pick, CardPreviewStyle.EventLayout);
        }

        SetEventFinished(L10NLookup($"{Id.Entry}.pages.BEGIN_ADVENTURE.description"));
        return Task.CompletedTask;
    }

    private async Task Goodbye()
    {
        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            GoodbyeMaxHpLoss,
            isFromCard: false);
        List<CardModel> cards = (await CardSelectCmd.FromDeckForRemoval(
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 2))).ToList();
        await CardPileCmd.RemoveFromDeck(cards);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.GOODBYE.description"));
    }
}
