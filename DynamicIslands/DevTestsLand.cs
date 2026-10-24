using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicIslands
{
	/// <summary>
	/// Where Raft puts things on the land of its islands (CIMeasureLand): every tree, bush, rock, harvestable and piece
	/// of driftwood with the habitat it stands in - how far inland from the coast, how steep, how high above the sea,
	/// on which ground texture - and the land's area per habitat, so the generator can place things as Raft does.
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIMeasureLand", docs: "Dev, editor: where Raft puts every tree, bush, rock, harvestable and piece of driftwood on the land of its islands - how far inland, how steep, how high, on which ground texture, how far apart - and the land's area per habitat: raft_land.txt in Mods\\DynamicIslands. CIMeasureLand [part of a scene name]")]
		public static void MeasureLandCommand(string[] args)
		{
			string filter = args != null && args.Length > 0 ? string.Join(" ", args) : null;
			StartTest(MeasureLandRoutine(filter));
		}

		/// <summary>The generator's kind of a Raft object on land (trees, bushes, rocks, harvest, beach), or props / sea.</summary>
		internal static string LandKindOf(string name)
		{
			if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^(Log|TreeLog_\d+|Driftwood\w*|Plank\w*)$")) return IslandGenerator.CatBeach;
			// (Raft's flowers are pickups, but plants: raft_land.txt has them with the bushes, and so does the generator)
			if (name.StartsWith("Pickup_Landmark_Flower_")) return IslandGenerator.CatBushes;
			string k = KindOfObject(name);
			if (k == RaftIslands.Trees) return IslandGenerator.CatTrees;
			if (k == RaftIslands.Bushes) return IslandGenerator.CatBushes;
			if (k == RaftIslands.Rocks) return IslandGenerator.CatRocks;
			if (k == RaftIslands.Harvest) return IslandGenerator.CatHarvest;
			return k;
		}

		static IEnumerator MeasureLandRoutine(string filter)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			float t0 = Time.realtimeSinceStartup;
			List<string> scenes = PlaceableCatalog.LandmarkSceneNames().Where(s => filter == null || s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			Log("Measuring the land of " + scenes.Count + " of Raft's island scenes");
			Scene lab = SceneManager.CreateScene("CI_MeasureLab", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
			var text = new StringBuilder();
			text.Append("# Where Raft puts things on the land of its islands (CIMeasureLand, raft=").Append(Application.version).Append(")\n");
			text.Append("# Habitat bins (RaftLand.BinOf): inland from the coast 0-3-8-20-50+ m x slope 0-10-20-30-40+ deg x height above the sea 0-1.5-4-12-30+ m\n");
			text.Append("# land\t<island>\t<style>\t<top m>\tm² of land per bin (bin:m²)\n");
			text.Append("# lobj\t<island>\t<name>\t<kind>\tcount\tcount per bin\theight p10/p50/p90\tinland p10/p50/p90\tslope p10/p50/p90\ton sand/grass/rock/other\tsize p50\tabove ground p50\tnearest same p50\tnearest any p50\n");
			int measured = 0;
			try
			{
				for (int i = 0; i < scenes.Count; i++)
				{
					if (!DynamicIslands.InEditor()) { Fail("left the editor while measuring"); yield break; }
					if (i > 0 && i % 6 == 0) yield return Resources.UnloadUnusedAssets();
					string why = "not loaded";
					bool ok = false;
					yield return PlaceableCatalog.VisitScene(scenes[i], s => MeasureScene(s, lab, r => ok = true, w => why = w, (island, h, n, cell, origin) =>
					{
						try { text.Append(LandLines(s, island, h, n, cell, origin)); }
						catch (Exception e) { Log("  " + scenes[i] + ": " + e); }
					}));
					if (ok) { measured++; Log("  " + scenes[i] + " measured"); } else Log("  " + scenes[i] + ": " + why);
				}
			}
			finally
			{
				if (lab.IsValid()) SceneManager.UnloadSceneAsync(lab);
			}
			string path = Path.Combine(DynamicIslands.assetpath, RaftLand.FileName);
			File.WriteAllText(path, text.ToString());
			RaftLand.Reload();
			Log("Measured the land of " + measured + " island(s) in " + (Time.realtimeSinceStartup - t0).ToString("F0") + " s: " + Path.GetFullPath(path));
			if (measured > 0) Log("PASS: measured the land"); else Fail("measured nothing");
		}

		[ConsoleCommand(name: "CILandLikeRaft", docs: "Dev, editor: the generator puts trees, bushes, rocks and harvestables where Raft's islands of the style have them (raft_land.txt): per kind, how much of its spread over the habitats overlaps Raft's; no more trees on the beach or on cliffs than Raft has; big rocks sunk; pictures of each style (shot_land_*)")]
		public static void LandLikeRaftCommand()
		{
			StartTest(LandLikeRaftRoutine());
		}

		static IEnumerator LandLikeRaftRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); yield break; }
			bool ok = true;
			Check(ref ok, RaftLand.Loaded && DynamicIslands.instance.modlistEntry.modinfo.modFiles.ContainsKey(RaftLand.FileName), "Raft's land is measured (" + RaftLand.FileName + " shipped): " +
				string.Join(", ", Enumerable.Range(0, TerrainPainter.Styles.Length).Select(i => TerrainPainter.StyleName(i) + " " + RaftLand.For(i).Things.Count + " kinds").ToArray()));
			Vector3 size = IslandGenerator.BuildArea;
			int res = IslandGenerator.BuildResolution;
			float step = size.x / (res - 1);
			var core = new HashSet<string>(PlaceableCatalog.CoreNames);
			// (the beach strip and the cliffs: where Raft has almost nothing but bamboo and boulders)
			Func<int, bool> beachStrip = b => { int i, sl, hb; RaftLand.Unbin(b, out i, out sl, out hb); return i == 0 || hb == 0; };
			Func<int, bool> cliff = b => { int i, sl, hb; RaftLand.Unbin(b, out i, out sl, out hb); return sl == RaftLand.Bins1 - 1; };
			Func<int, int, IslandGenSettings> settingsFor = (st, seed) =>
			{
				var gs = new IslandGenSettings { Seed = seed, Radius = 80f, Height = 45f, Style = st, Cliffs = 0.3f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
				// (at Like Raft: thicker than that - a jungle - thickens the higher ground only (the review 2026-10-03), so at
				// 0.45 Tropical trees went inland, 52 % overlap with 19 % of Raft's on the low flat shore against 3 %)
				IslandGenerator.NatureLikeRaft(gs);
				gs.BeachThings = 0f;
				return gs;
			};
			foreach (int style in new[] { TerrainPainter.Tropical, TerrainPainter.Forest, TerrainPainter.Desert, TerrainPainter.Snowy })
			{
				IslandGenSettings s = settingsFor(style, 3100 + style);
				float[,] m = IslandGenerator.HeightsMetres(s, size, res);
				int[] bins = IslandGenerator.HabitatBins(s, m, step);
				var genArea = new float[RaftLand.BinCount];
				foreach (int b in bins) if (b >= 0) genArea[b] += step * step;
				// (against Raft's islands of the size the generator goes by - its big ones here (11aa73a): all of them pooled,
				// the small islands' thicker, more coastal spread took Tropical trees' overlap down to 52 %)
				LandStyle land = RaftLand.For(style, genArea.Sum() < RaftLand.SmallLand);
				List<IslandObject> objs = IslandGenerator.PlanAll(s, m, size);
				Func<IslandObject, int> binOf = o => { int x = Mathf.Clamp(Mathf.RoundToInt(o.Position.x / step), 0, res - 1), z = Mathf.Clamp(Mathf.RoundToInt(o.Position.z / step), 0, res - 1); return bins[z * res + x]; };
				foreach (string cat in new[] { IslandGenerator.CatTrees, IslandGenerator.CatBushes, IslandGenerator.CatRocks, IslandGenerator.CatHarvest })
				{
					var kinds = land.Of(cat).Where(t => core.Contains(t.Name)).ToList();
					if (kinds.Sum(t => t.Count) < 25) { Log("  " + TerrainPainter.StyleName(style) + " " + cat + ": too few on Raft's islands (the zone rules place them)"); continue; }
					var names = new HashSet<string>(kinds.Select(t => t.Name));
					var placed = objs.Where(o => names.Contains(o.Name)).ToList();
					if (placed.Count < 20) { Check(ref ok, placed.Count > 0, TerrainPainter.StyleName(style) + " " + cat + ": only " + placed.Count + " placed"); continue; }
					// Raft's spread over this island's habitats (density x area) against the generated one
					var raft = new float[RaftLand.BinCount]; var gen = new float[RaftLand.BinCount];
					// (over the land that isn't cliff: the generator keeps plants off the rock its painting puts on slopes over
					// about 35° - on Balboa, Raft has trees on grass-painted 40-55° slopes; that part is checked on its own below)
					bool rockBound = cat == IslandGenerator.CatRocks;
					for (int b = 0; b < RaftLand.BinCount; b++) raft[b] = rockBound || !cliff(b) ? kinds.Sum(t => t.Density[b]) * genArea[b] : 0f;
					foreach (IslandObject o in placed) { int b = binOf(o); if (b >= 0 && (rockBound || !cliff(b))) gen[b]++; }
					// (a few can't show a spread over many habitats, nor can one island's land: under 60 placed, two more islands of
					// the style pooled in - 21 Snowy rocks on one overlapped 52 %, under even the bar of drawing as many from Raft's)
					int islands = 1;
					for (int extra = 1; extra <= 2 && gen.Sum() < 60f; extra++, islands++)
					{
						IslandGenSettings s2 = settingsFor(style, 3100 + style + 1000 * extra);
						float[,] m2 = IslandGenerator.HeightsMetres(s2, size, res);
						int[] bins2 = IslandGenerator.HabitatBins(s2, m2, step);
						var area2 = new float[RaftLand.BinCount];
						foreach (int b in bins2) if (b >= 0) area2[b] += step * step;
						for (int b = 0; b < RaftLand.BinCount; b++) if (rockBound || !cliff(b)) raft[b] += kinds.Sum(t => t.Density[b]) * area2[b];
						foreach (IslandObject o in IslandGenerator.PlanAll(s2, m2, size).Where(p => names.Contains(p.Name)))
						{
							int b = bins2[Mathf.Clamp(Mathf.RoundToInt(o.Position.z / step), 0, res - 1) * res + Mathf.Clamp(Mathf.RoundToInt(o.Position.x / step), 0, res - 1)];
							if (b >= 0 && (rockBound || !cliff(b))) gen[b]++;
						}
					}
					float rt = raft.Sum();
					float gt = Mathf.Max(1f, gen.Sum());
					float overlap = rt > 0f ? Enumerable.Range(0, RaftLand.BinCount).Sum(b => Mathf.Min(raft[b] / rt, gen[b] / gt)) : 0f;
					// (big ones - trees but bamboo, big rocks - on the beach strip and on cliffs, against Raft's share)
					Func<string, bool> big = n => !n.StartsWith("Bamboo");
					float raftBeach = 0f, raftCliff = 0f, raftBig = 0f;
					for (int b = 0; b < RaftLand.BinCount; b++) { float v = kinds.Where(t => big(t.Name)).Sum(t => t.Density[b]) * genArea[b]; raftBig += v; if (beachStrip(b)) raftBeach += v; if (cliff(b)) raftCliff += v; }
					var bigPlaced = placed.Where(o => big(o.Name)).ToList();
					float genBeach = bigPlaced.Count(o => { int b = binOf(o); return b >= 0 && beachStrip(b); }) / Mathf.Max(1f, bigPlaced.Count);
					float genCliff = bigPlaced.Count(o => { int b = binOf(o); return b >= 0 && cliff(b); }) / Mathf.Max(1f, bigPlaced.Count);
					raftBeach /= Mathf.Max(1e-6f, raftBig); raftCliff /= Mathf.Max(1e-6f, raftBig);
					string label = TerrainPainter.StyleName(style) + " " + cat;
					// (measured far inland on Raft - Temperance's small islands sit on its big terrain, their "coast" 100 m
					// away - the spread can't be compared on an island this size: only where they may not be is checked)
					float inland50 = kinds.Sum(t => t.Inland[1] * t.Count) / Mathf.Max(1, kinds.Sum(t => t.Count));
					if (inland50 > 60f) Log("  " + label + ": measured " + inland50.ToString("F0") + " m inland on Raft (a bigger terrain): " + placed.Count + " placed, " + (overlap * 100f).ToString("F0") + " % overlap (not compared)");
					// (55 %: plants kept off the 35-40° slopes the painting turns to rock take a little of Raft's spread away; a few
					// can't cover a spread over many habitats - at Like Raft, 21 Snowy rocks drawn from Raft's own spread overlap it
					// by less than that one time in several: the bar is what 19 in 20 such draws of as many reach, when it is lower)
					float bar = Mathf.Min(0.55f, DrawnOverlap(raft, rt, Mathf.RoundToInt(gen.Sum())));
					if (inland50 <= 60f) Check(ref ok, overlap > bar, label + ": " + placed.Count + " placed" + (islands > 1 ? " (" + gen.Sum().ToString("F0") + " on " + islands + " islands)" : "") + ", " + (overlap * 100f).ToString("F0") + " % of their spread over the habitats overlaps Raft's" +
						(bar < 0.55f ? " (bar " + (bar * 100f).ToString("F0") + " %: as many drawn from Raft's spread reach that 19 times in 20)" : ""));
					// (where it differs most: inland band, slope band, height band - Raft's share against the generator's)
					if (overlap <= bar && rt > 0f)
						Log("    land " + genArea.Sum().ToString("F0") + " m² (" + (genArea.Sum() < RaftLand.SmallLand ? "small" : "big") + "); most apart: " + string.Join(", ",
							Enumerable.Range(0, RaftLand.BinCount).OrderByDescending(b => Mathf.Abs(raft[b] / rt - gen[b] / gt)).Take(6).Select(b => { int bi, bs, bh; RaftLand.Unbin(b, out bi, out bs, out bh);
								return "in" + bi + "/sl" + bs + "/h" + bh + " Raft " + (raft[b] / rt).ToString("P0") + " gen " + (gen[b] / gt).ToString("P0"); }).ToArray()));
					if (cat == IslandGenerator.CatTrees || cat == IslandGenerator.CatBushes)
						Check(ref ok, genBeach <= raftBeach + 0.05f && genCliff <= raftCliff + 0.05f, label + ": on the beach strip " + genBeach.ToString("P0") + " (Raft " + raftBeach.ToString("P0") + "), on cliffs over 40° " + genCliff.ToString("P0") + " (Raft " + raftCliff.ToString("P0") + ")");
				}
				// Rocks sunk as Raft's: its big boulders stand about a third of their size in the ground
				var sunk = objs.Where(o => land.Of(IslandGenerator.CatRocks).Any(t => t.Name == o.Name && t.Above < -1f)).ToList();
				if (sunk.Count > 0)
				{
					// (or as deep as "never out of sight" lets it - D62, 6494a3d: its top a third of its height and 10 cm over the
					// terrain; a desert rock with its pivot in its middle goes no deeper than 0.2-0.4 m by its pivot)
					Func<IslandObject, bool> isSunk = o =>
					{
						float g = IslandGenerator.TerrainSurface(m, step, o.Position.x, o.Position.z);
						if (o.Position.y < g - 0.3f) return true;
						Bounds lb;
						if (!PlaceableCatalog.LocalBounds(o.Name, out lb)) return false;
						float ly = Mathf.Abs(o.Scale.y), height = lb.size.y * ly;
						return o.Position.y + lb.max.y * ly - g < 0.35f * height + 0.2f;
					};
					float below = sunk.Count(isSunk) / (float)sunk.Count;
					Check(ref ok, below > 0.9f, TerrainPainter.StyleName(style) + ": " + below.ToString("P0") + " of " + sunk.Count + " big rocks sunk into the ground as Raft's are");
				}
				// A picture of it
				IslandGenerator.GenerateInEditor(s);
				IslandGenerator.FrameCamera(s);
				yield return new WaitForSecondsRealtime(1.2f);
				Screenshot(new[] { "land_" + TerrainPainter.StyleName(style).ToLowerInvariant() });
				yield return new WaitForSecondsRealtime(0.6f);
			}
			if (ok) Log("PASS: land objects placed like Raft's"); else Fail("land objects placed like Raft's");
		}

		/// <summary>How much n things drawn at random from Raft's spread (per habitat bin, rt its sum) overlap it: what 19 draws in 20 reach.</summary>
		static float DrawnOverlap(float[] raft, float rt, int n)
		{
			if (n <= 0 || rt <= 0f) return 0f;
			var rnd = new System.Random(n);
			var cum = new float[raft.Length];
			float c = 0f;
			for (int b = 0; b < raft.Length; b++) { c += raft[b] / rt; cum[b] = c; }
			var count = new int[raft.Length];
			var got = new List<float>();
			for (int trial = 0; trial < 200; trial++)
			{
				Array.Clear(count, 0, count.Length);
				for (int i = 0; i < n; i++)
				{
					int b = Array.BinarySearch(cum, (float)rnd.NextDouble() * c);
					count[Mathf.Clamp(b < 0 ? ~b : b, 0, raft.Length - 1)]++;
				}
				float o = 0f;
				for (int b = 0; b < raft.Length; b++) o += Mathf.Min(raft[b] / rt, count[b] / (float)n);
				got.Add(o);
			}
			got.Sort();
			return got[got.Count / 20];
		}

		/// <summary>Which ground texture a Raft terrain layer is: 0 sand, 1 grass (snow, ground), 2 rock, 3 other.</summary>
		static int TextureSlotOf(string texture)
		{
			foreach (TerrainPainter.Style s in TerrainPainter.Styles)
			{
				int i = Array.IndexOf(s.Textures, texture);
				if (i == TerrainPainter.Sand) return 0;
				if (i == TerrainPainter.Grass) return 1;
				if (i == TerrainPainter.Rock) return 2;
			}
			string t = texture.ToLowerInvariant();
			if (t.Contains("sand") || t.Contains("beach")) return 0;
			if (t.Contains("grass") || t.Contains("snow") || t.Contains("ground") || t.Contains("moss") || t.Contains("dirt")) return 1;
			if (t.Contains("rock") || t.Contains("cliff") || t.Contains("stone") || t.Contains("gravel")) return 2;
			return 3;
		}

		/// <summary>One island's lines of raft_land.txt.</summary>
		static string LandLines(Scene src, RaftIsland island, float[,] h, int n, float cell, Vector2 origin)
		{
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			// Distance inland from the coast (m) of every land cell (two chamfer passes from the water cells)
			var d = new float[n, n];
			for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) d[z, x] = h[z, x] <= 0f ? 0f : 1e9f;
			for (int pass = 0; pass < 2; pass++)
			{
				for (int z = 0; z < n; z++)
					for (int x = 0; x < n; x++)
					{
						float v = d[z, x];
						if (x > 0) v = Mathf.Min(v, d[z, x - 1] + 1f); if (z > 0) v = Mathf.Min(v, d[z - 1, x] + 1f);
						if (x > 0 && z > 0) v = Mathf.Min(v, d[z - 1, x - 1] + 1.414f); if (x < n - 1 && z > 0) v = Mathf.Min(v, d[z - 1, x + 1] + 1.414f);
						d[z, x] = v;
					}
				for (int z = n - 1; z >= 0; z--)
					for (int x = n - 1; x >= 0; x--)
					{
						float v = d[z, x];
						if (x < n - 1) v = Mathf.Min(v, d[z, x + 1] + 1f); if (z < n - 1) v = Mathf.Min(v, d[z + 1, x] + 1f);
						if (x < n - 1 && z < n - 1) v = Mathf.Min(v, d[z + 1, x + 1] + 1.414f); if (x > 0 && z < n - 1) v = Mathf.Min(v, d[z + 1, x - 1] + 1.414f);
						d[z, x] = v;
					}
			}
			Func<int, int, float> slopeAt = (x, z) =>
			{
				int x0 = Mathf.Max(0, x - 1), x1 = Mathf.Min(n - 1, x + 1), z0 = Mathf.Max(0, z - 1), z1 = Mathf.Min(n - 1, z + 1);
				float gx = (h[z, x1] - h[z, x0]) / ((x1 - x0) * cell), gz = (h[z1, x] - h[z0, x]) / ((z1 - z0) * cell);
				return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
			};
			Func<float, float, float> sample = (wx, wz) =>
			{
				float fx = Mathf.Clamp((wx - origin.x) / cell, 0f, n - 1.001f), fz = Mathf.Clamp((wz - origin.y) / cell, 0f, n - 1.001f);
				int x0 = (int)fx, z0 = (int)fz; float tx = fx - x0, tz = fz - z0;
				return Mathf.Lerp(Mathf.Lerp(h[z0, x0], h[z0, x0 + 1], tx), Mathf.Lerp(h[z0 + 1, x0], h[z0 + 1, x0 + 1], tx), tz);
			};

			// The land's area per habitat bin
			var area = new float[RaftLand.BinCount];
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
					if (h[z, x] > 0f) area[RaftLand.BinOf(d[z, x] * cell, slopeAt(x, z), h[z, x])] += cell * cell;
			var sb = new StringBuilder();
			sb.Append("land\t").Append(island.Label).Append('\t').Append(island.Style).Append('\t').Append(island.Top.ToString("0.#", inv)).Append('\t')
				.Append(string.Join(" ", Enumerable.Range(0, RaftLand.BinCount).Where(b => area[b] > 0f).Select(b => b + ":" + area[b].ToString("0", inv)).ToArray())).Append('\n');

			// The ground textures (the scene's terrains)
			var terrains = src.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)).Where(t => t.terrainData != null && t.terrainData.terrainLayers != null && t.terrainData.terrainLayers.Length > 0).ToList();
			Func<Vector3, float[]> textureAt = p =>
			{
				var shares = new float[4];
				foreach (Terrain t in terrains)
				{
					TerrainData td = t.terrainData;
					Vector3 local = p - t.transform.position;
					if (local.x < 0f || local.z < 0f || local.x > td.size.x || local.z > td.size.z) continue;
					int ar = td.alphamapResolution;
					int ax = Mathf.Clamp((int)(local.x / td.size.x * ar), 0, ar - 1), az = Mathf.Clamp((int)(local.z / td.size.z * ar), 0, ar - 1);
					float[,,] a = td.GetAlphamaps(ax, az, 1, 1);
					for (int l = 0; l < td.alphamapLayers; l++)
					{
						TerrainLayer tl = td.terrainLayers[l];
						shares[TextureSlotOf(tl != null && tl.diffuseTexture != null ? tl.diffuseTexture.name : "")] += a[0, 0, l];
					}
					return shares;
				}
				shares[3] = 1f; // (no terrain here: a mesh island)
				return shares;
			};

			// Everything placed on the land: the placeables, and every (not nested) pickup
			var found = new List<KeyValuePair<string, Transform>>(PlaceableCatalog.PlaceablesOf(src));
			foreach (GameObject root in src.GetRootGameObjects())
				foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
				{
					if (!t.name.StartsWith("Pickup_", StringComparison.OrdinalIgnoreCase)) continue;
					bool nested = false;
					for (Transform p = t.parent; p != null && !nested; p = p.parent) nested = p.name.StartsWith("Pickup_", StringComparison.OrdinalIgnoreCase);
					if (!nested) found.Add(new KeyValuePair<string, Transform>(PlaceableCatalog.CleanName(t.name), t));
				}
			var items = new List<KeyValuePair<string, float[]>>(); // bin, height, inland, slope, sand, grass, rock, other, size, above, x, z
			foreach (var kv in found)
			{
				string kind = LandKindOf(kv.Key);
				if (kind == RaftIslands.Sea || kind == RaftIslands.Props || kind == RaftIslands.Loot) continue;
				Vector3 pos = kv.Value.position;
				float gx = (pos.x - origin.x) / cell, gz = (pos.z - origin.y) / cell;
				if (gx < 0 || gz < 0 || gx > n - 1 || gz > n - 1) continue;
				float ground = sample(pos.x, pos.z);
				if (ground <= 0f) continue; // (under water: raft_underwater.txt)
				int ix = Mathf.RoundToInt(gx), iz = Mathf.RoundToInt(gz);
				float size = 0f;
				Renderer[] rs = kv.Value.GetComponentsInChildren<Renderer>(true);
				if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds); size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); }
				float slope = slopeAt(ix, iz), inland = d[iz, ix] * cell;
				float[] tex = textureAt(pos);
				items.Add(new KeyValuePair<string, float[]>(kv.Key, new[] { RaftLand.BinOf(inland, slope, ground), ground, inland, slope, tex[0], tex[1], tex[2], tex[3], size, pos.y - ground, pos.x, pos.z }));
			}
			// The nearest other object, of the same kind and of any kind
			var nearSame = new float[items.Count]; var nearAny = new float[items.Count];
			for (int a = 0; a < items.Count; a++)
			{
				float same = 999f, any = 999f;
				for (int b = 0; b < items.Count; b++)
				{
					if (a == b) continue;
					float dx = items[a].Value[10] - items[b].Value[10], dz = items[a].Value[11] - items[b].Value[11], dd = Mathf.Sqrt(dx * dx + dz * dz);
					if (dd < any) any = dd;
					if (dd < same && items[a].Key == items[b].Key) same = dd;
				}
				nearSame[a] = same; nearAny[a] = any;
			}
			foreach (var g in items.Select((it, i) => new { it, i }).GroupBy(x => x.it.Key).OrderByDescending(g => g.Count()))
			{
				var l = g.Select(x => x.it.Value).ToList();
				var bins = new Dictionary<int, int>();
				foreach (float[] o in l) { int b = (int)o[0]; int c; bins.TryGetValue(b, out c); bins[b] = c + 1; }
				Func<int, List<float>> col = k => l.Select(o => o[k]).ToList();
				Func<int, string> trip = k => P(col(k), 0.1f) + "/" + P(col(k), 0.5f) + "/" + P(col(k), 0.9f);
				Func<int, string> mean = k => (l.Average(o => o[k])).ToString("0.00", inv);
				sb.Append("lobj\t").Append(island.Label).Append('\t').Append(g.Key).Append('\t').Append(LandKindOf(g.Key)).Append('\t').Append(l.Count)
					.Append('\t').Append(string.Join(" ", bins.OrderBy(b => b.Key).Select(b => b.Key + ":" + b.Value).ToArray()))
					.Append('\t').Append(trip(1)).Append('\t').Append(trip(2)).Append('\t').Append(trip(3))
					.Append('\t').Append(mean(4)).Append('/').Append(mean(5)).Append('/').Append(mean(6)).Append('/').Append(mean(7))
					.Append('\t').Append(P(col(8), 0.5f)).Append('\t').Append(P(col(9), 0.5f))
					.Append('\t').Append(P(g.Select(x => nearSame[x.i]).ToList(), 0.5f)).Append('\t').Append(P(g.Select(x => nearAny[x.i]).ToList(), 0.5f)).Append('\n');
			}
			return sb.ToString();
		}

		[ConsoleCommand(name: "CITerrainDetails", docs: "Dev, anywhere: every terrain in the scene and its detail layers (Raft's grass and sea grass are terrain details, not objects): prototype, size, how many in all")]
		public static void TerrainDetailsCommand()
		{
			foreach (Terrain t in UnityEngine.Object.FindObjectsOfType<Terrain>())
			{
				TerrainData td = t.terrainData;
				int res = td.detailResolution;
				Log("  terrain " + t.name + " of " + t.transform.root.name + ": size " + td.size + ", detail resolution " + res + ", " + td.detailPrototypes.Length + " detail layers, drawn " + t.drawTreesAndFoliage + " (density " + t.detailObjectDensity + ", distance " + t.detailObjectDistance + ")");
				if (res <= 0 || td.detailPrototypes.Length == 0) continue;
				// (per height band above the sea, the world's 0: its area, and per detail layer how many stand in it and on which
				// ground texture - Raft's own islands, to set the generator's grass and sea grass as thick as there)
				float[] edges = { -1e6f, -12f, -8f, -5f, -3f, -1.5f, 0f, 1f, 2.5f, 5f, 10f, 1e6f };
				int bands = edges.Length - 1, ares = td.alphamapResolution, layers = td.alphamapLayers;
				float[,,] paint = td.GetAlphamaps(0, 0, ares, ares);
				float cell = td.size.x / res * (td.size.z / res);
				var area = new float[bands];
				var count = new long[td.detailPrototypes.Length, bands];
				var onTex = new Dictionary<string, long>[td.detailPrototypes.Length];
				var layerData = new int[td.detailPrototypes.Length][,];
				for (int i = 0; i < td.detailPrototypes.Length; i++) { layerData[i] = td.GetDetailLayer(0, 0, res, res, i); onTex[i] = new Dictionary<string, long>(); }
				var texArea = new Dictionary<string, float>();
				for (int z = 0; z < res; z++)
					for (int x = 0; x < res; x++)
					{
						float h = t.transform.position.y + td.GetInterpolatedHeight((x + 0.5f) / res, (z + 0.5f) / res);
						int b = 0;
						while (b < bands - 1 && h >= edges[b + 1]) b++;
						area[b] += cell;
						// (the ground texture there: the strongest layer of the paint)
						int ax = Mathf.Clamp(x * ares / res, 0, ares - 1), az = Mathf.Clamp(z * ares / res, 0, ares - 1), best = 0;
						for (int l = 1; l < layers; l++) if (paint[az, ax, l] > paint[az, ax, best]) best = l;
						string tex = td.terrainLayers != null && best < td.terrainLayers.Length && td.terrainLayers[best] != null && td.terrainLayers[best].diffuseTexture != null ? td.terrainLayers[best].diffuseTexture.name : "layer" + best;
						string key = (h < 0f ? "under " : "land ") + tex;
						texArea[key] = (texArea.ContainsKey(key) ? texArea[key] : 0f) + cell;
						for (int i = 0; i < td.detailPrototypes.Length; i++)
						{
							int v = layerData[i][z, x];
							if (v == 0) continue;
							count[i, b] += v;
							onTex[i][key] = (onTex[i].ContainsKey(key) ? onTex[i][key] : 0) + v;
						}
					}
				Log("    ground: " + string.Join(", ", texArea.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value.ToString("F0") + " m²").ToArray()));
				for (int i = 0; i < td.detailPrototypes.Length; i++)
				{
					DetailPrototype p = td.detailPrototypes[i];
					long n = 0;
					for (int b = 0; b < bands; b++) n += count[i, b];
					if (n == 0) { Log("    layer " + i + ": " + (p.prototypeTexture != null ? p.prototypeTexture.name : p.prototype != null ? p.prototype.name : "?") + " - none"); continue; }
					Log("    layer " + i + ": " + (p.prototype != null ? "mesh " + p.prototype.name : p.prototypeTexture != null ? "texture " + p.prototypeTexture.name : "?") + " " + p.renderMode + ", " + p.minWidth + "-" + p.maxWidth + " wide, " + p.minHeight + "-" + p.maxHeight + " high, " + n + " in all; per m² by height: " +
						string.Join(" ", Enumerable.Range(0, bands).Where(b => count[i, b] > 0).Select(b => (edges[b] < -1e5f ? "<" : edges[b].ToString("0.#")) + ".." + (edges[b + 1] > 1e5f ? "" : edges[b + 1].ToString("0.#")) + ":" + (count[i, b] / Mathf.Max(1f, area[b])).ToString("F2")).ToArray()) +
						"; on " + string.Join(", ", onTex[i].OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + " " + (kv.Value / Mathf.Max(1f, texArea[kv.Key])).ToString("F2") + "/m²").ToArray()));
				}
			}
			Log("PASS: terrain details");
		}

		[ConsoleCommand(name: "CIIslandDensity", docs: "Dev, editor: the island being edited against Raft's islands of its style (the user, 2026-10-02: no sea full of grass, no beaches full of stones, Raft's finds under water) - things per 1000 m2 of land (trees, bushes, rocks, harvestables, beach things), of the beach strip (stones, clay, sand on it) and of sea floor 0-40 m deep (corals and plants, sea vines and kelp, rocks, finds, sunken things); the finds kind by kind (ores, clay, sand, scrap, stones, clams, algae) and any of Raft's sea finds lying on dry land; DENSE / THIN where it is off by more than 1.6 times. CIIslandDensity [style 0-4]")]
		public static void IslandDensityCommand(string[] args)
		{
			if (!DynamicIslands.InEditor() || terraineditor.terrain == null) { Fail("CIIslandDensity (in the editor, an island open)"); return; }
			Terrain ground = terraineditor.terrain;
			int style = args != null && args.Length > 0 ? int.Parse(args[0]) : TerrainPainter.StyleOf(ground);
			float sea = DynamicIslands.EditorWaterLevel, baseY = ground.transform.position.y;
			Vector3 size = ground.terrainData.size;
			// (the ground on a 2 m grid: land, its beach strip - under 1.5 m above the sea - and sea floor 0-40 m deep within 60 m
			// of the land, as Raft's were measured round its islands: over the whole terrain a shallow floor counted 1 km2)
			const float cell = 2f, reach = 60f;
			int nx = Mathf.CeilToInt(size.x / cell), nz = Mathf.CeilToInt(size.z / cell);
			var heights = new float[nx, nz];
			var dist = new int[nx, nz];
			var queue = new Queue<int>();
			float land = 0f, beach = 0f, floor = 0f;
			for (int i = 0; i < nx; i++)
				for (int j = 0; j < nz; j++)
				{
					float h = heights[i, j] = ground.SampleHeight(new Vector3(ground.transform.position.x + (i + 0.5f) * cell, 0f, ground.transform.position.z + (j + 0.5f) * cell)) + baseY - sea;
					dist[i, j] = h > 0f ? 0 : int.MaxValue;
					if (h > 0f) { land += cell * cell; if (h < 1.5f) beach += cell * cell; queue.Enqueue(i * nz + j); }
				}
			int steps = Mathf.CeilToInt(reach / cell);
			while (queue.Count > 0)
			{
				int c = queue.Dequeue(), ci = c / nz, cj = c % nz;
				if (dist[ci, cj] >= steps) continue;
				foreach (int[] o in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
				{
					int a = ci + o[0], b = cj + o[1];
					if (a < 0 || b < 0 || a >= nx || b >= nz || dist[a, b] <= dist[ci, cj] + 1) continue;
					dist[a, b] = dist[ci, cj] + 1;
					queue.Enqueue(a * nz + b);
				}
			}
			for (int i = 0; i < nx; i++)
				for (int j = 0; j < nz; j++)
					if (heights[i, j] <= 0f && heights[i, j] > -40f && dist[i, j] <= steps) floor += cell * cell;
			Func<Vector3, bool> nearLand = p =>
			{
				int i = Mathf.Clamp(Mathf.FloorToInt((p.x - ground.transform.position.x) / cell), 0, nx - 1), j = Mathf.Clamp(Mathf.FloorToInt((p.z - ground.transform.position.z) / cell), 0, nz - 1);
				return dist[i, j] <= steps;
			};
			int farOut = 0;
			bool small = Mathf.Sqrt(land / Mathf.PI) < (RaftIslands.SmallRadius + RaftIslands.LargeRadius) / 2f;
			LandStyle raft = RaftLand.For(style, small);
			var landKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for (int st = 0; st < TerrainPainter.Styles.Length; st++) foreach (LandThing t in RaftLand.For(st).Things) if (!landKind.ContainsKey(t.Name)) landKind[t.Name] = t.Category;
			foreach (LandThing t in raft.Things) landKind[t.Name] = t.Category;
			string[] landCats = { IslandGenerator.CatTrees, IslandGenerator.CatBushes, IslandGenerator.CatRocks, IslandGenerator.CatHarvest, IslandGenerator.CatBeach };
			string[] seaCats = { IslandGenerator.CatWater, IslandGenerator.CatSeaRocks, IslandGenerator.CatSeaFinds, IslandGenerator.CatSunken };
			var onLand = landCats.ToDictionary(c => c, c => 0);
			// (the kinds behind each number: "Radio Tower Remade: 124 harvestables" needed them named)
			var kinds = new Dictionary<string, Dictionary<string, int>>();
			Action<string, string> count = (where, n) =>
			{
				Dictionary<string, int> k;
				if (!kinds.TryGetValue(where, out k)) kinds[where] = k = new Dictionary<string, int>();
				k[n] = k.ContainsKey(n) ? k[n] + 1 : 1;
			};
			Func<string, string> top = where => kinds.ContainsKey(where) ? "  [" + string.Join(", ", kinds[where].OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key + " " + kv.Value).ToArray()) + "]" : "";
			var underWater = seaCats.ToDictionary(c => c, c => 0);
			var finds = new SortedDictionary<string, int>();
			// (further out than the zone: Raft's copper lies 50 m out as a rule, up to 135 m - Tide Farm's at 60-84 m was "missing")
			var findsOut = new SortedDictionary<string, int>();
			var findsDry = new SortedDictionary<string, int>();
			var dryAt = new List<string>();
			Vector2 mid = EditorLandCentre();
			int vines = 0, beachThings = 0, other = 0;
			foreach (EditorGameObject e in PlacedEditorObjects())
			{
				string n = e.GameObjectName ?? "";
				Vector3 p = e.transform.position;
				float h = ground.SampleHeight(p) + baseY - sea, above = p.y - sea;
				string seaCat = RaftUnderwater.CategoryOf(n), landCat;
				if (above < -0.3f && h < 0f)
				{
					if (!nearLand(p))
					{
						farOut++;
						if (seaCat == IslandGenerator.CatSeaFinds) { string kf = FindKind(n); findsOut[kf] = findsOut.ContainsKey(kf) ? findsOut[kf] + 1 : 1; }
						continue;
					}
					if (seaCat != null) { underWater[seaCat]++; count("sea " + seaCat, n); } else other++;
					if (seaCat == IslandGenerator.CatSeaFinds) { string k = FindKind(n); finds[k] = finds.ContainsKey(k) ? finds[k] + 1 : 1; }
					if (Regex.IsMatch(n, @"^(SeaVine3|SeaVine3_klump|[Ss]eavine_tongue|Pillar_\d+)$")) vines++;
				}
				else if (landKind.TryGetValue(n, out landCat))
				{
					onLand[landCat]++;
					count("land " + landCat, n);
					if (h < 1.5f && (landCat == IslandGenerator.CatBeach || landCat == IslandGenerator.CatRocks)) beachThings++;
				}
				else if (seaCat == IslandGenerator.CatSeaFinds && h > 0.3f && Regex.IsMatch(n, @"Iron|Copper|Scrap|GiantClam|SilverAlgae"))
				{
					// (Raft keeps its ores, scrap, clams and algae under water)
					string k = FindKind(n);
					findsDry[k] = findsDry.ContainsKey(k) ? findsDry[k] + 1 : 1;
					if (dryAt.Count < 6) dryAt.Add(k + " at " + (p.x - mid.x).ToString("F1") + " " + (p.z - mid.y).ToString("F1") + " h=" + above.ToString("F1") + " (ground " + h.ToString("F1") + ")");
				}
				else other++;
			}
			bool ok = true;
			Func<float, float, string> judge = (mine, theirs) => theirs <= 0.01f ? (mine > 0.5f ? "  (Raft has none)" : "") : mine > theirs * 1.6f ? "  DENSE" : mine < theirs / 1.6f ? "  THIN" : "";
			Log("  " + TerrainPainter.StyleName(style) + (small ? " (small)" : " (big)") + ": land " + land.ToString("F0") + " m2 (beach strip " + beach.ToString("F0") + "), sea floor 0-40 m within " + reach.ToString("F0") + " m of it " + floor.ToString("F0") + " m2 (" + farOut + " things further out)");
			float raftArea = Mathf.Max(1f, raft.Area.Sum());
			foreach (string c in landCats)
			{
				float mine = land > 1f ? onLand[c] * 1000f / land : 0f, theirs = raft.CountOf(c) * 1000f / raftArea;
				Log("  land " + c + ": " + onLand[c] + " = " + mine.ToString("F1") + " per 1000 m2 (Raft " + theirs.ToString("F1") + ")" + judge(mine, theirs) + top("land " + c));
			}
			Log("  on the beach strip (rocks and beach things): " + beachThings + " = " + (beach > 1f ? beachThings * 1000f / beach : 0f).ToString("F1") + " per 1000 m2");
			foreach (string c in seaCats)
			{
				float mine = floor > 1f ? underWater[c] * 1000f / floor : 0f, theirs = RaftUnderwater.DensityOf(style, c);
				Log("  under water " + c + ": " + underWater[c] + " = " + mine.ToString("F1") + " per 1000 m2 (Raft " + theirs.ToString("F1") + ")" + judge(mine, theirs) + top("sea " + c));
			}
			// (Raft's: its vine and kelp kinds per 1000 m2 of sea floor 0-40 m deep)
			SeaStyle raftSea = RaftUnderwater.For(style);
			float raftFloor = 0f, raftVines = 0f;
			for (int band = 0; band < 5; band++)
			{
				raftFloor += raftSea.Area[band];
				raftVines += raftSea.Things.Where(t => Regex.IsMatch(t.Name, @"^(SeaVine3|SeaVine3_klump|[Ss]eavine_tongue|Pillar_\d+)$")).Sum(t => t.Density[band]) * raftSea.Area[band];
			}
			float myVines = floor > 1f ? vines * 1000f / floor : 0f, theirVines = raftFloor > 0f ? raftVines * 1000f / raftFloor : 0f;
			Log("  sea vines and kelp: " + vines + " = " + myVines.ToString("F1") + " per 1000 m2 (Raft " + theirVines.ToString("F1") + ")" + judge(myVines, theirVines));
			Log("  finds under water: " + (finds.Count > 0 ? string.Join(", ", finds.Select(kv => kv.Key + " " + kv.Value).ToArray()) : "none"));
			string[] raftFinds = { "stone", "clay", "sand", "scrap", "metal ore", "copper ore" };
			if (findsOut.Count > 0) Log("  finds further out (more than " + reach.ToString("F0") + " m from the land): " + string.Join(", ", findsOut.Select(kv => kv.Key + " " + kv.Value).ToArray()));
			string[] missing = raftFinds.Where(k => !finds.ContainsKey(k) && !findsOut.ContainsKey(k)).ToArray();
			if (floor > 2000f && missing.Length > 0) Log("  missing under water (Raft has them): " + string.Join(", ", missing));
			if (findsDry.Count > 0) { ok = false; Log("  Raft's sea finds on dry land: " + string.Join(", ", findsDry.Select(kv => kv.Key + " " + kv.Value).ToArray()) + " - " + string.Join("; ", dryAt.ToArray())); }
			Log("  other objects (the island's own): " + other);
			// (Raft's big islands grow their bamboo thick within 20 m of the water, under 4 m up: how far from the water - any,
			// and the open sea - the island's bamboo stands; the big tropical islands' wide beaches were fields of it, the review
			// 2026-10-03)
			{
				var fromAny = new int[nx, nz]; var fromSea = new int[nx, nz];
				var qa = new Queue<int>(); var qs = new Queue<int>();
				for (int i = 0; i < nx; i++)
					for (int j = 0; j < nz; j++)
					{
						fromAny[i, j] = heights[i, j] <= 0f ? 0 : int.MaxValue;
						fromSea[i, j] = int.MaxValue;
						if (heights[i, j] <= 0f) qa.Enqueue(i * nz + j);
						if (heights[i, j] <= 0f && (i == 0 || j == 0 || i == nx - 1 || j == nz - 1)) { fromSea[i, j] = 0; qs.Enqueue(i * nz + j); }
					}
				int[][] steps4 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
				// (the open sea: all the water reached through water from the terrain's edge, at 0 - the land is measured from its
				// shore, not from the terrain's edge)
				var open = new Queue<int>(qs);
				while (open.Count > 0)
				{
					int c = open.Dequeue(), ci = c / nz, cj = c % nz;
					foreach (int[] o in steps4)
					{
						int a = ci + o[0], b = cj + o[1];
						if (a < 0 || b < 0 || a >= nx || b >= nz || heights[a, b] > 0f || fromSea[a, b] == 0) continue;
						fromSea[a, b] = 0;
						open.Enqueue(a * nz + b);
						qs.Enqueue(a * nz + b);
					}
				}
				Action<int[,], Queue<int>, bool> spread = (d, q, waterFirst) =>
				{
					while (q.Count > 0)
					{
						int c = q.Dequeue(), ci = c / nz, cj = c % nz;
						foreach (int[] o in steps4)
						{
							int a = ci + o[0], b = cj + o[1];
							if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
							// (from the open sea over the land: a lagoon or a lake in it isn't the sea)
							if (waterFirst && heights[ci, cj] > 0f && heights[a, b] <= 0f) continue;
							if (d[a, b] <= d[ci, cj] + 1) continue;
							d[a, b] = d[ci, cj] + 1;
							q.Enqueue(a * nz + b);
						}
					}
				};
				spread(fromAny, qa, false);
				spread(fromSea, qs, true);
				var bins = new[] { 3f, 8f, 20f, 50f, float.PositiveInfinity }; // (the last: also bamboo no water reaches - by a lagoon the open sea doesn't)
				var anyHist = new int[5]; var seaHist = new int[5];
				foreach (EditorGameObject e in PlacedEditorObjects())
				{
					if (!(e.GameObjectName ?? "").StartsWith("Bamboo")) continue;
					Vector3 p = e.transform.position;
					int i = Mathf.Clamp(Mathf.FloorToInt((p.x - ground.transform.position.x) / cell), 0, nx - 1), j = Mathf.Clamp(Mathf.FloorToInt((p.z - ground.transform.position.z) / cell), 0, nz - 1);
					float da = fromAny[i, j] == int.MaxValue ? 1e9f : fromAny[i, j] * cell, ds = fromSea[i, j] == int.MaxValue ? 1e9f : fromSea[i, j] * cell;
					anyHist[Array.FindIndex(bins, x => da < x)]++;
					seaHist[Array.FindIndex(bins, x => ds < x)]++;
				}
				if (anyHist.Sum() > 0)
					Log("  bamboo by metres from water (0-3, 3-8, 8-20, 20-50, 50+): any water " + string.Join("/", anyHist.Select(v => v.ToString()).ToArray()) + ", the open sea " + string.Join("/", seaHist.Select(v => v.ToString()).ToArray()) + " (Raft's big islands: thick within 20 m of the water, under 4 m up)");
			}
			if (ok) Log("PASS: island density"); else Fail("island density");
		}

		/// <summary>The kind of one of Raft's sea finds, by its name.</summary>
		static string FindKind(string n)
		{
			if (n.Contains("Iron")) return "metal ore";
			if (n.Contains("Copper")) return "copper ore";
			if (n.Contains("Clay")) return "clay";
			if (n.Contains("Sand")) return "sand";
			if (n.Contains("Scrap")) return "scrap";
			if (n.Contains("GiantClam")) return "giant clam";
			if (n.Contains("SilverAlgae")) return "silver algae";
			if (n.Contains("Rock")) return "stone";
			return n;
		}

		[ConsoleCommand(name: "CIGenLikeRaft", docs: "Dev, anywhere: the generator's Like Raft (the Nature and Life under water quick buttons) against Raft's own islands, kind by kind: trees, bushes, rocks, harvestables and beach things per 1000 m² of land on tropical islands the size of Raft's small ones and of its big ones (raft_land.txt), and corals, rocks, finds and sunken things per 1000 m² of sea floor 0-40 m deep (raft_underwater.txt) - each within a third of Raft's; and the slider values that would hit Raft's. CIGenLikeRaft [trees bushes rocks harvest beach]: other land values to try")]
		public static void GenLikeRaftCommand(string[] args)
		{
			float[] values = args != null && args.Length == 5 ? args.Select(a => float.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray() : null;
			StartTest(GenLikeRaftRoutine(values));
		}

		static IEnumerator GenLikeRaftRoutine(float[] values)
		{
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			string[] cats = { IslandGenerator.CatTrees, IslandGenerator.CatBushes, IslandGenerator.CatRocks, IslandGenerator.CatHarvest, IslandGenerator.CatBeach };
			string[] groups = { "small", "big" };
			// Raft's: the land of its small and of its big tropical islands, pooled (raft_land.txt)
			byte[] bytes = RaftIslands.ModFile(RaftLand.FileName);
			if (bytes == null) { Fail("no " + RaftLand.FileName); yield break; }
			var groupOf = new Dictionary<string, string>();
			var raftArea = new Dictionary<string, float> { { "small", 0f }, { "big", 0f } };
			var raftCount = groups.ToDictionary(g => g, g => cats.ToDictionary(c => c, c => 0f));
			var raftNames = new Dictionary<string, float>();
			var genNames = new Dictionary<string, float>();
			foreach (string raw in Encoding.UTF8.GetString(bytes).Split('\n'))
			{
				string[] f = raw.TrimEnd('\r').Split('\t');
				if (f[0] == "land" && f.Length >= 5 && f[2] == "Tropical")
				{
					string g = f[1].StartsWith("Small island") ? "small" : f[1].StartsWith("Big island") || f[1] == "Big" ? "big" : null;
					if (g == null) continue;
					groupOf[f[1]] = g;
					raftArea[g] += f[4].Split(' ').Where(p => p.IndexOf(':') > 0).Sum(p => float.Parse(p.Substring(p.IndexOf(':') + 1), inv));
				}
				else if (f[0] == "lobj" && f.Length >= 5 && groupOf.ContainsKey(f[1]) && cats.Contains(f[3]))
				{
					raftCount[groupOf[f[1]]][f[3]] += float.Parse(f[4], inv);
					string key = groupOf[f[1]] + "/" + f[3] + "/" + f[2];
					raftNames[key] = (raftNames.ContainsKey(key) ? raftNames[key] : 0f) + float.Parse(f[4], inv);
				}
			}

			// The generator's, with the Like Raft quick buttons (or the land values given), on islands as big and high as Raft's
			Vector3 size = IslandGenerator.BuildArea;
			int res = IslandGenerator.BuildResolution;
			float step = size.x / (res - 1);
			var genArea = new Dictionary<string, float> { { "small", 0f }, { "big", 0f } };
			var genCount = groups.ToDictionary(g => g, g => cats.ToDictionary(c => c, c => 0f));
			string[] seaCats = { IslandGenerator.CatWater, IslandGenerator.CatSeaRocks, IslandGenerator.CatSeaFinds, IslandGenerator.CatSunken };
			float seaArea = 0f;
			var seaCount = seaCats.ToDictionary(c => c, c => 0f);
			float[] used = null;
			foreach (string g in groups)
			{
				int seeds = g == "small" ? 8 : 3;
				for (int seed = 1; seed <= seeds; seed++)
				{
					var s = new IslandGenSettings { Seed = 900 + seed, Style = TerrainPainter.Tropical, Radius = g == "small" ? RaftIslands.SmallRadius : RaftIslands.LargeRadius, Height = g == "small" ? RaftIslands.SmallTop : RaftIslands.LargeTop, Hostiles = 0, Friendly = 0, SeaLife = 0, Loot = 0 };
					IslandGenerator.NatureLikeRaft(s);
					IslandGenerator.SeaLikeRaft(s);
					if (values != null) { s.Trees = values[0]; s.Bushes = values[1]; s.Rocks = values[2]; s.Harvest = values[3]; s.BeachThings = values[4]; }
					used = cats.Select(c => IslandGenerator.AmountOf(s, c)).ToArray();
					float sea = s.WaterLevel;
					float[,] m = IslandGenerator.HeightsMetres(s, size, res);
					List<IslandObject> list = IslandGenerator.PlanAll(s, m, size);
					for (int z = 0; z < res; z++)
						for (int x = 0; x < res; x++)
						{
							float d = sea - m[z, x];
							if (d < 0f) genArea[g] += step * step;
							else if (d < 40f) seaArea += step * step;
						}
					foreach (IslandObject o in list)
					{
						float d = sea - IslandGenerator.SampleHeights(m, res, step, o.Position.x, o.Position.z);
						if (d < 0f)
						{
							string k = LandKindOf(o.Name);
							if (!genCount[g].ContainsKey(k)) continue;
							genCount[g][k]++;
							string key = g + "/" + k + "/" + o.Name;
							genNames[key] = (genNames.ContainsKey(key) ? genNames[key] : 0f) + 1f;
						}
						else if (d < 40f) { string k = RaftUnderwater.CategoryOf(o.Name); if (k != null) seaCount[k]++; }
					}
					yield return null;
				}
			}
			for (int i = 0; i < cats.Length; i++)
			{
				var line = new StringBuilder("  " + IslandGenerator.CategoryLabel(cats[i]) + " (slider " + used[i].ToString("F2", inv) + ")");
				foreach (string g in groups)
				{
					float raft = raftCount[g][cats[i]] * 1000f / Mathf.Max(1f, raftArea[g]), gen = genCount[g][cats[i]] * 1000f / Mathf.Max(1f, genArea[g]);
					float ratio = raft > 0.05f ? gen / raft : gen > 0.05f ? 99f : 1f;
					// (judged where Raft has enough of the kind to go by: a handful of logs on its small islands isn't a density)
					bool judged = raftCount[g][cats[i]] >= 20f;
					bool good = !judged || ratio >= 0.67f && ratio <= 1.5f;
					if (!good) ok = false;
					line.Append("; " + g + " islands: Raft " + raft.ToString("F1", inv) + ", here " + gen.ToString("F1", inv) + " per 1000 m² (x" + ratio.ToString("F2", inv) + (!judged ? ", too few of Raft's to judge" : good ? "" : " - off") + ", slider for Raft's: " +
						(gen > 0.05f ? Mathf.Clamp01(used[i] * Mathf.Sqrt(raft / gen)).ToString("F2", inv) : "?") + ")");
				}
				Log(line.ToString());
				foreach (string g in groups)
				{
					Func<Dictionary<string, float>, float, string> top = (names, landArea) => string.Join(", ", names.Where(kv => kv.Key.StartsWith(g + "/" + cats[i] + "/"))
						.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key.Substring(kv.Key.LastIndexOf('/') + 1) + " " + (kv.Value * 1000f / Mathf.Max(1f, landArea)).ToString("F1", inv)).ToArray());
					Log("      " + g + ": Raft's " + top(raftNames, raftArea[g]) + " | here " + top(genNames, genArea[g]));
				}
			}
			SeaStyle raftSea = RaftUnderwater.For(TerrainPainter.Tropical);
			float raftSeaArea = raftSea.Area.Take(5).Sum();
			foreach (string c in seaCats)
			{
				float raft = raftSea.Of(c).Sum(t => Enumerable.Range(0, 5).Sum(b => t.Density[b] * raftSea.Area[b])) * 1000f / Mathf.Max(1f, raftSeaArea);
				float gen = seaCount[c] * 1000f / Mathf.Max(1f, seaArea);
				float ratio = raft > 0.05f ? gen / raft : 1f;
				bool good = ratio >= 0.67f && ratio <= 1.5f;
				if (!good) ok = false;
				Log("  " + IslandGenerator.CategoryLabel(c) + " 0-40 m down: Raft " + raft.ToString("F1", inv) + ", here " + gen.ToString("F1", inv) + " per 1000 m² (x" + ratio.ToString("F2", inv) + (good ? "" : " - off") + ")");
			}
			Log("  Raft's land: small islands " + raftArea["small"].ToString("F0", inv) + " m², big " + raftArea["big"].ToString("F0", inv) + " m²; generated: small " + genArea["small"].ToString("F0", inv) + " m², big " + genArea["big"].ToString("F0", inv) + " m²");

			// The quick button per style
			for (int st = 0; st < TerrainPainter.Styles.Length; st++)
			{
				var x = new IslandGenSettings { Style = st };
				IslandGenerator.NatureLikeRaft(x);
				Log("  Like Raft, " + TerrainPainter.StyleName(st) + ": Trees=" + x.Trees.ToString("F2", inv) + " Bushes=" + x.Bushes.ToString("F2", inv) + " Rocks=" + x.Rocks.ToString("F2", inv) +
					" Harvest=" + x.Harvest.ToString("F2", inv) + " BeachThings=" + x.BeachThings.ToString("F2", inv));
			}

			// Randomize existing: a variation of some of Raft's own islands is as thick with things as the island itself
			foreach (string label in new[] { "Small island 3", "Small island 6", "Small island 10", "Big island OG", "Big", "Balboa Small 2", "Caravan Island Real Deal" })
			{
				RaftIsland isl = RaftIslands.All.FirstOrDefault(r => r.Label == label);
				float[] own = RaftLand.IslandDensities(label);
				if (isl == null || own == null) { Log("  " + label + ": not measured"); continue; }
				IslandGenSettings v = RaftIslands.VariationOf(isl, new IslandGenSettings { Seed = 77, Hostiles = 0, Friendly = 0, SeaLife = 0, Loot = 0 });
				float sea = v.WaterLevel;
				float[,] m = IslandGenerator.HeightsMetres(v, size, res);
				List<IslandObject> list = IslandGenerator.PlanAll(v, m, size);
				float land = 0f;
				for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) if (m[z, x] > sea) land += step * step;
				string[] four = { IslandGenerator.CatTrees, IslandGenerator.CatBushes, IslandGenerator.CatRocks, IslandGenerator.CatHarvest };
				var n = new float[4];
				foreach (IslandObject o in list)
					if (IslandGenerator.SampleHeights(m, res, step, o.Position.x, o.Position.z) > sea)
					{
						int k = Array.IndexOf(four, LandKindOf(o.Name));
						if (k >= 0) n[k]++;
					}
				var parts = new List<string>();
				bool fits = true;
				for (int k = 0; k < 4; k++)
				{
					float here = n[k] * 1000f / Mathf.Max(1f, land), r = own[k] > 0.05f ? here / own[k] : here > 0.05f ? 99f : 1f;
					// (judged where the island has enough of the kind to go by)
					bool judged = own[k] * isl.Area / 1000f >= 20f;
					if (judged && (r < 0.45f || r > 2.2f)) fits = false;
					parts.Add(IslandGenerator.CategoryLabel(four[k]) + " " + own[k].ToString("F1", inv) + " -> " + here.ToString("F1", inv) + (judged ? " (x" + r.ToString("F2", inv) + ")" : ""));
				}
				if (!fits) ok = false;
				Log("  " + (fits ? "" : "OFF: ") + "a variation of " + label + " (" + TerrainPainter.StyleName(v.Style) + ", " + isl.Area.ToString("F0", inv) + " m² of land, here " + land.ToString("F0", inv) + "): " + string.Join(", ", parts.ToArray()) + " per 1000 m²");
				yield return null;
			}
			if (ok) Log("PASS: the generator's Like Raft is like Raft's islands"); else Fail("the generator's Like Raft is like Raft's islands");
		}
	}
}
