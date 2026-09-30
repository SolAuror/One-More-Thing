using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Sol.Grab;
using Sol.Outline;
using Object = UnityEngine.Object;

namespace OneMoreThing.Editor
{
    [InitializeOnLoad]
    public static class HouseFeelChecks
    {
        const string Running = "OneMoreThing.HouseFeelChecks";
        static double nextStep, timeout;
        static int step, errors;
        static float baselineFov, remaining;
        static FirstPersonController player;
        static RoutineSession session;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool batch;
        static InputSettings.BackgroundBehavior savedBackground;
        static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        static bool changedInputSettings;
        static HouseFeelChecks() => EditorApplication.playModeStateChanged += ModeChanged;

        public static void RunBatch()
        {
            HouseFeelTools.ApplyBatch();
            CheckSavedScene();
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("One More Thing/Check house refinements")]
        public static void CheckSavedScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Require(scene.path == HouseFeelTools.ScenePath, "Open HouseLevel.");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var components = t.GetComponents<Component>();
                Require(components.All(c => c != null), "Missing script: " + t.name);
                Require(!components.GroupBy(c => c.GetType()).Any(g => g.Count() > 1), "Duplicate component: " + t.name);
                if (!HouseFeelTools.IsLooseProp(t)) continue;
                Require(t.GetComponent<GrabInteractable>() != null && t.GetComponent<OutlineComponent>() != null, "Missing clutter interaction: " + t.name);
                Require(t.GetComponentsInChildren<MeshCollider>().All(c => c.convex), "Concave movable collider: " + t.name);
            }
            var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            Require(doors.Length == 7, "Seven internal doors expected.");
            foreach (var door in doors)
            {
                var start = door.transform.localRotation;
                var open = door.IsOpen;
                if (open) { door.Toggle(); Require(Quaternion.Angle(door.transform.localRotation, door.ClosedLocalRotation) < .01f, "Ajar door cannot close: " + door.name); }
                door.ApplyStartingPose();
                Require(Quaternion.Angle(start, door.transform.localRotation) < .01f, "Starting pose not restored.");
            }
            var test = new GameObject("Door test");
            try
            {
                var door = test.AddComponent<DoorInteractable>();
                foreach (DoorInteractable.SwingDirection direction in Enum.GetValues(typeof(DoorInteractable.SwingDirection)))
                {
                    door.swingDirection = direction;
                    door.ConfigureClosedPose(Quaternion.Euler(0, 180, 0), 25f);
                    Require(Mathf.Abs(Mathf.Abs(door.CurrentAngle) - 25f) < .001f, "Open percent interpolation.");
                    door.Toggle(); Require(Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0, 180, 0)) < .01f, "Closed reference pose.");
                    Require(door.ClampAngle(900) == (direction == DoorInteractable.SwingDirection.Outward ? 0 : 100), "Positive swing stop.");
                    Require(door.ClampAngle(-900) == (direction == DoorInteractable.SwingDirection.Inward ? 0 : -100), "Negative swing stop.");
                }
                door.swingDirection = DoorInteractable.SwingDirection.Both;
                door.ConfigureClosedPose(Quaternion.identity, -40f);
                Require(Mathf.Abs(door.CurrentAngle + 40f) < .001f, "Outward starting pose in Both mode.");
            }
            finally { Object.DestroyImmediate(test); }
            var flowers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => (t.name.StartsWith("Flower_03") || t.name.StartsWith("Flower_04") || t.name.StartsWith("Flower_07"))
                    && t.GetComponent<Renderer>() != null).ToArray();
            var plants = flowers.Select(t => t.GetComponent<TaskInteractable>()).ToArray();
            Require(plants.Length == 16 && plants.All(p => p != null && p.taskId == "plant"
                && p.plantRequests.Length == 2 && p.plantRequests.All(s => !string.IsNullOrWhiteSpace(s))),
                "All sixteen flowers need two individual watering clues.");
            Require(plants.Select(p => p.locationDescription).Distinct().Count() == 16, "Every plant needs a distinct location.");
            Require(ItemNames.For(plants[0]) == "Plant", "Asset names must not leak into prompts.");
            Debug.Log("HOUSE_FEEL_EDIT_CHECKS_PASSED: door closure, starts and limits; all clutter adapters; duplicate audit; sixteen plant request lists.");
        }

        static void ModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                batch = Application.isBatchMode; step = errors = 0;
                nextStep = EditorApplication.timeSinceStartup + 2;
                timeout = EditorApplication.timeSinceStartup + 75;
                Application.logMessageReceived += Log;
                EditorApplication.update += Tick;
            }
        }
        static void Log(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextStep) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < timeout, "Play check timed out.");
                if (step == 0)
                {
                    session = Object.FindFirstObjectByType<RoutineSession>();
                    player = Object.FindFirstObjectByType<FirstPersonController>();
                    baselineFov = player.viewCamera.fieldOfView;
                    Capture("morning-screen");
                    var plantRequest = session.PlantRequest;
                    Require(session.PlantTarget != null && session.PlantTarget.plantRequests.Contains(plantRequest), "Request must describe chosen plant.");
                    Require(session.MessageBody(session.definition.phoneMessages[0]).Contains(plantRequest), "Email and phone request disagree.");
                    Require(Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Count(t => t.taskId == "plant" && t.CanInteract) == 1, "Exactly one plant target.");
                    session.State.Start();
                    foreach (var computer in Object.FindObjectsByType<ComputerScreen>(FindObjectsSortMode.None))
                    {
                        Require(computer.Canvas != null, "Computer UI missing.");
                        computer.GetComponent<DeviceInteractable>().Open();
                        computer.SelectEmail(0); computer.ShowPage(DevicePage.Email);
                        var body = computer.Canvas.GetComponentsInChildren<Text>(true).Last(t => t.name == "Email body");
                        Require(body.text.Contains(plantRequest), "Live email lacks plant request.");
                        Require(computer.Canvas.GetComponents<CanvasGroup>().Length == 1, "Duplicate computer input group.");
                        session.devices.Close();
                    }
                    var loose = Object.FindObjectsByType<GrabbableComponent>(FindObjectsSortMode.None).First(g => g.wakeOnFirstGrab);
                    Require(loose.Body.isKinematic, "Untouched clutter should remain still.");
                    loose.OnGrab(); Require(!loose.Body.isKinematic && !loose.Body.useGravity, "Grab must wake clutter.");
                    loose.OnRelease(); Require(!loose.Body.isKinematic && loose.Body.useGravity, "Release must restore gravity.");
                    var coffee = Object.FindObjectsByType<ConsumableInteractable>(FindObjectsSortMode.None).First(c => c.kind == ConsumableInteractable.ConsumptionKind.Coffee);
                    Require(coffee.TryConsume(), "Coffee cannot be drunk.");
                    Require(Mathf.Approximately(player.MovementSpeed, player.walkSpeed * 1.12f) && player.CoffeeSecondsRemaining == 30f, "Coffee speed/duration.");
                    Require(!coffee.TryConsume(), "Coffee can only be consumed once.");
                    player.DrinkCoffee(); Require(player.MovementSpeed < player.walkSpeed * 1.13f, "Coffee must not stack.");
                    remaining = player.CoffeeSecondsRemaining;
                    session.State.SetPaused(true);
                    step++; nextStep = EditorApplication.timeSinceStartup + .5;
                }
                else if (step == 1)
                {
                    Require(Mathf.Approximately(remaining, player.CoffeeSecondsRemaining), "Paused coffee timer advanced.");
                    session.State.SetPaused(false);
                    savedBackground = InputSystem.settings.backgroundBehavior;
                    savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    changedInputSettings = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift)); InputSystem.Update();
                    // Exercise the same FOV branch used after a successful controller move, without moving the player's spawn.
                    Invoke(player, "UpdateHurryView", true);
                    Require(player.viewCamera.fieldOfView > baselineFov && player.viewCamera.fieldOfView <= baselineFov + 4.01f,
                        "Hurry FOV: " + player.viewCamera.fieldOfView + " / " + baselineFov + "; shift=" + keyboard.leftShiftKey.isPressed + "; delta=" + Time.unscaledDeltaTime);
                    Require(Mathf.Approximately(player.MovementSpeed, player.walkSpeed * 1.12f), "Shift must not increase movement speed.");
                    player.ResetHurryView(); Require(Mathf.Approximately(player.viewCamera.fieldOfView, baselineFov), "FOV reset.");
                    mouse = InputSystem.AddDevice<Mouse>();
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
                    var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
                    var phone = Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Single(t => t.taskId == "phone");
                    interactor.grabber.Grab(phone.GetComponent<GrabbableComponent>());
                    Invoke(interactor, "LateUpdate");
                    Require(interactor.HasTask && interactor.Prompt.Contains("Phone") && interactor.Prompt.Contains("Hold E")
                        && !interactor.Prompt.Contains("LMB"), "Held task prop must retain its simple E action.");
                    session.Tick(.3f, "phone");
                    Require(session.HasPhone, "Phone can be collected while held.");
                    interactor.grabber.Release();
                    InputSystem.RemoveDevice(mouse); mouse = null;
                    InputSystem.RemoveDevice(keyboard); keyboard = null;
                    Time.timeScale = 20f;
                    step++; nextStep = EditorApplication.timeSinceStartup + 3;
                }
                else if (step == 2)
                {
                    if (player.CoffeeSecondsRemaining > 0f) { nextStep = EditorApplication.timeSinceStartup + .5; return; }
                    Time.timeScale = 1;
                    Require(Mathf.Approximately(player.MovementSpeed, player.walkSpeed), "Coffee did not expire.");
                    session.State.SetPaused(false);
                    var computer = Object.FindFirstObjectByType<ComputerScreen>();
                    computer.GetComponent<DeviceInteractable>().Open();
                    computer.SelectEmail(0); computer.ShowPage(DevicePage.Email);
                    step++; nextStep = EditorApplication.timeSinceStartup + 1;
                }
                else if (step == 3)
                {
                    Capture("plant-email");
                    session.devices.Close();
                    session.State.ReleaseTask("message");
                    Require(session.devices.OpenPhone(), "Phone opens after collection.");
                    session.devices.Phone.OpenThread(0);
                    step++; nextStep = EditorApplication.timeSinceStartup + .5;
                }
                else
                {
                    Capture("plant-phone");
                    session.devices.Close();
                    Require(errors == 0, "Runtime errors were logged.");
                    Debug.Log("HOUSE_FEEL_PLAY_CHECKS_PASSED: live plant email and phone consistency; computer focus; clutter waking and release; one-shot coffee, pause, nonstacking and expiry; hurry FOV/reset.");
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static void Capture(string name)
        {
            var camera = Object.FindFirstObjectByType<FirstPersonController>().viewCamera;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f; }
            var rt = new RenderTexture(1600, 900, 24);
            var old = camera.targetTexture;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(Path.GetFullPath(Application.dataPath + "/../../../" + name + ".png"), image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = old; RenderTexture.active = null; rt.Release(); Object.Destroy(rt);
                foreach (var canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
        static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        static void Finish(int code)
        {
            Time.timeScale = 1;
            if (changedInputSettings)
            {
                InputSystem.settings.backgroundBehavior = savedBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
                changedInputSettings = false;
            }
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            SessionState.SetBool(Running, false);
            if (batch) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
