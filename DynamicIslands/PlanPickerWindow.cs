using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// ROADMAP CT5: "Choose plan..." in the New Game box opens this window - the plans on the left (name, kind, island
	/// count; a search), the selected one on the right with a picture (the library's picture, else a map of its first
	/// island, else a plain placeholder), its full description, random/story and the islands it brings. Select or a
	/// double-click chooses it (WorldDirector.PendingPlan); up/down, Enter and Esc work too.
	/// </summary>
	public static class PlanPickerWindow
	{
		public const string CanvasName = "CustomIslandsPlanPicker", RowPrefix = "Plan_";

		static Canvas canvas;
		static RectTransform list;
		static Text title, kindText, settings, description, islands, empty;
		static InputField search;
		static RawImage picture;
		static Texture2D placeholder;
		static string selected, lastClicked;
		static float lastClickTime;
		static readonly List<KeyValuePair<string, Button>> rows = new List<KeyValuePair<string, Button>>();
		static readonly Dictionary<string, Texture> pictures = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);

		public static bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }
		public static RectTransform Root { get { return canvas != null ? (RectTransform)canvas.transform : null; } }
		/// <summary>The plan selected in the list (not yet chosen until Select).</summary>
		public static string Highlighted { get { return selected; } }
		public static Texture Picture { get { return picture != null ? picture.texture : null; } }
		public static bool PictureIsPlaceholder { get { return picture != null && picture.texture == placeholder; } }
		public static List<string> Shown() { return rows.Where(kv => kv.Value != null && kv.Value.gameObject.activeSelf).Select(kv => kv.Key).ToList(); }
		public static InputField Search { get { return search; } }

		static void Build()
		{
			canvas = UIKit.CreateCanvas(CanvasName, 875); // (over Raft's New Game box, under the island library (880) and the info boxes)
			UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
			RectTransform root = (RectTransform)canvas.transform;
			RectTransform dim = UIKit.Rect("Dim", root);
			UIKit.Stretch(dim);
			dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
			RectTransform panel = UIKit.Panel(root, "Panel", new RectOffset(16, 16, 12, 14), 8f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 660f));

			RectTransform head = UIKit.Row(panel, 34f, 8f, "Head");
			Text t = UIKit.Label(head, "CHOOSE A WORLD PLAN", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(t);
			UIKit.Size(t.gameObject, 330);
			Text sub = UIKit.Label(head, "which islands the new world gets - pick one to see what it does", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			sub.horizontalOverflow = HorizontalWrapMode.Wrap;
			search = UIKit.Field(head, "Search plans...", "", 32f, "Show only the plans whose name, description or islands hold this");
			UIKit.Size(search.gameObject, 260);
			search.onValueChanged.AddListener(_ => ShowList());

			RectTransform body = UIKit.Rect("Body", panel);
			UIKit.Size(body.gameObject, -1, 538);
			HorizontalLayoutGroup h = UIKit.Horizontal(body.gameObject, 12f);
			h.childForceExpandWidth = false; h.childForceExpandHeight = true; h.childControlHeight = true;

			RectTransform listBox = UIKit.Rect("ListBox", body);
			UIKit.Size(listBox.gameObject, 420, 538);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect scroll;
			list = UIKit.ScrollList(listBox, out scroll, 3f);
			UIKit.Stretch((RectTransform)scroll.transform, 4, 4, 4, 4);
			empty = UIKit.Label(listBox, "", 13, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, "Empty");
			UIKit.Stretch(empty.rectTransform, 20, 20, 20, 20);

			RectTransform detail = UIKit.Rect("Detail", body);
			UIKit.Vertical(detail.gameObject, 5f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(detail.gameObject, 636, 538);
			RectTransform frame = UIKit.Rect("PictureFrame", detail);
			UIKit.Size(frame.gameObject, 636, 250);
			UIKit.Background(frame.gameObject, UIKit.FieldBg, 6);
			picture = UIKit.Picture(frame, null, 636, 250, "Picture");
			UIKit.Anchor(picture.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(636f, 250f));
			title = UIKit.Label(detail, "", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "PlanTitle");
			UIKit.Size(title.gameObject, -1, 26);
			kindText = UIKit.Label(detail, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Kind");
			UIKit.Size(kindText.gameObject, -1, 16);
			settings = UIKit.Label(detail, "", 13, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold, "Settings");
			settings.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(settings.gameObject, -1, 36);
			description = UIKit.Label(detail, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Description");
			description.horizontalOverflow = HorizontalWrapMode.Wrap;
			description.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(description.gameObject, -1, 104);
			islands = UIKit.Label(detail, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Islands");
			islands.horizontalOverflow = HorizontalWrapMode.Wrap;
			islands.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(islands.gameObject, -1, 52);
			RectTransform actions = UIKit.Row(detail, 34f, 8f, "Actions");
			Button select = UIKit.Button(actions, "Select", Choose, "Use this plan for the new world (Enter, or double-click it in the list)", 150, 34f, 15);
			select.name = "Button_Select";
			UIKit.Primary(select);
			UIKit.Button(actions, "View rules...", () => { if (selected != null) InfoWindow.Open("Plan: " + selected, NewWorldOptions.PlanText(WorldPlan.Load(selected)), new InfoWindow.Choice("Close", InfoWindow.Close)); },
				"Every rule of the plan in words: when and where each island comes", 140, 34f, 13).name = "Button_Rules";
			UIKit.Size(UIKit.Label(actions, "", 12).gameObject, -1, -1, 1);
			UIKit.Button(actions, "Cancel", Close, "Keep the plan chosen before (Esc)", 120, 34f, 13).name = "Button_Cancel";

			WindowKeys keys = canvas.gameObject.AddComponent<WindowKeys>();
			keys.Typing = () => search != null && search.isFocused;
			keys.Close = Close;
			canvas.gameObject.AddComponent<PickerKeys>();
			canvas.gameObject.SetActive(false);
		}

		/// <summary>Opens the window with the New Game box's plan selected.</summary>
		public static void Open()
		{
			if (canvas == null) Build();
			selected = NewWorldOptions.Selected;
			lastClicked = null;
			canvas.gameObject.SetActive(true);
			search.text = "";
			ShowList();
		}

		public static void Close()
		{
			if (canvas != null) canvas.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		/// <summary>Select: the highlighted plan becomes the New Game box's choice.</summary>
		public static void Choose()
		{
			if (selected == null) return;
			WorldDirector.PendingPlan = selected;
			Close();
			NewWorldOptions.Refresh();
		}

		/// <summary>Highlights a plan by name (tests, keys).</summary>
		public static bool Select(string name)
		{
			if (!rows.Any(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;
			selected = rows.First(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Key;
			ShowDetail();
			return true;
		}

		internal static void Move(int by)
		{
			List<string> shown = Shown();
			if (shown.Count == 0) return;
			int i = shown.FindIndex(n => n.Equals(selected, StringComparison.OrdinalIgnoreCase));
			i = Mathf.Clamp(i < 0 ? 0 : i + by, 0, shown.Count - 1);
			Select(shown[i]);
			Button b = rows.First(kv => kv.Key == shown[i]).Value;
			ScrollRect sr = list.GetComponentInParent<ScrollRect>();
			if (sr != null && shown.Count > 1)
			{
				// keep the row in view
				float viewH = ((RectTransform)sr.viewport ?? (RectTransform)sr.transform).rect.height, contentH = list.rect.height;
				if (contentH > viewH)
				{
					float top = -((RectTransform)b.transform).anchoredPosition.y - ((RectTransform)b.transform).rect.height * 0.5f;
					float y = Mathf.Clamp(top - viewH * 0.4f, 0f, contentH - viewH);
					sr.verticalNormalizedPosition = 1f - y / (contentH - viewH);
				}
			}
		}

		/// <summary>built in / library / your plan, and "big plan" for ten islands or more.</summary>
		internal static string KindOf(WorldPlan p, int islandCount)
		{
			string kind = p.BuiltIn ? "built in" :
				Installed(p.Name) != null ? "library plan" : "your plan";
			return islandCount >= 10 ? kind + ", big plan" : kind;
		}

		/// <summary>The islands a plan brings by name (island and one-of rules), in the plan's order.</summary>
		internal static List<string> IslandsOf(WorldPlan p)
		{
			var names = new List<string>();
			foreach (IntroRule r in p.Rules)
			{
				if (r.What == "island" && r.WhatArg.Trim().Length > 0) names.Add(r.WhatArg.Trim());
				else if (r.What == "oneof") names.AddRange(r.WhatArg.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0));
			}
			return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		static LibraryInstalled Installed(string plan)
		{
			try { return LibraryPack.Installed().FirstOrDefault(i => i.kind == LibraryPack.KindPlan && i.plan.Equals(plan, StringComparison.OrdinalIgnoreCase)); }
			catch { return null; }
		}

		static void ShowList()
		{
			if (canvas == null) return;
			foreach (Transform c in list) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
			rows.Clear();
			string s = search.text.Trim();
			int count = 0;
			foreach (string n in WorldPlan.All())
			{
				WorldPlan p = WorldPlan.Load(n);
				if (p == null) continue;
				List<string> isl = IslandsOf(p);
				if (s.Length > 0 && (p.Name + " " + p.Description + " " + string.Join(" ", isl.ToArray())).IndexOf(s, StringComparison.OrdinalIgnoreCase) < 0) continue;
				AddRow(p, isl.Count);
				count++;
			}
			empty.text = count == 0 ? "No plans match the search." : "";
			if (selected == null || !rows.Any(kv => kv.Key.Equals(selected, StringComparison.OrdinalIgnoreCase))) selected = rows.Count > 0 ? rows[0].Key : null;
			ShowDetail();
		}

		static void AddRow(WorldPlan p, int islandCount)
		{
			string name = p.Name;
			Button b = UIKit.Button(list, "", () => Clicked(name), null, -1, 44f, 13);
			b.name = RowPrefix + name;
			UIKit.Flat(b);
			UIKit.LabelOf(b).text = "";
			Text nm = UIKit.Label(b.transform, name, 14, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold, "Name");
			UIKit.Stretch(nm.rectTransform, 10, 90, 4, 20);
			Text sub = UIKit.Label(b.transform, KindOf(p, islandCount), 11, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, "Sub");
			UIKit.Stretch(sub.rectTransform, 10, 90, 24, 2);
			Text cnt = UIKit.Label(b.transform, p.BuiltIn ? "" : islandCount == 1 ? "1 island" : islandCount + " islands", 11, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, "Count");
			UIKit.Stretch(cnt.rectTransform, 10, 10, 0, 0);
			UIKit.Hint(b.gameObject, p.Description.Length > 0 ? p.Description : name);
			rows.Add(new KeyValuePair<string, Button>(name, b));
		}

		static void Clicked(string name)
		{
			bool twice = name == lastClicked && Time.unscaledTime - lastClickTime < 0.4f;
			lastClicked = name; lastClickTime = Time.unscaledTime;
			Select(name);
			if (twice) Choose();
		}

		static void ShowDetail()
		{
			foreach (var kv in rows) UIKit.SetActive(kv.Value, kv.Key == selected);
			WorldPlan p = selected != null ? WorldPlan.Load(selected) : null;
			if (p == null)
			{
				title.text = kindText.text = settings.text = description.text = islands.text = "";
				SetPicture(Placeholder());
				return;
			}
			List<string> isl = IslandsOf(p);
			title.text = p.Name;
			kindText.text = KindOf(p, isl.Count) + (p.BuiltIn ? "" : "  -  " + p.Rules.Count + (p.Rules.Count == 1 ? " rule" : " rules"));
			settings.text = "Random islands while sailing: " + (p.Random ? "on" : "off") + "     Raft's story: " +
				(!p.RaftStory ? "off - the plan's own story" : p.LeaveOut.Count > 0 ? "on, without " + string.Join(", ", p.LeaveOut.ToArray()) : "on");
			description.text = p.Description.Trim().Length > 0 ? p.Description.Trim() : p.Name == WorldPlan.NoneName ? "A normal Raft world." : "(no description)";
			islands.text = isl.Count == 0 ? (p.BuiltIn ? "" : "Brings no islands by name (only kinds or pools of islands).") :
				"Islands it brings (" + isl.Count + "): " + string.Join(", ", isl.Take(30).ToArray()) + (isl.Count > 30 ? ", ..." : "");
			ShowPicture(p, isl);
		}

		static void ShowPicture(WorldPlan p, List<string> isl)
		{
			Texture tex;
			if (pictures.TryGetValue(p.Name, out tex) && tex != null) { SetPicture(tex); return; }
			SetPicture(Placeholder());
			// the library's picture, when the plan came from the library and its list was read
			LibraryInstalled inst = Installed(p.Name);
			LibraryEntry entry = inst != null && LibraryClient.Entries != null ? LibraryClient.Entries.FirstOrDefault(e => e.Info.id == inst.id) : null;
			string pic = entry == null ? null : entry.Info.pictures != null && entry.Info.pictures.Length > 0 ? entry.Info.pictures[0] : entry.Info.icon;
			string plan = p.Name;
			if (!string.IsNullOrEmpty(pic))
			{
				DynamicIslands.instance.StartCoroutine(LibraryClient.Picture(entry, pic, t =>
				{
					if (t == null) return;
					pictures[plan] = t;
					if (selected == plan && picture != null) SetPicture(t);
				}));
			}
			// else a map of its first island
			tex = IslandMap(isl.FirstOrDefault());
			if (tex != null) { pictures[plan] = tex; SetPicture(tex); }
		}

		/// <summary>Shows a texture whole in the 636x250 frame (a square island map in the middle, the placeholder filling it).</summary>
		static void SetPicture(Texture t)
		{
			picture.texture = t;
			float w = 636f, hgt = 250f;
			if (t != null && t != placeholder && t.height > 0)
			{
				float a = (float)t.width / t.height;
				if (a < w / hgt) w = hgt * a; else hgt = w / a;
			}
			picture.rectTransform.sizeDelta = new Vector2(w, hgt);
		}

		/// <summary>A top-down map of a saved island (the generator's preview colours), or null.</summary>
		internal static Texture2D IslandMap(string island)
		{
			if (string.IsNullOrEmpty(island)) return null;
			try
			{
				string path = IslandSpawner.PathFor(island);
				if (path == null || !System.IO.File.Exists(path)) return null;
				return IslandThumbs.Map(IslandFile.Load(path), 192);
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Plan picture of " + island + ": " + e.Message); return null; }
		}

		static Texture2D Placeholder()
		{
			if (placeholder != null) return placeholder;
			placeholder = new Texture2D(2, 2);
			Color c = new Color(0.16f, 0.25f, 0.30f, 1f);
			placeholder.SetPixels(new[] { c, c, c, c });
			placeholder.Apply();
			return placeholder;
		}

		/// <summary>Up/down move through the list, Enter selects.</summary>
		class PickerKeys : MonoBehaviour
		{
			void Update()
			{
				if (InfoWindow.IsOpen || DropList.Busy) return;
				if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
				else if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
				else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Choose();
			}
		}
	}
}
