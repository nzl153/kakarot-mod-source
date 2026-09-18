using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Events;

[CustomID("KAKAROT_TOURNAMENT_CAFETERIA")]
public sealed class KakarotTournamentCafeteria : CustomEventModel
{
    private const decimal LightSnackHeal = 6m;
    private const decimal FeastGoldCost = 40m;
    private const decimal FeastHeal = 12m;

    public override string CustomInitialPortraitPath =>
        "res://images/events/kakarot_tournament_cafeteria.png";

    public override bool IsAllowed(IRunState runState) =>
        KakarotEventEligibility.AllowsExtraEvent(runState);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        string lightKey = $"{Id.Entry}.pages.INITIAL.options.LIGHT_SNACK";
        string feastKey = $"{Id.Entry}.pages.INITIAL.options.FEAST";
        string feastLockedKey = $"{Id.Entry}.pages.INITIAL.options.FEAST_LOCKED";

        return Owner != null && (decimal)Owner.Gold >= FeastGoldCost
            ? new List<EventOption>
            {
                new EventOption(this, LightSnack, lightKey),
                new EventOption(this, Feast, feastKey),
            }
            : new List<EventOption>
            {
                new EventOption(this, LightSnack, lightKey),
                new EventOption(this, null, feastLockedKey),
            };
    }

    private async Task LightSnack()
    {
        await CreatureCmd.Heal(Owner!.Creature, LightSnackHeal);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LIGHT_SNACK.description"));
    }

    private async Task Feast()
    {
        await PlayerCmd.LoseGold(FeastGoldCost, Owner!, GoldLossType.Spent);
        await CreatureCmd.Heal(Owner!.Creature, FeastHeal);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.FEAST.description"));
    }
}
