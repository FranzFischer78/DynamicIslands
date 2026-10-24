using System;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A keypad code lock, as Tangaroa's launch keypad and Vasagatan's four-digit code (ROADMAP LM12): an object whose
	/// setting lock.code holds digits opens a keypad when a player uses it; the right code runs the object's "use" event
	/// (its checks and actions, as if used), a wrong one says so. Once opened the lock stays open for everyone (the
	/// island's state, LockKeyBase + object) and the object then works as any used object. The code is usually found
	/// on a note somewhere on the island.
	/// </summary>
	public class CodeLock : MonoBehaviour
	{
		public const string Code = "lock.code";
		public const int LockKeyBase = 0xA0000; // (after SharedOnceBase 0x80000 + 0xFFFF, before PendingBase 0x900000)

		static CodeLock instance;
		public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
		/// <summary>What is typed (the tests read it).</summary>
		public static string Typed { get { return instance != null ? instance.typed : ""; } }

		IslandWorldState.Entry entry;
		int index;
		string code = "", typed = "";
		Text display, said;
		float openedAt;
		bool cursorWasFree;
		Transform at;

		public static bool HasCode(System.Collections.Generic.IDictionary<string, string> props) { return ObjectProps.Get(props, Code).Trim().Length > 0; }

		public static bool Unlocked(IslandWorldState.Entry e, int index) { return e != null && e.State.ContainsKey(LockKeyBase + index); }

		/// <summary>The player uses a locked object: the keypad opens (or, open already, the object is used).</summary>
		public static void Use(IslandWorldState.Entry e, int index, string code, Transform where)
		{
			if (Unlocked(e, index)) { Behaviours.Fire(e, index, "use", true); return; }
			if (instance == null) Build();
			instance.entry = e; instance.index = index; instance.code = code.Trim(); instance.typed = ""; instance.at = where;
			instance.said.text = "Type the code";
			instance.Refresh();
			instance.gameObject.SetActive(true);
			instance.openedAt = Time.unscaledTime;
			instance.cursorWasFree = Cursor.visible;
			if (!instance.cursorWasFree) try { RAPI.ToggleCursor(true); } catch { }
		}

		/// <summary>Presses a key of the keypad (a digit, "C" to clear, "OK").</summary>
		public static void Press(string key) { if (IsOpen) instance.Key(key); }

		/// <summary>Closes the keypad if it is open (the play tests, after a wrong code).</summary>
		public static void Close() { if (IsOpen) instance.Hide(); }

		static void Build()
		{
			Canvas canvas = UIKit.CreateCanvas("CustomIslands_CodeLock", 510);
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<CodeLock>();
			RectTransform panel = UIKit.Panel(canvas.transform, "Panel", new RectOffset(18, 18, 14, 16), 8f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 430));
			UIKit.Label(panel, "Keypad", 22, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
			instance.display = UIKit.Label(panel, "", 30, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold, "Display");
			instance.said = UIKit.Label(panel, "", 13, UIKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, "Said");
			string[][] rows = { new[] { "1", "2", "3" }, new[] { "4", "5", "6" }, new[] { "7", "8", "9" }, new[] { "C", "0", "OK" } };
			foreach (string[] row in rows)
			{
				RectTransform r = UIKit.Row(panel, 52f, 6f, "Row");
				foreach (string k in row) { string key = k; UIKit.Button(r, key, () => instance.Key(key), null, 80, 50, 20); }
			}
			UIKit.Button(panel, "Close", () => instance.Hide(), null, -1, 30, 13);
			canvas.gameObject.SetActive(false);
		}

		void Key(string k)
		{
			if (k == "C") typed = "";
			else if (k == "OK")
			{
				if (typed == code)
				{
					// (the lock stays open for everyone: the host keeps it with the island's state, and the use runs)
					entry.State[LockKeyBase + index] = new ObjectState { Active = false, Day = 0 };
					IslandNetwork.SendUsed(entry.Id, LockKeyBase + index, 0);
					IslandWorldState.Entry e = entry; int i = index;
					Hide();
					IslandInfo.ShowMessage("The keypad beeps twice. Unlocked.");
					Behaviours.Fire(e, i, "use", true);
					return;
				}
				said.text = "Wrong code";
				typed = "";
			}
			else if (typed.Length < 12) typed += k;
			Refresh();
		}

		void Refresh() { display.text = typed.Length > 0 ? typed : "_ _ _ _"; }

		void Hide()
		{
			if (!gameObject.activeSelf) return;
			gameObject.SetActive(false);
			if (!cursorWasFree) try { RAPI.ToggleCursor(false); } catch { }
		}

		void Update()
		{
			if (Time.unscaledTime - openedAt < 0.2f) return;
			for (int d = 0; d <= 9; d++) if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) Key(d.ToString());
			if (Input.GetKeyDown(KeyCode.Backspace)) Key("C");
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Key("OK");
			if (EscKey.Down(this) || Input.GetKeyDown(KeyCode.Tab)) { Hide(); return; }
			Network_Player player = null;
			try { player = ComponentManager<Network_Player>.Value; } catch { }
			if (at == null || !LoadSceneManager.IsGameSceneLoaded || (player != null && Vector3.Distance(player.transform.position, at.position) > 8f)) Hide();
		}
	}
}
