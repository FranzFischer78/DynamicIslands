using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "My islands" (ROADMAP T3): every island file in one list - where it came from (mine, the island library, generated
	/// while sailing, a copy from a host), how many saved worlds use it and its weight in the random pool (spawnpool.txt) -
	/// with Open, Rename, Delete and Tidy up (unused generated islands and host copies). Filters: All, Mine, Library,
	/// Generated, From hosts; a search. Opened by My islands... in the Islands window.
	/// </summary>
	public class MyIslandsWindow : MonoBehaviour
	{
		public const string All = "All", Mine = "Mine", Library = "Library", Generated = "Generated", Hosts = "From hosts";
		static readonly string[] Filters = { All, Mine, Library, Generated, Hosts };

		static MyIslandsWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		internal static GameObject Root { get { return instance != null ? instance.gameObject : null; } }
		internal static InputField NameField { get { return instance != null ? instance.nameField : null; } }
		internal static InputField SearchField { get { return instance != null ? instance.search : null; } }
		internal static string StatusText { get { return instance != null ? instance.status.text : ""; } }
		internal static void Pick(string name) { if (instance != null) instance.OnPick(name); }
		internal static void SetFilter(string f) { if (instance != null) { instance.filter = f; instance.Refresh(); } }
		/// <summary>Tests: the names listed now.</summary>
		internal static List<string> Listed { get { return instance != null ? instance.listed : new List<string>(); } }

		string filter = All, picked, pendingDelete, pendingGive;
		/// <summary>R1c: "Give my worlds this version", shown for a picked island that saved worlds play from an older copy.</summary>
		RectTransform giveRow;
		Text giveText;
		List<string> olderWorlds = new List<string>();
		/// <summary>Tests: whether the Give row is shown now, and a press of its button.</summary>
		internal static bool GiveShown { get { return instance != null && instance.giveRow.gameObject.activeSelf; } }
		internal static void Give() { if (instance != null) instance.OnGive(); }
		InputField search, nameField;
		RectTransform listContent;
		Text status, summary;
		Button[] tabs;
		readonly List<string> listed = new List<string>();
		Dictionary<string, HashSet<string>> worlds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> library = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("MyIslandsWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<MyIslandsWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.picked = instance.pendingDelete = instance.pendingGive = null;
			instance.pendingTidy = null;
			instance.ShowGive();
			instance.nameField.text = "";
			instance.search.text = "";
			instance.Look();
			instance.Refresh();
			instance.SetStatus("Click an island to pick it. The weight is its chance in the random pool while sailing (0 = never).", false);
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		void Update()
		{
			EditorInput.IsTyping = (search != null && search.isFocused) || (nameField != null && nameField.isFocused) || listContent.GetComponentsInChildren<InputField>().Any(f => f.isFocused);
			if (EscKey.Down(this)) Close();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 9f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "MY ISLANDS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			summary = UIKit.Label(head, "", 11, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, "Summary");
			tabs = UIKit.Tabs(panel, Filters, new[] { "Every island file", "Islands you made (or saved)", "Islands the island library or a pack installed", "Islands generated while sailing (gen-...): they live on in their worlds", "Copies of hosts' islands (name_<hash>), kept for the worlds that use them" },
				i => { filter = Filters[i]; Refresh(); }, 28f, 13);
			search = UIKit.Field(panel, "Search (name)", "", 28f, "Only the islands whose name has this");
			search.onValueChanged.AddListener(v => Refresh());

			RectTransform cols = UIKit.Row(panel, 16f, 6f, "Columns");
			UIKit.Label(cols, "Island", 11, UIKit.TextMuted, TextAnchor.MiddleLeft);
			foreach (var c in new[] { new KeyValuePair<string, int>("From", 84), new KeyValuePair<string, int>("Worlds", 64), new KeyValuePair<string, int>("Weight", 66) })
				UIKit.Size(UIKit.Label(cols, c.Key, 11, UIKit.TextMuted, TextAnchor.MiddleCenter).gameObject, c.Value, 16);

			RectTransform listBox = UIKit.Rect("ListBox", panel);
			UIKit.Size(listBox.gameObject, -1, 320);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect sr;
			listContent = UIKit.ScrollList(listBox, out sr, 2f);
			UIKit.Stretch((RectTransform)sr.transform, 4, 4, 4, 4);

			nameField = UIKit.Field(panel, "the picked island's name - type a new one and press Rename", "", 28f, "Type a new name and press Rename: worlds, plans and rules that name it follow");
			nameField.characterLimit = 64;
			nameField.onValueChanged.AddListener(v => pendingDelete = null);
			status = UIKit.Label(panel, "", 12, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Italic, "Status");
			status.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(status.gameObject, -1, 34);
			UIKit.Fit(status);

			giveRow = UIKit.Row(panel, 28f, 6f, "GiveRow");
			giveText = UIKit.Label(giveRow, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, "GiveText");
			giveText.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Button(giveRow, "Give my worlds this version", OnGive, "Saved worlds that play an older copy of the picked island (kept for them when it changed) play it as it is now; the old copies go to the deleted folder (asks first)", 230, 28f, 13).name = "Button_GiveVersion";
			giveRow.gameObject.SetActive(false);

			RectTransform buttons = UIKit.Row(panel, 32f, 6f, "Buttons");
			UIKit.Button(buttons, "Open", OnOpen, "Open the picked island in the editor", -1, 32, 14);
			UIKit.Button(buttons, "Rename", OnRename, "Give the picked island the name typed above", -1, 32, 14);
			Button del = UIKit.Button(buttons, "Delete", OnDelete, "Delete the picked island (asks first, says who uses it; moved to Mods\\DynamicIslands\\deleted)", -1, 32, 14);
			UIKit.DangerButton(del);
			UIKit.Button(buttons, "Tidy up", OnTidy, "Move copies from hosts no saved world uses and generated islands nothing uses to the deleted folder (click twice)", -1, 32, 14).name = "Button_TidyUp";
			UIKit.Button(buttons, "Close", Close, "Close (Esc)", -1, 32, 14);
		}

		/// <summary>Who uses what: the saved worlds (every copy of their state) and the library's installs.</summary>
		void Look()
		{
			try { worlds = Housekeeping.WorldsByIsland(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] My islands: " + e.Message); }
			library = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try { foreach (LibraryInstalled e in LibraryPack.Installed()) foreach (LibraryInstalledFile f in e.files) if (!f.shared && f.kind != LibraryPack.KindPlan) library.Add(f.name); } catch { }
		}

		/// <summary>Where an island file came from: Mine, Library, Generated or From hosts.</summary>
		public static string SourceOf(string name, ICollection<string> libraryNames)
		{
			if (IslandNetwork.IsDownloadName(name)) return Hosts;
			if (name.StartsWith(CustomIslandSpawner.GeneratedPrefix, StringComparison.OrdinalIgnoreCase)) return Generated;
			if (libraryNames != null && libraryNames.Contains(name)) return Library;
			return Mine;
		}

		void Refresh()
		{
			for (int i = 0; i < tabs.Length; i++) UIKit.SetActive(tabs[i], Filters[i] == filter);
			foreach (Transform child in listContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			listed.Clear();
			List<string> all = IslandSpawner.ListSavedIslands().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
			string q = search.text.Trim();
			var counts = Filters.Skip(1).ToDictionary(f => f, f => 0);
			foreach (string n in all) counts[SourceOf(n, library)]++;
			summary.text = all.Count + " islands: " + string.Join(", ", counts.Where(c => c.Value > 0).Select(c => c.Value + " " + c.Key.ToLowerInvariant()).ToArray());
			foreach (string n in all)
			{
				string src = SourceOf(n, library);
				if (filter != All && src != filter) continue;
				if (q.Length > 0 && n.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
				listed.Add(n);
			}
			foreach (string n in listed.Take(400)) Row(n);
			if (listed.Count > 400) UIKit.Label(listContent, "... and " + (listed.Count - 400) + " more: search to narrow the list", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			if (listed.Count == 0) UIKit.Label(listContent, all.Count == 0 ? "No islands yet." : "None here.", 13, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
		}

		void Row(string n)
		{
			string name = n, src = SourceOf(n, library);
			HashSet<string> by;
			int used = worlds.TryGetValue(n, out by) ? by.Count : 0;
			RectTransform row = UIKit.Row(listContent, 26f, 6f, "Row_" + n);
			Button pick = UIKit.Button(row, n, () => OnPick(name), used > 0 ? "Used in: " + string.Join(", ", by.Take(8).ToArray()) + (used > 8 ? " and " + (used - 8) + " more" : "") : "No saved world uses it", -1, 26f, 13);
			pick.name = "Island_" + n;
			Text label = UIKit.LabelOf(pick);
			label.alignment = TextAnchor.MiddleLeft;
			label.rectTransform.offsetMin = new Vector2(8, 0);
			UIKit.Fit(label, 9);
			UIKit.SetActive(pick, n == picked);
			UIKit.Size(UIKit.Label(row, src, 11, src == Mine ? UIKit.TextColor : UIKit.TextMuted, TextAnchor.MiddleCenter).gameObject, 84, 26);
			UIKit.Size(UIKit.Label(row, used == 0 ? "-" : used.ToString(), 12, used > 0 ? UIKit.TextColor : UIKit.TextMuted, TextAnchor.MiddleCenter).gameObject, 64, 26);
			bool listedInPool;
			float w = CustomIslandSpawner.PoolWeight(n, out listedInPool);
			InputField wf = UIKit.Field(row, "0", w.ToString("0.##", CultureInfo.InvariantCulture), 26f, src == Hosts ? "A copy from a host never comes by chance" : listedInPool ? "Its own weight: how often it turns up by chance (0 = never)" : "The weight every island without its own gets: type one of its own");
			wf.name = "Weight_" + n;
			UIKit.Size(wf.gameObject, 66, 26);
			wf.contentType = InputField.ContentType.DecimalNumber;
			wf.characterLimit = 5;
			wf.interactable = src != Hosts;
			if (!listedInPool && wf.textComponent != null) wf.textComponent.color = UIKit.TextMuted;
			wf.onEndEdit.AddListener(v => SetWeight(name, v, wf));
		}

		/// <summary>A row's weight typed: written to spawnpool.txt and the field updated in place (the list isn't built
		/// again - that destroyed the button the click that left the field was on, and the click was lost).</summary>
		void SetWeight(string name, string text, InputField wf)
		{
			float w;
			bool listedInPool;
			if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out w) || w < 0f)
			{
				SetStatus("The weight is a number: 1 = like any island, 2 = twice as likely, 0 = never by chance.", true);
				wf.text = CustomIslandSpawner.PoolWeight(name, out listedInPool).ToString("0.##", CultureInfo.InvariantCulture);
				return;
			}
			if (Mathf.Approximately(CustomIslandSpawner.PoolWeight(name, out listedInPool), w) && listedInPool) return;
			try { CustomIslandSpawner.SetPoolWeight(name, w); }
			catch (Exception e) { SetStatus("Could not change spawnpool.txt: " + e.Message, true); return; }
			SetStatus("'" + name + "' now has weight " + w.ToString("0.##", CultureInfo.InvariantCulture) + " in the random pool" + (w == 0f ? " (it never comes by chance)" : "") + ".", false);
			if (wf.textComponent != null) wf.textComponent.color = UIKit.TextColor;
		}

		void OnPick(string n)
		{
			picked = n;
			pendingDelete = pendingGive = null;
			pendingTidy = null;
			nameField.text = n;
			ShowGive();
			HashSet<string> by;
			int used = worlds.TryGetValue(n, out by) ? by.Count : 0;
			SetStatus("'" + n + "' (" + SourceOf(n, library).ToLowerInvariant() + "): " + (used > 0 ? "used in " + string.Join(", ", by.Take(4).Select(x => "'" + x + "'").ToArray()) + (used > 4 ? " and " + (used - 4) + " more" : "") : "no saved world uses it") + ".", false);
			foreach (Button b in listContent.GetComponentsInChildren<Button>()) UIKit.SetActive(b, b.name == "Island_" + n);
		}

		bool HasPick() { return picked != null && File.Exists(IslandSpawner.PathFor(picked)); }

		void OnOpen()
		{
			if (!HasPick()) { SetStatus("Pick an island first.", true); return; }
			string n = picked;
			Close();
			IslandFilesWindow.Close();
			if (!DynamicIslands.LoadIsland(n)) DynamicIslands.Notify("Opening '" + n + "' failed - see the console (F10)", true);
		}

		void OnRename()
		{
			if (!HasPick()) { SetStatus("Pick an island first.", true); return; }
			string to = nameField.text.Trim();
			string problem = IslandRename.Problem(picked, to);
			if (problem != null) { SetStatus(problem, true); return; }
			try
			{
				string from = picked;
				List<string> also = IslandRename.Rename(from, to);
				picked = to;
				pendingGive = null;
				Look();
				Refresh();
				ShowGive();
				SetStatus("Renamed '" + from + "' to '" + to + "'." + (also.Count > 0 ? " Changed too: " + string.Join("; ", also.ToArray()) + "." : ""), false);
				DynamicIslands.Notify("Renamed '" + from + "' to '" + to + "'");
			}
			catch (Exception e) { SetStatus(SafeFile.InUse(e) ? "It is in use by another program - close it there and rename again (nothing was changed)." : "Could not rename: " + e.Message, true); }
		}

		void OnDelete()
		{
			if (!HasPick()) { SetStatus("Pick an island first.", true); return; }
			string n = picked;
			if (pendingDelete != n)
			{
				pendingDelete = n;
				HashSet<string> by;
				List<string> plans = IslandFilesWindow.PlansNaming(n), islands = IslandFilesWindow.IslandsNaming(n);
				string uses = (worlds.TryGetValue(n, out by) && by.Count > 0 ? " Saved worlds that have it: " + string.Join(", ", by.Take(4).ToArray()) + (by.Count > 4 ? " and " + (by.Count - 4) + " more" : "") + " - it goes missing there." : "") +
					(plans.Count > 0 ? " Plans that bring it: " + string.Join(", ", plans.Take(4).ToArray()) + "." : "") +
					(islands.Count > 0 ? " Islands whose rules bring it: " + string.Join(", ", islands.Take(4).ToArray()) + "." : "");
				SetStatus("Delete '" + n + "'? Press Delete again." + uses + " (It is moved to Mods\\DynamicIslands\\" + IslandFilesWindow.DeletedFolderName + ".)", true);
				return;
			}
			try
			{
				IslandFilesWindow.MoveToDeleted(n);
				picked = pendingDelete = pendingGive = null;
				nameField.text = "";
				IslandCache.Forget();
				Refresh();
				ShowGive();
				SetStatus("Deleted '" + n + "' (kept in " + IslandFilesWindow.DeletedFolderName + ").", false);
				DynamicIslands.Notify("Deleted island '" + n + "' (kept in " + IslandFilesWindow.DeletedFolderName + " until you remove it there)");
			}
			catch (Exception e) { SetStatus(SafeFile.InUse(e) ? "'" + n + "' is in use by another program - close it there and delete again (nothing was moved)." : "Could not delete: " + e.Message, true); }
		}

		/// <summary>The Give row: shown when saved worlds play an older copy of the picked island.</summary>
		void ShowGive()
		{
			olderWorlds = picked != null && File.Exists(IslandSpawner.PathFor(picked)) ? LibraryPack.WorldsOnOlderVersion(picked) : new List<string>();
			giveRow.gameObject.SetActive(olderWorlds.Count > 0);
			if (olderWorlds.Count > 0)
				giveText.text = olderWorlds.Count + " saved world" + (olderWorlds.Count == 1 ? " plays" : "s play") + " an older copy: " + string.Join(", ", olderWorlds.Take(3).Select(w => "'" + w + "'").ToArray()) + (olderWorlds.Count > 3 ? " and " + (olderWorlds.Count - 3) + " more" : "");
		}

		void OnGive()
		{
			if (!HasPick() || olderWorlds.Count == 0) { SetStatus("Pick an island that saved worlds play from an older copy.", true); return; }
			string n = picked;
			if (pendingGive != n)
			{
				pendingGive = n;
				SetStatus("Give " + string.Join(", ", olderWorlds.Take(4).Select(w => "'" + w + "'").ToArray()) + (olderWorlds.Count > 4 ? " and " + (olderWorlds.Count - 4) + " more" : "") +
					" this '" + n + "'? What was picked, looted or opened there may land on other objects. Press the button again.", true);
				return;
			}
			pendingGive = null;
			try
			{
				List<string> moved;
				List<string> changed = LibraryPack.GiveWorldsThisVersion(n, out moved);
				string text = LibraryPack.GaveText(n, changed, moved);
				Look();
				Refresh();
				OnPick(n);
				SetStatus(text, false);
				DynamicIslands.Notify(text, false, 8);
			}
			catch (Exception e) { SetStatus(SafeFile.InUse(e) ? "A world file is in use by another program - close it there and try again." : "Could not change them: " + e.Message, true); }
		}

		Housekeeping.Scan pendingTidy;

		/// <summary>Tidy up asks first: the first press looks and says what it would clear, the second looks again and clears
		/// what was shown and is still unused.</summary>
		void OnTidy()
		{
			Housekeeping.Scan scan = Housekeeping.Look();
			if (pendingTidy == null)
			{
				if (scan.UnusedCopies + scan.UnusedGenerated.Count == 0) { SetStatus("Nothing to tidy up: every copy from a host and every generated island is used.", false); return; }
				pendingTidy = scan;
				SetStatus(scan.Describe() + " Press Tidy up again to clear it.", true);
				return;
			}
			scan = scan.Within(pendingTidy);
			pendingTidy = null;
			string text = Housekeeping.TidyUp(scan);
			Look();
			Refresh();
			ShowGive();
			SetStatus(text, false);
		}

		void SetStatus(string message, bool warning)
		{
			status.text = message;
			status.color = warning ? new Color(1f, 0.72f, 0.45f) : UIKit.TextColor;
		}
	}
}
