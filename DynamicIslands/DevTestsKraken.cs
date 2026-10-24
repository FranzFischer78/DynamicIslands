using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Tests of the world option Kraken arm (Kraken).</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIKraken", docs: "Dev, world (host, single player): the option Kraken arm - its option lists; the day roll (about 1 in 8 from day 8, never near land; the free arms swipe), storm roll (1 in 4) and neighbour roll (1 in 2); on a raft grown to 5x5 an arm grabs an EDGE floor (never a middle one) with 1-3 more arms 3-8 m out and its base deep under the sea, the rise message (kind 37) sent; a miss doesn't count; 10 hits make it let go with every floor kept; a short stay makes it tear the grabbed floor plus exactly the neighbours its seed says; the option off sends an arm away. Pictures play_kraken_side/_top/_torn.jpg. Raft and options put back after (torn floors stay torn)")]
		public static void KrakenTestCommand() { StartTest(KrakenRoutine()); }

		static IEnumerator KrakenWait(float s) { for (float t = Time.time + s; Time.time < t;) yield return null; }

		/// <summary>Grows the raft to 5 x 5 floors around its middle (cheat placement: the host's goes to every player).</summary>
		internal static bool KrakenGrowRaft(Network_Player me, List<Block> made)
		{
			List<Block> start = StormSeas.Foundations();
			if (start.Count == 0) return false;
			Vector3 o = start[0].transform.localPosition;
			var taken = new HashSet<Vector2Int>(start.Select(b => StormSeas.CellOf(b.transform.localPosition, o)));
			int cx = Mathf.RoundToInt((float)taken.Average(c => c.x)), cz = Mathf.RoundToInt((float)taken.Average(c => c.y));
			Item_Base foundation = ItemManager.GetItemByName("Block_Foundation");
			for (int i = -2; i <= 2; i++)
				for (int j = -2; j <= 2; j++)
				{
					if (taken.Contains(new Vector2Int(cx + i, cz + j))) continue;
					Block b = null;
					try { b = me.BlockCreator.CreateBlockCheat(foundation, o + new Vector3((cx + i) * HeadStart.Cell, 0f, (cz + j) * HeadStart.Cell), Vector3.zero, DPS.Default, 0); } catch (Exception e) { Log("  (placing failed: " + e.Message + ")"); }
					if (b != null) made.Add(b);
				}
			return true;
		}

		static IEnumerator KrakenRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("kraken: host, in a world"); yield break; }
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			var made = new List<Block>();
			var sent = new List<IslandNetMessage>();
			Network_Player me = RAPI.GetLocalPlayer();
			if (me == null) { Fail("kraken: no player"); yield break; }
			Kraken.Reset();
			try
			{
				// 1. The option
				int at = Array.IndexOf(WorldOptions.All, WorldOptions.Kraken);
				Check(ref ok, at >= 0 && WorldOptions.Labels.Length == WorldOptions.All.Length && WorldOptions.Hints.Length == WorldOptions.All.Length && WorldOptions.Shorts.Length == WorldOptions.All.Length
					&& WorldOptions.Labels[at] == "Kraken arm" && WorldOptions.Hints[at].IndexOf("1 in 2", StringComparison.Ordinal) >= 0, "the option 'kraken' is listed with its label, hint and short text (at " + at + ")");
				WorldOptions.Set(new HashSet<string>(optionsBefore) { WorldOptions.Kraken });
				Check(ref ok, Kraken.On, "the option is on");
				int seed = WorldOptions.Seed;

				// 2. The rolls
				int days = Enumerable.Range(Kraken.FirstDay, 6000).Count(d => Kraken.Rolls(seed, d));
				bool early = Enumerable.Range(0, Kraken.FirstDay).Any(d => Kraken.Rolls(seed, d));
				int storms = Enumerable.Range(1, 2000).Count(n => Kraken.StormRolls(seed, n));
				int sides = Enumerable.Range(1, 1000).Sum(q => Enumerable.Range(0, 4).Count(i => Kraken.NeighbourGoes(q * 7919, i)));
				Check(ref ok, days >= 660 && days <= 840 && !early, "days with an arm: " + days + " of 6000 (about 750), none before day " + Kraken.FirstDay + ": " + !early);
				Check(ref ok, storms >= 440 && storms <= 560, "storms with an arm: " + storms + " of 2000 (about 500)");
				Check(ref ok, sides >= 1850 && sides <= 2150, "neighbours that go: " + sides + " of 4000 (about 2000)");
				int[] hues = new int[3];
				for (int n = 0; n < 4000; n++) hues[Kraken.Colouring(WorldOptions.Mix(seed, n, 1))]++;
				Check(ref ok, hues[0] >= 2850 && hues[0] <= 3150 && hues[1] >= 400 && hues[1] <= 600 && hues[2] >= 400 && hues[2] <= 600,
					"colourings of 4000 arms: purple " + hues[0] + " (about 3000), green " + hues[1] + ", blue " + hues[2] + " (about 500 each)");
				// a whole sighting standing by the free arms (3 arms, every swipe landing for the full stay): never deadly
				foreach (float start in new[] { 100f, 40f, 26f })
					for (int col = 0; col < 3; col++)
					{
						float hp = start, lostAll = 0f;
						int swipes = 3 * (int)Math.Ceiling(Kraken.StaySeconds / Kraken.SwipeEvery);
						for (int n = 0; n < swipes; n++) { float h = Kraken.SwipeHit(col, lostAll, hp); hp -= h; lostAll += h; }
						bool fine = lostAll <= Kraken.MaxTaken + 0.01f && (start <= Kraken.KeepHealth || hp >= Kraken.KeepHealth - 0.01f);
						if (!fine || start == 100f) Check(ref ok, fine, Kraken.ColouringNames[col] + " kraken, " + swipes + " swipes on a player with " + start + " health: loses " + lostAll.ToString("F1") + ", keeps " + hp.ToString("F1") + " (first swipe " + Kraken.SwipeHit(col, 0f, 100f).ToString("F1") + ")");
					}

				// 3. A raft of 5 x 5 floors
				if (!KrakenGrowRaft(me, made)) { Fail("kraken: no foundation on the raft"); yield break; }
				yield return KrakenWait(0.5f);
				List<Block> edges = StormSeas.EdgeFoundations(), middle = StormSeas.MiddleFoundations();
				int floors = StormSeas.Foundations().Count;
				Check(ref ok, floors >= 25 && middle.Count >= 9, "the raft: " + floors + " floors, " + edges.Count + " on the edge, " + middle.Count + " in the middle");

				// 4. An arm grabs an edge floor
				IslandNetwork.Loopback = sent.Add;
				Kraken.TestStay = 60f;
				string said = Kraken.Bring();
				Log("bring: " + said);
				Kraken.Arm arm = Kraken.Current;
				Check(ref ok, Kraken.Active && arm != null && arm.Block != null, "an arm is out");
				if (arm == null) yield break;
				Check(ref ok, edges.Contains(arm.Block) && !middle.Contains(arm.Block), "it grabbed an edge floor (" + arm.BlockIndex + "), not a middle one");
				var grid = StormSeas.Grid(StormSeas.Foundations());
				var cell = grid.First(kv => kv.Value == arm.Block).Key;
				Check(ref ok, !grid.ContainsKey(cell + StormSeas.Sides[arm.SideIndex]), "it comes from open water (side " + arm.SideIndex + ")");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Kraken && m.Name == "rise" && m.Index == arm.Number), "the rise message went to every player (kind " + IslandNetMessage.Kraken + ")");
				IslandNetMessage join = Kraken.Message();
				Check(ref ok, join != null && join.Name == "rise" && join.Data.StartsWith(arm.BlockIndex + ";"), "a player joining now gets the arm: " + (join != null ? join.Data : "null"));
				Transform pivot = arm.transform.parent;
				Vector2Int sv = StormSeas.Sides[arm.SideIndex];
				Vector3 u = new Vector3(sv.x, 0f, sv.y), s = Vector3.Cross(Vector3.up, u);
				Vector3 E = arm.Block.transform.localPosition + u * 0.75f;
				float baseY = arm.Others.Concat(new[] { arm.Main }).Max(t => pivot.TransformPoint(t.Ctrl[0]).y);
				var dists = arm.Others.Select(t => { Vector3 d = t.Ctrl[3] - E; return Vector3.Dot(d, u); }).ToList();
				Check(ref ok, arm.Extras >= 1 && arm.Extras <= 3 && arm.Others.Count == arm.Extras, "more arms (looks only): " + arm.Extras);
				Check(ref ok, dists.All(d => d >= 2.9f && d <= 8.1f), "they rise 3-8 m off the edge: " + string.Join(", ", dists.Select(d => d.ToString("F1")).ToArray()));
				Check(ref ok, baseY < -12f, "every arm's base is deep under the sea (highest base at " + baseY.ToString("F1") + " m)");
				yield return KrakenWait(Kraken.RiseSeconds + 0.5f);
				Check(ref ok, arm.Phase == 1, "it has risen and holds on");
				Vector3 Ew = pivot.TransformPoint(E);
				yield return PlayPicture("kraken_side", pivot.TransformPoint(E + s * 10f - u * 1.5f + Vector3.up * 0.9f), pivot.TransformPoint(E + Vector3.up * 2.3f + u * 2f));
				yield return PlayPicture("kraken_top", pivot.TransformPoint(E - u * 8f + s * 3.5f + Vector3.up * 3.4f), pivot.TransformPoint(E + Vector3.up * 2.2f + u * 1.5f));
				// the rarer colourings (1 in 8 each), on this arm for a picture, then back to its own
				Log("this arm's colouring: " + Kraken.ColouringNames[Kraken.Colouring(arm.Seed)]);
				int[] colourings = { 1, 2, Kraken.Colouring(arm.Seed) };
				for (int k = 0; k < colourings.Length; k++)
				{
					foreach (var t in arm.Others.Concat(new[] { arm.Main })) t.Recolour(Kraken.Skin(), colourings[k]);
					if (k < 2) yield return PlayPicture("kraken_" + Kraken.ColouringNames[colourings[k]], pivot.TransformPoint(E + s * 10f - u * 1.5f + Vector3.up * 0.9f), pivot.TransformPoint(E + Vector3.up * 2.3f + u * 2f));
				}

				// 4b. The free arms swipe: stand at the edge by the nearest one; within a cycle it slams there and hurts a little
				Kraken.Tentacle near = arm.Others.OrderBy(t => Vector3.Dot(t.Ctrl[3] - E, u)).First();
				float along = Mathf.Clamp(Vector3.Dot(near.Ctrl[3] - E, s), -1.5f, 1.5f);
				PlayerMove.To(me, pivot.TransformPoint(E - u * 0.6f + s * along + Vector3.up * 1.2f));
				float hp0 = me.Stats.stat_health.Value;
				int landed = Kraken.SwipesLanded;
				for (float t = Time.time + Kraken.SwipeEvery + 2f; Time.time < t && Kraken.SwipesLanded == landed;) yield return KrakenWait(0.1f);
				float lost = hp0 - me.Stats.stat_health.Value;
				Check(ref ok, Kraken.SwipesLanded > landed && lost > 0f && lost <= Kraken.SwipeDamage * 2f, "a free arm swiped the player standing by it: -" + lost.ToString("F1") + " health (of " + hp0.ToString("F0") + ")");
				// pictures of the next swipe: reared up, then the slam
				Vector3 Nw = near.Ctrl[3];
				Vector3 camAt = E - u * 5f + s * 13f + Vector3.up * 6f, lookAt = (Nw + E) * 0.5f + Vector3.up * 1.5f;
				camAt.y = Mathf.Max(camAt.y, E.y + 5f);
				for (float t = Time.time + Kraken.SwipeEvery + 1f; Time.time < t && arm.SwipePushOf(near).y < 1.1f;) yield return KrakenWait(0.02f);
				yield return PlayPicture("kraken_swipe_rear", pivot.TransformPoint(camAt), pivot.TransformPoint(lookAt));
				landed = Kraken.SwipesLanded;
				for (float t = Time.time + 2f; Time.time < t && Kraken.SwipesLanded == landed;) yield return KrakenWait(0.02f);
				yield return PlayPicture("kraken_swipe_slam", pivot.TransformPoint(camAt), pivot.TransformPoint(lookAt));
				PlayerMove.To(me, pivot.TransformPoint(arm.Block.transform.localPosition - u * 4.5f + Vector3.up * 1.2f));
				yield return KrakenWait(0.3f);

				// 5. Hits: a miss doesn't count; 10 hits make it let go
				Vector3 target = pivot.TransformPoint(arm.Main.Pts[(int)(arm.Main.N * 0.62f)]);
				Vector3 from = target - (pivot.TransformDirection(u) * 2f) + Vector3.up * 0.3f + pivot.TransformDirection(-u) * 0.5f;
				bool miss = Kraken.TryHit(from, -(target - from), 3f);
				Check(ref ok, !miss && arm.Hits == 0, "a swing the other way misses");
				int beaten = Kraken.Beaten;
				// keep swinging until 10 count: a swing inside the 0.3 s cooldown (game time; a minimised Raft runs slowly) is not a hit
				int swings = 0;
				for (float t = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < t && arm != null && arm.Phase == 1 && !arm.Released;)
				{
					// the raft drifts: aim from beside the arm where it is now
					target = pivot.TransformPoint(arm.Main.Pts[(int)(arm.Main.N * 0.62f)]);
					from = target - pivot.TransformDirection(u) * 2.5f + Vector3.up * 0.3f;
					Kraken.TryHit(from, target - from, 3f);
					swings++;
					yield return KrakenWait(0.35f);
				}
				Log("swings: " + swings);
				Check(ref ok, Kraken.Beaten == beaten + 1 && arm.Released && arm.Hits == Kraken.HitsNeeded, "10 hits: it let go (" + arm.Hits + " hits)");
				Check(ref ok, sent.Count(m => m.Kind == IslandNetMessage.Kraken && m.Name == "hits") == Kraken.HitsNeeded && sent.Any(m => m.Kind == IslandNetMessage.Kraken && m.Name == "let"), "every hit and the let-go went to every player");
				yield return KrakenWait(Kraken.SinkSeconds + 0.6f);
				Check(ref ok, Kraken.Current == null && arm == null, "the arms are gone");
				Check(ref ok, StormSeas.Foundations().Count == floors, "every floor kept: " + StormSeas.Foundations().Count + " of " + floors);

				// 6. Too slow: it tears its floor and the neighbours its seed says
				Kraken.TestStay = 5f;
				Log("bring: " + Kraken.Bring());
				arm = Kraken.Current;
				if (arm == null) { Fail("kraken: no second arm"); yield break; }
				grid = StormSeas.Grid(StormSeas.Foundations());
				cell = grid.First(kv => kv.Value == arm.Block).Key;
				Block grabbed = arm.Block;
				var goes = new List<Block>(); var stays = new List<Block>();
				for (int i = 0; i < 4; i++) { Block n; if (grid.TryGetValue(cell + StormSeas.Sides[i], out n)) (Kraken.NeighbourGoes(arm.Seed, i) ? goes : stays).Add(n); }
				int before = StormSeas.Foundations().Count, torn = Kraken.Torn;
				Log("grabbed " + cell + ", neighbours going " + goes.Count + ", staying " + stays.Count);
				pivot = arm.transform.parent;
				sv = StormSeas.Sides[arm.SideIndex]; u = new Vector3(sv.x, 0f, sv.y); s = Vector3.Cross(Vector3.up, u);
				E = grabbed.transform.localPosition + u * 0.75f;
				for (float t = Time.time + 12f; Time.time < t && Kraken.Torn == torn;) yield return null;
				yield return KrakenWait(0.8f);
				yield return PlayPicture("kraken_torn", pivot.TransformPoint(E + s * 7f + u * 4f + Vector3.up * 4f), pivot.TransformPoint(E - u * 0.5f));
				yield return KrakenWait(0.5f);
				int after = StormSeas.Foundations().Count;
				Check(ref ok, Kraken.Torn == torn + 1 && grabbed == null, "it tore the grabbed floor away");
				Check(ref ok, after == before - 1 - goes.Count && Kraken.LastTornCount == 1 + goes.Count, "floors " + before + " -> " + after + ", torn " + Kraken.LastTornCount + " (1 + " + goes.Count + " neighbours from the seed)");
				Check(ref ok, goes.All(b => b == null) && stays.All(b => b != null), "the neighbours rolled to go are gone, the others stay");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Kraken && m.Name == "tear" && m.Count == 1 + goes.Count), "the tear went to every player");
				yield return KrakenWait(Kraken.SinkSeconds * 1.4f + 0.6f);
				Check(ref ok, Kraken.Current == null, "the arms are gone after the tear");

				// 7. The option off sends an arm away
				Kraken.TestStay = 60f;
				Log("bring: " + Kraken.Bring());
				yield return KrakenWait(1f);
				WorldOptions.Set(new HashSet<string>(optionsBefore.Where(x => x != WorldOptions.Kraken)));
				yield return KrakenWait(0.3f);
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.Kraken && m.Name == "away") && !Kraken.Active, "switched off: the arm goes away");
				Check(ref ok, Kraken.Bring().IndexOf("off", StringComparison.Ordinal) >= 0 && (Kraken.Current == null || Kraken.Current.Phase == 2), "off: no arm comes");
				Kraken.KrakenCommand(new string[0]);
			}
			finally
			{
				IslandNetwork.Loopback = null;
				Kraken.TestStay = null;
				foreach (Block b in made) { try { if (b != null) BlockCreator.RemoveBlockNetwork(b, null, true); } catch { } }
				WorldOptions.Set(optionsBefore);
				Kraken.Reset();
			}
			if (ok) Log("PASS: kraken"); else Fail("kraken");
		}
	}
}
