using Sol.Grab;
using Sol.Outline;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OneMoreThing // This is the player interaction system, which handles looking at objects, interacting with them, and managing the player's held object.
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public RoutineSession session;
        public Camera viewCamera;
        public PhysicsGrabber grabber;
        [Min(0.1f)] public float reach = 2.6f;
        public LayerMask interactionMask = ~0;
        [Min(0.1f)] public float screenLookDistance = 8f;
        public string Prompt { get; private set; } = "";
        public float Progress { get; private set; }
        public bool HasTask { get; private set; }
        private OutlineComponent highlighted;
        private TaskInteractable watched;
        private float watchTime;
        private DoorInteractable heldDoor;
        public bool IsHandlingObject => heldDoor != null || (grabber != null && grabber.Held != null);

        private void LateUpdate()
        {
            if (session == null || session.State == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (session.State.Phase == RunPhase.Ready)
            {
                Prompt = "Phone alarm  ·  E silence";
                Progress = 0f;
                HasTask = false;
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame) SilenceAlarm();
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (session.IsRunning && session.devices != null && session.devices.IsOpen) session.devices.Close();
                else if (session.IsRunning) session.State.SetPaused(true);
                else if (session.State.Phase == RunPhase.Paused) session.State.SetPaused(false);
            }
            if (session.IsRunning && keyboard != null && keyboard.pKey.wasPressedThisFrame && session.devices != null)
            {
                if (session.devices.IsOpen && session.devices.IsPhone) session.devices.Close();
                else session.devices.OpenPhone();
            }
            Prompt = "";
            Progress = 0f;
            HasTask = false;
            string working = null;
            bool usedObject = IsHandlingObject;
            if (!session.CanControl)
            {
                SetHighlight(null);
                watched = null;
                if (grabber != null) grabber.Release();
                heldDoor = null;
                if (session.IsRunning && session.devices != null && session.devices.IsOpen)
                    session.Tick(Time.deltaTime, session.devices.WorkingTaskId, session.devices.clockMultiplier, session.devices.ClockSource);
                return;
            }
            // Handle button release even while the morning note hides the interaction prompts.
            if (grabber != null && grabber.Held != null && (mouse == null || !mouse.leftButton.isPressed))
                grabber.Release();
            if (mouse == null || !mouse.leftButton.isPressed) heldDoor = null;
            if (keyboard != null && keyboard.tabKey.isPressed)
            {
                SetHighlight(null); watched = null;
                heldDoor = null;
                session.Tick(Time.deltaTime);
                return;
            }
            if (heldDoor != null)
            {
                SetHighlight(null);
                watched = null;
                Prompt = "Door";
                if (!heldDoor.Drag(viewCamera, reach)) heldDoor = null;
            }
            else if (grabber != null && grabber.Held != null)
            {
                SetHighlight(null);
                watched = null;
                var heldConsumable = grabber.Held.GetComponentInParent<ConsumableInteractable>();
                Prompt = ItemNames.For(grabber.Held);
                if (heldConsumable != null && heldConsumable.CanConsume)
                    Prompt = ItemNames.Use(Prompt, heldConsumable.kind == ConsumableInteractable.ConsumptionKind.Coffee ? "Drink" : "Eat");
                if (heldConsumable == null || !heldConsumable.CanConsume)
                {
                    var heldTask = grabber.Held.GetComponentInParent<TaskInteractable>();
                    if (heldTask != null && heldTask.CanInteract) working = ReadTaskInput(heldTask, keyboard);
                }
                if (heldConsumable != null && heldConsumable.CanConsume && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                    usedObject |= heldConsumable.TryConsume(grabber);
                else if (mouse != null)
                {
                    grabber.AdjustDistance(mouse.scroll.ReadValue().y * 0.001f);
                    if (mouse.rightButton.wasPressedThisFrame) grabber.Throw();
                }
            }
            else if (viewCamera != null && Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward,
                out var hit, reach, interactionMask, QueryTriggerInteraction.Ignore))
            {
                var task = hit.collider.GetComponentInParent<TaskInteractable>();
                var exit = hit.collider.GetComponentInParent<ExitInteractable>();
                var grab = hit.collider.GetComponentInParent<GrabInteractable>();
                var door = hit.collider.GetComponentInParent<DoorInteractable>();
                var device = hit.collider.GetComponentInParent<DeviceInteractable>();
                var lightSwitch = hit.collider.GetComponentInParent<LightSwitchInteractable>();
                var consumable = hit.collider.GetComponentInParent<ConsumableInteractable>();
                bool valid = (task != null && task.CanInteract)
                    || (exit != null && exit.isActiveAndEnabled) || (grab != null && grab.CanGrab)
                    || (door != null && door.isActiveAndEnabled) || (device != null && device.isActiveAndEnabled)
                    || (lightSwitch != null && lightSwitch.isActiveAndEnabled) || (consumable != null && consumable.CanConsume);
                SetHighlight(valid ? hit.collider.GetComponentInParent<OutlineComponent>() : null);
                Component item = consumable != null ? consumable : task != null ? task : door != null ? door
                    : device != null ? device : lightSwitch != null ? lightSwitch : grab != null ? grab : (Component)exit;
                if (item != null) Prompt = ItemNames.For(item);
                if (task != watched) { watched = task; watchTime = 0f; }
                if ((consumable == null || !consumable.CanConsume) && task != null && task.CanInteract)
                {
                    watchTime += Time.deltaTime;
                    if (watchTime >= 0.4f) session.State.Discover(task.taskId);
                    working = ReadTaskInput(task, keyboard);
                }
                if (consumable != null && consumable.CanConsume)
                {
                    watched = null;
                    if (consumable.CanConsume) Prompt = ItemNames.Use(ItemNames.For(consumable),
                        consumable.kind == ConsumableInteractable.ConsumptionKind.Coffee ? "Drink" : "Eat");
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame) usedObject |= consumable.TryConsume(grabber);
                }
                else if (lightSwitch != null && lightSwitch.isActiveAndEnabled)
                {
                    Prompt = ItemNames.Use(lightSwitch.roomName + " lights", lightSwitch.IsOn ? "Switch off" : "Switch on");
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame) { lightSwitch.Toggle(); usedObject = true; }
                }
                else if (device != null && device.isActiveAndEnabled)
                {
                    Prompt = ItemNames.Use(device.displayName, "Use");
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame) device.Open();
                }
                else if (exit != null && exit.isActiveAndEnabled)
                {
                    Prompt = ItemNames.Use("Front door", "Leave");
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame) exit.TryLeave();
                }
                else if (door != null && door.isActiveAndEnabled)
                {
                    Prompt = "Door";
                    if (mouse != null && mouse.leftButton.wasPressedThisFrame && mouse.leftButton.isPressed)
                    { heldDoor = door; door.BeginGrab(viewCamera, hit.point); working = null; }
                }
                if (heldDoor == null && grab != null && grab.CanGrab && grabber != null)
                {
                    if (grab.isActiveAndEnabled && mouse != null && mouse.leftButton.wasPressedThisFrame && mouse.leftButton.isPressed)
                    { grabber.Grab(grab.Grabbable); working = null; }
                }
            }
            else
            {
                watched = null;
                SetHighlight(null);
            }
            // Independent of hand interactions: a carried object can occlude a screen,
            // but holding something does not switch gaze detection off.
            var screen = FindWatchedScreen(viewCamera, screenLookDistance, interactionMask);
            bool browsing = session.devices != null && session.devices.IsOpen;
            session.Tick(Time.deltaTime, browsing ? session.devices.WorkingTaskId : working,
                browsing ? session.devices.clockMultiplier : screen != null ? screen.clockMultiplier : 1f,
                browsing ? session.devices.ClockSource : screen != null ? screen.screenId : "",
                usedObject || IsHandlingObject || screen != null);
        }

        private string ReadTaskInput(TaskInteractable task, Keyboard keyboard)
        {
            bool repeat = task.Task.Completed && task.Task.Definition.repeatable;
            if (repeat && keyboard != null && keyboard.eKey.wasPressedThisFrame) session.State.RepeatTask(task.taskId);
            HasTask = session.State.CanWork(task.taskId);
            Progress = repeat ? 0f : task.Task.Progress / task.Task.Definition.seconds;
            if (HasTask || repeat) Prompt = ItemNames.Use(ItemNames.For(task), ItemNames.TaskAction(task.taskId), true);
            return HasTask && keyboard != null && keyboard.eKey.isPressed ? task.taskId : null;
        }

        public void SilenceAlarm()
        {
            if (session?.State?.Phase != RunPhase.Ready) return;
            session.State.Start();
            session.Notify("Out by 08:00. Keys, phone, bag. Tab for my note; E to use things.");
        }

        public static AttentionScreen FindWatchedScreen(Camera camera, float distance, LayerMask mask)
        {
            if (camera == null || !Physics.Raycast(camera.transform.position, camera.transform.forward,
                out var hit, distance, mask, QueryTriggerInteraction.Ignore)) return null;
            var screen = hit.collider.GetComponent<AttentionScreen>();
            return screen != null && screen.isActiveAndEnabled && screen.Faces(camera.transform.position) ? screen : null;
        }

        private void SetHighlight(OutlineComponent next)
        {
            if (highlighted == next) return;
            if (highlighted != null && !highlighted.alwaysVisible) highlighted.HideOutline();
            highlighted = next;
            if (highlighted != null) highlighted.ShowOutline();
        }

        private void OnDisable() { SetHighlight(null); heldDoor = null; if (grabber != null) grabber.Release(); }
    }
}
