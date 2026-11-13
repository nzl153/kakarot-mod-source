using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotShenronWishTransform() : KakarotCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotJusticeHeartRitualPower>(),
        HoverTipFactory.FromPower<KakarotWildRitualPower>(),
        HoverTipFactory.FromPower<KakarotSCellPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> options =
        [
            ModelDb.Card<KakarotTransformWishJustice>().ToMutable(),
            ModelDb.Card<KakarotTransformWishWild>().ToMutable(),
            ModelDb.Card<KakarotTransformWishSCell>().ToMutable(),
        ];
        foreach (var option in options)
        {
            option.Owner = Owner;
        }

        var picked = await CardSelectCmd.FromChooseACardScreen(choiceContext, options, Owner);
        int amount = picked!.DynamicVars["Amount"].IntValue;
        switch (picked)
        {
            case KakarotTransformWishJustice:
                await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(choiceContext, Owner, this, amount);
                break;
            case KakarotTransformWishWild:
                await KakarotWildRitualHandler.AddRitualProgress(choiceContext, Owner, this, amount);
                break;
            default:
                await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, amount, this);
                break;
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
