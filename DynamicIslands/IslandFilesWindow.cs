using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The editor's Save / Load window: a name field, the island's height in the world, a list of saved islands
	/// (click = pick, double-click = load) and Save / Load / Delete / Close buttons. Built in code with UIKit.
	/// Open with the top bar's Open / Save as buttons or Ctrl+O; Ctrl+S saves the current island.
	/// </summary>
	public class IslandFilesWindow : MonoBehaviour
	{
		static IslandFilesWindow instance;

		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		/// <summary>Tests: the window, its name field and its status line.</summary>
		internal static GameObject Root { get { return instance != null ? instance.gameObject : null; } }
		internal static InputField NameField { get { return instance != null ? instance.nameField : null; } }
		internal static string StatusText { get { return instance != null ? instance.status.text : ""; } }

		InputField nameField;
		InputField elevationField;
		RectTransform listContent;
		Text status;
		string pendingOverwrite, pendingDelete, renameFrom;
		string lastClicked;
		float lastClickTime;

		/// <summary>Builds the (hidden) window on the editor canvas. Call once when the editor opens.</summary>
		public static void Create(Transform canvas, GameObject unusedTemplate)
		{
			var blocker = new GameObject("IslandFilesWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f); // dims the editor and swallows clicks (modal)
			instance = blocker.AddComponent<IslandFilesWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			GeneratorWindow.Close();
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.nameField.text = DynamicIslands.currentIslandName;
			instance.elevationField.text = DynamicIslands.currentElevation.ToString(System.Globalization.CultureInfo.InvariantCulture);
			instance.pendingOverwrite = instance.pendingDelete = instance.renameFrom = null;
			instance.SetStatus("Type a name and press Save, or pick an island. Double-click an island to open it.", false);
			instance.Refresh();
			instance.nameField.ActivateInputField();
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		/// <summary>Ctrl+S: save straight away if the island already has a file, otherwise ask for a name.</summary>
		public static void QuickSave()
		{
			// (a new island is "myisland" until it has its own name: it saved over a 'myisland' saved before)
			if (!DynamicIslands.IsUnnamed && File.Exists(IslandSpawner.PathFor(DynamicIslands.currentIslandName))) DynamicIslands.SaveIsland(DynamicIslands.currentIslandName, true);
			else Open();
		}

		void Update()
		{
			EditorInput.IsTyping = (nameField != null && nameField.isFocused) || (elevationField != null && elevationField.isFocused);
			if (EscKey.Down(this)) Close();
			else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && nameField.text.Trim().Length > 0) { if (renameFrom != null) OnRename(); else OnSave(); } // (Enter after Rename renames: it saved the open island under the new name)
		}

		void OnDisable()
		{
			EditorInput.IsTyping = false;
		}

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 0));

			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "ISLANDS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Label(head, "Mods\\DynamicIslands", 11, UIKit.TextMuted, TextAnchor.MiddleRight);

			// Name and height, side by side
			RectTransform island = UIKit.Group(panel, "Island");
			// (a ? after each label; the height's label and its ? are as wide as its field below: 124 + 8 + 18)
			RectTransform labels = UIKit.Row(island, 18f, 8f, "Labels");
			UIKit.Size(UIKit.Label(labels, "Name", 13, UIKit.TextMuted).gameObject, 40);
			UIKit.Help(labels, HelpName).name = "Help_Name";
			UIKit.Size(UIKit.Label(labels, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, "Gap").gameObject, -1, -1, 1);
			Text hl = UIKit.Label(labels, "Height in the world (m)", 13, UIKit.TextMuted);
			UIKit.Size(hl.gameObject, 124);
			UIKit.Fit(hl, 10);
			UIKit.Help(labels, HelpHeight).name = "Help_Height";
			RectTransform fields = UIKit.Row(island, 32f, 8f, "Fields");
			nameField = UIKit.Field(fields, "e.g. myisland", "", 32f);
			nameField.characterLimit = 64;
			nameField.onValueChanged.AddListener(v => { pendingOverwrite = null; pendingDelete = null; Highlight(); });
			// Elevation: 0 = normal island, above 0 = flying, below 0 = under water (saved with the island)
			elevationField = UIKit.Field(fields, "0", "0", 32f, "60 = a flying island, -30 = under water, 0 = a normal island");
			UIKit.Size(elevationField.gameObject, 150);
			elevationField.contentType = InputField.ContentType.DecimalNumber;
			elevationField.characterLimit = 6;
			elevationField.onEndEdit.AddListener(v =>
			{
				float e;
				// (an undo step and an unsaved change, like the Island tab's height: it was neither, so Test and the autosave missed it)
				IslandSettingsUndo.Change(() => DynamicIslands.currentElevation = float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e)
					? Mathf.Clamp(e, IslandSpawner.MinElevation, IslandSpawner.MaxElevation) : 0f);
				elevationField.text = DynamicIslands.currentElevation.ToString(System.Globalization.CultureInfo.InvariantCulture);
				EditorUI.RefreshIsland();
				SetStatus("In a world this island will be " + IslandSpawner.DescribeElevation(DynamicIslands.currentElevation) + ". Save to keep it.", false);
			});

			// Saved islands
			RectTransform saved = UIKit.Group(panel, "Saved islands");
			RectTransform listHead = UIKit.Row(saved, 18f, 6f, "ListHead");
			UIKit.Label(listHead, "Click an island to pick it, double-click to open it", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Help(listHead, HelpList).name = "Help_List";
			RectTransform listBox = UIKit.Rect("ListBox", saved);
			UIKit.Size(listBox.gameObject, -1, 230);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect sr;
			listContent = UIKit.ScrollList(listBox, out sr, 3f);
			UIKit.Stretch((RectTransform)sr.transform, 4, 4, 4, 4);

			// Sharing: the picked island as a pack (with the islands it brings), and packs others made
			RectTransform share = UIKit.Row(panel, 30f, 8f, "Share");
			UIKit.Button(share, "Export...", OnExport, "Share the picked island: a pack (.zip) with the islands its rules bring, to send or to put in the island library", -1, 30, 13);
			UIKit.Button(share, "Import...", () => { Close(); LibraryImportWindow.Open(); }, "Install islands and plans from a pack (.zip) someone made, or remove what you installed", -1, 30, 13);
			UIKit.Button(share, "Library...", () => { Close(); LibraryWindow.Open(1); }, "The island library: islands and plans others made, to download", -1, 30, 13);
			UIKit.Button(share, "My islands...", () => { Close(); MyIslandsWindow.Open(); }, "Every island file: where it came from, which worlds use it, its weight in the random pool; open, rename, delete, tidy up", -1, 30, 13).name = "Button_MyIslands";
			UIKit.Help(share, HelpShare).name = "Help_Share";

			status = UIKit.Label(panel, "", 14, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Italic, "Status");
			UIKit.Size(status.gameObject, -1, 34);
			UIKit.Fit(status);

			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			Button save = UIKit.Button(buttons, "Save", OnSave, "Save the island under this name (Enter)", -1, 34, 15);
			UIKit.Primary(save);
			UIKit.Button(buttons, "Open", OnLoad, "Open the picked island (unsaved changes are kept as an autosave)", -1, 34, 15);
			Button ren = UIKit.Button(buttons, "Rename", OnRename, "Rename the picked island: press Rename, type the new name, press Rename again (worlds, plans and rules that name it follow)", -1, 34, 15);
			ren.name = "Button_Rename";
			Button del = UIKit.Button(buttons, "Delete", OnDelete, "Delete the picked island's file (asks first)", -1, 34, 15);
			UIKit.DangerButton(del);
			UIKit.Button(buttons, "Close", Close, "Close (Esc)", -1, 34, 15);
		}

		// The ? marks' texts (the tests check each is there)
		public const string HelpName = "NAME: the island's file name - Mods\\DynamicIslands\\<name>.island. Type one and press Save (or Enter) to save the " +
			"island under it; picking an island in the list puts its name here. Saving under a name that is taken asks first.\n\n" +
			"Letters, numbers, spaces and - _ are fine; not \\ / : * ? \" < > | = , ; or a # or @ at the start, at most 60 characters. " +
			"The name players see in the world is the Island tab's Name, not this one.";
		public const string HelpHeight = "HEIGHT IN THE WORLD: how high the whole island sits in a world, in metres - the same value as the Island tab's Height " +
			"(change one and the other follows).\n\n" +
			"0 = a normal island at sea level: players step onto its beach from the raft.\n" +
			"Above 0 = a flying island (e.g. 60): it floats over the sea with a rocky underside; players build stairs or pillars up from the raft " +
			"to reach it, and a raft fits underneath.\n" +
			"Below 0 = sunk under water (e.g. -30): its land lies that many metres deeper than a normal island's; players dive to it.\n\n" +
			"From -100 to 250 m. It is saved with the island (press Save to keep it) and Ctrl+Z undoes a change.";
		public const string HelpList = "SAVED ISLANDS: every island in Mods\\DynamicIslands, with when it was saved and its size. Click one to pick it " +
			"(its name goes into Name, for Open, Rename, Delete or Export), double-click to open it in the editor - unsaved changes " +
			"of the island open now are kept as an autosave.\n\nCopies kept for saved worlds or downloaded from a host aren't listed: " +
			"My islands... shows every file.";
		public const string HelpShare = "SHARING: Export... makes a pack (.zip) of the picked island, with the islands its rules bring, to send to a friend or " +
			"put in the island library. Import... installs a pack someone made (or removes what you installed). Library... opens the island " +
			"library to download islands and plans others made. My islands... lists every island file: where it came from, which worlds use it, " +
			"its weight in the random pool.";

		// Rows built at once when the window opens; the rest follow, RowsPerFrame a frame (each row costs ~1 ms in a slow
		// box: 1,000 islands held the window's opening for over 2 s)
		const int RowsAtOnce = 120, RowsPerFrame = 200;
		Coroutine moreRows;

		void Refresh()
		{
			if (moreRows != null) { StopCoroutine(moreRows); moreRows = null; }
			foreach (Transform child in listContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			// (not the copies kept for saved worlds or downloaded from hosts - name_hash: deleting one broke the world that plays
			// it, and opening one to edit makes a second island; the library window's Remove unused cleans them up)
			string[] names = IslandSpawner.ListSavedIslands().Where(n => !IslandNetwork.IsDownloadName(n)).ToArray();
			if (names.Length == 0)
			{
				UIKit.Size(UIKit.Label(listContent, "No saved islands yet.", 14, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, "Empty").gameObject, -1, 30);
				return;
			}
			Button first = null;
			int now = Math.Min(names.Length, RowsAtOnce);
			for (int i = 0; i < now; i++) first = AddRow(names[i], first);
			Highlight();
			if (now < names.Length) moreRows = StartCoroutine(MoreRows(names, now, first));
		}

		System.Collections.IEnumerator MoreRows(string[] names, int from, Button first)
		{
			for (int i = from; i < names.Length; i++)
			{
				if ((i - from) % RowsPerFrame == 0) yield return null;
				AddRow(names[i], first);
			}
			moreRows = null;
			Highlight();
		}

		/// <summary>One island's row; returns the first row (the others are copies of it: building each took 2-5 ms).</summary>
		Button AddRow(string n, Button first)
		{
			string islandName = n;
			var info = new FileInfo(IslandSpawner.PathFor(n));
			string metaText = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + "   " + Math.Max(1, info.Length / 1024) + " KB";
			Button entry;
			if (first == null)
			{
				entry = first = UIKit.Button(listContent, n, () => OnEntryClicked(islandName), null, -1, 30, 14);
				Text label = UIKit.LabelOf(entry);
				label.alignment = TextAnchor.MiddleLeft;
				label.rectTransform.offsetMin = new Vector2(10, 0);
				label.rectTransform.offsetMax = new Vector2(-150, 0); // clear of the date and size on the right
				UIKit.Fit(label, 9);
				Text meta = UIKit.Label(entry.transform, metaText, 11, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, "Meta");
				UIKit.Stretch(meta.rectTransform, 10, 10, 0, 0);
			}
			else
			{
				entry = UIKit.CopyButton(first, listContent, n, () => OnEntryClicked(islandName), 14);
				entry.transform.Find("Meta").GetComponent<Text>().text = metaText;
			}
			entry.name = "Island_" + n;
			return first;
		}

		void Highlight()
		{
			string current = nameField != null ? nameField.text.Trim() : "";
			foreach (Button b in listContent.GetComponentsInChildren<Button>(true))
				UIKit.SetActive(b, b.name == "Island_" + current);
		}

		void OnEntryClicked(string islandName)
		{
			bool doubleClick = lastClicked == islandName && Time.unscaledTime - lastClickTime < 0.4f;
			lastClicked = islandName; lastClickTime = Time.unscaledTime;
			nameField.text = islandName;
			if (doubleClick) OnLoad();
		}

		void OnSave()
		{
			float e;
			if (float.TryParse(elevationField.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e))
				DynamicIslands.currentElevation = Mathf.Clamp(e, IslandSpawner.MinElevation, IslandSpawner.MaxElevation);
			string n = nameField.text.Trim();
			if (n.Length == 0) { SetStatus("Type a name first.", true); return; }
			if (FileNames.IslandProblem(n) != null) { SetStatus(FileNames.IslandProblem(n), true); return; }
			bool exists = File.Exists(IslandSpawner.PathFor(n));
			if (exists && n != DynamicIslands.currentIslandName && pendingOverwrite != n)
			{
				pendingOverwrite = n;
				SetStatus("'" + n + "' already exists. Press Save again to overwrite it.", true);
				return;
			}
			if (DynamicIslands.SaveIsland(n, true)) Close();
			else SetStatus("Saving failed - see the console (F10).", true);
		}

		void OnExport()
		{
			string n = nameField.text.Trim();
			if (n.Length == 0 || !File.Exists(IslandSpawner.PathFor(n))) { SetStatus("Pick a saved island to share (save the one you're editing first).", true); return; }
			Close();
			LibraryExportWindow.OpenIsland(n);
		}

		void OnLoad()
		{
			string n = nameField.text.Trim();
			if (n.Length == 0) { SetStatus("Pick an island to open.", true); return; }
			if (!File.Exists(IslandSpawner.PathFor(n))) { SetStatus("There is no saved island called '" + n + "'.", true); return; }
			if (DynamicIslands.LoadIsland(n)) Close();
			else SetStatus("Opening failed - see the console (F10).", true);
		}

		/// <summary>
		/// Rename (ROADMAP E9): the first press keeps the picked island, the second renames it to the name typed then -
		/// with the saved worlds, plans and island rules that name it (IslandRename).
		/// </summary>
		void OnRename()
		{
			string n = nameField.text.Trim();
			if (renameFrom == null || !File.Exists(IslandSpawner.PathFor(renameFrom)))
			{
				if (n.Length == 0 || !File.Exists(IslandSpawner.PathFor(n))) { SetStatus("Pick an island to rename.", true); return; }
				if (IslandRename.FromProblem(n) != null) { SetStatus(IslandRename.FromProblem(n), true); return; }
				renameFrom = n;
				SetStatus("Type the new name for '" + n + "' and press Rename again.", false);
				nameField.ActivateInputField();
				return;
			}
			if (n.Equals(renameFrom)) { SetStatus("Type the new name for '" + renameFrom + "' and press Rename again.", false); return; }
			string problem = IslandRename.Problem(renameFrom, n);
			if (problem != null) { SetStatus(problem, true); return; }
			try
			{
				List<string> also = IslandRename.Rename(renameFrom, n);
				DynamicIslands.Notify("Renamed '" + renameFrom + "' to '" + n + "'");
				SetStatus("Renamed '" + renameFrom + "' to '" + n + "'." + (also.Count > 0 ? " Changed too: " + string.Join("; ", also.ToArray()) + "." : ""), false);
				renameFrom = null;
				Refresh();
			}
			catch (Exception ex) { SetStatus(SafeFile.InUse(ex) ? "'" + renameFrom + "' is in use by another program - close it there and rename again (nothing was changed)." : "Could not rename: " + ex.Message, true); }
		}

		void OnDelete()
		{
			string n = nameField.text.Trim();
			string path = IslandSpawner.PathFor(n);
			if (n.Length == 0 || !File.Exists(path)) { SetStatus("Pick an island to delete.", true); return; }
			if (pendingDelete != n)
			{
				pendingDelete = n;
				// (what would miss it: saved worlds that have it, plans that bring it)
				List<string> worlds = LibraryPack.WorldsUsing(n);
				List<string> plans = PlansNaming(n), islands = IslandsNaming(n);
				string uses = (worlds.Count > 0 ? " Saved worlds that have it: " + string.Join(", ", worlds.Take(4).ToArray()) + (worlds.Count > 4 ? " and " + (worlds.Count - 4) + " more" : "") + " - it goes missing there." : "") +
					(plans.Count > 0 ? " Plans that bring it: " + string.Join(", ", plans.Take(4).ToArray()) + "." : "") +
					(islands.Count > 0 ? " Islands whose rules bring it: " + string.Join(", ", islands.Take(4).ToArray()) + "." : "");
				SetStatus("Delete '" + n + "'? Press Delete again." + uses + " (It is moved to Mods\\DynamicIslands\\" + DeletedFolderName + ", where you can get it back.)", true);
				return;
			}
			try
			{
				MoveToDeleted(n);
				pendingDelete = null;
				DynamicIslands.Notify("Deleted island '" + n + "' (kept in " + DeletedFolderName + " until you remove it there)");
				SetStatus("Deleted '" + n + "'.", false);
				Refresh();
			}
			catch (Exception ex) { SetStatus(SafeFile.InUse(ex) ? "'" + n + "' is in use by another program - close it there and delete again (nothing was moved)." : "Could not delete: " + ex.Message, true); }
		}

		public const string DeletedFolderName = "deleted";

		/// <summary>
		/// An island deleted in the editor is moved aside, not erased: Mods\DynamicIslands\deleted\&lt;name&gt;.island (an older
		/// deleted one of the same name gets a date). Put it back by moving the file into Mods\DynamicIslands.
		/// </summary>
		public static void MoveToDeleted(string name)
		{
			string path = IslandSpawner.PathFor(name);
			string folder = Path.Combine(DynamicIslands.assetpath, DeletedFolderName);
			Directory.CreateDirectory(folder);
			string to = Path.Combine(folder, Path.GetFileName(path));
			if (File.Exists(to)) File.Move(to, Path.Combine(folder, name + " " + File.GetLastWriteTime(to).ToString("yyyy-MM-dd HHmmss") + IslandFile.Extension));
			File.Move(path, to);
			IslandSummaries.Forget(name);
		}

		/// <summary>
		/// A world's island whose file is gone (tidied up, deleted, or a gen- file removed by hand): a copy in the deleted
		/// folder (or its subfolders) is put back. With the world's recorded hash only a copy with that content counts;
		/// without one, the newest copy of that name. The copy in the deleted folder stays. True if the file is back.
		/// </summary>
		public static bool RestoreFromDeleted(string name, string hash)
		{
			try
			{
				string path = IslandSpawner.PathFor(name);
				string folder = Path.Combine(DynamicIslands.assetpath, DeletedFolderName);
				if (File.Exists(path) || !Directory.Exists(folder)) return false;
				string ext = Path.GetExtension(path);
				bool known = IslandNetwork.IsHash(hash);
				var candidates = Directory.GetFiles(folder, "*" + ext, SearchOption.AllDirectories).Where(f =>
				{
					string n = Path.GetFileNameWithoutExtension(f);
					if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
					if (n.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)) return true; // (an older one with a date)
					return known && string.Equals(n, IslandNetwork.DownloadName(name, hash), StringComparison.OrdinalIgnoreCase);
				}).OrderByDescending(f => File.GetLastWriteTimeUtc(f)).ToList();
				string pick = known ? candidates.FirstOrDefault(f => IslandNetwork.Hash(File.ReadAllBytes(f)) == hash) : candidates.FirstOrDefault();
				if (pick == null) return false;
				File.Copy(pick, path);
				IslandSummaries.Forget(name);
				Debug.Log("[CUSTOM ISLANDS] The world's island '" + name + "' was missing: put back from " + pick);
				return true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not put '" + name + "' back from the deleted folder: " + e.Message); return false; }
		}

		/// <summary>Saved plans with a rule that brings this island by name (island: or oneof:).</summary>
		public static List<string> PlansNaming(string island)
		{
			var result = new List<string>();
			foreach (string p in WorldPlan.All().Where(x => !WorldPlan.IsBuiltIn(x)))
			{
				WorldPlan plan = WorldPlan.Load(p);
				if (plan != null && plan.Rules.Any(r => LibraryPack.RuleNames(r, island))) result.Add(p);
			}
			return result;
		}

		/// <summary>Other saved islands whose own rules (a quest done, a zone, a visit...) bring this island by name.</summary>
		public static List<string> IslandsNaming(string island)
		{
			var result = new List<string>();
			try
			{
				foreach (string file in Directory.GetFiles(DynamicIslands.assetpath, "*" + IslandFile.Extension))
				{
					string other = Path.GetFileNameWithoutExtension(file);
					if (other.Equals(island, StringComparison.OrdinalIgnoreCase) || IslandNetwork.IsDownloadName(other)) continue;
					if (IslandCache.RulesOf(other).Any(r => LibraryPack.RuleNames(r, island))) result.Add(other);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Looking for islands that bring '" + island + "': " + e.Message); }
			IslandCache.SaveRules();
			return result;
		}

		void SetStatus(string message, bool warning)
		{
			status.text = message;
			status.color = warning ? new Color(1f, 0.72f, 0.45f) : UIKit.TextColor;
		}
	}
}
