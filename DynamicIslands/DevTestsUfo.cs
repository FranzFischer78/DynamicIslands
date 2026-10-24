using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIUfo", docs: "Dev, in game (host): UFO sightings (TODO 4h). Checks the schedule and the message text, sends a saucer across now (by day or night), checks that it moves and is gone at the end, and takes pictures shot_ufo_<day|night>_wide.png and _close.png. CIUfo [day|night] [seconds]")]
		public static void UfoCommand(string[] args)
		{
			bool night = args != null && args.Length > 0 && args[0].ToLowerInvariant() == "night";
			float seconds = args != null && args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 30f;
			StartTest(UfoRoutine(night, seconds));
		}

		static IEnumerator UfoRoutine(bool night, float seconds)
		{
			bool ok = true;
			string tag = night ? "night" : "day";
			// The schedule: about two days in five, none before day 2, the same for the same seed; hours by day or at night
			int days = Enumerable.Range(0, 2000).Count(d => UfoSightings.Rolls(42, d));
			Check(ref ok, days > 2000 * 0.33 && days < 2000 * 0.47, "about 40 % of days have a sighting: " + days + " of 2000");
			Check(ref ok, !UfoSightings.Rolls(42, 0) && !UfoSightings.Rolls(42, 1), "none before day 2");
			int nights = Enumerable.Range(2, 2000).Where(d => UfoSightings.Rolls(7, d)).Count(d => { float h = UfoSightings.HourOf(7, d); return h >= 21f || h < 3f; });
			int all = Enumerable.Range(2, 2000).Count(d => UfoSightings.Rolls(7, d));
			Check(ref ok, nights > all * 0.4 && nights < all * 0.6, "about half at night: " + nights + " of " + all);
			Check(ref ok, Enumerable.Range(2, 500).All(d => { float h = UfoSightings.HourOf(7, d); return h >= 0f && h < 24f && !(h >= 3f && h < 8f) && !(h >= 18f && h < 21f); }), "hours 21-03 or 08-18");
			// The message text survives a round trip
			UfoSaucer.Path p0 = UfoSaucer.Roll(new System.Random(5));
			UfoSaucer.Path back = UfoSaucer.Path.Decode(p0.Encode());
			Check(ref ok, back != null && back.Encode() == p0.Encode(), "path written and read back: " + p0.Encode());
			Check(ref ok, UfoSaucer.Path.Decode("x|1") == null && UfoSaucer.Path.Decode(null) == null, "bad text is ignored");

			Vector3? raftPos = CustomIslandSpawner.RaftPosition;
			if (!raftPos.HasValue || !Raft_Network.IsHost || Camera.main == null) { Fail("ufo: run in a world, as the host"); yield break; }
			SetHour(night ? 23.5f : 12f);
			yield return new WaitForSeconds(3f);
			Log("ufo: hour " + (night ? 23.5f : 12f) + ", night " + NightDanger.IsNight + ", camera far " + Camera.main.farClipPlane);
			Check(ref ok, NightDanger.IsNight == night, "the sky's " + tag);

			// The host's own message reaches every player (here: caught on its way)
			IslandNetMessage sent = null;
			Action<IslandNetMessage> before = IslandNetwork.Loopback;
			IslandNetwork.Loopback = m => { if (m.Kind == IslandNetMessage.Moment && m.Name == "ufo") sent = m; };
			int startedBefore = UfoSightings.Started;
			var path = new UfoSaucer.Path { Hue = night ? 0.58f : 0.02f, Glow = night ? 1 : 0, Heading = 90f, Side = 120f, Height = 150f, Seconds = seconds, Spin = 1 };
			try { UfoSightings.Send(path); }
			finally { IslandNetwork.Loopback = before; }
			Check(ref ok, sent != null && sent.Data == path.Encode(), "sent to the players: " + (sent == null ? "nothing" : sent.Data));
			UfoSaucer u = UfoSightings.Current;
			Check(ref ok, u != null && UfoSightings.Started == startedBefore + 1, "a saucer in the sky");
			if (u == null) { Fail("ufo: no saucer"); yield break; }
			Check(ref ok, u.GetComponentsInChildren<Collider>(true).Length == 0, "no colliders (looks only)");
			Check(ref ok, u.GetComponentsInChildren<Renderer>().Length >= 16, "built of " + u.GetComponentsInChildren<Renderer>().Length + " parts");

			// It moves: about Reach * 2 / seconds metres a second, high up
			Vector3 a = u.transform.position;
			yield return new WaitForSeconds(2f);
			if (u == null) { Fail("ufo: gone too soon"); yield break; }
			Vector3 b = u.transform.position;
			float speed = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z)) / 2f, expect = UfoSaucer.Reach * 2f / seconds;
			Check(ref ok, speed > expect * 0.7f && speed < expect * 1.3f, "it moves: " + speed.ToString("F0") + " m/s (expected about " + expect.ToString("F0") + ")");
			Check(ref ok, b.y - raftPos.Value.y > 90f, "high in the sky: " + (b.y - raftPos.Value.y).ToString("F0") + " m");

			// Pictures: from the raft, the saucer coming in over the sea; and close up
			while (u != null && u.Along < 0.27f) yield return null;
			if (u == null) { Fail("ufo: gone too soon"); yield break; }
			Vector3 eye = raftPos.Value + Vector3.up * 3f;
			Vector3 at = Vector3.Lerp(eye, u.transform.position, 0.9f) + Vector3.down * 20f;
			Log("ufo: saucer at " + u.transform.position.ToString("F0") + ", " + Vector3.Distance(eye, u.transform.position).ToString("F0") + " m from the raft, " + (u.Along * 100f).ToString("F0") + " % along");
			yield return CameraShot(eye, at, "ufo_" + tag + "_wide");
			if (u != null)
			{
				Vector3 c = u.transform.position;
				Vector3 dir = (u.transform.forward + Vector3.up * 0.2f + u.transform.right * 0.8f).normalized;
				yield return CameraShot(c + dir * 34f, c, "ufo_" + tag + "_close");
			}

			// Gone at the end of its path
			float until = Time.time + seconds + 5f;
			while (u != null && Time.time < until) yield return null;
			Check(ref ok, u == null && UfoSightings.Current == null, "gone after its crossing");
			Log((ok ? "PASS" : "FAIL") + ": ufo " + tag);
		}
	}
}
