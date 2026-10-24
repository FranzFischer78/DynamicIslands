using System;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>The editor's Check warns about the quest traps of guide 12.4 (AU47): an island made in memory with a
		/// later step's chest and warthogs there from the start, a story item in a zone that fires again, a show/hide and a
		/// Raft item check on a defeat event, a Raft item check on the quest-done event and Raft items given by a note. The
		/// same island done right (hidden, an ambush, Once ever, show, story items) gets none of these warnings.</summary>
		[ConsoleCommand(name: "CICheckQuestTraps", docs: "Dev, anywhere: the island Check's quest-trap warnings for an island made in memory, and none for the same island done right")]
		public static void CheckQuestTraps(string[] args)
		{
			bool ok = true;
			string warthog = ContentCatalog.Creatures.First(k => k.Label == "Warthog").Name;
			List<PlanChecker.Finding> bad = TrapFindings(MakeTrapIsland(warthog, false));
			foreach (PlanChecker.Finding f in bad) Log("  " + f.Level + ": " + f.Text + " | fix: " + f.Fix);
			Func<string, string, bool> has = (a, b) => bad.Any(f => f.Level == PlanChecker.Level.Warning && f.Text.StartsWith("This island: ") && f.Text.Contains(a) && f.Text.Contains(b) && f.Fix.Length > 0);
			Check(ref ok, has("step 2 of the quest", "the chest \"Vault\""), "a later step's chest there from the start");
			Check(ref ok, has("step 3 of the quest", "warthog spot"), "a later step's warthogs there from the start");
			Check(ref ok, has("trigger zone 'gate'", "gives the story item"), "a story item in a zone that is ready again (Fires: Once)");
			Check(ref ok, has("show/hide 'bridge'", "flips the bridge or door back"), "show/hide on a defeat event that happens again");
			Check(ref ok, has("the defeat event of a warthog spot", "left out"), "a Raft item check on a defeat event");
			Check(ref ok, has("\"when its quest is done\" event", "left out"), "a Raft item check on the quest-done event");
			Check(ref ok, has("the note \"Diary\" gives", "first reader"), "Raft items given by a note");
			List<PlanChecker.Finding> good = TrapFindings(MakeTrapIsland(warthog, true));
			foreach (PlanChecker.Finding f in good.Where(y => y.Text.StartsWith("This island: "))) Log("  (right) " + f.Level + ": " + f.Text);
			Check(ref ok, !good.Any(f => f.Text.StartsWith("This island: ")), "the same island done right: no trap warnings");
			if (ok) Log("PASS: check quest traps"); else Fail("check quest traps");
		}

		/// <summary>What the island Check (an island's own rules, Island tab) says about an island made in memory.</summary>
		static List<PlanChecker.Finding> TrapFindings(IslandFile f)
		{
			PlanChecker.Facts own = PlanChecker.FromFile(f, f.Name, false);
			return PlanChecker.Check(new WorldPlan { Name = f.Name }, true, false, own);
		}

		/// <summary>An island with the traps (right = false), or the same island done right.</summary>
		static IslandFile MakeTrapIsland(string warthog, bool right)
		{
			var f = new IslandFile { Name = right ? "CI traps right" : "CI traps" };
			f.Props[IslandQuest.KeySteps] = "read|Diary|1|\nopen|Vault|1|\nkill|Warthog|2|";
			if (!right) f.Props[BehaviourProps.CheckKey("quest")] = "has|Plank|5";
			else f.Props[StoryItems.Key] = "ci-key|Gate key||The zone's key."; // (right: its story item in the Story items list - SL5 warns otherwise)
			Func<string, Dictionary<string, string>, IslandObject> obj = (name, props) => new IslandObject { Name = name, Position = Vector3.zero, Props = props };
			// The note of step 1: Raft items for its first reader (right: a story item, the crew's)
			f.Objects.Add(obj("CI_Note", new Dictionary<string, string> { { ObjectProps.NoteTitle, "Diary" }, { ObjectProps.NoteText, "Day one." },
				{ BehaviourProps.EventKey("read"), right ? "show|vault|\nshow|hogs|" : "give||Plank*3" } }));
			// The chest of step 2: there from the start (right: hidden until the note shows it)
			var chest = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Vault" }, { ObjectProps.LootItems, "Plank*2" }, { BehaviourProps.Name, "vault" } };
			if (right) chest[BehaviourProps.Hidden] = "1";
			f.Objects.Add(obj("CI_Chest", chest));
			// The warthogs of step 3: there from the start, and their defeat event shows/hides a bridge and checks planks
			// (right: an ambush, hidden until shown, and the defeat shows the bridge)
			var hogs = new Dictionary<string, string> { { ObjectProps.CreatureCount, "2" }, { BehaviourProps.Name, "hogs" }, { BehaviourProps.EventKey("defeat"), right ? "show|bridge|" : "toggle|bridge|" } };
			if (right) hogs[BehaviourProps.Hidden] = "1";
			else hogs[BehaviourProps.CheckKey("defeat")] = "take|Plank|2";
			f.Objects.Add(obj(warthog, hogs));
			// A zone with a story item: ready again after the regrow days (right: Once ever)
			var zone = new Dictionary<string, string> { { ObjectProps.ZoneId, "gate" }, { ObjectProps.LootItems, StoryItems.Ref("ci-key") + "*1" } };
			if (right) zone[ObjectProps.ZoneRepeat] = ObjectProps.ZoneOnceEver;
			f.Objects.Add(obj(ContentCatalog.TriggerZone, zone));
			f.Objects.Add(obj("CI_Bridge", new Dictionary<string, string> { { BehaviourProps.Name, "bridge" }, { BehaviourProps.Hidden, "1" } }));
			return f;
		}
	}
}
