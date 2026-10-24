using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CITreasureProbe", docs: "Dev, in game (host): puts Raft's own buried treasure (TreasurePointManager) on a small custom island and looks at it - for metal detector treasure on custom islands")]
		public static void TreasureProbeCommand(string[] args) { StartTest(TreasureProbeRoutine()); }

		static IEnumerator TreasureProbeRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (player == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			IslandWorldState.Remove(DirtHoneyIsland);
			MakeDirtHoneyIsland().Save(IslandSpawner.PathFor(DirtHoneyIsland));
			Vector3? spot = CustomIslandSpawner.FindClearSpot(raft.Value, CustomIslandSpawner.LandRadius(DirtHoneyIsland), 450f);
			if (!spot.HasValue) { Fail("no open sea"); yield break; }
			yield return DynamicIslands.instance.SpawnIslandFile(DirtHoneyIsland, spot.Value, true);
			IslandWorldState.Entry e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == DirtHoneyIsland);
			if (e == null || e.Root == null) { Fail("no island"); yield break; }
			yield return new WaitForSeconds(1f);
			TreasurePointManager tm = UnityEngine.Object.FindObjectOfType<TreasurePointManager>();
			Log("treasure: manager " + (tm != null));
			if (tm == null) yield break;
			// A spot on the land near the middle
			Vector3 at = e.Root.GetComponentsInChildren<PickupItem_Networked>(true).First(p => p.name.Contains("Dirt")).transform.position + new Vector3(3f, 0f, 3f);
			RaycastHit hit;
			if (Physics.Raycast(at + Vector3.up * 50f, Vector3.down, out hit, 100f, LayerMasks.MASK_GroundMask)) at = hit.point;
			Log("treasure: at " + at + " ground hit " + (hit.collider != null ? hit.collider.name : "-"));
			try { tm.AddTreasurePointNetworked(e.Root.transform, at, 0); }
			catch (Exception ex) { Log("treasure: add threw " + ex); }
			yield return new WaitForSeconds(1f);
			const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var dict = (Dictionary<Transform, List<TreasurePoint>>)typeof(TreasurePointManager).GetField("treasurePoints", all).GetValue(tm);
			foreach (var kv in dict) Log("treasure: parent " + (kv.Key != null ? kv.Key.name : "null") + " has " + kv.Value.Count);
			TreasurePoint first = dict.Where(kv => kv.Key == e.Root.transform).SelectMany(kv => kv.Value).FirstOrDefault();
			if (first != null) Log("treasure: ours at " + first.transform.position + " local " + first.transform.localPosition + " parent " + (first.transform.parent != null ? first.transform.parent.name : "-") + " active " + first.gameObject.activeInHierarchy);
			float d;
			TreasurePoint tp = tm.GetClosestTreasurePointTo(at, 10f, true, true, out d) ?? first;
			Log("treasure: closest " + (tp != null ? tp.name + " at " + tp.transform.position + " d=" + d : "none"));
			if (tp != null)
			{
				foreach (Transform t in tp.GetComponentsInChildren<Transform>(true))
					Log("treasure:   " + t.name + " tag=" + t.tag + " layer=" + LayerMask.LayerToName(t.gameObject.layer) + " active=" + t.gameObject.activeSelf + " comps: " + string.Join(", ", t.GetComponents<Component>().Select(c => c != null ? c.GetType().Name : "-").ToArray()));
				foreach (FieldInfo f in typeof(TreasurePoint).GetFields(all)) { try { Log("treasure:   TreasurePoint." + f.Name + " = " + Show(f.GetValue(tp))); } catch { } }
				foreach (MethodInfo m in typeof(TreasurePoint).GetMethods(all | BindingFlags.DeclaredOnly)) Log("treasure:   method " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name).ToArray()) + ")");
			}
			Log("treasure probe done");
		}
	}
}
