using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		#region Readiness waits (instead of fixed sleeps)

		// 10-20: the world and island steps of the tests waited fixed times (3 s after a world came in, 2 s for an island
		// to settle, 1 s after removing one). They now wait until the thing is ready, with the old time or more as a
		// backstop: a check that never comes true costs no more than before, and one that comes true early saves the rest.

		/// <summary>Seconds since <paramref name="since"/> (realtime), for the readiness lines in the log.</summary>
		static string Took(float since) { return (Time.realtimeSinceStartup - since).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " s"; }

		/// <summary>
		/// An island that came is all there and still: no cave mountain waiting to be built over it, its rigidbodies asleep
		/// and the number of its objects the same for three looks 0.1 s apart (pieces that come over several frames
		/// keep it changing). At least <paramref name="min"/> s; at most <paramref name="max"/> s (the callers pass the fixed
		/// wait they had: never longer than before).
		/// </summary>
		static IEnumerator WaitIslandSettled(IslandWorldState.Entry e, float min = 0.5f, float max = 4f)
		{
			float start = Time.realtimeSinceStartup, end = start + max;
			int still = 0, lastCount = -1;
			while (Time.realtimeSinceStartup < end)
			{
				yield return new WaitForFixedUpdate();
				yield return new WaitForSecondsRealtime(0.1f);
				if (e == null || e.Root == null) yield break;
				int count = e.Root.GetComponentsInChildren<Transform>(true).Length;
				bool mountain = e.Root.GetComponentsInChildren<CaveMountain>(true).Any(m => m.Pending);
				bool moving = e.Root.GetComponentsInChildren<Rigidbody>().Any(r => !r.isKinematic && !r.IsSleeping() && r.GetComponentInParent<AI_NetworkBehaviour>() == null); // (animals walk about)
				still = !mountain && !moving && count == lastCount ? still + 1 : 0;
				lastCount = count;
				if (still >= 3 && Time.realtimeSinceStartup - start >= min) yield break;
			}
			Log("  (island '" + (e != null ? e.Name : "?") + "' still settling after " + Took(start) + ": went on)");
		}

		/// <summary>Removes these islands and waits until they are gone from the scene (and one physics step), at most
		/// <paramref name="max"/> s - instead of a fixed second.</summary>
		static IEnumerator RemoveIslandsAndWait(IList<int> ids, float max = 2f)
		{
			var set = new HashSet<int>(ids);
			List<GameObject> roots = IslandWorldState.Islands.Where(x => set.Contains(x.Id) && x.Root != null).Select(x => x.Root).ToList();
			IslandWorldState.RemoveIds(ids, true);
			IslandCache.Forget();
			float end = Time.realtimeSinceStartup + max;
			do yield return null; while (Time.realtimeSinceStartup < end && roots.Any(r => r != null));
			yield return new WaitForFixedUpdate();
		}

		#endregion
	}
}
