using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Common;

/// <summary>集气：初始牌组中的 1 费技能；+3 怒气并将本牌移回手牌；升级：固有。</summary>
public class KakarotChargeUp() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Stars", 3m)];

    protected override bool IsPlayable => base.IsPlayable && Owner?.Creature?.GetPower<KakarotSuperSaiyanGodPower>() == null;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainStars(DynamicVars["Stars"].BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    protected override PileType GetResultPileTypeForCardPlay() => PileType.Hand;
}
