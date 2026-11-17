using System.Collections.Generic;
using Godot;

namespace KakarotMod.Kakarot;

internal static class KakarotResourceDiagnostics
{
	private static readonly string[] RequiredPaths =
	{
		"res://Kakarot/Images/Charui/kakarot_combat_model.png",
		"res://Kakarot/Images/Charui/kakarot_combat_model_dead.png",
		"res://Kakarot/Images/Charui/kakarot_rest_site_model.png",
		"res://Kakarot/Images/Charui/kakarot_merchant_model.png",
		"res://images/ui/hands/multiplayer_hand_kakarot_point.png",
		"res://images/ui/hands/multiplayer_hand_kakarot_rock.png",
		"res://images/ui/hands/multiplayer_hand_kakarot_paper.png",
		"res://images/ui/hands/multiplayer_hand_kakarot_scissors.png",
		"res://Kakarot/Images/UI/Counters/kakarot_orb_layer_1.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_orb_layer_2.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_orb_layer_3.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_orb_layer_4.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_orb_layer_5.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_star_layer_1.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_star_layer_2.ctex",
		"res://Kakarot/Images/UI/Counters/kakarot_star_layer_3.ctex",
		"res://images/ui/combat/combat_reticle.svg",
	};

	public static void LogMissingResources()
	{
		var missing = new List<string>();
		foreach (var path in RequiredPaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				missing.Add(path);
				continue;
			}

			// Exists() can still pass when import remap is broken; verify the resource can really load.
			if (ResourceLoader.Load(path) == null)
			{
				missing.Add($"{path} (exists but failed to load)");
			}
		}

		if (missing.Count == 0)
		{
			GD.Print("[Kakarot] Resource check passed (all critical files found).");
			return;
		}

		GD.PrintErr($"[Kakarot] Resource check: {missing.Count} critical file(s) missing.");
		foreach (var path in missing)
		{
			GD.PrintErr($"[Kakarot] Missing: {path}");
		}
	}
}
