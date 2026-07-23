using Generated;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerRuntime : MonoBehaviour
    {
        [Header("Locomotion Scripts")]
        [SerializeField] private PlayerCharacter character;
        [SerializeField] private PlayerCamera camera;
        [SerializeField] private PlayerCollisionTrigger collisionTrigger;

        [Header("Camera Juice Settings")] 
        [SerializeField] private CameraSpring cameraSpring;
        [SerializeField] private CameraLean cameraLean;

        private PlayerInputActions _inputActions;

        private void OnEnable()
        {
            _inputActions = new PlayerInputActions();
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Dispose();
            
        }

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            
            character.Initialize();
            camera.Initialize(character.GetCameraTarget());
            
            cameraSpring.Initialize();
            cameraLean.Initialize();
        }

        private void Update()
        {
            var input = _inputActions.Player;
            var deltaTime = Time.deltaTime;
            
            var cameraInput = new CameraInput { Look = input.Look.ReadValue<Vector2>() };
            var characterInput = new CharacterInput
            {
                Rotation        = camera.transform.rotation,
                Move            = input.Move.ReadValue<Vector2>(),
                IsSprinting     = input.Sprint.IsPressed(),
                IsJumpPressed   = input.Jump.WasPressedThisFrame(),
                IsJumpSustained = input.Jump.IsPressed(), 
                Crouch          = input.Crouch.WasPressedThisFrame() 
                    ? CrouchInput.Toggle 
                    : CrouchInput.None
            };
            
            camera.UpdateRotation(cameraInput);
            character.UpdateInput(characterInput);
            character.UpdateBody(deltaTime); 
            
            #if UNITY_EDITOR
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                var ray = new Ray(camera.transform.position, camera.transform.forward);
                if (Physics.Raycast(ray, out var hit))
                {
                    Teleport(hit.point); 
                }
            }
            #endif
        }

        private void FixedUpdate()
        {
            collisionTrigger.UpdateCollisionTrigger(camera.transform.position);
        }

        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            var cameraTarget = character.GetCameraTarget();
            var stateInfo = character.GetStateInfo();
            
            camera.UpdatePosition(character.GetCameraTarget());

            cameraSpring.UpdateSpring(cameraTarget.up, deltaTime);
            cameraLean.UpdateLean(
                stateInfo.Current.IsActive(Stance.Slide), 
                stateInfo.Current.Acceleration, 
                cameraTarget.up, 
                deltaTime
            );
        }

        private void Teleport(Vector3 position)
        {
            character.SetPosition(position); 
        }
    }
}
