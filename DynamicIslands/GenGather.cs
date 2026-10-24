using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The generator's things to gather (ROADMAP LM9, the user 2026-10-05: options for gatherables on land and under
	/// water, little or much): Gather spreads what suits the island's style on the land - trees to cut, fruit, berries,
	/// flowers - and Shallows Raft's sea finds just off the shore (sand, clay, stone, iron and copper ore, scrap, giant
	/// clams, seaweed), as the library's islands have them. Both 0 by default: islands made before are unchanged.
	/// Which kinds come can be switched off one by one (GatherOff: kind keys, "" = all on, so older settings and recipes
	/// are unchanged), and ShallowsDepth sets how far out the sea finds go (how deep: 6 m as before).
	/// </summary>
	public static class GenGather
	{
		/// <summary>The style's things to gather (by TerrainPainter style), each with whether it is a tree (more room).</summary>
		static readonly Dictionary<int, KeyValuePair<string, bool>[]> Kinds = new Dictionary<int, KeyValuePair<string, bool>[]>
		{
			{ TerrainPainter.Tropical, new[] { T("Pickup_Landmark_Tree_Palm 1"), T("Pickup_Landmark_Tree_Palm 3"), T("Pickup_Landmark_Tree_Mango"), S("Pickup_Landmark_PineappleLandmark"),
				S("Pickup_Landmark_WatermelonLandmark"), S("Banana_Bush_2"), S("Pickup_Landmark_Flower_Red"), S("Pickup_Landmark_Flower_Yellow"), S("Pickup_Landmark_DirtPickup") } },
			{ 1, new[] { T(PlaceableCatalog.SnowyPine), S("Pickup_Landmark_BerryBush"), S("Pickup_Landmark_Flower_White"), S("Pickup_Landmark_Flower_Blue") } },
			{ 2, new[] { T("Pickup_Landmark_Tree_Palm 2"), T("Pickup_Landmark_Tree_Palm 4"), S("Pickup_Landmark_PineappleLandmark"), S("Pickup_Landmark_WatermelonLandmark"),
				S("Pickup_Landmark_Flower_Yellow"), S("Pickup_Landmark_Flower_Red") } },
			{ 3, new[] { T("Pickup_Landmark_Tree_Birch"), T("Pickup_Landmark_Tree_Pine"), S("Pickup_Landmark_BerryBush"), S("Pickup_Landmark_Flower_Blue"), S("Pickup_Landmark_Flower_White"),
				S("Pickup_Landmark_Flower_Red"), S("Pickup_Landmark_DirtPickup") } },
			{ 4, new[] { T("Pickup_Landmark_Tree_Palm 3"), T("Pickup_Landmark_Tree_Mango"), S("Pickup_Landmark_PineappleLandmark"), S("Pickup_Landmark_Flower_Black"), S("Pickup_Landmark_Flower_Red"), S("Pickup_Landmark_DirtPickup") } },
		};
		/// <summary>
		/// The object list's groups of things to gather, by the style of island they suit (ROADMAP LM11, the user 2026-10-05:
		/// "by style"). A thing can be in several (berries suit snowy and forest islands). Raft has no date palm: desert
		/// islands get Raft's palms that its desert-like islands have, and Caravan Town's acacias.
		/// </summary>
		public static readonly string[] Styles =
		{
			"Tropical: palms, mangoes, pineapples, melons, bananas, flowers", "Snowy: snowy pines, berries, white and blue flowers",
			"Desert: palms, acacias, pineapples, melons, flowers", "Forest: birches, pines, berries, mushrooms, flowers",
			"Volcanic: palms, mangoes, pineapples, black and red flowers", "Sea finds (under water): sand, clay, stone, ores, scrap, clams, seaweed, silver algae",
			"Finds on land: stone, scrap, titanium, planks, plastic, dirt, wild beehives", "Other",
		};

		static readonly System.Text.RegularExpressions.Regex[] StyleOf =
		{
			R(@"Tree_Palm [13]$|Palmtree \d|Tree_Mango|MangoTree|PineappleLandmark|WatermelonLandmark|Tree_Banana|Tree_Tangaroa|Strawberry|Flower_(Red|Yellow)$"),
			R(@"Tree_PineSnowy$|BerryBush|Flower_(White|Blue)$"),
			R(@"Tree_Palm [24]$|AcaciaTree|PineappleLandmark|WatermelonLandmark|Flower_(Yellow|Red)$|Sand_Caravan$"),
			R(@"Tree_Birch|Tree_Pine$|BerryBush|Mushroom|Flower_(Blue|White|Red)$"),
			R(@"Tree_Palm 3$|Tree_Mango|PineappleLandmark|Flower_(Black|Red)$"),
			R(@"_OceanBottom$|GiantClam|SilverAlgae|^SeaVine|Landmark_Sand$|Clay \d|Landmark_Rock \d|Iron \d|Copper \d"),
			R(@"_Land$|DirtPickup|Beehive"),
		};
		static System.Text.RegularExpressions.Regex R(string p) { return new System.Text.RegularExpressions.Regex(p); }

		/// <summary>The style groups (Styles) a thing to gather is listed under; "Other" when none fits.</summary>
		public static List<string> StylesOf(string name)
		{
			var list = new List<string>();
			for (int i = 0; i < StyleOf.Length; i++) if (StyleOf[i].IsMatch(name ?? "")) list.Add(Styles[i]);
			if (list.Count == 0) list.Add(Styles[Styles.Length - 1]);
			return list;
		}

		static KeyValuePair<string, bool> T(string n) { return new KeyValuePair<string, bool>(n, true); }
		static KeyValuePair<string, bool> S(string n) { return new KeyValuePair<string, bool>(n, false); }

		/// <summary>Raft's sea finds in the shallows, each with its share (lib_sea's shallows).</summary>
		static readonly KeyValuePair<string, int>[] SeaFinds =
		{
			W("Pickup_Landmark_Sand", 4), W("Pickup_Landmark_Clay 1", 2), W("Pickup_Landmark_Clay 2", 2), W("Pickup_Landmark_Rock 1", 2), W("Pickup_Landmark_Rock 2", 2),
			W("Pickup_Landmark_Iron 1", 2), W("Pickup_Landmark_Iron 3", 1), W("Pickup_Landmark_Copper 1", 1), W("Pickup_Landmark_Scrap 1_OceanBottom", 2),
			W("Pickup_Landmark_Scrap 3_OceanBottom", 1), W("Pickup_Landmark_GiantClam", 1), W("SeaVine3_klump", 2),
		};
		static KeyValuePair<string, int> W(string n, int w) { return new KeyValuePair<string, int>(n, w); }

		/// <summary>The kinds the builder can switch off (GatherOff's keys), with what they are called: on the land, then in the shallows.</summary>
		public static readonly string[] LandKeys = { "palm", "datepalm", "mango", "pineapple", "watermelon", "banana", "pine", "birch", "berry", "flower", "dirt", "hive" };
		public static readonly string[] LandLabels = { "Palms", "Desert palms", "Mangoes", "Pineapples", "Watermelons", "Bananas", "Pines", "Birches", "Berries", "Flowers", "Dirt", "Beehives" };
		public static readonly string[] SeaKeys = { "sand", "clay", "stone", "iron", "copper", "scrap", "clam", "seaweed" };
		public static readonly string[] SeaLabels = { "Sand", "Clay", "Stone", "Iron ore", "Copper ore", "Scrap", "Giant clams", "Seaweed" };

		/// <summary>The kind key of one of the objects above ("" if none).</summary>
		public static string KeyOf(string name)
		{
			if (name == ContentCatalog.WildHive) return "hive";
			if (name.Contains("Palm 2") || name.Contains("Palm 4")) return "datepalm";
			if (name.Contains("Palm")) return "palm";
			if (name.Contains("Mango")) return "mango";
			if (name.Contains("Pineapple")) return "pineapple";
			if (name.Contains("Watermelon")) return "watermelon";
			if (name.Contains("Banana")) return "banana";
			if (name.Contains("Tree_Pine")) return "pine";
			if (name.Contains("Birch")) return "birch";
			if (name.Contains("BerryBush")) return "berry";
			if (name.Contains("Flower")) return "flower";
			if (name.Contains("DirtPickup")) return "dirt";
			if (name.Contains("Sand")) return "sand";
			if (name.Contains("Clay")) return "clay";
			if (name.Contains("Rock")) return "stone";
			if (name.Contains("Iron")) return "iron";
			if (name.Contains("Copper")) return "copper";
			if (name.Contains("Scrap")) return "scrap";
			if (name.Contains("GiantClam")) return "clam";
			if (name.Contains("SeaVine")) return "seaweed";
			return "";
		}

		/// <summary>The land kinds a style has (in LandKeys' order): what its pick list offers.</summary>
		public static List<string> LandKeysOf(int style)
		{
			KeyValuePair<string, bool>[] kinds;
			var keys = Kinds.TryGetValue(style, out kinds) ? kinds.Select(k => KeyOf(k.Key)).ToList() : new List<string>();
			if (HivesFor(style)) keys.Add("hive");
			return LandKeys.Where(keys.Contains).ToList();
		}

		static bool HivesFor(int style) { return style != TerrainPainter.Snowy && style != TerrainPainter.Desert; }

		/// <summary>Whether a kind is switched on in a GatherOff list (comma separated keys; "" = all on).</summary>
		public static bool IsOn(string off, string key) { return !(off ?? "").Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries).Any(k => k.Trim().Equals(key, StringComparison.OrdinalIgnoreCase)); }

		/// <summary>The GatherOff list with a kind switched the other way (keys kept in LandKeys/SeaKeys order).</summary>
		public static string Toggle(string off, string key)
		{
			var list = (off ?? "").Split(ListSeparators).Select(k => k.Trim().ToLowerInvariant()).Where(k => k.Length > 0).ToList();
			if (list.Contains(key)) list.Remove(key); else list.Add(key);
			return string.Join(",", LandKeys.Concat(SeaKeys).Where(list.Contains).ToArray());
		}

		/// <summary>What a GatherOff list may be separated by: spawnpool.txt by hand ("palm mango") as the Defaults window takes it.</summary>
		static readonly char[] ListSeparators = { ',', ' ' };

		/// <summary>A GatherOff list with only known keys, in order ("" = all on).</summary>
		public static string Clean(string off)
		{
			var list = (off ?? "").Split(ListSeparators).Select(k => k.Trim().ToLowerInvariant()).ToList();
			return string.Join(",", LandKeys.Concat(SeaKeys).Where(list.Contains).ToArray());
		}

		/// <summary>How deep the shallows' finds go (ShallowsDepth, m): from MinDepth (right at the shore) to MaxDepth (well out
		/// on the shelf); DefaultDepth as before. They always lie at least 0.6 m down.</summary>
		public const float MinDepth = 2f, MaxDepth = 20f, DefaultDepth = 6f;

		/// <summary>At 1 (much): this many things to gather per 1000 m2 of land, sea finds per 1000 m2 of shallows.</summary>
		public const float LandPer1000 = 10f, SeaPer1000 = 30f;
		public const int MaxEach = 400;

		/// <summary>Takes the 1 m cells under a building's footprint (its meshes' bounds, scaled and turned, 0.5 m around): small
		/// things are kept clear of by their middle alone.</summary>
		static void TakeFootprint(IslandObject o, HashSet<long> taken, Func<float, float, long> cell)
		{
			Bounds b;
			if (o == null || string.IsNullOrEmpty(o.Name) || !PlaceableCatalog.LocalBounds(o.Name, out b)) return;
			Vector3 c = Vector3.Scale(b.center, o.Scale), e = Vector3.Scale(b.extents, o.Scale);
			e = new Vector3(Mathf.Abs(e.x), 0f, Mathf.Abs(e.z));
			if (Mathf.Max(e.x, e.z) < 1.5f) return;
			const float pad = 0.5f;
			Quaternion back = Quaternion.Euler(0f, -o.EulerRotation.y, 0f);
			int reach = Mathf.CeilToInt(new Vector2(Mathf.Abs(c.x) + e.x, Mathf.Abs(c.z) + e.z).magnitude + pad) + 1;
			int x0 = Mathf.FloorToInt(o.Position.x), z0 = Mathf.FloorToInt(o.Position.z);
			for (int dx = -reach; dx <= reach; dx++)
				for (int dz = -reach; dz <= reach; dz++)
				{
					float x = x0 + dx + 0.5f, z = z0 + dz + 0.5f;
					Vector3 local = back * new Vector3(x - o.Position.x, 0f, z - o.Position.z) - new Vector3(c.x, 0f, c.z);
					if (Mathf.Abs(local.x) <= e.x + pad && Mathf.Abs(local.z) <= e.z + pad) taken.Add(cell(x, z));
				}
		}

		/// <summary>An object's own scale (as the generator's other objects get it).</summary>
		static Vector3 ScaleOf(string name) { GameObject p = PlaceableCatalog.Get(name); return p != null ? p.transform.localScale : Vector3.one; }

		/// <summary>
		/// Adds the things to gather to a generated island's objects; returns how many (land, sea). planned: the objects
		/// there before the buildings were put (null: none known) - every other object is kept clear of by its footprint,
		/// not only its middle (CA20: a palm grew inside a shack, a sea find lay in a sunken plane).
		/// </summary>
		public static KeyValuePair<int, int> Apply(IslandFile f, IslandGenSettings s, int seed, ICollection<IslandObject> planned = null)
		{
			if (s.Gather <= 0f && s.Shallows <= 0f) return new KeyValuePair<int, int>(0, 0);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1), sea = f.WaterLevel;
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			// (taken spots: a coarse grid of what the generator placed, so nothing lands on a tree, a rock or a building)
			var taken = new HashSet<long>();
			Func<float, float, long> cell = (x, z) => ((long)Mathf.FloorToInt(x) << 32) ^ (uint)Mathf.FloorToInt(z);
			foreach (IslandObject o in f.Objects) taken.Add(cell(o.Position.x, o.Position.z));
			if (planned != null)
				foreach (IslandObject o in f.Objects)
					if (!planned.Contains(o)) TakeFootprint(o, taken, cell);
			Func<float, float, float, bool> free = (x, z, r) =>
			{
				int ir = Mathf.CeilToInt(r);
				for (int dx = -ir; dx <= ir; dx++) for (int dz = -ir; dz <= ir; dz++) if (dx * dx + dz * dz <= r * r && taken.Contains(cell(x + dx, z + dz))) return false;
				return true;
			};
			// The land and the shallows, counted on a 2 m grid
			float landArea = 0f, shallowArea = 0f, deep = Mathf.Clamp(s.ShallowsDepth, MinDepth, MaxDepth);
			for (float x = 0; x < f.TerrainSize.x; x += 2f)
				for (float z = 0; z < f.TerrainSize.z; z += 2f)
				{
					float h = ground(x, z);
					if (h > sea + 0.3f) landArea += 4f; else if (h < sea - 0.6f && h > sea - deep) shallowArea += 4f;
				}
			var rnd = new System.Random(seed ^ 0x6a7e);
			int land = 0, wet = 0;
			KeyValuePair<string, bool>[] all;
			if (s.Gather > 0f && Kinds.TryGetValue(s.Style, out all))
			{
				// (only the kinds switched on; none on = no land things, maybe beehives)
				KeyValuePair<string, bool>[] kinds = all.Where(k => IsOn(s.GatherOff, KeyOf(k.Key))).ToArray();
				int want = Mathf.Min(MaxEach, Mathf.RoundToInt(landArea / 1000f * LandPer1000 * s.Gather));
				for (int k = 0; kinds.Length > 0 && k < want * 30 && land < want; k++)
				{
					float x = (float)rnd.NextDouble() * f.TerrainSize.x, z = (float)rnd.NextDouble() * f.TerrainSize.z, h = ground(x, z);
					if (h < sea + 0.4f) continue;
					// (not on steep ground: a fruit bush or a palm on a cliff face)
					if (Mathf.Abs(ground(x + 1f, z) - h) > 0.8f || Mathf.Abs(ground(x, z + 1f) - h) > 0.8f) continue;
					var kind = kinds[rnd.Next(kinds.Length)];
					if (!free(x, z, kind.Value ? 4f : 2f)) continue;
					// (snowy islands' pines to cut wear snow - LM11; Raft's own pine if the snowy one couldn't be made)
					string name = kind.Key == PlaceableCatalog.SnowyPine && PlaceableCatalog.Get(kind.Key) == null ? "Pickup_Landmark_Tree_Pine" : kind.Key;
					f.Objects.Add(new IslandObject { Name = name, Position = new Vector3(x, h, z), Scale = ScaleOf(name), Unscaled = PlaceableCatalog.Get(name) == null, EulerRotation = new Vector3(0f, (float)(rnd.NextDouble() * 360.0), 0f) });
					taken.Add(cell(x, z));
					land++;
				}
				// Wild beehives (honey: Raft has none wild - a container of honeycomb in Raft's beehive, refilling), where
				// flowers grow: tropical, forest and volcanic islands, about one per 3000 m2 of land at the top, at most 3
				int hives = !HivesFor(s.Style) || !IsOn(s.GatherOff, "hive") ? 0 : Mathf.Min(3, Mathf.FloorToInt(landArea / 3000f * s.Gather));
				for (int k = 0, made = 0; k < 400 && made < hives; k++)
				{
					float x = (float)rnd.NextDouble() * f.TerrainSize.x, z = (float)rnd.NextDouble() * f.TerrainSize.z, h = ground(x, z);
					if (h < sea + 1f || Mathf.Abs(ground(x + 1f, z) - h) > 0.5f || Mathf.Abs(ground(x, z + 1f) - h) > 0.5f || !free(x, z, 3f)) continue;
					f.Objects.Add(new IslandObject { Name = ContentCatalog.WildHive, Position = new Vector3(x, h, z), Scale = ScaleOf(ContentCatalog.WildHive), Unscaled = PlaceableCatalog.Get(ContentCatalog.WildHive) == null, EulerRotation = new Vector3(0f, (float)(rnd.NextDouble() * 360.0), 0f),
						Props = new Dictionary<string, string> { { ObjectProps.LootItems, ContentCatalog.WildHiveLoot }, { ObjectProps.NoteTitle, "Wild beehive" } } });
					taken.Add(cell(x, z));
					made++; land++;
				}
			}
			KeyValuePair<string, int>[] finds = SeaFinds.Where(w => IsOn(s.GatherOff, KeyOf(w.Key))).ToArray();
			if (s.Shallows > 0f && finds.Length > 0)
			{
				int total = finds.Sum(w => w.Value);
				int want = Mathf.Min(MaxEach, Mathf.RoundToInt(shallowArea / 1000f * SeaPer1000 * s.Shallows));
				for (int k = 0; k < want * 30 && wet < want; k++)
				{
					float x = (float)rnd.NextDouble() * f.TerrainSize.x, z = (float)rnd.NextDouble() * f.TerrainSize.z, h = ground(x, z);
					if (h > sea - 0.6f || h < sea - deep) continue;
					if (!free(x, z, 1.5f)) continue;
					int pick = rnd.Next(total);
					string name = finds[0].Key;
					foreach (var w in finds) { if (pick < w.Value) { name = w.Key; break; } pick -= w.Value; }
					f.Objects.Add(new IslandObject { Name = name, Position = new Vector3(x, h, z), Scale = ScaleOf(name), Unscaled = PlaceableCatalog.Get(name) == null, EulerRotation = new Vector3(0f, (float)(rnd.NextDouble() * 360.0), 0f) });
					taken.Add(cell(x, z));
					wet++;
				}
			}
			return new KeyValuePair<int, int>(land, wet);
		}
	}
}
