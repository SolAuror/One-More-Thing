using UnityEngine;

namespace Sol.Grab
{

    /// Attach to any object that should be grabbable.
    /// Requires a Collider for raycasting and a Rigidbody for collision-aware movement.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public class GrabbableComponent : MonoBehaviour
    {
        [Tooltip("Hold distance from the camera when grabbed")]
        [Range(0.5f, 20f)]
        public float holdDistance = 1.6f;

        [Tooltip("How quickly the object follows the target position (higher = snappier)")]
        [Range(1f, 50f)]
        public float followSpeed = 15f;

        [Header("Throw")]
        [Tooltip("Overall throw power multiplier for this object.")]
        [Min(0f)] public float throwPowerMultiplier = 1f;
        [Tooltip("How strongly this object benefits from close-range throw bonus.")]
        [Min(0f)] public float closeThrowBonusMultiplier = 1f;
        [Tooltip("For authored clutter: remain still until first grabbed, then use normal gravity on release.")]
        public bool wakeOnFirstGrab;

        private Rigidbody _rb;
        private bool _hadGravity;
        private bool _wasKinematic;
        private bool _isGrabbed;
        private float _linearDamping;
        private float _angularDamping;
        private RigidbodyInterpolation _interpolation;
        private CollisionDetectionMode _collisionDetection;
        public Rigidbody Body => _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Called when the object is picked up.
        /// </summary>
        public void OnGrab()
        {
            if (_isGrabbed) return;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            RefreshStoredPhysicsState();
            if (wakeOnFirstGrab) { _wasKinematic = false; _hadGravity = true; wakeOnFirstGrab = false; }
            _isGrabbed = true;

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = false;
                _rb.linearDamping = 10f;
                _rb.angularDamping = 10f;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        /// <summary>
        /// Called when the object is released.
        /// </summary>
        public void OnRelease()
        {
            if (!_isGrabbed) return;
            _isGrabbed = false;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.ClampMagnitude(_rb.linearVelocity, 2f);
                _rb.angularVelocity = Vector3.zero;
                _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _rb.isKinematic = _wasKinematic;
                _rb.useGravity = _hadGravity;
                _rb.linearDamping = _linearDamping;
                _rb.angularDamping = _angularDamping;
                _rb.interpolation = _interpolation;
                _rb.collisionDetectionMode = _collisionDetection;
            }
        }

        public void RefreshStoredPhysicsState()
        {
            if (_rb == null)
                return;

            _hadGravity = _rb.useGravity;
            _wasKinematic = _rb.isKinematic;
            _linearDamping = _rb.linearDamping;
            _angularDamping = _rb.angularDamping;
            _interpolation = _rb.interpolation;
            _collisionDetection = _rb.collisionDetectionMode;
        }

        /// <summary>
        /// Move toward the target position. Called each FixedUpdate by PhysicsGrabber.
        /// </summary>
        public void MoveToward(Vector3 targetPosition)
        {
            if (!_isGrabbed) return;
            if (_rb != null && !_rb.isKinematic)
            {
                Vector3 direction = targetPosition - _rb.position;
                _rb.linearVelocity = Vector3.ClampMagnitude(direction * followSpeed, 8f);
            }
        }

        public bool IsGrabbed => _isGrabbed;
        private void OnDisable() => OnRelease();
    }
}
