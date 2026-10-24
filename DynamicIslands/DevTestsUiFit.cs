using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Every button of the editor's windows inside its window (UIKit.RowOverflow), and the Islands window's help.</summary>
	public static partial class DevTests
	{
		// (Raft's screen sizes from small to ultra-wide and 4K; the mod's canvases always fit a 1400 x 800 layout, so a smaller UI
		// - 0.8 - only gives more room: the mod has no UI scale setting that makes it larger)
		static readonly Vector2[] FitScreens = { new Vector2(1280, 720), new Vector2(1366, 768), new Vector2(1600, 900), new Vector2(1920, 1080), new Vector2(2560, 1440), new Vector2(3840, 2160), new Vector2(1024, 768), new Vector2(1280, 1024), new Vector2(1920, 1200), new Vector2(2560, 1080), new Vector2(3440, 1440) };
		static readonly float[] FitScales = { 1f, 0.8f };

		[ConsoleCommand(name: "CIButtonsInside", docs: "Dev, editor: every editor window opened in turn (Islands, My islands, generator tabs, World plans and the island's rules with their Check, Progression and New main story, quest, behaviours, story items, pieces and stamps, placed list, item and sound pickers, choice, name prompt, Share, Import, library, info box) - at 11 screen sizes (1024x768 to 3840x2160, 21:9) and UI scales 1 and 0.8 the window fits the screen and every button and ? lies inside it (UIKit.RowOverflow), none squeezed to nothing. Picture shot_fit_worldplans")]
		public static void ButtonsInsideCommand() { StartTest(ButtonsInsideRoutine()); }

		class FitWindow
		{
			public string Name;
			public Action Open, Close;
			public Func<RectTransform> Panel;
			public FitWindow(string name, Action open, Action close, Func<RectTransform> panel) { Name = name; Open = open; Close = close; Panel = panel; }
		}

		/// <summary>The panel of an editor window (its "Panel" under the window's object on the editor's canvas).</summary>
		static RectTransform EditorPanel(string window)
		{
			Transform w = EditorUI.Canvas != null ? EditorUI.Canvas.transform.Find(window) : null;
			return w != null ? w.Find("Panel") as RectTransform : null;
		}

		/// <summary>The panel of a window on a canvas of its own (the info box, the island library).</summary>
		static RectTransform CanvasPanel(string canvas)
		{
			GameObject c = GameObject.Find(canvas);
			return c != null ? c.transform.Find("Panel") as RectTransform : null;
		}

		static IEnumerator ButtonsInsideRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("buttons inside: open the editor first"); yield break; }
			bool ok = true;
			HelpLinks.TestMode = true;
			WriteCIPlan(CIAdventure);
			var choices = new List<ChoiceWindow.Choice> { new ChoiceWindow.Choice("a", "A choice", "with a line that says what it does"), new ChoiceWindow.Choice("b", "Another choice") };
			var windows = new List<FitWindow>
			{
				new FitWindow("islandfiles", IslandFilesWindow.Open, IslandFilesWindow.Close, () => EditorPanel("IslandFilesWindow")),
				new FitWindow("myislands", MyIslandsWindow.Open, MyIslandsWindow.Close, () => EditorPanel("MyIslandsWindow")),
				new FitWindow("generator", () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(GeneratorWindow.TabNormal); }, GeneratorWindow.Close, () => EditorPanel("GeneratorWindow")),
				new FitWindow("generator randomize", () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(GeneratorWindow.TabRandomize); }, GeneratorWindow.Close, () => EditorPanel("GeneratorWindow")),
				new FitWindow("generator ready", () => { GeneratorWindow.Open(); GeneratorWindow.ShowTab(GeneratorWindow.TabReady); }, GeneratorWindow.Close, () => EditorPanel("GeneratorWindow")),
				new FitWindow("worldplans", () => WorldPlanWindow.Open(CIAdventure), WorldPlanWindow.Close, () => EditorPanel("WorldPlanWindow")),
				new FitWindow("islandrules", WorldPlanWindow.OpenIsland, WorldPlanWindow.Close, () => EditorPanel("WorldPlanWindow")),
				new FitWindow("plancheck", () => { WorldPlanWindow.Open(CIAdventure); PlanCheckWindow.Open(CIAdventure, new List<PlanChecker.Finding> { new PlanChecker.Finding(-1, PlanChecker.Level.Problem, "A finding for the test", "What to do about it") }, null, null); },
					() => { PlanCheckWindow.Close(); WorldPlanWindow.Close(); }, () => EditorPanel("PlanCheckWindow")),
				new FitWindow("progression", () => { WorldPlanWindow.Open(CIAdventure); PlanProgressWindow.Open(WorldPlanWindow.Plan, null); }, () => { PlanProgressWindow.Close(); WorldPlanWindow.Close(); }, () => EditorPanel("PlanProgressWindow")),
				new FitWindow("mainstory", () => { WorldPlanWindow.Open(CIAdventure); MainStoryHelper.Open(); }, () => { MainStoryHelper.Close(); WorldPlanWindow.Close(); }, () => EditorPanel("MainStoryHelper")),
				new FitWindow("quest", QuestEditorWindow.Open, QuestEditorWindow.Close, () => EditorPanel("QuestEditorWindow")),
				new FitWindow("behaviours", BehaviourWindow.OpenIsland, BehaviourWindow.Close, () => EditorPanel("BehaviourWindow")),
				new FitWindow("storyitems", StoryItemsWindow.Open, StoryItemsWindow.Close, () => EditorPanel("StoryItemsWindow")),
				new FitWindow("pieces", () => PiecesWindow.Open(false), PiecesWindow.Close, () => EditorPanel("PiecesWindow")),
				new FitWindow("stamps", () => PiecesWindow.Open(true), PiecesWindow.Close, () => EditorPanel("PiecesWindow")),
				new FitWindow("placedlist", PlacedListWindow.Open, PlacedListWindow.Close, () => EditorPanel("PlacedListWindow")),
				new FitWindow("itempicker", () => ItemPickerWindow.OpenFor(() => "", v => { }), ItemPickerWindow.Close, () => EditorPanel("ItemPickerWindow")),
				new FitWindow("soundpicker", () => SoundPickerWindow.OpenFor(v => { }), SoundPickerWindow.Close, () => EditorPanel("SoundPickerWindow")),
				new FitWindow("choice", () => ChoiceWindow.Open("A CHOICE", choices, v => { }), ChoiceWindow.Close, () => EditorPanel("ChoiceWindow")),
				new FitWindow("prompt", () => TextPromptWindow.Open("A NAME", "Type a name for the test", "citest", v => { }), TextPromptWindow.Close, () => EditorPanel("TextPromptWindow")),
				new FitWindow("camera", CameraSettingsWindow.Open, CameraSettingsWindow.Close, () => EditorPanel("CameraSettingsWindow")),
				new FitWindow("share", () => LibraryExportWindow.OpenPlan(CIAdventure), LibraryExportWindow.Close, () => LibraryExportWindow.Window != null ? LibraryExportWindow.Window.Find("Panel") as RectTransform : null),
				new FitWindow("import", LibraryImportWindow.Open, LibraryImportWindow.Close, () => LibraryImportWindow.Window != null ? LibraryImportWindow.Window.Find("Panel") as RectTransform : null),
				new FitWindow("library", () => LibraryWindow.Open(1), LibraryWindow.Close, () => CanvasPanel(LibraryWindow.CanvasName)),
				new FitWindow("info", () => InfoWindow.Open("A BOX", "A few lines of text for the test.", new InfoWindow.Choice("One button", () => { }), new InfoWindow.Choice("Another", () => { }, null, true)), InfoWindow.Close, () => CanvasPanel(InfoWindow.CanvasName)),
			};
			try
			{
				foreach (FitWindow w in windows)
				{
					try { w.Open(); }
					catch (Exception e) { Check(ref ok, false, w.Name + ": opening threw " + e.Message); continue; }
					yield return new WaitForSecondsRealtime(0.5f); // (rows laid out, lists filled)
					RectTransform panel = w.Panel();
					if (panel == null || !panel.gameObject.activeInHierarchy) { Check(ref ok, false, w.Name + ": the window opens (no panel)"); try { w.Close(); } catch { } continue; }
					var problems = new List<string>();
					foreach (Vector2 screen in FitScreens)
						foreach (float scale in FitScales)
							foreach (string p in UIKit.RowOverflow(panel, screen, scale))
								if (!problems.Contains(p)) problems.Add(p);
					int buttons = panel.GetComponentsInChildren<Button>(false).Length;
					Check(ref ok, problems.Count == 0, w.Name + ": " + buttons + " buttons inside the window at every screen size" + (problems.Count > 0 ? " - not: " + string.Join("; ", problems.Take(8).ToArray()) + (problems.Count > 8 ? " (and " + (problems.Count - 8) + " more)" : "") : ""));
					if (w.Name == "worldplans")
					{
						// The bottom: two rows, the plan's work above, its files below, Close inside the right edge
						Transform work = panel.Find("Buttons/PlanButtons"), files = panel.Find("Buttons/FileButtons");
						Check(ref ok, work != null && files != null && work.GetComponentsInChildren<Button>(false).Any(b => b.name == "Button_TestPlan") && files.GetComponentsInChildren<Button>(false).Any(b => b.name == "Button_Close"),
							"World plans: + Add a rule ... Test this plan in one row, Export ... Close in the next");
						Screenshot(new[] { "fit_worldplans" });
						yield return new WaitForSecondsRealtime(0.4f);
					}
					try { w.Close(); } catch (Exception e) { Check(ref ok, false, w.Name + ": closing threw " + e.Message); }
					yield return null;
				}
			}
			finally { HelpLinks.TestMode = false; InfoWindow.Close(); RemoveCIPlan(CIAdventure); }
			if (ok) Log("PASS: buttons inside their windows"); else Fail("buttons inside their windows");
		}

		[ConsoleCommand(name: "CIIslandFilesHelp", docs: "Dev, editor: the ISLANDS window's ? marks - Name, Height in the world (m), the island list, Library/Share/Export - each there, inside the window, with its text; hovering each shows its popup with that text, moving off closes it; the height's text says 0 is at sea level, above flies, below is sunk, and that it is the Island tab's height")]
		public static void IslandFilesHelpCommand() { StartTest(IslandFilesHelpRoutine()); }

		static IEnumerator IslandFilesHelpRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("islands window help: open the editor first"); yield break; }
			bool ok = true;
			try
			{
				IslandFilesWindow.Open();
				yield return new WaitForSecondsRealtime(0.5f);
				RectTransform panel = EditorPanel("IslandFilesWindow");
				Check(ref ok, IslandFilesWindow.IsOpen && panel != null, "the Islands window opens");
				if (panel == null) yield break;
				Canvas.ForceUpdateCanvases();
				List<UIKit.HelpMark> marks = panel.GetComponentsInChildren<UIKit.HelpMark>(false).ToList();
				var wanted = new[] { IslandFilesWindow.HelpName, IslandFilesWindow.HelpHeight, IslandFilesWindow.HelpList, IslandFilesWindow.HelpShare };
				foreach (string text in wanted)
					Check(ref ok, marks.Any(m => m.Text == text), "a ? for: " + text.Substring(0, Math.Min(40, text.Length)) + "...");
				Check(ref ok, marks.All(m => m.Text != null && m.Text.Length > 60), marks.Count + " ? marks, each with a real explanation");
				Check(ref ok, IslandFilesWindow.HelpHeight.Contains("0 ") && IslandFilesWindow.HelpHeight.Contains("60") && IslandFilesWindow.HelpHeight.Contains("-30") && IslandFilesWindow.HelpHeight.Contains("Island tab"),
					"the height's ? says sea level (0), flying (60), sunk (-30) and that it is the Island tab's height");
				Rect area = ScreenRect(panel);
				Check(ref ok, marks.All(m => { Rect r = ScreenRect((RectTransform)m.transform); return r.width >= 10f && area.Contains(r.center); }), "every ? has its size and sits in the window");
				foreach (UIKit.HelpMark m in marks)
				{
					m.OnPointerEnter(null);
					yield return new WaitForSecondsRealtime(0.15f);
					Check(ref ok, UIKit.ShownHelp == m.Text, "hovering the ? by " + m.transform.parent.name + " opens its text");
					m.OnPointerExit(null);
					yield return null;
					Check(ref ok, UIKit.ShownHelp == null, "moving off it closes the popup");
				}
				foreach (string p in UIKit.RowOverflow(panel, new Vector2(1920, 1080)))
					Check(ref ok, false, "Islands window: " + p);
			}
			finally { IslandFilesWindow.Close(); }
			if (ok) Log("PASS: islands window help"); else Fail("islands window help");
		}
	}
}
