using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotMadFist() : KakarotCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override int CanonicalStarCost => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
        await CardPileCmd.Draw(choiceContext, 1, Owner);
        if (Owner.PlayerCombatState.Hand.Cards.Count > 0)
        {
            var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, Owner, 1, this);
            foreach (var c in chosen)
            {
                await CardCmd.Discard(choiceContext, c);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}