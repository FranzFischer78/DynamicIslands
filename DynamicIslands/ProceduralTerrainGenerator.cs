using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DynamicIslands
{
	public class ProceduralTerrainGenerator
	{

		public static int width = 256;    // Width of the terrain
		public static int length = 256;   // Length of the terrain
		public static float scale = 20f;  // Scale factor for the height values
		public static float maxHeight = 10f;   // Maximum height of the terrain

		public float lakeHeight = 2f;     // Height of the lake surface
		public float lakeRadius = 30f;    // Radius of the lake
		public float hillHeight = 6f;     // Height of the hill
		public Vector2 hillCenter = new Vector2(128f, 128f);   // Center position of the hill
		public float hillRadius = 50f;    // Radius of the hill
		public float mountainHeight = 10f; // Height of the mountain
		public Vector2 mountainCenter = new Vector2(192f, 192f);   // Center position of the mountain
		public float mountainRadius = 40f;    // Radius of the mountain

		public AnimationCurve falloffCurve;   // Falloff curve for smooth blending
		public float curveScale = 5f;         // Scale factor for the curve

		public void TerrainGenerator(Terrain terrain)
		{
			falloffCurve = CreateLinearFalloffCurve();
			terrain.terrainData = GenerateTerrain(terrain.terrainData);
		}

		AnimationCurve CreateLinearFalloffCurve()
		{
			AnimationCurve curve = new AnimationCurve();
			float stepSize = 0.01f;

			for (float t = 0f; t <= 1f; t += stepSize)
			{
				float noise = Mathf.PerlinNoise(t * curveScale, 0f);
				curve.AddKey(t, noise);
			}

			return curve;
		}

		TerrainData GenerateTerrain(TerrainData terrainData)
		{
			terrainData.heightmapResolution = width + 1;
			terrainData.size = new Vector3(width, maxHeight, length);
			terrainData.SetHeights(0, 0, GenerateHeights());
			return terrainData;
		}

		float[,] GenerateHeights()
		{
			float[,] heights = new float[width, length];
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < length; y++)
				{
					heights[x, y] = CalculateHeight(x, y);
				}
			}
			return heights;
		}


		float CalculateHeight(int x, int y)
		{
			// Generate random height value based on the x and y coordinates
			float xCoord = (float)x / width * scale;
			float yCoord = (float)y / length * scale;
			float height = Mathf.PerlinNoise(xCoord, yCoord);

			// Add features to the terrain
			Vector2 position = new Vector2(x, y);
			height += LakeFeature(position);
			height += HillFeature(position);
			height += MountainFeature(position);

			return height;
		}

		float LakeFeature(Vector2 position)
		{
			// Calculate the distance from the center of the lake feature
			float distance = Vector2.Distance(position, new Vector2(width / 2f, length / 2f));

			// Check if the position is within the lake radius
			if (distance < lakeRadius)
			{
				// Map the distance within the lake radius to a falloff value between 0 and 1
				float normalizedDistance = distance / lakeRadius;
				float falloff = falloffCurve.Evaluate(normalizedDistance);

				// Map the falloff value to a height value between 0 and -lakeHeight
				float height = Mathf.Lerp(0f, -lakeHeight, falloff);
				return height;
			}

			return 0f;
		}

		float HillFeature(Vector2 position)
		{
			// Calculate the distance from the center of the hill feature
			float distance = Vector2.Distance(position, hillCenter);

			// Check if the position is within the hill radius
			if (distance < hillRadius)
			{
				// Map the distance within the hill radius to a falloff value between 0 and 1
				float normalizedDistance = distance / hillRadius;
				float falloff = falloffCurve.Evaluate(normalizedDistance);

				// Map the falloff value to a height value between 0 and hillHeight
				float height = Mathf.Lerp(0f, hillHeight, falloff);
				return height;
			}

			return 0f;
		}

		float MountainFeature(Vector2 position)
		{
			// Calculate the distance from the center of the mountain feature
			float distance = Vector2.Distance(position, mountainCenter);

			// Check if the position is within the mountain radius
			if (distance < mountainRadius)
			{
				// Map the distance within the mountain radius to a falloff value between 0 and 1
				float normalizedDistance = distance / mountainRadius;
				float falloff = falloffCurve.Evaluate(normalizedDistance);

				// Map the falloff value to a height value between 0 and mountainHeight
				float height = Mathf.Lerp(0f, mountainHeight, falloff);
				return height;
			}

			return 0f;
		}
	}
}
