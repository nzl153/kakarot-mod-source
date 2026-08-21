using System;
using HarmonyLib;
using Godot;
using KakarotMod.KakarotCode;
using KakarotMod.KakarotCode.Encounters;
using KakarotMod.KakarotCode.Events;
using KakarotMod.KakarotCode.Monsters.Frieza;
using KakarotMod.KakarotCode.Patches;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.Kakarot;

[ModInitializer(nameof(Initialize))]
public class MainFile
{
	public const string ModId = "Kakarot";

	public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

	public static void Initialize()
	{
		Harmony harmony = new(ModId);
		harmony.PatchAll();
		KakarotResourceDiagnostics.LogMissingResources();
		KakarotWildKeyword.LogIfSlotCollides();

		try
		{
			KakarotStaticModelVisibility.TryApplyOverlayPatches(harmony);
			KakarotWildKeywordVisibilityPatches.TryApply(harmony);
			KakarotWildKeywordFacePatches.TryApply(harmony);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Kakarot] Optional patch probe failed: {ex.Message}");
		}

		// ModelDb 在模组 Initialize 返回之后才完成卡牌注册；此处立刻查询 ModelDb 会 KeyNotFound。
		if (Godot.Engine.GetMainLoop() is Godot.SceneTree sceneTree)
		{
			var timer = sceneTree.CreateTimer(1.0);
			timer.Timeout += LogFriezaRegistration;
		}
	}

	private static void LogFriezaRegistration()
	{
		try
		{
			ModelId eventId = ModelDb.GetId<FriezaChallenge>();
			ModelId encounterId = ModelDb.GetId<FriezaBossEncounter>();
			ModelId bossId = ModelDb.GetId<FriezaBoss>();
			GD.Print(
				$"[Kakarot] Frieza registration: " +
				$"event={eventId.Entry}:{ModelDb.Contains(typeof(FriezaChallenge))}, " +
				$"encounter={encounterId.Entry}:{ModelDb.Contains(typeof(FriezaBossEncounter))}, " +
				$"boss={bossId.Entry}:{ModelDb.Contains(typeof(FriezaBoss))}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Kakarot] Frieza registration check failed: {ex}");
		}
	}
}
