using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreThing //this script is for the in game mobile phone display, which is used to show messages and the social feed
{
    public sealed class PhoneScreen : MonoBehaviour
    {
        public RoutineSession session;
        public DevicePage Page { get; private set; } = DevicePage.Home;
        // Arrival order can differ from definition order; keep the selected conversation by task ID.
        public int SelectedMessage => selectedMessageId == null ? -1
            : System.Array.FindIndex(Messages, message => message.taskId == selectedMessageId);
        public LocalFeed Feed { get; private set; }
        public Canvas Canvas { get; private set; }
        public string WorkingTaskId => workingTask != null && Page == DevicePage.Messages && session.State.CanWork(workingTask) ? workingTask : null;
        private GameObject home, inbox, conversation, social;
        private Text clock, homeClock, remaining, badge, preview, threadTitle, received, sent, replyStatus;
        private Button reply;
        private RectTransform threadList;
        private string workingTask;
        private string selectedMessageId;
        private int lastUnread = -1, lastReceived = -1;
        private Camera viewCamera;
        private PhoneMessage[] Messages => (session.definition.phoneMessages ?? System.Array.Empty<PhoneMessage>()).Where(m => session.State.Find(m.taskId).Available).ToArray();

        public void Initialize(RoutineSession owner, Camera camera)
        {
            session = owner;
            viewCamera = camera;
            Canvas = DeviceUI.WorldCanvas("Held smartphone", camera.transform, new Vector2(440, 830), camera);
            Canvas.transform.localPosition = new Vector3(0.065f, -0.002f, 0.19f);
            Canvas.transform.localRotation = Quaternion.Euler(0, -3, -2);
            Canvas.transform.localScale = Vector3.one * 0.000285f;
            var root = Canvas.transform;
            DeviceUI.Box("Phone shadow", root, new Vector2(8, -7), new Vector2(448, 835), new Color(0.01f, 0.02f, 0.02f));
            DeviceUI.Box("Case", root, Vector2.zero, new Vector2(440, 830), new Color(0.13f, 0.19f, 0.18f));
            DeviceUI.Box("Glass", root, Vector2.zero, new Vector2(415, 805), new Color(0.94f, 0.96f, 0.92f));
            DeviceUI.Box("Earpiece", root, new Vector2(0, 374), new Vector2(98, 22), new Color(0.04f, 0.07f, 0.07f));
            DeviceUI.Box("Front camera", root, new Vector2(63, 374), new Vector2(15, 15), new Color(0.10f, 0.14f, 0.17f));
            clock = DeviceUI.Text("Status time", root, new Vector2(-133, 373), new Vector2(99, 32), "07:55", 22, DeviceUI.Ink, TextAnchor.MiddleLeft);
            DeviceUI.Text("Status icons", root, new Vector2(143, 373), new Vector2(93, 30), "LTE  92%", 17, DeviceUI.Ink, TextAnchor.MiddleRight);
            DeviceUI.Button("Power", root, new Vector2(223, 182), new Vector2(11, 84), "", () => session.devices.Close(), DeviceUI.Ink);
            home = PageRoot("Home screen"); inbox = PageRoot("Messages inbox"); conversation = PageRoot("Conversation"); social = PageRoot("Phone Loop");
            BuildHome(); BuildMessages(); BuildSocial();
            DeviceUI.Button("Home", root, new Vector2(0, -378), new Vector2(160, 40), "", () => ShowPage(DevicePage.Home), new Color(0,0,0,0));
            DeviceUI.Box("Home indicator", root, new Vector2(0, -378), new Vector2(124, 8), DeviceUI.Ink);
            DeviceUI.Text("Put away hint", root, new Vector2(0, -453), new Vector2(420, 32), "P / Esc  ·  put away", 20, Color.white, TextAnchor.MiddleCenter);
            ShowPage(DevicePage.Home); Canvas.gameObject.SetActive(false);
        }
        private GameObject PageRoot(string name) => DeviceUI.Rect(name, Canvas.transform, new Vector2(0, -4), new Vector2(380, 692)).gameObject;
        private void BuildHome()
        {
            DeviceUI.Text("Day", home.transform, new Vector2(0, 277), new Vector2(330, 40), "Monday morning", 25, DeviceUI.Ink, TextAnchor.MiddleCenter);
            homeClock = DeviceUI.Text("Home time", home.transform, new Vector2(0, 211), new Vector2(330, 102), "07:55", 76, DeviceUI.Ink, TextAnchor.MiddleCenter);
            var reminder = DeviceUI.Box("Departure", home.transform, new Vector2(0, 97), new Vector2(346, 103), new Color(0.84f, 0.89f, 0.82f));
            DeviceUI.Text("Departure title", reminder.transform, new Vector2(0, 24), new Vector2(303, 35), "LEAVE AT 08:00", 23);
            remaining = DeviceUI.Text("Departure countdown", reminder.transform, new Vector2(0, -20), new Vector2(303, 40), "", 29);
            var messages = DeviceUI.Button("Messages app", home.transform, new Vector2(-84, -35), new Vector2(88, 88), "", () => ShowPage(DevicePage.Messages));
            DeviceUI.Box("Chat icon", messages.transform, new Vector2(0, 3), new Vector2(51, 33), Color.white);
            DeviceUI.Box("Chat tail", messages.transform, new Vector2(-16, -14), new Vector2(12, 15), Color.white, false);
            badge = DeviceUI.Text("Unread badge", messages.transform, new Vector2(38, 36), new Vector2(42, 34), "", 23, DeviceUI.Ink, TextAnchor.MiddleCenter);
            DeviceUI.Button("Loop app", home.transform, new Vector2(84, -35), new Vector2(88, 88), "loop", () => ShowPage(DevicePage.Social), new Color(0.70f, 0.40f, 0.40f), 28);
            DeviceUI.Text("App names", home.transform, new Vector2(0, -101), new Vector2(338, 36), "Messages                 Loop", 23, DeviceUI.Ink, TextAnchor.MiddleCenter);
            var notification = DeviceUI.Button("Recent notification", home.transform, new Vector2(0, -226), new Vector2(346, 151), "", () => ShowPage(DevicePage.Messages), new Color(0.86f, 0.89f, 0.85f));
            preview = DeviceUI.Text("Preview", notification.transform, Vector2.zero, new Vector2(307, 122), "Messages\n\nAll quiet for now.", 24);
        }
        private void BuildMessages()
        {
            DeviceUI.Text("Inbox title", inbox.transform, new Vector2(0, 290), new Vector2(340, 58), "Messages", 42);
            threadList = DeviceUI.Rect("Threads", inbox.transform, new Vector2(0, -9), new Vector2(350, 490));
            DeviceUI.Button("Back to messages", conversation.transform, new Vector2(-107, 292), new Vector2(135, 41), "< Inbox", () => { selectedMessageId = null; ShowPage(DevicePage.Messages); }, fontSize: 21);
            threadTitle = DeviceUI.Text("Contact", conversation.transform, new Vector2(0, 225), new Vector2(338, 60), "", 34);
            var bubble = DeviceUI.Box("Incoming bubble", conversation.transform, new Vector2(-8, 83), new Vector2(322, 190), new Color(0.85f, 0.89f, 0.85f));
            received = DeviceUI.Text("Message", bubble.transform, Vector2.zero, new Vector2(284, 152), "", 25);
            received.resizeTextForBestFit = true; received.resizeTextMinSize = 18; received.resizeTextMaxSize = 25;
            var response = DeviceUI.Box("Outgoing bubble", conversation.transform, new Vector2(22, -122), new Vector2(291, 125), new Color(0.72f, 0.85f, 0.78f));
            sent = DeviceUI.Text("Reply text", response.transform, Vector2.zero, new Vector2(254, 95), "", 24);
            replyStatus = DeviceUI.Text("Delivery", conversation.transform, new Vector2(0, -214), new Vector2(333, 40), "", 20, DeviceUI.Ink, TextAnchor.MiddleRight);
            reply = DeviceUI.Button("Send reply", conversation.transform, new Vector2(0, -283), new Vector2(338, 58), "Send reply", ReplySelected, fontSize: 23);
        }
        private void BuildSocial()
        {
            DeviceUI.Text("Loop heading", social.transform, new Vector2(0, 295), new Vector2(340, 52), "loop   /   For you", 32);
            var root = DeviceUI.Rect("Phone feed", social.transform, new Vector2(0, -24), new Vector2(368, 560));
            Feed = root.gameObject.AddComponent<LocalFeed>(); Feed.Build(new Vector2(368, 560));
        }
        public bool Supports(DevicePage page) => page == DevicePage.Home || page == DevicePage.Messages || page == DevicePage.Social;
        public void ShowPage(DevicePage page)
        {
            if (!Supports(page) || Canvas == null) return;
            workingTask = null; Page = page;
            home.SetActive(page == DevicePage.Home); social.SetActive(page == DevicePage.Social);
            inbox.SetActive(page == DevicePage.Messages && SelectedMessage < 0);
            conversation.SetActive(page == DevicePage.Messages && SelectedMessage >= 0);
            if (page == DevicePage.Messages) DrawMessages();
        }
        public void OpenThread(int index)
        {
            var messages = Messages;
            if (index < 0 || index >= messages.Length) return;
            selectedMessageId = messages[index].taskId; ShowPage(DevicePage.Messages);
        }
        public void ReplySelected()
        {
            if (!session.IsRunning || !session.HasPhone || !session.devices.IsPhone || !session.devices.IsOpen
                || Page != DevicePage.Messages || selectedMessageId == null) return;
            if (session.State.CanWork(selectedMessageId)) workingTask = selectedMessageId;
        }
        private void DrawMessages()
        {
            for (int i = threadList.childCount - 1; i >= 0; i--) { var child = threadList.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            var messages = Messages;
            if (messages.Length == 0) DeviceUI.Text("Empty inbox", threadList, new Vector2(0, 139), new Vector2(321, 158), "No messages yet.\n\nYour morning is yours.", 27);
            for (int i = 0; i < messages.Length; i++)
            {
                string taskId = messages[i].taskId; bool answered = session.State.Find(taskId).Completed;
                DeviceUI.Button("Thread " + i, threadList, new Vector2(0, 168 - i * 126), new Vector2(340, 107), messages[i].sender + "\n" + (answered ? "Reply sent" : "New message"), () => { selectedMessageId = taskId; ShowPage(DevicePage.Messages); }, answered ? new Color(0.42f, 0.51f, 0.47f) : DeviceUI.Teal, 25);
            }
            RefreshConversation();
        }
        private void RefreshConversation()
        {
            var message = Messages.FirstOrDefault(item => item.taskId == selectedMessageId);
            if (message == null) return;
            var task = session.State.Find(message.taskId);
            threadTitle.text = message.sender; received.text = session.MessageBody(message);
            sent.transform.parent.gameObject.SetActive(task.Completed || workingTask != null);
            sent.text = message.taskId == "message" ? "I'll water it before I leave." : "I'll get back to you soon.";
            replyStatus.text = task.Completed ? "Delivered" : workingTask != null ? "Sending..." : "A reply takes about " + task.Definition.completionTimeCost.ToString("0") + " sec";
            reply.interactable = session.IsRunning && !task.Completed && workingTask == null;
            reply.GetComponentInChildren<Text>().text = task.Completed ? "Sent" : workingTask != null ? "Sending..." : "Send reply";
        }
        public void SetVisible(bool value)
        {
            if (Canvas != null) Canvas.gameObject.SetActive(value);
            if (!value) workingTask = null;
        }
        private void Update()
        {
            if (Canvas == null || session.State == null) return;
            clock.text = homeClock.text = DeviceUI.Clock(session.State); remaining.text = DeviceUI.Remaining(session.State) + " remaining";
            int unread = session.devices.UnreadMessages; var messages = Messages;
            if (lastUnread != unread || lastReceived != messages.Length)
            {
                lastUnread = unread; lastReceived = messages.Length; badge.text = unread > 0 ? unread.ToString() : "";
                preview.text = messages.Length == 0 ? "Messages\n\nAll quiet for now." : messages.Last().sender + "\n\n" + (unread > 0 ? "A new message is waiting." : "You're all caught up.");
                DrawMessages();
            }
            if (workingTask != null && session.State.Find(workingTask).Completed) workingTask = null;
            if (Page == DevicePage.Messages) RefreshConversation();
        }
        private void OnDestroy() { if (Canvas != null) Destroy(Canvas.gameObject); }

        private void LateUpdate()
        {
            if (Canvas == null || !Canvas.gameObject.activeSelf) return;
            // Includes the power button, shadow and put-away hint below the case.
            DeviceUI.FitHeldCanvas(Canvas, viewCamera, new Rect(-224, -472, 456, 895),
                new Rect(.06f, .15f, .88f, .79f), new Vector2(.62f, .5f), .19f, .000285f);
        }
    }
}
