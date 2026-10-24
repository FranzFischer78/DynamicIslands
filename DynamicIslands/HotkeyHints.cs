using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// L2: the mod's own keys in a world - the journal (J) and the stats page (K) - which the player can change in Defaults
	/// (Keys): kept in Mods\DynamicIslands\world_rules.txt as journalkey= and statskey= (Unity KeyCode names). This PC's
	/// alone; nothing is sent to other players.
	/// </summary>
	public static class ModKeys
	{
		public const string JournalSetting = "journalkey", StatsSetting = "statskey";
		public const KeyCode JournalDefault = KeyCode.J, StatsDefault = KeyCode.K;
		static KeyCode? journal, stats;

		public static KeyCode Journal { get { if (journal == null) journal = Read(JournalSetting, JournalDefault); return journal.Value; } }
		public static KeyCode Stats { get { if (stats == null) stats = Read(StatsSetting, StatsDefault); return stats.Value; } }

		/// <summary>While Defaults waits for a key to be pressed (and in the frame it got one): the journal and stats keys do nothing.</summary>
		public static bool Listening { get { return listening || Time.frameCount == setFrame; } }
		static bool listening;
		static int setFrame = -1;
		internal static void Listen(bool on) { listening = on; if (!on) setFrame = Time.frameCount; }

		static KeyCode Read(string setting, KeyCode fallback)
		{
			string v = WorldRules.ReadDefault(setting);
			if (string.IsNullOrEmpty(v)) return fallback;
			try
			{
				var k = (KeyCode)Enum.Parse(typeof(KeyCode), v.Trim(), true);
				// (a number, "7", parses as KeyCode 7, which no key has: Alpha7 is meant)
				if (Enum.IsDefined(typeof(KeyCode), k) && Problem(k) == null) return k;
			}
			catch { }
			Debug.LogWarning("[CUSTOM ISLANDS] " + WorldRules.DefaultFileName + ": " + setting + "=" + v + " is not a key that can be used - " + fallback + " instead");
			return fallback;
		}

		/// <summary>Why a key can't be one of the mod's keys, or null.</summary>
		public static string Problem(KeyCode k)
		{
			if (k == KeyCode.None) return "no key";
			if (k == KeyCode.Escape) return "Esc closes windows";
			if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) return "a mouse button";
			if (k >= KeyCode.JoystickButton0) return "a controller button";
			if (k == KeyCode.Return || k == KeyCode.KeypadEnter || k == KeyCode.Tab || k == KeyCode.Space || k == KeyCode.Backspace) return "it is used to type and to play";
			// (a key Raft's own controls use - movement, interact, hotbar numbers - would do both: CA29)
			try
			{
				if (MyInput.Keybinds != null)
					foreach (var kb in MyInput.Keybinds)
						if (kb.Value != null && kb.Value.MainKey == k) return "Raft uses it (" + kb.Key + ")";
			}
			catch { }
			if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return "the number keys pick hotbar slots";
			return null;
		}

		/// <summary>Sets one of the keys (forJournal: the journal's, else the stats page's); the reason when it can't be, or null.</summary>
		public static string Set(bool forJournal, KeyCode k)
		{
			string problem = Problem(k);
			if (problem != null) return Name(k) + " can't be used: " + problem + ".";
			if (k == (forJournal ? Stats : Journal)) return Name(k) + " already opens the " + (forJournal ? "stats page" : "journal") + ".";
			WorldRules.SaveDefault(forJournal ? JournalSetting : StatsSetting, k.ToString());
			if (forJournal) journal = k; else stats = k;
			HotkeyHints.KeysChanged();
			return null;
		}

		/// <summary>Both keys back to J and K.</summary>
		public static void Reset()
		{
			WorldRules.SaveDefault(JournalSetting, JournalDefault.ToString());
			WorldRules.SaveDefault(StatsSetting, StatsDefault.ToString());
			journal = JournalDefault;
			stats = StatsDefault;
			HotkeyHints.KeysChanged();
		}

		/// <summary>A key as the player knows it: "7" for Alpha7, "Num 7" for Keypad7.</summary>
		public static string Name(KeyCode k)
		{
			string s = k.ToString();
			if (s.StartsWith("Alpha") && s.Length == 6) return s.Substring(5);
			if (s.StartsWith("Keypad")) return "Num " + s.Substring(6);
			return s;
		}

		/// <summary>The keyboard key pressed in this frame (Escape too, to cancel), or None.</summary>
		public static KeyCode Pressed()
		{
			if (!Input.anyKeyDown) return KeyCode.None;
			foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
			{
				if (k == KeyCode.None || (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) || k >= KeyCode.JoystickButton0) continue;
				if (Input.GetKeyDown(k)) return k;
			}
			return KeyCode.None;
		}
	}

	/// <summary>
	/// The mod's own keys at the hotbar, like Raft's: Raft shows its inventory and notebook keys as small tabs at the ends
	/// of the hotbar (_CanvasGame_New/.../Hotbar/KeyboardLayout: an icon on a brown tab with the key under it). The
	/// journal (J) and, while the level up system is on, the stats page (K) get the same tabs beside the notebook's -
	/// copies of Raft's tab, so they look and hide exactly as Raft's do (with the keyboard layout).
	/// </summary>
	public static class HotkeyHints
	{
		public const string JournalName = "CustomIslands_Hotkey_Journal", StatsName = "CustomIslands_Hotkey_Stats";
		const string LayoutPath = "_CanvasGame_New/InventoryParent/Hotbar/KeyboardLayout";
		static RectTransform journal, stats;
		static float nextLook;
		static Sprite bookIcon;

		/// <summary>Every frame in a world (cheap: looks twice a second).</summary>
		public static void Tick()
		{
			if (Time.unscaledTime < nextLook) return;
			nextLook = Time.unscaledTime + 0.5f;
			try
			{
				if (journal == null || stats == null) Build();
				if (stats != null && stats.gameObject.activeSelf != PlayerLevels.On) stats.gameObject.SetActive(PlayerLevels.On);
				KeepKeys();
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] The hotbar's key tabs: " + e.Message); nextLook = Time.unscaledTime + 30f; }
		}

		static void Build()
		{
			GameObject layout = GameObject.Find(LayoutPath);
			if (layout == null) return;
			Transform note = layout.transform.Find("UI_Hotkey_Element_NoteBook"), inv = layout.transform.Find("UI_Hotkey_Element_Inventory");
			if (note == null) return;
			RectTransform noteRect = (RectTransform)note;
			// (one tab width and a gap further along, away from the inventory's tab: the notebook's is at the hotbar's right end)
			float dir = inv != null && ((RectTransform)inv).anchoredPosition.x > noteRect.anchoredPosition.x ? -1f : 1f;
			float step = (noteRect.rect.width + 6f) * dir;
			if (journal == null) journal = Make(layout.transform, noteRect, JournalName, BookIcon(), ModKeys.Name(JournalWindow.Key), step);
			if (stats == null) stats = Make(layout.transform, noteRect, StatsName, StarTabIcon(), ModKeys.Name(PlayerLevels.Key), step * 2f);
			Debug.Log("[CUSTOM ISLANDS] Hotbar key tabs: journal (" + JournalWindow.Key + ") and stats (" + PlayerLevels.Key + ") beside Raft's notebook tab");
		}

		static RectTransform Make(Transform parent, RectTransform from, string name, Sprite icon, string key, float dx)
		{
			Transform old = parent.Find(name);
			if (old != null) return (RectTransform)old;
			var copy = (RectTransform)UnityEngine.Object.Instantiate(from.gameObject, parent, false).transform;
			copy.name = name;
			copy.anchoredPosition = from.anchoredPosition + new Vector2(dx, 0f);
			// (only the look: nothing of Raft's that could set the copy's text or react to it)
			foreach (MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>(true))
				if (!(b is Graphic) && !(b is LayoutElement) && !(b is Shadow) && !b.GetType().Name.StartsWith("TextMeshPro")) UnityEngine.Object.Destroy(b);
			Image img = copy.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.transform != copy);
			if (img != null && icon != null)
			{
				img.sprite = icon;
				img.preserveAspect = true;
				// (the open book is wider than Raft's notebook icon and covered the key's letter - Franz, 2026-10-17: a bit
				// smaller, and the letter drawn after the icon, so it is always on top)
				img.rectTransform.localScale = Vector3.one * IconScale;
				Graphic letter = KeyText(copy);
				if (letter != null && !letter.transform.IsChildOf(img.transform))
				{
					Transform t = letter.transform;
					while (t.parent != null && t.parent != img.transform.parent && t.parent != copy) t = t.parent;
					if (t.parent == img.transform.parent) t.SetAsLastSibling();
				}
			}
			SetKey(copy, key);
			return copy;
		}

		/// <summary>The size of the mod's icons on their tabs (a part of Raft's icon's place).</summary>
		public const float IconScale = 0.8f;

		/// <summary>The key's letter on a tab (Raft's TextMeshPro, or a Text), or null.</summary>
		static Graphic KeyText(RectTransform tab)
		{
			Graphic tmp = tab.GetComponentsInChildren<Graphic>(true).FirstOrDefault(c => c != null && c.GetType().Name == "TextMeshProUGUI");
			return tmp ?? tab.GetComponentInChildren<Text>(true);
		}

		/// <summary>Tests: the tab's key letter is drawn after (over) its icon.</summary>
		public static bool KeyOverIcon(string tabName)
		{
			GameObject tab = GameObject.Find(tabName);
			if (tab == null) return false;
			Graphic letter = KeyText((RectTransform)tab.transform);
			Image img = tab.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.transform != tab.transform);
			if (letter == null || img == null) return false;
			var order = tab.GetComponentsInChildren<Graphic>(true).ToList(); // (depth first: the order they are drawn in)
			return order.IndexOf(letter) > order.IndexOf(img);
		}

		/// <summary>
		/// The key's letter on a tab, shown as Raft shows TAB under the inventory's tab (Franz, 2026-10-17: the journal's tab had
		/// none): switched on, drawn, as opaque as Raft's own, saying the key bound now.
		/// </summary>
		static void SetKey(RectTransform tab, string key)
		{
			Graphic letter = KeyText(tab);
			if (letter == null) return;
			for (Transform t = letter.transform; t != null && t != tab; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
			letter.enabled = true;
			Graphic raftLetter = RaftKeyText();
			Color c = raftLetter != null ? raftLetter.color : letter.color;
			if (c.a < 0.9f) c.a = 1f;
			letter.color = c;
			if (letter is Text) ((Text)letter).text = key;
			else Traverse.Create(letter).Property("text").SetValue(key);
		}

		/// <summary>The letter under Raft's inventory tab (TAB), whose look the mod's tabs copy.</summary>
		static Graphic RaftKeyText()
		{
			GameObject inv = GameObject.Find(LayoutPath + "/UI_Hotkey_Element_Inventory");
			return inv != null ? KeyText((RectTransform)inv.transform) : null;
		}

		static string LetterText(Graphic letter)
		{
			if (letter == null) return null;
			if (letter is Text) return ((Text)letter).text;
			return Traverse.Create(letter).Property("text").GetValue<string>();
		}

		/// <summary>Tests: the key a tab shows, or why none is seen (switched off, see-through, empty).</summary>
		public static string KeyShown(string tabName)
		{
			GameObject tab = GameObject.Find(tabName);
			if (tab == null) return "(no tab)";
			Graphic letter = KeyText((RectTransform)tab.transform);
			if (letter == null) return "(no letter)";
			if (!letter.gameObject.activeInHierarchy) return "(letter switched off)";
			if (!letter.enabled) return "(letter not drawn)";
			if (letter.color.a < 0.5f) return "(letter see-through)";
			string s = LetterText(letter);
			return string.IsNullOrEmpty(s) ? "(letter empty)" : s;
		}

		/// <summary>The key each tab should show now (Tick puts it back if something else changed it).</summary>
		static void KeepKeys()
		{
			if (journal != null && KeyShown(JournalName) != ModKeys.Name(JournalWindow.Key)) SetKey(journal, ModKeys.Name(JournalWindow.Key));
			if (stats != null && stats.gameObject.activeInHierarchy && KeyShown(StatsName) != ModKeys.Name(PlayerLevels.Key)) SetKey(stats, ModKeys.Name(PlayerLevels.Key));
		}

		/// <summary>The journal's or stats page's key was changed (ModKeys): the tabs say the new ones.</summary>
		public static void KeysChanged()
		{
			try
			{
				if (journal != null) SetKey(journal, ModKeys.Name(JournalWindow.Key));
				if (stats != null) SetKey(stats, ModKeys.Name(PlayerLevels.Key));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] The hotbar's key tabs: " + e.Message); }
		}

		static Sprite starTab;

		/// <summary>The level bar's star as a light silhouette, so the tab tints it as Raft tints its own tab icons (the bar's
		/// dark star came out darker than the others).</summary>
		public static Sprite StarTabIcon()
		{
			if (starTab != null) return starTab;
			// (drawn here: the level bar's star texture can't be read back to make a light copy)
			const int W = 72, H = 68;
			var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CustomIslands_StarTabIcon" };
			Vector2 c = new Vector2(W / 2f, H / 2f - 2f);
			float R = 32f, r = R * 0.48f;
			var pts = new Vector2[10];
			for (int i = 0; i < 10; i++) { float ang = Mathf.PI / 2f + i * Mathf.PI / 5f; float rad = i % 2 == 0 ? R : r; pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad; }
			var px = new Color32[W * H];
			for (int y = 0; y < H; y++)
				for (int x = 0; x < W; x++)
				{
					// (4 samples a pixel for smooth edges)
					int inside = 0;
					for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++) if (Inside(pts, new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f))) inside++;
					px[y * W + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 4));
				}
			tex.SetPixels32(px);
			tex.Apply();
			starTab = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
			return starTab;
		}

		static bool Inside(Vector2[] poly, Vector2 p)
		{
			bool inside = false;
			for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
				if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
			return inside;
		}

		/// <summary>An open book, drawn as Raft's tab icons are: one light silhouette the tab tints (80 x 64).</summary>
		public static Sprite BookIcon()
		{
			if (bookIcon != null) return bookIcon;
			const int W = 80, H = 64;
			var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CustomIslands_BookIcon" };
			var px = new Color32[W * H];
			for (int y = 0; y < H; y++)
				for (int x = 0; x < W; x++)
				{
					float fx = x + 0.5f, fy = y + 0.5f;
					float cx = W / 2f;
					float d = Mathf.Abs(fx - cx);             // from the spine
					float top = 52f + 6f * (d / cx) - 5f * Mathf.Pow(d / cx, 2f) * 2f; // pages curve up from the spine, down at the edges
					float bottom = 12f + 5f * (d / cx);
					bool page = d < cx - 3f && fy > bottom && fy < top;
					bool spineGap = d < 1.4f && fy > bottom + 3f;  // the fold between the pages
					bool lines = page && d > 8f && d < cx - 9f && ((int)(fy - bottom) % 8 == 4) && fy < top - 6f; // lines of writing
					bool cover = d < cx - 1f && fy > bottom - 5f && fy <= bottom + 1f; // the cover under the pages
					float a = (page && !spineGap && !lines) || cover ? 1f : 0f;
					px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
				}
			tex.SetPixels32(px);
			tex.Apply();
			bookIcon = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
			bookIcon.name = "CustomIslands_BookIcon";
			return bookIcon;
		}
	}
}
