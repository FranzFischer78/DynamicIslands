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
	/// <summary>Helpers for the two-player test (the second Raft runs in Sandboxie, see TEST_PROTOCOL.md §5).</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIAlive", docs: "Dev: fills up the local player's hunger, thirst, health and oxygen, and respawns them if they are down")]
		public static void AliveCommand()
		{
			PlayerTrust.ForTests = true; // (CB2: the CI tests set players' records and use things from afar - CITrust turns it off)
			StartTest(AliveRoutine());
		}

		static System.Collections.IEnumerator AliveRoutine()
		{
			yield return EnsureAlive();
			Log("Alive: the player is up and fed");
		}

		/// <summary>
		/// One line per piece of shared state, in a fixed order, so the host's and the second player's logs can be
		/// compared line by line: role, the shared clock, islands (id, name, hash, where), the used objects' state
		/// per island (quest steps included), and the story items and journal pages.
		/// </summary>
		[ConsoleCommand(name: "CIMPState", docs: "Dev, two-player test: logs a summary of the shared state (islands, object state, quests, story items, clock) to compare between host and client")]
		public static void MPStateCommand()
		{
			bool host = Raft_Network.IsHost;
			var net = ComponentManager<Raft_Network>.Value;
			int players = net != null && net.remoteUsers != null ? net.remoteUsers.Count : 0;
			Log("MP role: " + (host ? "host" : "client") + ", players " + players + (Sandboxed ? " (Sandboxie)" : ""));
			Log("MP clock: " + SharedClock.Now.ToString("F1") + (SharedClock.Synced ? " (synced)" : " (local)"));
			Log("MP islands: " + IslandWorldState.Islands.Count);
			foreach (var e in IslandWorldState.Islands.OrderBy(i => i.Id))
			{
				// (harvested trees and picked-up items reach the entry when the island unloads or the world saves: now)
				if (e.Root != null) IslandObjectState.Capture(e);
				Log("MP island " + e.Id + " " + e.HostName + " hash " + (e.Hash ?? IslandNetwork.HashOf(e.Name)) + " at " + e.Position.ToString("F0") + (e.Label.Length > 0 ? " label '" + e.Label + "'" : "") +
					(e.WaitingForFile ? " (waiting for the file)" : e.Failed ? " (failed)" : e.Root == null ? " (unloaded)" : " (loaded)"));
				string state = string.Join(" ", e.State.OrderBy(kv => kv.Key)
					.Select(kv => kv.Key.ToString("X") + "=" + (kv.Value.Active ? "on" : "off") + "/" + kv.Value.Yield));
				Log("MP state " + e.Id + ": " + (state.Length > 0 ? state : "(none)"));
				IslandQuest q = QuestTracker.QuestOf(e);
				if (q.Steps.Count > 0) Log("MP quest " + e.Id + ": step " + QuestTracker.StepOf(e) + " of " + q.Steps.Count);
				if (e.Root != null)
				{
					// (Raft's own creatures: the host spawns them, Raft's network shows them to everyone)
					Vector3 at = e.Position;
					// (live ones: a body lingers longer on the host than on other players)
					var near = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null && (a.networkEntity == null || !a.networkEntity.IsDead) && Vector3.Distance(new Vector3(a.transform.position.x, 0f, a.transform.position.z), new Vector3(at.x, 0f, at.z)) < 300f).ToList();
					Log("MP creatures near " + e.Id + ": " + near.Count + (near.Count > 0 ? " (" + string.Join(", ", near.Select(a => a.name.Replace("(Clone)", "")).OrderBy(n => n).ToArray()) + ")" : ""));
				}
			}
			PlayerInventory inv = RAPI.GetLocalPlayer() != null ? RAPI.GetLocalPlayer().Inventory : null;
			// (per player, not shared: e.g. the planks a lever gave the player who pulled it)
			if (inv != null) Log("MP this player: " + string.Join(", ", new[] { "Plank", "Stone", "Rope", "Nail", "Scrap" }.Select(i => i + " x" + inv.GetItemCount(i)).ToArray()));
			Log("MP story items: " + string.Join(", ", StoryBook.Items.Select(h => h.Def.Id + " x" + h.Count).ToArray()));
			Log("MP story pages: " + string.Join(", ", StoryBook.Pages.Select(p => p.Key).ToArray()));
			Log("MP state done");
		}

		/// <summary>Empties the crew's story book here and, on the host, for every player (tests start and end clean).</summary>
		static void ResetStoryEverywhere()
		{
			StoryBook.Reset();
			if (Raft_Network.IsHost) IslandNetwork.SendStory(StoryBook.StateMessage());
		}

		/// <summary>
		/// The host spawns an island for the second player to play on, and keeps it: the sample island (trees and
		/// pickups) with a door and its lever, a chest with a story key, a vault the key opens, and a note.
		/// Its objects: door, lever (switches the door, gives 2 planks), vault (needs story:mp-key), note "MP note".
		/// </summary>
		[ConsoleCommand(name: "CIMPIsland", docs: "Dev, in game (host): spawns and keeps 'cimpworld', an island for the two-player test (door + lever, key chest + vault, a note, trees and pickups); CIMPIsland regrow <days>: with the island's own regrow rule")]
		public static void MPIslandCommand(string[] args)
		{
			int days;
			StartTest(MPIslandRoutine(args != null && args.Length > 1 && args[0] == "regrow" && int.TryParse(args[1], out days) ? days : -1));
		}

		static System.Collections.IEnumerator MPIslandRoutine(int regrow = -1)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandFile f = SampleIsland();
			f.Name = "cimpworld";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 20f);
			f.Props[StoryItems.Key] = "mp-key|Crew key||A key for the two-player test.";
			f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = ground(c.x + 6f, c.y), Props = P(BehaviourProps.Name, "door", BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "0.5") });
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x + 3f, c.y + 3f), Props = P(BehaviourProps.Name, "lever", BehaviourProps.Use, "Pull the lever",
				BehaviourProps.EventKey("use"), "switch|door|\nmessage||The door moves\ngive||" + ContentCatalog.PresetLoot(new[] { "Plank*2" })) });
			f.Objects.Add(new IslandObject { Name = "Loot_ChestSmall", Position = ground(c.x - 4f, c.y - 3f), Props = P(ObjectProps.LootItems, "story:mp-key*1") });
			f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = ground(c.x + 6f, c.y + 6f), Props = P(BehaviourProps.Name, "vault", BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "0.5",
				BehaviourProps.Use, "Open the vault", BehaviourProps.CheckKey("use"), "take|story:mp-key|1", BehaviourProps.ElseKey("use"), "message||It's locked.",
				BehaviourProps.EventKey("use"), "open||\nmessage||The key turns.\njournal|The vault|The crew opened the vault.") });
			f.Objects.Add(new IslandObject { Name = "Note_Paper", Position = ground(c.x - 2f, c.y + 4f), Props = P(ObjectProps.NoteTitle, "MP note", ObjectProps.NoteText, "Read by one player, in the crew's journal.") });
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x - 6f, c.y) + Vector3.up, Props = P(BehaviourProps.Move, "0,2,0", BehaviourProps.MoveTime, "3", BehaviourProps.MoveMode, "loop",
				BehaviourProps.Spin, "40", BehaviourProps.Collision, "none", BehaviourProps.Name, "mover") });
			f.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = ground(c.x, c.y - 8f), Props = P(ObjectProps.ZoneId, "mpzone", ObjectProps.ZoneRadius, "4",
				BehaviourProps.EventKey("enter"), "message||Zone entered\nsignal||mp-zone") });
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x + 10f, c.y + 8f), Props = P(ObjectProps.CreatureDamage, "0") });
			f.Props[IslandQuest.KeyTitle] = "Crew quest";
			f.Props[IslandQuest.KeySteps] = "reach|mpzone|1|\nread|MP note|1|";
			if (regrow >= 0) f.Props[IslandProps.RegrowDays] = regrow.ToString();
			f.Save(IslandSpawner.PathFor("cimpworld"));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("cimpworld"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			ResetStoryEverywhere();
			yield return DynamicIslands.instance.SpawnIslandFile("cimpworld", spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == "cimpworld");
			if (e == null || e.Root == null) { Fail("cimpworld did not spawn"); yield break; }
			Log("PASS: cimpworld spawned (island " + e.Id + ") at " + spot.Value.ToString("F0") + (regrow >= 0 ? ", its own rule: regrow " + IslandRules.RegrowDays(e) + " days" : "") + ": " + e.Root.GetComponentsInChildren<HarvestableTree>().Length + " trees, " +
				e.Root.GetComponentsInChildren<PickupItem_Networked>().Count(p => p.GetComponent<HarvestableTree>() == null) + " pickups");
		}

		/// <summary>Where the "mover" log of cimpworld is now (both players at the same moment: the same pose).</summary>
		[ConsoleCommand(name: "CIMoverPose", docs: "Dev, in game: the pose of the object named 'mover' on an island and the shared clock: CIMoverPose <island>")]
		public static void MoverPoseCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			IslandObjectRef r = e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(o => o.Name == "mover");
			if (r == null) { Fail("no 'mover' on '" + e.HostName + "'"); return; }
			Log("Mover at clock " + SharedClock.Now.ToString("F2") + ": local height " + (r.transform.position.y - e.Root.transform.position.y).ToString("F2") + ", turn " + r.transform.eulerAngles.y.ToString("F0"));
		}

		// What a player does, as either player (the client's goes through the network as a real player's would).
		// Each takes the island (part of its name) first, and puts the local player next to the object.

		static IslandWorldState.Entry LoadedIsland(string[] args)
		{
			string name = args != null && args.Length > 0 ? args[0] : "";
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
			if (e == null) Fail("no loaded island called '" + name + "' here");
			return e;
		}

		static void PutPlayerNear(Transform t, float offset = 1.5f)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || t == null) return;
			Vector3 target = t.position + new Vector3(offset, 0f, offset);
			RaycastHit hit;
			// (from just above the animal: from 30 m up the ray landed on a mast platform over a cove's puffer fish, in the
			// mast-top zone - Signal Rock's screechers woke early, SL6 2026-10-19)
			if (Physics.Raycast(target + Vector3.up * 3f, Vector3.down, out hit, 60f, 1 << IslandSpawner.TerrainLayer)) target.y = hit.point.y + 1.2f;
			// (off the raft first: a player still a child of the rocking raft was swung 14 m out of the gift zone - PlayerMove)
			PlayerMove.To(player, target);
			KeepAlive(player);
		}

		[ConsoleCommand(name: "CIMisplace", docs: "Dev, anywhere (either player): the next time Raft puts this player back (loading, joining), they are moved <dx> <dz> m off, as Raft once did: CIMisplace <dx> <dz>")]
		public static void MisplaceCommand(string[] args)
		{
			float dx = 0f, dz = 0f;
			if (args != null && args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dx);
			if (args != null && args.Length > 1) float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dz);
			PlayerHold.TestOffset = new Vector3(dx, 0f, dz);
			Log("Misplace set: the next place Raft gives is moved " + PlayerHold.TestOffset.ToString("F0"));
		}

		[ConsoleCommand(name: "CIHoldAtSea", docs: "Dev, in a world (either player): holds the player 150 m off the raft over the open sea, as a join once did (AU82) - they must be let go within 5 s")]
		public static void HoldAtSeaCommand()
		{
			StartTest(HoldAtSea());
		}

		static IEnumerator HoldAtSea()
		{
			Network_Player p = RAPI.GetLocalPlayer();
			if (p == null) { Fail("hold at sea: no player"); yield break; }
			Raft r = UnityEngine.Object.FindObjectOfType<Raft>();
			Vector3 at = (r != null ? r.transform.position : p.transform.position) + new Vector3(150f, 1.5f, 0f);
			PlayerHold.TestHold(at);
			float t0 = Time.unscaledTime;
			while (PlayerHold.Busy && Time.unscaledTime - t0 < 10f) yield return null;
			float took = Time.unscaledTime - t0;
			if (!PlayerHold.Busy && took < 5f) Log("PASS: hold at sea (let go after " + took.ToString("F1") + " s)");
			else Fail("hold at sea: still held after " + took.ToString("F1") + " s");
		}

		[ConsoleCommand(name: "CIOnRaft", docs: "Dev, in game (either player): puts the local player on the raft, as Raft does after loading far from it")]
		public static void OnRaftCommand()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("no local player"); return; }
			// (Raft's SetToWalkableBlockPosition didn't move a client - its placed-block list has no walkable blocks there:
			// down onto the deck from above the raft's middle)
			Vector3? middle = CustomIslandSpawner.RaftPosition;
			if (!middle.HasValue) { Fail("no raft"); return; }
			Vector3 target = middle.Value + Vector3.up * 1.5f;
			RaycastHit hit;
			if (Physics.Raycast(middle.Value + Vector3.up * 20f, Vector3.down, out hit, 40f, ~(1 << LayerMask.NameToLayer("LocalPlayer")), QueryTriggerInteraction.Ignore)) target.y = hit.point.y + 1.2f;
			// (off the raft first: a player still a child of the rocking raft was swung 14 m out of the gift zone - PlayerMove)
			PlayerMove.To(player, target);
			KeepAlive(player);
			Log("On the raft: " + (player.transform.position - middle.Value).magnitude.ToString("F0") + " m from its middle, standing on " + (hit.collider != null ? hit.collider.name : "nothing"));
		}

		[ConsoleCommand(name: "CIUse", docs: "Dev, in game (either player): uses an object as a player pressing E would: CIUse <island> <object name or index>")]
		public static void UseCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string what = args.Length > 1 ? args[1] : "";
			IslandObjectRef r = e.Root.GetComponentsInChildren<IslandObjectRef>(true)
				.FirstOrDefault(o => string.Equals(o.Name, what, StringComparison.OrdinalIgnoreCase) || o.Index.ToString() == what);
			if (r == null) { Fail("no object '" + what + "' on '" + e.HostName + "'"); return; }
			PutPlayerNear(r.transform);
			Behaviours.Fire(e, r.Index, "use", true);
			StartTest(AfterAction("Used '" + what + "' on '" + e.HostName + "' (" + (Raft_Network.IsHost ? "host" : "client") + ")"));
		}

		[ConsoleCommand(name: "CIGoto", docs: "Dev, in game (either player): puts the local player next to an object of the island (its name, or a trigger zone's id): CIGoto <island> <name>")]
		public static void GotoCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string what = args.Length > 1 ? args[1] : "";
			IslandObjectRef r = e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(o =>
				string.Equals(o.Name, what, StringComparison.OrdinalIgnoreCase) || string.Equals(ObjectProps.Get(o.Props, ObjectProps.ZoneId), what, StringComparison.OrdinalIgnoreCase));
			if (r == null) { Fail("no object or zone '" + what + "' on '" + e.HostName + "'"); return; }
			PutPlayerNear(r.transform, 0.3f); // (near the middle: a zone measures the distance in 3D, a slope adds to it)
			StartTest(StayThere(r.transform));
			StartTest(AfterAction("Went to '" + what + "' on '" + e.HostName + "' (" + (Raft_Network.IsHost ? "host" : "client") + ")", 2f)); // (zones check once a second)
		}


		/// <summary>
		/// After a test teleport: Raft sometimes moves the player on at once (a fall, its own spawn-point fix), before a zone
		/// there looks (4 times a second) - put them back, and say so.
		/// </summary>
		static IEnumerator StayThere(Transform target)
		{
			yield return new WaitForSeconds(0.6f);
			Network_Player p = RAPI.GetLocalPlayer();
			TriggerZone z = target != null ? UnityEngine.Object.FindObjectsOfType<TriggerZone>().OrderBy(x => (x.transform.position - target.position).sqrMagnitude).FirstOrDefault() : null;
			if (p == null || z == null || (z.transform.position - target.position).magnitude > 1f) yield break;
			float d = (p.transform.position - z.transform.position).magnitude;
			if (d <= z.Radius) yield break;
			Log("  (the player was moved " + d.ToString("F1") + " m away from zone '" + z.Id + "' at once: put there again; " + Whereabouts(p, z.transform.position) + ")");
			PutPlayerNear(target, 0.3f);
			yield return new WaitForSeconds(0.6f);
			if (p == null || z == null) yield break;
			d = (p.transform.position - z.transform.position).magnitude;
			if (d > z.Radius) Log("  (moved " + d.ToString("F1") + " m away again; " + Whereabouts(p, z.transform.position) + ")");
		}

		/// <summary>What may have moved a player off a test spot: their parent, what is under them, the raft's distance.</summary>
		static string Whereabouts(Network_Player p, Vector3 spot)
		{
			Vector3 at = p.transform.position;
			RaycastHit hit;
			string under = Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.collider.name + " (layer " + LayerMask.LayerToName(hit.collider.gameObject.layer) + ")" : "nothing";
			Raft raft = ComponentManager<Raft>.Value;
			return "now " + at.ToString("F1") + ", " + (at - spot).ToString("F1") + " from the spot, parent " + (p.transform.parent != null ? p.transform.parent.name : "none")
				+ ", under " + under + (raft != null ? ", raft " + (raft.transform.position - at).magnitude.ToString("F0") + " m" : "");
		}

		[ConsoleCommand(name: "CIChests", docs: "Dev, in game (either player): an island's chests as this machine has them, in CIOpenChest's order: CHEST <n> '<title>' opened|closed")]
		public static void ChestsCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			LootCrate[] chests = e.Root.GetComponentsInChildren<LootCrate>(false).OrderBy(c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null ? r.Index : 0; }).ToArray();
			for (int i = 0; i < chests.Length; i++)
			{
				IslandObjectRef r = chests[i].GetComponentInParent<IslandObjectRef>();
				Log("CHEST " + (i + 1) + " '" + (r != null ? ObjectProps.Get(r.Props, ObjectProps.NoteTitle) : "") + "' " + (chests[i].Looted ? "opened" : "closed"));
			}
			Log("PASS: chests");
		}
		[ConsoleCommand(name: "CIOpenChest", docs: "Dev, in game (either player): opens a chest as a player would: CIOpenChest <island> [n-th chest, from 1; default the first not yet opened]")]
		public static void OpenChestCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			// (hidden chests - buried treasure - can't be reached until an action shows them)
			LootCrate[] chests = e.Root.GetComponentsInChildren<LootCrate>(false).OrderBy(c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null ? r.Index : 0; }).ToArray();
			int n;
			string title = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "";
			Func<LootCrate, string> titleOf = c => { IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>(); return r != null ? ObjectProps.Get(r.Props, ObjectProps.NoteTitle) : ""; };
			LootCrate chest = title.Length == 0 ? chests.FirstOrDefault(c => !c.Looted)
				: int.TryParse(title, out n) ? chests.ElementAtOrDefault(n - 1)
				: chests.FirstOrDefault(c => titleOf(c).Equals(title, StringComparison.OrdinalIgnoreCase));
			if (chest == null) { Fail("no such chest on '" + e.HostName + "' (" + chests.Length + " chests)"); return; }
			PutPlayerNear(chest.transform);
			StartTest(OpenChestRoutine(e, chest));
		}

		static IEnumerator OpenChestRoutine(IslandWorldState.Entry e, LootCrate chest)
		{
			chest.LastGiven = new List<string>();
			List<string> got = chest.Open();
			// (a client opens when the host says yes: Claims)
			for (float t = 0; got.Count == 0 && !chest.Looted && t < 5f; t += 0.1f) { yield return new WaitForSeconds(0.1f); got = chest.LastGiven; }
			if (got.Count == 0) got = chest.LastGiven;
			NoteReader.Close();
			Log("Opened chest: got " + (got.Count > 0 ? string.Join(", ", got.ToArray()) : "nothing") + (chest.Looted ? "" : " (still closed)") + (Claims.LastAnswer != null ? " (claim " + Claims.LastAnswer + ")" : ""));
			yield return AfterAction("Opened a chest on '" + e.HostName + "' (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}

		[ConsoleCommand(name: "CIManyPlayers", docs: "Dev, anywhere: what the host decides for many players at once (up to Raft's 8), without them: a chest or a fire-once zone goes to the first of seven players asking (Claims) and is held for them, then used; an event that happens once (arrive, read) runs its shared part once however many ask; story items given by an action are the crew's (the host's, once)")]
		public static void ManyPlayersCommand() { StartTest(ManyPlayersRoutine()); }

		static IEnumerator ManyPlayersRoutine()
		{
			bool ok = true;
			// (an island of the test's own: nothing of a real world is touched)
			var e = new IslandWorldState.Entry { Id = 990001, Name = "cimanyplayers", HostName = "cimanyplayers" };
			int chest = ContentState.LootKeyBase + 3, zone = TriggerZone.KeyBase + 1;
			ulong[] players = { 76561190000000001UL, 76561190000000002UL, 76561190000000003UL, 76561190000000004UL, 76561190000000005UL, 76561190000000006UL, 76561190000000007UL };
			// Seven players ask for one chest in the same moment: the first gets it
			int grants = players.Count(p => Claims.HostGrant(e, chest, p));
			Check(ref ok, grants == 1, "7 players ask for one chest at once: " + grants + " granted (1 wanted)");
			Check(ref ok, Claims.HostGrant(e, chest, players[0]), "the player who has it asks again (a second press): still theirs");
			Check(ref ok, players.Skip(1).All(p => !Claims.HostGrant(e, chest, p)), "the others are still told no while it is held");
			// Another chest and a zone are separate
			Check(ref ok, Claims.HostGrant(e, chest + 1, players[4]) && Claims.HostGrant(e, zone, players[6]), "another chest and a zone go to other players at the same time");
			// Used: nobody gets it again, not even the one who had it
			e.State[chest] = new ObjectState { Active = false, Day = 1 };
			Check(ref ok, players.All(p => !Claims.HostGrant(e, chest, p)), "once it is used: no one gets it");
			// A hold that is never used (the player's checks failed: no key) ends, and someone else may try
			Check(ref ok, !Claims.HostGrant(e, zone, players[2]), "the zone held for another player: no");
			yield return new WaitForSecondsRealtime(6.5f);
			Check(ref ok, Claims.HostGrant(e, zone, players[2]), "the hold ends after a few seconds when the zone was never set off: the next player gets it");

			// Once-events: seven players arriving together - the shared part runs once; other events every time
			int arrive = Enumerable.Range(0, 7).Count(i => Behaviours.SharedOnce(e, Behaviours.IslandIndex, "arrive"));
			int read = Enumerable.Range(0, 7).Count(i => Behaviours.SharedOnce(e, 12, "read"));
			int readOther = Enumerable.Range(0, 7).Count(i => Behaviours.SharedOnce(e, 13, "read"));
			// (a use - a lever - runs every time, but seven players at it in the same second count once: AU18)
			int use = Enumerable.Range(0, 7).Count(i => Behaviours.SharedOnce(e, 12, "use"));
			Check(ref ok, arrive == 1 && read == 1 && readOther == 1 && use == 1, "shared parts: arrive " + arrive + ", a note read " + read + ", another note " + readOther + " (1 each), use " + use + " (1: seven in the same second count once)");

			// Story items from an action: the host's part (once for the crew); Raft's items go to each player
			var story = new ObjAction { Verb = "give", Arg = StoryItems.Ref("cikey") + "*1;Plank*3" };
			var plain = new ObjAction { Verb = "give", Arg = "Plank*3" };
			Check(ref ok, Behaviours.GivesStory(story) && !Behaviours.GivesStory(plain), "a give with a story item is done by the host, once; one of Raft's items only is each player's");
			if (ok) Log("PASS: many players"); else Fail("many players");
		}

		[ConsoleCommand(name: "CIReadNote", docs: "Dev, in game (either player): reads a note as a player would: CIReadNote <island> [part of its title]")]
		public static void ReadNoteCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string title = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "";
			CustomNote note = e.Root.GetComponentsInChildren<CustomNote>(true).FirstOrDefault(c => c.GetComponent<LootCrate>() == null && (c.Title ?? "").IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0);
			if (note == null) { Fail("no note '" + title + "' on '" + e.HostName + "'"); return; }
			PutPlayerNear(note.transform);
			NoteReader.Open(note);
			NoteReader.Close();
			StartTest(AfterAction("Read '" + note.Title + "' on '" + e.HostName + "'"));
		}

		/// <summary>One axe hit on a tree, as Raft's Axe.OnAxeHit does it: the host chops; a client sends Message_AxeHit to the host.</summary>
		[ConsoleCommand(name: "CIChop", docs: "Dev, in game (either player): one axe hit on a tree of the island, as Raft's axe does it: CIChop <island>")]
		public static void ChopCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			HarvestableTree tree = e.Root.GetComponentsInChildren<HarvestableTree>().FirstOrDefault(t => !t.Depleted && t.gameObject.activeInHierarchy);
			if (tree == null) { Fail("no tree to chop on '" + e.HostName + "'"); return; }
			Network_Player player = RAPI.GetLocalPlayer();
			PutPlayerNear(tree.transform);
			int ord = (int)(tree.PickupNetwork.ObjectIndex & 0xFFFF);
			var msg = new Message_AxeHit((Messages)102, player, player.steamID);
			msg.treeObjectIndex = (int)tree.PickupNetwork.ObjectIndex;
			msg.HitPoint = tree.transform.position + Vector3.up;
			msg.HitNormal = Vector3.up;
			if (Raft_Network.IsHost)
			{
				tree.Harvest(player.Inventory);
				player.Network.RPC(msg, Target.Other, Steamworks.EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
			}
			else player.SendP2P(msg, Steamworks.EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
			Log("Chopped tree #" + ord + " on '" + e.HostName + "' once (" + (Raft_Network.IsHost ? "host" : "client: sent to the host") + ")");
		}

		/// <summary>Picks up an item of the island through the local player's Pickup script (Raft sends a client's pickup to the host).</summary>
		[ConsoleCommand(name: "CIPick", docs: "Dev, in game (either player): picks up an item (rock, plant...) of the island as a player would: CIPick <island> [part of its name, e.g. GiantClam]")]
		public static void PickCommand(string[] args)
		{
			IslandWorldState.Entry e = LoadedIsland(args);
			if (e == null) return;
			string part = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "";
			// (the lowest id: the same pickup on both players - their child lists come in different orders, IM28)
			PickupItem_Networked pn = e.Root.GetComponentsInChildren<PickupItem_Networked>().OrderBy(p => p.ObjectIndex)
				.FirstOrDefault(p => p.GetComponent<HarvestableTree>() == null && p.gameObject.activeInHierarchy && p.GetComponent<PickupItem>() != null &&
					(part.Length == 0 || p.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 || (p.transform.parent != null && p.transform.parent.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)));
			if (pn == null) { Fail("nothing to pick up on '" + e.HostName + "'" + (part.Length > 0 ? " called '" + part + "'" : "")); return; }
			Pickup pickup = RAPI.GetLocalPlayer().GetComponentInChildren<Pickup>(true);
			if (pickup == null) { Fail("the player has no Pickup script"); return; }
			PutPlayerNear(pn.transform);
			int ord = (int)(pn.ObjectIndex & 0xFFFF);
			pickup.PickupItemByType(pn.GetComponent<PickupItem>(), true);
			Log("Picked up " + pn.name + " #" + ord + " on '" + e.HostName + "' (" + (Raft_Network.IsHost ? "host" : "client") + ")");
		}

		/// <summary>After an action: what the player was told, and the island's shared state here, half a second later.</summary>
		static System.Collections.IEnumerator AfterAction(string what, float wait = 0.5f)
		{
			yield return new WaitForSeconds(wait);
			// (a player who is down lies where they fell: teleports don't take - say so)
			Network_Player me = RAPI.GetLocalPlayer();
			Player body = me != null ? me.GetComponentInChildren<Player>(true) : null;
			bool down = me != null && ((me.Stats != null && me.Stats.stat_health.Value <= 0f) || (body != null && body.IsDead));
			Log(what + ": message '" + Behaviours.LastMessage + "', story items " + string.Join(", ", StoryBook.Items.Select(h => h.Def.Id + " x" + h.Count).ToArray()) + (down ? " (THE PLAYER IS DOWN)" : ""));
		}
	}
}
