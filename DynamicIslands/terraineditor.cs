using DynamicIslands.Editor;
using System;
using System.Collections.Generic;
using CommandUndoRedo;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands
{
	/// <summary>
	/// Editor terrain: creates the 1000 x 600 x 1000 terrain and sculpts it with a round brush (its edge: smooth, linear or
	/// hard, TerrainBrushes.BrushFalloff) while the Terrain tab is selected. Textures are repainted automatically when a
	/// stroke ends.
	/// </summary>
	public class terraineditor : MonoBehaviour
	{
		public static Terrain terrain;
		public static TerrainData terrainData;

		public Vector3 terrainSize = new Vector3(1000, 600, 1000);
		public int heightmapResolution = 513;

		public enum TerrainModificationAction
		{
			Raise,
			Lower,
			Flatten,
			Sample,
			SampleAverage,
			Smooth,
			PaintLayer, // paint terraineditor.paintLayer by hand
			AutoPaint,  // brush back to automatic texturing
			Stamp,      // one click puts down TerrainStamps.Current, as big as the brush
			Noise,      // adds bumps of TerrainBrushes.NoiseScale (Shift: takes them away)
			Erode,      // thermal erosion: ground steeper than TerrainBrushes.TalusAngle slides down
		}

		/// <summary>Tests: the next strokes take the noise away, as Shift does.</summary>
		public static bool SubtractNoise;
		float noiseSign = 1f; // -1: this stroke takes the noise away (Shift held when it started)

		/// <summary>Where the brush last was over the terrain (Save stamp captures around it).</summary>
		public static Vector3? LastPoint;
		bool stamped; // a stamp goes down once per click

		/// <summary>Texture layer used by PaintLayer (TerrainPainter.Seabed/Sand/Grass/Rock).</summary>
		public static int paintLayer = TerrainPainter.Sand;

		/// <summary>Alphamap-sized mask, 1 where the texture was painted by hand (auto texturing leaves those pixels alone).</summary>
		public static float[,] paintMask;

		public static TerrainModificationAction modificationAction = TerrainModificationAction.Raise;

		/// <summary>Brush radius in metres.</summary>
		public static float brushRadius = 15f;
		/// <summary>How fast Raise/Lower change the ground at the brush centre, in metres per second.
		/// For Flatten/Smooth it scales how quickly the ground converges.</summary>
		public static float strength = 4f;

		public const float MinRadius = 2f, MaxRadius = 80f;
		public const float MinStrength = 0.5f, MaxStrength = 20f;

		public static bool allowEditing = true;

		float flattenTarget; // normalised height sampled when a Flatten stroke starts

		/// <summary>The Flatten brush's "Flatten to" field: metres above the sea it levels the ground to (a recipe's
		/// "brush flatten ... h="); null = the height where the stroke starts.</summary>
		public static float? FlattenHeight;
		bool useFlattenHeight = true; // false while a recipe's stroke runs (SimulateStroke/SimulateDrag: their own h= or none)
		bool stroking;
		Vector3 dirtyMin, dirtyMax; // world-space area touched by the current stroke, repainted on release
		float dt; // time step of the current brush application
		float[,] strokeHeights; float[,,] strokeAlpha; float[,] strokeMask; // undo snapshot taken when the stroke starts

		void Start()
		{
			terrainData = new TerrainData();
			// Resolution first: changing it afterwards rescales the size
			terrainData.heightmapResolution = heightmapResolution;
			terrainData.size = terrainSize;

			terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
			terrain.transform.position = Vector3.zero;
			terrain.gameObject.layer = IslandSpawner.TerrainLayer;
			// CreateTerrainGameObject already adds a TerrainCollider bound to the same data

			paintMask = null;
			TerrainPainter.Setup(terrain, DynamicIslands.EditorWaterLevel);
			paintMask = new float[terrainData.alphamapResolution, terrainData.alphamapResolution];
		}

		void Update()
		{
			try
			{
				EditorInput.HandleShortcuts();
				EditorUI.Tick();
				ModifyTerrain();
				CaveGround.EditorTick(terrain, stroking); // (the ground out of the caves' rooms, as in a world - X7)
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Terrain editor: " + e.Message);
			}
		}

		bool CanSculpt()
		{
			if (!allowEditing || terrain == null) return false;
			if (IslandFilesWindow.IsOpen || GeneratorWindow.IsOpen || TextPromptWindow.IsOpen || NoteEditorWindow.IsOpen || ItemPickerWindow.IsOpen || SoundPickerWindow.IsOpen || QuestEditorWindow.IsOpen || ChoiceWindow.IsOpen || WorldPlanWindow.IsOpen || BehaviourWindow.IsOpen || CameraSettingsWindow.IsOpen) return false;
			if (TabSelector.instance != null && TabSelector.instance.SelectedTab != TAB.TerrainEdit) return false;
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
			if (ObjectPlacer.Current != null) return false;
			if (DynamicIslands.EditorGizmoHandler != null && DynamicIslands.EditorGizmoHandler.isTransforming) return false;
			return true;
		}

		bool TerrainUnderMouse(out Vector3 point)
		{
			RaycastHit hit;
			point = Vector3.zero;
			if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 5000f) || hit.collider.GetComponent<Terrain>() != terrain)
				return false;
			point = hit.point;
			return true;
		}

		void ModifyTerrain()
		{
			if (stroking && !Input.GetMouseButton(0)) EndStroke();

			Vector3 point = Vector3.zero;
			bool canSculpt = CanSculpt();
			bool overTerrain = canSculpt && TerrainUnderMouse(out point);
			BrushCursor.Update(terrain, overTerrain, overTerrain ? point : Vector3.zero);
			if (overTerrain) LastPoint = point;
			if (modificationAction == TerrainModificationAction.Stamp && canSculpt && !EditorInput.IsTyping && !EditorCamera.Looking) // (Q/E fly the camera then)
			{
				if (Input.GetKeyDown(KeyCode.Q)) TerrainStamps.Rotation -= 15f;
				if (Input.GetKeyDown(KeyCode.E)) TerrainStamps.Rotation += 15f;
			}

			if (!Input.GetMouseButton(0) || !overTerrain || (!stroking && EditorCamera.UsingMouse)) return; // (Alt+drag orbits the camera)
			if (!stroking) BeginStroke(point);
			ApplyAt(point, Time.deltaTime);
		}

		void BeginStroke(Vector3 point)
		{
			stroking = true;
			stamped = false;
			dirtyMin = point; dirtyMax = point;
			flattenTarget = SampleNormalizedHeight(point);
			if (FlattenHeight.HasValue && useFlattenHeight)
				flattenTarget = Mathf.Clamp01((DynamicIslands.EditorWaterLevel + FlattenHeight.Value - terrain.transform.position.y) / terrainData.size.y);
			noiseSign = EditorInput.Shift || SubtractNoise ? -1f : 1f;
			// Full snapshot for undo; only the part the stroke touches is kept when it ends
			int hres = terrainData.heightmapResolution, ares = terrainData.alphamapResolution;
			strokeHeights = terrainData.GetHeights(0, 0, hres, hres);
			strokeAlpha = terrainData.GetAlphamaps(0, 0, ares, ares);
			strokeMask = paintMask != null ? (float[,])paintMask.Clone() : null;
		}

		void ApplyAt(Vector3 point, float deltaTime)
		{
			dt = deltaTime;
			switch (modificationAction)
			{
				case TerrainModificationAction.Raise: ApplyBrush(point, +1f); break;
				case TerrainModificationAction.Lower: ApplyBrush(point, -1f); break;
				case TerrainModificationAction.Flatten: ApplyFlatten(point); break;
				case TerrainModificationAction.Smooth: ApplySmooth(point); break;
				case TerrainModificationAction.Noise: ApplyNoise(point); break;
				case TerrainModificationAction.Erode: ApplyErode(point); break;
				case TerrainModificationAction.PaintLayer: ApplyPaint(point, false); break;
				case TerrainModificationAction.AutoPaint: ApplyPaint(point, true); break;
				case TerrainModificationAction.Stamp:
					if (!stamped && TerrainStamps.Current != null) TerrainStamps.Apply(terrain, TerrainStamps.Current, point, brushRadius, TerrainStamps.Rotation);
					stamped = true;
					break;
				case TerrainModificationAction.Sample:
				case TerrainModificationAction.SampleAverage:
					flattenTarget = SampleNormalizedHeight(point);
					modificationAction = TerrainModificationAction.Flatten;
					break;
			}

			Vector3 r = new Vector3(brushRadius, 0, brushRadius);
			dirtyMin = Vector3.Min(dirtyMin, point - r);
			dirtyMax = Vector3.Max(dirtyMax, point + r);
		}

		void EndStroke()
		{
			stroking = false;
			// Sculpting changes heights/slopes, so refresh the automatic texturing (hand-painted pixels are kept)
			if (modificationAction != TerrainModificationAction.PaintLayer && modificationAction != TerrainModificationAction.AutoPaint)
				TerrainPainter.PaintWorldArea(terrain, DynamicIslands.EditorWaterLevel, dirtyMin, dirtyMax, paintMask);
			RecordUndo();
		}

		/// <summary>Stores the area touched by the stroke (before/after) as one undo step.</summary>
		void RecordUndo()
		{
			if (strokeHeights == null) return;
			int hres = terrainData.heightmapResolution;
			float spacing = terrainData.size.x / (hres - 1);
			Vector3 o = terrain.transform.position;
			int hx0 = Mathf.Clamp(Mathf.FloorToInt((dirtyMin.x - o.x) / spacing) - 1, 0, hres - 1), hz0 = Mathf.Clamp(Mathf.FloorToInt((dirtyMin.z - o.z) / spacing) - 1, 0, hres - 1);
			int hx1 = Mathf.Clamp(Mathf.CeilToInt((dirtyMax.x - o.x) / spacing) + 1, 0, hres - 1), hz1 = Mathf.Clamp(Mathf.CeilToInt((dirtyMax.z - o.z) / spacing) + 1, 0, hres - 1);
			var heightRect = new RectInt(hx0, hz0, hx1 - hx0 + 1, hz1 - hz0 + 1);

			RectInt alphaRect;
			if (!TerrainPainter.WorldToAlphamapRect(terrain, dirtyMin - Vector3.one * 2f, dirtyMax + Vector3.one * 2f, out alphaRect)) { strokeHeights = null; return; }

			var stroke = new TerrainStrokeCommand(terrainData,
				heightRect, TerrainStrokeCommand.Crop(strokeHeights, heightRect),
				alphaRect, TerrainStrokeCommand.Crop(strokeAlpha, alphaRect),
				strokeMask != null ? TerrainStrokeCommand.Crop(strokeMask, alphaRect) : null, paintMask);
			List<CommandUndoRedo.ICommand> follow = FollowGround();
			if (follow.Count == 0) CommandUndoRedo.UndoRedoManager.Insert(stroke);
			else
			{
				var group = new CommandUndoRedo.CommandGroup();
				group.Add(stroke);
				foreach (CommandUndoRedo.ICommand c in follow) group.Add(c);
				CommandUndoRedo.UndoRedoManager.Insert(group);
			}
			strokeHeights = null; strokeAlpha = null; strokeMask = null;
		}

		/// <summary>
		/// Placed objects that stood on the ground where a stroke raised or lowered it go up or down with it (the same undo
		/// step as the stroke): lowered ground left trees, rocks and huts standing in the air, raised ground buried them.
		/// What stands higher - on a deck, a roof, a tower - stays where it is.
		/// </summary>
		List<CommandUndoRedo.ICommand> FollowGround()
		{
			var moved = new List<CommandUndoRedo.ICommand>();
			GameObject root = GameObject.Find("PlacedObjects");
			RuntimeGizmos.TransformGizmo gizmo = DynamicIslands.EditorGizmoHandler;
			if (root == null || gizmo == null || strokeHeights == null || modificationAction == TerrainModificationAction.PaintLayer || modificationAction == TerrainModificationAction.AutoPaint) return moved;
			int res = terrainData.heightmapResolution;
			Vector3 o = terrain.transform.position, size = terrainData.size;
			foreach (EditorGameObject e in root.GetComponentsInChildren<EditorGameObject>(false))
			{
				Vector3 p = e.transform.position;
				if (p.x < dirtyMin.x - 1f || p.x > dirtyMax.x + 1f || p.z < dirtyMin.z - 1f || p.z > dirtyMax.z + 1f) continue;
				// (the ground there before the stroke, from its snapshot, and now)
				float u = Mathf.Clamp((p.x - o.x) / size.x * (res - 1), 0f, res - 1.001f), v = Mathf.Clamp((p.z - o.z) / size.z * (res - 1), 0f, res - 1.001f);
				int x0 = (int)u, z0 = (int)v;
				float fx = u - x0, fz = v - z0;
				float before = Mathf.Lerp(Mathf.Lerp(strokeHeights[z0, x0], strokeHeights[z0, x0 + 1], fx), Mathf.Lerp(strokeHeights[z0 + 1, x0], strokeHeights[z0 + 1, x0 + 1], fx), fz) * size.y + o.y;
				float now = terrain.SampleHeight(p) + o.y;
				if (Mathf.Abs(now - before) < 0.02f || p.y > before + 0.6f || p.y < before - 8f) continue; // (a van set down on a slope by its base stands a few metres below the ground under its middle)
				var command = new RuntimeGizmos.TransformCommand(gizmo, e.transform);
				e.transform.position = p + Vector3.up * (now - before);
				command.StoreNewTransformValues();
				moved.Add(command);
			}
			return moved;
		}

		/// <summary>Runs a complete brush stroke at a point without the mouse (used by the automated tests). flattenTo: Flatten
		/// to this height above the terrain's base instead of the height where the stroke starts (a recipe's "h=").</summary>
		public void SimulateStroke(Vector3 point, int frames, float deltaTime, float? flattenTo = null, bool withField = false)
		{
			useFlattenHeight = withField;
			BeginStroke(point);
			useFlattenHeight = true;
			if (flattenTo.HasValue) flattenTarget = Mathf.Clamp01(flattenTo.Value / terrainData.size.y);
			for (int i = 0; i < frames; i++) ApplyAt(point, deltaTime);
			EndStroke();
		}

		/// <summary>A brush stroke dragged from one point to another without the mouse (the recipes: Flatten to the
		/// height where the drag starts - or to flattenTo - Sample there and flatten along).</summary>
		public void SimulateDrag(Vector3 from, Vector3 to, int frames, float deltaTime, float? flattenTo = null)
		{
			useFlattenHeight = false;
			BeginStroke(from);
			useFlattenHeight = true;
			if (flattenTo.HasValue) flattenTarget = Mathf.Clamp01(flattenTo.Value / terrainData.size.y);
			for (int i = 0; i < frames; i++) ApplyAt(Vector3.Lerp(from, to, frames > 1 ? i / (float)(frames - 1) : 1f), deltaTime);
			EndStroke();
		}

		/// <summary>
		/// Texture brush in alphamap space. Hand painting blends towards the chosen layer and marks the pixels
		/// in the paint mask; AutoPaint blends back to the automatic weights and clears the mask.
		/// </summary>
		void ApplyPaint(Vector3 world, bool auto)
		{
			int res = terrainData.alphamapResolution;
			if (paintMask == null || paintMask.GetLength(0) != res) paintMask = new float[res, res];
			float pixel = terrainData.size.x / res;
			Vector3 local = world - terrain.transform.position;
			float cx = local.x / pixel - 0.5f, cz = local.z / pixel - 0.5f, rs = Mathf.Max(1f, brushRadius / pixel);

			int x0 = Mathf.Clamp(Mathf.FloorToInt(cx - rs), 0, res - 1), z0 = Mathf.Clamp(Mathf.FloorToInt(cz - rs), 0, res - 1);
			int x1 = Mathf.Clamp(Mathf.CeilToInt(cx + rs), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt(cz + rs), 0, res - 1);
			if (x1 < x0 || z1 < z0) return;
			int cols = x1 - x0 + 1, rows = z1 - z0 + 1;

			float[,,] maps = terrainData.GetAlphamaps(x0, z0, cols, rows);
			int layers = maps.GetLength(2);
			if (!auto && paintLayer >= layers) return; // (a mixed style's texture, and the mix was taken out)
			float rate = Mathf.Clamp01(strength * 0.25f * dt);
			var target = new float[layers];

			for (int z = 0; z < rows; z++)
				for (int x = 0; x < cols; x++)
				{
					float dx = (x0 + x - cx) / rs, dz = (z0 + z - cz) / rs, d2 = dx * dx + dz * dz;
					if (d2 >= 1f) continue;
					float w = TerrainBrushes.Weight(d2);

					if (auto) TerrainPainter.AutoWeights(terrain, DynamicIslands.EditorWaterLevel, x0 + x, z0 + z, target);
					else for (int l = 0; l < layers; l++) target[l] = l == paintLayer ? 1f : 0f;

					float t = Mathf.Clamp01(rate * w * 4f);
					for (int l = 0; l < layers; l++) maps[z, x, l] = Mathf.Lerp(maps[z, x, l], target[l], t);

					if (auto) { if (w > 0.3f) paintMask[z0 + z, x0 + x] = 0f; }
					else if (w > 0.05f) paintMask[z0 + z, x0 + x] = 1f;
				}
			terrainData.SetAlphamaps(x0, z0, maps);
		}

		float SampleNormalizedHeight(Vector3 world)
		{
			return (terrain.SampleHeight(world)) / terrainData.size.y;
		}

		/// <summary>Heightmap block under the brush plus per-sample weights (1 at the centre, 0 at the rim, by the brush's falloff).</summary>
		bool GetBrushArea(Vector3 world, out int x0, out int z0, out float[,] weights)
		{
			int res = terrainData.heightmapResolution;
			float spacing = terrainData.size.x / (res - 1);
			Vector3 local = world - terrain.transform.position;
			float cx = local.x / spacing, cz = local.z / spacing, rs = Mathf.Max(1f, brushRadius / spacing);

			x0 = Mathf.Clamp(Mathf.FloorToInt(cx - rs), 0, res - 1);
			z0 = Mathf.Clamp(Mathf.FloorToInt(cz - rs), 0, res - 1);
			int x1 = Mathf.Clamp(Mathf.CeilToInt(cx + rs), 0, res - 1);
			int z1 = Mathf.Clamp(Mathf.CeilToInt(cz + rs), 0, res - 1);
			weights = new float[z1 - z0 + 1, x1 - x0 + 1];
			if (x1 <= x0 || z1 <= z0) return false;

			for (int z = 0; z <= z1 - z0; z++)
				for (int x = 0; x <= x1 - x0; x++)
				{
					float dx = (x0 + x - cx) / rs, dz = (z0 + z - cz) / rs;
					float d2 = dx * dx + dz * dz;
					weights[z, x] = TerrainBrushes.Weight(d2); // (the brush edge: smooth, linear or hard)
				}
			return true;
		}

		void ApplyBrush(Vector3 world, float direction)
		{
			int x0, z0; float[,] w;
			if (!GetBrushArea(world, out x0, out z0, out w)) return;
			float[,] h = terrainData.GetHeights(x0, z0, w.GetLength(1), w.GetLength(0));
			float delta = direction * strength * dt / terrainData.size.y;
			for (int z = 0; z < w.GetLength(0); z++)
				for (int x = 0; x < w.GetLength(1); x++)
					h[z, x] = Mathf.Clamp01(h[z, x] + delta * w[z, x]);
			terrainData.SetHeights(x0, z0, h);
		}

		void ApplyFlatten(Vector3 world)
		{
			int x0, z0; float[,] w;
			if (!GetBrushArea(world, out x0, out z0, out w)) return;
			float[,] h = terrainData.GetHeights(x0, z0, w.GetLength(1), w.GetLength(0));
			float rate = Mathf.Clamp01(strength * 0.5f * dt);
			for (int z = 0; z < w.GetLength(0); z++)
				for (int x = 0; x < w.GetLength(1); x++)
					h[z, x] = Mathf.Lerp(h[z, x], flattenTarget, rate * w[z, x]);
			terrainData.SetHeights(x0, z0, h);
		}

		void ApplySmooth(Vector3 world)
		{
			int x0, z0; float[,] w;
			if (!GetBrushArea(world, out x0, out z0, out w)) return;
			int rows = w.GetLength(0), cols = w.GetLength(1);
			float[,] h = terrainData.GetHeights(x0, z0, cols, rows);
			float[,] result = (float[,])h.Clone();
			float rate = Mathf.Clamp01(strength * 0.5f * dt);
			for (int z = 1; z < rows - 1; z++)
				for (int x = 1; x < cols - 1; x++)
				{
					float avg = (h[z - 1, x] + h[z + 1, x] + h[z, x - 1] + h[z, x + 1] + h[z, x]) / 5f;
					result[z, x] = Mathf.Lerp(h[z, x], avg, rate * w[z, x] * 4f);
				}
			terrainData.SetHeights(x0, z0, result);
		}

		/// <summary>Noise brush: adds the noise field (TerrainBrushes.Noise, bumps NoiseScale metres across) under the brush,
		/// Strength metres a second at the middle; a stroke started with Shift takes it away.</summary>
		void ApplyNoise(Vector3 world)
		{
			int x0, z0; float[,] w;
			if (!GetBrushArea(world, out x0, out z0, out w)) return;
			float[,] h = terrainData.GetHeights(x0, z0, w.GetLength(1), w.GetLength(0));
			float spacing = terrainData.size.x / (terrainData.heightmapResolution - 1);
			TerrainBrushes.AddNoise(h, w, x0, z0, spacing, terrainData.size.y, TerrainBrushes.NoiseScale, noiseSign * strength * dt);
			terrainData.SetHeights(x0, z0, h);
		}

		/// <summary>Erosion brush: a few iterations of thermal erosion under the brush (one sample more all round, where
		/// the material may land); Strength sets how fast.</summary>
		void ApplyErode(Vector3 world)
		{
			int x0, z0; float[,] w;
			if (!GetBrushArea(world, out x0, out z0, out w)) return;
			int res = terrainData.heightmapResolution;
			int bx0 = Mathf.Max(0, x0 - 1), bz0 = Mathf.Max(0, z0 - 1);
			int bx1 = Mathf.Min(res - 1, x0 + w.GetLength(1)), bz1 = Mathf.Min(res - 1, z0 + w.GetLength(0));
			int cols = bx1 - bx0 + 1, rows = bz1 - bz0 + 1;
			var weights = new float[rows, cols];
			for (int z = 0; z < w.GetLength(0); z++)
				for (int x = 0; x < w.GetLength(1); x++)
					weights[z0 - bz0 + z, x0 - bx0 + x] = w[z, x];
			float[,] h = terrainData.GetHeights(bx0, bz0, cols, rows);
			float spacing = terrainData.size.x / (res - 1);
			TerrainBrushes.Erode(h, weights, TerrainBrushes.TalusRise(spacing, terrainData.size.y), Mathf.Min(0.5f, strength * dt * 1.5f), 4);
			terrainData.SetHeights(bx0, bz0, h);
		}
	}
}
