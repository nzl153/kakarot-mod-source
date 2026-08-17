using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotSpiritBomb() : KakarotCard(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override bool HasStarCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("PerX", 8m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        await KakarotUltraInstinctTriggerHelper.RemoveUltraInstinctForms(creature);
        await PowerCmd.Remove<KakarotSuperSaiyanBluePower>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyanGodPower>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4Power>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4EnergyCapPower>(creature);
        await PowerCmd.Remove<SuperSaiyanFormPower>(creature);
        await PowerCmd.Remove<SuperSaiyanRagePower>(creature);
        await PowerCmd.Remove<KaiokenPower>(creature);

        var starX = ResolveStarXValue();
        var totalDamage = starX * DynamicVars["PerX"].BaseValue;
        if (totalDamage <= 0)
        {
            return;
        }

        await DamageCmd.Attack(totalDamage).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PerX"].UpgradeValueBy(2m);
    }
}
