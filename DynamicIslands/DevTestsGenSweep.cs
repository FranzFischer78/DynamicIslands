using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// The generator sweep (the user: "try out more generation combinations and fix all the errors that arise from different
	/// ones"): Generate island run through the Generate island window itself - its three tabs, Normal, Randomize existing and
	/// Ready-made - over a fixed list of cases, so a failure repeats. Each case counts the exceptions and errors logged while
	/// it runs (Raft's own too) and checks the island it made: made, land above the sea, no NaN or huge sizes, a sensible
	/// number of objects inside the island's area, and the window's "can players reach it" line agreeing with the made ground.
	/// </summary>
	public static partial class DevTests
	{
		/// <summary>One case of the generator sweep.</summary>
		class GenSweepCase
		{
			public int Index;
			public string Group = "";
			public int Tab;
			/// <summary>Normal: the window's settings. Randomize existing and Ready-made: only the seed is used.</summary>
			public IslandGenSettings S = new IslandGenSettings();
			/// <summary>Randomize existing: the Raft island's scene, a variation of it or something new like it, its Rebuild it
			/// choice (null = what picking the island chooses, "" = none).</summary>
			public string Island;
			public bool Variation;
			public string Remake;
			/// <summary>Ready-made: the built-in map type's name.</summary>
			public string Type;
			/// <summary>In the quick run (CIGenSweep quick).</summary>
			public bool Quick;

			public string Describe()
			{
				string head = "#" + Index + " [" + Group + "] ";
				if (Tab == GeneratorWindow.TabRandomize)
					return head + "Randomize existing: " + Island + (Variation ? " (a variation)" : " (something new like it)") + ", seed " + S.Seed +
						(Remake == null ? "" : ", Rebuild it " + (Remake.Length == 0 ? "none" : Remake));
				if (Tab == GeneratorWindow.TabReady) return head + "Ready-made: " + Type + ", seed " + S.Seed;
				return head + "Normal: " + GenSweepSettingsText(S);
			}
		}

		/// <summary>Cases per chunk when the chunks aren't given: about 4 minutes each (measure, then change it).</summary>
		const int GenSweepPerChunk = 30;
		/// <summary>Longest wait for one case (scenes for buildings, caves and Raft's kinds under water load first).</summary>
		const float GenSweepTimeout = 180f;

		static readonly MethodInfo GenSweepOnGenerate = typeof(GeneratorWindow).GetMethod("OnGenerate", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly FieldInfo GenSweepMapType = typeof(GeneratorWindow).GetField("mapType", BindingFlags.Instance | BindingFlags.NonPublic);
		static readonly FieldInfo GenSweepLoading = typeof(GeneratorWindow).GetField("loadingBuildings", BindingFlags.Instance | BindingFlags.NonPublic);

		/// <summary>The settings that differ from a new IslandGenSettings, as "Field=value" (enough to make the case again).</summary>
		static string GenSweepSettingsText(IslandGenSettings s)
		{
			string[] defaults = new IslandGenSettings().ToText().Split('\n');
			return string.Join(" ", s.ToText().Split('\n').Where(l => l.Length > 0 && !defaults.Contains(l)).ToArray());
		}

		static float GenSweepF(System.Random r, float lo, float hi) { return lo + (float)r.NextDouble() * (hi - lo); }

		/// <summary>A value of 0..1, or 0 half of the time (the shaping sliders are mostly off).</summary>
		static float GenSweepSome(System.Random r) { return r.Next(2) == 0 ? 0f : (float)r.NextDouble(); }

		/// <summary>
		/// Every case, always in the same order (a case's number names it in CIGenCase). Raft's measured islands
		/// (raft_islands.txt) and the built-in map types are read as they are; they come last, so a change to them leaves the
		/// numbers of the Normal cases as they were.
		/// </summary>
		static List<GenSweepCase> GenSweepCases()
		{
			var list = new List<GenSweepCase>();
			Action<string, IslandGenSettings, bool> normal = (group, s, quick) => list.Add(new GenSweepCase { Group = group, Tab = GeneratorWindow.TabNormal, S = s, Quick = quick });
			Func<int, IslandGenSettings> basic = seed => new IslandGenSettings { Seed = seed, Radius = 70f, Height = 30f, ObjectDensity = 0.4f };
			int styles = TerrainPainter.Styles.Length, shapes = IslandShapes.Names.Length;

			// 1. Every style x layout
			for (int style = 0; style < styles; style++)
				for (int shape = 0; shape < shapes; shape++)
				{
					IslandGenSettings s = basic(100 + style * 10 + shape);
					s.Style = style; s.Shape = shape;
					normal("style x layout", s, shape == style || (style == styles - 1 && shape == shapes - 1));
				}

			// 2. Sizes (Raft's small, large and Balboa) x heights (lowest, highest) x layouts
			float[] radii = { RaftIslands.SmallRadius, RaftIslands.LargeRadius, RaftIslands.BalboaRadius };
			float[] heights = { IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight };
			for (int ri = 0; ri < radii.Length; ri++)
				for (int hi = 0; hi < heights.Length; hi++)
					for (int shape = 0; shape < shapes; shape++)
					{
						IslandGenSettings s = basic(300 + ri * 100 + hi * 10 + shape);
						s.Radius = radii[ri]; s.Height = heights[hi]; s.Shape = shape; s.Style = shape % styles;
						normal("size x height", s, shape == IslandShapes.Round && ri != 1 && hi != ri / 2);
					}

			// 3. Peaks at their least and most
			foreach (int peaks in new[] { 1, IslandGenSettings.MaxPeaks })
				for (int shape = 0; shape < shapes; shape++)
				{
					IslandGenSettings s = basic(1000 + peaks * 10 + shape);
					s.Peaks = peaks; s.Shape = shape; s.Height = 60f; s.Radius = 100f;
					normal("peaks", s, false);
				}

			// 4. Coast and bays at their extremes
			foreach (float coast in new[] { 0f, 1f })
				foreach (float bays in new[] { 0f, 1f })
					foreach (int shape in new[] { IslandShapes.Round, IslandShapes.Crescent, IslandShapes.Plateau, IslandShapes.Archipelago })
					{
						IslandGenSettings s = basic(1100 + (int)(coast * 100f) + (int)(bays * 10f) + shape);
						s.Coast = coast; s.Bays = bays; s.Shape = shape; s.Radius = 90f;
						normal("coast and bays", s, coast > 0.5f && bays > 0.5f && shape == IslandShapes.Crescent);
					}

			// 5. Hostile creatures 0, 2 and the most, in every style (the most also with the most friendly ones and sea life)
			foreach (int hostiles in new[] { 0, 2, IslandGenSettings.MaxCreatureSpots })
				for (int style = 0; style < styles; style++)
				{
					IslandGenSettings s = basic(1200 + hostiles * 10 + style);
					s.Style = style; s.Hostiles = hostiles; s.Radius = 90f;
					if (hostiles == IslandGenSettings.MaxCreatureSpots) { s.Friendly = IslandGenSettings.MaxCreatureSpots; s.SeaLife = IslandGenSettings.MaxCreatureSpots; }
					normal("creatures", s, hostiles == IslandGenSettings.MaxCreatureSpots && style == TerrainPainter.Volcanic);
				}

			// 6. Buildings and caves: off, a cave alone, then on at every count 1..max (kinds in turn, a cave with the odd
			// counts), and every kind with a cave
			List<string> kinds = GenBuildings.Kinds;
			{
				IslandGenSettings off = basic(1300); off.Radius = 110f; off.Height = 35f;
				normal("buildings", off, false);
				IslandGenSettings cave = off.Copy(); cave.Seed = 1301; cave.Caves = true;
				normal("buildings", cave, false);
				for (int n = 1; n <= GenBuildings.MaxCount; n++)
				{
					IslandGenSettings s = off.Copy();
					s.Seed = 1310 + n; s.Buildings = true; s.BuildingCount = n; s.BuildingKind = kinds[n % kinds.Count]; s.Caves = n % 2 == 1; s.Style = n % styles;
					normal("buildings", s, n == GenBuildings.MaxCount);
				}
				for (int k = 0; k < kinds.Count; k++)
				{
					IslandGenSettings s = off.Copy();
					s.Seed = 1350 + k; s.Buildings = true; s.BuildingCount = 3; s.BuildingKind = kinds[k]; s.Caves = true; s.Style = k % styles;
					normal("building kinds", s, false);
				}
			}

			// 7. Every other setting at its extremes, one at a time
			var one = new List<KeyValuePair<string, Action<IslandGenSettings>>>
			{
				new KeyValuePair<string, Action<IslandGenSettings>>("stretched", x => { x.Stretch = IslandGenSettings.MaxStretch; x.StretchAngle = 45f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("round hills", x => x.PeakShape = 0f),
				new KeyValuePair<string, Action<IslandGenSettings>>("spires", x => { x.PeakShape = 1f; x.Height = 120f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("smooth", x => x.Roughness = 0f),
				new KeyValuePair<string, Action<IslandGenSettings>>("rough", x => x.Roughness = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("terraces", x => x.Terraces = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("lakes", x => x.Lakes = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("valleys", x => x.Valleys = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("erosion", x => x.Erosion = 1f),
				new KeyValuePair<string, Action<IslandGenSettings>>("all shaping", x => { x.Terraces = 1f; x.Lakes = 1f; x.Valleys = 1f; x.Erosion = 1f; x.PeakShape = 1f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("cliffs, no beach", x => { x.Cliffs = 1f; x.BeachWidth = 0f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("widest beach", x => { x.BeachWidth = 1f; x.Cliffs = 0f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("shallow sea floor", x => x.SeaFloor = IslandGenSettings.SeaFloorShallow),
				new KeyValuePair<string, Action<IslandGenSettings>>("rocky seabed", x => x.Seabed = IslandGenSettings.SeabedRocky),
				new KeyValuePair<string, Action<IslandGenSettings>>("reef seabed", x => x.Seabed = IslandGenSettings.SeabedReef),
				new KeyValuePair<string, Action<IslandGenSettings>>("no shelf, sheer drop", x => { x.Shelf = 0f; x.DropOff = 1f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("widest shelf, gentle drop", x => { x.Shelf = 1f; x.DropOff = 0f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("no objects", x => x.ObjectDensity = 0f),
				new KeyValuePair<string, Action<IslandGenSettings>>("densest", x => { x.ObjectDensity = 1f; x.Clusters = 1f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("every object slider full", x => { x.Trees = 1f; x.Bushes = 1f; x.Rocks = 1f; x.BeachThings = 1f; x.Harvest = 1f; x.Water = 1f; x.SeaRocks = 1f; x.SeaFinds = 1f; x.Sunken = 1f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("every object slider none", x => { x.Trees = 0f; x.Bushes = 0f; x.Rocks = 0f; x.BeachThings = 0f; x.Harvest = 0f; x.Water = 0f; x.SeaRocks = 0f; x.SeaFinds = 0f; x.Sunken = 0f; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("gather everything", x => { x.Gather = 1f; x.Shallows = 1f; x.ShallowsDepth = GenGather.MaxDepth; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("gather, shallowest", x => { x.Gather = 1f; x.Shallows = 1f; x.ShallowsDepth = GenGather.MinDepth; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("most loot", x => { x.Loot = IslandGenSettings.MaxLoot; x.LootMin = 5; x.LootMax = 5; x.LootHidden = true; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("most quest steps", x => x.QuestSteps = GenQuest.MaxSteps),
				new KeyValuePair<string, Action<IslandGenSettings>>("boss creatures", x => { x.Hostiles = 6; x.Difficulty = "Boss"; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("smallest", x => { x.Radius = IslandGenSettings.MinRadius; x.Height = IslandGenSettings.MinHeight; }),
				new KeyValuePair<string, Action<IslandGenSettings>>("biggest", x => { x.Radius = IslandGenSettings.MaxRadius; x.Height = IslandGenSettings.MaxHeight; }),
			};
			for (int f = 1; f <= GenFeatures.Max; f++)
			{
				int features = f;
				one.Add(new KeyValuePair<string, Action<IslandGenSettings>>("Raft's features " + features, x => { x.Features = features; x.Radius = 110f; }));
			}
			for (int i = 0; i < one.Count; i++)
			{
				IslandGenSettings s = basic(1400 + i);
				one[i].Value(s);
				normal("extreme: " + one[i].Key, s, one[i].Key == "all shaping" || one[i].Key == "Raft's features " + GenFeatures.Max);
			}

			// 8. Fixed seeds, settings as the window opens with them
			foreach (int seed in new[] { 0, 1, 42, 999999, 123456789, -7 })
				normal("seed", new IslandGenSettings { Seed = seed }, seed == 42);

			// 9. Seeded random mixes of everything
			for (int i = 0; i < 60; i++)
			{
				var r = new System.Random(20261016 + i);
				var s = new IslandGenSettings
				{
					Seed = r.Next(1, 999999),
					Radius = GenSweepF(r, IslandGenSettings.MinRadius, IslandGenSettings.MaxRadius),
					Height = GenSweepF(r, IslandGenSettings.MinHeight, IslandGenSettings.MaxHeight),
					Roughness = (float)r.NextDouble(),
					Peaks = r.Next(1, IslandGenSettings.MaxPeaks + 1),
					ObjectDensity = (float)r.NextDouble(),
					Style = r.Next(styles),
					Shape = r.Next(shapes),
					Coast = r.Next(3) == 0 ? -1f : (float)r.NextDouble(),
					Bays = GenSweepSome(r),
					BeachWidth = (float)r.NextDouble(),
					Cliffs = GenSweepSome(r),
					Stretch = GenSweepF(r, 1f, IslandGenSettings.MaxStretch),
					StretchAngle = GenSweepF(r, 0f, 180f),
					PeakShape = (float)r.NextDouble(),
					Terraces = GenSweepSome(r),
					Lakes = GenSweepSome(r),
					Valleys = GenSweepSome(r),
					Erosion = GenSweepSome(r),
					SeaFloor = r.Next(2),
					Shelf = (float)r.NextDouble(),
					DropOff = (float)r.NextDouble(),
					Seabed = r.Next(3),
					Hostiles = r.Next(0, IslandGenSettings.MaxCreatureSpots + 1),
					Friendly = r.Next(0, 9),
					SeaLife = r.Next(0, 9),
					Loot = r.Next(0, IslandGenSettings.MaxLoot + 1),
					LootHidden = r.Next(2) == 0,
					Buildings = r.Next(4) == 0,
					BuildingKind = kinds[r.Next(kinds.Count)],
					BuildingCount = r.Next(1, GenBuildings.MaxCount + 1),
					Caves = r.Next(4) == 0,
					QuestSteps = r.Next(0, GenQuest.MaxSteps + 1),
					Features = r.Next(0, GenFeatures.Max + 1),
					Gather = GenSweepSome(r),
					Shallows = GenSweepSome(r),
				};
				normal("random mix", s, i < 2);
			}

			// 10. Randomize existing: every Raft island offered, as a variation (with what picking it chooses); every fourth
			// also as something new like it; the story islands with designs also without Rebuild it
			List<RaftIsland> offered = RaftIslands.Offered;
			RaftIsland biggest = offered.OrderByDescending(i => i.Radius).FirstOrDefault();
			for (int i = 0; i < offered.Count; i++)
			{
				RaftIsland island = offered[i];
				list.Add(new GenSweepCase { Group = "randomize existing", Tab = GeneratorWindow.TabRandomize, Island = island.Scene, Variation = true, S = new IslandGenSettings { Seed = 2000 + i }, Quick = i == 0 || island == biggest });
				if (i % 4 == 0)
					list.Add(new GenSweepCase { Group = "randomize existing, like it", Tab = GeneratorWindow.TabRandomize, Island = island.Scene, Variation = false, S = new IslandGenSettings { Seed = 2100 + i } });
				if (Remakes.For(island.Scene).Count > 0)
					list.Add(new GenSweepCase { Group = "randomize existing, no rebuild", Tab = GeneratorWindow.TabRandomize, Island = island.Scene, Variation = true, Remake = "", S = new IslandGenSettings { Seed = 2200 + i } });
			}

			// 11. Ready-made: every built-in kind, two seeds
			foreach (MapType type in MapTypes.All.Where(t => t.FromFile == null))
				foreach (int seed in new[] { 1, 2 })
					list.Add(new GenSweepCase { Group = "ready-made", Tab = GeneratorWindow.TabReady, Type = type.Name, S = new IslandGenSettings { Seed = seed },
						Quick = seed == 1 && (type.Name == "random" || type.Name == "wreck" || type.Name == "sky" || type.Name == "sunken") });

			for (int i = 0; i < list.Count; i++) list[i].Index = i;
			return list;
		}

		#region Commands

		[ConsoleCommand(name: "CIGenSweep", docs: "Dev, editor: the generator sweep - Generate island through its window (Normal, Randomize existing, Ready-made) over a fixed list of cases (every style x layout, sizes, heights, peaks, coast and bays, creatures, buildings and caves, every other setting at its extremes, fixed seeds, seeded random mixes, every Raft island, every ready-made kind): per case no exception or error, the island made with land, no NaN or huge sizes, objects in range and in the island's area, the reach line agrees with the made ground. A failing case is one FAIL line with its number and settings (CIGenCase <number> runs it alone). CIGenSweep quick (about 20 cases, the default) | <chunk> [chunks] (1-based; chunks of about " + "30" + " cases when not given) | all | list")]
		public static void GenSweepCommand(string[] args)
		{
			List<GenSweepCase> all = GenSweepCases();
			string a0 = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "quick";
			int defaultChunks = Mathf.Max(1, (all.Count + GenSweepPerChunk - 1) / GenSweepPerChunk);
			if (a0 == "list")
			{
				foreach (GenSweepCase c in all) Log(c.Describe() + (c.Quick ? " (quick)" : ""));
				Log("PASS: generator sweep list: " + all.Count + " cases, " + all.Count(c => c.Quick) + " in the quick run, " + defaultChunks + " chunks of about " + GenSweepPerChunk);
				return;
			}
			if (a0 == "quick") { StartTest(GenSweepRoutine(all.Where(c => c.Quick).ToList(), "quick")); return; }
			if (a0 == "all") { StartTest(GenSweepRoutine(all, "all")); return; }
			int chunk, chunks = defaultChunks;
			if (!int.TryParse(a0, NumberStyles.Integer, CultureInfo.InvariantCulture, out chunk) ||
				(args.Length > 1 && !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out chunks)) || chunks < 1 || chunk < 1 || chunk > chunks)
			{
				Fail("generator sweep: CIGenSweep quick | all | list | <chunk 1-" + defaultChunks + "> [chunks, default " + defaultChunks + "]");
				return;
			}
			int size = (all.Count + chunks - 1) / chunks;
			StartTest(GenSweepRoutine(all.Skip((chunk - 1) * size).Take(size).ToList(), "chunk " + chunk + "/" + chunks));
		}

		[ConsoleCommand(name: "CIGenCase", docs: "Dev, editor: runs cases of the generator sweep alone, by their numbers (CIGenSweep list shows them): CIGenCase <number> [number ...]")]
		public static void GenCaseCommand(string[] args)
		{
			List<GenSweepCase> all = GenSweepCases();
			var run = new List<GenSweepCase>();
			foreach (string a in args ?? new string[0])
			{
				int i;
				if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) || i < 0 || i >= all.Count) { Fail("generator case: no case " + a + " (0-" + (all.Count - 1) + ")"); return; }
				run.Add(all[i]);
			}
			if (run.Count == 0) { Fail("generator case: CIGenCase <number> [number ...] (0-" + (all.Count - 1) + ")"); return; }
			StartTest(GenSweepRoutine(run, "case" + (run.Count == 1 ? " #" + run[0].Index : "s " + string.Join(" ", run.Select(c => "#" + c.Index).ToArray()))));
		}

		#endregion

		#region The run

		static IEnumerator GenSweepRoutine(List<GenSweepCase> cases, string what)
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || !PlaceableCatalog.IsBuilt) { Fail("generator sweep " + what + ": open the editor first"); yield break; }
			if (GenSweepOnGenerate == null || GenSweepMapType == null || GenSweepLoading == null) { Fail("generator sweep " + what + ": the Generate island window's OnGenerate, mapType or loadingBuildings is gone (renamed?)"); yield break; }
			int errors = 0;
			string firstError = null;
			// (every exception and error while a case runs, Raft's own too; Unity's "Screen position out of view frustum" is a
			// camera drawing while Raft is minimised, as the tests run it - not the generator's)
			Application.LogCallback counter = (msg, trace, type) =>
			{
				if (type == LogType.Exception || ((type == LogType.Error || type == LogType.Assert) && !msg.StartsWith("[CITEST]") && !msg.StartsWith("Screen position out of view frustum")))
				{
					errors++;
					if (firstError == null) firstError = msg + " " + (trace ?? "").Split('\n').FirstOrDefault();
				}
			};
			var failed = new List<string>();
			int offByOne = 0;
			float t0 = Time.realtimeSinceStartup;
			Application.logMessageReceived += counter;
			try
			{
				for (int i = 0; i < cases.Count; i++)
				{
					GenSweepCase c = cases[i];
					errors = 0;
					firstError = null;
					var problems = new List<string>();
					var notes = new List<string>();
					float c0 = Time.realtimeSinceStartup;
					yield return GenSweepOne(c, problems, notes);
					// (an error logged a moment after Generate - a coroutine it started - still counts for this case)
					yield return null;
					if (errors > 0) problems.Add(errors + " exception(s)/error(s) logged, the first: " + firstError);
					float secs = Time.realtimeSinceStartup - c0;
					if (notes.Any(n => n.StartsWith("reach off by one"))) offByOne++;
					if (problems.Count > 0)
					{
						failed.Add("#" + c.Index);
						Fail("gen case " + c.Describe() + " (" + secs.ToString("F1", CultureInfo.InvariantCulture) + " s): " + string.Join("; ", problems.ToArray()) + (notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : ""));
					}
					else if (secs > 30f || notes.Count > 0)
						Log("  gen case #" + c.Index + " ok in " + secs.ToString("F1", CultureInfo.InvariantCulture) + " s" + (notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : ""));
					GenSweepTidy();
					// (now and then the textures and meshes of the islands made before go too)
					if (i % 20 == 19) { yield return Resources.UnloadUnusedAssets(); GC.Collect(); }
				}
			}
			finally
			{
				Application.logMessageReceived -= counter;
				if (GeneratorWindow.IsOpen) GeneratorWindow.Close();
			}
			string summary = "generator sweep " + what + ": " + cases.Count + " cases, " + failed.Count + " failed" + (failed.Count > 0 ? " (" + string.Join(" ", failed.ToArray()) + ")" : "") +
				(offByOne > 0 ? ", " + offByOne + " with the reach line one level off the made ground" : "") + ", " + (Time.realtimeSinceStartup - t0).ToString("F0", CultureInfo.InvariantCulture) + " s";
			if (failed.Count == 0) Log("PASS: " + summary); else Fail(summary);
		}

		/// <summary>One case through the window, then the island it made checked.</summary>
		static IEnumerator GenSweepOne(GenSweepCase c, List<string> problems, List<string> notes)
		{
			if (GeneratorWindow.IsOpen) GeneratorWindow.Close();
			yield return null;
			GeneratorWindow.Open();
			yield return null;
			GeneratorWindow w = GeneratorWindow.Instance;
			if (w == null || !GeneratorWindow.IsOpen) { problems.Add("the Generate island window did not open"); yield break; }
			GeneratorWindow.ShowTab(c.Tab);
			yield return null;

			// The window's settings, as the controls set them
			MapType type = null;
			float elevation = 0f;
			string madeName = null, setupError = null;
			IslandGenSettings asked = null;
			try
			{
				if (c.Tab == GeneratorWindow.TabNormal) { GeneratorWindow.Use(c.S); asked = c.S; }
				else if (c.Tab == GeneratorWindow.TabRandomize)
				{
					RaftIsland island = RaftIslands.Offered.FirstOrDefault(x => x.Scene == c.Island);
					if (island == null) setupError = "Raft's island " + c.Island + " isn't offered (raft_islands.txt changed?)";
					else
					{
						GeneratorWindow.Pick(island, c.Variation);
						IslandGenSettings s = GeneratorWindow.Current;
						s.Seed = c.S.Seed;
						if (c.Remake != null) s.Remake = c.Remake;
						GeneratorWindow.Use(s);
					}
				}
				else
				{
					type = MapTypes.All.FirstOrDefault(t => t.FromFile == null && t.Name == c.Type);
					if (type == null) setupError = "no built-in ready-made kind " + c.Type;
					else
					{
						GenSweepMapType.SetValue(w, MapTypes.All.IndexOf(type));
						IslandGenSettings s = GeneratorWindow.Current;
						s.Seed = c.S.Seed;
						GeneratorWindow.Use(s);
						IslandGenSettings rolled = MapTypes.Roll(type, new System.Random(c.S.Seed), out elevation);
						madeName = MapTypes.FreeFileName(type, rolled);
					}
				}
			}
			catch (Exception e) { setupError = "setting up the window threw: " + e.GetType().Name + ": " + e.Message; }
			if (setupError != null) { problems.Add(setupError); yield break; }
			yield return null;

			// The reach line, as the window shows it before Generate
			ReachResult shown = null;
			string shownText = "";
			try { shown = GeneratorWindow.ReachNow(); shownText = GeneratorWindow.ReachText; }
			catch (Exception e) { problems.Add("the reach line threw: " + e.Message); }
			bool landless = type != null && (type.Build != null || Mathf.Abs(elevation) > 1f);
			if (shown == null) problems.Add("no reach line: \"" + shownText + "\"");
			else
			{
				string[] starts = { "Easy:", "Reachable:", "Possible but tricky:", "Possible but unlikely:", "Not from the raft" };
				bool fits = shown.Level >= 0 && shown.Level < starts.Length && (shownText.StartsWith(starts[shown.Level]) || (shown.Level == IslandReach.Tricky && shownText.StartsWith("By diving:")));
				if (!fits) problems.Add("the reach line doesn't fit its level " + shown.Level + ": \"" + shownText + "\"");
				if (shownText.Contains("NaN") || shownText.Contains("Infinity") || shownText.Contains("-2147483648")) problems.Add("the reach line has a broken number: \"" + shownText + "\"");
			}

			// Generate (Ready-made: Make, clicked twice - the first click asks to save the island first)
			string generateError = null;
			float g0 = Time.realtimeSinceStartup;
			try
			{
				GenSweepOnGenerate.Invoke(w, null);
				if (c.Tab == GeneratorWindow.TabReady && GeneratorWindow.IsOpen) GenSweepOnGenerate.Invoke(w, null);
			}
			catch (TargetInvocationException e) { generateError = "Generate threw: " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message; }
			catch (Exception e) { generateError = "Generate threw: " + e.GetType().Name + ": " + e.Message; }
			if (generateError != null) { problems.Add(generateError); yield break; }
			// (Raft's scenes for buildings, caves and the kinds under water load first: the window waits, then generates)
			while (GeneratorWindow.IsOpen && Time.realtimeSinceStartup - g0 < GenSweepTimeout && (Time.realtimeSinceStartup - g0 < 1f || (bool)GenSweepLoading.GetValue(w)))
				yield return new WaitForSecondsRealtime(0.25f);
			if (GeneratorWindow.IsOpen)
			{
				problems.Add(Time.realtimeSinceStartup - g0 >= GenSweepTimeout ? "not done after " + GenSweepTimeout.ToString("F0") + " s" : "nothing made: the window stayed open");
				yield break;
			}
			if (c.Tab != GeneratorWindow.TabReady)
			{
				if (!(GeneratorWindow.LastResult ?? "").StartsWith("Island ")) { problems.Add("Generate said nothing about the island (\"" + GeneratorWindow.LastResult + "\")"); yield break; }
			}
			else
			{
				if (madeName == null || !File.Exists(IslandSpawner.PathFor(madeName))) { problems.Add("Make saved no island file '" + madeName + "'"); yield break; }
				// (an island with objects of Raft's other scenes loads once they are in)
				float l0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - l0 < GenSweepTimeout && !GenSweepLoaded()) yield return new WaitForSecondsRealtime(0.25f);
				if (!GenSweepLoaded()) { problems.Add("the made island '" + madeName + "' did not load in the editor"); GenSweepDelete(madeName); yield break; }
			}
			yield return null;
			yield return null;

			try { GenSweepCheck(c, asked, shown, landless, type, problems, notes); }
			catch (Exception e) { problems.Add("checking the island threw: " + e.GetType().Name + ": " + e.Message); }
			if (madeName != null) GenSweepDelete(madeName);
		}

		/// <summary>The island in the editor as the case made it: its ground, the generator's report, its objects, its reach.</summary>
		static void GenSweepCheck(GenSweepCase c, IslandGenSettings asked, ReachResult shown, bool landless, MapType type, List<string> problems, List<string> notes)
		{
			Terrain terrain = terraineditor.terrain;
			TerrainData data = terrain.terrainData;
			Vector3 size = data.size;
			if (!GenSweepFinite(size) || size.x <= 0f || size.z <= 0f || size.x > 5000f || size.y > 5000f || size.z > 5000f) { problems.Add("terrain size " + size); return; }
			int res = data.heightmapResolution;
			float[,] h = data.GetHeights(0, 0, res, res);
			float sea = DynamicIslands.EditorWaterLevel;
			var m = new float[res, res];
			int land = 0, broken = 0;
			float low = float.MaxValue, high = float.MinValue;
			for (int z = 0; z < res; z++)
				for (int x = 0; x < res; x++)
				{
					float v = h[z, x] * size.y;
					if (float.IsNaN(v) || float.IsInfinity(v)) { broken++; v = 0f; }
					m[z, x] = v;
					if (v > sea) land++;
					low = Mathf.Min(low, v); high = Mathf.Max(high, v);
				}
			if (broken > 0) problems.Add(broken + " ground heights are NaN or infinite");
			float step = size.x / (res - 1);
			float top = high - sea;
			if (type == null || (type.Build == null && !landless))
			{
				if (land == 0) problems.Add("no land above the sea (highest ground " + top.ToString("F1") + " m)");
			}
			else if (type.Build == null && high - low < 1f) problems.Add("flat ground: no island (" + low.ToString("F1") + "-" + high.ToString("F1") + " m)");
			// (Normal: the tallest peak is the height asked for)
			if (asked != null && land > 0 && top > asked.Height * 1.1f + 2f) problems.Add("the top is " + top.ToString("F1") + " m, " + asked.Height.ToString("F1") + " m asked for");

			// The generator's report (Normal and Randomize existing)
			if (type == null)
			{
				GenReport r = IslandGenerator.LastReport;
				if (r == null) problems.Add("no generator report");
				else
				{
					float[] values = { r.LandLength, r.LandWidth, r.Top, r.LandArea, r.Seconds };
					if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < 0f)) problems.Add("the report has a broken size: land " + r.LandLength + " x " + r.LandWidth + ", top " + r.Top + ", area " + r.LandArea);
					else if (r.LandLength > size.x * 1.5f || r.LandWidth > size.z * 1.5f || r.Top > IslandGenSettings.MaxHeight + 20f || r.LandArea > size.x * size.z)
						problems.Add("the report's sizes are bigger than the terrain: land " + r.LandLength.ToString("F0") + " x " + r.LandWidth.ToString("F0") + " m, top " + r.Top.ToString("F0") + " m, area " + r.LandArea.ToString("F0") + " m² (terrain " + size.x.ToString("F0") + " m)");
					if (r.Wanted < 0) problems.Add("the report wanted " + r.Wanted + " objects");
				}
			}

			// The objects: how many, and all of them inside the island's area
			List<EditorGameObject> objs = PlacedEditorObjects();
			int n = objs.Count;
			if (n > IslandGenerator.MaxObjects + 3000) problems.Add(n + " objects (the most is " + IslandGenerator.MaxObjects + " and what buildings, creatures and loot add)");
			// (Normal, with trees, bushes or rocks asked for on land of some size: something stands on it)
			if (asked != null && asked.Amount(asked.Trees) + asked.Amount(asked.Bushes) + asked.Amount(asked.Rocks) >= 0.1f && asked.Radius >= 20f && land > 0 && n == 0)
				problems.Add("no objects (trees " + asked.Amount(asked.Trees).ToString("F2") + ", bushes " + asked.Amount(asked.Bushes).ToString("F2") + ", rocks " + asked.Amount(asked.Rocks).ToString("F2") + ")");
			Vector3 origin = terrain.transform.position;
			int outside = 0;
			string firstOutside = null;
			foreach (EditorGameObject e in objs)
			{
				Vector3 p = e.transform.position;
				bool inside = GenSweepFinite(p) && p.x >= origin.x - 100f && p.x <= origin.x + size.x + 100f && p.z >= origin.z - 100f && p.z <= origin.z + size.z + 100f &&
					p.y >= origin.y - 300f && p.y <= origin.y + size.y + 300f && GenSweepFinite(e.transform.lossyScale) && e.transform.lossyScale.magnitude < 1000f;
				if (inside) continue;
				outside++;
				if (firstOutside == null) firstOutside = e.name + " at " + p + " scale " + e.transform.lossyScale;
			}
			if (outside > 0) problems.Add(outside + " objects outside the island's area or with a broken position or size, the first: " + firstOutside);

			// The reach line before Generate and the ground made agree (a level apart is noted: the line works on a coarser grid)
			if (shown != null && !landless && land > 0)
			{
				ReachResult made = IslandReach.Assess(m, step, sea);
				int apart = Mathf.Abs(made.Level - shown.Level);
				if (apart > 1) problems.Add("the reach line said level " + shown.Level + " (\"" + shown.Text + "\"), the made ground is level " + made.Level + " (\"" + made.Text + "\")");
				else if (apart == 1) notes.Add("reach off by one: the line " + shown.Level + ", the made ground " + made.Level);
			}
		}

		static bool GenSweepFinite(Vector3 v) { return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)); }

		/// <summary>A ready-made island is in the editor (DynamicIslands.LoadIsland puts its objects under LoadedObjects).</summary>
		static bool GenSweepLoaded()
		{
			GameObject placed = GameObject.Find("PlacedObjects");
			return placed != null && placed.transform.Cast<Transform>().Any(t => t.name == "LoadedObjects");
		}

		static void GenSweepDelete(string name)
		{
			try { string f = IslandSpawner.PathFor(name); if (File.Exists(f)) File.Delete(f); }
			catch (Exception e) { Log("  could not delete the made island " + name + ": " + e.Message); }
		}

		/// <summary>Between cases: the hidden objects Generate keeps for undo go, and the undo history with them.</summary>
		static void GenSweepTidy()
		{
			try
			{
				if (GeneratorWindow.IsOpen) GeneratorWindow.Close();
				GameObject placed = GameObject.Find("PlacedObjects");
				if (placed != null)
				{
					List<EditorGameObject> hidden = placed.GetComponentsInChildren<EditorGameObject>(true).Where(x => !x.gameObject.activeSelf).ToList();
					foreach (EditorGameObject h in hidden) UnityEngine.Object.Destroy(h.gameObject);
					List<Transform> empty = placed.transform.Cast<Transform>().Where(x => x.name == "GeneratedObjects" && x.GetComponentsInChildren<EditorGameObject>(true).All(y => !y.gameObject.activeSelf)).ToList();
					foreach (Transform holder in empty) UnityEngine.Object.Destroy(holder.gameObject);
				}
				CommandUndoRedo.UndoRedoManager.Clear();
			}
			catch (Exception e) { Log("  tidying up after a case: " + e.Message); }
		}

		#endregion
	}
}
