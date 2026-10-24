using System;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>Raft's progression in the plans (the user, 2026-10-05): the plan Check names every blueprint of Raft's
		/// story islands a plan never gives. The library's plans give them all (their own islands or the story islands they
		/// keep); a plan with Raft's story off and no islands is told about every one.</summary>
		[ConsoleCommand(name: "CIPlanBlueprints", docs: "Dev, anywhere: the plan Check's blueprint findings for the library's plans (installed), and for a plan with Raft's story off and no islands; CIPlanBlueprints <plan name>: that plan only - every story blueprint it never hands out (the user, 2026-10-18: every plan gives them all)")]
		public static void PlanBlueprints(string[] args)
		{
			bool ok = true;
			ScrambledBlueprints.Read();
			int all = ScrambledBlueprints.OnIslands.Count;
			Check(ref ok, all >= 20, "Raft's story islands' blueprints known: " + all);
			string one = args != null ? string.Join(" ", args).Trim() : "";
			foreach (string name in one.Length > 0 ? new[] { one } : new[] { "Raft 2 - The Drowned Frontier", "Raft Remade", "The Long Voyage", "Silver Screen Seas", "The Abyss Expedition", "The Rainmakers" })
			{
				WorldPlan p = WorldPlan.Load(name);
				if (p == null) { if (one.Length > 0) Check(ref ok, false, "no saved plan '" + one + "'"); else Log("  (plan '" + name + "' isn't installed - skipped)"); continue; }
				List<PlanChecker.Finding> f = PlanChecker.Check(p, false, false);
				PlanChecker.Finding miss = f.FirstOrDefault(x => x.Text.StartsWith("Raft's blueprints never given"));
				PlanChecker.Finding tip = f.FirstOrDefault(x => x.Text.StartsWith("Raft's blueprints given by the plan's islands"));
				Check(ref ok, miss == null, "'" + name + "' gives every one of Raft's blueprints" + (miss != null ? " - " + miss.Text : ""));
				if (tip != null) Log("  " + tip.Text);
				// (the plan editor's Progression panel shows the same, rule by rule)
				PlanChecker.Progress pr = PlanChecker.Progression(p, false);
				// (Raft's story islands it keeps are steps of their own now, at their places in play order: Rule -1)
				Check(ref ok, pr.Missing.Count == 0 && pr.Kept.Count + pr.Steps.Where(s => s.Rule >= 0).Sum(s => s.Blueprints.Count) >= all,
					"'" + name + "' progression: " + pr.Kept.Count + " kept, " + pr.Steps.Count(s => s.Rule >= 0 && s.Blueprints.Count > 0) + " rules give blueprints, " + pr.Missing.Count + " never given, " +
					pr.Steps.Sum(s => s.NeedsLate.Count) + " locks before their item");
				foreach (PlanChecker.Progress.Step st in pr.Steps.Where(st => st.Rule >= 0 && st.NeedsLate.Count > 0))
					Log("  rule " + st.Rule + " '" + p.Rules[st.Rule].Id + "' (" + p.Rules[st.Rule].WhatArg + "): wanted before any island gives it: " + string.Join(", ", st.NeedsLate.ToArray()) + " (its islands give: " + string.Join(", ", st.Gives.ToArray()) + ")");
				PlanProgressWindow.Open(p, null);
				if (PlanProgressWindow.IsOpen)
				{
					Check(ref ok, PlanProgressWindow.Rows.Count == pr.Steps.Count && !PlanProgressWindow.Rows.Any(r => r.StartsWith("NEVER GIVEN")),
						"the Progression panel shows " + PlanProgressWindow.Rows.Count + " rows: " + string.Join(" / ", PlanProgressWindow.Rows.Take(3).Select(r => r.Length > 90 ? r.Substring(0, 90) + "..." : r).ToArray()));
					PlanProgressWindow.Close();
				}
			}
			if (one.Length > 0) { if (ok) Log("PASS: plan blueprints '" + one + "'"); else Fail("plan blueprints '" + one + "'"); return; }
			var bare = new WorldPlan { Name = "CI no story", Random = true, RaftStory = false };
			PlanChecker.Finding none = PlanChecker.Check(bare, false, false).FirstOrDefault(x => x.Text.StartsWith("Raft's blueprints never given"));
			Check(ref ok, none != null && ScrambledBlueprints.OnIslands.Keys.All(b => none.Text.Contains(PlanChecker.Pretty(b))),
				"a plan with Raft's story off and no islands is told about all " + all + " blueprints");
			PlanChecker.Progress bp = PlanChecker.Progression(bare, false);
			Check(ref ok, bp.Missing.Count == all && bp.Kept.Count == 0 && bp.Steps.Count == 0, "its progression: " + bp.Missing.Count + " never given, " + bp.Kept.Count + " kept");
			PlanProgressWindow.Open(bare, null);
			if (PlanProgressWindow.IsOpen)
			{
				Check(ref ok, PlanProgressWindow.Rows.Count == 1 && PlanProgressWindow.Rows[0].StartsWith("NEVER GIVEN"), "the Progression panel shows it never given: " + string.Join(" / ", PlanProgressWindow.Rows.ToArray()).Substring(0, Math.Min(120, string.Join(" / ", PlanProgressWindow.Rows.ToArray()).Length)));
				PlanProgressWindow.Close();
			}
			else Log("  (the Progression panel isn't built here: run in the editor to check it too)");
			if (ok) Log("PASS: plan blueprints"); else Fail("plan blueprints");
		}

		/// <summary>Raft's story-island blueprints (raft_blueprints.txt) neither learned nor in the local player's inventory - the
		/// play test's "expect blueprints" at a plan's end (the user, 2026-10-18: nothing missing from the crafting menu).</summary>
		public static List<string> StoryBlueprintsNotLearned()
		{
			var missing = new List<string>();
			foreach (string bp in BlueprintSteps.StoryBlueprints())
				if (!BlueprintSteps.Has(bp)) missing.Add(bp + (BlueprintSteps.Exists(bp) ? "" : " (no such item)"));
			return missing;
		}
	}
}
