using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LobotomyMVP.Presentation
{
    /// Фабрика UI-элементов: всё строится из кода, шрифт встроенный.
    public static class UiKit
    {
        private static Font _font;
        public static Font Font => _font ?? (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static GameObject Root(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            go.GetComponent<Image>().color = bg;
            return go;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static RectTransform Place(Transform parent, float x, float y, float w, float h)
        {
            var rt = (RectTransform)parent;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 float x, float y, float w, float h, bool alignRight = false)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = color; t.text = text;
            t.alignment = alignRight ? TextAnchor.MiddleRight : TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string text, int size, Color bg, Color fg,
                                    float x, float y, float w, float h, System.Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = bg;
            var b = go.GetComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(() => onClick());
            var lblGo = new GameObject("T", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)lblGo.transform;
            Stretch(lrt);
            var t = lblGo.GetComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = fg; t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        /// Горизонтальная полоса; возвращает Image fill для обновления sizeDelta.
        public static Image Bar(Transform parent, float x, float y, float w, float h, Color bg, Color fill,
                                out RectTransform fillRt)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = bg;

            var fgo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fgo.transform.SetParent(go.transform, false);
            fillRt = (RectTransform)fgo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            fgo.GetComponent<Image>().color = fill;
            fgo.GetComponent<Image>().type = Image.Type.Filled;
            fgo.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;
            var img = fgo.GetComponent<Image>();
            return img;
        }

        public static void SetBar(Image fill, float ratio)
        {
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;
            fill.fillAmount = ratio;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.GetComponent<EventSystem>().tag = "Untagged";
        }

        public static Canvas CreateCanvas(int sortOrder)
        {
            var go = new GameObject("LobotomyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            return c;
        }
    }
}
