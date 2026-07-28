using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotKiBlast() : KakarotCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override int CanonicalStarCost => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override bool ShouldGlowGoldInternal => WasLastCardPlayedSkill;

    private bool WasLastCardPlayedSkill
    {
        get
        {
            var entry = CombatManager.Instance.History.CardPlaysStarted.LastOrDefault(e =>
                e.CardPlay.Card.Owner == Owner && e.HappenedThisTurn(CombatState) && e.CardPlay.Card != this);
            return entry != null && entry.CardPlay.Card.Type == CardType.Skill;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool repeat = WasLastCardPlayedSkill;
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
