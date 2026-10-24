using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// The rest of the two-player test: an island with every in-world feature for the second player to play
	/// (tools\mpfull.ps1 drives it), and what a player does to creatures, zones and islands, through Raft's own paths.
	/// </summary>
	public static partial class DevTests
	{
		const string FullIsland = "cimpfull", VisitIsland = "cimpvisit";

		/// <summary>
		/// cimpfull: chests (plain, teleporting, locked + driftwood with its key), a gate lever that uses up a plank and
		/// falls shut after a wait, a gift zone, an ambush zone with a hidden warthog, a red warthog to defeat, a
		/// chicken to catch, gems (story items), a bell with "any of" checks and the rope that sends its signal, a
		/// treasure map (bottle, X zone, buried chest), a sign, an invisible wall, atmosphere and sound zones, a quest
		/// (reach, kill, catch, collect, pages) with a reward and an island "quest" event, and island rules (the quest
		/// and the bell's signal each bring a sandbar).
		/// </summary>
		[ConsoleCommand(name: "CIMPFull", docs: "Dev, in game (host): spawns and keeps 'cimpfull', an island with every in-world feature for the two-player test (tools\\mpfull.ps1)")]
		public static void MPFullCommand()
		{
			StartTest(MPFullRoutine());
		}

		static IEnumerator MPFullRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandFile f = SampleIsland();
			f.Name = FullIsland;
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> at = (dx, dz) =>
			{
				float x = c.x + dx, z = c.y + dz;
				int ix = Mathf.Clamp(Mathf.RoundToInt(x / step), 0, res - 1), iz = Mathf.Clamp(Mathf.RoundToInt(z / step), 0, res - 1);
				return new Vector3(x, f.Heights[iz, ix] * f.TerrainSize.y, z);
			};
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 30f);
			string loop = SoundLibrary.Events.FirstOrDefault(ev => ev.ToLowerInvariant().Contains("ambien") && SoundLibrary.IsLooping(ev)) ?? SoundLibrary.Events.FirstOrDefault(SoundLibrary.IsLooping) ?? "";
			Action<string, Vector3, Dictionary<string, string>> add = (name, pos, props) => f.Objects.Add(new IslandObject { Name = name, Position = pos, Props = props });

			f.Props[StoryItems.Key] = "gem|Gem||A green gem.\nsmall-key|Small key||A small key.\nmp-map|Treasure map||A map with an X on it.";
			add("Loot_Chest", at(-6f, -6f), P(ObjectProps.LootItems, "Plank*3;Rope*1", ObjectProps.NoteTitle, "Supplies"));
			add("Loot_ChestSmall", at(6f, -6f), P(ObjectProps.LootItems, "Nail*1", ObjectProps.NoteTitle, "Porter", BehaviourProps.EventKey("open"), "teleport|lever2|"));
			add("Block_Wall_Thatch", at(10f, 4f), P(BehaviourProps.Name, "gate", BehaviourProps.Move, "0,3,0", BehaviourProps.MoveTime, "0.3"));
			add("Log", at(8f, 8f), P(BehaviourProps.Name, "lever2", BehaviourProps.Use, "Pull the lever",
				BehaviourProps.CheckKey("use"), "take|Plank|1\nstate|gate|closed", BehaviourProps.ElseKey("use"), "message||It needs a plank.",
				BehaviourProps.EventKey("use"), "open|gate|\nwait||6\nclose|gate|\nmessage||The gate falls shut."));
			add(ContentCatalog.TriggerZone, at(-10f, 0f), P(ObjectProps.ZoneId, "gift", ObjectProps.ZoneRadius, "3", ObjectProps.ZoneMessage, "A gift!", ObjectProps.LootItems, "Rope*2"));
			add("Creature_Boar", at(0f, 16f), P(BehaviourProps.Name, "ambush", BehaviourProps.Hidden, "1", ObjectProps.CreatureDamage, "0"));
			add(ContentCatalog.TriggerZone, at(0f, 10f), P(ObjectProps.ZoneId, "trap", ObjectProps.ZoneRadius, "3", BehaviourProps.EventKey("enter"), "show|ambush|\nmessage||Ambush!"));
			add("Creature_Boar", at(-16f, 12f), P(BehaviourProps.Name, "hog", ObjectProps.CreatureDamage, "0", ObjectProps.CreatureSize, "1.4", ObjectProps.TintColor, "#FF3030",
				BehaviourProps.EventKey("defeat"), "message||The hog is down\njournal|Hog|The crew defeated the red hog."));
			add("Creature_Chicken", at(16f, 12f), P(ObjectProps.CreatureSize, "1.5"));
			add("Loot_Chest", at(-12f, -12f), P(ObjectProps.LootItems, "Rope*2", ObjectProps.NoteTitle, "Locked chest",
				BehaviourProps.CheckKey("open"), "take|story:small-key|1", BehaviourProps.ElseKey("open"), "message||The chest is locked."));
			add("Log", at(-16f, -8f), P(BehaviourProps.Name, "driftwood", BehaviourProps.Use, "Search the driftwood", BehaviourProps.CheckKey("use"), "!has|story:small-key|1",
				BehaviourProps.EventKey("use"), "give||story:small-key*1\nmessage||A small key!", BehaviourProps.ElseKey("use"), "message||Nothing else here."));
			add("Log", at(12f, -12f), P(BehaviourProps.Name, "bell", BehaviourProps.Use, "Ring the bell", BehaviourProps.CheckKey("use"), "any\nhas|story:gem|5\nsignal|bell-ok|",
				BehaviourProps.EventKey("use"), "message||The bell rings.", BehaviourProps.ElseKey("use"), "message||The bell is stuck."));
			add("Log", at(16f, -4f), P(BehaviourProps.Name, "rope", BehaviourProps.Use, "Pull the rope", BehaviourProps.EventKey("use"), "signal||bell-ok\nmessage||Something clicks."));
			add("Log", at(-4f, 20f), P(BehaviourProps.Name, "gems", BehaviourProps.Use, "Dig", BehaviourProps.CheckKey("use"), "!has|story:gem|2",
				BehaviourProps.EventKey("use"), "give||story:gem*2\nmessage||Two gems!", BehaviourProps.ElseKey("use"), "message||Nothing more here."));
			add("Note_Bottle", at(4f, 20f), P(ObjectProps.NoteTitle, "A message in a bottle", ObjectProps.NoteText, "A map is rolled up inside.", BehaviourProps.EventKey("read"), "give||story:mp-map*1"));
			add("Loot_ChestSmall", at(22f, 22f), P(BehaviourProps.Name, "buried", BehaviourProps.Hidden, "1", ObjectProps.LootItems, "Scrap*2", ObjectProps.NoteTitle, "Buried chest"));
			add(ContentCatalog.TriggerZone, at(22f, 22f), P(ObjectProps.ZoneId, "x", ObjectProps.ZoneRadius, "4", ObjectProps.ZoneRepeat, "1",
				BehaviourProps.CheckKey("enter"), "has|story:mp-map|1\n!state|buried|shown", BehaviourProps.EventKey("enter"), "show|buried|\nmessage||The map leads here!"));
			add("Note_Sign", at(0f, -12f), P(ObjectProps.NoteTitle, "Welcome sign", ObjectProps.NoteText, "Two players tested here."));
			f.Objects.Add(new IslandObject { Name = ContentCatalog.HelperWall, Position = at(-24f, 0f), Scale = new Vector3(2f, 1f, 1f) });
			add(ContentCatalog.AtmosphereZoneName, at(-22f, 18f), P(BehaviourProps.Name, "fog", ObjectProps.ZoneRadius, "8", ObjectProps.AtmoFog, "#8899AA", ObjectProps.AtmoParticles, "mist"));
			add(ContentCatalog.SoundZoneName, at(-22f, 18f), P(ObjectProps.ZoneRadius, "8", ObjectProps.SoundEvent, loop));
			f.Props[IslandProps.Title] = "Full test island";
			f.Props[IslandProps.Description] = "Everything, for two players.";
			f.Props[IslandQuest.KeyTitle] = "Full quest";
			f.Props[IslandQuest.KeySteps] = "reach|trap|1|\nkill|Warthog|1|\ncatch|Chicken|1|\ncollect|story:gem|2|\npages||1|";
			f.Props[IslandQuest.KeyReward] = ContentCatalog.PresetLoot(new[] { "Plank*4" });
			f.Props[IslandQuest.KeyDone] = "Full quest done!";
			f.Props[BehaviourProps.EventKey("quest")] = "message||The island thanks the crew";
			f.Props[WorldDirector.IslandRulesKey] =
				"q | type:sandbar | quest:self | near:self:400:any | The quest brought a sandbar | Quest sandbar\n" +
				"b | type:sandbar | signal:self:bell-ok | near:self:500:any | The bell brought a sandbar | Bell sandbar";
			f.Save(IslandSpawner.PathFor(FullIsland));

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(FullIsland), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			ResetStoryEverywhere();
			yield return DynamicIslands.instance.SpawnIslandFile(FullIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == FullIsland);
			if (e == null || e.Root == null) { Fail(FullIsland + " did not spawn"); yield break; }
			// (the creatures need the island's NavMesh first)
			float t0 = Time.realtimeSinceStartup;
			List<CreatureSpawnPoint> spots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).ToList();
			while (Time.realtimeSinceStartup - t0 < 60f && spots.Where(p => p.gameObject.activeInHierarchy).Any(p => p.Spawned.Count == 0)) yield return new WaitForSeconds(0.5f);
			Log("PASS: " + FullIsland + " spawned (island " + e.Id + ") at " + spot.Value.ToString("F0") + ", creatures " +
				string.Join(", ", spots.Select(p => p.Kind.Label + " " + p.Spawned.Count + (p.gameObject.activeInHierarchy ? "" : " (hidden)")).ToArray()));
		}

		/// <summary>cimpvisit: a small island 600 m ahead (the host's raft is too far to count as visiting) whose rule brings a sandbar when players first get there.</summary>
		[ConsoleCommand(name: "CIMPVisit", docs: "Dev, in game (host): spawns 'cimpvisit' 600 m ahead with an island rule 'when players first get here: bring a sandbar'")]
		public static void MPVisitCommand()
		{
			StartTest(MPVisitRoutine());
		}

		static IEnumerator MPVisitRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandFile f = System.IO.File.Exists(IslandSpawner.PathFor(TestIsland)) ? IslandFile.Load(IslandSpawner.PathFor(TestIsland)) : SampleIsland();
			f.Name = VisitIsland;
			f.Props[IslandProps.Title] = "Far island";
			f.Props[WorldDirector.IslandRulesKey] = "v | type:sandbar | visit:self | near:self:400:any | Someone reached the far island | Visit sandbar";
			f.Save(IslandSpawner.PathFor(VisitIsland));
			// (420-590 m out, any direction: far enough that the host on its raft doesn't count as visiting - land radius + 25 m - and near enough that other players load it: 600 m)
			Vector3? spot = null;
			for (int i = 0; i < 18 && !spot.HasValue; i++)
			{
				Vector3 dir = Quaternion.Euler(0f, i * 20f, 0f) * Vector3.forward;
				Vector3? s = CustomIslandSpawner.FindClearSpot(raftPos.Value + dir * 500f, CustomIslandSpawner.LandRadius(VisitIsland), 70f);
				if (s.HasValue && FlatDistance(s.Value, raftPos.Value) > 420f && FlatDistance(s.Value, raftPos.Value) < 590f) spot = s;
			}
			if (!spot.HasValue) { Fail("no open sea 420-590 m from the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(VisitIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == VisitIsland);
			if (e == null) { Fail(VisitIsland + " did not spawn"); yield break; }
			Log("PASS: " + VisitIsland + " spawned " + FlatDistance(e.Position, raftPos.Value).ToString("F0") + " m from the raft (land radius " + CustomIslandSpawner.LandRadius(VisitIsland).ToString("F0") + " m)");
		}

		[ConsoleCommand(name: "CIObj", docs: "Dev, in game (either player): how a named object is on this machine - shown or hidden, open or closed: CIObj <island> <name>")]
		public static void ObjCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string what = args.Length > 1 ? args[1] : "";
			List<IslandObjectRef> refs = e.Root.GetComponentsInChildren<IslandObjectRef>(true).Where(o => string.Equals(o.Name, what, StringComparison.OrdinalIgnoreCase)).ToList();
			if (refs.Count == 0) { Fail("no object '" + what + "' on '" + e.HostName + "'"); return; }
			foreach (IslandObjectRef r in refs)
			{
				ObjectState s;
				bool open = e.State.TryGetValue(Behaviours.StateBase + r.Index, out s) && s.Yield == 1;
				IslandBehaviour b = r.GetComponent<IslandBehaviour>();
				Log("Object '" + what + "' on '" + e.HostName + "': " + (r.gameObject.activeSelf ? "shown" : "hidden") + ", " + (open ? "open" : "closed") +
					(b != null ? ", mover target " + b.Target.ToString("F0") : "") + " (" + (Raft_Network.IsHost ? "host" : "client") + ")");
			}
		}

		[ConsoleCommand(name: "CIWhere", docs: "Dev, in game (either player): how far the local player is from a named object: CIWhere <island> <name>")]
		public static void WhereCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string what = args.Length > 1 ? args[1] : "";
			IslandObjectRef r = e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(o => string.Equals(o.Name, what, StringComparison.OrdinalIgnoreCase));
			Network_Player player = RAPI.GetLocalPlayer();
			if (r == null || player == null) { Fail("no object '" + what + "' on '" + e.HostName + "'"); return; }
			Log("The player is " + FlatDistance(player.transform.position, r.transform.position).ToString("F1") + " m from '" + what + "' (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}

		[ConsoleCommand(name: "CIGotoIsland", docs: "Dev, in game (either player): puts the local player on the highest point of an island: CIGotoIsland <island>")]
		public static void GotoIslandCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e != null) StartTest(StandRoutine(e.Root));
		}

		/// <summary>The animals near an island's creature spots of a kind (every machine: Raft shows the host's animals to everyone).</summary>
		/// (Hostile animals chase players far from their spot: anything of the spots' kinds on or near the island counts;
		/// "Warthog 1.4" also asks for that size, to tell two spots of one kind apart.)
		static List<AI_NetworkBehaviour> AnimalsOf(IslandWorldState.Entry e, string kind, float margin = 60f)
		{
			string[] k = (kind ?? "").Split(' ');
			float size = 0f;
			bool bySize = k.Length > 1 && float.TryParse(k[k.Length - 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out size);
			if (!bySize) size = 0f;
			string label = bySize ? string.Join(" ", k.Take(k.Length - 1).ToArray()) : kind ?? "";
			List<CreatureSpawnPoint> spots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null && (label.Length == 0 || p.Kind.Label.Equals(label, StringComparison.OrdinalIgnoreCase))).ToList();
			float reach = CustomIslandSpawner.LandRadius(e.Name) + margin;
			// (on the host: only what the island's own spots spawned - a generated island or Raft's own island with the
			// randomizer's extra hens 230 m off Camp Blackwater were counted with it in Silver Screen Seas, 2026-10-05)
			// (a boss phase's summons too - CIRfBossPhaseMp's host saw the beast but not its 2 summons, 2026-10-18)
			HashSet<AI_NetworkBehaviour> own = new HashSet<AI_NetworkBehaviour>(spots.SelectMany(p => p.Spawned.Concat(p.Summoned)).Where(a => a != null));
			bool byOwn = Raft_Network.IsHost && own.Count > 0;
			return UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null && (!byOwn || own.Contains(a)) && spots.Any(p => p.Kind.Type == a.behaviourType) &&
					FlatDistance(a.transform.position, e.Position) < reach && (!bySize || Mathf.Abs(a.transform.localScale.x - size) < 0.05f))
				.OrderBy(a => a.transform.position.x).ToList();
		}

		/// <summary>Each animal near the island: kind, health, size and tint (tint and size are what other players see too).</summary>
		[ConsoleCommand(name: "CICreatures", docs: "Dev, in game (either player): the animals of an island's creature spots - health, size, tint: CICreatures <island>")]
		public static void CreaturesCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			List<AI_NetworkBehaviour> all = AnimalsOf(e, "");
			foreach (AI_NetworkBehaviour a in all)
			{
				Renderer r = a.GetComponentsInChildren<Renderer>().FirstOrDefault(x => x is SkinnedMeshRenderer) ?? a.GetComponentsInChildren<Renderer>().FirstOrDefault();
				string tint = "none";
				if (r != null)
				{
					var block = new MaterialPropertyBlock();
					r.GetPropertyBlock(block);
					string prop = new[] { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Tint", "_AlbedoColor" }.FirstOrDefault(p => r.sharedMaterial != null && r.sharedMaterial.HasProperty(p));
					if (prop != null && !block.isEmpty) tint = "#" + ColorUtility.ToHtmlStringRGB(block.GetColor(prop));
					if (tint == "#FFFFFF") tint = "none"; // (white = untinted)
				}
				Network_Entity ne = a.networkEntity;
				Log("Creature " + a.behaviourType + ": " + (ne == null ? "no entity" : ne.IsDead ? "dead" : "alive") + ", health " + (ne != null && ne.stat_health != null ? ne.stat_health.Value.ToString("F0") + "/" + ne.stat_health.Max.ToString("F0") : "?") +
					", size " + a.transform.localScale.x.ToString("F2") + ", tint " + tint + ", id " + a.ObjectIndex);
			}
			Log("Creatures near '" + e.HostName + "': " + all.Count(a => a.networkEntity == null || !a.networkEntity.IsDead) + " alive (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}

		/// <summary>Hits the animals of a kind until they are down, through Raft's Network_Host.DamageEntity (a client's hit goes to the host).</summary>
		[ConsoleCommand(name: "CIHitOnce", docs: "Dev, in game (either player), IM15: one hit of <damage> on the first live animal of a kind at an island, as this player's weapon (Raft's DamageEntity, the Damage stat on top): CIHitOnce <island> <damage> <kind>; logs HITONCE <id> before <health>, x<Damage stat> x<game mode>")]
		public static void HitOnceCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			float dmg;
			if (args.Length < 3 || !float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dmg)) { Fail("CIHitOnce <island> <damage> <kind>"); return; }
			string kind = string.Join(" ", args.Skip(2).ToArray());
			AI_NetworkBehaviour a = AnimalsOf(e, kind, 150f).Where(x => x.networkEntity != null && !x.networkEntity.IsDead)
				// (the lowest id: the same animal on both players - their lists come in different orders, IM28)
				.OrderBy(x => x.ObjectIndex).FirstOrDefault();
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (a == null || host == null) { Fail("no live " + kind + " at '" + e.HostName + "'"); return; }
			SO_GameModeValue mode = GameModeValueManager.GetCurrentGameModeValue();
			float modeMul = mode != null && mode.playerSpecificVariables != null ? mode.playerSpecificVariables.outgoingDamageMultiplierPVE : 1f;
			float before = a.networkEntity.stat_health.Value;
			PutPlayerNear(a.transform);
			host.DamageEntity(a.networkEntity, a.transform, dmg, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("HITONCE " + a.ObjectIndex + " before " + before.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ", x" + PlayerLevels.Factor(LevelRules.Damage).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
				" x" + modeMul.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " (" + (Raft_Network.IsHost ? "host" : "client: sent to the host") + ")");
			Log("PASS: hit once");
		}

		[ConsoleCommand(name: "CIRaceCount", docs: "Dev, in game (either player), IM28: what a race of two players changes, counted before and after: CIRaceCount <island> <animal kind> <item part, * = any>; logs RACE kills <this player's kills> live <live animals of the kind> pickups <that item's pickups left on the island> item <its name> have <how many this player has>")]
		public static void RaceCountCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			if (args.Length < 3) { Fail("CIRaceCount <island> <animal kind> <item part>"); return; }
			string kind = args[1], part = string.Join(" ", args.Skip(2).ToArray());
			int live = AnimalsOf(e, kind, 150f).Count(a => a.networkEntity != null && !a.networkEntity.IsDead);
			List<PickupItem> left = e.Root.GetComponentsInChildren<PickupItem>()
				.Where(p => p.gameObject.activeInHierarchy && p.GetComponent<HarvestableTree>() == null && (part == "*" ||
					p.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 || (p.transform.parent != null && p.transform.parent.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
			// (the first networked one, as CIPick takes it; many give their item through a yield, not an itemInstance - then
			// "have" counts everything this player carries, IM28)
			PickupItem first = left.Where(p => p.GetComponent<PickupItem_Networked>() != null).OrderBy(p => p.GetComponent<PickupItem_Networked>().ObjectIndex).FirstOrDefault();
			string item = first != null && first.itemInstance != null && first.itemInstance.baseItem != null ? first.itemInstance.UniqueName : "";
			// (the first one's object name, as CIPick finds it: up to a space or bracket)
			string obj = first != null ? first.name.Split(' ', '(')[0] : "?";
			if (part != "*") left = left.Where(p => p.itemInstance != null && p.itemInstance.UniqueName == item).ToList();
			Network_Player me = RAPI.GetLocalPlayer();
			int have = me == null ? -1 : item.Length > 0 ? me.Inventory.GetItemCount(item) :
				me.Inventory.allSlots.Where(x => x != null && !x.IsEmpty && x.itemInstance != null).Sum(x => x.itemInstance.Amount);
			Log("RACE kills " + (PlayerLevels.On && PlayerLevels.Mine != null ? PlayerLevels.Mine.Kills : -1) + " live " + live + " pickups " + left.Count + " item " + (item.Length > 0 ? item : first != null ? "any" : "?") + " have " + have + " obj " + obj);
			Log("PASS: race count");
		}

		[ConsoleCommand(name: "CIHit", docs: "Dev, in game (either player): defeats the animals of a kind at an island, as a player's weapon would (Raft's DamageEntity): CIHit <island> <kind, e.g. Warthog>")]
		public static void HitCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string kind = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "";
			StartTest(HitRoutine(e, kind));
		}

		static IEnumerator HitRoutine(IslandWorldState.Entry e, string kind)
		{
			// (an ambush's animals come up a moment after the zone goes off - their NavMesh is built first: wait for them; 15 s
			// was once not enough, and the ambush warthog left alive knocked the players off their places)
			List<AI_NetworkBehaviour> animals = null;
			for (float until = Time.realtimeSinceStartup + 45f; Time.realtimeSinceStartup < until; )
			{
				animals = AnimalsOf(e, kind, 150f).Where(a => a.networkEntity != null && !a.networkEntity.IsDead).ToList(); // (hostile animals chase players well off their island)
				if (animals.Count > 0) break;
				yield return new WaitForSeconds(0.5f);
			}
			if (animals == null || animals.Count == 0)
			{
				// (what there is instead: every animal of the island's creature kinds anywhere, its size, state and distance)
				var kinds = new HashSet<AI_NetworkBehaviourType>(e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null).Select(p => p.Kind.Type));
				foreach (AI_NetworkBehaviour a in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null && kinds.Contains(a.behaviourType)))
					Log("  there is a " + a.behaviourType + " of size " + a.transform.localScale.x.ToString("F2") + ", " + (a.networkEntity == null ? "no entity" : a.networkEntity.IsDead ? "dead" : "alive") +
						", " + FlatDistance(a.transform.position, e.Position).ToString("F0") + " m from the island's middle (reach " + (CustomIslandSpawner.LandRadius(e.Name) + 150f).ToString("F0") + " m), at y " + a.transform.position.y.ToString("F1"));
				foreach (CreatureSpawnPoint p in e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true))
					Log("  spot " + (p.Kind != null ? p.Kind.Label : "?") + ": " + (p.gameObject.activeInHierarchy ? "shown" : "hidden") + ", spawned " + p.Spawned.Count);
				Fail("no live " + kind + " at '" + e.HostName + "'");
				yield break;
			}
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (host == null) { Fail("no Network_Host"); yield break; }
			PutPlayerNear(animals[0].transform);
			foreach (AI_NetworkBehaviour a in animals)
				host.DamageEntity(a.networkEntity, a.transform, 9999f, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
			Log("Hit " + animals.Count + " " + kind + "(s) at '" + e.HostName + "' (" + (Raft_Network.IsHost ? "host" : "client: sent to the host") + ")");
		}

		/// <summary>
		/// Catches a catchable animal of the island as Raft's net launcher and carrying do it: the net sets
		/// CaptureAnimal.IsCaptured (a client tells the host), then the player picks it up (Raft lets it go from its
		/// spawner: it belongs to the players) and puts it down.
		/// </summary>
		[ConsoleCommand(name: "CICatch", docs: "Dev, in game (either player): catches an island's catchable animal as the net launcher and carrying do: CICatch <island>")]
		public static void CatchCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e != null) StartTest(CatchRoutine(e));
		}

		static IEnumerator CatchRoutine(IslandWorldState.Entry e)
		{
			AI_NetworkBehaviour_Domestic animal = AnimalsOf(e, "").OfType<AI_NetworkBehaviour_Domestic>().FirstOrDefault(a => a.networkEntity == null || !a.networkEntity.IsDead);
			if (animal == null) { Fail("no catchable animal at '" + e.HostName + "'"); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			PutPlayerNear(animal.transform);
			yield return new WaitForSeconds(0.5f);
			if (animal.captureScript == null || !animal.captureScript.IsCapturable) { Fail("the " + animal.behaviourType + " can't be caught (capturable " + (animal.captureScript != null && animal.captureScript.IsCapturable) + ")"); yield break; }
			animal.captureScript.IsCaptured = true;
			Log("Netted a " + animal.behaviourType + " (" + (Raft_Network.IsHost ? "host" : "client") + ")");
			yield return new WaitForSeconds(2f);
			if (animal == null) { Fail("the animal is gone after netting"); yield break; }
			PutPlayerNear(animal.transform);
			// (a client's carry goes through the host and starts in Raft's delayed StartCarryDelayed: a stop sent a fixed 2 s
			// later could overtake it, and player 2 went on carrying the chicken - every later teleport of that session
			// snapped back. Wait for each step to arrive)
			if (animal.carryScript != null && animal.carryScript.OnStartCarry != null) animal.carryScript.OnStartCarry(player);
			float t0 = Time.realtimeSinceStartup;
			while (animal != null && animal.carryScript != null && !animal.carryScript.IsBeingCarried && Time.realtimeSinceStartup - t0 < 10f) yield return new WaitForSeconds(0.25f);
			bool carried = animal != null && animal.carryScript != null && animal.carryScript.IsBeingCarried;
			yield return new WaitForSeconds(1f);
			if (animal != null && animal.carryScript != null && animal.carryScript.OnStopCarry != null) animal.carryScript.OnStopCarry(player, false);
			t0 = Time.realtimeSinceStartup;
			while (animal != null && animal.carryScript != null && animal.carryScript.IsBeingCarried && Time.realtimeSinceStartup - t0 < 10f) yield return new WaitForSeconds(0.25f);
			yield return new WaitForSeconds(1f);
			if (!carried) { Fail("the " + (animal != null ? animal.behaviourType.ToString() : "animal") + " was never carried"); yield break; }
			if (animal != null && animal.carryScript != null && animal.carryScript.IsBeingCarried) { Fail("the " + animal.behaviourType + " is still carried after putting it down"); yield break; }
			Log("Caught a " + (animal != null ? animal.behaviourType.ToString() : "?") + ": state " + (animal != null ? animal.DomesticState.ToString() : "gone") + ", spawner " + (animal != null && animal.connectedSpawner != null ? "still the island's" : "none (it's the players')"));
		}

		/// <summary>Atmosphere and sound where the local player stands, the island's sign text and its invisible wall (what this machine shows).</summary>
		[ConsoleCommand(name: "CIZoneFx", docs: "Dev, in game (either player): the atmosphere and sound where the player is, the island's sign texts and invisible walls: CIZoneFx <island>")]
		public static void ZoneFxCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			float w = 0f;
			if (Camera.main != null) AtmosphereZone.Strongest(Camera.main.transform.position, out w);
			string sounds = string.Join(", ", e.Root.GetComponentsInChildren<SoundZone>(true).Select(s => s.Playing ? "playing" : "silent").ToArray());
			string signs = string.Join(", ", e.Root.GetComponentsInChildren<TextMesh>(true).Where(t => t.name == ContentCatalog.SignTextName).Select(t => "'" + t.text.Replace("\n", " ") + "'").ToArray());
			Transform wall = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == ContentCatalog.HelperWall);
			BoxCollider wb = wall != null ? wall.GetComponentInChildren<BoxCollider>() : null;
			bool blocks = false;
			if (wb != null)
			{
				Physics.SyncTransforms();
				Vector3 from = wb.bounds.center - wall.forward * 5f;
				RaycastHit hit;
				blocks = Physics.Raycast(from, wall.forward, out hit, 10f, 1 << IslandSpawner.TerrainLayer) && hit.collider == wb;
			}
			Log("Zone fx at the player: atmosphere " + w.ToString("F2") + ", sound zones " + sounds + "; signs " + signs + "; invisible wall " + (wb == null ? "missing" : (blocks ? "blocks" : "doesn't block") + ", " + wall.GetComponentsInChildren<Renderer>().Length + " renderers") +
				" (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}

		/// <summary>Makes everything used on this machine's islands that many days older (both players run it: days passing).</summary>
		[ConsoleCommand(name: "CIAge", docs: "Dev, in game (either player): makes the used objects' state of every island N days older, as if N days passed: CIAge <days>")]
		public static void AgeCommand(string[] args)
		{
			int days;
			if (args == null || args.Length == 0 || !int.TryParse(args[0], out days)) { Fail("usage: CIAge <days>"); return; }
			int n = 0;
			foreach (IslandWorldState.Entry e in IslandWorldState.Islands)
			{
				if (e.Root != null) IslandObjectState.Capture(e);
				foreach (ObjectState s in e.State.Values) { s.Day -= days; n++; }
			}
			Log("Aged " + n + " state entries by " + days + " days");
		}

		/// <summary>Unloads an island here and loads it again, as the streamer does when the raft sails away and back.</summary>
		[ConsoleCommand(name: "CIReloadIsland", docs: "Dev, in game (either player): unloads an island on this machine and loads it again, as sailing away and back does: CIReloadIsland <island>")]
		public static void ReloadIslandCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e != null) StartTest(ReloadIslandRoutine(e));
		}

		static IEnumerator ReloadIslandRoutine(IslandWorldState.Entry e)
		{
			IslandObjectState.Capture(e);
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return null;
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			Log("Reloaded '" + e.HostName + "' (" + (e.Root != null ? "loaded" : "FAILED") + ")");
		}
	}
}
