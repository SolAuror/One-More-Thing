using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sol.Grab;
using Object = UnityEngine.Object;

namespace OneMoreThing.Editor
{
    [InitializeOnLoad]
    public static class TimerDishChecks
    {
        const string BatchKey = "OneMoreThing.TimerDishChecks";
        static double runAt;
        static TimerDishChecks()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(BatchKey, false)) return;
                runAt = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += RunWhenReady;
            };
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(HouseFeelTools.ScenePath);
            TimerDishTools.Apply(); Check();
            SessionState.SetBool(BatchKey, true);
            EditorApplication.EnterPlaymode();
        }
        static void RunWhenReady()
        {
            if (EditorApplication.timeSinceStartup < runAt) return;
            EditorApplication.update -= RunWhenReady;
            SessionState.SetBool(BatchKey, false);
            try { CheckPlay(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        [MenuItem("One More Thing/Check timer and dish wiring")]
        public static void Check()
        {
            var definition = ScriptableObject.CreateInstance<RoutineDefinition>();
            try
            {
                definition.duration = 100;
                definition.tasks = new[] { new RoutineTask { id = "test", required = true, seconds = 10, completionTimeCost = 2 } };
                var state = new RoutineState(definition); state.Start();
                state.Tick(10, isMoving: false); Near(state.Remaining, 100, "Ordinary looking should be free.");
                state.Tick(3, clockRate: 2, clockSource: "computer", isMoving: false); Near(state.Remaining, 94, "Stationary device use resumes timer.");
                state.Tick(3, clockRate: 2, clockSource: "screen", isMoving: false); Near(state.Remaining, 88, "Watching screen cannot idle-pause.");
                state.Tick(2, clockSource: "screen", isMoving: false); Near(state.Remaining, 86, "Normal-rate device still counts.");
                state.Tick(10, isMoving: false); Near(state.Remaining, 84.5f, "Idle grace expires after activity stops.");
                state.Tick(1, isMoving: false, isInteracting: true); Near(state.Remaining, 83.5f, "Grabbing resumes idle timer.");
                state.Tick(4, isMoving: false, isInteracting: true); Near(state.Remaining, 79.5f, "Holding objects keeps time running.");
                state.Tick(10, isMoving: false); float before = state.Remaining;
                state.Tick(1, "test", isMoving: false); Near(state.Remaining, before - 1, "Stationary work costs time.");
                state.SetPaused(true); before = state.Remaining;
                state.Tick(2, clockRate: 2, isMoving: true, isInteracting: true); Near(state.Remaining, before, "Explicit pause is respected.");
                state.SetPaused(false);
                state.Tick(1000, clockRate: 2, clockSource: "computer", isMoving: false);
                Require(state.Phase == RunPhase.TimedOut && state.Remaining == 0, "Device timeouts must stop at zero.");
            }
            finally { Object.DestroyImmediate(definition); }
            var session = Object.FindFirstObjectByType<RoutineSession>();
            var tasks = Object.FindObjectsByType<TaskInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var dishes = tasks.Where(t => t.taskId.StartsWith("dish_dining_")).ToArray();
            Require(dishes.Length == 6 && dishes.Select(t => t.taskId).Distinct().Count() == 6, "Six distinct dining pickups.");
            Require(!tasks.Any(t => t.taskId == "dish_dining"), "Group pickup remains.");
            foreach (var dish in dishes)
            {
                Require(dish.hideOnCompletion == dish.gameObject && dish.onCompleted.GetPersistentEventCount() == 0, "Pickup must affect only itself.");
                Require(session.definition.tasks.Single(t => t.id == "cup").prerequisites.Contains(dish.taskId), "Sink must require every dish.");
            }
            var bowl = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Single(r => r.name == "Plate_03" && TimerDishTools.OnDiningTable(r));
            Require(bowl.GetComponent<TaskInteractable>() == null, "Fruit bowl must not be a dish task.");
            foreach (var task in tasks)
            {
                Require(task.hideOnCompletion != bowl.gameObject, "Fruit bowl is hidden by another task.");
                for (int i = 0; i < task.onCompleted.GetPersistentEventCount(); i++)
                    Require(task.onCompleted.GetPersistentTarget(i) != bowl.gameObject, "Fruit bowl remains in a group event.");
            }
            Debug.Log("TIMER_DISH_EDIT_CHECKS_PASSED: idle/activity transitions, screen rates, manual pause, timeout, individual dishes and fruit-bowl exclusion.");
        }

        [MenuItem("One More Thing/Check timer and dishes in Play mode")]
        public static void CheckPlay()
        {
            Require(Application.isPlaying, "Enter Play mode first.");
            Check();
            var session = Object.FindFirstObjectByType<RoutineSession>();
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
            var camera = player.viewCamera;
            var position = camera.transform.localPosition; var rotation = camera.transform.localRotation;
            bool playerEnabled = player.enabled, interactorEnabled = interactor.enabled;
            try
            {
                player.enabled = false; interactor.enabled = false;
                session.State.Start(); session.State.SetPaused(false);
                session.Tick(5); float before = session.State.Remaining;
                session.Tick(1); Near(session.State.Remaining, before, "Live idle pause.");
                var computer = Object.FindFirstObjectByType<ComputerScreen>();
                computer.GetComponent<DeviceInteractable>().Open();
                session.Tick(1, clockRate: session.devices.clockMultiplier, clockSource: session.devices.ClockSource);
                Near(session.State.Remaining, before - session.devices.clockMultiplier, "Browsing while stationary must run timer.");
                session.devices.Close(); session.Tick(5);
                var surface = computer.surface.transform;
                camera.transform.position = surface.position - surface.forward * .5f;
                camera.transform.rotation = Quaternion.LookRotation(surface.forward, surface.up);
                Physics.SyncTransforms();
                Require(PlayerInteractor.FindWatchedScreen(camera, 8f, ~0) == computer.surface, "Real screen gaze should be detected.");
                before = session.State.Remaining;
                typeof(PlayerInteractor).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, null);
                Require(session.State.Remaining < before && session.State.ClockRate == computer.surface.clockMultiplier, "Real gaze must resume countdown.");
                camera.transform.localPosition = position; camera.transform.localRotation = rotation;
                session.Tick(5); before = session.State.Remaining;
                var prop = Object.FindObjectsByType<GrabbableComponent>(FindObjectsSortMode.None).First(g => g.GetComponent<TaskInteractable>() == null);
                interactor.grabber.Grab(prop); session.Tick(1);
                Near(session.State.Remaining, before - 1, "Live held prop must resume countdown.");
                interactor.grabber.Release(); session.Tick(5); before = session.State.Remaining;
                var doorField = typeof(PlayerInteractor).GetField("heldDoor", BindingFlags.Instance | BindingFlags.NonPublic);
                doorField.SetValue(interactor, Object.FindFirstObjectByType<DoorInteractable>());
                session.Tick(1); Near(session.State.Remaining, before - 1, "Door grabbing must cost time.");
                doorField.SetValue(interactor, null);
                var dishes = Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Where(t => t.taskId.StartsWith("dish_dining_")).OrderBy(t => t.taskId).ToArray();
                var bowl = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Single(r => r.name == "Plate_03" && TimerDishTools.OnDiningTable(r));
                session.Tick(1, "dish_living"); session.Tick(1, "dish_bedroom");
                for (int i = 0; i < dishes.Length; i++)
                {
                    Require(!session.State.CanWork("cup"), "Sink unlocks before every dish is gathered.");
                    var consumable = dishes[i].GetComponent<ConsumableInteractable>();
                    if (consumable != null && consumable.CanConsume) consumable.TryConsume(interactor.grabber);
                    session.Tick(.4f, dishes[i].taskId);
                    Require(!dishes[i].gameObject.activeSelf, "Collected dish must disappear.");
                    Require(dishes.Skip(i + 1).All(t => t.gameObject.activeSelf), "Collecting one dish hid another.");
                    Require(bowl.gameObject.activeInHierarchy, "Fruit bowl disappeared.");
                }
                Require(session.State.CanWork("cup"), "All dishes gathered must unlock washing.");
                session.Tick(3f, "cup"); Require(bowl.gameObject.activeInHierarchy, "Washing must leave fruit bowl alone.");
                Debug.Log("TIMER_DISH_PLAY_CHECKS_PASSED: real device/gaze/grab/door timer paths; each dish removed alone; washing gated by all dishes; fruit bowl preserved.");
            }
            finally
            {
                session.devices.Close(); interactor.grabber.Release();
                camera.transform.localPosition = position; camera.transform.localRotation = rotation;
                player.enabled = playerEnabled; interactor.enabled = interactorEnabled;
                session.State.SetPaused(true);
            }
        }
        static void Near(float value, float expected, string message) => Require(Mathf.Abs(value - expected) < .005f, message + " " + value + " != " + expected);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
