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

namespace KakarotMod.KakarotCode.Cards.Uncommon;

/// <summary>Co-op card: give a teammate energy, and advance Justice ritual if they are Kakarot.</summary>
public class KakarotTransmitKi() : KakarotCoopColorlessCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("RitualProgress", 1m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotJusticeHeartRitualPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var teammate = TryGetTeammate();
        if (teammate == null)
        {
            return;
        }

        await PlayerCmd.GainEnergy(1m, teammate);
        if (teammate.Character is KakarotCharacter)
        {
            await AdvanceJusticeHeartRitual(choiceContext, teammate);
        }
    }

    private async Task AdvanceJusticeHeartRitual(PlayerChoiceContext choiceContext, Player targetPlayer)
    {
        if (targetPlayer == null)
        {
            return;
        }

        await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(
            choiceContext,
            targetPlayer,
            this,
            DynamicVars["RitualProgress"].IntValue);
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
        DynamicVars["RitualProgress"].UpgradeValueBy(1m);
    }
}
