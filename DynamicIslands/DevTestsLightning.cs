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
		[ConsoleCommand(name: "CILightning", docs: "Dev, in game (host): Lightning strikes (TODO 4k). Checks the storm rolls and the message text, strikes now on the sea (or on the raft's antenna: rod; one is put on the raft if it has none) in a storm, checks the bolt, its flash and its end, and that nothing on the raft or the player was damaged; takes pictures shot_lightning_<sea|rod>_<day|night>.png. CILightning [sea|rod] [night|day]")]
		public static void LightningCommand(string[] args)
		{
			bool rod = args != null && args.Length > 0 && args[0].ToLowerInvariant() == "rod";
			bool night = !(args != null && args.Length > 1 && args[1].ToLowerInvariant() == "day");
			StartTest(LightningRoutine(rod, night));
		}

		static IEnumerator LightningRoutine(bool rod, bool night)
		{
			bool ok = true;
			string tag = (rod ? "rod" : "sea") + "_" + (night ? "night" : "day");
			// The storms' rolls: about 3 in 10 without a bolt, never more than MaxPerStorm, about 1.5 on average
			var r = new System.Random(11);
			int[] counts = Enumerable.Range(0, 4000).Select(i => LightningStrikes.RollCount(r)).ToArray();
			int none = counts.Count(c => c == 0);
			Check(ref ok, none > 4000 * 0.25 && none < 4000 * 0.35, "storms without a bolt: " + none + " of 4000");
			Check(ref ok, counts.Max() == LightningStrikes.MaxPerStorm && counts.Min() == 0, "0 to " + counts.Max() + " bolts a storm, " + counts.Average().ToString("F2") + " on average");
			// Where a sea bolt hits: 30-150 m from the raft
			var seaRolls = Enumerable.Range(0, 500).Select(i => LightningStrikes.Roll(r, false)).ToList();
			Check(ref ok, seaRolls.All(s => !s.Rod && s.Point.y == 0f && s.Point.magnitude >= LightningStrikes.MinDistance - 0.01f && s.Point.magnitude <= LightningStrikes.MaxDistance + 0.01f), "sea bolts 30-150 m away");
			// The message text survives a round trip
			LightningStrikes.Strike s0 = LightningStrikes.Roll(new System.Random(5), false);
			LightningStrikes.Strike back = LightningStrikes.Strike.Decode(s0.Encode());
			Check(ref ok, back != null && back.Encode() == s0.Encode(), "strike written and read back: " + s0.Encode());
			Check(ref ok, LightningStrikes.Strike.Decode("x|1") == null && LightningStrikes.Strike.Decode(null) == null && LightningStrikes.Strike.Decode("sea|a|b|c|d") == null, "bad text is ignored");

			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			Network_Player player = RAPI.GetLocalPlayer();
			Raft raft = ComponentManager<Raft>.Value ?? UnityEngine.Object.FindObjectOfType<Raft>();
			if (!raftPos.HasValue || raft == null || player == null || !Raft_Network.IsHost || Camera.main == null) { Fail("lightning: run in a world, as the host"); yield break; }

			// The option off: no bolts, even in a storm
			bool was = WorldOptions.On(WorldOptions.Lightning);
			LightningStrikes.TestStorm = true;
			if (was) WorldOptions.Set(WorldOptions.Current.Where(o => o != WorldOptions.Lightning).ToList());
			int before0 = LightningStrikes.Started;
			yield return new WaitForSeconds(2.5f);
			Check(ref ok, LightningStrikes.Started == before0, "option off: no bolt");
			WorldOptions.Set(WorldOptions.Current.Concat(new[] { WorldOptions.Lightning }).ToList());
			Check(ref ok, LightningStrikes.On && LightningStrikes.IsStorm, "option on, a storm");

			// The storm's look: Raft's rain, at night or by day
			WeatherManager wm = UnityEngine.Object.FindObjectOfType<WeatherManager>();
			if (wm != null) { try { wm.SetWeather(StormDays.StormWeather(wm), false); wm.WeatherTimer = 600f; } catch (Exception e) { Log("  (weather: " + e.Message + ")"); } }
			SetHour(night ? 23.5f : 13f);
			yield return new WaitForSeconds(6f);
			Log("lightning: weather " + (wm != null ? wm.GetCurrentWeatherType().ToString() : "?") + ", night " + NightDanger.IsNight);

			// A rod: the raft's antenna (one put on an edge foundation if it has none)
			if (rod && !LightningStrikes.RodTop().HasValue)
			{
				Block edge = BlockCreator.GetPlacedBlocks().Where(b => b != null && b.buildableItem != null && b.buildableItem.UniqueName.Contains("Foundation"))
					.OrderByDescending(b => new Vector2(b.transform.localPosition.x, b.transform.localPosition.z).magnitude).FirstOrDefault();
				Item_Base antenna = ItemManager.GetItemByName("Placeable_Reciever_Antenna");
				if (edge != null && antenna != null)
				{
					Vector3 local = edge.transform.localPosition; local.y = HeadStart.FoundationTop(edge, raft);
					try { player.BlockCreator.CreateBlockCheat(antenna, local, Vector3.zero, DPS.Default, 0); } catch (Exception e) { Log("  (antenna: " + e.Message + ")"); }
					yield return new WaitForSeconds(1f);
				}
				Log("lightning: an antenna put on the raft: " + LightningStrikes.RodTop().HasValue);
			}
			Vector3? rodTop = LightningStrikes.RodTop();
			if (rod) Check(ref ok, rodTop.HasValue, "the raft has an antenna: " + (rodTop.HasValue ? rodTop.Value.ToString("F1") : "none"));
			if (rod && !rodTop.HasValue) { Fail("lightning: no antenna"); yield break; }

			// What could be damaged: the raft's blocks and their health, the player's health
			List<Block> blocks = BlockCreator.GetPlacedBlocks().Where(b => b != null).ToList();
			int count0 = blocks.Count, health0 = blocks.Sum(b => b.Health);
			float hp0 = player.Stats != null && player.Stats.stat_health != null ? player.Stats.stat_health.Value : -1f;
			int fires0 = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Count(p => p.name.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 && p.isPlaying);

			// Strike now: the host's message reaches every player (here: caught on its way)
			IslandNetMessage sent = null;
			Action<IslandNetMessage> loop = IslandNetwork.Loopback;
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Moment && m.Name == "bolt") sent = m; };
			int started = LightningStrikes.Started;
			LightningStrikes.Strike strike = rod ? LightningStrikes.Roll(new System.Random(3), true) : new LightningStrikes.Strike { Point = new Vector3(55f, 0f, 70f), Shape = 1234 };
			try { LightningStrikes.Send(strike); }
			finally { IslandNetwork.Loopback = loop; }
			Check(ref ok, sent != null && sent.Data == strike.Encode(), "sent to the players: " + (sent == null ? "nothing" : sent.Data));
			Check(ref ok, strike.Rod == rod, rod ? "on the antenna" : "on the sea");
			LightningBolt bolt = LightningStrikes.Current;
			Check(ref ok, bolt != null && LightningStrikes.Started == started + 1, "a bolt");
			if (bolt == null) { Fail("lightning: no bolt"); yield break; }
			Check(ref ok, bolt.GetComponentsInChildren<Collider>(true).Length == 0, "no colliders (looks only)");
			Check(ref ok, bolt.Lines.Count >= 4 && bolt.Lines.All(l => l.positionCount >= 2), "a bolt of " + bolt.Lines.Count + " lines (with forks and glow)");
			Check(ref ok, bolt.Flash != null && bolt.Flash.intensity > 4f && bolt.Flash.range > 500f, "a flash: light " + (bolt.Flash != null ? bolt.Flash.intensity.ToString("F1") : "none"));
			float dist = Vector3.Distance(new Vector3(bolt.Hit.x, 0f, bolt.Hit.z), new Vector3(raftPos.Value.x, 0f, raftPos.Value.z));
			if (rod) Check(ref ok, Vector3.Distance(bolt.Hit, raft.transform.TransformPoint(rodTop.Value)) < 0.5f, "it hits the antenna's top");
			else Check(ref ok, dist > 25f && dist < 160f && Mathf.Abs(bolt.Hit.y) < 0.5f, "it hits the sea " + dist.ToString("F0") + " m from the raft");
			Check(ref ok, bolt.Top.y - bolt.Hit.y > 150f, "from " + (bolt.Top.y - bolt.Hit.y).ToString("F0") + " m up");
			Check(ref ok, !string.IsNullOrEmpty(LightningStrikes.LastThunder) && LightningStrikes.LastThunder != "none", "thunder: " + LightningStrikes.LastThunder);

			// It flickers and is gone within a second
			bool dimmed = false;
			float until = Time.time + 3f;
			while (bolt != null && Time.time < until) { if (bolt.Bright < 0.5f) dimmed = true; yield return null; }
			Check(ref ok, bolt == null && dimmed, "it flickers and is gone in under a second");

			// The picture: the bolt held at its brightest
			LightningStrikes.TestHold = true;
			LightningStrikes.Send(strike);
			LightningBolt held = LightningStrikes.Current;
			yield return null;
			if (held != null)
			{
				Vector3 eye, at;
				if (rod)
				{
					Vector3 top = raft.transform.TransformPoint(rodTop.Value);
					Vector3 away = (new Vector3(top.x, 0f, top.z) - new Vector3(raftPos.Value.x, 0f, raftPos.Value.z)); if (away.sqrMagnitude < 1f) away = Vector3.forward; away.Normalize();
					eye = top + away * 55f + Vector3.up * 4f + Vector3.Cross(away, Vector3.up) * 20f;
					at = top + Vector3.up * 30f;
				}
				else
				{
					Vector3 to = (held.Hit - raftPos.Value); to.y = 0f; to.Normalize();
					eye = raftPos.Value - to * 12f + Vector3.up * 5f;
					at = held.Hit + Vector3.up * 35f;
				}
				yield return CameraShot(eye, at, "lightning_" + tag);
			}
			LightningStrikes.TestHold = false;
			until = Time.time + 3f;
			while (held != null && Time.time < until) yield return null;

			// Nothing damaged: the same blocks with the same health, the player's health, no fire
			yield return new WaitForSeconds(1f);
			blocks = BlockCreator.GetPlacedBlocks().Where(b => b != null).ToList();
			Check(ref ok, blocks.Count == count0 && blocks.Sum(b => b.Health) == health0, "the raft untouched: " + blocks.Count + " blocks (was " + count0 + "), health " + blocks.Sum(b => b.Health) + " (was " + health0 + ")");
			float hp1 = player.Stats != null && player.Stats.stat_health != null ? player.Stats.stat_health.Value : -1f;
			Check(ref ok, hp1 >= hp0, "the player unhurt: health " + hp1 + " (was " + hp0 + ")");
			int fires1 = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Count(p => p.name.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 && p.isPlaying);
			Check(ref ok, fires1 <= fires0, "no fire: " + fires1 + " fire effects (was " + fires0 + ")");
			Check(ref ok, LightningStrikes.Current == null, "the bolt gone");

			// Put back: the option as it was, the weather Raft's again
			LightningStrikes.TestStorm = null;
			if (!was) WorldOptions.Set(WorldOptions.Current.Where(o => o != WorldOptions.Lightning).ToList());
			if (wm != null) wm.WeatherTimer = 5f;
			Log((ok ? "PASS" : "FAIL") + ": lightning " + tag);
		}
	}
}
