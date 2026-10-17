using System;
using Godot;

namespace KakarotMod.KakarotCode.Characters;

internal sealed partial class KakarotAuraVfx : Node2D
{
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

    private static float Hash01(float value) =>
        MathF.Abs(MathF.Sin(value * 12.9898f) * 43758.5453f) % 1f;
}
