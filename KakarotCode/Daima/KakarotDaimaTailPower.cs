using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Daima;

public sealed class KakarotDaimaTailPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => "kakarot_tail_prep_power.png".PowerImagePath();
    public override string CustomBigIconPath => "kakarot_tail_prep_power.png".BigPowerImagePath();

#if STS2_BETA
    public override decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource, CardPlay cardPlay)
#else
    public override decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
#endif
    {
        return dealer == Owner && KakarotDaimaStagePower.IsPoweredAttackSafe(props) ? Amount : 0m;
    }
}
