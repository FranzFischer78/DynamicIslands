using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Test gaps (TEST_GAPS_2026-10-13): the newer world options for a late joiner / a rejoin / a host restart (CILateState,
	/// compared between the players by tools\mplatejoin.ps1), the options switched off and on while their effect is live
	/// (CIOptionsLiveToggle), the giant rogue shark with player 2 (CIGiantMP), a player standing on a ghost raft's or a trader
	/// raft's deck while the real raft moves away (CIGhostDeck).
	/// </summary>
	public static partial class DevTests
	{
		#region CILateState - what a player sees of the newer world options

		[ConsoleCommand(name: "CILateState", docs: "Dev, in game (either player): the newer world options as this machine has them - 'LATE <what> ...' lines to compare between players (tools\\mplatejoin.ps1): options, daily quest, rogue shark, trader stalls, storm day, barrels, night danger, iron raft, shared EXP. CILateState [night|day|sky] takes night or day for the Night danger lines (sky = Raft's own, the default)")]
		public static void LateStateCommand(string[] args)
		{
			if (!LoadSceneManager.IsGameSceneLoaded) { Fail("late state: run in a world"); return; }
			string mode = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "sky";
			NightDanger.TestNight = mode == "night" ? true : mode == "day" ? false : (bool?)null;
			var inv = CultureInfo.InvariantCulture;
			string who = Raft_Network.IsHost ? "host" : "player who joined";
			Log("LATE options " + WorldOptions.Encode(WorldOptions.Current, WorldOptions.Seed));
			DailyQuest.Task d = DailyQuest.Current;
			Log("LATE daily " + (DailyQuest.On ? "on" : "off") + " day " + d.Day + " " + d.Kind + " " + d.Target + " need " + d.Need + " have " + d.Have + " " + d.State);
			AI_NetworkBehavior_Shark rogue = RogueShark.Find();
			Log("LATE rogue " + (RogueShark.On ? "on" : "off") + " index " + RogueShark.Index + " giant " + RogueShark.Giant + " alive " + (rogue != null && RogueShark.Alive) +
				" rust " + (rogue != null && RogueShark.LooksRogue(rogue)) + " quietuntil " + RogueShark.QuietUntil);
			TradeStand[] stalls = UnityEngine.Object.FindObjectsOfType<TradeStand>();
			Log("LATE trader " + (WorldOptions.On(WorldOptions.TraderRaft) ? "on" : "off") + " stalls " + stalls.Length +
				(stalls.Length == 0 ? "" : ": " + string.Join(" | ", stalls.OrderBy(t => t.Index).Select(t => "#" + t.Index + " " + TraderRaft.Text(t.Give) + ">" + TraderRaft.Text(t.Get) + " " + t.Left + "/" + t.Stock).ToArray())));
			WeatherManager wm = UnityEngine.Object.FindObjectOfType<WeatherManager>();
			Log("LATE storm " + (StormDays.On ? "on" : "off") + " day " + StormDays.Today + " isstorm " + StormDays.IsStorm + " shark x" + StormDays.SharkFactor.ToString("0.##", inv) +
				" weather " + (wm != null ? wm.GetCurrentWeatherType().ToString() : "none"));
			Log("LATE barrels " + (GoldenBarrels.On ? "on" : "off"));
			foreach (PickupItem_Networked p in UnityEngine.Object.FindObjectsOfType<PickupItem_Networked>().Where(GoldenBarrels.IsBarrel).OrderBy(p => p.ObjectIndex))
				Log("LATE barrel #" + p.ObjectIndex + "=" + (GoldenBarrels.Kind(p) == GoldenBarrels.Gold ? "golden" : GoldenBarrels.Kind(p) == GoldenBarrels.Silver ? "silver" : "plain") + (GoldenBarrels.LooksSpecial(p) ? "*" : ""));
			Log("LATE night " + (NightDanger.On ? "on" : "off") + " night " + NightDanger.IsNight + " monsters x" + NightDanger.MonsterFactor.ToString("0.##", inv) + " shark x" + NightDanger.SharkFactor.ToString("0.##", inv));
			Log("LATE iron " + (WorldOptions.On(WorldOptions.IronRaft) ? "on" : "off") + " bites 6,5,5 take " + IronBites());
			Log("LATE sharedxp " + (WorldOptions.On(WorldOptions.SharedXp) ? "on" : "off"));
			NightDanger.TestNight = null;
			Log("PASS: late state (" + who + ")");
		}

		static Block RaftFoundation()
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null) return null;
			return raft.GetComponentsInChildren<Block>().FirstOrDefault(x => x.name.Contains("Foundation") || (x.buildableItem != null && x.buildableItem.UniqueName.Contains("Foundation")));
		}

		/// <summary>What bites of 6, 5 and 5 take from a raft foundation under the option as it is now (the block is not hurt).</summary>
		static string IronBites()
		{
			Block floor = RaftFoundation();
			if (floor == null) return "n/a (no foundation)";
			IronRaft.Reset();
			string r = string.Join(",", new[] { 6, 5, 5 }.Select(b => IronRaft.Scaled(floor, b).ToString()).ToArray());
			IronRaft.Reset();
			return r;
		}

		#endregion

		#region CIOptionsLiveToggle - options switched off and on while their effect is live

		[ConsoleCommand(name: "CIOptionsLiveToggle", docs: "Dev, world (host, a test world 'CI ...'): each newer option's effect started, its option switched off, the effect ended cleanly (or stays as designed), switched on again: iron raft, night danger (shark search interval), daily quest (open task), storm day (held weather), rogue shark (alive), trader raft (afloat), silver & golden barrel. No exception or mod warning in between. CIOptionsLiveToggle [iron|night|daily|storm|rogue|trader|barrels ...] (default all). The two-player part is mplatejoin.ps1 -parts toggle")]
		public static void OptionsLiveToggleCommand(string[] args) { StartTest(OptionsLiveToggleRoutine(args)); }

		static readonly string[] LiveToggleOptions = { WorldOptions.IronRaft, WorldOptions.NightDanger, WorldOptions.DailyQuest, WorldOptions.StormDays, WorldOptions.RogueShark, WorldOptions.TraderRaft, WorldOptions.Barrels };

		static void LiveOpt(string option, bool on)
		{
			var s = new HashSet<string>(WorldOptions.Current);
			if (on) s.Add(option); else s.Remove(option);
			WorldOptions.Set(s);
		}

		static IEnumerator OptionsLiveToggleRoutine(string[] args)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("options live toggle: host, in a world"); yield break; }
			bool all = args == null || args.Length == 0;
			Func<string, bool> want = k => all || args.Contains(k);
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			int bad = 0;
			var logs = new List<string>();
			Application.LogCallback watch = (text, trace, type) =>
			{
				if (type == LogType.Exception || ((type == LogType.Warning || type == LogType.Error) && text.Contains("[CUSTOM ISLANDS]") &&
					System.Text.RegularExpressions.Regex.IsMatch(text, @"\[(rogue|storm|night|ironraft|barrels|daily|trader|options)\]")))
				{ bad++; Log("  (logged: " + text + ")"); }
				if (text.Contains("[storm]") || text.Contains("[rogue]")) logs.Add(text);
			};
			Application.logMessageReceived += watch;
			string dailyBefore = DailyQuest.Current.Encode();
			string rogueBefore = RogueShark.WriteLines().FirstOrDefault() ?? "@rogueshark=0;0";
			var knownIslands = new HashSet<int>(IslandWorldState.Islands.Select(e => e.Id));
			try
			{
				WorldOptions.Set(new HashSet<string>(optionsBefore.Where(o => Array.IndexOf(LiveToggleOptions, o) < 0)));
				IronRaft.Reset();
				yield return null;

				if (want("iron"))
				{
					// the bites (6, 5, 5 take 3, 2, 3 with the option on; the whole bite off)
					LiveOpt(WorldOptions.IronRaft, true);
					string a = IronBites();
					Check(ref ok, a == "3,2,3", "iron raft on: bites of 6, 5, 5 take " + a);
					LiveOpt(WorldOptions.IronRaft, false);
					string b = IronBites();
					Check(ref ok, b == "6,5,5", "iron raft switched off: the whole bite (" + b + ")");
					LiveOpt(WorldOptions.IronRaft, true);
					string c = IronBites();
					Check(ref ok, c == "3,2,3", "iron raft on again: halved again, no carry left over (" + c + ")");
					LiveOpt(WorldOptions.IronRaft, false);
				}

				if (want("night"))
				{
					NightDanger.TestNight = true;
					LiveOpt(WorldOptions.NightDanger, true);
					AI_NetworkBehavior_Shark shark = LiveSharks().FirstOrDefault();
					AI_StateMachine_Shark sm = shark != null ? shark.GetComponentInChildren<AI_StateMachine_Shark>(true) : null;
					float onSearch = sm != null ? sm.SearchBlockInterval : -1f;
					Check(ref ok, Mathf.Approximately(NightDanger.SharkFactor, NightDanger.SharkNight) && Mathf.Approximately(NightDanger.MonsterFactor, NightDanger.MonsterNight), "night danger on, at night: shark x" + NightDanger.SharkFactor + ", monsters x" + NightDanger.MonsterFactor);
					LiveOpt(WorldOptions.NightDanger, false);
					float offSearch = sm != null ? sm.SearchBlockInterval : -1f;
					Check(ref ok, NightDanger.SharkFactor == 1f && NightDanger.MonsterFactor == 1f, "switched off: shark x" + NightDanger.SharkFactor + ", monsters x" + NightDanger.MonsterFactor + " at once");
					if (sm != null && offSearch > 0f) Check(ref ok, Mathf.Abs(onSearch / offSearch - NightDanger.SharkNight) < 0.01f, "the shark's next look for a block to bite (pending): " + onSearch.ToString("F1") + " s at night with the option on, " + offSearch.ToString("F1") + " s off (x" + (onSearch / offSearch).ToString("F2") + ")");
					else Log("  (no shark here: the search interval isn't compared)");
					LiveOpt(WorldOptions.NightDanger, true);
					float again = sm != null ? sm.SearchBlockInterval : -1f;
					Check(ref ok, Mathf.Approximately(NightDanger.SharkFactor, NightDanger.SharkNight) && (sm == null || Mathf.Abs(again - onSearch) < 0.01f), "on again: shark x" + NightDanger.SharkFactor + (sm != null ? " (" + again.ToString("F1") + " s)" : ""));
					LiveOpt(WorldOptions.NightDanger, false);
					NightDanger.TestNight = null;
				}

				if (want("daily"))
				{
					// a day in the future so the tick does not start another; by day so it does not run out
					NightDanger.TestNight = false;
					LiveOpt(WorldOptions.DailyQuest, true);
					DailyQuest.Start(Math.Max(DailyQuest.Current.Day + 1, DailyQuest.Today + 1));
					DailyQuest.Current.Kind = DailyQuest.Gather; DailyQuest.Current.Target = "Plank"; DailyQuest.Current.Need = 5; DailyQuest.Current.Have = 0; DailyQuest.Current.State = "open";
					DailyQuest.Add(DailyQuest.Gather, "Plank", 2);
					Check(ref ok, DailyQuest.Current.Have == 2 && DailyQuest.Current.State == "open", "daily quest on: 2 of 5 planks counted (" + DailyQuest.Current.Have + "/" + DailyQuest.Current.Need + " " + DailyQuest.Current.State + ")");
					LiveOpt(WorldOptions.DailyQuest, false);
					NightDanger.TestNight = true; // (night would run it out if the option still ran the tick)
					DailyQuest.Add(DailyQuest.Gather, "Plank", 2);
					yield return new WaitForSeconds(2.5f);
					Check(ref ok, DailyQuest.Current.Have == 2 && DailyQuest.Current.State == "open", "switched off, at night: nothing counted, the task neither done nor run out (" + DailyQuest.Current.Have + "/" + DailyQuest.Current.Need + " " + DailyQuest.Current.State + ")");
					NightDanger.TestNight = false;
					LiveOpt(WorldOptions.DailyQuest, true);
					DailyQuest.Add(DailyQuest.Gather, "Plank", 1);
					Check(ref ok, DailyQuest.Current.Have == 3 && DailyQuest.Current.State == "open", "on again: counting goes on from 2 (" + DailyQuest.Current.Have + "/" + DailyQuest.Current.Need + " " + DailyQuest.Current.State + ")");
					LiveOpt(WorldOptions.DailyQuest, false);
					DailyQuest.Current = DailyQuest.Task.Decode(dailyBefore);
					NightDanger.TestNight = null;
				}

				if (want("storm"))
				{
					int seed = WorldOptions.Seed;
					int day = Enumerable.Range(Math.Max(StormDays.FirstDay, StormDays.Today + 1), 2000).First(x => StormDays.Rolls(seed, x));
					StormDays.TestDay = day;
					StormDays.TestNight = false;
					logs.Clear();
					WeatherManager wm = UnityEngine.Object.FindObjectOfType<WeatherManager>();
					LiveOpt(WorldOptions.StormDays, true);
					yield return WaitFor(() => wm == null || wm.GetCurrentWeatherType() == StormDays.StormWeather(wm), 10f);
					Check(ref ok, StormDays.IsStorm && Mathf.Approximately(StormDays.SharkFactor, StormDays.SharkStorm) && (wm == null || wm.GetCurrentWeatherType() == StormDays.StormWeather(wm)),
						"storm day " + day + " on: storm weather " + (wm != null ? wm.GetCurrentWeatherType().ToString() : "none") + ", shark x" + StormDays.SharkFactor);
					LiveOpt(WorldOptions.StormDays, false);
					yield return WaitFor(() => logs.Any(l => l.Contains("The storm is over")), 10f);
					Check(ref ok, !StormDays.IsStorm && StormDays.SharkFactor == 1f, "switched off: no storm day, shark x" + StormDays.SharkFactor + " at once");
					Check(ref ok, logs.Any(l => l.Contains("The storm is over")), "switched off: the held weather is let go (\"The storm is over\")");
					LiveOpt(WorldOptions.StormDays, true);
					yield return WaitFor(() => wm == null || wm.GetCurrentWeatherType() == StormDays.StormWeather(wm), 10f);
					Check(ref ok, StormDays.IsStorm && (wm == null || wm.GetCurrentWeatherType() == StormDays.StormWeather(wm)), "on again: the storm weather again (" + (wm != null ? wm.GetCurrentWeatherType().ToString() : "none") + ")");
					LiveOpt(WorldOptions.StormDays, false);
					yield return WaitFor(() => logs.Count(l => l.Contains("The storm is over")) >= 2, 10f);
					StormDays.TestDay = null; StormDays.TestNight = null;
				}

				if (want("rogue"))
				{
					int seed = WorldOptions.Seed;
					RogueShark.Reset();
					LiveOpt(WorldOptions.RogueShark, true);
					AI_NetworkBehavior_Shark rogue = RogueShark.Spawn(false);
					yield return new WaitForSeconds(2f);
					Check(ref ok, rogue != null && RogueShark.Alive && RogueShark.Index != 0, "rogue shark on: a rogue #" + RogueShark.Index + " alive");
					LiveOpt(WorldOptions.RogueShark, false);
					int roll = Enumerable.Range(RogueShark.FirstDay + 1, 2000).First(x => RogueShark.Rolls(seed, x));
					RogueShark.TestDay = roll;
					uint index = RogueShark.Index;
					int sharks = LiveSharks().Count;
					yield return new WaitForSeconds(3f);
					// (the option's text: a rogue stays until it is killed; switched off only stops new ones - the state must be one or the other, not half)
					bool stays = RogueShark.Index == index && RogueShark.Find() != null && RogueShark.Alive;
					bool gone = RogueShark.Index == 0 && !RogueShark.Alive;
					Check(ref ok, stays || gone, "switched off: the rogue " + (stays ? "stays (alive, #" + index + ")" : gone ? "is gone" : "is in between: index " + RogueShark.Index + ", alive " + RogueShark.Alive));
					Check(ref ok, LiveSharks().Count <= sharks, "switched off, on a rolling day " + roll + ": no new shark (" + LiveSharks().Count + " alive, " + sharks + " before)");
					AI_NetworkBehavior_Shark live = RogueShark.Find();
					if (live != null && live.networkEntity != null && !live.networkEntity.IsDead)
					{
						live.networkEntity.Damage(100000f, live.transform.position, Vector3.up, EntityType.Player, true);
						yield return new WaitForSeconds(3f);
					}
					Check(ref ok, RogueShark.Index == 0 && !RogueShark.Alive, "killed with the option off: it ends cleanly (index " + RogueShark.Index + ", alive " + RogueShark.Alive + "), no new rogue on the rolling day");
					RogueShark.Reset();
					RogueShark.TestDay = roll;
					LiveOpt(WorldOptions.RogueShark, true);
					yield return new WaitForSeconds(3f);
					Check(ref ok, RogueShark.Alive && RogueShark.Index != 0, "on again, on the rolling day " + roll + ": a rogue comes (#" + RogueShark.Index + ")");
					AI_NetworkBehavior_Shark last = RogueShark.Find();
					if (last != null && last.networkEntity != null && !last.networkEntity.IsDead) last.networkEntity.Damage(100000f, last.transform.position, Vector3.up, EntityType.Player, true);
					yield return new WaitForSeconds(2.5f);
					LiveOpt(WorldOptions.RogueShark, false);
					RogueShark.TestDay = null;
				}

				if (want("trader") && CustomIslandSpawner.RaftPosition.HasValue)
				{
					LiveOpt(WorldOptions.TraderRaft, true);
					int stalls0 = UnityEngine.Object.FindObjectsOfType<TradeStand>().Length;
					string r = CustomIslandSpawner.TrySpawn(CustomIslandSpawner.RaftPosition.Value, true, CustomIslandSpawner.TypePrefix + TraderRaft.TypeName);
					Check(ref ok, r.StartsWith("Spawning"), "trader raft on: one brought (" + r + ")");
					yield return WaitFor(() => UnityEngine.Object.FindObjectsOfType<TradeStand>().Length > stalls0, 40f);
					int stalls = UnityEngine.Object.FindObjectsOfType<TradeStand>().Length;
					Check(ref ok, stalls > stalls0, "its stalls are there (" + (stalls - stalls0) + ")");
					LiveOpt(WorldOptions.TraderRaft, false);
					yield return new WaitForSeconds(2f);
					Check(ref ok, UnityEngine.Object.FindObjectsOfType<TradeStand>().Length == stalls, "switched off: the trader afloat stays (" + UnityEngine.Object.FindObjectsOfType<TradeStand>().Length + " stalls)");
					int known = IslandWorldState.Islands.Count;
					TraderRaft.Reset();
					for (int i = 0; i < 40; i++) TraderRaft.OnSailed(250f, CustomIslandSpawner.RaftPosition.Value);
					yield return new WaitForSeconds(1f);
					Check(ref ok, TraderRaft.Brought == 0 && IslandWorldState.Islands.Count == known, "switched off, 10 km sailed: no new trader (" + (IslandWorldState.Islands.Count - known) + " islands more)");
					LiveOpt(WorldOptions.TraderRaft, true);
					Check(ref ok, UnityEngine.Object.FindObjectsOfType<TradeStand>().Length == stalls, "on again: the stalls still there");
					LiveOpt(WorldOptions.TraderRaft, false);
					foreach (IslandWorldState.Entry e in IslandWorldState.Islands.Where(e => !knownIslands.Contains(e.Id) && (e.HostName ?? "").Contains(TraderRaft.TypeName)).ToList())
						IslandWorldState.RemoveIds(new List<int> { e.Id }, true);
				}

				if (want("barrels"))
				{
					PickupItem_Networked barrel = UnityEngine.Object.FindObjectsOfType<PickupItem_Networked>().Where(GoldenBarrels.IsBarrel).OrderBy(p => p.ObjectIndex).FirstOrDefault();
					if (barrel == null) Log("  (no barrel in the sea: the barrel step is skipped)");
					else
					{
						GoldenBarrels.TestKind = GoldenBarrels.Gold;
						LiveOpt(WorldOptions.Barrels, true);
						GoldenBarrels.OnSpawned(barrel);
						Check(ref ok, GoldenBarrels.Kind(barrel) == GoldenBarrels.Gold && GoldenBarrels.LooksSpecial(barrel), "barrels on: barrel #" + barrel.ObjectIndex + " golden and looks it");
						GoldenBarrels.TestKind = null;
						LiveOpt(WorldOptions.Barrels, false);
						Check(ref ok, GoldenBarrels.Kind(barrel) == GoldenBarrels.None, "switched off: no extra loot from it (kind " + GoldenBarrels.Kind(barrel) + ")");
						Check(ref ok, !GoldenBarrels.LooksSpecial(barrel), "switched off: the barrel is plain-coloured again");
						GoldenBarrels.OnSpawned(barrel);
						LiveOpt(WorldOptions.Barrels, true);
						GoldenBarrels.OnSpawned(barrel);
						Check(ref ok, GoldenBarrels.Kind(barrel) == GoldenBarrels.KindOf(WorldOptions.Seed, barrel.ObjectIndex), "on again: its kind is the world's roll (" + GoldenBarrels.Kind(barrel) + ")");
						LiveOpt(WorldOptions.Barrels, false);
						GoldenBarrels.OnSpawned(barrel);
					}
				}

				Check(ref ok, bad == 0, "no exception or mod warning while the options went off and on (" + bad + ")");
			}
			finally
			{
				Application.logMessageReceived -= watch;
				NightDanger.TestNight = null;
				StormDays.TestDay = null; StormDays.TestNight = null;
				RogueShark.TestDay = null;
				GoldenBarrels.TestKind = null;
				IronRaft.Reset();
				AI_NetworkBehavior_Shark left = RogueShark.Find();
				if (left != null && left.networkEntity != null && !left.networkEntity.IsDead) left.networkEntity.Damage(100000f, left.transform.position, Vector3.up, EntityType.Player, true);
				WorldOptions.Set(optionsBefore);
				try { DailyQuest.Current = DailyQuest.Task.Decode(dailyBefore); } catch { }
				RogueShark.ReadLine("rogueshark", rogueBefore.Substring(rogueBefore.IndexOf('=') + 1));
				IslandWorldState.Save();
			}
			if (ok) Log("PASS: options live toggle"); else Fail("options live toggle");
		}

		#endregion

		#region CIGiantMP - the giant rogue shark with player 2

		static Vector3 lastRoguePos;

		static Network_Player GiantOtherPlayer() { return UnityEngine.Object.FindObjectsOfType<Network_Player>().FirstOrDefault(p => p != null && !p.IsLocalPlayer); }

		[ConsoleCommand(name: "CIGiantMP", docs: "Dev, in game (either player; the two-player part is mplatejoin.ps1 -parts giant): the giant rogue shark. CIGiantMP info = 'GIANT info ...' (giant, rust-red, size against a plain shark, health, bite) | hit <damage> = a hit on it as a weapon does (a client's goes to the host) | bite plain|giant = (host) a bite on the other player of the shark's number, a plain shark's or the giant's | health = 'GIANT health ...' of this player | heal = this player full health | loot [x z] = 'GIANT loot ...' pickups lying where the rogue was last seen (or around x z, the other machine's spot)")]
		public static void GiantMPCommand(string[] args)
		{
			string mode = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "info";
			var inv = CultureInfo.InvariantCulture;
			AI_NetworkBehavior_Shark rogue = RogueShark.Find();
			Network_Player me = RAPI.GetLocalPlayer();
			string who = Raft_Network.IsHost ? "host" : "player who joined";
			if (mode == "info")
			{
				if (rogue == null) { Log("GIANT info: giant " + RogueShark.Giant + " index " + RogueShark.Index + " not here (" + who + ")"); Fail("giant info: no rogue shark here"); return; }
				lastRoguePos = rogue.transform.position;
				AI_NetworkBehavior_Shark plain = LiveSharks().Where(s => !RogueShark.IsRogue(s)).OrderBy(s => (s.transform.position - rogue.transform.position).sqrMagnitude).FirstOrDefault();
				Renderer rb = WorldRandomizer.BodyOf(rogue).FirstOrDefault(), pb = plain != null ? WorldRandomizer.BodyOf(plain).FirstOrDefault() : null;
				float ratio = rb != null && pb != null && pb.bounds.size.magnitude > 0f ? rb.bounds.size.magnitude / pb.bounds.size.magnitude : -1f;
				AI_State_Attack_Entity_Shark bite = rogue.GetComponentInChildren<AI_State_Attack_Entity_Shark>(true);
				Log("GIANT info: giant " + RogueShark.Giant + " index " + RogueShark.Index + " rust " + RogueShark.LooksRogue(rogue) + " size x" + ratio.ToString("F2", inv) + " hp " +
					(rogue.networkEntity != null ? rogue.networkEntity.stat_health.Value.ToString("F0", inv) + "/" + rogue.networkEntity.stat_health.Max.ToString("F0", inv) : "?") +
					" bite " + (bite != null ? bite.AttackPlayerDamage.ToString() : "?") + " (" + who + ")");
				Log("PASS: giant info");
			}
			else if (mode == "hit")
			{
				float damage;
				if (args.Length < 2 || !float.TryParse(args[1], NumberStyles.Float, inv, out damage)) { Fail("usage: CIGiantMP hit <damage>"); return; }
				if (rogue == null || rogue.networkEntity == null) { Fail("giant hit: no rogue shark here"); return; }
				Network_Host host = null;
				try { host = ComponentManager<Network_Host>.Value; } catch { }
				if (host == null) { Fail("giant hit: no Network_Host"); return; }
				lastRoguePos = rogue.transform.position;
				float before = rogue.networkEntity.stat_health.Value;
				host.DamageEntity(rogue.networkEntity, rogue.transform, damage, rogue.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
				Log("GIANT hit " + damage.ToString("F0", inv) + ": health was " + before.ToString("F0", inv) + "/" + rogue.networkEntity.stat_health.Max.ToString("F0", inv) + " (" + (Raft_Network.IsHost ? "host" : "client: sent to the host") + ")");
				Log("PASS: giant hit");
			}
			else if (mode == "bite")
			{
				bool giant = args.Length > 1 && args[1].ToLowerInvariant() == "giant";
				Network_Player target = GiantOtherPlayer();
				Network_Host host = null;
				try { host = ComponentManager<Network_Host>.Value; } catch { }
				if (!Raft_Network.IsHost || host == null || target == null || target.Stats == null) { Fail("giant bite: the host, with another player in the world"); return; }
				AI_NetworkBehavior_Shark plain = LiveSharks().FirstOrDefault(s => !RogueShark.IsRogue(s));
				AI_State_Attack_Entity_Shark b = (giant ? rogue : plain) != null ? (giant ? rogue : plain).GetComponentInChildren<AI_State_Attack_Entity_Shark>(true) : null;
				int damage = b != null ? b.AttackPlayerDamage : 0;
				if (damage <= 0 && !giant && rogue != null)
				{
					AI_State_Attack_Entity_Shark g = rogue.GetComponentInChildren<AI_State_Attack_Entity_Shark>(true);
					damage = g != null ? Mathf.RoundToInt(g.AttackPlayerDamage / RogueShark.GiantDamage) : 0; // (no plain shark: the giant's number without its multiplier)
				}
				if (damage <= 0) { Fail("giant bite: no shark's bite number found"); return; }
				PlayerStats stats = target.Stats;
				host.DamageEntity(stats, target.transform, damage, target.transform.position, Vector3.up, EntityType.Enemy, null);
				Log("GIANT bite " + (giant ? "giant" : "plain") + " " + damage + " on the other player (their health on this machine now " + stats.stat_health.Value.ToString("F1", inv) + ")");
				Log("PASS: giant bite");
			}
			else if (mode == "health")
			{
				if (me == null || me.Stats == null) { Fail("giant health: no local player"); return; }
				Log("GIANT health: " + me.Stats.stat_health.Value.ToString("F1", inv) + "/" + me.Stats.stat_health.Max.ToString("F1", inv) + " (" + who + ")");
				Log("PASS: giant health");
			}
			else if (mode == "heal")
			{
				if (me == null) { Fail("giant heal: no local player"); return; }
				KeepAlive(me);
				me.Stats.stat_BonusHealth.statTarget.Value = 0f;
				Log("PASS: giant heal (" + me.Stats.stat_health.Value.ToString("F0", inv) + ")");
			}
			else if (mode == "loot")
			{
				Vector3 at = lastRoguePos;
				float lx, lz;
				if (args.Length > 2 && float.TryParse(args[1], NumberStyles.Float, inv, out lx) && float.TryParse(args[2], NumberStyles.Float, inv, out lz)) at = new Vector3(lx, at.y, lz);
				var near = UnityEngine.Object.FindObjectsOfType<PickupItem_Networked>().Where(p => p != null && p.gameObject.activeInHierarchy && !p.name.StartsWith("Pickup_Floating_") && FlatDistance(p.transform.position, at) < 30f).ToList();
				var names = near.GroupBy(p => p.name.Replace("(Clone)", "").Trim()).OrderBy(g => g.Key).Select(g => g.Key + "x" + g.Count()).ToArray();
				Log("GIANT loot: " + near.Count + " pickup(s) within 30 m of where it was last seen (" + at.ToString("F0") + "): " + string.Join(" ", names) + " (" + who + ")");
				Log("PASS: giant loot");
			}
			else Fail("usage: CIGiantMP info | hit <damage> | bite plain|giant | health | heal | loot");
		}

		#endregion

		#region CIGhostDeck - standing on a ghost raft's or a trader raft's deck

		[ConsoleCommand(name: "CIGhostDeck", docs: "Dev, in game (the two-player part is mplatejoin.ps1 -parts ghostdeck): a raft of blocks on the sea as a deck to stand on. CIGhostDeck make [ghost|trader] = (host) brings one and logs 'DECKISLAND <name>' | stand <island> = puts this player on its deck, standing on it | check <island> [seconds] = this player stays on the deck that long (and how far Raft's own raft is) | where <island> = 'DECK where ...' | away [seconds] = (host) sails Raft's raft away from the island")]
		public static void GhostDeckCommand(string[] args)
		{
			string mode = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (mode == "make") { StartTest(GhostDeckMake(args.Length > 1 ? args[1].ToLowerInvariant() : "ghost")); return; }
			if (mode == "away")
			{
				float s;
				StartTest(GhostDeckAway(args.Length > 1 && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out s) ? s : 20f));
				return;
			}
			if ((mode == "stand" || mode == "check" || mode == "where") && args.Length > 1)
			{
				if (mode == "stand") { StartTest(GhostDeckGoStand(args[1])); return; }
				IslandWorldState.Entry e = LoadedIsland(new[] { args[1] });
				if (e == null) return;
				float seconds;
				if (mode == "stand") StartTest(GhostDeckStand(e));
				else if (mode == "check") StartTest(GhostDeckCheck(e, args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ? seconds : 5f));
				else { DeckSample(e, out _, out _); Log("PASS: deck where"); }
				return;
			}
			Fail("usage: CIGhostDeck make [ghost|trader] | stand <island> | check <island> [seconds] | where <island> | away [seconds]");
		}

		static IEnumerator GhostDeckMake(string kind)
		{
			if (!Raft_Network.IsHost || !CustomIslandSpawner.RaftPosition.HasValue) { Fail("deck island: host, in a world"); yield break; }
			yield return EnsureAlive();
			int before = IslandWorldState.Islands.Count;
			IslandWorldState.Entry e = null;
			if (kind == "trader")
			{
				string r = CustomIslandSpawner.TrySpawn(CustomIslandSpawner.RaftPosition.Value, true, CustomIslandSpawner.TypePrefix + TraderRaft.TypeName);
				if (!r.StartsWith("Spawning")) { Fail("deck island: no trader raft brought (" + r + ")"); yield break; }
				yield return WaitFor(() => IslandWorldState.Islands.Count > before && IslandWorldState.Islands.Last().Root != null, 40f);
				e = IslandWorldState.Islands.Skip(before).FirstOrDefault(i => (i.HostName ?? "").Contains(TraderRaft.TypeName));
			}
			else
			{
				var s = new IslandGenSettings { Height = 2f, Radius = 26f };
				int seed = 1;
				while (GhostRafts.SizeOf(seed) != GhostRafts.Large) seed++;
				s.Seed = seed;
				string name = CustomIslandSpawner.GeneratedPrefix + GhostRafts.TypeName + "-" + seed;
				IslandFile f = MapTypes.Create(MapTypes.Get(GhostRafts.TypeName), s, 0f, name);
				f.Save(IslandSpawner.PathFor(name));
				// (out to 400 m: the CI world keeps islands of earlier runs, and the two rings within 160 m can all be taken)
				Vector3? spot = CustomIslandSpawner.FindClearSpot(CustomIslandSpawner.RaftPosition.Value, 40f, 400f);
				if (!spot.HasValue) { Fail("deck island: no open sea near the raft"); yield break; }
				yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
				e = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			}
			if (e == null || e.Root == null) { Fail("deck island: not brought / not loaded"); yield break; }
			yield return new WaitForSeconds(3f);
			Log("DECKISLAND " + e.HostName);
			Log("PASS: deck island made (" + kind + ", " + FlatDistance(e.Position, CustomIslandSpawner.RaftPosition.Value).ToString("F0") + " m from Raft's raft)");
		}

		/// <summary>A clear point of the island's deck (nothing above it), or its middle.</summary>
		static Vector3 DeckSpot(IslandWorldState.Entry e)
		{
			foreach (BoxCollider d in e.Root.GetComponentsInChildren<BoxCollider>(true).Where(x => x.name == "CustomIslands_Deck"))
				for (int ix = 0; ix < 5; ix++)
					for (int iz = 0; iz < 5; iz++)
					{
						Bounds b = d.bounds;
						Vector3 top = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (ix + 0.5f) / 5f), b.max.y, Mathf.Lerp(b.min.z, b.max.z, (iz + 0.5f) / 5f));
						RaycastHit hit;
						if (Physics.Raycast(top + Vector3.up * 6f, Vector3.down, out hit, 7f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == d) return top + Vector3.up * 1.2f;
					}
			return e.Position + Vector3.up * 3f;
		}

		/// <summary>Whether the local player stands on the island's deck now, and how far it is from the island's middle.</summary>
		static bool DeckSample(IslandWorldState.Entry e, out float fromMiddle, out string seen)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			fromMiddle = -1f; seen = "no player";
			if (player == null) return false;
			Collider under = player.PersonController.groundRaycastHit.collider;
			fromMiddle = FlatDistance(player.transform.position, e.Position);
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			bool grounded = player.PersonController.IsGrounded;
			seen = (grounded ? "grounded" : "in the air") + " on " + (under != null ? under.name : "nothing") + ", " + fromMiddle.ToString("F1") + " m from the island's middle, y " + player.transform.position.y.ToString("F1") +
				(raft.HasValue ? ", Raft's raft " + FlatDistance(raft.Value, e.Position).ToString("F0") + " m from the island" : "");
			Log("DECK where: " + seen + " (" + (Raft_Network.IsHost ? "host" : "player who joined") + ")");
			return grounded && under != null && under.name == "CustomIslands_Deck" && fromMiddle < 30f;
		}

		/// <summary>The island may not be loaded yet on the player who joined (a trader raft comes ahead of the raft, out of their reach): go above it first.</summary>
		static IEnumerator GhostDeckGoStand(string name)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			IslandWorldState.Entry far = IslandWorldState.Islands.FirstOrDefault(i => (i.HostName ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
			if (far != null && far.Root == null && player != null)
			{
				PlayerMove.To(player, far.Position + Vector3.up * 30f);
				KeepAlive(player);
				yield return WaitFor(() => far.Root != null, 30f);
			}
			IslandWorldState.Entry e = LoadedIsland(new[] { name });
			if (e != null) yield return GhostDeckStand(e);
		}

		static IEnumerator GhostDeckStand(IslandWorldState.Entry e)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("deck stand: no local player"); yield break; }
			yield return PutPlayer(player, DeckSpot(e), false);
			bool stood = false; string seen = "";
			for (int i = 0; i < 8 && !stood; i++)
			{
				yield return new WaitForSeconds(0.25f);
				stood = DeckSample(e, out _, out seen);
			}
			KeepAlive(player);
			if (stood) Log("PASS: deck stand (" + seen + ")"); else Fail("deck stand: " + seen);
		}

		static IEnumerator GhostDeckCheck(IslandWorldState.Entry e, float seconds)
		{
			int total = 0, on = 0;
			string last = "";
			for (float t = 0f; t < seconds; t += 0.5f)
			{
				float d; string seen;
				if (DeckSample(e, out d, out seen)) on++;
				total++; last = seen;
				yield return new WaitForSeconds(0.5f);
			}
			if (on == total && total > 0) Log("PASS: deck stays (on the deck in " + on + " of " + total + " samples over " + seconds.ToString("F0") + " s; " + last + ")");
			else Fail("deck stays: on the deck in " + on + " of " + total + " samples (" + last + ")");
		}

		static IEnumerator GhostDeckAway(float seconds)
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			IslandWorldState.Entry e = IslandWorldState.Islands.Where(i => i.Root != null && ((i.HostName ?? "").Contains(GhostRafts.TypeName) || (i.HostName ?? "").Contains(TraderRaft.TypeName)))
				.OrderBy(i => raft.HasValue ? FlatDistance(i.Position, raft.Value) : 0f).FirstOrDefault();
			if (!Raft_Network.IsHost || !raft.HasValue || e == null) { Fail("deck away: host, with a ghost or trader raft near"); yield break; }
			Vector3 dir = new Vector3(raft.Value.x - e.Position.x, 0f, raft.Value.z - e.Position.z);
			dir = dir.sqrMagnitude < 1f ? Vector3.right : dir.normalized;
			Vector3 target = raft.Value + dir * 300f;
			Log("DECK away: Raft's raft is " + FlatDistance(raft.Value, e.Position).ToString("F0") + " m from '" + e.HostName + "', sailing to " + target.ToString("F0"));
			yield return PushTowards(target, seconds, "away from " + e.HostName);
			raft = CustomIslandSpawner.RaftPosition;
			float away = raft.HasValue ? FlatDistance(raft.Value, e.Position) : -1f;
			Log("DECK away: now " + away.ToString("F0") + " m from the island");
			if (away > 0f) Log("PASS: deck away (" + away.ToString("F0") + " m)"); else Fail("deck away: no raft position");
		}

		#endregion
	}
}
