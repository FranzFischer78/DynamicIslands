using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// World Plans' Preview notebook (NextTask_QuestBook.md, Q7): the plan in the window, saved or not, shown in Raft's own
	/// notebook in the editor's test world (IslandTest.StartPreview takes it there). The story is played out as a list of
	/// moments in the order players meet them - an island's coordinates found, each quest step done, each note read, the
	/// next coordinates - and a bar steps through them (Back / Next), or shows every page at once (All). QuestBook draws
	/// the book from this instead of the world's chain while the preview is on.
	/// </summary>
	public static class QuestBookPreview
	{
		public class Moment
		{
			public string Kind = "", Step = "", Rule = "", Text = "", Key = "";
			public int Arg;
		}

		/// <summary>The plan being previewed (null: no preview).</summary>
		public static WorldPlan Plan { get; private set; }
		public static bool Active { get { return Plan != null; } }
		public static readonly List<string> Steps = new List<string>();
		public static readonly List<IntroRule> Rules = new List<IntroRule>();
		public static readonly List<Moment> Moments = new List<Moment>();
		/// <summary>How many moments have happened (Moments.Count: all of them).</summary>
		public static int At { get; private set; }

		static Canvas bar;
		static Text barText;

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [quest book] " + msg); }

		/// <summary>In the test world: the plan's main story in the book, every page shown, the bar and the book open.</summary>
		public static void Begin(WorldPlan plan)
		{
			Plan = plan;
			Rules.Clear();
			Rules.AddRange(plan.Rules.Where(r => r.MainStory));
			Steps.Clear();
			Steps.AddRange(StoryChain.BuildSteps(plan.RaftStory, plan.LeaveOut, plan.Rules.Where(r => r.Special)));
			// (islands beside Raft's story: after the chain, in the plan's order)
			Steps.AddRange(Rules.Where(r => r.Beside).Select(r => StoryChain.RuleKey(r.Id)));
			MakeMoments();
			At = Moments.Count;
			ShowBar();
			QuestBook.Refresh();
			OpenBook();
			Log("Preview of '" + plan.Name + "': " + Moments.Count + " moments, " + Rules.Count + " main story island(s)");
		}

		/// <summary>The preview is over (back to the editor, or the test world left): the book is the world's again.</summary>
		public static void End()
		{
			if (Plan == null) return;
			Plan = null;
			Steps.Clear(); Rules.Clear(); Moments.Clear();
			if (bar != null) UnityEngine.Object.Destroy(bar.gameObject);
			bar = null;
			try { QuestBook.Refresh(); } catch { }
		}

		static IntroRule RuleOf(string step) { return Rules.FirstOrDefault(r => r.Id.Equals(StoryChain.RuleIdOf(step), StringComparison.OrdinalIgnoreCase)); }

		static string IslandOf(IntroRule r) { return r.What == "island" ? r.WhatArg : null; }

		/// <summary>The story as players meet it: per step of the chain, its coordinates found, its quest's steps and its notes
		/// (spread through the quest), the next coordinates; the ending page last.</summary>
		static void MakeMoments()
		{
			Moments.Clear();
			foreach (string step in Steps)
			{
				if (StoryChain.IsRaft(step))
				{
					string name = StoryOrder.Name(StoryChain.TypeOfStep(step));
					Moments.Add(new Moment { Kind = "open", Step = step, Text = "Raft's " + name + ": its coordinates are found" });
					Moments.Add(new Moment { Kind = "done", Step = step, Text = name + " is done: the next coordinates" });
					continue;
				}
				IntroRule r = RuleOf(step);
				if (r == null) continue;
				string title = r.TabName, island = IslandOf(r);
				Moments.Add(new Moment { Kind = "open", Step = step, Rule = r.Id, Text = title + ": its coordinates are found - its tab and intro" });
				List<IslandQuest.Step> qs = island != null ? IslandCache.QuestOfFile(island).Steps : new List<IslandQuest.Step>();
				List<int> notes = island != null ? IslandCache.NotesOf(island) : new List<int>();
				Dictionary<int, KeyValuePair<string, string>> texts = island != null ? IslandCache.NoteTextsOf(island) : new Dictionary<int, KeyValuePair<string, string>>();
				int n = 0;
				for (int k = 0; k <= qs.Count; k++)
				{
					// (the notes spread through the quest: before step k, those due by then)
					int due = qs.Count == 0 ? notes.Count : (int)Math.Ceiling(notes.Count * (k + 1) / (double)(qs.Count + 1));
					for (; n < Math.Min(due, notes.Count); n++)
					{
						KeyValuePair<string, string> t;
						string nt = texts.TryGetValue(notes[n], out t) && t.Key.Length > 0 ? t.Key : "note #" + notes[n];
						Moments.Add(new Moment { Kind = "note", Step = step, Rule = r.Id, Arg = notes[n], Text = title + ": '" + nt + "' is read" });
					}
					if (k < qs.Count) Moments.Add(new Moment { Kind = "step", Step = step, Rule = r.Id, Arg = k + 1, Text = title + ": quest step " + (k + 1) + " of " + qs.Count + " done - " + qs[k].Describe() });
				}
				// (the pages its events write - "journal page" actions: they go onto its tab too)
				if (island != null)
					foreach (string ev in IslandCache.EventPagesOf(island))
						Moments.Add(new Moment { Kind = "page", Step = step, Rule = r.Id, Key = ev, Text = title + ": an event writes '" + ev.Substring(ev.IndexOf(':') + 1) + "'" });
				Moments.Add(new Moment { Kind = "done", Step = step, Rule = r.Id, Text = title + " is done (" + r.DescribeDone() + "): the next coordinates" });
			}
			if (Plan.StoryEnding.Trim().Length > 0) Moments.Add(new Moment { Kind = "end", Text = "The story ends: the last page" });
		}

		static IEnumerable<Moment> Happened { get { return Moments.Take(At); } }

		public static bool Opened(string step) { return Happened.Any(m => m.Kind == "open" && m.Step == step); }
		public static bool IsDone(string step) { return Happened.Any(m => m.Kind == "done" && m.Step == step); }
		public static int StepsDone(string ruleId) { return Happened.Count(m => m.Kind == "step" && m.Rule == ruleId); }
		public static List<int> NotesRead(string ruleId) { return Happened.Where(m => m.Kind == "note" && m.Rule == ruleId).Select(m => m.Arg).ToList(); }
		/// <summary>The event pages written so far ("object number:title").</summary>
		public static List<string> PagesWritten(string ruleId) { return Happened.Where(m => m.Kind == "page" && m.Rule == ruleId).Select(m => m.Key).ToList(); }
		public static bool Over { get { return Happened.Any(m => m.Kind == "end"); } }

		/// <summary>An example frequency for the preview (the world makes the real one): 4 digits from the rule's name.</summary>
		public static string Frequency(string ruleId)
		{
			int h = 17;
			foreach (char c in ruleId ?? "") h = h * 31 + c;
			return "#" + (Math.Abs(h) % 10000).ToString("0000", CultureInfo.InvariantCulture);
		}

		/// <summary>Moves through the story: -1 back, +1 next; Show(0) = the start, Show(Moments.Count) = all.</summary>
		public static void Show(int at)
		{
			if (!Active) return;
			At = Mathf.Clamp(at, 0, Moments.Count);
			QuestBook.Refresh();
			UpdateBar();
			OpenBook();
		}

		public static string Current { get { return At == 0 ? "The start: nothing found yet" : At >= Moments.Count ? "Everything (" + Moments.Count + " moments): every page as at the end" : Moments[At - 1].Text; } }

		static void OpenBook()
		{
			try
			{
				Network_Player p = RAPI.GetLocalPlayer();
				NoteBookUI ui = p != null ? p.NoteBookUI : null;
				if (ui == null) return;
				if (!ui.isDisplayed) ui.SetBookActive(true);
				// (the page of the moment just shown: its island's tab)
				string step = At > 0 && At <= Moments.Count ? Moments[At - 1].Step : null;
				int page = step != null ? QuestBook.FirstPageOfStep(step) : -1;
				if (page >= 0) ui.FlipToPageLocally((uint)page);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [quest book] " + e.Message); }
		}

		static void ShowBar()
		{
			if (bar != null) UnityEngine.Object.Destroy(bar.gameObject);
			bar = UIKit.CreateCanvas("CustomIslands_NotebookPreview", 520);
			RectTransform panel = UIKit.Panel(bar.transform, "Panel", new RectOffset(14, 14, 8, 8), 6f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(980, 86));
			RectTransform head = UIKit.Row(panel, 22f, 8f, "Head");
			UIKit.Label(head, "PREVIEW NOTEBOOK: " + Plan.Name.ToUpperInvariant(), 13, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			barText = UIKit.Label(panel, "", 13, UIKit.TextColor, TextAnchor.MiddleLeft);
			barText.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(barText.gameObject, -1, 20);
			RectTransform row = UIKit.Row(panel, 28f, 6f, "Buttons");
			UIKit.Button(row, "|◀ Start", () => Show(0), "The start of the story: nothing found yet", 90, 26f, 12).name = "Preview_Start";
			UIKit.Button(row, "◀ Back", () => Show(At - 1), "One moment back", 90, 26f, 12).name = "Preview_Back";
			UIKit.Button(row, "Next ▶", () => Show(At + 1), "The next moment: what players find next", 90, 26f, 12).name = "Preview_Next";
			UIKit.Button(row, "All ▶|", () => Show(Moments.Count), "Every page, as at the end of the story", 90, 26f, 12).name = "Preview_All";
			UIKit.Button(row, "Open the notebook", OpenBook, "Open Raft's notebook again (Esc closes it)", 150, 26f, 12);
			UIKit.Size(UIKit.Label(row, "", 12).gameObject, -1, -1, 1);
			Button back = UIKit.Button(row, "Back to the plan", IslandTest.Back, "Leave the test world (not saved) and go back to this world plan", 180, 26f, 12);
			UIKit.Primary(back);
			UpdateBar();
		}

		static void UpdateBar()
		{
			if (barText != null) barText.text = (At > 0 && At < Moments.Count ? At + " / " + Moments.Count + ":  " : "") + Current;
		}
	}
}
