using System.Collections;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Guide pictures that need a world set up first (ROADMAP D6): Raft's Receiver with the custom islands' green dots.
	/// </summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIReceiverShot", docs: "Dev, in game (host): a Receiver with its screen in front of the camera with its radar on, custom islands round the raft (up to 3 brought), and its picture shot_receiver.png for the guide. CIReceiverShot [turn degrees] [metres ahead] [metres down]")]
		public static void ReceiverShot(string[] args)
		{
			float turn = args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 0f;
			float ahead = args != null && args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0.5f;
			float down = args != null && args.Length > 2 ? float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0.05f;
			StartTest(ReceiverShotRoutine(turn, ahead, down));
		}

		static IEnumerator ReceiverShotRoutine(float turn, float ahead, float down)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("receiver shot: run in a world, as the host"); yield break; }
			// A few islands round the raft, so the radar has dots in more than one direction
			Vector3[] around = { new Vector3(250f, 0, 420f), new Vector3(-480f, 0, 120f), new Vector3(150f, 0, -520f) };
			string[] names = IslandSpawner.ListSavedIslands().Where(n => !n.StartsWith("ci")).Take(3).ToArray();
			for (int i = IslandWorldState.Islands.Count; i < 3 && i < names.Length; i++)
				yield return DynamicIslands.instance.SpawnIslandFile(names[i], raftPos.Value + around[i], true);
			Reciever prefab = null;
			foreach (Item_Base item in ItemManager.GetAllItems())
			{
				try
				{
					if (item == null || item.settings_buildable == null || !item.settings_buildable.Placeable) continue;
					Block[] blocks = item.settings_buildable.GetBlockPrefabs();
					prefab = blocks != null ? blocks.Where(b => b != null).Select(b => b.GetComponentInChildren<Reciever>(true)).FirstOrDefault(rc => rc != null) : null;
					if (prefab != null) break;
				}
				catch { }
			}
			if (prefab == null) { Fail("receiver shot: could not find Raft's receiver block"); yield break; }
			Transform cam = Camera.main.transform;
			Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
			GameObject go = UnityEngine.Object.Instantiate(prefab.transform.root.gameObject, cam.position + flat * 3f, Quaternion.identity);
			Reciever r = go.GetComponentInChildren<Reciever>(true);
			Transform screen = r.canvasSection != null ? r.canvasSection.transform : r.dotParent;
			// The screen (a small world-space canvas) faces the camera, ahead metres away and down metres below the eye
			go.transform.RotateAround(screen.position, Vector3.up, Vector3.SignedAngle(Vector3.ProjectOnPlane(screen.forward, Vector3.up), flat, Vector3.up) + turn);
			go.transform.position += cam.position + flat * ahead + Vector3.down * down - screen.position;
			cam.rotation = Quaternion.LookRotation(screen.position - cam.position);
			yield return null;
			// The screen is off without a charged battery: Raft's own update (which would switch it off again) stops, and
			// everything above the dots goes on
			r.enabled = false;
			if (r.radarSection != null) r.radarSection.SetActive(true);
			for (Transform t = r.dotParent; t != null && t != go.transform; t = t.parent) t.gameObject.SetActive(true);
			foreach (Canvas c in go.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
			for (int i = 0; i < 30; i++) { IslandRadar.Draw(r); cam.rotation = Quaternion.LookRotation(screen.position - cam.position); yield return null; }
			int dots = IslandRadar.DotsOf(r).Count(d => d != null && d.gameObject.activeSelf);
			Screenshot(new[] { "receiver" });
			yield return new WaitForSeconds(1f);
			UnityEngine.Object.Destroy(go);
			Log((dots > 0 ? "PASS" : "FAIL") + ": receiver shot with " + dots + " green dot(s)");
		}

		[ConsoleCommand(name: "CILevelTagShot", docs: "Dev, in game (two players, the level up system on and the other player past level 1): this player stands in front of the other one and takes shot_level_tag.png, the \"Lv n\" under their name, for the guide. CILevelTagShot [metres away]")]
		public static void LevelTagShot(string[] args)
		{
			if (args != null && args.Length > 0 && args[0] == "pose") { StartTest(LevelTagPoseRoutine()); return; }
			float away = args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 3f;
			StartTest(LevelTagShotRoutine(away));
		}

		// CILevelTagShot pose (the other player): held standing on the middle of the raft for 25 s, for the picture
		static IEnumerator LevelTagPoseRoutine()
		{
			Network_Player me = RAPI.GetLocalPlayer();
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (me == null || !raftPos.HasValue) { Fail("level tag pose: in a world"); yield break; }
			Vector3 at = raftPos.Value + Vector3.up * 1.1f;
			Log("PASS: level tag pose at " + at);
			float end = Time.time + 25f;
			while (Time.time < end)
			{
				me.transform.position = at;
				me.Stats.stat_health.Value = me.Stats.stat_health.Max;
				me.Stats.stat_oxygen.Value = me.Stats.stat_oxygen.Max;
				yield return null;
			}
		}

		static IEnumerator LevelTagShotRoutine(float away)
		{
			Network_Player me = RAPI.GetLocalPlayer();
			Network_Player other = Players.All.FirstOrDefault(p => p != null && p != me && p.playerNameTextMesh != null);
			if (me == null || other == null) { Fail("level tag shot: needs a second player"); yield break; }
			Vector3 at = other.transform.position;
			Transform cam = Camera.main.transform;
			// (this player stands where its camera already faces the other one: Raft's mouse look keeps its own turn)
			Vector3 dir = -Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
			if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
			Log("level tag shot: the other player at " + at + ", this one at " + me.transform.position);
			for (int i = 0; i < 40; i++)
			{
				// (held there: off the raft's edge this player would sink)
				me.transform.position = at + dir * away;
				me.Stats.stat_health.Value = me.Stats.stat_health.Max;
				me.Stats.stat_oxygen.Value = me.Stats.stat_oxygen.Max;
				// (between the name and the chest: the tag, the name and the player all in the picture)
				cam.rotation = Quaternion.LookRotation(other.playerNameTextMesh.transform.position + Vector3.down * 0.7f - cam.position);
				yield return null;
			}
			Renderer body = other.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r is SkinnedMeshRenderer).FirstOrDefault();
			Log("level tag shot: camera " + cam.position + " looking " + cam.forward + ", name at " + other.playerNameTextMesh.transform.position + ", body " + (body != null ? body.bounds.center.ToString() : "none shown"));
			string tag;
			bool shown = LevelTags.Shown.TryGetValue(other.steamID.Id, out tag);
			Screenshot(new[] { "level_tag" });
			yield return new WaitForSeconds(1f);
			Log((shown ? "PASS" : "FAIL") + ": level tag shot (" + (shown ? tag : "no tag shown") + ")");
		}

		[ConsoleCommand(name: "CIGiantShot", docs: "Dev, in game (host): a giant rogue shark (RogueShark giant) and a plain shark side by side under water in front of this player, and their picture shot_giant.png. CIGiantShot [metres away] [metres apart]")]
		public static void GiantShot(string[] args)
		{
			float away = args != null && args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 16f;
			float apart = args != null && args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 9f;
			StartTest(GiantShotRoutine(away, apart));
		}

		static IEnumerator GiantShotRoutine(float away, float apart)
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Host_Entities host = null;
			try { host = ComponentManager<Network_Host_Entities>.Value; } catch { }
			if (!raftPos.HasValue || !Raft_Network.IsHost || host == null) { Fail("giant shot: run in a world, as the host"); yield break; }
			AI_NetworkBehavior_Shark giant = RogueShark.Find();
			if (giant == null || !RogueShark.Giant) giant = RogueShark.Spawn(true);
			AI_NetworkBehavior_Shark plain = LiveSharks().FirstOrDefault(s => s != giant && !RogueShark.IsRogue(s));
			if (plain == null) plain = host.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, raftPos.Value + new Vector3(30f, -8f, 0f), null) as AI_NetworkBehavior_Shark;
			if (giant == null || plain == null) { Fail("giant shot: no giant or no plain shark"); yield break; }
			Network_Player me = RAPI.GetLocalPlayer();
			Transform cam = Camera.main.transform;
			// This player under water off the raft's side; the two sharks ahead of them, side on
			Vector3 eye = raftPos.Value + new Vector3(0f, -4f, -25f);
			Vector3 ahead = Vector3.back;
			Vector3 side = Vector3.Cross(Vector3.up, ahead).normalized;
			// (the sharks still: their AI and bodies would swim off again before the picture is taken at the frame's end)
			giant.enabled = false;
			plain.enabled = false;
			foreach (Rigidbody rb in giant.GetComponentsInChildren<Rigidbody>().Concat(plain.GetComponentsInChildren<Rigidbody>())) rb.isKinematic = true;
			for (int i = 0; i < 70; i++)
			{
				if (i == 50)
				{
					Log("giant shot: eye " + cam.position + ", giant at " + giant.transform.position + " body " + WorldRandomizer.BodyOf(giant).Select(r => r.bounds.center.ToString()).FirstOrDefault()
						+ ", plain at " + plain.transform.position + " body " + WorldRandomizer.BodyOf(plain).Select(r => r.bounds.center.ToString()).FirstOrDefault());
					Screenshot(new[] { "giant" });
				}
				me.transform.position = eye;
				// (kept alive: no air down there, and the sharks bite)
				me.Stats.stat_health.Value = me.Stats.stat_health.Max;
				me.Stats.stat_oxygen.Value = me.Stats.stat_oxygen.Max;
				giant.transform.position = eye + ahead * away + side * apart * 0.5f + Vector3.down * 1.5f;
				giant.transform.rotation = Quaternion.LookRotation(-side);
				plain.transform.position = eye + ahead * away - side * apart * 0.5f;
				plain.transform.rotation = Quaternion.LookRotation(side);
				cam.rotation = Quaternion.LookRotation(eye + ahead * away + Vector3.down * 1f - cam.position);
				yield return null;
			}
			float plainSize = WorldRandomizer.BodyOf(plain).Select(r => r.bounds.size.magnitude).DefaultIfEmpty(0f).First();
			float giantSize = WorldRandomizer.BodyOf(giant).Select(r => r.bounds.size.magnitude).DefaultIfEmpty(0f).First();
			giant.enabled = true;
			plain.enabled = true;
			foreach (Rigidbody rb in giant.GetComponentsInChildren<Rigidbody>().Concat(plain.GetComponentsInChildren<Rigidbody>())) rb.isKinematic = false;
			Log("PASS: giant shot (" + (plainSize > 0f ? (giantSize / plainSize).ToString("F2") : "?") + "x: " + plainSize.ToString("F1") + " -> " + giantSize.ToString("F1") + ")");
		}
	}
}
