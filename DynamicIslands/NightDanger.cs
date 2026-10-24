using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AzureSky;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Night is dangerous" (WorldOptions.NightDanger): after dark the sea and the islands are harsher, by day
	/// calmer. Day and night are Raft's own sky's (AzureSkyController.IsDaytime, the same on every machine).
	///   - Monsters: on top of the world's monster difficulty, x1.3 health and damage at night, x0.85 by day
	///     (MonsterDifficulty.Scale, where every hit is scaled on the machine it happens on).
	///   - The shark: looks for the raft to bite more often at night (its search interval x0.6), less often by day (x1.25).
	///   - How many monsters live at each spot stays as built: saves, "defeat" actions and kill quests count the same.
	/// </summary>
	public static class NightDanger
	{
		public const float MonsterNight = 1.3f, MonsterDay = 0.85f, SharkNight = 0.6f, SharkDay = 1.25f;

		/// <summary>Tests: night (true) or day (false) whatever the sky says; null = the sky.</summary>
		internal static bool? TestNight;

		static AzureSkyController sky;

		public static bool On { get { return WorldOptions.On(WorldOptions.NightDanger); } }

		public static bool IsNight
		{
			get
			{
				if (TestNight.HasValue) return TestNight.Value;
				if (sky == null) sky = RaftSkySea.Sky ?? UnityEngine.Object.FindObjectOfType<AzureSkyController>();
				return sky != null && !sky.IsDaytime;
			}
		}

		/// <summary>Monsters' health and damage on top of the world's difficulty (1 with the option off).</summary>
		public static float MonsterFactor { get { return On ? (IsNight ? MonsterNight : MonsterDay) : 1f; } }

		/// <summary>The shark's time between looks for the raft (1 with the option off).</summary>
		public static float SharkFactor { get { return On ? (IsNight ? SharkNight : SharkDay) : 1f; } }

		internal static void Reset() { TestNight = null; sky = null; }
	}

	/// <summary>How long the shark waits between looks for a block to bite: shorter at night with Night is dangerous on.</summary>
	[HarmonyPatch(typeof(AI_StateMachine_Shark), "SearchBlockInterval", MethodType.Getter)]
	static class NightDangerShark
	{
		static void Postfix(ref float __result)
		{
			try { __result *= NightDanger.SharkFactor * StormDays.SharkFactor; }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [night] " + e.Message); }
		}
	}
}
