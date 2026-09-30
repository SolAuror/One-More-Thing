using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace OneMoreThing.Editor
{
    [InitializeOnLoad]
    public static class AspectRatioChecks
    {
        const string Running = "OneMoreThing.AspectRatioChecks";
        static readonly Vector2Int[] Sizes = { new(1024, 768), new(1280, 720), new(1920, 1080),
            new(1920, 1200), new(2560, 1080), new(3840, 1080), new(3840, 2160), new(720, 1280) };
        static readonly string[] Views = { "morning", "hud", "note", "phone", "messages", "phone-feed",
            "computer-mail", "computer-feed", "pause", "results" };
        static int view, size, errors;
        static double next, deadline;
        static RoutineSession session;
        static RoutineHUD hud;
        static Camera camera;
        static Canvas overlay;
        static MorningCarryUI carry;
        static Keyboard keyboard;
        static InputSettings.BackgroundBehavior savedBackground;
        static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        static string Output => Path.GetFullPath(Application.dataPath + "/../../../aspect-ratio-checks");

        static AspectRatioChecks() => EditorApplication.playModeStateChanged += change =>
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                view = size = errors = 0; next = EditorApplication.timeSinceStartup + 3;
                deadline = next + 360;
                Application.logMessageReceived += Log;
                EditorApplication.update += Tick;
            }
        };

        public static void RunBatch()
        {
            WindowsBuildTools.Prepare();
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        static void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Aspect checks timed out.");
                if (session == null)
                {
                    session = Object.FindFirstObjectByType<RoutineSession>();
                    hud = Object.FindFirstObjectByType<RoutineHUD>();
                    Require(hud.HasRequiredReferences && hud.enabled, "HUD startup failed.");
                    var player = Object.FindFirstObjectByType<FirstPersonController>();
                    camera = player.viewCamera; player.enabled = false;
                    Object.FindFirstObjectByType<PlayerInteractor>().enabled = false;
                    carry = hud.GetComponent<MorningCarryUI>();
                    savedBackground = InputSystem.settings.backgroundBehavior;
                    savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    overlay = hud.GetComponent<Canvas>();
                    overlay.renderMode = RenderMode.ScreenSpaceCamera; overlay.worldCamera = camera; overlay.planeDistance = .3f;
                }
                if (size == 0) SetView();
                CaptureAndCheck(Sizes[size]);
                size++;
                if (size == Sizes.Length) { size = 0; view++; }
                if (view == Views.Length)
                {
                    Require(errors == 0, "Runtime errors occurred during aspect checks.");
                    Debug.Log("ASPECT_CHECKS_PASSED: 80 rendered UI views across 4:3, 16:9, 16:10, 21:9, 32:9, 4K and portrait windows.");
                    Finish(0); return;
                }
                next = EditorApplication.timeSinceStartup + .05;
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        static void SetView()
        {
            if (view == 1)
            {
                hud.primaryButton.onClick.Invoke();
                Require(session.CanControl, "Start morning button failed.");
                session.State.Tick(2, "phone");
            }
            if (view == 3) Require(session.devices.OpenPhone(), "Phone did not open.");
            if (view == 4)
            {
                session.State.ReleaseTask("message");
                session.devices.Phone.OpenThread(0);
            }
            if (view == 5) session.devices.ShowPage(DevicePage.Social);
            if (view == 6)
            {
                session.devices.Close();
                session.devices.OpenComputer(Object.FindFirstObjectByType<DeviceInteractable>());
                session.devices.ShowPage(DevicePage.Email);
            }
            if (view == 7) session.devices.ShowPage(DevicePage.Social);
            if (view == 8) { session.devices.Close(); session.State.SetPaused(true); }
            if (view == 9) { session.State.SetPaused(false); session.State.Tick(10000); }
        }

        static void CaptureAndCheck(Vector2Int resolution)
        {
            var target = new RenderTexture(resolution.x, resolution.y, 24);
            var old = camera.targetTexture;
            try
            {
                camera.targetTexture = target; camera.aspect = (float)resolution.x / resolution.y;
                InputSystem.QueueStateEvent(keyboard, view == 2 ? new KeyboardState(Key.Tab) : new KeyboardState());
                InputSystem.Update();
                Invoke(hud.GetComponent<CanvasScaler>(), "Handle");
                Invoke(hud, "LateUpdate"); Invoke(carry, "LateUpdate");
                var note = Field<Canvas>(carry, "note");
                if (view >= 3 && view <= 5) Invoke(session.devices.Phone, "LateUpdate");
                if (view == 6 || view == 7)
                {
                    Invoke(session.devices, "UpdateComputerFraming");
                    camera.transform.SetPositionAndRotation(Field<Vector3>(session.devices, "targetPosition"),
                        Field<Quaternion>(session.devices, "targetRotation"));
                }
                Canvas.ForceUpdateCanvases();
                var root = (RectTransform)overlay.transform;
                Require(root.rect.width >= 1199 && root.rect.height >= 899, "Canvas cropped its reference area.");
                if (view == 0 || view >= 8)
                {
                    InFrame((RectTransform)hud.modal.transform, "Morning/pause/results panel");
                    foreach (var button in hud.modal.GetComponentsInChildren<Button>()) InFrame((RectTransform)button.transform, button.name);
                    var scroll = hud.modalBody.GetComponentInParent<ScrollRect>();
                    Require(scroll != null, "Results need a scroll viewport.");
                    InFrame(scroll.viewport, "Modal text viewport");
                    if (view == 9)
                    {
                        scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                        Require(scroll.content.rect.height <= scroll.viewport.rect.height || scroll.content.anchoredPosition.y > 0,
                            "Long results cannot scroll to the end.");
                    }
                }
                if (view == 1) InFrame(Field<Canvas>(carry, "watch").transform.Find("Case") as RectTransform, "Watch");
                if (view == 2)
                {
                    Require(note.gameObject.activeSelf && Field<Text>(carry, "checklist").text.Contains("Collect keys"), "Tab note did not populate.");
                    InFrame(note.transform as RectTransform, "Note");
                }
                if (view >= 3 && view <= 5)
                {
                    var phone = session.devices.Phone.Canvas;
                    InFrame(phone.transform as RectTransform, "Phone");
                    InFrame(phone.transform.Find("Put away hint") as RectTransform, "Phone exit hint");
                    foreach (var button in phone.GetComponentsInChildren<Button>())
                    {
                        var scroll = button.GetComponentInParent<ScrollRect>();
                        if (scroll == null) InFrame((RectTransform)button.transform, button.name);
                    }
                    if (view == 5) CheckFeed(session.devices.Phone.Feed);
                }
                if (view == 6 || view == 7)
                {
                    InFrame(session.devices.ActiveComputer.Canvas.transform as RectTransform, "Focused computer");
                    if (view == 7) CheckFeed(session.devices.ActiveComputer.Feed);
                }
                camera.Render(); RenderTexture.active = target;
                var capture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0); capture.Apply();
                File.WriteAllBytes(Path.Combine(Output, Views[view] + "-" + resolution.x + "x" + resolution.y + ".png"), capture.EncodeToPNG());
                Object.Destroy(capture);
                Debug.Log("UI_VIEW_PASSED: " + Views[view] + " " + resolution);
            }
            finally
            {
                camera.targetTexture = old; camera.ResetAspect(); RenderTexture.active = null;
                target.Release(); Object.Destroy(target);
            }
        }

        static void InFrame(RectTransform rect, string label)
        {
            Require(rect != null, "Missing UI: " + label);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var world in corners)
            {
                var p = camera.WorldToViewportPoint(world);
                Require(p.z > 0 && p.x >= -.001f && p.x <= 1.001f && p.y >= -.001f && p.y <= 1.001f,
                    label + " clipped at " + camera.aspect + ": " + p);
            }
        }
        static void CheckFeed(LocalFeed feed)
        {
            InFrame(feed.Scroll.viewport, "Social viewport");
            feed.Scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            InFrame(feed.Scroll.content.Find("More posts") as RectTransform, "More posts after scrolling");
            feed.Scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
        }
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Finish(int code)
        {
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = savedBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
            }
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            SessionState.SetBool(Running, false);
            EditorApplication.Exit(code);
        }
    }
}
