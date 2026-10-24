using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIRadarNeeds", docs: "Dev, in game (host, a fresh world without the engine): SL9 - a plan rule's 'needs=engine' WHERE part: the line kept; without the engine blueprint its island comes straight ahead (even a 'near' rule) and its Receiver dot is hidden; with it the dot shows and a 'near' rule places to the side")]
		public static void RadarNeedsCommand(string[] args) { StartTest(RadarNeedsRoutine()); }

		static IEnumerator RadarNeedsRoutine()
		{
			Network_Player me = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (me == null || !raft.HasValue || !Raft_Network.IsHost) { Fail("run in a world, as the host"); yield break; }
			const string bp = "Blueprint_MotorWheel";
			if (BlueprintSteps.Has(bp)) { Fail("this player has the engine blueprint already: use a fresh world"); yield break; }
			bool ok = true;
			// (the line: written with a short name, read back to the blueprint; old lines read as before)
			IntroRule a = IntroRule.Parse("gated | island:ciradarneeds | start | near:nowhere:900:west:needs=engine | | Gated");
			IntroRule b = IntroRule.Parse("plain | island:ciradarneeds | start | ahead:400 | | ");
			Check(ref ok, a != null && a.Needs == bp && a.Where == "near" && a.WhereRef == "nowhere" && a.Distance == 900f && a.Direction == "west", "needs=engine read: " + (a != null ? a.Needs + ", " + a.Where + ":" + a.WhereRef + ":" + a.Distance + ":" + a.Direction : "null"));
			Check(ref ok, a != null && a.ToLine().Contains("near:nowhere:900:west:needs=engine") && IntroRule.Parse(a.ToLine()).Needs == bp, "written back: " + (a != null ? a.ToLine() : ""));
			Check(ref ok, b != null && b.Needs == "" && !b.ToLine().Contains("needs="), "a rule without it: no needs part");
			Check(ref ok, a != null && a.DescribeWhere().Contains("Motor Wheel"), "described: " + (a != null ? a.DescribeWhere() : ""));
			yield return EnsureAlive();
			const string name = "ciradarneeds";
			var s = new IslandGenSettings { Seed = 8994, Radius = 25f, Height = 5f, Trees = 0f, Bushes = 0f, Rocks = 0f, Harvest = 0f, BeachThings = 0f, Water = 0f, SeaRocks = 0f, SeaFinds = 0f, Sunken = 0f };
			IslandGenerator.CreateFile(s, name).Save(IslandSpawner.PathFor(name));
			IslandWorldState.Remove(name);
			WorldPlan keep = WorldDirector.Plan;
			IntroRule side = IntroRule.Parse("sided | island:ciradarneeds | start | near:gated:700:east:needs=engine | | Sided");
			WorldDirector.Plan = new WorldPlan { Name = "CI radar needs", Random = true, Rules = { a, side } };
			GameObject recv = null;
			try
			{
				// (without the engine: straight ahead of the raft, not west of an island that isn't there)
				Vector3 sail = CustomIslandSpawner.SailDirection();
				string why = WorldDirector.Bring(a, null, null);
				Check(ref ok, why == null, "the gated 'near' rule brings its island without the engine (" + (why ?? "on its way") + ")");
				IslandWorldState.Entry e = null;
				for (float t = 0f; t < 30f && (e == null || e.Root == null); t += 0.5f) { e = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name && i.Rule == "gated"); yield return new WaitForSeconds(0.5f); }
				if (e == null) { Fail("the gated island did not come"); yield break; }
				Vector3 to = e.Position - raft.Value;
				float off = Vector2.Angle(new Vector2(sail.x, sail.z), new Vector2(to.x, to.z));
				Check(ref ok, off <= 20f, "it came straight ahead: " + Gf(off, "F0") + " deg off the way the raft sails, " + Gf(new Vector2(to.x, to.z).magnitude, "F0") + " m");
				Check(ref ok, ReturningIslands.NeedsOf(e) == bp, "its rule is found: needs " + ReturningIslands.NeedsOf(e));
				// (the Receiver: no dot until the player has the engine blueprint)
				Reciever r = MakeReceiver(me, out recv);
				if (r == null) { Fail("could not find Raft's receiver block"); yield break; }
				yield return new WaitForSeconds(1.2f);
				IslandRadar.Draw(r);
				Check(ref ok, !IslandRadar.Shown.Contains(e), "without the engine: no dot (" + IslandRadar.Shown.Count + " dot(s) for other islands)");
				me.Inventory.AddItem(bp, 1);
				yield return new WaitForSeconds(1.2f);
				IslandRadar.Draw(r);
				int at = IslandRadar.Shown.IndexOf(e);
				List<Reciever_Dot> dots = IslandRadar.DotsOf(r);
				Check(ref ok, at >= 0 && at < dots.Count && dots[at] != null && dots[at].gameObject.activeSelf && dots[at].dotNumberText != null && dots[at].dotNumberText.text.StartsWith("Gated"),
					"with the engine blueprint: its dot shows (" + (at >= 0 && at < dots.Count && dots[at] != null && dots[at].dotNumberText != null ? dots[at].dotNumberText.text : "none") + ")");
				// (with the engine: a 'near' rule goes to the side, as WHERE says)
				why = WorldDirector.Bring(side, null, null);
				IslandWorldState.Entry f = null;
				for (float t = 0f; t < 30f && (f == null || f.Root == null); t += 0.5f) { f = IslandWorldState.Islands.LastOrDefault(i => i.HostName == name && i.Rule == "sided"); yield return new WaitForSeconds(0.5f); }
				if (f == null) Check(ref ok, false, "the sided island did not come (" + (why ?? "") + ")");
				else
				{
					Vector3 d = f.Position - e.Position;
					float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
					Check(ref ok, Mathf.Abs(Mathf.DeltaAngle(bearing, 90f)) <= 50f, "with the engine: placed east of the first island (bearing " + Gf(bearing, "F0") + ", " + Gf(new Vector2(d.x, d.z).magnitude, "F0") + " m)");
				}
			}
			finally
			{
				WorldDirector.Plan = keep;
				if (recv != null) UnityEngine.Object.Destroy(recv);
				me.Inventory.RemoveItem(bp, 1);
				IslandWorldState.Remove(name);
			}
			if (ok) Log("PASS: radar needs"); else Fail("radar needs");
		}

		/// <summary>A Receiver (Raft's block) above the player with its radar on.</summary>
		static Reciever MakeReceiver(Network_Player me, out GameObject go)
		{
			go = null;
			Reciever prefab = null;
			foreach (Item_Base item in ItemManager.GetAllItems())
			{
				try
				{
					if (item == null || item.settings_buildable == null || !item.settings_buildable.Placeable) continue;
					Block[] blocks = item.settings_buildable.GetBlockPrefabs();
					prefab = blocks != null ? blocks.Where(x => x != null).Select(x => x.GetComponentInChildren<Reciever>(true)).FirstOrDefault(x => x != null) : null;
					if (prefab != null) break;
				}
				catch { }
			}
			if (prefab == null) return null;
			go = UnityEngine.Object.Instantiate(prefab.transform.root.gameObject, me.transform.position + Vector3.up * 3f, Quaternion.identity);
			Reciever r = go.GetComponentInChildren<Reciever>(true);
			if (r != null && r.radarSection != null) r.radarSection.SetActive(true);
			return r;
		}
	}
}
