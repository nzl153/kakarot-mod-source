using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotKiBlast() : KakarotCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override int CanonicalStarCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override bool ShouldGlowGoldInternal => WasLastCardPlayedSkill;

    private bool WasLastCardPlayedSkill
    {
        get
        {
            var entry = CombatManager.Instance.History.CardPlaysStarted.LastOrDefault(e =>
                e.CardPlay.Card.Owner == Owner && e.HappenedThisTurn(CombatState) && e.CardPlay.Card != this);
            return entry != null && entry.CardPlay.Card.Type == CardType.Skill;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool repeat = WasLastCardPlayedSkill;
        await FireKiBlast(choiceContext, cardPlay, echo: false);
        if (repeat)
        {
            // 第二发用回响变体：弹丸更白更大、命中多一圈扩散环，
            // 让「上一张是技能所以又打了一发」在画面上直接可读。
            await FireKiBlast(choiceContext, cardPlay, echo: true);
        }
    }

    // 弹丸从手心飞到敌人身上，飞到了伤害才结算 —— 所以走 BeforeDamage 而不是直接调用。
    private async Task FireKiBlast(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool echo)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .BeforeDamage(async () =>
            {
                KakarotCombatPresentation.PlayKiBlastProjectile(Owner, cardPlay, echo);
                await Cmd.Wait(KakarotCombatPresentation.KiBlastFlightSeconds);
            })
            .WithHitVfxNode(KakarotCombatPresentation.KiBlastHit(echo))
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
