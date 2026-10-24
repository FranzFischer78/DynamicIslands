using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A block taken down gives back half of what it cost when it was placed, not of today's build cost (ROADMAP AU33:
	/// Raft refunds from the one cost list per item, so building at 0 % and raising the cost to +100 % gave everything back
	/// for free, and the other way round lost three quarters).
	///   - Every machine notes the build cost a block was placed at (BlockCreator.CreateBlock, not loads or a join's copy
	///     of the raft) by its ObjectIndex, the same everywhere - only when it differs from the world's "base" percent,
	///     so the list stays short. The base is the cost the world had when this version first opened it (blocks built
	///     before were built at that cost, as far as anyone knows), or when it was created.
	///   - The host keeps the list in the world file ("@builtat=base|percent:index,index;...") and sends it with the
	///     world rules (kind 15, Name) to each player who joins.
	///   - Taking a block down (RemovePlaceables.ReturnItemsFromBlock, on the remover's machine) puts the block's cost
	///     list at the placing percent for that one call and back right after.
	/// </summary>
	public static class BuildCostRefund
	{
		static readonly Dictionary<uint, int> placedAt = new Dictionary<uint, int>();
		static int basePercent;
		static bool baseRead;

		public static int Count { get { return placedAt.Count; } }
		public static int Base { get { return basePercent; } }

		/// <summary>The percent this block was placed at.</summary>
		public static int PercentOf(uint objectIndex) { int p; return placedAt.TryGetValue(objectIndex, out p) ? p : basePercent; }

		/// <summary>A world opens or is created (host), or a host's world arrives (client): nothing noted yet.</summary>
		internal static void Reset(int percent)
		{
			placedAt.Clear();
			basePercent = BuildCost.Clamp(percent);
			baseRead = false;
		}

		/// <summary>The world file's build cost was read: an older world's blocks were built at it.</summary>
		internal static void OnCostRead(int percent) { if (!baseRead) basePercent = BuildCost.Clamp(percent); }

		/// <summary>Every machine: a player placed a block now.</summary>
		internal static void OnPlaced(Block block, bool replicating)
		{
			if (replicating || block == null) return;
			if (BuildCost.Current == basePercent) placedAt.Remove(block.ObjectIndex);
			else placedAt[block.ObjectIndex] = BuildCost.Current;
		}

		#region The world file and the network

		internal static bool ReadLine(string key, string value)
		{
			if (!key.Equals("builtat", StringComparison.OrdinalIgnoreCase)) return false;
			Decode(value);
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (HasState) yield return "@builtat=" + Encode();
		}

		// (also when the cost is not the base: a world whose blocks were built at 0% and whose cost was then raised would
		// otherwise read back as an older world's - every block built at the cost - and refund more than was paid)
		internal static bool HasState { get { return basePercent > 0 || placedAt.Count > 0 || BuildCost.Current != basePercent; } }

		public static string Encode()
		{
			return basePercent.ToString(CultureInfo.InvariantCulture) + "|" + string.Join(";", placedAt.GroupBy(kv => kv.Value).OrderBy(g => g.Key)
				.Select(g => g.Key.ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", g.Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture)).ToArray())).ToArray());
		}

		internal static void Decode(string data)
		{
			placedAt.Clear();
			if (data == null) return;
			string[] parts = data.Split('|');
			int b;
			if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) { basePercent = BuildCost.Clamp(b); baseRead = true; }
			if (parts.Length < 2) return;
			foreach (string group in parts[1].Split(';'))
			{
				int colon = group.IndexOf(':'), p;
				if (colon < 1 || !int.TryParse(group.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out p)) continue;
				foreach (string s in group.Substring(colon + 1).Split(','))
				{
					uint idx;
					if (uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx)) placedAt[idx] = BuildCost.Clamp(p);
				}
			}
		}

		#endregion

		#region Taking a block down

		/// <summary>The amounts changed for one refund (put back after it).</summary>
		internal static List<KeyValuePair<CostMultiple, int>> UsePlacingCost(Block block)
		{
			if (block == null || block.buildableItem == null || block.buildableItem.settings_recipe == null || block.buildableItem.settings_recipe.NewCost == null) return null;
			int at = PercentOf(block.ObjectIndex);
			placedAt.Remove(block.ObjectIndex);
			if (at == BuildCost.Applied) return null;
			var changed = new List<KeyValuePair<CostMultiple, int>>();
			foreach (CostMultiple c in block.buildableItem.settings_recipe.NewCost)
			{
				if (c == null) continue;
				changed.Add(new KeyValuePair<CostMultiple, int>(c, c.amount));
				c.amount = BuildCost.Cost(BuildCost.OriginalOf(c), at);
			}
			Debug.Log("[CUSTOM ISLANDS] [build cost] " + block.buildableItem.UniqueName + " " + block.ObjectIndex + " placed at " + BuildCost.Describe(at) + ": gives back by that cost (now " + BuildCost.Describe(BuildCost.Applied) + ")");
			return changed;
		}

		internal static void PutBack(List<KeyValuePair<CostMultiple, int>> changed)
		{
			if (changed == null) return;
			foreach (KeyValuePair<CostMultiple, int> kv in changed) kv.Key.amount = kv.Value;
		}

		#endregion
	}

	/// <summary>Every machine: a block placed now notes the build cost it was placed at.</summary>
	[HarmonyPatch(typeof(BlockCreator), "CreateBlock")]
	static class BuildCostPlaced
	{
		static void Postfix(Block __result, bool replicating)
		{
			try { BuildCostRefund.OnPlaced(__result, replicating); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [build cost] Noting a block's cost: " + e.Message); }
		}
	}

	/// <summary>The remover's machine: a block taken down gives back by the cost it was placed at.</summary>
	[HarmonyPatch(typeof(RemovePlaceables), "ReturnItemsFromBlock")]
	static class BuildCostRefundPatch
	{
		static void Prefix(Block block, out List<KeyValuePair<CostMultiple, int>> __state)
		{
			__state = null;
			try { __state = BuildCostRefund.UsePlacingCost(block); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [build cost] Refund: " + e.Message); }
		}

		static Exception Finalizer(Exception __exception, List<KeyValuePair<CostMultiple, int>> __state)
		{
			BuildCostRefund.PutBack(__state);
			return __exception;
		}
	}
}
