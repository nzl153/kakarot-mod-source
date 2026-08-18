using System.Collections.Generic;
using Godot;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Ancient;

public class KakarotSuperSaiyanBlueTransform() : KakarotCard(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            GD.PrintErr("[Kakarot] SuperSaiyanBlueTransform aborted: owner/creature is null.");
            return;
        }

        var creature = Owner.Creature;

        if (creature.HasPower<KakarotSuperSaiyanBluePower>())
        {
            await CardCmd.Exhaust(choiceContext, this, causedByEthereal: false, skipVisuals: false);
            return;
        }

        await KakarotUltraInstinctTriggerHelper.RemoveAllTransformPowers(creature);

        var blue = (KakarotSuperSaiyanBluePower)ModelDb.Power<KakarotSuperSaiyanBluePower>().ToMutable();
        await KakarotPowerCmd.Apply(choiceContext, blue, creature, 1, creature, this);

        KakarotFormVisuals.Refresh(creature);

        var combatState = CombatState ?? creature.CombatState;
        var playerCombatState = Owner.PlayerCombatState;
        if (combatState == null || playerCombatState?.Hand?.Cards == null)
        {
            GD.PrintErr("[Kakarot] SuperSaiyanBlueTransform skipped follow-up card operations: combat state is unavailable.");
            await CardCmd.Exhaust(choiceContext, this, causedByEthereal: false, skipVisuals: false);
            return;
        }

        // 对标原版「神化 Apotheosis」：只动战斗内实例，不污染跑图卡组；
        // IsUpgradable 会挡掉不可升级与已满级的牌。变身后新生成的牌不在此列。
        foreach (var card in playerCombatState.AllCards)
        {
            if (card != this && card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
        }

        var godKi = combatState.CreateCard<KakarotGodKi>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(godKi, PileType.Hand, Owner);
        KakarotGodKi.UpgradeAllInstancesInCombat(Owner);
        KakarotDestruction.UpgradeAllInstancesInCombat(Owner);
        await KakarotDestruction.TryRetrieveToHand(Owner, this);

        await CardCmd.Exhaust(choiceContext, this, causedByEthereal: false, skipVisuals: false);
    }
}
