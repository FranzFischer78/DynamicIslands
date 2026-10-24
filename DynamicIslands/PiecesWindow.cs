using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>Moving the files of groups and stamps (ROADMAP T4): renamed in place, deleted to the "deleted" folder.</summary>
	public static class PiecesFiles
	{
		/// <summary>Moves a file to a new name (a change of case only goes by a temporary name: Windows sees the same file).</summary>
		public static void Move(string from, string to)
		{
			if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
			{
				File.Move(from, to + ".renaming");
				File.Move(to + ".renaming", to);
			}
			else File.Move(from, to);
		}

		/// <summary>Moves a file to Mods\DynamicIslands\deleted\&lt;kind&gt; (an older one of the same name gets a date).</summary>
		public static void MoveToDeleted(string path, string kind)
		{
			string folder = Path.Combine(Path.Combine(DynamicIslands.assetpath, IslandFilesWindow.DeletedFolderName), kind);
			Directory.CreateDirectory(folder);
			string to = Path.Combine(folder, Path.GetFileName(path));
			if (File.Exists(to))
			{
				// (a dated one of the same second can be there too - the same file removed twice: it stopped the Remove)
				string dated = Path.GetFileNameWithoutExtension(path) + " " + File.GetLastWriteTime(to).ToString("yyyy-MM-dd HHmmss");
				string older = Path.Combine(folder, dated + Path.GetExtension(path));
				for (int i = 2; File.Exists(older); i++) older = Path.Combine(folder, dated + " " + i + Path.GetExtension(path));
				File.Move(to, older);
			}
			File.Move(path, to);
		}
	}

	/// <summary>
	/// "My groups and stamps" (ROADMAP T4): the saved object groups and terrain stamps, each to rename or delete (groups
	/// could only be deleted with the DeleteGroup command, stamps not at all). Opened by "Manage..." beside Save as group
	/// (Objects tab) and Save stamp (Terrain tab).
	/// </summary>
	public class PiecesWindow : MonoBehaviour
	{
		static PiecesWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		/// <summary>Tests: the window, its name field and its status line.</summary>
		internal static GameObject Root { get { return instance != null ? instance.gameObject : null; } }
		internal static InputField NameField { get { return instance != null ? instance.nameField : null; } }
		internal static string StatusText { get { return instance != null ? instance.status.text : ""; } }
		internal static string Picked { get { return instance != null ? instance.picked : null; } }
		internal static void Pick(string name) { if (instance != null) instance.OnPick(name); }

		bool stamps;
		string picked, pendingDelete;
		InputField nameField;
		RectTransform listContent;
		Text status;
		Button[] tabs;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("PiecesWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<PiecesWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		/// <summary>Opens on the groups (false) or the stamps (true).</summary>
		public static void Open(bool showStamps)
		{
			if (instance == null) return;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.ShowKind(showStamps);
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		void Update()
		{
			EditorInput.IsTyping = nameField != null && nameField.isFocused;
			if (EscKey.Down(this)) Close();
			else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && picked != null && nameField.text.Trim() != picked) OnRename();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "MY GROUPS AND STAMPS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			tabs = UIKit.Tabs(panel, new[] { "Groups", "Stamps" }, new[] { "Object groups you saved (Objects tab > Save as group...): placed from \"My groups\" on the object list", "Terrain stamps you saved (Terrain tab > Save stamp...); the built-in ones can't be changed" }, i => ShowKind(i == 1), 30f, 14);

			RectTransform listBox = UIKit.Rect("ListBox", panel);
			UIKit.Size(listBox.gameObject, -1, 230);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect sr;
			listContent = UIKit.ScrollList(listBox, out sr, 3f);
			UIKit.Stretch((RectTransform)sr.transform, 4, 4, 4, 4);

			RectTransform name = UIKit.Group(panel, "Name");
			nameField = UIKit.Field(name, "pick one in the list, then type its new name", "", 30f, "The picked one's name: type a new name and press Rename (Enter)");
			nameField.characterLimit = 64;
			nameField.onValueChanged.AddListener(v => pendingDelete = null);

			status = UIKit.Label(panel, "", 13, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Italic, "Status");
			UIKit.Size(status.gameObject, -1, 34);
			status.horizontalOverflow = HorizontalWrapMode.Wrap;

			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Button(buttons, "Rename", OnRename, "Give the picked one the name typed above (Enter)", -1, 34, 15);
			Button del = UIKit.Button(buttons, "Delete", OnDelete, "Delete the picked one (asks first; it is moved to Mods\\DynamicIslands\\deleted, where you can get it back)", -1, 34, 15);
			UIKit.DangerButton(del);
			UIKit.Button(buttons, "Close", Close, "Close (Esc)", -1, 34, 15);
		}

		void ShowKind(bool showStamps)
		{
			stamps = showStamps;
			for (int i = 0; i < tabs.Length; i++) UIKit.SetActive(tabs[i], i == (stamps ? 1 : 0));
			picked = pendingDelete = null;
			nameField.text = "";
			Refresh();
			SetStatus(Names().Count == 0
				? (stamps ? "No stamps of your own yet: Terrain tab > Save stamp... keeps the land under the brush." : "No groups yet: select objects, then Objects tab > Save as group...")
				: "Click one to pick it, then Rename or Delete.", false);
		}

		List<string> Names()
		{
			return stamps ? TerrainStamps.All.Where(s => !s.BuiltIn).Select(s => s.Name).ToList() : GroupLibrary.Saved().ToList();
		}

		void Refresh()
		{
			foreach (Transform child in listContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			foreach (string n in Names())
			{
				string itemName = n;
				Button entry = UIKit.Button(listContent, n, () => OnPick(itemName), null, -1, 30, 14);
				entry.name = "Piece_" + n;
				Text label = UIKit.LabelOf(entry);
				label.alignment = TextAnchor.MiddleLeft;
				label.rectTransform.offsetMin = new Vector2(10, 0);
				UIKit.SetActive(entry, n == picked);
			}
		}

		void OnPick(string n)
		{
			picked = n;
			pendingDelete = null;
			nameField.text = n;
			nameField.ActivateInputField();
			Refresh();
			SetStatus("'" + n + "': type a new name and press Rename, or press Delete.", false);
		}

		bool Exists(string n) { return stamps ? TerrainStamps.Exists(n) : GroupLibrary.Exists(n); }

		void OnRename()
		{
			if (picked == null || !Exists(picked)) { SetStatus("Pick one in the list first.", true); return; }
			string to = nameField.text.Trim();
			if (to.Length == 0 || to == picked) { SetStatus("Type the new name for '" + picked + "' first.", true); return; }
			string problem = FileNames.Problem(to);
			if (problem != null) { SetStatus(problem, true); return; }
			if (stamps && TerrainStamps.All.Any(s => s.BuiltIn && s.Name.Equals(to, StringComparison.OrdinalIgnoreCase))) { SetStatus("'" + to + "' is one of the built-in stamps - pick another name.", true); return; }
			if (!to.Equals(picked, StringComparison.OrdinalIgnoreCase) && Exists(to)) { SetStatus("There is " + (stamps ? "a stamp" : "a group") + " called '" + to + "' already.", true); return; }
			string from = picked;
			try
			{
				if (stamps) { TerrainStamps.Rename(from, to); EditorUI.RefreshStamps(); }
				else { GroupLibrary.RenameFile(from, to); DynamicIslands.instance.StartCoroutine(GroupLibrary.Register(to)); }
			}
			catch (Exception e) { SetStatus(SafeFile.InUse(e) ? "'" + from + "' is in use by another program - close it there and try again." : "Could not rename: " + e.Message, true); return; }
			picked = to;
			Refresh();
			SetStatus("Renamed '" + from + "' to '" + to + "'.", false);
			DynamicIslands.Notify("Renamed " + (stamps ? "the stamp" : "the group") + " '" + from + "' to '" + to + "'");
		}

		void OnDelete()
		{
			if (picked == null || !Exists(picked)) { SetStatus("Pick one in the list first.", true); return; }
			if (pendingDelete != picked)
			{
				pendingDelete = picked;
				SetStatus("Delete " + (stamps ? "the stamp" : "the group") + " '" + picked + "'? Press Delete again. (It is moved to Mods\\DynamicIslands\\" + IslandFilesWindow.DeletedFolderName + "\\" + (stamps ? "stamps" : "groups") + ", where you can get it back.)", true);
				return;
			}
			string n = picked;
			try
			{
				if (stamps) { TerrainStamps.MoveToDeleted(n); EditorUI.RefreshStamps(); }
				else GroupLibrary.MoveToDeleted(n);
			}
			catch (Exception e) { SetStatus(SafeFile.InUse(e) ? "'" + n + "' is in use by another program - close it there and try again." : "Could not delete: " + e.Message, true); return; }
			picked = pendingDelete = null;
			nameField.text = "";
			Refresh();
			SetStatus("Deleted '" + n + "'.", false);
			DynamicIslands.Notify("Deleted " + (stamps ? "the stamp" : "the group") + " '" + n + "' (kept in " + IslandFilesWindow.DeletedFolderName + " until you remove it there)");
		}

		void SetStatus(string message, bool warning)
		{
			status.text = message;
			status.color = warning ? new Color(1f, 0.72f, 0.45f) : UIKit.TextColor;
		}
	}
}
