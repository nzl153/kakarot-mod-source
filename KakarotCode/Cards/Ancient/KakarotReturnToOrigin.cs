using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace KakarotMod.KakarotCode.Cards.Ancient;

/// <summary>Granted after 5 Wild plays this combat. Enter Super Saiyan 4; add Tenfold Kamehameha to hand. Exhaust.</summary>
public class KakarotReturnToOrigin() : KakarotCard(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool IsPlayable
    {
        get
        {
            try
            {
                return base.IsPlayable && Owner?.Creature?.GetPower<KakarotSuperSaiyan4Power>() == null;
            }
            catch (CanonicalModelException)
            {
                return base.IsPlayable;
            }
            catch (MutableModelException)
            {
                return base.IsPlayable;
            }
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotSuperSaiyan4Power>(),
        HoverTipFactory.FromPower<KakarotSuperSaiyan4EnergyCapPower>(),
        HoverTipFactory.FromCard<KakarotTenfoldKamehameha>(),
        WildKeywordHoverTip,
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;

        await KakarotUltraInstinctTriggerHelper.RemoveAllTransformPowers(creature);

        var ss4 = (KakarotSuperSaiyan4Power)ModelDb.Power<KakarotSuperSaiyan4Power>().ToMutable();
        await KakarotPowerCmd.Apply(choiceContext, ss4, creature, 1, creature, this);

        var cap = (KakarotSuperSaiyan4EnergyCapPower)ModelDb.Power<KakarotSuperSaiyan4EnergyCapPower>().ToMutable();
        await KakarotPowerCmd.Apply(choiceContext, cap, creature, 1, creature, this);

        var tenfold = CombatState.CreateCard<KakarotTenfoldKamehameha>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(tenfold, PileType.Hand, Owner);

        KakarotWildHelper.UpgradeAllWildCardsInCombat(Owner);

        KakarotFormVisuals.Refresh(creature);

        await CardCmd.Exhaust(choiceContext, this, causedByEthereal: false, skipVisuals: false);
    }
}
