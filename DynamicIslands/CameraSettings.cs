using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The editor camera's settings (TODO §5 "Editor camera: modern controls and settings"): look sensitivity, what dragging
	/// up does while looking, the flying speed, zoom and pan speed, and the wheel's direction. This PC's own, kept in
	/// Mods\DynamicIslands\world_rules.txt (camera...= lines) beside the mod's keys. Changed in the editor (the status bar's
	/// Camera button: CameraSettingsWindow) and in Defaults (Editor camera); the flying speed also with the right mouse +
	/// wheel. A change is saved a moment after the last one (a dragged slider writes the file once), and when a window or
	/// the editor closes.
	/// </summary>
	public static class CameraSettings
	{
		/// <summary>A number setting: a multiplier of the camera's own speed (1 = as the mod has it).</summary>
		public class Number
		{
			public string Key, Label, Hint;
			public float Min, Max;
			public const float Default = 1f;
		}

		public static readonly Number LookNumber = new Number { Key = "cameralook", Label = "Look sensitivity", Min = 0.2f, Max = 4f,
			Hint = "How fast the view turns while the right mouse is held (and while orbiting with Alt + left mouse): x1 as the mod has it" };
		public static readonly Number SpeedNumber = new Number { Key = "cameraspeed", Label = "Move speed", Min = 0.1f, Max = 10f,
			Hint = "How fast WASD (and Q/E, Space, C) move the camera: x1 as the mod has it. Right mouse + wheel changes it too" };
		public static readonly Number ZoomNumber = new Number { Key = "camerazoom", Label = "Zoom speed", Min = 0.25f, Max = 4f,
			Hint = "How far one notch of the wheel (and Alt + right-drag) zooms: x1 = a fifth of the way to what is under the cursor" };
		public static readonly Number PanNumber = new Number { Key = "camerapan", Label = "Pan speed", Min = 0.25f, Max = 4f,
			Hint = "How far middle-drag slides the view: x1 = the ground under the cursor sticks to it" };
		public static readonly Number[] Numbers = { LookNumber, SpeedNumber, ZoomNumber, PanNumber };

		public const string DragUpKey = "cameradragup", WheelKey = "camerawheel";

		static readonly Dictionary<string, float> values = new Dictionary<string, float>();
		static readonly HashSet<string> dirty = new HashSet<string>();
		static bool? dragUpLooksUp, wheelReversed;
		static float saveAt = -1f;

		public static float Look { get { return Get(LookNumber); } }
		public static float Speed { get { return Get(SpeedNumber); } }
		public static float Zoom { get { return Get(ZoomNumber); } }
		public static float Pan { get { return Get(PanNumber); } }

		/// <summary>Dragging up while looking tilts the view up, the mod's own way since 2026-10-21 (the user asked for it on by default;
		/// off: down, as if the scene were dragged - the default from 2026-10-02). Only a saved "down" turns it off.</summary>
		public static bool DragUpLooksUp
		{
			get { if (dragUpLooksUp == null) dragUpLooksUp = !string.Equals(WorldRules.ReadDefault(DragUpKey), "down", StringComparison.OrdinalIgnoreCase); return dragUpLooksUp.Value; }
			set { dragUpLooksUp = value; Mark(DragUpKey); }
		}

		/// <summary>The wheel turned the other way: rolling it up zooms out (and slows the flying down).</summary>
		public static bool WheelReversed
		{
			get { if (wheelReversed == null) wheelReversed = string.Equals(WorldRules.ReadDefault(WheelKey), "reversed", StringComparison.OrdinalIgnoreCase); return wheelReversed.Value; }
			set { wheelReversed = value; Mark(WheelKey); }
		}

		public static float Get(Number n)
		{
			float v;
			if (values.TryGetValue(n.Key, out v)) return v;
			v = Number.Default;
			string s = WorldRules.ReadDefault(n.Key);
			float read;
			if (!string.IsNullOrEmpty(s) && float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out read) && !float.IsNaN(read) && !float.IsInfinity(read)) v = Mathf.Clamp(read, n.Min, n.Max);
			values[n.Key] = v;
			return v;
		}

		/// <summary>Sets a number (kept within its range, two decimals); saved a moment later. Returns what it became.</summary>
		public static float Set(Number n, float v)
		{
			v = (float)Math.Round(Mathf.Clamp(v, n.Min, n.Max), 2);
			if (values.ContainsKey(n.Key) && Mathf.Approximately(values[n.Key], v)) return v;
			values[n.Key] = v;
			Mark(n.Key);
			return v;
		}

		static void Mark(string key) { dirty.Add(key); saveAt = Time.unscaledTime + 0.6f; }

		/// <summary>Unsaved changes wait to be written.</summary>
		public static bool Unsaved { get { return dirty.Count > 0; } }

		/// <summary>Every frame (the editor camera, the windows): writes the changes once they stop for a moment.</summary>
		public static void Tick() { if (dirty.Count > 0 && Time.unscaledTime >= saveAt) Save(); }

		/// <summary>Writes the changed settings to world_rules.txt now.</summary>
		public static void Save()
		{
			if (dirty.Count == 0) return;
			var write = new Dictionary<string, string>();
			foreach (string key in dirty)
			{
				if (key == DragUpKey) write[key] = DragUpLooksUp ? "up" : "down";
				else if (key == WheelKey) write[key] = WheelReversed ? "reversed" : "normal";
				else write[key] = Format(values[key]);
			}
			dirty.Clear();
			saveAt = -1f;
			WorldRules.SaveDefaults(write);
		}

		/// <summary>Every camera setting back to the mod's own, saved at once.</summary>
		public static void Reset()
		{
			foreach (Number n in Numbers) { values[n.Key] = Number.Default; dirty.Add(n.Key); }
			dragUpLooksUp = true; dirty.Add(DragUpKey);
			wheelReversed = false; dirty.Add(WheelKey);
			Save();
		}

		/// <summary>Forgets what was read (tests: the file was changed behind it). Unsaved changes are written first.</summary>
		internal static void Reload()
		{
			Save();
			values.Clear();
			dragUpLooksUp = wheelReversed = null;
		}

		public static string Format(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

		/// <summary>
		/// The settings' controls (a slider each, and the two switches) in a group of a window; the window calls the action
		/// it returns to show the values again (after Mod's own, or the wheel changed the speed).
		/// </summary>
		public static Action Build(Transform group)
		{
			var sliders = new List<KeyValuePair<Number, UIKit.SliderRow>>();
			Action refresh = null;
			foreach (Number n in Numbers)
			{
				Number m = n;
				UIKit.SliderRow row = UIKit.Slider(group, n.Label, Mathf.Log(n.Min), Mathf.Log(n.Max), Mathf.Log(Get(n)), x => "x" + Format((float)Math.Round(Mathf.Exp(x), 2)), x => Set(m, Mathf.Exp(x)), n.Hint);
				row.Slider.name = "CameraSlider_" + n.Key;
				sliders.Add(new KeyValuePair<Number, UIKit.SliderRow>(n, row));
			}
			RectTransform flips = UIKit.Row(group, 28f, 6f, "Row_CameraSwitches");
			Button up = UIKit.Button(flips, "", () => { DragUpLooksUp = !DragUpLooksUp; refresh(); },
				"While the right mouse is held: dragging the mouse up tilts the view up (the mod's own way, as in most games and Unity) or down (as if the scene were dragged) - click to switch", -1, 28f, 13);
			up.name = "Camera_DragUp";
			Button wheel = UIKit.Button(flips, "", () => { WheelReversed = !WheelReversed; refresh(); },
				"The mouse wheel: rolling it up zooms in (and, with the right mouse held, flies faster), or the other way round - click to switch", -1, 28f, 13);
			wheel.name = "Camera_Wheel";
			refresh = () =>
			{
				foreach (var kv in sliders)
				{
					if (kv.Value.Slider == null) continue;
					float v = Get(kv.Key);
					kv.Value.Slider.SetValueWithoutNotify(Mathf.Log(v));
					kv.Value.Value.text = "x" + Format(v);
				}
				if (up != null) { UIKit.LabelOf(up).text = "Drag up: look " + (DragUpLooksUp ? "up" : "down"); UIKit.SetActive(up, DragUpLooksUp); }
				if (wheel != null) { UIKit.LabelOf(wheel).text = "Wheel up: zoom " + (WheelReversed ? "out" : "in"); UIKit.SetActive(wheel, WheelReversed); }
			};
			refresh();
			return refresh;
		}
	}

	/// <summary>
	/// The editor's camera window (the status bar's Camera button): every mouse button and key of the editor (ControlGroups), and the camera's settings
	/// (CameraSettings). A modal window like TextPromptWindow; Esc or Close shuts it and saves.
	/// </summary>
	public class CameraSettingsWindow : MonoBehaviour
	{
		static CameraSettingsWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		/// <summary>Tests: the window's root.</summary>
		internal static GameObject Root { get { return instance != null ? instance.gameObject : null; } }

		Action refresh;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("CameraSettingsWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<CameraSettingsWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.refresh();
		}

		public static void Close()
		{
			if (instance == null) return;
			if (instance.gameObject.activeSelf) EditorInput.SubWindowClosedFrame = Time.frameCount;
			instance.gameObject.SetActive(false);
			CameraSettings.Save();
		}

		/// <summary>Shows the values again if the window is open.</summary>
		public static void Refresh() { if (IsOpen) instance.refresh(); }

		void Update()
		{
			CameraSettings.Tick();
			if (EscKey.Down(this)) Close();
		}

		void OnDisable() { CameraSettings.Save(); }

		/// <summary>
		/// Every mouse button and key of the editor, for a new user: each group's title, then the control and what it does in
		/// pairs. The Camera group and the settings fill the left column, the others the right.
		/// </summary>
		internal static readonly string[][] ControlGroups =
		{
			new[] { "Camera",
				"Right mouse + drag", "look around",
				"Right mouse + WASD", "fly the way you look",
				"Right mouse + Q / E", "fly down / up",
				"Right mouse + wheel", "flying speed (kept)",
				"WASD / arrow keys", "move over the island",
				"Space / C", "straight up / straight down",
				"Middle mouse drag", "pan: the ground follows the mouse",
				"Wheel", "zoom towards the mouse",
				"Alt + left drag", "orbit around the selection",
				"Alt + right drag", "zoom in / out",
				"F", "frame the selection, or the island",
				"Shift (held)", "the camera three times faster" },
			new[] { "Anywhere in the editor",
				"F1 / F2 / F3", "the Terrain, Objects, Island tab",
				"Ctrl+Z", "undo",
				"Ctrl+Y, Ctrl+Shift+Z", "redo",
				"Ctrl+S / Ctrl+O", "save the island / open one",
				"Esc", "close a window, stop placing" },
			new[] { "Terrain tab",
				"Left mouse drag", "use the brush",
				"Shift + drag", "Noise: takes the bumps away",
				"Q / E", "Stamp: turn the stamp" },
			new[] { "Objects tab",
				"Left click", "place the picked object, or select",
				"Shift + click", "keep placing / add to the selection",
				"Ctrl + click", "take one out of the selection",
				"Left drag on the ground", "box select (Shift: add to it)",
				"Q / E,  [ / ]", "turn / resize while placing",
				"1 / 2 / 3 / 4", "handles: move / turn / scale / all",
				"X", "arrows: the world's or the object's own",
				"P", "turn around each one or the middle",
				"Ctrl (held)", "snap while dragging (0.25 m, 15°)",
				"Ctrl+D / Delete", "duplicate / delete the selection",
				"Ctrl+C / Ctrl+V", "copy / paste, also to another island",
				"Ctrl+A", "select everything" },
		};

		/// <summary>A group of the controls list: the control on the left in gold, what it does beside it.</summary>
		static void Controls(Transform column, string[] group)
		{
			RectTransform box = UIKit.Group(column, group[0], "Keys_" + group[0]);
			box.GetComponent<VerticalLayoutGroup>().spacing = 1f;
			for (int i = 1; i + 1 < group.Length; i += 2)
			{
				RectTransform row = UIKit.Row(box, 18f, 8f, "Key_" + group[i]);
				UIKit.Size(UIKit.Label(row, group[i], 13, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Normal, "Key").gameObject, 172);
				UIKit.Label(row, group[i + 1], 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Does");
			}
		}

		const float Width = 1040f, ColumnWidth = (Width - 32f - 12f) / 2f;

		/// <summary>A column of the window, half its width: its groups one under the other, from the top.</summary>
		static RectTransform Column(Transform parent, string name)
		{
			RectTransform c = UIKit.Rect(name, parent);
			UIKit.Vertical(c.gameObject, 8f, null).childAlignment = TextAnchor.UpperLeft;
			UIKit.Size(c.gameObject, ColumnWidth);
			return c;
		}

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 0));
			Text title = UIKit.Label(panel, "EDITOR CAMERA AND CONTROLS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.Size(title.gameObject, -1, 26);
			// the two columns side by side, as tall as the taller one
			RectTransform columns = UIKit.Rect("Controls", panel);
			HorizontalLayoutGroup h = UIKit.Horizontal(columns.gameObject, 12f);
			h.childAlignment = TextAnchor.UpperLeft; h.childForceExpandHeight = false;
			RectTransform left = Column(columns, "Column_Camera"), right = Column(columns, "Column_Editing");
			Controls(left, ControlGroups[0]);
			RectTransform group = UIKit.Group(left, "Settings (this PC; also in Defaults)", "Settings");
			refresh = CameraSettings.Build(group);
			for (int i = 1; i < ControlGroups.Length; i++) Controls(right, ControlGroups[i]);
			RectTransform buttons = UIKit.Row(panel, 32f, 8f, "Buttons");
			UIKit.Button(buttons, "Mod's own", () => { CameraSettings.Reset(); refresh(); EditorUI.Flash("The camera settings are the mod's own again"); }, "Every camera setting back to what a new install of Custom Islands has", 120, 32);
			UIKit.Size(UIKit.Label(buttons, "", 12, UIKit.TextMuted).gameObject, -1, -1, 1);
			Button close = UIKit.Button(buttons, "Close", Close, "Close (Esc); the settings are saved", 100, 32);
			UIKit.Primary(close);
		}
	}
}
