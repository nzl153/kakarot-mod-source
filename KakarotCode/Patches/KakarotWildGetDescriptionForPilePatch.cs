#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;
// Custom keyword slot 8 is absent from the engine's card-face keyword order.
[HarmonyPatch]
public static class KakarotWildGetDescriptionForPilePatch
{
    // Lazy reflection prevents an upstream rename from breaking adjacent patches at type initialization.
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

    // 卡面使用公开重载，升级预览使用私有重载；两个入口都必须补上自定义 Wild 文本。
    private static IEnumerable<MethodBase> TargetMethods()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        return typeof(CardModel)
            .GetMethods(flags)
            .Where(method =>
                method.Name == nameof(CardModel.GetDescriptionForPile)
                && method.ReturnType == typeof(string)
                && method.GetParameters().Length >= 2
                && method.GetParameters()[0].ParameterType == typeof(PileType));
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
        // 只防重复前置：这一行永远加在最前面，所以用前缀判断。
        // 不能用 Contains——卡面文案本身就可能写着「赋予[gold]野性[/gold]。」
        //（激发野性/兽性冲拳/尾袭准备/大猿形态/野性全开），会被误判成已加过而整行丢失。
        if (text.StartsWith(line, StringComparison.Ordinal))
        {
            return;
        }

        __result = string.IsNullOrEmpty(text) ? line : line + "\n" + text;
    }
}
