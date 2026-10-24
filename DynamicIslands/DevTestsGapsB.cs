using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Two-player gaps (mpgaps.ps1): a Boss warthog's EXP for player 2 (IM12), alphas and Big Bruce the same on both machines
	/// (IM6, IM37), a den guard's health and size on both (IM33), the Damage stat leaving player hits and blocks alone (IL4),
	/// and one of Raft's islands reached first by player 2 far from the host (IM35).
	/// </summary>
	public static partial class DevTests
	{
		const string GapsXpIsland = "cigapsxp";
		static readonly CultureInfo GInv = CultureInfo.InvariantCulture;

		static string Gf(float v, string fmt = "F2") { return v.ToString(fmt, GInv); }
		static string GWho() { return Raft_Network.IsHost ? "host" : "client"; }

		#region IM12: a Boss warthog's EXP

		[ConsoleCommand(name: "CIGapsXpIsland", docs: "Dev, in game (host), IM12: spawns 'cigapsxp' 150 m ahead with the level up system, a plain warthog and a Boss one (health x4, damage x2.5, size x1.4)")]
		public static void GapsXpIslandCommand() { StartTest(GapsXpIslandRoutine()); }

		static IEnumerator GapsXpIslandRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandFile f = SampleIsland();
			f.Name = GapsXpIsland;
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, float> h = (x, z) => f.Heights[Mathf.Clamp(Mathf.RoundToInt(z / step), 0, res - 1), Mathf.Clamp(Mathf.RoundToInt(x / step), 0, res - 1)] * f.TerrainSize.y;
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 20f);
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = new Vector3(c.x + 7f, h(c.x + 7f, c.y + 4f), c.y + 4f), Props = P(ObjectProps.CreatureCount, "1") });
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = new Vector3(c.x - 7f, h(c.x - 7f, c.y - 4f), c.y - 4f),
				Props = P(ObjectProps.CreatureCount, "1", ObjectProps.CreatureHealth, "4", ObjectProps.CreatureDamage, "2.5", ObjectProps.CreatureSpeed, "1.3", ObjectProps.CreatureSize, "1.4") });
			PlayerLevels.SetEnabled(true); // (a world option: islands don't turn it on)
			f.Save(IslandSpawner.PathFor(GapsXpIsland));
			IslandWorldState.Entry old = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == GapsXpIsland);
			if (old != null) IslandWorldState.RemoveIds(new[] { old.Id }, true);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(GapsXpIsland), 150f);
			for (int move = 0; move < 3 && !spot.HasValue; move++)
			{
				Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
				if (raft == null || raft.body == null) break;
				Vector3 dir = Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward;
				raft.body.position = raft.body.position + dir * 600f;
				raft.body.velocity = Vector3.zero;
				Physics.SyncTransforms();
				OnRaftCommand();
				Log("  (no open sea near the raft: the raft moved 600 m on)");
				yield return new WaitForSeconds(3f);
				raftPos = CustomIslandSpawner.RaftPosition;
				if (raftPos.HasValue) spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(GapsXpIsland), 150f);
			}
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(GapsXpIsland, spot.Value, true);
			if (PlayerLevels.On) Log("PASS: cigapsxp spawned"); else Fail("'" + GapsXpIsland + "' did not turn the level up system on");
		}

		/// <summary>The island's live warthogs, smallest first (the Boss one is size x1.4: the biggest).</summary>
		static List<AI_NetworkBehaviour> GapsWarthogs(IslandWorldState.Entry e)
		{
			return AnimalsOf(e, "Warthog", 100f).Where(a => a.networkEntity != null && !a.networkEntity.IsDead).OrderBy(a => a.transform.localScale.x).ToList();
		}

		[ConsoleCommand(name: "CIGapsXpWorth", docs: "Dev, in game (host), IM12: what each live warthog of an island is worth (PlayerLevels.MonsterXp): CIGapsXpWorth <island>; logs XPWORTH big|small #<index> size S: xp N (max M)")]
		public static void GapsXpWorthCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			List<AI_NetworkBehaviour> w = GapsWarthogs(e);
			if (w.Count < 2) { Fail("2 live warthogs at '" + e.HostName + "' (" + w.Count + ")"); return; }
			foreach (var x in new[] { new KeyValuePair<string, AI_NetworkBehaviour>("big", w[w.Count - 1]), new KeyValuePair<string, AI_NetworkBehaviour>("small", w[0]) })
				Log("XPWORTH " + x.Key + " #" + x.Value.ObjectIndex + " size " + Gf(x.Value.transform.localScale.x) + ": xp " + PlayerLevels.MonsterXp(x.Value) + " (max " + Gf(x.Value.networkEntity.stat_health.Max, "F1") + ") (" + GWho() + ")");
			Log("PASS: xp worth");
		}

		[ConsoleCommand(name: "CIGapsKillXp", docs: "Dev, in game (either player; player 2), IM12: defeats the biggest or smallest live warthog of an island in one hit and waits for the EXP: CIGapsKillXp <island> big|small; logs KILLXP #<index> gained G")]
		public static void GapsKillXpCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			bool big = args == null || args.Length < 2 || args[1] != "small";
			List<AI_NetworkBehaviour> w = GapsWarthogs(e);
			if (w.Count == 0) { Fail("no live warthog at '" + e.HostName + "'"); return; }
			if (PlayerLevels.Mine == null) { Fail("the level up system is off here"); return; }
			StartTest(GapsKillXpRoutine(big ? w[w.Count - 1] : w[0]));
		}

		static IEnumerator GapsKillXpRoutine(AI_NetworkBehaviour a)
		{
			int before = PlayerLevels.Mine.Xp;
			int worthHere = PlayerLevels.MonsterXp(a);
			PutPlayerNear(a.transform, 3f);
			ComponentManager<Network_Host>.Value.DamageEntity(a.networkEntity, a.transform, 99999f, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			for (float t = 0; t < 15f && PlayerLevels.Mine != null && PlayerLevels.Mine.Xp == before; t += 0.5f) yield return new WaitForSeconds(0.5f);
			yield return new WaitForSeconds(1f); // (a second message for the same kill would come now)
			int gained = PlayerLevels.Mine != null ? PlayerLevels.Mine.Xp - before : -1;
			Log("KILLXP #" + a.ObjectIndex + " gained " + gained + " (worth " + worthHere + " by this machine's copy, size " + Gf(a.transform.localScale.x) + ") (" + GWho() + ")");
			if (gained > 0) Log("PASS: kill xp"); else Fail("no EXP came for the kill of #" + a.ObjectIndex);
		}

		[ConsoleCommand(name: "CIGapsLastRemote", docs: "Dev, in game (host), IM12: the host's last EXP for another player's hit (PlayerLevels.LastRemote): logs LASTREMOTE <id> +<n> [kill]")]
		public static void GapsLastRemoteCommand()
		{
			Log("LASTREMOTE " + (PlayerLevels.LastRemote ?? "none"));
			Log("PASS: last remote");
		}

		#endregion

		#region IL4: the Damage stat leaves players and blocks alone

		[ConsoleCommand(name: "CIGapsDamagePoints", docs: "Dev, in game (either player), IL4: EXP enough and points spent until this player has <n> points in Damage (stats page clicks): CIGapsDamagePoints <n>; logs DMGPOINTS points P factor F")]
		public static void GapsDamagePointsCommand(string[] args)
		{
			int n;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out n)) { Fail("CIGapsDamagePoints <n>"); return; }
			LevelRecord r = PlayerLevels.Mine;
			if (r == null) { Fail("the level up system is off here"); return; }
			n = Mathf.Clamp(n, 0, LevelRules.MaxPoints);
			bool was = LevelWindow.IsOpen;
			LevelWindow.Open();
			for (int i = 0; i < 40 && r.Points[LevelRules.Damage] < n; i++)
			{
				if (r.Unspent <= 0) PlayerLevels.GiveXp(Mathf.Max(1, LevelRules.TotalFor(r.Level + 1) - r.Xp), null);
				LevelWindow.Click(LevelRules.Damage, true);
			}
			if (!was) LevelWindow.Close();
			Log("DMGPOINTS points " + r.Points[LevelRules.Damage] + " factor " + Gf(PlayerLevels.Factor(LevelRules.Damage)) + " (" + GWho() + ")");
			if (r.Points[LevelRules.Damage] >= n) Log("PASS: damage points"); else Fail("only " + r.Points[LevelRules.Damage] + " Damage points");
		}

		[ConsoleCommand(name: "CIGapsFriendlyFire", docs: "Dev, in game (either player), IL4: Raft's friendly fire on or off on this machine (GameManager.FriendlyFire): CIGapsFriendlyFire on|off")]
		public static void GapsFriendlyFireCommand(string[] args)
		{
			GameManager.FriendlyFire = args != null && args.Length > 0 && args[0] == "on";
			Log("FRIENDLYFIRE " + (GameManager.FriendlyFire ? "on" : "off") + " (" + GWho() + ")");
			Log("PASS: friendly fire");
		}

		static float gapsTook = -1f;
		static bool gapsWatching;
		// (a new watch ends the one before: two at once added the same hit twice - IL4's second hit read 40 for 20)
		static int gapsWatchId;

		[ConsoleCommand(name: "CIGapsWatchHealth", docs: "Dev, in game (either player; the host), IL4: fills this player's health, then adds up every drop in it for <seconds> (regeneration left out): CIGapsWatchHealth <seconds>; CIGapsWatchResult then logs WATCH took X")]
		public static void GapsWatchHealthCommand(string[] args)
		{
			float secs;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], NumberStyles.Float, GInv, out secs)) secs = 20f;
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null || p.Stats == null) { Fail("no player"); return; }
			StartTest(GapsWatchRoutine(p, secs));
		}

		static IEnumerator GapsWatchRoutine(Network_Player p, float secs)
		{
			Stat_Health h = p.Stats.stat_health;
			h.Value = h.Max;
			gapsTook = 0f;
			gapsWatching = true;
			int id = ++gapsWatchId;
			float prev = h.Value;
			Log("WATCH started, health " + Gf(h.Value, "F1") + "/" + Gf(h.Max, "F1") + " (" + GWho() + ")");
			for (float t = 0; t < secs; t += Time.deltaTime)
			{
				yield return null;
				if (id != gapsWatchId) yield break;
				if (h.Value < prev - 0.001f) gapsTook += prev - h.Value;
				prev = h.Value;
			}
			gapsWatching = false;
		}

		[ConsoleCommand(name: "CIGapsWatchResult", docs: "Dev, in game (either player), IL4: the health this player lost since CIGapsWatchHealth: logs WATCH took X")]
		public static void GapsWatchResultCommand()
		{
			Network_Player p = RAPI.GetLocalPlayer();
			Log("WATCH took " + Gf(gapsTook) + (gapsWatching ? " (still watching)" : "") + ", health now " + (p != null && p.Stats != null ? Gf(p.Stats.stat_health.Value, "F1") : "?") + " (" + GWho() + ")");
			Log("PASS: watch result");
		}

		[ConsoleCommand(name: "CIGapsHitPlayer", docs: "Dev, in game (either player; player 2), IL4: hits the other player once for <damage>, as a weapon would (Raft's DamageEntity, from a player): CIGapsHitPlayer <damage>; logs HITPLAYER")]
		public static void GapsHitPlayerCommand(string[] args)
		{
			float dmg;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], NumberStyles.Float, GInv, out dmg)) { Fail("CIGapsHitPlayer <damage>"); return; }
			Network_Player me = RAPI.GetLocalPlayer();
			Network_Player other = Players.All.Where(x => x != null && x != me && x.Stats != null).OrderBy(x => me != null ? (x.transform.position - me.transform.position).sqrMagnitude : 0f).FirstOrDefault();
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (other == null || host == null) { Fail("no other player here"); return; }
			host.DamageEntity(other.Stats, other.transform, dmg, other.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("HITPLAYER " + other.steamID.Id + " for " + Gf(dmg, "F1") + ", Damage x" + Gf(PlayerLevels.Factor(LevelRules.Damage)) + ", friendly fire " + (GameManager.FriendlyFire ? "on" : "off") + " (" + GWho() + ")");
			Log("PASS: hit player");
		}

		[ConsoleCommand(name: "CIGapsBlockHit", docs: "Dev, in game (host), IL4: damages one sturdy raft block by <damage> through Raft's Block.Damage (the shark's bite) and puts its health back; lists the patches on Block.Damage: CIGapsBlockHit <damage>; logs BLOCKHIT")]
		public static void GapsBlockHitCommand(string[] args)
		{
			int dmg;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out dmg)) { Fail("CIGapsBlockHit <damage>"); return; }
			MethodInfo damage = AccessTools.Method(typeof(Block), "Damage", new[] { typeof(int) });
			MethodInfo setHealth = AccessTools.Method(typeof(Block), "SetHealth", new[] { typeof(int) });
			PropertyInfo health = AccessTools.Property(typeof(Block), "Health");
			if (damage == null || setHealth == null || health == null) { Fail("Block.Damage / SetHealth / Health not found"); return; }
			Func<Block, float> hp = b => Convert.ToSingle(health.GetValue(b, null), GInv);
			Block block = UnityEngine.Object.FindObjectsOfType<Block>().Where(b => b != null && b.gameObject.activeInHierarchy && hp(b) > dmg * 3).OrderByDescending(b => hp(b)).FirstOrDefault();
			if (block == null) { Fail("no raft block with more than " + dmg * 3 + " health"); return; }
			float before = hp(block);
			int expected = IronRaft.Scaled(block, dmg);
			damage.Invoke(block, new object[] { dmg });
			float after = hp(block);
			setHealth.Invoke(block, new object[] { Mathf.RoundToInt(before) });
			HarmonyLib.Patches info = HarmonyLib.Harmony.GetPatchInfo(damage);
			string owners = info == null ? "" : string.Join(" ", info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Select(x => x.PatchMethod.DeclaringType.Name).ToArray());
			Log("BLOCKHIT " + block.name + " before " + Gf(before, "F1") + " after " + Gf(after, "F1") + " took " + Gf(before - after, "F1") + " expected " + expected + ", Damage x" + Gf(PlayerLevels.Factor(LevelRules.Damage)) + ", patches [" + owners + "] (" + GWho() + ")");
			Log("PASS: block hit");
		}

		#endregion

		#region IM6, IM37, IM33: alphas, Big Bruce, den guards

		/// <summary>The biggest a kind is drawn by Raft (its prefab's localScaleInterval), as min and max; null when unknown here.</summary>
		static Vector2? PrefabScale(AI_NetworkBehaviourType type)
		{
			Network_Host_Entities host = ComponentManager<Network_Host_Entities>.Value;
			if (host == null || host.AINetworkBehaviourPrefabs == null) return null;
			AI_NetworkBehaviour p = host.AINetworkBehaviourPrefabs.FirstOrDefault(x => x != null && x.behaviourType == type);
			return p == null ? (Vector2?)null : new Vector2(p.localScaleInterval.minValue, p.localScaleInterval.maxValue);
		}

		static string AnimalLine(AI_NetworkBehaviour a)
		{
			Stat_Health h = a.networkEntity != null ? a.networkEntity.stat_health : null;
			float plain;
			bool hasPlain = CreatureSpawner.HealthBefore.TryGetValue(a, out plain);
			Vector2? range = PrefabScale(a.behaviourType);
			string variant;
			if (!WorldRandomizer.VariantOfIndex.TryGetValue(a.ObjectIndex, out variant)) variant = "none";
			return "#" + a.ObjectIndex + " " + a.behaviourType + " " + (a.networkEntity == null || a.networkEntity.IsDead ? "dead" : "alive") +
				", health " + (h != null ? Gf(h.Value, "F1") + "/" + Gf(h.Max, "F1") : "?/?") + ", plain " + (hasPlain ? Gf(plain, "F1") : "?") +
				", size " + Gf(a.transform.localScale.x, "F3") + ", base " + (range.HasValue ? Gf(range.Value.x, "F3") + "-" + Gf(range.Value.y, "F3") : "?-?") + ", variant " + variant + " (" + GWho() + ")";
		}

		[ConsoleCommand(name: "CIGapsAnimal", docs: "Dev, in game (either player), IM6/IM37/IM33: one animal by its network index as this machine has it: CIGapsAnimal <index>; logs ANIMAL #i Type alive|dead, health V/M, plain P, size S, base min-max, variant v")]
		public static void GapsAnimalCommand(string[] args)
		{
			uint idx;
			if (args == null || args.Length == 0 || !uint.TryParse(args[0], out idx)) { Fail("CIGapsAnimal <index>"); return; }
			AI_NetworkBehaviour a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.ObjectIndex == idx);
			if (a == null) { Log("ANIMAL #" + idx + " none here (" + GWho() + ")"); Log("PASS: animal"); return; }
			Log("ANIMAL " + AnimalLine(a));
			Log("PASS: animal");
		}

		[ConsoleCommand(name: "CIGapsBruce", docs: "Dev, in game (host), IM6: makes the nearest live shark Big Bruce the way a world does (a Wild seed that rolls it); logs BRUCE <index>")]
		public static void GapsBruceCommand()
		{
			if (!Raft_Network.IsHost) { Fail("host only"); return; }
			StartTest(GapsBruceRoutine());
		}

		static IEnumerator GapsBruceRoutine()
		{
			var shark = new AI_NetworkBehavior_Shark[1];
			var old = new RandomizerSettings[1];
			yield return BruceMake(shark, old);
			if (shark[0] == null) yield break; // (BruceMake failed already)
			Log("BRUCE " + shark[0].ObjectIndex);
			Log("PASS: bruce");
		}

		[ConsoleCommand(name: "CIGapsGuard", docs: "Dev, in game (either player), IM33: the guard of an island's den as this machine has it: CIGapsGuard <island>; logs GUARD #i Kind alive|dead, health V/M, plain P, size S, range a-b (Raft's draw x the spot's size)")]
		public static void GapsGuardCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			CreatureSpawnPoint guard = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).FirstOrDefault(s => ObjectProps.Get(s.Props, ObjectProps.CreatureZone) == "cave");
			if (guard == null || guard.Kind == null) { Fail("no den guard on '" + e.HostName + "'"); return; }
			Transform den = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => RaftProps.Get(t.name) != null && RaftProps.Get(t.name).IsCave);
			Vector3 middle = guard.transform.position;
			float reach = 25f;
			if (den != null) { PropInfo p = RaftProps.Get(den.name); middle = den.position + den.rotation * p.Centre; reach = Mathf.Max(p.Size.x, p.Size.z) * 0.5f + 15f; }
			AI_NetworkBehaviour a = Raft_Network.IsHost ? guard.Spawned.FirstOrDefault(x => x != null && x.networkEntity != null && !x.networkEntity.IsDead) : null;
			if (a == null)
				a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(x => x != null && x.behaviourType == guard.Kind.Type && x.networkEntity != null && !x.networkEntity.IsDead && FlatDistance(x.transform.position, middle) < reach)
					.OrderBy(x => FlatDistance(x.transform.position, middle)).FirstOrDefault();
			if (a == null) { Fail("no live " + guard.Kind.Label + " guard in the den of '" + e.HostName + "' (" + GWho() + ")"); return; }
			float size = ObjectProps.Size(guard.Props);
			Vector2? range = PrefabScale(a.behaviourType);
			Log("GUARD " + AnimalLine(a) + ", kind " + guard.Kind.Label + ", spot size " + Gf(size) + ", range " + (range.HasValue ? Gf(range.Value.x * size, "F3") + "-" + Gf(range.Value.y * size, "F3") : "?-?"));
			Log("PASS: guard");
		}

		[ConsoleCommand(name: "CIGapsHitIndex", docs: "Dev, in game (either player; player 2), IM33: one hit of <damage> on the animal with network index <index>, as a weapon (Raft's DamageEntity; a client's goes to the host): CIGapsHitIndex <index> <damage>; logs HITIDX")]
		public static void GapsHitIndexCommand(string[] args)
		{
			uint idx;
			float dmg;
			if (args == null || args.Length < 2 || !uint.TryParse(args[0], out idx) || !float.TryParse(args[1], NumberStyles.Float, GInv, out dmg)) { Fail("CIGapsHitIndex <index> <damage>"); return; }
			AI_NetworkBehaviour a = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().FirstOrDefault(x => x != null && x.ObjectIndex == idx);
			if (a == null || a.networkEntity == null) { Fail("no animal #" + idx + " here"); return; }
			float before = a.networkEntity.stat_health.Value;
			PutPlayerNear(a.transform, 3f);
			ComponentManager<Network_Host>.Value.DamageEntity(a.networkEntity, a.transform, dmg, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("HITIDX #" + idx + " for " + Gf(dmg, "F1") + ", before " + Gf(before, "F1") + ", Damage x" + Gf(PlayerLevels.Factor(LevelRules.Damage)) + " (" + GWho() + ")");
			Log("PASS: hit index");
		}

		#endregion

		#region IM35: one of Raft's islands reached by player 2 far from the host

		static Landmark LandmarkByKey(string key)
		{
			return WorldManager.AllLandmarks.FirstOrDefault(l => l != null && l.isSpawned && WorldRandomizer.SpawnKey(l).ToString() == key);
		}

		static bool HasRaftLoot(Landmark l) { return l.landmarkItems != null && l.landmarkItems.Any(WorldRandomizer.IsRaftLoot); }

		/// <summary>Moved crates and clams of one island: "i@x,z" of each still there, and the indices of those gone.</summary>
		static void MovedOf(Landmark l, out List<string> sig, out List<int> gone)
		{
			sig = new List<string>();
			gone = new List<int>();
			if (l.landmarkItems == null) return;
			for (int i = 0; i < l.landmarkItems.Length; i++)
			{
				LandmarkItem it = l.landmarkItems[i];
				if (!WorldRandomizer.IsRaftLoot(it)) continue;
				Vector3? o = WorldRandomizer.OriginalOf(l, it);
				if (!o.HasValue || (o.Value - it.transform.position).magnitude <= 1f) continue;
				PickupItem p = it.GetComponentInChildren<PickupItem>(true);
				if (!it.gameObject.activeInHierarchy || (p != null && !p.gameObject.activeInHierarchy)) gone.Add(i);
				else sig.Add(i + "@" + Gf(it.transform.position.x, "F1") + "," + Gf(it.transform.position.z, "F1"));
			}
		}

		[ConsoleCommand(name: "CIGapsFarIsland", docs: "Dev, in game (either player; player 2), IM35: goes to the nearest of Raft's plain islands with crates that is at least <metres> from every other player, and waits for its ground and the randomizer: CIGapsFarIsland <metres>; logs FAR <key> ... or SKIP:")]
		public static void GapsFarIslandCommand(string[] args)
		{
			float min;
			if (args == null || args.Length == 0 || !float.TryParse(args[0], NumberStyles.Float, GInv, out min)) min = 1000f;
			Network_Player me = RAPI.GetLocalPlayer();
			List<Network_Player> others = Players.All.Where(x => x != null && x != me).ToList();
			if (me == null || others.Count == 0) { Fail("two players needed"); return; }
			List<Landmark> all = WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned && WorldRandomizer.IsNatural(l) && HasRaftLoot(l)).ToList();
			Func<Landmark, float> away = l => others.Min(o => FlatDistance(o.transform.position, l.transform.position));
			Landmark far = all.Where(l => away(l) >= min).OrderBy(away).FirstOrDefault();
			if (far == null)
			{
				Log("SKIP: no plain island of Raft's with crates " + Gf(min, "F0") + " m or more from the other player here (" + all.Count + " spawned, farthest " + (all.Count > 0 ? Gf(all.Max(away), "F0") : "-") + " m)");
				return;
			}
			StartTest(GapsGotoLandmarkRoutine(far, "FAR"));
		}

		[ConsoleCommand(name: "CIGapsGotoLandmark", docs: "Dev, in game (either player), IM35: goes to one of Raft's islands by its key (WorldRandomizer.SpawnKey) and waits for its ground: CIGapsGotoLandmark <key>")]
		public static void GapsGotoLandmarkCommand(string[] args)
		{
			Landmark l = LandmarkByKey(args != null && args.Length > 0 ? args[0] : "");
			if (l == null) { Fail("no spawned island of Raft's with key '" + (args != null && args.Length > 0 ? args[0] : "") + "' here"); return; }
			StartTest(GapsGotoLandmarkRoutine(l, "AT"));
		}

		static IEnumerator GapsGotoLandmarkRoutine(Landmark l, string tag)
		{
			Network_Player me = RAPI.GetLocalPlayer();
			Network_Player other = Players.All.FirstOrDefault(x => x != null && x != me);
			float dist = other != null ? FlatDistance(other.transform.position, l.transform.position) : -1f;
			yield return PutPlayer(me, l.transform.position + Vector3.up * 3f, true);
			for (float t = 0; t < 40f && !WorldRandomizer.GroundOn(l); t += 1f) { KeepAlive(me); yield return new WaitForSeconds(1f); }
			RaycastHit hit;
			if (Physics.Raycast(l.transform.position + Vector3.up * 200f, Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore))
				yield return PutPlayer(me, hit.point + Vector3.up * 1.5f, false);
			// (the randomizer moves the crates once the ground is on here)
			List<string> sig = null;
			List<int> gone = null;
			for (float t = 0; t < 30f; t += 1f)
			{
				MovedOf(l, out sig, out gone);
				if (sig.Count + gone.Count > 0) break;
				KeepAlive(me);
				yield return new WaitForSeconds(1f);
			}
			Log(tag + " " + WorldRandomizer.SpawnKey(l) + " " + WorldRandomizer.KindOf(l) + ", " + Gf(dist, "F0") + " m from the other player, ground " + (WorldRandomizer.GroundOn(l) ? "on" : "off") + ", moved " + (sig.Count + gone.Count) + " (" + GWho() + ")");
			Log("Went to landmark " + WorldRandomizer.SpawnKey(l));
		}

		[ConsoleCommand(name: "CIGapsLandmark", docs: "Dev, in game (either player), IM35: one of Raft's islands as this machine has it - ground, its moved crates and clams (where, which are gone), its extras: CIGapsLandmark <key>; logs LANDMARK")]
		public static void GapsLandmarkCommand(string[] args)
		{
			string key = args != null && args.Length > 0 ? args[0] : "";
			Landmark l = LandmarkByKey(key);
			if (l == null) { Log("LANDMARK " + key + " not spawned here (" + GWho() + ")"); Log("PASS: landmark"); return; }
			Network_Player me = RAPI.GetLocalPlayer();
			List<string> sig;
			List<int> gone;
			MovedOf(l, out sig, out gone);
			IslandWorldState.Entry ex = IslandWorldState.Islands.LastOrDefault(x => WorldRandomizer.IsExtras(x) && WorldRandomizer.IslandAt(x.Position) == l);
			Log("LANDMARK " + key + " ground " + (WorldRandomizer.GroundOn(l) ? "on" : "off") + ", " + (me != null ? Gf(FlatDistance(me.transform.position, l.transform.position), "F0") : "?") + " m from me, moved " + (sig.Count + gone.Count) +
				", gone [" + string.Join(" ", gone.Select(i => i.ToString()).ToArray()) + "], sig [" + string.Join(" ", sig.ToArray()) + "], extras " + (ex == null ? "none" : ex.HostName + (ex.Root != null ? " loaded" : " unloaded")) + " (" + GWho() + ")");
			Log("PASS: landmark");
		}

		[ConsoleCommand(name: "CIGapsPickAt", docs: "Dev, in game (either player), IM35: picks up one moved crate or clam of one of Raft's islands, as a player would: CIGapsPickAt <key>; logs PASS: picked moved <key>/<i>")]
		public static void GapsPickAtCommand(string[] args)
		{
			string key = args != null && args.Length > 0 ? args[0] : "";
			Landmark l = LandmarkByKey(key);
			if (l == null || l.landmarkItems == null) { Fail("no island of Raft's with key '" + key + "' here"); return; }
			for (int i = 0; i < l.landmarkItems.Length; i++)
			{
				LandmarkItem it = l.landmarkItems[i];
				if (!WorldRandomizer.IsRaftLoot(it) || !it.gameObject.activeInHierarchy) continue;
				Vector3? o = WorldRandomizer.OriginalOf(l, it);
				PickupItem item = it.GetComponentInChildren<PickupItem>();
				if (!o.HasValue || (o.Value - it.transform.position).magnitude <= 1f || item == null) continue;
				Pickup pickup = RAPI.GetLocalPlayer().GetComponentInChildren<Pickup>(true);
				PutPlayerNear(item.transform);
				pickup.PickupItemByType(item, true);
				Log("Picked up the moved " + it.name + " " + key + "/" + i + " (" + GWho() + ")");
				Log("PASS: picked moved " + key + "/" + i);
				return;
			}
			Fail("no moved crate or clam to pick up on " + key);
		}

		#endregion
	}
}
