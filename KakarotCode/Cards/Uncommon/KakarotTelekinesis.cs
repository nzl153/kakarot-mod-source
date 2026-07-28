using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotTelekinesis() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static readonly LocString DiscardPileSelectPrompt = new("combat_messages", "KAKAROTMOD_DISCARD_SELECT");

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("CardsToPick", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var discardPile = Owner.PlayerCombatState.DiscardPile;
        var discard = discardPile.Cards;
        if (discard.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(DiscardPileSelectPrompt, (int)DynamicVars["CardsToPick"].BaseValue);
        var chosen = await CardSelectCmd.FromSimpleGrid(choiceContext, discard, Owner, prefs);
        var list = chosen.ToList();
        foreach (var card in list)
        {
            if (card.Pile == discardPile)
            {
                await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, this, skipVisuals: false);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CardsToPick"].UpgradeValueBy(1m);
    }
}