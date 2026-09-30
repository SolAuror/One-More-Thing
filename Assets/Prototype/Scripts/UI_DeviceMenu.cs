using System.Linq;
using UnityEngine;

namespace OneMoreThing //the menu for the in game phone and computer, which allows the player to view messages, 
                        //social media, and other apps. it also manages the shared clock and input for these devices.
{
    public enum DevicePage { Home, Email, Messages, Social, Videos, Games }

    // Coordinates input and the shared clock. Each physical device owns its UI and app state.
    public sealed class DeviceMenu : MonoBehaviour
    {
        public RoutineSession session;
        [Min(1f)] public float clockMultiplier = 2f;
        public bool IsOpen { get; private set; }
        public bool IsPhone { get; private set; }
        public ComputerScreen ActiveComputer { get; private set; }
        public PhoneScreen Phone { get; private set; }
        public DevicePage Page => IsPhone ? Phone.Page : ActiveComputer != null ? ActiveComputer.Page : DevicePage.Home;
        public string WorkingTaskId => !IsOpen || !session.IsRunning ? null : IsPhone ? Phone.WorkingTaskId : ActiveComputer != null ? ActiveComputer.WorkingTaskId : null;
        public int UnreadMessages => session.State == null ? 0 : (session.definition.phoneMessages ?? System.Array.Empty<PhoneMessage>())
            .Count(m => session.State.Find(m.taskId).Available && !session.State.Find(m.taskId).Completed);
        public string ClockSource => IsPhone ? "phone-menu" : "computer-menu";
        private Camera viewCamera;
        private Vector3 savedPosition, targetPosition;
        private Quaternion savedRotation, targetRotation;
        private float visitStartedAt;

        private void Awake()
        {
            if (session == null) session = GetComponent<RoutineSession>();
            session.devices = this;
        }
        private void Start()
        {
            viewCamera = FindFirstObjectByType<FirstPersonController>().viewCamera;
            Phone = new GameObject("Phone apps").AddComponent<PhoneScreen>(); Phone.transform.SetParent(transform, false);
            Phone.Initialize(session, viewCamera);
            foreach (var device in FindObjectsByType<DeviceInteractable>(FindObjectsSortMode.None))
            {
                if (device.session == null) device.session = session;
                var screen = device.GetComponent<ComputerScreen>();
                if (screen == null) screen = device.gameObject.AddComponent<ComputerScreen>();
                screen.Initialize(session, viewCamera);
            }
        }
        public bool OpenPhone()
        {
            if (!session.IsRunning || !session.HasPhone || Phone == null)
            { if (session.IsRunning) session.Notify("My phone is beside the bed."); return false; }
            if (IsOpen) Close();
            IsPhone = IsOpen = true; visitStartedAt = session.State.Elapsed;
            Phone.SetVisible(true); session.SyncCursor(); return true;
        }
        public void OpenComputer(DeviceInteractable device)
        {
            if (!session.IsRunning || device == null || viewCamera == null) return;
            var computer = device.GetComponent<ComputerScreen>();
            if (computer == null || computer.Canvas == null) return;
            if (IsOpen) Close();
            FindFirstObjectByType<FirstPersonController>()?.ResetHurryView();
            savedPosition = viewCamera.transform.localPosition; savedRotation = viewCamera.transform.localRotation;
            ActiveComputer = computer; IsOpen = true; IsPhone = false;
            visitStartedAt = session.State.Elapsed;
            UpdateComputerFraming();
            computer.SetFocused(true); session.SyncCursor();
        }

        private void UpdateComputerFraming()
        {
            var surface = (RectTransform)ActiveComputer.surface.transform;
            float width = surface.rect.width * Mathf.Abs(surface.lossyScale.x);
            float height = surface.rect.height * Mathf.Abs(surface.lossyScale.y);
            float tangent = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            float distance = Mathf.Max(width / (2 * tangent * viewCamera.aspect * .80f),
                height / (2 * tangent * .80f));
            targetPosition = surface.TransformPoint(surface.rect.center) - surface.forward * Mathf.Max(.20f, distance);
            targetRotation = Quaternion.LookRotation(surface.forward, surface.up);
        }
        public void OpenComputer(string name)
        {
            var device = FindObjectsByType<DeviceInteractable>(FindObjectsSortMode.None).FirstOrDefault(d => d.displayName == name);
            if (device == null) device = FindFirstObjectByType<DeviceInteractable>();
            OpenComputer(device);
        }
        public void Close()
        {
            if (!IsOpen) return;
            float spent = session.State.Elapsed - visitStartedAt;
            if (ActiveComputer != null)
            {
                ActiveComputer.SetFocused(false);
                if (viewCamera != null) { viewCamera.transform.localPosition = savedPosition; viewCamera.transform.localRotation = savedRotation; }
                ActiveComputer = null;
            }
            if (Phone != null) Phone.SetVisible(false);
            IsOpen = false; IsPhone = false;
            if (session.IsRunning && spent >= 1) session.Notify(Mathf.RoundToInt(spent) + " seconds passed.");
            session.SyncCursor();
        }
        public void ShowPage(DevicePage page) { if (!IsOpen) return; if (IsPhone) Phone.ShowPage(page); else ActiveComputer?.ShowPage(page); }
        public void ReplySelected() { if (IsPhone) Phone.ReplySelected(); }
        public void NextPost() { if (IsPhone) Phone.Feed.NextBatch(); else ActiveComputer?.Feed.NextBatch(); }
        private void Update()
        {
            if (!IsOpen || session.State == null) return;
            if (session.State.Phase == RunPhase.Succeeded || session.State.Phase == RunPhase.TimedOut) { Close(); return; }
            if (IsPhone) Phone.SetVisible(session.IsRunning);
            else if (session.IsRunning && ActiveComputer != null)
            {
                UpdateComputerFraming();
                float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18);
                viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(viewCamera.transform.position, targetPosition, blend), Quaternion.Slerp(viewCamera.transform.rotation, targetRotation, blend));
            }
        }
        private void OnDisable() { if (session != null && session.State != null) Close(); }
    }
}
