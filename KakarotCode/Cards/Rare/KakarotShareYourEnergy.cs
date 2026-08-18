using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotShareYourEnergy() : KakarotCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    // 只复制队友格挡，不修改其状态，避免跨玩家扣除造成联机不同步。
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool IsPlayable => base.IsPlayable && TryGetTeammate() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new DynamicVar("Draw", 1m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner.Creature.CombatState;
        decimal teammateBlock = combatState.Players
            .Where(p => p != Owner && p.Creature.IsAlive)
            .Sum(p => p.Creature.Block);

        if (teammateBlock > 0m)
        {
            var copiedBlock = new BlockVar(teammateBlock, ValueProp.Move);
            await CreatureCmd.GainBlock(Owner.Creature, copiedBlock, cardPlay);
        }

        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, Owner);
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
        DynamicVars["Draw"].UpgradeValueBy(1m);
    }
}
