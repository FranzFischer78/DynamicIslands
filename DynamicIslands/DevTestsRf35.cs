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
	/// <summary>RF3 survivors in cages (Survivors.cs) and RF5 boss phases (BossPhases.cs).</summary>
	public static partial class DevTests
	{
		#region Survivors (RF3)

		const string RfSurvIsland = "cirfsurv", RfSurvMpIsland = "cirfsurvmp", RfSurvKey = "cirf-cage-key", RfSurvId = "cirf-mara", RfSurvName = "Mara";
		const string RfSurvLine = "You found me! I thought the sea had me for good.";

		static IslandFile RfSurvFile(string island)
		{
			IslandFile f = ScIsland(island, "Survivor test");
			Vector3 at = ScDry(f, Vector2.zero, 7);
			f.Objects.Add(ScObj(Survivors.CageObject, at, Survivors.IdProp, RfSurvId, Survivors.NameProp, RfSurvName, Survivors.LineProp, RfSurvLine, Survivors.KeyProp, StoryItems.Ref(RfSurvKey)));
			return f;
		}

		/// <summary>The crew's cage keys set to exactly n (host).</summary>
		static void RfKeys(int n)
		{
			int have = StoryBook.Count(RfSurvKey);
			if (have > n) StoryBook.Take(RfSurvKey, have - n); else if (have < n) StoryBook.Give(RfSurvKey, n - have);
		}

		static SurvivorCage RfCage(IslandWorldState.Entry e)
		{
			return e != null && e.Root != null ? e.Root.GetComponentsInChildren<SurvivorCage>(true).FirstOrDefault() : null;
		}

		static bool RfCageHidden(SurvivorCage c)
		{
			return c != null && c.Cage != null && c.Cage.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled);
		}

		static bool RfOnRaft(GameObject figure)
		{
			Raft raft = ComponentManager<Raft>.Value;
			return figure != null && raft != null && figure.transform.parent == raft.transform && figure.activeInHierarchy;
		}

		[ConsoleCommand(name: "CIRfSurvivor", docs: "Dev, in game (host): RF3 survivor cage - locked without its story key; with it, it opens (the key used up), the survivor says its line in a bubble, signal 'survivor', then stands on the raft; using it again does nothing")]
		public static void RfSurvivorCommand(string[] args) { StartTest(RfSurvivorRoutine()); }

		static IEnumerator RfSurvivorRoutine()
		{
			string where = GwWhere("rf survivor");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { Survivors.CageObject });
			Check(ref ok, PlaceableCatalog.Get(Survivors.CageObject) != null, "the cage object loads (from Raft's " + Survivors.CageSource + ")");
			Survivors.Clear();
			RfKeys(0);
			yield return GwSpawnFile(g, RfSurvFile(RfSurvIsland), RfSurvIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				SurvivorCage c = RfCage(e);
				Check(ref ok, c != null, "the cage is on the island");
				if (c == null) { Fail("rf survivor"); yield break; }
				yield return new WaitForSeconds(1f);
				Check(ref ok, c.Figure != null && c.Figure.activeInHierarchy && !c.IsOpen, "the survivor stands in the closed cage");
				// (the shark cage's rope - loose on the ground away from Radio tower's crane - is left out of the survivor cage,
				// and only out of it: Raft's shark cage keeps it)
				GameObject shark = PlaceableCatalog.Get(Survivors.CageSource);
				MeshFilter[] own = c.Cage.GetComponentsInChildren<MeshFilter>(true), raft = shark != null ? shark.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];
				Func<Mesh, long> tris = mesh => { long n = 0; for (int i = 0; mesh != null && i < mesh.subMeshCount; i++) n += mesh.GetIndexCount(i) / 3; return n; };
				Check(ref ok, Survivors.LeftOut.Count > 0 && own.Length > 0 && own.Length == raft.Length && c.Cage.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled) &&
					own.Zip(raft, (a, b) => a.sharedMesh != b.sharedMesh && tris(a.sharedMesh) > 0 && tris(a.sharedMesh) < tris(b.sharedMesh) && !b.sharedMesh.name.Contains("survivor")).All(x => x),
					"the cage shows without the shark cage's loose rope (" + string.Join(" | ", Survivors.LeftOut.Select(l => l.Length > 60 ? l.Substring(0, 60) : l).ToArray()) + "); Raft's shark cage keeps it");
				int bubbles = Survivors.Bubbles, rescued = Survivors.RescuedCount;
				PutPlayerNear(c.transform);
				bool used = c.Use();
				yield return new WaitForSeconds(0.5f);
				Check(ref ok, !used && !c.IsOpen && !ContentState.IsUsed(e, c.KeyOf()) && Survivors.Bubbles == bubbles, "without the key the cage stays locked");
				RfKeys(1);
				yield return new WaitForSeconds(0.3f);
				Check(ref ok, StoryBook.Count(RfSurvKey) == 1, "the crew has the key");
				used = c.Use();
				yield return new WaitForSeconds(1f);
				Check(ref ok, used && c.IsOpen && ContentState.IsUsed(e, c.KeyOf()), "with the key the cage opens");
				Check(ref ok, StoryBook.Count(RfSurvKey) == 0, "the key is used up (" + StoryBook.Count(RfSurvKey) + " left)");
				Check(ref ok, Survivors.Bubbles == bubbles + 1 && Survivors.LastBubble == RfSurvName + ": " + RfSurvLine, "the survivor's bubble: " + Survivors.LastBubble);
				Check(ref ok, Survivors.Has(RfSurvId) && Survivors.RescuedCount == rescued + 1, "the world's list has the survivor");
				Check(ref ok, Behaviours.HasSignal(e, Survivors.DefaultSignal), "signal '" + Survivors.DefaultSignal + "' sent");
				Check(ref ok, Survivors.WriteLines().Any(l => l.StartsWith("@survivor=" + RfSurvId + "|" + RfSurvName)), "the world file gets the survivor (" + string.Join(" ", Survivors.WriteLines().ToArray()) + ")");
				yield return new WaitForSeconds(Survivors.BubbleTime + 2.5f);
				GameObject fig = Survivors.FigureOf(RfSurvId);
				Check(ref ok, c.Figure == null && RfOnRaft(fig) && Survivors.FigureCount == 1, "after the bubble the survivor stands on the raft" + (fig != null ? " at " + fig.transform.localPosition.ToString("F1") : ""));
				RfKeys(1);
				yield return new WaitForSeconds(0.3f);
				used = c.Use();
				yield return new WaitForSeconds(1f);
				Check(ref ok, !used && StoryBook.Count(RfSurvKey) == 1 && Survivors.Bubbles == bubbles + 1 && Survivors.RescuedCount == rescued + 1 && Survivors.FigureCount == 1, "using the open cage again does nothing (the key kept, no second survivor)");
				yield return EnsureAlive();
			}
			finally { GwDrop(g, RfSurvIsland); Survivors.Clear(); RfKeys(0); }
			if (ok) Log("PASS: rf survivor"); else Fail("rf survivor");
		}

		[ConsoleCommand(name: "CIRfSurvivorReload", docs: "Dev, in game (host): RF3 across reloads - the island unloaded and loaded again: the cage stays open, no survivor in it, one on the raft (not two); the world file's @survivor line read back gives the same list")]
		public static void RfSurvivorReloadCommand(string[] args) { StartTest(RfSurvivorReloadRoutine()); }

		static IEnumerator RfSurvivorReloadRoutine()
		{
			string where = GwWhere("rf survivor reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { Survivors.CageObject });
			Survivors.Clear();
			RfKeys(1);
			yield return GwSpawnFile(g, RfSurvFile(RfSurvIsland), RfSurvIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				SurvivorCage c = RfCage(e);
				if (c == null) { Fail("rf survivor reload: no cage"); yield break; }
				yield return new WaitForSeconds(1f);
				PutPlayerNear(c.transform);
				c.Use();
				yield return new WaitForSeconds(Survivors.BubbleTime + 2.5f);
				Check(ref ok, c.IsOpen && RfOnRaft(Survivors.FigureOf(RfSurvId)), "before: the cage open, the survivor on the raft");
				int bubbles = Survivors.Bubbles;

				yield return GwReload(e);
				yield return new WaitForSeconds(1.5f);
				c = RfCage(e);
				Check(ref ok, c != null && c.IsOpen && c.Figure == null && RfCageHidden(c), "reloaded: the cage is open and empty");
				Check(ref ok, Survivors.Bubbles == bubbles, "reloaded: no bubble again");
				Check(ref ok, Survivors.All.Count(r => r.Id == RfSurvId) == 1 && Survivors.FigureCount == 1 && RfOnRaft(Survivors.FigureOf(RfSurvId)), "reloaded: one survivor on the raft (" + Survivors.FigureCount + ")");
				Check(ref ok, Behaviours.HasSignal(e, Survivors.DefaultSignal), "reloaded: signal 'survivor' is kept");

				// The world file: written, the list emptied (a world being read), the line read back - in one frame, before the
				// open cage's own check (0.4 s) puts the survivor back
				List<string> lines = Survivors.WriteLines().ToList();
				Survivors.Reset();
				Check(ref ok, !Survivors.Has(RfSurvId) && Survivors.All.Count == 0, "the list emptied");
				foreach (string l in lines) { int eq = l.IndexOf('='); Survivors.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				Check(ref ok, Survivors.All.Count(r => r.Id == RfSurvId && r.Name == RfSurvName) == 1, "the world file's line read back");
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, Survivors.FigureCount == 1 && RfOnRaft(Survivors.FigureOf(RfSurvId)), "after the read the survivor is on the raft, once (" + Survivors.FigureCount + ")");

				// A world saved between the open and the list (the list lacks an open cage's survivor): the cage puts it back,
				// with no second signal or rescue
				int rescuedBefore = Survivors.RescuedCount;
				Survivors.Reset();
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, Survivors.Has(RfSurvId) && Survivors.RescuedCount == rescuedBefore && Survivors.FigureCount == 1, "the open cage puts its missing survivor back on the list, as a heal (no new rescue)");
				c = RfCage(e);
				yield return new WaitForSeconds(1f);
				Check(ref ok, Survivors.All.Count == 1 && Survivors.Bubbles == bubbles, "the open cage doesn't free the survivor a second time");
				yield return EnsureAlive();
			}
			finally { GwDrop(g, RfSurvIsland); Survivors.Clear(); RfKeys(0); }
			if (ok) Log("PASS: rf survivor reload"); else Fail("rf survivor reload");
		}

		[ConsoleCommand(name: "CIRfSurvivorMp", docs: "Dev, two-player test: RF3 - CIRfSurvivorMp setup (host: island 'cirfsurvmp' with a locked cage, the crew holds its key) / open (this player opens it) / check (either: the cage open, the survivor in the world's list and on the raft) / end (host)")]
		public static void RfSurvivorMpCommand(string[] args) { StartTest(RfSurvivorMpRoutine(args != null && args.Length > 0 ? args[0] : "check")); }

		static IEnumerator RfSurvivorMpRoutine(string part)
		{
			if (RAPI.GetLocalPlayer() == null) { Fail("rf survivor mp: in a world"); yield break; }
			if (part == "setup" || part == "end")
			{
				if (!Raft_Network.IsHost) { Fail("rf survivor mp " + part + ": as the host"); yield break; }
				Survivors.Clear();
				RfKeys(part == "setup" ? 1 : 0);
				if (part == "end")
				{
					IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => x.HostName == RfSurvMpIsland).Select(x => x.Id).ToList(), true);
					try { if (System.IO.File.Exists(IslandSpawner.PathFor(RfSurvMpIsland))) System.IO.File.Delete(IslandSpawner.PathFor(RfSurvMpIsland)); } catch { }
					IslandCache.Forget();
					Log("PASS: rf survivor mp end");
					yield break;
				}
				yield return PlaceableCatalog.EnsureLoaded(new List<string> { Survivors.CageObject });
				yield return ScMpSpawn(RfSurvMpIsland, "Survivor test", f => { IslandFile s = RfSurvFile(RfSurvMpIsland); f.Objects.AddRange(s.Objects); });
				yield break;
			}
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName == RfSurvMpIsland);
			float t0 = Time.realtimeSinceStartup;
			while ((e == null || RfCage(e) == null) && Time.realtimeSinceStartup - t0 < 30f)
			{
				yield return new WaitForSeconds(0.5f);
				e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName == RfSurvMpIsland);
			}
			SurvivorCage c = RfCage(e);
			if (c == null) { Fail("rf survivor mp " + part + " (" + GupRole + "): no cage on '" + RfSurvMpIsland + "' here"); yield break; }
			bool ok = true;
			if (part == "open")
			{
				t0 = Time.realtimeSinceStartup;
				while (StoryBook.Count(RfSurvKey) == 0 && Time.realtimeSinceStartup - t0 < 10f) yield return new WaitForSeconds(0.5f);
				Check(ref ok, StoryBook.Count(RfSurvKey) == 1 && !c.IsOpen, GupRole + ": the cage closed, the crew's key here");
				PutPlayerNear(c.transform);
				c.Use();
				t0 = Time.realtimeSinceStartup;
				while (!c.IsOpen && Time.realtimeSinceStartup - t0 < 10f) yield return new WaitForSeconds(0.25f);
				Check(ref ok, c.IsOpen, GupRole + ": the cage opened (" + (Time.realtimeSinceStartup - t0).ToString("F1") + " s)");
				Check(ref ok, Survivors.LastBubble == RfSurvName + ": " + RfSurvLine, GupRole + ": the bubble: " + Survivors.LastBubble);
				if (ok) Log("PASS: rf survivor mp open (" + GupRole + ")"); else Fail("rf survivor mp open (" + GupRole + ")");
				yield break;
			}
			// check: everything settled (the bubble gone) on this machine
			t0 = Time.realtimeSinceStartup;
			while (!(c.IsOpen && Survivors.Has(RfSurvId) && RfOnRaft(Survivors.FigureOf(RfSurvId))) && Time.realtimeSinceStartup - t0 < Survivors.BubbleTime + 15f) yield return new WaitForSeconds(0.5f);
			GameObject fig = Survivors.FigureOf(RfSurvId);
			Log("RF survivor status " + GupRole + ": open=" + GsYes(c.IsOpen) + " inCage=" + GsYes(c.Figure != null) + " listed=" + Survivors.All.Count + " onRaft=" + Survivors.FigureCount +
				" key=" + StoryBook.Count(RfSurvKey) + " signal=" + GsYes(Behaviours.HasSignal(e, Survivors.DefaultSignal)) + (fig != null ? " at=" + fig.transform.localPosition.ToString("F1") : ""));
			Check(ref ok, c.IsOpen && c.Figure == null, GupRole + ": the cage is open and empty");
			Check(ref ok, Survivors.All.Count == 1 && Survivors.Has(RfSurvId) && Survivors.FigureCount == 1 && RfOnRaft(fig), GupRole + ": the survivor is on the raft, once");
			Check(ref ok, StoryBook.Count(RfSurvKey) == 0, GupRole + ": the key is used up");
			Check(ref ok, Behaviours.HasSignal(e, Survivors.DefaultSignal), GupRole + ": signal 'survivor'");
			if (ok) Log("PASS: rf survivor mp check (" + GupRole + ")"); else Fail("rf survivor mp check (" + GupRole + ")");
		}

		[ConsoleCommand(name: "CIRfSurvivorAnim", docs: "Dev, in game: logs how a survivor figure's animator is set up beside the local player's own (where the Animator sits, avatar, controller, parameters, bound bones) - for its pose")]
		public static void RfSurvivorAnimCommand(string[] args) { StartTest(RfSurvivorAnimRoutine()); }

		static string RfAnimInfo(Animator an, Transform top)
		{
			if (an == null) return "no Animator";
			Transform hips = an.isHuman ? an.GetBoneTransform(HumanBodyBones.Hips) : null;
			AnimatorClipInfo[] clips = an.isActiveAndEnabled && an.runtimeAnimatorController != null ? an.GetCurrentAnimatorClipInfo(0) : new AnimatorClipInfo[0];
			string pars = an.isActiveAndEnabled && an.runtimeAnimatorController != null ? string.Join(" ", an.parameters.Select(p => p.name + "(" + p.type + ")=" + (p.type == AnimatorControllerParameterType.Float ? an.GetFloat(p.name).ToString("0.##") : p.type == AnimatorControllerParameterType.Int ? an.GetInteger(p.name).ToString() : p.type == AnimatorControllerParameterType.Bool ? an.GetBool(p.name).ToString() : "trig")).ToArray()) : "-";
			return "at '" + PathOf(an.transform, top) + "' enabled=" + an.isActiveAndEnabled + " human=" + an.isHuman + " avatar=" + (an.avatar != null ? an.avatar.name + (an.avatar.isValid ? "" : " INVALID") : "none")
				+ " controller=" + (an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "none") + " cull=" + an.cullingMode + " hips=" + (hips != null ? PathOf(hips, top) : "none")
				+ " layers=" + an.layerCount + " clips=" + string.Join(",", clips.Select(c => c.clip != null ? c.clip.name : "?").ToArray()) + " params: " + pars;
		}

		static IEnumerator RfSurvivorAnimRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) { Fail("rf survivor anim: run in a world"); yield break; }
			yield return new WaitForSeconds(0.5f);
			PlayerAnimator pa = player.GetComponentInChildren<PlayerAnimator>(true);
			Log("player animator: " + RfAnimInfo(pa != null ? pa.anim : player.GetComponentInChildren<Animator>(true), player.transform));
			foreach (CharacterModelModifications cm in player.GetComponentsInChildren<CharacterModelModifications>(true))
				Log("player model: '" + PathOf(cm.transform, player.transform) + "' active=" + cm.gameObject.activeInHierarchy + " own Animator=" + (cm.GetComponent<Animator>() != null));
			GameObject m = Survivors.Model(Survivors.CharacterFor("", "cirf-anim"), "CI_SurvivorAnim");
			if (m == null) { Fail("rf survivor anim: no model"); yield break; }
			try
			{
				m.transform.position = player.transform.position + player.transform.forward * 3f;
				m.SetActive(true);
				Log("figure before: " + RfAnimInfo(m.GetComponentInChildren<Animator>(true), m.transform));
				yield return new WaitForSeconds(1.5f);
				Log("figure after 1.5 s: " + RfAnimInfo(m.GetComponentInChildren<Animator>(true), m.transform));
			}
			finally { UnityEngine.Object.Destroy(m); }
			Log("PASS: rf survivor anim (info only)");
		}

		[ConsoleCommand(name: "CIRfSurvivorShot", docs: "Dev, in game (host): RF3 pictures - the survivor in its cage (shot_rf_survivor_cage.png), its bubble when freed (shot_rf_survivor_bubble.png) and on the raft (shot_rf_survivor_raft.png)")]
		public static void RfSurvivorShotCommand(string[] args) { StartTest(RfSurvivorShotRoutine()); }

		static IEnumerator RfSurvivorShotRoutine()
		{
			string where = GwWhere("rf survivor shot");
			if (where != null) { Fail(where); yield break; }
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { Survivors.CageObject });
			Survivors.Clear();
			RfKeys(1);
			yield return GwSpawnFile(g, RfSurvFile(RfSurvIsland), RfSurvIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			bool ok = true;
			try
			{
				SurvivorCage c = RfCage(e);
				if (c == null) { Fail("rf survivor shot: no cage"); yield break; }
				yield return new WaitForSeconds(1.5f);
				Transform cage = c.Cage != null ? c.Cage.transform : c.transform;
				Vector3 mid = c.Figure != null ? c.Figure.transform.position + Vector3.up * 1f : cage.position + Vector3.up;
				Vector3 side = cage.forward;
				side.y = 0f;
				if (side.sqrMagnitude < 0.01f) side = Vector3.forward;
				side.Normalize();
				Vector3 from = mid + side * 4.5f + Vector3.right * 1.5f + Vector3.up * 0.8f;
				yield return CameraShot(from, mid, "rf_survivor_cage");
				PutPlayerNear(c.transform, 3f);
				c.Use();
				yield return new WaitForSeconds(1f);
				Check(ref ok, c.IsOpen, "the cage opened");
				yield return CameraShot(from + Vector3.up * 0.6f, mid + Vector3.up * 0.8f, "rf_survivor_bubble");
				yield return new WaitForSeconds(Survivors.BubbleTime + 2.5f);
				GameObject fig = Survivors.FigureOf(RfSurvId);
				Check(ref ok, RfOnRaft(fig), "the survivor is on the raft");
				if (fig != null)
				{
					Vector3 f = fig.transform.position + Vector3.up * 1f, out1 = fig.transform.forward;
					yield return CameraShot(f + out1 * 3.5f + Vector3.up * 0.8f + fig.transform.right * 1.2f, f, "rf_survivor_raft");
				}
				yield return EnsureAlive();
			}
			finally { GwDrop(g, RfSurvIsland); Survivors.Clear(); RfKeys(0); }
			if (ok) Log("PASS: rf survivor shot"); else Fail("rf survivor shot");
		}

		#endregion

		#region Boss phases (RF5)

		const string RfBossIsland = "cirfboss", RfBossMpIsland = "cirfbossmp";

		static IslandFile RfBossFile(string island, int seed)
		{
			float elev;
			IslandGenSettings s = MapTypes.Roll(MapTypes.Get("boss"), new System.Random(seed), out elev);
			return MapTypes.Create(MapTypes.Get("boss"), s, elev, island);
		}

		static CreatureSpawnPoint RfBossSpot(IslandWorldState.Entry e)
		{
			return e != null && e.Root != null ? e.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).FirstOrDefault(p => BossPhases.Has(p.Props)) : null;
		}

		static bool RfAlive(AI_NetworkBehaviour a) { return a != null && a.networkEntity != null && a.networkEntity.stat_health != null && !a.networkEntity.IsDead && a.networkEntity.stat_health.Value > 0f; }

		static AI_NetworkBehaviour RfBoss(CreatureSpawnPoint p) { return p != null ? p.Spawned.FirstOrDefault(RfAlive) : null; }

		static IEnumerator RfWaitBoss(IslandWorldState.Entry e, float seconds)
		{
			// (the boss map type's beast waits for its arena zone: a player steps in)
			CreatureSpawnPoint p = RfBossSpot(e);
			string zone = p != null ? ObjectProps.Get(p.Props, ObjectProps.CreatureZone) : "";
			if (zone.Length > 0 && RfBoss(p) == null) ScEnterZone(e, zone);
			float t0 = Time.realtimeSinceStartup;
			while (RfBoss(RfBossSpot(e)) == null && Time.realtimeSinceStartup - t0 < seconds) yield return new WaitForSeconds(0.5f);
		}

		static float RfShare(AI_NetworkBehaviour a) { return a.networkEntity.stat_health.Value / a.networkEntity.stat_health.Max; }

		/// <summary>A player's hit through Raft's Network_Host.DamageEntity (as a weapon's).</summary>
		static void RfHit(AI_NetworkBehaviour a, float damage)
		{
			Network_Host host = ComponentManager<Network_Host>.Value;
			if (host != null && a != null) host.DamageEntity(a.networkEntity, a.transform, damage, a.transform.position + Vector3.up, Vector3.up, EntityType.Player, null);
		}

		class RfFight { public int Hits; public float Before = -1f, After = -1f; }

		/// <summary>Hits of step × the boss's full health until its phase starts (or 40 hits).</summary>
		static IEnumerator RfHitToPhase(AI_NetworkBehaviour a, float step, RfFight r)
		{
			int s0 = BossPhases.Started;
			float dmg = step * a.networkEntity.stat_health.Max;
			while (BossPhases.Started == s0 && r.Hits < 40 && RfAlive(a))
			{
				r.Before = RfShare(a);
				RfHit(a, dmg);
				r.Hits++;
				r.After = RfShare(a);
				yield return null;
			}
		}

		static void RfKill(AI_NetworkBehaviour a)
		{
			if (RfAlive(a)) a.networkEntity.Damage(100000f, a.transform.position, Vector3.up, EntityType.Player, true);
		}

		[ConsoleCommand(name: "CIRfBossPhase", docs: "Dev, in game (host): RF5 boss phase on the boss map type's beast - hit down past half: two more come, it gets faster, signal 'phase' and its message once; more hits don't repeat it; its death still sends 'boss' and ends the phase")]
		public static void RfBossPhaseCommand(string[] args) { StartTest(RfBossPhaseRoutine()); }

		static IEnumerator RfBossPhaseRoutine()
		{
			string where = GwWhere("rf boss phase");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { MapTypes.ArenaStake });
			yield return GwSpawnFile(g, RfBossFile(RfBossIsland, 6161), RfBossIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				yield return RfWaitBoss(e, 40f);
				CreatureSpawnPoint p = RfBossSpot(e);
				AI_NetworkBehaviour boss = RfBoss(p);
				Check(ref ok, p != null && boss != null, "the beast is there with a phase (" + (p != null ? BossPhases.Describe(p.Props) : "no spot") + ")");
				if (boss == null) { Fail("rf boss phase"); yield break; }
				float share = BossPhases.ShareOf(p.Props), speed0 = CreatureSpawner.SpeedOf(boss);
				int expect = CreatureSpawner.SpawnsInThisMode(BossPhases.SummonKindOf(p).Type) ? BossPhases.SummonCount(p.Props) : 0;
				int started = BossPhases.Started;
				var r = new RfFight();
				yield return RfHitToPhase(boss, 0.17f, r);
				Check(ref ok, BossPhases.Started == started + 1 && r.Before > share && r.After <= share && r.After > 0f, "the phase starts at the hit that takes it to " + Mathf.RoundToInt(r.After * 100f) + "% (from " + Mathf.RoundToInt(r.Before * 100f) + "%, hit " + r.Hits + ")");
				Check(ref ok, BossPhases.InPhase(e, p), "the fight's phase is in the island's state");
				yield return new WaitForSeconds(1f);
				Check(ref ok, CreatureSpawner.SummonedAlive(p) == expect && p.Summoned.Where(RfAlive).All(a => a.gameObject.activeInHierarchy), expect + " summons appear (" + CreatureSpawner.SummonedAlive(p) + ")");
				float want = Mathf.Clamp(speed0 * BossPhases.SpeedOf(p.Props), ObjectProps.MinMultiplier, 4f);
				Check(ref ok, Mathf.Abs(CreatureSpawner.SpeedOf(boss) - want) < 0.01f, "its speed x" + speed0.ToString("0.##") + " -> x" + CreatureSpawner.SpeedOf(boss).ToString("0.##"));
				Check(ref ok, Behaviours.HasSignal(e, BossPhases.DefaultSignal), "signal 'phase' sent");
				Check(ref ok, (Behaviours.LastMessage ?? "").Contains("roars"), "the phase event's message: " + Behaviours.LastMessage);
				for (int i = 0; i < 2 && RfAlive(boss); i++) { RfHit(boss, 0.05f * boss.networkEntity.stat_health.Max); yield return new WaitForSeconds(1.2f); }
				Check(ref ok, BossPhases.Started == started + 1 && p.Summoned.Count == expect, "more hits don't start it again (" + (BossPhases.Started - started) + " start(s), " + p.Summoned.Count + " summons)");
				RfKill(boss);
				float t0 = Time.realtimeSinceStartup;
				while (!Behaviours.HasSignal(e, MapTypes.BossSignal) && Time.realtimeSinceStartup - t0 < 8f) yield return new WaitForSeconds(0.5f);
				Check(ref ok, Behaviours.HasSignal(e, MapTypes.BossSignal), "its death still sends 'boss' (the summons don't hold it up: " + CreatureSpawner.SummonedAlive(p) + " still about)");
				Check(ref ok, !BossPhases.InPhase(e, p) && BossPhases.Started == started + 1, "the fight is over: the phase is cleared");
				yield return EnsureAlive();
			}
			finally { GwDrop(g, RfBossIsland); }
			if (ok) Log("PASS: rf boss phase"); else Fail("rf boss phase");
		}

		[ConsoleCommand(name: "CIRfBossPhaseReload", docs: "Dev, in game (host): RF5 across a reload mid-fight - the beast in its phase, the island unloaded and loaded again: it comes back in its phase (at that share, still faster), no new summons, no second start; its death sends 'boss'")]
		public static void RfBossPhaseReloadCommand(string[] args) { StartTest(RfBossPhaseReloadRoutine()); }

		static IEnumerator RfBossPhaseReloadRoutine()
		{
			string where = GwWhere("rf boss phase reload");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			var g = new GwIsland();
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { MapTypes.ArenaStake });
			yield return GwSpawnFile(g, RfBossFile(RfBossIsland, 6262), RfBossIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) yield break;
			try
			{
				yield return RfWaitBoss(e, 40f);
				CreatureSpawnPoint p = RfBossSpot(e);
				AI_NetworkBehaviour boss = RfBoss(p);
				if (boss == null) { Fail("rf boss phase reload: no beast"); yield break; }
				float share = BossPhases.ShareOf(p.Props), speed0 = CreatureSpawner.SpeedOf(boss);
				int started = BossPhases.Started;
				var r = new RfFight();
				yield return RfHitToPhase(boss, 0.17f, r);
				Check(ref ok, BossPhases.Started == started + 1 && BossPhases.InPhase(e, p), "before: the phase started (" + Mathf.RoundToInt(r.After * 100f) + "%)");

				yield return GwReload(e);
				yield return RfWaitBoss(e, 40f);
				yield return new WaitForSeconds(1f);
				p = RfBossSpot(e);
				boss = RfBoss(p);
				Check(ref ok, p != null && boss != null, "reloaded: the beast is back");
				if (boss == null) { Fail("rf boss phase reload"); yield break; }
				Check(ref ok, BossPhases.InPhase(e, p), "reloaded: the fight is still in its phase");
				Check(ref ok, RfShare(boss) <= share + 0.001f, "reloaded: its health at most the phase's share (" + Mathf.RoundToInt(RfShare(boss) * 100f) + "%)");
				float want = Mathf.Clamp(speed0 * BossPhases.SpeedOf(p.Props), ObjectProps.MinMultiplier, 4f);
				Check(ref ok, Mathf.Abs(CreatureSpawner.SpeedOf(boss) - want) < 0.01f, "reloaded: still faster (x" + CreatureSpawner.SpeedOf(boss).ToString("0.##") + ")");
				Check(ref ok, BossPhases.Started == started + 1 && p.Summoned.Count == 0, "reloaded: no second start, no new summons (" + p.Summoned.Count + ")");
				RfHit(boss, 0.05f * boss.networkEntity.stat_health.Max);
				yield return new WaitForSeconds(1.2f);
				Check(ref ok, BossPhases.Started == started + 1, "a hit after the reload doesn't start it again");
				RfKill(boss);
				float t0 = Time.realtimeSinceStartup;
				while (!Behaviours.HasSignal(e, MapTypes.BossSignal) && Time.realtimeSinceStartup - t0 < 8f) yield return new WaitForSeconds(0.5f);
				Check(ref ok, Behaviours.HasSignal(e, MapTypes.BossSignal) && !BossPhases.InPhase(e, p), "its death sends 'boss', the phase is cleared");
				yield return EnsureAlive();
			}
			finally { GwDrop(g, RfBossIsland); }
			if (ok) Log("PASS: rf boss phase reload"); else Fail("rf boss phase reload");
		}

		[ConsoleCommand(name: "CIRfBossNightmare", docs: "Dev, in game (host): RF5 under the Nightmare monster difficulty - a hit takes a smaller share of the beast's health than under Normal, and the phase still starts at the hit that takes it to half")]
		public static void RfBossNightmareCommand(string[] args) { StartTest(RfBossNightmareRoutine()); }

		static IEnumerator RfBossNightmareRoutine()
		{
			string where = GwWhere("rf boss nightmare");
			if (where != null) { Fail(where); yield break; }
			bool ok = true;
			var g = new GwIsland();
			int level = MonsterDifficulty.Current;
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { MapTypes.ArenaStake });
			yield return GwSpawnFile(g, RfBossFile(RfBossIsland, 6363), RfBossIsland);
			IslandWorldState.Entry e = g.E;
			if (e == null) { MonsterDifficulty.Current = level; yield break; }
			try
			{
				yield return RfWaitBoss(e, 40f);
				CreatureSpawnPoint p = RfBossSpot(e);
				AI_NetworkBehaviour boss = RfBoss(p);
				if (boss == null) { Fail("rf boss nightmare: no beast"); yield break; }
				float share = BossPhases.ShareOf(p.Props), max = boss.networkEntity.stat_health.Max, h0 = boss.networkEntity.stat_health.Value;
				// (one hit under Normal and one under Nightmare, the health put back after each: the share a hit takes)
				MonsterDifficulty.Current = MonsterDifficulty.Normal;
				RfHit(boss, 0.17f * max);
				float normal = (h0 - boss.networkEntity.stat_health.Value) / max;
				boss.networkEntity.stat_health.Value = h0;
				MonsterDifficulty.Current = MonsterDifficulty.Nightmare;
				RfHit(boss, 0.17f * max);
				float nightmare = (h0 - boss.networkEntity.stat_health.Value) / max;
				boss.networkEntity.stat_health.Value = h0;
				Check(ref ok, nightmare > 0f && nightmare < normal * 0.75f, "under Nightmare a hit takes " + Mathf.RoundToInt(nightmare * 100f) + "% of its health (Normal: " + Mathf.RoundToInt(normal * 100f) + "%)");
				int started = BossPhases.Started;
				var r = new RfFight();
				yield return RfHitToPhase(boss, 0.17f, r);
				Check(ref ok, BossPhases.Started == started + 1 && r.Before > share && r.After <= share && r.After > 0f, "Nightmare: the phase starts at the hit that takes it to " + Mathf.RoundToInt(r.After * 100f) + "% (from " + Mathf.RoundToInt(r.Before * 100f) + "%, after " + r.Hits + " hits)");
				Check(ref ok, Mathf.Approximately(boss.networkEntity.stat_health.Max, max), "the health's full share is unchanged (" + boss.networkEntity.stat_health.Max + ")");
				RfKill(boss);
				yield return new WaitForSeconds(1.5f);
				yield return EnsureAlive();
			}
			finally { MonsterDifficulty.Current = level; GwDrop(g, RfBossIsland); }
			if (ok) Log("PASS: rf boss nightmare"); else Fail("rf boss nightmare");
		}

		[ConsoleCommand(name: "CIRfBossPhaseMp", docs: "Dev, two-player test: RF5 - CIRfBossPhaseMp setup (host: the boss island 'cirfbossmp'; logs the beast's full health) / hit <share> (this player hits the beast for that share of its full health, through Raft's DamageEntity: a guest's goes to the host) / status (either: phase, signal, the animals seen) / check (either: the phase started once, the beast and its summons seen here) / end (host)")]
		public static void RfBossPhaseMpCommand(string[] args)
		{
			string part = args != null && args.Length > 0 ? args[0] : "status";
			float share = 0.3f;
			if (args != null && args.Length > 1) float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out share);
			StartTest(RfBossPhaseMpRoutine(part, share));
		}

		static float rfMpMax = -1f;
		static int rfMpStarted = -1;

		static IEnumerator RfBossPhaseMpRoutine(string part, float share)
		{
			if (RAPI.GetLocalPlayer() == null || !CustomIslandSpawner.RaftPosition.HasValue) { Fail("rf boss mp: in a world"); yield break; }
			if (part == "setup" || part == "end")
			{
				if (!Raft_Network.IsHost) { Fail("rf boss mp " + part + ": as the host"); yield break; }
				IslandWorldState.RemoveIds(IslandWorldState.Islands.Where(x => x.HostName == RfBossMpIsland).Select(x => x.Id).ToList(), true);
				if (part == "end")
				{
					try { if (System.IO.File.Exists(IslandSpawner.PathFor(RfBossMpIsland))) System.IO.File.Delete(IslandSpawner.PathFor(RfBossMpIsland)); } catch { }
					IslandCache.Forget();
					Log("PASS: rf boss mp end");
					yield break;
				}
				yield return PlaceableCatalog.EnsureLoaded(new List<string> { MapTypes.ArenaStake });
				RfBossFile(RfBossMpIsland, 6464).Save(IslandSpawner.PathFor(RfBossMpIsland));
				Vector3? spot = null;
				for (float d = 400f; !spot.HasValue && d <= 1600f; d += 300f)
				{
					Vector3? s = CustomIslandSpawner.FindClearSpot(CustomIslandSpawner.RaftPosition.Value, CustomIslandSpawner.LandRadius(RfBossMpIsland), d);
					if (s.HasValue) spot = new Vector3(s.Value.x, 0f, s.Value.z);
				}
				if (!spot.HasValue) { Fail("rf boss mp: no open sea near the raft"); yield break; }
				var made = new List<IslandWorldState.Entry>();
				yield return ScBring(RfBossMpIsland, spot.Value, made);
				IslandWorldState.Entry me = made.FirstOrDefault();
				if (me == null || me.Root == null) { Fail("rf boss mp: the island didn't come"); yield break; }
				yield return RfWaitBoss(me, 40f);
				AI_NetworkBehaviour b = RfBoss(RfBossSpot(me));
				if (b == null) { Fail("rf boss mp: no beast"); yield break; }
				rfMpMax = b.networkEntity.stat_health.Max;
				rfMpStarted = BossPhases.Started;
				Log("PASS: rf boss mp setup (the " + RfBossSpot(me).Kind.Label + ", full health " + rfMpMax.ToString("F0") + ", " + BossPhases.Describe(RfBossSpot(me).Props) + ")");
				yield break;
			}
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName == RfBossMpIsland);
			CreatureSpawnPoint p = RfBossSpot(e);
			if (p == null || p.Kind == null) { Fail("rf boss mp " + part + " (" + GupRole + "): '" + RfBossMpIsland + "' is not loaded here"); yield break; }
			Func<List<AI_NetworkBehaviour>> seen = () => AnimalsOf(e, p.Kind.Label, 150f).Where(RfAlive).ToList();
			if (part == "hit")
			{
				// (the animal with the lowest id: the same one on both players)
				AI_NetworkBehaviour a = seen().OrderBy(x => x.ObjectIndex).FirstOrDefault();
				if (a == null) { Fail("rf boss mp hit (" + GupRole + "): no live " + p.Kind.Label + " seen here"); yield break; }
				float max = a.networkEntity.stat_health.Max;
				PutPlayerNear(a.transform, 4f);
				RfHit(a, share * max);
				yield return new WaitForSeconds(1.5f);
				Log("PASS: rf boss mp hit (" + GupRole + ": " + Mathf.RoundToInt(share * 100f) + "% of " + max.ToString("F0") + (Raft_Network.IsHost ? "" : ", sent to the host") + ")");
				yield break;
			}
			bool ok = true;
			if (part == "check")
			{
				float t0 = Time.realtimeSinceStartup;
				while (!(BossPhases.InPhase(e, p) && seen().Count >= 1 + BossPhases.SummonCount(p.Props)) && Time.realtimeSinceStartup - t0 < 15f) yield return new WaitForSeconds(0.5f);
			}
			List<AI_NetworkBehaviour> live = seen();
			AI_NetworkBehaviour host = Raft_Network.IsHost ? RfBoss(p) : null;
			Log("RF boss status " + GupRole + ": phase=" + GsYes(BossPhases.InPhase(e, p)) + " signal=" + GsYes(Behaviours.HasSignal(e, BossPhases.DefaultSignal)) + " seen=" + live.Count +
				(Raft_Network.IsHost ? " started=" + (BossPhases.Started - rfMpStarted) + " summons=" + CreatureSpawner.SummonedAlive(p) + (host != null ? " health=" + Mathf.RoundToInt(RfShare(host) * 100f) + "%" : " beast=down") : "") +
				" message=" + (Behaviours.LastMessage ?? "").Replace(' ', '_'));
			if (part == "status") { Log("PASS: rf boss mp status (" + GupRole + ")"); yield break; }
			int expect = BossPhases.SummonCount(p.Props);
			Check(ref ok, BossPhases.InPhase(e, p) && Behaviours.HasSignal(e, BossPhases.DefaultSignal), GupRole + ": the phase started, signal 'phase'");
			Check(ref ok, live.Count >= 1 + expect, GupRole + ": the beast and its " + expect + " summons are seen here (" + live.Count + ")");
			if (Raft_Network.IsHost) Check(ref ok, BossPhases.Started - rfMpStarted == 1 && CreatureSpawner.SummonedAlive(p) == expect, "host: one start, " + expect + " summons");
			if (ok) Log("PASS: rf boss mp check (" + GupRole + ")"); else Fail("rf boss mp check (" + GupRole + ")");
		}

		#endregion
	}
}
