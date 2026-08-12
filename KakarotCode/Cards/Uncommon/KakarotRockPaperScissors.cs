using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotRockPaperScissors() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BuffChance", 40m),
        new DynamicVar("SelfDamageChance", 30m),
        new DynamicVar("Strength", 2m),
        new DynamicVar("Dexterity", 2m),
        new DynamicVar("SelfDamage", 5m),
        new DynamicVar("EnergyGain", 2m),
        new DynamicVar("Draw", 1m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var roll = Owner.RunState.Rng.Niche.NextInt(100);
        if (roll < DynamicVars["BuffChance"].IntValue)
        {
            await KakarotPowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["Strength"].BaseValue, Owner.Creature, this);
            await KakarotPowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars["Dexterity"].BaseValue, Owner.Creature, this);
            return;
        }

        if (roll < DynamicVars["BuffChance"].IntValue + DynamicVars["SelfDamageChance"].IntValue)
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner.Creature,
                DynamicVars["SelfDamage"].BaseValue,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Owner.Creature,
                this);
            return;
        }

        await PlayerCmd.GainEnergy(DynamicVars["EnergyGain"].BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);
    }
}
