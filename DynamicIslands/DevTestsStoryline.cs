using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommandUndoRedo;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Dev tests for the storyline builder (TEST_CATALOGUE section SL): the quest editor's buttons, the most quests an island has.</summary>
	public static partial class DevTests
	{
		#region SL1, SL2: the quest editor's buttons

		const BindingFlags SlAny = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

		static Transform SlWindow() { return EditorUI.Canvas != null ? EditorUI.Canvas.transform.Find("QuestEditorWindow") : null; }

		static string SlLabel(Button b) { Text t = UIKit.LabelOf(b); return t != null ? t.text : ""; }

		/// <summary>The window's visible buttons whose label is (or starts with) this text.</summary>
		static List<Button> SlButtons(string label, bool prefix, Transform under = null)
		{
			Transform w = under ?? SlWindow();
			if (w == null) return new List<Button>();
			return w.GetComponentsInChildren<Button>(false).Where(b => b.gameObject.activeInHierarchy && (prefix ? SlLabel(b).StartsWith(label) : SlLabel(b) == label)).ToList();
		}

		/// <summary>Presses the first such button (its onClick, as a click would). False when there is none.</summary>
		static bool SlPress(string label, bool prefix = false, Transform under = null)
		{
			List<Button> l = SlButtons(label, prefix, under);
			if (l.Count == 0) return false;
			l[0].onClick.Invoke();
			return true;
		}

		/// <summary>The step rows of the window, top to bottom.</summary>
		static List<Transform> SlRows()
		{
			Transform w = SlWindow();
			if (w == null) return new List<Transform>();
			return w.GetComponentsInChildren<Transform>(false).Where(t => t.name == "Step" && t.gameObject.activeInHierarchy && t.GetComponentsInChildren<Button>(false).Any(b => b.name == "Drop_StepType"))
				.OrderByDescending(t => t.position.y).ToList();
		}

		static List<string> SlTargets() { return SlRows().Select(r => { InputField f = r.GetComponentInChildren<InputField>(false); return f != null ? f.text : "?"; }).ToList(); }

		static void SlSetTarget(Transform row, string text)
		{
			InputField f = row.GetComponentInChildren<InputField>(false);
			f.text = text;
			f.onEndEdit.Invoke(text);
		}

		static object SlEditor() { return typeof(QuestEditorWindow).GetField("instance", SlAny).GetValue(null); }
		static T SlField<T>(string name) { return (T)typeof(QuestEditorWindow).GetField(name, SlAny).GetValue(SlEditor()); }
		static IslandQuest SlShownQuest() { return SlField<IslandQuest>("quest"); }

		static string SlSteps(IDictionary<string, string> props, int n) { return string.Join(",", IslandQuest.From(props, n, true).Steps.Select(s => s.Target).ToArray()); }

		static bool SlSameProps(IDictionary<string, string> a, IDictionary<string, string> b) { return a.Count == b.Count && a.All(kv => { string v; return b.TryGetValue(kv.Key, out v) && v == kv.Value; }); }

		static void SlQuestKeysOut(IDictionary<string, string> props)
		{
			foreach (string k in props.Keys.Where(k => k.StartsWith("quest")).ToList()) props.Remove(k);
			props.Remove(WorldDirector.IslandRulesKey);
		}

		/// <summary>Quest n typed into the window as the builder does: title, the first step's target, a reward (the item picker is a separate window).</summary>
		static void SlFillShown(string title, string target, string reward)
		{
			InputField t = SlField<InputField>("titleField");
			t.text = title;
			SlSetTarget(SlRows()[0], target);
			SlShownQuest().Reward = reward;
		}

		[ConsoleCommand(name: "CIQuestEditSteps", docs: "Dev, editor: SL1 + SL2 - the quest editor's buttons: + Add a step up to 10 (the 11th refused); up on the first and down on the last do nothing; up/down swap two; x removes one; Save keeps the order and drops an empty 'go to' step; Esc without Save changes nothing; + Another quest up to 9; Remove this quest on Quest 2 of 3 makes Quest 3 into Quest 2 (title, steps, reward); Save is one undo step (Ctrl+Z old quests, Ctrl+Y new); Remove quest takes all quests and the bring rule, one undo restores both; the island's settings and the undo list put back after")]
		public static void QuestEditStepsCommand() { StartTest(QuestEditStepsRoutine()); }

		static IEnumerator QuestEditStepsRoutine()
		{
			yield return WaitForEditor(false);
			if (!DynamicIslands.InEditor() || SlWindow() == null) { Fail("quest edit steps: the editor and its quest window are needed"); yield break; }
			bool ok = true;
			var propsBefore = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
			try
			{
				UndoRedoManager.Clear();
				SlQuestKeysOut(DynamicIslands.currentIslandProps);
				EditorUI.RefreshIsland();

				// ---- SL1: the step buttons
				QuestEditorWindow.Open();
				yield return null;
				Check(ref ok, QuestEditorWindow.IsOpen && SlRows().Count == 1, "an island without a quest: the window opens with one empty step (" + SlRows().Count + ")");
				for (int i = 0; i < 9; i++) { Check(ref ok, SlPress("+ Add a step"), "+ Add a step " + (i + 2)); yield return null; }
				Check(ref ok, SlRows().Count == 10, "10 steps after nine adds (" + SlRows().Count + ")");
				SlPress("+ Add a step");
				yield return null;
				Check(ref ok, SlRows().Count == 10, "the 11th step is refused (" + SlRows().Count + ")");
				var order = new List<string>();
				List<Transform> rows = SlRows();
				for (int i = 0; i < 10; i++) { SlSetTarget(rows[i], "a" + (i + 1)); order.Add("a" + (i + 1)); }
				SlField<InputField>("titleField").text = "Steps test";

				// (a button that does nothing at the ends: its click is allowed, nothing moves)
				SlPress("▲", false, SlRows()[0]);
				yield return null;
				SlPress("▼", false, SlRows()[9]);
				yield return null;
				Check(ref ok, SlTargets().SequenceEqual(order), "up on the first and down on the last do nothing: " + string.Join(",", SlTargets().ToArray()));
				SlPress("▲", false, SlRows()[2]); // a3 above a2
				yield return null;
				order = new List<string> { "a1", "a3", "a2", "a4", "a5", "a6", "a7", "a8", "a9", "a10" };
				Check(ref ok, SlTargets().SequenceEqual(order), "up on step 3 swaps it with step 2: " + string.Join(",", SlTargets().ToArray()));
				SlPress("▼", false, SlRows()[0]); // a1 below a3
				yield return null;
				order = new List<string> { "a3", "a1", "a2", "a4", "a5", "a6", "a7", "a8", "a9", "a10" };
				Check(ref ok, SlTargets().SequenceEqual(order), "down on step 1 swaps it with step 2: " + string.Join(",", SlTargets().ToArray()));
				SlPress("×", false, SlRows()[4]); // a5
				yield return null;
				order.RemoveAt(4);
				Check(ref ok, SlTargets().SequenceEqual(order), "x removes step 5: " + string.Join(",", SlTargets().ToArray()));
				SlPress("+ Add a step"); // an empty 'go to' step at the end
				yield return null;
				Check(ref ok, SlRows().Count == 10 && SlTargets().Last() == "", "a new empty step is added at the end (" + SlRows().Count + ")");
				Check(ref ok, SlPress("Save"), "Save is pressed");
				yield return null;
				IslandQuest saved = IslandQuest.From(DynamicIslands.currentIslandProps, 0, true);
				Check(ref ok, !QuestEditorWindow.IsOpen && string.Join(",", saved.Steps.Select(s => s.Target).ToArray()) == string.Join(",", order.ToArray()) && saved.Title == "Steps test",
					"Save keeps that order and drops the empty 'go to' step: " + string.Join(",", saved.Steps.Select(s => s.Target).ToArray()) + " (" + saved.Steps.Count + " steps)");
				Check(ref ok, UndoRedoManager.UndoCount == 1, "Save is one undo step (" + UndoRedoManager.UndoCount + ")");

				// Esc / close without Save
				var snap = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				QuestEditorWindow.Open();
				yield return null;
				SlPress("+ Add a step");
				SlField<InputField>("titleField").text = "Changed";
				SlPress("×", false, SlRows()[0]);
				yield return null;
				QuestEditorWindow.Close();
				yield return null;
				Check(ref ok, !QuestEditorWindow.IsOpen && SlSameProps(snap, DynamicIslands.currentIslandProps) && UndoRedoManager.UndoCount == 1, "closed without Save (Esc and Cancel both call Close): the island's settings are as they were, no undo step");
				QuestEditorWindow.Open();
				yield return null;
				SlPress("Cancel");
				yield return null;
				Check(ref ok, !QuestEditorWindow.IsOpen && SlSameProps(snap, DynamicIslands.currentIslandProps), "Cancel changes nothing");

				// ---- SL2: the island's quests
				UndoRedoManager.Clear();
				SlQuestKeysOut(DynamicIslands.currentIslandProps);
				QuestEditorWindow.Open();
				yield return null;
				SlFillShown("Main", "m1", "");
				SlPress("+ Another quest");
				yield return null;
				Check(ref ok, SlField<int>("number") == 1 && SlRows().Count == 1, "+ Another quest opens Quest 2 with one empty step");
				SlFillShown("Two", "t2", "Plank*2");
				SlPress("+ Another quest");
				yield return null;
				SlFillShown("Three", "t3", "Rope*3");
				SlPress("Main quest", true);
				yield return null;
				Check(ref ok, SlField<InputField>("titleField").text == "Main" && SlTargets().SequenceEqual(new[] { "m1" }), "back on the main quest its title and step are kept");
				SlPress("Quest 3", true);
				yield return null;
				Check(ref ok, SlField<InputField>("titleField").text == "Three" && SlTargets().SequenceEqual(new[] { "t3" }), "Quest 3 keeps its own title and step");
				SlPress("Main quest", true);
				yield return null;
				QuestEditorWindow.SetQuestBringRule(DynamicIslands.currentIslandProps, null);
				// (the bring rule is set on the window as the builder does: kind "A new island")
				DropList.Click(SlField<Button>("bringKindButton"), "type");
				yield return null;
				int undoBefore = UndoRedoManager.UndoCount;
				var preSave = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				SlPress("Save");
				yield return null;
				var post3 = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				Check(ref ok, IslandQuest.CountIn(post3) == 3 && IslandQuest.From(post3, 1, true).Title == "Two" && IslandQuest.From(post3, 2, true).Title == "Three" && IslandQuest.From(post3, 1, true).Reward == "Plank*2" && IslandQuest.From(post3, 2, true).Reward == "Rope*3" && SlSteps(post3, 0) == "m1" && SlSteps(post3, 1) == "t2" && SlSteps(post3, 2) == "t3",
					"Save keeps three quests (titles, steps, rewards)");
				Check(ref ok, QuestEditorWindow.QuestBringRule(post3) != null, "the main quest's 'bring a new island' rule is saved");
				Check(ref ok, UndoRedoManager.UndoCount == undoBefore + 1, "Save of three quests is one undo step (" + (UndoRedoManager.UndoCount - undoBefore) + ")");

				// + Another quest up to 9, then none
				QuestEditorWindow.Open();
				yield return null;
				for (int i = 0; i < 6; i++) { Check(ref ok, SlPress("+ Another quest"), "+ Another quest (quest " + (4 + i) + ")"); yield return null; }
				int tabs = SlButtons("Main quest", true).Count + SlButtons("Quest ", true).Count;
				Check(ref ok, tabs == IslandQuest.MaxQuests && SlButtons("+ Another quest", false).Count == 0, "nine quests, and then no + Another quest (" + tabs + " tabs)");
				QuestEditorWindow.Close();
				yield return null;
				Check(ref ok, SlSameProps(post3, DynamicIslands.currentIslandProps), "the six empty extra quests closed without Save changed nothing");

				// Remove this quest on Quest 2 of 3
				QuestEditorWindow.Open();
				yield return null;
				SlPress("Quest 2", true);
				yield return null;
				Check(ref ok, SlField<int>("number") == 1 && SlPress("Remove this quest"), "on Quest 2, Remove this quest is pressed");
				yield return null;
				Check(ref ok, SlButtons("Quest 3", true).Count == 0 && SlButtons("Quest 2", true).Count == 1 && SlLabel(SlButtons("Quest 2", true)[0]).Contains("Three"), "Quest 3 is now Quest 2 in the tabs (" + string.Join(" | ", SlButtons("Quest ", true).Select(SlLabel).ToArray()) + ")");
				SlPress("Save");
				yield return null;
				var after2 = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				Check(ref ok, IslandQuest.CountIn(after2) == 2 && IslandQuest.From(after2, 1, true).Title == "Three" && IslandQuest.From(after2, 1, true).Reward == "Rope*3" && SlSteps(after2, 1) == "t3" && !after2.ContainsKey("quest3.steps") && !after2.ContainsKey("quest3.title") && !after2.ContainsKey("quest3.reward"),
					"saved: the old Quest 3 is Quest 2 with its title, steps and reward, and no quest3 keys are left");
				Check(ref ok, UndoRedoManager.UndoCount == undoBefore + 2, "that Save is one more undo step");
				UndoRedoManager.Undo();
				yield return null;
				Check(ref ok, SlSameProps(post3, DynamicIslands.currentIslandProps), "Ctrl+Z brings the three quests back");
				UndoRedoManager.Redo();
				yield return null;
				Check(ref ok, SlSameProps(after2, DynamicIslands.currentIslandProps), "Ctrl+Y brings the two quests again");
				UndoRedoManager.Undo();
				UndoRedoManager.Undo();
				yield return null;
				Check(ref ok, SlSameProps(preSave, DynamicIslands.currentIslandProps), "two undos: the island's settings as before the first Save (no quests)");
				UndoRedoManager.Redo();
				UndoRedoManager.Redo();
				yield return null;

				// Remove quest: all quests and the bring rule, one undo
				IntroRule keep = IntroRule.Parse("keepme | island:cislkeep | start | ahead:300 | | ");
				if (keep != null)
				{
					List<IntroRule> rules = IntroRule.ParseLines(ObjectProps.Get(DynamicIslands.currentIslandProps, WorldDirector.IslandRulesKey));
					rules.Add(keep);
					DynamicIslands.currentIslandProps[WorldDirector.IslandRulesKey] = IntroRule.ToLines(rules);
				}
				else Log("  (no second rule parsed: only the bring rule checked)");
				var pre = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				int u = UndoRedoManager.UndoCount;
				QuestEditorWindow.Open();
				yield return null;
				SlPress("Quest 2", true);
				yield return null;
				Check(ref ok, SlPress("Remove quest"), "on the Quest 2 tab, Remove quest is pressed");
				yield return null;
				var none = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				Check(ref ok, !QuestEditorWindow.IsOpen && IslandQuest.CountIn(none) == 1 && !IslandQuest.From(none, 0, true).Exists && !IslandQuest.From(none, 1, true).Exists && QuestEditorWindow.QuestBringRule(none) == null && !none.Keys.Any(k => k.StartsWith("quest")),
					"every quest and the bring rule are gone");
				if (keep != null) Check(ref ok, IntroRule.ParseLines(ObjectProps.Get(none, WorldDirector.IslandRulesKey)).Any(r => r.Id == "keepme"), "the island's other rule is kept");
				Check(ref ok, UndoRedoManager.UndoCount == u + 1, "Remove quest is one undo step (" + (UndoRedoManager.UndoCount - u) + ")");
				UndoRedoManager.Undo();
				yield return null;
				Check(ref ok, SlSameProps(pre, DynamicIslands.currentIslandProps) && QuestEditorWindow.QuestBringRule(DynamicIslands.currentIslandProps) != null && IslandQuest.CountIn(DynamicIslands.currentIslandProps) == 2, "one Ctrl+Z restores the quests and the bring rule together");
				UndoRedoManager.Redo();
				yield return null;
				Check(ref ok, SlSameProps(none, DynamicIslands.currentIslandProps), "Ctrl+Y removes them again");
			}
			finally
			{
				QuestEditorWindow.Close();
				DynamicIslands.currentIslandProps.Clear();
				foreach (var kv in propsBefore) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
				UndoRedoManager.Clear();
				EditorUI.RefreshIsland();
			}
			if (ok) Log("PASS: quest editor steps and quests"); else Fail("quest editor steps and quests");
		}

		#endregion

		#region SL10: the most quests

		static int SlKey(string method, params object[] args) { return (int)typeof(QuestTracker).GetMethod(method, SlAny).Invoke(null, args); }
		static readonly string[] SlTypes = { "reach", "read", "open", "kill", "catch" };

		static int SlCount(int i) { return i % 5 == 3 ? 3 : i % 5 == 4 ? 2 : 1; }

		[ConsoleCommand(name: "CIMaxQuests", docs: "Dev, in game (host): SL10 - an island with 9 quests of 10 steps each (reach, read, open, kill x3, catch x2), a step of each done early, all done in a mixed order: every event moves exactly the right quest to the right step and progress; the journal's count has 9 lines, the panel shows; no two state keys alike (0x40000-0x4053F); the state encoded and decoded, and the island reloaded, are the same")]
		public static void MaxQuestsCommand() { StartTest(MaxQuestsRoutine()); }

		static IEnumerator MaxQuestsRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("max quests: run in a world, as the host"); yield break; }
			bool ok = true;
			yield return EnsureAlive();
			const string name = "cimaxquests";
			const int Q = 9, S = 10;
			Vector3 playerBefore = player.transform.position;
			QuestRewards.ForgetIsland(name);
			IslandWorldState.Remove(name);
			try
			{
				var gs = new IslandGenSettings { Seed = 1010, Radius = 30f, Height = 6f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
				IslandFile f = IslandGenerator.CreateFile(gs, name);
				for (int n = 0; n < Q; n++)
				{
					var q = new IslandQuest { Title = "MQ" + n };
					for (int i = 0; i < S; i++) q.Steps.Add(new IslandQuest.Step { Type = SlTypes[i % 5], Target = "q" + n + "s" + i, Count = SlCount(i) });
					q.To(f.Props, n);
				}
				Check(ref ok, IslandQuest.CountIn(f.Props) == Q && Enumerable.Range(0, Q).All(n => IslandQuest.From(f.Props, n, true).Steps.Count == S), "nine quests of ten steps in the island's settings");
				f.Save(IslandSpawner.PathFor(name));
				Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(name), 450f);
				if (!spot.HasValue) { Fail("max quests: no open sea near the raft"); yield break; }
				yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
				IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
				if (e == null || e.Root == null) { Fail("max quests: " + name + " did not spawn"); yield break; }
				player.transform.position = LandSpot(e) + Vector3.up * 1.5f;
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, QuestTracker.QuestsOf(e) == Q && Enumerable.Range(0, Q).All(n => QuestTracker.QuestOf(e, n).Steps.Count == S && QuestTracker.StepOf(e, n) == 0), "the spawned island has " + QuestTracker.QuestsOf(e) + " quests of ten steps, all at the first step");

				// The journal's count before: nine lines, none done
				List<QuestCount.Quest> before = QuestCount.All().Where(c => c.Name.Contains("MQ")).ToList();
				Check(ref ok, before.Count == Q && before.All(c => !c.Done) && Enumerable.Range(0, Q).All(n => before.Any(c => c.Name.EndsWith("MQ" + n))), "the journal's count has the nine quests, none done (" + before.Count + ")");

				// State keys: none alike
				var keys = new List<int>();
				for (int n = 0; n < Q; n++)
				{
					keys.Add(SlKey("StepKeyOf", n));
					keys.Add(SlKey("ProgressKeyOf", n));
					for (int i = 0; i < S; i++) keys.Add(SlKey("EarlyKeyOf", n, i));
				}
				Check(ref ok, keys.Distinct().Count() == keys.Count, "no two state keys alike: " + keys.Count + " keys for 9 quests x (step, progress, 10 early)");
				Check(ref ok, keys.All(k => k >= 0x40000 && k <= 0x4053F), "all in 0x40000-0x4053F (" + keys.Min().ToString("X") + "-" + keys.Max().ToString("X") + ")");

				// Early work: step 8 (open) and step 10 (catch x2) before step 1
				for (int n = 0; n < Q; n++)
				{
					QuestTracker.Event(e, "open", "q" + n + "s7");
					QuestTracker.Event(e, "catch", "q" + n + "s9");
					QuestTracker.Event(e, "catch", "q" + n + "s9");
				}
				var problems = new List<string>();
				for (int n = 0; n < Q; n++)
				{
					ObjectState s7, s9;
					bool h7 = e.State.TryGetValue(SlKey("EarlyKeyOf", n, 7), out s7), h9 = e.State.TryGetValue(SlKey("EarlyKeyOf", n, 9), out s9);
					if (QuestTracker.StepOf(e, n) != 0) problems.Add("q" + n + " moved to step " + QuestTracker.StepOf(e, n));
					if (!h7 || s7.Yield != 1 || !h9 || s9.Yield != 2) problems.Add("q" + n + " early work " + (h7 ? s7.Yield.ToString() : "-") + "/" + (h9 ? s9.Yield.ToString() : "-"));
				}
				Check(ref ok, problems.Count == 0, "step 8 and step 10 of every quest done early are kept for each, no quest moved" + (problems.Count > 0 ? ": " + string.Join("; ", problems.ToArray()) : ""));

				// The events of each quest in order, with what each leaves behind
				var queue = new List<List<string[]>>(); // per quest: type, target, expected step, expected progress
				for (int n = 0; n < Q; n++)
				{
					var l = new List<string[]>();
					foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 6, 8 })
					{
						int c = SlCount(i), next = i == 6 ? 8 : i + 1;
						if (i == 8) next = 10;
						for (int k = 1; k <= c; k++) l.Add(new[] { SlTypes[i % 5], "q" + n + "s" + i, (k == c ? next : i).ToString(), (k == c ? 0 : k).ToString() });
					}
					queue.Add(l);
				}
				// (mixed order: each round the quests take turns from a different start, one event each)
				problems.Clear();
				int round = 0, fired = 0;
				bool panelChecked = false;
				while (queue.Any(l => l.Count > 0))
				{
					for (int j = 0; j < Q; j++)
					{
						int n = (j * 4 + round) % Q;
						if (queue[n].Count == 0) continue;
						string[] ev = queue[n][0];
						queue[n].RemoveAt(0);
						var stepsBefore = Enumerable.Range(0, Q).Select(x => QuestTracker.StepOf(e, x)).ToArray();
						QuestTracker.Event(e, ev[0], ev[1]);
						fired++;
						for (int x = 0; x < Q; x++)
						{
							int want = x == n ? int.Parse(ev[2]) : stepsBefore[x];
							int got = QuestTracker.StepOf(e, x);
							if (got != want) problems.Add("event " + fired + " (" + ev[1] + "): quest " + x + " at step " + got + ", expected " + want);
						}
						ObjectState ps;
						int prog = e.State.TryGetValue(SlKey("ProgressKeyOf", n), out ps) ? ps.Yield : 0;
						if (prog != int.Parse(ev[3])) problems.Add("event " + fired + " (" + ev[1] + "): progress " + prog + ", expected " + ev[3]);
					}
					round++;
					yield return null;
					if (round == 5 && !panelChecked)
					{
						panelChecked = true;
						yield return new WaitForSeconds(1.5f);
						Check(ref ok, QuestTracker.PanelShown, "half way, standing on the island: the quest panel shows");
						List<QuestCount.Quest> mid = QuestCount.All().Where(c => c.Name.Contains("MQ")).ToList();
						Check(ref ok, mid.Count == Q && mid.All(c => !c.Done), "half way: still nine lines in the journal's count, none done (" + mid.Count + ")");
					}
				}
				Check(ref ok, problems.Count == 0, fired + " events in a mixed order: every one moved only its own quest to the right step and progress" + (problems.Count > 0 ? " - first problems: " + string.Join("; ", problems.Take(4).ToArray()) : ""));
				yield return new WaitForSeconds(1f);
				Check(ref ok, Enumerable.Range(0, Q).All(n => QuestTracker.IsDone(e, n) && QuestTracker.StepOf(e, n) == S), "all nine quests are done (steps " + string.Join(",", Enumerable.Range(0, Q).Select(n => QuestTracker.StepOf(e, n).ToString()).ToArray()) + ")");
				var stepKeys = new HashSet<int>(Enumerable.Range(0, Q).Select(n => SlKey("StepKeyOf", n)));
				List<int> inRange = e.State.Keys.Where(k => k >= 0x40000 && k <= 0x4053F).ToList();
				Check(ref ok, inRange.Count == Q && inRange.All(stepKeys.Contains), "left in the state: the nine step keys only, the progress and early keys are gone (" + inRange.Count + " keys)");
				List<QuestCount.Quest> after = QuestCount.All().Where(c => c.Name.Contains("MQ")).ToList();
				Check(ref ok, after.Count == Q && after.All(c => c.Done), "the journal's count: nine quests, all done (" + after.Count(c => c.Done) + " done)");

				// Save and load: the state written and read, and the island reloaded
				var copy = e.State.Where(kv => kv.Key >= 0x40000 && kv.Key <= 0x4053F).ToDictionary(kv => kv.Key, kv => kv.Value);
				Dictionary<int, ObjectState> decoded = IslandObjectState.Decode(IslandObjectState.Encode(e.State));
				Check(ref ok, copy.All(kv => { ObjectState d; return decoded.TryGetValue(kv.Key, out d) && d.Yield == kv.Value.Yield && d.Active == kv.Value.Active && d.Day == kv.Value.Day; }), "the world file's state line (encoded, decoded) has every quest key as it was");
				yield return ReloadIslandRoutine(e);
				yield return new WaitForSeconds(1f);
				Check(ref ok, copy.All(kv => { ObjectState d; return e.State.TryGetValue(kv.Key, out d) && d.Yield == kv.Value.Yield; }) && Enumerable.Range(0, Q).All(n => QuestTracker.IsDone(e, n)), "after the island is reloaded all nine quests are still done");
			}
			finally
			{
				IslandWorldState.Remove(name);
				try { System.IO.File.Delete(IslandSpawner.PathFor(name)); } catch { }
				QuestRewards.ForgetIsland(name);
				if (player != null) player.transform.position = playerBefore;
			}
			if (ok) Log("PASS: max quests"); else Fail("max quests");
		}

		#endregion
	}
}
