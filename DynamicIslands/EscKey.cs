using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Esc for every Custom Islands window, the topmost one first (Franz, 2026-10-17: some windows closed on Esc, others
	/// didn't, and a press could close a window and the one under it). A window asks <see cref="Down"/> in its Update
	/// instead of reading the key itself. On the frame Esc is pressed every open window that asks is noted; after all of
	/// them asked (LateUpdate) the one drawn on top wins - the highest canvas sorting order, then the latest in the
	/// hierarchy - and on the next frame only that window gets true and closes. One press closes one window.
	/// Text typed into a field isn't lost to Esc: Unity's field puts back what it held before it was clicked into, and that
	/// is undone here, so a window with unsaved work can ask (<see cref="AskUnsaved"/>) instead of losing it.
	/// </summary>
	public static class EscKey
	{
		static int pressFrame = -1, decidedFrame = -1, swallowFrame = -1;
		static Component winner;
		static readonly List<Component> candidates = new List<Component>();
		static readonly Dictionary<InputField, string> typed = new Dictionary<InputField, string>();
		static readonly List<InputField> fieldBuffer = new List<InputField>();
		static Router router;

		/// <summary>Esc is down this frame (the key, or a test's <see cref="Press"/>).</summary>
		public static bool Pressed { get { return pressFrame == Time.frameCount || Input.GetKeyDown(KeyCode.Escape); } }

		/// <summary>The window that closed on the last Esc (tests, the log).</summary>
		public static string LastClosed { get; private set; }

		/// <summary>Esc as if pressed on the next frame (tests: a coroutine runs after this frame's Updates).</summary>
		public static void Press() { pressFrame = Time.frameCount + 1; Ensure(); }

		/// <summary>
		/// Asked by an open window in its Update: true when it is the topmost window that should close on the last Esc.
		/// (The answer comes one frame after the press, when every open window has asked.)
		/// </summary>
		public static bool Down(Component who)
		{
			if (who == null) return false;
			Ensure(); // (made before the first press: one made on it would miss that frame's LateUpdate)
			int frame = Time.frameCount;
			if (winner != null && decidedFrame == frame - 1 && ReferenceEquals(winner, who))
			{
				winner = null;
				KeepTyped(who);
				LastClosed = who.GetType().Name + " (" + who.gameObject.name + ")";
				Debug.Log("[CUSTOM ISLANDS] Esc closes " + LastClosed);
				return true;
			}
			if (!Pressed) { RememberTyped(who); return false; }
			KeepTyped(who);
			if (!candidates.Contains(who)) candidates.Add(who);
			return false;
		}

		/// <summary>This frame's Esc was used for something else (a key being chosen, a search emptied): no window closes on it.</summary>
		public static void Swallow() { swallowFrame = Time.frameCount; }

		/// <summary>Esc closes this window (one without an Update of its own): close runs when it is the topmost.</summary>
		public static void Add(GameObject window, Action close)
		{
			if (window == null || close == null) return;
			EscCloser c = window.GetComponent<EscCloser>() ?? window.AddComponent<EscCloser>();
			c.Close = close;
		}

		/// <summary>
		/// Esc on a window with changes not saved yet: an info box asks - Save and close, Close without saving, or Keep
		/// editing (the box's own Esc keeps editing too).
		/// </summary>
		public static void AskUnsaved(string what, Action save, Action discard)
		{
			InfoWindow.Open("Unsaved changes", what + " has changes that aren't saved yet. Save them before the window closes?",
				new InfoWindow.Choice("Save and close", () => { InfoWindow.Close(); save(); }, "Save the changes, then close the window", true),
				new InfoWindow.Choice("Close without saving", () => { InfoWindow.Close(); discard(); }, "The changes are lost"),
				new InfoWindow.Choice("Keep editing", null, "Back to the window (Esc)"));
			Debug.Log("[CUSTOM ISLANDS] Esc: unsaved changes in " + what + ", asked first");
		}

		#region Typed text

		static void RememberTyped(Component who)
		{
			who.GetComponentsInChildren(false, fieldBuffer);
			foreach (InputField f in fieldBuffer) typed[f] = f.text;
			if (typed.Count > 400)
			{
				var gone = new List<InputField>();
				foreach (InputField f in typed.Keys) if (f == null) gone.Add(f);
				foreach (InputField f in gone) typed.Remove(f);
			}
		}

		/// <summary>A field Esc put back to its old text gets what was typed again (and its window takes it, as on Enter).</summary>
		static void KeepTyped(Component who)
		{
			who.GetComponentsInChildren(false, fieldBuffer);
			foreach (InputField f in fieldBuffer)
			{
				string was;
				if (!typed.TryGetValue(f, out was) || was == f.text) continue;
				f.text = was;
				try { f.onEndEdit.Invoke(was); }
				catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Esc: a field kept its text, its window didn't take it: " + e.Message); }
			}
		}

		#endregion

		#region The topmost

		/// <summary>The object that picks the topmost window (made once, kept across scenes).</summary>
		public static void Ensure()
		{
			if (router != null) return;
			var go = new GameObject("CustomIslands_EscKey");
			UnityEngine.Object.DontDestroyOnLoad(go);
			router = go.AddComponent<Router>();
		}

		/// <summary>The sorting order the window is drawn with: its canvas's, or the root canvas's.</summary>
		static int Order(Component c)
		{
			Canvas canvas = c.GetComponentInParent<Canvas>();
			while (canvas != null && !canvas.isRootCanvas && !canvas.overrideSorting)
				canvas = canvas.transform.parent != null ? canvas.transform.parent.GetComponentInParent<Canvas>() : null;
			return canvas != null ? canvas.sortingOrder : int.MinValue;
		}

		static List<int> Path(Transform t)
		{
			var path = new List<int>();
			for (; t != null; t = t.parent) path.Insert(0, t.GetSiblingIndex());
			return path;
		}

		/// <summary>Above 0 when a is drawn over b.</summary>
		static int Compare(Component a, Component b)
		{
			int oa = Order(a), ob = Order(b);
			if (oa != ob) return oa.CompareTo(ob);
			List<int> pa = Path(a.transform), pb = Path(b.transform);
			for (int i = 0; i < pa.Count && i < pb.Count; i++) if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
			return pa.Count.CompareTo(pb.Count); // (a window inside another is drawn over it)
		}

		class Router : MonoBehaviour
		{
			void LateUpdate()
			{
				if (candidates.Count == 0) return;
				if (swallowFrame == Time.frameCount) { candidates.Clear(); return; }
				Component top = null;
				foreach (Component c in candidates)
					if (c != null && c.gameObject.activeInHierarchy && (top == null || Compare(c, top) > 0)) top = c;
				candidates.Clear();
				if (top == null) return;
				winner = top;
				decidedFrame = Time.frameCount;
			}
		}

		#endregion
	}

	/// <summary>Esc for a window without an Update of its own (<see cref="EscKey.Add"/>).</summary>
	public class EscCloser : MonoBehaviour
	{
		public Action Close;

		void Update()
		{
			if (Close != null && !DropList.Busy && EscKey.Down(this)) Close();
		}
	}
}
