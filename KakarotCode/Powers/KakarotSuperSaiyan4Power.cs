using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotSuperSaiyan4Power : KakarotPower
{
    // Use constants so removal always reverses exactly what application added.
    private const int StrengthBonus = 10;
    private const int DexterityBonus = 6;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthBonus),
        new DynamicVar("Dexterity", DexterityBonus),
    ];

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(target, StrengthBonus, target, cardSource);
        await KakarotPowerCmd.Apply<DexterityPower>(target, DexterityBonus, target, cardSource);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (Owner.Player != null)
        {
            KakarotWildHelper.UpgradeAllWildCardsInCombat(Owner.Player);
        }

        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainStars(2m, player);

        var hand = player.PlayerCombatState.Hand.Cards;
        if (hand.Count == 0)
        {
            return;
        }

        CardModel card = null;
        try
        {
            var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, player, 1, this);
            card = chosen.FirstOrDefault();
        }
        catch
        {
            return;
        }

        if (card == null)
        {
            return;
        }

        card.AddKeyword(KakarotWildKeyword.Wild);
        KakarotWildHelper.EnsureWildActsUpgradedIfSs4(player, card);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(oldOwner, -StrengthBonus, oldOwner, null);
        await KakarotPowerCmd.Apply<DexterityPower>(oldOwner, -DexterityBonus, oldOwner, null);
        KakarotFormVisuals.Refresh(oldOwner);
    }
}
