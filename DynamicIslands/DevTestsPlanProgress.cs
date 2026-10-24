using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>TODO 4c, 2026-10-16: world plans give Raft's progression in Raft's order (a blueprint never before the ones it
	/// builds on), and Raft's quest items and the plans' own story items are kept for later islands (a lock on one island,
	/// its key found on an island met before) - in a test plan and in the library's five plans.</summary>
	public static partial class DevTests
	{
		const string ProgEarly = "citest-pp-early", ProgBases = "citest-pp-bases", ProgKey = "ci-pp-key";

		[ConsoleCommand(name: "CIPlanBlueprintOrder", docs: "Dev, anywhere: the plan Check and Progression warn about a blueprint given before the ones it builds on (the engine controls before the motor wheel and steering wheel), not when Raft's order is kept; a lock whose story item an island met before gives shows where it was kept from")]
		public static void PlanBlueprintOrderCommand(string[] args) { StartTest(PlanBlueprintOrderRoutine()); }

		static IEnumerator PlanBlueprintOrderRoutine()
		{
			bool ok = true;
			try { PlanBlueprintOrderChecks(ref ok); }
			catch (Exception ex) { Check(ref ok, false, "no errors: " + ex); }
			finally { DeleteTestIslands(new[] { ProgEarly, ProgBases }); }
			if (ok) Log("PASS: plan blueprint order"); else Fail("plan blueprint order");
			yield break;
		}

		static void PlanBlueprintOrderChecks(ref bool ok)
		{
			ScrambledBlueprints.Read();
			Check(ref ok, ScrambledBlueprints.OnIslands.ContainsKey("Blueprint_EngineControls") && ScrambledBlueprints.OnIslands.ContainsKey("Blueprint_MotorWheel"), "Raft's story-island blueprints read");
			// One island gives the engine controls and a key; the other the motor wheel and steering wheel, behind a door
			// wanting that key
			IslandFile ef = EmptyTestIsland();
			ef.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.zero, Props = new Dictionary<string, string> { { ObjectProps.LootItems, "Blueprint_EngineControls*1;" + StoryItems.Prefix + ProgKey + "*1;Plank*2" } } });
			IslandFile bf = EmptyTestIsland();
			bf.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = Vector3.zero, Props = new Dictionary<string, string> {
				{ ObjectProps.LootItems, "Blueprint_MotorWheel*1;Blueprint_SteeringWheel*1" }, { BehaviourProps.CheckKey("open"), "has|" + StoryItems.Prefix + ProgKey + "|1" } } });
			ef.Save(IslandSpawner.PathFor(ProgEarly));
			bf.Save(IslandSpawner.PathFor(ProgBases));
			Func<string, string, bool, WorldPlan> plan = (first, second, story) =>
			{
				var p = new WorldPlan { Name = "CI Plan Blueprint Order", Random = false, RaftStory = story };
				p.Rules.Add(new IntroRule { Id = "cione", What = "island", WhatArg = first, When = "start", Where = "receiver", Label = "One", StoryPlace = "first" });
				p.Rules.Add(new IntroRule { Id = "citwo", What = "island", WhatArg = second, When = "start", Where = "receiver", Label = "Two", StoryPlace = "after:cione" });
				return p;
			};
			Func<WorldPlan, string> order = p => string.Join(" / ", PlanChecker.Check(p, false, false).Where(x => x.Text.Contains("it builds on")).Select(x => x.Text).ToArray());

			// The engine controls first, their bases on the next island: warned, naming that island
			WorldPlan early = plan(ProgEarly, ProgBases, false);
			string warned = order(early);
			Check(ref ok, warned.Contains("Rule 1 'cione'") && warned.Contains("Engine Controls") && warned.Contains("Motor Wheel and Steering Wheel") && warned.Contains("rule 2 'citwo'"),
				"the engine controls before the motor wheel and steering wheel: warned (" + warned + ")");
			PlanChecker.Progress pr = PlanChecker.Progression(early, false);
			PlanChecker.Progress.Step one = pr.Steps.FirstOrDefault(s => s.Rule == 0), two = pr.Steps.FirstOrDefault(s => s.Rule == 1);
			Check(ref ok, one != null && one.Early.Count == 1 && one.Early[0].Bases.Count == 2 && one.Early[0].Later.StartsWith("rule 2"),
				"Progression: rule 1 gives them too early, rule 2 later (" + (one != null && one.Early.Count > 0 ? one.Early[0].Later : "none") + ")");
			// The key: given by rule 1, kept by the crew, opens rule 2's door
			string keyLabel = StoryItems.Label(ProgKey);
			Check(ref ok, two != null && two.NeedsLate.Count == 0 && two.NeedsFrom.ContainsKey(keyLabel) && two.NeedsFrom[keyLabel].StartsWith("rule 1"),
				"rule 2's lock: its key kept from rule 1 (" + (two != null && two.NeedsFrom.ContainsKey(keyLabel) ? two.NeedsFrom[keyLabel] : "none") + ")");

			// Raft's order (the bases first): no order warning, and the lock comes before its key
			WorldPlan right = plan(ProgBases, ProgEarly, false);
			Check(ref ok, order(right).Length == 0, "the bases first: no order warning (" + order(right) + ")");
			PlanChecker.Progress rp = PlanChecker.Progression(right, false);
			PlanChecker.Progress.Step lockFirst = rp.Steps.FirstOrDefault(s => s.Rule == 0);
			Check(ref ok, lockFirst != null && lockFirst.NeedsLate.Contains(keyLabel) && !lockFirst.NeedsFrom.ContainsKey(keyLabel), "the lock before its key: wanted before any island gives it");

			// Raft's story on: the engine controls first, before Vasagatan - Vasagatan named; Raft's own chain alone is in order
			var withStory = new WorldPlan { Name = "CI Plan Blueprint Order story", Random = false, RaftStory = true };
			withStory.Rules.Add(new IntroRule { Id = "cione", What = "island", WhatArg = ProgEarly, When = "start", Where = "receiver", Label = "One", StoryPlace = "first" });
			string raft = order(withStory);
			Check(ref ok, raft.Contains("Rule 1 'cione'") && raft.Contains("Vasagatan"), "before Raft's Vasagatan: warned, naming it (" + raft + ")");
			var plain = new WorldPlan { Name = "CI Plan Blueprint Order Raft", Random = false, RaftStory = true };
			PlanChecker.Progress pp = PlanChecker.Progression(plain, false);
			Check(ref ok, pp.Steps.Count >= 7 && pp.Steps.All(s => s.Early.Count == 0) && pp.Missing.Count == 0, "Raft's own story: every blueprint after its bases (" + pp.Steps.Count + " story islands)");

			PlanProgressWindow.Open(early, null);
			if (PlanProgressWindow.IsOpen)
			{
				List<string> rows = PlanProgressWindow.Rows;
				Check(ref ok, rows.Count >= 2 && rows[0].Contains("Too early:") && rows[1].Contains("kept from rule 1"),
					"the Progression panel: " + string.Join(" / ", rows.Select(r => r.Length > 110 ? r.Substring(0, 110) + "..." : r).ToArray()));
				PlanProgressWindow.Close();
			}
			else Log("  (the Progression panel isn't built here: run in the editor to check it too)");
		}

		static readonly string[] LibraryPlans = { "Raft 2 - The Drowned Frontier", "Raft Remade", "The Long Voyage", "Silver Screen Seas", "The Abyss Expedition" };

		/// <summary>The library's plans (installed): every one of Raft's blueprints, none before its bases, no lock before its
		/// story item, no main quest held up by a tool, no story item id on two islands - and each plan has a lock on one
		/// island whose story item is found on an island met before (kept by the crew, TODO 4c).</summary>
		[ConsoleCommand(name: "CIPlanLibraryProgression", docs: "Dev, anywhere: the library's five plans (installed) give Raft's progression in order, every lock's story item before it, and use at least one story item kept from an earlier island")]
		public static void PlanLibraryProgression(string[] args)
		{
			bool ok = true;
			int checkedPlans = 0;
			foreach (string name in LibraryPlans)
			{
				WorldPlan p = WorldPlan.Load(name);
				if (p == null) { Log("  (plan '" + name + "' isn't installed - skipped)"); continue; }
				checkedPlans++;
				PlanChecker.Progress pr = PlanChecker.Progression(p, false);
				List<PlanChecker.Finding> f = PlanChecker.Check(p, false, false);
				Func<PlanChecker.Finding, bool> progression = x => x.Text.StartsWith("Raft's blueprints never given") || x.Text.Contains("it builds on") || x.Text.Contains("has a lock that wants the story item") ||
					x.Text.Contains("can't be done without the") || x.Text.Contains("is given on ");
				List<PlanChecker.Finding> bad = f.Where(progression).ToList();
				foreach (PlanChecker.Finding b in bad) Log("  '" + name + "': " + b.Text);
				int late = pr.Steps.Sum(s => s.NeedsLate.Count), early = pr.Steps.Sum(s => s.Early.Count), tools = pr.Steps.Sum(s => s.ToolNeeds.Count(n => n.Main));
				List<string> kept = pr.Steps.Where(s => s.Rule >= 0).SelectMany(s => s.NeedsFrom.Select(kv => kv.Key + " (rule " + (s.Rule + 1) + ", from " + kv.Value + ")")).ToList();
				Check(ref ok, bad.Count == 0 && pr.Missing.Count == 0 && late == 0 && early == 0 && tools == 0,
					"'" + name + "': " + pr.Missing.Count + " blueprints never given, " + early + " too early, " + late + " locks before their item, " + tools + " quests held up by a tool, " + bad.Count + " such findings");
				Check(ref ok, kept.Count > 0, "'" + name + "' keeps story items for later islands: " + (kept.Count > 0 ? string.Join("; ", kept.ToArray()) : "none"));
			}
			Check(ref ok, checkedPlans == LibraryPlans.Length, checkedPlans + " of the " + LibraryPlans.Length + " library plans installed");
			if (ok) Log("PASS: plan library progression"); else Fail("plan library progression");
		}
	}
}
