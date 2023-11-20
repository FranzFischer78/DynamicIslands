using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovementScript : MonoBehaviour
{
	[SerializeField]
	[Tooltip("Movement speed of the Camera")]
	float speed = 0.06f;
	[SerializeField]
	[Tooltip("Zoom speed of the Camera")]
	float zoomSpeed = 10.0f;
	[SerializeField]
	[Tooltip("Rotation speed of the Camera")]
	float rotateSpeed = 0.1f;

	float maxHeight = 40f;
	float minHeight = 4f;

	Vector2 p1;
	Vector2 p2;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
		float hsp = speed * Input.GetAxis("Horizontal");
		float vsp = speed * Input.GetAxis("Vertical");
		float scrollSp = -zoomSpeed * Input.GetAxis("Mouse ScrollWheel");

		if ((transform.position.y >= maxHeight) && (scrollSp > 0))
		{
			scrollSp = 0;
		} else if ((transform.position.y <= minHeight) && (scrollSp < 0))
		{
			scrollSp = 0;
		}




		Vector3 verticalMove = new Vector3(0, scrollSp, 0);
		Vector3 lateralMove = hsp * transform.right;
		Vector3 forwardMove = transform.forward;
		forwardMove.y = 0;
		forwardMove.Normalize();
		forwardMove *= vsp;

		Vector3 move = verticalMove + lateralMove + forwardMove;

		transform.position += move;

		getCameraRotation();

		if (Input.GetKey(KeyCode.LeftShift))
		{
			speed = 5f;
		}
		else
		{
			speed = 0.6f;
		}
    }

	void getCameraRotation()
	{ 
		if (Input.GetMouseButtonDown(2))
		{
			p1 = Input.mousePosition;
		}

		if (Input.GetMouseButton(2))
		{
			p2 = Input.mousePosition;

			float dx = (p2 - p1).x * rotateSpeed;
			float dy = (p2 - p1).y * rotateSpeed;

			transform.rotation *= Quaternion.Euler(new Vector3(0, dx, 0));
			transform.GetChild(0).transform.rotation *= Quaternion.Euler(new Vector3(-dy, 0, 0));

			p1 = p2;
		}
	}
}
