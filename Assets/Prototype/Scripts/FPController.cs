using UnityEngine;
using UnityEngine.InputSystem;

namespace OneMoreThing
{
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public RoutineSession session;
        public Camera viewCamera;
        [Min(0.1f)] public float walkSpeed = 2.8f;
        [Range(0.01f, 0.5f)] public float mouseSensitivity = 0.09f;
        public float gravity = -20f;
        private CharacterController controller;
        private InputAction move;
        private InputAction look;
        private InputAction hurry;
        private float restingFov;
        public float CoffeeSecondsRemaining { get; private set; }
        public float MovementSpeed => walkSpeed * (CoffeeSecondsRemaining > 0f ? 1.12f : 1f);
        private float pitch;
        private float verticalSpeed;
        private Vector3 spawn;
        public bool MovedThisFrame { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawn = transform.position;
            if (viewCamera != null)
            {
                restingFov = viewCamera.fieldOfView;
                pitch = Mathf.DeltaAngle(0f, viewCamera.transform.localEulerAngles.x);
            }
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            hurry = new InputAction("Hurry", InputActionType.Button, "<Keyboard>/leftShift");
        }

        private void OnEnable() { move.Enable(); look.Enable(); hurry.Enable(); }
        private void OnDisable() { move.Disable(); look.Disable(); hurry.Disable(); ResetHurryView(); }
        private void OnDestroy() { move.Dispose(); look.Dispose(); hurry.Dispose(); }
        public void DrinkCoffee() => CoffeeSecondsRemaining = 30f;
        public void ResetHurryView() { if (viewCamera != null && restingFov > 0f) viewCamera.fieldOfView = restingFov; }
        private void UpdateHurryView(bool moving)
        {
            if (viewCamera == null) return;
            float target = restingFov + (moving && hurry.IsPressed() ? 4f : 0f);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }

        private void Update()
        {
            MovedThisFrame = false;
            if (session != null && session.IsRunning) CoffeeSecondsRemaining = Mathf.Max(0f, CoffeeSecondsRemaining - Time.deltaTime);
            if (session == null || !session.CanLook || viewCamera == null || Cursor.lockState != CursorLockMode.Locked)
            { UpdateHurryView(false); return; }
            Vector2 aim = look.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, aim.x, 0f);
            pitch = Mathf.Clamp(pitch - aim.y, -85f, 85f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (!session.CanControl) { UpdateHurryView(false); return; }
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            Vector3 beforeMove = transform.position;
            controller.Move(((transform.right * input.x + transform.forward * input.y) * MovementSpeed
                + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 displacement = transform.position - beforeMove;
            displacement.y = 0f;
            MovedThisFrame = displacement.sqrMagnitude > 0.00000001f;
            UpdateHurryView(input.sqrMagnitude > .01f && MovedThisFrame);
            if (transform.position.y < spawn.y - 12f)
            {
                controller.enabled = false;
                transform.position = spawn;
                controller.enabled = true;
                verticalSpeed = 0f;
                session.Notify("Returned to the starting position.");
            }
        }
    }
}
