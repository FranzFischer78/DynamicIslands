using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>One kind of object on the land of Raft's islands of a style (measured by CIMeasureLand).</summary>
	public class LandThing
	{
		public string Name, Category;
		public int Count;
		/// <summary>Objects per 1000 m² of land, per habitat bin (RaftLand.BinOf).</summary>
		public float[] Density = new float[RaftLand.BinCount];
		/// <summary>Where they stand: height above the sea, distance inland from the coast, slope (p10 / p50 / p90).</summary>
		public float[] Height = new float[3], Inland = new float[3], Slope = new float[3];
		/// <summary>Share standing on sand, grass (snow, ground), rock.</summary>
		public float OnSand, OnGrass, OnRock;
		/// <summary>Size (m) and how far above the ground their pivot is (negative: sunk), p50; the nearest other object of the same kind (m, p50).</summary>
		public float Size, Above, NearestSame;
	}

	/// <summary>Raft's islands of a style, pooled: the land's area per habitat bin and every kind on it.</summary>
	public class LandStyle
	{
		public string Name;
		public float[] Area = new float[RaftLand.BinCount];
		public List<LandThing> Things = new List<LandThing>();
		public IEnumerable<LandThing> Of(string category) { return Things.Where(t => t.Category == category); }
		/// <summary>Objects of a category per 1000 m² in a bin (0 where Raft has none).</summary>
		public float DensityOf(string category, int bin) { return Of(category).Sum(t => t.Density[bin]); }
		public int CountOf(string category) { return Of(category).Sum(t => t.Count); }
	}

	/// <summary>
	/// Where Raft puts things on the land of its islands, by habitat: how far inland from the coast, how steep, how
	/// high above the sea (raft_land.txt, measured by the dev command CIMeasureLand, shipped with the mod). The
	/// generator places land objects where Raft has them and picks kinds as Raft mixes them there.
	/// </summary>
	public static class RaftLand
	{
		public const string FileName = "raft_land.txt";

		/// <summary>Habitat bins: metres inland from the coast, slope in degrees, metres above the sea.</summary>
		public static readonly float[] InlandBins = { 0f, 3f, 8f, 20f, 50f, 1e6f };
		public static readonly float[] SlopeBins = { 0f, 10f, 20f, 30f, 40f, 91f };
		public static readonly float[] HeightBins = { 0f, 1.5f, 4f, 12f, 30f, 1e6f };
		public const int Bins1 = 5, BinCount = Bins1 * Bins1 * Bins1;

		static int Band(float[] edges, float v) { for (int i = edges.Length - 2; i >= 0; i--) if (v >= edges[i]) return i; return 0; }

		public static int BinOf(float inland, float slope, float height) { return (Band(InlandBins, inland) * Bins1 + Band(SlopeBins, slope)) * Bins1 + Band(HeightBins, Mathf.Max(0f, height)); }

		public static void Unbin(int bin, out int inland, out int slope, out int height) { height = bin % Bins1; slope = bin / Bins1 % Bins1; inland = bin / (Bins1 * Bins1); }

		public static string DescribeBin(int bin)
		{
			int i, s, h;
			Unbin(bin, out i, out s, out h);
			Func<float[], int, string> r = (e, k) => e[k].ToString("0.#", CultureInfo.InvariantCulture) + (e[k + 1] > 1e5f ? "+" : "-" + e[k + 1].ToString("0.#", CultureInfo.InvariantCulture));
			return "inland " + r(InlandBins, i) + " m, slope " + r(SlopeBins, s) + "°, height " + r(HeightBins, h) + " m";
		}

		/// <summary>The islands that count (as RaftUnderwater): Raft's natural islands.</summary>
		static readonly System.Text.RegularExpressions.Regex Natural = new System.Text.RegularExpressions.Regex(@"^(Big island .+|Big|Small island \d+|Pilot|Boat|Balboa .+|Caravan .+|Temperance Small \d+)$");

		/// <summary>Raft's islands with less land than this (m²) are its small ones: its "Small island"s, Pilot and Boat have up
		/// to about 2100 m², Caravan's small ones about 5000; its big ones 12 000 and more.</summary>
		public const float SmallLand = 6000f;
		/// <summary>The fewest objects a size's pool needs to go by (else all of the style's islands are used).</summary>
		const int MinPooled = 150;

		/// <summary>Per style: all of Raft's natural islands of it, only its small ones, only its big ones.</summary>
		static LandStyle[] styles, smallStyles, bigStyles;
		/// <summary>Every measured island (by label): m² of land, then trees, bushes, rocks and harvestables on it.</summary>
		static Dictionary<string, float[]> islands;

		public static void Reload() { styles = smallStyles = bigStyles = null; islands = null; }

		/// <summary>
		/// An island's own trees, bushes, rocks and harvestables per 1000 m² of its land (on the land only: what lies under
		/// water around it isn't counted), as measured on it; null if it wasn't.
		/// </summary>
		public static float[] IslandDensities(string label)
		{
			if (styles == null) Load();
			float[] n;
			if (islands == null || label == null || !islands.TryGetValue(label, out n) || n[0] < 1f) return null;
			return new[] { n[1] * 1000f / n[0], n[2] * 1000f / n[0], n[3] * 1000f / n[0], n[4] * 1000f / n[0] };
		}

		public static bool Loaded { get { if (styles == null) Load(); return styles.Any(s => s != null && s.Things.Count > 0); } }

		/// <summary>Raft's islands of a style (Volcanic: the desert's); never null (empty without the file).</summary>
		public static LandStyle For(int style)
		{
			if (styles == null) Load();
			LandStyle s = styles[Mathf.Clamp(style, 0, styles.Length - 1)];
			return s ?? new LandStyle { Name = TerrainPainter.StyleName(style) };
		}

		/// <summary>
		/// Raft's small islands of a style, or its big ones: a small island of Raft's is far thicker with things (five times
		/// the bushes, three times the trees for its land) and everything on it stands nearer the shore and lower down. All of
		/// the style's islands where Raft has too few of that size to go by.
		/// </summary>
		/// <summary>Whether Raft has small islands of the style to go by (only its tropical ones and Caravan's islets).</summary>
		public static bool HasSmall(int style)
		{
			if (styles == null) Load();
			LandStyle s = smallStyles[Mathf.Clamp(style, 0, smallStyles.Length - 1)];
			return s != null && s.Things.Sum(t => t.Count) >= MinPooled;
		}

		public static LandStyle For(int style, bool small)
		{
			if (styles == null) Load();
			LandStyle[] pool = small ? smallStyles : bigStyles;
			LandStyle s = pool[Mathf.Clamp(style, 0, pool.Length - 1)];
			return s != null && s.Things.Sum(t => t.Count) >= MinPooled ? s : For(style);
		}

		static float Num(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f; }
		static float[] Triple(string s) { string[] p = s.Split('/'); return p.Length == 3 ? new[] { Num(p[0]), Num(p[1]), Num(p[2]) } : new float[3]; }

		class Acc
		{
			public string Name, Category;
			public int Count;
			public float[] Counts = new float[BinCount];
			public double W, H0, H1, H2, I0, I1, I2, S0, S1, S2, Sand, Grass, Rock, Size, Above, AboveW, Near;
		}

		static void Load()
		{
			int count = TerrainPainter.Styles.Length;
			styles = new LandStyle[count]; smallStyles = new LandStyle[count]; bigStyles = new LandStyle[count];
			byte[] bytes = RaftIslands.ModFile(FileName);
			if (bytes == null) { Debug.LogWarning("[CUSTOM ISLANDS] " + FileName + " is missing: land objects are placed by the generator's own rules"); return; }
			var islandStyle = new Dictionary<string, int>();
			var islandSmall = new Dictionary<string, bool>();
			islands = new Dictionary<string, float[]>();
			string[] kinds = { "trees", "bushes", "rocks", "harvest" };
			// (pools: 0 = all of a style's islands, 1 = its small ones, 2 = its big ones)
			var areas = new Dictionary<int, Dictionary<string, float[]>>[3];
			var acc = new Dictionary<int, Dictionary<string, Acc>>[3];
			for (int p = 0; p < 3; p++) { areas[p] = new Dictionary<int, Dictionary<string, float[]>>(); acc[p] = new Dictionary<int, Dictionary<string, Acc>>(); }
			try
			{
				foreach (string raw in Encoding.UTF8.GetString(bytes).Split('\n'))
				{
					string line = raw.TrimEnd('\r');
					if (line.Length == 0 || line.StartsWith("#")) continue;
					string[] f = line.Split('\t');
					// (every island's own numbers, for making one like it)
					if (f[0] == "land" && f.Length >= 5)
						islands[f[1]] = new[] { f[4].Split(' ').Where(p => p.IndexOf(':') > 0).Sum(p => Num(p.Substring(p.IndexOf(':') + 1))), 0f, 0f, 0f, 0f };
					else if (f[0] == "lobj" && f.Length >= 5 && islands.ContainsKey(f[1]) && Array.IndexOf(kinds, f[3]) >= 0 && !UnderWaterOnly(f[2]))
						islands[f[1]][1 + Array.IndexOf(kinds, f[3])] += Num(f[4]);
					// (Raft's natural islands only: towns, the radio tower and the story islands' insides aren't how islands grow)
					if (f.Length > 1 && !Natural.IsMatch(f[1])) continue;
					if (f[0] == "land" && f.Length >= 5)
					{
						int st = TerrainPainter.StyleIndex(f[2]);
						islandStyle[f[1]] = st;
						var a = new float[BinCount];
						foreach (string pair in f[4].Split(' ')) { int c = pair.IndexOf(':'); if (c > 0) { int b; if (int.TryParse(pair.Substring(0, c), out b) && b >= 0 && b < BinCount) a[b] = Num(pair.Substring(c + 1)); } }
						bool small = a.Sum() < SmallLand;
						islandSmall[f[1]] = small;
						foreach (int p in new[] { 0, small ? 1 : 2 })
						{
							if (!areas[p].ContainsKey(st)) areas[p][st] = new Dictionary<string, float[]>();
							// (islands sharing one terrain - Raft's small islands share a few - count once)
							areas[p][st][f[4]] = a;
						}
					}
					else if (f[0] == "lobj" && f.Length >= 14 && islandStyle.ContainsKey(f[1]))
					{
						int st = islandStyle[f[1]];
						string name = f[2], cat = f[3];
						// (Raft's sea finds - ores, scrap, clams, silver algae - are the sea's: a few lay at the waterline and were
						// counted as land, and the generator put ore and clams on land; the user wants them under water, 2026-10-03)
						if (UnderWaterOnly(name)) continue;
						int n = (int)Num(f[4]);
						if (n <= 0) continue;
						var counts = new float[BinCount];
						foreach (string pair in f[5].Split(' ')) { int c = pair.IndexOf(':'); if (c > 0) { int b; if (int.TryParse(pair.Substring(0, c), out b) && b >= 0 && b < BinCount) counts[b] += Num(pair.Substring(c + 1)); } }
						float[] h = Triple(f[6]), i = Triple(f[7]), s = Triple(f[8]), t = f[9].Split('/').Select(Num).ToArray();
						foreach (int p in new[] { 0, islandSmall[f[1]] ? 1 : 2 })
						{
							if (!acc[p].ContainsKey(st)) acc[p][st] = new Dictionary<string, Acc>();
							Acc a;
							if (!acc[p][st].TryGetValue(name, out a)) acc[p][st][name] = a = new Acc { Name = name, Category = cat };
							for (int b = 0; b < BinCount; b++) a.Counts[b] += counts[b];
							a.Count += n; a.W += n;
							a.H0 += h[0] * n; a.H1 += h[1] * n; a.H2 += h[2] * n;
							a.I0 += i[0] * n; a.I1 += i[1] * n; a.I2 += i[2] * n;
							a.S0 += s[0] * n; a.S1 += s[1] * n; a.S2 += s[2] * n;
							if (t.Length >= 3) { a.Sand += t[0] * n; a.Grass += t[1] * n; a.Rock += t[2] * n; }
							a.Size += Num(f[10]) * n; a.Near += Num(f[12]) * n;
							if (Plausible(Num(f[11]), Num(f[10]))) { a.Above += Num(f[11]) * n; a.AboveW += n; }
						}
					}
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + FileName + ": " + e.Message); }
			styles = Build(acc[0], areas[0], count);
			smallStyles = Build(acc[1], areas[1], count);
			bigStyles = Build(acc[2], areas[2], count);
			Debug.Log("[CUSTOM ISLANDS] On the land of Raft's islands: " + string.Join(", ", Enumerable.Range(0, styles.Length).Where(i => styles[i] != null)
				.Select(i => TerrainPainter.StyleName(i) + " " + styles[i].Things.Count + " kinds (small islands " + (smallStyles[i] != null ? smallStyles[i].Things.Sum(t => t.Count) : 0) +
					" things, big " + (bigStyles[i] != null ? bigStyles[i].Things.Sum(t => t.Count) : 0) + ")").ToArray()));
		}

		/// <summary>Raft's finds that belong under water only: metal and copper ore, scrap, giant clams, silver algae.</summary>
		public static bool UnderWaterOnly(string name)
		{
			return System.Text.RegularExpressions.Regex.IsMatch(name ?? "", @"^Pickup_Landmark_(Iron \d+|Copper \d+|Scrap \d+_OceanBottom|GiantClam|SilverAlgae)$");
		}

		/// <summary>
		/// Whether an island's "above the ground" for a kind is how deep it stands in its ground: no deeper or higher than the
		/// thing is big. Else the measured ground was something else's - the top of a big rock over it, a cliff in the sample's
		/// cell - and is left out: one island's -22 m for a coral 2.6 m big sank every coral of the style out of sight (2026-10-02).
		/// </summary>
		public static bool Plausible(float above, float size)
		{
			return Mathf.Abs(above) <= Mathf.Max(size, 0.5f);
		}

		/// <summary>A pool of islands per style: the land's area per bin and every kind with its densities and where it stands.</summary>
		static LandStyle[] Build(Dictionary<int, Dictionary<string, Acc>> acc, Dictionary<int, Dictionary<string, float[]>> areas, int count)
		{
			var result = new LandStyle[count];
			foreach (var kv in acc)
			{
				var style = new LandStyle { Name = TerrainPainter.StyleName(kv.Key) };
				if (areas.ContainsKey(kv.Key)) foreach (float[] a in areas[kv.Key].Values) for (int b = 0; b < BinCount; b++) style.Area[b] += a[b];
				foreach (Acc a in kv.Value.Values)
				{
					double w = Math.Max(1, a.W);
					var t = new LandThing
					{
						Name = a.Name, Category = a.Category, Count = a.Count,
						Height = new[] { (float)(a.H0 / w), (float)(a.H1 / w), (float)(a.H2 / w) },
						Inland = new[] { (float)(a.I0 / w), (float)(a.I1 / w), (float)(a.I2 / w) },
						Slope = new[] { (float)(a.S0 / w), (float)(a.S1 / w), (float)(a.S2 / w) },
						OnSand = (float)(a.Sand / w), OnGrass = (float)(a.Grass / w), OnRock = (float)(a.Rock / w),
						Size = (float)(a.Size / w), Above = a.AboveW > 0 ? (float)(a.Above / a.AboveW) : 0f, NearestSame = (float)(a.Near / w),
					};
					for (int b = 0; b < BinCount; b++) t.Density[b] = style.Area[b] > 1f ? a.Counts[b] * 1000f / style.Area[b] : 0f;
					style.Things.Add(t);
				}
				// (a stable order: the generator's islands must not depend on the file's order)
				style.Things.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
				result[kv.Key] = style;
			}
			if (result[TerrainPainter.Volcanic] == null && result[TerrainPainter.Desert] != null) result[TerrainPainter.Volcanic] = result[TerrainPainter.Desert];
			return result;
		}
	}
}
