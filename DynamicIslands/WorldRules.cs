using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The rules a world is created with in Raft's New Game box, the same for every player in it: the monster difficulty
	/// (MonsterDifficulty) and the build cost (BuildCost). Saved in the world file ("@monsters=", "@buildcost="), sent by the
	/// host to every player who joins (network kind 15: Index = monster level, Count = build cost %) and again when the
	/// host changes one. A client goes back to Raft's own rules when it joins a world, until the host's arrive.
	/// The last choices in the New Game box are kept in Mods\DynamicIslands\world_rules.txt.
	/// </summary>
	public static class WorldRules
	{
		public const string DefaultFileName = "world_rules.txt";

		static string DefaultPath { get { return Path.Combine(DynamicIslands.assetpath, DefaultFileName); } }

		/// <summary>A last choice from world_rules.txt ("key=value" lines), or null.</summary>
		public static string ReadDefault(string key)
		{
			try
			{
				if (!File.Exists(DefaultPath)) return null;
				foreach (string line in File.ReadAllLines(DefaultPath))
				{
					int eq = line.IndexOf('=');
					if (line.StartsWith("#") || eq < 1) continue;
					if (line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return line.Substring(eq + 1).Trim();
				}
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not read " + DefaultPath + ": " + e.Message); }
			return null;
		}

		public static void SaveDefault(string key, string value)
		{
			SaveDefaults(new Dictionary<string, string> { { key, value } });
		}

		/// <summary>Several last choices at once (one write of world_rules.txt): the other lines stay as they are.</summary>
		public static void SaveDefaults(IDictionary<string, string> changes)
		{
			try
			{
				var values = new List<KeyValuePair<string, string>>();
				if (File.Exists(DefaultPath))
					foreach (string line in File.ReadAllLines(DefaultPath))
					{
						int eq = line.IndexOf('=');
						if (line.StartsWith("#") || eq < 1) continue;
						string k = line.Substring(0, eq).Trim();
						if (!changes.Keys.Any(c => c.Equals(k, StringComparison.OrdinalIgnoreCase))) values.Add(new KeyValuePair<string, string>(k, line.Substring(eq + 1).Trim()));
					}
				values.AddRange(changes);
				SafeFile.WriteAllLines(DefaultPath, new[] { "# The last choices in the New Game box: monsters=timid|normal|fierce|savage|nightmare, buildcost=0-100 (% more)",
					"# journalkey=J, statskey=K: the keys of the journal and the stats page in a world (Unity key names; Defaults > Keys)",
					"# cameralook, cameraspeed, camerazoom, camerapan=1 (x0.1-10), cameradragup=down|up, camerawheel=normal|reversed: the editor camera (Defaults > Editor camera, or the editor's Camera button)" }
					.Concat(values.Select(kv => kv.Key + "=" + kv.Value)).ToArray());
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Could not write " + DefaultPath + ": " + e.Message); }
		}

		/// <summary>
		/// Before a world's island list is read (IslandWorldState, host): a brand-new world gets the rules chosen for it, any
		/// other Raft's own until its lines are read. (A client's are reset when the host's world arrives, OnWorldReceived,
		/// and then come from the host: never reset here, where it could come after the host's message.)
		/// </summary>
		internal static void Reset()
		{
			worldRegrow = -1;
			if (!Raft_Network.IsHost) return;
			Survivors.Reset();
			bool isNew = false;
			try { isNew = GameManager.IsInNewGame; } catch { }
			bool newWorld = isNew;
			MonsterDifficulty.Reset(newWorld);
			BuildCost.Reset(newWorld);
			BuildCostRefund.Reset(BuildCost.Current);
			AnimalLooks.Reset(newWorld);
			if (newWorld) Broadcast();
		}

		/// <summary>A "@key=value" line of the world file; false if it isn't one of the rules.</summary>
		internal static bool ReadLine(string key, string value)
		{
			if (key.Equals("regrow", StringComparison.OrdinalIgnoreCase))
			{
				int d;
				if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out d)) worldRegrow = Mathf.Max(0, d);
				return true;
			}
			return MonsterDifficulty.ReadLine(key, value) || BuildCost.ReadLine(key, value) || BuildCostRefund.ReadLine(key, value) || Survivors.ReadLine(key, value) || AnimalLooks.ReadLine(key, value);
		}

		internal static IEnumerable<string> WriteLines()
		{
			IEnumerable<string> lines = MonsterDifficulty.WriteLines().Concat(BuildCost.WriteLines()).Concat(BuildCostRefund.WriteLines()).Concat(Survivors.WriteLines()).Concat(AnimalLooks.WriteLines());
			return worldRegrow >= 0 ? lines.Concat(new[] { "@regrow=" + worldRegrow.ToString(CultureInfo.InvariantCulture) }) : lines;
		}

		/// <summary>
		/// The world's days until things come back (ROADMAP AU20): taken from the host's spawnpool.txt when the world is first
		/// played and kept with the world from then on - a later host with another setting refilled every chest a group had
		/// emptied for good ("never"). -1 = not known yet.
		/// </summary>
		static int worldRegrow = -1;

		/// <summary>Host, after the world file was read: a world without its own regrow days takes this PC's now.</summary>
		internal static void OnWorldRead()
		{
			if (!Raft_Network.IsHost) return;
			if (worldRegrow < 0) worldRegrow = CustomIslandSpawner.RegrowDays;
			else if (worldRegrow != CustomIslandSpawner.RegrowDays) Debug.Log("[CUSTOM ISLANDS] [world rules] This world keeps its regrow days (" + worldRegrow + "); this PC's spawnpool.txt says " + CustomIslandSpawner.RegrowDays + " (for new worlds)");
			Broadcast();
		}

		/// <summary>Host: changes the world's regrow days (the RegrowDays command).</summary>
		public static void SetRegrow(int days)
		{
			worldRegrow = Mathf.Max(0, days);
			Broadcast();
			IslandWorldState.Save();
		}

		/// <summary>True when the world file is needed for the rules alone.</summary>
		internal static bool HasState { get { return Survivors.HasState || MonsterDifficulty.HasState || BuildCost.HasState || BuildCostRefund.HasState || AnimalLooks.HasState || worldRegrow >= 0 && worldRegrow != CustomIslandSpawner.RegrowDays; } }

		public static string Describe() { return "monsters " + MonsterDifficulty.Describe(MonsterDifficulty.Current) + ", build cost " + BuildCost.Describe(BuildCost.Current); }

		/// <summary>Host -> clients: the world's rules, and the host's spawnpool.txt settings every player must share (Data);
		/// the rescued survivors (Names, RF3).</summary>
		internal static IslandNetMessage Message()
		{
			return new IslandNetMessage { Kind = IslandNetMessage.WorldRules, Index = MonsterDifficulty.Current, Count = BuildCost.Current, Data = HostSettingsData() + AnimalLooks.Data(), Name = BuildCostRefund.Encode(), Names = Survivors.Encode() };
		}

		internal static void Broadcast()
		{
			if (Raft_Network.IsHost) IslandNetwork.SendWorldRules(Message());
		}

		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			MonsterDifficulty.FromHost(msg.Index);
			BuildCost.FromHost(msg.Count);
			// (the cost each block was placed at, AU33; a host with an older version: its blocks at its cost)
			if (msg.Name != null) BuildCostRefund.Decode(msg.Name); else BuildCostRefund.OnCostRead(msg.Count);
			HostSettingsFrom(msg.Data);
			Survivors.FromHost(msg.Names);
		}

		/// <summary>Client: a host's world arrived; Raft's own rules until that host's come.</summary>
		internal static void OnWorldReceived()
		{
			MonsterDifficulty.OnWorldReceived();
			BuildCost.OnWorldReceived();
			BuildCostRefund.Reset(0);
			AnimalLooks.OnWorldReceived();
			Survivors.Reset();
			hostKnown = false;
			worldRegrow = -1;
		}

		#region The host's settings (spawnpool.txt)

		// Settings of the host's spawnpool.txt that change what every player sees, so a player who joins uses the host's,
		// not their own file's: whether custom islands show on the Receiver, how far away an island unloads, and (only
		// shown: the host alone decides what grows back, IslandObjectState) the regrow days. The rest of the file only
		// matters on the host, whose spawner alone places islands.
		static bool hostKnown, hostReceiver = true;
		static float hostUnload = 800f;
		static int hostRegrow = 3;

		static bool UseHost { get { return hostKnown && !Raft_Network.IsHost && LoadSceneManager.IsGameSceneLoaded; } }

		/// <summary>Whether custom islands show on the Receiver in this world: the host's setting.</summary>
		public static bool ShowOnReceiver { get { return UseHost ? hostReceiver : CustomIslandSpawner.ShowOnReceiver; } }

		/// <summary>How far the Receiver shows custom islands (m, 0 = all): the host's setting.</summary>
		public static float ReceiverDistance { get { return UseHost ? hostReceiverDistance : CustomIslandSpawner.ReceiverDistance; } }
		static float hostReceiverDistance = 2000f;

		/// <summary>How far away a custom island unloads in this world: the host's setting.</summary>
		public static float UnloadDistance { get { return UseHost ? hostUnload : CustomIslandSpawner.UnloadDistance; } }

		/// <summary>The world's days until things come back (the host's; islands may have their own rule, IslandRules).</summary>
		public static int RegrowDays { get { return UseHost ? hostRegrow : worldRegrow >= 0 ? worldRegrow : CustomIslandSpawner.RegrowDays; } }

		/// <summary>Whether this machine uses a host's settings (a client in a game whose host sent them).</summary>
		public static bool HostSettingsFromHost { get { return UseHost; } }

		static string HostSettingsData()
		{
			// (the distances to two decimals: whole metres gave player 2 813 m where the host had 812.5 - IM25)
			return "receiver=" + (CustomIslandSpawner.ShowOnReceiver ? 1 : 0) + ";unload=" + CustomIslandSpawner.UnloadDistance.ToString("0.##", CultureInfo.InvariantCulture) +
				";regrow=" + RegrowDays + ";rdist=" + CustomIslandSpawner.ReceiverDistance.ToString("0.##", CultureInfo.InvariantCulture);
		}

		static void HostSettingsFrom(string data)
		{
			if (string.IsNullOrEmpty(data)) return; // (a host with an older version: this player's own)
			foreach (string part in data.Split(';'))
			{
				int eq = part.IndexOf('=');
				if (eq < 1) continue;
				string key = part.Substring(0, eq), value = part.Substring(eq + 1);
				float v;
				if (!CustomIslandSpawner.ParseNumber(value, out v)) continue;
				if (key == "receiver") hostReceiver = v != 0f;
				else if (key == "unload") hostUnload = Mathf.Max(300f, v);
				else if (key == "regrow") hostRegrow = Mathf.Max(0, Mathf.RoundToInt(v));
				else if (key == "rdist") hostReceiverDistance = Mathf.Max(0f, v);
				else AnimalLooks.FromHost(key, v); // (rare=, caught=: the rare animal looks)
			}
			hostKnown = true;
			Debug.Log("[CUSTOM ISLANDS] [world rules] The host's settings: " + HostSettingsDescribe());
		}

		public static string HostSettingsDescribe()
		{
			return "custom islands on the Receiver " + (ShowOnReceiver ? "shown" : "hidden") + ", unloaded beyond " + UnloadDistance.ToString("F0", CultureInfo.InvariantCulture) +
				" m, things come back after " + RegrowDays + " day(s)" + (UseHost ? " (the host's)" : "");
		}

		static string lastPoolData;

		/// <summary>Host: spawnpool.txt was read: if a shared setting changed while a game is open, every player gets it.</summary>
		internal static void OnPoolChanged()
		{
			string data = HostSettingsData();
			if (data == lastPoolData) return;
			bool first = lastPoolData == null;
			lastPoolData = data;
			if (!first && Raft_Network.IsHost && LoadSceneManager.IsGameSceneLoaded) Broadcast();
		}

		#endregion
	}

	/// <summary>
	/// How many more materials everything in Raft's build menu (the hammer's blocks) costs in a world: 0-100 %, each amount
	/// rounded to the nearest (never below Raft's own). Raft keeps one cost list per buildable item
	/// (ItemInstance_Recipe.NewCost); placing a block takes it from
	/// the builder's inventory on the builder's machine (BlockCreator.CreateBlock), removing one gives it back
	/// (RemovePlaceables.ReturnItemsFromBlock), and the hammer's repair and reinforce use it too - so those follow.
	/// The amounts are set from Raft's own numbers as first seen (never on top of an earlier change) while a world is
	/// open on this machine, and put back as soon as it isn't (BuildCostKeeper), so every world has its own cost, leaving
	/// and joining again changes nothing, and the main menu, the editor and the next world start from Raft's.
	/// </summary>
	public static class BuildCost
	{
		public const int Max = 100, Step = 5;

		/// <summary>The current world's percent (clients get the host's).</summary>
		public static int Current;
		/// <summary>Chosen in the New Game box for the world being created (null = the last choice, Default).</summary>
		public static int? Pending;
		/// <summary>The percent on Raft's numbers now (0: Raft's own).</summary>
		public static int Applied { get; private set; }

		static readonly Dictionary<CostMultiple, int> originals = new Dictionary<CostMultiple, int>();
		static List<Item_Base> items;
		static GameObject keeper;

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [build cost] " + msg); }

		public static int Clamp(int percent) { return Mathf.Clamp(percent, 0, Max); }

		/// <summary>An amount at this percent, rounded to the nearest (a half up) and never below Raft's own: 1 at 5-45 % stays 1,
		/// 1 at 50 % is 2, 2 is 3, 3 is 5. (Rounded up before, "+5 %" doubled every 1-item cost - AU41.)</summary>
		public static int Cost(int amount, int percent)
		{
			if (percent <= 0 || amount <= 0) return amount;
			return Mathf.Max(amount, (amount * (100 + percent) + 50) / 100);
		}

		public static string Describe(int percent) { return percent <= 0 ? "Raft's own" : "+" + percent + "%"; }

		/// <summary>What the percent does, shown under the slider.</summary>
		public static string DescriptionFor(int percent)
		{
			if (percent <= 0) return "Building costs what it does in Raft.";
			return "Everything in the build menu costs " + percent + "% more materials, rounded: 1 plank becomes " + Cost(1, percent) + ", 2 become " + Cost(2, percent) +
				", 3 become " + Cost(3, percent) + ". Removing a block gives back half of what it cost when it was placed.";
		}

		/// <summary>The "?" next to the slider.</summary>
		public static string HelpText
		{
			get
			{
				return "How many more materials everything in the build menu costs in this world: the hammer's foundations, floors, walls, " +
					"roofs, stairs, pillars and the rest. 0% is Raft's own cost, 100% twice as much.\n" +
					"Amounts are rounded to the nearest whole number (a half up), never below Raft's own: at 50% one plank becomes two and two planks become three; below 50% one plank stays one.\n" +
					"Removing a block with the hammer gives back half of what it cost when it was placed, as in Raft (a change later doesn't change it), and repairing and reinforcing blocks cost more too. " +
					"What you make in the crafting menu (Tab) costs the same as in Raft.\n" +
					"Every player in the world pays the same: players who join get the host's setting. " +
					"The host can change it later with the console command BuildCost (F10).";
			}
		}

		#region The last choice and the world file

		/// <summary>The percent last chosen in the New Game box (0 until one was).</summary>
		public static int Default
		{
			get { int p; return int.TryParse(WorldRules.ReadDefault("buildcost") ?? "", NumberStyles.Integer, CultureInfo.InvariantCulture, out p) ? Clamp(p) : 0; }
		}

		public static void SaveDefault(int percent) { WorldRules.SaveDefault("buildcost", Clamp(percent).ToString(CultureInfo.InvariantCulture)); }

		internal static void Reset(bool newWorld)
		{
			if (newWorld)
			{
				Current = Clamp(Pending ?? Default);
				Pending = null;
				Log("New world '" + SaveAndLoad.CurrentGameFileName + "': build cost " + Describe(Current));
				if (Current > 0) DynamicIslands.Notify("Building in this world costs " + Describe(Current));
			}
			else Current = 0;
			Refresh();
		}

		internal static bool ReadLine(string key, string value)
		{
			if (!key.Equals("buildcost", StringComparison.OrdinalIgnoreCase)) return false;
			int p;
			if (!int.TryParse(value.Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out p)) { Debug.LogWarning("[CUSTOM ISLANDS] Unknown build cost '" + value + "' in the world file: Raft's own"); p = 0; }
			Current = Clamp(p);
			BuildCostRefund.OnCostRead(Current);
			Log("This world's build cost: " + Describe(Current));
			Refresh();
			return true;
		}

		internal static IEnumerable<string> WriteLines()
		{
			if (Current > 0) yield return "@buildcost=" + Current.ToString(CultureInfo.InvariantCulture);
		}

		internal static bool HasState { get { return Current > 0; } }

		/// <summary>Host: changes the current world's build cost (the BuildCost command, tests) and tells everyone.</summary>
		public static void Set(int percent)
		{
			Current = Clamp(percent);
			Log("Build cost in this world: " + Describe(Current));
			Refresh();
			WorldRules.Broadcast();
			// (saved with the world at once, like the other settings: the copy players keep for host swap follows)
			IslandWorldState.Save();
		}

		internal static void FromHost(int percent)
		{
			int before = Current;
			Current = Clamp(percent);
			Log("The host's build cost: " + Describe(Current));
			Refresh();
			if (Current != before && Current > 0) DynamicIslands.Notify("Building in this world costs " + Describe(Current));
		}

		internal static void OnWorldReceived()
		{
			Current = 0;
			Refresh();
		}

		#endregion

		#region Raft's numbers

		/// <summary>Raft's build menu's items (gathered once a world's build menu is there).</summary>
		public static IList<Item_Base> Items { get { return items != null ? (IList<Item_Base>)items : new Item_Base[0]; } }

		/// <summary>
		/// The items of Raft's build menu: those of its buttons (main categories, sub categories, blocks) and what is built
		/// through them (their wood, thatch and tier 3 upgrades, mirrored versions).
		/// </summary>
		internal static List<Item_Base> FindBuildMenuItems()
		{
			var found = new List<Item_Base>();
			var seen = new HashSet<Item_Base>();
			Action<Item_Base> add = null;
			add = i =>
			{
				if (i == null || !seen.Add(i)) return;
				found.Add(i);
				ItemInstance_Buildable b = i.settings_buildable;
				if (b == null) return;
				if (b.upgrades != null) { add(b.upgrades.woodVersion); add(b.upgrades.thatchVersion); add(b.upgrades.tier3Version); }
				add(b.mirroredVersion);
			};
			foreach (BuildMenuItem_SelectMainCategory x in Resources.FindObjectsOfTypeAll<BuildMenuItem_SelectMainCategory>()) if (x.gameObject.scene.IsValid()) add(x.buildableItem);
			foreach (BuildMenuItem_SelectSubCategory x in Resources.FindObjectsOfTypeAll<BuildMenuItem_SelectSubCategory>()) if (x.gameObject.scene.IsValid()) add(x.buildableItem);
			foreach (BuildMenuItem_SelectBlock x in Resources.FindObjectsOfTypeAll<BuildMenuItem_SelectBlock>()) if (x.gameObject.scene.IsValid()) add(x.buildableItem);
			return found;
		}

		/// <summary>The cost entries (one per kind of material) of these items.</summary>
		public static List<CostMultiple> Entries(IEnumerable<Item_Base> of)
		{
			return of.Where(i => i != null && i.settings_recipe != null && i.settings_recipe.NewCost != null)
				.SelectMany(i => i.settings_recipe.NewCost).Where(c => c != null).Distinct().ToList();
		}

		/// <summary>Raft's own amount of a cost entry (as first seen by the mod).</summary>
		public static int OriginalOf(CostMultiple c)
		{
			int o;
			return c != null && originals.TryGetValue(c, out o) ? o : c != null ? c.amount : 0;
		}

		/// <summary>Sets cost entries to this percent of Raft's own amounts (remembered the first time: never on top of an earlier change).</summary>
		public static void ApplyTo(IEnumerable<CostMultiple> entries, int percent)
		{
			foreach (CostMultiple c in entries)
			{
				if (c == null) continue;
				int orig;
				if (!originals.TryGetValue(c, out orig)) { orig = c.amount; originals[c] = orig; }
				c.amount = Cost(orig, percent);
			}
		}

		/// <summary>Every changed amount back to Raft's own.</summary>
		public static void RestoreAll()
		{
			foreach (KeyValuePair<CostMultiple, int> kv in originals) if (kv.Key != null) kv.Key.amount = kv.Value;
			if (Applied != 0) Log("Raft's own build costs back (" + originals.Count + " amounts)");
			Applied = 0;
		}

		/// <summary>
		/// Puts the current world's percent on Raft's numbers while a world is open here, Raft's own otherwise. Called when
		/// the percent changes, and a few times a second by the keeper (worlds loaded and left, the build menu appearing).
		/// </summary>
		public static void Refresh()
		{
			EnsureKeeper();
			bool inGame = false;
			try { inGame = LoadSceneManager.IsGameSceneLoaded; } catch { }
			if (!inGame) { searchWait = 0f; nextSearch = 0f; }
			int want = inGame ? Current : 0;
			if (want == Applied) return;
			if (want == 0) { RestoreAll(); return; }
			if (items == null || items.Count == 0)
			{
				if (Time.unscaledTime < nextSearch) return;
				items = FindBuildMenuItems();
				if (items.Count == 0)
				{
					// (the build menu isn't there yet: the keeper tries again, less often the longer it isn't - every object
					// was searched 4 times a second for as long as the world was open)
					searchWait = Mathf.Min(Mathf.Max(searchWait * 2f, 0.5f), 30f);
					nextSearch = Time.unscaledTime + searchWait;
					return;
				}
				searchWait = 0f;
			}
			List<CostMultiple> entries = Entries(items);
			ApplyTo(entries, want);
			Applied = want;
			Log("Build menu costs " + Describe(want) + ": " + items.Count + " items, " + entries.Count + " amounts (e.g. " + Example() + ")");
		}

		/// <summary>When the build menu is looked for again, and how long the last wait was (it doubles, up to 30 s).</summary>
		static float nextSearch, searchWait;

		static string Example()
		{
			Item_Base i = items.FirstOrDefault(x => x != null && x.settings_recipe != null && x.settings_recipe.NewCost != null && x.settings_recipe.NewCost.Length > 0);
			if (i == null) return "-";
			return i.UniqueName + " " + string.Join(" + ", i.settings_recipe.NewCost.Where(c => c != null && c.items != null && c.items.Length > 0)
				.Select(c => OriginalOf(c) + "->" + c.amount + " " + c.items[0].UniqueName).ToArray());
		}

		static void EnsureKeeper()
		{
			if (keeper != null) return;
			keeper = new GameObject("CustomIslands_BuildCost");
			UnityEngine.Object.DontDestroyOnLoad(keeper);
			keeper.AddComponent<BuildCostKeeper>();
		}

		#endregion

		[ConsoleCommand(name: "BuildCost", docs: "Build cost: BuildCost = this world's; BuildCost <0-100> = everything in the build menu costs that many % more, rounded (host), or at the main menu for the next new world")]
		public static void BuildCostCommand(string[] args)
		{
			string arg = args != null ? string.Join(" ", args).Trim().TrimEnd('%') : "";
			bool inGame = LoadSceneManager.IsGameSceneLoaded;
			if (arg.Length > 0)
			{
				int p;
				if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out p) || p < 0 || p > Max) { DynamicIslands.Notify("BuildCost: use a number from 0 to " + Max + " (% more)", true); return; }
				if (!inGame)
				{
					NewWorldRulesBox.BuildPercent = p;
					SaveDefault(p);
					DynamicIslands.Notify("Building in the next new world costs " + Describe(p));
					return;
				}
				if (!Raft_Network.IsHost) { DynamicIslands.Notify("Only the host can change the build cost", true); return; }
				Set(p);
				DynamicIslands.Notify("Building in this world costs " + Describe(Current));
				return;
			}
			if (inGame) Log("Build cost in this world: " + Describe(Current) + " (on Raft's numbers now: " + Describe(Applied) + ")");
			else Log("Build cost in the next new world: " + Describe(NewWorldRulesBox.BuildPercent));
		}
	}

	/// <summary>Keeps Raft's cost numbers right for where this machine is: a world's build cost in it, Raft's own outside.</summary>
	class BuildCostKeeper : MonoBehaviour
	{
		float next;

		void Update()
		{
			if (Time.unscaledTime < next) return;
			next = Time.unscaledTime + 0.25f;
			try { BuildCost.Refresh(); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [build cost] " + e.Message); next = Time.unscaledTime + 5f; }
		}
	}

	/// <summary>
	/// The world rules in Raft's New Game box, right under Raft's game mode tabs: the monster difficulty (a slider with the
	/// five levels, what the level does below it, a "?" with all of them) and the build cost (0-100 % in steps of 5, its "?").
	/// The box grows to make room (everything under the tabs moves down, the box moves up by half as much, and more when it
	/// would reach below the screen). Runs after the other additions to the box (the plan, the randomizer), so they move
	/// down too; ones made later, placed from the box's bottom as those are, land below the panel since the box grew.
	/// </summary>
	[HarmonyPatch(typeof(NewGameBox), "Open")]
	[HarmonyPriority(Priority.Low)]
	static class NewWorldRulesBox
	{
		public const string PanelName = "CustomIslands_WorldRules";
		public const float PanelHeight = 248f;

		static int? monsterChoice, buildChoice, looksChoice;
		/// <summary>The box shows the remembered choices again (a saved world was loaded after the box was changed).</summary>
		internal static void Forget() { monsterChoice = buildChoice = looksChoice = null; }
		static UIKit.SliderRow monsterSlider, buildSlider;
		static Text monsterDetail, buildDetail, looksDetail;
		static readonly List<Button> looksButtons = new List<Button>();
		static readonly List<Text> ticks = new List<Text>();

		/// <summary>The monster level shown in the box (the last choice until it is moved).</summary>
		public static int MonsterLevel
		{
			get { if (!monsterChoice.HasValue) monsterChoice = MonsterDifficulty.Default; return monsterChoice.Value; }
			set { monsterChoice = MonsterDifficulty.Clamp(value); Refresh(); }
		}

		/// <summary>The build cost shown in the box, in % (the last choice until it is moved).</summary>
		public static int BuildPercent
		{
			get { if (!buildChoice.HasValue) buildChoice = BuildCost.Default; return buildChoice.Value; }
			set { buildChoice = BuildCost.Clamp(value); Refresh(); }
		}

		/// <summary>The rare animal looks level shown (the last choice until another is pressed).</summary>
		public static int LooksLevel
		{
			get { if (!looksChoice.HasValue) looksChoice = AnimalLooks.Default; return looksChoice.Value; }
			set { looksChoice = AnimalLooks.Clamp(value); Refresh(); }
		}

		/// <summary>The rare animal looks buttons (Normal, More, Lots) and their line (tests).</summary>
		public static Button LooksButton(int level) { return level >= 0 && level < looksButtons.Count ? looksButtons[level] : null; }
		public static string LooksText { get { return looksDetail != null ? looksDetail.text : null; } }

		/// <summary>The sliders (tests move them as a player would); the build cost slider counts steps of 5 %.</summary>
		public static Slider MonsterSlider { get { return monsterSlider != null ? monsterSlider.Slider : null; } }
		public static Slider BuildSlider { get { return buildSlider != null ? buildSlider.Slider : null; } }
		public static string MonsterText { get { return monsterDetail != null ? monsterDetail.text : null; } }
		public static string BuildText { get { return buildDetail != null ? buildDetail.text : null; } }

		[HarmonyPriority(Priority.Low)]
		static void Postfix(NewGameBox __instance)
		{
			try { Ensure(__instance); }
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Adding the world rules to the New Game box: " + e); }
		}

		static void Ensure(NewGameBox box)
		{
			var rt = (RectTransform)box.transform;
			// (the sliders are in the World settings window now; the box only follows the game mode for their texts)
			if (rt.GetComponent<NewWorldRulesWatch>() == null) rt.gameObject.AddComponent<NewWorldRulesWatch>();
			Refresh();
		}

		internal static IEnumerable<RectTransform> Children(Transform parent)
		{
			foreach (Transform t in parent) { var r = t as RectTransform; if (r != null) yield return r; }
		}

		/// <summary>A corner in the world: 0 bottom left, 1 top left, 2 top right, 3 bottom right.</summary>
		static Vector3 Corner(RectTransform r, int i)
		{
			var c = new Vector3[4];
			r.GetWorldCorners(c);
			return c[i];
		}

		/// <summary>Makes a rectangle this tall (in its own units) with its top left corner here (in the world).</summary>
		static void SetHeight(RectTransform r, Vector3 topLeft, float height)
		{
			float missing = height - r.rect.height;
			if (Mathf.Abs(missing) > 0.5f) r.sizeDelta += new Vector2(0f, missing);
			r.position += topLeft - Corner(r, 1);
		}

		/// <summary>A child's rectangle in the box's own coordinates.</summary>
		internal static Rect LocalRect(RectTransform box, RectTransform r)
		{
			var c = new Vector3[4];
			r.GetWorldCorners(c);
			Vector3 a = box.InverseTransformPoint(c[0]), b = box.InverseTransformPoint(c[2]);
			return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
		}

		/// <summary>The line under Raft's game mode tabs and their description (the panel goes right below it), and the tabs' divider's ends, in the box's coordinates.</summary>
		static bool FindLine(RectTransform box, RectTransform background, out float cut, out float left, out float right)
		{
			cut = float.MaxValue; left = float.MaxValue; right = float.MinValue;
			foreach (RectTransform c in Children(box))
			{
				if (!(c.name.EndsWith("_ModeDescription") || c.name == "TabsDivider")) continue;
				Rect r = LocalRect(box, c);
				cut = Mathf.Min(cut, r.yMin);
				if (c.name == "TabsDivider") { left = r.xMin; right = r.xMax; }
			}
			if (cut == float.MaxValue || background == null) return false;
			if (left == float.MaxValue) { Rect bg = LocalRect(box, background); left = bg.xMin + 14f; right = bg.xMax - 14f; }
			return true;
		}

		/// <summary>
		/// "World rules" in the World settings window (WorldSettingsWindow): the monster difficulty and the build cost sliders,
		/// each with its text. (They sat in the New Game box under Raft's game modes before, which made it taller; the box
		/// is Raft's own size again.)
		/// </summary>
		internal static RectTransform BuildInto(Transform parent)
		{
			RectTransform panel = UIKit.Rect(PanelName, parent);
			UIKit.Size(panel.gameObject, -1, PanelHeight);
			UIKit.Background(panel.gameObject, UIKit.GroupBg, 6);
			UIKit.Vertical(panel.gameObject, 2f, new RectOffset(12, 12, 6, 6));

			// The monster difficulty
			monsterSlider = UIKit.Slider(panel, "MONSTER DIFFICULTY", 0, MonsterDifficulty.Names.Length - 1, MonsterLevel, v => MonsterFormat(Mathf.RoundToInt(v)),
				v => { monsterChoice = MonsterDifficulty.Clamp(Mathf.RoundToInt(v)); Refresh(); },
				"How tough the monsters of the new world are (their health and the damage they deal)", true, MonsterDifficulty.HelpText);
			Heading(monsterSlider);
			// (the five names under the slider's notches: its handle moves 7 px in from each end)
			RectTransform row = UIKit.Rect("Ticks", panel);
			UIKit.Size(row.gameObject, -1, 13);
			ticks.Clear();
			int n = MonsterDifficulty.Names.Length;
			for (int i = 0; i < n; i++)
			{
				float at = i / (float)(n - 1);
				Text t = UIKit.Label(row, MonsterDifficulty.Names[i], 11, UIKit.TextMuted, i == 0 ? TextAnchor.MiddleLeft : i == n - 1 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter, FontStyle.Normal, "Tick_" + MonsterDifficulty.Names[i]);
				RectTransform r = t.rectTransform;
				r.anchorMin = r.anchorMax = new Vector2(at, 0.5f);
				r.pivot = new Vector2(at, 0.5f);
				r.sizeDelta = new Vector2(90f, 13f);
				r.anchoredPosition = new Vector2(7f - 14f * at, 0f);
				t.raycastTarget = false;
				ticks.Add(t);
			}
			// (two lines: the level's text, and the Peaceful / Creative note after it)
			monsterDetail = Detail(panel, "MonsterDetail", 32);

			// The build cost, in steps of 5 %
			RectTransform gap = UIKit.Rect("Gap", panel);
			UIKit.Size(gap.gameObject, -1, 4);
			buildSlider = UIKit.Slider(panel, "BUILD COST", 0, BuildCost.Max / BuildCost.Step, BuildPercent / BuildCost.Step, v => BuildFormat(Mathf.RoundToInt(v) * BuildCost.Step),
				v => { buildChoice = BuildCost.Clamp(Mathf.RoundToInt(v) * BuildCost.Step); Refresh(); },
				"How many more materials everything in the build menu costs in the new world", true, BuildCost.HelpText);
			Heading(buildSlider);
			buildDetail = Detail(panel, "BuildDetail", 44); // (three lines in the window's column)

			// Rare animal looks: three buttons (Normal, More, Lots) and their line
			RectTransform lrow = UIKit.Row(panel, 24f, 4f, "RareLooks");
			Text lt = UIKit.Label(lrow, "RARE ANIMAL LOOKS", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold, "Label");
			UIKit.Heading(lt, 13);
			UIKit.Size(lt.gameObject, 170);
			looksButtons.Clear();
			for (int i = 0; i < AnimalLooks.Names.Length; i++)
			{
				int level = i;
				Button b = UIKit.Button(lrow, AnimalLooks.Names[i], () => { looksChoice = level; Refresh(); }, AnimalLooks.HelpText, -1, 24f, 12);
				b.name = "RareLooks_" + AnimalLooks.Names[i];
				looksButtons.Add(b);
			}
			looksDetail = Detail(panel, "RareLooksDetail", 30);

			Refresh();
			return panel;
		}

		static void Heading(UIKit.SliderRow row)
		{
			// (a title like the plan's and the randomizer's)
			Transform title = row.Slider.transform.parent.Find("Top/Label");
			if (title != null) UIKit.Heading(title.GetComponent<Text>(), 13);
		}

		static Text Detail(Transform panel, string name, float height)
		{
			Text t = UIKit.Label(panel, "", 12, UIKit.TextColor, TextAnchor.UpperLeft, FontStyle.Italic, name);
			UIKit.Size(t.gameObject, -1, height);
			t.horizontalOverflow = HorizontalWrapMode.Wrap;
			t.verticalOverflow = VerticalWrapMode.Truncate;
			return t;
		}

		static string MonsterFormat(int level) { return MonsterDifficulty.Name(level).ToUpperInvariant() + "   " + MonsterDifficulty.FactorText(level); }
		static string BuildFormat(int percent) { return percent <= 0 ? "RAFT'S OWN" : "+" + percent + "%"; }

		/// <summary>
		/// What the game mode tab does with the monster level, after its text - as Raft's own modes are (AU61, their values
		/// read from Raft's game files): in Peaceful the monsters are tame and players' hits count as usual, in Creative
		/// players take no damage, Easy and Hard change the monsters' hits (and Hard players' hits on them, x0.8) as well.
		/// </summary>
		static string ModeNote(GameMode mode)
		{
			switch (mode)
			{
				case GameMode.Peaceful: return " In Peaceful monsters leave you alone, so only their health changes.";
				case GameMode.Creative: return " In Creative nothing can hurt you, so only their health changes.";
				case GameMode.Easy: return " On top of Easy: their hits hurt you less.";
				case GameMode.Hardcore: return " On top of Hard: their hits hurt more and yours do less.";
				default: return "";
			}
		}

		internal static void Refresh()
		{
			if (monsterSlider != null && monsterSlider.Slider != null)
			{
				int l = MonsterLevel;
				if (Mathf.RoundToInt(monsterSlider.Slider.value) != l) monsterSlider.Slider.SetValueWithoutNotify(l);
				monsterSlider.Value.text = MonsterFormat(l);
				monsterDetail.text = MonsterDifficulty.Descriptions[l] + (l == MonsterDifficulty.Normal ? "" : ModeNote(GameManager.GameMode));
				// (the chosen one golden; Raft's body font is bold already)
				for (int i = 0; i < ticks.Count; i++)
					if (ticks[i] != null) ticks[i].color = i == l ? UIKit.Accent : UIKit.TextMuted;
			}
			if (buildSlider != null && buildSlider.Slider != null)
			{
				int p = BuildPercent, step = Mathf.RoundToInt(p / (float)BuildCost.Step);
				if (Mathf.RoundToInt(buildSlider.Slider.value) != step) buildSlider.Slider.SetValueWithoutNotify(step);
				buildSlider.Value.text = BuildFormat(p);
				buildDetail.text = BuildCost.DescriptionFor(p) + (p > 0 && GameManager.GameMode == GameMode.Creative ? " (Creative builds for free anyway.)" : "");
			}
			if (looksDetail != null)
			{
				int l = LooksLevel;
				for (int i = 0; i < looksButtons.Count; i++) if (looksButtons[i] != null) UIKit.SetActive(looksButtons[i], i == l);
				looksDetail.text = AnimalLooks.DescriptionFor(l);
			}
			WorldSettingsWindow.Show(); // (its summary and the box's button follow)
		}
	}

	/// <summary>On the New Game box: the texts follow the game mode tab chosen (Peaceful and Creative add a note).</summary>
	class NewWorldRulesWatch : MonoBehaviour
	{
		GameMode shownMode = (GameMode)(-1);

		void LateUpdate()
		{
			if (GameManager.GameMode != shownMode) { shownMode = GameManager.GameMode; NewWorldRulesBox.Refresh(); }
		}
	}

	/// <summary>Create pressed: the new world gets the rules shown in the box, and they are kept as the next defaults.</summary>
	[HarmonyPatch(typeof(NewGameBox), "Button_CreateNewGame")]
	static class NewWorldRulesChoice
	{
		static void Prefix()
		{
			try
			{
				int l = NewWorldRulesBox.MonsterLevel, p = NewWorldRulesBox.BuildPercent;
				MonsterDifficulty.Pending = l;
				BuildCost.Pending = p;
				MonsterDifficulty.SaveDefault(l);
				BuildCost.SaveDefault(p);
				AnimalLooks.Pending = NewWorldRulesBox.LooksLevel;
				AnimalLooks.SaveDefault(NewWorldRulesBox.LooksLevel);
				Debug.Log("[CUSTOM ISLANDS] Creating a world with the monster difficulty " + MonsterDifficulty.Describe(l) + " and the build cost " + BuildCost.Describe(p));
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] New world rules: " + e); }
		}
	}
}
