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
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// The generator's dev commands: CIMeasureIslands measures Raft's own islands (the numbers behind the size and
	/// height presets and the "Randomize existing" tab).
	/// </summary>
	public static partial class DevTests
	{
		#region Measuring Raft's islands

		/// <summary>The layer the measured copies are put on (the picture's camera sees only it).</summary>
		const int MeasureLayer = 31;

		[ConsoleCommand(name: "CIMeasureIslands", docs: "Dev, editor: measures each of Raft's island scenes (land size above the sea, highest point, shallow water, peaks, slopes, coast, ground style, objects per kind), renders a picture and samples its ground heights: raft_islands.txt, island_thumbs\\ and island_heights\\ in Mods\\DynamicIslands. CIMeasureIslands [part of a scene name]")]
		public static void MeasureIslandsCommand(string[] args)
		{
			string filter = args != null && args.Length > 0 ? string.Join(" ", args) : null;
			StartTest(MeasureIslandsRoutine(filter));
		}

		static readonly Regex TreeWord = new Regex(@"(?i)(tree|palm|pine|birch|bamboo)"), NotTreeWord = new Regex(@"(?i)(log|stump|leaf|leaves|branch|root)");
		static readonly Regex BushWord = new Regex(@"(?i)(bush|fern|monstera|grass|flower|plant|banana|shrub|reed|cact)");
		static readonly Regex RockWord = new Regex(@"(?i)(rock|boulder|stone|cliff|stalag|pebble)");
		static readonly Regex SeaWord = new Regex(@"(?i)(coral|seavine|kelp|seaweed|anemone|urchin|sea_?grass)");
		static readonly Regex LootWord = new Regex(@"(?i)(chest|crate|barrel|storage|loot|locker)");
		/// <summary>
		/// LM7: colliders of buildings and other built things (walls, roofs, stairs, decks, towers, huts, pipes, tunnels...):
		/// on the built places (Radio Tower, Caravan Town, Tangaroa, Varuna Point, Utopia, Temperance) they were read as
		/// ground, so a variation had rocky towers where the buildings stood. A sea or ocean floor is still ground.
		/// </summary>
		static readonly Regex BuildingWord = new Regex(@"(?i)(wall|roof|(?<!sea|ocean|sea_|ocean_)floor|stair|door|window|beam|girder|truss|fence|railing|balcon|deck|scaffold|platform|tower|building|house|(^|_)hut|igloo|cabin|container|pipe|tunnel|crane|antenna|bridge|dome|tank|apartment|skyscraper|ladder|plank|pier|dock|walkway|chimney|elevator|ramp|(^|_)kit_|_set_|^RT_|^Relay_|^VP_|^UT_|^TP_Selene|^TP_Moontown|^Tangaroa|^Caravan(Hub|Base|Barebones))");
		/// <summary>(a name with one of these words is ground even with a place's prefix: VP_Cliff, TP_IcePillar, UT_Rock...)</summary>
		static readonly Regex NaturalWord = new Regex(@"(?i)(rock(?!et)|cliff|mountain|terrain|sand|stone|boulder|stalag|(^|_)ice|snow|coral|landmass|ground(?!pillar))");
		/// <summary>Colliders that aren't ground: plants (tree trunks, bushes), water, and harvestable pickups.</summary>
		static readonly Regex NotGroundWord = new Regex(@"(?i)(tree|palm|pine|birch|bamboo|bush|fern|monstera|grass|flower|plant|banana|leaf|leaves|coral|seavine|kelp|seaweed|water|ocean|^Pickup_|QuestItem)");

		/// <summary>Which kind of RaftIslands.Kinds a placeable or harvestable object is.</summary>
		static string KindOfObject(string name)
		{
			if (PlaceableCatalog.IsHarvestableName(name))
				return name.Contains("Tree") ? RaftIslands.Trees : RaftIslands.Harvest;
			if (SeaWord.IsMatch(name)) return RaftIslands.Sea;
			if (TreeWord.IsMatch(name) && !NotTreeWord.IsMatch(name)) return RaftIslands.Trees;
			if (BushWord.IsMatch(name)) return RaftIslands.Bushes;
			if (RockWord.IsMatch(name)) return RaftIslands.Rocks;
			if (LootWord.IsMatch(name)) return RaftIslands.Loot;
			return RaftIslands.Props;
		}

		static IEnumerator MeasureIslandsRoutine(string filter)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			float t0 = Time.realtimeSinceStartup;
			List<string> scenes = PlaceableCatalog.LandmarkSceneNames().Where(s => filter == null || s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			Log("Measuring " + scenes.Count + " of Raft's island scenes" + (filter != null ? " matching '" + filter + "'" : ""));
			string thumbDir = Path.Combine(DynamicIslands.assetpath, RaftIslands.ThumbFolder), heightDir = Path.Combine(DynamicIslands.assetpath, RaftIslands.HeightFolder);
			Directory.CreateDirectory(thumbDir);
			Directory.CreateDirectory(heightDir);

			// A scene of its own, with its own physics: the copies answer raycasts there and nowhere else
			Scene lab = SceneManager.CreateScene("CI_MeasureLab", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
			var results = new List<RaftIsland>();
			var skipped = new List<string>();
			try
			{
				for (int i = 0; i < scenes.Count; i++)
				{
					if (!DynamicIslands.InEditor()) { Fail("left the editor while measuring"); yield break; }
					if (i > 0 && i % 6 == 0) yield return Resources.UnloadUnusedAssets();
					RaftIsland found = null;
					string why = "not loaded";
					float s0 = Time.realtimeSinceStartup;
					yield return PlaceableCatalog.VisitScene(scenes[i], s => MeasureScene(s, lab, r => found = r, w => why = w));
					if (found != null)
					{
						results.Add(found);
						Log(string.Format("  {0} ({1}): land {2:F0} x {3:F0} m ({4:F0} m², radius {5:F0} m), top {6:F1} m, shallow water to {7:F0} m, {8} peak(s), slope {9:F0}°, cliffs {10:P0}, beach {11:P0}, coast {12:F2}, {13}; {14} [{15:F1} s]",
							found.Label, found.Kind, found.Length, found.Width, found.Area, found.Radius, found.Top, found.Shelf, found.Peaks, found.Slope, found.Cliffs, found.Beach, found.Compact, found.Style,
							string.Join(", ", RaftIslands.Kinds.Where(k => found.Count(k) > 0).Select(k => found.Count(k) + " " + k).ToArray()), Time.realtimeSinceStartup - s0));
					}
					else { skipped.Add(PlaceableCatalog.SceneLabel(scenes[i]) + " (" + why + ")"); Log("  " + scenes[i] + ": " + why); }
				}
			}
			finally
			{
				if (lab.IsValid()) SceneManager.UnloadSceneAsync(lab);
			}

			// One line per island; measuring only some keeps the others' lines
			string path = Path.Combine(DynamicIslands.assetpath, RaftIslands.FileName);
			var lines = new Dictionary<string, string>();
			if (filter != null && File.Exists(path))
				foreach (string line in File.ReadAllLines(path))
				{
					Match m = Regex.Match(line, @"^scene=([^\t]+)");
					if (m.Success) lines[m.Groups[1].Value] = line;
				}
			foreach (RaftIsland r in results) lines[r.Scene] = RaftIslands.Format(r);
			var order = PlaceableCatalog.LandmarkSceneNames();
			var text = new StringBuilder();
			text.Append("# CustomIslands Raft islands ").Append(RaftIslands.FileVersion).Append(" raft=").Append(Application.version).Append('\n');
			text.Append("# Raft's own islands, measured by CIMeasureIslands: land above the sea (length/width along its long axis, m; angle of that axis; area m²; radius of a round island that big),\n");
			text.Append("# top = highest ground above the sea, shelf = radius of land + water down to 8 m deep, slope = mean degrees, cliffs = steep share of the coast, beach = share of land under 3 m,\n");
			text.Append("# compact = 1 for a round coast (lower = ragged), then objects per kind. Pictures in island_thumbs, ground heights in island_heights.\n");
			foreach (var kv in lines.OrderBy(kv => order.IndexOf(kv.Key))) text.Append(kv.Value).Append('\n');
			File.WriteAllText(path, text.ToString());
			RaftIslands.Reload();
			Log("Measured " + results.Count + " island(s) in " + (Time.realtimeSinceStartup - t0).ToString("F0") + " s, written to " + Path.GetFullPath(path) +
				(skipped.Count > 0 ? "; no land in: " + string.Join(", ", skipped.ToArray()) : ""));
			if (results.Count > 0) Log("PASS: measured Raft's islands"); else Fail("measured no island");
		}

		/// <summary>Measures one switched-off Raft scene; done() gets the result (not called when it has no land).</summary>
		static IEnumerator MeasureScene(Scene src, Scene lab, Action<RaftIsland> done, Action<string> noLand) { return MeasureScene(src, lab, done, noLand, null); }

		/// <summary>(with <paramref name="underwater"/>: after the ground grid, that is called instead of writing the heights and picture)</summary>
		static IEnumerator MeasureScene(Scene src, Scene lab, Action<RaftIsland> done, Action<string> noLand, Action<RaftIsland, float[,], int, float, Vector2> underwater)
		{
			var island = new RaftIsland { Scene = src.name, Label = RaftIslands.LabelOf(src.name) };
			foreach (string k in RaftIslands.Kinds) island.Counts[k] = 0;

			// 1. Objects by kind, read in the switched-off scene
			foreach (var p in PlaceableCatalog.PlaceablesOf(src)) island.Counts[KindOfObject(p.Key)]++;
			GameObject[] roots = src.GetRootGameObjects();
			foreach (Transform t in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
			{
				string cn = PlaceableCatalog.CleanName(t.name);
				if (PlaceableCatalog.IsHarvestableName(cn)) island.Counts[KindOfObject(cn)]++;
			}
			island.Counts[RaftIslands.Spawners] = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Count(m => m != null && m.GetType().Name.IndexOf("Spawner", StringComparison.OrdinalIgnoreCase) >= 0);

			// 2. A copy without scripts, sounds, effects or physics bodies in the lab scene: its colliders are the ground
			var holder = new GameObject("CI_MeasureCopy");
			holder.SetActive(false);
			SceneManager.MoveGameObjectToScene(holder, lab);
			var extra = new List<GameObject>();
			try
			{
				foreach (GameObject root in roots)
				{
					GameObject copy = UnityEngine.Object.Instantiate(root, holder.transform);
					copy.name = root.name;
					copy.SetActive(true);
				}
				PlaceableCatalog.StripScripts(holder);
				foreach (Joint j in holder.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
				foreach (ParticleSystem ps in holder.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(ps);
				foreach (Component c in holder.GetComponentsInChildren<Component>(true).Where(c => c is ParticleSystemRenderer || c is AudioSource || c is Rigidbody || c is Animator || c is Animation ||
					c is Light || c is Camera || c is ReflectionProbe || c is AudioListener || c is UnityEngine.AI.NavMeshAgent || c is UnityEngine.AI.NavMeshObstacle || c is Cloth || c is TrailRenderer || c is LineRenderer || c is WindZone).ToList())
					if (c != null) UnityEngine.Object.DestroyImmediate(c);

				var ground = new HashSet<Collider>();
				int terrains = 0, meshes = 0, built = 0;
				foreach (Collider c in holder.GetComponentsInChildren<Collider>(true))
				{
					if (c.isTrigger || !c.enabled) continue;
					if (IsBuilt(c.transform, holder.transform)) { built++; continue; }
					if (!IsGround(c.transform, holder.transform)) continue;
					if (LayerMask.LayerToName(c.gameObject.layer).IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0) continue;
					ground.Add(c);
					if (c is TerrainCollider) terrains++; else meshes++;
				}
				// Ground textures: the style it looks like
				island.Style = StyleOfScene(holder, src.name);
				foreach (Transform t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = MeasureLayer;
				holder.SetActive(true);
				island.Kind = terrains > 0 && meshes > 0 ? "terrain+mesh" : terrains > 0 ? "terrain" : "mesh";
				if (ground.Count == 0) { noLand("no ground colliders"); yield break; }
				yield return null;

				Bounds b = new Bounds();
				bool any = false;
				foreach (Collider c in ground)
				{
					if (c == null || !c.gameObject.activeInHierarchy) continue;
					if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
				}
				if (!any) { noLand("no active ground"); yield break; }
				ground.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

				// 3. Ground heights on a grid (the sea is at y = 0 in Raft's scenes)
				float extent = Mathf.Min(1600f, Mathf.Max(b.size.x, b.size.z) + 40f);
				float cell = Mathf.Max(2f, extent / 400f);
				int n = Mathf.CeilToInt(extent / cell) + 1;
				Vector2 origin = new Vector2(b.center.x - (n - 1) * cell / 2f, b.center.z - (n - 1) * cell / 2f);
				var h = new float[n, n];
				PhysicsScene physics = lab.GetPhysicsScene();
				var hits = new RaycastHit[48];
				float top = b.max.y + 5f, reach = b.size.y + 20f;
				float lowest = float.MaxValue;
				for (int z = 0; z < n; z++)
					for (int x = 0; x < n; x++)
					{
						Vector3 from = new Vector3(origin.x + x * cell, top, origin.y + z * cell);
						int count = physics.Raycast(from, Vector3.down, hits, reach, 1 << MeasureLayer, QueryTriggerInteraction.Ignore);
						float best = float.NaN;
						for (int k = 0; k < count; k++)
							if (ground.Contains(hits[k].collider) && (float.IsNaN(best) || hits[k].point.y > best)) best = hits[k].point.y;
						h[z, x] = best;
						if (!float.IsNaN(best)) lowest = Mathf.Min(lowest, best);
					}
				// (no ground below: deep water)
				float deep = Mathf.Min(lowest, -30f);
				for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) if (float.IsNaN(h[z, x])) h[z, x] = deep;
				Log("    " + src.name + ": " + ground.Count + " ground colliders (" + terrains + " terrain; " + built + " of buildings left out), bounds " + b.min.ToString("F0") + " - " + b.max.ToString("F0") + ", grid " + n + "x" + n + " of " + cell.ToString("F1") + " m, heights " + lowest.ToString("F1") + " to " + h.Cast<float>().Max().ToString("F1"));

				if (!Analyse(island, h, n, cell, origin)) { noLand("nothing above the sea"); yield break; }
				if (underwater != null) { underwater(island, h, n, cell, origin); done(island); yield break; }

				// 4. Its ground heights around the land's middle (for "a variation of it")
				HeightField field = Resample(h, n, cell, origin, island);
				File.WriteAllBytes(Path.Combine(Path.Combine(DynamicIslands.assetpath, RaftIslands.HeightFolder), RaftIslands.SafeName(src.name) + ".bin"), RaftIslands.WriteHeights(field));

				// 5. Its picture, looking at the middle of its land
				double lx = 0, lz = 0; int lc = 0;
				for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) if (h[z, x] > 0f) { lx += x; lz += z; lc++; }
				yield return RenderIslandPicture(lab, island, extra, new Vector2(origin.x + (float)(lx / lc) * cell, origin.y + (float)(lz / lc) * cell));
				done(island);
			}
			finally
			{
				foreach (GameObject g in extra) if (g != null) UnityEngine.Object.DestroyImmediate(g);
				UnityEngine.Object.DestroyImmediate(holder);
			}
		}

		static bool IsGround(Transform t, Transform stop)
		{
			for (Transform p = t; p != null && p != stop; p = p.parent)
				if (NotGroundWord.IsMatch(PlaceableCatalog.CleanName(p.name))) return false;
			return !IsBuilt(t, stop);
		}

		/// <summary>LM7: a collider of something built (BuildingWord on it or a parent) - never a terrain.</summary>
		static bool IsBuilt(Transform t, Transform stop)
		{
			if (t.GetComponent<TerrainCollider>() != null) return false;
			for (Transform p = t; p != null && p != stop; p = p.parent)
			{
				string n = PlaceableCatalog.CleanName(p.name);
				if (BuildingWord.IsMatch(n) && !NaturalWord.IsMatch(n)) return true;
			}
			return false;
		}

		/// <summary>The generator style whose ground textures the scene's terrains (or ground materials) use most; by the scene's name if none match.</summary>
		static string StyleOfScene(GameObject holder, string scene)
		{
			var names = new HashSet<string>();
			foreach (Terrain t in holder.GetComponentsInChildren<Terrain>(true))
				if (t.terrainData != null && t.terrainData.terrainLayers != null)
					foreach (TerrainLayer l in t.terrainData.terrainLayers)
						if (l != null && l.diffuseTexture != null) names.Add(l.diffuseTexture.name);
			int best = -1, bestScore = 0;
			for (int s = 0; s < TerrainPainter.Styles.Length; s++)
			{
				int score = TerrainPainter.Styles[s].Textures.Distinct().Count(names.Contains);
				// (volcanic borrows desert and snow textures: only by name)
				if (s != TerrainPainter.Volcanic && score > bestScore) { best = s; bestScore = score; }
			}
			if (best < 0)
				best = scene.Contains("Temperance") ? TerrainPainter.Snowy : scene.Contains("Caravan") ? TerrainPainter.Desert : scene.Contains("Balboa") ? TerrainPainter.Forest : TerrainPainter.Tropical;
			return TerrainPainter.StyleName(best);
		}

		/// <summary>Land size, height, shallow water, long axis, slopes, coast, peaks from a height grid. False if nothing is above the sea.</summary>
		static bool Analyse(RaftIsland island, float[,] h, int n, float cell, Vector2 origin)
		{
			int land = 0, shallow = 0, low = 0;
			double sx = 0, sz = 0;
			float top = float.MinValue;
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					float v = h[z, x];
					if (v > -8f) shallow++;
					if (v <= 0f) continue;
					land++; sx += x; sz += z;
					if (v < 3f) low++;
					top = Mathf.Max(top, v);
				}
			float area = land * cell * cell;
			if (land < 10 || area < 100f) return false;
			island.Area = area;
			island.Radius = Mathf.Sqrt(area / Mathf.PI);
			island.Top = top;
			island.Shelf = Mathf.Sqrt(shallow * cell * cell / Mathf.PI);
			island.Beach = low / (float)land;

			// Long axis from the spread of the land (a filled ellipse has variance (length/2)² / 4 along an axis)
			double mx = sx / land, mz = sz / land, cxx = 0, czz = 0, cxz = 0;
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					if (h[z, x] <= 0f) continue;
					double dx = x - mx, dz = z - mz;
					cxx += dx * dx; czz += dz * dz; cxz += dx * dz;
				}
			cxx /= land; czz /= land; cxz /= land;
			double tr = cxx + czz, det = cxx * czz - cxz * cxz, disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
			double l1 = tr / 2 + disc, l2 = Math.Max(0, tr / 2 - disc);
			island.Length = (float)(4 * Math.Sqrt(l1)) * cell;
			island.Width = (float)(4 * Math.Sqrt(l2)) * cell;
			island.Angle = Mathf.Repeat((float)(0.5 * Math.Atan2(2 * cxz, cxx - czz)) * Mathf.Rad2Deg, 180f);

			// Slopes; the coast (land next to water): its length, and how much of it is steep
			double slopes = 0;
			int coast = 0, steepCoast = 0, edges = 0;
			for (int z = 1; z < n - 1; z++)
				for (int x = 1; x < n - 1; x++)
				{
					if (h[z, x] <= 0f) continue;
					float gx = (h[z, x + 1] - h[z, x - 1]) / (2f * cell), gz = (h[z + 1, x] - h[z - 1, x]) / (2f * cell);
					float slope = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
					slopes += slope;
					int water = (h[z, x + 1] <= 0f ? 1 : 0) + (h[z, x - 1] <= 0f ? 1 : 0) + (h[z + 1, x] <= 0f ? 1 : 0) + (h[z - 1, x] <= 0f ? 1 : 0);
					if (water == 0) continue;
					edges += water;
					coast++;
					// Steep: 4 m inland it is already 4 m higher (a beach rises much slower)
					float inland = 0f;
					int steps = Mathf.Max(1, Mathf.RoundToInt(4f / cell));
					for (int dz = -steps; dz <= steps; dz += steps)
						for (int dx = -steps; dx <= steps; dx += steps)
						{
							int xx = Mathf.Clamp(x + dx, 0, n - 1), zz = Mathf.Clamp(z + dz, 0, n - 1);
							inland = Mathf.Max(inland, h[zz, xx]);
						}
					if (inland > 4f) steepCoast++;
				}
			island.Slope = (float)(slopes / land);
			island.Cliffs = coast > 0 ? steepCoast / (float)coast : 0f;
			// (the perimeter of a round island counted along grid edges is 4/pi too long: 0.617 = a circle)
			float perimeter = edges * cell;
			island.Compact = perimeter > 0f ? Mathf.Clamp(4f * Mathf.PI * area / (perimeter * perimeter) / 0.617f, 0f, 1.2f) : 1f;
			island.Peaks = CountPeaks(h, n, cell, top, island.Radius);
			return true;
		}

		/// <summary>Hilltops: the highest point within a radius, clearly above the ring around it.</summary>
		static int CountPeaks(float[,] h, int n, float cell, float top, float landRadius)
		{
			int r = Mathf.Max(3, Mathf.RoundToInt(Mathf.Max(12f, landRadius * 0.22f) / cell));
			float minHeight = Mathf.Max(3f, top * 0.3f), prominence = Mathf.Max(2.5f, top * 0.15f);
			// A little smoothing first, so rocks and huts aren't peaks
			var s = new float[n, n];
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					float sum = 0; int c = 0;
					for (int dz = -2; dz <= 2; dz++)
						for (int dx = -2; dx <= 2; dx++)
						{
							int xx = x + dx, zz = z + dz;
							if (xx < 0 || zz < 0 || xx >= n || zz >= n) continue;
							sum += h[zz, xx]; c++;
						}
					s[z, x] = sum / c;
				}
			int peaks = 0;
			for (int z = r; z < n - r; z++)
				for (int x = r; x < n - r; x++)
				{
					float v = s[z, x];
					if (v < minHeight) continue;
					bool highest = true;
					float ring = 0f; int ringCount = 0;
					for (int dz = -r; dz <= r && highest; dz++)
						for (int dx = -r; dx <= r; dx++)
						{
							int d2 = dx * dx + dz * dz;
							if (d2 > r * r || d2 == 0) continue;
							float w = s[z + dz, x + dx];
							if (w > v || (w == v && (dz < 0 || (dz == 0 && dx < 0)))) { highest = false; break; }
							if (d2 >= (r - 1) * (r - 1)) { ring += w; ringCount++; }
						}
					if (highest && ringCount > 0 && v - ring / ringCount >= prominence) peaks++;
				}
			return Mathf.Max(1, peaks);
		}

		/// <summary>The measured grid around the land's middle, at most 193 samples a side (ground heights file).</summary>
		static HeightField Resample(float[,] h, int n, float cell, Vector2 origin, RaftIsland island)
		{
			// The land's middle, and how far its land and shallow water reach from it
			double sx = 0, sz = 0; int c = 0;
			for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) if (h[z, x] > 0f) { sx += x; sz += z; c++; }
			float mx = (float)(sx / c), mz = (float)(sz / c);
			float reach = 0f;
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
					if (h[z, x] > -8f) reach = Mathf.Max(reach, new Vector2(x - mx, z - mz).magnitude * cell);
			reach = Mathf.Min(reach + 30f, (n - 1) * cell / 2f + 30f);
			int m = Mathf.Min(193, Mathf.CeilToInt(reach * 2f / cell) + 1);
			float step = reach * 2f / (m - 1);
			var f = new HeightField { Cell = step, Nx = m, Nz = m, H = new float[m, m] };
			for (int z = 0; z < m; z++)
				for (int x = 0; x < m; x++)
				{
					float gx = mx + (x - (m - 1) / 2f) * step / cell, gz = mz + (z - (m - 1) / 2f) * step / cell;
					gx = Mathf.Clamp(gx, 0f, n - 1.001f); gz = Mathf.Clamp(gz, 0f, n - 1.001f);
					int x0 = (int)gx, z0 = (int)gz;
					float tx = gx - x0, tz = gz - z0;
					f.H[z, x] = Mathf.Lerp(Mathf.Lerp(h[z0, x0], h[z0, x0 + 1], tx), Mathf.Lerp(h[z0 + 1, x0], h[z0 + 1, x0 + 1], tx), tz);
				}
			return f;
		}

		/// <summary>A picture of the measured copy from above the south-west, with a see-through sea: island_thumbs\&lt;scene&gt;.png.</summary>
		static IEnumerator RenderIslandPicture(Scene lab, RaftIsland island, List<GameObject> extra, Vector2 landMiddle)
		{
			// Its land and a rim of the water around it
			float reach = Mathf.Max(island.Length, island.Width) * 0.55f + Mathf.Clamp(island.Radius * 0.4f, 8f, 40f);
			Vector3 centre = new Vector3(landMiddle.x, Mathf.Max(1f, island.Top * 0.25f), landMiddle.y);

			var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
			extra.Add(water);
			SceneManager.MoveGameObjectToScene(water, lab);
			UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
			water.layer = MeasureLayer;
			water.transform.position = new Vector3(centre.x, 0.05f, centre.z);
			water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			water.transform.localScale = Vector3.one * Mathf.Max(4000f, reach * 40f); // (also over the island's deep rock foot)
			Shader sprite = Shader.Find("Sprites/Default");
			if (sprite != null) water.GetComponent<Renderer>().material = new Material(sprite) { color = new Color(0.09f, 0.36f, 0.56f, 0.8f) };

			var sun = new GameObject("CI_MeasureSun");
			extra.Add(sun);
			SceneManager.MoveGameObjectToScene(sun, lab);
			Light light = sun.AddComponent<Light>();
			light.type = LightType.Directional;
			light.cullingMask = 1 << MeasureLayer;
			light.intensity = 0.9f;
			light.shadows = LightShadows.None;
			sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

			var camGo = new GameObject("CI_MeasureCamera");
			extra.Add(camGo);
			SceneManager.MoveGameObjectToScene(camGo, lab);
			Camera cam = camGo.AddComponent<Camera>();
			cam.enabled = false;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = new Color(0.16f, 0.42f, 0.6f);
			cam.cullingMask = 1 << MeasureLayer;
			cam.fieldOfView = 30f;
			cam.nearClipPlane = 1f;
			cam.farClipPlane = reach * 12f + 500f;
			cam.transform.rotation = Quaternion.Euler(40f, 35f, 0f);
			cam.transform.position = centre - cam.transform.forward * (reach / Mathf.Sin(15f * Mathf.Deg2Rad) * 0.95f);

			const int W = 384, H = 240;
			var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 };
			bool fog = RenderSettings.fog;
			RenderSettings.fog = false;
			try
			{
				cam.targetTexture = rt;
				{ bool gfx = GfxShotBegin(); try { cam.Render(); } finally { GfxShotEnd(gfx); } } // (CIGfx low: the user's quality)
				RenderTexture previous = RenderTexture.active;
				RenderTexture.active = rt;
				var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
				tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
				tex.Apply();
				RenderTexture.active = previous;
				File.WriteAllBytes(Path.Combine(Path.Combine(DynamicIslands.assetpath, RaftIslands.ThumbFolder), RaftIslands.SafeName(island.Scene) + ".jpg"), tex.EncodeToJPG(88));
				UnityEngine.Object.Destroy(tex);
			}
			finally
			{
				RenderSettings.fog = fog;
				cam.targetTexture = null;
				rt.Release();
				UnityEngine.Object.Destroy(rt);
			}
			yield return null;
		}

		#endregion

		#region Can players reach it?

		[ConsoleCommand(name: "CIReachTest", docs: "Dev, editor: the generator's \"can players reach it\" line - a beach island is easy, cliffs all around need building, a low ledge is tricky, the plateau's ramp, sea stacks, flying and sunken ready-made islands; the same answer from the settings and from the generated terrain; the window shows it above the seed")]
		public static void ReachTestCommand()
		{
			StartTest(ReachTestRoutine());
		}

		static IEnumerator ReachTestRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			bool ok = true;
			var basic = new IslandGenSettings { Seed = 606, Radius = 70f, Height = 35f, BeachWidth = 0.6f, Cliffs = 0f };
			var cases = new List<KeyValuePair<string, IslandGenSettings>>
			{
				new KeyValuePair<string, IslandGenSettings>("beaches all round", basic.Copy()),
				new KeyValuePair<string, IslandGenSettings>("cliffs all round, 60 m high", Tweak(basic, x => { x.Cliffs = 1f; x.Height = 60f; x.BeachWidth = 0f; })),
				new KeyValuePair<string, IslandGenSettings>("half cliffs", Tweak(basic, x => x.Cliffs = 0.5f)),
				new KeyValuePair<string, IslandGenSettings>("plateau with a ramp", Tweak(basic, x => { x.Shape = IslandShapes.Plateau; x.Height = 25f; })),
				new KeyValuePair<string, IslandGenSettings>("sea stacks", Tweak(basic, x => { x.Shape = IslandShapes.Stacks; x.Height = 45f; })),
				new KeyValuePair<string, IslandGenSettings>("a small low island", Tweak(basic, x => { x.Radius = RaftIslands.SmallRadius; x.Height = 6f; })),
				new KeyValuePair<string, IslandGenSettings>("spires", Tweak(basic, x => { x.PeakShape = 1f; x.Height = 90f; x.Roughness = 1f; })),
			};
			var levels = new Dictionary<string, ReachResult>();
			foreach (var c in cases)
			{
				float t0 = Time.realtimeSinceStartup;
				ReachResult r = IslandReach.Assess(c.Value);
				levels[c.Key] = r;
				Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "  {0}: level {1}, walk {2:P0}, with jumps {3:P0}, at all {4:P0}, beaches {5:P0}, lowest ledge {6:F1} m, top {7}/{8} [{9:F0} ms]: {10}",
					c.Key, r.Level, r.Walk, r.WithJumps, r.Any, r.Beaches, r.LowestLedge, r.TopByWalking, r.TopAtAll, (Time.realtimeSinceStartup - t0) * 1000f, r.Text));
				yield return null;
			}
			Check(ref ok, levels["beaches all round"].Level == IslandReach.Easy && levels["beaches all round"].Beaches > 0.5f, "beaches all round: easy (" + levels["beaches all round"].Text + ")");
			Check(ref ok, levels["cliffs all round, 60 m high"].Level >= IslandReach.VeryTricky, "cliffs all round: " + levels["cliffs all round, 60 m high"].Text);
			Check(ref ok, levels["half cliffs"].Level <= IslandReach.Mostly && levels["half cliffs"].Beaches > 0.3f && levels["half cliffs"].Beaches < 0.75f, "half cliffs: fewer beaches, still reachable (" + levels["half cliffs"].Text + ")");
			Check(ref ok, levels["plateau with a ramp"].Level <= IslandReach.Mostly && levels["plateau with a ramp"].TopAtAll, "the plateau: its ramp leads up (" + levels["plateau with a ramp"].Text + ")");
			Check(ref ok, levels["sea stacks"].Level >= IslandReach.Mostly && !levels["sea stacks"].TopByWalking, "sea stacks: their tops need building (" + levels["sea stacks"].Text + ")");
			Check(ref ok, levels["a small low island"].Level == IslandReach.Easy, "a small low island: easy (" + levels["a small low island"].Text + ")");

			// Ledges: a flat island with a sheer edge into water 3 m deep, a little higher each time: a jump out of the
			// water, hopping up the rock (CIReachWorld measured both with Raft's player), then only building
			var ledges = new[] { IslandReach.SwimLedge * 0.8f, (IslandReach.SwimLedge + IslandReach.ClimbLedge) / 2f, IslandReach.ClimbLedge + 1f };
			var want = new[] { IslandReach.Tricky, IslandReach.VeryTricky, IslandReach.No };
			for (int i = 0; i < ledges.Length; i++)
			{
				const int N = 101;
				var g = new float[N, N];
				for (int z = 0; z < N; z++) for (int x = 0; x < N; x++) g[z, x] = new Vector2(x - 50, z - 50).magnitude * 2f < 40f ? 20f + ledges[i] : 17f;
				ReachResult lr = IslandReach.Assess(g, 2f, 20f);
				Check(ref ok, lr.Level == want[i] && Mathf.Abs(lr.LowestLedge - ledges[i]) < 0.05f, "a sheer ledge " + ledges[i].ToString("F2") + " m above the water: level " + lr.Level + " (want " + want[i] + "), lowest ledge " + lr.LowestLedge.ToString("F2") + " m");
			}

			// The same answer from the generated terrain (the editor's own heights)
			IslandGenSettings cliffs = cases[1].Value;
			IslandGenerator.GenerateInEditor(Tweak(cliffs, x => { x.Trees = 0f; x.Bushes = 0f; x.Rocks = 0f; x.Harvest = 0f; x.BeachThings = 0f; x.Water = 0f; x.SeaRocks = 0f; x.SeaFinds = 0f; x.Sunken = 0f; }));
			TerrainData data = terraineditor.terrain.terrainData;
			int res = data.heightmapResolution;
			float[,] h = data.GetHeights(0, 0, res, res);
			var m = new float[res, res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) m[z, x] = h[z, x] * data.size.y;
			ReachResult fromTerrain = IslandReach.Assess(m, data.size.x / (res - 1), DynamicIslands.EditorWaterLevel);
			Check(ref ok, fromTerrain.Level == levels[cases[1].Key].Level, "the generated cliffs island's own terrain gives the same answer (level " + fromTerrain.Level + ", lowest ledge " + fromTerrain.LowestLedge.ToString("F1") + " m)");

			// Ready-made islands above and below the sea
			MapType sky = MapTypes.Get("sky"), sunken = MapTypes.Get("sunken");
			float up, down;
			IslandGenSettings skyS = MapTypes.Roll(sky, new System.Random(5), out up), sunkS = MapTypes.Roll(sunken, new System.Random(5), out down);
			ReachResult skyR = IslandReach.Assess(skyS, up), sunkR = IslandReach.Assess(sunkS, down);
			Check(ref ok, up > 1f && skyR.Level == IslandReach.No && skyR.Text.Contains("floats") && down < -1f && sunkR.Text.Contains("diving"), "a sky island: " + skyR.Text + " A sunken one: " + sunkR.Text);

			// The window shows it above the seed, and quickly
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
			GeneratorWindow.Use(basic);
			ReachResult shown = GeneratorWindow.ReachNow();
			string text = GeneratorWindow.ReachText;
			Transform reachRow = GeneratorWindow.Instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Reach");
			Transform seedRow = GeneratorWindow.Instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Seed" && t.GetComponentInChildren<InputField>(true) != null && t.GetComponent<HorizontalLayoutGroup>() != null);
			Canvas.ForceUpdateCanvases();
			bool above = reachRow != null && seedRow != null && reachRow.position.y > seedRow.position.y;
			Check(ref ok, shown != null && shown.Level == IslandReach.Easy && text.StartsWith("Easy") && above && GeneratorWindow.ReachSeconds < 1.5f,
				"the window: \"" + text + "\" above the seed row: " + above + " (worked out in " + (GeneratorWindow.ReachSeconds * 1000f).ToString("F0") + " ms)");
			GeneratorWindow.Use(cliffs);
			GeneratorWindow.ReachNow();
			yield return new WaitForSecondsRealtime(0.3f);
			Screenshot(new[] { "gen_reach" });
			yield return new WaitForSecondsRealtime(0.6f);
			GeneratorWindow.Close();
			if (ok) Log("PASS: can players reach it"); else Fail("can players reach it");
		}

		static IslandGenSettings Tweak(IslandGenSettings s, Action<IslandGenSettings> change) { IslandGenSettings c = s.Copy(); change(c); return c; }

		[ConsoleCommand(name: "CIGenDefault", docs: "Dev, editor (Raft just started): the first Generate is an island like Raft's small ones - layout Raft small island, 25-75 m long, 6-18 m high, not round (its coast 1.25x further out one way than another), reachable from the raft; several seeds; Defaults gives it; after a Randomize existing generate (an island like Tangaroa) the Generate island tab still opens on its own settings; pictures shot_gendefault_<seed>.png and gendefault_<seed>.png (top down)")]
		public static void GenDefaultCommand()
		{
			StartTest(GenDefaultRoutine());
		}

		static IEnumerator GenDefaultRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			bool ok = true;
			IslandGenSettings first = IslandGenerator.Last;
			Check(ref ok, first.Shape == IslandShapes.RaftSmall && IslandShapes.Order[0] == IslandShapes.RaftSmall,
				"nothing generated yet: the window's first settings are layout " + IslandShapes.Names[first.Shape] + ", " + (first.Radius * 2f).ToString("F0") + " m, " + first.Height.ToString("F0") + " m high (Raft small island first in the Layout list)");

			// The default island, several seeds: its size, height, outline and reach, from its heights
			Vector3 size = IslandGenerator.BuildArea;
			int res = IslandGenerator.BuildResolution;
			float step = size.x / (res - 1);
			int[] seeds = { 1, 7, 42, 1234, 5150, 90210 };
			foreach (int seed in seeds)
			{
				IslandGenSettings s = IslandGenSettings.RaftLike(seed);
				float sea = s.WaterLevel;
				float[,] m = IslandGenerator.HeightsMetres(s, size, res);
				double cx = 0, cz = 0; int cells = 0; float top = 0f;
				for (int z = 0; z < res; z++)
					for (int x = 0; x < res; x++)
						if (m[z, x] > sea) { cx += x; cz += z; cells++; top = Mathf.Max(top, m[z, x] - sea); }
				if (cells == 0) { Check(ref ok, false, "seed " + seed + ": no land"); continue; }
				var mid = new Vector2((float)(cx / cells) * step, (float)(cz / cells) * step);
				Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(m, res, step, x, z);
				var rays = new float[64];
				for (int k = 0; k < 64; k++)
				{
					Vector2 dir = new Vector2(Mathf.Cos(k * Mathf.PI / 32f), Mathf.Sin(k * Mathf.PI / 32f));
					float last = 0f;
					for (float d = 0f; d < 150f; d += 0.5f) if (ground(mid.x + dir.x * d, mid.y + dir.y * d) > sea) last = d;
					rays[k] = last;
				}
				float length = Enumerable.Range(0, 32).Max(k => rays[k] + rays[k + 32]);
				float far = rays.Max(), near = Mathf.Max(0.5f, rays.Min());
				ReachResult r = IslandReach.Assess(s), fromHeights = IslandReach.Assess(m, step, sea);
				Check(ref ok, length >= 25f && length <= 75f && top >= 6f && top <= 18f && far / near > 1.25f && r.Level <= IslandReach.Mostly && fromHeights.Level <= IslandReach.Mostly,
					string.Format(System.Globalization.CultureInfo.InvariantCulture, "seed {0} ({1}): {2:F0} m long, {3:F1} m high, coast {4:F0}-{5:F0} m from the middle (x{6:F2}), reach {7} / from its heights {8}: {9}",
						seed, RaftIslandName(s), length, top, near, far, far / near, r.Level, fromHeights.Level, fromHeights.Text));
				if (seed == seeds[0] || seed == seeds[2] || seed == seeds[4])
				{
					Texture2D tex = IslandGenerator.Preview(m, step, s.Style, null, sea);
					File.WriteAllBytes(Path.Combine(DynamicIslands.assetpath, "gendefault_" + seed + ".png"), tex.EncodeToPNG());
					UnityEngine.Object.Destroy(tex);
				}
				yield return null;
			}

			// The window: Generate island opens on it; Defaults gives it (the seed and style stay)
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
			Check(ref ok, GeneratorWindow.Current.Shape == IslandShapes.RaftSmall, "the window opens on layout " + IslandShapes.Names[GeneratorWindow.Current.Shape]);
			GeneratorWindow.Use(new IslandGenSettings { Seed = 1234, Shape = IslandShapes.Atoll, Radius = 90f, Style = 1 });
			Button defaults = FindButton("Defaults");
			if (defaults != null) defaults.onClick.Invoke();
			IslandGenSettings def = GeneratorWindow.Current;
			Check(ref ok, defaults != null && def.Shape == IslandShapes.RaftSmall && def.Seed == 1234 && def.Style == 1 && Mathf.Abs(def.Radius - IslandGenSettings.RaftLike().Radius) < 0.01f, "Defaults: layout " + IslandShapes.Names[def.Shape] + ", seed " + def.Seed + ", style " + def.Style);

			// Three default islands generated in the editor, pictured as the window leaves them
			foreach (int seed in new[] { 1, 42, 5150 })
			{
				GeneratorWindow.Open();
				yield return null;
				GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
				IslandGenSettings g = IslandGenSettings.RaftLike(seed);
				g.Style = DynamicIslands.currentStyle;
				GeneratorWindow.Use(g);
				yield return PressGenerate(60f);
				Check(ref ok, GeneratorWindow.LastResult != null, "seed " + seed + " generated in the editor: " + (GeneratorWindow.LastResult ?? "nothing"));
				yield return new WaitForSecondsRealtime(1.5f);
				Screenshot(new[] { "gendefault_" + seed });
				yield return new WaitForSecondsRealtime(0.6f);
			}

			// After a Randomize existing generate (an island like Tangaroa: big and round), Generate island still opens on its own settings
			RaftIsland big = RaftIslands.All.FirstOrDefault(i => i.Label.IndexOf("Tangaroa", StringComparison.OrdinalIgnoreCase) >= 0) ?? RaftIslands.All.OrderByDescending(i => i.Radius).FirstOrDefault();
			if (big == null) Check(ref ok, false, "no measured Raft islands");
			else
			{
				GeneratorWindow.Open();
				yield return null;
				GeneratorWindow.ShowTab(GeneratorWindow.TabRandomize);
				GeneratorWindow.Pick(big, false);
				yield return null;
				IslandGenSettings like = GeneratorWindow.Current;
				yield return PressGenerate(120f);
				bool made = GeneratorWindow.LastResult != null && IslandGenerator.Last.Radius > 40f;
				GeneratorWindow.Open();
				yield return null;
				IslandGenSettings onRandomize = GeneratorWindow.Current;
				GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
				IslandGenSettings onNormal = GeneratorWindow.Current;
				Check(ref ok, made && onRandomize.Radius > 40f && onNormal.Shape == IslandShapes.RaftSmall && onNormal.Radius < 30f,
					"after a generate like " + big.Label + " (" + (like.Radius * 2f).ToString("F0") + " m, " + IslandShapes.Names[like.Shape] + "): Randomize existing keeps it (" + (onRandomize.Radius * 2f).ToString("F0") + " m), Generate island opens on " + IslandShapes.Names[onNormal.Shape] + " " + (onNormal.Radius * 2f).ToString("F0") + " m");
				GeneratorWindow.Close();
				GeneratorWindow.Open();
				yield return null;
				Check(ref ok, GeneratorWindow.Current.Shape == IslandShapes.RaftSmall, "reopened on Generate island: " + IslandShapes.Names[GeneratorWindow.Current.Shape]);
				GeneratorWindow.Close();
			}
			if (ok) Log("PASS: the first Generate is like Raft's small islands"); else Fail("the first Generate is like Raft's small islands");
		}

		/// <summary>The generator window's button with this label (tests).</summary>
		static Button FindButton(string label)
		{
			return GeneratorWindow.Instance.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.GetComponentsInChildren<Text>(true).Any(t => t.text == label));
		}

		/// <summary>Clicks the generator window's Generate button and waits (up to the seconds given) for the island.</summary>
		static IEnumerator PressGenerate(float seconds)
		{
			Button gen = FindButton("Generate");
			if (gen == null) { Log("  (no Generate button)"); yield break; }
			// (the last run's result stays until this run's Generate starts - after the buildings load, maybe much later)
			string before = GeneratorWindow.LastResult;
			gen.onClick.Invoke();
			float until = Time.realtimeSinceStartup + seconds;
			while ((GeneratorWindow.LastResult == null || ReferenceEquals(GeneratorWindow.LastResult, before)) && Time.realtimeSinceStartup < until) yield return null;
		}

		/// <summary>Which of Raft's islands a Raft small / large layout uses (for the log).</summary>
		static string RaftIslandName(IslandGenSettings s)
		{
			bool small = s.Shape == IslandShapes.RaftSmall;
			List<RaftIsland> pool = RaftIslands.All.Where(i => small ? i.Label.StartsWith("Small island") : i.Label.StartsWith("Big island") || i.Label == "Big")
				.Where(i => RaftIslands.Heights(i) != null).OrderBy(i => i.Scene, StringComparer.Ordinal).ToList();
			return pool.Count == 0 ? "no measured islands" : pool[new System.Random(s.Seed * 13 + 5).Next(pool.Count)].Label;
		}

		[ConsoleCommand(name: "CIGenBuildings", docs: "Dev, editor: the generator's Buildings and caves - huts with a cave, cabins, a themed scene, a mix, and neither: what each puts on the island (foundations, walls, pillars, a roof on them, a bed and a chest; the scene's props; Raft's cave piece with its guard), the ground levelled under a hut, the settings kept by a preset; pictures shot_genbuild_*.png")]
		public static void GenBuildingsCommand() { StartTest(GenBuildingsRoutine()); }

		static IEnumerator GenBuildingsRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("CIGenBuildings (in the editor)"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			Func<string, int> count = n => PlacedEditorObjects().Count(e => (e.GameObjectName ?? "") == n);
			Func<string, int> countLike = n => PlacedEditorObjects().Count(e => (e.GameObjectName ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
			Func<string, bool, string, int, bool, IslandGenSettings> make = (kind, on, style, n, caves) => new IslandGenSettings
			{
				Seed = 4242, Radius = 110f, Height = 22f, Roughness = 0.3f, Peaks = 1, ObjectDensity = 0.3f,
				Style = style == "snowy" ? TerrainPainter.Snowy : TerrainPainter.Tropical, Buildings = on, BuildingKind = kind, BuildingCount = n, Caves = caves,
			};
			var cases = new[]
			{
				new { Name = "huts", S = make(GenBuildings.Huts, true, "tropical", 3, true) },
				new { Name = "cabins", S = make(GenBuildings.Cabins, true, "tropical", 2, false) },
				new { Name = "raftcamp", S = make("raftcamp", true, "tropical", 1, false) },
				new { Name = "mixed", S = make(GenBuildings.Mixed, true, "snowy", 4, false) },
				new { Name = "none", S = make(GenBuildings.Mixed, false, "tropical", 3, false) },
				// (ROADMAP LM10: 0 to 25 structures, wrecks and landmarks on land and in the water)
				new { Name = "zero", S = make(GenBuildings.Mixed, true, "tropical", 0, false) },
				new { Name = "landmarks", S = make(GenBuildings.Landmarks, true, "tropical", 25, false) },
				new { Name = "many", S = make(GenBuildings.Mixed, true, "tropical", 25, false) },
			};
			foreach (var c in cases)
			{
				yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(c.S));
				IslandGenerator.GenerateInEditor(c.S);
				yield return new WaitForSecondsRealtime(0.5f);
				List<string> built = IslandGenerator.LastReport.Built;
				string what = string.Join(", ", built.ToArray());
				int found = count("Block_Foundation"), roofs = countLike("Roof"), pillars = count("Block_Pillar_Wood");
				switch (c.Name)
				{
					case "huts":
						Check(ref ok, built.Count(b => b == "a castaway hut") == 3 && count("Block_Wall_Thatch") >= 3 * 7 && found >= 3 * 4 && pillars >= 12 && roofs >= 3 && count("Placeable_Bed_Hammock") + count("Placeable_Bed_Basic") == 3,
							"3 castaway huts: " + found + " foundations, " + count("Block_Wall_Thatch") + " thatch walls, " + pillars + " corner pillars, " + roofs + " roof pieces, " + count("Placeable_Bed_Hammock") + " hammocks and " + count("Placeable_Bed_Basic") + " beds (" + what + ")");
						if (RandomizerIslands.CanBuildCaves)
							Check(ref ok, built.Contains("a cave") && RandomizerIslands.Dens.Any(d => count(d) == 1), "a cave: Raft's cave piece set into the land (" + string.Join(", ", RandomizerIslands.Dens.Select(d => d + " " + count(d)).ToArray()) + ")");
						else Log("  (no measured cave pieces on this Raft: the cave is left out - " + what + ")");
						// (the ground under each hut was built up: no foundation more than 0.5 m over the terrain under it)
						Check(ref ok, InsideWalls("Placeable_Bed_Hammock") && InsideWalls("Placeable_Bed_Basic") && InsideWalls("Loot_Chest"), "the hammocks and chests are inside their huts' walls");
						// (the walls and pillars stand on the foundations' deck: their bottoms at the foundations' tops - at FloatDepth they
						// hung 0.13 m in the air, the user saw, 2026-10-03)
						Func<EditorGameObject, Bounds> box = e => { Renderer[] rs = PlacementOptions.ShapeRenderers(e.gameObject); Bounds bb = rs[0].bounds; foreach (Renderer r in rs) bb.Encapsulate(r.bounds); return bb; };
						List<EditorGameObject> decks = PlacedEditorObjects().Where(e => e.GameObjectName == "Block_Foundation").ToList();
						// (how far each one's foot is over the deck under it: above it floats; a pillar reaching a little into the floor is fine)
						Func<string, float> over = n => PlacedEditorObjects().Where(e => e.GameObjectName == n).Select(e =>
						{
							Bounds wb = box(e);
							EditorGameObject under = decks.OrderBy(d => new Vector2(d.transform.position.x - wb.center.x, d.transform.position.z - wb.center.z).sqrMagnitude).First();
							return wb.min.y - box(under).max.y;
						}).DefaultIfEmpty(0f).Max();
						float walls = Mathf.Max(over("Block_Wall_Thatch"), over("Block_Wall_Wood")), posts = over("Block_Pillar_Wood");
						Check(ref ok, walls < 0.04f && walls > -0.1f && posts < 0.04f, "the walls and pillars stand on the foundations' deck: a wall's foot at most " + walls.ToString("F3") + " m over it, a pillar's " + posts.ToString("F3") + " m");
						Terrain t = terraineditor.terrain;
						float worst = PlacedEditorObjects().Where(e => e.GameObjectName == "Block_Foundation").Select(e => e.transform.position.y - (t.SampleHeight(e.transform.position) + t.transform.position.y)).DefaultIfEmpty(0f).Max();
						Check(ref ok, worst < 0.5f, "the huts stand on their ground: a foundation at most " + worst.ToString("F2") + " m over the terrain under it");
						break;
					case "cabins":
						Check(ref ok, built.Count(b => b == "a wooden cabin") == 2 && count("Block_Wall_Wood") >= 14 && count("Placeable_Bed_Wood") == 2 && PlacedEditorObjects().Count(e => e.GameObjectName == "Note_Book" && ObjectProps.Get(e.Props, ObjectProps.NoteTitle) == "Cabin log") == 2 && !built.Any(b => b.Contains("cave")),
							"2 wooden cabins with a bed and a log, no cave: " + count("Block_Wall_Wood") + " wooden walls (" + what + ")");
						Check(ref ok, InsideWalls("Placeable_Bed_Wood") && InsideWalls("Loot_Chest") && InsideWalls("Note_Book") && count("Bed_Simple") == 0, "the beds, chests and logs are inside their cabins' walls (a bed stuck out through the front wall, 2026-10-03)");
						break;
					case "raftcamp":
						Check(ref ok, built.Count == 1 && built[0].Contains("castaway") && count("campfire_1") >= 1, "a castaways' camp: its campfire and props (" + what + ")");
						break;
					case "mixed":
						Check(ref ok, built.Count(b => !b.Contains("found no")) >= 2, "mixed on a snowy island: " + what);
						break;
					case "none":
						Check(ref ok, built.Count == 0 && found == 0, "buildings and caves off: nothing built (" + found + " foundations)");
						break;
					case "zero":
						Check(ref ok, built.Count == 0 && found == 0 && GenBuildings.LandmarkNames.All(n => count(n) == 0), "How many 0: nothing built (" + what + ")");
						break;
					case "landmarks":
					{
						int made = built.Count(b => !b.Contains("found no"));
						float sea = DynamicIslands.EditorWaterLevel;
						List<EditorGameObject> pieces = PlacedEditorObjects().Where(e => GenBuildings.LandmarkNames.Contains(e.GameObjectName)).ToList();
						int sunk = built.Count(b => b.Contains("sunken")), wetPieces = pieces.Count(e => e.transform.position.y < sea - 2f);
						Check(ref ok, made >= 12 && pieces.Count >= made, "wrecks and landmarks, 25 asked: " + made + " made, " + pieces.Count + " set pieces (" + what + ")");
						Check(ref ok, sunk == 0 || wetPieces >= sunk, "the sunken ones lie on the sea floor: " + sunk + " sunken, " + wetPieces + " pieces under water");
						Check(ref ok, built.Select(b => b.Replace("a ", "")).Distinct().Count() >= 5, "of many kinds: " + string.Join(", ", built.Select(b => b.Replace("a ", "")).Distinct().ToArray()));
						break;
					}
					case "many":
						Check(ref ok, built.Count(b => !b.Contains("found no")) >= 10 && built.Count(b => !b.Contains("found no")) <= 25, "25 of a mix asked on a big island: " + built.Count(b => !b.Contains("found no")) + " (" + what + ")");
						break;
				}
				EditorGameObject first = c.Name == "landmarks" ? PlacedEditorObjects().FirstOrDefault(e => e.GameObjectName == "BoatStranded" || e.GameObjectName == "Airplane")
					: PlacedEditorObjects().FirstOrDefault(e => e.GameObjectName == "Block_Foundation" || e.GameObjectName == "campfire_1");
				if (first != null && Camera.main != null)
				{
					Vector3 at = first.transform.position;
					Camera.main.transform.SetPositionAndRotation(at + new Vector3(-9f, 6f, -9f), Quaternion.LookRotation(at + new Vector3(2f, 1f, 1.5f) - (at + new Vector3(-9f, 6f, -9f))));
					yield return new WaitForSecondsRealtime(1f);
					Screenshot(new[] { "genbuild_" + c.Name });
					yield return new WaitForSecondsRealtime(0.5f);
				}
			}
			// Each of the structures of Raft's pieces (LM10): lighthouse, lookout mast, jetty, ruin, skyscraper - three of a kind
			foreach (string only in new[] { "lighthouse", "lookout mast", "jetty", "ruin", "skyscraper" })
			{
				GenBuildings.OnlyLandmark = only;
				IslandGenSettings ls = make(GenBuildings.Landmarks, true, "tropical", 3, false);
				ls.Seed = 4343;
				try
				{
					yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(ls));
					IslandGenerator.GenerateInEditor(ls);
				}
				finally { GenBuildings.OnlyLandmark = null; }
				yield return new WaitForSecondsRealtime(0.5f);
				List<string> built = IslandGenerator.LastReport.Built;
				int made = built.Count(b => b == "a " + only);
				string key = only == "jetty" ? "Block_Foundation" : only == "ruin" ? "Loot_Crate" : only == "skyscraper" ? "RT_RoofLamp" : "RT_PillarThick";
				Check(ref ok, made >= 1 && count(key) >= made, only + ": " + made + " of 3 made (" + count(key) + " " + key + ") - " + string.Join(", ", built.ToArray()));
				EditorGameObject piece = PlacedEditorObjects().FirstOrDefault(e => e.GameObjectName == key);
				if (piece != null && Camera.main != null)
				{
					Vector3 at = piece.transform.position;
					Camera.main.transform.SetPositionAndRotation(at + new Vector3(-16f, 12f, -16f), Quaternion.LookRotation(at + new Vector3(-3f, 5f, 0f) - (at + new Vector3(-16f, 12f, -16f))));
					yield return new WaitForSecondsRealtime(1f);
					Screenshot(new[] { "genbuild_" + only.Replace(" ", "") });
					yield return new WaitForSecondsRealtime(0.5f);
				}
			}
			// The settings go with a preset
			IslandGenSettings back = IslandGenSettings.FromText(cases[0].S.ToText());
			Check(ref ok, back.Buildings && back.BuildingKind == GenBuildings.Huts && back.BuildingCount == 3 && back.Caves, "a preset keeps Buildings, Kind, How many and Caves");
			if (ok) Log("PASS: generator buildings and caves"); else Fail("generator buildings and caves");
		}

		/// <summary>Every object of this name (a hut's chest: titled "... chest", not the cave's hoard) lies within the box of the hut around it (its foundations' outer edges, 1.5 m cells), give or take 5 cm.</summary>
		static bool InsideWalls(string name)
		{
			List<EditorGameObject> found = PlacedEditorObjects().Where(e => e.GameObjectName == "Block_Foundation").ToList();
			bool all = true;
			foreach (EditorGameObject e in PlacedEditorObjects().Where(x => x.GameObjectName == name && (name != "Loot_Chest" || ObjectProps.Get(x.Props, ObjectProps.NoteTitle).EndsWith(" chest"))))
			{
				Renderer[] rs = PlacementOptions.ShapeRenderers(e.gameObject);
				if (rs.Length == 0) continue;
				Bounds b = rs[0].bounds;
				foreach (Renderer r in rs) b.Encapsulate(r.bounds);
				List<EditorGameObject> hut = found.Where(f => new Vector2(f.transform.position.x - b.center.x, f.transform.position.z - b.center.z).magnitude < 5f).ToList();
				if (hut.Count == 0) { Log("  " + name + ": no hut around it"); all = false; continue; }
				float x0 = hut.Min(f => f.transform.position.x) - 0.75f - 0.05f, x1 = hut.Max(f => f.transform.position.x) + 0.75f + 0.05f;
				float z0 = hut.Min(f => f.transform.position.z) - 0.75f - 0.05f, z1 = hut.Max(f => f.transform.position.z) + 0.75f + 0.05f;
				bool inside = b.min.x >= x0 && b.max.x <= x1 && b.min.z >= z0 && b.max.z <= z1;
				if (!inside) { Log("  " + name + " sticks out of its hut: its box x " + b.min.x.ToString("F2") + ".." + b.max.x.ToString("F2") + " z " + b.min.z.ToString("F2") + ".." + b.max.z.ToString("F2") + ", the hut x " + x0.ToString("F2") + ".." + x1.ToString("F2") + " z " + z0.ToString("F2") + ".." + z1.ToString("F2")); all = false; }
			}
			return all;
		}

		/// <summary>CW6: the island Check on the generated quest (the island in the editor): no problems.</summary>
		static void CheckGeneratedQuest(ref bool ok, string label)
		{
			var f = new IslandFile { Name = "generated", Props = new Dictionary<string, string>(DynamicIslands.currentIslandProps) };
			foreach (EditorGameObject e in PlacedEditorObjects()) f.Objects.Add(new IslandObject { Name = e.GameObjectName, Props = e.Props != null ? new Dictionary<string, string>(e.Props) : null });
			List<PlanChecker.Finding> found = PlanChecker.QuestFindings(f);
			var bad = found.Where(x => x.Level == PlanChecker.Level.Problem).Select(x => x.Text).ToList();
			Check(ref ok, bad.Count == 0, label + ": Check finds no problem in the quest" + (bad.Count > 0 ? " - " + string.Join(" / ", bad.ToArray()) : "") + (found.Count > bad.Count ? " (" + (found.Count - bad.Count) + " tip/warning)" : ""));
		}

		[ConsoleCommand(name: "CIGenQuest", docs: "Dev, editor: the generator's Quest steps - islands with quests of 1, 3, 5 and 8 steps: the island's quest has them, every step has what it needs on the island (the note, chest, zone, creatures, map pieces in chests, animals), the hoard last; 0 steps leaves the quest alone; Ctrl+Z brings the old quest back; the window steps aside after Generate")]
		public static void GenQuestCommand() { StartTest(GenQuestRoutine()); }

		static IEnumerator GenQuestRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("CIGenQuest (in the editor)"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			List<EditorGameObject> objs = null;
			Func<string, string, bool> has = (name, title) => objs.Any(e => (e.GameObjectName ?? "").StartsWith(name) && ObjectProps.Get(e.Props, ObjectProps.NoteTitle) == title);
			foreach (int n in new[] { 1, 3, 5, 8 })
			{
				var gs = new IslandGenSettings { Seed = 777 + n, Radius = 120f, Height = 28f, Roughness = 0.35f, Peaks = 2, ObjectDensity = 0.3f, QuestSteps = n };
				IslandGenerator.GenerateInEditor(gs);
				yield return new WaitForSecondsRealtime(0.4f);
				objs = PlacedEditorObjects();
				IslandQuest q = IslandQuest.From(DynamicIslands.currentIslandProps);
				var missing = new List<string>();
				foreach (IslandQuest.Step st in q.Steps)
				{
					bool there;
					switch (st.Type)
					{
						case "read": there = objs.Any(e => (e.GameObjectName ?? "").StartsWith("Note_") && ObjectProps.Get(e.Props, ObjectProps.NoteTitle) == st.Target); break;
						case "open": there = objs.Any(e => ContentCatalog.IsLootObject(e.GameObjectName ?? "") && ObjectProps.Get(e.Props, ObjectProps.NoteTitle) == st.Target); break;
						case "reach": there = objs.Any(e => e.GameObjectName == ContentCatalog.TriggerZone && ObjectProps.Get(e.Props, ObjectProps.ZoneId) == st.Target); break;
						case "kill": case "catch": there = objs.Count(e => { ContentCatalog.CreatureKind ck = ContentCatalog.CreatureOf(e.GameObjectName ?? ""); return ck != null && ck.Label == st.Target; }) > 0; break;
						case "collect": there = objs.Count(e => ObjectProps.Get(e.Props, ObjectProps.LootItems).Contains(StoryItems.Prefix + st.Target + "*")) >= st.Count && StoryItems.Of(DynamicIslands.currentIslandProps).Any(d => d.Id == st.Target); break;
						default: there = false; break;
					}
					if (!there) missing.Add(st.Type + "|" + st.Target);
				}
				bool last = q.Steps.Count > 0 && q.Steps.Last().Type == "open" && q.Steps.Last().Target.EndsWith("hoard");
				Check(ref ok, q.Steps.Count == n && missing.Count == 0 && last && q.Title.Length > 0 && (n == 1 || q.Steps[0].Type == "read"),
					n + " step(s): \"" + q.Title + "\": " + string.Join(" > ", q.Steps.Select(x => x.Type + " " + x.Target).ToArray()) + (missing.Count > 0 ? " - nothing on the island for: " + string.Join(", ", missing.ToArray()) : "") + " (" + IslandGenerator.LastReport.Describe() + ")");
				CheckGeneratedQuest(ref ok, n + " step(s)");
				Check(ref ok, !(q.Title == "The lookout's log" && !q.Steps.Any(x => x.Type == "reach")), n + " step(s): the title fits the steps (\"" + q.Title + "\")");
			}
			// CW6: with huts and a cave - the key in a hut opens the hoard, the hoard in the cave
			{
				var gs = new IslandGenSettings { Seed = 791, Radius = 120f, Height = 28f, Roughness = 0.35f, Peaks = 2, ObjectDensity = 0.3f, QuestSteps = 5, Buildings = true, BuildingKind = GenBuildings.Huts, BuildingCount = 2, Caves = true };
				yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(gs));
				IslandGenerator.GenerateInEditor(gs);
				yield return new WaitForSecondsRealtime(0.5f);
				objs = PlacedEditorObjects();
				IslandQuest q = IslandQuest.From(DynamicIslands.currentIslandProps);
				bool keyStep = q.Steps.Any(x => x.Type == "collect" && x.Target == "hoardkey");
				EditorGameObject hoardObj = objs.FirstOrDefault(e => ObjectProps.Get(e.Props, ObjectProps.NoteTitle) == q.Steps.Last().Target);
				bool locked = hoardObj != null && ObjectProps.Get(hoardObj.Props, BehaviourProps.CheckKey("open")).Contains("hoardkey");
				bool caveMade = IslandGenerator.LastReport.Built.Contains("a cave");
				Check(ref ok, keyStep && locked && (!caveMade || q.Steps.Last().Target == "Cave hoard"), "with huts and a cave: \"" + q.Title + "\": " + string.Join(" > ", q.Steps.Select(x => x.Type + " " + x.Target).ToArray()) + " (the hoard " + (locked ? "locked by the hut's key" : "not locked") + (caveMade ? ", a cave made" : ", no cave fitted") + ")");
				CheckGeneratedQuest(ref ok, "with huts and a cave");
			}
			// Quest steps 0: the island's old quest goes with the objects it was about (CA22); Ctrl+Z gives both back
			string before = ObjectProps.Get(DynamicIslands.currentIslandProps, IslandQuest.KeySteps);
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 31, Radius = 90f, Height = 20f, QuestSteps = 0 });
			yield return new WaitForSecondsRealtime(0.3f);
			Check(ref ok, !string.IsNullOrEmpty(before) && string.IsNullOrEmpty(ObjectProps.Get(DynamicIslands.currentIslandProps, IslandQuest.KeySteps)), "no quest steps: the island's old quest goes with its objects (CA22)");
			CommandUndoRedo.UndoRedoManager.Undo();
			yield return new WaitForSecondsRealtime(0.3f);
			Check(ref ok, ObjectProps.Get(DynamicIslands.currentIslandProps, IslandQuest.KeySteps) == before, "Ctrl+Z after generating without quest steps brings the old quest back");
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 32, Radius = 120f, Height = 28f, Peaks = 2, QuestSteps = 4 });
			yield return new WaitForSecondsRealtime(0.3f);
			bool changed = ObjectProps.Get(DynamicIslands.currentIslandProps, IslandQuest.KeySteps) != before;
			CommandUndoRedo.UndoRedoManager.Undo();
			yield return new WaitForSecondsRealtime(0.3f);
			Check(ref ok, changed && ObjectProps.Get(DynamicIslands.currentIslandProps, IslandQuest.KeySteps) == before, "Ctrl+Z after generating a quest brings the island's quest before back");
			// The window steps aside after Generate (the user: so the new island is in view at once)
			GeneratorWindow.Open();
			yield return new WaitForSecondsRealtime(0.5f);
			System.Reflection.MethodInfo gen = typeof(GeneratorWindow).GetMethod("OnGenerate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			GeneratorWindow w = UnityEngine.Object.FindObjectOfType<GeneratorWindow>();
			if (gen != null && w != null) gen.Invoke(w, null);
			yield return new WaitForSecondsRealtime(1f);
			Check(ref ok, !GeneratorWindow.IsOpen && (GeneratorWindow.LastResult ?? "").StartsWith("Island "), "Generate closes the window and says what it made: \"" + GeneratorWindow.LastResult + "\"");
			Screenshot(new[] { "genquest_after" });
			if (ok) Log("PASS: generator quests"); else Fail("generator quests");
		}

		[ConsoleCommand(name: "CIRemake", docs: "Dev, editor: Randomize existing's Rebuild it - the Radio Tower remade as a radio tower, an oil rig and a lighthouse under construction (each with two seeds): built of the tower's own pieces, all of them loaded (no missing-object blocks), nothing of it in the air (CIFloating), the report says what it built; pictures shot_remake_*.png. CIRemake [design id]")]
		public static void RemakeCommand(string[] args) { StartTest(RemakeRoutine(args != null && args.Length > 0 ? args[0] : null)); }

		static IEnumerator RemakeRoutine(string only)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("CIRemake (in the editor)"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			RaftIsland radar = RaftIslands.All.FirstOrDefault(i => i.Scene.Contains("Landmark_Radar"));
			if (radar == null) { Fail("no measured Radio Tower (raft_islands.txt)"); yield break; }
			foreach (Remakes.Design d in Remakes.For(radar.Scene))
			{
				if (only != null && d.Id != only) continue;
				foreach (int seed in new[] { 11, 12 })
				{
					IslandGenSettings gs = RaftIslands.LikeIt(radar, new IslandGenSettings { Seed = seed });
					gs.Seed = seed;
					gs.Remake = d.Id;
					yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(gs));
					IslandGenerator.GenerateInEditor(gs);
					yield return new WaitForSecondsRealtime(0.6f);
					List<string> built = IslandGenerator.LastReport.Built;
					List<EditorGameObject> objs = PlacedEditorObjects();
					int floors = objs.Count(e => e.GameObjectName == "RT_Floor"), walls = objs.Count(e => (e.GameObjectName ?? "").StartsWith("RT_Wall")), pillars = objs.Count(e => e.GameObjectName == "RT_PillarThick");
					int missing = PlacedEditorObjects().Count(e => e.gameObject.name.Contains(IslandSpawner.MissingTag));
					string word = d.Id == "rt.tower" ? "radio tower" : d.Id == "rt.rig" ? "oil rig" : "lighthouse";
					Check(ref ok, built.Any(x => x.Contains(word)) && floors >= 2 && (pillars >= 1 || d.Id == "rt.lighthouse") && missing == 0,
						d.Label + ", seed " + seed + ": " + string.Join(", ", built.ToArray()) + " - " + floors + " floors, " + walls + " walls, " + pillars + " pillars" + (missing > 0 ? ", " + missing + " objects not loaded" : ""));
					// Nothing of it in the air: the floating check on the RT pieces and ladders
					FloatingCommand(new[] { "0.3", "RT_" });
					FloatingCommand(new[] { "0.3", "Ladder" });
					EditorGameObject mid = objs.Where(e => e.GameObjectName == "RT_Floor").OrderBy(e => e.transform.position.y).FirstOrDefault();
					if (mid != null && Camera.main != null)
					{
						Vector3 at = mid.transform.position + new Vector3(-3f, 0f, 0f);
						float height = objs.Where(e => (e.GameObjectName ?? "").StartsWith("RT_")).Max(e => e.transform.position.y) - at.y;
						Vector3 from = at + new Vector3(-22f - height * 0.6f, height * 0.6f + 6f, -22f - height * 0.6f);
						Camera.main.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at + Vector3.up * height * 0.45f - from));
						yield return new WaitForSecondsRealtime(1.2f);
						Screenshot(new[] { "remake_" + d.Id.Replace("rt.", "") + "_" + seed });
						yield return new WaitForSecondsRealtime(0.5f);
					}
				}
			}
			if (ok) Log("PASS: remakes of Raft's islands"); else Fail("remakes of Raft's islands");
		}

		[ConsoleCommand(name: "CIStoryRemake", docs: "Dev, editor: ROADMAP CW1 - Rebuild it for Raft's other story islands: each design (or those whose id has <part>) generated on its island's ground with seed [seed] (11): built, every piece loaded, its loot container there, the pieces standing on the ground (none more than 0.4 m over it); a picture shot_sr_<id>.png. CIStoryRemake [part] [seed]")]
		public static void StoryRemakeCommand(string[] args)
		{
			// (a part and/or a seed, in any order: "CIStoryRemake 12" is seed 12 for all of them)
			string part = ""; int seed = 11, n;
			foreach (string a in args ?? new string[0]) { if (int.TryParse(a, out n)) seed = n; else if (a.Trim().Length > 0) part = a.Trim(); }
			StartTest(StoryRemakeRoutine(part, seed));
		}

		static IEnumerator StoryRemakeRoutine(string part, int seed)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("CIStoryRemake (in the editor)"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			int count = 0;
			foreach (Remakes.Design d in Remakes.All.Where(x => x.Kit != null && (part.Length == 0 || x.Id.Contains(part))))
			{
				RaftIsland island = RaftIslands.All.FirstOrDefault(i => i.Scene.Contains(d.Scene));
				if (island == null) { Check(ref ok, false, d.Id + ": no measured " + d.Scene + " (raft_islands.txt)"); continue; }
				IslandGenSettings gs = RaftIslands.LikeIt(island, new IslandGenSettings { Seed = seed });
				gs.Seed = seed;
				gs.Remake = d.Id;
				yield return PlaceableCatalog.EnsureLoaded(GenBuildings.NeededNames(gs));
				IslandGenerator.GenerateInEditor(gs);
				yield return new WaitForSecondsRealtime(0.6f);
				List<string> built = IslandGenerator.LastReport.Built;
				List<EditorGameObject> objs = PlacedEditorObjects();
				var kit = new HashSet<string>(d.Kit);
				List<EditorGameObject> mine = objs.Where(e => kit.Contains(e.GameObjectName)).ToList();
				int missing = objs.Count(e => e.gameObject.name.Contains(IslandSpawner.MissingTag));
				bool loot = mine.Any(e => e.Props != null && e.Props.ContainsKey(ObjectProps.LootItems));
				// (standing: the bottom of each piece's meshes no more than 0.4 m over the ground under its middle - things set
				// on another piece, a table's lantern, the roof, the mast, aren't counted)
				Terrain terrain = terraineditor.terrain;
				var floating = new List<string>();
				foreach (EditorGameObject e in mine)
				{
					Renderer[] rs = PlacementOptions.ShapeRenderers(e.gameObject);
					if (rs.Length == 0) continue;
					Bounds b = rs[0].bounds;
					foreach (Renderer r in rs) b.Encapsulate(r.bounds);
					float ground = terrain.SampleHeight(b.center) + terrain.transform.position.y;
					if (b.min.y - ground > 0.4f && !mine.Any(o => o != e && o.transform.position.y < e.transform.position.y - 0.2f && (new Vector2(o.transform.position.x - e.transform.position.x, o.transform.position.z - e.transform.position.z)).magnitude < 6f))
						floating.Add(e.GameObjectName + " +" + (b.min.y - ground).ToString("F1"));
				}
				// (and on the land: no part of a piece out over the sea - the user, 2026-10-06: Utopia's crane stood off the island)
				float sea = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
				var overSea = new List<string>();
				foreach (EditorGameObject e in mine)
				{
					Renderer[] rs = PlacementOptions.ShapeRenderers(e.gameObject);
					if (rs.Length == 0) continue;
					Bounds b = rs[0].bounds;
					foreach (Renderer r in rs) b.Encapsulate(r.bounds);
					foreach (Vector3 corner in new[] { new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.max.z) })
						if (terrain.SampleHeight(corner) + terrain.transform.position.y < sea - 0.3f) { overSea.Add(e.GameObjectName); break; }
				}
				Check(ref ok, built.Count > 0 && mine.Count >= 8 && missing == 0 && loot && floating.Count == 0 && overSea.Count == 0,
					d.Label + " (" + d.Id + "), seed " + seed + ": " + string.Join(", ", built.ToArray()) + " - " + mine.Count + " pieces" + (missing > 0 ? ", " + missing + " not loaded" : "") + (loot ? ", loot" : ", NO LOOT") + (floating.Count > 0 ? ", in the air: " + string.Join(", ", floating.Take(6).ToArray()) : "") + (overSea.Count > 0 ? ", out over the sea: " + string.Join(", ", overSea.Distinct().Take(6).ToArray()) : ""));
				if (mine.Count > 0 && Camera.main != null)
				{
					Vector3 c = new Vector3(mine.Average(e => e.transform.position.x), mine.Min(e => e.transform.position.y), mine.Average(e => e.transform.position.z));
					float size = Mathf.Max(12f, mine.Max(e => (new Vector2(e.transform.position.x - c.x, e.transform.position.z - c.z)).magnitude));
					// (high enough to look over the trees round the pad)
					Vector3 from = c + new Vector3(-size * 0.9f, size * 1.5f + 8f, -size * 0.9f);
					Camera.main.transform.SetPositionAndRotation(from, Quaternion.LookRotation(c + Vector3.up * 2f - from));
					yield return new WaitForSecondsRealtime(1.2f);
					Screenshot(new[] { "sr_" + d.Id.Replace(".", "_") });
					yield return new WaitForSecondsRealtime(0.5f);
				}
				count++;
			}
			if (ok && count > 0) Log("PASS: story remakes (" + count + ")"); else Fail("story remakes");
		}

		[ConsoleCommand(name: "CIUnderwaterShots", docs: "Dev, editor: generates an island on the deep sea floor (style: CIUnderwaterShots [Tropical|Snowy|Desert|Forest|Volcanic]) and takes pictures: from above, from the side under water, the reef on the shelf, and the drop-off (shot_uw_*.png)")]
		public static void UnderwaterShotsCommand(string[] args)
		{
			int style = args != null && args.Length > 0 ? TerrainPainter.StyleIndex(args[0]) : TerrainPainter.Tropical;
			StartTest(UnderwaterShotsRoutine(style));
		}

		static IEnumerator UnderwaterShotsRoutine(int style)
		{
			yield return WaitForEditor(false);
			var s = new IslandGenSettings { Seed = 1234, Radius = 55f, Height = 28f, Style = style, Trees = 0.35f, Bushes = 0.3f, Rocks = 0.3f, Harvest = 0.3f, BeachThings = 0.3f };
			int n = IslandGenerator.GenerateInEditor(s);
			Log("Generated " + n + " objects: " + IslandGenerator.LastReport.Describe());
			Terrain terrain = terraineditor.terrain;
			float sea = terrain.transform.position.y + DynamicIslands.EditorWaterLevel;
			Vector3 mid = terrain.transform.position + new Vector3(500f, 0f, 500f);
			// The ground's textures under water by depth, against Raft's islands (its rock share grows with depth: about a
			// tenth on the shallow shelf, a quarter to a third at 5-20 m, over half below 20 m)
			{
				TerrainData td = terrain.terrainData;
				int ar = td.alphamapResolution;
				float[,,] a = td.GetAlphamaps(0, 0, ar, ar);
				var rock = new float[RaftUnderwater.BandCount]; var cells = new int[RaftUnderwater.BandCount];
				for (int z = 0; z < ar; z += 2)
					for (int x = 0; x < ar; x += 2)
					{
						float depth = sea - (td.GetInterpolatedHeight((x + 0.5f) / ar, (z + 0.5f) / ar) + terrain.transform.position.y);
						if (depth < 0.5f || depth > 100f) continue;
						int b = RaftUnderwater.BandOf(depth);
						rock[b] += a[z, x, TerrainPainter.Rock]; cells[b]++;
					}
				string shares = string.Join(", ", Enumerable.Range(0, 6).Where(b => cells[b] > 0).Select(b => RaftUnderwater.Bands[b] + "-" + RaftUnderwater.Bands[b + 1] + " m " + (rock[b] / cells[b]).ToString("P0")).ToArray());
				float shallow = cells[1] > 0 ? rock[1] / cells[1] : 0f, deepRock = cells[4] > 0 ? rock[4] / cells[4] : 0f;
				if (shallow < 0.3f && deepRock > 0.4f && deepRock < 0.7f) Log("PASS: rock under water by depth: " + shares + " (Raft's tropical islands: 2-5 m 7-28 %, 20-40 m 43-63 %)");
				else Fail("rock under water by depth: " + shares);
			}
			Func<float, float, float> ground = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
			// The coast to the south: walk out from the middle until the ground is under water
			float d = 0f;
			while (d < 300f && ground(mid.x, mid.z - d) > sea) d += 1f;
			Transform cam = Camera.main.transform;
			string prefix = "uw_" + TerrainPainter.StyleName(style).ToLowerInvariant() + "_";
			var views = new[]
			{
				new KeyValuePair<string, Vector3[]>("above", new[] { new Vector3(mid.x, sea + 110f, mid.z - d - 120f), new Vector3(mid.x, sea - 10f, mid.z - d * 0.3f) }),
				new KeyValuePair<string, Vector3[]>("side", new[] { new Vector3(mid.x, sea - 35f, mid.z - d - 110f), new Vector3(mid.x, sea - 30f, mid.z - d) }),
				new KeyValuePair<string, Vector3[]>("reef", new[] { new Vector3(mid.x + 6f, sea - 4f, mid.z - d - 26f), new Vector3(mid.x - 4f, sea - 9f, mid.z - d - 5f) }),
				new KeyValuePair<string, Vector3[]>("dropoff", new[] { new Vector3(mid.x + 20f, sea - 12f, mid.z - d - 30f), new Vector3(mid.x - 10f, sea - 70f, mid.z - d - 90f) }),
			};
			foreach (var v in views)
			{
				cam.position = v.Value[0];
				cam.LookAt(v.Value[1]);
				yield return new WaitForSecondsRealtime(0.8f);
				Screenshot(new[] { prefix + v.Key });
				yield return new WaitForSecondsRealtime(0.6f);
			}
			DynamicIslands.SaveIsland("ciuw" + TerrainPainter.StyleName(style).ToLowerInvariant());
			Log("PASS: underwater pictures (coast " + d.ToString("F0") + " m south of the middle)");
		}

		#endregion

		#region How high a player gets (the generator's "can players reach it" line)

		[ConsoleCommand(name: "CIPlayerJump", docs: "Dev, in game (standing on the raft): measures Raft's player - walkable slope, step, jump speed and gravity, how high a jump from the ground lifts the feet, how deep a swimmer floats and how high a jump out of the water gets (the numbers behind the generator's reachability line)")]
		public static void PlayerJumpCommand()
		{
			StartTest(PlayerJumpRoutine());
		}

		static IEnumerator PlayerJumpRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || player.PersonController == null) { Fail("no player in a world"); yield break; }
			PersonController pc = player.PersonController;
			CharacterController cc = pc.controller;
			System.Reflection.FieldInfo minAngle = typeof(PersonController).GetField("minValidGroundAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Player: jumpSpeed {0}, gravity {1}, slopeLimit {2}, stepOffset {3}, height {4}, radius {5}, skin {6}, minValidGroundAngle {7}",
				pc.jumpSpeed, pc.gravity, cc.slopeLimit, cc.stepOffset, cc.height, cc.radius, cc.skinWidth, minAngle != null ? minAngle.GetValue(pc) : "?"));
			yield return EnsureAlive();
			Func<float> feet = () => cc.transform.TransformPoint(cc.center - Vector3.up * cc.height / 2f).y;
			// (GroundControll: a jump sets the vertical speed to jumpSpeed and gravity takes it off; WaterControll: 1.25 x jumpSpeed)
			float groundJump = pc.jumpSpeed * pc.jumpSpeed / (2f * pc.gravity);
			Log("A jump lifts the feet " + groundJump.ToString("F2") + " m (jumpSpeed² / 2 gravity)");

			// A swimmer in open water: how deep the feet float, then a jump out of the water as WaterControll does it
			Vector3 open = player.transform.position;
			for (int i = 0; i < 32; i++)
			{
				Vector3 p = player.transform.position + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (30f + i * 4f);
				if (!Physics.CheckSphere(new Vector3(p.x, 0f, p.z), 12f, ~0, QueryTriggerInteraction.Ignore)) { open = new Vector3(p.x, 0f, p.z); break; }
			}
			yield return PutPlayer(player, open + Vector3.down * 0.6f, true);
			yield return new WaitForSeconds(3f);
			float floatFeet = 0f; int n = 0;
			for (float t = 0f; t < 1f; t += Time.deltaTime) { floatFeet += feet(); n++; yield return null; }
			floatFeet /= Mathf.Max(1, n);
			pc.SwitchControllerType(ControllerType.Ground);
			pc.externalVelocity = Vector3.up * pc.jumpSpeed * 1.25f;
			float peak = floatFeet;
			for (float t = 0f; t < 2f; t += Time.deltaTime) { peak = Mathf.Max(peak, feet()); yield return null; }
			Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "A swimmer floats with the feet {0:F2} m down; a jump out of the water lifts them to {1:F2} m (+{2:F2} m)", floatFeet, peak, peak - floatFeet));
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "RESULT walk slope {0}, step {1}, jump up {2:F2} m, out of the water onto {3:F2} m + step", cc.slopeLimit, cc.stepOffset, groundJump, peak));
			if (peak > floatFeet + 1f) Log("PASS: measured the player"); else Fail("measuring the player's jump out of the water (feet " + floatFeet.ToString("F2") + " -> " + peak.ToString("F2") + ")");
		}

		/// <summary>Puts the player at a spot, walking (on something) or swimming.</summary>
		static IEnumerator PutPlayer(Network_Player player, Vector3 at, bool water)
		{
			PlayerMove.To(player, at, water ? ControllerType.Water : ControllerType.Ground);
			yield return null;
		}

		#endregion

		#region Measuring what is under water

		[ConsoleCommand(name: "CIMeasureUnderwater", docs: "Dev, editor: what lies under water around each of Raft's islands - every object (by name) with its depth, distance from the coast, slope and size, the ground's depth profile, and the terrain textures by depth: raft_underwater.txt in Mods\\DynamicIslands. CIMeasureUnderwater [part of a scene name]")]
		public static void MeasureUnderwaterCommand(string[] args)
		{
			string filter = args != null && args.Length > 0 ? string.Join(" ", args) : null;
			StartTest(MeasureUnderwaterRoutine(filter));
		}

		[ConsoleCommand(name: "CIDumpObject", docs: "Dev, editor: logs the parents, children and components of the first object with a name in one of Raft's island scenes. CIDumpObject <part of a scene name> <object name>")]
		public static void DumpObjectCommand(string[] args)
		{
			if (args == null || args.Length < 2) { Log("Usage: CIDumpObject <part of a scene name> <object name>"); return; }
			string scene = PlaceableCatalog.LandmarkSceneNames().FirstOrDefault(s => s.IndexOf(args[0], StringComparison.OrdinalIgnoreCase) >= 0);
			string name = string.Join(" ", args.Skip(1).ToArray());
			if (scene == null) { Fail("no scene matching " + args[0]); return; }
			StartTest(PlaceableCatalog.VisitScene(scene, s => DumpObject(s, name)));
		}

		static IEnumerator DumpObject(Scene s, string name)
		{
			Transform t = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => PlaceableCatalog.CleanName(x.name) == name);
			if (t == null) { Fail(name + " not found in " + s.name); yield break; }
			var sb = new StringBuilder();
			for (Transform p = t.parent; p != null; p = p.parent) sb.Append(" < ").Append(p.name);
			Log(name + " in " + s.name + " at " + t.position.ToString("F1") + " scale " + t.lossyScale.ToString("F2") + ", parents:" + sb);
			if (t.parent != null)
				foreach (Transform sib in t.parent) if (sib != t && Vector3.Distance(sib.position, t.position) < 8f) Log("  near sibling " + sib.name + " at " + (sib.position - t.position).ToString("F2"));
			foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
			{
				int depth = 0; for (Transform p = c; p != t; p = p.parent) depth++;
				Log("  " + new string(' ', depth * 2) + c.name + (c.gameObject.activeSelf ? "" : " (off)") + " layer " + LayerMask.LayerToName(c.gameObject.layer) + ": " + string.Join(", ", c.GetComponents<Component>().Where(x => x != null && !(x is Transform)).Select(x => x.GetType().Name).ToArray()));
			}
			Log("PASS: dumped " + name);
		}

		/// <summary>Depth bands of the ground (m below the sea): 0-2, 2-5, 5-10, 10-20, 20-40, 40-80, 80+.</summary>
		static readonly float[] DepthBands = { 0f, 2f, 5f, 10f, 20f, 40f, 80f, 10000f };

		static int BandOf(float depth) { for (int i = DepthBands.Length - 2; i >= 0; i--) if (depth >= DepthBands[i]) return i; return 0; }

		static IEnumerator MeasureUnderwaterRoutine(string filter)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor()) { Fail("open the editor first"); yield break; }
			float t0 = Time.realtimeSinceStartup;
			List<string> scenes = PlaceableCatalog.LandmarkSceneNames().Where(s => filter == null || s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			Log("Measuring under water around " + scenes.Count + " of Raft's island scenes");
			Scene lab = SceneManager.CreateScene("CI_MeasureLab", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
			var text = new StringBuilder();
			text.Append("# What lies under water around Raft's islands (CIMeasureUnderwater, raft=").Append(Application.version).Append(")\n");
			text.Append("# area\t<island>\t<style>\tm² of ground per depth band (0-2 2-5 5-10 10-20 20-40 40-80 80+ m)\tsteep share per band\tlowest\n");
			text.Append("# profile\t<island>\tmedian ground depth every 5 m from the coast\n");
			text.Append("# obj\t<island>\t<name>\tcount\tdepth p10/p50/p90\tcoast distance p10/p50/p90\tslope p50\tsize p50\tabove ground p50\tcount per band\tcatalog\n");
			text.Append("# tex\t<island>\t<band>\ttexture=share ...\n");
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
						try { text.Append(Underwater(s, island, h, n, cell, origin)); }
						catch (Exception e) { Log("  " + scenes[i] + ": " + e); }
					}));
					if (ok) { measured++; Log("  " + scenes[i] + " measured"); } else Log("  " + scenes[i] + ": " + why);
				}
			}
			finally
			{
				if (lab.IsValid()) SceneManager.UnloadSceneAsync(lab);
			}
			string path = Path.Combine(DynamicIslands.assetpath, "raft_underwater.txt");
			File.WriteAllText(path, text.ToString());
			Log("Measured under water around " + measured + " island(s) in " + (Time.realtimeSinceStartup - t0).ToString("F0") + " s: " + Path.GetFullPath(path));
			if (measured > 0) Log("PASS: measured under water"); else Fail("measured nothing");
		}

		static string P(List<float> v, float q) { if (v.Count == 0) return "-"; v.Sort(); return v[Mathf.Clamp((int)(q * v.Count), 0, v.Count - 1)].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }

		/// <summary>One island's lines of raft_underwater.txt.</summary>
		static string Underwater(Scene src, RaftIsland island, float[,] h, int n, float cell, Vector2 origin)
		{
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			// Distance from the coast (m) of every water cell (two chamfer passes), and the slope
			var d = new float[n, n];
			for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) d[z, x] = h[z, x] > 0f ? 0f : 1e9f;
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
			Func<float, float, float> sample = (wx, wz) =>
			{
				float fx = Mathf.Clamp((wx - origin.x) / cell, 0f, n - 1.001f), fz = Mathf.Clamp((wz - origin.y) / cell, 0f, n - 1.001f);
				int x0 = (int)fx, z0 = (int)fz; float tx = fx - x0, tz = fz - z0;
				return Mathf.Lerp(Mathf.Lerp(h[z0, x0], h[z0, x0 + 1], tx), Mathf.Lerp(h[z0 + 1, x0], h[z0 + 1, x0 + 1], tx), tz);
			};
			Func<int, int, float> slopeAt = (x, z) =>
			{
				int x0 = Mathf.Max(0, x - 1), x1 = Mathf.Min(n - 1, x + 1), z0 = Mathf.Max(0, z - 1), z1 = Mathf.Min(n - 1, z + 1);
				float gx = (h[z, x1] - h[z, x0]) / ((x1 - x0) * cell), gz = (h[z1, x] - h[z0, x]) / ((z1 - z0) * cell);
				return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
			};
			float lowest = float.MaxValue;
			foreach (float v in h) lowest = Mathf.Min(lowest, v);
			int bands = DepthBands.Length - 1;
			var area = new float[bands]; var steep = new float[bands];
			var profile = new Dictionary<int, List<float>>();
			for (int z = 0; z < n; z++)
				for (int x = 0; x < n; x++)
				{
					float v = h[z, x];
					if (v > 0f || v <= lowest + 0.5f) continue; // (land, and the flat floor of "no ground")
					int b = BandOf(-v);
					area[b] += cell * cell;
					if (slopeAt(x, z) > 35f) steep[b] += cell * cell;
					int k = (int)(d[z, x] * cell / 5f);
					if (k < 40) { if (!profile.ContainsKey(k)) profile[k] = new List<float>(); profile[k].Add(-v); }
				}
			var sb = new StringBuilder();
			string name0 = island.Label;
			sb.Append("area\t").Append(name0).Append('\t').Append(island.Style).Append('\t').Append(string.Join(" ", area.Select(a => a.ToString("0", inv)).ToArray()))
				.Append('\t').Append(string.Join(" ", area.Select((a, i) => a > 0f ? (steep[i] / a).ToString("0.00", inv) : "-").ToArray())).Append('\t').Append(lowest.ToString("0.#", inv)).Append('\n');
			sb.Append("profile\t").Append(name0).Append('\t').Append(string.Join(" ", profile.Keys.OrderBy(k => k).Select(k => (k * 5) + ":" + P(profile[k], 0.5f)).ToArray())).Append('\n');

			// Objects under water: the placeable ones, and every pickup (harvestables, seaweed...)
			var found = new List<KeyValuePair<string, Transform>>(PlaceableCatalog.PlaceablesOf(src));
			foreach (GameObject root in src.GetRootGameObjects())
				foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
				{
					if (!t.name.StartsWith("Pickup_", StringComparison.OrdinalIgnoreCase)) continue;
					bool nested = false;
					for (Transform p = t.parent; p != null && !nested; p = p.parent) nested = p.name.StartsWith("Pickup_", StringComparison.OrdinalIgnoreCase);
					if (!nested) found.Add(new KeyValuePair<string, Transform>(PlaceableCatalog.CleanName(t.name), t));
				}
			var byName = new Dictionary<string, List<float[]>>(); // depth, coast, slope, size, above ground, band
			foreach (var kv in found)
			{
				Vector3 pos = kv.Value.position;
				if (pos.y > -0.3f) continue;
				float gx = (pos.x - origin.x) / cell, gz = (pos.z - origin.y) / cell;
				if (gx < 0 || gz < 0 || gx > n - 1 || gz > n - 1) continue;
				int ix = Mathf.RoundToInt(gx), iz = Mathf.RoundToInt(gz);
				float ground = sample(pos.x, pos.z);
				float size = 0f;
				Renderer[] rs = kv.Value.GetComponentsInChildren<Renderer>(true);
				if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds); size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); }
				List<float[]> list;
				if (!byName.TryGetValue(kv.Key, out list)) byName[kv.Key] = list = new List<float[]>();
				list.Add(new[] { -pos.y, d[iz, ix] * cell, slopeAt(ix, iz), size, pos.y - ground, BandOf(Mathf.Max(0f, -ground)) });
			}
			foreach (var kv in byName.OrderByDescending(kv => kv.Value.Count))
			{
				var l = kv.Value;
				var perBand = new int[bands];
				foreach (float[] o in l) perBand[(int)o[5]]++;
				string catalog = PlaceableCatalog.CoreNames.Contains(kv.Key) ? "core" : PlaceableCatalog.SceneOf(kv.Key) != null ? "index" : PlaceableCatalog.IsHarvestableName(kv.Key) ? "harvest" : "-";
				sb.Append("obj\t").Append(name0).Append('\t').Append(kv.Key).Append('\t').Append(l.Count)
					.Append('\t').Append(P(l.Select(o => o[0]).ToList(), 0.1f)).Append('/').Append(P(l.Select(o => o[0]).ToList(), 0.5f)).Append('/').Append(P(l.Select(o => o[0]).ToList(), 0.9f))
					.Append('\t').Append(P(l.Select(o => o[1]).ToList(), 0.1f)).Append('/').Append(P(l.Select(o => o[1]).ToList(), 0.5f)).Append('/').Append(P(l.Select(o => o[1]).ToList(), 0.9f))
					.Append('\t').Append(P(l.Select(o => o[2]).ToList(), 0.5f)).Append('\t').Append(P(l.Select(o => o[3]).ToList(), 0.5f)).Append('\t').Append(P(l.Select(o => o[4]).ToList(), 0.5f))
					.Append('\t').Append(string.Join(" ", perBand.Select(c => c.ToString(inv)).ToArray())).Append('\t').Append(catalog).Append('\n');
			}

			// Ground textures under water, by depth band
			var tex = new Dictionary<int, Dictionary<string, float>>();
			foreach (GameObject root in src.GetRootGameObjects())
				foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
				{
					TerrainData td = t.terrainData;
					if (td == null || td.terrainLayers == null || td.terrainLayers.Length == 0) continue;
					int ar = td.alphamapResolution, layers = td.alphamapLayers;
					float[,,] alpha = td.GetAlphamaps(0, 0, ar, ar);
					int stepA = Mathf.Max(1, ar / 256);
					for (int az = 0; az < ar; az += stepA)
						for (int ax = 0; ax < ar; ax += stepA)
						{
							float wx = t.transform.position.x + (ax + 0.5f) / ar * td.size.x, wz = t.transform.position.z + (az + 0.5f) / ar * td.size.z;
							float y = t.SampleHeight(new Vector3(wx, 0f, wz)) + t.transform.position.y;
							if (y > -0.2f || y <= lowest + 0.5f) continue;
							int b = BandOf(-y);
							Dictionary<string, float> w;
							if (!tex.TryGetValue(b, out w)) tex[b] = w = new Dictionary<string, float>();
							for (int l = 0; l < layers; l++)
							{
								TerrainLayer tl = td.terrainLayers[l];
								string ln = tl != null && tl.diffuseTexture != null ? tl.diffuseTexture.name : "layer" + l;
								float a;
								w.TryGetValue(ln, out a);
								w[ln] = a + alpha[az, ax, l];
							}
						}
				}
			foreach (var kv in tex.OrderBy(kv => kv.Key))
			{
				float total = kv.Value.Values.Sum();
				if (total <= 0f) continue;
				sb.Append("tex\t").Append(name0).Append('\t').Append(DepthBands[kv.Key].ToString(inv)).Append('\t')
					.Append(string.Join(" ", kv.Value.Where(x => x.Value / total >= 0.01f).OrderByDescending(x => x.Value).Select(x => x.Key + "=" + (x.Value / total).ToString("0.00", inv)).ToArray())).Append('\n');
			}
			return sb.ToString();
		}

		#endregion

		#region The generator window in the button walk

		/// <summary>A window's tab button (UIKit.Tabs): walked as a screen of its own.</summary>
		static bool IsTabButton(Button b) { return b.transform.parent != null && b.transform.parent.name == "Tabs"; }

		/// <summary>The generator window, one screen per tab: its tab button, then every button of that tab.</summary>
		static IEnumerator GeneratorScreens(ButtonRun run)
		{
			string[] names = { "Normal", "Randomize existing", "Ready-made" };
			for (int t = 0; t < 3; t++)
			{
				CloseAllWindows();
				GeneratorWindow.Open();
				yield return null;
				GeneratorWindow w = GeneratorWindow.Instance;
				if (w == null || !GeneratorWindow.IsOpen) { run.Errors.Add("Generator: the window did not open"); yield break; }
				Button tab = w.GetComponentsInChildren<Button>(true).Where(IsTabButton).ElementAtOrDefault(t);
				if (tab == null) { run.Errors.Add("Generator: no tab button " + names[t]); continue; }
				ScreenStarts(run, "Generator, " + names[t] + " tab");
				int before = run.Pressed;
				yield return Press(run, tab, "Generator", 1);
				if (w.Tab != t) run.Errors.Add("Generator: the " + names[t] + " tab button did not show its tab");
				run.Windows++;
				yield return TestWindow(run, w, null, "Generator, " + names[t] + " tab", 1);
				ScreenTimeLimit(run, "Generator, " + names[t] + " tab", run.Pressed - before, "");
				run.PerScreen["Generator, " + names[t] + " tab"] = run.Pressed - before;
				GeneratorWindow.Close();
				yield return null;
				if (UIKit.ShownHelp != null) run.Unclosed.Add("a help popup stayed open after the generator closed (" + names[t] + " tab)");
			}
			// (the button walk saved a preset named after the prompt's default text)
			try { File.Delete(Path.Combine(GeneratorWindow.PresetFolder, "my island.txt")); } catch { }
		}

		#endregion

		#region The generator's settings

		[ConsoleCommand(name: "CIGenFeatures", docs: "Dev, editor: every setting of the generator - deterministic, has an effect, keeps the edge flat; the top is the height asked for; size and height presets; stretch; object sliders (none, the water slider alone, a jungle); creatures and loot tiers; generating with content, undo/redo, save and load")]
		public static void GenFeaturesCommand()
		{
			StartTest(GenFeaturesRoutine());
		}

		static IEnumerator GenFeaturesRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); yield break; }
			bool ok = true;
			Vector3 size = IslandGenerator.BuildArea;
			const int Res = 257;
			var basic = new IslandGenSettings { Seed = 5150, Radius = 90f, Height = 45f };
			float step = size.x / (Res - 1), sea = basic.WaterLevel;
			float[,] baseline = IslandGenerator.HeightsMetres(basic, size, Res);
			Func<float[,], float[,], bool> same = (a, b) => a.Cast<float>().SequenceEqual(b.Cast<float>());
			Func<float[,], bool> edgesFlat = a => Enumerable.Range(0, Res).All(i => a[0, i] < 0.01f && a[Res - 1, i] < 0.01f && a[i, 0] < 0.01f && a[i, Res - 1] < 0.01f);
			Func<float[,], float> top = a => a.Cast<float>().Max() - sea;

			// 1. Each setting: deterministic, changes the island, the edge stays flat seabed, the top is the height asked for
			var variants = new List<KeyValuePair<string, Action<IslandGenSettings>>>
			{
				new KeyValuePair<string, Action<IslandGenSettings>>("coast", x => x.Coast = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("bays", x => x.Bays = 0.8f),
				new KeyValuePair<string, Action<IslandGenSettings>>("beach", x => x.BeachWidth = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("cliffs", x => x.Cliffs = 0.7f),
				new KeyValuePair<string, Action<IslandGenSettings>>("stretch", x => { x.Stretch = 2.5f; x.StretchAngle = 30f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("peak shape", x => x.PeakShape = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("terraces", x => x.Terraces = 0.8f),
				new KeyValuePair<string, Action<IslandGenSettings>>("lakes", x => x.Lakes = 0.8f),
				new KeyValuePair<string, Action<IslandGenSettings>>("valleys", x => x.Valleys = 0.8f),
				new KeyValuePair<string, Action<IslandGenSettings>>("erosion", x => x.Erosion = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("shallow water", x => x.Shelf = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("rocky seabed", x => x.Seabed = IslandGenSettings.SeabedRocky),
				new KeyValuePair<string, Action<IslandGenSettings>>("reef ring", x => x.Seabed = IslandGenSettings.SeabedReef),
				new KeyValuePair<string, Action<IslandGenSettings>>("twin peaks", x => x.Shape = IslandShapes.TwinPeaks),
				new KeyValuePair<string, Action<IslandGenSettings>>("crescent", x => x.Shape = IslandShapes.Crescent),
				new KeyValuePair<string, Action<IslandGenSettings>>("volcanic", x => x.Style = TerrainPainter.Volcanic),
				new KeyValuePair<string, Action<IslandGenSettings>>("Balboa size", x => { x.Radius = RaftIslands.BalboaRadius; x.Height = RaftIslands.BalboaTop; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("small island", x => { x.Radius = RaftIslands.SmallRadius; x.Height = RaftIslands.SmallTop; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("sheer drop-off", x => x.DropOff = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("gentle drop-off", x => x.DropOff = 0f),
				new KeyValuePair<string, Action<IslandGenSettings>>("shallow sea floor", x => x.SeaFloor = IslandGenSettings.SeaFloorShallow),
				new KeyValuePair<string, Action<IslandGenSettings>>("deep atoll", x => x.Shape = IslandShapes.Atoll),
				new KeyValuePair<string, Action<IslandGenSettings>>("deep archipelago", x => x.Shape = IslandShapes.Archipelago),
				new KeyValuePair<string, Action<IslandGenSettings>>("deep sea stacks", x => x.Shape = IslandShapes.Stacks),
			};
			foreach (var v in variants)
			{
				IslandGenSettings vs = basic.Copy();
				v.Value(vs);
				float t0 = Time.realtimeSinceStartup;
				float[,] a = IslandGenerator.HeightsMetres(vs, size, Res);
				float seconds = Time.realtimeSinceStartup - t0;
				float[,] b = IslandGenerator.HeightsMetres(vs, size, Res);
				float peak = a.Cast<float>().Max() - vs.WaterLevel;
				Check(ref ok, same(a, b) && !same(a, baseline) && edgesFlat(a) && Mathf.Abs(peak - vs.Height) < Mathf.Max(1f, vs.Height * 0.04f),
					v.Key + ": same twice, differs from the plain island, flat edges, top " + peak.ToString("F1") + " m (asked " + vs.Height.ToString("F0") + ") [" + (seconds * 1000f).ToString("F0") + " ms at " + Res + "]");
				yield return null;
			}

			// 2. Stretch: long and narrow along the direction
			{
				IslandGenSettings st = basic.Copy(); st.Stretch = 2.5f; st.StretchAngle = 0f; st.Coast = 0.2f;
				float[,] a = IslandGenerator.HeightsMetres(st, size, Res);
				float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
				for (int z = 0; z < Res; z++) for (int x = 0; x < Res; x++) if (a[z, x] > sea) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z); }
				float lx = (maxX - minX) * step, lz = (maxZ - minZ) * step;
				Check(ref ok, lx > lz * 1.8f, "stretch 2.5 along x: land " + lx.ToString("F0") + " m long, " + lz.ToString("F0") + " m wide");
			}

			// 3. The presets are Raft's measured islands
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
			yield return null;
			GeneratorWindow w = GeneratorWindow.Instance;
			Func<string, string, Button> button = (row, label) => w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.transform.parent.name == row && LabelOfButton(b).StartsWith(label));
			var presets = new[] { new[] { "SizePresets", "Small island" }, new[] { "SizePresets", "Large island" }, new[] { "SizePresets", "Balboa" }, new[] { "HeightPresets", "Small island" }, new[] { "HeightPresets", "Large island" }, new[] { "HeightPresets", "Balboa" } };
			float[] want = { RaftIslands.SmallRadius, RaftIslands.LargeRadius, RaftIslands.BalboaRadius, RaftIslands.SmallTop, RaftIslands.LargeTop, RaftIslands.BalboaTop };
			for (int i = 0; i < presets.Length; i++)
			{
				Button b = button(presets[i][0], presets[i][1]);
				if (b != null) b.onClick.Invoke();
				float got = i < 3 ? w.Settings.Radius : w.Settings.Height;
				Check(ref ok, b != null && Mathf.Abs(got - Mathf.Clamp(want[i], i < 3 ? IslandGenSettings.MinRadius : IslandGenSettings.MinHeight, i < 3 ? IslandGenSettings.MaxRadius : IslandGenSettings.MaxHeight)) < 0.01f,
					"the " + presets[i][1] + " " + (i < 3 ? "size" : "height") + " button sets " + want[i].ToString("F1") + " m (" + (b != null ? LabelOfButton(b) : "no button") + ")");
			}
			Check(ref ok, RaftIslands.All.Count > 20, RaftIslands.All.Count + " of Raft's islands measured (raft_islands.txt)");
			GeneratorWindow.Close();

			// 4. Objects: none, the water slider alone, a jungle; the same list twice
			var calm = new IslandGenSettings { Seed = 777, Radius = 90f, Height = 40f, Trees = 0f, Bushes = 0f, Rocks = 0f, BeachThings = 0f, Harvest = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			float[,] hm = IslandGenerator.HeightsMetres(calm, size, IslandGenerator.BuildResolution);
			List<IslandObject> none = IslandGenerator.PlanAll(calm, hm, size);
			Check(ref ok, none.Count == 0, "every object slider at none: " + none.Count + " objects");
			IslandGenSettings wet = calm.Copy(); wet.Water = 1f;
			var wetReport = new GenReport();
			List<IslandObject> reef = IslandGenerator.PlanAll(wet, hm, size, wetReport);
			bool allUnder = reef.All(o => o.Position.y < sea - 0.2f);
			Check(ref ok, reef.Count > 200 && allUnder && wetReport.Count(IslandGenerator.CatWater) == reef.Count, "the corals and plants slider alone: " + reef.Count + " corals and sea plants, all under water: " + allUnder);

			// 4b. Under water like Raft's islands (RaftUnderwater): every kind at the depths it has there, as dense as there
			yield return UnderwaterChecks(r => { if (!r) ok = false; });
			IslandGenSettings jungle = calm.Copy(); jungle.Trees = 1f; jungle.Bushes = 1f; jungle.Rocks = 0.75f; jungle.Harvest = 0.75f; jungle.BeachThings = 0.6f; jungle.Water = 0.5f;
			var jr = new GenReport();
			float j0 = Time.realtimeSinceStartup;
			List<IslandObject> dense = IslandGenerator.PlanAll(jungle, hm, size, jr);
			float jSeconds = Time.realtimeSinceStartup - j0;
			List<IslandObject> dense2 = IslandGenerator.PlanAll(jungle, hm, size);
			bool sameList = dense.Count == dense2.Count && dense.Zip(dense2, (x, y) => x.Name == y.Name && x.Position == y.Position).All(t => t);
			// Trees of the jungle: how far to the nearest other tree (the median)
			var treePos = dense.Where(o => IslandGenerator.Categories.Length > 0 && (o.Name.Contains("Palm") || o.Name.Contains("Mango") || o.Name.StartsWith("Bamboo"))).Select(o => new Vector2(o.Position.x, o.Position.z)).ToList();
			float medianGap = treePos.Count > 1 ? treePos.Take(300).Select(p => treePos.Where(q => q != p).Min(q => (q - p).magnitude)).OrderBy(d => d).ElementAt(Mathf.Min(150, treePos.Count - 1) / 2) : 0f;
			Check(ref ok, dense.Count > 3000 && dense.Count <= IslandGenerator.MaxObjects && sameList,
				"jungle (trees and bushes at the top): " + dense.Count + " objects (" + jr.Describe() + "; wanted " + jr.Wanted + ") planned in " + jSeconds.ToString("F2") + " s, the same twice: " + sameList + ", trees about " + medianGap.ToString("F1") + " m apart");

			// 5. Creatures and loot: kinds, counts, tiers
			IslandGenSettings content = calm.Copy();
			content.Hostiles = 5; content.HostileKinds = "Bear,Hyena"; content.Difficulty = "Hard"; content.Friendly = 3; content.SeaLife = 2;
			content.Loot = 10; content.LootMin = 5; content.LootMax = 5; content.Trees = 0.4f; content.LootHidden = true;
			var cr = new GenReport();
			List<IslandObject> stuff = IslandGenerator.PlanAll(content, hm, size, cr);
			var creatures = stuff.Where(o => ContentCatalog.IsCreature(o.Name)).ToList();
			var hostile = creatures.Where(o => o.Name == "Creature_Bear" || o.Name == "Creature_Hyena").ToList();
			bool hard = hostile.All(o => ObjectProps.Get(o.Props, ObjectProps.CreatureHealth) == "2");
			var boxes = stuff.Where(o => ContentCatalog.IsLootObject(o.Name)).ToList();
			bool tier5 = boxes.All(o => ObjectProps.Loot(o.Props).Any(l => l.Key == "TitaniumIngot" || l.Key == "ExplosiveGoo" || l.Key == "Battery"));
			Check(ref ok, hostile.Count == 5 && hard && creatures.Count == 10 && boxes.Count == 10 && tier5,
				"content: " + hostile.Count + " hostile spots (bears and hyenas, hard: " + hard + "), " + (creatures.Count - hostile.Count) + " friendly and sea spots, " + boxes.Count + " tier-5 boxes (" +
				(boxes.Count > 0 ? ObjectProps.Get(boxes[0].Props, ObjectProps.LootItems) : "-") + ")");
			for (int tier = 1; tier <= 5; tier++)
				Log("  tier " + tier + ": " + IslandGenerator.TierLoot(tier, new System.Random(tier)));

			// 6. Into the editor with content: one undo step, saved and loaded with its settings
			Transform placed = GameObject.Find("PlacedObjects").transform;
			DynamicIslands.NewIsland(); // (an empty island on the shallow seabed: generating moves the sea to the deep sea floor's level)
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			IslandGenSettings full = content.Copy(); full.Seed = 31337; full.Loot = 6; full.LootMin = 1; full.LootMax = 3; full.Trees = 0.5f; full.Bushes = 0.4f; full.Rocks = 0.3f; full.Water = 0.3f; full.SeaFinds = 0.5f; full.SeaRocks = 0.5f;
			float levelBefore = DynamicIslands.EditorWaterLevel;
			float g0 = Time.realtimeSinceStartup;
			int n = IslandGenerator.GenerateInEditor(full);
			float gSeconds = Time.realtimeSinceStartup - g0;
			yield return null;
			float levelAfter = DynamicIslands.EditorWaterLevel;
			GameObject plane = GameObject.Find("WaterLevel");
			int visible = placed.GetComponentsInChildren<EditorGameObject>(false).Length;
			int withProps = placed.GetComponentsInChildren<EditorGameObject>(false).Count(e => e.Props != null && e.Props.Count > 0);
			CommandUndoRedo.UndoRedoManager.Undo();
			int afterUndo = placed.GetComponentsInChildren<EditorGameObject>(false).Length;
			float levelUndone = DynamicIslands.EditorWaterLevel;
			CommandUndoRedo.UndoRedoManager.Redo();
			int afterRedo = placed.GetComponentsInChildren<EditorGameObject>(false).Length;
			Check(ref ok, n > 500 && visible == n && withProps >= 15 && afterUndo != n && afterRedo == n,
				"generated in the editor in " + gSeconds.ToString("F1") + " s: " + n + " objects (" + IslandGenerator.LastReport.Describe() + "), " + withProps + " with settings; undo " + afterUndo + ", redo " + afterRedo);
			Check(ref ok, levelBefore == IslandFile.DefaultWaterLevel && levelAfter == IslandFile.DeepWaterLevel && levelUndone == levelBefore && DynamicIslands.EditorWaterLevel == levelAfter && plane != null && Mathf.Abs(plane.transform.position.y - levelAfter) < 0.01f,
				"the sea level: " + levelBefore + " m -> " + levelAfter + " m above the terrain's base (the blue plane at " + (plane != null ? plane.transform.position.y.ToString("F0") : "-") + "), undo " + levelUndone + ", redo " + DynamicIslands.EditorWaterLevel);
			DynamicIslands.SaveIsland("cigenfeatures");
			IslandFile file = IslandFile.Load(IslandSpawner.PathFor("cigenfeatures"));
			int savedProps = file.Objects.Count(o => o.Props != null && o.Props.Count > 0);
			Check(ref ok, file.Objects.Count == n && savedProps == withProps && file.WaterLevel == IslandFile.DeepWaterLevel, "saved as cigenfeatures.island: " + file.Objects.Count + " objects, " + savedProps + " with settings, sea level " + file.WaterLevel + " m (" + (new FileInfo(IslandSpawner.PathFor("cigenfeatures")).Length / 1024) + " KB)");
			DynamicIslands.NewIsland();
			float newLevel = DynamicIslands.EditorWaterLevel;
			DynamicIslands.LoadIsland("cigenfeatures");
			Check(ref ok, newLevel == IslandFile.DefaultWaterLevel && DynamicIslands.EditorWaterLevel == IslandFile.DeepWaterLevel, "New island goes back to the shallow seabed (" + newLevel + " m); loading cigenfeatures brings its deep sea (" + DynamicIslands.EditorWaterLevel + " m)");
			yield return null;
			IslandGenerator.FrameCamera(full);
			yield return new WaitForSecondsRealtime(1f);
			Screenshot(new[] { "gen_features" });
			yield return new WaitForSecondsRealtime(0.5f);
			if (ok) Log("PASS: generator settings"); else Fail("generator settings");
		}

		/// <summary>
		/// The sea floor and the life under water against Raft's measured islands: the depth profile (shelf, drop-off,
		/// floor), every kind placed is one Raft has there, corals at their depths and about as dense, ores on the steep
		/// slopes, the styles' differences (Balboa's barrels, Temperance's bare rock), the sliders, the shallow sea floor.
		/// </summary>
		static IEnumerator UnderwaterChecks(Action<bool> result)
		{
			bool ok = true;
			Vector3 size = IslandGenerator.BuildArea;
			int res = IslandGenerator.BuildResolution;
			float step = size.x / (res - 1);
			var files = DynamicIslands.instance.modlistEntry.modinfo.modFiles;
			Check(ref ok, RaftUnderwater.Loaded && files.ContainsKey(RaftUnderwater.FileName), "Raft's islands under water are measured (" + RaftUnderwater.FileName + " shipped: " + files.ContainsKey(RaftUnderwater.FileName) + "): " +
				string.Join(", ", Enumerable.Range(0, TerrainPainter.Styles.Length).Select(i => TerrainPainter.StyleName(i) + " " + RaftUnderwater.For(i).Things.Count + " kinds").ToArray()));

			var s = new IslandGenSettings { Seed = 2718, Radius = 60f, Height = 30f, Trees = 0f, Bushes = 0f, Rocks = 0f, BeachThings = 0f, Harvest = 0f, Water = 0.5f, SeaRocks = 0.5f, SeaFinds = 0.5f, Sunken = 0.5f };
			float sea = s.WaterLevel;
			float[,] m = IslandGenerator.HeightsMetres(s, size, res);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(m, res, step, x, z);

			// 1. The profile, along 32 rays from the middle: how deep 10, 25, 60 m and 150 m off the coast (Raft's big islands: about 6, 12, 45 m, then the floor)
			var at = new[] { 10f, 25f, 60f, 150f };
			var depths = at.Select(_ => new List<float>()).ToArray();
			for (int k = 0; k < 32; k++)
			{
				Vector2 dir = new Vector2(Mathf.Cos(k * Mathf.PI / 16f), Mathf.Sin(k * Mathf.PI / 16f));
				float d = 0f;
				while (d < 400f && ground(500f + dir.x * d, 500f + dir.y * d) > sea) d += 1f;
				for (int i = 0; i < at.Length; i++) depths[i].Add(sea - ground(500f + dir.x * (d + at[i]), 500f + dir.y * (d + at[i])));
			}
			float[] med = depths.Select(l => l.OrderBy(v => v).ElementAt(l.Count / 2)).ToArray();
			Check(ref ok, med[0] > 2f && med[0] < 14f && med[1] > 6f && med[1] < 40f && med[2] > 25f && med[3] > 100f,
				"the deep sea floor: " + string.Join(", ", at.Select((a, i) => med[i].ToString("F0") + " m deep " + a.ToString("F0") + " m off the coast").ToArray()) + " (Raft's islands: a shelf about 10 m deep, then the drop-off to 150-165 m)");

			// 2. The life under water: kinds, depths, density, slopes
			var rep = new GenReport();
			List<IslandObject> list = IslandGenerator.PlanAll(s, m, size, rep);
			List<IslandObject> list2 = IslandGenerator.PlanAll(s, m, size);
			bool same = list.Count == list2.Count && list.Zip(list2, (x, y) => x.Name == y.Name && x.Position == y.Position).All(t => t);
			SeaStyle raft = RaftUnderwater.For(TerrainPainter.Tropical);
			var raftKinds = new HashSet<string>(raft.Things.Select(t => t.Name));
			bool allRaft = list.All(o => raftKinds.Contains(o.Name)), allUnder = list.All(o => o.Position.y < sea);
			Func<IslandObject, float> depthOf = o => sea - ground(o.Position.x, o.Position.z);
			Func<IslandObject, float> slopeOf = o =>
			{
				float gx = (ground(o.Position.x + 2f, o.Position.z) - ground(o.Position.x - 2f, o.Position.z)) / 4f, gz = (ground(o.Position.x, o.Position.z + 2f) - ground(o.Position.x, o.Position.z - 2f)) / 4f;
				return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
			};
			Func<IEnumerable<float>, float> median = v => { var l = v.OrderBy(x => x).ToList(); return l.Count > 0 ? l[l.Count / 2] : -1f; };
			var corals = list.Where(o => RaftUnderwater.CategoryOf(o.Name) == IslandGenerator.CatWater).ToList();
			var ores = list.Where(o => o.Name.StartsWith("Pickup_Landmark_Iron") || o.Name.StartsWith("Pickup_Landmark_Copper")).ToList();
			var finds = list.Where(o => RaftUnderwater.CategoryOf(o.Name) == IslandGenerator.CatSeaFinds).Select(o => System.Text.RegularExpressions.Regex.Replace(o.Name, @"^Pickup_Landmark_|[ _]?\d.*$", "")).Distinct().OrderBy(n => n).ToList();
			float coralDepth = median(corals.Select(depthOf)), oreDepth = median(ores.Select(depthOf)), coralSlope = median(corals.Select(slopeOf)), oreSlope = median(ores.Select(slopeOf));
			// (corals per 1000 m² of ground 0-40 m deep near the island, against Raft's tropical islands)
			float area = 0f;
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) { float dd = sea - m[z, x]; if (dd > 0.3f && dd < 40f) area += step * step; }
			float coralDensity = corals.Count(o => depthOf(o) < 40f) * 1000f / Mathf.Max(1f, area), raftDensity = RaftUnderwater.DensityOf(TerrainPainter.Tropical, IslandGenerator.CatWater);
			Check(ref ok, same && allRaft && allUnder && corals.Count > 100 && ores.Count > 0 && finds.Count >= 4 && rep.Count(IslandGenerator.CatSeaRocks) > 0,
				"a tropical island's sea like Raft's: " + list.Count + " objects (" + rep.Describe() + "), every one a kind Raft has there: " + allRaft + ", all under water: " + allUnder + ", the same twice: " + same + "; finds: " + string.Join(", ", finds.ToArray()));
			Check(ref ok, coralDepth > 3f && coralDepth < 22f && oreSlope > coralSlope && coralDensity > raftDensity * 0.3f && coralDensity < raftDensity * 3f,
				"corals and plants mostly " + coralDepth.ToString("F0") + " m down on " + coralSlope.ToString("F0") + "° ground (Raft: 9-14 m), ores " + oreDepth.ToString("F0") + " m down on " + oreSlope.ToString("F0") + "° slopes; " +
				coralDensity.ToString("F0") + " corals per 1000 m² down to 40 m (Raft's tropical islands: " + raftDensity.ToString("F0") + ")");
			// (only rocks at the shore break the surface: out in water deeper than 4 m their tops stay under it)
			var sticking = list.Where(o => RaftUnderwater.CategoryOf(o.Name) == IslandGenerator.CatSeaRocks && depthOf(o) > 4f).Where(o =>
			{
				Bounds b;
				return PlaceableCatalog.LocalBounds(o.Name, out b) && o.Position.y + b.max.y * o.Scale.y > sea + 0.3f;
			}).Select(o => o.Name).ToList();
			Check(ref ok, sticking.Count == 0, "rocks out in deeper water stay under the surface (" + sticking.Count + " stick out" + (sticking.Count > 0 ? ": " + string.Join(", ", sticking.Take(5).ToArray()) : "") + ")");
			yield return null;

			// 3. The sliders: Teeming is about twice Like Raft (three times carpeted a lagoon's floor); none is none
			IslandGenSettings rich = s.Copy(); rich.Water = 1f;
			int richCorals = IslandGenerator.PlanAll(rich, m, size).Count(o => RaftUnderwater.CategoryOf(o.Name) == IslandGenerator.CatWater);
			Check(ref ok, richCorals > corals.Count * 1.5f && richCorals < corals.Count * 2.6f, "Teeming: " + richCorals + " corals and plants against " + corals.Count + " like Raft (about twice)");

			// 4. The styles: Balboa's barrels and no corals (forest), Temperance's bare rock (snowy)
			IslandGenSettings forest = s.Copy(); forest.Style = TerrainPainter.Forest;
			var fr = new GenReport();
			List<IslandObject> fl = IslandGenerator.PlanAll(forest, IslandGenerator.HeightsMetres(forest, size, res), size, fr);
			IslandGenSettings snowy = s.Copy(); snowy.Style = TerrainPainter.Snowy;
			var sr = new GenReport();
			List<IslandObject> sl = IslandGenerator.PlanAll(snowy, IslandGenerator.HeightsMetres(snowy, size, res), size, sr);
			Check(ref ok, fr.Count(IslandGenerator.CatSunken) > 5 && fr.Count(IslandGenerator.CatWater) == 0 && sr.Count(IslandGenerator.CatWater) == 0 && sr.Count(IslandGenerator.CatSeaRocks) > 0,
				"forest (Balboa): " + fr.Describe() + "; snowy (Temperance): " + sr.Describe());
			yield return null;

			// 5. A shallow sea floor: the old flat seabed 20 m down, the life under water on it
			IslandGenSettings shallow = s.Copy(); shallow.SeaFloor = IslandGenSettings.SeaFloorShallow;
			float[,] sm = IslandGenerator.HeightsMetres(shallow, size, res);
			List<IslandObject> shl = IslandGenerator.PlanAll(shallow, sm, size);
			float lowest = sm.Cast<float>().Min(), highest = sm.Cast<float>().Max();
			bool within = shl.All(o => o.Position.y < shallow.WaterLevel && o.Position.y > -30f);
			Check(ref ok, shallow.WaterLevel == IslandFile.DefaultWaterLevel && lowest >= 0f && Mathf.Abs(highest - shallow.WaterLevel - shallow.Height) < 2f && within && shl.Count > 50,
				"shallow sea floor: the sea at " + shallow.WaterLevel + " m above the terrain's base, " + shl.Count + " objects under water on it");
			result(ok);
		}

		#endregion

		#region Randomize existing

		[ConsoleCommand(name: "CIRandomizeTest", docs: "Dev, editor: every measured Raft island can be picked (picture and ground heights exist); \"something new like it\" gives settings from its measurements; \"a variation of it\" keeps its size and height; the window picks and previews them")]
		public static void RandomizeTestCommand()
		{
			StartTest(RandomizeTestRoutine());
		}

		static IEnumerator RandomizeTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			List<RaftIsland> offered = RaftIslands.Offered;
			Check(ref ok, offered.Count >= 25, offered.Count + " of Raft's islands offered");
			// (the measurement ships with the mod: a Raft without Mods\\DynamicIslands\\raft_islands.txt still has it)
			var files = DynamicIslands.instance.modlistEntry.modinfo.modFiles;
			int shippedThumbs = files.Keys.Count(k => k.StartsWith(RaftIslands.ThumbFolder + "/")), shippedHeights = files.Keys.Count(k => k.StartsWith(RaftIslands.HeightFolder + "/"));
			Check(ref ok, files.ContainsKey(RaftIslands.FileName) && shippedThumbs >= offered.Count && shippedHeights >= offered.Count, "shipped in the mod: " + RaftIslands.FileName + " " + files.ContainsKey(RaftIslands.FileName) + ", " + shippedThumbs + " pictures, " + shippedHeights + " height files");
			Vector3 size = IslandGenerator.BuildArea;
			const int Res = 257;
			float step = size.x / (Res - 1);
			int pictures = 0, fields = 0, fits = 0;
			var misfits = new List<string>();
			foreach (RaftIsland isl in offered)
			{
				if (RaftIslands.Thumbnail(isl) != null) pictures++;
				if (RaftIslands.Heights(isl) != null) fields++;
				IslandGenSettings like = RaftIslands.LikeIt(isl, new IslandGenSettings { Seed = 99 });
				bool likeOk = Mathf.Abs(like.Radius - Mathf.Clamp(isl.Radius, IslandGenSettings.MinRadius, IslandGenSettings.MaxRadius)) < 0.01f && Mathf.Abs(like.Height - Mathf.Clamp(isl.Top, IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight)) < 0.01f && like.Style == isl.StyleIndex;
				IslandGenSettings vari = RaftIslands.VariationOf(isl, new IslandGenSettings { Seed = 99 });
				// (LM7: in the terrain it gets - Temperance's is bigger than the build area)
				Vector3 varSize = IslandGenerator.AreaFor(vari);
				float varStep = varSize.x / (Res - 1);
				float[,] m = IslandGenerator.HeightsMetres(vari, varSize, Res);
				int land = m.Cast<float>().Count(v => v > vari.WaterLevel);
				float area = land * varStep * varStep, peak = m.Cast<float>().Max() - vari.WaterLevel;
				// (a variation at its own size: about as much land as the real one, and exactly its height - Temperance too, LM7)
				float expect = isl.Area * Mathf.Pow(vari.Radius / Mathf.Max(1f, isl.Radius), 2f);
				bool variOk = Mathf.Abs(peak - vari.Height) < Mathf.Max(1f, vari.Height * 0.05f) && area > expect * 0.5f && area < expect * 1.6f;
				// (a story island with a look that shapes its ground - Caravan's mesas, Tangaroa, Utopia, Varuna, the radio
				// tower - gets the look's own size and height, not the measured ones: its look carried on both is the check)
				Remakes.Look look = Remakes.LookFor(isl.Scene);
				if (look != null && look.Shape != null) { likeOk = like.Look == look.Id; variOk = vari.Look == look.Id; }
				if (likeOk && variOk) fits++;
				else misfits.Add(isl.Label + " (like " + likeOk + ", variation land " + area.ToString("F0") + " of " + isl.Area.ToString("F0") + " m², top " + peak.ToString("F0") + " of " + vari.Height.ToString("F0") + ")");
				yield return null;
			}
			Check(ref ok, pictures == offered.Count && fields == offered.Count, "pictures " + pictures + ", ground heights " + fields + " of " + offered.Count);
			Check(ref ok, fits == offered.Count, fits + " of " + offered.Count + " give settings from their measurements and variations of their size and height" + (misfits.Count > 0 ? "; not: " + string.Join("; ", misfits.ToArray()) : ""));

			// The window: pick one, both ways, and see the preview change
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow.ShowTab(GeneratorWindow.TabRandomize);
			RaftIsland big = offered.FirstOrDefault(i => i.Label.Contains("Twin peak")) ?? offered[0];
			GeneratorWindow.Pick(big, false);
			yield return null;
			IslandGenSettings likeSettings = GeneratorWindow.Instance.Settings.Copy();
			GeneratorWindow.Pick(big, true);
			yield return null;
			IslandGenSettings variSettings = GeneratorWindow.Instance.Settings.Copy();
			Texture pic = GeneratorWindow.PreviewNow();
			Check(ref ok, likeSettings.Source == "" && likeSettings.Shape == IslandShapes.TwinPeaks && variSettings.Source == big.Scene && pic != null,
				"the window picks " + big.Label + ": like it = " + IslandShapes.Names[likeSettings.Shape] + " layout, variation = its own ground; preview " + (pic != null ? pic.width + " px" : "missing"));
			yield return new WaitForSecondsRealtime(0.4f);
			Screenshot(new[] { "gen_randomize" });
			yield return new WaitForSecondsRealtime(0.6f);
			// ... and generate the variation into the editor
			variSettings.Stretch = 1.4f; variSettings.StretchAngle = 60f; variSettings.SourceRoughen = 0.5f; variSettings.Erosion = 0.5f;
			GeneratorWindow.Close();
			int n = IslandGenerator.GenerateInEditor(variSettings);
			IslandGenerator.FrameCamera(variSettings);
			GenReport r = IslandGenerator.LastReport;
			Check(ref ok, n > 50 && Mathf.Abs(r.Top - variSettings.Height) < variSettings.Height * 0.06f, "a stretched, roughened, eroded variation of " + big.Label + ": land " + r.LandLength.ToString("F0") + " x " + r.LandWidth.ToString("F0") + " m, top " + r.Top.ToString("F0") + " m, " + n + " objects in " + r.Seconds.ToString("F1") + " s");
			yield return new WaitForSecondsRealtime(1f);
			Screenshot(new[] { "gen_variation" });
			yield return new WaitForSecondsRealtime(0.5f);
			if (ok) Log("PASS: randomize existing"); else Fail("randomize existing");
		}

		#endregion

		#region How dense is too dense

		[ConsoleCommand(name: "CIGenBench", docs: "Dev, editor: generates islands with about 1000, 3000, 6000 and 9000 objects and measures the cost: generating, the editor's frame time, the file's size, saving and loading. Writes the table to the log (README: generator costs)")]
		public static void GenBenchCommand()
		{
			StartTest(GenBenchRoutine());
		}

		static IEnumerator GenBenchRoutine()
		{
			yield return WaitForEditor(false);
			var rows = new List<string> { "objects | generate s | editor ms/frame | file KB | save s | load s | free walk m (median) | walks blocked within 5 m" };
			float[] levels = { 0.22f, 0.4f, 0.6f, 1f, 1.01f };
			foreach (float setting in levels)
			{
				float level = setting;
				// (the last one: a Balboa-sized jungle, up to the most objects an island gets)
				bool big = setting > 1f;
				level = Mathf.Min(1f, level);
				var s = new IslandGenSettings { Seed = 2026, Radius = big ? RaftIslands.BalboaRadius : 110f, Height = big ? 90f : 50f, Trees = level, Bushes = level, Rocks = Mathf.Min(level, 0.7f), Harvest = Mathf.Min(level, 0.6f), BeachThings = 0.4f, Water = level * 0.6f };
				float t0 = Time.realtimeSinceStartup;
				int n = IslandGenerator.GenerateInEditor(s);
				float gen = Time.realtimeSinceStartup - t0;
				IslandGenerator.FrameCamera(s);
				yield return new WaitForSecondsRealtime(1f);
				float f0 = Time.realtimeSinceStartup;
				for (int i = 0; i < 60; i++) yield return null;
				float frame = (Time.realtimeSinceStartup - f0) / 60f * 1000f;
				float s0 = Time.realtimeSinceStartup;
				DynamicIslands.SaveIsland("cigenbench");
				float save = Time.realtimeSinceStartup - s0;
				long kb = new FileInfo(IslandSpawner.PathFor("cigenbench")).Length / 1024;
				float l0 = Time.realtimeSinceStartup;
				DynamicIslands.LoadIsland("cigenbench");
				float load = Time.realtimeSinceStartup - l0;
				yield return null;
				float blocked;
				float walk = Walkability(out blocked);
				string row = n + " | " + gen.ToString("F1") + " | " + frame.ToString("F1") + " | " + kb + " | " + save.ToString("F2") + " | " + load.ToString("F1") + " | " + walk.ToString("F1") + " | " + blocked.ToString("P0");
				rows.Add(row);
				Log("  " + row + "  (" + IslandGenerator.LastReport.Describe() + ")");
				Screenshot(new[] { "gen_bench_" + n });
				yield return new WaitForSecondsRealtime(0.5f);
			}
			foreach (string r in rows) Log("BENCH " + r);
			// For the world test (CIGenWorld): a jungle with animals and loot
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 4711, Radius = 110f, Height = 50f, Trees = 1f, Bushes = 1f, Rocks = 0.7f, Harvest = 0.6f, BeachThings = 0.4f, Water = 0.5f, Hostiles = 8, Friendly = 4, SeaLife = 3, Loot = 12, LootMin = 1, LootMax = 5 });
			DynamicIslands.SaveIsland("cigenjungle");
			Log("PASS: generator costs measured");
		}

		/// <summary>
		/// How far a person gets walking straight across the land: from 300 random spots on the land, in random
		/// directions, 0.5 m steps along the ground until an object's collider (a trunk, a rock) is in the way of a body
		/// 0.6 m wide and 1.7 m tall, up to 20 m. Returns the median distance; blocked = the share stopped within 5 m.
		/// </summary>
		static float Walkability(out float blocked)
		{
			Physics.SyncTransforms();
			Terrain terrain = terraineditor.terrain;
			Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
			float sea = origin.y + DynamicIslands.EditorWaterLevel;
			var rnd = new System.Random(5);
			var runs = new List<float>();
			var hits = new Collider[16];
			Func<Vector3, bool> free = q =>
			{
				int n = Physics.OverlapCapsuleNonAlloc(q + Vector3.up * 0.65f, q + Vector3.up * 1.4f, 0.3f, hits, ~0, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider) && hits[i].GetComponentInParent<EditorGameObject>() != null) return false;
				return true;
			};
			for (int tries = 0; runs.Count < 300 && tries < 5000; tries++)
			{
				Vector3 p = origin + new Vector3(size.x / 2f + ((float)rnd.NextDouble() - 0.5f) * 500f, 0, size.z / 2f + ((float)rnd.NextDouble() - 0.5f) * 500f);
				p.y = terrain.SampleHeight(p) + origin.y;
				if (p.y < sea + 1f || !free(p)) continue;
				float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
				Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
				float d = 0f;
				for (float t = 0.5f; t <= 20f; t += 0.5f)
				{
					Vector3 q = p + dir * t;
					q.y = terrain.SampleHeight(q) + origin.y;
					if (q.y < sea + 0.3f) { d = 20f; break; } // (reached the water: nothing in the way)
					if (!free(q)) break;
					d = t;
				}
				runs.Add(d);
			}
			if (runs.Count == 0) { blocked = 0f; return 0f; }
			runs.Sort();
			blocked = runs.Count(r => r < 5f) / (float)runs.Count;
			return runs[runs.Count / 2];
		}

		[ConsoleCommand(name: "CIGenWorld", docs: "Dev, in game (host): spawns a saved (dense) island 330 m ahead and measures the cost: spawning, frames before and while standing on it, the creatures' NavMesh; then removes it and puts the player back on the raft. CIGenWorld <island> [keep]")]
		public static void GenWorldCommand(string[] args)
		{
			string name = args != null ? string.Join(" ", args.Where(a => a != "keep").ToArray()) : "";
			if (name.Length == 0) { Fail("usage: CIGenWorld <island> [keep]"); return; }
			StartTest(GenWorldRoutine(name, args.Contains("keep")));
		}

		static IEnumerator GenWorldRoutine(string name, bool keep)
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue) { Fail("not in a world"); yield break; }
			if (!File.Exists(IslandSpawner.PathFor(name))) { Fail("no island file '" + name + "'"); yield break; }
			Func<int, IEnumerator> frames = null;
			float measured = 0f;
			frames = count => FrameTime(count, ms => measured = ms);
			yield return frames(120);
			float before = measured;
			string navmesh = null;
			Application.LogCallback watch = (msg, trace, type) => { if (msg.Contains("NavMesh for")) navmesh = msg.Substring(msg.IndexOf("NavMesh")); };
			Application.logMessageReceived += watch;
			int count0 = IslandWorldState.Islands.Count;
			Vector3 pos = raft.Value + Vector3.forward * 330f; pos.y = 0f;
			float t0 = Time.realtimeSinceStartup;
			yield return DynamicIslands.instance.SpawnIslandFile(name, pos, true);
			float spawn = Time.realtimeSinceStartup - t0;
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(count0).FirstOrDefault();
			if (entry == null || entry.Root == null) { Application.logMessageReceived -= watch; Fail("the island did not spawn"); yield break; }
			Transform objects = entry.Root.transform.Find("Objects");
			int n = objects != null ? objects.childCount : 0;
			yield return frames(120);
			float after = measured;
			yield return StandRoutine(entry.Root);
			yield return new WaitForSecondsRealtime(2f);
			yield return frames(120);
			float standing = measured;
			for (int i = 0; i < 20 && navmesh == null; i++) yield return new WaitForSecondsRealtime(0.5f);
			Application.logMessageReceived -= watch;
			Screenshot(new[] { "gen_world_" + name });
			yield return new WaitForSecondsRealtime(0.6f);
			Log(string.Format("BENCH world '{0}': {1} objects, spawned in {2:F1} s; frame {3:F1} ms before, {4:F1} ms with it 330 m away, {5:F1} ms standing on it; {6}",
				name, n, spawn, before, after, standing, navmesh ?? "no creatures (no NavMesh)"));
			if (!keep)
			{
				OnRaftCommand();
				yield return null;
				IslandWorldState.Remove(entry.Name);
			}
			if (n > 0) Log("PASS: dense island in a world"); else Fail("dense island in a world: no objects");
		}

		[ConsoleCommand(name: "CIDiveShots", docs: "Dev, in game (host): spawns a saved island 330 m ahead and dives around it, taking pictures with Raft's own water: at the surface, over the reef on the shelf, at the drop-off and deep on the slope (shot_dive_<island>_*.png); then back on the raft. CIDiveShots <island> [keep]")]
		public static void DiveShotsCommand(string[] args)
		{
			if (args == null || args.Length == 0) { Log("Usage: CIDiveShots <island> [keep]"); return; }
			bool keep = args.Length > 1 && args[args.Length - 1] == "keep";
			StartTest(DiveShotsRoutine(string.Join(" ", args.Take(keep ? args.Length - 1 : args.Length).ToArray()), keep));
		}

		/// <summary>Turns the player's view (Raft's MouseLook keeps its own angles, so they are set too).</summary>
		static void Look(Network_Player player, float yaw, float pitch)
		{
			foreach (MouseLook ml in player.GetComponentsInChildren<MouseLook>(true))
			{
				System.Reflection.BindingFlags f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
				if (ml.axes == MouseLook.RotationAxes.MouseX) { ml.rotX = yaw; typeof(MouseLook).GetField("targetRotX", f).SetValue(ml, yaw); }
				else if (ml.axes == MouseLook.RotationAxes.MouseY) { ml.rotY = pitch; typeof(MouseLook).GetField("targetRotY", f).SetValue(ml, pitch); }
			}
		}

		static IEnumerator DiveShotsRoutine(string name, bool keep)
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			if (!raft.HasValue || player == null) { Fail("not in a world"); yield break; }
			// ("real" / "real:Big" ...: one of Raft's own islands, Landmark_<kind>, for comparing the two under water)
			bool real = name == "real" || name.StartsWith("real:");
			if (!real && !File.Exists(IslandSpawner.PathFor(name))) { Fail("no island file '" + name + "'"); yield break; }
			yield return EnsureAlive();
			int count0 = IslandWorldState.Islands.Count;
			Vector3 pos = raft.Value + Vector3.forward * 330f; pos.y = 0f;
			float t0 = Time.realtimeSinceStartup;
			IslandWorldState.Entry entry = null;
			Terrain terrain = null;
			if (real)
			{
				ChunkPointType kind;
				try { kind = (ChunkPointType)Enum.Parse(typeof(ChunkPointType), "Landmark_" + (name.Length > 5 ? name.Substring(5) : "Small")); }
				catch { Fail("no Raft island kind '" + name.Substring(5) + "'"); yield break; }
				// (the nearest of that kind already in the world, else one called up ahead of the raft)
				Vector3 at = raft.Value;
				Func<Landmark> nearest = () => UnityEngine.Object.FindObjectsOfType<Landmark>()
					.Where(l => l.name.Contains("#" + kind + "#") && l.GetComponentInChildren<Terrain>() != null)
					.OrderBy(l => (l.transform.position - at).sqrMagnitude).FirstOrDefault();
				Landmark landmark = nearest();
				if (landmark == null) ComponentManager<ChunkManager>.Value.AddChunkPointCheat(kind, pos);
				for (float w = 0f; w < 30f && landmark == null; w += 0.5f)
				{
					yield return new WaitForSeconds(0.5f);
					landmark = nearest();
				}
				terrain = landmark != null ? landmark.GetComponentInChildren<Terrain>() : null;
				if (terrain != null)
				{
					pos = terrain.transform.position + terrain.terrainData.size / 2f; pos.y = 0f;
					name = "real_" + landmark.name.Replace(" ", "").Replace("(Clone)", "");
				}
			}
			else
			{
				yield return DynamicIslands.instance.SpawnIslandFile(name, pos, true);
				entry = IslandWorldState.Islands.Skip(count0).FirstOrDefault();
				if (entry == null || entry.Root == null) { Fail("the island did not spawn"); yield break; }
				terrain = entry.Root.GetComponentInChildren<Terrain>();
			}
			float spawn = Time.realtimeSinceStartup - t0;
			Log("Spawned '" + name + "' in " + spawn.ToString("F1") + " s: terrain " + (terrain != null ? terrain.terrainData.size.x.ToString("F0") + " m, " + terrain.terrainData.heightmapResolution + " samples, from " + terrain.transform.position.y.ToString("F0") + " m" : "none"));
			if (terrain == null) { Fail("no terrain"); yield break; }
			yield return new WaitForSeconds(2f);
			Func<float, float, float> ground = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
			// The coast towards the raft
			Vector3 toRaft = (raft.Value - pos); toRaft.y = 0f; toRaft.Normalize();
			float d = 0f;
			while (d < 400f && ground(pos.x + toRaft.x * d, pos.z + toRaft.z * d) > 0f) d += 1f;
			Vector3 coast = pos + toRaft * d;
			float yawToIsland = Quaternion.LookRotation(-toRaft).eulerAngles.y;
			var views = new[]
			{
				new { Key = "surface", Out = 45f, Y = 0.2f, Pitch = 5f },
				new { Key = "floor", Out = 10f, Y = -0.8f, Pitch = 50f },
				new { Key = "reef", Out = 14f, Y = -4f, Pitch = 20f },
				new { Key = "shelf", Out = 25f, Y = -8f, Pitch = 10f },
				new { Key = "dropoff", Out = 45f, Y = -16f, Pitch = 35f },
				new { Key = "deep", Out = 75f, Y = -35f, Pitch = 0f },
			};
			CharacterController cc = player.PersonController.controller;
			foreach (var v in views)
			{
				Vector3 at = coast + toRaft * v.Out;
				at.y = Mathf.Max(v.Y, ground(at.x, at.z) + 2f);
				cc.enabled = false;
				player.transform.position = at;
				cc.enabled = true;
				Look(player, yawToIsland, v.Pitch);
				yield return new WaitForSeconds(1.2f);
				Look(player, yawToIsland, v.Pitch);
				yield return new WaitForSeconds(0.3f);
				Log("  " + v.Key + ": " + v.Out + " m off the coast, " + (-at.y).ToString("F0") + " m deep over ground " + (-ground(at.x, at.z)).ToString("F0") + " m deep");
				Screenshot(new[] { "dive_" + name + "_" + v.Key });
				yield return new WaitForSeconds(0.8f);
			}
			yield return EnsureAlive();
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			if (!keep && entry != null) IslandWorldState.Remove(entry.Name);
			Log("PASS: dive pictures of '" + name + "' (coast " + d.ToString("F0") + " m from its middle)");
		}

		/// <summary>The average frame time (ms) over some frames.</summary>
		static IEnumerator FrameTime(int count, Action<float> result)
		{
			float t0 = Time.realtimeSinceStartup;
			for (int i = 0; i < count; i++) yield return null;
			result((Time.realtimeSinceStartup - t0) / count * 1000f);
		}

		#endregion

		#region The tool panel fits

		[ConsoleCommand(name: "CIPanelFit", docs: "Dev, editor: the left tool panel with the tallest and widest inspectors (a creature with custom colours, a chest with many items, each zone): nothing wider than the panel, never below the status bar, scrolls when too tall")]
		public static void PanelFitCommand()
		{
			StartTest(PanelFitRoutine());
		}

		static IEnumerator PanelFitRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			GameObject root = GameObject.Find("PlacedObjects");
			Vector3 at = terraineditor.terrain.transform.position + new Vector3(520f, DynamicIslands.EditorWaterLevel + 2f, 520f);
			var kinds = new[] { "Log", "Creature_Boar", "Loot_Chest", ContentCatalog.TriggerZone, ContentCatalog.AtmosphereZoneName, ContentCatalog.SoundZoneName, "Note_Paper" };
			EditorUI.SetTab(TAB.ObjectPlace);
			RectTransform frame = EditorUI.ToolFrame;
			RectTransform viewport = (RectTransform)frame.Find("Viewport");
			RectTransform status = (RectTransform)EditorUI.Canvas.transform.Find("StatusBar");
			for (int k = 0; k < kinds.Length; k++)
			{
				EditorGameObject e = PlaceForTest(kinds[k], at + new Vector3(k * 5f, 0, 0), root.transform);
				if (e == null) { Log("  (no " + kinds[k] + ")"); continue; }
				if (kinds[k] == "Loot_Chest")
					e.Props[ObjectProps.LootItems] = "Plank*1;Plastic*1;Rope*1;Nail*1;Stone*1;Scrap*1;MetalOre*1;CopperOre*1;Bolt*1;Hinge*1";
				TransformGizmoSelect(e.transform);
				ObjectInspector.Refresh();
				yield return null; yield return null;
				// The tallest look: a creature with its custom colour sliders open
				if (kinds[k] == "Creature_Boar")
				{
					Button custom = EditorUI.Canvas.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Button_Custom colour...");
					if (custom != null) custom.onClick.Invoke();
					yield return null; yield return null;
				}
				yield return null;
				Vector3[] v = new Vector3[4], f = new Vector3[4], sb = new Vector3[4];
				viewport.GetWorldCorners(v);
				frame.GetWorldCorners(f);
				status.GetWorldCorners(sb);
				float widest = float.MinValue; string widestName = "";
				foreach (RectTransform r in viewport.GetComponentsInChildren<RectTransform>(false))
				{
					if (r.GetComponent<Graphic>() == null) continue;
					Vector3[] c = new Vector3[4];
					r.GetWorldCorners(c);
					if (c[2].x > widest) { widest = c[2].x; widestName = r.name + " in " + (r.parent != null ? r.parent.name : ""); }
				}
				RectTransform content = (RectTransform)viewport.Find("ToolPanel");
				bool scrolls = content.rect.height > viewport.rect.height + 1f;
				bool fitsWide = widest <= v[2].x + 1f, fitsHigh = f[0].y >= sb[1].y - 1f;
				Check(ref ok, fitsWide && fitsHigh, kinds[k] + " selected: widest part ends at " + widest.ToString("F0") + " px (panel " + v[2].x.ToString("F0") + ", " + widestName + "), panel bottom " + f[0].y.ToString("F0") +
					" px above the status bar's top " + sb[1].y.ToString("F0") + " px" + (scrolls ? ", scrolls (" + content.rect.height.ToString("F0") + " of " + viewport.rect.height.ToString("F0") + ")" : ""));
				if (kinds[k] == "Creature_Boar") { Screenshot(new[] { "panel_creature" }); yield return new WaitForSecondsRealtime(0.5f); }
				if (kinds[k] == "Log") { Screenshot(new[] { "panel_log" }); yield return new WaitForSecondsRealtime(0.5f); }
			}
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			if (ok) Log("PASS: the tool panel fits"); else Fail("the tool panel fits");
		}

		#endregion

		#region Pictures of the window

		[ConsoleCommand(name: "CIGenShots", docs: "Dev, editor: screenshots of the generator window, each tab (shot_gen_normal / _randomize / _ready, and the Normal tab scrolled down), and a help popup")]
		public static void GenShotsCommand()
		{
			StartTest(GenShotsRoutine());
		}

		static IEnumerator GenShotsRoutine()
		{
			yield return WaitForEditor(false);
			GeneratorWindow.Open();
			string[] names = { "normal", "randomize", "ready" };
			for (int t = 0; t < 3; t++)
			{
				GeneratorWindow.ShowTab(t);
				if (t == GeneratorWindow.TabRandomize && RaftIslands.Offered.Count > 0) GeneratorWindow.Pick(RaftIslands.Offered.FirstOrDefault(i => i.Label.StartsWith("Big island")) ?? RaftIslands.Offered[0], true);
				GeneratorWindow.PreviewNow();
				yield return new WaitForSecondsRealtime(0.6f);
				Screenshot(new[] { "gen_" + names[t] });
				yield return new WaitForSecondsRealtime(0.6f);
				if (t == GeneratorWindow.TabNormal)
				{
					// Further down the Normal tab, and a help popup
					ScrollRect scroll = GeneratorWindow.Instance.GetComponentsInChildren<ScrollRect>(false).FirstOrDefault();
					for (float pos = 0.62f; pos >= 0f && scroll != null; pos -= 0.62f)
					{
						scroll.verticalNormalizedPosition = pos;
						yield return new WaitForSecondsRealtime(0.4f);
						Button help = GeneratorWindow.Instance.GetComponentsInChildren<Button>(false).Where(b => b.name == "Help").Skip(pos > 0.3f ? 12 : 22).FirstOrDefault();
						if (help != null) help.onClick.Invoke();
						yield return new WaitForSecondsRealtime(0.3f);
						Screenshot(new[] { "gen_normal_" + (pos > 0.3f ? "middle" : "bottom") });
						yield return new WaitForSecondsRealtime(0.6f);
						if (help != null) help.onClick.Invoke();
					}
				}
			}
			GeneratorWindow.Close();
			yield return null;
			Log(UIKit.ShownHelp == null ? "PASS: generator pictures" : "FAIL: a help popup stayed open");
		}

		#endregion
	}
}
