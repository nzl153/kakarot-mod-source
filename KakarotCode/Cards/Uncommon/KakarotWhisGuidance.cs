using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotWhisGuidance() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DodgePercent", 10m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotWhisGuidancePower>(),
        HoverTipFactory.FromPower<KakarotJusticeHeartRitualPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var add = DynamicVars["DodgePercent"].IntValue;
        var existing = Owner.Creature.GetPower<KakarotWhisGuidancePower>();
        var total = Math.Min(100, add + (int)(existing?.Amount ?? 0));
        var ritualProgressAtTurnStart = (existing?.RitualProgressAtTurnStart ?? 0) + 1;
        if (existing != null)
        {
            await PowerCmd.Remove<KakarotWhisGuidancePower>(Owner.Creature);
        }

        var power = (KakarotWhisGuidancePower)ModelDb.Power<KakarotWhisGuidancePower>().ToMutable();
        power.Configure(ritualProgressAtTurnStart);
        await KakarotPowerCmd.Apply(choiceContext, power, Owner.Creature, total, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DodgePercent"].UpgradeValueBy(5m);
    }
}
