using System;
using System.Collections.Generic;
using CommandUndoRedo;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The terrain brushes' shared parts (ROADMAP E6): the brush edge (falloff) every sculpt and paint brush uses, the noise
	/// and erosion brushes' work on a block of heights, switching the island between a shallow and a deep sea floor without
	/// generating it again, and a second ground style mixed into the first (texture layers 5-8).
	/// </summary>
	public static class TerrainBrushes
	{
		#region Falloff

		/// <summary>How a brush fades towards its rim: Smooth (soft, the old brush), Linear (evenly) or Hard (full strength to the rim).</summary>
		public enum Falloff { Smooth, Linear, Hard }

		public static readonly string[] FalloffNames = { "Smooth", "Linear", "Hard" };

		/// <summary>The brush edge every sculpt and paint brush uses.</summary>
		public static Falloff BrushFalloff = Falloff.Smooth;

		/// <summary>Brush weight (1 in the middle, 0 at and beyond the rim) at d2 = (distance / radius)².</summary>
		public static float Weight(float d2) { return Weight(d2, BrushFalloff); }

		public static float Weight(float d2, Falloff falloff)
		{
			if (d2 >= 1f) return 0f;
			switch (falloff)
			{
				case Falloff.Linear: return 1f - Mathf.Sqrt(d2);
				case Falloff.Hard: return Mathf.Clamp01((1f - Mathf.Sqrt(d2)) / 0.08f); // (a thin soft rim, so the edge isn't jagged)
				default: { float f = 1f - d2; return f * f; }
			}
		}

		#endregion

		#region Noise and erosion

		/// <summary>Noise brush: the size (m) of its bumps.</summary>
		public static float NoiseScale = 12f;
		public const float MinNoiseScale = 2f, MaxNoiseScale = 60f;

		/// <summary>Erosion brush: the steepest slope (degrees) that stays; steeper ground slides down until it is this steep.</summary>
		public static float TalusAngle = 32f;
		public const float MinTalus = 10f, MaxTalus = 60f;

		/// <summary>
		/// The noise field (about -1..1) at a point of the terrain (metres from its corner). Always the same field, so a stroke
		/// with the noise taken away (Shift) smooths out what an earlier stroke of the same size added.
		/// </summary>
		public static float Noise(float x, float z, float scale)
		{
			float s = Mathf.Max(0.5f, scale);
			float a = Mathf.PerlinNoise(x / s + 173.31f, z / s + 41.97f) - 0.5f;
			float b = Mathf.PerlinNoise(x * 2f / s + 911.13f, z * 2f / s + 377.71f) - 0.5f;
			return (a * 0.67f + b * 0.33f) * 2.6f;
		}

		/// <summary>
		/// Adds the noise field to a block of normalised heights (x0/z0: its first sample, spacing: metres between samples),
		/// metres high at full weight; negative metres takes it away.
		/// </summary>
		public static void AddNoise(float[,] h, float[,] w, int x0, int z0, float spacing, float sizeY, float scale, float metres)
		{
			float d = metres / sizeY;
			for (int z = 0; z < h.GetLength(0); z++)
				for (int x = 0; x < h.GetLength(1); x++)
				{
					if (w[z, x] <= 0f) continue;
					h[z, x] = Mathf.Clamp01(h[z, x] + d * w[z, x] * Noise((x0 + x) * spacing, (z0 + z) * spacing, scale));
				}
		}

		/// <summary>
		/// Thermal erosion on a block of normalised heights: where the ground is steeper than the talus angle (talusRise: the
		/// rise allowed from one sample to the next, normalised), part of the extra slides to the lower neighbours. Nothing
		/// is lost or added: the material only moves. rate: share of the extra moved per iteration (at full weight).
		/// </summary>
		public static void Erode(float[,] h, float[,] w, float talusRise, float rate, int iterations)
		{
			int rows = h.GetLength(0), cols = h.GetLength(1);
			var delta = new float[rows, cols];
			var ex = new float[4];
			int[] dz = { -1, 1, 0, 0 }, dx = { 0, 0, -1, 1 };
			for (int it = 0; it < iterations; it++)
			{
				Array.Clear(delta, 0, delta.Length);
				for (int z = 0; z < rows; z++)
					for (int x = 0; x < cols; x++)
					{
						if (w[z, x] <= 0f) continue;
						float sum = 0f, max = 0f;
						for (int n = 0; n < 4; n++)
						{
							int nz = z + dz[n], nx = x + dx[n];
							ex[n] = 0f;
							if (nz < 0 || nz >= rows || nx < 0 || nx >= cols) continue;
							float e = h[z, x] - h[nz, nx] - talusRise;
							if (e <= 0f) continue;
							ex[n] = e; sum += e; max = Mathf.Max(max, e);
						}
						if (sum <= 0f) continue;
						float move = Mathf.Clamp01(rate * w[z, x]) * max * 0.5f;
						delta[z, x] -= move;
						for (int n = 0; n < 4; n++)
							if (ex[n] > 0f) delta[z + dz[n], x + dx[n]] += move * ex[n] / sum;
					}
				for (int z = 0; z < rows; z++)
					for (int x = 0; x < cols; x++)
						h[z, x] = Mathf.Clamp01(h[z, x] + delta[z, x]);
			}
		}

		/// <summary>The rise (normalised) from one height sample to the next at the talus angle.</summary>
		public static float TalusRise(float spacing, float sizeY) { return Mathf.Tan(Mathf.Clamp(TalusAngle, 1f, 89f) * Mathf.Deg2Rad) * spacing / sizeY; }

		#endregion

		#region Sea floor

		/// <summary>Ground within this many metres of the terrain's base is untouched sea floor.</summary>
		public const float FloorEpsilon = 0.05f;
		/// <summary>Deeper floor: metres over which the floor falls from the old depth next to the island's slope to the new one.</summary>
		public const float FloorRamp = 100f;

		/// <summary>True when the island being edited stands on Raft's deep sea floor (its sea 160 m above the terrain's base).</summary>
		public static bool EditorDeep { get { return DynamicIslands.EditorWaterLevel > (IslandFile.DefaultWaterLevel + IslandFile.DeepWaterLevel) / 2f; } }

		/// <summary>
		/// The heights after moving the sea from its old height to deltaMetres higher above the terrain's base (normalised
		/// heights, the terrain's size): everything above the sea floor keeps its height above (or depth under) the sea; the
		/// untouched floor - at the terrain's base - stays there, so it is that much deeper or shallower now. A deeper floor
		/// falls away over FloorRamp metres from the island's slope instead of a sheer wall; on a shallower one what was deeper
		/// than the new floor is floor. Null when the land would reach above the build area.
		/// </summary>
		public static float[,] SeaFloorHeights(float[,] h, Vector3 size, float deltaMetres)
		{
			int rows = h.GetLength(0), cols = h.GetLength(1);
			float spacing = size.x / Mathf.Max(1, cols - 1), d = deltaMetres / size.y, eps = FloorEpsilon / size.y;
			bool deeper = deltaMetres > 0f;
			float[,] dist = deeper ? FloorDistance(h, eps, spacing) : null;
			var result = new float[rows, cols];
			for (int z = 0; z < rows; z++)
				for (int x = 0; x < cols; x++)
				{
					float v = h[z, x];
					if (v <= eps)
						result[z, x] = deeper ? Mathf.Clamp01(v + d * (1f - Mathf.SmoothStep(0f, 1f, dist[z, x] / FloorRamp))) : v;
					else
					{
						float n = v + d;
						if (n > 1f) return null;
						result[z, x] = Mathf.Max(0f, n);
					}
				}
			return result;
		}

		/// <summary>Metres from each floor sample to the nearest ground above the floor (two-pass chamfer distance).</summary>
		static float[,] FloorDistance(float[,] h, float eps, float spacing)
		{
			int rows = h.GetLength(0), cols = h.GetLength(1);
			var dist = new float[rows, cols];
			float diag = spacing * 1.41421356f;
			for (int z = 0; z < rows; z++)
				for (int x = 0; x < cols; x++)
					dist[z, x] = h[z, x] > eps ? 0f : float.MaxValue / 4f;
			for (int z = 0; z < rows; z++)
				for (int x = 0; x < cols; x++)
				{
					float v = dist[z, x];
					if (x > 0) v = Mathf.Min(v, dist[z, x - 1] + spacing);
					if (z > 0)
					{
						v = Mathf.Min(v, dist[z - 1, x] + spacing);
						if (x > 0) v = Mathf.Min(v, dist[z - 1, x - 1] + diag);
						if (x < cols - 1) v = Mathf.Min(v, dist[z - 1, x + 1] + diag);
					}
					dist[z, x] = v;
				}
			for (int z = rows - 1; z >= 0; z--)
				for (int x = cols - 1; x >= 0; x--)
				{
					float v = dist[z, x];
					if (x < cols - 1) v = Mathf.Min(v, dist[z, x + 1] + spacing);
					if (z < rows - 1)
					{
						v = Mathf.Min(v, dist[z + 1, x] + spacing);
						if (x < cols - 1) v = Mathf.Min(v, dist[z + 1, x + 1] + diag);
						if (x > 0) v = Mathf.Min(v, dist[z + 1, x - 1] + diag);
					}
					dist[z, x] = v;
				}
			return dist;
		}

		/// <summary>
		/// Editor (Island tab): puts the island being edited on a deep sea floor like Raft's (sea 160 m above the terrain's
		/// base) or a shallow one (20 m) without generating it again - the land, the shallows and what stands on them keep
		/// their height above the sea, only the untouched floor gets deeper or shallower. One undo step. Returns what
		/// happened, for the status bar (null when it was done).
		/// </summary>
		public static string SwitchSeaFloor(bool deep)
		{
			Terrain terrain = terraineditor.terrain;
			if (terrain == null) return "Open the editor first";
			float from = DynamicIslands.EditorWaterLevel, to = deep ? IslandFile.DeepWaterLevel : IslandFile.DefaultWaterLevel;
			if (Mathf.Abs(to - from) < 0.01f) return "The island is already on a " + (deep ? "deep" : "shallow") + " sea floor";
			TerrainData data = terrain.terrainData;
			if (to >= data.size.y - 10f) return "The terrain is too low for a deep sea floor";
			int hres = data.heightmapResolution, ares = data.alphamapResolution;
			float[,] before = data.GetHeights(0, 0, hres, hres);
			float[,] after = SeaFloorHeights(before, data.size, to - from);
			if (after == null) return "The land is too high to go on a deep sea floor (it would reach above the editor's " + data.size.y.ToString("F0") + " m)";

			float[,,] alphaBefore = data.GetAlphamaps(0, 0, ares, ares);
			float[,] maskBefore = terraineditor.paintMask != null ? (float[,])terraineditor.paintMask.Clone() : null;
			data.SetHeights(0, 0, after);
			DynamicIslands.SetEditorWaterLevel(to);
			// (the automatic texturing follows the new depths; hand-painted ground keeps its paint)
			TerrainPainter.Setup(terrain, terrain.transform.position.y + to, terraineditor.paintMask);

			var group = new CommandGroup();
			group.Add(new WaterLevelCommand(from, to));
			group.Add(new TerrainStrokeCommand(data, new RectInt(0, 0, hres, hres), before, new RectInt(0, 0, ares, ares), alphaBefore, maskBefore, terraineditor.paintMask));
			foreach (ICommand c in FollowSea(terrain, before, to - from)) group.Add(c);
			UndoRedoManager.Insert(group);
			if (Camera.main != null) Camera.main.transform.position += Vector3.up * (to - from);
			return null;
		}

		/// <summary>
		/// The placed objects after a sea floor switch: what stood on the ground goes up or down with it, the rest (on the
		/// water, in the air) keeps its height above the sea.
		/// </summary>
		static List<ICommand> FollowSea(Terrain terrain, float[,] before, float deltaMetres)
		{
			var moved = new List<ICommand>();
			GameObject root = GameObject.Find("PlacedObjects");
			RuntimeGizmos.TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			if (root == null || gizmo == null) return moved;
			TerrainData data = terrain.terrainData;
			int res = data.heightmapResolution;
			Vector3 o = terrain.transform.position, size = data.size;
			foreach (EditorGameObject e in root.GetComponentsInChildren<EditorGameObject>(true))
			{
				// (a part of a placed object that is one itself moves with it)
				if (e.transform.parent != null && e.transform.parent.GetComponentInParent<EditorGameObject>() != null) continue;
				Vector3 p = e.transform.position;
				float u = Mathf.Clamp((p.x - o.x) / size.x * (res - 1), 0f, res - 1.001f), v = Mathf.Clamp((p.z - o.z) / size.z * (res - 1), 0f, res - 1.001f);
				int x0 = (int)u, z0 = (int)v;
				float fx = u - x0, fz = v - z0;
				float was = Mathf.Lerp(Mathf.Lerp(before[z0, x0], before[z0, x0 + 1], fx), Mathf.Lerp(before[z0 + 1, x0], before[z0 + 1, x0 + 1], fx), fz) * size.y + o.y;
				float now = terrain.SampleHeight(p) + o.y;
				float dy = p.y <= was + 0.6f && p.y >= was - 8f ? now - was : deltaMetres;
				if (Mathf.Abs(dy) < 0.001f) continue;
				var command = new RuntimeGizmos.TransformCommand(gizmo, e.transform);
				e.transform.position = p + Vector3.up * dy;
				command.StoreNewTransformValues();
				moved.Add(command);
			}
			return moved;
		}

		#endregion

		#region Mixed styles

		/// <summary>
		/// Editor (Paint ground): mixes a second style's four textures into the island as texture layers 5-8 (mix = -1: none
		/// again; ground painted with them goes back to the first style's matching texture). One undo step.
		/// </summary>
		public static void SetEditorMix(int mix)
		{
			Terrain terrain = terraineditor.terrain;
			int before = DynamicIslands.currentMixStyle;
			mix = mix < 0 ? -1 : Mathf.Clamp(mix, 0, TerrainPainter.Styles.Length - 1);
			if (terrain == null || mix == before) { DynamicIslands.SetEditorMixStyle(mix); return; }
			TerrainData data = terrain.terrainData;
			int ares = data.alphamapResolution;
			float[,,] alphaBefore = data.GetAlphamaps(0, 0, ares, ares);
			DynamicIslands.SetEditorMixStyle(mix);
			float[,,] alphaAfter = data.GetAlphamaps(0, 0, ares, ares);
			// (painting with a mix texture that is gone now)
			if (mix < 0 && terraineditor.paintLayer >= TerrainPainter.LayerCount) terraineditor.paintLayer -= TerrainPainter.LayerCount;
			UndoRedoManager.Insert(new MixStyleCommand(data, before, mix, alphaBefore, alphaAfter));
		}

		#endregion
	}

	/// <summary>Undo step for mixing a second style into the island (or taking it out): its layers and the whole texture paint.</summary>
	public class MixStyleCommand : ICommand
	{
		readonly TerrainData data;
		readonly int before, after;
		readonly float[,,] alphaBefore, alphaAfter;

		public MixStyleCommand(TerrainData data, int before, int after, float[,,] alphaBefore, float[,,] alphaAfter)
		{
			this.data = data; this.before = before; this.after = after; this.alphaBefore = alphaBefore; this.alphaAfter = alphaAfter;
		}

		public void Execute() { Apply(after, alphaAfter); }
		public void UnExecute() { Apply(before, alphaBefore); }

		void Apply(int mix, float[,,] alpha)
		{
			DynamicIslands.SetEditorMixStyle(mix);
			if (data != null && alpha != null) data.SetAlphamaps(0, 0, TerrainPainter.FitLayers(alpha, data.alphamapLayers));
			if (mix < 0 && terraineditor.paintLayer >= TerrainPainter.LayerCount) terraineditor.paintLayer -= TerrainPainter.LayerCount;
		}
	}
}
