using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CommandUndoRedo;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Settings for one generated island. The same settings (including the seed) always give the same island.
	/// Every setting has a neutral default, so islands made from only the older settings (map types, random islands
	/// while sailing) still come out as before in spirit.
	/// </summary>
	public class IslandGenSettings
	{
		public int Seed = 1;
		/// <summary>Radius of the land (m): for the round layouts the coast runs about this far from the middle; the others fit their ring, islets or stacks into it.</summary>
		public float Radius = 90f;
		/// <summary>Height of the highest point above sea level (m): the tallest peak is exactly this high.</summary>
		public float Height = 45f;
		/// <summary>0 = smooth hills, 1 = bumpy, ridged hills.</summary>
		public float Roughness = 0.5f;
		public int Peaks = 2;
		/// <summary>0 = no objects, 1 = dense: what the object sliders left at -1 follow (map types, random islands).</summary>
		public float ObjectDensity = 0.5f;
		/// <summary>Island style (TerrainPainter.Styles): ground textures and which objects are scattered. Volcanic islands get a cone with a crater.</summary>
		public int Style = TerrainPainter.Tropical;
		/// <summary>The land's layout (IslandShapes): one round island, a ring around a lagoon, several islets...</summary>
		public int Shape = IslandShapes.Round;

		// Coast and outline (0..1 unless said)
		/// <summary>How ragged the coastline is; -1 = follow Roughness.</summary>
		public float Coast = -1f;
		/// <summary>Inlets cut into the coast.</summary>
		public float Bays;
		/// <summary>Width of the sandy band between the sea and the hills.</summary>
		public float BeachWidth = 0.5f;
		/// <summary>Share of the coast that drops steeply into the sea instead of a beach.</summary>
		public float Cliffs;
		/// <summary>1 = round, up to 3 = three times as long as wide (the area stays the same); along StretchAngle (degrees).</summary>
		public float Stretch = 1f, StretchAngle;

		// Land
		/// <summary>0 = full, rounded hills; 0.3 = as generated; 1 = sharp spires over flat lowland.</summary>
		public float PeakShape = 0.3f;
		public float Terraces, Lakes, Valleys, Erosion;

		// The sea around it
		/// <summary>SeaFloorDeep: the island rises from a sea floor 160 m down like Raft's own (a shallow shelf, then a
		/// drop-off into the deep); SeaFloorShallow: a flat seabed 20 m down all around (islands before the deep sea floor).</summary>
		public int SeaFloor;
		public const int SeaFloorDeep = 0, SeaFloorShallow = 1;
		/// <summary>Width of the shallow water around the island (round layouts).</summary>
		public float Shelf = 0.35f;
		/// <summary>Deep sea floor: 0 = a long, gentle slope down (Raft's big islands), 1 = a sheer wall (its small islands).</summary>
		public float DropOff = 0.5f;
		/// <summary>SeabedSand, SeabedRocky or SeabedReef.</summary>
		public int Seabed;
		public const int SeabedSand = 0, SeabedRocky = 1, SeabedReef = 2;

		// Objects, each 0..1 (-1 = follow ObjectDensity: at 0.5 each kind Like Raft); Clusters: 0 = spread evenly, 1 = in groves and fields
		public float Trees = -1f, Bushes = -1f, Rocks = -1f, BeachThings = -1f, Harvest = -1f;
		public float Clusters = 0.35f;
		// Under water, each 0..1 where 0.5 = as dense as around Raft's islands (-1 = like Raft, or none if ObjectDensity is 0):
		// corals and sea plants, rocks, things to collect (stones, clay, sand, scrap, ores, giant clams), sunken barrels
		public float Water = -1f, SeaRocks = -1f, SeaFinds = -1f, Sunken = -1f;
		// Things to gather (GenGather, ROADMAP LM9), each 0..1 (0 = none, as before): Gather - what suits the style on the land
		// (trees to cut, fruit, berries, flowers); Shallows - Raft's sea finds just off the shore (sand, clay, stone, ores, scrap, clams, seaweed)
		public float Gather = 0f, Shallows = 0f;
		/// <summary>Kinds of things to gather switched off (GenGather.LandKeys / SeaKeys, comma separated): "" = all on, as before.</summary>
		public string GatherOff = "";
		/// <summary>How deep the finds in the shallows go (m, GenGather.MinDepth-MaxDepth): further out the deeper; 6 as before.</summary>
		public float ShallowsDepth = GenGather.DefaultDepth;

		// Content: creature spots and loot boxes
		public int Hostiles, Friendly, SeaLife;
		/// <summary>Kinds of hostile creatures (AI types, comma separated), "" = the style's own.</summary>
		public string HostileKinds = "";
		/// <summary>A creature stat preset (ObjectProps.Presets): Easy, Normal, Hard, Boss.</summary>
		public string Difficulty = "Normal";
		public int Loot;
		/// <summary>Loot tiers the boxes are rolled between (1 = planks and plastic ... 5 = titanium and batteries).</summary>
		public int LootMin = 1, LootMax = 3;
		/// <summary>Loot boxes tucked in next to trees, bushes and rocks instead of out in the open.</summary>
		public bool LootHidden;
		// (no "Levels" any more: the level up system is a world option only - New Game > World settings. An older preset's
		// "Levels=True" line is passed by, as FromText passes every name it doesn't know)
		/// <summary>Buildings on the land (GenBuildings): on or off, of which kind (mixed, huts, cabins or a themed scene), how many.</summary>
		public bool Buildings;
		public string BuildingKind = GenBuildings.Mixed;
		public int BuildingCount = 2;
		/// <summary>A cave set into the land, with a guard and a hoard (GenBuildings).</summary>
		public bool Caves;
		/// <summary>A quest made with the island: how many steps (0 = none; GenQuest).</summary>
		public int QuestSteps;
		/// <summary>Raft's features on the island (GenFeatures, ROADMAP LM12): how many, 0-10 - a cache behind vines, buried
		/// treasure, a zipline, a strongbox behind a code panel, wild beehives, dirt for the shovel; past six Raft's story
		/// machinery (LM12): a generator and a radio, an engine, a cage, a lift.</summary>
		public int Features;
		/// <summary>One of Raft's story islands rebuilt from its own pieces: a design id, "&lt;scene&gt;:any" for one of its
		/// designs at random, or "" (Remakes; the Randomize existing tab).</summary>
		public string Remake = "";
		/// <summary>One of Raft's story islands' looks the ground and pieces follow (Remakes.Looks, IslandLooks.cs: "caravan",
		/// "tangaroa"...; "" = none) - set by the Randomize existing tab's like-it and variation.</summary>
		public string Look = "";
		/// <summary>The look's original island's highest point over the sea (Caravan's mesas reach it).</summary>
		public float LookTop;

		// "Randomize existing": start from one of Raft's islands' own ground (RaftIslands) instead of a layout
		/// <summary>Scene of the Raft island whose ground is the start ("" = a layout).</summary>
		public string Source = "";
		/// <summary>Bumps added to the Raft island's land, and wobble of its coast (0..1).</summary>
		public float SourceRoughen, SourceWobble;
		public bool Mirror;
		/// <summary>LM7: under water also the other kinds Raft's islands have there (RaftUnderwater.ExtraCategoryOf), loaded
		/// from their scenes first (SeaNamesNeeded) - the Randomize existing tab. A property: not in the saved presets.</summary>
		public bool RaftSeaKinds { get; set; }

		// The land can reach well past Radius with a ragged coast and stretch; the edge of the 1000 m build area is always flat seabed
		public const float MinRadius = 8f, MaxRadius = 280f, MinHeight = 3f, MaxHeight = 160f, MaxStretch = 3f;
		/// <summary>The lowest Height a map type or a ghost raft's islet may ask for (a sandbar, 2 m): below the sliders' MinHeight on purpose.</summary>
		public const float MinTypeHeight = 2f;
		/// <summary>LM7: a variation of one of Raft's islands may be as big as the biggest (Temperance, 457 m): its terrain
		/// grows past the 1000 m build area to hold it (IslandGenerator.AreaFor) instead of the island being scaled down.</summary>
		public const float MaxSourceRadius = 480f;
		public const int MaxPeaks = 5, MaxCreatureSpots = 24, MaxLoot = 40;

		public float CoastAmount { get { return Coast < 0f ? Roughness : Coast; } }

		public void Clamp()
		{
			if (Source == null) Source = "";
			// (map types may go smaller than the editor's sliders: a sandbar, a low atoll; a Raft island's variation as big as it is, LM7)
			Radius = Mathf.Clamp(Radius, MinRadius, Source.Length > 0 ? MaxSourceRadius : MaxRadius);
			Height = Mathf.Clamp(Height, MinTypeHeight, MaxHeight);
			Shape = Mathf.Clamp(Shape, 0, IslandShapes.Names.Length - 1);
			Roughness = Mathf.Clamp01(Roughness);
			Peaks = Mathf.Clamp(Peaks, 1, MaxPeaks);
			ObjectDensity = Mathf.Clamp01(ObjectDensity);
			Style = Mathf.Clamp(Style, 0, TerrainPainter.Styles.Length - 1);
			if (Coast >= 0f) Coast = Mathf.Clamp01(Coast);
			Bays = Mathf.Clamp01(Bays); BeachWidth = Mathf.Clamp01(BeachWidth); Cliffs = Mathf.Clamp01(Cliffs);
			Stretch = Mathf.Clamp(Stretch, 1f, MaxStretch); StretchAngle = Mathf.Repeat(StretchAngle, 180f);
			PeakShape = Mathf.Clamp01(PeakShape); Terraces = Mathf.Clamp01(Terraces); Lakes = Mathf.Clamp01(Lakes); Valleys = Mathf.Clamp01(Valleys); Erosion = Mathf.Clamp01(Erosion);
			Shelf = Mathf.Clamp01(Shelf); Seabed = Mathf.Clamp(Seabed, 0, 2);
			SeaFloor = Mathf.Clamp(SeaFloor, 0, 1); DropOff = Mathf.Clamp01(DropOff);
			Clusters = Mathf.Clamp01(Clusters); Gather = Mathf.Clamp01(Gather); Shallows = Mathf.Clamp01(Shallows);
			GatherOff = GenGather.Clean(GatherOff); ShallowsDepth = Mathf.Clamp(ShallowsDepth, GenGather.MinDepth, GenGather.MaxDepth);
			Hostiles = Mathf.Clamp(Hostiles, 0, MaxCreatureSpots); Friendly = Mathf.Clamp(Friendly, 0, MaxCreatureSpots); SeaLife = Mathf.Clamp(SeaLife, 0, MaxCreatureSpots);
			Loot = Mathf.Clamp(Loot, 0, MaxLoot);
			// (a preset or a map type's set line may ask for more than the sliders give: kept in range here, not only where used)
			BuildingCount = Mathf.Clamp(BuildingCount, 0, GenBuildings.MaxCount); QuestSteps = Mathf.Clamp(QuestSteps, 0, GenQuest.MaxSteps); Features = Mathf.Clamp(Features, 0, GenFeatures.Max);
			LootMin = Mathf.Clamp(LootMin, 1, 5); LootMax = Mathf.Clamp(LootMax, LootMin, 5);
			SourceRoughen = Mathf.Clamp01(SourceRoughen); SourceWobble = Mathf.Clamp01(SourceWobble);
			if (HostileKinds == null) HostileKinds = "";
			if (Difficulty == null || !ObjectProps.Presets.Any(p => p.Key == Difficulty)) Difficulty = "Normal";
			if (Source == null) Source = "";
		}

		public IslandGenSettings Copy() { return (IslandGenSettings)MemberwiseClone(); }

		/// <summary>
		/// The generator's first settings (and its Defaults button): an island like Raft's own small ones - the "Raft small
		/// island" layout, about 32-60 m across and 8-15 m high, an uneven coast with a bay or two, a beach all round (no
		/// cliffs: players get on from the raft by wading or a jump), and objects as dense as on Raft's small islands.
		/// Plain new IslandGenSettings() stays as it was (presets, map types and older files start from it).
		/// </summary>
		public static IslandGenSettings RaftLike(int seed = 1)
		{
			var s = new IslandGenSettings { Seed = seed, Shape = IslandShapes.RaftSmall };
			s.Radius = Mathf.Clamp(RaftIslands.SmallRadius * 1.15f, 14f, 24f);
			s.Height = Mathf.Clamp(RaftIslands.SmallTop, 8f, 15f);
			s.Peaks = 1;
			s.Roughness = 0.35f;
			s.Coast = 0.5f;
			s.Bays = 0.3f;
			s.BeachWidth = 0.5f;
			s.Cliffs = 0f;
			RandomizerIslands.LikeRaftSmall(s);
			return s;
		}

		/// <summary>An object slider's value: its own, or (at -1) one that follows ObjectDensity.</summary>
		public float Amount(float own) { return own >= 0f ? Mathf.Clamp01(own) : ObjectDensity * 0.45f; }

		/// <summary>An under-water slider's value: its own, or (at -1) as around Raft's islands (none on an island without objects).</summary>
		public float AmountSea(float own) { return own >= 0f ? Mathf.Clamp01(own) : ObjectDensity > 0.01f ? 0.5f : 0f; }

		/// <summary>Terrain height (m above its base) of the sea for these settings.</summary>
		public float WaterLevel { get { return SeaFloor == SeaFloorShallow ? IslandFile.DefaultWaterLevel : IslandFile.DeepWaterLevel; } }
		public bool Deep { get { return SeaFloor != SeaFloorShallow; } }

		static FieldInfo[] Fields { get { return typeof(IslandGenSettings).GetFields(BindingFlags.Public | BindingFlags.Instance); } }

		/// <summary>"name=value" lines (the generator's saved presets).</summary>
		public string ToText()
		{
			var sb = new StringBuilder();
			foreach (FieldInfo f in Fields)
			{
				object v = f.GetValue(this);
				sb.Append(f.Name).Append('=').Append(v is float ? ((float)v).ToString("0.###", CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n');
			}
			return sb.ToString();
		}

		/// <summary>Settings from ToText's lines; unknown or missing names keep their defaults.</summary>
		public static IslandGenSettings FromText(string text)
		{
			var s = new IslandGenSettings();
			foreach (string raw in (text ?? "").Split('\n'))
			{
				string line = raw.Trim();
				int eq = line.IndexOf('=');
				if (eq <= 0) continue;
				FieldInfo f = typeof(IslandGenSettings).GetField(line.Substring(0, eq), BindingFlags.Public | BindingFlags.Instance);
				if (f == null) continue;
				string v = line.Substring(eq + 1);
				try
				{
					if (f.FieldType == typeof(float)) { float n = float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture); if (!float.IsNaN(n) && !float.IsInfinity(n)) f.SetValue(s, n); } // (NaN slips past Clamp - CB12)
					else if (f.FieldType == typeof(int)) f.SetValue(s, int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture));
					else if (f.FieldType == typeof(bool)) f.SetValue(s, bool.Parse(v));
					else if (f.FieldType == typeof(string)) f.SetValue(s, v);
				}
				catch { }
			}
			s.Clamp();
			return s;
		}
	}

	/// <summary>Layouts of generated land.</summary>
	public static class IslandShapes
	{
		/// <summary>Round: one island with peaks. Atoll: a ring of low land around a shallow lagoon. Archipelago: several
		/// islets on a shallow shelf. Stacks: steep rock pillars rising from shallow water. Plateau: a flat-topped mesa
		/// with cliffs and one ramp up. Marsh: low, bumpy land with pools of water. Crescent: a curved island around a
		/// sheltered bay (like Raft's "Cresent"). Twin peaks: two tall peaks with a saddle between (like Raft's). Raft small
		/// island / Raft large island: the outline and ground of one of Raft's own small (or big) islands, picked by the seed,
		/// turned, mirrored and wobbled, with a beach all round where cliffs aren't asked for (the first Generate's layout).</summary>
		public const int Round = 0, Atoll = 1, Archipelago = 2, Stacks = 3, Plateau = 4, Marsh = 5, Crescent = 6, TwinPeaks = 7, RaftSmall = 8, RaftLarge = 9;
		public static readonly string[] Names = { "Round", "Atoll", "Archipelago", "Sea stacks", "Plateau", "Marsh", "Crescent", "Twin peaks", "Raft small island", "Raft large island" };
		/// <summary>The order the generator's Layout list shows them in: Raft's own island shapes first.</summary>
		public static readonly int[] Order = { RaftSmall, RaftLarge, Round, Atoll, Archipelago, Stacks, Plateau, Marsh, Crescent, TwinPeaks };
		/// <summary>True for the layouts made from Raft's own islands' ground.</summary>
		public static bool FromRaft(int shape) { return shape == RaftSmall || shape == RaftLarge; }
		public static readonly string[] Hints =
		{
			"One island with hills and peaks", "A ring of low land around a shallow lagoon", "Several islets on a shallow shelf",
			"Steep rock pillars rising from shallow water", "A flat-topped mesa with cliffs and one ramp up", "Low, bumpy land with pools of water",
			"A curved island around a sheltered bay", "Two tall peaks with a saddle between them",
			"Shaped like one of Raft's own small islands (the seed picks which): an uneven outline, a beach all round, a hill in the middle",
			"Shaped like one of Raft's own big islands (the seed picks which), with a beach where there are no cliffs",
		};
	}

	/// <summary>Undo step for changing the island style (part of generating an island).</summary>
	public class StyleCommand : ICommand
	{
		readonly int before, after;
		public StyleCommand(int before, int after) { this.before = before; this.after = after; }
		public void Execute() { DynamicIslands.SetEditorStyle(after); }
		public void UnExecute() { DynamicIslands.SetEditorStyle(before); }
	}

	/// <summary>Undo step for changing the sea level of the island being edited (generating on a deep or shallow sea floor).</summary>
	public class WaterLevelCommand : ICommand
	{
		readonly float before, after;
		public WaterLevelCommand(float before, float after) { this.before = before; this.after = after; }
		public void Execute() { DynamicIslands.SetEditorWaterLevel(after); }
		public void UnExecute() { DynamicIslands.SetEditorWaterLevel(before); }
	}

	/// <summary>What one generation made (the window's status line and the tests).</summary>
	public class GenReport
	{
		public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
		public int Creatures, Chests;
		/// <summary>Objects the planner wanted before the cap (MaxObjects) scaled them down.</summary>
		public int Wanted;
		public float LandArea, Top, Seconds;
		public float LandLength, LandWidth;
		/// <summary>The buildings and caves put on it (GenBuildings), or why not.</summary>
		public List<string> Built = new List<string>();

		public int Nature { get { return Counts.Values.Sum(); } }
		public int Count(string key) { int n; return Counts.TryGetValue(key, out n) ? n : 0; }

		public string Describe()
		{
			var parts = IslandGenerator.Categories.Where(c => Count(c) > 0).Select(c => Count(c) + " " + IslandGenerator.CategoryLabel(c)).ToList();
			if (Creatures > 0) parts.Add(Creatures + " creature spot" + (Creatures == 1 ? "" : "s"));
			if (Chests > 0) parts.Add(Chests + " loot box" + (Chests == 1 ? "" : "es"));
			parts.AddRange(Built);
			return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "no objects";
		}
	}

	/// <summary>
	/// Raft's story islands rebuilt in a new way from their own pieces (the user, 2026-10-03: "the radio tower graphics can
	/// become an oil rig, a different kind of radio tower or a lighthouse in construction - rebuild something similar, not
	/// just shuffle things around"). A design builds a new structure of the island's kit on the generated ground, different
	/// by the seed (how high, how many storeys and decks, which walls have windows, which way it faces). The Radio Tower's
	/// kit (floors 6 x 9 m with their pivot on their +x +z corner, walls 3 m high and 3 m wide, railings 1.5 m, thick
	/// pillars 6 m - placed as the library's lib_rt recipes place them): a radio tower, an oil rig, a lighthouse under
	/// construction.
	/// </summary>
	public static partial class Remakes
	{
		public class Design
		{
			public string Id, Label, Hint, Scene;
			public Func<MapKit, IslandGenSettings, string> Build;
			/// <summary>The pieces it uses (loaded before generating); null = the Radio Tower's kit.</summary>
			public string[] Kit;
		}

		public const string Any = ":any";

		static readonly string[] RtKit = { "RT_Floor", "RT_Wall1", "RT_WallWindow1", "RT_WallWindow2", "RT_WallDoor1", "RT_Fence", "RT_PillarThick", "RT_SatteliteDisc", "RT_WindMill",
			"RT_RoofLamp", "RT_Floodlight", "RT_PowerBox", "RT_CommRadio", "RT_RadarScreen", "RT_PlasticBoat", "LandmarkLadder_6m", "Table Office", "Chair Office", "Locker" };

		static Design[] all;
		/// <summary>Every design: the Radio Tower's, then the other story islands' (StoryRemakes.cs, ROADMAP CW1).</summary>
		public static Design[] All { get { return all ?? (all = Radio.Concat(StoryDesigns()).ToArray()); } }

		static readonly Design[] Radio =
		{
			new Design { Id = "rt.tower", Scene = "Landmark_Radar", Label = "A radio tower", Build = Tower,
				Hint = "A new radio tower of the Radio Tower's pieces: a station on the ground, legs 6 to 18 m up to the radio room, a roof with the dish, the windmill and a mast, ladders all the way" },
			new Design { Id = "rt.rig", Scene = "Landmark_Radar", Label = "An oil rig", Build = Rig,
				Hint = "An oil rig over the shallow sea off the island: four of the tower's floors on legs down to the sea floor, railings, a control room with the dish, a flare stack, ladders down to a boat" },
			new Design { Id = "rt.lighthouse", Scene = "Landmark_Radar", Label = "A lighthouse under construction", Build = Lighthouse,
				Hint = "A lighthouse being built on a headland: 4 to 6 storeys of the tower's walls and floors, the top ones unfinished, poles around it, the lamp still waiting by the door (or up, when it's finished)" },
		};

		/// <summary>The designs for one of Raft's islands (its scene name).</summary>
		public static List<Design> For(string scene) { scene = FamilyScene(scene); return All.Where(d => SceneIs(scene, d.Scene)).ToList(); }

		/// <summary>True when the island's scene name holds the design's scene as a whole name: "55#Landmark_Temperance#"
		/// is Temperance, "57#Landmark_TemperanceSmall#1" (one of its islets) is not.</summary>
		static bool SceneIs(string scene, string design)
		{
			scene = scene ?? "";
			for (int i = scene.IndexOf(design, StringComparison.OrdinalIgnoreCase); i >= 0; i = scene.IndexOf(design, i + 1, StringComparison.OrdinalIgnoreCase))
			{
				int end = i + design.Length;
				if (end >= scene.Length || !char.IsLetterOrDigit(scene[end])) return true;
			}
			return false;
		}

		/// <summary>The objects of Raft's islands a remake uses (loaded on demand before generating).</summary>
		public static List<string> NeededNames(IslandGenSettings s)
		{
			if (string.IsNullOrEmpty(s.Remake)) return new List<string>();
			List<Design> ds = s.Remake.EndsWith(Any) ? For(s.Remake.Substring(0, s.Remake.Length - Any.Length)) : All.Where(d => d.Id == s.Remake).ToList();
			return ds.SelectMany(d => d.Kit ?? RtKit).Distinct().ToList();
		}

		/// <summary>Builds the settings' design (or one of the island's at random) on the kit's file; what it built, or why not.</summary>
		public static string Build(MapKit k, IslandGenSettings s)
		{
			if (string.IsNullOrEmpty(s.Remake)) return null;
			Design d;
			if (s.Remake.EndsWith(Any))
			{
				List<Design> list = For(s.Remake.Substring(0, s.Remake.Length - Any.Length));
				d = list.Count > 0 ? list[k.Rnd.Next(list.Count)] : null;
			}
			else d = All.FirstOrDefault(x => x.Id == s.Remake);
			if (d == null) return "no design '" + s.Remake + "'";
			return d.Build(k, s) ?? "no place for " + d.Label.ToLowerInvariant() + " on this ground";
		}

		#region The kit

		/// <summary>A frame: its origin (terrain-local x z), its turn (a multiple of 90) and its floor (terrain height).</summary>
		struct Frame
		{
			public Vector2 O;
			public float Yaw, Y;
			public Frame(Vector2 o, float yaw, float y) { O = o; Yaw = yaw; Y = y; }
			public Frame Up(float dy) { return new Frame(O, Yaw, Y + dy); }
			public Vector2 At(float x, float z) { Vector3 v = Quaternion.Euler(0f, Yaw, 0f) * new Vector3(x, 0f, z); return new Vector2(O.x + v.x, O.y + v.z); }
		}

		static IslandObject P(MapKit k, Frame f, string name, float x, float z, float dy = 0f, float yaw = 0f, Dictionary<string, string> props = null)
		{
			Vector2 w = f.At(x, z);
			return k.Add(name, new Vector3(w.x, f.Y + dy, w.y), f.Yaw + yaw, props, 0f);
		}

		/// <summary>A thing standing on what is under it (a floor dy over the frame's): its own bottom there, as a recipe's
		/// "sit" puts it - a table's mesh reaches 0.54 m under its pivot: set by its pivot it sank into the floor, and the radio
		/// on it hung 0.54 m over it.</summary>
		static IslandObject Sit(MapKit k, Frame f, string name, float x, float z, float dy = 0f, float yaw = 0f, Dictionary<string, string> props = null)
		{
			Bounds b;
			return P(k, f, name, x, z, dy - (GenBuildings.Measured(name, out b) ? b.min.y : 0f), yaw, props);
		}

		/// <summary>A frame whose 6 x 9 floor (x -6..0, z -4.5..4.5) has its middle at c.</summary>
		static Frame Around(Vector2 c, float yaw, float y) { Vector3 v = Quaternion.Euler(0f, yaw, 0f) * new Vector3(-3f, 0f, 0f); return new Frame(c - new Vector2(v.x, v.z), yaw, y); }

		static readonly string[] Slots = { "b1", "b2", "f1", "f2", "l1", "l2", "l3", "r1", "r2", "r3" };

		/// <summary>One storey (lib_rt's rt_storey): the floor and ten wall slots (a name, or null for an opening).</summary>
		static void Storey(MapKit k, Frame f, Func<string, string> slot, bool floor = true)
		{
			if (floor) P(k, f, "RT_Floor", 0f, 4.5f);
			var at = new Dictionary<string, Vector3>
			{
				{ "b1", new Vector3(-3f, 4.5f, 0f) }, { "b2", new Vector3(0f, 4.5f, 0f) }, { "f1", new Vector3(-3f, -4.5f, 0f) }, { "f2", new Vector3(0f, -4.5f, 0f) },
				{ "l1", new Vector3(-6.04f, -4.5f, 90f) }, { "l2", new Vector3(-6.04f, -1.5f, 90f) }, { "l3", new Vector3(-6.04f, 1.5f, 90f) },
				{ "r1", new Vector3(0f, -4.5f, 90f) }, { "r2", new Vector3(0f, -1.5f, 90f) }, { "r3", new Vector3(0f, 1.5f, 90f) },
			};
			foreach (string sl in Slots)
			{
				string piece = slot(sl);
				if (piece != null) P(k, f, piece, at[sl].x, at[sl].y, 0f, at[sl].z);
			}
		}

		/// <summary>Railings round a 6 x 9 floor (lib_rt's rt_railing), each side on or off; gap: leaves out the front's pieces over x gap-1.5..gap+1.5.</summary>
		static void Railing(MapKit k, Frame f, float dy, bool back = true, bool front = true, bool left = true, bool right = true, float gap = float.NaN)
		{
			foreach (float x in new[] { -4.5f, -3f, -1.5f, 0f })
			{
				if (back) P(k, f, "RT_Fence", x, 4.5f, dy);
				bool inGap = !float.IsNaN(gap) && x - 1.5f < gap + 0.9f && x > gap - 0.9f;
				if (front && !inGap) P(k, f, "RT_Fence", x, -4.5f, dy);
			}
			foreach (float z in new[] { -4.5f, -3f, -1.5f, 0f, 1.5f, 3f })
			{
				if (left) P(k, f, "RT_Fence", -6f, z, dy, 90f);
				if (right) P(k, f, "RT_Fence", 0f, z, dy, 90f);
			}
		}

		static readonly Vector2[] LegSpots = { new Vector2(-5.9f, -4.4f), new Vector2(-5.9f, 4.4f), new Vector2(-0.1f, -4.4f), new Vector2(-0.1f, 4.4f) };

		/// <summary>Thick pillars under a 6 x 9 floor (lib_rt's rt_legs), 6 m each, from the floor down until one reaches the ground.</summary>
		static int Legs(MapKit k, Frame f, HashSet<Vector2> done = null)
		{
			int n = 0;
			foreach (Vector2 l in LegSpots)
			{
				Vector2 w = f.At(l.x, l.y);
				if (done != null) { Vector2 key = new Vector2(Mathf.Round(w.x * 2f), Mathf.Round(w.y * 2f)); if (done.Contains(key)) continue; done.Add(key); }
				float ground = k.Ground(w);
				for (int i = 1; i <= 12 && f.Y - 6f * (i - 1) > ground + 0.05f; i++) { P(k, f, "RT_PillarThick", l.x, l.y, -6f * i); n++; }
			}
			return n;
		}

		/// <summary>Ladders (Raft's 6 m one) against a floor's front edge at x, from a bottom height up to the top (terrain heights).</summary>
		static void Ladders(MapKit k, Frame f, float x, float bottom, float top)
		{
			for (float b = top - 6f; b > bottom - 6f + 0.01f; b -= 6f) P(k, f, "LandmarkLadder_6m", x, -4.62f, b - f.Y);
		}

		/// <summary>Levels the ground under a frame's floor (x0..x1, z0..z1) to a height, blended into the land over blend metres.</summary>
		static void Level(MapKit k, Frame f, float x0, float x1, float z0, float z1, float height, float blend)
		{
			IslandFile file = k.File;
			int res = file.HeightmapResolution;
			float step = file.TerrainSize.x / (res - 1);
			Quaternion back = Quaternion.Euler(0f, -f.Yaw, 0f);
			float reach = Mathf.Max(x1 - x0, z1 - z0) + blend + 2f;
			Vector2 mid = f.At((x0 + x1) / 2f, (z0 + z1) / 2f);
			// (what stands in the blend keeps its height over the ground as it changes, as at the cave - a tree past the
			// clearing floated 3.8 m over the lowered ground, CIGroundTypes)
			var around = file.Objects.Where(o => Mathf.Abs(o.Position.x - mid.x) < reach + step && Mathf.Abs(o.Position.z - mid.y) < reach + step).Select(o => new KeyValuePair<IslandObject, float>(o, k.Ground(new Vector2(o.Position.x, o.Position.z)))).ToList();
			float[,] was = (float[,])file.Heights.Clone();
			for (int zi = Mathf.Max(0, Mathf.FloorToInt((mid.y - reach) / step)); zi <= Mathf.Min(res - 1, Mathf.CeilToInt((mid.y + reach) / step)); zi++)
				for (int xi = Mathf.Max(0, Mathf.FloorToInt((mid.x - reach) / step)); xi <= Mathf.Min(res - 1, Mathf.CeilToInt((mid.x + reach) / step)); xi++)
				{
					Vector3 l = back * new Vector3(xi * step - f.O.x, 0f, zi * step - f.O.y);
					float outside = Mathf.Max(Mathf.Max(x0 - l.x, l.x - x1), Mathf.Max(z0 - l.z, l.z - z1));
					float weight = outside <= 0f ? 1f : outside < blend ? 1f - outside / blend : 0f;
					if (weight <= 0f) continue;
					float h = file.Heights[zi, xi] * file.TerrainSize.y;
					file.Heights[zi, xi] = Mathf.Lerp(h, height, weight * weight * (3f - 2f * weight)) / file.TerrainSize.y;
				}
			// (nature moves by the lowest change under it, and goes where the ground changed unevenly under it, as at the cave:
			// by its pivot's change alone, grass on a steep blend stood 1.1-1.7 m in the air, CIGroundIslands 2026-10-20)
			foreach (var kv in around)
			{
				Vector2 p = new Vector2(kv.Key.Position.x, kv.Key.Position.z);
				float d = k.Ground(p) - kv.Value;
				if (kv.Key.Props == null || kv.Key.Props.Count == 0)
				{
					float most = d;
					foreach (Vector2 o in new[] { new Vector2(1.5f, 0f), new Vector2(-1.5f, 0f), new Vector2(0f, 1.5f), new Vector2(0f, -1.5f) })
					{
						float ch = k.Ground(p + o) - IslandGenerator.SampleHeights(was, res, step, p.x + o.x, p.y + o.y) * file.TerrainSize.y;
						d = Mathf.Min(d, ch);
						most = Mathf.Max(most, ch);
					}
					if (most - d > 1f) { file.Objects.Remove(kv.Key); continue; }
				}
				kv.Key.Position.y += d;
			}
		}

		static string Wall(System.Random r, int windows) { int v = r.Next(10); return v < windows ? (r.NextDouble() < 0.5 ? "RT_WallWindow1" : "RT_WallWindow2") : "RT_Wall1"; }

		static float Turn(System.Random r) { return 90f * r.Next(4); }

		/// <summary>The highest ground of a 6 x 9 floor's corners and middle (terrain height).</summary>
		static float TopUnder(MapKit k, Frame f)
		{
			float top = float.MinValue;
			foreach (Vector2 p in new[] { new Vector2(-6f, -4.5f), new Vector2(-6f, 4.5f), new Vector2(0f, -4.5f), new Vector2(0f, 4.5f), new Vector2(-3f, 0f) }) top = Mathf.Max(top, k.Ground(f.At(p.x, p.y)));
			return top;
		}

		/// <summary>A radio room's things on its floor: the radio on its table, a chair, a locker with loot, the radar screen, a lamp.</summary>
		static void RadioRoom(MapKit k, Frame f, string title)
		{
			Bounds table;
			Sit(k, f, "Table Office", -3f, 3.4f);
			// (the radio on the table's top)
			Sit(k, f, "RT_CommRadio", -3.3f, 3.8f, GenBuildings.Measured("Table Office", out table) ? table.size.y : 1.08f);
			Sit(k, f, "Chair Office", -3f, 2.6f, 0f, 170f);
			Sit(k, f, "Locker", -0.6f, 2.5f, 0f, 270f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Metal") }, { ObjectProps.NoteTitle, title } });
			Sit(k, f, "RT_RadarScreen", -5.45f, -0.5f, 0f, 90f);
			P(k, f, "RT_RoofLamp", -3f, 0f, 2.95f);
		}

		#endregion

		#region The Radio Tower's designs

		static string Tower(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			Vector2 c = k.Highest(k.Mid, s.Radius * 0.5f);
			if (k.Ground(c) - k.Sea < 1.5f) return null;
			Frame f = Around(c, Turn(r), 0f);
			float y0 = TopUnder(k, f) + 0.05f;
			f = new Frame(f.O, f.Yaw, y0);
			Level(k, f, -7f, 1f, -6f, 6f, y0 - 0.05f, 5f);
			k.Clear(c, 14f);
			int legs = 1 + r.Next(3);
			// The station on the ground: a door at the front left, windows here and there
			Storey(k, f, sl => sl == "f1" ? "RT_WallDoor1" : Wall(r, 4));
			Sit(k, f, "RT_PowerBox", -5.4f, 2.6f, 0f, 90f);
			Sit(k, f, "Locker", -0.6f, 3.4f, 0f, 270f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Basics") }, { ObjectProps.NoteTitle, "Station locker" } });
			// Its roof, railed but open where the ladder comes up
			Frame roof = f.Up(3f);
			P(k, roof, "RT_Floor", 0f, 4.5f);
			Railing(k, roof, 0f, gap: -1.5f);
			// Legs up to the radio room, a ladder from the ground all the way
			for (int i = 0; i < legs; i++) foreach (Vector2 l in LegSpots) P(k, roof, "RT_PillarThick", l.x, l.y, 6f * i);
			Frame room = roof.Up(6f * legs);
			Ladders(k, f, -1.5f, y0, room.Y);
			Storey(k, room, sl => sl == "f2" ? "RT_WallDoor1" : sl.StartsWith("l") || sl.StartsWith("r") ? Wall(r, 7) : Wall(r, 5));
			RadioRoom(k, room, "Radio locker");
			// The roof: the dish, the windmill, a mast with a lamp
			Frame top = room.Up(3f);
			P(k, top, "RT_Floor", 0f, 4.5f);
			Railing(k, top, 0f);
			Sit(k, top, "RT_SatteliteDisc", -4.6f, -2.5f, 0f, 200f);
			Sit(k, top, "RT_WindMill", -1.2f, 3.2f, 0f, Turn(r));
			P(k, top, "RT_PillarThick", -3f, 1.5f);
			P(k, top, "RT_RoofLamp", -3f, 1.5f, 6f);
			Sit(k, top, "RT_Floodlight", -5.5f, 4f, 0f, 315f);
			return "a radio tower " + Mathf.RoundToInt(3f + 6f * legs + 3f) + " m high";
		}

		static string Rig(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			// (the shallow sea off the island, out to the shelf's edge - the whole rig and 6 m round it over water at least 4 m
			// deep: one built where the coast was close stood half in the island's slope)
			float yaw = 0f;
			Vector2? site = null;
			for (int tries = 0; tries < 90 && !site.HasValue; tries++)
			{
				Vector2? c = k.Find(k.Mid, s.Radius + (tries < 30 ? 80f : 170f), (above, slope) => above < -5f && above > (tries < 30 ? -25f : -40f), 0f);
				if (!c.HasValue) continue;
				yaw = Turn(r);
				Quaternion q = Quaternion.Euler(0f, yaw, 0f);
				bool clear = true;
				for (float x = -12f; x <= 12f && clear; x += 3f)
					for (float z = -15f; z <= 15f && clear; z += 3f)
					{
						Vector3 v = q * new Vector3(x, 0f, z);
						float g = k.Ground(c.Value + new Vector2(v.x, v.z));
						clear = g < k.Sea - 4f && g > k.Sea - 60f; // (and not past a drop-off: the legs, 12 of 6 m from the deck, would end above the sea floor)
					}
				if (clear) site = c;
			}
			if (!site.HasValue) return null;
			float deck = k.Sea + 6f + r.Next(3);
			// Four floors: 12 x 18 m, x -6..6, z -4.5..13.5 around the frame's origin; its middle on the site
			Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 4.5f);
			var rig = new Frame(site.Value - new Vector2(off.x, off.z), yaw, deck);
			var legsDone = new HashSet<Vector2>();
			int legs = 0;
			var floors = new[] { new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(0f, 9f), new Vector2(6f, 9f) };
			foreach (Vector2 o in floors)
			{
				var fl = new Frame(rig.At(o.x, o.y), yaw, deck);
				P(k, fl, "RT_Floor", 0f, 4.5f);
				legs += Legs(k, fl, legsDone);
				// (railings on the rig's outer edges only)
				Railing(k, fl, 0f, back: o.y > 0f, front: o.y == 0f, left: o.x == 0f, right: o.x > 0f, gap: o.x == 0f && o.y == 0f ? -1.5f : float.NaN);
			}
			// The control room on the back right floor, its door towards the deck; the dish on its roof
			var control = new Frame(rig.At(6f, 9f), yaw, deck);
			Storey(k, control, sl => sl == "l2" ? "RT_WallDoor1" : sl.StartsWith("b") || sl.StartsWith("r") ? Wall(r, 6) : Wall(r, 3), floor: false);
			RadioRoom(k, control, "Rig locker");
			P(k, control.Up(3f), "RT_Floor", 0f, 4.5f);
			Railing(k, control.Up(3f), 0f);
			Sit(k, control.Up(3f), "RT_SatteliteDisc", -3f, 0f, 0f, Turn(r));
			// The flare stack at the front right corner, lit at the top; the windmill; the power box
			var front = new Frame(rig.At(6f, 0f), yaw, deck);
			int stack = 2 + r.Next(2);
			for (int i = 0; i < stack; i++) P(k, front, "RT_PillarThick", -0.6f, -3.8f, 6f * i);
			Sit(k, front, "RT_Floodlight", -0.6f, -3.8f, 6f * stack, Turn(r));
			Sit(k, front, "RT_WindMill", -4f, 2f, 0f, Turn(r));
			Sit(k, rig, "RT_PowerBox", -5.3f, 7f, 0f, 90f);
			Sit(k, rig, "Loot_Crate", -2f, 11f, 0f, 0f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Metal") }, { ObjectProps.NoteTitle, "Rig supplies" } });
			// Down to the water: ladders at the front left, the boat at their foot
			Ladders(k, rig, -1.5f, k.Sea - 6f, deck);
			P(k, rig, "RT_PlasticBoat", -1.5f, -6.5f, k.Sea - 0.3f - deck, Turn(r));
			return "an oil rig over " + Mathf.RoundToInt(k.Sea - k.Ground(site.Value)) + " m of water, its deck " + Mathf.RoundToInt(deck - k.Sea) + " m up on " + legs + " legs";
		}

		static string Lighthouse(MapKit k, IslandGenSettings s)
		{
			System.Random r = k.Rnd;
			// A headland: high ground with the sea close by
			Vector2 best = Vector2.zero;
			float bestH = float.MinValue;
			for (int i = 0; i < 400; i++)
			{
				float a = (float)r.NextDouble() * Mathf.PI * 2f, d = (float)Math.Sqrt(r.NextDouble()) * s.Radius;
				Vector2 p = k.Mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
				float above = k.Ground(p) - k.Sea;
				if (above < 2f || above > 40f || k.Slope(p) > 24f || above <= bestH) continue;
				bool coast = false;
				for (int j = 0; j < 8 && !coast; j++) { float b = j * Mathf.PI / 4f; coast = k.Ground(p + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 32f) < k.Sea; }
				if (!coast) continue;
				best = p; bestH = above;
			}
			// (no headland on a small, steep island: its highest ground)
			if (bestH == float.MinValue) { best = k.Highest(k.Mid, s.Radius * 0.6f); if (k.Ground(best) - k.Sea < 1.5f) return null; }
			Frame f = Around(best, Turn(r), 0f);
			float y0 = TopUnder(k, f) + 0.05f;
			f = new Frame(f.O, f.Yaw, y0);
			Level(k, f, -8f, 2f, -7f, 7f, y0 - 0.05f, 5f);
			k.Clear(best, 14f);
			int storeys = 4 + r.Next(3), unfinished = r.Next(3); // (0: finished, its lantern room lit at the top)
			int built = storeys - unfinished;
			// (the storeys built, and over them the one being built: its floor and some of its walls - nothing above it, so
			// no wall stands on air)
			int standing = built + (unfinished > 0 ? 1 : 0);
			for (int i = 0; i < standing; i++)
			{
				Frame fl = f.Up(3f * i);
				bool open = i >= built;
				// (the front right slot stays open on the upper storeys: the ladder comes in there; a finished lighthouse's top
				// storey is its lantern room, windows all round - a box of plain storeys didn't look like one)
				bool lantern = unfinished == 0 && i == storeys - 1;
				Storey(k, fl, sl => i == 0 && sl == "f1" ? "RT_WallDoor1" : i > 0 && sl == "f2" ? null : lantern ? "RT_WallWindow2" : open && r.NextDouble() < 0.45 ? null : Wall(r, open ? 2 : i == 0 ? 3 : 1));
				if (lantern) foreach (float yaw in new[] { 0f, 90f, 180f, 270f }) Sit(k, fl, "RT_Floodlight", -3f + (yaw == 90f ? 1.2f : yaw == 270f ? -1.2f : 0f), (yaw == 0f ? 1.2f : yaw == 180f ? -1.2f : 0f), 0f, yaw);
			}
			Frame roof = f.Up(3f * Mathf.Max(built, 1));
			if (unfinished == 0)
			{
				// (the roof over the lantern room, railed, and a lit mast in its middle)
				P(k, roof, "RT_Floor", 0f, 4.5f);
				Railing(k, roof, 0f, gap: -1.5f);
				P(k, roof, "RT_PillarThick", -3f, 0f);
				P(k, roof, "RT_RoofLamp", -3f, 0f, 6f);
				Sit(k, roof, "RT_Floodlight", -3f, 1.2f, 0f, 0f);
			}
			else
			{
				// The lamp still waits by the door, in its crate
				Sit(k, f, "RT_Floodlight", -4.5f, -7f, k.Ground(f.At(-4.5f, -7f)) - f.Y, Turn(r));
				Sit(k, f, "Loot_Crate", -2.5f, -7.2f, k.Ground(f.At(-2.5f, -7.2f)) - f.Y, 0f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Metal") }, { ObjectProps.NoteTitle, "Lamp parts" } });
			}
			// Poles round it while it is built: thick pillars at its corners, up to its top
			float topY = f.Y + 3f * standing;
			foreach (Vector2 c in new[] { new Vector2(-6.6f, -5.1f), new Vector2(-6.6f, 5.1f), new Vector2(0.6f, -5.1f), new Vector2(0.6f, 5.1f) })
			{
				if (unfinished == 0) break;
				float g = k.Ground(f.At(c.x, c.y));
				for (float b = g - 0.3f; b < topY; b += 6f) P(k, f, "RT_PillarThick", c.x, c.y, b - f.Y);
			}
			Ladders(k, f, -1.5f, y0, f.Y + 3f * Mathf.Max(built, 1));
			return "a lighthouse " + (unfinished == 0 ? "of " + storeys + " storeys, its lamp lit" : "under construction: " + built + " of " + storeys + " storeys built");
		}

		#endregion
	}

	/// <summary>
	/// A quest made with the island (the user, 2026-10-03: "select how many steps the quest should have and it generates
	/// that together with the island"): 1 to 8 steps of the quest editor's own kinds, each with what it needs put on the
	/// island - a note to read where players land, a lookout to reach, monsters to defeat, map pieces in chests to collect,
	/// animals to catch, a torn page, a supply crate - and the hoard to open last. A step whose place doesn't fit is left
	/// out (the report says how many it got). Deterministic for the seed.
	/// </summary>
	public static class GenQuest
	{
		public const int MaxSteps = 8;
		// (titles that fit any quest; the others only fit some steps - ROADMAP CW6: "The lookout's log" came without a lookout)
		static readonly string[] Titles = { "The castaway's trail", "Lost supplies", "The last signal", "The hermit's hoard", "The drowned expedition" };

		/// <summary>A title that fits the steps chosen.</summary>
		static string TitleFor(System.Random r, List<string> steps, bool hutKey, bool cave)
		{
			var fits = new List<string>(Titles);
			if (steps.Any(s => s.StartsWith("reach|lookout"))) fits.Add("The lookout's log");
			if (steps.Any(s => s.StartsWith("collect|mappiece"))) { fits.Add("Buried treasure"); fits.Add("The torn map"); }
			if (hutKey) { fits.Add("The keeper's secret"); fits.Add("The hut's key"); }
			if (cave) { fits.Add("The cave's hoard"); fits.Add("Into the dark"); }
			return fits[r.Next(fits.Count)];
		}

		/// <summary>Adds the quest (and its notes, chests, zones, creatures, story item) to the kit's file. Returns how many steps it got (0: none).</summary>
		public static int Make(MapKit k, IslandGenSettings s)
		{
			int want = Mathf.Clamp(s.QuestSteps, 0, MaxSteps);
			if (want == 0) return 0;
			System.Random r = k.Rnd;
			var steps = new List<string>();
			// (the island's buildings and cave, when the generator made them - CW6: a key in a hut opens the hoard, the hoard
			// in the cave)
			IslandObject hutChest = k.File.Objects.FirstOrDefault(o => o.Props != null && (ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Castaway's chest" || ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Cabin chest"));
			IslandObject caveHoard = k.File.Objects.FirstOrDefault(o => o.Props != null && ObjectProps.Get(o.Props, ObjectProps.NoteTitle) == "Cave hoard");
			Func<Func<float, float, bool>, float, Vector2?> spot = (ok, apart) => k.Find(k.Mid, s.Radius * 0.85f, ok, apart);
			// The hoard: high up and out of the way
			Vector2 hoard = k.Highest(k.Mid, s.Radius * 0.6f);
			// (not on what was built at the top - a remade tower or lighthouse: the chest's clearing took its floors, walls and ladders)
			if (!k.IsFree(hoard, 6f)) hoard = k.Find(k.Mid, s.Radius * 0.6f, MapKit.Dry, 6f) ?? hoard;
			if (want == 1)
			{
				if (caveHoard != null) steps.Add("open|Cave hoard|1|Find the hoard in the island's cave");
				else
				{
					k.Chest("Loot_ChestLarge", hoard, "Hidden hoard", MapKit.Loot("Treasure"));
					steps.Add("open|Hidden hoard|1|Find the hidden hoard (it's up high)");
				}
				k.Quest(TitleFor(r, steps, false, caveHoard != null), "Someone hid a hoard on this island.", "The hoard is yours!", "", steps.ToArray());
				return 1;
			}
			// First: a note where players come ashore
			Vector2? landing = spot(MapKit.Beach, 10f) ?? spot(MapKit.Dry, 10f);
			if (landing.HasValue)
			{
				k.Note("Note_Paper", landing.Value, "Castaway's note", "If you're reading this, the sea brought you here too. I left what I could for whoever came next - " +
					"but I didn't make it easy. Look around the island: " + (want > 3 ? "the lookout up high, the pieces of my map, " : "") + "and my hoard at the top.");
				steps.Add("read|Castaway's note|1|Read the castaway's note on the beach");
			}
			// The middle, from what an island can have
			var pool = new List<string> { "reach", "kill", "collect", "page", "crate", "catch" };
			for (int i = pool.Count - 1; i > 0; i--) { int j = r.Next(i + 1); string t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
			// (the lookout and the monsters first when there's room for few: they need least)
			pool = pool.OrderBy(p => p == "catch" ? 2 : p == "page" || p == "crate" ? 1 : 0).ToList();
			int middle = want - 2;
			// (a key in a hut: one of the middle steps, and the hoard won't open without it)
			bool hutKey = hutChest != null && middle > 0;
			if (hutKey)
			{
				middle--;
				hutChest.Props[ObjectProps.LootItems] = StoryItems.Prefix + "hoardkey*1;" + ObjectProps.Get(hutChest.Props, ObjectProps.LootItems);
				var keyDefs = StoryItems.Of(k.File.Props);
				if (!keyDefs.Any(d => d.Id == "hoardkey")) keyDefs.Add(new StoryItemDef { Id = "hoardkey", Name = "Hoard key", Icon = StoryItems.QuestIcon + "Vasagatan_Key_Red", Description = "The castaway's key, kept in their hut." });
				k.File.Props[StoryItems.Key] = StoryItems.Text(keyDefs);
				steps.Add("collect|hoardkey|1|Find the key in the castaway's " + (ObjectProps.Get(hutChest.Props, ObjectProps.NoteTitle) == "Cabin chest" ? "cabin" : "hut"));
			}
			string hostile = s.Style == TerrainPainter.Snowy ? "PolarBear" : s.Style == TerrainPainter.Forest ? "Bear" : s.Style == TerrainPainter.Desert ? "Hyena" : r.NextDouble() < 0.5 ? "Boar" : "Rat";
			string friendly = s.Style == TerrainPainter.Snowy ? "Goat" : s.Style == TerrainPainter.Desert ? "Llama" : "Chicken";
			foreach (string kind in pool.Take(middle))
			{
				switch (kind)
				{
					case "reach":
					{
						Vector2 top = k.Highest(k.Mid, s.Radius * 0.9f);
						if ((top - hoard).magnitude < 12f) { Vector2? other = spot((a, sl) => a > 6f && sl < 30f, 20f); if (other.HasValue) top = other.Value; }
						k.Zone(top, "lookout", 6f, "From up here you see the whole island.");
						steps.Add("reach|lookout|1|Climb to the lookout");
						break;
					}
					case "kill":
					{
						Vector2? den = spot(MapKit.Dry, 18f);
						if (!den.HasValue) break;
						int n = hostile == "Bear" || hostile == "PolarBear" ? 1 : 2 + r.Next(2);
						k.Creature(hostile, den.Value, n, "Normal", 1f, null, false);
						ContentCatalog.CreatureKind ck = ContentCatalog.CreatureOf("Creature_" + hostile);
						string label = ck != null ? ck.Label : hostile;
						steps.Add("kill|" + label + "|" + n + "|" + (n > 1 ? "Chase off the " + label.ToLowerInvariant() + "s" : "Defeat the " + label.ToLowerInvariant()));
						break;
					}
					case "collect":
					{
						string id = "mappiece";
						int got = 0;
						for (int c = 0; c < 3; c++)
						{
							Vector2? p = spot(MapKit.Dry, 14f);
							if (!p.HasValue) continue;
							k.Chest("Loot_ChestSmall", p.Value, "Map piece box", StoryItems.Prefix + id + "*1;" + MapKit.Loot("Basics").Split(';').First());
							got++;
						}
						if (got == 0) break;
						var defs = StoryItems.Of(k.File.Props);
						if (!defs.Any(d => d.Id == id)) defs.Add(new StoryItemDef { Id = id, Name = "Map piece", Icon = StoryItems.QuestIcon + "Vasagatan_FourDigitCode", Description = "A torn piece of the castaway's map." });
						k.File.Props[StoryItems.Key] = StoryItems.Text(defs);
						steps.Add("collect|" + id + "|" + got + "|Find the " + got + " pieces of the map");
						break;
					}
					case "page":
					{
						Vector2? p = spot(MapKit.Dry, 14f);
						if (!p.HasValue) break;
						k.Note("Note_Papers", p.Value, "Torn page", "...the hoard is where the island is highest. I marked the way with stones, but the storm took them. Keep climbing.");
						steps.Add("read|Torn page|1|Find the torn page of the castaway's diary");
						break;
					}
					case "crate":
					{
						Vector2? p = spot(MapKit.Dry, 14f);
						if (!p.HasValue) break;
						k.Chest("Loot_Crate", p.Value, "Supply crate", MapKit.Loot("Food"));
						steps.Add("open|Supply crate|1|Find the castaway's supply crate");
						break;
					}
					case "catch":
					{
						Vector2? p = spot((a, sl) => a > 1.5f && sl < 12f, 16f);
						if (!p.HasValue) break;
						k.Creature(friendly, p.Value, 2, "Normal", 1f, null, true);
						ContentCatalog.CreatureKind ck = ContentCatalog.CreatureOf("Creature_" + friendly);
						steps.Add("catch|" + (ck != null ? ck.Label : friendly) + "|1|Catch one of the island's " + (ck != null ? ck.Label.ToLowerInvariant() : friendly.ToLowerInvariant()) + "s (Raft's net launcher)");
						break;
					}
				}
			}
			// Last: the hoard - in the cave when the island has one, else at the top; locked when a hut keeps its key
			string hoardTitle = caveHoard != null ? "Cave hoard" : "Castaway's hoard";
			IslandObject hoardChest = caveHoard;
			if (hoardChest == null) hoardChest = k.Chest("Loot_ChestLarge", hoard, "Castaway's hoard", MapKit.Loot("Treasure") + ";" + MapKit.Loot("Metal").Split(';').First());
			if (hutKey)
			{
				hoardChest.Props[BehaviourProps.CheckKey("open")] = "take|" + StoryItems.Prefix + "hoardkey|1";
				hoardChest.Props[BehaviourProps.ElseKey("open")] = "message||It's locked. The castaway must have kept the key somewhere safe - their hut?";
			}
			steps.Add("open|" + hoardTitle + "|1|" + (caveHoard != null ? "Open the hoard in the island's cave" : "Open the castaway's hoard at the top of the island"));
			k.Quest(TitleFor(r, steps, hutKey, caveHoard != null), "A castaway lived here. Their note should be near the beach.", "You found everything the castaway left behind.", "", steps.ToArray());
			return steps.Count;
		}
	}

	/// <summary>
	/// Buildings and caves on a generated island (the user, 2026-10-03: "a toggle to generate houses and structures, with
	/// options for different types, and a checkbox if caves should be generated"). Kinds: castaway huts of Raft's thatch
	/// and wooden cabins (Raft's foundations, walls, corner pillars and a hipped roof - RaftRoof.Hip - on the ground built up
	/// under them), the world randomizer's themed scenes from the quest islands (RandomizerIslands.Themes), or a mix of what
	/// suits the style. A cave is one of Raft's own cave pieces set into the land with a guard and a hoard
	/// (RandomizerIslands.EmbeddedCave). Put on the planned file before it is used, so the ground they level goes with it.
	/// </summary>
	public static class GenBuildings
	{
		public const string Mixed = "mixed", Huts = "huts", Cabins = "cabins", Landmarks = "landmarks";
		// (the user, 2026-10-05, ROADMAP LM10: "0 to 25" structures of any kind, on land or in the water)
		public const int MaxCount = 25;

		/// <summary>The kinds in the generator's list: mixed, huts, cabins, then the themed scenes.</summary>
		public static List<string> Kinds { get { return new[] { Mixed, Huts, Cabins, Landmarks }.Concat(RandomizerIslands.Themes.Select(t => t.Name)).ToList(); } }

		public static string Label(string kind)
		{
			if (kind == Mixed) return "Mixed";
			if (kind == Huts) return "Castaway huts";
			if (kind == Cabins) return "Wooden cabins";
			if (kind == Landmarks) return "Wrecks and landmarks";
			Theme t = RandomizerIslands.ThemeOf(kind);
			return t != null ? t.Label : kind;
		}

		public static string Hint(string kind)
		{
			if (kind == Mixed) return "A bit of everything that suits the style: huts, cabins and scenes";
			if (kind == Huts) return "Huts of Raft's thatch walls and roof on foundations, a hammock and a chest inside";
			if (kind == Landmarks) return "Boats on the beach and sunk in the sea, plane wrecks on land and under water, vans, caravans, shacks, statues and rocket debris";
			if (kind == Cabins) return "Cabins of Raft's wooden walls and roof on foundations, a bed, a chest and a log inside";
			Theme t = RandomizerIslands.ThemeOf(kind);
			return t != null ? "A scene from the quest islands, with its props, a " + (t.Container ?? "chest").Replace("Loot_", "").ToLowerInvariant() + " and a note" : "";
		}

		/// <summary>The objects the settings' buildings and caves may use that come from Raft's island scenes (loaded on demand:
		/// PlaceableCatalog.EnsureLoaded before generating in the editor - the huts' blocks are always there).</summary>
		public static List<string> NeededNames(IslandGenSettings s)
		{
			var names = new List<string>();
			if (s.Buildings && (s.BuildingKind == Mixed || s.BuildingKind == Landmarks)) names.AddRange(LandmarkNames);
			if (s.Buildings && s.BuildingKind != Huts && s.BuildingKind != Cabins && s.BuildingKind != Landmarks)
			{
				IEnumerable<Theme> themes = s.BuildingKind == Mixed ? RandomizerIslands.ThemesFor(s.Style) : new[] { RandomizerIslands.ThemeOf(s.BuildingKind) }.Where(t => t != null);
				foreach (Theme t in themes)
					names.AddRange(t.Anchors.Concat(t.Medium).Concat(t.Small).Concat(new[] { t.Container, t.NoteKind }).Where(n => !string.IsNullOrEmpty(n)));
			}
			if (s.Buildings && (s.BuildingKind == Mixed || s.BuildingKind == Huts || s.BuildingKind == Cabins)) names.AddRange(FloorPickupNames);
			if (s.Caves) names.AddRange(RandomizerIslands.Dens);
			names.AddRange(Remakes.NeededNames(s));
			names.AddRange(Remakes.LookNames(s));
			if (s.Features > 0) names.AddRange(GenFeatures.NamesFor(s));
			return names.Distinct().ToList();
		}

		/// <summary>Adds what the settings ask for to a planned island file (its Heights may change). Returns what was put, for the report.</summary>
		public static List<string> Apply(IslandFile file, IslandGenSettings s)
		{
			var done = new List<string>();
			if ((!s.Buildings && !s.Caves && s.QuestSteps <= 0 && s.Features <= 0 && string.IsNullOrEmpty(s.Remake) && string.IsNullOrEmpty(s.Look)) || file == null || file.Heights == null) return done;
			var k = new MapKit(file, s.Seed * 7919 + 13);
			System.Random r = k.Rnd;
			// The story island's look first: its ground and its signature pieces (its own random stream: k.Rnd draws as before)
			string look = Remakes.BuildLook(k, s);
			if (look != null) done.Add(look);
			// A story island rebuilt first: it takes the island's best place (its top, a headland, the sea off the coast)
			string remade = Remakes.Build(k, s);
			if (remade != null) done.Add(remade);
			// The look's last pieces, where the design left room (the Radio Tower's dish)
			string finish = Remakes.FinishLook(k, s);
			if (finish != null) done.Add(finish);
			// A cave first: it needs the most room (an outcrop over land, open ground at its mouth)
			if (s.Caves)
			{
				string guard = s.Style == TerrainPainter.Snowy ? "PolarBear" : s.Style == TerrainPainter.Forest ? "Bear" : s.Style == TerrainPainter.Desert ? "Hyena" : r.NextDouble() < 0.5 ? "Boar" : "Rat";
				bool cave = RandomizerIslands.CanBuildCaves && RandomizerIslands.EmbeddedCave(k, s, guard, "Treasure", "Cave hoard");
				done.Add(cave ? "a cave" : RandomizerIslands.CanBuildCaves ? "no cave (no spot fits: it needs a hill by open, level land)" : "no cave (Raft's cave pieces aren't loaded)");
			}
			if (!s.Buildings) { GenFeatures.Make(k, s, done); Quest(k, s, done); Remakes.SettleLook(k); return done; }
			List<Theme> suits = RandomizerIslands.ThemesFor(s.Style);
			int count = Mathf.Clamp(s.BuildingCount, 0, MaxCount), built = 0;
			var spots = new List<Vector2>();
			int picksBefore = file.Objects.Count(o => IsFloorPickup(o.Name));
			for (int i = 0, tries = 0; built < count && tries < count * 4; tries++)
			{
				string kind = s.BuildingKind;
				if (kind == Mixed)
				{
					var pool = new List<string> { Huts, Cabins, Landmarks };
					pool.AddRange(suits.Select(t => t.Name));
					kind = pool[r.Next(pool.Count)];
				}
				if (kind == Landmarks)
				{
					string what = Landmark(k, s, spots);
					if (what == null) continue;
					built++;
					done.Add(what);
					continue;
				}
				bool hut = kind == Huts || kind == Cabins;
				Theme theme = hut ? null : RandomizerIslands.ThemeOf(kind);
				if (!hut && theme == null) break;
				// (open, level, dry land; apart from each other, the cave, its guard and the ground in front of its mouth)
				Vector2? spot = k.Find(k.Mid, s.Radius * 0.8f, (above, slope) => above > 1.5f && above < 30f && slope < (hut ? 9f : 10f), 24f);
				if (!spot.HasValue) continue;
				Vector2 p = spot.Value;
				if (spots.Any(o => (o - p).magnitude < 22f)) continue;
				if (file.Objects.Any(o => o.Props != null && o.Props.ContainsKey("cave") && new Vector2(o.Position.x - p.x, o.Position.z - p.y).magnitude < 38f)) continue;
				bool ok = hut ? Hut(k, p, kind == Cabins) : RandomizerIslands.Dress(k, theme, p, 9f);
				if (!ok) continue;
				spots.Add(p);
				built++;
				done.Add(Label(kind).ToLowerInvariant().TrimEnd('s').Replace("castaway hut", "a castaway hut").Replace("wooden cabin", "a wooden cabin"));
			}
			if (built < count) done.Add((count - built) + " building(s) found no level spot");
			int picks = file.Objects.Count(o => IsFloorPickup(o.Name)) - picksBefore;
			if (picks > 0) done.Add(picks + (picks == 1 ? " thing" : " things") + " to pick up on the houses' floors");
			GenFeatures.Make(k, s, done);
			Quest(k, s, done);
			// (the look's pieces back on the ground that designs, buildings and caves changed after them)
			Remakes.SettleLook(k);
			return done;
		}

		/// <summary>Tests: every wreck or landmark of this kind ("lighthouse", "jetty"...), null = any.</summary>
		internal static string OnlyLandmark;

		/// <summary>The wrecks and landmarks' set pieces (Raft's quest islands' own; loaded with their scenes).</summary>
		public static readonly string[] LandmarkNames = { "BoatStranded", "Airplane", "RT_PlasticBoat", "RT_PillarThick", "RT_Floor", "RT_Fence", "LandmarkLadder_6m", "TP_RotatingRedLight", "RT_Floodlight",
			"RT_SatteliteDisc", "RT_WindMill", "RT_Wall1", "RT_WallWindowBroken", "RT_WallWindowBroken1", "RT_WallWindowBroken2", "Van_1", "Van_2", "Van3", "Van_4", "Van_5", "Caravan_Blue_01", "Caravan_Green_01",
			"Caravan_Yellow_01", "Balboa_Shack", "TangaroaFounderStatue", "RaftMonument", "CaravanRocket", "CaravanRocketDebris_Body1", "CaravanRocketDebris_Door", "CaravanRocketDebris_Canister",
			"RT_WallWindow1", "RT_WallWindow2", "RT_WallDoor1", "RT_RoofLamp", "Locker" };

		/// <summary>
		/// One wreck or landmark (ROADMAP LM10): a boat run aground on the beach or sunk off the coast, a plane crashed on the
		/// land or lying under water, a small boat pulled up the beach, a van, a caravan, a shack, a statue on high ground or a
		/// rocket's debris - Raft's own set pieces, stood on the ground as the randomizer's oddities are, with a chest by the
		/// ones on land. Kept 22 m from the island's other buildings (spots). Returns what was put, or null (no spot).
		/// </summary>
		internal static string Landmark(MapKit k, IslandGenSettings s, List<Vector2> spots)
		{
			System.Random r = k.Rnd;
			string[] kinds = { "beached boat", "sunken boat", "plane wreck", "sunken plane", "small boat", "van", "caravan", "shack", "statue", "rocket debris", "lighthouse", "lookout mast", "jetty", "ruin", "skyscraper" };
			string kind = OnlyLandmark ?? kinds[r.Next(kinds.Length)];
			bool wet = kind == "sunken boat" || kind == "sunken plane", beach = kind == "beached boat" || kind == "small boat" || kind == "jetty";
			bool jetty = kind == "jetty";
			Func<float, float, bool> ok;
			if (kind == "sunken boat") ok = (above, slope) => above < -5f && above > -14f && slope < 22f;
			else if (wet) ok = (above, slope) => above < -4f && above > -10f && slope < 22f;
			else if (jetty) ok = (above, slope) => above > -0.2f && above < 0.35f && slope < 12f; // (at the waterline)
			else if (beach) ok = (above, slope) => above > 0.3f && above < 1.8f && slope < 15f;
			else if (kind == "statue" || kind == "lookout mast") ok = (above, slope) => above > 4f && slope < 12f;
			else if (kind == "lighthouse") ok = (above, slope) => above > 1.3f && above < 8f && slope < 10f;
			else ok = (above, slope) => above > 1.3f && above < 30f && slope < 9f;
			Vector2? found = null;
			for (int i = 0; i < 12 && found == null; i++)
			{
				Vector2? p = k.Find(k.Mid, s.Radius * (wet ? 1.5f : beach ? 1.2f : 0.85f), ok, 12f);
				if (!p.HasValue || spots.Any(o => (o - p.Value).magnitude < 22f)) continue;
				// (the big ones on land on even ground: a shack on a 9-degree slope stood 3 m up on its low side)
				bool tower = kind == "lighthouse" || kind == "lookout mast" || kind == "ruin" || kind == "skyscraper";
				if (!wet && !beach && Uneven(k, p.Value + (tower && kind != "skyscraper" ? new Vector2(-3f, 0f) : Vector2.zero), kind == "skyscraper" ? 8f : kind == "shack" || kind == "plane wreck" || tower ? 7f : 4f) > (tower ? 1.5f : 1f)) continue;
				// (a lighthouse near the coast: open sea within 25 m)
				if (kind == "lighthouse" && !Enumerable.Range(0, 16).Any(a => k.Ground(p.Value + new Vector2(Mathf.Cos(a * 0.3927f), Mathf.Sin(a * 0.3927f)) * 25f) < k.Sea)) continue;
				// (a jetty: the sea 12 m out from the beach, away from the land's middle)
				if (kind == "jetty" && k.Ground(p.Value - (k.Mid - p.Value).normalized * 12f) > k.Sea - 1f) continue;
				found = p;
			}
			if (!found.HasValue) return null;
			Vector2 c = found.Value;
			Vector2 toMid = k.Mid - c;
			float yaw = (float)r.NextDouble() * 360f, shore = Mathf.Atan2(toMid.x, toMid.y) * Mathf.Rad2Deg + 90f;
			Func<string[], string> one = a => a[r.Next(a.Length)];
			switch (kind)
			{
				case "beached boat": RandomizerContent.Piece(k, "BoatStranded", c, shore, 1.3f, RandomizerContent.Tilt(r, 4f), 12f + (float)r.NextDouble() * 8f, 10f); break;
				case "sunken boat": RandomizerContent.Piece(k, "BoatStranded", c, yaw, 1.0f, RandomizerContent.Tilt(r, 10f), 18f + (float)r.NextDouble() * 20f, 0f); break;
				case "plane wreck": RandomizerContent.Piece(k, "Airplane", c, yaw, 0.6f, RandomizerContent.Tilt(r, 7f), RandomizerContent.Tilt(r, 12f), 9f); break;
				case "sunken plane": RandomizerContent.Piece(k, "Airplane", c, yaw, 0.8f, RandomizerContent.Tilt(r, 10f), RandomizerContent.Tilt(r, 25f), 0f); break;
				case "small boat": RandomizerContent.Piece(k, "RT_PlasticBoat", c, shore, 0.15f, RandomizerContent.Tilt(r, 4f), 8f + (float)r.NextDouble() * 10f, 4f); break;
				case "van": RandomizerContent.Piece(k, one(new[] { "Van_1", "Van_2", "Van3", "Van_4", "Van_5" }), c, yaw, 0.35f, RandomizerContent.Tilt(r, 4f), RandomizerContent.Tilt(r, 5f), 7f); break;
				case "caravan": RandomizerContent.Piece(k, one(new[] { "Caravan_Blue_01", "Caravan_Green_01", "Caravan_Yellow_01" }), c, yaw, 0.05f, 0f, RandomizerContent.Tilt(r, 2f), 6f); break;
				case "shack": RandomizerContent.Piece(k, "Balboa_Shack", c, yaw, 0.1f, 0f, 0f, 10f); break;
				case "statue": RandomizerContent.Piece(k, one(new[] { "TangaroaFounderStatue", "RaftMonument" }), c, yaw, 0.15f, 0f, 0f, 5f); break;
				case "lighthouse": RtTower(k, c, true); break;
				case "lookout mast": RtTower(k, c, false); break;
				case "jetty": Jetty(k, c, -toMid.normalized); break;
				case "ruin": Ruin(k, c, r); break;
				case "skyscraper": Remakes.Skyscraper(k, c, r); break;
				default:
					RandomizerContent.Piece(k, "CaravanRocket", c, yaw, 0.2f, 25f + (float)r.NextDouble() * 20f, RandomizerContent.Tilt(r, 20f), 5f);
					string[] debris = { "CaravanRocketDebris_Body1", "CaravanRocketDebris_Door", "CaravanRocketDebris_Canister" };
					for (int i = 0; i < 5; i++)
					{
						float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 3f + (float)r.NextDouble() * 5f;
						RandomizerContent.Piece(k, one(debris), c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, (float)r.NextDouble() * 360f, 0.05f, RandomizerContent.Tilt(r, 40f), RandomizerContent.Tilt(r, 40f));
					}
					break;
			}
			spots.Add(c);
			// (a chest by the ones on land: the boat's stores, the plane's cargo, the van's glovebox...)
			if (!wet)
			{
				Vector2? dry = k.Find(c, 12f, MapKit.Dry, 4f);
				if (dry.HasValue) k.Chest(one(new[] { "Loot_Barrel", "Loot_Box", "Loot_Crate" }), dry.Value, beach ? "Ship's stores" : kind == "plane wreck" ? "Cargo" : "Stores", MapKit.Loot(r.NextDouble() < 0.5 ? "Food" : "Metal"));
			}
			return "a " + kind;
		}

		/// <summary>The lowest ground under the four legs of a 6 x 9 m radio-tower footprint whose -x +z corner pivot is at c (x -6..0, z -4.5..4.5 of c).</summary>
		static float LegsGround(MapKit k, Vector2 c)
		{
			float low = float.MaxValue;
			foreach (float lx in new[] { -5.9f, -0.1f }) foreach (float lz in new[] { -4.4f, 4.4f }) low = Mathf.Min(low, k.Ground(c + new Vector2(lx, lz)));
			return low;
		}

		/// <summary>A piece of a radio-tower frame at c (its floor y0), x z y in the frame (as lib_rt's macros place them), turned yaw.</summary>
		static IslandObject Rt(MapKit k, string name, Vector2 c, float y0, float x, float z, float y, float yaw = 0f)
		{
			return k.Add(name, new Vector3(c.x + x, y0 + y, c.y + z), yaw, new Dictionary<string, string> { { "set.piece", "1" } }, 0f);
		}

		/// <summary>
		/// A lookout of Raft's radio tower pieces (as Radio Tower Remade's tower, two decks): four thick legs from the lowest
		/// ground under them, decks at 6.3 and 13.3 m, ladders up its front, railings; on top a lighthouse's rotating light and
		/// floodlights, or a lookout mast's dish and wind wheel - and a chest on the top deck.
		/// </summary>
		static void RtTower(MapKit k, Vector2 c, bool lighthouse)
		{
			k.Clear(c + new Vector2(-3f, 0f), 8f);
			float y0 = LegsGround(k, c);
			foreach (float lx in new[] { -5.9f, -0.1f })
				foreach (float lz in new[] { -4.4f, 4.4f })
				{
					Rt(k, "RT_PillarThick", c, y0, lx, lz, 0f);
					Rt(k, "RT_PillarThick", c, y0, lx, lz, 6f);
					IslandObject last = Rt(k, "RT_PillarThick", c, y0, lx, lz, 12f);
					last.Scale = new Vector3(last.Scale.x, last.Scale.y * (1.3f / 6f), last.Scale.z);
				}
			foreach (float d in new[] { 6.3f, 13.3f })
			{
				Rt(k, "RT_Floor", c, y0, 0f, 4.5f, d);
				// (as Railing: four pieces along the back - a fifth at -6 ran off the floor - and the sides without 1.5 m holes)
				for (float x = 0f; x > -6f; x -= 1.5f) Rt(k, "RT_Fence", c, y0, x, 4.5f, d);
				foreach (float z in new[] { -4.5f, -3f, -1.5f, 0f, 1.5f, 3f }) { Rt(k, "RT_Fence", c, y0, -6f, z, d, 90f); Rt(k, "RT_Fence", c, y0, 0f, z, d, 90f); }
			}
			Rt(k, "LandmarkLadder_6m", c, y0, -1.5f, -4.62f, 0f);
			Rt(k, "LandmarkLadder_6m", c, y0, -4.5f, -4.62f, 7.3f); // (its top at the top deck, as Ladders puts them: from 6.3 it ended 1 m under it)
			if (lighthouse)
			{
				Rt(k, "TP_RotatingRedLight", c, y0, -3f, 0f, 13.3f);
				Rt(k, "RT_Floodlight", c, y0, -1.5f, 3.6f, 13.3f, 180f);
				Rt(k, "RT_Floodlight", c, y0, -4.5f, 3.6f, 13.3f, 180f);
			}
			else
			{
				Rt(k, "RT_SatteliteDisc", c, y0, -4.5f, 2.5f, 13.3f);
				Rt(k, "RT_WindMill", c, y0, -1.5f, 3f, 13.3f);
			}
			k.Add("Loot_Chest", new Vector3(c.x - 3f, y0 + 13.3f, c.y + 1.5f), 0f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot(lighthouse ? "Metal" : "Basics") }, { ObjectProps.NoteTitle, lighthouse ? "Keeper's chest" : "Lookout's chest" } }, 0f);
		}

		/// <summary>A jetty of Raft's foundations from the beach out over the water (two wide, eight long), a small boat at its end.</summary>
		static void Jetty(MapKit k, Vector2 c, Vector2 seaward)
		{
			float g = PlacementOptions.GridSize;
			float yaw = Mathf.Atan2(seaward.x, seaward.y) * Mathf.Rad2Deg;
			Vector2 side = new Vector2(seaward.y, -seaward.x);
			k.Clear(c + seaward * 6f, 8f);
			for (int i = 0; i < 8; i++)
				for (int j = 0; j < 2; j++)
				{
					Vector2 p = c + seaward * (i * g) + side * (j * g);
					// (one level deck from the waterline out, a little over the sea as a raft's deck is; a foundation set on the sand
					// sank into it, one at the sea's level drowned under the water, 2026-10-05)
					float y = Mathf.Max(k.Ground(p) + 0.05f, k.Sea + 0.45f);
					k.Add("Block_Foundation", new Vector3(p.x, y + 0.001f, p.y), yaw, new Dictionary<string, string> { { "set.piece", "1" } }, 0f);
				}
			Vector2 end = c + seaward * (8.5f * g) + side * (2.2f * g);
			// (floating by it, as Radio Tower Remade's boat: set on the sea floor it lay drowned under the deck)
			k.Add("RT_PlasticBoat", new Vector3(end.x, k.Sea - 0.3f, end.y), yaw + 90f, new Dictionary<string, string> { { "set.piece", "1" } }, 0f);
		}

		/// <summary>A ruin: a radio-tower room's walls on the ground, half of them broken or gone, no floor or roof, a crate inside.</summary>
		static void Ruin(MapKit k, Vector2 c, System.Random r)
		{
			k.Clear(c + new Vector2(-3f, 0f), 7f);
			float y0 = LegsGround(k, c);
			string[] wall = { "RT_Wall1", "RT_WallWindowBroken", "RT_WallWindowBroken1", "RT_WallWindowBroken2", null, null };
			// (no floor: on a slope it sank half into the ground; the room's ground is grown over)
			var slots = new[] { new Vector3(-3f, 4.5f, 0f), new Vector3(0f, 4.5f, 0f), new Vector3(-3f, -4.5f, 0f), new Vector3(0f, -4.5f, 0f),
				new Vector3(-6.04f, -4.5f, 90f), new Vector3(-6.04f, -1.5f, 90f), new Vector3(-6.04f, 1.5f, 90f), new Vector3(0f, -4.5f, 90f), new Vector3(0f, -1.5f, 90f), new Vector3(0f, 1.5f, 90f) };
			foreach (Vector3 sl in slots)
			{
				string w = wall[r.Next(wall.Length)];
				if (w != null) Rt(k, w, c, y0, sl.x, sl.y, 0.05f, sl.z);
			}
			k.Add("Loot_Crate", k.At(new Vector2(c.x - 3f, c.y)), (float)r.NextDouble() * 360f, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot("Treasure") }, { ObjectProps.NoteTitle, "Ruin's crate" } }, 0f);
		}

		/// <summary>How much the ground rises and falls within radius of p (m).</summary>
		static float Uneven(MapKit k, Vector2 p, float radius)
		{
			float lo = float.MaxValue, hi = float.MinValue;
			for (float x = -radius; x <= radius; x += 1.5f)
				for (float z = -radius; z <= radius; z += 1.5f)
				{
					if (x * x + z * z > radius * radius) continue;
					float g = k.Ground(p + new Vector2(x, z));
					lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
				}
			return hi - lo;
		}

		/// <summary>The quest last: its notes and chests go where the buildings left room.</summary>
		static void Quest(MapKit k, IslandGenSettings s, List<string> done)
		{
			if (s.QuestSteps <= 0) return;
			int got = GenQuest.Make(k, s);
			done.Add("a quest of " + got + (got == 1 ? " step" : " steps") + (got == s.QuestSteps ? "" : " (" + (s.QuestSteps - got) + " found no place)"));
		}

		/// <summary>
		/// A hut of w x d cells (3 x 2 or 2 x 2) on Raft's foundations, levelled on the highest ground under it - the ground
		/// built up under it to that height, blended over 3 m (on a slope its low side hung in the air) - with walls on three
		/// sides (the fourth open), pillars at the corners and a hipped roof resting on them (RaftRoof.Hip: no roof floats).
		/// Thatch with a hammock, or wood with a bed and a log; a chest in both.
		/// </summary>
		public static bool Hut(MapKit k, Vector2 c, bool wood)
		{
			float g = PlacementOptions.GridSize;
			int w = k.Rnd.NextDouble() < 0.6 ? 3 : 2, d = 2;
			k.Clear(c + new Vector2((w - 1) * g / 2f, (d - 1) * g / 2f), 6f);
			float top = float.MinValue;
			for (int x = -1; x <= w; x++) for (int z = -1; z <= d; z++) top = Mathf.Max(top, k.Ground(c + new Vector2(x * g, z * g)));
			if (top - k.Sea < 0.8f) return false;
			Vector3 o = new Vector3(c.x, top, c.y);
			// (the deck: the foundations' visible top, measured - 0.05 m over their pivot. At FloatDepth (0.35) the walls and
			// pillars hung 0.3 m in the air, the user saw; at the raft deck's walking height (0.22) still 0.17 m)
			Bounds fb;
			float deck = top + (Measured("Block_Foundation", out fb) ? fb.max.y : 0.05f);
			IslandFile f = k.File;
			float step = f.TerrainSize.x / (f.HeightmapResolution - 1);
			Vector2 lo = c - new Vector2(g, g), hi = c + new Vector2(w * g, d * g);
			for (int zi = Mathf.Max(0, Mathf.FloorToInt((lo.y - 4f) / step)); zi <= Mathf.Min(f.HeightmapResolution - 1, Mathf.CeilToInt((hi.y + 4f) / step)); zi++)
				for (int xi = Mathf.Max(0, Mathf.FloorToInt((lo.x - 4f) / step)); xi <= Mathf.Min(f.HeightmapResolution - 1, Mathf.CeilToInt((hi.x + 4f) / step)); xi++)
				{
					float px = xi * step, pz = zi * step;
					float outside = Mathf.Max(Mathf.Max(lo.x - px, px - hi.x), Mathf.Max(lo.y - pz, pz - hi.y));
					float weight = outside <= 0f ? 1f : outside < 3f ? 1f - outside / 3f : 0f;
					float h = f.Heights[zi, xi] * f.TerrainSize.y;
					if (weight > 0f && h < top) f.Heights[zi, xi] = Mathf.Lerp(h, top - 0.05f, weight) / f.TerrainSize.y;
				}
			for (int x = 0; x < w; x++)
				for (int z = 0; z < d; z++)
					k.Add("Block_Foundation", o + new Vector3(x * g, 0f, z * g), 0f, null, 0f);
			RaftRoof.Hip((n, p, ry) => k.Add(n, p, ry, null, 0f), new Vector3(o.x, deck + RaftRoof.OnWalls, o.z), w, d, wood);
			string wall = wood ? "Block_Wall_Wood" : "Block_Wall_Thatch";
			for (int x = 0; x < w; x++)
			{
				k.Add(wall, new Vector3(o.x + x * g, deck, o.z - g / 2f), 0f, null, 0f);
				k.Add(wall, new Vector3(o.x + x * g, deck, o.z + (d - 1) * g + g / 2f), 0f, null, 0f);
			}
			for (int z = 0; z < d; z++) k.Add(wall, new Vector3(o.x - g / 2f, deck, o.z + z * g), 90f, null, 0f);
			foreach (Vector2 corner in new[] { new Vector2(-g / 2f, -g / 2f), new Vector2((w - 0.5f) * g, -g / 2f), new Vector2(-g / 2f, (d - 0.5f) * g), new Vector2((w - 0.5f) * g, (d - 0.5f) * g) })
				k.Add("Block_Pillar_Wood", new Vector3(o.x + corner.x, deck, o.z + corner.y), 0f, null, 0f);
			// Inside, fitted by their own size (a bed set by a guess stuck out through the front wall): the bed or hammock along
			// the back wall, the chest in the front corner by the closed side, the log by the open side
			Vector2 inMin = new Vector2(o.x - g / 2f + 0.15f, o.z - g / 2f + 0.15f), inMax = new Vector2(o.x + (w - 0.5f) * g - 0.15f, o.z + (d - 0.5f) * g - 0.15f);
			// (Raft's own beds, always loaded - the abandoned rafts' bed loads with their scenes, and was a missing-object block;
			// the hammock is 3.6 m long: only in a hut three cells long)
			int furniture = k.File.Objects.Count;
			Inside(k, wood ? "Placeable_Bed_Wood" : w >= 3 ? "Placeable_Bed_Hammock" : "Placeable_Bed_Basic", inMin, inMax, 0.5f, 1f, deck, null);
			Inside(k, "Loot_Chest", inMin, inMax, 0f, 0f, deck, new Dictionary<string, string> { { ObjectProps.LootItems, MapKit.Loot(wood ? "Metal" : "Basics") + ";" + MapKit.Loot("Food").Split(';').First() }, { ObjectProps.NoteTitle, wood ? "Cabin chest" : "Castaway's chest" } });
			if (wood)
				Inside(k, "Note_Book", inMin, inMax, 1f, 0f, deck, new Dictionary<string, string> { { ObjectProps.NoteTitle, "Cabin log" }, { ObjectProps.NoteText, "Built it plank by plank. The roof keeps the rain out, the walls keep the wind out. The sharks I keep out myself." } });
			FloorPickups(k, inMin, inMax, deck, furniture);
			return true;
		}

		/// <summary>
		/// ROADMAP LM13: Raft's land pickups that may lie on a house's floor - plastic, scrap and (rarely) a plastic pile, the
		/// pieces Raft's own Radio Tower leaves about (one scene to load). Raft's planks lie on Tangaroa only, a big scene, so
		/// the generator leaves them to the library islands' recipes.
		/// </summary>
		public static readonly string[] FloorPickupNames = { "Pickup_Landmark_Plastic3_Land", "Pickup_Landmark_Scrap 1_Land", "Pickup_Landmark_Scrap 2_Land", "Pickup_Landmark_Scrap 3_Land", "Pickup_Landmark_Plastic6_Land" };

		/// <summary>Each pickup's lowest point under its pivot as an island spawns it (measured by CIFloorPickups): used when the
		/// piece's scene isn't loaded to measure it (islands made while sailing, the world randomizer).</summary>
		static readonly float[] FloorPickupBottoms = { -0.210f, -0.028f, -0.060f, -0.114f, -0.152f };

		public static bool IsFloorPickup(string name) { return Array.IndexOf(FloorPickupNames, name) >= 0; }

		/// <summary>Tests: no floor pickups (CIFloorPickups compares an island made with and without them).</summary>
		internal static bool NoFloorPickups;

		/// <summary>Tests: FloorPickupBottoms' value for a pickup (NaN = not one).</summary>
		internal static float FloorPickupBottom(string name) { int i = Array.IndexOf(FloorPickupNames, name); return i < 0 ? float.NaN : FloorPickupBottoms[i]; }

		/// <summary>
		/// ROADMAP LM13 (the user, 2026-10-09: "inside houses and other structures there are sometimes things lying on the
		/// floor to pick up"): 0-2 of Raft's land pickups (FloorPickupNames) on a house's floor - none in about a third of
		/// the houses, two in one of five, as light as Raft's own buildings. Kept 0.3 m clear of the furniture put from index
		/// furnitureFrom on (blocks - walls, pillars, roof - don't count), 0.45 m in from the walls and 0.7 m apart, lying flat
		/// on the floor at a random turn. Its own random stream from the floor's place: the island's other draws are as before.
		/// Added straight to the file (not through MapKit.Add, so later spots are found as before). Returns how many.
		/// </summary>
		public static int FloorPickups(MapKit k, Vector2 inMin, Vector2 inMax, float floor, int furnitureFrom)
		{
			var r = new System.Random((Mathf.RoundToInt(inMin.x * 10f) * 73856093) ^ (Mathf.RoundToInt(inMin.y * 10f) * 19349663) ^ 0x1A13);
			double roll = r.NextDouble();
			int n = roll < 0.35 ? 0 : roll < 0.8 ? 1 : 2;
			if (n == 0 || NoFloorPickups) return 0;
			List<IslandObject> objs = k.File.Objects;
			var taken = new List<Rect>();
			for (int i = Mathf.Max(0, furnitureFrom); i < objs.Count; i++)
			{
				IslandObject o = objs[i];
				if (o.Name.StartsWith("Block_")) continue;
				Bounds b;
				if (!Measured(o.Name, out b)) { taken.Add(new Rect(o.Position.x - 0.7f, o.Position.z - 0.7f, 1.4f, 1.4f)); continue; }
				Quaternion q = Quaternion.Euler(o.EulerRotation);
				float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
				foreach (Vector3 corner in new[] { new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.max.z) })
				{
					Vector3 p = o.Position + q * corner; // (Measured's box is at the piece's own scale, as placed)
					x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
				}
				taken.Add(Rect.MinMaxRect(x0 - 0.3f, z0 - 0.3f, x1 + 0.3f, z1 + 0.3f));
			}
			Vector2 lo = inMin + new Vector2(0.45f, 0.45f), hi = inMax - new Vector2(0.45f, 0.45f);
			if (hi.x <= lo.x || hi.y <= lo.y) return 0;
			var put = new List<Vector2>();
			for (int i = 0; i < n; i++)
			{
				// (plastic most, scrap of three sizes, a plastic pile now and then as the only one)
				int pick = n == 1 && r.NextDouble() < 0.12 ? 4 : new[] { 0, 0, 0, 0, 1, 1, 2, 2, 3 }[r.Next(9)];
				float yaw = (float)r.NextDouble() * 360f;
				for (int tries = 0; tries < 30; tries++)
				{
					var p = new Vector2(Mathf.Lerp(lo.x, hi.x, (float)r.NextDouble()), Mathf.Lerp(lo.y, hi.y, (float)r.NextDouble()));
					if (taken.Any(t => t.Contains(p)) || put.Any(o => (o - p).sqrMagnitude < 0.49f)) continue;
					string name = FloorPickupNames[pick];
					Bounds b;
					float bottom = Measured(name, out b) ? b.min.y : FloorPickupBottoms[pick];
					GameObject proto = PlaceableCatalog.Get(name);
					objs.Add(new IslandObject { Name = name, Position = new Vector3(p.x, floor - bottom, p.y), EulerRotation = new Vector3(0f, yaw, 0f), Scale = proto != null ? proto.transform.localScale : Vector3.one, Unscaled = proto == null });
					put.Add(p);
					break;
				}
			}
			return put.Count;
		}

		/// <summary>
		/// Puts an object inside a box on the floor by its own size: turned so its long side runs along x, its box against the
		/// box's side at fx, fz (0 = the min side, 1 = the max side, 0.5 = the middle).
		/// </summary>
		static void Inside(MapKit k, string name, Vector2 min, Vector2 max, float fx, float fz, float floor, Dictionary<string, string> props)
		{
			Bounds b;
			if (!Measured(name, out b)) { k.Add(name, new Vector3(Mathf.Lerp(min.x, max.x, fx), floor, Mathf.Lerp(min.y, max.y, fz)), 0f, props, 0f); return; }
			float yaw = b.size.z > b.size.x ? 90f : 0f;
			// (turned 90 its own z runs along the world's x: the box's centre turns with it)
			Vector3 c = Quaternion.Euler(0f, yaw, 0f) * b.center;
			float sx = yaw == 0f ? b.size.x : b.size.z, sz = yaw == 0f ? b.size.z : b.size.x;
			float x = sx > max.x - min.x ? (min.x + max.x) / 2f : Mathf.Lerp(min.x + sx / 2f, max.x - sx / 2f, fx);
			float z = sz > max.y - min.y ? (min.y + max.y) / 2f : Mathf.Lerp(min.y + sz / 2f, max.y - sz / 2f, fz);
			k.Add(name, new Vector3(x - c.x, floor, z - c.z), yaw, props, 0f);
		}

		static readonly Dictionary<string, Bounds> measured = new Dictionary<string, Bounds>();

		/// <summary>
		/// An object's box as an island spawns it unturned (relative to its pivot): from a copy spawned once and measured. The
		/// catalog's prototype measure put a bed's box on the wrong side of its pivot, out through the front wall.
		/// </summary>
		internal static bool Measured(string name, out Bounds b)
		{
			if (measured.TryGetValue(name, out b)) return true;
			GameObject go = PlaceableCatalog.Spawn(name, null);
			if (go == null) return false;
			try
			{
				go.transform.position = Vector3.zero;
				go.transform.rotation = Quaternion.identity;
				GameObject proto = PlaceableCatalog.Get(name);
				if (proto != null) go.transform.localScale = proto.transform.localScale;
				Renderer[] rs = PlacementOptions.ShapeRenderers(go);
				if (rs.Length == 0) return false;
				b = rs[0].bounds;
				foreach (Renderer r in rs) b.Encapsulate(r.bounds);
				measured[name] = b;
				return true;
			}
			finally { UnityEngine.Object.DestroyImmediate(go); }
		}
	}

	/// <summary>
	/// Procedural islands for the editor (Discord roadmap 1.5). The land comes from a layout (a noisy coast with a
	/// beach, peaks and hills; a ring; islets; stacks; a mesa; a marsh; a crescent; twin peaks) or from one of Raft's
	/// own islands' ground, then passes shape it further: peak shape, valleys, height (the top is exactly the highest
	/// point asked for), terraces, erosion, lakes, and the seabed. Textures are painted automatically; objects are
	/// scattered by zone (under water, wet sand, beach, land, rocky ground) and kind (trees, bushes, rocks, beach
	/// things, harvestables, water plants), on a spatial grid so the densest settings make a jungle; creature spots
	/// and tiered loot boxes are placed last. Generating replaces the island and is one undo step. Deterministic.
	/// </summary>
	public static class IslandGenerator
	{
		/// <summary>The settings the generator window opens with: the last ones generated, at first an island like Raft's small ones.</summary>
		public static IslandGenSettings Last;
		// (set here, after every static field: RaftLike reaches HabitatOf, whose cache is declared further down and was still
		// null when the catalog was already built - remaking a generated island in a world then failed with a NullReference)
		static IslandGenerator() { Last = IslandGenSettings.RaftLike(); }
		/// <summary>What the last generation made.</summary>
		public static GenReport LastReport = new GenReport();

		/// <summary>Beach shelf height above sea level (m) that the land profile rises to before the hills start.</summary>
		public const float ShelfHeight = 2.5f;
		/// <summary>Most objects one island gets (a denser island's objects are scaled down to this; see README for the measured costs).</summary>
		public const int MaxObjects = 12000;

		/// <summary>Terrain height (m above its base) of the sea for the island being generated: every entry point sets it from its settings (UseSea).</summary>
		static float Sea = IslandFile.DefaultWaterLevel;
		/// <summary>Deep sea floor (like Raft's): a shelf, then a drop-off to the floor at the terrain's base.</summary>
		static bool deep;
		/// <summary>Deep sea floor: metres over which the drop-off falls from the shelf's edge to the floor.</summary>
		static float dropWidth = 100f;
		static Vector2 seaOffA, seaOffB;

		static void UseSea(IslandGenSettings s)
		{
			Sea = s.WaterLevel;
			deep = s.Deep;
			// (bigger islands have longer slopes, as on Raft)
			dropWidth = DropWidthOf(s);
			var rnd = new System.Random(s.Seed * 211 + 29);
			seaOffA = RandomOffset(rnd); seaOffB = RandomOffset(rnd);
		}

		#region Objects of each style

		/// <summary>Which catalog objects a style scatters, by kind (names from PlaceableCatalog's core objects; missing ones are skipped).</summary>
		class StylePools
		{
			public Regex ShoreTrees, Trees, Bushes, Rocks, Beach, LandHarvest, SeaHarvest, Water;
		}

		static readonly Regex Corals = new Regex(@"^(Coral\d+|LeafCoral_\d+|TableCoral_\d+|CauliCoral|CylinderCoral_\d+|SpineCoral_\d+|SeaVine3|seavine_tongue|SeaVine3_klump)$");
		static readonly Regex SeaOres = new Regex(@"^Pickup_Landmark_(Iron \d+|Copper \d+|Scrap \d+_OceanBottom)$");
		static readonly Regex Nothing = new Regex(@"^$^");

		/// <summary>Corals and reef things of the object list (map types dress sunken islands with them).</summary>
		internal static string[] CoralNames() { return PlaceableCatalog.GenNames.Where(n => (Corals.IsMatch(n) || Regex.IsMatch(n, @"^Reef_(Barrel\d+|Container)$")) && !n.StartsWith("Pickup_")).ToArray(); }

		// Indexed like TerrainPainter.Styles. (BigRock_Low*_Sand is left out: those are cliff-sized formations that swamp a generated island)
		/// <summary>Some style's pools pick this name (tests: CIGenNames).</summary>
		internal static bool InAnyPool(string name)
		{
			return Pools.Any(p => new[] { p.ShoreTrees, p.Trees, p.Bushes, p.Rocks, p.Beach, p.LandHarvest, p.SeaHarvest, p.Water }.Any(r => r != null && r.IsMatch(name)));
		}

		static readonly StylePools[] Pools =
		{
			new StylePools // Tropical
			{
				ShoreTrees = new Regex(@"^(Pickup_Landmark_Tree_Palm \d+|BigPalm\d+)$"),
				Trees = new Regex(@"^(Pickup_Landmark_Tree_Palm \d+|BigPalm\d+|Pickup_Landmark_MangoTree|Pickup_Landmark_Tree_Mango|Pickup_Landmark_Tree_Banana|Bamboo_\d+)$"),
				Bushes = new Regex(@"^(Bush2?|Monstera_\d+|Banana_Bush_\d+|Bamboo_\d+|Pickup_Landmark_Flower_(Black|Blue|Red|White|Yellow))$"),
				Rocks = new Regex(@"^(BigBoulder\d+_Low|SmallBoulder\d+)$"),
				Beach = new Regex(@"^(Log|SmallBoulder\d+)$"),
				LandHarvest = new Regex(@"^Pickup_Landmark_(Rock \d+|BerryBush|Clay \d+|Sand|PineappleLandmark|WatermelonLandmark)$"),
				SeaHarvest = SeaOres, Water = Corals,
			},
			new StylePools // Snowy
			{
				ShoreTrees = Nothing,
				Trees = new Regex(@"^TP_PineTreeSnowy$"),
				Bushes = new Regex(@"^TP_SnowDrift0\d$"),
				Rocks = new Regex(@"^(TP_BigRock0[2-4]|TP_SmallRock0\d|TP_StalagmiteCluster0\d_Snow)$"),
				Beach = new Regex(@"^(TP_SmallRock0\d|TP_IceShore_Small\d|TP_SnowDrift0\d)$"),
				LandHarvest = new Regex(@"^Pickup_Landmark_(Rock \d+|Clay \d+)$"),
				SeaHarvest = SeaOres,
				Water = new Regex(@"^(TP_SmallRock0\d|SeaVine3|SeaVine3_klump|seavine_tongue)$"),
			},
			new StylePools // Desert
			{
				ShoreTrees = new Regex(@"^SmallBushyTree_\d+$"),
				Trees = new Regex(@"^(Cactus\w+|SmallBushyTree_\d+)$"),
				Bushes = new Regex(@"^(BigBush_\d+|SmallBush_\d+|DesertFern_\d+|CaravanIsland_(Yellow|Green|Brown)Grass)$"),
				Rocks = new Regex(@"^(BigSharpRock_\d+|CaravanIsland_SmallRock_\d+)$"),
				Beach = new Regex(@"^(CaravanIsland_SmallRock_\d+|Log)$"),
				LandHarvest = new Regex(@"^Pickup_Landmark_(PineappleLandmark|Sand_Caravan|Rock \d+|Clay \d+)$"),
				SeaHarvest = SeaOres, Water = Corals,
			},
			new StylePools // Forest
			{
				ShoreTrees = new Regex(@"^(BirchTree_Small\d|PineTree_Small\d)$"),
				Trees = new Regex(@"^(BirchTree_\w+|PineTree_\w+|Pickup_Landmark_Tree_(Pine|Birch))$"),
				Bushes = new Regex(@"^(Balboa_(Big)?Bush_\d+ Variant|Tree_Stump|TreeLog_\d+)$"),
				Rocks = new Regex(@"^(BigRock_\d+|SmallRock_\d+)$"),
				Beach = new Regex(@"^(SmallRock_\d+|TreeLog_\d+|Log)$"),
				LandHarvest = new Regex(@"^Pickup_Landmark_(Rock \d+|Clay \d+|Sand|BerryBush)$"),
				SeaHarvest = SeaOres, Water = Corals,
			},
			new StylePools // Volcanic (no Temperance rocks anywhere: they have snow on them)
			{
				ShoreTrees = Nothing,
				Trees = new Regex(@"^SmallBushyTree_\d+$"),
				Bushes = new Regex(@"^(DesertFern_\d+|SmallBush_\d+|CaravanIsland_BrownGrass)$"),
				Rocks = new Regex(@"^(BigSharpRock_\d+|CaravanIsland_SmallRock_\d+)$"),
				Beach = new Regex(@"^CaravanIsland_SmallRock_\d+$"),
				LandHarvest = new Regex(@"^Pickup_Landmark_(Iron \d+|Copper \d+|Rock \d+)$"),
				SeaHarvest = new Regex(@"^Pickup_Landmark_(Scrap \d+_OceanBottom|Iron \d+)$"),
				Water = new Regex(@"^(TableCoral_\d+|SeaVine3|SeaVine3_klump)$"),
			},
		};

		/// <summary>The kinds of scattered objects, in the order they are placed (big things first, then what fills the gaps).</summary>
		public const string CatTrees = "trees", CatRocks = "rocks", CatBushes = "bushes", CatHarvest = "harvest", CatBeach = "beach";
		/// <summary>Under water (placed from Raft's measured islands, RaftUnderwater): corals and sea plants, rocks, things to collect, sunken barrels.</summary>
		public const string CatWater = "water", CatSeaRocks = "searocks", CatSeaFinds = "seafinds", CatSunken = "sunken";
		public static readonly string[] LandCategories = { CatTrees, CatRocks, CatBushes, CatHarvest, CatBeach };
		public static readonly string[] SeaCategories = { CatSeaRocks, CatSunken, CatSeaFinds, CatWater };
		public static readonly string[] Categories = { CatTrees, CatRocks, CatBushes, CatHarvest, CatBeach, CatWater, CatSeaRocks, CatSeaFinds, CatSunken };

		public static string CategoryLabel(string cat)
		{
			switch (cat)
			{
				case CatTrees: return "trees";
				case CatRocks: return "rocks";
				case CatBushes: return "bushes and plants";
				case CatHarvest: return "harvestables";
				case CatBeach: return "beach things";
				case CatSeaRocks: return "rocks under water";
				case CatSeaFinds: return "finds under water";
				case CatSunken: return "sunken barrels";
				default: return "corals and sea plants";
			}
		}

		/// <summary>Objects per 1000 m² of their zones at a slider's top (the slider squares its value: half way is a quarter of this).</summary>
		static float MaxDensity(string cat)
		{
			switch (cat)
			{
				case CatTrees: return 110f;   // about 3 m apart: a jungle you squeeze through
				case CatBushes: return 220f;
				case CatRocks: return 45f;
				case CatHarvest: return 25f;
				case CatBeach: return 30f;
				default: return 90f;         // a dense coral reef
			}
		}

		/// <summary>A category's slider in the settings.</summary>
		/// <summary>A land slider's value: its own, or (at -1) its kind's Like Raft for the style, as thick again as ObjectDensity
		/// says (0.5 = as on Raft's islands, 1 = twice as thick, 0 = none).</summary>
		static float LandAmount(IslandGenSettings s, float own, string cat)
		{
			if (own >= 0f) return Mathf.Clamp01(own);
			return s.ObjectDensity <= 0.001f ? 0f : Mathf.Clamp01(LikeRaftAmount(s.Style, cat) * Mathf.Sqrt(2f * s.ObjectDensity));
		}

		public static float AmountOf(IslandGenSettings s, string cat)
		{
			switch (cat)
			{
				case CatTrees: return LandAmount(s, s.Trees, cat);
				case CatBushes: return LandAmount(s, s.Bushes, cat);
				case CatRocks: return LandAmount(s, s.Rocks, cat);
				case CatHarvest: return LandAmount(s, s.Harvest, cat);
				case CatBeach: return LandAmount(s, s.BeachThings, cat);
				case CatSeaRocks: return s.AmountSea(s.SeaRocks);
				case CatSeaFinds: return s.AmountSea(s.SeaFinds);
				case CatSunken: return s.AmountSea(s.Sunken);
				default: return s.AmountSea(s.Water);
			}
		}

		/// <summary>How many times as dense as around Raft's islands an under-water slider makes its kind (0.5 = as on Raft, 1 = twice
		/// that: three times carpeted a shallow lagoon's floor with corals, nothing like Raft's own reefs).</summary>
		public static float SeaFactor(float amount) { return amount <= 0f ? 0f : amount <= 0.5f ? Mathf.Pow(2f * amount, 1.6f) : 2f * amount; }

		/// <summary>The Nature quick button Like Raft: each land slider where its kind is as thick on the ground as on Raft's own
		/// islands of the style (LikeRaftAmount) - Raft's small islands' way on a small island, its big ones' on a big one.</summary>
		public static void NatureLikeRaft(IslandGenSettings s)
		{
			s.Trees = LikeRaftAmount(s.Style, CatTrees);
			s.Bushes = LikeRaftAmount(s.Style, CatBushes);
			s.Rocks = LikeRaftAmount(s.Style, CatRocks);
			s.Harvest = LikeRaftAmount(s.Style, CatHarvest);
			s.BeachThings = LikeRaftAmount(s.Style, CatBeach);
		}

		/// <summary>How thick a kind usually stands on the land of Raft's small (or big) islands of a style, per 1000 m² (0: not measured).</summary>
		public static float TypicalDensity(int style, string cat, bool small)
		{
			Habitat h = HabitatOf(style, cat, small);
			return h != null ? h.Typical : 0f;
		}

		/// <summary>
		/// A land slider's Like Raft for a style: the value at which its kind stands as thick on the ground as on Raft's big
		/// islands of the style (raft_land.txt; a small island then gets its small islands' density by itself), allowing for
		/// the spots the generator can't use. 0.33 without the measurements.
		/// </summary>
		public static float LikeRaftAmount(int style, string cat)
		{
			if (cat == CatBeach) return 0.3f; // (the beach keeps the zone rule: Raft has a few logs, mostly inland)
			Habitat h = HabitatOf(style, cat, false);
			if (h != null) return Mathf.Clamp01(Mathf.Sqrt(h.Typical * PlacingLoss(cat) / MaxDensity(cat)));
			// (too few of the kind on Raft's islands of the style to say where it grows - Temperance's bushes, Balboa's
			// pickups: as few as there, which is next to none)
			LandStyle land = RaftLand.For(style);
			float area = land.Area.Sum();
			if (land.Things.Count == 0 || area < 1f) return 0.33f;
			return Mathf.Clamp01(Mathf.Sqrt(land.CountOf(cat) * 1000f / area * PlacingLoss(cat) / MaxDensity(cat)));
		}

		/// <summary>
		/// How much more of a kind Like Raft asks for than its habitats alone would give: a generated island has more land
		/// a kind grows poorly on (cliffs, high ground, bare beach) than Raft's own, so the same habitat densities came out
		/// thinner over its land. Measured with CIGenLikeRaft on islands of the size and height of Raft's big ones.
		/// </summary>
		static float PlacingLoss(string cat)
		{
			switch (cat)
			{
				case CatTrees: return 1.28f;
				case CatBushes: return 1.69f;
				case CatRocks: return 1.2f;
				case CatHarvest: return 2.38f;
				default: return 1f;
			}
		}

		/// <summary>
		/// On a small island, how much more again (on top of Raft's small islands being thicker with things): a small
		/// generated island has more bare beach and steep shore for its size than Raft's small ones. Measured with
		/// CIGenLikeRaft on islands of the size and height of Raft's small ones.
		/// </summary>
		static float SmallBoost(string cat)
		{
			switch (cat)
			{
				case CatTrees: return 1.66f;
				case CatBushes: return 1.69f;
				case CatRocks: return 1.39f;
				default: return 1f;
			}
		}

		/// <summary>The Life under water quick button Like Raft: as dense as around Raft's own islands.</summary>
		public static void SeaLikeRaft(IslandGenSettings s) { s.Water = s.SeaRocks = s.SeaFinds = s.Sunken = 0.5f; }

		/// <summary>The slider value that gives this many objects per 1000 m² (the inverse of the density curve).</summary>
		public static float AmountFor(string cat, float perThousand) { return Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, perThousand) / MaxDensity(cat))); }

		#endregion

		#region Generating in the editor

		/// <summary>Generates into the editor terrain (and scatters objects) as one undoable step. Returns how many objects were placed.</summary>
		public static int GenerateInEditor(IslandGenSettings settings)
		{
			float t0 = Time.realtimeSinceStartup;
			IslandGenSettings s = settings.Copy();
			s.Clamp();
			Last = s.Copy();
			Terrain terrain = terraineditor.terrain;
			// (the full build area: after opening a small island the new one was squeezed into its size. Undo can't give the
			// smaller ground back - it came back flat, with the old objects floating over it: that island stays in its file,
			// its unsaved changes go to its autosave, and the undo history starts here)
			bool resized = DynamicIslands.ResetBuildArea();
			// (LM7: a variation of a Raft island bigger than the build area - Temperance - gets a terrain its real size)
			Vector3 area = AreaFor(s);
			bool bigger = area.x > terrain.terrainData.size.x + 0.5f;
			if (bigger) { terrain.terrainData.size = area; resized = true; }
			if (resized)
			{
				terraineditor.paintMask = null;
				DynamicIslands.KeepUnsaved();
				UndoRedoManager.Clear();
				DynamicIslands.Notify(bigger ? "The new island is " + area.x.ToString("F0") + " m across, as big as the Raft island it comes from: Undo can't bring the island before back (it is still in its file)"
					: "The new island uses the editor's full build area: Undo can't bring the smaller island back (it is still in its file)");
			}
			TerrainData data = terrain.terrainData;
			int hres = data.heightmapResolution, ares = data.alphamapResolution;

			// Before-snapshots for undo
			float[,] heightsBefore = data.GetHeights(0, 0, hres, hres);
			float[,,] alphaBefore = data.GetAlphamaps(0, 0, ares, ares);
			float[,] maskBefore = terraineditor.paintMask != null ? (float[,])terraineditor.paintMask.Clone() : null;

			int styleBefore = DynamicIslands.currentStyle;
			float levelBefore = DynamicIslands.EditorWaterLevel;
			DynamicIslands.SetEditorStyle(s.Style); // before painting, so the style's textures go on
			DynamicIslands.SetEditorWaterLevel(s.WaterLevel);
			float[,] metres = HeightsMetres(s, data.size, hres);
			UseSea(s);
			// (the objects planned first, with the buildings and caves, which level the ground under them: the terrain then
			// gets the heights they leave, in the same undo step)
			var report = new GenReport();
			IslandFile planned = null;
			float[,] heights = Normalised(metres, data.size.y);
			if (PlaceableCatalog.IsBuilt)
			{
				planned = new IslandFile { WaterLevel = s.WaterLevel, TerrainSize = data.size, HeightmapResolution = hres, Heights = heights };
				planned.Objects = PlanAll(s, metres, data.size, report);
				report.Built = GenBuildings.Apply(planned, s);
				GenGather.Apply(planned, s, s.Seed); // (as CreateFile: the same seed and settings, the same island)
				heights = planned.Heights;
			}
			data.SetHeights(0, 0, heights);
			// A new island starts with automatic texturing everywhere
			if (terraineditor.paintMask == null || terraineditor.paintMask.GetLength(0) != ares) terraineditor.paintMask = new float[ares, ares];
			else Array.Clear(terraineditor.paintMask, 0, terraineditor.paintMask.Length);
			TerrainPainter.Setup(terrain, terrain.transform.position.y + s.WaterLevel, terraineditor.paintMask);

			var group = new CommandGroup();
			group.Add(new StyleCommand(styleBefore, s.Style)); // first in, so undo restores the old style last
			group.Add(new WaterLevelCommand(levelBefore, s.WaterLevel));
			group.Add(new TerrainStrokeCommand(data, new RectInt(0, 0, hres, hres), heightsBefore, new RectInt(0, 0, ares, ares), alphaBefore, maskBefore, terraineditor.paintMask));

			// The old objects go (hidden, so undo brings them back), the new ones come
			Transform placed = GameObject.Find("PlacedObjects") != null ? GameObject.Find("PlacedObjects").transform : null;
			var made = new List<GameObject>();
			if (placed != null)
			{
				if (DynamicIslands.EditorGizmoHandler != null) DynamicIslands.EditorGizmoHandler.ClearTargets(false);
				// (earlier islands' hidden objects are cleared first: cleared after, they took this island's old objects too,
				// and undoing this step brought its land back without them)
				PurgeHidden(placed);
				List<GameObject> old = placed.GetComponentsInChildren<EditorGameObject>(false).Select(e => e.gameObject).ToList();
				if (resized) foreach (GameObject go in old) UnityEngine.Object.Destroy(go);
				else
				{
					foreach (GameObject go in old) go.SetActive(false);
					if (old.Count > 0) group.Add(new ObjectVisibilityCommand(old, false));
				}

				if (planned != null)
				{
					IslandFile file = planned;
					var holder = new GameObject("GeneratedObjects");
					holder.transform.SetParent(placed, false);
					holder.transform.position = terrain.transform.position;
					IslandSpawner.SpawnObjects(file, holder.transform, true);
					made = holder.GetComponentsInChildren<EditorGameObject>(true).Select(e => e.gameObject).ToList();
				}
				else Debug.LogWarning("[CUSTOM ISLANDS] Objects are still loading; generated the terrain without objects");
			}
			if (made.Count > 0) group.Add(new ObjectVisibilityCommand(made, true));

			// (a generated quest becomes the island's quest - with its story item - in the same undo step. With no quest
			// steps the old quest goes too: its objects just went - CA22)
			bool dropQuest = s.QuestSteps <= 0 && placed != null && DynamicIslands.currentIslandProps.ContainsKey(IslandQuest.KeySteps);
			if ((s.QuestSteps > 0 && planned != null && planned.Props.ContainsKey(IslandQuest.KeySteps)) || dropQuest)
			{
				IslandFile q = dropQuest ? new IslandFile() : planned;
				if (dropQuest) Debug.Log("[CUSTOM ISLANDS] Generated with no quest steps: the island's old quest went with its objects (undo brings both back)");
				ICommand quest = IslandSettingsUndo.Record(() =>
				{
					foreach (string key in new[] { IslandQuest.KeyTitle, IslandQuest.KeyIntro, IslandQuest.KeySteps, IslandQuest.KeyReward, IslandQuest.KeyDone, StoryItems.Key })
					{
						string v;
						if (q.Props.TryGetValue(key, out v) && v.Length > 0) DynamicIslands.currentIslandProps[key] = v;
						else DynamicIslands.currentIslandProps.Remove(key);
					}
				});
				if (quest != null) group.Add(quest);
			}
			// (not after a resize: the before-heights were taken from the resized terrain, so undo put the old island back
			// stretched over the new size, without its objects - the history starts after this island, as the notice says)
			if (!resized) UndoRedoManager.Insert(group);
			MeasureLand(metres, data.size.x / (hres - 1), report);
			report.Seconds = Time.realtimeSinceStartup - t0;
			LastReport = report;
			Debug.Log(string.Format(CultureInfo.InvariantCulture, "[CUSTOM ISLANDS] Generated island: seed {0}, {1} {2}, radius {3:F0} m, height {4:F0} m, land {5:F0} x {6:F0} m, {7} objects ({8}) in {9:F1} s",
				s.Seed, TerrainPainter.StyleName(s.Style), s.Source.Length > 0 ? "from " + s.Source : IslandShapes.Names[s.Shape], s.Radius, s.Height, report.LandLength, report.LandWidth, made.Count, report.Describe(), report.Seconds));
			return made.Count;
		}

		/// <summary>Hidden objects of earlier generations are kept for undo; past this many they are removed (and undo can't bring them back).</summary>
		const int MaxHiddenObjects = 25000;

		static void PurgeHidden(Transform placed)
		{
			List<EditorGameObject> hidden = placed.GetComponentsInChildren<EditorGameObject>(true).Where(e => !e.gameObject.activeSelf).ToList();
			if (hidden.Count <= MaxHiddenObjects) return;
			foreach (EditorGameObject e in hidden) UnityEngine.Object.Destroy(e.gameObject);
			foreach (Transform t in placed.Cast<Transform>().Where(t => t.name == "GeneratedObjects" && t.GetComponentsInChildren<EditorGameObject>(true).All(e => !e.gameObject.activeSelf)).ToList())
				UnityEngine.Object.Destroy(t.gameObject);
			// (older undo steps pointed at those objects: they would undo only in part - the history goes, and the player is told)
			CommandUndoRedo.UndoRedoManager.Clear();
			Debug.Log("[CUSTOM ISLANDS] Removed " + hidden.Count + " hidden objects of earlier islands (they are kept for undo up to " + MaxHiddenObjects + "); the undo history is cleared");
			DynamicIslands.Notify("Many islands generated: earlier ones are cleared from memory, so Undo goes back to the last island only");
		}

		/// <summary>Points the editor camera at the island from a distance that fits it.</summary>
		public static void FrameCamera(IslandGenSettings s)
		{
			Terrain terrain = terraineditor.terrain;
			if (terrain == null || Camera.main == null) return;
			Vector3 c = terrain.transform.position + new Vector3(terrain.terrainData.size.x / 2f, s.WaterLevel, terrain.terrainData.size.z / 2f);
			// (far enough back for the land and a tall peak)
			float r = Mathf.Max(30f, Mathf.Max(s.Radius * Mathf.Sqrt(Mathf.Max(1f, s.Stretch)), s.Height * 0.9f));
			Transform cam = Camera.main.transform;
			cam.position = c + new Vector3(0, s.Height * 0.8f + r * 0.9f, -r * 1.9f);
			cam.LookAt(c + Vector3.up * (s.Height * 0.3f));
		}

		/// <summary>Size of the land above the sea (along its long axis and across) and its highest point.</summary>
		static void MeasureLand(float[,] m, float step, GenReport report)
		{
			int res = m.GetLength(0), land = 0;
			double sx = 0, sz = 0;
			float top = 0f;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					if (m[z, x] > Sea) { land++; sx += x; sz += z; top = Mathf.Max(top, m[z, x] - Sea); }
			report.LandArea = land * step * step;
			report.Top = top;
			if (land < 3) return;
			double mx = sx / land, mz = sz / land, cxx = 0, czz = 0, cxz = 0;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					if (m[z, x] > Sea) { double dx = x - mx, dz = z - mz; cxx += dx * dx; czz += dz * dz; cxz += dx * dz; }
			cxx /= land; czz /= land; cxz /= land;
			double tr = cxx + czz, disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - (cxx * czz - cxz * cxz)));
			report.LandLength = (float)(4 * Math.Sqrt(tr / 2 + disc)) * step;
			report.LandWidth = (float)(4 * Math.Sqrt(Math.Max(0, tr / 2 - disc))) * step;
		}

		#endregion

		#region Heights

		/// <summary>Normalised heightmap (0..1 of the terrain height) for a terrain of this size and resolution.</summary>
		public static float[,] Heights(IslandGenSettings s, Vector3 size, int res) { return Normalised(HeightsMetres(s, size, res), size.y); }

		static float[,] Normalised(float[,] m, float height)
		{
			int res = m.GetLength(0);
			var h = new float[res, m.GetLength(1)];
			for (int z = 0; z < res; z++)
				for (int x = 0; x < h.GetLength(1); x++)
					h[z, x] = Mathf.Clamp01(m[z, x] / height);
			return h;
		}

		/// <summary>Ground heights in metres above the terrain's base (the sea is at the settings' WaterLevel), for the whole terrain.</summary>
		public static float[,] HeightsMetres(IslandGenSettings s, Vector3 size, int res) { return HeightsMetres(s, size, res, size.x); }

		/// <summary>
		/// Ground heights (m above the terrain's base) on a res x res grid covering span x span metres around the
		/// middle of a terrain of this size (the preview looks at a smaller square than the whole build area).
		/// </summary>
		public static float[,] HeightsMetres(IslandGenSettings settings, Vector3 size, int res, float span)
		{
			IslandGenSettings s = settings.Copy();
			s.Clamp();
			UseSea(s);
			float step = span / (res - 1);
			Func<Vector2, float> shape = ShapeOf(s);
			var m = new float[res, res];
			float a = s.StretchAngle * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a), k = Mathf.Sqrt(s.Stretch);
			float half = span / 2f;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					float px = x * step - half, pz = z * step - half;
					// In the island's own frame: its long axis along x, stretched (the area stays the same)
					m[z, x] = shape(new Vector2((px * ca + pz * sa) / k, (-px * sa + pz * ca) * k));
				}
			var g = new Grid { M = m, Res = res, Step = step, Half = half };
			ShapePeaks(g, s);
			CarveValleys(g, s);
			Normalise(g, s);
			Terrace(g, s);
			if (s.Erosion > 0f) Erode(g, s);
			if (s.Terraces > 0f || s.Erosion > 0f) Normalise(g, s); // (steps and rain lower the top a little)
			if (IslandShapes.FromRaft(s.Shape) && s.Source.Length == 0) { RaftBeach(g, s); Normalise(g, s); }
			CarveLakes(g, s);
			ShapeSeabed(g, s);
			FlattenEdges(g, s, size);
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
					m[z, x] = Mathf.Clamp(m[z, x], 0f, size.y);
			return m;
		}

		/// <summary>A height grid in metres; cell (x, z) is at (x * Step - Half, z * Step - Half) from the middle.</summary>
		class Grid
		{
			public float[,] M;
			public int Res;
			public float Step, Half;
			public Vector2 Pos(int x, int z) { return new Vector2(x * Step - Half, z * Step - Half); }
			public float Max() { float m = float.MinValue; foreach (float v in M) if (v > m) m = v; return m; }
			public float At(float px, float pz)
			{
				float fx = Mathf.Clamp((px + Half) / Step, 0f, Res - 1.001f), fz = Mathf.Clamp((pz + Half) / Step, 0f, Res - 1.001f);
				int x0 = (int)fx, z0 = (int)fz;
				float tx = fx - x0, tz = fz - z0;
				return Mathf.Lerp(Mathf.Lerp(M[z0, x0], M[z0, x0 + 1], tx), Mathf.Lerp(M[z0 + 1, x0], M[z0 + 1, x0 + 1], tx), tz);
			}
		}

		/// <summary>The layout's (or the Raft island's) ground: metres above the terrain's base at a point relative to the middle.</summary>
		static Func<Vector2, float> ShapeOf(IslandGenSettings s)
		{
			if (s.Source.Length > 0)
			{
				RaftIsland island = RaftIslands.Get(s.Source);
				HeightField field = island != null ? RaftIslands.Heights(island) : null;
				if (field != null) return FromRaftIsland(s, island, field);
				Debug.LogWarning("[CUSTOM ISLANDS] No ground heights for Raft island '" + s.Source + "': using the round layout");
			}
			if (IslandShapes.FromRaft(s.Shape))
			{
				RaftIsland island = RaftLayoutIsland(s);
				HeightField field = island != null ? RaftIslands.Heights(island) : null;
				if (field != null) return FromRaftLayout(s, island, field);
				// (no measured islands: a round island with an uneven, stretched coast and bays instead)
				IslandGenSettings round = s.Copy();
				round.Coast = Mathf.Max(round.CoastAmount, 0.6f);
				round.Bays = Mathf.Max(round.Bays, 0.3f);
				return Round(round);
			}
			switch (s.Shape)
			{
				case IslandShapes.Atoll: return Atoll(s);
				case IslandShapes.Archipelago: return Archipelago(s);
				case IslandShapes.Stacks: return Stacks(s);
				case IslandShapes.Plateau: return Plateau(s);
				case IslandShapes.Crescent: return Crescent(s);
				default: return Round(s);
			}
		}

		/// <summary>Smooth 0..1 as x goes from a to b (either way round).</summary>
		static float SS(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

		/// <summary>A pattern around the island: a few soft bumps (0..1) at random directions.</summary>
		class Around
		{
			readonly float[] centres, halfWidths;

			public Around(System.Random rnd, int count, float minHalf, float maxHalf)
			{
				centres = new float[count]; halfWidths = new float[count];
				for (int i = 0; i < count; i++)
				{
					centres[i] = (float)rnd.NextDouble() * Mathf.PI * 2f;
					halfWidths[i] = minHalf + (float)rnd.NextDouble() * (maxHalf - minHalf);
				}
			}

			public float At(float angle)
			{
				float v = 0f;
				for (int i = 0; i < centres.Length; i++)
				{
					float d = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, centres[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
					if (d < halfWidths[i]) v = Mathf.Max(v, 1f - SS(halfWidths[i] * 0.35f, halfWidths[i], d));
				}
				return v;
			}
		}

		/// <summary>
		/// Which stretches of the coast are cliffs (0..1 by direction): a smooth wave around the island, cut so that just
		/// the share asked for is cliff (all of it at 100 %), in a few long stretches with short blends between them.
		/// </summary>
		class CliffMask
		{
			readonly float[] phase = new float[4], amp = new float[4];
			readonly float share, cut, blend;

			public CliffMask(System.Random rnd, float share)
			{
				this.share = share;
				for (int i = 0; i < 4; i++) { phase[i] = (float)rnd.NextDouble() * Mathf.PI * 2f; amp[i] = (0.4f + 0.6f * (float)rnd.NextDouble()) / (i + 1); }
				if (share <= 0.001f || share >= 0.999f) return;
				var v = new float[720];
				for (int i = 0; i < v.Length; i++) v[i] = Wave(i * Mathf.PI * 2f / v.Length);
				Array.Sort(v);
				cut = v[Mathf.Clamp(Mathf.RoundToInt((1f - share) * (v.Length - 1)), 0, v.Length - 1)];
				blend = (v[v.Length - 1] - v[0]) * 0.04f;
			}

			float Wave(float angle) { float w = 0f; for (int i = 0; i < 4; i++) w += amp[i] * Mathf.Sin((i + 1) * angle + phase[i]); return w; }

			public float At(float angle)
			{
				if (share <= 0.001f) return 0f;
				if (share >= 0.999f) return 1f;
				return SS(cut - blend, cut + blend, Wave(angle));
			}
		}

		/// <summary>Width (m) of the beach behind the waterline.</summary>
		static float BeachMetres(IslandGenSettings s) { return Mathf.Lerp(0.03f, 0.3f, s.BeachWidth) * s.Radius + Mathf.Lerp(1f, 6f, s.BeachWidth); }
		/// <summary>Width (m) of the shallow water (down to 8 m deep) off the coast, and of the slope down to the seabed after it.</summary>
		static float ShelfMetres(IslandGenSettings s) { return Mathf.Lerp(0.06f, 0.6f, s.Shelf) * s.Radius + Mathf.Lerp(4f, 30f, s.Shelf); }
		static float DropMetres(IslandGenSettings s) { return 10f + 0.12f * s.Radius; }

		/// <summary>
		/// Ground height d metres off the coast. Shallow seabed: shallow water, then the slope down to the seabed (the
		/// terrain's base, 20 m down). Deep sea floor (DeepDepth): the shelf, then the drop-off to the floor.
		/// </summary>
		static float OffCoast(Vector2 p, float d, float shelf, float drop)
		{
			if (deep) return Mathf.Max(0f, Sea - DeepDepth(p, d, shelf));
			float depth = 8f * SS(0f, shelf, d) + (Sea - 8f) * SS(shelf, shelf + drop, d);
			return Mathf.Max(0f, Sea - depth);
		}

		/// <summary>Depth of the shelf's edge on a deep sea floor (Raft's islands: 8-12 m about 20-30 m out).</summary>
		const float ShelfDepth = 10f;

		/// <summary>
		/// Deep sea floor, measured on Raft's islands (CIMeasureUnderwater): from the coast the shelf deepens evenly to
		/// about ShelfDepth, then the drop-off falls to the floor (Fall). The shelf's width and depth wander along the coast.
		/// </summary>
		static float DeepDepth(Vector2 p, float d, float shelf)
		{
			if (d <= 0f) return 0f;
			float n = Fbm(p / 45f + seaOffA, 3);
			float sh = Mathf.Max(3f, shelf * (1f + 0.45f * n)), edge = ShelfDepth * (1f + 0.2f * n);
			if (d <= sh) return edge * d / sh;
			return Fall(p, d - sh, edge);
		}

		/// <summary>
		/// Deep sea floor: the depth x metres beyond the edge of shallow water that is startDepth deep there. It falls
		/// over dropWidth to the floor (the terrain's base), steepest in the middle, with spurs and gullies running down.
		/// </summary>
		static float Fall(Vector2 p, float x, float startDepth)
		{
			if (x <= 0f) return startDepth;
			float u = x / dropWidth;
			float c = u >= 1f ? 1f : Mathf.Lerp(u, u * u * (3f - 2f * u), 0.5f);
			float depth = startDepth + (Sea - startDepth) * c;
			float ridge = 1f - 2f * Mathf.Abs(Fbm(p / 17f + seaOffB, 3)); // -1..1: spurs (up) and gullies (down)
			depth -= ridge * Mathf.Min(9f, 1f + depth * 0.1f) * SS(0f, 8f, x) * (1f - SS(0.85f, 1.1f, u));
			return Mathf.Clamp(depth, startDepth * 0.8f, Sea);
		}

		/// <summary>Deep sea floor beyond the edge of shallow water (startDepth deep there): a shelf deepening to ShelfDepth over shelfWidth, then the drop-off.</summary>
		static float ShelfThenFall(Vector2 p, float x, float startDepth, float shelfWidth)
		{
			if (x <= 0f) return startDepth;
			if (x <= shelfWidth) return Mathf.Lerp(startDepth, ShelfDepth, x / Mathf.Max(1f, shelfWidth));
			return Fall(p, x - shelfWidth, ShelfDepth);
		}

		static float DropWidthOf(IslandGenSettings s) { return Mathf.Lerp(170f, 35f, s.DropOff) * Mathf.Lerp(0.75f, 1.15f, Mathf.InverseLerp(10f, 120f, s.Radius)); }

		/// <summary>How far from the middle the island's terrain (land, shallow water, slope) can reach (m).</summary>
		public static float Reach(IslandGenSettings s)
		{
			float r;
			if (s.Source.Length > 0)
			{
				RaftIsland i = RaftIslands.Get(s.Source);
				r = i != null ? s.Radius * Mathf.Max(1.2f, i.Shelf / Mathf.Max(1f, i.Radius)) + 25f : s.Radius * 1.6f;
			}
			else if (IslandShapes.FromRaft(s.Shape))
			{
				RaftIsland i = RaftLayoutIsland(s);
				r = i != null ? s.Radius * Mathf.Max(1.2f, i.Shelf / Mathf.Max(1f, i.Radius)) + 25f : s.Radius * 1.6f + 25f;
			}
			else
				switch (s.Shape)
				{
					case IslandShapes.Atoll: case IslandShapes.Archipelago: case IslandShapes.Stacks: case IslandShapes.Crescent: r = s.Radius * 1.45f + 10f; break;
					default: r = s.Radius * (1f + 0.3f * s.CoastAmount) + ShelfMetres(s) + (s.Deep ? 0.15f * DropWidthOf(s) : DropMetres(s)); break; // (the noise seldom reaches its full range)
				}
			return Mathf.Min(r * Mathf.Sqrt(Mathf.Max(1f, s.Stretch)), AreaFor(s).x / 2f);
		}

		/// <summary>The peaks of the round layouts: x, z (from the middle), radius, relative height (the first is the highest).</summary>
		static List<Vector4> PeakSpots(IslandGenSettings s, System.Random rnd)
		{
			var peaks = new List<Vector4>();
			if (s.Shape == IslandShapes.TwinPeaks)
			{
				float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = s.Radius * 0.34f;
				for (int i = 0; i < 2; i++)
				{
					float ai = a + i * Mathf.PI;
					peaks.Add(new Vector4(Mathf.Cos(ai) * d, Mathf.Sin(ai) * d, s.Radius * 0.36f, i == 0 ? 1f : 0.9f));
				}
				return peaks;
			}
			for (int i = 0; i < s.Peaks; i++)
			{
				float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
				float d = i == 0 ? (float)rnd.NextDouble() * s.Radius * 0.25f : (0.2f + 0.3f * (float)rnd.NextDouble()) * s.Radius;
				float r = s.Radius * (0.35f + 0.25f * (float)rnd.NextDouble());
				float h = i == 0 ? 1f : 0.45f + 0.45f * (float)rnd.NextDouble(); // the first peak is the highest
				peaks.Add(new Vector4(Mathf.Cos(a) * d, Mathf.Sin(a) * d, r, h));
			}
			return peaks;
		}

		/// <summary>One peak's shape at squared distance q (in peak radii): a rounded hill, blending into a spire as PeakShape grows.</summary>
		static float PeakProfile(float q, float peakShape)
		{
			float hill = Mathf.Exp(-q * 1.6f);
			if (peakShape <= 0.3f) return hill;
			float spire = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Sqrt(q) * 0.85f), 1.3f + 1.7f * peakShape);
			return Mathf.Lerp(hill, spire, SS(0.3f, 1f, peakShape));
		}

		/// <summary>
		/// One island (also the marsh and twin peaks): a noisy coast (with bays and cliffs), a beach, peaks and hills
		/// behind it; shallow water off the coast, then a slope to the seabed.
		/// </summary>
		static Func<Vector2, float> Round(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed);
			// Noise offsets make each seed a different part of the noise field
			Vector2 coastOffset = RandomOffset(rnd), hillOffset = RandomOffset(rnd), warpOffset = RandomOffset(rnd);
			List<Vector4> peaks = PeakSpots(s, rnd);
			var bays = new Around(new System.Random(s.Seed * 17 + 3), 1 + Mathf.RoundToInt(s.Bays * 4f), 0.14f, 0.3f);
			var cliffs = new CliffMask(new System.Random(s.Seed * 29 + 11), s.Cliffs);

			bool volcanic = s.Style == TerrainPainter.Volcanic, marsh = s.Shape == IslandShapes.Marsh;
			float coast = s.CoastAmount;
			float coastScale = 1f / Mathf.Max(20f, s.Radius * 0.6f);
			float hillScale = 1f / Mathf.Clamp(s.Radius * 0.45f, 12f, 45f);
			float beach = BeachMetres(s), shelf = ShelfMetres(s), drop = DropMetres(s);
			float cliffTop = Mathf.Clamp(s.Height * 0.4f, 3f, 30f);
			float rise = Mathf.Max(0f, s.Height - ShelfHeight);
			float ridged = s.Roughness * s.Roughness * 0.6f;

			return p =>
			{
				float dist = p.magnitude;
				// Ragged coastline: the radius varies with low-frequency noise (domain-warped a little)
				Vector2 warp = new Vector2(Fbm(p * coastScale + warpOffset, 2), Fbm(p * coastScale + warpOffset + new Vector2(17.3f, 5.1f), 2));
				float cn = Fbm((p + warp * s.Radius * 0.25f * coast) * coastScale + coastOffset, 4);
				float cf = 1f + 0.45f * coast * cn;
				float angle = Mathf.Atan2(p.y, p.x);
				if (s.Bays > 0f) cf *= 1f - 0.65f * s.Bays * bays.At(angle);
				float d = dist - s.Radius * cf; // metres beyond the coast (negative: inland)
				float cliff = s.Cliffs > 0f ? cliffs.At(angle) : 0f;
				if (d >= 0f) return OffCoast(p, d, Mathf.Lerp(shelf, 2f, cliff), drop);

				float u = -d;
				float ground = ShelfHeight * SS(0f, beach, u);
				if (cliff > 0f) ground = Mathf.Lerp(ground, cliffTop * SS(0f, 2f + 0.03f * s.Radius, u), cliff);
				float e = Sea + ground;

				// Peaks and hills, behind the beach
				float inland = SS(beach * 0.5f, beach + 0.4f * s.Radius, u);
				if (inland <= 0f) return e;
				float mountain = 0f;
				for (int i = 0; i < peaks.Count; i++)
				{
					Vector4 pk = peaks[i];
					float dx = p.x - pk.x, dz = p.y - pk.y;
					float q = (dx * dx + dz * dz) / (pk.z * pk.z);
					float v;
					if (volcanic)
					{
						// A straight-sided cone, and a crater in the main one
						float r = Mathf.Sqrt(q);
						v = pk.w * Mathf.Pow(Mathf.Max(0f, 1f - r * 0.85f), 1.4f);
						if (i == 0 && r < 0.2f) v -= pk.w * 0.45f * (1f - r / 0.2f);
					}
					else v = pk.w * PeakProfile(q, s.PeakShape);
					mountain = Mathf.Max(mountain, v);
				}
				float hills = Fbm(p * hillScale + hillOffset, 4); // -1..1
				if (ridged > 0f) hills = Mathf.Lerp(hills, 1f - 2f * Mathf.Abs(hills), ridged);
				float bump = mountain * (1f + 0.35f * s.Roughness * hills) + 0.18f * s.Roughness * (hills * 0.5f + 0.5f);
				e += inland * Mathf.Max(0f, bump) * rise;
				// A marsh has pools of shallow water in its low land
				if (marsh) e = Mathf.Max(Sea - 1.5f, e - inland * Mathf.Max(0f, Fbm(p / 22f + warpOffset * 1.7f, 3) - 0.05f) * 14f);
				return e;
			};
		}

		/// <summary>A ring of low land (dunes up to the height setting) around a shallow lagoon, with a channel or two into it.</summary>
		static Func<Vector2, float> Atoll(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed);
			Vector2 coastOff = RandomOffset(rnd), hillOff = RandomOffset(rnd), floorOff = RandomOffset(rnd);
			float ring = s.Radius * 0.75f, width = s.Radius * (0.16f + 0.1f * s.CoastAmount);
			float shelfW = ShelfMetres(s) * 0.6f;
			float coastScale = 1f / Mathf.Max(20f, s.Radius * 0.5f);
			return p =>
			{
				float dist = p.magnitude;
				float coast = Fbm(p * coastScale + coastOff, 3);
				float d = Mathf.Abs(dist - ring) / Mathf.Max(4f, width * (1f + 0.6f * coast));
				float top = (1f - SS(0.55f, 1.25f, d)) * SS(-0.5f, -0.3f, coast);
				float dunes = top * Mathf.Max(0f, Fbm(p / 30f + hillOff, 3) * 0.5f + 0.5f) * Mathf.Max(0f, s.Height - ShelfHeight);
				float floor = dist < ring ? Sea - 2f - 2.5f * (Fbm(p / 40f + floorOff, 2) * 0.5f + 0.5f) 
					: deep ? Sea - ShelfThenFall(p, dist - ring - Mathf.Max(4f, width * (1f + 0.6f * coast)), 2f, shelfW) : Mathf.Lerp(0f, Sea - 2f, 1f - SS(1f, 3.2f, d));
				return Mathf.Lerp(floor, Sea + ShelfHeight, top) + dunes;
			};
		}

		/// <summary>Archipelago islets: x, z (relative to the middle), radius, height. The first is the biggest, near the middle.</summary>
		public static List<Vector4> Islets(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed * 101 + 3);
			int n = 3 + rnd.Next(4);
			var list = new List<Vector4>();
			for (int i = 0; i < n * 12 && list.Count < n; i++)
			{
				float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
				float d = list.Count == 0 ? (float)rnd.NextDouble() * s.Radius * 0.2f : s.Radius * (0.4f + 0.45f * (float)rnd.NextDouble());
				float r = s.Radius * (list.Count == 0 ? 0.28f : 0.14f + 0.12f * (float)rnd.NextDouble());
				var c = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
				if (list.Any(o => (new Vector2(o.x, o.y) - c).magnitude < (o.z + r) * 1.2f)) continue; // water between them
				float h = Mathf.Max(ShelfHeight + 2f, s.Height * (list.Count == 0 ? 1f : 0.35f + 0.5f * (float)rnd.NextDouble()));
				list.Add(new Vector4(c.x, c.y, r, h));
			}
			return list;
		}

		/// <summary>Several islets (Islets) on a shallow shelf that joins them under water.</summary>
		static Func<Vector2, float> Archipelago(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed);
			Vector2 coastOff = RandomOffset(rnd), hillOff = RandomOffset(rnd);
			List<Vector4> islets = Islets(s);
			float coastAmount = s.CoastAmount, shelfW = ShelfMetres(s) * 0.6f;
			return p =>
			{
				float shelf = 1f - SS(0.95f, 1.4f, p.magnitude / s.Radius);
				float floor = deep ? Sea - ShelfThenFall(p, p.magnitude - 0.95f * s.Radius, 2.5f, shelfW) : Mathf.Lerp(0f, Sea - 2.5f, shelf), best = floor;
				float coast = Fbm(p / Mathf.Max(15f, s.Radius * 0.25f) + coastOff, 3);
				foreach (Vector4 o in islets)
				{
					float q = (p - new Vector2(o.x, o.y)).magnitude / (o.z * (1f + 0.35f * coastAmount * coast));
					float land = 1f - SS(0.7f, 1.25f, q);
					if (land <= 0f) continue;
					float hill = Mathf.Exp(-q * q * 2.2f) * (1f + 0.3f * s.Roughness * Fbm(p / 25f + hillOff, 3));
					best = Mathf.Max(best, Mathf.Lerp(floor, Sea + ShelfHeight, land) + land * Mathf.Max(0f, hill) * (o.w - ShelfHeight));
				}
				return best;
			};
		}

		/// <summary>Sea stacks: x, z (relative to the middle), radius, height above the sea. The first is the tallest.</summary>
		public static List<Vector4> StackSpots(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed * 97 + 5);
			int n = 3 + rnd.Next(5);
			var list = new List<Vector4>();
			for (int i = 0; i < n * 12 && list.Count < n; i++)
			{
				float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
				float d = list.Count == 0 ? (float)rnd.NextDouble() * s.Radius * 0.25f : s.Radius * (0.2f + 0.55f * (float)rnd.NextDouble());
				float r = Mathf.Clamp(s.Radius * (0.09f + 0.09f * (float)rnd.NextDouble()), 6f, 26f);
				var c = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
				if (list.Any(o => (new Vector2(o.x, o.y) - c).magnitude < (o.z + r) * 1.4f)) continue;
				list.Add(new Vector4(c.x, c.y, r, s.Height * (list.Count == 0 ? 1f : 0.45f + 0.5f * (float)rnd.NextDouble())));
			}
			return list;
		}

		/// <summary>Steep rock pillars with flat tops (StackSpots), a rocky beach at their feet, on a shallow shelf.</summary>
		static Func<Vector2, float> Stacks(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed);
			Vector2 edgeOff = RandomOffset(rnd), capOff = RandomOffset(rnd);
			List<Vector4> stacks = StackSpots(s);
			float coastAmount = s.CoastAmount, shelfW = ShelfMetres(s) * 0.6f;
			return p =>
			{
				float plate = 1f - SS(0.8f, 1.25f, p.magnitude / s.Radius);
				float floor = deep ? Sea - ShelfThenFall(p, p.magnitude - 0.8f * s.Radius, 2.5f, shelfW) : Mathf.Lerp(0f, Sea - 2.5f, plate), e = floor;
				float edge = Fbm(p / 12f + edgeOff, 3);
				foreach (Vector4 o in stacks)
				{
					float q = (p - new Vector2(o.x, o.y)).magnitude / (o.z * (1f + 0.25f * coastAmount * edge));
					if (q > 2f) continue;
					float side = 1f - SS(0.8f, 1f, q);
					float cap = o.w * (1f + 0.04f * Fbm(p / 6f + capOff, 2));
					e = Mathf.Max(e, Mathf.Lerp(floor, Sea + cap, side));
					e = Mathf.Max(e, Mathf.Lerp(floor, Sea + 0.8f, 1f - SS(1f, 1.9f, q))); // the rocky beach at its foot
				}
				return e;
			};
		}

		/// <summary>The plateau's ramp: the direction (radians, from +x towards +z) it goes up from the shore.</summary>
		public static float RampAngle(IslandGenSettings s) { return (float)new System.Random(s.Seed * 53 + 1).NextDouble() * Mathf.PI * 2f; }

		/// <summary>A flat-topped mesa (the height setting) with cliffs, a beach around it, and one ramp up from the shore.</summary>
		static Func<Vector2, float> Plateau(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed);
			Vector2 coastOff = RandomOffset(rnd), topOff = RandomOffset(rnd);
			float ramp = RampAngle(s);
			Vector2 rampDir = new Vector2(Mathf.Cos(ramp), Mathf.Sin(ramp));
			float coastScale = 1f / Mathf.Max(20f, s.Radius * 0.6f);
			float coastAmount = s.CoastAmount, shelf = ShelfMetres(s), drop = DropMetres(s);
			return p =>
			{
				float coast = Fbm(p * coastScale + coastOff, 4);
				float cr = s.Radius * (1f + 0.3f * coastAmount * coast);
				float d = p.magnitude - cr;
				if (d >= 0f) return OffCoast(p, d, shelf, drop);
				float rr = p.magnitude / cr;
				float baseLevel = Sea + ShelfHeight * SS(0f, 4f + 0.08f * s.Radius, -d);
				float rise = s.Height - ShelfHeight;
				float cliff = 1f - SS(0.55f, 0.7f, rr);
				float e = baseLevel + cliff * (rise + 0.4f * Fbm(p / 10f + topOff, 2));
				float along = Vector2.Dot(p, rampDir), across = Mathf.Abs(p.x * rampDir.y - p.y * rampDir.x);
				if (along > 0f)
					e = Mathf.Max(e, baseLevel + rise * SS(0.95f, 0.5f, rr) * (1f - SS(5f, 9f, across)));
				return e;
			};
		}

		/// <summary>A curved island (an arc of land with a ridge along it) around a sheltered, shallow bay.</summary>
		static Func<Vector2, float> Crescent(IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed);
			Vector2 coastOff = RandomOffset(rnd), hillOff = RandomOffset(rnd);
			float open = (float)rnd.NextDouble() * 360f;
			float ring = s.Radius * 0.62f, half = s.Radius * 0.33f;
			float coastAmount = s.CoastAmount, shelf = ShelfMetres(s) * 0.7f, drop = DropMetres(s);
			float beach = BeachMetres(s) * 0.6f, rise = Mathf.Max(0f, s.Height - ShelfHeight);
			return p =>
			{
				float dist = p.magnitude;
				float gap = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, open)) / 180f; // 0 in the gap's middle, 1 opposite
				float arc = SS(0.28f, 0.55f, gap);                     // the land's width tapers to the tips
				float coast = Fbm(p / Mathf.Max(18f, s.Radius * 0.35f) + coastOff, 3);
				float width = half * arc * (1f + 0.4f * coastAmount * coast);
				float u = width - Mathf.Abs(dist - ring);              // metres inside the band (> 0 = land)
				if (u <= 0f)
				{
					float e = OffCoast(p, -u, shelf, drop);
					// The bay inside stays shallow
					if (dist < ring) e = Mathf.Max(e, Sea - 2f - 2f * SS(0f, ring * 0.8f, -u));
					return e;
				}
				float ground = Sea + ShelfHeight * SS(0f, beach, u);
				float ridge = SS(beach * 0.5f, Mathf.Max(beach + 1f, width * 0.9f), u) * SS(0.3f, 0.75f, gap);
				float hills = Fbm(p / Mathf.Clamp(s.Radius * 0.3f, 10f, 35f) + hillOff, 4);
				return ground + ridge * rise * Mathf.Max(0f, 0.75f + 0.25f * hills + 0.3f * s.Roughness * hills);
			};
		}

		/// <summary>One of Raft's islands' own ground (RaftIslands), scaled to the size asked for, mirrored, wobbled and roughened.</summary>
		static Func<Vector2, float> FromRaftIsland(IslandGenSettings s, RaftIsland island, HeightField field)
		{
			var rnd = new System.Random(s.Seed);
			Vector2 warpOff = RandomOffset(rnd), roughOff = RandomOffset(rnd);
			float scale = s.Radius / Mathf.Max(1f, island.Radius);
			float warpScale = 1f / Mathf.Max(12f, s.Radius * 0.45f);
			float wobble = s.SourceWobble * 0.2f * island.Radius, bumps = s.SourceRoughen * 0.15f * Mathf.Max(8f, island.Top);
			// (beyond the measured grid there is no data: instead of its edge going on for ever - an endless shelf around
			// islands measured only to shallow water - the ground falls to the sea floor, over a slope as wide as Raft's
			// small islands' (40 m) to its big islands' (150 m))
			float fall = Mathf.Lerp(40f, 150f, Mathf.InverseLerp(10f, 120f, island.Radius));
			return p =>
			{
				Vector2 q = p / scale;
				if (s.Mirror) q.x = -q.x;
				if (wobble > 0f) q += new Vector2(Fbm(p * warpScale + warpOff, 3), Fbm(p * warpScale + warpOff + new Vector2(31.7f, 11.3f), 3)) * wobble;
				float h = field.At(q.x, q.y);
				float outside = Mathf.Max(Mathf.Abs(q.x) - field.HalfX, Mathf.Abs(q.y) - field.HalfZ);
				if (outside > 0f && h < 0f) h = Mathf.Lerp(h, -Sea, SS(0f, fall, outside));
				// (Raft's deep water, down to 160 m, is the terrain's seabed here, 20 m down)
				float e = Sea + h;
				if (bumps > 0f && h > 0f) e += bumps * Fbm(p / 16f + roughOff, 4) * SS(0f, 4f, h);
				return e;
			};
		}

		/// <summary>The Raft small / large island layouts: which of Raft's measured islands the seed picks (null without them).</summary>
		static RaftIsland RaftLayoutIsland(IslandGenSettings s)
		{
			bool small = s.Shape == IslandShapes.RaftSmall;
			List<RaftIsland> pool = RaftIslands.All
				.Where(i => small ? i.Label.StartsWith("Small island") : i.Label.StartsWith("Big island") || i.Label == "Big")
				.Where(i => RaftIslands.Heights(i) != null).OrderBy(i => i.Scene, StringComparer.Ordinal).ToList();
			if (pool.Count == 0) return null;
			return pool[new System.Random(s.Seed * 13 + 5).Next(pool.Count)];
		}

		/// <summary>
		/// A Raft small / large island layout: the picked island's own ground at the size asked for, turned and mirrored by
		/// the seed, its coast wobbled (Coast), bitten into (Bays) and its land roughened (Roughness), so no two seeds match.
		/// </summary>
		static Func<Vector2, float> FromRaftLayout(IslandGenSettings s, RaftIsland island, HeightField field)
		{
			var rnd = new System.Random(s.Seed * 7 + 2);
			float turn = (float)rnd.NextDouble() * Mathf.PI * 2f, ct = Mathf.Cos(turn), st = Mathf.Sin(turn);
			IslandGenSettings t = s.Copy();
			t.Mirror = s.Mirror ^ (rnd.Next(2) == 1);
			t.SourceWobble = Mathf.Max(s.SourceWobble, 0.25f + 0.5f * s.CoastAmount);
			t.SourceRoughen = Mathf.Max(s.SourceRoughen, 0.5f * s.Roughness);
			Func<Vector2, float> ground = FromRaftIsland(t, island, field);
			Around bays = s.Bays > 0f ? new Around(new System.Random(s.Seed * 17 + 3), 1 + Mathf.RoundToInt(s.Bays * 3f), 0.14f, 0.3f) : null;
			return p =>
			{
				var q = new Vector2(p.x * ct - p.y * st, p.x * st + p.y * ct);
				// (a bay: the ground there is looked up further out, so the coast comes in)
				if (bays != null) q *= 1f + 0.55f * s.Bays * bays.At(Mathf.Atan2(q.y, q.x));
				return ground(q);
			};
		}

		/// <summary>
		/// The Raft small / large island layouts: a beach all round (except where cliffs are asked for). The land rises from
		/// the waterline over a beach, then no steeper than a player walks up (so a measured island's cliff becomes a slope),
		/// and the sea bed just off the coast is a shallow shelf a player wades out of: players get on from the raft.
		/// </summary>
		static void RaftBeach(Grid g, IslandGenSettings s)
		{
			int n = g.Res;
			const float Far = 1e6f;
			float[,] inland = new float[n, n], offshore = new float[n, n];
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					bool land = g.M[z, x] > Sea;
					inland[z, x] = land ? Far : 0f;
					offshore[z, x] = land ? 0f : Far;
				}
			Chamfer(inland, n, g.Step);
			Chamfer(offshore, n, g.Step);
			float beach = Mathf.Lerp(3f, 9f, s.BeachWidth), beachTop = Mathf.Min(ShelfHeight, s.Height * 0.5f);
			// (rise per metre inland: just under the 45 degrees a player walks up - steeper only where the height asked for
			// can't be reached that way on so little land; Normalise then brings the top back to the height asked for)
			float deepest = 0f;
			foreach (float u in inland) if (u < Far) deepest = Mathf.Max(deepest, u);
			float Climb = Mathf.Max(0.95f, (s.Height - beachTop) / Mathf.Max(1f, deepest - beach));
			var cliffs = new CliffMask(new System.Random(s.Seed * 29 + 11), s.Cliffs);
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					Vector2 p = g.Pos(x, z);
					float keep = s.Cliffs > 0f ? cliffs.At(Mathf.Atan2(p.y, p.x)) : 0f;
					if (keep >= 0.999f) continue;
					float e = g.M[z, x];
					if (e > Sea)
					{
						float u = inland[z, x];
						float cap = Sea + beachTop * SS(0f, beach, u) + Mathf.Max(0f, u - beach) * Climb;
						if (e > cap) g.M[z, x] = Mathf.Lerp(cap, e, keep);
					}
					else
					{
						float d = offshore[z, x];
						float want = Mathf.Lerp(Sea - 0.2f - 0.3f * d, e, SS(4f, 10f, d));
						if (want > e) g.M[z, x] = Mathf.Lerp(want, e, keep);
					}
				}
		}

		/// <summary>Distances (m) on a grid, in place: cells at 0 are the starts, the rest (very large) get the distance to the nearest start.</summary>
		static void Chamfer(float[,] d, int n, float step)
		{
			float a = step, b = step * 1.41421356f;
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					float v = d[z, x];
					if (x > 0) v = Mathf.Min(v, d[z, x - 1] + a);
					if (z > 0)
					{
						v = Mathf.Min(v, d[z - 1, x] + a);
						if (x > 0) v = Mathf.Min(v, d[z - 1, x - 1] + b);
						if (x < n - 1) v = Mathf.Min(v, d[z - 1, x + 1] + b);
					}
					d[z, x] = v;
				}
			for (int z = n - 1; z >= 0; z--)
				for (int x = n - 1; x >= 0; x--)
				{
					float v = d[z, x];
					if (x < n - 1) v = Mathf.Min(v, d[z, x + 1] + a);
					if (z < n - 1)
					{
						v = Mathf.Min(v, d[z + 1, x] + a);
						if (x < n - 1) v = Mathf.Min(v, d[z + 1, x + 1] + b);
						if (x > 0) v = Mathf.Min(v, d[z + 1, x - 1] + b);
					}
					d[z, x] = v;
				}
		}

		/// <summary>Peak shape over the whole land: below 0.3 fuller, rounder hills; above it sharper tops over flatter lowland.</summary>
		static void ShapePeaks(Grid g, IslandGenSettings s)
		{
			if (Mathf.Abs(s.PeakShape - 0.3f) < 0.01f) return;
			float exp = s.PeakShape < 0.3f ? Mathf.Lerp(0.6f, 1f, s.PeakShape / 0.3f) : Mathf.Lerp(1f, 2.2f, (s.PeakShape - 0.3f) / 0.7f);
			float baseH = Sea + ShelfHeight, top = g.Max();
			if (top <= baseH + 0.5f) return;
			for (int z = 0; z < g.Res; z++)
				for (int x = 0; x < g.Res; x++)
				{
					float e = g.M[z, x];
					if (e <= baseH) continue;
					g.M[z, x] = baseH + Mathf.Pow((e - baseH) / (top - baseH), exp) * (top - baseH);
				}
		}

		/// <summary>Makes the highest point exactly the height asked for (the beach and the sea stay as they are).</summary>
		static void Normalise(Grid g, IslandGenSettings s)
		{
			float baseH = Sea + Mathf.Min(ShelfHeight, s.Height * 0.8f), top = g.Max();
			if (top <= baseH + 0.05f) return;
			float k = (Sea + s.Height - baseH) / (top - baseH);
			for (int z = 0; z < g.Res; z++)
				for (int x = 0; x < g.Res; x++)
				{
					float e = g.M[z, x];
					if (e > baseH) g.M[z, x] = baseH + (e - baseH) * k;
				}
		}

		/// <summary>Steps in the land: flat terraces with steep risers between them.</summary>
		static void Terrace(Grid g, IslandGenSettings s)
		{
			if (s.Terraces <= 0f) return;
			float baseH = Sea + ShelfHeight + 0.5f;
			float stepH = Mathf.Lerp(Mathf.Max(3f, s.Height / 3f), Mathf.Max(2.5f, s.Height / 10f), s.Terraces);
			float w = Mathf.Lerp(0.45f, 0.1f, s.Terraces), amount = Mathf.Min(1f, s.Terraces * 1.5f);
			for (int z = 0; z < g.Res; z++)
				for (int x = 0; x < g.Res; x++)
				{
					float e = g.M[z, x];
					if (e <= baseH) continue;
					float t = (e - baseH) / stepH, fl = Mathf.Floor(t);
					float stepped = baseH + stepH * (fl + SS(0.5f - w, 0.5f + w, t - fl));
					g.M[z, x] = Mathf.Lerp(e, stepped, amount);
				}
		}

		/// <summary>River valleys: from near the highest point, winding down to the sea, cut into the land.</summary>
		static void CarveValleys(Grid g, IslandGenSettings s)
		{
			if (s.Valleys <= 0f) return;
			var rnd = new System.Random(s.Seed * 61 + 17);
			// The highest cell
			int hx = 0, hz = 0; float top = float.MinValue;
			for (int z = 0; z < g.Res; z++) for (int x = 0; x < g.Res; x++) if (g.M[z, x] > top) { top = g.M[z, x]; hx = x; hz = z; }
			if (top < Sea + 3f) return;
			int n = 1 + Mathf.RoundToInt(s.Valleys * 4f);
			float width = Mathf.Lerp(5f, 14f, s.Valleys) + 0.05f * s.Radius;
			float reach = width * 2.5f;
			Vector2 peak = g.Pos(hx, hz);
			float start = rnd.NextDouble() < 0.5 ? 0f : (float)rnd.NextDouble() * Mathf.PI * 2f;
			for (int v = 0; v < n; v++)
			{
				float angle = start + v * Mathf.PI * 2f / n + ((float)rnd.NextDouble() - 0.5f) * 0.6f;
				Vector2 p = peak + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (width + 0.12f * s.Radius * (float)rnd.NextDouble());
				var path = new List<Vector2> { p };
				for (int i = 0; i < 400; i++)
				{
					angle += ((float)rnd.NextDouble() - 0.5f) * 0.45f;
					p += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5f;
					path.Add(p);
					if (Mathf.Abs(p.x) > g.Half || Mathf.Abs(p.y) > g.Half || g.At(p.x, p.y) < Sea - 3f) break;
				}
				if (path.Count < 3) continue;
				float startH = g.At(path[0].x, path[0].y);
				// Cells near the path: lowered to a floor that falls from 70 % of the start to below the sea
				float minX = path.Min(q => q.x) - reach, maxX = path.Max(q => q.x) + reach, minZ = path.Min(q => q.y) - reach, maxZ = path.Max(q => q.y) + reach;
				int x0 = Mathf.Clamp(Mathf.FloorToInt((minX + g.Half) / g.Step), 0, g.Res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxX + g.Half) / g.Step), 0, g.Res - 1);
				int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ + g.Half) / g.Step), 0, g.Res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ + g.Half) / g.Step), 0, g.Res - 1);
				for (int z = z0; z <= z1; z++)
					for (int x = x0; x <= x1; x++)
					{
						float e = g.M[z, x];
						if (e < Sea - 2f) continue;
						Vector2 c = g.Pos(x, z);
						float best = float.MaxValue, at = 0f;
						for (int i = 0; i < path.Count - 1; i++)
						{
							Vector2 a = path[i], ab = path[i + 1] - a;
							float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
							float dd = (a + ab * t - c).sqrMagnitude;
							if (dd < best) { best = dd; at = (i + t) / (path.Count - 1); }
						}
						float d = Mathf.Sqrt(best);
						if (d >= reach) continue;
						float floor = Mathf.Lerp(startH * 0.7f + Sea * 0.3f, Sea - 1.5f, Mathf.Pow(at, 0.7f));
						float target = floor + (e - floor) * SS(0f, reach, d);
						if (target < e) g.M[z, x] = target;
					}
			}
		}

		/// <summary>Inland lakes: bowls in the lower land that go below sea level, so the sea fills them.</summary>
		static void CarveLakes(Grid g, IslandGenSettings s)
		{
			if (s.Lakes <= 0f) return;
			var rnd = new System.Random(s.Seed * 73 + 5);
			int wanted = 1 + Mathf.RoundToInt(s.Lakes * 3f);
			float top = g.Max();
			float maxH = Sea + Mathf.Max(3f, (top - Sea) * 0.45f);
			float size = Mathf.Clamp(s.Radius / 60f, 0.35f, 1.8f);
			var lakes = new List<Vector3>();
			for (int attempt = 0; attempt < 400 && lakes.Count < wanted; attempt++)
			{
				int x = rnd.Next(g.Res), z = rnd.Next(g.Res);
				float e = g.M[z, x];
				float r = Mathf.Lerp(6f, 22f, s.Lakes) * (0.7f + 0.6f * (float)rnd.NextDouble()) * size;
				// (on a small or narrow island big lakes don't fit inland: after half the tries, smaller ones, down to a third)
				if (attempt >= 200) r *= 1f - 0.67f * (attempt - 200) / 200f;
				if (e < Sea + 1.5f || e > maxH) continue;
				Vector2 c = g.Pos(x, z);
				if (lakes.Any(l => (new Vector2(l.x, l.y) - c).magnitude < l.z + r + 8f)) continue;
				// Land all around it (a lake, not a bay)
				bool inland = true;
				for (int k = 0; k < 12 && inland; k++)
				{
					float a = k * Mathf.PI / 6f;
					if (g.At(c.x + Mathf.Cos(a) * (r * 1.4f + 6f), c.y + Mathf.Sin(a) * (r * 1.4f + 6f)) < Sea + 1f) inland = false;
				}
				if (inland) lakes.Add(new Vector3(c.x, c.y, r));
			}
			Vector2 wob = RandomOffset(rnd);
			float bottom = Sea - 1f - 2.5f * s.Lakes;
			foreach (Vector3 l in lakes)
			{
				float reach = l.z * 1.8f;
				int x0 = Mathf.Clamp(Mathf.FloorToInt((l.x - reach + g.Half) / g.Step), 0, g.Res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((l.x + reach + g.Half) / g.Step), 0, g.Res - 1);
				int z0 = Mathf.Clamp(Mathf.FloorToInt((l.y - reach + g.Half) / g.Step), 0, g.Res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((l.y + reach + g.Half) / g.Step), 0, g.Res - 1);
				for (int z = z0; z <= z1; z++)
					for (int x = x0; x <= x1; x++)
					{
						Vector2 c = g.Pos(x, z);
						float t = (c - new Vector2(l.x, l.y)).magnitude / l.z * (1f + 0.2f * Fbm(c / 9f + wob, 2));
						float target = Mathf.Lerp(bottom, g.M[z, x], SS(0.6f, 1.5f, t));
						if (target < g.M[z, x]) g.M[z, x] = target;
					}
			}
		}

		/// <summary>The seabed near the island: sand (as it is), rocky (bumps and outcrops), or a reef ring just under the surface.</summary>
		static void ShapeSeabed(Grid g, IslandGenSettings s)
		{
			if (s.Seabed == IslandGenSettings.SeabedSand) return;
			var rnd = new System.Random(s.Seed * 43 + 9);
			Vector2 off = RandomOffset(rnd), gapOff = RandomOffset(rnd);
			for (int z = 0; z < g.Res; z++)
				for (int x = 0; x < g.Res; x++)
				{
					float e = g.M[z, x], depth = Sea - e;
					if (depth <= 0.5f || depth >= Sea - 1f) continue;
					Vector2 c = g.Pos(x, z);
					if (s.Seabed == IslandGenSettings.SeabedRocky)
					{
						// (fades out before the flat seabed, which must stay flat)
						float mask = SS(0.5f, 3f, depth) * (1f - SS(12f, 17f, depth));
						g.M[z, x] = Mathf.Min(Sea - 0.6f, e + mask * 2.6f * Fbm(c / 7f + off, 3));
					}
					else
					{
						float band = SS(3.5f, 5.5f, depth) * (1f - SS(9f, 12f, depth));
						float gaps = SS(-0.25f, 0.1f, Fbm(c / 40f + gapOff, 2));
						float reef = Sea - 0.8f - 0.9f * Mathf.Abs(Fbm(c / 6f + off, 2));
						g.M[z, x] = Mathf.Max(e, Mathf.Lerp(e, reef, band * gaps));
					}
				}
		}

		/// <summary>The edge of the build area is always flat seabed (a big, stretched island is cut off before it).</summary>
		static void FlattenEdges(Grid g, IslandGenSettings s, Vector3 size)
		{
			float hx = size.x / 2f, hz = size.z / 2f;
			for (int z = 0; z < g.Res; z++)
				for (int x = 0; x < g.Res; x++)
				{
					Vector2 c = g.Pos(x, z);
					float edge = Mathf.Min(hx - Mathf.Abs(c.x), hz - Mathf.Abs(c.y));
					if (edge < 70f) g.M[z, x] *= SS(8f, 70f, edge);
				}
		}

		/// <summary>Hydraulic erosion: raindrops run downhill, carry soil away from slopes and drop it in hollows (natural gullies).</summary>
		static void Erode(Grid g, IslandGenSettings s)
		{
			var rnd = new System.Random(s.Seed * 97 + 1);
			int res = g.Res;
			// In units of about the island's height, so the same settings erode big and small islands alike
			float unit = Mathf.Max(5f, s.Height);
			var h = new float[res * res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) h[z * res + x] = g.M[z, x] / unit;
			float land = (Sea + 0.5f) / unit;
			int drops = Mathf.RoundToInt(s.Erosion * res * res * 0.25f);
			const float Inertia = 0.05f, Capacity = 4f, MinCapacity = 0.01f, ErodeSpeed = 0.3f, DepositSpeed = 0.3f, Evaporate = 0.02f, Gravity = 4f;
			const int Life = 30, Radius = 2;
			// Brush: the cells a drop erodes around it, with weights
			var bx = new List<int>(); var bz = new List<int>(); var bw = new List<float>();
			float wsum = 0f;
			for (int dz = -Radius; dz <= Radius; dz++)
				for (int dx = -Radius; dx <= Radius; dx++)
				{
					float d = Mathf.Sqrt(dx * dx + dz * dz);
					if (d > Radius) continue;
					bx.Add(dx); bz.Add(dz); bw.Add(1f - d / Radius); wsum += 1f - d / Radius;
				}
			for (int i = 0; i < bw.Count; i++) bw[i] /= wsum;
			// (slopes per cell, not per metre: scale by the cell size so resolutions agree)
			float cellScale = g.Step / 2f;
			for (int n = 0; n < drops; n++)
			{
				float px = (float)rnd.NextDouble() * (res - 2), pz = (float)rnd.NextDouble() * (res - 2);
				if (h[(int)pz * res + (int)px] < land) continue;
				float dirX = 0f, dirZ = 0f, speed = 1f, water = 1f, sediment = 0f;
				for (int life = 0; life < Life; life++)
				{
					int cx = (int)px, cz = (int)pz;
					float ox = px - cx, oz = pz - cz;
					int i00 = cz * res + cx;
					float h00 = h[i00], h10 = h[i00 + 1], h01 = h[i00 + res], h11 = h[i00 + res + 1];
					float gx = ((h10 - h00) * (1 - oz) + (h11 - h01) * oz) / cellScale, gz = ((h01 - h00) * (1 - ox) + (h11 - h10) * ox) / cellScale;
					float height = h00 * (1 - ox) * (1 - oz) + h10 * ox * (1 - oz) + h01 * (1 - ox) * oz + h11 * ox * oz;
					dirX = dirX * Inertia - gx * (1 - Inertia); dirZ = dirZ * Inertia - gz * (1 - Inertia);
					float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
					if (len < 1e-6f) break;
					dirX /= len; dirZ /= len;
					px += dirX; pz += dirZ;
					if (px < 1 || pz < 1 || px >= res - 2 || pz >= res - 2) break;
					int nx = (int)px, nz = (int)pz;
					float nox = px - nx, noz = pz - nz;
					int j = nz * res + nx;
					float newHeight = h[j] * (1 - nox) * (1 - noz) + h[j + 1] * nox * (1 - noz) + h[j + res] * (1 - nox) * noz + h[j + res + 1] * nox * noz;
					float delta = newHeight - height;
					if (newHeight < land - 0.3f) { break; } // reached the sea: the soil goes with it
					float capacity = Mathf.Max(-delta * speed * water * Capacity, MinCapacity);
					if (sediment > capacity || delta > 0)
					{
						// Drop soil: fill the hollow (uphill) or what the drop can't carry
						float amount = delta > 0 ? Mathf.Min(delta, sediment) : (sediment - capacity) * DepositSpeed;
						sediment -= amount;
						h[i00] += amount * (1 - ox) * (1 - oz); h[i00 + 1] += amount * ox * (1 - oz);
						h[i00 + res] += amount * (1 - ox) * oz; h[i00 + res + 1] += amount * ox * oz;
					}
					else
					{
						float amount = Mathf.Min((capacity - sediment) * ErodeSpeed, -delta);
						for (int b = 0; b < bw.Count; b++)
						{
							int xx = cx + bx[b], zz = cz + bz[b];
							if (xx < 0 || zz < 0 || xx >= res || zz >= res) continue;
							int k = zz * res + xx;
							if (h[k] < land) continue;
							float take = Mathf.Min(amount * bw[b], h[k] - land * 0.98f);
							if (take <= 0f) continue;
							h[k] -= take;
							sediment += take;
						}
					}
					speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + delta * Gravity));
					water *= 1 - Evaporate;
				}
			}
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) g.M[z, x] = h[z * res + x] * unit;
		}

		static Vector2 RandomOffset(System.Random rnd)
		{
			return new Vector2((float)rnd.NextDouble() * 1000f, (float)rnd.NextDouble() * 1000f);
		}

		/// <summary>Fractal Perlin noise in roughly -1..1.</summary>
		static float Fbm(Vector2 p, int octaves)
		{
			float sum = 0f, amp = 0.5f, norm = 0f;
			for (int i = 0; i < octaves; i++)
			{
				sum += amp * (Mathf.PerlinNoise(p.x, p.y) * 2f - 1f);
				norm += amp;
				p *= 2.03f;
				amp *= 0.5f;
			}
			return sum / norm;
		}

		#endregion

		#region Objects

		/// <summary>Zones of the ground, by height above the sea and slope.</summary>
		const int ZWater = 0, ZWet = 1, ZBeach = 2, ZLand = 3, ZRock = 4, ZoneCount = 5;

		/// <summary>How each kind uses the zones (weight per zone), and how big its things may be (m, larger ones are scaled down).</summary>
		static float[] ZoneWeights(string cat)
		{
			switch (cat)
			{
				case CatTrees: return new[] { 0f, 0f, 0.5f, 1f, 0.12f };
				case CatBushes: return new[] { 0f, 0f, 0.3f, 1f, 0.2f };
				case CatRocks: return new[] { 0f, 0.1f, 0.2f, 0.25f, 1f };
				case CatHarvest: return new[] { 0f, 0f, 0.4f, 1f, 0.4f }; // (the ores and scrap under water are "things to collect under water")
				case CatBeach: return new[] { 0f, 0.6f, 1f, 0f, 0f };
				default: return new[] { 0f, 0f, 0f, 0f, 0f };
			}
		}

		/// <summary>
		/// Where a kind of land object grows on Raft's islands of a style (RaftLand, measured by CIMeasureLand): the kinds
		/// the catalog has, each one's density per habitat bin, and the category's together. Null where Raft's islands of
		/// the style have too few of them (the zone rules decide there).
		/// </summary>
		class Habitat
		{
			public LandThing[] Kinds;
			/// <summary>Per kind, per bin: objects per 1000 m² (small bins shrunk towards none, so a few objects in a scrap of land don't count for much).</summary>
			public float[][] KindWeight;
			/// <summary>Per bin: the category's objects per 1000 m²; and its average over Raft's land.</summary>
			public float[] Weight;
			public float Typical;
			/// <summary>The category's usual steepest ground (the median of its kinds' 90th percentiles of slope).</summary>
			public float Slope90;
			/// <summary>Grows on grass (most of its kinds, WithinHabit): the cliffs over 40° - rock-painted here - don't count as its land.</summary>
			public bool OnGrass;
		}

		/// <summary>Area (m²) a habitat bin is shrunk by: a bin needs about this much of Raft's land before its density counts in full.</summary>
		const float HabitatPrior = 400f;
		/// <summary>The fewest measured objects of a category for its habitat to decide.</summary>
		const int HabitatMinCount = 25;

		// (made on first use, not by an initializer: Last = RaftLike() above runs first and reaches HabitatOf - with an
		// initializer here the dictionary was still null then, and the class failed to load)
		static Dictionary<string, Habitat> habitats;

		/// <summary>Where Raft's islands of the style grow a category: its small islands' way (small) or its big ones'.</summary>
		static Habitat HabitatOf(int style, string cat, bool small)
		{
			if (cat == CatBeach) return null; // (driftwood and pebbles on the beach: Raft's few logs lie inland; the beach keeps the zone rule)
			if (!PlaceableCatalog.IsBuilt) return null; // (not cached: the catalog's names aren't known yet)
			string key = style + "/" + cat + (small ? "/small" : "/big");
			Habitat h;
			if (habitats == null) habitats = new Dictionary<string, Habitat>();
			if (habitats.TryGetValue(key, out h)) return h;
			LandStyle land = RaftLand.For(style, small);
			var core = new HashSet<string>(PlaceableCatalog.GenNames);
			LandThing[] kinds = land.Of(cat).Where(t => core.Contains(t.Name)).ToArray();
			h = null;
			if (kinds.Sum(t => t.Count) >= HabitatMinCount)
			{
				h = new Habitat { Kinds = kinds, KindWeight = new float[kinds.Length][], Weight = new float[RaftLand.BinCount] };
				float totalArea = land.Area.Sum(), totalCount = 0f;
				for (int k = 0; k < kinds.Length; k++)
				{
					h.KindWeight[k] = new float[RaftLand.BinCount];
					for (int b = 0; b < RaftLand.BinCount; b++)
					{
						float count = kinds[k].Density[b] * land.Area[b] / 1000f;
						h.KindWeight[k][b] = count * 1000f / (land.Area[b] + HabitatPrior);
						h.Weight[b] += h.KindWeight[k][b];
						totalCount += count;
					}
				}
				h.Typical = totalArea > 0f ? totalCount * 1000f / totalArea : 0f;
				// (the median of its kinds' limits: bamboo alone - most of the tropical "trees" - would set it for the palms)
				List<float> limits = kinds.Select(t => t.Slope[2]).OrderBy(v => v).ToList();
				h.Slope90 = limits[limits.Count / 2];
				h.OnGrass = kinds.Sum(t => t.OnGrass * t.Count) / Mathf.Max(1, kinds.Sum(t => t.Count)) >= 0.85f;
				if (h.Typical <= 0f) h = null;
			}
			habitats[key] = h;
			return h;
		}

		/// <summary>Tests: the habitat bin of every cell of these heights (-1: not land), as the planner sees them.</summary>
		internal static int[] HabitatBins(IslandGenSettings s, float[,] metres, float step)
		{
			UseSea(s);
			Ground g = Survey(metres, step);
			var bins = new int[g.Res * g.Res];
			for (int i = 0; i < bins.Length; i++) bins[i] = -1;
			for (int b = 0; b < RaftLand.BinCount; b++) foreach (int c in g.BinCells[b]) bins[c] = b;
			return bins;
		}

		/// <summary>Metres inland from the coast at a point (the nearest cell).</summary>
		static float InlandAt(Ground g, float x, float z)
		{
			int ix = Mathf.Clamp(Mathf.RoundToInt(x / g.Step), 0, g.Res - 1), iz = Mathf.Clamp(Mathf.RoundToInt(z / g.Step), 0, g.Res - 1);
			return g.Inland[iz * g.Res + ix];
		}

		/// <summary>
		/// Within where a kind usually stands on Raft's islands: no steeper than its 90th percentile - and than its
		/// category's usual one, so a kind measured a few times on a cliff doesn't take the whole kind there - plus 5°;
		/// off the beach as far as it keeps off it (its 10th percentiles of distance inland and height, three quarters of
		/// them, but never asking more than 9 m inland or 4.5 m up: an island's size isn't Raft's).
		/// </summary>
		static bool WithinHabit(LandThing t, float categorySlope, float slope, float height, float inland, Ground island)
		{
			Vector2 grassLine = island.GrassLine;
			if (slope > Mathf.Min(t.Slope[2], categorySlope) + 5f) return false;
			// (and on the ground texture it grows on: Raft's palms and bushes stand on grass nearly always - the automatic
			// paint (TerrainPainter.AutoWeights) makes rock from 28° and sand below the island's grass line, where they'd
			// look planted on stone or sand)
			float rock = Mathf.InverseLerp(28f, 42f, slope), grass = Mathf.InverseLerp(grassLine.x, grassLine.y, height) * (1f - rock);
			if (t.OnGrass >= 0.85f && grass < 0.5f) return false;
			if (t.OnRock < 0.15f && rock > 0.5f) return false;
			// (an island's size isn't Raft's: never asking more than a third of how far inland and how high it goes)
			if (t.Inland[0] > 2f && inland < Mathf.Min(t.Inland[0], 12f, island.MostInland / 3f) * 0.75f) return false;
			if (t.Height[0] > 1f && height < Mathf.Min(t.Height[0], 6f, island.Top / 3f) * 0.75f) return false;
			return true;
		}

		/// <summary>How suitable a bin is for a category, against its average on Raft's land (0 = never there, 1 = as usual, capped at 2).</summary>
		static float Suitability(Habitat h, int bin) { return Mathf.Min(2f, h.Weight[bin] / h.Typical); }

		static float MaxSize(string cat, int zone)
		{
			switch (cat)
			{
				case CatTrees: return zone == ZBeach ? 18f : 24f;
				case CatBushes: return 8f;
				case CatRocks: return zone == ZRock ? 11f : 6f;
				case CatBeach: return 5f;
				default: return 6f;
			}
		}

		/// <summary>How close things of a kind may stand: a share of their size (trees' crowns overlap, rocks barely).</summary>
		static float Footprint(string cat, float size)
		{
			switch (cat)
			{
				case CatTrees: return Mathf.Clamp(size * 0.13f, 0.7f, 3f);
				case CatBushes: return Mathf.Clamp(size * 0.22f, 0.4f, 2f);
				case CatRocks: return Mathf.Clamp(size * 0.3f, 0.5f, 4f);
				case CatWater: return Mathf.Clamp(size * 0.25f, 0.4f, 2f);
				default: return Mathf.Clamp(size * 0.3f, 0.5f, 2.5f);
			}
		}

		/// <summary>Things already placed, on a grid of cells, for "is this spot free?" (and removing nature around content).</summary>
		class SpotGrid
		{
			const float Cell = 4f;
			readonly int n;
			readonly List<int>[] cells;
			public readonly List<Vector3> Spots = new List<Vector3>(); // x, z, footprint radius
			public readonly List<bool> Removed = new List<bool>();
			float biggest;

			public SpotGrid(float size) { n = Mathf.CeilToInt(size / Cell) + 1; cells = new List<int>[n * n]; }

			int CellOf(float v) { return Mathf.Clamp((int)(v / Cell), 0, n - 1); }

			public bool Free(float x, float z, float r)
			{
				int span = Mathf.CeilToInt((r + biggest) / Cell);
				int cx = CellOf(x), cz = CellOf(z);
				for (int j = Mathf.Max(0, cz - span); j <= Mathf.Min(n - 1, cz + span); j++)
					for (int i = Mathf.Max(0, cx - span); i <= Mathf.Min(n - 1, cx + span); i++)
					{
						List<int> list = cells[j * n + i];
						if (list == null) continue;
						foreach (int k in list)
						{
							if (Removed[k]) continue;
							Vector3 o = Spots[k];
							float dx = o.x - x, dz = o.y - z, rr = o.z + r;
							if (dx * dx + dz * dz < rr * rr) return false;
						}
					}
				return true;
			}

			public int Add(float x, float z, float r)
			{
				int k = Spots.Count;
				Spots.Add(new Vector3(x, z, r));
				Removed.Add(false);
				int c = CellOf(z) * n + CellOf(x);
				if (cells[c] == null) cells[c] = new List<int>();
				cells[c].Add(k);
				biggest = Mathf.Max(biggest, r);
				return k;
			}

			/// <summary>Removes the spots within radius of a point; returns their indices.</summary>
			public List<int> Clear(float x, float z, float radius)
			{
				var gone = new List<int>();
				int span = Mathf.CeilToInt((radius + biggest) / Cell);
				int cx = CellOf(x), cz = CellOf(z);
				for (int j = Mathf.Max(0, cz - span); j <= Mathf.Min(n - 1, cz + span); j++)
					for (int i = Mathf.Max(0, cx - span); i <= Mathf.Min(n - 1, cx + span); i++)
					{
						List<int> list = cells[j * n + i];
						if (list == null) continue;
						foreach (int k in list)
						{
							if (Removed[k]) continue;
							Vector3 o = Spots[k];
							if ((o.x - x) * (o.x - x) + (o.y - z) * (o.y - z) < radius * radius) { Removed[k] = true; gone.Add(k); }
						}
					}
				return gone;
			}
		}

		/// <summary>The planner's view of the ground: heights (m above the terrain's base) with zones per cell.</summary>
		class Ground
		{
			public float[,] M;
			public int Res;
			public float Step, Top;
			public readonly List<int>[] Zones = new List<int>[ZoneCount];
			public float ZoneArea(int z) { return Zones[z].Count * Step * Step; }
			/// <summary>The land's cells by habitat (RaftLand.BinOf: how far inland, how steep, how high), as measured on Raft's islands.</summary>
			public readonly List<int>[] BinCells = new List<int>[RaftLand.BinCount];
			public float BinArea(int b) { return BinCells[b].Count * Step * Step; }
			/// <summary>Metres inland from the nearest water, per cell (z * Res + x; 0 under water).</summary>
			public float[] Inland;
			/// <summary>m² of land; small = as Raft's small islands (RaftLand.SmallLand), which grow their own way.</summary>
			public float LandArea;
			/// <summary>The farthest any land is from the water (m).</summary>
			public float MostInland;
			public bool Small { get { return LandArea < RaftLand.SmallLand; } }
			/// <summary>Where the paint turns from sand to grass here (TerrainPainter.GrassLine), m above the sea: from - to.</summary>
			public Vector2 GrassLine = new Vector2(2.5f, 5f);

			public float At(float x, float z)
			{
				float fx = Mathf.Clamp(x / Step, 0, Res - 1.001f), fz = Mathf.Clamp(z / Step, 0, Res - 1.001f);
				int x0 = (int)fx, z0 = (int)fz;
				float tx = fx - x0, tz = fz - z0;
				return Mathf.Lerp(Mathf.Lerp(M[z0, x0], M[z0, x0 + 1], tx), Mathf.Lerp(M[z0 + 1, x0], M[z0 + 1, x0 + 1], tx), tz);
			}

			/// <summary>The ground where the terrain has it (its triangles, TerrainSurface) - what an object's foot stands on;
			/// At blends smoothly, for the decisions (what grows where), as it always did.</summary>
			public float Surface(float x, float z) { return TerrainSurface(M, Step, x, z); }

			public float Slope(float x, float z)
			{
				float dx = (At(x + Step, z) - At(x - Step, z)) / (2f * Step);
				float dz = (At(x, z + Step) - At(x, z - Step)) / (2f * Step);
				return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
			}

			public int ZoneOf(float above, float slope)
			{
				if (above < -12f) return -1;
				if (above < -1.2f) return ZWater;
				if (above < 0.6f) return ZWet;
				if (slope > 32f || above > Mathf.Max(6f, Top * 0.82f)) return ZRock;
				return above < ShelfHeight + 1.2f ? ZBeach : ZLand;
			}
		}

		/// <summary>
		/// The height of a heights grid (metres, [z, x], cells of step metres from its corner) where Unity's terrain has it:
		/// two triangles per cell, split from the cell's corner (0,0) to (1,1). Between the corners that lies above or below
		/// smooth (bilinear) blending by up to a quarter of the cell's twist - on an uneven steep slope half a metre and more
		/// (a coral set down on the blended height stood in the water). split: 1 = the other diagonal, 2 = blended (the
		/// test that checks which one the terrain has, CIGroundSplit).
		/// </summary>
		public static float TerrainSurface(float[,] m, float step, float x, float z, int split = 0)
		{
			int res = m.GetLength(0);
			float fx = Mathf.Clamp(x / step, 0, res - 1.001f), fz = Mathf.Clamp(z / step, 0, res - 1.001f);
			int x0 = (int)fx, z0 = (int)fz;
			float u = fx - x0, v = fz - z0;
			float h00 = m[z0, x0], h10 = m[z0, x0 + 1], h01 = m[z0 + 1, x0], h11 = m[z0 + 1, x0 + 1];
			if (split == 2) return Mathf.Lerp(Mathf.Lerp(h00, h10, u), Mathf.Lerp(h01, h11, u), v);
			if (split == 1) return u + v <= 1f ? h00 + (h10 - h00) * u + (h01 - h00) * v : h11 + (h01 - h11) * (1f - u) + (h10 - h11) * (1f - v);
			return u > v ? h00 + (h10 - h00) * u + (h11 - h10) * v : h00 + (h11 - h01) * u + (h01 - h00) * v;
		}

		static Ground Survey(float[,] metres, float step)
		{
			int res = metres.GetLength(0);
			var g = new Ground { M = metres, Res = res, Step = step };
			for (int i = 0; i < ZoneCount; i++) g.Zones[i] = new List<int>();
			for (int i = 0; i < RaftLand.BinCount; i++) g.BinCells[i] = new List<int>();
			float top = 0f;
			foreach (float v in metres) top = Mathf.Max(top, v - Sea);
			g.Top = top;
			// How far inland each land cell is (m from the nearest water: two chamfer passes)
			var inland = new float[res * res];
			g.Inland = inland;
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) inland[z * res + x] = metres[z, x] > Sea ? 1e9f : 0f;
			for (int pass = 0; pass < 2; pass++)
			{
				for (int z = 1; z < res; z++)
					for (int x = 1; x < res - 1; x++)
					{
						int i = z * res + x;
						float v = inland[i];
						if (v == 0f) continue;
						v = Mathf.Min(v, Mathf.Min(inland[i - 1] + step, inland[i - res] + step));
						v = Mathf.Min(v, Mathf.Min(inland[i - res - 1], inland[i - res + 1]) + step * 1.414f);
						inland[i] = v;
					}
				for (int z = res - 2; z >= 0; z--)
					for (int x = res - 2; x >= 1; x--)
					{
						int i = z * res + x;
						float v = inland[i];
						if (v == 0f) continue;
						v = Mathf.Min(v, Mathf.Min(inland[i + 1] + step, inland[i + res] + step));
						v = Mathf.Min(v, Mathf.Min(inland[i + res + 1], inland[i + res - 1]) + step * 1.414f);
						inland[i] = v;
					}
			}
			for (int z = 1; z < res - 1; z++)
				for (int x = 1; x < res - 1; x++)
				{
					float e = metres[z, x];
					if (e < Sea - 12f) continue;
					float dx = (metres[z, x + 1] - metres[z, x - 1]) / (2f * g.Step), dz = (metres[z + 1, x] - metres[z - 1, x]) / (2f * g.Step);
					float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
					int zone = g.ZoneOf(e - Sea, slope);
					if (zone >= 0) g.Zones[zone].Add(z * res + x);
					if (e > Sea) g.BinCells[RaftLand.BinOf(inland[z * res + x], slope, e - Sea)].Add(z * res + x);
				}
			g.LandArea = g.BinCells.Sum(c => c.Count) * g.Step * g.Step;
			g.MostInland = 0f;
			foreach (float v in inland) if (v < 1e8f) g.MostInland = Mathf.Max(g.MostInland, v);
			g.GrassLine = TerrainPainter.GrassLine(g.LandArea);
			return g;
		}

		/// <summary>Everything the generator places for these heights (m above the terrain's base): nature, then creature spots and loot boxes. Deterministic for the settings.</summary>
		public static List<IslandObject> PlanAll(IslandGenSettings s, float[,] metres, Vector3 size, GenReport report = null)
		{
			var result = new List<IslandObject>();
			if (report == null) report = new GenReport();
			if (!PlaceableCatalog.IsBuilt) return result;
			s = s.Copy();
			s.Clamp();
			UseSea(s);
			float step = size.x / (metres.GetLength(0) - 1);
			Ground ground = Survey(metres, step);
			SeaGround sea = SurveySea(metres, step);
			var spots = new SpotGrid(size.x);
			var owners = new List<IslandObject>(); // the object of each spot (null for content's own spots)
			var cats = new List<string>();          // and its kind
			// How many of each kind; land and sea scaled down together past MaxObjects
			Dictionary<string, int> land = LandTargets(s, ground);
			Dictionary<string, float> seaWanted = SeaTargets(s, sea);
			float wanted = land.Values.Sum() + seaWanted.Values.Sum();
			report.Wanted = Mathf.RoundToInt(wanted);
			float k = wanted > MaxObjects ? MaxObjects / wanted : 1f;
			if (k < 1f) foreach (string cat in LandCategories) land[cat] = Mathf.FloorToInt(land[cat] * k);
			PlanNature(s, ground, spots, owners, cats, report, land);
			PlanSea(s, ground, sea, spots, owners, cats, report, k);
			PlanContent(s, ground, spots, owners, cats, report, result);
			for (int i = 0; i < owners.Count; i++)
				if (owners[i] != null && !spots.Removed[i]) result.Add(owners[i]);
			// (content first in the list: map types and tests find it quickly; nature follows)
			return result;
		}

		/// <summary>About how many objects of each kind the settings give (before content clears some), from heights on any grid (the preview's).</summary>
		public static Dictionary<string, int> Estimate(IslandGenSettings s, float[,] metres, float step)
		{
			s = s.Copy();
			s.Clamp();
			UseSea(s);
			Dictionary<string, int> land = LandTargets(s, Survey(metres, step));
			Dictionary<string, float> sea = SeaTargets(s, SurveySea(metres, step));
			float wanted = land.Values.Sum() + sea.Values.Sum();
			float k = wanted > MaxObjects ? MaxObjects / wanted : 1f;
			var targets = new Dictionary<string, int>();
			foreach (string cat in LandCategories) targets[cat] = Mathf.FloorToInt(land[cat] * k);
			foreach (string cat in SeaCategories) targets[cat] = Mathf.RoundToInt(sea[cat] * k);
			return targets;
		}

		/// <summary>How many objects of each land kind: density x the area of its zones.</summary>
		static Dictionary<string, int> LandTargets(IslandGenSettings s, Ground ground)
		{
			var targets = new Dictionary<string, int>();
			foreach (string cat in LandCategories)
			{
				float area = 0f;
				Habitat hab = HabitatOf(s.Style, cat, ground.Small);
				if (hab != null)
				{
					// (as much land as suits the kind the way Raft's islands have it: none on the bare beach, cliffs or where Raft never grows it)
					for (int b = 0; b < RaftLand.BinCount; b++)
					{
						int bi, bs, bh;
						RaftLand.Unbin(b, out bi, out bs, out bh);
						if (hab.OnGrass && bs == RaftLand.Bins1 - 1) continue;
						area += Suitability(hab, b) * ground.BinArea(b);
					}
					// (a slider's Like Raft is the density of Raft's big islands; its small ones are that much thicker with things:
					// five times the bushes, three times the trees - at most eight times, for the desert's shrub-covered islets)
					if (ground.Small)
					{
						// (a style without small islands of Raft's to go by - Balboa's and Temperance's are all big - gets the
						// tropical ones' difference: Raft's small islands are that much thicker with things than its big ones)
						int by = RaftLand.HasSmall(s.Style) ? s.Style : TerrainPainter.Tropical;
						Habitat small = HabitatOf(by, cat, true), big = HabitatOf(by, cat, false);
						if (small != null && big != null && small != big && big.Typical > 0f) area *= Mathf.Clamp(small.Typical / big.Typical, 0.25f, 8f) * SmallBoost(cat);
					}
				}
				else
				{
					float[] w = ZoneWeights(cat);
					for (int z = 0; z < ZoneCount; z++) area += w[z] * ground.ZoneArea(z);
				}
				float v = AmountOf(s, cat);
				float target = MaxDensity(cat) * v * v * area / 1000f;
				// (never much thicker than Raft's own islands of the style and size at the slider's Like Raft: a cluster of
				// little islets got bushes at twice Raft's small islands' - "way too much", the user, 2026-10-03)
				if (cat != CatBeach && ground.LandArea > 1f)
				{
					LandStyle raft = RaftLand.For(s.Style, ground.Small);
					float raftArea = raft != null ? raft.Area.Sum() : 0f, like = LikeRaftAmount(s.Style, cat);
					if (raftArea > 1f && like > 0.01f)
					{
						float most = raft.CountOf(cat) * 1000f / raftArea * (v / like) * (v / like) * ground.LandArea / 1000f;
						if (most > 0f && target > most) target = most;
					}
				}
				targets[cat] = Mathf.RoundToInt(target);
			}
			return targets;
		}

		static void PlanNature(IslandGenSettings s, Ground ground, SpotGrid spots, List<IslandObject> owners, List<string> cats, GenReport report, Dictionary<string, int> targets)
		{
			string[] names = PlaceableCatalog.GenNames.ToArray(); // the same list on every PC, so a seed always gives the same island (CA19)
			StylePools pools = Pools[Mathf.Clamp(s.Style, 0, Pools.Length - 1)];
			Func<Regex, string[]> pick = r => names.Where(n => r.IsMatch(n)).ToArray();
			string[] trees = pick(pools.Trees), shoreTrees = pick(pools.ShoreTrees), bushes = pick(pools.Bushes), rocks = pick(pools.Rocks), beach = pick(pools.Beach);
			string[] landHarvest = pick(pools.LandHarvest), seaHarvest = pick(pools.SeaHarvest), water = pick(pools.Water);
			if (shoreTrees.Length == 0) shoreTrees = trees;
			string[] beachHarvest = landHarvest.Where(n => Regex.IsMatch(n, @"(Rock|Clay|Sand|Scrap)")).ToArray();
			if (beachHarvest.Length == 0) beachHarvest = landHarvest;

			// (targets: how many of each land kind, already scaled down past MaxObjects)
			for (int ci = 0; ci < LandCategories.Length; ci++)
			{
				string cat = LandCategories[ci];
				int target = targets[cat];
				if (target <= 0) continue;
				var rnd = new System.Random(s.Seed * 7919 + 13 + ci * 1013);
				Vector2 clusterOff = RandomOffset(rnd);
				// Where: in the habitat bins Raft's islands of the style have this kind of thing in, as often as there (or,
				// without a measurement, the zones)
				Habitat hab = HabitatOf(s.Style, cat, ground.Small);
				int slots = hab != null ? RaftLand.BinCount : ZoneCount;
				float[] w = hab != null ? hab.Weight : ZoneWeights(cat);
				Func<int, List<int>> cellsOf = i => hab != null ? ground.BinCells[i] : ground.Zones[i];
				float[] cum = new float[slots];
				float total = 0f;
				// (thicker than Like Raft - a jungle - thickens the island's higher ground, not its low shore and flats: under 4 m
				// above the sea it stays as thick as Raft's, where Raft's bamboo stands thick; the jungle islands' wide low beaches
				// were fields of bamboo, the review 2026-10-03)
				float inside = 1f;
				if (hab != null)
				{
					float like = LikeRaftAmount(s.Style, cat), now = AmountOf(s, cat);
					float factor = like > 0.01f ? now * now / (like * like) : 1f;
					if (factor > 1.05f)
					{
						float coast = 0f, inner = 0f;
						for (int i = 0; i < slots; i++)
						{
							float wi = w[i] * cellsOf(i).Count;
							if (i % RaftLand.Bins1 <= 1) coast += wi; else inner += wi;
						}
						if (inner > 0f) inside = Mathf.Max(1f, (factor * (coast + inner) - coast) / inner);
					}
				}
				for (int i = 0; i < slots; i++)
				{
					// (grass-bound kinds: not on the cliffs, WithinHabit wouldn't take a spot there)
					if (hab != null && hab.OnGrass && i / RaftLand.Bins1 % RaftLand.Bins1 == RaftLand.Bins1 - 1) { cum[i] = total; continue; }
					total += w[i] * cellsOf(i).Count * (hab != null && i % RaftLand.Bins1 > 1 ? inside : 1f);
					cum[i] = total;
				}
				if (total <= 0f) continue;
				float[] kindCum = hab != null ? new float[hab.Kinds.Length] : null;
				int placed = 0;
				// (tries enough spots: on a small island most are taken or outside a kind's habits, and giving up early left it bare)
				for (int attempt = 0; attempt < target * 12 + 100 && placed < target; attempt++)
				{
					float r = (float)rnd.NextDouble() * total;
					int slot = 0;
					while (slot < slots - 1 && r >= cum[slot]) slot++;
					List<int> cells = cellsOf(slot);
					if (cells.Count == 0) continue;
					int cell = cells[rnd.Next(cells.Count)];
					float x = (cell % ground.Res + (float)rnd.NextDouble() - 0.5f) * ground.Step, z0 = (cell / ground.Res + (float)rnd.NextDouble() - 0.5f) * ground.Step;
					float h = ground.At(x, z0), above = h - Sea;
					int zone = ground.ZoneOf(above, ground.Slope(x, z0));
					// (the jitter may have moved it into the water, or out of it)
					if (hab != null ? above < 0.1f : (slot == ZWater ? above > -1f : above < 0.3f && slot != ZWet)) continue;
					if (s.Clusters > 0f)
					{
						float n = Fbm(new Vector2(x, z0) / 28f + clusterOff, 3);
						if ((float)rnd.NextDouble() > Mathf.Lerp(1f, SS(-0.1f, 0.35f, n), s.Clusters)) continue;
					}
					// Which: as Raft mixes the kinds in that habitat (palms inland, bamboo by the beach...), or from the style's pool
					string kind;
					LandThing measured = null;
					if (hab != null)
					{
						// (only kinds this spot is within the usual place of - WithinHabit - so the odd palm on a cliff or bush at
						// the waterline of Raft's islands isn't copied; rocks go wherever Raft has them)
						float slopeHere = ground.Slope(x, z0), inlandHere = InlandAt(ground, x, z0);
						float kt = 0f;
						for (int k = 0; k < hab.Kinds.Length; k++)
						{
							if (cat == CatRocks || WithinHabit(hab.Kinds[k], hab.Slope90, slopeHere, above, inlandHere, ground)) kt += hab.KindWeight[k][slot];
							kindCum[k] = kt;
						}
						if (kt <= 0f) continue;
						float kr = (float)rnd.NextDouble() * kt;
						int pickK = 0;
						while (pickK < hab.Kinds.Length - 1 && kr >= kindCum[pickK]) pickK++;
						measured = hab.Kinds[pickK];
						kind = measured.Name;
					}
					else
					{
						string[] pool;
						switch (cat)
						{
							case CatTrees: pool = zone == ZBeach ? shoreTrees : trees; break;
							case CatBushes: pool = bushes; break;
							case CatRocks: pool = rocks; break;
							case CatBeach: pool = beach; break;
							// (on the beach only what lies on beaches - stones, clay, sand - not berry bushes or pineapples)
							case CatHarvest: pool = slot == ZWater ? seaHarvest : zone <= ZBeach ? beachHarvest : landHarvest; break;
							default: pool = water; break;
						}
						if (pool.Length == 0) continue;
						kind = pool[rnd.Next(pool.Length)];
					}
					float yaw = (float)rnd.NextDouble() * 360f;
					float scale = 0.85f + 0.3f * (float)rnd.NextDouble();
					float objectSize = SpawnSize(kind); // (as spawned: the prototype carries Raft's own scale)
					float maxSize = MaxSize(cat, zone);
					if (objectSize * scale > maxSize) scale = Mathf.Max(0.15f, maxSize / objectSize);
					float foot = Footprint(cat, objectSize * scale);
					// (something flat and wide - a snow drift - lies along a gentle slope: sunk to its low edge it was buried, set
					// down flat its low edge stood in the air; on a steep one it would hang down the slope like a sheet - not there.
					// Ice at the shore stays as Raft has it, flat at the waterline)
					Vector3 lean = Vector3.up;
					bool flat = kind.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) < 0 && LiesFlat(kind, scale, ground, x, z0, out lean);
					if (flat && Vector3.Angle(Vector3.up, lean) > 20f) continue; // (lean down: the ground under its rim isn't level enough)
					// (ice: not where the beach under its mesh's middle rises well over its pivot's - kept out of that sand it stood
					// 1.6 m over the shore at its pivot, CIGenMatrix; Raft lays shore ice on gentle beaches)
					if (!flat && kind.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						Vector3 mid0 = MeshMiddle(kind, scale, yaw);
						if (ground.Surface(x + mid0.x, z0 + mid0.z) - ground.Surface(x, z0) > 0.6f) continue;
					}
					if (!spots.Free(x, z0, foot)) continue;
					spots.Add(x, z0, foot);
					GameObject proto = PlaceableCatalog.Get(kind);
					Vector3 baseScale = proto != null ? proto.transform.localScale : Vector3.one;
					// (all of its base on the ground: down to the lowest ground under it - on a slope the low side of a rock, a bush
					// or a log stood in the air; then sunk as Raft sinks them: its big boulders stand a third of their size in the ground)
					float y = flat ? ground.Surface(x, z0) - 0.2f : h - BaseDrop(ground, x, z0, kind, cat, scale);
					// (ice at the shore lies flat at the waterline as Raft has it: down to the lowest ground under it but no lower than
					// the sea - set at the ground under its middle, its low side stood in the air over a sloping beach)
					if (kind.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0) y = Mathf.Min(h, Mathf.Max(y, Sea));
					if (measured != null && measured.Above < -0.2f && measured.Size > 0.5f) y += Mathf.Max(measured.Above / measured.Size, -0.6f) * objectSize * scale;
					// (never out of sight - but not ice, which keeps to the waterline: its mesh hangs 7 m under its pivot, and kept a
					// third of its height up it stood 2.5 m over the shore, CIGenMatrix)
					if (!flat && kind.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) < 0) y = Mathf.Max(y, LowestShowing(kind, scale, ground.Surface(x, z0), 0.35f));
					// (ice: only its top kept out of the ground - at the waterline on a beach whose middle is higher the sand rose over
					// it, up to 1.9 m, CIGroundTypes; by the ground at its mesh's middle too, which lies off its pivot: by the pivot's
					// alone the sand there still rose up to 0.8 m over it)
					else if (!flat)
					{
						Vector3 mid = MeshMiddle(kind, scale, yaw);
						y = Mathf.Max(y, LowestShowing(kind, scale, Mathf.Max(ground.Surface(x, z0), ground.Surface(x + mid.x, z0 + mid.z)), 0f));
					}
					Vector3 euler = lean == Vector3.up ? new Vector3(0, yaw, 0) : (Quaternion.FromToRotation(Vector3.up, lean) * Quaternion.Euler(0f, yaw, 0f)).eulerAngles;
					owners.Add(new IslandObject { Name = kind, Position = new Vector3(x, y, z0), EulerRotation = euler, Scale = baseScale * scale, Unscaled = proto == null });
					cats.Add(cat);
					placed++;
				}
				report.Counts[cat] = placed;
			}
		}

		#endregion

		#region Under water, like Raft's islands

		/// <summary>The sea around the land: cells by depth band (RaftUnderwater.Bands), with their distance from the land.</summary>
		class SeaGround
		{
			public int Res;
			public float Step;
			/// <summary>Metres from the nearest land, per cell (z * Res + x).</summary>
			public float[] Coast;
			/// <summary>The cells of each depth band, nearest the land first.</summary>
			public readonly List<int>[] Bands = new List<int>[RaftUnderwater.BandCount];
			public float Area(int band) { return Bands[band].Count * Step * Step; }
			/// <summary>How many of a band's cells (the first ones of Bands) lie within this many metres of the land.</summary>
			public int Within(int band, float reach)
			{
				List<int> cells = Bands[band];
				int lo = 0, hi = cells.Count;
				while (lo < hi) { int mid = (lo + hi) / 2; if (Coast[cells[mid]] <= reach) lo = mid + 1; else hi = mid; }
				return lo;
			}
			/// <summary>How far from the land a kind goes (m): as far as nearly all of Raft's own.</summary>
			public static float Reach(SeaThing t) { return t.CoastHigh * 1.3f + 6f; }
		}

		/// <summary>How far from the land the sea's objects go (Raft: most within 70 m of the coast, big rocks further out).</summary>
		const float SeaReach = 170f;
		/// <summary>The deepest ground dressed (m): an island on a deep sea floor keeps its ground in a world down to about 110 m (IslandSpawner).</summary>
		const float MaxSeaDepth = 105f;

		static SeaGround SurveySea(float[,] m, float step)
		{
			int res = m.GetLength(0);
			var g = new SeaGround { Res = res, Step = step, Coast = new float[res * res] };
			for (int b = 0; b < RaftUnderwater.BandCount; b++) g.Bands[b] = new List<int>();
			// Distance (in cells) from the land: two chamfer passes
			float[] d = g.Coast;
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) d[z * res + x] = m[z, x] > Sea ? 0f : 1e9f;
			for (int pass = 0; pass < 2; pass++)
			{
				for (int z = 0; z < res; z++)
					for (int x = 0; x < res; x++)
					{
						int i = z * res + x;
						float v = d[i];
						if (x > 0) v = Mathf.Min(v, d[i - 1] + 1f);
						if (z > 0) { v = Mathf.Min(v, d[i - res] + 1f); if (x > 0) v = Mathf.Min(v, d[i - res - 1] + 1.414f); if (x < res - 1) v = Mathf.Min(v, d[i - res + 1] + 1.414f); }
						d[i] = v;
					}
				for (int z = res - 1; z >= 0; z--)
					for (int x = res - 1; x >= 0; x--)
					{
						int i = z * res + x;
						float v = d[i];
						if (x < res - 1) v = Mathf.Min(v, d[i + 1] + 1f);
						if (z < res - 1) { v = Mathf.Min(v, d[i + res] + 1f); if (x < res - 1) v = Mathf.Min(v, d[i + res + 1] + 1.414f); if (x > 0) v = Mathf.Min(v, d[i + res - 1] + 1.414f); }
						d[i] = v;
					}
			}
			for (int z = 1; z < res - 1; z++)
				for (int x = 1; x < res - 1; x++)
				{
					int i = z * res + x;
					d[i] *= step;
					float e = m[z, x], depth = Sea - e;
					// (not the land, and not the flat floor at the terrain's base, as Raft's measurements leave out its deep floor)
					if (depth < 0.3f || e < 0.5f || depth > MaxSeaDepth || d[i] > SeaReach) continue;
					g.Bands[RaftUnderwater.BandOf(depth)].Add(i);
				}
			// (nearest the land first - ties by cell, so a seed always gives the same island)
			foreach (List<int> cells in g.Bands) cells.Sort((p, q) => { int c = g.Coast[p].CompareTo(g.Coast[q]); return c != 0 ? c : p.CompareTo(q); });
			return g;
		}

		/// <summary>The island's reefs: the sea floor's cells its corals grow on (Core) and its sea vines and kelp (Halo), by depth band.</summary>
		class ReefMask
		{
			public readonly List<int>[] Core = new List<int>[RaftUnderwater.BandCount], Halo = new List<int>[RaftUnderwater.BandCount];
			/// <summary>All of the reefs' cells, whatever their depth.</summary>
			public readonly List<int> CoreAll = new List<int>(), HaloAll = new List<int>();
			static readonly Regex LooseKinds = new Regex(@"^(SeaVine|[Ss]eavine|Seaweed|Kelp|Pillar_\d)");
			/// <summary>Sea vines, seaweed and kelp: round the reefs, not only on them.</summary>
			public static bool Loose(string name) { return LooseKinds.IsMatch(name); }
			public ReefMask() { for (int b = 0; b < RaftUnderwater.BandCount; b++) { Core[b] = new List<int>(); Halo[b] = new List<int>(); } }
		}

		/// <summary>Corals per reef, its radius (m) and the least gap between two reefs' middles.</summary>
		const float PerReef = 250f, ReefRadius = 7.5f, ReefGap = 18f;

		/// <summary>
		/// Where an island's corals grow: Raft's own islands have theirs in a few dense reefs - two or three tight patches some
		/// 15 m across on its small islands, and along the reef's rim round its big ones, with bare sand, rock and the inner
		/// lagoon between - and the generator had spread as many evenly over all of the sea floor around the land: a carpet
		/// of coral over a wide shallow bank ("way too much", the user, 2026-10-03; seen beside Raft's own with CIRaftView).
		/// One reef per 250 corals, 2-20 m down and 6-45 m from the coast, more on the slopes than on flat sand, at least 18 m
		/// apart; each reef a ragged patch of about 7.5 m radius (Groups makes them tighter or looser); sea vines and kelp
		/// round them too.
		/// </summary>
		static ReefMask PlanReefs(IslandGenSettings s, Ground ground, SeaGround sea, float corals)
		{
			var mask = new ReefMask();
			if (corals < 1f) return mask;
			var rnd = new System.Random(s.Seed * 6007 + 911);
			int wanted = Mathf.Clamp(Mathf.RoundToInt(corals / PerReef), 1, 80);
			var cand = new List<int>();
			var cum = new List<float>();
			float total = 0f;
			for (int b = 1; b <= 3; b++)
				foreach (int c in sea.Bands[b])
				{
					float d = sea.Coast[c];
					if (d < 6f || d > 45f) continue;
					total += 0.3f + SS(2f, 15f, ground.Slope((c % sea.Res) * sea.Step, (c / sea.Res) * sea.Step));
					cand.Add(c);
					cum.Add(total);
				}
			if (cand.Count == 0) return mask;
			var reefs = new List<Vector3>(); // x, z, radius
			float size = Mathf.Lerp(1.35f, 0.75f, Mathf.Clamp01(s.Clusters)); // (Groups: tighter reefs)
			for (int tries = 0; reefs.Count < wanted && tries < wanted * 50; tries++)
			{
				int i = cum.BinarySearch((float)rnd.NextDouble() * total);
				if (i < 0) i = ~i;
				int c = cand[Mathf.Min(i, cand.Count - 1)];
				var p = new Vector2((c % sea.Res) * sea.Step, (c / sea.Res) * sea.Step);
				if (reefs.Any(q => (new Vector2(q.x, q.y) - p).sqrMagnitude < ReefGap * ReefGap)) continue;
				reefs.Add(new Vector3(p.x, p.y, ReefRadius * size * (0.7f + 0.6f * (float)rnd.NextDouble())));
			}
			Vector2 off = RandomOffset(rnd);
			for (int b = 0; b < RaftUnderwater.BandCount; b++)
				foreach (int c in sea.Bands[b])
				{
					float x = (c % sea.Res) * sea.Step, z = (c / sea.Res) * sea.Step, near = float.MaxValue;
					foreach (Vector3 q in reefs)
					{
						float dx = x - q.x, dz = z - q.y;
						near = Mathf.Min(near, Mathf.Sqrt(dx * dx + dz * dz) / q.z);
					}
					float ragged = 0.7f + 0.6f * Mathf.PerlinNoise(x / 5f + off.x, z / 5f + off.y);
					if (near < ragged) { mask.Core[b].Add(c); mask.CoreAll.Add(c); }
					if (near < ragged * 1.8f) { mask.Halo[b].Add(c); mask.HaloAll.Add(c); }
				}
			Debug.Log("[CUSTOM ISLANDS] Reefs: " + corals.ToString("F0") + " corals wanted, " + reefs.Count + " of " + wanted + " reefs, " +
				string.Join(" ", Enumerable.Range(0, RaftUnderwater.BandCount).Select(b => mask.Core[b].Count.ToString()).ToArray()) + " reef cells by band (" + sea.Step.ToString("F1") + " m)");
			return mask;
		}

		/// <summary>How many objects of each under-water kind (before the cap): Raft's density per depth band x this island's ground in that band
		/// within the kind's reach of the land x the slider.</summary>
		static Dictionary<string, float> SeaTargets(IslandGenSettings s, SeaGround sea)
		{
			var wanted = SeaCategories.ToDictionary(c => c, c => 0f);
			foreach (string cat in SeaCategories)
			{
				float f = SeaFactor(AmountOf(s, cat));
				if (f <= 0f) continue;
				f *= SeaCap(s, sea, cat, f);
				foreach (SeaThing t in SeaThingsOf(s, cat))
					for (int b = 0; b < RaftUnderwater.BandCount; b++) wanted[cat] += t.Density[b] * sea.Within(b, SeaGround.Reach(t)) * sea.Step * sea.Step * f;
			}
			return wanted;
		}

		/// <summary>
		/// How much of a category's count under water is kept: the rocks at most half as many again as Raft's islands of the
		/// style have on their sea floor (per m², 0-40 m deep within 60 m of the land, as CIIslandDensity compares them) x the
		/// slider. Raft's measurement read the ground under its shore boulders as land where their tops break the surface -
		/// Temperance's ice boulders and Balboa's rocks all in the shallowest band, 0 m from the coast - so a wide shallow shelf
		/// got a wall of them (Glacier Station: eight times Temperance's, the review 2026-10-03).
		/// </summary>
		static float SeaCap(IslandGenSettings s, SeaGround sea, string cat, float f)
		{
			// (the corals and plants too, at Raft's own: a generated island on a wide shallow bank had nearly half as many again
			// as Raft's per m² - "way too much", the user, 2026-10-03)
			float times = cat == CatSeaRocks ? 1.5f : cat == CatWater ? 1f : 0f;
			if (times <= 0f || f <= 0f) return 1f;
			float raft = RaftUnderwater.DensityOf(s.Style, cat, s.RaftSeaKinds);
			if (raft <= 0f) return 1f;
			float zone = 0f, total = 0f, cell = sea.Step * sea.Step;
			for (int b = 0; b < 5; b++) zone += sea.Within(b, 60f) * cell;
			foreach (SeaThing t in SeaThingsOf(s, cat))
				for (int b = 0; b < RaftUnderwater.BandCount; b++) total += t.Density[b] * sea.Within(b, SeaGround.Reach(t)) * cell * f;
			float most = times * raft / 1000f * zone * f;
			return total > most ? most / total : 1f;
		}

		static HashSet<string> coreNames;
		static int coreNamesCount = -1;

		/// <summary>The measured kinds of a style that this editor has (the catalog's core objects, so a seed always gives the same island).</summary>
		static List<SeaThing> SeaThingsOf(IslandGenSettings s, string cat) { return SeaThingsOf(s.Style, cat, s.RaftSeaKinds); }

		/// <summary>(extra, LM7: also the other kinds Raft's islands have under water that the catalog has, core or in its index -
		/// the index is the same on every PC, so a seed still gives the same island once they are loaded)</summary>
		static List<SeaThing> SeaThingsOf(int style, string cat, bool extra)
		{
			if (!PlaceableCatalog.IsBuilt) return new List<SeaThing>();
			List<string> core = PlaceableCatalog.GenNames.ToList();
			if (coreNames == null || core.Count != coreNamesCount) { coreNames = new HashSet<string>(core); coreNamesCount = core.Count; }
			Func<SeaThing, bool> has = t => coreNames.Contains(t.Name) || (t.Extra && PlaceableCatalog.SceneOf(t.Name) != null);
			List<SeaThing> list = RaftUnderwater.For(style).Of(cat, extra).Where(has).ToList();
			// (every sea has something to collect: Temperance's bare, icy rock borrows the tropical islands' finds)
			if (list.Count == 0 && cat == CatSeaFinds) list = RaftUnderwater.For(TerrainPainter.Tropical).Of(cat, extra).Where(has).ToList();
			return list;
		}

		/// <summary>LM7: the extra under-water kinds these settings place that must be loaded from Raft's scenes first (empty without RaftSeaKinds).</summary>
		public static List<string> SeaNamesNeeded(IslandGenSettings s)
		{
			if (s == null || !s.RaftSeaKinds) return new List<string>();
			return SeaCategories.Where(c => SeaFactor(AmountOf(s, c)) > 0f).SelectMany(c => SeaThingsOf(s.Style, c, true)).Where(t => t.Extra).Select(t => t.Name).Distinct().ToList();
		}

		/// <summary>How far the terrain's surface falls within r of a point, below the point's own.</summary>
		static float SurfaceFall(Ground ground, float x, float z, float r)
		{
			float mid = ground.Surface(x, z), low = mid;
			for (int i = 0; i < 8; i++)
			{
				float a = i * Mathf.PI / 4f;
				low = Mathf.Min(low, ground.Surface(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r));
			}
			return mid - low;
		}

		/// <summary>
		/// How far below the ground at its middle an object goes so that all of its base stands on the ground: the lowest
		/// ground under its base - a tree's trunk, else most of its width (set down by its middle on a slope, the low side
		/// of a rock, a bush, a log or a snow drift stood in the air). 0 on level ground; it doesn't change what is placed
		/// where, so a seed still gives the same island.
		/// </summary>
		static float BaseDrop(Ground ground, float x, float z, string name, string cat, float scale)
		{
			GameObject proto = PlaceableCatalog.Get(name);
			Vector3 ls = proto != null ? proto.transform.localScale : Vector3.one;
			Bounds b;
			float width = PlaceableCatalog.LocalBounds(name, out b) ? Mathf.Max(b.size.x * Mathf.Abs(ls.x), b.size.z * Mathf.Abs(ls.z)) * scale : 1f;
			float radius = cat == CatTrees ? Mathf.Min(0.5f * scale, width * 0.15f) : Mathf.Min(width * 0.38f, 6f);
			// (from the blended height the caller sets things down at, to the terrain's own surface under the base)
			float h = ground.At(x, z), low = ground.Surface(x, z);
			for (int i = 0; i < 8; i++)
			{
				float a = i * Mathf.PI / 4f;
				low = Mathf.Min(low, ground.Surface(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius));
			}
			// (a mesh whose bottom is above its pivot - some of Raft's stood on their pivot in a hollow - comes down onto the ground too)
			float lift = proto != null && PlaceableCatalog.LocalBounds(name, out b) ? Mathf.Max(0f, b.min.y * Mathf.Abs(ls.y) * scale) : 0f;
			return h - low + lift;
		}

		/// <summary>
		/// The lowest a thing's pivot may be set so part of it still stands above the ground at its middle: its top at least
		/// share of its height (and 0.15 m) and 10 cm more - the terrain made from the grid lies up to 17 cm off it on a rough sea
		/// floor - but never more than four fifths of its height over the terrain's surface. Raft sinks its boulders
		/// a third of their size into the slopes - but sunk by a share of its width a flat rock went under, and corals, rocks and
		/// finds sunk by a whole size under water were out of sight: 14 to 27 on every library island (CIFloating, 2026-10-02).
		/// </summary>
		static float LowestShowing(string name, float scale, float surface, float share)
		{
			GameObject proto = PlaceableCatalog.Get(name);
			Bounds b;
			if (proto == null || !PlaceableCatalog.LocalBounds(name, out b)) return float.NegativeInfinity;
			float ly = Mathf.Abs(proto.transform.localScale.y) * scale, height = b.size.y * ly;
			float keep = Mathf.Min(Mathf.Max(share * height, 0.15f) + 0.1f, 0.8f * height);
			return surface + keep - b.max.y * ly;
		}

		/// <summary>Where the middle of a thing's mesh lies from its pivot, turned by its yaw (zero without its bounds).</summary>
		static Vector3 MeshMiddle(string name, float scale, float yaw)
		{
			GameObject proto = PlaceableCatalog.Get(name);
			Bounds b;
			if (proto == null || !PlaceableCatalog.LocalBounds(name, out b)) return Vector3.zero;
			return Quaternion.Euler(0f, yaw, 0f) * (Vector3.Scale(b.center, proto.transform.localScale) * scale);
		}

		/// <summary>
		/// Whether an object is flat and wide (a snow drift: less than a quarter as high as it is wide) - it lies along the
		/// slope (lean: the ground's up there, averaged over its width) instead of sinking to its low edge. Else lean is up.
		/// </summary>
		static bool LiesFlat(string name, float scale, Ground ground, float x, float z, out Vector3 lean)
		{
			lean = Vector3.up;
			GameObject proto = PlaceableCatalog.Get(name);
			Vector3 ls = proto != null ? proto.transform.localScale : Vector3.one;
			Bounds b;
			if (!PlaceableCatalog.LocalBounds(name, out b)) return false;
			float width = Mathf.Max(b.size.x * Mathf.Abs(ls.x), b.size.z * Mathf.Abs(ls.z)) * scale, height = b.size.y * Mathf.Abs(ls.y) * scale;
			if (width < 2f || height > 0.25f * width) return false;
			float d = Mathf.Clamp(width * 0.3f, 0.5f, 4f);
			float dx = (ground.At(x + d, z) - ground.At(x - d, z)) / (2f * d), dz = (ground.At(x, z + d) - ground.At(x, z - d)) / (2f * d);
			lean = new Vector3(-dx, 1f, -dz).normalized;
			// (the ground under its rim must lie about in that plane: at a cliff's edge or over a gully it would hang in the air)
			float h = ground.At(x, z), r = width * 0.38f;
			for (int i = 0; i < 8; i++)
			{
				float a = i * Mathf.PI / 4f, ox = Mathf.Cos(a) * r, oz = Mathf.Sin(a) * r;
				if (ground.At(x + ox, z + oz) < h + dx * ox + dz * oz - 0.5f) { lean = Vector3.down; break; }
			}
			return true;
		}

		/// <summary>Largest dimension (m) of a catalog object spawned at its prototype's own scale (the scale its Raft original had).</summary>
		static float SpawnSize(string name)
		{
			GameObject proto = PlaceableCatalog.Get(name);
			Vector3 ls = proto != null ? proto.transform.localScale : Vector3.one;
			return PlaceableCatalog.ApproxSize(name) * Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
		}

		/// <summary>How far an object's body reaches from its middle across the ground (its meshes' bounds as placed, the wider
		/// way; 0 if unknown).</summary>
		static float BodyReach(IslandObject o)
		{
			Bounds b;
			if (!PlaceableCatalog.LocalBounds(o.Name, out b)) return 0f;
			Vector3 c = Vector3.Scale(b.center, o.Scale), e = Vector3.Scale(b.extents, o.Scale);
			return new Vector2(c.x, c.z).magnitude + Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.z));
		}

		/// <summary>How close things under water may stand.</summary>
		static float SeaFootprint(string cat, float size)
		{
			switch (cat)
			{
				case CatSeaRocks: return Mathf.Clamp(size * 0.2f, 0.6f, 5f);
				case CatSunken: return Mathf.Clamp(size * 0.3f, 0.6f, 3f);
				case CatSeaFinds: return 0.5f;
				default: return Mathf.Clamp(size * 0.25f, 0.35f, 1.8f);
			}
		}

		/// <summary>
		/// Dresses the sea like Raft's islands of the style (RaftUnderwater): per depth band, each measured kind as dense
		/// as there (times the slider), at the depths and distances from the coast it has there; ores and cliff rocks keep
		/// to steep slopes; big rock formations sink into the drop-off as deep as Raft's do; corals gather in reefs.
		/// </summary>
		static void PlanSea(IslandGenSettings s, Ground ground, SeaGround sea, SpotGrid spots, List<IslandObject> owners, List<string> cats, GenReport report, float cap)
		{
			for (int ci = 0; ci < SeaCategories.Length; ci++)
			{
				string cat = SeaCategories[ci];
				report.Counts[cat] = 0;
				float f = SeaFactor(AmountOf(s, cat)) * cap;
				if (f <= 0f) continue;
				f *= SeaCap(s, sea, cat, f);
				var rnd = new System.Random(s.Seed * 4099 + 71 + ci * 577);
				Vector2 reefOff = RandomOffset(rnd);
				int placed = 0;
				ReefMask reefs = null;
				if (cat == CatWater)
				{
					float corals = 0f;
					foreach (SeaThing t in SeaThingsOf(s, cat))
						for (int b = 0; b < RaftUnderwater.BandCount; b++) corals += t.Density[b] * sea.Within(b, SeaGround.Reach(t)) * sea.Step * sea.Step * f;
					reefs = PlanReefs(s, ground, sea, corals);
				}
				foreach (SeaThing t in SeaThingsOf(s, cat))
				{
					GameObject proto = PlaceableCatalog.Get(t.Name);
					if (proto == null) continue;
					bool pickup = t.Name.StartsWith("Pickup_");
					float protoSize = Mathf.Max(0.2f, SpawnSize(t.Name)); // (as spawned at the prototype's scale, like Raft's measured size)
					// (one try at a spot of a depth band within the kind's reach of the land: the spot rules, then the thing set down
					// there; steep: ores and cliff rocks keep to the steep slopes - the rule the guarantee below may drop)
					float reach = SeaGround.Reach(t), placeReach = reach;
					Func<int, System.Random, bool, bool> tryPlace = (b, r, steep) =>
					{
						int cell;
						if (reefs != null)
						{
							// (corals and plants only on the island's reefs - see PlanReefs; sea vines and kelp round them too - at
							// whatever depth the reef is, as deep as Raft has the kind)
							List<int> pool = ReefMask.Loose(t.Name) ? reefs.HaloAll : reefs.CoreAll;
							if (pool.Count == 0) return false;
							cell = pool[r.Next(pool.Count)];
							if (sea.Coast[cell] > placeReach) return false;
						}
						else
						{
							int near = sea.Within(b, placeReach);
							if (near == 0) return false;
							cell = sea.Bands[b][r.Next(near)];
						}
						float x = (cell % sea.Res + (float)r.NextDouble() - 0.5f) * sea.Step, z = (cell / sea.Res + (float)r.NextDouble() - 0.5f) * sea.Step;
						float h = ground.At(x, z), depth = Sea - h;
						if (depth < 0.3f) return false;
						if (b >= 0 ? RaftUnderwater.BandOf(depth) != b : depth < t.DepthLow * 0.6f || depth > t.DepthHigh * 1.4f + 2f) return false;
						float slope = ground.Slope(x, z);
						if (t.Slope >= 40f && steep) { if ((float)r.NextDouble() > SS(12f, t.Slope, slope) + 0.08f) return false; } // (ores and cliff rocks keep to the steep slopes)
						else if (slope > 62f) return false;
						// As big as Raft's (a little either way); never much taller than the water is deep
						float scale = pickup ? 1f : Mathf.Clamp(t.Size / protoSize, 0.3f, 3f) * (0.8f + 0.4f * (float)r.NextDouble());
						float actual = protoSize * scale;
						// (only rocks right at the shore break the surface, as on Raft's islands; further out they stay under water)
						float room = sea.Coast[cell] < 8f ? Mathf.Max(4f, depth * 1.3f) : Mathf.Max(2.5f, depth * 0.8f);
						if (!pickup && actual > room) { scale *= room / actual; actual = protoSize * scale; }
						float foot = SeaFootprint(cat, actual);
						if (reefs != null) foot = Mathf.Max(0.22f, foot * 0.65f); // (a reef's corals grow close together, as Raft's do)
						if (!spots.Free(x, z, foot)) return false;
						spots.Add(x, z, foot);
						float y = h - BaseDrop(ground, x, z, t.Name, cat, scale); // (all of its base on the sea floor, as on land)
						if (t.Above < -0.3f && t.Size > 0.5f) y += Mathf.Max(t.Above / t.Size, -0.6f) * actual; // (sunk into the slope as on Raft's islands)
						else if (pickup && slope > 30f) y -= 0.15f;
						y = Mathf.Max(y, LowestShowing(t.Name, scale, ground.Surface(x, z), pickup ? 0.5f : 0.35f)); // (never out of sight: a find half)
						float yaw = (float)r.NextDouble() * 360f;
						Vector3 euler = cat == CatSeaRocks ? new Vector3(((float)r.NextDouble() - 0.5f) * 16f, yaw, ((float)r.NextDouble() - 0.5f) * 16f) : new Vector3(0f, yaw, 0f);
						owners.Add(new IslandObject { Name = t.Name, Position = new Vector3(x, y, z), EulerRotation = euler, Scale = proto.transform.localScale * scale });
						cats.Add(cat);
						return true;
					};
					int ofKind = 0, bestBand = -1, bestAnyBand = -1;
					float expectedAll = 0f, bestExpected = 0f, bestAny = 0f, reefWanted = 0f;
					// As many as Raft has on as much ground within their reach of the land - not on all of the band's ground: on a
					// wide shallow shelf most of it is further out, and its share crowded the strip by the shore - a wall of ice
					// boulders along Glacier Station's shores, ten times Temperance's (the review, 2026-10-03)
					for (int b = 0; b < RaftUnderwater.BandCount; b++)
					{
						if (t.Density[b] <= 0f) continue;
						float onAll = t.Density[b] * sea.Area(b) * f; // (on all of the band's ground: whether the guarantee below gives one)
						expectedAll += onAll;
						if (onAll > bestAny) { bestAny = onAll; bestAnyBand = b; }
						int within = sea.Within(b, reach);
						if (within == 0) continue;
						float expected = t.Density[b] * within * sea.Step * sea.Step * f;
						if (expected > bestExpected) { bestExpected = expected; bestBand = b; }
						if (reefs != null) { reefWanted += expected; continue; }
						int n = (int)expected + (rnd.NextDouble() < expected - (int)expected ? 1 : 0);
						for (int attempt = 0, got = 0; attempt < n * 8 + 4 && got < n; attempt++)
							if (tryPlace(b, rnd, true)) { got++; placed++; ofKind++; }
					}
					// (a reef's corals: as many of each as the island's sea floor would hold at Raft's density, all on the reefs)
					if (reefs != null && reefWanted > 0f)
					{
						int n = (int)reefWanted + (rnd.NextDouble() < reefWanted - (int)reefWanted ? 1 : 0);
						for (int attempt = 0, got = 0; attempt < n * 14 + 4 && got < n; attempt++)
							if (tryPlace(-1, rnd, true)) { got++; placed++; ofKind++; }
					}
					// (each of Raft's finds is there where Raft has them - ores, clay, sand, scrap, clams: the user wants Raft's
					// resources under water on every island, 2026-10-03; a small island's share of a rare one rounded to none, and
					// ores keep to steep slopes it may not have - one is placed then, on any slope of its usual depth; and where its
					// depth lies further out than Raft has it, on the nearest ground of that depth: Tide Farm's copper ore)
					if (cat == CatSeaFinds && pickup && ofKind == 0 && expectedAll >= 0.1f && bestAnyBand >= 0)
					{
						var extra = new System.Random(s.Seed * 7919 + StableHash(t.Name));
						for (int attempt = 0; attempt < 80 && ofKind == 0 && bestBand >= 0; attempt++)
							if (tryPlace(bestBand, extra, false)) { placed++; ofKind++; }
						List<int> band = sea.Bands[bestAnyBand];
						placeReach = Mathf.Max(reach, sea.Coast[band[Mathf.Min(band.Count - 1, 40)]]);
						for (int attempt = 0; attempt < 80 && ofKind == 0; attempt++)
							if (tryPlace(bestAnyBand, extra, false)) { placed++; ofKind++; }
						placeReach = reach;
					}
				}
				report.Counts[cat] = placed;
			}
		}

		#endregion

		#region Content: creatures and loot

		/// <summary>A hash of a name that is the same in every run (string.GetHashCode needn't be).</summary>
		static int StableHash(string s)
		{
			unchecked
			{
				int h = 23;
				foreach (char c in s) h = h * 31 + c;
				return h & 0x7fffffff;
			}
		}

		/// <summary>The hostile creatures of each style (AI types), when no kinds are chosen.</summary>
		static readonly string[][] StyleHostiles =
		{
			new[] { "Boar", "StoneBird" }, new[] { "PolarBear" }, new[] { "Hyena", "Rat" }, new[] { "Bear", "Boar", "BugSwarm_Bee" }, new[] { "Rat", "Roach", "StoneBird" },
		};
		static readonly string[][] StyleFriendly =
		{
			new[] { "Chicken", "Goat" }, new[] { "Goat" }, new[] { "Llama", "Chicken" }, new[] { "Goat", "Chicken" }, new[] { "Chicken" },
		};
		static readonly string[] SeaKinds = { "Turtle", "Stingray", "PufferFish", "Dolphin" };

		/// <summary>The hostile kinds used for these settings (the chosen ones that exist, else the style's).</summary>
		public static string[] HostileKindsOf(IslandGenSettings s)
		{
			string[] chosen = (s.HostileKinds ?? "").Split(',').Select(k => k.Trim()).Where(k => k.Length > 0 && ContentCatalog.IsCreature("Creature_" + k)).ToArray();
			return chosen.Length > 0 ? chosen : StyleHostiles[Mathf.Clamp(s.Style, 0, StyleHostiles.Length - 1)];
		}

		/// <summary>How many animals live at one spot of a kind.</summary>
		static int HerdSize(string kind, System.Random rnd)
		{
			switch (kind)
			{
				case "Rat": case "Rat_Tangaroa": case "Roach": case "Hyena": case "PufferFish": return 2 + rnd.Next(2);
				case "Chicken": return 2 + rnd.Next(3);
				case "Goat": case "Llama": case "Boar": case "Pig": case "Turtle": case "Stingray": case "Dolphin": return 1 + rnd.Next(2);
				default: return 1;
			}
		}

		/// <summary>The loot tiers: candidate stacks "Item*min-max" ("a|b" = one of them). Only items this Raft has are used.</summary>
		public static readonly string[][] LootTiers =
		{
			new[] { "Plank*8-15", "Plastic*6-12", "Thatch*6-10", "Rope*2-4" },
			new[] { "Nail*6-12", "Stone*4-8", "Scrap*3-6", "Coconut*2-3|Watermelon*1-2|Raw_Potato*2-4|Mango*2-3|Banana*2-3|Pineapple*1-2" },
			new[] { "MetalOre*3-6", "CopperOre*2-5", "Bolt*3-6", "Hinge*2-4" },
			new[] { "MetalIngot*3-6", "CopperIngot*2-5", "CircuitBoard*1-2" },
			new[] { "TitaniumIngot*1-3", "ExplosiveGoo*1-3", "Battery*1-1", "HealingSalve_Good*1-2" },
		};

		/// <summary>The box each tier comes in (1 barrel ... 5 large chest); under water always a sunken barrel.</summary>
		static readonly string[] TierBoxes = { "Loot_Barrel", "Loot_ChestSmall", "Loot_Crate", "Loot_Chest", "Loot_ChestLarge" };
		static readonly string[] TierTitles = { "Supplies", "Small chest", "Crate", "Chest", "Treasure chest" };

		/// <summary>A box's items for a tier (1..5): each of the tier's stacks, and now and then one from the tier below.</summary>
		public static string TierLoot(int tier, System.Random rnd)
		{
			tier = Mathf.Clamp(tier, 1, 5);
			var stacks = new List<KeyValuePair<string, int>>();
			Action<string> add = entry =>
			{
				string[] options = entry.Split('|');
				string[] p = options[rnd.Next(options.Length)].Split('*');
				string[] range = p.Length > 1 ? p[1].Split('-') : new[] { "1" };
				int min = int.Parse(range[0], CultureInfo.InvariantCulture), max = range.Length > 1 ? int.Parse(range[1], CultureInfo.InvariantCulture) : min;
				int amount = min + rnd.Next(max - min + 1);
				if (ContentCatalog.ItemExists(p[0]) && !stacks.Any(x => x.Key == p[0])) stacks.Add(new KeyValuePair<string, int>(p[0], amount));
			};
			foreach (string entry in LootTiers[tier - 1]) add(entry);
			if (tier > 1 && rnd.NextDouble() < 0.4) add(LootTiers[tier - 2][rnd.Next(LootTiers[tier - 2].Length)]);
			return ObjectProps.LootText(stacks);
		}

		static void PlanContent(IslandGenSettings s, Ground ground, SpotGrid spots, List<IslandObject> owners, List<string> cats, GenReport report, List<IslandObject> result)
		{
			var rnd = new System.Random(s.Seed * 31 + 7);
			float apart = Mathf.Clamp(s.Radius * 0.22f, 8f, 30f);
			var creatureSpots = new List<Vector2>();
			Func<int[], Func<float, float, bool>, float, Vector2?> find = (zones, ok, keepApart) =>
			{
				// (a spot in one of the zones where ok(height above the sea, slope) holds, away from the other creatures)
				if (zones.All(zz => ground.Zones[zz].Count == 0)) return null;
				for (int tries = 0; tries < 300; tries++)
				{
					List<int> cells = ground.Zones[zones[rnd.Next(zones.Length)]];
					if (cells.Count == 0) continue;
					int cell = cells[rnd.Next(cells.Count)];
					float x = (cell % ground.Res) * ground.Step, z = (cell / ground.Res) * ground.Step;
					float above = ground.At(x, z) - Sea;
					if (!ok(above, ground.Slope(x, z))) continue;
					float need = tries < 200 ? keepApart : keepApart * 0.4f;
					if (creatureSpots.Any(c => (c - new Vector2(x, z)).sqrMagnitude < need * need)) continue;
					return new Vector2(x, z);
				}
				return null;
			};
			float[] stats = ObjectProps.Presets.First(p => p.Key == s.Difficulty).Value;
			Action<string, Vector2, float> creature = (kind, p, lift) =>
			{
				spots.Clear(p.x, p.y, 2.5f); // (room for the animals: the nature there goes)
				var props = new Dictionary<string, string> { { ObjectProps.CreatureCount, HerdSize(kind, rnd).ToString(CultureInfo.InvariantCulture) } };
				if (s.Difficulty != "Normal" && ContentCatalog.CreatureOf("Creature_" + kind) != null && ContentCatalog.CreatureOf("Creature_" + kind).Category == ContentCatalog.HostileCategory)
				{
					props[ObjectProps.CreatureHealth] = ObjectProps.Format(stats[0]);
					props[ObjectProps.CreatureDamage] = ObjectProps.Format(stats[1]);
					props[ObjectProps.CreatureSpeed] = ObjectProps.Format(stats[2]);
				}
				GameObject proto = PlaceableCatalog.Get("Creature_" + kind);
				// (not under the terrain's own surface where it lies above the blended height)
				result.Add(new IslandObject { Name = "Creature_" + kind, Position = new Vector3(p.x, Mathf.Max(ground.At(p.x, p.y), ground.Surface(p.x, p.y)) + lift, p.y), EulerRotation = new Vector3(0, (float)rnd.NextDouble() * 360f, 0), Scale = proto != null ? proto.transform.localScale : Vector3.one, Props = props, Unscaled = proto == null });
				spots.Add(p.x, p.y, 1.5f);
				owners.Add(null);
				cats.Add(null);
				creatureSpots.Add(p);
				report.Creatures++;
			};

			string[] hostiles = HostileKindsOf(s);
			for (int i = 0; i < s.Hostiles; i++)
			{
				string kind = hostiles[rnd.Next(hostiles.Length)];
				bool flies = kind.StartsWith("StoneBird") || kind == "BugSwarm_Bee";
				Vector2? p = find(new[] { ZLand }, (above, slope) => above > 3f && slope < 22f, apart);
				if (p.HasValue) creature(kind, p.Value, flies ? 2f : 0f);
			}
			string[] friendly = StyleFriendly[Mathf.Clamp(s.Style, 0, StyleFriendly.Length - 1)];
			for (int i = 0; i < s.Friendly; i++)
			{
				Vector2? p = find(new[] { ZLand, ZBeach }, (above, slope) => above > 1.5f && slope < 18f, apart * 0.6f);
				if (p.HasValue) creature(friendly[rnd.Next(friendly.Length)], p.Value, 0f);
			}
			for (int i = 0; i < s.SeaLife; i++)
			{
				Vector2? p = find(new[] { ZWater }, (above, slope) => above < -3f, apart * 0.5f);
				if (p.HasValue) creature(SeaKinds[rnd.Next(SeaKinds.Length)], p.Value, 1.5f + (float)rnd.NextDouble() * 1.5f);
			}

			// Loot boxes: out in the open, or tucked in next to a tree, bush or rock; now and then sunken
			var hideBy = new List<int>();
			// (rocks by how far their body reaches, not their spacing footprint - a third of their size, at most 4 m: a box
			// tucked in by a 12 m boulder, or put in the open next to one, lay inside it - CA21)
			var body = new Dictionary<int, float>();
			var rocks = new List<Vector3>();
			for (int i = 0; i < owners.Count; i++)
			{
				IslandObject o = owners[i];
				if (o != null && !spots.Removed[i] && o.Position.y > Sea + 0.6f && !o.Name.StartsWith("Pickup_")) hideBy.Add(i);
				if (o == null || spots.Removed[i] || (cats[i] != CatRocks && cats[i] != CatSeaRocks)) continue;
				float reach = BodyReach(o);
				if (reach <= spots.Spots[i].z) continue;
				body[i] = reach;
				rocks.Add(new Vector3(o.Position.x, o.Position.z, reach));
			}
			for (int i = 0; i < s.Loot; i++)
			{
				int tier = s.LootMin + rnd.Next(s.LootMax - s.LootMin + 1);
				bool sunken = ground.Zones[ZWater].Count > 0 && rnd.NextDouble() < 0.2;
				Vector2? spot = null;
				for (int tries = 0; tries < 200 && !spot.HasValue; tries++)
				{
					Vector2 p;
					if (sunken)
					{
						int cell = ground.Zones[ZWater][rnd.Next(ground.Zones[ZWater].Count)];
						p = new Vector2((cell % ground.Res) * ground.Step, (cell / ground.Res) * ground.Step);
						if (ground.At(p.x, p.y) - Sea > -2f) continue;
					}
					else if (s.LootHidden && hideBy.Count > 0 && tries < 150)
					{
						int byIndex = hideBy[rnd.Next(hideBy.Count)];
						Vector3 by = spots.Spots[byIndex];
						float reach;
						if (body.TryGetValue(byIndex, out reach)) by.z = reach;
						float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = by.z + 0.9f + (float)rnd.NextDouble() * 0.8f;
						p = new Vector2(by.x + Mathf.Cos(a) * d, by.y + Mathf.Sin(a) * d);
					}
					else
					{
						List<int> cells = rnd.NextDouble() < 0.7 ? ground.Zones[ZLand] : ground.Zones[ZBeach];
						if (cells.Count == 0) cells = ground.Zones[ZBeach];
						if (cells.Count == 0) break;
						int cell = cells[rnd.Next(cells.Count)];
						p = new Vector2((cell % ground.Res) * ground.Step, (cell / ground.Res) * ground.Step);
					}
					float above = ground.At(p.x, p.y) - Sea;
					if (!sunken && (above < 0.6f || ground.Slope(p.x, p.y) > 25f)) continue;
					// (nor on a cliff's edge the blended slope misses - a sea stack's rim: the box sank to the foot of the drop, 2-3 m)
					if (!sunken && SurfaceFall(ground, p.x, p.y, 0.6f) > 0.6f) continue;
					if (!s.LootHidden && !sunken && !spots.Free(p.x, p.y, 0.8f)) continue;
					if (rocks.Any(r => (r.x - p.x) * (r.x - p.x) + (r.y - p.y) * (r.y - p.y) < (r.z + 0.5f) * (r.z + 0.5f))) continue;
					spot = p;
				}
				if (!spot.HasValue) continue;
				Vector2 at = spot.Value;
				spots.Clear(at.x, at.y, s.LootHidden ? 0.6f : 1.5f);
				string box = sunken ? "Loot_SunkenBarrel" : TierBoxes[tier - 1];
				if (tier == 1 && !sunken && rnd.NextDouble() < 0.5) box = "Loot_Box";
				var props = new Dictionary<string, string> { { ObjectProps.LootItems, TierLoot(tier, rnd) }, { ObjectProps.NoteTitle, (sunken ? "Sunken barrel" : TierTitles[tier - 1]) + " (tier " + tier + ")" } };
				GameObject proto = PlaceableCatalog.Get(box);
				result.Add(new IslandObject { Name = box, Position = new Vector3(at.x, ground.At(at.x, at.y) - BaseDrop(ground, at.x, at.y, box, "", 1f), at.y), EulerRotation = new Vector3(0, (float)rnd.NextDouble() * 360f, 0), Scale = proto != null ? proto.transform.localScale : Vector3.one, Props = props, Unscaled = proto == null });
				spots.Add(at.x, at.y, 0.8f);
				owners.Add(null);
				cats.Add(null);
				report.Chests++;
			}
			// (nature cleared away for content no longer counts)
			foreach (string cat in Categories)
				report.Counts[cat] = 0;
			for (int i = 0; i < owners.Count; i++)
				if (owners[i] != null && !spots.Removed[i]) { string c = cats[i]; report.Counts[c] = report.Count(c) + 1; }
		}

		#endregion

		#region Islands generated while sailing

		/// <summary>The editor's build area, which generated island files use too.</summary>
		internal static readonly Vector3 BuildArea = new Vector3(1000f, 600f, 1000f);

		/// <summary>
		/// LM7: the terrain an island of these settings needs - the build area, or for a variation of one of Raft's islands
		/// bigger than it (Temperance: 1074 x 926 m of land) a square that holds its real land and shallow water, at the size
		/// asked for, with room for the flat seabed at the edge (FlattenEdges). Before, Temperance was scaled down to fit.
		/// </summary>
		public static Vector3 AreaFor(IslandGenSettings s)
		{
			if (s == null || string.IsNullOrEmpty(s.Source)) return BuildArea;
			RaftIsland i = RaftIslands.Get(s.Source);
			HeightField f = i != null ? RaftIslands.Heights(i) : null;
			if (f == null) return BuildArea;
			float scale = Mathf.Clamp(s.Radius, IslandGenSettings.MinRadius, IslandGenSettings.MaxSourceRadius) / Mathf.Max(1f, i.Radius);
			float half = SourceReach(i.Scene, f) * scale * Mathf.Sqrt(Mathf.Max(1f, s.Stretch)) + Mathf.Clamp01(s.SourceWobble) * 0.2f * i.Radius * scale + 90f;
			float size = Mathf.Clamp(Mathf.Ceil(half * 2f / 50f) * 50f, BuildArea.x, 2000f);
			return new Vector3(size, BuildArea.y, size);
		}

		static readonly Dictionary<string, float> sourceReach = new Dictionary<string, float>();

		/// <summary>How far a Raft island's land and shallow water (down to 8 m) reach from its middle in any direction (m, as
		/// measured): the variation is turned by its Direction (StretchAngle), so a corner of the land can end up along x or z.</summary>
		static float SourceReach(string scene, HeightField f)
		{
			float r;
			if (sourceReach.TryGetValue(scene, out r)) return r;
			r = 0f;
			for (int z = 0; z < f.Nz; z++)
				for (int x = 0; x < f.Nx; x++)
					if (f.H[z, x] > -8f)
					{
						float dx = x - (f.Nx - 1) / 2f, dz = z - (f.Nz - 1) / 2f;
						r = Mathf.Max(r, Mathf.Sqrt(dx * dx + dz * dz) * f.Cell);
					}
			sourceReach[scene] = r;
			return r;
		}
		internal const int BuildResolution = 513;

		/// <summary>
		/// A complete island file from settings, without the editor: heights, objects (the object catalog must be
		/// built) and style; textures are painted automatically when it spawns.
		/// </summary>
		public static IslandFile CreateFile(IslandGenSettings settings, string name)
		{
			IslandGenSettings s = settings.Copy();
			s.Clamp();
			Vector3 area = AreaFor(s);
			float[,] metres = HeightsMetres(s, area, BuildResolution);
			UseSea(s);
			var file = new IslandFile
			{
				Name = name,
				WaterLevel = s.WaterLevel,
				TerrainSize = area,
				HeightmapResolution = BuildResolution,
				Heights = Normalised(metres, area.y),
				Style = s.Style == TerrainPainter.Tropical ? "" : TerrainPainter.StyleName(s.Style),
			};
			file.Objects = PlanAll(s, metres, area);
			var planned = new HashSet<IslandObject>(file.Objects);
			List<string> built = GenBuildings.Apply(file, s);
			KeyValuePair<int, int> gathered = GenGather.Apply(file, s, s.Seed, planned);
			if (gathered.Key + gathered.Value > 0) Debug.Log("[CUSTOM ISLANDS] Generated island '" + name + "': " + gathered.Key + " things to gather on the land, " + gathered.Value + " in the shallows");
			if (built.Count > 0) Debug.Log("[CUSTOM ISLANDS] Generated island '" + name + "': " + string.Join(", ", built.ToArray()));
			return file;
		}

		/// <summary>Bilinear height (0..1) at terrain-local x, z.</summary>
		internal static float SampleHeights(float[,] h, int res, float step, float x, float z)
		{
			float fx = Mathf.Clamp(x / step, 0, res - 1.001f), fz = Mathf.Clamp(z / step, 0, res - 1.001f);
			int x0 = (int)fx, z0 = (int)fz;
			float tx = fx - x0, tz = fz - z0;
			return Mathf.Lerp(Mathf.Lerp(h[z0, x0], h[z0, x0 + 1], tx), Mathf.Lerp(h[z0 + 1, x0], h[z0 + 1, x0 + 1], tx), tz);
		}

		/// <summary>Random settings for an island generated while sailing, in one of the allowed styles.</summary>
		public static IslandGenSettings RandomSettings(System.Random rnd, int[] styles)
		{
			var s = new IslandGenSettings
			{
				Seed = rnd.Next(1, 999999),
				Radius = 52f + (float)rnd.NextDouble() * 96f, // fits between Raft's own islands more often
				Height = 15f + (float)rnd.NextDouble() * 55f,
				Roughness = 0.3f + (float)rnd.NextDouble() * 0.6f,
				Peaks = 1 + rnd.Next(3),
				ObjectDensity = 0.4f + (float)rnd.NextDouble() * 0.4f,
				Style = styles != null && styles.Length > 0 ? styles[rnd.Next(styles.Length)] : TerrainPainter.Tropical,
			};
			// Now and then an island that looks different: long, with bays, cliffs or a valley
			if (rnd.NextDouble() < 0.4) { s.Stretch = 1f + (float)rnd.NextDouble() * 0.9f; s.StretchAngle = (float)rnd.NextDouble() * 180f; }
			if (rnd.NextDouble() < 0.3) s.Bays = 0.3f + (float)rnd.NextDouble() * 0.5f;
			if (rnd.NextDouble() < 0.3) s.Cliffs = 0.2f + (float)rnd.NextDouble() * 0.5f;
			if (rnd.NextDouble() < 0.25) s.Valleys = 0.3f + (float)rnd.NextDouble() * 0.4f;
			// Under water: a gentle slope or a wall into the deep, as Raft's islands vary
			s.DropOff = 0.15f + (float)rnd.NextDouble() * 0.8f;
			return s;
		}

		#endregion

		#region Preview

		/// <summary>
		/// A top-down picture of heights (m above the terrain's base, from HeightsMetres): the sea by depth, the land
		/// in the style's colours by height and slope, lit from the north-west. Reuses the texture when it fits.
		/// </summary>
		public static Texture2D Preview(float[,] m, float step, int style, Texture2D reuse = null, float waterLevel = IslandFile.DefaultWaterLevel)
		{
			float sea = waterLevel;
			int res = m.GetLength(0);
			Texture2D tex = reuse != null && reuse.width == res && reuse.height == res ? reuse : new Texture2D(res, res, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CI_GeneratorPreview" };
			var px = new Color32[res * res];
			Color sand, grass, rock, high;
			switch (style)
			{
				case TerrainPainter.Snowy: sand = new Color(0.78f, 0.8f, 0.82f); grass = new Color(0.93f, 0.95f, 0.98f); rock = new Color(0.5f, 0.52f, 0.56f); high = Color.white; break;
				case TerrainPainter.Desert: sand = new Color(0.93f, 0.8f, 0.55f); grass = new Color(0.82f, 0.64f, 0.42f); rock = new Color(0.66f, 0.38f, 0.28f); high = new Color(0.75f, 0.5f, 0.35f); break;
				case TerrainPainter.Forest: sand = new Color(0.88f, 0.82f, 0.62f); grass = new Color(0.25f, 0.45f, 0.2f); rock = new Color(0.5f, 0.49f, 0.47f); high = new Color(0.42f, 0.44f, 0.4f); break;
				case TerrainPainter.Volcanic: sand = new Color(0.33f, 0.31f, 0.3f); grass = new Color(0.27f, 0.25f, 0.24f); rock = new Color(0.42f, 0.26f, 0.2f); high = new Color(0.2f, 0.18f, 0.18f); break;
				default: sand = new Color(0.94f, 0.86f, 0.62f); grass = new Color(0.36f, 0.62f, 0.27f); rock = new Color(0.55f, 0.53f, 0.5f); high = new Color(0.45f, 0.5f, 0.38f); break;
			}
			float top = 1f;
			foreach (float v in m) top = Mathf.Max(top, v - sea);
			Vector3 light = new Vector3(-0.55f, 0.75f, 0.4f).normalized;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					float e = m[z, x], above = e - sea;
					float gx = (m[z, Mathf.Min(res - 1, x + 1)] - m[z, Mathf.Max(0, x - 1)]) / (2f * step);
					float gz = (m[Mathf.Min(res - 1, z + 1), x] - m[Mathf.Max(0, z - 1), x]) / (2f * step);
					Color c;
					if (above < 0f)
					{
						float depth = -above;
						c = Color.Lerp(new Color(0.42f, 0.8f, 0.82f), new Color(0.1f, 0.36f, 0.58f), SS(0f, 12f, depth));
						c = Color.Lerp(c, new Color(0.06f, 0.24f, 0.44f), SS(12f, 20f, depth));
						c = Color.Lerp(c, new Color(0.02f, 0.1f, 0.22f), SS(25f, 110f, depth)); // (the drop-off into the deep)
					}
					else
					{
						float slope = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
						c = Color.Lerp(sand, grass, SS(ShelfHeight, ShelfHeight + 2f, above));
						c = Color.Lerp(c, high, SS(0.55f, 0.95f, above / top) * 0.7f);
						c = Color.Lerp(c, rock, SS(28f, 40f, slope));
						Vector3 n = new Vector3(-gx, 1f, -gz).normalized;
						c *= Mathf.Clamp(0.55f + 0.6f * Vector3.Dot(n, light), 0.35f, 1.25f);
					}
					px[z * res + x] = c;
				}
			tex.SetPixels32(px);
			tex.Apply(false);
			return tex;
		}

		#endregion
	}
}
