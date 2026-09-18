using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

// Include the custom Wild slot in card-face keyword queries.
public static class KakarotWildKeywordFacePatches
{
    public static void TryApply(Harmony harmony)
    {
        TryPatchCanonicalKeywordsGetter(harmony);
        TryPatchCardModelInstancePredicates(harmony);
        TryPatchCardModelKeywordEnumerables(harmony);
    }

    private static void TryPatchCanonicalKeywordsGetter(Harmony harmony)
    {
        var getter = AccessTools.PropertyGetter(typeof(CardModel), "CanonicalKeywords");
        if (getter == null)
        {
            GD.PrintErr("[Kakarot] Wild face: CardModel.CanonicalKeywords getter not found.");
            return;
        }

        try
        {
            harmony.Patch(getter, postfix: new HarmonyMethod(typeof(KakarotWildKeywordFacePatches), nameof(CanonicalKeywordsPostfix)));
            GD.Print("[Kakarot] Wild face: patched CardModel.CanonicalKeywords getter.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] Wild face: CanonicalKeywords patch failed: {ex.Message}");
        }
    }

    public static void CanonicalKeywordsPostfix(CardModel __instance, ref IEnumerable<CardKeyword> __result)
    {
        if (__instance?.Keywords == null || __result == null)
        {
            return;
        }

        if (!__instance.Keywords.Contains(KakarotWildKeyword.Wild))
        {
            return;
        }

        var list = __result.ToList();
        if (list.Contains(KakarotWildKeyword.Wild))
        {
            return;
        }

        list.Add(KakarotWildKeyword.Wild);
        __result = list;
    }

    // Explicit names avoid patching unrelated methods added by later game builds.
    private static readonly string[] InstancePredicateNameWhitelist =
    [
        "ShouldShowKeywordOnCardFace",
        "ShouldDisplayKeywordOnCardFace",
        "IsKeywordVisibleOnCardFace",
        "IsKeywordDisplayedOnCard",
        "ShouldShowKeywordChip",
        "ShouldShowKeywordBanner",
        "IsKeywordInlineOnCard",
    ];

    private static void TryPatchCardModelInstancePredicates(Harmony harmony)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var found = 0;
        foreach (var name in InstancePredicateNameWhitelist)
        {
            var method = typeof(CardModel)
                .GetMethods(flags)
                .FirstOrDefault(m =>
                    m.Name == name
                    && !m.IsSpecialName
                    && m.ReturnType == typeof(bool)
                    && m.GetParameters() is { Length: 1 } ps
                    && ps[0].ParameterType == typeof(CardKeyword));

            if (method == null)
            {
                continue;
            }

            try
            {
                harmony.Patch(
                    method,
                    postfix: new HarmonyMethod(typeof(KakarotWildKeywordFacePatches), nameof(InstanceKeywordBoolPostfix)));
                GD.Print($"[Kakarot] Wild face: patched CardModel.{method.Name}(CardKeyword)");
                found++;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[Kakarot] Wild face: skip CardModel.{method.Name}: {ex.Message}");
            }
        }

        if (found == 0)
        {
            GD.Print("[Kakarot] Wild face: no whitelisted bool predicates on CardModel matched (likely fine if engine doesn't expose any).");
        }
    }

    public static void InstanceKeywordBoolPostfix(CardModel __instance, CardKeyword keyword, ref bool __result)
    {
        if (keyword != KakarotWildKeyword.Wild)
        {
            return;
        }

        if (__instance?.Keywords == null || !__instance.Keywords.Contains(KakarotWildKeyword.Wild))
        {
            return;
        }

        __result = true;
    }

    // Restrict list patches to known card-face keyword methods.
    private static readonly string[] KeywordEnumerableNameWhitelist =
    [
        "GetDisplayKeywords",
        "GetVisibleKeywords",
        "GetCardFaceKeywords",
        "GetKeywordsForDisplay",
    ];

    private static void TryPatchCardModelKeywordEnumerables(Harmony harmony)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (var name in KeywordEnumerableNameWhitelist)
        {
            var method = typeof(CardModel)
                .GetMethods(flags)
                .FirstOrDefault(m =>
                    m.Name == name
                    && !m.IsSpecialName
                    && m.GetParameters().Length == 0
                    && m.ReturnType == typeof(IEnumerable<CardKeyword>));

            if (method == null)
            {
                continue;
            }

            try
            {
                harmony.Patch(
                    method,
                    postfix: new HarmonyMethod(typeof(KakarotWildKeywordFacePatches), nameof(KeywordEnumerablePostfix)));
                GD.Print($"[Kakarot] Wild face: patched IEnumerable CardModel.{method.Name}()");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[Kakarot] Wild face: skip IEnumerable {method.Name}: {ex.Message}");
            }
        }
    }

    public static void KeywordEnumerablePostfix(CardModel __instance, ref IEnumerable<CardKeyword> __result)
    {
        CanonicalKeywordsPostfix(__instance, ref __result);
    }
}
