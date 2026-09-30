using UnityEngine;
using UnityEngine.UI;

namespace OneMoreThing //this is the general script for the the non interactable screens in the game. it was the precursor to both phone and desktop screen scripts and is used for distracting the player.
{
    // The thin collider covers only the visible display. Gaze uses the first solid
    // ray hit, so walls, the screen's back and carried objects do not drain time.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AttentionScreen : MonoBehaviour
    {
        public string screenId = "screen";
        public RoutineSession session;
        [Min(1f)] public float clockMultiplier = 2f;
        [Min(1f)] public float pageSeconds = 5f;
        public string[] headlines = { "One more clip", "Wait for the ending", "You might also like..." };
        public string[] captions = { "A tiny detour.", "This will only take a moment.", "Up next, just for you." };
        public Text headlineText, captionText, pageText;
        public Image progressImage;
        public RectTransform motionGraphic;
        [HideInInspector] public bool useDeviceInterface;
        private float elapsed;
        private int shownPage = -1;

        public bool Faces(Vector3 point) => Vector3.Dot(-transform.forward, point - transform.position) > 0f;

        private void Update()
        {
            if (useDeviceInterface) return;
            if (headlines == null || headlines.Length == 0) return;
            if (session != null && session.IsRunning) elapsed += Time.deltaTime;
            float pageTime = Mathf.Max(1f, pageSeconds);
            int page = Mathf.FloorToInt(elapsed / pageTime) % headlines.Length;
            if (page != shownPage)
            {
                shownPage = page;
                if (headlineText != null) headlineText.text = headlines[page];
                if (captionText != null) captionText.text = captions != null && captions.Length > 0 ? captions[page % captions.Length] : "";
                if (pageText != null) pageText.text = "FOR YOU    /    " + (page + 1).ToString("00") + "    /    UP NEXT >";
            }
            if (progressImage != null)
                progressImage.rectTransform.localScale = new Vector3(Mathf.Repeat(elapsed / pageTime, 1f), 1f, 1f);
            if (motionGraphic != null)
            {
                motionGraphic.anchoredPosition = new Vector2(285f + Mathf.Sin(elapsed * 1.1f) * 72f, 22f + Mathf.Cos(elapsed * 0.8f) * 32f);
                motionGraphic.localRotation = Quaternion.Euler(0, 0, elapsed * 12f);
            }
        }
    }
}
