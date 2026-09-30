using UnityEngine;
using UnityEngine.UI;

namespace OneMoreThing //this script powers the social media feed displayed in game
{
    public sealed class LocalFeed : MonoBehaviour
    {
        public int Batch { get; private set; }
        public ScrollRect Scroll { get; private set; }
        private RectTransform content;
        private float width;
        private readonly string[] names = { "miso.and.me", "small.spaces", "weekend.kitchen", "tiny.discoveries", "slow.mornings", "saved.for.later" };
        private readonly string[] captions = { "He has a bed. He chose the box.", "A little corner worth staying in.", "Five-minute breakfast. Forty-minute cleanup.", "One question. Twelve open tabs.", "Nothing on the list. Everything to look at.", "I'll definitely come back to this." };
        public void Build(Vector2 size)
        {
            width = size.x;
            var view = DeviceUI.Box("Feed viewport", transform, Vector2.zero, size, Color.white);
            view.raycastTarget = true; view.gameObject.AddComponent<RectMask2D>();
            Scroll = gameObject.AddComponent<ScrollRect>(); Scroll.viewport = view.rectTransform;
            Scroll.horizontal = false; Scroll.vertical = true; Scroll.scrollSensitivity = 35; Scroll.movementType = ScrollRect.MovementType.Clamped;
            content = DeviceUI.Rect("Posts", view.transform, Vector2.zero, new Vector2(width, 1));
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1); content.pivot = new Vector2(0.5f, 1);
            Scroll.content = content; Draw();
        }
        public void NextBatch() { Batch++; Draw(); }
        private void Draw()
        {
            for (int i = content.childCount - 1; i >= 0; i--) { var child = content.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            float cardHeight = width < 450 ? 360 : 330;
            content.sizeDelta = new Vector2(width, cardHeight * 3 + 86);
            for (int i = 0; i < 3; i++)
            {
                int index = (Batch * 3 + i) % names.Length;
                var card = DeviceUI.Rect("Post " + index, content, Vector2.zero, new Vector2(width - 28, cardHeight - 12));
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.anchoredPosition = new Vector2(0, -cardHeight * i - cardHeight * 0.5f);
                DeviceUI.Text("Author", card, new Vector2(0, cardHeight * 0.5f - 35), new Vector2(width - 58, 32), names[index], 22);
                var art = DeviceUI.Box("Photo", card, new Vector2(0, 13), new Vector2(width - 54, 190), index % 2 == 0 ? new Color(0.87f, 0.81f, 0.72f) : new Color(0.65f, 0.78f, 0.75f));
                if (index % 2 == 0) DeviceUI.Cat(art.transform, new Vector2(0, -4));
                else
                {
                    DeviceUI.Box("Window", art.transform, new Vector2(-55, 25), new Vector2(87, 95), new Color(0.90f, 0.94f, 0.85f));
                    DeviceUI.Box("Desk", art.transform, new Vector2(20, -43), new Vector2(190, 13), new Color(0.63f, 0.43f, 0.29f));
                    DeviceUI.Box("Plant", art.transform, new Vector2(67, -12), new Vector2(39, 51), DeviceUI.Teal);
                }
                DeviceUI.Text("Caption", card, new Vector2(0, -cardHeight * 0.5f + 50), new Vector2(width - 58, 62), captions[index], 22);
            }
            var button = DeviceUI.Button("More posts", content, Vector2.zero, new Vector2(width - 50, 52), "More for you", NextBatch);
            var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -cardHeight * 3 - 37);
            content.anchoredPosition = Vector2.zero;
            Scroll.StopMovement(); Scroll.verticalNormalizedPosition = 1;
        }
    }
}
