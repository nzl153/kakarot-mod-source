using System.Collections.Generic;
using System.Linq;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotLevitation() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static readonly LocString DiscardPileSelectPrompt = new("combat_messages", "KAKAROTMOD_DISCARD_SELECT");

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Dexterity", 3m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotTemporaryAgilityPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotTemporaryAgilityPower>(choiceContext, Owner.Creature, DynamicVars["Dexterity"].BaseValue, Owner.Creature, this);

        var discardPile = Owner.PlayerCombatState.DiscardPile;
        if (discardPile.Cards.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(DiscardPileSelectPrompt, 1);
        var chosen = await CardSelectCmd.FromSimpleGrid(choiceContext, discardPile.Cards, Owner, prefs);
        var card = chosen.FirstOrDefault();
        if (card?.Pile == discardPile)
        {
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, this, skipVisuals: false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Dexterity"].UpgradeValueBy(1m);
    }
}
