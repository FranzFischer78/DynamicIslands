using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using RuntimeGizmos;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// More tests (NextTask_MoreTestsAndTestEverything.md): the generator more broadly (every layout x style x sea
	/// floor, the editor against the sailing path, presets, deep variations, the window), the life under water and
	/// the deep sea floor in a world, "can players reach it" against Raft's own controller, two players and deep
	/// islands, a long sail, and helpers for the real mouse and key tests of tools\alltests.ps1.
	/// </summary>
	public static partial class DevTests
	{
		static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

		#region The generator: every layout x style x sea floor

		[ConsoleCommand(name: "CIGenMatrix", docs: "Dev, editor: every layout x style x sea floor (8 x 5 x 2) with its own seed: no exceptions, the top as asked, flat edges, no NaN, every object on the ground (or sunk as Raft's are), inside the build area, nothing above the sea offshore, under the object cap, the same twice, and the reach line in time")]
		public static void GenMatrixCommand()
		{
			StartTest(GenMatrixRoutine());
		}

		static IEnumerator GenMatrixRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); yield break; }
			bool ok = true;
			Vector3 size = IslandGenerator.BuildArea;
			int res = IslandGenerator.BuildResolution;
			float step = size.x / (res - 1);
			int combos = 0, bad = 0;
			float slowestReach = 0f, slowestGen = 0f;
			var problems = new List<string>();
			var bounds = new Dictionary<string, Bounds>();
			Func<string, Bounds?> localBounds = name =>
			{
				Bounds b;
				if (bounds.TryGetValue(name, out b)) return b;
				if (!PlaceableCatalog.LocalBounds(name, out b)) return null;
				bounds[name] = b;
				return b;
			};
			for (int shape = 0; shape < IslandShapes.Names.Length; shape++)
				for (int style = 0; style < TerrainPainter.Styles.Length; style++)
					for (int floor = 0; floor < 2; floor++)
					{
						int seed = 1000 + shape * 100 + style * 10 + floor;
						var s = new IslandGenSettings { Seed = seed, Shape = shape, Style = style, SeaFloor = floor, Radius = 50f + 12f * (seed % 5), Height = 18f + 7f * (seed % 7), ObjectDensity = 0.5f, Hostiles = 2, Friendly = 1, Loot = 3 };
						string label = IslandShapes.Names[shape] + " / " + TerrainPainter.StyleName(style) + " / " + (floor == 0 ? "deep" : "shallow") + " (seed " + seed + ")";
						combos++;
						var why = new List<string>();
						try
						{
							float t0 = Time.realtimeSinceStartup;
							float[,] m = IslandGenerator.HeightsMetres(s, size, res);
							var rep = new GenReport();
							List<IslandObject> objs = IslandGenerator.PlanAll(s, m, size, rep);
							float gen = Time.realtimeSinceStartup - t0;
							slowestGen = Mathf.Max(slowestGen, gen);
							float sea = s.WaterLevel;
							bool nan = m.Cast<float>().Any(v => float.IsNaN(v) || float.IsInfinity(v));
							float peak = m.Cast<float>().Max() - sea;
							bool edges = Enumerable.Range(0, res).All(i => m[0, i] < 0.01f && m[res - 1, i] < 0.01f && m[i, 0] < 0.01f && m[i, res - 1] < 0.01f);
							if (nan) why.Add("NaN heights");
							if (Mathf.Abs(peak - s.Height) > Mathf.Max(1f, s.Height * 0.05f)) why.Add("top " + peak.ToString("F1") + " m, asked " + s.Height.ToString("F0"));
							if (!edges) why.Add("edges not flat");
							if (objs.Count > IslandGenerator.MaxObjects) why.Add(objs.Count + " objects (cap " + IslandGenerator.MaxObjects + ")");
							int floating = 0, buried = 0, outside = 0, above = 0;
							string exFloat = null, exBuried = null, exAbove = null;
							foreach (IslandObject o in objs)
							{
								if (o.Position.x < 0f || o.Position.z < 0f || o.Position.x > size.x || o.Position.z > size.z) { outside++; continue; }
								if (float.IsNaN(o.Position.y)) { floating++; continue; }
								// (the ground as the terrain has it, its triangles - what the generator sets things on since 11aa73a; the
								// blended height lies metres off it on a steep twisted cell: rocks read 1.2 m up, a chest 3.4 m down)
								float g = IslandGenerator.TerrainSurface(m, step, o.Position.x, o.Position.z);
								Bounds? lb = localBounds(o.Name);
								float tall = lb.HasValue ? lb.Value.size.y * Mathf.Abs(o.Scale.y) : 2f;
								float big = lb.HasValue ? Vector3.Scale(lb.Value.size, o.Scale).magnitude : 2f;
								// (on the ground: raised at most a little - flying creatures' spots hover 2 m up; sunk at most as deep as
								// the object is big: the generator sinks sea rocks and wreckage by Raft's measured ratio of their size)
								float lift = ContentCatalog.IsCreature(o.Name) ? 2.2f : 0.6f + tall * 0.1f;
								if (o.Position.y > g + lift) { floating++; if (exFloat == null) exFloat = o.Name + " " + (o.Position.y - g).ToString("F1") + " m up"; }
								else if (o.Position.y < g - 0.5f - big) { buried++; if (exBuried == null) exBuried = o.Name + " " + (g - o.Position.y).ToString("F1") + " m down"; }
								// (offshore: ground more than a metre under the sea keeps the object's base under it)
								if (g < sea - 1f && o.Position.y > sea + 0.05f) { above++; if (exAbove == null) exAbove = o.Name; }
							}
							if (floating > 0) why.Add(floating + " floating (" + exFloat + ")");
							if (buried > 0) why.Add(buried + " buried (" + exBuried + ")");
							if (outside > 0) why.Add(outside + " outside the build area");
							if (above > 0) why.Add(above + " above the sea offshore (" + exAbove + ")");
							// (the same twice: every eighth one, and the list, not only the count)
							if (combos % 8 == 1)
							{
								float[,] m2 = IslandGenerator.HeightsMetres(s, size, res);
								List<IslandObject> objs2 = IslandGenerator.PlanAll(s, m2, size);
								bool same = m.Cast<float>().SequenceEqual(m2.Cast<float>()) && objs.Count == objs2.Count && objs.Zip(objs2, (a, b) => a.Name == b.Name && a.Position == b.Position).All(x => x);
								if (!same) why.Add("not the same twice");
							}
							float r0 = Time.realtimeSinceStartup;
							ReachResult reach = IslandReach.Assess(s);
							float rs = Time.realtimeSinceStartup - r0;
							slowestReach = Mathf.Max(slowestReach, rs);
							if (reach == null || string.IsNullOrEmpty(reach.Text)) why.Add("no reach line");
							if (rs > 1.5f) why.Add("reach took " + rs.ToString("F2") + " s");
							Log("  " + label + ": top " + peak.ToString("F1") + "/" + s.Height.ToString("F0") + " m, " + objs.Count + " objects (" + rep.Describe() + ") in " + gen.ToString("F2") + " s; reach " + (reach != null ? reach.Level.ToString() : "-") + " in " + (rs * 1000f).ToString("F0") + " ms" + (why.Count > 0 ? "  <- " + string.Join("; ", why.ToArray()) : ""));
						}
						catch (Exception e) { why.Add("exception " + e.GetType().Name + ": " + e.Message); Log("  " + label + ": " + e); }
						if (why.Count > 0) { bad++; problems.Add(label + ": " + string.Join("; ", why.ToArray())); }
						yield return null;
					}
			Check(ref ok, bad == 0, combos + " layouts x styles x sea floors generated; " + bad + " with problems" + (problems.Count > 0 ? ": " + string.Join(" | ", problems.Take(8).ToArray()) : ""));
			Check(ref ok, slowestReach < 1.5f, "the slowest reach line took " + (slowestReach * 1000f).ToString("F0") + " ms; the slowest island " + slowestGen.ToString("F1") + " s");
			if (ok) Log("PASS: every layout, style and sea floor"); else Fail("every layout, style and sea floor");
		}

		#endregion

		#region The editor's island and the sailing path's file

		[ConsoleCommand(name: "CIGenParity", docs: "Dev, editor: generating in the editor and IslandGenerator.CreateFile (islands made while sailing, map types) give the same heights and the same objects for the same settings")]
		public static void GenParityCommand()
		{
			StartTest(GenParityRoutine());
		}

		static IEnumerator GenParityRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("open the editor first"); yield break; }
			bool ok = true;
			var cases = new[]
			{
				new IslandGenSettings { Seed = 4242, Radius = 70f, Height = 40f, Trees = 0.4f, Bushes = 0.3f, Rocks = 0.3f, Harvest = 0.3f, Water = 0.5f, SeaFinds = 0.5f, Hostiles = 3, Loot = 4 },
				new IslandGenSettings { Seed = 777, Radius = 55f, Height = 25f, Shape = IslandShapes.Atoll, Style = TerrainPainter.Snowy, SeaFloor = IslandGenSettings.SeaFloorShallow, ObjectDensity = 0.6f, Friendly = 2 },
				new IslandGenSettings { Seed = 31, Radius = 90f, Height = 60f, Shape = IslandShapes.Plateau, Style = TerrainPainter.Volcanic, Cliffs = 0.5f, Erosion = 0.5f, Valleys = 0.5f, ObjectDensity = 0.4f },
			};
			Terrain terrain = terraineditor.terrain;
			foreach (IslandGenSettings s in cases)
			{
				int n = IslandGenerator.GenerateInEditor(s);
				yield return null;
				TerrainData data = terrain.terrainData;
				int res = data.heightmapResolution;
				float[,] h = data.GetHeights(0, 0, res, res);
				IslandFile f = IslandGenerator.CreateFile(s, "ciparity");
				bool sameGrid = f.HeightmapResolution == res && Mathf.Abs(f.TerrainSize.x - data.size.x) < 0.01f && Mathf.Abs(f.TerrainSize.y - data.size.y) < 0.01f;
				float worst = 0f;
				if (sameGrid) for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) worst = Mathf.Max(worst, Mathf.Abs(h[z, x] - f.Heights[z, x]) * data.size.y);
				// The objects the editor shows, as the file would store them (relative to the terrain's corner)
				Transform holder = GameObject.Find("PlacedObjects").transform.Cast<Transform>().LastOrDefault(t => t.name == "GeneratedObjects" && t.gameObject.activeInHierarchy);
				var shown = holder != null ? holder.GetComponentsInChildren<EditorGameObject>(false).Select(e => new KeyValuePair<string, Vector3>(e.GameObjectName, e.transform.position - terrain.transform.position)).ToList() : new List<KeyValuePair<string, Vector3>>();
				Func<IEnumerable<KeyValuePair<string, Vector3>>, List<string>> keys = l => l.Select(k => k.Key + "@" + k.Value.x.ToString("F1", Inv) + "," + k.Value.y.ToString("F1", Inv) + "," + k.Value.z.ToString("F1", Inv)).OrderBy(k => k, StringComparer.Ordinal).ToList();
				List<string> a = keys(shown), b = keys(f.Objects.Select(o => new KeyValuePair<string, Vector3>(o.Name, o.Position)));
				var onlyEditor = a.Except(b).ToList(); var onlyFile = b.Except(a).ToList();
				bool sameLevel = Mathf.Abs(DynamicIslands.EditorWaterLevel - f.WaterLevel) < 0.01f;
				bool sameStyle = TerrainPainter.StyleIndex(f.Style) == s.Style;
				Check(ref ok, sameGrid && worst < 0.01f && onlyEditor.Count == 0 && onlyFile.Count == 0 && n == f.Objects.Count && sameLevel && sameStyle,
					IslandShapes.Names[s.Shape] + " " + TerrainPainter.StyleName(s.Style) + " seed " + s.Seed + ": heights differ by at most " + worst.ToString("F3") + " m, objects editor " + n + " / file " + f.Objects.Count +
					(onlyEditor.Count + onlyFile.Count > 0 ? " (only in the editor: " + string.Join(", ", onlyEditor.Take(3).ToArray()) + "; only in the file: " + string.Join(", ", onlyFile.Take(3).ToArray()) + ")" : "") +
					", sea level " + DynamicIslands.EditorWaterLevel + "/" + f.WaterLevel + ", style " + f.Style);
				yield return null;
			}
			if (ok) Log("PASS: the editor and the sailing path make the same island"); else Fail("the editor and the sailing path make the same island");
		}

		#endregion

		#region Presets through the window

		[ConsoleCommand(name: "CIPresetTest", docs: "Dev, editor: the generator window's presets with the newer settings (sea floor, drop-off, the four sea sliders): Save these settings -> file -> use it again; an old preset file without them loads with the defaults (deep, 0.5, like Raft)")]
		public static void PresetTestCommand()
		{
			StartTest(PresetTestRoutine());
		}

		static IEnumerator PresetTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			const string Name = "ci preset test", OldName = "ci old preset";
			string path = Path.Combine(GeneratorWindow.PresetFolder, Name + ".txt"), oldPath = Path.Combine(GeneratorWindow.PresetFolder, OldName + ".txt");
			try { File.Delete(path); File.Delete(oldPath); } catch { }
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
			var mine = new IslandGenSettings { Seed = 55, Radius = 77f, Height = 33f, Shape = IslandShapes.Crescent, Style = TerrainPainter.Desert, SeaFloor = IslandGenSettings.SeaFloorShallow, DropOff = 0.85f, Water = 0.8f, SeaRocks = 0.1f, SeaFinds = 0.7f, Sunken = 0.3f, Cliffs = 0.4f, Hostiles = 3, HostileKinds = "Hyena", Loot = 5, LootMin = 2, LootMax = 4 };
			GeneratorWindow.Use(mine);
			yield return null;
			GeneratorWindow w = GeneratorWindow.Instance;
			Button save = w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => LabelOfButton(b).StartsWith("Save these settings"));
			if (save != null) save.onClick.Invoke();
			yield return null;
			TextPromptWindow prompt = UnityEngine.Object.FindObjectsOfType<TextPromptWindow>().FirstOrDefault(p => p.gameObject.activeInHierarchy);
			bool prompted = prompt != null;
			if (prompted)
			{
				InputField field = (InputField)typeof(TextPromptWindow).GetField("field", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(prompt);
				field.text = Name;
				Button okButton = prompt.GetComponentsInChildren<Button>(true).FirstOrDefault(b => LabelOfButton(b) == "OK");
				if (okButton != null) okButton.onClick.Invoke();
			}
			yield return null;
			bool written = File.Exists(path);
			string text = written ? File.ReadAllText(path) : "";
			Check(ref ok, prompted && written && new[] { "SeaFloor=1", "DropOff=0.85", "Water=0.8", "SeaRocks=0.1", "SeaFinds=0.7", "Sunken=0.3" }.All(k => text.Contains(k)),
				"Save these settings... asks a name and writes " + Path.GetFileName(path) + " with the sea floor, drop-off and the four sea sliders (" + (written ? text.Split('\n').Length + " lines" : "missing") + ")");
			// Back to the defaults, then the preset's button
			Button defaults = w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => LabelOfButton(b) == "Defaults");
			if (defaults != null) defaults.onClick.Invoke();
			yield return null;
			bool reset = w.Settings.SeaFloor == IslandGenSettings.SeaFloorDeep && Mathf.Abs(w.Settings.DropOff - 0.5f) < 0.001f;
			Button use = w.GetComponentsInChildren<Button>(true).FirstOrDefault(b => LabelOfButton(b) == Name);
			if (use != null) use.onClick.Invoke();
			yield return null;
			IslandGenSettings back = w.Settings.Copy();
			IslandGenSettings want = mine.Copy(); want.Clamp(); want.Seed = back.Seed;
			string diff = SettingsDiff(want, back);
			Check(ref ok, reset && use != null && diff == null, "Defaults reset them" + (reset ? "" : " (NOT)") + "; the preset's button (" + (use != null ? "found" : "missing") + ") brings every setting back" + (diff != null ? ": " + diff : ""));
			// An old preset (before the deep sea floor and the life under water): the defaults
			string old = string.Join("\n", mine.ToText().Split('\n').Where(l => !System.Text.RegularExpressions.Regex.IsMatch(l, "^(SeaFloor|DropOff|Water|SeaRocks|SeaFinds|Sunken)=")).ToArray());
			File.WriteAllText(oldPath, old);
			IslandGenSettings o = IslandGenSettings.FromText(File.ReadAllText(oldPath));
			Check(ref ok, o.SeaFloor == IslandGenSettings.SeaFloorDeep && Mathf.Abs(o.DropOff - 0.5f) < 0.001f && Mathf.Abs(o.AmountSea(o.Water) - 0.5f) < 0.001f && Mathf.Abs(o.AmountSea(o.SeaFinds) - 0.5f) < 0.001f && o.Shape == IslandShapes.Crescent && Mathf.Abs(o.Radius - 77f) < 0.01f,
				"an old preset without the new settings: sea floor " + (o.SeaFloor == 0 ? "deep" : "shallow") + ", drop-off " + o.DropOff + ", corals " + o.AmountSea(o.Water) + ", things to collect " + o.AmountSea(o.SeaFinds) + " (like Raft); its own layout and size kept");
			GeneratorWindow.Close();
			try { File.Delete(path); File.Delete(oldPath); } catch { }
			if (ok) Log("PASS: generator presets"); else Fail("generator presets");
		}

		[ConsoleCommand(name: "CILevelPreset", docs: "Dev, any time: Level up is a world option only - the generator has no Levels setting, a preset saved now has no Levels line, an older preset with Levels=True still loads with its other values (the line passed by), and a map type's Levels line is passed by too")]
		public static void LevelPresetCommand()
		{
			bool ok = true;
			try
			{
				Check(ref ok, typeof(IslandGenSettings).GetField("Levels") == null, "the generator has no Levels setting");
				var s0 = new IslandGenSettings { Seed = 9, Radius = 33f };
				string text = s0.ToText();
				Check(ref ok, !text.Contains("Levels"), "a preset saved now has no Levels line");
				IslandGenSettings back = IslandGenSettings.FromText(text + "Levels=True" + (char)10);
				Check(ref ok, back.Seed == 9 && Mathf.Approximately(back.Radius, 33f) && back.ToText() == text, "an older preset with Levels=True loads, the line passed by");
				var t = new MapType { Name = "cilevelpreset", Sets = { "Levels=on", "Radius=30" } };
				float elevation;
				IslandGenSettings rolled = MapTypes.Roll(t, new System.Random(1), out elevation);
				Check(ref ok, rolled != null, "a map type with a Levels line still rolls");
			}
			catch (Exception e) { Fail("level preset: " + e); ok = false; }
			if (ok) Log("PASS: level preset"); else Fail("level preset");
		}

		/// <summary>The first setting that differs (name: a -> b), or null.</summary>
		static string SettingsDiff(IslandGenSettings a, IslandGenSettings b)
		{
			foreach (FieldInfo f in typeof(IslandGenSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				object x = f.GetValue(a), y = f.GetValue(b);
				bool same = x is float ? Mathf.Abs((float)x - (float)y) < 0.002f : Equals(x, y);
				if (!same) return f.Name + ": " + x + " -> " + y;
			}
			return null;
		}

		#endregion

		#region Randomize existing, on the deep sea floor

		[ConsoleCommand(name: "CIRandomizeDeep", docs: "Dev, editor: a variation of each measured Raft island keeps its real depths under water (the generated profile against the measured one in raft_underwater.txt); \"something new like it\" gets the island's drop-off")]
		public static void RandomizeDeepCommand()
		{
			StartTest(RandomizeDeepRoutine());
		}

		/// <summary>
		/// The profile of generated heights as CIMeasureUnderwater measured Raft's: every cell under water by its distance
		/// to the nearest land (a chamfer distance), the median depth per 5 m of it (0 m, 5 m, ...).
		/// </summary>
		static SortedDictionary<float, float> GeneratedProfile(float[,] m, int res, float step, float sea)
		{
			const float Far = 1e9f;
			var d = new float[res, res];
			for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) d[z, x] = m[z, x] > sea ? 0f : Far;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					float v = d[z, x];
					if (x > 0) v = Mathf.Min(v, d[z, x - 1] + 1f); if (z > 0) v = Mathf.Min(v, d[z - 1, x] + 1f);
					if (x > 0 && z > 0) v = Mathf.Min(v, d[z - 1, x - 1] + 1.414f); if (x < res - 1 && z > 0) v = Mathf.Min(v, d[z - 1, x + 1] + 1.414f);
					d[z, x] = v;
				}
			for (int z = res - 1; z >= 0; z--)
				for (int x = res - 1; x >= 0; x--)
				{
					float v = d[z, x];
					if (x < res - 1) v = Mathf.Min(v, d[z, x + 1] + 1f); if (z < res - 1) v = Mathf.Min(v, d[z + 1, x] + 1f);
					if (x < res - 1 && z < res - 1) v = Mathf.Min(v, d[z + 1, x + 1] + 1.414f); if (x > 0 && z < res - 1) v = Mathf.Min(v, d[z + 1, x - 1] + 1.414f);
					d[z, x] = v;
				}
			var bins = new Dictionary<int, List<float>>();
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					// (as measured: the flat floor beyond the ground - "no ground" there - left out)
					if (m[z, x] > sea || d[z, x] >= Far || m[z, x] <= 0.5f) continue;
					int k = (int)(d[z, x] * step / 5f);
					if (k >= 60) continue;
					List<float> l;
					if (!bins.TryGetValue(k, out l)) bins[k] = l = new List<float>();
					l.Add(sea - m[z, x]);
				}
			var p = new SortedDictionary<float, float>();
			foreach (var kv in bins) { kv.Value.Sort(); p[kv.Key * 5f] = kv.Value[kv.Value.Count / 2]; }
			return p;
		}

		/// <summary>How far out from the coast a profile first gets this deep (interpolated, as RaftUnderwater.DistanceToDepth), -1 if never.</summary>
		static float DistanceToDepth(SortedDictionary<float, float> p, float depth)
		{
			float lastD = 0f, lastDepth = 0f;
			foreach (var kv in p)
			{
				if (kv.Value >= depth) return kv.Value - lastDepth < 0.01f ? kv.Key : Mathf.Lerp(lastD, kv.Key, (depth - lastDepth) / (kv.Value - lastDepth));
				lastD = kv.Key; lastDepth = kv.Value;
			}
			return -1f;
		}

		static IEnumerator RandomizeDeepRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			Vector3 size = IslandGenerator.BuildArea;
			const int Res = 513;
			float step = size.x / (Res - 1);
			int compared = 0, close = 0, dropOk = 0, measured = 0;
			var off = new List<string>();
			var ratios = new List<float>();
			foreach (RaftIsland isl in RaftIslands.Offered)
			{
				float raft12 = RaftUnderwater.DistanceToDepth(isl.Label, 12f), raft50 = RaftUnderwater.DistanceToDepth(isl.Label, 50f);
				IslandGenSettings like = RaftIslands.LikeIt(isl, new IslandGenSettings { Seed = 3 });
				if (Mathf.Abs(like.DropOff - RaftUnderwater.DropOffOf(isl)) < 0.001f && like.SeaFloor == IslandGenSettings.SeaFloorDeep) dropOk++;
				if (raft12 < 0f || raft50 < 0f) continue;
				measured++;
				IslandGenSettings vari = RaftIslands.VariationOf(isl, new IslandGenSettings { Seed = 3 });
				float[,] m = IslandGenerator.HeightsMetres(vari, size, Res);
				SortedDictionary<float, float> prof = GeneratedProfile(m, Res, step, vari.WaterLevel);
				if (prof.Count == 0) { off.Add(isl.Label + ": no land"); continue; }
				float gen12 = DistanceToDepth(prof, 12f), gen50 = DistanceToDepth(prof, 50f);
				compared++;
				// (the same within 15 m, or within half again: the ground is resampled from the measurement's 2 m grid, and
				// the profile of a ragged coast depends on where the rays leave it)
				bool near12 = gen12 >= 0f && Mathf.Abs(gen12 - raft12) <= Mathf.Max(15f, raft12 * 0.5f);
				bool near50 = gen50 >= 0f && Mathf.Abs(gen50 - raft50) <= Mathf.Max(15f, raft50 * 0.5f);
				if (gen50 > 0f) ratios.Add(gen50 / Mathf.Max(1f, raft50));
				if (near12 && near50) close++;
				else off.Add(isl.Label + " 12 m deep at " + gen12.ToString("F0") + " (Raft " + raft12.ToString("F0") + "), 50 m at " + gen50.ToString("F0") + " (Raft " + raft50.ToString("F0") + ")");
				Log("  " + isl.Label + ": 12 m deep " + gen12.ToString("F0") + " m out (Raft " + raft12.ToString("F0") + "), 50 m deep " + gen50.ToString("F0") + " m out (Raft " + raft50.ToString("F0") + "); like it: drop-off " + like.DropOff.ToString("F2"));
				yield return null;
			}
			float median = ratios.Count > 0 ? ratios.OrderBy(r => r).ElementAt(ratios.Count / 2) : 0f;
			Check(ref ok, compared >= 10 && close >= compared * 0.8f && median > 0.7f && median < 1.4f,
				close + " of " + compared + " variations keep their island's depths (12 m and 50 m deep at about the measured distance from the coast; median ratio " + median.ToString("F2") + ")" + (off.Count > 0 ? "; off: " + string.Join("; ", off.Take(6).ToArray()) : ""));
			Check(ref ok, dropOk == RaftIslands.Offered.Count, "\"something new like it\" gets the island's drop-off and the deep sea floor: " + dropOk + " of " + RaftIslands.Offered.Count + " (" + measured + " have a measured profile)");
			if (ok) Log("PASS: randomize existing on the deep sea floor"); else Fail("randomize existing on the deep sea floor");
		}

		#endregion

		#region The generator window, drawn

		[ConsoleCommand(name: "CIGenWindowTest", docs: "Dev, editor: every slider and choice of the generator window's Normal tab changes the preview (a pixel hash) or the estimate - or, for those that only matter when generating, the setting; the reach line's colour follows its level; the window fits a 1280 x 720 screen")]
		public static void GenWindowTestCommand()
		{
			StartTest(GenWindowTestRoutine());
		}

		static int PixelHash(Texture t)
		{
			var tex = t as Texture2D;
			if (tex == null) return 0;
			unchecked
			{
				int h = 17;
				Color32[] px = tex.GetPixels32();
				for (int i = 0; i < px.Length; i += 3) h = h * 31 + (px[i].r | px[i].g << 8 | px[i].b << 16);
				return h;
			}
		}

		static IEnumerator GenWindowTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow.ShowTab(GeneratorWindow.TabNormal);
			var start = new IslandGenSettings { Seed = 1234, Radius = 70f, Height = 40f, Stretch = 1.6f, ObjectDensity = 0.5f, Hostiles = 2, Loot = 3 };
			GeneratorWindow.Use(start);
			GeneratorWindow w = GeneratorWindow.Instance;
			FieldInfo estField = typeof(GeneratorWindow).GetField("estimate", BindingFlags.NonPublic | BindingFlags.Instance);
			Func<string> estimate = () => { var t = estField != null ? estField.GetValue(w) as Text : null; return t != null ? t.text : ""; };
			Transform tab = w.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Tab0");
			// (settings that only matter when generating: they change the setting, not the picture or the count - also the things
			// to gather, buildings, quest and Raft's features, LM9-LM12: the estimate counts only land and sea nature)
			var onlySetting = new HashSet<string> { "Groups", "Lowest tier", "Highest tier", "Things to gather", "Finds in the shallows", "How far out", "How many", "Quest steps", "Raft's features" };
			int sliders = 0, changed = 0;
			var dead = new List<string>();
			foreach (Transform row in tab.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Slider_")).ToList())
			{
				Slider sl = row.GetComponentInChildren<Slider>(true);
				if (sl == null) continue;
				string label = row.name.Substring("Slider_".Length);
				GeneratorWindow.Use(start);
				int h0 = PixelHash(GeneratorWindow.PreviewNow());
				string e0 = estimate(), s0 = w.Settings.ToText();
				float v = sl.value - sl.minValue > (sl.maxValue - sl.minValue) / 2f ? sl.minValue : sl.maxValue;
				if (label == "Highest tier") v = sl.minValue; // (the lowest tier follows it down)
				if (label == "Direction") v = 90f; // (0° and 180° are the same way)
				sl.value = v;
				int h1 = PixelHash(GeneratorWindow.PreviewNow());
				string e1 = estimate(), s1 = w.Settings.ToText();
				sliders++;
				bool drew = h1 != h0 || e1 != e0;
				bool set = s1 != s0;
				if (drew || (onlySetting.Contains(label) && set)) changed++;
				else dead.Add(label + (set ? " (the setting changed, not the preview)" : " (nothing changed)"));
				yield return null;
			}
			// The choices: style and layout steppers, sea floor, seabed, toughness, placed
			var choices = new[] { new[] { "Style", ">" }, new[] { "Layout", ">" }, new[] { "Sea floor", "Shallow (20 m)" }, new[] { "Seabed", "Reef ring" }, new[] { "Toughness", "Boss" }, new[] { "Placed", "Hidden" } };
			int choiceOk = 0;
			foreach (var c in choices)
			{
				GeneratorWindow.Use(start);
				int h0 = PixelHash(GeneratorWindow.PreviewNow());
				string e0 = estimate(), s0 = w.Settings.ToText();
				Transform row = tab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == c[0]);
				Button b = row != null ? row.GetComponentsInChildren<Button>(true).FirstOrDefault(x => LabelOfButton(x) == c[1]) : null;
				if (b != null) b.onClick.Invoke();
				int h1 = PixelHash(GeneratorWindow.PreviewNow());
				bool drew = h1 != h0 || estimate() != e0;
				bool set = w.Settings.ToText() != s0;
				bool settingOnly = c[0] == "Toughness" || c[0] == "Placed";
				if (b != null && (drew || (settingOnly && set))) choiceOk++;
				else dead.Add(c[0] + " " + c[1] + (b == null ? " (no button)" : set ? " (the setting changed, not the preview)" : " (nothing changed)"));
				yield return null;
			}
			Check(ref ok, sliders >= 30 && changed == sliders && choiceOk == choices.Length, sliders + " sliders and " + choices.Length + " choices: " + (changed + choiceOk) + " change the preview, the estimate or (generating only) the setting" + (dead.Count > 0 ? "; not: " + string.Join(", ", dead.ToArray()) : ""));

			// The reach line's colour follows its level
			FieldInfo reachField = typeof(GeneratorWindow).GetField("reachText", BindingFlags.NonPublic | BindingFlags.Instance);
			Func<string> reachRaw = () => { var t = reachField != null ? reachField.GetValue(w) as Text : null; return t != null ? t.text : ""; };
			GeneratorWindow.Use(new IslandGenSettings { Seed = 606, Radius = 70f, Height = 35f, BeachWidth = 0.6f, Cliffs = 0f });
			ReachResult easy = GeneratorWindow.ReachNow();
			string easyRaw = reachRaw();
			GeneratorWindow.Use(new IslandGenSettings { Seed = 606, Radius = 70f, Height = 60f, BeachWidth = 0f, Cliffs = 1f });
			ReachResult hard = GeneratorWindow.ReachNow();
			string hardRaw = reachRaw();
			Check(ref ok, easy != null && hard != null && easyRaw.Contains(IslandReach.ColourOf(easy.Level)) && hardRaw.Contains(IslandReach.ColourOf(hard.Level)) && IslandReach.ColourOf(easy.Level) != IslandReach.ColourOf(hard.Level),
				"the reach line: beaches " + IslandReach.ColourOf(easy != null ? easy.Level : 0) + ", cliffs all round " + IslandReach.ColourOf(hard != null ? hard.Level : 0) + " (" + (hard != null ? hard.Text : "-") + ")");

			// The window fits a 1280 x 720 screen (the canvas scales to fit 1400 x 800)
			CanvasScaler scaler = EditorUI.Canvas != null ? EditorUI.Canvas.GetComponent<CanvasScaler>() : null;
			RectTransform panel = (RectTransform)w.transform.Find("Panel");
			if (scaler != null && panel != null)
			{
				Vector2 refRes = scaler.referenceResolution;
				float scale = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(1280f / refRes.x, 720f / refRes.y)
					: scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(1280f / refRes.x, 720f / refRes.y)
					: Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(1280f / refRes.x, 2f), Mathf.Log(720f / refRes.y, 2f), scaler.matchWidthOrHeight));
				float px = panel.rect.height * scale, wide = panel.rect.width * scale;
				Check(ref ok, px <= 720f && wide <= 1280f, "the window at 1280 x 720: " + wide.ToString("F0") + " x " + px.ToString("F0") + " px (scale " + scale.ToString("F2") + ", " + scaler.screenMatchMode + " " + refRes + ")");
			}
			else Check(ref ok, false, "no canvas scaler or window panel");
			GeneratorWindow.Close();
			if (ok) Log("PASS: the generator window"); else Fail("the generator window");
		}

		#endregion

		#region Under water in a world: pickups

		const string UnderwaterPickIsland = "ciuwpick";

		/// <summary>The kinds the life under water gives players to take, by a part of their name.</summary>
		static readonly string[] UnderwaterKinds = { "GiantClam", "SilverAlgae", "Iron", "Scrap", "Rock", "SeavineKlump" };

		/// <summary>A small deep island with each kind of underwater pickup on its shelf, a few metres down (built once).</summary>
		static IslandFile MakeUnderwaterPickIsland()
		{
			var s = new IslandGenSettings { Seed = 8080, Radius = 40f, Height = 16f, Trees = 0.2f, Bushes = 0.1f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0.2f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, UnderwaterPickIsland);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, float> ground = (x, z) => IslandGenerator.SampleHeights(f.Heights, res, step, x, z) * f.TerrainSize.y;
			// Walk out from the land to ground 5 m under the sea, one direction per kind
			string[] names = { "Pickup_Landmark_GiantClam", "Pickup_Landmark_SilverAlgae", "Pickup_Landmark_Iron 1", "Pickup_Landmark_Scrap 1_OceanBottom", "Pickup_Landmark_Rock 1", "SeaVine3_klump" };
			for (int i = 0; i < names.Length; i++)
			{
				float a = i * Mathf.PI * 2f / names.Length;
				Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
				float d = 0f;
				while (d < 200f && ground(c.x + dir.x * d, c.y + dir.y * d) > f.WaterLevel - 5f) d += 0.5f;
				float x = c.x + dir.x * d, z = c.y + dir.y * d;
				f.Objects.Add(new IslandObject { Name = names[i], Position = new Vector3(x, ground(x, z), z) });
			}
			return f;
		}

		[ConsoleCommand(name: "CIUnderwaterPick", docs: "Dev, in game (either player; the host spawns): on a deep island with each kind of pickup under water (giant clam, silver algae, ore, scrap, stone, the seaweed in a sea vine), dives for each the way CIPick does: the items arrive, each stays gone after unloading and loading the island, and comes back after the regrow days. CIUnderwaterPick [keep]")]
		public static void UnderwaterPickCommand(string[] args)
		{
			StartTest(UnderwaterPickRoutine(args != null && args.Contains("keep")));
		}

		/// <summary>Every item the local player has (unique name -> count).</summary>
		static Dictionary<string, int> Items(Network_Player player)
		{
			var d = new Dictionary<string, int>();
			if (player == null || player.Inventory == null) return d;
			foreach (Slot sl in player.Inventory.allSlots)
			{
				if (sl == null || sl.itemInstance == null || sl.itemInstance.baseItem == null) continue;
				string n = sl.itemInstance.baseItem.UniqueName;
				int v; d.TryGetValue(n, out v); d[n] = v + sl.itemInstance.Amount;
			}
			return d;
		}

		static string Gained(Dictionary<string, int> before, Dictionary<string, int> after)
		{
			var got = after.Where(kv => { int b; before.TryGetValue(kv.Key, out b); return kv.Value > b; }).Select(kv => { int b; before.TryGetValue(kv.Key, out b); return kv.Key + " x" + (kv.Value - b); }).ToArray();
			return got.Length > 0 ? string.Join(", ", got) : "";
		}

		/// <summary>The island's pickups (not trees) whose name has this part, active or not.</summary>
		static List<PickupItem_Networked> PickupsOf(IslandWorldState.Entry e, string part)
		{
			return e.Root.GetComponentsInChildren<PickupItem_Networked>(true).Where(p => p.GetComponent<HarvestableTree>() == null && p.GetComponent<PickupItem>() != null &&
				(p.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 || (p.transform.parent != null && p.transform.parent.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
		}

		static IEnumerator UnderwaterPickRoutine(bool keep)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue) { Fail("run in a world"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName == UnderwaterPickIsland);
			if (e == null)
			{
				if (!Raft_Network.IsHost) { Fail("no loaded island called '" + UnderwaterPickIsland + "' here (the host spawns it)"); yield break; }
				MakeUnderwaterPickIsland().Save(IslandSpawner.PathFor(UnderwaterPickIsland));
				Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(UnderwaterPickIsland), 450f);
				if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
				yield return DynamicIslands.instance.SpawnIslandFile(UnderwaterPickIsland, spot.Value, true);
				e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == UnderwaterPickIsland);
				if (e == null || e.Root == null) { Fail(UnderwaterPickIsland + " did not spawn"); yield break; }
			}
			yield return new WaitForSeconds(1f);
			Pickup pickup = player.GetComponentInChildren<Pickup>(true);
			if (pickup == null) { Fail("the player has no Pickup script"); yield break; }
			var picked = new Dictionary<string, int>(); // kind -> network ordinal
			foreach (string kind in UnderwaterKinds)
			{
				PickupItem_Networked pn = PickupsOf(e, kind).FirstOrDefault(p => p.gameObject.activeInHierarchy);
				if (pn == null) { Check(ref ok, false, kind + ": none to pick on '" + e.HostName + "' (" + string.Join(", ", e.Root.GetComponentsInChildren<PickupItem_Networked>(true).Select(p => p.name).Distinct().Take(12).ToArray()) + ")"); continue; }
				float depth = -pn.transform.position.y;
				PutPlayerNear(pn.transform, 0.8f);
				player.PersonController.SwitchControllerType(ControllerType.Water);
				yield return new WaitForSeconds(0.3f);
				Dictionary<string, int> before = Items(player);
				pickup.PickupItemByType(pn.GetComponent<PickupItem>(), true);
				yield return new WaitForSeconds(1f);
				string got = Gained(before, Items(player));
				bool gone = pn == null || !pn.gameObject.activeInHierarchy;
				picked[kind] = (int)(pn.ObjectIndex & 0xFFFF);
				Check(ref ok, got.Length > 0 && gone, kind + " (" + pn.name + ", " + depth.ToString("F1") + " m down): got " + (got.Length > 0 ? got : "nothing") + ", gone from the sea floor: " + gone);
			}
			OnRaftCommand();
			yield return new WaitForSeconds(1f);
			// Unload and load the island again: still gone
			if (e.Root != null) yield return ReloadIslandRoutine(e);
			yield return new WaitForSeconds(1f);
			int still = picked.Count(kv => e.Root != null && PickupsOf(e, kv.Key).Any(p => (int)(p.ObjectIndex & 0xFFFF) == kv.Value && !p.gameObject.activeInHierarchy));
			Check(ref ok, e.Root != null && still == picked.Count, "after unloading and loading '" + e.HostName + "': " + still + " of " + picked.Count + " picked things still gone");
			if (!keep)
			{
				// Days later they are back (the regrow days)
				int days = IslandRules.RegrowDays(e) + 1;
				IslandObjectState.Capture(e);
				foreach (ObjectState st in e.State.Values) st.Day -= days;
				yield return ReloadIslandRoutine(e);
				yield return new WaitForSeconds(1f);
				int back = picked.Count(kv => e.Root != null && PickupsOf(e, kv.Key).Any(p => (int)(p.ObjectIndex & 0xFFFF) == kv.Value && p.gameObject.activeInHierarchy));
				Check(ref ok, back == picked.Count, days + " days later " + back + " of " + picked.Count + " are back");
				if (Raft_Network.IsHost) IslandWorldState.Remove(UnderwaterPickIsland);
			}
			if (ok) Log("PASS: underwater pickups"); else Fail("underwater pickups");
		}

		#endregion

		#region Can players reach it: Raft's own controller

		[ConsoleCommand(name: "CIReachWorld", docs: "Dev, in game (host): spawns islands of each reach level (a beach, and ledges as steep as the generator's grid allows, 0.8 / 1.8 / 3 m above the water, or the heights given) and brings Raft's player in from the water with its own CharacterController (walking and jumping as WaterControll and GroundControll do); what they get onto must match IslandReach's level; measures the raft's deck. CIReachWorld [ledge heights...]")]
		public static void ReachWorldCommand(string[] args)
		{
			var ledges = new List<float>();
			foreach (string a in args ?? new string[0]) { float v; if (float.TryParse(a, System.Globalization.NumberStyles.Float, Inv, out v)) ledges.Add(v); }
			StartTest(ReachWorldRoutine(ledges.Count > 0 ? ledges.ToArray() : new[] { 0.8f, 1.8f, 3f }));
		}

		/// <summary>A flat round island, radius r, its edge a ledge this high above the water as steep as the generator's grid (1.95 m) allows, in water 3 m deep.</summary>
		static IslandFile LedgeIsland(string name, float ledge, float r)
		{
			const int Res = 257;
			float step = IslandGenerator.BuildArea.x / (IslandGenerator.BuildResolution - 1);
			var f = new IslandFile { Name = name, WaterLevel = IslandFile.DefaultWaterLevel, TerrainSize = new Vector3(step * (Res - 1), 600f, step * (Res - 1)), HeightmapResolution = Res, Heights = new float[Res, Res] };
			for (int z = 0; z < Res; z++)
				for (int x = 0; x < Res; x++)
				{
					float d = new Vector2(x - 128, z - 128).magnitude * step;
					// (the sea floor slopes up to 3 m deep near the island, then the ledge; flat seabed from 120 m out: the island's spacing stays small)
					float h = d < r ? f.WaterLevel + ledge : Mathf.Lerp(f.WaterLevel - 3f, 0.5f, Mathf.InverseLerp(r + 20f, 120f, d));
					f.Heights[z, x] = h / f.TerrainSize.y;
				}
			return f;
		}

		static IEnumerator ReachWorldRoutine(float[] ledgeHeights)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			PersonController pc = player.PersonController;
			CharacterController cc = pc.controller;

			// The raft's deck: the top of its walkable blocks (foundations) above the sea
			float deck = -1f;
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj != null)
			{
				var tops = raftObj.GetComponentsInChildren<Block>(true).Where(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName.StartsWith("Block_Foundation")).Select(b => b.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled && c.gameObject.activeInHierarchy).Select(c => c.bounds.max.y).DefaultIfEmpty(float.MinValue).Max()).Where(y => y > -5f).ToList();
				if (tops.Count > 0) deck = tops.OrderBy(y => y).ElementAt(tops.Count / 2);
				if (tops.Count > 0) Log("The raft's foundations: " + tops.Count + ", their tops " + tops.Min().ToString("F2") + " .. " + tops.Max().ToString("F2") + " m above the sea (the raft bobs on the waves)");
			}
			Log(deck >= 0f ? "The raft's deck (median top of its foundations): " + deck.ToString("F2") + " m above the sea (IslandReach.RaftDeck " + IslandReach.RaftDeck + ")" : "The raft's deck: no foundations to measure in this world (IslandReach.RaftDeck " + IslandReach.RaftDeck + " stays)");
			// (only logged: the test world's raft is half wrecked and bobs, its foundations read -1.8 .. 1.1 m between restarts)
			float deckUsed = deck >= 0.1f && deck <= 0.6f ? deck : IslandReach.RaftDeck;

			var islands = new List<KeyValuePair<string, IslandFile>>();
			var beach = new IslandGenSettings { Seed = 606, Radius = 40f, Height = 12f, BeachWidth = 0.8f, Cliffs = 0f, SeaFloor = IslandGenSettings.SeaFloorShallow, ObjectDensity = 0f };
			islands.Add(new KeyValuePair<string, IslandFile>("a beach", IslandGenerator.CreateFile(beach, "cireach-beach")));
			foreach (float lh in ledgeHeights)
				islands.Add(new KeyValuePair<string, IslandFile>("a ledge " + lh.ToString("0.0#", Inv) + " m up", LedgeIsland("cireach-" + lh.ToString("0.0#", Inv).Replace('.', '_'), lh, 30f)));
			foreach (var kv in islands)
			{
				IslandFile f = kv.Value;
				f.Save(IslandSpawner.PathFor(f.Name));
				int res = f.HeightmapResolution;
				float step = f.TerrainSize.x / (res - 1);
				var m = new float[res, res];
				for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) m[z, x] = f.Heights[z, x] * f.TerrainSize.y;
				ReachResult r = IslandReach.Assess(m, step, f.WaterLevel);
				Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(f.Name), 500f);
				if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
				yield return DynamicIslands.instance.SpawnIslandFile(f.Name, spot.Value, true);
				IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == f.Name);
				if (e == null || e.Root == null) { Check(ref ok, false, f.Name + " did not spawn"); continue; }
				yield return new WaitForSeconds(1f);
				Physics.SyncTransforms();
				Terrain terrain = e.Root.GetComponentInChildren<Terrain>();
				Func<Vector3, float> ground = p => terrain.SampleHeight(p) + terrain.transform.position.y;
				Vector3 centre = new Vector3(e.Position.x, 0f, e.Position.z);
				// The coast on the side facing +x: the first point from the middle outwards with ground under the sea
				Vector3 dir = Vector3.right;
				float coast = 0f;
				while (coast < 150f && ground(centre + dir * coast) > 0f) coast += 0.25f;
				float top = ground(centre);

				// (the island's ground must stop a swimmer before the swim: on a minimised Raft (few frames) the first island's
				// swimmer went in under the beach - feet 3.4 m under the sea, 6.5 m of ground over them - and the raft try a moment
				// later stood on it; RUN2 10-20. Waited for here, up to 15 s; if it never comes, that is the mod's)
				{
					TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
					Vector3 probe = centre + dir * Mathf.Max(0f, coast - 2f);
					float waited = 0f;
					bool solid = false;
					for (; waited < 15f; waited += 0.25f)
					{
						Physics.SyncTransforms();
						RaycastHit hit;
						solid = tc != null && tc.enabled && tc.Raycast(new Ray(probe + Vector3.up * 60f, Vector3.down), out hit, 200f) && Mathf.Abs(hit.point.y - ground(probe)) < 0.5f;
						if (solid) break;
						yield return new WaitForSecondsRealtime(0.25f);
					}
					if (waited > 0f) Log("  (" + kv.Key + ": the ground stopped things after " + waited.ToString("F2") + " s more)");
					if (!solid) { Check(ref ok, false, kv.Key + ": the island's ground has no collider at its coast 15 s after it spawned"); IslandWorldState.Remove(f.Name); yield return null; continue; }
				}
				// 1. From the water: swim in, jump when blocked (WaterControll's jump: 1.25 x jumpSpeed), up to 4 tries
				bool fromWater = false; int waterJumps = 0;
				yield return BringIn(player, centre + dir * (coast + 5f), -dir, true, ground, (got, j) => { fromWater = got; waterJumps = j; });
				// 2. From a raft pushed against the coast: stand on a deck this high, walk and jump (GroundControll: jumpSpeed)
				bool fromRaft = false;
				{
					var deckBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
					deckBox.name = "CI_TestDeck";
					deckBox.layer = IslandSpawner.TerrainLayer;
					deckBox.transform.localScale = new Vector3(3f, 1f, 4f);
					deckBox.transform.position = centre + dir * (coast + 1.6f) + Vector3.up * (deckUsed - 0.5f);
					Physics.SyncTransforms();
					yield return BringIn(player, centre + dir * (coast + 2.4f) + Vector3.up * (deckUsed + 1f), -dir, false, ground, (got, j) => fromRaft = got);
					UnityEngine.Object.Destroy(deckBox);
				}
				// What Raft's player did: walked in (a beach), one jump out of the water (a low ledge), several jumps up the
				// rock or from the raft (possible but unlikely), or nothing (building)
				int got2 = fromWater && waterJumps == 0 ? IslandReach.Easy : fromWater && waterJumps == 1 ? IslandReach.Tricky : fromWater || fromRaft ? IslandReach.VeryTricky : IslandReach.No;
				// (the line that holds run after run is whether they get on at all: how many jumps it takes varies with the
				// timing against Raft's bobbing in the water - a 0.8 m ledge took 1 jump in one run and 3 in another. A beach
				// is walked onto; anything IslandReach calls possible is got onto; what it says needs building is not)
				bool match = r.Level <= IslandReach.Mostly ? fromWater && waterJumps <= 1 : r.Level <= IslandReach.VeryTricky ? fromWater || fromRaft : !fromWater && !fromRaft;
				// (what needs building: a big wave now and then lifts a swimmer onto a ledge otherwise out of reach - Raft's sea,
				// not the island. Twice more: it holds when the player gets on at most once in three tries)
				if (!match && r.Level == IslandReach.No)
				{
					int gotOn = 1;
					for (int again = 0; again < 2; again++)
					{
						OnRaftCommand();
						yield return new WaitForSeconds(0.5f);
						bool g = false;
						yield return BringIn(player, centre + dir * (coast + 5f), -dir, true, ground, (got, j) => g = got);
						if (g) gotOn++;
					}
					Log("  (" + kv.Key + ": got on " + gotOn + " time(s) of 3 from the water)");
					match = gotOn <= 1;
				}
				string[] levelNames = { "easy", "reachable", "possible but tricky", "possible but unlikely", "not without building" };
				Check(ref ok, match, kv.Key + ": IslandReach says " + levelNames[r.Level] + " (lowest ledge " + r.LowestLedge.ToString("F2") + " m); Raft's player got on from the water: " + fromWater + " (" + waterJumps + " jump(s)), from a raft " + deckUsed.ToString("F1") + " m high: " + fromRaft + " (level " + got2 + ")");
				OnRaftCommand();
				yield return new WaitForSeconds(0.5f);
				IslandWorldState.Remove(f.Name);
				yield return null;
			}
			if (ok) Log("PASS: can players reach it, with Raft's player"); else Fail("can players reach it, with Raft's player");
		}

		/// <summary>
		/// Brings the player in towards the island: each frame a step along dir with the CharacterController (on top of
		/// Raft's own controller), and when stuck against something a jump as Raft does it (out of the water 1.25 x the
		/// jump speed, from the ground the jump speed). Got on: standing on the island's land (above the sea), at least a
		/// metre past the coast.
		/// </summary>
		static IEnumerator BringIn(Network_Player player, Vector3 from, Vector3 dir, bool swimming, Func<Vector3, float> ground, Action<bool, int> result)
		{
			PersonController pc = player.PersonController;
			CharacterController cc = pc.controller;
			cc.enabled = false;
			player.transform.position = swimming ? new Vector3(from.x, -0.6f, from.z) : from;
			pc.externalVelocity = Vector3.zero;
			pc.SwitchControllerType(swimming ? ControllerType.Water : ControllerType.Ground);
			cc.enabled = true;
			Look(player, Quaternion.LookRotation(dir).eulerAngles.y, 0f);
			yield return new WaitForSeconds(swimming ? 1.5f : 0.8f);
			Vector3 start = player.transform.position;
			Func<float> feet = () => cc.transform.TransformPoint(cc.center - Vector3.up * cc.height / 2f).y;
			int jumps = 0;
			float stuckFor = 0f, lastJump = -10f;
			Vector3 last = player.transform.position;
			bool on = false;
			for (float t = 0f; t < 10f; t += Time.deltaTime)
			{
				KeepAlive(player);
				// (Raft's own move this frame has just run: whether it stands on something - IsGrounded is the controller's flag of
				// the last Move, so it is read before the test's own push - with Raft's coyote time)
				bool grounded = pc.IsConsideredGrounded;
				// (at Raft's own speeds: walking, or swimming)
				float speed = pc.controllerType == ControllerType.Water ? pc.swimSpeed : pc.normalSpeed;
				cc.Move(dir * speed * Time.deltaTime);
				Vector3 now = player.transform.position;
				float moved = new Vector2(now.x - last.x, now.z - last.z).magnitude;
				last = now;
				stuckFor = moved < speed * Time.deltaTime * 0.3f ? stuckFor + Time.deltaTime : 0f;
				// (a jump only from where Raft allows one: swimming, or standing on the ground - never again in mid-air)
				bool inWater = pc.controllerType == ControllerType.Water;
				if (stuckFor > 0.25f && t - lastJump > 1.2f && jumps < 8 && (inWater || grounded))
				{
					pc.SwitchControllerType(ControllerType.Ground);
					pc.externalVelocity = Vector3.up * pc.jumpSpeed * (inWater ? 1.25f : 1f);
					jumps++; lastJump = t; stuckFor = 0f;
				}
				// Standing on the land, past the coast
				float g = ground(now);
				if (g > 0.15f && feet() > g - 0.4f && grounded && (new Vector2(now.x - start.x, now.z - start.z).magnitude > (swimming ? 6f : 3.5f))) { on = true; break; }
				yield return null;
			}
			Log("  " + (swimming ? "from the water" : "from the raft") + ": " + (on ? "got on" : "did not get on") + " (walking " + pc.normalSpeed + ", swimming " + pc.swimSpeed + " m/s; " + jumps + " jump(s); feet at " + feet().ToString("F2") + " m over ground at " + ground(player.transform.position).ToString("F2") + " m)");
			result(on, jumps);
		}

		#endregion

		#region The deep sea floor in a world

		[ConsoleCommand(name: "CIDeepWorld", docs: "Dev, in game (host): a generated island on the deep sea floor in a world - its ground kept to 110 m with no edge above 100 m, its land radius (spacing) as a shallow one's; flying (holes, the underside, no foot hanging under it, no underwater objects) and sunken (its top at the depth asked); the creatures' NavMesh only over land and shallows, every hostile spot on it; several deep islands at once (frame time)")]
		public static void DeepWorldCommand()
		{
			StartTest(DeepWorldRoutine());
		}

		static IEnumerator DeepWorldRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			// (the generator places nothing until the object catalog is built: just after a world loads it may still be building)
			yield return PlaceableCatalog.EnsureBuilt();
			var deepS = new IslandGenSettings { Seed = 9191, Radius = 55f, Height = 30f, Trees = 0.3f, Bushes = 0.2f, Rocks = 0.2f, Harvest = 0.2f, BeachThings = 0.2f, Water = 0.5f, SeaRocks = 0.5f, SeaFinds = 0.5f, Sunken = 0.3f, Hostiles = 4, HostileKinds = "Boar" };
			IslandGenSettings shallowS = deepS.Copy(); shallowS.SeaFloor = IslandGenSettings.SeaFloorShallow;
			IslandFile deepF = IslandGenerator.CreateFile(deepS, "cideep"), shallowF = IslandGenerator.CreateFile(shallowS, "cideepshallow");
			deepF.Save(IslandSpawner.PathFor("cideep"));
			shallowF.Save(IslandSpawner.PathFor("cideepshallow"));
			float lrDeep = IslandSpawner.LandRadius(deepF), lrShallow = IslandSpawner.LandRadius(shallowF);
			Check(ref ok, lrDeep > deepS.Radius * 0.8f && lrDeep < lrShallow * 1.6f + 20f, "land radius (spacing between islands): deep " + lrDeep.ToString("F0") + " m, the same island on a shallow seabed " + lrShallow.ToString("F0") + " m (land about " + deepS.Radius.ToString("F0") + " m)");

			// 1. At sea: the ground kept to 110 m down, no edge of the terrain above 100 m
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius("cideep"), 500f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			var nav = new List<string>();
			Application.LogCallback watch = (msg, trace, type) => { if (msg.Contains("NavMesh for")) nav.Add(msg); };
			Application.logMessageReceived += watch;
			yield return DynamicIslands.instance.SpawnIslandFile("cideep", spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == "cideep");
			if (e == null || e.Root == null) { Application.logMessageReceived -= watch; Fail("cideep did not spawn"); yield break; }
			Terrain terrain = e.Root.GetComponentInChildren<Terrain>();
			TerrainData td = terrain.terrainData;
			float lowest = float.MaxValue, edgeShallowest = float.MaxValue;
			int tres = td.heightmapResolution;
			float[,] th = td.GetHeights(0, 0, tres, tres);
			for (int z = 0; z < tres; z++)
				for (int x = 0; x < tres; x++)
				{
					float y = th[z, x] * td.size.y + terrain.transform.position.y;
					lowest = Mathf.Min(lowest, y);
					if (x == 0 || z == 0 || x == tres - 1 || z == tres - 1) edgeShallowest = Mathf.Min(edgeShallowest, -y);
				}
			Check(ref ok, lowest < -100f && edgeShallowest > 100f, "at sea: the ground goes down to " + (-lowest).ToString("F0") + " m, the terrain's edge is " + edgeShallowest.ToString("F0") + " m down at its shallowest (" + td.size.x.ToString("F0") + " m, " + tres + " samples)");
			// 2. The NavMesh: over land and the shallows only; every hostile spot on it
			for (int i = 0; i < 40 && nav.Count == 0; i++) yield return new WaitForSeconds(0.5f);
			Application.logMessageReceived -= watch;
			NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
			Vector3 c = new Vector3(e.Position.x, 0f, e.Position.z);
			var mine = tri.vertices.Where(v => new Vector2(v.x - c.x, v.z - c.z).magnitude < lrShallow + 60f).ToList();
			float deepest = mine.Count > 0 ? -mine.Min(v => v.y) : 0f;
			var spots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).ToList();
			int onMesh = spots.Count(p => { NavMeshHit hit; return NavMesh.SamplePosition(p.transform.position, out hit, 2.5f, NavMesh.AllAreas); });
			Check(ref ok, nav.Count > 0 && mine.Count > 0 && deepest < 25f && onMesh == spots.Count && spots.Count > 0,
				"the creatures' NavMesh (" + (nav.Count > 0 ? nav[0].Substring(nav[0].IndexOf("NavMesh")) : "not built") + "): " + mine.Count + " vertices on the island, the deepest " + deepest.ToString("F1") + " m under the sea; " + onMesh + " of " + spots.Count + " hostile spots on it");
			IslandWorldState.Remove("cideep");
			yield return null;

			// 3. Flying: holes, the underside, nothing hanging under it, no underwater objects
			Vector3 flyAt = spot.Value; flyAt.y = 60f;
			yield return DynamicIslands.instance.SpawnIslandFile("cideep", flyAt, true);
			e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == "cideep");
			if (e != null && e.Root != null)
			{
				terrain = e.Root.GetComponentInChildren<Terrain>();
				bool[,] holes = terrain.terrainData.GetHoles(0, 0, terrain.terrainData.heightmapResolution - 1, terrain.terrainData.heightmapResolution - 1);
				int holeCount = holes.Cast<bool>().Count(b => !b);
				Transform under = terrain.transform.Find("Underside");
				float underLowest = under != null ? under.GetComponent<MeshRenderer>().bounds.min.y : float.NaN;
				Transform objs = e.Root.transform.Find("Objects");
				// (objects stand on the land, 60 m up; any under the island's sea level would hang in the air)
				int low = objs != null ? objs.Cast<Transform>().Count(t => t.position.y < 60f - 0.6f) : -1;
				int wanted = deepF.Objects.Count(o => o.Position.y >= deepF.WaterLevel - 0.5f);
				int shown = objs != null ? objs.childCount : 0;
				// (beyond the coast nothing hangs down: the deep foot is holes, the underside stays inside the coastline)
				Physics.SyncTransforms();
				RaycastHit hit;
				bool footUnder = Enumerable.Range(0, 8).Any(k => Physics.Raycast(new Vector3(c.x, 1f, c.z) + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * deepS.Radius * 1.6f, Vector3.up, out hit, 58f, 1 << IslandSpawner.TerrainLayer));
				Check(ref ok, holeCount > 0 && under != null && underLowest >= 7.5f && low == 0 && !footUnder && shown <= wanted + 5,
					"flying 60 m up: " + holeCount + " holes, underside down to " + underLowest.ToString("F1") + " m above the sea, " + low + " objects under the land, nothing hangs below the coast: " + !footUnder + ", " + shown + " objects (the file has " + wanted + " above its sea)");
				string open = FlyingOpenFromBelow(e.Root);
				Check(ref ok, open == null, "seen from below the flying cideep's underside meets its ground all round the coast" + (open != null ? ": " + open : ""));
				Screenshot(new[] { "deep_flying" });
				yield return new WaitForSeconds(0.6f);
				IslandWorldState.Remove("cideep");
			}
			else Check(ref ok, false, "the flying cideep did not spawn");
			yield return null;

			// 4. Sunken: its top at the depth asked
			Vector3 sinkAt = spot.Value; sinkAt.y = -40f;
			yield return DynamicIslands.instance.SpawnIslandFile("cideep", sinkAt, true);
			e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == "cideep");
			if (e != null && e.Root != null)
			{
				terrain = e.Root.GetComponentInChildren<Terrain>();
				td = terrain.terrainData;
				float topY = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution).Cast<float>().Max() * td.size.y + terrain.transform.position.y;
				Check(ref ok, Mathf.Abs(topY - (-40f + deepS.Height)) < 1.5f, "sunken 40 m: its top " + (-topY).ToString("F1") + " m under the sea (asked " + (40f - deepS.Height).ToString("F0") + " m)");
				IslandWorldState.Remove("cideep");
			}
			else Check(ref ok, false, "the sunken cideep did not spawn");
			yield return null;

			// 5. Several deep islands at once: the frame time
			float before = 0f;
			yield return FrameTime(120, ms => before = ms);
			int n0 = IslandWorldState.Islands.Count;
			for (int i = 0; i < 3; i++)
			{
				Vector3? s3 = CustomIslandSpawner.FindClearSpot(raft.Value + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 350f, CustomIslandSpawner.LandRadius("cideep"), 300f);
				if (s3.HasValue) yield return DynamicIslands.instance.SpawnIslandFile("cideep", s3.Value, true);
			}
			int spawned = IslandWorldState.Islands.Count - n0;
			yield return new WaitForSeconds(3f);
			float after = 0f;
			yield return FrameTime(120, ms => after = ms);
			Log(string.Format(Inv, "BENCH {0} deep islands ({1} objects each) at once: frame {2:F1} ms before, {3:F1} ms with them", spawned, deepF.Objects.Count, before, after));
			Check(ref ok, spawned >= 2 && after < Mathf.Max(before * 2.5f, before + 25f), spawned + " deep islands at once: frame " + before.ToString("F1") + " -> " + after.ToString("F1") + " ms");
			IslandWorldState.Remove("cideep");
			OnRaftCommand();
			if (ok) Log("PASS: the deep sea floor in a world"); else Fail("the deep sea floor in a world");
		}

		#endregion

		#region Two players and deep islands

		[ConsoleCommand(name: "CIDeepMP", docs: "Dev, in game (host): spawns and keeps a generated deep island 'cideepmp' (with pickups under water) and a ready-made atoll made from a seed, for the two-player test (mpfull.ps1 phase deep); each is logged with CIIslandHash")]
		public static void DeepMPCommand()
		{
			StartTest(DeepMPRoutine());
		}

		static IEnumerator DeepMPRoutine()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandFile f = MakeUnderwaterPickIsland();
			f.Name = "cideepmp";
			f.Save(IslandSpawner.PathFor("cideepmp"));
			string atoll = GeneratorWindow.MakeType(MapTypes.Get("atoll"), 20260926);
			if (atoll == null) { Fail("making the atoll"); yield break; }
			// (the bigger atoll first; within 600 m, where the other players load islands; kept from an earlier try)
			foreach (string name in new[] { atoll, "cideepmp" })
			{
				if (IslandWorldState.Islands.Any(i => i.HostName == name && i.Root != null)) continue;
				Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 600f);
				if (!spot.HasValue) { Fail("no open sea near the raft for '" + name + "'"); yield break; }
				yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			}
			IslandFile af = IslandFile.Load(IslandSpawner.PathFor(atoll));
			Log("Ready-made atoll '" + atoll + "': sea level " + af.WaterLevel + " m above its terrain's base (" + (af.WaterLevel == IslandFile.DeepWaterLevel ? "deep sea floor" : "shallow") + ")");
			foreach (string name in new[] { "cideepmp", atoll }) LogIslandHash(name);
			Log("PASS: cideepmp spawned (and " + atoll + ")");
		}

		[ConsoleCommand(name: "CIIslandHash", docs: "Dev, in game (either player): a hash of an island's terrain heights as spawned here, and how many objects it has under water and on land: CIIslandHash <island>")]
		public static void IslandHashCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e != null) LogIslandHash(e.HostName);
		}

		static void LogIslandHash(string name)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.Root != null && i.HostName == name);
			if (e == null) { Fail("no loaded island called '" + name + "' here"); return; }
			Terrain t = e.Root.GetComponentInChildren<Terrain>();
			uint h = 2166136261;
			if (t != null)
			{
				TerrainData td = t.terrainData;
				foreach (float v in td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution)) { unchecked { h = (h ^ (uint)Mathf.RoundToInt(v * 1e6f)) * 16777619; } }
			}
			Transform objs = e.Root.transform.Find("Objects");
			int under = objs != null ? objs.Cast<Transform>().Count(o => o.position.y < e.Position.y - 0.5f) : 0, all = objs != null ? objs.childCount : 0;
			int pickups = e.Root.GetComponentsInChildren<PickupItem_Networked>(false).Count(p => p.GetComponent<HarvestableTree>() == null && p.transform.position.y < e.Position.y - 0.5f);
			Log("Island hash '" + e.HostName + "': heights " + h.ToString("X8") + " (" + (t != null ? t.terrainData.heightmapResolution + " samples" : "no terrain") + "), objects " + all + ", under water " + under + ", pickups under water " + pickups + " (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}

		#endregion

		#region A long sail

		[ConsoleCommand(name: "CILongSail", docs: "Dev, in game (host, Normal world): sails about 14 km with automatic islands (one tried every 850 m - the spacing between islands is 800 m - or 150 m on when Raft's islands left no room), then checks the islands behind unloaded and memory, frame time and errors stayed flat. CILongSail [km] [m/s]")]
		public static void LongSailCommand(string[] args)
		{
			float km = 14f, speed = 30f;
			if (args != null && args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, Inv, out km);
			if (args != null && args.Length > 1) float.TryParse(args[1], System.Globalization.NumberStyles.Float, Inv, out speed);
			RunInBackground();
			StartTest(LongSailRoutine(km, speed, args != null && args.Length > 2 && args[2] == "keep"));
		}

		static IEnumerator LongSailRoutine(float km, float speed, bool keep = false)
		{
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj == null || raftObj.body == null || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			bool ok = true;
			Rigidbody body = raftObj.body;
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 dir = Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward;
			int errors = 0; string firstError = null;
			Application.LogCallback watch = (msg, trace, type) =>
			{
				if ((type == LogType.Exception || type == LogType.Error) && (msg.Contains("[CUSTOM ISLANDS]") || (trace ?? "").Contains("DynamicIslands")) && !msg.Contains("[CITEST]"))
				{ errors++; if (firstError == null) firstError = msg.Split('\n')[0]; }
			};
			Application.logMessageReceived += watch;
			int n0 = IslandWorldState.Islands.Count, spawned = 0, unloaded = 0;
			Application.LogCallback unloads = (msg, trace, type) => { if (msg.Contains("Unloaded island")) unloaded++; };
			Application.logMessageReceived += unloads;
			var samples = new List<float[]>(); // sailed m, frame ms, managed MB, Unity MB (0 if the build does not report it), loaded islands
			float sailed = 0f, sinceSpawn = 800f, nextSample = 0f;
			Vector3 last = body.position;
			if (player != null) player.transform.position = body.position + Vector3.up * 3f;
			while (sailed < km * 1000f)
			{
				yield return new WaitForFixedUpdate();
				KeepAlive(player);
				// (the raft is dragged through islands' land: one scrapes the host off, and an island a player is at stays loaded)
				if (player != null && Flat(player.transform.position - body.position).magnitude > 30f) PlayerMove.To(player, body.position + Vector3.up * 3f);
				body.MovePosition(body.position + dir * speed * Time.fixedDeltaTime);
				Vector3 d = Flat(body.position - last);
				last = body.position;
				if (d.magnitude < 100f) { sailed += d.magnitude; sinceSpawn += d.magnitude; }
				if (sinceSpawn > 850f)
				{
					sinceSpawn = 0f;
					int before = IslandWorldState.Islands.Count;
					string r = CustomIslandSpawner.TrySpawn(body.position, true);
					if (IslandWorldState.Islands.Count > before) spawned++;
					else sinceSpawn = 700f; // (Raft's own islands in the way: try again 150 m on)
					Log("  at " + (sailed / 1000f).ToString("F1") + " km: " + r);
				}
				if (sailed >= nextSample)
				{
					nextSample += 500f;
					float t0 = Time.realtimeSinceStartup;
					for (int i = 0; i < 30; i++) yield return null;
					float ms = (Time.realtimeSinceStartup - t0) / 30f * 1000f;
					float managed = GC.GetTotalMemory(false) / 1048576f, unity = Mathf.Max(UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(), UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()) / 1048576f;
					int loaded = IslandWorldState.Islands.Count(x => x.Root != null);
					samples.Add(new[] { sailed, ms, managed, unity, loaded });
					Log(string.Format(Inv, "  {0:F1} km: frame {1:F1} ms, managed {2:F0} MB, Unity {3:F0} MB, islands {4} ({5} loaded)", sailed / 1000f, ms, managed, unity, IslandWorldState.Islands.Count, loaded));
				}
			}
			body.velocity = Vector3.zero;
			yield return new WaitForSeconds(3f);
			Application.logMessageReceived -= watch;
			Application.logMessageReceived -= unloads;
			int added = IslandWorldState.Islands.Count - n0;
			int loadedNow = IslandWorldState.Islands.Count(x => x.Root != null);
			var near = new List<Vector3> { body.position };
			near.AddRange(Players.All.Where(p => p != null).Select(p => p.transform.position));
			int loadedFar = IslandWorldState.Islands.Count(x => x.Root != null && near.All(p => Flat(x.Position - p).magnitude - Mathf.Max(0f, CustomIslandSpawner.LandRadius(x.Name)) > WorldRules.UnloadDistance + 100f));
			// (the first samples against the last: Unity's own memory settles after the first islands)
			Func<int, float> firstThird = k => samples.Take(Mathf.Max(1, samples.Count / 3)).Skip(1).DefaultIfEmpty(samples[0]).Average(s => s[k]);
			Func<int, float> lastThird = k => samples.Skip(samples.Count - Mathf.Max(1, samples.Count / 3)).Average(s => s[k]);
			float f0 = firstThird(1), f1 = lastThird(1), m0 = firstThird(3), m1 = lastThird(3), g0 = firstThird(2), g1 = lastThird(2);
			Check(ref ok, added >= 10, "sailed " + (sailed / 1000f).ToString("F1") + " km: " + added + " islands appeared (" + spawned + " by the spawner), " + unloaded + " unloaded behind, " + loadedNow + " loaded now");
			Check(ref ok, loadedFar == 0 && unloaded >= added - loadedNow, "only the islands near the raft or a player stay loaded (" + loadedNow + " loaded, " + loadedFar + " far from everyone)");
			Check(ref ok, f1 < Mathf.Max(f0 * 1.5f, f0 + 8f), "frame time stays flat: " + f0.ToString("F1") + " ms at first, " + f1.ToString("F1") + " ms at the end");
			Check(ref ok, m1 < m0 + 400f && g1 < g0 + 150f, "memory stays flat: Unity " + m0.ToString("F0") + " -> " + m1.ToString("F0") + " MB, managed " + g0.ToString("F0") + " -> " + g1.ToString("F0") + " MB");
			Check(ref ok, errors == 0, errors + " errors from the mod while sailing" + (firstError != null ? " (first: " + firstError + ")" : ""));
			// (the test world stays as it was: no automatic islands left behind, unless kept for a check after)
			if (!keep) IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(n0).Select(x => x.Id).ToList(), true);
			if (ok) Log("PASS: a long sail"); else Fail("a long sail");
		}

		#endregion

		#region Helpers for the real mouse and key tests (tools\alltests.ps1 phase keys)

		/// <summary>
		/// Where something is on the screen, as fractions of Raft's window from its top left (tools\ui.ps1 turns them
		/// into screen pixels): object &lt;name&gt; (its middle), gizmo &lt;x|y|z&gt; (the move handle's shaft), terrain
		/// (the ground at the view's middle), panel (the tool panel), and the second placed object (other).
		/// </summary>
		[ConsoleCommand(name: "CIScreenPoint", docs: "Dev, editor: where something is on the screen, as fractions of the window from its top left: CIScreenPoint object <name> | last [n] | gizmo <x|y|z> | terrain | panel")]
		public static void ScreenPointCommand(string[] args)
		{
			Camera cam = Camera.main;
			if (cam == null || args == null || args.Length == 0) { Fail("usage: CIScreenPoint object <name> | gizmo <x|y|z> | terrain | panel"); return; }
			Vector3 screen;
			string what = args[0].ToLowerInvariant();
			if (what == "object" || what == "last")
			{
				string name = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "";
				int back;
				List<EditorGameObject> placed = PlacedEditorObjects();
				// (last [n]: the n-th object from the end, e.g. the ones CIPlaceAt just put down)
				EditorGameObject eo = what == "last" ? (int.TryParse(name.Length > 0 ? name : "1", out back) && back >= 1 && back <= placed.Count ? placed[placed.Count - back] : null)
					: placed.FirstOrDefault(o => o.GameObjectName.StartsWith(name) || o.name.StartsWith(name));
				if (eo == null) { Fail("no placed '" + name + "'"); return; }
				Renderer[] rs = eo.GetComponentsInChildren<Renderer>();
				Vector3 p = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }).center : eo.transform.position;
				screen = cam.WorldToScreenPoint(p);
			}
			else if (what == "gizmo")
			{
				var g = DynamicIslands.EditorGizmoHandler;
				if (g == null || g.mainTargetRoot == null) { Fail("nothing selected"); return; }
				object info = typeof(TransformGizmo).GetField("axisInfo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);
				string axis = args.Length > 1 ? args[1].ToLowerInvariant() : "x";
				Vector3 d = (Vector3)info.GetType().GetField(axis + "Direction").GetValue(info);
				screen = cam.WorldToScreenPoint(g.pivotPoint + d * g.GetHandleLength(TransformType.Move) * 0.7f);
				Vector3 tip = cam.WorldToScreenPoint(g.pivotPoint + d * g.GetHandleLength(TransformType.Move));
				Vector3 along = (tip - cam.WorldToScreenPoint(g.pivotPoint)).normalized;
				Log(string.Format(Inv, "Screen direction of the {0} handle: {1:F3} {2:F3}", axis, along.x, -along.y));
			}
			else if (what == "terrain")
			{
				Terrain t = terraineditor.terrain;
				Vector3 mid = t.transform.position + new Vector3(t.terrainData.size.x / 2f, 0f, t.terrainData.size.z / 2f);
				mid.y = t.SampleHeight(mid) + t.transform.position.y;
				screen = cam.WorldToScreenPoint(mid);
			}
			else if (what == "panel")
			{
				Vector3[] c = new Vector3[4];
				EditorUI.ToolFrame.GetWorldCorners(c);
				screen = (c[0] + c[2]) / 2f;
				screen.z = 1f; // (an overlay canvas: its corners are screen pixels already)
			}
			else { Fail("unknown: " + what); return; }
			Log(string.Format(Inv, "Screen point {0}: {1:F4} {2:F4} (of {3} x {4}, in front: {5})", string.Join(" ", args), screen.x / Screen.width, 1f - screen.y / Screen.height, Screen.width, Screen.height, screen.z > 0f));
		}

		/// <summary>A cheap fingerprint of the editor terrain's heights (sculpting changes it).</summary>
		static string TerrainFingerprint()
		{
			Terrain t = terraineditor.terrain;
			if (t == null) return "-";
			TerrainData td = t.terrainData;
			int res = td.heightmapResolution;
			float[,] h = td.GetHeights(0, 0, res, res);
			double sum = 0;
			for (int z = 0; z < res; z += 2) for (int x = 0; x < res; x += 2) sum += h[z, x];
			return (sum * td.size.y).ToString("F2", Inv);
		}

		/// <summary>The status bar's flashed message (EditorUI.Flash), if one is showing.</summary>
		static string FlashText()
		{
			FieldInfo f = typeof(EditorUI).GetField("flash", BindingFlags.NonPublic | BindingFlags.Static), u = typeof(EditorUI).GetField("flashUntil", BindingFlags.NonPublic | BindingFlags.Static);
			if (f == null || u == null) return "";
			return Time.unscaledTime < (float)u.GetValue(null) ? (string)f.GetValue(null) ?? "" : "";
		}

		/// <summary>Where the selection is in the view (0..1), or "-".</summary>
		static string SelectionInView()
		{
			var g = DynamicIslands.EditorGizmoHandler;
			if (g == null || g.mainTargetRoot == null || Camera.main == null) return "-";
			Renderer[] rs = g.mainTargetRoot.GetComponentsInChildren<Renderer>();
			Vector3 p = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }).center : g.mainTargetRoot.position;
			Vector3 v = Camera.main.WorldToViewportPoint(p);
			return string.Format(Inv, "{0:F2} {1:F2} dist {2:F1} pos {3:F2} {4:F2} {5:F2}", v.x, v.y, Vector3.Distance(Camera.main.transform.position, p), g.mainTargetRoot.position.x, g.mainTargetRoot.position.y, g.mainTargetRoot.position.z);
		}

		[ConsoleCommand(name: "CIEditorMore", docs: "Dev, editor: one line with what the mouse tests change: the terrain's fingerprint, the camera speed factor, the flashed status text, where the selection is in the view, and whether an object is being placed")]
		public static void EditorMoreCommand()
		{
			if (!DynamicIslands.InEditor()) { Fail("not in the editor"); return; }
			ObjectPlacer placer = ObjectPlacer.Current;
			var g = DynamicIslands.EditorGizmoHandler;
			Log("Editor more: heights " + TerrainFingerprint() + ", speed " + EditorCamera.SpeedFactor.ToString("F2", Inv) + ", flash '" + FlashText() + "', selection " + SelectionInView() +
				", selected " + (g != null ? g.SelectedRoots.Count : 0) + ", placing " + (placer != null ? placer.GameObjectName : "none") + ", undo " + CommandUndoRedo.UndoRedoManager.UndoCount + ", objects " + PlacedEditorObjects().Count);
		}

		[ConsoleCommand(name: "CIMouseScene", docs: "Dev, editor: for the real mouse tests - the camera 25 m from the middle of the terrain, looking at it from above at an angle, nothing selected; with 'objects' also a log 4 m right and a wall 4 m left of the middle: CIMouseScene [objects]")]
		public static void MouseSceneCommand(string[] args)
		{
			Camera cam = Camera.main;
			Terrain t = terraineditor.terrain;
			if (cam == null || t == null) { Fail("not in the editor"); return; }
			Vector3 mid = t.transform.position + new Vector3(t.terrainData.size.x / 2f, 0f, t.terrainData.size.z / 2f);
			mid.y = t.SampleHeight(mid) + t.transform.position.y;
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			cam.transform.position = mid + new Vector3(0f, 12f, -22f);
			cam.transform.LookAt(mid);
			if (args != null && args.Contains("objects"))
			{
				Transform placed = GameObject.Find("PlacedObjects").transform;
				Func<Vector3, Vector3> onGround = p => { p.y = t.SampleHeight(p) + t.transform.position.y; return p; };
				// (the log on the right: when it is selected its move gizmo's x arrow - several metres long from here - points away from the wall)
				PlaceForTest("Log", onGround(mid + new Vector3(4f, 0f, 0f)), placed);
				PlaceForTest("Block_Wall_Thatch", onGround(mid + new Vector3(-4f, 0f, 0f)), placed);
				Physics.SyncTransforms();
			}
			Log("Mouse scene: the camera looks at " + mid.ToString("F1") + (args != null && args.Contains("objects") ? ", a log and a wall placed there" : ""));
		}

		[ConsoleCommand(name: "CIDeselect", docs: "Dev, editor: selects nothing")]
		public static void DeselectCommand()
		{
			if (DynamicIslands.EditorGizmoHandler != null) DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			Log("Deselected");
		}

		#endregion

		#region Raft's interact key on a note and a lever

		[ConsoleCommand(name: "CIFace", docs: "Dev, in game: stands the player in front of an island object (its name, or a note's title) looking at it, and checks Raft's interaction ray finds it (the keys test then presses E): CIFace <island> <name or title>")]
		public static void FaceCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e != null) StartTest(FaceRoutine(e, args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : ""));
		}

		static IEnumerator FaceRoutine(IslandWorldState.Entry e, string what)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			IslandObjectRef r = e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(o => string.Equals(o.Name, what, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(ObjectProps.Get(o.Props, ObjectProps.NoteTitle), what, StringComparison.OrdinalIgnoreCase));
			if (r == null || player == null) { Fail("no object '" + what + "' on '" + e.HostName + "'"); yield break; }
			Transform holder = r.transform.Find(CustomNote.HolderName);
			Collider col = holder != null ? holder.GetComponent<Collider>() : null;
			if (col == null) { Fail("'" + what + "' has nothing to interact with"); yield break; }
			NoteReader.Close();
			yield return EnsureAlive();
			Physics.SyncTransforms();
			Vector3 target = col.bounds.center;
			// On the ground 1.6 m from it, first on the side towards the island's middle (open ground), then around it
			// until Raft's ray finds it (a rock or the slope can be in the way from one side)
			Vector3 toMiddle = Flat(new Vector3(e.Position.x, 0f, e.Position.z) - target).normalized;
			if (toMiddle.sqrMagnitude < 0.01f) toMiddle = Vector3.forward;
			CharacterController cc = player.PersonController.controller;
			Camera cam = Helper.MainCamera;
			RaycastInteractable found = null;
			for (int k = 0; k < 8 && (found == null || found.gameObject != holder.gameObject); k++)
			{
				Vector3 stand = target + Quaternion.Euler(0f, k * 45f, 0f) * toMiddle * 1.6f;
				RaycastHit ground;
				if (Physics.Raycast(stand + Vector3.up * 10f, Vector3.down, out ground, 30f, 1 << IslandSpawner.TerrainLayer)) stand.y = ground.point.y + 0.2f;
				cc.enabled = false;
				player.transform.position = stand;
				player.PersonController.SwitchControllerType(ControllerType.Ground);
				cc.enabled = true;
				yield return new WaitForSeconds(k == 0 ? 1f : 0.6f);
				Vector3 look = target - cam.transform.position;
				Look(player, Quaternion.LookRotation(Flat(look)).eulerAngles.y, -Mathf.Atan2(look.y, Flat(look).magnitude) * Mathf.Rad2Deg);
				yield return new WaitForSeconds(0.5f);
				found = Helper.FindInteractable(Player.UseDistance * 1.1f, QueryTriggerInteraction.Collide);
			}
			typeof(Behaviours).GetProperty("LastMessage").SetValue(null, "", null);
			if (found != null && found.gameObject == holder.gameObject) Log("Facing '" + what + "': Raft's interaction ray finds it from " + Vector3.Distance(cam.transform.position, target).ToString("F1") + " m");
			else Fail("facing '" + what + "': Raft's ray found " + (found != null ? found.name : "nothing"));
		}

		[ConsoleCommand(name: "CILastMessage", docs: "Dev, in game: the last message an island object gave this player, and whether the note reader is open")]
		public static void LastMessageCommand()
		{
			Log("Last message '" + Behaviours.LastMessage + "', island message '" + IslandInfo.LastMessage + "', note reader " + (NoteReader.IsOpen ? "open" : "closed"));
		}

		#endregion
	}
}
