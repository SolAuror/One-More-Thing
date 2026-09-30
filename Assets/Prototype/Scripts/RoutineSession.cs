using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMoreThing
{
    [DefaultExecutionOrder(-100)]
    public sealed class RoutineSession : MonoBehaviour
    {
        public RoutineDefinition definition;
        public RoutineState State { get; private set; }
        public string Feedback { get; private set; }
        public float TimeCostFlash { get; private set; }
        public DeviceMenu devices;
        public bool IsRunning => State != null && State.Phase == RunPhase.Playing;
        public bool CanControl => IsRunning && (devices == null || !devices.IsOpen);
        public bool CanLook => State != null && (State.Phase == RunPhase.Ready || CanControl);
        public bool HasPhone => State?.Find("phone")?.Completed == true;
        private float phonePickedUpAt = -1f;
        private bool[] firedCues;
        [Min(1f)] public float notificationSeconds = 6f;
        private readonly Queue<string> notifications = new();
        private float feedbackRemaining;
        private FirstPersonController player;
        private PlayerInteractor interactor;
        public TaskInteractable PlantTarget { get; private set; }
        public string PlantRequest { get; private set; } = "The plants are all right for today.";
        public string HouseEmail { get; private set; }

        public string MessageBody(PhoneMessage message) => message.taskId == "message"
            ? PlantRequest + "\nThanks!" : message.body;

        public void Tick(float delta, string workingTaskId = null, float clockRate = 1f, string clockSource = "", bool isInteracting = false)
            => State.Tick(delta, workingTaskId, clockRate, clockSource, player != null && player.MovedThisFrame,
                isInteracting || (devices != null && devices.IsOpen) || (interactor != null && interactor.IsHandlingObject));

        private void Awake()
        {
            if (definition == null) { Debug.LogError("Assign a morning routine to the session.", this); enabled = false; return; }
            State = new RoutineState(definition);
            var plants = FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None)
                // This session wakes before the interactables; isActiveAndEnabled can still be false here.
                .Where(t => t.session == this && t.taskId == "plant" && t.enabled && t.gameObject.activeInHierarchy).ToArray();
            if (plants.Length > 0)
            {
                PlantTarget = plants[UnityEngine.Random.Range(0, plants.Length)];
                var requests = (PlantTarget.plantRequests ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                PlantRequest = requests.Length > 0 ? requests[UnityEngine.Random.Range(0, requests.Length)]
                    : "A little water for the plant " + PlantTarget.locationDescription + " today?";
            }
            var hints = new List<string>();
            var chores = FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Where(t => t.session == this).Select(t => t.taskId).ToHashSet();
            if (chores.Contains("cup")) hints.Add("I'll make dinner tonight. A clear sink would be lovely to come home to.\n\nThe mugs do seem to enjoy visiting other rooms.");
            if (chores.Contains("laundry")) hints.Add("Nearly out of clean towels again. I think the missing ones are having a reunion in the two bathrooms.\n\nSee you later!");
            if (chores.Contains("books")) hints.Add("Borrow anything from the living-room shelf. Just leave the books standing when you're done.\n\nThey lean on each other enough already.");
            HouseEmail = hints.Count > 0 ? hints[UnityEngine.Random.Range(0, hints.Count)] : "Hope your morning is a gentle one. See you later.";
            player = FindFirstObjectByType<FirstPersonController>();
            interactor = FindFirstObjectByType<PlayerInteractor>();
            firedCues = new bool[definition.cues?.Length ?? 0];
            State.Changed += SyncCursor;
            State.TaskCompleted += OnTaskCompleted;
            SyncCursor();
        }

        private void Update()
        {
            if (IsRunning && !(CanControl && UnityEngine.InputSystem.Keyboard.current?.tabKey.isPressed == true))
            {
                feedbackRemaining -= Time.unscaledDeltaTime;
                if (feedbackRemaining <= 0f)
                {
                    Feedback = notifications.Count > 0 ? notifications.Dequeue() : "";
                    feedbackRemaining = string.IsNullOrEmpty(Feedback) ? 0f : notificationSeconds;
                }
            }
            TimeCostFlash = Mathf.Max(0f, TimeCostFlash - Time.unscaledDeltaTime * 2f);
            if (!IsRunning) return;
            for (int i = 0; i < firedCues.Length; i++)
            {
                if (firedCues[i] || State.Elapsed < definition.cues[i].afterSeconds) continue;
                firedCues[i] = true;
                State.Discover(definition.cues[i].taskId);
            }
            if (!HasPhone) return;
            if (phonePickedUpAt < 0f) phonePickedUpAt = State.RealElapsed;
            foreach (var message in definition.phoneMessages ?? Array.Empty<PhoneMessage>())
            {
                if (State.Find(message.taskId).Available || State.RealElapsed - phonePickedUpAt < message.afterPickupSeconds) continue;
                State.ReleaseTask(message.taskId);
                Notify("New message from " + message.sender + "   [P] Phone");
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && IsRunning) State.SetPaused(true);
        }

        private void OnDestroy()
        {
            if (State != null) { State.Changed -= SyncCursor; State.TaskCompleted -= OnTaskCompleted; }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SyncCursor()
        {
            Cursor.lockState = CanLook ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !CanLook;
        }

        public void Notify(string message)
        {
            if (Feedback == message || notifications.Contains(message)) return;
            if (string.IsNullOrEmpty(Feedback)) { Feedback = message; feedbackRemaining = notificationSeconds; }
            else notifications.Enqueue(message);
        }
        private void OnTaskCompleted(RoutineState.TaskState task, float cost)
        {
            if (task.Definition.id == "phone") phonePickedUpAt = State.RealElapsed;
            string result = task.Definition.id switch
            {
                "phone" => "Phone in pocket.", "keys" => "Keys collected.", "bag" => "Bag packed.",
                "laundry" => "Washer running.", "cup" => "Dishes clean.", "plant" => "Plant watered.", "books" => "Books straightened.",
                "email" => "Email read.", _ => task.Definition.title + " - done."
            };
            if (task.Definition.id.StartsWith("laundry_")) result = "Laundry gathered: " + State.RequirementStatus("laundry") + (State.CanWork("laundry") ? ". Ready for the washer." : ". One bathroom left.");
            if (task.Definition.id.StartsWith("dish_")) result = "Dishes gathered: " + State.RequirementStatus("cup") + (State.CanWork("cup") ? ". Ready to wash." : ".");
            if (task.Definition.id.StartsWith("message")) result = "Reply sent.";
            Notify(result + (cost > 0f ? "  " + cost.ToString("0.#") + " seconds passed." : ""));
            TimeCostFlash = cost > 0f ? 1f : 0f;
        }
        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().path);

        public string ExportRun()
        {
            var csv = new StringBuilder("clock_seconds_spent,real_seconds,event,task_or_screen_id,value\n");
            foreach (var item in State.Events)
                csv.Append(item.Elapsed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.RealElapsed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(item.Kind)).Append(',').Append(Escape(item.TaskId)).Append(',')
                    .Append(item.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
            string path = Path.Combine(Application.persistentDataPath, "run-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            File.WriteAllText(path, csv.ToString());
            Debug.Log("Playtest log saved: " + path);
            return path;
        }

        private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
