using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Two copies of one island counting their own pages and story items (StoryItems.cs, Quest.cs), and the object
	/// index made again in the main menu after a Raft update (PlaceableCatalog.cs). 2026-10-23.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIIslandCopiesCount", docs: "Dev, world (host, 'CI ...'): two copies of one island - a find-pages step and a collect step on each count that copy's own pages and story items (not the other copy's), finds from elsewhere count for both, and the finds' islands survive a save")]
		public static void IslandCopiesCountCommand(string[] args) { StartTest(IslandCopiesCountRoutine()); }

		static IEnumerator IslandCopiesCountRoutine()
		{
			yield return null;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("island copies: needs a world, as the host"); yield break; }
			bool ok = true;
			const string name = "cicopytest", gem = "cicopygem";
			// (all in one frame: two list entries without a file or a root, gone again before the spawner looks at them)
			IslandWorldState.Entry e1 = IslandWorldState.Add(name, new Vector3(90000f, 0f, 90000f), null, false);
			IslandWorldState.Entry e2 = IslandWorldState.Add(name, new Vector3(91000f, 0f, 90000f), null, false);
			try
			{
				Check(ref ok, StoryBook.PageIsland(e1) == name && StoryBook.PageIsland(e2) == name + "|2", "the copies' page islands: '" + StoryBook.PageIsland(e1) + "', '" + StoryBook.PageIsland(e2) + "'");

				var pages = new IslandQuest.Step { Type = "pages", Target = "", Count = 1 };
				StoryBook.AddPage("note:" + StoryBook.PageIsland(e2) + ":5", "Copy two's note", "x", "Copy");
				int p1 = QuestTracker.Found(e1, pages), p2 = QuestTracker.Found(e2, pages);
				Check(ref ok, p1 == 0 && p2 == 1, "a note read on the second copy counts for its pages step only (first " + p1 + ", second " + p2 + ")");
				StoryBook.AddPage("note:" + StoryBook.PageIsland(e1) + ":5", "Copy one's note", "x", "Copy");
				p1 = QuestTracker.Found(e1, pages); p2 = QuestTracker.Found(e2, pages);
				Check(ref ok, p1 == 1 && p2 == 1, "the same note read on the first copy counts for the first (first " + p1 + ", second " + p2 + ")");

				var collect = new IslandQuest.Step { Type = "collect", Target = StoryItems.Ref(gem), Count = 2 };
				int before = StoryBook.FoundCount(gem);
				StoryBook.Give(gem, 2, e1);
				int c1 = QuestTracker.Found(e1, collect) - before, c2 = QuestTracker.Found(e2, collect) - before;
				Check(ref ok, c1 == 2 && c2 <= 0, "2 story items found on the first copy count for its collect step, not the second's (first +" + c1 + ", second " + c2 + ")");
				StoryBook.Give(gem, 1, e2);
				c1 = QuestTracker.Found(e1, collect) - before; c2 = QuestTracker.Found(e2, collect) - before;
				Check(ref ok, c1 == 2 && c2 == 1, "1 found on the second copy counts for the second only (first +" + c1 + ", second +" + c2 + ")");
				StoryBook.Give(gem, 1, null); // (the player is at neither copy: from elsewhere)
				c1 = QuestTracker.Found(e1, collect) - before; c2 = QuestTracker.Found(e2, collect) - before;
				Check(ref ok, c1 == 3 && c2 == 2, "1 found elsewhere counts for both (first +" + c1 + ", second +" + c2 + ")");
				var any = new IslandQuest.Step { Type = "collect", Target = "", Count = 1 };
				int a1 = QuestTracker.Found(e1, any), a2 = QuestTracker.Found(e2, any);
				Check(ref ok, a1 - a2 == 1, "a collect step for any story item leaves out the other copy's finds too (first " + a1 + ", second " + a2 + ")");

				// Saved with the world: the islands the items were found on come back
				string line = StoryBook.WriteLines().FirstOrDefault(l => l.StartsWith("@story.item=" + gem + "|", StringComparison.OrdinalIgnoreCase));
				Check(ref ok, line != null && line.Contains(Uri.EscapeDataString(name + "=2")) && line.Contains(Uri.EscapeDataString(Uri.EscapeDataString(name + "|2") + "=1")), "the saved line keeps where they were found: " + line);
				if (line != null)
				{
					int eq = line.IndexOf('=');
					StoryBook.ReadLine(line.Substring(1, eq - 1), line.Substring(eq + 1));
				}
				c1 = QuestTracker.Found(e1, collect) - before; c2 = QuestTracker.Found(e2, collect) - before;
				Check(ref ok, c1 == 3 && c2 == 2, "read back from the save the counts are the same (first +" + c1 + ", second +" + c2 + ")");
				// A world from before (no islands kept): every copy counts every find, as before
				StoryBook.ReadLine("story.item", Regex.Replace(line ?? "", @"^@story\.item=", "").Split('|').Take(7).Aggregate((x, y) => x + "|" + y));
				c1 = QuestTracker.Found(e1, collect) - before; c2 = QuestTracker.Found(e2, collect) - before;
				Check(ref ok, c1 == 4 && c2 == 4, "a save from before counts every find for both copies (first +" + c1 + ", second +" + c2 + ")");
			}
			finally
			{
				if (StoryBook.Count(gem) > 0) StoryBook.Take(gem, StoryBook.Count(gem));
				IslandWorldState.RemoveIds(new[] { e1.Id, e2.Id }, false);
			}
			if (ok) Log("PASS: island copies count their own pages and story items"); else Fail("island copies count their own pages and story items");
		}

		[ConsoleCommand(name: "CIIndexMenuScan", docs: "Dev, main menu: after a Raft update (pretended) the object index is noticed as stale and made again in the main menu, in the background; the index file is put back as it was after")]
		public static void IndexMenuScanCommand(string[] args) { StartTest(IndexMenuScanRoutine()); }

		static IEnumerator IndexMenuScanRoutine()
		{
			yield return null;
			if (!PlaceableCatalog.InMainMenu()) { Fail("index menu scan: needs the main menu"); yield break; }
			bool ok = true;
			string path = PlaceableCatalog.IndexPath;
			bool existed = File.Exists(path);
			string backup = existed ? File.ReadAllText(path) : null;
			string current = backup ?? PlaceableCatalog.ShippedIndex();
			string why0 = PlaceableCatalog.IndexStaleReason();
			Check(ref ok, why0 == null && current != null, "the installed index is current (" + (why0 ?? "yes") + ")");
			try
			{
				int objects0 = (current ?? "").Split('\n').Count(l => l.Trim().Length > 0 && !l.StartsWith("#"));
				// (a Raft update, pretended: the player's index and the mod's own were both made with the Raft before)
				PlaceableCatalog.RaftVersionForTest = "9.9.9-ci-new";
				string why = PlaceableCatalog.IndexStaleReason();
				Check(ref ok, why != null && why.Contains("9.9.9-ci-new"), "an index from another Raft is noticed: " + why);
				float started = Time.realtimeSinceStartup;
				PlaceableCatalog.CheckIndexInMenu(0f);
				while (PlaceableCatalog.MenuScanRunning && Time.realtimeSinceStartup - started < 400f) yield return new WaitForSecondsRealtime(1f);
				float took = Time.realtimeSinceStartup - started;
				string made = File.Exists(path) ? File.ReadAllText(path) : "";
				int objects = made.Split('\n').Count(l => l.Trim().Length > 0 && !l.StartsWith("#"));
				Check(ref ok, (PlaceableCatalog.MenuScanResult ?? "").StartsWith("scanned") && PlaceableCatalog.IndexStaleReason() == null,
					"the main menu scanned Raft's islands again: " + PlaceableCatalog.MenuScanResult + " in " + took.ToString("F0") + " s");
				Check(ref ok, made.Contains("raft=9.9.9-ci-new") && Regex.IsMatch(made, @"^# scenes=\d+", RegexOptions.Multiline) && objects >= objects0 * 9 / 10,
					"the new index has its scene count and " + objects + " objects (the one before: " + objects0 + ")");
				Check(ref ok, PlaceableCatalog.MenuScanLongestFrame < 1.5f, "the menu kept running: longest frame " + (PlaceableCatalog.MenuScanLongestFrame * 1000f).ToString("F0") + " ms (" + PlaceableCatalog.MenuScanLongestStep + ")");
				Check(ref ok, PlaceableCatalog.InMainMenu(), "still in the main menu");
			}
			finally
			{
				// (as it was: the player's file back, or none when the mod's own was used)
				PlaceableCatalog.RaftVersionForTest = null;
				try { if (existed) SafeFile.WriteAllText(path, backup); else if (File.Exists(path)) File.Delete(path); }
				catch (Exception ex) { Log("could not put the index back: " + ex.Message); }
				PlaceableCatalog.ReadIndexForTest();
			}
			if (ok) Log("PASS: a stale object index is made again in the main menu"); else Fail("a stale object index is made again in the main menu");
		}

		[ConsoleCommand(name: "CIOldWorldPlanHashes", docs: "Dev, world (host, 'CI ...'): a world from before plans kept their islands' hashes (pre-10-06) - without any owner the host adopts the plan and records its own islands' hashes; hosted by another player, a library plan's unchanged islands get their hashes from the install (an edited one, or another version, doesn't); all in one frame, the world's plan put back after")]
		public static void OldWorldPlanHashesCommand(string[] args)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("old world plan hashes: needs a world, as the host"); return; }
			string sample = IslandSpawner.ListSavedIslands().Where(n => File.Exists(IslandSpawner.PathFor(n))).OrderBy(n => new FileInfo(IslandSpawner.PathFor(n)).Length).FirstOrDefault();
			if (sample == null) { Fail("old world plan hashes: no island file to copy"); return; }
			bool ok = true;
			const string own = "ciplanold_own", lib = "ciplanold_lib";
			// (the world's plan state, put back after)
			WorldPlan plan0 = WorldDirector.Plan; string name0 = WorldDirector.PlanName, from0 = WorldDirector.PlanFrom;
			ulong owner0 = WorldDirector.PlanOwner, saved0 = WorldDirector.SavedByForTest;
			var hashes0 = new Dictionary<string, string>(WorldDirector.PlanHashes, StringComparer.OrdinalIgnoreCase);
			ulong me = 0; try { me = Steamworks.SteamUser.GetSteamID().m_SteamID; } catch { }
			try
			{
				byte[] bytes = File.ReadAllBytes(IslandSpawner.PathFor(sample));
				File.WriteAllBytes(IslandSpawner.PathFor(own), bytes);
				File.WriteAllBytes(IslandSpawner.PathFor(lib), bytes.Concat(new byte[] { (byte)'\n' }).ToArray());
				var plan = new WorldPlan { Name = "ciplanold", Random = false };
				plan.Rules.Add(new IntroRule { What = "island", WhatArg = own });
				plan.Rules.Add(new IntroRule { What = "oneof", WhatArg = lib + ", ciplanold_none" });
				WorldDirector.Plan = plan; WorldDirector.PlanName = plan.Name;

				// 1. No owner at all (saved before @savedby): the host adopts the plan, its files are the plan's
				WorldDirector.PlanFrom = ""; WorldDirector.PlanOwner = 0; WorldDirector.SavedByForTest = 0; WorldDirector.PlanHashes.Clear();
				WorldDirector.AdoptOwnerlessPlan();
				WorldDirector.RecordPlanHashes(WorldDirector.PlanOwner == me);
				Check(ref ok, me != 0 && WorldDirector.PlanOwner == me, "no owner known: this host owns the plan now (" + WorldDirector.PlanOwner + ")");
				string h;
				Check(ref ok, WorldDirector.PlanHashes.TryGetValue(own, out h) && h == IslandNetwork.HashOf(own) && WorldDirector.PlanHashes.ContainsKey(lib) && !WorldDirector.PlanHashes.ContainsKey("ciplanold_none"),
					"its islands' hashes are recorded at once (" + string.Join(", ", WorldDirector.PlanHashes.Select(k => k.Key + ":" + k.Value).ToArray()) + ")");

				// 2. Saved by another player, the plan from the library: the unchanged installed island's hash, the others by name as before
				WorldDirector.PlanOwner = 0; WorldDirector.SavedByForTest = 76561190000000001UL; WorldDirector.PlanHashes.Clear();
				WorldDirector.AdoptOwnerlessPlan();
				WorldDirector.PlanFrom = "library:ciplanold@3";
				var entry = new LibraryInstalled { id = "ciplanold", source = LibraryPack.SourceLibrary, version = 3, kind = LibraryPack.KindPlan };
				entry.files.Add(new LibraryInstalledFile { name = lib, original = lib, kind = LibraryPack.KindIsland, sha256 = LibraryPack.ShaOfFile(IslandSpawner.PathFor(lib)) });
				entry.files.Add(new LibraryInstalledFile { name = own, original = own, kind = LibraryPack.KindIsland, sha256 = "0000" }); // (changed here since)
				WorldDirector.InstalledForTest = new List<LibraryInstalled> { entry };
				WorldDirector.RecordPlanHashes(false);
				Check(ref ok, WorldDirector.PlanOwner == 0, "another player saved it: no owner taken over (" + WorldDirector.PlanOwner + ")");
				Check(ref ok, WorldDirector.PlanHashes.TryGetValue(lib, out h) && h == IslandNetwork.HashOf(lib) && WorldDirector.FileFor(lib) == lib,
					"the library plan's unchanged island has its hash from the install: " + (h ?? "none"));
				Check(ref ok, !WorldDirector.PlanHashes.ContainsKey(own) && WorldDirector.FileFor(own) == own, "an island changed since it was installed isn't taken for the plan's: found by name as before");

				// 3. Another version of the entry than the world's: nothing from it
				WorldDirector.PlanHashes.Clear();
				WorldDirector.PlanFrom = "library:ciplanold@2";
				WorldDirector.RecordPlanHashes(false);
				Check(ref ok, WorldDirector.PlanHashes.Count == 0, "the world's plan from another version of the entry: no hashes from the install (" + WorldDirector.PlanHashes.Count + ")");
			}
			catch (Exception e) { Fail("old world plan hashes: " + e); ok = false; }
			finally
			{
				WorldDirector.InstalledForTest = null;
				WorldDirector.Plan = plan0; WorldDirector.PlanName = name0; WorldDirector.PlanFrom = from0;
				WorldDirector.PlanOwner = owner0; WorldDirector.SavedByForTest = saved0;
				WorldDirector.PlanHashes.Clear();
				foreach (var k in hashes0) WorldDirector.PlanHashes[k.Key] = k.Value;
				foreach (string n in new[] { own, lib }) try { File.Delete(IslandSpawner.PathFor(n)); } catch { }
			}
			if (ok) Log("PASS: old worlds record their plan islands' hashes"); else Fail("old worlds record their plan islands' hashes");
		}
	}
}
