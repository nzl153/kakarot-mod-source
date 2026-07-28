using System.Collections.Generic;
using System.Linq;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotCombo() : KakarotCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override bool ShouldGlowGoldInternal => WasLastCardPlayedAttack;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    private bool WasLastCardPlayedAttack
    {
        get
        {
            var entry = CombatManager.Instance.History.CardPlaysStarted.LastOrDefault(e =>
                e.CardPlay.Card.Owner == Owner && e.HappenedThisTurn(CombatState) && e.CardPlay.Card != this);
            return entry != null && entry.CardPlay.Card.Type == CardType.Attack;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool repeat = WasLastCardPlayedAttack;
        await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
        if (repeat)
        {
            await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
