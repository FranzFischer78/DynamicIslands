using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world option "Ghost rafts" (WorldOptions.GhostRafts): abandoned rafts of Raft's own blocks lie on the sea and come
	/// up ahead while sailing (host; the map type "ghostraft", so every player gets the same file). Three sizes, rolled from
	/// the raft's seed:
	///   - small (about 60%): a few foundations, a barrel, a note in a bottle;
	///   - medium (about 28%): a hut of walls and a roof, a barrel and a box, a captain's note, sometimes a rat or two;
	///   - large (about 12%): a wide raft with huts, pillars and a lookout, a hoard chest and barrels, guarded by rats on its
	///     deck and screechers circling above (Raft's stone birds that go for players).
	/// Not within the first 1.5 km of a world; then about one per 3 km, tried every 200 m until there is room ahead.
	/// </summary>
	public static class GhostRafts
	{
		public const string TypeName = "ghostraft";
		const float FirstAfter = 1500f, ChancePerKm = 0.33f, RetryMetres = 200f;

		static float sailed, sinceTry;
		static bool due;

		/// <summary>Tests: how many were brought in this world since it loaded.</summary>
		public static int Brought;

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [ghost rafts] " + msg); }

		internal static void Reset() { sailed = sinceTry = 0f; due = false; Brought = 0; }

		internal static bool ReadLine(string key, string value)
		{
			if (key != "ghostsailed") return false;
			// (NaN and Infinity read as numbers: NaN would stop them coming all session, Infinity would be saved again - left out)
			float v;
			sailed = float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) && !float.IsInfinity(v) ? Mathf.Max(0f, v) : 0f;
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (sailed > 0f) yield return "@ghostsailed=" + sailed.ToString("F0", CultureInfo.InvariantCulture);
		}

		/// <summary>Host: the raft sailed this far; now and then a ghost raft comes up ahead.</summary>
		internal static void OnSailed(float metres, Vector3 raftPos)
		{
			if (!Raft_Network.IsHost || !WorldOptions.On(WorldOptions.GhostRafts)) return;
			sailed += metres;
			if (!due && sailed > FirstAfter && UnityEngine.Random.value < 1f - Mathf.Pow(1f - ChancePerKm, metres / 1000f)) { due = true; sinceTry = RetryMetres; }
			if (!due) return;
			sinceTry += metres;
			if (sinceTry < RetryMetres) return;
			sinceTry = 0f;
			string r = CustomIslandSpawner.TrySpawn(raftPos, false, CustomIslandSpawner.TypePrefix + TypeName);
			Log(r);
			if (r.StartsWith("Spawning")) { due = false; sailed = FirstAfter - 1500f; Brought++; }
		}

		#region The map type

		public const int Small = 0, Medium = 1, Large = 2;
		public static readonly string[] SizeNames = { "small", "medium", "large" };

		/// <summary>The size a seed gives (the same on every machine).</summary>
		public static int SizeOf(int seed)
		{
			double r = new System.Random(seed ^ 0x6A05).NextDouble();
			return r < 0.6 ? Small : r < 0.88 ? Medium : Large;
		}

		/// <summary>Settings of a new ghost raft: its reach (for finding room at sea) follows its size.</summary>
		internal static IslandGenSettings Settings(System.Random rnd)
		{
			var s = new IslandGenSettings { Seed = rnd.Next(1, 999999), Height = 2f };
			int size = SizeOf(s.Seed);
			s.Radius = size == Large ? 26f : size == Medium ? 18f : 14f;
			return s;
		}

		static readonly string[] BottleTexts =
		{
			"If you find this raft, it's yours. We made for the tower lights and didn't look back.",
			"Day 40. The shark took the last of the foundations on the east side. We are going on with what floats.",
			"Left in a hurry. The barrel still has what we couldn't carry.",
			"Whoever reads this: don't sleep on a raft without a roof. We learned.",
		};
		static readonly string[] CaptainTexts =
		{
			"Captain's log. The rats came aboard with the last crate from the island. We fought them off the deck, then gave up the deck.",
			"Captain's log. Birds again at dawn, dropping stones on the roof. The crew left on the small raft. I am staying with the hoard.",
			"Captain's log. We found more than we could carry at the old research station. It is all in the big chest, under the hut. Guard it.",
			"Captain's log. Nobody answered on the Receiver for eleven days. We tie up here and wait. The rats don't wait.",
		};

		/// <summary>An abandoned raft on open water (no land): its size from the seed.</summary>
		internal static IslandFile Build(IslandGenSettings s, string name)
		{
			var f = new IslandFile
			{
				Name = name, TerrainSize = IslandGenerator.BuildArea, HeightmapResolution = IslandGenerator.BuildResolution,
				Heights = new float[IslandGenerator.BuildResolution, IslandGenerator.BuildResolution],
			};
			var k = new MapKit(f, s.Seed);
			var rnd = new System.Random(s.Seed);
			int size = SizeOf(s.Seed);
			float g = PlacementOptions.GridSize, sea = k.Sea;
			// (afloat like the player's raft: a deck a player stands on, 0.35 m above the sea)
			float floatY = sea + PlacementOptions.FoundationFloat, deck = floatY + PlacementOptions.FoundationPlanks; // (walls, pillars and loot on the planks)
			int w = size == Large ? 8 + rnd.Next(3) : size == Medium ? 5 + rnd.Next(2) : 3 + rnd.Next(2);
			int d = size == Large ? 6 + rnd.Next(3) : size == Medium ? 4 + rnd.Next(2) : 2 + rnd.Next(2);
			// the raft's middle in the build area
			Vector3 o = new Vector3(k.Mid.x - w * g / 2f, 0f, k.Mid.y - d * g / 2f);
			Func<int, int, Vector3> at = (x, z) => o + new Vector3(x * g, deck, z * g);
			Func<int, int, Vector2> flat = (x, z) => new Vector2(o.x + x * g, o.z + z * g);
			var missing = new HashSet<int>();
			for (int x = 0; x < w; x++)
				for (int z = 0; z < d; z++)
				{
					// a few foundations are gone at the edges of the bigger ones (never under the loot, at most one in eight)
					bool edge = x == 0 || z == 0 || x == w - 1 || z == d - 1;
					bool underLoot = (x == 1 && z == 1) || (x == w - 2 && z == d - 2) || (x == 2 && z == d - 2) || (x == w - 3 && z == 1) || (x == w - 2 && z == 0) || (x == w - 1 && z == d / 2);
					if (size != Small && edge && !underLoot && missing.Count < w * d / 8 && rnd.NextDouble() < 0.18) { missing.Add(x * 100 + z); continue; }
					k.Add("Block_Foundation", o + new Vector3(x * g, floatY, z * g), 0f, null, 0f);
				}
			Func<int, int, bool> has = (x, z) => x >= 0 && z >= 0 && x < w && z < d && !missing.Contains(x * 100 + z);
			Func<string, Dictionary<string, string>> loot = preset => new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot(preset) }, { ObjectProps.NoteTitle, "Ghost raft" } };

			if (size == Small)
			{
				k.Add("Loot_Barrel", at(1, d - 1 > 1 ? 1 : 0), 0f, loot("Basics"), 0f);
				k.Add("Note_Bottle", at(w - 1 > 1 ? w - 2 : 0, 0), (float)rnd.NextDouble() * 360f, Note("Message in a bottle", BottleTexts, rnd), 0f);
				if (rnd.NextDouble() < 0.5) k.Add("Block_Pillar_Wood", at(0, 0) + new Vector3(-g / 2, 0f, -g / 2), 0f, null, 0f);
			}
			else
			{
				// A hut in the middle: four walls' worth of thatch, pillars at its corners, a roof
				int hx = w / 2 - 1, hz = d / 2 - 1;
				Hut(k, at, g, hx, hz, rnd);
				k.Add("Loot_Barrel", at(1, 1), 0f, loot("Basics"), 0f);
				k.Add("Loot_Box", at(w - 2, d - 2), (float)rnd.NextDouble() * 40f, loot("Metal"), 0f);
				k.Add("Note_Paper", at(hx, hz) + new Vector3(0.3f, 0f, 0.3f), 0f, Note("Captain's log", CaptainTexts, rnd), 0f);
				k.Add("Block_Ladder", at(w, d / 2) + new Vector3(-g / 2, 0f, 0f), 90f, null, 0f);
				if (size == Medium && rnd.NextDouble() < 0.45)
					k.Creature("Rat", flat(w - 2, 1), 1 + rnd.Next(2), "Normal", 1f, null, false, deck, 0f);
			}
			if (size == Large)
			{
				// A second hut, a lookout of pillars, the hoard in the main hut, more barrels - and its guards
				int hx2 = 1, hz2 = d - 3;
				if (has(hx2, hz2) && has(hx2 + 1, hz2 + 1)) Hut(k, at, g, hx2, hz2, rnd);
				for (int i = 0; i < 4; i++)
				{
					int px = w - 1 - (i % 2), pz = i / 2;
					if (has(px, pz)) k.Add("Block_Pillar_Wood", at(px, pz) + new Vector3(g / 2, 0f, -g / 2), 0f, null, 0f);
				}
				var hoard = loot("Treasure");
				hoard[ObjectProps.NoteTitle] = "Ghost raft hoard";
				k.Add("Loot_Chest", at(w / 2, d / 2), 180f, hoard, 0f);
				k.Add("Loot_Barrel", at(2, d - 2), 0f, loot("Food"), 0f);
				k.Add("Loot_Barrel", at(w - 3, 1), 0f, loot("Metal"), 0f);
				// (not clearing around them: that takes away whatever has no settings - the raft's foundations, walls and pillars;
				// the hoard chest stood on the sea where the screechers' foundation had been, CA30)
				k.Creature("Rat", flat(2, 2), 3 + rnd.Next(3), "Hard", 1f, null, false, deck, 0f);
				k.Creature("Rat", flat(w - 3, d - 3), 2 + rnd.Next(2), "Normal", 1f, null, false, deck, 0f);
				k.Creature("StoneBird", flat(w / 2, d / 2), 1 + rnd.Next(2), "Normal", 1f, null, false, deck + 10f, 0f);
			}
			f.Props[IslandProps.Title] = size == Large ? "Ghost raft (large)" : "Ghost raft";
			return f;
		}

		static Dictionary<string, string> Note(string title, string[] texts, System.Random rnd)
		{
			return new Dictionary<string, string> { { ObjectProps.NoteTitle, title }, { ObjectProps.NoteText, texts[rnd.Next(texts.Length)] } };
		}

		/// <summary>A small hut of thatch walls and a roof over a 2 x 2 foundation square (its corner x, z).</summary>
		static void Hut(MapKit k, Func<int, int, Vector3> at, float g, int x, int z, System.Random rnd)
		{
			Vector3 c = at(x, z);
			foreach (Vector3 p in new[] { new Vector3(-g / 2, 0f, -g / 2), new Vector3(g * 1.5f, 0f, -g / 2), new Vector3(-g / 2, 0f, g * 1.5f), new Vector3(g * 1.5f, 0f, g * 1.5f) })
				k.Add("Block_Pillar_Wood", c + p, 0f, null, 0f);
			k.Add("Block_Wall_Thatch", c + new Vector3(0f, 0f, -g / 2), 0f, null, 0f);
			k.Add("Block_Wall_Thatch", c + new Vector3(g, 0f, g * 1.5f), 180f, null, 0f);
			k.Add("Block_Wall_Thatch", c + new Vector3(-g / 2, 0f, g), 90f, null, 0f);
			// (a hipped roof on the pillars and walls - half the time with its back corner gone: the ghost raft is a ruin)
			bool whole = rnd.NextDouble() < 0.5;
			RaftRoof.Hip((n, p, ry) => k.Add(n, p, ry, null, 0f), c + new Vector3(0f, RaftRoof.OnWalls, 0f), 2, 2, false, i => !whole && i == 3);
		}

		#endregion
	}
}
