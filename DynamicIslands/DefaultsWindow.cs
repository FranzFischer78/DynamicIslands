using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// "Defaults..." (ROADMAP AU46): the settings of this PC's Mods\DynamicIslands\spawnpool.txt in a window, so nobody has to
	/// edit the file by hand - how often random islands come, how far apart and ahead, when they unload and come back, the
	/// Receiver's dots, the regrow days new worlds get, and the generated islands (with their things to gather - ROADMAP LM9). Opened from World settings in the New
	/// Game box (only there: in a world, regrow days are the RegrowDays command). Each value is saved as soon as its field is left (or its button
	/// pressed), kept within its range, through CustomIslandSpawner.SetPoolValues: the file is written whole, its comments
	/// and island lines as they were. In a world the host's players get the settings they share at once (OnPoolChanged).
	/// </summary>
	public class DefaultsWindow : MonoBehaviour
	{
		public const string CanvasName = "CustomIslands_DefaultsWindow";
		static Canvas canvas;
		static DefaultsWindow instance;
		static Text status;
		static readonly Dictionary<string, InputField> fields = new Dictionary<string, InputField>();
		static readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();

		/// <summary>A number setting's row: its key in spawnpool.txt, what it is called here, its hint, and whether it is a whole number.</summary>
		struct Setting
		{
			public string Key, Label, Hint;
			public bool Whole;
			public Setting(string key, string label, string hint, bool whole = false) { Key = key; Label = label; Hint = hint; Whole = whole; }
		}

		static readonly Setting[] Sailing =
		{
			new Setting("chancePerKm", "Random islands (0 = off)", "Random islands while sailing: any number above 0 is on, 0 is off (0-1). How often they come is each world's own setting (World settings > Islands while sailing)"),
			new Setting("quietMinutes", "Quiet minutes in a new world", "No random island in a new world's first minutes of play (game time, 0-240)"),
			new Setting("minSpacing", "Spacing (m)", "Metres kept between custom islands, centre to centre (0 or more)"),
			new Setting("spawnDistanceMin", "Appear from (m ahead)", "How far ahead of the raft an island appears, nearest (20 or more). Raft's camera renders to about 400 m"),
			new Setting("spawnDistanceMax", "... to (m ahead)", "How far ahead of the raft an island appears, furthest (never nearer than the nearest)"),
			new Setting("returnMinutes", "Needed island returns (min)", "An island the players still need that the raft left behind comes back ahead of the raft after this many minutes (0 = never)"),
		};

		static readonly Setting[] Shared =
		{
			new Setting("receiverDistance", "Receiver range (m, 0 = all)", "Receiver dots only for islands this close (0 = all of them); an island the players still need shows however far it is"),
			new Setting("unloadDistance", "Unload beyond (m)", "Islands further than this from the raft are unloaded, and come back when the raft returns (300 or more)"),
			new Setting("regrowDays", "Regrow days (new worlds)", "Harvested trees and picked-up items grow back after this many in-game days (0 = never). A world keeps the days it was first played with: change a world's in Esc > Custom Islands", true),
		};

		static readonly Setting[] Generated =
		{
			new Setting("generated", "Weight of new generated islands", "Brand-new random islands join the pool with this weight (0 = never); each is saved as gen-<style>-<seed>.island"),
			new Setting("generatedFlyingChance", "Chance one flies (0-1)", "The chance that a generated island is a flying one (0-1)"),
			new Setting("generatedGather", "Things to gather (0-1)", "The generator's Things to gather on them: trees to cut, fruit, berries and flowers that suit the style (0 = none, 1 = much)"),
			new Setting("generatedShallows", "Finds in the shallows (0-1)", "The generator's Finds in the shallows on them: sand, clay, stone, ores, scrap, clams and seaweed just off the shore (0 = none, 1 = much)"),
			new Setting("generatedShallowsDepth", "... down to (m)", "How far out the finds in the shallows go: from 0.6 m under water down to this depth (2-20 m; 6 as the generator has it)"),
		};

		public static bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }
		/// <summary>L2: which key waits for a press (null: none; true: the journal's, false: the stats page's).</summary>
		static bool? listenFor;
		/// <summary>Tests: a key pressed while a Keys button waits, as a real press does.</summary>
		internal static void PressKey(bool forJournal, KeyCode k) { ListenFor(forJournal); TakeKey(k); }
		/// <summary>Tests: the window's root.</summary>
		internal static GameObject Root { get { return canvas != null ? canvas.gameObject : null; } }
		/// <summary>Tests: a setting's field by its key in spawnpool.txt ("minSpacing").</summary>
		internal static InputField FieldFor(string key) { InputField f; return fields.TryGetValue(key, out f) ? f : null; }
		internal static string StatusText { get { return status != null ? status.text : ""; } }

		static void Build()
		{
			canvas = UIKit.CreateCanvas(CanvasName, 885); // (over World settings, the world window (870) and the island library (880); under the info boxes and ? popups)
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<DefaultsWindow>();
			RectTransform root = (RectTransform)canvas.transform;
			RectTransform dim = UIKit.Rect("Dim", root);
			UIKit.Stretch(dim);
			dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
			RectTransform panel = UIKit.Panel(root, "Panel", new RectOffset(18, 18, 12, 14), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 0f));
			RectTransform head = UIKit.Row(panel, 30f, 6f, "Head");
			Text title = UIKit.Label(head, "CUSTOM ISLANDS - DEFAULTS", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(title);
			UIKit.Label(head, "Mods\\DynamicIslands\\" + CustomIslandSpawner.PoolFileName, 12, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Italic, "File");
			Text intro = UIKit.Label(panel, "The settings of every world you host (players who join get yours). Saved as soon as a field is left; the file's notes and island list stay as they are. " +
				"A world keeps the regrow days it was first played with - change a world's own in Esc > Custom Islands.", 12, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, "Intro");
			intro.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(intro.gameObject, -1, 32);

			RectTransform columns = UIKit.Rect("Columns", panel);
			UIKit.Horizontal(columns.gameObject, 16f).childForceExpandWidth = false;
			columns.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
			RectTransform left = UIKit.Rect("Left", columns);
			UIKit.Vertical(left.gameObject, 6f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(left.gameObject, 420);
			RectTransform right = UIKit.Rect("Right", columns);
			UIKit.Vertical(right.gameObject, 6f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(right.gameObject, 428);

			// Random islands while sailing (the host's spawner alone uses these)
			RectTransform sailing = UIKit.Group(left, "Random islands while sailing", "Sailing");
			foreach (Setting s in Sailing) NumberRow(sailing, s);

			// The mod's keys in a world (L2: world_rules.txt, this PC's own)
			RectTransform keysGroup = UIKit.Group(left, "Keys (this PC)", "Keys");
			RectTransform keyRow = UIKit.Row(keysGroup, 28f, 6f, "Row_Keys");
			UIKit.Label(keyRow, "Journal", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "JournalLabel");
			Add("Key_Journal", UIKit.Button(keyRow, "", () => ListenFor(true), "The key that opens the crew's journal in a world: click, then press the new key (Esc: keep it)", 80, 28f, 13));
			UIKit.Label(keyRow, "Stats", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "StatsLabel");
			Add("Key_Stats", UIKit.Button(keyRow, "", () => ListenFor(false), "The key that opens the stats page (level up system) in a world: click, then press the new key (Esc: keep it)", 80, 28f, 13));

			// The island editor's camera (world_rules.txt, this PC's own; also the editor's Camera... button)
			RectTransform cameraGroup = UIKit.Group(left, "Editor camera (this PC)", "EditorCamera");
			refreshCamera = CameraSettings.Build(cameraGroup);

			// What every player of a hosted world shares (WorldRules.HostSettingsData)
			RectTransform shared = UIKit.Group(right, "Shared with every player", "Shared");
			Add("showOnReceiver", UIKit.Button(shared, "", () => Apply("showOnReceiver", CustomIslandSpawner.ShowOnReceiver ? "0" : "1"), "Show custom islands as green dots on Raft's Receiver", -1, 28f, 13));
			foreach (Setting s in Shared) NumberRow(shared, s);

			// Generated islands
			RectTransform gen = UIKit.Group(right, "Generated islands", "Generated");
			foreach (Setting s in Generated) NumberRow(gen, s);
			RectTransform styles = UIKit.Row(gen, 26f, 4f, "Styles");
			foreach (TerrainPainter.Style st in TerrainPainter.Styles)
			{
				string name = st.Name;
				Add("Style_" + name, UIKit.Button(styles, name, () => FlipStyle(name), "Generated islands can be " + name + " (at least one style)", -1, 26f, 12));
			}
			// (which things to gather they get: lit = on - each style uses the ones it has)
			string[] keys = GenGather.LandKeys.Concat(GenGather.SeaKeys).ToArray(), labels = GenGather.LandLabels.Concat(GenGather.SeaLabels).ToArray();
			RectTransform kinds = null;
			for (int i = 0; i < keys.Length; i++)
			{
				if (i % 5 == 0) kinds = UIKit.Row(gen, 24f, 3f, "GatherKinds");
				string key = keys[i];
				Add("Gather_" + key, UIKit.Button(kinds, labels[i], () => Write(new Dictionary<string, string> { { "generatedGatherOff", GenGather.Toggle(CustomIslandSpawner.GeneratedGatherOff, key) } }, null),
					labels[i] + " on generated islands' Things to gather / Finds in the shallows: on (lit) or off", -1, 24f, 11));
			}

			status = UIKit.Label(panel, "", 12, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Italic, "Status");
			status.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(status.gameObject, -1, 30);
			UIKit.Fit(status);
			RectTransform buttonsRow = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Button(buttonsRow, "Mod's own", ModsOwn, "Every setting here back to what a new install of Custom Islands has, the keys J and K and the editor camera too (the island list stays as it is) - click twice", 150, 34f, 14);
			UIKit.Label(buttonsRow, "", 12);
			Button close = UIKit.Button(buttonsRow, "Close", Close, "Close (Esc)", 140, 34f, 15);
			UIKit.Primary(close);
			canvas.gameObject.SetActive(false);
		}

		static Action refreshCamera;

		static void NumberRow(Transform group, Setting s)
		{
			RectTransform row = UIKit.Row(group, 28f, 6f, "Row_" + s.Key);
			UIKit.Label(row, s.Label, 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, "Label");
			InputField f = UIKit.Field(row, "0", "", 28f, s.Hint);
			f.name = "Setting_" + s.Key;
			UIKit.Size(f.gameObject, 96, 28);
			f.contentType = s.Whole ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
			f.characterLimit = 8;
			string key = s.Key;
			f.onEndEdit.AddListener(v => Apply(key, v));
			fields[key] = f;
		}

		static void Add(string name, Button b) { b.name = name; buttons[name] = b; }

		public static void Open()
		{
			CameraSettings.Reload(); // (the file as it is now)
			if (canvas == null) Build();
			canvas.gameObject.SetActive(true);
			modsOwnUntil = 0f;
			CustomIslandSpawner.LoadPool(false); // (the file as it is now, if it was changed by hand)
			SetStatus("Each value is saved as soon as its field is left.", false);
			Refresh();
		}

		/// <summary>The frame Esc or Close shut the window in (the world window under it doesn't close with the same Esc).</summary>
		internal static int ClosedFrame = -1;

		public static void Close()
		{
			if (canvas != null && canvas.gameObject.activeSelf) ClosedFrame = Time.frameCount;
			if (canvas != null) canvas.gameObject.SetActive(false);
			CameraSettings.Save();
			EditorInput.IsTyping = false;
			WorldWindow.Refresh();
		}

		void Update()
		{
			EditorInput.IsTyping = listenFor != null || fields.Values.Any(f => f != null && f.isFocused);
			CameraSettings.Tick();
			if (listenFor != null)
			{
				KeyCode k = ModKeys.Pressed();
				if (k != KeyCode.None) TakeKey(k);
				return;
			}
			if (EscKey.Down(this)) Close();
		}

		void OnDisable()
		{
			EditorInput.IsTyping = false;
			if (listenFor != null) { listenFor = null; ModKeys.Listen(false); }
			CameraSettings.Save();
		}

		/// <summary>A Keys button: the next key pressed becomes that key.</summary>
		static void ListenFor(bool forJournal)
		{
			listenFor = forJournal;
			ModKeys.Listen(true);
			SetStatus("Press the new key for the " + (forJournal ? "journal" : "stats page") + " (Esc: keep " + ModKeys.Name(forJournal ? ModKeys.Journal : ModKeys.Stats) + ").", false);
			Refresh();
		}

		static void TakeKey(KeyCode k)
		{
			bool forJournal = listenFor == true;
			listenFor = null;
			ModKeys.Listen(false);
			if (k == KeyCode.Escape) { EscKey.Swallow(); ClosedFrame = Time.frameCount; SetStatus("The keys stay as they were.", false); Refresh(); return; }
			string problem = ModKeys.Set(forJournal, k);
			if (problem != null) SetStatus(problem, true);
			else SetStatus("The " + (forJournal ? "journal" : "stats page") + " opens with " + ModKeys.Name(k) + " now (saved in " + WorldRules.DefaultFileName + ").", false);
			Refresh();
		}

		/// <summary>One setting typed (or switched): written to spawnpool.txt within its range, or why not.</summary>
		static void Apply(string key, string text)
		{
			text = (text ?? "").Trim().Replace(',', '.');
			if (text.Length == 0) { Refresh(); return; }
			if (text == CustomIslandSpawner.FormatValue(CustomIslandSpawner.ValueOf(key))) return;
			Write(new Dictionary<string, string> { { key, text } }, text);
		}

		static void FlipStyle(string name)
		{
			var on = CustomIslandSpawner.GeneratedStyles.Select(TerrainPainter.StyleName).ToList();
			if (on.Remove(name)) { if (on.Count == 0) { SetStatus("Generated islands need at least one style.", true); return; } }
			else on.Add(name);
			// (in the order Raft's styles have)
			Write(new Dictionary<string, string> { { "generatedStyles", string.Join(", ", TerrainPainter.Styles.Select(st => st.Name).Where(on.Contains).ToArray()) } }, null);
		}

		static float modsOwnUntil;

		/// <summary>Every setting back to the mod's own (the numbers and styles a new spawnpool.txt has). Asks first: the
		/// first click says what it does, a second within a few seconds does it.</summary>
		public static void ModsOwn()
		{
			if (Time.unscaledTime > modsOwnUntil)
			{
				modsOwnUntil = Time.unscaledTime + 6f;
				SetStatus("Every setting here (the keys J and K and the editor camera too) goes back to the mod's own. Click Mod's own again to go ahead.", true);
				return;
			}
			modsOwnUntil = 0f;
			var values = new Dictionary<string, string>();
			foreach (string key in CustomIslandSpawner.NumberKeys)
			{
				float v = CustomIslandSpawner.DefaultOf(key);
				if (!float.IsNaN(v)) values[key] = CustomIslandSpawner.FormatValue(v);
			}
			values["generatedStyles"] = string.Join(", ", TerrainPainter.Styles.Select(st => st.Name).ToArray());
			values["generatedGatherOff"] = "";
			ModKeys.Reset();
			CameraSettings.Reset();
			Write(values, null);
		}

		/// <summary>Writes these values (all or none) and says what was saved; typed: what the player typed, to say when it was kept within its range.</summary>
		static void Write(Dictionary<string, string> values, string typed)
		{
			try
			{
				Dictionary<string, string> written = CustomIslandSpawner.SetPoolValues(values);
				float t, w;
				bool clamped = typed != null && float.TryParse(typed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t) &&
					written.Values.Any(v => float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w) && !Mathf.Approximately(w, t));
				SetStatus("Saved: " + string.Join(", ", written.Select(kv => kv.Key + " = " + kv.Value).ToArray()) + (clamped ? " (kept within what it may be)" : "") +
					(LoadSceneManager.IsGameSceneLoaded && Raft_Network.IsHost ? " - for this world's players too." : "."), false);
			}
			catch (ArgumentException e) { SetStatus(e.Message, true); }
			catch (Exception e) { SetStatus(SafeFile.InUse(e) ? CustomIslandSpawner.PoolFileName + " is in use by another program - close it there and try again (nothing was changed)." : "Could not change " + CustomIslandSpawner.PoolFileName + ": " + e.Message, true); }
			Refresh();
		}

		public static void Refresh()
		{
			if (canvas == null || !canvas.gameObject.activeSelf) return;
			foreach (var kv in fields)
				if (kv.Value != null && !kv.Value.isFocused) kv.Value.text = CustomIslandSpawner.FormatValue(CustomIslandSpawner.ValueOf(kv.Key));
			UIKit.LabelOf(buttons["Key_Journal"]).text = listenFor == true ? "press..." : ModKeys.Name(ModKeys.Journal);
			UIKit.LabelOf(buttons["Key_Stats"]).text = listenFor == false ? "press..." : ModKeys.Name(ModKeys.Stats);
			UIKit.SetActive(buttons["Key_Journal"], listenFor == true);
			UIKit.SetActive(buttons["Key_Stats"], listenFor == false);
			if (refreshCamera != null) refreshCamera();
			Button receiver = buttons["showOnReceiver"];
			UIKit.LabelOf(receiver).text = "Receiver dots:  " + (CustomIslandSpawner.ShowOnReceiver ? "ON" : "off");
			UIKit.SetActive(receiver, CustomIslandSpawner.ShowOnReceiver);
			foreach (TerrainPainter.Style st in TerrainPainter.Styles)
			{
				Button b;
				if (buttons.TryGetValue("Style_" + st.Name, out b)) UIKit.SetActive(b, CustomIslandSpawner.GeneratedStyles.Select(TerrainPainter.StyleName).Contains(st.Name));
			}
			foreach (string key in GenGather.LandKeys.Concat(GenGather.SeaKeys))
			{
				Button b;
				if (buttons.TryGetValue("Gather_" + key, out b)) UIKit.SetActive(b, GenGather.IsOn(CustomIslandSpawner.GeneratedGatherOff, key));
			}
		}

		static void SetStatus(string message, bool warning)
		{
			if (status == null) return;
			status.text = message;
			status.color = warning ? new Color(1f, 0.72f, 0.45f) : UIKit.TextColor;
		}
	}
}
