using System.Threading.Tasks;
using KakarotMod.KakarotCode.Cards.Rare;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Potions;

public sealed class KakarotSenzuJar : KakarotPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature target)
    {
        var senzuBean = Owner.Creature.CombatState.CreateCard<KakarotSenzuBean>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(
            senzuBean,
            PileType.Hand,
            Owner);
    }
}
