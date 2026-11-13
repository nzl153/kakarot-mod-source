using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Config;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;

namespace KakarotMod.KakarotCode.Gravity;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
public static class KakarotGravityNewRunPatch
{
    public static void Postfix(RunState state)
    {
        if (state.Players.Count != 1 || state.Players[0].Character is not KakarotCharacter
            || KakarotModConfig.GravityTraining == KakarotGravityLevel.Off)
        {
            return;
        }

        var relic = (KakarotGravityWeights)ModelDb.Relic<KakarotGravityWeights>().ToMutable();
        relic.SetLevel((int)KakarotModConfig.GravityTraining);
        relic.FloorAddedToDeck = 1;
        state.Players[0].AddRelicInternal(relic);
        SaveManager.Instance.MarkRelicAsSeen(relic);
    }
}

[HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
public static class KakarotGravityMonsterHpPatch
{
    public static void Postfix(CombatState __instance, Creature __result)
    {
        if (__result.Side != CombatSide.Enemy || __result.Monster == null
            || __instance.RunState.Players.Count != 1)
        {
            return;
        }

        var relic = __instance.RunState.Players[0].GetRelic<KakarotGravityWeights>();
        if (relic is not { IsActive: true })
        {
            return;
        }

        __result.SetMaxHpInternal(__result.MaxHp * (1m + relic.HpBoostIn(__instance.Encounter) / 100m));
        __result.SetCurrentHpInternal(__result.MaxHp);
    }
}

// 怪物在战斗中重设血量（孵化、换阶段、复活、蜈蚣节进场）走的是这两个命令，在这里补上重力加成。
[HarmonyPatch]
public static class KakarotGravityMonsterHpResetPatch
{
    public static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxAndCurrentHp)),
        AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp)),
    ];

    public static void Prefix(Creature creature, ref decimal amount)
    {
        // 瀑布巨人末阶段把血量设成近乎无限，不能再乘。
        if (creature.Side != CombatSide.Enemy || creature.Monster == null || amount >= 100000000m
            || creature.CombatState is not { } state || state.RunState.Players.Count != 1)
        {
            return;
        }

        var relic = state.RunState.Players[0].GetRelic<KakarotGravityWeights>();
        if (relic is { IsActive: true })
        {
            amount = Math.Floor(amount * (1m + relic.HpBoostIn(state.Encounter) / 100m));
        }
    }
}

// 实验体复活是先设血量上限、再按原始数值回血，回血量也要跟着乘，否则复活后血不满。
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
public static class KakarotGravityTestSubjectRevivePatch
{
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (creature.Monster is not MegaCrit.Sts2.Core.Models.Monsters.TestSubject
            || creature.CombatState is not { } state || state.RunState.Players.Count != 1)
        {
            return;
        }

        var relic = state.RunState.Players[0].GetRelic<KakarotGravityWeights>();
        if (relic is { IsActive: true })
        {
            amount = Math.Floor(amount * (1m + relic.HpBoostIn(state.Encounter) / 100m));
        }
    }
}

[HarmonyPatch(typeof(NClickableControl), nameof(NClickableControl._GuiInput))]
public static class KakarotGravityTrainingClickPatch
{
    public static void Postfix(NClickableControl __instance, InputEvent inputEvent)
    {
        if (__instance is not NRelicInventoryHolder holder
            || inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
            || holder.Relic?.Model is not KakarotGravityWeights relic
            || !LocalContext.IsMe(relic.Owner) || !relic.IsActive
            || !CombatManager.Instance.IsInProgress || relic.UsedWeightsThisCombat || relic.RequestPending)
        {
            return;
        }

        relic.RequestPending = true;
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new KakarotGravityRemoveWeightsAction(relic.Owner));
    }
}
