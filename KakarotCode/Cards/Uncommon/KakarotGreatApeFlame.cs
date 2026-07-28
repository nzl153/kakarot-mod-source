using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotGreatApeFlame() : KakarotCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("StarCost", 2m), new DamageVar(10m, ValueProp.Move)];

    public override int CanonicalStarCost => (int)DynamicVars["StarCost"].BaseValue;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);

        if (Owner.PlayerCombatState.Hand.Cards.Count > 0)
        {
            int cardsToExhaust = System.Math.Min(2, Owner.PlayerCombatState.Hand.Cards.Count);
            var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, Owner, cardsToExhaust, this);
            foreach (var c in chosen)
            {
                await CardCmd.Exhaust(choiceContext, c);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
