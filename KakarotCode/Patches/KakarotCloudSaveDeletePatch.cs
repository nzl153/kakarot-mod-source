using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace KakarotMod.KakarotCode.Patches;

// Ignore only stale history-backup deletion failures caused by cloud/local save drift.
[HarmonyPatch(typeof(GodotFileIo), nameof(GodotFileIo.DeleteFile))]
public static class KakarotCloudSaveDeletePatch
{
    [HarmonyFinalizer]
    public static Exception DeleteFileFinalizer(Exception __exception, string path)
    {
        if (__exception == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return __exception;
        }

        var normalized = path.Replace('\\', '/');
        var isHistoryBackup = normalized.Contains("/saves/history/", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(".backup", StringComparison.OrdinalIgnoreCase);
        if (!isHistoryBackup)
        {
            return __exception;
        }

        // Ignore stale backup delete failures so startup can proceed.
        return null;
    }
}
