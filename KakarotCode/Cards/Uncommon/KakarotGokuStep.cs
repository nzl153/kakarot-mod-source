using System.Collections.Generic;

using KakarotMod.KakarotCode.Powers;

using MegaCrit.Sts2.Core.Commands;

using MegaCrit.Sts2.Core.Entities.Cards;

using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using MegaCrit.Sts2.Core.HoverTips;

using MegaCrit.Sts2.Core.Localization.DynamicVars;

using MegaCrit.Sts2.Core.Models;



namespace KakarotMod.KakarotCode.Cards.Uncommon;



/// <summary>This turn, your next Skill card is played twice.</summary>

public class KakarotGokuStep() : KakarotCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)

{

    protected override IEnumerable<DynamicVar> CanonicalVars =>

    [

        new PowerVar<KakarotDoubleNextSkillPower>(1m),

    ];



    protected override IEnumerable<IHoverTip> ExtraHoverTips =>

    [

        HoverTipFactory.FromPower<KakarotDoubleNextSkillPower>(),

    ];



    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)

    {

        await KakarotPowerCmd.Apply<KakarotDoubleNextSkillPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);

    }



    protected override void OnUpgrade()

    {

        EnergyCost.UpgradeBy(-1);

        AddKeyword(CardKeyword.Retain);

    }

}
