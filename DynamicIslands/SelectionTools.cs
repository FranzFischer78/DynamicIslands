using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeGizmos;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Selection tools (ROADMAP E3): box select (drag on empty ground in the Objects tab; Shift adds), select all
	/// (Ctrl+A), select the same kind, and hidden or locked objects (from the Placed objects list). Hidden objects aren't
	/// drawn and can't be picked; locked ones are drawn but can't be picked. Both are for editing only: the island saves
	/// and plays with every object, and opening another island clears them.
	/// </summary>
	public static class SelectionTools
	{
		static readonly Dictionary<EditorGameObject, List<Behaviour>> hiddenParts = new Dictionary<EditorGameObject, List<Behaviour>>();
		static readonly Dictionary<EditorGameObject, List<Renderer>> hiddenRenderers = new Dictionary<EditorGameObject, List<Renderer>>();
		static readonly HashSet<EditorGameObject> locked = new HashSet<EditorGameObject>();

		public static bool IsHidden(EditorGameObject o) { return o != null && hiddenRenderers.ContainsKey(o); }
		public static bool IsLocked(EditorGameObject o) { return o != null && locked.Contains(o); }
		/// <summary>Can be clicked, boxed or selected by Select all: neither hidden nor locked.</summary>
		public static bool Pickable(EditorGameObject o) { return o != null && o.gameObject.activeInHierarchy && !IsHidden(o) && !IsLocked(o); }

		/// <summary>
		/// Every placed object (the deleted ones, kept for undo, are inactive: left out) - also those of an opened or generated
		/// island, which stand one level down in a holder ("LoadedObjects", "GeneratedObjects"): only the root's own children
		/// were looked at, so Select all, box select and the list found none of them. (Objects inside a placed one are part of it.)
		/// </summary>
		public static List<EditorGameObject> Placed()
		{
			GameObject root = GameObject.Find("PlacedObjects");
			if (root == null) return new List<EditorGameObject>();
			return root.GetComponentsInChildren<EditorGameObject>(false).Where(e => e.transform.parent == null || e.transform.parent.GetComponentInParent<EditorGameObject>() == null).ToList();
		}

		/// <summary>Hides (or shows) objects: their renderers and colliders off, so they can't be seen or picked.</summary>
		public static void SetHidden(IEnumerable<EditorGameObject> objects, bool hide)
		{
			Purge();
			TransformGizmo g = DynamicIslands.EditorGizmoHandler;
			foreach (EditorGameObject o in objects.Where(x => x != null).ToList())
			{
				if (hide && !hiddenRenderers.ContainsKey(o))
				{
					if (g != null) g.RemoveTarget(o.transform, false);
					List<Renderer> rs = o.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
					foreach (Renderer r in rs) r.enabled = false;
					hiddenRenderers[o] = rs;
					var parts = new List<Behaviour>();
					foreach (Collider c in o.GetComponentsInChildren<Collider>(true).Where(c => c.enabled)) c.enabled = false;
					foreach (Canvas c in o.GetComponentsInChildren<Canvas>(true).Where(c => c.enabled)) { c.enabled = false; parts.Add(c); }
					hiddenParts[o] = parts;
				}
				else if (!hide && hiddenRenderers.ContainsKey(o))
				{
					foreach (Renderer r in hiddenRenderers[o]) if (r != null) r.enabled = true;
					foreach (Collider c in o.GetComponentsInChildren<Collider>(true)) c.enabled = true;
					foreach (Behaviour b in hiddenParts[o]) if (b != null) b.enabled = true;
					hiddenRenderers.Remove(o);
					hiddenParts.Remove(o);
				}
			}
		}

		/// <summary>Locks (or unlocks) objects: still seen, but clicks, boxes and Select all pass them by.</summary>
		public static void SetLocked(IEnumerable<EditorGameObject> objects, bool lockThem)
		{
			Purge();
			TransformGizmo g = DynamicIslands.EditorGizmoHandler;
			foreach (EditorGameObject o in objects.Where(x => x != null))
			{
				if (lockThem) { locked.Add(o); if (g != null) g.RemoveTarget(o.transform, false); }
				else locked.Remove(o);
			}
		}

		public static int HiddenCount { get { Purge(); return hiddenRenderers.Count; } }
		public static int LockedCount { get { Purge(); return locked.Count; } }

		/// <summary>Objects destroyed (another island opened) are forgotten.</summary>
		static void Purge()
		{
			foreach (EditorGameObject o in hiddenRenderers.Keys.Where(k => k == null).ToList()) { hiddenRenderers.Remove(o); hiddenParts.Remove(o); }
			locked.RemoveWhere(o => o == null);
		}

		/// <summary>Select all (Ctrl+A): every placed object that isn't hidden or locked. Returns how many.</summary>
		public static int SelectAll()
		{
			return Select(Placed().Where(Pickable).ToList(), false);
		}

		/// <summary>Same kind: every placed object of the kinds selected now. Returns how many.</summary>
		public static int SelectSameKind()
		{
			TransformGizmo g = DynamicIslands.EditorGizmoHandler;
			if (g == null) return 0;
			var kinds = new HashSet<string>(g.SelectedRoots.Where(t => t != null).Select(t => t.GetComponent<EditorGameObject>()).Where(e => e != null).Select(e => e.GameObjectName));
			if (kinds.Count == 0) return 0;
			return Select(Placed().Where(o => Pickable(o) && kinds.Contains(o.GameObjectName)).ToList(), false);
		}

		/// <summary>Selects these objects (adding to the selection, or in place of it).</summary>
		public static int Select(List<EditorGameObject> objects, bool add)
		{
			TransformGizmo g = DynamicIslands.EditorGizmoHandler;
			if (g == null) return 0;
			if (!add) g.ClearTargets(false);
			foreach (EditorGameObject o in objects) g.AddTarget(o.transform, false);
			return objects.Count;
		}

		#region Box select

		static Vector2 boxStart;
		static bool pressed, boxing;
		static RectTransform box;

		/// <summary>Box select is being dragged (tests, and the camera leaves the mouse alone).</summary>
		public static bool Boxing { get { return boxing; } }

		/// <summary>Every frame in the Objects tab: a left-drag that starts on empty ground draws a box; releasing it selects what is inside.</summary>
		public static void Tick()
		{
			TransformGizmo g = DynamicIslands.EditorGizmoHandler;
			Camera cam = Camera.main;
			if (g == null || cam == null) { pressed = boxing = false; ShowBox(false); return; }
			if (Input.GetMouseButtonDown(0) && CanStart(g, cam)) { pressed = true; boxStart = Input.mousePosition; }
			if (!pressed) return;
			Vector2 now = Input.mousePosition;
			if (!boxing && (now - boxStart).magnitude > 8f) boxing = true;
			if (boxing) DrawBox(boxStart, now);
			if (!Input.GetMouseButton(0))
			{
				if (boxing) BoxSelect(boxStart, now, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
				pressed = boxing = false;
				ShowBox(false);
			}
		}

		static bool CanStart(TransformGizmo g, Camera cam)
		{
			if (EditorInput.IsTyping || EditorCamera.UsingMouse || g.placingObject || g.translatingAxis != RuntimeGizmos.Axis.None) return false;
			if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return false;
			if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return false;
			return PlacementOptions.PickObject(cam.ScreenPointToRay(Input.mousePosition), Physics.DefaultRaycastLayers) == null;
		}

		/// <summary>Selects the pickable objects whose middle is inside the screen box (from, to in screen pixels).</summary>
		public static int BoxSelect(Vector2 from, Vector2 to, bool add)
		{
			Camera cam = Camera.main;
			if (cam == null) return 0;
			Rect r = Rect.MinMaxRect(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y), Mathf.Max(from.x, to.x), Mathf.Max(from.y, to.y));
			var inside = new List<EditorGameObject>();
			foreach (EditorGameObject o in Placed().Where(Pickable))
			{
				Vector3 s = cam.WorldToScreenPoint(o.transform.position);
				if (s.z > 0f && r.Contains(new Vector2(s.x, s.y))) inside.Add(o);
			}
			Select(inside, add);
			if (inside.Count > 0) DynamicIslands.Notify("Selected " + inside.Count + " object(s) in the box" + (add ? " (added)" : ""));
			return inside.Count;
		}

		internal static void DrawBox(Vector2 a, Vector2 b)
		{
			if (box == null)
			{
				if (EditorUI.Canvas == null) return;
				box = UIKit.Rect("SelectionBox", EditorUI.Canvas.transform);
				Image img = box.gameObject.AddComponent<Image>();
				img.color = new Color(1f, 0.85f, 0.4f, 0.18f);
				img.raycastTarget = false;
				Outline line = box.gameObject.AddComponent<Outline>();
				line.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
				line.effectDistance = new Vector2(1.5f, 1.5f);
				box.anchorMin = box.anchorMax = Vector2.zero;
				box.pivot = Vector2.zero;
			}
			ShowBox(true);
			float scale = EditorUI.Canvas.scaleFactor > 0f ? EditorUI.Canvas.scaleFactor : 1f;
			box.anchoredPosition = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)) / scale;
			box.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)) / scale;
			box.SetAsLastSibling();
		}

		internal static void ShowBox(bool on) { if (box != null && box.gameObject.activeSelf != on) box.gameObject.SetActive(on); }

		#endregion
	}

	/// <summary>
	/// "Placed objects" (ROADMAP E3): the island's objects by kind, with how many - Select a kind, Hide/Show it,
	/// Lock/Unlock it; a search narrows the list. Opened by List... in the Selection group.
	/// </summary>
	public class PlacedListWindow : MonoBehaviour
	{
		static PlacedListWindow instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		internal static GameObject Root { get { return instance != null ? instance.gameObject : null; } }
		internal static InputField Search { get { return instance != null ? instance.search : null; } }

		InputField search;
		RectTransform listContent;
		Text summary;

		public static void Create(Transform canvas)
		{
			var blocker = new GameObject("PlacedListWindow", typeof(RectTransform), typeof(Image));
			blocker.layer = 5;
			blocker.transform.SetParent(canvas, false);
			UIKit.Stretch((RectTransform)blocker.transform);
			blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
			instance = blocker.AddComponent<PlacedListWindow>();
			instance.Build();
			blocker.SetActive(false);
		}

		public static void Open()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(true);
			instance.transform.SetAsLastSibling();
			instance.search.text = "";
			instance.Refresh();
		}

		public static void Close()
		{
			if (instance == null) return;
			instance.gameObject.SetActive(false);
			EditorInput.IsTyping = false;
		}

		void Update()
		{
			EditorInput.IsTyping = search != null && search.isFocused;
			if (EscKey.Down(this)) Close();
		}

		void OnDisable() { EditorInput.IsTyping = false; }

		void Build()
		{
			RectTransform panel = UIKit.Panel(transform, "Panel", new RectOffset(16, 16, 14, 16), 10f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 0));
			RectTransform head = UIKit.Row(panel, 28f, 6f, "Head");
			UIKit.Label(head, "PLACED OBJECTS", 18, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
			summary = UIKit.Label(head, "", 11, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, "Summary");
			search = UIKit.Field(panel, "Search (palm, chest...)", "", 30f, "Only the kinds whose name has this");
			search.onValueChanged.AddListener(v => Refresh());
			RectTransform listBox = UIKit.Rect("ListBox", panel);
			UIKit.Size(listBox.gameObject, -1, 340);
			UIKit.Background(listBox.gameObject, UIKit.FieldBg, 6);
			ScrollRect sr;
			listContent = UIKit.ScrollList(listBox, out sr, 3f);
			UIKit.Stretch((RectTransform)sr.transform, 4, 4, 4, 4);
			RectTransform buttons = UIKit.Row(panel, 32f, 8f, "Buttons");
			UIKit.Button(buttons, "Show all", () => { SelectionTools.SetHidden(SelectionTools.Placed(), false); Refresh(); }, "Show every hidden object again", -1, 32, 14);
			UIKit.Button(buttons, "Unlock all", () => { SelectionTools.SetLocked(SelectionTools.Placed(), false); Refresh(); }, "Unlock every locked object", -1, 32, 14);
			UIKit.Button(buttons, "Close", Close, "Close (Esc)", -1, 32, 14);
		}

		/// <summary>The kinds again (after a change).</summary>
		void Refresh()
		{
			foreach (Transform child in listContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
			List<EditorGameObject> all = SelectionTools.Placed();
			string q = search.text.Trim();
			var kinds = all.GroupBy(o => o.GameObjectName).Select(gr => new { Name = gr.Key, Label = PlaceableCatalog.DisplayName(gr.Key), List = gr.ToList() })
				.Where(k => q.Length == 0 || k.Label.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || k.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
				.OrderBy(k => k.Label, StringComparer.OrdinalIgnoreCase).ToList();
			summary.text = all.Count + " objects, " + all.Select(o => o.GameObjectName).Distinct().Count() + " kinds" + (SelectionTools.HiddenCount > 0 ? " · " + SelectionTools.HiddenCount + " hidden" : "") + (SelectionTools.LockedCount > 0 ? " · " + SelectionTools.LockedCount + " locked" : "");
			foreach (var k in kinds.Take(300))
			{
				var list = k.List;
				int hidden = list.Count(SelectionTools.IsHidden), lockedN = list.Count(SelectionTools.IsLocked);
				RectTransform row = UIKit.Row(listContent, 28f, 4f, "Kind_" + k.Name);
				Text l = UIKit.Label(row, k.Label + "  <color=#b8a888>x" + list.Count + (hidden > 0 ? ", " + hidden + " hidden" : "") + (lockedN > 0 ? ", " + lockedN + " locked" : "") + "</color>", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Label");
				l.supportRichText = true;
				UIKit.Size(l.gameObject, -1, 28);
				UIKit.Button(row, "Select", () =>
				{
					int n = SelectionTools.Select(list.Where(SelectionTools.Pickable).ToList(), false);
					Close();
					DynamicIslands.Notify(n > 0 ? "Selected " + n + " " + k.Label : "All of them are hidden or locked", n == 0);
				}, "Select every " + k.Label + " (not the hidden or locked ones)", 64, 26f, 12).name = "Button_Select_" + k.Name;
				UIKit.Button(row, hidden == list.Count ? "Show" : "Hide", () => { SelectionTools.SetHidden(list, hidden != list.Count); Refresh(); },
					"Hide them while you edit (they are still saved and in the game), or show them again", 56, 26f, 12).name = "Button_Hide_" + k.Name;
				UIKit.Button(row, lockedN == list.Count ? "Unlock" : "Lock", () => { SelectionTools.SetLocked(list, lockedN != list.Count); Refresh(); },
					"Lock them: still seen, but clicks, boxes and Select all pass them by", 64, 26f, 12).name = "Button_Lock_" + k.Name;
			}
			if (kinds.Count > 300) UIKit.Label(listContent, "... and " + (kinds.Count - 300) + " more kinds: search to narrow the list", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
			if (kinds.Count == 0) UIKit.Label(listContent, all.Count == 0 ? "Nothing placed yet." : "No kind has '" + q + "' in its name.", 13, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
		}
	}
}
