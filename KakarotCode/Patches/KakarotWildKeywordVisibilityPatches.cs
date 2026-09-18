using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;
// Treat custom keyword slot 8 as card-face visible on known engine predicates.
public static class KakarotWildKeywordVisibilityPatches
{
    private static readonly string[] MethodNameHints =
    [
        "ShouldDisplayKeywordOnCard",
        "IsKeywordDisplayedOnCard",
        "IsDisplayedOnCardFace",
        "ShouldShowKeywordOnCard",
    ];

    public static void TryApply(Harmony harmony)
    {
        Type[] types;
        try
        {
            types = typeof(CardModel).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).Cast<Type>().ToArray();
        }

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.ReturnType != typeof(bool))
                {
                    continue;
                }

                var ps = method.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(CardKeyword))
                {
                    continue;
                }

                if (!MethodNameHints.Any(m => m == method.Name))
                {
                    continue;
                }

                try
                {
                    var postfix = new HarmonyMethod(
                        typeof(KakarotWildKeywordVisibilityPatches),
                        nameof(KeywordVisibilityPostfix));

                    harmony.Patch(method, postfix: postfix);
                    GD.Print($"[Kakarot] Patched keyword visibility: {type.Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[Kakarot] Skip keyword visibility patch {type.Name}.{method.Name}: {ex.Message}");
                }
            }
        }
    }

    public static void KeywordVisibilityPostfix(CardKeyword keyword, ref bool __result)
    {
        if (keyword == KakarotWildKeyword.Wild)
        {
            __result = true;
        }
    }
}
