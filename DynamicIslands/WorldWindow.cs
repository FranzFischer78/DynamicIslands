using HMLLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The world's Custom Islands settings in a running world - a "CUSTOM ISLANDS" button in Raft's pause menu (Esc). The
	/// same groups as World settings in the New Game box: the world rules (monsters, build cost), the world randomizer,
	/// the extra options and the level up system, which islands turn up while sailing, and the world's plan and story. The
	/// host changes them here for every player (the same as the F10 commands Monsters, BuildCost, Randomizer, WorldOptions,
	/// Levels, WorldIslands); other players see the host's settings, read only. Regrow days, the Receiver and the unload
	/// distance are not here (the user, 2026-10-10): F10 RegrowDays, and Defaults... in the New Game box (spawnpool.txt).
	/// Compact (the user, 2026-10-10: it was taller than the screen): two columns of small groups, tick-box rows
	/// (UIKit.Check) for the parts, options and islands, the options and islands in scrolling lists, the whole body in a
	/// scroll area when the screen is too short. Each group says what a change does in a world already under way (MidGame,
	/// from the code's Set paths); a row's full description shows in the hint line at the bottom while it is hovered.
	/// </summary>
	public class WorldWindow : MonoBehaviour
	{
		public const string CanvasName = "CustomIslands_WorldWindow", ButtonName = "CustomIslands_PauseButton";
		/// <summary>The panel's most height (canvas units; the canvas is at least 800 high) and its width.</summary>
		const float MaxHeight = 780f, Width = 960f, RowH = 22f, CheckH = 22f, OptionRowH = 26f, NameWidth = 150f;
		static Canvas canvas;
		static WorldWindow instance;
		static Text hostText, planText, islandsText, hintText, headStartText;
		static RectTransform panel, body, bodyView, islandList;
		static readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
		static float nextRefresh;
		/// <summary>
		/// The spawn pool's entries as last read, with what they were read from (AU41): the window refreshes
		/// every second so a client sees the host's changes, and that read every saved island's name each
		/// time. They are read again only when their signature changed - the islands folder's and spawnpool.txt's times.
		/// The island list's buttons are made again only when the entries changed.
		/// </summary>
		static List<string> candidates, filledFrom;
		static string candidatesSig;

		const string DefaultHint = "Point at a setting to read what it does.  The same with F10: Monsters, BuildCost, Randomizer, WorldOptions, Levels, WorldIslands.";

		#region What a change does in a running world (from the code; GUIDE "Changing settings in a running world")

		/// <summary>Monsters: MonsterDamagePatch scales every hit (MonsterDifficulty.Scale) - nothing is stored on the animals.
		/// Build cost: BuildCost.Set -> Refresh puts the new amounts on the build menu; BuildCostRefund gives a block back by the
		/// cost it was placed at.</summary>
		internal const string RulesMidGame = "Now: monsters at once, on every hit - those already out too. Build cost: the build menu at once; a block built before gives back what it cost then.";
		/// <summary>WorldRandomizer.Set: animals looked at again (animalsSeen cleared; a changed one is never changed back), loot
		/// off puts crates back (RestoreLoot), extras once per island of Raft's (seen), sailing islands from OnSailed on.</summary>
		internal const string RandomizerMidGame = "Now: colours and alphas for animals not changed yet (changed ones keep their look). Animals, loot and finds for Raft's islands not met yet - islands already looked at keep what they got; loot off puts crates back at once. Oddities, bosses, large: from now on while sailing.";
		/// <summary>WorldIslands.Set: the spawner's next pick (TakesPart); nothing already here is removed.</summary>
		internal const string IslandsMidGame = "Now: the next random island is picked from this list; islands already in the world stay.";
		/// <summary>The plan is chosen in the New Game box and stays with the world (the user, 2026-10-10: no plan list here).</summary>
		internal const string PlanMidGame = "Chosen when the world was made; it stays. Which random islands come while sailing is set under Islands while sailing.";
		internal const string LevelsMidGame = "At once. Off keeps everyone's levels and takes the stat points' bonuses away until it is on again.";

		/// <summary>What switching an extra option does in a world under way (the options' own On checks - WorldOptions.Set only
		/// sets, sends and saves; no option is reset by it).</summary>
		internal static string MidGame(string option)
		{
			switch (option)
			{
				case WorldOptions.Blueprints: return "Off: blueprints not yet taken go back to Raft's places; learned ones stay.";
				case WorldOptions.StoryOrder: return "Off: the Receiver's list goes back to Raft's order at once.";
				case WorldOptions.GhostRafts: return "From now on: the first after 1.5 km sailed. Off: one afloat stays.";
				case WorldOptions.PrivateStorage: return "Only storages built while on are private. Off: all open at once.";
				case WorldOptions.LongVoyage: return "Spacing at once; fewer islands after the next random island.";
				case WorldOptions.IronRaft: return "At once, from the next shark bite.";
				case WorldOptions.SharedXp: return "At once, from the next monster defeated.";
				case WorldOptions.NightDanger: return "At once: monsters' hits and the shark's visits.";
				case WorldOptions.DailyQuest: return "On: a task by daylight. Off: today's task waits, unchanged.";
				case WorldOptions.RogueShark: return "On: today's chance at once. Off: one out stays until killed.";
				case WorldOptions.Barrels: return "Barrels from now on. Off: no extra loot, at once.";
				case WorldOptions.StormDays: return "At once: a storm day starts or ends within seconds.";
				case WorldOptions.GlowSea: return "At once: tonight's glow starts or fades within seconds.";
				case WorldOptions.TraderRaft: return "From now on: the first after 3 km sailed. Off: one afloat stays.";
				case WorldOptions.Kraken: return "From now on. Off: an arm holding the raft lets go at once.";
				case WorldOptions.GhostShip: return "From the next nightfall. Off: a ship out goes at once.";
				case WorldOptions.Upgrades: return "Off: recipes hidden; built upgrades keep working, learned stay.";
				case WorldOptions.StormSeas: return "At once: Raft's next weather; a storm blowing rocks harder.";
				default: return "";
			}
		}

		#endregion

		public static bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }
		/// <summary>Tests: a button of the window by its name ("Monsters_Savage", "Option_ghostrafts", "Levels"...).</summary>
		public static Button ButtonNamed(string name) { Button b; return buttons.TryGetValue(name, out b) ? b : null; }
		/// <summary>Tests: whether a tick-box row of the window ("Option_ghostrafts", "Levels", "Part_colours", "Island_...") is ticked.</summary>
		public static bool Ticked(string name) { return UIKit.IsChecked(ButtonNamed(name)); }
		static bool Host { get { return Raft_Network.IsHost; } }

		static void Build()
		{
			canvas = UIKit.CreateCanvas(CanvasName, 870); // (over Raft's pause menu, under the info boxes and ? popups)
			DontDestroyOnLoad(canvas.gameObject);
			instance = canvas.gameObject.AddComponent<WorldWindow>();
			RectTransform root = (RectTransform)canvas.transform;
			RectTransform dim = UIKit.Rect("Dim", root);
			UIKit.Stretch(dim);
			dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
			// (its height is set each frame: as tall as its content, never taller than the screen - FitHeight)
			panel = UIKit.Panel(root, "Panel", new RectOffset(16, 16, 10, 12), 5f, false);
			UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 700f));
			RectTransform head = UIKit.Row(panel, 26f, 6f, "Head");
			Text title = UIKit.Label(head, "CUSTOM ISLANDS - THIS WORLD", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Title");
			UIKit.UseTitleFont(title);
			hostText = UIKit.Label(head, "", 12, UIKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Italic, "Who");
			Text top = UIKit.Label(panel, "Changes are saved with the world and sent to every player; only the host can change them. Each group says what a change does in a world already under way.", 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Note");
			UIKit.Size(top.gameObject, -1, 14); UIKit.Fit(top, 9);

			// The body scrolls when the screen is too short for it
			ScrollRect bodyScroll;
			body = UIKit.ScrollList(panel, out bodyScroll, 0f);
			bodyScroll.name = "BodyScroll";
			bodyView = (RectTransform)bodyScroll.transform;
			LayoutElement bodySize = UIKit.Size(bodyView.gameObject, -1, -1);
			bodySize.flexibleHeight = 1; bodySize.minHeight = 120f;

			RectTransform columns = UIKit.Rect("Columns", body);
			UIKit.Horizontal(columns.gameObject, 12f).childForceExpandWidth = false;
			HorizontalLayoutGroup ch = columns.GetComponent<HorizontalLayoutGroup>();
			ch.childAlignment = TextAnchor.UpperLeft; ch.childForceExpandHeight = false;
			RectTransform left = UIKit.Rect("Left", columns);
			UIKit.Vertical(left.gameObject, 5f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(left.gameObject, 450);
			RectTransform right = UIKit.Rect("Right", columns);
			UIKit.Vertical(right.gameObject, 5f, new RectOffset(0, 0, 0, 0));
			UIKit.Size(right.gameObject, 450);

			BuildRules(left);
			BuildRandomizer(left);
			BuildIslands(left);
			BuildOptions(right);
			BuildPlan(right);

			// The hovered setting's full description (the editor's status bar isn't there in a world)
			RectTransform hintBox = UIKit.Rect("HintLine", panel);
			UIKit.Background(hintBox.gameObject, UIKit.GroupBg, 5);
			UIKit.Size(hintBox.gameObject, -1, 30);
			hintText = UIKit.Label(hintBox, DefaultHint, 11, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Italic, "Hint");
			UIKit.Stretch(hintText.rectTransform, 8, 8, 1, 1);
			UIKit.Fit(hintText, 9);
			UIKit.HintChanged += h => { if (hintText != null && IsOpen) hintText.text = string.IsNullOrEmpty(h) ? DefaultHint : h; };
			UIKit.HintBars.Add(() => IsOpen);

			RectTransform buttonsRow = UIKit.Row(panel, 30f, 8f, "Buttons");
			UIKit.Label(buttonsRow, "", 11);
			// (while trying an island from the editor: the starter kit - on for the next Test, or given again now)
			Add("TestKit", UIKit.Button(buttonsRow, "", () => { IslandTest.KitOn = !IslandTest.KitOn; Refresh(); }, "Test in a world starts you with a starter kit: weapons, tools, a head light, flippers, an oxygen bottle, food and water, hunger and thirst full. Switch it off to try the island with empty hands (from the next Test)", 120, 30f, 13));
			Add("GiveKit", UIKit.Button(buttonsRow, "Give the kit", () => { List<string> got = IslandTest.GiveKit(); DynamicIslands.Notify(got.Count > 0 ? "Starter kit: " + string.Join(", ", got.ToArray()) : "The starter kit couldn't be given"); }, "The starter kit again, now (lost it, used it up), and hunger, thirst, health and oxygen full", 120, 30f, 13));
			Add("BackToEditor", UIKit.Button(buttonsRow, "Back to the editor", IslandTest.Back, "Leave the test world without saving it and open the island in the editor again", 170, 30f, 13));
			Button close = UIKit.Button(buttonsRow, "Close", Close, "Back to the game menu (Esc)", 130, 30f, 14);
			UIKit.Primary(close);
			canvas.gameObject.SetActive(false);
		}

		/// <summary>A group (UIKit.Group) a little tighter than the editor's.</summary>
		static RectTransform Section(Transform parent, string title, string name)
		{
			RectTransform g = UIKit.Group(parent, title, name);
			VerticalLayoutGroup v = g.GetComponent<VerticalLayoutGroup>();
			v.spacing = 3f; v.padding = new RectOffset(8, 8, 3, 6);
			return g;
		}

		/// <summary>A small italic line (or two, three) under a group's controls: what a change does in a running world.</summary>
		static Text Note(Transform parent, string text, int lines, string name = "MidGame")
		{
			Text t = UIKit.Label(parent, text, 10, UIKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, name);
			t.horizontalOverflow = HorizontalWrapMode.Wrap;
			t.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(t.gameObject, -1, lines * 12 + 1);
			return t;
		}

		static void BuildRules(Transform left)
		{
			RectTransform rules = Section(left, "World rules", "Rules");
			RectTransform mrow = UIKit.Row(rules, RowH, 3f, "Monsters");
			UIKit.Size(UIKit.Label(mrow, "Monsters", 11, UIKit.TextMuted).gameObject, 62);
			for (int i = 0; i < MonsterDifficulty.Names.Length; i++)
			{
				int level = i;
				Add("Monsters_" + MonsterDifficulty.Names[i], UIKit.Button(mrow, MonsterDifficulty.Names[i], () => { MonsterDifficulty.Set(level); Refresh(); }, "Monster difficulty - monsters' health and the damage they deal: " + MonsterDifficulty.Describe(level) + ". In this world now: at once, on the next hit.", -1, RowH, 11));
			}
			RectTransform crow = UIKit.Row(rules, RowH, 3f, "BuildCost");
			UIKit.Size(UIKit.Label(crow, "Build cost", 11, UIKit.TextMuted).gameObject, 62);
			Add("BuildCost_Less", UIKit.Button(crow, "- 5 %", () => { BuildCost.Set(Mathf.Max(0, BuildCost.Current - BuildCost.Step)); Refresh(); }, "The build menu costs 5 % less, at once. Blocks built before keep giving back by the cost they were built at", 60, RowH, 11));
			Add("BuildCost_Value", UIKit.Button(crow, "", () => { }, "How many more materials the build menu costs than in Raft", -1, RowH, 11));
			Add("BuildCost_More", UIKit.Button(crow, "+ 5 %", () => { BuildCost.Set(Mathf.Min(BuildCost.Max, BuildCost.Current + BuildCost.Step)); Refresh(); }, "The build menu costs 5 % more, at once. Blocks built before keep giving back by the cost they were built at", 60, RowH, 11));
			RectTransform lrow = UIKit.Row(rules, RowH, 3f, "RareLooks");
			UIKit.Size(UIKit.Label(lrow, "Rare looks", 11, UIKit.TextMuted).gameObject, 62);
			for (int i = 0; i < AnimalLooks.Names.Length; i++)
			{
				int level = i;
				Add("RareLooks_" + AnimalLooks.Names[i], UIKit.Button(lrow, AnimalLooks.Names[i], () => { AnimalLooks.Set(level); Refresh(); }, "Rare animal looks - how often cluckers, goats and llamas come with a rare look: " + AnimalLooks.DescriptionFor(level) + " In this world now: for animals that come from now on.", -1, RowH, 11));
			}
			Note(rules, RulesMidGame, 2);
		}

		static void BuildRandomizer(Transform left)
		{
			RectTransform rnd = Section(left, "World randomizer", "Randomizer");
			RectTransform rrow = UIKit.Row(rnd, RowH, 3f, "Level");
			for (int i = 0; i < RandomizerSettings.LevelNames.Length; i++)
			{
				int level = i;
				Add("Randomizer_" + RandomizerSettings.LevelNames[i], UIKit.Button(rrow, RandomizerSettings.LevelNames[i], () => SetRandomizer(s => s.Level = level), RandomizerSettings.LevelHint(level), -1, RowH, 11));
			}
			for (int row = 0; row < 2; row++)
			{
				RectTransform prow = UIKit.Row(rnd, CheckH, 2f, "Parts" + row);
				for (int i = row * 4; i < Math.Min(RandomizerSettings.Features.Length, row * 4 + 4); i++)
				{
					string part = RandomizerSettings.Features[i];
					Add("Part_" + part, Cell(UIKit.Check(prow, RandomizerSettings.FeatureLabels[i], () => SetRandomizer(s => { if (!s.Disabled.Remove(part)) s.Disabled.Add(part); }), RandomizerSettings.FeatureHints[i] + " " + PartMidGame(part), CheckH, 11)));
				}
			}
			Note(rnd, RandomizerMidGame, 3);
		}

		/// <summary>One randomizer part switched in a world under way (WorldRandomizer.Set, VariantOf, GiveExtras, OnSailed).</summary>
		static string PartMidGame(string part)
		{
			switch (part)
			{
				case RandomizerSettings.Colours:
				case RandomizerSettings.Alphas: return "IN THIS WORLD NOW: animals are looked at again at once; one already changed keeps its look.";
				case RandomizerSettings.Loot: return "IN THIS WORLD NOW: Raft's islands not met yet; off puts moved crates and clams back at once.";
				case RandomizerSettings.Animals:
				case RandomizerSettings.Finds: return "IN THIS WORLD NOW: Raft's islands not met yet; an island already looked at keeps what it got.";
				default: return "IN THIS WORLD NOW: counts while sailing from now on; off: one due doesn't come, islands already here stay.";
			}
		}

		static void BuildIslands(Transform left)
		{
			RectTransform box = Section(left, "Islands while sailing", "IslandsWhileSailing");
			islandsText = UIKit.Label(box, "", 10, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, "IslandsNote");
			islandsText.horizontalOverflow = HorizontalWrapMode.Wrap;
			islandsText.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(islandsText.gameObject, -1, 25);
			RectTransform listBox = UIKit.Rect("IslandList", box);
			UIKit.Size(listBox.gameObject, -1, 140);
			ScrollRect scroll;
			islandList = UIKit.ScrollList(listBox, out scroll, 0f);
			UIKit.Stretch((RectTransform)scroll.transform, 0, 0, 0, 0);
			Note(box, IslandsMidGame, 1);
		}

		static void BuildOptions(Transform right)
		{
			RectTransform box = Section(right, "Extra options", "ExtraOptions");
			Note(box, "Each row: what switching it does in this world now. Point at a row for what the option is.", 1, "OptionsNote");
			RectTransform listBox = UIKit.Rect("OptionList", box);
			UIKit.Size(listBox.gameObject, -1, 370);
			ScrollRect scroll;
			RectTransform list = UIKit.ScrollList(listBox, out scroll, 1f);
			scroll.name = "OptionsScroll";
			UIKit.Stretch((RectTransform)scroll.transform, 0, 0, 0, 0);
			for (int i = 0; i < WorldOptions.All.Length; i++)
			{
				string option = WorldOptions.All[i];
				Add("Option_" + option, OptionRow(list, WorldOptions.Labels[i], () => { var on = new HashSet<string>(WorldOptions.Current); if (!on.Remove(option)) on.Add(option); WorldOptions.Set(on); Refresh(); },
					WorldOptions.Hints[i], MidGame(option)));
			}
			Add("Levels", OptionRow(list, "Level up system", () => { PlayerLevels.SetEnabled(!PlayerLevels.On); Refresh(); },
				"Players earn EXP from monsters and spend stat points (K) on speed, damage, health and more.", LevelsMidGame));
			// (the head start raft is built once, when the world is made - HeadStart.OnWorldRead: shown, not switched)
			RectTransform hs = UIKit.Row(list, OptionRowH, 6f, "HeadStart");
			UIKit.Hint(hs.gameObject, "The head start raft is built once, when the world is made (chosen in the New Game box's World settings). It can't be added or taken away in a running world.");
			headStartText = UIKit.Label(hs, "", 11, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "Text");
			headStartText.raycastTarget = true; // (its hint)
		}

		/// <summary>An extra option's row: tick box and name, and on its right what switching it does in a world under way.</summary>
		static Button OptionRow(Transform list, string name, Action onClick, string hint, string midGame)
		{
			Button b = UIKit.Check(list, name, onClick, hint + (midGame.Length > 0 ? "  IN THIS WORLD NOW: " + midGame : ""), OptionRowH, 12);
			UIKit.Size(UIKit.LabelOf(b).gameObject, NameWidth, OptionRowH);
			Text effect = UIKit.Label(b.transform, midGame, 10, UIKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, "MidGame");
			effect.horizontalOverflow = HorizontalWrapMode.Wrap;
			effect.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(effect.gameObject, -1, OptionRowH);
			return b;
		}

		static void BuildPlan(Transform right)
		{
			RectTransform box = Section(right, "Plan and story", "PlanAndStory");
			// (the plan is shown, not picked: a world keeps the plan it was made with - the user, 2026-10-10)
			planText = UIKit.Label(box, "", 11, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, "Plan");
			planText.horizontalOverflow = HorizontalWrapMode.Wrap;
			planText.verticalOverflow = VerticalWrapMode.Truncate;
			UIKit.Size(planText.gameObject, -1, 76);
			Note(box, PlanMidGame, 2);
			// (no list of the world's islands here any more: the quest book shows where each island's quest is - the user, 2026-10-10)
		}

		/// <summary>The panel as tall as its content (the body's rows and the fixed lines around them), never taller than the screen.</summary>
		void LateUpdate()
		{
			if (panel == null || body == null || bodyView == null) return;
			RectTransform root = (RectTransform)canvas.transform;
			float chrome = panel.rect.height - bodyView.rect.height;
			float want = Mathf.Min(MaxHeight, root.rect.height - 16f, body.rect.height + chrome);
			if (Mathf.Abs(panel.sizeDelta.y - want) > 0.5f) panel.sizeDelta = new Vector2(Width, Mathf.Max(200f, want));
		}


		static void Add(string name, Button b) { b.name = name; buttons[name] = b; }

		/// <summary>A tick box sharing its line equally with the others (not as wide as its text), so the columns line up.</summary>
		static Button Cell(Button b) { UIKit.Size(b.gameObject, 0, CheckH, 1); return b; }

		static void SetRandomizer(Action<RandomizerSettings> change)
		{
			RandomizerSettings s = WorldRandomizer.Current.Copy();
			change(s);
			WorldRandomizer.Set(s);
			Refresh();
		}

		public static void Open()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) return;
			if (canvas == null) Build();
			canvas.gameObject.SetActive(true);
			// (read afresh each time the window opens)
			candidatesSig = null;
			FillIslands();
			Refresh();
		}

		/// <summary>A cheap look at what the island list depends on: the islands folder's time (a file added, removed or renamed) and spawnpool.txt's. Null if it can't be told.</summary>
		static string CandidatesSignature()
		{
			try
			{
				string folder = DynamicIslands.assetpath, pool = System.IO.Path.Combine(folder, CustomIslandSpawner.PoolFileName);
				return (System.IO.Directory.Exists(folder) ? System.IO.Directory.GetLastWriteTimeUtc(folder).Ticks : 0L) + "/" + (System.IO.File.Exists(pool) ? System.IO.File.GetLastWriteTimeUtc(pool).Ticks : 0L);
			}
			catch { return null; }
		}

		/// <summary>WorldIslands.Candidates, read again only when the islands folder or spawnpool.txt changed.</summary>
		static List<string> Candidates()
		{
			string sig = CandidatesSignature();
			if (candidates == null || sig == null || sig != candidatesSig)
			{
				candidates = WorldIslands.Candidates();
				candidatesSig = sig;
			}
			return candidates;
		}


		public static void Close()
		{
			if (canvas != null) canvas.gameObject.SetActive(false);
		}

		void Update()
		{
			if (EscKey.Down(this)) { Close(); return; }
			if (!LoadSceneManager.IsGameSceneLoaded) { Close(); return; }
			// (a client sees the host's changes as they come)
			if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 1f; Refresh(); }
		}

		/// <summary>The island list: every entry of the spawn pool with a tick box (host); a player sees the host's choice isn't sent to them.</summary>
		static void FillIslands()
		{
			foreach (Transform c in islandList) Destroy(c.gameObject);
			foreach (string key in buttons.Keys.Where(k => k.StartsWith("Island_")).ToList()) buttons.Remove(key);
			filledFrom = null;
			if (!Host) return;
			filledFrom = Candidates();
			// (two tick-box rows to a line)
			RectTransform line = null;
			for (int i = 0; i < filledFrom.Count; i++)
			{
				string e = filledFrom[i];
				if (i % 2 == 0)
				{
					line = UIKit.Row(islandList, CheckH, 4f, "Line" + i / 2);
					line.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
				}
				Button b = Cell(UIKit.Check(line, WorldIslands.Label(e), () => { WorldIslands.Set(e, !WorldIslands.TakesPart(e)); Refresh(); },
					(e == CustomIslandSpawner.GeneratedEntry ? WorldIslands.GeneratedHelp : WorldIslands.Hint(e)) + "  IN THIS WORLD NOW: " + IslandsMidGame, CheckH, 11));
				Add("Island_" + e, b);
			}
			// (an odd count: an empty half keeps the last one half wide)
			if (filledFrom.Count % 2 == 1) UIKit.Size(UIKit.Rect("Empty", line).gameObject, 0, CheckH, 1);
		}

		public static void Refresh()
		{
			if (canvas == null || !canvas.gameObject.activeSelf) return;
			bool host = Host;
			hostText.text = host ? "you are the host: changes are for every player" : "the host's settings - only the host changes them";
			foreach (var kv in buttons) kv.Value.interactable = host && kv.Key != "BuildCost_Value";
			buttons["BackToEditor"].gameObject.SetActive(IslandTest.Testing);
			buttons["TestKit"].gameObject.SetActive(IslandTest.Testing);
			buttons["GiveKit"].gameObject.SetActive(IslandTest.Testing);
			UIKit.LabelOf(buttons["TestKit"]).text = "Kit: " + (IslandTest.KitOn ? "On" : "Off");
			UIKit.SetActive(buttons["TestKit"], IslandTest.KitOn);
			// (a plan tried from the main menu goes back to its plan there, not to the editor)
			if (IslandTest.Testing) UIKit.LabelOf(buttons["BackToEditor"]).text = IslandTest.FromMenu ? "Back to the world plan" : "Back to the editor";
			buttons["BackToEditor"].interactable = true;
			for (int i = 0; i < MonsterDifficulty.Names.Length; i++) UIKit.SetActive(buttons["Monsters_" + MonsterDifficulty.Names[i]], MonsterDifficulty.Current == i);
			for (int i = 0; i < AnimalLooks.Names.Length; i++) UIKit.SetActive(buttons["RareLooks_" + AnimalLooks.Names[i]], AnimalLooks.Current == i);
			UIKit.LabelOf(buttons["BuildCost_Value"]).text = BuildCost.Describe(BuildCost.Current);
			RandomizerSettings r = WorldRandomizer.Current;
			for (int i = 0; i < RandomizerSettings.LevelNames.Length; i++) UIKit.SetActive(buttons["Randomizer_" + RandomizerSettings.LevelNames[i]], r.Level == i);
			foreach (string part in RandomizerSettings.Features)
			{
				Button b = buttons["Part_" + part];
				// (with the randomizer off its parts do nothing: they toggled with nothing to see)
				b.interactable = host && r.Level > RandomizerSettings.Off;
				UIKit.SetCheck(b, r.Level > RandomizerSettings.Off && !r.Disabled.Contains(part));
			}
			foreach (string o in WorldOptions.All)
			{
				Button b = buttons["Option_" + o];
				UIKit.SetCheck(b, WorldOptions.On(o));
				// (an option no longer offered shows only while the world has it on, to switch it off)
				b.gameObject.SetActive(!WorldOptions.IsRetired(o) || WorldOptions.On(o));
			}
			UIKit.SetCheck(buttons["Levels"], PlayerLevels.On);
			headStartText.text = "Head start raft: " + (host ? HeadStart.Describe(HeadStart.Level) : "the host's") + " - built when the world was made; can't change now.";

			if (host)
			{
				List<string> all = Candidates();
				// (an island saved or removed meanwhile: the list made again - only then)
				if (!ReferenceEquals(all, filledFrom)) FillIslands();
				int part = all.Count(WorldIslands.TakesPart);
				islandsText.text = (CustomIslandSpawner.Enabled ? "" : "Random islands are off in this world (its plan, or CustomIslandsAuto off) - the list counts when they are on. ") +
					part + " of " + all.Count + " take part: untick what this world shouldn't meet by chance (islands a plan or quest brings still come).";
				foreach (string e in all)
				{
					Button b;
					if (!buttons.TryGetValue("Island_" + e, out b)) continue;
					UIKit.SetCheck(b, WorldIslands.TakesPart(e));
				}
			}
			else islandsText.text = WorldIslands.DescribeForPlayer();

			planText.text = host ? PlanSummary() : WorldDirector.DescribeForPlayer(false) + (StoryChain.Active ? "\n" + StorySummary() : "");
		}

		/// <summary>The plan and its rules, and the story chain if the plan changed Raft's story (host).</summary>
		static string PlanSummary()
		{
			WorldPlan p = WorldDirector.Plan;
			var lines = new List<string> { "<b>Plan:</b> " + WorldDirector.PlanName + (p == null ? " (missing)" : "") + " - random islands while sailing " + (CustomIslandSpawner.Enabled ? "on" : "off") };
			if (p != null && p.Rules.Count > 0)
			{
				int done = p.Rules.Count(x => WorldDirector.Done.Contains(x.Id) || StoryChain.Brought.Contains(x.Id));
				lines.Add(done + " of " + p.Rules.Count + " island(s) of the plan are here; next: " +
					string.Join(", ", p.Rules.Where(x => !WorldDirector.Done.Contains(x.Id) && !StoryChain.Brought.Contains(x.Id)).Take(3).Select(x => (x.Label.Length > 0 ? x.Label : x.Id)).ToArray()));
			}
			if (StoryChain.Active) lines.Add(StorySummary());
			return string.Join("\n", lines.ToArray());
		}

		static string StorySummary()
		{
			return "<b>Story:</b> " + string.Join(" > ", StoryChain.Steps.Select(s => StoryChain.StepName(s) + (StoryChain.Done.Contains(s) ? " ✓" : StoryChain.Unlocked.Contains(s) ? " (now)" : "")).ToArray());
		}
	}

	/// <summary>
	/// One Esc press closes only our window: Raft's pause menu skips the press while one of our in-world windows is open, or was open
	/// last frame (the window may have closed on this same press before the pause menu's Update ran).
	/// </summary>
	[HarmonyPatch(typeof(PauseMenu), "Update")]
	public static class EscGuard
	{
		static int openFrame = -10;

		/// <summary>Our windows a player sees in a world (each closes itself on Esc).</summary>
		public static bool AnyOpen()
		{
			return JournalWindow.IsOpen || NoteReader.IsOpen || CodeLock.IsOpen || ChoiceWindow.IsOpen || InfoWindow.IsOpen
				|| LevelWindow.IsOpen || BehaviourWindow.IsOpen;
		}

		/// <summary>True when an Esc this frame belongs to one of our windows (tests).</summary>
		public static bool Blocks()
		{
			if (AnyOpen()) openFrame = Time.frameCount;
			return openFrame >= Time.frameCount - 1;
		}

		static bool Prefix()
		{
			try { return !(Blocks() && Input.GetKeyDown(KeyCode.Escape)); }
			catch { return true; }
		}
	}

	/// <summary>Raft's pause menu (Esc in a world) gets a CUSTOM ISLANDS button, a copy of its own buttons, before Exit.</summary>
	[HarmonyPatch(typeof(PauseMenu), "Start")]
	static class WorldWindowButton
	{
		static void Postfix(PauseMenu __instance)
		{
			try
			{
				GameObject holder = __instance.buttonHolderPanel;
				if (holder == null || holder.transform.Find(WorldWindow.ButtonName) != null) return;
				Button[] own = holder.GetComponentsInChildren<Button>(true).Where(b => b.transform.parent == holder.transform).ToArray();
				if (own.Length == 0) return;
				Button model = own[0];
				GameObject copy = UnityEngine.Object.Instantiate(model.gameObject, holder.transform);
				copy.name = WorldWindow.ButtonName;
				// (Raft's localisation would put the model's own word back)
				foreach (Component loc in copy.GetComponentsInChildren<Component>(true).Where(x => x != null && x.GetType().Name.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0).ToList())
					UnityEngine.Object.Destroy(loc);
				foreach (Text t in copy.GetComponentsInChildren<Text>(true)) t.text = "Custom Islands";
				foreach (Component tmp in copy.GetComponentsInChildren<Component>(true).Where(x => x != null && x.GetType().Name.StartsWith("TextMeshPro")))
					Traverse.Create(tmp).Property("text").SetValue("Custom Islands");
				Button button = copy.GetComponent<Button>();
				button.onClick = new Button.ButtonClickedEvent();
				button.onClick.AddListener(WorldWindow.Open);
				copy.transform.SetSiblingIndex(Mathf.Max(0, own[own.Length - 1].transform.GetSiblingIndex()));
				Debug.Log("[CUSTOM ISLANDS] Pause menu: a Custom Islands button (the world's settings)");
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] The pause menu's Custom Islands button: " + e.Message); }
		}
	}
}
