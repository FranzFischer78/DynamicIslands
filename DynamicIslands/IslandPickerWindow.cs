using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Islands while sailing" in the New Game box's World settings window: a button ("CHOOSE ISLANDS...  12 of 14") that
	/// opens a list of everything that can turn up by chance while sailing - the saved islands, the map types and brand-new
	/// generated islands of spawnpool.txt - A to Z, each a compact row: a tick box, a picture (IslandThumbs: a map of the
	/// island, its library picture, or one of Raft's islands), a QUEST / no quest badge, two lines of its description
	/// (IslandSummaries) and the small print. Untick what the world being created shouldn't have (WorldIslands.Chosen); a
	/// search field narrows the list, "Tick all" / "Untick all" do every row and "Tick shown" / "Untick shown" the rows
	/// shown. The choice is the world's when Create is pressed, and the next world starts from it.
	/// </summary>
	public static class IslandPickerWindow
	{
		public const string WindowName = "CustomIslands_IslandPicker", EntryName = "CustomIslands_Islands", RowPrefix = "Pick_";

		static RectTransform window, list;
		static Button entryButton;
		static Text entryText, countText, planNote;
		static InputField search;
		static readonly Dictionary<string, Button> rows = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, RawImage> thumbs = new Dictionary<string, RawImage>(StringComparer.OrdinalIgnoreCase);
		static readonly Dictionary<string, Text> descs = new Dictionary<string, Text>(StringComparer.OrdinalIgnoreCase), badges = new Dictionary<string, Text>(StringComparer.OrdinalIgnoreCase), metas = new Dictionary<string, Text>(StringComparer.OrdinalIgnoreCase);
		static readonly HashSet<string> libraryPictures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		static Dictionary<string, LibraryInstalled> fromLibrary = new Dictionary<string, LibraryInstalled>();
		static Button allButton;
		static int loadRun;
		static bool indexAsked;

		public static bool IsOpen { get { return window != null && window.gameObject.activeSelf; } }
		public static RectTransform Window { get { return window; } }
		public static Button EntryButton { get { return entryButton; } }
		public static InputField Search { get { return search; } }
		public static Text Count { get { return countText; } }
		/// <summary>The row of a pool entry (tests click it as a player would), or null.</summary>
		public static Button Row(string entry) { Button b; return rows.TryGetValue(entry, out b) ? b : null; }
		public static Button AllButton { get { return allButton; } }
		/// <summary>True once the rows' descriptions and quest badges are filled in (the summaries read).</summary>
		public static bool Loaded { get; private set; }
		/// <summary>What a row shows (tests): its badge ("QUEST", "no quest", "NEW EACH TIME"), description, picture, small print.</summary>
		public static string BadgeOf(string entry) { Text t; return badges.TryGetValue(entry, out t) && t != null ? t.text : null; }
		public static string DescriptionOf(string entry) { Text t; return descs.TryGetValue(entry, out t) && t != null ? t.text : null; }
		public static string SmallPrintOf(string entry) { Text t; return metas.TryGetValue(entry, out t) && t != null ? t.text : null; }
		public static Texture PictureOf(string entry) { RawImage i; return thumbs.TryGetValue(entry, out i) && i != null && i.enabled ? i.texture : null; }
		public static bool HasLibraryPicture(string entry) { return libraryPictures.Contains(entry); }
		/// <summary>The entries whose rows are shown (not hidden by the search).</summary>
		public static List<string> Shown() { return rows.Where(kv => kv.Value != null && kv.Value.gameObject.activeSelf).Select(kv => kv.Key).ToList(); }

		/// <summary>The group in the World settings window (parent), and the list's window over the whole menu (screen).</summary>
		internal static void BuildEntry(Transform parent, Transform screen)
		{
			RectTransform group = UIKit.Group(parent, "Islands while sailing", EntryName);
			UIKit.Hint(group.gameObject, "Which of your islands - and which kinds of new ones - turn up by chance while you sail in this world. Untick the ones it shouldn't have.");
			entryButton = UIKit.Button(group, "", Open, "Choose the islands that turn up by chance while sailing in this world", -1, 28f, 14);
			entryText = UIKit.Label(group, "", 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Detail");
			entryText.horizontalOverflow = HorizontalWrapMode.Wrap;
			entryText.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(entryText.gameObject, -1, 28);
			// How often: one random custom island after every <min>-<max> of Raft's own islands (default 3-6; min 2-20, max 4-50)
			const string gapHint = "How often an island from this list turns up: one after every so many of Raft's own islands you meet - a number between these two each time. 3-6 by default; the lowest 2-4, the highest 20-50. However many islands are ticked, they never crowd the sea.";
			RectTransform gapRow = UIKit.Row(group, 26f, 4f, "Gap");
			Text lead = UIKit.Label(gapRow, "One after every", 12, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "GapLead");
			UIKit.Size(lead.gameObject, 104);
			UIKit.Button(gapRow, "-", () => StepGap(-1, 0), gapHint, 24, 24f, 14).name = "GapMinLess";
			gapMinText = UIKit.Label(gapRow, "", 13, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "GapMin");
			UIKit.Size(gapMinText.gameObject, 26);
			UIKit.Button(gapRow, "+", () => StepGap(1, 0), gapHint, 24, 24f, 14).name = "GapMinMore";
			Text to = UIKit.Label(gapRow, "to", 12, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, "GapTo");
			UIKit.Size(to.gameObject, 20);
			UIKit.Button(gapRow, "-", () => StepGap(0, -1), gapHint, 24, 24f, 14).name = "GapMaxLess";
			gapMaxText = UIKit.Label(gapRow, "", 13, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "GapMax");
			UIKit.Size(gapMaxText.gameObject, 26);
			UIKit.Button(gapRow, "+", () => StepGap(0, 1), gapHint, 24, 24f, 14).name = "GapMaxMore";
			UIKit.Label(gapRow, "of Raft's islands", 12, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "GapTail");
			UIKit.Hint(gapRow.gameObject, gapHint);
			Build(screen);
			ShowEntry();
		}

		static Text gapMinText, gapMaxText;

		/// <summary>The World settings window's - and + by the span (tests click them as a player would).</summary>
		internal static void StepGap(int min, int max)
		{
			int[] g = WorldIslands.ChosenGap;
			int lo = g[0] + min, hi = g[1];
			// (the top goes in ones up to 10, then in fives: 10, 15, 20 ... 50)
			if (max > 0) hi = hi < 10 ? hi + 1 : (hi / 5 + 1) * 5;
			else if (max < 0) hi = hi <= 10 ? hi - 1 : ((hi - 1) / 5) * 5;
			if (lo > hi) hi = lo;
			WorldIslands.SetChosenGap(lo, hi);
			ShowEntry();
		}

		// The rows' layout (a row is a flat button: tick box, picture, name + badge, description, small print)
		const float RowH = 64f, ThumbW = 76f, ThumbH = 56f, TextLeft = 122f;

		static void Build(Transform screen)
		{
			if (window != null) UnityEngine.Object.Destroy(window.gameObject);
			window = UIKit.Rect(WindowName, screen);
			UIKit.Stretch(window);
			window.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f); // (what is behind it dimmed, its clicks taken)
			RectTransform panel = UIKit.Panel(window, "Panel", new RectOffset(16, 16, 10, 12), 6f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 0f));
			RectTransform head = UIKit.Row(panel, 30f, 6f, "Head");
			Text title = UIKit.Label(head, "ISLANDS WHILE SAILING", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(title);
			UIKit.Label(head, "for the world you create", 12, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Italic);
			Text intro = UIKit.Label(panel, "These turn up by chance while you sail. Untick the ones this world shouldn't have; islands you make or download later join in. Point at a row for its whole description. (In a world, the host can change it with the console command WorldIslands.)",
				12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Intro");
			intro.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(intro.gameObject, -1, 32);
			planNote = UIKit.Label(panel, "", 12, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Italic, "PlanNote");
			planNote.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(planNote.gameObject, -1, 30);

			RectTransform tools = UIKit.Row(panel, 30f, 6f, "Tools");
			search = UIKit.Field(tools, "Search...", "", 30f, "Show only the islands whose name holds this");
			search.onValueChanged.AddListener(Filter);
			allButton = UIKit.Button(tools, "Tick all", ToggleAll, "Tick every island in the list - or, when all are ticked, untick them all (whatever the search shows)", 100, 30f, 13);
			allButton.name = "Button_All";
			UIKit.Button(tools, "Tick shown", () => SetShown(true), "Tick every island shown (all of them, or the ones the search found)", 104, 30f, 13);
			UIKit.Button(tools, "Untick shown", () => SetShown(false), "Untick every island shown (all of them, or the ones the search found)", 112, 30f, 13);

			RectTransform listBox = UIKit.Rect("ListBox", panel);
			UIKit.Size(listBox.gameObject, -1, 372);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect scroll;
			list = UIKit.ScrollList(listBox, out scroll, 3f);
			UIKit.Stretch((RectTransform)scroll.transform, 4, 4, 4, 4);

			countText = UIKit.Label(panel, "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Count");
			UIKit.Size(countText.gameObject, -1, 20);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Label(buttons, "", 12);
			Button done = UIKit.Button(buttons, "Done", Close, "Keep this choice for the world you create", 150, 34f, 15);
			UIKit.Primary(done);
			EscKey.Add(window.gameObject, Close); // (Esc = Done)
			window.gameObject.SetActive(false);
		}

		public static void Open()
		{
			if (window == null) return;
			window.gameObject.SetActive(true);
			window.SetAsLastSibling();
			if (search != null) search.text = "";
			Refresh();
		}

		public static void Close()
		{
			if (window != null) window.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
			ShowEntry();
		}

		/// <summary>One row per pool entry: first the kinds of new islands, then the saved islands - each group A to Z.</summary>
		static void Refresh()
		{
			foreach (Transform child in list) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
			rows.Clear(); thumbs.Clear(); descs.Clear(); badges.Clear(); metas.Clear();
			List<string> all = WorldIslands.Candidates();
			List<string> kinds = all.Where(e => !WorldIslands.IsIsland(e)).OrderBy(WorldIslands.Label, StringComparer.OrdinalIgnoreCase).ToList();
			List<string> islands = all.Where(WorldIslands.IsIsland).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();
			fromLibrary = LibraryIslands();
			if (all.Count == 0) Header("Nothing can turn up while sailing: every island is left out (Defaults... in World settings).");
			if (kinds.Count > 0) { Header("NEW EACH TIME"); foreach (string e in kinds) AddRow(e); }
			if (islands.Count > 0) { Header("YOUR ISLANDS (" + islands.Count + ")"); foreach (string e in islands) AddRow(e); }
			ShowEntry();
			int run = ++loadRun;
			Loaded = false;
			if (DynamicIslands.instance != null)
			{
				DynamicIslands.instance.StartCoroutine(LoadTexts(run, islands));
				DynamicIslands.instance.StartCoroutine(LoadPictures(run, kinds.Concat(islands).ToList()));
			}
		}

		static void Header(string text)
		{
			Text h = UIKit.Label(list, text, 12, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Header");
			UIKit.Size(h.gameObject, -1, 22);
		}

		static void AddRow(string entry)
		{
			bool island = WorldIslands.IsIsland(entry);
			Button b = UIKit.Button(list, "", () => Flip(entry), WorldIslands.Hint(entry), -1, RowH, 13);
			b.name = RowPrefix + entry;
			UIKit.Flat(b);
			Text label = UIKit.LabelOf(b);
			label.text = "";
			// The tick box: a frame with a golden square in it while the island takes part
			RectTransform box = UIKit.Rect("Box", b.transform);
			box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
			box.pivot = new Vector2(0f, 0.5f);
			box.anchoredPosition = new Vector2(10f, 0f);
			box.sizeDelta = new Vector2(18f, 18f);
			UIKit.Background(box.gameObject, UIKit.FieldBg, 3);
			UIKit.Border(box, UIKit.Tan, 3, 1.5f);
			RectTransform tick = UIKit.Rect("Tick", box);
			UIKit.Stretch(tick, 4, 4, 4, 4);
			UIKit.Background(tick.gameObject, UIKit.Accent, 2);
			// The picture: a map of the island, its library picture, or (map types, generated) one of Raft's islands
			RectTransform frame = UIKit.Rect("Thumb", b.transform);
			frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0f, 0.5f);
			frame.anchoredPosition = new Vector2(36f, 0f);
			frame.sizeDelta = new Vector2(ThumbW, ThumbH);
			UIKit.Background(frame.gameObject, new Color(0.16f, 0.25f, 0.30f, 1f), 4).raycastTarget = false;
			RawImage pic = UIKit.Picture(frame, null, ThumbW, ThumbH, "Picture");
			UIKit.Stretch(pic.rectTransform, 2, 2, 2, 2);
			pic.enabled = false;
			thumbs[entry] = pic;
			// Name, badge, description, small print
			Text nm = UIKit.Label(b.transform, WorldIslands.Label(entry), 14, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold, "Name");
			UIKit.Stretch(nm.rectTransform, TextLeft, 120, 4, RowH - 22);
			UIKit.Fit(nm, 10);
			RectTransform badge = UIKit.Rect("Badge", b.transform);
			badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1f, 1f);
			badge.anchoredPosition = new Vector2(entry == CustomIslandSpawner.GeneratedEntry ? -32f : -8f, -5f);
			badge.sizeDelta = new Vector2(104f, 16f);
			Image badgeBg = UIKit.Background(badge.gameObject, UIKit.Accent, 3);
			badgeBg.raycastTarget = false;
			Text badgeText = UIKit.Label(badge, island ? "" : "NEW EACH TIME", 10, UIKit.AccentText, TextAnchor.MiddleCenter, FontStyle.Bold, "Text");
			UIKit.Stretch(badgeText.rectTransform);
			badges[entry] = badgeText;
			if (island) Badge(entry, null); else { badgeBg.color = UIKit.Tan; }
			Text desc = UIKit.Label(b.transform, island ? "..." : Describe(entry), 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Description");
			UIKit.Stretch(desc.rectTransform, TextLeft, 12, 23, 16);
			desc.horizontalOverflow = HorizontalWrapMode.Wrap;
			desc.verticalOverflow = VerticalWrapMode.Truncate;
			descs[entry] = desc;
			Text meta = UIKit.Label(b.transform, island ? Small(entry, null) : WorldIslands.Detail(entry), 10, UIKit.TextMuted, TextAnchor.LowerLeft, FontStyle.Italic, "Meta");
			UIKit.Stretch(meta.rectTransform, TextLeft, 12, RowH - 17, 2);
			metas[entry] = meta;
			if (entry == CustomIslandSpawner.GeneratedEntry)
			{
				// (CB16: the user didn't see what this row does - a ? at its right end says it all; its click doesn't flip the row)
				Button help = UIKit.Help(b.transform, WorldIslands.GeneratedHelp);
				help.name = "Help_Generated";
				var hr = (RectTransform)help.transform;
				hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 1f);
				hr.anchoredPosition = new Vector2(-8f, -4f);
				hr.sizeDelta = new Vector2(18f, 18f);
			}
			rows[entry] = b;
			Look(entry);
		}

		/// <summary>A map type's or the generated row's description.</summary>
		static string Describe(string entry)
		{
			if (entry == CustomIslandSpawner.GeneratedEntry) return "The mod makes up a new random island now and then - random size and style - saved as gen-... in this world.";
			MapType t = MapTypes.Get(entry.Substring(CustomIslandSpawner.TypePrefix.Length));
			return t != null && !string.IsNullOrEmpty(t.Description) ? t.Description : "A new island of this map type each time.";
		}

		/// <summary>The small print under an island: library or your own, its difficulty (once its summary is read: TODO 4d), size, date.</summary>
		static string Small(string entry, IslandSummaries.Summary s)
		{
			string who = fromLibrary.ContainsKey(entry) ? "library island" : "your island";
			if (s != null && !string.IsNullOrEmpty(s.Author)) who += " by " + s.Author;
			if (s != null) who += "  -  " + IslandDifficulty.Label(s.Monsters);
			try
			{
				var info = new System.IO.FileInfo(IslandSpawner.PathFor(entry));
				if (info.Exists) return who + "  -  " + Math.Max(1, info.Length / 1024) + " KB, " + info.LastWriteTime.ToString("yyyy-MM-dd");
			}
			catch { }
			return who;
		}

		/// <summary>The badge of an island: QUEST (golden) or no quest (plain); "..." while its summary is read.</summary>
		static void Badge(string entry, IslandSummaries.Summary s)
		{
			Text t;
			if (!badges.TryGetValue(entry, out t) || t == null) return;
			Image bg = t.transform.parent.GetComponent<Image>();
			if (s == null) { t.text = "..."; t.color = UIKit.TextMuted; if (bg != null) bg.color = Color.clear; return; }
			t.text = s.HasQuest ? "QUEST" : "no quest";
			t.color = s.HasQuest ? UIKit.AccentText : UIKit.TextMuted;
			t.fontStyle = s.HasQuest ? FontStyle.Bold : FontStyle.Italic;
			if (bg != null) bg.color = s.HasQuest ? UIKit.Accent : Color.clear;
			((RectTransform)t.transform.parent).sizeDelta = new Vector2(s.HasQuest ? 58f : 70f, 16f);
		}

		/// <summary>The islands' descriptions and quests from the summaries (summarycache.txt; changed files read on a worker thread).</summary>
		static IEnumerator LoadTexts(int run, List<string> islands)
		{
			IslandSummaries.Job job = IslandSummaries.Prepare(islands);
			while (!job.Finished) yield return null;
			// (a look started for another list was under way: ours, read now)
			if (islands.Any(n => !job.names.Contains(n, StringComparer.OrdinalIgnoreCase)))
			{
				job = IslandSummaries.Prepare(islands);
				while (!job.Finished) yield return null;
			}
			if (run != loadRun) yield break;
			foreach (string e in islands)
			{
				IslandSummaries.Summary s = IslandSummaries.Of(e);
				Text d;
				if (descs.TryGetValue(e, out d) && d != null)
				{
					string text = s == null ? "" : s.Description.Trim().Length > 0 ? s.Description.Trim() : s.Title.Trim().Length > 0 && s.Title.Trim() != e ? s.Title.Trim() : "";
					d.text = text.Length > 0 ? text.Replace("\n", " ") : "(no description)";
					d.color = text.Length > 0 ? UIKit.TextColor : UIKit.TextMuted;
					Button b = Row(e);
					if (b != null) UIKit.Hint(b.gameObject, e + (s != null && s.HasQuest ? "  (has a quest)" : "") + (text.Length > 0 ? ":\n" + text : ""));
				}
				Badge(e, s ?? new IslandSummaries.Summary());
				Text m;
				if (metas.TryGetValue(e, out m) && m != null) m.text = Small(e, s);
			}
			Loaded = true;
		}

		/// <summary>The rows' pictures, top first: Raft's for map types, the library picture or a map for islands (made once, then cached).</summary>
		static IEnumerator LoadPictures(int run, List<string> entries)
		{
			int quick = 0;
			foreach (string e in entries)
			{
				if (run != loadRun || !IsOpen) yield break;
				if (!WorldIslands.IsIsland(e)) { SetThumb(e, IslandThumbs.Stock(e)); continue; }
				Texture2D t = IslandThumbs.Cached(e);
				if (t != null) { SetThumb(e, t); if (++quick % 12 == 0) yield return null; continue; }
				yield return IslandThumbs.Make(e, tex => { if (run == loadRun) SetThumb(e, tex); });
			}
			if (run != loadRun) yield break;
			// Library islands: their own picture over the map, once the library's list is known (read once if it isn't)
			if (fromLibrary.Count == 0) yield break;
			if (LibraryClient.Entries == null && LibraryClient.Online && !indexAsked)
			{
				indexAsked = true;
				yield return LibraryClient.LoadIndex(false, err => { });
			}
			if (LibraryClient.Entries == null) yield break;
			foreach (string e in entries)
			{
				if (run != loadRun || !IsOpen) yield break;
				LibraryInstalled inst;
				if (!fromLibrary.TryGetValue(e, out inst)) continue;
				LibraryEntry le = LibraryClient.Entries.FirstOrDefault(x => x.Info.id == inst.id);
				if (le == null) continue;
				string name = !string.IsNullOrEmpty(le.Info.icon) ? le.Info.icon : le.Info.pictures != null && le.Info.pictures.Length > 0 ? le.Info.pictures[0] : null;
				if (string.IsNullOrEmpty(name)) continue;
				yield return LibraryClient.Picture(le, name, tex => { if (tex != null && run == loadRun) { SetThumb(e, tex); libraryPictures.Add(e); } });
			}
		}

		static void SetThumb(string entry, Texture t)
		{
			RawImage img;
			if (t == null || !thumbs.TryGetValue(entry, out img) || img == null) return;
			IslandThumbs.Cover(img, t, ThumbW - 4f, ThumbH - 4f);
			img.enabled = true;
		}

		/// <summary>Installed library islands by island name.</summary>
		static Dictionary<string, LibraryInstalled> LibraryIslands()
		{
			var d = new Dictionary<string, LibraryInstalled>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (LibraryInstalled i in LibraryPack.Installed())
					foreach (LibraryInstalledFile f in i.files)
						if (!f.shared && f.kind == LibraryPack.KindIsland && !d.ContainsKey(f.name)) d[f.name] = i;
			}
			catch { }
			return d;
		}

		/// <summary>Whether an entry's row shows it taking part (its tick box ticked).</summary>
		public static bool IsTicked(string entry)
		{
			Button b = Row(entry);
			Transform tick = b != null ? b.transform.Find("Box/Tick") : null;
			return tick != null && tick.gameObject.activeSelf;
		}

		static void Look(string entry)
		{
			Button b = Row(entry);
			if (b == null) return;
			bool on = !WorldIslands.Chosen.Contains(entry);
			Transform tick = b.transform.Find("Box/Tick");
			if (tick != null) tick.gameObject.SetActive(on);
			Transform name = b.transform.Find("Name");
			if (name != null) name.GetComponent<Text>().color = on ? UIKit.TextColor : UIKit.TextMuted;
			RawImage pic;
			if (thumbs.TryGetValue(entry, out pic) && pic != null) pic.color = on ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
		}

		public static void Flip(string entry)
		{
			if (!WorldIslands.Chosen.Remove(entry)) WorldIslands.Chosen.Add(entry);
			Look(entry);
			ShowEntry();
		}

		static void SetShown(bool on)
		{
			foreach (string entry in Shown())
			{
				if (on) WorldIslands.Chosen.Remove(entry); else WorldIslands.Chosen.Add(entry);
				Look(entry);
			}
			ShowEntry();
		}

		/// <summary>"Tick all" / "Untick all": every entry of the list, whatever the search shows (tests click it as a player would).</summary>
		public static void ToggleAll()
		{
			List<string> all = WorldIslands.Candidates();
			bool allOn = all.All(e => !WorldIslands.Chosen.Contains(e));
			foreach (string e in all) { if (allOn) WorldIslands.Chosen.Add(e); else WorldIslands.Chosen.Remove(e); Look(e); }
			ShowEntry();
		}

		static void Filter(string text)
		{
			string t = (text ?? "").Trim();
			foreach (var kv in rows)
				if (kv.Value != null) kv.Value.gameObject.SetActive(t.Length == 0 || WorldIslands.Label(kv.Key).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
			// A section heading shows only while a row under it does
			if (list == null || rows.Count == 0) return;
			GameObject heading = null;
			bool any = false;
			foreach (Transform child in list)
			{
				if (child.name == "Header")
				{
					if (heading != null) heading.SetActive(any);
					heading = child.gameObject; any = false;
				}
				else if (child.gameObject.activeSelf) any = true;
			}
			if (heading != null) heading.SetActive(any);
		}

		/// <summary>The counts on the World settings button and in the list, and what the chosen plan means for them.</summary>
		internal static void ShowEntry()
		{
			List<string> all = WorldIslands.Candidates();
			HashSet<string> off = WorldIslands.Chosen;
			int taking = all.Count(e => !off.Contains(e));
			if (entryButton != null) UIKit.LabelOf(entryButton).text = "CHOOSE ISLANDS...   " + (taking == all.Count ? "all " + all.Count : taking == 0 ? "none" : taking + " of " + all.Count);
			WorldPlan plan = WorldPlan.Load(NewWorldOptions.Selected);
			bool random = plan == null || plan.Random;
			string note = random ? "" : "The plan '" + plan.Name + "' has no random islands, so this list only counts with a plan that has them (like Random islands).";
			if (entryText != null) entryText.text = random ? (taking == all.Count ? "Every island can turn up while sailing." : taking == 0 ? "No island turns up by chance while sailing (a world plan's own islands still come)." : (all.Count - taking) + " left out of this world.") + (off.Contains(CustomIslandSpawner.GeneratedEntry) ? " No made-up islands." : "") : note;
			if (planNote != null) planNote.text = random ? "Plan: '" + (plan != null ? plan.Name : WorldPlan.RandomName) + "' - islands from this list turn up by chance while sailing (a world plan's own islands only come in their plan)." : note;
			if (allButton != null) UIKit.LabelOf(allButton).text = taking == all.Count && all.Count > 0 ? "Untick all" : "Tick all";
			if (countText != null) countText.text = taking + " of " + all.Count + " take part" + (taking < all.Count ? ",  " + (all.Count - taking) + " left out" : "");
			int[] gap = WorldIslands.ChosenGap;
			if (gapMinText != null) gapMinText.text = gap[0].ToString();
			if (gapMaxText != null) gapMaxText.text = gap[1].ToString();
		}
	}
}
