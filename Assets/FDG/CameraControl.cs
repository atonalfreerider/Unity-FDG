using UnityEngine;

namespace FDG
{
    public class CameraControl : MonoBehaviour
    {
        // From: 
        // http://answers.unity3d.com/questions/29741/mouse-look-script.html 
        public float SensitivityX = 15f;
        public float SensitivityY = 15f;

        public float MinimumXRotation = -360f;
        public float MaximumXRotation = 360f;

        public float MinimumYRotation = -60f;
        public float MaximumYRotation = 60f;

        float rotationX;
        float rotationY;

        void Update()
        {
            // always cast rays so that targets can get highlighted 

            // Rotate the camera on right click 
            if (Input.GetMouseButton(1))
            {
                // Read the mouse input axis 
                rotationX += Input.GetAxis("Mouse X") * SensitivityX;
                rotationY += Input.GetAxis("Mouse Y") * SensitivityY;
                rotationX = ClampAngle(
                    rotationX,
                    MinimumXRotation,
                    MaximumXRotation);
                rotationY = ClampAngle(
                    rotationY,
                    MinimumYRotation,
                    MaximumYRotation);
                Quaternion xQuaternion =
                    Quaternion.AngleAxis(rotationX, Vector3.up);
                Quaternion yQuaternion =
                    Quaternion.AngleAxis(rotationY, -Vector3.right);
                transform.localRotation = xQuaternion * yQuaternion;
            }

            MoveCamera();
        }

        static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f)
            {
                angle += 360f;
            }

            if (angle > 360f)
            {
                angle -= 360f;
            }

            return Mathf.Clamp(angle, min, max);
        }

        void MoveCamera()
        {
            if (Input.GetKey(KeyCode.W))
            {
                transform.position +=
                    transform.forward.normalized * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.A))
            {
                transform.position -=
                    transform.right.normalized * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.S))
            {
                transform.position -=
                    transform.forward.normalized * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.D))
            {
                transform.position +=
                    transform.right.normalized * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.Q))
            {
                transform.position += Vector3.down * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.E))
            {
                transform.position += Vector3.up * Time.deltaTime;
            }
        }
    }
}