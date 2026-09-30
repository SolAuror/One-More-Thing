using UnityEngine;
using UnityEngine.UI;

namespace OneMoreThing
{
    [DisallowMultipleComponent]
    public sealed class ComputerScreen : MonoBehaviour
    {
        public RoutineSession session;
        public AttentionScreen surface;
        [Tooltip("Optional authored desktop canvas. Its placement is preserved and live controls are rebuilt inside it.")]
        public Canvas desktopTemplate;
        public DevicePage Page { get; private set; } = DevicePage.Home;
        public string WorkingTaskId => working && Page == DevicePage.Email && session.State.CanWork("email") ? "email" : null;
        public LocalFeed Feed { get; private set; }
        public Canvas Canvas { get; private set; }
        private CanvasGroup input;
        private GameObject home, email, social, games;
        private Text clock, countdown, emailBody, emailSubject, emailStatus;
        private Button readButton;
        private bool working;
        private int selectedEmail;

        public void Initialize(RoutineSession owner, Camera camera)
        {
            if (Canvas != null) return;
            session = owner;
            if (surface == null) surface = GetComponentInChildren<AttentionScreen>();
            if (surface == null) { Debug.LogError("Computer needs a screen surface.", this); return; }
            surface.useDeviceInterface = true;
            for (int i = 0; i < surface.transform.childCount; i++) surface.transform.GetChild(i).gameObject.SetActive(false);
            var baseImage = surface.GetComponent<Image>(); if (baseImage != null) baseImage.enabled = false;
            Canvas = desktopTemplate != null ? desktopTemplate
                : DeviceUI.WorldCanvas("Computer desktop", surface.transform, new Vector2(1440, 700), camera);
            Canvas.worldCamera = camera;
            // Imported monitors have non-uniform scale. Keep type undistorted and inside the bezel.
            var parentScale = surface.transform.lossyScale;
            float pixelScale = ((RectTransform)surface.transform).rect.width * Mathf.Abs(parentScale.x) * .89f / 1440f;
            if (desktopTemplate == null)
            {
                Canvas.transform.localScale = new Vector3(pixelScale / Mathf.Abs(parentScale.x), pixelScale / Mathf.Abs(parentScale.y), pixelScale / Mathf.Abs(parentScale.z));
                Canvas.transform.localPosition = new Vector3(0, -30, -1);
            }
            if (Canvas.GetComponent<RectMask2D>() == null) Canvas.gameObject.AddComponent<RectMask2D>();
            input = Canvas.GetComponent<CanvasGroup>();
            if (input == null) input = Canvas.gameObject.AddComponent<CanvasGroup>();
            input.interactable = input.blocksRaycasts = false;
            var root = Canvas.transform;
            DeviceUI.Box("Wallpaper", root, Vector2.zero, new Vector2(1440, 700), new Color(0.16f, 0.31f, 0.34f), false);
            DeviceUI.Box("Wallpaper horizon", root, new Vector2(325, -90), new Vector2(950, 420), new Color(0.24f, 0.40f, 0.40f));
            DeviceUI.Box("Menu bar", root, new Vector2(0, 325), new Vector2(1440, 50), new Color(0.08f, 0.19f, 0.23f), false);
            DeviceUI.Text("Brand", root, new Vector2(-505, 325), new Vector2(370, 35), "morningOS    /    Personal", 24, Color.white, TextAnchor.MiddleLeft);
            clock = DeviceUI.Text("Clock", root, new Vector2(450, 325), new Vector2(180, 35), "07:55", 24, Color.white, TextAnchor.MiddleRight);
            DeviceUI.Button("Leave desk", root, new Vector2(641, 325), new Vector2(122, 34), "Stand up", () => session.devices.Close(), new Color(0.26f, 0.42f, 0.43f), 19);
            home = PageRoot("Desktop"); email = PageRoot("Mail"); social = PageRoot("Loop"); games = PageRoot("Games");
            BuildHome(); BuildEmail(); BuildSocial(); BuildGames();
            var dock = DeviceUI.Box("Dock", root, new Vector2(0, -302), new Vector2(640, 65), new Color(0.08f, 0.19f, 0.23f));
            DeviceUI.Button("Desktop", dock.transform, new Vector2(-230, 0), new Vector2(125, 44), "Desktop", () => ShowPage(DevicePage.Home), fontSize: 22);
            DeviceUI.Button("Mail", dock.transform, new Vector2(-77, 0), new Vector2(125, 44), "Mail", () => ShowPage(DevicePage.Email), fontSize: 22);
            DeviceUI.Button("Loop", dock.transform, new Vector2(77, 0), new Vector2(125, 44), "Loop", () => ShowPage(DevicePage.Social), fontSize: 22);
            DeviceUI.Button("Games", dock.transform, new Vector2(230, 0), new Vector2(125, 44), "Games", () => ShowPage(DevicePage.Games), fontSize: 22);
            ShowPage(DevicePage.Home);
        }
        private GameObject PageRoot(string name) => DeviceUI.Rect(name, Canvas.transform, new Vector2(0, 14), new Vector2(1360, 455)).gameObject;
        private void BuildHome()
        {
            DeviceUI.Text("Greeting", home.transform, new Vector2(-225, 85), new Vector2(700, 90), "Good morning.", 70, DeviceUI.Paper);
            DeviceUI.Text("Subtitle", home.transform, new Vector2(-224, -2), new Vector2(700, 70), "A few things before you head out.", 30, DeviceUI.Paper);
            var note = DeviceUI.Box("Departure reminder", home.transform, new Vector2(449, 20), new Vector2(320, 300), new Color(0.94f, 0.83f, 0.56f));
            DeviceUI.Text("Reminder", note.transform, new Vector2(0, 36), new Vector2(264, 180), "BEFORE I LEAVE\n\nKeys. Phone. Bag.\nOut the door by 08:00.", 25);
            countdown = DeviceUI.Text("Time left", note.transform, new Vector2(0, -103), new Vector2(264, 50), "", 27);
            DeviceUI.Button("Open inbox", home.transform, new Vector2(-422, -112), new Vector2(300, 60), "Check the inbox", () => ShowPage(DevicePage.Email));
        }
        private void BuildEmail()
        {
            DeviceUI.Box("Mail window", email.transform, Vector2.zero, new Vector2(1310, 440), DeviceUI.Paper);
            DeviceUI.Text("Inbox heading", email.transform, new Vector2(-473, 173), new Vector2(290, 50), "Mail / Inbox", 33);
            string[] subjects = { "Alex / A little water", "Sam / Back later", "Weekend / A little room" };
            for (int i = 0; i < subjects.Length; i++)
            { int index = i; DeviceUI.Button("Email " + i, email.transform, new Vector2(-464, 80 - i * 81), new Vector2(313, 64), subjects[i], () => SelectEmail(index), fontSize: 22); }
            emailSubject = DeviceUI.Text("Subject", email.transform, new Vector2(167, 164), new Vector2(836, 50), "", 34);
            emailBody = DeviceUI.Text("Email body", email.transform, new Vector2(172, 10), new Vector2(846, 224), "", 28);
            readButton = DeviceUI.Button("Read email", email.transform, new Vector2(387, -164), new Vector2(368, 53), "Mark as read", ReadEmail, fontSize: 23);
            emailStatus = DeviceUI.Text("Read status", email.transform, new Vector2(-26, -163), new Vector2(384, 48), "", 23, DeviceUI.Ink, TextAnchor.MiddleLeft);
            SelectEmail(0);
        }
        private void BuildSocial()
        {
            DeviceUI.Box("Browser", social.transform, Vector2.zero, new Vector2(1080, 455), DeviceUI.Paper);
            DeviceUI.Text("Browser address", social.transform, new Vector2(0, 200), new Vector2(1000, 35), "loop.local / for-you                                           Saved   Following   For you", 23);
            var feed = DeviceUI.Rect("Desktop feed", social.transform, new Vector2(0, -23), new Vector2(980, 369));
            Feed = feed.gameObject.AddComponent<LocalFeed>(); Feed.Build(new Vector2(980, 369));
        }
        private void BuildGames()
        {
            DeviceUI.Box("Game library", games.transform, Vector2.zero, new Vector2(1080, 455), DeviceUI.Paper);
            DeviceUI.Text("Games title", games.transform, new Vector2(0, 178), new Vector2(980, 60), "Your little games shelf", 40);
            var art = DeviceUI.Box("Game cover", games.transform, new Vector2(-301, -20), new Vector2(342, 258), new Color(0.67f, 0.77f, 0.63f));
            DeviceUI.Cat(art.transform, Vector2.zero, 1.2f);
            DeviceUI.Text("Game description", games.transform, new Vector2(192, 0), new Vector2(526, 230), "POCKET BREAK\n\nA small game for a spare minute.\n\nComing soon", 31);
        }
        public bool Supports(DevicePage page) => page == DevicePage.Home || page == DevicePage.Email || page == DevicePage.Social || page == DevicePage.Games;
        public void ShowPage(DevicePage page)
        {
            if (!Supports(page) || Canvas == null) return;
            working = false; Page = page;
            home.SetActive(page == DevicePage.Home); email.SetActive(page == DevicePage.Email);
            social.SetActive(page == DevicePage.Social); games.SetActive(page == DevicePage.Games);
        }
        public void SelectEmail(int index)
        {
            selectedEmail = Mathf.Clamp(index, 0, 2); working = false;
            emailSubject.text = new[] { "A little water before you leave", "Back later", "A little room to think" }[selectedEmail];
            emailBody.text = new[] {
                "From: Alex\n\n" + session.PlantRequest + "\n\nJust that one today. The others have enough water.",
                "From: Sam\n\n" + session.HouseEmail,
                "From: Weekend\n\nA small kindness for whoever comes home next:\nleave one corner a little nicer than you found it.\n\nThe rest of the day can wait its turn."
            }[selectedEmail];
        }
        public void ReadEmail()
        {
            if (!session.IsRunning || session.devices.ActiveComputer != this) return;
            if (selectedEmail == 0 && session.State.CanWork("email")) working = true;
            else emailStatus.text = "Saved for later";
        }
        public void SetFocused(bool value) { if (input != null) input.interactable = input.blocksRaycasts = value; if (!value) working = false; }
        private void Update()
        {
            if (Canvas == null || session.State == null) return;
            clock.text = DeviceUI.Clock(session.State);
            countdown.text = DeviceUI.Remaining(session.State) + " until I leave";
            bool active = session.IsRunning && session.devices.ActiveComputer == this && !session.devices.IsPhone;
            input.interactable = input.blocksRaycasts = active;
            if (session.State.Find("email")?.Completed == true && working) working = false;
            if (selectedEmail == 0)
            {
                bool done = session.State.Find("email")?.Completed == true;
                emailStatus.text = done ? "Read" : working ? "Reading..." : "Unread";
                readButton.interactable = active && !done && !working;
                readButton.GetComponentInChildren<Text>().text = done ? "Read" : "Read / " + session.State.Find("email").Definition.completionTimeCost.ToString("0") + " sec";
            }
            else { readButton.interactable = active; readButton.GetComponentInChildren<Text>().text = "Save for later"; }
        }
    }
}
