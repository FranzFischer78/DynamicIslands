using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandUndoRedo;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of the audit's leftovers (ROADMAP §2b A1-A8) and the release cases they closed: a file another program holds
	/// open (UP7) and a folder with a thousand islands (UP10).
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CILockFile", docs: "Dev, editor (TEST_CATALOGUE UP7): an island file another program holds open (a FileShare.None stream) - saving over it says 'in use by another program' and leaves the file as it was with no .tmp beside it, Delete says so and moves nothing, a settings write the same; once let go, saving works. Cleans up")]
		public static void LockFileCommand() { StartTest(LockFileRoutine()); }

		static IEnumerator LockFileRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("lock file: in the editor"); yield break; }
			bool ok = true;
			const string name = "citest-lock";
			string path = IslandSpawner.PathFor(name);
			int told = 0;
			Application.LogCallback counter = (msg, trace, type) => { if (msg.Contains("in use by another program")) told++; };
			FileStream held = null;
			try
			{
				DynamicIslands.NewIsland();
				Check(ref ok, DynamicIslands.SaveIsland(name), "a test island saved as '" + name + "'");
				byte[] before = File.ReadAllBytes(path);
				Application.logMessageReceived += counter;
				held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); // (as a program that locks it)
				terraineditor.terrain.terrainData.SetHeights(20, 20, new float[,] { { 0.4f } });
				UndoRedoManager.Execute(new NoopCommand());
				int t0 = told;
				bool saved = DynamicIslands.SaveIsland(name);
				Check(ref ok, !saved && told > t0, "saving over it is refused and says 'in use by another program'");
				held.Dispose(); held = null;
				Check(ref ok, File.ReadAllBytes(path).SequenceEqual(before) && !File.Exists(path + ".tmp"), "the saved island is as it was, no .tmp beside it");
				held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
				bool moved = true;
				try { IslandFilesWindow.MoveToDeleted(name); } catch (Exception e) { moved = false; Check(ref ok, SafeFile.InUse(e), "Delete: the error is 'in use' (" + e.GetType().Name + ")"); }
				Check(ref ok, !moved, "Delete moves nothing while it is held");
				string settings = Path.Combine(DynamicIslands.assetpath, "citest-lock.txt");
				File.WriteAllText(settings, "old");
				using (new FileStream(settings, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					bool refused = false;
					try { SafeFile.WriteAllText(settings, "new"); } catch (Exception e) { refused = SafeFile.InUse(e); }
					Check(ref ok, refused && !File.Exists(settings + ".tmp"), "a settings file held open: the write says 'in use', no .tmp left");
				}
				Check(ref ok, File.ReadAllText(settings) == "old", "... and the file is as it was");
				File.Delete(settings);
				held.Dispose(); held = null;
				Check(ref ok, DynamicIslands.SaveIsland(name) && !File.ReadAllBytes(path).SequenceEqual(before), "let go: saving works again (with the change)");
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e.Message); }
			finally
			{
				Application.logMessageReceived -= counter;
				if (held != null) held.Dispose();
				try { if (DynamicIslands.InEditor()) DynamicIslands.NewIsland(); } catch { }
				foreach (string f in new[] { path, path + ".tmp", EditorAutosave.PathFor(name) }) try { if (File.Exists(f)) File.Delete(f); } catch { }
			}
			if (ok) Log("PASS: lock file"); else Fail("lock file");
		}

		[ConsoleCommand(name: "CILeftoversUnit", docs: "Dev, anywhere (ROADMAP §2b A1, A4): Tidy up's rules on test files - a world file this PC hosted whose Raft world is gone counts as deleted, one saved by another host (a joined world) doesn't; a gen- island no world uses is unused, one a world uses isn't; a copy no world uses is unused; a rule finds an island from its copy (name_hash) when only that is here. Cleans up")]
		public static void LeftoversUnit()
		{
			bool ok = true;
			string worlds = Path.Combine(DynamicIslands.assetpath, "worlds");
			Directory.CreateDirectory(worlds);
			string mine = Path.Combine(worlds, "citest-left-mine.txt"), joined = Path.Combine(worlds, "citest-left-joined.txt"), user = Path.Combine(worlds, "citest-left-user.txt");
			const string genUsed = "gen-citest-left-used", genFree = "gen-citest-left-free", copyBase = "citest-left-copy";
			string copyName = null;
			try
			{
				string by = Housekeeping.SavedByLine();
				Check(ref ok, by != null, "this PC's hosting line: " + by);
				File.WriteAllLines(mine, new[] { "# Custom islands in world 'CI Gone World 7x': ...", "@savedat=1", by ?? "@savedby=1" });
				File.WriteAllLines(joined, new[] { "# Custom islands in world 'CI Joined World 7x': ...", "@savedat=1", "@savedby=76561190000000001" });
				var small = new IslandFile { TerrainSize = new Vector3(32f, 60f, 32f), HeightmapResolution = 17, Heights = new float[17, 17] };
				foreach (string g in new[] { genUsed, genFree, copyBase }) { small.Name = g; small.Heights[8, 8] = g.Length / 100f; small.Save(IslandSpawner.PathFor(g)); }
				string hash = IslandNetwork.HashOf(copyBase);
				copyName = IslandNetwork.DownloadName(copyBase, hash);
				File.Move(IslandSpawner.PathFor(copyBase), IslandSpawner.PathFor(copyName));
				File.WriteAllLines(user, new[] { "# Custom islands in world 'CI Left User 7x': ...", genUsed + "|0|0|0||||" });
				IslandCache.Forget();

				List<KeyValuePair<string, string>> gone = Housekeeping.DeletedWorldFiles();
				Check(ref ok, gone.Any(g => g.Value == "CI Gone World 7x"), "a world this PC hosted whose Raft world is gone counts as deleted");
				Check(ref ok, !gone.Any(g => g.Value == "CI Joined World 7x"), "a world another host saved (joined here) is kept");
				Housekeeping.Scan scan = Housekeeping.Look();
				Check(ref ok, scan.UnusedGenerated.Contains(genFree) && !scan.UnusedGenerated.Contains(genUsed), "a gen- island no world uses is unused, one a world uses isn't");
				Check(ref ok, scan.Copies.Any(c => c.Key == copyName && c.Value.Count == 0), "a copy no world uses is unused: " + copyName);
				Log("  " + scan.Describe());
				Check(ref ok, WorldDirector.FileFor(copyBase) == copyName && WorldDirector.FileFor("citest-left-none") == null, "a rule finds '" + copyBase + "' from its copy when only that is here");
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e.Message); }
			finally
			{
				foreach (string f in new[] { mine, joined, user }) try { if (File.Exists(f)) File.Delete(f); } catch { }
				foreach (string n in new[] { genUsed, genFree, copyBase, copyName }) try { if (n != null && File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n)); } catch { }
				IslandCache.Forget();
			}
			if (ok) Log("PASS: leftovers unit"); else Fail("leftovers unit");
		}

		[ConsoleCommand(name: "CIHotkeyTabs", docs: "Dev, world (host, a test world 'CI ...'): the hotbar's key tabs - the journal's (J) beside Raft's notebook tab, the stats page's (K) only while the level up system is on (switched on and back); picture shot_hotkey_tabs.png")]
		public static void HotkeyTabsCommand() { StartTest(HotkeyTabsRoutine()); }

		static IEnumerator HotkeyTabsRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("hotkey tabs: host in a test world 'CI ...'"); yield break; }
			bool ok = true;
			bool levels = PlayerLevels.On;
			yield return new WaitForSeconds(1f);
			GameObject j = GameObject.Find(HotkeyHints.JournalName), note = GameObject.Find("UI_Hotkey_Element_NoteBook");
			Func<GameObject, string> text = g => { if (g == null) return null; Component t = g.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == "TextMeshProUGUI"); return t != null ? HarmonyLib.Traverse.Create(t).Property("text").GetValue<string>() : null; };
			Check(ref ok, j != null && note != null && j.transform.parent == note.transform.parent, "the journal's tab is beside Raft's notebook tab");
			Check(ref ok, text(j) == JournalWindow.Key.ToString(), "it says the journal's key: " + text(j));
			Check(ref ok, j != null && note != null && j.transform.position.x > note.transform.position.x, "... after the notebook's tab (right of it)");
			Check(ref ok, HotkeyHints.KeyOverIcon(HotkeyHints.JournalName), "its key letter is drawn over the book icon (it was hidden under it)");
			// (Franz, 2026-10-17: the journal's tab showed no key, where the inventory's shows TAB)
			Check(ref ok, HotkeyHints.KeyShown(HotkeyHints.JournalName) == ModKeys.Name(JournalWindow.Key), "the journal's tab shows its key as the inventory's shows TAB (switched on, drawn, opaque): " + HotkeyHints.KeyShown(HotkeyHints.JournalName));
			KeyCode was = ModKeys.Journal;
			KeyCode other = new[] { KeyCode.H, KeyCode.U, KeyCode.Y, KeyCode.O, KeyCode.P }.FirstOrDefault(c => c != was && c != ModKeys.Stats && ModKeys.Problem(c) == null);
			string setProblem = ModKeys.Set(true, other);
			yield return new WaitForSeconds(0.7f);
			Check(ref ok, setProblem == null && HotkeyHints.KeyShown(HotkeyHints.JournalName) == ModKeys.Name(other), "the journal's key changed to " + other + ": its tab says so (" + (setProblem ?? HotkeyHints.KeyShown(HotkeyHints.JournalName)) + ")");
			ModKeys.Set(true, was);
			yield return new WaitForSeconds(0.7f);
			Check(ref ok, HotkeyHints.KeyShown(HotkeyHints.JournalName) == ModKeys.Name(was), "and back: " + HotkeyHints.KeyShown(HotkeyHints.JournalName));
			PlayerLevels.SetEnabled(true);
			yield return new WaitForSeconds(1.5f);
			GameObject k = GameObject.Find(HotkeyHints.StatsName);
			Check(ref ok, k != null && k.activeInHierarchy && text(k) == PlayerLevels.Key.ToString(), "the level up system on: the stats tab shows its key " + text(k));
			Screenshot(new[] { "hotkey_tabs" });
			yield return new WaitForSeconds(0.5f);
			PlayerLevels.SetEnabled(false);
			yield return new WaitForSeconds(1.5f);
			GameObject k2 = GameObject.Find(HotkeyHints.StatsName); // (Find skips inactive objects)
			Check(ref ok, k2 == null, "off: the stats tab is gone again");
			if (levels) PlayerLevels.SetEnabled(true);
			if (ok) Log("PASS: hotkey tabs"); else Fail("hotkey tabs");
		}

		[ConsoleCommand(name: "CIDropOpen", docs: "Dev: opens the first visible drop-down list named so (e.g. Drop_When), as a click on it would: CIDropOpen <name>")]
		public static void DropOpenCommand(string[] args)
		{
			string name = args != null && args.Length > 0 ? args[0] : "";
			// (the top-most one: cards further down a list are there too, scrolled out of view)
			DropdownButton d = UnityEngine.Object.FindObjectsOfType<DropdownButton>().Where(x => x.name == name && x.isActiveAndEnabled).OrderByDescending(x => x.transform.position.y).FirstOrDefault();
			if (d == null) { Fail("no visible drop-down '" + name + "'"); return; }
			DropList.Open(d.GetComponent<UnityEngine.UI.Button>(), d);
			Log("Opened the drop-down " + name + " (" + d.Options.Count + " options)");
			StartTest(DropReport());
		}

		static IEnumerator DropReport()
		{
			for (int i = 0; i < 3; i++)
			{
				GameObject list = GameObject.Find("DropdownList");
				RectTransform r = list != null ? (RectTransform)list.transform : null;
				Log("  frame " + i + ": open " + DropList.IsOpen + (r != null ? ", list at " + r.anchoredPosition + " size " + r.rect.size + " under " + r.parent.parent.name + ", screen " + RectTransformUtility.WorldToScreenPoint(null, r.position) : ", no list"));
				yield return null;
			}
		}

		[ConsoleCommand(name: "CICheckReport", docs: "Dev, editor: the World Plans window's Check on a plan full of mistakes - two test islands (one with a zone 'gate', a note 'Diary' and a quest whose steps need a note and creatures it hasn't, one without a quest) and rules that wait for a quest that can't be finished, for an island without a quest, for a zone it hasn't, for each other in a circle, near an island nobody has, with no number, and after a broken rule: each found at its level, with why and how to fix it; the report window opens (shot_plan_check.png) and each card shows its findings; SL4: a rule waiting for step 5 of a 3-step quest, for quest 3 of an island with 2, and a next-when step:4 on a 3-step quest are each a Problem with a fix, none with the quest put back. Cleans up")]
		public static void PlanCheckCommand() { StartTest(PlanCheckRoutine()); }

		static IEnumerator PlanCheckRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("plan check: in the editor"); yield break; }
			bool ok = true;
			const string questIsland = "cicheck-quest", plainIsland = "cicheck-plain", twoIsland = "cicheck-two", planName = "CI Check";
			try
			{
				// Two small islands: one with a zone, a note and a quest that can't be finished; one without a quest
				int res = 17;
				var h = new float[res, res]; for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) h[y, x] = 0.4f;
				var q = new IslandFile { TerrainSize = new Vector3(64f, 60f, 64f), HeightmapResolution = res, Heights = h, Name = questIsland };
				q.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = new Vector3(30, 25, 30), Props = new Dictionary<string, string> { { ObjectProps.ZoneId, "gate" } } });
				q.Objects.Add(new IslandObject { Name = "Note_Paper", Position = new Vector3(32, 25, 30), Props = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Diary" }, { ObjectProps.NoteText, "Hello" } } });
				var quest = new IslandQuest { Title = "Test" };
				quest.Steps.Add(new IslandQuest.Step { Type = "reach", Target = "gate" });
				quest.Steps.Add(new IslandQuest.Step { Type = "read", Target = "Lost page" });
				quest.Steps.Add(new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 5 });
				quest.To(q.Props);
				q.Save(IslandSpawner.PathFor(questIsland));
				var p = new IslandFile { TerrainSize = q.TerrainSize, HeightmapResolution = res, Heights = h, Name = plainIsland };
				p.Save(IslandSpawner.PathFor(plainIsland));

				var plan = new WorldPlan { Name = planName, Random = false };
				Func<string, string, IntroRule> rule = (id, line) => IntroRule.Parse(id + " | " + line);
				plan.Rules.Add(rule("start", "island:" + questIsland + " | start | ahead:300 | A test | Start"));
				plan.Rules.Add(rule("afterquest", "type:camp | quest:start | near:start:600:any | msg | A"));          // 2: the quest can't be finished
				plan.Rules.Add(rule("plain", "island:" + plainIsland + " | km:1 | ahead:300 | msg | P"));
				plan.Rules.Add(rule("noquest", "type:camp | quest:plain | near:plain:600:any | msg | N"));               // 4: no quest on 'plain'
				plan.Rules.Add(rule("cave", "type:camp | zone:start:cave | near:start:600:any | msg | C"));              // 5: no zone 'cave'
				plan.Rules.Add(rule("circleA", "type:camp | rule:circleB | ahead:300 | msg | A2"));                      // 6-7: a circle
				plan.Rules.Add(rule("circleB", "type:camp | rule:circleA | ahead:300 | msg | B2"));
				plan.Rules.Add(rule("lost", "type:camp | start | near:nobody:600:any | msg | L"));                       // 8: near nobody
				plan.Rules.Add(rule("nonumber", "type:camp | km:abc | ahead:300 | msg | X"));                           // 9: no number
				plan.Rules.Add(rule("afterbroken", "type:camp | visit:afterquest | near:afterquest:600:any | msg | B")); // 10: after a broken rule
				Check(ref ok, plan.Rules.All(r => r != null), "the test plan's " + plan.Rules.Count + " rules read");

				List<PlanChecker.Finding> found = PlanChecker.Check(plan, false, true);
				foreach (PlanChecker.Finding f in found) Log("  " + f.Level + " " + (f.Rule + 1) + ": " + f.Text);
				Func<int, PlanChecker.Level, string, bool> has = (i, lvl, part) => found.Any(f => f.Rule == i && f.Level == lvl && f.Text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 && f.Fix.Length > 0);
				Check(ref ok, !found.Any(f => f.Rule == 0 && f.Level == PlanChecker.Level.Problem), "rule 1 (the start island) has no problem");
				Check(ref ok, has(1, PlanChecker.Level.Problem, "needs a note titled \"Lost page\""), "rule 2: its island's quest can't be finished - step 2 needs a note 'Lost page' (the island has 'Diary')");
				Check(ref ok, has(1, PlanChecker.Level.Problem, "warthogs"), "rule 2: ... and step 3 needs warthogs the island hasn't");
				Check(ref ok, has(3, PlanChecker.Level.Problem, "has no quest"), "rule 4 waits for the quest of an island without one");
				Check(ref ok, has(4, PlanChecker.Level.Problem, "zone 'cave'"), "rule 5 waits for a zone 'cave' the island hasn't (it has 'gate')");
				Check(ref ok, found.Any(f => f.Level == PlanChecker.Level.Problem && f.Text.Contains("in a circle")), "rules 6 and 7 wait for each other in a circle");
				Check(ref ok, has(7, PlanChecker.Level.Problem, "placed near 'nobody'"), "rule 8 is placed near an island nobody has");
				Check(ref ok, has(8, PlanChecker.Level.Problem, "no number"), "rule 9 waits for a distance with no number");
				Check(ref ok, has(9, PlanChecker.Level.Warning, "has a problem"), "rule 10 waits for a broken rule: a warning");
				Check(ref ok, found.Where(f => f.Level == PlanChecker.Level.Problem).All(f => f.Fix.Length > 0), "every problem says how to fix it");

				// SL4: a step or quest removed from an island a rule waits for - each is a Problem; with the quest put back, none
				{
					Func<int, int, IslandFile> twoIslandFile = (mainSteps, quests) =>
					{
						var t = new IslandFile { TerrainSize = q.TerrainSize, HeightmapResolution = res, Heights = h, Name = twoIsland };
						t.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = new Vector3(30, 25, 30), Props = new Dictionary<string, string> { { ObjectProps.ZoneId, "gate" } } });
						for (int n = 0; n < quests; n++)
						{
							var qq = new IslandQuest { Title = "Quest " + (n + 1) };
							for (int s = 0; s < (n == 0 ? mainSteps : 1); s++) qq.Steps.Add(new IslandQuest.Step { Type = "reach", Target = "gate" });
							qq.To(t.Props, n);
						}
						return t;
					};
					Func<List<PlanChecker.Finding>> checkTwo = () =>
					{
						var plan2 = new WorldPlan { Name = planName, Random = false };
						plan2.Rules.Add(rule("start", "island:" + twoIsland + " | start | ahead:300 | A test | Start"));
						plan2.Rules.Add(rule("step5", "type:camp | step:start:5 | ahead:300 | msg | S5"));            // 2: step 5 of a 3-step quest
						plan2.Rules.Add(rule("quest3", "type:camp | quest:start:3 | ahead:300 | msg | Q3"));          // 3: quest 3 of an island with 2
						plan2.Rules.Add(rule("nextwhen", "island:" + twoIsland + " | start | receiver:300 | msg | NW | first | step:4")); // 4: next-when step 4 of a 3-step quest
						return PlanChecker.Check(plan2, false, true);
					};
					twoIslandFile(3, 2).Save(IslandSpawner.PathFor(twoIsland));
					List<PlanChecker.Finding> broken = checkTwo();
					foreach (PlanChecker.Finding f in broken) Log("  SL4 " + f.Level + " " + (f.Rule + 1) + ": " + f.Text);
					Func<List<PlanChecker.Finding>, int, string, bool> problem = (list, i, part) => list.Any(f => f.Rule == i && f.Level == PlanChecker.Level.Problem && f.Text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 && f.Fix.Length > 0);
					Check(ref ok, problem(broken, 1, "5 quest steps") && problem(broken, 1, "only 3"), "SL4: a rule waiting for step 5 of a 3-step quest is a Problem with a fix");
					Check(ref ok, problem(broken, 2, "quest 3"), "SL4: a rule waiting for quest 3 of an island with 2 quests is a Problem with a fix");
					Check(ref ok, problem(broken, 3, "4 quest steps") && problem(broken, 3, "only 3"), "SL4: a next-when step:4 on a 3-step quest is a Problem with a fix");
					// (the quest put back: five steps in Quest 1, three quests)
					twoIslandFile(5, 3).Save(IslandSpawner.PathFor(twoIsland));
					List<PlanChecker.Finding> fixedUp = checkTwo();
					foreach (PlanChecker.Finding f in fixedUp) Log("  SL4 (put back) " + f.Level + " " + (f.Rule + 1) + ": " + f.Text);
					Check(ref ok, !fixedUp.Any(f => f.Level == PlanChecker.Level.Problem && (f.Text.Contains("quest steps of") || f.Text.Contains("waits for quest 3"))),
						"SL4: with the quest put back (5 steps, 3 quests), none of the three is a Problem");
				}

				// The window: Check opens the report, the cards show their findings
				plan.Save();
				WorldPlanWindow.Open(planName);
				yield return null;
				Transform window = EditorUI.Canvas.transform.Find("WorldPlanWindow");
				UnityEngine.UI.Button check = window != null ? window.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(b => UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == "Check") : null;
				if (check != null) check.onClick.Invoke();
				yield return null; yield return null;
				Check(ref ok, PlanCheckWindow.IsOpen && PlanCheckWindow.Shown != null && PlanCheckWindow.Shown.Count(f => f.Level == PlanChecker.Level.Problem) >= 7, "Check opens the report: " + (PlanCheckWindow.Shown != null ? PlanCheckWindow.Shown.Count + " findings" : "none"));
				int onCards = window != null ? window.GetComponentsInChildren<Transform>(false).Count(t => t.name == "Finding") : 0;
				Check(ref ok, onCards >= 1, "the cards show their findings (" + onCards + " shown in view)");
				Screenshot(new[] { "plan_check" });
				yield return new WaitForSeconds(0.5f);
				PlanCheckWindow.Close();
				WorldPlanWindow.Close();
			}
			finally
			{
				PlanCheckWindow.Close();
				WorldPlanWindow.Close();
				foreach (string n in new[] { questIsland, plainIsland, twoIsland }) try { if (System.IO.File.Exists(IslandSpawner.PathFor(n))) System.IO.File.Delete(IslandSpawner.PathFor(n)); } catch { }
				try { if (System.IO.File.Exists(WorldPlan.PathFor(planName))) System.IO.File.Delete(WorldPlan.PathFor(planName)); } catch { }
			}
			if (ok) Log("PASS: plan check"); else Fail("plan check");
		}

		#region Test plans

		/// <summary>Test plans (the sample plans the mod wrote until 2026-10-16, when the user dropped them: tests now write
		/// these "CI ..." plans themselves and move them to deleted\plans after). Together they use most map types, WHEN and
		/// WHERE kinds, and a Receiver story with Raft's story off.</summary>
		internal const string CIAdventure = "CI Adventure", CIHopping = "CI Island hopping", CIGrowing = "CI Growing sea", CIReceiver = "CI Receiver adventure";

		internal static readonly List<KeyValuePair<string, WorldPlanTemplates.Template>> CIPlans = new List<KeyValuePair<string, WorldPlanTemplates.Template>>
		{
			new KeyValuePair<string, WorldPlanTemplates.Template>("CI Island hopping", new WorldPlanTemplates.Template {
				Description = "An old camp near the start; each island you reach shows the way to the next, further out",
				Text = "random = on\n" +
					"rule = camp | type:camp | start | ahead:350 | Smoke rises from a small island ahead. | Old camp\n" +
					"rule = treasure | type:treasure | visit:camp | near:camp:900:any | Birds fly off towards another island... | Treasure\n" +
					"rule = islets | type:archipelago | visit:treasure | near:treasure:1100:any | From the top you can see a group of islets. | Islets\n" +
					"rule = volcano | type:volcano | visit:islets | near:islets:1400:any | Smoke rises far away. | Volcano\n" }),
			new KeyValuePair<string, WorldPlanTemplates.Template>("CI Adventure", new WorldPlanTemplates.Template {
				Description = "A story: each quest leads to the next island (camp, islets, a beast, a treasure), with wrecks and a sunken island on the way",
				Text = "random = off\n" +
					"rule = camp | type:camp | start | ahead:350 | Smoke rises from a small island ahead. | Old camp\n" +
					"rule = islets | type:archipelago | quest:camp | near:camp:900:north-east | The notice mentioned islets to the north-east. | Islets\n" +
					"rule = beast | type:boss | quest:islets | near:islets:1000:any | The castaway's note warns of a beast on a plateau... | Plateau\n" +
					"rule = treasure | type:treasure | quest:beast | near:beast:900:any | Among the spoils: a map to a treasure island! | Treasure\n" +
					"rule = wreck | type:wreck | km:2 | ahead:300 | Something floats ahead: a wrecked raft. | Wreck\n" +
					"rule = sunken | type:sunken | km:5 | ahead:350 | The water below looks strangely shallow. Dive! | Sunken\n" }),
			new KeyValuePair<string, WorldPlanTemplates.Template>("CI Growing sea", new WorldPlanTemplates.Template {
				Description = "Random islands while sailing, plus a special island every few km and days",
				Text = "random = on\n" +
					"rule = sandbar | type:sandbar | km:1 | ahead:300 | A tiny island with a chest. | Sandbar\n" +
					"rule = atoll | type:atoll | km:4 | ahead:450 | A ring of land around a lagoon. | Atoll\n" +
					"rule = stacks | type:stacks | day:3 | ahead:400 | Tall rocks rise from the sea. | Sea stacks\n" +
					"rule = swamp | type:swamp | day:6 | ahead:400 | A green mist hangs over the water. | Swamp\n" +
					"rule = spire | type:spire | km:8 | ahead:450 | It's getting colder. | Frozen spire\n" +
					"rule = volcano | type:volcano | day:10 | ahead:450 | The sea smells of sulphur. | Volcano\n" }),
			new KeyValuePair<string, WorldPlanTemplates.Template>("CI Receiver adventure", new WorldPlanTemplates.Template {
				Description = "A new adventure instead of Raft's story: each island is found with the Receiver, and its quest gives the next frequency",
				Text = "random = off\n" +
					"story = off\n" +
					"storyending = The treasure is yours, and the last frequency falls silent. Every island of the adventure is in this book.\\n\\nThe end.\n" +
					"rule = camp | type:camp | start | receiver:600 | A faint signal crackles on the Receiver... | Old camp | first | quest\n" +
					"rule = islets | type:archipelago | start | receiver:800 | The camp's radio log names another frequency. | Islets | after:camp | quest\n" +
					"rule = beast | type:boss | start | receiver:900 | A distress call from a plateau... | Plateau | after:islets | quest\n" +
					"rule = treasure | type:treasure | start | receiver:800 | Among the spoils: the frequency of a treasure island! | Treasure | after:beast | quest\n" }),
		};

		/// <summary>A test plan as written (not the file on disk).</summary>
		internal static WorldPlan CIPlanOf(string name)
		{
			WorldPlanTemplates.Template t = CIPlans.First(x => x.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
			WorldPlan p = WorldPlan.Parse(name, t.Text);
			p.Description = t.Description;
			return p;
		}

		/// <summary>Writes a test plan to the plans folder (fresh each time) and returns its name; RemoveCIPlan moves it out after.</summary>
		internal static string WriteCIPlan(string name) { CIPlanOf(name).Save(); return name; }

		/// <summary>Moves a test plan's file to deleted\plans, so no test plans stay in the player's list.</summary>
		internal static void RemoveCIPlan(string name)
		{
			try { string f = WorldPlan.PathFor(name); if (File.Exists(f)) PiecesFiles.MoveToDeleted(f, "plans"); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not move the test plan '" + name + "' aside: " + e.Message); }
		}

		#endregion

		[ConsoleCommand(name: "CICheckPlans", docs: "Dev, editor: runs the World Plans window's Check on plans and logs what it finds (CICheckPlans = the four test plans CI Adventure, CI Island hopping, CI Growing sea, CI Receiver adventure; CICheckPlans <plan> = one saved plan). The test plans must have no problems: PASS: test plans check clean")]
		public static void CheckPlansCommand(string[] args)
		{
			bool one = args != null && args.Length > 0;
			List<string> names = one ? new List<string> { string.Join(" ", args) } : CIPlans.Select(t => t.Key).ToList();
			bool ok = true;
			foreach (string n in names)
			{
				// (the test plans as written here, not files on disk)
				WorldPlan p = one ? WorldPlan.Load(n) : CIPlanOf(n);
				if (p == null) { Log("  no plan '" + n + "'"); continue; }
				List<PlanChecker.Finding> found = PlanChecker.Check(p, false, true);
				Log("Plan '" + n + "': " + found.Count(f => f.Level == PlanChecker.Level.Problem) + " problems, " + found.Count(f => f.Level == PlanChecker.Level.Warning) + " warnings, " + found.Count(f => f.Level == PlanChecker.Level.Tip) + " tips");
				foreach (PlanChecker.Finding f in found) Log("  " + f.Level + " " + (f.Rule + 1) + ": " + f.Text);
				if (found.Any(f => f.Level == PlanChecker.Level.Problem)) ok = false;
			}
			if (!one) { if (ok) Log("PASS: test plans check clean"); else Fail("test plans check clean"); }
		}

		const string CrateIsland = "cicrate";

		[ConsoleCommand(name: "CIRaftCrateEditor", docs: "Dev, editor: the abandoned rafts' crate (Pickup_Landmark_LandmarkCrateRaft) is in the object list under Loot & chests as 'Abandoned raft crate'; placed on a small generated island saved as 'cicrate' (for CIRaftCrateWorld)")]
		public static void RaftCrateEditorCommand() { StartTest(RaftCrateEditorRoutine()); }

		static IEnumerator RaftCrateEditorRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("raft crate editor: in the editor"); yield break; }
			bool ok = true;
			yield return PlaceableCatalog.EnsureBuilt();
			string crate = PlaceableCatalog.RaftCrate;
			Check(ref ok, PlaceableCatalog.IsHarvestable(crate), "the crate is taken from Raft's drifting raft with its gameplay");
			Check(ref ok, PlaceableCatalog.CategoryOf(crate) == ContentCatalog.LootCategory, "it is listed under " + ContentCatalog.LootCategory + " (" + PlaceableCatalog.CategoryOf(crate) + ")");
			DynamicIslands.NewIsland();
			yield return null;
			IslandGenerator.GenerateInEditor(new IslandGenSettings { Seed = 777, Radius = 50f, Height = 20f, Style = TerrainPainter.Tropical });
			yield return null;
			Transform placed = GameObject.Find("PlacedObjects").transform;
			Terrain t = terraineditor.terrain;
			Vector3 at = t.transform.position + new Vector3(500f, 0f, 500f);
			at.y = t.transform.position.y + t.SampleHeight(at) + 0.3f;
			GameObject go = PlaceableCatalog.Spawn(crate, placed);
			Check(ref ok, go != null, "the crate can be placed in the editor");
			if (go != null)
			{
				go.transform.position = at;
				EditorGameObject.Attach(go, crate, null);
				Check(ref ok, DynamicIslands.SaveIsland(CrateIsland), "a small island with the crate saved as '" + CrateIsland + "'");
				IslandFile f = IslandFile.Load(IslandSpawner.PathFor(CrateIsland));
				Check(ref ok, f.Objects.Any(o => o.Name == crate), "the island file has the crate");

				// Its two limits, where builders look: the list's hint, the inspector, Check
				string hint = ContentCatalog.Hint(crate) ?? "";
				Check(ref ok, hint.Contains("can't be chosen") && hint.Contains("Open a chest"), "the object list's hint tells both limits");
				EditorUI.SetTab(TAB.ObjectPlace);
				DynamicIslands.EditorGizmoHandler.ClearTargets(false);
				DynamicIslands.EditorGizmoHandler.AddTarget(go.transform, false);
				ObjectInspector.Refresh();
				yield return null;
				yield return new WaitForSecondsRealtime(0.3f);
				Transform group = EditorUI.Canvas.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Group_Abandoned raft crate");
				string said = group != null ? string.Join(" ", group.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(x => x.text).ToArray()) : "";
				Check(ref ok, group != null && group.gameObject.activeInHierarchy && said.Contains("can't be chosen") && said.Contains("Open a chest"), "the inspector's 'Abandoned raft crate' group tells both limits");
				Check(ref ok, !EditorUI.Canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Any(b => UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text.StartsWith("A chest")), "the inspector doesn't offer to make it a chest");
				Screenshot(new[] { "raft_crate_inspector" });
				yield return new WaitForSecondsRealtime(0.5f);
				EditorUI.SetTab(TAB.TerrainEdit);
				var quest = new IslandQuest { Title = "Test" };
				quest.Steps.Add(new IslandQuest.Step { Type = "open" });
				quest.To(f.Props);
				f.Save(IslandSpawner.PathFor(CrateIsland));
				var plan = new WorldPlan { Name = "CI crate plan", Random = false };
				plan.Rules.Add(IntroRule.Parse("start | island:" + CrateIsland + " | start | ahead:300 | A test | Start"));
				plan.Rules.Add(IntroRule.Parse("after | type:camp | quest:start | near:start:600:any | msg | A"));
				List<PlanChecker.Finding> found = PlanChecker.Check(plan, false, true);
				PlanChecker.Finding open = found.FirstOrDefault(x => x.Rule == 1 && x.Level == PlanChecker.Level.Problem && x.Text.Contains("needs a chest to open"));
				Check(ref ok, open != null && open.Text.Contains("raft crates don't count"), "Check: an \"Open a chest\" step on an island with only the raft crate is a problem, and says the crate doesn't count" + (open != null ? " (" + open.Text + ")" : ""));
				foreach (string key in f.Props.Keys.Where(k => k.StartsWith("quest.")).ToList()) f.Props.Remove(key);
				f.Save(IslandSpawner.PathFor(CrateIsland));
			}
			DynamicIslands.NewIsland();
			if (ok) Log("PASS: raft crate editor"); else Fail("raft crate editor");
		}

		[ConsoleCommand(name: "CIRaftCrateWorld", docs: "Dev, world (host, a test world 'CI ...'; after CIRaftCrateEditor): the island 'cicrate' comes beside the raft; its crate is Raft's own (a pickup with Raft's random loot); picked up as a player does, it gives items and is gone, and the island remembers it was taken. The island and its file are removed after")]
		public static void RaftCrateWorldCommand() { StartTest(RaftCrateWorldRoutine()); }

		static IEnumerator RaftCrateWorldRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("raft crate world: host in a test world 'CI ...'"); yield break; }
			if (!System.IO.File.Exists(IslandSpawner.PathFor(CrateIsland))) { Fail("raft crate world: run CIRaftCrateEditor first"); yield break; }
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft, CustomIslandSpawner.LandRadius(CrateIsland), 700f) ?? CustomIslandSpawner.FindClearSpot(raft, CustomIslandSpawner.LandRadius(CrateIsland), 1500f);
			Check(ref ok, spot.HasValue, "a place for the island near the raft");
			if (!spot.HasValue) { Fail("raft crate world"); yield break; }
			// (the player there first: islands stay loaded where a player is, however far the raft)
			{ CharacterController c0 = player.PersonController.controller; c0.enabled = false; player.transform.position = spot.Value + Vector3.up * 30f; player.PersonController.SwitchControllerType(ControllerType.Ground); c0.enabled = true; }
			yield return new WaitForSeconds(1f);
			yield return DynamicIslands.instance.SpawnIslandFile(CrateIsland, spot.Value, true);
			yield return new WaitForSeconds(2f);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => x.HostName == CrateIsland);
			GameObject crate = e != null && e.Root != null ? e.Root.GetComponentsInChildren<Transform>(true).Select(x => x.gameObject).FirstOrDefault(g => g.name.StartsWith(PlaceableCatalog.RaftCrate)) : null;
			PickupItem pickup = crate != null ? crate.GetComponentInChildren<PickupItem>() : null;
			Check(ref ok, crate != null && pickup != null && crate.GetComponentInChildren<RandomDropper>() != null && crate.GetComponentInChildren<PickupItem_Networked>() != null,
				"the island's crate is Raft's own: a pickup with Raft's random loot" + (crate == null ? " (no crate on the island)" : ""));
			if (pickup != null && player != null)
			{
				// (the player beside the crate, as one who swam or sailed there)
				CharacterController cc = player.PersonController.controller;
				cc.enabled = false;
				player.transform.position = crate.transform.position + Vector3.up * 1.5f + Vector3.right * 1.2f;
				player.PersonController.SwitchControllerType(ControllerType.Ground);
				cc.enabled = true;
				yield return new WaitForSeconds(1f);
				Func<int> items = () => player.Inventory.allSlots.Where(sl => sl != null && sl.itemInstance != null && sl.itemInstance.baseItem != null).Sum(sl => sl.itemInstance.Amount);
				int before = items();
				int errors = 0; string first = null;
				Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; if (first == null) first = msg; } };
				Application.logMessageReceived += counter;
				Pickup hands = player.GetComponentInChildren<Pickup>();
				try { if (hands != null) hands.PickupItem(pickup, true, false); }
				catch (Exception ex) { errors++; first = first ?? ex.Message; }
				yield return new WaitForSeconds(2f);
				Application.logMessageReceived -= counter;
				int after = items();
				Check(ref ok, hands != null && after > before, "picked up as a player does: items " + before + " -> " + after);
				Check(ref ok, crate == null || !crate.activeInHierarchy, "the crate is gone");
				// (what the island keeps is recorded when the world saves or the island unloads: now)
				Check(ref ok, crate != null, "the crate is hidden, not destroyed (so the island can remember it)");
				IslandObjectState.Capture(e);
				Check(ref ok, e.State.Values.Any(s => !s.Active), "the island remembers the crate was taken (" + string.Join(", ", e.State.Select(kv => kv.Key + (kv.Value.Active ? ":active" : ":taken")).ToArray()) + ")");
				// (unloaded and loaded again: still taken)
				IslandSpawner.Despawn(e.Root); e.Root = null;
				yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
				yield return new WaitForSeconds(1f);
				GameObject again = e.Root != null ? e.Root.GetComponentsInChildren<Transform>(true).Select(x => x.gameObject).FirstOrDefault(g => g.name.StartsWith(PlaceableCatalog.RaftCrate)) : null;
				Check(ref ok, again != null && !again.activeInHierarchy, "loaded again: the crate is still taken");
				Check(ref ok, errors == 0, "no errors (" + errors + (first != null ? ": " + first : "") + ")");
			}
			IslandWorldState.Remove(CrateIsland);
			try { System.IO.File.Delete(IslandSpawner.PathFor(CrateIsland)); } catch { }
			if (ok) Log("PASS: raft crate world"); else Fail("raft crate world");
		}

		[ConsoleCommand(name: "CIChargeMeterEarly", docs: "Dev, world (a test world 'CI ...'): Raft's quirk a crate pickup ran into - with the inventory emptied, a fishing rod, hook or bow and planks given in one go into the empty selected hotbar slot (as a crate's loot is): both arrive and no error (Raft reset the item's charge meter before its Start: RaftQuirks fills it in first). The inventory is emptied after")]
		public static void ChargeMeterEarlyCommand() { StartTest(ChargeMeterEarlyRoutine()); }

		static IEnumerator ChargeMeterEarlyRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("charge meter early: in a test world 'CI ...'"); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			bool ok = true;
			int errors = 0; string first = null;
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; if (first == null) first = msg; } };
			int caught = ChargeMeterResetBeforeStart.Caught, tried = 0;
			foreach (string name in new[] { "FishingRod", "Hook_Plastic", "Bow" })
			{
				if (ItemManager.GetItemByName(name) == null) { Log("charge meter early: no item '" + name + "' in this Raft"); continue; }
				ClearInventory();
				yield return new WaitForSeconds(0.5f);
				int e0 = errors;
				Application.logMessageReceived += counter;
				try { player.Inventory.AddItem(name, 1); player.Inventory.AddItem("Plank", 3); }
				catch (Exception ex) { errors++; first = first ?? ex.Message; }
				yield return new WaitForSeconds(1f);
				Application.logMessageReceived -= counter;
				tried++;
				Check(ref ok, errors == e0 && player.Inventory.GetItemCount(name) == 1 && player.Inventory.GetItemCount("Plank") == 3, name + " and planks given at once: both arrive, no error" + (errors > e0 ? " (" + first + ")" : ""));
			}
			Check(ref ok, tried > 0, "items tried: " + tried);
			Log("charge meter early: a meter reset before its Start " + (ChargeMeterResetBeforeStart.Caught - caught) + " time(s), filled in first");
			ClearInventory();
			if (ok) Log("PASS: charge meter early"); else Fail("charge meter early");
		}

		const string BulkPrefix = "bulk-";

		[ConsoleCommand(name: "CIBulkIslands", docs: "Dev, anywhere (editor for the Islands window) (TEST_CATALOGUE UP10): CIBulkIslands [n] - n small islands (bulk-0001 ..., default 1000) in the folder: listing them, the spawn pool, CHOOSE ISLANDS' list, the library's Tidy up look and (in the editor) the Islands window each take under 2 s, and the search finds one. The islands are removed after (CIBulkIslands keep leaves them; CIBulkIslands clean removes them)")]
		public static void BulkIslandsCommand(string[] args)
		{
			string a = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (a == "clean") { Log("Removed " + BulkClean() + " bulk islands"); return; }
			int n;
			if (!int.TryParse(a, out n)) n = 1000;
			StartTest(BulkRoutine(Mathf.Clamp(n, 1, 5000), args != null && args.Contains("keep")));
		}

		static int BulkClean()
		{
			int n = 0;
			foreach (string f in Directory.GetFiles(DynamicIslands.assetpath, BulkPrefix + "*" + IslandFile.Extension)) { try { File.Delete(f); n++; } catch { } }
			IslandCache.Forget();
			CustomIslandSpawner.LoadPool(true);
			return n;
		}

		static IEnumerator BulkRoutine(int count, bool keep)
		{
			bool ok = true;
			try
			{
				// A small island: 64 m, a hill in the middle, nothing on it (a thousand of them are ~2 MB)
				int res = 33;
				var h = new float[res, res];
				for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) { float d = new Vector2(x - 16, y - 16).magnitude / 16f; h[y, x] = Mathf.Max(0f, 0.45f - 0.3f * d); }
				var file = new IslandFile { TerrainSize = new Vector3(64f, 60f, 64f), HeightmapResolution = res, Heights = h };
				var sw = Stopwatch.StartNew();
				for (int i = 1; i <= count; i++) { file.Name = BulkPrefix + i.ToString("0000"); file.Save(IslandSpawner.PathFor(file.Name)); }
				Log(count + " small islands written in " + sw.ElapsedMilliseconds + " ms");
				IslandCache.Forget();

				Func<string, Action, long> time = (what, act) => { var t = Stopwatch.StartNew(); act(); t.Stop(); Log("  " + what + ": " + t.ElapsedMilliseconds + " ms"); return t.ElapsedMilliseconds; };
				List<string> listed = null;
				long listMs = time("listing the islands", () => listed = IslandSpawner.ListSavedIslands().ToList());
				Check(ref ok, listMs < 2000 && listed.Count(x => x.StartsWith(BulkPrefix)) == count, "the islands are listed (" + listed.Count + " in all) in " + listMs + " ms");
				Check(ref ok, listed.Contains(BulkPrefix + (count / 2).ToString("0000")), "the search finds one of them: " + BulkPrefix + (count / 2).ToString("0000"));
				long poolMs = time("the spawn pool", () => CustomIslandSpawner.LoadPool(true));
				Check(ref ok, poolMs < 2000, "the spawn pool builds in " + poolMs + " ms");
				List<string> candidates = null;
				long pickMs = time("CHOOSE ISLANDS' list", () => candidates = WorldIslands.Candidates());
				Check(ref ok, pickMs < 2000 && candidates.Count(c => c.StartsWith(BulkPrefix)) == count, "CHOOSE ISLANDS lists them in " + pickMs + " ms");
				Housekeeping.Scan scan = null;
				// (the Installed tab no longer looks through everything when it opens: Tidy up does, on its first click)
				long tidyMs = time("Tidy up's look (its first click)", () => scan = Housekeeping.Look());
				Check(ref ok, tidyMs < 10000 && scan != null, "Tidy up looks through them and every world's saves in " + tidyMs + " ms (on its click)");
				// (Tidy up reads only each island's settings now: the same settings as a whole read, on the islands that have some)
				int same = 0, compared = 0;
				foreach (string n in listed.Where(x => !x.StartsWith(BulkPrefix) && !IslandNetwork.IsDownloadName(x)).Take(40))
				{
					string p = IslandSpawner.PathFor(n);
					Dictionary<string, string> whole = IslandFile.Load(p).Props, part = IslandFile.LoadProps(p);
					compared++;
					if (whole.Count == part.Count && whole.All(kv => part.ContainsKey(kv.Key) && part[kv.Key] == kv.Value)) same++;
				}
				Check(ref ok, same == compared, "reading only an island's settings gives the same as reading it whole (" + same + " of " + compared + ")");
				if (DynamicIslands.InEditor())
				{
					long winMs = time("the Islands window", () => { IslandFilesWindow.Open(); IslandFilesWindow.Close(); });
					Check(ref ok, winMs < 2000, "the Islands window opens in " + winMs + " ms");
				}
				long mem = GC.GetTotalMemory(false) / (1024 * 1024);
				Log("  managed memory now " + mem + " MB");
			}
			catch (Exception e) { Check(ref ok, false, "no errors: " + e.Message); }
			finally { if (!keep) Log("Removed " + BulkClean() + " bulk islands"); }
			yield return null;
			if (ok) Log("PASS: bulk islands"); else Fail("bulk islands");
		}

		static string WorldDeleteMarker { get { return Path.Combine(Application.temporaryCachePath, "ci_worlddelete.txt"); } }

		[ConsoleCommand(name: "CIWorldDelete", docs: "Dev, UW8 - a world deleted in Raft's Load Game box: CIWorldDelete mark (in a 'CI ...' world, host: levels on with EXP and build cost 40 %, its id noted; CISave after) / delete <name> (main menu: Raft's own LoadGameBox.DeleteSave on that world, a copy of its folder kept first) / check (in a new world with the same name: a new id, nothing of the deleted one - levels off, build cost Raft's)")]
		public static void WorldDelete(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "";
			if (what == "mark")
			{
				string name = SaveAndLoad.CurrentGameFileName;
				if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || name == null || !name.StartsWith("CI ")) { Fail("run in a 'CI ...' world, as the host"); return; }
				PlayerLevels.TurnOn(false);
				PlayerLevels.SetMine(new LevelRecord { Xp = 500 });
				BuildCost.Set(40);
				File.WriteAllText(WorldDeleteMarker, SaveAndLoad.WorldGuid + "|" + name);
				Log("PASS: world delete marked: '" + name + "' (" + SaveAndLoad.WorldGuid + "), levels on with 500 EXP, build cost " + BuildCost.Describe(40));
			}
			else if (what == "delete") StartTest(WorldDeleteRoutine(string.Join(" ", args.Skip(1).ToArray())));
			else if (what == "check")
			{
				if (!LoadSceneManager.IsGameSceneLoaded || !File.Exists(WorldDeleteMarker)) { Fail("run in the new world, after mark and delete"); return; }
				string[] m = File.ReadAllText(WorldDeleteMarker).Split('|');
				bool ok = true;
				string oldFile = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), m[0] + ".txt");
				Check(ref ok, SaveAndLoad.CurrentGameFileName == m[1], "a new world with the same name '" + SaveAndLoad.CurrentGameFileName + "'");
				Check(ref ok, SaveAndLoad.WorldGuid.ToString() != m[0], "it has its own id (" + SaveAndLoad.WorldGuid + ", the deleted one's " + m[0] + ")");
				Check(ref ok, IslandWorldState.WorldFilePath != oldFile, "its own world file, not the deleted one's (" + Path.GetFileName(IslandWorldState.WorldFilePath) + ")");
				// (a new world takes the New Game box's last choice for levels - world_rules.txt - so on is fine; the 500 EXP are not)
				int xp = PlayerLevels.On && PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp : 0;
				Check(ref ok, PlayerLevels.On == PlayerLevels.Default && xp == 0 && BuildCost.Current == 0, "nothing of the deleted world: levels " + (PlayerLevels.On ? "on (the New Game box's default)" : "off") + " with " + xp + " EXP, build cost " + BuildCost.Describe(BuildCost.Current));
				Log("  (the deleted world's own file " + (File.Exists(oldFile) ? "is left in the worlds folder, unused: no world has that id again" : "is gone") + ")");
				if (ok) Log("PASS: world delete check"); else Fail("world delete check");
			}
			else Fail("CIWorldDelete mark|delete <name>|check");
		}

		static IEnumerator WorldDeleteRoutine(string name)
		{
			LoadGameBox box = Resources.FindObjectsOfTypeAll<LoadGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (box == null || LoadSceneManager.IsGameSceneLoaded) { Fail("no Load Game box (go to the main menu first)"); yield break; }
			if (!name.StartsWith("CI ")) { Fail("only 'CI ...' test worlds"); yield break; }
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { } box.Open();
			float timeout = Time.realtimeSinceStartup + 180f;
			int count = -1;
			while (Time.realtimeSinceStartup < timeout)
			{
				yield return new WaitForSecondsRealtime(1f);
				int now = box.loadGameSelections != null ? box.loadGameSelections.Count : 0;
				if (now > 0 && now == count) break;
				count = now;
			}
			LoadGame_Selection pick = box.loadGameSelections == null ? null : box.loadGameSelections.FirstOrDefault(s => s.text_GameName != null && s.text_GameName.text == name);
			if (pick == null || pick.directoryInfo == null) { Fail("no saved world called '" + name + "'"); yield break; }
			string folder = pick.directoryInfo.FullName;
			string copy = Path.Combine(Path.Combine(Application.temporaryCachePath, "ci_deleted_worlds"), name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
			CopyFolder(folder, copy);
			HarmonyLib.Traverse.Create(box).Field("selectedGame").SetValue(pick);
			HarmonyLib.AccessTools.Method(typeof(LoadGameBox), "DeleteSave").Invoke(box, null);
			yield return null;
			bool gone = !Directory.Exists(folder) && !box.loadGameSelections.Contains(pick);
			try { box.Close(); } catch { }
			if (gone) Log("PASS: world deleted through Raft's Load Game box: '" + name + "' (a copy kept at " + copy + ")");
			else Fail("world delete: '" + name + "' still there (" + folder + ")");
		}

		static void CopyFolder(string from, string to)
		{
			Directory.CreateDirectory(to);
			foreach (string f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
			foreach (string d in Directory.GetDirectories(from)) CopyFolder(d, Path.Combine(to, Path.GetFileName(d)));
		}

		static string WorldCloudMarker { get { return Path.Combine(Application.temporaryCachePath, "ci_worldcloud.txt"); } }
		static string WorldCloudAway { get { return Path.Combine(DynamicIslands.assetpath, "deleted", "ci_uw6"); } }

		[ConsoleCommand(name: "CIWorldCloud", docs: "Dev, UW6 - Steam Cloud brings Raft's world folder to another PC but not the mod's worlds\\<id>.txt: CIWorldCloud mark <island> (in a 'CI ...' world, host, with <island> spawned: levels on with 500 EXP, build cost 40 %; CISave after) / strip (main menu: moves the world's mod file and <island>'s file to deleted\\ci_uw6) / check (the world loaded again: read from Raft's world folder, the levels and build cost kept, <island> named as missing; puts <island>'s file back)")]
		public static void WorldCloud(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "";
			if (what == "mark")
			{
				string name = SaveAndLoad.CurrentGameFileName, island = args.Length > 1 ? args[1] : "";
				if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || name == null || !name.StartsWith("CI ")) { Fail("run in a 'CI ...' world, as the host"); return; }
				if (!IslandWorldState.Islands.Any(e => e.HostName == island)) { Fail("world cloud: '" + island + "' isn't in this world (spawn it first)"); return; }
				PlayerLevels.TurnOn(false);
				PlayerLevels.SetMine(new LevelRecord { Xp = 500 });
				BuildCost.Set(40);
				File.WriteAllText(WorldCloudMarker, SaveAndLoad.WorldGuid + "|" + name + "|" + island);
				Log("PASS: world cloud marked: '" + name + "' (" + SaveAndLoad.WorldGuid + "), levels on with 500 EXP, build cost " + BuildCost.Describe(40) + ", island " + island);
			}
			else if (what == "strip")
			{
				if (LoadSceneManager.IsGameSceneLoaded || !File.Exists(WorldCloudMarker)) { Fail("world cloud strip: at the main menu, after mark"); return; }
				string[] m = File.ReadAllText(WorldCloudMarker).Split('|');
				string worldFile = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), m[0] + ".txt"), islandFile = IslandSpawner.PathFor(m[2]);
				if (!File.Exists(worldFile) || !File.Exists(islandFile)) { Fail("world cloud strip: " + (File.Exists(worldFile) ? islandFile : worldFile) + " isn't there"); return; }
				Directory.CreateDirectory(WorldCloudAway);
				string stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
				File.Move(worldFile, Path.Combine(WorldCloudAway, m[0] + " " + stamp + ".txt"));
				string keep = Path.Combine(WorldCloudAway, Path.GetFileName(islandFile));
				if (File.Exists(keep)) File.Move(keep, keep + "." + stamp);
				File.Move(islandFile, keep);
				IslandCache.Forget();
				Log("PASS: world cloud stripped: the world's mod file and '" + m[2] + "' moved to " + WorldCloudAway);
			}
			else if (what == "check")
			{
				if (!LoadSceneManager.IsGameSceneLoaded || !File.Exists(WorldCloudMarker)) { Fail("world cloud check: in the world, after mark and strip"); return; }
				string[] m = File.ReadAllText(WorldCloudMarker).Split('|');
				bool ok = true;
				string keep = Path.Combine(WorldCloudAway, Path.GetFileName(IslandSpawner.PathFor(m[2])));
				try
				{
					Check(ref ok, SaveAndLoad.CurrentGameFileName == m[1] && SaveAndLoad.WorldGuid.ToString() == m[0], "the same world '" + SaveAndLoad.CurrentGameFileName + "'");
					Check(ref ok, (WorldCopy.LastSource ?? "").StartsWith("Raft's world folder"), "read from Raft's world folder (" + WorldCopy.LastSource + ")");
					int xp = PlayerLevels.On && PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp : 0;
					Check(ref ok, PlayerLevels.On && xp == 500 && BuildCost.Current == 40, "its state came along: levels " + (PlayerLevels.On ? "on" : "off") + " with " + xp + " EXP, build cost " + BuildCost.Describe(BuildCost.Current));
					IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == m[2]);
					// (waiting for its file is set together with the host's notice naming the island - IslandWorldState.Load)
					Check(ref ok, e != null && (e.WaitingForFile || e.Failed) && e.Root == null, "'" + m[2] + "', with no file here, is kept in the world, left out and named to the host" + (e == null ? " (not in the world)" : e.WaitingForFile ? " (waits for its file)" : e.Failed ? " (failed)" : " (spawned?)"));
				}
				catch (Exception ex) { Check(ref ok, false, "no errors: " + ex.Message); }
				finally { try { if (File.Exists(keep) && !File.Exists(IslandSpawner.PathFor(m[2]))) File.Move(keep, IslandSpawner.PathFor(m[2])); IslandCache.Forget(); } catch { } }
				if (ok) Log("PASS: world cloud check"); else Fail("world cloud check");
			}
			else Fail("CIWorldCloud mark <island>|strip|check");
		}

		static string OldSaveMarker { get { return Path.Combine(Application.temporaryCachePath, "ci_oldsave.txt"); } }

		[ConsoleCommand(name: "CIOldSave", docs: "Dev, IX2 - a world saved by an older build (only island lines in its file, no copies in Raft's folders): CIOldSave mark <island> (in a 'CI ...' world, host, <island> spawned: levels, build cost, the randomizer and an option on; CISave after) / strip (main menu: the world's file cut to the old island lines, Raft's folder copies moved to deleted\\ci_ix2) / check (loaded again: everything off, the islands kept)")]
		public static void OldSave(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "";
			if (what == "mark")
			{
				string name = SaveAndLoad.CurrentGameFileName, island = args.Length > 1 ? args[1] : "", folder = WorldCopy.RaftWorldFolder;
				if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || name == null || !name.StartsWith("CI ") || folder == null) { Fail("run in a saved 'CI ...' world, as the host"); return; }
				if (!IslandWorldState.Islands.Any(e => e.HostName == island)) { Fail("old save: '" + island + "' isn't in this world (spawn it first)"); return; }
				PlayerLevels.TurnOn(false);
				PlayerLevels.SetMine(new LevelRecord { Xp = 500 });
				BuildCost.Set(40);
				WorldRandomizer.Set(new RandomizerSettings { Level = RandomizerSettings.Wild, Seed = 7 });
				WorldOptions.Set(new HashSet<string>(WorldOptions.Current) { WorldOptions.StormDays });
				File.WriteAllText(OldSaveMarker, SaveAndLoad.WorldGuid + "|" + name + "|" + island + "|" + IslandWorldState.Islands.Count + "|" + folder);
				Log("PASS: old save marked: '" + name + "' with " + IslandWorldState.Islands.Count + " island(s), levels, build cost, the randomizer (Wild) and storm days on");
			}
			else if (what == "strip")
			{
				if (LoadSceneManager.IsGameSceneLoaded || !File.Exists(OldSaveMarker)) { Fail("old save strip: at the main menu, after mark"); return; }
				string[] m = File.ReadAllText(OldSaveMarker).Split('|');
				string worldFile = Path.Combine(Path.Combine(DynamicIslands.assetpath, "worlds"), m[0] + ".txt");
				if (!File.Exists(worldFile)) { Fail("old save strip: " + worldFile + " isn't there"); return; }
				string away = Path.Combine(DynamicIslands.assetpath, "deleted", "ci_ix2", DateTime.Now.ToString("yyyyMMddHHmmss"));
				Directory.CreateDirectory(away);
				File.Copy(worldFile, Path.Combine(away, m[0] + ".txt"));
				// (the oldest format: a comment and one line per island - name|x|y|z|state - nothing else)
				string[] lines = File.ReadAllLines(worldFile);
				var old = lines.Where(l => l.StartsWith("#")).Take(1).Concat(lines.Where(l => l.Length > 0 && !l.StartsWith("#") && !l.StartsWith("@")).Select(l => string.Join("|", l.Split('|').Take(5).ToArray()))).ToArray();
				File.WriteAllLines(worldFile, old);
				int moved = 0;
				foreach (string dir in new[] { m[4] }.Concat(Directory.Exists(m[4]) ? Directory.GetDirectories(m[4]) : new string[0]))
				{
					string f = Path.Combine(dir, WorldCopy.FileName);
					if (!File.Exists(f)) continue;
					File.Move(f, Path.Combine(away, (dir == m[4] ? "world" : Path.GetFileName(dir)) + "_" + WorldCopy.FileName));
					moved++;
				}
				Log("PASS: old save stripped: the world's file cut to " + (old.Length - 1) + " island line(s) (" + lines.Length + " lines before), " + moved + " copies in Raft's folders moved to " + away);
			}
			else if (what == "check")
			{
				if (!LoadSceneManager.IsGameSceneLoaded || !File.Exists(OldSaveMarker)) { Fail("old save check: in the world, after mark and strip"); return; }
				string[] m = File.ReadAllText(OldSaveMarker).Split('|');
				bool ok = true;
				try
				{
					Check(ref ok, SaveAndLoad.CurrentGameFileName == m[1] && SaveAndLoad.WorldGuid.ToString() == m[0], "the same world '" + SaveAndLoad.CurrentGameFileName + "'");
					Check(ref ok, WorldCopy.LastSource == "mod folder", "read from the mod's (old) file (" + WorldCopy.LastSource + ")");
					Check(ref ok, !PlayerLevels.On && BuildCost.Current == 0 && !WorldRandomizer.Current.On && !WorldOptions.On(WorldOptions.StormDays),
						"everything off: levels " + (PlayerLevels.On ? "on" : "off") + ", build cost " + BuildCost.Describe(BuildCost.Current) + ", randomizer " + (WorldRandomizer.Current.On ? "on" : "off") + ", storm days " + (WorldOptions.On(WorldOptions.StormDays) ? "on" : "off"));
					IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == m[2]);
					Check(ref ok, IslandWorldState.Islands.Count.ToString() == m[3] && e != null && !e.Failed && !e.WaitingForFile, "nothing lost: " + IslandWorldState.Islands.Count + " island(s) (" + m[3] + " before), '" + m[2] + "' " + (e == null ? "missing" : e.Failed ? "failed" : e.Root != null ? "there" : "kept"));
				}
				catch (Exception ex) { Check(ref ok, false, "no errors: " + ex.Message); }
				if (ok) Log("PASS: old save check"); else Fail("old save check");
			}
			else Fail("CIOldSave mark <island>|strip|check");
		}

		#region IX3: features on at once

		static string ComboMarker { get { return Path.Combine(Application.temporaryCachePath, "ci_combo.txt"); } }
		static readonly System.Globalization.CultureInfo ComboCulture = System.Globalization.CultureInfo.InvariantCulture;

		[ConsoleCommand(name: "CIComboWorld", docs: "Dev, IX3 - the randomizer (Wild), Nightmare, the level up system and build cost 50 % on at once: CIComboWorld set (a 'CI ...' world, host, a live warthog near - CIRandomizerLarge keep: two of Raft's own warthogs, a seed that makes one an alpha; it has x3 health once and is worth more EXP, a Nightmare hit gives its share, a recipe costs +50 %; CISave after) / check (loaded again: the same alpha's health and EXP, the same recipe cost - nothing applied twice) / client (a player who joined: the same build cost, recipe cost and alpha health)")]
		public static void ComboWorldCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "";
			if (what == "set") StartTest(ComboSetRoutine());
			else if (what == "check") StartTest(ComboCheckRoutine(false));
			else if (what == "client") StartTest(ComboCheckRoutine(true));
			else Fail("CIComboWorld set|check|client");
		}

		static IEnumerator ComboSetRoutine()
		{
			string name = SaveAndLoad.CurrentGameFileName;
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host_Entities ents = ComponentManager<Network_Host_Entities>.Value;
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || name == null || !name.StartsWith("CI ") || player == null || ents == null || host == null) { Fail("run in a 'CI ...' world, as the host"); yield break; }
			yield return EnsureAlive();
			AI_NetworkBehaviour near = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(x => x != null && x.behaviourType == AI_NetworkBehaviourType.Boar && x.networkEntity != null && !x.networkEntity.IsDead)
				.OrderBy(x => (x.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
			if (near == null) { Fail("combo world: no live warthog in the world (run CIRandomizerLarge keep first)"); yield break; }

			// Warthogs of Raft's own (made by Raft's spawning, not a custom island's: those are never alphas) next to it
			Vector3 land = near.transform.position;
			var made = new List<AI_NetworkBehaviour>();
			for (int i = 0; i < 3; i++)
			{
				AI_NetworkBehaviour ai = null;
				try { ai = ents.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Boar, land + new Vector3(2.5f + i * 2.5f, 1.5f, 2f), null); }
				catch (Exception e) { Log("  warthog not made: " + (e.InnerException ?? e).Message); }
				if (ai != null) made.Add(ai);
			}
			for (int i = 0; i < 4; i++) { KeepAlive(player); yield return new WaitForSeconds(0.5f); }
			// (two that no earlier seed made an alpha: their health is still Raft's)
			var free = made.Where(x => x != null && x.ObjectIndex != 0 && x.networkEntity != null && x.networkEntity.stat_health != null && !x.networkEntity.IsDead &&
				!CreatureSpawner.IsOnCustomIsland(x) && !(WorldRandomizer.VariantOfIndex.ContainsKey(x.ObjectIndex) && WorldRandomizer.VariantOfIndex[x.ObjectIndex] == "alpha")).ToList();
			if (free.Count < 2) { Fail("combo world: Raft made " + made.Count + " warthog(s), " + free.Count + " usable"); yield break; }
			AI_NetworkBehaviour a = free[0], b = free[1];
			float baseMax = b.networkEntity.stat_health.Max;
			// (its worth before it is an alpha: asked first, it is remembered - an alpha made later must not keep it)
			int xpBefore = PlayerLevels.MonsterXp(a);

			RandomizerSettings prev = WorldRandomizer.Current;
			var s = new RandomizerSettings { Level = RandomizerSettings.Wild };
			int seed = 0;
			for (int tryseed = 1; tryseed < 200000 && seed == 0; tryseed++)
			{
				s.Seed = tryseed;
				WorldRandomizer.Current = s;
				WorldRandomizer.Variant va = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Boar, a.ObjectIndex), vb = WorldRandomizer.VariantOf(AI_NetworkBehaviourType.Boar, b.ObjectIndex);
				if (va != null && va.Alpha && (vb == null || !vb.Alpha)) seed = tryseed;
			}
			WorldRandomizer.Current = prev;
			if (seed == 0) { Fail("combo world: no seed makes #" + a.ObjectIndex + " an alpha and #" + b.ObjectIndex + " not"); yield break; }

			// Everything on at once
			MonsterDifficulty.Set(MonsterDifficulty.Nightmare);
			PlayerLevels.TurnOn(false);
			PlayerLevels.SetMine(new LevelRecord());
			BuildCost.Set(50);
			WorldRandomizer.Set(s);
			for (float t = 0; t < 10f && !(WorldRandomizer.VariantOfIndex.ContainsKey(a.ObjectIndex) && WorldRandomizer.VariantOfIndex[a.ObjectIndex] == "alpha"); t += 0.5f) { KeepAlive(player); yield return new WaitForSeconds(0.5f); }
			for (float t0 = Time.realtimeSinceStartup; BuildCost.Applied != 50 && Time.realtimeSinceStartup - t0 < 10f; ) yield return new WaitForSeconds(0.25f);
			yield return new WaitForSeconds(1f);
			KeepAlive(player);

			bool ok = true;
			if (a == null || b == null || a.networkEntity == null || b.networkEntity == null) { Fail("combo world: the warthogs are gone"); yield break; }
			float aMax = a.networkEntity.stat_health.Max, bMax = b.networkEntity.stat_health.Max;
			bool isAlpha = WorldRandomizer.VariantOfIndex.ContainsKey(a.ObjectIndex) && WorldRandomizer.VariantOfIndex[a.ObjectIndex] == "alpha";
			Check(ref ok, isAlpha && Mathf.Abs(aMax - 3f * baseMax) <= baseMax * 0.02f + 1f && Mathf.Abs(bMax - baseMax) <= 0.5f,
				"seed " + seed + " (Wild): #" + a.ObjectIndex + " is an alpha with " + aMax.ToString("F0") + " health, x3 once (Raft's " + baseMax.ToString("F0") + "); #" + b.ObjectIndex + " " + bMax.ToString("F0"));
			Check(ref ok, MonsterDifficulty.Current == MonsterDifficulty.Nightmare && PlayerLevels.On && BuildCost.Current == 50 && BuildCost.Applied == 50,
				"Nightmare, levels on, build cost " + BuildCost.Describe(BuildCost.Current) + " (on Raft's numbers " + BuildCost.Describe(BuildCost.Applied) + ")");
			float dA = PlayerLevels.DamageOf(a.gameObject, a.behaviourType), dB = PlayerLevels.DamageOf(b.gameObject, b.behaviourType);
			Check(ref ok, dB > 0f && dA >= dB * 0.99f && dA <= dB * 1.65f, "its damage x" + (dB > 0f ? (dA / dB).ToString("F2") : "?") + " (" + dA.ToString("F1") + " vs " + dB.ToString("F1") + "; x1.6 at most, once)");
			int aXp = PlayerLevels.MonsterXp(a), bXp = PlayerLevels.MonsterXp(b), fresh = LevelRules.XpOf(aMax, dA);
			Check(ref ok, aXp == fresh && aXp > xpBefore, "the alpha's worth worked out again from its new health: " + aXp + " EXP (from " + aMax.ToString("F0") + " health, " + dA.ToString("F1") + " damage: " + fresh + "; " + xpBefore + " before)");
			Check(ref ok, aXp > bXp && bXp == LevelRules.XpOf(bMax, dB), "the alpha is worth more: " + aXp + " EXP vs " + bXp + " (Nightmare doesn't change the worth)");

			// A hit at Nightmare: the monster loses its share (half), the EXP is that share of its worth
			float pve = 1f;
			SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
			if (mode != null && mode.playerSpecificVariables != null) pve = mode.playerSpecificVariables.negateOutgoingPlayerDamage ? 0f : mode.playerSpecificVariables.outgoingDamageMultiplierPVE;
			if (pve <= 0f) Log("  note: this game mode takes away players' damage - no hit");
			else
			{
				float factor = MonsterDifficulty.Factor * NightDanger.MonsterFactor, dealt = aMax * 0.2f;
				float v0 = a.networkEntity.stat_health.Value;
				int xp0 = PlayerLevels.Mine.Xp;
				host.DamageEntity(a.networkEntity, a.transform, dealt / pve, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
				float lost = v0 - a.networkEntity.stat_health.Value;
				int gain = PlayerLevels.Mine.Xp - xp0, want = Mathf.RoundToInt(lost / aMax * aXp);
				Check(ref ok, Mathf.Abs(lost - dealt / factor) <= 1f && Mathf.Abs(gain - want) <= 1 && gain > 0,
					"a hit of " + dealt.ToString("F0") + " at Nightmare (x" + factor.ToString("F2") + ") takes " + lost.ToString("F1") + " health and gives " + gain + " EXP (its share: " + want + ")");
				KeepAlive(player);
			}

			// A recipe of the build menu: Raft's amount +50 %
			Item_Base item = null;
			int entry = -1;
			foreach (Item_Base i in BuildCost.Items.Where(x => x != null && x.settings_recipe != null && x.settings_recipe.NewCost != null).OrderBy(x => x.UniqueName))
			{
				List<CostMultiple> cs = i.settings_recipe.NewCost.ToList();
				int k = cs.FindIndex(c => c != null && c.items != null && c.items.Length > 0 && c.items[0] != null && BuildCost.OriginalOf(c) >= 3);
				if (k >= 0) { item = i; entry = k; break; }
			}
			CostMultiple cost = item != null ? item.settings_recipe.NewCost.ToList()[entry] : null;
			int orig = cost != null ? BuildCost.OriginalOf(cost) : 0, amount = cost != null ? cost.amount : 0;
			Check(ref ok, cost != null && amount == BuildCost.Cost(orig, 50) && amount > orig,
				cost == null ? "a build menu recipe with 3 or more of a material (none)" : item.UniqueName + " costs " + amount + " " + cost.items[0].UniqueName + " (Raft " + orig + ", +50 %: " + BuildCost.Cost(orig, 50) + ")");

			File.WriteAllText(ComboMarker, string.Join("|", new[] {
				SaveAndLoad.WorldGuid.ToString(), name, seed.ToString(ComboCulture),
				a.ObjectIndex.ToString(ComboCulture), aMax.ToString("R", ComboCulture), aXp.ToString(ComboCulture),
				b.ObjectIndex.ToString(ComboCulture), bMax.ToString("R", ComboCulture), bXp.ToString(ComboCulture),
				item != null ? item.UniqueName : "", entry.ToString(ComboCulture), orig.ToString(ComboCulture), amount.ToString(ComboCulture), dA.ToString("R", ComboCulture) }));
			if (ok) Log("PASS: combo world set"); else Fail("combo world set");
		}

		/// <summary>IX3 after a save and load (host) or a join (client): the same alpha, worth and recipe cost - nothing twice.</summary>
		static IEnumerator ComboCheckRoutine(bool client)
		{
			string label = client ? "combo world client" : "combo world check";
			if (!LoadSceneManager.IsGameSceneLoaded || !File.Exists(ComboMarker) || client == Raft_Network.IsHost) { Fail(label + ": in the world " + (client ? "as a player who joined" : "as the host") + ", after CIComboWorld set"); yield break; }
			string[] m = File.ReadAllText(ComboMarker).Split('|');
			if (m.Length < 14) { Fail(label + ": the marker " + ComboMarker + " is from another build"); yield break; }
			int seed = int.Parse(m[2], ComboCulture), aXp = int.Parse(m[5], ComboCulture), bXp = int.Parse(m[8], ComboCulture);
			int entry = int.Parse(m[10], ComboCulture), orig = int.Parse(m[11], ComboCulture), amount = int.Parse(m[12], ComboCulture);
			uint aIdx = uint.Parse(m[3], ComboCulture), bIdx = uint.Parse(m[6], ComboCulture);
			float aMax = float.Parse(m[4], ComboCulture), bMax = float.Parse(m[7], ComboCulture), dA = float.Parse(m[13], ComboCulture);
			Network_Player player = RAPI.GetLocalPlayer();
			bool ok = true;
			if (!client) Check(ref ok, SaveAndLoad.WorldGuid.ToString() == m[0] && SaveAndLoad.CurrentGameFileName == m[1], "the same world '" + SaveAndLoad.CurrentGameFileName + "'");

			// (a player gets the settings from the host a moment after joining)
			for (float t0 = Time.realtimeSinceStartup; (BuildCost.Applied != 50 || !PlayerLevels.On || WorldRandomizer.Current.Seed != seed) && Time.realtimeSinceStartup - t0 < 20f; ) yield return new WaitForSeconds(0.25f);
			Check(ref ok, WorldRandomizer.Current.Level == RandomizerSettings.Wild && WorldRandomizer.Current.Seed == seed && MonsterDifficulty.Current == MonsterDifficulty.Nightmare && PlayerLevels.On && BuildCost.Current == 50 && BuildCost.Applied == 50,
				"still on: randomizer " + WorldRandomizer.Current.LevelName + " seed " + WorldRandomizer.Current.Seed + " (" + seed + "), monsters " + MonsterDifficulty.Names[MonsterDifficulty.Clamp(MonsterDifficulty.Current)] +
				", levels " + (PlayerLevels.On ? "on" : "off") + ", build cost " + BuildCost.Describe(BuildCost.Current) + " (on Raft's numbers " + BuildCost.Describe(BuildCost.Applied) + ")");

			// The alpha: the same animal if Raft brought it back with its index (it looks at the animals every half second)
			Func<uint, AI_NetworkBehaviour> find = idx => UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.ObjectIndex == idx && x.networkEntity != null && x.networkEntity.stat_health != null && !x.networkEntity.IsDead);
			Func<uint, bool> alpha = idx => WorldRandomizer.VariantOfIndex.ContainsKey(idx) && WorldRandomizer.VariantOfIndex[idx] == "alpha";
			for (float t = 0; t < 60f && !(find(aIdx) != null && alpha(aIdx)); t += 0.5f) { KeepAlive(player); yield return new WaitForSeconds(0.5f); }
			// (time for anything applied a second time to show)
			for (int i = 0; i < 6; i++) { KeepAlive(player); yield return new WaitForSeconds(0.5f); }
			AI_NetworkBehaviour a = find(aIdx);
			string which = "#" + aIdx;
			if (a == null || !alpha(aIdx))
			{
				// (Raft gave it another index: any alpha warthog of Raft's own has the same health)
				a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.behaviourType == AI_NetworkBehaviourType.Boar && x.networkEntity != null && x.networkEntity.stat_health != null && !x.networkEntity.IsDead && alpha(x.ObjectIndex) && !CreatureSpawner.IsOnCustomIsland(x));
				if (a != null) which = "#" + a.ObjectIndex + " (another alpha warthog: #" + aIdx + " " + (find(aIdx) == null ? "not here" : "not an alpha") + ")";
			}
			if (a == null) Log("  note: alpha #" + aIdx + " isn't here (Raft didn't bring it back) and no other alpha warthog is - its health and EXP are skipped");
			else
			{
				float max = a.networkEntity.stat_health.Max;
				Check(ref ok, Mathf.Abs(max - aMax) <= 1f, "alpha " + which + ": " + max.ToString("F0") + " health as before (" + aMax.ToString("F0") + "), not x3 again");
				if (!client)
				{
					float d = PlayerLevels.DamageOf(a.gameObject, a.behaviourType);
					int xp = PlayerLevels.MonsterXp(a);
					Check(ref ok, Mathf.Abs(d - dA) <= dA * 0.01f + 0.01f && xp == aXp, "its damage " + d.ToString("F1") + " (" + dA.ToString("F1") + " before) and worth " + xp + " EXP (" + aXp + " before)");
				}
			}
			AI_NetworkBehaviour b = find(bIdx);
			if (b != null && !alpha(bIdx))
			{
				float max = b.networkEntity.stat_health.Max;
				bool same = Mathf.Abs(max - bMax) <= 1f && (client || PlayerLevels.MonsterXp(b) == bXp);
				Check(ref ok, same, "the other warthog #" + bIdx + ": " + max.ToString("F0") + " health (" + bMax.ToString("F0") + " before)" + (client ? "" : ", worth " + PlayerLevels.MonsterXp(b) + " EXP (" + bXp + ")"));
			}
			else Log("  note: warthog #" + bIdx + " isn't here as before - skipped");

			// The recipe: Raft's amount +50 %, once
			Item_Base item = m[9].Length > 0 ? ItemManager.GetItemByName(m[9]) : null;
			List<CostMultiple> cs = item != null && item.settings_recipe != null && item.settings_recipe.NewCost != null ? item.settings_recipe.NewCost.ToList() : new List<CostMultiple>();
			CostMultiple cost = entry >= 0 && entry < cs.Count ? cs[entry] : null;
			Check(ref ok, cost != null && cost.amount == amount && BuildCost.OriginalOf(cost) == orig && cost.amount == BuildCost.Cost(orig, 50),
				cost == null ? "the recipe " + m[9] + " (not found)" : m[9] + " costs " + cost.amount + " (" + amount + " before, Raft " + BuildCost.OriginalOf(cost) + ", +50 %: " + BuildCost.Cost(orig, 50) + ")");
			if (ok) Log("PASS: " + label); else Fail(label);
		}

		#endregion
	}
}
