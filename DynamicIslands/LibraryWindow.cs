using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The ISLAND LIBRARY window (main menu, the New Game box's "GET MORE PLANS...", the editor's Islands window): the island
	/// library's plans and islands with their icons, a search, and for the picked one its pictures, description, author,
	/// version and size, and one button - DOWNLOAD, UPDATE (the library has a newer version than the one installed) or
	/// INSTALLED - plus REMOVE for an installed one. Downloads show their progress and install like Import (LibraryPack).
	/// Never inside a running world. It goes online only while it's open (LibraryClient).
	/// </summary>
	public static class LibraryWindow
	{
		public const string CanvasName = "CustomIslandsLibrary", RowPrefix = "Entry_";

		static Canvas canvas;
		static RectTransform list, detail;
		static Text status, title, meta, quest, tags, description, warning, progress, pictureCount, empty;
		static Button[] tabs;
		static Button mainButton, removeButton, sailingButton, keepButton, cancelButton;
		static InputField search;
		static RawImage picture;
		static int tab, pictureIndex;
		static LibraryEntry selected;
		static bool appearWhileSailing, busy, fromNewGame, pendingRemove, pendingUpdate;
		static readonly Dictionary<string, Button> rows = new Dictionary<string, Button>();

		public static bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }
		public static RectTransform Root { get { return canvas != null ? (RectTransform)canvas.transform : null; } }
		public static LibraryEntry Selected { get { return selected; } }
		public static Button MainButton { get { return mainButton; } }
		public static Button RemoveButton { get { return removeButton; } }
		public static Button KeepButton { get { return keepButton; } }
		public static string Status { get { return status != null ? status.text : ""; } }
		public static string Progress { get { return progress != null ? progress.text : ""; } }
		public static string QuestText { get { return quest != null ? quest.text : ""; } }
		/// <summary>The detail panel's quest line: islands only ("quest" tag, any case); empty for a plan.</summary>
		public static string QuestLine(LibraryInfo i)
		{
			if (i == null || i.IsPlan) return "";
			bool has = i.tags != null && i.tags.Any(t => string.Equals((t ?? "").Trim(), "quest", StringComparison.OrdinalIgnoreCase));
			return has ? "Quest: yes" : "No quest - an island to explore";
		}
		public static bool Busy { get { return busy; } }
		public static Texture Picture { get { return picture != null ? picture.texture : null; } }
		public static Button Row(string id) { Button b; return rows.TryGetValue(id, out b) ? b : null; }
		public static List<string> Shown() { return rows.Where(kv => kv.Value != null && kv.Value.gameObject.activeSelf).Select(kv => kv.Key).ToList(); }

		static void Build()
		{
			canvas = UIKit.CreateCanvas(CanvasName, 880);
			UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
			RectTransform root = (RectTransform)canvas.transform;
			RectTransform dim = UIKit.Rect("Dim", root);
			UIKit.Stretch(dim);
			dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
			RectTransform panel = UIKit.Panel(root, "Panel", new RectOffset(16, 16, 12, 14), 8f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 720f));

			RectTransform head = UIKit.Row(panel, 34f, 8f, "Head");
			Text t = UIKit.Label(head, "ISLAND LIBRARY", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(t);
			UIKit.Size(t.gameObject, 260);
			status = UIKit.Label(head, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Status");
			status.horizontalOverflow = HorizontalWrapMode.Wrap;
			Button submit = UIKit.Button(head, "Submit yours...", OpenSubmit, "How to get your own island or plan into the library", 150, 32f, 13);
			submit.name = "Button_Submit";
			UIKit.Primary(submit);
			// (the user, 2026-10-04: a manual button here, so players can read how the library, islands, world plans and the
			// editor work - the illustrated guide, chapter 15 about the library's examples)
			UIKit.Button(head, "Guide", HelpLinks.OpenGuide, "The illustrated guide (PDF): the island library and its examples (chapter 15), world plans, the editor and everything else", 100, 32f, 13).name = "Button_Guide";
			UIKit.Button(head, "Refresh", () => Load(true), "Read the library's list again", 110, 32f, 13);
			UIKit.Button(head, "Close", Close, "Close the library", 110, 32f, 13);

			RectTransform tools = UIKit.Row(panel, 32f, 8f, "Tools");
			tabs = UIKit.Tabs(tools, new[] { "World plans", "Islands" }, new[] { "Plans: a whole adventure - which islands come, when and where", "Single islands for the editor, your plans, or to turn up while sailing" }, i => { tab = i; ShowList(); }, 32f, 14);
			foreach (Button b in tabs) UIKit.Size(b.gameObject, 150);
			search = UIKit.Field(tools, "Search titles, authors, tags...", "", 32f, "Show only the entries whose title, author, summary or tags hold this");
			search.onValueChanged.AddListener(_ => ShowList());
			WindowKeys keys = canvas.gameObject.AddComponent<WindowKeys>();
			keys.Typing = () => search != null && search.isFocused;
			keys.Close = Close;

			RectTransform body = UIKit.Rect("Body", panel);
			UIKit.Size(body.gameObject, -1, 610);
			HorizontalLayoutGroup h = UIKit.Horizontal(body.gameObject, 12f);
			h.childForceExpandWidth = false; h.childForceExpandHeight = true; h.childControlHeight = true;

			RectTransform listBox = UIKit.Rect("ListBox", body);
			UIKit.Size(listBox.gameObject, 560, 610);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect scroll;
			list = UIKit.ScrollList(listBox, out scroll, 4f);
			UIKit.Stretch((RectTransform)scroll.transform, 4, 4, 4, 4);
			empty = UIKit.Label(listBox, "", 13, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, "Empty");
			UIKit.Stretch(empty.rectTransform, 20, 20, 20, 20);
			empty.horizontalOverflow = HorizontalWrapMode.Wrap;

			detail = UIKit.Rect("Detail", body);
			UIKit.Vertical(detail.gameObject, 5f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(detail.gameObject, 632, 610);
			picture = UIKit.Picture(detail, null, 632, 356, "Picture");
			RectTransform picRow = UIKit.Row(detail, 26f, 6f, "PictureRow");
			UIKit.Button(picRow, "<", () => ShowPicture(pictureIndex - 1), "The picture before", 40, 24f, 13);
			pictureCount = UIKit.Label(picRow, "", 12, UIKit.TextMuted, TextAnchor.MiddleCenter);
			UIKit.Button(picRow, ">", () => ShowPicture(pictureIndex + 1), "The next picture", 40, 24f, 13);
			title = UIKit.Label(detail, "", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "EntryTitle");
			UIKit.Size(title.gameObject, -1, 26);
			meta = UIKit.Label(detail, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Meta");
			meta.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(meta.gameObject, -1, 32);
			// (the user, 2026-10-19: the library page of an island should say whether it has a quest on it or not - read from the "quest" tag)
			quest = UIKit.Label(detail, "", 12, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "QuestLine");
			UIKit.Size(quest.gameObject, -1, 16);
			tags = UIKit.Label(detail, "", 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Tags");
			UIKit.Size(tags.gameObject, -1, 16);
			description = UIKit.Label(detail, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Description");
			description.horizontalOverflow = HorizontalWrapMode.Wrap;
			description.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(description.gameObject, -1, 46);
			warning = UIKit.Label(detail, "", 12, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Italic, "Warning");
			warning.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(warning.gameObject, -1, 30);
			RectTransform actions = UIKit.Row(detail, 34f, 8f, "Actions");
			mainButton = UIKit.Button(actions, "Download", OnMain, "Download and install it", 170, 34f, 15);
			mainButton.name = "Button_Main";
			removeButton = UIKit.Button(actions, "Remove", OnRemove, "Remove what it installed (what a saved world uses stays; click twice)", 120, 34f, 13);
			removeButton.name = "Button_Remove";
			keepButton = UIKit.Button(actions, "Keep my changes", OnKeep, "Update the rest, and keep your changed files as they are (the new version's are not installed)", 150, 34f, 13);
			keepButton.name = "Button_Keep";
			cancelButton = UIKit.Button(actions, "Cancel", () => { pendingUpdate = false; progress.text = ""; ShowDetail(); }, "Don't update now", 90, 34f, 13);
			cancelButton.name = "Button_Cancel";
			sailingButton = UIKit.Button(actions, "", () => { appearWhileSailing = !appearWhileSailing; ShowDetail(); }, "An island may also turn up by chance while sailing - in new worlds and in the ones you've started with random islands (untick it for a new world in World settings)", -1, 34f, 12);
			sailingButton.name = "Button_Sailing";
			progress = UIKit.Label(detail, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Progress");
			progress.horizontalOverflow = HorizontalWrapMode.Wrap;
			progress.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(progress.gameObject, -1, 34);
			canvas.gameObject.SetActive(false);
		}

		/// <summary>Opens the library (tab 0 = plans, 1 = islands). newGame: a downloaded plan is then chosen in the New Game box.</summary>
		public static void Open(int onTab = 0, bool newGame = false)
		{
			if (LoadSceneManager.IsGameSceneLoaded) { DynamicIslands.Notify("The island library opens from the main menu or the island editor, not inside a world.", true); return; }
			if (canvas == null) Build();
			tab = onTab; fromNewGame = newGame; selected = null; pictureIndex = 0; pendingRemove = false; pendingUpdate = false;
			canvas.gameObject.SetActive(true);
			if (search != null) search.text = "";
			progress.text = "";
			ShowList();
			Load(false);
		}

		public static void Close()
		{
			if (canvas != null) canvas.gameObject.SetActive(false);
			if (fromNewGame) NewWorldOptions.Refresh();
		}

		/// <summary>"Submit yours...": how an island or plan gets into the library - export it, post it on the Discord, it is approved first.</summary>
		public static void OpenSubmit()
		{
			InfoWindow.Open("Submit your island or plan",
				"Made an island or a world plan others would enjoy? Share it, and it can be in this library for every player.\n\n" +
				"1.  <b>Export it.</b> In the island editor, open the Islands window (Open) or World plans, pick your island or plan and click <b>Export...</b>. " +
				"Fill in the title, a summary and a description, and take a picture. The pack (.zip) goes to <i>Mods\\DynamicIslands\\exports</i>; it holds every island it needs.\n" +
				"2.  <b>Post the pack on the Custom Islands Discord</b>, with a few words about it: what players find, how long it takes, how many players.\n" +
				"3.  <b>It is approved first.</b> Every entry is looked at (that it loads, works and is fine for everyone) before it goes into the library. " +
				"That can take a while; once it is in, it shows up here for every player.\n\n" +
				"To update it later, export it again (the pack becomes its next version) and post that the same way.\n\n" +
				"By submitting, you say you made it and share it under CC BY 4.0: others may use it and must credit you.",
				new InfoWindow.Choice("Open the Discord", () => HelpLinks.Open(HelpLinks.Discord), "The Custom Islands Discord server: post your pack there", true),
				new InfoWindow.Choice("Open exports folder", OpenExports, "The folder with your exported packs (.zip)"),
				new InfoWindow.Choice("Guide: sharing", () => HelpLinks.OpenGuideSection("47-saving-and-sharing"), "Section 4.7 of the guide, online: Export, packs and the library"),
				new InfoWindow.Choice("Close", InfoWindow.Close, "Back to the library (Esc)"));
		}

		static void OpenExports()
		{
			string folder = LibraryPack.ExportFolder;
			try { Directory.CreateDirectory(folder); } catch { }
			HelpLinks.LastOpened = folder;
			if (HelpLinks.TestMode || HelpLinks.Automated) { InfoWindow.SetStatus("(test) " + folder); return; }
			try { HelpLinks.OpenFolder(Path.GetFullPath(folder)); }
			catch (Exception e) { InfoWindow.SetStatus("Could not open " + folder + ": " + e.Message); return; }
			InfoWindow.SetStatus("Your packs are in " + Path.GetFullPath(folder));
		}

		static void Load(bool force)
		{
			status.text = "Reading the library's list...";
			DynamicIslands.instance.StartCoroutine(LibraryClient.LoadIndex(force, error =>
			{
				status.text = error ?? (LibraryClient.Entries.Count + " entries in the library");
				status.color = error != null ? UIKit.Danger : UIKit.TextMuted;
				ShowList();
			}));
		}

		static IEnumerable<LibraryEntry> ForTab()
		{
			if (LibraryClient.Entries == null) return new LibraryEntry[0];
			string kind = tab == 0 ? LibraryPack.KindPlan : LibraryPack.KindIsland;
			string s = search != null ? search.text.Trim() : "";
			return LibraryClient.Entries.Where(e => e.Info.kind == kind &&
				(s.Length == 0 || (e.Info.title + " " + e.Info.author + " " + e.Info.summary + " " + string.Join(" ", e.Info.tags)).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
				.OrderByDescending(e => e.Info.featured).ThenByDescending(e => e.Updated).ThenBy(e => e.Info.title);
		}

		static void ShowList()
		{
			if (canvas == null) return;
			for (int i = 0; i < tabs.Length; i++) UIKit.SetActive(tabs[i], i == tab);
			foreach (Transform c in list) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
			rows.Clear();
			List<LibraryEntry> shown = ForTab().ToList();
			empty.text = LibraryClient.Entries == null ? (LibraryClient.LastError ?? "") : shown.Count == 0 ? (tab == 0 ? "No world plans" : "No islands") + (search.text.Trim().Length > 0 ? " match the search." : " in the library yet.") : "";
			foreach (LibraryEntry e in shown) AddRow(e);
			if (selected == null || !shown.Contains(selected)) selected = shown.FirstOrDefault();
			ShowDetail();
		}

		static void AddRow(LibraryEntry e)
		{
			LibraryEntry entry = e;
			Button b = UIKit.Button(list, "", () => { selected = entry; pictureIndex = 0; pendingRemove = false; pendingUpdate = false; ShowDetail(); }, null, -1, 64f, 13);
			b.name = RowPrefix + e.Info.id;
			UIKit.Flat(b);
			UIKit.LabelOf(b).text = "";
			RawImage icon = UIKit.Picture(b.transform, null, 56, 56, "Icon");
			RectTransform ir = icon.rectTransform;
			ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f); ir.anchoredPosition = new Vector2(4f, 0f); ir.sizeDelta = new Vector2(56f, 56f);
			Text name = UIKit.Label(b.transform, e.Info.title, 14, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold, "Name");
			UIKit.Stretch(name.rectTransform, 70, 110, 6, 30);
			// (its difficulty when the list says: TODO 4d)
			string difficulty = IslandDifficulty.Label(e.Monsters);
			Text sub = UIKit.Label(b.transform, "by " + e.Info.author + "  -  " + (difficulty.Length > 0 ? difficulty + "  -  " : "") + e.Info.summary, 11, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, "Sub");
			UIKit.Stretch(sub.rectTransform, 70, 110, 28, 4);
			sub.horizontalOverflow = HorizontalWrapMode.Wrap; sub.verticalOverflow = VerticalWrapMode.Truncate;
			LibraryClient.State st = LibraryClient.StateOf(e);
			string badge = st == LibraryClient.State.Update ? "UPDATE" : st == LibraryClient.State.Installed ? "INSTALLED" : e.Info.featured ? "FEATURED" : "";
			Text bd = UIKit.Label(b.transform, badge, 11, st == LibraryClient.State.NotInstalled ? UIKit.Accent : UIKit.Good, TextAnchor.MiddleRight, FontStyle.Bold, "Badge");
			UIKit.Stretch(bd.rectTransform, 10, 10, 0, 0);
			rows[e.Info.id] = b;
			if (!string.IsNullOrEmpty(e.Info.icon))
				DynamicIslands.instance.StartCoroutine(LibraryClient.Picture(e, e.Info.icon, tex => { if (icon != null) icon.texture = tex; }));
		}

		/// <summary>Selects an entry by its id (tests).</summary>
		public static bool Select(string id)
		{
			LibraryEntry e = LibraryClient.Entries != null ? LibraryClient.Entries.FirstOrDefault(x => x.Info.id == id) : null;
			if (e == null) return false;
			tab = e.Info.IsPlan ? 0 : 1;
			selected = e; pictureIndex = 0; pendingRemove = false; pendingUpdate = false;
			ShowList();
			return true;
		}

		static void ShowDetail()
		{
			foreach (var kv in rows) if (kv.Value != null) UIKit.SetActive(kv.Value, selected != null && kv.Key == selected.Info.id);
			bool any = selected != null;
			detail.gameObject.SetActive(any);
			if (!any) return;
			LibraryInfo i = selected.Info;
			title.text = i.title;
			meta.text = (i.IsPlan ? "World plan" : "Island") + " by " + i.author + "  -  version " + i.version + (selected.Updated.Length > 0 ? ", " + selected.Updated : "") +
				"  -  " + Math.Max(1, selected.Size / 1024) + " KB" + (i.IsPlan ? ", " + selected.Islands + " island(s)" : "") +
				(i.players.Length > 0 ? "  -  players " + i.players : "") + (i.length.Length > 0 ? ", " + i.length : "") + (i.basedOn.Length > 0 ? "\nBased on " + i.basedOn : "");
			quest.text = QuestLine(i);
			tags.text = i.tags.Length > 0 ? "Tags: " + string.Join(", ", i.tags) : "";
			description.text = i.description.Length > 0 ? i.description : i.summary;
			LibraryClient.State st = LibraryClient.StateOf(selected);
			bool newer = LibraryPack.CompareVersions(i.minModVersion, LibraryPack.ModVersion) > 0;
			warning.text = newer ? "Made with Custom Islands " + i.minModVersion + " - you have " + LibraryPack.ModVersion + ". Some things may be missing, and a quest that needs them may not be finishable." : "";
			UIKit.LabelOf(mainButton).text = busy ? "..." : st == LibraryClient.State.Installed ? "Installed" : st == LibraryClient.State.Update ? (pendingUpdate ? "Update, keep a copy" : "Update") : newer ? "Download anyway" : "Download";
			mainButton.interactable = !busy && st != LibraryClient.State.Installed;
			if (st == LibraryClient.State.Installed) UIKit.Flat(mainButton); else if (pendingUpdate) UIKit.DangerButton(mainButton); else UIKit.Primary(mainButton);
			removeButton.gameObject.SetActive(st != LibraryClient.State.NotInstalled && !pendingUpdate);
			keepButton.gameObject.SetActive(pendingUpdate && !busy);
			cancelButton.gameObject.SetActive(pendingUpdate && !busy);
			UIKit.Flat(keepButton);
			UIKit.Flat(cancelButton);
			UIKit.LabelOf(removeButton).text = pendingRemove ? "Sure? Remove" : "Remove";
			if (pendingRemove) UIKit.DangerButton(removeButton); else UIKit.Flat(removeButton);
			removeButton.interactable = !busy;
			sailingButton.gameObject.SetActive(!i.IsPlan && st != LibraryClient.State.Installed);
			UIKit.LabelOf(sailingButton).text = "Also turn up while sailing:  " + (appearWhileSailing ? "YES" : "no");
			UIKit.SetActive(sailingButton, appearWhileSailing);
			ShowPicture(pictureIndex);
		}

		static void ShowPicture(int index)
		{
			if (selected == null) return;
			string[] pics = selected.Info.pictures.Length > 0 ? selected.Info.pictures : (selected.Info.icon.Length > 0 ? new[] { selected.Info.icon } : new string[0]);
			if (pics.Length == 0) { picture.texture = null; pictureCount.text = "no picture"; return; }
			pictureIndex = (index % pics.Length + pics.Length) % pics.Length;
			string count = (pictureIndex + 1) + " / " + pics.Length;
			LibraryEntry e = selected;
			int want = pictureIndex;
			// (cleared first: while a slow download ran, the entry before's picture stayed up, so one island's picture
			// showed on every entry clicked after it. A picture in memory comes back at once, before the first yield.)
			picture.texture = null;
			pictureCount.text = count + "  (loading)";
			DynamicIslands.instance.StartCoroutine(LibraryClient.Picture(e, pics[pictureIndex], tex =>
			{
				if (selected != e || pictureIndex != want) return;
				picture.texture = tex;
				pictureCount.text = tex != null ? count : count + "  (can't be loaded)";
			}));
		}

		/// <summary>Download / Update (public for tests: clicking the button does this).</summary>
		public static void OnMain()
		{
			if (selected == null || busy) return;
			LibraryEntry e = selected;
			bool update = LibraryClient.StateOf(e) == LibraryClient.State.Update;
			// (an update replaces the entry's files - also ones the player changed since: the first click says which, and
			// offers the choice - replace them (theirs kept as "name (yours)", out of the random pool), keep them, or cancel)
			List<string> changed = update ? LibraryPack.ChangedFiles(e.Info.id) : new List<string>();
			if (changed.Count > 0 && !pendingUpdate)
			{
				pendingUpdate = true;
				pendingRemove = false;
				progress.color = UIKit.Danger;
				progress.text = "You changed " + string.Join(", ", changed.Select(n => "'" + n + "'").ToArray()) + " since you downloaded it" + UsedIn(changed) + ". " +
					"Update, keep a copy: the new version replaces them, and yours stay as '<name> (yours)'. Keep my changes: the rest is updated, your changed files stay as they are.";
				ShowDetail();
				return;
			}
			Start(e, update, true);
		}

		/// <summary>Keep my changes (public for tests): updates with the player's changed files left as they are.</summary>
		public static void OnKeep()
		{
			if (selected == null || busy || !pendingUpdate) return;
			Start(selected, true, false);
		}

		/// <summary>Where the changed files are used: " (used in the worlds 'A', 'B')" or "".</summary>
		static string UsedIn(List<string> names)
		{
			var worlds = new List<string>();
			try { foreach (string n in names) foreach (string w in LibraryPack.WorldsUsing(n)) if (!worlds.Contains(w)) worlds.Add(w); }
			catch (Exception ex) { Debug.LogWarning("[CUSTOM ISLANDS] [library] Worlds using: " + ex.Message); }
			return worlds.Count == 0 ? "" : " (used in the world" + (worlds.Count > 1 ? "s " : " ") + string.Join(", ", worlds.Take(4).Select(w => "'" + w + "'").ToArray()) + (worlds.Count > 4 ? "..." : "") + ")";
		}

		static void Start(LibraryEntry e, bool update, bool replace)
		{
			pendingUpdate = false;
			busy = true;
			pendingRemove = false;
			ShowDetail();
			DynamicIslands.instance.StartCoroutine(LibraryClient.Download(e, appearWhileSailing, replace, text => progress.text = text, (report, error) =>
			{
				busy = false;
				if (error != null) { progress.text = error; progress.color = UIKit.Danger; }
				else
				{
					progress.color = UIKit.TextColor;
					string planLine = report.PlanName != null ? (fromNewGame ? "The plan '" + report.PlanName + "' is chosen in the New Game box." : "Pick the plan '" + report.PlanName + "' in the New Game box.") : "";
					progress.text = (update ? "Updated" : "Installed") + " '" + e.Info.title + "'. " + planLine +
						(report.Lines.Any(l => l.Contains(" is kept as '")) ? " (Your changed versions are kept as '<name> (yours)'.)" :
						report.Lines.Any(l => l.StartsWith("Kept your changed")) ? " (Your changed files were kept.)" :
						report.Lines.Any(l => l.Contains(" as '")) ? " (Some islands got a new name: you have different ones with the same name.)" : "");
					if (fromNewGame && report.PlanName != null) WorldDirector.PendingPlan = report.PlanName;
				}
				ShowList();
			}));
		}

		/// <summary>Remove (public for tests): the first click asks, the second removes.</summary>
		public static void OnRemove()
		{
			if (selected == null || busy) return;
			if (!pendingRemove) { pendingRemove = true; pendingUpdate = false; ShowDetail(); return; }
			pendingRemove = false;
			LibraryPack.Report r = LibraryPack.Remove(selected.Info.id);
			progress.color = UIKit.TextColor;
			progress.text = "Removed '" + selected.Info.title + "'." + (r.Lines.Any(l => l.StartsWith("Kept")) ? " " + string.Join(" ", r.Lines.Where(l => l.StartsWith("Kept")).ToArray()) : "");
			ShowList();
		}
	}
}
