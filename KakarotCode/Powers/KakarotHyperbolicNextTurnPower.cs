using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotHyperbolicNextTurnPower : KakarotPower
{
    private decimal _energyPerStack = 3m;
    private decimal _starsPerStack = 3m;
    private decimal _drawPerStack = 1m;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => StackCount;

    private int StackCount => Math.Max(1, (int)Amount);
    private decimal Energy => _energyPerStack * StackCount;
    private decimal Stars => _starsPerStack * StackCount;
    private decimal Draw => _drawPerStack * StackCount;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Energy", 3m),
        new DynamicVar("Stars", 3m),
        new DynamicVar("Draw", 1m),
    ];

    public void Configure(decimal energy, decimal stars, decimal draw)
    {
        AssertMutable();
        _energyPerStack = energy;
        _starsPerStack = stars;
        _drawPerStack = draw;
        UpdateDynamicVars();
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        UpdateDynamicVars();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(Energy, player);
        await PlayerCmd.GainStars(Stars, player);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, (int)Draw, player);
        await PowerCmd.Remove(this);
    }

    private void UpdateDynamicVars()
    {
        DynamicVars["Energy"].BaseValue = Energy;
        DynamicVars["Stars"].BaseValue = Stars;
        DynamicVars["Draw"].BaseValue = Draw;
    }
}
