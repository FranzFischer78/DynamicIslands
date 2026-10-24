using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RaftModLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The main story in Raft's own notebook (NextTask_QuestBook.md): a world plan's islands in the story chain get a tab
	/// (title + Receiver #digits, one of Raft's ten tab colours) and pages - an intro, the quest's checklist and the
	/// island's notes once read - cloned from Raft's own pages, tabs and papers, so they look like Raft's. The book is
	/// laid out in the chain's order: First page, then each step (Raft's islands' own pages, or a plan island's), and Raft's
	/// islands the chain left out go to the back, their tabs hidden. After the last main story island: the plan's ending page.
	///   - Every machine builds the same book from the chain (StoryChain: steps, what is unlocked/done, the main story rules
	///     - sent to players), the islands' quest steps and the crew's story book (notes read): page numbers match, so
	///     Raft's page flips between players land on the same page.
	///   - Our notes are never in Raft's unlock list (NoteBook.unlockedNoteBookIndexes): Raft's save keeps only its own
	///     notes, and a world opened without the mod has Raft's book. QuestBook shows/hides them itself.
	///   - Rebuilt when anything it shows changes (a signature checked twice a second).
	/// Side quests (islands not in the chain) stay in the journal.
	/// </summary>
	public static class QuestBook
	{
		const string Mark = "_CIQB";
		/// <summary>Our notes' numbers (never in Raft's unlock list; only to tell them apart).</summary>
		const int IndexBase = 20000;
		const int PadLines = 15;
		/// <summary>Notes longer than this get a whole page (Raft's notepad) instead of half a page (a letter).</summary>
		public const int LetterLength = 300;

		static NoteBookUI ui;
		static bool captured;
		static NoteBookPage[] raftPages;
		static readonly Dictionary<NoteBookPage, uint> raftIndex = new Dictionary<NoteBookPage, uint>();
		static readonly List<Transform> raftTabOrder = new List<Transform>();
		static readonly Dictionary<ChunkPointType, Notebook_ThumbnailShortcut> raftTabs = new Dictionary<ChunkPointType, Notebook_ThumbnailShortcut>();
		static Notebook_ThumbnailShortcut firstTab;
		static RectTransform strip;
		static NoteBookPage tplLeft, tplRight;
		static NoteBookNote tplLetter, tplPad, tplPostit;
		static NoteBook_QuestItem slotTpl;
		static TMP_Text itemText;
		static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
		static readonly List<GameObject> made = new List<GameObject>();
		static readonly HashSet<ChunkPointType> hiddenRaft = new HashSet<ChunkPointType>();
		static readonly List<KeyValuePair<Notebook_ThumbnailShortcut, uint[]>> tabPages = new List<KeyValuePair<Notebook_ThumbnailShortcut, uint[]>>();
		/// <summary>Each chain step's first page in the book as laid out ("end": the ending page).</summary>
		static readonly Dictionary<string, int> stepPage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		/// <summary>A chain step's first page now (-1: not in the book).</summary>
		public static int FirstPageOfStep(string step) { int p; return step != null && stepPage.TryGetValue(step, out p) ? p : -1; }
		static string lastSig;
		static float nextCheck;
		static bool endingShown, endingKnown;
		static int noteCounter;

		/// <summary>The book as laid out last (tests): "First page[0-1] > Radio Tower[2-5] > Saltmarsh Ferry #3712[6-9]..."</summary>
		public static string Layout { get; private set; }
		/// <summary>Our tabs now in the book (tests): title, "#digits", colour 1-10, first page.</summary>
		public static readonly List<string> Tabs = new List<string>();
		/// <summary>The text of every page of ours (tests): page index -> the texts of its notes.</summary>
		public static readonly Dictionary<uint, List<string>> PageTexts = new Dictionary<uint, List<string>>();

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [quest book] " + msg); }

		#region The world's main story

		/// <summary>A main story island's tab and what its pages say.</summary>
		public class Island
		{
			public IntroRule Rule;
			public string Title = "", Frequency = "", Intro = "";
			public int Colour;
			/// <summary>The checklist's lines: done steps (struck through), the current one; empty without a quest.</summary>
			public List<string> Checklist = new List<string>();
			public bool HasQuest, QuestDone;
			/// <summary>Notes read on it: title and text, in their order on the island.</summary>
			public List<KeyValuePair<string, string>> Notes = new List<KeyValuePair<string, string>>();
		}

		/// <summary>The main story islands whose tab is open now (their frequency found / their moment came), in chain order.</summary>
		public static List<Island> OpenIslands(bool all = false)
		{
			var list = new List<Island>();
			List<IntroRule> rules = RulesNow;
			int ordinal = 0;
			foreach (string step in StepsNow)
			{
				if (StoryChain.IsRaft(step)) continue;
				IntroRule r = rules.FirstOrDefault(x => x.Id.Equals(StoryChain.RuleIdOf(step), StringComparison.OrdinalIgnoreCase));
				if (r == null) continue;
				ordinal++;
				if (!all && !OpenNow(r, step)) continue;
				list.Add(Preview ? DescribePreview(r, ordinal) : Describe(r, ordinal, DoneNow(step)));
			}
			return list;
		}

		#region Where the book's story comes from: the world's chain, or World Plans' preview of a plan

		static bool Preview { get { return QuestBookPreview.Active; } }
		/// <summary>The book's steps in order: the chain (Raft's own order when the plan doesn't change it), then the main story
		/// islands beside Raft's story.</summary>
		static List<string> StepsNow
		{
			get
			{
				if (Preview) return QuestBookPreview.Steps;
				List<string> steps = (StoryChain.Active ? StoryChain.Steps : StoryChain.BuildSteps(true, new HashSet<string>(), new IntroRule[0])).ToList();
				steps.AddRange(StoryChain.BookRules.Where(r => r.Beside).Select(r => StoryChain.RuleKey(r.Id)));
				return steps;
			}
		}
		static List<IntroRule> RulesNow { get { return Preview ? QuestBookPreview.Rules.ToList() : StoryChain.BookRules; } }
		static bool OpenNow(IntroRule r, string step)
		{
			if (Preview) return QuestBookPreview.Opened(step);
			// (beside Raft's story: on the Receiver its tab opens with its frequency; an older plan's, when it comes)
			if (r.Beside && r.Where != "receiver") return WorldDirector.Done.Contains(r.Id) || IslandWorldState.Islands.Any(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase));
			return StoryChain.Fired.Contains(r.Id) || StoryChain.Unlocked.Contains(step) || StoryChain.Done.Contains(step);
		}
		static bool DoneNow(string step)
		{
			if (Preview) return QuestBookPreview.IsDone(step);
			IntroRule r = StoryChain.IsRaft(step) ? null : StoryChain.BookRules.FirstOrDefault(x => x.Id.Equals(StoryChain.RuleIdOf(step), StringComparison.OrdinalIgnoreCase));
			return r != null && r.Beside ? StoryChain.IsDone(r) : StoryChain.Done.Contains(step);
		}
		static string EndingNow { get { return Preview ? QuestBookPreview.Plan.StoryEnding ?? "" : StoryChain.StoryEnding; } }
		static string FrequencyNow(IntroRule r) { return Preview ? (r.Where == "receiver" ? QuestBookPreview.Frequency(r.Id) : "") : StoryChain.FrequencyOf(r.Id) ?? ""; }

		#endregion

		static string IntroOf(IntroRule r, string title, string frequency)
		{
			return r.TabIntro.Trim().Length > 0 ? r.TabIntro.Trim() :
				(r.Where == "receiver" && frequency.Length > 0 ? "A new frequency: " + frequency + ".\nTune the Receiver to it to find " + title + "." : "The way to " + title + " is open.") +
				(r.Message.Trim().Length > 0 ? "\n\n" + r.Message.Trim() : "");
		}

		static void Checklist(Island i, IslandQuest q, int at)
		{
			i.HasQuest = true;
			at = Mathf.Clamp(at, 0, q.Steps.Count);
			i.QuestDone = at >= q.Steps.Count;
			i.Checklist.Add("<b>" + q.ShownTitle + "</b>");
			for (int k = 0; k < at; k++) i.Checklist.Add("<s>" + q.Steps[k].Describe() + "</s>");
			if (at < q.Steps.Count) i.Checklist.Add("> " + q.Steps[at].Describe());
			else i.Checklist.Add("Done!");
		}

		/// <summary>An island of the previewed plan: its quest and notes from its file, as far as the preview has come.</summary>
		static Island DescribePreview(IntroRule r, int ordinal)
		{
			var i = new Island { Rule = r, Title = r.TabName, Colour = ValidColour(r.TabColour) ? r.TabColour : AutoColour(ordinal) };
			i.Frequency = FrequencyNow(r);
			i.Intro = IntroOf(r, i.Title, i.Frequency);
			string island = r.What == "island" ? r.WhatArg : null;
			IslandQuest q = island != null ? IslandCache.QuestOfFile(island) : new IslandQuest();
			if (q.Exists) Checklist(i, q, QuestBookPreview.StepsDone(r.Id));
			else if (island == null) i.Checklist.Add("(a new island made in the world: its quest isn't known before)");
			Dictionary<int, KeyValuePair<string, string>> texts = island != null ? IslandCache.NoteTextsOf(island) : new Dictionary<int, KeyValuePair<string, string>>();
			foreach (int n in QuestBookPreview.NotesRead(r.Id).OrderBy(x => x))
			{
				KeyValuePair<string, string> t;
				if (texts.TryGetValue(n, out t)) i.Notes.Add(t);
			}
			Dictionary<string, string> events = island != null ? IslandCache.EventPageTextsOf(island) : new Dictionary<string, string>();
			foreach (string ev in QuestBookPreview.PagesWritten(r.Id))
			{
				string text;
				if (events.TryGetValue(ev, out text)) i.Notes.Add(new KeyValuePair<string, string>(ev.Substring(ev.IndexOf(':') + 1), text));
			}
			return i;
		}

		static Island Describe(IntroRule r, int ordinal, bool stepDone)
		{
			var i = new Island { Rule = r, Title = r.TabName, Colour = ValidColour(r.TabColour) ? r.TabColour : AutoColour(ordinal) };
			string f = FrequencyNow(r);
			i.Frequency = f ?? "";
			i.Intro = IntroOf(r, i.Title, i.Frequency);
			List<IslandWorldState.Entry> entries = IslandWorldState.Islands.Where(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase)).ToList();
			IslandWorldState.Entry entry = entries.FirstOrDefault();
			IslandQuest q = entry != null ? QuestTracker.QuestOf(entry) : null;
			if (q != null && q.Exists) Checklist(i, q, QuestTracker.StepOf(entry));
			// (its further quests after the main one - LM4)
			if (entry != null)
				for (int n = 1, count = QuestTracker.QuestsOf(entry); n < count; n++)
				{
					IslandQuest more = QuestTracker.QuestOf(entry, n);
					if (!more.Exists) continue;
					i.Checklist.Add(more.ShownTitle + ":");
					Checklist(i, more, QuestTracker.StepOf(entry, n));
				}
			else if (entry == null) i.Checklist.Add(stepDone ? "Done." : "Not reached yet.");
			// (its notes read and the pages its events wrote - everything the journal would have had of it - in the order the
			// crew found them)
			foreach (IslandWorldState.Entry e in entries)
			{
				string note = "note:" + StoryBook.PageIsland(e) + ":", act = "act:" + StoryBook.PageIsland(e) + ":";
				foreach (StoryBook.Page p in StoryBook.Pages.Where(p => p.Key.StartsWith(note, StringComparison.OrdinalIgnoreCase) || p.Key.StartsWith(act, StringComparison.OrdinalIgnoreCase)))
					i.Notes.Add(new KeyValuePair<string, string>(p.Title ?? "", p.Text ?? ""));
			}
			return i;
		}

		/// <summary>The story items of the main story islands (they go into Raft's Found items, not the journal).</summary>
		public static HashSet<string> MainItemIds()
		{
			var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (!Applies) return ids;
			if (Preview)
			{
				foreach (Island i in OpenIslands())
					if (i.Rule.What == "island") foreach (StoryItemDef d in StoryItems.Of(IslandCache.Props(i.Rule.WhatArg))) ids.Add(d.Id);
				return ids;
			}
			foreach (IntroRule r in StoryChain.BookRules)
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase)))
					foreach (StoryItemDef d in StoryItems.Of(IslandCache.PropsOf(e))) ids.Add(d.Id);
			return ids;
		}

		/// <summary>A journal page that belongs to the main story (Raft's notebook shows it): a note or event page of a main
		/// story island, or a frequency of the chain - the journal leaves these out.</summary>
		public static bool IsMainPage(StoryBook.Page p)
		{
			if (!Applies || p == null) return false;
			string k = p.Key ?? "";
			if (k.StartsWith("storyfreq:", StringComparison.OrdinalIgnoreCase))
			{
				string what = k.Substring(10);
				return StoryChain.IsRaft(what) || StoryChain.BookRules.Any(r => r.Id.Equals(what, StringComparison.OrdinalIgnoreCase));
			}
			foreach (IntroRule r in StoryChain.BookRules)
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(e => e.Rule.Equals(r.Id, StringComparison.OrdinalIgnoreCase)))
				{
					string island = StoryBook.PageIsland(e);
					if (k.StartsWith("note:" + island + ":", StringComparison.OrdinalIgnoreCase) || k.StartsWith("act:" + island + ":", StringComparison.OrdinalIgnoreCase)) return true;
				}
			return false;
		}

		/// <summary>The main story is in Raft's notebook in this world (the journal says so instead of listing it).</summary>
		public static bool InNotebook { get { return Applies && StoryChain.BookRules.Count > 0; } }

		/// <summary>An island of the main story: all its quests are in Raft's notebook, so the journal's quest list keeps them
		/// with the main story's line, not under the side quests (ROADMAP X8).</summary>
		public static bool IsMainIsland(IslandWorldState.Entry e)
		{
			return Applies && e != null && !string.IsNullOrEmpty(e.Rule) && StoryChain.BookRules.Any(r => r.Id.Equals(e.Rule, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>The crew's story items of the main story (held now).</summary>
		public static List<StoryBook.Held> MainItems()
		{
			HashSet<string> ids = MainItemIds();
			if (Preview)
			{
				// (the preview shows the open islands' items as found)
				var held = new List<StoryBook.Held>();
				foreach (Island i in OpenIslands())
					if (i.Rule.What == "island")
						foreach (StoryItemDef d in StoryItems.Of(IslandCache.Props(i.Rule.WhatArg))) if (!held.Any(h => h.Def.Id == d.Id)) held.Add(new StoryBook.Held { Def = d, Count = 1 });
				return held;
			}
			return ids.Count == 0 ? new List<StoryBook.Held>() : StoryBook.Items.Where(h => ids.Contains(h.Def.Id)).ToList();
		}

		/// <summary>A colour for a tab without one: Raft's tabs use 1, 2 and 4-10 for its own islands; ours go round from 3.</summary>
		static int AutoColour(int ordinal) { return AutoColours[(ordinal - 1) % AutoColours.Length]; }

		/// <summary>Raft's nine tab sprites (there is no 3), in the order tabs without a colour get them.</summary>
		static readonly int[] AutoColours = { 8, 9, 4, 6, 5, 10, 1, 7, 2 };

		/// <summary>A tab colour Raft has a sprite for (3, or out of range: by its place).</summary>
		public static bool ValidColour(int c) { return c >= 1 && c <= IntroRule.TabColours && c != 3; }

		/// <summary>The main story is over: every main story step done (the plan's ending page shows).</summary>
		public static bool StoryOver
		{
			get
			{
				if (Preview) return QuestBookPreview.Over;
				List<string> steps = StepsNow;
				List<string> main = steps.Where(s => !StoryChain.IsRaft(s) && StoryChain.BookRules.Any(r => r.Id.Equals(StoryChain.RuleIdOf(s), StringComparison.OrdinalIgnoreCase))).ToList();
				// (Raft's islands count only when the plan's chain has them: beside Raft's own story, only the plan's)
				bool raftDone = !StoryChain.Active || steps.Where(StoryChain.IsRaft).All(s => StoryChain.Done.Contains(s) || StoryChain.TypeOfStep(s) == ChunkPointType.Landmark_Utopia);
				return main.Count > 0 && raftDone && main.All(DoneNow);
			}
		}

		/// <summary>The book differs from Raft's: a chain with main story islands, or Raft's islands left out.</summary>
		static bool Applies { get { return Preview || (StoryChain.HasSnapshot && (StoryChain.Active || StoryChain.BookRules.Count > 0)); } }

		static string Signature()
		{
			if (Preview) return "preview " + QuestBookPreview.At + "\n" + QuestBookPreview.Plan.ToText();
			if (!Applies) return "raft";
			var parts = new List<string> { string.Join(",", StoryChain.Steps.ToArray()), string.Join(",", StoryChain.Unlocked.OrderBy(s => s).ToArray()),
				string.Join(",", StoryChain.Done.OrderBy(s => s).ToArray()), string.Join(",", StoryChain.Fired.OrderBy(s => s).ToArray()), StoryChain.StoryEnding, "over " + StoryOver };
			foreach (IntroRule r in StoryChain.BookRules) parts.Add(r.ToLine() + "@" + StoryChain.FrequencyOf(r.Id));
			foreach (StoryBook.Held h in MainItems()) parts.Add("item " + h.Def.Id + "=" + h.Count);
			foreach (Island i in OpenIslands()) parts.Add(i.Title + ":" + string.Join(";", i.Checklist.ToArray()) + ":" + i.Notes.Count);
			return string.Join("\n", parts.ToArray());
		}

		#endregion

		#region Every frame

		/// <summary>Every frame in a world (DynamicIslands.Update): the book follows the chain.</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextCheck) return;
			nextCheck = Time.unscaledTime + 0.5f;
			NoteBookUI now = null;
			try { Network_Player p = RAPI.GetLocalPlayer(); now = p != null ? p.NoteBookUI : null; } catch { }
			if (now != ui) { ui = now; captured = false; made.Clear(); lastSig = null; endingKnown = false; raftIndex.Clear(); raftTabOrder.Clear(); raftTabs.Clear(); }
			if (ui == null) return;
			string sig = Signature();
			if (sig == lastSig) return;
			lastSig = sig;
			try { Apply(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [quest book] " + e); }
		}

		/// <summary>Tests: lays the book out again now.</summary>
		public static void Refresh() { nextCheck = 0; lastSig = null; Tick(); }

		#endregion

		#region Raft's book

		static void Capture()
		{
			Traverse tr = Traverse.Create(ui);
			raftPages = (tr.Field("pageObjs").GetValue<NoteBookPage[]>() ?? new NoteBookPage[0]).Where(p => p != null).ToArray();
			raftIndex.Clear();
			foreach (NoteBookPage p in raftPages) raftIndex[p] = p.pageIndex;
			tplLeft = raftPages.FirstOrDefault(p => p.pageIndex == 6);
			tplRight = raftPages.FirstOrDefault(p => p.pageIndex == 7);
			NoteBookNote[] notes = tr.Field("notes").GetValue<NoteBookNote[]>() ?? new NoteBookNote[0];
			tplLetter = notes.FirstOrDefault(n => n != null && n.noteIndex == 12);
			tplPad = notes.FirstOrDefault(n => n != null && n.noteIndex == 1);
			tplPostit = notes.FirstOrDefault(n => n != null && n.noteIndex == 2 && !n.isFrequencyThumbnail);
			raftTabs.Clear();
			raftTabOrder.Clear();
			foreach (Notebook_ThumbnailShortcut t in ui.GetComponentsInChildren<Notebook_ThumbnailShortcut>(true))
			{
				if (t.name.EndsWith(Mark)) continue;
				NoteBookNote n = t.GetComponent<NoteBookNote>();
				if (n != null && n.isFrequencyThumbnail) raftTabs[n.thumbNailLandmarkType] = t;
				else if (t.targetPage != null && t.targetPage.pageIndex == 0) firstTab = t;
				strip = t.transform.parent as RectTransform;
			}
			if (strip != null) foreach (Transform c in strip) raftTabOrder.Add(c);
			NoteBook_QuestItem[] slots = tr.Field("questItemUIs").GetValue<NoteBook_QuestItem[]>() ?? new NoteBook_QuestItem[0];
			slotTpl = slots.FirstOrDefault(s => s != null);
			Transform desc = ui.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Text_ItemDescription");
			itemText = desc != null ? desc.GetComponent<TMP_Text>() : null;
			sprites.Clear();
			foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
				if (s != null && s.name.StartsWith("NoteBook_Thumbnail_") && !sprites.ContainsKey(s.name)) sprites[s.name] = s;
			captured = tplLeft != null && tplRight != null && tplLetter != null && tplPad != null && strip != null && raftTabs.Count > 0;
			Log(captured ? "Raft's notebook read: " + raftPages.Length + " pages, " + raftTabs.Count + " island tabs" : "Raft's notebook isn't as expected: the main story stays in the journal");
		}

		/// <summary>Raft's book as it came (our pages and tabs taken out, Raft's page numbers and tab order back).</summary>
		static void Restore()
		{
			// (Raft's open spread may be one of ours: point it at Raft's first one, or Raft's redraw stops on a page that's gone
			// - the book stayed blank after the preview moved on)
			Traverse tr = Traverse.Create(ui);
			NoteBookPage left = raftPages.FirstOrDefault(p => raftIndex[p] == 0), right = raftPages.FirstOrDefault(p => raftIndex[p] == 1);
			NoteBookPage openLeft = tr.Field("leftPage").GetValue<NoteBookPage>(), openRight = tr.Field("rightPage").GetValue<NoteBookPage>();
			if (openLeft == null || openLeft.name.EndsWith(Mark)) { if (openLeft != null) openLeft.gameObject.SetActive(false); tr.Field("leftPage").SetValue(left); }
			if (openRight == null || openRight.name.EndsWith(Mark)) { if (openRight != null) openRight.gameObject.SetActive(false); tr.Field("rightPage").SetValue(right); }
			// (at once: the tab strip's order and Raft's page list are set up again in the same frame)
			foreach (GameObject g in made) if (g != null) UnityEngine.Object.DestroyImmediate(g);
			made.Clear();
			tabPages.Clear();
			hiddenRaft.Clear();
			PageTexts.Clear();
			Tabs.Clear();
			foreach (KeyValuePair<NoteBookPage, uint> kv in raftIndex) if (kv.Key != null) { kv.Key.pageIndex = kv.Value; SetPageNumber(kv.Key); }
			Traverse.Create(ui).Field("pageObjs").SetValue(raftPages);
			for (int k = 0; k < raftTabOrder.Count; k++) if (raftTabOrder[k] != null) raftTabOrder[k].SetSiblingIndex(k);
		}

		static void SetPageNumber(NoteBookPage p)
		{
			Transform n = p.transform.Find("Page Title/Page number");
			TMP_Text t = n != null ? n.GetComponent<TMP_Text>() : null;
			if (t != null) t.text = (p.pageIndex + 1).ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Raft's pages of one island (its tab's page and every page with the same title).</summary>
		static List<NoteBookPage> RaftGroup(ChunkPointType t)
		{
			Notebook_ThumbnailShortcut tab;
			if (!raftTabs.TryGetValue(t, out tab) || tab.targetPage == null) return new List<NoteBookPage>();
			string title = (string)Traverse.Create(tab.targetPage).Field("pageTitleString").GetValue() ?? "";
			return raftPages.Where(p => (string)Traverse.Create(p).Field("pageTitleString").GetValue() == title && title.Length > 0).OrderBy(p => raftIndex[p]).ToList();
		}

		static void Apply()
		{
			if (!captured) Capture();
			if (!captured) return;
			Traverse tr = Traverse.Create(ui);
			NoteBookPage showing = PageAt((uint)tr.Field("currentPageIndex").GetValue<uint>());
			Restore();
			if (!Applies) { Layout = "Raft's own"; Finish(tr, showing); return; }

			var pages = new List<NoteBookPage>();
			var layout = new List<string>();
			uint next = 0;
			var placed = new HashSet<NoteBookPage>();
			Action<string, List<NoteBookPage>> place = (name, group) =>
			{
				if (group.Count == 0) return;
				uint first = next;
				foreach (NoteBookPage p in group)
				{
					p.pageIndex = next++; SetPageNumber(p); pages.Add(p); placed.Add(p);
					if (p.name.EndsWith(Mark)) PageTexts[p.pageIndex] = p.GetComponentsInChildren<NoteBookNote>(true).Select(n => n.GetComponentsInChildren<TMP_Text>(true).Select(x => x.text).FirstOrDefault() ?? "").ToList();
				}
				if (next % 2 == 1) next++;
				layout.Add(name + "[" + first + "-" + (next - 1) + "]");
			};
			place("First page", raftPages.Where(p => raftIndex[p] <= 1).OrderBy(p => raftIndex[p]).ToList());
			if (firstTab != null) firstTab.transform.SetSiblingIndex(0);
			int tabAt = 1;
			List<Island> open = OpenIslands();
			var stepTypes = new HashSet<ChunkPointType>();
			stepPage.Clear();
			foreach (string step in StepsNow)
			{
				if (StoryChain.IsRaft(step))
				{
					ChunkPointType t = StoryChain.TypeOfStep(step);
					stepTypes.Add(t);
					stepPage[step] = (int)next;
					place(StoryOrder.Name(t), RaftGroup(t));
					Notebook_ThumbnailShortcut tab;
					if (raftTabs.TryGetValue(t, out tab)) tab.transform.SetSiblingIndex(tabAt++);
					continue;
				}
				Island i = open.FirstOrDefault(x => x.Rule.Id.Equals(StoryChain.RuleIdOf(step), StringComparison.OrdinalIgnoreCase));
				if (i == null) continue;
				List<NoteBookPage> group = BuildIsland(i);
				stepPage[step] = (int)next;
				place(i.Title + (i.Frequency.Length > 0 ? " " + i.Frequency : ""), group);
				Notebook_ThumbnailShortcut ours = MakeTab(i, group[0]);
				ours.transform.SetSiblingIndex(tabAt++);
				tabPages.Add(new KeyValuePair<Notebook_ThumbnailShortcut, uint[]>(ours, group.Select(p => p.pageIndex).ToArray()));
				Tabs.Add(i.Title + "|" + i.Frequency + "|" + i.Colour + "|" + group[0].pageIndex);
			}
			bool over = StoryOver;
			if (over && EndingNow.Trim().Length > 0) { stepPage["end"] = (int)next; place("The end", BuildEnding()); }
			if (endingKnown && over && !endingShown && Raft_Network.IsHost && !Preview)
				StoryChain.Announce("The end of the story", "A last page waits in your notebook.");
			endingShown = over; endingKnown = true;
			// Raft's islands the chain hasn't got: their pages at the back, their tabs hidden
			foreach (KeyValuePair<ChunkPointType, Notebook_ThumbnailShortcut> kv in raftTabs)
				if (!stepTypes.Contains(kv.Key)) { hiddenRaft.Add(kv.Key); place("(" + StoryOrder.Name(kv.Key) + ", not in this story)", RaftGroup(kv.Key)); }
			foreach (NoteBookPage p in raftPages.Where(p => !placed.Contains(p))) { p.pageIndex = next++; pages.Add(p); }
			tr.Field("pageObjs").SetValue(pages.ToArray());
			BuildItems();
			if (tabPages.Count + raftTabs.Count - hiddenRaft.Count > 11) MakeTabsScroll();
			Layout = string.Join(" > ", layout.ToArray());
			Log("Laid out: " + Layout);
			Finish(tr, showing);
		}

		static NoteBookPage PageAt(uint index)
		{
			NoteBookPage[] all = Traverse.Create(ui).Field("pageObjs").GetValue<NoteBookPage[]>() ?? new NoteBookPage[0];
			return all.FirstOrDefault(p => p != null && p.pageIndex == index);
		}

		/// <summary>Raft redraws the book; the page that was open stays open (at its new number).</summary>
		static void Finish(Traverse tr, NoteBookPage showing)
		{
			tr.Field("highestUnlockedPageIndex").SetValue(1u);
			uint at = showing != null && showing.gameObject != null ? showing.pageIndex - showing.pageIndex % 2 : 0;
			tr.Field("currentPageIndex").SetValue(at);
			tr.Method("UpdatePages").GetValue();
			if ((uint)tr.Field("currentPageIndex").GetValue<uint>() > (uint)tr.Field("highestUnlockedPageIndex").GetValue<uint>()) { tr.Field("currentPageIndex").SetValue(0u); tr.Method("UpdatePages").GetValue(); }
		}

		/// <summary>After Raft sets its notes' visibility: Raft's tabs of islands not in this story stay hidden.</summary>
		internal static void AfterRaftVisibility(NoteBookUI book)
		{
			if (book != ui) return;
			foreach (ChunkPointType t in hiddenRaft)
			{
				Notebook_ThumbnailShortcut tab;
				if (raftTabs.TryGetValue(t, out tab) && tab != null) tab.gameObject.SetActive(false);
			}
			if (!Preview) return;
			// (the preview: Raft's islands of the chain as far as it has come - their tab and every note of theirs shown, without
			// touching Raft's list of notes found; the next real redraw puts Raft's own state back)
			foreach (string step in StepsNow.Where(StoryChain.IsRaft))
			{
				ChunkPointType t = StoryChain.TypeOfStep(step);
				bool open = QuestBookPreview.Opened(step);
				Notebook_ThumbnailShortcut tab;
				if (raftTabs.TryGetValue(t, out tab) && tab != null) tab.gameObject.SetActive(open);
				foreach (NoteBookPage p in RaftGroup(t))
					foreach (NoteBookNote n in (NoteBookNote[])Traverse.Create(p).Field("notes").GetValue() ?? new NoteBookNote[0])
						if (n != null) { n.isUnlocked = open; n.gameObject.SetActive(open); }
			}
		}

		/// <summary>After a page flip: the tab strip shows the open island's tab.</summary>
		internal static void AfterFlip(NoteBookUI book)
		{
			if (book != ui || strip == null) return;
			ScrollRect sr = (strip.parent != null ? strip.parent.GetComponent<ScrollRect>() : null);
			if (sr == null) return;
			uint at = (uint)Traverse.Create(ui).Field("currentPageIndex").GetValue<uint>();
			Transform tab = null;
			foreach (KeyValuePair<Notebook_ThumbnailShortcut, uint[]> kv in tabPages) if (kv.Value.Contains(at) || kv.Value.Contains(at + 1)) tab = kv.Key.transform;
			if (tab == null) foreach (Notebook_ThumbnailShortcut t in raftTabs.Values) if (t.targetPage != null && RaftGroupOf(t).Any(p => p.pageIndex == at || p.pageIndex == at + 1)) tab = t.transform;
			if (tab == null) return;
			ScrollTo(sr, tab as RectTransform);
		}

		static List<NoteBookPage> RaftGroupOf(Notebook_ThumbnailShortcut t)
		{
			NoteBookNote n = t.GetComponent<NoteBookNote>();
			return n != null ? RaftGroup(n.thumbNailLandmarkType) : new List<NoteBookPage>();
		}

		static void ScrollTo(ScrollRect sr, RectTransform tab)
		{
			if (tab == null) return;
			float content = sr.content.rect.height, view = ((RectTransform)sr.transform).rect.height;
			if (content <= view) return;
			float y = -tab.anchoredPosition.y; // (from the strip's top)
			float pos = Mathf.Clamp01((y - view / 2f) / (content - view));
			sr.verticalNormalizedPosition = 1f - pos;
		}

		/// <summary>The tab strip (Raft's ThumbNails, as tall as the book) inside a viewport that scrolls (mouse wheel).</summary>
		static void MakeTabsScroll()
		{
			if (strip == null || strip.parent.name == "CI_TabViewport" + Mark) return;
			var vp = new GameObject("CI_TabViewport" + Mark, typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
			RectTransform v = vp.GetComponent<RectTransform>();
			v.SetParent(strip.parent, false);
			v.SetSiblingIndex(strip.GetSiblingIndex());
			v.anchorMin = strip.anchorMin; v.anchorMax = strip.anchorMax; v.pivot = strip.pivot;
			// (wider on the left only: a tab grows to the left when the mouse is on it; the right edge stays at the book's)
			v.anchoredPosition = strip.anchoredPosition - new Vector2(15f, 0f); v.sizeDelta = new Vector2(strip.sizeDelta.x + 30f, strip.sizeDelta.y);
			vp.GetComponent<Image>().color = new Color(1, 1, 1, 0.003f); // (catches the wheel between tabs)
			strip.SetParent(v, false);
			strip.anchorMin = new Vector2(0.5f, 1f); strip.anchorMax = new Vector2(0.5f, 1f); strip.pivot = new Vector2(0.5f, 1f);
			strip.anchoredPosition = new Vector2(15f, 0f);
			ContentSizeFitter fit = strip.GetComponent<ContentSizeFitter>() ?? strip.gameObject.AddComponent<ContentSizeFitter>();
			fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			ScrollRect sr = vp.GetComponent<ScrollRect>();
			sr.content = strip; sr.viewport = v; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 20f;
			LayoutRebuilder.ForceRebuildLayoutImmediate(strip);
			Log("The tabs scroll now (more than fit on the book)");
		}

		/// <summary>Tests: scroll the tab strip (0 top, 1 bottom); false when it doesn't scroll.</summary>
		public static bool ScrollTabs(float f)
		{
			ScrollRect sr = strip != null ? (strip.parent != null ? strip.parent.GetComponent<ScrollRect>() : null) : null;
			if (sr == null) return false;
			sr.verticalNormalizedPosition = 1f - Mathf.Clamp01(f);
			return true;
		}

		#endregion

		#region Our pages

		static NoteBookPage NewPage(bool left, string title)
		{
			NoteBookPage tpl = left ? tplLeft : tplRight;
			NoteBookPage pg = UnityEngine.Object.Instantiate(tpl, tpl.transform.parent);
			pg.name = (left ? "NoteBookPageL_" : "NoteBookPageR_") + title + Mark;
			made.Add(pg.gameObject);
			Traverse.Create(pg).Field("pageTitleString").SetValue(title);
			Transform tt = pg.transform.Find("Page Title");
			if (tt != null && tt.GetComponent<TMP_Text>() != null) tt.GetComponent<TMP_Text>().text = title;
			foreach (NoteBookNote n in pg.GetComponentsInChildren<NoteBookNote>(true)) UnityEngine.Object.DestroyImmediate(n.gameObject);
			Traverse.Create(pg).Field("notes").SetValue(new NoteBookNote[0]);
			pg.gameObject.SetActive(false);
			return pg;
		}

		/// <summary>A paper on a page: a copy of one of Raft's notes, our text on it (top = its top edge from the page's top).</summary>
		static NoteBookNote AddPaper(NoteBookPage pg, NoteBookNote tpl, string text, float top, float height = 0f)
		{
			Transform holder = pg.transform.Find("Notes") ?? pg.transform;
			NoteBookNote n = UnityEngine.Object.Instantiate(tpl, holder);
			n.name = "NoteBookNote_" + (IndexBase + noteCounter) + Mark;
			n.noteIndex = IndexBase + noteCounter++;
			n.landmarkType = ChunkPointType.None;
			n.isFrequencyThumbnail = false;
			n.isUnlocked = true;
			Traverse.Create(n).Field("voiceActor").SetValue(null);
			Traverse.Create(n).Field("voiceData").SetValue(null);
			foreach (Component c in n.GetComponentsInChildren<Component>(true))
				if (c != null && c.GetType().Name == "Localize") UnityEngine.Object.DestroyImmediate(c);
			Transform play = n.transform.Find("Play&Stop Button");
			if (play != null) play.gameObject.SetActive(false);
			RectTransform r = (RectTransform)n.transform;
			r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
			r.localEulerAngles = Vector3.zero;
			if (height > 0f) r.sizeDelta = new Vector2(r.sizeDelta.x, height);
			r.anchoredPosition = new Vector2(0f, -top - r.sizeDelta.y / 2f);
			TMP_Text[] tx = n.GetComponentsInChildren<TMP_Text>(true);
			if (tx.Length > 0)
			{
				tx[0].text = text;
				tx[0].enableAutoSizing = true;
				tx[0].fontSizeMax = tx[0].fontSize;
				tx[0].fontSizeMin = 5f;
			}
			for (int k = 1; k < tx.Length; k++) tx[k].text = "";
			n.gameObject.SetActive(true);
			var list = ((NoteBookNote[])Traverse.Create(pg).Field("notes").GetValue() ?? new NoteBookNote[0]).ToList();
			list.Add(n);
			Traverse.Create(pg).Field("notes").SetValue(list.ToArray());
			return n;
		}

		/// <summary>A main story island's pages: the intro and the start of the checklist, more checklist pages if long, then
		/// its notes, two to a page.</summary>
		static List<NoteBookPage> BuildIsland(Island i)
		{
			var group = new List<NoteBookPage>();
			NoteBookPage first = NewPage(true, i.Title);
			group.Add(first);
			AddPaper(first, tplLetter, i.Intro, 30f, 125f);
			// (no post-it with the frequency: Raft's post-it has its own number printed on it - the tab and the intro give ours)
			// (the checklist on the page opposite the intro, and on as many pages as it needs)
			int line = 0;
			List<string> rows = i.Checklist;
			while (line < rows.Count)
			{
				NoteBookPage more = NewPage(group.Count % 2 == 0, i.Title);
				group.Add(more);
				int take = Mathf.Min(PadLines, rows.Count - line);
				AddPaper(more, tplPad, string.Join("\n", rows.Skip(line).Take(take).ToArray()), 30f, 340f);
				line += take;
			}
			NoteBookPage page = null;
			int onPage = 0;
			foreach (KeyValuePair<string, string> note in i.Notes)
			{
				string title = note.Key.Trim();
				string text = (title.Length > 0 && !title.Equals("note", StringComparison.OrdinalIgnoreCase) ? "<b>" + title + "</b>\n\n" : "") + note.Value.Trim();
				// (a long note gets a page of its own on Raft's tall notepad: on a half-page letter it ran off the paper)
				if (note.Value.Trim().Length > LetterLength)
				{
					page = NewPage(group.Count % 2 == 0, i.Title); group.Add(page);
					AddPaper(page, tplPad, text, 30f, 340f);
					onPage = 2;
					continue;
				}
				if (page == null || onPage == 2) { page = NewPage(group.Count % 2 == 0, i.Title); group.Add(page); onPage = 0; }
				AddPaper(page, tplLetter, text, onPage == 0 ? 35f : 205f, 160f);
				onPage++;
			}
			// (the page numbers come with the layout; the texts are listed by the final number for tests)
			return group;
		}

		/// <summary>The main story's items in Raft's Found items: copies of Raft's own slots after Raft's quest items.</summary>
		static void BuildItems()
		{
			Items.Clear();
			if (slotTpl == null) return;
			foreach (StoryBook.Held h in MainItems())
			{
				GameObject g = UnityEngine.Object.Instantiate(slotTpl.gameObject, slotTpl.transform.parent);
				g.name = "QuestItem_" + h.Def.Id + Mark;
				made.Add(g);
				// (Raft's slot script reads one of Raft's quest items: ours shows its own picture and name)
				foreach (Component c in g.GetComponentsInChildren<Component>(true))
					if (c is NoteBook_QuestItem) UnityEngine.Object.DestroyImmediate(c);
				Transform img = g.transform.Find("ItemImage");
				Sprite s = StoryItems.IconSprite(h.Def.Icon);
				if (img != null && img.GetComponent<Image>() != null && s != null) img.GetComponent<Image>().sprite = s;
				Transform amount = g.transform.Find("AmountText"), amountBack = g.transform.Find("AmountBackground");
				if (amount != null && amount.GetComponent<TMP_Text>() != null) amount.GetComponent<TMP_Text>().text = h.Count.ToString(CultureInfo.InvariantCulture);
				if (amount != null) amount.gameObject.SetActive(h.Count > 1);
				if (amountBack != null) amountBack.gameObject.SetActive(h.Count > 1);
				QuestBookItemHover hover = g.AddComponent<QuestBookItemHover>();
				hover.Text = itemText;
				hover.Label = h.Def.ShownName;
				g.transform.SetAsLastSibling();
				g.SetActive(true);
				Items.Add(h.Def.ShownName + (h.Count > 1 ? " x" + h.Count : ""));
			}
		}

		/// <summary>Our items now in Found items (tests).</summary>
		public static readonly List<string> Items = new List<string>();

		static List<NoteBookPage> BuildEnding()
		{
			NoteBookPage pg = NewPage(true, "The end");
			AddPaper(pg, tplPad, EndingNow.Trim(), 40f, 320f);
			return new List<NoteBookPage> { pg };
		}

		static Notebook_ThumbnailShortcut MakeTab(Island i, NoteBookPage target)
		{
			Notebook_ThumbnailShortcut tpl = raftTabs.Values.First();
			Notebook_ThumbnailShortcut tab = UnityEngine.Object.Instantiate(tpl, tpl.transform.parent);
			tab.name = "ThumbNailButton_" + i.Title + Mark;
			made.Add(tab.gameObject);
			tab.targetPage = target;
			// (Raft sets it in Awake, which a copy made while the book is shut hasn't had yet)
			Traverse.Create(tab).Field("notebookUI").SetValue(ui);
			NoteBookNote n = tab.GetComponent<NoteBookNote>();
			if (n != null) { n.noteIndex = IndexBase + noteCounter++; n.isFrequencyThumbnail = false; n.thumbNailLandmarkType = ChunkPointType.None; n.isUnlocked = true; }
			foreach (Component c in tab.GetComponentsInChildren<Component>(true))
				if (c != null && (c.GetType().Name == "FrequencyTextMeshProUI" || c.GetType().Name == "Localize")) UnityEngine.Object.DestroyImmediate(c);
			foreach (TMP_Text t in tab.GetComponentsInChildren<TMP_Text>(true))
			{
				if (t.name == "DestinationName") { t.text = i.Title; t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = 6f; }
				if (t.name == "DestinationFrequency") { t.text = i.Frequency; t.gameObject.SetActive(i.Frequency.Length > 0); }
			}
			Sprite s;
			Image img = tab.GetComponent<Image>();
			if (img != null && sprites.TryGetValue("NoteBook_Thumbnail_" + i.Colour, out s)) { img.sprite = s; img.color = Color.white; }
			tab.gameObject.SetActive(true);
			return tab;
		}

		#endregion

		/// <summary>Tests: the open island tab's page, or -1.</summary>
		public static int PageOfTab(string title)
		{
			string t = Tabs.FirstOrDefault(x => x.Split('|')[0].Equals(title, StringComparison.OrdinalIgnoreCase));
			return t != null ? int.Parse(t.Split('|')[3]) : -1;
		}

		/// <summary>Tests: presses our tab (as a click does).</summary>
		public static bool PressTab(string title)
		{
			foreach (KeyValuePair<Notebook_ThumbnailShortcut, uint[]> kv in tabPages)
				if (kv.Key.name == "ThumbNailButton_" + title + Mark) { kv.Key.OnThumbnailButtonPress(); return true; }
			return false;
		}

		/// <summary>Tests: is Raft's tab of an island shown in the book.</summary>
		public static bool RaftTabShown(ChunkPointType t)
		{
			Notebook_ThumbnailShortcut tab;
			return raftTabs.TryGetValue(t, out tab) && tab != null && tab.gameObject.activeSelf;
		}
	}

	/// <summary>A story item's slot in Found items: its name under the title while the mouse is on it, as Raft's do.</summary>
	public class QuestBookItemHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
	{
		public TMP_Text Text;
		public string Label = "";
		public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (Text != null) Text.text = Label; }
		public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (Text != null && Text.text == Label) Text.text = ""; }
	}

	[HarmonyPatch(typeof(NoteBookUI), "UpdateAllNotesVisibility")]
	static class QuestBookVisibility
	{
		static void Postfix(NoteBookUI __instance) { try { QuestBook.AfterRaftVisibility(__instance); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [quest book] " + e.Message); } }
	}

	[HarmonyPatch(typeof(NoteBookUI), "FlipToPageLocally")]
	static class QuestBookFlip
	{
		static void Postfix(NoteBookUI __instance) { try { QuestBook.AfterFlip(__instance); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [quest book] " + e.Message); } }
	}
}
