using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The mod's Harmony patches, applied one class at a time. Harmony's PatchAll stops at the first patch that fails - after
	/// a Raft update changes a method, that would switch off every patch after it and the rest of the mod's start, without a
	/// word. Here each one is tried on its own; the ones that fail are logged, and the main menu tells the player which
	/// parts of the mod don't work with this Raft version (a box with Report a problem).
	/// </summary>
	public static class PatchHealth
	{
		static readonly List<string> failed = new List<string>();
		static bool told;

		/// <summary>The patch classes that couldn't be applied (tests).</summary>
		public static IList<string> FailedPatches { get { return failed.AsReadOnly(); } }
		public static int Applied { get; private set; }

		public static void PatchAll(Harmony harmony)
		{
			foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(PatchHealth).Assembly))
			{
				if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
				try { harmony.CreateClassProcessor(t).Patch(); Applied++; }
				catch (Exception e) { Failed(t.Name, e); }
			}
			Debug.Log("[CUSTOM ISLANDS] " + Applied + " patches applied" + (failed.Count > 0 ? ", " + failed.Count + " failed: " + string.Join(", ", failed.ToArray()) : ""));
		}

		public static void Failed(string name, Exception e)
		{
			failed.Add(name);
			Exception inner = e.InnerException ?? e;
			Debug.LogError("[CUSTOM ISLANDS] The patch " + name + " doesn't fit this Raft version (" + Feature(name) + " won't work): " + inner.GetType().Name + ": " + inner.Message);
		}

		/// <summary>The part of the mod a patch class belongs to, in players' words.</summary>
		public static string Feature(string patch)
		{
			var words = new[]
			{
				new[] { "StormSeas", "storm seas" }, new[] { "GlowSea", "the bioluminescent sea" }, new[] { "Upgrade", "extra upgrades" }, new[] { "StoryChain", "the story chain (your islands on the Receiver)" }, new[] { "StoryOrder", "story islands in a new order" },
				new[] { "Blueprint", "scrambled blueprints" }, new[] { "PrivateStorage", "private storages" }, new[] { "Ghost", "ghost rafts" },
				new[] { "Level", "the level up system" }, new[] { "Stat", "the level up system" }, new[] { "Monster", "monster difficulty" },
				new[] { "BuildCost", "build cost" }, new[] { "Randomizer", "the world randomizer" }, new[] { "WorldSettings", "the World settings window" },
				new[] { "NewWorld", "the New Game box" }, new[] { "NewGame", "the New Game box" }, new[] { "Save", "saving custom islands with the world" },
				new[] { "ChunkPoint", "keeping Raft's islands clear of custom ones" }, new[] { "Creature", "creatures" }, new[] { "Note", "notes" },
				new[] { "Receiver", "the Receiver" }, new[] { "Notice", "the main menu box" }, new[] { "Library", "the island library" },
				new[] { "IslandGround", "standing on custom islands (a moving raft could carry players off them)" },
				new[] { "DeadCarrier", "warthogs letting go of a player when killed while carrying them" },
				new[] { "FogBanks", "fog banks" }, new[] { "AnimalLooks", "rare animal looks" }, new[] { "Kraken", "the kraken arm" },
			};
			foreach (string[] w in words) if (patch.IndexOf(w[0], StringComparison.OrdinalIgnoreCase) >= 0) return w[1];
			return "a part of the mod (" + patch + ")";
		}

		/// <summary>The main menu appeared: once per Raft start, a box when patches failed.</summary>
		public static void ShowIfFailed()
		{
			if (failed.Count == 0 || told) return;
			told = true;
			string parts = string.Join("\n", failed.Select(Feature).Distinct().Select(f => "•  " + f).ToArray());
			InfoWindow.Open("Some parts of Custom Islands are off",
				"This version of Raft changed something the mod relies on, so these parts of Custom Islands won't work until the mod is updated:\n\n" +
				parts + "\n\nEverything else works. Please report it (Report a problem) - say which Raft version you have. " +
				"Your islands and worlds are not changed.",
				new InfoWindow.Choice("Report a problem", InfoWindow.OpenReport, "How to report it, on GitHub or Discord", true),
				new InfoWindow.Choice("Close", InfoWindow.Close, "Close this box (Esc)"));
		}
	}
}
