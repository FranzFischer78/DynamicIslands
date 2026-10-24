using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using HMLLibrary;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Monster size (2026-10-16): a bear at size x3, one at x1 and one whose file says x5 (clamped to x3) - body and hit box
	/// three times as big, health the same, and the big one still reaches the player; a picture of it. CB16: the "?" on the
	/// "Brand-new generated islands" row of the World settings' island list, with its popup in a picture.
	/// </summary>
	public static partial class DevTests
	{
		const string SizeIsland = "cisize";

		[ConsoleCommand(name: "CISizeBears", docs: "Dev, in game (host): spawns 'cisize' with bears at size x1, x3 and x5 (clamped to x3); checks their size, hit box, health and that the x3 bear hurts the player; picture shot_size_bear.png")]
		public static void SizeBearsCommand() { StartTest(SizeBearsRoutine()); }

		static IEnumerator SizeBearsRoutine()
		{
			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			// (a flat bare island of its own: generated_sample is no longer in Mods)
			var gs = new IslandGenSettings { Seed = 4242, Radius = 40f, Height = 4f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandFile f = IslandGenerator.CreateFile(gs, SizeIsland);
			f.Elevation = 0f;
			Vector2 c = IslandSpawner.LandCentre(f);
			int res = f.HeightmapResolution;
			float step = f.TerrainSize.x / (res - 1);
			Func<float, float, float> h = (x, z) => f.Heights[Mathf.Clamp(Mathf.RoundToInt(z / step), 0, res - 1), Mathf.Clamp(Mathf.RoundToInt(x / step), 0, res - 1)] * f.TerrainSize.y;
			f.Objects.RemoveAll(o => new Vector2(o.Position.x - c.x, o.Position.z - c.y).magnitude < 30f || o.Name.StartsWith("Creature_"));
			Action<float, float, string> bear = (dx, dz, size) => f.Objects.Add(new IslandObject { Name = "Creature_Bear", Position = new Vector3(c.x + dx, h(c.x + dx, c.y + dz), c.y + dz),
				Props = P(ObjectProps.CreatureCount, "1", ObjectProps.CreatureSize, size) });
			bear(14f, 0f, "3");
			bear(-14f, 0f, "1");
			bear(0f, -16f, "5");
			f.Save(IslandSpawner.PathFor(SizeIsland));
			IslandWorldState.Entry old = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == SizeIsland);
			if (old != null) IslandWorldState.RemoveIds(new[] { old.Id }, true);
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raftPos.Value, CustomIslandSpawner.LandRadius(SizeIsland), 150f);
			if (!spot.HasValue) { Fail("no open sea near the raft"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(SizeIsland, spot.Value, true);
			IslandWorldState.Entry entry = IslandWorldState.Islands.FirstOrDefault(e => e.HostName == SizeIsland && e.Root != null);
			if (entry == null) { Fail("'" + SizeIsland + "' did not load"); yield break; }
			PutPlayerNear(entry.Root.transform, 0f);

			// The three spots' bears (each spot spawns its own)
			List<CreatureSpawnPoint> spots = new List<CreatureSpawnPoint>();
			for (float t = 0f; t < 40f; t += 1f)
			{
				spots = entry.Root.GetComponentsInChildren<CreatureSpawnPoint>(true).Where(p => p.Kind != null && p.Kind.Type == AI_NetworkBehaviourType.Bear).ToList();
				if (spots.Count == 3 && spots.All(p => p.Spawned.Any(a => a != null))) break;
				yield return new WaitForSeconds(1f);
			}
			Func<string, AI_NetworkBehaviour> of = s => spots.Where(p => { string v; return p.Props.TryGetValue(ObjectProps.CreatureSize, out v) && v == s; }).SelectMany(p => p.Spawned).FirstOrDefault(a => a != null);
			AI_NetworkBehaviour big = of("3"), small = of("1"), clamped = of("5");
			if (big == null || small == null || clamped == null) { Fail("the bears did not spawn (" + spots.Count + " spots; x3 " + (big != null) + ", x1 " + (small != null) + ", x5 " + (clamped != null) + ")"); yield break; }
			bool ok = true;
			Func<AI_NetworkBehaviour, Vector3> box = a =>
			{
				Collider[] cs = a.GetComponentsInChildren<Collider>().Where(x => x.enabled && !x.isTrigger).ToArray();
				if (cs.Length == 0) return Vector3.zero;
				Bounds b = cs[0].bounds;
				foreach (Collider x in cs) b.Encapsulate(x.bounds);
				return b.size;
			};
			float sBig = big.transform.localScale.x, sSmall = small.transform.localScale.x, sClamp = clamped.transform.localScale.x;
			Vector3 bBig = box(big), bSmall = box(small);
			float hBig = big.networkEntity.stat_health.Max, hSmall = small.networkEntity.stat_health.Max;
			Log("SIZE x3 " + Gf(sBig, "F3") + ", x1 " + Gf(sSmall, "F3") + ", x5 " + Gf(sClamp, "F3") + "; hit box x3 " + Gf(bBig.y) + " m high, x1 " + Gf(bSmall.y) + " m; health " + Gf(hBig, "F0") + " / " + Gf(hSmall, "F0"));
			// (Raft gives each bear a random size of its own - the prefab's localScaleInterval - and the spot's size multiplies it: two
			// bears' x3 and x1 came out 3.15 apart, RUN1 2026-10-21. So each is checked against Raft's range times its own size, and
			// the hit box against the two bears' real sizes)
			float lo = 1f, hi = 1f;
			Network_Host_Entities hostE = ComponentManager<Network_Host_Entities>.Value;
			AI_NetworkBehaviour prefab = hostE != null && hostE.AINetworkBehaviourPrefabs != null ? hostE.AINetworkBehaviourPrefabs.FirstOrDefault(b => b != null && b.behaviourType == AI_NetworkBehaviourType.Bear) : null;
			try { if (prefab != null && prefab.localScaleInterval.maxValue > 0f) { lo = Mathf.Min(prefab.localScaleInterval.minValue, prefab.localScaleInterval.maxValue); hi = Mathf.Max(prefab.localScaleInterval.minValue, prefab.localScaleInterval.maxValue); } } catch { }
			if (lo <= 0f) lo = hi;
			Func<float, float, bool> inRange = (sc, k) => sc >= k * lo - 0.02f && sc <= k * hi + 0.02f;
			Func<float, string> range = k => Gf(k * lo, "F3") + "-" + Gf(k * hi, "F3");
			Log("  (Raft's own bear sizes " + range(1f) + (prefab == null ? ", no prefab found: 1" : "") + ")");
			Check(ref ok, inRange(sBig, 3f) && inRange(sSmall, 1f), "the x3 bear is three times Raft's size, the x1 one Raft's own (x3 " + Gf(sBig, "F3") + " in " + range(3f) + ", x1 " + Gf(sSmall, "F3") + " in " + range(1f) + ")");
			Check(ref ok, inRange(sClamp, 3f), "a file's x5 comes out as x3 (" + Gf(sClamp, "F3") + " in " + range(3f) + ", not " + range(5f) + ")");
			Check(ref ok, bSmall.y > 0.1f && sSmall > 0f && Mathf.Abs(bBig.y / bSmall.y / (sBig / sSmall) - 1f) < 0.15f, "its hit box grows with it (" + Gf(bSmall.y > 0f ? bBig.y / bSmall.y : 0f) + "x for " + Gf(sSmall > 0f ? sBig / sSmall : 0f) + "x the size)");
			Check(ref ok, Mathf.Abs(hBig - hSmall) < 0.5f, "its health stays as it was (" + Gf(hBig, "F0") + " and " + Gf(hSmall, "F0") + ")");

			// A picture: the big bear from the front, a few metres off
			Vector3 at = big.transform.position + Vector3.up * 2.2f;
			Vector3 fwd = Flat(big.transform.forward).sqrMagnitude > 0.01f ? Flat(big.transform.forward).normalized : Vector3.forward;
			yield return CameraShot(at + fwd * 9f + Vector3.up * 1.5f + Vector3.Cross(Vector3.up, fwd) * 3f, at, "size_bear");

			// It still reaches the player: next to it, the player's health drops while the x3 bear is the nearest
			Network_Player me = RAPI.GetLocalPlayer();
			bool hurt = false;
			string near = "";
			for (float t = 0f; t < 30f && !hurt; t += 0.25f)
			{
				if (t == 0f || t % 5f == 0f) { PutPlayerNear(big.transform, 2.5f); yield return null; }
				float before = me.Stats.stat_health.Value;
				yield return new WaitForSeconds(0.25f);
				if (big == null || big.networkEntity == null || big.networkEntity.IsDead) break;
				if (me.Stats.stat_health.Value < before - 0.01f)
				{
					AI_NetworkBehaviour nearest = new[] { big, small, clamped }.Where(a => a != null).OrderBy(a => (a.transform.position - me.transform.position).sqrMagnitude).First();
					near = (nearest == big ? "the x3 bear" : nearest == small ? "the x1 bear" : "the x5 bear") + " " + Gf((nearest.transform.position - me.transform.position).magnitude, "F1") + " m off";
					hurt = nearest == big || nearest == clamped;
				}
				KeepAlive(me);
			}
			Check(ref ok, hurt, "the x3 bear hurts the player next to it" + (near.Length > 0 ? " (" + near + ")" : ""));
			KeepAlive(me);
			Log(ok ? "PASS: size bears" : "FAIL: size bears");
		}

		[ConsoleCommand(name: "CIGeneratedHelp", docs: "Dev, main menu (CB16): World settings > Islands while sailing: the 'Brand-new generated islands' row has a ? with the full text (how often, where saved, how to keep one); its popup opens on hover; picture shot_cb16_generated.png")]
		public static void GeneratedHelpCommand() { StartTest(GeneratedHelpRoutine()); }

		static IEnumerator GeneratedHelpRoutine()
		{
			NewGameBox newGame = Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid());
			if (newGame == null) { Fail("no New Game box (main menu?)"); yield break; }
			bool ok = true;
			newGame.gameObject.SetActive(true);
			try { newGame.Close(); } catch { } newGame.Open();
			yield return new WaitForSecondsRealtime(1f);
			WorldSettingsWindow.Open();
			yield return null;
			Button open = IslandPickerWindow.EntryButton;
			if (open == null) { Fail("no 'Islands while sailing' button"); yield break; }
			open.onClick.Invoke();
			yield return null;
			Button row = IslandPickerWindow.Row(CustomIslandSpawner.GeneratedEntry);
			Check(ref ok, row != null, "the list has the generated row");
			UIKit.HelpMark mark = row != null ? row.GetComponentsInChildren<UIKit.HelpMark>(true).FirstOrDefault() : null;
			Check(ref ok, mark != null && mark.Text == WorldIslands.GeneratedHelp, "the row has a ? with the full text");
			string help = WorldIslands.GeneratedHelp;
			Check(ref ok, help.Contains("gen-") && help.Contains("Save as") && help.Contains("Mods") && help.Contains("3-6"), "the text says where they are saved, how often and how to keep one");
			if (mark != null)
			{
				bool tickedBefore = IslandPickerWindow.IsTicked(CustomIslandSpawner.GeneratedEntry);
				Canvas.ForceUpdateCanvases();
				Rect r = ScreenRect((RectTransform)mark.transform), rowRect = ScreenRect((RectTransform)row.transform);
				Check(ref ok, r.width >= 10f && rowRect.Contains(r.center), "the ? sits on the row");
				mark.OnPointerEnter(null);
				yield return new WaitForSecondsRealtime(0.3f);
				Check(ref ok, UIKit.ShownHelp == help, "hovering it opens the text");
				Screenshot(new[] { "cb16_generated" });
				yield return new WaitForSecondsRealtime(0.6f);
				mark.OnPointerExit(null);
				yield return null;
				Check(ref ok, UIKit.ShownHelp == null, "moving off it closes the popup");
				Check(ref ok, IslandPickerWindow.IsTicked(CustomIslandSpawner.GeneratedEntry) == tickedBefore, "the row's tick is as it was");
			}
			IslandPickerWindow.Close();
			WorldSettingsWindow.Close();
			try { newGame.Close(); } catch { }
			Log(ok ? "PASS: generated help" : "FAIL: generated help");
		}
	}
}
