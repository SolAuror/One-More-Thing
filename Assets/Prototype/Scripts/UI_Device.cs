using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneMoreThing //this is the shared drawing primitives for the phone and desktop screens. it is used to create the UI elements on those screens, such as buttons, text, and images. 
{
    // Shared drawing primitives only. Phone and computer keep their own views and app state.
    public static class DeviceUI
    {
        public static readonly Color Ink = new Color(0.12f, 0.19f, 0.23f);
        public static readonly Color Paper = new Color(0.96f, 0.95f, 0.90f);
        public static readonly Color Teal = new Color(0.14f, 0.43f, 0.42f);
        private static Sprite rounded;
        public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Rounded UI shape", filterMode = FilterMode.Bilinear };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(Mathf.Abs(x - 31.5f) - 15.5f, Mathf.Abs(y - 31.5f) - 15.5f);
                    float distance = new Vector2(Mathf.Max(p.x, 0), Mathf.Max(p.y, 0)).magnitude - 16;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - distance));
                }
                texture.SetPixels(pixels); texture.Apply();
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 18);
                rounded.name = "Rounded panel";
                return rounded;
            }
        }
        public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        public static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool round = true)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            if (round) { image.sprite = Rounded; image.type = Image.Type.Sliced; }
            image.raycastTarget = false; return image;
        }
        public static Text Text(string name, Transform parent, Vector2 position, Vector2 size, string content, int fontSize = 24, Color? color = null, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Font; text.fontSize = fontSize; text.color = color ?? Ink; text.text = content;
            text.alignment = alignment; text.raycastTarget = false; return text;
        }
        public static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string content, UnityAction callback, Color? color = null, int fontSize = 23)
        {
            var image = Box(name, parent, position, size, color ?? Teal); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.86f, 0.94f, 0.94f); colors.pressedColor = new Color(0.68f, 0.8f, 0.8f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (callback != null) button.onClick.AddListener(callback);
            Text("Label", image.transform, Vector2.zero, size - new Vector2(14, 6), content, fontSize, Color.white, TextAnchor.MiddleCenter);
            return button;
        }
        public static Canvas WorldCanvas(string name, Transform parent, Vector2 size, Camera camera)
        {
            var rect = Rect(name, parent, Vector2.zero, size);
            var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            rect.gameObject.AddComponent<GraphicRaycaster>(); return canvas;
        }

        // Fit camera-held UI inside a viewport region, including perspective from its tilt.
        // Re-evaluate when the window or FOV changes; never allocate per-frame corner arrays.
        public static void FitHeldCanvas(Canvas canvas, Camera camera, Rect content, Rect viewport,
            Vector2 alignment, float depth, float maximumScale)
        {
            var rotation = canvas.transform.localRotation;
            float tangent = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float scale = maximumScale;
            float minX = 0, maxX = 0, minY = 0, maxY = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                minX = minY = float.PositiveInfinity; maxX = maxY = float.NegativeInfinity;
                for (int i = 0; i < 4; i++)
                {
                    var corner = rotation * new Vector3((i & 1) == 0 ? content.xMin : content.xMax,
                        (i & 2) == 0 ? content.yMin : content.yMax, 0) * scale;
                    float z = Mathf.Max(camera.nearClipPlane + .001f, depth + corner.z);
                    float x = corner.x / (2 * z * tangent * camera.aspect);
                    float y = corner.y / (2 * z * tangent);
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                float fit = Mathf.Min(viewport.width / (maxX - minX), viewport.height / (maxY - minY));
                if (fit >= 1) break;
                scale *= fit * .98f;
            }
            float xOffset = Mathf.Lerp(viewport.xMin - minX, viewport.xMax - maxX, alignment.x) - .5f;
            float yOffset = Mathf.Lerp(viewport.yMin - minY, viewport.yMax - maxY, alignment.y) - .5f;
            canvas.transform.localScale = Vector3.one * scale;
            canvas.transform.localPosition = new Vector3(xOffset * 2 * depth * tangent * camera.aspect,
                yOffset * 2 * depth * tangent, depth);
        }

        public static ScrollRect MakeScrollable(Text text)
        {
            var content = text.rectTransform;
            var viewport = Rect(text.name + " viewport", content.parent, content.anchoredPosition, content.sizeDelta);
            viewport.anchorMin = content.anchorMin; viewport.anchorMax = content.anchorMax;
            viewport.pivot = content.pivot;
            viewport.SetSiblingIndex(content.GetSiblingIndex());
            viewport.gameObject.AddComponent<RectMask2D>();
            var hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, content.sizeDelta.y);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var bar = Rect(text.name + " scrollbar", viewport.parent,
                viewport.anchoredPosition + new Vector2(viewport.sizeDelta.x * .5f + 12, 0),
                new Vector2(8, viewport.sizeDelta.y));
            bar.anchorMin = viewport.anchorMin; bar.anchorMax = viewport.anchorMax; bar.pivot = viewport.pivot;
            var handle = Box("Handle", bar, Vector2.zero, Vector2.zero, new Color(.65f, .75f, .7f, .7f));
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            handle.raycastTarget = true;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        public static string Clock(RoutineState state)
        {
            int seconds = 8 * 3600 - Mathf.CeilToInt(state.Remaining);
            return (seconds / 3600).ToString("00") + ":" + (seconds / 60 % 60).ToString("00");
        }
        public static string Remaining(RoutineState state)
        {
            int seconds = Mathf.CeilToInt(state.Remaining);
            return (seconds / 60).ToString("0") + ":" + (seconds % 60).ToString("00");
        }
        public static void Cat(Transform parent, Vector2 position, float scale = 1)
        {
            var root = Rect("Cat illustration", parent, position, new Vector2(240, 150)); root.localScale = Vector3.one * scale;
            Color fur = new Color(0.79f, 0.54f, 0.36f);
            Box("Body", root, new Vector2(0, -20), new Vector2(145, 82), fur);
            var head = Box("Head", root, new Vector2(-25, 24), new Vector2(99, 82), fur);
            Box("Ear", head.transform, new Vector2(-33, 34), new Vector2(35, 35), fur, false).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Box("Ear", head.transform, new Vector2(33, 34), new Vector2(35, 35), fur, false).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Box("Eye", head.transform, new Vector2(-22, 3), new Vector2(7, 14), Ink);
            Box("Eye", head.transform, new Vector2(22, 3), new Vector2(7, 14), Ink);
            Box("Nose", head.transform, new Vector2(0, -15), new Vector2(12, 8), new Color(0.94f, 0.65f, 0.63f));
            Box("Tail", root, new Vector2(85, 0), new Vector2(75, 18), fur).transform.localRotation = Quaternion.Euler(0, 0, 32);
        }
    }
}
