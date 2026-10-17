using System;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace KakarotMod.KakarotCode.Patches;

internal static class KakarotStaticModelVisibility
{
    private static readonly Vector2 MerchantGameOverDeadPos = new(-70f, -83f);
    private static readonly Vector2 MerchantGameOverDeadScale = new(0.327f, 0.327f);

    private const string OriginalZIndexMeta = "kakarot_original_creature_z_index";
    private const string OriginalZRelativeMeta = "kakarot_original_creature_z_relative";

    public static void TryApplyOverlayPatches(Harmony harmony)
    {
        TryPatchOverlay(harmony, typeof(NRewardsScreen), nameof(NRewardsScreen.AfterOverlayOpened),
            typeof(KakarotRewardsScreenCleanupPatch), nameof(KakarotRewardsScreenCleanupPatch.Postfix));
        TryPatchOverlay(harmony, typeof(NRewardsScreen), nameof(NRewardsScreen.AfterOverlayClosed),
            typeof(KakarotRewardsScreenCleanupPatch), nameof(KakarotRewardsScreenCleanupPatch.AfterClosed));

        TryPatchOverlay(harmony, typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayOpened),
            typeof(KakarotCardRewardSelectionCleanupPatch), nameof(KakarotCardRewardSelectionCleanupPatch.Postfix));
        TryPatchOverlay(harmony, typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayClosed),
            typeof(KakarotCardRewardSelectionCleanupPatch), nameof(KakarotCardRewardSelectionCleanupPatch.AfterClosed));

    }

    private static void TryPatchOverlay(Harmony harmony, Type targetType, string targetMethodName, Type patchType, string patchMethodName)
    {
        try
        {
            var target = AccessTools.Method(targetType, targetMethodName);
            var patch = AccessTools.Method(patchType, patchMethodName);
            if (target == null || patch == null)
            {
                GD.Print($"[Kakarot] Skipped optional static-model overlay patch: {targetType.Name}.{targetMethodName}");
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(patch));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] Optional static-model overlay patch failed for {targetType.Name}.{targetMethodName}: {ex.Message}");
        }
    }

    public static void HideAll(Node root)
    {
        SetModels(root, false, includeRoomStaticModel: true);
    }

    public static void ShowAllActiveModels(Node root)
    {
        SetModels(root, true, includeRoomStaticModel: true);
    }

    // Only target the mod's room portrait; the base StaticModel belongs to the game.
    public static void HideRoomModelsOnly(Node root)
    {
        SetRoomModels(root, false);
    }

    public static void ShowRoomModelsOnly(Node root)
    {
        SetRoomModels(root, true);
    }

    private static void SetRoomModels(Node root, bool visible)
    {
        if (root == null)
        {
            return;
        }

        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (Node child in node.GetChildren())
            {
                if (child.Name == "KakarotStaticModel" && child is CanvasItem canvasItem)
                {
                    canvasItem.Visible = visible;
                }

                stack.Push(child);
            }
        }
    }

    // Room-replacement mods can leave completed CombatRoom visuals mounted.
    // Hide only stale KakarotVisual nodes and never touch active combat rooms.
    public static void HideLingeringCombatVisual(Node root)
    {
        if (root == null)
        {
            return;
        }

        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (Node child in node.GetChildren())
            {
                if (child.Name == "KakarotVisual" && child is CanvasItem canvasItem)
                {
                    canvasItem.Visible = false;
                }

                stack.Push(child);
            }
        }
    }

    public static void PrepareGameOverVisuals(Node root, Node preservedRoot)
    {
        if (root == null || preservedRoot == null)
        {
            return;
        }

        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (Node child in node.GetChildren())
            {
                var isPreserved = HasAncestor(child, preservedRoot);
                if (child.Name == "KakarotVisual" && child is CanvasItem canvasItem)
                {
                    canvasItem.Visible = isPreserved;
                    if (isPreserved)
                    {
                        var staticModel = child.GetNodeOrNull<Sprite2D>("StaticModel");
                        if (staticModel != null)
                        {
                            KakarotFormVisuals.ApplyDeadVisual(staticModel);
                        }
                    }
                }
                else if (child.Name == "KakarotStaticModel" && child is Sprite2D roomStaticModel)
                {
                    roomStaticModel.Visible = isPreserved;
                    if (isPreserved)
                    {
                        KakarotFormVisuals.ApplyDeadVisual(
                            roomStaticModel,
                            MerchantGameOverDeadPos,
                            MerchantGameOverDeadScale);
                    }
                }

                stack.Push(child);
            }
        }
    }

    public static void NormalizeRaisedKakarotCreatureLayers(Node root)
    {
        if (root == null)
        {
            return;
        }

        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (Node child in node.GetChildren())
            {
                if (child.Name == "KakarotVisual")
                {
                    var creatureNode = FindCreatureAncestor(child);
                    if (creatureNode != null && !creatureNode.HasMeta(OriginalZIndexMeta))
                    {
                        creatureNode.SetMeta(OriginalZIndexMeta, creatureNode.ZIndex);
                        creatureNode.SetMeta(OriginalZRelativeMeta, creatureNode.ZAsRelative);
                        creatureNode.ZIndex = 0;
                        creatureNode.ZAsRelative = false;
                    }
                }

                stack.Push(child);
            }
        }
    }

    public static void RestoreKakarotCreatureLayers(Node root)
    {
        if (root == null)
        {
            return;
        }

        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NCreature creatureNode && creatureNode.HasMeta(OriginalZIndexMeta))
            {
                creatureNode.ZIndex = creatureNode.GetMeta(OriginalZIndexMeta).AsInt32();
                creatureNode.ZAsRelative = creatureNode.GetMeta(OriginalZRelativeMeta).AsBool();
                creatureNode.RemoveMeta(OriginalZIndexMeta);
                creatureNode.RemoveMeta(OriginalZRelativeMeta);
            }

            foreach (Node child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    private static NCreature FindCreatureAncestor(Node node)
    {
        for (var current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current is NCreature creatureNode)
            {
                return creatureNode;
            }
        }

        return null;
    }

    private static void SetModels(Node root, bool visible, bool includeRoomStaticModel)
    {
        if (root == null)
        {
            return;
        }

        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (Node child in node.GetChildren())
            {
                if (IsKakarotStaticModel(child, includeRoomStaticModel) && child is CanvasItem canvasItem)
                {
                    canvasItem.Visible = visible;
                }

                stack.Push(child);
            }
        }
    }

    private static bool IsKakarotStaticModel(Node node, bool includeRoomStaticModel)
    {
        if (node.Name == "StaticModel" && HasAncestorNamed(node, "KakarotVisual"))
        {
            return true;
        }

        return includeRoomStaticModel && node.Name == "KakarotStaticModel";
    }

    private static bool HasAncestorNamed(Node node, string ancestorName)
    {
        for (var current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current.Name == ancestorName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAncestor(Node node, Node ancestor)
    {
        for (var current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }
}
