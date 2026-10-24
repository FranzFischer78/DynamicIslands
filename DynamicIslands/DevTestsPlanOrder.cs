using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>The plan Check in play order (the user, 2026-10-16): the story chain and each rule's WHEN, not the file's
	/// order; Raft's tools for the islands' features (RaftFeatures); one story item id on two islands.</summary>
	public static partial class DevTests
	{
		const string OrderVines = "citest-po-vines", OrderTools = "citest-po-tools", OrderCache = "citest-po-cache";

		/// <summary>A saved island to copy the land of (as CIStoryOrderCheck), emptied: objects and settings are the test's.</summary>
		static IslandFile EmptyTestIsland()
		{
			string source = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().First(n => !n.StartsWith("ci") && !IslandNetwork.IsDownloadName(n));
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor(source));
			f.Objects.Clear();
			f.Props = new Dictionary<string, string>();
			return f;
		}

		static void DeleteTestIslands(IEnumerable<string> names)
		{
			foreach (string n in names) try { if (System.IO.File.Exists(IslandSpawner.PathFor(n))) System.IO.File.Delete(IslandSpawner.PathFor(n)); } catch { }
		}

		[ConsoleCommand(name: "CIPlanProgressOrder", docs: "Dev, anywhere: the plan Check and Progression in play order - a plan whose file order differs from its story chain (first, after the Radio Tower, a rule waiting for another's quest) is walked as played, with Raft's story islands at their places; vines in front of a quest's chest before any machete are warned about, buried treasure before the detector only noted")]
		public static void PlanProgressOrderCommand(string[] args) { StartTest(PlanProgressOrderRoutine()); }

		static IEnumerator PlanProgressOrderRoutine()
		{
			bool ok = true;
			try { PlanProgressOrderChecks(ref ok); }
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex); }
			finally { DeleteTestIslands(new[] { OrderVines, OrderTools, OrderCache }); }
			if (ok) Log("PASS: plan progress order"); else Fail("plan progress order");
			yield break;
		}

		static void PlanProgressOrderChecks(ref bool ok)
		{
			// Islands: vines in front of the chest the quest opens (the machete, for the quest) and buried treasure (the
			// detector, optional); a chest with a machete; buried treasure alone
			IslandFile vf = EmptyTestIsland();
			vf.Props[IslandQuest.KeyTitle] = "CI behind the vines";
			vf.Props[IslandQuest.KeySteps] = "open|Behind the vines|1|";
			Dictionary<string, string> vines = ObjectProps.Defaults(ContentCatalog.MacheteVines);
			vines[BehaviourProps.EventKey("use")] = "hide|\nshow|civinechest";
			vf.Objects.Add(new IslandObject { Name = ContentCatalog.MacheteVines, Position = Vector3.zero, Props = vines });
			vf.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.one, Props = new Dictionary<string, string> { { BehaviourProps.Name, "civinechest" }, { ObjectProps.NoteTitle, "Behind the vines" }, { BehaviourProps.Hidden, "1" }, { ObjectProps.LootItems, "Plank*2" } } });
			vf.Objects.Add(new IslandObject { Name = ContentCatalog.BuriedTreasure, Position = Vector3.one * 4f, Props = new Dictionary<string, string> { { ObjectProps.TreasureKind, "0" } } });
			IslandFile tf = EmptyTestIsland();
			tf.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.zero, Props = new Dictionary<string, string> { { ObjectProps.LootItems, ContentCatalog.MacheteItem + "*1;Rope*2" } } });
			IslandFile cf = EmptyTestIsland();
			cf.Objects.Add(new IslandObject { Name = ContentCatalog.BuriedTreasure, Position = Vector3.zero, Props = new Dictionary<string, string> { { ObjectProps.TreasureKind, "0" } } });
			vf.Save(IslandSpawner.PathFor(OrderVines));
			tf.Save(IslandSpawner.PathFor(OrderTools));
			cf.Save(IslandSpawner.PathFor(OrderCache));
			PlanChecker.Facts vx = PlanChecker.FromFile(vf, OrderVines, false), tx = PlanChecker.FromFile(tf, OrderTools, false);
			Check(ref ok, vx.MainNeeds.Contains(ContentCatalog.MacheteItem + "|vines") && vx.NeedsTools.ContainsKey(global::DynamicIslands.Editor.RaftFeatures.MetalDetectorItem) && !vx.MainNeeds.Any(m => m.StartsWith(global::DynamicIslands.Editor.RaftFeatures.MetalDetectorItem + "|")),
				"the vines are in the quest's way (machete), the treasure is optional (detector): " + string.Join(", ", vx.MainNeeds.ToArray()));
			Check(ref ok, tx.GivesTools.Contains(ContentCatalog.MacheteItem), "a chest with a machete gives the tool");

			// File order: the vines' rule, the tools' rule (after the vines' quest), the treasure's (first in the story).
			// Play order: the treasure, Radio Tower, the vines, the tools, Vasagatan, Balboa...
			Func<string, string, WorldPlan> plan = (vinesPlace, toolsPlace) =>
			{
				var p = new WorldPlan { Name = "CI Plan Progress Order", Random = false };
				p.Rules.Add(new IntroRule { Id = "civines", What = "island", WhatArg = OrderVines, When = "start", Where = "receiver", Label = "Vines", StoryPlace = vinesPlace });
				p.Rules.Add(toolsPlace == null
					? new IntroRule { Id = "citools", What = "island", WhatArg = OrderTools, When = "quest", WhenRef = "civines", Where = "ahead", Message = "Tools." }
					: new IntroRule { Id = "citools", What = "island", WhatArg = OrderTools, When = "start", Where = "receiver", Label = "Tools", StoryPlace = toolsPlace });
				p.Rules.Add(new IntroRule { Id = "cicache", What = "island", WhatArg = OrderCache, When = "start", Where = "receiver", Label = "Cache", StoryPlace = "first" });
				return p;
			};
			WorldPlan early = plan("after:RadioTower", null);
			List<PlanChecker.Stop> stops = PlanChecker.PlayOrder(early);
			string order = string.Join(" > ", stops.Select(s => PlanChecker.StopName(early, s)).ToArray());
			Check(ref ok, stops.Count == 3 + StoryOrder.Chain.Length && stops[0].Rule == 2 && stops[1].Raft == ChunkPointType.Landmark_RadioTower && stops[2].Rule == 0 && stops[3].Rule == 1 && stops[4].Raft == ChunkPointType.Landmark_Vasagatan,
				"play order, not the file's: " + order);
			Func<List<PlanChecker.Finding>, string> tools = fs => string.Join(" / ", fs.Where(x => x.Text.Contains("can't be done without the")).Select(x => x.Text).ToArray());
			string warned = tools(PlanChecker.Check(early, false, false));
			Check(ref ok, warned.Contains("machete") && warned.Contains("'citools'") && warned.Contains("the story stops here"), "vines in a quest's way before the machete: warned, naming the later island (" + warned + ")");
			Check(ref ok, !warned.Contains("metal detector"), "the buried treasure before the detector: no warning");
			PlanChecker.Progress pr = PlanChecker.Progression(early, false);
			PlanChecker.Progress.Step cache = pr.Steps.FirstOrDefault(s => s.Rule == 2);
			PlanChecker.Progress.ToolNeed detector = cache != null ? cache.ToolNeeds.FirstOrDefault(n => n.Tool == global::DynamicIslands.Editor.RaftFeatures.MetalDetectorItem) : null;
			Check(ref ok, pr.Steps.Count > 0 && pr.Steps[0].Rule == 2 && detector != null && !detector.Main && detector.Later == "Caravan Town",
				"Progression: the treasure first, quietly noted - Caravan Town gives the detector later (" + (detector != null ? detector.Later : "none") + ")");
			PlanChecker.Progress.Step balboa = pr.Steps.FirstOrDefault(s => s.Raft == ChunkPointType.Landmark_Balboa);
			Check(ref ok, balboa != null && balboa.Blueprints.Contains("Blueprint_Machete") && pr.Steps.IndexOf(balboa) > pr.Steps.FindIndex(s => s.Rule == 0),
				"Balboa gives its blueprints at its place in the chain, after the vines' island, not at the start");
			// The machete given before the vines: the tools' island first, or the vines after Balboa
			string fine = tools(PlanChecker.Check(plan("after:RadioTower", "first"), false, false)) + tools(PlanChecker.Check(plan("after:Balboa", "after:Utopia"), false, false));
			Check(ref ok, fine.Length == 0, "the machete before the vines (an island first, or Balboa's blueprint): fine (" + fine + ")");
			// Raft's story off and nothing gives a machete: never given
			WorldPlan bare = plan("first", "after:civines");
			bare.RaftStory = false;
			bare.Rules.RemoveAt(1);
			string none = tools(PlanChecker.Check(bare, false, false));
			Check(ref ok, none.Contains("no island of the plan gives it"), "Raft's story off, no machete anywhere: warned (" + none + ")");
			PlanProgressWindow.Open(early, null);
			if (PlanProgressWindow.IsOpen)
			{
				List<string> rows = PlanProgressWindow.Rows;
				Check(ref ok, rows.Count > 4 && rows[0].StartsWith("RULE 3") && rows[1].StartsWith("RADIO TOWER") && rows[2].StartsWith("RULE 1") && rows.Any(r => r.Contains("Optional:")),
					"the Progression panel in play order: " + string.Join(" / ", rows.Take(4).Select(r => r.Length > 70 ? r.Substring(0, 70) + "..." : r).ToArray()));
				PlanProgressWindow.Close();
			}
			else Log("  (the Progression panel isn't built here: run in the editor to check it too)");
		}

		[ConsoleCommand(name: "CIStoryIdClash", docs: "Dev, anywhere: the plan Check's story item ids - two islands that each give a 'keycard' are warned about (one's key opens the other's lock), an island that only wants one another gives is fine, two with the pickup of the same Raft quest item too; Raft's own quest items on its story islands aren't keys for the plan's locks")]
		public static void StoryIdClashCommand(string[] args) { StartTest(StoryIdClashRoutine()); }

		static IEnumerator StoryIdClashRoutine()
		{
			bool ok = true;
			string[] islands = { "citest-clash-a", "citest-clash-b", "citest-clash-c", "citest-clash-d", "citest-clash-e" };
			try { StoryIdClashChecks(ref ok, islands); }
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex); }
			finally { DeleteTestIslands(islands); }
			if (ok) Log("PASS: story id clash"); else Fail("story id clash");
			yield break;
		}

		static void StoryIdClashChecks(ref bool ok, string[] islands)
		{
			string card = StoryItems.Ref("keycard");
			Func<string, bool, bool, IslandFile> make = (item, gives, locks) =>
			{
				IslandFile f = EmptyTestIsland();
				if (gives) f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.zero, Props = new Dictionary<string, string> { { ObjectProps.LootItems, item + "*1;Plank*2" } } });
				if (locks) f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.one * 3f, Props = new Dictionary<string, string> { { ObjectProps.LootItems, "Rope*2" }, { BehaviourProps.CheckKey("open"), "has|" + item + "|1" } } });
				return f;
			};
			make(card, true, true).Save(IslandSpawner.PathFor(islands[0]));
			make(card, true, true).Save(IslandSpawner.PathFor(islands[1]));
			make(card, false, true).Save(IslandSpawner.PathFor(islands[2]));
			string cutters = StoryItems.Ref(QuestItemPickups.IdPrefix + "Vasagatan_BoltCutter");
			make(cutters, true, true).Save(IslandSpawner.PathFor(islands[3]));
			make(cutters, true, true).Save(IslandSpawner.PathFor(islands[4]));
			Func<int[], List<PlanChecker.Finding>> check = which =>
			{
				var p = new WorldPlan { Name = "CI Story Id Clash", Random = false };
				foreach (int i in which) p.Rules.Add(new IntroRule { Id = "clash" + i, What = "island", WhatArg = islands[i], When = "start" });
				return PlanChecker.Check(p, false, false);
			};
			Func<List<PlanChecker.Finding>, PlanChecker.Finding> clash = fs => fs.FirstOrDefault(x => x.Text.StartsWith("The story item '"));
			PlanChecker.Finding two = clash(check(new[] { 0, 1, 2 }));
			Check(ref ok, two != null && two.Level == PlanChecker.Level.Warning && two.Text.Contains("'keycard'") && two.Text.Contains(islands[0]) && two.Text.Contains(islands[1]) && !two.Text.Contains(islands[2]) && two.Fix.Contains("-keycard'"),
				"two islands each giving a 'keycard': warned, with its own id as the fix (" + (two != null ? two.Text + " | " + two.Fix : "nothing") + ")");
			PlanChecker.Finding one = clash(check(new[] { 0, 2 }));
			Check(ref ok, one == null, "one gives it, another only wants it: fine (" + (one != null ? one.Text : "") + ")");
			PlanChecker.Finding raft = clash(check(new[] { 3, 4 }));
			Check(ref ok, raft != null && raft.Text.Contains("Vasagatan_BoltCutter") && raft.Fix.Contains("of the island's own"), "two islands with Raft's bolt cutters: warned (" + (raft != null ? raft.Text : "nothing") + ")");
			// Raft's own quest items open only their story island: Tangaroa kept, a plan island's door wanting Tangaroa's keycard
			// is still told no island gives it
			IslandFile door = EmptyTestIsland();
			door.Objects.Add(new IslandObject { Name = "PlantationDoor", Position = Vector3.zero, Props = ObjectProps.Defaults("PlantationDoor") });
			door.Save(IslandSpawner.PathFor(islands[2]));
			var kept = new WorldPlan { Name = "CI Raft's Own Keys", Random = false, RaftStory = true };
			kept.Rules.Add(new IntroRule { Id = "door", What = "island", WhatArg = islands[2], When = "start", Where = "receiver", Label = "Door", StoryPlace = "after:Utopia" });
			string want = string.Join(" / ", PlanChecker.Check(kept, false, false).Where(x => x.Text.Contains("has a lock that wants")).Select(x => x.Text).ToArray());
			Check(ref ok, want.Contains("no island of the plan gives") && want.Contains("Raft's own on Tangaroa"), "Raft's own keycard on Tangaroa isn't a key for the plan's door (" + want + ")");
		}
	}
}
