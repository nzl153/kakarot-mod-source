using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// Work around cloud/local save drift where deleting stale history backups can throw
/// and block startup. We only swallow targeted history backup delete failures.
/// </summary>
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
