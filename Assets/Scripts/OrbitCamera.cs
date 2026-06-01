using UnityEngine;

// orbitalna kamera oko centra scene
// desni klik + vuci = rotacija, scroll = zoom
// pozicija = Rotation(elevation, azimuth) * (0, 0, -distance)

// dokumentacija:
// https://docs.unity3d.com/ScriptReference/Input.GetAxis.html

public class OrbitCamera : MonoBehaviour
{
    private float distance = 15f;
    private float currentX = 30f; // elevacija (stepeni)
    private float currentY = 45f; // azimut (stepeni)
    private float rotationSpeed = 3f;
    private float zoomSpeed = 3f;

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            currentX += Input.GetAxis("Mouse Y") * rotationSpeed;
            currentY -= Input.GetAxis("Mouse X") * rotationSpeed;

            // spreci gimbal lock
            currentX = Mathf.Clamp(currentX, 5f, 85f);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, 5f, 40f);
        }

        // sferne -> kartezijanske
        Quaternion rotation = Quaternion.Euler(currentX, currentY, 0);
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        transform.position = offset;
        transform.LookAt(Vector3.zero);
    }
}
