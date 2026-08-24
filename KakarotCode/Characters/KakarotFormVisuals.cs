using System;
using Godot;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KakarotMod.KakarotCode.Characters;

public static class KakarotFormVisuals
{
    // Offsets compensate for center-anchored scaling.
    private static readonly Vector2 AlivePosition = new(0f, -180f);
    private static readonly Vector2 DeadPosition = new(0f, -68f);
    private static readonly Vector2 AliveScale = new(0.176f, 0.176f);
    private static readonly Vector2 DeadScale = new(0.30f, 0.30f);

    private const string BaseModelPath = "res://Kakarot/Images/Charui/kakarot_combat_model.png";
    private const string DeadModelPath = "res://Kakarot/Images/Charui/kakarot_combat_model_dead.png";
    private const string KaiokenModelPath = "res://Kakarot/Images/Charui/kakarot_combat_model_kaioken.png";
    private const string Ss1Path = "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_1.png";
    private const string Ss2Path = "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_2.png";
    private const string Ss3Path = "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_3.png";

    private const string UltraInstinctOmenPath = "res://Kakarot/Images/Charui/kakarot_combat_model_ultra_instinct_omen.png";
    private const string PerfectUltraInstinctPath = "res://Kakarot/Images/Charui/kakarot_combat_model_perfect_ultra_instinct.png";
    private static readonly string[] Ss4Paths =
    [
        "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_4.png",
        "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan4.png",
    ];
    private static readonly string[] SsBluePaths =
    [
        "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_blue.png",
        "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_god_blue.png",
    ];
    private static readonly string[] SsGodPaths =
    [
        "res://Kakarot/Images/Charui/kakarot_combat_model_super_saiyan_god.png",
        "res://Kakarot/Images/Charui/kakarot_combat_model_god.png",
    ];

    public static string ResolveKamehamehaPosePath(Creature creature) => ResolvePosePath(creature, "_kamehameha_pose");

    public static string ResolveAttackPosePath(Creature creature) => ResolvePosePath(creature, "_attack_pose");

    public static string ResolveHurtPosePath(Creature creature) => ResolvePosePath(creature, "_hurt_pose");

    // Missing form-specific poses fall back to the base pose.
    private static string ResolvePosePath(Creature creature, string suffix)
    {
        if (creature == null || !IsKakarot(creature))
        {
            return null;
        }

        string modelPath = ResolveModelPath(creature);
        string posePath = ToPosePath(modelPath, suffix);
        if (!string.IsNullOrEmpty(posePath) && ResourceLoader.Exists(posePath))
        {
            return posePath;
        }

        string basePosePath = ToPosePath(BaseModelPath, suffix);
        if (!string.IsNullOrEmpty(basePosePath) && ResourceLoader.Exists(basePosePath))
        {
            return basePosePath;
        }

        return null;
    }

    private static string ToPosePath(string modelPath, string suffix)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            return null;
        }

        const string ext = ".png";
        if (!modelPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return modelPath.Substring(0, modelPath.Length - ext.Length) + suffix + ext;
    }

    public static (Vector2 Pos, Vector2 Scale) GetRestTransform(Creature creature)
    {
        bool dead = creature?.IsDead ?? false;
        return dead ? (DeadPosition, DeadScale) : (AlivePosition, AliveScale);
    }

    public static void ApplyDeadVisual(Sprite2D staticModel)
    {
        ApplyDeadVisual(staticModel, DeadPosition, DeadScale);
    }

    public static void ApplyDeadVisual(Sprite2D staticModel, Vector2 position, Vector2 scale)
    {
        if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
        {
            return;
        }

        KakarotAuraVfx.Stop(staticModel);
        // 濒死余烬是 StaticModel 的子节点，不归 KakarotAuraVfx 管，得单独收。
        // 不收的后果：尸体继续冒火星，而且 StopAllMotion 杀掉循环 tween 后
        // Modulate 会停在半红的某一帧，尸体永久挂着粉色。
        KakarotCombatPresentation.SetNearDeathAura(staticModel, false);
        KakarotCombatPresentation.TryRestoreFromKamehamehaPose(staticModel);

        if (ResourceLoader.Exists(DeadModelPath))
        {
            staticModel.Texture = ResourceLoader.Load<Texture2D>(DeadModelPath);
        }

        staticModel.Visible = true;
        staticModel.ZIndex = 0;
        KakarotCombatPresentation.StopAllMotion(staticModel, position, scale);
    }

    public static void Refresh(Creature creature)
    {
        try
        {
            if (!IsKakarot(creature))
            {
                return;
            }

            var staticModel = NCombatRoom.Instance?
                .GetCreatureNode(creature)?
                .Visuals?
                .GetNodeOrNull<Sprite2D>("StaticModel");

            if (staticModel == null)
            {
                return;
            }

            if (creature.IsDead)
            {
                ApplyDeadVisual(staticModel);
                return;
            }

            // Cancel pending pose restoration before changing form textures.
            KakarotCombatPresentation.TryRestoreFromKamehamehaPose(staticModel);

            var selectedPath = ResolveModelPath(creature);
            if (!ResourceLoader.Exists(selectedPath))
            {
                return;
            }

            if (!string.Equals(staticModel.Texture?.ResourcePath, selectedPath, StringComparison.Ordinal))
            {
                staticModel.Texture = ResourceLoader.Load<Texture2D>(selectedPath);
            }

            staticModel.Visible = true;
            staticModel.Position = AlivePosition;
            staticModel.Scale = AliveScale;
            // Match the base character layer so combat effects render above the portrait.
            staticModel.ZIndex = 0;

            ApplyFacing(creature, staticModel);
            KakarotCombatPresentation.StartIdleBreathing(staticModel, AlivePosition, AliveScale);
            KakarotAuraVfx.NotifyFormChangedDeferred(staticModel, creature);
        }
        catch
        {
        }
    }

    public static void RefreshFacing(Creature creature)
    {
        try
        {
            if (!IsKakarot(creature) || creature.IsDead)
            {
                return;
            }

            var staticModel = NCombatRoom.Instance?
                .GetCreatureNode(creature)?
                .Visuals?
                .GetNodeOrNull<Sprite2D>("StaticModel");
            if (staticModel == null)
            {
                return;
            }

            ApplyFacing(creature, staticModel);
        }
        catch
        {
        }
    }

    public static void RefreshFacingToTarget(Creature creature, Creature target)
    {
        try
        {
            if (!IsKakarot(creature) || creature.IsDead || target == null || target.IsDead)
            {
                return;
            }

            var staticModel = NCombatRoom.Instance?
                .GetCreatureNode(creature)?
                .Visuals?
                .GetNodeOrNull<Sprite2D>("StaticModel");
            if (staticModel == null)
            {
                return;
            }

            if (!ApplyFacingToTarget(creature, target, staticModel))
            {
                ApplyFacing(creature, staticModel);
            }
        }
        catch
        {
        }
    }

    private static string ResolveModelPath(Creature creature)
    {
        if (creature.IsDead && ResourceLoader.Exists(DeadModelPath))
        {
            return DeadModelPath;
        }

        if (creature.HasPower<KakarotPerfectUltraInstinctPower>())
        {
            if (ResourceLoader.Exists(PerfectUltraInstinctPath))
            {
                return PerfectUltraInstinctPath;
            }

            if (ResourceLoader.Exists(UltraInstinctOmenPath))
            {
                return UltraInstinctOmenPath;
            }

            if (ResourceLoader.Exists(Ss3Path))
            {
                return Ss3Path;
            }
        }

        if (creature.HasPower<KakarotUltraInstinctOmenPower>())
        {
            if (ResourceLoader.Exists(UltraInstinctOmenPath))
            {
                return UltraInstinctOmenPath;
            }

            if (ResourceLoader.Exists(Ss3Path))
            {
                return Ss3Path;
            }
        }

        if (creature.HasPower<KakarotSuperSaiyan4Power>())
        {
            var ss4Path = FirstExistingPath(Ss4Paths);
            if (ss4Path != null)
            {
                return ss4Path;
            }
            return BaseModelPath;
        }

        if (creature.HasPower<KakarotSuperSaiyanBluePower>())
        {
            var ssbPath = FirstExistingPath(SsBluePaths);
            if (ssbPath != null)
            {
                return ssbPath;
            }
            return BaseModelPath;
        }

        if (creature.HasPower<KakarotSuperSaiyanGodPower>())
        {
            var ssgPath = FirstExistingPath(SsGodPaths);
            if (ssgPath != null)
            {
                return ssgPath;
            }
            return BaseModelPath;
        }

        var form = creature.GetPower<SuperSaiyanFormPower>();
        if (form != null)
        {
            var tier = (int)form.Amount;
            var path = tier switch
            {
                3 => Ss3Path,
                2 => Ss2Path,
                _ => Ss1Path,
            };
            if (ResourceLoader.Exists(path))
            {
                return path;
            }
        }

        if (creature.HasPower<KaiokenPower>() && ResourceLoader.Exists(KaiokenModelPath))
        {
            return KaiokenModelPath;
        }

        return BaseModelPath;
    }

    private static bool IsKakarot(Creature creature)
    {
        var entry = creature.Player?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(entry) && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstExistingPath(string[] paths)
    {
        foreach (var path in paths)
        {
            if (ResourceLoader.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static void ApplyFacing(Creature creature, Sprite2D staticModel)
    {
        var myNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        var parent = myNode?.GetParent();
        if (myNode == null || parent == null)
        {
            return;
        }

        Creature nearestTarget = null;
        var nearestDistance = float.MaxValue;
        foreach (var other in parent.GetChildren())
        {
            if (other is not MegaCrit.Sts2.Core.Nodes.Combat.NCreature otherNode)
            {
                continue;
            }

            var otherEntity = otherNode.Entity;
            if (otherEntity == null || otherEntity == creature || otherEntity.IsDead || otherEntity.Side == creature.Side)
            {
                continue;
            }

            var dx = Math.Abs(otherNode.GlobalPosition.X - myNode.GlobalPosition.X);
            if (!(dx < nearestDistance))
            {
                continue;
            }

            nearestDistance = dx;
            nearestTarget = otherEntity;
        }

        if (nearestTarget == null)
        {
            return;
        }

        var targetNode = NCombatRoom.Instance?.GetCreatureNode(nearestTarget);
        if (targetNode == null)
        {
            return;
        }

        var shouldFaceRight = targetNode.GlobalPosition.X >= myNode.GlobalPosition.X;
        staticModel.FlipH = !shouldFaceRight;
    }

    private static bool ApplyFacingToTarget(Creature creature, Creature target, Sprite2D staticModel)
    {
        var myNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        var targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (myNode == null || targetNode == null)
        {
            return false;
        }

        var shouldFaceRight = targetNode.GlobalPosition.X >= myNode.GlobalPosition.X;
        staticModel.FlipH = !shouldFaceRight;
        return true;
    }
}
