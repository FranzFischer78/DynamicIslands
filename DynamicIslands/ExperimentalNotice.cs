using System;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The main menu's "EXPERIMENTAL ALPHA RELEASE" box: this is the mod's first release, so new players are told that things are
	/// likely to change, some systems might be unstable and progress isn't guaranteed to be saved. It starts on the right
	/// half of Raft's main menu, clear of the menu buttons and the New Game box, and can be dragged anywhere (the place is
	/// remembered). Its buttons lead to help: the Discord server, the guide (PDF) and Report a problem. "Got it" folds it
	/// into a slim bar with those buttons (Show opens it again); the X closes it altogether (Franz, 2026-10-17), and then
	/// only a small "Custom Islands: help" link in the menu's bottom right corner is left, which opens it again. Both are
	/// remembered for this version of the mod only (Mods\DynamicIslands\notice.txt), so a new version shows the whole box again.
	/// </summary>
	public static class ExperimentalNotice
	{
		public const string PanelName = "CustomIslands_ExperimentalNotice";
		public const string Header = "EXPERIMENTAL ALPHA RELEASE";
		public static readonly string[] Points =
		{
			"Things are likely to change.",
			"Some systems might be unstable.",
			"We do not guarantee that progress is always saved.",
		};
		const string FileName = "notice.txt";
		/// <summary>Where the box starts: its top middle, as a part of the menu's width and height (then moved right, or up,
		/// as far as it takes to keep clear of the New Game box on narrower screens).</summary>
		static readonly Vector2 StartSpot = new Vector2(0.64f, 0.8f);
		/// <summary>The player dragged it (or a saved place was used): it stays there instead of its start.</summary>
		static bool dragged;
		const float Width = 600f; // (the button row needs ~550 inside the 18+18 padding)

		static RectTransform panel, body;
		static Button toggle, discordButton, guideButton, reportButton, closeButton, helpLink;
		/// <summary>The X was pressed (for this version): the box is gone, the help link shows instead.</summary>
		static bool closed;

		public static RectTransform Panel { get { return panel; } }
		public static bool Folded { get { return body != null && !body.gameObject.activeSelf; } }
		/// <summary>Out of the way: another window of the main menu is open (the box is never shown over one).</summary>
		public static bool Hidden { get { return panel != null && !panel.gameObject.activeSelf; } }
		public static Button Toggle { get { return toggle; } }
		public static Button DiscordButton { get { return discordButton; } }
		public static Button GuideButton { get { return guideButton; } }
		public static Button ReportButton { get { return reportButton; } }
		public static Button CloseButton { get { return closeButton; } }
		/// <summary>The small link left on the menu while the box is closed: opens the box again.</summary>
		public static Button HelpLink { get { return helpLink; } }
		public const string HelpLinkName = "CustomIslands_HelpLink";
		/// <summary>The X closed the box (this version): only the help link is on the menu.</summary>
		public static bool Closed { get { return closed; } }
		/// <summary>Whether the X was pressed for this version.</summary>
		public static bool ClosedThisVersion { get { return Read("closed") == Version; } }

		/// <summary>The mod's version (modinfo.json), for the box's line and for remembering "Got it".</summary>
		static string version;
		public static string Version
		{
			get
			{
				// (the mod's own modinfo.json, shipped in the .rmod - read once: the library asks for every entry it lists)
				if (version != null) return version;
				try
				{
					byte[] b = RaftIslands.ModFile("modinfo.json");
					System.Text.RegularExpressions.Match m = b != null ? System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.UTF8.GetString(b), "\"version\"\\s*:\\s*\"([^\"]+)\"") : null;
					if (m != null && m.Success) version = m.Groups[1].Value;
					return version ?? "?";
				}
				catch { return "?"; }
			}
		}

		static string FilePath { get { return Path.Combine(DynamicIslands.assetpath, FileName); } }

		/// <summary>notice.txt's value for a key (seen = the version "Got it" was pressed for, place = where the box was dragged (from the middle of the menu)).</summary>
		static string Read(string key)
		{
			try
			{
				if (!File.Exists(FilePath)) return null;
				string line = File.ReadAllLines(FilePath).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(key + "="));
				return line != null ? line.Substring(key.Length + 1) : null;
			}
			catch { return null; }
		}

		static void Write(string key, string value)
		{
			try
			{
				Directory.CreateDirectory(DynamicIslands.assetpath);
				var lines = File.Exists(FilePath) ? File.ReadAllLines(FilePath).Where(l => l.Trim().Length > 0 && !l.Trim().StartsWith(key + "=")).ToList() : new System.Collections.Generic.List<string>();
				if (value != null) lines.Add(key + "=" + value);
				if (lines.Count == 0) { if (File.Exists(FilePath)) File.Delete(FilePath); }
				else SafeFile.WriteAllText(FilePath, string.Join("\n", lines.ToArray()) + "\n");
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not write " + FilePath + ": " + e.Message); }
		}

		/// <summary>Whether "Got it" was pressed for this version.</summary>
		public static bool SeenThisVersion { get { return Read("seen") == Version; } }

		static void Remember(bool seen) { Write("seen", seen ? Version : null); }

		/// <summary>The main menu appeared (DynamicIslands.HookUI): the box on its canvas, once.</summary>
		public static void Show(Transform mainMenuCanvas)
		{
			if (mainMenuCanvas == null || mainMenuCanvas.Find(PanelName) != null) return;
			try { Build(mainMenuCanvas); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] The experimental release box: " + e); }
		}

		static void Build(Transform canvas)
		{
			panel = UIKit.Panel(canvas, PanelName, new RectOffset(18, 18, 12, 14), 8f);
			panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f); // (positions from the middle of the menu)
			panel.pivot = new Vector2(0.5f, 1f);
			panel.anchoredPosition = Vector2.zero;
			panel.sizeDelta = new Vector2(Width, 0f);
			panel.gameObject.AddComponent<Dragger>();
			UIKit.Hint(panel.gameObject, "Drag the box to move it");

			// (the header alone on its line, as large as fits: Raft's title font is wide)
			Text title = UIKit.Label(panel, Header, 30, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(title);
			title.horizontalOverflow = HorizontalWrapMode.Wrap;
			title.resizeTextForBestFit = true;
			title.resizeTextMinSize = 12;
			title.resizeTextMaxSize = title.fontSize;
			UIKit.Size(title.gameObject, -1, 36);

			body = UIKit.Rect("Body", panel);
			UIKit.Vertical(body.gameObject, 5f, new RectOffset(0, 0, 0, 0));
			Text intro = UIKit.Label(body, "This is the first release of Custom Islands (version " + Version + "), an early alpha. Please keep in mind:", 16, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Intro");
			intro.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(intro.gameObject, -1, 42); // (two lines)
			for (int i = 0; i < Points.Length; i++)
			{
				Text t = UIKit.Label(body, "•  " + Points[i], 17, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Point" + i);
				t.horizontalOverflow = HorizontalWrapMode.Wrap;
				UIKit.Size(t.gameObject, -1, 24);
			}
			Text advice = UIKit.Label(body, "Back up the worlds you care about (Raft keeps them in AppData\\LocalLow\\Redbeet Interactive\\Raft\\User). " +
				"New here? The guide shows you around. Found a problem, or have a question? Report it, or ask on the Custom Islands Discord.", 13, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, "Advice");
			advice.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(advice.gameObject, -1, 54);

			// (the help buttons stay on the folded bar too)
			RectTransform buttons = UIKit.Row(panel, 32f, 6f, "Buttons");
			discordButton = UIKit.Button(buttons, "Discord", () => HelpLinks.Open(HelpLinks.Discord), "The Custom Islands Discord server: questions, islands, news", 104, 32f, 14);
			discordButton.name = "Discord";
			guideButton = UIKit.Button(buttons, "Guide (PDF)", HelpLinks.OpenGuide, "The illustrated guide: installing, sailing, the editor, world plans, playing together", 124, 32f, 14);
			guideButton.name = "Guide";
			reportButton = UIKit.Button(buttons, "Report a problem", InfoWindow.OpenReport, "How to report a problem, on GitHub or Discord", 164, 32f, 14);
			reportButton.name = "Report";
			UIKit.Label(buttons, "", 12);
			toggle = UIKit.Button(buttons, "", Flip, null, 92, 32f, 14);
			toggle.name = "Toggle";
			UIKit.Primary(toggle);
			closeButton = UIKit.Button(buttons, "X", Close, "Close this box (until the next version of Custom Islands). The help link in the bottom right corner opens it again", 32, 32f, 14);
			closeButton.name = "Close";
			BuildHelpLink(canvas);
			SetFolded(SeenThisVersion);
			SetClosed(ClosedThisVersion);
			dragged = false;
			PlaceStart();
			PlaceSaved();
			// (on the menu's canvas, not the box: it keeps looking while the box is hidden)
			if (canvas.GetComponent<Watcher>() == null) canvas.gameObject.AddComponent<Watcher>();
			Debug.Log("[CUSTOM ISLANDS] Alpha notice shown" + (closed ? " (closed for " + Version + ": the help link only)" : Folded ? " (folded: seen for " + Version + ")" : ""));
		}

		/// <summary>The small "Custom Islands: help" link in the menu's bottom right corner, shown while the box is closed.</summary>
		static void BuildHelpLink(Transform canvas)
		{
			Transform old = canvas.Find(HelpLinkName);
			if (old != null) UnityEngine.Object.Destroy(old.gameObject);
			helpLink = UIKit.Button(canvas, "Custom Islands: help", Reopen, "The alpha notice again: the Discord, the guide (PDF) and Report a problem", 170, 26f, 12);
			helpLink.name = HelpLinkName;
			var r = (RectTransform)helpLink.transform;
			r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
			r.anchoredPosition = new Vector2(-12f, 12f);
			r.sizeDelta = new Vector2(170f, 26f);
			helpLink.gameObject.SetActive(false);
		}

		/// <summary>The X: the box closes and stays closed for this version; the help link opens it again.</summary>
		public static void Close()
		{
			SetClosed(true);
			Write("closed", Version);
		}

		/// <summary>The help link: the box again (folded as it was), and it stays open on the next start.</summary>
		public static void Reopen()
		{
			SetClosed(false);
			Write("closed", null);
		}

		static void SetClosed(bool c)
		{
			closed = c;
			if (panel == null) return;
			bool free = OtherWindow() == null;
			panel.gameObject.SetActive(!c && free);
			if (helpLink != null) helpLink.gameObject.SetActive(c && free);
			if (!c) KeepOnScreen();
		}

		/// <summary>"Got it" folds the box and remembers it for this version; the folded bar's "Show" opens it again.</summary>
		public static void Flip()
		{
			bool fold = !Folded;
			SetFolded(fold);
			Remember(fold);
		}

		static void SetFolded(bool folded)
		{
			if (body == null || toggle == null) return;
			body.gameObject.SetActive(!folded);
			UIKit.LabelOf(toggle).text = folded ? "Show" : "Got it";
			LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
			KeepOnScreen();
		}

		#region Out of the way of other windows

		/// <summary>
		/// Another window of the main menu is open: one of Raft's (every MenuBox - New Game, Load World, Settings, Join,
		/// Credits, Delete, Exit...) or the mod's (World settings, the island list, the island library, export and import,
		/// an info box, a list or a prompt, the editor's loading box). What it is, or null.
		/// </summary>
		public static string OtherWindow()
		{
			foreach (MenuBox b in boxes) if (b != null && b.IsOpen) return b.GetType().Name;
			if (WorldSettingsWindow.IsOpen) return "World settings";
			if (IslandPickerWindow.IsOpen) return "Choose islands";
			if (LibraryWindow.IsOpen) return "Island library";
			if (LibraryExportWindow.IsOpen) return "Export";
			if (LibraryImportWindow.IsOpen) return "Import";
			if (InfoWindow.IsOpen) return "Info box";
			if (ChoiceWindow.IsOpen) return "List";
			if (TextPromptWindow.IsOpen) return "Prompt";
			if (EditorLoadingBox.Showing) return "Editor loading";
			return null;
		}

		static MenuBox[] boxes = new MenuBox[0];
		static float nextLook;

		/// <summary>Every frame on the main menu: the box hides while another window is open, and comes back after.</summary>
		class Watcher : MonoBehaviour
		{
			void Update()
			{
				if (panel == null) return;
				// (Raft's boxes found again now and then: closed ones are inactive, so inactive ones count. Every object is
				// searched: once they are found, every 15 s - or at once when one is gone)
				if (Time.unscaledTime >= nextLook || Array.Exists(boxes, b => b == null))
				{
					boxes = Resources.FindObjectsOfTypeAll<MenuBox>().Where(b => b != null && b.gameObject.scene.IsValid()).ToArray();
					nextLook = Time.unscaledTime + (boxes.Length > 0 ? 15f : 1f);
				}
				bool free = OtherWindow() == null, show = free && !closed;
				if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
				if (helpLink != null && helpLink.gameObject.activeSelf != (free && closed)) helpLink.gameObject.SetActive(free && closed);
			}
		}

		#endregion

		#region Moving it

		/// <summary>Moves the box (a drag, or tests), kept on the screen, and remembers where.</summary>
		public static void MoveBy(Vector2 delta)
		{
			if (panel == null) return;
			dragged = true;
			panel.anchoredPosition += delta;
			KeepOnScreen();
			Vector2 p = panel.anchoredPosition;
			Write("place", p.x.ToString("0", CultureInfo.InvariantCulture) + "," + p.y.ToString("0", CultureInfo.InvariantCulture));
		}

		/// <summary>Back where it starts (forgets the dragged place).</summary>
		public static void ResetPlace()
		{
			if (panel == null) return;
			dragged = false;
			PlaceStart();
			Write("place", null);
		}

		/// <summary>Where the player dragged the box ("x,y", null = where it starts): tests keep it and put it back.</summary>
		public static string SavedPlace { get { return Read("place"); } set { Write("place", value); } }

		/// <summary>The box where it starts, then moved to the saved place (if any).</summary>
		public static void ApplySavedPlace()
		{
			if (panel == null) return;
			dragged = false;
			PlaceStart();
			PlaceSaved();
		}

		static void PlaceSaved()
		{
			string pos = Read("place");
			if (pos == null) return;
			string[] xy = pos.Split(',');
			float x, y;
			if (xy.Length == 2 && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
			{
				dragged = true;
				panel.anchoredPosition = new Vector2(x, y);
				KeepOnScreen();
			}
		}

		/// <summary>Where the box starts: right of the middle, clear of the New Game box's controls (measured, since Raft
		/// scales its menu with the screen's height); where there is no room beside it, at the top of the menu.</summary>
		static void PlaceStart()
		{
			var area = panel != null ? panel.parent as RectTransform : null;
			if (area == null) return;
			Canvas.ForceUpdateCanvases();
			Rect a = area.rect, r = panel.rect;
			float half = r.width / 2f;
			float x = a.xMin + a.width * StartSpot.x, top = a.yMin + a.height * StartSpot.y;
			Transform box = area.Find("New Game Box");
			float boxRight = float.MinValue;
			if (box != null)
			{
				var c = new Vector3[4];
				foreach (Selectable s in box.GetComponentsInChildren<Selectable>(true))
				{
					((RectTransform)s.transform).GetWorldCorners(c);
					for (int i = 0; i < 4; i++) boxRight = Mathf.Max(boxRight, area.InverseTransformPoint(c[i]).x);
				}
			}
			if (boxRight > float.MinValue && x - half < boxRight + 20f)
			{
				x = Mathf.Min(boxRight + 20f + half, a.xMax - 8f - half);
				if (x - half < boxRight + 20f) top = a.yMax - 60f; // (no room beside it: above it, at the top right)
			}
			panel.anchoredPosition = new Vector2(x, top) - a.center;
			KeepOnScreen();
			lastScreen = new Vector2(Screen.width, Screen.height);
			lastHeight = r.height;
		}

		static Vector2 lastScreen;
		static float lastHeight;

		/// <summary>The whole box inside the menu (another screen size, folding, a drag too far).</summary>
		public static void KeepOnScreen()
		{
			var area = panel != null ? panel.parent as RectTransform : null;
			if (area == null) return;
			Canvas.ForceUpdateCanvases();
			Rect a = area.rect, r = panel.rect;
			// (the panel's corners in the parent's space: anchor point + position + its rect)
			Vector2 anchorPoint = new Vector2(a.xMin + a.width * panel.anchorMin.x, a.yMin + a.height * panel.anchorMin.y);
			Vector2 p = panel.anchoredPosition;
			float left = anchorPoint.x + p.x + r.xMin, right = anchorPoint.x + p.x + r.xMax;
			float bottom = anchorPoint.y + p.y + r.yMin, top = anchorPoint.y + p.y + r.yMax;
			// (too far right or up: back in; then too far left or down wins, so the header and buttons are never cut off)
			if (right > a.xMax - 4f) p.x -= right - (a.xMax - 4f);
			if (left + p.x - panel.anchoredPosition.x < a.xMin + 4f) p.x = panel.anchoredPosition.x + a.xMin + 4f - left;
			if (top > a.yMax - 4f) p.y -= top - (a.yMax - 4f);
			if (bottom + p.y - panel.anchoredPosition.y < a.yMin + 4f && top + a.yMin + 4f - bottom <= a.yMax) p.y = panel.anchoredPosition.y + a.yMin + 4f - bottom;
			panel.anchoredPosition = p;
		}

		/// <summary>Drags the box by its surface (the buttons still click).</summary>
		class Dragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
		{
			public void OnBeginDrag(PointerEventData e) { dragged = true; }

			// (another screen size, or folded: its start again, unless the player put it somewhere)
			void LateUpdate()
			{
				if (dragged || panel == null) return;
				if (lastScreen != new Vector2(Screen.width, Screen.height) || Mathf.Abs(lastHeight - panel.rect.height) > 1f) PlaceStart();
			}

			public void OnDrag(PointerEventData e)
			{
				Canvas c = GetComponentInParent<Canvas>();
				float scale = c != null && c.rootCanvas != null ? c.rootCanvas.scaleFactor : 1f;
				panel.anchoredPosition += e.delta / Mathf.Max(0.01f, scale);
			}

			public void OnEndDrag(PointerEventData e) { MoveBy(Vector2.zero); }
		}

		#endregion
	}
}
