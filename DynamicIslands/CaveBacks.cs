using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Raft draws its cave pieces' rock shells (Balboa's dens, the tunnels) from inside only: from outside - in the editor, or
	/// over a gap in the ground - and from inside wherever one fold of a shell is seen from behind, you looked through them (X7).
	/// Every cave piece gets its shell a second time, each face turned the other way: the same rock, seen from outside. Raft's
	/// meshes can't be read by the mod, so each is read back from the graphics card once (Unity 2021's mesh buffers).
	/// </summary>
	public static class CaveBacks
	{
		/// <summary>Name of the back's object (under each of the piece's meshes).</summary>
		public const string Name = "CaveBack";

		static readonly Dictionary<Mesh, Mesh> made = new Dictionary<Mesh, Mesh>();
		static bool warned;

		/// <summary>Selene's rooms, tunnels and doors (Raft's own walls, ceilings, floors and door frames inside the station):
		/// drawn from inside only too, so a room's outer wall - or its ceiling seen from above - showed the room through it (X7).</summary>
		public const string SelenePrefix = "TP_Selene_Int_";

		/// <summary>A piece that gets its backs: a cave piece's shell, or one of Selene's inside pieces.</summary>
		public static bool Wants(string name)
		{
			if (string.IsNullOrEmpty(name)) return false;
			PropInfo p = RaftProps.Get(name);
			return (p != null && p.IsCave) || name.StartsWith(SelenePrefix, System.StringComparison.Ordinal);
		}

		/// <summary>The collider belongs to a piece drawn from both sides (its backs added): a look meeting its back sees it.</summary>
		public static bool DrawnBothSides(Collider c)
		{
			for (Transform t = c != null ? c.transform : null; t != null; t = t.parent)
				if (Wants(t.name.Replace("(Clone)", "").Trim())) return Has(t);
			return false;
		}

		/// <summary>A cave piece or a Selene piece just placed or spawned (editor or world): its faces get their other side.</summary>
		public static void Add(GameObject piece, string name)
		{
			if (piece == null || !Wants(name)) return;
			foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>(true))
			{
				if (mf.name == Name || mf.sharedMesh == null || mf.transform.Find(Name) != null) continue;
				MeshRenderer r = mf.GetComponent<MeshRenderer>();
				if (r == null) continue;
				Mesh back = Back(mf.sharedMesh);
				if (back == null) continue;
				var go = new GameObject(Name);
				go.layer = mf.gameObject.layer;
				go.transform.SetParent(mf.transform, false);
				go.AddComponent<MeshFilter>().sharedMesh = back;
				MeshRenderer br = go.AddComponent<MeshRenderer>();
				br.sharedMaterials = r.sharedMaterials;
				br.shadowCastingMode = r.shadowCastingMode; // (so the roof shades the cave from the sun too: the inside's roof faces away from it)
				br.receiveShadows = r.receiveShadows;
				br.enabled = r.enabled;
			}
		}

		/// <summary>The cave piece's meshes have their backs (tests).</summary>
		public static bool Has(Transform piece)
		{
			foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>(true))
				if (mf.name != Name && mf.sharedMesh != null && mf.GetComponent<MeshRenderer>() != null && mf.transform.Find(Name) == null) return false;
			return true;
		}

		/// <summary>The mesh turned inside out: same points, faces wound the other way, normals reversed. Null: couldn't be read.</summary>
		static Mesh Back(Mesh m)
		{
			Mesh back;
			if (made.TryGetValue(m, out back)) return back;
			back = null;
			try
			{
				VertexAttributeDescriptor[] attrs = m.GetVertexAttributes();
				int count = m.vertexCount;
				var streams = new byte[m.vertexBufferCount][];
				for (int s = 0; s < streams.Length; s++)
					using (GraphicsBuffer vb = m.GetVertexBuffer(s))
					{
						streams[s] = new byte[vb.count * vb.stride];
						vb.GetData(streams[s]);
					}
				// (the normals reversed; and the tangents' handedness, so a bumpy surface stays bumped the same way)
				Flip(m, streams, VertexAttribute.Normal, 3);
				Flip(m, streams, VertexAttribute.Tangent, 1);
				byte[] index;
				using (GraphicsBuffer ib = m.GetIndexBuffer())
				{
					index = new byte[ib.count * ib.stride];
					ib.GetData(index);
				}
				int size = m.indexFormat == IndexFormat.UInt16 ? 2 : 4;
				for (int sm = 0; sm < m.subMeshCount; sm++)
				{
					SubMeshDescriptor d = m.GetSubMesh(sm);
					if (d.topology != MeshTopology.Triangles) continue;
					for (int i = d.indexStart; i + 2 < d.indexStart + d.indexCount; i += 3)
						for (int b = 0; b < size; b++)
						{
							byte t = index[(i + 1) * size + b];
							index[(i + 1) * size + b] = index[(i + 2) * size + b];
							index[(i + 2) * size + b] = t;
						}
				}
				back = new Mesh { name = m.name + " (back)" };
				back.SetVertexBufferParams(count, attrs);
				for (int s = 0; s < streams.Length; s++) back.SetVertexBufferData(streams[s], 0, 0, streams[s].Length, s, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
				back.SetIndexBufferParams(index.Length / size, m.indexFormat);
				back.SetIndexBufferData(index, 0, 0, index.Length, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
				back.subMeshCount = m.subMeshCount;
				for (int sm = 0; sm < m.subMeshCount; sm++) back.SetSubMesh(sm, m.GetSubMesh(sm), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
				back.bounds = m.bounds;
				back.UploadMeshData(true);
			}
			catch (System.Exception e)
			{
				back = null;
				if (!warned) Debug.LogWarning("[CUSTOM ISLANDS] A cave piece's outside couldn't be made (" + m.name + "): " + e.Message);
				warned = true;
			}
			made[m] = back;
			return back;
		}

		/// <summary>Negates the first n components of an attribute in the raw vertex data, whatever number format it is in.</summary>
		static void Flip(Mesh m, byte[][] streams, VertexAttribute a, int n)
		{
			if (!m.HasVertexAttribute(a)) return;
			int s = m.GetVertexAttributeStream(a), offset = m.GetVertexAttributeOffset(a), stride = m.GetVertexBufferStride(s), dim = m.GetVertexAttributeDimension(a);
			VertexAttributeFormat f = m.GetVertexAttributeFormat(a);
			byte[] data = streams[s];
			int first = a == VertexAttribute.Tangent ? 3 : 0, last = a == VertexAttribute.Tangent ? Mathf.Min(dim, 4) : Mathf.Min(dim, n); // (a tangent: just its w)
			if (a == VertexAttribute.Tangent && dim < 4) return;
			for (int v = 0; v * stride + offset < data.Length; v++)
				for (int c = first; c < last; c++)
				{
					switch (f)
					{
						case VertexAttributeFormat.Float32:
						{
							int at = v * stride + offset + c * 4;
							data[at + 3] ^= 0x80; // (the sign bit: little-endian, the last byte)
							break;
						}
						case VertexAttributeFormat.Float16:
						{
							int at = v * stride + offset + c * 2;
							data[at + 1] ^= 0x80;
							break;
						}
						case VertexAttributeFormat.SNorm8:
						{
							int at = v * stride + offset + c;
							data[at] = (byte)(sbyte)Mathf.Max(-127, -(sbyte)data[at]);
							break;
						}
						case VertexAttributeFormat.SNorm16:
						{
							int at = v * stride + offset + c * 2;
							short x = (short)(data[at] | (data[at + 1] << 8));
							x = (short)Mathf.Max(-32767, -x);
							data[at] = (byte)(x & 0xff);
							data[at + 1] = (byte)((x >> 8) & 0xff);
							break;
						}
					}
				}
		}
	}
}
