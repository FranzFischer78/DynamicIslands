using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A small UI toolkit for the editor, built entirely in code, in the look of Raft's own menus: brown grunge
	/// panels with a dark rim, tan buttons with dark lettering, dark brown fields and wells, Raft's title font
	/// (ChineseRocks) for headings and buttons and its body font (Calibri bold) for text. Raft's sprites and fonts
	/// are taken from memory at the main menu (<see cref="CaptureRaftLook"/>); without them, rounded sprites
	/// generated at runtime and Arial stand in, in the same colours.
	/// Helpers for panels, bordered groups, buttons, choice buttons, sliders, text fields and layouts.
	/// Every control can carry a hint that the status bar shows while the mouse is over it.
	/// </summary>
	public static class UIKit
	{
		// Theme: Raft's menu colours (the New Game and Settings boxes)
		public static readonly Color PanelBg = new Color(0.41f, 0.29f, 0.18f, 1f);       // grunge brown (when the texture is missing)
		public static readonly Color PanelRim = new Color(0.373f, 0.224f, 0.125f, 1f);   // #5F3920, the dark edge under panels
		public static readonly Color GroupBg = new Color(0.231f, 0.129f, 0.059f, 0.38f); // a darker well in a panel
		public static readonly Color GroupBorder = new Color(0.733f, 0.631f, 0.416f, 0.28f);
		public static readonly Color ButtonBg = new Color(0.373f, 0.224f, 0.125f, 1f);   // dark choice button (Raft's tabs)
		public static readonly Color ButtonHover = new Color(0.46f, 0.29f, 0.17f, 1f);
		public static readonly Color ButtonPressed = new Color(0.29f, 0.17f, 0.09f, 1f);
		public static readonly Color ButtonBorder = new Color(0.231f, 0.129f, 0.059f, 0.9f);
		public static readonly Color Tan = new Color(0.733f, 0.631f, 0.416f, 1f);         // #BBA16A, Raft's lettering and button colour
		public static readonly Color Accent = new Color(0.97f, 0.8f, 0.42f, 1f);          // golden: values, the chosen thing
		public static readonly Color AccentText = new Color(0.19f, 0.12f, 0.05f, 1f);    // dark lettering on tan
		public static readonly Color TextColor = new Color(0.93f, 0.87f, 0.73f, 1f);
		public static readonly Color TextMuted = new Color(0.8f, 0.7f, 0.5f, 1f);
		public static readonly Color TextShadow = new Color(0.278f, 0.145f, 0.055f, 1f); // #47250E
		public static readonly Color FieldBg = new Color(0.31f, 0.18f, 0.095f, 1f);
		public static readonly Color SlotText = new Color(0.25f, 0.17f, 0.08f, 1f);
		public static readonly Color Danger = new Color(0.9f, 0.33f, 0.3f, 1f);
		public static readonly Color Good = new Color(0.62f, 0.8f, 0.3f, 1f);

		public const int FontSize = 15;
		public const float RowHeight = 30f;

		static Font font, titleFont, arial;
		/// <summary>Body text: Raft's Calibri bold, or Arial.</summary>
		public static Font Font
		{
			get { CaptureRaftLook(); return font != null ? font : Arial; }
			set { font = value; }
		}

		/// <summary>Headings and buttons: Raft's ChineseRocks (capitals only), or the body font.</summary>
		public static Font TitleFont { get { CaptureRaftLook(); return titleFont != null ? titleFont : Font; } }

		static Font Arial { get { if (arial == null) arial = Resources.GetBuiltinResource<Font>("Arial.ttf"); return arial; } }

		#region Raft's own look (sprites and fonts from memory)

		static readonly string[] RaftSprites = { "RoundedSquare_5px", "DarkbrownGrunge", "BigButtonNormal", "BigButtonHighlighted", "CraftButtonNormal", "DeleteButtonNorma",
			"Divider", "Scrollbar", "ItemBG_wo_Frame", "ExitButtonNormal" };
		static readonly Dictionary<string, Sprite> raft = new Dictionary<string, Sprite>();
		static float nextCapture = -1f;

		/// <summary>
		/// Finds Raft's menu sprites and fonts in memory (they are loaded at the main menu, and Raft's settings and
		/// loading screens keep them for the whole game) and keeps them. Cheap once everything is found.
		/// </summary>
		public static void CaptureRaftLook()
		{
			if (font != null && titleFont != null && raft.Count == RaftSprites.Length) return;
			if (Time.realtimeSinceStartup < nextCapture) return;
			nextCapture = Time.realtimeSinceStartup + 10f;
			try
			{
				foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
				{
					if (f == null) continue;
					if (font == null && f.name == "calibrib") font = f;
					if (titleFont == null && f.name == "ChineseRocks") titleFont = f;
				}
				foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
					if (s != null && s.texture != null && !raft.ContainsKey(s.name) && Array.IndexOf(RaftSprites, s.name) >= 0) raft[s.name] = s;
				Debug.Log("[CUSTOM ISLANDS] Raft's look: fonts " + (font != null ? "calibrib" : "-") + "/" + (titleFont != null ? "ChineseRocks" : "-") + ", sprites " + raft.Count + "/" + RaftSprites.Length +
					(raft.Count < RaftSprites.Length ? " (missing " + string.Join(", ", RaftSprites.Where(n => !raft.ContainsKey(n)).ToArray()) + ")" : ""));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Raft's look: " + e.Message); }
		}

		/// <summary>One of Raft's sprites by name, or null.</summary>
		public static Sprite RaftSprite(string name)
		{
			CaptureRaftLook();
			Sprite s;
			return raft.TryGetValue(name, out s) && s != null ? s : null;
		}

		/// <summary>Whether the font draws every character of the text (Raft's fonts lack some symbols).</summary>
		static bool Covers(Font f, string text)
		{
			if (f == null || string.IsNullOrEmpty(text)) return true;
			bool tag = false;
			foreach (char c in text)
			{
				if (c == '<') tag = true;
				else if (c == '>') { tag = false; continue; }
				if (tag || c == '\n' || c == ' ') continue;
				if (!f.HasCharacter(c)) return false;
			}
			return true;
		}

		/// <summary>Keeps a text in its preferred font, switching to a fallback while the text has characters the font lacks.</summary>
		public class FontFallback : MonoBehaviour
		{
			public Font Preferred, Fallback;
			Text text;
			string shown;

			void LateUpdate()
			{
				if (text == null) text = GetComponent<Text>();
				if (text == null || text.text == shown) return;
				shown = text.text;
				Font want = Covers(Preferred, shown) ? Preferred : Covers(Fallback, shown) ? Fallback : Arial;
				if (text.font != want) text.font = want;
			}
		}

		/// <summary>Gives a text Raft's title font (with fallbacks for symbols), in capitals as Raft writes them.</summary>
		internal static void UseTitleFont(Text t)
		{
			Font title = TitleFont;
			if (title == null || title == Font) return;
			t.font = Covers(title, t.text) ? title : Font;
			t.fontStyle = FontStyle.Normal;
			var fb = Ensure<FontFallback>(t.gameObject);
			fb.Preferred = title;
			fb.Fallback = Font;
		}

		/// <summary>Raft's lettering shadow (dark brown, down and right).</summary>
		public static Shadow TextShade(Text t, float distance = 1.5f)
		{
			Shadow s = Ensure<Shadow>(t.gameObject);
			s.effectColor = TextShadow;
			s.effectDistance = new Vector2(distance, -distance);
			return s;
		}

		/// <summary>Raft's panel surface: the dark rim, the brown grunge texture tiled inside, a thin light edge.</summary>
		public static void Surface(RectTransform r, int radius = 6, bool rim = true)
		{
			Image bg = Background(r.gameObject, rim ? PanelRim : PanelBg, radius);
			Sprite grunge = RaftSprite("DarkbrownGrunge");
			if (grunge != null)
			{
				RectTransform g = Rect("Grunge", r);
				g.SetAsFirstSibling();
				if (rim) Stretch(g, 2, 2, 2, 4); else Stretch(g);
				var img = g.gameObject.AddComponent<Image>();
				img.sprite = grunge; img.type = Image.Type.Tiled; img.color = Color.white; img.raycastTarget = false;
				g.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			}
			else if (!rim) bg.color = PanelBg;
			if (rim) Border(r, new Color(Tan.r, Tan.g, Tan.b, 0.35f), radius, 1f);
		}

		#endregion

		#region Generated sprites

		static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

		/// <summary>A filled rounded rectangle (9-sliced, so it stretches to any size).</summary>
		public static Sprite Rounded(int radius) { return RoundedSprite(radius, 0f); }

		/// <summary>Only the outline of a rounded rectangle, <paramref name="thickness"/> pixels wide.</summary>
		public static Sprite Outline(int radius, float thickness = 1.5f) { return RoundedSprite(radius, thickness); }

		static Sprite RoundedSprite(int radius, float thickness)
		{
			string key = radius + "/" + thickness;
			Sprite s;
			if (sprites.TryGetValue(key, out s) && s != null) return s;
			int size = radius * 2 + 4;
			var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CI_Rounded" + key };
			var pixels = new Color32[size * size];
			float half = size / 2f;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					// Signed distance to the rounded box edge (negative inside)
					float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
					float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
					float outside = new Vector2(Mathf.Max(px, 0), Mathf.Max(py, 0)).magnitude + Mathf.Min(Mathf.Max(px, py), 0);
					float d = outside - radius;
					float a = Mathf.Clamp01(0.5f - d);
					if (thickness > 0f) a *= Mathf.Clamp01(0.5f + d + thickness);
					pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
				}
			tex.SetPixels32(pixels);
			tex.Apply(false, true);
			float b = radius + 1;
			s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
			s.name = tex.name;
			sprites[key] = s;
			return s;
		}

		#endregion

		#region Layout basics

		/// <summary>The component, added if missing (Unity objects must not be null-coalesced with ??).</summary>
		public static T Ensure<T>(GameObject go) where T : Component
		{
			T c = go.GetComponent<T>();
			return c != null ? c : go.AddComponent<T>();
		}

		public static RectTransform Rect(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.layer = 5; // UI
			go.transform.SetParent(parent, false);
			return (RectTransform)go.transform;
		}

		public static void Stretch(RectTransform r, float left = 0, float right = 0, float top = 0, float bottom = 0)
		{
			r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
			r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
		}

		/// <summary>Anchors a rect to a corner/edge: anchor and pivot (0..1 each), position relative to the anchor, size.</summary>
		public static void Anchor(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
		{
			r.anchorMin = r.anchorMax = r.pivot = anchor;
			r.anchoredPosition = position;
			r.sizeDelta = size;
		}

		/// <summary>Raft's light tan of its item slots, as a sheet to read on (tinted as a slot button at rest).</summary>
		public static readonly Color Parchment = new Color(0.86f, 0.83f, 0.78f, 1f);
		/// <summary>Lettering on the tan sheet: Raft's dark brown.</summary>
		public static readonly Color ParchmentInk = new Color(0.2f, 0.13f, 0.06f, 1f);

		/// <summary>
		/// A sheet to read on in Raft's light tan - its item slots' look (ItemBG_wo_Frame), with Raft's dark rim: the journal's
		/// page and the note reader (the user, 2026-10-02: the cream paper felt like Windows Notepad). Without Raft's sprite a
		/// tan rounded sheet.
		/// </summary>
		public static Image ParchmentSheet(RectTransform r)
		{
			Image img = Ensure<Image>(r.gameObject);
			Sprite s = RaftSprite("ItemBG_wo_Frame");
			if (s != null)
			{
				img.sprite = s;
				img.type = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
				img.color = Parchment;
			}
			else
			{
				img.sprite = Rounded(6);
				img.type = Image.Type.Sliced;
				img.color = Tan;
			}
			Border(r, PanelRim, 6, 2f);
			return img;
		}

		public static Image Background(GameObject go, Color color, int radius = 8)
		{
			Image img = Ensure<Image>(go);
			img.sprite = radius > 0 ? Rounded(radius) : null;
			img.type = Image.Type.Sliced;
			img.color = color;
			return img;
		}

		/// <summary>An outline drawn over a control (does not catch clicks).</summary>
		public static Image Border(Transform target, Color color, int radius = 8, float thickness = 1.5f)
		{
			RectTransform r = Rect("Border", target);
			Stretch(r);
			r.SetAsLastSibling();
			var img = r.gameObject.AddComponent<Image>();
			img.sprite = Outline(radius, thickness);
			img.type = Image.Type.Sliced;
			img.color = color;
			img.raycastTarget = false;
			r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // never takes part in a parent's layout
			return img;
		}

		public static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding, bool fitHeight = false)
		{
			var v = Ensure<VerticalLayoutGroup>(go);
			v.spacing = spacing; v.padding = padding ?? new RectOffset();
			v.childControlWidth = true; v.childControlHeight = true;
			v.childForceExpandWidth = true; v.childForceExpandHeight = false;
			if (fitHeight) (Ensure<ContentSizeFitter>(go)).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			return v;
		}

		public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, RectOffset padding = null)
		{
			var h = Ensure<HorizontalLayoutGroup>(go);
			h.spacing = spacing; h.padding = padding ?? new RectOffset();
			h.childControlWidth = true; h.childControlHeight = true;
			h.childForceExpandWidth = true; h.childForceExpandHeight = true;
			h.childAlignment = TextAnchor.MiddleLeft;
			return h;
		}

		/// <summary>
		/// Layout size of a control. A given width or height is fixed (it doesn't grow); without a width the control
		/// takes its share of the free space in a row.
		/// </summary>
		public static LayoutElement Size(GameObject go, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
		{
			var le = Ensure<LayoutElement>(go);
			if (preferredWidth >= 0) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
			if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; le.flexibleHeight = 0; }
			le.flexibleWidth = flexibleWidth >= 0 ? flexibleWidth : preferredWidth >= 0 ? 0 : 1;
			return le;
		}

		/// <summary>A row that lays its children out side by side (equal widths unless they say otherwise).</summary>
		public static RectTransform Row(Transform parent, float height = RowHeight, float spacing = 6f, string name = "Row")
		{
			RectTransform r = Rect(name, parent);
			Horizontal(r.gameObject, spacing).childForceExpandWidth = false; // fixed-width controls stay fixed, the others share the rest
			Size(r.gameObject, -1, height);
			return r;
		}

		/// <summary>
		/// Dev and tests: what doesn't fit in a window at a screen of this size (pixels) and UI scale (1 = the mod's own; 1.25 =
		/// everything a quarter larger): the window itself past the edge of the screen, and every button (and ? mark) that lies
		/// outside the window or is squeezed to nothing - a row of fixed widths wider than the window pushed its last buttons
		/// out (World plans' Close). One line each; empty when everything fits. The window is taken to be centred, as the
		/// editor's windows are. A button in a scrolled list (under a mask) is only checked across: scrolled out of view is fine.
		/// </summary>
		public static List<string> RowOverflow(RectTransform window, Vector2 screen, float uiScale = 1f)
		{
			var problems = new List<string>();
			if (window == null) { problems.Add("no window"); return problems; }
			Canvas.ForceUpdateCanvases();
			Canvas canvas = window.GetComponentInParent<Canvas>();
			if (canvas != null) canvas = canvas.rootCanvas;
			RectTransform root = canvas != null ? (RectTransform)canvas.transform : null;
			UnityEngine.Rect own = LocalRect(window, root);
			// (the canvas size Raft's screen gives, in the units the window is laid out in)
			float scale = ScaleFor(canvas != null ? canvas.GetComponent<CanvasScaler>() : null, screen) * Mathf.Max(0.1f, uiScale);
			Vector2 logical = screen / scale;
			UnityEngine.Rect area = new UnityEngine.Rect(-logical.x / 2f, -logical.y / 2f, logical.x, logical.y);
			UnityEngine.Rect placed = new UnityEngine.Rect(own.x - (root != null ? root.rect.center.x : 0f), own.y - (root != null ? root.rect.center.y : 0f), own.width, own.height);
			if (!Inside(placed, area, 1f))
				problems.Add(window.name + " (" + Mathf.RoundToInt(own.width) + " x " + Mathf.RoundToInt(own.height) + ") doesn't fit the screen " +
					Mathf.RoundToInt(screen.x) + " x " + Mathf.RoundToInt(screen.y) + " at UI scale " + uiScale.ToString("0.##") + " (" + Mathf.RoundToInt(logical.x) + " x " + Mathf.RoundToInt(logical.y) + ")");
			foreach (Button b in window.GetComponentsInChildren<Button>(false))
			{
				RectTransform r = (RectTransform)b.transform;
				UnityEngine.Rect rect = LocalRect(r, root);
				string name = b.name + " in " + (r.parent != null ? r.parent.name : "?");
				if (rect.width < 4f || rect.height < 4f) { problems.Add(name + " is squeezed to " + Mathf.RoundToInt(rect.width) + " x " + Mathf.RoundToInt(rect.height)); continue; }
				RectMask2D clip = r.parent != null ? r.parent.GetComponentInParent<RectMask2D>() : null;
				Mask mask = r.parent != null ? r.parent.GetComponentInParent<Mask>() : null;
				RectTransform view = clip != null && clip.transform.IsChildOf(window) ? (RectTransform)clip.transform : mask != null && mask.transform.IsChildOf(window) ? (RectTransform)mask.transform : null;
				if (view != null)
				{
					UnityEngine.Rect v = LocalRect(view, root);
					if (rect.xMin < v.xMin - 1f || rect.xMax > v.xMax + 1f) problems.Add(name + " sticks out of its list by " + Mathf.RoundToInt(Mathf.Max(v.xMin - rect.xMin, rect.xMax - v.xMax)) + " px");
				}
				else if (!Inside(rect, own, 1f))
					problems.Add(name + " lies outside " + window.name + " by " + Mathf.RoundToInt(Mathf.Max(Mathf.Max(own.xMin - rect.xMin, rect.xMax - own.xMax), Mathf.Max(own.yMin - rect.yMin, rect.yMax - own.yMax))) + " px");
			}
			return problems;
		}

		/// <summary>A rect's corners in another's local space (the whole screen's, for a canvas root).</summary>
		static UnityEngine.Rect LocalRect(RectTransform r, RectTransform space)
		{
			var c = new Vector3[4];
			r.GetWorldCorners(c);
			if (space != null) for (int i = 0; i < 4; i++) c[i] = space.InverseTransformPoint(c[i]);
			return UnityEngine.Rect.MinMaxRect(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y), Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
		}

		static bool Inside(UnityEngine.Rect inner, UnityEngine.Rect outer, float slack)
		{
			return inner.xMin >= outer.xMin - slack && inner.yMin >= outer.yMin - slack && inner.xMax <= outer.xMax + slack && inner.yMax <= outer.yMax + slack;
		}

		/// <summary>The scale a canvas scaler gives a screen of this size (as CanvasScaler works it out).</summary>
		public static float ScaleFor(CanvasScaler scaler, Vector2 screen)
		{
			if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize ? scaler.scaleFactor : 1f;
			Vector2 reference = scaler.referenceResolution;
			if (reference.x <= 0f || reference.y <= 0f) return 1f;
			float w = screen.x / reference.x, h = screen.y / reference.y;
			switch (scaler.screenMatchMode)
			{
				case CanvasScaler.ScreenMatchMode.Expand: return Mathf.Min(w, h);
				case CanvasScaler.ScreenMatchMode.Shrink: return Mathf.Max(w, h);
				default: return Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(w, 2f), Mathf.Log(h, 2f), scaler.matchWidthOrHeight));
			}
		}

		#endregion

		#region Controls

		public static Text Label(Transform parent, string text, int size = FontSize, Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, string name = "Label")
		{
			RectTransform r = Rect(name, parent);
			var t = r.gameObject.AddComponent<Text>();
			Color c = color ?? TextColor;
			// Raft's body font is already bold
			t.font = Font; t.fontSize = size; t.color = c; t.alignment = anchor; t.fontStyle = font != null && style == FontStyle.Bold ? FontStyle.Normal : style;
			t.text = text; t.supportRichText = true; t.raycastTarget = false;
			t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
			Ensure<LayoutElement>(r.gameObject).flexibleWidth = 1; // shares a row's free space
			// A bold label in the accent colour, large or in capitals, is a heading: Raft's title font and colour, larger, with its shadow
			if (color.HasValue && c == Accent && style == FontStyle.Bold && (size >= 16 || (text ?? "").Any(char.IsLetter) && text == text.ToUpperInvariant())) Heading(t, size);
			else if (c.grayscale > 0.5f) TextShade(t, size >= 18 ? 1.5f : 1f);
			return t;
		}

		/// <summary>Keeps a long text inside its box: it wraps, then shrinks down to minSize (default two thirds of its size) instead of spilling out.</summary>
		public static Text Fit(Text t, int minSize = 0)
		{
			int max = t.fontSize;
			if (minSize <= 0) minSize = Mathf.Max(8, Mathf.RoundToInt(max * 0.65f));
			t.resizeTextForBestFit = true; t.resizeTextMaxSize = max; t.resizeTextMinSize = Mathf.Min(minSize, max);
			t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
			return t;
		}

		/// <summary>Makes a text a heading in Raft's style (the title font a third larger, tan, dark shadow).</summary>
		public static Text Heading(Text t, int size)
		{
			UseTitleFont(t);
			if (t.font == TitleFont && TitleFont != Font) t.fontSize = Mathf.RoundToInt(size * 1.35f);
			t.color = Tan;
			TextShade(t, size >= 20 ? 2f : 1.5f);
			return t;
		}

		/// <summary>A panel: Raft's brown grunge surface with a dark rim, vertical layout.</summary>
		public static RectTransform Panel(Transform parent, string name, RectOffset padding = null, float spacing = 8f, bool fitHeight = true)
		{
			RectTransform r = Rect(name, parent);
			Surface(r);
			Vertical(r.gameObject, spacing, padding ?? new RectOffset(10, 10, 10, 10), fitHeight);
			return r;
		}

		/// <summary>
		/// A group of related controls in a darker well, with a small title on top, e.g. "SCULPT". Returns the
		/// content area (vertical layout) to add rows to.
		/// </summary>
		public static RectTransform Group(Transform parent, string title, string name = null)
		{
			RectTransform box = Rect(name ?? ("Group_" + title), parent);
			Background(box.gameObject, GroupBg, 6);
			Vertical(box.gameObject, 6f, new RectOffset(8, 8, 5, 8));
			if (!string.IsNullOrEmpty(title))
			{
				Text t = Label(box, title.ToUpperInvariant(), 12, Tan, TextAnchor.MiddleLeft, FontStyle.Normal, "Title");
				UseTitleFont(t);
				if (t.font == TitleFont && TitleFont != Font) t.fontSize = 17;
				Size(t.gameObject, -1, 18);
			}
			Border(box, GroupBorder, 6, 1f);
			return box;
		}

		/// <summary>The looks a button can have.</summary>
		public enum Look { Plain, Choice, Chosen, Primary, Danger, Slot }

		/// <summary>A button: Raft's tan button with dark lettering in its title font, optional hint for the status bar.</summary>
		public static Button Button(Transform parent, string label, Action onClick, string hint = null, float width = -1, float height = RowHeight, int fontSize = FontSize)
		{
			RectTransform r = Rect("Button_" + label, parent);
			Image img = Background(r.gameObject, Color.white, 6);
			var b = r.gameObject.AddComponent<Button>();
			Register(b);
			b.targetGraphic = img;
			// Keyboard focus would keep the "selected" look and steal Space/Enter; the editor is mouse-driven
			var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
			Text t = Label(r, label, fontSize, AccentText, TextAnchor.MiddleCenter, FontStyle.Normal, "Text");
			Stretch(t.rectTransform, 6, 6, 1, 1);
			UseTitleFont(t);
			// Raft's title font is narrow capitals: a size larger reads like the body font, and shrinks to fit
			int max = t.font == TitleFont && TitleFont != Font ? Mathf.RoundToInt(fontSize * 1.3f) : fontSize;
			t.fontSize = max;
			t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Min(10, max); t.resizeTextMaxSize = max;
			t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
			Border(r, ButtonBorder, 6, 1f);
			Size(r.gameObject, width, height);
			SetLook(b, Look.Plain);
			// (a click leaves no keyboard focus behind: Space - the editor camera's "up" - would press the button again)
			b.onClick.AddListener(() => { if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == b.gameObject) EventSystem.current.SetSelectedGameObject(null); });
			if (onClick != null) b.onClick.AddListener(() => onClick());
			if (hint != null) Hint(r.gameObject, hint);
			return b;
		}

		/// <summary>A copy of a button made by Button() (its look, label and parts) under the parent, with its own click: much
		/// cheaper than building one from scratch when a list has a thousand rows (the Islands window took 3-6 s with 1,000
		/// islands). The label's text is set and its font and size (fontSize as given to Button()) picked again as Button() does.</summary>
		public static Button CopyButton(Button template, Transform parent, string label, Action onClick, int fontSize = FontSize)
		{
			GameObject go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
			go.name = "Button_" + label;
			var b = go.GetComponent<Button>();
			b.onClick = new Button.ButtonClickedEvent();
			Register(b);
			b.onClick.AddListener(() => { if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == b.gameObject) EventSystem.current.SetSelectedGameObject(null); });
			if (onClick != null) b.onClick.AddListener(() => onClick());
			Text t = LabelOf(b);
			if (t != null)
			{
				t.text = label;
				UseTitleFont(t);
				int max = t.font == TitleFont && TitleFont != Font ? Mathf.RoundToInt(fontSize * 1.3f) : fontSize;
				t.fontSize = max; t.resizeTextMaxSize = max; t.resizeTextMinSize = Mathf.Min(t.resizeTextMinSize, max);
			}
			return b;
		}

		/// <summary>Every button the mod made (destroyed ones become null): the button test checks each one was pressed.</summary>
		public static readonly List<Button> AllButtons = new List<Button>();
		static int pruneAt = 256;

		/// <summary>Adds a button to AllButtons, dropping destroyed ones now and then (windows rebuild their rows and the
		/// object browser its tiles: the list otherwise grew with every rebuild for as long as Raft ran).</summary>
		public static void Register(Button b)
		{
			AllButtons.Add(b);
			if (AllButtons.Count < pruneAt) return;
			AllButtons.RemoveAll(x => x == null);
			pruneAt = Math.Max(256, AllButtons.Count * 2);
		}

		public static Text LabelOf(Button b) { Transform t = b.transform.Find("Text"); return t != null ? t.GetComponent<Text>() : null; }

		/// <summary>Shows a button as the chosen one of a group (Raft's light tab) or as one not chosen (a dark tab).</summary>
		public static void SetActive(Button b, bool active) { SetLook(b, active ? Look.Chosen : Look.Choice); }

		/// <summary>The main button of a window (Save, OK): Raft's green craft button.</summary>
		public static void Primary(Button b) { SetLook(b, Look.Primary); }

		/// <summary>A button that removes or deletes something: Raft's red delete button.</summary>
		public static void DangerButton(Button b) { SetLook(b, Look.Danger); }

		/// <summary>A dark button (list headers and the like).</summary>
		public static void Flat(Button b) { SetLook(b, Look.Choice); }

		/// <summary>A tile of a grid (objects, items): Raft's inventory slot, with dark lettering.</summary>
		public static void Slot(Button b) { SetLook(b, Look.Slot); }

		/// <summary>Gives a button one of the looks (sprite, colours for normal / hover / pressed, lettering).</summary>
		public static void SetLook(Button b, Look look)
		{
			if (b == null) return;
			Image img = b.targetGraphic as Image;
			Sprite sprite = null;
			float ppu = 1f;
			switch (look)
			{
				case Look.Plain: sprite = RaftSprite("BigButtonNormal"); ppu = 3f; break;
				case Look.Chosen: sprite = RaftSprite("BigButtonHighlighted") ?? RaftSprite("BigButtonNormal"); ppu = 3f; break;
				case Look.Primary: sprite = RaftSprite("CraftButtonNormal"); break;
				case Look.Danger: sprite = RaftSprite("DeleteButtonNorma"); ppu = 3f; break;
				case Look.Slot: sprite = RaftSprite("ItemBG_wo_Frame"); break;
				case Look.Choice: sprite = RaftSprite("RoundedSquare_5px"); break;
			}
			// Without Raft's sprite: a rounded shape in the colour the sprite has
			Color fill = Color.white;
			if (sprite == null)
			{
				sprite = Rounded(6);
				ppu = 1f;
				fill = look == Look.Primary ? new Color(0.45f, 0.6f, 0.2f) : look == Look.Danger ? new Color(0.72f, 0.3f, 0.2f) : look == Look.Choice ? Color.white : Tan;
			}
			if (img != null)
			{
				img.sprite = sprite;
				img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
				img.pixelsPerUnitMultiplier = ppu;
				img.color = fill;
			}
			ColorBlock cb = b.colors;
			if (look == Look.Choice)
			{
				cb.normalColor = ButtonBg; cb.highlightedColor = ButtonHover; cb.pressedColor = ButtonPressed;
			}
			else if (look == Look.Chosen)
			{
				cb.normalColor = new Color(1f, 0.96f, 0.82f); cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.8f, 0.75f, 0.66f);
			}
			else
			{
				cb.normalColor = new Color(0.86f, 0.83f, 0.78f); cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.74f, 0.7f, 0.64f);
			}
			cb.selectedColor = cb.normalColor;
			cb.disabledColor = new Color(cb.normalColor.r * 0.7f, cb.normalColor.g * 0.7f, cb.normalColor.b * 0.7f, 0.5f);
			cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
			b.colors = cb;

			Transform border = b.transform.Find("Border");
			if (border != null) border.GetComponent<Image>().color = look == Look.Choice ? ButtonBorder : look == Look.Slot ? new Color(0.23f, 0.13f, 0.06f, 0.5f) : Color.clear;

			Text t = LabelOf(b);
			if (t == null) return;
			Shadow s = Ensure<Shadow>(t.gameObject);
			switch (look)
			{
				case Look.Choice:
					t.color = Tan; s.effectColor = TextShadow; break;
				case Look.Primary:
					t.color = new Color(1f, 0.97f, 0.86f); s.effectColor = new Color(0.12f, 0.2f, 0.03f, 0.9f); break;
				case Look.Danger:
					t.color = new Color(1f, 0.93f, 0.84f); s.effectColor = new Color(0.3f, 0.07f, 0.03f, 0.9f); break;
				default:
					t.color = look == Look.Slot ? SlotText : AccentText; s.effectColor = Color.clear; break; // dark lettering on tan needs no shadow
			}
			s.effectDistance = new Vector2(1f, -1f);
		}

		/// <summary>A small coloured square at the left of a button (texture swatches).</summary>
		public static Image Swatch(Button b, Color color)
		{
			RectTransform r = Rect("Swatch", b.transform);
			Anchor(r, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(14f, 14f));
			r.pivot = new Vector2(0f, 0.5f);
			var img = r.gameObject.AddComponent<Image>();
			img.sprite = Rounded(3); img.type = Image.Type.Sliced; img.color = color; img.raycastTarget = false;
			Text t = LabelOf(b);
			t.rectTransform.offsetMin = new Vector2(26f, t.rectTransform.offsetMin.y);
			t.alignment = TextAnchor.MiddleLeft;
			return img;
		}

		public class SliderRow
		{
			public Slider Slider;
			public Text Value;
		}

		/// <summary>"Label (?) ........ value" on one line, the slider below it. help: the text of the "?" mark after the label (none if null).</summary>
		public static SliderRow Slider(Transform parent, string label, float min, float max, float value, Func<float, string> format, Action<float> onChange, string hint = null, bool whole = false, string help = null)
		{
			RectTransform box = Rect("Slider_" + label, parent);
			Vertical(box.gameObject, 2f, new RectOffset(0, 0, 0, 0));
			RectTransform top = Row(box, 18, 4, "Top");
			Text l = Label(top, label, 14, TextColor);
			if (help != null)
			{
				// The mark right after the label's text, the value at the far right
				Ensure<LayoutElement>(l.gameObject).flexibleWidth = 0;
				Help(top, help, 16f);
			}
			Text v = Label(top, "", 14, Accent, TextAnchor.MiddleRight, FontStyle.Bold, "Value");

			RectTransform sr = Rect("Slider", box);
			Size(sr.gameObject, -1, 18);
			var slider = sr.gameObject.AddComponent<Slider>();
			RectTransform bg = Rect("Background", sr);
			bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f); bg.sizeDelta = new Vector2(0, 6); bg.anchoredPosition = Vector2.zero;
			Background(bg.gameObject, PanelRim, 3);
			RectTransform fillArea = Rect("Fill Area", sr);
			fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f); fillArea.sizeDelta = new Vector2(-12, 6); fillArea.anchoredPosition = new Vector2(-1, 0);
			RectTransform fill = Rect("Fill", fillArea);
			fill.sizeDelta = new Vector2(10, 0);
			Background(fill.gameObject, Tan, 3);
			RectTransform handleArea = Rect("Handle Slide Area", sr);
			Stretch(handleArea, 7, 7, 0, 0);
			RectTransform handle = Rect("Handle", handleArea);
			handle.sizeDelta = new Vector2(11, 0);
			Image hImg = Background(handle.gameObject, Color.white, 4);
			hImg.color = Tan;
			if (RaftSprite("Scrollbar") != null) { hImg.sprite = RaftSprite("Scrollbar"); hImg.type = Image.Type.Sliced; }
			slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = hImg;
			slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
			var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;
			slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole;
			slider.SetValueWithoutNotify(value);
			v.text = format(value);
			slider.onValueChanged.AddListener(x => { v.text = format(x); if (onChange != null) onChange(x); });
			if (hint != null) Hint(box.gameObject, hint);
			return new SliderRow { Slider = slider, Value = v };
		}

		public static InputField Field(Transform parent, string placeholder, string text = "", float height = RowHeight, string hint = null)
		{
			RectTransform r = Rect("Field", parent);
			Image img = Background(r.gameObject, FieldBg, 6);
			Border(r, ButtonBorder, 6, 1f);
			if (RaftSprite("RoundedSquare_5px") != null) img.sprite = RaftSprite("RoundedSquare_5px");
			Size(r.gameObject, -1, height);
			var f = r.gameObject.AddComponent<InputField>();
			f.targetGraphic = img;
			Text ph = Label(r, placeholder, FontSize, TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Placeholder");
			Stretch(ph.rectTransform, 10, 10, 0, 0);
			Text t = Label(r, "", FontSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Text");
			Stretch(t.rectTransform, 10, 10, 0, 0);
			t.supportRichText = false;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			f.textComponent = t; f.placeholder = ph;
			f.caretColor = Accent; f.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
			var nav = f.navigation; nav.mode = Navigation.Mode.None; f.navigation = nav;
			f.text = text;
			if (hint != null) Hint(r.gameObject, hint);
			return f;
		}

		/// <summary>A text field for several lines (Enter = new line), text from the top left, wrapped.</summary>
		public static InputField TextArea(Transform parent, string placeholder, float height, string hint = null)
		{
			InputField f = Field(parent, placeholder, "", height, hint);
			f.lineType = InputField.LineType.MultiLineNewline;
			foreach (Text t in new[] { f.textComponent, (Text)f.placeholder })
			{
				t.alignment = TextAnchor.UpperLeft;
				t.horizontalOverflow = HorizontalWrapMode.Wrap;
				t.verticalOverflow = VerticalWrapMode.Truncate;
				Stretch(t.rectTransform, 10, 10, 8, 8);
			}
			return f;
		}

		/// <summary>A small square button filled with a colour (colour pickers).</summary>
		public static Button ColorButton(Transform parent, Color color, Action onClick, string hint = null, float size = 24f)
		{
			RectTransform r = Rect("Color", parent);
			Image img = Background(r.gameObject, color, 4);
			var b = r.gameObject.AddComponent<Button>();
			Register(b);
			b.targetGraphic = img;
			ColorBlock cb = b.colors;
			cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); cb.selectedColor = Color.white;
			b.colors = cb;
			var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
			Border(r, ButtonBorder, 4, 1f);
			// A row of swatches may shrink them a little rather than grow wider than its panel
			Size(r.gameObject, size, size).minWidth = Mathf.Min(size, 12f);
			if (onClick != null) b.onClick.AddListener(() => onClick());
			if (hint != null) Hint(r.gameObject, hint);
			return b;
		}

		/// <summary>
		/// A compact tick-box row: a small box (ticked or not, SetCheck) and its label (LabelOf), on no background - the
		/// row lights up faintly under the mouse. For long lists of on/off settings where a full button each is too big.
		/// </summary>
		public static Button Check(Transform parent, string label, Action onClick, string hint = null, float height = 20f, int fontSize = 12)
		{
			RectTransform r = Rect("Check_" + label, parent);
			Image img = Background(r.gameObject, Accent, 4);
			var b = r.gameObject.AddComponent<Button>();
			Register(b);
			b.targetGraphic = img;
			ColorBlock cb = b.colors;
			cb.normalColor = new Color(1f, 1f, 1f, 0f); cb.highlightedColor = new Color(1f, 1f, 1f, 0.16f); cb.pressedColor = new Color(1f, 1f, 1f, 0.28f);
			cb.selectedColor = cb.normalColor; cb.disabledColor = cb.normalColor; cb.colorMultiplier = 1f; cb.fadeDuration = 0.06f;
			b.colors = cb;
			var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
			// (the box keeps its size, centred in the row; the label fills the rest)
			HorizontalLayoutGroup h = Horizontal(r.gameObject, 6f, new RectOffset(3, 4, 0, 0));
			h.childForceExpandWidth = false; h.childForceExpandHeight = false;
			// (18: still 10 px on a 1366 x 768 screen)
			float side = Mathf.Min(18f, height - 4f);
			RectTransform box = Rect("Box", r);
			Background(box.gameObject, FieldBg, 3);
			Border(box, Tan, 3, 1f);
			Size(box.gameObject, side, side);
			// (the tick is a filled square, not a glyph: Raft's fonts may lack one)
			RectTransform tick = Rect("Tick", box);
			Stretch(tick, 3, 3, 3, 3);
			Background(tick.gameObject, Accent, 2).raycastTarget = false;
			Text t = Label(r, label, fontSize, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Text");
			t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
			t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Min(9, fontSize); t.resizeTextMaxSize = fontSize;
			Size(t.gameObject, -1, height);
			Size(r.gameObject, -1, height);
			b.onClick.AddListener(() => { if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == b.gameObject) EventSystem.current.SetSelectedGameObject(null); });
			if (onClick != null) b.onClick.AddListener(() => onClick());
			if (hint != null) Hint(r.gameObject, hint);
			return b;
		}

		/// <summary>Shows a Check row ticked or not; one that can't be clicked (interactable false) is dimmed.</summary>
		public static void SetCheck(Button b, bool on)
		{
			if (b == null) return;
			Transform tick = b.transform.Find("Box/Tick");
			if (tick != null) tick.gameObject.SetActive(on);
			Transform box = b.transform.Find("Box");
			Image fill = box != null ? box.GetComponent<Image>() : null;
			if (fill != null) fill.color = on ? new Color(0.45f, 0.29f, 0.13f, 1f) : FieldBg;
			Text t = LabelOf(b);
			if (t != null) t.color = b.interactable ? (on ? Accent : TextColor) : new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.6f);
		}

		/// <summary>Whether a Check row shows ticked (tests).</summary>
		public static bool IsChecked(Button b)
		{
			Transform tick = b != null ? b.transform.Find("Box/Tick") : null;
			return tick != null && tick.gameObject.activeSelf;
		}

		/// <summary>A thin horizontal line between sections.</summary>
		public static void Separator(Transform parent)
		{
			RectTransform r = Rect("Separator", parent);
			var img = r.gameObject.AddComponent<Image>();
			img.color = new Color(Tan.r, Tan.g, Tan.b, 0.45f); img.raycastTarget = false;
			Size(r.gameObject, -1, 2);
		}

		/// <summary>A vertical scroll list; returns the content (vertical layout) to fill.</summary>
		public static RectTransform ScrollList(Transform parent, out ScrollRect scroll, float spacing = 4f)
		{
			RectTransform view = Rect("Scroll", parent);
			var bg = view.gameObject.AddComponent<Image>();
			bg.color = new Color(0, 0, 0, 0.001f); // catches the mouse wheel over gaps
			scroll = view.gameObject.AddComponent<ScrollRect>();
			scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
			scroll.inertia = false;

			RectTransform viewport = Rect("Viewport", view);
			Stretch(viewport, 0, 10, 0, 0);
			viewport.gameObject.AddComponent<RectMask2D>();
			RectTransform content = Rect("Content", viewport);
			content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
			content.offsetMin = content.offsetMax = Vector2.zero;
			Vertical(content.gameObject, spacing, new RectOffset(0, 2, 0, 4), true);

			RectTransform bar = Rect("Scrollbar", view);
			bar.anchorMin = new Vector2(1, 0); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(1, 0.5f);
			bar.sizeDelta = new Vector2(6, 0); bar.anchoredPosition = Vector2.zero;
			Background(bar.gameObject, new Color(0.23f, 0.13f, 0.06f, 0.6f), 3);
			var sb = bar.gameObject.AddComponent<Scrollbar>();
			sb.direction = Scrollbar.Direction.BottomToTop;
			RectTransform slide = Rect("Sliding Area", bar);
			Stretch(slide);
			RectTransform handle = Rect("Handle", slide);
			Stretch(handle);
			sb.handleRect = handle;
			sb.targetGraphic = Background(handle.gameObject, Tan, 3);
			var nav = sb.navigation; nav.mode = Navigation.Mode.None; sb.navigation = nav;

			scroll.viewport = viewport; scroll.content = content;
			scroll.verticalScrollbar = sb;
			scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			return content;
		}

		/// <summary>
		/// A panel as tall as its content, but never taller than the screen below it: then it scrolls. Raft's surface is
		/// the frame (named name + "Frame"; anchor and size its width like any rect, top-left pivot); its content area
		/// (named name, vertical layout) is returned. bottomMargin: space kept free under the panel (the status bar).
		/// Whatever is too wide for the panel is cut off at its edge instead of spilling over it.
		/// </summary>
		public static RectTransform ScrollPanel(Transform parent, string name, RectOffset padding, float spacing, float bottomMargin)
		{
			RectTransform frame = Rect(name + "Frame", parent);
			Surface(frame);
			var scroll = frame.gameObject.AddComponent<ScrollRect>();
			scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f; scroll.inertia = false;

			// (room for the scrollbar is always kept at the right, so rows don't change width when it shows)
			RectTransform viewport = Rect("Viewport", frame);
			Stretch(viewport, 0, 8, 0, 0);
			viewport.gameObject.AddComponent<RectMask2D>();
			RectTransform content = Rect(name, viewport);
			content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
			content.offsetMin = content.offsetMax = Vector2.zero;
			Vertical(content.gameObject, spacing, padding, true);

			RectTransform bar = Rect("Scrollbar", frame);
			bar.anchorMin = new Vector2(1, 0); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(1, 0.5f);
			bar.sizeDelta = new Vector2(5, -16); bar.anchoredPosition = new Vector2(-3, 0);
			Background(bar.gameObject, new Color(0.23f, 0.13f, 0.06f, 0.6f), 3);
			var sb = bar.gameObject.AddComponent<Scrollbar>();
			sb.direction = Scrollbar.Direction.BottomToTop;
			RectTransform slide = Rect("Sliding Area", bar);
			Stretch(slide);
			RectTransform handle = Rect("Handle", slide);
			Stretch(handle);
			sb.handleRect = handle;
			sb.targetGraphic = Background(handle.gameObject, Tan, 3);
			var nav = sb.navigation; nav.mode = Navigation.Mode.None; sb.navigation = nav;

			scroll.viewport = viewport; scroll.content = content;
			scroll.verticalScrollbar = sb;
			scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			var fit = frame.gameObject.AddComponent<FitToContent>();
			fit.Content = content; fit.BottomMargin = bottomMargin;
			return content;
		}

		/// <summary>Keeps a ScrollPanel's frame as tall as its content, up to the space left below its top on the canvas.</summary>
		public class FitToContent : MonoBehaviour
		{
			public RectTransform Content;
			public float BottomMargin;

			void LateUpdate()
			{
				var self = (RectTransform)transform;
				var parent = self.parent as RectTransform;
				if (Content == null || parent == null) return;
				// (anchored at the top: its top is this far below the parent's top)
				float top = -self.anchoredPosition.y + (1f - self.anchorMax.y) * parent.rect.height;
				float available = Mathf.Max(60f, parent.rect.height - top - BottomMargin);
				float h = Mathf.Min(Content.rect.height, available);
				if (Mathf.Abs(self.sizeDelta.y - h) > 0.5f) self.sizeDelta = new Vector2(self.sizeDelta.x, h);
			}
		}

		/// <summary>A picture (a texture drawn as is, e.g. a preview map or a thumbnail) of a fixed size, or filling a row's width when width &lt; 0.</summary>
		public static RawImage Picture(Transform parent, Texture texture, float width, float height, string name = "Picture")
		{
			RectTransform r = Rect(name, parent);
			var img = r.gameObject.AddComponent<RawImage>();
			img.texture = texture;
			img.raycastTarget = false;
			Size(r.gameObject, width, height);
			return img;
		}

		/// <summary>A row of tab buttons; the chosen one shows light (SetActive). onSelect gets the tab's index.</summary>
		public static Button[] Tabs(Transform parent, string[] labels, string[] hints, Action<int> onSelect, float height = 32f, int fontSize = FontSize)
		{
			RectTransform row = Row(parent, height, 4f, "Tabs");
			var buttons = new Button[labels.Length];
			for (int i = 0; i < labels.Length; i++)
			{
				int index = i;
				buttons[i] = Button(row, labels[i], () => onSelect(index), hints != null && i < hints.Length ? hints[i] : null, -1, height, fontSize);
			}
			return buttons;
		}

		#endregion

		#region Help marks ("?" with a popup)

		static RectTransform helpPopup;
		static Text helpText;
		static HelpMark helpShownBy;

		/// <summary>
		/// A small round "?" (put it after a setting's label): hovering it shows a popup next to it with a few sentences
		/// about the setting - what it does, its range, tips. Clicking it (touch screens) shows the popup too, until the
		/// mouse moves off it, another click, or a few seconds pass.
		/// </summary>
		public static Button Help(Transform parent, string text, float size = 18f)
		{
			RectTransform r = Rect("Help", parent);
			Image img = Background(r.gameObject, new Color(Tan.r, Tan.g, Tan.b, 0.9f), Mathf.RoundToInt(size / 2f));
			var b = r.gameObject.AddComponent<Button>();
			Register(b);
			b.targetGraphic = img;
			var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
			ColorBlock cb = b.colors;
			cb.normalColor = Color.white; cb.highlightedColor = new Color(1.2f, 1.15f, 1f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); cb.selectedColor = Color.white;
			b.colors = cb;
			Text t = Label(r, "?", Mathf.RoundToInt(size * 0.75f), AccentText, TextAnchor.MiddleCenter, FontStyle.Bold, "Text");
			Stretch(t.rectTransform);
			Size(r.gameObject, size, size);
			var mark = r.gameObject.AddComponent<HelpMark>();
			mark.Text = text;
			b.onClick.AddListener(() => mark.Toggle());
			return b;
		}

		/// <summary>The help text shown now (null when no popup is open): the tests check that popups close.</summary>
		/// <summary>Tests: the popup itself (where it is on the screen).</summary>
		public static RectTransform HelpPopup { get { return helpPopup; } }

		public static string ShownHelp { get { return helpPopup != null && helpPopup.gameObject.activeSelf ? helpText.text : null; } }

		public class HelpMark : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
		{
			public string Text;
			float pinnedUntil;

			public void OnPointerEnter(PointerEventData e) { ShowHelp(this); if (HintChanged != null) HintChanged("Help: move the mouse off the ? to close it"); }
			public void OnPointerExit(PointerEventData e) { pinnedUntil = 0f; HideHelp(this); if (HintChanged != null) HintChanged(null); }

			/// <summary>A click (or tap) shows the popup for a while, or closes it when it is open.</summary>
			public void Toggle()
			{
				if (helpShownBy == this && pinnedUntil > Time.unscaledTime) { pinnedUntil = 0f; HideHelp(this); return; }
				pinnedUntil = Time.unscaledTime + 8f;
				ShowHelp(this);
			}

			void Update()
			{
				if (helpShownBy != this || pinnedUntil <= 0f) return;
				// A pinned popup closes after a while, or with the next click anywhere else
				if (Time.unscaledTime > pinnedUntil || (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, Input.mousePosition, null)))
				{
					pinnedUntil = 0f;
					HideHelp(this);
				}
			}

			void OnDisable() { pinnedUntil = 0f; HideHelp(this); }
		}

		static void ShowHelp(HelpMark mark)
		{
			if (helpPopup == null) BuildHelpPopup();
			helpShownBy = mark;
			helpText.text = mark.Text;
			helpPopup.gameObject.SetActive(true);
			helpPopup.SetAsLastSibling();
			PlaceHelp(mark);
		}

		/// <summary>
		/// Puts the popup next to its mark (right and below), kept on the screen. Again every frame while it shows
		/// (HelpPlacer): the first popup of a session is placed before its new canvas has scaled to the screen, and would
		/// otherwise land far off it; a scrolled list or another screen size moves the mark too.
		/// </summary>
		static void PlaceHelp(HelpMark mark)
		{
			if (helpPopup == null || mark == null) return;
			LayoutRebuilder.ForceRebuildLayoutImmediate(helpPopup);
			var canvas = (RectTransform)helpPopup.parent;
			Vector3[] c = new Vector3[4];
			((RectTransform)mark.transform).GetWorldCorners(c);
			Vector2 local;
			Camera cam = null;
			Canvas markCanvas = mark.GetComponentInParent<Canvas>();
			if (markCanvas != null && markCanvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = markCanvas.worldCamera;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, RectTransformUtility.WorldToScreenPoint(cam, c[3]), null, out local);
			// (its height for this text: the rect can still hold the last text's until the canvas updates)
			Vector2 size = new Vector2(helpPopup.rect.width, Mathf.Max(helpPopup.rect.height, LayoutUtility.GetPreferredHeight(helpPopup)));
			Rect area = canvas.rect;
			float x = Mathf.Clamp(local.x + 6f, area.xMin + 4f, area.xMax - size.x - 4f);
			float y = local.y - 4f;
			if (y - size.y < area.yMin + 4f) y = local.y + ((RectTransform)mark.transform).rect.height + size.y + 8f; // above it, when there's no room below
			y = Mathf.Clamp(y, area.yMin + size.y + 4f, area.yMax - 4f);
			helpPopup.anchoredPosition = new Vector2(x - area.xMin, y - area.yMax);
		}

		/// <summary>Keeps the shown popup next to its mark.</summary>
		class HelpPlacer : MonoBehaviour
		{
			void LateUpdate() { if (helpShownBy != null && helpPopup != null && helpPopup.gameObject.activeSelf) PlaceHelp(helpShownBy); PlaceTip(); }
		}

		static void HideHelp(HelpMark mark)
		{
			if (helpShownBy != mark) return;
			helpShownBy = null;
			if (helpPopup != null) helpPopup.gameObject.SetActive(false);
		}

		/// <summary>The one popup all "?" marks share, on its own canvas above every window (it never catches the mouse).</summary>
		static void BuildHelpPopup()
		{
			Canvas canvas = CreateCanvas("CustomIslandsHelp", 900);
			canvas.GetComponent<GraphicRaycaster>().enabled = false;
			canvas.gameObject.AddComponent<HelpPlacer>();
			helpPopup = Panel(canvas.transform, "HelpPopup", new RectOffset(12, 12, 9, 11), 4f);
			helpPopup.anchorMin = helpPopup.anchorMax = new Vector2(0, 1);
			helpPopup.pivot = new Vector2(0, 1);
			helpPopup.sizeDelta = new Vector2(330, 0);
			helpText = Label(helpPopup, "", 14, TextColor);
			helpText.lineSpacing = 1.05f;
			foreach (Graphic g in helpPopup.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
			helpPopup.gameObject.SetActive(false);
			// (the hover tip, for controls where no status bar shows their hint: smaller, as wide as its text up to 300)
			tipPopup = Panel(canvas.transform, "HintTip", new RectOffset(9, 9, 6, 7), 2f);
			tipPopup.anchorMin = tipPopup.anchorMax = new Vector2(0, 1);
			tipPopup.pivot = new Vector2(0, 1);
			tipPopup.sizeDelta = new Vector2(300, 0);
			tipText = Label(tipPopup, "", 12, TextColor);
			foreach (Graphic g in tipPopup.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
			tipPopup.gameObject.SetActive(false);
		}

		#endregion

		#region Hints (status bar text while hovering)

		/// <summary>Raised when the mouse enters (text) or leaves (null) a control with a hint.</summary>
		public static event Action<string> HintChanged;

		/// <summary>
		/// Windows that show hints in a bar of their own (the editor's status bar, the in-world window's hint line) say here
		/// when that bar is on the screen. Anywhere else - the main menu's windows, the stats page, the alpha box - a
		/// control's hint shows in a small tip by the mouse after a moment (until 2026-10-15 those hints were never drawn).
		/// </summary>
		public static readonly List<Func<bool>> HintBars = new List<Func<bool>>();

		static bool HintBarShown()
		{
			foreach (Func<bool> shown in HintBars) { try { if (shown()) return true; } catch { } }
			return false;
		}

		public static void Hint(GameObject go, string hint)
		{
			var h = Ensure<HintTarget>(go);
			h.Text = hint;
		}

		public class HintTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
		{
			public string Text;
			public void OnPointerEnter(PointerEventData e) { if (HintChanged != null) HintChanged(Text); if (!HintBarShown()) ShowTip(this); }
			public void OnPointerExit(PointerEventData e) { if (HintChanged != null) HintChanged(null); HideTip(this); }
			void OnDisable() { if (HintChanged != null) HintChanged(null); HideTip(this); }
		}

		static RectTransform tipPopup;
		static Text tipText;
		static HintTarget tipShownBy;
		static float tipAt;

		/// <summary>The hover tip's text while it shows (null when none): the tests read it.</summary>
		public static string ShownTip { get { return tipPopup != null && tipPopup.gameObject.activeSelf ? tipText.text : null; } }
		/// <summary>Tests: the tip itself (where it is on the screen).</summary>
		public static RectTransform HintTip { get { return tipPopup; } }

		/// <summary>The tip comes once the mouse has rested on the control a moment (HelpPlacer shows and places it).</summary>
		static void ShowTip(HintTarget t)
		{
			if (string.IsNullOrEmpty(t.Text)) return;
			if (helpPopup == null) BuildHelpPopup();
			tipShownBy = t;
			tipAt = Time.unscaledTime + 0.6f;
			tipText.text = t.Text;
			tipPopup.gameObject.SetActive(false);
		}

		static void HideTip(HintTarget t)
		{
			if (tipShownBy != t) return;
			tipShownBy = null;
			if (tipPopup != null) tipPopup.gameObject.SetActive(false);
		}

		/// <summary>The tip below and right of the mouse, kept on the screen; as wide as its text, up to 300.</summary>
		static void PlaceTip()
		{
			if (tipShownBy == null || tipPopup == null) return;
			if (!tipShownBy.isActiveAndEnabled) { HideTip(tipShownBy); return; }
			if (Time.unscaledTime < tipAt) return;
			if (!tipPopup.gameObject.activeSelf)
			{
				tipPopup.sizeDelta = new Vector2(Mathf.Min(300f, tipText.preferredWidth + 20f), 0);
				tipPopup.gameObject.SetActive(true);
				tipPopup.SetAsLastSibling();
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(tipPopup);
			var canvas = (RectTransform)tipPopup.parent;
			Vector2 local;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, Input.mousePosition, null, out local);
			Vector2 size = new Vector2(tipPopup.rect.width, Mathf.Max(tipPopup.rect.height, LayoutUtility.GetPreferredHeight(tipPopup)));
			Rect area = canvas.rect;
			float x = Mathf.Clamp(local.x + 14f, area.xMin + 4f, area.xMax - size.x - 4f);
			float y = local.y - 22f;
			if (y - size.y < area.yMin + 4f) y = local.y + size.y + 8f; // above the mouse, when there's no room below
			y = Mathf.Clamp(y, area.yMin + size.y + 4f, area.yMax - 4f);
			tipPopup.anchoredPosition = new Vector2(x - area.xMin, y - area.yMax);
		}

		#endregion

		/// <summary>An overlay canvas that scales with the screen and always fits a 1400 x 800 layout.</summary>
		public static Canvas CreateCanvas(string name, int sortingOrder)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			go.layer = 5;
			var canvas = go.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;
			canvas.pixelPerfect = false;
			var scaler = go.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1400, 800);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // the smaller of the two scales, so 1400 x 800 always fits
			scaler.referencePixelsPerUnit = 100;
			return canvas;
		}
	}
}
