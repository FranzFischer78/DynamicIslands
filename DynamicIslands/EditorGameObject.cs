using RuntimeGizmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DynamicIslands.Editor
{



	public class EditorGameObject : MonoBehaviour
	{
		public string GameObjectName;
		public float arrowLength = 5;

		void Start()
		{
			

		}


		public void ShowGizmos(GizmosType GizmoType)
		{
			switch (GizmoType)
			{
				case GizmosType.Position:
					break;

				case GizmosType.Rotation:
					break;

				case GizmosType.Scale:
					break;
			}
		}

		public enum GizmosType
		{
			Position = 0,
			Rotation =1,
			Scale = 2
		}

		
	}

	
}
