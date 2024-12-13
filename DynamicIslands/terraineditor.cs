using DynamicIslands.Editor;
using RaftModLoader;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicIslands
{
	public class terraineditor : MonoBehaviour
	{
		// Reference to the terrain in the scene
		public static Terrain terrain;
		public static TerrainData terrainData;

		// The brush size and strength
		public float brushSize = 1.0f;
		public float brushStrength = 0.01f;

		public float moveSpeed = 5.0f;

		// The size of the terrain in terrain units
		public Vector3 terrainSize = new Vector3(1000, 600, 1000);

		// The heightmap resolution of the terrain
		public int heightmapResolution = 513;

		// The detail resolution of the terrain
		public int detailResolution = 1024;

		// The base texture resolution of the terrain
		public int baseTextureResolution = 1024;

		public Text CamPos;

		public enum TerrainModificationAction
		{
			Raise,
			Lower,
			Flatten,
			Sample,
			SampleAverage,
		}

		public static TerrainModificationAction modificationAction;

		public static bool allowEditing = false;

		private Camera mainCam;
		private Terrain _targetTerrain;

		public static int brushWidth = 2;
		public static int brushHeight = 2;

		public float _sampledHeight;

		public static float strength = .1f;

		private const string modName = "TerrainEdit";

		private Projector _projector;
		private GameObject currentProjector;

		public static AssetBundle canvasBundle;

		public static GameObject customCanvas = null;

		void Start()
		{
			CamPos = GameObject.Find("CamPos").GetComponent<Text>();
			// Create a new TerrainData object
			terrainData = new TerrainData();

			// Set the size of the terrain
			terrainData.size = terrainSize;

			// Set the heightmap resolution
			terrainData.heightmapResolution = heightmapResolution;

			// Set the detail resolution
			terrainData.SetDetailResolution(detailResolution, 8);

			// Set the base texture resolution
			terrainData.baseMapResolution = baseTextureResolution;

			// Create a new Terrain game object
			terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();

			// Set the position of the terrain
			terrain.transform.position = Vector3.zero;

			terrain.gameObject.layer = (LayerMask)16;
			TerrainCollider terrainCollider;
			try
			{
				// Add a TerrainCollider component to the terrain game object
				terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
				terrainCollider = terrain.gameObject.GetComponent<TerrainCollider>();


			}
			catch { terrainCollider = terrain.gameObject.GetComponent<TerrainCollider>(); }

			ProceduralTerrainGenerator generator = new ProceduralTerrainGenerator();
			//generator.TerrainGenerator(terrain);

			terrainCollider.terrainData = terrain.terrainData;






		}


		void Update()
		{
			try
			{
				//UI
				var cam = Camera.main.transform.position;
				CamPos.text = "X" + cam.x + " Y" + cam.y + " Z" + cam.z;

				ModifyTerrain();

				return;

				if (FindObjectOfType<TabSelector>().SelectedTab == TAB.TerrainEdit)
				{
					// If the left or right mouse button is being held down
					if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
					{

						// Get the size of the screen in pixels
						Vector2 screenSize = new Vector2(Screen.width, Screen.height);
						// Get the mouse position in screen coordinates
						Vector2 mousePos = Input.mousePosition;

						// Subtract the half-width and half-height of the screen from the mouse position
						Vector2 modifiedMousePos = mousePos - (screenSize / 2);

						// Convert the modified mouse position to world coordinates
						Vector3 worldPos = Camera.main.ScreenToWorldPoint(modifiedMousePos);

						//Debug.Log("Mouse position" + mousePos.ToString() + modifiedMousePos.ToString());



						// Get the position of the camera
						Vector3 cameraPos = Camera.main.transform.position;
					

					}
				}
			}
			catch (Exception e) { }
		}

		private void ModifyTerrain()
		{
			//if (!allowEditing) { return; }

			//if (RAPI.IsCurrentSceneMainMenu())
			//{
				//return;
			//}

			if (Input.GetMouseButton(0))
			{
				if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit))
				{
					if (hit.transform.TryGetComponent(out Terrain terrain)) _targetTerrain = terrain;

					print(modificationAction);

					switch (modificationAction)
					{
						case TerrainModificationAction.Raise:

							RaiseTerrain(hit.point, strength, brushWidth, brushHeight);

							break;

						case TerrainModificationAction.Lower:

							LowerTerrain(hit.point, strength, brushWidth, brushHeight);

							break;

						case TerrainModificationAction.Flatten:

							FlattenTerrain(hit.point, _sampledHeight, brushWidth, brushHeight);

							break;

						case TerrainModificationAction.Sample:

							_sampledHeight = SampleHeight(hit.point);

							break;

						case TerrainModificationAction.SampleAverage:

							_sampledHeight = SampleAverageHeight(hit.point, brushWidth, brushHeight);

							break;
					}
				}
			}
		}

		private TerrainData GetTerrainData() => _targetTerrain.terrainData;

		private int GetHeightmapResolution() => GetTerrainData().heightmapResolution;

		private Vector3 GetTerrainSize() => GetTerrainData().size;

		public Vector3 WorldToTerrainPosition(Vector3 worldPosition)
		{
			var terrainPosition = worldPosition - _targetTerrain.GetPosition();

			var terrainSize = GetTerrainSize();

			var heightmapResolution = GetHeightmapResolution();

			terrainPosition = new Vector3(terrainPosition.x / terrainSize.x, terrainPosition.y / terrainSize.y, terrainPosition.z / terrainSize.z);

			return new Vector3(terrainPosition.x * heightmapResolution, 0, terrainPosition.z * heightmapResolution);
		}

		public Vector2Int GetBrushPosition(Vector3 worldPosition, int brushWidth, int brushHeight)
		{
			var terrainPosition = WorldToTerrainPosition(worldPosition);

			var heightmapResolution = GetHeightmapResolution();

			return new Vector2Int((int)Mathf.Clamp(terrainPosition.x - brushWidth / 2.0f, 0.0f, heightmapResolution), (int)Mathf.Clamp(terrainPosition.z - brushHeight / 2.0f, 0.0f, heightmapResolution));
		}

		public Vector2Int GetSafeBrushSize(int brushX, int brushY, int brushWidth, int brushHeight)
		{
			var heightmapResolution = GetHeightmapResolution();

			while (heightmapResolution - (brushX + brushWidth) < 0) brushWidth--;

			while (heightmapResolution - (brushY + brushHeight) < 0) brushHeight--;

			return new Vector2Int(brushWidth, brushHeight);
		}

		public void RaiseTerrain(Vector3 worldPosition, float strength, int brushWidth, int brushHeight)
		{
			var brushPosition = GetBrushPosition(worldPosition, brushWidth, brushHeight);

			var brushSize = GetSafeBrushSize(brushPosition.x, brushPosition.y, brushWidth, brushHeight);

			var terrainData = GetTerrainData();

			var heights = terrainData.GetHeights(brushPosition.x, brushPosition.y, brushSize.x, brushSize.y);

			for (var y = 0; y < brushSize.y; y++)
			{
				for (var x = 0; x < brushSize.x; x++)
				{
					heights[y, x] += strength * Time.deltaTime;
				}
			}

			terrainData.SetHeights(brushPosition.x, brushPosition.y, heights);
		}

		public void LowerTerrain(Vector3 worldPosition, float strength, int brushWidth, int brushHeight)
		{
			var brushPosition = GetBrushPosition(worldPosition, brushWidth, brushHeight);

			var brushSize = GetSafeBrushSize(brushPosition.x, brushPosition.y, brushWidth, brushHeight);

			var terrainData = GetTerrainData();

			var heights = terrainData.GetHeights(brushPosition.x, brushPosition.y, brushSize.x, brushSize.y);

			for (var y = 0; y < brushSize.y; y++)
			{
				for (var x = 0; x < brushSize.x; x++)
				{
					heights[y, x] -= strength * Time.deltaTime;
				}
			}

			terrainData.SetHeights(brushPosition.x, brushPosition.y, heights);
		}

		public void FlattenTerrain(Vector3 worldPosition, float height, int brushWidth, int brushHeight)
		{
			var brushPosition = GetBrushPosition(worldPosition, brushWidth, brushHeight);

			var brushSize = GetSafeBrushSize(brushPosition.x, brushPosition.y, brushWidth, brushHeight);

			var terrainData = GetTerrainData();

			var heights = terrainData.GetHeights(brushPosition.x, brushPosition.y, brushSize.x, brushSize.y);

			for (var y = 0; y < brushSize.y; y++)
			{
				for (var x = 0; x < brushSize.x; x++)
				{
					heights[y, x] = height;
				}
			}

			terrainData.SetHeights(brushPosition.x, brushPosition.y, heights);
		}

		public float SampleHeight(Vector3 worldPosition)
		{
			var terrainPosition = WorldToTerrainPosition(worldPosition);

			return GetTerrainData().GetInterpolatedHeight((int)terrainPosition.x, (int)terrainPosition.z);
		}

		public float SampleAverageHeight(Vector3 worldPosition, int brushWidth, int brushHeight)
		{
			var brushPosition = GetBrushPosition(worldPosition, brushWidth, brushHeight);

			var brushSize = GetSafeBrushSize(brushPosition.x, brushPosition.y, brushWidth, brushHeight);

			var heights2D = GetTerrainData().GetHeights(brushPosition.x, brushPosition.y, brushSize.x, brushSize.y);

			var heights = new float[heights2D.Length];

			var i = 0;

			for (int y = 0; y <= heights2D.GetUpperBound(0); y++)
			{
				for (int x = 0; x <= heights2D.GetUpperBound(1); x++)
				{
					heights[i++] = heights2D[y, x];
				}
			}

			return heights.Average();
		}
	}
}






