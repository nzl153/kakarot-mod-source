using System;
using HarmonyLib;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// 卡卡罗特界面自定义音效的两个接线（纯表现层）：
/// ① 入场过场擦除音：游戏在 embark 时 SfxCmd.Play(character.CharacterTransitionSfx)，我们的
///    "event:/sfx/ui/wipe_kakarot" 事件不存在（mod 加不了 FMOD event，只会静默）；命中该字符串时
///    补播 sfx/ui/transition.wav，时机与原生过场擦除完全一致，并拦下原生调用避免响铁甲战士的擦除音。
/// ② 选人语音：NCharacterSelectButton.Select 在（重新）选中时触发，命中卡卡罗特时播 voice/select_voice.wav
///    （PlayVoice 独占，来回选人不叠音）。
/// 均不改玩法/RNG/联机，只补声音。
/// </summary>
[HarmonyPatch]
public static class KakarotAudioPatches
{
    [HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
    [HarmonyPrefix]
    public static bool SfxPlayPrefix(string sfx)
    {
        try
        {
            if (!string.IsNullOrEmpty(sfx)
                && sfx.Contains("wipe", StringComparison.OrdinalIgnoreCase)
                && sfx.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
            {
                KakarotAudio.PlaySfx("sfx/ui/transition.wav");
                return false;   // 拦下原生，避免响原版擦除音。
            }
        }
        catch
        {
            // 纯表现：异常不拦原生。
        }

        return true;
    }

    [HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))]
    [HarmonyPostfix]
    public static void SelectPostfix(NCharacterSelectButton __instance)
    {
        try
        {
            var entry = __instance?.Character?.Id.Entry;
            if (!string.IsNullOrWhiteSpace(entry)
                && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
            {
                KakarotAudio.PlayVoice("voice/select_voice.wav");
            }
        }
        catch
        {
            // 纯表现。
        }
    }
}
