using Godot;

namespace KakarotMod.KakarotCode.Characters;

internal enum KakarotAuraForm
{
    Base,
    Kaioken,
    SuperSaiyan1,
    SuperSaiyan2,
    SuperSaiyan3,
    SuperSaiyanGod,
    SuperSaiyanBlue,
    SuperSaiyan4,
    UltraInstinctOmen,
    PerfectUltraInstinct,
}

internal enum KakarotAuraPlayback
{
    Charge,
    Transform,
}

internal readonly record struct KakarotAuraPalette(
    Color Primary,
    Color Secondary,
    Color Lightning,
    bool HasLightning);

internal static class KakarotAuraConfig
{
    internal const string ShaderPath = "res://Kakarot/Shaders/kakarot_charge_aura.gdshader";
    internal const string TexturePath = "res://Kakarot/Images/Vfx/kakarot_aura_neutral_mask.png";
    internal const string NodeName = "KakarotChargeAuraVfx";

    internal const int BackZIndex = 0;
    internal const int FrontZIndex = 1;
    internal const int LightningZIndex = 2;

    internal const float ReferenceModelScale = 0.176f;
    internal const float BackOpacity = 0.58f;
    internal const float FrontOpacity = 0.38f;
    internal const float BackFlowSpeed = 1.0f;
    internal const float FrontFlowSpeed = 1.0f;
    internal const float FrontPhaseOffset = 5.37f;
    internal const float PersistentIntensity = 0.24f;
    internal const float PersistentFlowSpeedScale = 0.65f;

    internal const float ChargeDuration = 1.16f;
    internal const float TransformDuration = 1.48f;
    internal const float SuccessfulFormWaitDuration = 2.40f;
    internal const float FadeInDuration = 0.14f;
    internal const float FadeOutDuration = 0.30f;
    internal const float ColorTransitionDuration = 0.46f;

    internal const float LightningCoreWidth = 2.4f;
    internal const float LightningGlowWidth = 10f;
    internal const float LightningBranchWidthScale = 0.72f;
    internal const float LightningCoreOpacity = 0.98f;
    internal const float LightningGlowOpacity = 0.46f;
    internal const float LightningCoreTintMix = 0.24f;
    internal const float LightningVisibilityThreshold = 0.02f;
    internal const float PersistentLightningStrength = 0.52f;
    internal const float LightningFlashMinDuration = 0.085f;
    internal const float LightningFlashMaxDuration = 0.15f;
    internal const float PersistentLightningMinInterval = 0.38f;
    internal const float PersistentLightningMaxInterval = 0.82f;
    internal const float BurstLightningMinInterval = 0.12f;
    internal const float BurstLightningMaxInterval = 0.28f;
    internal const int LightningBoltCount = 3;
    internal const int LightningMainPointCount = 5;
    internal const int LightningBranchPointCount = 3;
    internal const float LightningStartY = -175f;
    internal const float LightningStartYVariance = 280f;
    internal const float LightningMinLength = 46f;
    internal const float LightningLengthVariance = 48f;
    internal const float LightningBaseX = 74f;
    internal const float LightningBaseXVariance = 55f;
    internal const float LightningHorizontalDrift = 32f;
    internal const float LightningZigzag = 28f;
    internal const float LightningBranchMinLength = 28f;
    internal const float LightningBranchLengthVariance = 24f;
    internal const float LightningBranchVerticalSpread = 34f;
    internal const float PersistentSecondBoltChance = 0.22f;
    internal const float BurstThirdBoltChance = 0.44f;
    internal const float LightningReverseDirectionChance = 0.24f;
    internal const float LightningLateBranchChance = 0.38f;

    internal static readonly Vector2 Offset = new(6f, 4f);
    internal static readonly Vector2 SpriteScale = new(370f / 338f, 500f / 582f);
    internal static readonly Color LightningCoreColor = Colors.White;

    internal static bool IsPersistent(KakarotAuraForm form) => form is
        KakarotAuraForm.Kaioken or
        KakarotAuraForm.SuperSaiyan1 or
        KakarotAuraForm.SuperSaiyan2 or
        KakarotAuraForm.SuperSaiyan3 or
        KakarotAuraForm.SuperSaiyanBlue or
        KakarotAuraForm.SuperSaiyan4 or
        KakarotAuraForm.UltraInstinctOmen or
        KakarotAuraForm.PerfectUltraInstinct;

    internal static KakarotAuraPalette GetPalette(KakarotAuraForm form) => form switch
    {
        KakarotAuraForm.Kaioken => new(new Color("d71936"), new Color("720a1f"), Colors.White, false),
        KakarotAuraForm.SuperSaiyan1 => GoldenAura(false),
        KakarotAuraForm.SuperSaiyan2 => GoldenAura(true),
        KakarotAuraForm.SuperSaiyan3 => GoldenAura(false),
        KakarotAuraForm.SuperSaiyanGod => new(new Color("ff8a92"), new Color("b92b46"), Colors.White, false),
        KakarotAuraForm.SuperSaiyanBlue => new(new Color("5eeaff"), new Color("087fc4"), Colors.White, false),
        KakarotAuraForm.SuperSaiyan4 => GoldenAura(false),
        KakarotAuraForm.UltraInstinctOmen => UltraInstinctAura(),
        KakarotAuraForm.PerfectUltraInstinct => UltraInstinctAura(),
        _ => new(new Color("ffffff"), new Color("c7d1dc"), Colors.White, false),
    };

    private static KakarotAuraPalette GoldenAura(bool hasLightning) =>
        new(new Color("fff176"), new Color("ffb21c"), new Color("5edcff"), hasLightning);

    private static KakarotAuraPalette UltraInstinctAura() =>
        new(new Color("f8fcff"), new Color("8fd8ff"), Colors.White, false);
}
