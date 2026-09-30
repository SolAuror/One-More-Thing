using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OneMoreThing //the script for the morning note and the watch the player carries around. it shows the time left and the tasks that need to be completed before leaving the house.
{
    public sealed class MorningCarryUI : MonoBehaviour
    {
        public RoutineSession session;
        private Canvas watch, note;
        private Camera viewCamera;
        private Text countdown, rate, checklist;
        private void Start()
        {
            var camera = FindFirstObjectByType<FirstPersonController>().viewCamera;
            viewCamera = camera;
            watch = DeviceUI.WorldCanvas("Departure watch", camera.transform, new Vector2(300, 180), camera);
            watch.transform.localPosition = new Vector3(-0.23f, -0.15f, 0.25f);
            watch.transform.localRotation = Quaternion.Euler(0, 8, 7);
            watch.transform.localScale = Vector3.one * 0.00029f;
            DeviceUI.Box("Strap", watch.transform, new Vector2(0, -100), new Vector2(145, 340), new Color(.22f,.25f,.25f));
            DeviceUI.Box("Case", watch.transform, Vector2.zero, new Vector2(300,180), new Color(.09f,.12f,.13f));
            DeviceUI.Box("Display", watch.transform, Vector2.zero, new Vector2(278,158), new Color(.77f,.84f,.75f));
            DeviceUI.Text("Deadline", watch.transform, new Vector2(0,55), new Vector2(245,25), "OUT THE DOOR 08:00", 18);
            countdown = DeviceUI.Text("Time left", watch.transform, new Vector2(0,0), new Vector2(250,78), "", 68, null, TextAnchor.MiddleCenter);
            rate = DeviceUI.Text("Clock rate", watch.transform, new Vector2(0,-56), new Vector2(250,28), "", 19, null, TextAnchor.MiddleCenter);
            note = DeviceUI.WorldCanvas("Morning note", camera.transform, new Vector2(520,620), camera);
            note.transform.localPosition = new Vector3(.035f,0,.25f);
            note.transform.localRotation = Quaternion.Euler(0,-3,-3);
            note.transform.localScale = Vector3.one * .0004f;
            DeviceUI.Box("Paper", note.transform, Vector2.zero, new Vector2(520,620), DeviceUI.Paper, false);
            DeviceUI.Box("Tape", note.transform, new Vector2(-120,305), new Vector2(145,40), new Color(.86f,.77f,.52f,.9f), false);
            DeviceUI.Text("Heading", note.transform, new Vector2(0,235), new Vector2(440,66), "Before I leave", 38);
            checklist = DeviceUI.Text("Checklist", note.transform, new Vector2(0,-25), new Vector2(440,450), "", 28);
        }
        private void LateUpdate()
        {
            if (watch == null) return;
            DeviceUI.FitHeldCanvas(watch, viewCamera, new Rect(-150, -90, 300, 180),
                new Rect(.025f, .035f, .24f, .20f), Vector2.zero, .25f, .00029f);
            DeviceUI.FitHeldCanvas(note, viewCamera, new Rect(-260, -310, 520, 635),
                new Rect(.08f, .1f, .84f, .80f), Vector2.one * .5f, .25f, .0004f);
            watch.gameObject.SetActive(session.CanControl);
            bool showNote = session.CanControl && Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            note.gameObject.SetActive(showNote);
            countdown.text = DeviceUI.Remaining(session.State);
            countdown.color = session.TimeCostFlash > 0 || session.State.Remaining < 60 ? new Color(.55f,.2f,.1f) : DeviceUI.Ink;
            rate.text = session.State.ClockRate == 0 ? "IDLE - CLOCK PAUSED" : session.State.ClockRate > 1 ? "TIME PASSING  2x" : "TIME LEFT";
            if (showNote) checklist.text = string.Join("\n\n", session.State.Tasks.Where(t => t.Definition.required)
                .Select(t => (t.Completed ? "[x] " : "[  ] ") + t.Definition.title))
                + "\n\nIf there's time...\n\nLaundry: " + session.State.RequirementStatus("laundry")
                + (session.State.Find("laundry").Completed ? " - running" : " collected")
                + "\nDishes: " + session.State.RequirementStatus("cup")
                + (session.State.Find("cup").Completed ? " - clean" : " collected");
        }
        private void OnDestroy() { if (watch != null) Destroy(watch.gameObject); if (note != null) Destroy(note.gameObject); }
    }
}
