using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Daima;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Events;

public sealed class KakarotDaimaDemonRealmGate : CustomEventModel
{
    private const decimal MaxHpLoss = 10m;
    private const decimal HealAmount = 10m;

    public override string CustomInitialPortraitPath =>
        "res://images/events/kakarot_daima_demon_realm_gate.png";

    public override bool IsAllowed(IRunState runState) =>
        KakarotEventEligibility.AllowsExtraEvent(runState);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(
                    this,
                    KakarotEventEligibility.IsKakarot(Owner) ? EnterGate : null,
                    $"{Id.Entry}.pages.INITIAL.options.ENTER_GATE",
                    HoverTipFactory.FromRelic<KakarotDaimaThirdEye>())
                .ThatDecreasesMaxHp(MaxHpLoss),
            new EventOption(
                this,
                AcceptHealing,
                $"{Id.Entry}.pages.INITIAL.options.ACCEPT_HEALING"),
        ];
    }

    private async Task EnterGate()
    {
        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            MaxHpLoss,
            isFromCard: false);
        await RelicCmd.Obtain<KakarotDaimaThirdEye>(Owner);
        SetEventFinished(PageDescription("ENTER_GATE"));
    }

    private async Task AcceptHealing()
    {
        await CreatureCmd.Heal(Owner.Creature, HealAmount);
        SetEventFinished(PageDescription("ACCEPT_HEALING"));
    }
}
