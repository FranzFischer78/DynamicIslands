using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>The help of the World plans window (? marks, Help) and the alpha box's help buttons.</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIPlanHelp", docs: "Dev, editor: the World plans window's help - a ? on every part (the plan row, templates, random islands, Raft's story, description, map, Check, Export/Import, and 7 on each rule card - DOT since SL9, whose ? has the Receiver text and opens on hover), each inside the window with its text; hovering shows the popup; Help opens the steps with the guide's buttons (online section, PDF); the island's rules have their own help and no Export ?. Pictures shot_planhelp_*")]
		public static void PlanHelpCommand() { StartTest(PlanHelpRoutine()); }

		static IEnumerator PlanHelpRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("plan help: open the editor first"); yield break; }
			bool ok = true;
			HelpLinks.TestMode = true;
			try
			{
				WorldPlanWindow.Open(WriteCIPlan(CIAdventure));
				yield return new WaitForSecondsRealtime(0.5f);
				Transform window = EditorUI.Canvas.transform.Find("WorldPlanWindow");
				RectTransform panel = window != null ? window.Find("Panel") as RectTransform : null;
				Check(ref ok, WorldPlanWindow.IsOpen && panel != null, "the window opens on " + CIAdventure);
				if (panel == null) yield break;
				// (TODO 5b: a plan with a Main story line opens on that tab; the rule cards counted here are on Other islands)
				WorldPlanWindow.ShowTab(WorldPlanWindow.TabOther);
				yield return null;
				Canvas.ForceUpdateCanvases();
				List<UIKit.HelpMark> marks = window.GetComponentsInChildren<UIKit.HelpMark>(false).ToList();
				int cards = window.GetComponentsInChildren<Transform>(false).Count(t => t.name == "Rule");
				int onCards = marks.Count(m => m.GetComponentsInParent<Transform>(true).Any(t => t.name == "Rule"));
				// (a km/day card has an 8th: "again until done", SR1)
				int again = window.GetComponentsInChildren<Transform>(false).Count(t => t.name == "Check_Again");
				Check(ref ok, cards > 0 && onCards == cards * 7 + again, onCards + " ? on " + cards + " rule cards (7 each with DOT, +1 on the " + again + " with again until done)");
				// (SL9's Receiver DOT: its own ? on every card, with the Receiver text - counted above only as one of 7, GAP 10-20)
				List<UIKit.HelpMark> dots = marks.Where(m => m.Text != null && m.Text.StartsWith("DOT - ") && m.Text.Contains("Receiver") && m.GetComponentsInParent<Transform>(true).Any(t => t.name == "Rule")).ToList();
				Check(ref ok, dots.Count == cards, dots.Count + " DOT ? (the Receiver's dot) on " + cards + " rule cards - one each");
				Check(ref ok, marks.Count - onCards == 5, (marks.Count - onCards) + " ? for the window's parts on the Other islands tab (plan row, templates, map, Check, Export/Import)");
				// (TODO 5b: the plan's settings and its Main story line are tabs of their own)
				WorldPlanWindow.ShowTab(WorldPlanWindow.TabSettings);
				yield return null;
				int settingsMarks = window.GetComponentsInChildren<UIKit.HelpMark>(false).Count(m => m.GetComponentsInParent<Transform>(true).Any(t => t.name == "SettingsTab"));
				Check(ref ok, settingsMarks == 3, settingsMarks + " ? on the Settings tab (random islands, Raft's story, description)");
				WorldPlanWindow.ShowTab(WorldPlanWindow.TabStory);
				yield return null;
				int storyMarks = window.GetComponentsInChildren<UIKit.HelpMark>(false).Count(m => m.GetComponentsInParent<Transform>(true).Any(t => t.name == "StoryTools"));
				Check(ref ok, storyMarks == 1, storyMarks + " ? on the Main story line tab's buttons");
				WorldPlanWindow.ShowTab(WorldPlanWindow.TabOther);
				yield return null;
				Canvas.ForceUpdateCanvases();
				marks = window.GetComponentsInChildren<UIKit.HelpMark>(false).ToList();
				Check(ref ok, marks.All(m => m.Text != null && m.Text.Length > 60), "every ? has a real explanation");
				Rect area = ScreenRect(panel);
				// (a ? on a card further down the list is scrolled out of view: only the visible ones must be inside the window)
				RectTransform list = window.GetComponentsInChildren<ScrollRect>(false).Select(s => s.viewport).FirstOrDefault();
				Rect listArea = list != null ? ScreenRect(list) : area;
				var outside = marks.Where(m =>
				{
					Rect r = ScreenRect((RectTransform)m.transform);
					bool onCard = m.GetComponentsInParent<Transform>(true).Any(t => t.name == "Rule");
					if (onCard && !listArea.Contains(r.center)) return false; // (scrolled out of view)
					return r.width < 10f || !area.Contains(r.center);
				}).ToList();
				Check(ref ok, outside.Count == 0, "every ? has its size and sits in the window" + (outside.Count > 0 ? " - not: " + outside.Count : ""));
				// Hovering shows the popup
				UIKit.HelpMark when = marks.FirstOrDefault(m => m.Text.StartsWith("WHEN"));
				if (when != null)
				{
					when.OnPointerEnter(null);
					// (a hover lasts; the popup is placed again each frame - a minimised Raft draws few frames, so wait for it to be placed)
					Rect pop = new Rect();
					for (float t = 0; t < 3f; t += 0.2f)
					{
						yield return new WaitForSecondsRealtime(0.2f);
						Canvas.ForceUpdateCanvases();
						pop = UIKit.HelpPopup != null ? ScreenRect(UIKit.HelpPopup) : new Rect();
						if (pop.xMin >= -1f && pop.yMin >= -1f && pop.xMax <= Screen.width + 1f && pop.yMax <= Screen.height + 1f) break;
					}
					Check(ref ok, UIKit.ShownHelp == when.Text && pop.width > 50f && pop.height > 50f && pop.xMin >= -1f && pop.yMin >= -1f && pop.xMax <= Screen.width + 1f && pop.yMax <= Screen.height + 1f,
						"hovering the When ? shows its help, on the screen (" + pop + ")");
					Screenshot(new[] { "planhelp_when" });
					yield return new WaitForSecondsRealtime(0.6f);
					when.OnPointerExit(null);
					yield return null;
					Check(ref ok, UIKit.ShownHelp == null, "moving off it closes the popup");
				}
				else Check(ref ok, false, "a When ? on the first card");
				// (from the marks found again after the tab switches: the cards may have been made anew)
				UIKit.HelpMark dot = marks.FirstOrDefault(m => m != null && m.Text != null && m.Text.StartsWith("DOT - "));
				if (dot != null)
				{
					dot.OnPointerEnter(null);
					for (float t = 0; t < 3f && UIKit.ShownHelp != dot.Text; t += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
					Check(ref ok, UIKit.ShownHelp == dot.Text && UIKit.HelpPopup != null, "hovering the DOT ? shows its Receiver help");
					dot.OnPointerExit(null);
					yield return null;
				}
				else Check(ref ok, false, "a DOT ? on the first card");
				// Help
				Button help = panel.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "HelpButton");
				Check(ref ok, help != null, "a Help button in the window's head");
				if (help != null)
				{
					help.onClick.Invoke();
					yield return new WaitForSecondsRealtime(0.4f);
					Check(ref ok, InfoWindow.IsOpen && InfoWindow.Title == "HOW TO MAKE A WORLD PLAN" && InfoWindow.Body.Contains("+ Add a rule") && InfoWindow.Body.Contains("Choose plan...") && InfoWindow.Body.Contains("Test this plan"),
						"Help shows the steps: " + InfoWindow.Title);
					Screenshot(new[] { "planhelp_steps" });
					yield return new WaitForSecondsRealtime(0.6f);
					Button online = InfoWindow.ButtonNamed("Guide: world plans step by step");
					if (online != null) online.onClick.Invoke();
					Check(ref ok, HelpLinks.LastOpened == HelpLinks.GuideOnline + "#72-your-first-world-plan-step-by-step", "its guide button opens section 7.2 online: " + HelpLinks.LastOpened);
					Button pdf = InfoWindow.ButtonNamed("Open the guide (PDF)");
					HelpLinks.LastOpened = null;
					if (pdf != null) pdf.onClick.Invoke();
					CheckGuideOpened(ref ok);
					InfoWindow.Close();
					yield return null;
					Check(ref ok, !InfoWindow.IsOpen && WorldPlanWindow.IsOpen, "Close goes back to the plan");
				}
				WorldPlanWindow.Close();

				// The island's own rules
				var saved = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
				try
				{
					DynamicIslands.currentIslandProps.Clear();
					WorldDirector.SetRulesInProps(DynamicIslands.currentIslandProps, new[] { IntroRule.Parse("reward | type:random | quest:self | near:self:600:north | Well done | Reward") });
					WorldPlanWindow.OpenIsland();
				}
				catch (Exception e) { Check(ref ok, false, "island rules: " + e.Message); }
				yield return new WaitForSecondsRealtime(0.5f);
				marks = window.GetComponentsInChildren<UIKit.HelpMark>(false).ToList();
				Check(ref ok, marks.Count == 6 + 2 && !marks.Any(m => m.Text.StartsWith("Export")), "island rules: " + marks.Count + " ? (the card's 6 with DOT - no story row on an island - plus map and Check), no Export ?");
				Button islandHelp = panel.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "HelpButton");
				if (islandHelp != null) islandHelp.onClick.Invoke();
				yield return new WaitForSecondsRealtime(0.3f);
				Check(ref ok, InfoWindow.IsOpen && InfoWindow.Title == "ISLAND RULES" && InfoWindow.ButtonLabels.Contains("Guide: islands that bring islands"), "the island rules' Help: " + InfoWindow.Title);
				InfoWindow.Close();
				WorldPlanWindow.Close();
				DynamicIslands.currentIslandProps.Clear();
				foreach (var kv in saved) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
			}
			finally { HelpLinks.TestMode = false; InfoWindow.Close(); WorldPlanWindow.Close(); RemoveCIPlan(CIAdventure); }
			if (ok) Log("PASS: plan help"); else Fail("plan help");
		}

		[ConsoleCommand(name: "CIMenuLibrary", docs: "Dev, main menu: the ISLAND LIBRARY button shows its words (all of them drawn, on the screen) and opens the library; the library's Submit yours... shows how to submit (export, post on the Discord, approved first) with its buttons (Discord, exports folder, the guide's section); pictures shot_menu_library, shot_library_submit")]
		public static void MenuLibraryCommand() { StartTest(MenuLibraryRoutine()); }

		static IEnumerator MenuLibraryRoutine()
		{
			bool ok = true;
			GameObject button = GameObject.Find("MainMenuCanvas/MenuButtons/LIBRARY");
			Text text = button != null ? button.GetComponentInChildren<Text>() : null;
			if (text == null) { Fail("menu library: no ISLAND LIBRARY button on the main menu"); yield break; }
			Canvas.ForceUpdateCanvases();
			int drawn = text.cachedTextGenerator.characterCountVisible;
			Check(ref ok, text.text == "ISLAND LIBRARY" && drawn >= "ISLAND LIBRARY".Length - 1, "the button shows ISLAND LIBRARY (" + drawn + " letters drawn, size " + text.fontSize + ")");
			Text editor = GameObject.Find("MainMenuCanvas/MenuButtons/EDITOR") != null ? GameObject.Find("MainMenuCanvas/MenuButtons/EDITOR").GetComponentInChildren<Text>() : null;
			int size = text.cachedTextGenerator.fontSizeUsedForBestFit, editorSize = editor != null ? editor.cachedTextGenerator.fontSizeUsedForBestFit : 0;
			Check(ref ok, editorSize > 0 && size <= editorSize && size >= editorSize / 2, "its words fit the button, no larger than EDITOR's (" + size + " / " + editorSize + ")");
			var corners = new Vector3[4];
			text.rectTransform.GetWorldCorners(corners);
			// (the right end of the letters as drawn: preferredWidth is at the font's own size, not the smaller best-fit one,
			// and on a half-width window - 1280 px - it put the words past the middle though they ended at 310 px)
			float right = corners[0].x + text.preferredWidth * text.canvas.scaleFactor;
			var verts = text.cachedTextGenerator.verts;
			if (verts.Count > 0)
			{
				float maxX = float.MinValue;
				for (int i = 0; i < verts.Count; i++) maxX = Mathf.Max(maxX, verts[i].position.x);
				right = text.rectTransform.TransformPoint(new Vector3(maxX / text.pixelsPerUnit, 0f, 0f)).x;
			}
			Check(ref ok, corners[0].x >= 0f && right <= Screen.width * 0.5f, "its words are on the screen, on the left (" + corners[0].x.ToString("F0") + " to " + right.ToString("F0") + " px)");
			Screenshot(new[] { "menu_library" });
			yield return new WaitForSecondsRealtime(0.6f);
			HelpLinks.TestMode = true;
			try
			{
				button.GetComponent<Button>().onClick.Invoke();
				yield return new WaitForSecondsRealtime(1f);
				Check(ref ok, LibraryWindow.IsOpen, "the button opens the library");
				Button submit = LibraryWindow.Root != null ? LibraryWindow.Root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Button_Submit") : null;
				Check(ref ok, submit != null, "the library has Submit yours...");
				if (submit != null)
				{
					submit.onClick.Invoke();
					yield return new WaitForSecondsRealtime(0.4f);
					Check(ref ok, InfoWindow.IsOpen && InfoWindow.Title == "SUBMIT YOUR ISLAND OR PLAN" && InfoWindow.Body.Contains("Export") && InfoWindow.Body.Contains("Discord") && InfoWindow.Body.Contains("approved"),
						"it explains: export, post on the Discord, approved first");
					Screenshot(new[] { "library_submit" });
					yield return new WaitForSecondsRealtime(0.6f);
					InfoWindow.ButtonNamed("Open the Discord").onClick.Invoke();
					Check(ref ok, HelpLinks.LastOpened == HelpLinks.Discord, "Open the Discord: " + HelpLinks.LastOpened);
					InfoWindow.ButtonNamed("Open exports folder").onClick.Invoke();
					Check(ref ok, HelpLinks.LastOpened == LibraryPack.ExportFolder && Directory.Exists(LibraryPack.ExportFolder), "Open exports folder: " + HelpLinks.LastOpened);
					InfoWindow.ButtonNamed("Guide: sharing").onClick.Invoke();
					Check(ref ok, HelpLinks.LastOpened == HelpLinks.GuideOnline + "#47-saving-and-sharing", "Guide: sharing opens section 4.7");
					InfoWindow.ButtonNamed("Close").onClick.Invoke();
					yield return null;
					Check(ref ok, !InfoWindow.IsOpen && LibraryWindow.IsOpen, "Close goes back to the library");
				}
				// The Guide button (the user, 2026-10-04): the illustrated guide
				Button guide = LibraryWindow.Root != null ? LibraryWindow.Root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Button_Guide") : null;
				Check(ref ok, guide != null, "the library has a Guide button");
				if (guide != null)
				{
					guide.onClick.Invoke();
					string opened = HelpLinks.LastOpened ?? "";
					Check(ref ok, opened.EndsWith(HelpLinks.PdfName) || opened == HelpLinks.GuidePdfOnline, "Guide opens the guide: " + opened);
				}
			}
			finally { HelpLinks.TestMode = false; InfoWindow.Close(); LibraryWindow.Close(); }
			if (ok) Log("PASS: menu library"); else Fail("menu library");
		}

		[ConsoleCommand(name: "CIEditorUpDown", docs: "Dev, editor: the camera goes straight up and down (Space / C) without turning or sliding sideways, whichever way it looks; the keys are written in the camera help and the Terrain tab's tips")]
		public static void EditorUpDownCommand() { StartTest(EditorUpDownRoutine()); }

		static IEnumerator EditorUpDownRoutine()
		{
			EditorCamera cam = EditorCamera.Instance;
			if (!DynamicIslands.InEditor() || cam == null) { Fail("editor up/down: open the editor first"); yield break; }
			bool ok = true;
			foreach (float dir in new[] { 1f, -1f })
			{
				Vector3 start = cam.transform.position;
				Quaternion look = cam.transform.rotation;
				float until = Time.unscaledTime + 1f;
				while (Time.unscaledTime < until)
				{
					cam.Move(new Vector3(0f, dir, 0f), false, false, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
					yield return null;
				}
				yield return new WaitForSecondsRealtime(0.3f);
				Vector3 moved = cam.transform.position - start;
				Check(ref ok, moved.y * dir > 2f && new Vector2(moved.x, moved.z).magnitude < 0.05f * Mathf.Abs(moved.y) + 0.05f && Quaternion.Angle(look, cam.transform.rotation) < 0.5f,
					(dir > 0 ? "Space" : "C") + ": straight " + (dir > 0 ? "up" : "down") + " " + moved.y.ToString("F1") + " m (sideways " + new Vector2(moved.x, moved.z).magnitude.ToString("F2") + " m)");
			}
			Text tips = EditorUI.Canvas.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Tips" && t.text.StartsWith("Left mouse: use the brush"));
			Check(ref ok, EditorCamera.Help.Contains("Space / C") && tips != null && tips.text.Contains("Space / C: straight up / down"), "the keys are written in the help and the Terrain tab's tips");
			Screenshot(new[] { "editor_updown" });
			yield return new WaitForSecondsRealtime(0.6f);
			if (ok) Log("PASS: editor up down"); else Fail("editor up down");
		}

		const string PickShrine = "cipick-shrine", PickPlain = "cipick-plain", PickPlan = "CI Picks";

		[ConsoleCommand(name: "CIPlanPicks", docs: "Dev, editor: the World plans window's lists (the ▾ after a field): the island a rule waits for (the plan's rules and saved islands, with their quests), after rule (rules only), an island's zones, signals and quest steps, near which island (and 'where it happened'), the story's done-when zone; picking fills the field and the plan; a map-type island's list is empty and says why; the island's own rules offer self. Makes and removes cipick-* islands and the plan 'CI Picks'. Pictures shot_planpicks_*")]
		public static void PlanPicksCommand() { StartTest(PlanPicksRoutine()); }

		static void MakePickIslands()
		{
			var f = new IslandFile { Name = PickShrine, TerrainSize = new Vector3(120, 40, 120), HeightmapResolution = 33, Heights = new float[33, 33] };
			for (int y = 0; y < 33; y++) for (int x = 0; x < 33; x++) f.Heights[y, x] = 0.4f;
			foreach (string zone in new[] { "altar", "cave" })
				f.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Props = new Dictionary<string, string> { { ObjectProps.ZoneId, zone } } });
			f.Objects.Add(new IslandObject { Name = "Log", Props = new Dictionary<string, string> { { BehaviourProps.Name, "lever" }, { BehaviourProps.EventPrefix + "use", "signal||gate open" } } });
			TestQuest().To(f.Props);
			f.Save(IslandSpawner.PathFor(PickShrine));
			var g = new IslandFile { Name = PickPlain, TerrainSize = new Vector3(120, 40, 120), HeightmapResolution = 33, Heights = new float[33, 33] };
			g.Save(IslandSpawner.PathFor(PickPlain));
			IslandCache.Forget();
		}

		static IEnumerator PlanPicksRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("plan picks: open the editor first"); yield break; }
			bool ok = true;
			MakePickIslands();
			WorldPlan p = WorldPlan.Parse(PickPlan, "description = Lists test\nrandom = off\n" +
				"rule = shrine | island:" + PickShrine + " | start | ahead:300 | | Shrine\n" +      // 0
				"rule = next | type:camp | quest:shrine | near:shrine:600:any | |\n" +               // 1
				"rule = z | type:sandbar | zone:shrine:altar | near:shrine:500:any | |\n" +          // 2
				"rule = s | type:sandbar | signal:shrine:gate open | ahead:300 | |\n" +              // 3
				"rule = st | type:sandbar | step:shrine:2 | ahead:300 | |\n" +                       // 4
				"rule = after | type:wreck | rule:shrine | ahead:300 | |\n" +                        // 5
				"rule = typed | type:sandbar | zone:next:x | ahead:300 | |\n" +                      // 6
				"rule = story | island:" + PickShrine + " | start | receiver:600 | | Story | first | zone:altar\n"); // 7
			p.Save();
			Transform window = EditorUI.Canvas.transform.Find("WorldPlanWindow");
			try
			{
				WorldPlanWindow.Open(PickPlan);
			}
			catch (Exception e) { Check(ref ok, false, "open: " + e.Message); }
			yield return new WaitForSecondsRealtime(0.5f);
			Func<int, string, List<string>> values = (rule, field) => WorldPlanWindow.FieldChoices(rule, field).Select(c => c.Value).ToList();
			List<ChoiceWindow.Choice> refs = WorldPlanWindow.FieldChoices(1, "whenref");
			var refValues = refs.Select(c => c.Value).ToList();
			Check(ref ok, refValues.Contains("shrine") && refValues.Contains("z") && !refValues.Contains("next") && refValues.Contains(PickShrine) && refValues.Contains(PickPlain),
				"'quest done at' lists the plan's other rules and your saved islands (" + refValues.Count + ")");
			ChoiceWindow.Choice shrineRule = refs.FirstOrDefault(c => c.Value == "shrine"), plain = refs.FirstOrDefault(c => c.Value == PickPlain);
			Check(ref ok, shrineRule != null && shrineRule.Detail.Contains("quest, 4 steps") && plain != null && plain.Detail.Contains("no quest"), "each says whether it has a quest: " + (shrineRule != null ? shrineRule.Detail : "?") + " / " + (plain != null ? plain.Detail : "?"));
			List<string> afterRule = values(5, "whenref");
			Check(ref ok, afterRule.Contains("shrine") && !afterRule.Contains(PickShrine) && !afterRule.Contains("after"), "'after rule' lists rules only: " + string.Join(", ", afterRule.ToArray()));
			Check(ref ok, values(2, "whenarg").SequenceEqual(new[] { "altar", "cave" }), "'zone fires at shrine' lists its zones: " + string.Join(", ", values(2, "whenarg").ToArray()));
			Check(ref ok, values(3, "whenarg").SequenceEqual(new[] { "gate open" }), "'signal sent at shrine' lists its signal: " + string.Join(", ", values(3, "whenarg").ToArray()));
			Check(ref ok, values(4, "whenarg").SequenceEqual(new[] { "1", "2", "3", "4" }), "'quest step done at shrine' lists its 4 steps");
			Check(ref ok, values(6, "whenarg").Count == 0, "a map-type island's zones can't be listed");
			List<string> near = values(2, "whereref");
			Check(ref ok, near.Count > 0 && near[0] == "" && near.Contains("shrine") && near.Contains(PickPlain), "'near an island' lists 'where it happened', the rules and your islands");
			Check(ref ok, values(7, "donearg").SequenceEqual(new[] { "altar", "cave" }), "the story's 'done when its zone fires' lists the zones of the island it brings");

			// As a player clicks: the ▼ opens the list, a pick fills the field
			Func<int, string, Button> pickButton = (rule, name) =>
			{
				// (the card of rule n: a Main story line card is on that tab - TODO 5b)
				Transform card = WorldPlanWindow.CardFor(rule);
				return card != null ? card.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name) : null;
			};
			bool storyPick = pickButton(7, "Pick_DoneArg") != null;
			Button zoneButton = pickButton(2, "Pick_WhenArg");
			Check(ref ok, zoneButton != null && pickButton(1, "Pick_WhenRef") != null && pickButton(2, "Pick_WhereRef") != null && storyPick && pickButton(0, "Pick_WhenArg") == null,
				"a ▾ after the fields that name something (none after 'the world starts')");
			if (zoneButton != null)
			{
				// (the card in view, as a player scrolls to it before clicking)
				ScrollRect rulesScroll = window.GetComponentsInChildren<ScrollRect>(false).FirstOrDefault(s => zoneButton.transform.IsChildOf(s.content));
				if (rulesScroll != null)
				{
					RectTransform zoneCard = WorldPlanWindow.CardFor(2);
					Canvas.ForceUpdateCanvases();
					float gap = ScreenRect(rulesScroll.viewport).yMax - ScreenRect(zoneCard).yMax;
					rulesScroll.content.anchoredPosition += new Vector2(0f, gap / rulesScroll.content.lossyScale.y); // (up by how far its top is under the list's top)
					yield return null;
				}
				zoneButton.onClick.Invoke();
				yield return new WaitForSecondsRealtime(0.4f);
				Check(ref ok, ChoiceWindow.IsOpen && ChoiceWindow.Values.SequenceEqual(new[] { "altar", "cave" }) && ChoiceWindow.Title.Contains("TRIGGER ZONES"), "clicking it lists the zones: " + ChoiceWindow.Title);
				Screenshot(new[] { "planpicks_zones" });
				yield return new WaitForSecondsRealtime(0.6f);
				ChoiceWindow.PickValue("cave");
				yield return null;
				Transform card = WorldPlanWindow.CardFor(2);
				bool shown = card != null && card.GetComponentsInChildren<InputField>(false).Any(f => f.text == "cave");
				Check(ref ok, WorldPlanWindow.Current.Rules[2].WhenArg == "cave" && shown, "picking 'cave' fills the field and the rule");
			}
			Button refButton = pickButton(1, "Pick_WhenRef");
			if (refButton != null) { refButton.onClick.Invoke(); yield return null; ChoiceWindow.PickValue(PickPlain); yield return null; }
			Check(ref ok, WorldPlanWindow.Current.Rules[1].WhenRef == PickPlain, "picking an island for 'quest done at': " + WorldPlanWindow.Current.Rules[1].WhenRef);
			Button whereButton = pickButton(2, "Pick_WhereRef");
			if (whereButton != null) { whereButton.onClick.Invoke(); yield return null; ChoiceWindow.PickValue(""); yield return null; }
			Check(ref ok, WorldPlanWindow.Current.Rules[2].WhereRef == "", "picking 'where it happened' empties 'of'");
			Button doneButton = pickButton(7, "Pick_DoneArg");
			if (doneButton != null) { doneButton.onClick.Invoke(); yield return null; ChoiceWindow.PickValue("cave"); yield return null; }
			Check(ref ok, WorldPlanWindow.Current.Rules[7].StoryDone == "zone:cave", "picking the story's zone: " + WorldPlanWindow.Current.Rules[7].StoryDone);
			Button typedButton = pickButton(6, "Pick_WhenArg");
			if (typedButton != null) { typedButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.4f); }
			string why = ChoiceWindow.EmptyText;
			Check(ref ok, ChoiceWindow.IsOpen && why != null && why.Contains("map type") && why.Contains("type the name"), "a map-type island's empty list says why: " + why);
			Screenshot(new[] { "planpicks_empty" });
			yield return new WaitForSecondsRealtime(0.6f);
			ChoiceWindow.Close();
			WorldPlanWindow.Close();

			// The island's own rules: self first
			var saved = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
			DynamicIslands.currentIslandProps.Clear();
			WorldDirector.SetRulesInProps(DynamicIslands.currentIslandProps, new[] { IntroRule.Parse("reward | type:random | quest:self | near:self:600:north | Well done | Reward") });
			WorldPlanWindow.OpenIsland();
			yield return new WaitForSecondsRealtime(0.4f);
			List<string> own = values(0, "whenref"), ownNear = values(0, "whereref");
			Check(ref ok, own.Count > 0 && own[0] == IntroRule.Self && ownNear.Count > 0 && ownNear[0] == IntroRule.Self, "an island's own rules offer 'self' first");
			WorldPlanWindow.Close();
			DynamicIslands.currentIslandProps.Clear();
			foreach (var kv in saved) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;

			File.Delete(WorldPlan.PathFor(PickPlan));
			foreach (string n in new[] { PickShrine, PickPlain }) { string path = IslandSpawner.PathFor(n); if (File.Exists(path)) File.Delete(path); }
			IslandCache.Forget();
			if (ok) Log("PASS: plan picks"); else Fail("plan picks");
		}

		[ConsoleCommand(name: "CIWindowShot", docs: "Dev, editor: opens one editor window at the current screen size and takes its picture (shot_window_<name>), for the guide - works with Raft minimised, the screen size is not changed. Names: report (Report a problem, also on the main menu), worldplans (the test plan CI Adventure), behaviours (the island's events), islandfiles, quest, storyitems, generator")]
		public static void WindowShotCommand(string[] args) { StartTest(WindowShotRoutine(args != null && args.Length > 0 ? args[0] : "worldplans")); }

		static IEnumerator WindowShotRoutine(string name)
		{
			Action open = null, close = null;
			if (name != "report" && !DynamicIslands.InEditor()) { Fail("window shot: open the editor first"); yield break; }
			switch (name)
			{
				case "report": open = InfoWindow.OpenReport; close = InfoWindow.Close; break; // (the main menu's Report a problem box)
				case "worldplans": open = () => WorldPlanWindow.Open(WriteCIPlan(CIAdventure)); close = () => { WorldPlanWindow.Close(); RemoveCIPlan(CIAdventure); }; break;
				case "behaviours": open = BehaviourWindow.OpenIsland; close = BehaviourWindow.Close; break;
				case "islandfiles": open = IslandFilesWindow.Open; close = IslandFilesWindow.Close; break;
				case "quest": open = QuestEditorWindow.Open; close = QuestEditorWindow.Close; break;
				case "storyitems": open = StoryItemsWindow.Open; close = StoryItemsWindow.Close; break;
				case "pieces": open = () => PiecesWindow.Open(false); close = PiecesWindow.Close; break;
				case "generator": open = () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(0); }; close = GeneratorWindow.Close; break;
			}
			if (open == null) { Fail("window shot: no window '" + name + "'"); yield break; }
			open();
			yield return new WaitForSecondsRealtime(0.8f);
			Screenshot(new[] { "window_" + name });
			yield return new WaitForSecondsRealtime(0.6f);
			close();
			Log("PASS: window shot " + name);
		}

		/// <summary>The guide's PDF came out of the .rmod and was opened as a file.</summary>
		static void CheckGuideOpened(ref bool ok)
		{
			string path = Path.Combine(DynamicIslands.assetpath, HelpLinks.PdfName);
			bool file = HelpLinks.LastOpened != null && HelpLinks.LastOpened.StartsWith("file:") && File.Exists(path) && new FileInfo(path).Length > 1000000;
			Check(ref ok, file, "the guide opens as the PDF from the mod: " + HelpLinks.LastOpened + (File.Exists(path) ? " (" + new FileInfo(path).Length / 1024 + " KB)" : " (no file)"));
		}

		[ConsoleCommand(name: "CINoticeShot", docs: "Dev, main menu: the guide's picture of the main menu with the alpha box open (shot_main_menu.png); a folded box is opened for the picture and folded again after, the player's choice (notice.txt) untouched")]
		public static void NoticeShotCommand() { StartTest(NoticeShotRoutine()); }

		static IEnumerator NoticeShotRoutine()
		{
			bool folded = ExperimentalNotice.Folded;
			var fold = HarmonyLib.Traverse.Create(typeof(ExperimentalNotice)).Method("SetFolded", new[] { typeof(bool) });
			if (folded) fold.GetValue(false);
			yield return new WaitForSecondsRealtime(0.5f);
			Screenshot(new[] { "main_menu" });
			yield return new WaitForSecondsRealtime(1.5f);
			if (folded) fold.GetValue(true);
			Log("PASS: notice shot");
		}

		/// <summary>The alpha box is never shown over another window: Raft's New Game and Load World boxes and the mod's info box each hide it, closing them brings it back.</summary>
		static IEnumerator NoticeCoverRoutine(Action<bool, string> check)
		{
			yield return new WaitForSecondsRealtime(1.2f);
			check(!ExperimentalNotice.Hidden && ExperimentalNotice.OtherWindow() == null, "on the plain main menu the box shows (nothing else open: " + (ExperimentalNotice.OtherWindow() ?? "none") + ")");
			foreach (Type t in new[] { typeof(NewGameBox), typeof(LoadGameBox) })
			{
				MenuBox box = Resources.FindObjectsOfTypeAll(t).OfType<MenuBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
				if (box == null) { check(false, t.Name + " not found on the main menu"); continue; }
				try { box.Close(); } catch { } box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
				yield return new WaitForSecondsRealtime(1.5f);
				check(box.IsOpen && ExperimentalNotice.Hidden, t.Name + " open: the alpha box is hidden (" + (ExperimentalNotice.OtherWindow() ?? "nothing seen") + ")");
				Screenshot(new[] { "notice_under_" + t.Name });
				yield return new WaitForSecondsRealtime(0.6f);
				box.Close();
				yield return new WaitForSecondsRealtime(1.5f);
				check(!box.IsOpen && !ExperimentalNotice.Hidden, t.Name + " closed: the box is back");
			}
			InfoWindow.OpenReport();
			yield return null;
			yield return null;
			check(ExperimentalNotice.Hidden, "Report a problem's box open: the alpha box is hidden");
			InfoWindow.Close();
			yield return null;
			yield return null;
			check(!ExperimentalNotice.Hidden, "... and back when it closes");
		}

		/// <summary>The alpha box's help: Discord, Guide, Report a problem (its box and buttons), and moving the box.</summary>
		static IEnumerator NoticeHelpRoutine(Action<bool, string> check)
		{
			HelpLinks.TestMode = true;
			try
			{
				check(ExperimentalNotice.DiscordButton != null && ExperimentalNotice.GuideButton != null && ExperimentalNotice.ReportButton != null
					&& ExperimentalNotice.DiscordButton.gameObject.activeInHierarchy && ExperimentalNotice.ReportButton.gameObject.activeInHierarchy, "Discord, Guide (PDF) and Report a problem on the box");
				ExperimentalNotice.DiscordButton.onClick.Invoke();
				check(HelpLinks.LastOpened == HelpLinks.Discord, "Discord opens " + HelpLinks.LastOpened);
				HelpLinks.LastOpened = null;
				ExperimentalNotice.GuideButton.onClick.Invoke();
				bool g = true; CheckGuideOpened(ref g); check(g, "Guide (PDF) opens the PDF");
				ExperimentalNotice.ReportButton.onClick.Invoke();
				yield return new WaitForSecondsRealtime(0.4f);
				check(InfoWindow.IsOpen && InfoWindow.Title == "REPORT A PROBLEM" && InfoWindow.Body.Contains("Player.log") && InfoWindow.Body.Contains("What you expected") && InfoWindow.Body.Contains(ExperimentalNotice.Version),
					"Report a problem opens its box: what to include, the logs, the versions");
				var want = new[] { "Copy report form", "Open log folder", "Report on GitHub", "Post on Discord", "Close" };
				check(want.All(w => InfoWindow.ButtonLabels.Contains(w)), "its buttons: " + string.Join(", ", InfoWindow.ButtonLabels.ToArray()));
				check(InfoWindow.ButtonsOutside().Count == 0, "every button inside the box" + (InfoWindow.ButtonsOutside().Count > 0 ? " - sticking out: " + string.Join(", ", InfoWindow.ButtonsOutside().ToArray()) : ""));
				Screenshot(new[] { "notice_report" });
				yield return new WaitForSecondsRealtime(0.6f);
				InfoWindow.ButtonNamed("Report on GitHub").onClick.Invoke();
				check(HelpLinks.LastOpened != null && HelpLinks.LastOpened.StartsWith(HelpLinks.Issues + "/new?body=") && Uri.UnescapeDataString(HelpLinks.LastOpened).Contains("Custom Islands " + ExperimentalNotice.Version),
					"Report on GitHub opens a new issue with the form and the versions");
				InfoWindow.ButtonNamed("Post on Discord").onClick.Invoke();
				check(HelpLinks.LastOpened == HelpLinks.Discord, "Post on Discord opens the server");
				InfoWindow.ButtonNamed("Open log folder").onClick.Invoke();
				check(HelpLinks.LastOpened == HelpLinks.LogFolder && File.Exists(Path.Combine(HelpLinks.LogFolder, "Player.log")), "Open log folder: " + HelpLinks.LogFolder + " (Player.log there)");
				string clip = GUIUtility.systemCopyBuffer;
				InfoWindow.ButtonNamed("Copy report form").onClick.Invoke();
				check(GUIUtility.systemCopyBuffer.Contains("What happened:") && GUIUtility.systemCopyBuffer.Contains("Custom Islands " + ExperimentalNotice.Version) && InfoWindow.Status.StartsWith("Copied"),
					"Copy report form puts the form on the clipboard (" + HelpLinks.Versions + ")");
				GUIUtility.systemCopyBuffer = clip;
				InfoWindow.ButtonNamed("Close").onClick.Invoke();
				yield return null;
				check(!InfoWindow.IsOpen, "Close closes it");

				// Moving the box: a drag moves it, never off the screen, and the place is remembered
				RectTransform panel = ExperimentalNotice.Panel;
				Vector2 start = panel.anchoredPosition;
				ExperimentalNotice.MoveBy(new Vector2(-250f, -150f));
				yield return null;
				Rect r = ScreenRect(panel);
				check((panel.anchoredPosition - start).magnitude > 100f && r.xMin >= -1f && r.yMin >= -1f && r.xMax <= Screen.width + 1f && r.yMax <= Screen.height + 1f, "dragging moves it (" + start + " > " + panel.anchoredPosition + ")");
				string notice = File.ReadAllText(Path.Combine(DynamicIslands.assetpath, "notice.txt"));
				check(notice.Contains("place="), "the place is remembered (notice.txt)");
				Screenshot(new[] { "notice_moved" });
				yield return new WaitForSecondsRealtime(0.6f);
				ExperimentalNotice.MoveBy(new Vector2(5000f, 5000f));
				yield return null;
				r = ScreenRect(panel);
				check(r.xMin >= -1f && r.yMin >= -1f && r.xMax <= Screen.width + 1f && r.yMax <= Screen.height + 1f, "dragged too far, it stays on the screen (" + r + ")");
				ExperimentalNotice.MoveBy(new Vector2(-5000f, -5000f));
				yield return null;
				r = ScreenRect(panel);
				check(r.xMin >= -1f && r.yMin >= -1f && r.xMax <= Screen.width + 1f && r.yMax <= Screen.height + 1f, "and the other way (" + r + ")");
				ExperimentalNotice.ResetPlace();
				yield return null;
				check(panel.anchoredPosition == start || (panel.anchoredPosition - start).magnitude < 1f, "back where it starts");
			}
			finally { HelpLinks.TestMode = false; InfoWindow.Close(); }
		}

		[ConsoleCommand(name: "CIHoverTips", docs: "Dev, main menu or editor (X9): where no status bar shows a control's hover text - the main menu's windows (here the plan picker), the stats page - it comes in a small tip by the mouse after a moment, on the screen, and goes when the mouse leaves; in the editor its status bar has it and no tip comes")]
		public static void HoverTipsCommand() { StartTest(HoverTipsRoutine()); }

		static IEnumerator HoverTipsRoutine()
		{
			bool ok = true;
			bool editor = EditorUI.Canvas != null && EditorUI.Canvas.gameObject.activeInHierarchy;
			bool opened = false;
			if (!editor && !PlanPickerWindow.IsOpen) { PlanPickerWindow.Open(); opened = true; yield return new WaitForSecondsRealtime(0.5f); }
			UIKit.HintTarget target = UnityEngine.Object.FindObjectsOfType<UIKit.HintTarget>().FirstOrDefault(h => h.isActiveAndEnabled && !string.IsNullOrEmpty(h.Text));
			if (target == null) { if (opened) PlanPickerWindow.Close(); Fail("hover tips: no control with a hover text on the screen"); yield break; }
			string said = null;
			Action<string> heard = h => said = h;
			UIKit.HintChanged += heard;
			try
			{
				target.OnPointerEnter(null);
				yield return new WaitForSecondsRealtime(0.2f);
				Check(ref ok, UIKit.ShownTip == null, "no tip at once (" + (UIKit.ShownTip ?? "none") + ")");
				yield return new WaitForSecondsRealtime(0.9f);
				if (editor) Check(ref ok, UIKit.ShownTip == null && said == target.Text, "the editor: its status bar has the text, no tip (" + target.name + ": " + said + ")");
				else
				{
					Check(ref ok, UIKit.ShownTip == target.Text, "a tip shows the hover text after a moment (" + target.name + ": " + (UIKit.ShownTip ?? "none") + ")");
					var c = new Vector3[4];
					if (UIKit.HintTip != null) UIKit.HintTip.GetWorldCorners(c);
					Check(ref ok, UIKit.HintTip != null && c[0].x >= 0 && c[0].y >= 0 && c[2].x <= Screen.width && c[2].y <= Screen.height && c[2].x - c[0].x <= 301f * UIKit.HintTip.lossyScale.x,
						"the tip is on the screen, at most 300 wide (" + c[0] + " - " + c[2] + ")");
					Screenshot(new[] { "hovertip" });
				}
				target.OnPointerExit(null);
				yield return null;
				Check(ref ok, UIKit.ShownTip == null && said == null, "it goes when the mouse leaves");
			}
			finally { UIKit.HintChanged -= heard; if (opened) PlanPickerWindow.Close(); }
			if (ok) Log("PASS: hover tips"); else Fail("hover tips");
		}
	}
}
