using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world setting "Head start raft" (World settings in the New Game box, levels 1-3): a new world starts on a bigger
	/// raft that is already set up, built once by the host on the first load (through the host's BlockCreator, so it is
	/// Raft's own blocks, saved with the world like any built ones - joiners get them with the world).
	///   - Level 1: 6 x 12 foundations, a simple grill, a purifier, the research table, a bed and a small storage with
	///     10 planks, plastic, rope and stones, 5 raw fish and 5 potatoes.
	///   - Level 2: 8 x 14, level 1 plus 2 smelters, 2 medium storages (10 of each small island resource, 2 batteries,
	///     2 flippers), a sail and 10 item nets.
	///   - Level 3: 10 x 20, level 2 plus the Receiver with its 3 antennas and a battery, and 2 more medium storages.
	///   - Never twice: "@headstartdone=1" in the world file; a world whose raft is already big when it would be built
	///     (a crash before Raft saved it, then played on) is marked done instead.
	/// </summary>
	public static class HeadStart
	{
		public const float Cell = 1.5f;
		/// <summary>Above this many foundations the raft isn't the start raft any more: never built on.</summary>
		const int SmallRaft = 12;

		/// <summary>This world's level (host; 0 = none).</summary>
		public static int Level;
		/// <summary>This world's head start was built (or given up on).</summary>
		public static bool Done;
		/// <summary>Tests: what the last build placed ("" before one).</summary>
		public static string LastBuild = "";

		/// <summary>The next new world's level as the World settings window shows it (null = the remembered one).</summary>
		public static int? Pending;
		public static int Default { get { int l; int.TryParse((WorldRules.ReadDefault("headstart") ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out l); return Mathf.Clamp(l, 0, 3); } }
		public static void SaveDefault(int level) { WorldRules.SaveDefault("headstart", level.ToString(CultureInfo.InvariantCulture)); }
		public static int Chosen { get { if (!Pending.HasValue) Pending = Default; return Pending.Value; } set { Pending = Mathf.Clamp(value, 0, 3); } }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [head start] " + msg); }

		public static int Width(int level) { return level >= 3 ? 10 : level == 2 ? 8 : 6; }
		public static int Length(int level) { return level >= 3 ? 20 : level == 2 ? 14 : 12; }
		public static string Describe(int level) { return level <= 0 ? "off" : "level " + level + " (" + Width(level) + " x " + Length(level) + ")"; }

		#region The world file

		/// <summary>Before a world's file is read (WorldOptions.Reset): a new one gets the chosen level.</summary>
		internal static void Reset(bool isNew)
		{
			Level = 0;
			Done = false;
			LastBuild = "";
			if (isNew) { Level = Chosen; if (Level > 0) Log("New world: " + Describe(Level)); }
			Pending = null;
		}

		internal static bool ReadLine(string key, string value)
		{
			switch (key)
			{
				case "headstart": int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out Level); Level = Mathf.Clamp(Level, 0, 3); return true;
				case "headstartdone": Done = value.Trim() == "1"; return true;
			}
			return false;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Level <= 0) yield break;
			yield return "@headstart=" + Level.ToString(CultureInfo.InvariantCulture);
			if (Done) yield return "@headstartdone=1";
		}

		internal static bool HasState { get { return Level > 0; } }

		/// <summary>The world's file was read (or a new world started): the host builds the head start when it is due.</summary>
		internal static void OnWorldRead()
		{
			if (!Raft_Network.IsHost || Level <= 0 || Done || DynamicIslands.instance == null) return;
			DynamicIslands.instance.StartCoroutine(BuildWhenReady());
		}

		#endregion

		#region Building

		/// <summary>One thing the head start places: its item, where (in cells from the new raft's corner; x across, z along)
		/// and the turn about y.</summary>
		struct Piece
		{
			public string Item; public float X, Z, Turn;
			public Piece(string item, float x, float z, float turn = 0f) { Item = item; X = x; Z = z; Turn = turn; }
		}

		static IEnumerator BuildWhenReady()
		{
			float until = Time.realtimeSinceStartup + 120f;
			while (Time.realtimeSinceStartup < until)
			{
				Network_Player p = RAPI.GetLocalPlayer();
				Raft r = UnityEngine.Object.FindObjectOfType<Raft>();
				if (p != null && p.BlockCreator != null && r != null && Foundations(r).Count > 0 && LoadSceneManager.IsGameSceneLoaded) break;
				yield return new WaitForSecondsRealtime(0.5f);
			}
			// (the raft and the player settle a moment first - the start raft is still being made a frame or two)
			yield return new WaitForSecondsRealtime(2f);
			try { Build(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [head start] " + e); }
		}

		internal static List<Block> Foundations(Raft raft)
		{
			return raft.GetComponentsInChildren<Block>().Where(x => x != null && (x.name.Contains("Foundation") || (x.buildableItem != null && x.buildableItem.UniqueName.Contains("Foundation")))).ToList();
		}

		/// <summary>Host: builds this world's head start now, unless it was built already. Returns what happened.</summary>
		public static string Build()
		{
			if (!Raft_Network.IsHost) return "only the host builds the head start";
			if (Level <= 0 || Done) return "nothing to build (" + Describe(Level) + (Done ? ", built already" : "") + ")";
			Network_Player player = RAPI.GetLocalPlayer();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (player == null || raft == null) return "no player or raft yet";
			List<Block> floors = Foundations(raft);
			if (floors.Count == 0) return "no foundation on the raft";
			if (floors.Count > SmallRaft)
			{
				Done = true;
				Log("The raft has " + floors.Count + " foundations already: no head start (marked done)");
				return LastBuild = "skipped: the raft is big already (" + floors.Count + " foundations)";
			}
			int w = Width(Level), l = Length(Level);
			Vector3 o = floors[0].transform.localPosition;
			// (the start raft's cells, and the new raft centred on them)
			var taken = new HashSet<Vector2Int>();
			foreach (Block b in floors) taken.Add(CellOf(b.transform.localPosition, o));
			float cx = (float)taken.Average(c => c.x), cz = (float)taken.Average(c => c.y);
			int x0 = Mathf.RoundToInt(cx - (w - 1) / 2f), z0 = Mathf.RoundToInt(cz - (l - 1) / 2f);
			float top = FoundationTop(floors[0], raft) - o.y;

			int made = 0, failed = 0;
			Item_Base foundation = ItemManager.GetItemByName("Block_Foundation");
			for (int i = 0; i < w; i++)
				for (int j = 0; j < l; j++)
				{
					if (taken.Contains(new Vector2Int(x0 + i, z0 + j))) continue;
					if (Place(player, foundation, o + new Vector3((x0 + i) * Cell, 0f, (z0 + j) * Cell), 0f) != null) made++; else failed++;
				}

			var placed = new List<Block>();
			foreach (Piece pc in Pieces(Level, w, l))
			{
				// (on the floor: Raft puts a placeable's pivot where the player points on the foundation's top; a net hangs
				// in the water beside the raft, its pivot level with the top too)
				Block b = Place(player, ItemManager.GetItemByName(pc.Item), o + new Vector3((x0 + pc.X) * Cell, top, (z0 + pc.Z) * Cell), pc.Turn);
				if (b != null) placed.Add(b); else failed++;
			}
			string stocked = Stock(placed);
			string receiver = Level >= 3 ? SetUpReceiver(player, placed, o, top, x0, z0, w, l) : "";
			Done = true;
			LastBuild = "level " + Level + ": " + made + " foundations, " + placed.Count + " placed" + (failed > 0 ? ", " + failed + " failed" : "") + "; " + stocked + receiver;
			Log(LastBuild);
			DynamicIslands.Notify("Head start raft (" + Describe(Level) + ") is built: the storages hold your first supplies.", false);
			return LastBuild;
		}

		static Vector2Int CellOf(Vector3 local, Vector3 o) { return new Vector2Int(Mathf.RoundToInt((local.x - o.x) / Cell), Mathf.RoundToInt((local.z - o.z) / Cell)); }

		/// <summary>The foundation's walking surface, in the raft's space (its solid colliders' top; 0.6 above it without).</summary>
		internal static float FoundationTop(Block f, Raft raft)
		{
			float best = float.MinValue;
			foreach (Collider c in f.GetComponentsInChildren<Collider>())
			{
				if (c == null || c.isTrigger || !c.enabled) continue;
				Vector3 t = raft.transform.InverseTransformPoint(new Vector3(c.bounds.center.x, c.bounds.max.y, c.bounds.center.z));
				if (t.y > best) best = t.y;
			}
			return best > float.MinValue ? best : f.transform.localPosition.y + 0.6f;
		}

		static Block Place(Network_Player player, Item_Base item, Vector3 local, float turn)
		{
			if (item == null) return null;
			try { return player.BlockCreator.CreateBlockCheat(item, local, new Vector3(0f, turn, 0f), DPS.Default, 0); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [head start] " + item.UniqueName + ": " + e.Message); return null; }
		}

		/// <summary>What each level places on the floor, along the sides so the middle stays free (the start raft).</summary>
		static List<Piece> Pieces(int level, int w, int l)
		{
			var list = new List<Piece>
			{
				new Piece("Placeable_CookingStand_Food_One", 0, 1),
				new Piece("Placeable_CookingStand_Purifier_One", 0, 2),
				new Piece("Placeable_ResearchTable", 0, 4),
				new Piece("Placeable_Storage_Small", 0, 6),
				new Piece("Placeable_Bed_Basic", w - 1, 1.5f),
			};
			if (level >= 2)
			{
				list.Add(new Piece("Placeable_CookingStand_Smelter", w - 1, 4));
				list.Add(new Piece("Placeable_CookingStand_Smelter", w - 1, 6));
				list.Add(new Piece("Placeable_Storage_Medium", 0, 7));
				list.Add(new Piece("Placeable_Storage_Medium", 0, 8));
				list.Add(new Piece("Placeable_Sail", (w - 1) / 2f, l - 2));
				// (10 item nets in the water: the front row, the rest at the back)
				int front = Math.Min(w, 10);
				for (int i = 0; i < front; i++) list.Add(new Piece("Placeable_CollectionNet_Basic", i, l));
				for (int i = 0; i < 10 - front; i++) list.Add(new Piece("Placeable_CollectionNet_Basic", w / 2 - (10 - front) / 2 + i, -1));
			}
			if (level >= 3)
			{
				list.Add(new Piece("Placeable_Storage_Medium", 0, 9));
				list.Add(new Piece("Placeable_Storage_Medium", 0, 10));
			}
			return list;
		}

		/// <summary>The supplies in the storages, in the order they were placed: each storage gets its list.</summary>
		static string Stock(List<Block> placed)
		{
			var lists = new List<KeyValuePair<string, int>[]>
			{
				new[] { Kv("Plank", 10), Kv("Plastic", 10), Kv("Rope", 10), Kv("Stone", 10), Kv("Raw_Mackerel", 5), Kv("Raw_Potato", 5) },
			};
			if (Level >= 2)
			{
				lists.Add(new[] { Kv("Sand", 10), Kv("Clay", 10), Kv("Stone", 10), Kv("Scrap", 10), Kv("MetalOre", 10), Kv("Battery", 2) });
				lists.Add(new[] { Kv("CopperOre", 10), Kv("SeaVine", 10), Kv("Thatch", 10), Kv("Dirt", 10), Kv("VineGoo", 10), Kv("Flipper", 2) });
			}
			if (Level >= 3)
			{
				lists.Add(new[] { Kv("Plank", 40), Kv("Plastic", 40), Kv("Rope", 20), Kv("Scrap", 20), Kv("MetalOre", 20), Kv("CopperOre", 10) });
				lists.Add(new[] { Kv("Sand", 20), Kv("Clay", 20), Kv("Stone", 20), Kv("SeaVine", 20), Kv("Thatch", 20), Kv("Raw_Potato", 10) });
			}
			List<Storage_Small> storages = placed.Select(b => b.GetComponentInChildren<Storage_Small>(true)).Where(s => s != null).ToList();
			int items = 0, missing = 0;
			for (int i = 0; i < lists.Count && i < storages.Count; i++)
			{
				Inventory inv = Traverse.Create(storages[i]).Field("inventoryReference").GetValue<Inventory>();
				if (inv == null) { missing += lists[i].Length; continue; }
				foreach (var kv in lists[i])
				{
					if (ItemManager.GetItemByName(kv.Key) == null) { missing++; Log("no item " + kv.Key); continue; }
					inv.AddItem(kv.Key, kv.Value);
					items += kv.Value;
				}
			}
			if (storages.Count < lists.Count) missing += lists.Skip(storages.Count).Sum(x => x.Length);
			return storages.Count + " storages hold " + items + " items" + (missing > 0 ? " (" + missing + " kinds left out)" : "");
		}

		static KeyValuePair<string, int> Kv(string k, int v) { return new KeyValuePair<string, int>(k, v); }

		/// <summary>Level 3: the Receiver at the back, its 3 antennas on the raft as far apart as Raft asks, a battery in it.</summary>
		static string SetUpReceiver(Network_Player player, List<Block> placed, Vector3 o, float top, int x0, int z0, int w, int l)
		{
			Vector3 at = o + new Vector3((x0 + (w - 1) / 2f) * Cell, top, (z0 + 1) * Cell);
			Block rb = Place(player, ItemManager.GetItemByName("Placeable_Reciever"), at, 0f);
			Reciever rec = rb != null ? rb.GetComponentInChildren<Reciever>(true) : null;
			if (rec == null) return "; no receiver";
			placed.Add(rb);
			float min = 0f, max = 1000f, apart = 0f;
			try
			{
				object d = Traverse.Create(rec).Field("requiredAntennaDistance").GetValue();
				if (d != null) { min = Traverse.Create(d).Field("minValue").GetValue<float>(); max = Traverse.Create(d).Field("maxValue").GetValue<float>(); }
				apart = Traverse.Create(rec).Field("minDistanceBetweenAntennas").GetValue<float>();
			}
			catch (Exception e) { Log("receiver distances: " + e.Message); }
			// (the floor's cells, the farthest from each other first, within Raft's distance from the receiver)
			var cells = new List<Vector3>();
			for (int i = 0; i < w; i++)
				for (int j = 0; j < l; j++)
				{
					if (i > 0 && i < w - 1 && j > 0 && j < l - 1) continue;
					Vector3 c = o + new Vector3((x0 + i) * Cell, top, (z0 + j) * Cell);
					float d = Vector3.Distance(new Vector3(c.x, 0f, c.z), new Vector3(at.x, 0f, at.z));
					if (d > min + 0.5f && d < max - 0.5f && placed.All(b => (b.transform.localPosition - c).sqrMagnitude > 1f)) cells.Add(c);
				}
			var chosen = new List<Vector3>();
			while (chosen.Count < 3 && cells.Count > 0)
			{
				Vector3 best = cells.OrderByDescending(c => chosen.Count == 0 ? (c - at).sqrMagnitude : chosen.Min(x => (x - c).sqrMagnitude)).First();
				cells.Remove(best);
				if (chosen.All(x => Vector3.Distance(x, best) > apart + 0.5f)) chosen.Add(best);
			}
			Item_Base antenna = ItemManager.GetItemByName("Placeable_Reciever_Antenna");
			int antennas = 0;
			foreach (Vector3 c in chosen) { Block a = Place(player, antenna, c, 0f); if (a != null) { placed.Add(a); antennas++; } }
			Item_Base battery = ItemManager.GetItemByName("Battery");
			Battery slot = rb.GetComponentInChildren<Battery>(true);
			bool charged = slot != null && battery != null && slot.Insert(null, battery.MaxUses, battery.UniqueIndex); // (no player: it would take it from a hand)
			return "; the receiver with " + antennas + " antennas" + (charged ? " and a battery" : ", no battery");
		}

		#endregion

		[ConsoleCommand(name: "HeadStart", docs: "The world setting Head start raft: HeadStart = this world's level and the next new world's; HeadStart 0-3 (main menu) = the next new world's level (0 = off)")]
		public static void HeadStartCommand(string[] args)
		{
			int n;
			if (args != null && args.Length > 0 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
			{
				if (LoadSceneManager.IsGameSceneLoaded) { Debug.Log("[CUSTOM ISLANDS] The head start is chosen for a new world, in the main menu"); }
				else { Chosen = n; SaveDefault(Chosen); try { WorldSettingsWindow.Show(); } catch { } }
			}
			Debug.Log("[CUSTOM ISLANDS] Head start raft: this world " + Describe(Level) + (Done ? " (built)" : "") + ", the next new world " + Describe(Chosen) + (LastBuild.Length > 0 ? " - " + LastBuild : ""));
		}
	}
}
