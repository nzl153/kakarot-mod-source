using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Ancient;

/// <summary>巅峰的极易：1 能量 1 怒气，5% 进入完美自在极意（成功则消耗）。</summary>
public class KakarotPeakUltraInstinct() : KakarotCard(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;

    public override int CanonicalStarCost => 1;

    private const int SuccessChance = 25;

    protected override bool IsPlayable =>
        base.IsPlayable && Owner?.Creature?.GetPower<KakarotUltraInstinctOmenPower>() != null;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        if (Owner.RunState.Rng.Niche.NextInt(100) >= SuccessChance)
        {
            return;
        }

        await PowerCmd.Remove<KakarotSuperSaiyan4Power>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4EnergyCapPower>(creature);
        await PowerCmd.Remove<KakarotUltraInstinctOmenPower>(creature);
        var perfect = (KakarotPerfectUltraInstinctPower)ModelDb.Power<KakarotPerfectUltraInstinctPower>().ToMutable();
        await KakarotPowerCmd.Apply(choiceContext, perfect, creature, 1, creature, this);
        await CardCmd.Exhaust(choiceContext, this, causedByEthereal: false, skipVisuals: false);
        KakarotFormVisuals.Refresh(creature);
    }
}
