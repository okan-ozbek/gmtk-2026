using KinematicCharacterController;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

/*
 * Todo
 * Cleanup code in this file
 * Check out the vignette from the video
 * Fix up the jumping
 * Organize the files
 * Start making settings
 * Start making scene transitions
 * Find assets
 * Create win condition
 * Create monster ai (basic)
 * Also fix camera limits, now we can do 360s
 */

namespace Player
{
    public enum CrouchInput
    {
        None,
        Toggle,
    };

    public enum Stance
    {
        Stand,
        Crouch,
        Slide,
    }

    public struct CharacterInput
    {
        public Quaternion Rotation;
        public Vector2 Move;
        public bool IsSprinting;
        public bool IsJumpPressed;
        public bool IsJumpSustained;
        public CrouchInput Crouch;
    }

    public struct CharacterState
    {
        public bool IsActive(Stance stance) => stance == Stance;
        public bool IsInactive(Stance stance) => stance != Stance;
        
        public bool IsGrounded() => Grounded;
        public bool IsAirborne() => !Grounded;
        
        public bool Grounded;
        public Stance Stance;
        public Vector3 Velocity;
        public Vector3 Acceleration;
    }

    public struct CharacterStateInfo
    {
        public CharacterState Current;
        public CharacterState Previous;
        public CharacterState Temporary;

        public void UpdatePrevious()
        {
            Previous = Temporary;
        }

        public void UpdateTemporary()
        {
            Temporary = Current;
        }
    }

    public struct InputRequest
    {
        public Quaternion Rotation;
        public Vector3 Move;
        public bool IsJumping;
        public bool IsJumpingSustained;
        public bool IsCrouching;
        public bool IsAirborneCrouching;
        public bool IsSprinting;
    }
    
    public class PlayerCharacter : MonoBehaviour, ICharacterController
    {
        #region Inspector Fields
        [Header("Character Controller")]
        [SerializeField] private KinematicCharacterMotor motor;
        [Space]
        [Header("Other Object Related Settings")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform root;
        [Space]
        [Header("Character Movement Settings")] 
        [Header("Grounded Movement")]
        [SerializeField] private float walkSpeed = 15f;
        [SerializeField] private float sprintSpeed = 20f;
        [SerializeField] private float crouchSpeed = 7f;
        [SerializeField] private float moveResponse = 25f;
        [SerializeField] private float crouchResponse = 20f;
        [Space]
        [Header("Airborne Movement")]
        [SerializeField] private float jumpSpeed = 20f;
        [SerializeField] private float airSpeed = 15f;
        [SerializeField] private float airAcceleration = 70f;
        [SerializeField] private float coyoteTime = 0.2f;
        [Space] 
        [Header("Sliding Movement")] 
        [SerializeField] private float slideStartSpeed = 25f;
        [SerializeField] private float slideEndSpeed = 15f;
        [SerializeField] private float slideFriction = 0.8f;
        [SerializeField] private float slideSteerAcceleration = 5f;
        [Space]
        [Header("Gravity Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float jumpSustainGravityMultiplier = 0.4f;
        [SerializeField] private float gravity = -90f;
        [SerializeField] private float slideGravity = -90f;
        [Space]
        [Header("Stance Settings")]
        [SerializeField] private float standHeight = 2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchHeightResponse = 15f;
        [Range(0f, 1f)]
        [SerializeField] private float standCameraTargetHeight = 0.9f;
        [Range(0f, 1f)]
        [SerializeField] private float crouchCameraTargetHeight = 0.7f;
        #endregion
        
        #region Private Fields
        private Collider[] _overlapResults;

        private CharacterStateInfo _stateInfo;
        private InputRequest _request;

        private float _timeSinceUngrounded;
        private float _timeSinceJumpRequested;
        private bool _ungroundedDueToJump;
        #endregion
    
        public void Initialize()
        {
            _stateInfo.Current.Stance = Stance.Stand;
            _stateInfo.UpdatePrevious();
            
            _overlapResults = new Collider[8];
            
            motor.CharacterController = this;
        }

        public void UpdateInput(CharacterInput input)
        {
            _request.Rotation = input.Rotation;

            _request.Move = new Vector3(input.Move.x, 0f, input.Move.y);
            _request.Move = Vector3.ClampMagnitude(_request.Move, 1f);
            _request.Move = input.Rotation * _request.Move;

            _request.IsSprinting = input.IsSprinting;
            
            var wasJumpingLastFrame = _request.IsJumping;
            _request.IsJumping = input.IsJumpPressed || _request.IsJumping;
            if (_request.IsJumping && !wasJumpingLastFrame)
            {
                _timeSinceJumpRequested = 0f;
            }
            
            _request.IsJumpingSustained = input.IsJumpSustained;

            var wasCrouchingLastFrame = _request.IsCrouching;
            _request.IsCrouching = input.Crouch switch
            {
                CrouchInput.Toggle => !_request.IsCrouching,
                _ => _request.IsCrouching
            };

            _request.IsAirborneCrouching = _request.IsCrouching switch
            {
                true when !wasCrouchingLastFrame => !_stateInfo.Current.Grounded,
                false when wasCrouchingLastFrame => false,
                _ => _request.IsAirborneCrouching
            };
        }

        public void UpdateBody(float deltaTime)
        {
            var currentHeight = motor.Capsule.height;
            var desiredCameraHeight = (_stateInfo.Current.IsActive(Stance.Stand))
                ? standCameraTargetHeight
                : crouchCameraTargetHeight;
            
            var normalizedHeight = currentHeight / standHeight;
            
            var cameraTargetHeight = currentHeight * desiredCameraHeight;
            var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

            cameraTarget.localPosition = Vector3.Lerp
            (
                a: cameraTarget.localPosition, 
                b: new Vector3(0f, cameraTargetHeight, 0f), 
                t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
            );

            root.localScale = Vector3.Lerp
            (
                a: root.localScale,
                b: rootTargetScale,
                t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
            );
        }
    
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            var forward = Vector3.ProjectOnPlane
            (
                _request.Rotation * Vector3.forward, 
                motor.CharacterUp
            );

            if (forward != Vector3.zero)
            {
                currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
            }
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _stateInfo.Current.Grounded = motor.GroundingStatus.IsStableOnGround;
            _stateInfo.Current.Velocity = motor.Velocity;
            _stateInfo.Current.Acceleration = Vector3.zero;
            
            GroundedMovement(ref currentVelocity, deltaTime);
            AirborneMovement(ref currentVelocity, deltaTime);
            Gravity(ref currentVelocity, deltaTime);
            Jump(ref currentVelocity, deltaTime);
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            _stateInfo.UpdateTemporary();
            
            Crouch();
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            Uncrouch();
            
            _stateInfo.UpdatePrevious();
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            if (_stateInfo.Current.IsAirborne() && _stateInfo.Current.IsActive(Stance.Slide))
            {
                _stateInfo.Current.Stance = Stance.Crouch;
            }
        }
        
        public Transform GetCameraTarget() => cameraTarget;
        
        public CharacterStateInfo GetStateInfo() => _stateInfo;

        public void SetPosition(Vector3 position, bool noVelocity = true)
        {
            motor.SetPosition(position);

            if (noVelocity)
            {
                motor.BaseVelocity = Vector3.zero;
            }
        }
        
        private int GetCharacterOverlapResults()
        {
            return motor.CharacterOverlap(
                motor.TransientPosition,
                motor.TransientRotation,
                _overlapResults,
                motor.CollidableLayers,
                QueryTriggerInteraction.Ignore
            );
        }
        
        private void GroundedMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            if (_stateInfo.Current.IsAirborne())
            {
                return;
            }

            _timeSinceUngrounded = 0f;
            _ungroundedDueToJump = false;
            
            var groundedMovement = motor.GetDirectionTangentToSurface
            (
                direction: _request.Move,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _request.Move.magnitude;

            SlidingMovement(ref currentVelocity, groundedMovement, deltaTime);
            NormalMovement(ref currentVelocity, groundedMovement, deltaTime);
            ApplyFriction(ref currentVelocity, groundedMovement, deltaTime);
        }

        private void ApplyFriction(ref Vector3 currentVelocity, Vector3 groundedMovement, float deltaTime)
        {
            if (_stateInfo.Current.IsInactive(Stance.Slide))
            {
                return;
            }
            
            currentVelocity -= currentVelocity * (slideFriction * deltaTime);

            SlopeAcceleration(ref currentVelocity, deltaTime); 
            SlideSteer(ref currentVelocity, groundedMovement, deltaTime);
            ExitSlide(currentVelocity);
        }

        private void SlopeAcceleration(ref Vector3 currentVelocity, float deltaTime)
        {
            var force = Vector3.ProjectOnPlane
            (
                vector: -motor.CharacterUp,
                planeNormal: motor.GroundingStatus.GroundNormal
            ) * slideGravity;

            currentVelocity -= force * deltaTime;
        }

        private void SlideSteer(ref Vector3 currentVelocity, Vector3 groundedMovement, float deltaTime)
        {
            var currentSpeed = currentVelocity.magnitude;
            var targetVelocity = groundedMovement * currentSpeed;
            var steerVelocity = currentVelocity;
            var steerForce = (targetVelocity - steerVelocity) * (slideSteerAcceleration * deltaTime);
            
            steerVelocity += steerForce;
            steerVelocity = Vector3.ClampMagnitude(steerVelocity, currentSpeed);

            _stateInfo.Current.Acceleration = (steerVelocity - currentVelocity) / deltaTime;
            currentVelocity = steerVelocity;
        }

        private void ExitSlide(Vector3 currentVelocity)
        {
            if (currentVelocity.magnitude < slideEndSpeed)
            {
                _stateInfo.Current.Stance = Stance.Crouch;
            } 
        }

        private void SlidingMovement(ref Vector3 currentVelocity, Vector3 groundedMovement, float deltaTime)
        {
            if (CanPerformSlide(currentVelocity))
            {
                _stateInfo.Current.Stance = Stance.Slide;

                if (_stateInfo.Previous.IsAirborne())
                {
                    currentVelocity = Vector3.ProjectOnPlane
                    (
                        vector: _stateInfo.Previous.Velocity,
                        planeNormal: motor.GroundingStatus.GroundNormal
                    );
                }

                var effectiveSlideStartSpeed = slideStartSpeed;
                if (_stateInfo.Previous.IsAirborne() && !_request.IsAirborneCrouching)
                {
                    effectiveSlideStartSpeed = 0f;
                    _request.IsAirborneCrouching = false;
                }
                
                var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                currentVelocity = motor.GetDirectionTangentToSurface
                (
                    direction: currentVelocity,
                    surfaceNormal: motor.GroundingStatus.GroundNormal
                ) * slideSpeed;
            }
        }

        private bool CanPerformSlide(Vector3 currentVelocity)
        {
            return (
                IsMoving(currentVelocity) && 
                _stateInfo.Current.IsActive(Stance.Crouch) && 
                (_stateInfo.Previous.IsActive(Stance.Stand) || _stateInfo.Previous.IsAirborne())
            );
        }

        private void NormalMovement(ref Vector3 currentVelocity, Vector3 groundedMovement, float deltaTime)
        {
            if (_stateInfo.Current.IsActive(Stance.Slide))
            {
                return;
            }
            
            var speed = (_stateInfo.Current.IsActive(Stance.Stand))
                ? (_request.IsSprinting) ? sprintSpeed : walkSpeed
                : crouchSpeed;

            var response = (_stateInfo.Current.IsActive(Stance.Stand))
                ? moveResponse
                : crouchResponse;

            var targetVelocity = groundedMovement * speed; 
            
            var moveVelocity = Vector3.Lerp
            (
                a: currentVelocity,
                b: targetVelocity,
                t: 1f - Mathf.Exp(-response * deltaTime)
            );

            _stateInfo.Current.Acceleration = (moveVelocity - currentVelocity) / deltaTime;
            currentVelocity = moveVelocity;
        }

        private void AirborneMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            if (_stateInfo.Current.IsGrounded())
            {
                return;
            }
            
            if (!IsMoving(currentVelocity))
            {
                return;
            }

            _timeSinceUngrounded += deltaTime;
            
            var planarMovement = Vector3.ProjectOnPlane
            (
                vector: _request.Move,
                planeNormal: motor.CharacterUp
            ).normalized * _request.Move.magnitude;

            var currentPlanarVelocity = Vector3.ProjectOnPlane
            (
                vector: currentVelocity ,
                planeNormal: motor.CharacterUp
            );

            var movementForce = planarMovement * (airAcceleration * deltaTime);

            ApplyNormalMovementForce(ref movementForce, currentPlanarVelocity);
            ApplyConstrainedMovementForce(ref movementForce, currentPlanarVelocity);
            PreventClimbingSteepSlopes(ref movementForce, currentVelocity);
               
            currentVelocity += movementForce;

        }

        private void ApplyNormalMovementForce(ref Vector3 movementForce, Vector3 currentPlanarVelocity)
        {
            if (currentPlanarVelocity.magnitude >= airSpeed)
            {
                return;
            }

            var targetPlanarVelocity = currentPlanarVelocity + movementForce;
            targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
            movementForce = targetPlanarVelocity - currentPlanarVelocity;
        }

        private void ApplyConstrainedMovementForce(ref Vector3 movementForce, Vector3 currentPlanarVelocity)
        {
            if (Vector3.Dot(currentPlanarVelocity, movementForce) <= 0f)
            {
                return;
            }
            
            var constrainedMovementForce = Vector3.ProjectOnPlane
            (
                vector: movementForce,
                planeNormal: currentPlanarVelocity.normalized
            );

            movementForce = constrainedMovementForce;
        }

        private void PreventClimbingSteepSlopes(ref Vector3 movementForce, Vector3 currentVelocity)
        {
            if (!motor.GroundingStatus.FoundAnyGround)
            {
                return;
            }

            if (Vector3.Dot(movementForce, currentVelocity + movementForce) <= 0f)
            {
                return;
            }

            var obstructionNormal = Vector3.Cross
            (
                motor.CharacterUp,
                Vector3.Cross
                (
                    motor.CharacterUp,
                    motor.GroundingStatus.GroundNormal
                )
            ).normalized;
                    
            movementForce = Vector3.ProjectOnPlane(movementForce, obstructionNormal);
        }

        private void Gravity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (_stateInfo.Current.IsGrounded())
            {
                return;
            }
            
            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (_request.IsJumpingSustained && verticalSpeed > 0f) 
            {
                effectiveGravity *= jumpSustainGravityMultiplier;
            }
                
            currentVelocity += motor.CharacterUp * (effectiveGravity * deltaTime);
        }

        private void Jump(ref Vector3 currentVelocity, float deltaTime)
        {
            if (!_request.IsJumping)
            {
                return;
            }

            var canJumpWithCoyoteTime = (_timeSinceUngrounded < coyoteTime && !_ungroundedDueToJump);
            
            if (_stateInfo.Current.IsAirborne() && !canJumpWithCoyoteTime)
            {
                _timeSinceJumpRequested += deltaTime;
                _request.IsJumping = (_timeSinceJumpRequested < coyoteTime);
                
                return;
            }
            
            _request.IsJumping = false;
            _request.IsCrouching = false;
            _request.IsAirborneCrouching = false;
            
            motor.ForceUnground(time: 0f);
            _ungroundedDueToJump = true;

            var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);

            currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
        }
        
        private void Crouch()
        {
            if (!_request.IsCrouching || _stateInfo.Current.IsInactive(Stance.Stand))
            {
                return;
            }
            
            _stateInfo.Current.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
            );
        }

        private void Uncrouch()
        {
            if (_request.IsCrouching || _stateInfo.Current.IsInactive(Stance.Crouch))
            {
                return;
            }

            if (CanUncrouch())
            {
                _stateInfo.Current.Stance = Stance.Stand;
            }
        }
        
        private bool CanUncrouch()
        {
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: standHeight * 0.5f
            );

            if (GetCharacterOverlapResults() > 0f)
            {
                _request.IsCrouching = true;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * 0.5f
                );

                return false;
            }

            return true;
        }

        private bool IsMoving(Vector3 currentVelocity) => currentVelocity.sqrMagnitude > 0f;
        
        #region Unused Kinematic Character Motor Functions
        public bool IsColliderValidForCollisions(Collider coll) => true;
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {}
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {}
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) {}
        public void OnDiscreteCollisionDetected(Collider hitCollider) {}
        #endregion
    }
}
