using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;
using UnityEngine.AzureSky;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		#region Bioluminescent sea

		[ConsoleCommand(name: "CIGlowSea", docs: "Dev, world (host): the option Bioluminescent sea - the night's roll comes from the seed and the night (the same twice, about 1 in 4); off: no glow, fishing as usual; on, a glowing night forced: the sparks, patches, light and the water's glow show, the fishing wait is halved, two pictures (glowsea_1, glowsea_2); then off again: all gone, the water as before")]
		public static void GlowSeaTestCommand() { StartTest(GlowSeaRoutine()); }

		/// <summary>The wait Raft's bobber gets for a fish when Raft asks for <paramref name="wait"/> s (through the GlowSea patch).</summary>
		static float BiteWait(float wait)
		{
			var go = new GameObject("CITEST_Bobber");
			go.SetActive(false); // (no Awake: only the bite timer is wanted)
			try
			{
				Bobber b = go.AddComponent<Bobber>();
				MethodInfo m = typeof(Bobber).GetMethod("FishOnHook", BindingFlags.Instance | BindingFlags.NonPublic);
				object routine = m.Invoke(b, new object[] { wait });
				FieldInfo f = routine.GetType().GetField("timeDelay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				return f != null ? (float)f.GetValue(routine) : -1f;
			}
			finally { UnityEngine.Object.Destroy(go); }
		}

		static IEnumerator GlowSeaRoutine()
		{
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost) { Fail("glow sea: host, in a world"); yield break; }
			bool ok = true;
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			AzureSkyController sky = ComponentManager<AzureSkyController>.Value;
			float hourBefore = sky != null ? sky.timeOfDay.hour : 12f;
			UltimateWater.Water water = RaftSkySea.Sea ?? UnityEngine.Object.FindObjectOfType<UltimateWater.Water>();
			var emissionParam = UltimateWater.WaterMaterials.ColorParameter.EmissionColor;
			Color emissionBefore = water != null ? water.Materials.GetParameterValue(emissionParam) : Color.clear;
			try
			{
				// The roll
				int seed = WorldOptions.Seed, rolled = 0;
				bool same = true;
				for (int n = 1; n <= 2000; n++) { bool r = GlowSea.Rolls(seed, n); same &= r == GlowSea.Rolls(seed, n); if (r) rolled++; }
				Check(ref ok, same && rolled > 2000 * 0.2f && rolled < 2000 * 0.3f, "the roll: the same twice, " + rolled + " of 2000 nights glow");

				// Off: even a forced glowing night stays dark
				WorldOptions.Set(new HashSet<string>(optionsBefore.Where(o => o != WorldOptions.GlowSea)));
				GlowSea.Reset();
				GlowSea.TestGlow = true;
				SetHour(23f);
				yield return new WaitForSeconds(2f);
				float waitOff = BiteWait(10f);
				Check(ref ok, !GlowSea.Glowing && GlowSea.Level == 0f && GlowSea.Look == null && Mathf.Approximately(waitOff, 10f), "the option off: no glow, the fishing wait 10 s stays " + waitOff + " s");

				// On, by the night's own roll: the day never glows; at night it glows when the night rolls it
				WorldOptions.Set(new HashSet<string>(optionsBefore) { WorldOptions.GlowSea });
				GlowSea.TestGlow = null;
				SetHour(13f);
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, !GlowSea.Glowing, "the option on, by day (13:00): no glow");
				SetHour(23f);
				yield return new WaitForSeconds(1.5f);
				Check(ref ok, GlowSea.Glowing == GlowSea.Rolls(seed, GlowSea.Night), "23:00: night " + GlowSea.Night + (GlowSea.Rolls(seed, GlowSea.Night) ? " rolls a glow" : " rolls dark") + ", glowing " + GlowSea.Glowing);

				// On, a glowing night forced: the look and the bites
				GlowSea.TestGlow = true;
				float until = Time.time + 12f;
				while (Time.time < until && GlowSea.Level < 1f) yield return null;
				yield return new WaitForSeconds(1f);
				GlowSeaLook look = GlowSea.Look;
				Check(ref ok, GlowSea.Level >= 1f && look != null, "a glowing night: the glow faded in (" + GlowSea.Level.ToString("0.00") + ")");
				if (look != null)
				{
					Log("  the look: " + look.Describe());
					Color now = look.WaterEmission;
					Check(ref ok, look.SparkCount > 300 && look.PatchCount > 30 && look.LightIntensity > 1f, "sparks " + look.SparkCount + ", patches " + look.PatchCount + ", light " + look.LightIntensity.ToString("0.0") + ", fish halos " + look.HaloCount + " (" + look.FishSystems + " fish systems near)");
					Check(ref ok, water == null || (look.EmissionSet && now.b > emissionBefore.b + 0.05f), "the water's glow " + emissionBefore + " -> " + now);
				}
				float waitOn = BiteWait(10f);
				Check(ref ok, Mathf.Approximately(waitOn, 10f * GlowSea.BiteFactor) && GlowSea.LastWaitIn == 10f, "the fishing wait 10 s -> " + waitOn + " s");

				// Two pictures of the night
				Network_Player me = RAPI.GetLocalPlayer();
				Vector3 c = CustomIslandSpawner.RaftPosition ?? (me != null ? me.transform.position : Vector3.zero);
				c.y = 0f;
				Vector3 fwd = me != null ? Vector3.ProjectOnPlane(me.transform.forward, Vector3.up).normalized : Vector3.forward;
				if (fwd.sqrMagnitude < 0.1f) fwd = Vector3.forward;
				Vector3 side = Vector3.Cross(Vector3.up, fwd);
				yield return CameraShot(c - fwd * 9f + side * 5f + Vector3.up * 6f, c + fwd * 10f - side * 2f, "glowsea_1");
				yield return CameraShot(c + side * 16f + Vector3.up * 1.4f, c + Vector3.up * 0.5f, "glowsea_2");

				// Off again: all gone, the water as before
				GlowSea.TestGlow = false;
				until = Time.time + 12f;
				while (Time.time < until && GlowSea.Look != null) yield return null;
				yield return new WaitForSeconds(0.5f);
				float waitAfter = BiteWait(10f);
				Color after = water != null ? water.Materials.GetParameterValue(emissionParam) : Color.clear;
				Check(ref ok, GlowSea.Look == null && GlowSea.Level == 0f && GameObject.Find("DI_GlowSea") == null && Mathf.Approximately(waitAfter, 10f), "the glow over: look gone, fishing wait " + waitAfter + " s");
				Check(ref ok, water == null || Mathf.Abs(after.b - emissionBefore.b) < 0.005f, "the water's glow back to " + after);
			}
			finally
			{
				GlowSea.Reset();
				WorldOptions.Set(optionsBefore);
				SetHour(hourBefore);
			}
			if (ok) Log("PASS: glow sea"); else Fail("glow sea");
		}

		#endregion
	}
}
