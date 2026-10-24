using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A world's extra options, ticked in the New Game box's World settings window (WorldSettingsWindow) and the same for
	/// every player in the world:
	///   - blueprints: Raft's blueprints on its story islands are found on other islands than usual (ScrambledBlueprints);
	///   - storyorder: Raft's story islands come in another order (StoryOrder);
	///   - ghostrafts: abandoned rafts drift by while sailing, some large and guarded (GhostRafts);
	///   - privatestorage: a storage opens only for the player who built it (PrivateStorage);
	///   - longvoyage: random custom islands come half as often and further apart (WorldIslands.NewTarget, CustomIslandSpawner);
	///   - ironraft: the raft's blocks take half damage from the shark (IronRaft);
	///   - sharedxp: players near a kill get 60 % of its EXP too (LevelSystem.ShareKill);
	///   - nightdanger: monsters tougher and the shark keener at night, both calmer by day (NightDanger);
	///   - dailyquest: a small task each day, a small reward when done before dark (DailyQuest);
	///   - rogueshark: now and then a second, rust-red shark that stays until killed (RogueShark);
	///   - barrels: now and then a silver or golden barrel in the sea, with a little extra loot (GoldenBarrels);
	///   - stormdays: now and then a day of rough sea and rain, the shark keener (StormDays);
	///   - traderraft: very rarely a merchant's raft comes by: basic resources for seeds, trees or a blueprint (TraderRaft);
	///   - ghostship: on about one night in four a glowing ghost ship comes by, its crew ghosts; it sinks at dawn (GhostShip);
	///   - upgrades: better versions of Raft's things to craft (ExtraUpgrades.Defs);
	///   - ufo: now and then a flying saucer crosses the sky, looks only (UfoSightings).
	///   - stormseas: storms much more often, rocking the raft harder; now and then one breaks a floor on its edge (StormSeas).
	///   - glowsea: on about one night in four the sea around the raft and the fish glow blue; fish bite faster (GlowSea).
	///   - lightning: now and then in a storm a bolt hits the sea near the raft, rarely its antenna; looks only (LightningStrikes).
	///   - whalebreach: now and then, by day, a whale breaches far from the raft, looks only (WhaleBreach);
	///   - fogbanks: now and then thick fog rolls in for a few hours, 45-75 m to see (FogBanks);
	///   - kraken: rarely a giant tentacle grabs a raft floor on the edge; hit it off, or it tears that floor (and maybe its neighbours) away (Kraken).
	/// Saved in the world file ("@options=", "@optionseed=" - the seed every shuffle of the world comes from), sent by the host
	/// to every player who joins and when they change (network kind 17: Data = "on=a,b;seed=n", Name = the private
	/// storages' builders). The last choice in the box is kept in world_rules.txt ("options=").
	/// </summary>
	public static class WorldOptions
	{
		public const string Blueprints = "blueprints", StoryOrder = "storyorder", GhostRafts = "ghostrafts", PrivateStorage = "privatestorage", LongVoyage = "longvoyage", IronRaft = "ironraft", SharedXp = "sharedxp", NightDanger = "nightdanger", DailyQuest = "dailyquest", RogueShark = "rogueshark", Barrels = "barrels", StormDays = "stormdays", TraderRaft = "traderraft", Upgrades = "upgrades", StormSeas = "stormseas", Ufo = "ufo", GlowSea = "glowsea", Lightning = "lightning", GhostShip = "ghostship", WhaleBreach = "whalebreach", Fog = "fogbanks", Kraken = "kraken";
		public static readonly string[] All = { Blueprints, StoryOrder, GhostRafts, PrivateStorage, LongVoyage, IronRaft, SharedXp, NightDanger, DailyQuest, RogueShark, Barrels, StormDays, TraderRaft, Upgrades, StormSeas, Ufo, GlowSea, Lightning, GhostShip, WhaleBreach, Fog, Kraken };
		/// <summary>
		/// Options no longer offered (the user, 2026-10-09): not in the New Game box, left out of the remembered choice, and in
		/// World settings only while a world still has them on (so the host can switch them off). A world that has one on keeps it.
		/// </summary>
		public static readonly string[] Retired = { Blueprints, StoryOrder };
		public static bool IsRetired(string option) { return Array.IndexOf(Retired, option) >= 0; }
		public static string[] Offered { get { return All.Where(o => !IsRetired(o)).ToArray(); } }
		public static readonly string[] Labels = { "Scrambled blueprints", "Story islands in a new order", "Ghost rafts", "Private storages", "Long voyage", "Iron raft", "Shared EXP", "Night is dangerous", "Daily quest", "Rogue shark", "Silver & golden barrels", "Storm days", "Trader raft", "Extra upgrades", "Storm seas", "UFO sightings", "Bioluminescent sea", "Lightning strikes", "Ghost ship", "Whale breach", "Fog banks", "Kraken arm" };
		public static readonly string[] Hints =
		{
			"The blueprints lying on Raft's story islands are found on other story islands than usual. What the story needs (the steering wheel, the engine and its fuel, the machete) is never moved: the story can always be finished.",
			"Raft's story islands come in another order: the Receiver's frequencies and the notes that lead on follow the new order. The ending stays last. For players who know the way by heart.",
			"Abandoned rafts drift by while sailing: small ones with a little loot and a note, and now and then a large one guarded by rats and screechers, with a better hoard.",
			"A storage opens only for the player who built it: each player keeps their own things. Storages built before the option was on (or by nobody) open for everyone.",
			"Random custom islands come half as often and lie further apart, so food, water and the raft matter more between stops. Raft's own islands, and quest and plan islands, keep their places.",
			"The raft's blocks take half damage from shark bites: fewer repairs. Taking pieces down with the hammer or the axe works as always, and shark bait is eaten as fast as ever.",
			"When a player defeats a monster, every other player within 50 m gets 60 % of its EXP too (less what their own hits on it already earned). Only defeated monsters count; playing alone nothing changes.",
			"After dark monsters are tougher (x1.3 health and damage on top of the monster difficulty) and the shark comes for the raft more often; by day both are calmer (x0.85, the shark less often).",
			"Each morning the crew gets a small task for the day: gather some planks, plastic, palm leaves or scrap, catch a few fish or defeat a few monsters. Done before dark, everyone gets a small reward (basic resources or food); not done, it runs out.",
			"Now and then a second shark comes beside the usual one: rust-red, and it doesn't leave on its own - only when it's killed. Once it is, no other comes for several days. Never before day 3. Now and then it is a giant: twice the size, much tougher.",
			"Now and then a barrel drifting in the sea is silver (about 1 in 40) or golden (about 1 in 150). Besides the usual loot, a silver one holds a few extra nails, rope and scrap; a golden one more of them, and sometimes a battery or bolts.",
			"On about one day in ten the sea is rough and it rains from morning to morning: the shark comes for the raft more often, and the rain waters the crops. Everyone is warned at nightfall the evening before. Never before day 3.",
			"Very rarely (about once in 7 km of sailing, never in the first 3 km) a trader's raft comes up ahead. Its stalls swap basic resources - planks, plastic, rope, scrap - for fruit and flower seeds, a tree seed, and now and then a blueprint. Each stall has a little stock, shared by all players.",
			"Better versions of Raft's things to craft, next to the thing they improve once it is learned: large battery, titanium grill, blast furnace, reinforced storage, wide net, large fuel and water tanks, greenhouse plot, turbo engine, large wind turbine, comfy bed, magnet hook, titanium rod, swift flippers, large air tank, rapid charger, fast recycler, bright lantern, telescope, titanium greatsword, spear and machete, long paddle, floodlight, steel pot, fast juicer, sturdy scarecrow, master hammer, longbow, bright head light, bright lamp and lumber axe.",
			"Storms come much more often (a calm spell is half as long, and half the time Raft's next weather is a storm) and rock the raft harder. Each storm has a 1 in 4 chance to break one raft floor on the edge or a corner of the raft - never one in the middle: what stood on it falls into the sea. A raft of 4 floors or fewer is never broken. The host's storms and floors are every player's.",
			"Now and then (about three times in a week of days, half of them at night) a flying saucer crosses the sky high above the raft, from horizon to horizon in about half a minute, in a new colour each time. At night it glows green or purple. Looks only: every player sees the same one.",
			"On about one night in four the sea around the raft glows: tiny blue lights twinkle on the waves and drift under them, a swimmer stirs up sparks, and the fish shine. On those nights fish bite twice as fast on a rod. Looks only otherwise; the same nights for every player.",
			"In a storm (Raft's rain or rough snow, Storm days and Storm seas too), now and then a bolt of lightning hits the sea 30-150 m from the raft: a jagged bolt, a flash that lights everything up, thunder. Some storms have none, a few have up to four. Very rarely it hits the raft's Receiver antenna (or wind turbine). Looks only: nothing is damaged, nothing burns, nobody is hurt.",
			"On about one night in four a ghost ship comes by soon after dark: a glowing wreck close beside the raft, near enough to swim or sail to. Its chests hold good loot, but its crew are ghosts - see-through rats and screechers that fight like the living. At dawn it sinks with whatever is still aboard. Players asleep when it would come: no ship that night. The same ship, ghosts and loot for every player.",
			"Now and then, by day (about once or twice a Raft day), a whale breaches far out at sea, 170-320 m from the raft: it rises, leaps half out of the water, falls back with a great splash and spray, and dives away. Looks only: it never comes near the raft or the players. Every player sees the same whale at the same place.",
			"Now and then (on about three days in five, by day or by night) thick fog rolls in over the sea for three to six hours, fading in and out. In it you see only 45-75 m: islands show up late, so the radar matters. While a storm blows the wind keeps the fog away. Looks only; the same for every player.",
			"Rarely (on about one day in eight from day 8, and with Storm seas in about one storm in four; never while the raft is near an island) a giant tentacle rises from the sea beside the raft and grabs a floor on its edge - never one in the middle. Hit it with a melee weapon or a spear, 10 hits within a minute (any players together), and it lets go and sinks. If not, it tears that floor away, and each floor right beside it with a 1 in 2 chance: what stood on them goes too. Rafts of 8 floors or fewer are never grabbed. Every player sees the same arm and the same floors go.",
		};

		/// <summary>
		/// The short text under each toggle in World settings: three lines of 11 pt at most (the user, 2026-10-20: the long
		/// ones ran out of the box). The whole Hints text shows on hover over the toggle.
		/// </summary>
		public static readonly string[] Shorts =
		{
			"Raft's story blueprints lie on other story islands than usual. What the story needs is never moved.",
			"Raft's story islands come in another order; the Receiver and the notes follow it. The ending stays last.",
			"Abandoned rafts drift by: small ones with a little loot and a note, now and then a large one guarded by rats and screechers.",
			"A storage opens only for the player who built it: each player keeps their own things.",
			"Random custom islands come half as often and lie further apart. Raft's own, quest and plan islands stay where they are.",
			"The raft's blocks take half damage from shark bites: fewer repairs.",
			"Players within 50 m of a defeated monster get 60 % of its EXP too.",
			"After dark monsters are tougher and the shark comes more often; by day both are calmer.",
			"Each morning the crew gets a small task. Done before dark, everyone gets a small reward.",
			"Now and then a second, rust-red shark comes and stays until it is killed. Sometimes it is a giant. Never before day 3.",
			"Now and then a drifting barrel is silver or golden, with extra nails, rope and scrap - sometimes a battery or bolts.",
			"On about one day in ten the sea is rough and it rains all day: the shark comes more often. Warned the evening before.",
			"Very rarely a trader's raft comes up ahead. Its stalls swap basic resources for seeds and now and then a blueprint.",
			"About 30 better versions of Raft's things to craft (large battery, turbo engine, comfy bed, longbow and more), next to the thing they improve.",
			"Storms come much more often and rock the raft harder. Now and then one breaks a floor on the raft's edge; rafts of 4 floors or fewer never.",
			"Now and then a flying saucer crosses the sky high above the raft, glowing at night. Looks only.",
			"On about one night in four the sea and the fish glow blue, and fish bite twice as fast on a rod.",
			"In storms, lightning now and then hits the sea near the raft, rarely its antenna. Looks only: nothing is damaged.",
			"On about one night in four a ghost ship comes by: good loot, guarded by ghost rats and screechers. It sinks at dawn.",
			"Now and then, by day, a whale breaches far out at sea. Looks only: it never comes near the raft.",
			"Now and then thick fog rolls in for a few hours: you see only 45-75 m, so the radar matters. Looks only.",
			"Rarely a giant tentacle grabs the raft's edge. Its other arms slam at players near them - step away. Hit it 10 times within a minute, or it tears that floor away - and maybe the ones beside it.",
		};

		/// <summary>The current world's options (clients get the host's).</summary>
		public static readonly HashSet<string> Current = new HashSet<string>();
		/// <summary>The seed every shuffle of this world comes from (the same on every machine).</summary>
		public static int Seed;
		/// <summary>Chosen in the New Game box for the world being created (null = the last choice).</summary>
		public static HashSet<string> Pending;
		/// <summary>Raised on every machine when the options (or the seed) change.</summary>
		public static event Action Changed;

		public static bool On(string option) { return Current.Contains(option); }

		static void Log(string msg) { Debug.Log("[CUSTOM ISLANDS] [options] " + msg); }

		/// <summary>
		/// A well-mixed number from a world's seed, a day (or index) and a salt, the same on every machine. Not System.Random
		/// seeded with a sum: the first value of nearby seeds is nearly the same, so day after day rolled alike.
		/// </summary>
		public static int Mix(int seed, int day, int salt)
		{
			unchecked
			{
				uint h = (uint)seed * 2654435761u ^ (uint)day * 2246822519u ^ (uint)salt * 3266489917u;
				h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
				return (int)h;
			}
		}

		/// <summary>Mix as a number in [0, 1).</summary>
		public static double Unit(int seed, int day, int salt) { return (uint)Mix(seed, day, salt) / 4294967296.0; }

		public static string Label(string option) { int i = Array.IndexOf(All, option); return i >= 0 ? Labels[i] : option; }

		/// <summary>"on=a,b;seed=n" (only known options, in their order).</summary>
		public static string Encode(IEnumerable<string> on, int seed)
		{
			return "on=" + string.Join(",", All.Where(o => on.Contains(o)).ToArray()) + ";seed=" + seed.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>The options of a list like "a,b" (unknown names left out).</summary>
		public static HashSet<string> Parse(string list)
		{
			var set = new HashSet<string>();
			foreach (string p in (list ?? "").Split(',', ' ', ';'))
			{
				string o = p.Trim().ToLowerInvariant();
				if (All.Contains(o)) set.Add(o);
			}
			return set;
		}

		public static string Describe(IEnumerable<string> on)
		{
			string[] l = All.Where(o => on.Contains(o)).Select(Label).ToArray();
			return l.Length == 0 ? "none" : string.Join(", ", l);
		}

		public static string Describe() { return Describe(Current) + " (seed " + Seed + ")"; }

		#region The last choice (world_rules.txt)

		public static HashSet<string> Defaults { get { var d = Parse(WorldRules.ReadDefault("options")); d.RemoveWhere(IsRetired); return d; } }

		public static void SaveDefaults(IEnumerable<string> on) { WorldRules.SaveDefault("options", string.Join(",", All.Where(o => on.Contains(o)).ToArray())); }

		#endregion

		#region The world file

		/// <summary>Before a world's island list is read (host) or when joining one (client: the host's come).</summary>
		/// <summary>An option was on at some point in this world: its seed stays in the world file even with every option off
		/// (the file was deleted then, and an option switched on again got another seed - another story order, other
		/// blueprint pairs). A world that never had one writes no file for it.</summary>
		static bool used;

		internal static void Reset()
		{
			Current.Clear();
			Seed = 0;
			used = false;
			global::DynamicIslands.Editor.StoryOrder.Reset();
			if (!Raft_Network.IsHost) { Notify(); return; }
			bool isNew = false;
			try { isNew = GameManager.IsInNewGame; } catch { }
			if (isNew)
			{
				foreach (string o in Pending ?? Defaults) Current.Add(o);
				Seed = new System.Random().Next(1, int.MaxValue);
				used = Current.Count > 0;
				Log("New world: " + Describe());
				// (the level up system, when it was chosen in World settings)
				global::DynamicIslands.Editor.PlayerLevels.OnNewWorld();
			}
			global::DynamicIslands.Editor.HeadStart.Reset(isNew);
			Pending = null;
			// (a saved world loaded: every unsaved New Game choice goes back to the remembered one, as the options' and the
			// islands' did - the level up system, the randomizer and the rules box kept theirs)
			if (!isNew) { PlayerLevels.Pending = null; WorldRandomizer.Pending = null; NewWorldRulesBox.Forget(); }
			global::DynamicIslands.Editor.PrivateStorage.Reset();
			global::DynamicIslands.Editor.IronRaft.Reset();
			global::DynamicIslands.Editor.NightDanger.Reset();
			global::DynamicIslands.Editor.DailyQuest.Reset();
			global::DynamicIslands.Editor.RogueShark.Reset();
			global::DynamicIslands.Editor.GoldenBarrels.Reset();
			global::DynamicIslands.Editor.StormDays.Reset();
			global::DynamicIslands.Editor.WhaleBreach.Reset();
			global::DynamicIslands.Editor.Kraken.Reset();
			global::DynamicIslands.Editor.GhostShip.Reset();
			global::DynamicIslands.Editor.StormSeas.Reset();
			global::DynamicIslands.Editor.TraderRaft.Reset();
			global::DynamicIslands.Editor.UpgradeTable.Reset();
			ScrambledBlueprints.Reset();
			global::DynamicIslands.Editor.GhostRafts.Reset();
			global::DynamicIslands.Editor.LightningStrikes.Reset();
			global::DynamicIslands.Editor.UfoSightings.Reset();
			global::DynamicIslands.Editor.FogBanks.Reset();
			Notify();
			if (isNew) Broadcast();
		}

		internal static bool ReadLine(string key, string value)
		{
			switch (key)
			{
				case "options": Current.Clear(); foreach (string o in Parse(value)) Current.Add(o); if (Current.Count > 0) used = true; global::DynamicIslands.Editor.UpgradeTable.OptionsRead(On(Upgrades)); Notify(); return true;
				case "optionsused": used = true; return true;
				case "optionseed": int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out Seed); Notify(); return true;
			}
			return global::DynamicIslands.Editor.HeadStart.ReadLine(key, value) || global::DynamicIslands.Editor.PrivateStorage.ReadLine(key, value) || global::DynamicIslands.Editor.GhostRafts.ReadLine(key, value) || global::DynamicIslands.Editor.DailyQuest.ReadLine(key, value) || global::DynamicIslands.Editor.RogueShark.ReadLine(key, value) || global::DynamicIslands.Editor.TraderRaft.ReadLine(key, value) || global::DynamicIslands.Editor.UpgradeTable.ReadLine(key, value) || global::DynamicIslands.Editor.StormSeas.ReadLine(key, value);
		}

		internal static IEnumerable<string> WriteLines()
		{
			// (the head start's lines also in a world without options)
			foreach (string l in global::DynamicIslands.Editor.HeadStart.WriteLines()) yield return l;
			if (Current.Count == 0 && Seed == 0) yield break;
			yield return "@options=" + string.Join(",", All.Where(On).ToArray());
			yield return "@optionseed=" + Seed.ToString(CultureInfo.InvariantCulture);
			if (used) yield return "@optionsused=1";
			foreach (string l in global::DynamicIslands.Editor.PrivateStorage.WriteLines()) yield return l;
			foreach (string l in global::DynamicIslands.Editor.GhostRafts.WriteLines()) yield return l;
			foreach (string l in global::DynamicIslands.Editor.DailyQuest.WriteLines()) yield return l;
			foreach (string l in global::DynamicIslands.Editor.RogueShark.WriteLines()) yield return l;
			foreach (string l in global::DynamicIslands.Editor.TraderRaft.WriteLines()) yield return l;
			foreach (string l in global::DynamicIslands.Editor.UpgradeTable.WriteLines()) yield return l;
			foreach (string l in global::DynamicIslands.Editor.StormSeas.WriteLines()) yield return l;
		}

		internal static bool HasState { get { return Current.Count > 0 || used || global::DynamicIslands.Editor.PrivateStorage.HasState || global::DynamicIslands.Editor.HeadStart.HasState; } }

		#endregion

		#region Network (kind 17)

		internal static IslandNetMessage Message()
		{
			return new IslandNetMessage { Kind = IslandNetMessage.WorldOptions, Data = Encode(Current, Seed), Name = global::DynamicIslands.Editor.PrivateStorage.Encode() };
		}

		internal static void Broadcast()
		{
			if (Raft_Network.IsHost) IslandNetwork.SendToEveryone(Message());
		}

		internal static void OnMessage(IslandNetMessage msg)
		{
			if (Raft_Network.IsHost) return;
			HashSet<string> on = new HashSet<string>();
			int seed = 0;
			foreach (string part in (msg.Data ?? "").Split(';'))
			{
				int eq = part.IndexOf('=');
				if (eq < 1) continue;
				string k = part.Substring(0, eq), v = part.Substring(eq + 1);
				if (k == "on") on = Parse(v);
				else if (k == "seed") int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
			}
			Current.Clear();
			foreach (string o in on) Current.Add(o);
			Seed = seed;
			if (msg.Name != null) global::DynamicIslands.Editor.PrivateStorage.Decode(msg.Name);
			Log("The host's options: " + Describe());
			Notify();
			// (the blueprints swapped now, not at the next look 1.5 s on - AU65)
			try { ScrambledBlueprints.ApplyNow(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [options] Blueprints: " + e.Message); }
		}

		/// <summary>The world was left (every machine): none until the next world's are read or come.</summary>
		internal static void Forget()
		{
			Current.Clear();
			Seed = 0;
			used = false;
			ScrambledBlueprints.Reset();
			global::DynamicIslands.Editor.UpgradeTable.Reset();
		}

		/// <summary>Client: a host's world arrived: none until the host's come.</summary>
		internal static void OnWorldReceived()
		{
			Current.Clear();
			Seed = 0;
			global::DynamicIslands.Editor.PrivateStorage.Reset();
			global::DynamicIslands.Editor.IronRaft.Reset();
			global::DynamicIslands.Editor.NightDanger.Reset();
			global::DynamicIslands.Editor.DailyQuest.Reset();
			global::DynamicIslands.Editor.RogueShark.Reset();
			global::DynamicIslands.Editor.GoldenBarrels.Reset();
			global::DynamicIslands.Editor.StormDays.Reset();
			global::DynamicIslands.Editor.WhaleBreach.Reset();
			global::DynamicIslands.Editor.Kraken.Reset();
			global::DynamicIslands.Editor.GhostShip.Reset();
			global::DynamicIslands.Editor.StormSeas.Reset();
			global::DynamicIslands.Editor.TraderRaft.Reset();
			global::DynamicIslands.Editor.LightningStrikes.Reset();
			ScrambledBlueprints.Reset();
			global::DynamicIslands.Editor.UpgradeTable.Reset();
			Notify();
		}

		static void Notify()
		{
			// (the story order and the blueprints follow at once, on every machine)
			// (also while a world is still loading: Raft restores the notebook before the mod reads the world file's options)
			global::DynamicIslands.Editor.StoryOrder.Rebuild();
			ScrambledBlueprints.Rebuild();
			try { GoldenBarrels.Repaint(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [options] Barrels: " + e.Message); }
			if (Changed == null) return;
			try { Changed(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] [options] " + e); }
		}

		#endregion

		/// <summary>Host (and tests): changes the current world's options; every player gets them. Keeps the seed (a world without one gets one).</summary>
		public static void Set(IEnumerable<string> on)
		{
			Current.Clear();
			foreach (string o in on) if (All.Contains(o)) Current.Add(o);
			if (Seed == 0) Seed = new System.Random().Next(1, int.MaxValue);
			if (Current.Count > 0) used = true;
			Log("Set: " + Describe());
			Notify();
			Broadcast();
			IslandWorldState.Save();
		}

		[ConsoleCommand(name: "WorldOptions", docs: "The world's options (chosen in the New Game box's World settings): WorldOptions = what this world has; WorldOptions +option / -option (ghostrafts, privatestorage, longvoyage, ironraft, sharedxp, nightdanger, dailyquest, rogueshark, barrels, stormdays, traderraft, upgrades, stormseas, ufo, glowsea, lightning, ghostship, whalebreach, fogbanks) = change them for this world (host)")]
		public static void WorldOptionsCommand(string[] args)
		{
			if (args != null && args.Length > 0 && !LoadSceneManager.IsGameSceneLoaded)
			{
				// (the main menu: the next new world, remembered and shown in the World settings window - the words were ignored)
				HashSet<string> next = WorldSettingsWindow.Chosen;
				foreach (string a in args)
				{
					string o = a.TrimStart('+', '-').ToLowerInvariant();
					if (!All.Contains(o)) { Debug.Log("[CUSTOM ISLANDS] Unknown option '" + a + "' (" + string.Join(", ", All) + ")"); continue; }
					if (a.StartsWith("-")) next.Remove(o); else next.Add(o);
				}
				SaveDefaults(next);
				try { WorldSettingsWindow.Show(); } catch { }
			}
			else if (args != null && args.Length > 0 && LoadSceneManager.IsGameSceneLoaded)
			{
				if (!Raft_Network.IsHost) { Debug.Log("[CUSTOM ISLANDS] Only the host changes the world's options"); return; }
				var on = new HashSet<string>(Current);
				foreach (string a in args)
				{
					string o = a.TrimStart('+', '-').ToLowerInvariant();
					if (!All.Contains(o)) { Debug.Log("[CUSTOM ISLANDS] Unknown option '" + a + "' (" + string.Join(", ", All) + ")"); continue; }
					if (a.StartsWith("-")) on.Remove(o); else on.Add(o);
				}
				Set(on);
			}
			Debug.Log("[CUSTOM ISLANDS] World options: " + Describe() + (LoadSceneManager.IsGameSceneLoaded ? "" : " (next new world: " + Describe(Pending ?? Defaults) + ")"));
		}
	}
}
