using UnityEngine;

namespace Player
{
    public struct CameraInput
    {
        public Vector2 Look;
    }

    public class PlayerCamera : MonoBehaviour
    {
        [Header("Camera Settings")] 
        [SerializeField] private float sensitivity = 0.2f;
        [SerializeField] private Vector2 verticalLookMinMax = new(-70f, 90f);
        
        private Vector3 _eulerAngles;
        
        public void Initialize(Transform cameraTarget)
        {
            transform.position = cameraTarget.position;
            transform.eulerAngles = _eulerAngles = cameraTarget.eulerAngles;
        }

        public void UpdateRotation(CameraInput input) 
        {
            _eulerAngles += new Vector3(-input.Look.y, input.Look.x, 0f) * sensitivity;
            _eulerAngles.x = Mathf.Clamp(_eulerAngles.x, verticalLookMinMax.x, verticalLookMinMax.y);
            transform.eulerAngles = _eulerAngles;
        }

        public void UpdatePosition(Transform cameraTarget)
        {
            transform.position = cameraTarget.position;
        }
    }
}