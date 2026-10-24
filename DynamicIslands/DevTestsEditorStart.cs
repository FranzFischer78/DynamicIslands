using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Tests of TODO 5b "Islands and world plans apart": the main menu's EDITOR opens a start window (an island, a world plan,
	/// the guide), and world plans are made on the main menu with no island loaded.
	/// </summary>
	public static partial class DevTests
	{
		static bool OnRaftMenu() { return !DynamicIslands.InEditor() && !LoadSceneManager.IsGameSceneLoaded && GameObject.Find("MainMenuCanvas") != null; }

		/// <summary>Errors and exceptions logged while a test runs (not the tests' own lines).</summary>
		static Application.LogCallback WatchErrors(List<string> into)
		{
			Application.LogCallback watch = (msg, trace, type) =>
			{
				if ((type == LogType.Exception || type == LogType.Error) && !msg.StartsWith("[CITEST]")) into.Add(msg.Split('\n')[0]);
			};
			Application.logMessageReceived += watch;
			return watch;
		}

		static void StartShot(string name) { Screenshot(new[] { "split_" + name }); }

		static string ReadStartFile()
		{
			string p = Path.Combine(DynamicIslands.assetpath, "editor_start.txt");
			try { return File.Exists(p) ? File.ReadAllText(p) : null; } catch { return null; }
		}

		static void PutStartFileBack(string text)
		{
			string p = Path.Combine(DynamicIslands.assetpath, "editor_start.txt");
			try { if (text != null) File.WriteAllText(p, text); else if (File.Exists(p)) PiecesFiles.MoveToDeleted(p, "ci"); } catch { }
		}

		static void PlanAway(string name)
		{
			try { string p = WorldPlan.PathFor(name); if (File.Exists(p)) PiecesFiles.MoveToDeleted(p, "plans"); } catch { }
		}

		[ConsoleCommand(name: "CICreateWindow", docs: "Dev, main menu: TODO 5b - the main menu's EDITOR opens the start window over the menu (no editor loaded): three cards (an island, a world plan, the guide) with their buttons, fitting 1024 x 768; the guide button opens the guide; Open an island... lists the islands; New world plan asks how to start (empty, Main story line, templates, copies), then the name; the plan opens on the main menu with the template's rules; closed, the start window offers Continue on it and shows its Main story line; Continue opens it again. The test plan goes to the deleted folder after. CICreateWindow shot: also pictures (shot_split_*.png)")]
		public static void CreateWindowCommand(string[] args) { StartTest(CreateWindowRoutine(args != null && args.Contains("shot"))); }

		static IEnumerator CreateWindowRoutine(bool shots)
		{
			if (!OnRaftMenu()) { Fail("start window: run it on the main menu"); yield break; }
			bool ok = true;
			var errors = new List<string>();
			Application.LogCallback watch = WatchErrors(errors);
			HelpLinks.TestMode = true;
			string startFile = ReadStartFile();
			string name = "CI Start " + DateTime.Now.ToString("HHmmss");
			try
			{
				Transform ed = GameObject.Find("MainMenuCanvas").transform.Find("MenuButtons/EDITOR");
				Check(ref ok, ed != null, "the main menu has EDITOR");
				if (ed != null) ed.GetComponent<Button>().onClick.Invoke();
				yield return null; yield return null;
				Check(ref ok, CreateWindow.IsOpen && !DynamicIslands.InEditor(), "EDITOR opens the start window over the main menu, no editor loaded");
				GameObject win = WindowObject(typeof(CreateWindow));
				if (win == null) { Fail("start window"); yield break; }
				foreach (string card in new[] { "Card_Island", "Card_Plan", "Card_Guide" })
					Check(ref ok, win.GetComponentsInChildren<RectTransform>(false).Any(r => r.name == card), "the card " + card);
				foreach (string b in new[] { "New island", "Open an island...", "New world plan", "Open a world plan...", "Guide" })
					Check(ref ok, win.GetComponentsInChildren<Button>(false).Any(x => x.name == "Button_" + b), "the button " + b);
				List<string> over = UIKit.RowOverflow((RectTransform)win.transform.Find("Panel"), new Vector2(1024, 768));
				Check(ref ok, over.Count == 0, "it fits 1024 x 768" + (over.Count > 0 ? ": " + string.Join("; ", over.ToArray()) : ""));
				if (shots) { StartShot("start_window"); yield return new WaitForSecondsRealtime(1f); }

				HelpLinks.LastOpened = null;
				Check(ref ok, Click(win, "Guide") && !string.IsNullOrEmpty(HelpLinks.LastOpened), "Open the guide opens the guide (" + HelpLinks.LastOpened + ")");

				Check(ref ok, Click(win, "Open an island..."), "Open an island... clicked");
				yield return null;
				for (float t = 0; t < 30f && ChoiceWindow.Loading; t += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.Title == "OPEN AN ISLAND", "Open an island... lists the saved islands (" + ChoiceWindow.Values.Count + ")");
				ChoiceWindow.Close();
				yield return null;

				Check(ref ok, Click(win, "New world plan"), "New world plan clicked");
				yield return null; yield return null;
				List<string> how = ChoiceWindow.Values;
				Check(ref ok, ChoiceWindow.IsOpen && how.Contains(WorldPlanWindow.NewEmpty) && how.Contains(WorldPlanWindow.NewStory) && how.Any(v => v.StartsWith(WorldPlanWindow.NewTemplatePrefix)),
					"New world plan asks how to start: " + how.Count + " choices (empty, Main story line, templates, copies)");
				if (shots) { StartShot("new_plan_ask"); yield return new WaitForSecondsRealtime(1f); }
				string template = WorldPlanTemplates.All.Select(x => x.Key).FirstOrDefault(k => WorldPlanTemplates.Get(k).Rules.Any(r => r.InStory)) ?? WorldPlanTemplates.All[0].Key;
				int templateRules = WorldPlanTemplates.Get(template).Rules.Count;
				Check(ref ok, ChoiceWindow.PickValue(WorldPlanWindow.NewTemplatePrefix + template), "from the template '" + template + "'");
				yield return null; yield return null;
				GameObject prompt = WindowObject(typeof(TextPromptWindow));
				Check(ref ok, prompt != null && TypeInto(prompt, "Name", name) && Click(prompt, "OK"), "the name prompt: '" + name + "', OK");
				yield return null; yield return null;
				GameObject plan = WindowObject(typeof(WorldPlanWindow));
				Check(ref ok, plan != null && MenuWindows.Root != null && plan.transform.IsChildOf(MenuWindows.Root) && !DynamicIslands.InEditor(), "the plan opens on the main menu");
				Check(ref ok, WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == name && File.Exists(WorldPlan.PathFor(name)), "the new plan '" + name + "' is shown and saved");
				Check(ref ok, WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Rules.Count == templateRules, "it has the template's " + templateRules + " rules (" + (WorldPlanWindow.Plan != null ? WorldPlanWindow.Plan.Rules.Count : -1) + ")");
				Check(ref ok, EditorEntry.LastPlan == name && EditorEntry.Last == EditorEntry.Plan, "it is remembered as the last plan");
				if (shots) { StartShot("plan_on_menu"); yield return new WaitForSecondsRealtime(1f); }
				if (plan != null) Check(ref ok, Click(plan, "Close"), "the plan's Close");
				yield return null; yield return null;
				Check(ref ok, !WorldPlanWindow.IsOpen && CreateWindow.IsOpen, "closing the plan shows the start window again");
				Button cont = win.GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.name == "Button_ContinuePlan");
				Check(ref ok, cont != null && UIKit.LabelOf(cont).text.Contains(name), "the plan card offers Continue '" + name + "'");
				Text story = win.GetComponentsInChildren<Text>(false).FirstOrDefault(x => x.name == "PlanStory");
				Check(ref ok, story != null && story.text.Contains("Main story line") && story.text.Contains("1. "), "the plan card shows its Main story line: " + (story != null ? story.text.Replace("\n", " / ") : "none"));
				if (shots) { StartShot("start_window_continue"); yield return new WaitForSecondsRealtime(1f); }
				if (cont != null) cont.onClick.Invoke();
				yield return null;
				Check(ref ok, WorldPlanWindow.IsOpen && WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == name, "Continue opens the plan again");
				WorldPlanWindow.Close();
				CreateWindow.Close();
				Check(ref ok, errors.Count == 0, "no errors (" + string.Join("; ", errors.Take(3).ToArray()) + ")");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				HelpLinks.TestMode = false;
				PlanAway(name);
				PutStartFileBack(startFile);
			}
			if (ok) Log("PASS: start window"); else Fail("start window");
		}

		[ConsoleCommand(name: "CIPlanFromMenu", docs: "Dev, main menu: TODO 5b - a world plan on the main menu with no island loaded: Check runs there; Test this plan makes a new world 'Plan test <time>' with it, its island comes, the world window offers Back to the world plan, and back goes to the main menu with the start window and the plan open again (not the editor). Several minutes; the test plan goes to the deleted folder after")]
		public static void PlanFromMenuCommand(string[] args) { StartTest(PlanFromMenuRoutine()); }

		static IEnumerator PlanFromMenuRoutine()
		{
			if (!OnRaftMenu()) { Fail("plan from the menu: run it on the main menu"); yield break; }
			bool ok = true;
			var errors = new List<string>();
			Application.LogCallback watch = WatchErrors(errors);
			string startFile = ReadStartFile();
			string island = IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().FirstOrDefault(n => !n.StartsWith("gen-") && !n.StartsWith("ci"));
			var wp = new WorldPlan { Name = "CI Menu Plan", Random = false, Description = "A plan tested from the main menu (CIPlanFromMenu)" };
			wp.Rules.Add(IntroRule.Parse("first | island:" + island + " | start | ahead:300 | A first island | First"));
			wp.Save();
			try
			{
				CreateWindow.Open();
				WorldPlanWindow.Open(wp.Name);
				yield return null; yield return null;
				GameObject pw = WindowObject(typeof(WorldPlanWindow));
				Check(ref ok, pw != null && pw.transform.IsChildOf(MenuWindows.Root) && WorldPlanWindow.Plan.Name == wp.Name, "the plan opens on the main menu");
				if (pw == null) { Fail("plan from the menu"); yield break; }
				Check(ref ok, Click(pw, "Check"), "Check clicked");
				for (float t = 0; t < 20f && !PlanCheckWindow.IsOpen; t += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
				Check(ref ok, PlanCheckWindow.IsOpen, "Check's report opens on the main menu");
				PlanCheckWindow.Close();
				yield return null;
				Check(ref ok, Click(pw, "Test this plan"), "Test this plan clicked");
				yield return null;
				Check(ref ok, IslandTest.Busy && IslandTest.PlanToTest == wp.Name && IslandTest.FromMenu, "Test this plan: on its way from the main menu");
				for (float t = 0; t < 300f && !IslandTest.Testing; t += 1f) yield return new WaitForSeconds(1f);
				for (float t = 0; t < 40f && !IslandWorldState.Islands.Any(x => x.HostName.Equals(island, StringComparison.OrdinalIgnoreCase)); t += 1f) yield return new WaitForSeconds(1f);
				string world = SaveAndLoad.CurrentGameFileName ?? "";
				Check(ref ok, IslandTest.Testing && world.StartsWith("Plan test"), "in a new world '" + world + "' (" + IslandTest.LastStep + ")");
				Check(ref ok, IslandWorldState.Islands.Any(x => x.HostName.Equals(island, StringComparison.OrdinalIgnoreCase)), "the plan's first island '" + island + "' came");
				WorldWindow.Open();
				yield return null;
				Button back = WorldWindow.ButtonNamed("BackToEditor");
				Check(ref ok, back != null && back.gameObject.activeInHierarchy && UIKit.LabelOf(back).text == "Back to the world plan", "the world window offers Back to the world plan (" + (back != null ? UIKit.LabelOf(back).text : "none") + ")");
				if (back != null) back.onClick.Invoke();
				for (float t = 0; t < 240f && (IslandTest.Busy || !WorldPlanWindow.IsOpen); t += 1f) yield return new WaitForSeconds(1f);
				yield return new WaitForSeconds(1f);
				Check(ref ok, OnRaftMenu() && WorldPlanWindow.IsOpen && WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == wp.Name, "back on the main menu with the plan open");
				Check(ref ok, CreateWindow.IsOpen, "the start window is under it");
				WorldPlanWindow.Close();
				CreateWindow.Close();
				Check(ref ok, errors.Count == 0, "no errors (" + string.Join("; ", errors.Take(3).ToArray()) + ")");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				PlanAway(wp.Name);
				PutStartFileBack(startFile);
			}
			if (ok) Log("PASS: plan from the menu"); else Fail("plan from the menu");
		}

		/// <summary>A plan window tab clicked as a player clicks it ("Tab_Story", "Tab_Other", "Tab_Settings").</summary>
		static bool ClickTab(GameObject win, string name)
		{
			Button b = win != null ? win.GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.name == name) : null;
			if (b == null) return false;
			b.onClick.Invoke();
			return true;
		}

		static GameObject TopBar() { RectTransform t = UnityEngine.Object.FindObjectsOfType<RectTransform>().FirstOrDefault(x => x.name == "TopBar"); return t != null ? t.gameObject : null; }

		/// <summary>The card of a rule in the open plan, found by its id (the window holds the plan read from its file again).</summary>
		static GameObject StoryCard(WorldPlan p, IntroRule r)
		{
			RectTransform card = WorldPlanWindow.CardFor(p.Rules.FindIndex(x => x.Id == r.Id));
			return card != null ? card.gameObject : null;
		}

		static string SomeSavedIsland() { return IslandSpawner.ListSavedIslands().FirstOrDefault(n => n == "Crowfield Farm") ?? IslandSpawner.ListSavedIslands().FirstOrDefault(n => !n.StartsWith("gen-") && !n.StartsWith("ci")); }

		static Button TopBarButton(GameObject bar, string label) { return bar == null ? null : bar.GetComponentsInChildren<Button>(true).FirstOrDefault(b => UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == label); }

		[ConsoleCommand(name: "CIUseInPlan", docs: "Dev, editor: TODO 5b - the island editor's Use in a plan...: a saved island opened; Use in a plan... lists A new world plan... and the plans; a new plan 'CI Use <time>' with the island next in its Main story line (the plan opens on it; the file has it in the story, the last island before Raft's ending); again into the same plan as another island (not in the story); the rule card's Edit island opens the island, the top bar shows Back to the plan in place of Use in a plan... and still fits 1024 x 768; Back to the plan opens the plan again in the editor. The test plan goes to the deleted folder after")]
		public static void UseInPlanCommand(string[] args) { StartTest(UseInPlanRoutine()); }

		static IEnumerator UseInPlanRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("use in a plan: in the editor"); yield break; }
			bool ok = true;
			var errors = new List<string>();
			Application.LogCallback watch = WatchErrors(errors);
			string startFile = ReadStartFile();
			string island = SomeSavedIsland();
			string name = "CI Use " + DateTime.Now.ToString("HHmmss");
			try
			{
				Check(ref ok, island != null && DynamicIslands.LoadIsland(island), "the island '" + island + "' opened");
				// (objects from other islands load over a few seconds: the island has its name once they are in)
				for (float t = 0; t < 60f && (DynamicIslands.IsUnnamed || DynamicIslands.currentIslandName != island); t += 0.5f) yield return new WaitForSeconds(0.5f);
				yield return null; yield return null;
				GameObject bar = TopBar();
				Check(ref ok, bar != null && Click(bar, "Use in a plan..."), "the top bar's Use in a plan...");
				yield return null;
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.Values.Contains(EditorEntry.UseNewPlan), "it lists A new world plan... and " + (ChoiceWindow.Values.Count - 1) + " plan(s)");
				Check(ref ok, ChoiceWindow.PickValue(EditorEntry.UseNewPlan), "A new world plan...");
				yield return null; yield return null;
				GameObject prompt = WindowObject(typeof(TextPromptWindow));
				Check(ref ok, prompt != null && TypeInto(prompt, "Name", name) && Click(prompt, "OK"), "named '" + name + "'");
				yield return null; yield return null;
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.PickValue(EditorEntry.UseInStory), "as next in the Main story line");
				yield return null; yield return null;
				WorldPlan p = File.Exists(WorldPlan.PathFor(name)) ? WorldPlan.Load(name) : null;
				IntroRule story = p != null ? p.Rules.FirstOrDefault(r => r.WhatArg == island) : null;
				Check(ref ok, story != null && story.InStory && story.Where == "receiver", "the plan file has it in the Main story line (" + (story != null ? story.StoryPlace : "none") + ")");
				List<string> steps = p != null ? StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules) : new List<string>();
				int at = story != null ? steps.FindIndex(x => !StoryChain.IsRaft(x) && StoryChain.RuleIdOf(x) == story.Id) : -1;
				Check(ref ok, at >= 0 && (at == steps.Count - 1 || (at == steps.Count - 2 && StoryChain.TypeOfStep(steps[steps.Count - 1]) == ChunkPointType.Landmark_Utopia)), "it is the story's last island before Raft's ending (step " + (at + 1) + " of " + steps.Count + ")");
				Check(ref ok, WorldPlanWindow.IsOpen && WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == name, "the plan opens in the editor");
				WorldPlanWindow.Close();
				yield return null;

				Check(ref ok, Click(bar, "Use in a plan..."), "Use in a plan... again");
				yield return null;
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.PickValue(name), "the same plan from the list");
				yield return null; yield return null;
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.PickValue(EditorEntry.UseOther), "as another island");
				yield return null; yield return null;
				p = WorldPlan.Load(name);
				Check(ref ok, p != null && p.Rules.Count == 2 && p.Rules.Count(r => r.WhatArg == island && !r.MainStory && r.When == "start") == 1, "the plan has a second rule, not in the story (" + (p != null ? p.Rules.Count : 0) + " rules)");

				GameObject plan = WindowObject(typeof(WorldPlanWindow));
				Button edit = plan != null ? plan.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Button_EditIsland") : null;
				Check(ref ok, edit != null, "the rule card has Edit island");
				if (edit != null) edit.onClick.Invoke();
				for (float t = 0; t < 20f && WorldPlanWindow.IsOpen; t += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
				yield return null;
				Check(ref ok, !WorldPlanWindow.IsOpen && DynamicIslands.InEditor() && DynamicIslands.currentIslandName == island, "Edit island: the island in the editor");
				Button back = TopBarButton(bar, "Back to the plan"), use = TopBarButton(bar, "Use in a plan...");
				Check(ref ok, back != null && back.gameObject.activeInHierarchy && use != null && !use.gameObject.activeInHierarchy, "the top bar shows Back to the plan in place of Use in a plan...");
				// (the bar stretches across the screen, so its own width is the test screen's; what must fit is its buttons' width)
				RectTransform barRect = (RectTransform)bar.transform;
				Canvas barCanvas = barRect.GetComponentInParent<Canvas>();
				float room = 1024f / UIKit.ScaleFor(barCanvas != null ? barCanvas.rootCanvas.GetComponent<CanvasScaler>() : null, new Vector2(1024, 768));
				float need = LayoutUtility.GetPreferredWidth(barRect);
				Check(ref ok, need > 0 && need <= room, "the top bar's buttons fit 1024 x 768: " + Mathf.RoundToInt(need) + " of " + Mathf.RoundToInt(room) + " px");
				if (back != null) back.onClick.Invoke();
				yield return null; yield return null;
				Check(ref ok, WorldPlanWindow.IsOpen && WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == name, "Back to the plan: the plan open again");
				Check(ref ok, use != null && use.gameObject.activeInHierarchy && back != null && !back.gameObject.activeInHierarchy, "the top bar shows Use in a plan... again");
				WorldPlanWindow.Close();
				Check(ref ok, errors.Count == 0, "no errors (" + string.Join("; ", errors.Take(3).ToArray()) + ")");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				PlanAway(name);
				PutStartFileBack(startFile);
			}
			if (ok) Log("PASS: use in a plan"); else Fail("use in a plan");
		}

		[ConsoleCommand(name: "CIEditIslandFromMenu", docs: "Dev, main menu: TODO 5b - a plan on the main menu, its rule card's Edit island: the editor opens on that island with Back to the plan in its top bar; Back to the plan goes to the main menu with the start window and the plan open again. The test plan goes to the deleted folder after")]
		public static void EditIslandFromMenuCommand(string[] args) { StartTest(EditIslandFromMenuRoutine(args != null && args.Contains("shot"))); }

		static IEnumerator EditIslandFromMenuRoutine(bool shots)
		{
			if (!OnRaftMenu()) { Fail("edit island from the menu: run it on the main menu"); yield break; }
			bool ok = true;
			string startFile = ReadStartFile();
			string island = SomeSavedIsland();
			var wp = new WorldPlan { Name = "CI Edit Jump", Random = false, Description = "Edit island from the main menu (CIEditIslandFromMenu)" };
			wp.Rules.Add(IntroRule.Parse("first | island:" + island + " | start | ahead:300 | A first island | First"));
			wp.Save();
			try
			{
				CreateWindow.Open();
				WorldPlanWindow.Open(wp.Name);
				yield return null; yield return null;
				GameObject pw = WindowObject(typeof(WorldPlanWindow));
				Button edit = pw != null ? pw.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Button_EditIsland") : null;
				Check(ref ok, edit != null, "the rule card has Edit island");
				if (edit == null) { Fail("edit island from the menu"); yield break; }
				edit.onClick.Invoke();
				for (float t = 0; t < 180f && !(DynamicIslands.InEditor() && DynamicIslands.currentIslandName == island); t += 1f) yield return new WaitForSeconds(1f);
				yield return new WaitForSeconds(2f);
				Check(ref ok, DynamicIslands.InEditor() && DynamicIslands.currentIslandName == island, "the editor opens on '" + island + "' (" + DynamicIslands.currentIslandName + ")");
				Button back = TopBarButton(TopBar(), "Back to the plan");
				Check(ref ok, back != null && back.gameObject.activeInHierarchy, "the top bar has Back to the plan");
				if (shots) { StartShot("editor_back_to_plan"); yield return new WaitForSecondsRealtime(1f); }
				if (back != null) back.onClick.Invoke();
				for (float t = 0; t < 180f && !(OnRaftMenu() && WorldPlanWindow.IsOpen); t += 1f) yield return new WaitForSeconds(1f);
				yield return new WaitForSeconds(1f);
				Check(ref ok, OnRaftMenu() && WorldPlanWindow.IsOpen && WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == wp.Name && CreateWindow.IsOpen, "back on the main menu with the start window and the plan open");
				WorldPlanWindow.Close();
				CreateWindow.Close();
			}
			finally
			{
				PlanAway(wp.Name);
				PutStartFileBack(startFile);
			}
			if (ok) Log("PASS: edit island from the menu"); else Fail("edit island from the menu");
		}

		static List<string> OwnSteps(WorldPlan p) { return StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules).Where(x => !StoryChain.IsRaft(x)).Select(x => StoryChain.RuleIdOf(x)).ToList(); }

		static string StepsText(WorldPlan p) { return string.Join(",", StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules).ToArray()); }

		static int Shown(GameObject win, string name) { return win.GetComponentsInChildren<Transform>(false).Count(t => t.name == name); }

		[ConsoleCommand(name: "CIPlanTabs", docs: "Dev, editor: TODO 5b P3 - a world plan's three tabs. A plan with Raft's story on, two islands in its Main story line and one other island opens on Main story line: 2 cards between Raft's own story islands (grey lines), the tabs say (2) and (1), + Add a rule hidden. ▲ on the second story island puts it first of the two, ▲ again moves it past one of Raft's islands, ▼ on the last before Utopia is refused (Utopia stays last); the chain read back is the order shown. + Add a saved island puts one more at the end. Other islands: its 1 card and + Add a rule. Settings: random islands, Raft's story and the description, no cards. Save: the file's chain is the window's. Pictures with 'shot': split_plan_story_tab, split_plan_other_tab, split_plan_settings_tab. The test plan goes to the deleted folder after")]
		public static void PlanTabsCommand(string[] args) { StartTest(PlanTabsRoutine(args != null && args.Contains("shot"))); }

		static IEnumerator PlanTabsRoutine(bool shots)
		{
			if (!DynamicIslands.InEditor()) { Fail("plan tabs: in the editor"); yield break; }
			bool ok = true;
			var errors = new List<string>();
			Application.LogCallback watch = WatchErrors(errors);
			string startFile = ReadStartFile();
			List<string> islands = ChoiceWindow.Islands().Select(c => c.Value).Where(n => !n.StartsWith("ci", StringComparison.OrdinalIgnoreCase) && !n.StartsWith("gen-")).Take(3).ToList();
			if (islands.Count < 3) { Fail("plan tabs: needs three saved islands"); yield break; }
			string name = "CI Tabs " + DateTime.Now.ToString("HHmmss");
			var wp = new WorldPlan { Name = name, Random = false, RaftStory = true, Description = "Tabs test (CIPlanTabs)" };
			wp.Rules.Add(IntroRule.Parse("camp | type:camp | start | ahead:300 | | Camp"));
			IntroRule s1 = EditorEntry.RuleFor(wp, islands[0], true); wp.Rules.Add(s1);
			IntroRule s2 = EditorEntry.RuleFor(wp, islands[1], true); wp.Rules.Add(s2);
			wp.Save();
			try
			{
				WorldPlanWindow.Open(name);
				yield return null; yield return null;
				GameObject win = WindowObject(typeof(WorldPlanWindow));
				if (win == null) { Fail("plan tabs: the window didn't open"); yield break; }
				WorldPlan p = WorldPlanWindow.Plan;
				Check(ref ok, WorldPlanWindow.Tab == WorldPlanWindow.TabStory, "it opens on the Main story line tab");
				Button storyTab = win.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Tab_Story"), otherTab = win.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Tab_Other");
				Check(ref ok, storyTab != null && UIKit.LabelOf(storyTab).text == "Main story line (2)" && otherTab != null && UIKit.LabelOf(otherTab).text == "Other islands (1)", "the tabs say how many: " + (storyTab != null ? UIKit.LabelOf(storyTab).text : "?") + " / " + (otherTab != null ? UIKit.LabelOf(otherTab).text : "?"));
				int raftSteps = StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules).Count(StoryChain.IsRaft);
				Check(ref ok, Shown(win, "Rule") == 2 && Shown(win, "RaftStep") == raftSteps && raftSteps > 0, Shown(win, "Rule") + " story cards and " + Shown(win, "RaftStep") + " of Raft's islands as lines (" + raftSteps + ")");
				Check(ref ok, !win.GetComponentsInChildren<Button>(false).Any(b => b.name == "Button_+ Add a rule") && win.GetComponentsInChildren<Button>(false).Any(b => b.name == "Button_AddStoryIsland") && win.GetComponentsInChildren<Button>(false).Any(b => b.name == "Button_NewStoryIsland") && win.GetComponentsInChildren<Button>(false).Any(b => b.name == "Button_NewMainStory"),
					"the story tab's buttons (+ Add a rule only on Other islands)");
				Check(ref ok, OwnSteps(p).SequenceEqual(new[] { s1.Id, s2.Id }), "the story: " + string.Join(",", OwnSteps(p).ToArray()));
				if (shots) { StartShot("plan_story_tab"); yield return new WaitForSecondsRealtime(1f); }

				// ▲ ▼ along the story
				int s2At = StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules).IndexOf("rule:" + s2.Id);
				Check(ref ok, Click(StoryCard(p, s2), "▲"), "▲ on the second story island");
				yield return null;
				Check(ref ok, OwnSteps(p).SequenceEqual(new[] { s2.Id, s1.Id }), "it comes first of the two now: " + string.Join(",", OwnSteps(p).ToArray()));
				string before = StepsText(p);
				Check(ref ok, Click(StoryCard(p, s2), "▲"), "▲ again");
				yield return null;
				List<string> steps = StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules);
				Check(ref ok, steps.IndexOf("rule:" + s2.Id) == s2At - 2 && StoryChain.IsRaft(steps[s2At - 1]), "past one of Raft's islands: step " + (steps.IndexOf("rule:" + s2.Id) + 1) + " (" + StepsText(p) + ")");
				var raftOrder = steps.Where(StoryChain.IsRaft).ToList();
				Check(ref ok, raftOrder.SequenceEqual(StoryChain.BuildSteps(true, p.LeaveOut, new List<IntroRule>())), "Raft's own islands keep their order");
				before = StepsText(p);
				Check(ref ok, Click(StoryCard(p, s1), "▼"), "▼ on the last before Utopia");
				yield return null;
				Check(ref ok, StepsText(p) == before && win.GetComponentsInChildren<Text>(false).Any(t => t.text.Contains("Utopia")), "refused: Raft's ending stays last");

				// + Add a saved island
				Check(ref ok, Click(win, "+ Add a saved island"), "+ Add a saved island");
				yield return null; yield return null;
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.PickValue(islands[2]), "'" + islands[2] + "' picked");
				yield return null; yield return null;
				IntroRule s3 = p.Rules.FirstOrDefault(r => r.WhatArg == islands[2] && r.MainStory);
				steps = StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules);
				Check(ref ok, s3 != null && steps.IndexOf("rule:" + s3.Id) == steps.Count - 2 && Shown(win, "Rule") == 3, "it is the story's last island before Utopia, 3 cards (" + StepsText(p) + ")");

				// Other islands
				Check(ref ok, ClickTab(win, "Tab_Other"), "the Other islands tab");
				yield return null;
				Check(ref ok, Shown(win, "Rule") == 1 && Shown(win, "RaftStep") == 0 && win.GetComponentsInChildren<Button>(false).Any(b => UIKit.LabelOf(b) != null && UIKit.LabelOf(b).text == "+ Add a rule"), "Other islands: 1 card and + Add a rule");
				if (shots) { StartShot("plan_other_tab"); yield return new WaitForSecondsRealtime(1f); }

				// Settings
				Check(ref ok, ClickTab(win, "Tab_Settings"), "the Settings tab");
				yield return null;
				Transform box = win.GetComponentsInChildren<Transform>(false).FirstOrDefault(t => t.name == "SettingsTab");
				Check(ref ok, box != null && box.Find("Settings") != null && box.Find("Story") != null && box.Find("Description") != null && Shown(win, "Rule") == 0 && Shown(win, "Body") == 0, "Settings: random islands, Raft's story and the description, no cards");
				if (shots) { StartShot("plan_settings_tab"); yield return new WaitForSecondsRealtime(1f); }

				// Save: the file's chain is the window's
				Check(ref ok, Click(win, "Save"), "Save");
				yield return null;
				WorldPlan saved = WorldPlan.Load(name);
				Check(ref ok, saved != null && StepsText(saved) == StepsText(p), "the saved file's story is the window's: " + (saved != null ? StepsText(saved) : "none"));
				WorldPlanWindow.Close();
				Check(ref ok, errors.Count == 0, "no errors (" + string.Join("; ", errors.Take(3).ToArray()) + ")");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				WorldPlanWindow.Close();
				ChoiceWindow.Close();
				PlanAway(name);
				PutStartFileBack(startFile);
			}
			if (ok) Log("PASS: plan tabs"); else Fail("plan tabs");
		}

		[ConsoleCommand(name: "CIStoryNewIsland", docs: "Dev, editor: TODO 5b P3 - the Main story line tab's + New island: named 'CI New <time>', it is saved empty, put last in the story (before Utopia) and the plan saved; the editor shows it with Back to the plan; Back to the plan opens the plan on its Main story line with the new island's card. The test plan and island go to the deleted folder after")]
		public static void StoryNewIslandCommand(string[] args) { StartTest(StoryNewIslandRoutine()); }

		static IEnumerator StoryNewIslandRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("story new island: in the editor"); yield break; }
			bool ok = true;
			string startFile = ReadStartFile();
			string stamp = DateTime.Now.ToString("HHmmss"), name = "CI NewTab " + stamp, island = "CI New " + stamp;
			var wp = new WorldPlan { Name = name, Random = false, RaftStory = true, Description = "New island from the plan (CIStoryNewIsland)" };
			wp.Save();
			try
			{
				WorldPlanWindow.Open(name);
				yield return null; yield return null;
				GameObject win = WindowObject(typeof(WorldPlanWindow));
				Check(ref ok, win != null && WorldPlanWindow.Tab == WorldPlanWindow.TabStory && win.GetComponentsInChildren<Transform>(false).Any(t => t.name == "NoStory"), "an empty plan opens on its Main story line, saying how to start one");
				Check(ref ok, win != null && Click(win, "+ New island"), "+ New island");
				yield return null; yield return null;
				GameObject prompt = WindowObject(typeof(TextPromptWindow));
				Check(ref ok, prompt != null && TypeInto(prompt, "Name", island) && Click(prompt, "OK"), "named '" + island + "'");
				for (float t = 0; t < 10f && DynamicIslands.currentIslandName != island; t += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
				yield return null;
				Check(ref ok, !WorldPlanWindow.IsOpen && DynamicIslands.currentIslandName == island && File.Exists(IslandSpawner.PathFor(island)), "the editor on the new island, saved");
				WorldPlan p = WorldPlan.Load(name);
				IntroRule r = p != null ? p.Rules.FirstOrDefault(x => x.WhatArg == island) : null;
				List<string> steps = p != null ? StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules) : new List<string>();
				Check(ref ok, r != null && r.MainStory && steps.IndexOf("rule:" + r.Id) == steps.Count - 2, "the plan file has it last in the story, before Utopia (" + string.Join(",", steps.ToArray()) + ")");
				Button back = TopBarButton(TopBar(), "Back to the plan");
				Check(ref ok, back != null && back.gameObject.activeInHierarchy, "the top bar has Back to the plan");
				if (back != null) back.onClick.Invoke();
				yield return null; yield return null;
				win = WindowObject(typeof(WorldPlanWindow));
				Check(ref ok, WorldPlanWindow.IsOpen && WorldPlanWindow.Plan.Name == name && WorldPlanWindow.Tab == WorldPlanWindow.TabStory && win != null && win.GetComponentsInChildren<Transform>(false).Count(t => t.name == "Rule") == 1, "Back to the plan: the plan on its Main story line, with the island's card");
				WorldPlanWindow.Close();
			}
			finally
			{
				WorldPlanWindow.Close();
				PlanAway(name);
				if (DynamicIslands.currentIslandName == island) DynamicIslands.NewIsland();
				try { if (File.Exists(IslandSpawner.PathFor(island))) IslandFilesWindow.MoveToDeleted(island); } catch { }
				PutStartFileBack(startFile);
			}
			if (ok) Log("PASS: story new island"); else Fail("story new island");
		}
	}
}
