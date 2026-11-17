using KakarotMod.KakarotCode.Cards.Rare;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Cards.Uncommon;
using KakarotMod.KakarotCode.Daima;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotFatherSonWavePower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static bool IsKamehameha(CardModel card) =>
        card is KakarotKamehameha or KakarotTenfoldKamehameha or KakarotDragonFist or KakarotDaimaBreakthroughKamehameha;

#if STS2_V109
    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource, CardPlay cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource)
#endif
    {
        return dealer == Owner && cardSource != null && IsKamehameha(cardSource) ? 1m + Amount / 100m : 1m;
    }
}
