using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Helpers;

internal static class KakarotPowerCmd
{
    private static PlayerChoiceContext NoChoiceContext()
    {
        return new ThrowingPlayerChoiceContext();
    }

    public static Task Apply<T>(Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent = false)
        where T : PowerModel, new()
    {
        return PowerCmd.Apply<T>(NoChoiceContext(), target, amount, applier, cardSource, silent);
    }

    public static Task Apply<T>(IEnumerable<Creature> targets, decimal amount, Creature applier, CardModel cardSource, bool silent = false)
        where T : PowerModel, new()
    {
        return PowerCmd.Apply<T>(NoChoiceContext(), targets, amount, applier, cardSource, silent);
    }

    public static Task Apply(PowerModel power, Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent = false)
    {
        return PowerCmd.Apply(NoChoiceContext(), power, target, amount, applier, cardSource, silent);
    }

    public static Task Apply<T>(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent = false)
        where T : PowerModel, new()
    {
        return PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource, silent);
    }

    public static Task Apply<T>(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, decimal amount, Creature applier, CardModel cardSource, bool silent = false)
        where T : PowerModel, new()
    {
        return PowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);
    }

    public static Task Apply(PlayerChoiceContext choiceContext, PowerModel power, Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent = false)
    {
        return PowerCmd.Apply(choiceContext, power, target, amount, applier, cardSource, silent);
    }
}
