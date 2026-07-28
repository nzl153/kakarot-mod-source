using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Events;

public sealed class KakarotTournamentCafeteria : EventModel
{
    private const decimal LightSnackHeal = 6m;
    private const decimal FeastGoldCost = 40m;
    private const decimal FeastHeal = 12m;

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
