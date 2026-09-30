using UnityEngine;
using UnityEngine.Events;
using Sol.Grab;
using System.Collections.Generic;

namespace OneMoreThing
{
    [DisallowMultipleComponent]
    public sealed class TaskInteractable : MonoBehaviour
    {
        public RoutineSession session;
        public string taskId;
        [Tooltip("Plant location used by this run's email and text message, e.g. beside the bookshelf downstairs.")]
        public string locationDescription = "beside the bookshelf downstairs";
        [Tooltip("Accurate alternatives for this plant's email/text request. One is chosen for the whole morning.")]
        [TextArea(2, 4)] public string[] plantRequests = System.Array.Empty<string>();
        [Tooltip("Optional: remove a collected prop, but keep furniture active.")]
        public GameObject hideOnCompletion;
        public UnityEvent onCompleted;
        private readonly List<(GrabbableComponent book, Vector3 position, Quaternion rotation)> shelfBooks = new();
        public RoutineState.TaskState Task => session != null ? session.State?.Find(taskId) : null;
        public bool CanInteract => isActiveAndEnabled && Task != null
            && (taskId != "plant" || session.PlantTarget == this);

        private void Awake()
        {
            if (taskId != "books") return;
            var shelf = GetComponent<Renderer>();
            if (shelf == null) return;
            var bounds = shelf.bounds;
            bounds.Expand(0.5f);
            foreach (var book in FindObjectsByType<GrabbableComponent>(FindObjectsSortMode.None))
            {
                var renderer = book.GetComponent<Renderer>();
                if (!book.name.StartsWith("Book_", System.StringComparison.Ordinal)
                    || !bounds.Contains(renderer != null ? renderer.bounds.center : book.transform.position)) continue;
                shelfBooks.Add((book, book.transform.position, book.transform.rotation));
            }
        }

        private void Start()
        {
            if (session?.State != null) session.State.TaskCompleted += ApplyCompletion;
        }

        private void ApplyCompletion(RoutineState.TaskState task, float cost)
        {
            if (task.Definition.id != taskId || !CanInteract) return;
            if (taskId == "books") RestoreBooks();
            onCompleted?.Invoke();
            if (hideOnCompletion != null) hideOnCompletion.SetActive(false);
        }

        private void RestoreBooks()
        {
            var grabbers = FindObjectsByType<PhysicsGrabber>(FindObjectsSortMode.None);
            foreach (var pose in shelfBooks)
            {
                if (pose.book == null) continue;
                foreach (var grabber in grabbers)
                    if (grabber.Held == pose.book) grabber.Release();
                pose.book.OnRelease();
                var body = pose.book.Body;
                if (body != null)
                {
                    if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                    body.position = pose.position;
                    body.rotation = pose.rotation;
                    body.Sleep();
                }
                pose.book.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
            Physics.SyncTransforms();
        }

        private void OnDestroy()
        {
            if (session?.State != null) session.State.TaskCompleted -= ApplyCompletion;
        }
    }
}
