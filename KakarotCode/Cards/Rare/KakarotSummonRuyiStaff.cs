using System.Collections.Generic;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotSummonRuyiStaff() : KakarotCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var staff = (KakarotRuyiStaff)ModelDb.Card<KakarotRuyiStaff>().ToMutable();
            if (IsUpgraded)
            {
                staff.UpgradeInternal();
                staff.FinalizeUpgradeInternal();
            }

            return [HoverTipFactory.FromCard(staff)];
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var staff = CombatState.CreateCard<KakarotRuyiStaff>(Owner);
        if (IsUpgraded)
        {
            staff.UpgradeInternal();
            staff.FinalizeUpgradeInternal();
        }

        await CardPileCmd.AddGeneratedCardToCombat(staff, PileType.Hand, Owner);

        await KakarotPowerCmd.Apply<KakarotRuyiStaffSummonedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }
}
