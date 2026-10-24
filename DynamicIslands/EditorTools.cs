using System.Collections.Generic;
using CommandUndoRedo;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>Editor-wide keyboard state.</summary>
	public static class EditorInput
	{
		/// <summary>True while a text field (e.g. the island name) has focus: camera and tool shortcuts are ignored.</summary>
		public static bool IsTyping;

		/// <summary>Tests: keys the editor takes as pressed (and held) in the frame TestFrame, in place of the keyboard's - to try a
		/// shortcut without real key presses. null = the keyboard.</summary>
		internal static HashSet<KeyCode> TestKeys;
		internal static int TestFrame = -1;
		static bool Simulated { get { return TestKeys != null && Time.frameCount == TestFrame; } }

		/// <summary>A key went down in this frame (the keyboard's, or the tests').</summary>
		public static bool KeyDown(KeyCode k) { return Simulated ? TestKeys.Contains(k) : Input.GetKeyDown(k); }

		public static bool Ctrl { get { return Simulated ? TestKeys.Contains(KeyCode.LeftControl) : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); } }
		public static bool Shift { get { return Simulated ? TestKeys.Contains(KeyCode.LeftShift) : Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } }

		/// <summary>
		/// One of the editor's windows that dim the editor (modal) is open: the editor's own shortcuts wait. (Ctrl+O over the
		/// quest editor opened another island while the quest editor stayed open - its Save then wrote the first island's
		/// quest into the second; Ctrl+Z, Ctrl+V, Delete changed the island behind a window.)
		/// </summary>
		public static bool WindowOpen
		{
			get
			{
				return IslandFilesWindow.IsOpen || GeneratorWindow.IsOpen || TextPromptWindow.IsOpen || NoteEditorWindow.IsOpen || ItemPickerWindow.IsOpen ||
					SoundPickerWindow.IsOpen || QuestEditorWindow.IsOpen || ChoiceWindow.IsOpen || WorldPlanWindow.IsOpen || BehaviourWindow.IsOpen ||
					MyIslandsWindow.IsOpen || MainStoryHelper.IsOpen || PlanCheckWindow.IsOpen || PlanProgressWindow.IsOpen || StoryItemsWindow.IsOpen || PiecesWindow.IsOpen ||
					LibraryExportWindow.IsOpen || LibraryImportWindow.IsOpen || LibraryWindow.IsOpen || CameraSettingsWindow.IsOpen;
			}
		}

		/// <summary>
		/// The frame a window opened from another one (a prompt, a picker, a choice) closed: its Esc or Enter is still down
		/// for the window under it that frame, which closed too (losing its edits) or acted on it (Enter in the generator's
		/// name prompt generated). The windows under them wait that frame, as for an open drop-down list (DropList.Busy).
		/// </summary>
		public static int SubWindowClosedFrame = -1;
		public static bool SubWindowJustClosed { get { return SubWindowClosedFrame == Time.frameCount; } }

		/// <summary>Global editor shortcuts; called every frame by the terrain editor (which lives as long as the editor).</summary>
		public static void HandleShortcuts()
		{
			// (not mid-stroke or mid-drag either: an undo then, and the stroke's "before" kept what was just undone)
			if (IsTyping || !Ctrl || WindowOpen || Input.GetMouseButton(0)) return;
			if (KeyDown(KeyCode.Z) && !Shift) UndoRedoManager.Undo();
			else if (KeyDown(KeyCode.Y) || (KeyDown(KeyCode.Z) && Shift)) UndoRedoManager.Redo();
			else if (KeyDown(KeyCode.S)) IslandFilesWindow.QuickSave();
			else if (KeyDown(KeyCode.O)) IslandFilesWindow.Open();
		}
	}

	/// <summary>
	/// Undo step for one terrain brush stroke (sculpt or paint): the heights, texture weights and paint mask of
	/// the area the stroke touched, before and after.
	/// </summary>
	public class TerrainStrokeCommand : ICommand
	{
		readonly TerrainData data;
		readonly RectInt heightRect, alphaRect;
		readonly float[,] heightsBefore, heightsAfter;
		readonly float[,,] alphaBefore, alphaAfter;
		readonly float[,] maskBefore, maskAfter;

		public TerrainStrokeCommand(TerrainData data, RectInt heightRect, float[,] heightsBefore, RectInt alphaRect, float[,,] alphaBefore, float[,] maskBefore, float[,] mask)
		{
			this.data = data;
			this.heightRect = heightRect;
			this.alphaRect = alphaRect;
			this.heightsBefore = heightsBefore;
			this.alphaBefore = alphaBefore;
			this.maskBefore = maskBefore;
			heightsAfter = data.GetHeights(heightRect.x, heightRect.y, heightRect.width, heightRect.height);
			alphaAfter = data.GetAlphamaps(alphaRect.x, alphaRect.y, alphaRect.width, alphaRect.height);
			maskAfter = mask != null ? Crop(mask, alphaRect) : null;
		}

		public void Execute() { Apply(heightsAfter, alphaAfter, maskAfter); }
		public void UnExecute() { Apply(heightsBefore, alphaBefore, maskBefore); }

		void Apply(float[,] heights, float[,,] alpha, float[,] mask)
		{
			if (data == null) return;
			data.SetHeights(heightRect.x, heightRect.y, heights);
			// (a second style mixed in or taken out since: eight layers or four)
			data.SetAlphamaps(alphaRect.x, alphaRect.y, TerrainPainter.FitLayers(alpha, data.alphamapLayers));
			float[,] target = terraineditor.paintMask;
			if (mask != null && target != null && target.GetLength(0) >= alphaRect.yMax && target.GetLength(1) >= alphaRect.xMax)
				for (int z = 0; z < alphaRect.height; z++)
					for (int x = 0; x < alphaRect.width; x++)
						target[alphaRect.y + z, alphaRect.x + x] = mask[z, x];
		}

		public static float[,] Crop(float[,] source, RectInt r)
		{
			var result = new float[r.height, r.width];
			for (int z = 0; z < r.height; z++)
				for (int x = 0; x < r.width; x++)
					result[z, x] = source[r.y + z, r.x + x];
			return result;
		}

		public static float[,,] Crop(float[,,] source, RectInt r)
		{
			int layers = source.GetLength(2);
			var result = new float[r.height, r.width, layers];
			for (int z = 0; z < r.height; z++)
				for (int x = 0; x < r.width; x++)
					for (int l = 0; l < layers; l++)
						result[z, x, l] = source[r.y + z, r.x + x, l];
			return result;
		}
	}

	/// <summary>Undo step for placing (visible after) or deleting (hidden after) objects. Deleted objects are only hidden.</summary>
	public class ObjectVisibilityCommand : ICommand
	{
		readonly List<GameObject> objects;
		readonly bool visibleAfter;

		public ObjectVisibilityCommand(IEnumerable<GameObject> objects, bool visibleAfter)
		{
			this.objects = new List<GameObject>(objects);
			this.visibleAfter = visibleAfter;
		}

		public void Execute() { Set(visibleAfter); }
		public void UnExecute() { Set(!visibleAfter); }

		void Set(bool visible)
		{
			if (DynamicIslands.EditorGizmoHandler != null) DynamicIslands.EditorGizmoHandler.ClearTargets(false);
			foreach (GameObject go in objects)
				if (go != null) go.SetActive(visible);
		}
	}

	/// <summary>A ring on the terrain under the mouse showing the brush size, coloured by tool.</summary>
	public static class BrushCursor
	{
		const int Segments = 64;
		static LineRenderer ring;

		static readonly Color SculptColor = new Color(1f, 1f, 1f, 0.9f);
		static readonly Color AutoColor = new Color(0.4f, 0.9f, 1f, 0.9f);
		static readonly Color[] LayerColors =
		{
			new Color(0.55f, 0.45f, 0.3f, 0.95f), // seabed
			new Color(1f, 0.85f, 0.45f, 0.95f),   // sand
			new Color(0.45f, 0.9f, 0.3f, 0.95f),  // grass
			new Color(0.75f, 0.75f, 0.75f, 0.95f) // rock
		};

		public static void Update(Terrain terrain, bool visible, Vector3 centre)
		{
			if (ring == null)
			{
				if (!visible) return;
				var go = new GameObject("CustomIslands_BrushCursor");
				ring = go.AddComponent<LineRenderer>();
				ring.loop = true;
				ring.positionCount = Segments;
				ring.useWorldSpace = true;
				ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				ring.receiveShadows = false;
				Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
				if (shader != null) ring.material = new Material(shader);
			}
			ring.enabled = visible;
			if (!visible) return;

			Color c = terraineditor.modificationAction == terraineditor.TerrainModificationAction.PaintLayer ? LayerColors[Mathf.Clamp(terraineditor.paintLayer % TerrainPainter.LayerCount, 0, LayerColors.Length - 1)]
				: terraineditor.modificationAction == terraineditor.TerrainModificationAction.AutoPaint ? AutoColor : SculptColor;
			ring.startColor = ring.endColor = c;
			if (ring.material != null) ring.material.color = c;
			float radius = terraineditor.brushRadius;
			ring.startWidth = ring.endWidth = Mathf.Clamp(radius * 0.03f, 0.25f, 2f);

			float baseY = terrain.transform.position.y;
			for (int i = 0; i < Segments; i++)
			{
				float a = i / (float)Segments * Mathf.PI * 2f;
				Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
				p.y = baseY + terrain.SampleHeight(p) + 0.4f;
				ring.SetPosition(i, p);
			}
		}

		public static void Hide()
		{
			if (ring != null) ring.enabled = false;
		}
	}
}
