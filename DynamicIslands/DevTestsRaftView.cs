using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		[ConsoleCommand(name: "CIRaftView", docs: "Dev, editor: a picture of one of Raft's own islands as CIViewAt takes one of the island being made - the island's scene copied without its scripts to where the editor's island is (its sea on the editor's sea), the editor's island hidden for the picture: shot_<name>.png. To judge a generated island beside Raft's own (how thick its corals and bushes look). CIRaftView <part of a scene name or label, _ for spaces> <x> <z> <h> <yaw> <pitch> <dist> <name> [all: also what the scene has switched off]")]
		public static void RaftViewCommand(string[] args)
		{
			if (args == null || args.Length < 8 || !DynamicIslands.InEditor() || Camera.main == null) { Fail("CIRaftView <scene> <x> <z> <h> <yaw> <pitch> <dist> <name> (in the editor)"); return; }
			string want = args[0].Replace('_', ' ');
			List<string> scenes = PlaceableCatalog.LandmarkSceneNames();
			// (a label first - "Small island 1" - then any scene whose name has the words in it)
			string scene = scenes.FirstOrDefault(s => string.Equals(RaftIslands.LabelOf(s), want, StringComparison.OrdinalIgnoreCase))
				?? scenes.FirstOrDefault(s => RaftIslands.LabelOf(s).IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0);
			if (scene == null) { Fail("no Raft island scene like '" + want + "' (" + string.Join(", ", scenes.Select(RaftIslands.LabelOf).ToArray()) + ")"); return; }
			StartTest(PlaceableCatalog.VisitScene(scene, s => RaftViewShot(s, args)));
		}

		static IEnumerator RaftViewShot(Scene src, string[] args)
		{
			Vector2 mid = EditorLandCentre();
			float sea = DynamicIslands.EditorWaterLevel;
			var holder = new GameObject("CI_RaftViewCopy");
			holder.SetActive(false);
			Terrain own = terraineditor.terrain;
			GameObject placed = GameObject.Find("PlacedObjects");
			bool ownShown = own != null && own.enabled, placedShown = placed != null && placed.activeSelf;
			EditorCamera ec = Camera.main.GetComponent<EditorCamera>();
			try
			{
				// A copy of the island without its scripts, sounds, effects or physics bodies (as CIMeasureUnderwater makes one)
				foreach (GameObject root in src.GetRootGameObjects())
				{
					GameObject copy = UnityEngine.Object.Instantiate(root, holder.transform);
					copy.name = root.name;
					copy.SetActive(true);
				}
				PlaceableCatalog.StripScripts(holder);
				foreach (Joint j in holder.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
				foreach (ParticleSystem ps in holder.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(ps);
				foreach (Component c in holder.GetComponentsInChildren<Component>(true).Where(c => c is ParticleSystemRenderer || c is AudioSource || c is Rigidbody || c is Animator || c is Animation ||
					c is Light || c is Camera || c is ReflectionProbe || c is AudioListener || c is UnityEngine.AI.NavMeshAgent || c is UnityEngine.AI.NavMeshObstacle || c is Cloth || c is TrailRenderer || c is LineRenderer || c is WindZone).ToList())
					if (c != null) UnityEngine.Object.DestroyImmediate(c);
				// (no colliders in the editor's physics: the editor's own ground probes must not find Raft's island)
				foreach (Collider c in holder.GetComponentsInChildren<Collider>(true).ToList())
					if (c != null && !(c is TerrainCollider)) c.enabled = false;
				// (its LOD groups as the editor's own objects have them: the last level kept down to a small part of the screen -
				// Raft's corals vanished at 120 m while the editor's stayed, a picture that wasn't fair to compare)
				foreach (LODGroup group in holder.GetComponentsInChildren<LODGroup>(true))
				{
					LOD[] lods = group.GetLODs();
					if (lods.Length == 0 || lods[lods.Length - 1].screenRelativeTransitionHeight <= 0.01f) continue;
					lods[lods.Length - 1].screenRelativeTransitionHeight = 0.005f;
					for (int i = lods.Length - 2; i >= 0; i--)
						if (lods[i].screenRelativeTransitionHeight <= lods[i + 1].screenRelativeTransitionHeight) lods[i].screenRelativeTransitionHeight = lods[i + 1].screenRelativeTransitionHeight + 0.001f;
					group.SetLODs(lods);
				}
				// (what of it is switched off in the scene - and with "all" switched on: things Raft shows only near the player)
				var off = holder.GetComponentsInChildren<Transform>(true).Where(t => t != holder.transform && !t.gameObject.activeSelf).ToList();
				int offCorals = off.Count(t => RaftUnderwater.CategoryOf(PlaceableCatalog.CleanName(t.name)) == IslandGenerator.CatWater);
				int allCorals = holder.GetComponentsInChildren<Transform>(true).Count(t => RaftUnderwater.CategoryOf(PlaceableCatalog.CleanName(t.name)) == IslandGenerator.CatWater);
				Log("  " + off.Count + " objects switched off in the scene, " + offCorals + " of its " + allCorals + " corals and plants");
				if (args.Length > 8 && args[8] == "all") foreach (Transform t in off) t.gameObject.SetActive(true);
				holder.SetActive(true);
				yield return null;

				// Its land's middle: the ground's renderers above the sea (Raft's scenes have their sea at y = 0)
				Bounds b = new Bounds();
				bool any = false;
				foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
				{
					if (!IsGround(r.transform, holder.transform) || r.bounds.max.y < 0.5f || r.bounds.size.x > 2000f) continue;
					if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
				}
				foreach (Terrain t in holder.GetComponentsInChildren<Terrain>())
				{
					Bounds tb = new Bounds(t.transform.position + t.terrainData.size / 2f, t.terrainData.size);
					if (!any) { b = tb; any = true; } else b.Encapsulate(tb);
				}
				if (!any) { Fail("no ground in " + src.name); yield break; }
				holder.transform.position = new Vector3(mid.x - b.center.x, sea, mid.y - b.center.z);
				Log("  " + RaftIslands.LabelOf(src.name) + " (" + src.name + "): its middle at " + b.center.x.ToString("F0") + " " + b.center.z.ToString("F0") + ", set on the editor's island");

				if (own != null) own.enabled = false;
				if (placed != null) placed.SetActive(false);
				Vector3 at = new Vector3(mid.x + F(args[1]), sea + F(args[3]), mid.y + F(args[2]));
				Quaternion look = Quaternion.Euler(F(args[5]), F(args[4]), 0f);
				if (ec != null) ec.enabled = false;
				Camera.main.transform.SetPositionAndRotation(at - look * Vector3.forward * F(args[6]), look);
				yield return new WaitForSecondsRealtime(1.2f);
				Screenshot(new[] { args[7] });
				yield return new WaitForSecondsRealtime(0.5f);
				Log("PASS: raft view " + args[7]);
			}
			finally
			{
				if (own != null) own.enabled = ownShown;
				if (placed != null) placed.SetActive(placedShown);
				if (ec != null) ec.enabled = true;
				UnityEngine.Object.Destroy(holder);
			}
		}
	}
}
