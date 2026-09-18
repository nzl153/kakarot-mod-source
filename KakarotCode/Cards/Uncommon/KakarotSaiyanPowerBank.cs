using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotSaiyanPowerBank() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    // 联机专属化既补足协作卡结构，也移除单人野性成型后近乎无门槛的资源乘数。
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    protected override bool IsPlayable => base.IsPlayable && TryGetTeammate() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", 1m),
        new DynamicVar("Dexterity", 1m),
        new DynamicVar("RitualProgress", 1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotWildRitualPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var teammate = TryGetTeammate();
        if (teammate == null)
        {
            return;
        }

        await KakarotPowerCmd.Apply<StrengthPower>(
            choiceContext,
            teammate.Creature,
            DynamicVars["Strength"].BaseValue,
            Owner.Creature,
            this);
        await KakarotPowerCmd.Apply<DexterityPower>(
            choiceContext,
            teammate.Creature,
            DynamicVars["Dexterity"].BaseValue,
            Owner.Creature,
            this);

        if (teammate.Character is KakarotCharacter)
        {
            await KakarotWildRitualHandler.AddRitualProgress(
                choiceContext,
                teammate,
                this,
                DynamicVars["RitualProgress"].IntValue);
        }
    }

    private Player TryGetTeammate()
    {
        var combatState = Owner?.Creature?.CombatState;
        if (combatState == null)
        {
            return null;
        }

        return combatState.Players.FirstOrDefault(p => p != null && p != Owner && p.Creature != null && p.Creature.IsAlive);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
