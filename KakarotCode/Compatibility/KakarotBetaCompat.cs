global using KakarotMod.KakarotCode.Compatibility;

using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Compatibility;

public static class KakarotBetaCompat
{
    public static AttackCommand CardAttack(
        CardModel card,
        CardPlay cardPlay,
        decimal damage,
        ValueProp props)
    {
#if STS2_BETA
        return CommonActions.CardAttack(card, cardPlay, cardPlay.Target, damage, props);
#else
        return CommonActions.CardAttack(card, cardPlay.Target, damage, props);
#endif
    }

    public static AttackCommand FromKakarotCard(
        this AttackCommand command,
        CardModel card,
        CardPlay cardPlay)
    {
#if STS2_BETA
        return command.FromCard(card, cardPlay);
#else
        return command.FromCard(card);
#endif
    }

    public static Task<IEnumerable<DamageResult>> DamageFromCard(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
    {
#if STS2_BETA
        return CreatureCmd.Damage(choiceContext, target, amount, props, dealer, cardSource, cardPlay);
#else
        return CreatureCmd.Damage(choiceContext, target, amount, props, dealer, cardSource);
#endif
    }

    public static Task LoseBlock(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature remover = null)
    {
#if STS2_BETA
        return CreatureCmd.LoseBlock(choiceContext, target, amount, remover);
#else
        return CreatureCmd.LoseBlock(target, amount);
#endif
    }
}
