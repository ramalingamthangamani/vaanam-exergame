using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Vaanam.UI
{
    /// <summary>
    /// Small helpers to build UGUI in code. The whole HUD is created at runtime
    /// from these, which keeps the scene file tiny and lets the game run even in
    /// an empty scene with just the components attached.
    ///
    /// Child UI rule: BIG. Nothing interactive smaller than 120 px, text sizes
    /// start at 48, and all child-facing words are pictures (see L10n).
    /// </summary>
    public static class UiFactory
    {
        private static Font _font;
        private static Sprite _white, _rounded;

        public static Font DefaultFont()
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Set anchors in one call. Position and size are relative to the anchor.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(string name, Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont(); t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, 0f, 0f, 0.5f); shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button Button(string name, Transform parent, string label, UnityAction onClick, Vector2 size, Color? bg = null, int fontSize = 36)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite(); img.type = UnityEngine.UI.Image.Type.Sliced; img.color = bg ?? new Color(0.25f, 0.55f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors; colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var txt = Text("Label", rt, label, fontSize, Color.white);
            Stretch((RectTransform)txt.transform, 8f);
            return btn;
        }

        public static Sprite WhiteSprite()
        {
            if (_white != null) return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _white;
        }

        /// <summary>A 9-sliced rounded rectangle, generated once.</summary>
        public static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;
            const int S = 64, R = 22;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(R - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _rounded = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2));
            return _rounded;
        }

        /// <summary>Vertical gradient sprite (bottom colour to top colour).</summary>
        public static Sprite GradientSprite(Color bottom, Color top)
        {
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                Color c = Color.Lerp(bottom, top, y / 63f);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Picture of a phrase, or a plain English text fallback if the PNG is missing.</summary>
        public static RectTransform PhraseImage(string name, Transform parent, string key, Vector2 size, out Image image, out Text fallback)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;
            image = rt.gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false;
            fallback = Text("Fallback", rt, "", Mathf.RoundToInt(size.y * 0.5f), Color.white);
            Stretch((RectTransform)fallback.transform);
            SetPhrase(image, fallback, key);
            return rt;
        }

        public static void SetPhrase(Image image, Text fallback, string key)
        {
            Sprite s = string.IsNullOrEmpty(key) ? null : Core.L10n.TextSprite(key);
            image.sprite = s;
            image.enabled = s != null;
            if (fallback != null)
            {
                fallback.enabled = s == null && !string.IsNullOrEmpty(key);
                fallback.text = s == null && !string.IsNullOrEmpty(key) ? Core.L10n.Text(key, Core.Language.English) : "";
            }
        }
    }
}
