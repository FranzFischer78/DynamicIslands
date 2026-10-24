using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The mod's settings for a new world, in one place. Raft's New Game box stays as Raft made it, with the Custom Islands
	/// plan and, under it, "WORLD SETTINGS..." (saying how many settings differ from Raft's own). That opens a window over
	/// the menu with everything else, grouped:
	///   - World rules: the monster difficulty and the build cost (NewWorldRulesBox);
	///   - World randomizer: how much, and which parts (NewWorldOptions.BuildRandomizer);
	///   - Extra options: scrambled blueprints, story islands in a new order, ghost rafts, private storages (WorldOptions),
	///     each switched with its own button and explained under it;
	///   - Islands while sailing: which islands of the spawn pool the world gets (IslandPickerWindow, WorldIslands).
	/// "Raft's own" puts every one of them back to plain Raft; "Defaults..." opens this PC's spawnpool.txt settings
	/// (DefaultsWindow); "Done" closes it. The choices are kept for the world being
	/// made and remembered for the next one when Create is pressed.
	/// </summary>
	[HarmonyPatch(typeof(NewGameBox), "Open")]
	public static class WorldSettingsWindow
	{
		public const string ButtonName = "CustomIslands_WorldSettings", WindowName = "CustomIslands_WorldSettingsWindow";
		/// <summary>The extra options' list: five in view (each 91 high, 6 apart), the rest scrolled to.</summary>
		const float ListHeight = 5 * 91f + 4 * 6f + 4f;

		static Button openButton;
		static RectTransform window;
		static readonly Dictionary<string, Button> toggles = new Dictionary<string, Button>();
		static Text summary;
		static Button levelsToggle;
		public static Button LevelsToggle { get { return levelsToggle; } }
		static Button headStartToggle;
		public static Button HeadStartToggle { get { return headStartToggle; } }
		static bool hooked;

		/// <summary>The options shown (the last choice until one is made).</summary>
		public static HashSet<string> Chosen { get { if (WorldOptions.Pending == null) WorldOptions.Pending = WorldOptions.Defaults; return WorldOptions.Pending; } }

		public static bool IsOpen { get { return window != null && window.gameObject.activeSelf; } }
		public static Button OpenButton { get { return openButton; } }
		public static Button Toggle(string option) { Button b; return toggles.TryGetValue(option, out b) ? b : null; }
		public static RectTransform Window { get { return window; } }

		/// <summary>How many of the settings differ from plain Raft (the box's button says so).</summary>
		public static int Changed
		{
			get
			{
				return Chosen.Count + (PlayerLevels.Chosen ? 1 : 0) + (HeadStart.Chosen > 0 ? 1 : 0) + (NewWorldOptions.Randomizer.On ? 1 : 0) + (NewWorldRulesBox.MonsterLevel != MonsterDifficulty.Normal ? 1 : 0) + (NewWorldRulesBox.BuildPercent > 0 ? 1 : 0) + (NewWorldRulesBox.LooksLevel != AnimalLooks.Normal ? 1 : 0) +
					(WorldIslands.Chosen.Count > 0 ? 1 : 0); // (islands left out: the button said "Raft's own" while some were)
			}
		}

		static void Postfix(NewGameBox __instance)
		{
			try { Ensure(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Adding World settings to the New Game box: " + e); }
		}

		static void Ensure(NewGameBox box)
		{
			if (openButton == null)
			{
				// Under the Custom Islands plan, right of Raft's Create button
				RectTransform holder = UIKit.Rect(ButtonName, box.transform);
				holder.anchorMin = holder.anchorMax = new Vector2(1f, 0f);
				holder.pivot = new Vector2(1f, 0f);
				holder.anchoredPosition = new Vector2(-16f, 44f);
				holder.sizeDelta = new Vector2(292f, 34f);
				UIKit.Horizontal(holder.gameObject, 0f, new RectOffset(0, 0, 0, 0));
				openButton = UIKit.Button(holder, "", Open, "The world's settings: monster difficulty, build cost, the world randomizer and the extra options", -1, 34f, 14);
				UIKit.Primary(openButton);
			}
			if (window == null) Build(box);
			window.gameObject.SetActive(false);
			if (!hooked) { NewWorldOptions.Shown += Show; hooked = true; }
			Show();
		}

		static void Build(NewGameBox box)
		{
			// Over the whole menu (not inside the box: it has room for Raft's own things only)
			Canvas canvas = box.GetComponentInParent<Canvas>();
			Transform screen = canvas != null ? canvas.rootCanvas.transform : box.transform;
			window = UIKit.Rect(WindowName, screen);
			UIKit.Stretch(window);
			window.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f); // (the menu behind it dimmed, its clicks taken)
			RectTransform panel = UIKit.Panel(window, "Panel", new RectOffset(18, 18, 12, 14), 8f);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(930f, 0f));
			RectTransform head = UIKit.Row(panel, 30f, 6f, "Head");
			Text title = UIKit.Label(head, "WORLD SETTINGS", 22, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(title);
			UIKit.Label(head, "for the world you create - every player in it gets them from the host", 12, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Italic);

			// (as tall as its taller column: no empty band under them)
			RectTransform columns = UIKit.Rect("Columns", panel);
			UIKit.Horizontal(columns.gameObject, 16f).childForceExpandWidth = false;
			columns.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
			// Left: the world's rules and the randomizer
			RectTransform left = UIKit.Rect("Left", columns);
			UIKit.Vertical(left.gameObject, 8f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(left.gameObject, 430);
			Text rulesTitle = UIKit.Label(left, "WORLD RULES", 14, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "RulesTitle");
			UIKit.Size(rulesTitle.gameObject, -1, 18);
			NewWorldRulesBox.BuildInto(left);
			NewWorldOptions.BuildRandomizer(left);
			IslandPickerWindow.BuildEntry(left, screen);
			// Right: the extra options
			RectTransform right = UIKit.Rect("Right", columns);
			UIKit.Vertical(right.gameObject, 6f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(right.gameObject, 450);
			Text optionsTitle = UIKit.Label(right, "EXTRA OPTIONS", 14, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "OptionsTitle");
			UIKit.Size(optionsTitle.gameObject, -1, 18);
			toggles.Clear();
			// (one list, about five options in view and the rest scrolled to: all of them would make the window taller than the screen)
			ScrollRect scroll;
			RectTransform list = UIKit.ScrollList(right, out scroll, 6f);
			scroll.name = "OptionsScroll";
			UIKit.Size(scroll.gameObject, -1, ListHeight);
			for (int i = 0; i < WorldOptions.All.Length; i++)
			{
				string option = WorldOptions.All[i];
				if (WorldOptions.IsRetired(option)) continue;
				RectTransform group = UIKit.Group(list, null, "Option_" + option);
				Button b = UIKit.Button(group, "", () => Flip(option), WorldOptions.Hints[i], -1, 28f, 14);
				b.name = "Toggle_" + option;
				toggles[option] = b;
				Text d = UIKit.Label(group, WorldOptions.Shorts[i], 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Detail");
				d.horizontalOverflow = HorizontalWrapMode.Wrap;
				UIKit.Size(d.gameObject, -1, 44);
			}
			// The level up system (also switched on by an island made with it, unless switched off here or with Levels off)
			{
				const string hint = "Players earn EXP from monsters and spend stat points (K) on speed, damage, health and more. Off: only an island made with the level up system switches it on. The host can change it in a world: Levels on / off.";
				RectTransform group = UIKit.Group(list, null, "Option_levels");
				levelsToggle = UIKit.Button(group, "", () => { PlayerLevels.Chosen = !PlayerLevels.Chosen; Show(); }, hint, -1, 28f, 14);
				levelsToggle.name = "Toggle_levels";
				Text d = UIKit.Label(group, hint, 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Detail");
				d.horizontalOverflow = HorizontalWrapMode.Wrap;
				UIKit.Size(d.gameObject, -1, 44);
			}
			// Head start raft: off, level 1, 2 or 3 - each press the next
			{
				const string hint = "The world starts on a bigger raft that is already set up. Level 1: 6 x 12 foundations, a grill, a purifier, the research table, a bed and a storage of first supplies. Level 2: 8 x 14, also smelters, a sail, 10 item nets and island resources. Level 3: 10 x 20, also the Receiver with its antennas.";
				RectTransform group = UIKit.Group(list, null, "Option_headstart");
				headStartToggle = UIKit.Button(group, "", () => { HeadStart.Chosen = (HeadStart.Chosen + 1) % 4; Show(); }, hint, -1, 28f, 14);
				headStartToggle.name = "Toggle_headstart";
				Text d = UIKit.Label(group, "The world starts on a bigger raft, already set up: level 1 with a grill, purifier, research table and bed, level 2 also smelters, a sail and nets, level 3 also the Receiver.", 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "Detail");
				d.horizontalOverflow = HorizontalWrapMode.Wrap;
				UIKit.Size(d.gameObject, -1, 44);
			}

			summary =UIKit.Label(panel, "", 12, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, "Summary");
			summary.horizontalOverflow = HorizontalWrapMode.Wrap;
			UIKit.Size(summary.gameObject, -1, 30);
			RectTransform buttons = UIKit.Row(panel, 34f, 8f, "Buttons");
			UIKit.Button(buttons, "Raft's own", RaftsOwn, "Every setting back to plain Raft: Normal monsters, Raft's build cost, no randomizer, no extra options", 150, 34f, 14);
			// (spawnpool.txt's settings for every world this PC hosts, without editing the file - ROADMAP AU46)
			UIKit.Button(buttons, "Defaults...", DefaultsWindow.Open, "This PC's settings for every world it hosts: random islands, spacing, distances, the Receiver, regrow days, generated islands", 150, 34f, 14).name = "Button_Defaults";
			UIKit.Label(buttons, "", 12);
			Button done = UIKit.Button(buttons, "Done", Close, "Keep these settings for the world you create", 150, 34f, 15);
			UIKit.Primary(done);
			EscKey.Add(window.gameObject, Close); // (Esc = Done)
			window.gameObject.SetActive(false);
			Debug.Log("[CUSTOM ISLANDS] New Game box: World settings (world rules, randomizer, extra options) in their own window");
		}

		public static void Open()
		{
			if (window == null) return;
			window.gameObject.SetActive(true);
			window.SetAsLastSibling();
			NewWorldRulesBox.Refresh();
			Show();
		}

		public static void Close()
		{
			if (window != null) window.gameObject.SetActive(false);
			Show();
		}

		public static void Flip(string option)
		{
			if (!Chosen.Remove(option)) Chosen.Add(option);
			Show();
		}

		/// <summary>Everything back to plain Raft: no extra options, the randomizer off, Normal monsters, Raft's build cost.</summary>
		public static void RaftsOwn()
		{
			Chosen.Clear();
			PlayerLevels.Chosen = false;
			HeadStart.Chosen = 0;
			NewWorldOptions.Randomizer.Level = RandomizerSettings.Off;
			NewWorldOptions.Randomizer.Disabled.Clear();
			NewWorldRulesBox.MonsterLevel = MonsterDifficulty.Normal;
			NewWorldRulesBox.BuildPercent = 0;
			NewWorldRulesBox.LooksLevel = AnimalLooks.Normal;
			NewWorldOptions.Refresh();
			Show();
		}

		internal static void Show()
		{
			HashSet<string> on = Chosen;
			foreach (var kv in toggles)
			{
				if (kv.Value == null) continue;
				bool active = on.Contains(kv.Key);
				UIKit.LabelOf(kv.Value).text = WorldOptions.Label(kv.Key) + ":  " + (active ? "ON" : "off");
				UIKit.SetActive(kv.Value, active);
			}
			if (levelsToggle != null)
			{
				UIKit.LabelOf(levelsToggle).text = "Level up system:  " + (PlayerLevels.Chosen ? "ON" : "off");
				UIKit.SetActive(levelsToggle, PlayerLevels.Chosen);
			}
			if (headStartToggle != null)
			{
				UIKit.LabelOf(headStartToggle).text = "Head start raft:  " + (HeadStart.Chosen > 0 ? "LEVEL " + HeadStart.Chosen : "off");
				UIKit.SetActive(headStartToggle, HeadStart.Chosen > 0);
			}
			IslandPickerWindow.ShowEntry();
			int changed = Changed;
			if (openButton != null) UIKit.LabelOf(openButton).text = "WORLD SETTINGS...   " + (changed == 0 ? "Raft's own" : changed + " changed");
			if (summary != null)
				summary.text = "Now: monsters " + MonsterDifficulty.Describe(NewWorldRulesBox.MonsterLevel) + ", build cost " + BuildCost.Describe(NewWorldRulesBox.BuildPercent) + ", rare animal looks " + AnimalLooks.Describe(NewWorldRulesBox.LooksLevel) +
					", the world randomizer " + NewWorldOptions.Randomizer.Describe() + ", extra options: " + WorldOptions.Describe(on).ToLowerInvariant() + ", levels " + (PlayerLevels.Chosen ? "on" : "off") + ", head start raft " + HeadStart.Describe(HeadStart.Chosen) +
					(WorldIslands.Chosen.Count > 0 ? ", islands while sailing: " + WorldIslands.Chosen.Count + " left out" : "") +
					". The Custom Islands plan ('" + NewWorldOptions.Selected + "') is chosen in the New Game box.";
		}
	}

	/// <summary>Create pressed: the world gets the options shown, and they are remembered for the next one.</summary>
	[HarmonyPatch(typeof(NewGameBox), "Button_CreateNewGame")]
	static class WorldSettingsChoice
	{
		static void Prefix()
		{
			try
			{
				HashSet<string> on = WorldSettingsWindow.Chosen;
				WorldOptions.SaveDefaults(on);
				WorldIslands.SaveDefaults(WorldIslands.Chosen);
				PlayerLevels.SaveDefault(PlayerLevels.Chosen);
				HeadStart.SaveDefault(HeadStart.Chosen);
				Debug.Log("[CUSTOM ISLANDS] Creating a world with the options " + WorldOptions.Describe(on) + "; islands left out while sailing: " +
					(WorldIslands.Chosen.Count == 0 ? "none" : string.Join(", ", WorldIslands.Chosen.Select(WorldIslands.Label).ToArray())));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] New world options: " + e); }
		}
	}
}
