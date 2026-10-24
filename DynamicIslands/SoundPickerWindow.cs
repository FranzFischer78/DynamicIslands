using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft's sounds (FMOD events from its loaded sound banks) to choose one for a sound zone: a search box and a list;
	/// each row can be listened to (\u25BA) and chosen (Use). Loops are marked, since they suit "while inside" zones.
	/// </summary>
	public class SoundPickerWindow : MonoBehaviour
	{
		const int MaxRows = 150;
		static SoundPickerWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

		EditorGameObject target;
		/// <summary>Instead of a zone: who gets the chosen sound (the behaviour window's "play a sound").</summary>
		Action<string> onPick;
		InputField search;
		RectTransform list;
		Text countText;
		float rebuildAt = -1f;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("SoundPickerWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<SoundPickerWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open(EditorGameObject target)
		{
			if (instance == null || target == null) return;
			instance.target = target;
			instance.onPick = null;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.search.text = "amb/";
			instance.Rebuild();
			instance.search.ActivateInputField();
		}

		/// <summary>Opens the list to choose a sound for something other than a zone.</summary>
		public static void OpenFor(Action<string> pick)
		{
			if (instance == null) return;
			instance.target = null;
			instance.onPick = pick;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.search.text = "";
			instance.Rebuild();
			instance.search.ActivateInputField();
		}

		public static void Close()
		{
			if (instance == null) return;
			SoundLibrary.StopPreview();
			if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			instance.gameObject.SetActive(false);
			instance.target = null;
			EditorInput.IsTyping = false;
			ObjectInspector.Refresh();
		}

		/// <summary>Chooses a sound for a zone (an undo step); a loop plays while inside, anything else once on entering.</summary>
		public static void Use(EditorGameObject target, string path)
		{
			Dictionary<string, string> p = ObjectProps.With(target.Props, ObjectProps.SoundEvent, path);
			p = ObjectProps.With(p, ObjectProps.SoundMode, SoundLibrary.IsLooping(path) ? null : "enter");
			PropsCommand.Change(target, p);
		}

		void Update()
		{
			EditorInput.IsTyping = search.isFocused;
			if (EscKey.Down(this)) Close();
			if (rebuildAt > 0f && Time.unscaledTime >= rebuildAt) { rebuildAt = -1f; Rebuild(); }
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "RAFT'S SOUNDS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			countText = UIKit.Label(head, "", 12, UIKit.TextMuted, TextAnchor.MiddleRight);
			search = UIKit.Field(panel, "Search (ambience, bird, wind, wave, music...)", "", 30f, "Type part of a sound's name");
			search.onValueChanged.AddListener(v => rebuildAt = Time.unscaledTime + 0.25f);
			RectTransform box = UIKit.Rect("List", panel);
			UIKit.Size(box.gameObject, -1, 420);
			ScrollRect scroll;
			list = UIKit.ScrollList(box, out scroll, 3f);
			UIKit.Stretch((RectTransform)scroll.transform);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Label(buttons, "\u25BA listens \u00B7 [loop] = a loop (good for \"while inside\")", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			UIKit.Button(buttons, "Stop", SoundLibrary.StopPreview, "Stop listening", 80, 34);
			Button close = UIKit.Button(buttons, "Close", Close, "Back to the editor", 100, 34);
			UIKit.SetActive(close, true);
		}

		void Rebuild()
		{
			foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			List<string> all = SoundLibrary.Events;
			string q = search.text.Trim().ToLowerInvariant();
			List<string> hits = all.Where(e => q.Length == 0 || e.ToLowerInvariant().Contains(q)).ToList();
			countText.text = hits.Count + " of " + all.Count + " sounds";
			if (all.Count == 0) UIKit.Label(list, "Raft's sound banks aren't loaded here.", 13, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
			foreach (string path in hits.Take(MaxRows))
			{
				string p = path;
				RectTransform row = UIKit.Row(list, 26f, 4f, "Sound");
				Text t = UIKit.Label(row, (SoundLibrary.IsLooping(p) ? "[loop] " : "") + p.Replace("event:/", ""), 12, UIKit.TextColor);
				t.horizontalOverflow = HorizontalWrapMode.Overflow;
				UIKit.Button(row, "\u25BA", () => SoundLibrary.Preview(p), "Listen", 30, 24f, 11);
				Button use = UIKit.Button(row, "Use", () => { Action<string> pick = onPick; if (pick != null) pick(p); else if (target != null) Use(target, p); Close(); }, "Use this sound for the zone", 52, 24f, 11);
				UIKit.SetActive(use, target != null && ObjectProps.Get(target.Props, ObjectProps.SoundEvent) == p);
			}
			if (hits.Count > MaxRows) UIKit.Label(list, (hits.Count - MaxRows) + " more: type more of the name.", 12, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
		}
	}
}
