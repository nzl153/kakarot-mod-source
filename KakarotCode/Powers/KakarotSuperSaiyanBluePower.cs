using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotSuperSaiyanBluePower : KakarotPower
{
    private const int StrengthBonus = 12;
    private const int DexterityBonus = 6;
    private const int RagePerTurn = 2;

    private int _strengthBonus = StrengthBonus;
    private int _dexterityBonus = DexterityBonus;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthBonus),
        new DynamicVar("Dexterity", DexterityBonus),
        new DynamicVar("Rage", RagePerTurn),
    ];

    // 超蓝以「替换超赛神」为代价，丢掉了全牌 -1 费这份持续经济。
    // 每回合固定怒气 + 变身时全牌升级共同承担补偿，单靠力量敏捷补不回来。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainStars(DynamicVars["Rage"].BaseValue, player);
    }

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(target, _strengthBonus, target, cardSource);
        await KakarotPowerCmd.Apply<DexterityPower>(target, _dexterityBonus, target, cardSource);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(oldOwner, -_strengthBonus, oldOwner, null);
        await KakarotPowerCmd.Apply<DexterityPower>(oldOwner, -_dexterityBonus, oldOwner, null);
        KakarotFormVisuals.Refresh(oldOwner);
    }
}
