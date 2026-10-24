using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The note editor: a window to write a readable object's title and text, with a preview of how the note looks
	/// when a player reads it in a world. Opened from the inspector's "Edit note..." button; Save is one undo step.
	/// </summary>
	public class NoteEditorWindow : MonoBehaviour
	{
		static NoteEditorWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		public const int MaxTitle = 60, MaxText = 4000;

		EditorGameObject target;
		InputField titleField, textField;
		Text previewTitle, previewText, countText;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("NoteEditorWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f); // modal
			instance = blocker.AddComponent<NoteEditorWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open(EditorGameObject target)
		{
			if (instance == null || target == null) return;
			instance.target = target;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.titleField.text = ObjectProps.Get(target.Props, ObjectProps.NoteTitle, "");
			instance.textField.text = ObjectProps.Get(target.Props, ObjectProps.NoteText, "");
			instance.UpdatePreview();
			instance.textField.ActivateInputField();
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
			instance.target = null;
			EditorInput.IsTyping = false;
		}

		/// <summary>Writes a note's title and text as one undo step (the tests use this too).</summary>
		public static void Apply(EditorGameObject target, string title, string text)
		{
			if (target == null) return;
			Dictionary<string, string> p = ObjectProps.With(target.Props, ObjectProps.NoteTitle, title ?? "");
			p = ObjectProps.With(p, ObjectProps.NoteText, text ?? "");
			PropsCommand.Change(target, p);
		}

		void Save()
		{
			if (target != null) Apply(target, titleField.text.Trim(), textField.text.TrimEnd());
			DynamicIslands.Notify("Note saved: \"" + titleField.text.Trim() + "\" (Ctrl+Z undoes)");
			Close();
		}

		/// <summary>The title or text differs from the note's (Esc asks before it closes).</summary>
		public static bool Unsaved
		{
			get
			{
				EditorGameObject t = instance != null ? instance.target : null;
				if (t == null || !IsOpen) return false;
				return instance.titleField.text.Trim() != ObjectProps.Get(t.Props, ObjectProps.NoteTitle, "").Trim()
					|| instance.textField.text.TrimEnd() != ObjectProps.Get(t.Props, ObjectProps.NoteText, "").TrimEnd();
			}
		}

		void Update()
		{
			EditorInput.IsTyping = titleField.isFocused || textField.isFocused;
			if (EscKey.Down(this)) { if (Unsaved) EscKey.AskUnsaved("The note", Save, Close); else Close(); }
			else if (EditorInput.Ctrl && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.S))) Save();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void UpdatePreview()
		{
			previewTitle.text = titleField.text.Trim().Length > 0 ? titleField.text.Trim() : "Note";
			previewText.text = textField.text.Length > 0 ? textField.text : "(The page is empty.)";
			countText.text = textField.text.Length + " / " + MaxText;
		}

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 0));

			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "NOTE EDITOR", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			UIKit.Label(head, "Players read it in a world with the interact key (E)", 12, UIKit.TextMuted, TextAnchor.MiddleRight);

			RectTransform columns = UIKit.Row(panel, 470f, 12f, "Columns");
			columns.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

			// Left: writing
			RectTransform write = UIKit.Group(columns, "Write");
			UIKit.Size(write.gameObject, 430, 470);
			UIKit.Size(UIKit.Label(write, "Title", 13, UIKit.TextMuted).gameObject, -1, 16);
			titleField = UIKit.Field(write, "e.g. Captain's log, day 12", "", 32f, "The note's title, shown in bold and in the \"Read\" hint");
			titleField.characterLimit = MaxTitle;
			titleField.onValueChanged.AddListener(v => UpdatePreview());
			RectTransform textHead = UIKit.Row(write, 16f, 4f, "TextHead");
			UIKit.Label(textHead, "Text", 13, UIKit.TextMuted);
			countText = UIKit.Label(textHead, "", 11, UIKit.TextMuted, TextAnchor.MiddleRight);
			textField = UIKit.TextArea(write, "Write what the note says. Enter starts a new line.", 330f, "The note's text (Enter = new line, Ctrl+Enter = save)");
			textField.characterLimit = MaxText;
			textField.onValueChanged.AddListener(v => UpdatePreview());

			// Right: how it looks
			RectTransform look = UIKit.Group(columns, "Preview");
			UIKit.Size(look.gameObject, 410, 470);
			// (as the note reader shows it: the title in Raft's lettering over the text on Raft's light tan)
			previewTitle = UIKit.Label(look, "", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.Size(previewTitle.gameObject, -1, 30);
			RectTransform sheet = UIKit.Rect("Sheet", look);
			UIKit.Size(sheet.gameObject, -1, 396);
			UIKit.ParchmentSheet(sheet);
			UIKit.Vertical(sheet.gameObject, 8f, new RectOffset(20, 20, 16, 16));
			previewText = UIKit.Label(sheet, "", 14, UIKit.ParchmentInk, TextAnchor.UpperLeft, FontStyle.Normal, "Text");
			{ Shadow sh = previewText.GetComponent<Shadow>(); if (sh != null) UnityEngine.Object.Destroy(sh); }
			previewText.supportRichText = false;
			previewText.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(previewText.gameObject, -1, -1).flexibleHeight = 1f;

			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Label(buttons, "Ctrl+Enter saves \u00B7 Esc cancels", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			Button save = UIKit.Button(buttons, "Save", Save, "Keep the note (Ctrl+Z undoes)", 110, 34);
			UIKit.Primary(save);
			UIKit.Button(buttons, "Cancel", Close, "Close without changing the note", 110, 34);
		}
	}
}
