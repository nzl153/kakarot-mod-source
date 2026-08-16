using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KakarotMod.KakarotCode.Characters;

// 纯表现节点：不返回 Task，也不进入任何战斗结算链。
internal sealed partial class KakarotAuraVfx : Node2D
{
    private Sprite2D _staticModel;
    private Creature _creature;
    private ShaderMaterial _backMaterial;
    private ShaderMaterial _frontMaterial;
    private LightningBolt[] _lightningBolts = [];

    private KakarotAuraPalette _fromPalette;
    private KakarotAuraPalette _targetPalette;
    private KakarotAuraForm _targetForm;
    private float _elapsed;
    private float _duration;
    private float _colorElapsed;
    private float _lightningElapsed;
    private float _lightningNextFlashDelay;
    private float _lightningFlashDuration;
    private float _currentIntensity;
    private float _visualActivity;
    private float _burstStartIntensity;
    private float _retireStartIntensity;
    private float _retireElapsed;
    private int _lightningStep;
    private int _activeLightningBoltCount;
    private bool _formRefreshPending;
    private bool _burstActive;
    private bool _retiring;

    private sealed class LightningBolt(
        Line2D mainGlow,
        Line2D mainCore,
        Line2D branchGlow,
        Line2D branchCore)
    {
        internal Line2D MainGlow { get; } = mainGlow;
        internal Line2D MainCore { get; } = mainCore;
        internal Line2D BranchGlow { get; } = branchGlow;
        internal Line2D BranchCore { get; } = branchCore;

        internal void SetVisible(bool visible)
        {
            MainGlow.Visible = visible;
            MainCore.Visible = visible;
            BranchGlow.Visible = visible;
            BranchCore.Visible = visible;
        }
    }

    internal static void TryPlay(Sprite2D staticModel, Creature creature, string cardEntryId)
    {
        try
        {
            if (!IsUsable(staticModel, creature) ||
                !KakarotAuraFormResolver.TryResolvePlayback(
                    cardEntryId,
                    creature,
                    out var playback,
                    out var target,
                    out var waitForSuccessfulForm) ||
                staticModel.GetParent() is not Node2D parent)
            {
                return;
            }

            var aura = parent.GetNodeOrNull<KakarotAuraVfx>(KakarotAuraConfig.NodeName);
            if (aura == null)
            {
                aura = new KakarotAuraVfx { Name = KakarotAuraConfig.NodeName };
                parent.AddChild(aura);
                if (!aura.Initialize(staticModel, creature))
                {
                    aura.QueueFree();
                    return;
                }
            }

            aura.Restart(playback, target, waitForSuccessfulForm);
        }
        catch
        {
            // 表现异常不得中断出牌。
        }
    }

    internal static void NotifyFormChangedDeferred(Sprite2D staticModel, Creature creature)
    {
        try
        {
            if (!IsUsable(staticModel, creature) || staticModel.GetParent() is not Node parent)
            {
                return;
            }

            var form = KakarotAuraFormResolver.ResolveCurrent(creature);
            var aura = parent.GetNodeOrNull<KakarotAuraVfx>(KakarotAuraConfig.NodeName);
            if (aura == null && KakarotAuraConfig.IsPersistent(form))
            {
                aura = new KakarotAuraVfx { Name = KakarotAuraConfig.NodeName };
                parent.AddChild(aura);
                if (!aura.Initialize(staticModel, creature))
                {
                    aura.QueueFree();
                    return;
                }

                aura.StartPersistent(form);
            }

            if (aura != null)
            {
                // 下一帧再读取 Power，保持通知完全脱离结算调用栈。
                aura._formRefreshPending = true;
            }
        }
        catch
        {
        }
    }

    internal static void Stop(Sprite2D staticModel)
    {
        try
        {
            if (staticModel == null || !GodotObject.IsInstanceValid(staticModel) ||
                staticModel.GetParent() is not Node parent)
            {
                return;
            }

            parent.GetNodeOrNull<KakarotAuraVfx>(KakarotAuraConfig.NodeName)?.QueueFree();
        }
        catch
        {
        }
    }

    public override void _Process(double delta)
    {
        if (!IsUsable(_staticModel, _creature))
        {
            QueueFree();
            return;
        }

        if (_formRefreshPending)
        {
            _formRefreshPending = false;
            HandleFormRefresh(KakarotAuraFormResolver.ResolveCurrent(_creature));
        }

        float dt = (float)Math.Max(delta, 0.0);
        _elapsed += dt;
        _colorElapsed += dt;
        _lightningElapsed += dt;
        if (_retiring)
        {
            _retireElapsed += dt;
        }

        if (_burstActive && _elapsed >= _duration)
        {
            CompleteBurst();
            if (IsQueuedForDeletion())
            {
                return;
            }
        }

        if (_retiring && _retireElapsed >= KakarotAuraConfig.FadeOutDuration)
        {
            QueueFree();
            return;
        }

        SyncToModel();
        UpdatePalette();
        UpdateVisibility();
        UpdateLightning();
    }

    private bool Initialize(Sprite2D staticModel, Creature creature)
    {
        if (!ResourceLoader.Exists(KakarotAuraConfig.ShaderPath) ||
            !ResourceLoader.Exists(KakarotAuraConfig.TexturePath))
        {
            return false;
        }

        var shader = ResourceLoader.Load<Shader>(KakarotAuraConfig.ShaderPath);
        var texture = ResourceLoader.Load<Texture2D>(KakarotAuraConfig.TexturePath);
        if (shader == null || texture == null)
        {
            return false;
        }

        _staticModel = staticModel;
        _creature = creature;
        _backMaterial = CreateMaterial(
            shader,
            false,
            KakarotAuraConfig.BackOpacity,
            0f,
            KakarotAuraConfig.BackFlowSpeed);
        _frontMaterial = CreateMaterial(
            shader,
            true,
            KakarotAuraConfig.FrontOpacity,
            KakarotAuraConfig.FrontPhaseOffset,
            KakarotAuraConfig.FrontFlowSpeed);

        AddChild(CreateAuraLayer("BackAura", KakarotAuraConfig.BackZIndex, texture, _backMaterial));
        AddChild(CreateAuraLayer("FrontAura", KakarotAuraConfig.FrontZIndex, texture, _frontMaterial));
        CreateLightningArcs();

        // 后层与人物同 Z 时依赖树顺序：气焰根节点必须排在人物之前。
        if (GetParent() is Node parent)
        {
            parent.MoveChild(this, staticModel.GetIndex());
        }

        SetProcess(true);
        return true;
    }

    private void Restart(KakarotAuraPlayback playback, KakarotAuraForm target, bool waitForSuccessfulForm)
    {
        _burstStartIntensity = _currentIntensity;
        _burstActive = true;
        _retiring = false;
        _retireElapsed = 0f;
        _elapsed = 0f;
        _duration = waitForSuccessfulForm
            ? KakarotAuraConfig.SuccessfulFormWaitDuration
            : playback == KakarotAuraPlayback.Transform
                ? KakarotAuraConfig.TransformDuration
                : KakarotAuraConfig.ChargeDuration;
        _colorElapsed = KakarotAuraConfig.ColorTransitionDuration;
        _lightningElapsed = KakarotAuraConfig.PersistentLightningMaxInterval;
        _lightningNextFlashDelay = 0f;
        _formRefreshPending = false;

        var currentForm = KakarotAuraFormResolver.ResolveCurrent(_creature);
        _fromPalette = KakarotAuraConfig.GetPalette(currentForm);
        _targetPalette = _fromPalette;
        _targetForm = currentForm;

        if (!waitForSuccessfulForm && target != currentForm)
        {
            BeginPaletteTransition(target);
        }

        Visible = true;
        UpdatePalette();
        UpdateVisibility();
    }

    private void StartPersistent(KakarotAuraForm form)
    {
        _fromPalette = KakarotAuraConfig.GetPalette(form);
        _targetPalette = _fromPalette;
        _targetForm = form;
        _colorElapsed = KakarotAuraConfig.ColorTransitionDuration;
        _lightningElapsed = KakarotAuraConfig.PersistentLightningMaxInterval;
        _lightningNextFlashDelay = 0f;
        _burstActive = false;
        _retiring = false;
        _currentIntensity = KakarotAuraConfig.PersistentIntensity;
        Visible = true;
        UpdatePalette();
        UpdateVisibility();
    }

    private void HandleFormRefresh(KakarotAuraForm form)
    {
        BeginPaletteTransition(form);
        if (_burstActive)
        {
            return;
        }

        if (KakarotAuraConfig.IsPersistent(form))
        {
            _retiring = false;
            _retireElapsed = 0f;
            return;
        }

        BeginRetire();
    }

    private void CompleteBurst()
    {
        _burstActive = false;
        _elapsed = 0f;

        var currentForm = KakarotAuraFormResolver.ResolveCurrent(_creature);
        BeginPaletteTransition(currentForm);
        if (KakarotAuraConfig.IsPersistent(currentForm))
        {
            _retiring = false;
            _retireElapsed = 0f;
            _currentIntensity = KakarotAuraConfig.PersistentIntensity;
            return;
        }

        // 爆发末段已经淡出，非驻留形态可直接回收。
        QueueFree();
    }

    private void BeginRetire()
    {
        if (_retiring)
        {
            return;
        }

        _retiring = true;
        _retireElapsed = 0f;
        _retireStartIntensity = _currentIntensity;
    }

    private void BeginPaletteTransition(KakarotAuraForm form)
    {
        if (form == _targetForm)
        {
            return;
        }

        _fromPalette = InterpolatePalette();
        _targetPalette = KakarotAuraConfig.GetPalette(form);
        _targetForm = form;
        _colorElapsed = 0f;
        if (_targetPalette.HasLightning)
        {
            _lightningElapsed = KakarotAuraConfig.PersistentLightningMaxInterval;
            _lightningNextFlashDelay = 0f;
        }
    }

    private void SyncToModel()
    {
        float scaleRatio = Math.Abs(_staticModel.Scale.X) / KakarotAuraConfig.ReferenceModelScale;
        if (!float.IsFinite(scaleRatio) || scaleRatio <= 0.01f)
        {
            scaleRatio = 1f;
        }

        Position = _staticModel.Position + KakarotAuraConfig.Offset * scaleRatio;
        Scale = Vector2.One * scaleRatio;
        Rotation = _staticModel.Rotation;
        ZIndex = _staticModel.ZIndex;
        Visible = _staticModel.Visible && !_creature.IsDead;
    }

    private void UpdatePalette()
    {
        var palette = InterpolatePalette();
        SetPalette(_backMaterial, palette);
        SetPalette(_frontMaterial, palette);
    }

    private void UpdateVisibility()
    {
        float intensity;
        float activity;
        if (_burstActive)
        {
            float fadeIn = SmoothStep(Math.Clamp(
                _elapsed / KakarotAuraConfig.FadeInDuration,
                0f,
                1f));
            float remaining = _duration - _elapsed;
            float fadeOut = SmoothStep(Math.Clamp(
                remaining / KakarotAuraConfig.FadeOutDuration,
                0f,
                1f));
            activity = Math.Min(fadeIn, fadeOut);

            float endIntensity = KakarotAuraConfig.IsPersistent(
                KakarotAuraFormResolver.ResolveCurrent(_creature))
                ? KakarotAuraConfig.PersistentIntensity
                : 0f;
            intensity = _elapsed < KakarotAuraConfig.FadeInDuration
                ? Mathf.Lerp(_burstStartIntensity, 1f, fadeIn)
                : remaining < KakarotAuraConfig.FadeOutDuration
                    ? Mathf.Lerp(endIntensity, 1f, fadeOut)
                    : 1f;
        }
        else if (_retiring)
        {
            float retireProgress = SmoothStep(Math.Clamp(
                _retireElapsed / KakarotAuraConfig.FadeOutDuration,
                0f,
                1f));
            intensity = Mathf.Lerp(_retireStartIntensity, 0f, retireProgress);
            activity = 0f;
        }
        else
        {
            intensity = KakarotAuraConfig.PersistentIntensity;
            activity = 0f;
        }

        _currentIntensity = intensity;
        _visualActivity = activity;

        _backMaterial.SetShaderParameter("intensity", intensity);
        _frontMaterial.SetShaderParameter("intensity", intensity);
        float flowScale = Mathf.Lerp(
            KakarotAuraConfig.PersistentFlowSpeedScale,
            1f,
            activity);
        _backMaterial.SetShaderParameter("flow_speed", KakarotAuraConfig.BackFlowSpeed * flowScale);
        _frontMaterial.SetShaderParameter("flow_speed", KakarotAuraConfig.FrontFlowSpeed * flowScale);
    }

    private void CreateLightningArcs()
    {
        var additiveMaterial = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
        };
        _lightningBolts = new LightningBolt[KakarotAuraConfig.LightningBoltCount];
        for (int index = 0; index < _lightningBolts.Length; index++)
        {
            var mainGlow = CreateLightningLine(
                $"Ss2Lightning{index + 1}MainGlow",
                KakarotAuraConfig.LightningGlowWidth,
                additiveMaterial);
            var mainCore = CreateLightningLine(
                $"Ss2Lightning{index + 1}MainCore",
                KakarotAuraConfig.LightningCoreWidth,
                additiveMaterial);
            var branchGlow = CreateLightningLine(
                $"Ss2Lightning{index + 1}BranchGlow",
                KakarotAuraConfig.LightningGlowWidth * KakarotAuraConfig.LightningBranchWidthScale,
                additiveMaterial);
            var branchCore = CreateLightningLine(
                $"Ss2Lightning{index + 1}BranchCore",
                KakarotAuraConfig.LightningCoreWidth * KakarotAuraConfig.LightningBranchWidthScale,
                additiveMaterial);

            AddChild(mainGlow);
            AddChild(mainCore);
            AddChild(branchGlow);
            AddChild(branchCore);
            _lightningBolts[index] = new LightningBolt(mainGlow, mainCore, branchGlow, branchCore);
        }
    }

    private static Line2D CreateLightningLine(
        string name,
        float width,
        CanvasItemMaterial material) =>
        new()
        {
            Name = name,
            ZIndex = KakarotAuraConfig.LightningZIndex,
            Width = width,
            JointMode = Line2D.LineJointMode.Round,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            Antialiased = true,
            Visible = false,
            Material = material,
        };

    private void HideLightning()
    {
        foreach (var bolt in _lightningBolts)
        {
            bolt.SetVisible(false);
        }
    }

    private void UpdateLightning()
    {
        var palette = InterpolatePalette();
        float colorProgress = GetColorProgress();
        float lightningWeight = Mathf.Lerp(
            _fromPalette.HasLightning ? 1f : 0f,
            _targetPalette.HasLightning ? 1f : 0f,
            colorProgress);
        if (lightningWeight <= KakarotAuraConfig.LightningVisibilityThreshold ||
            _currentIntensity <= 0.01f)
        {
            HideLightning();
            return;
        }

        if (_lightningElapsed >= _lightningNextFlashDelay)
        {
            BeginLightningFlash();
        }

        float flashProgress = _lightningFlashDuration <= 0f
            ? 1f
            : Math.Clamp(_lightningElapsed / _lightningFlashDuration, 0f, 1f);
        if (flashProgress >= 1f)
        {
            HideLightning();
            return;
        }

        float pulse = 1f - SmoothStep(flashProgress);
        float auraVisibility = Math.Clamp(
            _currentIntensity / KakarotAuraConfig.PersistentIntensity,
            0f,
            1f);
        float strength = Mathf.Lerp(
            KakarotAuraConfig.PersistentLightningStrength,
            1f,
            _visualActivity);
        float alpha = lightningWeight * pulse * auraVisibility * strength;
        var coreColor = KakarotAuraConfig.LightningCoreColor.Lerp(
            palette.Lightning,
            KakarotAuraConfig.LightningCoreTintMix);

        for (int index = 0; index < _lightningBolts.Length; index++)
        {
            bool visible = index < _activeLightningBoltCount &&
                alpha > KakarotAuraConfig.LightningVisibilityThreshold;
            var bolt = _lightningBolts[index];
            bolt.SetVisible(visible);
            if (!visible)
            {
                continue;
            }

            bolt.MainGlow.DefaultColor = new Color(
                palette.Lightning,
                alpha * KakarotAuraConfig.LightningGlowOpacity);
            bolt.BranchGlow.DefaultColor = bolt.MainGlow.DefaultColor;
            bolt.MainCore.DefaultColor = new Color(
                coreColor,
                alpha * KakarotAuraConfig.LightningCoreOpacity);
            bolt.BranchCore.DefaultColor = bolt.MainCore.DefaultColor;
        }
    }

    private void BeginLightningFlash()
    {
        _lightningElapsed = 0f;
        _lightningStep++;
        bool burst = _burstActive;
        _lightningFlashDuration = Mathf.Lerp(
            KakarotAuraConfig.LightningFlashMinDuration,
            KakarotAuraConfig.LightningFlashMaxDuration,
            Hash01(_lightningStep * 3.17f));
        _lightningNextFlashDelay = Mathf.Lerp(
            burst
                ? KakarotAuraConfig.BurstLightningMinInterval
                : KakarotAuraConfig.PersistentLightningMinInterval,
            burst
                ? KakarotAuraConfig.BurstLightningMaxInterval
                : KakarotAuraConfig.PersistentLightningMaxInterval,
            Hash01(_lightningStep * 7.31f));
        _activeLightningBoltCount = burst
            ? 2 + (Hash01(_lightningStep * 5.23f) < KakarotAuraConfig.BurstThirdBoltChance ? 1 : 0)
            : 1 + (Hash01(_lightningStep * 5.23f) < KakarotAuraConfig.PersistentSecondBoltChance ? 1 : 0);

        for (int index = 0; index < _lightningBolts.Length; index++)
        {
            var bolt = _lightningBolts[index];
            if (index >= _activeLightningBoltCount)
            {
                bolt.SetVisible(false);
                continue;
            }

            BuildLightningPoints(bolt, index, _lightningStep);
        }
    }

    private static void BuildLightningPoints(LightningBolt bolt, int boltIndex, int step)
    {
        var mainPoints = new Vector2[KakarotAuraConfig.LightningMainPointCount];
        float seed = step * 19.7f + boltIndex * 31.1f;
        float side = Hash01(seed + 0.71f) < 0.5f ? -1f : 1f;
        float startY = KakarotAuraConfig.LightningStartY
            + Hash01(seed + 1.37f) * KakarotAuraConfig.LightningStartYVariance;
        float length = KakarotAuraConfig.LightningMinLength
            + Hash01(seed + 2.11f) * KakarotAuraConfig.LightningLengthVariance;
        float verticalDirection = Hash01(seed + 2.73f) < KakarotAuraConfig.LightningReverseDirectionChance
            ? -1f
            : 1f;
        float baseX = side * (KakarotAuraConfig.LightningBaseX
            + Hash01(seed + 3.19f) * KakarotAuraConfig.LightningBaseXVariance);
        float drift = (Hash01(seed + 4.07f) - 0.5f)
            * 2f
            * KakarotAuraConfig.LightningHorizontalDrift;

        for (int pointIndex = 0; pointIndex < mainPoints.Length; pointIndex++)
        {
            float progress = pointIndex / (float)(mainPoints.Length - 1);
            float endpointWeight = MathF.Sin(progress * MathF.PI);
            float zigzag = (Hash01(seed + pointIndex * 4.73f) - 0.5f)
                * 2f
                * KakarotAuraConfig.LightningZigzag
                * endpointWeight;
            mainPoints[pointIndex] = new Vector2(
                baseX + drift * progress + zigzag,
                startY + verticalDirection * length * progress);
        }

        int branchOriginIndex = 2 + (Hash01(seed + 8.41f) < KakarotAuraConfig.LightningLateBranchChance ? 1 : 0);
        branchOriginIndex = Math.Min(branchOriginIndex, mainPoints.Length - 2);
        var branchPoints = new Vector2[KakarotAuraConfig.LightningBranchPointCount];
        var branchStart = mainPoints[branchOriginIndex];
        float branchLength = KakarotAuraConfig.LightningBranchMinLength
            + Hash01(seed + 9.13f) * KakarotAuraConfig.LightningBranchLengthVariance;
        var branchEnd = branchStart + new Vector2(
            side * branchLength,
            (Hash01(seed + 10.37f) - 0.5f) * KakarotAuraConfig.LightningBranchVerticalSpread);

        for (int pointIndex = 0; pointIndex < branchPoints.Length; pointIndex++)
        {
            float progress = pointIndex / (float)(branchPoints.Length - 1);
            float bend = (Hash01(seed + 11.71f + pointIndex * 2.91f) - 0.5f)
                * KakarotAuraConfig.LightningZigzag
                * MathF.Sin(progress * MathF.PI);
            branchPoints[pointIndex] = branchStart.Lerp(branchEnd, progress) + new Vector2(0f, bend);
        }

        bolt.MainGlow.Points = mainPoints;
        bolt.MainCore.Points = mainPoints;
        bolt.BranchGlow.Points = branchPoints;
        bolt.BranchCore.Points = branchPoints;
    }

    private KakarotAuraPalette InterpolatePalette()
    {
        float progress = GetColorProgress();
        return new KakarotAuraPalette(
            _fromPalette.Primary.Lerp(_targetPalette.Primary, progress),
            _fromPalette.Secondary.Lerp(_targetPalette.Secondary, progress),
            _fromPalette.Lightning.Lerp(_targetPalette.Lightning, progress),
            _targetPalette.HasLightning);
    }

    private float GetColorProgress() => SmoothStep(Math.Clamp(
        _colorElapsed / KakarotAuraConfig.ColorTransitionDuration,
        0f,
        1f));

    private static Sprite2D CreateAuraLayer(
        string name,
        int zIndex,
        Texture2D texture,
        ShaderMaterial material) =>
        new()
        {
            Name = name,
            ZIndex = zIndex,
            Texture = texture,
            Scale = KakarotAuraConfig.SpriteScale,
            Material = material,
        };

    private static ShaderMaterial CreateMaterial(
        Shader shader,
        bool frontLayer,
        float opacity,
        float phaseOffset,
        float flowSpeed)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("opacity", opacity);
        material.SetShaderParameter("flow_speed", flowSpeed);
        material.SetShaderParameter("front_layer", frontLayer ? 1f : 0f);
        material.SetShaderParameter("phase_offset", phaseOffset);
        material.SetShaderParameter("intensity", 0f);
        return material;
    }

    private static void SetPalette(ShaderMaterial material, KakarotAuraPalette palette)
    {
        material.SetShaderParameter("primary_color", palette.Primary);
        material.SetShaderParameter("secondary_color", palette.Secondary);
    }

    private static bool IsUsable(Sprite2D staticModel, Creature creature) =>
        staticModel != null &&
        GodotObject.IsInstanceValid(staticModel) &&
        creature != null &&
        !creature.IsDead;

    private static float Hash01(float value) =>
        MathF.Abs(MathF.Sin(value * 12.9898f) * 43758.5453f) % 1f;

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);
}
