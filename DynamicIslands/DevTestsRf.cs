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
	/// <summary>ROADMAP RF1/RF2: the when/then actions "playsound", "stopsound" and "camera", and a plan's ending (credits).</summary>
	public static partial class DevTests
	{
		const string RfSoundIsland = "cirfsound", RfSoundMpIsland = "cirfmp";

		/// <summary>A looped sound and a short one Raft has (the first music cue, a short sound).</summary>
		static void RfSounds(out string loop, out string once)
		{
			List<string> ev = SoundLibrary.Events;
			loop = ev.FirstOrDefault(s => s.IndexOf("/music/", StringComparison.OrdinalIgnoreCase) >= 0) ?? ev.FirstOrDefault() ?? "";
			once = ev.FirstOrDefault(s => s.IndexOf("click", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("/ui/", StringComparison.OrdinalIgnoreCase) >= 0) ?? ev.LastOrDefault() ?? "";
		}

		/// <summary>The test island: levers for the sounds and the camera, a marker and a thing to look at 16 m apart.</summary>
		static IslandFile RfBuild(string name)
		{
			string loop, once;
			RfSounds(out loop, out once);
			var s = new IslandGenSettings { Seed = 7171, Radius = 50f, Height = 8f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(s, name);
			var objs = new[]
			{
				new[] { "jukebox", "-6", "0", "playsound||" + loop + "|60%|loop|near" },
				new[] { "bell", "-3", "0", "playsound||" + once + "|once" },
				new[] { "stopper", "0", "0", "stopsound|jukebox|" },
				new[] { "quiet", "3", "0", "stopsound||all" },
				new[] { "camlever", "6", "0", "camera|marker|look|2|50" },
				new[] { "farcam", "0", "-18", "camera|marker|look|2|8" },
				new[] { "marker", "6", "12", "" },
				new[] { "look", "-6", "18", "" },
			};
			foreach (string[] o in objs)
			{
				Vector3 at = ScDry(f, new Vector2(float.Parse(o[1]), float.Parse(o[2])), 3);
				f.Objects.Add(o[3].Length > 0
					? ScObj("Note_Sign", at, BehaviourProps.Name, o[0], BehaviourProps.Use, "Pull", BehaviourProps.EventPrefix + "use", o[3])
					: ScObj("Note_Sign", at, BehaviourProps.Name, o[0]));
			}
			IslandWorldState.Remove(name);
			f.Save(IslandSpawner.PathFor(name));
			IslandCache.Forget();
			return f;
		}

		static IEnumerator RfSpawn(string name, float maxDistance, Action<IslandWorldState.Entry> got)
		{
			yield return EnsureAlive();
			yield return PlaceableCatalog.EnsureLoaded(new List<string> { "Note_Sign" });
			RfBuild(name);
			Vector3? spot = ScSpot(name, maxDistance);
			if (!spot.HasValue) { Fail("rf: no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(name, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name);
			if (e == null || e.Root == null) { Fail("rf: " + name + " did not spawn"); yield break; }
			yield return new WaitForSeconds(1f);
			got(e);
		}

		static void RfRemove(string name)
		{
			TriggerSounds.StopAll();
			IslandWorldState.Remove(name);
			try { if (System.IO.File.Exists(IslandSpawner.PathFor(name))) System.IO.File.Delete(IslandSpawner.PathFor(name)); } catch { }
			IslandCache.Forget();
		}

		/// <summary>The local player out of the island's reach (Behaviours.Near false): on the raft when that is far enough, else at sea beyond it.</summary>
		static void RfAway(IslandWorldState.Entry e)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null) return;
			float need = Mathf.Max(0f, CustomIslandSpawner.LandRadius(e.Name)) + 160f;
			Raft r = UnityEngine.Object.FindObjectOfType<Raft>();
			Vector3 from = r != null ? r.transform.position : player.transform.position;
			Vector2 d = new Vector2(from.x - e.Position.x, from.z - e.Position.z);
			if (d.magnitude >= need && r != null) { OnRaftCommand(); return; }
			Vector2 dir = d.sqrMagnitude > 1f ? d.normalized : Vector2.right;
			PlayerMove.To(player, new Vector3(e.Position.x + dir.x * need, 1f, e.Position.z + dir.y * need));
			KeepAlive(player);
		}

		#region RF1 sounds

		[ConsoleCommand(name: "CIRfSound", docs: "Dev, world (host, test world 'CI ...'): the when/then 'play sound' and 'stop sound' (RF1) - the action read and described, Check warns on a sound Raft doesn't have; a lever plays a looped sound (an FMOD source here, kept in the island's state, told to every player), a short one plays once and is not kept, 'stop sound' on one object and 'all' remove it, the player leaving the island stops it and coming back starts the loop again (TEST_CATALOGUE RF1)")]
		public static void RfSoundCommand() { StartTest(RfSoundRoutine()); }

		static IEnumerator RfSoundRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("rf sound: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			string loop, once;
			RfSounds(out loop, out once);
			Check(ref ok, SoundLibrary.Events.Count > 0 && loop.Length > 0 && once.Length > 0, "Raft's sounds listed (" + SoundLibrary.Events.Count + "): loop '" + loop + "', once '" + once + "'");
			if (!ok) { Fail("rf sound"); yield break; }

			// The action read, written and described
			TriggerSounds.Spec sp = TriggerSounds.Parse(loop + "| 60% |island|loop");
			Check(ref ok, sp.Path == loop && Mathf.Abs(sp.Volume - 0.6f) < 0.001f && sp.Island && sp.Loop, "the action's sound read: path, 60%, island, loop");
			Check(ref ok, TriggerSounds.Join(sp) == loop + "|0.6|loop|island" && TriggerSounds.Parse(TriggerSounds.Join(sp)).Volume == sp.Volume, "... and written back: " + TriggerSounds.Join(sp));
			Check(ref ok, TriggerSounds.Parse(once).Volume > 0.89f && !TriggerSounds.Parse(once).Loop && !TriggerSounds.Parse(once).Island, "a bare path: once, near the object, 90%");
			ObjAction act = ObjAction.Parse("playsound||" + loop + "|0.5|loop|island");
			Check(ref ok, act != null && act.Verb == "playsound" && BehaviourProps.SharedVerbs.Contains("playsound") && BehaviourProps.SharedVerbs.Contains("stopsound"), "playsound and stopsound are shared verbs (run on the host for everyone)");
			Check(ref ok, act != null && act.Describe().StartsWith("play ") && act.Describe().Contains("on a loop for everyone on the island"), "it reads as '" + (act != null ? act.Describe() : "?") + "'");
			ObjAction st = ObjAction.Parse("stopsound||all");
			Check(ref ok, st != null && st.Describe().Contains("stop every sound"), "stop all reads as '" + (st != null ? st.Describe() : "?") + "'");

			// Check: a sound Raft doesn't have
			IslandFile bad = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 1, Radius = 30f, Height = 6f }, "cirfbad");
			bad.Objects.Add(new IslandObject { Name = "Note_Sign", Props = P(BehaviourProps.Name, "lever", BehaviourProps.EventPrefix + "use", "playsound||event:/no/such/sound|loop") });
			List<PlanChecker.Finding> traps = PlanChecker.FromFile(bad, "cirfbad", false).Traps;
			Check(ref ok, traps.Any(t => t.Text.Contains("sound Raft doesn't have") && t.Text.Contains("event:/no/such/sound")), "Check warns on an unknown sound name: " + string.Join(" / ", traps.Select(t => t.Text).ToArray()));
			bad.Objects[0].Props[BehaviourProps.EventPrefix + "use"] = "playsound||" + loop + "|loop";
			Check(ref ok, !PlanChecker.FromFile(bad, "cirfbad", false).Traps.Any(t => t.Text.Contains("sound Raft doesn't have")), "... and not on one of Raft's");

			IslandWorldState.Entry e = null;
			yield return RfSpawn(RfSoundIsland, 390f, x => e = x);
			if (e == null) yield break;
			IslandObjectRef jukebox = ScObjOf(e, "jukebox");
			int key = TriggerSounds.KeyBase + jukebox.Index;
			var sent = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => sent.Add(m);
			try
			{
				int started = TriggerSounds.Started;
				ScUse(e, "jukebox");
				yield return new WaitForSeconds(0.6f);
				Check(ref ok, TriggerSounds.CountPlaying(e.Id, true) == 1 && TriggerSounds.AnyAudible(e.Id) && TriggerSounds.Started == started + 1, "the lever plays the looped sound: one FMOD source here, playing (" + TriggerSounds.CountPlaying(e.Id, true) + ", last '" + TriggerSounds.LastPath + "')");
				Check(ref ok, e.State.ContainsKey(key) && e.State[key].Yield == TriggerSounds.Hash(ObjAction.ParseLines(jukebox.Props[BehaviourProps.EventPrefix + "use"]).First().Arg), "the loop is kept in the island's state (key " + key.ToString("X") + ")");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Moment && m.Name == "sound" && m.Index == jukebox.Index && m.Ids[0] == e.Id), "every player is told (message Moment 'sound')");

				ScUse(e, "bell");
				yield return new WaitForSeconds(0.2f);
				Check(ref ok, TriggerSounds.Started == started + 2 && TriggerSounds.LastPath == once, "the bell plays its sound once ('" + TriggerSounds.LastPath + "')");
				Check(ref ok, !e.State.ContainsKey(TriggerSounds.KeyBase + ScObjOf(e, "bell").Index), "... and a once sound is not kept");

				ScUse(e, "stopper");
				yield return new WaitForSeconds(0.6f);
				Check(ref ok, TriggerSounds.CountPlaying(e.Id, true) == 0 && !e.State.ContainsKey(key), "'stop sound' of the jukebox: its source gone, its state gone");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Moment && m.Name == "stop"), "... told to every player (Moment 'stop')");

				ScUse(e, "jukebox");
				yield return new WaitForSeconds(0.6f);
				Check(ref ok, TriggerSounds.CountPlaying(e.Id, true) == 1, "the jukebox again: playing");
				RfAway(e);
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, !Behaviours.Near(e) && TriggerSounds.CountPlaying(e.Id) == 0 && !TriggerSounds.AnyAudible(e.Id) && e.State.ContainsKey(key), "the player leaves the island: the sound stops here (the loop still kept with the island)");
				started = TriggerSounds.Started;
				PutPlayerNear(jukebox.transform);
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, TriggerSounds.CountPlaying(e.Id, true) == 1 && TriggerSounds.Started == started + 1, "back on the island: the loop plays again");

				ScUse(e, "quiet");
				yield return new WaitForSeconds(0.6f);
				Check(ref ok, TriggerSounds.CountPlaying(e.Id) == 0 && !e.State.Keys.Any(k => k >= TriggerSounds.KeyBase && k < TriggerSounds.KeyBase + 0x10000), "'stop sound' all: nothing plays, nothing kept");

				ScUse(e, "jukebox");
				yield return new WaitForSeconds(0.6f);
				IslandSpawner.Despawn(e.Root);
				e.Root = null;
				yield return new WaitForSeconds(1.2f);
				Check(ref ok, TriggerSounds.CountPlaying(e.Id) == 0, "the island unloaded: its sound stops");
			}
			finally { IslandNetwork.Loopback = null; }
			RfAway(e);
			RfRemove(RfSoundIsland);
			if (ok) Log("PASS: rf sound"); else Fail("rf sound");
		}

		[ConsoleCommand(name: "CIRfSoundReload", docs: "Dev, world (host, test world 'CI ...'): a looped sound and a once sound played, the island unloaded and loaded again (as after a world reload): the loop plays again, the once sound is not played again (TEST_CATALOGUE RF1)")]
		public static void RfSoundReloadCommand() { StartTest(RfSoundReloadRoutine()); }

		static IEnumerator RfSoundReloadRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("rf sound reload: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			IslandWorldState.Entry e = null;
			yield return RfSpawn(RfSoundIsland, 390f, x => e = x);
			if (e == null) yield break;
			ScUse(e, "jukebox");
			ScUse(e, "bell");
			yield return new WaitForSeconds(0.6f);
			Check(ref ok, TriggerSounds.CountPlaying(e.Id, true) == 1, "a loop and a once sound played");
			int started = TriggerSounds.Started;
			string loopPath = TriggerSounds.Parse(ObjAction.ParseLines(ScObjOf(e, "jukebox").Props[BehaviourProps.EventPrefix + "use"]).First().Arg).Path;
			yield return ReloadIslandRoutine(e);
			Check(ref ok, e.Root != null, "the island loaded again");
			PutPlayerNear(ScObjOf(e, "jukebox").transform);
			yield return new WaitForSeconds(2f);
			Check(ref ok, TriggerSounds.CountPlaying(e.Id, true) == 1 && TriggerSounds.AnyAudible(e.Id) && TriggerSounds.LastPath == loopPath, "after the reload the loop plays again ('" + TriggerSounds.LastPath + "')");
			Check(ref ok, TriggerSounds.Started == started + 1 && TriggerSounds.CountPlaying(e.Id, false) == 0, "... and only the loop: the once sound is not played again (" + (TriggerSounds.Started - started) + " started)");
			RfAway(e);
			RfRemove(RfSoundIsland);
			if (ok) Log("PASS: rf sound reload"); else Fail("rf sound reload");
		}

		#endregion

		#region RF2 camera

		[ConsoleCommand(name: "CIRfCamera", docs: "Dev, world (host, test world 'CI ...'): the when/then 'camera' (RF2) - a lever: the camera at the marker looking at the target, the player's input held, back after its seconds with the input given back; again and skipped (as Esc does); out of range no moment; a picture shot_rf_camera.png (TEST_CATALOGUE RF2)")]
		public static void RfCameraCommand() { StartTest(RfCameraRoutine()); }

		static IEnumerator RfCameraRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("rf camera: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			string look; float secs, range;
			CameraMoment.Parse("look|2|50", out look, out secs, out range);
			Check(ref ok, look == "look" && secs == 2f && range == 50f, "the action's look-at, seconds and range read");
			CameraMoment.Parse("|99|1", out look, out secs, out range);
			Check(ref ok, look == "" && secs == 30f && range == 5f, "seconds and range kept within 1-30 s and 5-1000 m");
			ObjAction act = ObjAction.Parse("camera|marker|look|2|50");
			Check(ref ok, act != null && BehaviourProps.SharedVerbs.Contains("camera") && act.Describe().Contains("'marker'") && act.Describe().Contains("'look'"), "it reads as '" + (act != null ? act.Describe() : "?") + "'");
			IslandFile bad = IslandGenerator.CreateFile(new IslandGenSettings { Seed = 1, Radius = 30f, Height = 6f }, "cirfbad");
			bad.Objects.Add(new IslandObject { Name = "Note_Sign", Props = P(BehaviourProps.Name, "lever", BehaviourProps.EventPrefix + "use", "camera|nowhere|lever|3|40") });
			Check(ref ok, PlanChecker.FromFile(bad, "cirfbad", false).Traps.Any(t => t.Text.Contains("camera moment") && t.Text.Contains("'nowhere'") && !t.Text.Contains("'lever'")), "Check warns on a marker no object is called");

			IslandWorldState.Entry e = null;
			yield return RfSpawn(RfSoundIsland, 390f, x => e = x);
			if (e == null) yield break;
			Network_Player player = RAPI.GetLocalPlayer();
			Transform marker = ScObjOf(e, "marker").transform, lookAt = ScObjOf(e, "look").transform;
			Vector3 want = marker.position + Vector3.up * 1.6f;
			int shown = CameraMoment.Shown, skipped = CameraMoment.Skipped;
			var sent = new List<IslandNetMessage>();
			IslandNetwork.Loopback = m => sent.Add(m);
			try { ScUse(e, "camlever"); }
			finally { IslandNetwork.Loopback = null; }
			yield return null;
			Check(ref ok, CameraMoment.Active && CameraMoment.Shown == shown + 1 && CameraMoment.InputHeld, "the lever starts the moment, the player's input held");
			Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Moment && m.Name == "camera" && m.Ids[0] == e.Id), "every player is told (Moment 'camera')");
			yield return new WaitForSecondsRealtime(CameraMoment.Glide + 0.4f);
			Transform cam = player.Camera != null ? player.Camera.transform : Camera.main.transform;
			float off = Vector3.Distance(cam.position, want), turn = Vector3.Angle(cam.forward, lookAt.position - want);
			Check(ref ok, off < 0.3f && turn < 3f, "the camera at the marker (" + off.ToString("F2") + " m off), looking at the target (" + turn.ToString("F1") + " degrees off)");
			Shot("rf_camera");
			yield return new WaitForSecondsRealtime(2f + CameraMoment.Glide + 0.3f);
			float home = Vector3.Distance(cam.position, player.transform.position);
			Check(ref ok, !CameraMoment.Active && !CameraMoment.InputHeld && home < 3f, "back after 2 s: the camera with the player (" + home.ToString("F1") + " m), the input given back");

			ScUse(e, "camlever");
			yield return new WaitForSecondsRealtime(1f);
			Check(ref ok, CameraMoment.Active && CameraMoment.InputHeld, "again: the moment runs");
			CameraMoment.Skip();
			yield return null;
			yield return null;
			yield return null;
			home = Vector3.Distance(cam.position, player.transform.position);
			Check(ref ok, !CameraMoment.Active && !CameraMoment.InputHeld && CameraMoment.Skipped == skipped + 1 && home < 3f, "Esc skips it: the camera back at once (" + home.ToString("F1") + " m), the input given back");
			Check(ref ok, CanvasHelper.ActiveMenu != MenuType.PauseMenu, "... and Raft's pause menu is not left open");

			shown = CameraMoment.Shown;
			ScUse(e, "farcam");
			yield return new WaitForSeconds(0.3f);
			Check(ref ok, CameraMoment.Shown == shown && !CameraMoment.Active, "out of the moment's range (" + Vector3.Distance(player.transform.position, marker.position).ToString("F0") + " m, range 8): no moment here");
			RfAway(e);
			RfRemove(RfSoundIsland);
			if (ok) Log("PASS: rf camera"); else Fail("rf camera");
		}

		#endregion

		#region Two players

		[ConsoleCommand(name: "CIRfMp", docs: "Dev, world (host, two-player test): CIRfMp setup = the RF test island 'cirfmp' near the raft, kept for player 2; CIRfMp end = removed. See mprf.ps1")]
		public static void RfMpCommand(string[] args) { StartTest(RfMpRoutine(args != null && args.Length > 0 ? args[0] : "setup")); }

		static IEnumerator RfMpRoutine(string part)
		{
			if (!Raft_Network.IsHost || !CustomIslandSpawner.RaftPosition.HasValue || RAPI.GetLocalPlayer() == null) { Fail("rf mp: run as the host, in a world"); yield break; }
			if (part == "end") { RfRemove(RfSoundMpIsland); Log("PASS: rf mp end"); yield break; }
			IslandWorldState.Entry e = null;
			yield return RfSpawn(RfSoundMpIsland, 390f, x => e = x);
			if (e == null) yield break;
			Log("PASS: rf mp setup (island " + e.Id + ")");
		}

		[ConsoleCommand(name: "CIRfStatus", docs: "Dev, two-player test (either player): one line of what this machine has of 'cirfmp': RF status <role>: loops=.. audible=.. started=.. shown=.. near=.. (see mprf.ps1)")]
		public static void RfStatusCommand() { StartTest(RfStatusRoutine()); }

		static IEnumerator RfStatusRoutine()
		{
			yield return null;
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.Root != null && i.HostName == RfSoundMpIsland);
			if (e == null) { Fail("rf status: '" + RfSoundMpIsland + "' is not loaded here"); yield break; }
			Log("RF status " + GupRole + ": loops=" + TriggerSounds.CountPlaying(e.Id, true) + " audible=" + (TriggerSounds.AnyAudible(e.Id) ? "yes" : "no") + " started=" + TriggerSounds.Started +
				" kept=" + e.State.Keys.Count(k => k >= TriggerSounds.KeyBase && k < TriggerSounds.KeyBase + 0x10000) + " shown=" + CameraMoment.Shown + " near=" + (Behaviours.Near(e) ? "yes" : "no"));
			Log("PASS: rf status " + GupRole);
		}

		[ConsoleCommand(name: "CIRfAway", docs: "Dev, two-player test (either player): the local player out of reach of 'cirfmp' (on the raft, or at sea beyond it)")]
		public static void RfAwayCommand()
		{
			IslandWorldState.Entry e = IslandWorldState.Islands.FirstOrDefault(i => i.HostName == RfSoundMpIsland);
			if (e == null) { Fail("rf away: no '" + RfSoundMpIsland + "'"); return; }
			RfAway(e);
			StartTest(AfterAction("Away from '" + RfSoundMpIsland + "' (" + GupRole + ")", 1.5f));
		}

		#endregion

		#region RF2 ending

		static WorldPlan RfEndingPlan()
		{
			return WorldPlan.Parse("CI rf ending", "story = off\nstoryending = The sea is quiet now.\\nThank you for sailing.\nendingtitle = The Last Lighthouse\ncredits = Ann Builder\\nBo Maker\n" +
				"rule = rfa | type:camp | start | receiver:300 | | Camp A | first | visit | Alpha Camp | 9 | Hello\n" +
				"rule = rfb | type:sandbar | start | receiver:400 | | Bar | after:rfa | visit\n");
		}

		[ConsoleCommand(name: "CIRfEnding", docs: "Dev, world (host, test world 'CI ...'): a plan's ending (RF2) - endingtitle and credits read and written; the last main story step done: the credits (title, makers, closing note) shown once and told to every player; read back from the world file they are not shown again; Raft's notebook keeps the note (TEST_CATALOGUE RF2)")]
		public static void RfEndingCommand() { StartTest(RfEndingRoutine(false)); }

		[ConsoleCommand(name: "CIRfEndingShot", docs: "Dev, world (host, test world 'CI ...'): a picture of a plan's credits as the story ends: shot_rf_credits.png (TEST_CATALOGUE RF2)")]
		public static void RfEndingShotCommand() { StartTest(RfEndingRoutine(true)); }

		static IEnumerator RfEndingRoutine(bool shot)
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || !(SaveAndLoad.CurrentGameFileName ?? "").StartsWith("CI ")) { Fail("rf ending: host, in a test world 'CI ...'"); yield break; }
			bool ok = true;
			List<string> linesBefore = StoryChain.WriteLines().ToList();
			var unlockedBefore = NoteBook.unlockedChunkPointType.ToList();
			var indexesBefore = NoteBook.unlockedNoteBookIndexes.ToList();
			RecieverFrequency[] freqBefore = RecieverFrequency.AllFrequencies != null ? RecieverFrequency.AllFrequencies.ToArray() : null;
			var sent = new List<IslandNetMessage>();
			try
			{
				WorldPlan plan = RfEndingPlan();
				Check(ref ok, plan.EndingTitle == "The Last Lighthouse" && plan.Credits == "Ann Builder\nBo Maker" && plan.StoryEnding == "The sea is quiet now.\nThank you for sailing.", "the plan's ending title, makers and closing note read");
				WorldPlan back = WorldPlan.Parse("t", plan.ToText());
				Check(ref ok, back.EndingTitle == plan.EndingTitle && back.Credits == plan.Credits, "... written and read again the same");
				Check(ref ok, !WorldPlan.Parse("t", "rule = camp | type:camp | start | ahead:350 | | \n").ToText().Split('\n').Any(l => l.StartsWith("credits") || l.StartsWith("endingtitle")), "an older plan writes neither");

				StoryChain.Reset();
				StoryCredits.Close();
				StoryChain.FromPlan(plan);
				QuestBook.Refresh();
				int times = StoryCredits.TimesShown;
				yield return new WaitForSecondsRealtime(1.5f); // (the host sees the story going on)
				Check(ref ok, !QuestBook.StoryOver && !StoryCredits.IsOpen && StoryCredits.TimesShown == times, "while the story goes on: no credits");
				IslandNetwork.Loopback = m => sent.Add(m);
				// (a banner up as the story ends - a milestone or a new island - waits hidden behind the credits: it showed faintly
				// behind the card's top, RF2)
				IslandInfo.Show("CI banner", "", "A banner up as the story ends");
				StoryChain.MarkDone("rule:rfa");
				StoryChain.MarkDone("rule:rfb");
				QuestBook.Refresh();
				yield return new WaitForSecondsRealtime(2.5f);
				Check(ref ok, IslandInfo.BannerAlpha < 0.01f, "no banner shows behind the credits (alpha " + IslandInfo.BannerAlpha.ToString("F2") + ")");
				IslandNetwork.Loopback = null;
				Check(ref ok, QuestBook.StoryOver && StoryCredits.IsOpen && StoryCredits.TimesShown == times + 1, "the last step done: the credits shown (" + (StoryCredits.TimesShown - times) + " time)");
				Check(ref ok, StoryCredits.ShownTitle == "The Last Lighthouse" && StoryCredits.ShownCredits == "Ann Builder\nBo Maker" && StoryCredits.ShownNote == "The sea is quiet now.\nThank you for sailing.",
					"... with the title, the makers and the closing note ('" + StoryCredits.ShownTitle + "' / " + (StoryCredits.ShownCredits ?? "").Replace("\n", ", ") + ")");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Moment && m.Name == "ending" && (m.Data ?? "").Contains("Bo Maker")), "every player is told (Moment 'ending')");
				Check(ref ok, StoryChain.CreditsShown, "'shown' is kept with the world");
				if (shot) { yield return new WaitForSecondsRealtime(0.8f); Shot("rf_credits"); yield return new WaitForSecondsRealtime(1f); }
				yield return new WaitForSecondsRealtime(2.5f);
				Check(ref ok, StoryCredits.TimesShown == times + 1, "shown once only");
				StoryCredits.Close();
				Check(ref ok, !StoryCredits.IsOpen, "closed (Back to the sea, or Esc)");

				// Read back from the world file: not again; the notebook keeps the note
				List<string> lines = StoryChain.WriteLines().ToList();
				Check(ref ok, lines.Contains("@storycreditsshown=1") && lines.Any(l => l.StartsWith("@storyendtitle=The Last Lighthouse")) && lines.Any(l => l.StartsWith("@storycredits=")), "the world file keeps the title, the makers and 'shown'");
				StoryChain.Reset();
				foreach (string l in lines) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				StoryChain.OnWorldRead();
				QuestBook.Refresh();
				Check(ref ok, StoryChain.CreditsShown && StoryChain.EndingTitle == "The Last Lighthouse" && StoryChain.Credits == "Ann Builder\nBo Maker", "read back: the same title, makers and 'shown'");
				yield return new WaitForSecondsRealtime(2.5f);
				Check(ref ok, StoryCredits.TimesShown == times + 1 && !StoryCredits.IsOpen, "read back: the credits are not shown again");
				Check(ref ok, QuestBook.StoryOver && QuestBook.PageTexts.Values.Any(v => v.Any(t => t == "The sea is quiet now.\nThank you for sailing.")), "Raft's notebook keeps the closing note (" + QuestBook.Layout + ")");
			}
			finally
			{
				IslandNetwork.Loopback = null;
				StoryCredits.Close();
				List<int> made = IslandWorldState.Islands.Where(x => x.Rule == "rfa" || x.Rule == "rfb").Select(x => x.Id).ToList();
				if (made.Count > 0) IslandWorldState.RemoveIds(made, true);
				StoryChain.Reset();
				foreach (string l in linesBefore) { int eq = l.IndexOf('='); StoryChain.ReadLine(l.Substring(1, eq - 1), l.Substring(eq + 1)); }
				NoteBook.unlockedNoteBookIndexes.Clear(); NoteBook.unlockedNoteBookIndexes.AddRange(indexesBefore);
				NoteBook.unlockedChunkPointType.Clear(); NoteBook.unlockedChunkPointType.AddRange(unlockedBefore);
				if (freqBefore != null) RecieverFrequency.AllFrequencies = freqBefore;
				StoryChain.OnWorldRead();
				IslandWorldState.Save();
				QuestBook.Refresh();
			}
			if (ok) Log(shot ? "PASS: rf ending shot" : "PASS: rf ending"); else Fail(shot ? "rf ending shot" : "rf ending");
		}

		#endregion
	}
}
