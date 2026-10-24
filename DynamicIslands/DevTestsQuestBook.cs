using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DynamicIslands.Editor;

namespace DynamicIslands
{
	/// <summary>
	/// The quest book spike (NextTask_QuestBook.md, Q0): what Raft's notebook is made of, and whether the mod can add
	/// tabs and pages to it. CINoteBookDump writes the book's objects to notebook_dump.txt; CINoteBookOpen opens it at a
	/// page (pictures); CINoteBookUnlockAll unlocks every note of Raft's (test worlds only).
	/// </summary>
	public static partial class DevTests
	{
		static NoteBookUI LocalNoteBookUI()
		{
			Network_Player p = null;
			try { p = RAPI.GetLocalPlayer(); } catch { }
			return p != null ? p.NoteBookUI : null;
		}

		static string RectOf(Transform t)
		{
			RectTransform r = t as RectTransform;
			if (r == null) return "pos=" + t.localPosition;
			return "anch=" + r.anchoredPosition + " size=" + r.sizeDelta + " min=" + r.anchorMin + " max=" + r.anchorMax + " piv=" + r.pivot + " rot=" + t.localEulerAngles.z.ToString("0") + " scale=" + t.localScale.x.ToString("0.##");
		}

		static void DumpTree(StringBuilder sb, Transform t, string indent, int depth)
		{
			string comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).ToArray());
			TMP_Text text = t.GetComponent<TMP_Text>();
			Image img = t.GetComponent<Image>();
			sb.Append(indent).Append(t.gameObject.activeSelf ? "" : "(off) ").Append(t.name).Append(" [").Append(comps).Append("] ").Append(RectOf(t));
			if (text != null) sb.Append(" TEXT=\"").Append((text.text ?? "").Replace("\n", "\\n").Substring(0, Math.Min(80, (text.text ?? "").Length))).Append("\" font=").Append(text.font != null ? text.font.name : "-").Append(" fs=").Append(text.fontSize);
			if (img != null) sb.Append(" IMG=").Append(img.sprite != null ? img.sprite.name : "-").Append(" col=").Append(img.color);
			sb.AppendLine();
			if (depth <= 0) { if (t.childCount > 0) sb.Append(indent).Append("  ... ").Append(t.childCount).AppendLine(" children"); return; }
			foreach (Transform c in t) DumpTree(sb, c, indent + "  ", depth - 1);
		}

		static string NbPath(Transform t, Transform root)
		{
			var parts = new List<string>();
			for (Transform x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
			return string.Join("/", parts.ToArray());
		}

		[ConsoleCommand(name: "CINoteBookDump", docs: "Dev, world: writes Raft's notebook (pages, notes, tabs, found items, the whole object tree) to Mods\\DynamicIslands\\notebook_dump.txt (quest book spike)")]
		public static void NoteBookDumpCommand()
		{
			NoteBookUI ui = LocalNoteBookUI();
			if (ui == null) { Fail("NoteBookDump: no local player's notebook (in a world?)"); return; }
			var sb = new StringBuilder();
			Traverse tr = Traverse.Create(ui);
			NoteBookPage[] pages = tr.Field("pageObjs").GetValue<NoteBookPage[]>() ?? new NoteBookPage[0];
			NoteBookNote[] notes = tr.Field("notes").GetValue<NoteBookNote[]>() ?? new NoteBookNote[0];
			Canvas canvas = tr.Field("noteBookCanvas").GetValue<Canvas>();
			Transform root = canvas != null ? canvas.transform : ui.transform;
			sb.AppendLine("NoteBookUI on " + NbPath(ui.transform, null) + "; canvas " + (canvas != null ? canvas.name + " mode=" + canvas.renderMode + " scale=" + canvas.transform.localScale : "-"));
			sb.AppendLine("currentPageIndex=" + tr.Field("currentPageIndex").GetValue() + " highestUnlockedPageIndex=" + tr.Field("highestUnlockedPageIndex").GetValue() + " isDisplayed=" + ui.isDisplayed);
			sb.AppendLine("unlocked note indexes: " + string.Join(",", (NoteBook.unlockedNoteBookIndexes ?? new List<int>()).Select(i => i.ToString()).ToArray()));
			sb.AppendLine("unlocked chunk types: " + string.Join(",", (NoteBook.unlockedChunkPointType ?? new List<ChunkPointType>()).Select(i => i.ToString()).ToArray()));
			sb.AppendLine();
			sb.AppendLine("PAGES " + pages.Length);
			foreach (NoteBookPage pg in pages)
			{
				if (pg == null) { sb.AppendLine("  null"); continue; }
				Traverse pt = Traverse.Create(pg);
				NoteBookNote[] pn = pt.Field("notes").GetValue<NoteBookNote[]>() ?? new NoteBookNote[0];
				sb.AppendLine("  page " + pg.pageIndex + " '" + pg.name + "' type=" + pg.GetType().Name + " title='" + pt.Field("pageTitleString").GetValue() + "' active=" + pg.gameObject.activeSelf + " path=" + NbPath(pg.transform, root) + " notes=" + string.Join(",", pn.Select(n => n == null ? "null" : n.noteIndex.ToString()).ToArray()));
			}
			sb.AppendLine();
			sb.AppendLine("NOTES " + notes.Length);
			foreach (NoteBookNote n in notes)
			{
				if (n == null) { sb.AppendLine("  null"); continue; }
				TMP_Text[] texts = n.GetComponentsInChildren<TMP_Text>(true);
				sb.AppendLine("  note " + n.noteIndex + " '" + n.name + "' land=" + n.landmarkType + " unlocked=" + n.isUnlocked + " thumb=" + n.isFrequencyThumbnail + "/" + n.thumbNailLandmarkType + " path=" + NbPath(n.transform, root)
					+ " comps=" + string.Join(",", n.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray())
					+ " texts=" + string.Join(" | ", texts.Select(t => t.name + ":" + (t.text ?? "").Replace("\n", "\\n").Substring(0, Math.Min(50, (t.text ?? "").Length))).ToArray()));
			}
			sb.AppendLine();
			Notebook_ThumbnailShortcut[] tabs = ui.GetComponentsInChildren<Notebook_ThumbnailShortcut>(true);
			if (tabs.Length == 0 && canvas != null) tabs = canvas.GetComponentsInChildren<Notebook_ThumbnailShortcut>(true);
			sb.AppendLine("TABS " + tabs.Length);
			foreach (Notebook_ThumbnailShortcut tab in tabs)
			{
				sb.AppendLine("  tab '" + tab.name + "' target=" + (tab.targetPage != null ? tab.targetPage.pageIndex + " " + tab.targetPage.name : "null") + " active=" + tab.gameObject.activeInHierarchy + " path=" + NbPath(tab.transform, root) + " " + RectOf(tab.transform));
				foreach (Transform c in tab.GetComponentsInChildren<Transform>(true)) if (c != tab.transform) sb.AppendLine("     " + NbPath(c, tab.transform) + " [" + string.Join(",", c.GetComponents<Component>().Where(x => x != null && !(x is Transform)).Select(x => x.GetType().Name).ToArray()) + "] " + (c.GetComponent<TMP_Text>() != null ? "TEXT=\"" + c.GetComponent<TMP_Text>().text.Replace("\n", "\\n") + "\"" : ""));
			}
			sb.AppendLine();
			NoteBook_QuestItem[] items = tr.Field("questItemUIs").GetValue<NoteBook_QuestItem[]>() ?? new NoteBook_QuestItem[0];
			sb.AppendLine("QUEST ITEM SLOTS " + items.Length + (items.Length > 0 && items[0] != null ? " first at " + NbPath(items[0].transform, root) : ""));
			sb.AppendLine();
			sb.AppendLine("TREE (from " + root.name + ")");
			DumpTree(sb, root, "", 9);
			string file = Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "notebook_dump.txt"));
			File.WriteAllText(file, sb.ToString());
			Log("PASS: notebook dumped to " + file + " (" + pages.Length + " pages, " + notes.Length + " notes, " + tabs.Length + " tabs)");
		}

		[ConsoleCommand(name: "CINoteBookOpen", docs: "Dev, world: opens Raft's notebook at a page (CINoteBookOpen [page]); CINoteBookOpen close shuts it")]
		public static void NoteBookOpenCommand(string[] args)
		{
			NoteBookUI ui = LocalNoteBookUI();
			if (ui == null) { Fail("NoteBookOpen: no notebook"); return; }
			if (args != null && args.Length > 0 && args[0] == "close") { ui.SetBookActive(false); Log("PASS: notebook closed"); return; }
			if (!ui.isDisplayed) ui.SetBookActive(true);
			uint page;
			if (args != null && args.Length > 0 && uint.TryParse(args[0], out page)) ui.FlipToPageLocally(page);
			Log("PASS: notebook open at " + Traverse.Create(ui).Field("currentPageIndex").GetValue());
		}

		[ConsoleCommand(name: "CINoteBookUnlockAll", docs: "Dev, world (test worlds only): unlocks every note in Raft's notebook (pictures)")]
		public static void NoteBookUnlockAllCommand()
		{
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("NoteBookUnlockAll: only in a test world (named CI ...)"); return; }
			NoteBookUI ui = LocalNoteBookUI();
			NoteBook nb = UnityEngine.Object.FindObjectOfType<NoteBook>();
			if (ui == null || nb == null) { Fail("NoteBookUnlockAll: no notebook"); return; }
			NoteBookNote[] notes = Traverse.Create(ui).Field("notes").GetValue<NoteBookNote[]>() ?? new NoteBookNote[0];
			int n = 0;
			foreach (NoteBookNote note in notes) if (note != null && !note.isUnlocked) { nb.UnlockSpecificNoteWithUniqueNoteIndex(note.noteIndex, true, false); n++; }
			Traverse.Create(ui).Method("UpdatePages").GetValue();
			Log("PASS: " + n + " notes unlocked");
		}

		[ConsoleCommand(name: "CIQuestBookUnit", docs: "Dev, anywhere: the quest book's plan data without a world - a rule's tab title, colour and intro (written and read back, line breaks kept), 'next coordinates when a note is read', the tab's name when none is given, a plan's ending page, older rules and plans unchanged (TEST_CATALOGUE QB1)")]
		public static void QuestBookUnitCommand()
		{
			bool ok = true;
			IntroRule r = IntroRule.Parse("ferry | island:Saltmarsh Ferry | start | receiver:800 | A ferry's band. | Ferry | after:beacon | note:5 | The Ferry | 4 | Line one\\nLine two");
			Check(ref ok, r != null && r.TabTitle == "The Ferry" && r.TabColour == 4 && r.TabIntro == "Line one\nLine two" && r.StoryDone == "note:5" && r.MainStory,
				"a main story rule with a tab title, colour and a two-line intro read: " + (r != null ? r.TabTitle + " / " + r.TabColour + " / " + r.TabIntro.Replace("\n", "\\n") + " / " + r.StoryDone : "null"));
			IntroRule back = r != null ? IntroRule.Parse(r.ToLine()) : null;
			Check(ref ok, back != null && back.ToLine() == r.ToLine() && back.TabIntro == r.TabIntro, "... written and read back the same: " + (r != null ? r.ToLine() : ""));
			Check(ref ok, r != null && r.DescribeDone() == "its note #5 is read" && IntroRule.NormalDone("Note: 7") == "note:7", "next coordinates when a note is read: " + (r != null ? r.DescribeDone() : ""));
			string oldLine = "cove | island:Wreckers' Cove | start | receiver:700 | A lantern code. | Wreckers' Cove | after:RadioTower | quest";
			IntroRule old = IntroRule.Parse(oldLine);
			Check(ref ok, old != null && old.ToLine() == oldLine && old.TabTitle == "" && old.TabColour == 0 && old.TabIntro == "" && old.MainStory, "an older story rule (eight parts) reads and writes as before, and is main story");
			IntroRule side = IntroRule.Parse("trip | island:Signal Rock | km:3 | sailing:300 | | ");
			Check(ref ok, side != null && !side.MainStory, "a rule outside the chain is a side quest");
			IntroRule wild = IntroRule.Parse("w | island:X | start | ahead:300 | | | | | | 15 | ");
			Check(ref ok, wild != null && wild.TabColour == IntroRule.TabColours && !wild.MainStory, "a colour past Raft's ten is cut to 10: " + (wild != null ? wild.TabColour.ToString() : "null"));
			IntroRule bad = IntroRule.Parse("w | island:X | start | ahead:300 | | | first | | | blue | ");
			Check(ref ok, bad != null && bad.TabColour == 0, "a colour that isn't a number = chosen by place");
			Check(ref ok, old != null && old.TabName == "Wreckers' Cove", "no tab title: the Receiver label");
			IntroRule noLabel = IntroRule.Parse("isle | island:Thornwood | start | receiver:900 | | | after:Vasagatan | quest");
			Check(ref ok, noLabel != null && noLabel.TabName == "Thornwood", "no title or label: the island's name");
			IntroRule pipe = new IntroRule { Id = "p", What = "island", WhatArg = "X", StoryPlace = "first", TabTitle = "A|B", TabIntro = "x | y\r\nz" };
			IntroRule pipeBack = IntroRule.Parse(pipe.ToLine());
			Check(ref ok, pipeBack != null && pipeBack.TabTitle == "A/B" && pipeBack.TabIntro == "x / y\nz", "a '|' in a title or intro can't break the line: " + (pipeBack != null ? pipeBack.TabTitle + " / " + pipeBack.TabIntro.Replace("\n", "\\n") : "null"));

			IntroRule beside = IntroRule.Parse("shelter | island:Shelter Atoll | quest:mine | near:mine:700:east | A shelter. | Shelter Atoll | Beside");
			Check(ref ok, beside != null && beside.Beside && beside.MainStory && !beside.InStory && !beside.Special && beside.ToLine().EndsWith("| beside | "),
				"a main story island beside Raft's story: in the notebook, not in the Receiver chain: " + (beside != null ? beside.ToLine() : "null"));
			WorldPlan bp = WorldPlan.Parse("t", "rule = " + (beside != null ? beside.ToLine() : "") + "\n");
			Check(ref ok, !bp.ChangesStory && bp.HasStory && StoryChain.BuildSteps(true, new HashSet<string>(), bp.Rules.Where(x => x.Special)).All(StoryChain.IsRaft),
				"a plan with only islands beside Raft's story leaves Raft's chain alone (but has a story for the book)");

			WorldPlan p = WorldPlan.Parse("t", "story = off\nstoryending = The sea gives up its last secret.\\n\\nThe end.\nrule = a | island:X | start | receiver:600 | | | first | quest | Start | 2 | \n");
			Check(ref ok, p.StoryEnding == "The sea gives up its last secret.\n\nThe end." && p.Rules.Count == 1 && p.Rules[0].TabColour == 2, "a plan's ending page read (line breaks kept)");
			WorldPlan p2 = WorldPlan.Parse("t", p.ToText());
			Check(ref ok, p2.StoryEnding == p.StoryEnding && p2.Rules[0].ToLine() == p.Rules[0].ToLine(), "... written and read again the same");
			WorldPlan plain = WorldPlan.Parse("t", "rule = camp | type:camp | start | ahead:350 | | \n");
			Check(ref ok, plain.StoryEnding == "" && !plain.ToText().Split('\n').Any(l => l.StartsWith("storyending")), "an older plan has no ending page and writes none");
			if (ok) Log("PASS: quest book unit"); else Fail("quest book unit");
		}

		[ConsoleCommand(name: "CIQuestBookWorld", docs: "Dev, world (host, test world 'CI ...'): the main story in Raft's notebook - a plan with two main story islands (a camp first, a sandbar on the Receiver after it) and a side quest on the Receiver: the first tab with its intro, Raft's islands at the back with their tabs hidden, a note read on its pages, the side quest only in the journal, the next tab with its #digits when the first is done, its tab pressed, the ending page when the story is over, the same book after the world file is read back, nothing of ours in Raft's save (TEST_CATALOGUE QB3, QB8-QB14, QB17)")]
		public static void QuestBookWorldCommand() { StartTest(QuestBookWorldRoutine()); }

		static IEnumerator QuestBookWorldRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("quest book world: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			var unlockedBefore = NoteBook.unlockedChunkPointType.ToList();
			var indexesBefore = NoteBook.unlockedNoteBookIndexes.ToList();
			RecieverFrequency[] freqBefore = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.ToArray() : null;
			var made = new List<int>();
			var pageKeys = new List<string>();
			try
			{
				StoryChain.Reset();
				WorldPlan plan = WorldPlan.Parse("CI quest book", "story = off\nstoryending = The end test.\\nThank you.\n" +
					"rule = a | type:camp | start | receiver:300 | | Camp A | first | visit | Alpha Camp | 9 | Hello\\nworld\n" +
					"rule = b | type:sandbar | start | receiver:400 | | Bar | after:a | visit\n" +
					"rule = c | type:sandbar | start | receiver:400 | | Side\n");
				StoryChain.FromPlan(plan);
				StoryChain.Tick(); StoryChain.OnTuned(StoryChain.TypeOfRule("a")); // (its coordinates found at the start: a player tunes to them)
				yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == "a"), 60f);
				IslandWorldState.Entry a = IslandWorldState.Islands.FirstOrDefault(e => e.Rule == "a");
				if (a != null) made.Add(a.Id);
				QuestBook.Refresh();
				Check(ref ok, a != null, "the first main story island came (" + (a != null ? a.HostName : "none") + ")");
				Check(ref ok, QuestBook.Tabs.Count == 1 && QuestBook.Tabs[0].StartsWith("Alpha Camp|" + StoryChain.FrequencyOf("a") + "|9|2"), "one tab of ours, with its coordinates: " + string.Join(" ; ", QuestBook.Tabs.ToArray()));
				Check(ref ok, (QuestBook.Layout ?? "").StartsWith("First page[0-1] > Alpha Camp " + StoryChain.FrequencyOf("a") + "[2-") && QuestBook.Layout.Contains("(Radio Tower, not in this story)"), "the book: " + QuestBook.Layout);
				Check(ref ok, !StoryOrder.Chain.Any(QuestBook.RaftTabShown), "Raft's story is off: none of Raft's tabs shown");
				Check(ref ok, QuestBook.PageTexts.Any(kv => kv.Key == 2 && kv.Value.Any(t => t == "Hello\nworld")), "its first page has the intro written in the plan");
				bool hasQuest = a != null && QuestTracker.QuestOf(a).Exists;
				Check(ref ok, hasQuest == QuestBook.PageTexts.Any(kv => kv.Value.Any(t => t.StartsWith("<b>"))), "a checklist page when the island has a quest (" + (hasQuest ? "it has" : "it has none") + ")");

				// A note read on it: on its pages, not in the journal
				string noteKey = a != null ? "note:" + a.HostName + ":5" : "note:x:5";
				pageKeys.Add(noteKey);
				StoryBook.AddPage(noteKey, "Diary", "Dear diary, the camp is cold.", "Alpha");
				yield return new WaitForSeconds(1f);
				QuestBook.Refresh();
				Check(ref ok, QuestBook.PageTexts.Values.Any(v => v.Any(t => t.Contains("Dear diary"))), "a note read on it goes onto its pages");
				StoryBook.Page notePage = StoryBook.Pages.FirstOrDefault(p => p.Key == noteKey);
				Check(ref ok, notePage != null && QuestBook.IsMainPage(notePage), "... and is kept out of the journal");
				// A page an event writes on it ("journal page" action): its notebook pages too
				string actKey = a != null ? "act:" + a.HostName + ":3:Painted sign" : "act:x:3:Painted sign";
				pageKeys.Add(actKey);
				StoryBook.AddPage(actKey, "Painted sign", "KEEP OUT - the camp is closed.", "Alpha");
				yield return new WaitForSeconds(1f);
				QuestBook.Refresh();
				StoryBook.Page actPage = StoryBook.Pages.FirstOrDefault(p => p.Key == actKey);
				Check(ref ok, QuestBook.PageTexts.Values.Any(v => v.Any(t => t.Contains("KEEP OUT"))) && actPage != null && QuestBook.IsMainPage(actPage),
					"a page its events write goes onto its notebook pages, not the journal");

				// The side quest: in the journal, never a tab
				StoryChain.Tick();
				yield return new WaitForSeconds(1f);
				QuestBook.Refresh();
				Check(ref ok, StoryChain.Fired.Contains("c") && !QuestBook.Tabs.Any(t => t.StartsWith("Side|")), "the side quest's frequency came and it has no tab");
				StoryBook.Page sidePage = StoryBook.Pages.FirstOrDefault(p => p.Key == "storyfreq:c");
				Check(ref ok, sidePage != null && !QuestBook.IsMainPage(sidePage), "its frequency stays in the journal");

				// The first done: the next tab with its frequency
				StoryChain.MarkDone("rule:a");
				StoryChain.Tick();
				yield return new WaitForSeconds(1f);
				QuestBook.Refresh();
				string freqB = StoryChain.FrequencyOf("b");
				Check(ref ok, QuestBook.Tabs.Count == 2 && QuestBook.Tabs[1].StartsWith("Bar|" + freqB + "|"), "the first done: the second tab with its frequency " + freqB + ": " + string.Join(" ; ", QuestBook.Tabs.ToArray()));
				StoryBook.Page freqPage = StoryBook.Pages.FirstOrDefault(p => p.Key == "storyfreq:b");
				Check(ref ok, freqPage == null || QuestBook.IsMainPage(freqPage), "its frequency page belongs to the notebook, not the journal");
				NoteBookUI ui = LocalNoteBookUI();
				if (ui != null && !ui.isDisplayed) ui.SetBookActive(true);
				yield return new WaitForSeconds(0.5f);
				QuestBook.PressTab("Bar");
				yield return new WaitForSeconds(0.5f);
				uint at = ui != null ? (uint)Traverse.Create(ui).Field("currentPageIndex").GetValue<uint>() : 999;
				Check(ref ok, at == QuestBook.PageOfTab("Bar"), "its tab pressed: the book at page " + at + " (its first page " + QuestBook.PageOfTab("Bar") + ")");
				Shot("questbook_world");
				yield return new WaitForSeconds(0.6f);

				// The end
				Check(ref ok, !(QuestBook.Layout ?? "").Contains("The end"), "no ending page while the story goes on");
				StoryChain.MarkDone("rule:b");
				yield return new WaitForSeconds(0.5f);
				QuestBook.Refresh();
				Check(ref ok, QuestBook.StoryOver && (QuestBook.Layout ?? "").Contains("The end[") && QuestBook.PageTexts.Values.Any(v => v.Any(t => t == "The end test.\nThank you.")), "the story over: the ending page (" + QuestBook.Layout + ")");

				// The world file read back: the same book
				string layoutBefore = QuestBook.Layout, tabsBefore = string.Join(";", QuestBook.Tabs.ToArray());
				List<string> lines = StoryChain.WriteLines().ToList();
				StoryChain.Reset();
				foreach (string l in lines) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				StoryChain.OnWorldRead();
				QuestBook.Refresh();
				Check(ref ok, QuestBook.Layout == layoutBefore && string.Join(";", QuestBook.Tabs.ToArray()) == tabsBefore, "read back from the world file: the same book (" + QuestBook.Layout + ")");
				Check(ref ok, lines.Any(l => l.StartsWith("@storyending=")), "the ending page is kept in the world file");
				Check(ref ok, !NoteBook.unlockedNoteBookIndexes.Any(i => i >= 20000), "Raft's list of notes found (its save) has none of ours");
				if (ui != null) ui.SetBookActive(false);
			}
			finally
			{
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(e => e.Rule == "a" || e.Rule == "b" || e.Rule == "c").ToList()) made.Add(e.Id);
				if (made.Count > 0) IslandWorldState.RemoveIds(made.Distinct().ToList(), true);
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				NoteBook.unlockedNoteBookIndexes.Clear(); NoteBook.unlockedNoteBookIndexes.AddRange(indexesBefore);
				NoteBook.unlockedChunkPointType.Clear(); NoteBook.unlockedChunkPointType.AddRange(unlockedBefore);
				if (freqBefore != null) RecieverFrequency.AllFrequencies = freqBefore;
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
				QuestBook.Refresh();
			}
			if (ok) Log("PASS: quest book world"); else Fail("quest book world");
		}

		[ConsoleCommand(name: "CIQuestBookPlans", docs: "Dev, world (host, test world 'CI ...'): ROADMAP X8 - the story lines' quests in Raft's notebook, not the journal. For each plan (CIQuestBookPlans <plan>[;<plan>...], default The Abyss Expedition;The Long Voyage): its main story rules are the notebook's rules, the journal's quest list has none of its main story islands outside the main story line and keeps the side islands there, and the first main story island on the Receiver, brought, gets a tab and counts with the main story (TEST_CATALOGUE QB21)")]
		public static void QuestBookPlansCommand(string[] args)
		{
			string all = args != null && args.Length > 0 ? string.Join(" ", args) : "The Abyss Expedition;The Long Voyage";
			StartTest(QuestBookPlansRoutine(all.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList()));
		}

		static IEnumerator QuestBookPlansRoutine(List<string> names)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("quest book plans: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			WorldPlan planBefore = WorldDirector.Plan;
			var made = new List<int>();
			try
			{
				foreach (string name in names)
				{
					WorldPlan plan = WorldPlan.Load(name);
					Check(ref ok, plan != null, name + ": the plan file is there");
					if (plan == null) continue;
					StoryChain.Reset();
					WorldDirector.Plan = plan;
					StoryChain.FromPlan(plan);
					StoryChain.Tick();
					QuestBook.Refresh();
					var main = plan.Rules.Where(r => r.MainStory).ToList();
					var side = plan.Rules.Where(r => !r.MainStory).ToList();
					List<string> book = StoryChain.BookRules.Select(r => r.Id.ToLowerInvariant()).OrderBy(s => s).ToList();
					Check(ref ok, main.Count > 0 && book.SequenceEqual(main.Select(r => r.Id.ToLowerInvariant()).OrderBy(s => s)),
						name + ": its " + main.Count + " main story rules are the notebook's (" + string.Join(", ", book.ToArray()) + "); " + side.Count + " side");
					Func<IntroRule, string> title = r =>
					{
						string t = ObjectProps.Get(IslandCache.Props(r.WhatArg), IslandProps.Title);
						return t.Length > 0 ? t : r.WhatArg;
					};
					List<QuestCount.Quest> quests = QuestCount.CountHere();
					List<string> leaked = quests.Where(q => q.Group != QuestCount.PlanStory)
						.Where(q => main.Any(r => r.What == "island" && q.Name.StartsWith(title(r), StringComparison.OrdinalIgnoreCase))).Select(q => q.Group + ": " + q.Name).ToList();
					Check(ref ok, leaked.Count == 0, name + ": no main story island's quest outside the main story line" + (leaked.Count > 0 ? " - leaked: " + string.Join("; ", leaked.ToArray()) : ""));
					List<string> sideInStory = quests.Where(q => q.Group == QuestCount.PlanStory)
						.Where(q => side.Any(r => r.What == "island" && q.Name.StartsWith(title(r), StringComparison.OrdinalIgnoreCase))).Select(q => q.Name).ToList();
					Check(ref ok, sideInStory.Count == 0, name + ": no side island counted as main story" + (sideInStory.Count > 0 ? " - " + string.Join("; ", sideInStory.ToArray()) : ""));
					string text = JournalWindow.QuestList(quests);
					List<string> named = main.Where(r => r.What == "island" && text.IndexOf(title(r), StringComparison.OrdinalIgnoreCase) >= 0).Select(r => title(r)).ToList();
					Check(ref ok, text.Contains("Main story: ") && named.Count == 0, name + ": the journal's quest list has the main story as one line and names none of its islands" + (named.Count > 0 ? " - named: " + string.Join(", ", named.ToArray()) : ""));
					int sideQuests = quests.Count(q => q.Group == QuestCount.PlanIslands);
					Log(name + ": journal quest list - " + quests.Count(q => q.Group == QuestCount.PlanStory) + " main story, " + sideQuests + " side (" + string.Join(" | ", text.Split('\n').Where(l => l.Trim().Length > 0).Take(3).ToArray()) + ")");

					// The first main story island that is on the Receiver now: brought, a tab, counted with the main story
					IntroRule first = main.FirstOrDefault(r => r.Where == "receiver" && StoryChain.Fired.Contains(r.Id));
					if (first == null) { Log(name + ": no main story island on the Receiver at the start (its first comes after Raft's) - bring step left out"); continue; }
					StoryChain.OnTuned(StoryChain.TypeOfRule(first.Id));
					yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == first.Id), 120f);
					IslandWorldState.Entry at = IslandWorldState.Islands.FirstOrDefault(e => e.Rule == first.Id);
					Check(ref ok, at != null, name + ": its first main story island '" + first.Id + "' came");
					if (at == null) continue;
					made.Add(at.Id);
					yield return new WaitForSeconds(1f);
					QuestBook.Refresh();
					Check(ref ok, QuestBook.IsMainIsland(at) && QuestBook.Tabs.Count >= 1, name + ": it is a main story island with a tab (" + string.Join(" ; ", QuestBook.Tabs.ToArray()) + ")");
					quests = QuestCount.CountHere();
					string island = Behaviours.IslandTitle(at);
					List<string> out1 = quests.Where(q => q.Group != QuestCount.PlanStory && q.Name.StartsWith(island, StringComparison.OrdinalIgnoreCase)).Select(q => q.Group + ": " + q.Name).ToList();
					Check(ref ok, out1.Count == 0, name + ": come, its quests are still only with the main story" + (out1.Count > 0 ? " - " + string.Join("; ", out1.ToArray()) : ""));
					IslandWorldState.RemoveIds(made.Distinct().ToList(), true);
					made.Clear();
				}
			}
			finally
			{
				if (made.Count > 0) IslandWorldState.RemoveIds(made.Distinct().ToList(), true);
				WorldDirector.Plan = planBefore;
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
				QuestBook.Refresh();
			}
			if (ok) Log("PASS: quest book plans"); else Fail("quest book plans");
		}

		[ConsoleCommand(name: "CIQuestBookPreview", docs: "Dev, editor: World Plans' Preview notebook as a builder uses it - a plan (CIQuestBookPreview <plan>, default 'Raft 2 - The Drowned Frontier') opened, a change made and not saved, Preview notebook: the test world with Raft's book open on the plan's tabs; the step-through from the start (no tabs) one moment at a time, All; pictures; Back to World Plans with the unsaved change still there (TEST_CATALOGUE QB7). Several minutes")]
		public static void QuestBookPreviewCommand(string[] args)
		{
			// (CIQuestBookPreview <plan> stay: the preview stays open after the checks, for pictures)
			bool stay = args != null && args.Contains("stay");
			string[] words = args != null ? args.Where(a => a != "stay").ToArray() : new string[0];
			string name = words.Length > 0 ? string.Join(" ", words) : "Raft 2 - The Drowned Frontier";
			StartTest(QuestBookPreviewRoutine(name, stay));
		}

		static IEnumerator QuestBookPreviewRoutine(string name, bool stay)
		{
			if (!DynamicIslands.InEditor()) { Fail("quest book preview: in the editor"); yield break; }
			WorldPlan saved = WorldPlan.Load(name);
			if (saved == null) { Fail("quest book preview: no plan '" + name + "'"); yield break; }
			bool ok = true;
			int main = saved.Rules.Count(r => r.MainStory);
			WorldPlanWindow.Open(name);
			yield return new WaitForSeconds(0.5f);
			WorldPlan open = WorldPlanWindow.Plan;
			string marker = "Preview test " + DateTime.Now.ToString("HHmmss");
			WorldPlanWindow.RecipeDescription(marker); // (a change not saved, typed in: it must come back)
			Button preview = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.name == "Button_PreviewNotebook");
			Check(ref ok, preview != null && preview.gameObject.activeInHierarchy, "World Plans has Preview notebook");
			if (preview == null) yield break;
			preview.onClick.Invoke();
			yield return WaitFor(() => QuestBookPreview.Active && IslandTest.Testing && LoadSceneManager.IsGameSceneLoaded, 240f);
			Check(ref ok, QuestBookPreview.Active && IslandTest.Testing, "the test world with the preview (" + IslandTest.LastStep + ")");
			if (!QuestBookPreview.Active) yield break;
			yield return new WaitForSeconds(3f);
			QuestBook.Refresh();
			Check(ref ok, QuestBookPreview.At == QuestBookPreview.Moments.Count && QuestBook.Tabs.Count == main, "All: every main story tab (" + QuestBook.Tabs.Count + " of " + main + "), " + QuestBookPreview.Moments.Count + " moments");
			NoteBookUI ui = LocalNoteBookUI();
			Check(ref ok, ui != null && ui.isDisplayed, "Raft's notebook is open");
			Shot("questbook_preview_all");
			yield return new WaitForSeconds(0.6f);
			QuestBookPreview.Show(0);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, QuestBook.Tabs.Count == 0, "the start: no tab yet (" + QuestBook.Tabs.Count + ")");
			// (the plan's own islands' moments: with Raft's story on, Raft's islands come between them)
			List<int> opens = Enumerable.Range(0, QuestBookPreview.Moments.Count).Where(k => QuestBookPreview.Moments[k].Kind == "open" && !StoryChain.IsRaft(QuestBookPreview.Moments[k].Step)).ToList();
			int first = opens.Count > 0 ? opens[0] : 0, second = opens.Count > 1 ? opens[1] : QuestBookPreview.Moments.Count - 1;
			QuestBookPreview.Show(first + 1);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, QuestBook.Tabs.Count == 1, "the first island's coordinates found: its tab (" + string.Join(";", QuestBook.Tabs.ToArray()) + ") - " + QuestBookPreview.Current);
			QuestBookPreview.Show(second);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, QuestBook.Tabs.Count == 1, "everything up to the second island's coordinates: still one tab of ours - " + QuestBookPreview.Current);
			QuestBookPreview.Show(second + 1);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, main < 2 || QuestBook.Tabs.Count == 2, "the next coordinates: the second tab (" + string.Join(";", QuestBook.Tabs.ToArray()) + ")");
			uint openAt = ui != null ? (uint)Traverse.Create(ui).Field("currentPageIndex").GetValue<uint>() : 999;
			NoteBookPage leftOpen = ui != null ? Traverse.Create(ui).Field("leftPage").GetValue<NoteBookPage>() : null;
			Check(ref ok, QuestBook.PageTexts.ContainsKey(openAt) && leftOpen != null && leftOpen.pageIndex == openAt && leftOpen.gameObject.activeInHierarchy,
				"the book is open on the new island's first page (" + openAt + "), and it shows");
			Shot("questbook_preview_step");
			yield return new WaitForSeconds(0.6f);
			QuestBookPreview.Show(QuestBookPreview.Moments.Count);
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, QuestBook.Tabs.Count == main, "All again: every tab");
			Check(ref ok, !NoteBook.unlockedNoteBookIndexes.Any(i => i >= 20000), "Raft's list of notes found has none of ours");
			if (stay) { Log((ok ? "PASS" : "FAIL") + ": quest book preview (staying in the preview)"); yield break; }
			IslandTest.Back();
			yield return WaitFor(() => DynamicIslands.InEditor() && WorldPlanWindow.IsOpen, 180f);
			Check(ref ok, WorldPlanWindow.IsOpen && WorldPlanWindow.Plan != null && WorldPlanWindow.Plan.Name == name && WorldPlanWindow.Plan.Description == marker, "back in World Plans on the plan, the unsaved change still there");
			Check(ref ok, !QuestBookPreview.Active, "the preview is over");
			WorldPlanWindow.Close();
			if (ok) Log("PASS: quest book preview"); else Fail("quest book preview");
		}

		[ConsoleCommand(name: "CIQuestBookEditor", docs: "Dev, editor: the quest book in World Plans - New main story... makes the cards (Raft's story off: the first island ahead, the others on the Receiver each after the one before; on: after the Raft island chosen), the NOTEBOOK row on a main story card, Check's notebook warnings (a next-coordinates note that isn't there, colour 3, a long tab title, an island without notes); nothing saved (TEST_CATALOGUE QB4-QB6)")]
		public static void QuestBookEditorCommand() { StartTest(QuestBookEditorRoutine()); }

		static IEnumerator QuestBookEditorRoutine()
		{
			if (!DynamicIslands.InEditor()) { Fail("quest book editor: in the editor"); yield break; }
			bool ok = true;
			List<string> islands = ChoiceWindow.Islands().Select(c => c.Value).ToList();
			if (islands.Count < 3) { Fail("quest book editor: needs three saved islands"); yield break; }
			WorldPlanWindow.RecipeNew("CI quest book editor");
			yield return null;
			// The helper, Raft's story off
			MainStoryHelper.Open();
			yield return null;
			Check(ref ok, MainStoryHelper.IsOpen, "New main story... opens the helper");
			MainStoryHelper.RaftStory = false;
			MainStoryHelper.Entries.Clear();
			for (int k = 0; k < 3; k++) MainStoryHelper.Entries.Add(new MainStoryHelper.Entry { Island = islands[k], Colour = k == 1 ? 5 : 0, Next = k == 2 ? "visit" : "" });
			string why = MainStoryHelper.Finish();
			yield return null;
			WorldPlan p = WorldPlanWindow.Plan;
			List<IntroRule> made = p != null ? p.Rules.Where(r => r.MainStory).ToList() : new List<IntroRule>();
			Check(ref ok, why == null && !MainStoryHelper.IsOpen && made.Count == 3, "Done made three main story cards" + (why != null ? ": " + why : ""));
			if (made.Count == 3)
			{
				Check(ref ok, !p.RaftStory && made[0].StoryPlace == "first" && made[0].Where == "receiver" && made[1].StoryPlace == "after:" + made[0].Id && made[1].Where == "receiver" && made[2].StoryPlace == "after:" + made[1].Id,
					"Raft's story off: every island on the Receiver (main story = coordinates), each after the one before (" + string.Join(" ; ", made.Select(r => r.Id + " " + r.StoryPlace + " " + r.Where).ToArray()) + ")");
				Check(ref ok, made[1].TabColour == 5 && made[2].StoryDone == "visit" && made.All(r => r.What == "island"), "their colours and next-coordinates choices kept");
			}
			Button nb = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.name == "Drop_TabColour");
			Check(ref ok, nb != null, "a main story card has the NOTEBOOK row");
			// Raft's story on: after the Raft island chosen
			WorldPlanWindow.RecipeNew("CI quest book editor");
			yield return null;
			MainStoryHelper.Open();
			MainStoryHelper.RaftStory = true;
			MainStoryHelper.Entries.Clear();
			MainStoryHelper.Entries.Add(new MainStoryHelper.Entry { Island = islands[0], After = "Vasagatan" });
			MainStoryHelper.Entries.Add(new MainStoryHelper.Entry { Island = islands[1] });
			MainStoryHelper.Finish();
			yield return null;
			p = WorldPlanWindow.Plan;
			made = p.Rules.Where(r => r.MainStory).ToList();
			Check(ref ok, p.RaftStory && made.Count == 2 && made[0].StoryPlace == "after:Vasagatan" && made[1].StoryPlace == "after:" + made[0].Id && made.All(r => r.Where == "receiver"),
				"Raft's story on: after Vasagatan, the next after it, both on the Receiver (" + string.Join(" ; ", made.Select(r => r.StoryPlace).ToArray()) + ")");
			// Check's notebook warnings
			if (made.Count == 2)
			{
				made[0].StoryDone = "note:99999";
				made[0].TabColour = 3;
				made[1].TabTitle = "A very long tab title for a tab";
			}
			List<PlanChecker.Finding> f = PlanChecker.Check(p, false, false);
			Check(ref ok, f.Any(x => x.Level == PlanChecker.Level.Problem && x.Text.Contains("note #99999")), "Check: a next-coordinates note that isn't on the island is a problem");
			Check(ref ok, f.Any(x => x.Text.Contains("tab colour 3")), "Check: colour 3 isn't one of Raft's");
			Check(ref ok, f.Any(x => x.Text.Contains("tab title") && x.Text.Contains("long")), "Check: a long tab title");
			if (made.Count == 2)
			{
				made[1].Where = "ahead";
				f = PlanChecker.Check(p, false, false);
				Check(ref ok, f.Any(x => x.Level == PlanChecker.Level.Problem && x.Text.Contains("isn't on the Receiver")), "Check: a main story island without coordinates (not on the Receiver) is a problem");
				made[1].Where = "receiver";
			}
			string bare = islands.FirstOrDefault(n => IslandCache.NoteTextsOf(n).Count == 0);
			if (bare != null)
			{
				p.Rules.Add(new IntroRule { Id = "bare", What = "island", WhatArg = bare, StoryPlace = "after:" + made[1].Id, Where = "receiver", Distance = 600f });
				f = PlanChecker.Check(p, false, false);
				Check(ref ok, f.Any(x => x.Level == PlanChecker.Level.Tip && x.Text.Contains("has no notes with a text")), "Check: a main story island without notes ('" + bare + "')");
			}
			else Log("  (every saved island has notes: the no-notes tip isn't checked)");
			WorldPlanWindow.Close();
			// (a new plan is saved at once: the test's own is moved out of the plans, not left among the player's)
			string testPlan = WorldPlan.PathFor("CI quest book editor");
			try
			{
				if (File.Exists(testPlan))
				{
					string away = Path.Combine(Path.GetTempPath(), "ci_old_plans");
					Directory.CreateDirectory(away);
					File.Copy(testPlan, Path.Combine(away, "CI quest book editor " + DateTime.Now.ToString("MMddHHmmss") + ".plan"), true);
					File.Delete(testPlan);
				}
			}
			catch (Exception e) { Log("  (the test plan couldn't be moved: " + e.Message + ")"); }
			if (ok) Log("PASS: quest book editor"); else Fail("quest book editor");
		}

		[ConsoleCommand(name: "CIQuestBookTabs", docs: "Dev, world (host, test world 'CI ...'): Raft's story on with Balboa left out and a plan island after Vasagatan - its tab between Vasagatan's and Caravan Town's, Balboa's tab hidden; then 14 main story islands open: the tabs scroll, every tab in the strip (TEST_CATALOGUE QB3, QB15, QB16)")]
		public static void QuestBookTabsCommand() { StartTest(QuestBookTabsRoutine()); }

		static IEnumerator QuestBookTabsRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("quest book tabs: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			var unlockedBefore = NoteBook.unlockedChunkPointType.ToList();
			var indexesBefore = NoteBook.unlockedNoteBookIndexes.ToList();
			RecieverFrequency[] freqBefore = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.ToArray() : null;
			try
			{
				StoryChain.Reset();
				StoryChain.FromPlan(WorldPlan.Parse("CI tabs", "storyleaveout = Balboa\nrule = detour | type:sandbar | start | receiver:400 | | Detour | after:Vasagatan | visit | My Detour | 4 | \n"));
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_RadioTower);
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_Vasagatan);
				NoteBook.UnlockFrequency(ChunkPointType.Landmark_Balboa); // (Vasagatan's note: here it leads to the detour)
				StoryChain.Tick();
				yield return new WaitForSeconds(1f);
				QuestBook.Refresh();
				string layout = QuestBook.Layout ?? "";
				int vas = layout.IndexOf("Vasagatan["), det = layout.IndexOf("My Detour"), car = layout.IndexOf("Caravan Town[");
				Check(ref ok, vas >= 0 && det > vas && car > det, "the plan's island between Vasagatan and Caravan Town in the book: " + layout);
				Check(ref ok, layout.Contains("(Balboa, not in this story)") && !QuestBook.RaftTabShown(ChunkPointType.Landmark_Balboa), "Balboa left out: its pages at the back, its tab hidden");
				Check(ref ok, QuestBook.Tabs.Count == 1 && QuestBook.Tabs[0].StartsWith("My Detour|#"), "our tab with its frequency: " + string.Join(";", QuestBook.Tabs.ToArray()));
				NoteBookUI ui = LocalNoteBookUI();
				Notebook_ThumbnailShortcut[] tabs = ui != null ? ui.GetComponentsInChildren<Notebook_ThumbnailShortcut>(true) : new Notebook_ThumbnailShortcut[0];
				Func<string, int> sib = t => { Notebook_ThumbnailShortcut x = tabs.FirstOrDefault(y => y.name.Contains(t)); return x != null ? x.transform.GetSiblingIndex() : -1; };
				Check(ref ok, sib("Vasagatan") >= 0 && sib("My Detour") > sib("Vasagatan") && sib("CaravanIsland") > sib("My Detour"), "on the book's edge too: Vasagatan, My Detour, Caravan Town (" + sib("Vasagatan") + ", " + sib("My Detour") + ", " + sib("CaravanIsland") + ")");

				// Many tabs: 14 main story islands, all open
				StoryChain.Reset();
				var text = new System.Text.StringBuilder("story = off\n");
				for (int i = 1; i <= 14; i++) text.Append("rule = s" + i + " | type:sandbar | start | receiver:400 | | Isle " + i + " | " + (i == 1 ? "first" : "after:s" + (i - 1)) + " | visit\n");
				StoryChain.FromPlan(WorldPlan.Parse("CI many tabs", text.ToString()));
				for (int i = 1; i <= 14; i++) { StoryChain.Tick(); if (i < 14) StoryChain.MarkDone("rule:s" + i); }
				StoryChain.Tick();
				yield return new WaitForSeconds(1f);
				QuestBook.Refresh();
				Check(ref ok, QuestBook.Tabs.Count == 14, "14 main story tabs: " + QuestBook.Tabs.Count);
				if (ui != null && !ui.isDisplayed) ui.SetBookActive(true);
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, QuestBook.ScrollTabs(1f), "the tabs scroll (more than fit on the book's edge)");
				yield return new WaitForSeconds(0.3f);
				Shot("questbook_tabs_scroll");
				yield return new WaitForSeconds(0.6f);
				QuestBook.PressTab("Isle 14");
				yield return new WaitForSeconds(0.3f);
				uint at = ui != null ? (uint)Traverse.Create(ui).Field("currentPageIndex").GetValue<uint>() : 999;
				Check(ref ok, at == QuestBook.PageOfTab("Isle 14"), "the last tab pressed: its page (" + at + ")");
				if (ui != null) ui.SetBookActive(false);
			}
			finally
			{
				foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(e => e.Rule == "detour" || System.Text.RegularExpressions.Regex.IsMatch(e.Rule, "^s[0-9]+$")).ToList()) IslandWorldState.RemoveIds(new List<int> { e.Id }, true);
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				NoteBook.unlockedNoteBookIndexes.Clear(); NoteBook.unlockedNoteBookIndexes.AddRange(indexesBefore);
				NoteBook.unlockedChunkPointType.Clear(); NoteBook.unlockedChunkPointType.AddRange(unlockedBefore);
				if (freqBefore != null) RecieverFrequency.AllFrequencies = freqBefore;
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
				QuestBook.Refresh();
			}
			if (ok) Log("PASS: quest book tabs"); else Fail("quest book tabs");
		}

		const string MpPlan = "story = off\nstoryending = The end test.\\nThank you.\n" +
			"rule = a | type:camp | start | receiver:300 | | Camp A | first | visit | Alpha Camp | 9 | Hello\\nworld\n" +
			"rule = b | type:sandbar | start | receiver:400 | | Bar | after:a | visit\n" +
			"rule = c | type:sandbar | start | receiver:400 | | Side\n";

		[ConsoleCommand(name: "CIQuestBookMP", docs: "Dev, world, two players (mpquestbook.ps1): start (host: the quest book test plan's chain in this test world, the first island brought), done <a|b> (host: that island done), note (any player: a note read on the first island)")]
		public static void QuestBookMPCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "";
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ") && Raft_Network.IsHost) { Fail("quest book mp: a test world 'CI ...'"); return; }
			switch (what)
			{
				case "start":
					if (!Raft_Network.IsHost) { Fail("quest book mp start: the host"); return; }
					StoryChain.Reset();
					StoryChain.FromPlan(WorldPlan.Parse("CI quest book", MpPlan));
					StartTest(MpBrought());
					return;
				case "done":
					if (!Raft_Network.IsHost) { Fail("quest book mp done: the host"); return; }
					StoryChain.MarkDone("rule:" + (args.Length > 1 ? args[1] : "a"));
					StoryChain.Tick();
					Log("PASS: quest book mp done " + (args.Length > 1 ? args[1] : "a"));
					return;
				case "note":
					IslandWorldState.Entry a = IslandWorldState.Islands.FirstOrDefault(e => e.Rule == "a");
					if (a == null) { Fail("quest book mp note: the first island isn't in this world"); return; }
					StoryBook.AddPage("note:" + a.HostName + ":5", "Diary", "Dear diary, the camp is cold.", "Alpha");
					Log("PASS: quest book mp note");
					return;
			}
			Fail("CIQuestBookMP start | done <a|b> | note");
		}

		static IEnumerator MpBrought()
		{
			StoryChain.Tick();
			StoryChain.OnTuned(StoryChain.TypeOfRule("a")); // (its coordinates found at the start: the host tunes to them)
			yield return WaitFor(() => IslandWorldState.Islands.Any(e => e.Rule == "a"), 60f);
			if (IslandWorldState.Islands.Any(e => e.Rule == "a")) Log("PASS: quest book mp start"); else Fail("quest book mp start: the first island didn't come");
		}

		[ConsoleCommand(name: "CIQuestBookCheck", docs: "Dev, world (either player): this player's quest book in one line (QBOOK tabs | notes | ending), to compare the players' (mpquestbook.ps1); CIQuestBookCheck <tabs> also checks the number of our tabs")]
		public static void QuestBookCheckCommand(string[] args)
		{
			QuestBook.Refresh();
			string tabs = string.Join(";", QuestBook.Tabs.ToArray());
			bool note = QuestBook.PageTexts.Values.Any(v => v.Any(t => t.Contains("Dear diary")));
			bool end = (QuestBook.Layout ?? "").Contains("The end[");
			Log("QBOOK " + tabs + " | note " + note + " | end " + end + " | " + (QuestBook.Layout ?? "").Split(new[] { " > (" }, StringSplitOptions.None)[0]);
			int want;
			if (args != null && args.Length > 0 && int.TryParse(args[0], out want) && QuestBook.Tabs.Count != want) { Fail("quest book check: " + QuestBook.Tabs.Count + " tabs, " + want + " expected"); return; }
			Log("PASS: quest book check");
		}

		[ConsoleCommand(name: "CIQuestBookState", docs: "Dev, world: the quest book now - its layout, our tabs, Raft's tabs shown, our pages' texts")]
		public static void QuestBookStateCommand()
		{
			QuestBook.Refresh();
			Log("QUESTBOOK layout: " + QuestBook.Layout);
			foreach (string t in QuestBook.Tabs) Log("QUESTBOOK tab: " + t);
			Log("QUESTBOOK items: " + string.Join(", ", QuestBook.Items.ToArray()));
			Log("QUESTBOOK Raft tabs shown: " + string.Join(",", StoryOrder.Chain.Where(QuestBook.RaftTabShown).Select(StoryOrder.Name).ToArray()));
			foreach (KeyValuePair<uint, List<string>> kv in QuestBook.PageTexts.OrderBy(k => k.Key))
				Log("QUESTBOOK page " + kv.Key + ": " + string.Join(" || ", kv.Value.Select(x => x.Replace("\n", "\\n")).ToArray()));
			Log("PASS: quest book state");
		}

		[ConsoleCommand(name: "CIQuestBookOpen", docs: "Dev, world: opens Raft's notebook at one of the quest book's tabs (CIQuestBookOpen <tab title>), or at a page number")]
		public static void QuestBookOpenCommand(string[] args)
		{
			string title = args != null ? string.Join(" ", args) : "";
			NoteBookUI ui = LocalNoteBookUI();
			if (ui == null) { Fail("QuestBookOpen: no notebook"); return; }
			if (!ui.isDisplayed) ui.SetBookActive(true);
			uint page;
			if (uint.TryParse(title, out page)) ui.FlipToPageLocally(page);
			else if (!QuestBook.PressTab(title)) { Fail("QuestBookOpen: no tab '" + title + "'"); return; }
			Log("PASS: notebook open at " + Traverse.Create(ui).Field("currentPageIndex").GetValue());
		}

		static void DropComponents(GameObject go, params string[] typeNames)
		{
			foreach (Component c in go.GetComponentsInChildren<Component>(true))
				if (c != null && typeNames.Contains(c.GetType().Name)) UnityEngine.Object.DestroyImmediate(c);
		}

		[ConsoleCommand(name: "CINoteBookClone", docs: "Dev, world (test worlds only): quest book spike - adds <n> test islands to Raft's notebook (a tab each, cloned from Vasagatan's, and two pages with a note), unlocks them and opens the book at the first; CINoteBookClone <n> scroll also puts the tabs into a scrolling strip")]
		public static void NoteBookCloneCommand(string[] args)
		{
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("NoteBookClone: only in a test world (named CI ...)"); return; }
			int count = 1;
			if (args != null && args.Length > 0) int.TryParse(args[0], out count);
			bool scroll = args != null && args.Contains("scroll");
			NoteBookUI ui = LocalNoteBookUI();
			NoteBook nb = UnityEngine.Object.FindObjectOfType<NoteBook>();
			if (ui == null || nb == null) { Fail("NoteBookClone: no notebook"); return; }
			Traverse tr = Traverse.Create(ui);
			var pages = (tr.Field("pageObjs").GetValue<NoteBookPage[]>() ?? new NoteBookPage[0]).ToList();
			var notes = (tr.Field("notes").GetValue<NoteBookNote[]>() ?? new NoteBookNote[0]).ToList();
			NoteBookPage tplL = pages.FirstOrDefault(p => p.pageIndex == 6), tplR = pages.FirstOrDefault(p => p.pageIndex == 7);
			Notebook_ThumbnailShortcut tplTab = ui.GetComponentsInChildren<Notebook_ThumbnailShortcut>(true).FirstOrDefault(t => t.name.Contains("Vasagatan"));
			if (tplL == null || tplR == null || tplTab == null) { Fail("NoteBookClone: Vasagatan's pages or tab not found"); return; }
			uint next = pages.Max(p => p.pageIndex) + 1;
			if (next % 2 == 1) next++; // a spread starts on an even (left) page
			int firstPage = (int)next;
			Sprite[] tabSprites = Resources.FindObjectsOfTypeAll<Sprite>().Where(s => s.name.StartsWith("NoteBook_Thumbnail_")).OrderBy(s => s.name).ToArray();
			for (int i = 0; i < count; i++)
			{
				int idx = 10000 + i * 100;
				string title = "Test Island " + (i + 1);
				NoteBookPage left = null;
				foreach (NoteBookPage tpl in new[] { tplL, tplR })
				{
					NoteBookPage pg = UnityEngine.Object.Instantiate(tpl, tpl.transform.parent);
					pg.pageIndex = next;
					pg.name = (tpl == tplL ? "NoteBookPageL_" : "NoteBookPageR_") + title + "_PageIndex " + next + "_CI";
					Traverse.Create(pg).Field("pageTitleString").SetValue(title);
					Transform tt = pg.transform.Find("Page Title");
					if (tt != null && tt.GetComponent<TMP_Text>() != null) tt.GetComponent<TMP_Text>().text = title;
					Transform nt = pg.transform.Find("Page Title/Page number");
					if (nt != null && nt.GetComponent<TMP_Text>() != null) nt.GetComponent<TMP_Text>().text = (next + 1).ToString();
					// one note kept (the first), the rest removed
					NoteBookNote[] pn = pg.GetComponentsInChildren<NoteBookNote>(true);
					for (int k = 1; k < pn.Length; k++) UnityEngine.Object.DestroyImmediate(pn[k].gameObject);
					NoteBookNote note = pn[0];
					note.noteIndex = idx + 1 + (tpl == tplL ? 0 : 1);
					note.landmarkType = ChunkPointType.None;
					note.isUnlocked = false;
					note.name = "NoteBookNote_Index" + note.noteIndex + "_CI";
					Traverse.Create(note).Field("voiceActor").SetValue(null);
					Traverse.Create(note).Field("voiceData").SetValue(null);
					DropComponents(note.gameObject, "Localize");
					Transform play = note.transform.Find("Play&Stop Button");
					if (play != null) play.gameObject.SetActive(false);
					TMP_Text[] tx = note.GetComponentsInChildren<TMP_Text>(true);
					if (tx.Length > 0) tx[0].text = tpl == tplL
						? "This is a page of " + title + ", added by Custom Islands while the game runs. If you can read this in Raft's own book, with Raft's paper and handwriting, the quest book can be done.\n\n-The mod"
						: "A second note on the right page. Notes can be as long as Raft's (about 350 characters).";
					for (int k = 1; k < tx.Length; k++) tx[k].text = "";
					Traverse.Create(pg).Field("notes").SetValue(new[] { note });
					pages.Add(pg);
					notes.Add(note);
					if (tpl == tplL) left = pg;
					next++;
				}
				Notebook_ThumbnailShortcut tab = UnityEngine.Object.Instantiate(tplTab, tplTab.transform.parent);
				tab.name = "ThumbNailButton_" + title + "_CI";
				tab.targetPage = left;
				NoteBookNote tn = tab.GetComponent<NoteBookNote>();
				tn.noteIndex = idx;
				tn.isFrequencyThumbnail = false;
				tn.thumbNailLandmarkType = ChunkPointType.None;
				tn.isUnlocked = false;
				DropComponents(tab.gameObject, "FrequencyTextMeshProUI", "Localize");
				foreach (TMP_Text t in tab.GetComponentsInChildren<TMP_Text>(true))
				{
					if (t.name == "DestinationName") t.text = title;
					if (t.name == "DestinationFrequency") { t.text = "#" + (1000 + i * 37).ToString(); t.gameObject.SetActive(true); }
				}
				Image img = tab.GetComponent<Image>();
				if (img != null && tabSprites.Length > 0) img.sprite = tabSprites[i % tabSprites.Length];
				notes.Add(tn);
			}
			tr.Field("pageObjs").SetValue(pages.ToArray());
			tr.Field("notes").SetValue(notes.ToArray());
			if (scroll) MakeTabsScroll(tplTab.transform.parent as RectTransform);
			int unlocked = 0;
			foreach (NoteBookNote n in notes) if (n.noteIndex >= 10000) { nb.UnlockSpecificNoteWithUniqueNoteIndex(n.noteIndex, true, false); unlocked++; }
			tr.Method("UpdatePages").GetValue();
			if (!ui.isDisplayed) ui.SetBookActive(true);
			bool flipped = ui.FlipToPageLocally((uint)firstPage);
			Log("PASS: " + count + " test islands added (pages from " + firstPage + ", " + unlocked + " notes unlocked, flip " + flipped + ", now at " + tr.Field("currentPageIndex").GetValue() + ", highest " + tr.Field("highestUnlockedPageIndex").GetValue() + ", tab sprites " + tabSprites.Length + (scroll ? ", tabs scroll" : "") + ")");
		}

		/// <summary>The tab strip (Raft's ThumbNails, a VerticalLayoutGroup as tall as the book) inside a scrolling viewport.</summary>
		static void MakeTabsScroll(RectTransform strip)
		{
			if (strip == null || strip.parent.name == "CI_TabViewport") return;
			var vp = new GameObject("CI_TabViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
			RectTransform v = vp.GetComponent<RectTransform>();
			v.SetParent(strip.parent, false);
			v.SetSiblingIndex(strip.GetSiblingIndex());
			v.anchorMin = strip.anchorMin; v.anchorMax = strip.anchorMax; v.pivot = strip.pivot;
			v.anchoredPosition = strip.anchoredPosition; v.sizeDelta = new Vector2(strip.sizeDelta.x + 30f, strip.sizeDelta.y);
			vp.GetComponent<Image>().color = new Color(1, 1, 1, 0.003f); // catches the wheel between tabs
			strip.SetParent(v, false);
			strip.anchorMin = new Vector2(0.5f, 1f); strip.anchorMax = new Vector2(0.5f, 1f); strip.pivot = new Vector2(0.5f, 1f);
			strip.anchoredPosition = new Vector2(15f, 0f);
			var fit = strip.gameObject.AddComponent<ContentSizeFitter>();
			fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			ScrollRect sr = vp.GetComponent<ScrollRect>();
			sr.content = strip; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 20f;
			sr.viewport = v;
			LayoutRebuilder.ForceRebuildLayoutImmediate(strip);
		}

		[ConsoleCommand(name: "CINoteBookScrollTo", docs: "Dev, world: scrolls the quest book spike's tab strip (0 = top, 1 = bottom)")]
		public static void NoteBookScrollToCommand(string[] args)
		{
			ScrollRect sr = UnityEngine.Object.FindObjectsOfType<ScrollRect>().FirstOrDefault(s => s.name == "CI_TabViewport");
			if (sr == null) { Fail("NoteBookScrollTo: no tab strip that scrolls"); return; }
			float f = 0f;
			if (args != null && args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f);
			sr.verticalNormalizedPosition = 1f - f;
			Log("PASS: tabs scrolled to " + f + " (content " + sr.content.rect.height.ToString("0") + " in " + ((RectTransform)sr.transform).rect.height.ToString("0") + ")");
		}

		[ConsoleCommand(name: "CINoteBookTab", docs: "Dev, world: presses a notebook tab by its title (as a player's click does)")]
		public static void NoteBookTabCommand(string[] args)
		{
			string title = args != null ? string.Join(" ", args) : "";
			NoteBookUI ui = LocalNoteBookUI();
			Notebook_ThumbnailShortcut tab = ui != null ? ui.GetComponentsInChildren<Notebook_ThumbnailShortcut>(true).FirstOrDefault(t => t.GetComponentsInChildren<TMP_Text>(true).Any(x => x.name == "DestinationName" && x.text == title)) : null;
			if (tab == null) { Fail("NoteBookTab: no tab '" + title + "'"); return; }
			tab.OnThumbnailButtonPress();
			Log("PASS: tab '" + title + "' pressed, at page " + Traverse.Create(ui).Field("currentPageIndex").GetValue());
		}
	}
}
