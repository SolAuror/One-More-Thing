using UnityEngine;

namespace OneMoreThing
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DoorInteractable : MonoBehaviour
    {
        public enum SwingDirection { Inward, Outward, Both }
        [Tooltip("Inward: 0 to +100 degrees. Outward: 0 to -100 degrees. Both: the full 200 degree arc.")]
        public SwingDirection swingDirection = SwingDirection.Inward;
        [Tooltip("Starting opening. 0 is closed, 100 is fully open. In Both mode, negative percentages open outward.")]
        [Range(-100f, 100f)] public float openPercent;
        [Min(1f)] public float swingSpeed = 180f;
        public bool IsOpen { get; private set; }
        [SerializeField, HideInInspector] private Quaternion closedRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private bool hasClosedPose;
        [SerializeField, HideInInspector] private int poseVersion;
        public bool HasConfiguredPose => poseVersion > 0;
        private bool previewPending;
        public const float HalfArc = 100f;
        public float CurrentAngle => angle;
        public Quaternion ClosedLocalRotation => closedRotation;
        private float angle;
        private Vector3 grabbedPoint;
        private float grabDistance;
        public void BeginGrab(Camera camera, Vector3 hitPoint)
        {
            if (camera == null) return;
            grabbedPoint = transform.InverseTransformPoint(hitPoint);
            grabDistance = Vector3.Distance(camera.transform.position, hitPoint);
        }

        public bool Drag(Camera camera, float reach)
        {
            if (!isActiveAndEnabled || camera == null) return false;
            Vector3 point = transform.TransformPoint(grabbedPoint);
            if (Vector3.Distance(camera.transform.position, point) > reach + 0.5f) return false;
            Vector3 target = camera.transform.position + camera.transform.forward * grabDistance;
            Vector3 from = Vector3.ProjectOnPlane(point - transform.position, transform.up);
            Vector3 to = Vector3.ProjectOnPlane(target - transform.position, transform.up);
            if (from.sqrMagnitude < 0.001f || to.sqrMagnitude < 0.001f) return true;
            float wanted = ClampAngle(angle + Vector3.SignedAngle(from, to, transform.up));
            SetAngle(Mathf.MoveTowards(angle, wanted, swingSpeed * Time.deltaTime));
            return true;
        }
        private void Awake()
        {
            if (!hasClosedPose) { closedRotation = transform.localRotation; hasClosedPose = true; }
            ApplyStartingPose();
        }
        private void OnValidate() => previewPending = true;
        private void Update()
        {
            if (!Application.IsPlaying(gameObject) && previewPending && hasClosedPose)
            { previewPending = false; ApplyStartingPose(); }
        }
        public void ConfigureClosedPose(Quaternion rotation, float startingPercent)
        {
            closedRotation = rotation; hasClosedPose = true; poseVersion = 1; openPercent = startingPercent;
            ApplyStartingPose();
        }
        public void ApplyStartingPose()
        {
            float fraction = Mathf.Clamp(openPercent / 100f, -1f, 1f);
            float start = swingDirection == SwingDirection.Both ? fraction * HalfArc
                : Mathf.Lerp(0f, swingDirection == SwingDirection.Outward ? -HalfArc : HalfArc, Mathf.Abs(fraction));
            SetAngle(start);
        }
        private void SetAngle(float value)
        {
            angle = ClampAngle(value);
            transform.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
            IsOpen = Mathf.Abs(angle) > 1f;
        }
        public float ClampAngle(float value)
        {
            if (swingDirection == SwingDirection.Both) return Mathf.Clamp(value, -HalfArc, HalfArc);
            float limit = swingDirection == SwingDirection.Outward ? -HalfArc : HalfArc;
            return Mathf.Clamp(value, Mathf.Min(0f, limit), Mathf.Max(0f, limit));
        }
        public void Toggle()
        {
            SetAngle(IsOpen ? 0f : swingDirection == SwingDirection.Outward ? -HalfArc : HalfArc);
        }
    }
}
