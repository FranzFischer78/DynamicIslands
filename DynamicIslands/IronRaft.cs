using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Iron raft" (WorldOptions.IronRaft): the raft's blocks take half damage.
	///   - Raft damages a block only through Block.Damage, and only the shark calls it (biting the raft, or a bait): the
	///     hammer and the axe take blocks down another way (RemovePlaceables.PickupBlock) and are unaffected, and so is a
	///     block Raft removes on purpose.
	///   - Half of each bite, the odd half carried per block (a bite of 5 then 5 takes 2 then 3), the same on every machine.
	///   - Shark bait is not part of the raft: it's eaten as fast as always.
	/// </summary>
	public static class IronRaft
	{
		public const float Factor = 0.5f;
		static readonly Dictionary<Block, float> carry = new Dictionary<Block, float>();

		internal static void Reset() { carry.Clear(); }

		/// <summary>What a bite of this much does to this block (the whole bite when the option is off or for bait).</summary>
		public static int Scaled(Block block, int damage)
		{
			if (damage <= 0 || block == null || !WorldOptions.On(WorldOptions.IronRaft) || IsBait(block)) return damage;
			float c;
			carry.TryGetValue(block, out c);
			float want = damage * Factor + c;
			int take = Mathf.FloorToInt(want + 0.0001f);
			carry[block] = want - take;
			if (carry.Count > 500) { var gone = new List<Block>(); foreach (Block b in carry.Keys) if (b == null) gone.Add(b); foreach (Block b in gone) carry.Remove(b); }
			return take;
		}

		static bool IsBait(Block block)
		{
			string n = block.buildableItem != null ? block.buildableItem.UniqueName : block.name;
			return n != null && n.IndexOf("Bait", StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}

	/// <summary>Raft damages a block (the shark's bite): half of it with Iron raft on.</summary>
	[HarmonyPatch(typeof(Block), "Damage")]
	static class IronRaftDamage
	{
		static void Prefix(Block __instance, ref int damage)
		{
			try { damage = IronRaft.Scaled(__instance, damage); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [ironraft] " + e.Message); }
		}
	}
}
