#nullable enable
using System;
using Godot;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

public partial class FriezaSupernovaVfx : Node2D
{
    private const string ObserverName = "FriezaSupernovaObserver";
    private const string DetonatingMeta = "FriezaSupernovaDetonating";

    public static void PlayCharge(Creature creature, int level)
    {
        // Power is authoritative, including reconstruction after a visual reload.
        EnsureCharge(creature);
        Run(() =>
        {
            Sprite2D? sprite = FriezaBossVisuals.GetStaticModel(creature);
            if (sprite != null) FriezaBossVisuals.PlaySfx(sprite,
                level == 1 ? "supernova_charge" : "supernova_charge_full", -6f);
        });
    }

    private Sprite2D _sprite = null!;
    private NCreature? _creatureNode;
    private Node2D? _ball;
    private ShaderMaterial[] _materials = [];
    private float _diameter;
    private float _age;

    public override void _Ready()
    {
        _sprite = GetParent<Sprite2D>();
        for (Node? parent = _sprite.GetParent(); parent != null; parent = parent.GetParent())
        {
            if (parent is not NCreature creatureNode) continue;
            _creatureNode = creatureNode;
            break;
        }
        SetProcess(_creatureNode != null);
    }

    public override void _Process(double delta)
    {
        try { UpdateCharge((float)delta); }
        catch (Exception ex)
        {
            // A missing visual resource must not stop combat or retry every frame.
            SetProcess(false);
            if (_ball != null) _ball.Visible = false;
            GD.PrintErr($"[Kakarot][Frieza] charge observer failed: {ex}");
        }
    }

    private void UpdateCharge(float delta)
    {
        _age += delta;
        Creature creature = _creatureNode!.Entity;
        var power = creature.GetPower<FriezaSupernovaChargePower>();
        bool active = creature.IsAlive && CombatManager.Instance.IsInProgress &&
            creature.Monster is FriezaBoss { Phase: 3 } && power != null &&
            power.Damage < power.DynamicVars["Threshold"].IntValue;
        if (power == null && _sprite.HasMeta(DetonatingMeta)) _sprite.RemoveMeta(DetonatingMeta);
        if (!active || _sprite.HasMeta(DetonatingMeta))
        {
            if (_ball != null)
            {
                _ball.Visible = false;
                _ball.QueueFree();
                _ball = null;
            }
            _diameter = 0f;
            return;
        }
        if (_ball == null)
        {
            _ball = FriezaVfxKit.CreateField(this, "frieza_supernova", Vector2.One * 512f, out _materials);
            _ball.Name = "FriezaSupernovaCharge";
        }
        float targetDiameter = power!.Turns > 0 ? 350f : 470f;
        _diameter = Mathf.Lerp(_diameter, targetDiameter, 1f - Mathf.Exp(-delta * 10f));
        GlobalPosition = _sprite.ToGlobal(new Vector2(-180f, -1160f));
        GlobalScale = Vector2.One;
        _ball.Scale = Vector2.One * _diameter / 512f * (1f + 0.018f * Mathf.Sin(_age * 3.5f));
        foreach (var material in _materials) material.SetShaderParameter("age", _age);
    }

    public static void EnsureCharge(Creature creature)
    {
        Run(() =>
        {
            Sprite2D? sprite = FriezaBossVisuals.GetStaticModel(creature);
            if (sprite == null || sprite.GetNodeOrNull(ObserverName) != null) return;
            sprite.AddChild(new FriezaSupernovaVfx { Name = ObserverName, ZIndex = 21 });
        });
    }

    public static void PlayDetonate(Creature source)
    {
        Run(() =>
        {
            var room = NCombatRoom.Instance;
            var sourceSprite = FriezaBossVisuals.GetStaticModel(source);
            if (room == null || sourceSprite == null) return;
            // Existing callers visit every player; one stage-wide blast serves the whole resolution.
            if (sourceSprite.HasMeta(DetonatingMeta)) return;
            sourceSprite.SetMeta(DetonatingMeta, true);
            var observer = sourceSprite.GetNodeOrNull<Node2D>(ObserverName);
            var charge = observer?.GetNodeOrNull<Node2D>("FriezaSupernovaCharge");
            if (charge != null) charge.Visible = false;

            Vector2 center = Vector2.Zero;
            int count = 0;
            foreach (var player in source.CombatState!.Players)
            {
                var node = room.GetCreatureNode(player.Creature);
                if (!player.Creature.IsAlive || node == null) continue;
                center += node.VfxSpawnPosition;
                count++;
            }
            if (count == 0) return;
            center /= count;
            FriezaBossVisuals.PlaySfx(sourceSprite, "supernova_detonate", -3f);

            Node2D blast = FriezaVfxKit.CreateField(room.CombatVfxContainer,
                "frieza_supernova", Vector2.One * 1200f, out var materials);
            blast.Name = "FriezaSupernovaDetonate";
            blast.ZIndex = 26;
            blast.GlobalPosition = center;
            foreach (var material in materials) material.SetShaderParameter("burst", 1f);

            var ringTexture = KakarotCombatPresentation.CreateRingTexture(256, 0.028f, Colors.White, Colors.White);
            for (int layer = 0; layer < 2; layer++)
            {
                var ring = new Sprite2D
                {
                    Texture = ringTexture,
                    Modulate = layer == 0 ? new Color(0.10f, 0.025f, 0.012f, 0.88f) : new Color(1f, 0.62f, 0.20f, 0.8f),
                    Material = layer == 1 ? KakarotCombatPresentation.CreateAdditiveMaterial() : null,
                    ZIndex = 27,
                };
                room.CombatVfxContainer.AddChild(ring);
                ring.GlobalPosition = center + new Vector2(0f, 110f);
                ring.GlobalScale = new Vector2(0.8f, 0.14f);
                Tween ringTween = ring.CreateTween().SetParallel();
                ringTween.TweenProperty(ring, "global_scale", new Vector2(8.2f, 1.35f) * (layer == 0 ? 1.04f : 1f), 0.5f)
                    .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                ringTween.TweenProperty(ring, "modulate:a", 0f, 0.52f);
                ringTween.Chain().TweenCallback(Callable.From(ring.QueueFree));
            }
            void Update(float age)
            {
                float scale = 0.32f + 0.68f * (1f - Mathf.Exp(-age * 14f));
                blast.GlobalScale = Vector2.One * scale;
                foreach (var material in materials) material.SetShaderParameter("age", age);
            }
            Update(0f);
            Tween tween = blast.CreateTween();
            tween.TweenMethod(Callable.From<float>(Update), 0f, 0.85f, 0.85f);
            tween.TweenCallback(Callable.From(blast.QueueFree));
        });
    }

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { GD.PrintErr($"[Kakarot][Frieza] supernova vfx failed: {ex}"); }
    }
}
