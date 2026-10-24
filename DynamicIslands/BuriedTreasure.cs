using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Buried treasure for the metal detector and the shovel (the user, 2026-10-05: "vines, ziplines, metal detection" on
	/// the islands, every Raft feature on at least one). A "Buried treasure" marker in the editor; in a world it becomes
	/// Raft's own treasure point (TreasurePointManager.AddTreasurePoint): the detector beeps near it, three digs with the
	/// shovel bring up the chest, and the chest gives Raft's treasure loot. Every machine makes the same points with the
	/// same network indexes (from the island's id, above its pickups'), so digging and picking reach the other players.
	/// Dug up = remembered like a picked pickup (its ordinal in the island's state) and back after the regrow days.
	/// RF4: a treasure can carry extra loot of its own (treasure.loot), given to whoever picks up its chest, once. One that
	/// holds a story item never comes back (KeepBase in the island's state, shared like a used loot crate).
	/// </summary>
	public class BuriedTreasure : MonoBehaviour
	{
		/// <summary>Raft's treasure kind (its uniqueTreasureIndex: 0 = the common chest).</summary>
		public int Kind;
		/// <summary>Its place among the island's buried treasures.</summary>
		public int Number;
		/// <summary>The treasure point made for it (null: dug up before, or not made yet).</summary>
		public TreasurePoint Point;
		/// <summary>Whether a point was made for it on this load (not: dug up before and not grown back).</summary>
		public bool Made;
		/// <summary>Extra loot of its own (treasure.loot), given with Raft's treasure loot when its chest is picked up.</summary>
		public List<KeyValuePair<string, int>> Loot = new List<KeyValuePair<string, int>>();

		/// <summary>Island state keys of treasures dug up for good (they held a story item): KeepBase + Number.
		/// (after BossPhases.KeyBase 0xE0000 + 0xFFFF. It was 0xD0000, Survivors' and TriggerSounds' range: the sounds' tick
		/// dropped it and a story treasure came back - mprf4, 2026-10-18)</summary>
		public const int KeepBase = 0xF0000;

		/// <summary>KeepBase before 2026-10-18 (1d72043): still read, so a treasure dug in an older save stays dug.</summary>
		public const int OldKeepBase = 0xD0000;

		/// <summary>Dug up for good: its key, or the old key in an older save (not when a survivor cage on the island uses that key).</summary>
		public static bool DugForGood(IslandWorldState.Entry e, int number)
		{
			if (ContentState.IsUsed(e, KeepBase + number)) return true;
			int old = OldKeepBase + number;
			if (!ContentState.IsUsed(e, old)) return false;
			return e.Root == null || !e.Root.GetComponentsInChildren<SurvivorCage>(true).Any(c => c.KeyOf() == old);
		}

		/// <summary>Holds a story item: dug up once for the whole crew, never grows back.</summary>
		public bool HoldsStory { get { return Loot.Any(l => StoryItems.IsStory(l.Key)); } }

		/// <summary>The network indexes of the island's n-th treasure: the point's and its chest's (the chest's low 16 bits are the state ordinal).</summary>
		public static void Indexes(int islandId, int n, out uint point, out uint chest)
		{
			uint b = 0x40000000u | ((uint)(islandId & 0x3FFF) << 16) | 0xC000u;
			point = b + (uint)(n * 2);
			chest = point + 1;
		}

		/// <summary>A network index from Indexes (a point or chest of ours).</summary>
		public static bool IsOurs(uint index) { return (index & 0xC0000000u) == 0x40000000u && (index & 0xC000u) == 0xC000u; }

		public static int Ordinal(int islandId, int n) { uint p, c; Indexes(islandId, n, out p, out c); return (int)(c & 0xFFFF); }

		static TreasurePointManager Manager { get { return UnityEngine.Object.FindObjectOfType<TreasurePointManager>(); } }

		/// <summary>Makes the island's treasure points, except those dug up and not grown back (after its state was applied).</summary>
		public static void OnIslandReady(IslandWorldState.Entry e)
		{
			if (e == null || e.Root == null) return;
			BuriedTreasure[] all = e.Root.GetComponentsInChildren<BuriedTreasure>(true);
			if (all.Length == 0) return;
			TreasurePointManager tm = Manager;
			if (tm == null) { Debug.LogWarning("[CUSTOM ISLANDS] No TreasurePointManager: buried treasure left out"); return; }
			int made = 0;
			foreach (BuriedTreasure b in all)
			{
				ObjectState s;
				if (e.State.TryGetValue(Ordinal(e.Id, b.Number), out s) && !s.Active) continue;
				if (DugForGood(e, b.Number)) continue;
				uint point, chest;
				Indexes(e.Id, b.Number, out point, out chest);
				try
				{
					tm.AddTreasurePoint(point, chest, e.Root.transform, e.Root.transform.InverseTransformPoint(b.transform.position), b.Kind);
					b.Point = PointsOf(tm, e.Root.transform).LastOrDefault();
					b.Made = true;
					made++;
				}
				catch (Exception ex) { Debug.LogError("[CUSTOM ISLANDS] Buried treasure on '" + e.HostName + "': " + ex.Message); }
			}
			if (made > 0) Debug.Log("[CUSTOM ISLANDS] '" + e.HostName + "': " + made + " buried treasure(s)");
		}

		/// <summary>Records the dug-up treasures in the island's state (with IslandObjectState.Capture).</summary>
		public static void Capture(IslandWorldState.Entry e, int today)
		{
			if (e == null || e.Root == null) return;
			foreach (BuriedTreasure b in e.Root.GetComponentsInChildren<BuriedTreasure>(true))
			{
				// (not made on this load: dug up before, its state stays as it is)
				if (!b.Made) continue;
				int ord = Ordinal(e.Id, b.Number);
				bool gone = b.Point == null || b.Point.pickupNetworked == null || !b.Point.pickupNetworked.gameObject.activeInHierarchy;
				ObjectState old;
				if (!gone) { e.State.Remove(ord); continue; }
				if (e.State.TryGetValue(ord, out old) && !old.Active) continue;
				e.State[ord] = new ObjectState { Active = false, Yield = -1, Day = today };
			}
		}

		/// <summary>Takes the island's treasure points out of Raft's manager (the island unloads).</summary>
		public static void OnDespawn(GameObject root)
		{
			if (root == null || root.GetComponentInChildren<BuriedTreasure>(true) == null) return;
			TreasurePointManager tm = Manager;
			if (tm == null) return;
			try { tm.RemoveTreasurePointsViaTransformReference(root.transform); }
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] Removing buried treasure: " + ex.Message); }
		}

		/// <summary>Chests whose own loot this machine gave already (their network index).</summary>
		static readonly HashSet<uint> given = new HashSet<uint>();

		/// <summary>The local player picked something up: a treasure chest of ours gives its own loot (once), and one holding
		/// a story item is marked as gone for good.</summary>
		internal static void PickedUp(Network_Player player, PickupItem item)
		{
			if (player == null || !player.IsLocalPlayer || item == null) return;
			PickupItem_Networked p = item.GetComponent<PickupItem_Networked>();
			if (p == null) return;
			BuriedTreasure b = OfChest(p.ObjectIndex);
			if (b == null) return;
			if (!given.Add(p.ObjectIndex)) return;
			if (given.Count > 256) given.Clear();
			if (b.HoldsStory) ContentState.MarkUsed(b.transform, KeepBase + b.Number);
			if (b.Loot.Count == 0) return;
			List<string> got = TriggerZone.Give(b.Loot);
			Debug.Log("[CUSTOM ISLANDS] Buried treasure #" + p.ObjectIndex + " dug up: +" + string.Join(", ", got.ToArray()));
			if (got.Count > 0) IslandInfo.Show("Buried treasure!", "", "Inside, besides Raft's treasure: " + string.Join(", ", got.ToArray()) + ".");
		}

		/// <summary>The loaded treasure whose chest has this network index (null: none of ours).</summary>
		public static BuriedTreasure OfChest(uint chestIndex)
		{
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e == null || e.Root == null) continue;
				foreach (BuriedTreasure b in e.Root.GetComponentsInChildren<BuriedTreasure>(true))
				{
					uint point, chest;
					Indexes(e.Id, b.Number, out point, out chest);
					if (chest == chestIndex) return b;
				}
			}
			return null;
		}

		/// <summary>Forgets which chests gave their loot (tests).</summary>
		public static void ForgetGiven() { given.Clear(); }

		static readonly FieldInfo pointsField = typeof(TreasurePointManager).GetField("treasurePoints", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		/// <summary>Every treasure point Raft's manager keeps.</summary>
		public static List<TreasurePoint> AllPoints(TreasurePointManager tm)
		{
			var d = pointsField != null ? pointsField.GetValue(tm) as Dictionary<Transform, List<TreasurePoint>> : null;
			return d != null ? d.Values.SelectMany(l => l).Where(p => p != null).ToList() : new List<TreasurePoint>();
		}

		/// <summary>The treasure points Raft's manager keeps under a parent.</summary>
		public static List<TreasurePoint> PointsOf(TreasurePointManager tm, Transform parent)
		{
			var d = pointsField != null ? pointsField.GetValue(tm) as Dictionary<Transform, List<TreasurePoint>> : null;
			List<TreasurePoint> l;
			if (parent == null) return new List<TreasurePoint>();
			return d != null && d.TryGetValue(parent, out l) ? l.Where(p => p != null).ToList() : new List<TreasurePoint>();
		}
	}

	/// <summary>Leaves our own treasure points out of the world Raft sends a joining player: every machine makes them from the
	/// island itself. A landmark kind of point on our island has no landmark, so Raft's GetSave gives null and the whole world
	/// message threw (NullReferenceException in Message_TreasurePoint_Create.SerializeFast): the joiner waited for a world
	/// that never came (mpscenarios' hung CIJoinHost, 2026-10-18). A plain one came back on the joiner as a second point
	/// with the same network indexes.</summary>
	[HarmonyPatch(typeof(Message_TreasurePoint_Create), "SerializeFast")]
	static class BuriedTreasureNotSent
	{
		static void Prefix(Message_TreasurePoint_Create __instance)
		{
			try
			{
				if (__instance.rgd_treasurePoints == null) return;
				__instance.rgd_treasurePoints = __instance.rgd_treasurePoints.Where(r => r != null && !BuriedTreasure.IsOurs(r.tpObjectIndex)).ToArray();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [treasure] " + e.Message); }
		}
	}

	[HarmonyPatch(typeof(Pickup), "AddItemToInventory")]
	static class BuriedTreasurePickup
	{
		static void Postfix(Pickup __instance, PickupItem item)
		{
			try { BuriedTreasure.PickedUp(Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>(), item); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [treasure] " + e.Message); }
		}
	}
}
