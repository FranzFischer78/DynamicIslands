using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Tests for the level up system (LevelSystem.cs, LevelWindow.cs).</summary>
	public static partial class DevTests
	{
		const string LevelIsland = "cilevels", LevelMPIsland = "cilevelmp";

		#region Editor

		[ConsoleCommand(name: "CILevelTest", docs: "Dev, editor: the level up system's numbers (EXP per level, a monster's EXP, 2 points a level, +1% a point, 10 at most), records written and read; no Level up switch on the Island tab or in the generator (a world option only), an older preset or island with it passed by")]
		public static void LevelTest()
		{
			StartTest(LevelTestRoutine());
		}

		static IEnumerator LevelTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;

			// The curve: level 2 after 1 kill of Bruce (EXP gained is nine times the measure), level 3 about 2 more, level 4 about 3
			// more, level 5 about 4 more
			int bruce = LevelRules.XpOf(LevelRules.SharkHealth, LevelRules.SharkDamage);
			Check(ref ok, bruce == LevelRules.BruceXp && bruce == 9 * LevelRules.ReferenceXp, "Bruce is worth " + bruce + " EXP (nine times what the levels are measured in)");
			int[] kills = Enumerable.Range(1, 5).Select(l => Mathf.CeilToInt(LevelRules.XpFor(l) / (float)bruce)).ToArray();
			Check(ref ok, kills.SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "kills of Bruce from each level to the next: " + string.Join(", ", kills.Select(k => k.ToString()).ToArray()));
			Check(ref ok, LevelRules.TotalFor(1) == 0 && LevelRules.TotalFor(2) == 100 && LevelRules.TotalFor(3) == 300 && LevelRules.TotalFor(4) == 700 && LevelRules.TotalFor(5) == 1300,
				"EXP at which levels 2-5 start: " + string.Join(", ", Enumerable.Range(2, 4).Select(l => LevelRules.TotalFor(l).ToString()).ToArray()));
			Check(ref ok, LevelRules.LevelOf(0) == 1 && LevelRules.LevelOf(99) == 1 && LevelRules.LevelOf(100) == 2 && LevelRules.LevelOf(299) == 2 && LevelRules.LevelOf(300) == 3 && LevelRules.LevelOf(1300) == 5,
				"levels from EXP at the edges (99 -> 1, 100 -> 2, 299 -> 2, 300 -> 3, 1300 -> 5)");
			Check(ref ok, Mathf.Approximately(LevelRules.Factor(0), 1f) && Mathf.Approximately(LevelRules.Factor(4), 1.04f) && Mathf.Approximately(LevelRules.Factor(15), 1.15f) && Mathf.Approximately(LevelRules.Factor(20), 1.15f),
				"a point is +1%, 15 points at most (+15%)");
			// A monster's EXP: from its health and its damage, both count
			int weak = LevelRules.XpOf(50f, 5f), tough = LevelRules.XpOf(800f, 5f), biting = LevelRules.XpOf(50f, 40f);
			Check(ref ok, weak >= 1 && tough > weak && biting > weak && LevelRules.XpOf(0f, 0f) == 1, "EXP grows with health and with damage (" + weak + " / tougher " + tough + " / biting harder " + biting + "), at least 1");
			Check(ref ok, !LevelRules.IsMonster(AI_NetworkBehaviourType.Chicken) && !LevelRules.IsMonster(AI_NetworkBehaviourType.Turtle) && !LevelRules.IsMonster(AI_NetworkBehaviourType.NPC_Johan) &&
				LevelRules.IsMonster(AI_NetworkBehaviourType.Shark) && LevelRules.IsMonster(AI_NetworkBehaviourType.Boar) && LevelRules.IsMonster(AI_NetworkBehaviourType.MamaBear), "chickens, turtles and people aren't monsters; Bruce, warthogs and bears are");

			// Records
			var r = new LevelRecord { Xp = 750, Kills = 37 };
			r.Points[LevelRules.Jump] = 3; r.Points[LevelRules.Oxygen] = 2;
			LevelRecord back = LevelRecord.Decode(r.Encode());
			Check(ref ok, back.Xp == 750 && back.Kills == 37 && back.Points.SequenceEqual(r.Points) && back.Level == 4 && back.Unspent == 1, "a record written and read back (" + r.Encode() + ": level " + back.Level + ", " + back.Unspent + " point left, 37 monsters)");
			Check(ref ok, LevelRules.StatCount == 9 && LevelRules.StatNames[LevelRules.Thirst] == "Thirst" && LevelRecord.Decode("300|0,0,0,0,0,0,0,1,0").Points[LevelRules.Thirst] == 1, "nine stats, Thirst among them");
			Check(ref ok, LevelRecord.Decode("300|1,0,0,0,0,0,0,0").Kills == 0 && LevelRecord.Decode("300|1,0,0,0,0,0,0,0").Points[0] == 1, "an older record (eight stats, no kills) still reads");
			// All stats full: the levels go on, without points
			Check(ref ok, LevelRules.AllPoints == 135 && LevelRules.PointsAt(69) == 135 && LevelRules.PointsAt(70) == 135 && LevelRules.PointsAt(100) == 135 && LevelRules.PointsAt(68) == 134, "every stat full takes 135 points, at level 69; later levels give none");
			var full = new LevelRecord { Xp = LevelRules.TotalFor(80), Points = Enumerable.Repeat(15, LevelRules.StatCount).ToArray() };
			Check(ref ok, full.Level == 80 && full.Unspent == 0 && full.AllFull, "level 80 with every stat full: nothing to spend");
			var nearly = new LevelRecord { Xp = LevelRules.TotalFor(80), Points = new[] { 15, 15, 15, 15, 15, 15, 15, 15, 0 } };
			Check(ref ok, nearly.Unspent == 15 && !nearly.AllFull, "level 80 with 120 spent: the last 15 points are still there");
			var old = LevelRecord.Decode(LevelRules.TotalFor(60) + "|10,10,10,10,10,10,10,10,10");
			Check(ref ok, old.Spent == 90 && !old.AllFull, "a record from the 10-point cap reads as it was, with room for 5 more in each stat");
			LevelRecord cheat = LevelRecord.Decode("150|10,10,10,0,0,0,0,0");
			Check(ref ok, cheat.Level == 2 && cheat.Spent == 2, "no more points than the level gives (level 2 of a hand-edited file keeps " + cheat.Spent + ")");
			Check(ref ok, LevelRecord.Decode("").Xp == 0 && LevelRecord.Decode("x|y").Xp == 0 && LevelRecord.Decode("-5").Xp == 0, "broken records read as nothing");
			// A world option only (New Game > World settings, the Levels command): no "Level up" switch on the Island tab or in
			// the generator any more; an older island's or preset's value is passed by
			var saved = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
			EditorUI.SetTab(TAB.Island);
			yield return null;
			Check(ref ok, !EditorUI.Canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "LevelUp"), "the Island tab has no Level up switch (a world option only)");
			Screenshot(new[] { "levels_island_tab" });
			yield return new WaitForSecondsRealtime(0.5f);
			GeneratorWindow.Open();
			yield return new WaitForSecondsRealtime(0.3f);
			GameObject gw = GameObject.Find("GeneratorWindow");
			int rows = gw != null ? gw.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Level up") : -1;
			Check(ref ok, gw != null && rows == 0, "the generator has no Level up choice on any tab (" + rows + ")");
			IslandGenSettings gs = GeneratorWindow.Current;
			GeneratorWindow.Close();
			IslandGenSettings older = IslandGenSettings.FromText("Radius=40" + (char)10 + "Levels=True" + (char)10 + "Height=12");
			Check(ref ok, older != null && Mathf.Approximately(older.Radius, 40f) && Mathf.Approximately(older.Height, 12f) && !older.ToText().Contains("Levels"), "an older preset with Levels=True still loads (the line passed by)");
			if (gs != null)
			{
				gs.Radius = 40f; gs.Height = 12f; gs.ObjectDensity = 0.1f; gs.Hostiles = 1; gs.Seed = 777;
				Check(ref ok, !IslandGenerator.CreateFile(gs, "cilevels_gen").Props.ContainsKey(IslandProps.Levels), "a generated island has no Level up rule");
			}
			DynamicIslands.currentIslandProps[IslandProps.Levels] = "on";
			EditorUI.RefreshIsland();
			Check(ref ok, !PlayerLevels.On, "an older island's Level up rule in the editor doesn't turn the system on");
			DynamicIslands.currentIslandProps.Clear();
			foreach (var kv in saved) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
			EditorUI.RefreshIsland();
			Check(ref ok, !PlayerLevels.On, "the level up system is off in the editor");
			if (ok) Log("PASS: level up system (editor)"); else Fail("level up system (editor)");
		}

		#endregion

		#region World

		[ConsoleCommand(name: "CILevelWorld", docs: "Dev, in game (host): an island with warthogs (an older island's level rule doesn't turn the system on; the Levels command does), hits give their share of the monster's EXP (floating over it), a kill all of it, level up, the stats page (K), every stat on Raft's player, saving with the world, the host keeping other players' records")]
		public static void LevelWorld()
		{
			StartTest(LevelWorldRoutine());
		}

		static IEnumerator LevelWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			Network_Player player = RAPI.GetLocalPlayer();
			ulong me = player.steamID.Id;

			// Harmony: Raft's methods the stats work through
			string owner = DynamicIslands.HarmonyId;
			foreach (MethodBase m in new MethodBase[] { AccessTools.Method(typeof(PersonController), "GroundControll"), AccessTools.Method(typeof(PersonController), "WaterControll"),
				AccessTools.PropertyGetter(typeof(Stat_Consumable), "LostPerSecond"), AccessTools.Method(typeof(Stat_Oxygen), "Update"), AccessTools.Method(typeof(Network_Host), "DamageEntity"), AccessTools.Method(typeof(Network_Host_Entities), "Deserialize") })
			{
				Patches info = m != null ? Harmony.GetPatchInfo(m) : null;
				Check(ref ok, info != null && info.Prefixes.Concat(info.Postfixes).Any(p => p.owner == owner && p.PatchMethod.DeclaringType.Name.StartsWith("Level")), "Raft's " + (m != null ? m.DeclaringType.Name + "." + m.Name : "?") + " is patched");
			}

			// An island with the system on and three warthogs that don't bite
			IslandFile f = SampleIsland();
			f.Name = LevelIsland;
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 15f);
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x + 6f, c.y + 4f), Props = P(ObjectProps.CreatureCount, "3", ObjectProps.CreatureDamage, "0") });
			f.Objects.Add(new IslandObject { Name = "Creature_Chicken", Position = ground(c.x - 6f, c.y - 4f), Props = P(ObjectProps.CreatureCount, "1") });
			f.Props[IslandProps.Levels] = "on"; // (an older island's rule: ignored now, the level up system is a world option)
			f.Props[IslandProps.Title] = "Level Isle";
			f.Save(IslandSpawner.PathFor(LevelIsland));

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(LevelIsland), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			var sent = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Levels) sent.Add(m); };
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile(LevelIsland, spot.Value, true);
			IslandNetwork.Loopback = null;
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault(e => e.HostName == LevelIsland);
			if (entry == null || entry.Root == null) { Fail("the level island did not spawn"); yield break; }
			Check(ref ok, !PlayerLevels.On, "an older island with the Level up rule doesn't turn the system on");
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Levels) sent.Add(m); };
			PlayerLevels.SetEnabled(true);
			IslandNetwork.Loopback = null;
			Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Level == 1, "the world option (Levels command) turns the level up system on (level " + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Level : 0) + ")");
			Check(ref ok, sent.Any(m => m.Name == "on"), "the other players are told");
			Check(ref ok, LevelHud.LastAnnounce != null && LevelHud.LastAnnounce.StartsWith("LEVEL UP SYSTEM"), "a banner says so: " + LevelHud.LastAnnounce);

			// Wait for the warthogs; stand next to them
			float t0 = Time.realtimeSinceStartup;
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			while (Time.realtimeSinceStartup - t0 < 40f)
			{
				boars = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null && p.Kind.Type == AI_NetworkBehaviourType.Boar)
					.SelectMany(p => p.Spawned).Where(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead && a.networkEntity.stat_health.Max > 0f).ToList();
				if (boars.Count >= 3) break;
				yield return new WaitForSeconds(0.5f);
			}
			if (boars.Count < 3) { Fail("the warthogs did not come (" + boars.Count + ")"); LevelCleanup(entry); yield break; }
			yield return StandRoutine(entry.Root);
			PutPlayerNear(boars[0].transform, 5f);
			Network_Host host = ComponentManager<Network_Host>.Value;
			float pve = 1f;
			SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
			if (mode != null && mode.playerSpecificVariables != null) pve = mode.playerSpecificVariables.negateOutgoingPlayerDamage ? 0f : mode.playerSpecificVariables.outgoingDamageMultiplierPVE;
			Log("  warthog: health " + boars[0].networkEntity.stat_health.Max + ", damage " + PlayerLevels.DamageOf(boars[0].gameObject, boars[0].behaviourType) + ", worth " + PlayerLevels.MonsterXp(boars[0]) + " EXP; PvE damage x" + pve);
			if (pve <= 0f) { Fail("this world's game mode takes away players' damage"); LevelCleanup(entry); yield break; }

			// Hits: each gives its share of the warthog's EXP, and the kill all of the rest
			AI_NetworkBehaviour boar = boars[0];
			int worth = PlayerLevels.MonsterXp(boar);
			float max = boar.networkEntity.stat_health.Max;
			int xp0 = PlayerLevels.Mine.Xp, kills0 = PlayerLevels.Mine.Kills;
			var gains = new List<int>();
			for (int i = 0; i < 6 && !boar.networkEntity.IsDead; i++)
			{
				if (i == 0)
				{
					// (the player looks at the warthog, so the picture shows the EXP floating over it)
					Vector3 eye = Camera.main != null ? Camera.main.transform.position : player.transform.position + Vector3.up * 1.6f;
					Vector3 to = boar.transform.position + Vector3.up * 0.8f - eye;
					Look(player, Quaternion.LookRotation(new Vector3(to.x, 0f, to.z)).eulerAngles.y, Mathf.Atan2(-to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg);
					yield return new WaitForSeconds(0.3f);
				}
				int x = PlayerLevels.Mine.Xp;
				host.DamageEntity(boar.networkEntity, boar.transform, max * 0.3f / pve, boar.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
				gains.Add(PlayerLevels.Mine.Xp - x);
				if (i == 0)
				{
					Check(ref ok, PlayerLevels.LastGain == gains[0] && LevelHud.LastFloat == "+" + gains[0] + " EXP" && LevelHud.Floating.Contains(LevelHud.LastFloat), "a hit floats its EXP over the warthog (" + LevelHud.LastFloat + ")");
					Check(ref ok, (PlayerLevels.LastGainAt - boar.transform.position).y > 0.2f && FlatDistance(PlayerLevels.LastGainAt, boar.transform.position) < 3f, "above the warthog (" + (PlayerLevels.LastGainAt - boar.transform.position).y.ToString("F1") + " m up)");
					Check(ref ok, LevelHud.BarShown, "the level bar shows");
					yield return new WaitForSeconds(0.3f);
					Screenshot(new[] { "levels_hit" });
				}
				yield return new WaitForSeconds(0.2f);
			}
			Check(ref ok, boar.networkEntity.IsDead, "four hits of 30% kill it");
			Check(ref ok, PlayerLevels.Mine.Xp - xp0 == worth && gains.All(g => g >= 0), "the hits give the warthog's EXP, no more (" + string.Join(" + ", gains.Select(g => g.ToString()).ToArray()) + " = " + (PlayerLevels.Mine.Xp - xp0) + " of " + worth + ")");
			int afterKill = PlayerLevels.Mine.Xp;
			host.DamageEntity(boar.networkEntity, boar.transform, 50f, boar.transform.position, Vector3.up, EntityType.Player, null);
			Check(ref ok, PlayerLevels.Mine.Xp == afterKill, "a dead warthog gives nothing more");
			Check(ref ok, PlayerLevels.Mine.Kills == kills0 + 1, "the kill is counted (" + PlayerLevels.Mine.Kills + " monster(s) defeated), once");

			// A chicken isn't a monster
			AI_NetworkBehaviour chicken = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null && p.Kind.Type == AI_NetworkBehaviourType.Chicken).SelectMany(p => p.Spawned).FirstOrDefault(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead);
			if (chicken != null)
			{
				int x = PlayerLevels.Mine.Xp;
				host.DamageEntity(chicken.networkEntity, chicken.transform, 1f, chicken.transform.position, Vector3.up, EntityType.Player, null);
				Check(ref ok, PlayerLevels.Mine.Xp == x, "hitting a chicken gives no EXP");
			}
			else Log("  (no chicken came: not checked)");
			// A monster's hit on the player is left alone
			host.DamageEntity(player.Stats, player.transform, 5f, player.transform.position, Vector3.up, EntityType.Enemy, null);
			Check(ref ok, PlayerLevels.Mine.Xp == afterKill, "being bitten gives no EXP");
			KeepAlive(player);

			// Another player's hits: the host works out their EXP (it has the monster as built) and sends it back to them.
			// The host hits a warthog for 30%, "player 4242" (Raft's damage message from them) for the rest and kills it.
			AI_NetworkBehaviour shared = boars[2];
			int sharedWorth = PlayerLevels.MonsterXp(shared);
			float sharedMax = shared.networkEntity.stat_health.Max;
			var gainMsgs = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Levels && m.Name == "gain") gainMsgs.Add(m); };
			int hostBefore = PlayerLevels.Mine.Xp, hostKills = PlayerLevels.Mine.Kills;
			host.DamageEntity(shared.networkEntity, shared.transform, sharedMax * 0.3f / pve, shared.transform.position, Vector3.up, EntityType.Player, null);
			int hostShare = PlayerLevels.Mine.Xp - hostBefore;
			for (int i = 0; i < 3 && !shared.networkEntity.IsDead; i++)
			{
				float hit = sharedMax * 0.3f;
				PlayerLevels.OnRemoteHit(shared.networkEntity, hit, 4242UL); // (as LevelRemoteHitPatch does, just before Raft applies it)
				shared.networkEntity.Damage(hit, shared.transform.position, Vector3.up, EntityType.Player, false);
				yield return new WaitForSeconds(0.2f);
			}
			IslandNetwork.Loopback = null;
			int remoteShare = gainMsgs.Sum(m => m.Count);
			Check(ref ok, shared.networkEntity.IsDead && gainMsgs.Count > 0 && gainMsgs.Last().FullList && !gainMsgs.Take(gainMsgs.Count - 1).Any(m => m.FullList) && gainMsgs.All(m => m.Index == unchecked((int)shared.networkEntity.ObjectIndex)),
				"another player's hits: the host sends them their EXP, the last one as the kill (" + string.Join(" + ", gainMsgs.Select(m => m.Count + (m.FullList ? " kill" : "")).ToArray()) + ")");
			Check(ref ok, hostShare > 0 && remoteShare > 0 && hostShare + remoteShare <= sharedWorth + 1 && hostShare + remoteShare >= sharedWorth - 1,
				"one warthog, two players: " + hostShare + " for the host + " + remoteShare + " for the other = " + (hostShare + remoteShare) + " of " + sharedWorth);
			Check(ref ok, PlayerLevels.Mine.Xp == hostBefore + hostShare && PlayerLevels.Mine.Kills == hostKills, "the other player's hits and kill give the host nothing");

			// The Damage stat: a hit takes 10% more with 10 points
			AI_NetworkBehaviour second = boars[1];
			PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(6), Points = new[] { 0, 0, 0, 0, 10, 0, 0, 0, 0 } });
			float hp0 = second.networkEntity.stat_health.Value;
			host.DamageEntity(second.networkEntity, second.transform, 10f / pve, second.transform.position, Vector3.up, EntityType.Player, null);
			float took = hp0 - second.networkEntity.stat_health.Value;
			Check(ref ok, Mathf.Abs(took - 11f) < 0.05f, "with 10 points in Damage a hit of 10 takes " + took.ToString("F2"));
			PlayerLevels.SetMine(new LevelRecord());

			// Level up: 99 EXP, then a hit's worth more
			PlayerLevels.SetMine(new LevelRecord { Xp = 99 });
			PlayerLevels.GiveXp(1, player.transform.position + player.transform.forward * 3f);
			Check(ref ok, PlayerLevels.Mine.Level == 2 && PlayerLevels.Mine.Unspent == 2, "100 EXP is level 2 with 2 stat points");
			Check(ref ok, LevelHud.LastAnnounce != null && LevelHud.LastAnnounce.StartsWith("LEVEL 2!") && LevelHud.LastAnnounce.Contains("Click here"), "a box: " + LevelHud.LastAnnounce);
			yield return new WaitForSeconds(1.2f);
			Screenshot(new[] { "levels_levelup" });
			yield return new WaitForSeconds(0.6f); // (the picture lands a few frames later; clicking the box below hides it)
			// The level bar next to Raft's health, thirst and hunger bars
			Check(ref ok, LevelHud.BarShown && LevelHud.BarText != null && LevelHud.BarText.StartsWith("LV 2") && LevelHud.BarPlace != null && LevelHud.BarPlace.Contains("Raft's 3 stat bars"),
				"the level bar sits by Raft's stat bars: \"" + LevelHud.BarText + "\", " + LevelHud.BarPlace);
			Check(ref ok, LevelHud.BarStyle != null && LevelHud.BarStyle.StartsWith("a copy of Raft's") && LevelHud.BarIcon != null && LevelHud.BarIcon.name == "CustomIslands_StatIcon_Level",
				"it is styled as Raft's own bars (" + LevelHud.BarStyle + "), with the level's star icon (" + (LevelHud.BarIcon != null ? LevelHud.BarIcon.name : "none") + ")");
			// The level up box opens the stats page when clicked (the mouse is free in a menu)
			Button box = LevelHud.BannerButton;
			Check(ref ok, box != null && box.interactable, "the level up box can be clicked");
			if (box != null) box.onClick.Invoke();
			for (int i = 0; i < 5 && !LevelWindow.IsOpen; i++) yield return null;
			Check(ref ok, LevelWindow.IsOpen, "clicking it opens the stats page");
			LevelWindow.Close();
			yield return null;
			// Raft's inventory (Tab) shows a Stats button by the level bar; it closes the inventory and opens the page
			CanvasHelper canvasHelper = ComponentManager<CanvasHelper>.Value ?? UnityEngine.Object.FindObjectOfType<CanvasHelper>();
			Check(ref ok, LevelHud.StatsButton != null && !LevelHud.StatsButton.gameObject.activeInHierarchy, "no Stats button while playing");
			if (canvasHelper != null) canvasHelper.OpenMenuCloseOther(MenuType.Inventory, true);
			for (int i = 0; i < 10 && (LevelHud.StatsButton == null || !LevelHud.StatsButton.gameObject.activeInHierarchy); i++) yield return null;
			Check(ref ok, CanvasHelper.ActiveMenu == MenuType.Inventory && LevelHud.StatsButton != null && LevelHud.StatsButton.gameObject.activeInHierarchy, "Raft's inventory open: the Stats button shows");
			yield return new WaitForSeconds(0.3f);
			Screenshot(new[] { "levels_inventory" });
			yield return new WaitForSeconds(0.5f);
			if (LevelHud.StatsButton != null) LevelHud.StatsButton.onClick.Invoke();
			for (int i = 0; i < 10 && !LevelWindow.IsOpen; i++) yield return null;
			// (with the page open RML's ToggleCursor marks "PauseMenu" as the active menu, to keep the game from taking the keys: no menu shows)
			Check(ref ok, LevelWindow.IsOpen && CanvasHelper.ActiveMenu != MenuType.Inventory, "the Stats button closes the inventory and opens the page (page " + (LevelWindow.IsOpen ? "open" : "closed") + ", Raft's menu " + CanvasHelper.ActiveMenu + ")");
			LevelWindow.Close();
			yield return null;

			// The stats page
			yield return new WaitForSeconds(0.4f);
			LevelWindow.Open();
			yield return null;
			Check(ref ok, LevelWindow.IsOpen && LevelWindow.Shown.StartsWith("LEVEL 2 |") && LevelWindow.Shown.Contains("2</color> stat point"), "the stats page opens: " + LevelWindow.Shown);
			Check(ref ok, LevelWindow.Click(LevelRules.Walk, true) && PlayerLevels.Mine.Points[LevelRules.Walk] == 1 && PlayerLevels.Mine.Unspent == 1, "+ puts a point into Walk speed");
			Check(ref ok, LevelWindow.Shown.Contains("Walk speed +1%"), "the page shows it: " + LevelWindow.Shown);
			Check(ref ok, LevelWindow.Click(LevelRules.Walk, false) && PlayerLevels.Mine.Points[LevelRules.Walk] == 0 && PlayerLevels.Mine.Unspent == 2, "- takes it back while the page is open");
			Check(ref ok, !LevelWindow.Click(LevelRules.Walk, false), "nothing to take back below what it had");
			LevelWindow.Click(LevelRules.Health, true);
			LevelWindow.Click(LevelRules.Oxygen, true);
			Check(ref ok, PlayerLevels.Mine.Unspent == 0 && !LevelWindow.Click(LevelRules.Swim, true), "no point left: + does nothing");
			yield return null;
			Screenshot(new[] { "levels_page" });
			yield return new WaitForSeconds(0.5f);
			LevelWindow.Close();
			LevelWindow.Open();
			Check(ref ok, !LevelWindow.Click(LevelRules.Health, false), "closed and opened again: the points stay");
			LevelWindow.Close();
			Check(ref ok, !LevelWindow.IsOpen, "the page closes");

			// At most 15 points in a stat
			PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(15) });
			for (int i = 0; i < 15; i++) PlayerLevels.Spend(LevelRules.Jump);
			Check(ref ok, PlayerLevels.Mine.Points[LevelRules.Jump] == 15 && !PlayerLevels.Spend(LevelRules.Jump) && PlayerLevels.Mine.Unspent == 13, "15 points in Jump height, not 16 (" + PlayerLevels.Mine.Unspent + " left for the others)");

			// Every stat full: still levelling up, without points
			PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(70) - 1, Points = Enumerable.Repeat(LevelRules.MaxPoints, LevelRules.StatCount).ToArray(), Kills = 900 });
			Check(ref ok, PlayerLevels.Mine.Level == 69 && PlayerLevels.Mine.Unspent == 0 && PlayerLevels.Mine.AllFull, "level 69 with every stat full");
			PlayerLevels.GiveXp(1, null);
			Check(ref ok, PlayerLevels.Mine.Level == 70 && PlayerLevels.Mine.Unspent == 0 && LevelHud.LastAnnounce.StartsWith("LEVEL 70!") && LevelHud.LastAnnounce.Contains("no more points"),
				"level 70 still comes, with no points: " + LevelHud.LastAnnounce);
			LevelWindow.Open();
			yield return null;
			Check(ref ok, LevelWindow.Shown.StartsWith("LEVEL 70 | Every stat is full") && LevelWindow.Shown.Contains("Monsters defeated: 900") && LevelWindow.Shown.Contains("Thirst +15% max"), "the page says so, with the monsters defeated: " + LevelWindow.Shown);
			Screenshot(new[] { "levels_page_full" });
			yield return new WaitForSeconds(0.5f);
			LevelWindow.Close();

			// Every stat on Raft's player
			PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(69), Points = Enumerable.Repeat(LevelRules.MaxPoints, LevelRules.StatCount).ToArray() });
			PersonController pc = player.PersonController;
			float walk = pc.normalSpeed, run = pc.sprintSpeed, swim = pc.swimSpeed, jump = pc.jumpSpeed;
			StatApply.Saved sv = StatApply.Boost(pc);
			Check(ref ok, Near(pc.normalSpeed, walk * 1.15f) && Near(pc.sprintSpeed, run * 1.15f) && Near(pc.swimSpeed, swim * 1.15f) && Near(pc.jumpSpeed * pc.jumpSpeed, jump * jump * 1.15f),
				"while Raft moves the player: walk " + walk.ToString("F2") + " -> " + pc.normalSpeed.ToString("F2") + ", run " + run.ToString("F2") + " -> " + pc.sprintSpeed.ToString("F2") + ", swim " + swim.ToString("F2") + " -> " + pc.swimSpeed.ToString("F2") +
				", jump " + jump.ToString("F2") + " -> " + pc.jumpSpeed.ToString("F2") + " m/s (15% higher)");
			StatApply.Restore(pc, sv);
			Check(ref ok, pc.normalSpeed == walk && pc.sprintSpeed == run && pc.swimSpeed == swim && pc.jumpSpeed == jump, "and Raft's own numbers after (Raft's flippers still change them as usual)");
			yield return new WaitForSeconds(0.6f);
			Stat_Health health = player.Stats.stat_health;
			float baseMax = StatApply.HealthBase;
			Check(ref ok, baseMax > 0f && Near(health.Max, baseMax * 1.15f), "maximum health " + baseMax.ToString("F0") + " -> " + health.Max.ToString("F1"));
			Stat_Consumable hunger = player.Stats.stat_hunger.normalConsumable;
			float hungerLost = hunger.LostPerSecond;
			PlayerLevels.Mine.Points[LevelRules.Hunger] = 0;
			float hungerRaft = hunger.LostPerSecond;
			Check(ref ok, hungerRaft > 0f && Near(hungerLost, hungerRaft / 1.15f), "hunger drains " + hungerRaft.ToString("F4") + " -> " + hungerLost.ToString("F4") + " a second (lasts 15% longer)");
			Stat_Consumable thirst = player.Stats.stat_thirst.normalConsumable;
			float thirstLost = thirst.LostPerSecond;
			PlayerLevels.Mine.Points[LevelRules.Thirst] = 0;
			float thirstRaft = thirst.LostPerSecond;
			Check(ref ok, thirstRaft > 0f && Near(thirstLost, thirstRaft / 1.15f), "thirst drains " + thirstRaft.ToString("F4") + " -> " + thirstLost.ToString("F4") + " a second (lasts 15% longer)");
			PlayerLevels.SetMine(new LevelRecord());
			yield return new WaitForSeconds(0.6f);
			Check(ref ok, Near(health.Max, baseMax) && health.Value <= baseMax, "no points: Raft's health again (" + health.Max.ToString("F0") + ")");
			Check(ref ok, Near(PlayerLevels.Factor(LevelRules.Oxygen), 1f), "no points: breath as Raft's");

			// Kept with the world; the host keeps the other players' records and gives them back
			PlayerLevels.SetMine(new LevelRecord { Xp = 345, Points = new[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, Kills = 12 });
			PlayerLevels.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "mine", Data = "1234|0,0,1,1,1,0,0,0,0|3" }, new Network_UserId(4242UL));
			LevelRecord guest = PlayerLevels.RecordOf(4242UL);
			Check(ref ok, guest != null && guest.Xp == 1234 && guest.Points[LevelRules.Damage] == 1 && guest.Kills == 3, "the host keeps another player's record");
			// An older message from that player (sent before EXP they earned since) arrives late: left out (EXP only grows)
			PlayerLevels.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "mine", Data = "900|0,0,0,0,0,0,0,0,0|1" }, new Network_UserId(4242UL));
			guest = PlayerLevels.RecordOf(4242UL);
			Check(ref ok, guest != null && guest.Xp == 1234 && guest.Kills == 3, "a late, older record from that player doesn't take their progress away (" + (guest != null ? guest.Xp.ToString() : "none") + " EXP)");
			// Everyone's level goes to everyone (for the "Lv n" under each other player's name)
			var levelMsgs = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Levels && m.Name == "levels") levelMsgs.Add(m); };
			yield return new WaitForSeconds(1.5f);
			IslandNetwork.Loopback = null;
			string list = levelMsgs.Count > 0 ? levelMsgs.Last().Data : "";
			Check(ref ok, list.Contains("4242=" + guest.Level) && list.Contains(me + "=" + PlayerLevels.Mine.Level) && PlayerLevels.LevelOf(4242UL) == guest.Level,
				"every player's level goes to everyone: " + list);
			IslandNetMessage back = PlayerLevels.StateFor(4242UL);
			Check(ref ok, back != null && back.Name == "state" && back.Count == 1 && back.Data == guest.Encode(), "and gives it back when they join");
			List<string> lines = PlayerLevels.WriteLines().ToList();
			Check(ref ok, lines.Contains("@levels=on") && lines.Contains("@level=" + me + "|345|1,0,0,1,0,0,0,0,0|12") && lines.Contains("@level=4242|1234|0,0,1,1,1,0,0,0,0|3"), "the world's lines: " + string.Join("  ", lines.ToArray()));
			IslandWorldState.Save();
			string worldFile = Directory.GetFiles(Path.Combine(DynamicIslands.assetpath, "worlds"), SaveAndLoad.WorldGuid + ".txt").FirstOrDefault();
			string[] saved = worldFile != null ? File.ReadAllLines(worldFile) : new string[0];
			Check(ref ok, saved.Contains("@levels=on") && saved.Contains("@level=" + me + "|345|1,0,0,1,0,0,0,0,0|12"), "saved in the world's island file");
			PlayerLevels.Reset();
			foreach (string line in lines) { int eq = line.IndexOf('='); PlayerLevels.ReadLine(line.Substring(1, eq - 1), line.Substring(eq + 1)); }
			Check(ref ok, PlayerLevels.On && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == 345 && PlayerLevels.Mine.Points[LevelRules.Jump] == 1 && PlayerLevels.RecordOf(4242UL) != null, "read back: on, level " + (PlayerLevels.Mine != null ? PlayerLevels.Mine.Level : 0));

			// K opens the page (Raft's own input, as a player presses it: the keys test does it with a real key)
			LevelCleanup(entry);
			IslandWorldState.Save();
			saved = worldFile != null && File.Exists(worldFile) ? File.ReadAllLines(worldFile) : new string[0];
			Check(ref ok, !PlayerLevels.On && !saved.Any(l => l.StartsWith("@level")), "turned off again for the test world");
			if (ok) Log("PASS: level up system in a world"); else Fail("level up system in a world");
		}

		static bool Near(float a, float b) { return Mathf.Abs(a - b) <= 0.002f * Mathf.Max(1f, Mathf.Abs(b)); }

		[ConsoleCommand(name: "CILevelLateState", docs: "Dev, in game (player 2, the level up system on), IL29: the host's \"state\" reply arriving late - one with less EXP than this player has keeps theirs and tells the host; one with more is taken; one with the same EXP keeps the points spent since")]
		public static void LevelLateState()
		{
			StartTest(LevelLateStateRoutine());
		}

		static IEnumerator LevelLateStateRoutine()
		{
			if (Raft_Network.IsHost || !PlayerLevels.On || PlayerLevels.Mine == null) { Fail("late state: needs player 2 in a world with the level up system on"); yield break; }
			bool ok = true;
			LevelRecord start = LevelRecord.Decode(PlayerLevels.Mine.Encode());
			var sent = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Levels && m.Name == "mine") sent.Add(m); };
			try
			{
				// EXP earned right after joining (the host's reply to the join is still on its way)
				PlayerLevels.SetMine(new LevelRecord { Xp = start.Xp + 500, Points = (int[])start.Points.Clone(), Kills = start.Kills + 2 });
			}
			catch (Exception e) { IslandNetwork.Loopback = null; Fail("late state: " + e.Message); yield break; }
			yield return new WaitForSeconds(1.5f);
			sent.Clear();
			// The late reply: the record the host had before that EXP
			PlayerLevels.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "state", Count = 1, Data = start.Encode() }, new Network_UserId(0UL));
			Check(ref ok, PlayerLevels.Mine.Xp == start.Xp + 500 && PlayerLevels.Mine.Kills == start.Kills + 2, "a late reply with less EXP keeps this player's (" + PlayerLevels.Mine.Xp + " EXP, was " + start.Xp + " in the reply)");
			yield return new WaitForSeconds(1.5f);
			IslandNetMessage told = sent.LastOrDefault();
			Check(ref ok, told != null && LevelRecord.Decode(told.Data).Xp == start.Xp + 500, "and tells the host its record (" + (told != null ? told.Data : "nothing sent") + ")");
			// A reply with more EXP (the host knows of EXP this player's record missed): taken
			int[] morePoints = (int[])start.Points.Clone();
			PlayerLevels.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "state", Count = 1, Data = new LevelRecord { Xp = start.Xp + 900, Points = morePoints, Kills = start.Kills + 5 }.Encode() }, new Network_UserId(0UL));
			Check(ref ok, PlayerLevels.Mine.Xp == start.Xp + 900 && PlayerLevels.Mine.Kills == start.Kills + 5, "a reply with more EXP is taken (" + PlayerLevels.Mine.Xp + ")");
			// The same EXP again after a point was spent here: the point stays
			int[] spent = (int[])PlayerLevels.Mine.Points.Clone(); spent[LevelRules.Health]++;
			PlayerLevels.SetMine(new LevelRecord { Xp = PlayerLevels.Mine.Xp, Points = spent, Kills = PlayerLevels.Mine.Kills });
			PlayerLevels.OnMessage(new IslandNetMessage { Kind = IslandNetMessage.Levels, Name = "state", Count = 1, Data = new LevelRecord { Xp = start.Xp + 900, Points = morePoints, Kills = start.Kills + 5 }.Encode() }, new Network_UserId(0UL));
			Check(ref ok, PlayerLevels.Mine.Points[LevelRules.Health] == spent[LevelRules.Health], "a reply with the same EXP keeps the point spent since");
			IslandNetwork.Loopback = null;
			// (the host gets the newest record: its own copy only grows)
			yield return new WaitForSeconds(1.5f);
			if (ok) Log("PASS: late state reply"); else Fail("late state reply");
		}

		static void LevelCleanup(IslandWorldState.Entry entry)
		{
			IslandNetwork.Loopback = null;
			LevelWindow.Close();
			PlayerLevels.TurnOff();
			if (entry != null) IslandWorldState.RemoveIds(new[] { entry.Id }, true);
			if (File.Exists(IslandSpawner.PathFor(LevelIsland))) File.Delete(IslandSpawner.PathFor(LevelIsland));
		}

		[ConsoleCommand(name: "CILevelTable", docs: "Dev, in game: every monster Raft has - its health, damage and EXP (Bruce first), and how many kills of it make level 2; checks Bruce is worth " + "120" + " EXP (six times the 20 the levels are measured in)")]
		public static void LevelTable()
		{
			Network_Host_Entities host = ComponentManager<Network_Host_Entities>.Value;
			if (host == null) { Fail("run in a world"); return; }
			bool ok = true;
			var rows = new List<string>();
			float sharkHp = -1f, sharkDmg = -1f;
			foreach (AI_NetworkBehavior_Shark s in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehavior_Shark>())
			{
				if (s == null || s.networkEntity == null || s.networkEntity.stat_health == null) continue;
				sharkHp = s.networkEntity.stat_health.Max;
				sharkDmg = PlayerLevels.DamageOf(s.gameObject, s.behaviourType);
				rows.Add(string.Format("  Shark (Bruce, in the sea): health {0:F0}, damage {1:F0}, {2} EXP", sharkHp, sharkDmg, LevelRules.XpOf(sharkHp, sharkDmg)));
				break;
			}
			foreach (AI_NetworkBehaviour a in host.AINetworkBehaviourPrefabs ?? new AI_NetworkBehaviour[0])
			{
				if (a == null) continue;
				Network_Entity ne = a.networkEntity != null ? a.networkEntity : a.GetComponentInChildren<Network_Entity>(true);
				// (a prefab's maximum is set only in Awake, from its serialized maxValue)
				float hp = ne != null && ne.stat_health != null ? ne.stat_health.Max : 0f, dmg = PlayerLevels.DamageOf(a.gameObject, a.behaviourType);
				if (hp <= 0f && ne != null && ne.stat_health != null) { FieldInfo mv = AccessTools.Field(typeof(Stat), "maxValue"); if (mv != null) hp = (float)mv.GetValue(ne.stat_health); }
				int xp = LevelRules.XpOf(hp, dmg);
				rows.Add(string.Format("  {0}: health {1:F0}, damage {2:F0}, {3}", a.behaviourType, hp, dmg, LevelRules.IsMonster(a.behaviourType) ? xp + " EXP, " + Mathf.CeilToInt(LevelRules.XpFor(1) / (float)xp) + " kills to level 2" : "not a monster"));
				if (a.behaviourType == AI_NetworkBehaviourType.Shark && sharkHp < 0f) { sharkHp = hp; sharkDmg = dmg; }
			}
			foreach (string r in rows) Log(r);
			Check(ref ok, sharkHp > 0f, "Bruce found (health " + sharkHp + ", damage " + sharkDmg + "; the rules use " + LevelRules.SharkHealth + " / " + LevelRules.SharkDamage + ")");
			if (sharkHp > 0f) Check(ref ok, Math.Abs(LevelRules.XpOf(sharkHp, sharkDmg) - LevelRules.BruceXp) <= 1, "Bruce is worth " + LevelRules.XpOf(sharkHp, sharkDmg) + " EXP (1 kill for level 2)");
			if (ok) Log("PASS: monster EXP table"); else Fail("monster EXP table");
		}

		#endregion

		#region Two players

		[ConsoleCommand(name: "CILevelMP", docs: "Dev, in game (host): spawns and keeps 'cilevelmp' 150 m ahead, an island with the level up system and harmless warthogs, for the two-player test (either player then CIHit cilevelmp Warthog, CILevelInfo)")]
		public static void LevelMP()
		{
			StartTest(LevelMPRoutine());
		}

		static IEnumerator LevelMPRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandFile f = SampleIsland();
			f.Name = LevelMPIsland;
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 15f);
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = new Vector3(c.x + 6f, f.Heights[Mathf.RoundToInt((c.y + 4f) / step), Mathf.RoundToInt((c.x + 6f) / step)] * f.TerrainSize.y, c.y + 4f), Props = P(ObjectProps.CreatureCount, "4", ObjectProps.CreatureDamage, "0") });
			PlayerLevels.SetEnabled(true); // (a world option: islands don't turn it on)
			f.Save(IslandSpawner.PathFor(LevelMPIsland));
			IslandWorldState.Entry old = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == LevelMPIsland);
			if (old != null) IslandWorldState.RemoveIds(new[] { old.Id }, true);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(LevelMPIsland), 150f);
			// (a new world starts among Raft's first islands: the raft 600 m on, as the scenario tests move it - K and Tab, and a
			// newcomer's level, failed with the level up system never on)
			for (int move = 0; move < 3 && !spot.HasValue; move++)
			{
				Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
				if (raft == null || raft.body == null) break;
				Vector3 dir = Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward;
				raft.body.position = raft.body.position + dir * 600f;
				raft.body.velocity = Vector3.zero;
				Physics.SyncTransforms();
				OnRaftCommand();
				Log("  (no open sea for '" + LevelMPIsland + "' near the raft: the raft moved 600 m on)");
				yield return new WaitForSeconds(3f);
				raftPos = CustomIslandSpawner.RaftPosition;
				if (raftPos.HasValue) spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(LevelMPIsland), 150f);
			}
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(LevelMPIsland, spot.Value, true);
			if (PlayerLevels.On) Log("PASS: '" + LevelMPIsland + "' spawned, the level up system is on");
			else Fail("'" + LevelMPIsland + "' did not turn the level up system on");
		}

		[ConsoleCommand(name: "CIWeaponsDifficulty", docs: "Dev, in game (host): Raft's own spear swings (MeleeWeapon.OnHitEntity with a real raycast hit) kill a level island warthog at Nightmare, Timid and Normal - each swing takes the spear's damage divided by the level's factor, so Nightmare takes about twice Normal's swings and Timid 0.75 of them, and Raft's health bar (health / max) shows that fraction after the first swing. The difficulty and the record are put back after (IW1)")]
		public static void WeaponsDifficultyCommand() { StartTest(WeaponsDifficultyRoutine()); }

		static IEnumerator WeaponsDifficultyRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			if (!raftPos.HasValue || !Raft_Network.IsHost || player == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			int difficulty0 = MonsterDifficulty.Current;
			MeleeWeapon[] melee = player.GetComponentsInChildren<MeleeWeapon>(true).Where(w => w != null && Traverse.Create(w).Field("damage").GetValue<int>() > 0f).ToArray();
			MeleeWeapon spear = melee.FirstOrDefault(w => w.name.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0) ?? melee.FirstOrDefault();
			if (spear == null) { Fail("no melee weapon in the player's hands"); yield break; }
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 3, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			float pve = 1f;
			SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
			if (mode != null && mode.playerSpecificVariables != null) pve = mode.playerSpecificVariables.negateOutgoingPlayerDamage ? 0f : mode.playerSpecificVariables.outgoingDamageMultiplierPVE;
			if (pve <= 0f) { Fail("this world's game mode takes away players' damage"); LevelCleanup(entry); yield break; }
			Network_Host host = ComponentManager<Network_Host>.Value;
			Traverse tw = Traverse.Create(spear);
			if (tw.Field("hostNetwork").GetValue() == null) tw.Field("hostNetwork").SetValue(host);
			if (tw.Field("playerNetwork").GetValue() == null) tw.Field("playerNetwork").SetValue(player);
			float dmg = tw.Field("damage").GetValue<int>();
			MethodInfo onHit = AccessTools.Method(typeof(MeleeWeapon), "OnHitEntity");
			int[] levels = { MonsterDifficulty.Normal, MonsterDifficulty.Nightmare, MonsterDifficulty.Timid };
			int[] swings = new int[levels.Length];
			try
			{
				for (int k = 0; k < levels.Length; k++)
				{
					MonsterDifficulty.Current = levels[k];
					PlayerLevels.SetMine(new LevelRecord());
					AI_NetworkBehaviour boar = boars[k];
					string name = MonsterDifficulty.Name(levels[k]);
					float max = boar.networkEntity.stat_health.Max, per = dmg * pve / MonsterDifficulty.Factor;
					int want = Mathf.CeilToInt(max / per - 0.001f), n = 0;
					float firstFraction = -1f;
					for (int i = 0; i < want + 10 && !boar.networkEntity.IsDead; i++)
					{
						PutPlayerNear(boar.transform, 4f);
						yield return new WaitForSeconds(0.15f);
						RaycastHit hit;
						Collider col = boar.GetComponentsInChildren<Collider>().FirstOrDefault(x => x.enabled && !x.isTrigger);
						Vector3 aim = col != null ? col.bounds.center : boar.transform.position + Vector3.up * 0.5f;
						hit = default(RaycastHit); if (col == null || !col.Raycast(new Ray(aim + Vector3.up * 3f, Vector3.down), out hit, 6f)) // (its own collider only: another warthog's bones stood in the way)
						{ Check(ref ok, false, name + ": no raycast hit on the warthog" + (hit.transform != null ? " (hit " + hit.transform.name + ")" : "")); break; }
						try { onHit.Invoke(spear, new object[] { hit, boar.networkEntity }); }
						catch (Exception e) { if (n == 0) Log("  (" + spear.name + " after the hit: " + (e.InnerException ?? e).Message + ")"); }
						n++;
						yield return new WaitForSeconds(0.1f);
						if (firstFraction < 0f) firstFraction = boar.networkEntity.stat_health.Value / max;
					}
					swings[k] = n;
					float wantFraction = Mathf.Max(0f, 1f - per / max);
					Check(ref ok, boar.networkEntity.IsDead && n == want, name + ": " + spear.name + " (" + dmg.ToString("F0") + " damage) kills a warthog of " + max.ToString("F0") + " health in " + n + " swings (want " + want + ")");
					Check(ref ok, Mathf.Abs(firstFraction - wantFraction) < 0.01f, name + ": the health bar after the first swing shows " + (firstFraction * 100f).ToString("F1") + "% (want " + (wantFraction * 100f).ToString("F1") + "%)");
				}
				if (swings[0] > 0)
				{
					float night = (float)swings[1] / swings[0], timid = (float)swings[2] / swings[0];
					Check(ref ok, Mathf.Abs(swings[1] - 2f * swings[0]) <= 1f && Mathf.Abs(swings[2] - 0.75f * swings[0]) <= 1f, "Nightmare takes " + night.ToString("F2") + "x Normal's swings (about 2), Timid " + timid.ToString("F2") + "x (about 0.75)");
				}
			}
			finally
			{
				MonsterDifficulty.Current = difficulty0;
				PlayerLevels.SetMine(new LevelRecord());
			}
			LevelCleanup(entry);
			if (ok) Log("PASS: weapons difficulty"); else Fail("weapons difficulty");
		}

		[ConsoleCommand(name: "CILevelBruce", docs: "Dev, in game (host): Bruce in the sea - the real shark hit with Raft's own spear swings (MeleeWeapon.OnHitEntity on a raycast hit) until he dies gives exactly his EXP (Bruce's, at Normal) and one kill; his corpse hit again gives nothing, nor does the next shark coming back until it is hit (IL2)")]
		public static void LevelBruceCommand() { StartTest(LevelBruceRoutine()); }

		static IEnumerator LevelBruceRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || player == null || host == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			int difficulty0 = MonsterDifficulty.Current;
			MonsterDifficulty.Current = MonsterDifficulty.Normal;
			MeleeWeapon spear = player.GetComponentsInChildren<MeleeWeapon>(true).Where(w => w != null && Traverse.Create(w).Field("damage").GetValue<int>() > 0f)
				.OrderByDescending(w => w.name.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0).FirstOrDefault();
			AI_NetworkBehavior_Shark bruce = null;
			for (int i = 0; i < 60 && bruce == null; i++)
			{
				bruce = LiveSharks().Where(s => !RogueShark.IsRogue(s)).OrderBy(s => (s.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
				if (bruce == null) yield return new WaitForSeconds(1f);
			}
			if (spear == null || bruce == null) { Fail("a spear (" + (spear != null) + ") and a live shark (" + (bruce != null) + ")"); MonsterDifficulty.Current = difficulty0; yield break; }
			Traverse tw = Traverse.Create(spear);
			if (tw.Field("hostNetwork").GetValue() == null) tw.Field("hostNetwork").SetValue(host);
			if (tw.Field("playerNetwork").GetValue() == null) tw.Field("playerNetwork").SetValue(player);
			MethodInfo onHit = AccessTools.Method(typeof(MeleeWeapon), "OnHitEntity");
			PlayerLevels.TurnOn(false);
			PlayerLevels.SetMine(new LevelRecord());
			try
			{
				Network_Entity e = bruce.networkEntity;
				int worth = PlayerLevels.MonsterXp(bruce), swings = 0, misses = 0;
				Check(ref ok, worth == LevelRules.BruceXp, "Bruce is worth " + worth + " EXP (want " + LevelRules.BruceXp + ")");
				for (int i = 0; i < 200 && !e.IsDead && misses < 20; i++)
				{
					KeepAlive(player);
					RaycastHit hit;
					if (!AimAt(bruce, out hit)) { misses++; yield return new WaitForSeconds(0.2f); continue; }
					try { onHit.Invoke(spear, new object[] { hit, e }); }
					catch (Exception ex) { if (swings == 0) Log("  (" + spear.name + " after the hit: " + (ex.InnerException ?? ex).Message + ")"); }
					swings++;
					yield return new WaitForSeconds(0.15f);
				}
				Check(ref ok, e.IsDead, "Bruce killed with " + swings + " swings of " + spear.name + (misses > 0 ? " (" + misses + " misses)" : ""));
				Check(ref ok, PlayerLevels.Mine.Xp == worth && PlayerLevels.Mine.Kills == 1, "his kill gives exactly his EXP (" + PlayerLevels.Mine.Xp + " of " + worth + "), one kill (" + PlayerLevels.Mine.Kills + ")");
				int xp = PlayerLevels.Mine.Xp;
				RaycastHit corpse;
				if (e.IsDead && AimAt(bruce, out corpse))
				{
					for (int i = 0; i < 3; i++) { try { onHit.Invoke(spear, new object[] { corpse, e }); } catch { } }
					host.DamageEntity(e, bruce.transform, 50f, bruce.transform.position, Vector3.up, EntityType.Player, null);
				}
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, PlayerLevels.Mine.Xp == xp && PlayerLevels.Mine.Kills == 1, "his corpse hit again gives nothing (EXP " + PlayerLevels.Mine.Xp + ", kills " + PlayerLevels.Mine.Kills + ")");
				AI_NetworkBehavior_Shark back = null;
				for (int i = 0; i < 120 && back == null; i++)
				{
					back = LiveSharks().FirstOrDefault(s => s != bruce && !RogueShark.IsRogue(s));
					if (back == null) { KeepAlive(player); yield return new WaitForSeconds(1f); }
				}
				if (back == null) Log("  (no shark came back within 2 minutes)");
				else
				{
					yield return new WaitForSeconds(2f);
					Check(ref ok, PlayerLevels.Mine.Xp == xp && PlayerLevels.Mine.Kills == 1, "a shark came back: no EXP for that (EXP " + PlayerLevels.Mine.Xp + ", kills " + PlayerLevels.Mine.Kills + ")");
				}
			}
			finally
			{
				MonsterDifficulty.Current = difficulty0;
				PlayerLevels.SetMine(new LevelRecord());
				PlayerLevels.TurnOff();
			}
			KeepAlive(player);
			if (ok) Log("PASS: level bruce"); else Fail("level bruce");
		}

		/// <summary>A raycast hit on a creature's own collider, from above it (as a swing from the raft lands).</summary>
		static bool AimAt(AI_NetworkBehaviour ai, out RaycastHit hit)
		{
			hit = default(RaycastHit);
			foreach (Collider col in ai.GetComponentsInChildren<Collider>().Where(x => x.enabled && !x.isTrigger))
			{
				Vector3 c = col.bounds.center;
				foreach (Vector3 from in new[] { c + Vector3.up * 3f, c + Vector3.right * 3f, c - Vector3.right * 3f, c + Vector3.forward * 3f })
				{
					RaycastHit[] hits = Physics.RaycastAll(from, (c - from).normalized, 6f, ~0, QueryTriggerInteraction.Ignore);
					foreach (RaycastHit h in hits.OrderBy(h => h.distance))
						if (h.collider == col) { hit = h; return true; }
				}
			}
			return false;
		}

		[ConsoleCommand(name: "CILevelDifficulty", docs: "Dev, in game (host): the level island's warthogs hit for 30% of their health through Raft's DamageEntity at each monster difficulty - Nightmare takes twice the hits (7), Timid fewer (3), Normal 4 - and each kill gives exactly the warthog's EXP, no more, no less; with Raft's Creative rule that players' hits do nothing an island creature still takes the hit and gives EXP (AU61), and the stats still apply. The difficulty and the record are put back after (IL12, IL11)")]
		public static void LevelDifficultyCommand() { StartTest(LevelDifficultyRoutine()); }

		static IEnumerator LevelDifficultyRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			int difficulty0 = MonsterDifficulty.Current;
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 4, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			PutPlayerNear(boars[0].transform, 5f);
			Network_Host host = ComponentManager<Network_Host>.Value;
			float pve = 1f;
			SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
			if (mode != null && mode.playerSpecificVariables != null) pve = mode.playerSpecificVariables.negateOutgoingPlayerDamage ? 0f : mode.playerSpecificVariables.outgoingDamageMultiplierPVE;
			if (pve <= 0f) { Fail("this world's game mode takes away players' damage"); LevelCleanup(entry); yield break; }
			try
			{
				int[] levels = { MonsterDifficulty.Nightmare, MonsterDifficulty.Timid, MonsterDifficulty.Normal };
				int[] wantHits = { 7, 3, 4 };
				for (int k = 0; k < levels.Length; k++)
				{
					MonsterDifficulty.Current = levels[k];
					PlayerLevels.SetMine(new LevelRecord());
					AI_NetworkBehaviour boar = boars[k];
					int worth = PlayerLevels.MonsterXp(boar);
					float max = boar.networkEntity.stat_health.Max;
					int xp0 = PlayerLevels.Mine.Xp, kills0 = PlayerLevels.Mine.Kills, hits = 0;
					for (int i = 0; i < 12 && !boar.networkEntity.IsDead; i++)
					{
						host.DamageEntity(boar.networkEntity, boar.transform, max * 0.3f / pve, boar.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
						hits++;
						yield return new WaitForSeconds(0.2f);
					}
					string name = MonsterDifficulty.Name(levels[k]);
					Check(ref ok, boar.networkEntity.IsDead && hits == wantHits[k], name + ": hits of 30% kill a warthog in " + hits + " (want " + wantHits[k] + ")");
					Check(ref ok, PlayerLevels.Mine.Xp - xp0 == worth && PlayerLevels.Mine.Kills == kills0 + 1, name + ": the kill gives its EXP exactly (" + (PlayerLevels.Mine.Xp - xp0) + " of " + worth + "), one kill");
				}
				// Raft's Peaceful / Creative: players' hits do nothing (negateOutgoingPlayerDamage) - and give no EXP (IL11)
				MonsterDifficulty.Current = MonsterDifficulty.Normal;
				if (mode != null && mode.playerSpecificVariables != null)
				{
					AI_NetworkBehaviour last = boars[3];
					float hp = last.networkEntity.stat_health.Value;
					int xp = PlayerLevels.Mine.Xp;
					bool negate0 = mode.playerSpecificVariables.negateOutgoingPlayerDamage;
					mode.playerSpecificVariables.negateOutgoingPlayerDamage = true;
					try { host.DamageEntity(last.networkEntity, last.transform, last.networkEntity.stat_health.Max * 0.5f, last.transform.position + Vector3.up, Vector3.up, EntityType.Player, null); }
					finally { mode.playerSpecificVariables.negateOutgoingPlayerDamage = negate0; }
					yield return new WaitForSeconds(0.2f);
					// (an island creature still takes hits there - AU61 kill quests, IslandCreatureHitPatch - so the hit gives its EXP)
					float hp1 = last.networkEntity.stat_health.Value;
					Check(ref ok, hp1 < hp - 1f && PlayerLevels.Mine.Xp > xp && !mode.playerSpecificVariables.negateOutgoingPlayerDamage, "a game mode where players' hits do nothing: an island creature still takes the hit and gives EXP, the mode's switch is put back (health " + hp.ToString("F0") + " -> " + hp1.ToString("F0") + ", EXP +" + (PlayerLevels.Mine.Xp - xp) + ")");
					PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(6), Points = new[] { 0, 0, 0, 0, 10, 0, 0, 0, 0 } });
					Check(ref ok, Mathf.Abs(PlayerLevels.Factor(LevelRules.Damage) - 1.1f) < 0.001f, "the stats still apply there (Damage x" + PlayerLevels.Factor(LevelRules.Damage).ToString("F2") + ")");
				}
				else Log("  (no game mode values: the Peaceful check skipped)");
			}
			finally
			{
				MonsterDifficulty.Current = difficulty0;
				PlayerLevels.SetMine(new LevelRecord());
			}
			LevelCleanup(entry);
			if (ok) Log("PASS: level difficulty"); else Fail("level difficulty");
		}

		/// <summary>Host: spawns the level island (levels on) near the raft with <paramref name="count"/> harmless warthogs
		/// and stands the player on it. On failure it says so and leaves entryOut[0] null.</summary>
		static IEnumerator LevelBoarsRoutine(Vector3 raftPos, int count, List<AI_NetworkBehaviour> boars, IslandWorldState.Entry[] entryOut)
		{
			IslandFile f = SampleIsland();
			f.Name = LevelIsland;
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			float step = f.TerrainSize.x / (f.HeightmapResolution - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 15f);
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x + 6f, c.y + 4f), Props = P(ObjectProps.CreatureCount, count.ToString(), ObjectProps.CreatureDamage, "0") });
			PlayerLevels.SetEnabled(true); // (a world option: islands don't turn it on)
			f.Save(IslandSpawner.PathFor(LevelIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos, CustomIslandSpawner.LandRadius(LevelIsland), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile(LevelIsland, spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault(e => e.HostName == LevelIsland);
			if (entry == null || entry.Root == null) { Fail("the level island did not spawn"); yield break; }
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 40f)
			{
				boars.Clear();
				boars.AddRange(entry.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null && p.Kind.Type == AI_NetworkBehaviourType.Boar)
					.SelectMany(p => p.Spawned).Where(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead && a.networkEntity.stat_health.Max > 0f));
				if (boars.Count >= count) break;
				yield return new WaitForSeconds(0.5f);
			}
			if (boars.Count < count) { Fail("the warthogs did not come (" + boars.Count + ")"); LevelCleanup(entry); yield break; }
			yield return StandRoutine(entry.Root);
			entryOut[0] = entry;
		}

		[ConsoleCommand(name: "CILevelWeapons", docs: "Dev, in game (host): Raft's own hit code on the level island's warthogs - every melee weapon in the player's hands (MeleeWeapon.OnHitEntity with a real raycast hit) does its damage times the Damage stat (10 points: x1.1) and gives EXP; a firework lit from the player's hand (Firework_Hand.LaunchFirework, then Raft's Firework.TryToDamageEntity, which counts as the world's damage) gives that player the EXP too. Arrows and stones call the same DamageEntity as players (IL1)")]
		public static void LevelWeaponsCommand() { StartTest(LevelWeaponsRoutine()); }

		static IEnumerator LevelWeaponsRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			if (!raftPos.HasValue || !Raft_Network.IsHost || player == null) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			PlayerLevels.TurnOff();
			MeleeWeapon[] melee = player.GetComponentsInChildren<MeleeWeapon>(true).Where(w => w != null && Traverse.Create(w).Field("damage").GetValue<int>() > 0f).ToArray();
			Firework_Hand hand = player.GetComponentsInChildren<Firework_Hand>(true).FirstOrDefault();
			List<AI_NetworkBehaviour> boars = new List<AI_NetworkBehaviour>();
			IslandWorldState.Entry[] entryOut = new IslandWorldState.Entry[1];
			yield return LevelBoarsRoutine(raftPos.Value, 3, boars, entryOut);
			IslandWorldState.Entry entry = entryOut[0];
			if (entry == null) yield break;
			Check(ref ok, melee.Length > 0, melee.Length + " melee weapons in the player's hands (" + string.Join(", ", melee.Select(w => w.name).Distinct().ToArray()) + ")");
			float pve = 1f;
			SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
			if (mode != null && mode.playerSpecificVariables != null) pve = mode.playerSpecificVariables.negateOutgoingPlayerDamage ? 0f : mode.playerSpecificVariables.outgoingDamageMultiplierPVE;
			if (pve <= 0f) { Fail("this world's game mode takes away players' damage"); LevelCleanup(entry); yield break; }
			Network_Host host = ComponentManager<Network_Host>.Value;
			int b = 0;
			Func<AI_NetworkBehaviour> next = () =>
			{
				while (b < boars.Count && (boars[b] == null || boars[b].networkEntity.IsDead || boars[b].networkEntity.stat_health.Value < boars[b].networkEntity.stat_health.Max * 0.3f)) b++;
				return b < boars.Count ? boars[b] : null;
			};
			try
			{
				PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(6), Points = new[] { 0, 0, 0, 0, 10, 0, 0, 0, 0 } });
				foreach (MeleeWeapon w in melee)
				{
					AI_NetworkBehaviour boar = next();
					if (boar == null) { Check(ref ok, false, "warthogs left for " + w.name); break; }
					PutPlayerNear(boar.transform, 4f);
					yield return new WaitForSeconds(0.3f);
					Traverse tw = Traverse.Create(w);
					if (tw.Field("hostNetwork").GetValue() == null) tw.Field("hostNetwork").SetValue(host);
					if (tw.Field("playerNetwork").GetValue() == null) tw.Field("playerNetwork").SetValue(player);
					RaycastHit hit;
					Collider col = boar.GetComponentsInChildren<Collider>().FirstOrDefault(x => x.enabled && !x.isTrigger);
					Vector3 aim = col != null ? col.bounds.center : boar.transform.position + Vector3.up * 0.5f;
					hit = default(RaycastHit); if (col == null || !col.Raycast(new Ray(aim + Vector3.up * 3f, Vector3.down), out hit, 6f)) // (its own collider only: another warthog's bones stood in the way)
					{ Check(ref ok, false, w.name + ": no raycast hit on the warthog" + (hit.transform != null ? " (hit " + hit.transform.name + ")" : "")); continue; }
					float hp = boar.networkEntity.stat_health.Value, dmg = tw.Field("damage").GetValue<int>();
					int xp = PlayerLevels.Mine.Xp;
					try { AccessTools.Method(typeof(MeleeWeapon), "OnHitEntity").Invoke(w, new object[] { hit, boar.networkEntity }); }
					catch (Exception e) { Log("  (" + w.name + " after the hit: " + (e.InnerException ?? e).Message + ")"); }
					yield return new WaitForSeconds(0.2f);
					float lost = hp - boar.networkEntity.stat_health.Value, want = Mathf.Min(hp, dmg * 1.1f * pve);
					Check(ref ok, Mathf.Abs(lost - want) < 0.05f + want * 0.01f && PlayerLevels.Mine.Xp > xp,
						w.name + ": " + lost.ToString("F1") + " damage (want " + want.ToString("F1") + " = " + dmg.ToString("F1") + " x1.1" + (pve != 1f ? " x" + pve.ToString("F2") : "") + "), EXP +" + (PlayerLevels.Mine.Xp - xp));
				}
				// a firework: lit from the player's own hand (its owner kept), Raft's own TryToDamageEntity hurts the warthog as the world
				AI_NetworkBehaviour fb = next();
				if (hand == null || fb == null) Check(ref ok, false, "a firework hand (" + (hand != null) + ") and a warthog left (" + (fb != null) + ")");
				else
				{
					PutPlayerNear(fb.transform, 6f);
					yield return new WaitForSeconds(0.3f);
					uint index = SaveAndLoad.GetUniqueObjectIndex();
					hand.LaunchFirework(player.transform.position + Vector3.up * 30f, Vector3.up, 5f, false, 0, index);
					Firework fw = UnityEngine.Object.FindObjectsOfType<Firework>().FirstOrDefault(x => x.ObjectIndex == index);
					Network_Entity_Redirect target = fb.GetComponentInChildren<Network_Entity_Redirect>(true);
					if (fw == null || target == null) Check(ref ok, false, "the firework (" + (fw != null) + ") and the warthog's hit box (" + (target != null) + ")");
					else
					{
						float hp = fb.networkEntity.stat_health.Value;
						int xp = PlayerLevels.Mine.Xp;
						AccessTools.Method(typeof(Firework), "TryToDamageEntity").Invoke(fw, new object[] { target.transform });
						UnityEngine.Object.Destroy(fw.gameObject);
						yield return new WaitForSeconds(0.2f);
						Check(ref ok, fb.networkEntity.stat_health.Value < hp && PlayerLevels.Mine.Xp > xp,
							"a firework: health " + hp.ToString("F0") + " -> " + fb.networkEntity.stat_health.Value.ToString("F0") + ", EXP +" + (PlayerLevels.Mine.Xp - xp) + " for the player who lit it");
						Check(ref ok, LevelFireworkPatch.By == 0UL, "the firework's owner is let go after its hit");
					}
				}
			}
			finally { PlayerLevels.SetMine(new LevelRecord()); }
			LevelCleanup(entry);
			if (ok) Log("PASS: level weapons"); else Fail("level weapons");
		}

		[ConsoleCommand(name: "CILevelCost", docs: "Dev, in game: what the level up system costs a frame - Raft's hunger/thirst getter (patched), the name tags' and the health stat's ticks - timed 20,000 times with the system on and off; each must stay under 2 microseconds a call more, and 300 frames with it on take about as long as with it off (within 15%: frame times wander) (IL24)")]
		public static void LevelCostCommand() { StartTest(LevelCostRoutine()); }

		static IEnumerator LevelCostRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (!LoadSceneManager.IsGameSceneLoaded || player == null || player.Stats == null) { Fail("level cost: run in a world"); yield break; }
			bool ok = true;
			bool wasOn = PlayerLevels.On;
			Stat_Consumable bar = player.Stats.stat_hunger.normalConsumable;
			MethodInfo getter = AccessTools.PropertyGetter(typeof(Stat_Consumable), "LostPerSecond");
			Func<bool, double[]> time = on =>
			{
				if (on) PlayerLevels.TurnOn(false); else PlayerLevels.TurnOff();
				const int n = 20000;
				var sw = System.Diagnostics.Stopwatch.StartNew();
				for (int i = 0; i < n; i++) getter.Invoke(bar, null);
				double g = sw.Elapsed.TotalMilliseconds * 1000.0 / n;
				sw = System.Diagnostics.Stopwatch.StartNew();
				for (int i = 0; i < n / 20; i++) { LevelTags.Tick(); StatApply.Tick(); }
				double t = sw.Elapsed.TotalMilliseconds * 1000.0 / (n / 20);
				return new[] { g, t };
			};
			double[] off = time(false), on1 = time(true);
			Check(ref ok, on1[0] - off[0] < 2.0, "the hunger getter: " + off[0].ToString("F2") + " us off, " + on1[0].ToString("F2") + " us on");
			Check(ref ok, on1[1] - off[1] < 20.0, "the name tags' and health's ticks: " + off[1].ToString("F2") + " us off, " + on1[1].ToString("F2") + " us on");
			var frames = new float[2];
			for (int round = 0; round < 2; round++)
			{
				if (round == 1) PlayerLevels.TurnOn(false); else PlayerLevels.TurnOff();
				yield return new WaitForSecondsRealtime(1f);
				float sum = 0f;
				for (int i = 0; i < 300; i++) { yield return null; sum += Time.unscaledDeltaTime; }
				frames[round] = sum / 300f * 1000f;
			}
			// (frame times wander by a few percent by themselves: logged; a failure only when far off)
			Check(ref ok, frames[1] <= frames[0] * 1.15f + 0.5f, "300 frames: " + frames[0].ToString("F2") + " ms off, " + frames[1].ToString("F2") + " ms on");
			if (wasOn) PlayerLevels.TurnOn(false); else PlayerLevels.TurnOff();
			if (ok) Log("PASS: level cost"); else Fail("level cost");
		}

		[ConsoleCommand(name: "CILevelBody", docs: "Dev, in game: the Health, Hunger and Thirst stats on Raft's own player - 10 points in Health raise the maximum by 10%, healing stops there, Raft's bonus health is left alone, and after a respawn without a bed the maximum is still raised; 10 points in Hunger and Thirst make Raft's drain (normal and bonus bars) 10% slower, measured over game time at 4x speed. The record and time speed are put back after (IL5, IL10)")]
		public static void LevelBodyCommand() { StartTest(LevelBodyRoutine()); }

		static IEnumerator LevelBodyRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (!LoadSceneManager.IsGameSceneLoaded || player == null || player.Stats == null) { Fail("level body: run in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			bool wasOn = PlayerLevels.On;
			if (!wasOn) PlayerLevels.TurnOn(false);
			LevelRecord kept = PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : new LevelRecord();
			float scale0 = Time.timeScale;
			PlayerStats s = player.Stats;
			Player body = player.GetComponentInChildren<Player>(true);
			Func<Stat_Consumable, float> lost = c => Traverse.Create(c).Property("LostPerSecond").GetValue<float>();
			var ten = new LevelRecord { Xp = LevelRules.TotalFor(40) };
			ten.Points[LevelRules.Health] = 10; ten.Points[LevelRules.Hunger] = 10; ten.Points[LevelRules.Thirst] = 10;
			try
			{
				// Health: 10 points -> Raft's maximum x1.1
				PlayerLevels.SetMine(new LevelRecord());
				yield return new WaitForSeconds(0.8f);
				KeepAlive(player);
				float baseMax = s.stat_health.Max, bonus0 = s.stat_BonusHealth != null ? s.stat_BonusHealth.Max : -1f;
				PlayerLevels.SetMine(ten);
				yield return new WaitForSeconds(0.8f);
				float max10 = s.stat_health.Max;
				Check(ref ok, Mathf.Abs(max10 - baseMax * 1.1f) < 0.05f, "10 points in Health: maximum " + baseMax.ToString("F1") + " -> " + max10.ToString("F1"));
				s.stat_health.Value = max10 - 5f;
				s.stat_health.Value += 20f;
				Check(ref ok, s.stat_health.Value <= max10 + 0.01f && s.stat_health.Value >= max10 - 0.01f, "healing stops at the new maximum (" + s.stat_health.Value.ToString("F1") + ")");
				Check(ref ok, s.stat_BonusHealth == null || Mathf.Abs(s.stat_BonusHealth.Max - bonus0) < 0.01f, "Raft's bonus health is left alone (" + (s.stat_BonusHealth != null ? s.stat_BonusHealth.Max.ToString("F1") : "none") + ")");
				if (body != null)
				{
					body.RespawnWithoutBed(false);
					yield return new WaitForSeconds(3f);
					Check(ref ok, Mathf.Abs(s.stat_health.Max - max10) < 0.05f, "after a respawn without a bed the maximum is still raised (" + s.stat_health.Max.ToString("F1") + ")");
				}
				else Log("  (no Player component: respawn not checked)");
				KeepAlive(player);

				// Hunger and thirst: Raft's drain, read and measured
				Stat_Consumable[] bars = { s.stat_hunger.normalConsumable, s.stat_hunger.bonusConsumable, s.stat_thirst.normalConsumable, s.stat_thirst.bonusConsumable };
				string[] names = { "hunger", "hunger (bonus)", "thirst", "thirst (bonus)" };
				float[] with10 = bars.Select(c => c != null ? lost(c) : 0f).ToArray();
				PlayerLevels.SetMine(new LevelRecord());
				float[] with0 = bars.Select(c => c != null ? lost(c) : 0f).ToArray();
				for (int i = 0; i < bars.Length; i++)
					if (bars[i] != null && with0[i] > 0f)
						Check(ref ok, Mathf.Abs(with10[i] - with0[i] / 1.1f) < with0[i] * 0.005f, names[i] + ": " + with0[i].ToString("F5") + "/s -> " + with10[i].ToString("F5") + "/s with 10 points");
				var drop = new float[2][];
				for (int round = 0; round < 2; round++)
				{
					PlayerLevels.SetMine(round == 0 ? new LevelRecord() : ten);
					yield return new WaitForSeconds(0.5f);
					KeepAlive(player);
					s.stat_hunger.Normal.Value = s.stat_hunger.Normal.Max * 0.6f;
					s.stat_thirst.Normal.Value = s.stat_thirst.Normal.Max * 0.6f;
					float h0 = s.stat_hunger.Normal.Value, t0 = s.stat_thirst.Normal.Value;
					Time.timeScale = 4f;
					yield return new WaitForSecondsRealtime(10f);
					Time.timeScale = scale0;
					drop[round] = new[] { h0 - s.stat_hunger.Normal.Value, t0 - s.stat_thirst.Normal.Value };
				}
				for (int k = 0; k < 2; k++)
				{
					float ratio = drop[0][k] > 0f ? drop[1][k] / drop[0][k] : 0f;
					Check(ref ok, drop[0][k] > 0f && Mathf.Abs(ratio - 1f / 1.1f) < 0.04f, (k == 0 ? "hunger" : "thirst") + " over game time: " + drop[0][k].ToString("F3") + " with 0 points, " + drop[1][k].ToString("F3") + " with 10 (x" + ratio.ToString("F3") + ", about 0.909)");
				}
			}
			finally
			{
				Time.timeScale = scale0;
				PlayerLevels.SetMine(kept);
				if (!wasOn) PlayerLevels.TurnOff();
				KeepAlive(player);
			}
			if (ok) Log("PASS: level body"); else Fail("level body");
		}

		[ConsoleCommand(name: "CILevelAir", docs: "Dev, in game: the Oxygen stat measured - this player held 6 m under the sea, Raft's breath bar drops over game time at 4x speed with 0 and then 10 points in Oxygen: with 10 it drops about 10% slower (x0.909). The record, the time speed and the player are put back after (IL8)")]
		public static void LevelAirCommand() { StartTest(LevelAirRoutine()); }

		static IEnumerator LevelAirRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!LoadSceneManager.IsGameSceneLoaded || player == null || player.Stats == null || !raft.HasValue) { Fail("level air: run in a world"); yield break; }
			yield return EnsureAlive();
			bool ok = true, wasOn = PlayerLevels.On;
			if (!wasOn) PlayerLevels.TurnOn(false);
			LevelRecord kept = PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : new LevelRecord();
			float scale0 = Time.timeScale;
			Stat_Oxygen air = player.Stats.stat_oxygen;
			var drop = new float[2];
			try
			{
				Vector3 under = raft.Value + new Vector3(0f, 0f, -40f);
				under.y = -6f;
				for (int round = 0; round < 2; round++)
				{
					var r = new LevelRecord { Xp = LevelRules.TotalFor(40) };
					r.Points[LevelRules.Oxygen] = round == 0 ? 0 : 10;
					PlayerLevels.SetMine(r);
					PlayerMove.To(player, under, ControllerType.Water);
					KeepAlive(player);
					yield return new WaitForSeconds(1f);
					air.Value = air.Max;
					float a0 = air.Value, until = Time.realtimeSinceStartup + 6f, g0 = Time.time;
					Time.timeScale = 4f;
					while (Time.realtimeSinceStartup < until)
					{
						// (held under: a still player drifts up and breathes at the surface)
						if (Mathf.Abs(player.transform.position.y - under.y) > 0.5f) PlayerMove.To(player, under, ControllerType.Water);
						yield return null;
					}
					Time.timeScale = scale0;
					// (per second of game time: the frame rate decides how much game time 6 real seconds hold)
					drop[round] = (a0 - air.Value) / Mathf.Max(0.001f, Time.time - g0);
				}
				float ratio = drop[0] > 0f ? drop[1] / drop[0] : 0f;
				Check(ref ok, drop[0] > 0f && air.Value > 0f && Mathf.Abs(ratio - 1f / 1.1f) < 0.04f, "breath under the sea per game second: " + drop[0].ToString("F3") + " with 0 points, " + drop[1].ToString("F3") + " with 10 (x" + ratio.ToString("F3") + ", about 0.909)");
			}
			finally
			{
				Time.timeScale = scale0;
				PlayerLevels.SetMine(kept);
				if (!wasOn) PlayerLevels.TurnOff();
				air.Value = air.Max;
				OnRaftCommand();
				KeepAlive(player);
			}
			if (ok) Log("PASS: level air"); else Fail("level air");
		}

		// CILevelMove: the walk, run, swim and jump stats measured with Raft's controller moving the player on real keys (IL9)
		static GameObject movePad;
		static LevelRecord moveKept;
		static bool moveWasOn, moveWater;
		static Vector3 moveStart;
		static float movePeak;
		static int moveRun;

		[ConsoleCommand(name: "CILevelMove", docs: "Dev, in game: the speed and jump stats with real keys (IL9) - CILevelMove land|water <points> puts this player on a flat pad 40 m over the sea (or in the sea) facing north, with <points> in Walk, Run, Swim and Jump (logs Ready); then hold W, Shift+W or Space for real, and CILevelMove result logs MOVE across <m> up <m> since and puts the player back; CILevelMove end puts the record, the pad and the player back")]
		public static void LevelMoveCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			int points = 0;
			if (args != null && args.Length > 1) int.TryParse(args[1], out points);
			StartTest(LevelMoveRoutine(what, Mathf.Clamp(points, 0, LevelRules.MaxPoints)));
		}

		static IEnumerator LevelMoveRoutine(string what, int points)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!LoadSceneManager.IsGameSceneLoaded || player == null || !raft.HasValue) { Fail("level move: run in a world"); yield break; }
			if (what == "end")
			{
				moveRun++;
				if (movePad != null) UnityEngine.Object.Destroy(movePad);
				movePad = null;
				if (moveKept != null) { PlayerLevels.SetMine(moveKept); if (!moveWasOn) PlayerLevels.TurnOff(); }
				moveKept = null;
				OnRaftCommand();
				KeepAlive(player);
				Log("Level move ended: record and player back");
				yield break;
			}
			if (what == "result")
			{
				if (moveKept == null) { Fail("level move: CILevelMove land|water first"); yield break; }
				Vector3 at = player.transform.position;
				Log("MOVE across " + ScFlat(at, moveStart).ToString("F2", CultureInfo.InvariantCulture) + " up " + (movePeak - moveStart.y).ToString("F2", CultureInfo.InvariantCulture) +
					" (" + (moveWater ? "water" : "land") + ", " + PlayerLevels.Mine.Points[LevelRules.Walk] + " points)");
				yield return MoveToStart(player, moveStart, moveWater);
				yield break;
			}
			if (what != "land" && what != "water") { Fail("level move: CILevelMove land|water <points>, result or end"); yield break; }
			yield return EnsureAlive();
			if (moveKept == null) { moveWasOn = PlayerLevels.On; moveKept = PlayerLevels.Mine != null ? PlayerLevels.Mine.Copy() : new LevelRecord(); }
			if (!PlayerLevels.On) PlayerLevels.TurnOn(false);
			var r = new LevelRecord { Xp = LevelRules.TotalFor(40) };
			foreach (int st in new[] { LevelRules.Walk, LevelRules.Run, LevelRules.Swim, LevelRules.Jump }) r.Points[st] = points;
			PlayerLevels.SetMine(r);
			moveWater = what == "water";
			Vector3 start;
			if (moveWater)
			{
				// (south of the raft, swimming on south: away from it)
				start = raft.Value + new Vector3(0f, 0f, -40f);
				start.y = 0.5f;
			}
			else
			{
				// (a flat pad well over the sea and anything on it: nothing to bump into for 100 m)
				if (movePad == null)
				{
					movePad = new GameObject("CILevelMovePad");
					movePad.layer = IslandSpawner.TerrainLayer;
					movePad.AddComponent<BoxCollider>().size = new Vector3(300f, 1f, 300f);
				}
				movePad.transform.position = raft.Value + new Vector3(0f, 40f, 0f);
				movePad.transform.rotation = Quaternion.identity;
				start = movePad.transform.position + new Vector3(0f, 1.6f, 0f);
			}
			moveStart = start;
			yield return MoveToStart(player, start, moveWater);
			int run = ++moveRun;
			DynamicIslands.instance.StartCoroutine(TrackMove(player, run));
			Log("Ready: " + what + ", " + points + " points in Walk, Run, Swim and Jump (Walk factor " + PlayerLevels.Factor(LevelRules.Walk).ToString("F2", CultureInfo.InvariantCulture) + ")");
		}

		/// <summary>The player back at the start, facing the way to go, settled; the measuring starts from there.</summary>
		static IEnumerator MoveToStart(Network_Player player, Vector3 start, bool water)
		{
			PlayerMove.To(player, start, water ? ControllerType.Water : ControllerType.Ground);
			Look(player, water ? 180f : 0f, 0f);
			KeepAlive(player);
			yield return new WaitForSeconds(1.2f);
			moveStart = player.transform.position;
			movePeak = moveStart.y;
		}

		static IEnumerator TrackMove(Network_Player player, int run)
		{
			while (run == moveRun && player != null)
			{
				movePeak = Mathf.Max(movePeak, player.transform.position.y);
				yield return null;
			}
		}

		[ConsoleCommand(name: "CILevelInfo", docs:"Dev, in game (either player): the level up system here - on or off, this player's level, EXP and points; on the host also every player's record. CILevelInfo off = turn it off (host, tests)")]
		public static void LevelInfo(string[] args)
		{
			if (args != null && args.Length > 0 && args[0] == "off") { PlayerLevels.TurnOff(); Log("Levels turned off"); return; }
			LevelRecord r = PlayerLevels.Mine;
			Network_Player p = RAPI.GetLocalPlayer();
			Log("LEVELS " + (PlayerLevels.On ? "on" : "off") + (r != null ? ": level " + r.Level + ", xp " + r.Xp + ", points " + string.Join(",", r.Points.Select(x => x.ToString()).ToArray()) + ", unspent " + r.Unspent + ", kills " + r.Kills : "") +
				", tags [" + string.Join(" ", LevelTags.Shown.Select(kv => kv.Key + "=" + kv.Value.Replace(" ", "")).ToArray()) + "]" + ", bar " + (LevelHud.BarShown ? "shown" : "hidden") + ", stats button " + (LevelHud.StatsButton != null && LevelHud.StatsButton.gameObject.activeInHierarchy ? "shown" : "hidden") +
				(p != null && p.PersonController != null ? ", max health " + (p.Stats != null ? p.Stats.stat_health.Max.ToString("F1") : "?") : "") + ", stats page " + (LevelWindow.IsOpen ? "open" : "closed") + (Raft_Network.IsHost ? " (host)" : " (client)"));
			if (p != null)
				foreach (Network_Player other in UnityEngine.Object.FindObjectsOfType<Network_Player>().Where(x => x != null && x != p))
				{
					LevelRecord o = Raft_Network.IsHost ? PlayerLevels.RecordOf(other.steamID.Id) : null;
					Log("LEVELS player " + other.steamID.Id + ": " + (o != null ? "xp " + o.Xp + ", level " + o.Level + ", points " + string.Join(",", o.Points.Select(x => x.ToString()).ToArray()) : Raft_Network.IsHost ? "no record" : "level " + PlayerLevels.LevelOf(other.steamID.Id) + " (as the host sent it)") + ", max health here " + (other.Stats != null && other.Stats.stat_health != null ? other.Stats.stat_health.Max.ToString("F1") : "?"));
				}
		}

		[ConsoleCommand(name: "CILevelGive", docs: "Dev, in game (either player): gives this player EXP, as hits would: CILevelGive <exp>")]
		public static void LevelGive(string[] args)
		{
			int xp;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out xp) || xp <= 0) { Fail("CILevelGive <exp>"); return; }
			if (!PlayerLevels.On) { Fail("the level up system is off here"); return; }
			Network_Player p = RAPI.GetLocalPlayer();
			PlayerLevels.GiveXp(xp, p != null ? p.transform.position + p.transform.forward * 3f + Vector3.up : (Vector3?)null);
			Log("PASS: gave " + xp + " EXP: level " + PlayerLevels.Mine.Level + ", xp " + PlayerLevels.Mine.Xp);
		}

		[ConsoleCommand(name: "CILevelHealth", docs: "Dev, in game (host), for the persist phase: CILevelHealth set = levels on, 10 points in Health and full health (110 / 110), to save; CILevelHealth check = after loading or a restart the player came back with 110 / 110, not cut to 100 before the stat applied; CILevelHealth off = levels off and a fresh record (IL6); CILevelHealth other = in another world loaded after one with levels: off, 100 / 100, no tags or bar (IL27)")]
		public static void LevelHealthCommand(string[] args)
		{
			string what = args != null && args.Length > 0 ? args[0] : "";
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null || !Raft_Network.IsHost) { Fail("run in a world, as the host"); return; }
			Stat_Health h = p.Stats.stat_health;
			if (what == "set")
			{
				PlayerLevels.TurnOn(false);
				PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(6), Points = new[] { 0, 0, 0, 0, 0, 10, 0, 0, 0 } });
				h.Value = h.Max;
				if (h.Max > 109.9f && h.Value > 109.9f) Log("PASS: health set to " + h.Value.ToString("F1") + " / " + h.Max.ToString("F1"));
				else Fail("health set: " + h.Value.ToString("F1") + " / " + h.Max.ToString("F1") + " (want 110 / 110)");
			}
			else if (what == "check")
			{
				int points = PlayerLevels.Mine != null ? PlayerLevels.Mine.Points[LevelRules.Health] : -1;
				if (PlayerLevels.On && points == 10 && h.Max > 109.9f && h.Value > 109.9f) Log("PASS: health kept: " + h.Value.ToString("F1") + " / " + h.Max.ToString("F1") + ", 10 points in Health");
				else Fail("health kept: " + h.Value.ToString("F1") + " / " + h.Max.ToString("F1") + ", levels " + (PlayerLevels.On ? "on" : "off") + ", " + points + " points in Health (want 110 / 110, 10)");
			}
			else if (what == "off")
			{
				PlayerLevels.SetMine(new LevelRecord());
				PlayerLevels.TurnOff();
				Log("PASS: levels off, a fresh record");
			}
			else if (what == "other")
			{
				// IL27: after a world with levels on (10 points in Health) the player loads another world without them
				int tags = LevelTags.Shown.Count(kv => !string.IsNullOrEmpty(kv.Value));
				bool clean = !PlayerLevels.On && h.Max < 100.1f && h.Value < 100.1f && tags == 0 && !LevelHud.BarShown;
				string state = "levels " + (PlayerLevels.On ? "ON" : "off") + ", health " + h.Value.ToString("F1") + " / " + h.Max.ToString("F1") + ", " + tags + " name tags, bar " + (LevelHud.BarShown ? "SHOWN" : "hidden");
				if (clean) Log("PASS: another world, nothing left: " + state); else Fail("another world: " + state + " (want off, 100 / 100, no tags, no bar)");
			}
			else Fail("CILevelHealth set|check|off|other");
		}

		[ConsoleCommand(name: "CILevelDamageOthers", docs: "Dev, in game (host): the Damage stat only makes hits on monsters bigger (IL4, one player): with 10 points a player's hit on a player (as friendly fire) and the world's hit take Raft's amount and give no EXP, while a player's hit on a monster is the stat's x bigger")]
		public static void LevelDamageOthers()
		{
			Network_Player p = RAPI.GetLocalPlayer();
			Network_Host net = ComponentManager<Network_Host>.Value;
			if (p == null || net == null || !Raft_Network.IsHost) { Fail("run in a world, as the host"); return; }
			bool ok = true;
			bool wasOn = PlayerLevels.On;
			LevelRecord keep = PlayerLevels.Mine;
			int monsters = MonsterDifficulty.Current;
			MonsterDifficulty.Current = MonsterDifficulty.Normal;
			PlayerLevels.TurnOn(false);
			var drops = new Dictionary<int, float[]>();
			foreach (int points in new[] { 0, 10 })
			{
				PlayerLevels.SetMine(new LevelRecord { Xp = LevelRules.TotalFor(6), Points = new[] { 0, 0, 0, 0, points, 0, 0, 0, 0 } });
				int xp0 = PlayerLevels.Mine.Xp;
				drops[points] = new[] { HitAndHeal(net, p.Stats, 10f, EntityType.Player), HitAndHeal(net, p.Stats, 10f, EntityType.Environment) };
				Check(ref ok, PlayerLevels.Mine.Xp == xp0, points + " points in Damage: a hit on a player gives no EXP (" + xp0 + " -> " + PlayerLevels.Mine.Xp + ")");
			}
			Check(ref ok, Mathf.Abs(drops[10][0] - drops[0][0]) < 0.01f, "a player's hit on a player: " + drops[0][0].ToString("F2") + " at 0 points, " + drops[10][0].ToString("F2") + " at 10 (Raft's either way" + (drops[0][0] <= 0f ? "; 0: friendly fire is off in this game mode" : "") + ")");
			Check(ref ok, drops[0][1] > 0f && Mathf.Abs(drops[10][1] - drops[0][1]) < 0.01f, "the world's hit: " + drops[0][1].ToString("F2") + " at 0 points, " + drops[10][1].ToString("F2") + " at 10 (Raft's either way)");
			float factor = PlayerLevels.Factor(LevelRules.Damage);
			Check(ref ok, factor > 1.01f, "on a monster the same 10 points make a hit x" + factor.ToString("F2"));
			PlayerLevels.SetMine(keep ?? new LevelRecord());
			if (!wasOn) PlayerLevels.TurnOff();
			MonsterDifficulty.Current = monsters;
			if (ok) Log("PASS: level damage others"); else Fail("level damage others");
		}

		[ConsoleCommand(name: "CILevelSpend", docs: "Dev, in game (either player): puts a stat point into a stat by its number (0 walk ... 7 oxygen) through the stats page, as clicking its +: CILevelSpend <stat>")]
		public static void LevelSpend(string[] args)
		{
			int stat;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out stat)) { Fail("CILevelSpend <0-7>"); return; }
			bool was = LevelWindow.IsOpen;
			LevelWindow.Open();
			bool done = LevelWindow.Click(stat, true);
			if (!was) LevelWindow.Close();
			if (done) Log("PASS: a point into " + LevelRules.StatNames[stat]); else Fail("no point to spend on " + stat);
		}

		#endregion
	}
}
