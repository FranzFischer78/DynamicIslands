using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// The whole-adventure scenarios (TEST_PROTOCOL §5j SC60-SC62): "CI Voyage", four islands built from nothing as a
	/// builder builds them (the generator, then content), a plan with every When and every Where, Check, Test in a world,
	/// Export; then played to its end in a world (alone here; two players and two host swaps in tools\mpadventure.ps1).
	/// </summary>
	public static partial class DevTests
	{
		public const string VoyagePlan = "CI Voyage", VoyageLanding = "civoy-landing", VoyageCove = "civoy-cove", VoyageVault = "civoy-vault", VoyageSummit = "civoy-summit", VoyagePackId = "ci-voyage";
		static readonly string[] VoyageIslands = { VoyageLanding, VoyageCove, VoyageVault, VoyageSummit };

		/// <summary>The voyage's plan: every When (start, km, day, quest, step, zone, visit, after rule, signal) and every Where
		/// (ahead, near, on the Receiver, by chance while sailing). The cove comes by the landing's own rule (id "cove").</summary>
		public static string VoyagePlanText()
		{
			return "description = A test voyage: four islands built from nothing, every kind of rule\nrandom = off\n" +
				"rule = landing | island:" + VoyageLanding + " | start | ahead:400 | A voyage begins. | Landing Beach\n" +
				"rule = wreck | type:wreck | km:1 | ahead:600 | A wreck drifts ahead. | Wreck\n" +
				"rule = luck | type:treasure | day:2 | sailing:600 | | Lucky find\n" +
				"rule = sandbar | type:sandbar | step:landing:3 | near:landing:500:east | | Sandbar\n" +
				"rule = dock | type:oddity | zone:cove:dock | near:cove:500:west | | Oddity\n" +
				"rule = sunk | type:sunken | visit:cove | near:cove:500:south | | Sunken reef\n" +
				"rule = vault | island:" + VoyageVault + " | signal:cove:gate | receiver:600 | A signal from the vault. | The Vault\n" +
				"rule = summit | island:" + VoyageSummit + " | quest:vault | near:vault:600:north | The summit calls. | Summit Isle\n" +
				"rule = sky | type:sky | rule:summit | ahead:900 | | Sky island\n";
		}

		/// <summary>Builds the voyage's four islands with the generator and puts their content on them (as the editor's
		/// object list and inspector do: the same object names and settings).</summary>
		static void BuildVoyageIslands()
		{
			// Landing Beach: a camp zone, the captain's log (a map piece), supplies, two warthogs, two chickens; a quest with
			// every step that can be done here; an arrive event (a journal page), a quest event, and its own rule (the cove)
			{
				IslandFile f = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 5101, Style = TerrainPainter.Tropical, Shape = IslandShapes.Round, Radius = 60f, Height = 16f, ObjectDensity = 0.5f }, VoyageLanding);
				var k = new MapKit(f, 51);
				Vector2 c = k.Mid;
				Func<Vector2, Vector2> at = o => k.Find(c + o, 20f, MapKit.Dry, 7f) ?? (c + o);
				f.Props[IslandProps.Title] = "Landing Beach";
				f.Props[StoryItems.Key] = "civoy-map|Map piece||A torn piece of a sea chart\ncivoy-key|Brass key||Opens the vault\ncivoy-crown|Old crown||The vault's treasure";
				k.Zone(at(new Vector2(0, 0)), "camp", 5f, "You found the camp");
				IslandObject log = k.Note("Note_Book", at(new Vector2(12, 0)), "Captain's log", "Day 40. The chart is torn in two. One half is here, the other went with the smugglers.");
				log.Props[BehaviourProps.EventPrefix + "read"] = "give||story:civoy-map*1";
				IslandObject sup = k.Chest("Loot_Chest", at(new Vector2(-12, 0)), "Supplies", "Plank*5;Rope*2");
				sup.Props[ObjectProps.LootRefill] = "0";
				k.Creature("Boar", at(new Vector2(0, 18)), 2, "Normal", 1f, null, false);
				k.Creature("Chicken", at(new Vector2(0, -18)), 2, "Normal", 1f, null, false);
				k.Quest("The landing", "Make the landing safe.", "The landing is safe. The chart points north.", "Nail*5",
					"reach|camp|1|Find the camp", "read|Captain's log|1|Read the captain's log", "open|Supplies|1|Open the supplies",
					"kill|Warthog|2|Chase off the warthogs", "catch|Chicken|1|Catch a chicken");
				f.Props[BehaviourProps.EventPrefix + "arrive"] = "journal|The landing|We reached the landing beach.";
				f.Props[BehaviourProps.EventPrefix + "quest"] = "message||The chart points north to a cove";
				WorldDirector.SetRulesInProps(f.Props, new List<IntroRule> { IntroRule.Parse("cove | island:" + VoyageCove + " | quest:self | near:self:600:north | The cove lies north. | Smugglers' Cove") });
				f.Save(IslandSpawner.PathFor(VoyageLanding));
			}
			// Smugglers' Cove: a dock zone, three crates (one with the second map piece, one with the key), a note whose wait
			// shows a hidden marker, a lever that sends the signal "gate" and swings a door; quest: 3 chests, 2 map pieces
			{
				IslandFile f = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 5102, Style = TerrainPainter.Forest, Shape = IslandShapes.Round, Radius = 55f, Height = 14f, ObjectDensity = 0.5f }, VoyageCove);
				var k = new MapKit(f, 52);
				Vector2 c = k.Mid;
				Func<Vector2, Vector2> at = o => k.Find(c + o, 20f, MapKit.Dry, 7f) ?? (c + o);
				f.Props[IslandProps.Title] = "Smugglers' Cove";
				k.Zone(at(new Vector2(0, 0)), "dock", 5f, "The smugglers' dock");
				for (int i = 1; i <= 3; i++)
				{
					string loot = "Rope*1" + (i == 2 ? ";story:civoy-map*1" : i == 3 ? ";story:civoy-key*1" : "");
					IslandObject crate = k.Chest("Loot_Crate", at(new Vector2(-16 + i * 8, 14)), "Crate " + i, loot);
					crate.Props[ObjectProps.LootRefill] = "0";
				}
				IslandObject note = k.Note("Note_Paper", at(new Vector2(14, -6)), "Smuggler's note", "Pull the lever and the gate answers. Mind the marker.");
				note.Props[BehaviourProps.EventPrefix + "read"] = "wait||5\nshow|marker|";
				k.Add("Note_Sign", k.At(at(new Vector2(18, -14))), 0f, P(BehaviourProps.Name, "marker", BehaviourProps.Hidden, "1"));
				k.Add("Note_Sign", k.At(at(new Vector2(-14, -8))), 0f, P(BehaviourProps.Name, "lever", BehaviourProps.Use, "Pull", BehaviourProps.EventPrefix + "use", "signal||gate\nswitch|door|"));
				k.Add(ContentCatalog.HelperWall, k.At(at(new Vector2(-20, -16))), 0f, P(BehaviourProps.Name, "door", BehaviourProps.Move, "0,4,0", BehaviourProps.MoveMode, "switch"));
				k.Quest("Smugglers' cove", "The smugglers hid their loot here.", "The cove gave up its secrets.", "Scrap*3",
					"open||3|Open the smugglers' crates", "collect|story:civoy-map|2|Find both map pieces");
				f.Save(IslandSpawner.PathFor(VoyageCove));
			}
			// The Vault: a zone, a door that takes the key, the treasure, a ledger; quest: reach, open the treasure, 3 pages
			{
				IslandFile f = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 5103, Style = TerrainPainter.Desert, Shape = IslandShapes.Round, Radius = 50f, Height = 18f, ObjectDensity = 0.4f }, VoyageVault);
				var k = new MapKit(f, 53);
				Vector2 c = k.Mid;
				Func<Vector2, Vector2> at = o => k.Find(c + o, 20f, MapKit.Dry, 7f) ?? (c + o);
				f.Props[IslandProps.Title] = "The Vault";
				k.Zone(at(new Vector2(0, 0)), "vault", 5f, "The vault");
				k.Add("Note_Sign", k.At(at(new Vector2(10, 8))), 0f, P(BehaviourProps.Name, "vaultdoor", BehaviourProps.Use, "Unlock",
					BehaviourProps.CheckPrefix + "use", "take|story:civoy-key|1", BehaviourProps.EventPrefix + "use", "message||The vault opens", BehaviourProps.ElsePrefix + "use", "message||It is locked"));
				IslandObject t = k.Chest("Loot_Chest", at(new Vector2(-10, 8)), "Treasure", "MetalIngot*3;story:civoy-crown*1");
				t.Props[ObjectProps.LootRefill] = "0";
				k.Note("Note_Book", at(new Vector2(0, -12)), "Vault ledger", "Everything the smugglers took is written here.");
				k.Quest("The vault", "Open the smugglers' vault.", "The treasure is yours.", "MetalIngot*2",
					"reach|vault|1|Find the vault", "open|Treasure|1|Open the treasure", "pages|all|3|Collect three journal pages");
				f.Save(IslandSpawner.PathFor(VoyageVault));
			}
			// Summit Isle: the last note; its quest event writes the voyage's last page
			{
				IslandFile f = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 5104, Style = TerrainPainter.Snowy, Shape = IslandShapes.Round, Radius = 45f, Height = 22f, ObjectDensity = 0.4f }, VoyageSummit);
				var k = new MapKit(f, 54);
				Vector2 c = k.Mid;
				f.Props[IslandProps.Title] = "Summit Isle";
				k.Note("Note_Sign", k.Find(c, 20f, MapKit.Dry, 7f) ?? c, "Summit stone", "Here the voyage ends.");
				k.Quest("The summit", "Climb to the summit stone.", "The voyage is complete.", "", "read|Summit stone|1|Read the summit stone");
				f.Props[BehaviourProps.EventPrefix + "quest"] = "journal|The end|The voyage is complete.";
				f.Save(IslandSpawner.PathFor(VoyageSummit));
			}
			IslandCache.Forget();
		}

		[ConsoleCommand(name: "CIAdventureBuild", docs: "Dev, editor: SC60 - the voyage 'CI Voyage' built from nothing: four islands made with the generator and given their content (chests, notes, zones, creatures, a door and a lever, story items, a quest with every step type, island events, an island rule); the plan with every When and Where; Check; Test in a world for each island; exported as a pack. CIAdventureBuild notest = without the Tests (minutes)")]
		public static void AdventureBuildCommand(string[] args) { StartTest(AdventureBuildRoutine(args == null || !args.Contains("notest"))); }

		static IEnumerator AdventureBuildRoutine(bool test)
		{
			yield return WaitForEditor(false);
			yield return PlaceableCatalog.EnsureBuilt();
			bool ok = true;
			// (the voyage's pack installed here by an earlier fetch goes first: removed later, it took the islands and the
			// plan just built under the same names to the deleted folder)
			try { LibraryPack.Remove(VoyagePackId); } catch { }
			try { BuildVoyageIslands(); }
			catch (Exception ex) { Fail("adventure build: " + ex); yield break; }
			foreach (string n in VoyageIslands)
			{
				IslandFile f = null;
				try { f = IslandFile.Load(IslandSpawner.PathFor(n)); } catch { }
				Check(ref ok, f != null && f.Objects.Count > 0 && IslandSpawner.LandRadius(f) > 10f, "'" + n + "' built and saved (" + (f != null ? f.Objects.Count + " objects, land " + IslandSpawner.LandRadius(f).ToString("F0") + " m" : "unreadable") + ")");
				// (a world leaves out what the mod doesn't know, with only a log line: "Creature_Creature_Boar" once made a
				// landing without its warthogs, found only when the play test waited for them)
				if (f == null) continue;
				string[] unknown = f.Objects.Select(o => o.Name).Where(x => !ContentCatalog.IsCreature(x) && !ContentCatalog.IsZone(x) && !ContentCatalog.IsHelper(x) && PlaceableCatalog.Get(x) == null).Distinct().ToArray();
				Check(ref ok, unknown.Length == 0, "... every object of '" + n + "' is one the mod knows" + (unknown.Length > 0 ? " - not: " + string.Join(", ", unknown) : ""));
			}
			IslandQuest lq = IslandQuest.From(IslandFile.Load(IslandSpawner.PathFor(VoyageLanding)).Props);
			Check(ref ok, lq.Steps.Select(s => s.Type).Distinct().Count() == 5, "the landing's quest has reach, read, open, kill and catch steps; the cove's collect, the vault's pages");
			WorldPlan plan = WorldPlan.Parse(VoyagePlan, VoyagePlanText());
			plan.Save();
			WorldPlan back = WorldPlan.Load(VoyagePlan);
			Check(ref ok, back != null && back.Rules.Count == 9, "the plan saved with " + (back != null ? back.Rules.Count : 0) + " rules");
			var whens = new HashSet<string>(back.Rules.Select(r => r.When));
			var wheres = new HashSet<string>(back.Rules.Select(r => r.Where));
			foreach (IntroRule r in WorldDirector.RulesFromProps(IslandFile.Load(IslandSpawner.PathFor(VoyageLanding)).Props)) { whens.Add(r.When); wheres.Add(r.Where); }
			Check(ref ok, IntroRule.WhenKinds.All(whens.Contains), "every When is used: " + string.Join(", ", IntroRule.WhenKinds.Where(w => !whens.Contains(w)).Select(w => "missing " + w).DefaultIfEmpty("all").ToArray()));
			Check(ref ok, IntroRule.WhereKinds.All(wheres.Contains), "every Where is used: " + string.Join(", ", IntroRule.WhereKinds.Where(w => !wheres.Contains(w)).Select(w => "missing " + w).DefaultIfEmpty("all").ToArray()));
			// Check, as the World Plans window's button runs it (deep: inside the islands and samples of the map types)
			List<PlanChecker.Finding> found = PlanChecker.Check(back, false, true, null);
			foreach (PlanChecker.Finding x in found) Log("  Check: " + x.Level + (x.Rule >= 0 ? " (rule " + back.Rules[x.Rule].Id + ")" : "") + ": " + x.Text);
			Check(ref ok, !found.Any(x => x.Level == PlanChecker.Level.Problem), "Check finds no problem (" + found.Count(x => x.Level == PlanChecker.Level.Warning) + " warnings, " + found.Count(x => x.Level == PlanChecker.Level.Tip) + " tips)");
			// Test in a world, each island as a builder tries it
			if (test)
				foreach (string n in VoyageIslands)
				{
					Check(ref ok, DynamicIslands.LoadIsland(n), "'" + n + "' opened in the editor");
					yield return new WaitForSeconds(1f);
					IslandTest.Start();
					for (float t = 0; t < 300f && !IslandTest.Testing; t += 1f) yield return new WaitForSeconds(1f);
					yield return new WaitForSeconds(5f);
					IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(x => x.HostName == n);
					Check(ref ok, IslandTest.Testing && e != null && e.Root != null, "Test in a world: '" + n + "' beside the raft (" + IslandTest.LastStep + ")");
					IslandTest.Back();
					for (float t = 0; t < 240f && !(DynamicIslands.InEditor() && !IslandTest.Busy); t += 1f) yield return new WaitForSeconds(1f);
					yield return new WaitForSeconds(2f);
					Check(ref ok, DynamicIslands.InEditor(), "back in the editor");
				}
			// Exported as a pack
			string error;
			string zip = LibraryPack.Export(new LibraryInfo { title = VoyagePlan, id = VoyagePackId, author = "CI Tester", summary = "The test voyage", remix = true }, null, WorldPlan.Load(VoyagePlan), null, null, out error);
			LibraryPackContents pack = zip != null ? LibraryPack.Read(zip, out error) : null;
			Check(ref ok, pack != null && VoyageIslands.All(n => pack.Files.ContainsKey(n + ".island")) && pack.Files.ContainsKey(VoyagePlan + ".plan"),
				"exported as a pack with the plan and its four islands: " + (pack != null ? zip + " (" + string.Join(", ", pack.Files.Keys.ToArray()) + ")" : error));
			if (ok) Log("PASS: adventure build"); else Fail("adventure build");
		}

		[ConsoleCommand(name: "CIAdventureForget", docs: "Dev, main menu: removes the voyage's islands, plan and their downloaded copies from this PC - a second PC that never had them (in the Sandboxie box a delete hides this PC's own files from the box)")]
		public static void AdventureForgetCommand()
		{
			int n = 0;
			foreach (string isl in IslandSpawner.ListSavedIslands().Where(x => x.StartsWith("civoy-", StringComparison.OrdinalIgnoreCase)).ToList())
				try { File.Delete(IslandSpawner.PathFor(isl)); n++; } catch { }
			foreach (string p in WorldPlan.All().Where(x => x.StartsWith(VoyagePlan, StringComparison.OrdinalIgnoreCase)).ToList())
				try { File.Delete(WorldPlan.PathFor(p)); n++; } catch { }
			try { LibraryPack.Remove(VoyagePackId); } catch { }
			IslandCache.Forget();
			Log("Forgot the voyage: " + n + " files removed; islands left: " + IslandSpawner.ListSavedIslands().Count(x => x.StartsWith("civoy-")));
		}

		[ConsoleCommand(name: "CIImportPack", docs: "Dev, anywhere: imports a pack zip as the Import window does: CIImportPack <path or file name in exports\\ or import\\>")]
		public static void ImportPackCommand(string[] args)
		{
			string name = args != null && args.Length > 0 ? string.Join(" ", args) : "";
			string path = File.Exists(name) ? name : new[] { Path.Combine(LibraryPack.ExportFolder, name), Path.Combine(LibraryPack.ImportFolder, name) }.FirstOrDefault(File.Exists);
			if (path == null) { Fail("import: no pack '" + name + "'"); return; }
			string error;
			LibraryPackContents pack = LibraryPack.Read(path, out error);
			if (pack == null) { Fail("import: " + error); return; }
			LibraryPack.Report r = LibraryPack.Install(pack, false, true, LibraryPack.SourceImport);
			Log("Imported '" + path + "': " + r.ToString().Replace("\n", " / "));
			Log("PASS: import " + (r.PlanName ?? "?"));
		}

		#region Playing the voyage (SC61)

		static IslandWorldState.Entry VoyageEntry(string rule) { return IslandWorldState.Islands.FirstOrDefault(x => x.Rule == rule); }

		static IEnumerator WaitLoaded(string rule, float seconds)
		{
			yield return WaitFor(() => { IslandWorldState.Entry e = VoyageEntry(rule); return e != null && (e.Root != null || e.Failed); }, seconds);
			IslandWorldState.Entry x = VoyageEntry(rule);
			if (x != null && x.Root == null && !x.Failed)
			{
				// (far from the raft: the player goes there, as a player sails or swims there)
				PlayerMove.To(RAPI.GetLocalPlayer(), x.Position + Vector3.up * 30f);
				yield return WaitFor(() => x.Root != null, 30f);
			}
		}

		static int VoyageStep(string rule) { IslandWorldState.Entry e = VoyageEntry(rule); return e != null ? QuestTracker.StepOf(e) : -1; }

		[ConsoleCommand(name: "CIAdventurePlay", docs: "Dev, world (host; a new world made with the plan 'CI Voyage'): SC61 - the voyage played to its end. CIAdventurePlay landing / cove / vault = each part (the runner saves and loads between them); CIAdventurePlay end = everything as at the end; CIAdventurePlay clean = the voyage's islands removed")]
		public static void AdventurePlayCommand(string[] args)
		{
			string part = args != null && args.Length > 0 ? args[0] : "landing";
			StartTest(AdventurePlayRoutine(part));
		}

		static IEnumerator AdventurePlayRoutine(string part)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("adventure play " + part + ": host, in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Network_Player me = RAPI.GetLocalPlayer();
			if (part == "landing")
			{
				Check(ref ok, WorldDirector.PlanName == VoyagePlan, "the world plays '" + VoyagePlan + "' (" + WorldDirector.PlanName + ")");
				yield return WaitLoaded("landing", 60f);
				IslandWorldState.Entry l = VoyageEntry("landing");
				Check(ref ok, l != null && l.Root != null, "start: Landing Beach came ahead of the raft");
				if (l == null || l.Root == null) { Fail("adventure play landing"); yield break; }
				yield return StandRoutine(l.Root);
				yield return new WaitForSeconds(2f);
				Check(ref ok, StoryBook.Pages.Any(p => (p.Text ?? "").Contains("We reached the landing beach")), "arriving: the island's event wrote a journal page");
				ScEnterZone(l, "camp"); yield return new WaitForSeconds(1.5f);
				ScReadNote(l, "Captain's log"); yield return new WaitForSeconds(1.5f);
				Check(ref ok, VoyageStep("landing") == 2 && StoryBook.Count("civoy-map") == 1, "the camp found, the log read: step 3 of 5, a map piece in the journal (" + StoryBook.Count("civoy-map") + ")");
				ScOpenChest(l, "Supplies"); yield return new WaitForSeconds(1.5f);
				for (int i = 0; i < 6; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("sandbar") != null, "three steps done: 'step:landing:3' brought the sandbar east of the landing");
				yield return ScWaitAnimals(l, "Warthog", 2, 20f);
				foreach (AI_NetworkBehaviour a in ScAnimals(l, "Warthog")) { PutPlayerNear(a.transform); ScKill(a); yield return new WaitForSeconds(0.3f); }
				yield return new WaitForSeconds(3f);
				Check(ref ok, VoyageStep("landing") == 4, "the warthogs chased off: step 5 of 5 (" + VoyageStep("landing") + ")");
				yield return ScWaitAnimals(l, "Chicken", 1, 20f);
				var chicken = ScAnimals(l, "Chicken").OfType<AI_NetworkBehaviour_Domestic>().FirstOrDefault();
				yield return ScCarryHome(chicken, false);
				yield return new WaitForSeconds(3f);
				Check(ref ok, VoyageStep("landing") == 5, "a chicken caught: the landing's quest is done (" + VoyageStep("landing") + ")");
				for (int i = 0; i < 6 && VoyageEntry("cove") == null; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				IslandWorldState.Entry cove = VoyageEntry("cove");
				Check(ref ok, cove != null, "the quest done: the landing's own rule brought the cove");
				if (cove != null && l.Root != null) Check(ref ok, ScFlat(cove.Position, l.Position) > 300f && cove.Position.z > l.Position.z, "... north of the landing (" + ScFlat(cove.Position, l.Position).ToString("F0") + " m)");
				OnRaftCommand();
			}
			else if (part == "cove")
			{
				yield return new WaitForSeconds(3f);
				Check(ref ok, VoyageStep("landing") == 5 && VoyageEntry("cove") != null && StoryBook.Count("civoy-map") == 1, "after loading: the landing's quest done, the cove there, one map piece");
				// km sailed: the wreck
				WorldDirector.Sailed += 1100f;
				for (int i = 0; i < 6 && VoyageEntry("wreck") == null; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("wreck") != null, "1.1 km sailed: 'km:1' brought the wreck");
				// a night slept: day 2 - then the lucky find comes by chance while sailing
				int? d0 = ScDay;
				BedManager beds = ComponentManager<BedManager>.Value ?? UnityEngine.Object.FindObjectOfType<BedManager>() ?? Resources.FindObjectsOfTypeAll<BedManager>().FirstOrDefault(b => b.gameObject.scene.IsValid());
				// (Raft's BedManager.Slumber is a private static coroutine; a new world starts on day 0: two nights)
				System.Reflection.MethodInfo slumber = typeof(BedManager).GetMethod("Slumber", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
				MonoBehaviour sleeper = beds != null ? (MonoBehaviour)beds : DynamicIslands.instance;
				for (int night = 0; night < 4 && slumber != null && ScDay.HasValue && ScDay.Value < 2; night++)
				{
					AzureSkyHour(22f);
					yield return null;
					IEnumerator s = slumber.Invoke(slumber.IsStatic ? null : beds, new object[] { true }) as IEnumerator;
					if (s != null) yield return sleeper.StartCoroutine(s);
					yield return new WaitForSeconds(1.5f);
				}
				Check(ref ok, ScDay >= 2, "day " + ScDay + " (slept from day " + d0 + ")");
				for (int i = 0; i < 4; i++) { StoryChain.Tick(); yield return new WaitForSeconds(0.5f); }
				WorldDirector.Sailed += 2000f;
				for (int i = 0; i < 8 && VoyageEntry("luck") == null; i++) { StoryChain.Tick(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("luck") != null, "day 2, then 2 km sailed: 'day:2' + 'by chance while sailing' brought the lucky find");
				// the cove: reached (visit), the dock (zone), the crates, the note's wait, the lever's signal
				yield return WaitLoaded("cove", 60f);
				IslandWorldState.Entry c = VoyageEntry("cove");
				if (c == null || c.Root == null) { Fail("adventure play cove: the cove didn't load"); yield break; }
				yield return StandRoutine(c.Root);
				for (int i = 0; i < 6 && VoyageEntry("sunk") == null; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("sunk") != null, "players at the cove: 'visit:cove' brought the sunken reef");
				ScEnterZone(c, "dock"); yield return new WaitForSeconds(1.5f);
				for (int i = 0; i < 6 && VoyageEntry("dock") == null; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("dock") != null, "the dock zone: 'zone:cove:dock' brought the oddity");
				for (int i = 1; i <= 3; i++) { ScOpenChest(c, "Crate " + i); yield return new WaitForSeconds(1.2f); }
				yield return new WaitForSeconds(2f);
				Check(ref ok, StoryBook.Count("civoy-map") == 2 && StoryBook.Count("civoy-key") == 1, "the crates: the second map piece and the key (maps " + StoryBook.Count("civoy-map") + ", key " + StoryBook.Count("civoy-key") + ")");
				Check(ref ok, VoyageStep("cove") == 2, "the cove's quest done: 3 chests, 2 map pieces collected (" + VoyageStep("cove") + ")");
				ScReadNote(c, "Smuggler's note");
				IslandObjectRef marker = ScObjOf(c, "marker");
				Check(ref ok, marker != null && !marker.gameObject.activeInHierarchy, "the note read: the marker still hidden");
				yield return new WaitForSeconds(6.5f);
				Check(ref ok, marker != null && marker.gameObject.activeInHierarchy, "... and shown after the note's 5 s wait");
				ScUse(c, "lever");
				yield return new WaitForSeconds(1.5f);
				for (int i = 0; i < 6; i++) { StoryChain.Tick(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, StoryChain.Fired.Contains("vault"), "the lever's signal 'gate': the vault's rule fired (" + StoryChain.LastBanner + ")");
				string freq = StoryChain.FrequencyOf("vault");
				Check(ref ok, freq != null && StoryBook.Pages.Any(p => p.Key == "storyfreq:vault"), "... a Receiver frequency (" + freq + ") and a journal page for it");
				yield return TuneTo("vault");
				Check(ref ok, VoyageEntry("vault") != null, "tuned to " + freq + ": the vault came");
				OnRaftCommand();
			}
			else if (part == "vault")
			{
				yield return new WaitForSeconds(3f);
				Check(ref ok, VoyageEntry("vault") != null && StoryBook.Count("civoy-key") == 1 && StoryBook.Count("civoy-map") == 2, "after loading: the vault there, the key and both map pieces in the journal");
				yield return WaitLoaded("vault", 60f);
				IslandWorldState.Entry v = VoyageEntry("vault");
				if (v == null || v.Root == null) { Fail("adventure play vault: the vault didn't load"); yield break; }
				yield return StandRoutine(v.Root);
				ScEnterZone(v, "vault"); yield return new WaitForSeconds(1.5f);
				ScUse(v, "vaultdoor"); yield return new WaitForSeconds(1.5f);
				Check(ref ok, Behaviours.LastMessage.Contains("vault opens") && StoryBook.Count("civoy-key") == 0, "the door takes the key and opens ('" + Behaviours.LastMessage + "', keys " + StoryBook.Count("civoy-key") + ")");
				ScOpenChest(v, "Treasure"); yield return new WaitForSeconds(1.5f);
				Check(ref ok, StoryBook.Count("civoy-crown") == 1, "the treasure: the old crown");
				ScReadNote(v, "Vault ledger"); yield return new WaitForSeconds(2f);
				Check(ref ok, VoyageStep("vault") == 3, "the vault's quest done: 3 journal pages (the log, the note, the ledger) (" + VoyageStep("vault") + ")");
				for (int i = 0; i < 6 && VoyageEntry("summit") == null; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("summit") != null, "the vault's quest done: 'quest:vault' brought the summit");
				for (int i = 0; i < 6 && VoyageEntry("sky") == null; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, VoyageEntry("sky") != null, "'after rule summit' brought the sky island");
				yield return WaitLoaded("summit", 60f);
				IslandWorldState.Entry s = VoyageEntry("summit");
				if (s != null && s.Root != null)
				{
					yield return StandRoutine(s.Root);
					ScReadNote(s, "Summit stone"); yield return new WaitForSeconds(2f);
				}
				Check(ref ok, VoyageStep("summit") == 1 && StoryBook.Pages.Any(p => (p.Text ?? "").Contains("The voyage is complete")), "the summit stone read: the voyage is complete (a journal page)");
				OnRaftCommand();
			}
			else if (part == "atlanding" || part == "atcove")
			{
				// (SC39: an older save loaded from Raft's Load Game box - everything as at that save)
				yield return new WaitForSeconds(3f);
				bool cove = part == "atcove";
				Check(ref ok, VoyageStep("landing") == 5 && VoyageEntry("cove") != null, "the landing's quest done and the cove there");
				Check(ref ok, cove ? VoyageStep("cove") == 2 : VoyageStep("cove") <= 0, "the cove's quest " + (cove ? "done" : "not begun") + " (" + VoyageStep("cove") + ")");
				Check(ref ok, StoryBook.Count("civoy-map") == (cove ? 2 : 1) && StoryBook.Count("civoy-key") == (cove ? 1 : 0), "the journal's story items as at that save (maps " + StoryBook.Count("civoy-map") + ", key " + StoryBook.Count("civoy-key") + ")");
				Check(ref ok, cove ? VoyageEntry("vault") != null : VoyageEntry("vault") == null && VoyageEntry("wreck") == null, "the islands as at that save (vault " + (VoyageEntry("vault") != null) + ", wreck " + (VoyageEntry("wreck") != null) + ")");
			}
			else if (part == "end")
			{
				yield return new WaitForSeconds(3f);
				Check(ref ok, new[] { "landing", "cove", "vault", "summit" }.All(r => VoyageEntry(r) != null), "after loading: all four islands are in the world");
				Check(ref ok, VoyageStep("landing") == 5 && VoyageStep("cove") == 2 && VoyageStep("vault") == 3 && VoyageStep("summit") == 1, "every quest done");
				Check(ref ok, StoryBook.Count("civoy-map") == 2 && StoryBook.Count("civoy-key") == 0 && StoryBook.Count("civoy-crown") == 1, "story items: 2 map pieces, no key (used), the crown");
				Check(ref ok, new[] { "wreck", "luck", "sandbar", "dock", "sunk", "sky" }.All(r => VoyageEntry(r) != null), "the map-type islands every rule brought are there");
				Check(ref ok, IslandWorldState.Islands.Count(x => x.Rule == "landing") == 1 && IslandWorldState.Islands.Count(x => x.Rule == "cove") == 1, "each island once");
			}
			else if (part == "clean")
			{
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => !string.IsNullOrEmpty(x.Rule)).Select(x => x.Id).ToList(), true);
				WorldDirector.Done.Clear();
				StoryChain.Reset();
				foreach (string id in new[] { "civoy-map", "civoy-key", "civoy-crown" }) StoryBook.Take(id, StoryBook.Count(id));
				IslandWorldState.Save();
				Log("PASS: adventure play clean");
				yield break;
			}
			IslandWorldState.Save();
			if (ok) Log("PASS: adventure play " + part); else Fail("adventure play " + part);
		}

		#endregion
	}
}
