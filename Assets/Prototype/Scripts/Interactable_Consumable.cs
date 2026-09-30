using Sol.Grab;
using UnityEngine;
using UnityEngine.Events;

namespace OneMoreThing
{
    [DisallowMultipleComponent]
    public sealed class ConsumableInteractable : MonoBehaviour
    {
        public enum ConsumptionKind { Food, Coffee }
        public ConsumptionKind kind;
        [Tooltip("Coffee keeps its cup; optionally replace its mesh with an empty version.")]
        public Mesh emptyCupMesh;
        public AudioClip consumeSound;
        public UnityEvent onConsumed;
        public bool IsConsumed { get; private set; }
        public bool CanConsume => isActiveAndEnabled && !IsConsumed;

        public bool TryConsume(PhysicsGrabber grabber = null)
        {
            if (!CanConsume) return false;
            IsConsumed = true;
            if (kind == ConsumptionKind.Food && grabber != null && grabber.Held != null
                && grabber.Held.transform.IsChildOf(transform)) grabber.Release();
            if (consumeSound != null) AudioSource.PlayClipAtPoint(consumeSound, transform.position, 0.45f);
            if (kind == ConsumptionKind.Coffee && emptyCupMesh != null && TryGetComponent<MeshFilter>(out var filter))
                filter.sharedMesh = emptyCupMesh;
            if (kind == ConsumptionKind.Coffee) FindFirstObjectByType<FirstPersonController>()?.DrinkCoffee();
            onConsumed?.Invoke();
            if (kind == ConsumptionKind.Food) gameObject.SetActive(false);
            return true;
        }
    }
}
