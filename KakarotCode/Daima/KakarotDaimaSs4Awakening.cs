using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Daima;

// Ancient controls the frame; autoAdd:false keeps this wish reward out of every pool.
#pragma warning disable STS004 // Pool registration is deliberately disabled for this wish-only card.
public class KakarotDaimaSs4Awakening() : CustomCardModel(
    0, CardType.Skill, CardRarity.Ancient, TargetType.Self, showInCardLibrary: false, autoAdd: false)
{
    public override int CanonicalStarCost => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    // Rendering still needs the character's energy icon and frame tint.
    public override CardPoolModel Pool => ModelDb.CardPool<KakarotCardPool>();
    public override string PortraitPath => "res://Kakarot/Images/Cards/kakarot_daima_ss4_awakening.png";
    public override string BetaPortraitPath => PortraitPath;
    public override string CustomPortraitPath => "res://Kakarot/Images/Cards/Big/kakarot_daima_ss4_awakening.png";

    protected override bool IsPlayable => base.IsPlayable
        && Owner.Creature.HasPower<KakarotDaimaSs4KidPower>()
        && !Owner.PlayerCombatState.Hand.Cards.Any(card => card is KakarotOutOfControl);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        if (!creature.HasPower<KakarotDaimaSs4KidPower>())
        {
            return;
        }

        await KakarotDaima.SetStage<KakarotDaimaSs4Power>(creature);
        await PowerCmd.Remove<KakarotDaimaWishPower>(creature);
        // First turn's Vigor right away; later turns come from the stage power.
        await KakarotPowerCmd.Apply<VigorPower>(creature, KakarotDaimaSs4Power.VigorPerTurn, creature, null);
        var finisher = CombatState.CreateCard<KakarotDaimaBreakthroughKamehameha>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(finisher, PileType.Hand, Owner);
    }
}
#pragma warning restore STS004
