using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// ROADMAP R1b: every object of an island has its own number (IslandObject.Uid), kept through edits. A saved world
	/// remembers what was used on an island by keys made from the objects' places in the file (the n-th pickup, chest,
	/// creature, zone, treasure, the i-th object's behaviours, locks and quest items - see Layout). When an edit removes
	/// objects or changes their order, Carry turns those keys into the new file's: old key -> the object (its number, and
	/// which of its pickups) -> that object's key in the new file. What belonged to a removed object is dropped.
	///
	/// In the file: a tail block "uid" (int32 block version, int32 count, int32 next number, int32 names check, int32[count]
	/// numbers), written only when the numbers aren't simply 1, 2, 3... with the next one count + 1 - every other file stays
	/// as it was. A file without the block (or one an older version of the mod re-saved: the count or the names check no
	/// longer fit) numbers its objects by place, 1, 2, 3..., which is what the keys meant before.
	/// </summary>
	public static class StableIds
	{
		public const string Tag = "uid";
		const int BlockVersion = 1;

		static int NamesCheck(IslandFile f)
		{
			unchecked
			{
				uint h = 2166136261;
				foreach (IslandObject o in f.Objects)
				{
					foreach (char c in o.Name ?? "") { h ^= c; h *= 16777619; }
					h ^= 10; h *= 16777619;
				}
				return (int)h;
			}
		}

		/// <summary>The block's numbers and next number when it fits the file, else null (next: 0).</summary>
		static int[] Parse(IslandFile f, out int next)
		{
			next = 0;
			byte[] d;
			if (f.Tail == null || !f.Tail.TryGetValue(Tag, out d) || d == null) return null;
			try
			{
				using (var r = new BinaryReader(new MemoryStream(d)))
				{
					if (r.ReadInt32() != BlockVersion) return null;
					int count = r.ReadInt32(), n = r.ReadInt32(), check = r.ReadInt32();
					if (count != f.Objects.Count || check != NamesCheck(f)) return null;
					var ids = new int[count];
					var seen = new HashSet<int>();
					for (int i = 0; i < count; i++)
					{
						ids[i] = r.ReadInt32();
						if (ids[i] <= 0 || !seen.Add(ids[i])) return null;
					}
					if (count > 0 && n <= ids.Max()) return null;
					next = n;
					return ids;
				}
			}
			catch (Exception) { return null; }
		}

		/// <summary>Whether the file has numbers of its own (a block that fits), not just its objects' places.</summary>
		public static bool HasOwn(IslandFile f) { int next; return Parse(f, out next) != null; }

		/// <summary>After reading a file: each object's number (by place without a block that fits).</summary>
		internal static void ReadTail(IslandFile f)
		{
			int next;
			int[] ids = Parse(f, out next);
			if (ids == null)
			{
				if (f.Tail != null && f.Tail.Remove(Tag))
					Debug.Log("[CUSTOM ISLANDS] '" + f.Name + "': its object numbers don't fit its objects (saved by an older version?) - numbered by place");
				for (int i = 0; i < f.Objects.Count; i++) f.Objects[i].Uid = i + 1;
				f.NextUid = f.Objects.Count + 1;
				return;
			}
			for (int i = 0; i < ids.Length; i++) f.Objects[i].Uid = ids[i];
			f.NextUid = next;
		}

		/// <summary>
		/// Gives every object without a number (new ones, and the second of two with one number - a copy) the next one,
		/// never one an earlier object of the island had.
		/// </summary>
		public static void Assign(IslandFile f)
		{
			int stale;
			Parse(f, out stale);
			int next = Math.Max(Math.Max(1, f.NextUid), stale);
			foreach (IslandObject o in f.Objects) if (o.Uid >= next) next = o.Uid + 1;
			var seen = new HashSet<int>();
			foreach (IslandObject o in f.Objects)
				if (o.Uid <= 0 || !seen.Add(o.Uid)) { o.Uid = next++; seen.Add(o.Uid); }
			f.NextUid = next;
		}

		/// <summary>Writes the block into the tail about to be saved (or leaves it out when the numbers are 1, 2, 3...).</summary>
		internal static void WriteTail(IslandFile f, Dictionary<string, byte[]> tail)
		{
			Assign(f);
			tail.Remove(Tag);
			bool plain = f.NextUid == f.Objects.Count + 1;
			for (int i = 0; plain && i < f.Objects.Count; i++) plain = f.Objects[i].Uid == i + 1;
			if (plain) return;
			using (var m = new MemoryStream())
			using (var w = new BinaryWriter(m))
			{
				w.Write(BlockVersion);
				w.Write(f.Objects.Count);
				w.Write(f.NextUid);
				w.Write(NamesCheck(f));
				foreach (IslandObject o in f.Objects) w.Write(o.Uid);
				w.Flush();
				tail[Tag] = m.ToArray();
			}
		}

		/// <summary>
		/// Before an island is saved over its file: new objects standing exactly where an object of the same name stood in
		/// the file (an island made again from its recipe, an object put back) get that object's number, and the next
		/// number never goes back below the file's. Then every object has one (Assign).
		/// </summary>
		public static int Prepare(IslandFile next, IslandFile before)
		{
			int matched = 0;
			if (before != null)
			{
				next.NextUid = Math.Max(next.NextUid, before.NextUid);
				var used = new HashSet<int>(next.Objects.Where(o => o.Uid > 0).Select(o => o.Uid));
				var free = new Dictionary<string, List<IslandObject>>();
				foreach (IslandObject o in before.Objects)
				{
					if (used.Contains(o.Uid)) continue;
					List<IslandObject> list;
					if (!free.TryGetValue(o.Name ?? "", out list)) free[o.Name ?? ""] = list = new List<IslandObject>();
					list.Add(o);
				}
				foreach (IslandObject o in next.Objects)
				{
					List<IslandObject> list;
					if (o.Uid > 0 || !free.TryGetValue(o.Name ?? "", out list)) continue;
					IslandObject was = list.FirstOrDefault(b => (b.Position - o.Position).sqrMagnitude < 0.05f * 0.05f);
					if (was == null) continue;
					o.Uid = was.Uid;
					list.Remove(was);
					matched++;
				}
			}
			Assign(next);
			return matched;
		}

		// ---- Keys --------------------------------------------------------------------------------------------------

		/// <summary>The keys whose number says the object's place in the file (Behaviours, CodeLock, QuestItemPickups).</summary>
		static readonly int[] IndexBases = { Behaviours.StateBase, Behaviours.DoneBase, Behaviours.SharedOnceBase, CodeLock.LockKeyBase, QuestItemPickups.KeyBase, Survivors.KeyBase, BossPhases.KeyBase };
		const int TreasureLow = 0xC000;

		/// <summary>Where each running number of an island's objects points: the object's place, and (pickups) which of its pickups.</summary>
		public class Layout
		{
			public int[] Uids;
			public readonly Dictionary<int, int> IndexOf = new Dictionary<int, int>();
			public readonly List<KeyValuePair<int, int>> Pickups = new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(-1, -1) }; // [n] (from 1)
			public readonly Dictionary<long, int> PickupOf = new Dictionary<long, int>();
			public readonly List<int> Creatures = new List<int>(), Loot = new List<int>(), Zones = new List<int>(), Treasure = new List<int>();
			/// <summary>Why the pickups can't be known (an object not loaded in this session); null when they can.</summary>
			public string Unsure;
		}

		static readonly Dictionary<string, int> pickupCounts = new Dictionary<string, int>();

		/// <summary>How many of Raft's pickups (trees, rocks, items) an object brings; -1 when it isn't loaded now.</summary>
		internal static int PickupCount(string name)
		{
			int n;
			if (pickupCounts.TryGetValue(name, out n)) return n;
			GameObject proto = PlaceableCatalog.Get(name);
			// (not in this Raft at all: missing - it keeps the numbers it had, PickupGap)
			if (proto == null) return PlaceableCatalog.IsIndexed(name) ? -1 : ShippedPickups(name);
			n = proto.GetComponentsInChildren<PickupItem_Networked>(true).Length;
			pickupCounts[name] = n;
			return n;
		}

		/// <summary>Raft's objects that bring pickups, and how many: measured (CIMeasurePickups) and shipped with the mod.</summary>
		public const string PickupsFile = "raft_pickups.txt";
		static Dictionary<string, int> shippedPickups;
		/// <summary>Tests: objects that "were" in Raft, with their pickups.</summary>
		internal static readonly Dictionary<string, int> TestPickups = new Dictionary<string, int>();

		/// <summary>
		/// ROADMAP R2b: how many pickups an object brought in the Raft version raft_pickups.txt was measured in (0 when it
		/// isn't listed). An object a Raft update removed keeps its numbers in a world, so the trees and items after it keep
		/// what the world saved for them. The shipped list, not this PC's Raft: every player numbers them the same.
		/// </summary>
		public static int ShippedPickups(string name)
		{
			int n;
			if (name == null) return 0;
			if (TestPickups.TryGetValue(name, out n)) return n;
			if (shippedPickups == null)
			{
				shippedPickups = new Dictionary<string, int>();
				try
				{
					byte[] bytes = RaftIslands.ModFile(PickupsFile);
					if (bytes == null) Debug.LogWarning("[CUSTOM ISLANDS] " + PickupsFile + " is missing: an object a Raft update removed would shift the numbers of the pickups after it");
					else
						foreach (string raw in System.Text.Encoding.UTF8.GetString(bytes).Split('\n'))
						{
							string[] p = raw.TrimEnd('\r').Split('\t');
							if (p.Length >= 2 && !p[0].StartsWith("#") && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0) shippedPickups[p[0]] = n;
						}
				}
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + PickupsFile + ": " + e.Message); }
			}
			return shippedPickups.TryGetValue(name, out n) ? n : 0;
		}

		/// <summary>
		/// The running numbers of an island in a world as IslandSpawner.SpawnBody gives them (flying: the objects under its
		/// own sea level are left out, but creatures, zones and treasure still count them) and RegisterNetworkIds (pickups in
		/// the order of the objects).
		/// </summary>
		public static Layout LayoutOf(IslandFile f, bool flying)
		{
			var l = new Layout { Uids = f.Objects.Select(o => o.Uid).ToArray() };
			if (!PlaceableCatalog.IsBuilt) l.Unsure = "the object catalog isn't loaded";
			for (int i = 0; i < f.Objects.Count; i++)
			{
				IslandObject o = f.Objects[i];
				l.IndexOf[o.Uid] = i;
				bool underSea = flying && o.Position.y < f.WaterLevel - 0.5f;
				if (ContentCatalog.IsCreature(o.Name)) { l.Creatures.Add(i); continue; }
				if (ContentCatalog.IsZone(o.Name)) { if (o.Name == ContentCatalog.TriggerZone) l.Zones.Add(i); continue; }
				if (ContentCatalog.IsTreasure(o.Name)) { l.Treasure.Add(i); continue; }
				if (ContentCatalog.IsHelper(o.Name) || underSea) continue;
				int count = PickupCount(o.Name);
				if (count < 0) { if (l.Unsure == null) l.Unsure = "'" + o.Name + "' isn't loaded"; count = 0; }
				for (int s = 0; s < count; s++)
				{
					l.PickupOf[((long)o.Uid << 16) | (uint)s] = l.Pickups.Count;
					l.Pickups.Add(new KeyValuePair<int, int>(i, s));
				}
				if (ObjectProps.IsLoot(o.Name, o.Props)) l.Loot.Add(i);
			}
			return l;
		}

		/// <summary>
		/// In a world, once an island's pickups are numbered: whether LayoutOf counted them as RegisterNetworkIds did. A
		/// mismatch (an object whose pickups change when made) would carry a world's trees and items to the wrong ones - logged.
		/// </summary>
		public static bool CheckPickups(IslandFile island, GameObject root, bool flying)
		{
			try
			{
				if (island == null || root == null) return true;
				Layout l = LayoutOf(island, flying);
				if (l.Unsure != null) return true;
				int actual = root.GetComponentsInChildren<PickupItem_Networked>(true).Length + root.GetComponentsInChildren<PickupGap>(true).Sum(g => g.Count),
					counted = l.Pickups.Count - 1;
				if (actual == counted) return true;
				Debug.LogWarning("[CUSTOM ISLANDS] '" + island.Name + "': " + actual + " pickups in the world, " + counted + " counted from its file - " +
					"an edit of this island may not carry its saved worlds' trees and items right");
				return false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Counting the pickups of '" + (island != null ? island.Name : "?") + "': " + e.Message); return true; }
		}

		/// <summary>The key in the new layout of what the old key named; false when its object is gone (or the key is unknown).</summary>
		static bool Move(int key, Layout was, Layout now, out int moved)
		{
			moved = key;
			Func<int, int> uidAt = i => i >= 0 && i < was.Uids.Length ? was.Uids[i] : 0;
			Func<int, int> indexNow = uid => { int j; return uid > 0 && now.IndexOf.TryGetValue(uid, out j) ? j : -1; };
			// (a counted kind: the n-th of its kind -> its object -> that object's place among its kind now)
			Func<List<int>, List<int>, int, int> counted = (a, b, n) =>
			{
				if (n < 0 || n >= a.Count) return -1;
				int j = indexNow(uidAt(a[n]));
				return j < 0 ? -1 : b.IndexOf(j);
			};
			if (key >= 1 && key < TreasureLow)
			{
				if (key >= was.Pickups.Count) return false;
				KeyValuePair<int, int> p = was.Pickups[key];
				int n;
				if (!now.PickupOf.TryGetValue(((long)uidAt(p.Key) << 16) | (uint)p.Value, out n)) return false;
				moved = n;
				return n < TreasureLow;
			}
			if (key >= TreasureLow && key <= 0xFFFF)
			{
				if ((key - TreasureLow - 1) % 2 != 0) return false;
				int t = counted(was.Treasure, now.Treasure, (key - TreasureLow - 1) / 2);
				moved = TreasureLow + t * 2 + 1;
				return t >= 0 && moved <= 0xFFFF;
			}
			int[] countedBases = { CreatureSpawner.StateKeyBase, ContentState.LootKeyBase, TriggerZone.KeyBase };
			List<int>[] wasLists = { was.Creatures, was.Loot, was.Zones }, nowLists = { now.Creatures, now.Loot, now.Zones };
			for (int k = 0; k < countedBases.Length; k++)
				if (key >= countedBases[k] && key < countedBases[k] + 0x10000)
				{
					int c = counted(wasLists[k], nowLists[k], key - countedBases[k]);
					moved = countedBases[k] + c;
					return c >= 0;
				}
			foreach (int b in IndexBases)
				if (key >= b && key < b + 0x10000)
				{
					int i = key - b;
					if (i >= was.Uids.Length) return true; // (not an object's: an old signal key in the same range - kept)
					int j = indexNow(uidAt(i));
					moved = b + j;
					return j >= 0 && j < 0x10000;
				}
			if (key >= Behaviours.PendingBase && key < Behaviours.PendingBase + 0x100000)
			{
				int i = (key - Behaviours.PendingBase) >> 4, ev = (key - Behaviours.PendingBase) & 0xF;
				int j = indexNow(uidAt(i));
				moved = Behaviours.PendingBase + (j << 4) + ev;
				return j >= 0 && j < 0x10000;
			}
			return true; // (not an object's: quest steps, rules, visits, signals - kept by their own place)
		}

		/// <summary>
		/// A world's saved state of an island (IslandObjectState.Encode) moved from one version of the island file to
		/// another. Null when the pickups can't be known now (unsure says why); dropped: what belonged to removed objects.
		/// </summary>
		public static string Carry(IslandFile was, IslandFile now, string state, bool flying, out int dropped, out string unsure)
		{
			dropped = 0;
			Layout a = LayoutOf(was, flying), b = LayoutOf(now, flying);
			unsure = a.Unsure ?? b.Unsure;
			if (unsure != null) return null;
			Dictionary<int, ObjectState> old = IslandObjectState.Decode(state);
			var moved = new Dictionary<int, ObjectState>();
			foreach (var kv in old)
			{
				int key;
				if (Move(kv.Key, a, b, out key) && !moved.ContainsKey(key)) moved[key] = kv.Value;
				else dropped++;
			}
			return IslandObjectState.Encode(moved);
		}

		/// <summary>Whether a world's line can be carried to the new file as it is: its rules and quest steps keep their places.</summary>
		public static bool ListsKept(IslandFile before, IslandFile after) { return !DynamicIslands.ListsShift(before, after); }

		/// <summary>The island's flying test for a world line's position (IslandSpawner.MakeRoot).</summary>
		public static bool FlyingAt(string y)
		{
			float v;
			return float.TryParse(y, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > IslandSpawner.FlyingThreshold;
		}
	}
}
