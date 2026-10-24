using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;

namespace DynamicIslands
{
	/// <summary>
	/// A LOOK prototype only (no gameplay): a giant "kraken arm" rising out of the sea beside the raft and curling over
	/// one edge foundation, to decide whether a Kraken arm event is worth building. Raft has no tentacle model and the
	/// mod imports no meshes, so two ways are tried:
	/// 1 - a chain of one of Raft's own pieces (default seavine_tongue), stretched along the curve and tinted dark purple;
	/// 2 - a chain of tapered Unity capsules with a copy of Raft's shark material (tinted), and pale suckers underneath.
	/// Both sway. Pictures: Mods\DynamicIslands\recipes\play_kraken&lt;way&gt;_&lt;piece&gt;_a/b.jpg.
	/// </summary>
	public static partial class DevTests
	{
		const string KrakenRootName = "CIKrakenLook";

		[ConsoleCommand(name: "CIKrakenLook", docs: "Dev, in game: LOOK prototype of a kraken arm curling over a raft edge foundation. 'CIKrakenLook 1 [piece]' = a chain of a Raft piece (default seavine_tongue) tinted purple; 'CIKrakenLook 2' = tapered capsules with Raft's shark skin; 'CIKrakenLook arm [seed]' = the real arm the option makes, with its extra arms (pictures play_krakenarm_*.jpg); 'CIKrakenLook clear' removes it. Takes two pictures (play_kraken*.jpg). No gameplay")]
		public static void KrakenLookCommand(string[] args)
		{
			string way = args != null && args.Length > 0 ? args[0] : "1";
			string piece = args != null && args.Length > 1 ? args[1] : "seavine_tongue";
			StartTest(KrakenLookRoutine(way, piece));
		}

		static IEnumerator KrakenLookRoutine(string way, string piece)
		{
			foreach (GameObject old in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g != null && g.name == KrakenRootName && g.scene.IsValid()).ToList())
				UnityEngine.Object.Destroy(old);
			foreach (Kraken.Arm old in UnityEngine.Object.FindObjectsOfType<Kraken.Arm>().Where(x => x != Kraken.Current).ToList())
				UnityEngine.Object.Destroy(old.gameObject);
			if (way == "clear") { Log("PASS: kraken look removed"); yield break; }
			if (way == "arm") { yield return KrakenLookArm(piece == "seavine_tongue" ? 1 : int.Parse(piece)); yield break; }
			Raft raft = ComponentManager<Raft>.Value;
			List<Block> blocks = BlockCreator.GetPlacedBlocks();
			if (raft == null || blocks == null || blocks.Count == 0) { Fail("kraken look: needs a world with a raft"); yield break; }
			yield return PlaceableCatalog.EnsureBuilt();

			// The edge foundation: the raft block farthest from the raft's middle (foundations first), in raft space
			Transform rt = raft.transform;
			List<Block> mine = blocks.Where(b => b != null && b.transform.IsChildOf(rt)).ToList();
			List<Block> found = mine.Where(b => b.name.IndexOf("Foundation", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			if (found.Count == 0) found = mine;
			Vector3 mid = Vector3.zero;
			foreach (Block b in found) mid += rt.InverseTransformPoint(b.transform.position);
			mid /= found.Count;
			Block edge = found.OrderByDescending(b => { Vector3 p = rt.InverseTransformPoint(b.transform.position) - mid; p.y = 0; return p.magnitude; }).First();
			Vector3 e = rt.InverseTransformPoint(edge.transform.position);
			Vector3 d = e - mid; d.y = 0;
			Vector3 u = Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x == 0 ? 1 : d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
			Vector3 s = Vector3.Cross(Vector3.up, u);
			Vector3 E = e + u * 0.75f;
			Log("edge foundation " + edge.name + " at raft-local " + e.ToString("F2") + ", outward " + u + ", " + found.Count + " foundations");

			var root = new GameObject(KrakenRootName);
			root.transform.SetParent(rt, false);
			root.transform.localPosition = Vector3.zero;
			root.transform.localRotation = Quaternion.identity;
			// (carried by the edge foundation itself, in raft space)
			root.transform.SetParent(edge.transform, true);
			var arm = root.AddComponent<KrakenArm>();
			Vector3 up = Vector3.up;
			// The deck's top over the edge foundation (raft space)
			float deck = 0.35f;
			Renderer[] er = edge.GetComponentsInChildren<Renderer>();
			if (er.Length > 0) deck = er.Max(r => rt.InverseTransformPoint(r.bounds.max).y) - e.y;
			Log("deck top " + deck.ToString("F2") + " m over the foundation's origin");
			// Up out of the sea close beside the edge, over the top, and the tip laid down on the deck with a small curl
			Vector3 D = E + up * deck;
			arm.Ctrl = new[]
			{
				E + u * 2.0f - up * 3.0f,
				E + u * 2.3f + up * 1.2f + s * 0.2f,
				E + u * 1.9f + up * 3.9f + s * 0.5f,
				E + u * 0.8f + up * 5.2f + s * 0.4f,
				E + u * -0.3f + up * 4.4f + s * 0.1f,
				D + u * -0.9f + up * 2.4f,
				D + u * -1.2f + up * 0.8f - s * 0.1f,
				D + u * -1.35f + up * 0.12f - s * 0.25f,
				D + u * -0.85f + up * 0.1f - s * 0.45f,
				D + u * -0.55f + up * 0.35f - s * 0.3f,
			};
			arm.Side = s; arm.Out = u;

			if (way == "2")
			{
				Material skin = KrakenSkin();
				if (skin == null) { Fail("kraken look: no shark (or other) material found"); UnityEngine.Object.Destroy(root); yield break; }
				arm.BuildCapsules(skin, new Color(0.62f, 0.38f, 0.62f), new Color(0.95f, 0.72f, 0.75f));
				piece = "capsules";
			}
			else
			{
				GameObject proto = PlaceableCatalog.Get(piece);
				if (proto == null) { Fail("kraken look: piece '" + piece + "' isn't loaded (core objects only)"); UnityEngine.Object.Destroy(root); yield break; }
				string colours = arm.BuildPieces(piece, new Color(0.26f, 0.08f, 0.30f));
				Log("piece " + piece + " tinted through: " + colours);
			}
			Log("arm: " + arm.Count + " segments; root under " + root.transform.parent.name + ", raft scale " + rt.lossyScale.ToString("F2"));
			Transform tipSeg = root.transform.GetChild(root.transform.childCount - 1);
			Log("world: edge block " + edge.transform.position.ToString("F1") + ", E " + rt.TransformPoint(E).ToString("F1") + ", arm base " + root.transform.GetChild(0).position.ToString("F1") + ", last part " + tipSeg.position.ToString("F1"));
			yield return new WaitForSeconds(1.5f);
			string tag = "kraken" + way + "_" + piece;
			Vector3 look = rt.TransformPoint(E + up * 2.8f - u * 0.3f);
			yield return PlayPicture(tag + "_a", rt.TransformPoint(E - u * 8f + s * 3.5f + up * 2.4f), look);
			yield return new WaitForSeconds(1.2f);
			yield return PlayPicture(tag + "_b", rt.TransformPoint(E + u * 9f + s * 7.5f + up * 2.6f), rt.TransformPoint(E + up * 2.2f));
			yield return new WaitForSeconds(1.0f);
			Log("world after 2 pictures: E " + rt.TransformPoint(E).ToString("F1") + ", arm base " + root.transform.GetChild(0).position.ToString("F1"));
			yield return PlayPicture(tag + "_c", rt.TransformPoint(E + s * 10f + up * 3.5f - u * 1f), rt.TransformPoint(E + up * 2.8f + u * 0.8f));
			Log("PASS: kraken look " + way + " (" + piece + ")");
		}

		/// <summary>The real arm (Kraken.Arm, as the world option makes it, no gameplay) on an edge floor of the raft, with its
		/// extra arms placed from the seed; pictures from the side near the waterline, from the deck and from above.</summary>
		static IEnumerator KrakenLookArm(int seed)
		{
			var grid = StormSeas.Grid(StormSeas.Foundations());
			var edge = grid.Where(kv => StormSeas.Sides.Any(x => !grid.ContainsKey(kv.Key + x))).OrderBy(kv => kv.Key.x).ThenBy(kv => kv.Key.y).Select(kv => (KeyValuePair<Vector2Int, Block>?)kv).FirstOrDefault();
			if (edge == null) { Fail("kraken look: needs a world with a raft"); yield break; }
			int side = Enumerable.Range(0, 4).First(i => !grid.ContainsKey(edge.Value.Key + StormSeas.Sides[i]));
			Kraken.Arm arm = Kraken.Arm.Make(edge.Value.Value, side, seed, 0, Kraken.HitsNeeded, 99999f, 0, 0f);
			if (arm == null) { Fail("kraken look: no arm made"); yield break; }
			Transform pivot = arm.transform.parent;
			Vector3 u = new Vector3(StormSeas.Sides[side].x, 0f, StormSeas.Sides[side].y), s = Vector3.Cross(Vector3.up, u);
			Vector3 E = edge.Value.Value.transform.localPosition + u * 0.75f;
			Log("arm on floor " + arm.BlockIndex + " from side " + side + ", " + arm.Extras + " more arms, base at " + pivot.TransformPoint(arm.Main.Ctrl[0]).y.ToString("F1") + " m");
			yield return new WaitForSeconds(Kraken.RiseSeconds + 1f);
			yield return PlayPicture("krakenarm_side", pivot.TransformPoint(E + s * 10f - u * 1.5f + Vector3.up * 0.9f), pivot.TransformPoint(E + Vector3.up * 2.3f + u * 2f));
			yield return PlayPicture("krakenarm_deck", pivot.TransformPoint(E - u * 8f + s * 3.5f + Vector3.up * 2.4f), pivot.TransformPoint(E + Vector3.up * 2.8f + u * 1.5f));
			yield return PlayPicture("krakenarm_high", pivot.TransformPoint(E - u * 6f + s * 6f + Vector3.up * 9f), pivot.TransformPoint(E + u * 4f));
			Log("PASS: kraken look arm (seed " + seed + ")");
		}

		/// <summary>Raft's shark material (a live shark's, else any loaded material named shark), as a copy.</summary>
		static Material KrakenSkin()
		{
			Material m = null;
			AI_NetworkBehavior_Shark shark = UnityEngine.Object.FindObjectOfType<AI_NetworkBehavior_Shark>();
			if (shark != null)
			{
				Log("shark renderers: " + string.Join(", ", shark.GetComponentsInChildren<Renderer>(true).Select(x => x.GetType().Name + " " + x.name + "=" + (x.sharedMaterial != null ? x.sharedMaterial.name : "-")).ToArray()));
				Renderer r = shark.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(x => x.bounds.size.sqrMagnitude).FirstOrDefault(x => x.sharedMaterial != null);
				if (r != null) m = r.sharedMaterial;
			}
			if (m == null) m = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x != null && x.name.IndexOf("shark", StringComparison.OrdinalIgnoreCase) >= 0 && x.name.IndexOf("cage", StringComparison.OrdinalIgnoreCase) < 0 && x.mainTexture != null);
			if (m == null) return null;
			Log("skin: material '" + m.name + "' shader '" + (m.shader != null ? m.shader.name : "-") + "' texture '" + (m.mainTexture != null ? m.mainTexture.name : "-") + "'" + (shark != null ? " (from a live shark)" : ""));
			return new Material(m);
		}

		static string KrakenTint(Material m, Color c, bool multiply)
		{
			var set = new List<string>();
			foreach (string p in new[] { "_Color", "_BaseColor", "_TintColor", "_MainColor", "_Albedo" })
				if (m.HasProperty(p))
				{
					Color was = m.GetColor(p);
					m.SetColor(p, multiply ? new Color(was.r * c.r, was.g * c.g, was.b * c.b, was.a) : new Color(c.r, c.g, c.b, was.a));
					set.Add(p);
				}
			return set.Count > 0 ? string.Join("+", set.ToArray()) : "none (" + (m.shader != null ? m.shader.name : "-") + ")";
		}

		/// <summary>A tinted copy of a piece's material; a shader without a colour (Raft's Vegetation, Shimmer) is swapped
		/// for Standard (alpha cut-out) with the piece's own texture.</summary>
		static Material KrakenRetint(Material src, Color tint, ref string colours)
		{
			Material own = new Material(src);
			string how = KrakenTint(own, tint, false);
			if (how.StartsWith("none"))
			{
				Shader std = Shader.Find("Standard");
				string texName = src.GetTexturePropertyNames().FirstOrDefault(n => src.GetTexture(n) != null && (n.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Albedo", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Diffuse", StringComparison.OrdinalIgnoreCase) >= 0))
					?? src.GetTexturePropertyNames().FirstOrDefault(n => src.GetTexture(n) != null);
				if (std != null)
				{
					own = new Material(std);
					if (texName != null) own.mainTexture = src.GetTexture(texName);
					own.color = tint;
					own.SetFloat("_Glossiness", 0.55f);
					own.SetFloat("_Mode", 1f);
					own.SetFloat("_Cutoff", 0.4f);
					own.EnableKeyword("_ALPHATEST_ON");
					own.renderQueue = 2450;
					how = "Standard with " + (texName ?? "no texture") + " (" + how + "; textures " + string.Join("/", src.GetTexturePropertyNames()) + ")";
				}
			}
			colours += (colours.Length > 0 ? ", " : "") + src.name + ":" + how;
			return own;
		}

		/// <summary>The arm: a curve through Ctrl (Catmull-Rom), segments laid along it every frame with a sway that grows
		/// toward the tip.</summary>
		public class KrakenArm : MonoBehaviour
		{
			public Vector3[] Ctrl;
			public Vector3 Side, Out;
			const int N = 30;
			readonly List<Transform> segs = new List<Transform>();
			readonly List<Transform> suckers = new List<Transform>();
			readonly List<int> suckerAt = new List<int>();
			bool capsules;
			float pieceLen = 1f, pieceWidth = 1f;
			public int Count { get { return segs.Count; } }

			static float Radius(float t) { return Mathf.Lerp(0.85f, 0.06f, Mathf.Pow(t, 0.85f)); }

			Vector3 At(float t)
			{
				int n = Ctrl.Length - 1;
				float f = Mathf.Clamp01(t) * n;
				int i = Mathf.Min((int)f, n - 1);
				float k = f - i;
				Vector3 p0 = Ctrl[Mathf.Max(i - 1, 0)], p1 = Ctrl[i], p2 = Ctrl[i + 1], p3 = Ctrl[Mathf.Min(i + 2, n)];
				return 0.5f * (2f * p1 + (-p0 + p2) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k * k + (-p0 + 3f * p1 - 3f * p2 + p3) * k * k * k);
			}

			Vector3[] Points()
			{
				var p = new Vector3[N + 1];
				float T = Time.time;
				for (int i = 0; i <= N; i++)
				{
					float t = i / (float)N;
					float w = t * t;
					p[i] = At(t) + Side * (Mathf.Sin(T * 1.1f + t * 5f) * 0.45f * w) + Vector3.up * (Mathf.Sin(T * 0.8f + t * 4f) * 0.25f * w) + Out * (Mathf.Sin(T * 0.6f + t * 3f) * 0.2f * w);
				}
				return p;
			}

			public void BuildCapsules(Material skin, Color tint, Color suckerTint)
			{
				capsules = true;
				Material body = new Material(skin); KrakenTint(body, tint, true);
				Material pale = new Material(skin); KrakenTint(pale, suckerTint, false);
				for (int i = 0; i < N; i++)
				{
					GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
					UnityEngine.Object.Destroy(g.GetComponent<Collider>());
					g.name = "seg" + i;
					g.transform.SetParent(transform, false);
					g.GetComponent<Renderer>().sharedMaterial = body;
					segs.Add(g.transform);
					if (i > N * 0.3f && i % 2 == 0)
					{
						GameObject c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
						UnityEngine.Object.Destroy(c.GetComponent<Collider>());
						c.name = "sucker" + i;
						c.transform.SetParent(transform, false);
						c.GetComponent<Renderer>().sharedMaterial = pale;
						suckers.Add(c.transform); suckerAt.Add(i);
					}
				}
				Update();
			}

			public string BuildPieces(string piece, Color tint)
			{
				string colours = "";
				var mats = new Dictionary<Material, Material>();
				for (int i = 0; i < N; i++)
				{
					GameObject go = PlaceableCatalog.Spawn(piece, null);
					go.transform.position = new Vector3(0, -900, 0);
					go.transform.rotation = Quaternion.identity;
					foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
					foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
					Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
					Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
					foreach (Renderer r in rs) b.Encapsulate(r.bounds);
					Vector3 c0 = b.center - go.transform.position, size = b.size;
					// The longest side becomes the segment's length (up)
					Vector3 axis = size.y >= size.x && size.y >= size.z ? Vector3.up : size.x >= size.z ? Vector3.right : Vector3.forward;
					float len = Vector3.Dot(size, axis);
					float wid = axis == Vector3.up ? Mathf.Max(size.x, size.z) : axis == Vector3.right ? Mathf.Max(size.y, size.z) : Mathf.Max(size.x, size.y);
					pieceLen = len; pieceWidth = wid;
					var holder = new GameObject("seg" + i).transform;
					holder.SetParent(transform, false);
					var fix = new GameObject("fix").transform;
					fix.SetParent(holder, false);
					Quaternion q = Quaternion.FromToRotation(axis, Vector3.up) * Quaternion.Euler(0, i * 47f, 0);
					fix.localRotation = q;
					fix.localPosition = -(q * (c0 - axis * len * 0.5f));
					go.transform.SetParent(fix, false);
					go.transform.localPosition = Vector3.zero;
					go.transform.localRotation = Quaternion.identity;
					foreach (Renderer r in rs)
					{
						Material[] ms = r.sharedMaterials;
						for (int k = 0; k < ms.Length; k++)
						{
							if (ms[k] == null) continue;
							Material own;
							if (!mats.TryGetValue(ms[k], out own)) { own = KrakenRetint(ms[k], tint, ref colours); mats[ms[k]] = own; }
							ms[k] = own;
						}
						r.sharedMaterials = ms;
					}
					segs.Add(holder);
				}
				Update();
				return colours + " | piece " + pieceLen.ToString("F2") + " long, " + pieceWidth.ToString("F2") + " wide";
			}

			void Update()
			{
				if (Ctrl == null || segs.Count == 0) return;
				Vector3[] p = Points();
				for (int i = 0; i < segs.Count; i++)
				{
					Vector3 a = p[i], b = p[i + 1], dir = b - a;
					float L = dir.magnitude, r = Radius((i + 0.5f) / N);
					Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / Mathf.Max(L, 0.0001f));
					Transform t = segs[i];
					if (capsules)
					{
						t.localPosition = (a + b) * 0.5f;
						t.localRotation = rot;
						t.localScale = new Vector3(2f * r, (L + r * 1.6f) * 0.5f, 2f * r);
					}
					else
					{
						t.localPosition = a;
						t.localRotation = rot;
						t.localScale = new Vector3(2f * r / pieceWidth, L * 1.35f / pieceLen, 2f * r / pieceWidth);
					}
				}
				for (int k = 0; k < suckers.Count; k++)
				{
					int i = suckerAt[k];
					Vector3 a = p[i], b = p[i + 1], prev = p[Mathf.Max(i - 1, 0)];
					// The inner side of the curl (toward the bend's centre)
					Vector3 n = (b - a) - (a - prev);
					Vector3 tangent = (b - a).normalized;
					n = n - tangent * Vector3.Dot(n, tangent);
					if (n.sqrMagnitude < 1e-6f) n = -Out;
					n.Normalize();
					float r = Radius((i + 0.5f) / N);
					Transform t = suckers[k];
					t.localPosition = (a + b) * 0.5f + n * r * 0.82f;
					t.localRotation = Quaternion.FromToRotation(Vector3.up, n);
					t.localScale = new Vector3(r * 0.95f, r * 0.3f, r * 0.95f);
				}
			}
		}
	}
}
