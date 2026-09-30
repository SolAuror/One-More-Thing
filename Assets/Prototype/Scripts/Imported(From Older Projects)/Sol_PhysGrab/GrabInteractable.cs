using UnityEngine;

namespace Sol.Grab
{
    /// <summary>
    /// Self-contained adapter for the prototype interaction system.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(GrabbableComponent))]
    public sealed class GrabInteractable : MonoBehaviour
    {
        public string interactionPrompt = "Pick up";
        public GrabbableComponent Grabbable => GetComponent<GrabbableComponent>();
        public bool CanGrab
        {
            get
            {
                var target = Grabbable;
                return isActiveAndEnabled && target != null && target.isActiveAndEnabled && !target.IsGrabbed;
            }
        }
    }
}
