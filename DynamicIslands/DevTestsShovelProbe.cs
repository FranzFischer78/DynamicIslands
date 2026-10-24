using System;
using System.Linq;
using System.Reflection;
using DynamicIslands.Editor;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		/// <summary>How Raft's shovel finds dirt (TODO: dirt on islands): the fields of the types with a diggable layer/tag or
		/// a terrain id for dirt, their values on loaded objects, and the tag, layer and textures of the terrains around.</summary>
		[ConsoleCommand(name: "CIProbeShovel", docs: "Dev: how Raft's shovel finds dirt - the diggable layer and tag, terrain ids, and the terrains' tags and layers")]
		public static void ProbeShovel(string[] args)
		{
			const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
			Assembly raft = typeof(Network_Player).Assembly;
			Type[] types;
			try { types = raft.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
			foreach (Type t in types)
			{
				FieldInfo[] fs = t.GetFields(all).Where(f => f.Name.IndexOf("diggable", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.StartsWith("TerrainID_") || f.Name == "Shovelable" || f.Name.IndexOf("TreasurePointDirt", StringComparison.Ordinal) >= 0).ToArray();
				if (fs.Length == 0) continue;
				Log("type " + t.FullName + " (base " + (t.BaseType != null ? t.BaseType.Name : "-") + ")");
				foreach (FieldInfo f in fs)
				{
					string v = "";
					try
					{
						if (f.IsStatic) v = Show(f.GetValue(null));
						else if (typeof(UnityEngine.Object).IsAssignableFrom(t))
						{
							UnityEngine.Object o = Resources.FindObjectsOfTypeAll(t).FirstOrDefault();
							v = o != null ? Show(f.GetValue(o)) + " (on " + o.name + ")" : "(none loaded)";
						}
					}
					catch (Exception e) { v = "? " + e.Message; }
					Log("  " + f.FieldType.Name + " " + f.Name + " = " + v);
				}
			}
			foreach (MethodInfo m in types.SelectMany(t => t.GetMethods(all).Where(x => x.DeclaringType == t && (x.Name.IndexOf("Shovel", StringComparison.Ordinal) >= 0 || x.Name.IndexOf("Dig", StringComparison.Ordinal) >= 0))))
				Log("method " + m.DeclaringType.FullName + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray()) + ")");
			foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.CompareTag("Pickup_Shovel")).Take(40))
			{
				Transform r = g.transform; while (r.parent != null) r = r.parent;
				Component[] cs = g.GetComponents<Component>();
				Log("shovel spot " + g.name + " in " + r.name + " scene '" + g.scene.name + "' layer " + LayerMask.LayerToName(g.layer) + " comps: " + string.Join(", ", cs.Select(c => c != null ? c.GetType().Name : "-").ToArray()));
				foreach (Component c in cs.Where(c => c != null && c.GetType().Assembly == raft))
					foreach (FieldInfo f in c.GetType().GetFields(all).Where(f => !f.IsStatic).Take(12))
					{ try { Log("    " + c.GetType().Name + "." + f.Name + " = " + Show(f.GetValue(c))); } catch { } }
			}
			foreach (string n in global::DynamicIslands.Editor.PlaceableCatalog.Names.Where(n => { GameObject p = global::DynamicIslands.Editor.PlaceableCatalog.Get(n); return p != null && p.GetComponentsInChildren<Transform>(true).Any(x => x.CompareTag("Pickup_Shovel")); }).Take(30))
				Log("catalog has a shovel spot: " + n);
			foreach (Terrain t in Terrain.activeTerrains)
			{
				TerrainData d = t.terrainData;
				Log("terrain " + t.name + " tag=" + t.gameObject.tag + " layer=" + LayerMask.LayerToName(t.gameObject.layer) + " (" + t.gameObject.layer + ") at " + t.transform.position + " layers: " +
					(d != null && d.terrainLayers != null ? string.Join(", ", d.terrainLayers.Select(l => l != null ? l.name : "-").ToArray()) : "-") + " parent " + (t.transform.parent != null ? t.transform.parent.name : "-"));
			}
		}

		[ConsoleCommand(name: "CIProbeWorking", docs: "Dev, in game (host): spawns an island with Raft's vines and zipline lines and lists their scripts and the scripts' methods - to make Raft's quest items work on custom islands")]
		public static void ProbeWorking(string[] args) { StartTest(ProbeWorkingRoutine()); }

		static System.Collections.IEnumerator ProbeWorkingRoutine()
		{
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!raft.HasValue) { Fail("in a world"); yield break; }
			string[] names = { "ChoppableVines", "ZiplinePath", "ZiplinePath_Landmark" };
			yield return global::DynamicIslands.Editor.PlaceableCatalog.EnsureLoaded(names.ToList());
			foreach (string n in names)
			{
				GameObject p = global::DynamicIslands.Editor.PlaceableCatalog.Get(n);
				if (p == null) { Log("probe: " + n + " not in the catalog"); continue; }
				foreach (Transform t in p.GetComponentsInChildren<Transform>(true))
				{
					MonoBehaviour[] ms = t.GetComponents<MonoBehaviour>().Where(m => m != null).ToArray();
					Collider[] cs = t.GetComponents<Collider>();
					if (ms.Length == 0 && cs.Length == 0) continue;
					Log("probe: " + n + "/" + t.name + " tag=" + t.tag + " layer=" + LayerMask.LayerToName(t.gameObject.layer) + " scripts: " + string.Join(", ", ms.Select(m => m.GetType().Name).ToArray()) + " colliders: " + cs.Length);
					foreach (MonoBehaviour m in ms.Where(m => !(m is global::DynamicIslands.Editor.BatchAnchor)))
					{
						const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
						Log("    " + m.GetType().Name + " : " + (m.GetType().BaseType != null ? m.GetType().BaseType.Name : "") + " methods: " + string.Join(", ", m.GetType().GetMethods(all).Select(x => x.Name).Distinct().Take(30).ToArray()));
						foreach (FieldInfo f in m.GetType().GetFields(all).Take(14)) { try { Log("      " + f.FieldType.Name + " " + f.Name + " = " + Show(f.GetValue(m))); } catch { } }
					}
				}
			}
			foreach (string zn in new[] { "ZiplinePath", "ZiplinePath_Landmark" })
			{
				GameObject z = PlaceableCatalog.Get(zn);
				MeshPath_Zipline_Landmark ml = z != null ? z.GetComponentInChildren<MeshPath_Zipline_Landmark>(true) : null;
				if (ml == null) continue;
				FieldInfo fa = typeof(MeshPath_Zipline_Landmark).GetField("pointA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), fb = typeof(MeshPath_Zipline_Landmark).GetField("pointB", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				Transform pa = fa != null ? fa.GetValue(ml) as Transform : null, pb = fb != null ? fb.GetValue(ml) as Transform : null;
				if (pa != null && pb != null) Log("probe zip " + zn + ": A " + z.transform.InverseTransformPoint(pa.position) + " B " + z.transform.InverseTransformPoint(pb.position) + " length " + Vector3.Distance(pa.position, pb.position).ToString("F1") + " scale " + z.transform.localScale);
				Renderer[] rs = z.GetComponentsInChildren<Renderer>(true);
				if (rs.Length > 0) { Bounds bb = rs[0].bounds; foreach (Renderer r in rs) bb.Encapsulate(r.bounds); Log("probe zip " + zn + " bounds " + (bb.min - z.transform.position) + " .. " + (bb.max - z.transform.position)); }
				foreach (Transform t in z.GetComponentsInChildren<Transform>(true)) Log("probe zip   " + t.name + " at " + z.transform.InverseTransformPoint(t.position));
			}
			GameObject v = PlaceableCatalog.Get("ChoppableVines");
			if (v != null) foreach (Transform t in v.GetComponentsInChildren<Transform>(true)) Log("probe vines: " + t.name + " parent " + (t.parent != null ? t.parent.name : "-") + " comps: " + string.Join(", ", t.GetComponents<Component>().Select(c => c != null ? c.GetType().Name : "-").ToArray()));
			Type[] types;
			try { types = typeof(Network_Player).Assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
			foreach (Type ty in types)
				foreach (MethodInfo m in ty.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
					.Where(m => m.Name == "AttachToZipline" || m.Name == "AttachPlayerToZipline" || m.Name.Contains("Machete") || m.Name == "OnChop" || ty.Name.Contains("Macheteable")))
					Log("probe method " + ty.FullName + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name).ToArray()) + ")");
			foreach (Type ty in types.Where(x => x.Name.Contains("TreasurePoint") || x.Name.Contains("MetalDetector") || x.Name == "Shovel"))
			{
				Log("probe ttype " + ty.FullName + " : " + (ty.BaseType != null ? ty.BaseType.Name : ""));
				foreach (MethodInfo m in ty.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
					Log("probe tm " + ty.Name + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name).ToArray()) + ")");
				foreach (FieldInfo fi in ty.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
				{
					string val = "";
					try { if (fi.IsStatic) val = Show(fi.GetValue(null)); else if (typeof(UnityEngine.Object).IsAssignableFrom(ty)) { UnityEngine.Object o = Resources.FindObjectsOfTypeAll(ty).FirstOrDefault(); if (o != null) val = Show(fi.GetValue(o)) + " (on " + o.name + ")"; } } catch { }
					Log("probe tf " + ty.Name + "." + fi.Name + " : " + fi.FieldType.Name + " = " + val);
				}
			}
			foreach (Type ty in types.Where(x => x.Name.Contains("Machete") || x.Name.Contains("Zipline")))
				Log("probe type " + ty.FullName + " : " + (ty.BaseType != null ? ty.BaseType.Name : ""));
			Log("probe done");
		}

		static string Show(object v)
		{
			if (v == null) return "null";
			if (v is LayerMask) { int m = ((LayerMask)v).value; return "mask " + m + " [" + string.Join(", ", Enumerable.Range(0, 32).Where(i => (m & (1 << i)) != 0).Select(i => LayerMask.LayerToName(i)).ToArray()) + "]"; }
			if (v is Array) return "[" + string.Join(", ", ((Array)v).Cast<object>().Select(Show).ToArray()) + "]";
			return v.ToString();
		}
	}
}
