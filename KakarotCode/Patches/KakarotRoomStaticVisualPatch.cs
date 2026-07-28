using System;
using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NMerchantRoom), "_Ready")]
public static class KakarotMerchantStaticVisualPatch
{
    private static readonly Vector2 MerchantPos = new(-70f, -195f);
    private static readonly Vector2 MerchantScale = new(0.192f, 0.192f);

    private const string MerchantModelPath = "res://Kakarot/Images/Charui/kakarot_merchant_model.png";
    private const string FallbackModelPath = "res://Kakarot/Images/Charui/kakarot_combat_model.png";

    public static void Postfix(NMerchantRoom __instance)
    {
        try
        {
            var players = Traverse.Create(__instance).Field("_players").GetValue<IList>();
            var visuals = __instance.PlayerVisuals;
            if (players == null || visuals == null)
            {
                return;
            }

            var count = Math.Min(players.Count, visuals.Count);
            for (var i = 0; i < count; i++)
            {
                if (players[i] is not Player p || !IsKakarot(p))
                {
                    continue;
                }

                ReplaceMerchantVisual(visuals[i]);
            }
        }
        catch
        {
        }
    }

    private static void ReplaceMerchantVisual(NMerchantCharacter visual)
    {
        foreach (var child in visual.GetChildren())
        {
            if (child is Node2D n && n.GetClass() == "SpineSprite")
            {
                n.Visible = false;
            }
        }

        var sprite = visual.GetNodeOrNull<Sprite2D>("KakarotStaticModel");
        if (sprite == null)
        {
            sprite = new Sprite2D();
            sprite.Name = "KakarotStaticModel";
            sprite.Position = MerchantPos;
            sprite.Scale = MerchantScale;
            visual.AddChild(sprite);
        }
        else
        {
            sprite.Position = MerchantPos;
            sprite.Scale = MerchantScale;
        }

        var path = ResourceLoader.Exists(MerchantModelPath) ? MerchantModelPath : FallbackModelPath;
        if (ResourceLoader.Exists(path))
        {
            sprite.Texture = ResourceLoader.Load<Texture2D>(path);
        }
    }

    private static bool IsKakarot(Player p)
    {
        var id = p?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(NFakeMerchant), "_Ready")]
public static class KakarotFakeMerchantStaticVisualPatch
{
    private static readonly Vector2 FakeMerchantPos = new(-70f, -195f);
    private static readonly Vector2 FakeMerchantScale = new(0.192f, 0.192f);

    private const string MerchantModelPath = "res://Kakarot/Images/Charui/kakarot_merchant_model.png";
    private const string FallbackModelPath = "res://Kakarot/Images/Charui/kakarot_combat_model.png";

    public static void Postfix(NFakeMerchant __instance)
    {
        try
        {
            var players = Traverse.Create(__instance).Field("_players").GetValue<IList>();
            var container = Traverse.Create(__instance).Field("_characterContainer").GetValue<Control>();
            if (players == null || container == null || players.Count != 1)
            {
                return;
            }

            if (players[0] is not Player p || !IsKakarot(p))
            {
                return;
            }

            foreach (var child in container.GetChildren())
            {
                if (child is Node2D visual)
                {
                    ReplaceFakeMerchantVisual(visual);
                    return;
                }
            }
        }
        catch
        {
        }
    }

    private static void ReplaceFakeMerchantVisual(Node2D visual)
    {
        HideExistingVisuals(visual);

        var sprite = visual.GetNodeOrNull<Sprite2D>("KakarotStaticModel");
        if (sprite == null)
        {
            sprite = new Sprite2D();
            sprite.Name = "KakarotStaticModel";
            visual.AddChild(sprite);
        }

        sprite.Visible = true;
        sprite.Position = FakeMerchantPos;
        sprite.Scale = FakeMerchantScale;
        sprite.ZIndex = 100;

        var path = ResourceLoader.Exists(MerchantModelPath) ? MerchantModelPath : FallbackModelPath;
        if (ResourceLoader.Exists(path))
        {
            sprite.Texture = ResourceLoader.Load<Texture2D>(path);
        }
    }

    private static void HideExistingVisuals(Node root)
    {
        var stack = new System.Collections.Generic.Stack<Node>();
        foreach (Node child in root.GetChildren())
        {
            stack.Push(child);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Name != "KakarotStaticModel" && node is CanvasItem canvasItem)
            {
                canvasItem.Visible = false;
            }

            foreach (Node child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    private static bool IsKakarot(Player p)
    {
        var id = p?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "_Ready")]
public static class KakarotRestSiteStaticVisualPatch
{
    private static readonly Vector2 RestSitePos = new(175f, -52f);
    private static readonly Vector2 RestSiteScale = new(0.29f, 0.29f);

    private const string RestSiteModelPath = "res://Kakarot/Images/Charui/kakarot_rest_site_model.png";
    private const string FallbackModelPath = "res://Kakarot/Images/Charui/kakarot_combat_model.png";

    public static void Postfix(NRestSiteRoom __instance)
    {
        try
        {
            foreach (var character in __instance.Characters)
            {
                if (!IsKakarot(character?.Player))
                {
                    continue;
                }

                ReplaceRestSiteVisual(character);
            }
        }
        catch
        {
        }
    }

    private static void ReplaceRestSiteVisual(NRestSiteCharacter character)
    {
        foreach (var child in character.GetChildren())
        {
            if (child is Node2D n && n.GetClass() == "SpineSprite")
            {
                n.Visible = false;
            }
        }

        var root = character.GetNodeOrNull<Control>("ControlRoot");
        if (root == null)
        {
            return;
        }

        var sprite = root.GetNodeOrNull<Sprite2D>("KakarotStaticModel");
        if (sprite == null)
        {
            sprite = new Sprite2D();
            sprite.Name = "KakarotStaticModel";
            sprite.Position = RestSitePos;
            sprite.Scale = RestSiteScale;
            sprite.ZIndex = 0;
            root.AddChild(sprite);
        }
        else
        {
            sprite.Position = RestSitePos;
            sprite.Scale = RestSiteScale;
            sprite.ZIndex = 0;
        }

        var path = ResourceLoader.Exists(RestSiteModelPath) ? RestSiteModelPath : FallbackModelPath;
        if (ResourceLoader.Exists(path))
        {
            sprite.Texture = ResourceLoader.Load<Texture2D>(path);
        }
    }

    private static bool IsKakarot(Player p)
    {
        var id = p?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
public static class KakarotMapCleanupPatch
{
    public static void Prefix()
    {
        // Keep active combat portraits while the map overlay is open.
        if (CombatManager.Instance is { IsOverOrEnding: false })
        {
            return;
        }

        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
        {
            return;
        }

        KakarotStaticModelVisibility.HideRoomModelsOnly(root);
        KakarotStaticModelVisibility.HideLingeringCombatVisual(root);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
public static class KakarotMapRestorePatch
{
    public static void Prefix(NMapScreen __instance, out bool __state)
    {
        __state = __instance.IsOpen;
    }

    public static void Postfix(NMapScreen __instance, bool __state)
    {
        try
        {
            if (!__state || __instance.IsTraveling)
            {
                return;
            }

            KakarotStaticModelVisibility.ShowRoomModelsOnly(__instance.GetTree()?.Root);
        }
        catch
        {
        }
    }
}

internal static class KakarotStaticModelVisibility
{
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

    public static void HideLivingCombatVisuals(Node root)
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
                    var creatureNode = FindCreatureAncestor(child);
                    if (creatureNode?.Entity?.IsDead != true)
                    {
                        canvasItem.Visible = false;
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
}

public static class KakarotRewardsScreenCleanupPatch
{
    public static void Postfix(NRewardsScreen __instance)
    {
        try
        {
            KakarotStaticModelVisibility.HideRoomModelsOnly(__instance.GetTree()?.Root);
        }
        catch
        {
        }
    }

    public static void AfterClosed(NRewardsScreen __instance)
    {
        try
        {
            KakarotStaticModelVisibility.ShowRoomModelsOnly(__instance.GetTree()?.Root);
        }
        catch
        {
        }
    }
}

public static class KakarotCardRewardSelectionCleanupPatch
{
    public static void Postfix(NCardRewardSelectionScreen __instance)
    {
        try
        {
            KakarotStaticModelVisibility.HideRoomModelsOnly(__instance.GetTree()?.Root);
            KakarotStaticModelVisibility.NormalizeRaisedKakarotCreatureLayers(__instance.GetTree()?.Root);
        }
        catch
        {
        }
    }

    public static void AfterClosed(NCardRewardSelectionScreen __instance)
    {
        try
        {
            var root = __instance.GetTree()?.Root;
            KakarotStaticModelVisibility.RestoreKakarotCreatureLayers(root);
            KakarotStaticModelVisibility.ShowRoomModelsOnly(root);
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen.AfterOverlayOpened))]
public static class KakarotCardSelectionCleanupPatch
{
    public static void Prefix()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
        {
            return;
        }

        KakarotStaticModelVisibility.HideAll(root);
    }

    [HarmonyPatch(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen.AfterOverlayClosed))]
    [HarmonyPostfix]
    public static void Postfix()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
        {
            return;
        }

        KakarotStaticModelVisibility.ShowAllActiveModels(root);
    }

}

[HarmonyPatch(typeof(NAbandonRunConfirmPopup), nameof(NAbandonRunConfirmPopup._EnterTree))]
public static class KakarotAbandonRunConfirmOpenPatch
{
    public static void Postfix(NAbandonRunConfirmPopup __instance)
    {
        try
        {
            KakarotStaticModelVisibility.HideAll(__instance.GetTree()?.Root);
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(NAbandonRunConfirmPopup), "OnNoButtonPressed")]
public static class KakarotAbandonRunConfirmCancelPatch
{
    public static void Prefix(NAbandonRunConfirmPopup __instance)
    {
        try
        {
            KakarotStaticModelVisibility.ShowAllActiveModels(__instance.GetTree()?.Root);
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "HideChoices")]
public static class KakarotRestSiteChoiceCleanupPatch
{
    public static void Prefix(NRestSiteRoom __instance)
    {
        try
        {
            foreach (var character in __instance.Characters)
            {
                var root = character?.GetNodeOrNull<Control>("ControlRoot");
                var staticModel = root?.GetNodeOrNull<CanvasItem>("KakarotStaticModel");
                if (staticModel != null)
                {
                    staticModel.Visible = false;
                }
            }
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "ShowChoices")]
public static class KakarotRestSiteChoiceRestorePatch
{
    public static void Prefix(NRestSiteRoom __instance)
    {
        try
        {
            foreach (var character in __instance.Characters)
            {
                var root = character?.GetNodeOrNull<Control>("ControlRoot");
                var staticModel = root?.GetNodeOrNull<CanvasItem>("KakarotStaticModel");
                if (staticModel != null)
                {
                    staticModel.Visible = true;
                }
            }
        }
        catch
        {
        }
    }
}

// The game-over overlay can reuse its node, so stale room portraits are cleared on every open.
[HarmonyPatch(typeof(NGameOverScreen), nameof(NGameOverScreen.AfterOverlayOpened))]
public static class KakarotGameOverCleanupPatch
{
    public static void Postfix(NGameOverScreen __instance)
    {
        try
        {
            var root = __instance.GetTree()?.Root;
            if (root == null)
            {
                return;
            }

            KakarotStaticModelVisibility.HideRoomModelsOnly(root);
            KakarotStaticModelVisibility.HideLivingCombatVisuals(root);
        }
        catch
        {
        }
    }
}
