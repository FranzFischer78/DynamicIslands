using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The object being placed: it follows the mouse over the terrain (and other objects) until a left click puts it
	/// down. Q / E turn it, [ / ] make it smaller / bigger, Ctrl + mouse fine-tunes the position, Esc cancels.
	/// Holding Shift while clicking keeps placing copies. "Random" and "Slope" (PlacementOptions) randomise the
	/// turn and size, and lean objects with the ground.
	/// </summary>
	public class ObjectPlacer : MonoBehaviour
	{
		/// <summary>The object being placed now, or null (ROADMAP P3: looked up with FindObjectOfType every frame before).</summary>
		public static ObjectPlacer Current { get { return current != null ? current : null; } }
		static ObjectPlacer current;

		void Awake() { current = this; }
		void OnDestroy() { if (current == this) current = null; }

		public string GameObjectName;

		public Terrain terrain;

		LayerMask layerMask;

		Vector3 lastMouseCoordinate = Vector3.zero;
		Quaternion baseRotation;
		Vector3 baseScale;
		float yaw;
		float scale = 1f;
		/// <summary>The turn and size Q/E and [ ] give the object being placed (tests).</summary>
		public float Yaw { get { return yaw; } }
		public float ScaleFactor { get { return scale; } }
		Vector3 groundNormal = Vector3.up;
		Collider[] ownColliders;

		/// <summary>Turn and size to start with (a copy placed with Shift keeps the previous one's).</summary>
		public float? StartYaw, StartScale;

		public void Start()
		{
			terrain = FindObjectOfType<Terrain>();
			try
			{
				this.gameObject.GetComponent<Collider>().enabled = false;
			}
			catch (Exception) { }

			layerMask = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Obstruction"));
			ownColliders = GetComponentsInChildren<Collider>(true);
			// (an object of Raft's islands that stood almost straight in Raft's scene stands straight - a ladder leant 14°)
			baseRotation = PlacementOptions.Straight(transform.rotation);
			baseScale = transform.localScale;
			if (PlacementOptions.RandomTurnAndSize && !PlacementOptions.SnapToGrid)
			{
				yaw = UnityEngine.Random.Range(0f, 360f);
				scale = UnityEngine.Random.Range(0.8f, 1.25f);
			}
			else
			{
				yaw = StartYaw ?? 0f;
				scale = StartScale ?? 1f;
			}
			Apply();
		}

		void Update()
		{
			// (Esc cancels: the gizmo selects objects on click again - it didn't until the tab changed)
			if (Input.GetKeyDown(KeyCode.Escape)) { EscKey.Swallow(); if (DynamicIslands.EditorGizmoHandler != null) DynamicIslands.EditorGizmoHandler.placingObject = false; Destroy(this.gameObject); return; }

			if (!EditorInput.IsTyping)
			{
				float turn = PlacementOptions.SnapToGrid ? 90f : 15f;
				if (Input.GetKeyDown(KeyCode.Q) && !EditorCamera.Looking) yaw = PlacementOptions.SnapToGrid ? Mathf.Round((yaw - turn) / turn) * turn : yaw - turn;
				if (Input.GetKeyDown(KeyCode.E) && !EditorCamera.Looking) yaw = PlacementOptions.SnapToGrid ? Mathf.Round((yaw + turn) / turn) * turn : yaw + turn;
				if (Input.GetKeyDown(KeyCode.LeftBracket)) scale = Mathf.Max(0.1f, scale / 1.1f);
				if (Input.GetKeyDown(KeyCode.RightBracket)) scale = Mathf.Min(10f, scale * 1.1f);
			}

			Vector3 mouseDelta = Input.mousePosition - lastMouseCoordinate;
			if (!EditorInput.Ctrl) FollowMouse();
			else
			{
				// Fine-tune the position with the mouse
				mouseDelta.Normalize();
				mouseDelta = mouseDelta / 10;
				this.gameObject.transform.position += Clamp(mouseDelta, -1f, 1f);
			}
			lastMouseCoordinate = Input.mousePosition;
			Apply();

			if (Input.GetMouseButtonDown(0) && !MouseOverUI() && !EditorCamera.UsingMouse) Place();
		}

		/// <summary>Moves the object to the first thing under the mouse that isn't the object itself.</summary>
		void FollowMouse()
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			foreach (RaycastHit hit in Physics.RaycastAll(ray, Mathf.Infinity, layerMask).OrderBy(h => h.distance))
			{
				if (hit.collider == null || ownColliders.Contains(hit.collider) || hit.collider.transform.IsChildOf(transform)) continue;
				transform.position = PlacementOptions.FloatIfBlock(GameObjectName, PlacementOptions.Snap(hit.point));
				// (on bare ground, standing straight: down to the lowest ground under its base, so a house on legs or a van on
				// a slope doesn't stand on its high side with the low side in the air)
				if (hit.collider is TerrainCollider && !PlacementOptions.AlignToSlope && !PlacementOptions.SnapToGrid)
					transform.position = new Vector3(transform.position.x, PlacementOptions.RestingPivotY(gameObject), transform.position.z);
				groundNormal = hit.normal;
				return;
			}
		}

		void Apply()
		{
			// Grid building stands straight: blocks don't lean with the slope
			transform.rotation = PlacementOptions.SnapToGrid ? Quaternion.Euler(0f, yaw, 0f) * baseRotation : PlacementOptions.Upright(yaw, baseRotation, groundNormal);
			transform.localScale = baseScale * scale;
		}

		void Place()
		{
			try { this.gameObject.GetComponent<Collider>().enabled = true; } catch (Exception) { }

			DynamicIslands.EditorGizmoHandler.placingObject = false;
			// (an island file holds at most so many objects: past that it couldn't be opened again)
			int adding = Editor.GroupLibrary.IsGroup(GameObjectName) ? Mathf.Max(1, this.gameObject.GetComponentsInChildren<Editor.GroupMember>(true).Length) : 1;
			if (!Editor.ObjectLimit.Allow(adding)) { Destroy(this.gameObject); return; }
			Transform placedRoot = GameObject.Find("PlacedObjects").transform;
			if (Editor.GroupLibrary.IsGroup(GameObjectName))
			{
				// A group becomes its separate objects again (one undo step)
				List<GameObject> created = Editor.GroupLibrary.Expand(this.gameObject, placedRoot);
				if (created.Count > 0) CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(created, true));
			}
			else
			{
				Editor.EditorGameObject.Attach(this.gameObject, GameObjectName);
				this.gameObject.transform.parent = placedRoot;
				// Placing is undoable (Ctrl+Z hides the object again)
				CommandUndoRedo.UndoRedoManager.Insert(new ObjectVisibilityCommand(new[] { this.gameObject }, true));
			}

			// Shift: keep placing the same object
			if (EditorInput.Shift)
			{
				GameObject next = PlaceableCatalog.Spawn(GameObjectName, null);
				if (next != null)
				{
					next.transform.position = transform.position;
					ObjectPlacer placer = next.AddComponent<ObjectPlacer>();
					placer.GameObjectName = GameObjectName;
					placer.StartYaw = yaw;
					placer.StartScale = scale;
					DynamicIslands.EditorGizmoHandler.placingObject = true;
				}
			}
			Destroy(this);
		}

		private bool MouseOverUI()
		{
			return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
		}

		public Vector3 Clamp(Vector3 value, float min, float max)
		{
			value.x = Mathf.Clamp(value.x, min, max);
			value.y = Mathf.Clamp(value.y, min, max);
			value.z = Mathf.Clamp(value.z, min, max);
			return value;
		}
	}
}
