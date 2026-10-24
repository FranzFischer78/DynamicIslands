using HarmonyLib;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's own objects that throw when they are used outside a world. The editor loads Raft's island scenes to offer
	/// their objects; a music zone in them (AreaZone_MusicActivation) looks up its quest when it is switched on - there is
	/// no quest tracker outside a world, so it threw a NullReferenceException, red in the console of every player who
	/// opened the editor with such objects. Raft's code always runs; only its error outside a world is dropped (it was
	/// skipped outside a world, and an island switched on while a world was still loading lost its music - CA25).
	/// </summary>
	[HarmonyPatch(typeof(AreaZone_MusicActivation), "OnEnable")]
	static class MusicZoneOutsideWorld
	{
		static System.Exception Finalizer(System.Exception __exception) { return LoadSceneManager.IsGameSceneLoaded ? __exception : null; }
	}

	/// <summary>
	/// Raft's boats, barrels and containers cut the sea out of their insides (UltimateWater's WaterVolumeSubtract). The
	/// editor has no sea of Raft's: each such object logged "Water reference is null" in red when placed (145 lines in
	/// one test run). Without water there is nothing to cut out.
	/// </summary>
	[HarmonyPatch(typeof(UltimateWater.WaterVolumeSubtract), "Register")]
	static class WaterCutoutWithoutWater
	{
		static bool Prefix(UltimateWater.Water water) { return water != null; }
	}

	/// <summary>
	/// A pickup that adds several items at once (a crate's loot) to an empty selected hotbar slot selects an item that
	/// charges up (a fishing rod, say) and deselects it again in the same frame. Its charge meter has not run Start yet,
	/// has no settings, and ChargeMeter.Reset threw a NullReferenceException in the middle of the pickup. The meter is
	/// given what its Start would have given it first.
	/// </summary>
	[HarmonyPatch(typeof(ChargeMeter), "Reset")]
	static class ChargeMeterResetBeforeStart
	{
		/// <summary>How often a meter was reset before its Start (for the test CIChargeMeterEarly).</summary>
		internal static int Caught;

		static bool Prefix(ChargeMeter __instance)
		{
			Traverse t = Traverse.Create(__instance);
			if (t.Field("settings").GetValue() == null) { Caught++; t.Field("settings").SetValue(ComponentManager<Settings>.Value); }
			if (t.Field("canvas").GetValue() == null) t.Field("canvas").SetValue(ComponentManager<CanvasHelper>.Value);
			return t.Field("settings").GetValue() != null;
		}
	}
}
