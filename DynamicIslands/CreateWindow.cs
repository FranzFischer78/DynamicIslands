using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The start window of the main menu's EDITOR (TODO 5b): three choices side by side, each with a few plain words on what
	/// it is - an island (made in the 3D editor), a world plan (which islands come into a new world, and when; made right
	/// here, no island loaded) and the guide. The island and plan cards offer New, Open... and Continue on the last one; the
	/// card of what was made last is the highlighted one. It stays open under the plan window, so closing a plan comes back
	/// to it.
	/// </summary>
	public class CreateWindow : MonoBehaviour
	{
		static CreateWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		/// <summary>Tests: the window's root.</summary>
		public static RectTransform Root { get { return instance != null ? (RectTransform)instance.transform : null; } }

		Button newIsland, openIsland, continueIsland, newPlan, openPlan, continuePlan, guideButton;
		RawImage islandPicture;
		Text islandNote, planNote, planStory;
		RectTransform islandCard, planCard;
		Texture2D shownMap;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("CreateWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
			instance = blocker.AddComponent<CreateWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		/// <summary>The main menu's EDITOR.</summary>
		public static void Open()
		{
			if (instance == null) MenuWindows.Ensure();
			if (instance == null) return;
			instance.Show();
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
		}

		const string IslandWords = "One place in the sea: its land, trees, buildings, loot, notes and quests. You shape it in the 3D editor and try it out with Test.";
		const string PlanWords = "Which of your islands come into a new world, when and where. Its Main story line is the islands the Receiver leads players to, one after the other. Players choose a plan when they start a New Game.";
		const string GuideWords = "New here? The guide shows how to make your first island and your first world plan, step by step, with pictures.";
		const string FitWords = "How they fit together: you build islands, and a world plan brings them into a world. The Island Library (on the main menu) shares both with other players.";

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(18, 18, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 0));
			RectTransform head = UIKit.Row(panel, 30f, 8f, "Head");
			UIKit.Size(UIKit.Label(head, "EDITOR", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title").gameObject, 110, 30);
			UIKit.Label(head, "What do you want to make?", 15, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Question");
			UIKit.Button(head, "Close", Close, "Back to Raft's main menu (Esc)", 90, 28f, 13);

			RectTransform cards = UIKit.Row(panel, 360f, 12f, "Cards");
			cards.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

			// An island
			islandCard = Card(cards, "An island", "Card_Island", 390);
			Text words = UIKit.Label(islandCard, IslandWords, 13, UIKit.TextColor, TextAnchor.UpperLeft);
			UIKit.Size(words.gameObject, -1, 54);
			RectTransform pictureRow = UIKit.Row(islandCard, 122f, 10f, "PictureRow");
			islandPicture = UIKit.Picture(pictureRow, null, 122, 122, "IslandPicture");
			islandNote = UIKit.Label(pictureRow, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "IslandNote");
			newIsland = UIKit.Button(islandCard, "New island", () => { Close(); EditorEntry.StartIsland(null); }, "The 3D editor with an empty sea: shape the land, then place objects", -1, 32f, 14);
			openIsland = UIKit.Button(islandCard, "Open an island...", PickIsland, "Choose one of your saved islands to edit in the 3D editor", -1, 32f, 14);
			continueIsland = UIKit.Button(islandCard, "Continue", () => { string n = EditorEntry.LastIsland; if (n == null) return; Close(); EditorEntry.StartIsland(n); }, "Open the island you worked on last", -1, 32f, 14);
			continueIsland.name = "Button_ContinueIsland";

			// A world plan
			planCard = Card(cards, "A world plan", "Card_Plan", 390);
			words = UIKit.Label(planCard, PlanWords, 13, UIKit.TextColor, TextAnchor.UpperLeft);
			UIKit.Size(words.gameObject, -1, 54);
			planStory = UIKit.Label(planCard, "", 12, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, "PlanStory");
			UIKit.Size(planStory.gameObject, -1, 104);
			planNote = UIKit.Label(planCard, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "PlanNote");
			UIKit.Size(planNote.gameObject, -1, 16);
			newPlan = UIKit.Button(planCard, "New world plan", AskNewPlan, "Start a new plan: empty, from a template or as a copy - then add islands to its Main story line", -1, 32f, 14);
			openPlan = UIKit.Button(planCard, "Open a world plan...", PickPlan, "Choose one of your world plans to edit", -1, 32f, 14);
			continuePlan = UIKit.Button(planCard, "Continue", () => { string n = EditorEntry.LastPlan; if (n != null) WorldPlanWindow.Open(n); }, "Open the world plan you worked on last", -1, 32f, 14);
			continuePlan.name = "Button_ContinuePlan";

			// The guide
			RectTransform guide = Card(cards, "The guide", "Card_Guide", 220);
			Text g = UIKit.Label(guide, GuideWords, 13, UIKit.TextColor, TextAnchor.UpperLeft);
			UIKit.Size(g.gameObject, -1, 110);
			guideButton = UIKit.Button(guide, "Open the guide", HelpLinks.OpenGuide, "The guide (PDF) of this version of Custom Islands", -1, 32f, 14);
			guideButton.name = "Button_Guide";
			UIKit.Button(guide, "Islands, plans, story", () => HelpLinks.OpenGuideSection("three-things-you-can-make"), "The guide online: what an island, a world plan and the Main story line are", -1, 32f, 13).name = "Button_GuideThree";

			Text fit = UIKit.Label(panel, FitWords, 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "HowItFits");
			UIKit.Size(fit.gameObject, -1, 20);
		}

		static RectTransform Card(Transform parent, string title, string name, float width)
		{
			RectTransform box = UIKit.Group(parent, title, name);
			VerticalLayoutGroup v = box.GetComponent<VerticalLayoutGroup>();
			v.spacing = 8f;
			v.padding = new RectOffset(12, 12, 8, 12);
			v.childForceExpandHeight = false;
			UIKit.Size(box.gameObject, width, -1, width > 300 ? 1 : 0);
			return box;
		}

		void Show()
		{
			string island = EditorEntry.LastIsland, plan = EditorEntry.LastPlan;
			bool planLast = EditorEntry.Last == EditorEntry.Plan && plan != null;

			continueIsland.gameObject.SetActive(island != null);
			if (island != null) UIKit.LabelOf(continueIsland).text = "Continue '" + island + "'";
			if (shownMap != null) Destroy(shownMap);
			shownMap = island != null ? PlanPickerWindow.IslandMap(island) : null;
			islandPicture.texture = shownMap;
			islandPicture.color = shownMap != null ? Color.white : new Color(1, 1, 1, 0);
			int count = ChoiceWindow.SavedIslandCount();
			islandNote.text = island != null ? "Last edited: '" + island + "'\nYou have " + count + " saved island" + (count == 1 ? "" : "s") + "."
				: count > 0 ? "You have " + count + " saved island" + (count == 1 ? "" : "s") + "." : "No saved islands yet: start with New island.";

			continuePlan.gameObject.SetActive(plan != null);
			if (plan != null) UIKit.LabelOf(continuePlan).text = "Continue '" + plan + "'";
			int plans = WorldPlan.All().Count(n => !WorldPlan.IsBuiltIn(n));
			WorldPlan p = plan != null ? WorldPlan.Load(plan) : null;
			planStory.text = p != null ? StoryLine(p) : "";
			planNote.text = plans > 0 ? "You have " + plans + " world plan" + (plans == 1 ? "" : "s") + "." : "No world plans yet: start with New world plan.";

			// (what was made last is the highlighted choice)
			UIKit.SetLook(continueIsland, !planLast ? UIKit.Look.Primary : UIKit.Look.Plain);
			UIKit.SetLook(continuePlan, planLast ? UIKit.Look.Primary : UIKit.Look.Plain);
			UIKit.SetLook(newIsland, island == null && !planLast ? UIKit.Look.Primary : UIKit.Look.Plain);
			UIKit.SetLook(newPlan, plan == null && planLast ? UIKit.Look.Primary : UIKit.Look.Plain);
		}

		/// <summary>The plan's Main story line in a few words: "1. Radio Tower  2. My island ...".</summary>
		public static string StoryLine(WorldPlan p)
		{
			List<string> steps = StoryChain.BuildSteps(p.RaftStory, p.LeaveOut, p.Rules);
			var names = steps.Select(s => StoryChain.IsRaft(s) ? StoryOrder.Name(StoryChain.TypeOfStep(s)) : (p.Rules.FirstOrDefault(r => r.Id.Equals(StoryChain.RuleIdOf(s), StringComparison.OrdinalIgnoreCase)) ?? new IntroRule { Id = StoryChain.RuleIdOf(s) }).TabName).ToList();
			int others = p.Rules.Count(r => !r.InStory);
			string line = names.Count == 0 ? "No Main story line (no islands on the Receiver)." :
				string.Join("   ", names.Take(8).Select((n, i) => (i + 1) + ". " + n).ToArray()) + (names.Count > 8 ? "   ... (" + names.Count + " in all)" : "");
			return "'" + p.Name + "' - Main story line:\n" + line + (others > 0 ? "\nand " + others + " other island rule" + (others == 1 ? "" : "s") + "." : "");
		}

		void PickIsland()
		{
			ChoiceWindow.OpenIslands("Open an island", ChoiceWindow.IslandsToOpen, n => { Close(); EditorEntry.StartIsland(n); }, "No saved islands yet: start with New island", openIsland);
		}

		void PickPlan()
		{
			ChoiceWindow.Open("Open a world plan", WorldPlan.All().Where(n => !WorldPlan.IsBuiltIn(n)).Select(n => { WorldPlan p = WorldPlan.Load(n); return new ChoiceWindow.Choice(n, n, p != null ? p.Description : ""); }),
				n => WorldPlanWindow.Open(n), "No world plans yet: start with New world plan");
		}

		void AskNewPlan()
		{
			WorldPlanWindow.AskNewPlan();
		}

		bool planWasOpen;

		void Update()
		{
			// (a plan was closed: its Continue and Main story line are shown as they are now)
			if (planWasOpen && !WorldPlanWindow.IsOpen) Show();
			planWasOpen = WorldPlanWindow.IsOpen;
			if (!IsOpen || WorldPlanWindow.IsOpen || ChoiceWindow.IsOpen || TextPromptWindow.IsOpen || InfoWindow.IsOpen || MainStoryHelper.IsOpen || EditorInput.SubWindowJustClosed) return;
			if (EscKey.Down(this)) Close();
		}

		void OnDestroy()
		{
			if (shownMap != null) Destroy(shownMap);
		}
	}
}
