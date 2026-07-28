using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

/// <summary>Deal damage, then choose hand card(s) to gain Wild.</summary>
public class KakarotSavagePunch() : KakarotCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new PowerVar<VulnerablePower>(1m),
        new DynamicVar("PickCount", 1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        WildKeywordHoverTip,
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
        if (cardPlay.Target != null && !cardPlay.Target.IsDead)
        {
            await KakarotPowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }

        // Exclude this card from count to avoid requesting impossible picks while it's being played.
        var selectableCount = Owner.PlayerCombatState.Hand.Cards.Count(c => c != this);
        var pickCount = System.Math.Min((int)DynamicVars["PickCount"].BaseValue, selectableCount);
        if (pickCount <= 0)
        {
            return;
        }

        IEnumerable<CardModel> chosen = [];
        try
        {
            // Use range selection so UI can safely confirm even when transient hand state changes.
            chosen = await KakarotCardSelectHelper.FromHandSelectRangeAsync(choiceContext, Owner, 0, pickCount, this);
        }
        catch
        {
            // Never block card resolution on selection UI issues.
        }

        foreach (var card in chosen.Where(c => c != null && c != this).Take(pickCount))
        {
            card.AddKeyword(KakarotWildKeyword.Wild);
            KakarotWildHelper.EnsureWildActsUpgradedIfSs4(Owner, card);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}
