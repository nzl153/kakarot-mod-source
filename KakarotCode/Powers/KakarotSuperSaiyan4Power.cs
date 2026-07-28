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

/// <summary>Super Saiyan 4: +10 Strength / +6 Dexterity; turn start +2 rage and mark one hand card Wild (upgraded while this persists).</summary>
public sealed class KakarotSuperSaiyan4Power : KakarotPower
{
    // 直接走常量：原来�?`_strengthBonus = StrengthBonus` 的实例字段是无意义的�?
    // IsInstanced=true 时存档加载会重建实例并把字段重置为初值，期望"运行期修改并持久�?是错的�?
    // 改成常量后语义明确——AfterRemoved 减去的就�?BeforeApplied 加上的同一个值�?
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
            // Keep original design: player chooses 1 hand card to gain Wild.
            var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, player, 1, this);
            card = chosen.FirstOrDefault();
        }
        catch
        {
            // Never let selection UI issues break turn flow.
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
