using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>One kind of object under water around Raft's islands of a style, as measured.</summary>
	public class SeaThing
	{
		public string Name, Category;
		/// <summary>Objects per m² of under-water ground in each depth band (RaftUnderwater.Bands).</summary>
		public float[] Density = new float[RaftUnderwater.BandCount];
		/// <summary>Depth of the object (m below the sea): 10th percentile, median, 90th percentile.</summary>
		public float DepthLow, Depth, DepthHigh;
		/// <summary>Distance from the coast (m): median and 90th percentile.</summary>
		public float Coast, CoastHigh;
		/// <summary>Median slope of the ground under it (degrees), its size (m) and how far above the ground it sits (negative: sunk in).</summary>
		public float Slope, Size, Above;
		public int Count;
		/// <summary>LM7: one of the other kinds Raft's islands have under water (RaftUnderwater.ExtraCategoryOf) - placed only
		/// for "Randomize existing" (IslandGenSettings.RaftSeaKinds), so the other generated islands stay as their seeds made them.</summary>
		public bool Extra;
	}

	/// <summary>What lies under water around Raft's islands of one style.</summary>
	public class SeaStyle
	{
		public string Name;
		public List<SeaThing> Things = new List<SeaThing>();
		/// <summary>Under-water ground measured (m²) per depth band.</summary>
		public float[] Area = new float[RaftUnderwater.BandCount];
		/// <summary>The core kinds of a category (without the extra ones, LM7).</summary>
		public IEnumerable<SeaThing> Of(string category) { return Things.Where(t => t.Category == category && !t.Extra); }
		/// <summary>A category's kinds, the extra ones too when <paramref name="extra"/> is set.</summary>
		public IEnumerable<SeaThing> Of(string category, bool extra) { return Things.Where(t => t.Category == category && (extra || !t.Extra)); }
	}

	/// <summary>
	/// Raft's islands under water, measured by the dev command CIMeasureUnderwater (raft_underwater.txt, shipped with the
	/// mod): every object by name with its depth, distance from the coast, slope, size and depth band, and how much
	/// ground each depth band has. The generator dresses the sea around its islands from these numbers, so a generated
	/// island looks like Raft's own under water: corals and sea vines on the shelf, stones, clay, sand, scrap and giant
	/// clams to collect, metal and copper ore on the steep slopes further down, boulders near the shore and huge rock
	/// formations on the drop-off. The islands are pooled by style (tropical: Raft's ordinary islands; forest: Balboa;
	/// desert: Caravan Island; snowy: Temperance's small islands; volcanic has no Raft island and uses the desert's).
	/// </summary>
	public static class RaftUnderwater
	{
		public const string FileName = "raft_underwater.txt";
		/// <summary>Depth bands of the ground (m below the sea): 0-2, 2-5, 5-10, 10-20, 20-40, 40-80, 80+.</summary>
		public static readonly float[] Bands = { 0f, 2f, 5f, 10f, 20f, 40f, 80f, 10000f };
		public const int BandCount = 7;

		public static int BandOf(float depth)
		{
			for (int i = BandCount - 1; i >= 0; i--) if (depth >= Bands[i]) return i;
			return 0;
		}

		/// <summary>The islands that count: Raft's natural islands (no towns or built places).</summary>
		static readonly Regex Natural = new Regex(@"^(Big island .+|Big|Small island \d+|Pilot|Boat|Balboa .+|Caravan .+|Temperance Small \d+)$");

		/// <summary>Which of the generator's under-water kinds an object is (null: not one the generator places).</summary>
		public static string CategoryOf(string name)
		{
			if (Regex.IsMatch(name, @"^(Coral\d+|LeafCoral_\d+|TableCoral_\d+|CauliCoral|CylinderCoral_\d+|SpineCoral_\d+|Pillar_\d+|SeaVine3|SeaVine3_klump|[Ss]eavine_tongue)$")) return IslandGenerator.CatWater;
			if (Regex.IsMatch(name, @"^(BigBoulder\d+_Low|SmallBoulder\d+|BigRock_Low\d+_Sand|BigRock_\d+|SmallRock_\d+|TP_BigRock0\d|TP_SmallRock0\d|BigSharpRock_\d+|CaravanIsland_SmallRock_\d+)$")) return IslandGenerator.CatSeaRocks;
			if (Regex.IsMatch(name, @"^Pickup_Landmark_(Rock \d+|Clay \d+|Sand|Sand_Caravan|Scrap \d+_OceanBottom|Iron \d+|Copper \d+|GiantClam|SilverAlgae)$")) return IslandGenerator.CatSeaFinds;
			if (Regex.IsMatch(name, @"^(Reef_Barrel\d+|Reef_Container|Reef_Buoy|FL_Plank\d*|FL_Plywood|FL_Pillar|FL_Crate)$")) return IslandGenerator.CatSunken;
			return null;
		}

		/// <summary>
		/// LM7: the other kinds Raft's natural islands have under water that the object catalog has (core or in its index,
		/// raft_underwater.txt's last column) - tiny corals, Caravan Island's big round rocks, sunken logs, an abandoned
		/// raft's roof and cloth, Balboa's wheelbarrow. Only "Randomize existing" places them (IslandGenSettings.RaftSeaKinds).
		/// Left out: the reef huts' walls and pillars (pieces of a building, not things that lie about), bamboo (it grows on
		/// the shore), the air pockets and the dive puzzle (Caravan Town's diving), and what the catalog hasn't (sea vine
		/// clumps, crates).
		/// </summary>
		public static string ExtraCategoryOf(string name)
		{
			if (Regex.IsMatch(name, @"^TinyCoral$")) return IslandGenerator.CatWater;
			if (Regex.IsMatch(name, @"^CaravanIsland_BigRoundRock_\d+$")) return IslandGenerator.CatSeaRocks;
			if (Regex.IsMatch(name, @"^(TreeLog_\d+|Log|FL_Roof\d|FL_Cloth|Reef_Wheelbarrow)$")) return IslandGenerator.CatSunken;
			return null;
		}

		static SeaStyle[] styles;
		/// <summary>Per island (label): the median ground depth every 5 m out from its coast.</summary>
		static readonly Dictionary<string, SortedDictionary<float, float>> profiles = new Dictionary<string, SortedDictionary<float, float>>();

		/// <summary>Reads the file again (after a new measurement).</summary>
		public static void Reload() { styles = null; profiles.Clear(); }

		/// <summary>How far out from an island's coast (m) its ground first gets this deep (-1 if it never does, or it wasn't measured).</summary>
		public static float DistanceToDepth(string label, float depth)
		{
			if (styles == null) Load();
			SortedDictionary<float, float> p;
			if (label == null || !profiles.TryGetValue(label, out p)) return -1f;
			float lastD = 0f, lastDepth = 0f;
			foreach (var kv in p)
			{
				if (kv.Value >= depth) return kv.Value - lastDepth < 0.01f ? kv.Key : Mathf.Lerp(lastD, kv.Key, (depth - lastDepth) / (kv.Value - lastDepth));
				lastD = kv.Key; lastDepth = kv.Value;
			}
			return -1f;
		}

		/// <summary>
		/// The generator's Drop-off (0 = a long, gentle slope, 1 = a sheer wall) that matches a Raft island: how far its
		/// ground takes from 12 m to 100 m deep (0.5 if it wasn't measured).
		/// </summary>
		public static float DropOffOf(RaftIsland island)
		{
			float a = DistanceToDepth(island.Label, 12f), b = DistanceToDepth(island.Label, 100f);
			if (a < 0f || b <= a) return 0.5f;
			// (the drop-off falls 60 % of the way to the floor in about 60 % of its width; bigger islands have longer slopes)
			float width = (b - a) / 0.6f / Mathf.Lerp(0.75f, 1.15f, Mathf.InverseLerp(10f, 120f, island.Radius));
			return Mathf.Clamp01(Mathf.InverseLerp(170f, 35f, width));
		}

		/// <summary>True if the measurements were found.</summary>
		public static bool Loaded { get { if (styles == null) Load(); return styles.Any(s => s != null && s.Things.Count > 0); } }

		/// <summary>What lies under water around Raft's islands of a style (TerrainPainter.Styles); never null (empty without the file).</summary>
		public static SeaStyle For(int style)
		{
			if (styles == null) Load();
			SeaStyle s = styles[Mathf.Clamp(style, 0, styles.Length - 1)];
			return s ?? new SeaStyle { Name = TerrainPainter.StyleName(style) };
		}

		class Acc
		{
			public string Name;
			public float[] Counts = new float[BandCount];
			public double W, DepthLow, Depth, DepthHigh, Coast, CoastHigh, Slope, Size, Above, AboveW;
		}

		static float Num(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f; }

		static float[] Triple(string s)
		{
			string[] p = s.Split('/');
			return p.Length == 3 ? new[] { Num(p[0]), Num(p[1]), Num(p[2]) } : new[] { 0f, 0f, 0f };
		}

		static void Load()
		{
			styles = new SeaStyle[TerrainPainter.Styles.Length];
			byte[] bytes = RaftIslands.ModFile(FileName);
			if (bytes == null) { Debug.LogWarning("[CUSTOM ISLANDS] " + FileName + " is missing: generated islands get no objects under water"); return; }
			var islandStyle = new Dictionary<string, int>();
			var areas = new Dictionary<int, Dictionary<string, float[]>>(); // style -> distinct area rows (islands sharing one terrain count once)
			var acc = new Dictionary<int, Dictionary<string, Acc>>();
			try
			{
				foreach (string raw in Encoding.UTF8.GetString(bytes).Split('\n'))
				{
					string line = raw.TrimEnd('\r');
					if (line.Length == 0 || line.StartsWith("#")) continue;
					string[] f = line.Split('\t');
					if (f[0] == "profile" && f.Length >= 3)
					{
						var p = new SortedDictionary<float, float>();
						foreach (string pair in f[2].Split(' '))
						{
							int colon = pair.IndexOf(':');
							if (colon > 0) p[Num(pair.Substring(0, colon))] = Num(pair.Substring(colon + 1));
						}
						profiles[f[1]] = p;
					}
					else if (f[0] == "area" && f.Length >= 4 && Natural.IsMatch(f[1]))
					{
						int st = TerrainPainter.StyleIndex(f[2]);
						islandStyle[f[1]] = st;
						float[] a = f[3].Split(' ').Select(Num).ToArray();
						if (a.Length != BandCount) continue;
						if (!areas.ContainsKey(st)) areas[st] = new Dictionary<string, float[]>();
						areas[st][f[3]] = a;
					}
					else if (f[0] == "obj" && f.Length >= 11 && islandStyle.ContainsKey(f[1]))
					{
						string name = f[2];
						if (CategoryOf(name) == null && ExtraCategoryOf(name) == null) continue;
						int st = islandStyle[f[1]];
						float n = Num(f[3]);
						float[] depth = Triple(f[4]), coast = Triple(f[5]);
						float[] perBand = f[9].Split(' ').Select(Num).ToArray();
						if (n <= 0f || perBand.Length != BandCount) continue;
						if (!acc.ContainsKey(st)) acc[st] = new Dictionary<string, Acc>();
						Acc a;
						if (!acc[st].TryGetValue(name, out a)) acc[st][name] = a = new Acc { Name = name };
						for (int b = 0; b < BandCount; b++) a.Counts[b] += perBand[b];
						a.W += n; a.DepthLow += depth[0] * n; a.Depth += depth[1] * n; a.DepthHigh += depth[2] * n;
						a.Coast += coast[1] * n; a.CoastHigh += coast[2] * n;
						a.Slope += Num(f[6]) * n; a.Size += Num(f[7]) * n;
						if (RaftLand.Plausible(Num(f[8]), Num(f[7]))) { a.Above += Num(f[8]) * n; a.AboveW += n; } // (see RaftLand.Plausible)
					}
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + FileName + ": " + e.Message); }

			foreach (var kv in acc)
			{
				var style = new SeaStyle { Name = TerrainPainter.StyleName(kv.Key) };
				foreach (float[] a in areas.ContainsKey(kv.Key) ? areas[kv.Key].Values : Enumerable.Empty<float[]>())
					for (int b = 0; b < BandCount; b++) style.Area[b] += a[b];
				foreach (Acc a in kv.Value.Values)
				{
					var t = new SeaThing
					{
						Name = a.Name, Category = CategoryOf(a.Name) ?? ExtraCategoryOf(a.Name), Extra = CategoryOf(a.Name) == null, Count = (int)a.W,
						DepthLow = (float)(a.DepthLow / a.W), Depth = (float)(a.Depth / a.W), DepthHigh = (float)(a.DepthHigh / a.W),
						Coast = (float)(a.Coast / a.W), CoastHigh = (float)(a.CoastHigh / a.W), Slope = (float)(a.Slope / a.W), Size = (float)(a.Size / a.W), Above = a.AboveW > 0 ? (float)(a.Above / a.AboveW) : 0f,
					};
					for (int b = 0; b < BandCount; b++) t.Density[b] = style.Area[b] > 1f ? a.Counts[b] / style.Area[b] : 0f;
					style.Things.Add(t);
				}
				// (a stable order: the generator's islands must not depend on the order of the file's lines)
				style.Things.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
				styles[kv.Key] = style;
			}
			// Volcanic islands aren't in Raft: dark desert rock under water, like Caravan Island's
			if (styles[TerrainPainter.Volcanic] == null && styles[TerrainPainter.Desert] != null) styles[TerrainPainter.Volcanic] = styles[TerrainPainter.Desert];
			Debug.Log("[CUSTOM ISLANDS] Under water around Raft's islands: " + string.Join(", ", Enumerable.Range(0, styles.Length).Where(i => styles[i] != null)
				.Select(i => TerrainPainter.StyleName(i) + " " + styles[i].Things.Count + " kinds").ToArray()));
		}

		/// <summary>Objects per 1000 m² of under-water ground from 0 to 40 m deep, by kind, for a style (the help texts and tests).</summary>
		public static float DensityOf(int style, string category) { return DensityOf(style, category, false); }

		/// <summary>(extra: the extra kinds counted too, LM7)</summary>
		public static float DensityOf(int style, string category, bool extra)
		{
			SeaStyle s = For(style);
			float area = 0f, count = 0f;
			for (int b = 0; b < 5; b++) { area += s.Area[b]; count += s.Of(category, extra).Sum(t => t.Density[b]) * s.Area[b]; }
			return area > 0f ? count * 1000f / area : 0f;
		}
	}
}
