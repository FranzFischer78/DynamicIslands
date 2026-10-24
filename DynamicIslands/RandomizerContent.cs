using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Objects of a land-less island laid over one of Raft's islands (the randomizer's extras), placed at world positions
	/// found on Raft's island: the file keeps them relative to the island's middle (IslandProps.Centre).
	/// </summary>
	public class WorldKit
	{
		public readonly IslandFile File;
		readonly Vector3 anchor, mid;
		/// <summary>Where things already are (Raft's and ours), to keep new ones apart.</summary>
		public readonly List<Vector3> Taken = new List<Vector3>();

		public WorldKit(string name, Vector3 islandMiddle)
		{
			const int res = 33; // (no land: the heightmap is never used)
			File = new IslandFile { Name = name, TerrainSize = IslandGenerator.BuildArea, HeightmapResolution = res, Heights = new float[res, res] };
			mid = new Vector3(File.TerrainSize.x / 2f, File.WaterLevel, File.TerrainSize.z / 2f);
			File.Props[IslandProps.Centre] = mid.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + mid.z.ToString("0.###", CultureInfo.InvariantCulture);
			anchor = new Vector3(islandMiddle.x, 0f, islandMiddle.z);
		}

		public IslandObject Add(string name, Vector3 world, float yaw, Dictionary<string, string> props = null)
		{
			GameObject proto = PlaceableCatalog.Get(name);
			var o = new IslandObject { Name = name, Position = world - anchor + mid, EulerRotation = new Vector3(0f, yaw, 0f), Scale = proto != null ? proto.transform.localScale : Vector3.one, Props = props, Unscaled = proto == null };
			File.Objects.Add(o);
			Taken.Add(world);
			return o;
		}

		public IslandObject Chest(string kind, Vector3 world, float yaw, string title, string loot, float sink = 0f)
		{
			var props = new Dictionary<string, string> { { ObjectProps.LootItems, loot } };
			if (title.Length > 0) props[ObjectProps.NoteTitle] = title;
			return Add(kind, world - Vector3.up * sink, yaw, props);
		}

		public IslandObject Note(string kind, Vector3 world, float yaw, string title, string text)
		{
			return Add(kind, world, yaw, new Dictionary<string, string> { { ObjectProps.NoteTitle, title }, { ObjectProps.NoteText, text } });
		}

		public IslandObject Zone(Vector3 world, string id, float radius, string message)
		{
			var props = new Dictionary<string, string> { { ObjectProps.ZoneId, id }, { ObjectProps.ZoneRadius, radius.ToString("0.#", CultureInfo.InvariantCulture) } };
			if (message.Length > 0) props[ObjectProps.ZoneMessage] = message;
			return Add(ContentCatalog.TriggerZone, world, 0f, props);
		}

		public IslandObject Creature(AI_NetworkBehaviourType type, Vector3 world, float yaw, int count, Color? tint)
		{
			var props = new Dictionary<string, string> { { ObjectProps.CreatureCount, Mathf.Clamp(count, 1, ObjectProps.MaxCount).ToString(CultureInfo.InvariantCulture) } };
			if (tint.HasValue) { props[ObjectProps.TintColor] = ObjectProps.ColorText(tint.Value); props[ObjectProps.TintAmount] = "1"; }
			return Add("Creature_" + type, world, yaw, props);
		}
	}

	/// <summary>What the world randomizer puts on Raft's islands, and the oddity islands and boss lairs it brings.</summary>
	public static class RandomizerContent
	{
		static float Yaw(System.Random r) { return (float)r.NextDouble() * 360f; }
		static T One<T>(System.Random r, IList<T> list) { return list[r.Next(list.Count)]; }

		#region Extras on Raft's islands

		static readonly AI_NetworkBehaviourType[] LandKinds =
		{
			AI_NetworkBehaviourType.Boar, AI_NetworkBehaviourType.Pig, AI_NetworkBehaviourType.Bear, AI_NetworkBehaviourType.PolarBear, AI_NetworkBehaviourType.Hyena,
			AI_NetworkBehaviourType.Chicken, AI_NetworkBehaviourType.Goat, AI_NetworkBehaviourType.Llama,
		};

		/// <summary>Tests: every island counts as a big one and gets its cave and outpost.</summary>
		internal static bool ForceBigFinds;
		/// <summary>Tests: the find every island gets ("treasure", "camp" or "stash"; null: by chance).</summary>
		internal static string ForceFind;

		static bool Dry(float h, float slope) { return h > 1.2f && h < 40f && slope < 26f; }

		/// <summary>
		/// The extras for one of Raft's islands (host, once per island and world): more animals, extra loot, a find.
		/// Returns null when this island gets nothing. what: a short list for the log.
		/// </summary>
		public static IslandFile Extras(Landmark l, string name, RandomizerSettings s, int seed, out string what)
		{
			var rnd = new System.Random(seed);
			var ground = new LandGround(l);
			var k = new WorldKit(name, l.transform.position);
			var parts = new List<string>();
			// Raft's things keep their room
			if (l.landmarkItems != null) k.Taken.AddRange(l.landmarkItems.Where(i => i != null).Select(i => i.transform.position));
			if (l.spawners != null) k.Taken.AddRange(l.spawners.Where(sp => sp != null).Select(sp => sp.transform.position));
			bool big = WorldRandomizer.KindOf(l).IndexOf("Big", StringComparison.OrdinalIgnoreCase) >= 0;
			int raftCount = k.Taken.Count;

			if (s.Has(RandomizerSettings.Animals)) Animals(k, ground, l, s, rnd, parts);
			if (s.Has(RandomizerSettings.Loot)) Loot(k, ground, s, rnd, parts);
			if (s.Has(RandomizerSettings.Finds) && (ForceFind != null || rnd.NextDouble() < s.Pick(0.1f, 0.2f, 0.3f) * (big ? 1.6f : 1f))) Find(k, ground, l, s, rnd, parts, big);
			// Raft's big islands: now and then a cave under a mound of rocks, and an outpost with things from the quest islands
			if (ForceBigFinds) big = true;
			if (s.Has(RandomizerSettings.Finds) && big && (ForceBigFinds || rnd.NextDouble() < s.Pick(0.25f, 0.45f, 0.7f)))
			{
				// (what would poke into the den: Raft's trees, bushes, crates, clams and boulders - not its flowers and small stones - and what was put here already)
				var blockers = (l.landmarkItems ?? new LandmarkItem[0]).Where(i => i != null && System.Text.RegularExpressions.Regex.IsMatch(i.name, "Palm|Tree|Bush|Crate|Clam|Boulder|Bamboo")).Select(i => i.transform.position).Concat(k.Taken.Skip(raftCount)).ToList();
				string cave = RandomizerIslands.Grotto(k, ground, rnd, new[] { "Boar", "Boar", "Bear", "Rat" }[rnd.Next(4)], rnd.NextDouble() < 0.3 ? "Treasure" : "Metal", blockers);
				if (cave != null) parts.Add(cave);
			}
			if (s.Has(RandomizerSettings.Finds) && big && (ForceBigFinds || rnd.NextDouble() < s.Pick(0.2f, 0.35f, 0.5f)))
			{
				List<Theme> themes = RandomizerIslands.ThemesFor(TerrainPainter.Tropical);
				Vector3? spot = themes.Count > 0 ? ground.Find(rnd, (h, slope) => h > 2.5f && h < 25f && slope < 10f, k.Taken, 7f, 600) : null;
				if (spot.HasValue)
				{
					Theme t = themes[rnd.Next(themes.Count)];
					if (RandomizerIslands.DressWorld(k, ground, t, spot.Value, 8f, rnd)) parts.Add("an outpost (" + t.Label.ToLowerInvariant() + ")");
					else Debug.Log("[CUSTOM ISLANDS] [randomizer] no outpost: the " + t.Label.ToLowerInvariant() + " didn't fit at the level spot");
				}
				else if (themes.Count > 0) Debug.Log("[CUSTOM ISLANDS] [randomizer] no outpost: no level spot clear of Raft's things");
			}

			what = string.Join(", ", parts.ToArray());
			// (a name players read in the journal and lists, never the file name rnd-<seed>-<id> - the user, 2026-10-06)
			if (!k.File.Props.ContainsKey(IslandProps.Title) || k.File.Props[IslandProps.Title].Length == 0)
				k.File.Props[IslandProps.Title] = WorldRandomizer.TitleFor(parts);
			return k.File.Objects.Count > 0 ? k.File : null;
		}

		static Color? Colour(RandomizerSettings s, System.Random rnd, AI_NetworkBehaviourType type)
		{
			if (!s.Has(RandomizerSettings.Colours) || rnd.NextDouble() >= s.Pick(0.15f, 0.3f, 0.55f)) return null;
			WorldRandomizer.Variant[] p = WorldRandomizer.PaletteOf(type);
			return p != null ? p[rnd.Next(p.Length)].Tint : (Color?)null;
		}

		static void Animals(WorldKit k, LandGround g, Landmark l, RandomizerSettings s, System.Random rnd, List<string> parts)
		{
			List<LandmarkEntitySpawner> land = (l.spawners ?? new LandmarkEntitySpawner[0]).Where(sp => sp != null && LandKinds.Contains(sp.entityType)).ToList();
			if (land.Count > 0)
			{
				// More of the animals the island has, near where Raft has them
				if (rnd.NextDouble() < s.Pick(0.5f, 0.8f, 1f))
				{
					int spots = s.Pick(1, 2, 3);
					for (int i = 0; i < spots; i++)
					{
						LandmarkEntitySpawner sp = One(rnd, land);
						AI_NetworkBehaviourType[] kinds = (sp.spawnabeEntityTypes ?? new AI_NetworkBehaviourType[0]).Where(t => LandKinds.Contains(t)).ToArray();
						AI_NetworkBehaviourType type = kinds.Length > 0 ? One(rnd, kinds) : sp.entityType;
						Vector3? p = g.Find(rnd, Dry, k.Taken, 4f, 80, sp.transform.position, 20f);
						if (!p.HasValue) continue;
						int n = 1 + (s.Level >= RandomizerSettings.Wild ? rnd.Next(2) : 0);
						k.Creature(type, p.Value, Yaw(rnd), n, Colour(s, rnd, type));
						parts.Add(n + " more " + type);
					}
				}
			}
			else if (rnd.NextDouble() < s.Pick(0.15f, 0.3f, 0.5f))
			{
				// Animals where Raft has none
				double r = rnd.NextDouble();
				AI_NetworkBehaviourType type = r < 0.45 ? AI_NetworkBehaviourType.Boar : r < 0.65 ? AI_NetworkBehaviourType.Chicken : r < 0.8 ? AI_NetworkBehaviourType.Goat : r < 0.9 ? AI_NetworkBehaviourType.Llama :
					s.Level >= RandomizerSettings.Wild ? AI_NetworkBehaviourType.Bear : AI_NetworkBehaviourType.Boar;
				Vector3? p = g.Find(rnd, (h, slope) => h > 1.5f && h < 30f && slope < 22f, k.Taken, 5f, 150);
				if (p.HasValue)
				{
					int n = type == AI_NetworkBehaviourType.Chicken ? 2 + rnd.Next(2) : type == AI_NetworkBehaviourType.Goat ? 1 + rnd.Next(2) : type == AI_NetworkBehaviourType.Boar && s.Level >= RandomizerSettings.Wild ? 2 : 1;
					k.Creature(type, p.Value, Yaw(rnd), n, Colour(s, rnd, type));
					parts.Add(n + " new " + type);
				}
			}
			// Now and then a puffer fish guarding the reef
			if (rnd.NextDouble() < s.Pick(0.1f, 0.25f, 0.4f))
			{
				Vector3? p = g.Find(rnd, (h, slope) => h > -12f && h < -4f && slope < 35f, k.Taken, 6f, 80);
				if (p.HasValue)
				{
					int n = 1 + rnd.Next(2);
					k.Creature(AI_NetworkBehaviourType.PufferFish, p.Value + Vector3.up * 1.5f, Yaw(rnd), n, Colour(s, rnd, AI_NetworkBehaviourType.PufferFish));
					parts.Add(n + " puffer fish");
				}
			}
		}

		static readonly string[][] LandContainers =
		{
			new[] { "Loot_Crate", "Washed-up crate" }, new[] { "Loot_Barrel", "Barrel" }, new[] { "Loot_Box", "Wooden box" }, new[] { "Loot_ChestSmall", "Small chest" },
		};

		/// <summary>A loot preset, mostly basics and food, now and then metal or treasure.</summary>
		static string LootOf(System.Random rnd, RandomizerSettings s)
		{
			double r = rnd.NextDouble();
			double treasure = s.Level >= RandomizerSettings.Wild ? 0.15 : 0.1;
			return MapKit.Loot(r < treasure ? "Treasure" : r < 0.35 ? "Metal" : r < 0.65 ? "Food" : "Basics");
		}

		static void Loot(WorldKit k, LandGround g, RandomizerSettings s, System.Random rnd, List<string> parts)
		{
			// Not always: sometimes
			if (rnd.NextDouble() >= s.Pick(0.35f, 0.6f, 0.85f)) return;
			int onLand = 1 + rnd.Next(s.Pick(1, 2, 3)), under = rnd.Next(s.Pick(2, 3, 4));
			int placed = 0, sunk = 0;
			for (int i = 0; i < onLand; i++)
			{
				Vector3? p = g.Find(rnd, (h, slope) => h > 1f && h < 30f && slope < 22f, k.Taken, 3f, 120);
				if (!p.HasValue) continue;
				string[] c = One(rnd, LandContainers);
				k.Chest(c[0], p.Value, Yaw(rnd), c[1], LootOf(rnd, s));
				placed++;
			}
			for (int i = 0; i < under; i++)
			{
				Vector3? p = g.Find(rnd, (h, slope) => h > -14f && h < -3f && slope < 30f, k.Taken, 5f, 120);
				if (!p.HasValue) continue;
				k.Chest("Loot_SunkenBarrel", p.Value, Yaw(rnd), "Sunken barrel", LootOf(rnd, s));
				sunk++;
			}
			if (placed > 0) parts.Add(placed + " crate(s)");
			if (sunk > 0) parts.Add(sunk + " sunken barrel(s)");
		}

		static readonly string[] MapTexts =
		{
			"I buried what was left of our cargo where this island is highest. If you find this, it's yours. Dig where you can see the whole island.",
			"X marks the spot - the top of this very island. Whoever reads this: I won't be coming back for it.",
			"Day 40. Hid the chest up high, where the waves can't reach. The gulls saw me. Nobody else did.",
			"To whoever is next: the treasure is on the highest ground of this island. Share it with your crew.",
		};
		static readonly string[] CampNotes =
		{
			"We have moved on to find more land. Take what you need from the crate, and follow the birds.",
			"The shark took the last of our foundations. We're building a new raft on the other side. Help yourself.",
			"Gone fishing. Back never. The crate is for you.",
			"Three of us stayed here for a week. The fruit was good. The boars were not friendly.",
		};
		static readonly string[] Diaries =
		{
			"Day 12. Still no land bigger than this. I keep what I found in the chest by the tree.",
			"If you read this, I made it off this rock. Or I didn't. Either way, the chest is yours now.",
			"I counted the palms again. Still the same number. I'm leaving tomorrow, my stash stays.",
			"Note to self: the seagulls are not your friends. The coconuts are.",
			"The radio said something about a tower. I'm going to find it. Leaving my spare things here.",
		};

		static void Find(WorldKit k, LandGround g, Landmark l, RandomizerSettings s, System.Random rnd, List<string> parts, bool big)
		{
			double r = ForceFind == "treasure" ? 0.0 : ForceFind == "camp" ? 0.5 : ForceFind == "stash" ? 0.9 : rnd.NextDouble();
			if (r < 0.4 && TreasureHunt(k, g, s, rnd)) { parts.Add("a treasure hunt"); return; }
			if (r < 0.7 && big && Camp(k, g, s, rnd)) { parts.Add("an abandoned camp"); return; }
			if (Stash(k, g, l, s, rnd)) parts.Add("a castaway's stash");
		}

		static bool TreasureHunt(WorldKit k, LandGround g, RandomizerSettings s, System.Random rnd)
		{
			// Where the island is highest (not a sheer cliff), and a bottle on the beach
			Vector3? top = null;
			for (int i = 0; i < 160; i++)
			{
				Vector3? p = g.Find(rnd, (h, slope) => h > 3f && slope < 28f, k.Taken, 4f, 1);
				if (p.HasValue && (!top.HasValue || p.Value.y > top.Value.y)) top = p;
			}
			Vector3? beach = g.Find(rnd, (h, slope) => h > 0.25f && h < 2.2f && slope < 20f, k.Taken, 4f, 200);
			if (!top.HasValue || !beach.HasValue) return false;
			k.Zone(top.Value, "x", 6f, "X marks the spot!");
			k.Chest("Loot_Chest", top.Value, Yaw(rnd), "Buried treasure", MapKit.Loot("Treasure") + ";" + MapKit.Loot("Metal").Split(';').First(), 0.3f);
			k.Note("Note_Bottle", beach.Value, Yaw(rnd), "Treasure map", One(rnd, MapTexts));
			new MapKit(k.File, 0).Quest("Treasure hunt", "A bottle glints on the beach.", "The treasure is yours!", "",
				"read|Treasure map|1|Find the map in a bottle on the beach", "reach|x|1|Find the highest point of the island", "open|Buried treasure|1|");
			return true;
		}

		static bool Camp(WorldKit k, LandGround g, RandomizerSettings s, System.Random rnd)
		{
			Vector3? spot = g.Find(rnd, (h, slope) => h > 2f && h < 20f && slope < 12f, k.Taken, 7f, 200);
			if (!spot.HasValue) return false;
			Vector3 c = spot.Value;
			float yaw = Yaw(rnd);
			Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
			Func<float, float, Vector3> at = (x, z) =>
			{
				Vector3 p = c + turn * new Vector3(x, 0f, z), hit, n;
				return g.Hit(p.x, p.z, out hit, out n) ? hit : p;
			};
			k.Add("Placeable_Lantern_FireBasket", at(0f, 0f), 0f);
			k.Add("Placeable_Bed_Hammock", at(3.5f, 1f), yaw + 90f);
			k.Add("Placeable_Flag_02", at(-3f, 3f), yaw);
			k.Add("Placeable_CookingStand_Food_One", at(-2.5f, -1.5f), yaw + 30f);
			k.Note("Note_Board", at(-1f, 4f), yaw, "Camp notice", One(rnd, CampNotes));
			k.Chest("Loot_Crate", at(3f, -3f), yaw + 15f, "Camp supplies", MapKit.Loot("Food") + ";" + MapKit.Loot("Basics").Split(';').First());
			new MapKit(k.File, 0).Quest("The empty camp", "Someone camped here not long ago.", "Where did they go?", "", "read|Camp notice|1|Read the notice board", "open|Camp supplies|1|");
			return true;
		}

		static bool Stash(WorldKit k, LandGround g, Landmark l, RandomizerSettings s, System.Random rnd)
		{
			// By one of the island's trees if it has any
			List<LandmarkItem> trees = (l.landmarkItems ?? new LandmarkItem[0]).Where(i => i != null && i.transform.position.y > 1f &&
				System.Text.RegularExpressions.Regex.IsMatch(i.name, "Palm|Tree", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
			Vector3? p = null;
			if (trees.Count > 0) p = g.Find(rnd, Dry, k.Taken.Where(t => !trees.Any(tr => tr.transform.position == t)).ToList(), 2.5f, 60, One(rnd, trees).transform.position, 4f);
			if (!p.HasValue) p = g.Find(rnd, Dry, k.Taken, 3f, 150);
			if (!p.HasValue) return false;
			float yaw = Yaw(rnd);
			k.Chest("Loot_ChestSmall", p.Value, yaw, "Castaway's stash", MapKit.Loot("Basics") + ";" + MapKit.Loot("Food").Split(';').First());
			Vector3 hit, n, q = p.Value + Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.2f, 0f, 0.4f);
			k.Note("Note_Paper", g.Hit(q.x, q.z, out hit, out n) ? hit : q, yaw, "Castaway's diary", One(rnd, Diaries));
			return true;
		}

		#endregion

		#region Oddity islands

		/// <summary>The oddities: map type name, label, what the island is called.</summary>
		public static readonly string[][] Oddities =
		{
			new[] { "van", "Van island", "A van on a small island. Nobody knows how it got there" },
			new[] { "caravan", "Caravan island", "A small island with someone's caravan and their things" },
			new[] { "planecrash", "Plane crash", "A small plane crashed on a tiny island; its cargo is still there" },
			new[] { "boatwreck", "Stranded boat", "A boat ran aground on a small island, with the captain's stores" },
			new[] { "shack", "Hermit's shack", "A shack and a few hens on a small island; the hermit is gone" },
			new[] { "statue", "Statue island", "A statue on top of a small island, with an offering chest" },
			new[] { "rocket", "Rocket debris", "Debris of a rocket on a small island, still smoking, with its payload" },
			new[] { "hut", "Castaway's hut", "A hut of raft blocks on a small island, with the castaway's chest" },
		};

		/// <summary>Bottom of a set piece below its pivot at scale 1 (measured with CIProbeSetPieces), to stand it on the ground.</summary>
		static readonly Dictionary<string, float> Bottom = new Dictionary<string, float>
		{
			{ "Van_1", -1.54f }, { "Van_2", -0.76f }, { "Van3", -1.50f }, { "Van_4", -1.91f }, { "Van_5", -1.57f },
			{ "Caravan_Blue_01", -0.01f }, { "Caravan_Green_01", -0.01f }, { "Caravan_Green_02", -0.01f }, { "Caravan_Yellow_01", -0.01f },
			{ "Airplane", -1.98f }, { "BoatStranded", -3.40f }, { "Balboa_Shack", -0.13f }, { "Balboa_DecorationPrefabBase_SimpleTent", -0.06f },
			{ "TangaroaFounderStatue", -0.01f }, { "RaftMonument", -0.07f }, { "CaravanRocket", -0.02f },
			{ "CaravanRocketDebris_Body1", -0.37f }, { "CaravanRocketDebris_Body2", -0.51f }, { "CaravanRocketDebris_Top1", -0.09f }, { "CaravanRocketDebris_Exhaust", -0.08f },
			{ "CaravanRocketDebris_Leg1", -0.17f }, { "CaravanRocketDebris_Door", -0.36f }, { "CaravanRocketDebris_Canister", -0.10f },
			{ "Tire_02", 0f }, { "Pallet", -0.30f }, { "RT_PlasticBoat", -0.55f }, { "Well", -1.61f },
		};

		/// <summary>Stands a set piece on the generated ground at p, sunk a little and tilted (a wreck lies crooked).</summary>
		internal static IslandObject Piece(MapKit k, string name, Vector2 p, float yaw, float sink, float tiltX = 0f, float tiltZ = 0f, float clear = 0f)
		{
			if (clear > 0f) k.Clear(p, clear);
			// Its bottom as measured (raft_props.txt; the table above where it wasn't), on the lowest ground under it
			float bottom;
			PropInfo info = RaftProps.Get(name);
			if (info != null) bottom = info.Bottom; else Bottom.TryGetValue(name, out bottom);
			float ground = RandomizerIslands.LowestUnder((x, z) => k.Ground(new Vector2(x, z)), name, p, yaw);
			// (never more than half its height under the ground at its middle: a 1 m boat on a beach edge was buried)
			if (info != null) ground = Mathf.Max(ground, k.Ground(p) - info.Size.y * 0.5f);
			// (a setting of its own, so content placed next to it doesn't clear it away like scattered nature)
			IslandObject o = k.Add(name, new Vector3(p.x, ground - bottom - sink, p.y), yaw, new Dictionary<string, string> { { "set.piece", "1" } }, 0f);
			o.EulerRotation = new Vector3(tiltX, yaw, tiltZ);
			return o;
		}

		static Vector2 Around(Vector2 c, float yaw, float x, float z) { Vector3 v = Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0f, z); return c + new Vector2(v.x, v.z); }

		internal static float Tilt(System.Random r, float max) { return ((float)r.NextDouble() * 2f - 1f) * max; }

		/// <summary>An oddity island's content: the set piece near the middle, its loot and a note (kind null = any). forceSprinkle (tests only): the first prop of the theme's touch is this one.</summary>
		public static void Oddity(MapKit k, IslandGenSettings s, string kind, string forceSprinkle = null)
		{
			System.Random r = k.Rnd;
			if (kind == null) kind = Oddities[r.Next(Oddities.Length)][0];
			string[] info = Oddities.First(o => o[0] == kind);
			k.File.Props[IslandProps.Title] = info[1];
			// A flat, dry spot near the middle
			Vector2 c = k.Find(k.Mid, s.Radius * 0.45f, (above, slope) => above > 1.3f && slope < 9f, 1f) ?? k.Find(k.Mid, s.Radius * 0.6f, MapKit.Dry, 1f) ?? k.Highest(k.Mid, s.Radius * 0.3f);
			float yaw = (float)r.NextDouble() * 360f;
			switch (kind)
			{
				case "van":
					string van = One(r, new[] { "Van_1", "Van_2", "Van3", "Van_4", "Van_5" });
					Piece(k, van, c, yaw, 0.35f, Tilt(r, 4f), Tilt(r, 5f), 7f);
					for (int i = 0; i < 2; i++) Piece(k, "Tire_02", Around(c, yaw, 5f + i * 1.3f, -2f + i), (float)r.NextDouble() * 360f, 0.05f);
					k.Chest("Loot_Box", Around(c, yaw, -4.5f, 2f), "Glovebox", MapKit.Loot("Metal"));
					k.Note("Note_Paper", Around(c, yaw, -3.5f, -3f), "Road trip", One(r, new[] { "Day 3 of the road trip. The road ended. So did the land.", "Parked here for the night. That was a year ago.", "Note to self: vans don't float. They don't sink either, apparently." }));
					break;
				case "caravan":
					string caravan = One(r, new[] { "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Green_02", "Caravan_Yellow_01" });
					Piece(k, caravan, c, yaw, 0.05f, 0f, Tilt(r, 2f), 6f);
					Piece(k, "Balboa_DecorationPrefabBase_SimpleTent", Around(c, yaw, 0f, 5f), yaw + 180f, 0.05f, 0f, 0f, 3f);
					k.Add("Placeable_Lantern_FireBasket", k.At(Around(c, yaw, 4f, 3f)), 0f, null, 1.5f);
					k.Chest("Loot_Chest", Around(c, yaw, -3.5f, 1.5f), "Caravan locker", MapKit.Loot("Food") + ";" + MapKit.Loot("Basics").Split(';').First());
					k.Note("Note_Book", Around(c, yaw, 3f, -2.5f), "Diary", One(r, new[] { "We brought the caravan to the coast for the view. The coast came to us.", "Grandma's caravan. Still smells like soup.", "If you find the keys, don't bother. The engine drowned." }));
					break;
				case "planecrash":
					Piece(k, "Airplane", c, yaw, 0.6f, Tilt(r, 7f), Tilt(r, 12f), 9f);
					k.Chest("Loot_Crate", Around(c, yaw, 5.5f, 2f), "Cargo", MapKit.Loot("Treasure"));
					k.Chest("Loot_Box", Around(c, yaw, -5f, -2.5f), "Supplies", MapKit.Loot("Food"));
					k.Note("Note_Papers", Around(c, yaw, 4f, -3.5f), "Flight log", One(r, new[] { "Fuel low. Visibility low. Land... any land. Found some.", "Mayday. Nobody answered. Landed anyway. Sort of.", "Cargo: spare parts, medicine, one very annoyed parrot. The parrot left." }));
					break;
				case "boatwreck":
					// On the beach, lying on its side
					Vector2 beach = k.Find(k.Mid, s.Radius * 1.1f, (above, slope) => above > 0.3f && above < 1.8f && slope < 15f, 1f) ?? c;
					Vector2 toMid = k.Mid - beach;
					float boatYaw = Mathf.Atan2(toMid.x, toMid.y) * Mathf.Rad2Deg + 90f;
					Piece(k, "BoatStranded", beach, boatYaw, 1.3f, Tilt(r, 4f), 12f + (float)r.NextDouble() * 8f, 10f);
					Vector2 dry = k.Find(beach, 12f, MapKit.Dry, 4f) ?? Around(beach, boatYaw, 0f, 6f);
					k.Chest("Loot_Barrel", dry, "Ship's stores", MapKit.Loot("Food"));
					k.Chest("Loot_Box", Around(dry, boatYaw, 2f, 1f), "Captain's locker", MapKit.Loot("Metal"));
					k.Note("Note_Book", Around(dry, boatYaw, -1.5f, 1.5f), "Captain's log", One(r, new[] { "Ran aground at high tide. Waited for the next one. It never came high enough.", "The crew took the lifeboat. I took the good biscuits.", "Anchored here. Permanently." }));
					break;
				case "shack":
					Piece(k, "Balboa_Shack", c, yaw, 0.1f, 0f, 0f, 10f);
					k.Chest("Loot_Chest", Around(c, yaw, 0f, 6.5f), "Hermit's chest", MapKit.Loot("Food") + ";" + MapKit.Loot("Metal").Split(';').First());
					k.Note("Note_Sign", Around(c, yaw, -4f, 6f), "Keep out", One(r, new[] { "Keep out. Or come in. I'm not here anyway.", "Feed the hens. They're all I had.", "Gone to find people. Taking the good hat." }));
					Vector2? hens = k.Find(c, 14f, MapKit.Dry, 5f);
					if (hens.HasValue) k.Creature("Chicken", hens.Value, 2);
					break;
				case "statue":
					Vector2 top = k.Highest(k.Mid, s.Radius * 0.5f);
					Piece(k, One(r, new[] { "TangaroaFounderStatue", "RaftMonument" }), top, yaw, 0.15f, 0f, 0f, 5f);
					k.Chest("Loot_ChestSmall", Around(top, yaw, 0f, 3.2f), "Offering", MapKit.Loot("Treasure"));
					k.Note("Note_Sign", Around(top, yaw, 2.5f, 3.5f), "Offering", "Take what you need. Leave the rest for the next castaway.");
					break;
				case "rocket":
					string[] debris = { "CaravanRocketDebris_Body1", "CaravanRocketDebris_Body2", "CaravanRocketDebris_Top1", "CaravanRocketDebris_Exhaust", "CaravanRocketDebris_Leg1", "CaravanRocketDebris_Door", "CaravanRocketDebris_Canister" };
					Piece(k, "CaravanRocket", c, yaw, 0.2f, 25f + (float)r.NextDouble() * 20f, Tilt(r, 20f), 5f);
					for (int i = 0; i < 12; i++)
					{
						float a = (float)r.NextDouble() * 360f, d = 1.5f + (float)r.NextDouble() * 7f;
						Piece(k, One(r, debris), Around(c, a, 0f, d), (float)r.NextDouble() * 360f, 0.05f, Tilt(r, 40f), Tilt(r, 40f));
					}
					k.Atmosphere(c, 14f, "#3A3A3A", 0.25f, "#FFB070", 0.25f, "embers");
					k.Chest("Loot_Crate", Around(c, yaw, 4f, 2f), "Payload", MapKit.Loot("Treasure") + ";" + MapKit.Loot("Metal").Split(';').First());
					k.Note("Note_Paper", Around(c, yaw, -3f, 3f), "Launch report", "Launch successful. Landing... less so.");
					break;
				default: // hut
					Hut(k, c, yaw);
					break;
			}
			// A touch of the quest island the set piece comes from
			string theme;
			Theme th = RandomizerIslands.OddityTheme.TryGetValue(kind, out theme) ? RandomizerIslands.ThemeOf(theme) : null;
			if (th != null) RandomizerIslands.Sprinkle(k, th, c, 6f, 13f, 3 + r.Next(4), forceSprinkle);
		}

		/// <summary>A small hut of Raft's blocks on the land, open on one side, with a hammock and the castaway's chest.</summary>
		static void Hut(MapKit k, Vector2 c, float yaw)
		{
			float g = PlacementOptions.GridSize;
			const int w = 3, d = 2;
			k.Clear(c, 6f);
			// Level on the highest ground under it, like a raft pulled onto the beach
			float top = float.MinValue;
			for (int x = -1; x <= w; x++) for (int z = -1; z <= d; z++) top = Mathf.Max(top, k.Ground(c + new Vector2(x * g, z * g)));
			Vector3 o = new Vector3(c.x, top, c.y);
			// (walls, pillars and things on the foundations' planks - FloatDepth, 0.35, put them 0.35 m over them, in the air)
			float deck = top + PlacementOptions.FoundationPlanks;
			// (and the ground built up under it to that height, blended over 3 m: on a slope its low side hung 0.7 m in the air)
			IslandFile f = k.File;
			float step = f.TerrainSize.x / (f.HeightmapResolution - 1);
			Vector2 lo = c - new Vector2(g, g), hi = c + new Vector2(w * g, d * g);
			for (int zi = Mathf.Max(0, Mathf.FloorToInt((lo.y - 4f) / step)); zi <= Mathf.Min(f.HeightmapResolution - 1, Mathf.CeilToInt((hi.y + 4f) / step)); zi++)
				for (int xi = Mathf.Max(0, Mathf.FloorToInt((lo.x - 4f) / step)); xi <= Mathf.Min(f.HeightmapResolution - 1, Mathf.CeilToInt((hi.x + 4f) / step)); xi++)
				{
					float px = xi * step, pz = zi * step;
					float out_ = Mathf.Max(Mathf.Max(lo.x - px, px - hi.x), Mathf.Max(lo.y - pz, pz - hi.y));
					float wgt = out_ <= 0f ? 1f : out_ < 3f ? 1f - out_ / 3f : 0f;
					float h = f.Heights[zi, xi] * f.TerrainSize.y;
					if (wgt > 0f && h < top) f.Heights[zi, xi] = Mathf.Lerp(h, top - 0.05f, wgt) / f.TerrainSize.y;
				}
			for (int x = 0; x < w; x++)
				for (int z = 0; z < d; z++)
					k.Add("Block_Foundation", o + new Vector3(x * g, 0f, z * g), 0f, null, 0f);
			// (a hipped roof on the walls and the corner pillars, as Raft's building puts one)
			RaftRoof.Hip((n, p, ry) => k.Add(n, p, ry, null, 0f), new Vector3(o.x, deck + RaftRoof.OnWalls, o.z), w, d);
			for (int x = 0; x < w; x++)
			{
				k.Add("Block_Wall_Thatch", new Vector3(o.x + x * g, deck, o.z - g / 2f), 0f, null, 0f);
				k.Add("Block_Wall_Thatch", new Vector3(o.x + x * g, deck, o.z + (d - 1) * g + g / 2f), 0f, null, 0f);
			}
			for (int z = 0; z < d; z++) k.Add("Block_Wall_Thatch", new Vector3(o.x - g / 2f, deck, o.z + z * g), 90f, null, 0f);
			foreach (Vector2 corner in new[] { new Vector2(-g / 2f, -g / 2f), new Vector2((w - 0.5f) * g, -g / 2f), new Vector2(-g / 2f, (d - 0.5f) * g), new Vector2((w - 0.5f) * g, (d - 0.5f) * g) })
				k.Add("Block_Pillar_Wood", new Vector3(o.x + corner.x, deck, o.z + corner.y), 0f, null, 0f);
			int furniture = k.File.Objects.Count;
			k.Add("Placeable_Bed_Hammock", new Vector3(o.x + g, deck, o.z + g * 0.5f), 90f, null, 0f);
			k.Add("Loot_Chest", new Vector3(o.x, deck, o.z + g), 90f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Basics") + ";" + MapKit.Loot("Food").Split(';').First() }, { ObjectProps.NoteTitle, "Castaway's chest" } }, 0f);
			k.Add("Note_Book", new Vector3(o.x + g * 2f, deck, o.z), 0f, new Dictionary<string, string> { { ObjectProps.NoteTitle, "Castaway's diary" }, { ObjectProps.NoteText, "Built this from what the sea gave me. The sea wants it back, bit by bit. Take the chest before it does." } }, 0f);
			// (ROADMAP LM13: maybe a bit of plastic or scrap on the floor)
			GenBuildings.FloorPickups(k, new Vector2(o.x - g / 2f + 0.15f, o.z - g / 2f + 0.15f), new Vector2(o.x + (w - 0.5f) * g - 0.15f, o.z + (d - 0.5f) * g - 0.15f), deck, furniture);
		}

		#endregion

		#region Boss lairs

		/// <summary>A boss lair: a plateau where a huge, very tough named beast and its guards keep a big hoard.</summary>
		public static void Lair(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Vector2 c = k.Mid;
			k.Clear(c, 26f);
			string beast, label, name, guard, guardLabel;
			string tint;
			if (s.Style == TerrainPainter.Snowy) { beast = "PolarBear"; label = "Polar bear"; name = "Frostfang"; guard = "PolarBear"; guardLabel = "Polar bear"; tint = "#CFE0FF"; }
			else if (s.Style == TerrainPainter.Volcanic) { beast = "Bear"; label = "Bear"; name = "Ashmaw"; guard = "Boar"; guardLabel = "Warthog"; tint = "#5A4A48"; }
			else if (s.Style == TerrainPainter.Desert) { beast = "Hyena"; label = "Hyena"; name = "The Laughing One"; guard = "Hyena"; guardLabel = "Hyena"; tint = "#8C6A4A"; }
			else if (s.Style == TerrainPainter.Tropical) { beast = "Boar"; label = "Warthog"; name = "The Tusk King"; guard = "Boar"; guardLabel = "Warthog"; tint = "#6B4034"; }
			else { beast = "Bear"; label = "Bear"; name = "Old Ironhide"; guard = "Boar"; guardLabel = "Warthog"; tint = "#4A3A34"; }
			k.File.Props[IslandProps.Title] = name + "'s lair";
			k.Zone(c, "arena", 22f, "The ground shakes... " + name + " wakes!");
			k.Atmosphere(c, 40f, "#2A1A1A", 0.35f, "#FF6A4A", 0.25f, "embers");
			var boss = new Dictionary<string, string>
			{
				{ ObjectProps.CreatureCount, "1" }, { ObjectProps.CreatureHealth, "6" }, { ObjectProps.CreatureDamage, "2.5" }, { ObjectProps.CreatureSpeed, "1.35" },
				{ ObjectProps.CreatureSize, beast == "Boar" || beast == "Hyena" ? "2.2" : "1.9" }, { ObjectProps.CreatureZone, "arena" }, { ObjectProps.CreatureRespawn, "0" },
				{ ObjectProps.TintColor, tint }, { ObjectProps.TintAmount, "0.8" }, { ObjectProps.NoteTitle, name },
			};
			k.Add("Creature_" + beast, k.At(c + new Vector2(9f, 0f)), (float)r.NextDouble() * 360f, boss, 2f);
			int guards = 2;
			for (int i = 0; i < guards; i++)
			{
				var g = new Dictionary<string, string>
				{
					{ ObjectProps.CreatureCount, "1" }, { ObjectProps.CreatureHealth, "2" }, { ObjectProps.CreatureDamage, "1.5" }, { ObjectProps.CreatureSpeed, "1.2" },
					{ ObjectProps.CreatureSize, "1.15" }, { ObjectProps.CreatureZone, "arena" }, { ObjectProps.CreatureRespawn, "0" },
				};
				k.Add("Creature_" + guard, k.At(Around(c, i * 180f + 45f, 0f, 12f)), (float)r.NextDouble() * 360f, g, 2f);
			}
			k.Chest("Loot_ChestLarge", c - new Vector2(5f, 0f), "Hoard", MapKit.Loot("Treasure") + ";" + MapKit.Loot("Metal"));
			string head = beast == "PolarBear" ? "Head_PolarBear" : beast == "Bear" ? "Head_Bear" : beast == "Hyena" ? "Head_Hyena" : "Head_Boar";
			k.Chest("Loot_Chest", c - new Vector2(5f, 3f), "Trophy chest", ContentCatalog.PresetLoot(new[] { head + "*1", "TitaniumIngot*4", "ExplosiveGoo*2", "HealingSalve_Good*2" }));
			int kills = guard == beast ? 1 + guards : 1;
			k.Quest(name, "Climb to the top. Something big sleeps up there, and it has been collecting.", name + " is defeated. The hoard is yours.", "",
				"reach|arena|1|Climb to the top of the plateau", "kill|" + label + "|" + kills + "|Defeat " + name + (guard == beast ? " and the guards" : ""), "open|Hoard|1|Take the hoard");
		}

		#endregion
	}
}
