using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace DynamicIslands.Editor
{
	[System.Serializable]
	public class IslandDataList
	{
		public List<IslandData> list = new List<IslandData>();
	}


	[System.Serializable]
	public class IslandData
	{
		public string Name { get; set; }

		public IslandTransformData TransformData { get; set; }





	}

	[System.Serializable]
	public class IslandTransformData
	{
		public float[] _position = new float[3];
		public float[] _rotation = new float[4];
		public float[] _scale = new float[3];

	

	}

	public class UtilityMethods {
		public static IslandTransformData IslandTransformDataConverter(Transform transform, bool worldSpace = false)
		{
			float[] _position = new float[3];
			float[] _rotation = new float[3];
			float[] _scale = new float[3];

			_position[0] = transform.localPosition.x;
			_position[1] = transform.localPosition.y;
			_position[2] = transform.localPosition.z;
	

			_rotation[0] = transform.localRotation.eulerAngles.x;
			_rotation[1] = transform.localRotation.eulerAngles.y;
			_rotation[2] = transform.localRotation.eulerAngles.z;

			_scale[0] = transform.localScale.x;
			_scale[1] = transform.localScale.y;
			_scale[2] = transform.localScale.z;

			IslandTransformData islandTransform = new IslandTransformData();
			islandTransform._position = _position;
			islandTransform._rotation = _rotation;
			islandTransform._scale=_scale;
			return islandTransform;


		} 


	}
}


/*	public IslandTransformData(Transform transform, bool worldSpace = false)
		{


			_position[0] = transform.localPosition.x;
			_position[1] = transform.localPosition.y;
			_position[2] = transform.localPosition.z;

			_rotation[0] = transform.localRotation.w;
			_rotation[1] = transform.localRotation.x;
			_rotation[2] = transform.localRotation.y;
			_rotation[3] = transform.localRotation.z;

			_scale[0] = transform.localScale.x;
			_scale[1] = transform.localScale.y;
			_scale[2] = transform.localScale.z;



		}*/
