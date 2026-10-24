using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// One of Raft's own islands as the dev command CIMeasureIslands measured it: the size of its land above the sea,
	/// its highest point, the shallow water around it, peaks, slopes, coast, ground style, and how many objects of each
	/// kind it has. The generator's size and height presets and its "Randomize existing" tab use these numbers.
	/// </summary>
	public class RaftIsland
	{
		public string Scene = "", Label = "", Kind = "", Style = "Tropical";
		/// <summary>Land above the sea: along its long axis, across it (m), and the long axis' direction (degrees from +x towards +z).</summary>
		public float Length, Width, Angle;
		/// <summary>Land above the sea (m²) and the radius of a round island that big (m).</summary>
		public float Area, Radius;
		/// <summary>Highest ground above the sea (m).</summary>
		public float Top;
		/// <summary>Radius of a round area as big as the land plus the shallow water around it (down to 8 m deep).</summary>
		public float Shelf;
		/// <summary>Mean slope of the land (degrees), share of the coast that is steep, share of the land under 3 m.</summary>
		public float Slope, Cliffs, Beach;
		/// <summary>1 = a round coast; lower = more ragged (bays, points, islets).</summary>
		public float Compact = 1f;
		public int Peaks = 1;
		/// <summary>Objects per kind (RaftIslands.Kinds).</summary>
		public Dictionary<string, int> Counts = new Dictionary<string, int>();

		public int Count(string kind) { int n; return Counts.TryGetValue(kind, out n) ? n : 0; }

		/// <summary>Objects of a kind per 1000 m² of land.</summary>
		public float Density(string kind) { return Area > 1f ? Count(kind) * 1000f / Area : 0f; }

		public int StyleIndex { get { return TerrainPainter.StyleIndex(Style); } }

		/// <summary>Where it comes from, for the list: "Island" for the pooled small and big ones, else the place's name.</summary>
		public string Group { get { return RaftIslands.GroupOf(Scene); } }
	}

	/// <summary>A Raft island's ground heights around its middle (m above the sea), sampled on a square grid.</summary>
	public class HeightField
	{
		public float Cell;
		public int Nx, Nz;
		/// <summary>[z, x], metres above the sea. The grid's middle is the land's middle.</summary>
		public float[,] H;

		public float Lowest { get { float m = float.MaxValue; foreach (float v in H) m = Mathf.Min(m, v); return m; } }

		/// <summary>Height at x, z metres from the land's middle (bilinear; the edge values outside the grid).</summary>
		public float At(float x, float z)
		{
			float fx = Mathf.Clamp(x / Cell + (Nx - 1) / 2f, 0f, Nx - 1.001f), fz = Mathf.Clamp(z / Cell + (Nz - 1) / 2f, 0f, Nz - 1.001f);
			int x0 = (int)fx, z0 = (int)fz;
			float tx = fx - x0, tz = fz - z0;
			return Mathf.Lerp(Mathf.Lerp(H[z0, x0], H[z0, x0 + 1], tx), Mathf.Lerp(H[z0 + 1, x0], H[z0 + 1, x0 + 1], tx), tz);
		}

		/// <summary>Half the grid's size (m): beyond this there is no data.</summary>
		public float HalfX { get { return (Nx - 1) / 2f * Cell; } }
		public float HalfZ { get { return (Nz - 1) / 2f * Cell; } }
	}

	/// <summary>
	/// Raft's own islands, measured (raft_islands.txt), with a picture of each (island_thumbs\) and their ground
	/// heights (island_heights\). The files are made by CIMeasureIslands in Mods\DynamicIslands and shipped with the mod;
	/// a copy in Mods\DynamicIslands (a newer measurement) is used first.
	/// </summary>
	public static class RaftIslands
	{
		public const string FileName = "raft_islands.txt", ThumbFolder = "island_thumbs", HeightFolder = "island_heights";
		public const int FileVersion = 1;

		/// <summary>The object kinds counted on each island.</summary>
		public const string Trees = "trees", Bushes = "bushes", Rocks = "rocks", Harvest = "harvest", Sea = "sea", Loot = "loot", Spawners = "spawners", Props = "props";
		public static readonly string[] Kinds = { Trees, Bushes, Rocks, Harvest, Sea, Loot, Spawners, Props };

		static List<RaftIsland> all;
		static readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
		static readonly Dictionary<string, HeightField> heights = new Dictionary<string, HeightField>();

		/// <summary>Every measured island with land, in Raft's build order (empty if the file is missing).</summary>
		public static List<RaftIsland> All { get { if (all == null) Load(); return all; } }

		/// <summary>Reads the files again (after a new measurement).</summary>
		public static void Reload() { all = null; thumbs.Clear(); heights.Clear(); }

		public static RaftIsland Get(string scene) { return All.FirstOrDefault(i => i.Scene == scene); }

		/// <summary>"34#Landmark_Small#1" -> "34_Landmark_Small_1": a file name for a scene.</summary>
		public static string SafeName(string scene) { return new string((scene ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray()); }

		/// <summary>"Small island 1", "Big island Twin peak", "Balboa"...: how the list names an island.</summary>
		public static string LabelOf(string scene)
		{
			string l = PlaceableCatalog.SceneLabel(scene);
			if (l.StartsWith("Small ")) return "Small island " + l.Substring(6);
			if (l.StartsWith("Big ")) return "Big island " + l.Substring(4);
			return l;
		}

		public static string GroupOf(string scene)
		{
			string l = PlaceableCatalog.SceneLabel(scene);
			return l.StartsWith("Small ") || l.StartsWith("Big ") ? "Islands" : l.Split(' ')[0];
		}

		/// <summary>
		/// A file of the mod: the copy in the .rmod; Mods\DynamicIslands\&lt;path&gt; only when the .rmod has none, or on a
		/// developer's PC with the file "dev_overrides" there (a new measurement being tried). ROADMAP AU38: the disk came
		/// first, so an unzipped .rmod's or an old measurement's copy won over every update - the mod's version and the
		/// blueprint list too.
		/// </summary>
		public static byte[] ModFile(string path)
		{
			bool devFirst = false;
			try { devFirst = File.Exists(Path.Combine(DynamicIslands.assetpath, DevOverrides)); } catch { }
			byte[] shipped = null;
			try
			{
				// (asked for directly, a missing file would log an error: look it up in the mod's file list first)
				var files = DynamicIslands.instance != null ? DynamicIslands.instance.modlistEntry.modinfo.modFiles : null;
				byte[] b;
				if (files != null && files.TryGetValue(path.Replace('\\', '/'), out b)) shipped = b;
			}
			catch { }
			if (shipped != null && !devFirst) return shipped;
			try
			{
				string disk = Path.Combine(DynamicIslands.assetpath, path);
				if (File.Exists(disk)) return File.ReadAllBytes(disk);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + path + ": " + e.Message); }
			return shipped;
		}

		/// <summary>A file in Mods\DynamicIslands that lets its copies of the mod's files win (developers trying a measurement).</summary>
		public const string DevOverrides = "dev_overrides";

		static void Load()
		{
			all = new List<RaftIsland>();
			byte[] bytes = ModFile(FileName);
			if (bytes == null) { Debug.LogWarning("[CUSTOM ISLANDS] " + FileName + " is missing: the generator's Raft presets use built-in numbers and \"Randomize existing\" is empty"); return; }
			try
			{
				foreach (string raw in Encoding.UTF8.GetString(bytes).Split('\n'))
				{
					string line = raw.TrimEnd('\r');
					if (line.Length == 0 || line.StartsWith("#")) continue;
					RaftIsland i = Parse(line);
					if (i != null && i.Area > 0f) all.Add(i);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + FileName + ": " + e.Message); }
		}

		static float F(Dictionary<string, string> d, string key, float fallback = 0f)
		{
			string s; float v;
			return d.TryGetValue(key, out s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
		}

		static RaftIsland Parse(string line)
		{
			var d = new Dictionary<string, string>();
			foreach (string part in line.Split('\t'))
			{
				int eq = part.IndexOf('=');
				if (eq > 0) d[part.Substring(0, eq)] = part.Substring(eq + 1);
			}
			string scene;
			if (!d.TryGetValue("scene", out scene)) return null;
			var i = new RaftIsland
			{
				Scene = scene, Label = d.ContainsKey("label") ? d["label"] : LabelOf(scene), Kind = d.ContainsKey("kind") ? d["kind"] : "", Style = d.ContainsKey("style") ? d["style"] : "Tropical",
				Length = F(d, "length"), Width = F(d, "width"), Angle = F(d, "angle"), Area = F(d, "area"), Radius = F(d, "radius"), Top = F(d, "top"), Shelf = F(d, "shelf"),
				Slope = F(d, "slope"), Cliffs = F(d, "cliffs"), Beach = F(d, "beach"), Compact = F(d, "compact", 1f), Peaks = Mathf.Max(1, Mathf.RoundToInt(F(d, "peaks", 1f))),
			};
			foreach (string k in Kinds) i.Counts[k] = Mathf.RoundToInt(F(d, k));
			return i;
		}

		/// <summary>One line of raft_islands.txt.</summary>
		public static string Format(RaftIsland i)
		{
			Func<float, string> f = v => v.ToString("0.##", CultureInfo.InvariantCulture);
			var sb = new StringBuilder();
			sb.Append("scene=").Append(i.Scene).Append("\tlabel=").Append(i.Label).Append("\tkind=").Append(i.Kind).Append("\tstyle=").Append(i.Style);
			sb.Append("\tlength=").Append(f(i.Length)).Append("\twidth=").Append(f(i.Width)).Append("\tangle=").Append(f(i.Angle));
			sb.Append("\tarea=").Append(f(i.Area)).Append("\tradius=").Append(f(i.Radius)).Append("\ttop=").Append(f(i.Top)).Append("\tshelf=").Append(f(i.Shelf));
			sb.Append("\tpeaks=").Append(i.Peaks).Append("\tslope=").Append(f(i.Slope)).Append("\tcliffs=").Append(f(i.Cliffs)).Append("\tbeach=").Append(f(i.Beach)).Append("\tcompact=").Append(f(i.Compact));
			foreach (string k in Kinds) sb.Append('\t').Append(k).Append('=').Append(i.Count(k));
			return sb.ToString();
		}

		/// <summary>The island's picture (null if there is none).</summary>
		public static Texture2D Thumbnail(RaftIsland i)
		{
			Texture2D t;
			if (thumbs.TryGetValue(i.Scene, out t)) return t;
			// (shipped as .jpg; a new measurement writes .jpg too, older ones .png)
			byte[] png = ModFile(ThumbFolder + "/" + SafeName(i.Scene) + ".jpg") ?? ModFile(ThumbFolder + "/" + SafeName(i.Scene) + ".png");
			t = null;
			if (png != null)
			{
				t = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "CI_Thumb_" + i.Label, wrapMode = TextureWrapMode.Clamp };
				if (!t.LoadImage(png)) { UnityEngine.Object.Destroy(t); t = null; }
			}
			thumbs[i.Scene] = t;
			return t;
		}

		/// <summary>The island's ground heights (null if they weren't measured).</summary>
		public static HeightField Heights(RaftIsland i)
		{
			HeightField h;
			if (heights.TryGetValue(i.Scene, out h)) return h;
			h = null;
			byte[] data = ModFile(HeightFolder + "/" + SafeName(i.Scene) + ".bin");
			if (data != null)
				try { h = ReadHeights(data); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Heights of " + i.Label + ": " + e.Message); }
			heights[i.Scene] = h;
			return h;
		}

		#region Settings from a measured island

		/// <summary>Raft's small islands (the pooled "Small" ones), its big ones, and Balboa: average land radius and highest point (measured; the numbers are fallbacks without the file).</summary>
		public static float SmallRadius { get { return Average(i => i.Label.StartsWith("Small island"), i => i.Radius, 16f); } }
		public static float LargeRadius { get { return Average(i => i.Label.StartsWith("Big island") || i.Label == "Big", i => i.Radius, 89f); } }
		public static float BalboaRadius { get { return Average(i => i.Label == "Balboa Island", i => i.Radius, 226f); } }
		public static float SmallTop { get { return Average(i => i.Label.StartsWith("Small island"), i => i.Top, 12f); } }
		/// <summary>(the median of the big ones: Twin peak's 123 m would pull an average up)</summary>
		public static float LargeTop { get { var t = All.Where(i => i.Label.StartsWith("Big island") || i.Label == "Big").Select(i => i.Top).OrderBy(v => v).ToList(); return t.Count == 0 ? 49f : t.Count % 2 == 1 ? t[t.Count / 2] : (t[t.Count / 2 - 1] + t[t.Count / 2]) / 2f; } }
		public static float BalboaTop { get { return Average(i => i.Label == "Balboa Island", i => i.Top, 151f); } }

		static float Average(Func<RaftIsland, bool> which, Func<RaftIsland, float> value, float fallback)
		{
			var list = All.Where(which).ToList();
			return list.Count > 0 ? list.Average(value) : fallback;
		}

		/// <summary>The islands worth offering in "Randomize existing": real land (no floating rafts).</summary>
		public static List<RaftIsland> Offered { get { return All.Where(i => i.Area >= 200f && i.Top >= 2f).ToList(); } }

		/// <summary>
		/// Generator settings for "something new like it": the island's size, height, peaks, slopes, coast, stretch,
		/// shallow water, style and object mix, with the content settings of <paramref name="from"/> (seed included).
		/// </summary>
		public static IslandGenSettings LikeIt(RaftIsland i, IslandGenSettings from)
		{
			IslandGenSettings s = from.Copy();
			s.Source = "";
			s.Shape = i.Label.Contains("Cresent") ? IslandShapes.Crescent : i.Label.Contains("Twin peak") ? IslandShapes.TwinPeaks : IslandShapes.Round;
			s.Style = i.StyleIndex;
			s.Radius = Mathf.Clamp(i.Radius, IslandGenSettings.MinRadius, IslandGenSettings.MaxRadius);
			s.Height = Mathf.Clamp(i.Top, IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight);
			s.Peaks = Mathf.Clamp(i.Peaks, 1, IslandGenSettings.MaxPeaks);
			s.Roughness = Mathf.Clamp01((i.Slope - 12f) / 35f);
			s.Coast = Mathf.Clamp01((1f - i.Compact) * 1.2f);
			s.Bays = Mathf.Clamp01((0.55f - i.Compact) * 1.3f);
			s.Cliffs = Mathf.Clamp01(i.Cliffs);
			s.BeachWidth = Mathf.Clamp01(i.Beach * 1.4f);
			s.Stretch = Mathf.Clamp(i.Width > 1f ? i.Length / i.Width : 1f, 1f, IslandGenSettings.MaxStretch);
			s.StretchAngle = i.Angle;
			// (the shallow water off its coast, in the shape ShelfMetres gives it)
			float shelf = Mathf.Max(0f, i.Shelf - i.Radius);
			s.Shelf = Mathf.Clamp01((shelf - 0.06f * i.Radius - 4f) / (0.54f * i.Radius + 26f));
			// (and its sea floor: Raft's own islands rise from the deep; how steeply its ground falls away)
			s.SeaFloor = IslandGenSettings.SeaFloorDeep;
			s.DropOff = RaftUnderwater.DropOffOf(i);
			ObjectsLike(i, s);
			Remakes.LookLike(i, s, false);
			return s;
		}

		/// <summary>Settings for "a variation of it": its own ground (scaled, stretched, roughened... from there), style and object mix.</summary>
		public static IslandGenSettings VariationOf(RaftIsland i, IslandGenSettings from)
		{
			// (a built place - Tangaroa, Utopia, Varuna Point - was measured with its buildings as ground: towers of earth.
			// Its variation is new ground like it with its look's pieces, TODO §3)
			if (i.Kind == "mesh" && Remakes.LookFor(i.Scene) != null) return LikeIt(i, from);
			IslandGenSettings s = from.Copy();
			s.Source = i.Scene;
			s.Style = i.StyleIndex;
			// (its real size: Temperance was scaled down to the build area, LM7 - the terrain grows instead, IslandGenerator.AreaFor)
			s.Radius = Mathf.Clamp(i.Radius, IslandGenSettings.MinRadius, IslandGenSettings.MaxSourceRadius);
			s.Height = Mathf.Clamp(i.Top, IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight);
			s.Stretch = 1f; s.StretchAngle = 0f; s.Mirror = false; s.SourceRoughen = 0f; s.SourceWobble = 0f;
			s.PeakShape = 0.3f; s.Terraces = 0f; s.Lakes = 0f; s.Valleys = 0f; s.Erosion = 0f; s.Seabed = IslandGenSettings.SeabedSand;
			s.SeaFloor = IslandGenSettings.SeaFloorDeep; // (its own ground goes down to Raft's sea floor)
			ObjectsLike(i, s);
			Remakes.LookLike(i, s, true);
			return s;
		}

		/// <summary>
		/// The land object sliders at the average density of Raft's own plain islands: its "Small island n"s, or its big ones.
		/// False (nothing changed) when raft_islands.txt has none.
		/// </summary>
		public static bool DensitiesLikeRaft(IslandGenSettings s, bool small)
		{
			// (Like Raft: a small island gets Raft's small islands' density by itself, from its land - CIGenLikeRaft)
			if (!RaftLand.Loaded) return false;
			IslandGenerator.NatureLikeRaft(s);
			return true;
		}

		/// <summary>The object sliders set to the island's own densities (and a few creatures and boxes if it has spawners and loot).</summary>
		static void ObjectsLike(RaftIsland i, IslandGenSettings s)
		{
			// (each kind as thick on the land as on the island itself, against Raft's islands of its size: Like Raft when it's
			// as thick as they usually are - counted on its land only, raft_land.txt; what lies under water is the sea's)
			float[] own = RaftLand.IslandDensities(i.Label);
			bool small = i.Area < RaftLand.SmallLand;
			string[] cats = { IslandGenerator.CatTrees, IslandGenerator.CatBushes, IslandGenerator.CatRocks, IslandGenerator.CatHarvest };
			var v = new float[cats.Length];
			for (int k = 0; k < cats.Length; k++)
			{
				float like = IslandGenerator.LikeRaftAmount(s.Style, cats[k]), typical = IslandGenerator.TypicalDensity(s.Style, cats[k], small);
				// (at most twice Like Raft: a tiny island with a handful of plants - Raft's Radar - scaled up to the slider's top,
				// twelve times Raft's harvestables on Radio Tower Remade, 2026-10-03)
				v[k] = own != null && typical > 0f ? Mathf.Clamp01(Mathf.Min(like * Mathf.Sqrt(own[k] / typical), like * 2f)) : like;
			}
			s.Trees = v[0]; s.Bushes = v[1]; s.Rocks = v[2]; s.Harvest = v[3];
			s.BeachThings = IslandGenerator.LikeRaftAmount(s.Style, IslandGenerator.CatBeach);
			// (under water: as dense as around Raft's islands of its style, measured by CIMeasureUnderwater)
			s.Water = s.SeaRocks = s.SeaFinds = s.Sunken = 0.5f;
			s.Hostiles = Mathf.Clamp(Mathf.RoundToInt(i.Count(Spawners) * 0.5f), 0, 12);
			s.Loot = Mathf.Clamp(Mathf.RoundToInt(i.Count(Loot) / 4f), 0, 20);
		}

		#endregion

		const uint HeightMagic = 0x48495343; // "CSIH"

		/// <summary>Height file: magic, version, cell size, nx, nz, then nx*nz int16 decimetres above the sea (row-major [z, x]), deflated.</summary>
		public static byte[] WriteHeights(HeightField h)
		{
			using (var ms = new MemoryStream())
			{
				var header = new BinaryWriter(ms);
				header.Write(HeightMagic);
				header.Write(1);
				header.Flush();
				using (var deflate = new DeflaterOutputStream(ms) { IsStreamOwner = false })
				using (var w = new BinaryWriter(deflate))
				{
					w.Write(h.Cell); w.Write(h.Nx); w.Write(h.Nz);
					for (int z = 0; z < h.Nz; z++)
						for (int x = 0; x < h.Nx; x++)
							w.Write((short)Mathf.Clamp(Mathf.RoundToInt(h.H[z, x] * 10f), short.MinValue, short.MaxValue));
					w.Flush();
					deflate.Finish();
				}
				return ms.ToArray();
			}
		}

		static HeightField ReadHeights(byte[] data)
		{
			using (var ms = new MemoryStream(data))
			{
				var header = new BinaryReader(ms);
				if (header.ReadUInt32() != HeightMagic) throw new InvalidDataException("not a height file");
				header.ReadInt32();
				using (var inflate = new InflaterInputStream(ms) { IsStreamOwner = false })
				using (var r = new BinaryReader(inflate))
				{
					var h = new HeightField { Cell = r.ReadSingle(), Nx = r.ReadInt32(), Nz = r.ReadInt32() };
					if (h.Nx < 2 || h.Nz < 2 || h.Nx > 2048 || h.Nz > 2048 || h.Cell <= 0f) throw new InvalidDataException("bad size " + h.Nx + "x" + h.Nz);
					h.H = new float[h.Nz, h.Nx];
					for (int z = 0; z < h.Nz; z++)
						for (int x = 0; x < h.Nx; x++)
							h.H[z, x] = r.ReadInt16() / 10f;
					return h;
				}
			}
		}
	}
}
