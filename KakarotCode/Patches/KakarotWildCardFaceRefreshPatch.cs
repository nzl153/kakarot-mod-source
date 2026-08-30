#nullable enable
using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace KakarotMod.KakarotCode.Patches;

// NCard subscribes to Affliction/Enchantment changes but not to KeywordsChanged, so a card that
// gains Wild while already on screen keeps its stale face text until something else redraws it.
// Piggyback on the engine's own subscribe/unsubscribe pair so the handler follows node pooling.
public static class KakarotWildCardFaceRefreshPatch
{
    // One handler per NCard: the same delegate instance must be used for -= to detach.
    private static readonly ConditionalWeakTable<NCard, Action> Handlers = new();

    private static Action GetHandler(NCard card)
    {
        return Handlers.GetValue(card, static node => () => QueueRefresh(node));
    }

    private static void QueueRefresh(NCard card)
    {
        // Keywords change mid-resolution; defer so the face refresh lands after the model settles.
        Callable.From(() =>
        {
            try
            {
                if (!GodotObject.IsInstanceValid(card) || card.Model == null || !card.IsNodeReady())
                {
                    return;
                }

                card.UpdateVisuals(card.DisplayingPile, CardPreviewMode.Normal);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[Kakarot] Wild face refresh failed: {ex.Message}");
            }
        }).CallDeferred();
    }

    [HarmonyPatch(typeof(NCard), "SubscribeToModel")]
    public static class Subscribe
    {
        public static void Postfix(NCard __instance, CardModel? model)
        {
            if (model == null || !__instance.IsInsideTree())
            {
                return;
            }

            var handler = GetHandler(__instance);
            model.KeywordsChanged -= handler;
            model.KeywordsChanged += handler;
        }
    }

    [HarmonyPatch(typeof(NCard), "UnsubscribeFromModel")]
    public static class Unsubscribe
    {
        public static void Postfix(NCard __instance, CardModel? model)
        {
            if (model == null)
            {
                return;
            }

            model.KeywordsChanged -= GetHandler(__instance);
        }
    }
}
