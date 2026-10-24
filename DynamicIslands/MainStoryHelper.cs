using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// World Plans' "New main story..." (NextTask_QuestBook.md, Q6): a main story made step by step - Raft's story on or off,
	/// then the saved islands in the order players meet them, each with where it sits in Raft's story, its tab colour and
	/// what gives the next coordinates. Done makes the rule cards (each island on the Receiver, after the one before; the
	/// first one's frequency given at the start), runs Check, and offers Preview notebook. The cards can be edited after.
	/// </summary>
	public class MainStoryHelper : MonoBehaviour
	{
		public class Entry
		{
			public string Island = "";
			/// <summary>Raft's story on: after which of Raft's islands (StoryOrder key) - "" = after the island before.</summary>
			public string After = "";
			public int Colour;
			public string Next = "";
		}

		static MainStoryHelper instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		/// <summary>What the helper holds (tests).</summary>
		public static readonly List<Entry> Entries = new List<Entry>();
		public static bool RaftStory { get; set; }

		RectTransform list;
		Text storyLabel, summary;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("MainStoryHelper", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
			instance = blocker.AddComponent<MainStoryHelper>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			Entries.Clear();
			WorldPlan p = WorldPlanWindow.Plan;
			RaftStory = p == null || p.RaftStory;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.Show();
		}

		public static void Close() { if (instance != null) { DropList.Close(); if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount; instance.gameObject.SetActive(false); } }

		// (Esc: it had none - the World plans window under it closed instead, and Done then added to a closed plan)
		void Update() { if (!DropList.Busy && !ChoiceWindow.IsOpen && !EditorInput.SubWindowJustClosed && EscKey.Down(this)) Close(); }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(18, 18, 14, 16), 8f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 600));
			RectTransform head = UIKit.Row(panel, 30f, 8f, "Head");
			UIKit.Label(head, "NEW MAIN STORY", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			Text intro = UIKit.Label(panel, "The main story is the chain of islands players follow with the Receiver; each gets a tab in Raft's own notebook " +
				"(its quest steps, notes and items there). Other islands are side quests (the journal).\n" +
				"1. Raft's own story on or off.  2. Add your saved islands in the order players meet them.  3. Done: the rule cards are made - edit them after if you like.", 12, UIKit.TextMuted, TextAnchor.UpperLeft);
			intro.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(intro.gameObject, -1, 52);

			RectTransform story = UIKit.Row(panel, 30f, 8f, "Story");
			UIKit.Button(story, "Raft's story: on / off", () => { RaftStory = !RaftStory; Show(); }, "On: your islands come between Raft's own (Radio Tower ... Utopia). Off: only your islands - a new adventure", 190, 28f, 12).name = "Helper_Story";
			storyLabel = UIKit.Label(story, "", 12, UIKit.TextColor, TextAnchor.MiddleLeft);
			UIKit.Size(storyLabel.gameObject, -1, -1, 1);

			RectTransform box = UIKit.Rect("ListBox", panel);
			UIKit.Size(box.gameObject, -1, 360);
			ScrollRect s;
			list = UIKit.ScrollList(box, out s, 6f);
			UIKit.Stretch((RectTransform)s.transform);

			summary = UIKit.Label(panel, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft);
			UIKit.Size(summary.gameObject, -1, 18);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			// (opens at once and loads the islands in it, the button greyed meanwhile - it read every island's whole file first)
			Button add = null;
			add = UIKit.Button(buttons, "+ Add a saved island", () => ChoiceWindow.OpenIslands("Next island of the main story", ChoiceWindow.Islands, v => { Entries.Add(new Entry { Island = v }); Show(); }, null, add), "The next island players meet in the story", 190, 34f, 13);
			add.name = "Helper_Add";
			UIKit.Size(UIKit.Label(buttons, "", 12).gameObject, -1, -1, 1);
			Button done = UIKit.Button(buttons, "Done: make the cards", () => Finish(), "Make the rule cards for these islands in World Plans", 200, 34f, 13);
			done.name = "Helper_Done";
			UIKit.Primary(done);
			UIKit.Button(buttons, "Cancel", Close, "Close without making anything", 110, 34f, 13);
		}

		static readonly DropList.Option[] NextOptions =
		{
			new DropList.Option("", "Its quest is done (or reached)", "The next coordinates come when its quest is finished (or players reach it, if it has none)"),
			new DropList.Option("quest", "Its quest is done", "The next coordinates come when its quest is finished"),
			new DropList.Option("visit", "Players reach it", "The next coordinates come when a player gets there"),
		};

		void Show()
		{
			storyLabel.text = RaftStory ? "ON: your islands go between Raft's own story islands (choose after which one)." : "OFF: only your islands - the first one's coordinates come at the start, each next one's when the one before is done.";
			foreach (Transform c in list) Destroy(c.gameObject);
			if (Entries.Count == 0) UIKit.Label(list, "<i>No islands yet: + Add a saved island.</i>", 13, UIKit.TextMuted);
			for (int k = 0; k < Entries.Count; k++)
			{
				int i = k;
				Entry e = Entries[i];
				RectTransform row = UIKit.Row(list, 28f, 6f, "Island_" + i);
				UIKit.Size(UIKit.Label(row, (i + 1) + ".", 13, UIKit.Accent, TextAnchor.MiddleRight, FontStyle.Bold).gameObject, 26);
				UIKit.Size(UIKit.Label(row, e.Island, 13, UIKit.TextColor, TextAnchor.MiddleLeft).gameObject, 190);
				if (RaftStory)
				{
					var after = new List<DropList.Option> { new DropList.Option("", i == 0 ? "First, before Raft's" : "After the island before", i == 0 ? "Before Radio Tower" : "Right after island " + i) };
					after.AddRange(StoryOrder.Chain.Where(t => t != ChunkPointType.Landmark_Utopia).Select(t => new DropList.Option(StoryOrder.Key(t), "After " + StoryOrder.Name(t), "When " + StoryOrder.Name(t) + "'s note is found")));
					DropList.Make(row, "Helper_After_" + i, after, e.After, v => { e.After = v; Show(); }, 190, "Where it sits in Raft's story");
				}
				DropList.Make(row, "Helper_Colour_" + i, WorldPlanWindowColours(), e.Colour.ToString(), v => { int n; e.Colour = int.TryParse(v, out n) ? n : 0; Show(); }, 150, "Its tab colour in Raft's notebook");
				DropList.Make(row, "Helper_Next_" + i, NextOptions, e.Next, v => { e.Next = v; Show(); }, 200, "What gives the next island's coordinates");
				UIKit.Button(row, "▲", () => { if (i > 0) { Entries.Reverse(i - 1, 2); Show(); } }, "Earlier in the story", 26, 26f, 11);
				UIKit.Button(row, "✕", () => { Entries.RemoveAt(i); Show(); }, "Take it out", 26, 26f, 11);
			}
			summary.text = Entries.Count == 0 ? "" : Entries.Count + " island(s) in the main story" + (RaftStory ? ", with Raft's own story." : ", without Raft's story.");
		}

		static IList<DropList.Option> WorldPlanWindowColours() { return WorldPlanWindow.TabColourChoices; }

		/// <summary>The rule cards from the helper's islands, put into the plan in World Plans (tests call it as Done).</summary>
		public static string Finish()
		{
			WorldPlan p = WorldPlanWindow.Plan;
			if (p == null) return "World Plans isn't open";
			if (Entries.Count == 0) return "No islands: + Add a saved island";
			var rules = new List<IntroRule>();
			string before = null;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry e = Entries[i];
				string id = IntroRule.CleanId(new string(e.Island.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()));
				if (id.Length == 0) id = "story" + (i + 1);
				while (p.Rules.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || rules.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) id += "b";
				var r = new IntroRule { Id = id, What = "island", WhatArg = e.Island, When = "start", Label = e.Island.Length > 18 ? e.Island.Substring(0, 18) : e.Island, StoryDone = e.Next, TabColour = e.Colour };
				bool first = i == 0 && (!RaftStory || e.After.Length == 0);
				if (first) r.StoryPlace = "first";
				else if (RaftStory && e.After.Length > 0) r.StoryPlace = "after:" + e.After;
				else r.StoryPlace = "after:" + before;
				// (every main story island is found by its coordinates on the Receiver - the first one too: its frequency comes
				// at the start, on its notebook tab and in a banner)
				r.Where = "receiver";
				r.Distance = 800f;
				rules.Add(r);
				before = id;
			}
			WorldPlanWindow.AddFromHelper(RaftStory, rules);
			Close();
			return null;
		}
	}
}
