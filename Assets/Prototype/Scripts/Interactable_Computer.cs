using UnityEngine;

namespace OneMoreThing
{
    [DisallowMultipleComponent]
    public sealed class DeviceInteractable : MonoBehaviour
    {
        public RoutineSession session;
        public string displayName = "Bedroom computer";
        public void Open() { if (session != null && session.CanControl) session.devices?.OpenComputer(this); }
    }
}
