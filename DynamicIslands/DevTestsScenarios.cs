using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Scenario tests (TEST_PROTOCOL §5j, TEST_CATALOGUE §Y): Raft's own gameplay meeting the mod's islands - the raft
	/// sailing into islands and under flying ones, a full inventory, keys, dying and sleeping, catching, game modes. Each
	/// makes its own test islands (names starting "cisc"), puts everything back and removes them at the end.
	/// </summary>
	public static partial class DevTests
	{
		#region Scenario helpers

		/// <summary>The tests' own sample island: DevTests_sample.island, packed into the dev .rmod (pack.ps1 -Release leaves
		/// it out) - not a file in the player's Mods folder (the 10-16 cleanup moved generated_sample.island out of it).</summary>
		internal const string SampleFixture = "DevTests_sample.island";

		static IslandFile SampleIsland()
		{
			byte[] bytes = DynamicIslands.instance != null ? DynamicIslands.instance.GetEmbeddedFileBytes(SampleFixture) : null;
			if (bytes == null || bytes.Length == 0) throw new System.IO.FileNotFoundException("the test sample island " + SampleFixture + " is not in the .rmod (pack.ps1 without -Release)");
			return IslandFile.FromBytes(bytes, SampleFixture);
		}

		/// <summary>The sample island saved in Mods as "cisample", for tests that spawn or send an island by its file name -
		/// instead of whichever island the player happens to have saved. RemoveSampleIsland moves it out again.</summary>
		internal const string SampleSaved = "cisample";

		static string SaveSampleIsland()
		{
			IslandFile f = SampleIsland();
			f.Name = SampleSaved;
			f.Save(IslandSpawner.PathFor(SampleSaved));
			IslandCache.Forget();
			return SampleSaved;
		}

		static void RemoveSampleIsland()
		{
			try { if (System.IO.File.Exists(IslandSpawner.PathFor(SampleSaved))) System.IO.File.Delete(IslandSpawner.PathFor(SampleSaved)); } catch { }
			IslandCache.Forget();
		}

		/// <summary>The sample island under another name, at this elevation, with a title - a copy a test fills.</summary>
		static IslandFile ScIsland(string name, string title, float elevation = 0f)
		{
			IslandFile f = SampleIsland();
			f.Name = name;
			f.Elevation = elevation;
			f.Props[IslandProps.Title] = title;
			return f;
		}

		/// <summary>Ground height (m above the terrain's base) of an island file at a point of its build area.</summary>
		static float ScGround(IslandFile f, Vector2 p)
		{
			float step = f.TerrainSize.x / (f.HeightmapResolution - 1);
			int x = Mathf.Clamp(Mathf.RoundToInt(p.x / step), 0, f.HeightmapResolution - 1), z = Mathf.Clamp(Mathf.RoundToInt(p.y / step), 0, f.HeightmapResolution - 1);
			return f.Heights[z, x] * f.TerrainSize.y;
		}

		/// <summary>A dry, gentle spot of the island near its land centre (offset), for content.</summary>
		static Vector3 ScDry(IslandFile f, Vector2 offset, int seed = 1)
		{
			var k = new MapKit(f, seed);
			Vector2 c = IslandSpawner.LandCentre(f) + offset;
			Vector2? p = k.Find(c, 30f, MapKit.Dry, 6f);
			Vector2 at = p ?? c;
			return new Vector3(at.x, ScGround(f, at), at.y);
		}

		/// <summary>A spot of the island under its own sea (for sea creatures, sunken chests): searched from the land outwards.</summary>
		static Vector3? ScUnderSea(IslandFile f, float depth)
		{
			Vector2 c = IslandSpawner.LandCentre(f);
			for (float r = 20f; r < 400f; r += 4f)
				for (int a = 0; a < 24; a++)
				{
					Vector2 p = c + new Vector2(Mathf.Cos(a * Mathf.PI / 12f), Mathf.Sin(a * Mathf.PI / 12f)) * r;
					if (p.x < 5f || p.y < 5f || p.x > f.TerrainSize.x - 5f || p.y > f.TerrainSize.z - 5f) continue;
					float g = ScGround(f, p);
					if (g < f.WaterLevel - depth) return new Vector3(p.x, g + 0.5f, p.y);
				}
			return null;
		}

		static IslandObject ScObj(string name, Vector3 at, params string[] kv)
		{
			GameObject proto = PlaceableCatalog.Get(name);
			return new IslandObject { Name = name, Position = at, EulerRotation = Vector3.zero, Scale = proto != null ? proto.transform.localScale : Vector3.one, Props = P(kv) };
		}

		/// <summary>Brings a saved island into the world as a plan rule does (a world entry, told to every player) and waits until it stands.</summary>
		static IEnumerator ScBring(string name, Vector3 at, List<IslandWorldState.Entry> into)
		{
			IslandWorldState.Entry entry = IslandWorldState.Add(name, at, null, false);
			entry.Loading = true;
			IslandNetwork.BroadcastAdded(entry);
			into.Add(entry);
			yield return DynamicIslands.instance.SpawnIslandFile(name, at, true, entry);
			for (float t = 0; entry.Root == null && !entry.Failed && t < 20f; t += 0.25f) yield return new WaitForSeconds(0.25f);
		}

		/// <summary>Open sea for this saved island within maxDistance of the raft, at this elevation (null: no room). With no
		/// room (Raft's own islands around - a new world starts beside one), the raft is moved 600 m on, up to 4 times.</summary>
		static Vector3? ScSpot(string file, float maxDistance, float elevation = 0f)
		{
			for (int tries = 0; tries < 5; tries++)
			{
				Vector3? raft = CustomIslandSpawner.RaftPosition;
				if (!raft.HasValue) return null;
				Vector3? s = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(file), maxDistance);
				if (s.HasValue) return new Vector3(s.Value.x, elevation, s.Value.z);
				Raft r = UnityEngine.Object.FindObjectOfType<Raft>();
				if (r == null || r.body == null) return null;
				Vector3 dir = Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward;
				r.body.position = r.body.position + dir * 600f;
				r.body.velocity = Vector3.zero;
				Physics.SyncTransforms();
				OnRaftCommand();
				Log("  (no open sea for '" + file + "' near the raft: the raft moved 600 m on)");
			}
			return null;
		}

		static void ScRemove(List<IslandWorldState.Entry> made, params string[] files)
		{
			IslandWorldState.RemoveIds(made.Where(e => e != null).Select(e => e.Id).ToList(), true);
			made.Clear();
			foreach (string f in files)
				try { if (System.IO.File.Exists(IslandSpawner.PathFor(f))) System.IO.File.Delete(IslandSpawner.PathFor(f)); } catch { }
			IslandCache.Forget();
		}

		static IslandObjectRef ScObjOf(IslandWorldState.Entry e, string name)
		{
			return e != null && e.Root != null ? e.Root.GetComponentsInChildren<IslandObjectRef>(true).FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) : null;
		}

		static void ScReadNote(IslandWorldState.Entry e, string title)
		{
			CustomNote note = e.Root.GetComponentsInChildren<CustomNote>(true).FirstOrDefault(c => c.GetComponent<LootCrate>() == null && (c.Title ?? "").IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0);
			if (note == null) { Log("  (no note '" + title + "' on '" + e.HostName + "')"); return; }
			PutPlayerNear(note.transform);
			NoteReader.Open(note);
			NoteReader.Close();
		}

		static List<string> ScOpenChest(IslandWorldState.Entry e, string title)
		{
			LootCrate chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(c =>
			{
				IslandObjectRef r = c.GetComponentInParent<IslandObjectRef>();
				return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle).Equals(title, StringComparison.OrdinalIgnoreCase);
			});
			if (chest == null) { Log("  (no chest '" + title + "' on '" + e.HostName + "')"); return null; }
			PutPlayerNear(chest.transform);
			chest.LastGiven = new List<string>();
			return chest.Open();
		}

		static bool ScEnterZone(IslandWorldState.Entry e, string id)
		{
			TriggerZone z = e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
			if (z == null) { Log("  (no zone '" + id + "' on '" + e.HostName + "')"); return false; }
			PutPlayerNear(z.transform, 0.3f);
			z.Enter();
			return true;
		}

		static void ScUse(IslandWorldState.Entry e, string name)
		{
			IslandObjectRef r = ScObjOf(e, name);
			if (r == null) { Log("  (no object '" + name + "' on '" + e.HostName + "')"); return; }
			PutPlayerNear(r.transform);
			// (a survivor cage opens as a player's E on it does: SurvivorCage.Use, not the behaviours' use)
			SurvivorCage cage = r.GetComponentInChildren<SurvivorCage>(true) ?? r.GetComponentInParent<SurvivorCage>();
			if (cage != null) { Log("  (survivor cage '" + name + "': " + (cage.Use() ? "opened" : "stays shut") + ")"); return; }
			Behaviours.Fire(e, r.Index, "use", true);
		}

		/// <summary>Live animals of an island's spots of this kind label (e.g. "Warthog").</summary>
		static List<AI_NetworkBehaviour> ScAnimals(IslandWorldState.Entry e, string label)
		{
			if (e == null || e.Root == null) return new List<AI_NetworkBehaviour>();
			return AnimalsOf(e, label, 150f).Where(a => a != null && a.networkEntity != null && !a.networkEntity.IsDead).ToList();
		}

		static IEnumerator ScWaitAnimals(IslandWorldState.Entry e, string label, int count, float seconds)
		{
			for (float t = 0; t < seconds && ScAnimals(e, label).Count < count; t += 0.5f) yield return new WaitForSeconds(0.5f);
		}

		static void ScKill(AI_NetworkBehaviour a, float damage = 9999f)
		{
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (host != null && a != null && a.networkEntity != null)
				host.DamageEntity(a.networkEntity, a.transform, damage, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
		}

		static float ScFlat(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

		static bool ScOnRaft(Network_Player p, Rigidbody body)
		{
			if (p == null || body == null) return false;
			return (p.PersonController != null && p.PersonController.HasRaftAsParent) || (ScFlat(p.transform.position, body.position) < 15f && Mathf.Abs(p.transform.position.y - body.position.y) < 4f);
		}

		/// <summary>Pushes the raft along dir at speed for up to seconds; stops early when it hasn't moved half a metre in 2 s
		/// (stuck) or has gone 'until' metres. Returns via the out list: [moved, stuck (1/0), most it rose (m), most it tilted (deg)].</summary>
		static IEnumerator ScPush(Vector3 dir, float speed, float seconds, float until, List<float> result, Vector3? watch = null)
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Rigidbody body = raft != null ? raft.body : null;
			result.Clear();
			if (body == null) { result.AddRange(new[] { 0f, 1f, 0f, 0f, 0f }); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			float y0 = body.position.y, t = 0f, moved = 0f, rose = 0f, tilt = 0f, window = 0f;
			float closest = watch.HasValue ? ScFlat(body.position, watch.Value) : 0f;
			Vector3 last = body.position, windowStart = body.position;
			bool stuck = false;
			while (t < seconds)
			{
				yield return new WaitForFixedUpdate();
				t += Time.fixedDeltaTime;
				KeepAlive(player);
				// (pushed like a sail: a force up to the speed, never forcing the velocity - setting it every step overrode the
				// collision with the shore and slid the raft up a gentle beach, which a sail can't do)
				Vector3 flat = Flat(body.velocity);
				// (steered at what it is pushed into, with the sideways drift of Raft's current taken out: the current carried
				// the raft 67 m past a small ghost raft 275 m away)
				Vector3 aim = watch.HasValue && ScFlat(body.position, watch.Value) > 3f ? Flat(watch.Value - body.position).normalized : dir;
				body.AddForce(-(flat - Vector3.Dot(flat, aim) * aim) * 2f, ForceMode.Acceleration);
				if (Vector3.Dot(flat, aim) < speed) body.AddForce(aim * 6f, ForceMode.Acceleration);
				Vector3 d = Flat(body.position - last);
				if (d.magnitude < 50f) moved += d.magnitude; // (a world shift jumps the position)
				last = body.position;
				rose = Mathf.Max(rose, body.position.y - y0);
				if (watch.HasValue) closest = Mathf.Min(closest, ScFlat(body.position, watch.Value));
				tilt = Mathf.Max(tilt, Vector3.Angle(body.transform.up, Vector3.up));
				window += Time.fixedDeltaTime;
				if (window >= 2f)
				{
					float w = Flat(body.position - windowStart).magnitude;
					window = 0f; windowStart = body.position;
					if (w < 0.5f && w >= 0f && t > 3f)
					{
						stuck = true;
						// (what stops it: the nearest things in front of the raft and above it)
						foreach (RaycastHit h in Physics.SphereCastAll(body.position + Vector3.up * 1f, 3f, dir, 12f, ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.attachedRigidbody != body && !h.collider.transform.IsChildOf(raft.transform) && h.collider.GetComponentInParent<Block>() == null).OrderBy(h => h.distance).Take(3))
							Log("  stuck at " + body.position.ToString("F0") + ": in front " + h.collider.name + " (" + LayerMask.LayerToName(h.collider.gameObject.layer) + ", " + (h.collider.transform.root != null ? h.collider.transform.root.name : "-") + ") at " + h.distance.ToString("F1") + " m");
						break;
					}
				}
				if (moved >= until) break;
			}
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			result.AddRange(new[] { moved, stuck ? 1f : 0f, rose, tilt, closest });
		}

		static int? ScDay { get { try { return WorldManager.DayCounter; } catch { return null; } } }

		/// <summary>Gives the world another plan as the WorldPlan command (and Esc > Custom Islands) does: the director's
		/// plan and its story chain (SetPlan alone leaves the chain of the plan before).</summary>
		static bool ScSetPlan(string name, bool applyRandom)
		{
			if (!WorldDirector.SetPlan(name, applyRandom)) return false;
			StoryChain.FromPlan(WorldDirector.Plan);
			return true;
		}

		#endregion

		#region SC1, SC2 - the raft meets islands

		[ConsoleCommand(name: "CIScRam", docs: "Dev, world (host, a test world 'CI ...'): SC1 - the raft pushed into an island at 6 m/s stops at the shore, stays at the sea and level, the player stays on it, and it sails off when pushed back; the same against a ghost raft")]
		public static void ScRamCommand() { StartTest(ScRamRoutine()); }

		static IEnumerator ScRamRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario ram: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscram";
			try { ScIsland(isl, "Ram Rock").Save(IslandSpawner.PathFor(isl)); } catch (Exception ex) { Fail("scenario ram: no sample island (" + ex.Message + ")"); yield break; }
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			var r = new List<float>();

			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario ram: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			Check(ref ok, e.Root != null, "the island comes " + ScFlat(e.Position, raft.body.position).ToString("F0") + " m from the raft (land reaches " + CustomIslandSpawner.LandRadius(isl).ToString("F0") + " m)");
			if (e.Root != null)
			{
				OnRaftCommand();
				yield return new WaitForSeconds(1.5f);
				Network_Player player = RAPI.GetLocalPlayer();
				Vector3 dir = Flat(e.Position - raft.body.position).normalized;
				yield return ScPush(dir, 6f, 45f, 1000f, r);
				float left = ScFlat(e.Position, raft.body.position);
				Check(ref ok, r[1] > 0f, "rammed at 6 m/s, the raft is stopped by the island (moved " + r[0].ToString("F0") + " m; " + left.ToString("F0") + " m from the island's middle)");
				Check(ref ok, r[2] < 1.2f && r[3] < 15f, "the raft stays at the sea and level (rose " + r[2].ToString("F2") + " m, tilted " + r[3].ToString("F1") + " degrees)");
				Check(ref ok, ScOnRaft(player, raft.body), "the player standing on the raft is still on it");
				yield return ScPush(-dir, 6f, 10f, 1000f, r);
				Check(ref ok, r[0] > 20f, "pushed back, the raft sails off (" + r[0].ToString("F0") + " m in 10 s)");
			}
			ScRemove(made, isl);

			// A ghost raft (blocks on the sea, no land): rammed the same way
			string before = string.Join(",", IslandWorldState.Islands.Select(x => x.Id.ToString()).ToArray());
			string why = CustomIslandSpawner.TrySpawn(CustomIslandSpawner.RaftPosition.Value, true, "type:ghostraft");
			IslandWorldState.Entry ghost = null;
			for (float t = 0; t < 40f; t += 0.5f)
			{
				ghost = IslandWorldState.Islands.FirstOrDefault(x => !before.Split(',').Contains(x.Id.ToString()) && (x.HostName ?? "").IndexOf(GhostRafts.TypeName, StringComparison.OrdinalIgnoreCase) >= 0);
				if (ghost != null && ghost.Root != null) break;
				yield return new WaitForSeconds(0.5f);
			}
			if (ghost == null || ghost.Root == null) Log("  (no ghost raft came: " + (why ?? "?") + " - skipped)");
			else
			{
				made.Add(ghost);
				OnRaftCommand();
				yield return new WaitForSeconds(1.5f);
				Vector3 dir = Flat(ghost.Position - raft.body.position).normalized;
				float startDist = ScFlat(ghost.Position, raft.body.position);
				yield return ScPush(dir, 6f, startDist / 4f + 20f, 1000f, r, ghost.Position); // (as far away as automatic islands come: 250-400 m)
				bool through = r[0] > startDist; // (went past its middle: through the blocks)
				Check(ref ok, r[1] > 0f && !through, "rammed at 6 m/s, a ghost raft stops the raft too (moved " + r[0].ToString("F0") + " of " + startDist.ToString("F0") + " m, closest " + r[4].ToString("F0") + " m to its middle" + (through ? ": THROUGH IT" : "") + ")");
				Check(ref ok, r[2] < 1.2f && r[3] < 15f, "... the raft stays level (rose " + r[2].ToString("F2") + " m, tilted " + r[3].ToString("F1") + ")");
				yield return ScPush(-dir, 6f, 10f, 1000f, r);
				Check(ref ok, r[0] > 20f, "... and sails off again (" + r[0].ToString("F0") + " m in 10 s)");
				ScRemove(made, ghost.Name);
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario ram"); else Fail("scenario ram");
		}

		[ConsoleCommand(name: "CIScBeached", docs: "Dev, world (host, a test world 'CI ...'): SC89 - an island in the raft's way, and only Raft's own current: the raft drifts onto its beach and stops at the shore - it doesn't climb the beach, the player stays on it - and stays so while pressed on, and when the island loads again with the raft at its shore")]
		public static void ScBeachedCommand() { StartTest(ScBeachedRoutine()); }

		/// <summary>How far the land of a loaded island stands above this point (negative: the point is above its ground, or
		/// off the island).</summary>
		static float ScLandAbove(IslandWorldState.Entry e, Vector3 p)
		{
			Terrain t = e != null && e.Root != null ? e.Root.GetComponentInChildren<Terrain>() : null;
			if (t == null || t.terrainData == null) return float.NegativeInfinity;
			Vector3 local = p - t.transform.position;
			if (local.x < 0f || local.z < 0f || local.x > t.terrainData.size.x || local.z > t.terrainData.size.z) return float.NegativeInfinity;
			return t.SampleHeight(p) + t.transform.position.y - p.y;
		}

		static IEnumerator ScBeachedRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario beached: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscbeach";
			try { ScIsland(isl, "Beach Isle").Save(IslandSpawner.PathFor(isl)); } catch (Exception ex) { Fail("scenario beached: no sample island (" + ex.Message + ")"); yield break; }
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Rigidbody body = raft.body;
			OnRaftCommand();
			yield return new WaitForSeconds(2f);
			// (Raft runs its raft aground with colliders of its own on the RaftCollision layer, added by its RaftCollisionManager:
			// the raft's other collider, BakedBlocks, doesn't touch islands at all. Rafts in long-used test worlds had none and
			// drifted through every island - ROADMAP R16)
			int stoppers = Physics.OverlapSphere(body.position, 40f, 1 << 9, QueryTriggerInteraction.Ignore).Length;
			Check(ref ok, stoppers > 0, "the raft has Raft's own colliders that run it aground (" + stoppers + " on RaftCollision)");
			// Where Raft's current takes the raft, and the island's shore ahead in that direction (as near as the mod puts an
			// island: its Clearance from the raft)
			Vector3 drift = Flat(body.velocity);
			Vector3 dir = drift.magnitude > 0.2f ? drift.normalized : Vector3.forward;
			float land = CustomIslandSpawner.LandRadius(isl);
			Vector3 spot = Vector3.zero; bool found = false;
			for (int move = 0; move < 4 && !found; move++)
			{
				foreach (float ahead in new[] { 5f, 40f, 90f })
					foreach (float side in new[] { 0f, land * 0.3f, -land * 0.3f })
					{
						if (found) break;
						Vector3 c = body.position + dir * (land + CustomIslandSpawner.Clearance + ahead) + Vector3.Cross(Vector3.up, dir) * side;
						c.y = 0f;
						if (CustomIslandSpawner.Rejects(c, land, body.position, false) == null) { spot = c; found = true; }
					}
				if (found) break;
				// (Raft's own islands ahead: the raft 600 m to the side, out of their way, as ScSpot moves it)
				body.position = body.position + Vector3.Cross(Vector3.up, dir) * 600f;
				body.velocity = dir * drift.magnitude;
				Physics.SyncTransforms();
				OnRaftCommand();
				Log("  (no open sea ahead of the raft: it moved 600 m to the side)");
				yield return new WaitForSeconds(3f);
			}
			if (!found) { ScRemove(made, isl); Fail("scenario beached: no open sea ahead of the raft (" + drift.magnitude.ToString("F2") + " m/s; straight ahead: " + CustomIslandSpawner.Rejects(body.position + dir * (land + CustomIslandSpawner.Clearance + 5f), land, body.position, false) + ")"); yield break; }
			yield return ScBring(isl, spot, made);
			// (on the raft again: moving the raft to open sea left the player behind)
			OnRaftCommand();
			yield return new WaitForSeconds(2f);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("scenario beached: the island didn't come"); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			Log("  the raft drifts at " + drift.magnitude.ToString("F2") + " m/s; the island's land reaches " + land.ToString("F0") + " m, its middle " + ScFlat(e.Position, body.position).ToString("F0") + " m away");
			// Raft's current only (no push): until it hasn't moved half a metre in 3 s, 3 minutes at most
			float y0 = body.position.y, rose = 0f, tilt = 0f, window = 0f, t = 0f;
			Vector3 start = body.position, windowStart = body.position;
			bool stopped = false;
			while (t < 180f)
			{
				yield return new WaitForSeconds(0.25f);
				t += 0.25f; window += 0.25f;
				KeepAlive(player);
				rose = Mathf.Max(rose, body.position.y - y0);
				tilt = Mathf.Max(tilt, Vector3.Angle(body.transform.up, Vector3.up));
				if (window >= 3f) { bool still = Flat(body.position - windowStart).magnitude < 0.5f; window = 0f; windowStart = body.position; if (still && t > 6f) { stopped = true; break; } }
			}
			Check(ref ok, stopped, "the current takes the raft onto the island's beach and it stops at the shore (" + ScFlat(start, body.position).ToString("F0") + " m in " + t.ToString("F0") + " s)");
			Check(ref ok, rose < 1.2f && tilt < 15f, "... at the sea and level - not up the beach (rose " + rose.ToString("F2") + " m, tilted " + tilt.ToString("F1") + " degrees)");
			Check(ref ok, ScLandAbove(e, body.position) < 0.5f, "... and not inside the island's land (" + ScLandAbove(e, body.position).ToString("F1") + " m of land above the raft's middle)");
			Check(ref ok, ScOnRaft(player, body), "... the player standing on the raft is still on it (" + (player != null ? ScFlat(player.transform.position, body.position).ToString("F1") + " m from its middle, " + (player.transform.position.y - body.position.y).ToString("F1") + " m above it" : "no player") + ")");
			// Pressed on by the current for half a minute more
			for (float s = 0f; s < 30f; s += 0.25f) { yield return new WaitForSeconds(0.25f); KeepAlive(player); rose = Mathf.Max(rose, body.position.y - y0); tilt = Mathf.Max(tilt, Vector3.Angle(body.transform.up, Vector3.up)); }
			Check(ref ok, rose < 1.2f && tilt < 15f, "pressed on by the current for 30 s, it stays at the sea (rose " + rose.ToString("F2") + " m, tilted " + tilt.ToString("F1") + ")");
			// The island loads again with the raft at its shore (sailing off and back, a load, a player joining)
			IslandObjectState.Capture(e);
			e.Loading = true;
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			float y1 = body.position.y; rose = 0f; tilt = 0f;
			for (float s = 0f; s < 8f; s += 0.25f) { yield return new WaitForSeconds(0.25f); KeepAlive(player); rose = Mathf.Max(rose, body.position.y - y0); tilt = Mathf.Max(tilt, Vector3.Angle(body.transform.up, Vector3.up)); }
			Check(ref ok, e.Root != null && rose < 1.2f && tilt < 15f, "the island loads again with the raft at its shore: the raft stays at the sea (rose " + rose.ToString("F2") + " m, tilted " + tilt.ToString("F1") + ")");
			Check(ref ok, ScLandAbove(e, body.position) < 0.5f && ScOnRaft(player, body), "... not inside the land, the player still on the raft (" + ScLandAbove(e, body.position).ToString("F1") + " m of land above)");
			ScRemove(made, isl);
			OnRaftCommand();
			if (ok) Log("PASS: scenario beached"); else Fail("scenario beached");
		}

		[ConsoleCommand(name: "CIRaftLayers", docs: "Dev, world: which layers the raft's colliders (RaftCollision, BakedBlocks) and the player collide with, and the layers of the colliders of Raft's own islands near the raft")]
		public static void RaftLayersCommand()
		{
			var named = Enumerable.Range(0, 32).Where(l => !string.IsNullOrEmpty(LayerMask.LayerToName(l))).ToList();
			foreach (int a in new[] { 9, 18, LayerMask.NameToLayer("Player") }.Where(x => x >= 0))
				Log("LAYERS " + LayerMask.LayerToName(a) + " (" + a + ") collides with: " + string.Join(", ", named.Where(b => !Physics.GetIgnoreLayerCollision(a, b)).Select(b => LayerMask.LayerToName(b) + "(" + b + ")").ToArray()));
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Vector3 at = raft != null ? raft.body.position : Vector3.zero;
			var islands = UnityEngine.Object.FindObjectsOfType<Collider>().Where(c => c.enabled && !c.isTrigger && (raft == null || !c.transform.IsChildOf(raft.transform)) && !c.transform.root.name.StartsWith("CustomIsland_") && c.GetComponentInParent<Network_Player>() == null)
				.GroupBy(c => LayerMask.LayerToName(c.gameObject.layer) + " " + c.GetType().Name).Select(g => g.Key + " x" + g.Count() + " (e.g. " + g.First().name + " under " + g.First().transform.root.name + ")").Take(25);
			foreach (string s in islands) Log("LAYERS scene collider " + s);
			// The colliders on RaftCollision anywhere (the raft's own against islands?), and where they are
			foreach (Collider c in Resources.FindObjectsOfTypeAll<Collider>().Where(c => c.gameObject.scene.IsValid() && c.gameObject.layer == 9).Take(12))
				Log("LAYERS RaftCollision collider " + c.name + " (" + c.GetType().Name + ") under " + (c.transform.parent != null ? c.transform.parent.name : "-") + " root " + c.transform.root.name + ", enabled " + c.enabled + ", active " + c.gameObject.activeInHierarchy +
					(raft != null ? ", " + ScFlat(c.bounds.center, at).ToString("F0") + " m from the raft" : ""));
			if (raft != null)
			{
				Component cons = raft.GetComponentInChildren(HarmonyLib.AccessTools.TypeByName("BlockCollisionConsolidator"), true);
				Log("LAYERS consolidator " + (cons != null ? cons.name + " on " + cons.gameObject.name : "none") + "; raft children colliders (all): " + string.Join(", ", raft.GetComponentsInChildren<Collider>(true).GroupBy(x => LayerMask.LayerToName(x.gameObject.layer) + " " + x.GetType().Name + (x.enabled && x.gameObject.activeInHierarchy ? "" : " off")).Select(g => g.Key + " x" + g.Count()).ToArray()));
			}
		}

		[ConsoleCommand(name: "CIRaftColliders", docs: "Dev, world: RAFTCOLLIDERS <n> - how many of Raft's island colliders (RaftCollision, the ones that run the raft aground) are on the raft now")]
		public static void RaftCollidersCommand()
		{
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (raft == null) { Fail("raft colliders: no raft"); return; }
			// (R16: the guard's look right now - it hooks Raft's grid up when Raft didn't)
			if (Raft_Network.IsHost) Log("raft collider guard: " + (RaftColliderGuard.Heal(false) ? "hooked Raft's grid up now" : "Raft's grid was hooked up") + ", healed " + RaftColliderGuard.Healed + " time(s) this session");
			Collider[] near = Physics.OverlapSphere(raft.body.position, 40f, 1 << 9, QueryTriggerInteraction.Ignore);
			Log("RAFTCOLLIDERS " + near.Length + " on RaftCollision within 40 m of the raft (" + string.Join(", ", near.Select(c => c.name + " under " + (c.transform.parent != null ? c.transform.parent.name : "-")).Distinct().Take(3).ToArray()) + ")");
		}

		[ConsoleCommand(name: "CIRaftColliderGuard", docs: "Dev, world (host, 'CI ...'): R16 - Raft's collider grid unhooked and emptied as in the worlds that drifted (no grounding colliders); the guard hooks it up again and the raft has them back")]
		public static void RaftColliderGuardCommand() { StartTest(RaftColliderGuardRoutine()); }

		static IEnumerator RaftColliderGuardRoutine()
		{
			if (!Raft_Network.IsHost || !LoadSceneManager.IsGameSceneLoaded) { Fail("raft collider guard: run in a world, as the host"); yield break; }
			bool ok = true;
			var rcm = UnityEngine.Object.FindObjectOfType<RaftCollisionManager>();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (rcm == null || raft == null) { Fail("raft collider guard: no raft"); yield break; }
			Func<int> count = () => Physics.OverlapSphere(raft.body.position, 40f, 1 << 9, QueryTriggerInteraction.Ignore).Length;
			int before = count();
			Check(ref ok, before > 0, "the raft has Raft's grounding colliders: " + before);
			// As in the worlds that drifted: Raft's manager doesn't hear of the blocks (it never listened) and has no grid
			var tb = HarmonyLib.Traverse.Create(HarmonyLib.Traverse.Create(rcm).Field("raftBounds").GetValue<RaftBounds>());
			foreach (string ev in new[] { "OnAddWalkableBlock", "OnRemoveWalkableBlocks" })
			{
				Delegate d = tb.Field(ev).GetValue<Delegate>();
				if (d != null) foreach (Delegate x in d.GetInvocationList()) if (x.Target == (object)rcm) d = Delegate.Remove(d, x);
				tb.Field(ev).SetValue(d);
			}
			HarmonyLib.Traverse.Create(rcm).Field("blocks").GetValue<List<Vector3>>().Clear();
			rcm.Initialize();
			yield return new WaitForFixedUpdate();
			yield return null;
			int gone = count();
			Check(ref ok, gone == 0, "unhooked and emptied: " + gone + " left (the drifting worlds had none)");
			bool healed = RaftColliderGuard.Heal(false);
			yield return new WaitForFixedUpdate();
			yield return null;
			int after = count();
			Check(ref ok, healed && after >= before, "the guard hooked Raft's grid up again: " + after + " collider(s) (" + before + " before)");
			Check(ref ok, !RaftColliderGuard.Heal(false), "... and leaves it alone once it listens");
			if (ok) Log("PASS: raft collider guard"); else Fail("raft collider guard");
		}

		[ConsoleCommand(name: "CIDriftProbe", docs: "Dev, world (host, 'CI ...'): logs the raft each second for <seconds> (default 90) as Raft's current takes it towards 'ciscbeach' put ahead of it (CIScBeached's island): where, how fast, how much land above it, what it touches")]
		public static void DriftProbeCommand(string[] args) { StartTest(DriftProbeRoutine(args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 90f)); }

		static IEnumerator DriftProbeRoutine(float seconds)
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("drift probe: run in a world, as the host"); yield break; }
			const string isl = "ciscbeach";
			var made = new List<IslandWorldState.Entry>();
			try { ScIsland(isl, "Beach Isle").Save(IslandSpawner.PathFor(isl)); } catch (Exception ex) { Fail("drift probe: " + ex.Message); yield break; }
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Rigidbody body = raft.body;
			Vector3 dir = Flat(body.velocity).magnitude > 0.2f ? Flat(body.velocity).normalized : Vector3.forward;
			float land = CustomIslandSpawner.LandRadius(isl);
			Vector3 c = body.position + dir * (land + CustomIslandSpawner.Clearance + 5f); c.y = 0f;
			yield return ScBring(isl, c, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("drift probe: the island didn't come"); yield break; }
			Log("DRIFT layers raft " + LayerMask.LayerToName(9) + "/" + LayerMask.LayerToName(18) + " vs terrain " + LayerMask.LayerToName(IslandSpawner.TerrainLayer) + ": collide " + !Physics.GetIgnoreLayerCollision(9, IslandSpawner.TerrainLayer) + "/" + !Physics.GetIgnoreLayerCollision(18, IslandSpawner.TerrainLayer) +
				"; raft colliders: " + string.Join(", ", raft.GetComponentsInChildren<Collider>(false).Where(x => x.enabled && !x.isTrigger).GroupBy(x => LayerMask.LayerToName(x.gameObject.layer) + " " + x.GetType().Name).Select(g => g.Key + " x" + g.Count()).ToArray()));
			for (float t = 0f; t < seconds; t += 1f)
			{
				yield return new WaitForSeconds(1f);
				Vector3 p = body.position;
				string touching = string.Join(",", Physics.OverlapSphere(p, 4f, ~0, QueryTriggerInteraction.Ignore).Where(x => !x.transform.IsChildOf(raft.transform) && x.GetComponentInParent<Network_Player>() == null).Select(x => x.name + "/" + LayerMask.LayerToName(x.gameObject.layer)).Distinct().Take(4).ToArray());
				Log("DRIFT t=" + t.ToString("F0") + " to the island's middle " + ScFlat(e.Position, p).ToString("F0") + " m (land " + land.ToString("F0") + "), y " + p.y.ToString("F2") + ", vel " + body.velocity.ToString("F2") + ", land above " + ScLandAbove(e, p).ToString("F1") + ", speed " + raft.speed.ToString("F2") + " current " + raft.currentMovementSpeed.ToString("F2") + (touching.Length > 0 ? ", near " + touching : ""));
			}
			ScRemove(made, isl);
			OnRaftCommand();
			Log("PASS: drift probe");
		}

		[ConsoleCommand(name: "CIScFlyUnder", docs: "Dev, world (host, a test world 'CI ...'): SC2 - the raft pushed under an island flying at 60 m and over a sunken one passes; a low one at 4 m: what happens to the raft and the player on it")]
		public static void ScFlyUnderCommand() { StartTest(ScFlyUnderRoutine()); }

		static IEnumerator ScFlyUnderRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario fly under: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			var r = new List<float>();
			// (sunken: the sample island's highest point 12 m under the sea - its peak stands well above its own sea level)
			float peak = 30f;
			try { IslandFile sample = SampleIsland(); float top = 0f; foreach (float h in sample.Heights) top = Mathf.Max(top, h); peak = top * sample.TerrainSize.y - sample.WaterLevel; } catch { }
			foreach (float elevation in new[] { 60f, -(peak + 12f), 4f })
			{
				string isl = "ciscfly" + (elevation < 0 ? "sunk" : elevation.ToString("F0"));
				try { ScIsland(isl, "Fly " + elevation, elevation).Save(IslandSpawner.PathFor(isl)); } catch (Exception ex) { Fail("scenario fly under: " + ex.Message); yield break; }
				Vector3? spot = ScSpot(isl, 320f, elevation);
				if (!spot.HasValue) { Check(ref ok, false, "open sea for the island at " + elevation + " m"); ScRemove(made, isl); continue; }
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.LastOrDefault();
				if (e == null || e.Root == null) { Check(ref ok, false, "the island at " + elevation + " m came"); ScRemove(made, isl); continue; }
				OnRaftCommand();
				yield return new WaitForSeconds(1.5f);
				Network_Player player = RAPI.GetLocalPlayer();
				Vector3 dir = Flat(e.Position - raft.body.position).normalized;
				float through = ScFlat(e.Position, raft.body.position) + CustomIslandSpawner.LandRadius(isl) + 30f;
				yield return ScPush(dir, 6f, through / 2.5f + 30f, through, r); // (pushed like a sail, water drag keeps it near 3.5 m/s)
				bool passed = r[0] >= through - 1f;
				bool onRaft = ScOnRaft(player, raft.body);
				string what = elevation > 10f ? "flying at " + elevation + " m" : elevation < 0 ? "sunken (top under the sea)" : "flying low at " + elevation + " m";
				if (elevation != 4f)
				{
					Check(ref ok, passed, "the raft passes " + (elevation > 0 ? "under" : "over") + " an island " + what + " (" + r[0].ToString("F0") + " of " + through.ToString("F0") + " m)");
					if (!onRaft && player != null && player.PersonController != null)
						Log("  (the player: " + player.transform.position.ToString("F1") + ", the raft " + raft.body.position.ToString("F1") + ", on the raft as parent " + player.PersonController.HasRaftAsParent +
							", grounded " + player.PersonController.IsGrounded + ", standing on " + (player.PersonController.groundRaycastHit.collider != null ? player.PersonController.groundRaycastHit.collider.name : "nothing") +
							", health " + (player.Stats != null && player.Stats.stat_health != null ? player.Stats.stat_health.Value.ToString("F0") : "?") + ")");
					Check(ref ok, onRaft, "... the player standing on the raft is still on it");
				}
				else
				{
					// (its underside's lowest point is 2 m under its sea level: 2 m above the real sea - a bare raft passes, a
					// standing player may be pushed; either is fine as long as nothing gets stuck in it)
					Log("  low island at 4 m: the raft " + (passed ? "passed under" : "stopped") + " (" + r[0].ToString("F0") + " m), the player " + (onRaft ? "stayed on the raft" : "was pushed off"));
					yield return ScPush(-dir, 6f, 10f, 1000f, r);
					Check(ref ok, r[0] > 20f || passed, "an island flying at 4 m: the raft never gets stuck in it (it passed, or sails back off: " + r[0].ToString("F0") + " m)");
					Check(ref ok, player.transform.position.y > -3f, "... the player isn't pushed under the sea (y " + player.transform.position.y.ToString("F1") + ")");
				}
				ScRemove(made, isl);
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario fly under"); else Fail("scenario fly under");
		}

		#endregion

		#region SC3 - the world shifts while an island comes, is stood on, or is away

		/// <summary>Where an island's land centre is in the world now (its root is the terrain's corner).</summary>
		static Vector3 ScLandCentre(IslandWorldState.Entry e)
		{
			return e.Root != null ? e.Root.transform.position + LocalLandCentre(e.Root, e.Name) : e.Position;
		}

		/// <summary>An island's land centre from its root's corner: its IslandInfoTag's, else from its file, as the tag's is made
		/// (zero if neither). Since 10-19 only an island with a quest gets the tag (its banner): CIScShift took the terrain's
		/// corner of the quest-less sample island for its centre and measured 707 m off.</summary>
		internal static Vector3 LocalLandCentre(GameObject root, string fileName)
		{
			IslandInfoTag tag = root != null ? root.GetComponent<IslandInfoTag>() : null;
			if (tag != null) return tag.LocalCentre;
			try
			{
				IslandFile f = IslandFile.Load(IslandSpawner.PathFor(fileName));
				Vector2 land = IslandSpawner.LandCentre(f);
				return new Vector3(land.x, f.WaterLevel, land.y);
			}
			catch { return Vector3.zero; }
		}

		[ConsoleCommand(name: "CIScShift", docs: "Dev, world (host, 'CI ...'): SC3 - Raft's world shift (the origin moved) while an island is coming (with a world entry, as a plan rule brings it, and without, as SpawnIsland does - AU58), while the player stands on one, and while one is unloaded: everything ends where it should be")]
		public static void ScShiftCommand() { StartTest(ScShiftRoutine()); }

		static IEnumerator ScShiftRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario shift: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			WorldShiftManager wsm = UnityEngine.Object.FindObjectOfType<WorldShiftManager>();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			if (wsm == null || raft == null) { Fail("scenario shift: no WorldShiftManager or raft"); yield break; }
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscshift";
			IslandFile f;
			try { f = ScIsland(isl, "Shifty Isle"); } catch (Exception ex) { Fail("scenario shift: " + ex.Message); yield break; }
			// (an object from another of Raft's scenes: spawning waits for that scene to load - the moment a shift can come)
			f.Objects.Add(ScObj(PlaceableCatalog.RaftCrate, ScDry(f, new Vector2(0, 0), 1)));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3 shift = new Vector3(240f, 0f, -160f);

			// (a) with a world entry (a plan rule's island): the shift during the spawn
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario shift: no open sea near the raft"); yield break; }
			// (where it should land: the spot moved by the shifts made meanwhile - not by the raft, which drifts while it loads)
			Vector3 shifted0 = IslandWorldState.ShiftedBy;
			IslandWorldState.Entry entry = IslandWorldState.Add(isl, spot.Value, null, false);
			entry.Loading = true;
			made.Add(entry);
			Coroutine c = StartTest(DynamicIslands.instance.SpawnIslandFile(isl, spot.Value, true, entry));
			wsm.ResetToCenter(shift);
			yield return c;
			yield return WaitFor(() => entry.Root != null || entry.Failed, 20f);
			Vector3 want = spot.Value - (IslandWorldState.ShiftedBy - shifted0);
			float off = entry.Root != null ? ScFlat(ScLandCentre(entry), want) : 999f;
			Check(ref ok, entry.Root != null && off < 3f, "a world shift while a plan rule's island is coming: it lands where it should (" + off.ToString("F1") + " m off)");
			ScRemove(made);

			// (a2) without a world entry (SpawnIsland, the editor's Test): the shift during the spawn - AU58
			spot = ScSpot(isl, 400f);
			if (spot.HasValue)
			{
				shifted0 = IslandWorldState.ShiftedBy;
				int before = IslandWorldState.Islands.Count;
				c = StartTest(DynamicIslands.instance.SpawnIslandFile(isl, spot.Value, true));
				wsm.ResetToCenter(shift);
				yield return c;
				IslandWorldState.Entry e2 = IslandWorldState.Islands.Skip(before).FirstOrDefault(x => x.HostName == isl);
				want = spot.Value - (IslandWorldState.ShiftedBy - shifted0);
				off = e2 != null && e2.Root != null ? ScFlat(ScLandCentre(e2), want) : 999f;
				Check(ref ok, e2 != null && off < 3f, "a world shift while SpawnIsland's island is coming: it lands where it should (" + off.ToString("F1") + " m off) - AU58");
				if (e2 != null) made.Add(e2);
				ScRemove(made);
			}

			// (b) the player standing on an island when the world shifts
			spot = ScSpot(isl, 400f);
			if (spot.HasValue)
			{
				yield return ScBring(isl, spot.Value, made);
				IslandWorldState.Entry e = made.LastOrDefault();
				if (e != null && e.Root != null)
				{
					yield return StandRoutine(e.Root);
					Network_Player p = RAPI.GetLocalPlayer();
					Vector3 rel = p.transform.position - ScLandCentre(e);
					wsm.ResetToCenter(shift);
					for (int i = 0; i < 6; i++) { yield return new WaitForSeconds(0.5f); KeepAlive(p); }
					Vector3 rel2 = p.transform.position - ScLandCentre(e);
					Check(ref ok, (rel2 - rel).magnitude < 3f && p.PersonController.IsGrounded, "a world shift with the player standing on an island: they stay where they stood (moved " + (rel2 - rel).magnitude.ToString("F1") + " m relative to it, grounded " + p.PersonController.IsGrounded + ")");
					// (c) the island unloaded, the world shifts, it loads again
					Vector3 was = e.Position;
					shifted0 = IslandWorldState.ShiftedBy;
					OnRaftCommand();
					IslandObjectState.Capture(e);
					IslandSpawner.Despawn(e.Root);
					e.Root = null;
					yield return null;
					wsm.ResetToCenter(shift);
					yield return null;
					e.Loading = true;
					yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
					want = was - (IslandWorldState.ShiftedBy - shifted0);
					off = e.Root != null ? ScFlat(ScLandCentre(e), want) : 999f;
					Check(ref ok, e.Root != null && off < 3f, "an island unloaded during a world shift comes back where it was (" + off.ToString("F1") + " m off)");
				}
				else Check(ref ok, false, "the island came");
			}
			ScRemove(made, isl);
			OnRaftCommand();
			if (ok) Log("PASS: scenario shift"); else Fail("scenario shift");
		}

		#endregion

		#region SC8 - custom islands and Raft's islands side by side

		[ConsoleCommand(name: "CIScOverlap", docs: "Dev, world (host, 'CI ...'): SC8 - 6 km sailed with an island forced every 500 m (as the automatic spawner brings them): no custom island's land overlaps another's or one of Raft's islands (AU68: generated islands are placed by an estimated size)")]
		public static void ScOverlapCommand() { StartTest(ScOverlapRoutine()); }

		static IEnumerator ScOverlapRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario overlap: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Rigidbody body = raft.body;
			Network_Player player = RAPI.GetLocalPlayer();
			int before = IslandWorldState.Islands.Count;
			Vector3 dir = Flat(Raft.direction).sqrMagnitude > 0.01f ? Flat(Raft.direction).normalized : Vector3.forward;
			float sailed = 0f, next = 500f;
			Vector3 last = body.position;
			var overlaps = new List<string>();
			while (sailed < 6000f)
			{
				yield return new WaitForFixedUpdate();
				KeepAlive(player);
				body.MovePosition(body.position + dir * 25f * Time.fixedDeltaTime);
				Vector3 d = Flat(body.position - last);
				if (d.magnitude < 100f) sailed += d.magnitude;
				last = body.position;
				if (sailed >= next)
				{
					next += 500f;
					CustomIslandSpawner.TrySpawn(body.position, true);
					for (int i = 0; i < 30; i++) yield return null;
				}
			}
			body.velocity = Vector3.zero;
			yield return new WaitForSeconds(5f);
			List<IslandWorldState.Entry> mine = IslandWorldState.Islands.Skip(before).ToList();
			ChunkManager cm = ComponentManager<ChunkManager>.Value;
			foreach (IslandWorldState.Entry a in mine)
			{
				float ra = CustomIslandSpawner.LandRadius(a.Name);
				if (ra < 0f) continue;
				foreach (IslandWorldState.Entry b in IslandWorldState.Islands.Where(x => x != a && x.Id < a.Id))
				{
					float rb = CustomIslandSpawner.LandRadius(b.Name);
					if (rb >= 0f && ScFlat(a.Position, b.Position) < ra + rb) overlaps.Add(a.HostName + " and " + b.HostName + " (" + ScFlat(a.Position, b.Position).ToString("F0") + " m apart, land " + ra.ToString("F0") + " + " + rb.ToString("F0") + ")");
				}
				if (cm != null)
					foreach (ChunkPoint cp in cm.GetAllChunkPointsList())
					{
						if (cp.rule == null || cp.rule.name.IndexOf("FloatingRaft", StringComparison.OrdinalIgnoreCase) >= 0) continue;
						float dist = ScFlat(a.Position, cp.worldPosition);
						if (dist < ra + cp.rule.collisionOverlapRadius * 0.5f) overlaps.Add(a.HostName + " and Raft's " + cp.rule.name + " (" + dist.ToString("F0") + " m apart, land " + ra.ToString("F0") + ")");
					}
			}
			Check(ref ok, mine.Count >= 6, "6 km sailed: " + mine.Count + " islands came");
			Check(ref ok, overlaps.Count == 0, "no custom island's land overlaps another island" + (overlaps.Count > 0 ? ": " + string.Join("; ", overlaps.Take(5).ToArray()) : "") + " - AU68");
			IslandWorldState.RemoveIds(mine.Select(x => x.Id).ToList(), true);
			OnRaftCommand();
			if (ok) Log("PASS: scenario overlap"); else Fail("scenario overlap");
		}

		#endregion

		#region SC27 - monsters, difficulty and EXP agree

		[ConsoleCommand(name: "CIScMonsterLists", docs: "Dev, anywhere: SC27 - every animal that gives EXP is scaled by the monster difficulty and the other way round (AU69)")]
		public static void ScMonsterListsCommand()
		{
			bool ok = true;
			var differ = new List<string>();
			foreach (AI_NetworkBehaviourType t in Enum.GetValues(typeof(AI_NetworkBehaviourType)))
			{
				string n = t.ToString();
				if (n.StartsWith("NPC_") || n == "None" || n == "TEST") continue;
				bool exp = LevelRules.IsMonster(t), scaled = MonsterDifficulty.IsMonsterType(t);
				if (exp != scaled) differ.Add(n + (exp ? " gives EXP but isn't scaled by the difficulty" : " is scaled by the difficulty but gives no EXP"));
			}
			Check(ref ok, differ.Count == 0, "the level up system's monsters and the monster difficulty's are the same animals" + (differ.Count > 0 ? ": " + string.Join("; ", differ.ToArray()) : "") + " - AU69");
			if (ok) Log("PASS: scenario monster lists"); else Fail("scenario monster lists");
		}

		#endregion

		#region SC4, SC5 - standing on an island while the raft is away; the unload distance

		[ConsoleCommand(name: "CIScStayOnIsland", docs: "Dev, world (host, 'CI ...'): SC4 - the player on an island while the raft is moved 1.2 km off keeps the island; the player at the edge of a big island 330 m from its middle with unload 300 (AU56)")]
		public static void ScStayOnIslandCommand() { StartTest(ScStayOnIslandRoutine()); }

		static IEnumerator ScStayOnIslandRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario stay on island: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscstay", big = "ciscbig";
			float unloadBefore = CustomIslandSpawner.UnloadDistance;
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Network_Player player = RAPI.GetLocalPlayer();
			try { ScIsland(isl, "Stay Isle").Save(IslandSpawner.PathFor(isl)); } catch (Exception ex) { Fail("scenario stay on island: " + ex.Message); yield break; }
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario stay on island: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root != null)
			{
				yield return StandRoutine(e.Root);
				// The raft goes 1.2 km the other way (moved, as the streamer only looks at where it is)
				Vector3 away = Flat(raft.body.position - e.Position).normalized;
				raft.body.position = raft.body.position + away * 1200f;
				raft.body.velocity = Vector3.zero;
				for (int i = 0; i < 8; i++) { yield return new WaitForSeconds(0.5f); KeepAlive(player); }
				Check(ref ok, e.Root != null, "the raft 1.2 km away: the island stays loaded while the player stands on it (" + ScFlat(raft.body.position, e.Position).ToString("F0") + " m from the raft)");
				Check(ref ok, player.transform.position.y > e.Position.y - 2f && player.PersonController.IsGrounded, "... and the player is still standing on it (y " + player.transform.position.y.ToString("F1") + ")");
				OnRaftCommand();
				for (int i = 0; i < 10 && e.Root != null; i++) yield return new WaitForSeconds(0.5f);
				Check(ref ok, e.Root == null, "back on the raft, 1.2 km away: the island unloads");
			}
			else Check(ref ok, false, "the island came");
			ScRemove(made, isl);

			// A big island (stretched: its land reaches about 390 m from its middle), the unload distance at its lowest (300),
			// the player at the edge 330 m from the middle
			var s = new IslandGenSettings { Seed = 4407, Radius = 280f, Height = 20f, Stretch = 2f, StretchAngle = 0f, ObjectDensity = 0.1f };
			IslandFile bf = IslandGenerator.CreateFile(s, big);
			bf.Props[IslandProps.Title] = "Big Edge";
			bf.Save(IslandSpawner.PathFor(big));
			Vector3? bspot = ScSpot(big, 900f);
			if (!bspot.HasValue) { Check(ref ok, false, "open sea for a big island"); ScRemove(made, big); }
			else
			{
				yield return ScBring(big, bspot.Value, made);
				IslandWorldState.Entry b = made.LastOrDefault();
				if (b != null && b.Root != null)
				{
					// the land 330 m from the middle along the stretch (north, +z, or the other way)
					Terrain terrain = b.Root.GetComponentInChildren<Terrain>();
					Vector3 edge = Vector3.zero; bool found = false;
					foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
					{
						Vector3 p = b.Position + d * 330f;
						float h = terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : -99f;
						if (h > b.Position.y + 1f) { edge = new Vector3(p.x, h + 1.5f, p.z); found = true; break; }
					}
					if (!found) Log("  (no land 330 m from the big island's middle: its land reaches " + CustomIslandSpawner.LandRadius(big).ToString("F0") + " m - part skipped)");
					else
					{
						CustomIslandSpawner.UnloadDistance = 300f;
						PlayerMove.To(player, edge);
						// the raft far away, so only the player keeps the island
						raft.body.position = b.Position + Flat(raft.body.position - b.Position).normalized * 1500f;
						raft.body.velocity = Vector3.zero;
						float y0 = edge.y;
						for (int i = 0; i < 10; i++) { yield return new WaitForSeconds(0.5f); KeepAlive(player); }
						Check(ref ok, b.Root != null && player.transform.position.y > y0 - 4f, "unload distance 300, the player at the big island's edge 330 m from its middle: the island stays under them (" +
							(b.Root != null ? "loaded" : "UNLOADED") + ", player y " + player.transform.position.y.ToString("F1") + " from " + y0.ToString("F1") + ") - AU56");
						CustomIslandSpawner.UnloadDistance = unloadBefore;
					}
				}
				else Check(ref ok, false, "the big island came");
				ScRemove(made, big);
			}
			CustomIslandSpawner.UnloadDistance = unloadBefore;
			// (the raft back where islands are spawned from: put the player on it)
			OnRaftCommand();
			if (ok) Log("PASS: scenario stay on island"); else Fail("scenario stay on island");
		}

		[ConsoleCommand(name: "CIScStreamLoop", docs: "Dev, world (host, 'CI ...'): SC5 - the unload distance at its lowest (300) and an island 350 m off: it may load or stay away, but not load and unload every few seconds (AU56)")]
		public static void ScStreamLoopCommand() { StartTest(ScStreamLoopRoutine()); }

		static IEnumerator ScStreamLoopRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario stream loop: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscloop";
			float unloadBefore = CustomIslandSpawner.UnloadDistance;
			try { ScIsland(isl, "Loop Isle").Save(IslandSpawner.PathFor(isl)); } catch (Exception ex) { Fail("scenario stream loop: " + ex.Message); yield break; }
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			OnRaftCommand();
			// 350 m from the raft, in the first direction without another island near
			Vector3 at = Vector3.zero; bool found = false;
			for (int a = 0; a < 12 && !found; a++)
			{
				Vector3 d = Quaternion.Euler(0, a * 30f, 0) * Vector3.forward;
				Vector3 p = raft.body.position + d * 350f;
				p.y = 0f;
				if (IslandWorldState.Islands.All(x => ScFlat(x.Position, p) > 900f)) { at = p; found = true; }
			}
			if (!found) { ScRemove(made, isl); Fail("scenario stream loop: no room 350 m from the raft"); yield break; }
			yield return ScBring(isl, at, made);
			IslandWorldState.Entry e = made[0];
			CustomIslandSpawner.UnloadDistance = 300f;
			int changes = 0;
			bool was = e.Root != null;
			for (float t = 0; t < 20f; t += 0.25f)
			{
				yield return new WaitForSeconds(0.25f);
				raft.body.velocity = Vector3.zero;
				bool now = e.Root != null;
				if (now != was) changes++;
				was = now;
			}
			CustomIslandSpawner.UnloadDistance = unloadBefore;
			Check(ref ok, changes <= 1, "unload distance 300, an island 350 m off: it loads or unloads at most once in 20 s (" + changes + " changes) - AU56");
			// Two loads of one island in the same moment (the streaming and a reload): one copy. The second once stayed in the
			// world for good, unknown to the world's list - a later test's raft ran aground on it
			if (e.Root != null) { IslandObjectState.Capture(e); IslandSpawner.Despawn(e.Root); e.Root = null; }
			e.Loading = true;
			Coroutine first = StartTest(DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e));
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			yield return first;
			yield return new WaitForSeconds(0.5f);
			string rootName = e.Root != null ? e.Root.name : "?";
			int copies = IslandSpawner.SpawnedRoots.Count(r => r != null && r.name == rootName);
			Check(ref ok, e.Root != null && copies == 1, "the island loaded twice in the same moment: one copy in the world (" + copies + ")");
			ScRemove(made, isl);
			Check(ref ok, !IslandSpawner.SpawnedRoots.Any(r => r != null && r.name == rootName), "... and none left once it is removed");
			if (ok) Log("PASS: scenario stream loop"); else Fail("scenario stream loop");
		}

		#endregion

		#region SC14, SC16 - a full inventory; SC15 - keys and costs

		[ConsoleCommand(name: "CIScFullInventory", docs: "Dev, world (host, 'CI ...'): SC14/SC16 - every slot full; a chest, a zone, a quest reward, a give action and a note give items: what doesn't fit lies in front of the player, story items go to the journal, nothing is lost; an item Raft doesn't have is skipped")]
		public static void ScFullInventoryCommand() { StartTest(ScFullInventoryRoutine()); }

		static IEnumerator ScFullInventoryRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario full inventory: run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario full inventory: only in a test world 'CI ...' (it empties the inventory)"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscfull";
			IslandFile f;
			try { f = ScIsland(isl, "Full Pockets"); } catch (Exception ex) { Fail("scenario full inventory: " + ex.Message); yield break; }
			f.Props[StoryItems.Key] = "ciscgem|Sea gem||A gem from the test";
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(0, 0), 1), ObjectProps.NoteTitle, "Full chest", ObjectProps.LootItems, "Plank*5;TitaniumIngot*1;NoSuchItemCI*1;story:ciscgem*1", ObjectProps.LootRefill, "0"));
			f.Objects.Add(ScObj(ContentCatalog.TriggerZone, ScDry(f, new Vector2(12, 0), 2), ObjectProps.ZoneId, "gift", ObjectProps.ZoneRadius, "4", ObjectProps.LootItems, "Rope*3"));
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(-12, 0), 3), ObjectProps.NoteTitle, "Gem note", ObjectProps.NoteText, "A gem for the finder.", BehaviourProps.EventPrefix + "read", "give||story:ciscgem*1"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(0, 12), 4), BehaviourProps.Name, "giver", BehaviourProps.Use, "Take", BehaviourProps.EventPrefix + "use", "give||Scrap*2"));
			bool blueprint = ItemManager.GetItemByName("Blueprint_Firework") != null;
			new IslandQuest { Title = "Full pockets", Steps = { new IslandQuest.Step { Type = "reach", Target = "gift", Count = 1 } }, Reward = "Nail*4" + (blueprint ? ";Blueprint_Firework*1" : "") }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario full inventory: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario full inventory: the island didn't come"); yield break; }

			Network_Player player = RAPI.GetLocalPlayer();
			PlayerInventory inv = player.Inventory;
			ClearInventory();
			// Every slot full: stones a stack at a time until nothing more goes in
			for (int i = 0; i < 200; i++)
			{
				int had = inv.GetItemCount("Stone");
				inv.AddItem("Stone", 20);
				if (inv.GetItemCount("Stone") == had) break;
			}
			int planksBefore = inv.GetItemCount("Plank");
			inv.AddItem("Plank", 1);
			bool full = inv.GetItemCount("Plank") == planksBefore;
			Check(ref ok, full, "every slot of the inventory is full (a plank doesn't fit)");
			int gemsBefore = StoryBook.Count("ciscgem");
			// (what lay on the ground before: only items dropped by this test count)
			var droppedBefore = new HashSet<int>(UnityEngine.Object.FindObjectsOfType<PickupItem>().Where(p => p != null && p.isDropped).Select(p => p.GetInstanceID()));
			var warnings = new List<string>();
			Application.LogCallback watch = (text, trace, type) => { if (text.Contains("NoSuchItemCI")) warnings.Add(text); };
			Application.logMessageReceived += watch;
			try
			{
				List<string> got = ScOpenChest(e, "Full chest");
				yield return new WaitForSeconds(1f);
				LootCrate fullChest = ScChest(e, "Full chest");
				Check(ref ok, got != null && fullChest != null && fullChest.Looted, "the chest opens and counts as opened (" + (got != null ? string.Join(", ", got.ToArray()) : "nothing") + ")");
				ScEnterZone(e, "gift");
				yield return new WaitForSeconds(2f);
				ScUse(e, "giver");
				yield return new WaitForSeconds(1f);
				ScReadNote(e, "Gem note");
				yield return new WaitForSeconds(1f);
			}
			finally { Application.logMessageReceived -= watch; }
			// What lies in front of the player: dropped pickups near them
			yield return new WaitForSeconds(1f);
			Func<string, int> dropped = n => UnityEngine.Object.FindObjectsOfType<PickupItem>().Where(p => p != null && p.isDropped && !droppedBefore.Contains(p.GetInstanceID()) && p.itemInstance != null && p.itemInstance.UniqueName == n).Sum(p => Mathf.Max(1, p.itemInstance.Amount));
			foreach (PickupItem p in UnityEngine.Object.FindObjectsOfType<PickupItem>().Where(p => p != null && p.isDropped && !droppedBefore.Contains(p.GetInstanceID())).Take(8))
				Log("  dropped: " + (p.itemInstance != null ? p.itemInstance.UniqueName + " x" + p.itemInstance.Amount : "?") + ", " + (p.transform.position - player.transform.position).magnitude.ToString("F0") + " m from the player, y " + p.transform.position.y.ToString("F1"));
			int planks = dropped("Plank"), ropes = dropped("Rope"), nails = dropped("Nail"), scrap = dropped("Scrap"), ingots = dropped("TitaniumIngot");
			Check(ref ok, planks >= 5 && ingots >= 1, "the chest's items lie in front of the player (planks " + planks + "/5, titanium " + ingots + "/1)");
			Check(ref ok, ropes >= 3, "the zone's items too (ropes " + ropes + "/3)");
			Check(ref ok, nails >= 4, "the quest reward too (nails " + nails + "/4" + (blueprint ? ", and the blueprint " + dropped("Blueprint_Firework") + "/1" : "") + ")");
			Check(ref ok, scrap >= 2, "the give action's items too (scrap " + scrap + "/2)");
			Check(ref ok, StoryBook.Count("ciscgem") - gemsBefore == 2, "the story items go to the journal, not the full inventory (" + (StoryBook.Count("ciscgem") - gemsBefore) + "/2 gems)");
			Check(ref ok, warnings.Count >= 1, "an item Raft doesn't have is skipped with a line in the log (" + warnings.Count + ")");
			// Picked up again when there is room
			ClearInventory();
			PickupItem plank = UnityEngine.Object.FindObjectsOfType<PickupItem>().FirstOrDefault(p => p != null && p.isDropped && p.itemInstance != null && p.itemInstance.UniqueName == "Plank");
			if (plank != null)
			{
				Pickup pickup = player.GetComponentInChildren<Pickup>(true);
				PutPlayerNear(plank.transform);
				int before = inv.GetItemCount("Plank");
				if (pickup != null) pickup.PickupItemByType(plank, true);
				yield return new WaitForSeconds(1f);
				Check(ref ok, inv.GetItemCount("Plank") > before, "a dropped plank can be picked up again");
			}
			// Tidy: the dropped things and the test's gems
			foreach (PickupItem p in UnityEngine.Object.FindObjectsOfType<PickupItem>().Where(p => p != null && p.isDropped && (p.transform.position - player.transform.position).magnitude < 30f))
				UnityEngine.Object.Destroy(p.gameObject);
			StoryBook.Take("ciscgem", StoryBook.Count("ciscgem"));
			ClearInventory();
			ScRemove(made, isl);
			OnRaftCommand();
			if (ok) Log("PASS: scenario full inventory"); else Fail("scenario full inventory");
		}

		/// <summary>An island's chest by its title.</summary>
		static LootCrate ScChest(IslandWorldState.Entry e, string title)
		{
			return e == null || e.Root == null ? null : e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault(x =>
			{
				IslandObjectRef r = x.GetComponentInParent<IslandObjectRef>();
				return r != null && ObjectProps.Get(r.Props, ObjectProps.NoteTitle).Equals(title, StringComparison.OrdinalIgnoreCase);
			});
		}

		[ConsoleCommand(name: "CIScKeys", docs: "Dev, world (host, 'CI ...'): SC15 - a door with two take lines of planks (3 + 2) and 3 planks stays shut (AU67); nails in a storage don't count; one story key opens one of two doors")]
		public static void ScKeysCommand() { StartTest(ScKeysRoutine()); }

		static IEnumerator ScKeysRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario keys: run in a world, as the host"); yield break; }
			if (!(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("scenario keys: only in a test world 'CI ...' (it empties the inventory)"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "cisckeys";
			IslandFile f;
			try { f = ScIsland(isl, "Key Isle"); } catch (Exception ex) { Fail("scenario keys: " + ex.Message); yield break; }
			f.Props[StoryItems.Key] = "cisckey|Test key||Opens one door";
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(0, 0), 1), BehaviourProps.Name, "plankdoor", BehaviourProps.Use, "Open",
				BehaviourProps.CheckPrefix + "use", "take|Plank|3\ntake|Plank|2", BehaviourProps.EventPrefix + "use", "message||plankdoor opens", BehaviourProps.ElsePrefix + "use", "message||plankdoor refuses"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(10, 0), 2), BehaviourProps.Name, "naildoor", BehaviourProps.Use, "Open",
				BehaviourProps.CheckPrefix + "use", "has|Nail|5", BehaviourProps.EventPrefix + "use", "message||naildoor opens", BehaviourProps.ElsePrefix + "use", "message||naildoor refuses"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(-10, 0), 3), BehaviourProps.Name, "keydoora", BehaviourProps.Use, "Open",
				BehaviourProps.CheckPrefix + "use", "take|story:cisckey|1", BehaviourProps.EventPrefix + "use", "message||keydoora opens", BehaviourProps.ElsePrefix + "use", "message||keydoora refuses"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(0, -10), 4), BehaviourProps.Name, "keydoorb", BehaviourProps.Use, "Open",
				BehaviourProps.CheckPrefix + "use", "take|story:cisckey|1", BehaviourProps.EventPrefix + "use", "message||keydoorb opens", BehaviourProps.ElsePrefix + "use", "message||keydoorb refuses"));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario keys: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario keys: the island didn't come"); yield break; }
			PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
			ClearInventory();
			inv.AddItem("Plank", 3);
			ScUse(e, "plankdoor");
			yield return new WaitForSeconds(1f);
			Check(ref ok, Behaviours.LastMessage.Contains("plankdoor refuses") && inv.GetItemCount("Plank") == 3,
				"a door taking 3 + 2 planks with 3 planks: stays shut, the planks kept ('" + Behaviours.LastMessage + "', planks " + inv.GetItemCount("Plank") + ") - AU67");
			ClearInventory();
			inv.AddItem("Plank", 5);
			yield return new WaitForSeconds(0.7f); // (the object's cooldown)
			ScUse(e, "plankdoor");
			yield return new WaitForSeconds(1f);
			Check(ref ok, Behaviours.LastMessage.Contains("plankdoor opens") && inv.GetItemCount("Plank") == 0, "with 5 planks it opens and takes all 5 ('" + Behaviours.LastMessage + "', planks " + inv.GetItemCount("Plank") + ")");
			// Nails put away (in a storage, given away, dropped): not in the inventory - they don't count
			ScUse(e, "naildoor");
			yield return new WaitForSeconds(1f);
			Check(ref ok, Behaviours.LastMessage.Contains("naildoor refuses"), "nails that aren't in the inventory don't count ('" + Behaviours.LastMessage + "')");
			// One story key, two doors
			int keys = StoryBook.Count("cisckey");
			StoryBook.Give("story:cisckey", 1 - keys);
			ScUse(e, "keydoora");
			yield return new WaitForSeconds(1f);
			string first = Behaviours.LastMessage;
			ScUse(e, "keydoorb");
			yield return new WaitForSeconds(1f);
			Check(ref ok, first.Contains("keydoora opens") && Behaviours.LastMessage.Contains("keydoorb refuses") && StoryBook.Count("cisckey") == 0,
				"one story key opens one of two doors ('" + first + "', then '" + Behaviours.LastMessage + "', keys left " + StoryBook.Count("cisckey") + ")");
			ClearInventory();
			ScRemove(made, isl);
			OnRaftCommand();
			if (ok) Log("PASS: scenario keys"); else Fail("scenario keys");
		}

		#endregion

		#region SC19 - sea creatures on a flying island

		[ConsoleCommand(name: "CIScFlyingSea", docs: "Dev, world (host, 'CI ...'): SC19 - an island flying at 60 m with turtles and a puffer fish in its lagoon, an underwater zone and chest: what lies under the island's own sea is left out (AU57)")]
		public static void ScFlyingSeaCommand() { StartTest(ScFlyingSeaRoutine()); }

		static IEnumerator ScFlyingSeaRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario flying sea: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscflysea";
			IslandFile f;
			try { f = ScIsland(isl, "Sky Lagoon", 60f); } catch (Exception ex) { Fail("scenario flying sea: " + ex.Message); yield break; }
			Vector3? under = ScUnderSea(f, 4f);
			if (!under.HasValue) { Fail("scenario flying sea: the sample island has no sea floor 4 m under its sea"); yield break; }
			f.Objects.Add(ScObj("Creature_Turtle", under.Value, ObjectProps.CreatureCount, "2"));
			f.Objects.Add(ScObj("Creature_PufferFish", under.Value + new Vector3(4f, 0, 0), ObjectProps.CreatureCount, "1"));
			f.Objects.Add(ScObj(ContentCatalog.TriggerZone, under.Value + new Vector3(0, 0, 4f), ObjectProps.ZoneId, "seazone", ObjectProps.ZoneRadius, "3"));
			f.Objects.Add(ScObj("Loot_SunkenBarrel", under.Value + new Vector3(-4f, 0, 0), ObjectProps.NoteTitle, "Sea barrel", ObjectProps.LootItems, "Plank*1"));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f, 60f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario flying sea: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario flying sea: the island didn't come"); yield break; }
			yield return new WaitForSeconds(6f); // (creatures come after their NavMesh)
			int seaSpots = e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Count(p => p.Kind != null && (p.Kind.Type == AI_NetworkBehaviourType.Turtle || p.Kind.Type == AI_NetworkBehaviourType.PufferFish));
			var animals = UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour>().Where(a => a != null && (a.behaviourType == AI_NetworkBehaviourType.Turtle || a.behaviourType == AI_NetworkBehaviourType.PufferFish) && ScFlat(a.transform.position, e.Position) < 400f).ToList();
			int inAir = animals.Count(a => a.transform.position.y > 2f);
			int zones = e.Root.GetComponentsInChildren<TriggerZone>(true).Count(z => z.Id == "seazone");
			int barrels = e.Root.GetComponentsInChildren<LootCrate>(true).Count();
			Check(ref ok, seaSpots == 0 && inAir == 0, "a flying island's sea creature spots are left out (" + seaSpots + " spots, " + inAir + " of " + animals.Count + " sea animals above the real sea) - AU57");
			Check(ref ok, zones == 0, "... and its underwater zone (" + zones + ")");
			Check(ref ok, barrels == 0, "... and its sunken barrel (" + barrels + " chests)");
			ScRemove(made, isl);
			if (ok) Log("PASS: scenario flying sea"); else Fail("scenario flying sea");
		}

		#endregion

		#region SC20, SC21 - dying on an island mid-quest; a wait after death

		static Vector3 scDeathSpot;
		static int scDeathStep = -1;

		[ConsoleCommand(name: "CIScDeath", docs: "Dev, world (host, 'CI ...'): SC20/SC21 - the player dies on an island mid-quest and respawns on the raft: not pulled back, the quest step kept; a note's 'wait then teleport' after death doesn't take the respawned player (AU59). CIScDeath check = after a save and load: the player is where they respawned")]
		public static void ScDeathCommand(string[] args) { StartTest(args != null && args.Length > 0 && args[0] == "check" ? ScDeathCheckRoutine() : ScDeathRoutine()); }

		static IEnumerator ScDeathRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario death: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscdeath";
			IslandFile f;
			try { f = ScIsland(isl, "Grave Isle"); } catch (Exception ex) { Fail("scenario death: " + ex.Message); yield break; }
			Vector3 camp = ScDry(f, new Vector2(0, 0), 1), ledge = ScDry(f, new Vector2(25, 25), 2);
			f.Objects.Add(ScObj(ContentCatalog.TriggerZone, camp, ObjectProps.ZoneId, "camp", ObjectProps.ZoneRadius, "4"));
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(-12, 0), 3), ObjectProps.NoteTitle, "Trap note", ObjectProps.NoteText, "Wait for it.",
				BehaviourProps.EventPrefix + "read", "wait||8\nteleport|ledge|"));
			f.Objects.Add(ScObj("Note_Sign", ledge, BehaviourProps.Name, "ledge"));
			new IslandQuest { Title = "Grave", Steps = { new IslandQuest.Step { Type = "reach", Target = "camp", Count = 1 }, new IslandQuest.Step { Type = "read", Target = "Last words", Count = 1 } } }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario death: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario death: the island didn't come"); yield break; }
			Network_Player player = RAPI.GetLocalPlayer();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			ScEnterZone(e, "camp");
			yield return new WaitForSeconds(1.5f);
			Check(ref ok, QuestTracker.StepOf(e) == 1, "step 1 of the island's quest done (" + QuestTracker.StepOf(e) + ")");
			ScReadNote(e, "Trap note");
			yield return new WaitForSeconds(0.5f);
			// Death on the island (Raft's own: health to nothing), then Raft's respawn without a bed: on the raft
			Player body = player.GetComponentInChildren<Player>(true);
			scDeathSpot = player.transform.position;
			if (body != null) body.Kill(false);
			for (float t = 0; t < 6f && (body == null || !body.IsDead); t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			Check(ref ok, body != null && body.IsDead, "the player dies on the island");
			yield return new WaitForSecondsRealtime(2f);
			// (as the death menu's Respawn button does: Raft puts the player at a bed or on the raft)
			BedManager beds = ComponentManager<BedManager>.Value ?? UnityEngine.Object.FindObjectOfType<BedManager>() ?? Resources.FindObjectsOfTypeAll<BedManager>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (beds != null) beds.Button_Respawn();
			else if (body != null) body.RespawnWithoutBed(false);
			for (float t = 0; t < 10f && body != null && body.IsDead; t += 0.25f) yield return new WaitForSecondsRealtime(0.25f);
			yield return new WaitForSecondsRealtime(2f);
			Vector3 respawned = player.transform.position;
			Check(ref ok, body != null && !body.IsDead && ScFlat(respawned, raft.body.position) < 20f, "respawned on the raft (" + ScFlat(respawned, raft.body.position).ToString("F0") + " m from it, " + ScFlat(respawned, scDeathSpot).ToString("F0") + " m from where they died)");
			// The note's wait (8 s from reading) ends now: the respawned player must not be pulled back to the ledge
			IslandObjectRef ledgeRef = ScObjOf(e, "ledge");
			yield return new WaitForSecondsRealtime(9f);
			float toLedge = ledgeRef != null ? ScFlat(player.transform.position, ledgeRef.transform.position) : 999f;
			Check(ref ok, toLedge > 30f, "the note's 'wait then teleport' doesn't take the respawned player back (" + toLedge.ToString("F0") + " m from the ledge) - AU59");
			Check(ref ok, QuestTracker.StepOf(e) == 1, "the quest is still at step 2 after dying (" + QuestTracker.StepOf(e) + " done)");
			scDeathStep = QuestTracker.StepOf(e);
			OnRaftCommand();
			// (the island stays for the check after loading)
			if (ok) Log("PASS: scenario death"); else Fail("scenario death");
		}

		static IEnumerator ScDeathCheckRoutine()
		{
			yield return new WaitForSeconds(3f);
			bool ok = true;
			Network_Player player = RAPI.GetLocalPlayer();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == "ciscdeath");
			Check(ref ok, player != null && raft != null && ScFlat(player.transform.position, raft.body.position) < 20f, "after loading, the player is on the raft where they respawned, not where they died (" +
				(player != null && raft != null ? ScFlat(player.transform.position, raft.body.position).ToString("F0") : "?") + " m from the raft)");
			Check(ref ok, e != null && QuestTracker.StepOf(e) == (scDeathStep < 0 ? 1 : scDeathStep), "the island's quest kept its step after loading (" + (e != null ? QuestTracker.StepOf(e).ToString() : "no island") + ")");
			if (e != null) ScRemove(new List<IslandWorldState.Entry> { e }, "ciscdeath");
			if (ok) Log("PASS: scenario death check"); else Fail("scenario death check");
		}

		#endregion

		#region SC22 - kill quests in every game mode

		[ConsoleCommand(name: "CIScModes", docs: "Dev, world (host, 'CI ...'): SC22 - in Peaceful, Easy, Normal, Hard and Creative: a kill step with spear-strength hits, the EXP they give, and whether screechers and puffer fish come (AU61)")]
		public static void ScModesCommand() { StartTest(ScModesRoutine()); }

		static IEnumerator ScModesRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario modes: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			GameMode modeBefore = GameModeValueManager.GetCurrentGameModeValue().gameMode;
			bool levelsBefore = PlayerLevels.On;
			try
			{
				foreach (GameMode mode in new[] { GameMode.Peaceful, GameMode.Easy, GameMode.Normal, GameMode.Hardcore, GameMode.Creative })
				{
					string isl = "ciscmode" + mode.ToString().ToLowerInvariant();
					IslandFile f = ScIsland(isl, "Mode " + mode);
					PlayerLevels.SetEnabled(true); // (a world option: islands don't turn it on)
					f.Objects.Add(ScObj("Creature_Boar", ScDry(f, new Vector2(0, 0), 1), ObjectProps.CreatureCount, "2", ObjectProps.CreatureRespawn, "0"));
					f.Objects.Add(ScObj("Creature_StoneBird", ScDry(f, new Vector2(15, 0), 2), ObjectProps.CreatureCount, "1"));
					Vector3? sea = ScUnderSea(f, 3f);
					if (sea.HasValue) f.Objects.Add(ScObj("Creature_PufferFish", sea.Value, ObjectProps.CreatureCount, "1"));
					new IslandQuest { Title = "Hunt", Steps = { new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 2 } } }.To(f.Props);
					f.Save(IslandSpawner.PathFor(isl));
					GameModeValueManager.SelectCurrentGameMode(mode);
					Vector3? spot = ScSpot(isl, 320f);
					if (!spot.HasValue) { Check(ref ok, false, mode + ": open sea near the raft"); ScRemove(made, isl); continue; }
					yield return ScBring(isl, spot.Value, made);
					IslandWorldState.Entry e = made.LastOrDefault();
					if (e == null || e.Root == null) { Check(ref ok, false, mode + ": the island came"); ScRemove(made, isl); continue; }
					yield return ScWaitAnimals(e, "Warthog", 2, 20f);
					yield return new WaitForSeconds(2f);
					int birds = ScAnimals(e, "Screecher").Count, puffers = ScAnimals(e, "Puffer fish").Count;
					LevelRecord rec = PlayerLevels.Mine;
					int xp0 = rec != null ? rec.Xp : 0;
					// spear-strength hits (Raft's DamageEntity, 25 a hit) until both warthogs are down, at most 40 hits each
					foreach (AI_NetworkBehaviour a in ScAnimals(e, "Warthog"))
					{
						PutPlayerNear(a.transform);
						for (int i = 0; i < 40 && a != null && a.networkEntity != null && !a.networkEntity.IsDead; i++) { ScKill(a, 25f); yield return new WaitForSeconds(0.1f); }
					}
					yield return new WaitForSeconds(2.5f); // (kills are counted once a second)
					rec = PlayerLevels.Mine;
					int xp = (rec != null ? rec.Xp : 0) - xp0;
					int left = ScAnimals(e, "Warthog").Count;
					Log("  " + mode + ": warthogs left " + left + ", quest step " + QuestTracker.StepOf(e) + ", EXP " + xp + ", screechers " + birds + ", puffer fish " + puffers + (sea.HasValue ? "" : " (no sea spot)"));
					Check(ref ok, left == 0 && QuestTracker.StepOf(e) >= 1, mode + ": the warthogs can be killed with a spear's hits and the kill step counts (" + left + " left, step " + QuestTracker.StepOf(e) + ") - AU61");
					Check(ref ok, mode == GameMode.Peaceful || !PlayerLevels.On || xp > 0, mode + ": the kills give EXP (" + xp + ")");
					if (mode == GameMode.Creative) Check(ref ok, birds == 0, "Creative: no screechers come (as on Raft's islands) - so a screecher step there can never finish: Check must say so (AU55)");
					else Check(ref ok, birds >= 1, mode + ": the screecher comes (" + birds + ")");
					ScRemove(made, isl);
				}
			}
			finally
			{
				GameModeValueManager.SelectCurrentGameMode(modeBefore);
				if (!levelsBefore && PlayerLevels.On) PlayerLevels.TurnOff();
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario modes"); else Fail("scenario modes");
		}

		#endregion

		#region SC23, SC28 - catching; SC24 - a kill just before unloading

		[ConsoleCommand(name: "CIScCatch", docs: "Dev, world (host, 'CI ...'): SC23/SC28 - two chickens: one killed, one netted - the catch step counts and 'defeat' fires (AU60); a goat and a llama carried to the raft stay there when the island unloads. CIScCatch check = after a save and load: they're still on the raft")]
		public static void ScCatchCommand(string[] args) { StartTest(args != null && args.Length > 0 && args[0] == "check" ? ScCatchCheckRoutine() : ScCatchRoutine()); }

		static IEnumerator ScCatchRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario catch: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "cisccatch";
			IslandFile f;
			try { f = ScIsland(isl, "Farm Isle"); } catch (Exception ex) { Fail("scenario catch: " + ex.Message); yield break; }
			f.Objects.Add(ScObj("Creature_Chicken", ScDry(f, new Vector2(0, 0), 1), ObjectProps.CreatureCount, "2", ObjectProps.CreatureRespawn, "0", BehaviourProps.EventPrefix + "defeat", "message||the chickens are gone"));
			f.Objects.Add(ScObj("Creature_Goat", ScDry(f, new Vector2(20, 0), 2), ObjectProps.CreatureCount, "1", ObjectProps.CreatureRespawn, "0"));
			f.Objects.Add(ScObj("Creature_Llama", ScDry(f, new Vector2(-20, 0), 3), ObjectProps.CreatureCount, "1", ObjectProps.CreatureRespawn, "0"));
			new IslandQuest { Title = "Farm", Steps = { new IslandQuest.Step { Type = "catch", Target = "Chicken", Count = 1 } } }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario catch: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario catch: the island didn't come"); yield break; }
			yield return ScWaitAnimals(e, "Chicken", 2, 20f);
			yield return ScWaitAnimals(e, "Goat", 1, 10f);
			List<AI_NetworkBehaviour> chickens = ScAnimals(e, "Chicken");
			if (chickens.Count < 2) { ScRemove(made, isl); Fail("scenario catch: the two chickens didn't come (" + chickens.Count + ")"); yield break; }
			ScKill(chickens[0]);
			yield return new WaitForSeconds(2f);
			string messageBefore = Behaviours.LastMessage;
			yield return ScCarryHome(chickens[1] as AI_NetworkBehaviour_Domestic, false);
			yield return new WaitForSeconds(2.5f);
			Check(ref ok, QuestTracker.StepOf(e) >= 1, "the netted chicken counts for the catch step (" + QuestTracker.StepOf(e) + ")");
			Check(ref ok, Behaviours.LastMessage != messageBefore && Behaviours.LastMessage.Contains("chickens are gone"), "the spot's 'defeat' fires when its last chicken is netted, not killed ('" + Behaviours.LastMessage + "') - AU60");
			// A goat and a llama carried home to the raft
			foreach (string kind in new[] { "Goat", "Llama" })
			{
				var a = ScAnimals(e, kind).OfType<AI_NetworkBehaviour_Domestic>().FirstOrDefault();
				if (a == null) { Check(ref ok, false, "a " + kind + " to catch"); continue; }
				yield return ScCarryHome(a, true);
			}
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Func<int> home = () => UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour_Domestic>().Count(a => a != null && a.connectedSpawner == null && ScFlat(a.transform.position, raft.body.position) < 25f);
			int onRaft = home();
			Check(ref ok, onRaft >= 2, "the goat and the llama are on the raft (" + onRaft + " caught animals there)");
			// The island unloads (sailing away): the caught ones stay
			IslandObjectState.Capture(e);
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return new WaitForSeconds(2f);
			Check(ref ok, home() >= 2, "the island unloads: the caught animals stay on the raft (" + home() + ")");
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			yield return new WaitForSeconds(5f);
			// (wild ones only - the caught goat and llama on the raft nearby are Raft's own now, without a spawner)
			int back = new[] { "Goat", "Llama", "Chicken" }.Sum(k => ScAnimals(e, k).OfType<AI_NetworkBehaviour_Domestic>().Count(a => a.connectedSpawner != null));
			Check(ref ok, back == 0, "loaded again: none of the caught or killed animals is back on the island before the regrow days (" + back + ")");
			// (the island stays for the check after loading; the caught animals are Raft's own now)
			OnRaftCommand();
			if (ok) Log("PASS: scenario catch"); else Fail("scenario catch");
		}

		/// <summary>Nets an animal and carries it: to the raft (home) or puts it down where it is.</summary>
		static IEnumerator ScCarryHome(AI_NetworkBehaviour_Domestic animal, bool home)
		{
			if (animal == null) yield break;
			Network_Player player = RAPI.GetLocalPlayer();
			PutPlayerNear(animal.transform);
			yield return new WaitForSeconds(0.5f);
			if (animal.captureScript == null || !animal.captureScript.IsCapturable) { Log("  (the " + animal.behaviourType + " can't be caught)"); yield break; }
			animal.captureScript.IsCaptured = true;
			yield return new WaitForSeconds(2f);
			if (animal == null) yield break;
			PutPlayerNear(animal.transform);
			if (animal.carryScript != null && animal.carryScript.OnStartCarry != null) animal.carryScript.OnStartCarry(player);
			for (float t = 0; animal != null && animal.carryScript != null && !animal.carryScript.IsBeingCarried && t < 10f; t += 0.25f) yield return new WaitForSeconds(0.25f);
			if (home) { OnRaftCommand(); yield return new WaitForSeconds(1.5f); }
			else yield return new WaitForSeconds(1f);
			if (animal != null && animal.carryScript != null && animal.carryScript.OnStopCarry != null) animal.carryScript.OnStopCarry(player, false);
			for (float t = 0; animal != null && animal.carryScript != null && animal.carryScript.IsBeingCarried && t < 10f; t += 0.25f) yield return new WaitForSeconds(0.25f);
			yield return new WaitForSeconds(1f);
			Log("  caught a " + (animal != null ? animal.behaviourType + ", spawner " + (animal.connectedSpawner != null ? "still" : "none") : "?"));
		}

		static IEnumerator ScCatchCheckRoutine()
		{
			yield return new WaitForSeconds(5f);
			bool ok = true;
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			int onRaft = raft == null ? 0 : UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour_Domestic>().Count(a => a != null && (a.behaviourType == AI_NetworkBehaviourType.Goat || a.behaviourType == AI_NetworkBehaviourType.Llama) && ScFlat(a.transform.position, raft.body.position) < 30f);
			Check(ref ok, onRaft >= 2, "after loading, the goat and the llama are still on the raft (" + onRaft + ")");
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == "cisccatch");
			if (e != null) ScRemove(new List<IslandWorldState.Entry> { e }, "cisccatch");
			// (the test's animals off the raft again)
			foreach (AI_NetworkBehaviour_Domestic a in UnityEngine.Object.FindObjectsOfType<AI_NetworkBehaviour_Domestic>().Where(a => a != null && raft != null && ScFlat(a.transform.position, raft.body.position) < 30f))
				ScKill(a);
			if (ok) Log("PASS: scenario catch check"); else Fail("scenario catch check");
		}

		[ConsoleCommand(name: "CIScCollectFar", docs: "Dev, world (host, 'CI ...'): AT25 - an island far away whose quest collects story items the crew already holds: it isn't done while nobody is there; a player arriving finishes it and gets its reward (AU22)")]
		public static void ScCollectFarCommand() { StartTest(ScCollectFarRoutine()); }

		static IEnumerator ScCollectFarRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario collect far: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "cisccollect";
			IslandFile f;
			try { f = ScIsland(isl, "Map Rock"); } catch (Exception ex) { Fail("scenario collect far: " + ex.Message); yield break; }
			f.Props[StoryItems.Key] = "ciscpiece|Map piece||A piece of a map";
			new IslandQuest { Title = "The map", Reward = "Plank*5", Steps = { new IslandQuest.Step { Type = "collect", Target = StoryItems.Ref("ciscpiece"), Count = 3 } } }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			// (far from the raft: more than the island and 150 m from the player standing on it)
			Vector3 raft = CustomIslandSpawner.RaftPosition.Value;
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft + CustomIslandSpawner.SailDirection() * 650f, Mathf.Max(20f, CustomIslandSpawner.LandRadius(isl)), 300f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario collect far: no open sea 650 m ahead"); yield break; }
			int held = StoryBook.Count("ciscpiece");
			if (held < 3) StoryBook.Give("ciscpiece", 3 - held);
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("scenario collect far: the island didn't come"); yield break; }
			try
			{
				Network_Player player = RAPI.GetLocalPlayer();
				float away = ScFlat(player.transform.position, e.Position);
				yield return new WaitForSeconds(3f);
				Check(ref ok, QuestTracker.StepOf(e) == 0, "the crew holds the 3 map pieces, the island " + away.ToString("F0") + " m away: its quest waits for someone to be there (step " + QuestTracker.StepOf(e) + ") - AU22");
				PlayerInventory inv = player.Inventory;
				int planks = inv != null ? inv.GetItemCount("Plank") : 0;
				PlayerMove.To(player, ScLandCentre(e) + Vector3.up * 2f);
				for (float t = 0; t < 10f && QuestTracker.StepOf(e) == 0; t += 0.5f) yield return new WaitForSeconds(0.5f);
				yield return new WaitForSeconds(1f);
				Check(ref ok, QuestTracker.StepOf(e) >= 1, "a player arrives: the quest is done (step " + QuestTracker.StepOf(e) + ")");
				Check(ref ok, inv != null && inv.GetItemCount("Plank") - planks == 5, "... and they get its reward (" + (inv != null ? inv.GetItemCount("Plank") - planks : -1) + " of 5 planks)");
			}
			finally
			{
				StoryBook.Take("ciscpiece", 99);
				ScRemove(made, isl);
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario collect far"); else Fail("scenario collect far");
		}

		[ConsoleCommand(name: "CIScComeBack", docs: "Dev, world (host, 'CI ...'): AT24 - after the regrow days and a reload: a chest with a story item stays empty (no second key), a 'Once ever' zone stays used, a plain 'Once' zone and a plain chest are ready again (AU19)")]
		public static void ScComeBackCommand() { StartTest(ScComeBackRoutine()); }

		static IEnumerator ScComeBackRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario come back: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscback";
			IslandFile f;
			try { f = ScIsland(isl, "Back Isle"); } catch (Exception ex) { Fail("scenario come back: " + ex.Message); yield break; }
			f.Props[IslandProps.RegrowDays] = "2";
			f.Props[StoryItems.Key] = "ciscbackkey|Iron key||The key of the gate";
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(-10, 0), 1), ObjectProps.NoteTitle, "Key chest", ObjectProps.LootItems, StoryItems.Ref("ciscbackkey") + "*1"));
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(10, 0), 2), ObjectProps.NoteTitle, "Plain chest", ObjectProps.LootItems, "Plank*1"));
			f.Objects.Add(ScObj(ContentCatalog.TriggerZone, ScDry(f, new Vector2(0, 12), 3), ObjectProps.ZoneId, "ever", ObjectProps.ZoneRadius, "3", ObjectProps.ZoneRepeat, ObjectProps.ZoneOnceEver));
			f.Objects.Add(ScObj(ContentCatalog.TriggerZone, ScDry(f, new Vector2(0, -12), 4), ObjectProps.ZoneId, "plain", ObjectProps.ZoneRadius, "3"));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario come back: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("scenario come back: the island didn't come"); yield break; }
			try
			{
				StoryBook.Take("ciscbackkey", 99);
				ScOpenChest(e, "Key chest");
				ScOpenChest(e, "Plain chest");
				ScEnterZone(e, "ever");
				ScEnterZone(e, "plain");
				yield return new WaitForSeconds(1f);
				LootCrate keyChest = ScChest(e, "Key chest"), plainChest = ScChest(e, "Plain chest");
				Func<string, TriggerZone> zone = id => e.Root != null ? e.Root.GetComponentsInChildren<TriggerZone>(true).FirstOrDefault(z => z.Id == id) : null;
				int keyKey = keyChest != null ? keyChest.StateKey : -1, plainKey = plainChest != null ? plainChest.StateKey : -1;
				int everKey = zone("ever") != null ? zone("ever").StateKey : -1, plainZoneKey = zone("plain") != null ? zone("plain").StateKey : -1;
				Check(ref ok, ContentState.IsUsed(e, keyKey) && ContentState.IsUsed(e, plainKey) && ContentState.IsUsed(e, everKey) && ContentState.IsUsed(e, plainZoneKey) && StoryBook.Count("ciscbackkey") == 1,
					"both chests opened, both zones set off, the crew holds the key (" + StoryBook.Count("ciscbackkey") + ")");
				// The regrow days pass (the state's day moved back), and the island loads again
				foreach (int k in new[] { keyKey, plainKey, everKey, plainZoneKey })
				{
					ObjectState s;
					if (e.State.TryGetValue(k, out s)) e.State[k] = new ObjectState { Active = s.Active, Yield = s.Yield, Day = s.Day - 10 };
				}
				// (back on the raft first: still standing in the plain zone, the player set it off again as soon as it loaded - 10-14)
				OnRaftCommand();
				yield return new WaitForSeconds(0.5f);
				IslandObjectState.Capture(e);
				IslandSpawner.Despawn(e.Root);
				e.Root = null;
				yield return null;
				e.Loading = true;
				yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, ContentState.IsUsed(e, keyKey), "10 days later, loaded again: the chest with the story key stays empty - no second key (AU19)");
				Check(ref ok, ContentState.IsUsed(e, everKey), "... the 'Once ever' zone stays used (AU19)");
				Check(ref ok, !ContentState.IsUsed(e, plainKey) && !ContentState.IsUsed(e, plainZoneKey), "... while a plain chest and a plain once-zone are ready again (as loot: the regrow days)");
			}
			finally
			{
				StoryBook.Take("ciscbackkey", 99);
				ScRemove(made, isl);
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario come back"); else Fail("scenario come back");
		}

		[ConsoleCommand(name: "CIScWaitSave", docs: "Dev, world (host, 'CI ...'): AT21 - what comes after a wait isn't lost: CIScWaitSave prep = a note's 'wait 8 then show' with the island unloaded and loaded again during the wait (the vault shows), then a second note read just before the runner saves, quits and loads; CIScWaitSave check = after the load its vault shows (AU2)")]
		public static void ScWaitSaveCommand(string[] args) { StartTest(args != null && args.Length > 0 && args[0] == "check" ? ScWaitSaveCheck() : ScWaitSavePrep()); }

		const string WaitIsland = "ciscwait";

		static IEnumerator ScWaitSavePrep()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario wait save prep: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			IslandFile f;
			try { f = ScIsland(WaitIsland, "Waiting Rock"); } catch (Exception ex) { Fail("scenario wait save prep: " + ex.Message); yield break; }
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(-8, 0), 1), ObjectProps.NoteTitle, "First note", ObjectProps.NoteText, "Wait for it.",
				BehaviourProps.EventPrefix + "read", "message||The ground shakes...\nwait||8\nshow|vault1|"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(-8, 10), 2), BehaviourProps.Name, "vault1", BehaviourProps.Hidden, "1"));
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(8, 0), 3), ObjectProps.NoteTitle, "Second note", ObjectProps.NoteText, "Wait again.",
				BehaviourProps.EventPrefix + "read", "wait||20\nshow|vault2|"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(8, 10), 4), BehaviourProps.Name, "vault2", BehaviourProps.Hidden, "1"));
			f.Save(IslandSpawner.PathFor(WaitIsland));
			Vector3? spot = ScSpot(WaitIsland, 400f);
			if (!spot.HasValue) { ScRemove(made, WaitIsland); Fail("scenario wait save prep: no open sea near the raft"); yield break; }
			yield return ScBring(WaitIsland, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, WaitIsland); Fail("scenario wait save prep: the island didn't come"); yield break; }
			// (a) unloaded during the wait, loaded again after it: the vault shows when it loads
			ScReadNote(e, "First note");
			yield return new WaitForSeconds(0.5f);
			NoteReader.Close();
			IslandObjectState.Capture(e);
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return new WaitForSeconds(10f);
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			yield return new WaitForSeconds(1.5f);
			IslandObjectRef v1 = ScObjOf(e, "vault1");
			Check(ref ok, v1 != null && v1.gameObject.activeInHierarchy, "the island unloaded during the note's 8 s wait: loaded again, the vault shows (" + (v1 != null && v1.gameObject.activeInHierarchy ? "shown" : "still hidden") + ") - AU2");
			// (b) the second note's wait (20 s) is still running when the runner saves and quits now
			ScReadNote(e, "Second note");
			yield return new WaitForSeconds(0.5f);
			NoteReader.Close();
			IslandObjectState.Capture(e);
			IslandWorldState.Save();
			OnRaftCommand();
			if (ok) Log("PASS: scenario wait save prep"); else Fail("scenario wait save prep");
		}

		static IEnumerator ScWaitSaveCheck()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario wait save check: run in a world, as the host"); yield break; }
			bool ok = true;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == WaitIsland);
			if (e == null) { Fail("scenario wait save check: the island isn't in the world any more"); yield break; }
			if (e.Root == null)
			{
				// (loaded where it was: bring the raft's player there so it loads)
				PlayerMove.To(RAPI.GetLocalPlayer(), ScLandCentre(e) + Vector3.up * 3f);
				for (float t = 0; t < 30f && e.Root == null; t += 0.5f) yield return new WaitForSeconds(0.5f);
			}
			yield return new WaitForSeconds(2f);
			IslandObjectRef v2 = ScObjOf(e, "vault2");
			Check(ref ok, v2 != null && v2.gameObject.activeInHierarchy, "saved and quit during the second note's 20 s wait: after loading, its vault shows (" + (v2 != null && v2.gameObject.activeInHierarchy ? "shown" : "still hidden") + ") - AU2");
			var gone = new List<IslandWorldState.Entry> { e };
			ScRemove(gone, WaitIsland);
			OnRaftCommand();
			if (ok) Log("PASS: scenario wait save check"); else Fail("scenario wait save check");
		}

		[ConsoleCommand(name: "CIScEarly", docs: "Dev, world (host, 'CI ...'): AT20 - the guide's example quest done backwards (the warthogs defeated and the supplies opened before the diary is read): each step counts when it comes - reading the diary finishes the quest (AU1); SL7 save: steps 3 and 2 done before step 1, the world state written and read back - they still count when step 1 is done, on Quest 1 and Quest 2")]
		public static void ScEarlyCommand() { StartTest(ScEarlyRoutine()); }

		static IEnumerator ScEarlyRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario early: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscearly";
			IslandFile f;
			try { f = ScIsland(isl, "Early Isle"); } catch (Exception ex) { Fail("scenario early: " + ex.Message); yield break; }
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(-10, 0), 1), ObjectProps.NoteTitle, "Diary", ObjectProps.NoteText, "The supplies are in the chest. Watch out for the warthogs."));
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(10, 0), 2), ObjectProps.NoteTitle, "Supplies", ObjectProps.LootItems, "Plank*2"));
			f.Objects.Add(ScObj("Creature_Boar", ScDry(f, new Vector2(0, 14), 3), ObjectProps.CreatureCount, "2", ObjectProps.CreatureRespawn, "0"));
			new IslandQuest { Title = "The camp", Steps = {
				new IslandQuest.Step { Type = "read", Target = "Diary", Count = 1 },
				new IslandQuest.Step { Type = "open", Target = "Supplies", Count = 1 },
				new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 2 } } }.To(f.Props);
			// (Quest 2, for the save part: three note steps no real note matches - the part drives them with events)
			new IslandQuest { Title = "The second camp", Steps = {
				new IslandQuest.Step { Type = "read", Target = "SaveAlpha", Count = 1 },
				new IslandQuest.Step { Type = "read", Target = "SaveBeta", Count = 1 },
				new IslandQuest.Step { Type = "read", Target = "SaveGamma", Count = 1 } } }.To(f.Props, 1);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario early: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("scenario early: the island didn't come"); yield break; }
			try
			{
				yield return ScWaitAnimals(e, "Warthog", 2, 20f);
				foreach (AI_NetworkBehaviour a in ScAnimals(e, "Warthog").ToList()) ScKill(a);
				yield return new WaitForSeconds(2.5f);
				Check(ref ok, QuestTracker.StepOf(e) == 0, "the warthogs defeated first: the quest still waits for the diary (step " + (QuestTracker.StepOf(e) + 1) + ")");
				ScOpenChest(e, "Supplies");
				NoteReader.Close();
				yield return new WaitForSeconds(1f);
				Check(ref ok, QuestTracker.StepOf(e) == 0, "... and the supplies opened: it still waits for the diary");
				ScReadNote(e, "Diary");
				yield return new WaitForSeconds(1.5f);
				NoteReader.Close();
				int steps = QuestTracker.QuestOf(e).Steps.Count;
				Check(ref ok, QuestTracker.StepOf(e) >= steps, "the diary read: the supplies and the warthogs done before count now - the quest is done (" + QuestTracker.StepOf(e) + " of " + steps + ") - AU1");

				// SL7 save part (host): steps 3 and 2 done before step 1, the world state written and read back - they still count when step 1 is done; the same on Quest 2
				// (the quest's state put to the start first; Quest 2's keys are those of QuestTracker.StepKeyOf/ProgressKeyOf/EarlyKeyOf, which are private)
				foreach (int key in new[] { QuestTracker.StepKey, QuestTracker.ProgressKey, QuestTracker.EarlyKeyBase, QuestTracker.EarlyKeyBase + 1, QuestTracker.EarlyKeyBase + 2,
					0x40200 + 2, 0x40201 + 2, 0x40300 + 1 * 0x40, 0x40300 + 1 * 0x40 + 1, 0x40300 + 1 * 0x40 + 2 }) e.State.Remove(key);
				Check(ref ok, QuestTracker.StepOf(e, 0) == 0 && QuestTracker.StepOf(e, 1) == 0 && QuestTracker.QuestsOf(e) == 2, "save: both quests at their first step to begin with");
				Func<ObjectState, int> yieldOf = s => s != null ? s.Yield : 0;
				ObjectState es;
				// Quest 1: the supplies opened (step 2) and the warthogs defeated (step 3) before the diary
				QuestTracker.Event(e, "open", "Supplies");
				QuestTracker.Event(e, "kill", "Warthog", 2);
				Check(ref ok, QuestTracker.StepOf(e, 0) == 0 && e.State.TryGetValue(QuestTracker.EarlyKeyBase + 1, out es) && yieldOf(es) == 1 && e.State.TryGetValue(QuestTracker.EarlyKeyBase + 2, out es) && yieldOf(es) == 2,
					"save: Quest 1 - steps 3 and 2 done early are remembered, the quest still waits for step 1");
				// Quest 2: step 3 and step 2 before step 1
				QuestTracker.Event(e, "read", "SaveGamma");
				QuestTracker.Event(e, "read", "SaveBeta");
				Check(ref ok, QuestTracker.StepOf(e, 1) == 0 && e.State.ContainsKey(0x40300 + 1 * 0x40 + 1) && e.State.ContainsKey(0x40300 + 1 * 0x40 + 2), "save: Quest 2 - steps 3 and 2 done early are remembered");
				// the world file's write and read (IslandWorldState.Save writes IslandObjectState.Encode of the state; a joining player's island list uses the same)
				string encoded = IslandObjectState.Encode(e.State);
				e.State = IslandObjectState.Decode(encoded);
				Check(ref ok, e.State.TryGetValue(QuestTracker.EarlyKeyBase + 1, out es) && yieldOf(es) == 1 && e.State.TryGetValue(QuestTracker.EarlyKeyBase + 2, out es) && yieldOf(es) == 2 && e.State.ContainsKey(0x40300 + 1 * 0x40 + 1) && e.State.ContainsKey(0x40300 + 1 * 0x40 + 2) &&
					QuestTracker.StepOf(e, 0) == 0 && QuestTracker.StepOf(e, 1) == 0, "save: the early steps (both quests) are there after the state is written and read back (" + encoded.Length + " chars)");
				QuestTracker.Event(e, "read", "Diary");
				Check(ref ok, QuestTracker.StepOf(e, 0) >= steps, "save: Quest 1 - step 1 done after the reload: steps 2 and 3 count, the quest is done (" + QuestTracker.StepOf(e, 0) + " of " + steps + ")");
				QuestTracker.Event(e, "read", "SaveAlpha");
				Check(ref ok, QuestTracker.StepOf(e, 1) >= 3, "save: Quest 2 - step 1 done after the reload: steps 2 and 3 count, the quest is done (" + QuestTracker.StepOf(e, 1) + " of 3)");
				Check(ref ok, !e.State.ContainsKey(QuestTracker.EarlyKeyBase + 1) && !e.State.ContainsKey(QuestTracker.EarlyKeyBase + 2) && !e.State.ContainsKey(0x40300 + 1 * 0x40 + 1), "save: the early marks are used up once credited");
			}
			finally { ScRemove(made, isl); }
			OnRaftCommand();
			if (ok) Log("PASS: scenario early"); else Fail("scenario early");
		}

		[ConsoleCommand(name: "CIScReread", docs: "Dev, world (host, 'CI ...'): AT22 - a note that gives 10 planks and one that uses up 5 scrap for an ingot, each read three times: 10 planks in all, one ingot for 5 scrap - a re-read shows the note's messages only (AU16)")]
		public static void ScRereadCommand() { StartTest(ScRereadRoutine()); }

		static IEnumerator ScRereadRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario reread: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscreread";
			IslandFile f;
			try { f = ScIsland(isl, "Reading Rock"); } catch (Exception ex) { Fail("scenario reread: " + ex.Message); yield break; }
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(-8, 0), 1), ObjectProps.NoteTitle, "Gift note", ObjectProps.NoteText, "Take these.",
				BehaviourProps.EventPrefix + "read", "give||Plank*10\nmessage||Ten planks!"));
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(8, 0), 2), ObjectProps.NoteTitle, "Trade note", ObjectProps.NoteText, "Scrap for metal.",
				BehaviourProps.CheckPrefix + "read", "take|Scrap|5", BehaviourProps.EventPrefix + "read", "give||MetalIngot*1\nmessage||An ingot for your scrap.",
				BehaviourProps.ElsePrefix + "read", "message||Bring 5 scrap."));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario reread: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("scenario reread: the island didn't come"); yield break; }
			PlayerInventory inv = RAPI.GetLocalPlayer().Inventory;
			int planks0 = inv.GetItemCount("Plank"), ingots0 = inv.GetItemCount("MetalIngot");
			inv.AddItem("Scrap", 15);
			int scrap0 = inv.GetItemCount("Scrap");
			try
			{
				for (int i = 0; i < 3; i++) { ScReadNote(e, "Gift note"); yield return new WaitForSeconds(0.8f); NoteReader.Close(); }
				for (int i = 0; i < 3; i++) { ScReadNote(e, "Trade note"); yield return new WaitForSeconds(0.8f); NoteReader.Close(); }
				yield return new WaitForSeconds(0.5f);
				int planks = inv.GetItemCount("Plank") - planks0, ingots = inv.GetItemCount("MetalIngot") - ingots0, scrapUsed = scrap0 - inv.GetItemCount("Scrap");
				Check(ref ok, planks == 10, "the note that gives 10 planks, read three times: 10 planks in all (" + planks + ") - AU16");
				Check(ref ok, ingots == 1 && scrapUsed == 5, "the note that takes 5 scrap for an ingot, read three times: one ingot for 5 scrap (" + ingots + " ingot(s), " + scrapUsed + " scrap used) - AU16");
			}
			finally
			{
				int left = inv.GetItemCount("Scrap");
				if (left > 0) inv.RemoveItem("Scrap", Mathf.Min(left, 15));
				ScRemove(made, isl);
			}
			OnRaftCommand();
			if (ok) Log("PASS: scenario reread"); else Fail("scenario reread");
		}

		[ConsoleCommand(name: "CIScAlphaClicks", docs: "Dev, world (host, 'CI ...'): AT36 - an alpha's stats are given once: looked at again three times, as every change of the randomizer does (even its level clicked again), its health stays x3 (not x9) and it isn't healed (AU10)")]
		public static void ScAlphaClicksCommand() { StartTest(ScAlphaClicksRoutine()); }

		static IEnumerator ScAlphaClicksRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario alpha clicks: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscalpha";
			IslandFile f;
			try { f = ScIsland(isl, "Alpha Rock"); } catch (Exception ex) { Fail("scenario alpha clicks: " + ex.Message); yield break; }
			f.Objects.Add(ScObj("Creature_Boar", ScDry(f, new Vector2(0, 0), 1), ObjectProps.CreatureCount, "1", ObjectProps.CreatureRespawn, "0"));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 400f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario alpha clicks: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made.FirstOrDefault();
			if (e == null || e.Root == null) { ScRemove(made, isl); Fail("scenario alpha clicks: the island didn't come"); yield break; }
			yield return ScWaitAnimals(e, "Warthog", 1, 20f);
			AI_NetworkBehaviour boar = ScAnimals(e, "Warthog").FirstOrDefault();
			if (boar == null || boar.networkEntity == null || boar.networkEntity.stat_health == null) { ScRemove(made, isl); Fail("scenario alpha clicks: no warthog came"); yield break; }
			Stat_Health h = boar.networkEntity.stat_health;
			float raft = h.Max;
			// (as the world's roll makes one of Raft's own warthogs an alpha)
			WorldRandomizer.ApplyAlphaForTest(boar);
			yield return new WaitForSeconds(0.5f);
			float alpha = h.Max;
			Check(ref ok, alpha > raft * 2.5f, "an alpha: x3 health (" + raft.ToString("F0") + " -> " + alpha.ToString("F0") + ")");
			h.Value = alpha / 2f;
			for (int i = 0; i < 3; i++) { WorldRandomizer.ApplyAlphaForTest(boar); yield return new WaitForSeconds(0.5f); }
			Check(ref ok, Mathf.Abs(h.Max - alpha) < 1f, "looked at again three times (the randomizer set again): still x3, not more (" + alpha.ToString("F0") + " -> " + h.Max.ToString("F0") + ") - AU10");
			Check(ref ok, h.Value <= alpha / 2f + 1f, "... and not healed (" + h.Value.ToString("F0") + " of " + h.Max.ToString("F0") + ")");
			ScRemove(made, isl);
			OnRaftCommand();
			if (ok) Log("PASS: scenario alpha clicks"); else Fail("scenario alpha clicks");
		}

		[ConsoleCommand(name: "CIScLastKill", docs: "Dev, world (host, 'CI ...'): SC24 - the last warthog killed and the island unloaded within half a second: it stays dead and the kill step counts (AU60)")]
		public static void ScLastKillCommand() { StartTest(ScLastKillRoutine()); }

		static IEnumerator ScLastKillRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario last kill: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscatlast";
			IslandFile f;
			try { f = ScIsland(isl, "Last Boar"); } catch (Exception ex) { Fail("scenario last kill: " + ex.Message); yield break; }
			f.Objects.Add(ScObj("Creature_Boar", ScDry(f, new Vector2(0, 0), 1), ObjectProps.CreatureCount, "1", ObjectProps.CreatureRespawn, "0"));
			new IslandQuest { Title = "Last", Steps = { new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 1 } } }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario last kill: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			yield return ScWaitAnimals(e, "Warthog", 1, 20f);
			yield return new WaitForSeconds(1.5f);
			AI_NetworkBehaviour boar = ScAnimals(e, "Warthog").FirstOrDefault();
			if (boar == null) { ScRemove(made, isl); Fail("scenario last kill: no warthog came"); yield break; }
			ScKill(boar);
			yield return new WaitForSeconds(0.3f);
			IslandObjectState.Capture(e);
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return null;
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			yield return new WaitForSeconds(5f);
			Check(ref ok, ScAnimals(e, "Warthog").Count == 0, "killed and unloaded within half a second: the warthog stays dead (" + ScAnimals(e, "Warthog").Count + " alive) - AU60");
			Check(ref ok, QuestTracker.StepOf(e) >= 1, "... and the kill step counts (" + QuestTracker.StepOf(e) + ")");
			ScRemove(made, isl);
			if (ok) Log("PASS: scenario last kill"); else Fail("scenario last kill");
		}

		[ConsoleCommand(name: "CIScStray", docs: "Dev, world (host, 'CI ...'): a turtle and a warthog Raft takes away alive (destroyed, as Raft does with its sea animals and far ones) aren't defeated: the kill step stays, both come back when the island loads again (AU85)")]
		public static void ScStrayCommand() { StartTest(ScStrayRoutine()); }

		static IEnumerator ScStrayRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario stray: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscstray";
			IslandFile f;
			try { f = ScIsland(isl, "Stray Lagoon"); } catch (Exception ex) { Fail("scenario stray: " + ex.Message); yield break; }
			Vector3? under = ScUnderSea(f, 4f);
			if (!under.HasValue) { Fail("scenario stray: the sample island has no sea floor 4 m under its sea"); yield break; }
			f.Objects.Add(ScObj("Creature_Turtle", under.Value, ObjectProps.CreatureCount, "2", ObjectProps.CreatureRespawn, "0"));
			f.Objects.Add(ScObj("Creature_Boar", ScDry(f, new Vector2(0, 0), 1), ObjectProps.CreatureCount, "1", ObjectProps.CreatureRespawn, "0"));
			new IslandQuest { Title = "Stray", Steps = { new IslandQuest.Step { Type = "kill", Target = "Turtle", Count = 1 }, new IslandQuest.Step { Type = "kill", Target = "Warthog", Count = 1 } } }.To(f.Props);
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario stray: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			yield return ScWaitAnimals(e, "Turtle", 2, 20f);
			yield return ScWaitAnimals(e, "Warthog", 1, 20f);
			yield return new WaitForSeconds(1.5f);
			AI_NetworkBehaviour turtle = ScAnimals(e, "Turtle").FirstOrDefault(), boar = ScAnimals(e, "Warthog").FirstOrDefault();
			if (turtle == null || boar == null) { ScRemove(made, isl); Fail("scenario stray: the animals didn't come (" + ScAnimals(e, "Turtle").Count + " turtles, " + ScAnimals(e, "Warthog").Count + " warthogs)"); yield break; }
			// (as Raft takes them away: the object goes, nothing kills it)
			UnityEngine.Object.Destroy(turtle.gameObject);
			UnityEngine.Object.Destroy(boar.gameObject);
			yield return new WaitForSeconds(3f);
			Check(ref ok, QuestTracker.StepOf(e) == 0, "a turtle and a warthog 300 m off taken away alive: no kill step done (" + QuestTracker.StepOf(e) + ")");
			IslandObjectState.Capture(e);
			IslandSpawner.Despawn(e.Root);
			e.Root = null;
			yield return null;
			e.Loading = true;
			yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
			yield return ScWaitAnimals(e, "Turtle", 2, 20f);
			yield return ScWaitAnimals(e, "Warthog", 1, 10f);
			Check(ref ok, ScAnimals(e, "Turtle").Count == 2, "... both turtles back when the island loads again (" + ScAnimals(e, "Turtle").Count + ")");
			Check(ref ok, ScAnimals(e, "Warthog").Count == 1, "... and the warthog (" + ScAnimals(e, "Warthog").Count + ")");
			ScRemove(made, isl);
			if (ok) Log("PASS: scenario stray"); else Fail("scenario stray");
		}

		#endregion

		#region SC6, SC36 - sleeping with the mod's timers; anchored at an island for days

		[ConsoleCommand(name: "CIScSleep", docs: "Dev, world (host, 'CI ...'): SC6/SC36 - Raft's sleep (BedManager.Slumber) moves the day: an 'on day N+1' rule fires; a 'wait 30 s' started before still ends; a looted chest stays empty while the island is loaded and is full when it loads after the regrow days")]
		public static void ScSleepCommand() { StartTest(ScSleepRoutine()); }

		static IEnumerator ScSleepRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario sleep: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscsleep", plan = "ci scenario sleep";
			IslandFile f;
			try { f = ScIsland(isl, "Sleepy Cove"); } catch (Exception ex) { Fail("scenario sleep: " + ex.Message); yield break; }
			f.Props[IslandProps.RegrowDays] = "3";
			f.Objects.Add(ScObj("Loot_Chest", ScDry(f, new Vector2(0, 0), 1), ObjectProps.NoteTitle, "Sleep chest", ObjectProps.LootItems, "Plank*1"));
			f.Objects.Add(ScObj("Note_Paper", ScDry(f, new Vector2(10, 0), 2), ObjectProps.NoteTitle, "Sleep note", ObjectProps.NoteText, "Something comes after a while.",
				BehaviourProps.EventPrefix + "read", "wait||30\nshow|vault|"));
			f.Objects.Add(ScObj("Note_Sign", ScDry(f, new Vector2(-10, 0), 3), BehaviourProps.Name, "vault", BehaviourProps.Hidden, "1"));
			f.Save(IslandSpawner.PathFor(isl));
			string planBefore = WorldDirector.PlanName;
			var doneBefore = WorldDirector.Done.ToList();
			int? today = ScDay;
			if (!today.HasValue) { Fail("scenario sleep: no day counter"); yield break; }
			// (Raft's BedManager.Slumber is a private static coroutine, started by the bed manager when everyone sleeps)
			BedManager beds = ComponentManager<BedManager>.Value ?? UnityEngine.Object.FindObjectOfType<BedManager>() ?? Resources.FindObjectsOfTypeAll<BedManager>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			MethodInfo slumber = typeof(BedManager).GetMethod("Slumber", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (slumber == null) { Fail("scenario sleep: Raft's BedManager.Slumber wasn't found"); yield break; }
			MonoBehaviour runner = beds != null ? (MonoBehaviour)beds : DynamicIslands.instance;
			Vector3? spot = ScSpot(isl, 320f);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario sleep: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario sleep: the island didn't come"); yield break; }
			WorldPlan.Parse(plan, "random = off\nrule = tomorrow | type:wreck | day:" + (today.Value + 1) + " | ahead:800 | | Tomorrow\n").Save();
			try
			{
				ScSetPlan(plan, true);
				WorldDirector.Done.Remove("tomorrow");
				ScOpenChest(e, "Sleep chest");
				ScReadNote(e, "Sleep note");
				float readAt = Time.time;
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, !WorldDirector.Done.Contains("tomorrow"), "'on day " + (today.Value + 1) + "' waits today (day " + today.Value + ")");
				// Sleep through one night: late evening, then Raft's own slumber
				for (int night = 0; night < 4; night++)
				{
					AzureSkyHour(22f);
					yield return null;
					IEnumerator s = slumber.Invoke(slumber.IsStatic ? null : beds, new object[] { true }) as IEnumerator;
					if (s != null) yield return runner.StartCoroutine(s);
					yield return new WaitForSeconds(1.5f);
					if (night == 0)
					{
						Check(ref ok, ScDay == today.Value + 1, "one night's sleep: the day counter moves on (" + today.Value + " -> " + ScDay + ")");
						Check(ref ok, WorldDirector.Done.Contains("tomorrow"), "... and the 'on day " + (today.Value + 1) + "' rule fires within seconds");
					}
				}
				Check(ref ok, ScDay >= today.Value + 4, "four nights slept (day " + ScDay + ")");
				// The note's wait (30 game seconds from reading): ends after its time, sleeping or not
				IslandObjectRef vault = ScObjOf(e, "vault");
				for (float t = 0; Time.time - readAt < 33f && t < 40f; t += 0.5f) yield return new WaitForSeconds(0.5f);
				yield return new WaitForSeconds(1f);
				Check(ref ok, vault != null && vault.gameObject.activeInHierarchy, "the note's 'wait 30 s then show' ends after 30 game seconds (" + (Time.time - readAt).ToString("F0") + " s after reading: " + (vault != null && vault.gameObject.activeInHierarchy ? "shown" : "still hidden") + ")");
				// Regrow 3, four days later: still empty while loaded; full once the island loads again
				LootCrate chest = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault();
				Check(ref ok, chest != null && chest.Looted, "anchored at the island, 4 days later: the chest stays empty while it is loaded (regrowth comes when it loads)");
				IslandObjectState.Capture(e);
				IslandSpawner.Despawn(e.Root);
				e.Root = null;
				yield return null;
				e.Loading = true;
				yield return DynamicIslands.instance.SpawnIslandFile(e.Name, e.Position, false, e);
				yield return new WaitForSeconds(1f);
				chest = e.Root != null ? e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault() : null;
				Check(ref ok, chest != null && !chest.Looted, "... loaded again after the regrow days: the chest is full again");
			}
			finally
			{
				if (System.IO.File.Exists(WorldPlan.PathFor(plan))) System.IO.File.Delete(WorldPlan.PathFor(plan));
				WorldDirector.Done.Clear();
				foreach (string d in doneBefore) WorldDirector.Done.Add(d);
				if (!WorldDirector.SetPlan(planBefore, false)) WorldDirector.SetPlan(WorldPlan.RandomName, false);
				AzureSkyHour(10f);
			}
			// (the wreck the day rule brought, and the test island)
			made.AddRange(IslandWorldState.Islands.Where(x => x.Rule == "tomorrow"));
			ScRemove(made, isl);
			OnRaftCommand();
			if (ok) Log("PASS: scenario sleep"); else Fail("scenario sleep");
		}

		/// <summary>Sets the time of day as Raft's own cheat does (CITime).</summary>
		static void AzureSkyHour(float hour)
		{
			var sky = UnityEngine.Object.FindObjectOfType<UnityEngine.AzureSky.AzureSkyController>();
			if (sky != null && sky.timeOfDay != null) sky.timeOfDay.hour = hour;
		}

		#endregion

		#region SC18 - under water when the world is saved and loaded

		[ConsoleCommand(name: "CIScDive", docs: "Dev, world (host, 'CI ...'): SC18 - the player dives to a sunken island's chest 12 m down and opens it (then the runner saves, leaves and loads). CIScDive check = after loading: not inside the ground, not held under water, the chest still opened")]
		public static void ScDiveCommand(string[] args) { StartTest(args != null && args.Length > 0 && args[0] == "check" ? ScDiveCheckRoutine() : ScDiveRoutine()); }

		static IEnumerator ScDiveRoutine()
		{
			if (!CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("scenario dive: run in a world, as the host"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			var made = new List<IslandWorldState.Entry>();
			const string isl = "ciscdive";
			IslandFile f;
			try { f = ScIsland(isl, "Deep Reef"); } catch (Exception ex) { Fail("scenario dive: " + ex.Message); yield break; }
			Vector3 top = ScDry(f, new Vector2(0, 0), 1);
			// (sunk so the barrel lies 12 m under the sea: the ground there stands this high above the island's own sea level)
			float sink = -((top.y - f.WaterLevel) + 12f);
			f.Elevation = sink;
			f.Objects.Add(ScObj("Loot_SunkenBarrel", top, ObjectProps.NoteTitle, "Reef barrel", ObjectProps.LootItems, "Plank*1", ObjectProps.LootRefill, "0"));
			f.Save(IslandSpawner.PathFor(isl));
			Vector3? spot = ScSpot(isl, 320f, sink);
			if (!spot.HasValue) { ScRemove(made, isl); Fail("scenario dive: no open sea near the raft"); yield break; }
			yield return ScBring(isl, spot.Value, made);
			IslandWorldState.Entry e = made[0];
			if (e.Root == null) { ScRemove(made, isl); Fail("scenario dive: the island didn't come"); yield break; }
			LootCrate barrel = e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault();
			Check(ref ok, barrel != null && barrel.transform.position.y < -8f, "the barrel lies under water (y " + (barrel != null ? barrel.transform.position.y.ToString("F1") : "?") + ")");
			if (barrel != null)
			{
				PlayerMove.To(RAPI.GetLocalPlayer(), barrel.transform.position + Vector3.up * 1.5f, ControllerType.Water);
				yield return new WaitForSeconds(1f);
				barrel.LastGiven = new List<string>();
				barrel.Open();
				yield return new WaitForSeconds(1f);
				Check(ref ok, barrel.Looted, "opened under water");
				Log("  diving at y " + RAPI.GetLocalPlayer().transform.position.y.ToString("F1") + " (the runner saves and loads now)");
			}
			if (ok) Log("PASS: scenario dive"); else Fail("scenario dive");
		}

		static IEnumerator ScDiveCheckRoutine()
		{
			yield return new WaitForSeconds(5f);
			bool ok = true;
			Network_Player p = RAPI.GetLocalPlayer();
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(x => x.HostName == "ciscdive");
			Terrain t = e != null && e.Root != null ? e.Root.GetComponentInChildren<Terrain>() : null;
			float ground = t != null ? t.SampleHeight(p.transform.position) + t.transform.position.y : -999f;
			Check(ref ok, p.transform.position.y > ground - 0.5f, "after loading, the player isn't inside the sunken island's ground (y " + p.transform.position.y.ToString("F1") + ", ground " + ground.ToString("F1") + ")");
			float y0 = p.transform.position.y;
			yield return new WaitForSeconds(4f);
			bool held = Mathf.Abs(p.transform.position.y - y0) < 0.05f && p.transform.position.y < -2f;
			Check(ref ok, !held, "... and isn't held under water (y " + y0.ToString("F1") + " -> " + p.transform.position.y.ToString("F1") + ")");
			LootCrate barrel = e != null && e.Root != null ? e.Root.GetComponentsInChildren<LootCrate>(true).FirstOrDefault() : null;
			Check(ref ok, barrel != null && barrel.Looted, "the barrel opened before the save is still opened");
			if (e != null) ScRemove(new List<IslandWorldState.Entry> { e }, "ciscdive");
			OnRaftCommand();
			if (ok) Log("PASS: scenario dive check"); else Fail("scenario dive check");
		}

		#endregion
	}
}
