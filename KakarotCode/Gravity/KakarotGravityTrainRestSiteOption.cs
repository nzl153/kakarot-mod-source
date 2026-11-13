using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Extensions;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Gravity;

public sealed class KakarotGravityTrainRestSiteOption(Player owner) : CustomRestSiteOption(owner)
{
    public override string OptionId => "KAKAROTMOD_GRAVITY_TRAIN";
    public override string CustomIconPath => "kakarot_gravity_weights.png".BigRelicImagePath();
    public override IEnumerable<string> AssetPaths => [CustomIconPath];

    public override LocString Description
    {
        get
        {
            var description = base.Description;
            description.Add(new HealVar(Owner.Creature.MaxHp * 0.1m));
            return description;
        }
    }

    public override async Task<bool> OnSelect()
    {
        var relic = Owner.GetRelic<KakarotGravityWeights>();
        if (relic is not { IsActive: true, Level: >= 3 })
        {
            return false;
        }

        relic.AddTrainingStrength();
        await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp * 0.1m);
        return true;
    }
}
