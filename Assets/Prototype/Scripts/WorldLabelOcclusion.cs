using UnityEngine;

namespace OneMoreThing
{
    // Legacy TextMesh materials draw through walls. Keep small prop/door labels local to their room.
    [RequireComponent(typeof(TextMesh))]
    public sealed class WorldLabelOcclusion : MonoBehaviour
    {
        public Camera viewCamera;
        private Renderer label;
        private void Awake() => label = GetComponent<Renderer>();
        private void LateUpdate()
        {
            if (viewCamera == null || label == null) return;
            var delta = label.bounds.center - viewCamera.transform.position;
            bool visible = delta.magnitude <= 12f && Vector3.Dot(viewCamera.transform.forward, delta) > 0;
            if (visible && Physics.Raycast(viewCamera.transform.position, delta.normalized, out var hit,
                Mathf.Max(0, delta.magnitude - 0.03f), ~(1 << 2), QueryTriggerInteraction.Ignore))
                visible = transform.IsChildOf(hit.transform);
            label.enabled = visible;
        }
    }
}
