using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Token;

public class KakarotDestruction() : KakarotCard(3, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
{
    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override bool IsPlayable =>
        base.IsPlayable &&
        Owner?.Creature is { } creature &&
        (creature.HasPower<KakarotSuperSaiyanGodPower>() ||
         creature.HasPower<KakarotSuperSaiyanBluePower>() ||
         creature.HasPower<KakarotUltraInstinctOmenPower>() ||
         creature.HasPower<KakarotPerfectUltraInstinctPower>());

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(50m, ValueProp.Move),
        new PowerVar<VulnerablePower>(3m),
        new DynamicVar("StrengthLoss", 99m),
    ];

    public static void UpgradeAllInstancesInCombat(Player player)
    {
        foreach (var card in player.PlayerCombatState.AllCards.ToList())
        {
            if (card is KakarotDestruction destruction && !destruction.IsUpgraded)
            {
                destruction.UpgradeInternal();
                destruction.FinalizeUpgradeInternal();
            }
        }
    }

    public static async Task TryRetrieveToHand(Player player, AbstractModel source)
    {
        var cs = player.PlayerCombatState;
        var id = ModelDb.Card<KakarotDestruction>().Id;

        foreach (var pile in new[] { cs.DrawPile, cs.DiscardPile, cs.ExhaustPile, cs.Hand })
        {
            var card = pile.Cards.FirstOrDefault(c => c.Id == id);
            if (card == null)
            {
                continue;
            }

            if (pile == cs.Hand)
            {
                return;
            }

            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
        await KakarotPowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await KakarotPowerCmd.Apply<DarkShacklesPower>(choiceContext, cardPlay.Target, DynamicVars["StrengthLoss"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Damage.UpgradeValueBy(15m);
    }
}
