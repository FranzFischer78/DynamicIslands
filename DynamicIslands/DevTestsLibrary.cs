using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DynamicIslands.Editor;
using HMLLibrary;
using ICSharpCode.SharpZipLib.Zip;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of sharing islands and plans as packs (LibraryPack, the Share and Import windows) and of a world keeping its
	/// own copy of its plan (WorldDirector) - libexport.ps1, TEST_CATALOGUE G41-G44.
	/// </summary>
	public static partial class DevTests
	{
		const string LibA = "cilib-a", LibB = "cilib-b", LibC = "cilib-c", LibPlan = "CI Lib Plan", LibPlanCopy = "CI Copy Plan";

		/// <summary>A small island of its own (heights from seed) that brings the islands named, one rule each.</summary>
		static void MakeLibIsland(string name, int seed, params string[] brings)
		{
			var f = new IslandFile { Name = name, TerrainSize = new Vector3(120, 40, 120), HeightmapResolution = 33, Heights = new float[33, 33] };
			var rnd = new System.Random(seed);
			for (int y = 0; y < 33; y++) for (int x = 0; x < 33; x++) f.Heights[y, x] = (float)(0.3 + 0.2 * rnd.NextDouble());
			var rules = brings.Select((b, i) => new IntroRule { Id = "r" + i, What = "island", WhatArg = b, When = "visit", WhenRef = IntroRule.Self, Where = "near", WhereRef = IntroRule.Self, Distance = 600, Direction = "north" }).ToList();
			WorldDirector.SetRulesInProps(f.Props, rules);
			f.Props["quest.title"] = "Test " + name;
			f.Save(IslandSpawner.PathFor(name));
		}

		static void DeleteLib(params string[] names)
		{
			foreach (string n in names) { string p = IslandSpawner.PathFor(n); if (File.Exists(p)) File.Delete(p); }
		}

		static void CleanLibTests()
		{
			foreach (string n in IslandSpawner.ListSavedIslands().Where(n => n.StartsWith("cilib-", StringComparison.OrdinalIgnoreCase)).ToList()) DeleteLib(n);
			foreach (string p in WorldPlan.All().Where(n => n.StartsWith(LibPlan, StringComparison.OrdinalIgnoreCase) || n == LibPlanCopy).ToList())
				if (File.Exists(WorldPlan.PathFor(p))) File.Delete(WorldPlan.PathFor(p));
			// (only the tests' own entries leave installed.json; the player's stay)
			List<LibraryInstalled> installed = LibraryPack.Installed();
			if (installed.RemoveAll(e => e.id.StartsWith("ci-lib") || e.id.StartsWith("cilib")) > 0)
				LibraryPack.SaveInstalled(installed);
			string fake = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), "cilib-fake-world.txt");
			if (File.Exists(fake)) File.Delete(fake);
			foreach (string z in new[] { Path.Combine(LibraryPack.ExportFolder, "ci-lib-pack.zip"), Path.Combine(LibraryPack.ImportFolder, "ci-lib-pack.zip") }) if (File.Exists(z)) File.Delete(z);
			string last = Path.Combine(LibraryPack.ExportFolder, "exports.json");
			if (File.Exists(last) && File.ReadAllText(last).Contains("ci-lib")) File.Delete(last);
			if (File.Exists(LibraryPack.UpdatedLog)) File.WriteAllLines(LibraryPack.UpdatedLog, File.ReadAllLines(LibraryPack.UpdatedLog).Where(l => !l.StartsWith("cilib-")).ToArray());
			IslandCache.Forget();
		}

		static void WriteTestZip(string path, IEnumerable<KeyValuePair<string, byte[]>> files)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			using (var zip = new ZipOutputStream(File.Create(path)))
			{
				foreach (var f in files) { zip.PutNextEntry(new ZipEntry(f.Key) { Size = f.Value.Length }); zip.Write(f.Value, 0, f.Value.Length); zip.CloseEntry(); }
				zip.Finish();
			}
		}

		static KeyValuePair<string, byte[]> Entry(string name, string text) { return new KeyValuePair<string, byte[]>(name, Encoding.UTF8.GetBytes(text)); }
		static KeyValuePair<string, byte[]> Entry(string name, byte[] b) { return new KeyValuePair<string, byte[]>(name, b); }

		[ConsoleCommand(name: "CILibraryUnit", docs: "Dev, anywhere: the pack rules without the game - versions compared as numbers, ids from titles, safe file names, and broken or harmful packs refused with a reason and nothing written (a cut-off zip, a file outside its folder, 70 files, a zip bomb, a program, no info.json, a newer island format, an island with a damaged body, an info.json nested too deep, a plan naming an island it doesn't hold)")]
		public static void LibraryUnitCommand()
		{
			bool ok = true;
			Check(ref ok, LibraryPack.CompareVersions("3.10", "3.9") > 0 && LibraryPack.CompareVersions("3.0", "3") == 0 && LibraryPack.CompareVersions("", "0.1") < 0 && LibraryPack.CompareVersions("3.2", "3.10") < 0, "versions compared as numbers (3.10 > 3.9, 3.0 = 3)");
			Check(ref ok, LibraryPack.IdFrom("First Voyage!") == "first-voyage" && LibraryPack.IdFrom("  Åland  Isles ") == "land-isles" && LibraryPack.IdFrom("???") == "entry", "ids from titles: " + LibraryPack.IdFrom("First Voyage!") + ", " + LibraryPack.IdFrom("  Åland  Isles "));
			Check(ref ok, LibraryPack.IsSafeFileName("Palm Cove.island") && LibraryPack.IsSafeFileName("Åkerö.island") && !LibraryPack.IsSafeFileName("..\\x.island") && !LibraryPack.IsSafeFileName("a#b.island") &&
				!LibraryPack.IsSafeFileName("50%.island") && !LibraryPack.IsSafeFileName("CON.island") && !LibraryPack.IsSafeFileName(".hidden") && !LibraryPack.IsSafeFileName("a/b.island"), "safe file names (spaces and å/ä/ö yes; .., #, %, /, CON no)");

			string dir = Path.Combine(LibraryPack.LibraryFolder, "citest");
			Directory.CreateDirectory(dir);
			// (a real island: a pack's islands are read in full - AU39)
			string realPath = Path.Combine(dir, "real" + IslandFile.Extension);
			new IslandFile { Name = "x", TerrainSize = new Vector3(10, 10, 10), HeightmapResolution = 33, Heights = new float[33, 33] }.Save(realPath);
			byte[] island = File.ReadAllBytes(realPath);
			File.Delete(realPath);
			byte[] damaged = Encoding.ASCII.GetBytes("CISL").Concat(BitConverter.GetBytes(4)).Concat(new byte[40]).ToArray();
			byte[] newer = Encoding.ASCII.GetBytes("CISL").Concat(BitConverter.GetBytes(99)).Concat(new byte[40]).ToArray();
			string info = "{\"id\":\"ci-bad\",\"kind\":\"island\",\"title\":\"Bad\",\"author\":\"x\"}";
			var cases = new List<KeyValuePair<string, Action<string>>>
			{
				new KeyValuePair<string, Action<string>>("a cut-off zip", p => { WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("ci-bad/x.island", island) }); byte[] b = File.ReadAllBytes(p); File.WriteAllBytes(p, b.Take(b.Length / 2).ToArray()); }),
				new KeyValuePair<string, Action<string>>("a file outside its folder", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("../../evil.island", island) })),
				new KeyValuePair<string, Action<string>>("70 files", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info) }.Concat(Enumerable.Range(0, 70).Select(i => Entry("ci-bad/x" + i + ".island", island))))),
				new KeyValuePair<string, Action<string>>("a zip bomb (60 MB of zeros)", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("ci-bad/big.island", new byte[60 * 1024 * 1024]) })),
				new KeyValuePair<string, Action<string>>("a program", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("ci-bad/x.island", island), Entry("ci-bad/run.exe", new byte[10]) })),
				new KeyValuePair<string, Action<string>>("no info.json", p => WriteTestZip(p, new[] { Entry("ci-bad/x.island", island) })),
				new KeyValuePair<string, Action<string>>("a newer island format", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("ci-bad/x.island", newer) })),
				new KeyValuePair<string, Action<string>>("not an island file", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("ci-bad/x.island", "hello") })),
				new KeyValuePair<string, Action<string>>("an island with a damaged body", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("ci-bad/x.island", damaged) })),
				new KeyValuePair<string, Action<string>>("an info.json nested 100,000 deep", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", "{\"id\":\"ci-bad\",\"tags\":" + new string('[', 100000) + "}"), Entry("ci-bad/x.island", island) })),
				new KeyValuePair<string, Action<string>>("a plan naming an island it doesn't hold", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", "{\"id\":\"ci-bad\",\"kind\":\"plan\",\"title\":\"Bad\",\"author\":\"x\",\"plan\":\"P.plan\"}"), Entry("ci-bad/x.island", island), Entry("ci-bad/P.plan", "rule = a | island:y | start | ahead:300 | | ") })),
				new KeyValuePair<string, Action<string>>("two entries' folders", p => WriteTestZip(p, new[] { Entry("ci-bad/info.json", info), Entry("other/x.island", island) })),
			};
			var wrong = new List<string>();
			var reasons = new List<string>();
			int before = IslandSpawner.ListSavedIslands().Count();
			foreach (var c in cases)
			{
				string p = Path.Combine(dir, "bad.zip");
				c.Value(p);
				string error;
				LibraryPackContents pack = LibraryPack.Read(p, out error);
				if (pack != null || string.IsNullOrEmpty(error)) wrong.Add(c.Key); else reasons.Add(c.Key + ": " + error);
				File.Delete(p);
			}
			foreach (string r in reasons) Log("  refused " + r);
			Check(ref ok, wrong.Count == 0, "every broken or harmful pack is refused with a reason" + (wrong.Count > 0 ? " - not: " + string.Join(", ", wrong.ToArray()) : " (" + cases.Count + ")"));
			Check(ref ok, IslandSpawner.ListSavedIslands().Count() == before && !File.Exists(Path.Combine(DynamicIslands.assetpath, "evil.island")), "nothing was written");
			// A good pack reads (the file names may hold spaces and å/ä/ö; one folder)
			string good = Path.Combine(dir, "good.zip");
			WriteTestZip(good, new[] { Entry("ci-good/info.json", "{\"id\":\"ci-good\",\"kind\":\"island\",\"title\":\"Good\",\"author\":\"x\",\"minModVersion\":\"99.0\"}"), Entry("ci-good/Åkerö hamn.island", island) });
			string e2;
			LibraryPackContents g = LibraryPack.Read(good, out e2);
			Check(ref ok, g != null && g.IslandNames.Contains("Åkerö hamn") && g.Newer, "a good pack reads (" + (g != null ? string.Join(", ", g.IslandNames.ToArray()) : e2) + "), and knows it was made with a newer mod (Q5: warn and let the player choose)");
			File.Delete(good);
			Directory.Delete(dir, true);
			if (ok) Log("PASS: library unit"); else Fail("library unit");
		}

		[ConsoleCommand(name: "CILibraryRoundTrip", docs: "Dev, main menu or editor: a plan whose islands bring islands (a -> b -> c) exported as a pack (everything it needs, a missing island stops it, the next export is version 2 of the same entry), installed where its files aren't (the same bytes back, the plan, never while sailing), where a different 'cilib-b' is the player's (installed as 'cilib-b (CI Lib Pack)', a's rule follows, the player's untouched), updated while a saved world uses an island (that world keeps its version), a no-remix island refused, removed (keeping what a world uses); SL11: a storyline plan (tab titles, colours, intros, next-when, ending) is equal after export, remove and import; cleans up")]
		public static void LibraryRoundTripCommand()
		{
			bool ok = true;
			CleanLibTests();
			try
			{
				// A plan: its rule brings a, a brings b, b brings c
				MakeLibIsland(LibC, 3);
				MakeLibIsland(LibB, 2, LibC);
				MakeLibIsland(LibA, 1, LibB);
				var plan = new WorldPlan { Name = LibPlan, Description = "a test plan", Random = false };
				plan.Rules.Add(new IntroRule { Id = "start", What = "island", WhatArg = LibA, When = "start", Where = "ahead", Distance = 300 });
				plan.Save();
				List<string> missing;
				List<string> got = LibraryPack.Collect(new string[0], WorldPlan.Load(LibPlan), out missing);
				Check(ref ok, got.Count == 3 && missing.Count == 0 && new[] { LibA, LibB, LibC }.All(got.Contains), "the plan needs a, b and c (a brings b, b brings c): " + string.Join(", ", got.ToArray()));
				got = LibraryPack.Collect(new[] { LibB }, null, out missing);
				Check(ref ok, got.Count == 2 && got.Contains(LibC), "an island alone takes the islands it brings along: " + string.Join(", ", got.ToArray()));

				// A missing island stops the export
				var info = new LibraryInfo { title = "CI Lib Pack", id = "ci-lib-pack", author = "CI Tester", summary = "test", remix = true };
				byte[] c = File.ReadAllBytes(IslandSpawner.PathFor(LibC));
				DeleteLib(LibC);
				string error;
				Check(ref ok, LibraryPack.Export(info, null, WorldPlan.Load(LibPlan), null, null, out error) == null && error.Contains(LibC), "a missing island stops the export: " + error);
				File.WriteAllBytes(IslandSpawner.PathFor(LibC), c);

				// The export
				var shas = new[] { LibA, LibB, LibC }.ToDictionary(n => n, n => LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(n))));
				string zip = LibraryPack.Export(info, null, WorldPlan.Load(LibPlan), new byte[] { 1, 2, 3 }, null, out error);
				Check(ref ok, zip != null && File.Exists(zip), "exported: " + (zip ?? error));
				LibraryPackContents pack = zip != null ? LibraryPack.Read(zip, out error) : null;
				Check(ref ok, pack != null && pack.Info.IsPlan && pack.IslandNames.Count() == 3 && pack.Files.ContainsKey(LibPlan + ".plan") && pack.Files.ContainsKey("icon.jpg") && pack.Info.minModVersion == LibraryPack.ModVersion,
					"the pack: " + (pack != null ? string.Join(", ", pack.Files.Keys.ToArray()) : error));
				LibraryInfo next = LibraryPack.LastInfo(LibraryPack.KindPlan, LibPlan);
				Check(ref ok, next.id == "ci-lib-pack" && next.version == 2 && next.author == "CI Tester", "the next export of the plan is version 2 of the same entry (" + next.id + ", " + next.version + ")");
				if (pack == null) { Fail("library round trip"); return; }

				// Installed on a PC without these files: the same bytes back, the plan, the islands never while sailing
				DeleteLib(LibA, LibB, LibC);
				File.Delete(WorldPlan.PathFor(LibPlan));
				LibraryPack.Report r = LibraryPack.Install(pack, true, false, LibraryPack.SourceImport);
				Log("  " + r.ToString().Replace("\n", " / "));
				Check(ref ok, new[] { LibA, LibB, LibC }.All(n => File.Exists(IslandSpawner.PathFor(n)) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(n))) == shas[n]), "installed: a, b and c exactly as exported");
				Check(ref ok, WorldPlan.Load(LibPlan) != null && WorldPlan.Load(LibPlan).Rules.Count == 1, "the plan is installed");
				CustomIslandSpawner.LoadPool(true);
				Check(ref ok, !WorldIslands.Candidates().Any(n => n.StartsWith("cilib-")), "a plan's islands never turn up by chance while sailing (weight 0)");
				Check(ref ok, LibrarySource.OfPlan(LibPlan) == "import:ci-lib-pack@1", "the plan knows where it came from: " + LibrarySource.OfPlan(LibPlan));
				LibraryPack.Remove("ci-lib-pack");
				Check(ref ok, !new[] { LibA, LibB, LibC }.Any(n => File.Exists(IslandSpawner.PathFor(n))) && !File.Exists(WorldPlan.PathFor(LibPlan)) && !LibraryPack.Installed().Any(e => e.id == "ci-lib-pack"), "Remove takes everything it installed away");

				// A clash: the player has a different cilib-b (and a different plan of the same name)
				MakeLibIsland(LibB, 77);
				string mine = LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB)));
				File.WriteAllText(WorldPlan.PathFor(LibPlan), "description = the player's own\nrandom = on\n");
				r = LibraryPack.Install(pack, false, false, LibraryPack.SourceImport);
				Log("  " + r.ToString().Replace("\n", " / "));
				string renamed = LibB + " (CI Lib Pack)";
				Check(ref ok, File.Exists(IslandSpawner.PathFor(renamed)) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB))) == mine, "the pack's cilib-b is installed as '" + renamed + "'; the player's own is untouched");
				List<IntroRule> aRules = WorldDirector.RulesFromProps(IslandFile.Load(IslandSpawner.PathFor(LibA)).Props);
				Check(ref ok, aRules.Count == 1 && aRules[0].WhatArg == renamed, "a's rule now brings '" + (aRules.Count > 0 ? aRules[0].WhatArg : "?") + "'");
				string planName = r.PlanName;
				Check(ref ok, planName == LibPlan + " (CI Tester)" && WorldPlan.Load(LibPlan).Description == "the player's own", "the pack's plan is installed as '" + planName + "'; the player's own plan is untouched");

				// An update while a saved world uses cilib-a: the ground changed (the objects keep their order) - that world gets
				// it too, as with an editor save; then an update that removes an object - that world keeps its version
				string hashA = IslandNetwork.HashOf(LibA);
				string fake = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), "cilib-fake-world.txt");
				Directory.CreateDirectory(Path.GetDirectoryName(fake));
				File.WriteAllLines(fake, new[] { "# Custom islands in world 'CI Lib World': name|...", "@plan=" + planName, LibA + "|0|0|0||start||" + hashA });
				var info2 = new LibraryInfo { title = "CI Lib Pack", id = "ci-lib-pack", author = "CI Tester", version = 2 };
				// (version 2: cilib-a changed, from the pack's own bytes)
				string pack2 = Path.Combine(LibraryPack.LibraryFolder, "ci-lib-pack2.zip");
				string tmpA = Path.Combine(LibraryPack.LibraryFolder, "a2.tmp");
				File.WriteAllBytes(tmpA, pack.Files[LibA + ".island"]);
				IslandFile a2 = IslandFile.Load(tmpA);
				a2.Heights[3, 3] = 0.9f;
				// (two objects added at the end: the order of what was there is kept, so saved worlds get it)
				a2.Objects.Add(new IslandObject { Name = "Placeable_Rock_Small", Position = new Vector3(10f, 5f, 10f) });
				a2.Objects.Add(new IslandObject { Name = "Placeable_Rock_Small", Position = new Vector3(12f, 5f, 10f) });
				a2.Save(tmpA);
				WriteTestZip(pack2, new[] { Entry("ci-lib-pack/info.json", new LibraryInfo { id = "ci-lib-pack", kind = "plan", title = "CI Lib Pack", author = "CI Tester", version = 2, plan = LibPlan + ".plan" }.ToJson()),
					Entry("ci-lib-pack/" + LibA + ".island", File.ReadAllBytes(tmpA)), Entry("ci-lib-pack/" + LibB + ".island", pack.Files[LibB + ".island"]), Entry("ci-lib-pack/" + LibC + ".island", pack.Files[LibC + ".island"]),
					Entry("ci-lib-pack/" + LibPlan + ".plan", pack.Files[LibPlan + ".plan"]) });
				File.Delete(tmpA);
				LibraryPackContents p2 = LibraryPack.Read(pack2, out error);
				r = LibraryPack.Install(p2, false, false, LibraryPack.SourceImport);
				Log("  " + r.ToString().Replace("\n", " / "));
				Check(ref ok, !File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(LibA, hashA))) && IslandNetwork.HashOf(LibA) != hashA && r.ToString().Contains("the new '" + LibA + "'"),
					"updated, only the ground changed: the world that uses cilib-a gets the new version too (no copy kept, the report says so)");
				File.Delete(pack2);

				// Version 3: an object removed - the saved world keeps the version it started with (version 2)
				string hashA2 = IslandNetwork.HashOf(LibA);
				File.WriteAllLines(fake, new[] { "# Custom islands in world 'CI Lib World': name|...", "@plan=" + planName, LibA + "|0|0|0||start||" + hashA2 });
				string pack3 = Path.Combine(LibraryPack.LibraryFolder, "ci-lib-pack3.zip");
				IslandFile a3 = IslandFile.Load(IslandSpawner.PathFor(LibA));
				int objects3 = a3.Objects.Count;
				if (objects3 > 0) a3.Objects.RemoveAt(0);
				a3.Save(tmpA);
				WriteTestZip(pack3, new[] { Entry("ci-lib-pack/info.json", new LibraryInfo { id = "ci-lib-pack", kind = "plan", title = "CI Lib Pack", author = "CI Tester", version = 3, plan = LibPlan + ".plan" }.ToJson()),
					Entry("ci-lib-pack/" + LibA + ".island", File.ReadAllBytes(tmpA)), Entry("ci-lib-pack/" + LibB + ".island", pack.Files[LibB + ".island"]), Entry("ci-lib-pack/" + LibC + ".island", pack.Files[LibC + ".island"]),
					Entry("ci-lib-pack/" + LibPlan + ".plan", pack.Files[LibPlan + ".plan"]) });
				File.Delete(tmpA);
				LibraryPackContents p3 = LibraryPack.Read(pack3, out error);
				r = LibraryPack.Install(p3, false, false, LibraryPack.SourceImport);
				Log("  " + r.ToString().Replace("\n", " / "));
				Check(ref ok, objects3 > 0 && File.Exists(IslandSpawner.PathFor(IslandNetwork.DownloadName(LibA, hashA2))) && IslandNetwork.HashOf(LibA) != hashA2,
					"updated, an object removed: cilib-a is the new version, and the world that uses it keeps the one it started with (" + IslandNetwork.DownloadName(LibA, hashA2) + ")");
				Check(ref ok, WorldCopy.LocalFileFor(LibA, hashA2) == IslandNetwork.DownloadName(LibA, hashA2), "that world's island line finds its version: " + WorldCopy.LocalFileFor(LibA, hashA2));
				File.Delete(pack3);

				// No remixes: an island from an entry that says so can't be exported by someone else
				List<LibraryInstalled> all = LibraryPack.Installed();
				all.First(e => e.id == "ci-lib-pack").remix = false;
				LibraryPack.SaveInstalled(all);
				string refused = LibraryPack.Export(new LibraryInfo { title = "Stolen", author = "Someone Else" }, LibA, null, null, null, out error);
				Check(ref ok, refused == null && error.Contains("no remixes"), "a no-remix island is refused: " + error);
				Check(ref ok, LibraryPack.LastInfo(LibraryPack.KindIsland, LibA).basedOn == "CI Lib Pack by CI Tester", "an island from an entry is credited ('based on CI Lib Pack by CI Tester')");

				// Remove keeps what the saved world uses
				r = LibraryPack.Remove("ci-lib-pack");
				Log("  " + r.ToString().Replace("\n", " / "));
				Check(ref ok, File.Exists(IslandSpawner.PathFor(LibA)) && File.Exists(WorldPlan.PathFor(planName)) && !File.Exists(IslandSpawner.PathFor(LibC)) && r.ToString().Contains("CI Lib World"),
					"Remove keeps cilib-a and the plan (the world 'CI Lib World' uses them - it has no copy of the plan), removes the rest");
				Check(ref ok, File.Exists(IslandSpawner.PathFor(LibB)) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(LibB))) == mine, "the player's own cilib-b is still there, untouched");

				// SL11: a storyline shared - tab titles, colours, intros, a next-when note/step and an ending survive export -> remove -> import
				{
					const string nbIsland = "cilib-nb", nbPlan = "CI Lib Plan NB";
					MakeLibIsland(nbIsland, 5);
					var nbSource = new WorldPlan { Name = nbPlan, Description = "a storyline", Random = false, StoryEnding = "The end.\nThanks, åäö" };
					nbSource.Rules.Add(new IntroRule { Id = "one", What = "island", WhatArg = nbIsland, When = "start", Where = "receiver", Distance = 300, StoryPlace = "first", StoryDone = "step:2",
						TabTitle = "The Beginning", TabColour = 4, TabIntro = "Welcome, åäö\nSecond line" });
					nbSource.Rules.Add(new IntroRule { Id = "two", What = "island", WhatArg = nbIsland, When = "rule", WhenRef = "one", Where = "receiver", Distance = 300, StoryPlace = "after:one", StoryDone = "note:1",
						TabTitle = "Part two", TabColour = 6, TabIntro = "Next up" });
					nbSource.Save();
					WorldPlan nbSaved = WorldPlan.Load(nbPlan);
					string nbError;
					string nbZip = LibraryPack.Export(new LibraryInfo { title = "CI Lib NB", id = "ci-lib-nb", author = "CI Tester", summary = "test", remix = true }, null, nbSaved, null, null, out nbError);
					Check(ref ok, nbZip != null && File.Exists(nbZip), "SL11: a plan with tabs, colours, intros, next-when and an ending exported: " + (nbZip ?? nbError));
					LibraryPackContents nbPack = nbZip != null ? LibraryPack.Read(nbZip, out nbError) : null;
					if (nbPack != null)
					{
						DeleteLib(nbIsland);
						File.Delete(WorldPlan.PathFor(nbPlan));
						LibraryPack.Report nbReport = LibraryPack.Install(nbPack, true, false, LibraryPack.SourceImport);
						WorldPlan nbBack = WorldPlan.Load(nbReport.PlanName ?? nbPlan);
						Check(ref ok, nbBack != null && nbBack.Rules.Count == 2, "SL11: imported again (" + (nbBack != null ? nbBack.Rules.Count + " rules" : "no plan") + ")");
						if (nbBack != null && nbBack.Rules.Count == 2)
						{
							for (int i = 0; i < 2; i++)
							{
								IntroRule a = nbSaved.Rules[i], b = nbBack.Rules[i];
								Check(ref ok, a.TabTitle == b.TabTitle && a.TabColour == b.TabColour && a.TabIntro == b.TabIntro && a.StoryPlace == b.StoryPlace && a.StoryDone == b.StoryDone && a.ToLine() == b.ToLine(),
									"SL11: rule '" + a.Id + "' equal after the round trip: tab '" + b.TabTitle + "', colour " + b.TabColour + ", intro, place '" + b.StoryPlace + "', next-when '" + b.StoryDone + "'");
							}
							Check(ref ok, nbBack.StoryEnding == nbSaved.StoryEnding && nbBack.Description == nbSaved.Description, "SL11: the storyending and description are equal after the round trip ('" + nbBack.StoryEnding.Replace("\n", "\\n") + "')");
							Check(ref ok, string.Join("\n", nbBack.Rules.Select(x => x.ToLine()).ToArray()) == string.Join("\n", nbSaved.Rules.Select(x => x.ToLine()).ToArray()), "SL11: the plan's rules and the notebook's lines are the same as the source's");
						}
						LibraryPack.Remove("ci-lib-nb");
					}
					if (nbZip != null && File.Exists(nbZip)) File.Delete(nbZip);
				}
			}
			catch (Exception e) { Check(ref ok, false, "no exception: " + e); }
			finally
			{
				foreach (string n in IslandSpawner.ListSavedIslands().Where(n => n.StartsWith(LibA + "_")).ToList()) DeleteLib(n);
				CleanLibTests();

				RemovePoolTestLines();
			}
			if (ok) Log("PASS: library round trip"); else Fail("library round trip");
		}


		[ConsoleCommand(name: "CILibUpdateChoice", docs: "Dev, main menu or editor: T10 - an install records its files' SHA-256, so a library file changed without a new version shows as Update; an update over the player's changed island keeps theirs as '<name> (yours)' (out of the random pool), Keep my changes leaves it; updated islands are noted for the worlds that have them")]
		public static void LibUpdateChoiceCommand()
		{
			bool ok = true;
			CleanLibTests();
			const string id = "ci-lib-upd", u = "cilib-u", yours = "cilib-u (yours)";
			string tmp = Path.Combine(LibraryPack.LibraryFolder, "ci-lib-upd.tmp.zip");
			try
			{
				Directory.CreateDirectory(LibraryPack.LibraryFolder);
				Func<int, int, LibraryPackContents> packOf = (version, seed) =>
				{
					MakeLibIsland(u, seed);
					byte[] b = File.ReadAllBytes(IslandSpawner.PathFor(u));
					DeleteLib(u);
					WriteTestZip(tmp, new[] { Entry(id + "/info.json", new LibraryInfo { id = id, kind = "island", title = "CI Lib Upd", author = "CI Tester", version = version }.ToJson()), Entry(id + "/" + u + ".island", b) });
					string err;
					LibraryPackContents pc = LibraryPack.Read(tmp, out err);
					File.Delete(tmp);
					if (pc == null) throw new Exception("pack: " + err);
					return pc;
				};
				Func<int, string, LibraryEntry> entryOf = (version, sha) =>
				{
					var e = new LibraryEntry { Info = new LibraryInfo { id = id, kind = "island", title = "CI Lib Upd", version = version } };
					e.Files.Add(new LibraryFileRef { Name = u + ".island", Sha256 = sha });
					e.Files.Add(new LibraryFileRef { Name = "icon.jpg", Sha256 = "00" });
					return e;
				};

				// Version 1 installed: the pack's files are recorded
				LibraryPackContents p1 = packOf(1, 11);
				string sha1 = LibraryPack.Sha256(p1.Files[u + ".island"]);
				long before = DateTime.UtcNow.Ticks;
				LibraryPack.Install(p1, false, false, LibraryPack.SourceLibrary);
				LibraryInstalled inst = LibraryPack.Installed().FirstOrDefault(x => x.id == id);
				string rec;
				Check(ref ok, inst != null && inst.packed.TryGetValue(u + ".island", out rec) && rec == sha1, "installed.json records the pack's island file and its SHA-256");
				Check(ref ok, LibraryClient.StateOf(entryOf(1, sha1)) == LibraryClient.State.Installed, "the library lists the same file: Installed (a picture's change doesn't count)");
				Check(ref ok, LibraryClient.StateOf(entryOf(1, "ab" + sha1.Substring(2))) == LibraryClient.State.Update, "the library's file changed, same version number: Update");
				Check(ref ok, LibraryClient.StateOf(entryOf(2, sha1)) == LibraryClient.State.Update, "a higher version: Update");

				// The player changes the island; version 2 replaces it - theirs is kept as a copy, out of the random pool
				MakeLibIsland(u, 99);
				string mine = LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(u)));
				LibraryPackContents p2 = packOf(2, 22);
				string sha2 = LibraryPack.Sha256(p2.Files[u + ".island"]);
				MakeLibIsland(u, 99);
				LibraryPack.Report r = LibraryPack.Install(p2, true, true, LibraryPack.SourceLibrary);
				Log("  " + r.ToString().Replace("\n", " / "));
				Check(ref ok, LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(u))) == sha2, "Update, keep a copy: the new version is installed");
				Check(ref ok, File.Exists(IslandSpawner.PathFor(yours)) && LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(yours))) == mine && r.ToString().Contains("kept as '" + yours + "'"),
					"the player's version is kept as '" + yours + "', and the report says so");
				CustomIslandSpawner.LoadPool(true);
				Check(ref ok, !WorldIslands.Candidates().Contains(yours), "the copy never turns up by chance while sailing");
				Check(ref ok, LibraryPack.UpdatedSince(before).ContainsKey(u), "the update is noted for the worlds that have the island");
				Check(ref ok, LibraryClient.StateOf(entryOf(2, sha2)) == LibraryClient.State.Installed, "after the update the same listing is Installed");

				// Changed again; version 3 with Keep my changes: theirs stays, no copy
				DeleteLib(yours);
				MakeLibIsland(u, 98);
				mine = LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(u)));
				LibraryPackContents p3 = packOf(3, 33);
				MakeLibIsland(u, 98);
				r = LibraryPack.Install(p3, true, false, LibraryPack.SourceLibrary);
				Log("  " + r.ToString().Replace("\n", " / "));
				Check(ref ok, LibraryPack.Sha256(File.ReadAllBytes(IslandSpawner.PathFor(u))) == mine && !File.Exists(IslandSpawner.PathFor(yours)) && r.ToString().Contains("Kept your changed"),
					"Keep my changes: the player's island stays as it is, no copy");
			}
			catch (Exception e) { Check(ref ok, false, "no exception: " + e); }
			finally
			{
				if (File.Exists(tmp)) File.Delete(tmp);
				try { LibraryPack.Remove(id); } catch { }
				CleanLibTests();
				RemovePoolTestLines();
			}
			if (ok) Log("PASS: library update choice"); else Fail("library update choice");
		}

		static void RemovePoolTestLines()
		{
			string path = Path.Combine(DynamicIslands.assetpath, CustomIslandSpawner.PoolFileName);
			if (!File.Exists(path)) return;
			var lines = File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("cilib-")).ToArray();
			File.WriteAllLines(path, lines);
			CustomIslandSpawner.LoadPool(true);
		}

		[ConsoleCommand(name: "CILibraryWindows", docs: "Dev, editor: the Share and Import windows clicked as a player does - the Islands window's Export... opens Share for the picked island (what goes along, the picture of the editor's view, the author), Export writes the pack; the pack in the import folder is picked, shows what it holds, installs; the Installed list has it, Remove asks first and removes it; both windows fit the screen. Cleans up")]
		public static void LibraryWindowsCommand() { StartTest(LibraryWindowsRoutine()); }

		static IEnumerator LibraryWindowsRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			CleanLibTests();
			MakeLibIsland(LibC, 3);
			MakeLibIsland(LibB, 2, LibC);
			IslandFilesWindow.Open();
			yield return null;
			GameObject files = GameObject.Find("IslandFilesWindow");
			InputField nameField = files != null ? files.GetComponentsInChildren<InputField>(true).FirstOrDefault() : null;
			if (nameField != null) nameField.text = LibB;
			Check(ref ok, files != null && Click(files, "Export..."), "the Islands window has Export...");
			yield return null;
			Check(ref ok, LibraryExportWindow.IsOpen, "it opens the Share window");
			Text includes = LibraryExportWindow.Window.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Includes");
			Check(ref ok, includes != null && includes.text.Contains(LibB) && includes.text.Contains(LibC), "it says what goes along: " + (includes != null ? includes.text : "?"));
			RawImage pic = LibraryExportWindow.Window.GetComponentsInChildren<RawImage>(true).FirstOrDefault(i => i.name == "Preview");
			Check(ref ok, pic != null && pic.texture != null && pic.texture.width == 1280, "a picture of the editor's view is taken (" + (pic != null && pic.texture != null ? pic.texture.width + "x" + pic.texture.height : "none") + ")");
			Check(ref ok, TypeInto(LibraryExportWindow.Window.gameObject, "e.g. First Voyage", "CI Lib Pack") && TypeInto(LibraryExportWindow.Window.gameObject, "your name", "CI Tester") && TypeInto(LibraryExportWindow.Window.gameObject, "one line for the list", "a test"), "title, author and summary typed");
			Screenshot(new[] { "library_share" });
			yield return new WaitForSecondsRealtime(0.6f);
			var off = OffScreen(LibraryExportWindow.Window.gameObject, Screen.width, Screen.height);
			Check(ref ok, off.Count == 0, "the Share window fits the screen" + (off.Count > 0 ? " - off: " + string.Join(", ", off.Take(4).ToArray()) : ""));
			Check(ref ok, Click(LibraryExportWindow.Window.gameObject, "Export"), "Export clicked");
			yield return null;
			string zip = LibraryExportWindow.LastZip;
			Check(ref ok, zip != null && File.Exists(zip) && Path.GetFileName(zip) == "ci-lib-pack.zip", "the pack is written: " + (zip ?? LibraryExportWindow.Status));
			LibraryExportWindow.Close();

			// Import it where the islands aren't
			Directory.CreateDirectory(LibraryPack.ImportFolder);
			if (zip != null) File.Copy(zip, Path.Combine(LibraryPack.ImportFolder, "ci-lib-pack.zip"), true);
			DeleteLib(LibB, LibC);
			LibraryImportWindow.Open();
			yield return null;
			Check(ref ok, LibraryImportWindow.IsOpen && Click(LibraryImportWindow.Window.gameObject, "ci-lib-pack.zip"), "the Import window lists the pack; picked");
			yield return null;
			Check(ref ok, LibraryImportWindow.Detail.Contains("CI Lib Pack") && LibraryImportWindow.Detail.Contains(LibC), "it shows what the pack holds: " + LibraryImportWindow.Detail.Replace("\n", " / "));
			Check(ref ok, Click(LibraryImportWindow.Window.gameObject, "Install"), "Install clicked");
			yield return null;
			Check(ref ok, File.Exists(IslandSpawner.PathFor(LibB)) && File.Exists(IslandSpawner.PathFor(LibC)) && LibraryImportWindow.Status.Contains("Installed"), "installed: " + LibraryImportWindow.Status.Replace("\n", " / "));
			Transform row = LibraryImportWindow.Window.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Installed_ci-lib-pack");
			Check(ref ok, row != null, "the Installed list has it");
			Screenshot(new[] { "library_import" });
			yield return new WaitForSecondsRealtime(0.6f);
			off = OffScreen(LibraryImportWindow.Window.gameObject, Screen.width, Screen.height);
			Check(ref ok, off.Count == 0, "the Import window fits the screen" + (off.Count > 0 ? " - off: " + string.Join(", ", off.Take(4).ToArray()) : ""));
			LibraryImportWindow.AskRemove("ci-lib-pack");
			yield return null;
			Check(ref ok, File.Exists(IslandSpawner.PathFor(LibB)) && Click(LibraryImportWindow.Window.gameObject, "Sure? Remove"), "Remove asks first ('Sure? Remove')");
			yield return null;
			Check(ref ok, !File.Exists(IslandSpawner.PathFor(LibB)) && !LibraryPack.Installed().Any(e => e.id == "ci-lib-pack"), "then removes it");
			LibraryImportWindow.Close();
			CleanLibTests();
			RemovePoolTestLines();
			if (ok) Log("PASS: library windows"); else Fail("library windows");
		}

		[ConsoleCommand(name: "CIPlanCopyPrep", docs: "Dev, anywhere: CIPlanCopyPrep make = writes the test plan 'CI Copy Plan' (a sandbar at the start, a wreck after 50 km, and the island 'cilib-nothere', which doesn't exist); CIPlanCopyPrep delete = deletes the plan file")]
		public static void PlanCopyPrepCommand(string[] args)
		{
			if (args != null && args.Length > 0 && args[0] == "delete") { if (File.Exists(WorldPlan.PathFor(LibPlanCopy))) File.Delete(WorldPlan.PathFor(LibPlanCopy)); Log("Plan copy prep: deleted"); return; }
			File.WriteAllText(WorldPlan.PathFor(LibPlanCopy), "description = CI: a world keeps its plan\nrandom = off\n" +
				"rule = s | type:sandbar | start | ahead:300 | | Sandbar\nrule = w | type:wreck | km:50 | ahead:400 | | Wreck\nrule = gone | island:cilib-nothere | start | ahead:300 | | Gone\n");
			Log("Plan copy prep: made '" + LibPlanCopy + "'");
		}

		[ConsoleCommand(name: "CIPlanCopyCheck", docs: "Dev, world (host): the world keeps its own copy of its plan - its file has the plan's rules (@planrule), the director has them (from the world's copy when fromWorld is given), the wreck's rule is still to come, and the host was told the island 'cilib-nothere' is missing: CIPlanCopyCheck [fromWorld]")]
		public static void PlanCopyCheckCommand(string[] args)
		{
			bool fromWorld = args != null && args.Contains("fromWorld");
			bool ok = true;
			IslandWorldState.Save();
			string file = IslandWorldState.WorldFilePath;
			string[] lines = File.Exists(file) ? File.ReadAllLines(file) : new string[0];
			int ruleLines = lines.Count(l => l.StartsWith("@planrule="));
			Check(ref ok, WorldDirector.PlanName == LibPlanCopy && WorldDirector.Plan != null && WorldDirector.Plan.Rules.Count == 3, "the world's plan: " + WorldDirector.PlanName + " with " + (WorldDirector.Plan != null ? WorldDirector.Plan.Rules.Count : 0) + " rule(s)");
			Check(ref ok, ruleLines == 3 && lines.Any(l => l == "@planrandom=off"), "the world file keeps the plan: " + ruleLines + " @planrule line(s)");
			Check(ref ok, !fromWorld || (WorldDirector.PlanFromWorld && !File.Exists(WorldPlan.PathFor(LibPlanCopy))), "read from the world's own copy (the plan file is gone: " + !File.Exists(WorldPlan.PathFor(LibPlanCopy)) + ")");
			Check(ref ok, !WorldDirector.Done.Contains("w"), "the wreck's rule is still to come");
			Check(ref ok, WorldDirector.MissingIslands.Contains("cilib-nothere"), "the host was told the island 'cilib-nothere' is missing");
			Log("PLANCOPY " + WorldDirector.PlanName + " fromWorld=" + WorldDirector.PlanFromWorld + " done=" + string.Join(",", WorldDirector.Done.ToArray()));
			if (ok) Log("PASS: plan copy check"); else Fail("plan copy check");
		}
	}
}
