using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A drop-down list (a combo box) in Raft's look: a button that says what is chosen, with a ▼; clicking it opens the
	/// options right under it, each with a line saying what it does, the chosen one lit. A click outside or Esc closes it.
	/// Used where a button cycled through its options one click at a time (the World Plans window's rule cards).
	/// </summary>
	public static class DropList
	{
		public class Option
		{
			public string Value, Label, Hint;
			public Option(string value, string label, string hint = "") { Value = value; Label = label; Hint = hint ?? ""; }
		}

		static GameObject open;
		public static bool IsOpen { get { return open != null; } }
		static int closedFrame = -1;
		/// <summary>A list is open, or Esc closed one this frame: the window under it keeps its own Esc for the next press.</summary>
		public static bool Busy { get { return open != null || closedFrame == Time.frameCount; } }

		/// <summary>Tests: clicks the drop-down open and the option with this value, as a player does. False if the list hasn't it.</summary>
		public static bool Click(Button drop, string value)
		{
			if (drop == null) return false;
			drop.onClick.Invoke();
			Button option = open != null ? open.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Option_" + value) : null;
			if (option == null) { Close(); return false; }
			option.onClick.Invoke();
			return true;
		}

		/// <summary>Tests: the options the drop-down shows when clicked open (value and the line under it), then closed again.</summary>
		public static List<KeyValuePair<string, string>> Shown(Button drop)
		{
			var list = new List<KeyValuePair<string, string>>();
			if (drop == null) return list;
			drop.onClick.Invoke();
			if (open != null)
				foreach (Button b in open.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Option_")))
				{
					Text hint = b.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Hint");
					list.Add(new KeyValuePair<string, string>(b.name.Substring(7), hint != null ? hint.text : ""));
				}
			Close();
			return list;
		}

		/// <summary>A drop-down button: shows the chosen option; set(value) when another is picked.</summary>
		public static Button Make(Transform parent, string name, IList<Option> options, string current, Action<string> set, float width, string hint, float height = 26f, int fontSize = 12)
		{
			Option chosen = options.FirstOrDefault(o => o.Value == current) ?? options.FirstOrDefault();
			Button b = UIKit.Button(parent, "", null, hint, width, height, fontSize);
			b.name = name;
			Text t = UIKit.LabelOf(b);
			t.alignment = TextAnchor.MiddleLeft;
			UIKit.Stretch(t.rectTransform, 8, 22, 1, 1);
			t.text = chosen != null ? chosen.Label : "";
			Text arrow = UIKit.Label(b.transform, "▼", 10, UIKit.AccentText, TextAnchor.MiddleCenter, FontStyle.Normal, "Arrow");
			arrow.rectTransform.anchorMin = new Vector2(1, 0); arrow.rectTransform.anchorMax = new Vector2(1, 1);
			arrow.rectTransform.pivot = new Vector2(1, 0.5f); arrow.rectTransform.sizeDelta = new Vector2(20, 0); arrow.rectTransform.anchoredPosition = new Vector2(-4, 0);
			var d = b.gameObject.AddComponent<DropdownButton>();
			d.Options = options.ToList(); d.Value = chosen != null ? chosen.Value : current; d.Set = set;
			b.onClick.AddListener(() => Open(b, d));
			return b;
		}

		/// <summary>Opens the list under (or, near the screen's bottom, over) the button.</summary>
		public static void Open(Button button, DropdownButton d)
		{
			Close();
			Canvas canvas = button.GetComponentInParent<Canvas>();
			if (canvas == null) return;
			Transform root = canvas.rootCanvas.transform;
			// (a see-through cover over everything: a click outside the list closes it)
			var cover = new GameObject("DropdownCover", typeof(RectTransform), typeof(Image));
			cover.layer = 5;
			cover.transform.SetParent(root, false);
			RectTransform coverRect = (RectTransform)cover.transform;
			UIKit.Stretch(coverRect);
			cover.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
			var closer = cover.AddComponent<Button>();
			closer.transition = Selectable.Transition.None;
			closer.onClick.AddListener(Close);
			cover.AddComponent<DropdownCloser>();
			cover.transform.SetAsLastSibling();
			open = cover;

			bool hints = d.Options.Any(o => o.Hint.Length > 0);
			// (a row as tall as its description needs: two lines for a long one)
			Func<Option, float> rowH = o => o.Hint.Length == 0 ? 28f : o.Hint.Length > 70 ? 54f : 40f;
			float spacing = 3f;
			float fullH = d.Options.Sum(o => rowH(o) + spacing) + 12f;
			float height = Mathf.Min(fullH, 430f);
			RectTransform btn = (RectTransform)button.transform;
			float width = Mathf.Max(btn.rect.width * btn.lossyScale.x / coverRect.lossyScale.x, hints ? 460f : 220f);

			// (the button's corners in the cover's space)
			var corners = new Vector3[4];
			btn.GetWorldCorners(corners); // 0 bottom-left, 1 top-left
			Vector2 bottomLeft = (Vector2)coverRect.InverseTransformPoint(corners[0]) - coverRect.rect.min;
			Vector2 topLeft = (Vector2)coverRect.InverseTransformPoint(corners[1]) - coverRect.rect.min;
			bool below = bottomLeft.y - height - 4f >= 0f || topLeft.y + height + 4f > coverRect.rect.height;
			float x = Mathf.Clamp(bottomLeft.x, 4f, Mathf.Max(4f, coverRect.rect.width - width - 4f));

			RectTransform panel = UIKit.Rect("DropdownList", cover.transform);
			UIKit.Surface(panel, 6);
			panel.anchorMin = panel.anchorMax = Vector2.zero;
			panel.pivot = below ? new Vector2(0, 1) : new Vector2(0, 0);
			panel.sizeDelta = new Vector2(width, height);
			float y = below ? bottomLeft.y - 2f : topLeft.y + 2f;
			// (always on the screen)
			y = below ? Mathf.Clamp(y, height + 4f, coverRect.rect.height - 4f) : Mathf.Clamp(y, 4f, coverRect.rect.height - height - 4f);
			panel.anchoredPosition = new Vector2(x, y);
			// (the surface's own image keeps clicks on the list from reaching the cover)
			Image surface = panel.GetComponent<Image>();
			if (surface != null) surface.raycastTarget = true;

			Transform list;
			if (fullH > height)
			{
				ScrollRect scroll;
				list = UIKit.ScrollList(panel, out scroll, spacing);
				UIKit.Stretch((RectTransform)scroll.transform, 6, 6, 6, 6);
			}
			else
			{
				RectTransform inner = UIKit.Rect("Options", panel);
				UIKit.Stretch(inner, 6, 6, 6, 6);
				UIKit.Vertical(inner.gameObject, spacing, new RectOffset(0, 0, 0, 0));
				list = inner;
			}
			foreach (Option o in d.Options)
			{
				Option option = o;
				Button row = UIKit.Button(list, "", () => { Close(); d.Choose(option.Value); }, option.Hint.Length > 0 ? option.Hint : null, -1, rowH(option), 13);
				row.name = "Option_" + option.Value;
				Text t = UIKit.LabelOf(row);
				t.alignment = option.Hint.Length > 0 ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
				t.text = option.Label;
				if (option.Hint.Length > 0)
				{
					UIKit.Stretch(t.rectTransform, 8, 8, 3, rowH(option) - 22f);
					// (what it does, in the body font: Raft's narrow capitals were hard to read that small)
					Text h = UIKit.Label(row.transform, option.Hint, 11, new Color(0.29f, 0.19f, 0.09f, 1f), TextAnchor.LowerLeft, FontStyle.Italic, "Hint");
					UIKit.Stretch(h.rectTransform, 9, 8, 21, 3); h.alignment = TextAnchor.UpperLeft;
					h.horizontalOverflow = HorizontalWrapMode.Wrap;
				}
				if (option.Value == d.Value) UIKit.SetActive(row, true);
			}
		}

		public static void Close()
		{
			if (open != null) { UnityEngine.Object.Destroy(open); closedFrame = Time.frameCount; }
			open = null;
		}

		/// <summary>Tests: picks an option of the drop-down named so under root, as a click on it would.</summary>
		public static bool Pick(GameObject root, string name, string value)
		{
			DropdownButton d = root.GetComponentsInChildren<DropdownButton>(false).FirstOrDefault(x => x.name == name);
			if (d == null || !d.Options.Any(o => o.Value == value)) return false;
			d.Choose(value);
			return true;
		}
	}

	/// <summary>What a drop-down button holds: its options, the chosen value, what to do on a pick.</summary>
	public class DropdownButton : MonoBehaviour
	{
		public List<DropList.Option> Options = new List<DropList.Option>();
		public string Value;
		public Action<string> Set;

		public void Choose(string value)
		{
			Value = value;
			DropList.Option o = Options.FirstOrDefault(x => x.Value == value);
			Button b = GetComponent<Button>();
			if (o != null && b != null && UIKit.LabelOf(b) != null) UIKit.LabelOf(b).text = o.Label;
			if (Set != null) Set(value);
		}
	}

	/// <summary>Esc closes an open list.</summary>
	class DropdownCloser : MonoBehaviour
	{
		void Update() { if (EscKey.Down(this)) DropList.Close(); }
	}
}
