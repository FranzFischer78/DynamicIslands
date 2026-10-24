using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIFog", docs: "Dev, in game (host): Fog banks (TODO 4i). Checks the schedule (rate, hours, sight, storms, smooth ramps), then brings fog in now, checks that the sky's fog distance drops to the bank's sight, ends it and checks that Raft's own fog is back.")]
		public static void FogCommand(string[] args) { StartTest(FogRoutine()); }

		[ConsoleCommand(name: "CIFogLook", docs: "Dev, in game (host): pictures of a fog bank - an island looming out of it by day and the fog at night (shot_fog_day.png, shot_fog_day_far.png, shot_fog_day_near.png, shot_fog_day_clear.png, shot_fog_night.png, shot_fog_night_clear.png). CIFogLook [sight m]")]
		public static void FogLookCommand(string[] args)
		{
			float sight = args != null && args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 60f;
			StartTest(FogLookRoutine(sight));
		}

		static IEnumerator FogRoutine()
		{
			bool ok = true;
			// The schedule: about three days in five, none before day 2, the same for the same seed
			int days = Enumerable.Range(0, 2000).Count(d => FogBanks.Rolls(42, d));
			Check(ref ok, days > 2000 * 0.5 && days < 2000 * 0.7, "about 60 % of days have a fog bank: " + days + " of 2000");
			Check(ref ok, !FogBanks.Rolls(42, 0) && !FogBanks.Rolls(42, 1), "none before day 2");
			Check(ref ok, Enumerable.Range(0, 300).All(d => FogBanks.Rolls(42, d) == FogBanks.Rolls(42, d)), "the same twice");
			Check(ref ok, Enumerable.Range(0, 300).Count(d => FogBanks.Rolls(42, d) != FogBanks.Rolls(43, d)) > 50, "another seed, other days");
			var banks = Enumerable.Range(2, 1000).Where(d => FogBanks.Rolls(7, d)).Select(d => FogBanks.BankOf(7, d)).ToList();
			Check(ref ok, banks.All(b => b.Hours >= FogBanks.MinHours && b.Hours <= FogBanks.MaxHours), "banks last 3-6 hours");
			Check(ref ok, banks.All(b => b.Visibility >= FogBanks.NearVisibility && b.Visibility <= FogBanks.FarVisibility), "45-75 m to see");
			Check(ref ok, banks.Count(b => { double h = b.Start % 24.0; return h >= 20 || h < 5; }) > banks.Count / 8, "some at night too");
			// Smooth: the schedule's fog never jumps (a step of 0.01 h moves it by at most a little)
			float worst = 0f, prev = 0f; FogBanks.Bank bk;
			for (double t = 48; t < 24 * 60; t += 0.01) { float w = FogBanks.At(7, t, out bk); worst = Mathf.Max(worst, Mathf.Abs(w - prev)); prev = w; }
			Check(ref ok, worst < 0.03f, "the schedule fades in and out (largest step " + worst.ToString("F3") + " per 0.01 h)");
			FogBanks.Bank first = FogBanks.Next(7, 48);
			Check(ref ok, FogBanks.At(7, first.Start + first.Hours / 2, out bk) > 0.99f && bk.Day == first.Day, "thick in the middle of a bank");
			Check(ref ok, FogBanks.Stormy(UniqueWeatherType.Rain) && FogBanks.Stormy(UniqueWeatherType.BigWaves) && !FogBanks.Stormy(UniqueWeatherType.Calm) && !FogBanks.Stormy(UniqueWeatherType.Fog), "storms keep the fog away");

			if (CustomIslandSpawner.RaftPosition == null || !Raft_Network.IsHost || Camera.main == null) { Fail("fog: run in a world, as the host"); yield break; }
			// (on the raft, the camera over the sea: FogBanks lets go of its fog while the camera is under water - the gap phase ran
			// CIFog with the player in the sea and read Raft's own fog, RUN1 2026-10-21)
			yield return EnsureAlive();
			OnRaftCommand();
			yield return new WaitForSeconds(1.5f);
			if (Camera.main == null || Camera.main.transform.position.y < -0.6f) { Fail("fog: the camera is under water (y " + (Camera.main != null ? Camera.main.transform.position.y.ToString("F1") : "-") + ") even after putting the player on the raft"); yield break; }
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			try
			{
				var set = new HashSet<string>(optionsBefore); set.Add(WorldOptions.Fog); WorldOptions.Set(set);
				FogBanks.Reset();
				SetHour(12f);
				yield return new WaitForSeconds(2f);
				float raft = FogBanks.RaftDistance;
				bool unityFog = RenderSettings.fog; float unityEnd = RenderSettings.fogEndDistance;
				Log("fog: Raft's own fog distance " + raft.ToString("F0") + ", density " + FogBanks.ShownDensity.ToString("F2"));
				Check(ref ok, FogBanks.Weight == 0f && Mathf.Abs(FogBanks.ShownDistance - raft) < 1f, "no fog before: shown " + FogBanks.ShownDistance.ToString("F0"));
				Check(ref ok, FogBanks.TintShown == 0f, "no veil before");

				// In: fades, never jumps
				FogBanks.TestVisibility = 60f;
				FogBanks.TestTarget = 1f;
				float last = FogBanks.Weight, step = 0f, t0 = Time.unscaledTime;
				bool halfway = false;
				while (FogBanks.Weight < 0.999f && Time.unscaledTime - t0 < FogBanks.FadeSeconds * 2f)
				{
					yield return null;
					step = Mathf.Max(step, FogBanks.Weight - last); last = FogBanks.Weight;
					if (!halfway && FogBanks.Weight > 0.5f) { halfway = true; Log("fog: halfway after " + (Time.unscaledTime - t0).ToString("F1") + " s, shown " + FogBanks.ShownDistance.ToString("F0") + " m"); }
				}
				Check(ref ok, FogBanks.Weight >= 0.999f, "the fog rolls in (" + (Time.unscaledTime - t0).ToString("F0") + " s)");
				Check(ref ok, step < 0.05f, "smoothly (largest step " + step.ToString("F3") + " a frame)");
				yield return new WaitForSeconds(1f);
				float want = 60f * FogBanks.DistanceFactor;
				Check(ref ok, FogBanks.ShownDistance <= want * 1.1f && FogBanks.ShownDistance < raft * 0.5f, "fog distance drops: " + FogBanks.ShownDistance.ToString("F0") + " (Raft's " + raft.ToString("F0") + ", want " + want.ToString("F0") + ")");
				Check(ref ok, FogBanks.ShownDensity > 0.95f, "thick: density " + FogBanks.ShownDensity.ToString("F2"));
				Check(ref ok, RenderSettings.fog && FogBanks.NearEndShown > 0f && RenderSettings.fogEndDistance <= 60f * FogBanks.NearEnd * 1.1f,
					"the sea fogs too (Unity fog " + RenderSettings.fogStartDistance.ToString("F0") + "-" + RenderSettings.fogEndDistance.ToString("F0") + " m, colour " + RenderSettings.fogColor + ")");
				Check(ref ok, Mathf.Abs(FogBanks.TintShown - FogBanks.TintAlpha) < 0.02f, "the veil greys the view (" + FogBanks.TintShown.ToString("F2") + ")");
				Check(ref ok, Mathf.Abs(FogBanks.RaftDistance - raft) < Mathf.Max(5f, raft * 0.1f), "Raft's own value untouched: " + FogBanks.RaftDistance.ToString("F0"));

				// Out
				FogBanks.TestTarget = 0f;
				t0 = Time.unscaledTime;
				while (FogBanks.Weight > 0f && Time.unscaledTime - t0 < FogBanks.FadeSeconds * 2f) yield return null;
				yield return new WaitForSeconds(1f);
				Check(ref ok, FogBanks.Weight == 0f, "the fog lifts (" + (Time.unscaledTime - t0).ToString("F0") + " s)");
				Check(ref ok, Mathf.Abs(FogBanks.ShownDistance - FogBanks.RaftDistance) < 1f, "back to Raft's own fog: " + FogBanks.ShownDistance.ToString("F0") + " = " + FogBanks.RaftDistance.ToString("F0"));
				Check(ref ok, RenderSettings.fog == unityFog && Mathf.Abs(RenderSettings.fogEndDistance - unityEnd) < 0.01f && FogBanks.NearEndShown == 0f, "Unity's fog back as Raft had it (" + RenderSettings.fog + ", " + RenderSettings.fogEndDistance.ToString("F0") + ")");
				Check(ref ok, FogBanks.TintShown == 0f, "the veil gone");
			}
			finally { FogBanks.Reset(); WorldOptions.Set(optionsBefore); }
			if (ok) Log("PASS: fog banks"); else Fail("fog banks");
		}

		static IEnumerator FogLookRoutine(float sight)
		{
			if (CustomIslandSpawner.RaftPosition == null || Camera.main == null) { Fail("fog look: run in a world"); yield break; }
			yield return EnsureAlive();
			Vector3 raftPos = CustomIslandSpawner.RaftPosition.Value;
			// A spawned big island (Raft keeps unused island objects active in a pool: those stand anywhere, so only isSpawned ones)
			Landmark big = WorldManager.AllLandmarks == null ? null : WorldManager.AllLandmarks.Where(l => l != null && l.isSpawned && l.gameObject.activeInHierarchy && WorldRandomizer.KindOf(l) == "Landmark_Big")
				.OrderBy(l => (l.transform.position - raftPos).sqrMagnitude).FirstOrDefault();
			if (big == null || Vector3.Distance(Flat(big.transform.position), Flat(raftPos)) > 600f) yield return BringBigIsland(l => big = l);
			if (big == null) { Fail("fog look: no island"); yield break; }
			// Its land, found by rays straight down (not by its renderers' bounds - those take in far-off parts and gave a
			// point 130-165 m out at sea): the middle is where the island stands, the shore where ground above the sea ends
			// on the side towards the raft.
			Func<Vector3, bool> land = p =>
			{
				RaycastHit hit;
				return Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out hit, 600f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > 0.5f
					&& hit.collider.GetComponentInParent<Raft>() == null;
			};
			Vector3 centre = Flat(big.transform.position);
			if (!land(centre))
			{
				var cs = big.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToList();
				if (cs.Count > 0) { Bounds b = cs[0].bounds; foreach (Collider c in cs) b.Encapsulate(c.bounds); centre = Flat(b.center); }
			}
			Vector3 dir = Flat(raftPos - centre).sqrMagnitude > 1f ? Flat(raftPos - centre).normalized : Vector3.forward;
			float edge = 0f;
			for (float d = 0f, miss = 0f; d < 300f && miss < 12f; d += 2f)
				if (land(centre + dir * d)) { edge = d; miss = 0f; } else miss += 2f;
			if (edge < 5f) Log("fog look: no ground found under " + big.name + " - standing 60 m from its middle");
			if (edge < 5f) edge = 60f;
			// The island's highest ground (rays down over a grid round its middle): the camera looks at it
			RaycastHit top = default(RaycastHit);
			float reach = edge + 60f;
			for (float x = -reach; x <= reach; x += 4f)
				for (float z = -reach; z <= reach; z += 4f)
				{
					RaycastHit hit;
					Vector3 q = centre + new Vector3(x, 0f, z);
					if (Physics.Raycast(new Vector3(q.x, 300f, q.z), Vector3.down, out hit, 600f, ~0, QueryTriggerInteraction.Ignore)
						&& hit.point.y > top.point.y && hit.collider.GetComponentInParent<Raft>() == null) top = hit;
				}
			Vector3 at = top.point.y > 0.5f ? top.point : centre + Vector3.up * 10f;
			// Open water round the camera: no ground within 20 m of it (an uneven shore put the first pictures beside a
			// cliff or on a beach), stepping further out along the line until that holds
			Func<Vector3, bool> open = e =>
			{
				if (land(e)) return false;
				for (int i = 0; i < 16; i++)
				{
					Vector3 o = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
					for (float r = 5f; r <= 20f; r += 5f) if (land(e + o * r)) return false;
				}
				return true;
			};
			Func<float, float> clearFrom = m => { float k = m; while (k < m + 300f && !open(centre + dir * (edge + k))) k += 2f; return k; };
			Func<float, Vector3> eyeAt = m => centre + dir * (edge + clearFrom(m)) + Vector3.up * 3f;
			float looming = clearFrom(Mathf.Clamp(sight * 0.6f, 30f, 40f));
			Vector3 eye = centre + dir * (edge + looming) + Vector3.up * 3f;
			Log("fog look: island " + big.name + " at " + Flat(centre - raftPos).magnitude.ToString("F0") + " m, shore " + edge.ToString("F0") + " m from its middle, eye " + looming.ToString("F0") + " m off the shore, looking at its top (" + at.y.ToString("F0") + " m high, " + Flat(at - eye).magnitude.ToString("F0") + " m away)");

			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			try
			{
				var set = new HashSet<string>(optionsBefore); set.Add(WorldOptions.Fog); WorldOptions.Set(set);
				FogBanks.Reset();
				SetHour(11f);
				yield return new WaitForSeconds(3f);
				yield return CameraShot(eye, at, "fog_day_clear");
				FogBanks.TestVisibility = sight; FogBanks.TestInstant = true; FogBanks.TestTarget = 1f;
				yield return new WaitForSeconds(2f);
				Log("fog look: day, shown " + FogBanks.ShownDistance.ToString("F0") + " (Raft's " + FogBanks.RaftDistance.ToString("F0") + ")");
				yield return CameraShot(eye, at, "fog_day");
				// Looming: the shore from further out (only a shadow) and closer in (firming up)
				yield return CameraShot(eyeAt(sight * 1.2f), at, "fog_day_far");
				yield return CameraShot(eyeAt(15f), at, "fog_day_near");
				SetHour(23f);
				yield return new WaitForSeconds(3f);
				Log("fog look: night, shown " + FogBanks.ShownDistance.ToString("F0") + " (Raft's " + FogBanks.RaftDistance.ToString("F0") + ")");
				yield return CameraShot(eye, at, "fog_night");
				FogBanks.TestTarget = 0f;
				yield return new WaitForSeconds(1f);
				yield return CameraShot(eye, at, "fog_night_clear");
			}
			finally { FogBanks.Reset(); WorldOptions.Set(optionsBefore); SetHour(12f); }
			Log("PASS: fog pictures");
		}
	}
}
