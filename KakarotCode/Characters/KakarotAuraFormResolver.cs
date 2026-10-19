using System;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using KakarotMod.KakarotCode.Daima;

namespace KakarotMod.KakarotCode.Characters;

// 只读取现有 Power 和卡牌 ID，不修改任何战斗状态。
internal static class KakarotAuraFormResolver
{
    private const string SuperSaiyanTransformId = "KAKAROTMOD-KAKAROT_SUPER_SAIYAN_TRANSFORM";
    private const string KaiokenId = "KAKAROTMOD-KAKAROT_KAIOKEN";
    private const string SuperSaiyanBlueTransformId = "KAKAROTMOD-KAKAROT_SUPER_SAIYAN_BLUE_TRANSFORM";
    private const string SuperSaiyanGodTransformId = "KAKAROTMOD-KAKAROT_SUPER_SAIYAN_GOD_TRANSFORM";
    private const string UltraInstinctOmenId = "KAKAROTMOD-KAKAROT_ULTRA_INSTINCT_OMEN";
    private const string PeakUltraInstinctId = "KAKAROTMOD-KAKAROT_PEAK_ULTRA_INSTINCT";
    private const string ReturnToOriginId = "KAKAROTMOD-KAKAROT_RETURN_TO_ORIGIN";
    private const string DaimaTransformId = "KAKAROTMOD-KAKAROT_DAIMA_TRANSFORM";
    internal const string DaimaSs4AwakeningId = "KAKAROTMOD-KAKAROT_DAIMA_SS4_AWAKENING";

    internal static KakarotAuraForm ResolveCurrent(Creature creature)
    {
        if (creature == null)
        {
            return KakarotAuraForm.Base;
        }

        // 优先级必须与 KakarotFormVisuals.ResolveModelPath 保持一致。
        if (KakarotDaima.IsActive(creature))
        {
            return ResolveDaima(creature);
        }

        if (creature.HasPower<KakarotPerfectUltraInstinctPower>())
        {
            return KakarotAuraForm.PerfectUltraInstinct;
        }

        if (creature.HasPower<KakarotUltraInstinctOmenPower>())
        {
            return KakarotAuraForm.UltraInstinctOmen;
        }

        if (creature.HasPower<KakarotSuperSaiyan4Power>())
        {
            return KakarotAuraForm.SuperSaiyan4;
        }

        if (creature.HasPower<KakarotSuperSaiyanBluePower>())
        {
            return creature.HasPower<KaiokenPower>()
                ? KakarotAuraForm.SuperSaiyanBlueKaioken
                : KakarotAuraForm.SuperSaiyanBlue;
        }

        if (creature.HasPower<KakarotSuperSaiyanGodPower>())
        {
            return KakarotAuraForm.SuperSaiyanGod;
        }

        var superSaiyan = creature.GetPower<SuperSaiyanFormPower>();
        if (superSaiyan != null)
        {
            return Math.Clamp((int)superSaiyan.Amount, 1, 3) switch
            {
                3 => KakarotAuraForm.SuperSaiyan3,
                2 => KakarotAuraForm.SuperSaiyan2,
                _ => KakarotAuraForm.SuperSaiyan1,
            };
        }

        return creature.HasPower<KaiokenPower>()
            ? KakarotAuraForm.Kaioken
            : KakarotAuraForm.Base;
    }

    internal static bool TryResolvePlayback(
        string cardEntryId,
        Creature creature,
        out KakarotAuraPlayback playback,
        out KakarotAuraForm target,
        out bool waitForSuccessfulForm)
    {
        playback = KakarotAuraPlayback.Charge;
        target = ResolveCurrent(creature);
        waitForSuccessfulForm = false;

        if (string.IsNullOrWhiteSpace(cardEntryId))
        {
            return false;
        }

        if (EqualsId(cardEntryId, SuperSaiyanTransformId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = ResolveNextSuperSaiyan(creature);
            return true;
        }

        if (EqualsId(cardEntryId, DaimaTransformId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = ResolveNextDaima(creature);
            return true;
        }

        if (EqualsId(cardEntryId, KaiokenId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = creature.HasPower<KakarotSuperSaiyanBluePower>()
                ? KakarotAuraForm.SuperSaiyanBlueKaioken
                : KakarotAuraForm.Kaioken;
            return true;
        }

        if (EqualsId(cardEntryId, SuperSaiyanGodTransformId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = KakarotAuraForm.SuperSaiyanGod;
            return true;
        }

        if (EqualsId(cardEntryId, SuperSaiyanBlueTransformId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = KakarotAuraForm.SuperSaiyanBlue;
            return true;
        }

        if (EqualsId(cardEntryId, ReturnToOriginId) || EqualsId(cardEntryId, DaimaSs4AwakeningId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = KakarotAuraForm.SuperSaiyan4;
            return true;
        }

        if (EqualsId(cardEntryId, UltraInstinctOmenId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = KakarotAuraForm.UltraInstinctOmen;
            return true;
        }

        if (EqualsId(cardEntryId, PeakUltraInstinctId))
        {
            playback = KakarotAuraPlayback.Transform;
            target = KakarotAuraForm.PerfectUltraInstinct;
            waitForSuccessfulForm = true;
            return true;
        }

        return IsChargeCard(cardEntryId);
    }

    private static bool IsChargeCard(string cardEntryId) =>
        EqualsId(cardEntryId, "KAKAROTMOD-KAKAROT_CHARGE_UP") ||
        EqualsId(cardEntryId, "KAKAROTMOD-KAKAROT_GOD_KI") ||
        EqualsId(cardEntryId, "KAKAROTMOD-KAKAROT_GREAT_APE_FORM") ||
        EqualsId(cardEntryId, "KAKAROTMOD-KAKAROT_SHENRON_WISH_TRANSFORM");

    // 小悟空不带气焰；小超四和成年超四共用超四的红色。
    private static KakarotAuraForm ResolveDaima(Creature creature)
    {
        if (creature.HasPower<KakarotDaimaSs1Power>())
        {
            return KakarotAuraForm.SuperSaiyan1;
        }

        if (creature.HasPower<KakarotDaimaSs3Power>())
        {
            return KakarotAuraForm.SuperSaiyan3;
        }

        return creature.HasPower<KakarotDaimaKidPower>() ? KakarotAuraForm.Base : KakarotAuraForm.SuperSaiyan4;
    }

    // 解析发生在变身卡结算之前，所以要往前看一阶。
    private static KakarotAuraForm ResolveNextDaima(Creature creature)
    {
        if (creature.HasPower<KakarotDaimaKidPower>())
        {
            return KakarotAuraForm.SuperSaiyan1;
        }

        return creature.HasPower<KakarotDaimaSs1Power>() ? KakarotAuraForm.SuperSaiyan3 : KakarotAuraForm.SuperSaiyan4;
    }

    private static KakarotAuraForm ResolveNextSuperSaiyan(Creature creature)
    {
        var tier = creature?.GetPower<SuperSaiyanFormPower>()?.Amount ?? 0;
        return Math.Clamp((int)tier + 1, 1, 3) switch
        {
            3 => KakarotAuraForm.SuperSaiyan3,
            2 => KakarotAuraForm.SuperSaiyan2,
            _ => KakarotAuraForm.SuperSaiyan1,
        };
    }

    private static bool EqualsId(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
