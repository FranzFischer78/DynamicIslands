using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Tests for behaviours, events and actions, and collision (Behaviours.cs, BehaviourWindow.cs).</summary>
	public static partial class DevTests
	{
		static Dictionary<string, string> P(params string[] kv)
		{
			var d = new Dictionary<string, string>();
			for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
			return d;
		}

		#region Editor

		[ConsoleCommand(name: "CIBehaviourTest", docs: "Dev, editor: behaviours and events - actions read and written, the inspector group, the Behaviour & events window (screenshot), island events, invisible wall and ramp, undo, save and load")]
		public static void BehaviourTest()
		{
			StartTest(BehaviourTestRoutine());
		}

		static IEnumerator BehaviourTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;

			// Actions as text
			List<ObjAction> list = ObjAction.ParseLines("switch|door|\nmessage||The door creaks|open\ngive||Plank*2\nbogus|x|y\nsignal||vault");
			Check(ref ok, list.Count == 4 && list[1].Arg == "The door creaks|open" && list[3].Verb == "signal", "actions are read (a | inside a message is kept, an unknown verb is left out)");
			Check(ref ok, ObjAction.ParseLines(ObjAction.ToLines(list)).Select(a => a.Describe()).SequenceEqual(list.Select(a => a.Describe())), "actions written and read back are the same");
			Check(ref ok, list[0].Shared && !list[1].Shared && list[3].Shared, "shared and personal actions");
			var door = P(BehaviourProps.Name, "door", BehaviourProps.Turn, "90");
			Check(ref ok, BehaviourProps.Switches(door) && BehaviourProps.Any(door) && !BehaviourProps.Any(P(ObjectProps.NoteTitle, "x")), "a turning object is a door (opens and closes)");
			Check(ref ok, BehaviourProps.EventsFor(ContentCatalog.TriggerZone, null).Single().Key == "enter" && BehaviourProps.EventsFor("Loot_Chest", P(ObjectProps.LootItems, "")).Any(e => e.Key == "open") &&
				BehaviourProps.EventsFor("Creature_Boar", null).Single().Key == "defeat" && BehaviourProps.EventsFor(ContentCatalog.HelperWall, null).Count == 0, "which events each kind of object has");
			Check(ref ok, Behaviours.SignalKey("Vault") == Behaviours.SignalKey(" vault ") && Behaviours.SignalKey("vault") != Behaviours.SignalKey("vault2"), "signal keys");

			// Objects in the editor
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel, 500f);
			EditorGameObject lever = PlaceForTest("Log", c0, placed);
			EditorGameObject wall = PlaceForTest(ContentCatalog.HelperWall, c0 + new Vector3(6f, 0, 0), placed);
			EditorGameObject ramp = PlaceForTest(ContentCatalog.HelperRamp, c0 + new Vector3(-8f, 0, 0), placed);
			Check(ref ok, lever != null && wall != null && ramp != null, "a log, an invisible wall and an invisible ramp placed");
			if (lever == null || wall == null || ramp == null) yield break;
			Check(ref ok, wall.GetComponentsInChildren<Renderer>().Any() && wall.GetComponentsInChildren<Collider>().Length == 0, "the wall shows in the editor as a see-through slab");

			var leverProps = P(BehaviourProps.Name, "lever", BehaviourProps.Use, "Pull the lever", BehaviourProps.EventKey("use"), "switch|door|\nmessage||The door moves");
			PropsCommand.Change(lever, leverProps);
			ObjectInspector.Refresh();
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			DynamicIslands.EditorGizmoHandler.AddTarget(lever.transform, false); // (not an undo step: the undo below is the behaviour)
			yield return new WaitForSecondsRealtime(0.3f);
			Transform insp = EditorUI.Canvas.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Group_Behaviour & events");
			Check(ref ok, insp != null && insp.gameObject.activeInHierarchy, "the inspector has the Behaviour & events group");
			TextMesh tag = lever.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t => t.text.Contains("lever"));
			Check(ref ok, tag != null, "the object shows its name over it (" + (tag != null ? tag.text : "none") + ")");

			BehaviourWindow.Open(lever);
			yield return new WaitForSecondsRealtime(0.5f);
			Check(ref ok, BehaviourWindow.IsOpen, "the Behaviour & events window opens");
			Screenshot(new[] { "behaviour_window" });
			yield return new WaitForSecondsRealtime(0.5f);
			BehaviourWindow.Close();

			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, !BehaviourProps.Any(lever.Props), "undo takes the behaviour off");
			CommandUndoRedo.UndoRedoManager.Redo();
			Check(ref ok, ObjectProps.Get(lever.Props, BehaviourProps.Use) == "Pull the lever", "redo puts it back");

			// Island events
			var saved = new Dictionary<string, string>(DynamicIslands.currentIslandProps);
			DynamicIslands.currentIslandProps[BehaviourProps.EventKey("arrive")] = "message||Welcome";
			BehaviourWindow.OpenIsland();
			yield return new WaitForSecondsRealtime(0.4f);
			Screenshot(new[] { "island_events" });
			BehaviourWindow.Close();

			// Save and load keep it all
			DynamicIslands.SaveIsland("cibeh");
			IslandFile f = IslandFile.Load(IslandSpawner.PathFor("cibeh"));
			IslandObject lo = f.Objects.FirstOrDefault(o => o.Name == "Log");
			Check(ref ok, lo != null && ObjectProps.Get(lo.Props, BehaviourProps.EventKey("use")) == leverProps[BehaviourProps.EventKey("use")] && f.Objects.Count(o => ContentCatalog.IsHelper(o.Name)) == 2 &&
				ObjectProps.Get(f.Props, BehaviourProps.EventKey("arrive")) == "message||Welcome", "saved: the lever's actions, the wall and ramp, the island event");
			DynamicIslands.LoadIsland("cibeh");
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 5f && placed.GetComponentsInChildren<EditorGameObject>().Count(e => e.GameObjectName == "Log") == 0) yield return null;
			yield return new WaitForSecondsRealtime(0.3f);
			EditorGameObject back = placed.GetComponentsInChildren<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == "Log");
			Check(ref ok, back != null && ObjectProps.Get(back.Props, BehaviourProps.Name) == "lever", "loaded back with its behaviour");
			File.Delete(IslandSpawner.PathFor("cibeh"));
			DynamicIslands.currentIslandProps.Clear();
			foreach (var kv in saved) DynamicIslands.currentIslandProps[kv.Key] = kv.Value;
			DynamicIslands.NewIsland();
			if (ok) Log("PASS: behaviours in the editor"); else Fail("behaviours in the editor");
		}

		#endregion

		#region World

		[ConsoleCommand(name: "CIBehaviourWorld", docs: "Dev, in game (host): a lever opens a door, a zone wakes a hidden warthog and sends a signal a rule waits for, a chest teleports, hiding and showing the ambush spot, island arrival message, invisible wall and ramp, spinning, saved state")]
		public static void BehaviourWorld()
		{
			StartTest(BehaviourWorldRoutine());
		}

		static IEnumerator BehaviourWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;

			IslandFile f = SampleIsland();
			f.Name = "cibehworld";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 25f);
			int doorIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Block_Wall_Thatch", Position = ground(c.x + 6f, c.y), Props = P(BehaviourProps.Name, "door", BehaviourProps.Turn, "90", BehaviourProps.MoveTime, "1") });
			int leverIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x + 3f, c.y + 3f), Props = P(BehaviourProps.Name, "lever", BehaviourProps.Use, "Pull the lever",
				BehaviourProps.EventKey("use"), "switch|door|\nmessage||The door moves\ngive||" + ContentCatalog.PresetLoot(new[] { "Plank*2" })) });
			int spinIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x - 3f, c.y + 3f) + Vector3.up, Props = P(BehaviourProps.Spin, "90", BehaviourProps.Bob, "0.5", BehaviourProps.Collision, "none") });
			int ambushIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x + 10f, c.y + 8f), Props = P(ObjectProps.CreatureDamage, "0", BehaviourProps.Hidden, "1", BehaviourProps.Name, "ambush") });
			f.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = ground(c.x, c.y), Props = P(ObjectProps.ZoneId, "camp", ObjectProps.ZoneRadius, "4",
				BehaviourProps.EventKey("enter"), "show|ambush|\nsignal||camp-reached") });
			f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = ground(c.x - 4f, c.y - 3f), Props = P(ObjectProps.LootItems, "Nail*1", ObjectProps.NoteTitle, "Box", BehaviourProps.EventKey("open"), "teleport|lever|") });
			int wallIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = ContentCatalog.HelperWall, Position = ground(c.x - 10f, c.y), Scale = new Vector3(2f, 1f, 1f) });
			int hiderIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = "Log", Position = ground(c.x - 3f, c.y - 6f), Props = P(BehaviourProps.Use, "Wave the flag", BehaviourProps.EventKey("use"), "toggle|ambush|") });
			int rampIdx = f.Objects.Count;
			f.Objects.Add(new IslandObject { Name = ContentCatalog.HelperRamp, Position = ground(c.x, c.y - 12f) });
			f.Props[BehaviourProps.EventKey("arrive")] = "message||Welcome to the behaviour test";
			f.Props[WorldDirector.IslandRulesKey] = "sig | type:sandbar | signal:self:camp-reached | near:self:400:any | The signal brought a sandbar | Sandbar";
			f.Save(IslandSpawner.PathFor("cibehworld"));
			var created = new List<string> { "cibehworld" };

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("cibehworld"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("cibehworld", spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (e == null || e.Root == null) { Fail("the island did not spawn"); yield break; }
			Func<int, IslandObjectRef> obj = i => e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(r => r.Index == i);

			// What the objects became
			Check(ref ok, obj(doorIdx) != null && obj(doorIdx).GetComponent<IslandBehaviour>() != null, "the door moves (IslandBehaviour)");
			Check(ref ok, obj(leverIdx) != null && obj(leverIdx).GetComponentInChildren<UseInteract>(true) != null, "the lever can be used");
			Check(ref ok, obj(ambushIdx) != null && !obj(ambushIdx).gameObject.activeSelf, "the ambush warthog's spot is hidden");
			yield return new WaitForFixedUpdate();
			Physics.SyncTransforms(); // (Raft doesn't sync transforms at once: collider bounds and rays follow at the next physics step)
			// (walls and ramps without settings get no IslandObjectRef: find them by name)
			Transform w = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == ContentCatalog.HelperWall);
			BoxCollider wb = w != null ? w.GetComponentInChildren<BoxCollider>() : null;
			Check(ref ok, wb != null && wb.gameObject.layer == IslandSpawner.TerrainLayer && w.GetComponentsInChildren<Renderer>().Length == 0 && Mathf.Abs(wb.bounds.size.x - 8f) < 0.3f,
				"the invisible wall: a collider on the ground's layer, no renderer, scaled (" + (wb != null ? wb.bounds.size.ToString("F1") : "none") + ")");
			Transform rampRef = e.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == ContentCatalog.HelperRamp);
			RaycastHit hit;
			bool onRamp = rampRef != null && Physics.Raycast(rampRef.position + new Vector3(0f, 20f, 1f), Vector3.down, out hit, 40f, 1 << IslandSpawner.TerrainLayer) && hit.collider.transform.IsChildOf(rampRef);
			Check(ref ok, onRamp, "the invisible ramp can be stood on (a ray from above hits it)");
			IslandObjectRef spin = obj(spinIdx);
			Check(ref ok, spin != null && spin.GetComponentsInChildren<Collider>().All(col => !col.enabled || col.transform.name == CustomNote.HolderName), "'walk through' turns its collision off");
			Quaternion r0 = spin != null ? spin.transform.rotation : Quaternion.identity;

			yield return EnsureAlive();
			yield return StandRoutine(e.Root);
			yield return new WaitForSeconds(1.5f);
			// (over half a second: 90 degrees a second would wrap round over a longer time)
			r0 = spin != null ? spin.transform.rotation : Quaternion.identity;
			yield return new WaitForSeconds(0.5f);
			float turned = spin != null ? Quaternion.Angle(r0, spin.transform.rotation) : 0f;
			Check(ref ok, turned > 30f && turned < 60f, "the spinning log turns about 45 degrees in half a second (" + turned.ToString("F0") + ")");
			Check(ref ok, Behaviours.LastMessage == "Welcome to the behaviour test", "arriving shows the island's message (" + Behaviours.LastMessage + ")");

			var sent = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => sent.Add(m);
			PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
			int planks = inv.GetItemCount("Plank");
			try
			{
				// The lever: the door opens (shared, told to clients), a message and planks for this player
				Behaviours.Fire(e, leverIdx, "use", true);
				ObjectState s;
				Check(ref ok, e.State.TryGetValue(Behaviours.StateBase + doorIdx, out s) && s.Yield == 1, "using the lever opens the door (kept in the island's state)");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.ObjectSet && m.Index == doorIdx && m.Count == 3), "clients are told the door is open");
				Check(ref ok, Behaviours.LastMessage == "The door moves" && inv.GetItemCount("Plank") - planks == 2, "the player gets the message and 2 planks");
			}
			finally { IslandNetwork.Loopback = null; }
			Quaternion d0 = obj(doorIdx).transform.rotation;
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, Mathf.Abs(Quaternion.Angle(d0, obj(doorIdx).transform.rotation) - 90f) < 8f, "the door turned (" + Quaternion.Angle(d0, obj(doorIdx).transform.rotation).ToString("F0") + " degrees)");
			Screenshot(new[] { "behaviour_door" });

			// The zone: the warthog appears, the signal brings the island rule's sandbar
			e.Root.GetComponentsInChildren<TriggerZone>(true).First(z => z.Id == "camp").Enter();
			Check(ref ok, obj(ambushIdx).gameObject.activeSelf, "the zone shows the ambush spot");
			Check(ref ok, e.State.ContainsKey(Behaviours.SignalKey("camp-reached")), "the zone sent the signal 'camp-reached'");
			int count = IslandWorldState.Islands.Count;
			WorldDirector.Evaluate();
			IslandWorldState.Entry sand = IslandWorldState.Islands.Skip(count).FirstOrDefault();
			if (sand != null) created.Add(sand.HostName);
			Check(ref ok, sand != null && sand.HostName.StartsWith("gen-sandbar-"), "the signal made the island's rule bring a sandbar");
			float t0 = Time.realtimeSinceStartup;
			CreatureSpawnPoint boar = obj(ambushIdx).GetComponent<CreatureSpawnPoint>();
			while ((boar == null || boar.Spawned.Count == 0) && Time.realtimeSinceStartup - t0 < 30f) yield return new WaitForSeconds(0.5f);
			Check(ref ok, boar != null && boar.Spawned.Count == 1, "the hidden warthog appeared");
			// Hiding the spot again takes the warthog away (not defeated); showing it brings it back
			AI_NetworkBehaviour first = boar != null && boar.Spawned.Count > 0 ? boar.Spawned[0] : null;
			Behaviours.Fire(e, hiderIdx, "use", true);
			yield return new WaitForSeconds(2.5f);
			Check(ref ok, boar != null && boar.Spawned.Count == 0 && (first == null || !first.gameObject.activeInHierarchy) && !obj(ambushIdx).gameObject.activeSelf,
				"hiding the ambush spot takes its warthog away (not counted as defeated)");
			Behaviours.Fire(e, hiderIdx, "use", true);
			t0 = Time.realtimeSinceStartup;
			while ((boar == null || boar.Spawned.Count == 0) && Time.realtimeSinceStartup - t0 < 30f) yield return new WaitForSeconds(0.5f);
			if (boar == null || boar.Spawned.Count != 1) CreatureSpots();
			Check(ref ok, boar != null && boar.Spawned.Count == 1 && obj(ambushIdx).gameObject.activeSelf, "showing it again brings the warthog back");

			// The chest teleports the player to the lever
			yield return EnsureAlive();
			LootCrate chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault();
			if (chest != null) { chest.Open(); NoteReader.Close(); }
			yield return new WaitForSeconds(0.5f);
			Vector3 pp = RAPI.GetLocalPlayer().transform.position, lp = obj(leverIdx).transform.position;
			Check(ref ok, chest != null && new Vector2(pp.x - lp.x, pp.z - lp.z).magnitude < 2f, "opening the chest teleports the player to the lever");

			// A client's event: the host does the shared part (the door closes again)
			Behaviours.OnEventMessage(e.Id, leverIdx, "use", false);
			ObjectState ds;
			Check(ref ok, !e.State.TryGetValue(Behaviours.StateBase + doorIdx, out ds), "a client's lever use reaches the host: the door closes (back to how it was placed)");
			yield return new WaitForSeconds(1.2f); // (one use of a thing per second counts on the host: two players at one lever - AU18)
			Behaviours.Fire(e, leverIdx, "use", true); // open again for the reload check

			// Unloading and loading the island keeps the door open and the warthog's spot shown
			IslandObjectState.Capture(e);
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return null;
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			yield return new WaitForSeconds(0.5f);
			IslandBehaviour db = obj(doorIdx) != null ? obj(doorIdx).GetComponent<IslandBehaviour>() : null;
			Check(ref ok, db != null && db.Current == 1f && obj(ambushIdx).gameObject.activeSelf, "after reloading: the door is open at once and the ambush spot shown");
			Log(string.Join(", ", e.State.Keys.Where(k => k >= Behaviours.StateBase).Select(k => "0x" + k.ToString("X")).ToArray()));

			yield return new WaitForSeconds(1f);
			IslandWorldState.RemoveIds(IslandWorldState.Islands.Skip(before).Select(x => x.Id).ToList(), true);
			foreach (string n in created) if (File.Exists(IslandSpawner.PathFor(n))) File.Delete(IslandSpawner.PathFor(n));
			if (ok) Log("PASS: behaviours in a world"); else Fail("behaviours in a world");
		}

		#endregion
	}
}
