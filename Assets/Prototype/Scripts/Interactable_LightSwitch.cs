using UnityEngine;

namespace OneMoreThing
{
    [DisallowMultipleComponent]
    public sealed class LightSwitchInteractable : MonoBehaviour
    {
        public string roomName = "Room";
        public Light[] lights;
        public bool startsOn = true;
        public bool IsOn { get; private set; }
        private void Awake() => SetOn(startsOn);
        public void Toggle() => SetOn(!IsOn);
        public void SetOn(bool value)
        {
            IsOn = value;
            foreach (var light in lights ?? System.Array.Empty<Light>())
                if (light != null) light.enabled = value;
        }
    }
}
