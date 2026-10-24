using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A kind of island the generator can make on its own: the ranges its settings are rolled from (style, layout,
	/// size), how high it floats, and the content it gets (chests, notes, zones, creatures, atmosphere, a quest).
	/// World plans ("type:&lt;name&gt;"), the quest editor's "bring a new island" and the editor's Generate window use them.
	/// </summary>
	public class MapType
	{
		public string Name, Label, Description;
		/// <summary>Shown to players on arrival (the island's info.title), or "" for none.</summary>
		public string Title = "";
		/// <summary>Settings for one island of this type (the seed comes from rnd too); null: rolled from Ranges.</summary>
		public Func<System.Random, IslandGenSettings> Settings;
		/// <summary>The ranges its settings are rolled from, when they can be said as data (a .maptype file can hold them); null: Settings is code.</summary>
		public MapTypeRanges Ranges;
		/// <summary>The .maptype file it was read from (MapTypeFiles), or null for a built-in type.</summary>
		public string FromFile;
		/// <summary>A file's type: the built-in types whose settings code and content code it uses ("" = none), and its own content lines.</summary>
		public string SettingsFrom = "", ContentFrom = "";
		public List<string> Rules = new List<string>();
		/// <summary>Generator settings set after rolling ("Field value", a file's "set" lines - MapTypeFiles.ApplySets).</summary>
		public List<string> Sets = new List<string>();
		/// <summary>Chance of floating in the air, and how high then (m above sea level).</summary>
		public float FlyingChance;
		public float FlyingMin = 40f, FlyingMax = 90f;
		/// <summary>Under water: its top this many metres below the surface (0 = not sunken).</summary>
		public float SunkenDepth;
		/// <summary>What is placed on the generated land (optional).</summary>
		public Action<MapKit, IslandGenSettings> Content;
		/// <summary>Makes the whole file instead of the generator (objects-only places such as a wreck).</summary>
		public Func<IslandGenSettings, string, IslandFile> Build;
	}

	/// <summary>
	/// Roofs of Raft's own roof blocks, put together the way Raft's building snaps them, resting on Raft's walls and pillars
	/// (both 2.37-2.39 m over the deck). Measured in the editor (recipes kit_roof2, kit_genhuts): a straight piece's pivot is
	/// on its eave - the edge of its cell - and it climbs 1.2 m across the cell away from it (turned 0: towards -z, 90: -x,
	/// 180: +z, 270: +x); corners, V ridges, end caps and pyramids sit in their cell's middle (a corner turned 0 has its
	/// eaves on -x and +z, 90: +x +z, 180: +x -z, 270: -x -z; an end cap turned 0 closes a ridge's +x end). A roof's pivots go
	/// 2.42 m over the deck, its underside 0.1 m below them, on the walls' and pillars' tops; each ring further in sits
	/// 1.21 m higher. The generator's huts had one straight piece per cell at the cell's middle: half a cell out over the
	/// front wall, 0.6 m over its pillars, the back half of the hut open to the sky (the user, 2026-10-03).
	/// </summary>
	public static class RaftRoof
	{
		/// <summary>How far over the deck a roof on Raft's walls and pillars has its pivots.</summary>
		public const float OnWalls = 2.42f;
		/// <summary>How much higher each ring of a roof sits than the one around it.</summary>
		public const float Ring = 1.21f;

		/// <summary>
		/// A hipped roof over w x d cells of the 1.5 m grid: add(name, pivot, yaw) for each piece. first: the middle of the
		/// cell at the -x -z corner, at the roof's pivot height (the deck + OnWalls). One row: a ridge with closed ends (one
		/// cell: a pyramid); two or more each way: corners, straight pieces along the sides, the inside one ring up. wood:
		/// Raft's wooden roof blocks instead of thatch. skip(i): leaves the i-th piece out (a ruin's hole in the roof).
		/// </summary>
		public static void Hip(Action<string, Vector3, float> add, Vector3 first, int w, int d, bool wood = false, Func<int, bool> skip = null)
		{
			int n = 0;
			Action<string, Vector3, float> put = (name, at, yaw) => { if (skip == null || !skip(n)) add(name, at, yaw); n++; };
			string straight = wood ? "Block_Roof_Straight_Wood" : "Block_Roof_Straight_Thatch", corner = wood ? "Block_Roof_Corner_Wood" : "Block_Roof_Corner_Thatch",
				pyramid = wood ? "Block_Roof_Wood_Pyramid" : "Block_Roof_Thatch_Pyramid", cap = wood ? "Block_Roof_Wood_EndCap" : "Block_Roof_Thatch_EndCap",
				ridge = wood ? "Block_Roof_Wood_StraightV" : "Block_Roof_Thatch_StraightV";
			float g = PlacementOptions.GridSize;
			for (; w > 0 && d > 0; w -= 2, d -= 2, first += new Vector3(g, Ring, g))
			{
				Vector3 o = first;
				Func<int, int, Vector3> cell = (x, z) => o + new Vector3(x * g, 0f, z * g);
				if (w == 1 && d == 1) { put(pyramid, cell(0, 0), 0f); return; }
				if (d == 1)
				{
					put(cap, cell(0, 0), 180f);
					for (int x = 1; x < w - 1; x++) put(ridge, cell(x, 0), 0f);
					put(cap, cell(w - 1, 0), 0f);
					return;
				}
				if (w == 1)
				{
					put(cap, cell(0, 0), 90f);
					for (int z = 1; z < d - 1; z++) put(ridge, cell(0, z), 90f);
					put(cap, cell(0, d - 1), 270f);
					return;
				}
				put(corner, cell(0, 0), 270f);
				put(corner, cell(w - 1, 0), 180f);
				put(corner, cell(0, d - 1), 0f);
				put(corner, cell(w - 1, d - 1), 90f);
				for (int x = 1; x < w - 1; x++)
				{
					put(straight, cell(x, 0) + new Vector3(0f, 0f, -g / 2f), 180f);
					put(straight, cell(x, d - 1) + new Vector3(0f, 0f, g / 2f), 0f);
				}
				for (int z = 1; z < d - 1; z++)
				{
					put(straight, cell(0, z) + new Vector3(-g / 2f, 0f, 0f), 270f);
					put(straight, cell(w - 1, z) + new Vector3(g / 2f, 0f, 0f), 90f);
				}
			}
		}
	}

	/// <summary>
	/// Places content on a generated island file: heights come from the file, positions are terrain-local (as in
	/// IslandFile), and content pushes aside scattered nature around it. Deterministic for a seed.
	/// </summary>
	public class MapKit
	{
		public readonly IslandFile File;
		public readonly System.Random Rnd;
		readonly float step;
		readonly List<Vector2> placed = new List<Vector2>();
		/// <summary>The pieces a story island's look stood on the ground, with how far each was sunk (Remakes.Stand): stood
		/// again once the ground stops changing (Remakes.SettleLook).</summary>
		internal readonly List<KeyValuePair<IslandObject, float>> Stood = new List<KeyValuePair<IslandObject, float>>();
		/// <summary>Terrain height of the sea (the file's water level: 20 m on a shallow seabed, 160 m on a deep sea floor).</summary>
		public float Sea { get { return File.WaterLevel; } }

		public MapKit(IslandFile file, int seed)
		{
			File = file;
			Rnd = new System.Random(seed * 31 + 7);
			step = file.TerrainSize.x / (file.HeightmapResolution - 1);
		}

		/// <summary>The middle of the build area (generated land is centred on it).</summary>
		public Vector2 Mid { get { return new Vector2(File.TerrainSize.x / 2f, File.TerrainSize.z / 2f); } }

		/// <summary>Ground height (m above the terrain's base; the sea is at Sea).</summary>
		public float Ground(Vector2 p) { return IslandGenerator.SampleHeights(File.Heights, File.HeightmapResolution, step, p.x, p.y) * File.TerrainSize.y; }

		public float Slope(Vector2 p)
		{
			float dx = (Ground(p + new Vector2(step, 0)) - Ground(p - new Vector2(step, 0))) / (2f * step);
			float dz = (Ground(p + new Vector2(0, step)) - Ground(p - new Vector2(0, step))) / (2f * step);
			return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
		}

		public Vector3 At(Vector2 p, float lift = 0f) { return new Vector3(p.x, Ground(p) + lift, p.y); }

		/// <summary>The highest ground within radius of a point.</summary>
		public Vector2 Highest(Vector2 around, float radius)
		{
			radius = Mathf.Min(radius, IslandGenerator.BuildArea.x / 2f); // (a .maptype's top:<n> can be any number: no further than the island's area)
			Vector2 best = around;
			float h = float.MinValue;
			for (float x = -radius; x <= radius; x += 2f)
				for (float z = -radius; z <= radius; z += 2f)
				{
					if (x * x + z * z > radius * radius) continue;
					Vector2 p = around + new Vector2(x, z);
					float g = Ground(p);
					if (g > h) { h = g; best = p; }
				}
			return best;
		}

		/// <summary>A random spot within radius where ok(height above the sea, slope) holds and nothing else was put, or null.</summary>
		public Vector2? Find(Vector2 around, float radius, Func<float, float, bool> ok, float keepApart = 8f)
		{
			for (int i = 0; i < 600; i++)
			{
				float a = (float)Rnd.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)Rnd.NextDouble()) * radius;
				Vector2 p = around + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
				if (placed.Any(q => (q - p).sqrMagnitude < keepApart * keepApart)) continue;
				if (ok(Ground(p) - Sea, Slope(p))) return p;
			}
			return null;
		}

		/// <summary>Nothing this kit put is within apart of p (what Find keeps clear of).</summary>
		public bool IsFree(Vector2 p, float apart) { return !placed.Any(q => (q - p).sqrMagnitude < apart * apart); }

		/// <summary>Removes scattered nature (objects without settings) around a spot, so content isn't inside a tree.</summary>
		public void Clear(Vector2 p, float radius)
		{
			File.Objects.RemoveAll(o => (o.Props == null || o.Props.Count == 0) && new Vector2(o.Position.x - p.x, o.Position.z - p.y).sqrMagnitude < radius * radius);
		}

		public IslandObject Add(string name, Vector3 position, float yaw = 0f, Dictionary<string, string> props = null, float clear = 3f)
		{
			var p2 = new Vector2(position.x, position.z);
			// (set on the ground: down to the lowest ground under its base, as the generator and the randomizer's props stand -
			// on a slope its low side stood in the air)
			if (Mathf.Abs(position.y - Ground(p2)) < 0.01f)
			{
				float low = RandomizerIslands.LowestUnder((x, z) => Ground(new Vector2(x, z)), name, p2, yaw);
				if (!float.IsNaN(low)) position.y = Mathf.Min(position.y, low);
			}
			if (clear > 0f) Clear(p2, clear);
			GameObject proto = PlaceableCatalog.Get(name);
			var o = new IslandObject { Name = name, Position = position, EulerRotation = new Vector3(0, yaw, 0), Scale = proto != null ? proto.transform.localScale : Vector3.one, Props = props, Unscaled = proto == null };
			File.Objects.Add(o);
			placed.Add(p2);
			return o;
		}

		float Yaw() { return (float)Rnd.NextDouble() * 360f; }

		/// <summary>Loot of one of the chest presets (Basics, Metal, Food, Treasure), with the items this Raft has.</summary>
		public static string Loot(string preset)
		{
			var p = ContentCatalog.LootPresets.FirstOrDefault(x => x.Key == preset);
			return p.Value != null ? ContentCatalog.PresetLoot(p.Value) : ContentCatalog.DefaultLoot();
		}

		/// <summary>A container with loot; the title is what quests and players call it.</summary>
		public IslandObject Chest(string kind, Vector2 p, string title, string loot, string note = "", float lift = 0f)
		{
			var props = new Dictionary<string, string> { { ObjectProps.LootItems, loot } };
			if (title.Length > 0) props[ObjectProps.NoteTitle] = title;
			if (note.Length > 0) props[ObjectProps.NoteText] = note;
			return Add(kind, At(p, lift), Yaw(), props, 4f);
		}

		public IslandObject Note(string kind, Vector2 p, string title, string text)
		{
			return Add(kind, At(p), Yaw(), new Dictionary<string, string> { { ObjectProps.NoteTitle, title }, { ObjectProps.NoteText, text } });
		}

		public IslandObject Zone(Vector2 p, string id, float radius, string message = "")
		{
			var props = new Dictionary<string, string> { { ObjectProps.ZoneId, id }, { ObjectProps.ZoneRadius, radius.ToString("0.#", CultureInfo.InvariantCulture) } };
			if (message.Length > 0) props[ObjectProps.ZoneMessage] = message;
			return Add(ContentCatalog.TriggerZone, At(p), 0f, props, 0f);
		}

		/// <summary>Creatures at a spot (type as in Creature_&lt;type&gt;), with a difficulty preset (ObjectProps.Presets); scattered nature
		/// within <paramref name="clear"/> m is taken away (0 on a build of objects without settings: a ghost raft's foundations).</summary>
		public IslandObject Creature(string type, Vector2 p, int count, string preset = "Normal", float size = 1f, string zone = null, bool respawn = true, float lift = 0f, float clear = 2f)
		{
			var props = new Dictionary<string, string> { { ObjectProps.CreatureCount, Mathf.Clamp(count, 1, ObjectProps.MaxCount).ToString() } };
			float[] m = ObjectProps.Presets.FirstOrDefault(x => x.Key == preset).Value;
			if (m != null && preset != "Normal")
			{
				props[ObjectProps.CreatureHealth] = m[0].ToString("0.##", CultureInfo.InvariantCulture);
				props[ObjectProps.CreatureDamage] = m[1].ToString("0.##", CultureInfo.InvariantCulture);
				props[ObjectProps.CreatureSpeed] = m[2].ToString("0.##", CultureInfo.InvariantCulture);
			}
			if (size != 1f) props[ObjectProps.CreatureSize] = size.ToString("0.##", CultureInfo.InvariantCulture);
			if (zone != null) props[ObjectProps.CreatureZone] = zone;
			if (!respawn) props[ObjectProps.CreatureRespawn] = "0";
			return Add("Creature_" + type, At(p, lift), Yaw(), props, clear);
		}

		/// <summary>An atmosphere zone: fog and light colours (#RRGGBB) and amounts, particles (AtmosphereZone.ParticleKinds).</summary>
		public IslandObject Atmosphere(Vector2 p, float radius, string fog, float fogAmount, string light, float lightAmount, string particles)
		{
			var props = new Dictionary<string, string>
			{
				{ ObjectProps.ZoneRadius, radius.ToString("0.#", CultureInfo.InvariantCulture) },
				{ ObjectProps.AtmoFog, fog }, { ObjectProps.AtmoFogAmount, fogAmount.ToString("0.##", CultureInfo.InvariantCulture) },
				{ ObjectProps.AtmoLight, light }, { ObjectProps.AtmoLightAmount, lightAmount.ToString("0.##", CultureInfo.InvariantCulture) },
				{ ObjectProps.AtmoParticles, particles },
			};
			return Add(ContentCatalog.AtmosphereZoneName, At(p), 0f, props, 0f);
		}

		/// <summary>The island's quest; steps as "type|target|count|text".</summary>
		public void Quest(string title, string intro, string done, string reward, params string[] steps)
		{
			var q = new IslandQuest { Title = title, Intro = intro, Done = done, Reward = reward };
			foreach (string line in steps)
			{
				string[] p = line.Split('|');
				int n;
				q.Steps.Add(new IslandQuest.Step { Type = p[0], Target = p.Length > 1 ? p[1] : "", Count = p.Length > 2 && int.TryParse(p[2], out n) ? n : 1, Text = p.Length > 3 ? p[3] : "" });
			}
			q.To(File.Props);
		}

		/// <summary>Terrain-local position of a point given relative to the middle (generator islets and stacks).</summary>
		public Vector2 FromMid(Vector4 o) { return Mid + new Vector2(o.x, o.y); }

		public static bool Dry(float above, float slope) { return above > 1.2f && slope < 18f; }
		public static bool Beach(float above, float slope) { return above > 0.6f && above < 3.5f && slope < 15f; }
	}

	/// <summary>The map types: the built-in ones, then those of .maptype files (MapTypeFiles). Plans and the spawner refer to them by name.</summary>
	public static class MapTypes
	{
		static readonly int[] AllStyles = { TerrainPainter.Tropical, TerrainPainter.Snowy, TerrainPainter.Desert, TerrainPainter.Forest, TerrainPainter.Volcanic };

		/// <summary>
		/// Ranges for a type's settings (rolled as the map types always were: one of the styles, then the seed, radius,
		/// height, roughness around rough and 1 to peaksMax peaks). Kept as data so ExportMapType can write them to a file.
		/// </summary>
		static MapTypeRanges Gen(int[] styles, int shape, float rMin, float rMax, float hMin, float hMax, float rough, float density, int peaksMax = 3, string likeRaft = "")
		{
			return new MapTypeRanges { Styles = styles, Shape = shape, RadiusMin = rMin, RadiusMax = rMax, HeightMin = hMin, HeightMax = hMax, Roughness = rough, Density = density, Peaks = peaksMax, LikeRaft = likeRaft };
		}

		static int[] Of(params int[] styles) { return styles; }

		// (land objects as dense as on Raft's own small islands; before All, which uses it - static fields start in order)
		static readonly MapTypeRanges OddityRanges = Gen(Of(TerrainPainter.Tropical, TerrainPainter.Tropical, TerrainPainter.Desert, TerrainPainter.Forest), IslandShapes.Round, 24f, 32f, 3f, 6f, 0.3f, 0.5f, 1, MapTypeRanges.LikeRaftSmall);

		public static readonly List<MapType> All = new List<MapType>
		{
			new MapType { Name = "random", Label = "Random island", Description = "Any style and size; now and then a flying one", FlyingChance = 0.1f,
				Ranges = new MapTypeRanges { Random = true, Styles = AllStyles } },
			Styled("tropical", "Tropical island", "Palms, bushes and fruit trees", TerrainPainter.Tropical),
			Styled("snowy", "Snowy island", "Pines and snow drifts (Temperance)", TerrainPainter.Snowy),
			Styled("desert", "Desert island", "Cacti and dry grass (Caravan)", TerrainPainter.Desert),
			Styled("forest", "Forest island", "Birches and pines (Balboa)", TerrainPainter.Forest),
			Styled("volcanic", "Volcanic island", "A cone with a crater", TerrainPainter.Volcanic),

			new MapType { Name = "sandbar", Label = "Sandbar", Description = "A tiny island with a few palms and a small chest: a rest stop between islands",
				Ranges = Gen(Of(TerrainPainter.Tropical), IslandShapes.Round, 12f, 23f, 3f, 4f, 0.25f, 0.9f, 1),
				Content = (k, s) => k.Chest("Loot_ChestSmall", k.Highest(k.Mid, s.Radius), "Driftwood cache", MapKit.Loot("Basics")) },

			new MapType { Name = "atoll", Label = "Atoll", Title = "Atoll", Description = "A ring of low land around a shallow lagoon, with turtles and a sunken barrel",
				Ranges = Gen(Of(TerrainPainter.Tropical), IslandShapes.Atoll, 110f, 170f, 4f, 8f, 0.6f, 0.6f),
				Content = (k, s) =>
				{
					k.Chest("Loot_SunkenBarrel", k.Mid, "Sunken barrel", MapKit.Loot("Metal"));
					Vector2? lagoon = k.Find(k.Mid, s.Radius * 0.4f, (above, slope) => above < -1.5f);
					if (lagoon.HasValue) k.Creature("Turtle", lagoon.Value, 2);
				} },

			new MapType { Name = "archipelago", Label = "Archipelago", Title = "Archipelago", Description = "Several islets on a shallow shelf; a castaway hid three caches on them (quest)",
				Ranges = Gen(Of(TerrainPainter.Tropical, TerrainPainter.Forest), IslandShapes.Archipelago, 130f, 200f, 12f, 30f, 0.5f, 0.55f),
				Content = Archipelago },

			new MapType { Name = "stacks", Label = "Sea stacks", Title = "Sea stacks", Description = "Steep rock pillars; a chest waits on top of the tallest (quest: build your way up)",
				Ranges = Gen(Of(TerrainPainter.Tropical, TerrainPainter.Desert, TerrainPainter.Snowy), IslandShapes.Stacks, 90f, 150f, 25f, 55f, 0.6f, 0.45f),
				Content = Stacks },

			new MapType { Name = "boss", Label = "Boss island", Title = "The plateau", Description = "A flat-topped mesa with a ramp; walking into the arena wakes a huge beast (quest, big reward)",
				Ranges = Gen(Of(TerrainPainter.Forest, TerrainPainter.Volcanic, TerrainPainter.Snowy), IslandShapes.Plateau, 78f, 113f, 12f, 20f, 0.45f, 0.35f),
				Content = Boss },

			new MapType { Name = "volcano", Label = "Volcano", Title = "Volcano", Description = "A tall volcano; embers, red light and dark smoke near the crater",
				Ranges = Gen(Of(TerrainPainter.Volcanic), IslandShapes.Round, 95f, 148f, 60f, 100f, 0.65f, 0.5f, 2),
				Content = (k, s) => k.Atmosphere(k.Highest(k.Mid, s.Radius * 0.45f), 50f, "#3A1E14", 0.45f, "#FF7043", 0.5f, "embers") },

			new MapType { Name = "swamp", Label = "Swamp", Title = "Swamp", Description = "Low land with pools, green mist and fireflies; rats guard a stash (quest)",
				Ranges = Gen(Of(TerrainPainter.Forest), IslandShapes.Marsh, 87f, 139f, 5f, 9f, 0.9f, 0.9f),
				Content = Swamp },

			new MapType { Name = "spire", Label = "Frozen spire", Title = "Frozen spire", Description = "A snowy island with one very tall peak, falling snow and a polar bear",
				Ranges = Gen(Of(TerrainPainter.Snowy), IslandShapes.Round, 78f, 104f, 95f, 120f, 0.5f, 0.5f, 1), // (one peak)
				Content = (k, s) =>
				{
					k.Atmosphere(k.Mid, 50f, "#DDE6F0", 0.35f, "#CFE0FF", 0.25f, "snow");
					k.Chest("Loot_Chest", k.Highest(k.Mid, s.Radius * 0.5f), "Climber's cache", MapKit.Loot("Treasure"));
					Vector2? den = k.Find(k.Mid, s.Radius, MapKit.Dry);
					if (den.HasValue) k.Creature("PolarBear", den.Value, 1, "Hard");
				} },

			new MapType { Name = "treasure", Label = "Treasure island", Title = "Treasure island", Description = "A map in a bottle on the beach leads to a buried treasure (quest)",
				Ranges = Gen(Of(TerrainPainter.Tropical), IslandShapes.Round, 78f, 122f, 20f, 40f, 0.5f, 0.55f),
				Content = Treasure },

			new MapType { Name = "camp", Label = "Old camp", Title = "Old camp", Description = "An abandoned camp with a notice board and supplies (quest); good as the start of a story",
				Ranges = Gen(Of(TerrainPainter.Tropical, TerrainPainter.Forest), IslandShapes.Round, 78f, 113f, 15f, 30f, 0.45f, 0.5f),
				Content = Camp },

			new MapType { Name = "sunken", Label = "Sunken island", Title = "Sunken island", Description = "An island under water: corals, sunken barrels and puffer fish, for divers", SunkenDepth = 12f,
				Ranges = Gen(Of(TerrainPainter.Tropical), IslandShapes.Round, 52f, 87f, 9f, 13f, 0.6f, 0f),
				Content = Sunken },

			new MapType { Name = "sky", Label = "Sky island", Title = "Sky island", Description = "A small island floating high in the air, with a cache", FlyingChance = 1f, FlyingMin = 45f, FlyingMax = 90f,
				Ranges = Gen(Of(TerrainPainter.Tropical, TerrainPainter.Forest), IslandShapes.Round, 39f, 70f, 12f, 25f, 0.5f, 0.6f, 2),
				Content = (k, s) => k.Chest("Loot_ChestSmall", k.Highest(k.Mid, s.Radius * 0.6f), "Sky cache", MapKit.Loot("Metal")) },

			new MapType { Name = "wreck", Label = "Wreck", Title = "Wreck", Description = "No land: an abandoned raft of Raft's blocks with barrels to loot",
				Settings = rnd => new IslandGenSettings { Seed = rnd.Next(1, 999999), Radius = 16f, Height = 2f },
				Build = Wreck },

			new MapType { Name = GhostRafts.TypeName, Label = "Ghost raft", Title = "Ghost raft", Description = "No land: an abandoned raft of Raft's blocks with loot and a note - small, medium, or large and guarded by rats and screechers (the world option Ghost rafts brings them while sailing)",
				Settings = GhostRafts.Settings, Build = GhostRafts.Build },
			new MapType { Name = TraderRaft.TypeName, Label = "Trader raft", Title = "Trader raft", Description = "No land: a merchant's raft with a hut and stalls that swap basic resources for seeds, a tree seed or a blueprint (the world option Trader raft brings them while sailing)",
				Settings = TraderRaft.Settings, Build = TraderRaft.Build },
			new MapType { Name = GhostShip.TypeName, Label = "Ghost ship", Title = "Ghost ship", Description = "No land: a glowing wreck of Raft's blocks - a deck with railings, a cabin, a mast and a sail - with chests of good loot and a crew of ghost rats and screechers (the world option Ghost ship brings one now and then at night; it sinks at dawn)",
				Settings = GhostShip.Settings, Build = GhostShip.Build },

			// The world randomizer's islands (RandomizerContent): oddities and boss lairs
			new MapType { Name = "oddity", Label = "Oddity island", Description = "A small island with something odd on it: a van, a caravan, a crashed plane, a stranded boat, a shack, a statue, rocket debris or a hut",
				Ranges = OddityRanges, Content = (k, s) => { RandomizerContent.Oddity(k, s, null); RandomizerIslands.TreesToCut(k, s, true); } },
			Oddity(0), Oddity(1), Oddity(2), Oddity(3), Oddity(4), Oddity(5), Oddity(6), Oddity(7),
			new MapType { Name = "large", Label = "Large island", Description = "A large island made like Raft's big ones (as big, trees and plants as dense, warthogs, animals to catch, puffer fish, loot boxes), with scenes from the quest islands and a cave with a guard and a hoard",
				Settings = RandomizerIslands.LargeSettings, Content = (k, s) => { RandomizerIslands.Large(k, s); RandomizerIslands.TreesToCut(k, s, false); } },
			new MapType { Name = "lair", Label = "Boss lair", Title = "Lair", Description = "A plateau where a huge, very tough beast and its guards keep a big hoard and a trophy (quest; much harder than a boss island)",
				Ranges = Gen(Of(TerrainPainter.Forest, TerrainPainter.Volcanic, TerrainPainter.Snowy, TerrainPainter.Tropical, TerrainPainter.Desert), IslandShapes.Plateau, 85f, 120f, 14f, 22f, 0.45f, 0.35f, 3, MapTypeRanges.LikeRaftLarge),
				Content = (k, s) => { RandomizerContent.Lair(k, s); RandomizerIslands.TreesToCut(k, s, false); } },
		};

		static MapType Oddity(int i)
		{
			string[] o = RandomizerContent.Oddities[i];
			return new MapType { Name = o[0], Label = o[1], Description = o[2], Ranges = OddityRanges, Content = (k, s) => { RandomizerContent.Oddity(k, s, o[0]); RandomizerIslands.TreesToCut(k, s, true); } };
		}

		static MapType Styled(string name, string label, string description, int style)
		{
			return new MapType { Name = name, Label = label, Description = description, Ranges = new MapTypeRanges { Random = true, Styles = new[] { style } } };
		}

		#region Content

		static void Archipelago(MapKit k, IslandGenSettings s)
		{
			List<Vector4> islets = IslandGenerator.Islets(s);
			if (islets.Count == 0) return;
			Vector2 main = k.Highest(k.FromMid(islets[0]), islets[0].z * 0.5f);
			Vector2? noteSpot = k.Find(main, islets[0].z * 0.6f, MapKit.Dry);
			k.Note("Note_Paper", noteSpot ?? main, "Castaway's note", "I hid what I could save on the small islands around this one. Three caches in all. Whoever finds this: they're yours.");
			string[] loot = { "Basics", "Food", "Metal" };
			int caches = 0;
			foreach (Vector4 o in islets.Skip(1).Take(3))
			{
				k.Chest("Loot_ChestSmall", k.Highest(k.FromMid(o), o.z * 0.4f), "Cache", MapKit.Loot(loot[caches % loot.Length]));
				caches++;
			}
			if (caches > 0)
				k.Quest("The castaway's caches", "Someone lived here. Look for a note on the biggest islet.", "You found every cache the castaway hid.", "",
					"read|Castaway's note|1|", "open|Cache|" + caches + "|Find the " + caches + " caches on the islets");
		}

		static void Stacks(MapKit k, IslandGenSettings s)
		{
			List<Vector4> stacks = IslandGenerator.StackSpots(s);
			if (stacks.Count == 0) return;
			Vector4 tall = stacks[0];
			Vector2 top = k.Highest(k.FromMid(tall), tall.z * 0.5f);
			k.Chest("Loot_Chest", top, "Eagle's nest", MapKit.Loot("Treasure"));
			// A sign at the foot of the tallest stack
			Vector2? foot = k.Find(k.FromMid(tall), tall.z * 2f, MapKit.Beach, 3f);
			k.Note("Note_Sign", foot ?? k.FromMid(tall) + new Vector2(tall.z * 1.3f, 0f), "Warning", "Something glints on top of the tallest rock. You'll have to build your way up.");
			if (stacks.Count > 1) k.Creature("StoneBird", k.Highest(k.FromMid(stacks[1]), stacks[1].z * 0.5f), 1, "Normal", 1f, null, true, 2f);
			k.Quest("The eagle's nest", "", "Nobody has been up here for a long time.", "", "read|Warning|1|", "open|Eagle's nest|1|Reach the top of the tallest rock");
		}

		/// <summary>The boss arena's stakes (shown while the players fight), their name, and the signal of the beast defeated.</summary>
		public const string ArenaStake = "Balboa_DecorationPrefabBase_Wooden Spikes", ArenaGate = "Arena gate", BossSignal = "boss";

		static void Boss(MapKit k, IslandGenSettings s)
		{
			Vector2 c = k.Mid;
			k.Clear(c, 24f);
			// A boss arena (LM12, 2026-10-09): stepping in, stakes burst up round the arena and shut the players in with the
			// beast (for two minutes at most: a player who dies can come back); the beast defeated, they sink and the spoils show.
			// The zone fires every time while the beast lives and the stakes are down, so a fight lost re-arms the arena
			IslandObject zone = k.Zone(c, "arena", 18f);
			zone.Props[ObjectProps.ZoneRepeat] = "1";
			zone.Props[BehaviourProps.CheckKey("enter")] = "!signal|" + BossSignal + "\n" + "state|" + ArenaGate + "|hidden";
			zone.Props[BehaviourProps.EventKey("enter")] = "show|" + ArenaGate + "\n" + "message||The ground shakes... stakes burst up all round - there is no way out!" + "\n" + "wait||120" + "\n" + "hide|" + ArenaGate;
			for (int i = 0; i < 14; i++)
			{
				float a = i * Mathf.PI * 2f / 14f;
				Vector2 at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 20f;
				k.Add(ArenaStake, k.At(at), -a * Mathf.Rad2Deg + 90f, new Dictionary<string, string> { { BehaviourProps.Name, ArenaGate }, { BehaviourProps.Hidden, "1" }, { BehaviourProps.Collision, "solid" } }, 0f);
			}
			string beast = s.Style == TerrainPainter.Snowy ? "PolarBear" : "Bear";
			IslandObject boss = k.Creature(beast, c + new Vector2(8f, 0f), 1, "Boss", 1.6f, "arena", false);
			boss.Props[BehaviourProps.EventKey("defeat")] = "hide|" + ArenaGate + "\n" + "show|Spoils" + "\n" + "signal||" + BossSignal + "\n" + "message||The beast falls. The stakes sink back into the ground.";
			// Its second phase (RF5): at half its health it roars for two more of its kind and gets faster
			boss.Props[BossPhases.Share] = "0.5";
			boss.Props[BossPhases.Summon] = "2";
			boss.Props[BossPhases.Speed] = "1.3";
			boss.Props[BehaviourProps.EventKey(BossPhases.Event)] = "message||The beast roars in fury - two more answer its call!";
			IslandObject spoils = k.Chest("Loot_ChestLarge", c - new Vector2(4f, 0f), "Spoils", MapKit.Loot("Treasure"));
			spoils.Props[BehaviourProps.Name] = "Spoils";
			spoils.Props[BehaviourProps.Hidden] = "1";
			k.Quest("The beast of the plateau", "Climb the ramp to the top of the plateau.", "The beast is defeated. Its spoils are yours.", "",
				"reach|arena|1|Climb to the top of the plateau", "kill|" + (beast == "Bear" ? "Bear" : "Polar bear") + "|1|Defeat the beast", "open|Spoils|1|");
		}

		static void Swamp(MapKit k, IslandGenSettings s)
		{
			k.Atmosphere(k.Mid, 50f, "#4B5A3C", 0.55f, "#A8C890", 0.35f, "fireflies");
			Vector2? stash = k.Find(k.Mid, s.Radius * 0.6f, MapKit.Dry);
			if (!stash.HasValue) return;
			k.Chest("Loot_Crate", stash.Value, "Swamp stash", MapKit.Loot("Food"));
			for (int i = 0; i < 2; i++)
			{
				Vector2? nest = k.Find(stash.Value, 25f, MapKit.Dry, 6f);
				if (nest.HasValue) k.Creature("Rat", nest.Value, 3);
			}
			k.Quest("Rats in the reeds", "Something scurries between the pools.", "The swamp is quiet now.", "", "kill|Rat|4|Chase off the rats", "open|Swamp stash|1|");
		}

		static void Treasure(MapKit k, IslandGenSettings s)
		{
			Vector2 x = k.Highest(k.Mid, s.Radius * 0.7f);
			k.Zone(x, "x", 6f, "X marks the spot!");
			k.Chest("Loot_Chest", x, "Treasure", MapKit.Loot("Treasure"));
			Vector2? beach = k.Find(k.Mid, s.Radius * 1.3f, MapKit.Beach);
			k.Note("Note_Bottle", beach ?? k.Mid, "Treasure map", "X marks the spot. Climb to where this island is highest, and dig.");
			k.Quest("Treasure hunt", "A bottle glints on the beach.", "The treasure is yours!", "", "read|Treasure map|1|Find the map on the beach", "reach|x|1|Find the X", "open|Treasure|1|");
		}

		static void Camp(MapKit k, IslandGenSettings s)
		{
			Vector2? spot = k.Find(k.Mid, s.Radius, (above, slope) => above > 2f && above < 10f && slope < 10f, 1f);
			Vector2 c = spot ?? k.Highest(k.Mid, 10f);
			k.Clear(c, 9f);
			k.Add("Placeable_Lantern_FireBasket", k.At(c), 0f);
			k.Add("Placeable_Bed_Hammock", k.At(c + new Vector2(3.5f, 1f)), 90f);
			k.Add("Placeable_Flag_02", k.At(c + new Vector2(-3f, 3f)), 0f);
			k.Add("Placeable_CookingStand_Food_One", k.At(c + new Vector2(-2.5f, -1.5f)), 30f);
			k.Add("TP_Moontown_TarpCrate01", k.At(c + new Vector2(1f, -4f)), 15f);
			k.Note("Note_Board", c + new Vector2(-1f, 4f), "Camp notice", "We have moved on to find more land. Take what you need from the crate, and follow the birds.");
			k.Chest("Loot_Crate", c + new Vector2(3f, -3f), "Camp supplies", MapKit.Loot("Food") + ";" + MapKit.Loot("Basics").Split(';').FirstOrDefault());
			k.Quest("The empty camp", "Someone camped here not long ago.", "Where did they go?", "", "read|Camp notice|1|Read the notice board", "open|Camp supplies|1|");
		}

		static void Sunken(MapKit k, IslandGenSettings s)
		{
			string[] corals = IslandGenerator.CoralNames().Where(n => !n.StartsWith("Reef_")).ToArray();
			int count = Mathf.Clamp(Mathf.RoundToInt(s.Radius * s.Radius / 120f), 20, 120);
			for (int i = 0; i < count && corals.Length > 0; i++)
			{
				Vector2? p = k.Find(k.Mid, s.Radius * 1.2f, (above, slope) => above > -4f && slope < 35f, 3f);
				if (p.HasValue) k.Add(corals[k.Rnd.Next(corals.Length)], k.At(p.Value), (float)k.Rnd.NextDouble() * 360f, null, 0f);
			}
			for (int i = 0; i < 3; i++)
			{
				Vector2? p = k.Find(k.Mid, s.Radius, (above, slope) => above > 0f && slope < 25f, 10f);
				if (p.HasValue) k.Chest("Loot_SunkenBarrel", p.Value, "Sunken barrel", MapKit.Loot(i == 0 ? "Treasure" : "Metal"));
			}
			k.Creature("PufferFish", k.Mid, 2, "Normal", 1f, null, true, 4f);
			k.Creature("Turtle", k.Mid + new Vector2(s.Radius * 0.5f, 0f), 1, "Normal", 1f, null, true, 3f);
		}

		/// <summary>An abandoned raft of Raft's blocks on open water, with barrels (no land).</summary>
		static IslandFile Wreck(IslandGenSettings s, string name)
		{
			var f = new IslandFile
			{
				Name = name, TerrainSize = IslandGenerator.BuildArea, HeightmapResolution = IslandGenerator.BuildResolution,
				Heights = new float[IslandGenerator.BuildResolution, IslandGenerator.BuildResolution],
			};
			var k = new MapKit(f, s.Seed);
			var rnd = new System.Random(s.Seed);
			float g = PlacementOptions.GridSize, sea = k.Sea;
			Vector3 o = new Vector3(k.Mid.x, 0f, k.Mid.y);
			float floatY = sea + PlacementOptions.FoundationFloat, deck = floatY + PlacementOptions.FoundationPlanks; // (walls, pillars and loot on the planks)
			int w = 3 + rnd.Next(3), d = 2 + rnd.Next(3);
			bool second = true;
			for (int x = 0; x < w; x++)
				for (int z = 0; z < d; z++)
					// a few foundations are gone (not those under the pillars, the loot and the ladder: they'd hang in the air)
					if (rnd.NextDouble() > 0.12 || (z == 0 && (x <= 1 || x == w - 1)) || (x == 1 && z == d - 1) || (x == w - 1 && z == 1))
						k.Add("Block_Foundation", o + new Vector3(x * g, floatY, z * g), 0f, null, 0f);
					else if (x == 1 && z == 0) second = false;
			k.Add("Block_Pillar_Wood", o + new Vector3(-g / 2, deck, -g / 2), 0f, null, 0f);
			k.Add("Block_Pillar_Wood", o + new Vector3(g * 1.5f, deck, -g / 2), 0f, null, 0f);
			k.Add("Block_Wall_Thatch", o + new Vector3(0, deck, -g / 2), 0f, null, 0f);
			// (a shelter over the first two cells - when the second is there: two more pillars at their back and a roof on all four,
			// as Raft's building puts it)
			if (rnd.NextDouble() < 0.6 && second)
			{
				k.Add("Block_Pillar_Wood", o + new Vector3(-g / 2, deck, g / 2), 0f, null, 0f);
				k.Add("Block_Pillar_Wood", o + new Vector3(g * 1.5f, deck, g / 2), 0f, null, 0f);
				RaftRoof.Hip((n, p, ry) => k.Add(n, p, ry, null, 0f), o + new Vector3(0f, deck + RaftRoof.OnWalls, 0f), 2, 1);
			}
			k.Add("Block_Ladder", o + new Vector3(g * w, deck, g), 90f, null, 0f);
			var loot = new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Basics") }, { ObjectProps.NoteTitle, "Wreckage" } };
			k.Add("Loot_Barrel", o + new Vector3(g, deck, g * (d - 1)), 0f, loot, 0f);
			k.Add("Loot_Box", o + new Vector3(g * (w - 1), deck, 0f), 20f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Metal") }, { ObjectProps.NoteTitle, "Wreckage" } }, 0f);
			return f;
		}

		#endregion

		public static MapType Get(string name)
		{
			return All.FirstOrDefault(t => t.Name.Equals((name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>A built-in type by name (not one of a .maptype file), or null.</summary>
		public static MapType BuiltIn(string name)
		{
			return All.FirstOrDefault(t => t.FromFile == null && t.Name.Equals((name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Settings and elevation for a new island of this type.</summary>
		public static IslandGenSettings Roll(MapType type, System.Random rnd, out float elevation)
		{
			IslandGenSettings s = type.Settings != null ? type.Settings(rnd) : type.Ranges != null ? type.Ranges.Roll(rnd) : IslandGenerator.RandomSettings(rnd, AllStyles);
			if (type.Sets.Count > 0) MapTypeFiles.ApplySets(s, type.Sets);
			s.Clamp();
			elevation = 0f;
			if (type.SunkenDepth > 0f) elevation = -(s.Height + type.SunkenDepth + (float)rnd.NextDouble() * 4f);
			else if (type.FlyingChance > 0f && rnd.NextDouble() < type.FlyingChance)
				elevation = type.FlyingMin + (float)rnd.NextDouble() * (type.FlyingMax - type.FlyingMin);
			return s;
		}

		/// <summary>The island file of a rolled island (the object catalog must be built).</summary>
		public static IslandFile Create(MapType type, IslandGenSettings s, float elevation, string fileName)
		{
			IslandFile file = type.Build != null ? type.Build(s, fileName) : IslandGenerator.CreateFile(s, fileName);
			file.Elevation = elevation;
			if (type.Content != null)
			{
				try { type.Content(new MapKit(file, s.Seed), s); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Content of a '" + type.Name + "' island: " + e); }
			}
			if (type.Title.Length > 0 && !file.Props.ContainsKey(IslandProps.Title)) file.Props[IslandProps.Title] = type.Title;
			// (no chest or note where players can't get without building - ROADMAP LM3)
			try { int moved = IslandReach.MoveContentWithinReach(file); if (moved > 0) Debug.Log("[CUSTOM ISLANDS] '" + fileName + "': " + moved + " chest(s)/note(s) moved to where players reach them"); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reach check of a '" + type.Name + "' island: " + e.Message); }
			return file;
		}

		/// <summary>File name for a generated island of this type: gen-&lt;type&gt;-&lt;seed&gt;.</summary>
		public static string FileName(MapType type, IslandGenSettings s)
		{
			string kind = type.Name == "random" ? TerrainPainter.StyleName(s.Style).ToLowerInvariant() : type.Name;
			return CustomIslandSpawner.GeneratedPrefix + kind + "-" + s.Seed;
		}

		/// <summary>
		/// The file name for a new generated island that is free: gen-&lt;type&gt;-&lt;seed&gt;, or with -2, -3... when an island of that
		/// name exists already (seeds repeat: a new one must never overwrite an island another world uses, or one the player
		/// made with Make and changed).
		/// </summary>
		public static string FreeFileName(MapType type, IslandGenSettings s) { return CustomIslandSpawner.FreeName(FileName(type, s)); }

		/// <summary>About how far the land of these settings reaches (for placing it before the file exists).</summary>
		public static float EstimatedRadius(IslandGenSettings s) { return Mathf.Max(20f, IslandGenerator.Reach(s)); }
	}
}
