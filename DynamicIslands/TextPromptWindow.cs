using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>A small modal window asking for a name (saving a group or a terrain stamp): a text field, OK and Cancel.</summary>
	public class TextPromptWindow : MonoBehaviour
	{
		static TextPromptWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		Text titleText, messageText;
		InputField field;
		Action<string> onOk;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("TextPromptWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<TextPromptWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		/// <summary>Asks for a name; ok gets it (trimmed, never empty or with characters a file name can't have).</summary>
		public static void Open(string title, string message, string text, Action<string> ok)
		{
			if (instance == null) return;
			instance.titleText.text = title.ToUpperInvariant();
			instance.messageText.text = message;
			instance.field.text = text ?? "";
			instance.onOk = ok;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.field.ActivateInputField();
		}

		public static void Close()
		{
			if (instance == null) return;
			if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			instance.gameObject.SetActive(false);
			instance.onOk = null;
			EditorInput.IsTyping = false;
		}

		public static bool ValidName(string name)
		{
			return FileNames.Valid(name);
		}

		void Ok()
		{
			string name = field.text.Trim();
			if (!ValidName(name)) { messageText.text = "<color=#e05a4d>" + FileNames.Problem(name) + "</color>"; return; }
			Action<string> ok = onOk;
			Close();
			if (ok != null) ok(name);
		}

		void Update()
		{
			EditorInput.IsTyping = field.isFocused;
			if (EscKey.Down(this)) Close();
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Ok();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 0));
			titleText = UIKit.Label(panel, "", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.Size(titleText.gameObject, -1, 26);
			messageText = UIKit.Label(panel, "", 13, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, "Message");
			field = UIKit.Field(panel, "Name", "", 32f);
			field.characterLimit = 48;
			RectTransform buttons = UIKit.Row(panel, 32f, 8f, "Buttons");
			UIKit.Size(UIKit.Label(buttons, "", 12, UIKit.TextMuted).gameObject, -1, -1, 1);
			Button ok = UIKit.Button(buttons, "OK", Ok, "Save (Enter)", 100, 32);
			UIKit.Primary(ok);
			UIKit.Button(buttons, "Cancel", Close, "Don't save (Esc)", 100, 32);
		}
	}
}
