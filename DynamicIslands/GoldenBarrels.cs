using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Silver &amp; golden barrels" (WorldOptions.Barrels): now and then a barrel drifting in the sea is silver
	/// (about 1 in 40) or golden (about 1 in 150), and gives a little more than Raft's own barrel loot - silver some nails, rope
	/// and scrap, gold more of them and sometimes a battery or bolts. A small lucky moment, not enough to unbalance the game.
	///   - Which barrel is which comes from the world's seed and the barrel's network index: the same on every machine with no
	///     message (Raft sends the index with every floating object it spawns), and again when Raft reuses a pooled barrel.
	///   - Its colour is set when Raft spawns it (ObjectSpawner.SpawnItem); the extra loot goes to whoever picks it up, by
	///     hand or with the hook (Pickup.AddItemToInventory), on top of Raft's own.
	/// </summary>
	public static class GoldenBarrels
	{
		public const int None = 0, Silver = 1, Gold = 2;
		public const float SilverChance = 1f / 40f, GoldChance = 1f / 150f;
		public static readonly Color SilverTint = new Color(1.3f, 1.35f, 1.45f), GoldTint = new Color(1.7f, 1.3f, 0.45f);

		static readonly string[] SilverItems = { "Nail", "Rope", "Scrap" };
		static readonly int[] SilverMin = { 3, 1, 1 }, SilverMax = { 6, 2, 2 };
		static readonly string[] GoldItems = { "Nail", "Rope", "Scrap" };
		static readonly int[] GoldMin = { 6, 2, 2 }, GoldMax = { 10, 4, 3 };
		/// <summary>Gold's extra, one barrel in three: one of these.</summary>
		static readonly string[] GoldRare = { "Battery", "Bolt" };

		// (the barrels coloured now: a pooled one is plain again when Raft reuses it)
		static readonly Dictionary<int, int> coloured = new Dictionary<int, int>();

		/// <summary>Tests: every barrel spawned from now on is this kind.</summary>
		internal static int? TestKind;

		public static bool On { get { return WorldOptions.On(WorldOptions.Barrels); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [barrels] " + msg); }

		internal static void Reset() { TestKind = null; }

		static int Hash(int seed, uint index, int salt)
		{
			unchecked
			{
				uint h = (uint)seed * 2654435761u ^ index * 2246822519u ^ (uint)salt * 3266489917u;
				h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
				return (int)h;
			}
		}

		static float Unit(int h) { return (uint)h / 4294967296f; }

		/// <summary>What a barrel with this network index is in this world (the same on every machine).</summary>
		public static int KindOf(int seed, uint index)
		{
			float u = Unit(Hash(seed, index, 11));
			return u < GoldChance ? Gold : u < GoldChance + SilverChance ? Silver : None;
		}

		/// <summary>The extra loot of a silver or golden barrel (from the seed and its index).</summary>
		public static List<KeyValuePair<string, int>> LootOf(int seed, uint index, int kind)
		{
			var list = new List<KeyValuePair<string, int>>();
			if (kind == None) return list;
			string[] items = kind == Gold ? GoldItems : SilverItems;
			int[] min = kind == Gold ? GoldMin : SilverMin, max = kind == Gold ? GoldMax : SilverMax;
			var r = new System.Random(Hash(seed, index, 23));
			for (int i = 0; i < items.Length; i++) list.Add(new KeyValuePair<string, int>(items[i], r.Next(min[i], max[i] + 1)));
			if (kind == Gold && r.Next(3) == 0) list.Add(new KeyValuePair<string, int>(GoldRare[r.Next(GoldRare.Length)], 1 + r.Next(2)));
			return list;
		}

		/// <summary>Raft's floating barrel (its loot from a random dropper).</summary>
		public static bool IsBarrel(PickupItem_Networked p)
		{
			if (p == null || p.name.IndexOf("Barrel", StringComparison.OrdinalIgnoreCase) < 0) return false;
			PickupItem item = p.GetComponent<PickupItem>();
			return item != null && item.dropper != null;
		}

		/// <summary>This barrel's kind now (a test's forced one, else the world's roll).</summary>
		public static int Kind(PickupItem_Networked p)
		{
			if (!On || !IsBarrel(p)) return None;
			int forced;
			if (coloured.TryGetValue(p.GetInstanceID(), out forced) && forced < 0) return -forced;
			return KindOf(WorldOptions.Seed, p.ObjectIndex);
		}

		/// <summary>Raft spawned (or reused) a floating object: its colour.</summary>
		internal static void OnSpawned(PickupItem_Networked p)
		{
			if (p == null) return;
			int id = p.GetInstanceID();
			int kind = None;
			if (On && IsBarrel(p)) kind = TestKind ?? KindOf(WorldOptions.Seed, p.ObjectIndex);
			int was;
			bool had = coloured.TryGetValue(id, out was);
			if (kind == None) { if (had) { Paint(p, Color.white, false); coloured.Remove(id); } return; }
			// (a test's forced kind is kept as a negative number: the roll would say otherwise)
			coloured[id] = TestKind.HasValue ? -kind : kind;
			Paint(p, kind == Gold ? GoldTint : SilverTint, true);
			Log((kind == Gold ? "A golden" : "A silver") + " barrel #" + p.ObjectIndex);
		}

		/// <summary>The options changed: barrels in the sea take the colour that goes with them (plain when Barrels is off).</summary>
		internal static void Repaint()
		{
			if (!On && coloured.Count == 0) return;
			foreach (PickupItem_Networked p in UnityEngine.Object.FindObjectsOfType<PickupItem_Networked>())
				if (IsBarrel(p) || coloured.ContainsKey(p.GetInstanceID())) OnSpawned(p);
		}

		static void Paint(PickupItem_Networked p, Color c, bool on)
		{
			foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
			{
				if (r is ParticleSystemRenderer) continue;
				if (on) WorldRandomizer.TintTextures(r, c);
				else r.SetPropertyBlock(new MaterialPropertyBlock());
			}
		}

		/// <summary>Whether this barrel shows a silver or golden colour now (tests).</summary>
		internal static bool LooksSpecial(PickupItem_Networked p)
		{
			foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
			{
				var block = new MaterialPropertyBlock();
				r.GetPropertyBlock(block);
				foreach (string prop in new[] { "_Diffuse", "_MainTex" })
				{
					Texture t = r.sharedMaterial != null && r.sharedMaterial.HasProperty(prop) ? block.GetTexture(prop) : null;
					if (t != null && t.name.StartsWith("CI_Tinted_")) return true;
				}
			}
			return false;
		}

		static readonly HashSet<uint> given = new HashSet<uint>();

		/// <summary>This player picked up a barrel: a silver or golden one's extra loot (once per barrel).</summary>
		internal static void PickedUp(Network_Player player, PickupItem item)
		{
			if (player == null || !player.IsLocalPlayer || item == null) return;
			PickupItem_Networked p = item.GetComponent<PickupItem_Networked>();
			int kind = Kind(p);
			if (kind == None || !given.Add(p.ObjectIndex)) return;
			if (given.Count > 256) given.Clear();
			List<KeyValuePair<string, int>> loot = LootOf(WorldOptions.Seed, p.ObjectIndex, kind);
			var got = new List<string>();
			foreach (KeyValuePair<string, int> l in loot)
			{
				if (ItemManager.GetItemByName(l.Key) == null) continue;
				player.Inventory.AddItem(l.Key, l.Value);
				got.Add(l.Value + " " + l.Key);
			}
			string what = kind == Gold ? "A golden barrel" : "A silver barrel";
			Log(what + " #" + p.ObjectIndex + " picked up: +" + string.Join(", ", got.ToArray()));
			IslandInfo.Show(what + "!", "", "Inside, besides the usual: " + string.Join(", ", got.ToArray()) + ".");
		}

		[ConsoleCommand(name: "Barrels", docs: "The world option Silver & golden barrels: Barrels = the barrels in the sea now and their kinds; Barrels roll <index> [<to>] = what a barrel with that index is in this world (with <to>: the silver and golden ones in the range)")]
		public static void BarrelsCommand(string[] args)
		{
			uint index, to;
			if (args != null && args.Length > 2 && args[0] == "roll" && uint.TryParse(args[1], out index) && uint.TryParse(args[2], out to))
			{
				var special = new List<string>();
				for (uint i = index; i <= to && i < index + 100000; i++) { int k = KindOf(WorldOptions.Seed, i); if (k != None) special.Add("#" + i + "=" + Name(k)); }
				Debug.Log("[CUSTOM ISLANDS] Barrels: " + index + "-" + to + " (seed " + WorldOptions.Seed + "): " + special.Count + " silver or golden: " + string.Join(" ", special.ToArray()));
				return;
			}
			if (args != null && args.Length > 1 && args[0] == "roll" && uint.TryParse(args[1], out index))
			{
				int k = KindOf(WorldOptions.Seed, index);
				Debug.Log("[CUSTOM ISLANDS] Barrels: #" + index + " is " + Name(k) + (k == None ? "" : ", extra " + string.Join(", ", LootOf(WorldOptions.Seed, index, k).Select(l => l.Value + " " + l.Key).ToArray())));
				return;
			}
			List<PickupItem_Networked> sea = UnityEngine.Object.FindObjectsOfType<PickupItem_Networked>().Where(IsBarrel).OrderBy(p => p.ObjectIndex).ToList();
			Debug.Log("[CUSTOM ISLANDS] Barrels: " + (On ? "on" : "off") + ", " + sea.Count + " in the sea" + (sea.Count == 0 ? "" : ": " +
				string.Join(" ", sea.Select(p => "#" + p.ObjectIndex + "=" + Name(Kind(p)) + (LooksSpecial(p) ? "*" : "")).ToArray())));
		}

		static string Name(int kind) { return kind == Gold ? "golden" : kind == Silver ? "silver" : "plain"; }
	}

	[HarmonyPatch(typeof(ObjectSpawner), "SpawnItem")]
	static class GoldenBarrelsSpawn
	{
		static void Postfix(PickupItem_Networked __result)
		{
			try { GoldenBarrels.OnSpawned(__result); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [barrels] " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(Pickup), "AddItemToInventory")]
	static class GoldenBarrelsPickup
	{
		static void Postfix(Pickup __instance, PickupItem item)
		{
			try { GoldenBarrels.PickedUp(Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>(), item); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [barrels] " + e.Message); }
		}
	}
}
