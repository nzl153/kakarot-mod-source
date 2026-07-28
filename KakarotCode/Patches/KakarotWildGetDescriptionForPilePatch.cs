#nullable enable
using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// 卡面黄字来自 <see cref="CardModel.GetDescriptionForPile"/>：引擎用 <see cref="CardKeywordOrder"/> 的固定数组决定
/// 哪些 <see cref="CardKeyword"/> 会调用 <see cref="CardKeywordExtensions.GetCardText"/>。自定义槽位 8 不在数组里，
/// 因此永远不会出现；在 postfix 中按同样格式补上。
/// </summary>
[HarmonyPatch]
public static class KakarotWildGetDescriptionForPilePatch
{
    /// <summary>
    /// internal 类型 <c>CardKeywordExtensions</c> 上的公共方法，需反射调用。
    /// 用 <see cref="Lazy{T}"/> 包装：若游戏更新去掉/改名该方法，整个 class 不会因为
    /// 静态构造抛 <see cref="TypeInitializationException"/> 而连带毁掉相邻补丁。
    /// </summary>
    private static readonly Lazy<MethodInfo?> GetCardTextMethod = new(() =>
    {
        try
        {
            var type = typeof(CardKeyword).Assembly
                .GetType("MegaCrit.Sts2.Core.Entities.Cards.CardKeywordExtensions");
            if (type == null)
            {
                GD.PrintErr("[Kakarot] Wild card text: CardKeywordExtensions type not found; Wild keyword line will be skipped.");
                return null;
            }

            var mi = type.GetMethod(
                "GetCardText",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(CardKeyword)],
                null);
            if (mi == null)
            {
                GD.PrintErr("[Kakarot] Wild card text: GetCardText(CardKeyword) not found; Wild keyword line will be skipped.");
            }

            return mi;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] Wild card text reflection failed: {ex.Message}");
            return null;
        }
    });

    private static MethodBase TargetMethod()
    {
        var previewType = typeof(CardModel).GetNestedType("DescriptionPreviewType", BindingFlags.NonPublic);
        return AccessTools.Method(
            typeof(CardModel),
            nameof(CardModel.GetDescriptionForPile),
            [typeof(PileType), previewType!, typeof(Creature)]);
    }

    [HarmonyPostfix]
    public static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance.Keywords == null || !__instance.Keywords.Contains(KakarotWildKeyword.Wild))
        {
            return;
        }

        var mi = GetCardTextMethod.Value;
        if (mi == null)
        {
            return;
        }

        string? line;
        try
        {
            line = mi.Invoke(null, [KakarotWildKeyword.Wild]) as string;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] Wild card text invoke failed: {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        var text = __result ?? string.Empty;
        if (text.Contains(line, StringComparison.Ordinal))
        {
            return;
        }

        __result = string.IsNullOrEmpty(text) ? line : line + "\n" + text;
    }
}
