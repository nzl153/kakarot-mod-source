using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace KakarotMod.KakarotCode.Characters;

// 眼射线、怒吼、舞空术、仙豆、把元气分给我吧 —— 原本只有通用前冲或通用技能音的招式。
public static partial class KakarotCombatPresentation
{
    private const string EyeBeamCardId = "KAKAROTMOD-KAKAROT_EYE_BEAM";
    private const string AncientEyeBeamCardId = "KAKAROTMOD-KAKAROT_ANCIENT_EYE_BEAM";
    private const string RoarCardId = "KAKAROTMOD-KAKAROT_ROAR";
    private const string LevitationCardId = "KAKAROTMOD-KAKAROT_LEVITATION";
    private const string SenzuBeanCardId = "KAKAROTMOD-KAKAROT_SENZU_BEAN";
    private const string ShareYourEnergyCardId = "KAKAROTMOD-KAKAROT_SHARE_YOUR_ENERGY";

    private const string EyeBeamSfxPath = "res://Kakarot/Audio/sfx/frieza/energy_bolt.wav";
    private const string RoarSfxPath = "res://Kakarot/Audio/sfx/frieza/emperor_shockwave.wav";

    // 各站姿立绘里瞳孔的像素坐标（2048 图，朝右）。逐张对过图，换立绘要重标。
    private static readonly Dictionary<string, Vector2> EyePixelByModel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kakarot_combat_model"] = new(1115, 454),
        ["kakarot_combat_model_kaioken"] = new(1113, 462),
        ["kakarot_combat_model_super_saiyan_1"] = new(1078, 466),
        ["kakarot_combat_model_super_saiyan_2"] = new(1060, 478),
        ["kakarot_combat_model_super_saiyan_3"] = new(1132, 463),
        ["kakarot_combat_model_super_saiyan_4"] = new(1081, 433),
        ["kakarot_combat_model_super_saiyan_blue"] = new(1083, 453),
        ["kakarot_combat_model_super_saiyan_god"] = new(1115, 456),
        ["kakarot_combat_model_ultra_instinct_omen"] = new(1115, 433),
        ["kakarot_combat_model_perfect_ultra_instinct"] = new(1089, 465),
        ["kakarot_combat_model_daima_kid"] = new(1140, 935),
        ["kakarot_combat_model_daima_ss1"] = new(1147, 952),
        ["kakarot_combat_model_daima_ss3"] = new(1185, 990),
        ["kakarot_combat_model_daima_ss4"] = new(1224, 521),
        ["kakarot_combat_model_daima_ss4_kid"] = new(1177, 975),
    };

    private static readonly Color SenzuGlow = new(0.55f, 1f, 0.4f);
    private static readonly Color ShareEnergyGlow = new(0.7f, 0.9f, 1f);

    private static void TryPlaySkillMoveVfx(Player player, string cardEntryId, Node visualsRoot, Sprite2D staticModel)
    {
        if (string.IsNullOrEmpty(cardEntryId) || visualsRoot is not Node2D anchor)
        {
            return;
        }

        try
        {
            switch (cardEntryId.ToUpperInvariant())
            {
                case RoarCardId:
                    PlaySfx(visualsRoot, "RoarSfx", RoarSfxPath, -6f, false);
                    PlayRoarVfx(anchor, player.Creature, staticModel);
                    break;
                case LevitationCardId:
                    PlayLevitationVfx(anchor, player.Creature, staticModel);
                    break;
                case SenzuBeanCardId:
                    PlaySenzuVfx(anchor, player.Creature, staticModel);
                    break;
                case ShareYourEnergyCardId:
                    PlayShareEnergyVfx(anchor, staticModel);
                    break;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] skill move {cardEntryId} failed: {ex.Message}");
        }
    }

    // 立绘像素坐标 → 世界坐标，处理居中和左右翻转。
    private static Vector2 ModelPixelToWorld(Sprite2D staticModel, Vector2 pixel)
    {
        Rect2 rect = staticModel.GetRect();
        var texSize = staticModel.Texture.GetSize();
        var local = rect.Position + pixel * (rect.Size / texSize);
        if (staticModel.FlipH)
        {
            local.X = rect.Position.X + rect.Size.X - (local.X - rect.Position.X);
        }

        return staticModel.ToGlobal(local);
    }

    private static Vector2 ResolveEyeWorld(Sprite2D staticModel)
    {
        var file = staticModel.Texture?.ResourcePath?.GetFile().GetBaseName() ?? string.Empty;
        var pixel = EyePixelByModel.TryGetValue(file, out var p) ? p : EyePixelByModel["kakarot_combat_model"];
        return ModelPixelToWorld(staticModel, pixel);
    }

    private static Vector2 ResolveFeetWorld(Sprite2D staticModel)
    {
        var size = staticModel.Texture.GetSize();
        return ModelPixelToWorld(staticModel, new Vector2(size.X * 0.5f, size.Y * 0.97f));
    }

    // 眼射线和气合眼共用；气合眼更粗更久，震得更狠。
    private static void PlayEyeBeamVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel, bool ancient)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel?.Texture == null)
            {
                return;
            }

            if (cardPlay.Target != null)
            {
                KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
            }

            PlaySfx(visualsRoot, "EyeBeamSfx", EyeBeamSfxPath, ancient ? 0f : -2f, true);

            var eye = ResolveEyeWorld(staticModel);
            var target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (target == null || !TryGetCreatureChestWorld(target, out var end))
            {
                end = eye + new Vector2(staticModel.FlipH ? -600f : 600f, 0f);
            }

            var holder = new Node2D { Name = "KakarotEyeBeamFx", ZIndex = 30 };
            anchor.AddChild(holder);
            holder.GlobalPosition = eye;
            holder.GlobalScale = Vector2.One;
            holder.GlobalRotation = (end - eye).Angle();
            float length = eye.DistanceTo(end);

            // 三层光（淡蓝外晕、白中层、细实芯）+ 冲在最前的弹头，眼睛先闪一颗星芒。
            float w = ancient ? 3.6f : 2.4f;
            const float grow = 0.06f;
            float hold = ancient ? 0.34f : 0.24f;
            float thin = ancient ? 0.22f : 0.18f;
            float sx = length / 64f;

            var layers = new (Sprite2D Strip, float Width)[]
            {
                (CreateBeamStrip(64, new Color(0.72f, 0.88f, 1f, ancient ? 0.6f : 0.5f), 1.3f), 20f * w),
                (CreateBeamStrip(64, new Color(1f, 1f, 1f, 0.85f), 2.2f), 8f * w),
                (CreateBeamStrip(64, Colors.White, 6f), 3f * w),
            };
            var tw = holder.CreateTween().SetParallel(true);
            foreach (var (strip, width) in layers)
            {
                strip.Scale = new Vector2(0f, width / 64f);
                holder.AddChild(strip);
                tw.TweenProperty(strip, "scale:x", sx, grow).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                // 收尾不是淡出，而是整条光被捏细到消失。
                tw.TweenProperty(strip, "scale:y", 0f, thin).SetDelay(grow + hold)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            var core = layers[2].Strip;
            tw.TweenProperty(core, "scale:y", 5f * w / 64f, hold * 0.5f).SetDelay(grow);
            tw.TweenProperty(core, "scale:y", 3f * w / 64f, hold * 0.5f).SetDelay(grow + hold * 0.5f);

            var bolt = CreateStreak(new Color(1f, 1f, 1f), 46f * w, 14f * w);
            holder.AddChild(bolt);
            tw.TweenProperty(bolt, "position:x", length, grow).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(bolt, "modulate:a", 0f, 0.1).SetDelay(grow);

            SpawnStarGlint(holder, Vector2.Zero, 64f * w, ancient ? 0.5f : 0.4f, 0f);
            SpawnStarGlint(holder, new Vector2(length, 0f), 52f * w, ancient ? 0.42f : 0.34f, grow);

            tw.TweenCallback(Callable.From(() =>
            {
                if (ancient)
                {
                    PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Short, hitStop: true);
                    SpawnRadialBurst(anchor, end, 4f, 0.4f, new Color(0.8f, 0.92f, 1f));
                }
                else
                {
                    PlayImpactFeedback(ShakeStrength.Medium, ShakeDuration.Short, hitStop: false);
                    SpawnRadialBurst(anchor, end, 2.6f, 0.3f, new Color(0.8f, 0.92f, 1f));
                }
            })).SetDelay(grow);

            tw.Chain().TweenInterval(0.05);
            tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(holder)));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] eye beam failed: {ex.Message}");
        }
    }

    // 64 宽的横条，纵向按 falloff 做柔边；在 holder 里从原点向 +X 拉长。
    private static Sprite2D CreateBeamStrip(int height, Color color, float falloff)
    {
        var img = Image.CreateEmpty(64, height, false, Image.Format.Rgba8);
        float half = (height - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float a = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(y - half) / half), falloff);
            for (int x = 0; x < 64; x++)
            {
                img.SetPixel(x, y, new Color(color.R, color.G, color.B, color.A * a));
            }
        }

        return new Sprite2D
        {
            Texture = ImageTexture.CreateFromImage(img),
            Centered = false,
            Offset = new Vector2(0f, -height * 0.5f),
            Material = CreateAdditiveMaterial(),
        };
    }

    private static ImageTexture _streakTex;

    // 两头尖的光条，长宽单位是世界像素。
    private static Sprite2D CreateStreak(Color color, float length, float width)
    {
        if (_streakTex == null)
        {
            var img = Image.CreateEmpty(64, 16, false, Image.Format.Rgba8);
            for (int y = 0; y < 16; y++)
            {
                float v = 1f - Mathf.Abs(y - 7.5f) / 7.5f;
                for (int x = 0; x < 64; x++)
                {
                    float u = 1f - Mathf.Abs(x - 31.5f) / 31.5f;
                    img.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(u, 1.6f) * Mathf.Pow(v, 2f)));
                }
            }
            _streakTex = ImageTexture.CreateFromImage(img);
        }

        return new Sprite2D
        {
            Texture = _streakTex,
            Material = CreateAdditiveMaterial(),
            Modulate = color,
            Scale = new Vector2(length / 64f, width / 16f),
        };
    }

    // 十字星芒：一横一竖两道尖光 + 小光点，弹出来再缩没。
    private static void SpawnStarGlint(Node2D holder, Vector2 at, float size, float life, float delay)
    {
        var star = new Node2D { Position = at, Scale = Vector2.Zero, ZIndex = 2 };
        holder.AddChild(star);
        star.AddChild(CreateStreak(Colors.White, size, size * 0.12f));
        var v = CreateStreak(Colors.White, size * 0.55f, size * 0.1f);
        v.Rotation = Mathf.Pi / 2f;
        star.AddChild(v);
        star.AddChild(new Sprite2D
        {
            Texture = GetKiGlowTexture(),
            Material = CreateAdditiveMaterial(),
            Modulate = new Color(0.85f, 0.94f, 1f, 0.9f),
            Scale = new Vector2(size / 200f, size / 200f),
        });

        var tw = star.CreateTween();
        tw.TweenInterval(delay);
        tw.TweenProperty(star, "scale", Vector2.One, life * 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(star, "scale", Vector2.Zero, life * 0.75f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        star.CreateTween().TweenProperty(star, "rotation", 0.35f, life).SetDelay(delay);
    }

    // 怒吼：胸口推出三圈气浪，碎尘往外飞，中震。
    private static void PlayRoarVfx(Node2D anchor, Creature creature, Sprite2D staticModel)
    {
        if (!TryGetCreatureChestWorld(creature, out var chest))
        {
            chest = staticModel.GlobalPosition;
        }

        var holder = new Node2D { Name = "KakarotRoarFx", ZIndex = 30 };
        anchor.AddChild(holder);
        holder.GlobalPosition = chest;
        holder.GlobalScale = Vector2.One;

        var air = new Color(1f, 0.96f, 0.86f);
        SpawnKiFlash(holder, 1.6f, air, 0.22f);
        for (int i = 0; i < 3; i++)
        {
            SpawnKiRing(holder, 3.2f + i * 1.4f, 0.62f, air, 0.42f, i * 0.09f);
        }

        SpawnKiMotes(holder, 46, 0.9f, air, new Color(0.85f, 0.8f, 0.7f), 0f, Mathf.Pi, 320f, 760f, 0.5f, 0f);
        PlayImpactFeedback(ShakeStrength.Medium, ShakeDuration.Normal, hitStop: false);

        var life = holder.CreateTween();
        life.TweenInterval(1.0);
        life.TweenCallback(Callable.From(() => FreeIfValid(holder)));
    }

    // 舞空术：脚下一圈气流压开，人浮起来晃两下再落回原位。
    private static void PlayLevitationVfx(Node2D anchor, Creature creature, Sprite2D staticModel)
    {
        var feet = ResolveFeetWorld(staticModel);
        var holder = new Node2D { Name = "KakarotLevitationFx", ZIndex = 20 };
        anchor.AddChild(holder);
        holder.GlobalPosition = feet;
        holder.GlobalScale = Vector2.One;

        var air = new Color(0.92f, 0.97f, 1f);
        SpawnKiRing(holder, 2.6f, 0.22f, air, 0.45f, 0f);
        SpawnKiRing(holder, 1.8f, 0.22f, air, 0.4f, 0.12f);
        SpawnKiMotes(holder, 26, 0.7f, air, new Color(0.8f, 0.85f, 0.9f), 0f, Mathf.Pi, 160f, 360f, 0.45f, 0f);

        var life = holder.CreateTween();
        life.TweenInterval(0.9);
        life.TweenCallback(Callable.From(() => FreeIfValid(holder)));

        var rest = KakarotFormVisuals.GetRestTransform(creature);
        var up = rest.Pos + new Vector2(0f, -36f);
        var tw = staticModel.CreateTween();
        tw.TweenProperty(staticModel, "position", up, 0.28).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(staticModel, "position", up + new Vector2(0f, 8f), 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(staticModel, "position", up, 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(staticModel, "position", rest.Pos, 0.32).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
    }

    // 仙豆：身上一层绿光鼓一下，光点往上飘。
    private static void PlaySenzuVfx(Node2D anchor, Creature creature, Sprite2D staticModel)
    {
        if (!TryGetCreatureChestWorld(creature, out var chest))
        {
            chest = staticModel.GlobalPosition;
        }

        var holder = new Node2D { Name = "KakarotSenzuFx", ZIndex = 30 };
        anchor.AddChild(holder);
        holder.GlobalPosition = chest;
        holder.GlobalScale = Vector2.One;

        var body = new Sprite2D
        {
            Texture = CreateSoftGlowTexture(160, SenzuGlow, new Color(SenzuGlow.R, SenzuGlow.G, SenzuGlow.B, 0f), 0f, 1.4f),
            Material = CreateAdditiveMaterial(),
            Scale = new Vector2(1.2f, 2.2f),
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        holder.AddChild(body);
        var bt = body.CreateTween();
        bt.TweenProperty(body, "modulate:a", 0.75f, 0.18);
        bt.TweenProperty(body, "modulate:a", 0f, 0.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

        SpawnKiRing(holder, 2.4f, 0.35f, SenzuGlow, 0.5f, 0.05f);
        SpawnKiMotes(holder, 36, 0.8f, new Color(0.9f, 1f, 0.8f), SenzuGlow, -Mathf.Pi / 2f, Mathf.DegToRad(40f), 120f, 300f, 0.8f, -60f);

        var life = holder.CreateTween();
        life.TweenInterval(1.1);
        life.TweenCallback(Callable.From(() => FreeIfValid(holder)));
    }

    // 把元气分给我吧：光点从四周汇到头顶，聚成一团小光再散掉（不扔出去，那是元气弹的事）。
    private static void PlayShareEnergyVfx(Node2D anchor, Sprite2D staticModel)
    {
        var gather = staticModel.GlobalPosition + new Vector2(0f, -380f);
        SpawnSpiritBombMotes(anchor, gather);

        var holder = new Node2D { Name = "KakarotShareEnergyFx", ZIndex = 30 };
        anchor.AddChild(holder);
        holder.GlobalPosition = gather;
        holder.GlobalScale = Vector2.One;

        var orb = new Sprite2D
        {
            Texture = CreateSoftGlowTexture(128, Colors.White, ShareEnergyGlow, 0.35f, 1.7f),
            Material = CreateAdditiveMaterial(),
            Scale = new Vector2(0.05f, 0.05f),
        };
        holder.AddChild(orb);

        var tw = holder.CreateTween();
        tw.TweenProperty(orb, "scale", new Vector2(0.6f, 0.6f), 0.75).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(holder, "modulate:a", 0f, 0.35);
        tw.TweenCallback(Callable.From(() => FreeIfValid(holder)));
    }
}
