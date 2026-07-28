using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotKaioken() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", 3m),
        new DynamicVar("Strength", 2m),
        new DynamicVar("Dexterity", 1m),
        new DynamicVar("HpLoss", 2m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotUltraInstinctTriggerHelper.RemoveUltraInstinctForms(Owner.Creature);
        await PowerCmd.Remove<KakarotSuperSaiyanBluePower>(Owner.Creature);
        await PowerCmd.Remove<KakarotSuperSaiyanGodPower>(Owner.Creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4Power>(Owner.Creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4EnergyCapPower>(Owner.Creature);
        await PowerCmd.Remove<SuperSaiyanFormPower>(Owner.Creature);
        await PowerCmd.Remove<SuperSaiyanRagePower>(Owner.Creature);

        var existing = Owner.Creature.GetPower<KaiokenPower>();
        var totalTurns = (existing?.Amount ?? 0) + (int)DynamicVars["Turns"].BaseValue;
        var totalStrength = (existing?.StrengthBonus ?? 0) + (int)DynamicVars["Strength"].BaseValue;
        var totalDexterity = (existing?.DexterityBonus ?? 0) + (int)DynamicVars["Dexterity"].BaseValue;
        var totalHpLoss = (existing?.HpLossPerTurn ?? 0) + (int)DynamicVars["HpLoss"].BaseValue;
        var castCount = (existing?.CastCount ?? 0) + 1;
        if (existing != null)
        {
            await PowerCmd.Remove<KaiokenPower>(Owner.Creature);
        }

        var kaiokenPower = (KaiokenPower)ModelDb.Power<KaiokenPower>().ToMutable();
        kaiokenPower.ConfigureTotals(totalStrength, totalDexterity, totalHpLoss, castCount);

        await KakarotPowerCmd.Apply(choiceContext, kaiokenPower, Owner.Creature, totalTurns, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HpLoss"].UpgradeValueBy(-1m);
    }

    protected override PileType GetResultPileTypeForCardPlay() => PileType.Hand;
}
