using System.Collections.Generic;
using System.Linq;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotFightTogether() : KakarotCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("RagePerAttack", KakarotFightTogetherPower.RagePerAttackPerStack),
        new DynamicVar("BlockPerRage", KakarotFightTogetherPower.BlockPerRagePerStack),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<KakarotFightTogetherPower>()];

    protected override bool IsPlayable => base.IsPlayable && HasLivingTeammate();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotFightTogetherPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    private bool HasLivingTeammate()
    {
        var combatState = Owner?.Creature?.CombatState;
        return combatState != null
            && combatState.Players.Any(player => player != Owner
                && player.Creature.IsAlive
                && player.Creature.Side == Owner.Creature.Side);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords;
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
