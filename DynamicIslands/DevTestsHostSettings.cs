using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Tests: what every player of a world must share, whoever they are and however they came in (live, joining, joining
	/// again, both Rafts restarted, the host loading the world again): the host's settings and the world's.
	/// tools\mpsettings.ps1 compares them between the host and player 2.
	/// </summary>
	public static partial class DevTests
	{
		static uint Fnv(string s)
		{
			uint h = 2166136261;
			foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
			return h;
		}

		/// <summary>The lines of CIServerSig: one per thing every player must have the same.</summary>
		internal static List<string> ServerSig()
		{
			var inv = CultureInfo.InvariantCulture;
			var lines = new List<string>();
			lines.Add("monsters " + MonsterDifficulty.Names[MonsterDifficulty.Current]);
			lines.Add("buildcost " + BuildCost.Current);
			// (the numbers Raft's build menu has now on this machine: what a block really costs here - its items found afresh,
			// since BuildCost gathers them only once a percent above 0 was used on this machine)
			int n = 0, sum = 0;
			foreach (CostMultiple c in BuildCost.Entries(BuildCost.FindBuildMenuItems())) { n++; sum += c.amount; }
			lines.Add("buildmenu " + n + " amounts, sum " + sum);
			lines.Add("randomizer " + WorldRandomizer.Current.Encode());
			lines.Add("receiver " + (WorldRules.ShowOnReceiver ? "shown" : "hidden"));
			lines.Add("unload " + WorldRules.UnloadDistance.ToString("F0", inv));
			lines.Add("regrow " + WorldRules.RegrowDays);
			lines.Add("animallooks " + AnimalLooks.Names[AnimalLooks.Current] + " caught " + AnimalLooks.Caught.ToString("X", inv));
			string mode = "?";
			try { mode = GameManager.GameMode.ToString(); } catch { }
			lines.Add("gamemode " + mode);
			var islands = IslandWorldState.Islands.OrderBy(e => e.Id).ToList();
			lines.Add("islands " + islands.Count + (islands.Count > 0 ? " " : "") + string.Join(" ", islands.Select(e => e.Id + ":" + e.HostName + (string.IsNullOrEmpty(e.Label) ? "" : "'" + e.Label + "'")).ToArray()));
			IslandNetMessage story = StoryBook.StateMessage();
			lines.Add("story " + Fnv(story != null ? story.Data : "").ToString("X8"));
			// (Raft's story chain with the plan's islands in it - kind 19: the steps, frequencies, unlocked, fired, done, brought)
			lines.Add("storychain " + Fnv(StoryChain.Message().Data ?? "").ToString("X8"));
			// The level up system: on or off for the world, and each player here with their level and Health points as
			// this machine knows them (the host from the records, a player from the host's list)
			// The world's options (WorldSettingsWindow): what is on, the seed, and what follows from it on this machine - the
			// story order, the blueprints' pairs, the story islands unlocked, and the private storages' builders
			lines.Add("options " + WorldOptions.Encode(WorldOptions.Current, WorldOptions.Seed));
			lines.Add("storyorder " + StoryOrder.Describe(StoryOrder.Order));
			lines.Add("blueprints " + (ScrambledBlueprints.Active ? string.Join(",", ScrambledBlueprints.Movable.Select(b => b.Replace("Blueprint_", "") + ">" + ScrambledBlueprints.Map(b).Replace("Blueprint_", "")).ToArray()) : "Raft's own"));
			try { lines.Add("unlocked " + string.Join(",", (NoteBook.unlockedChunkPointType ?? new List<ChunkPointType>()).Where(t => Array.IndexOf(StoryOrder.Chain, t) >= 0).Select(StoryOrder.Name).OrderBy(x => x).ToArray())); } catch { }
			lines.Add("storages " + PrivateStorage.Encode());
			lines.Add("levels " + (PlayerLevels.On ? "on" : "off"));
			if (PlayerLevels.On)
			{
				LevelRecord own = PlayerLevels.Mine; // (the host's own record exists once asked for)
				var players = UnityEngine.Object.FindObjectsOfType<Network_Player>().Where(p => p != null).Select(p => p.steamID.Id).Distinct().OrderBy(id => id).ToList();
				lines.Add("levelplayers " + string.Join(" ", players.Select(id => id + "=" + PlayerLevels.LevelOf(id) + ":" + PlayerLevels.HealthPointsOf(id)).ToArray()));
			}
			return lines;
		}

		[ConsoleCommand(name: "CIServerSig", docs: "Dev, in game (either player): what every player of this world must have the same, as this machine has it - monster difficulty, build cost (and the build menu's numbers), randomizer, the host's Receiver/unload/regrow settings, game mode, the island list, the crew's story: 'SERVER <what> <value>' lines to compare between players")]
		public static void ServerSigCommand()
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("run in a world"); return; }
			foreach (string line in ServerSig()) Log("SERVER " + line);
			Log("PASS: server signature (" + (Raft_Network.IsHost ? "host" : "player who joined") + ")");
		}

		[ConsoleCommand(name: "CIHostOnlyCheck", docs: "Dev, in game (a player who joined): tries every command that changes the host's settings or islands (Monsters, BuildCost, Randomizer, WorldPlan, CustomIslandsAuto, SpawnIsland, RemoveIsland, WorldOptions, Levels, RegrowDays, WorldIslands, WorldIslandsGap, HeadStart, RogueShark spawn/giant, Traders bring, Barrels/StormDays roll, Resync); each must be refused (or, where it only reads, do nothing) and change nothing here")]
		public static void HostOnlyCheck()
		{
			StartTest(HostOnlyRoutine());
		}

		static IEnumerator HostOnlyRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || Raft_Network.IsHost) { Fail("run in a world, as a player who joined (not the host)"); yield break; }
			bool ok = true;
			List<string> before = ServerSig();
			bool autoBefore = CustomIslandSpawner.Enabled;
			int refused = 0;
			string extraBefore = HostOnlyExtraState();
			// (every host-only command says "Only the host ...": "can", "changes", "switches", "chooses")
			Application.LogCallback counter = (msg, trace, type) => { if (msg.Contains("Only the host")) refused++; };
			Application.logMessageReceived += counter;
			string otherLevel = MonsterDifficulty.Names[MonsterDifficulty.Current == MonsterDifficulty.Nightmare ? MonsterDifficulty.Timid : MonsterDifficulty.Nightmare].ToLowerInvariant();
			string otherPercent = BuildCost.Current == 100 ? "5" : "100";
			var tries = new List<KeyValuePair<string, Action>>
			{
				new KeyValuePair<string, Action>("Monsters " + otherLevel, () => MonsterDifficulty.MonstersCommand(new[] { otherLevel })),
				new KeyValuePair<string, Action>("BuildCost " + otherPercent, () => BuildCost.BuildCostCommand(new[] { otherPercent })),
				new KeyValuePair<string, Action>("Randomizer wild", () => DynamicIslands.RandomizerCommand(new[] { "wild" })),
				new KeyValuePair<string, Action>("WorldPlan " + CIAdventure, () => DynamicIslands.WorldPlanCommand(new[] { CIAdventure })),
				new KeyValuePair<string, Action>("CustomIslandsAuto " + (autoBefore ? "off" : "on"), () => DynamicIslands.CustomIslandsAutoCommand(new[] { autoBefore ? "off" : "on" })),
				new KeyValuePair<string, Action>("SpawnIsland cicreature 200", () => DynamicIslands.SpawnIslandCommand(new[] { "cicreature", "200" })),
				new KeyValuePair<string, Action>("RemoveIsland all", () => DynamicIslands.RemoveIslandCommand(new[] { "all" })),
				new KeyValuePair<string, Action>("WorldOptions +ghostrafts -privatestorage", () => WorldOptions.WorldOptionsCommand(new[] { "+ghostrafts", "-privatestorage" })),
				new KeyValuePair<string, Action>("Levels " + (PlayerLevels.On ? "off" : "on"), () => DynamicIslands.LevelsCommand(new[] { PlayerLevels.On ? "off" : "on" })),
				new KeyValuePair<string, Action>("WorldIslands all", () => WorldIslands.WorldIslandsCommand(new[] { "all" })),
				new KeyValuePair<string, Action>("RegrowDays " + (WorldRules.RegrowDays == 9 ? "4" : "9"), () => DynamicIslands.RegrowDaysCommand(new[] { WorldRules.RegrowDays == 9 ? "4" : "9" })),
				new KeyValuePair<string, Action>("WorldIslandsGap 20-50", () => WorldIslands.GapCommand(new[] { "20-50" })),
			};
			// (these say something else, or nothing, when a client tries: only that nothing changes is checked)
			var quiet = new List<KeyValuePair<string, Action>>
			{
				new KeyValuePair<string, Action>("HeadStart 3", () => HeadStart.HeadStartCommand(new[] { "3" })),
				new KeyValuePair<string, Action>("RogueShark spawn", () => RogueShark.RogueSharkCommand(new[] { "spawn" })),
				new KeyValuePair<string, Action>("RogueShark giant", () => RogueShark.RogueSharkCommand(new[] { "giant" })),
				new KeyValuePair<string, Action>("RogueShark kill", () => RogueShark.RogueSharkCommand(new[] { "kill" })),
				new KeyValuePair<string, Action>("Traders bring", () => TraderRaft.TradersCommand(new[] { "bring" })),
				new KeyValuePair<string, Action>("Barrels roll 1 200", () => GoldenBarrels.BarrelsCommand(new[] { "roll", "1", "200" })),
				new KeyValuePair<string, Action>("StormDays roll 1 100", () => StormDays.StormDaysCommand(new[] { "roll", "1", "100" })),
				new KeyValuePair<string, Action>("Kraken bring", () => Kraken.KrakenCommand(new[] { "bring" })),
				new KeyValuePair<string, Action>("Resync", () => DynamicIslands.ResyncCommand()), // (a client's own command: allowed, must change no setting)
			};
			foreach (var t in tries)
			{
				int r0 = refused;
				try { t.Value(); } catch (Exception e) { Check(ref ok, false, t.Key + " threw " + e.GetType().Name + ": " + e.Message); continue; }
				yield return null;
				Check(ref ok, refused > r0, t.Key + ": refused (\"Only the host ...\")");
			}
			foreach (var t in quiet)
			{
				try { t.Value(); } catch (Exception e) { Check(ref ok, false, t.Key + " threw " + e.GetType().Name + ": " + e.Message); continue; }
				yield return null;
			}
			Log("Also tried without a refusal text (nothing may change): " + string.Join(", ", quiet.Select(q => q.Key).ToArray()));
			yield return new WaitForSeconds(3f); // (anything a slip let through would have come back from the host by now)
			Application.logMessageReceived -= counter;
			List<string> after = ServerSig();
			List<string> diff = before.Where((l, i) => i >= after.Count || after[i] != l).Select(l => l + " -> " + (after.FirstOrDefault(a => a.Split(' ')[0] == l.Split(' ')[0]) ?? "(gone)")).ToList();
			Check(ref ok, diff.Count == 0, "nothing changed here (the server signature is the same" + (diff.Count > 0 ? ": " + string.Join("; ", diff.ToArray()) : "") + ")");
			string extraAfter = HostOnlyExtraState();
			Check(ref ok, extraAfter == extraBefore, "regrow days, island gap, head start, rogue shark and trader stalls unchanged here (" + extraBefore + (extraAfter != extraBefore ? " -> " + extraAfter : "") + ")");
			Check(ref ok, CustomIslandSpawner.Enabled == autoBefore, "automatic islands unchanged here (" + (autoBefore ? "on" : "off") + ")");
			if (ok) Log("PASS: host-only commands refused"); else Fail("host-only commands refused");
		}

		static string HostOnlyExtraState()
		{
			return "regrow " + WorldRules.RegrowDays + ", gap " + WorldIslands.GapMin + "-" + WorldIslands.GapMax + ", headstart " + HeadStart.Level + "/" + HeadStart.Chosen +
				", rogue #" + RogueShark.Index + (RogueShark.Giant ? " giant" : "") + (RogueShark.Alive ? " alive" : "") + ", stalls " + UnityEngine.Object.FindObjectsOfType<TradeStand>().Length;
		}

		[ConsoleCommand(name: "CIRandomizerHints", docs: "Dev, anywhere: each randomizer part and level has its hover hint, with how often per level and what stays as Raft has it (the user, 2026-10-04)")]
		public static void RandomizerHintsCommand()
		{
			bool ok = true;
			Check(ref ok, RandomizerSettings.FeatureHints.Length == RandomizerSettings.Features.Length, "a hint for each of the " + RandomizerSettings.Features.Length + " parts");
			for (int i = 0; i < RandomizerSettings.FeatureHints.Length; i++)
			{
				string h = RandomizerSettings.FeatureHints[i];
				Check(ref ok, h.Contains(" / ") && h.Contains("Stays"), RandomizerSettings.FeatureLabels[i] + ": per level and what stays (" + h.Length + " characters)");
			}
			for (int l = 0; l < RandomizerSettings.LevelNames.Length; l++)
				Check(ref ok, RandomizerSettings.LevelHint(l).StartsWith(RandomizerSettings.LevelNames[l].ToUpperInvariant()), "level " + RandomizerSettings.LevelNames[l] + ": " + RandomizerSettings.LevelHint(l));
			if (ok) Log("PASS: randomizer hints"); else Fail("randomizer hints");
		}

		[ConsoleCommand(name: "CIPlanIslandsPool", docs: "Dev, anywhere: the islands world plans bring (even listed by name in spawnpool.txt) and the randomizer's rnd- extras are never in the sailing list / random pool - they only come in their own plans (the user, 2026-10-06)")]
		public static void PlanIslandsPoolCommand()
		{
			bool ok = true;
			CustomIslandSpawner.LoadPool(true);
			HashSet<string> planned = CustomIslandSpawner.AllPlansIslandNames();
			List<string> withFiles = planned.Where(n => System.IO.File.Exists(IslandSpawner.PathFor(n))).ToList();
			Check(ref ok, withFiles.Count > 0, "the plans bring " + planned.Count + " island(s), " + withFiles.Count + " of them on this PC (e.g. " + string.Join(", ", withFiles.Take(4).ToArray()) + ")");
			List<string> all = WorldIslands.Candidates();
			Check(ref ok, !all.Any(planned.Contains), "none of them in the list (" + all.Count + " entries)");
			Check(ref ok, !CustomIslandSpawner.Pool(false).Any(p => planned.Contains(p.Key)), "none of them in the random pool");
			Check(ref ok, !all.Any(n => n.StartsWith(WorldRandomizer.ExtrasPrefix, StringComparison.OrdinalIgnoreCase)), "the randomizer's rnd- extras never in it");
			Check(ref ok, all.Count(WorldIslands.IsIsland) > 0, "single islands are in it (" + all.Count(WorldIslands.IsIsland) + ")");
			if (ok) Log("PASS: plan islands pool"); else Fail("plan islands pool");
		}

		[ConsoleCommand(name: "CIUniqueCopy", docs: "Dev: CIUniqueCopy <island|sample> <new name> - a copy of a saved island (or the sample fixture) with a setting of its own, so no other file has its content (host-swap tests: B must have no file of it, AU6 finds islands by content)")]
		public static void UniqueCopyCommand(string[] args)
		{
			if (args == null || args.Length != 2) { Fail("usage: CIUniqueCopy <island> <new name>"); return; }
			try
			{
				// ("sample": the tests' own sample island packed in the dev .rmod - generated_sample.island left Mods 10-16)
				IslandFile f = args[0].Equals("sample", StringComparison.OrdinalIgnoreCase) ? SampleIsland() : IslandFile.Load(IslandSpawner.PathFor(args[0]));
				f.Name = args[1];
				f.Props["test.unique"] = Guid.NewGuid().ToString("N");
				f.Save(IslandSpawner.PathFor(args[1]));
				Log("PASS: unique copy '" + args[1] + "' (" + IslandNetwork.HashOf(args[1]) + ")");
			}
			catch (Exception e) { Fail("unique copy: " + e.Message); }
		}

		[ConsoleCommand(name: "CISpawnPoolSet", docs: "Dev (either player): gives this machine other spawnpool.txt numbers than its file, kept when the file is read again - to test that a player who joins uses the host's: CISpawnPoolSet regrowDays 1 showOnReceiver 0 unloadDistance 350 ... | CISpawnPoolSet reset")]
		public static void SpawnPoolSet(string[] args)
		{
			if (args != null && args.Length == 1 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
			{
				CustomIslandSpawner.TestOverrides.Clear();
			}
			else
			{
				if (args == null || args.Length < 2 || args.Length % 2 != 0) { Fail("usage: CISpawnPoolSet <key> <value> [<key> <value> ...] | reset"); return; }
				for (int i = 0; i < args.Length; i += 2)
				{
					float v;
					string key = args[i].ToLowerInvariant();
					if (!float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v) || !CustomIslandSpawner.SetValue(key, v)) { Fail("not a number setting: " + args[i] + " " + args[i + 1]); return; }
					CustomIslandSpawner.TestOverrides[key] = v;
				}
			}
			CustomIslandSpawner.LoadPool(true);
			Log("Spawn pool here: regrowDays " + CustomIslandSpawner.RegrowDays + ", showOnReceiver " + (CustomIslandSpawner.ShowOnReceiver ? 1 : 0) + ", unloadDistance " +
				CustomIslandSpawner.UnloadDistance.ToString("F0", CultureInfo.InvariantCulture) + " (overrides: " + CustomIslandSpawner.TestOverrides.Count + "); in this game: " + WorldRules.HostSettingsDescribe());
		}

		[ConsoleCommand(name: "CIHarvestState", docs: "Dev, in game (either player): an island's used trees and pickups as this machine has them (to compare between players): CIHarvestState <island>")]
		public static void HarvestState(string[] args)
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => args != null && args.Length > 0 && x.HostName.Equals(args[0], StringComparison.OrdinalIgnoreCase));
			if (e == null) { Fail("no island '" + (args != null && args.Length > 0 ? args[0] : "") + "'"); return; }
			if (e.Root != null) IslandObjectState.Capture(e);
			List<int> used = e.State.Keys.Where(k => k < CreatureSpawner.StateKeyBase).OrderBy(k => k).ToList();
			int shownUsed = 0;
			if (e.Root != null)
				foreach (PickupItem_Networked pn in e.Root.GetComponentsInChildren<PickupItem_Networked>(true))
				{
					HarvestableTree tree = pn.GetComponent<HarvestableTree>();
					if (!pn.gameObject.activeSelf || (tree != null && tree.Depleted)) shownUsed++;
				}
			Log("HARVEST " + e.HostName + " used " + used.Count + ": " + string.Join(" ", used.Select(k => k.ToString("X")).ToArray()) + " | shown used here " + shownUsed +
				", waiting to show as grown back " + e.Regrown.Count + " (" + (e.Root != null ? "loaded" : "unloaded") + ", day " + WorldManager.DayCounter + ", " + (Raft_Network.IsHost ? "host" : "player who joined") + ")");
		}

		[ConsoleCommand(name: "CIHitAmount", docs: "Dev, in game (either player): one hit of <damage> on the first live animal of a kind at an island, through Raft's DamageEntity (a client's goes to the host): CIHitAmount <island> <kind> <damage>")]
		public static void HitAmount(string[] args)
		{
			if (args == null || args.Length < 3) { Fail("usage: CIHitAmount <island> <kind> <damage>"); return; }
			float damage;
			if (!float.TryParse(args[args.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out damage)) { Fail("not a number: " + args[args.Length - 1]); return; }
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string kind = string.Join(" ", args.Skip(1).Take(args.Length - 2).ToArray());
			AI_NetworkBehaviour a = AnimalsOf(e, kind, 150f).Where(x => x.networkEntity != null && !x.networkEntity.IsDead).OrderBy(x => x.ObjectIndex).FirstOrDefault();
			if (a == null) { Fail("no live " + kind + " at '" + e.HostName + "'"); return; }
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (host == null) { Fail("no Network_Host"); return; }
			float before = a.networkEntity.stat_health.Value;
			host.DamageEntity(a.networkEntity, a.transform, damage, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("Hit " + kind + " id " + a.ObjectIndex + " for " + damage.ToString("F0", CultureInfo.InvariantCulture) + " (health here was " + before.ToString("F0", CultureInfo.InvariantCulture) + "/" +
				a.networkEntity.stat_health.Max.ToString("F0", CultureInfo.InvariantCulture) + "; " + (Raft_Network.IsHost ? "host" : "client: sent to the host") + ")");
		}
	}
}
