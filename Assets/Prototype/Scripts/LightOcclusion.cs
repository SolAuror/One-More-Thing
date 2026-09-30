using UnityEngine;

namespace OneMoreThing //this script is used to limit the number of lights that can cast shadows in a scene, which can help improve performance in Unity. It allows you to specify a budget for how many lights can cast shadows and will automatically enable or disable shadows on lights based on their distance from an observer (like the main camera).
{
    [DisallowMultipleComponent]
    public sealed class LightLoD : MonoBehaviour
    {
        public Light[] localLights = System.Array.Empty<Light>();
        public Transform observer;
        [Range(0, 2)] public int shadowBudget = 2;
        [Min(1f)] public float shadowDistance = 12f;
        [Min(0.05f)] public float refreshInterval = 0.25f;
        private float nextRefresh;

        private void OnEnable()
        {
            if (observer == null && Camera.main != null) observer = Camera.main.transform;
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        public void Refresh()
        {
            if (localLights == null || observer == null) return;
            Light first = null, second = null;
            float firstScore = float.PositiveInfinity, secondScore = float.PositiveInfinity;
            foreach (var light in localLights)
            {
                // Point shadows need six maps. Accent/point lights keep illuminating without shadows.
                if (light == null || light.type != LightType.Spot || !light.isActiveAndEnabled || light.intensity <= 0f) continue;
                float distance = (light.transform.position - observer.position).sqrMagnitude;
                if (distance > shadowDistance * shadowDistance) continue;
                // Prefer existing casters to avoid swapping at the boundary between two rooms.
                float score = distance * (light.shadows != LightShadows.None ? 0.75f : 1f);
                if (score < firstScore) { second = first; secondScore = firstScore; first = light; firstScore = score; }
                else if (score < secondScore) { second = light; secondScore = score; }
            }
            foreach (var light in localLights)
            {
                if (light == null || light.type == LightType.Directional) continue;
                var shadows = (shadowBudget > 0 && light == first) || (shadowBudget > 1 && light == second)
                    ? LightShadows.Hard : LightShadows.None;
                if (light.shadows != shadows) light.shadows = shadows;
            }
        }
    }
}
