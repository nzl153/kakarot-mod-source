using System.Collections.Generic;
using System.Linq;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotAwakeningTrigger() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    // S 细胞推进超级赛亚人路线，与赛亚充电宝推进野性仪式的超级赛亚人 4 路线形成对称。
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool IsPlayable => base.IsPlayable && TryGetTeammate() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Heal", 5m),
        new DynamicVar("SCells", 3m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotSCellPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var teammate = TryGetTeammate();
        if (teammate == null)
        {
            return;
        }

        await CreatureCmd.Heal(teammate.Creature, DynamicVars["Heal"].BaseValue);
        if (teammate.Character is KakarotCharacter)
        {
            await KakarotSCellPower.TryGrantCellsAsync(
                choiceContext,
                teammate,
                DynamicVars["SCells"].IntValue,
                this);
        }
    }

    private Player TryGetTeammate()
    {
        var combatState = Owner?.Creature?.CombatState;
        if (combatState == null)
        {
            return null;
        }

        return combatState.Players.FirstOrDefault(p => p != null && p != Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
