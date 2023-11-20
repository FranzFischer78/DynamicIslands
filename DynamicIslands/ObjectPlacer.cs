using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DynamicIslands.Editor
{
	public class ObjectPlacer : MonoBehaviour
	{
		public string GameObjectName;

		public Terrain terrain;

		LayerMask layerMask;

		Vector3 lastMouseCoordinate = Vector3.zero;

		public void Start()
		{
			terrain = FindObjectOfType<Terrain>();
			try
			{
				this.gameObject.GetComponent<Collider>().enabled = false;
			}
			catch (Exception e) { }

			layerMask = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Obstruction"));


		}

		void FixedUpdate()
		{
			Vector3 mouseDelta = Input.mousePosition - lastMouseCoordinate;
			if (Input.GetKey(KeyCode.Escape))
			{
				Destroy(this.gameObject);
			}
			if (Input.GetMouseButton(0) && !MouseOverUI())
			{
				//Placing object at the current position
				try { this.gameObject.GetComponent<Collider>().enabled = true; } catch (Exception e) { }

				this.gameObject.AddComponent<Editor.EditorGameObject>();
				this.gameObject.GetComponent<Editor.EditorGameObject>().GameObjectName = GameObjectName;

				DynamicIslands.EditorGizmoHandler.placingObject = false;

				this.gameObject.transform.parent = GameObject.Find("PlacedObjects").transform;

				Destroy(this);
			}


			if (!Input.GetKey(KeyCode.LeftControl))
			{

				RaycastHit hit;
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
				{
					if (hit.collider != null)
					{
						if (hit.collider.gameObject != this.gameObject)
						{
							Collider[] cols = this.gameObject.GetComponentsInChildren<Collider>();
							foreach (Collider col in cols)
							{
								if (hit.collider == col)
								{
									lastMouseCoordinate = Input.mousePosition;
									return;
								}
							}

							/*if (hit.collider != this.gameObject.GetComponentInChildren<Collider>())
							{*/
							//Debug.Log(hit.point);
							this.gameObject.transform.position = hit.point;
							//}
						}
					}
				}
			}
			else
			{
				//fine tune object placement
				mouseDelta.Normalize();
				mouseDelta = mouseDelta / 10;
				Debug.Log(mouseDelta);

				this.gameObject.transform.position += Clamp(mouseDelta, -1f, 1f);
			}

			lastMouseCoordinate = Input.mousePosition;



		}

		private bool MouseOverUI()
		{
			return EventSystem.current.IsPointerOverGameObject();
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
