using System.Collections.Generic;
using UnityEngine;

namespace Sol.Grab
{
    public sealed class PhysicsGrabber : MonoBehaviour
    {
        public Camera viewCamera;
        public Collider playerCollider;
        public LayerMask obstructionMask = ~0;

        [Header("Throw")]
        public bool isThrowingEnabled = true;
        [Min(0f)] public float throwSpeed = 8f;
        [Min(0f)] public float throwUpwardBias = 0.08f;
        [Tooltip("Base speed limit; close-range bonuses and object power can raise this limit.")]
        [Min(0f)] public float maxThrowSpeed = 10f;
        [Min(0f)] public float closeThrowBonusSpeed = 6f;
        [Min(0f)] public float fullPowerThrowDistance = 1f;
        [Min(0f)] public float basePowerThrowDistance = 3f;
        private readonly List<(Collider a, Collider b)> ignoredPairs = new();
        private readonly RaycastHit[] hits = new RaycastHit[32];
        public GrabbableComponent Held { get; private set; }
        private float distance;

        public void Grab(GrabbableComponent target)
        {
            if (!isActiveAndEnabled || target == null || !target.isActiveAndEnabled || target.IsGrabbed || viewCamera == null) return;
            Release();
            Held = target;
            distance = Mathf.Clamp(target.holdDistance, 0.7f, 2.5f);
            target.OnGrab();
            if (playerCollider == null) return;
            foreach (var item in target.GetComponentsInChildren<Collider>())
            {
                if (Physics.GetIgnoreCollision(item, playerCollider)) continue;
                Physics.IgnoreCollision(item, playerCollider, true);
                ignoredPairs.Add((item, playerCollider));
            }
        }

        public void AdjustDistance(float delta) => distance = Mathf.Clamp(distance + delta, 0.7f, 2.5f);

        public void Throw()
        {
            if (!isActiveAndEnabled || !isThrowingEnabled || Held == null) return;
            if (!Held.isActiveAndEnabled || !Held.IsGrabbed || viewCamera == null) { Release(); return; }

            var target = Held;
            var body = target.Body;
            Vector3 direction = viewCamera.transform.forward + Vector3.up * Mathf.Max(0f, throwUpwardBias);
            if (direction.sqrMagnitude <= 0.001f) direction = viewCamera.transform.forward;

            // Match the Arcade grabber: pulling an object closer boosts its launch speed.
            float closeFactor = basePowerThrowDistance <= fullPowerThrowDistance
                ? (distance <= fullPowerThrowDistance ? 1f : 0f)
                : Mathf.InverseLerp(basePowerThrowDistance, fullPowerThrowDistance, distance);
            float power = Mathf.Max(0f, target.throwPowerMultiplier);
            float bonus = Mathf.Max(0f, closeThrowBonusSpeed) * Mathf.Max(0f, target.closeThrowBonusMultiplier);
            float baseSpeed = Mathf.Max(0f, throwSpeed);
            float speed = (baseSpeed + closeFactor * bonus) * power;
            float limit = Mathf.Max(Mathf.Max(0f, maxThrowSpeed), baseSpeed + bonus) * power;

            // Restore damping and player collisions before applying velocity, so dropping's cap cannot trim the throw.
            Release();
            if (body != null && !body.isKinematic)
                body.linearVelocity = Vector3.ClampMagnitude(direction.normalized * speed, limit);
        }

        private void FixedUpdate()
        {
            if (Held == null || !Held.isActiveAndEnabled || !Held.IsGrabbed || viewCamera == null)
            {
                // The prop may release itself when disabled, even without any ignored collision pairs.
                Release();
                return;
            }
            if (Vector3.Distance(Held.transform.position, viewCamera.transform.position) > 4f) { Release(); return; }
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            float permitted = distance;
            int count = Physics.SphereCastNonAlloc(ray, 0.18f, hits, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == playerCollider || hit.collider.transform.IsChildOf(Held.transform)) continue;
                permitted = Mathf.Min(permitted, Mathf.Max(0.35f, hit.distance - 0.1f));
            }
            Held.MoveToward(ray.GetPoint(permitted));
        }

        public void Release()
        {
            if (Held != null) Held.OnRelease();
            Held = null;
            foreach (var pair in ignoredPairs)
                if (pair.a != null && pair.b != null) Physics.IgnoreCollision(pair.a, pair.b, false);
            ignoredPairs.Clear();
        }

        private void OnDisable() => Release();
    }
}
