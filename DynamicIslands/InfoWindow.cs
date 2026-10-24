using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A message box with a title, a text (it scrolls when it is long) and a row of buttons, above every window of the
	/// mod, in the main menu, the editor or a world: "Report a problem" (the main menu's alpha box) and the Help of the
	/// editor's windows. Esc or Close closes it.
	/// </summary>
	public class InfoWindow : MonoBehaviour
	{
		public class Choice
		{
			public string Label, Hint;
			public Action Click;
			public bool Primary;
			public Choice(string label, Action click, string hint = null, bool primary = false) { Label = label; Click = click; Hint = hint; Primary = primary; }
		}

		public const string CanvasName = "CustomIslands_InfoWindow";
		static Canvas canvas;
		static InfoWindow instance;
		static Text titleText, bodyText, statusText;
		static RectTransform buttonRow, panel;
		const float PanelWidth = 760f, PanelPadding = 36f, ButtonSpacing = 8f;
		static ScrollRect scroll;
		static readonly List<Button> buttons = new List<Button>();

		public static bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }
		public static string Title { get { return titleText != null ? titleText.text : ""; } }
		public static string Body { get { return bodyText != null ? bodyText.text : ""; } }
		public static string Status { get { return statusText != null ? statusText.text : ""; } }
		public static RectTransform Root { get { return canvas != null ? (RectTransform)canvas.transform : null; } }
		/// <summary>Tests: the buttons shown now.</summary>
		public static Button ButtonNamed(string label) { return buttons.FirstOrDefault(b => b != null && UIKit.LabelOf(b).text == label); }
		/// <summary>Tests: the buttons that stick out of the box (their labels; empty when all fit).</summary>
		public static List<string> ButtonsOutside()
		{
			var outside = new List<string>();
			if (panel == null) return outside;
			var corners = new Vector3[4];
			panel.GetWorldCorners(corners);
			float left = corners[0].x, right = corners[2].x;
			foreach (Button b in buttons.Where(b => b != null))
			{
				var bc = new Vector3[4];
				((RectTransform)b.transform).GetWorldCorners(bc);
				if (bc[0].x < left - 0.5f || bc[2].x > right + 0.5f) outside.Add(UIKit.LabelOf(b).text);
			}
			return outside;
		}
		public static List<string> ButtonLabels { get { return buttons.Where(b => b != null).Select(b => UIKit.LabelOf(b).text).ToList(); } }

		static void Build()
		{
			canvas = UIKit.CreateCanvas(CanvasName, 890); // (above the island library (880), under the ? popups (900))
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<InfoWindow>();
			RectTransform root = (RectTransform)canvas.transform;
			RectTransform dim = UIKit.Rect("Dim", root);
			UIKit.Stretch(dim);
			dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
			panel = UIKit.Panel(root, "Panel", new RectOffset(18, 18, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, 0f));
			titleText = UIKit.Label(panel, "", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.Size(titleText.gameObject, -1, 32);

			RectTransform box = UIKit.Rect("TextBox", panel);
			UIKit.Size(box.gameObject, -1, 400);
			UIKit.Background(box.gameObject, new Color(0.231f, 0.129f, 0.059f, 0.3f), 6);
			RectTransform content = UIKit.ScrollList(box, out scroll, 4f);
			UIKit.Stretch((RectTransform)scroll.transform, 10, 4, 8, 8);
			bodyText = UIKit.Label(content, "", 15, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Body");
			bodyText.lineSpacing = 1.1f;

			statusText = UIKit.Label(panel, "", 13, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Italic, "Status");
			UIKit.Size(statusText.gameObject, -1, 18);
			buttonRow = UIKit.Row(panel, 34f, 8f, "Buttons");
			canvas.gameObject.SetActive(false);
		}

		/// <summary>Shows the box (again with new content when it is open). The last choice is usually Close.</summary>
		public static void Open(string title, string text, params Choice[] choices)
		{
			if (canvas == null) Build();
			Closed = null;
			titleText.text = title.ToUpperInvariant();
			bodyText.text = text;
			statusText.text = "";
			foreach (Transform child in buttonRow) Destroy(child.gameObject);
			buttons.Clear();
			UIKit.Size(UIKit.Label(buttonRow, "", 12).gameObject, -1, -1, 1); // (the buttons on the right)
			float row = 0f;
			foreach (Choice c in choices)
			{
				Choice choice = c;
				// (no action = it closes the box: a plan's "Close" passed null and threw instead - Franz, 2026-10-17)
				Button b = UIKit.Button(buttonRow, c.Label, () => { if (choice.Click != null) choice.Click(); else Close(); }, c.Hint, -1, 34f, 13);
				// (as wide as its label in Raft's font - a guess per letter was too narrow for some words)
				Text label = UIKit.LabelOf(b);
				float w = Mathf.Max(96f, Mathf.Max(26f + 9f * c.Label.Length, (label != null ? label.preferredWidth : 0f) + 34f));
				UIKit.Size(b.gameObject, w, 34f);
				row += w + ButtonSpacing;
				if (c.Primary) UIKit.Primary(b);
				buttons.Add(b);
			}
			// (the box grows so every button fits inside it, up to the screen's width)
			float screen = ((RectTransform)canvas.transform).rect.width;
			float width = Mathf.Max(PanelWidth, row + PanelPadding);
			if (screen > 0f) width = Mathf.Min(width, screen - 40f);
			panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);
			canvas.gameObject.SetActive(true);
			LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
			scroll.verticalNormalizedPosition = 1f;
			Debug.Log("[CUSTOM ISLANDS] Info box: " + title);
		}

		/// <summary>Called once when the box closes (any way: a button, Esc); Open clears it.</summary>
		public static Action Closed;

		public static void Close()
		{
			Action closed = Closed;
			Closed = null;
			if (closed != null && IsOpen) try { closed(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Info box closed: " + e.Message); }
			if (canvas != null && canvas.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			if (canvas != null) canvas.gameObject.SetActive(false);
		}

		/// <summary>A line over the buttons: what a button just did.</summary>
		public static void SetStatus(string text)
		{
			if (statusText != null) statusText.text = text;
		}

		void Update()
		{
			if (EscKey.Down(this)) Close();
		}

		#region The boxes

		/// <summary>The main menu's "Report a problem": what a good report holds, and where to send it.</summary>
		public static void OpenReport()
		{
			Open("Report a problem",
				"Thank you for helping! A good report lets someone who wasn't there see what happened, and make it happen again. Please include:\n\n" +
				"•  <b>What happened, in detail:</b> what you did, step by step, up to the problem - which island, which window or button. Does it happen every time, now and then, or once?\n" +
				"•  <b>What you expected</b> to happen if it had worked.\n" +
				"•  <b>Screenshots</b> or a short video if it can be seen (F12 in Steam, or Win+Shift+S).\n" +
				"•  <b>The logs</b>, before you start Raft again (a new start replaces them): <i>Player.log</i> and <i>Player-prev.log</i> from Raft's log folder (<b>Open log folder</b>).\n" +
				"•  <b>Your settings:</b> the versions (" + HelpLinks.Versions + "), the world's plan and World settings, single player or together (host or joined, how many players).\n" +
				"•  <b>The islands and world plans involved:</b> their names, and the files or where to download them.\n\n" +
				"<b>Copy report form</b> puts a form with your versions on the clipboard: paste it (Ctrl+V) into a GitHub issue or a Discord post and fill it in. " +
				"On GitHub (a free account is needed) you can attach the log files; on Discord you can also ask questions.",
				new Choice("Copy report form", () => { HelpLinks.CopyReportTemplate(); SetStatus("Copied: paste it (Ctrl+V) into the GitHub issue or the Discord post"); }, "Put a form to fill in, with your versions, on the clipboard"),
				new Choice("Open log folder", () => { HelpLinks.OpenLogFolder(); SetStatus("Attach Player.log (and Player-prev.log) from " + HelpLinks.LogFolder); }, "Raft's log folder, with Player.log and Player-prev.log"),
				new Choice("Report on GitHub", () => HelpLinks.Open(HelpLinks.NewIssueUrl), "A new issue on the mod's GitHub page, with the form in it", true),
				new Choice("Post on Discord", () => HelpLinks.Open(HelpLinks.Discord), "The Custom Islands Discord server", true),
				new Choice("Close", Close, "Close this box (Esc)"));
		}

		#endregion
	}
}
