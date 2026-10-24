using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>Tests of the world option Storm seas (StormSeas, TODO 4e).</summary>
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIStormSeas", docs: "Dev, world (host, single player): the option Storm seas - storm n breaks a floor about 1 in 4 (from the seed, the same twice); the raft rocks harder in a storm (x2.5 and a sway, never past 12 degrees, unchanged when calm); with it on Raft's weather pick gives a storm far more often, off as Raft does; the save line round trip; then on a raft grown to 5x5 a forced storm is counted and breaks exactly one floor on the edge (never one in the middle), with the banner message (kind 35) sent; pictures shot_stormseas_storm.png and shot_stormseas_broken.png; raft, weather and options put back after")]
		public static void StormSeasTestCommand() { StartTest(StormSeasRoutine()); }

		static bool IsStormWeather(Weather w)
		{
			if (w == null) return false;
			SO_Weather so = HarmonyLib.Traverse.Create(w).Field("so_weather").GetValue<SO_Weather>();
			return so != null && StormSeas.IsStormType(so.uniqueWeatherType);
		}

		static IEnumerator StormSeasRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("storm seas: host, in a world"); yield break; }
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			int storms0 = StormSeas.Storms, breaks0 = StormSeas.Breaks, pending0 = StormSeas.Pending;
			var made = new List<Block>();
			var sent = new List<IslandNetMessage>();
			WeatherManager wm = UnityEngine.Object.FindObjectOfType<WeatherManager>();
			Raft raft = UnityEngine.Object.FindObjectOfType<Raft>();
			Network_Player me = RAPI.GetLocalPlayer();
			if (wm == null || raft == null || me == null) { Fail("storm seas: no weather manager, raft or player"); yield break; }
			try
			{
				WorldOptions.Set(new HashSet<string>(optionsBefore) { WorldOptions.StormSeas });
				int seed = WorldOptions.Seed;

				// 1. The roll: about 1 in 4 storms, the same every time
				StormSeas.TestBreak = null;
				int hits = Enumerable.Range(1, 2000).Count(n => StormSeas.RollsBreak(seed, n));
				bool same = Enumerable.Range(1, 200).All(n => StormSeas.RollsBreak(seed, n) == StormSeas.RollsBreak(seed, n));
				Check(ref ok, hits >= 440 && hits <= 560 && same, "storms that break a floor: " + hits + " of 2000 (about 500), the same twice: " + same);

				// 2. The rocking
				Vector3 calm = new Vector3(4f, 30f, 357f);
				Vector3 full = StormSeas.Rocked(calm, 1f, 0f), big = StormSeas.Rocked(new Vector3(10f, 0f, 350f), 1f, 0f);
				Check(ref ok, StormSeas.Rocked(calm, 0f, 5f) == calm && Mathf.Abs(full.x - 10f) < 0.01f && Mathf.Abs(full.z - (-7.5f + 1.2f * Mathf.Sin(1f))) < 0.01f && full.y == 30f,
					"rocking: calm unchanged, a storm's tilt (4, -3) becomes " + full.ToString("F2") + " (x2.5 and the sway)");
				Check(ref ok, Mathf.Abs(big.x - StormSeas.MaxTilt) < 0.01f && Mathf.Abs(big.z + StormSeas.MaxTilt) < 0.01f, "rocking never past " + StormSeas.MaxTilt + " degrees: " + big.ToString("F2"));

				// 3. Raft's weather pick
				var pick = HarmonyLib.Traverse.Create(wm).Method("GetRandomWeather", new[] { typeof(WeatherPoolType), typeof(bool) });
				Func<int> countStorms = () => { int c = 0; for (int i = 0; i < 400; i++) if (IsStormWeather(pick.GetValue<Weather>(WeatherPoolType.Default, false))) c++; return c; };
				int on = countStorms();
				WorldOptions.Set(new HashSet<string>(optionsBefore.Where(x => x != WorldOptions.StormSeas)));
				int off = countStorms();
				WorldOptions.Set(new HashSet<string>(optionsBefore) { WorldOptions.StormSeas });
				Check(ref ok, on >= 160 && on > off, "Raft's weather pick gives a storm " + on + " of 400 times with the option on, " + off + " off");

				// 4. The save line
				StormSeas.Storms = 7; StormSeas.Breaks = 2; StormSeas.Pending = 0;
				string line = string.Join("|", StormSeas.WriteLines().ToArray());
				StormSeas.ReadLine("stormseas", "3;1;0");
				Check(ref ok, line == "@stormseas=7;2;0" && StormSeas.Storms == 3 && StormSeas.Breaks == 1 && StormSeas.Pending == 0, "save line: " + line + ", read back 3;1;0 as " + StormSeas.Storms + ";" + StormSeas.Breaks + ";" + StormSeas.Pending);
				StormSeas.Storms = storms0; StormSeas.Breaks = breaks0; StormSeas.Pending = 0;

				// 5. A raft of 5 x 5 floors (the start raft grown around its middle)
				List<Block> start = StormSeas.Foundations();
				if (start.Count == 0) { Fail("storm seas: no foundation on the raft"); yield break; }
				Vector3 o = start[0].transform.localPosition;
				var taken = new HashSet<Vector2Int>(start.Select(b => new Vector2Int(Mathf.RoundToInt((b.transform.localPosition.x - o.x) / HeadStart.Cell), Mathf.RoundToInt((b.transform.localPosition.z - o.z) / HeadStart.Cell))));
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
				for (int f = 0; f < 30; f++) yield return null;
				List<Block> all = StormSeas.Foundations(), edges = StormSeas.EdgeFoundations(), middle = StormSeas.MiddleFoundations();
				Check(ref ok, all.Count >= 25 && middle.Count >= 9 && edges.Count >= 16 && !edges.Intersect(middle).Any(), "the raft: " + all.Count + " floors, " + edges.Count + " on the edge, " + middle.Count + " in the middle (none both)");
				var edgeAt = edges.Select(b => b.transform.localPosition).ToList();
				Transform pivot = all[0].transform.parent;

				// 6. A storm: counted, and its floor breaks on the edge
				IslandNetwork.Loopback = sent.Add;
				wm.SetWeather(UniqueWeatherType.Calm, true);
				for (float t = Time.time + 3f; Time.time < t;) yield return null;
				int count = all.Count, stormsBefore = StormSeas.Storms, breaksBefore = StormSeas.Breaks;
				StormSeas.TestBreak = true;
				StormSeas.TestDelay = 8f;
				wm.SetWeather(StormDays.StormWeather(wm), false);
				for (float t = Time.time + 20f; Time.time < t && StormSeas.Storms == stormsBefore;) yield return null;
				Check(ref ok, StormSeas.Storms == stormsBefore + 1 && StormSeas.Pending == StormSeas.Storms, "a storm started (" + wm.GetCurrentWeatherType() + "): counted " + (StormSeas.Storms - stormsBefore) + ", its floor to break: " + (StormSeas.Pending > 0));
				for (float t = Time.time + 6f; Time.time < t;) yield return null;
				Check(ref ok, StormSeas.Strength > 0.2f, "the storm's rocking eases in: " + StormSeas.Strength.ToString("F2"));
				Vector3 mid = pivot != null ? pivot.TransformPoint(o + new Vector3(cx * HeadStart.Cell, 0f, cz * HeadStart.Cell)) : raft.transform.position;
				yield return CameraShot(mid + new Vector3(9f, 6f, -9f), mid, "stormseas_storm");
				for (float t = Time.time + 25f; Time.time < t && StormSeas.Breaks == breaksBefore;) yield return null;
				for (int f = 0; f < 30; f++) yield return null;
				List<Block> after = StormSeas.Foundations();
				bool wasEdge = StormSeas.LastBroken.HasValue && edgeAt.Any(p => (p - StormSeas.LastBroken.Value).sqrMagnitude < 0.01f);
				Check(ref ok, StormSeas.Breaks == breaksBefore + 1 && after.Count == count - 1, "the storm broke " + (StormSeas.Breaks - breaksBefore) + " floor(s): " + count + " -> " + after.Count);
				Check(ref ok, wasEdge, "the broken floor was on the edge (" + (StormSeas.LastBroken.HasValue ? StormSeas.LastBroken.Value.ToString("F1") : "none") + ")");
				Check(ref ok, middle.All(b => b != null), "no middle floor broke (" + middle.Count(b => b != null) + " of " + middle.Count + " still there)");
				Check(ref ok, sent.Any(m => m.Kind == IslandNetMessage.StormSeas && m.Name == "broke"), "the banner message went to every player (kind " + IslandNetMessage.StormSeas + ")");
				if (StormSeas.LastBroken.HasValue && pivot != null)
				{
					Vector3 gone = pivot.TransformPoint(StormSeas.LastBroken.Value);
					Vector3 outward = (gone - mid); outward.y = 0f;
					outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;
					yield return CameraShot(gone + outward * 5f + Vector3.up * 5f, gone, "stormseas_broken");
				}

				// 7. Off: no storm counted, Raft's pick untouched
				WorldOptions.Set(new HashSet<string>(optionsBefore.Where(o2 => o2 != WorldOptions.StormSeas)));
				Weather calmW = StormSeas.FindWeather(UniqueWeatherType.Calm);
				Check(ref ok, calmW != null && StormSeas.Picked(wm, WeatherPoolType.Default, calmW) == calmW, "the option off: Raft's pick stays its own");
			}
			finally
			{
				IslandNetwork.Loopback = null;
				StormSeas.TestBreak = null;
				StormSeas.TestDelay = null;
				foreach (Block b in made) { try { if (b != null) BlockCreator.RemoveBlockNetwork(b, null, true); } catch { } }
				StormSeas.Storms = storms0; StormSeas.Breaks = breaks0; StormSeas.Pending = pending0;
				try { wm.SetWeather(UniqueWeatherType.Calm, false); wm.WeatherTimer = 5f; } catch { }
				WorldOptions.Set(optionsBefore);
			}
			if (ok) Log("PASS: storm seas"); else Fail("storm seas");
		}
	}
}
