using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>Tests for the custom content: object settings (format 4), the inspector, notes and creatures.</summary>
	public static partial class DevTests
	{
		const string PropsIsland = "ciprops", CreatureIsland = "cicreature";

		/// <summary>Long test runs starve the test player (and warthogs bite): fill them up, and respawn them if they're down.</summary>
		static IEnumerator EnsureAlive()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) yield break;
			Player p = player.GetComponentInChildren<Player>(true);
			// (incapacitated, "waiting for rescue", is IsDead with some health left)
			Func<bool> down = () => (player.Stats != null && player.Stats.stat_health.Value <= 0f) || (p != null && p.IsDead);
			// (a respawn while the player is held over an island that is still loading - PlayerHold - didn't take: the player
			// stayed down, and every two-player check after it failed. Tried again until they are up)
			for (int tries = 0; tries < 3 && down() && p != null; tries++)
			{
				Log("The test player was down: respawning them (inventory kept)" + (tries > 0 ? " - again (" + (tries + 1) + ")" : ""));
				p.RespawnWithoutBed(false);
				for (float t = 0f; t < 6f && down(); t += 0.5f) yield return new WaitForSeconds(0.5f);
			}
			KeepAlive(player);
			if (down()) Log("The test player is STILL DOWN (health " + (player.Stats != null ? player.Stats.stat_health.Value.ToString("F0") : "?") + ", " + (p != null && p.IsDead ? "dead" : "not dead") + ")");
		}

		#region Editor: settings, inspector, file format 4

		[ConsoleCommand(name: "CIPropsTest", docs: "Dev, editor: creatures, notes and tints - placing, the inspector, undo/redo, duplicate, save and load (format 4)")]
		public static void PropsTest()
		{
			StartTest(PropsTestRoutine());
		}

		static EditorGameObject PlaceForTest(string name, Vector3 pos, Transform placed)
		{
			GameObject go = PlaceableCatalog.Spawn(name, placed);
			if (go == null) return null;
			go.transform.position = pos;
			foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = true;
			return EditorGameObject.Attach(go, name);
		}

		static IEnumerator PropsTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();

			var cats = PlaceableCatalog.Browse().ToDictionary(c => c.Key, c => c.Value.Count);
			Check(ref ok, ContentCatalog.Categories.All(cats.ContainsKey), "the object list has the categories " + string.Join(", ", ContentCatalog.Categories.Select(c => c + " (" + (cats.ContainsKey(c) ? cats[c] : 0) + ")").ToArray()));

			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject boar = PlaceForTest("Creature_Boar", c0, placed);
			EditorGameObject sign = PlaceForTest("Note_Sign", c0 + new Vector3(4, 0, 0), placed);
			string plainName = PlaceableCatalog.CoreNames.FirstOrDefault(n => PlaceableCatalog.CategoryOf(n) == PlaceableCatalog.NatureCategory && ContentCatalog.CreatureOf(n) == null);
			EditorGameObject plain = PlaceForTest(plainName, c0 + new Vector3(-4, 0, 0), placed);
			if (boar == null || sign == null || plain == null) { Fail("could not place a creature, a sign and " + plainName); yield break; }
			Check(ref ok, boar.transform.Find("Marker") != null || boar.transform.Find("Model") != null, "the warthog shows as " + (boar.transform.Find("Model") != null ? "Raft's model" : "a marker"));
			Check(ref ok, ObjectProps.Get(sign.Props, ObjectProps.NoteTitle) == "Sign", "a sign from the list starts as a note titled \"Sign\"");

			// Settings as undo steps
			PropsCommand.Change(boar, ObjectProps.With(boar.Props, ObjectProps.CreatureCount, "3"));
			PropsCommand.Change(boar, ObjectProps.With(ObjectProps.With(boar.Props, ObjectProps.CreatureHealth, "2"), ObjectProps.CreatureDamage, "1.5"));
			PropsCommand.Change(boar, ObjectProps.With(boar.Props, ObjectProps.TintColor, "#FF2020"));
			NoteEditorWindow.Apply(sign, "Welcome", "Line one\nLine two \u00E5\u00E4\u00F6");
			NoteEditorWindow.Apply(plain, "Hidden treasure", "Look under the palm.");
			TextMesh signText = sign.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t => t.name == ContentCatalog.SignTextName);
			Check(ref ok, signText != null && signText.text == "Welcome", "the sign board shows the note's title (" + (signText != null ? signText.text : "none") + ")");
			Check(ref ok, ObjectProps.Count(boar.Props) == 3 && ObjectProps.Health(boar.Props) == 2f && ObjectProps.HasTint(boar.Props), "creature settings: " + string.Join(", ", boar.Props.Select(kv => kv.Key + "=" + kv.Value).ToArray()));
			CommandUndoRedo.UndoRedoManager.Undo(); // the plain object's note
			CommandUndoRedo.UndoRedoManager.Undo(); // the sign's text
			bool undone = !ObjectProps.IsNote(plain.GameObjectName, plain.Props) && ObjectProps.Get(sign.Props, ObjectProps.NoteTitle) == "Sign";
			CommandUndoRedo.UndoRedoManager.Redo();
			CommandUndoRedo.UndoRedoManager.Redo();
			Check(ref ok, undone && ObjectProps.Get(sign.Props, ObjectProps.NoteText).StartsWith("Line one") && ObjectProps.IsNote(plain.GameObjectName, plain.Props), "undo and redo of note text");

			// Tint shows on the marker; labels show the settings
			Renderer body = boar.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name != ContentCatalog.MarkerOnly && !(r is ParticleSystemRenderer));
			var block = new MaterialPropertyBlock();
			if (body != null) body.GetPropertyBlock(block);
			Color shown = body != null && body.sharedMaterial != null && body.sharedMaterial.HasProperty("_Color") ? block.GetColor("_Color") : Color.clear;
			Check(ref ok, shown.r > shown.g * 2f, "the tint shows in the editor (" + shown + ")");
			TextMesh label = boar.GetComponentsInChildren<TextMesh>().FirstOrDefault();
			Check(ref ok, label != null && label.text.Contains("\u00D73"), "the marker's label shows the herd: " + (label != null ? label.text.Replace("\n", " / ") : "none"));
			TextMesh noteTag = plain.GetComponentsInChildren<TextMesh>().FirstOrDefault();
			Check(ref ok, noteTag != null && noteTag.text.Contains("Hidden treasure"), "a readable object shows its title in the editor");

			// Inspector
			TransformGizmoSelect(boar.transform);
			yield return null; yield return null;
			Check(ref ok, ObjectInspector.Visible && ObjectInspector.Target == boar, "selecting the warthog opens the creature editor");
			Screenshot(new[] { "props_creature" });
			yield return new WaitForSecondsRealtime(0.5f);
			TransformGizmoSelect(plain.transform);
			yield return null; yield return null;
			Check(ref ok, ObjectInspector.Visible && ObjectInspector.Target == plain, "selecting a readable object shows its note");
			Screenshot(new[] { "props_note" });
			yield return new WaitForSecondsRealtime(0.5f);
			NoteEditorWindow.Open(plain);
			yield return null;
			Screenshot(new[] { "props_noteeditor" });
			yield return new WaitForSecondsRealtime(0.5f);
			NoteEditorWindow.Close();

			// Duplicate keeps the settings
			TransformGizmoSelect(boar.transform);
			int copies = PlacementOptions.DuplicateSelection();
			EditorGameObject copy = DynamicIslands.EditorGizmoHandler.SelectedRoots.Select(t => t.GetComponent<EditorGameObject>()).FirstOrDefault();
			Check(ref ok, copies == 1 && copy != null && copy != boar && ObjectProps.Count(copy.Props) == 3 && ObjectProps.HasTint(copy.Props), "a copy keeps the creature's settings");
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);

			// Island info, save (format 4), wipe, load
			DynamicIslands.currentIslandProps[IslandProps.Title] = "Test Island";
			DynamicIslands.currentIslandProps[IslandProps.Author] = "CI";
			bool saved = DynamicIslands.SaveIsland(PropsIsland);
			int version;
			using (var r = new BinaryReader(File.OpenRead(IslandSpawner.PathFor(PropsIsland)))) { r.ReadUInt32(); version = r.ReadInt32(); }
			Check(ref ok, saved && version == 4, "saved as format " + version);
			var plainFile = new IslandFile { Name = "x", TerrainSize = new Vector3(10, 10, 10), HeightmapResolution = 33, Heights = new float[33, 33] };
			string tmp = Path.Combine(DynamicIslands.assetpath, "ciprops_plain.island");
			plainFile.Save(tmp);
			using (var r = new BinaryReader(File.OpenRead(tmp))) { r.ReadUInt32(); version = r.ReadInt32(); }
			File.Delete(tmp);
			Check(ref ok, version == 2, "an island without settings is still written as format 2 (older versions read it)");

			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			DynamicIslands.currentIslandProps.Clear();
			yield return null;
			DynamicIslands.LoadIsland(PropsIsland);
			yield return new WaitForSecondsRealtime(1f);
			List<EditorGameObject> back = placed.GetComponentsInChildren<EditorGameObject>().ToList();
			EditorGameObject b2 = back.FirstOrDefault(e => e.GameObjectName == "Creature_Boar");
			EditorGameObject s2 = back.FirstOrDefault(e => e.GameObjectName == "Note_Sign");
			EditorGameObject p2 = back.FirstOrDefault(e => e.GameObjectName == plainName);
			Check(ref ok, back.Count == 4 && b2 != null && ObjectProps.Count(b2.Props) == 3 && Mathf.Approximately(ObjectProps.Damage(b2.Props), 1.5f) && ObjectProps.ColorText(ObjectProps.Tint(b2.Props)) == "#FF2020",
				"the creatures' settings load back (" + back.Count + " objects)");
			Check(ref ok, s2 != null && ObjectProps.Get(s2.Props, ObjectProps.NoteText) == "Line one\nLine two \u00E5\u00E4\u00F6" && p2 != null && ObjectProps.Get(p2.Props, ObjectProps.NoteTitle) == "Hidden treasure",
				"note texts load back (with line breaks and \u00E5\u00E4\u00F6)");
			Check(ref ok, ObjectProps.Get(DynamicIslands.currentIslandProps, IslandProps.Title) == "Test Island" && ObjectProps.Get(DynamicIslands.currentIslandProps, IslandProps.Author) == "CI", "the island's name and author load back");

			// Loading an island while something is selected must not leave its settings on screen
			if (p2 != null)
			{
				TransformGizmoSelect(p2.transform);
				yield return null; yield return null;
				DynamicIslands.LoadIsland(PropsIsland);
				yield return new WaitForSecondsRealtime(0.5f);
				Check(ref ok, !ObjectInspector.Visible, "loading an island closes the inspector of the object that was selected");
			}

			File.Delete(IslandSpawner.PathFor(PropsIsland));
			if (ok) Log("PASS: object settings (creatures, notes, tints)"); else Fail("object settings (creatures, notes, tints)");
		}

		static void TransformGizmoSelect(Transform t)
		{
			var g = DynamicIslands.EditorGizmoHandler;
			g.ClearTargets(false);
			g.AddTarget(t, false);
		}

		#endregion

		#region World: creatures and notes

		[ConsoleCommand(name: "CICreatureProbe", docs: "Dev: what Raft offers for creatures here - its prefab list (in a world), their navigation, shaders; models in memory (editor)")]
		public static void CreatureProbe()
		{
			Network_Host_Entities host = null;
			try { host = ComponentManager<Network_Host_Entities>.Value; } catch { }
			AI_NetworkBehaviour[] prefabs = host != null ? host.AINetworkBehaviourPrefabs : Resources.FindObjectsOfTypeAll<AI_NetworkBehaviour>().Where(b => !b.gameObject.scene.IsValid()).ToArray();
			Log("Creature prefabs " + (host != null ? "in Network_Host_Entities" : "in memory (no world)") + ": " + (prefabs != null ? prefabs.Length : 0));
			if (prefabs != null)
				foreach (AI_NetworkBehaviour p in prefabs)
				{
					if (p == null) continue;
					NavMeshAgent agent = p.GetComponentInChildren<NavMeshAgent>(true);
					Renderer r = p.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x is SkinnedMeshRenderer || x is MeshRenderer);
					Material m = r != null ? r.sharedMaterial : null;
					string colorProp = m != null ? new[] { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Tint", "_AlbedoColor" }.FirstOrDefault(m.HasProperty) : null;
					float hp = -1f;
					try { hp = p.networkEntity != null && p.networkEntity.stat_health != null ? p.networkEntity.stat_health.Max : -1f; } catch { }
					Log(string.Format("  {0} ({1}): agent {2}, shader {3}, colour {4}, health {5}, scale {6}, offered {7}", p.behaviourType, p.GetType().Name,
						agent != null ? NavMesh.GetSettingsNameFromID(agent.agentTypeID) + "/" + agent.agentTypeID + " r=" + agent.radius.ToString("F2") + " speed " + agent.speed : "none",
						m != null ? m.shader.name : "-", colorProp ?? "none", hp, p.localScaleInterval != null ? p.localScaleInterval.minValue + "-" + p.localScaleInterval.maxValue : "?",
						ContentCatalog.CreatureOf("Creature_" + p.behaviourType) != null));
				}
			int mask = LayerMasks.MASK_RaycastInteractable;
			Log("Interactable layers: " + LayerList(mask) + "; rays hit triggers: " + Physics.queriesHitTriggers + "; NavMesh agent types: " + NavMesh.GetSettingsCount());
		}

		/// <summary>The sum of a damage field ("AI_State_X.field", as CreatureSpawner.ScaleDamage lists it) over root's components.</summary>
		static float DamageSum(GameObject root, string field)
		{
			int dot = field.IndexOf('.');
			string type = field.Substring(0, dot), name = field.Substring(dot + 1);
			float sum = 0f;
			foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (mb == null || mb.GetType().Name != type) continue;
				for (Type c = mb.GetType(); c != null && c != typeof(MonoBehaviour); c = c.BaseType)
				{
					var f = c.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
					if (f == null) continue;
					object v = f.GetValue(mb);
					sum += v is int ? (int)v : (float)v;
					break;
				}
			}
			return sum;
		}

		[ConsoleCommand(name: "CICreatureTest", docs: "Dev, in game (host): an island with creatures and notes 150 m ahead - spawning, stats, tint, NavMesh, an angler fish swimming its rounds, reading a note, the island banner, killing, unloading and reloading. CICreatureTest [keep]")]
		public static void CreatureTest(string[] args)
		{
			StartTest(CreatureTestRoutine(args != null && args.Contains("keep")));
		}

		/// <summary>Writes cicreature.island: the sample island with a herd of warthogs, a chicken, a puffer fish, an angler fish
		/// deeper down, a sign and an island name.</summary>
		static bool MakeCreatureIsland(out string error)
		{
			error = null;
			IslandFile f = SampleIsland();
			f.Name = CreatureIsland;
			f.Elevation = 0f;
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Vector2 c = IslandSpawner.LandCentre(f);
			Func<float, float, Vector3> ground = (x, z) =>
			{
				int ix = Mathf.Clamp(Mathf.RoundToInt(x / step), 0, res - 1), iz = Mathf.Clamp(Mathf.RoundToInt(z / step), 0, res - 1);
				return new Vector3(x, f.Heights[iz, ix] * f.TerrainSize.y, z);
			};
			// Clear the land centre of other objects so the animals have room
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 20f);
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x, c.y), Props = new Dictionary<string, string> {
				{ ObjectProps.CreatureCount, "2" }, { ObjectProps.CreatureHealth, "2" }, { ObjectProps.CreatureDamage, "1.5" }, { ObjectProps.CreatureSpeed, "1.5" }, { ObjectProps.TintColor, "#FF3030" } } });
			f.Objects.Add(new IslandObject { Name = "Creature_Chicken", Position = ground(c.x + 8f, c.y + 3f), Props = new Dictionary<string, string> { { ObjectProps.CreatureSize, "1.5" } } });
			// A puffer fish in the water off the coast
			Vector3 sea = ground(c.x, c.y);
			for (float d = 10f; d < 300f; d += 5f) { Vector3 p = ground(c.x + d, c.y); if (p.y < f.WaterLevel - 4f) { sea = new Vector3(p.x, f.WaterLevel - 3f, p.z); break; } }
			f.Objects.Add(new IslandObject { Name = "Creature_PufferFish", Position = sea });
			// An angler fish deeper down, 8 m under the surface where the sea is deeper than 14 m (Raft's swim their rounds)
			Vector3 deep = sea;
			for (float d = 10f; d < 300f; d += 5f) { Vector3 p = ground(c.x - d, c.y); if (p.y < f.WaterLevel - 14f) { deep = new Vector3(p.x, f.WaterLevel - 8f, p.z); break; } }
			f.Objects.Add(new IslandObject { Name = "Creature_AnglerFish", Position = deep });
			f.Objects.Add(new IslandObject { Name = "Note_Sign", Position = ground(c.x - 5f, c.y), Props = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Warning" }, { ObjectProps.NoteText, "Warthogs live here.\nBring a spear." } } });
			f.Props[IslandProps.Title] = "Warthog Hill";
			f.Props[IslandProps.Author] = "CI";
			f.Props[IslandProps.Description] = "Mind the red warthogs.";
			f.Props[IslandQuest.KeySteps] = "reach|cizone|1|"; // the banner only comes for an island with a quest (2026-10-19)
			f.Save(IslandSpawner.PathFor(CreatureIsland));
			return true;
		}

		static IEnumerator CreatureTestRoutine(bool keep)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			string error;
			if (!MakeCreatureIsland(out error)) { Fail(error); yield break; }
			IslandInfo.ForgetShown();
			bool ok = true;
			int exceptions = 0; string firstException = null;
			// (Unity's own "Screen position out of view frustum" is a camera drawing while Raft is minimised, as the tests run it - not the mod's)
			Application.LogCallback counter = (msg, trace, type) => { if (type == LogType.Exception || (type == LogType.Error && !msg.StartsWith("[CITEST]") && !msg.StartsWith("Screen position out of view frustum"))) { exceptions++; if (firstException == null) firstException = msg + " " + trace.Split('\n').FirstOrDefault(); } };
			Application.logMessageReceived += counter;

			// Clear of Raft's own islands (on top of one, players fall through the ground)
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(CreatureIsland), 390f);
			if (!spot.HasValue) { Application.logMessageReceived -= counter; Fail("no open sea near the raft for the test island"); yield break; }
			Vector3 pos = spot.Value;
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile(CreatureIsland, pos, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Application.logMessageReceived -= counter; Fail("the creature island did not spawn"); yield break; }
			List<CreatureSpawnPoint> points = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).ToList();
			Check(ref ok, points.Count == 4, points.Count + " creature spawn points (no markers in the world: " + (entry.Root.GetComponentsInChildren<Transform>(true).All(t => t.name != "Marker")) + ")");
			// (Raft's angler fish found no rounds to swim on a built island: it said so and its state threw every frame)
			int noRounds = 0;
			Application.LogCallback roundsCounter = (msg, trace, type) => { if (msg.Contains("Unable to find proper waypoint manager") || trace.Contains("AI_State_Waypoint_Circulation")) noRounds++; };
			Application.logMessageReceived += roundsCounter;

			float t0 = Time.realtimeSinceStartup;
			while (points.Sum(p => p.Spawned.Count) < 5 && Time.realtimeSinceStartup - t0 < 60f) yield return new WaitForSeconds(0.5f);
			yield return new WaitForSeconds(1f);
			foreach (CreatureSpawnPoint p in points)
				Log("  " + p.Kind.Label + ": " + p.Spawned.Count + " spawned " + string.Join(", ", p.Spawned.Where(a => a != null).Select(a => a.transform.position.ToString() + " hp " + a.networkEntity.stat_health.Value + "/" + a.networkEntity.stat_health.Max).ToArray()));
			CreatureSpawnPoint boars = points.FirstOrDefault(p => p.Kind.Type == AI_NetworkBehaviourType.Boar);
			CreatureSpawnPoint chicken = points.FirstOrDefault(p => p.Kind.Type == AI_NetworkBehaviourType.Chicken);
			CreatureSpawnPoint fish = points.FirstOrDefault(p => p.Kind.Type == AI_NetworkBehaviourType.PufferFish);
			Check(ref ok, boars != null && boars.Spawned.Count == 2, "two warthogs spawned (" + (Time.realtimeSinceStartup - t0).ToString("F1") + " s)");
			Check(ref ok, chicken != null && chicken.Spawned.Count == 1 && chicken.Spawned[0] is AI_NetworkBehaviour_Domestic, "a chicken spawned (catchable)");
			Check(ref ok, fish != null && fish.Spawned.Count == 1, "a puffer fish spawned in the sea");
			// The angler fish swims rounds around its spot: a ring of waypoints in open water, and it moves along them
			CreatureSpawnPoint anglers = points.FirstOrDefault(p => p.Kind.Type == AI_NetworkBehaviourType.AnglerFish);
			AI_NetworkBehaviour angler = anglers != null ? anglers.Spawned.FirstOrDefault(a => a != null) : null;
			Check(ref ok, angler != null, "an angler fish spawned in the deep");
			if (angler != null)
			{
				WaypointHandler rounds = CreatureSpawner.RoundsOf(angler);
				List<Waypoint> ring = CreatureSpawner.PointsOf(rounds);
				float sea = entry.Position.y + (entry.Root.GetComponent<IslandSettings>() != null ? entry.Root.GetComponent<IslandSettings>().WaterLevel : 0f);
				int clear = ring.Count(w => w != null && w.transform.position.y < sea - 1f && !Physics.CheckSphere(w.transform.position, 0.5f, 1 << IslandSpawner.TerrainLayer, QueryTriggerInteraction.Ignore));
				Check(ref ok, rounds != null && ring.Count == 6 && clear == 6, "the angler fish's rounds: " + ring.Count + " waypoints around its spot, " + clear + " of them in open water");
				Vector3 was = angler.transform.position;
				yield return new WaitForSeconds(4f);
				float moved = angler != null ? Vector3.Distance(was, angler.transform.position) : 0f;
				Check(ref ok, moved > 0.5f, "the angler fish swims its rounds: " + moved.ToString("F1") + " m in 4 s");
			}
			Application.logMessageReceived -= roundsCounter;
			Check(ref ok, noRounds == 0, "no 'Unable to find proper waypoint manager' and no errors from Raft's swimming rounds (" + noRounds + ")");
			NavMeshHit hit;
			Check(ref ok, boars != null && NavMesh.SamplePosition(boars.transform.position, out hit, 3f, NavMesh.AllAreas), "the island has a NavMesh at the warthogs' spot");

			Network_Host_Entities host = ComponentManager<Network_Host_Entities>.Value;
			AI_NetworkBehaviour boar = boars != null ? boars.Spawned.FirstOrDefault(a => a != null) : null;
			if (boar != null)
			{
				AI_NetworkBehaviour prefab = host.AINetworkBehaviourPrefabs.First(b => b != null && b.behaviourType == AI_NetworkBehaviourType.Boar);
				float rafts;
				bool known = CreatureSpawner.HealthBefore.TryGetValue(boar, out rafts);
				Check(ref ok, known && rafts > 0f && Mathf.Abs(boar.networkEntity.stat_health.Max - rafts * 2f) < 0.5f && Mathf.Approximately(boar.networkEntity.stat_health.Value, boar.networkEntity.stat_health.Max),
					"health \u00D72: " + boar.networkEntity.stat_health.Value + "/" + boar.networkEntity.stat_health.Max + " (Raft's warthog: " + (known ? rafts.ToString() : "?") + ")");
				AI_Movement move = boar.GetComponentInChildren<AI_Movement>(true);
				Check(ref ok, move != null && Mathf.Approximately(CreatureSpawner.SpeedOf(move), 1.5f), "speed \u00D71.5 registered (moving at " + (move != null ? move.MovementSpeed.ToString("F2") : "?") + ")");
				List<string> fields = CreatureSpawner.ScaleDamage(prefab.gameObject, 1f); // only lists the fields (x1)
				// (the spawned warthog's values against the prefab's: each field x1.5, an int rounded - CB13)
				var off = new List<string>();
				foreach (string fld in fields.Distinct())
				{
					float raftV = DamageSum(prefab.gameObject, fld), ours = DamageSum(boar.gameObject, fld);
					// (a field Raft fills in at run time, e.g. DamageBox.actualDamage, is 0 on the prefab: nothing to compare)
					if (raftV != 0f && Mathf.Abs(ours - raftV * 1.5f) > 0.51f * fields.Count(x => x == fld)) off.Add(fld + " " + raftV + "->" + ours);
				}
				Check(ref ok, fields.Count > 0 && off.Count == 0, "damage fields scaled \u00D71.5: " + string.Join(", ", fields.ToArray()) + (off.Count > 0 ? " - not \u00D71.5: " + string.Join(", ", off.ToArray()) : ""));
				Renderer r = boar.GetComponentsInChildren<Renderer>().FirstOrDefault(x => x is SkinnedMeshRenderer);
				var block = new MaterialPropertyBlock();
				if (r != null) r.GetPropertyBlock(block);
				string prop = r != null ? new[] { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Tint", "_AlbedoColor" }.FirstOrDefault(r.sharedMaterial.HasProperty) : null;
				Color col = prop != null ? block.GetColor(prop) : Color.clear;
				Check(ref ok, prop != null && col.r > col.g * 1.5f, "the warthog is tinted red (" + prop + " = " + col + ")");
				Check(ref ok, boar.connectedSpawner != null && boar.Serialize_Save() == null, "the warthog belongs to the island, not to Raft's world save");
			}

			// Read the note
			CustomNote note = entry.Root.GetComponentsInChildren<CustomNote>(true).FirstOrDefault();
			Check(ref ok, note != null && note.gameObject.layer != 0 && note.GetComponent<RaycastInteractable>() != null, "the sign is readable with Raft's interact key (layer " + (note != null ? LayerMask.LayerToName(note.gameObject.layer) : "-") + ")");
			if (note != null)
			{
				// (beside the sign, as a player reading it: the reader closes itself when the player is more than 8 m away - from
				// the raft it closed before the check when a frame took longer than its 0.2 s grace)
				Network_Player reader = RAPI.GetLocalPlayer();
				if (reader != null) yield return PutPlayer(reader, note.transform.position + Vector3.up * 1.5f + (reader.transform.position - note.transform.position).normalized * 2f, false);
				NoteReader.Open(note);
				yield return null;
				Check(ref ok, NoteReader.ShownTitle == "Warning" && NoteReader.ShownText == "Warthogs live here.\nBring a spear.", "the note opens: \"" + NoteReader.ShownTitle + "\"");
				Screenshot(new[] { "note_reader" });
				yield return new WaitForSeconds(1f);
				NoteReader.Close();
			}

			// Walk up to the island: its banner shows
			yield return StandRoutine(entry.Root);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, IslandInfo.LastShown != null && IslandInfo.LastShown.StartsWith("Warthog Hill"), "arriving shows the banner: " + IslandInfo.LastShown);
			{
				// An island with a title and no quest gets no banner tag; with a quest it does (settings are added either way)
				var noQ = new IslandFile { Name = "CI noquest" };
				noQ.Props[IslandProps.Title] = "No Quest Isle";
				GameObject tmp = new GameObject("CI noquest tag");
				IslandInfo.Tag(tmp, noQ);
				bool noBanner = tmp.GetComponent<IslandInfoTag>() == null && tmp.GetComponent<IslandSettings>() != null;
				UnityEngine.Object.Destroy(tmp);
				var withQ = new IslandFile { Name = "CI withquest" };
				withQ.Props[IslandProps.Title] = "Quest Isle";
				withQ.Props[IslandQuest.Key(IslandQuest.KeySteps, 3)] = "reach|cizone|1|";
				GameObject tmp2 = new GameObject("CI quest tag");
				IslandInfo.Tag(tmp2, withQ);
				bool banner = tmp2.GetComponent<IslandInfoTag>() != null;
				UnityEngine.Object.Destroy(tmp2);
				Check(ref ok, noBanner && banner, "a titled island without a quest has no banner tag, one with a quest has (" + noBanner + "/" + banner + ")");
			}
			Screenshot(new[] { "creatures" });
			yield return new WaitForSeconds(1f);

			// Kill a warthog: remembered
			if (boar != null)
			{
				boar.networkEntity.Damage(100000f, boar.transform.position, Vector3.up, EntityType.Player, true);
				yield return new WaitForSeconds(2.5f);
				ObjectState s;
				Check(ref ok, entry.State.TryGetValue(boars.StateKey, out s) && s.Yield == 1, "a killed warthog is remembered (" + (entry.State.ContainsKey(boars.StateKey) ? IslandObjectState.Encode(new Dictionary<int, ObjectState> { { boars.StateKey, entry.State[boars.StateKey] } }) : "no state") + ")");
			}

			// Unload: the animals go with the island; reload: the dead one stays dead
			List<AI_NetworkBehaviour> all = points.SelectMany(p => p.Spawned).Where(a => a != null).ToList();
			IslandObjectState.Capture(entry);
			IslandSpawner.Despawn(entry.Root);
			entry.Root = null;
			yield return new WaitForSeconds(0.5f);
			Check(ref ok, all.All(a => a == null) && CreatureSpawner.LiveCount == 0, "unloading the island removes its " + all.Count + " animals (" + all.Count(a => a != null) + " left)");
			// The automatic spawner loads it again (it is close to the raft)
			t0 = Time.realtimeSinceStartup;
			while (entry.Root == null && Time.realtimeSinceStartup - t0 < 60f) yield return new WaitForSeconds(0.5f);
			Check(ref ok, entry.Root != null && IslandSpawner.SpawnedRoots.Count(r => r != null && r.name == "CustomIsland_" + CreatureIsland) == 1, "the island comes back once (" + (Time.realtimeSinceStartup - t0).ToString("F1") + " s)");
			t0 = Time.realtimeSinceStartup;
			while (CreatureSpawner.LiveCount < 4 && Time.realtimeSinceStartup - t0 < 60f) yield return new WaitForSeconds(0.5f);
			yield return new WaitForSeconds(1f);
			CreatureSpawnPoint boars2 = entry.Root != null ? entry.Root.GetComponentsInChildren<CreatureSpawnPoint>().FirstOrDefault(p => p.Kind.Type == AI_NetworkBehaviourType.Boar) : null;
			Check(ref ok, boars2 != null && boars2.Spawned.Count == 1, "after reloading, one warthog (the other was killed): " + (boars2 != null ? boars2.Spawned.Count : -1));

			Application.logMessageReceived -= counter;
			Check(ref ok, exceptions == 0, "no errors (" + exceptions + (firstException != null ? ", first: " + firstException : "") + ")");
			if (!keep)
			{
				IslandWorldState.RemoveIds(new[] { entry.Id }, true);
				File.Delete(IslandSpawner.PathFor(CreatureIsland));
			}
			if (ok) Log("PASS: creatures and notes in a world" + (keep ? " (island kept)" : "")); else Fail("creatures and notes in a world");
		}

		#region Loot

		[ConsoleCommand(name: "CIItems", docs: "Dev: writes every item of Raft (unique name | shown name | has a picture) to Mods\\DynamicIslands\\items.txt, and checks the loot presets")]
		public static void ListItems()
		{
			var lines = ItemManager.GetAllItems().Where(i => i != null).OrderBy(i => i.UniqueName)
				.Select(i => i.UniqueName + " | " + ContentCatalog.ItemLabel(i.UniqueName) + " | " + (i.settings_Inventory != null && i.settings_Inventory.Sprite != null)).ToArray();
			File.WriteAllLines(Path.Combine(DynamicIslands.assetpath, "items.txt"), lines);
			Log(lines.Length + " items written to items.txt");
			foreach (var p in ContentCatalog.LootPresets)
			{
				string[] missing = p.Value.Select(x => x.Split('*')[0]).Where(n => !ContentCatalog.ItemExists(n)).ToArray();
				Log("Loot preset " + p.Key + ": " + ContentCatalog.PresetLoot(p.Value) + (missing.Length > 0 ? "   (not in Raft: " + string.Join(", ", missing) + ")" : ""));
			}
		}

		[ConsoleCommand(name: "CILootTest", docs: "Dev, editor: chests - the Loot & chests category, default loot, the item picker, amounts, undo, making any object a chest, save and load")]
		public static void LootTest()
		{
			StartTest(LootTestRoutine());
		}

		static IEnumerator LootTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			var cats = PlaceableCatalog.Browse().ToDictionary(c => c.Key, c => c.Value.Count);
			Check(ref ok, cats.ContainsKey(ContentCatalog.LootCategory) && cats[ContentCatalog.LootCategory] >= 5, "the object list has " + ContentCatalog.LootCategory + " (" + (cats.ContainsKey(ContentCatalog.LootCategory) ? cats[ContentCatalog.LootCategory] : 0) + ")");

			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject chest = PlaceForTest("Loot_Chest", c0, placed);
			string plainName = PlaceableCatalog.CoreNames.FirstOrDefault(n => n.StartsWith("TP_Moontown_TarpCrate"));
			EditorGameObject crate = PlaceForTest(plainName ?? PlaceableCatalog.CoreNames.First(), c0 + new Vector3(4, 0, 0), placed);
			if (chest == null || crate == null) { Fail("could not place a chest"); yield break; }
			List<KeyValuePair<string, int>> start = ObjectProps.Loot(chest.Props);
			Check(ref ok, start.Count >= 3 && start.All(l => ContentCatalog.ItemExists(l.Key)), "a new chest starts with Raft items: " + ObjectProps.Get(chest.Props, ObjectProps.LootItems));

			List<string> items = ItemManager.GetAllItems().Where(i => i != null && i.settings_Inventory != null && i.settings_Inventory.Sprite != null).Select(i => i.UniqueName).Take(40).ToList();
			ItemPickerWindow.AddItem(chest, items[20]);
			ItemPickerWindow.AddItem(chest, items[20]);
			List<KeyValuePair<string, int>> after = ObjectProps.Loot(chest.Props);
			Check(ref ok, after.Count == start.Count + 1 && after.Last().Key == items[20] && after.Last().Value == 2, "adding an item twice makes 2 of it (" + ContentCatalog.ItemLabel(items[20]) + ")");
			CommandUndoRedo.UndoRedoManager.Undo(); // both clicks are one step
			Check(ref ok, ObjectProps.Loot(chest.Props).Count == start.Count, "undo takes the added item out again");
			CommandUndoRedo.UndoRedoManager.Redo();

			// Any object can be a chest (with a note too)
			PropsCommand.Change(crate, ObjectProps.With(crate.Props, ObjectProps.LootItems, ContentCatalog.PresetLoot(ContentCatalog.LootPresets[1].Value)));
			NoteEditorWindow.Apply(crate, "Supplies", "Take what you need.");
			PropsCommand.Change(crate, ObjectProps.With(crate.Props, ObjectProps.LootRefill, "0"));
			Check(ref ok, ObjectProps.IsLoot(crate.GameObjectName, crate.Props) && ObjectProps.IsNote(crate.GameObjectName, crate.Props) && !ObjectProps.LootRefills(crate.Props), "a plain crate holds loot and a note, and never refills");
			TextMesh tag = crate.GetComponentsInChildren<TextMesh>().FirstOrDefault();
			Check(ref ok, tag != null && tag.text.Contains("Supplies") && tag.text.Contains("items"), "its name tag shows the note and the loot: " + (tag != null ? tag.text.Replace("\n", " / ") : "none"));

			// Inspector and the item picker
			TransformGizmoSelect(chest.transform);
			yield return null; yield return null;
			Check(ref ok, ObjectInspector.Visible && ObjectInspector.Target == chest, "selecting the chest opens its loot");
			Screenshot(new[] { "loot_inspector" });
			yield return new WaitForSecondsRealtime(0.5f);
			ItemPickerWindow.Open(chest);
			yield return null;
			Check(ref ok, ItemPickerWindow.IsOpen, "the item picker opens");
			Screenshot(new[] { "loot_picker" });
			yield return new WaitForSecondsRealtime(0.5f);
			ItemPickerWindow.Close();
			TransformGizmoSelect(crate.transform);
			yield return null; yield return null;
			Screenshot(new[] { "loot_crate" });
			yield return new WaitForSecondsRealtime(0.5f);
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);

			string chestLoot = ObjectProps.Get(chest.Props, ObjectProps.LootItems), crateLoot = ObjectProps.Get(crate.Props, ObjectProps.LootItems);
			bool saved = DynamicIslands.SaveIsland("ciloot");
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			DynamicIslands.LoadIsland("ciloot");
			yield return new WaitForSecondsRealtime(1f);
			List<EditorGameObject> back = placed.GetComponentsInChildren<EditorGameObject>().ToList();
			EditorGameObject c2 = back.FirstOrDefault(e => e.GameObjectName == "Loot_Chest"), k2 = back.FirstOrDefault(e => e.GameObjectName == crate.GameObjectName);
			Check(ref ok, saved && c2 != null && k2 != null && ObjectProps.Get(c2.Props, ObjectProps.LootItems) == chestLoot && ObjectProps.Get(k2.Props, ObjectProps.LootItems) == crateLoot && !ObjectProps.LootRefills(k2.Props),
				"the loot saves and loads back");
			File.Delete(IslandSpawner.PathFor("ciloot"));
			if (ok) Log("PASS: loot in the editor"); else Fail("loot in the editor");
		}

		[ConsoleCommand(name: "CILootWorld", docs: "Dev, in game (host): an island with two chests - opening gives the items, it stays empty (also after reloading), other players are told, refilling")]
		public static void LootWorld()
		{
			StartTest(LootWorldRoutine());
		}

		static IEnumerator LootWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			IslandFile f = SampleIsland();
			f.Name = "ciloot";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			string loot = ContentCatalog.PresetLoot(ContentCatalog.LootPresets[0].Value);
			f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = ground(c.x, c.y), Props = new Dictionary<string, string> { { ObjectProps.LootItems, loot } } });
			f.Objects.Add(new IslandObject { Name = "Loot_Barrel", Position = ground(c.x + 3f, c.y), Props = new Dictionary<string, string> { { ObjectProps.LootItems, "Plank*3" }, { ObjectProps.LootRefill, "0" }, { ObjectProps.NoteTitle, "Barrel note" } } });
			f.Save(IslandSpawner.PathFor("ciloot"));

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("ciloot"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("ciloot", spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Fail("the loot island did not spawn"); yield break; }
			List<LootCrate> crates = entry.Root.GetComponentsInChildren<LootCrate>().OrderBy(x => x.Ordinal).ToList();
			Check(ref ok, crates.Count == 2 && crates[0].GetComponent<RaycastInteractable>() != null && crates[1].GetComponent<CustomNote>() != null, crates.Count + " chests, interactable; the barrel also has a note");
			if (crates.Count < 2) yield break;

			// Open the chest: the items arrive in the inventory
			PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
			List<KeyValuePair<string, int>> want = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, loot } });
			Dictionary<string, int> had = want.ToDictionary(w => w.Key, w => inv.GetItemCount(w.Key));
			IslandNetMessage sent = null;
			IslandNetwork.Loopback = m => sent = m;
			List<string> given;
			try { given = crates[0].Open(); }
			finally { IslandNetwork.Loopback = null; }
			yield return null;
			// Anything that didn't fit was dropped, so count what arrived in the inventory
			int arrived = want.Count(w => inv.GetItemCount(w.Key) - had[w.Key] == w.Value);
			Check(ref ok, given.Count == want.Count && arrived > 0, "opening gives " + string.Join(", ", given.ToArray()) + " (" + arrived + " of " + want.Count + " kinds in the inventory, the rest dropped)");
			Check(ref ok, crates[0].Looted && crates[0].Open().Count == 0, "then it's empty (a second open gives nothing)");
			Check(ref ok, sent != null && sent.Kind == IslandNetMessage.ObjectUsed && sent.Ids[0] == entry.Id && sent.Index == crates[0].StateKey, "the other players are told (" + (sent != null ? "message " + sent.Kind + ", key " + sent.Index : "nothing sent") + ")");
			// A client receiving that message marks it too
			entry.State.Remove(crates[0].StateKey);
			ContentState.ApplyUsed(entry.Id, crates[0].StateKey, WorldManager.DayCounter); // (today: an older day could already be past the regrow time)
			Check(ref ok, crates[0].Looted, "a looted message from another player empties it here");

			// The barrel with a note: opens and shows the note
			// (at the barrel, as a player is: the reader closes when the player is more than 8 m away - after a long frame,
			// with a second player joined and a NavMesh being built, it had closed before the check)
			Network_Player me = RAPI.GetLocalPlayer();
			Vector3 wasAt = me != null ? me.transform.position : Vector3.zero;
			PutPlayerNear(crates[1].transform);
			crates[1].Open();
			yield return null;
			Check(ref ok, NoteReader.IsOpen && NoteReader.ShownTitle == "Barrel note", "a chest with a note shows the note when opened (" + (NoteReader.IsOpen ? "'" + NoteReader.ShownTitle + "'" : "reader closed") + ")");
			// (and back: the next test's island comes up in the same place, and its zones would go off under the player)
			if (me != null) { CharacterController cc = me.PersonController.controller; cc.enabled = false; me.transform.position = wasAt; cc.enabled = true; }
			NoteReader.Close();

			// Reload: still empty; after the regrow time the chest refills, the barrel (never) doesn't
			IslandObjectState.Capture(entry);
			IslandSpawner.Despawn(entry.Root);
			entry.Root = null;
			float t0 = Time.realtimeSinceStartup;
			while (entry.Root == null && Time.realtimeSinceStartup - t0 < 60f) yield return new WaitForSeconds(0.5f);
			crates = entry.Root != null ? entry.Root.GetComponentsInChildren<LootCrate>().OrderBy(x => x.Ordinal).ToList() : new List<LootCrate>();
			Check(ref ok, crates.Count == 2 && crates[0].Looted && crates[1].Looted, "after reloading the island both are still empty");
			int days = CustomIslandSpawner.RegrowDays;
			foreach (int key in new[] { ContentState.LootKeyBase, ContentState.LootKeyBase + 1 }) entry.State[key] = new ObjectState { Active = false, Day = -1000 };
			ContentState.OnIslandReady(entry);
			Check(ref ok, crates.Count == 2 && !crates[0].Looted && crates[1].Looted, "after " + days + " days the chest is full again; the barrel (never) stays empty");

			IslandWorldState.RemoveIds(new[] { entry.Id }, true);
			File.Delete(IslandSpawner.PathFor("ciloot"));
			if (ok) Log("PASS: loot in a world"); else Fail("loot in a world");
		}

		#endregion

		#region Trigger zones and island rules

		[ConsoleCommand(name: "CIZoneTest", docs: "Dev, editor: trigger zones - placing, the zone editor, linking a creature (ambush), renaming, the island rule, save and load")]
		public static void ZoneTest()
		{
			StartTest(ZoneTestRoutine());
		}

		static IEnumerator ZoneTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject zone = PlaceForTest(ContentCatalog.TriggerZone, c0, placed);
			EditorGameObject boar = PlaceForTest("Creature_Boar", c0 + new Vector3(6, 0, 0), placed);
			if (zone == null || boar == null) { Fail("could not place a zone and a warthog"); yield break; }
			string id = ObjectProps.Get(zone.Props, ObjectProps.ZoneId);
			Check(ref ok, id.StartsWith("zone-") && ContentCatalog.ZoneIdsInEditor().Contains(id), "a new zone gets a name (" + id + ")");
			PropsCommand.Change(zone, ObjectProps.With(ObjectProps.With(zone.Props, ObjectProps.ZoneRadius, "10"), ObjectProps.ZoneMessage, "You hear grunting..."));
			PropsCommand.Change(boar, ObjectProps.With(boar.Props, ObjectProps.CreatureZone, id));
			Transform sphere = zone.transform.Cast<Transform>().FirstOrDefault(t => t.name == ContentCatalog.MarkerOnly && t.GetComponent<TextMesh>() == null);
			Check(ref ok, sphere != null && Mathf.Approximately(sphere.localScale.x, 20f), "the sphere shows the 10 m radius");
			TextMesh bl = boar.GetComponentsInChildren<TextMesh>().FirstOrDefault();
			Check(ref ok, bl != null && bl.text.Contains("waits for " + id), "the warthog's tag says it waits: " + (bl != null ? bl.text.Replace("\n", " / ") : "none"));

			// Clicking in the zone's sphere still selects what is inside it
			Camera cam = Camera.main;
			cam.transform.position = c0 + new Vector3(6, 4, -8);
			cam.transform.LookAt(boar.transform.position + Vector3.up * 0.5f);
			Transform picked = PlacementOptions.PickObject(cam.ScreenPointToRay(cam.WorldToScreenPoint(boar.transform.position + Vector3.up * 0.5f)), ~0);
			Check(ref ok, picked == boar.transform, "clicking the warthog inside the zone selects the warthog (got " + (picked != null ? picked.name : "nothing") + ")");

			TransformGizmoSelect(zone.transform);
			yield return null; yield return null;
			Check(ref ok, ObjectInspector.Visible && ObjectInspector.Target == zone, "selecting the zone opens the zone editor");
			// Its Air row: "Air pocket" makes the zone an air pocket, "None" an ordinary zone again (the two did the opposite
			// when the row was new); the panel is built again after each click
			Func<string, Button> airButton = label => UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(b => b.transform.parent != null && b.transform.parent.name == "Air"
				&& b.GetComponentInChildren<Text>() != null && b.GetComponentInChildren<Text>().text == label);
			Check(ref ok, airButton("Air pocket") != null && airButton("None") != null, "the zone editor has an Air row: None / Air pocket");
			if (airButton("Air pocket") != null) { airButton("Air pocket").onClick.Invoke(); yield return null; yield return null; }
			Check(ref ok, ObjectProps.GetBool(zone.Props, ObjectProps.ZoneAir, false), "Air pocket makes the zone an air pocket (zone.air = '" + ObjectProps.Get(zone.Props, ObjectProps.ZoneAir) + "')");
			if (airButton("None") != null) { airButton("None").onClick.Invoke(); yield return null; yield return null; }
			Check(ref ok, !ObjectProps.GetBool(zone.Props, ObjectProps.ZoneAir, false), "None makes it an ordinary zone again (zone.air = '" + ObjectProps.Get(zone.Props, ObjectProps.ZoneAir) + "')");
			Screenshot(new[] { "zone_inspector" });
			yield return new WaitForSecondsRealtime(0.5f);
			TransformGizmoSelect(boar.transform);
			yield return null; yield return null;
			Screenshot(new[] { "zone_creature" });
			yield return new WaitForSecondsRealtime(0.5f);
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);

			// Island rule
			DynamicIslands.currentIslandProps[IslandProps.RegrowDays] = "5";
			bool saved = DynamicIslands.SaveIsland("cizone");
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			DynamicIslands.currentIslandProps.Clear();
			yield return null;
			DynamicIslands.LoadIsland("cizone");
			yield return new WaitForSecondsRealtime(1f);
			EditorGameObject z2 = placed.GetComponentsInChildren<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == ContentCatalog.TriggerZone);
			EditorGameObject b2 = placed.GetComponentsInChildren<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == "Creature_Boar");
			Check(ref ok, saved && z2 != null && b2 != null && ObjectProps.Get(z2.Props, ObjectProps.ZoneMessage) == "You hear grunting..." && ObjectProps.Get(b2.Props, ObjectProps.CreatureZone) == id
				&& ObjectProps.Get(DynamicIslands.currentIslandProps, IslandProps.RegrowDays) == "5", "the zone, the link and the island rule load back");
			File.Delete(IslandSpawner.PathFor("cizone"));
			if (ok) Log("PASS: trigger zones in the editor"); else Fail("trigger zones in the editor");
		}

		[ConsoleCommand(name: "CIBannerQueue", docs: "Dev (anywhere): banners and messages come one after another - a second one waits until the first has been up long enough to read (IslandInfo.ReadSeconds) and gone; the same one again while it is up shows once; a long text stays longer")]
		public static void BannerQueue() { StartTest(BannerQueueRoutine()); }

		static IEnumerator BannerQueueRoutine()
		{
			bool ok = true;
			IslandInfo.ForgetShown();
			for (float t = 0f; t < 15f && IslandInfo.OnScreen != null; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			IslandInfo.ShowMessage("CI first message");
			IslandInfo.ShowMessage("CI second message");
			IslandInfo.ShowMessage("CI first message");
			yield return null;
			Check(ref ok, IslandInfo.OnScreen == "CI first message", "the first message shows at once (" + IslandInfo.OnScreen + ")");
			float read = IslandInfo.ReadSeconds("", "CI first message");
			yield return new WaitForSecondsRealtime(read - 0.6f);
			Check(ref ok, IslandInfo.OnScreen == "CI first message", read.ToString("0.0") + " s to read it: still up just before, the second one waits (" + IslandInfo.OnScreen + ")");
			yield return new WaitForSecondsRealtime(1.2f);
			Check(ref ok, IslandInfo.OnScreen == "CI second message", "then the second one comes, after the first has gone (" + IslandInfo.OnScreen + ")");
			yield return new WaitForSecondsRealtime(IslandInfo.ReadSeconds("", "CI second message") + 0.5f);
			Check(ref ok, IslandInfo.OnScreen != "CI first message", "the first one, asked for twice, doesn't come again (" + (IslandInfo.OnScreen ?? "none up") + ")");
			float shortRead = IslandInfo.ReadSeconds("Island", "A short line."), longRead = IslandInfo.ReadSeconds("Island", new string('a', 200));
			Check(ref ok, shortRead >= 5f && longRead > shortRead + 5f && longRead <= 16f, "a long description stays longer (" + shortRead.ToString("0.0") + " s vs " + longRead.ToString("0.0") + " s)");
			IslandInfo.ForgetShown();
			if (ok) Log("PASS: banners one after another"); else Fail("banners one after another");
		}

		[ConsoleCommand(name: "CIStraightUnit", docs: "Dev (anywhere): objects placed standing straight (PlacementOptions.Straight, the placer's): a lean up to 25 degrees is taken off whichever of their own axes points up, the heading kept; a bigger lean is the object's look and kept")]
		public static void StraightUnit()
		{
			bool ok = true;
			foreach (float heading in new[] { 0f, 37f, 200f })
				foreach (Vector3 lean in new[] { new Vector3(14f, 0f, 0f), new Vector3(0f, 0f, -20f), new Vector3(9f, 0f, 12f) })
				{
					Quaternion r = Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(lean), s = PlacementOptions.Straight(r);
					float up = Vector3.Angle(s * Vector3.up, Vector3.up);
					float turned = Vector3.Angle(Vector3.ProjectOnPlane(r * Vector3.forward, Vector3.up), Vector3.ProjectOnPlane(s * Vector3.forward, Vector3.up));
					Check(ref ok, up < 0.05f && turned < 3f, "heading " + heading + ", lean " + lean + ": straight (" + up.ToString("F2") + " deg off), heading kept (" + turned.ToString("F1") + " deg)");
				}
			Quaternion big = Quaternion.Euler(40f, 30f, 0f);
			Check(ref ok, Quaternion.Angle(PlacementOptions.Straight(big), big) < 0.05f, "a 40 degree lean is the object's look: kept");
			// (an object whose own z points up - lying in its model - leaning 10 degrees: its z stands straight up)
			Quaternion lying = Quaternion.Euler(-80f, 0f, 0f), ls = PlacementOptions.Straight(lying);
			Check(ref ok, Vector3.Angle(ls * Vector3.forward, Vector3.up) < 0.05f, "an object whose own z points up stands on it (" + Vector3.Angle(ls * Vector3.forward, Vector3.up).ToString("F2") + " deg off)");
			if (ok) Log("PASS: objects placed standing straight"); else Fail("objects placed standing straight");
		}

		[ConsoleCommand(name: "CIDrawnInPlace", docs: "Dev, editor: every object of the catalog is drawn where it stands - copies placed, then moved 100 m: each of their meshes moves with them (Raft's statically batched scenery drew where it stands in Raft's own scene: the Stranded Gull's boat was invisible, its locker floating). Lists the batched objects and any still drawn elsewhere. CIDrawnInPlace [all: every one of Raft's island scenes loaded first]")]
		public static void DrawnInPlace(string[] args) { StartTest(DrawnInPlaceRoutine(args != null && args.Contains("all"))); }

		static IEnumerator DrawnInPlaceRoutine(bool all)
		{
			yield return WaitForEditor(false);
			if (all) yield return PlaceableCatalog.EnsureLoaded(PlaceableCatalog.IndexedNames.ToList());
			bool ok = true;
			Transform parent = new GameObject("CI_DrawnInPlace").transform;
			Vector3 at = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 60f, 500f), step = new Vector3(100f, 0f, 0f);
			var elsewhere = new List<string>();
			var batched = new List<string>();
			List<string> names = PlaceableCatalog.Names.ToList();
			int checkedCount = 0;
			// (in chunks: placed, a frame, their meshes' places read; moved 100 m, a frame, read again)
			for (int start = 0; start < names.Count; start += 100)
			{
				var copies = new List<KeyValuePair<string, GameObject>>();
				foreach (string name in names.Skip(start).Take(100))
				{
					GameObject go = null;
					try { go = PlaceableCatalog.Spawn(name, parent); } catch (Exception e) { Log("  " + name + ": " + e.Message); }
					if (go == null) continue;
					go.transform.position = at;
					copies.Add(new KeyValuePair<string, GameObject>(name, go));
				}
				yield return null;
				Func<GameObject, MeshRenderer[]> meshes = go => go.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null).ToArray();
				var before = copies.Select(c => meshes(c.Value).Select(r => r.bounds.center).ToArray()).ToList();
				foreach (var c in copies) c.Value.transform.position = at + step;
				yield return null;
				for (int k = 0; k < copies.Count; k++)
				{
					MeshRenderer[] rs = meshes(copies[k].Value);
					if (rs.Length != before[k].Length) continue;
					int stuck = 0;
					for (int i = 0; i < rs.Length; i++)
						if ((rs[i].bounds.center - before[k][i] - step).magnitude > 0.5f) stuck++;
					if (rs.Any(r => r.isPartOfStaticBatch)) batched.Add(copies[k].Key);
					if (stuck > 0) elsewhere.Add(copies[k].Key + " (" + stuck + " of " + rs.Length + " meshes)");
					checkedCount++;
				}
				foreach (var c in copies) UnityEngine.Object.Destroy(c.Value);
				yield return null;
			}
			UnityEngine.Object.Destroy(parent.gameObject);
			Log("  " + batched.Count + " objects have statically batched meshes (" + PlaceableCatalog.AnchoredRenderers + " meshes anchored in the catalog): " + string.Join(", ", batched.Take(80).ToArray()));
			Check(ref ok, checkedCount > 100 && elsewhere.Count == 0, checkedCount + " objects checked: " + (elsewhere.Count == 0 ? "each drawn where it stands" : elsewhere.Count + " drawn elsewhere: " + string.Join(", ", elsewhere.Take(60).ToArray())));
			if (ok) Log("PASS: objects drawn where they stand"); else Fail("objects drawn where they stand");
		}

		[ConsoleCommand(name: "CIDumpPlaced", docs: "Dev, in game: how a loaded island's objects are drawn - for each object whose name contains <part>: its renderers (on, seen, layer and the camera's culling of it, bounds), materials and shaders (found again by name the same?), LOD groups. CIDumpPlaced <island part> <object part>")]
		public static void DumpPlaced(string[] args)
		{
			if (args == null || args.Length < 2) { Fail("CIDumpPlaced <island part> <object part>"); return; }
			string islandPart = args[0], objectPart = string.Join(" ", args.Skip(1).ToArray());
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.Root != null && x.HostName.IndexOf(islandPart, StringComparison.OrdinalIgnoreCase) >= 0);
			if (e == null) { Fail("no loaded island with '" + islandPart + "' in its name"); return; }
			Camera cam = Camera.main;
			float[] culls = cam != null ? cam.layerCullDistances : null;
			int found = 0;
			// (the island's objects: the children of its objects' parent - or anything with that name)
			List<Transform> matches = e.Root.GetComponentsInChildren<Transform>(true).Where(x => x.name.IndexOf(objectPart, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			matches = matches.Where(x => !matches.Any(o => o != x && x.IsChildOf(o))).ToList();
			if (matches.Count == 0) Log("  no '" + objectPart + "'; the island's objects are named like: " + string.Join(", ", e.Root.GetComponentsInChildren<Transform>(true).Select(x => x.name).Distinct().Take(60).ToArray()));
			foreach (Transform t in matches)
			{
				found++;
				Log("  object '" + t.name + "' at " + (t.position - e.Position).ToString("F1") + " active " + t.gameObject.activeInHierarchy + ", scripts: " + string.Join(", ", t.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m != null).Select(m => m.GetType().Name).Distinct().ToArray()));
				foreach (LODGroup g in t.GetComponentsInChildren<LODGroup>(true))
					Log("    LOD group '" + g.name + "' enabled " + g.enabled + ", size " + g.size.ToString("F1") + ", levels " + string.Join(" / ", g.GetLODs().Select(l => l.screenRelativeTransitionHeight.ToString("F3") + " (" + l.renderers.Length + ")").ToArray()));
				foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
				{
					int layer = r.gameObject.layer;
					bool seenByCamera = cam != null && (cam.cullingMask & (1 << layer)) != 0;
					Log("    renderer '" + r.name + "' (" + r.GetType().Name + ") on " + r.enabled + ", active " + r.gameObject.activeInHierarchy + ", visible " + r.isVisible + ", layer " + LayerMask.LayerToName(layer) + " (" + layer + ") " +
						(seenByCamera ? "in the camera's mask" : "NOT in the camera's mask") + (culls != null && culls[layer] > 0f ? ", culled beyond " + culls[layer].ToString("F0") + " m" : "") + ", bounds " + (r.bounds.center - e.Position).ToString("F1") + " size " + r.bounds.size.ToString("F1") +
						(r is MeshRenderer && r.GetComponent<MeshFilter>() != null ? ", mesh " + (r.GetComponent<MeshFilter>().sharedMesh != null ? r.GetComponent<MeshFilter>().sharedMesh.name + " (" + r.GetComponent<MeshFilter>().sharedMesh.vertexCount + " vertices)" : "NONE") : ""));
					foreach (Material m in r.sharedMaterials)
					{
						if (m == null) { Log("      material: none"); continue; }
						Shader byName = m.shader != null ? Shader.Find(m.shader.name) : null;
						Log("      material '" + m.name + "' shader '" + (m.shader != null ? m.shader.name : "none") + "' supported " + (m.shader != null && m.shader.isSupported) + ", queue " + m.renderQueue +
							(m.HasProperty("_Color") ? ", colour " + m.GetColor("_Color") : "") + ", found by name: " + (byName == null ? "NOTHING" : byName == m.shader ? "the same" : "ANOTHER shader") + ", keywords " + string.Join(" ", m.shaderKeywords));
					}
				}
			}
			if (found == 0) Fail("no object with '" + objectPart + "' on '" + e.HostName + "'");
			else Log("PASS: dumped " + found + " object(s) of '" + e.HostName + "' (positions from its origin " + e.Position.ToString("F1") + ")");
		}

		[ConsoleCommand(name: "CIReturningIslands", docs: "Dev, in game (host, a test world 'CI ...'): islands the players still need come back ahead of the raft (ReturningIslands) - one with its quest begun and one a plan's rule waits for (never reached), left 1.3 km behind: after the return time (3 s here) both are ahead of the raft, load there, the quest where it was, a banner; one with its quest done, one never reached that nothing waits for, and one that came back 3 times stay; one a Receiver frequency brought doesn't count")]
		public static void ReturningIslandsTest() { StartTest(ReturningIslandsRoutine()); }

		static IEnumerator ReturningIslandsRoutine()
		{
			Vector3? raftAt = CustomIslandSpawner.RaftPosition;
			if (!raftAt.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("only in a test world 'CI ...' (it changes the world's plan for a moment)"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			// Six copies of an island with a two-step quest
			IslandFile f = SampleIsland();
			f.Props[IslandQuest.KeyTitle] = "Come back";
			f.Props[IslandQuest.KeySteps] = "reach|cizone|1|\nread|CI note|1|";
			string[] names = { "cireturn-begun", "cireturn-awaited", "cireturn-done", "cireturn-unseen", "cireturn-often", "cireturn-receiver" };
			foreach (string n in names) { f.Name = n; f.Save(IslandSpawner.PathFor(n)); }
			float minutesBefore = ReturningIslands.ReturnMinutes;
			WorldPlan planBefore = WorldDirector.Plan;
			var made = new List<IslandWorldState.Entry>();
			int returnedCount = 0;
			Action<IslandWorldState.Entry> onReturn = e => returnedCount++;
			ReturningIslands.Returned += onReturn;
			try
			{
				// Left behind the raft past the unload distance from their land's edge (unloaded), each its own way - an old
				// sample island counts its whole raised sea floor as land (700 m): 1.3 km wasn't away for it
				Vector3 back = -CustomIslandSpawner.SailDirection();
				float behindBy = WorldRules.UnloadDistance + Mathf.Max(0f, CustomIslandSpawner.LandRadius(names[0])) + 400f;
				for (int i = 0; i < names.Length; i++)
				{
					Vector3 at = raftAt.Value + Quaternion.Euler(0f, -75f + i * 30f, 0f) * back * behindBy;
					at.y = 0f;
					made.Add(IslandWorldState.Add(names[i], at, null, false));
				}
				Log("  six islands left " + behindBy.ToString("F0") + " m behind the raft (land radius " + CustomIslandSpawner.LandRadius(names[0]).ToString("F0") + " m)");
				IslandWorldState.Entry begun = made[0], awaited = made[1], done = made[2], unseen = made[3], often = made[4], receiver = made[5];
				var visit = new ObjectState { Active = true, Yield = 0, Day = 0 };
				begun.State[WorldDirector.VisitKey] = visit; QuestTracker.Set(begun, 1, 0, false);
				awaited.Rule = "awaited";
				done.State[WorldDirector.VisitKey] = visit; QuestTracker.Set(done, 2, 0, false);
				often.State[WorldDirector.VisitKey] = visit; QuestTracker.Set(often, 1, 0, false);
				often.State[ReturningIslands.ReturnsKey] = new ObjectState { Active = true, Yield = ReturningIslands.MaxReturns, Day = 0 };
				receiver.State[WorldDirector.VisitKey] = visit; QuestTracker.Set(receiver, 1, 0, false); receiver.Rule = "radio";
				// (a Receiver island, checked with no moment in between: the Receiver chain mustn't act on the plan for the moment)
				WorldDirector.Plan = WorldPlan.Parse("CI returning", "rule = radio | island:cireturn-receiver | start | receiver:900 | | \n");
				string receiverWhy = ReturningIslands.Why(receiver);
				// A plan whose rule waits for players to reach 'awaited'
				WorldDirector.Plan = WorldPlan.Parse("CI returning", "rule = next | island:cireturn-none | visit:awaited | ahead:300 | | \n");
				Check(ref ok, receiverWhy == null, "an island a Receiver frequency brought doesn't come back (the Receiver shows the way): " + (receiverWhy ?? "no"));
				// (out of the world for the rest: under the second plan its rule isn't there, so it would look like any begun quest)
				IslandWorldState.RemoveIds(new List<int> { receiver.Id }, false);
				made.Remove(receiver);
				Check(ref ok, ReturningIslands.Why(begun) == "quest" && ReturningIslands.Why(awaited) == "awaited" && ReturningIslands.Why(done) == null && ReturningIslands.Why(unseen) == null && ReturningIslands.Why(often) == null,
					"which ones are needed: begun " + (ReturningIslands.Why(begun) ?? "no") + ", awaited " + (ReturningIslands.Why(awaited) ?? "no") + ", done " + (ReturningIslands.Why(done) ?? "no") + ", unseen " + (ReturningIslands.Why(unseen) ?? "no") + ", came back 3 times " + (ReturningIslands.Why(often) ?? "no"));
				ReturningIslands.ReturnMinutes = 0.05f;
				for (float t = 0f; t < 40f && returnedCount < 2; t += 0.5f) yield return new WaitForSeconds(0.5f);
				yield return new WaitForSeconds(3f);
				if (returnedCount < 2) foreach (string l in ReturningIslands.Describe().Where(l => l.Contains("cireturn") || l.StartsWith("host"))) Log("  " + l);
				Vector3 raftNow = CustomIslandSpawner.RaftPosition ?? raftAt.Value;
				Func<IslandWorldState.Entry, float> from = e => new Vector2(e.Position.x - raftNow.x, e.Position.z - raftNow.z).magnitude;
				Func<IslandWorldState.Entry, bool> ahead = e => Vector3.Dot(new Vector3(e.Position.x - raftNow.x, 0f, e.Position.z - raftNow.z), CustomIslandSpawner.SailDirection()) > 0f;
				Check(ref ok, returnedCount == 2 && from(begun) < behindBy - 300f && from(awaited) < behindBy - 300f && ahead(begun) && ahead(awaited),
					"the two needed islands came back ahead of the raft: " + from(begun).ToString("F0") + " m and " + from(awaited).ToString("F0") + " m away (" + returnedCount + " came back)");
				Check(ref ok, from(done) > behindBy - 300f && from(unseen) > behindBy - 300f && from(often) > behindBy - 300f,
					"the others stayed behind: " + string.Join(", ", new[] { done, unseen, often }.Select(e => e.Name.Substring(9) + " " + from(e).ToString("F0") + " m").ToArray()));
				Check(ref ok, QuestTracker.StepOf(begun) == 1 && ReturningIslands.Returns(begun) == 1, "its quest where it was (step " + QuestTracker.StepOf(begun) + "), came back once (" + ReturningIslands.Returns(begun) + ")");
				Check(ref ok, (WorldDirector.LastAnnouncement ?? "").Contains("Back in sight"), "a banner says so: " + WorldDirector.LastAnnouncement);
				for (float t = 0f; t < 30f && (begun.Root == null || awaited.Root == null); t += 0.5f) yield return new WaitForSeconds(0.5f);
				Check(ref ok, begun.Root != null && awaited.Root != null, "they load where they came back to");
			}
			finally
			{
				ReturningIslands.Returned -= onReturn;
				ReturningIslands.ReturnMinutes = minutesBefore;
				WorldDirector.Plan = planBefore;
				IslandWorldState.RemoveIds(made.Select(e => e.Id).ToList(), false);
				foreach (string n in names) File.Delete(IslandSpawner.PathFor(n));
			}
			if (ok) Log("PASS: islands the players still need come back"); else Fail("islands the players still need come back");
		}

		[ConsoleCommand(name: "CIAgainUntilDone", docs: "Dev, in game (host, a test world 'CI ...'): SR1 - a 'day:N:again' rule's island comes back every N days until the players are done with it. A plan 'CI again' (day rules with again: an island with a two-step quest and one without; and one without again) brings three islands on day D; left behind unvisited, none is back the day before D+N; the world saved and read back (the plan's again, each island's clock, no second copies); on day D+N the two again islands come back ahead of the raft, the other stays; the plain one reached and the quest one begun, then left again: only the quest one comes back; its quest done, nothing comes back")]
		public static void AgainUntilDoneTest() { StartTest(AgainUntilDoneRoutine()); }

		[ConsoleCommand(name: "CISetDay", docs: "Dev, in game (host, a test world 'CI ...'): CISetDay <n> sets Raft's day counter to n, for plan tests with 'day:N' rules (run it before CIPlay)")]
		public static void SetDayCommand(string[] args)
		{
			int day;
			if (args == null || args.Length < 1 || !int.TryParse(args[0], out day) || day < 0) { Fail("CISetDay <day>"); return; }
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("CISetDay: host only, in a world"); return; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("CISetDay: only in a test world 'CI ...'"); return; }
			SetDay(day);
			if (WorldManager.DayCounter == day) Log("PASS: day " + day); else Fail("CISetDay: the day is " + WorldManager.DayCounter);
		}

		static void SetDay(int day)
		{
			System.Reflection.PropertyInfo p = typeof(WorldManager).GetProperty("DayCounter", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			System.Reflection.MethodInfo set = p != null ? p.GetSetMethod(true) : null;
			if (set != null) set.Invoke(null, new object[] { day });
		}

		static IEnumerator AgainUntilDoneRoutine()
		{
			if (!CvInTestWorld("again until done")) yield break;
			Vector3? raftAt = CustomIslandSpawner.RaftPosition;
			if (!raftAt.HasValue || !Raft_Network.IsHost) { Fail("again until done: run as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			// (the tests' own sample island, not whichever island the player has saved)
			string[] names = { "ciagain-quest", "ciagain-plain", "ciagain-once" };
			IslandFile f = SampleIsland();
			f.Props[IslandQuest.KeyTitle] = "Come again";
			f.Props[IslandQuest.KeySteps] = "reach|cizone|1|\nread|CI note|1|";
			f.Name = names[0]; f.Save(IslandSpawner.PathFor(names[0]));
			f.Props.Remove(IslandQuest.KeyTitle); f.Props.Remove(IslandQuest.KeySteps);
			for (int i = 1; i < names.Length; i++) { f.Name = names[i]; f.Save(IslandSpawner.PathFor(names[i])); }
			int dayBefore = WorldManager.DayCounter;
			const int every = 3;
			int d = Mathf.Max(every, dayBefore);
			WorldPlan planBefore = WorldDirector.Plan;
			string planNameBefore = WorldDirector.PlanName;
			int returned = 0;
			Action<IslandWorldState.Entry> onReturn = e => returned++;
			ReturningIslands.Returned += onReturn;
			Func<string, IslandWorldState.Entry> entry = rule => IslandWorldState.Islands.FirstOrDefault(x => x.Rule == rule);
			Func<string, int> copies = n => IslandWorldState.Islands.Count(x => x.Name == n);
			Func<string, string> why = rule => entry(rule) != null ? ReturningIslands.Why(entry(rule)) ?? "no" : "gone";
			Vector3 back = -CustomIslandSpawner.SailDirection();
			Func<IslandWorldState.Entry, float> from = e => { if (e == null) return -1f; Vector3 r = CustomIslandSpawner.RaftPosition ?? raftAt.Value; return new Vector2(e.Position.x - r.x, e.Position.z - r.z).magnitude; };
			float behindBy = WorldRules.UnloadDistance + Mathf.Max(0f, CustomIslandSpawner.LandRadius(names[0])) + 400f;
			// (left behind: unloaded, past the unload distance behind the raft, each its own way)
			Action<string[]> leave = rules =>
			{
				Vector3 r = CustomIslandSpawner.RaftPosition ?? raftAt.Value;
				for (int i = 0; i < rules.Length; i++)
				{
					IslandWorldState.Entry e = entry(rules[i]);
					if (e == null) continue;
					if (e.Root != null) { IslandObjectState.Capture(e); IslandSpawner.Despawn(e.Root); e.Root = null; }
					Vector3 at = r + Quaternion.Euler(0f, -40f + i * 40f, 0f) * back * behindBy;
					at.y = e.Position.y;
					e.Position = at;
				}
			};
			try
			{
				string text = "rule = qa | island:" + names[0] + " | day:" + d + ":again | ahead:400 | | Again quest\n" +
					"rule = pl | island:" + names[1] + " | day:" + d + ":again | ahead:700 | | Again plain\n" +
					"rule = once | island:" + names[2] + " | day:" + d + " | ahead:1000 | | Once\n";
				// (N = the rule's day number; the island first comes on day d = N here, so it is back on days 2N, 3N...)
				WorldPlan plan = WorldPlan.Parse("CI again", "random = off\n" + text);
				IntroRule qa = plan.Rules.FirstOrDefault(r => r.Id == "qa");
				Check(ref ok, plan.Rules.Count == 3 && qa != null && qa.Again && qa.Repeats && !plan.Rules[2].Again && qa.ToLine().Contains("day:" + d + ":again") && !plan.Rules[2].ToLine().Contains(":again"),
					"the plan reads 'day:" + d + ":again' and writes it back: " + (qa != null ? qa.ToLine() : "no rule"));
				Check(ref ok, qa != null && qa.DescribeWhen().Contains("again every"), "the card says so: " + (qa != null ? qa.DescribeWhen() : ""));
				int n = d; // every n days
				WorldDirector.Plan = plan; WorldDirector.PlanName = plan.Name;
				foreach (IntroRule r in plan.Rules) WorldDirector.Done.Remove(r.Id);
				SetDay(d);
				Check(ref ok, WorldManager.DayCounter == d, "day " + WorldManager.DayCounter + " (set from " + dayBefore + ")");
				for (int i = 0; i < 30 && (entry("qa") == null || entry("pl") == null || entry("once") == null); i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, entry("qa") != null && entry("pl") != null && entry("once") != null, "day " + d + ": the three day rules brought their islands");
				if (entry("qa") == null || entry("pl") == null || entry("once") == null) { Fail("again until done"); yield break; }
				yield return new WaitForSeconds(3f);
				Check(ref ok, why("qa") == "again" && why("pl") == "again" && why("once") == "no", "needed: quest island " + why("qa") + ", plain island " + why("pl") + ", the one without again " + why("once"));
				// Left behind, never reached: not back the day before d + n
				leave(new[] { "qa", "pl", "once" });
				SetDay(d + n - 1);
				for (int i = 0; i < 6; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, returned == 0 && from(entry("qa")) > behindBy - 300f, "day " + (d + n - 1) + ": not back yet (" + returned + " came back)");
				foreach (string l in ReturningIslands.Describe().Where(l => l.Contains("ciagain"))) Log("  " + l);
				// The world saved and read back
				foreach (IslandWorldState.Entry x in IslandWorldState.Islands.ToList())
					if (x.Root != null) { IslandObjectState.Capture(x); IslandSpawner.Despawn(x.Root); x.Root = null; }
				yield return null;
				IslandWorldState.Save();
				yield return null;
				IslandWorldState.OnWorldLoaded();
				yield return new WaitForSeconds(1f);
				IntroRule qaRead = WorldDirector.Plan != null ? WorldDirector.Plan.Rules.FirstOrDefault(r => r.Id == "qa") : null;
				ObjectState mark = null;
				bool hasMark = entry("qa") != null && entry("qa").State.TryGetValue(ReturningIslands.AgainKey, out mark);
				Check(ref ok, qaRead != null && qaRead.Again && hasMark && mark.Day == d,
					"after the world was saved and read: the plan's rule still has again (" + (qaRead != null ? qaRead.ToLine() : "none") + "), the island's clock counts from day " + (hasMark ? mark.Day.ToString() : "none"));
				Check(ref ok, copies(names[0]) == 1 && copies(names[1]) == 1 && copies(names[2]) == 1 && WorldDirector.Done.Contains("qa"), "one copy of each island, the rule done (no second copy)");
				for (int i = 0; i < 6; i++) { WorldDirector.Evaluate(); yield return new WaitForSeconds(0.5f); }
				Check(ref ok, returned == 0 && copies(names[0]) == 1, "... and still not back on day " + (d + n - 1));
				// Day d + n: both again islands come back, the other one stays
				SetDay(d + n);
				for (float t = 0f; t < 20f && returned < 2; t += 0.5f) yield return new WaitForSeconds(0.5f);
				yield return new WaitForSeconds(2.5f);
				Vector3 raftNow = CustomIslandSpawner.RaftPosition ?? raftAt.Value;
				Func<IslandWorldState.Entry, bool> ahead = e => e != null && Vector3.Dot(new Vector3(e.Position.x - raftNow.x, 0f, e.Position.z - raftNow.z), CustomIslandSpawner.SailDirection()) > 0f;
				Check(ref ok, returned == 2 && ahead(entry("qa")) && ahead(entry("pl")) && from(entry("qa")) < behindBy - 300f && from(entry("pl")) < behindBy - 300f,
					"day " + (d + n) + ": both again islands came back ahead of the raft (" + returned + " came back; " + from(entry("qa")).ToString("F0") + " m, " + from(entry("pl")).ToString("F0") + " m)");
				Check(ref ok, from(entry("once")) > behindBy - 300f, "the rule without again: its island stayed behind (" + from(entry("once")).ToString("F0") + " m)");
				Check(ref ok, (WorldDirector.LastAnnouncement ?? "").Contains("still waits for you"), "a banner says so: " + WorldDirector.LastAnnouncement);
				Check(ref ok, copies(names[0]) == 1 && copies(names[1]) == 1, "the same islands, no copies");
				// Reached: the plain one is done; the quest one begun (step 1 of 2) is not
				var visit = new ObjectState { Active = true, Yield = 0, Day = d + n };
				entry("pl").State[WorldDirector.VisitKey] = visit;
				entry("qa").State[WorldDirector.VisitKey] = visit; QuestTracker.Set(entry("qa"), 1, 0, false);
				Check(ref ok, why("pl") == "no" && why("qa") == "again", "the plain island reached: done (" + why("pl") + "); the quest island at step 1 of 2: " + why("qa"));
				yield return new WaitForSeconds(2.5f);
				leave(new[] { "qa", "pl" });
				returned = 0;
				SetDay(d + 2 * n);
				for (float t = 0f; t < 20f && returned < 1; t += 0.5f) yield return new WaitForSeconds(0.5f);
				yield return new WaitForSeconds(4f);
				Check(ref ok, returned == 1 && from(entry("qa")) < behindBy - 300f && from(entry("pl")) > behindBy - 300f,
					"day " + (d + 2 * n) + ": only the unfinished quest island came back (" + returned + "; quest " + from(entry("qa")).ToString("F0") + " m, plain " + from(entry("pl")).ToString("F0") + " m)");
				Check(ref ok, QuestTracker.StepOf(entry("qa")) == 1, "its quest where it was (step " + QuestTracker.StepOf(entry("qa")) + ")");
				// Its quest done: nothing comes back any more
				QuestTracker.Set(entry("qa"), 2, 0, false);
				Check(ref ok, why("qa") == "no", "its quest done: not needed (" + why("qa") + ")");
				leave(new[] { "qa" });
				returned = 0;
				SetDay(d + 4 * n);
				for (int i = 0; i < 6; i++) yield return new WaitForSeconds(1f);
				Check(ref ok, returned == 0 && from(entry("qa")) > behindBy - 300f, "day " + (d + 4 * n) + ": nothing came back (" + returned + ")");
			}
			finally
			{
				ReturningIslands.Returned -= onReturn;
				SetDay(dayBefore);
				WorldDirector.Plan = planBefore; WorldDirector.PlanName = planNameBefore;
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => names.Contains(x.Name)).Select(e => e.Id).ToList(), false);
				foreach (string n in names) File.Delete(IslandSpawner.PathFor(n));
			}
			if (ok) Log("PASS: again until done"); else Fail("again until done");
		}

		[ConsoleCommand(name: "CISpawnSpread", docs: "Dev, in game (host, a test world 'CI ...'): the spread of random picks from the spawn pool - 8000 picks: every island in the pool comes, each about as often as its weight says, and one this world has already (left behind, not finished) a quarter as often; an island 'not before 5 km' only once the world has sailed 5 km")]
		public static void SpawnSpreadTest(string[] args)
		{
			Vector3? raftAt = CustomIslandSpawner.RaftPosition;
			if (!raftAt.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); return; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("only in a test world 'CI ...'"); return; }
			bool ok = true;
			string[] names = { "cispread-a", "cispread-b", "cispread-c", "cispread-here" };
			IslandFile f = SampleIsland();
			f.Props.Remove(IslandQuest.KeyTitle); f.Props.Remove(IslandQuest.KeySteps);
			foreach (string n in names) { f.Name = n; f.Save(IslandSpawner.PathFor(n)); }
			const string late = "cispread-late";
			f.Name = late; f.Props[IslandProps.NotBeforeKm] = "5"; f.Save(IslandSpawner.PathFor(late));
			CustomIslandSpawner.LoadPool(true);
			IslandWorldState.Entry here = null;
			float sailedWas = WorldDirector.Sailed;
			try
			{
				Vector3 at = raftAt.Value - CustomIslandSpawner.SailDirection() * (WorldRules.UnloadDistance + 2000f); at.y = 0f;
				here = IslandWorldState.Add(names[3], at, null, false);
				var pool = CustomIslandSpawner.Pool().Where(p => !p.Key.Equals(late, StringComparison.OrdinalIgnoreCase)).ToList();
				var missing = names.Where(n => !pool.Any(p => p.Key.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
				if (missing.Count > 0) { Fail("not in the pool (this world's island list leaves them out?): " + string.Join(", ", missing.ToArray())); return; }
				var weights = pool.Select(p => p.Value * (p.Key != CustomIslandSpawner.GeneratedEntry && CustomIslandSpawner.InWorld(p.Key) != null ? CustomIslandSpawner.InWorldWeight : 1f)).ToList();
				float total = weights.Sum();
				const int picks = 8000;
				var counts = pool.ToDictionary(p => p.Key, p => 0, StringComparer.OrdinalIgnoreCase);
				var rnd = new System.Random(4711);
				for (int i = 0; i < picks; i++) { string k = CustomIslandSpawner.PickFrom(pool, (float)rnd.NextDouble()); if (k != null) counts[k]++; }
				var never = pool.Where((p, i) => counts[p.Key] == 0 && weights[i] / total * picks >= 20f).Select(p => p.Key).ToList();
				Check(ref ok, never.Count == 0, "every island in the pool (" + pool.Count + ") came in " + picks + " picks" + (never.Count > 0 ? " - never: " + string.Join(", ", never.ToArray()) : ""));
				for (int i = 0; i < pool.Count; i++)
				{
					if (!names.Contains(pool[i].Key, StringComparer.OrdinalIgnoreCase)) continue;
					float expected = weights[i] / total * picks;
					int got = counts[pool[i].Key];
					Check(ref ok, Mathf.Abs(got - expected) <= Mathf.Max(4f * Mathf.Sqrt(expected), expected * 0.3f), "'" + pool[i].Key + "' as often as its weight says: " + got + " picks, about " + expected.ToString("F0") + " expected");
				}
				float ratio = counts[names[3]] / (float)Mathf.Max(1, counts[names[0]]);
				// "Not before 5 km": out of the pool until the world has sailed 5 km
				WorldDirector.Sailed = 4000f;
				bool early = CustomIslandSpawner.Pool().Any(p => p.Key.Equals(late, StringComparison.OrdinalIgnoreCase));
				WorldDirector.Sailed = 5100f;
				bool after = CustomIslandSpawner.Pool().Any(p => p.Key.Equals(late, StringComparison.OrdinalIgnoreCase));
				WorldDirector.Sailed = sailedWas;
				Check(ref ok, !early && after, "an island 'not before 5 km' is out of the pool at 4 km (" + !early + "), in at 5.1 km (" + after + ")");
				Check(ref ok, ratio > 0.12f && ratio < 0.45f, "the one this world has already comes about a quarter as often: " + counts[names[3]] + " vs " + counts[names[0]] + " (" + ratio.ToString("F2") + ")");
			}
			finally
			{
				if (here != null) IslandWorldState.RemoveIds(new List<IslandWorldState.Entry> { here }.Select(e => e.Id).ToList(), false);
				WorldDirector.Sailed = sailedWas;
				foreach (string n in names.Concat(new[] { late })) File.Delete(IslandSpawner.PathFor(n));
				CustomIslandSpawner.LoadPool(true);
			}
			if (ok) Log("PASS: random picks spread by weight, every island comes, ones the world has less often"); else Fail("random picks spread by weight, every island comes, ones the world has less often");
		}

		[ConsoleCommand(name: "CIPoolAgain", docs: "Dev, in game (host, a test world 'CI ...'): the spawn pool and islands this world already has - a finished one (quest done; or without a quest, reached) is never picked again; an unfinished one (quest begun, or never reached, or a note not found) may be: picked, it comes back ahead of the raft as it was (its quest step kept, one copy), not a second fresh copy")]
		public static void PoolAgainTest() { StartTest(PoolAgainRoutine()); }

		static IEnumerator PoolAgainRoutine()
		{
			Vector3? raftAt = CustomIslandSpawner.RaftPosition;
			if (!raftAt.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("only in a test world 'CI ...'"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			// Two islands with a two-step quest, two without a quest
			string[] names = { "ciagain-done", "ciagain-begun", "ciagain-seen", "ciagain-unseen" };
			IslandFile f = SampleIsland();
			foreach (var o in f.Objects) if (o.Props != null) o.Props.Remove(ObjectProps.NoteText); // (no notes: they'd keep a finished island in the pool)
			f.Props[IslandQuest.KeyTitle] = "Again";
			f.Props[IslandQuest.KeySteps] = "reach|cizone|1|\nread|CI note|1|";
			for (int i = 0; i < 2; i++) { f.Name = names[i]; f.Save(IslandSpawner.PathFor(names[i])); }
			f.Props.Remove(IslandQuest.KeyTitle); f.Props.Remove(IslandQuest.KeySteps);
			for (int i = 2; i < 4; i++) { f.Name = names[i]; f.Save(IslandSpawner.PathFor(names[i])); }
			CustomIslandSpawner.LoadPool(true);
			var made = new List<IslandWorldState.Entry>();
			try
			{
				Vector3 back = -CustomIslandSpawner.SailDirection();
				float behindBy = WorldRules.UnloadDistance + Mathf.Max(0f, CustomIslandSpawner.LandRadius(names[0])) + 400f;
				for (int i = 0; i < names.Length; i++)
				{
					Vector3 at = raftAt.Value + Quaternion.Euler(0f, -45f + i * 30f, 0f) * back * behindBy;
					at.y = 0f;
					made.Add(IslandWorldState.Add(names[i], at, null, false));
				}
				IslandWorldState.Entry done = made[0], begun = made[1], seen = made[2], unseen = made[3];
				var visit = new ObjectState { Active = true, Yield = 0, Day = 0 };
				done.State[WorldDirector.VisitKey] = visit; QuestTracker.Set(done, 2, 0, false);
				begun.State[WorldDirector.VisitKey] = visit; QuestTracker.Set(begun, 1, 0, false);
				seen.State[WorldDirector.VisitKey] = visit;
				Check(ref ok, ReturningIslands.Finished(done) && !ReturningIslands.Finished(begun) && ReturningIslands.Finished(seen) && !ReturningIslands.Finished(unseen),
					"finished: quest done " + ReturningIslands.Finished(done) + ", begun " + ReturningIslands.Finished(begun) + ", no quest reached " + ReturningIslands.Finished(seen) + ", no quest never reached " + ReturningIslands.Finished(unseen));
				var pool = CustomIslandSpawner.Pool().Select(p => p.Key).ToList();
				Func<string, bool> inPool = n => pool.Contains(n, StringComparer.OrdinalIgnoreCase);
				Check(ref ok, !inPool(names[0]) && !inPool(names[2]), "the finished ones are not in the pool: done " + inPool(names[0]) + ", reached " + inPool(names[2]));
				Check(ref ok, inPool(names[1]) && inPool(names[3]), "the unfinished ones are: begun " + inPool(names[1]) + ", never reached " + inPool(names[3]));
				// A finished island with a note not found yet comes back, until every note is found
				IslandFile noted = IslandFile.Load(IslandSpawner.PathFor(names[0]));
				if (noted.Objects[0].Props == null) noted.Objects[0].Props = new Dictionary<string, string>();
				noted.Objects[0].Props[ObjectProps.NoteText] = "A note the players missed.";
				noted.Save(IslandSpawner.PathFor(names[0]));
				CustomIslandSpawner.LoadPool(true);
				bool missed = CustomIslandSpawner.Pool().Any(p => p.Key.Equals(names[0], StringComparison.OrdinalIgnoreCase));
				string noteKey = "note:" + done.HostName + ":0";
				StoryBook.AddPage(noteKey, "Missed", "A note the players missed.", "Again");
				bool allFound = !CustomIslandSpawner.Pool().Any(p => p.Key.Equals(names[0], StringComparison.OrdinalIgnoreCase));
				foreach (StoryBook.Page added in StoryBook.Pages.Where(x => x.Key == noteKey).ToList()) StoryBook.Pages.Remove(added);
				Check(ref ok, missed && allFound, "a finished island with a note not found comes back (" + missed + "), not once every note is found (" + allFound + ")");
				// Picked again: the island the world has comes back, no second copy
				Vector3 raftNow = CustomIslandSpawner.RaftPosition ?? raftAt.Value;
				foreach (IslandWorldState.Entry e in new[] { begun, unseen })
				{
					string said = CustomIslandSpawner.TrySpawn(raftNow, true, e.HostName);
					// (a sea crowded ahead - the island just brought back, Raft's islands in the way: each try picks other random spots; a player's game tries again later)
					for (int again = 0; again < 5 && said != null && said.Contains("no free spot"); again++) said = CustomIslandSpawner.TrySpawn(raftNow, true, e.HostName);
					// (still none: a test world crowded by the earlier suites' islands - run i 2026-10-20 failed after the runner's 4 sail-ons; the
					// sea's layout isn't what this tests, so once more with any spot clear of the raft, as CISpawnGenerated does)
					if (said != null && said.Contains("no free spot"))
					{
						CustomIslandSpawner.TestAllRound = true;
						try { said = CustomIslandSpawner.TrySpawn(raftNow, true, e.HostName); }
						finally { CustomIslandSpawner.TestAllRound = false; }
						Log("  ('" + e.HostName + "': no free spot in the crowded test sea - once more with any spot clear of the raft: " + said + ")");
					}
					float away = new Vector2(e.Position.x - raftNow.x, e.Position.z - raftNow.z).magnitude;
					int copies = IslandWorldState.Islands.Count(x => x.HostName == e.HostName);
					Check(ref ok, copies == 1 && away < behindBy - 300f && Vector3.Dot(e.Position - raftNow, CustomIslandSpawner.SailDirection()) > 0f,
						"'" + e.HostName + "' picked again comes back ahead (" + away.ToString("F0") + " m, " + copies + " in the world): " + said);
				}
				Check(ref ok, QuestTracker.StepOf(begun) == 1, "its quest where it was (step " + QuestTracker.StepOf(begun) + ")");
				// Here already (loading near the raft): not picked again either
				for (float t = 0f; t < 30f && begun.Root == null; t += 0.5f) yield return new WaitForSeconds(0.5f);
				Check(ref ok, CustomIslandSpawner.NotAgain(names[1]), "an island loaded near the players isn't picked again (" + (begun.Root != null ? "loaded" : "not loaded") + ")");
			}
			finally
			{
				IslandWorldState.RemoveIds(made.Select(e => e.Id).ToList(), false);
				foreach (string n in names) File.Delete(IslandSpawner.PathFor(n));
				CustomIslandSpawner.LoadPool(true);
			}
			if (ok) Log("PASS: finished islands are not picked again, unfinished ones come back"); else Fail("finished islands are not picked again, unfinished ones come back");
		}

		[ConsoleCommand(name: "CIRaftGap", docs: "Dev, in game (host, a test world 'CI ...' with random islands): random custom islands come after every min-max of Raft's own islands met - the span's limits (min 2-20, max 4-50, default 3-6), the world file lines, no island before its turn, none in a new world's first 10 minutes of play, one when its turn came (then a new count), and a long sail: Raft's islands met and custom islands brought (at most one per min). CIRaftGap [km]")]
		public static void RaftGapTest(string[] args)
		{
			float km = 8f;
			if (args != null && args.Length > 0) float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out km);
			StartTest(RaftGapRoutine(km));
		}

		static IEnumerator RaftGapRoutine(float km)
		{
			Raft raftObj = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raftObj == null || raftObj.body == null || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("only in a test world 'CI ...'"); yield break; }
			if (!CustomIslandSpawner.Enabled) { Fail("random islands are off in this world (a plan with random islands, or CustomIslandsAuto on)"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Func<string, string> parsed = t => { int[] g = WorldIslands.ParseGap(t); return g[0] + "-" + g[1]; };
			Check(ref ok, parsed("3-6") == "3-6" && parsed("1-100") == "2-50" && parsed("30-10") == "20-20" && parsed("4-3") == "4-4" && parsed("2-2") == "2-4" && parsed("nonsense") == "3-6",
				"the span's limits: 3-6 " + parsed("3-6") + ", 1-100 " + parsed("1-100") + ", 30-10 " + parsed("30-10") + ", 4-3 " + parsed("4-3") + ", 2-2 " + parsed("2-2") + ", nonsense " + parsed("nonsense"));
			int minBefore = WorldIslands.GapMin, maxBefore = WorldIslands.GapMax;
			float playBefore = WorldIslands.PlaySeconds;
			int spawnedLog = 0, metLog = 0;
			Application.LogCallback listen = (msg, trace, type) =>
			{
				if (msg.Contains("Random island after")) spawnedLog++;
				if (msg.Contains("Raft's islands met since the last random custom island")) metLog++;
			};
			Application.logMessageReceived += listen;
			Rigidbody body = raftObj.body;
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3 dir = Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward;
			Func<float, float, IEnumerator> sail = (metres, speed) => SailStraight(body, player, dir, metres, speed);
			try
			{
				// The world file keeps a span that isn't the default
				WorldIslands.GapMin = 5; WorldIslands.GapMax = 12;
				string lines = string.Join(" ", WorldIslands.WriteLines().ToArray());
				Check(ref ok, lines.Contains("@raftgap=5-12"), "the world file keeps the span: " + lines);
				WorldIslands.ReadLine("raftgap", "7-20");
				Check(ref ok, WorldIslands.GapMin == 7 && WorldIslands.GapMax == 20, "and reads it back: " + WorldIslands.GapMin + "-" + WorldIslands.GapMax);

				// Not before its turn: one more of Raft's islands to go
				WorldIslands.PlaySeconds = WorldIslands.LongPlayed;
				WorldIslands.GapMin = 2; WorldIslands.GapMax = 4;
				WorldIslands.Target = 3; WorldIslands.RaftIslandsSince = 0;
				int before = IslandWorldState.Islands.Count;
				yield return sail(300f, 15f);
				int metEarly = WorldIslands.RaftIslandsSince;
				Check(ref ok, metEarly >= WorldIslands.Target || IslandWorldState.Islands.Count == before,
					"no island before its turn: " + (IslandWorldState.Islands.Count - before) + " came, Raft's islands met " + metEarly + " of " + WorldIslands.Target);
				// Its turn, but in a new world's first minutes of play: none yet
				WorldIslands.RaftIslandsSince = WorldIslands.Target;
				WorldIslands.PlaySeconds = 60f;
				int quietBefore = spawnedLog;
				yield return sail(300f, 15f);
				Check(ref ok, spawnedLog == quietBefore, "none in a new world's first " + CustomIslandSpawner.QuietMinutes + " minutes (played " + (WorldIslands.PlaySeconds / 60f).ToString("F1") + " min): " + (spawnedLog - quietBefore) + " came");
				WorldIslands.PlaySeconds = CustomIslandSpawner.QuietMinutes * 60f + 1f;
				// Its turn: Raft's islands counted up to the target - an island comes, and a new count starts
				WorldIslands.RaftIslandsSince = WorldIslands.Target;
				before = IslandWorldState.Islands.Count;
				int spawnedBefore = spawnedLog;
				for (int i = 0; i < 8 && spawnedLog == spawnedBefore; i++) yield return sail(150f, 15f);
				Check(ref ok, spawnedLog > spawnedBefore && IslandWorldState.Islands.Count > before - 1 && WorldIslands.RaftIslandsSince < WorldIslands.Target && WorldIslands.Target >= 2 && WorldIslands.Target <= 4,
					"its turn: an island came (" + (spawnedLog - spawnedBefore) + "), a new count: " + WorldIslands.RaftIslandsSince + " of " + WorldIslands.Target);

				// A long sail with the default span: Raft's islands met and the custom islands they let come
				WorldIslands.GapMin = WorldIslands.GapDefaultMin; WorldIslands.GapMax = WorldIslands.GapDefaultMax; WorldIslands.NewTarget();
				int met0 = metLog, spawned0 = spawnedLog;
				int countedBefore = WorldIslands.RaftIslandsSince;
				int metTotal = 0, lastSince = WorldIslands.RaftIslandsSince;
				Log("  sailing " + km + " km with the default span " + WorldIslands.GapMin + "-" + WorldIslands.GapMax);
				for (float done = 0f; done < km * 1000f; done += 500f)
				{
					yield return sail(500f, 40f);
					int since = WorldIslands.RaftIslandsSince;
					metTotal += since >= lastSince ? since - lastSince : since; // (a spawn starts the count again)
					lastSince = since;
				}
				int customs = spawnedLog - spawned0;
				Log("  " + km + " km: Raft's islands met " + metTotal + ", random custom islands " + customs + " (" + WorldIslands.DescribeGap() + ")");
				Check(ref ok, customs <= metTotal / WorldIslands.GapDefaultMin + 1, "at most one custom island per " + WorldIslands.GapDefaultMin + " of Raft's islands: " + customs + " for " + metTotal);
			}
			finally
			{
				Application.logMessageReceived -= listen;
				WorldIslands.GapMin = minBefore; WorldIslands.GapMax = maxBefore; WorldIslands.NewTarget();
				WorldIslands.PlaySeconds = playBefore;
			}
			if (ok) Log("PASS: random custom islands follow Raft's own islands"); else Fail("random custom islands follow Raft's own islands");
		}

		static IEnumerator SailStraight(Rigidbody body, Network_Player player, Vector3 dir, float metres, float speed)
		{
			if (player != null) player.transform.position = body.position + Vector3.up * 3f;
			float sailed = 0f;
			Vector3 last = body.position;
			while (sailed < metres)
			{
				yield return new WaitForFixedUpdate();
				KeepAlive(player);
				body.MovePosition(body.position + dir * speed * Time.fixedDeltaTime);
				Vector3 d = Flat(body.position - last);
				if (d.magnitude < 100f) sailed += d.magnitude;
				last = body.position;
			}
		}

		[ConsoleCommand(name: "CIReturnState", docs: "Dev, in game (host): every island as the returning-islands clock sees it - why it is needed, how far, its land radius, loaded, how long it has been away, how often it came back")]
		public static void ReturnState() { foreach (string l in ReturningIslands.Describe()) Log("  " + l); Log("PASS: return state"); }

		[ConsoleCommand(name: "CIWreckDeck", docs: "Dev, in game (host): a wreck (the map type: Raft's foundations on open water) ahead of the raft - a player put on its deck stands on it; dropped onto it from 5 m lands on it, also when a frame takes 0.6 s on the way down (a big island loading: The Abyss Expedition's test fell through); dropped onto its roof, they end on the roof or the deck. CIWreckDeck [keep]")]
		public static void WreckDeck(string[] args) { StartTest(WreckDeckRoutine(args != null && args.Contains("keep"))); }

		static IEnumerator WreckDeckRoutine(bool keep)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			MapType type = MapTypes.Get("wreck");
			float elevation;
			// (the seed of the wreck the plan test fell through: it has a roof)
			IslandGenSettings s = MapTypes.Roll(type, new System.Random(957038), out elevation);
			IslandFile f = MapTypes.Create(type, s, elevation, "ciwreck");
			f.Save(IslandSpawner.PathFor("ciwreck"));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, 30f, 300f);
			if (!spot.HasValue) { Fail("no open sea near the raft for the wreck"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("ciwreck", spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Fail("the wreck did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			List<BoxCollider> decks = entry.Root.GetComponentsInChildren<BoxCollider>(true).Where(x => x.name == "CustomIslands_Deck").ToList();
			Check(ref ok, decks.Count >= 4, "the wreck has " + decks.Count + " deck tiles");
			// (a tile with open sky above it as wide as a player - not under the roof or its edge)
			Func<BoxCollider, Vector3> topOf = d => new Vector3(d.bounds.center.x, d.bounds.max.y, d.bounds.center.z);
			BoxCollider tile = decks.FirstOrDefault(d => { RaycastHit hit; return Physics.SphereCast(topOf(d) + Vector3.up * 8f, 0.6f, Vector3.down, out hit, 9f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == d; });
			Network_Player player = RAPI.GetLocalPlayer();
			if (tile != null && player != null)
			{
				Vector3 top = topOf(tile);
				string[] labels = { "put on its deck", "dropped onto its deck from 5 m", "dropped onto its deck from 5 m with a 0.6 s frame on the way",
					"dropped onto its deck from 5 m while a very large island loads" };
				float[] heights = { 1.2f, 5f, 5f, 5f };
				int[] hitches = { 0, 0, 600, 0 };
				IslandWorldState.Entry big = null;
				for (int k = 0; k < labels.Length; k++)
				{
					yield return PutPlayer(player, top + Vector3.up * heights[k], false);
					if (hitches[k] > 0) { yield return new WaitForSeconds(0.2f); System.Threading.Thread.Sleep(hitches[k]); }
					// (as in the plan test: reaching the wreck brought The Drowned Metropolis, 3 900 objects, that moment)
					if (k == 3 && IslandSpawner.ListSavedIslands().Contains("The Drowned Metropolis"))
					{
						int n0 = IslandWorldState.Islands.Count;
						StartTest(DynamicIslands.instance.SpawnIslandFile("The Drowned Metropolis", spot.Value + new Vector3(800f, 0f, 0f), false));
						yield return null;
						big = IslandWorldState.Islands.Skip(n0).FirstOrDefault();
					}
					bool stood = false; string seen = "";
					for (int i = 0; i < 16 && !stood; i++)
					{
						yield return new WaitForSeconds(0.25f);
						Collider under = player.PersonController.groundRaycastHit.collider;
						float above = player.transform.position.y - top.y;
						stood = player.PersonController.IsGrounded && under != null && under.name == "CustomIslands_Deck" && above > 0.5f && above < 2f;
						seen = (player.PersonController.IsGrounded ? "grounded" : "in the air") + " on " + (under != null ? under.name : "nothing") + ", " + above.ToString("F1") + " m above the deck";
					}
					Check(ref ok, stood, "a player " + labels[k] + " stands on it (" + seen + ")");
				}
				for (float w = 0f; w < 20f && big == null; w += 0.5f) { yield return new WaitForSeconds(0.5f); big = IslandWorldState.Islands.FirstOrDefault(e => e.Name == "The Drowned Metropolis" && e.Root != null && (e.Root.transform.position - (spot.Value + new Vector3(800f, 0f, 0f))).sqrMagnitude < 100f); }
				if (big != null) IslandWorldState.RemoveIds(new List<int> { big.Id }, true);
				// The thatch roof (when the wreck has one): dropped onto it, a player ends on the roof or the deck - never in the sea
				Transform roof = entry.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Block_Roof"));
				if (roof != null)
				{
					Bounds rb = roof.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
					yield return PutPlayer(player, new Vector3(rb.center.x, rb.max.y + 3f, rb.center.z), false);
					yield return new WaitForSeconds(3f);
					float above = player.transform.position.y - top.y;
					Check(ref ok, above > 0.5f, "a player dropped onto its roof ends on the roof or the deck (" + above.ToString("F1") + " m above the deck)");
				}
				OnRaftCommand();
			}
			else Check(ref ok, false, "an open deck tile to stand on");
			if (!keep)
			{
				IslandWorldState.RemoveIds(new List<int> { entry.Id }, true);
				File.Delete(IslandSpawner.PathFor("ciwreck"));
			}
			if (ok) Log("PASS: a wreck's deck holds players" + (keep ? " (wreck kept)" : "")); else Fail("a wreck's deck holds players");
		}

		[ConsoleCommand(name: "CIZoneWorld", docs: "Dev, in game (host): a trigger zone with a message, items and an ambush warthog; the island rule for regrowing")]
		public static void ZoneWorld()
		{
			StartTest(ZoneWorldRoutine());
		}

		static IEnumerator ZoneWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			IslandFile f = SampleIsland();
			f.Name = "cizone";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 15f);
			f.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = ground(c.x, c.y), Props = new Dictionary<string, string> {
				{ ObjectProps.ZoneId, "ambush" }, { ObjectProps.ZoneRadius, "8" }, { ObjectProps.ZoneMessage, "Something moves in the bushes!" }, { ObjectProps.LootItems, ContentCatalog.PresetLoot(new[] { "Plank*2", "Rope*1" }) } } });
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x + 5f, c.y), Props = new Dictionary<string, string> { { ObjectProps.CreatureZone, "ambush" } } });
			f.Objects.Add(new IslandObject { Name = "Creature_Chicken", Position = ground(c.x - 5f, c.y) });
			f.Props[IslandProps.RegrowDays] = "7";
			f.Save(IslandSpawner.PathFor("cizone"));

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("cizone"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("cizone", spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Fail("the zone island did not spawn"); yield break; }
			Check(ref ok, IslandRules.RegrowDays(entry) == 7, "the island's own rule: things come back after " + IslandRules.RegrowDays(entry) + " days");
			TriggerZone zone = entry.Root.GetComponentInChildren<TriggerZone>();
			Check(ref ok, zone != null && zone.Id == "ambush" && zone.GetComponentsInChildren<Renderer>().Length == 0, "the zone exists and is invisible");

			float t0 = Time.realtimeSinceStartup;
			while (CreatureSpawner.LiveCount < 1 && Time.realtimeSinceStartup - t0 < 30f) yield return new WaitForSeconds(0.5f);
			yield return new WaitForSeconds(2f);
			CreatureSpawnPoint boars = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>().First(p => p.Kind.Type == AI_NetworkBehaviourType.Boar);
			CreatureSpawnPoint chicken = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>().First(p => p.Kind.Type == AI_NetworkBehaviourType.Chicken);
			Check(ref ok, chicken.Spawned.Count == 1 && boars.Spawned.Count == 0, "the chicken is there, the ambush warthog waits (" + boars.Spawned.Count + ")");

			// Walk in (the zone checks the local player; the test sets it off directly)
			PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
			var items = ObjectProps.Loot(new Dictionary<string, string> { { ObjectProps.LootItems, ContentCatalog.PresetLoot(new[] { "Plank*2", "Rope*1" }) } });
			Dictionary<string, int> had = items.ToDictionary(i => i.Key, i => inv.GetItemCount(i.Key));
			zone.Enter();
			yield return null;
			Check(ref ok, IslandInfo.LastMessage == "Something moves in the bushes!", "walking in shows the message");
			Check(ref ok, items.Count > 0 && items.All(i => inv.GetItemCount(i.Key) - had[i.Key] == i.Value), "and gives " + string.Join(", ", items.Select(i => i.Key + "*" + i.Value).ToArray()));
			Screenshot(new[] { "zone_message" });
			t0 = Time.realtimeSinceStartup;
			while (boars.Spawned.Count == 0 && Time.realtimeSinceStartup - t0 < 20f) yield return new WaitForSeconds(0.5f);
			Check(ref ok, boars.Spawned.Count == 1 && zone.HasFired, "the ambush warthog appears (" + (Time.realtimeSinceStartup - t0).ToString("F1") + " s)");
			IslandInfo.ForgetShown();
			zone.Enter();
			Check(ref ok, IslandInfo.LastMessage == null, "a once-zone doesn't fire again");

			// Reload: the zone has fired, so the warthog is there at once
			IslandObjectState.Capture(entry);
			IslandSpawner.Despawn(entry.Root);
			entry.Root = null;
			t0 = Time.realtimeSinceStartup;
			while (entry.Root == null && Time.realtimeSinceStartup - t0 < 60f) yield return new WaitForSeconds(0.5f);
			t0 = Time.realtimeSinceStartup;
			CreatureSpawnPoint b2 = null;
			while (Time.realtimeSinceStartup - t0 < 30f)
			{
				b2 = entry.Root != null ? entry.Root.GetComponentsInChildren<CreatureSpawnPoint>().FirstOrDefault(p => p.Kind.Type == AI_NetworkBehaviourType.Boar) : null;
				if (b2 != null && b2.Spawned.Count > 0) break;
				yield return new WaitForSeconds(0.5f);
			}
			Check(ref ok, b2 != null && b2.Spawned.Count == 1, "after reloading, the ambush warthog is already there");

			IslandWorldState.RemoveIds(new[] { entry.Id }, true);
			File.Delete(IslandSpawner.PathFor("cizone"));
			if (ok) Log("PASS: trigger zones in a world"); else Fail("trigger zones in a world");
		}

		#endregion

		#region Object groups and terrain stamps

		[ConsoleCommand(name: "CIGroupTest", docs: "Dev, editor: object groups - save a selection (with settings) as a group, it appears in My groups, placing it gives the separate objects (one undo step)")]
		public static void GroupTest()
		{
			StartTest(GroupTestRoutine());
		}

		static IEnumerator GroupTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			CommandUndoRedo.UndoRedoManager.Clear();
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject chest = PlaceForTest("Loot_Chest", c0, placed);
			EditorGameObject boar = PlaceForTest("Creature_Boar", c0 + new Vector3(3, 0, 0), placed);
			EditorGameObject sign = PlaceForTest("Note_Sign", c0 + new Vector3(0, 0, 3), placed);
			PropsCommand.Change(boar, ObjectProps.With(boar.Props, ObjectProps.CreatureCount, "2"));
			int saved = GroupLibrary.Save("cigroup", new[] { chest, boar, sign });
			yield return GroupLibrary.Register("cigroup");
			var cats = PlaceableCatalog.Browse();
			var mine = cats.FirstOrDefault(c => c.Key == GroupLibrary.Category);
			Check(ref ok, saved == 3 && mine.Value != null && mine.Value.Any(e => e.Name == GroupLibrary.Prefix + "cigroup"), "the group is saved and listed under " + GroupLibrary.Category);
			Check(ref ok, cats.Count > 0 && cats[0].Key == GroupLibrary.Category, "My groups comes first in the object list");

			// Place it somewhere else, turned, through the object placer (as a click does)
			GameObject preview = PlaceableCatalog.Spawn(GroupLibrary.Prefix + "cigroup", null);
			preview.transform.position = c0 + new Vector3(40, 0, 0);
			preview.transform.rotation = Quaternion.Euler(0, 90, 0);
			ObjectPlacer placer = preview.AddComponent<ObjectPlacer>();
			placer.GameObjectName = GroupLibrary.Prefix + "cigroup";
			DynamicIslands.EditorGizmoHandler.placingObject = true;
			yield return null; // (Start)
			preview.transform.position = c0 + new Vector3(40, 0, 0);
			typeof(ObjectPlacer).GetMethod("Place", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(placer, null);
			yield return null;
			List<EditorGameObject> all = placed.GetComponentsInChildren<EditorGameObject>().ToList();
			List<EditorGameObject> copies = all.Where(e => e.transform.position.x > c0.x + 20f).ToList();
			EditorGameObject boarCopy = copies.FirstOrDefault(e => e.GameObjectName == "Creature_Boar");
			Check(ref ok, copies.Count == 3 && copies.All(e => !GroupLibrary.IsGroup(e.GameObjectName)), "placing the group gives its 3 separate objects (" + string.Join(", ", copies.Select(e => e.GameObjectName).ToArray()) + ")");
			Check(ref ok, boarCopy != null && ObjectProps.Count(boarCopy.Props) == 2 && ObjectProps.Loot(copies.First(e => e.GameObjectName == "Loot_Chest").Props).Count > 0, "their settings come along");
			// The group's pivot is the middle of its objects at the lowest one's height
			Vector3 pivot = new Vector3((chest.transform.position.x + boar.transform.position.x + sign.transform.position.x) / 3f, Mathf.Min(chest.transform.position.y, Mathf.Min(boar.transform.position.y, sign.transform.position.y)),
				(chest.transform.position.z + boar.transform.position.z + sign.transform.position.z) / 3f);
			Vector3 expected = c0 + new Vector3(40, 0, 0) + Quaternion.Euler(0, 90, 0) * (boar.transform.position - pivot);
			Check(ref ok, boarCopy != null && (boarCopy.transform.position - expected).magnitude < 0.2f, "the group is turned as placed (the warthog is at " + (boarCopy != null ? boarCopy.transform.position.ToString("F1") : "-") + ", expected " + expected.ToString("F1") + ")");
			CommandUndoRedo.UndoRedoManager.Undo();
			yield return null;
			Check(ref ok, copies.All(e => e == null || !e.gameObject.activeSelf), "one undo takes the whole group away");
			Screenshot(new[] { "groups" });

			GroupLibrary.Delete("cigroup");
			Check(ref ok, PlaceableCatalog.Get(GroupLibrary.Prefix + "cigroup") == null && !GroupLibrary.Saved().Contains("cigroup"), "DeleteGroup removes it");
			if (ok) Log("PASS: object groups"); else Fail("object groups");
		}

		[ConsoleCommand(name: "CIStampTest", docs: "Dev, editor: terrain stamps - built-in shapes (hill, crater), one per click, undo, saving your own and stamping it")]
		public static void StampTest()
		{
			StartTest(StampTestRoutine());
		}

		static IEnumerator StampTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.TerrainEdit);
			Terrain terrain = terraineditor.terrain;
			terraineditor editor = UnityEngine.Object.FindObjectOfType<terraineditor>();
			CommandUndoRedo.UndoRedoManager.Clear();
			Vector3 p = terrain.transform.position + new Vector3(300f, 0, 300f);
			float before = terrain.SampleHeight(p);
			float radius = terraineditor.brushRadius;
			terraineditor.brushRadius = 20f;
			TerrainStamps.Load();
			Check(ref ok, TerrainStamps.All.Count >= 6 && TerrainStamps.All.Take(6).All(s => s.BuiltIn), "6 built-in stamps: " + string.Join(", ", TerrainStamps.All.Select(s => s.Name).ToArray()));
			TerrainStamps.Selected = TerrainStamps.All.FindIndex(s => s.Name == "Hill");
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Stamp;
			editor.SimulateStroke(p, 10, 0.02f); // held for 10 frames: still one stamp
			float hill = terrain.SampleHeight(p) - before;
			Check(ref ok, Mathf.Abs(hill - 0.3f * 20f) < 1f, "a hill as big as the brush: +" + hill.ToString("F1") + " m at its middle (expected 6)");
			CommandUndoRedo.UndoRedoManager.Undo();
			Check(ref ok, Mathf.Abs(terrain.SampleHeight(p) - before) < 0.05f, "undo takes it away");

			TerrainStamps.Selected = TerrainStamps.All.FindIndex(s => s.Name == "Crater");
			Vector3 q = p + new Vector3(80, 0, 0);
			editor.SimulateStroke(p + new Vector3(80, 0, 0), 1, 0.02f);
			float mid = terrain.SampleHeight(q), rim = terrain.SampleHeight(q + new Vector3(12.4f, 0, 0));
			Check(ref ok, rim > mid + 3f, "a crater: rim " + rim.ToString("F1") + " m, middle " + mid.ToString("F1") + " m");

			// Save the crater as a stamp of our own and put it down elsewhere
			TerrainStamps.Save(TerrainStamps.Capture(terrain, q, 20f, "cistamp"));
			TerrainStamps.Load();
			EditorUI.RefreshStamps();
			int mineIndex = TerrainStamps.All.FindIndex(s => s.Name == "cistamp" && !s.BuiltIn);
			Check(ref ok, mineIndex >= 6, "a saved stamp is listed");
			TerrainStamps.Selected = mineIndex;
			Vector3 r2 = p + new Vector3(0, 0, 80);
			float b2 = terrain.SampleHeight(r2), br = terrain.SampleHeight(r2 + new Vector3(12.4f, 0, 0));
			editor.SimulateStroke(r2, 1, 0.02f);
			float dm = terrain.SampleHeight(r2) - b2, dr = terrain.SampleHeight(r2 + new Vector3(12.4f, 0, 0)) - br;
			Check(ref ok, dr > dm + 3f, "the saved crater stamps the same shape (rim +" + dr.ToString("F1") + " m, middle " + dm.ToString("F1") + " m)");
			Screenshot(new[] { "stamps" });
			CommandUndoRedo.UndoRedoManager.Undo();
			CommandUndoRedo.UndoRedoManager.Undo();

			File.Delete(Path.Combine(TerrainStamps.Folder, "cistamp.stamp"));
			TerrainStamps.Load();
			EditorUI.RefreshStamps();
			terraineditor.brushRadius = radius;
			terraineditor.modificationAction = terraineditor.TerrainModificationAction.Raise;
			if (ok) Log("PASS: terrain stamps"); else Fail("terrain stamps");
		}

		#endregion

		#region Atmosphere and sound zones

		[ConsoleCommand(name: "CIAmbienceTest", docs: "Dev, editor: atmosphere zones (fog, light, particles, seen from inside and outside) and sound zones (Raft's sounds, choosing one, listening)")]
		public static void AmbienceTest()
		{
			StartTest(AmbienceTestRoutine());
		}

		static IEnumerator AmbienceTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject atmo = PlaceForTest(ContentCatalog.AtmosphereZoneName, c0, placed);
			EditorGameObject sound = PlaceForTest(ContentCatalog.SoundZoneName, c0 + new Vector3(40, 0, 0), placed);
			if (atmo == null || sound == null) { Fail("could not place the zones"); yield break; }
			Dictionary<string, string> p = ObjectProps.With(atmo.Props, ObjectProps.ZoneRadius, "25");
			p = ObjectProps.With(p, ObjectProps.AtmoFog, "#B34D33");
			p = ObjectProps.With(p, ObjectProps.AtmoLight, "#FF8866");
			p = ObjectProps.With(p, ObjectProps.AtmoParticles, "fireflies");
			PropsCommand.Change(atmo, p);
			yield return new WaitForSeconds(1f);
			AtmosphereZone az = atmo.GetComponent<AtmosphereZone>();
			ParticleSystem ps = atmo.GetComponentInChildren<ParticleSystem>();
			Check(ref ok, az != null && az.FogAmount > 0f && ps != null && ps.isPlaying && ps.particleCount > 0, "the atmosphere zone shows in the editor: fog, light, " + (ps != null ? ps.particleCount + " fireflies" : "no particles"));

			Camera cam = Camera.main;
			cam.transform.position = c0 + new Vector3(0, 3, -6);
			cam.transform.rotation = Quaternion.Euler(10, 0, 0);
			yield return null; yield return null;
			float w;
			AtmosphereZone inside = AtmosphereZone.Strongest(cam.transform.position, out w);
			Check(ref ok, inside == az && w > 0.99f, "from inside the zone it acts fully (" + w.ToString("F2") + ")");
			Screenshot(new[] { "atmosphere_inside" });
			yield return new WaitForSecondsRealtime(0.5f);

			// A dark light tint really darkens: the sun takes it too while the camera draws inside (a cave was lit like the
			// beach - found building Shelter Atoll), and has its own colour again after
			PropsCommand.Change(atmo, ObjectProps.With(ObjectProps.With(p, ObjectProps.AtmoLight, "#101010"), ObjectProps.AtmoLightAmount, "0.9"));
			yield return null; yield return null; yield return null;
			Light sunNow = RenderSettings.sun != null ? RenderSettings.sun : UnityEngine.Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
			float before = AtmosphereZone.SunBefore.grayscale, drawn = AtmosphereZone.SunWhileDrawn.grayscale;
			Check(ref ok, sunNow != null && before > 0f && drawn < before * 0.3f && Mathf.Abs(sunNow.color.grayscale - before) < 0.01f,
				"in a dark zone the sun is dimmed while the camera draws (" + before.ToString("F2") + " -> " + drawn.ToString("F2") + ") and its own colour again after (" + (sunNow != null ? sunNow.color.grayscale.ToString("F2") : "no sun") + ")");
			// Bubbles: they rise
			PropsCommand.Change(atmo, ObjectProps.With(p, ObjectProps.AtmoParticles, "bubbles"));
			yield return new WaitForSeconds(1f);
			ParticleSystem bubbles = atmo.GetComponentInChildren<ParticleSystem>();
			Check(ref ok, bubbles != null && bubbles.isPlaying && bubbles.particleCount > 0 && bubbles.main.gravityModifier.constant < 0f, "bubbles rise from the zone (" + (bubbles != null ? bubbles.particleCount + " now, gravity " + bubbles.main.gravityModifier.constant : "no particles") + ")");
			PropsCommand.Change(atmo, p);
			yield return new WaitForSeconds(0.5f);
			cam.transform.position = c0 + new Vector3(0, 30, -60);
			yield return null;
			AtmosphereZone.Strongest(cam.transform.position, out w);
			Check(ref ok, w < 0.001f, "from outside it doesn't (" + w.ToString("F2") + ")");
			Screenshot(new[] { "atmosphere_outside" });
			yield return new WaitForSecondsRealtime(0.5f);

			// Sounds
			List<string> events = SoundLibrary.Events;
			Log("Raft's sounds: " + events.Count + " events, e.g. " + string.Join(", ", events.Where(e => e.ToLowerInvariant().Contains("ambien")).Take(6).ToArray()));
			string loop = events.FirstOrDefault(e => e.ToLowerInvariant().Contains("ambien") && SoundLibrary.IsLooping(e)) ?? events.FirstOrDefault(SoundLibrary.IsLooping);
			Check(ref ok, events.Count > 50 && loop != null, "Raft's sounds can be listed (" + events.Count + "), with loops like " + loop);
			if (loop != null)
			{
				SoundPickerWindow.Use(sound, loop);
				Check(ref ok, ObjectProps.Get(sound.Props, ObjectProps.SoundEvent) == loop && ObjectProps.Get(sound.Props, ObjectProps.SoundMode) == "", "choosing a loop makes it play while inside");
				SoundLibrary.Preview(loop);
				yield return new WaitForSecondsRealtime(1f);
				SoundLibrary.StopPreview();
			}
			TransformGizmoSelect(sound.transform);
			yield return null; yield return null;
			Screenshot(new[] { "sound_inspector" });
			yield return new WaitForSecondsRealtime(0.5f);
			SoundPickerWindow.Open(sound);
			yield return null;
			Screenshot(new[] { "sound_picker" });
			yield return new WaitForSecondsRealtime(0.5f);
			SoundPickerWindow.Close();
			TransformGizmoSelect(atmo.transform);
			yield return null; yield return null;
			Screenshot(new[] { "atmosphere_inspector" });
			yield return new WaitForSecondsRealtime(0.5f);
			DynamicIslands.EditorGizmoHandler.ClearTargets(false);

			bool saved = DynamicIslands.SaveIsland("ciambience");
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			yield return null;
			DynamicIslands.LoadIsland("ciambience");
			yield return new WaitForSecondsRealtime(1f);
			EditorGameObject a2 = placed.GetComponentsInChildren<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == ContentCatalog.AtmosphereZoneName);
			EditorGameObject s2 = placed.GetComponentsInChildren<EditorGameObject>().FirstOrDefault(e => e.GameObjectName == ContentCatalog.SoundZoneName);
			Check(ref ok, saved && a2 != null && s2 != null && ObjectProps.Get(a2.Props, ObjectProps.AtmoParticles) == "fireflies" && ObjectProps.Get(s2.Props, ObjectProps.SoundEvent) == (loop ?? ""), "both zones save and load");
			File.Delete(IslandSpawner.PathFor("ciambience"));
			if (ok) Log("PASS: atmosphere and sound zones in the editor"); else Fail("atmosphere and sound zones in the editor");
		}

		[ConsoleCommand(name: "CIAmbienceWorld", docs: "Dev, in game (host): an island with an atmosphere zone and a sound zone; standing in them")]
		public static void AmbienceWorld()
		{
			StartTest(AmbienceWorldRoutine());
		}

		static IEnumerator AmbienceWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			string loop = SoundLibrary.Events.FirstOrDefault(e => e.ToLowerInvariant().Contains("ambien") && SoundLibrary.IsLooping(e)) ?? SoundLibrary.Events.FirstOrDefault(SoundLibrary.IsLooping);
			IslandFile f = SampleIsland();
			f.Name = "ciambience";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.Add(new IslandObject { Name = ContentCatalog.AtmosphereZoneName, Position = ground(c.x, c.y), Props = new Dictionary<string, string> {
				{ ObjectProps.ZoneRadius, "50" }, { ObjectProps.AtmoFog, "#8899AA" }, { ObjectProps.AtmoParticles, "mist" } } });
			f.Objects.Add(new IslandObject { Name = ContentCatalog.SoundZoneName, Position = ground(c.x, c.y), Props = new Dictionary<string, string> {
				{ ObjectProps.ZoneRadius, "50" }, { ObjectProps.SoundEvent, loop ?? "" } } });
			f.Objects.Add(new IslandObject { Name = "Note_Sign", Position = ground(c.x + 3f, c.y + 3f), Props = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Misty Hollow" } } });
			f.Save(IslandSpawner.PathFor("ciambience"));

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("ciambience"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("ciambience", spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Fail("the island did not spawn"); yield break; }
			SoundZone sz = entry.Root.GetComponentInChildren<SoundZone>();
			AtmosphereZone az = entry.Root.GetComponentInChildren<AtmosphereZone>();
			TextMesh signText = entry.Root.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t => t.name == ContentCatalog.SignTextName);
			Check(ref ok, sz != null && az != null, "both zones are on the island");
			Check(ref ok, signText != null && signText.text.Contains("Misty"), "the sign shows its title: " + (signText != null ? signText.text.Replace("\n", " ") : "none"));
			Check(ref ok, sz != null && !sz.Playing, "the sound is off while the player is away");
			yield return StandRoutine(entry.Root);
			yield return new WaitForSeconds(1f);
			float w;
			AtmosphereZone.Strongest(Camera.main.transform.position, out w);
			Check(ref ok, sz != null && sz.Playing, "standing in the sound zone plays " + loop);
			Check(ref ok, w > 0.5f, "the atmosphere acts around the player (" + w.ToString("F2") + ")");
			Screenshot(new[] { "atmosphere_world" });
			yield return new WaitForSeconds(1f);

			IslandWorldState.RemoveIds(new[] { entry.Id }, true);
			File.Delete(IslandSpawner.PathFor("ciambience"));
			if (ok) Log("PASS: atmosphere and sound zones in a world"); else Fail("atmosphere and sound zones in a world");
		}

		#endregion

		#region Quests

		static IslandQuest TestQuest()
		{
			var q = new IslandQuest { Title = "The lost camp", Intro = "Someone camped here. Find out who.", Done = "You found the camp's secrets!", Reward = ContentCatalog.PresetLoot(new[] { "Plank*5", "Rope*2" }) };
			q.Steps.Add(new IslandQuest.Step { Type = "reach", Target = "camp" });
			q.Steps.Add(new IslandQuest.Step { Type = "read", Target = "Diary" });
			q.Steps.Add(new IslandQuest.Step { Type = "open", Target = "Supplies" });
			q.Steps.Add(new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 2, Text = "Chase off the warthogs" });
			return q;
		}

		[ConsoleCommand(name: "CIQuestTest", docs: "Dev, editor: the quest editor - a quest with four steps, the window, save and load")]
		public static void QuestTest()
		{
			StartTest(QuestTestRoutine());
		}

		static IEnumerator QuestTestRoutine()
		{
			yield return WaitForEditor(false);
			bool ok = true;
			Transform placed = GameObject.Find("PlacedObjects").transform;
			foreach (Transform child in placed) UnityEngine.Object.Destroy(child.gameObject);
			DynamicIslands.currentIslandProps.Clear();
			yield return null;
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel + 1f, 500f);
			EditorGameObject zone = PlaceForTest(ContentCatalog.TriggerZone, c0, placed);
			PropsCommand.Change(zone, ObjectProps.With(zone.Props, ObjectProps.ZoneId, "camp"));
			EditorGameObject diary = PlaceForTest("Note_Book", c0 + new Vector3(3, 0, 0), placed);
			NoteEditorWindow.Apply(diary, "Diary", "Day 1: ...");
			PlaceForTest("Creature_Boar", c0 + new Vector3(8, 0, 0), placed);
			QuestEditorWindow.Apply(TestQuest());
			IslandQuest back = IslandQuest.From(DynamicIslands.currentIslandProps);
			Check(ref ok, back.Steps.Count == 4 && back.Steps[3].Count == 2 && back.Steps[3].Describe() == "Chase off the warthogs" && back.Steps[1].Describe() == "Read \"Diary\"",
				"a quest with 4 steps is kept in the island's settings: " + string.Join(" / ", back.Steps.Select(s => s.Describe()).ToArray()));
			EditorUI.SetTab(TAB.Island);
			yield return null;
			Screenshot(new[] { "quest_tab" });
			yield return new WaitForSecondsRealtime(0.5f);
			QuestEditorWindow.Open();
			yield return null;
			Check(ref ok, QuestEditorWindow.IsOpen, "the quest editor opens");
			Screenshot(new[] { "quest_editor" });
			yield return new WaitForSecondsRealtime(0.5f);
			// Each step's kind is a list, and a small list beside its name has the island's names that kind can point at
			Transform qw = EditorUI.Canvas.transform.Find("QuestEditorWindow");
			List<UnityEngine.UI.Button> kinds = qw != null ? qw.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b => b.name == "Drop_StepType").ToList() : new List<UnityEngine.UI.Button>();
			List<UnityEngine.UI.Button> picks = qw != null ? qw.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b => b.name == "Pick_Target").ToList() : new List<UnityEngine.UI.Button>();
			var kindList = kinds.Count > 0 ? DropList.Shown(kinds[0]) : new List<KeyValuePair<string, string>>();
			Check(ref ok, kinds.Count == 4 && kindList.Count == IslandQuest.Types.Length && kindList.All(k => k.Value.Length > 0), "each step's kind is a list: " + kinds.Count + " steps, " + kindList.Count + " kinds, each saying what it asks");
			Func<int, string> listed = i => i < picks.Count ? string.Join(",", DropList.Shown(picks[i]).Select(o => o.Key).ToArray()) : "?";
			string p0 = listed(0), p1 = listed(1), p2 = listed(2);
			Check(ref ok, picks.Count == 3 && p0 == "camp" && p1 == "Diary" && p2 == "Warthog", "beside a step's name, the island's names it can point at: zones (" + p0 + "), notes (" + p1 + "), creatures (" + p2 + "); none for the chest step (no chest placed)");
			bool picked = kinds.Count > 1 && DropList.Click(kinds[1], "catch");
			yield return null;
			List<UnityEngine.UI.Button> after = qw.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b => b.name == "Drop_StepType").ToList();
			Check(ref ok, picked && after.Count == 4 && UIKit.LabelOf(after[1]).text == "Catch", "picking 'Catch' in step 2's list makes it a catch step (" + (after.Count > 1 ? UIKit.LabelOf(after[1]).text : "?") + ")");
			QuestEditorWindow.Close();
			Check(ref ok, IslandQuest.From(DynamicIslands.currentIslandProps).Steps[1].Type == "read", "closed without Save: the island's quest is as it was");
			bool saved = DynamicIslands.SaveIsland("ciquest");
			DynamicIslands.currentIslandProps.Clear();
			DynamicIslands.LoadIsland("ciquest");
			yield return new WaitForSecondsRealtime(1f);
			IslandQuest loaded = IslandQuest.From(DynamicIslands.currentIslandProps);
			Check(ref ok, saved && loaded.Steps.Count == 4 && loaded.Title == "The lost camp" && loaded.Reward == TestQuest().Reward, "the quest saves and loads with the island");
			File.Delete(IslandSpawner.PathFor("ciquest"));
			DynamicIslands.currentIslandProps.Clear();
			if (ok) Log("PASS: quest editor"); else Fail("quest editor");
		}

		[ConsoleCommand(name: "CIQuestWorld", docs: "Dev, in game (host): plays a quest - go to a zone, read a note, open a chest, defeat two warthogs - and gets the reward")]
		public static void QuestWorld()
		{
			StartTest(QuestWorldRoutine());
		}

		static IEnumerator QuestWorldRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			IslandFile f = SampleIsland();
			f.Name = "ciquest";
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, Vector3> ground = (x, z) => new Vector3(x, f.Heights[Mathf.RoundToInt(z / step), Mathf.RoundToInt(x / step)] * f.TerrainSize.y, z);
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 15f);
			f.Objects.Add(new IslandObject { Name = ContentCatalog.TriggerZone, Position = ground(c.x, c.y), Props = new Dictionary<string, string> { { ObjectProps.ZoneId, "camp" }, { ObjectProps.ZoneRadius, "5" } } });
			f.Objects.Add(new IslandObject { Name = "Note_Book", Position = ground(c.x + 3f, c.y), Props = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Diary" }, { ObjectProps.NoteText, "Day 1" } } });
			f.Objects.Add(new IslandObject { Name = "Loot_Chest", Position = ground(c.x - 3f, c.y), Props = new Dictionary<string, string> { { ObjectProps.NoteTitle, "Supplies" }, { ObjectProps.LootItems, "Nail*1" } } });
			f.Objects.Add(new IslandObject { Name = "Creature_Boar", Position = ground(c.x + 8f, c.y + 6f), Props = new Dictionary<string, string> { { ObjectProps.CreatureCount, "2" }, { ObjectProps.CreatureDamage, "0" } } });
			TestQuest().To(f.Props);
			f.Props[IslandProps.Title] = "Quest Isle";
			f.Save(IslandSpawner.PathFor("ciquest"));

			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius("ciquest"), 390f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			int before = IslandWorldState.Islands.Count;
			yield return DynamicIslands.instance.SpawnIslandFile("ciquest", spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.Skip(before).FirstOrDefault();
			if (entry == null || entry.Root == null) { Fail("the quest island did not spawn"); yield break; }
			// (the panel shows only close by - within 70 m of the land - and stays up out to 150 m; the user, 2026-10-07)
			IslandInfoTag tag = entry.Root.GetComponent<IslandInfoTag>();
			Vector3 mid = entry.Root.transform.position + tag.LocalCentre;
			Vector3 away = (RAPI.GetLocalPlayer().transform.position - mid).normalized;
			away.y = 0f;
			if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
			away.Normalize();
			yield return PutPlayer(RAPI.GetLocalPlayer(), mid + away * (tag.Radius + 110f), true);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, !QuestTracker.PanelShown, "no quest panel 110 m off the land");
			yield return PutPlayer(RAPI.GetLocalPlayer(), mid + away * (tag.Radius + 50f), true);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.PanelShown, "the quest panel shows 50 m off the land");
			yield return PutPlayer(RAPI.GetLocalPlayer(), mid + away * (tag.Radius + 110f), true);
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.PanelShown, "and stays up when the player moves back out to 110 m");
			yield return StandRoutine(entry.Root); // on the island: the quest shows
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.QuestOf(entry).Steps.Count == 4 && QuestTracker.StepOf(entry) == 0, "the island has its quest, at step 1");
			Check(ref ok, GameObject.Find("CustomIslands_Quest") != null && GameObject.Find("CustomIslands_Quest").GetComponentInChildren<Text>() != null, "the quest panel shows near the island");
			Screenshot(new[] { "quest_panel" });
			// (its X closes it until the step changes - ROADMAP CT10)
			GameObject.Find("CustomIslands_Quest").GetComponentsInChildren<Button>(true).First(b => b.name == "Close").onClick.Invoke();
			yield return new WaitForSeconds(1f);
			Check(ref ok, !QuestTracker.PanelShown, "the panel's X closes it");

			IslandNetMessage sent = null;
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.QuestStep) sent = m; };
			try
			{
				entry.Root.GetComponentInChildren<TriggerZone>().Enter();
				Check(ref ok, QuestTracker.StepOf(entry) == 1 && sent != null && sent.Index == 1, "walking into 'camp' does step 1 (and tells the others)");
				// The wrong note doesn't count, the right one does
				QuestTracker.Event(entry, "read", "Something else");
				Check(ref ok, QuestTracker.StepOf(entry) == 1, "reading another note doesn't count");
				CustomNote diary = entry.Root.GetComponentsInChildren<CustomNote>().First(n => n.Title == "Diary");
				NoteReader.Open(diary);
				NoteReader.Close();
				Check(ref ok, QuestTracker.StepOf(entry) == 2, "reading the diary does step 2");
				entry.Root.GetComponentInChildren<LootCrate>().Open();
				NoteReader.Close();
				Check(ref ok, QuestTracker.StepOf(entry) == 3, "opening the supplies does step 3");
			}
			finally { IslandNetwork.Loopback = null; }

			float t0 = Time.realtimeSinceStartup;
			CreatureSpawnPoint boars = entry.Root.GetComponentInChildren<CreatureSpawnPoint>();
			while ((boars == null || boars.Spawned.Count < 2) && Time.realtimeSinceStartup - t0 < 30f) { yield return new WaitForSeconds(0.5f); boars = entry.Root.GetComponentInChildren<CreatureSpawnPoint>(); }
			PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
			int planks = inv.GetItemCount("Plank");
			foreach (AI_NetworkBehaviour ai in boars.Spawned.ToList())
			{
				ai.networkEntity.Damage(100000f, ai.transform.position, Vector3.up, EntityType.Player, true);
				yield return new WaitForSeconds(1.5f);
				if (QuestTracker.StepOf(entry) == 3) Log("  defeated one: " + QuestTracker.QuestOf(entry).Steps[3].Describe());
			}
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.PanelShown, "the panel is back once the next step came");
			Check(ref ok, QuestTracker.StepOf(entry) == 4, "defeating both warthogs finishes the quest (step " + QuestTracker.StepOf(entry) + ")");
			Check(ref ok, QuestTracker.LastMessage != null && QuestTracker.LastMessage.StartsWith("Quest complete: The lost camp"), "\"" + QuestTracker.LastMessage + "\"");
			Check(ref ok, inv.GetItemCount("Plank") - planks == 5, "the reward arrives (planks +" + (inv.GetItemCount("Plank") - planks) + ")");
			Screenshot(new[] { "quest_done" });
			yield return new WaitForSeconds(1f);
			// (a finished quest's panel goes by itself after DoneHideSeconds (30 s; 3 s here) - ROADMAP CT10)
			Check(ref ok, QuestTracker.PanelShown, "the panel stays up a while after the quest is done");
			QuestTracker.DoneHideSeconds = 3f;
			yield return new WaitForSeconds(3f);
			Check(ref ok, !QuestTracker.PanelShown, "the finished quest's panel hides by itself");
			// (the journal: the island's line with its finished quest in light green - ROADMAP CT8)
			JournalWindow.Open();
			yield return new WaitForSeconds(1f);
			Text line = UnityEngine.Object.FindObjectsOfType<Text>().FirstOrDefault(t => t.name == "Island" && t.text.Contains("The lost camp"));
			Check(ref ok, line != null && line.text.Contains("√ done") && line.color.g > 0.8f && line.color.r < 0.7f, "the journal's line of the finished quest is light green" + (line != null ? " (" + line.color + ")" : " (no line)"));
			Screenshot(new[] { "journal_quest_done" });
			yield return new WaitForSeconds(0.6f);
			JournalWindow.Close();
			QuestTracker.DoneHideSeconds = 30f;

			// Saved with the world's state; a message from another player sets it too
			ObjectState s;
			Check(ref ok, entry.State.TryGetValue(QuestTracker.StepKey, out s) && s.Yield == 4, "the quest's progress is kept with the island (" + IslandObjectState.Encode(new Dictionary<int, ObjectState> { { QuestTracker.StepKey, entry.State[QuestTracker.StepKey] } }) + ")");

			IslandWorldState.RemoveIds(new[] { entry.Id }, true);
			File.Delete(IslandSpawner.PathFor("ciquest"));
			if (ok) Log("PASS: quests in a world"); else Fail("quests in a world");
		}

		#endregion

		[ConsoleCommand(name: "CIHierarchy", docs: "Dev: logs a catalog object's parts (names, components, local positions, sizes): CIHierarchy <object name>")]
		public static void Hierarchy(string[] args)
		{
			string name = args != null ? string.Join(" ", args) : "";
			DynamicIslands.instance.StartCoroutine(HierarchyRoutine(name));
		}

		static IEnumerator HierarchyRoutine(string name)
		{
			// (an on-demand object - Raft's shark cage, RF3's survivor cage made from it - is loaded first)
			yield return PlaceableCatalog.EnsureBuilt();
			if (PlaceableCatalog.Get(name) == null && PlaceableCatalog.IsIndexed(name)) yield return PlaceableCatalog.EnsureLoaded(new List<string> { name });
			GameObject proto = PlaceableCatalog.Get(name);
			if (proto == null) { Fail("no catalog object '" + name + "'"); yield break; }
			foreach (Transform t in proto.GetComponentsInChildren<Transform>(true))
			{
				int depth = 0;
				for (Transform p = t; p != proto.transform; p = p.parent) depth++;
				MeshFilter mf = t.GetComponent<MeshFilter>();
				Rigidbody rb = t.GetComponent<Rigidbody>();
				Log(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (off)") + " pos " + t.localPosition.ToString("F2") + " rot " + t.localEulerAngles.ToString("F0") + " scale " + t.localScale.ToString("F2") +
					" [" + string.Join(", ", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).ToArray()) + "]" +
					(rb != null ? " rigidbody " + (rb.isKinematic ? "kinematic" : "FREE") : "") +
					(mf != null && mf.sharedMesh != null ? " mesh " + mf.sharedMesh.name + " " + mf.sharedMesh.bounds.size.ToString("F2") : ""));
			}
			// (each mesh: readable or not, its parts (submeshes) with their materials and bounds)
			foreach (MeshRenderer r in proto.GetComponentsInChildren<MeshRenderer>(true))
			{
				MeshFilter mf = r.GetComponent<MeshFilter>();
				Mesh m = mf != null ? mf.sharedMesh : null;
				if (m == null) continue;
				Log(r.name + ": mesh " + m.name + " readable " + m.isReadable + ", " + m.subMeshCount + " parts, " + m.vertexCount + " vertices");
				for (int i = 0; i < m.subMeshCount; i++)
				{
					Material mat = i < r.sharedMaterials.Length ? r.sharedMaterials[i] : null;
					var d = m.GetSubMesh(i);
					Log("  part " + i + ": " + d.indexCount / 3 + " triangles, bounds " + d.bounds.center.ToString("F2") + " size " + d.bounds.size.ToString("F2") + ", material " +
						(mat != null ? mat.name + " (" + (mat.shader != null ? mat.shader.name : "?") + (mat.HasProperty("_Color") ? ", colour " + mat.color : "") + ")" : "none"));
				}
			}
			Log("hierarchy of '" + name + "' done");
		}

		[ConsoleCommand(name: "CINoteLook", docs: "Dev, in game: stands the player in front of the nearest readable note and checks Raft's own interaction ray finds it")]
		public static void NoteLook()
		{
			StartTest(NoteLookRoutine());
		}

		static IEnumerator NoteLookRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			CustomNote note = player != null ? UnityEngine.Object.FindObjectsOfType<CustomNote>().OrderBy(n => (n.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault() : null;
			if (note == null) { Fail("no player or no readable note (run CICreatureTest keep first)"); yield break; }
			Vector3 target = note.GetComponent<Collider>().bounds.center;
			Vector3 away = Vector3.ProjectOnPlane(note.transform.parent.forward, Vector3.up).normalized;
			if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
			// On the ground 1.8 m in front of it
			Vector3 stand = target + away * 1.8f;
			RaycastHit ground;
			if (Physics.Raycast(stand + Vector3.up * 10f, Vector3.down, out ground, 30f, 1 << IslandSpawner.TerrainLayer)) stand.y = ground.point.y + 0.2f;
			CharacterController cc = player.PersonController.controller;
			cc.enabled = false;
			player.transform.position = stand;
			player.PersonController.SwitchControllerType(ControllerType.Ground);
			cc.enabled = true;
			yield return new WaitForSeconds(1f);
			Camera cam = Helper.MainCamera;
			cam.transform.LookAt(target);
			RaycastInteractable found = Helper.FindInteractable(Player.UseDistance * 1.1f, QueryTriggerInteraction.Collide);
			bool ok = found != null && found.gameObject == note.gameObject && found.RaycastableObjects.Contains(note);
			if (ok) Log("PASS: Raft's interaction ray finds the note \"" + note.Title + "\" from " + Vector3.Distance(cam.transform.position, target).ToString("F1") + " m");
			else Fail("Raft's interaction ray found " + (found != null ? found.name : "nothing") + " instead of the note (camera at " + cam.transform.position + ", note at " + target + ")");
		}

		[ConsoleCommand(name: "CIMainMenu", docs: "Dev, in game or the editor: leaves the world (saving) for the main menu, as Raft's pause menu does (in the editor: as its Main menu button does)")]
		public static void MainMenu()
		{
			// (a running CIOptionsMatrix ends first, the world's own options back before the save)
			StopExclusive();
			// (test delays end with the test world: they stayed set for the session - CB13)
			Claims.TestAnswerDelay = 0f; CustomIslandSpawner.TestGenerateDelay = 0f;
			// (the editor isn't a game: leave it as its Main menu button does)
			if (DynamicIslands.InEditor())
			{
				Log("Leaving the editor");
				UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
				StartTest(WaitForMainMenu());
				return;
			}
			// (already at the main menu: nothing to leave - LeaveGame threw a NullReferenceException there)
			if (!LoadSceneManager.IsGameSceneLoaded || ComponentManager<Raft_Network>.Value == null) { Log("At the main menu already (not in a world or the editor)"); return; }
			Log("Leaving the world");
			ComponentManager<Raft_Network>.Value.LeaveGame((DisconnectReason)2, (SceneName)1, true, false);
			StartTest(WaitForMainMenu());
		}

		/// <summary>CIMainMenu's end (10-20): "At the main menu" once the menu's New Game box is there (60 s at most), then
		/// IDLE - so "CIMainMenu;CINewWorld ..." and scripts wait for the menu itself instead of sleeping.</summary>
		static IEnumerator WaitForMainMenu()
		{
			float start = Time.realtimeSinceStartup, end = start + 60f;
			yield return null;
			while (Time.realtimeSinceStartup < end && MenuNewGameBox() == null) yield return new WaitForSecondsRealtime(0.1f);
			if (MenuNewGameBox() == null) { Fail("main menu: not there after 60 s"); yield break; }
			Log("At the main menu (after " + Took(start) + ")");
		}

		[ConsoleCommand(name: "CIModelCheck", docs: "Dev, editor (after a world): creatures show Raft's real models instead of markers; places a few and takes a screenshot")]
		public static void ModelCheck()
		{
			StartTest(ModelCheckRoutine());
		}

		static IEnumerator ModelCheckRoutine()
		{
			yield return WaitForEditor(false);
			int models = ContentCatalog.Creatures.Count(k => PlaceableCatalog.Get(k.Name) != null && PlaceableCatalog.Get(k.Name).transform.Find("Model") != null);
			EditorUI.SetTab(TAB.ObjectPlace);
			Transform placed = GameObject.Find("PlacedObjects").transform;
			Vector3 c0 = terraineditor.terrain.transform.position + new Vector3(500f, DynamicIslands.EditorWaterLevel, 500f);
			string[] show = { "Creature_Boar", "Creature_Llama", "Creature_Bear", "Creature_Chicken", "Creature_Puffin", "Creature_ButlerBot", "Creature_StoneBird_Caravan" };
			for (int i = 0; i < show.Length; i++)
			{
				EditorGameObject e = PlaceForTest(show[i], c0 + new Vector3(i * 4f - 12f, 0, 0), placed);
				if (e != null && i == 0) PropsCommand.Change(e, ObjectProps.With(e.Props, ObjectProps.TintColor, "#6080FF"));
			}
			Camera.main.transform.position = c0 + new Vector3(0, 5f, -14f);
			Camera.main.transform.rotation = Quaternion.Euler(15f, 0, 0);
			yield return new WaitForSecondsRealtime(0.5f);
			Screenshot(new[] { "creature_models" });
			if (models == ContentCatalog.Creatures.Length) Log("PASS: all " + models + " creatures show Raft's models in the editor");
			else Fail("only " + models + " of " + ContentCatalog.Creatures.Length + " creatures show Raft's models (visit a world first)");
		}

		#endregion
	}
}
