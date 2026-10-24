using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// Pictures for checking by eye that every craftable of the world option "Extra upgrades" looks good next to its Raft base
	/// item (shot_up_*.png in the mod folder): CIUpgradeShots (the models side by side, base left), CIUpgradeIcons (one sheet of
	/// the icons), CIUpgradeMenuShot (the crafting menu with the upgrades and the upgrade table's menu).
	/// </summary>
	public static partial class DevTests
	{
		#region Helpers

		class UsRes { public bool Ok; }

		class UsRun
		{
			public Network_Player Player;
			public Vector3 Spot;
			public float Yaw;
			public int Shots, Fails;
			/// <summary>"close": also a close picture of each upgrade alone (its glints).</summary>
			public bool Close;
			public List<Block> Blocks = new List<Block>();
			public List<GameObject> Objs = new List<GameObject>();
		}

		static string UsName(ExtraUpgrades.Upgrade u) { return u.Name.StartsWith("DI_") ? u.Name.Substring(3) : u.Name; }

		static string UsPath(string shotName) { return Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "shot_" + shotName + ".png")); }

		static void UsArgs(string[] args, out string filter, out float yaw, out bool flag)
		{
			filter = null; yaw = 0f; flag = false;
			foreach (string a in args ?? new string[0])
			{
				float y;
				if (a.StartsWith("yaw=", StringComparison.OrdinalIgnoreCase) && float.TryParse(a.Substring(4), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)) yaw = y;
				else if (a.Equals("all", StringComparison.OrdinalIgnoreCase)) flag = true;
				else filter = filter == null ? a : filter + " " + a;
			}
		}

		static bool UsMatch(ExtraUpgrades.Upgrade u, string filter)
		{
			return filter == null || u.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || u.Display.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		static void UsSetHour(float hour)
		{
			try
			{
				var sky = ComponentManager<UnityEngine.AzureSky.AzureSkyController>.Value ?? UnityEngine.Object.FindObjectOfType<UnityEngine.AzureSky.AzureSkyController>();
				if (sky != null && sky.timeOfDay != null) sky.timeOfDay.hour = hour;
			}
			catch { }
		}

		static float UsGetHour()
		{
			try
			{
				var sky = ComponentManager<UnityEngine.AzureSky.AzureSkyController>.Value ?? UnityEngine.Object.FindObjectOfType<UnityEngine.AzureSky.AzureSkyController>();
				if (sky != null && sky.timeOfDay != null) return sky.timeOfDay.hour;
			}
			catch { }
			return -1f;
		}

		/// <summary>The bounds of everything drawn under an object (active mesh renderers).</summary>
		static bool UsBounds(GameObject go, out Bounds b)
		{
			b = new Bounds();
			bool any = false;
			if (go == null) return false;
			foreach (Renderer r in go.GetComponentsInChildren<Renderer>(false))
			{
				if (r == null || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
				if (r.bounds.size.sqrMagnitude < 1e-8f) continue;
				if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
			}
			return any;
		}

		static void UsRemove(UsRun run)
		{
			foreach (Block b in run.Blocks) { try { if (b != null) BlockCreator.RemoveBlockNetwork(b, null, true); } catch { } }
			run.Blocks.Clear();
			foreach (GameObject o in run.Objs) { try { if (o != null) UnityEngine.Object.Destroy(o); } catch { } }
			run.Objs.Clear();
		}

		static Block UsPlace(UsRun run, Item_Base item)
		{
			try
			{
				Block b = run.Player.BlockCreator.CreateBlockCheat(item, run.Spot, Vector3.zero, DPS.Default, 0);
				if (b != null) run.Blocks.Add(b);
				return b;
			}
			catch (Exception e) { Log("  (placing " + (item != null ? item.UniqueName : "?") + ": " + e.Message + ")"); return null; }
		}

		/// <summary>Moves a and b (their bounds a, ba / bb) so they stand side by side round the spot, 1.5 widths apart (a on the left as the camera sees them); the bounds of both.</summary>
		static Bounds UsArrange(GameObject a, Bounds ba, GameObject b, Bounds bb, Vector3 spot, float yaw)
		{
			Quaternion q = Quaternion.Euler(0f, yaw, 0f);
			Vector3 right = q * Vector3.right;
			float wa = Mathf.Max(ba.size.x, ba.size.z), wb = Mathf.Max(bb.size.x, bb.size.z);
			float apart = 1.5f * Mathf.Max(wa, wb);
			Vector3 da = spot - right * apart * 0.5f - ba.center, db = spot + right * apart * 0.5f - bb.center;
			a.transform.position += da;
			b.transform.position += db;
			Physics.SyncTransforms();
			ba.center += da; bb.center += db;
			Bounds all = ba;
			all.Encapsulate(bb);
			return all;
		}

		/// <summary>One picture of a box from the front (the camera yaw degrees round), 2 m away at the least, slightly above, all of the box in frame; waits until the file is there.</summary>
		static IEnumerator UsShoot(UsRun run, string label, Bounds all, float yaw, string shotName, float minDist)
		{
			string path = UsPath(shotName);
			try { if (File.Exists(path)) File.Delete(path); } catch { }
			UsSetHour(12f);
			Quaternion q = Quaternion.Euler(0f, yaw, 0f);
			Vector3 right = q * Vector3.right, fwd = q * Vector3.forward;
			Vector3 size = all.size;
			float w = Mathf.Abs(right.x) * size.x + Mathf.Abs(right.z) * size.z, d = Mathf.Abs(fwd.x) * size.x + Mathf.Abs(fwd.z) * size.z, h = size.y;
			const float vt = 0.577f; // (CameraShot's field of view: 60 degrees up and down, 16:9)
			float dist = Mathf.Max(minDist, Mathf.Max(w * 0.5f * 1.15f / (vt * 16f / 9f), h * 0.5f * 1.25f / vt) + d * 0.5f);
			Vector3 from = all.center - fwd * dist + Vector3.up * Mathf.Max(0.3f, h * 0.2f);
			yield return null; yield return null;
			yield return CameraShot(from, all.center, shotName);
			var res = new UsRes();
			yield return UsWaitFile(path, res);
			if (res.Ok) { run.Shots++; Log("SHOT up " + label + " " + Path.GetFileName(path)); }
			else { run.Fails++; Fail("upgrade shots: " + label + ": no picture " + Path.GetFileName(path)); }
		}

		/// <summary>The upgrade has its glints (ExtraUpgrades.Shine: one playing particle system), the base item none.</summary>
		static void UsGlintCheck(UsRun run, string n, GameObject baseObj, GameObject upObj)
		{
			Func<GameObject, int> glints = go => go == null ? 0 : go.GetComponentsInChildren<ParticleSystem>(true).Count(p => p.name == ExtraUpgrades.ShineName);
			int ga = glints(baseObj), gb = glints(upObj);
			ParticleSystem ps = upObj == null ? null : upObj.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == ExtraUpgrades.ShineName);
			Log("  (" + n + ": glints base " + ga + ", upgrade " + gb + (ps != null ? ", playing " + ps.isPlaying + ", rate " + ps.emission.rateOverTime.constant.ToString("F1") + ", " + ps.particleCount + " alive, shader " + UsShader(ps) + ", layer " + ps.gameObject.layer + ", at " + ps.transform.position.ToString("F2") + ", box " + ps.shape.scale.ToString("F2") + ", lossy " + ps.transform.lossyScale.ToString("F2") : "") + ")");
			if (gb != 1 || ga != 0 || ps == null || !ps.isPlaying) { run.Fails++; Fail("upgrade shots: " + n + ": glints wrong (base " + ga + ", upgrade " + gb + (ps != null && !ps.isPlaying ? ", not playing" : "") + ")"); }
		}

		static string UsShader(ParticleSystem ps)
		{
			var r = ps.GetComponent<ParticleSystemRenderer>();
			return r == null ? "no renderer" : (r.enabled ? "" : "off ") + (r.sharedMaterial == null ? "none" : r.sharedMaterial.shader.name);
		}

		/// <summary>With "close": the upgrade alone from near, after its glints have had time to show.</summary>
		static IEnumerator UsCloseShot(UsRun run, string n, GameObject upObj)
		{
			if (!run.Close) yield break;
			yield return new WaitForSeconds(1.5f);
			Bounds bb;
			if (!UsBounds(upObj, out bb)) yield break;
			ParticleSystem ps = upObj.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == ExtraUpgrades.ShineName);
			if (ps != null) Log("  (" + n + " close: " + ps.particleCount + " glints alive, model " + bb.center.ToString("F2") + " size " + bb.size.ToString("F2") + ")");
			yield return UsShoot(run, n + " close", bb, run.Yaw, "up_" + n + "_close", 0.9f);
		}

		static IEnumerator UsWaitFile(string path, UsRes res)
		{
			res.Ok = false;
			for (float until = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < until; )
			{
				bool there = false;
				try { there = File.Exists(path) && new FileInfo(path).Length > 0; } catch { }
				if (there) { res.Ok = true; yield break; }
				yield return null;
			}
		}

		/// <summary>A world copy of a model: scripts, animators and colliders off, on the default layer, at the origin of its own.</summary>
		static GameObject UsClone(GameObject src, string name, Color? tint)
		{
			GameObject hold = new GameObject("US_hold");
			hold.SetActive(false);
			try
			{
				GameObject cl = UnityEngine.Object.Instantiate(src, hold.transform, false);
				cl.name = name;
				// (a skinned mesh keeps the player's bones and would be drawn on the player: baked into a plain mesh)
				Renderer[] from = src.GetComponentsInChildren<Renderer>(true), to = cl.GetComponentsInChildren<Renderer>(true);
				// (a copy loses the property blocks the mod tints with: carried over, so the picture shows the real look)
				for (int i = 0; i < from.Length && i < to.Length; i++)
				{
					var pb = new MaterialPropertyBlock();
					from[i].GetPropertyBlock(pb);
					if (!pb.isEmpty) to[i].SetPropertyBlock(pb);
					for (int j = 0; j < from[i].sharedMaterials.Length && from[i].sharedMaterials.Length > 1; j++)
					{
						pb = new MaterialPropertyBlock();
						from[i].GetPropertyBlock(pb, j);
						if (!pb.isEmpty) to[i].SetPropertyBlock(pb, j);
					}
				}
				var bakedAt = new List<Transform>();
				for (int i = 0; i < from.Length && i < to.Length; i++)
				{
					SkinnedMeshRenderer fs = from[i] as SkinnedMeshRenderer, ts = to[i] as SkinnedMeshRenderer;
					if (fs == null || ts == null || fs.sharedMesh == null) continue;
					var baked = new Mesh();
					fs.BakeMesh(baked);
					Material[] mats = ts.sharedMaterials;
					GameObject g = ts.gameObject;
					UnityEngine.Object.DestroyImmediate(ts);
					g.AddComponent<MeshFilter>().sharedMesh = baked;
					g.AddComponent<MeshRenderer>().sharedMaterials = mats;
					bakedAt.Add(g.transform);
				}
				foreach (MonoBehaviour m in cl.GetComponentsInChildren<MonoBehaviour>(true)) { try { UnityEngine.Object.DestroyImmediate(m); } catch { } }
				foreach (Animator m in cl.GetComponentsInChildren<Animator>(true)) { try { UnityEngine.Object.DestroyImmediate(m); } catch { } }
				foreach (Rigidbody m in cl.GetComponentsInChildren<Rigidbody>(true)) { try { UnityEngine.Object.DestroyImmediate(m); } catch { } }
				foreach (Collider m in cl.GetComponentsInChildren<Collider>(true)) { try { UnityEngine.Object.DestroyImmediate(m); } catch { } }
				foreach (AudioSource m in cl.GetComponentsInChildren<AudioSource>(true)) { try { UnityEngine.Object.DestroyImmediate(m); } catch { } }
				foreach (Light m in cl.GetComponentsInChildren<Light>(true)) m.enabled = false;
				foreach (Transform t in cl.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
				foreach (Renderer r in cl.GetComponentsInChildren<Renderer>(true))
				{
					if (r is MeshRenderer || r is SkinnedMeshRenderer) { r.enabled = true; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
					SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
					if (sk != null) sk.updateWhenOffscreen = true;
				}
				cl.SetActive(true);
				cl.transform.SetParent(null, true);
				// (BakeMesh already applies the renderer's scale: the baked part must not be scaled again)
				foreach (Transform t in bakedAt)
				{
					Vector3 ls = t.lossyScale, l = t.localScale;
					if (Mathf.Abs(ls.x) > 1e-6f && Mathf.Abs(ls.y) > 1e-6f && Mathf.Abs(ls.z) > 1e-6f) t.localScale = new Vector3(l.x / ls.x, l.y / ls.y, l.z / ls.z);
				}
				if (tint.HasValue) ObjectProps.ApplyTint(cl, tint.Value, 1f);
				return cl;
			}
			catch (Exception e) { Log("  (copy of " + name + ": " + e.Message + ")"); return null; }
			finally { UnityEngine.Object.Destroy(hold); }
		}

		static string UsWornModels(Equipment e)
		{
			var parts = new List<string>();
			foreach (string f in new[] { "localModel", "remoteModel" })
			{
				Transform m = Traverse.Create(e).Field(f).GetValue<Transform>();
				if (m == null) { parts.Add(f + " none"); continue; }
				Renderer[] rs = m.GetComponentsInChildren<Renderer>(true);
				parts.Add(f + (m.gameObject.activeInHierarchy ? " shown" : " hidden") + ", " + rs.Count(r => r.enabled) + "/" + rs.Length + " renderers on");
			}
			return string.Join("; ", parts.ToArray());
		}

		/// <summary>Glint systems on an Equipment's worn models (only playing ones, or any).</summary>
		static int UsWornGlints(Equipment e, bool playing)
		{
			int n = 0;
			foreach (string f in new[] { "localModel", "remoteModel" })
			{
				Transform m = Traverse.Create(e).Field(f).GetValue<Transform>();
				Transform g = m != null ? m.Find(ExtraUpgrades.ShineName) : null;
				ParticleSystem ps = g != null ? g.GetComponent<ParticleSystem>() : null;
				if (ps != null && (!playing || ps.isPlaying)) n++;
			}
			return n;
		}

		static Equipment UsEquipment(Network_Player p, Item_Base item)
		{
			try
			{
				var pe = p.GetComponentInChildren<PlayerEquipment>(true);
				Equipment[] eq = pe != null ? Traverse.Create(pe).Field("equipment").GetValue<Equipment[]>() : null;
				return eq != null ? eq.FirstOrDefault(e => e != null && e.equipableItem == item) : null;
			}
			catch { return null; }
		}

		static void UsInitEquipment(Network_Player p, Equipment e)
		{
			try
			{
				if (Traverse.Create(e).Field("localModel").GetValue<Transform>() == null && Traverse.Create(e).Field("remoteModel").GetValue<Transform>() == null)
				{
					var m = AccessTools.Method(e.GetType(), "Initialize");
					if (m != null) m.Invoke(e, new object[] { p });
				}
			}
			catch (Exception ex) { Log("  (initializing " + e.name + ": " + (ex.InnerException ?? ex).Message + ")"); }
		}

		/// <summary>The world model of a hand-held or worn item (the hand model, or the worn model with a renderer), or null.</summary>
		static GameObject UsModelOf(UsRun run, ExtraUpgrades.Upgrade u, bool upgrade)
		{
			try
			{
				if (u.Held != null)
				{
					var c = run.Player.GetComponentInChildren<UseItemController>(true);
					var dict = c != null ? Traverse.Create(c).Field("connectionDictionary").GetValue<Dictionary<string, ItemConnection>>() : null;
					ItemConnection k;
					if (dict != null && dict.TryGetValue(upgrade ? u.Name : u.BaseName, out k) && k != null) return k.obj;
					return null;
				}
				// (a worn upgrade shares its base item's model: the same one for both, tinted for the upgrade)
				Equipment e = UsEquipment(run.Player, u.Base);
				if (e == null) return null;
				UsInitEquipment(run.Player, e);
				foreach (string f in new[] { "remoteModel", "localModel" })
				{
					Transform m = Traverse.Create(e).Field(f).GetValue<Transform>();
					if (m != null && m.GetComponentInChildren<Renderer>(true) != null) return m.gameObject;
				}
			}
			catch (Exception ex) { Log("  (model of " + u.Display + ": " + ex.Message + ")"); }
			return null;
		}

		#endregion

		#region CIUpgradeShots

		[ConsoleCommand(name: "CIUpgradeShots", docs: "Dev, world (host): for every craftable of the world option Extra upgrades (the upgrades, the upgrade research table, the large battery) one picture shot_up_<Name>.png (<Name> without DI_) of the Raft base item on the left and the upgrade on the right, 1.5 widths apart, floating over the raft, in midday light, no HUD: placeables as blocks; hand-held and worn ones as copies of their world/hand models (the mod's tint on the upgrade); the large battery in the slots of two identical machines (shot_up_LargeBattery.png and the slots close up, _slot); worn head lights also equipped (shot_up_<Name>_held_base.png / _held_up.png). Logs '[CITEST] SHOT up <Name> <file>' per picture. CIUpgradeShots [name filter] [yaw=<degrees to turn the camera round the pieces, default 0: looking along +Z>]. Built and hand-held upgrades must have their glints (one playing DI_UpgradeShine, none on the base). 'close': also shot_up_<Name>_close.png, the upgrade alone from near")]
		public static void UpgradeShotsCommand(string[] args)
		{
			string filter; float yaw; bool flag;
			bool close = (args ?? new string[0]).Any(a => a.Equals("close", StringComparison.OrdinalIgnoreCase));
			UsArgs((args ?? new string[0]).Where(a => !a.Equals("close", StringComparison.OrdinalIgnoreCase)).ToArray(), out filter, out yaw, out flag);
			StartTest(UpgradeShotsRoutine(filter, yaw, close));
		}

		static IEnumerator UpgradeShotsRoutine(string filter, float yaw, bool close)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			if (player == null || !CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("upgrade shots: host, in a world"); yield break; }
			var run = new UsRun { Player = player, Spot = CustomIslandSpawner.RaftPosition.Value + new Vector3(0f, 22f, 0f), Yaw = yaw, Close = close };
			List<ExtraUpgrades.Upgrade> list = ExtraUpgrades.All.Where(u => UsMatch(u, filter)).ToList();
			if (list.Count == 0) { Fail("upgrade shots: no upgrade matches '" + filter + "'"); yield break; }
			float hour0 = UsGetHour();
			Log("upgrade shots: " + list.Count + " craftables, spot " + run.Spot.ToString("F1") + ", camera yaw " + yaw + ", hour " + hour0.ToString("F1") + " -> 12");
			try
			{
				foreach (ExtraUpgrades.Upgrade u in list)
				{
					if (u.Item == null || u.Base == null) { run.Fails++; Fail("upgrade shots: " + UsName(u) + ": not registered (Raft has no " + u.BaseName + "?)"); continue; }
					UsSetHour(12f);
					if (u.Name == ExtraUpgrades.LargeBatteryName) yield return UsBatteryShots(run, u);
					else if (u.Held != null || u.Worn != null || u.Wearing != null) yield return UsModelShots(run, u);
					else yield return UsBlockShots(run, u);
					UsRemove(run);
					yield return new WaitForSeconds(0.2f);
				}
			}
			finally { UsRemove(run); if (hour0 >= 0f) UsSetHour(hour0); }
			if (run.Fails == 0 && run.Shots > 0) Log("PASS: upgrade shots (" + run.Shots + " pictures of " + list.Count + " craftables)");
			else Fail("upgrade shots: " + run.Fails + " failed, " + run.Shots + " pictures");
		}

		/// <summary>A placeable: Raft's block and the upgrade's block side by side.</summary>
		static IEnumerator UsBlockShots(UsRun run, ExtraUpgrades.Upgrade u)
		{
			string n = UsName(u);
			Block a = UsPlace(run, u.Base), b = UsPlace(run, u.Item);
			if (a == null || b == null) { run.Fails++; Fail("upgrade shots: " + n + ": could not place " + (a == null ? u.BaseName : u.Name)); yield break; }
			yield return new WaitForSeconds(0.4f);
			yield return null; yield return null; yield return null;
			Bounds ba, bb;
			if (!UsBounds(a.gameObject, out ba) || !UsBounds(b.gameObject, out bb)) { run.Fails++; Fail("upgrade shots: " + n + ": a placed block has no visible mesh"); yield break; }
			if (b.GetComponent<UpgradeBlock>() != null) UsGlintCheck(run, n, a.gameObject, b.gameObject);
			Bounds all = UsArrange(a.gameObject, ba, b.gameObject, bb, run.Spot, run.Yaw);
			yield return UsShoot(run, n, all, run.Yaw, "up_" + n, 2f);
			yield return UsCloseShot(run, n, b.gameObject);
		}

		/// <summary>A hand-held or worn item: copies of the base item's model and the upgrade's side by side; head lights also equipped.</summary>
		static IEnumerator UsModelShots(UsRun run, ExtraUpgrades.Upgrade u)
		{
			string n = UsName(u);
			// (a worn model is skinned to the player: copied while worn, so its bones hold a pose)
			Equipment wear = u.Held == null ? UsEquipment(run.Player, u.Base) : null;
			if (wear != null)
			{
				UsInitEquipment(run.Player, wear);
				try { if (!wear.Equipped) wear.Equip(null); } catch (Exception ex) { Log("  (putting on " + u.BaseName + ": " + (ex.InnerException ?? ex).Message + ")"); }
				yield return new WaitForSeconds(0.4f);
			}
			GameObject sa = UsModelOf(run, u, false), sb = UsModelOf(run, u, true);
			GameObject a = sa != null ? UsClone(sa, "US_base_" + n, null) : null, b = sb != null ? UsClone(sb, "US_up_" + n, null) : null;
			// (a held upgrade's copy keeps its own look; a worn one shares the base's model: given the look it has while worn)
			if (b != null && u.Held == null) { ExtraUpgrades.Untint(b); ExtraUpgrades.TintAll(b, u.Tint, u.Whiten); ExtraUpgrades.Shine(b, u.Tint); }
			if (wear != null) { try { if (wear.Equipped) wear.UnEquip(); } catch { } }
			if (sa == null || sb == null) { run.Fails++; Fail("upgrade shots: " + n + ": no model to copy (" + (sa == null ? u.BaseName : u.Name) + ")"); yield break; }
			if (a != null) run.Objs.Add(a);
			if (b != null) run.Objs.Add(b);
			if (a == null || b == null) { run.Fails++; Fail("upgrade shots: " + n + ": could not copy the model"); yield break; }
			a.transform.position = run.Spot; b.transform.position = run.Spot;
			yield return null; yield return null; yield return null;
			Bounds ba, bb;
			if (!UsBounds(a, out ba) || !UsBounds(b, out bb)) { run.Fails++; Fail("upgrade shots: " + n + ": the model has no visible mesh"); yield break; }
			Log("  (" + n + ": copied from " + sa.name + ": " + string.Join(", ", sa.GetComponentsInChildren<Renderer>(true).Select(r => r.GetType().Name + " " + r.name + " scale " + r.transform.lossyScale.ToString("F3") + (r.enabled ? "" : " off") + (r.gameObject.activeInHierarchy ? "" : " hidden")).ToArray()) + ")");
			Log("  (" + n + ": models " + ba.size.ToString("F2") + " and " + bb.size.ToString("F2") + ")");
			if (Mathf.Max(ba.size.x, ba.size.y, ba.size.z) < 0.05f || Mathf.Max(bb.size.x, bb.size.y, bb.size.z) < 0.05f) { run.Fails++; Fail("upgrade shots: " + n + ": the model copy is too small to see (under 5 cm)"); yield break; }
			if (u.Held != null) UsGlintCheck(run, n, a, b);
			Bounds all = UsArrange(a, ba, b, bb, run.Spot, run.Yaw);
			yield return UsShoot(run, n, all, run.Yaw, "up_" + n, 1.2f);
			if (u.Held != null) yield return UsCloseShot(run, n, b);
			UsRemove(run);
			// A worn item tinted only while worn: also really equipped
			if (u.Wearing != null || u.Worn != null)
			{
				yield return UsEquippedShot(run, u, "base", u.Base);
				yield return UsEquippedShot(run, u, "up", u.Item);
			}
		}

		static IEnumerator UsEquippedShot(UsRun run, ExtraUpgrades.Upgrade u, string tag, Item_Base item)
		{
			string n = UsName(u);
			Equipment e = UsEquipment(run.Player, item);
			if (e == null) { run.Fails++; Fail("upgrade shots: " + n + " " + tag + ": the player has no equipment of " + item.UniqueName); yield break; }
			UsInitEquipment(run.Player, e);
			bool onOk = true;
			try { if (!e.Equipped) e.Equip(null); }
			catch (Exception ex) { onOk = false; run.Fails++; Fail("upgrade shots: " + n + " " + tag + ": putting it on failed: " + (ex.InnerException ?? ex).Message); }
			if (!onOk) yield break;
			yield return new WaitForSeconds(0.4f);
			yield return null; yield return null; yield return null;
			// (Raft may hide one of the two worn models: glints on a hidden one start when it shows)
			int glints = UsWornGlints(e, false);
			Log("  (" + n + " " + tag + " worn: " + glints + " glint system(s), " + UsWornGlints(e, true) + " playing; " + UsWornModels(e) + ")");
			if (tag == "up" && glints == 0) { run.Fails++; Fail("upgrade shots: " + n + ": no glints while the upgrade is worn"); }
			if (tag == "base" && UsWornGlints(e, false) > 0) { run.Fails++; Fail("upgrade shots: " + n + ": the base item has glints while worn"); }
			Bounds b = new Bounds();
			bool found = false;
			foreach (string f in new[] { "localModel", "remoteModel" })
			{
				Transform m = Traverse.Create(e).Field(f).GetValue<Transform>();
				if (m != null && UsBounds(m.gameObject, out b)) { found = true; break; }
			}
			GameObject worn = null;
			if (found)
			{
				// (a copy of the worn model as tinted by the mod, at the spot: the first-person model moves with the camera)
				foreach (string f in new[] { "remoteModel", "localModel" })
				{
					Transform m = Traverse.Create(e).Field(f).GetValue<Transform>();
					if (m != null && m.GetComponentInChildren<Renderer>(true) != null) { worn = UsClone(m.gameObject, "US_worn_" + n, null); break; }
				}
				if (worn != null) { run.Objs.Add(worn); worn.transform.position = run.Spot; yield return null; yield return null; found = UsBounds(worn, out b); }
				else found = false;
			}
			if (!found) { run.Fails++; Fail("upgrade shots: " + n + " " + tag + ": worn, but no model is drawn (first- or third-person)"); }
			else
			{
				b.Expand(0.3f);
				yield return UsShoot(run, n + "_held_" + tag, b, run.Yaw, "up_" + n + "_held_" + tag, 0.8f);
			}
			UsRemove(run);
			try { if (e.Equipped) e.UnEquip(); } catch { }
			yield return null; yield return null;
			if (tag == "up" && UsWornGlints(e, false) > 0) { run.Fails++; Fail("upgrade shots: " + n + ": glints stay on the shared model after taking it off"); }
		}

		/// <summary>The large battery: two identical machines, Raft's battery in one slot and the large one in the other.</summary>
		static IEnumerator UsBatteryShots(UsRun run, ExtraUpgrades.Upgrade u)
		{
			string n = UsName(u);
			Item_Base large = u.Item, battery = ItemManager.GetItemByName("Battery");
			Item_Base machine = null;
			try
			{
				machine = ItemManager.GetAllItems().Where(i => i != null && i.settings_buildable != null).FirstOrDefault(i =>
				{
					try { Block[] p = i.settings_buildable.GetBlockPrefabs(); return p != null && p.Any(bk => bk != null && bk.GetComponentInChildren<Battery>(true) != null && bk.GetComponentInChildren<BatteryCharger>(true) == null); }
					catch { return false; }
				});
			}
			catch { }
			if (battery == null || machine == null) { run.Fails++; Fail("upgrade shots: " + n + ": no Battery or no machine with a battery slot"); yield break; }
			Block ma = UsPlace(run, machine), mb = UsPlace(run, machine);
			if (ma == null || mb == null) { run.Fails++; Fail("upgrade shots: " + n + ": could not place the machine"); yield break; }
			yield return new WaitForSeconds(0.4f);
			Battery sa = ma.GetComponentInChildren<Battery>(true), sb = mb.GetComponentInChildren<Battery>(true);
			if (sa == null || sb == null) { run.Fails++; Fail("upgrade shots: " + n + ": the machine has no battery slot"); yield break; }
			try
			{
				sa.Insert(null, battery.MaxUses, battery.UniqueIndex);
				sb.Insert(null, large.MaxUses, large.UniqueIndex);
			}
			catch (Exception ex) { run.Fails++; Fail("upgrade shots: " + n + ": putting the batteries in: " + ex.Message); yield break; }
			yield return new WaitForSeconds(0.6f);
			yield return null; yield return null;
			Bounds ba, bb;
			if (!UsBounds(ma.gameObject, out ba) || !UsBounds(mb.gameObject, out bb)) { run.Fails++; Fail("upgrade shots: " + n + ": the machine has no visible mesh"); yield break; }
			Bounds all = UsArrange(ma.gameObject, ba, mb.gameObject, bb, run.Spot, run.Yaw);
			yield return UsShoot(run, n, all, run.Yaw, "up_" + n, 2f);
			// the two slots close up
			GameObject m1 = UsBatteryModel(sa, battery), m2 = UsBatteryModel(sb, large);
			Bounds c1, c2;
			if (m1 != null && m2 != null && UsBounds(m1, out c1) && UsBounds(m2, out c2))
			{
				c1.Encapsulate(c2);
				c1.Expand(0.5f);
				yield return UsShoot(run, n + " (slots)", c1, run.Yaw, "up_" + n + "_slot", 0.8f);
			}
			else { run.Fails++; Fail("upgrade shots: " + n + ": the battery models are not shown in the slots (close-up skipped)"); }
		}

		static GameObject UsBatteryModel(Battery slot, Item_Base item)
		{
			try
			{
				ItemModelConnection c = slot.itemEnabler.GetObjectConnections().FirstOrDefault(x => x.item == item);
				return c != null && c.model != null ? c.model.gameObject : null;
			}
			catch { return null; }
		}

		#endregion

		#region CIUpgradeIcons

		[ConsoleCommand(name: "CIUpgradeIcons", docs: "Dev, world or menu: one sheet shot_up_icons.png of the icons of every craftable of the world option Extra upgrades: Raft's item and the upgrade side by side, three pairs to a line; the order (line, pair, names) in shot_up_icons.txt")]
		public static void UpgradeIconsCommand(string[] args) { StartTest(UpgradeIconsRoutine()); }

		static IEnumerator UpgradeIconsRoutine()
		{
			yield return null;
			const int cell = 128, perLine = 3, gap = 24;
			List<ExtraUpgrades.Upgrade> list = ExtraUpgrades.All.Where(u => u.Item != null && u.Base != null).ToList();
			if (list.Count == 0) { Fail("upgrade icons: no upgrade registered"); yield break; }
			int lines = (list.Count + perLine - 1) / perLine;
			int pairW = cell * 2 + gap;
			int W = pairW * perLine, H = (cell + 8) * lines;
			var sheet = new Texture2D(W, H, TextureFormat.RGBA32, false);
			var px = new Color32[W * H];
			Color32 bg = new Color32(56, 62, 70, 255);
			for (int i = 0; i < px.Length; i++) px[i] = bg;
			var txt = new System.Text.StringBuilder();
			int missing = 0;
			for (int i = 0; i < list.Count; i++)
			{
				ExtraUpgrades.Upgrade u = list[i];
				int line = i / perLine, pair = i % perLine;
				int x0 = pair * pairW, y0 = H - (line + 1) * (cell + 8) + 4;
				Color32[] ia = UsIcon(u.Base.settings_Inventory.Sprite, cell), ib = UsIcon(u.Item.settings_Inventory.Sprite, cell);
				if (ia == null || ib == null) missing++;
				UsPaste(px, W, ia, cell, x0, y0);
				UsPaste(px, W, ib, cell, x0 + cell, y0);
				txt.AppendLine("line " + (line + 1) + ", pair " + (pair + 1) + ": " + u.BaseName + " (" + u.Base.settings_Inventory.DisplayName + ")  ->  " + u.Display + " [" + u.Name + "]" + (ia == null || ib == null ? "  (an icon could not be read)" : ""));
				yield return null;
			}
			try
			{
				sheet.SetPixels32(px);
				sheet.Apply();
				File.WriteAllBytes(UsPath("up_icons"), sheet.EncodeToPNG());
				File.WriteAllText(Path.GetFullPath(Path.Combine(DynamicIslands.assetpath, "shot_up_icons.txt")), txt.ToString());
			}
			catch (Exception e) { Fail("upgrade icons: writing the sheet: " + e.Message); yield break; }
			finally { UnityEngine.Object.Destroy(sheet); }
			Log("SHOT up icons shot_up_icons.png (" + list.Count + " pairs, " + missing + " with an unreadable icon)");
			if (missing == 0) Log("PASS: upgrade icons (" + list.Count + " pairs)"); else Fail("upgrade icons: " + missing + " pairs had an icon that could not be read");
		}

		/// <summary>The pixels of a sprite scaled to cell x cell through a RenderTexture (the texture may not be readable), or null.</summary>
		static Color32[] UsIcon(Sprite s, int cell)
		{
			if (s == null) return null;
			RenderTexture rt = null;
			RenderTexture previous = RenderTexture.active;
			Texture2D tex = null;
			try
			{
				Texture2D src = s.texture;
				Rect r = s.textureRect;
				rt = RenderTexture.GetTemporary(cell, cell, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
				Graphics.Blit(src, rt, new Vector2(r.width / src.width, r.height / src.height), new Vector2(r.x / src.width, r.y / src.height));
				RenderTexture.active = rt;
				tex = new Texture2D(cell, cell, TextureFormat.RGBA32, false);
				tex.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
				return tex.GetPixels32();
			}
			catch (Exception e) { Log("  (icon " + s.name + ": " + e.Message + ")"); return null; }
			finally
			{
				RenderTexture.active = previous;
				if (rt != null) RenderTexture.ReleaseTemporary(rt);
				if (tex != null) UnityEngine.Object.Destroy(tex);
			}
		}

		static void UsPaste(Color32[] sheet, int sheetW, Color32[] icon, int cell, int x0, int y0)
		{
			if (icon == null) return;
			for (int y = 0; y < cell; y++)
				for (int x = 0; x < cell; x++)
				{
					Color32 c = icon[y * cell + x];
					int k = (y0 + y) * sheetW + x0 + x;
					Color32 b = sheet[k];
					float a = c.a / 255f;
					sheet[k] = new Color32((byte)(b.r + (c.r - b.r) * a), (byte)(b.g + (c.g - b.g) * a), (byte)(b.b + (c.b - b.b) * a), 255);
				}
		}

		#endregion

		#region CIUpgradeMenuShot

		[ConsoleCommand(name: "CIUpgradeMenuShot", docs: "Dev, world (host): the crafting menu opened on each crafting category the upgrades are in, with the first upgrade of it selected (its cost shows): shot_up_menu_<category>.png; the upgrade research table placed and its own menu opened: shot_up_tablemenu.png. The option is on and the upgrades learned for the pictures, put back after. CIUpgradeMenuShot [all] (all: one picture per upgrade, shot_up_menu_<category>_<Name>.png)")]
		public static void UpgradeMenuShotCommand(string[] args)
		{
			string filter; float yaw; bool all;
			UsArgs(args, out filter, out yaw, out all);
			StartTest(UpgradeMenuShotRoutine(all));
		}

		static void UsScreenshot(string shotName)
		{
			string path = UsPath(shotName);
			try { if (File.Exists(path)) File.Delete(path); } catch { }
			Screenshot(new[] { shotName });
		}

		static IEnumerator UsMenuPicture(UsRun run, string label, string shotName)
		{
			UsScreenshot(shotName);
			var res = new UsRes();
			yield return UsWaitFile(UsPath(shotName), res);
			if (res.Ok) { run.Shots++; Log("SHOT up " + label + " shot_" + shotName + ".png"); }
			else { run.Fails++; Fail("upgrade menu shots: " + label + ": no picture"); }
		}

		static IEnumerator UpgradeMenuShotRoutine(bool each)
		{
			Network_Player player = RAPI.GetLocalPlayer();
			var inv = ComponentManager<Inventory_ResearchTable>.Value;
			CanvasHelper ch = ComponentManager<CanvasHelper>.Value ?? UnityEngine.Object.FindObjectOfType<CanvasHelper>();
			CraftingMenu cm = Resources.FindObjectsOfTypeAll<CraftingMenu>().FirstOrDefault(m => m != null && m.gameObject.scene.IsValid());
			if (player == null || ch == null || cm == null || !CustomIslandSpawner.RaftPosition.HasValue || !Raft_Network.IsHost) { Fail("upgrade menu shots: host, in a world"); yield break; }
			var run = new UsRun { Player = player, Spot = CustomIslandSpawner.RaftPosition.Value + new Vector3(0f, 22f, 0f) };
			var optionsBefore = new HashSet<string>(WorldOptions.Current);
			string tableBefore = UpgradeTable.State();
			var baseLearned = new Dictionary<Item_Base, bool>();
			List<ExtraUpgrades.Upgrade> ups = ExtraUpgrades.Registered.Where(u => !u.Table && u.Base != null && u.Item.settings_recipe != null).ToList();
			var groups = ups.GroupBy(u => u.Item.settings_recipe.CraftingCategory).ToList();
			try
			{
				var set = new HashSet<string>(optionsBefore); set.Add(WorldOptions.Upgrades); WorldOptions.Set(set);
				foreach (ExtraUpgrades.Upgrade u in ups)
				{
					if (!baseLearned.ContainsKey(u.Base)) baseLearned[u.Base] = u.Base.settings_recipe.Learned;
					u.Base.settings_recipe.Learned = true;
					UpgradeTable.SetLearned(u, true);
				}
				UsSetHour(12f);
				yield return null; yield return null;
				var select = AccessTools.Method(typeof(CraftingMenu), "SelectCraftingCategory");
				var pick = AccessTools.Method(typeof(CraftingMenu), "SelectRecipe");
				foreach (var g in groups)
				{
					string cat = g.Key.ToString();
					foreach (ExtraUpgrades.Upgrade u in each ? g.ToList() : g.Take(1).ToList())
					{
						string shotName = "up_menu_" + cat + (each ? "_" + UsName(u) : "");
						bool opened = true;
						try
						{
							ch.OpenMenuCloseOther(MenuType.Inventory, true);
						}
						catch (Exception e) { opened = false; run.Fails++; Fail("upgrade menu shots: " + cat + ": opening the inventory: " + e.Message); }
						if (!opened) continue;
						yield return new WaitForSeconds(0.5f);
						try
						{
							object[] a = select.GetParameters().Length == 1 ? new object[] { g.Key } : new object[] { g.Key, false };
							select.Invoke(cm, a);
						}
						catch (Exception e) { opened = false; run.Fails++; Fail("upgrade menu shots: " + cat + ": selecting the category: " + (e.InnerException ?? e).Message); }
						if (!opened) { GupCloseMenu(); continue; }
						yield return new WaitForSeconds(0.4f);
						try { pick.Invoke(cm, new object[] { u.Item, false }); }
						catch (Exception e) { Log("  (selecting " + u.Display + " in " + cat + ": " + (e.InnerException ?? e).Message + ": the picture shows the list only)"); }
						yield return new WaitForSeconds(0.5f);
						yield return UsMenuPicture(run, cat + (each ? " " + UsName(u) : ""), shotName);
						GupCloseMenu();
						yield return new WaitForSeconds(0.3f);
					}
				}
				// the upgrade research table's own menu
				Item_Base table = ExtraUpgrades.TableItem;
				if (table == null || inv == null) { run.Fails++; Fail("upgrade menu shots: no upgrade table or research menu"); }
				else
				{
					Block tb = UsPlace(run, table);
					yield return new WaitForSeconds(0.5f);
					ResearchTable ut = UtTableOf(tb);
					bool tableOpen = false;
					// (Raft's own opening: the table's panel in the inventory menu; MenuType has no research table)
					try { tableOpen = ut != null && ut.Open(true, run.Player.steamID); }
					catch (Exception e) { Log("  (opening the upgrade table: " + (e.InnerException ?? e).Message + ")"); }
					if (!tableOpen) { run.Fails++; Fail("upgrade menu shots: the upgrade table could not be opened"); }
					else
					{
						yield return new WaitForSeconds(0.8f);
						yield return UsMenuPicture(run, "tablemenu", "up_tablemenu");
						UtEsc();
						yield return new WaitForSeconds(0.3f);
					}
				}
			}
			finally
			{
				UsRemove(run);
				GupCloseMenu();
				WorldOptions.Set(optionsBefore);
				foreach (var kv in baseLearned) kv.Key.settings_recipe.Learned = kv.Value;
				UpgradeTable.SetState(tableBefore);
			}
			if (run.Fails == 0 && run.Shots > 0) Log("PASS: upgrade menu shots (" + run.Shots + " pictures)");
			else Fail("upgrade menu shots: " + run.Fails + " failed, " + run.Shots + " pictures");
		}

		#endregion
	}
}
